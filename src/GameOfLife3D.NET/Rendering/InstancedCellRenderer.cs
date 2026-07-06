using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

[StructLayout(LayoutKind.Sequential)]
public struct InstanceData
{
    public Vector3 Position;
    public float GenerationT;
}

public sealed class InstancedCellRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<CellShape, IInstancedMesh> _meshes = new();
    private uint _instanceVbo;
    private int _maxInstances;
    private int _instanceCount;

    // Dirty instance range [start, end) pending upload. Union of all ranges
    // marked since the last upload; int.MaxValue start means "clean".
    private int _dirtyStart = int.MaxValue;
    private int _dirtyEnd;

    // Pre-allocated buffer
    private InstanceData[] _instanceBuffer = [];

    public int InstanceCount => _instanceCount;

    /// <summary>
    /// Read-only view of the registered shape meshes, used by
    /// ShapeThumbnailRenderer at startup to render each mesh once into a
    /// thumbnail texture.
    /// </summary>
    public IReadOnlyDictionary<CellShape, IInstancedMesh> GetMeshes() => _meshes;

    public InstancedCellRenderer(GL gl)
    {
        _gl = gl;
    }

    public void Initialize(int maxInstances = 4_000_000)
    {
        _maxInstances = maxInstances;
        _instanceBuffer = new InstanceData[maxInstances];

        _meshes[CellShape.Cube] = new CubeMesh(_gl);
        _meshes[CellShape.BeveledCube] = new BeveledCubeMesh(_gl);
        _meshes[CellShape.Tetrahedron] = new Meshes.TetrahedronMesh(_gl);
        _meshes[CellShape.Octahedron] = new Meshes.OctahedronMesh(_gl);
        _meshes[CellShape.SquarePyramid] = new Meshes.SquarePyramidMesh(_gl);
        _meshes[CellShape.Icosahedron] = new Meshes.IcosahedronMesh(_gl);
        _meshes[CellShape.Dodecahedron] = new Meshes.DodecahedronMesh(_gl);
        _meshes[CellShape.Sphere] = new Meshes.IcosphereMesh(_gl);
        _meshes[CellShape.Capsule] = new Meshes.CapsuleMesh(_gl);

        _instanceVbo = _gl.GenBuffer();

        // Allocate instance VBO
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(maxInstances * Marshal.SizeOf<InstanceData>()),
                null, BufferUsageARB.DynamicDraw);
        }

        foreach (var mesh in _meshes.Values)
            BindInstanceAttributesToVAO(mesh.Vao);
    }

    private unsafe void BindInstanceAttributesToVAO(uint vao)
    {
        _gl.BindVertexArray(vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);

        uint stride = (uint)Marshal.SizeOf<InstanceData>();

        // Instance Position: location 2
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.VertexAttribDivisor(2, 1);

        // Instance GenerationT: location 3
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(3, 1, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
        _gl.VertexAttribDivisor(3, 1);

        _gl.BindVertexArray(0);
    }

    public InstanceData[] GetInstanceBuffer() => _instanceBuffer;

    /// <summary>
    /// Swaps in a fully written replacement buffer (same capacity) and returns
    /// the previous one for reuse as the next staging buffer. Used by the
    /// background full-rebuild path; caller must follow up with
    /// <see cref="SetInstanceCount(int)"/> to mark the new contents dirty.
    /// </summary>
    public InstanceData[] SwapInstanceBuffer(InstanceData[] newBuffer)
    {
        var old = _instanceBuffer;
        _instanceBuffer = newBuffer;
        return old;
    }

    public int MaxInstances => _maxInstances;

    public void SetInstanceCount(int count)
    {
        _instanceCount = Math.Min(count, _maxInstances);
        MarkDirty(0, _instanceCount);
    }

    /// <summary>
    /// Sets the instance count when only <c>[dirtyStart, dirtyEnd)</c> of the
    /// buffer was modified, so the next upload transfers just that span.
    /// Pass an empty range for pure count changes (e.g. truncation) that
    /// need no upload at all.
    /// </summary>
    public void SetInstanceCount(int count, int dirtyStart, int dirtyEnd)
    {
        _instanceCount = Math.Min(count, _maxInstances);
        MarkDirty(dirtyStart, Math.Min(dirtyEnd, _instanceCount));
    }

    private void MarkDirty(int start, int endExclusive)
    {
        if (endExclusive <= start) return;
        _dirtyStart = Math.Min(_dirtyStart, start);
        _dirtyEnd = Math.Max(_dirtyEnd, endExclusive);
    }

    private unsafe void UploadIfDirty()
    {
        if (_instanceCount == 0) return;

        // Clamp to the drawn range; data past _instanceCount is never sampled
        // and whichever path grows the count re-marks the region it wrote.
        int start = Math.Min(_dirtyStart, _instanceCount);
        int end = Math.Min(_dirtyEnd, _instanceCount);
        if (end <= start) return;

        long uploadStart = System.Diagnostics.Stopwatch.GetTimestamp();
        int stride = Marshal.SizeOf<InstanceData>();
        long bytes = (long)(end - start) * stride;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        fixed (InstanceData* ptr = _instanceBuffer)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, (nint)start * stride, (nuint)bytes, ptr + start);
        }
        _dirtyStart = int.MaxValue;
        _dirtyEnd = 0;

        RenderPerfStats.LastUploadMs = System.Diagnostics.Stopwatch.GetElapsedTime(uploadStart).TotalMilliseconds;
        RenderPerfStats.LastUploadBytes = bytes;
    }

    private void GetActiveMesh(RenderSettings settings, CellShape? overrideShape, out uint vao, out uint indexCount)
    {
        CellShape effective = CellMeshGeometryFactory.ResolveRenderShape(overrideShape ?? settings.Shape, _instanceCount);

        IInstancedMesh mesh = _meshes.TryGetValue(effective, out var m)
            ? m
            : _meshes[CellShape.Cube]; // defensive fallback for unknown enum values

        vao = mesh.Vao;
        indexCount = mesh.IndexCount;
    }

    public void RenderSolid(ShaderProgram shader, Matrix4x4 view, Matrix4x4 proj, float time, RenderSettings settings, CellShape? overrideShape = null)
    {
        if (_instanceCount == 0 || _meshes.Count == 0) return;
        UploadIfDirty();

        shader.Use();
        shader.SetUniform("uView", view);
        shader.SetUniform("uProjection", proj);
        shader.SetUniform("uCellSize", 1.0f - settings.CellPadding);
        shader.SetUniform("uColorCycling", settings.FaceColorCycling);
        shader.SetUniform("uSolidColor", settings.CellColor);
        shader.SetUniform("uTime", time);
        shader.SetUniform("uLightDir", Vector3.Normalize(new Vector3(1f, 1f, 0.5f)));
        UploadGradientUniforms(shader, settings);

        // Fog
        shader.SetUniform("uFogEnabled", settings.FogEnabled);
        shader.SetUniform("uFogStart", settings.FogStart);
        shader.SetUniform("uFogEnd", settings.FogEnd);
        shader.SetUniform("uFogColor", settings.FogColor);

        // Clip plane
        shader.SetUniform("uClipEnabled", settings.ClipEnabled);
        shader.SetUniform("uClipY", settings.ClipY);

        // Generation fade-in
        shader.SetUniform("uFadeGeneration", settings.FadeGeneration);
        shader.SetUniform("uFadeOpacity", settings.FadeOpacity);
        shader.SetUniform("uGlobalAlpha", settings.GlobalAlpha);

        GetActiveMesh(settings, overrideShape, out uint vao, out uint indexCount);
        _gl.BindVertexArray(vao);
        unsafe
        {
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, indexCount,
                DrawElementsType.UnsignedInt, null, (uint)_instanceCount);
        }
        _gl.BindVertexArray(0);
    }

    public void RenderWireframe(ShaderProgram shader, Matrix4x4 view, Matrix4x4 proj, float time, RenderSettings settings)
    {
        if (_instanceCount == 0 || _meshes.Count == 0 || !settings.ShowWireframe) return;
        UploadIfDirty();

        shader.Use();
        shader.SetUniform("uView", view);
        shader.SetUniform("uProjection", proj);
        shader.SetUniform("uCellSize", 1.0f - settings.CellPadding);
        shader.SetUniform("uColorCycling", settings.EdgeColorCycling);
        shader.SetUniform("uEdgeColor", settings.EdgeColor);
        shader.SetUniform("uTime", time);
        shader.SetUniform("uHueAngle", settings.EdgeColorAngle);
        UploadGradientUniforms(shader, settings);

        // Fog
        shader.SetUniform("uFogEnabled", settings.FogEnabled);
        shader.SetUniform("uFogStart", settings.FogStart);
        shader.SetUniform("uFogEnd", settings.FogEnd);
        shader.SetUniform("uFogColor", settings.FogColor);

        // Clip plane
        shader.SetUniform("uClipEnabled", settings.ClipEnabled);
        shader.SetUniform("uClipY", settings.ClipY);

        // Generation fade-in
        shader.SetUniform("uFadeGeneration", settings.FadeGeneration);
        shader.SetUniform("uFadeOpacity", settings.FadeOpacity);
        shader.SetUniform("uGlobalAlpha", settings.GlobalAlpha);

        _gl.Enable(EnableCap.PolygonOffsetLine);
        _gl.PolygonOffset(-1f, -1f);
        _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);

        GetActiveMesh(settings, null, out uint vao, out uint indexCount);
        _gl.BindVertexArray(vao);
        unsafe
        {
            _gl.DrawElementsInstanced(PrimitiveType.Triangles, indexCount,
                DrawElementsType.UnsignedInt, null, (uint)_instanceCount);
        }
        _gl.BindVertexArray(0);

        _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        _gl.Disable(EnableCap.PolygonOffsetLine);
    }

    // Pre-built indexed uniform names so the per-frame upload path doesn't
    // allocate via string interpolation. Length matches RenderSettings.MaxGradientStops.
    private static readonly string[] GradientUniformNames = BuildGradientUniformNames();

    private static string[] BuildGradientUniformNames()
    {
        var names = new string[RenderSettings.MaxGradientStops];
        for (int i = 0; i < names.Length; i++)
            names[i] = $"uGradientColors[{i}]";
        return names;
    }

    /// <summary>
    /// Uploads the user-editable gradient palette to the supplied shader. Called
    /// once per draw call from both the face and wireframe render paths so each
    /// shader program ends up with a complete copy. Always sends
    /// <see cref="RenderSettings.MaxGradientStops"/> slots; padding the unused
    /// tail with the last valid color guarantees that any out-of-range read
    /// (e.g. a stale count uniform after a hot-reload) degenerates to a no-op
    /// rather than rendering black.
    /// </summary>
    private static void UploadGradientUniforms(ShaderProgram shader, RenderSettings settings)
    {
        // Defense-in-depth: the UI and persistence boundaries enforce >= MinGradientStops,
        // but a programmatic mutation or future bug could leave the list null/short. Falling
        // back to DefaultGradientStops keeps the renderer well-defined without mutating the
        // caller's settings — the UI will repair it on its next pass.
        IReadOnlyList<Vector3> stops = settings.GradientStops;
        if (stops is null || stops.Count < RenderSettings.MinGradientStops)
            stops = RenderSettings.DefaultGradientStops;

        int count = Math.Min(stops.Count, RenderSettings.MaxGradientStops);
        Vector3 last = stops[count - 1];
        for (int i = 0; i < RenderSettings.MaxGradientStops; i++)
        {
            Vector3 color = i < count ? stops[i] : last;
            shader.SetUniform(GradientUniformNames[i], color);
        }
        shader.SetUniform("uGradientStopCount", count);
    }

    public void Dispose()
    {
        foreach (var mesh in _meshes.Values)
            mesh.Dispose();
        _meshes.Clear();
        if (_instanceVbo != 0)
            _gl.DeleteBuffer(_instanceVbo);
    }
}

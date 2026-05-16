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
    private bool _dirty;

    // Pre-allocated buffer
    private InstanceData[] _instanceBuffer = [];

    // Performance guard: when the chosen shape is BeveledCube and the live
    // instance count exceeds this threshold, fall back to the plain cube mesh.
    // Other shapes (added by later tasks) have no fallback — they always
    // render as themselves.
    private const int BeveledMaxInstances = 500_000;

    public int InstanceCount => _instanceCount;

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

    public int MaxInstances => _maxInstances;

    public void SetInstanceCount(int count)
    {
        _instanceCount = Math.Min(count, _maxInstances);
        _dirty = true;
    }

    private unsafe void UploadIfDirty()
    {
        if (!_dirty || _instanceCount == 0) return;

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        fixed (InstanceData* ptr = _instanceBuffer)
        {
            _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                (nuint)(_instanceCount * Marshal.SizeOf<InstanceData>()), ptr);
        }
        _dirty = false;
    }

    private void GetActiveMesh(RenderSettings settings, out uint vao, out uint indexCount)
    {
        CellShape effective = settings.Shape;
        // Beveled cube falls back to plain cube above the LOD threshold —
        // preserves the original BeveledMaxInstances behavior.
        if (effective == CellShape.BeveledCube && _instanceCount > BeveledMaxInstances)
            effective = CellShape.Cube;

        IInstancedMesh mesh = _meshes.TryGetValue(effective, out var m)
            ? m
            : _meshes[CellShape.Cube]; // defensive fallback for unknown enum values

        vao = mesh.Vao;
        indexCount = mesh.IndexCount;
    }

    public void RenderSolid(ShaderProgram shader, Matrix4x4 view, Matrix4x4 proj, float time, RenderSettings settings)
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

        GetActiveMesh(settings, out uint vao, out uint indexCount);
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

        _gl.Enable(EnableCap.PolygonOffsetLine);
        _gl.PolygonOffset(-1f, -1f);
        _gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line);

        GetActiveMesh(settings, out uint vao, out uint indexCount);
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

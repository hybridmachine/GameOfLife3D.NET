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

public sealed class InstancedCubeRenderer : IDisposable
{
    private readonly GL _gl;
    private CubeMesh? _cubeMesh;
    private BeveledCubeMesh? _beveledMesh;
    private uint _instanceVbo;
    private int _maxInstances;
    private int _instanceCount;
    private bool _dirty;

    // Pre-allocated buffer
    private InstanceData[] _instanceBuffer = [];

    // Performance guard: disable beveled cubes above this threshold
    private const int BeveledMaxInstances = 500_000;

    public int InstanceCount => _instanceCount;

    public InstancedCubeRenderer(GL gl)
    {
        _gl = gl;
    }

    public void Initialize(int maxInstances = 4_000_000)
    {
        _maxInstances = maxInstances;
        _instanceBuffer = new InstanceData[maxInstances];

        _cubeMesh = new CubeMesh(_gl);
        _beveledMesh = new BeveledCubeMesh(_gl);

        _instanceVbo = _gl.GenBuffer();

        // Allocate instance VBO
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _instanceVbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(maxInstances * Marshal.SizeOf<InstanceData>()),
                null, BufferUsageARB.DynamicDraw);
        }

        // Bind instance attributes to both VAOs
        BindInstanceAttributesToVAO(_cubeMesh.Vao);
        BindInstanceAttributesToVAO(_beveledMesh.Vao);
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
        bool useBeveled = settings.UseBeveledCubes && _instanceCount <= BeveledMaxInstances && _beveledMesh != null;
        if (useBeveled)
        {
            vao = _beveledMesh!.Vao;
            indexCount = _beveledMesh.IndexCount;
        }
        else
        {
            vao = _cubeMesh!.Vao;
            indexCount = _cubeMesh.IndexCount;
        }
    }

    public void RenderSolid(ShaderProgram shader, Matrix4x4 view, Matrix4x4 proj, float time, RenderSettings settings)
    {
        if (_instanceCount == 0 || _cubeMesh == null) return;
        UploadIfDirty();

        shader.Use();
        shader.SetUniform("uView", view);
        shader.SetUniform("uProjection", proj);
        shader.SetUniform("uCellSize", 1.0f - settings.CellPadding);
        shader.SetUniform("uColorCycling", settings.FaceColorCycling);
        shader.SetUniform("uSolidColor", settings.CellColor);
        shader.SetUniform("uTime", time);
        shader.SetUniform("uLightDir", Vector3.Normalize(new Vector3(1f, 1f, 0.5f)));

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
        if (_instanceCount == 0 || _cubeMesh == null || !settings.ShowWireframe) return;
        UploadIfDirty();

        shader.Use();
        shader.SetUniform("uView", view);
        shader.SetUniform("uProjection", proj);
        shader.SetUniform("uCellSize", 1.0f - settings.CellPadding);
        shader.SetUniform("uColorCycling", settings.EdgeColorCycling);
        shader.SetUniform("uEdgeColor", settings.EdgeColor);
        shader.SetUniform("uTime", time);
        shader.SetUniform("uHueAngle", settings.EdgeColorAngle);

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

    public void Dispose()
    {
        _cubeMesh?.Dispose();
        _beveledMesh?.Dispose();
        if (_instanceVbo != 0)
            _gl.DeleteBuffer(_instanceVbo);
    }
}

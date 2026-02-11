using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class CubeMesh : IDisposable
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount => 36;

    // Unit cube centered at origin: positions + normals
    private static readonly float[] Vertices =
    [
        // Front face (Z+)
        -0.5f, -0.5f,  0.5f,   0f,  0f,  1f,
         0.5f, -0.5f,  0.5f,   0f,  0f,  1f,
         0.5f,  0.5f,  0.5f,   0f,  0f,  1f,
        -0.5f,  0.5f,  0.5f,   0f,  0f,  1f,
        // Back face (Z-)
        -0.5f, -0.5f, -0.5f,   0f,  0f, -1f,
        -0.5f,  0.5f, -0.5f,   0f,  0f, -1f,
         0.5f,  0.5f, -0.5f,   0f,  0f, -1f,
         0.5f, -0.5f, -0.5f,   0f,  0f, -1f,
        // Top face (Y+)
        -0.5f,  0.5f, -0.5f,   0f,  1f,  0f,
        -0.5f,  0.5f,  0.5f,   0f,  1f,  0f,
         0.5f,  0.5f,  0.5f,   0f,  1f,  0f,
         0.5f,  0.5f, -0.5f,   0f,  1f,  0f,
        // Bottom face (Y-)
        -0.5f, -0.5f, -0.5f,   0f, -1f,  0f,
         0.5f, -0.5f, -0.5f,   0f, -1f,  0f,
         0.5f, -0.5f,  0.5f,   0f, -1f,  0f,
        -0.5f, -0.5f,  0.5f,   0f, -1f,  0f,
        // Right face (X+)
         0.5f, -0.5f, -0.5f,   1f,  0f,  0f,
         0.5f,  0.5f, -0.5f,   1f,  0f,  0f,
         0.5f,  0.5f,  0.5f,   1f,  0f,  0f,
         0.5f, -0.5f,  0.5f,   1f,  0f,  0f,
        // Left face (X-)
        -0.5f, -0.5f, -0.5f,  -1f,  0f,  0f,
        -0.5f, -0.5f,  0.5f,  -1f,  0f,  0f,
        -0.5f,  0.5f,  0.5f,  -1f,  0f,  0f,
        -0.5f,  0.5f, -0.5f,  -1f,  0f,  0f,
    ];

    private static readonly uint[] Indices =
    [
         0,  1,  2,   2,  3,  0, // Front
         4,  5,  6,   6,  7,  4, // Back
         8,  9, 10,  10, 11,  8, // Top
        12, 13, 14,  14, 15, 12, // Bottom
        16, 17, 18,  18, 19, 16, // Right
        20, 21, 22,  22, 23, 20, // Left
    ];

    public CubeMesh(GL gl)
    {
        _gl = gl;
        Initialize();
    }

    private unsafe void Initialize()
    {
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        // Vertex data
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = Vertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(Vertices.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);

        // Index data
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* ptr = Indices)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(Indices.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);

        uint stride = 6 * sizeof(float);

        // Position: location 0
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

        // Normal: location 1
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class GridRenderer : IDisposable
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private int _vertexCount;
    private int _currentGridSize = -1;

    public GridRenderer(GL gl)
    {
        _gl = gl;
        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
    }

    public void UpdateGrid(int gridSize)
    {
        if (gridSize == _currentGridSize) return;
        _currentGridSize = gridSize;

        float halfSize = gridSize / 2f;
        var points = new List<float>();

        for (int i = 0; i <= gridSize; i++)
        {
            float pos = i - halfSize;
            // Line along Z
            points.AddRange([pos, 0, -halfSize]);
            points.AddRange([pos, 0, halfSize]);
            // Line along X
            points.AddRange([-halfSize, 0, pos]);
            points.AddRange([halfSize, 0, pos]);
        }

        _vertexCount = points.Count / 3;

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            var data = points.ToArray();
            fixed (float* ptr = data)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(data.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);
        }

        _gl.EnableVertexAttribArray(0);
        unsafe { _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0); }

        _gl.BindVertexArray(0);
    }

    public void Render(ShaderProgram shader, Matrix4x4 view, Matrix4x4 proj)
    {
        if (_vertexCount == 0) return;

        shader.Use();
        shader.SetUniform("uView", view);
        shader.SetUniform("uProjection", proj);
        shader.SetUniform("uColor", new Vector4(0.533f, 0.533f, 0.533f, 0.8f));

        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Lines, 0, (uint)_vertexCount);
        _gl.BindVertexArray(0);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
    }
}

using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

public sealed class SquarePyramidMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; }

    public SquarePyramidMesh(GL gl)
    {
        _gl = gl;
        var geometry = CellMeshGeometryFactory.GetGeometry(CellShape.SquarePyramid);
        IndexCount = geometry.IndexCount;
        MeshBuilder.UploadAndBind(geometry, out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

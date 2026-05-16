using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Square-base pyramid. Apex at (0, 0.5, 0), base square on Y=-0.5 spanning
/// ±0.5 in X and Z. Base is included so the underside isn't see-through.
/// </summary>
public sealed class SquarePyramidMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public SquarePyramidMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        var apex = (0f, 0.5f, 0f);
        var bl = (-0.5f, -0.5f, -0.5f);
        var br = ( 0.5f, -0.5f, -0.5f);
        var tr = ( 0.5f, -0.5f,  0.5f);
        var tl = (-0.5f, -0.5f,  0.5f);

        // Side triangles — normal hint points outward-and-slightly-up.
        MeshBuilder.AddTriangle(verts, idx, apex, bl, br, ( 0f,  0.5f, -1f));  // front (-Z)
        MeshBuilder.AddTriangle(verts, idx, apex, br, tr, ( 1f,  0.5f,  0f));  // right (+X)
        MeshBuilder.AddTriangle(verts, idx, apex, tr, tl, ( 0f,  0.5f,  1f));  // back (+Z)
        MeshBuilder.AddTriangle(verts, idx, apex, tl, bl, (-1f,  0.5f,  0f));  // left (-X)

        // Base — single quad facing -Y
        MeshBuilder.AddQuad(verts, idx, bl, br, tr, tl, (0f, -1f, 0f));

        IndexCount = (uint)idx.Count;
        MeshBuilder.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

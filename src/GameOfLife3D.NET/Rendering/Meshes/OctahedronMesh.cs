using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Octahedron with vertices at ±0.5 along each axis. 8 triangular faces, flat-
/// shaded. Reads as a sharp crystal — points along Y up/down line up with the
/// generation axis.
/// </summary>
public sealed class OctahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public OctahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        var px = ( 0.5f,  0f,  0f);
        var nx = (-0.5f,  0f,  0f);
        var py = ( 0f,  0.5f,  0f);
        var ny = ( 0f, -0.5f,  0f);
        var pz = ( 0f,  0f,  0.5f);
        var nz = ( 0f,  0f, -0.5f);

        // 8 triangular faces, one per (±x,±y,±z) octant. The third vertex on
        // each face is the axis-Y vertex matching the y-sign; normal hint is
        // the octant-corner direction. MeshBuilder.AddTriangle auto-corrects
        // winding.
        void Face(
            (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c,
            float nxs, float nys, float nzs)
            => MeshBuilder.AddTriangle(verts, idx, a, b, c, (nxs, nys, nzs));

        Face(px, pz, py,  1f,  1f,  1f);
        Face(px, py, nz,  1f,  1f, -1f);
        Face(px, ny, pz,  1f, -1f,  1f);
        Face(px, nz, ny,  1f, -1f, -1f);
        Face(nx, py, pz, -1f,  1f,  1f);
        Face(nx, nz, py, -1f,  1f, -1f);
        Face(nx, pz, ny, -1f, -1f,  1f);
        Face(nx, ny, nz, -1f, -1f, -1f);

        IndexCount = (uint)idx.Count;
        MeshBuilder.UploadAndBind(verts.ToArray(), idx.ToArray(), out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

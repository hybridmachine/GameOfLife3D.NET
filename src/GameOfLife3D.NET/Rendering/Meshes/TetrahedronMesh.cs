using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Regular tetrahedron inscribed in the unit cube (±0.5 extent). Uses the four
/// "alternating" corners of the cube as vertices, which guarantees the result
/// fits the cell footprint exactly. Flat-shaded — each face owns its own
/// vertex triples with the face normal.
/// </summary>
public sealed class TetrahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public TetrahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private unsafe void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        // Four alternating corners of the unit cube
        var a = ( 0.5f,  0.5f,  0.5f);
        var b = ( 0.5f, -0.5f, -0.5f);
        var c = (-0.5f,  0.5f, -0.5f);
        var d = (-0.5f, -0.5f,  0.5f);

        // Normal hints = direction from origin to face centroid (tetrahedron is
        // convex and centered at origin, so centroid direction is outward).
        // MeshBuilder.AddTriangle corrects winding to match.
        MeshBuilder.AddTriangle(verts, idx, a, b, c, Centroid(a, b, c));
        MeshBuilder.AddTriangle(verts, idx, a, d, b, Centroid(a, d, b));
        MeshBuilder.AddTriangle(verts, idx, a, c, d, Centroid(a, c, d));
        MeshBuilder.AddTriangle(verts, idx, b, d, c, Centroid(b, d, c));

        IndexCount = (uint)idx.Count;

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        var vertArr = verts.ToArray();
        var idxArr = idx.ToArray();

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* p = vertArr)
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertArr.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* p = idxArr)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(idxArr.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        uint stride = 6 * sizeof(float);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        _gl.BindVertexArray(0);
    }

    private static (float, float, float) Centroid(
        (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c)
        => ((a.X + b.X + c.X) / 3f, (a.Y + b.Y + c.Y) / 3f, (a.Z + b.Z + c.Z) / 3f);

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

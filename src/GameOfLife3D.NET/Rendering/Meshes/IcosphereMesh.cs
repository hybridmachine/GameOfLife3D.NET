using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Sphere built by subdividing an icosahedron once (each triangle → 4) and
/// projecting every vertex onto the bounding sphere (radius 0.5). 80 tris.
/// Smooth-shaded: vertex normal = normalize(vertex position) since the sphere
/// is centered at the origin.
/// </summary>
public sealed class IcosphereMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public IcosphereMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        const float Radius = 0.5f;

        // Start with the base icosahedron's 12 vertices (already scaled so
        // their magnitudes match Radius). Each face is 3 indices in BaseFaces.
        var positions = new List<Vector3>();
        foreach (var (x, y, z) in IcosahedronMesh.BaseVertices)
            positions.Add(new Vector3(x, y, z));

        var faces = new List<(int a, int b, int c)>();
        for (int f = 0; f < IcosahedronMesh.BaseFaces.Length; f += 3)
            faces.Add((IcosahedronMesh.BaseFaces[f],
                       IcosahedronMesh.BaseFaces[f + 1],
                       IcosahedronMesh.BaseFaces[f + 2]));

        // One subdivision pass.
        var midpointCache = new Dictionary<(int, int), int>();
        int Midpoint(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (midpointCache.TryGetValue(key, out var idx)) return idx;
            var mid = Vector3.Normalize((positions[a] + positions[b]) * 0.5f) * Radius;
            positions.Add(mid);
            int newIdx = positions.Count - 1;
            midpointCache[key] = newIdx;
            return newIdx;
        }

        var subdivided = new List<(int, int, int)>(faces.Count * 4);
        foreach (var (a, b, c) in faces)
        {
            int ab = Midpoint(a, b);
            int bc = Midpoint(b, c);
            int ca = Midpoint(c, a);
            subdivided.Add((a, ab, ca));
            subdivided.Add((b, bc, ab));
            subdivided.Add((c, ca, bc));
            subdivided.Add((ab, bc, ca));
        }

        // Build the flat vertex buffer with smooth normals (radial).
        var verts = new List<float>(positions.Count * 6);
        foreach (var p in positions)
        {
            Vector3 n = Vector3.Normalize(p);
            verts.Add(p.X); verts.Add(p.Y); verts.Add(p.Z);
            verts.Add(n.X); verts.Add(n.Y); verts.Add(n.Z);
        }

        var idx = new List<uint>(subdivided.Count * 3);
        foreach (var (a, b, c) in subdivided)
        {
            idx.Add((uint)a);
            idx.Add((uint)b);
            idx.Add((uint)c);
        }

        IndexCount = (uint)idx.Count;
        OctahedronMesh.UploadAndBind(verts.ToArray(), idx.ToArray(),
            out _vao, out _vbo, out _ebo, _gl);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

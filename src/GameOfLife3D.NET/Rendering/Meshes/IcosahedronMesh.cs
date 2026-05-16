using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Regular icosahedron: 12 vertices, 20 triangular faces. Vertices are the
/// canonical golden-ratio construction, scaled so the bounding sphere radius
/// equals 0.5 (max coord magnitude is φ × scale = 0.5).
/// </summary>
public sealed class IcosahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    // Pre-built unit-cube-scaled vertex coordinates and face index triples,
    // exposed for IcosphereMesh (which subdivides them) and DodecahedronMesh
    // (which uses them via the icosa-dual construction).
    internal static readonly (float X, float Y, float Z)[] BaseVertices;
    internal static readonly int[] BaseFaces =
    {
        0, 11,  5,    0,  5,  1,    0,  1,  7,    0,  7, 10,   0, 10, 11,
        1,  5,  9,    5, 11,  4,   11, 10,  2,   10,  7,  6,   7,  1,  8,
        3,  9,  4,    3,  4,  2,    3,  2,  6,    3,  6,  8,   3,  8,  9,
        4,  9,  5,    2,  4, 11,    6,  2, 10,    8,  6,  7,   9,  8,  1,
    };

    static IcosahedronMesh()
    {
        const float Phi = 1.6180339887f;
        const float S = 0.5f / Phi; // unscaled "1" maps to S; unscaled "φ" maps to 0.5
        BaseVertices = new (float, float, float)[]
        {
            (-S, S * Phi, 0f),  // 0
            ( S, S * Phi, 0f),  // 1
            (-S,-S * Phi, 0f),  // 2
            ( S,-S * Phi, 0f),  // 3
            (0f,-S,  S * Phi),  // 4
            (0f, S,  S * Phi),  // 5
            (0f,-S, -S * Phi),  // 6
            (0f, S, -S * Phi),  // 7
            ( S * Phi, 0f, -S), // 8
            ( S * Phi, 0f,  S), // 9
            (-S * Phi, 0f, -S), // 10
            (-S * Phi, 0f,  S), // 11
        };
    }

    public IcosahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        var verts = new List<float>();
        var idx = new List<uint>();

        for (int f = 0; f < BaseFaces.Length; f += 3)
        {
            var a = BaseVertices[BaseFaces[f]];
            var b = BaseVertices[BaseFaces[f + 1]];
            var c = BaseVertices[BaseFaces[f + 2]];
            // Icosahedron is convex around origin → centroid direction is outward.
            var n = ((a.X + b.X + c.X) / 3f,
                     (a.Y + b.Y + c.Y) / 3f,
                     (a.Z + b.Z + c.Z) / 3f);
            MeshBuilder.AddTriangle(verts, idx, a, b, c, n);
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

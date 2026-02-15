using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class BeveledCubeMesh : IDisposable
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    private const float H = 0.5f;    // Half-size
    private const float B = 0.08f;   // Bevel inset

    public BeveledCubeMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private unsafe void Generate()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        float o = H;           // outer extent
        float i = H - B;       // inner extent (beveled)

        // 6 main faces (each is a smaller quad, inset by B on all four edges)
        // Front (Z+)
        AddQuad(vertices, indices,
            (-i, -i, o), (i, -i, o), (i, i, o), (-i, i, o),
            (0, 0, 1));
        // Back (Z-)
        AddQuad(vertices, indices,
            (i, -i, -o), (-i, -i, -o), (-i, i, -o), (i, i, -o),
            (0, 0, -1));
        // Top (Y+)
        AddQuad(vertices, indices,
            (-i, o, i), (i, o, i), (i, o, -i), (-i, o, -i),
            (0, 1, 0));
        // Bottom (Y-)
        AddQuad(vertices, indices,
            (-i, -o, -i), (i, -o, -i), (i, -o, i), (-i, -o, i),
            (0, -1, 0));
        // Right (X+)
        AddQuad(vertices, indices,
            (o, -i, i), (o, -i, -i), (o, i, -i), (o, i, i),
            (1, 0, 0));
        // Left (X-)
        AddQuad(vertices, indices,
            (-o, -i, -i), (-o, -i, i), (-o, i, i), (-o, i, -i),
            (-1, 0, 0));

        // 12 edge bevels
        // Each bevel connects an edge of one main face to the corresponding edge of the adjacent main face.
        float d = 0.707107f; // 1/sqrt(2)

        // Front-Top: connects front face top edge to top face front edge
        AddQuad(vertices, indices,
            (-i, i, o), (i, i, o), (i, o, i), (-i, o, i),
            (0, d, d));
        // Front-Bottom: connects front face bottom edge to bottom face front edge
        AddQuad(vertices, indices,
            (i, -i, o), (-i, -i, o), (-i, -o, i), (i, -o, i),
            (0, -d, d));
        // Front-Right: connects front face right edge to right face front edge
        AddQuad(vertices, indices,
            (i, i, o), (i, -i, o), (o, -i, i), (o, i, i),
            (d, 0, d));
        // Front-Left: connects front face left edge to left face front edge
        AddQuad(vertices, indices,
            (-i, -i, o), (-i, i, o), (-o, i, i), (-o, -i, i),
            (-d, 0, d));
        // Back-Top: connects back face top edge to top face back edge
        AddQuad(vertices, indices,
            (i, i, -o), (-i, i, -o), (-i, o, -i), (i, o, -i),
            (0, d, -d));
        // Back-Bottom: connects back face bottom edge to bottom face back edge
        AddQuad(vertices, indices,
            (-i, -i, -o), (i, -i, -o), (i, -o, -i), (-i, -o, -i),
            (0, -d, -d));
        // Back-Right: connects back face right edge to right face back edge
        AddQuad(vertices, indices,
            (i, -i, -o), (i, i, -o), (o, i, -i), (o, -i, -i),
            (d, 0, -d));
        // Back-Left: connects back face left edge to left face back edge
        AddQuad(vertices, indices,
            (-i, i, -o), (-i, -i, -o), (-o, -i, -i), (-o, i, -i),
            (-d, 0, -d));
        // Top-Right: connects top face right edge to right face top edge
        AddQuad(vertices, indices,
            (i, o, i), (i, o, -i), (o, i, -i), (o, i, i),
            (d, d, 0));
        // Top-Left: connects top face left edge to left face top edge
        AddQuad(vertices, indices,
            (-i, o, -i), (-i, o, i), (-o, i, i), (-o, i, -i),
            (-d, d, 0));
        // Bottom-Right: connects bottom face right edge to right face bottom edge
        AddQuad(vertices, indices,
            (i, -o, -i), (i, -o, i), (o, -i, i), (o, -i, -i),
            (d, -d, 0));
        // Bottom-Left: connects bottom face left edge to left face bottom edge
        AddQuad(vertices, indices,
            (-i, -o, i), (-i, -o, -i), (-o, -i, -i), (-o, -i, i),
            (-d, -d, 0));

        // 8 corner triangles
        // Each corner fills the triangular gap where 3 bevel quads meet.
        float cd = 0.577350f; // 1/sqrt(3)

        // Front-Top-Right (+X, +Y, +Z)
        AddTriangle(vertices, indices,
            (i, i, o), (o, i, i), (i, o, i),
            (cd, cd, cd));
        // Front-Top-Left (-X, +Y, +Z)
        AddTriangle(vertices, indices,
            (-i, i, o), (-i, o, i), (-o, i, i),
            (-cd, cd, cd));
        // Front-Bottom-Right (+X, -Y, +Z)
        AddTriangle(vertices, indices,
            (i, -i, o), (i, -o, i), (o, -i, i),
            (cd, -cd, cd));
        // Front-Bottom-Left (-X, -Y, +Z)
        AddTriangle(vertices, indices,
            (-i, -i, o), (-o, -i, i), (-i, -o, i),
            (-cd, -cd, cd));
        // Back-Top-Right (+X, +Y, -Z)
        AddTriangle(vertices, indices,
            (i, i, -o), (i, o, -i), (o, i, -i),
            (cd, cd, -cd));
        // Back-Top-Left (-X, +Y, -Z)
        AddTriangle(vertices, indices,
            (-i, i, -o), (-o, i, -i), (-i, o, -i),
            (-cd, cd, -cd));
        // Back-Bottom-Right (+X, -Y, -Z)
        AddTriangle(vertices, indices,
            (i, -i, -o), (o, -i, -i), (i, -o, -i),
            (cd, -cd, -cd));
        // Back-Bottom-Left (-X, -Y, -Z)
        AddTriangle(vertices, indices,
            (-i, -i, -o), (-i, -o, -i), (-o, -i, -i),
            (-cd, -cd, -cd));

        IndexCount = (uint)indices.Count;

        _vao = _gl.GenVertexArray();
        _vbo = _gl.GenBuffer();
        _ebo = _gl.GenBuffer();

        _gl.BindVertexArray(_vao);

        var vertArray = vertices.ToArray();
        var idxArray = indices.ToArray();

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* ptr = vertArray)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertArray.Length * sizeof(float)), ptr, BufferUsageARB.StaticDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* ptr = idxArray)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(idxArray.Length * sizeof(uint)), ptr, BufferUsageARB.StaticDraw);

        uint stride = 6 * sizeof(float);

        // Position: location 0
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);

        // Normal: location 1
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        _gl.BindVertexArray(0);
    }

    private static void AddQuad(List<float> verts, List<uint> indices,
        (float X, float Y, float Z) a, (float X, float Y, float Z) b,
        (float X, float Y, float Z) c, (float X, float Y, float Z) d,
        (float X, float Y, float Z) normal)
    {
        // Auto-correct winding: compute cross product and flip if it disagrees with intended normal
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var cross = (
            ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2,
            ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3,
            ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        float dot = cross.Item1 * normal.X + cross.Item2 * normal.Y + cross.Item3 * normal.Z;

        if (dot < 0)
        {
            // Winding is backwards - swap b and d to reverse it
            (b, d) = (d, b);
        }

        uint baseIdx = (uint)(verts.Count / 6);

        AddVertex(verts, a, normal);
        AddVertex(verts, b, normal);
        AddVertex(verts, c, normal);
        AddVertex(verts, d, normal);

        indices.Add(baseIdx);
        indices.Add(baseIdx + 1);
        indices.Add(baseIdx + 2);
        indices.Add(baseIdx);
        indices.Add(baseIdx + 2);
        indices.Add(baseIdx + 3);
    }

    private static void AddTriangle(List<float> verts, List<uint> indices,
        (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c,
        (float X, float Y, float Z) normal)
    {
        // Auto-correct winding: compute cross product and flip if it disagrees with intended normal
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var cross = (
            ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2,
            ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3,
            ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        float dot = cross.Item1 * normal.X + cross.Item2 * normal.Y + cross.Item3 * normal.Z;

        if (dot < 0)
        {
            // Winding is backwards - swap b and c to reverse it
            (b, c) = (c, b);
        }

        uint baseIdx = (uint)(verts.Count / 6);

        AddVertex(verts, a, normal);
        AddVertex(verts, b, normal);
        AddVertex(verts, c, normal);

        indices.Add(baseIdx);
        indices.Add(baseIdx + 1);
        indices.Add(baseIdx + 2);
    }

    private static void AddVertex(List<float> verts, (float X, float Y, float Z) pos, (float X, float Y, float Z) normal)
    {
        verts.Add(pos.X);
        verts.Add(pos.Y);
        verts.Add(pos.Z);
        verts.Add(normal.X);
        verts.Add(normal.Y);
        verts.Add(normal.Z);
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

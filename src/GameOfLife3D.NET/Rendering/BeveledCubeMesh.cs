using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering;

public sealed class BeveledCubeMesh : IInstancedMesh
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
        MeshBuilder.AddQuad(vertices, indices,
            (-i, -i, o), (i, -i, o), (i, i, o), (-i, i, o),
            (0, 0, 1));
        // Back (Z-)
        MeshBuilder.AddQuad(vertices, indices,
            (i, -i, -o), (-i, -i, -o), (-i, i, -o), (i, i, -o),
            (0, 0, -1));
        // Top (Y+)
        MeshBuilder.AddQuad(vertices, indices,
            (-i, o, i), (i, o, i), (i, o, -i), (-i, o, -i),
            (0, 1, 0));
        // Bottom (Y-)
        MeshBuilder.AddQuad(vertices, indices,
            (-i, -o, -i), (i, -o, -i), (i, -o, i), (-i, -o, i),
            (0, -1, 0));
        // Right (X+)
        MeshBuilder.AddQuad(vertices, indices,
            (o, -i, i), (o, -i, -i), (o, i, -i), (o, i, i),
            (1, 0, 0));
        // Left (X-)
        MeshBuilder.AddQuad(vertices, indices,
            (-o, -i, -i), (-o, -i, i), (-o, i, i), (-o, i, -i),
            (-1, 0, 0));

        // 12 edge bevels
        // Each bevel connects an edge of one main face to the corresponding edge of the adjacent main face.
        float d = 0.707107f; // 1/sqrt(2)

        // Front-Top: connects front face top edge to top face front edge
        MeshBuilder.AddQuad(vertices, indices,
            (-i, i, o), (i, i, o), (i, o, i), (-i, o, i),
            (0, d, d));
        // Front-Bottom: connects front face bottom edge to bottom face front edge
        MeshBuilder.AddQuad(vertices, indices,
            (i, -i, o), (-i, -i, o), (-i, -o, i), (i, -o, i),
            (0, -d, d));
        // Front-Right: connects front face right edge to right face front edge
        MeshBuilder.AddQuad(vertices, indices,
            (i, i, o), (i, -i, o), (o, -i, i), (o, i, i),
            (d, 0, d));
        // Front-Left: connects front face left edge to left face front edge
        MeshBuilder.AddQuad(vertices, indices,
            (-i, -i, o), (-i, i, o), (-o, i, i), (-o, -i, i),
            (-d, 0, d));
        // Back-Top: connects back face top edge to top face back edge
        MeshBuilder.AddQuad(vertices, indices,
            (i, i, -o), (-i, i, -o), (-i, o, -i), (i, o, -i),
            (0, d, -d));
        // Back-Bottom: connects back face bottom edge to bottom face back edge
        MeshBuilder.AddQuad(vertices, indices,
            (-i, -i, -o), (i, -i, -o), (i, -o, -i), (-i, -o, -i),
            (0, -d, -d));
        // Back-Right: connects back face right edge to right face back edge
        MeshBuilder.AddQuad(vertices, indices,
            (i, -i, -o), (i, i, -o), (o, i, -i), (o, -i, -i),
            (d, 0, -d));
        // Back-Left: connects back face left edge to left face back edge
        MeshBuilder.AddQuad(vertices, indices,
            (-i, i, -o), (-i, -i, -o), (-o, -i, -i), (-o, i, -i),
            (-d, 0, -d));
        // Top-Right: connects top face right edge to right face top edge
        MeshBuilder.AddQuad(vertices, indices,
            (i, o, i), (i, o, -i), (o, i, -i), (o, i, i),
            (d, d, 0));
        // Top-Left: connects top face left edge to left face top edge
        MeshBuilder.AddQuad(vertices, indices,
            (-i, o, -i), (-i, o, i), (-o, i, i), (-o, i, -i),
            (-d, d, 0));
        // Bottom-Right: connects bottom face right edge to right face bottom edge
        MeshBuilder.AddQuad(vertices, indices,
            (i, -o, -i), (i, -o, i), (o, -i, i), (o, -i, -i),
            (d, -d, 0));
        // Bottom-Left: connects bottom face left edge to left face bottom edge
        MeshBuilder.AddQuad(vertices, indices,
            (-i, -o, i), (-i, -o, -i), (-o, -i, -i), (-o, -i, i),
            (-d, -d, 0));

        // 8 corner triangles
        // Each corner fills the triangular gap where 3 bevel quads meet.
        float cd = 0.577350f; // 1/sqrt(3)

        // Front-Top-Right (+X, +Y, +Z)
        MeshBuilder.AddTriangle(vertices, indices,
            (i, i, o), (o, i, i), (i, o, i),
            (cd, cd, cd));
        // Front-Top-Left (-X, +Y, +Z)
        MeshBuilder.AddTriangle(vertices, indices,
            (-i, i, o), (-i, o, i), (-o, i, i),
            (-cd, cd, cd));
        // Front-Bottom-Right (+X, -Y, +Z)
        MeshBuilder.AddTriangle(vertices, indices,
            (i, -i, o), (i, -o, i), (o, -i, i),
            (cd, -cd, cd));
        // Front-Bottom-Left (-X, -Y, +Z)
        MeshBuilder.AddTriangle(vertices, indices,
            (-i, -i, o), (-o, -i, i), (-i, -o, i),
            (-cd, -cd, cd));
        // Back-Top-Right (+X, +Y, -Z)
        MeshBuilder.AddTriangle(vertices, indices,
            (i, i, -o), (i, o, -i), (o, i, -i),
            (cd, cd, -cd));
        // Back-Top-Left (-X, +Y, -Z)
        MeshBuilder.AddTriangle(vertices, indices,
            (-i, i, -o), (-o, i, -i), (-i, o, -i),
            (-cd, cd, -cd));
        // Back-Bottom-Right (+X, -Y, -Z)
        MeshBuilder.AddTriangle(vertices, indices,
            (i, -i, -o), (o, -i, -i), (i, -o, -i),
            (cd, -cd, -cd));
        // Back-Bottom-Left (-X, -Y, -Z)
        MeshBuilder.AddTriangle(vertices, indices,
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

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }
}

using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

public sealed class CellMeshGeometry
{
    public const int FloatsPerVertex = 6;

    public CellMeshGeometry(float[] vertices, uint[] indices)
    {
        if (vertices.Length % FloatsPerVertex != 0)
            throw new ArgumentException("Vertex buffer must contain position and normal data.", nameof(vertices));

        if (indices.Length % 3 != 0)
            throw new ArgumentException("Index buffer must contain complete triangles.", nameof(indices));

        Vertices = vertices;
        Indices = indices;
    }

    public float[] Vertices { get; }
    public uint[] Indices { get; }
    public int VertexCount => Vertices.Length / FloatsPerVertex;
    public uint IndexCount => (uint)Indices.Length;
    public int TriangleCount => Indices.Length / 3;

    public Vector3 GetPosition(int vertexIndex)
    {
        int offset = vertexIndex * FloatsPerVertex;
        return new Vector3(Vertices[offset], Vertices[offset + 1], Vertices[offset + 2]);
    }

    public Vector3 GetNormal(int vertexIndex)
    {
        int offset = vertexIndex * FloatsPerVertex + 3;
        return new Vector3(Vertices[offset], Vertices[offset + 1], Vertices[offset + 2]);
    }
}

namespace GameOfLife3D.NET.Rendering;

/// <summary>
/// Shared geometry helpers for building IInstancedMesh implementations. Each
/// AddQuad / AddTriangle call appends flat-shaded vertices (position + normal,
/// 6 floats each) and the matching indices to the supplied lists. Winding is
/// auto-corrected against the supplied normal hint, so callers can pass corners
/// in either CW or CCW order.
/// </summary>
public static class MeshBuilder
{
    public static void AddQuad(List<float> verts, List<uint> indices,
        (float X, float Y, float Z) a, (float X, float Y, float Z) b,
        (float X, float Y, float Z) c, (float X, float Y, float Z) d,
        (float X, float Y, float Z) normal)
    {
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var cross = (
            ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2,
            ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3,
            ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        float dot = cross.Item1 * normal.X + cross.Item2 * normal.Y + cross.Item3 * normal.Z;

        if (dot < 0)
            (b, d) = (d, b);

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

    public static void AddTriangle(List<float> verts, List<uint> indices,
        (float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c,
        (float X, float Y, float Z) normal)
    {
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var cross = (
            ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2,
            ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3,
            ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        float dot = cross.Item1 * normal.X + cross.Item2 * normal.Y + cross.Item3 * normal.Z;

        if (dot < 0)
            (b, c) = (c, b);

        uint baseIdx = (uint)(verts.Count / 6);

        AddVertex(verts, a, normal);
        AddVertex(verts, b, normal);
        AddVertex(verts, c, normal);

        indices.Add(baseIdx);
        indices.Add(baseIdx + 1);
        indices.Add(baseIdx + 2);
    }

    public static void AddVertex(List<float> verts,
        (float X, float Y, float Z) pos, (float X, float Y, float Z) normal)
    {
        verts.Add(pos.X);
        verts.Add(pos.Y);
        verts.Add(pos.Z);
        verts.Add(normal.X);
        verts.Add(normal.Y);
        verts.Add(normal.Z);
    }
}

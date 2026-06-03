using Silk.NET.OpenGL;

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
        var ab = (X: b.X - a.X, Y: b.Y - a.Y, Z: b.Z - a.Z);
        var ac = (X: c.X - a.X, Y: c.Y - a.Y, Z: c.Z - a.Z);
        var cross = (
            X: ab.Y * ac.Z - ab.Z * ac.Y,
            Y: ab.Z * ac.X - ab.X * ac.Z,
            Z: ab.X * ac.Y - ab.Y * ac.X);
        float dot = cross.X * normal.X + cross.Y * normal.Y + cross.Z * normal.Z;

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
        var ab = (X: b.X - a.X, Y: b.Y - a.Y, Z: b.Z - a.Z);
        var ac = (X: c.X - a.X, Y: c.Y - a.Y, Z: c.Z - a.Z);
        var cross = (
            X: ab.Y * ac.Z - ab.Z * ac.Y,
            Y: ab.Z * ac.X - ab.X * ac.Z,
            Z: ab.X * ac.Y - ab.Y * ac.X);
        float dot = cross.X * normal.X + cross.Y * normal.Y + cross.Z * normal.Z;

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

    /// <summary>
    /// Appends a single flat-shaded vertex (position then normal, 6 floats) to
    /// <paramref name="verts"/>. No winding correction is applied.
    /// </summary>
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

    /// <summary>
    /// Shared GL setup used by the cell-shape meshes. Generates a VAO + VBO +
    /// EBO, uploads the position+normal vertex array and the unsigned-int
    /// index array, and configures attribute pointers (loc 0 = position, loc
    /// 1 = normal, stride 24 bytes). Used by every mesh that follows the
    /// flat- or smooth-shaded position+normal layout.
    /// </summary>
    public static unsafe void UploadAndBind(float[] vertArr, uint[] idxArr,
        out uint vao, out uint vbo, out uint ebo, GL gl)
    {
        vao = gl.GenVertexArray();
        vbo = gl.GenBuffer();
        ebo = gl.GenBuffer();

        gl.BindVertexArray(vao);

        gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        fixed (float* p = vertArr)
            gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(vertArr.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);

        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* p = idxArr)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer,
                (nuint)(idxArr.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        uint stride = 6 * sizeof(float);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));

        gl.BindVertexArray(0);
    }

    public static void UploadAndBind(CellMeshGeometry geometry,
        out uint vao, out uint vbo, out uint ebo, GL gl) =>
        UploadAndBind(geometry.Vertices, geometry.Indices, out vao, out vbo, out ebo, gl);
}

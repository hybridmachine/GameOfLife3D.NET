using System.Numerics;
using Silk.NET.OpenGL;

namespace GameOfLife3D.NET.Rendering.Meshes;

/// <summary>
/// Regular dodecahedron built as the dual of IcosahedronMesh: each dodec
/// vertex is the centroid of an icosa face, and each dodec face is the
/// pentagon connecting the 5 icosa-face centroids that meet at one icosa
/// vertex. 12 pentagons × 3 fan triangles = 36 triangles. The mesh is
/// uniformly rescaled so the largest coordinate magnitude equals 0.5
/// (fits the unit cell).
/// </summary>
public sealed class DodecahedronMesh : IInstancedMesh
{
    private readonly GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;

    public uint Vao => _vao;
    public uint IndexCount { get; private set; }

    public DodecahedronMesh(GL gl)
    {
        _gl = gl;
        Generate();
    }

    private void Generate()
    {
        int faceCount = IcosahedronMesh.BaseFaces.Length / 3;
        int vertCount = IcosahedronMesh.BaseVertices.Length;

        // 1. Compute the centroid of each icosa face — these are the 20 dodec
        //    vertices. Track the max coordinate magnitude so we can rescale.
        var centroids = new Vector3[faceCount];
        float maxComp = 0f;
        for (int f = 0; f < faceCount; f++)
        {
            var (ax, ay, az) = IcosahedronMesh.BaseVertices[IcosahedronMesh.BaseFaces[f * 3 + 0]];
            var (bx, by, bz) = IcosahedronMesh.BaseVertices[IcosahedronMesh.BaseFaces[f * 3 + 1]];
            var (cx, cy, cz) = IcosahedronMesh.BaseVertices[IcosahedronMesh.BaseFaces[f * 3 + 2]];
            centroids[f] = new Vector3(
                (ax + bx + cx) / 3f,
                (ay + by + cy) / 3f,
                (az + bz + cz) / 3f);
            maxComp = MathF.Max(maxComp, MathF.Max(
                MathF.Abs(centroids[f].X),
                MathF.Max(MathF.Abs(centroids[f].Y), MathF.Abs(centroids[f].Z))));
        }
        float scale = 0.5f / maxComp;
        for (int i = 0; i < faceCount; i++) centroids[i] *= scale;

        // 2. For each icosa vertex, collect the 5 face indices that contain
        //    it. Each vertex of a regular icosahedron is shared by exactly 5
        //    faces.
        var facesAtVertex = new int[vertCount][];
        {
            var counts = new int[vertCount];
            for (int f = 0; f < faceCount; f++)
                for (int k = 0; k < 3; k++)
                    counts[IcosahedronMesh.BaseFaces[f * 3 + k]]++;
            for (int v = 0; v < vertCount; v++) facesAtVertex[v] = new int[counts[v]];
            for (int v = 0; v < vertCount; v++) counts[v] = 0;
            for (int f = 0; f < faceCount; f++)
                for (int k = 0; k < 3; k++)
                {
                    int vi = IcosahedronMesh.BaseFaces[f * 3 + k];
                    facesAtVertex[vi][counts[vi]++] = f;
                }
        }

        // 3. For each icosa vertex, sort the 5 surrounding face-centroids by
        //    their angular position in the plane perpendicular to that vertex.
        //    Then fan-triangulate the resulting pentagon.
        var verts = new List<float>();
        var idx = new List<uint>();

        for (int v = 0; v < vertCount; v++)
        {
            var faceIdxs = facesAtVertex[v];
            if (faceIdxs.Length != 5) continue;  // should always be 5 for a valid icosa

            var (vx, vy, vz) = IcosahedronMesh.BaseVertices[v];
            Vector3 axis = Vector3.Normalize(new Vector3(vx, vy, vz));

            // Project each centroid onto the plane through origin perpendicular
            // to `axis`. The projected vectors lie in a 2D plane; we sort by
            // angle in that plane.
            var projected = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = centroids[faceIdxs[i]];
                projected[i] = c - axis * Vector3.Dot(c, axis);
            }
            Vector3 uAxis = Vector3.Normalize(projected[0]);
            Vector3 wAxis = Vector3.Cross(axis, uAxis);

            var angled = new (float angle, int faceIdx)[5];
            for (int i = 0; i < 5; i++)
            {
                float angle = MathF.Atan2(
                    Vector3.Dot(projected[i], wAxis),
                    Vector3.Dot(projected[i], uAxis));
                angled[i] = (angle, faceIdxs[i]);
            }
            Array.Sort(angled, (a, b) => a.angle.CompareTo(b.angle));

            // Fan-triangulate from angled[0]. Normal hint = axis (outward from
            // origin — same direction as the icosa vertex this face is dual to).
            var p0 = centroids[angled[0].faceIdx];
            for (int i = 1; i < 4; i++)
            {
                var pa = centroids[angled[i].faceIdx];
                var pb = centroids[angled[i + 1].faceIdx];
                MeshBuilder.AddTriangle(verts, idx,
                    (p0.X, p0.Y, p0.Z),
                    (pa.X, pa.Y, pa.Z),
                    (pb.X, pb.Y, pb.Z),
                    (axis.X, axis.Y, axis.Z));
            }
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

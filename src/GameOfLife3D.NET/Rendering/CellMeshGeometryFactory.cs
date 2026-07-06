using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

public static class CellMeshGeometryFactory
{
    public const int BeveledCubeRenderFallbackThreshold = 500_000;

    private const float Phi = 1.6180339887f;
    private const float IcosahedronScale = 0.5f / Phi;

    private static readonly Lazy<CellMeshGeometry> CubeGeometry = new(CreateCube);
    private static readonly Lazy<CellMeshGeometry> BeveledCubeGeometry = new(CreateBeveledCube);
    private static readonly Lazy<CellMeshGeometry> TetrahedronGeometry = new(CreateTetrahedron);
    private static readonly Lazy<CellMeshGeometry> OctahedronGeometry = new(CreateOctahedron);
    private static readonly Lazy<CellMeshGeometry> SquarePyramidGeometry = new(CreateSquarePyramid);
    private static readonly Lazy<CellMeshGeometry> IcosahedronGeometry = new(CreateIcosahedron);
    private static readonly Lazy<CellMeshGeometry> DodecahedronGeometry = new(CreateDodecahedron);
    private static readonly Lazy<CellMeshGeometry> IcosphereGeometry = new(CreateIcosphere);
    private static readonly Lazy<CellMeshGeometry> CapsuleGeometry = new(CreateCapsule);

    private static readonly (float X, float Y, float Z)[] IcosahedronBaseVertices =
    [
        (-IcosahedronScale, IcosahedronScale * Phi, 0f),
        ( IcosahedronScale, IcosahedronScale * Phi, 0f),
        (-IcosahedronScale,-IcosahedronScale * Phi, 0f),
        ( IcosahedronScale,-IcosahedronScale * Phi, 0f),
        (0f,-IcosahedronScale,  IcosahedronScale * Phi),
        (0f, IcosahedronScale,  IcosahedronScale * Phi),
        (0f,-IcosahedronScale, -IcosahedronScale * Phi),
        (0f, IcosahedronScale, -IcosahedronScale * Phi),
        ( IcosahedronScale * Phi, 0f, -IcosahedronScale),
        ( IcosahedronScale * Phi, 0f,  IcosahedronScale),
        (-IcosahedronScale * Phi, 0f, -IcosahedronScale),
        (-IcosahedronScale * Phi, 0f,  IcosahedronScale),
    ];

    private static readonly int[] IcosahedronBaseFaces =
    [
        0, 11,  5,    0,  5,  1,    0,  1,  7,    0,  7, 10,   0, 10, 11,
        1,  5,  9,    5, 11,  4,   11, 10,  2,   10,  7,  6,   7,  1,  8,
        3,  9,  4,    3,  4,  2,    3,  2,  6,    3,  6,  8,   3,  8,  9,
        4,  9,  5,    2,  4, 11,    6,  2, 10,    8,  6,  7,   9,  8,  1,
    ];

    public static CellMeshGeometry GetGeometry(CellShape shape) => shape switch
    {
        CellShape.Cube => CubeGeometry.Value,
        CellShape.BeveledCube => BeveledCubeGeometry.Value,
        CellShape.Tetrahedron => TetrahedronGeometry.Value,
        CellShape.Octahedron => OctahedronGeometry.Value,
        CellShape.SquarePyramid => SquarePyramidGeometry.Value,
        CellShape.Icosahedron => IcosahedronGeometry.Value,
        CellShape.Dodecahedron => DodecahedronGeometry.Value,
        CellShape.Sphere => IcosphereGeometry.Value,
        CellShape.Capsule => CapsuleGeometry.Value,
        _ => CubeGeometry.Value,
    };

    /// <summary>
    /// Above <see cref="BeveledCubeRenderFallbackThreshold"/> instances,
    /// high-poly shapes fall back to cheaper analogues so vertex cost stays
    /// bounded at extreme cell counts: BeveledCube→Cube, Sphere→Icosahedron,
    /// Capsule→Octahedron, Dodecahedron→Cube. Low-poly shapes render as
    /// selected at any count.
    /// </summary>
    public static CellShape ResolveRenderShape(CellShape shape, int instanceCount)
    {
        if (instanceCount <= BeveledCubeRenderFallbackThreshold)
            return shape;

        return shape switch
        {
            CellShape.BeveledCube => CellShape.Cube,
            CellShape.Sphere => CellShape.Icosahedron,
            CellShape.Capsule => CellShape.Octahedron,
            CellShape.Dodecahedron => CellShape.Cube,
            _ => shape,
        };
    }

    private static CellMeshGeometry CreateCube()
    {
        float[] vertices =
        [
            -0.5f, -0.5f,  0.5f,   0f,  0f,  1f,
             0.5f, -0.5f,  0.5f,   0f,  0f,  1f,
             0.5f,  0.5f,  0.5f,   0f,  0f,  1f,
            -0.5f,  0.5f,  0.5f,   0f,  0f,  1f,
            -0.5f, -0.5f, -0.5f,   0f,  0f, -1f,
            -0.5f,  0.5f, -0.5f,   0f,  0f, -1f,
             0.5f,  0.5f, -0.5f,   0f,  0f, -1f,
             0.5f, -0.5f, -0.5f,   0f,  0f, -1f,
            -0.5f,  0.5f, -0.5f,   0f,  1f,  0f,
            -0.5f,  0.5f,  0.5f,   0f,  1f,  0f,
             0.5f,  0.5f,  0.5f,   0f,  1f,  0f,
             0.5f,  0.5f, -0.5f,   0f,  1f,  0f,
            -0.5f, -0.5f, -0.5f,   0f, -1f,  0f,
             0.5f, -0.5f, -0.5f,   0f, -1f,  0f,
             0.5f, -0.5f,  0.5f,   0f, -1f,  0f,
            -0.5f, -0.5f,  0.5f,   0f, -1f,  0f,
             0.5f, -0.5f, -0.5f,   1f,  0f,  0f,
             0.5f,  0.5f, -0.5f,   1f,  0f,  0f,
             0.5f,  0.5f,  0.5f,   1f,  0f,  0f,
             0.5f, -0.5f,  0.5f,   1f,  0f,  0f,
            -0.5f, -0.5f, -0.5f,  -1f,  0f,  0f,
            -0.5f, -0.5f,  0.5f,  -1f,  0f,  0f,
            -0.5f,  0.5f,  0.5f,  -1f,  0f,  0f,
            -0.5f,  0.5f, -0.5f,  -1f,  0f,  0f,
        ];

        uint[] indices =
        [
             0,  1,  2,   2,  3,  0,
             4,  5,  6,   6,  7,  4,
             8,  9, 10,  10, 11,  8,
            12, 13, 14,  14, 15, 12,
            16, 17, 18,  18, 19, 16,
            20, 21, 22,  22, 23, 20,
        ];

        return new CellMeshGeometry(vertices, indices);
    }

    private static CellMeshGeometry CreateBeveledCube()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        const float h = 0.5f;
        const float b = 0.08f;
        float o = h;
        float i = h - b;

        MeshBuilder.AddQuad(vertices, indices, (-i, -i, o), (i, -i, o), (i, i, o), (-i, i, o), (0, 0, 1));
        MeshBuilder.AddQuad(vertices, indices, (i, -i, -o), (-i, -i, -o), (-i, i, -o), (i, i, -o), (0, 0, -1));
        MeshBuilder.AddQuad(vertices, indices, (-i, o, i), (i, o, i), (i, o, -i), (-i, o, -i), (0, 1, 0));
        MeshBuilder.AddQuad(vertices, indices, (-i, -o, -i), (i, -o, -i), (i, -o, i), (-i, -o, i), (0, -1, 0));
        MeshBuilder.AddQuad(vertices, indices, (o, -i, i), (o, -i, -i), (o, i, -i), (o, i, i), (1, 0, 0));
        MeshBuilder.AddQuad(vertices, indices, (-o, -i, -i), (-o, -i, i), (-o, i, i), (-o, i, -i), (-1, 0, 0));

        const float d = 0.707107f;
        MeshBuilder.AddQuad(vertices, indices, (-i, i, o), (i, i, o), (i, o, i), (-i, o, i), (0, d, d));
        MeshBuilder.AddQuad(vertices, indices, (i, -i, o), (-i, -i, o), (-i, -o, i), (i, -o, i), (0, -d, d));
        MeshBuilder.AddQuad(vertices, indices, (i, i, o), (i, -i, o), (o, -i, i), (o, i, i), (d, 0, d));
        MeshBuilder.AddQuad(vertices, indices, (-i, -i, o), (-i, i, o), (-o, i, i), (-o, -i, i), (-d, 0, d));
        MeshBuilder.AddQuad(vertices, indices, (i, i, -o), (-i, i, -o), (-i, o, -i), (i, o, -i), (0, d, -d));
        MeshBuilder.AddQuad(vertices, indices, (-i, -i, -o), (i, -i, -o), (i, -o, -i), (-i, -o, -i), (0, -d, -d));
        MeshBuilder.AddQuad(vertices, indices, (i, -i, -o), (i, i, -o), (o, i, -i), (o, -i, -i), (d, 0, -d));
        MeshBuilder.AddQuad(vertices, indices, (-i, i, -o), (-i, -i, -o), (-o, -i, -i), (-o, i, -i), (-d, 0, -d));
        MeshBuilder.AddQuad(vertices, indices, (i, o, i), (i, o, -i), (o, i, -i), (o, i, i), (d, d, 0));
        MeshBuilder.AddQuad(vertices, indices, (-i, o, -i), (-i, o, i), (-o, i, i), (-o, i, -i), (-d, d, 0));
        MeshBuilder.AddQuad(vertices, indices, (i, -o, -i), (i, -o, i), (o, -i, i), (o, -i, -i), (d, -d, 0));
        MeshBuilder.AddQuad(vertices, indices, (-i, -o, i), (-i, -o, -i), (-o, -i, -i), (-o, -i, i), (-d, -d, 0));

        const float cd = 0.577350f;
        MeshBuilder.AddTriangle(vertices, indices, (i, i, o), (o, i, i), (i, o, i), (cd, cd, cd));
        MeshBuilder.AddTriangle(vertices, indices, (-i, i, o), (-i, o, i), (-o, i, i), (-cd, cd, cd));
        MeshBuilder.AddTriangle(vertices, indices, (i, -i, o), (i, -o, i), (o, -i, i), (cd, -cd, cd));
        MeshBuilder.AddTriangle(vertices, indices, (-i, -i, o), (-o, -i, i), (-i, -o, i), (-cd, -cd, cd));
        MeshBuilder.AddTriangle(vertices, indices, (i, i, -o), (i, o, -i), (o, i, -i), (cd, cd, -cd));
        MeshBuilder.AddTriangle(vertices, indices, (-i, i, -o), (-o, i, -i), (-i, o, -i), (-cd, cd, -cd));
        MeshBuilder.AddTriangle(vertices, indices, (i, -i, -o), (o, -i, -i), (i, -o, -i), (cd, -cd, -cd));
        MeshBuilder.AddTriangle(vertices, indices, (-i, -i, -o), (-i, -o, -i), (-o, -i, -i), (-cd, -cd, -cd));

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static CellMeshGeometry CreateTetrahedron()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        var a = ( 0.5f,  0.5f,  0.5f);
        var b = ( 0.5f, -0.5f, -0.5f);
        var c = (-0.5f,  0.5f, -0.5f);
        var d = (-0.5f, -0.5f,  0.5f);

        MeshBuilder.AddTriangle(vertices, indices, a, b, c, Centroid(a, b, c));
        MeshBuilder.AddTriangle(vertices, indices, a, d, b, Centroid(a, d, b));
        MeshBuilder.AddTriangle(vertices, indices, a, c, d, Centroid(a, c, d));
        MeshBuilder.AddTriangle(vertices, indices, b, d, c, Centroid(b, d, c));

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static CellMeshGeometry CreateOctahedron()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        var px = ( 0.5f,  0f,  0f);
        var nx = (-0.5f,  0f,  0f);
        var py = ( 0f,  0.5f,  0f);
        var ny = ( 0f, -0.5f,  0f);
        var pz = ( 0f,  0f,  0.5f);
        var nz = ( 0f,  0f, -0.5f);

        MeshBuilder.AddTriangle(vertices, indices, px, pz, py, ( 1f,  1f,  1f));
        MeshBuilder.AddTriangle(vertices, indices, px, py, nz, ( 1f,  1f, -1f));
        MeshBuilder.AddTriangle(vertices, indices, px, ny, pz, ( 1f, -1f,  1f));
        MeshBuilder.AddTriangle(vertices, indices, px, nz, ny, ( 1f, -1f, -1f));
        MeshBuilder.AddTriangle(vertices, indices, nx, py, pz, (-1f,  1f,  1f));
        MeshBuilder.AddTriangle(vertices, indices, nx, nz, py, (-1f,  1f, -1f));
        MeshBuilder.AddTriangle(vertices, indices, nx, pz, ny, (-1f, -1f,  1f));
        MeshBuilder.AddTriangle(vertices, indices, nx, ny, nz, (-1f, -1f, -1f));

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static CellMeshGeometry CreateSquarePyramid()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        var apex = (0f, 0.5f, 0f);
        var bl = (-0.5f, -0.5f, -0.5f);
        var br = ( 0.5f, -0.5f, -0.5f);
        var tr = ( 0.5f, -0.5f,  0.5f);
        var tl = (-0.5f, -0.5f,  0.5f);

        MeshBuilder.AddTriangle(vertices, indices, apex, bl, br, ( 0f,  0.5f, -1f));
        MeshBuilder.AddTriangle(vertices, indices, apex, br, tr, ( 1f,  0.5f,  0f));
        MeshBuilder.AddTriangle(vertices, indices, apex, tr, tl, ( 0f,  0.5f,  1f));
        MeshBuilder.AddTriangle(vertices, indices, apex, tl, bl, (-1f,  0.5f,  0f));
        MeshBuilder.AddQuad(vertices, indices, bl, br, tr, tl, (0f, -1f, 0f));

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static CellMeshGeometry CreateIcosahedron()
    {
        var vertices = new List<float>();
        var indices = new List<uint>();

        for (int f = 0; f < IcosahedronBaseFaces.Length; f += 3)
        {
            var a = IcosahedronBaseVertices[IcosahedronBaseFaces[f]];
            var b = IcosahedronBaseVertices[IcosahedronBaseFaces[f + 1]];
            var c = IcosahedronBaseVertices[IcosahedronBaseFaces[f + 2]];
            var n = ((a.X + b.X + c.X) / 3f,
                     (a.Y + b.Y + c.Y) / 3f,
                     (a.Z + b.Z + c.Z) / 3f);
            MeshBuilder.AddTriangle(vertices, indices, a, b, c, n);
        }

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static CellMeshGeometry CreateDodecahedron()
    {
        int faceCount = IcosahedronBaseFaces.Length / 3;
        int vertexCount = IcosahedronBaseVertices.Length;

        var centroids = new Vector3[faceCount];
        float maxComponent = 0f;
        for (int f = 0; f < faceCount; f++)
        {
            var (ax, ay, az) = IcosahedronBaseVertices[IcosahedronBaseFaces[f * 3]];
            var (bx, by, bz) = IcosahedronBaseVertices[IcosahedronBaseFaces[f * 3 + 1]];
            var (cx, cy, cz) = IcosahedronBaseVertices[IcosahedronBaseFaces[f * 3 + 2]];
            centroids[f] = new Vector3((ax + bx + cx) / 3f, (ay + by + cy) / 3f, (az + bz + cz) / 3f);
            maxComponent = MathF.Max(maxComponent, MathF.Max(
                MathF.Abs(centroids[f].X),
                MathF.Max(MathF.Abs(centroids[f].Y), MathF.Abs(centroids[f].Z))));
        }

        float scale = 0.5f / maxComponent;
        for (int i = 0; i < faceCount; i++)
            centroids[i] *= scale;

        var facesAtVertex = new int[vertexCount][];
        var counts = new int[vertexCount];
        for (int f = 0; f < faceCount; f++)
            for (int k = 0; k < 3; k++)
                counts[IcosahedronBaseFaces[f * 3 + k]]++;

        for (int v = 0; v < vertexCount; v++)
        {
            facesAtVertex[v] = new int[counts[v]];
            counts[v] = 0;
        }

        for (int f = 0; f < faceCount; f++)
            for (int k = 0; k < 3; k++)
            {
                int vertex = IcosahedronBaseFaces[f * 3 + k];
                facesAtVertex[vertex][counts[vertex]++] = f;
            }

        var vertices = new List<float>();
        var indices = new List<uint>();

        for (int v = 0; v < vertexCount; v++)
        {
            var faceIndices = facesAtVertex[v];
            if (faceIndices.Length != 5)
                continue;

            var (vx, vy, vz) = IcosahedronBaseVertices[v];
            Vector3 axis = Vector3.Normalize(new Vector3(vx, vy, vz));
            var projected = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                Vector3 centroid = centroids[faceIndices[i]];
                projected[i] = centroid - axis * Vector3.Dot(centroid, axis);
            }

            Vector3 uAxis = Vector3.Normalize(projected[0]);
            Vector3 wAxis = Vector3.Cross(axis, uAxis);

            var angled = new (float angle, int faceIndex)[5];
            for (int i = 0; i < 5; i++)
            {
                float angle = MathF.Atan2(
                    Vector3.Dot(projected[i], wAxis),
                    Vector3.Dot(projected[i], uAxis));
                angled[i] = (angle, faceIndices[i]);
            }

            Array.Sort(angled, (a, b) => a.angle.CompareTo(b.angle));

            var p0 = centroids[angled[0].faceIndex];
            for (int i = 1; i < 4; i++)
            {
                var pa = centroids[angled[i].faceIndex];
                var pb = centroids[angled[i + 1].faceIndex];
                MeshBuilder.AddTriangle(vertices, indices,
                    (p0.X, p0.Y, p0.Z),
                    (pa.X, pa.Y, pa.Z),
                    (pb.X, pb.Y, pb.Z),
                    (axis.X, axis.Y, axis.Z));
            }
        }

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static CellMeshGeometry CreateIcosphere()
    {
        const float radius = 0.5f;

        var positions = new List<Vector3>();
        foreach (var (x, y, z) in IcosahedronBaseVertices)
            positions.Add(new Vector3(x, y, z));

        var faces = new List<(int A, int B, int C)>();
        for (int f = 0; f < IcosahedronBaseFaces.Length; f += 3)
            faces.Add((IcosahedronBaseFaces[f], IcosahedronBaseFaces[f + 1], IcosahedronBaseFaces[f + 2]));

        var midpointCache = new Dictionary<(int, int), int>();
        int Midpoint(int a, int b)
        {
            var key = a < b ? (a, b) : (b, a);
            if (midpointCache.TryGetValue(key, out int index))
                return index;

            var midpoint = Vector3.Normalize((positions[a] + positions[b]) * 0.5f) * radius;
            positions.Add(midpoint);
            int newIndex = positions.Count - 1;
            midpointCache[key] = newIndex;
            return newIndex;
        }

        var subdivided = new List<(int A, int B, int C)>(faces.Count * 4);
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

        var vertices = new List<float>(positions.Count * CellMeshGeometry.FloatsPerVertex);
        foreach (var position in positions)
        {
            Vector3 normal = Vector3.Normalize(position);
            vertices.Add(position.X);
            vertices.Add(position.Y);
            vertices.Add(position.Z);
            vertices.Add(normal.X);
            vertices.Add(normal.Y);
            vertices.Add(normal.Z);
        }

        var indices = new List<uint>(subdivided.Count * 3);
        foreach (var (a, b, c) in subdivided)
        {
            indices.Add((uint)a);
            indices.Add((uint)b);
            indices.Add((uint)c);
        }

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static CellMeshGeometry CreateCapsule()
    {
        const int segments = 8;
        const int capRings = 3;
        const float radius = 0.25f;
        const float halfBody = 0.25f;
        const float tau = MathF.PI * 2f;

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();

        for (int i = 0; i <= capRings; i++)
        {
            float phi = (MathF.PI * 0.5f) * i / capRings;
            float y = radius * MathF.Cos(phi);
            float r = radius * MathF.Sin(phi);

            for (int j = 0; j < segments; j++)
            {
                float theta = tau * j / segments;
                Vector3 dirOnCap = new(r * MathF.Cos(theta), y, r * MathF.Sin(theta));
                positions.Add(new Vector3(0f, halfBody, 0f) + dirOnCap);
                normals.Add(Vector3.Normalize(dirOnCap));
            }
        }

        for (int i = 0; i <= capRings; i++)
        {
            float phi = (MathF.PI * 0.5f) * i / capRings;
            float y = -radius * MathF.Sin(phi);
            float r = radius * MathF.Cos(phi);

            for (int j = 0; j < segments; j++)
            {
                float theta = tau * j / segments;
                Vector3 dirOnCap = new(r * MathF.Cos(theta), y, r * MathF.Sin(theta));
                positions.Add(new Vector3(0f, -halfBody, 0f) + dirOnCap);
                normals.Add(Vector3.Normalize(dirOnCap));
            }
        }

        var indices = new List<uint>();
        int totalRings = 2 * (capRings + 1);
        for (int ring = 0; ring < totalRings - 1; ring++)
        {
            for (int j = 0; j < segments; j++)
            {
                int jNext = (j + 1) % segments;
                int a = ring * segments + j;
                int b = ring * segments + jNext;
                int c = (ring + 1) * segments + jNext;
                int d = (ring + 1) * segments + j;

                indices.Add((uint)a);
                indices.Add((uint)b);
                indices.Add((uint)c);
                indices.Add((uint)a);
                indices.Add((uint)c);
                indices.Add((uint)d);
            }
        }

        var vertices = new List<float>(positions.Count * CellMeshGeometry.FloatsPerVertex);
        for (int i = 0; i < positions.Count; i++)
        {
            vertices.Add(positions[i].X);
            vertices.Add(positions[i].Y);
            vertices.Add(positions[i].Z);
            vertices.Add(normals[i].X);
            vertices.Add(normals[i].Y);
            vertices.Add(normals[i].Z);
        }

        return new CellMeshGeometry(vertices.ToArray(), indices.ToArray());
    }

    private static (float, float, float) Centroid(
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c)
        => ((a.X + b.X + c.X) / 3f, (a.Y + b.Y + c.Y) / 3f, (a.Z + b.Z + c.Z) / 3f);
}

using System.Globalization;
using System.Numerics;
using System.Text;
using GameOfLife3D.NET.Engine;

namespace GameOfLife3D.NET.IO;

public static class ModelExporter
{
    private static readonly Vector3[] CubeVertices =
    [
        new(-0.5f, -0.5f, -0.5f), new( 0.5f, -0.5f, -0.5f), new( 0.5f,  0.5f, -0.5f), new(-0.5f,  0.5f, -0.5f), // Back
        new(-0.5f, -0.5f,  0.5f), new( 0.5f, -0.5f,  0.5f), new( 0.5f,  0.5f,  0.5f), new(-0.5f,  0.5f,  0.5f), // Front
    ];

    private static readonly (int A, int B, int C, Vector3 Normal)[] CubeTriangles =
    [
        // Front face (Z+)
        (4, 5, 6, new(0, 0, 1)), (4, 6, 7, new(0, 0, 1)),
        // Back face (Z-)
        (1, 0, 3, new(0, 0, -1)), (1, 3, 2, new(0, 0, -1)),
        // Top face (Y+)
        (3, 7, 6, new(0, 1, 0)), (3, 6, 2, new(0, 1, 0)),
        // Bottom face (Y-)
        (0, 1, 5, new(0, -1, 0)), (0, 5, 4, new(0, -1, 0)),
        // Right face (X+)
        (1, 2, 6, new(1, 0, 0)), (1, 6, 5, new(1, 0, 0)),
        // Left face (X-)
        (0, 4, 7, new(-1, 0, 0)), (0, 7, 3, new(-1, 0, 0)),
    ];

    public static void ExportBinarySTL(string path, IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd, int gridSize, float cellPadding)
    {
        float cellSize = 1.0f - cellPadding;
        float halfGrid = gridSize / 2f;

        int totalCubes = 0;
        for (int g = displayStart; g <= displayEnd && g < generations.Count; g++)
            totalCubes += generations[g].LiveCells.Count;

        int totalTriangles = totalCubes * 12;

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        // 80-byte header
        var header = new byte[80];
        Encoding.ASCII.GetBytes("GameOfLife3D STL Export").CopyTo(header, 0);
        bw.Write(header);

        // Triangle count
        bw.Write((uint)totalTriangles);

        // Write triangles
        for (int g = displayStart; g <= displayEnd && g < generations.Count; g++)
        {
            foreach (var cell in generations[g].LiveCells)
            {
                var center = new Vector3(cell.X - halfGrid, g, cell.Y - halfGrid);

                foreach (var tri in CubeTriangles)
                {
                    // Normal
                    bw.Write(tri.Normal.X);
                    bw.Write(tri.Normal.Y);
                    bw.Write(tri.Normal.Z);

                    // Vertex A
                    var va = CubeVertices[tri.A] * cellSize + center;
                    bw.Write(va.X); bw.Write(va.Y); bw.Write(va.Z);

                    // Vertex B
                    var vb = CubeVertices[tri.B] * cellSize + center;
                    bw.Write(vb.X); bw.Write(vb.Y); bw.Write(vb.Z);

                    // Vertex C
                    var vc = CubeVertices[tri.C] * cellSize + center;
                    bw.Write(vc.X); bw.Write(vc.Y); bw.Write(vc.Z);

                    // Attribute byte count (unused)
                    bw.Write((ushort)0);
                }
            }
        }
    }

    public static void ExportOBJ(string path, IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd, int gridSize, float cellPadding)
    {
        float cellSize = 1.0f - cellPadding;
        float halfGrid = gridSize / 2f;

        using var sw = new StreamWriter(path);
        sw.WriteLine("# GameOfLife3D OBJ Export");
        sw.WriteLine($"# Generations {displayStart}-{displayEnd}");

        // Write normals (shared for all cubes)
        Vector3[] normals = [
            new(0, 0, 1), new(0, 0, -1),
            new(0, 1, 0), new(0, -1, 0),
            new(1, 0, 0), new(-1, 0, 0),
        ];
        foreach (var n in normals)
            sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "vn {0:F4} {1:F4} {2:F4}", n.X, n.Y, n.Z));

        int vertexOffset = 0;

        for (int g = displayStart; g <= displayEnd && g < generations.Count; g++)
        {
            foreach (var cell in generations[g].LiveCells)
            {
                var center = new Vector3(cell.X - halfGrid, g, cell.Y - halfGrid);

                // 8 vertices per cube
                foreach (var baseVert in CubeVertices)
                {
                    var v = baseVert * cellSize + center;
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "v {0:F4} {1:F4} {2:F4}", v.X, v.Y, v.Z));
                }

                // 12 triangles per cube
                int b = vertexOffset + 1; // OBJ is 1-indexed
                // Front (Z+) - normal 1
                sw.WriteLine($"f {b + 4}//{1} {b + 5}//{1} {b + 6}//{1}");
                sw.WriteLine($"f {b + 4}//{1} {b + 6}//{1} {b + 7}//{1}");
                // Back (Z-) - normal 2
                sw.WriteLine($"f {b + 1}//{2} {b + 0}//{2} {b + 3}//{2}");
                sw.WriteLine($"f {b + 1}//{2} {b + 3}//{2} {b + 2}//{2}");
                // Top (Y+) - normal 3
                sw.WriteLine($"f {b + 3}//{3} {b + 7}//{3} {b + 6}//{3}");
                sw.WriteLine($"f {b + 3}//{3} {b + 6}//{3} {b + 2}//{3}");
                // Bottom (Y-) - normal 4
                sw.WriteLine($"f {b + 0}//{4} {b + 1}//{4} {b + 5}//{4}");
                sw.WriteLine($"f {b + 0}//{4} {b + 5}//{4} {b + 4}//{4}");
                // Right (X+) - normal 5
                sw.WriteLine($"f {b + 1}//{5} {b + 2}//{5} {b + 6}//{5}");
                sw.WriteLine($"f {b + 1}//{5} {b + 6}//{5} {b + 5}//{5}");
                // Left (X-) - normal 6
                sw.WriteLine($"f {b + 0}//{6} {b + 4}//{6} {b + 7}//{6}");
                sw.WriteLine($"f {b + 0}//{6} {b + 7}//{6} {b + 3}//{6}");

                vertexOffset += 8;
            }
        }
    }

    public static long EstimateSTLSize(int cellCount)
    {
        // 80 header + 4 count + 50 bytes per triangle * 12 triangles per cube
        return 84 + (long)cellCount * 12 * 50;
    }
}

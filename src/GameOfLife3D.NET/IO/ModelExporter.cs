using System.Globalization;
using System.Numerics;
using System.Text;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.Rendering;

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
        int displayStart, int displayEnd, int gridSize, RenderSettings settings)
    {
        float cellSize = 1.0f - settings.CellPadding;
        float halfGrid = gridSize / 2f;
        string mtlPath = Path.ChangeExtension(path, ".mtl");
        var materialSet = ObjMaterialSet.Create(generations, displayStart, displayEnd, settings);

        WriteMtl(mtlPath, materialSet.Materials);

        using var sw = new StreamWriter(path);
        sw.WriteLine("# GameOfLife3D OBJ Export");
        sw.WriteLine($"# Generations {displayStart}-{displayEnd}");
        sw.WriteLine($"mtllib {Path.GetFileName(mtlPath)}");

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
    if (g < 0)
        continue;

    if (generations[g].LiveCells.Count == 0)
        continue;

    sw.WriteLine($"usemtl {materialSet.GetMaterialName(g)}");

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

    private static void WriteMtl(string path, IReadOnlyList<ObjMaterial> materials)
    {
        using var sw = new StreamWriter(path);
        sw.WriteLine("# GameOfLife3D OBJ Material Export");

        foreach (var material in materials)
        {
            Vector3 color = Clamp01(material.Color);
            sw.WriteLine();
            sw.WriteLine($"newmtl {material.Name}");
            sw.WriteLine("Ka 0.0000 0.0000 0.0000");
            sw.WriteLine(string.Format(CultureInfo.InvariantCulture, "Kd {0:F4} {1:F4} {2:F4}", color.X, color.Y, color.Z));
            sw.WriteLine("Ks 0.0000 0.0000 0.0000");
            sw.WriteLine("d 1.0000");
            sw.WriteLine("illum 1");
        }
    }

    private sealed class ObjMaterialSet
    {
        private const string SolidMaterialName = "cell_color";

        private readonly bool _usesGradient;
        private readonly Dictionary<int, string> _generationMaterials;

        private ObjMaterialSet(IReadOnlyList<ObjMaterial> materials, bool usesGradient, Dictionary<int, string> generationMaterials)
        {
            Materials = materials;
            _usesGradient = usesGradient;
            _generationMaterials = generationMaterials;
        }

        public IReadOnlyList<ObjMaterial> Materials { get; }

        public static ObjMaterialSet Create(IReadOnlyList<Generation> generations, int displayStart, int displayEnd, RenderSettings settings)
        {
            if (!settings.FaceColorCycling)
            {
                return new ObjMaterialSet(
                    [new ObjMaterial(SolidMaterialName, settings.CellColor)],
                    usesGradient: false,
                    new Dictionary<int, string>());
            }

            IReadOnlyList<Vector3> stops = GetValidGradientStops(settings);
            var materials = new List<ObjMaterial>();
            var byGeneration = new Dictionary<int, string>();

            for (int g = displayStart; g <= displayEnd && g < generations.Count; g++)
            {
                if (g < 0 || generations[g].LiveCells.Count == 0)
                    continue;

                string name = FormatGenerationMaterialName(g);
                byGeneration[g] = name;
                materials.Add(new ObjMaterial(name, ComputeGradientColor(g, displayStart, displayEnd, stops)));
            }

            return new ObjMaterialSet(materials, usesGradient: true, byGeneration);
        }

        public string GetMaterialName(int generation)
        {
            if (!_usesGradient)
                return SolidMaterialName;

            return _generationMaterials.TryGetValue(generation, out string? name)
                ? name
                : FormatGenerationMaterialName(generation);
        }
    }

    private sealed record ObjMaterial(string Name, Vector3 Color);

    private static IReadOnlyList<Vector3> GetValidGradientStops(RenderSettings settings)
    {
        IReadOnlyList<Vector3> stops = settings.GradientStops;
        if (stops.Count < RenderSettings.MinGradientStops)
            return RenderSettings.DefaultGradientStops;

        if (stops.Count <= RenderSettings.MaxGradientStops)
            return stops;

        return stops.Take(RenderSettings.MaxGradientStops).ToArray();
    }

    private static Vector3 ComputeGradientColor(int generation, int displayStart, int displayEnd, IReadOnlyList<Vector3> stops)
    {
        float range = Math.Max(displayEnd - displayStart + 1, 1);

        // OBJ materials are one static color per generation. Sample across the
        // inclusive displayed range so the final generation does not duplicate
        // the first color at the gradient's cyclic wrap point.
        float adjustedY = PositiveModulo(generation - displayStart, range);
        float t = adjustedY / range;

        int n = Math.Clamp(stops.Count, RenderSettings.MinGradientStops, RenderSettings.MaxGradientStops);
        float scaled = t * n;
        int seg = (int)MathF.Floor(scaled);
        float k = scaled - seg;

        int idxA = PositiveModulo(seg, n);
        int idxB = PositiveModulo(seg + 1, n);
        return Vector3.Lerp(stops[idxA], stops[idxB], k);
    }

    private static string FormatGenerationMaterialName(int generation) =>
        generation >= 0
            ? string.Format(CultureInfo.InvariantCulture, "gen_{0:D4}", generation)
            : string.Format(CultureInfo.InvariantCulture, "gen_m{0:D4}", Math.Abs(generation));

    private static float PositiveModulo(float value, float modulo)
    {
        float result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static int PositiveModulo(int value, int modulo)
    {
        int result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private static Vector3 Clamp01(Vector3 value) => new(
        Math.Clamp(value.X, 0f, 1f),
        Math.Clamp(value.Y, 0f, 1f),
        Math.Clamp(value.Z, 0f, 1f));

    public static long EstimateSTLSize(int cellCount)
    {
        // 80 header + 4 count + 50 bytes per triangle * 12 triangles per cube
        return 84 + (long)cellCount * 12 * 50;
    }
}

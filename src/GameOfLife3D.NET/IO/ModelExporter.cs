using System.Globalization;
using System.Numerics;
using System.Text;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.Rendering;

namespace GameOfLife3D.NET.IO;

public static class ModelExporter
{
    public static void ExportBinarySTL(string path, IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd, int gridSize, RenderSettings settings)
    {
        CellMeshGeometry geometry = CellMeshGeometryFactory.GetGeometry(settings.Shape);
        float cellSize = 1.0f - settings.CellPadding;
        float halfGrid = gridSize / 2f;

        long totalCells = CountVisibleCells(generations, displayStart, displayEnd);
        long totalTriangles = checked(totalCells * geometry.TriangleCount);
        if (totalTriangles > uint.MaxValue)
        {
            throw new InvalidOperationException(
                $"STL export would contain {totalTriangles:N0} triangles, exceeding the binary STL limit of {uint.MaxValue:N0}.");
        }

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);

        var header = new byte[80];
        Encoding.ASCII.GetBytes($"GameOfLife3D STL Export ({settings.Shape})").CopyTo(header, 0);
        bw.Write(header);
        bw.Write((uint)totalTriangles);

        if (!TryGetDisplayBounds(generations, displayStart, displayEnd, out int firstGeneration, out int lastGeneration))
            return;

        for (int g = firstGeneration; g <= lastGeneration; g++)
        {
            foreach (var cell in generations[g].LiveCells)
            {
                var center = new Vector3(cell.X - halfGrid, g, cell.Y - halfGrid);

                for (int i = 0; i < geometry.Indices.Length; i += 3)
                {
                    int ia = checked((int)geometry.Indices[i]);
                    int ib = checked((int)geometry.Indices[i + 1]);
                    int ic = checked((int)geometry.Indices[i + 2]);

                    Vector3 va = geometry.GetPosition(ia) * cellSize + center;
                    Vector3 vb = geometry.GetPosition(ib) * cellSize + center;
                    Vector3 vc = geometry.GetPosition(ic) * cellSize + center;
                    Vector3 fallbackNormal = geometry.GetNormal(ia) + geometry.GetNormal(ib) + geometry.GetNormal(ic);
                    Vector3 normal = CalculateFacetNormal(va, vb, vc, fallbackNormal);

                    WriteVector(bw, normal);
                    WriteVector(bw, va);
                    WriteVector(bw, vb);
                    WriteVector(bw, vc);
                    bw.Write((ushort)0);
                }
            }
        }
    }

    public static void ExportOBJ(string path, IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd, int gridSize, RenderSettings settings)
    {
        CellMeshGeometry geometry = CellMeshGeometryFactory.GetGeometry(settings.Shape);
        float cellSize = 1.0f - settings.CellPadding;
        float halfGrid = gridSize / 2f;
        string mtlPath = Path.ChangeExtension(path, ".mtl");
        var materialSet = ObjMaterialSet.Create(generations, displayStart, displayEnd, settings);

        WriteMtl(mtlPath, materialSet.Materials);

        using var sw = new StreamWriter(path);
        sw.WriteLine("# GameOfLife3D OBJ Export");
        sw.WriteLine($"# Generations {displayStart}-{displayEnd}");
        sw.WriteLine($"# Cell shape {settings.Shape}");
        sw.WriteLine($"mtllib {Path.GetFileName(mtlPath)}");

        for (int i = 0; i < geometry.VertexCount; i++)
        {
            Vector3 n = NormalizeOrZero(geometry.GetNormal(i));
            sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "vn {0:F4} {1:F4} {2:F4}", n.X, n.Y, n.Z));
        }

        long vertexOffset = 0;
        if (!TryGetDisplayBounds(generations, displayStart, displayEnd, out int firstGeneration, out int lastGeneration))
            return;

        for (int g = firstGeneration; g <= lastGeneration; g++)
        {
            if (generations[g].LiveCells.Count == 0)
                continue;

            sw.WriteLine($"usemtl {materialSet.GetMaterialName(g)}");

            foreach (var cell in generations[g].LiveCells)
            {
                var center = new Vector3(cell.X - halfGrid, g, cell.Y - halfGrid);

                for (int i = 0; i < geometry.VertexCount; i++)
                {
                    Vector3 v = geometry.GetPosition(i) * cellSize + center;
                    sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "v {0:F4} {1:F4} {2:F4}", v.X, v.Y, v.Z));
                }

                for (int i = 0; i < geometry.Indices.Length; i += 3)
                {
                    long a = geometry.Indices[i];
                    long b = geometry.Indices[i + 1];
                    long c = geometry.Indices[i + 2];
                    long va = vertexOffset + a + 1;
                    long vb = vertexOffset + b + 1;
                    long vc = vertexOffset + c + 1;
                    long na = a + 1;
                    long nb = b + 1;
                    long nc = c + 1;

                    sw.WriteLine($"f {va}//{na} {vb}//{nb} {vc}//{nc}");
                }

                vertexOffset = checked(vertexOffset + geometry.VertexCount);
            }
        }
    }

    private static bool TryGetDisplayBounds(IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd, out int firstGeneration, out int lastGeneration)
    {
        firstGeneration = Math.Max(displayStart, 0);
        lastGeneration = Math.Min(displayEnd, generations.Count - 1);
        return firstGeneration <= lastGeneration;
    }

    private static long CountVisibleCells(IReadOnlyList<Generation> generations, int displayStart, int displayEnd)
    {
        if (!TryGetDisplayBounds(generations, displayStart, displayEnd, out int firstGeneration, out int lastGeneration))
            return 0;

        long total = 0;
        for (int g = firstGeneration; g <= lastGeneration; g++)
            total = checked(total + generations[g].LiveCells.Count);

        return total;
    }

    private static Vector3 CalculateFacetNormal(Vector3 a, Vector3 b, Vector3 c, Vector3 fallbackNormal)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        float lengthSquared = normal.LengthSquared();
        Vector3 fallback = NormalizeOrZero(fallbackNormal);
        if (lengthSquared <= 1e-12f)
            return fallback;

        normal /= MathF.Sqrt(lengthSquared);
        if (fallback != Vector3.Zero && Vector3.Dot(normal, fallback) < 0f)
            normal = -normal;

        return normal;
    }

    private static Vector3 NormalizeOrZero(Vector3 value)
    {
        float lengthSquared = value.LengthSquared();
        return lengthSquared <= 1e-12f ? Vector3.Zero : value / MathF.Sqrt(lengthSquared);
    }

    private static void WriteVector(BinaryWriter bw, Vector3 value)
    {
        bw.Write(value.X);
        bw.Write(value.Y);
        bw.Write(value.Z);
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

            if (!TryGetDisplayBounds(generations, displayStart, displayEnd, out int firstGeneration, out int lastGeneration))
                return new ObjMaterialSet(materials, usesGradient: true, byGeneration);

            for (int g = firstGeneration; g <= lastGeneration; g++)
            {
                if (generations[g].LiveCells.Count == 0)
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

    public static long EstimateSTLSize(long cellCount, RenderSettings settings)
    {
        CellMeshGeometry geometry = CellMeshGeometryFactory.GetGeometry(settings.Shape);
        long totalTriangles = checked(cellCount * geometry.TriangleCount);
        return checked(84 + totalTriangles * 50);
    }

    public static long EstimateSTLSize(IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd, RenderSettings settings) =>
        EstimateSTLSize(CountVisibleCells(generations, displayStart, displayEnd), settings);
}

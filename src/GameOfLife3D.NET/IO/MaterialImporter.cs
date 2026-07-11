using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;
using GameOfLife3D.NET.Rendering;

namespace GameOfLife3D.NET.IO;

/// <summary>
/// Import result returned by <see cref="MaterialImporter"/>.
/// </summary>
public sealed class MaterialImportResult
{
    public CellMaterial? Material { get; init; }
    public string? Error { get; init; }

    /// <summary>
    /// Parameter names that were present in the source file but connected to
    /// textures (not supported yet). The UI can surface these to inform the
    /// user that the texture connection was ignored.
    /// </summary>
    public IReadOnlyList<string> UnsupportedTexturedParams { get; init; } = [];

    public bool IsSuccess => Material != null && Error == null;
}

/// <summary>
/// Imports OpenPBR / MaterialX (<c>.mtlx</c>) and hand-authored PBR sidecar
/// (<c>.pbr.json</c>) files into a <see cref="CellMaterial"/>.
///
/// <para>
/// For <c>.mtlx</c> files the importer searches for the first
/// <c>&lt;open_pbr_surface&gt;</c> or <c>&lt;standard_surface&gt;</c> node and
/// reads the scalar/color inputs whose names match the supported parameter set.
/// Texture-connected inputs are recorded in
/// <see cref="MaterialImportResult.UnsupportedTexturedParams"/> and ignored.
/// </para>
///
/// <para>
/// For <c>.pbr.json</c> files the JSON is deserialized directly into the
/// <see cref="PbrJsonDto"/> DTO whose property names mirror the OpenPBR spec
/// snake_case names.
/// </para>
/// </summary>
public static class MaterialImporter
{
    // ── Public entry points ───────────────────────────────────────────────────

    public static MaterialImportResult ImportFile(string path)
    {
        try
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".mtlx" => ImportMtlx(path),
                ".json" => ImportPbrJson(path),
                _ => new MaterialImportResult
                {
                    Error = $"Unsupported file extension '{ext}'. Expected .mtlx or .json."
                }
            };
        }
        catch (Exception ex)
        {
            return new MaterialImportResult { Error = $"Import failed: {ex.Message}" };
        }
    }

    // ── MaterialX (.mtlx) ─────────────────────────────────────────────────────

    private static MaterialImportResult ImportMtlx(string path)
    {
        XDocument doc = XDocument.Load(path);
        XElement? root = doc.Root;
        if (root == null)
            return new MaterialImportResult { Error = "Empty or invalid MaterialX document." };

        // Search all descendants for a supported shader node.
        // Typical path:
        //   <materialx> → <open_pbr_surface name="Surf" ...>
        //                       <input name="base_color" type="color3" value="1,0.5,0.2"/>
        // The node name may be prefixed with a namespace, so compare
        // LocalName only.
        XElement? shaderNode = root.Descendants()
            .FirstOrDefault(e =>
                string.Equals(e.Name.LocalName, "open_pbr_surface", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Name.LocalName, "standard_surface", StringComparison.OrdinalIgnoreCase));

        if (shaderNode == null)
            return new MaterialImportResult
            {
                Error = "No <open_pbr_surface> or <standard_surface> node found in this MaterialX file."
            };

        bool isOpenPbr = string.Equals(shaderNode.Name.LocalName, "open_pbr_surface",
            StringComparison.OrdinalIgnoreCase);

        // Collect all <input> children, keyed by name.
        var inputs = shaderNode.Elements()
            .Where(e => string.Equals(e.Name.LocalName, "input", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                e => (e.Attribute("name")?.Value ?? "").ToLowerInvariant(),
                e => e,
                StringComparer.OrdinalIgnoreCase);

        var textured = new List<string>();

        // Parameter name mapping: OpenPBR → standard_surface (where different).
        // We try both names so a single code path handles either node type.
        CellMaterial mat = CellMaterial.Default;
        mat = mat with
        {
            BaseColor = ReadColor3(inputs, "base_color",
                mat.BaseColor, textured),
            BaseMetalness = ReadFloat(inputs, isOpenPbr ? "base_metalness" : "metalness",
                mat.BaseMetalness, textured),
            BaseDiffuseRoughness = ReadFloat(inputs,
                isOpenPbr ? "base_diffuse_roughness" : "diffuse_roughness",
                mat.BaseDiffuseRoughness, textured),
            SpecularRoughness = ReadFloat(inputs, "specular_roughness",
                mat.SpecularRoughness, textured),
            SpecularIor = ReadFloat(inputs, isOpenPbr ? "specular_ior" : "specular_IOR",
                mat.SpecularIor, textured),
            EmissionColor = ReadColor3(inputs, "emission_color",
                mat.EmissionColor, textured),
            EmissionLuminance = ReadFloat(inputs,
                isOpenPbr ? "emission_luminance" : "emission",
                mat.EmissionLuminance, textured),
            CoatWeight = ReadFloat(inputs, isOpenPbr ? "coat_weight" : "coat",
                mat.CoatWeight, textured),
            CoatRoughness = ReadFloat(inputs, "coat_roughness",
                mat.CoatRoughness, textured),
            CoatIor = ReadFloat(inputs, isOpenPbr ? "coat_ior" : "coat_IOR",
                mat.CoatIor, textured),
        };

        return new MaterialImportResult
        {
            Material = mat,
            UnsupportedTexturedParams = textured
        };
    }

    // ── PBR JSON sidecar (.pbr.json) ─────────────────────────────────────────

    private static MaterialImportResult ImportPbrJson(string path)
    {
        string json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<PbrJsonDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dto == null)
            return new MaterialImportResult { Error = "Failed to deserialize PBR JSON." };

        CellMaterial mat = new()
        {
            BaseColor = dto.BaseColor?.Length == 3
                ? new Vector3(dto.BaseColor[0], dto.BaseColor[1], dto.BaseColor[2])
                : CellMaterial.Default.BaseColor,
            BaseMetalness = dto.BaseMetalness ?? CellMaterial.Default.BaseMetalness,
            BaseDiffuseRoughness = dto.BaseDiffuseRoughness ?? CellMaterial.Default.BaseDiffuseRoughness,
            SpecularRoughness = dto.SpecularRoughness ?? CellMaterial.Default.SpecularRoughness,
            SpecularIor = dto.SpecularIor ?? CellMaterial.Default.SpecularIor,
            EmissionColor = dto.EmissionColor?.Length == 3
                ? new Vector3(dto.EmissionColor[0], dto.EmissionColor[1], dto.EmissionColor[2])
                : CellMaterial.Default.EmissionColor,
            EmissionLuminance = dto.EmissionLuminance ?? CellMaterial.Default.EmissionLuminance,
            CoatWeight = dto.CoatWeight ?? CellMaterial.Default.CoatWeight,
            CoatRoughness = dto.CoatRoughness ?? CellMaterial.Default.CoatRoughness,
            CoatIor = dto.CoatIor ?? CellMaterial.Default.CoatIor,
        };

        return new MaterialImportResult { Material = mat };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static float ReadFloat(
        Dictionary<string, XElement> inputs, string name, float fallback,
        List<string> textured)
    {
        if (!inputs.TryGetValue(name, out var el)) return fallback;

        // If the input has a nodename attribute it's texture-connected.
        if (el.Attribute("nodename") != null)
        {
            textured.Add(name);
            return fallback;
        }

        string? val = el.Attribute("value")?.Value;
        if (val != null && float.TryParse(val, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float f))
            return f;

        return fallback;
    }

    private static Vector3 ReadColor3(
        Dictionary<string, XElement> inputs, string name, Vector3 fallback,
        List<string> textured)
    {
        if (!inputs.TryGetValue(name, out var el)) return fallback;

        if (el.Attribute("nodename") != null)
        {
            textured.Add(name);
            return fallback;
        }

        string? val = el.Attribute("value")?.Value;
        if (val == null) return fallback;

        // Expect "R, G, B" (comma or space-separated).
        string[] parts = val.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            return new Vector3(r, g, b);

        return fallback;
    }

    // ── JSON DTO ──────────────────────────────────────────────────────────────

    /// <summary>
    /// JSON DTO for hand-authored <c>.pbr.json</c> material files.
    /// All fields are optional so partial files are accepted gracefully.
    /// </summary>
    private sealed class PbrJsonDto
    {
        public float[]? BaseColor { get; set; }
        public float? BaseMetalness { get; set; }
        public float? BaseDiffuseRoughness { get; set; }
        public float? SpecularRoughness { get; set; }
        public float? SpecularIor { get; set; }
        public float[]? EmissionColor { get; set; }
        public float? EmissionLuminance { get; set; }
        public float? CoatWeight { get; set; }
        public float? CoatRoughness { get; set; }
        public float? CoatIor { get; set; }
    }
}

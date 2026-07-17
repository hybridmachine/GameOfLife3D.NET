using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    /// Parameter names that were present in the source file but connected to a
    /// node graph the importer cannot reduce to an image file (e.g. procedural
    /// noise), or whose image file was not found on disk. The UI can surface
    /// these to inform the user that part of the material was approximated.
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
/// Inputs connected directly or through a nodegraph output to an
/// <c>&lt;image&gt;</c>/<c>&lt;bitmap&gt;</c> node (optionally through one
/// <c>&lt;normalmap&gt;</c> hop for the normal input) resolve to absolute texture
/// paths; inputs connected to any other node graph are recorded in
/// <see cref="MaterialImportResult.UnsupportedTexturedParams"/> and fall back to
/// their constant defaults.
/// </para>
///
/// <para>
/// Texture semantics are <c>constant × textureSample</c>: when an input
/// whose default is not the multiplicative identity
/// (<c>base_metalness</c>, <c>specular_roughness</c>, <c>emission_color</c>)
/// is texture-connected, the constant is promoted to the identity (1 /
/// white) so the texture reads through unchanged.
/// </para>
///
/// <para>
/// For <c>.pbr.json</c> files the JSON is deserialized directly into the
/// <see cref="PbrJsonDto"/> DTO whose property names mirror the OpenPBR spec
/// snake_case names; relative <c>*_texture</c> paths resolve against the JSON
/// file's directory.
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

        var unsupported = new List<string>();
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var ctx = new MtlxImportContext(root, baseDir, unsupported);

        CellMaterial def = CellMaterial.Default;

        // Parameter name mapping: OpenPBR → standard_surface (where different).
        // We try both names so a single code path handles either node type.
        var baseColor = ReadColor3(inputs, "base_color", def.BaseColor, ctx).PromotedOnTexture();
        var metalness = ReadFloat(inputs, isOpenPbr ? "base_metalness" : "metalness",
            def.BaseMetalness, ctx).PromotedOnTexture();
        var diffuseRoughness = ReadFloat(inputs,
            isOpenPbr ? "base_diffuse_roughness" : "diffuse_roughness",
            def.BaseDiffuseRoughness, ctx);
        var baseWeight = ReadFloat(inputs, isOpenPbr ? "base_weight" : "base",
            def.BaseWeight, ctx);
        var specularWeight = ReadFloat(inputs, isOpenPbr ? "specular_weight" : "specular",
            def.SpecularWeight, ctx);
        var specularColor = ReadColor3(inputs, "specular_color", def.SpecularColor, ctx);
        var specularRoughness = ReadFloat(inputs, "specular_roughness",
            def.SpecularRoughness, ctx).PromotedOnTexture();
        var specularAnisotropy = ReadFloat(inputs,
            isOpenPbr ? "specular_roughness_anisotropy" : "specular_anisotropy",
            def.SpecularAnisotropy, ctx);
        var specularIor = ReadFloat(inputs, isOpenPbr ? "specular_ior" : "specular_IOR",
            def.SpecularIor, ctx);
        var emissionColor = ReadColor3(inputs, "emission_color", def.EmissionColor, ctx).PromotedOnTexture();
        var emissionLuminance = ReadFloat(inputs,
            isOpenPbr ? "emission_luminance" : "emission",
            def.EmissionLuminance, ctx);
        var coatWeight = ReadFloat(inputs, isOpenPbr ? "coat_weight" : "coat",
            def.CoatWeight, ctx);
        var coatColor = ReadColor3(inputs, "coat_color", def.CoatColor, ctx);
        var coatRoughness = ReadFloat(inputs, "coat_roughness", def.CoatRoughness, ctx);
        var coatAnisotropy = ReadFloat(inputs,
            isOpenPbr ? "coat_roughness_anisotropy" : "coat_anisotropy",
            def.CoatAnisotropy, ctx);
        var coatIor = ReadFloat(inputs, isOpenPbr ? "coat_ior" : "coat_IOR",
            def.CoatIor, ctx);
        var coatDarkening = ReadFloat(inputs, "coat_darkening", def.CoatDarkening, ctx);
        var fuzzWeight = ReadFloat(inputs, isOpenPbr ? "fuzz_weight" : "sheen",
            def.FuzzWeight, ctx);
        var fuzzColor = ReadColor3(inputs, isOpenPbr ? "fuzz_color" : "sheen_color",
            def.FuzzColor, ctx);
        var fuzzRoughness = ReadFloat(inputs, isOpenPbr ? "fuzz_roughness" : "sheen_roughness",
            def.FuzzRoughness, ctx);
        var thinFilmWeight = ReadFloat(inputs, "thin_film_weight", def.ThinFilmWeight, ctx);
        var thinFilmThickness = ReadFloat(inputs, "thin_film_thickness", def.ThinFilmThickness, ctx);
        var thinFilmIor = ReadFloat(inputs, "thin_film_ior", def.ThinFilmIor, ctx);
        var opacity = ReadFloat(inputs, isOpenPbr ? "geometry_opacity" : "opacity",
            def.GeometryOpacity, ctx).PromotedOnTexture();
        string? normalTexture = ReadTextureOnly(inputs,
            isOpenPbr ? "geometry_normal" : "normal", ctx, allowNormalMapHop: true);

        CellMaterial mat = def with
        {
            BaseColor = baseColor.Value,
            BaseColorTexture = baseColor.Texture,
            BaseMetalness = metalness.Value,
            MetalnessTexture = metalness.Texture,
            BaseMetalnessPromotedForTexture = metalness.WasPromoted,
            BaseDiffuseRoughness = diffuseRoughness.Value,
            BaseWeight = baseWeight.Value,
            SpecularWeight = specularWeight.Value,
            SpecularColor = specularColor.Value,
            SpecularRoughness = specularRoughness.Value,
            RoughnessTexture = specularRoughness.Texture,
            SpecularRoughnessPromotedForTexture = specularRoughness.WasPromoted,
            SpecularAnisotropy = specularAnisotropy.Value,
            SpecularIor = specularIor.Value,
            EmissionColor = emissionColor.Value,
            EmissionTexture = emissionColor.Texture,
            EmissionColorPromotedForTexture = emissionColor.WasPromoted,
            EmissionLuminance = emissionLuminance.Value,
            CoatWeight = coatWeight.Value,
            CoatColor = coatColor.Value,
            CoatRoughness = coatRoughness.Value,
            CoatAnisotropy = coatAnisotropy.Value,
            CoatIor = coatIor.Value,
            CoatDarkening = coatDarkening.Value,
            FuzzWeight = fuzzWeight.Value,
            FuzzColor = fuzzColor.Value,
            FuzzRoughness = fuzzRoughness.Value,
            ThinFilmWeight = thinFilmWeight.Value,
            ThinFilmThickness = thinFilmThickness.Value,
            ThinFilmIor = thinFilmIor.Value,
            GeometryOpacity = opacity.Value,
            OpacityTexture = opacity.Texture,
            NormalTexture = normalTexture,
        };

        return new MaterialImportResult
        {
            Material = mat,
            UnsupportedTexturedParams = unsupported
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

        string baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        var notes = new List<string>();

        // Relative texture paths resolve against the JSON file's directory.
        // A missing file is treated as "not texture-connected" (returning the
        // path anyway would trip the constant-promotion rule, and the shader
        // would fall back to a promoted constant instead of the intended
        // default), but noted so the UI can tell the user.
        string? ResolveTexture(string? p, string inputName)
        {
            if (p == null) return null;
            string full = Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(baseDir, p));
            if (!File.Exists(full))
            {
                notes.Add($"{inputName} (image file not found: {p})");
                return null;
            }
            return full;
        }

        var def = CellMaterial.Default;
        string? baseColorTex = ResolveTexture(dto.BaseColorTexture, "base_color");
        string? metalnessTex = ResolveTexture(dto.BaseMetalnessTexture, "base_metalness");
        string? roughnessTex = ResolveTexture(dto.SpecularRoughnessTexture, "specular_roughness");
        string? normalTex = ResolveTexture(dto.GeometryNormalTexture, "geometry_normal");
        string? emissionTex = ResolveTexture(dto.EmissionColorTexture, "emission_color");
        string? opacityTex = ResolveTexture(dto.GeometryOpacityTexture, "geometry_opacity");

        CellMaterial mat = new()
        {
            BaseColor = Color3OrDefault(dto.BaseColor, def.BaseColor),
            BaseColorTexture = baseColorTex,
            // Constant-promotion rule: a texture-connected scalar whose default
            // is not the multiplicative identity is promoted to 1 unless the
            // author supplied an explicit constant.
            BaseMetalness = dto.BaseMetalness ?? (metalnessTex != null ? 1f : def.BaseMetalness),
            MetalnessTexture = metalnessTex,
            BaseMetalnessPromotedForTexture = dto.BaseMetalness is null && metalnessTex is not null,
            BaseDiffuseRoughness = dto.BaseDiffuseRoughness ?? def.BaseDiffuseRoughness,
            BaseWeight = dto.BaseWeight ?? def.BaseWeight,
            SpecularWeight = dto.SpecularWeight ?? def.SpecularWeight,
            SpecularColor = Color3OrDefault(dto.SpecularColor, def.SpecularColor),
            SpecularRoughness = dto.SpecularRoughness ?? (roughnessTex != null ? 1f : def.SpecularRoughness),
            RoughnessTexture = roughnessTex,
            SpecularRoughnessPromotedForTexture =
                dto.SpecularRoughness is null && roughnessTex is not null,
            SpecularAnisotropy = dto.SpecularAnisotropy ?? def.SpecularAnisotropy,
            SpecularIor = dto.SpecularIor ?? def.SpecularIor,
            EmissionColor = Color3OrDefault(dto.EmissionColor, emissionTex != null ? Vector3.One : def.EmissionColor),
            EmissionTexture = emissionTex,
            EmissionColorPromotedForTexture = !HasColor3(dto.EmissionColor) && emissionTex is not null,
            EmissionLuminance = dto.EmissionLuminance ?? def.EmissionLuminance,
            CoatWeight = dto.CoatWeight ?? def.CoatWeight,
            CoatColor = Color3OrDefault(dto.CoatColor, def.CoatColor),
            CoatRoughness = dto.CoatRoughness ?? def.CoatRoughness,
            CoatAnisotropy = dto.CoatAnisotropy ?? def.CoatAnisotropy,
            CoatIor = dto.CoatIor ?? def.CoatIor,
            CoatDarkening = dto.CoatDarkening ?? def.CoatDarkening,
            FuzzWeight = dto.FuzzWeight ?? def.FuzzWeight,
            FuzzColor = Color3OrDefault(dto.FuzzColor, def.FuzzColor),
            FuzzRoughness = dto.FuzzRoughness ?? def.FuzzRoughness,
            ThinFilmWeight = dto.ThinFilmWeight ?? def.ThinFilmWeight,
            ThinFilmThickness = dto.ThinFilmThickness ?? def.ThinFilmThickness,
            ThinFilmIor = dto.ThinFilmIor ?? def.ThinFilmIor,
            GeometryOpacity = dto.GeometryOpacity ?? (opacityTex != null ? 1f : def.GeometryOpacity),
            OpacityTexture = opacityTex,
            NormalTexture = normalTex,
            TextureScale = dto.TextureScale ?? def.TextureScale,
        };

        return new MaterialImportResult { Material = mat, UnsupportedTexturedParams = notes };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Result of reading one scalar input: constant plus optional texture path.</summary>
    private readonly struct ScalarRead(float value, string? texture, bool wasPromoted = false)
    {
        public float Value { get; } = value;
        public string? Texture { get; } = texture;
        public bool WasPromoted { get; } = wasPromoted;

        /// <summary>
        /// Constant-promotion rule: a texture-connected scalar whose default is
        /// not the multiplicative identity (metalness 0, roughness 0.3) is
        /// promoted to 1 so the texture reads through unchanged.
        /// </summary>
        public ScalarRead PromotedOnTexture() =>
            Texture != null ? new ScalarRead(1f, Texture, wasPromoted: true) : this;
    }

    /// <summary>Result of reading one color input: constant plus optional texture path.</summary>
    private readonly struct ColorRead(Vector3 value, string? texture, bool wasPromoted = false)
    {
        public Vector3 Value { get; } = value;
        public string? Texture { get; } = texture;
        public bool WasPromoted { get; } = wasPromoted;

        /// <summary>
        /// Constant-promotion rule for colors: a texture-connected color whose
        /// default is not the multiplicative identity (emission_color defaults
        /// to black) is promoted to white so the texture reads through
        /// unchanged. A no-op for base_color, whose default is already white.
        /// </summary>
        public ColorRead PromotedOnTexture() =>
            Texture != null ? new ColorRead(Vector3.One, Texture, wasPromoted: true) : this;
    }

    private static ScalarRead ReadFloat(
        Dictionary<string, XElement> inputs, string name, float fallback,
        MtlxImportContext ctx)
    {
        if (!inputs.TryGetValue(name, out var el)) return new ScalarRead(fallback, null);

        if (HasNodeConnection(el))
            return new ScalarRead(fallback, ctx.ResolveImageTexture(el, name, allowNormalMapHop: false));

        string? val = el.Attribute("value")?.Value;
        if (val != null && float.TryParse(val, NumberStyles.Float,
                CultureInfo.InvariantCulture, out float f))
            return new ScalarRead(f, null);

        return new ScalarRead(fallback, null);
    }

    private static ColorRead ReadColor3(
        Dictionary<string, XElement> inputs, string name, Vector3 fallback,
        MtlxImportContext ctx)
    {
        if (!inputs.TryGetValue(name, out var el)) return new ColorRead(fallback, null);

        if (HasNodeConnection(el))
            return new ColorRead(fallback, ctx.ResolveImageTexture(el, name, allowNormalMapHop: false));

        string? val = el.Attribute("value")?.Value;
        if (val == null) return new ColorRead(fallback, null);

        // Expect "R, G, B" (comma or space-separated).
        string[] parts = val.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float r)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float g)
            && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
            return new ColorRead(new Vector3(r, g, b), null);

        return new ColorRead(fallback, null);
    }

    /// <summary>
    /// Resolves the texture path for an input whose constant value is not
    /// representable as a material constant (geometry_normal). Constant
    /// values are ignored; only image connections resolve.
    /// </summary>
    private static string? ReadTextureOnly(
        Dictionary<string, XElement> inputs, string name,
        MtlxImportContext ctx, bool allowNormalMapHop)
    {
        if (!inputs.TryGetValue(name, out var el)) return null;
        if (!HasNodeConnection(el)) return null;
        return ctx.ResolveImageTexture(el, name, allowNormalMapHop);
    }

    private static bool HasNodeConnection(XElement input) =>
        input.Attribute("nodename") != null || input.Attribute("nodegraph") != null;

    private static bool HasColor3(float[]? arr) => arr?.Length == 3;

    private static Vector3 Color3OrDefault(float[]? arr, Vector3 fallback) =>
        arr?.Length == 3 ? new Vector3(arr[0], arr[1], arr[2]) : fallback;

    /// <summary>
    /// Resolution context for one .mtlx import: locates nodes referenced by
    /// <c>nodename</c> or a <c>nodegraph</c>/<c>output</c> pair, turns
    /// <c>&lt;image&gt;</c>/<c>&lt;bitmap&gt;</c> file references into absolute
    /// paths (following a single <c>&lt;normalmap&gt;</c> hop where allowed), and
    /// records inputs wired to anything else as unsupported node graphs.
    /// </summary>
    private sealed class MtlxImportContext(XElement root, string baseDir, List<string> unsupported)
    {
        public string? ResolveImageTexture(XElement input, string inputName, bool allowNormalMapHop)
        {
            XElement scope = FindConnectionScope(input);
            XElement? node = ResolveConnectedNode(input, scope, out scope);
            if (node == null)
            {
                unsupported.Add($"{inputName} (unsupported node graph)");
                return null;
            }

            // Pass through one <normalmap> hop: <normalmap> → its "in" input → image.
            if (allowNormalMapHop && IsElement(node, "normalmap"))
            {
                XElement? innerInput = node.Elements()
                    .FirstOrDefault(e => IsElement(e, "input")
                        && string.Equals(e.Attribute("name")?.Value, "in",
                            StringComparison.OrdinalIgnoreCase));
                node = innerInput != null
                    ? ResolveConnectedNode(innerInput, scope, out scope)
                    : null;
                if (node == null)
                {
                    unsupported.Add($"{inputName} (unsupported node graph)");
                    return null;
                }
            }

            if (!IsElement(node, "image") && !IsElement(node, "bitmap"))
            {
                unsupported.Add($"{inputName} (unsupported node graph: {node.Name.LocalName})");
                return null;
            }

            // The file path is either a "file" attribute or a "file" input value.
            string? file = node.Attribute("file")?.Value
                ?? node.Elements()
                    .FirstOrDefault(e => IsElement(e, "input")
                        && string.Equals(e.Attribute("name")?.Value, "file", StringComparison.OrdinalIgnoreCase))
                    ?.Attribute("value")?.Value;

            if (file == null)
            {
                unsupported.Add($"{inputName} (image node has no file)");
                return null;
            }

            string full = Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(baseDir, file));
            if (!File.Exists(full))
            {
                // Return null (treated as "not texture-connected") so the
                // constant-promotion rule can't fire and the input keeps its
                // intended default constant.
                unsupported.Add($"{inputName} (image file not found: {file})");
                return null;
            }
            return full;
        }

        private XElement? ResolveConnectedNode(
            XElement connection, XElement defaultScope, out XElement resolvedScope,
            int graphDepth = 0)
        {
            if (graphDepth > 16)
            {
                resolvedScope = defaultScope;
                return null;
            }

            string? nodeName = connection.Attribute("nodename")?.Value;
            if (nodeName != null)
            {
                resolvedScope = defaultScope;
                return FindNode(defaultScope, nodeName);
            }

            string? graphName = connection.Attribute("nodegraph")?.Value;
            XElement? graph = graphName != null ? FindNodeGraph(graphName) : null;
            if (graph == null)
            {
                resolvedScope = defaultScope;
                return null;
            }

            string? outputName = connection.Attribute("output")?.Value;
            XElement[] outputs = graph.Elements()
                .Where(e => IsElement(e, "output"))
                .ToArray();
            XElement? output = outputName != null
                ? outputs.FirstOrDefault(e => e.Attribute("name")?.Value == outputName)
                : outputs.Length == 1
                    ? outputs[0]
                    : outputs.FirstOrDefault(e => e.Attribute("name")?.Value == "out");

            if (output == null)
            {
                resolvedScope = graph;
                return null;
            }

            return ResolveConnectedNode(output, graph, out resolvedScope, graphDepth + 1);
        }

        private XElement FindConnectionScope(XElement connection) =>
            connection.Ancestors().FirstOrDefault(e => IsElement(e, "nodegraph")) ?? root;

        private XElement? FindNodeGraph(string name) =>
            root.Descendants().FirstOrDefault(e =>
                IsElement(e, "nodegraph") && e.Attribute("name")?.Value == name);

        /// <summary>
        /// Finds a node element by its <c>name</c> attribute anywhere in the
        /// supplied scope. <c>&lt;input&gt;</c>/<c>&lt;output&gt;</c> elements also
        /// carry names, so they are excluded from the search.
        /// </summary>
        private static XElement? FindNode(XElement scope, string name) =>
            scope.Descendants().FirstOrDefault(e =>
                !IsElement(e, "input") && !IsElement(e, "output") &&
                e.Attribute("name")?.Value == name);

        private static bool IsElement(XElement e, string localName) =>
            string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);
    }

    // ── JSON DTO ──────────────────────────────────────────────────────────────

    /// <summary>
    /// JSON DTO for hand-authored <c>.pbr.json</c> material files.
    /// All fields are optional so partial files are accepted gracefully.
    /// </summary>
    private sealed class PbrJsonDto
    {
        [JsonPropertyName("base_color")]
        public float[]? BaseColor { get; set; }

        [JsonPropertyName("base_metalness")]
        public float? BaseMetalness { get; set; }

        [JsonPropertyName("base_diffuse_roughness")]
        public float? BaseDiffuseRoughness { get; set; }

        [JsonPropertyName("base_weight")]
        public float? BaseWeight { get; set; }

        [JsonPropertyName("specular_weight")]
        public float? SpecularWeight { get; set; }

        [JsonPropertyName("specular_color")]
        public float[]? SpecularColor { get; set; }

        [JsonPropertyName("specular_roughness")]
        public float? SpecularRoughness { get; set; }

        [JsonPropertyName("specular_roughness_anisotropy")]
        public float? SpecularAnisotropy { get; set; }

        [JsonPropertyName("specular_ior")]
        public float? SpecularIor { get; set; }

        [JsonPropertyName("emission_color")]
        public float[]? EmissionColor { get; set; }

        [JsonPropertyName("emission_luminance")]
        public float? EmissionLuminance { get; set; }

        [JsonPropertyName("coat_weight")]
        public float? CoatWeight { get; set; }

        [JsonPropertyName("coat_color")]
        public float[]? CoatColor { get; set; }

        [JsonPropertyName("coat_roughness")]
        public float? CoatRoughness { get; set; }

        [JsonPropertyName("coat_roughness_anisotropy")]
        public float? CoatAnisotropy { get; set; }

        [JsonPropertyName("coat_ior")]
        public float? CoatIor { get; set; }

        [JsonPropertyName("coat_darkening")]
        public float? CoatDarkening { get; set; }

        [JsonPropertyName("fuzz_weight")]
        public float? FuzzWeight { get; set; }

        [JsonPropertyName("fuzz_color")]
        public float[]? FuzzColor { get; set; }

        [JsonPropertyName("fuzz_roughness")]
        public float? FuzzRoughness { get; set; }

        [JsonPropertyName("thin_film_weight")]
        public float? ThinFilmWeight { get; set; }

        [JsonPropertyName("thin_film_thickness")]
        public float? ThinFilmThickness { get; set; }

        [JsonPropertyName("thin_film_ior")]
        public float? ThinFilmIor { get; set; }

        [JsonPropertyName("geometry_opacity")]
        public float? GeometryOpacity { get; set; }

        [JsonPropertyName("texture_scale")]
        public float? TextureScale { get; set; }

        [JsonPropertyName("base_color_texture")]
        public string? BaseColorTexture { get; set; }

        [JsonPropertyName("base_metalness_texture")]
        public string? BaseMetalnessTexture { get; set; }

        [JsonPropertyName("specular_roughness_texture")]
        public string? SpecularRoughnessTexture { get; set; }

        [JsonPropertyName("geometry_normal_texture")]
        public string? GeometryNormalTexture { get; set; }

        [JsonPropertyName("emission_color_texture")]
        public string? EmissionColorTexture { get; set; }

        [JsonPropertyName("geometry_opacity_texture")]
        public string? GeometryOpacityTexture { get; set; }
    }
}

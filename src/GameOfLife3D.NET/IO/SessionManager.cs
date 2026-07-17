using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameOfLife3D.NET.Camera;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.Rendering;

namespace GameOfLife3D.NET.IO;

public sealed class SessionData
{
    public GameState? GameState { get; set; }
    public CameraSessionData? Camera { get; set; }
    public RenderSessionData? RenderSettings { get; set; }
    public int DisplayStart { get; set; }
    public int DisplayEnd { get; set; }
}

public sealed class CameraSessionData
{
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetZ { get; set; }
    public float Distance { get; set; }
    public float Phi { get; set; }
    public float Theta { get; set; }
}

public sealed class RenderSessionData
{
    public float CellPadding { get; set; }
    public bool FaceColorCycling { get; set; }
    public bool EdgeColorCycling { get; set; }
    public float EdgeColorAngle { get; set; }

    // Legacy: pre-floor-mode sessions only had a grid-lines toggle. Nullable
    // so we can distinguish "field absent" (modern save) from an explicit
    // false; the JSON serializer's WhenWritingNull rule then keeps modern
    // saves from emitting it.
    public bool? ShowGridLines { get; set; }

    // Floor selection (Off / Grid / Reflective). Nullable so loaders can tell
    // "field absent" (legacy) from "field present with value 0 = Off".
    public int? FloorMode { get; set; }

    public bool ShowGenerationLabels { get; set; }
    public bool ShowWireframe { get; set; }
    public float CellColorR { get; set; }
    public float CellColorG { get; set; } = 1f;
    public float CellColorB { get; set; } = 0.533f;
    public float EdgeColorR { get; set; } = 1f;
    public float EdgeColorG { get; set; } = 1f;
    public float EdgeColorB { get; set; } = 1f;

    // Fog
    public bool FogEnabled { get; set; }
    public float FogStart { get; set; } = 20f;
    public float FogEnd { get; set; } = 100f;
    public float FogColorR { get; set; } = 0.05f;
    public float FogColorG { get; set; } = 0.05f;
    public float FogColorB { get; set; } = 0.08f;

    // Clip
    public bool ClipEnabled { get; set; }
    public float ClipY { get; set; } = 25f;

    // Background
    public int BackgroundMode { get; set; }
    public float BgTopR { get; set; } = 0.08f;
    public float BgTopG { get; set; } = 0.08f;
    public float BgTopB { get; set; } = 0.15f;
    public float BgBottomR { get; set; } = 0.02f;
    public float BgBottomG { get; set; } = 0.02f;
    public float BgBottomB { get; set; } = 0.04f;

    // Bloom
    public bool BloomEnabled { get; set; }
    public float BloomThreshold { get; set; } = 0.6f;
    public float BloomIntensity { get; set; } = 0.5f;

    // Cell shape. Nullable so loaders can tell "field absent" (legacy) from
    // "explicit value". Legacy `UseBeveledCubes` is kept for one release as a
    // fallback for sessions saved before this feature landed.
    public int? Shape { get; set; }

    // Legacy field — only read on load when `Shape` is null. Never written
    // by `FromRenderSettings` anymore.
    public bool? UseBeveledCubes { get; set; }

    // Reflective-floor / water tuning. Nullable so legacy sessions adopt the
    // current code defaults instead of zero-filled values.
    public float? WaveStrength { get; set; }
    public float? WaveSpeed { get; set; }
    public float? WaterTintR { get; set; }
    public float? WaterTintG { get; set; }
    public float? WaterTintB { get; set; }
    public float? Reflectivity { get; set; }
    public float? ReflectionResolutionScale { get; set; }

    // Face-cycling gradient stops, flattened RGB triples (length = 3 * stopCount).
    // Nullable for backward compatibility with sessions saved before the editor landed.
    public float[]? GradientStops { get; set; }

    // PBR material — all fields nullable so legacy sessions load cleanly.
    // When MaterialFilePath is present but the individual parameter fields are
    // absent (hand-edited JSON), the material defaults are used.
    public string? MaterialFilePath { get; set; }
    public float? MatBaseColorR { get; set; }
    public float? MatBaseColorG { get; set; }
    public float? MatBaseColorB { get; set; }
    public float? MatBaseMetalness { get; set; }
    public float? MatBaseDiffuseRoughness { get; set; }
    public float? MatSpecularRoughness { get; set; }
    public float? MatSpecularIor { get; set; }
    public float? MatEmissionColorR { get; set; }
    public float? MatEmissionColorG { get; set; }
    public float? MatEmissionColorB { get; set; }
    public float? MatEmissionLuminance { get; set; }
    public float? MatCoatWeight { get; set; }
    public float? MatCoatRoughness { get; set; }
    public float? MatCoatIor { get; set; }
    public float? MatBaseWeight { get; set; }
    public float? MatSpecularWeight { get; set; }
    public float? MatSpecularColorR { get; set; }
    public float? MatSpecularColorG { get; set; }
    public float? MatSpecularColorB { get; set; }
    public float? MatSpecularAnisotropy { get; set; }
    public float? MatCoatColorR { get; set; }
    public float? MatCoatColorG { get; set; }
    public float? MatCoatColorB { get; set; }
    public float? MatCoatAnisotropy { get; set; }
    public float? MatCoatDarkening { get; set; }
    public float? MatFuzzWeight { get; set; }
    public float? MatFuzzColorR { get; set; }
    public float? MatFuzzColorG { get; set; }
    public float? MatFuzzColorB { get; set; }
    public float? MatFuzzRoughness { get; set; }
    public float? MatThinFilmWeight { get; set; }
    public float? MatThinFilmThickness { get; set; }
    public float? MatThinFilmIor { get; set; }
    public float? MatGeometryOpacity { get; set; }
    public float? MatTextureScale { get; set; }
    // Texture slots: absolute file paths, null = constant only.
    public string? MatBaseColorTexture { get; set; }
    public string? MatMetalnessTexture { get; set; }
    public string? MatRoughnessTexture { get; set; }
    public string? MatNormalTexture { get; set; }
    public string? MatEmissionTexture { get; set; }
    public string? MatOpacityTexture { get; set; }
    // Nullable provenance flags distinguish fields absent from legacy sessions
    // from explicit false values written for author-supplied constants.
    public bool? MatBaseMetalnessPromotedForTexture { get; set; }
    public bool? MatSpecularRoughnessPromotedForTexture { get; set; }
    public bool? MatEmissionColorPromotedForTexture { get; set; }
    public float? EnvIntensity { get; set; }
}

public static class SessionManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Save(string path, GameEngine engine, CameraController camera, RenderSettings settings,
        int displayStart, int displayEnd)
    {
        var session = new SessionData
        {
            GameState = engine.ExportState(),
            Camera = FromCameraState(camera.GetState()),
            RenderSettings = FromRenderSettings(settings),
            DisplayStart = displayStart,
            DisplayEnd = displayEnd,
        };

        string json = JsonSerializer.Serialize(session, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static SessionData? Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SessionData>(json, JsonOptions);
    }

    private static CameraSessionData FromCameraState(CameraState state) => new()
    {
        TargetX = state.Target.X,
        TargetY = state.Target.Y,
        TargetZ = state.Target.Z,
        Distance = state.Distance,
        Phi = state.Phi,
        Theta = state.Theta,
    };

    public static CameraState ToCameraState(CameraSessionData data) => new()
    {
        Target = new Vector3(data.TargetX, data.TargetY, data.TargetZ),
        Distance = data.Distance,
        Phi = data.Phi,
        Theta = data.Theta,
    };

    private static RenderSessionData FromRenderSettings(RenderSettings s) => new()
    {
        CellPadding = s.CellPadding,
        FaceColorCycling = s.FaceColorCycling,
        EdgeColorCycling = s.EdgeColorCycling,
        EdgeColorAngle = s.EdgeColorAngle,
        // ShowGridLines is intentionally not populated on save — the canonical
        // field is now FloorMode. The property still exists on the data object
        // so old session files can be read back via the legacy fallback path
        // in ApplyRenderSettings.
        FloorMode = (int)s.FloorMode,
        ShowGenerationLabels = s.ShowGenerationLabels,
        ShowWireframe = s.ShowWireframe,
        CellColorR = s.CellColor.X,
        CellColorG = s.CellColor.Y,
        CellColorB = s.CellColor.Z,
        EdgeColorR = s.EdgeColor.X,
        EdgeColorG = s.EdgeColor.Y,
        EdgeColorB = s.EdgeColor.Z,
        // Fog
        FogEnabled = s.FogEnabled,
        FogStart = s.FogStart,
        FogEnd = s.FogEnd,
        FogColorR = s.FogColor.X,
        FogColorG = s.FogColor.Y,
        FogColorB = s.FogColor.Z,
        // Clip
        ClipEnabled = s.ClipEnabled,
        ClipY = s.ClipY,
        // Background
        BackgroundMode = (int)s.BackgroundMode,
        BgTopR = s.BackgroundTopColor.X,
        BgTopG = s.BackgroundTopColor.Y,
        BgTopB = s.BackgroundTopColor.Z,
        BgBottomR = s.BackgroundBottomColor.X,
        BgBottomG = s.BackgroundBottomColor.Y,
        BgBottomB = s.BackgroundBottomColor.Z,
        // Bloom
        BloomEnabled = s.BloomEnabled,
        BloomThreshold = s.BloomThreshold,
        BloomIntensity = s.BloomIntensity,
        // Cell shape (new field; legacy UseBeveledCubes intentionally not written)
        Shape = (int)s.Shape,
        // Reflective floor / water
        WaveStrength = s.WaveStrength,
        WaveSpeed = s.WaveSpeed,
        WaterTintR = s.WaterTint.X,
        WaterTintG = s.WaterTint.Y,
        WaterTintB = s.WaterTint.Z,
        Reflectivity = s.Reflectivity,
        ReflectionResolutionScale = s.ReflectionResolutionScale,
        // Gradient stops (flatten R, G, B triples in order)
        GradientStops = FlattenStops(s.GradientStops),
        // PBR material
        MaterialFilePath = s.MaterialFilePath,
        MatBaseColorR = s.ActiveMaterial?.BaseColor.X,
        MatBaseColorG = s.ActiveMaterial?.BaseColor.Y,
        MatBaseColorB = s.ActiveMaterial?.BaseColor.Z,
        MatBaseMetalness = s.ActiveMaterial?.BaseMetalness,
        MatBaseDiffuseRoughness = s.ActiveMaterial?.BaseDiffuseRoughness,
        MatSpecularRoughness = s.ActiveMaterial?.SpecularRoughness,
        MatSpecularIor = s.ActiveMaterial?.SpecularIor,
        MatEmissionColorR = s.ActiveMaterial?.EmissionColor.X,
        MatEmissionColorG = s.ActiveMaterial?.EmissionColor.Y,
        MatEmissionColorB = s.ActiveMaterial?.EmissionColor.Z,
        MatEmissionLuminance = s.ActiveMaterial?.EmissionLuminance,
        MatCoatWeight = s.ActiveMaterial?.CoatWeight,
        MatCoatRoughness = s.ActiveMaterial?.CoatRoughness,
        MatCoatIor = s.ActiveMaterial?.CoatIor,
        MatBaseWeight = s.ActiveMaterial?.BaseWeight,
        MatSpecularWeight = s.ActiveMaterial?.SpecularWeight,
        MatSpecularColorR = s.ActiveMaterial?.SpecularColor.X,
        MatSpecularColorG = s.ActiveMaterial?.SpecularColor.Y,
        MatSpecularColorB = s.ActiveMaterial?.SpecularColor.Z,
        MatSpecularAnisotropy = s.ActiveMaterial?.SpecularAnisotropy,
        MatCoatColorR = s.ActiveMaterial?.CoatColor.X,
        MatCoatColorG = s.ActiveMaterial?.CoatColor.Y,
        MatCoatColorB = s.ActiveMaterial?.CoatColor.Z,
        MatCoatAnisotropy = s.ActiveMaterial?.CoatAnisotropy,
        MatCoatDarkening = s.ActiveMaterial?.CoatDarkening,
        MatFuzzWeight = s.ActiveMaterial?.FuzzWeight,
        MatFuzzColorR = s.ActiveMaterial?.FuzzColor.X,
        MatFuzzColorG = s.ActiveMaterial?.FuzzColor.Y,
        MatFuzzColorB = s.ActiveMaterial?.FuzzColor.Z,
        MatFuzzRoughness = s.ActiveMaterial?.FuzzRoughness,
        MatThinFilmWeight = s.ActiveMaterial?.ThinFilmWeight,
        MatThinFilmThickness = s.ActiveMaterial?.ThinFilmThickness,
        MatThinFilmIor = s.ActiveMaterial?.ThinFilmIor,
        MatGeometryOpacity = s.ActiveMaterial?.GeometryOpacity,
        MatTextureScale = s.ActiveMaterial?.TextureScale,
        MatBaseColorTexture = s.ActiveMaterial?.BaseColorTexture,
        MatMetalnessTexture = s.ActiveMaterial?.MetalnessTexture,
        MatRoughnessTexture = s.ActiveMaterial?.RoughnessTexture,
        MatNormalTexture = s.ActiveMaterial?.NormalTexture,
        MatEmissionTexture = s.ActiveMaterial?.EmissionTexture,
        MatOpacityTexture = s.ActiveMaterial?.OpacityTexture,
        MatBaseMetalnessPromotedForTexture =
            s.ActiveMaterial?.BaseMetalnessPromotedForTexture,
        MatSpecularRoughnessPromotedForTexture =
            s.ActiveMaterial?.SpecularRoughnessPromotedForTexture,
        MatEmissionColorPromotedForTexture =
            s.ActiveMaterial?.EmissionColorPromotedForTexture,
        EnvIntensity = s.EnvIntensity,
    };

    private static float[] FlattenStops(IReadOnlyList<Vector3> stops)
    {
        var arr = new float[stops.Count * 3];
        for (int i = 0; i < stops.Count; i++)
        {
            arr[i * 3 + 0] = stops[i].X;
            arr[i * 3 + 1] = stops[i].Y;
            arr[i * 3 + 2] = stops[i].Z;
        }
        return arr;
    }

    public static void ApplyRenderSettings(RenderSessionData data, RenderSettings target)
    {
        target.CellPadding = data.CellPadding;
        target.FaceColorCycling = data.FaceColorCycling;
        target.EdgeColorCycling = data.EdgeColorCycling;
        target.EdgeColorAngle = data.EdgeColorAngle;
        // Floor mode: prefer the new field, fall back to the legacy
        // ShowGridLines bool for sessions saved before this feature landed.
        // Sessions that have neither (extremely old or hand-edited) leave
        // target.FloorMode untouched so it keeps the current renderer default
        // rather than being forced to Off.
        if (data.FloorMode.HasValue)
        {
            target.FloorMode = (FloorMode)Math.Clamp(data.FloorMode.Value, 0, 2);
        }
        else if (data.ShowGridLines.HasValue)
        {
            target.FloorMode = data.ShowGridLines.Value ? FloorMode.Grid : FloorMode.Off;
        }
        target.ShowGenerationLabels = data.ShowGenerationLabels;
        target.ShowWireframe = data.ShowWireframe;
        target.CellColor = new Vector3(data.CellColorR, data.CellColorG, data.CellColorB);
        target.EdgeColor = new Vector3(data.EdgeColorR, data.EdgeColorG, data.EdgeColorB);
        // Fog
        target.FogEnabled = data.FogEnabled;
        target.FogStart = data.FogStart;
        target.FogEnd = data.FogEnd;
        target.FogColor = new Vector3(data.FogColorR, data.FogColorG, data.FogColorB);
        // Clip
        target.ClipEnabled = data.ClipEnabled;
        target.ClipY = data.ClipY;
        // Background
        target.BackgroundMode = (BackgroundMode)data.BackgroundMode;
        target.BackgroundTopColor = new Vector3(data.BgTopR, data.BgTopG, data.BgTopB);
        target.BackgroundBottomColor = new Vector3(data.BgBottomR, data.BgBottomG, data.BgBottomB);
        // Bloom
        target.BloomEnabled = data.BloomEnabled;
        target.BloomThreshold = data.BloomThreshold;
        target.BloomIntensity = data.BloomIntensity;
        // Cell shape: prefer the new field, fall back to the legacy
        // UseBeveledCubes bool for sessions saved before this feature landed.
        // Use Enum.IsDefined rather than clamping so a session written by a
        // newer build with an unknown shape value falls back to the renderer
        // default explicitly (instead of silently saturating to the highest
        // known enum value).
        if (data.Shape.HasValue)
        {
            int raw = data.Shape.Value;
            target.Shape = Enum.IsDefined(typeof(CellShape), raw)
                ? (CellShape)raw
                : CellShape.BeveledCube;
        }
        else if (data.UseBeveledCubes.HasValue)
        {
            target.Shape = data.UseBeveledCubes.Value ? CellShape.BeveledCube : CellShape.Cube;
        }
        // Reflective floor / water — each field falls back to the current
        // setting on the target if the session predates that field.
        if (data.WaveStrength.HasValue) target.WaveStrength = data.WaveStrength.Value;
        if (data.WaveSpeed.HasValue) target.WaveSpeed = data.WaveSpeed.Value;
        if (data.WaterTintR.HasValue && data.WaterTintG.HasValue && data.WaterTintB.HasValue)
        {
            target.WaterTint = new Vector3(
                data.WaterTintR.Value, data.WaterTintG.Value, data.WaterTintB.Value);
        }
        if (data.Reflectivity.HasValue) target.Reflectivity = data.Reflectivity.Value;
        if (data.ReflectionResolutionScale.HasValue)
            target.ReflectionResolutionScale = Math.Clamp(data.ReflectionResolutionScale.Value, 0.25f, 1f);
        // Gradient stops — only adopt when the saved data is structurally valid.
        // Older sessions (or hand-edited JSON) that omit the field, supply too few
        // stops, or have a non-multiple-of-3 length explicitly reset to the Classic
        // default. Without this reset, loading an old session would silently keep
        // whatever palette the user had edited in the current run.
        if (data.GradientStops is { Length: > 0 } flat
            && flat.Length % 3 == 0
            && flat.Length / 3 >= RenderSettings.MinGradientStops)
        {
            int count = Math.Min(flat.Length / 3, RenderSettings.MaxGradientStops);
            var rebuilt = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                rebuilt.Add(new Vector3(
                    flat[i * 3 + 0],
                    flat[i * 3 + 1],
                    flat[i * 3 + 2]));
            }
            target.GradientStops = rebuilt;
        }
        else
        {
            target.ResetGradient();
        }
        // PBR material — restore only when at least one parameter field is present.
        // A session that predates this feature will have all Mat* fields null, so
        // ActiveMaterial stays null (legacy shader). If any parameter field is present
        // we reconstruct the material from persisted values; fields that are absent
        // fall back to CellMaterial.Default.
        bool hasMaterial = data.MaterialFilePath is not null
            || data.MatBaseColorR.HasValue
            || data.MatBaseColorG.HasValue
            || data.MatBaseColorB.HasValue
            || data.MatBaseMetalness.HasValue
            || data.MatBaseDiffuseRoughness.HasValue
            || data.MatSpecularRoughness.HasValue
            || data.MatSpecularIor.HasValue
            || data.MatEmissionColorR.HasValue
            || data.MatEmissionColorG.HasValue
            || data.MatEmissionColorB.HasValue
            || data.MatEmissionLuminance.HasValue
            || data.MatCoatWeight.HasValue
            || data.MatCoatRoughness.HasValue
            || data.MatCoatIor.HasValue
            || data.MatBaseWeight.HasValue
            || data.MatSpecularWeight.HasValue
            || data.MatSpecularColorR.HasValue
            || data.MatSpecularColorG.HasValue
            || data.MatSpecularColorB.HasValue
            || data.MatSpecularAnisotropy.HasValue
            || data.MatCoatColorR.HasValue
            || data.MatCoatColorG.HasValue
            || data.MatCoatColorB.HasValue
            || data.MatCoatAnisotropy.HasValue
            || data.MatCoatDarkening.HasValue
            || data.MatFuzzWeight.HasValue
            || data.MatFuzzColorR.HasValue
            || data.MatFuzzColorG.HasValue
            || data.MatFuzzColorB.HasValue
            || data.MatFuzzRoughness.HasValue
            || data.MatThinFilmWeight.HasValue
            || data.MatThinFilmThickness.HasValue
            || data.MatThinFilmIor.HasValue
            || data.MatGeometryOpacity.HasValue
            || data.MatTextureScale.HasValue
            || data.MatBaseColorTexture is not null
            || data.MatMetalnessTexture is not null
            || data.MatRoughnessTexture is not null
            || data.MatNormalTexture is not null
            || data.MatEmissionTexture is not null
            || data.MatOpacityTexture is not null;

        if (hasMaterial)
        {
            var defaults = CellMaterial.Default;
            // Sessions written before provenance was persisted retain the old
            // identity-value inference. New saves write false explicitly for
            // author-supplied identity constants, avoiding that ambiguity.
            bool metalnessPromoted = data.MatBaseMetalnessPromotedForTexture
                ?? (data.MatMetalnessTexture is not null && data.MatBaseMetalness == 1f);
            bool roughnessPromoted = data.MatSpecularRoughnessPromotedForTexture
                ?? (data.MatRoughnessTexture is not null && data.MatSpecularRoughness == 1f);
            bool emissionPromoted = data.MatEmissionColorPromotedForTexture
                ?? (data.MatEmissionTexture is not null
                    && data.MatEmissionColorR == 1f
                    && data.MatEmissionColorG == 1f
                    && data.MatEmissionColorB == 1f);
            target.ActiveMaterial = new CellMaterial
            {
                BaseColor = new Vector3(
                    data.MatBaseColorR ?? defaults.BaseColor.X,
                    data.MatBaseColorG ?? defaults.BaseColor.Y,
                    data.MatBaseColorB ?? defaults.BaseColor.Z),
                BaseMetalness = data.MatBaseMetalness ?? defaults.BaseMetalness,
                BaseDiffuseRoughness = data.MatBaseDiffuseRoughness ?? defaults.BaseDiffuseRoughness,
                SpecularRoughness = data.MatSpecularRoughness ?? defaults.SpecularRoughness,
                SpecularIor = data.MatSpecularIor ?? defaults.SpecularIor,
                EmissionColor = new Vector3(
                    data.MatEmissionColorR ?? defaults.EmissionColor.X,
                    data.MatEmissionColorG ?? defaults.EmissionColor.Y,
                    data.MatEmissionColorB ?? defaults.EmissionColor.Z),
                EmissionLuminance = data.MatEmissionLuminance ?? defaults.EmissionLuminance,
                CoatWeight = data.MatCoatWeight ?? defaults.CoatWeight,
                CoatRoughness = data.MatCoatRoughness ?? defaults.CoatRoughness,
                CoatIor = data.MatCoatIor ?? defaults.CoatIor,
                BaseWeight = data.MatBaseWeight ?? defaults.BaseWeight,
                SpecularWeight = data.MatSpecularWeight ?? defaults.SpecularWeight,
                SpecularColor = new Vector3(
                    data.MatSpecularColorR ?? defaults.SpecularColor.X,
                    data.MatSpecularColorG ?? defaults.SpecularColor.Y,
                    data.MatSpecularColorB ?? defaults.SpecularColor.Z),
                SpecularAnisotropy = data.MatSpecularAnisotropy ?? defaults.SpecularAnisotropy,
                CoatColor = new Vector3(
                    data.MatCoatColorR ?? defaults.CoatColor.X,
                    data.MatCoatColorG ?? defaults.CoatColor.Y,
                    data.MatCoatColorB ?? defaults.CoatColor.Z),
                CoatAnisotropy = data.MatCoatAnisotropy ?? defaults.CoatAnisotropy,
                CoatDarkening = data.MatCoatDarkening ?? defaults.CoatDarkening,
                FuzzWeight = data.MatFuzzWeight ?? defaults.FuzzWeight,
                FuzzColor = new Vector3(
                    data.MatFuzzColorR ?? defaults.FuzzColor.X,
                    data.MatFuzzColorG ?? defaults.FuzzColor.Y,
                    data.MatFuzzColorB ?? defaults.FuzzColor.Z),
                FuzzRoughness = data.MatFuzzRoughness ?? defaults.FuzzRoughness,
                ThinFilmWeight = data.MatThinFilmWeight ?? defaults.ThinFilmWeight,
                ThinFilmThickness = data.MatThinFilmThickness ?? defaults.ThinFilmThickness,
                ThinFilmIor = data.MatThinFilmIor ?? defaults.ThinFilmIor,
                GeometryOpacity = data.MatGeometryOpacity ?? defaults.GeometryOpacity,
                TextureScale = data.MatTextureScale ?? defaults.TextureScale,
                BaseColorTexture = data.MatBaseColorTexture,
                MetalnessTexture = data.MatMetalnessTexture,
                BaseMetalnessPromotedForTexture = metalnessPromoted,
                RoughnessTexture = data.MatRoughnessTexture,
                SpecularRoughnessPromotedForTexture = roughnessPromoted,
                NormalTexture = data.MatNormalTexture,
                EmissionTexture = data.MatEmissionTexture,
                EmissionColorPromotedForTexture = emissionPromoted,
                OpacityTexture = data.MatOpacityTexture,
            };
            target.ActiveMaterial = DropMissingMaterialTextures(target.ActiveMaterial);
            target.MaterialFilePath = data.MaterialFilePath;
        }
        else
        {
            target.ActiveMaterial = null;
            target.MaterialFilePath = null;
        }
        if (data.EnvIntensity.HasValue)
            target.EnvIntensity = Math.Clamp(data.EnvIntensity.Value, 0f, 5f);
    }

    /// <summary>
    /// Drops material texture slots whose image file no longer exists (the
    /// session outlived its textures), logging each drop to stderr. When a
    /// dropped slot's constant was promoted by the importer, the constant is
    /// reset to the material default — the same fallback the importer applies
    /// at import time — so a missing texture can't silently turn the surface
    /// fully metallic, mirror-smooth, or emissive. Explicit author constants
    /// are retained.
    /// </summary>
    private static CellMaterial DropMissingMaterialTextures(CellMaterial mat)
    {
        var defaults = CellMaterial.Default;

        (string? Path, bool Dropped) Check(string? path, string inputName)
        {
            if (path is null || File.Exists(path)) return (path, false);
            Console.Error.WriteLine(
                $"SessionManager: material texture for {inputName} not found, using constant: {path}");
            return (null, true);
        }

        var (baseColorTex, _) = Check(mat.BaseColorTexture, "base_color");
        var (metalnessTex, metalnessDropped) = Check(mat.MetalnessTexture, "base_metalness");
        var (roughnessTex, roughnessDropped) = Check(mat.RoughnessTexture, "specular_roughness");
        var (normalTex, _) = Check(mat.NormalTexture, "geometry_normal");
        var (emissionTex, emissionDropped) = Check(mat.EmissionTexture, "emission_color");
        var (opacityTex, _) = Check(mat.OpacityTexture, "geometry_opacity");

        return mat with
        {
            BaseColorTexture = baseColorTex,
            MetalnessTexture = metalnessTex,
            BaseMetalness = metalnessDropped && mat.BaseMetalnessPromotedForTexture
                ? defaults.BaseMetalness : mat.BaseMetalness,
            BaseMetalnessPromotedForTexture =
                !metalnessDropped && mat.BaseMetalnessPromotedForTexture,
            RoughnessTexture = roughnessTex,
            SpecularRoughness = roughnessDropped && mat.SpecularRoughnessPromotedForTexture
                ? defaults.SpecularRoughness : mat.SpecularRoughness,
            SpecularRoughnessPromotedForTexture =
                !roughnessDropped && mat.SpecularRoughnessPromotedForTexture,
            NormalTexture = normalTex,
            EmissionTexture = emissionTex,
            EmissionColor = emissionDropped && mat.EmissionColorPromotedForTexture
                ? defaults.EmissionColor : mat.EmissionColor,
            EmissionColorPromotedForTexture =
                !emissionDropped && mat.EmissionColorPromotedForTexture,
            OpacityTexture = opacityTex,
        };
    }
}

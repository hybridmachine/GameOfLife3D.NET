using System.Collections.Immutable;
using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

public sealed class RenderSettings
{
    public const int MinGradientStops = 2;
    public const int MaxGradientStops = 8;

    /// <summary>
    /// Default face-cycling palette — matches the original hard-coded gradient
    /// (blue → green → yellow → black → purple → wrap). Kept identical so any
    /// pre-feature session loads pixel-for-pixel the same. Exposed as an
    /// <see cref="ImmutableArray{T}"/> so external callers (and built-in presets
    /// that reference it) can't mutate the canonical default in place.
    /// </summary>
    public static readonly ImmutableArray<Vector3> DefaultGradientStops =
        ImmutableArray.Create(
            new Vector3(0f, 0f, 1f),       // blue
            new Vector3(0f, 1f, 0f),       // green
            new Vector3(1f, 1f, 0f),       // yellow
            new Vector3(0f, 0f, 0f),       // black
            new Vector3(0.5f, 0f, 0.5f));  // purple

    public float CellPadding { get; set; } = 0.2f;
    public Vector3 CellColor { get; set; } = new(0f, 1f, 0.533f); // #00ff88
    public Vector3 EdgeColor { get; set; } = new(1f, 1f, 1f);
    public FloorMode FloorMode { get; set; } = FloorMode.Reflective;
    public bool ShowGenerationLabels { get; set; } = false;
    public bool FaceColorCycling { get; set; } = true;
    public bool EdgeColorCycling { get; set; } = true;
    public float EdgeColorAngle { get; set; } = 180f;
    public bool ShowWireframe { get; set; } = false;

    /// <summary>
    /// User-editable face-color gradient stops. Cyclic (last wraps to first).
    /// Length is constrained to [MinGradientStops, MaxGradientStops] at the UI
    /// and persistence boundaries.
    /// </summary>
    public List<Vector3> GradientStops { get; set; } = new(DefaultGradientStops);

    /// <summary>
    /// Reserved for a future linear/no-wrap toggle. Currently always treated as
    /// true (cyclic) by the shader; do not flip until the shader branch lands.
    /// </summary>
    // TODO: wire this up alongside a uGradientWrap uniform once the shader gains
    // a clamp/wrap branch in computeGradientColor.
    public bool GradientWrap { get; set; } = true;

    public void ResetGradient()
    {
        GradientStops = new List<Vector3>(DefaultGradientStops);
    }

    // Fog
    public bool FogEnabled { get; set; }
    public float FogStart { get; set; } = 20f;
    public float FogEnd { get; set; } = 100f;
    public Vector3 FogColor { get; set; } = new(0.05f, 0.05f, 0.08f);

    // Clip plane
    public bool ClipEnabled { get; set; }
    public float ClipY { get; set; } = 25f;

    // Background
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.Starfield;
    public Vector3 BackgroundTopColor { get; set; } = new(0.08f, 0.08f, 0.15f);
    public Vector3 BackgroundBottomColor { get; set; } = new(0.02f, 0.02f, 0.04f);

    // Bloom
    public bool BloomEnabled { get; set; }
    public float BloomThreshold { get; set; } = 0.6f;
    public float BloomIntensity { get; set; } = 0.5f;

    // Cell shape — chosen mesh for all alive cells. Default matches the old
    // first-run behavior when UseBeveledCubes defaulted to true.
    public CellShape Shape { get; set; } = CellShape.BeveledCube;

    // Generation fade-in (cinematic mode)
    public float FadeGeneration { get; set; } = -1f;
    public float FadeOpacity { get; set; } = 1f;

    // Global alpha multiplier applied to all non-preview cells. Used by the
    // cinematic falling-cells transition to fade the pile out before the next
    // pattern loads. 1.0 = fully opaque (normal rendering).
    public float GlobalAlpha { get; set; } = 1f;

    // Reflective floor / water — only used when FloorMode == Reflective.
    // WaveStrength=0 gives a perfect mirror; defaults are tuned for a calm,
    // slightly rippled surface that doesn't fight the cubes for attention.
    public float WaveStrength { get; set; } = 0.15f;
    public float WaveSpeed { get; set; } = 0.4f;
    public Vector3 WaterTint { get; set; } = new(0.04f, 0.08f, 0.12f);
    public float Reflectivity { get; set; } = 0.55f;
    public float ReflectionResolutionScale { get; set; } = 0.5f;
}

public enum BackgroundMode
{
    Solid,
    Gradient,
    Starfield,
}

public enum FloorMode
{
    Off,
    Grid,
    Reflective,
}

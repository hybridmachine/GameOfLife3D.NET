using System.Numerics;

namespace GameOfLife3D.NET.Rendering;

public sealed class RenderSettings
{
    public float CellPadding { get; set; } = 0.2f;
    public Vector3 CellColor { get; set; } = new(0f, 1f, 0.533f); // #00ff88
    public Vector3 EdgeColor { get; set; } = new(1f, 1f, 1f);
    public bool ShowGridLines { get; set; } = true;
    public bool ShowGenerationLabels { get; set; } = true;
    public bool FaceColorCycling { get; set; } = true;
    public bool EdgeColorCycling { get; set; } = true;
    public float EdgeColorAngle { get; set; } = 180f;
    public bool ShowWireframe { get; set; } = true;

    // Fog
    public bool FogEnabled { get; set; }
    public float FogStart { get; set; } = 20f;
    public float FogEnd { get; set; } = 100f;
    public Vector3 FogColor { get; set; } = new(0.05f, 0.05f, 0.08f);

    // Clip plane
    public bool ClipEnabled { get; set; }
    public float ClipY { get; set; } = 25f;

    // Background
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.Solid;
    public Vector3 BackgroundTopColor { get; set; } = new(0.08f, 0.08f, 0.15f);
    public Vector3 BackgroundBottomColor { get; set; } = new(0.02f, 0.02f, 0.04f);

    // Bloom
    public bool BloomEnabled { get; set; }
    public float BloomThreshold { get; set; } = 0.6f;
    public float BloomIntensity { get; set; } = 0.5f;

    // Beveled cubes
    public bool UseBeveledCubes { get; set; }
}

public enum BackgroundMode
{
    Solid,
    Gradient,
    Starfield,
}

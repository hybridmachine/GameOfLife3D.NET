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
}

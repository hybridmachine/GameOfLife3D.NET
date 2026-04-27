using System.Numerics;

namespace GameOfLife3D.NET.Camera;

public sealed record CameraState
{
    public Vector3 Target { get; init; }
    public float Distance { get; init; }
    public float Phi { get; init; }
    public float Theta { get; init; }
}

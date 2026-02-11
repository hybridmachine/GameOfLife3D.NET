using System.Numerics;

namespace GameOfLife3D.NET.Camera;

public sealed class CameraState
{
    public Vector3 Target { get; set; }
    public float Distance { get; set; }
    public float Phi { get; set; }
    public float Theta { get; set; }
}

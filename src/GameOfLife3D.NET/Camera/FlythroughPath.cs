using System.Numerics;

namespace GameOfLife3D.NET.Camera;

public sealed class FlythroughPath
{
    public List<Vector3> PositionWaypoints { get; }
    public List<Vector3> LookAtWaypoints { get; }
    public float TotalDuration { get; }

    public FlythroughPath(List<Vector3> positionWaypoints, List<Vector3> lookAtWaypoints, float totalDuration)
    {
        PositionWaypoints = positionWaypoints;
        LookAtWaypoints = lookAtWaypoints;
        TotalDuration = totalDuration;
    }
}

using System.Numerics;

namespace GameOfLife3D.NET.Camera;

public sealed class FlythroughPath
{
    public IReadOnlyList<Vector3> PositionWaypoints { get; }
    public IReadOnlyList<Vector3> LookAtWaypoints { get; }
    public float TotalDuration { get; }

    public FlythroughPath(List<Vector3> positionWaypoints, List<Vector3> lookAtWaypoints, float totalDuration)
    {
        ArgumentNullException.ThrowIfNull(positionWaypoints);
        ArgumentNullException.ThrowIfNull(lookAtWaypoints);

        if (positionWaypoints.Count != lookAtWaypoints.Count)
            throw new ArgumentException("Position and look-at waypoint counts must match.");

        if (totalDuration <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalDuration), totalDuration, "Total duration must be positive.");

        PositionWaypoints = positionWaypoints.ToArray();
        LookAtWaypoints = lookAtWaypoints.ToArray();
        TotalDuration = totalDuration;
    }
}

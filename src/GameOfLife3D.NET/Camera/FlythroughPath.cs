using System.Numerics;

namespace GameOfLife3D.NET.Camera;

public sealed class FlythroughPath
{
    public IReadOnlyList<Vector3> PositionWaypoints { get; }
    public IReadOnlyList<Vector3> LookAtWaypoints { get; }

    // Optional explicit time per waypoint (seconds, monotonically increasing, last == TotalDuration).
    // When null, segments are uniformly spaced across [0, TotalDuration].
    public IReadOnlyList<float>? WaypointTimes { get; }

    public float TotalDuration { get; }

    public FlythroughPath(List<Vector3> positionWaypoints, List<Vector3> lookAtWaypoints, float totalDuration)
        : this(positionWaypoints, lookAtWaypoints, totalDuration, null)
    {
    }

    public FlythroughPath(
        List<Vector3> positionWaypoints,
        List<Vector3> lookAtWaypoints,
        float totalDuration,
        List<float>? waypointTimes)
    {
        ArgumentNullException.ThrowIfNull(positionWaypoints);
        ArgumentNullException.ThrowIfNull(lookAtWaypoints);

        if (positionWaypoints.Count != lookAtWaypoints.Count)
            throw new ArgumentException("Position and look-at waypoint counts must match.");

        if (totalDuration <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalDuration), totalDuration, "Total duration must be positive.");

        if (waypointTimes != null)
        {
            if (waypointTimes.Count != positionWaypoints.Count)
                throw new ArgumentException("Waypoint times count must match waypoint count.", nameof(waypointTimes));
            for (int i = 1; i < waypointTimes.Count; i++)
            {
                if (waypointTimes[i] <= waypointTimes[i - 1])
                    throw new ArgumentException("Waypoint times must be strictly increasing.", nameof(waypointTimes));
            }
            WaypointTimes = waypointTimes.ToArray();
        }

        PositionWaypoints = positionWaypoints.ToArray();
        LookAtWaypoints = lookAtWaypoints.ToArray();
        TotalDuration = totalDuration;
    }

}

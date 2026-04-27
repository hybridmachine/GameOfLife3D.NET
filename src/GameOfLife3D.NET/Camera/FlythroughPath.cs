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

    // Builds a time-indexed path from a sorted list of CameraKeyframes.
    // Converts each keyframe's spherical CameraState (Target, Distance, Phi, Theta)
    // to a world-space camera position. Look-at waypoint = keyframe.Target.
    public static FlythroughPath FromKeyframes(IReadOnlyList<CameraKeyframe> keyframes)
    {
        ArgumentNullException.ThrowIfNull(keyframes);
        if (keyframes.Count < 2)
            throw new ArgumentException("At least 2 keyframes are required.", nameof(keyframes));

        var sorted = keyframes.OrderBy(k => k.TimeSeconds).ToList();
        if (sorted[0].TimeSeconds < 0)
            throw new ArgumentException("Keyframe times must be non-negative.", nameof(keyframes));

        var positions = new List<Vector3>(sorted.Count);
        var lookAts = new List<Vector3>(sorted.Count);
        var times = new List<float>(sorted.Count);

        // Normalize so first keyframe is at t=0 (mirrors flythrough wall-clock starting at 0).
        double t0 = sorted[0].TimeSeconds;

        for (int i = 0; i < sorted.Count; i++)
        {
            var key = sorted[i];
            float relativeTime = (float)(key.TimeSeconds - t0);
            if (i > 0 && relativeTime <= times[i - 1])
                relativeTime = times[i - 1] + 0.001f; // de-duplicate identical times

            positions.Add(SphericalToWorld(key.State));
            lookAts.Add(key.State.Target);
            times.Add(relativeTime);
        }

        float totalDuration = times[^1];
        if (totalDuration <= 0f)
            throw new ArgumentException("Keyframes must span a positive duration.", nameof(keyframes));

        return new FlythroughPath(positions, lookAts, totalDuration, times);
    }

    // Mirrors CameraController.UpdateCameraPosition (Y-up spherical → Cartesian).
    private static Vector3 SphericalToWorld(CameraState state)
    {
        float sinPhi = MathF.Sin(state.Phi);
        float cosPhi = MathF.Cos(state.Phi);
        float sinTheta = MathF.Sin(state.Theta);
        float cosTheta = MathF.Cos(state.Theta);

        return state.Target + new Vector3(
            state.Distance * sinPhi * sinTheta,
            state.Distance * cosPhi,
            state.Distance * sinPhi * cosTheta
        );
    }
}

using System.Numerics;
using GameOfLife3D.NET.Engine;

namespace GameOfLife3D.NET.Camera;

public static class FlythroughPathGenerator
{
    private const float BaseDuration = 36f;
    private const float MinOrbitRadius = 10f;
    private const int HotspotSegments = 5;

    public static FlythroughPath? Generate(
        IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd,
        int gridSize,
        Vector3 currentCameraPosition,
        Vector3 currentCameraTarget)
    {
        // Compute the AABB of all visible live cells in world coordinates incrementally
        float halfSize = gridSize / 2f;
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        bool hasVisibleCells = false;

        for (int genIndex = displayStart; genIndex <= displayEnd && genIndex < generations.Count; genIndex++)
        {
            var gen = generations[genIndex];
            foreach (var cell in gen.LiveCells)
            {
                var worldCell = new Vector3(cell.X - halfSize, genIndex, cell.Y - halfSize);
                min = Vector3.Min(min, worldCell);
                max = Vector3.Max(max, worldCell);
                hasVisibleCells = true;
            }
        }

        if (!hasVisibleCells)
            return null;

        var center = (min + max) * 0.5f;
        var extents = max - min;
        float modelRadius = Math.Max(extents.Length() * 0.5f, MinOrbitRadius);

        // Find density hotspots
        var hotspots = FindHotspots(generations, displayStart, displayEnd, halfSize);

        var rng = Random.Shared;

        // Build waypoints
        var positions = new List<Vector3>();
        var lookAts = new List<Vector3>();

        // 1. Blend-in from current camera pose
        positions.Add(currentCameraPosition);
        lookAts.Add(currentCameraTarget);

        // 2. Establishing shot — high and distant
        float estAngle = (float)(rng.NextDouble() * MathF.PI * 2);
        float estHeight = center.Y + modelRadius * (1.2f + (float)rng.NextDouble() * 0.4f);
        float estDist = modelRadius * (1.5f + (float)rng.NextDouble() * 0.3f);
        positions.Add(center + new Vector3(
            MathF.Cos(estAngle) * estDist,
            estHeight - center.Y,
            MathF.Sin(estAngle) * estDist));
        lookAts.Add(center);

        // 3. Descending sweep — move down along Y from one side
        float sweepAngle = estAngle + MathF.PI * (0.3f + (float)rng.NextDouble() * 0.4f);
        float sweepDist = modelRadius * (0.8f + (float)rng.NextDouble() * 0.4f);
        positions.Add(new Vector3(
            center.X + MathF.Cos(sweepAngle) * sweepDist,
            min.Y + extents.Y * 0.7f,
            center.Z + MathF.Sin(sweepAngle) * sweepDist));
        lookAts.Add(new Vector3(center.X, min.Y + extents.Y * 0.5f, center.Z));

        // 4. Close-up passes near hotspots (2-3)
        var shuffledHotspots = hotspots.OrderBy(_ => rng.Next()).Take(3).ToList();
        foreach (var hotspot in shuffledHotspots)
        {
            float angle = (float)(rng.NextDouble() * MathF.PI * 2);
            float closeDist = modelRadius * (0.3f + (float)rng.NextDouble() * 0.2f);
            closeDist = Math.Max(closeDist, MinOrbitRadius * 0.5f);
            float heightOffset = (float)(rng.NextDouble() - 0.5f) * modelRadius * 0.2f;

            positions.Add(hotspot + new Vector3(
                MathF.Cos(angle) * closeDist,
                heightOffset,
                MathF.Sin(angle) * closeDist));
            lookAts.Add(hotspot);
        }

        // 5. Orbit arcs — waypoints at varying theta/height
        int orbitCount = 2 + rng.Next(2); // 2-3 orbit waypoints
        for (int i = 0; i < orbitCount; i++)
        {
            float theta = (float)(rng.NextDouble() * MathF.PI * 2);
            float heightFrac = (float)rng.NextDouble();
            float orbitDist = modelRadius * (0.6f + (float)rng.NextDouble() * 0.6f);
            orbitDist = Math.Max(orbitDist, MinOrbitRadius);
            float y = min.Y + extents.Y * heightFrac;

            positions.Add(new Vector3(
                center.X + MathF.Cos(theta) * orbitDist,
                y,
                center.Z + MathF.Sin(theta) * orbitDist));
            lookAts.Add(new Vector3(center.X, y, center.Z));
        }

        // 6. Closing shot — different angle from start
        float closeAngle = estAngle + MathF.PI * (0.8f + (float)rng.NextDouble() * 0.4f);
        float closingDist = modelRadius * (1.0f + (float)rng.NextDouble() * 0.3f);
        float closingHeight = center.Y + modelRadius * (0.5f + (float)rng.NextDouble() * 0.3f);
        positions.Add(center + new Vector3(
            MathF.Cos(closeAngle) * closingDist,
            closingHeight - center.Y,
            MathF.Sin(closeAngle) * closingDist));
        lookAts.Add(center);

        // Duration: scale up for large models
        int visibleGens = Math.Min(displayEnd, generations.Count - 1) - displayStart + 1;
        float duration = BaseDuration + Math.Max(0, visibleGens - 20) * 0.3f;

        return new FlythroughPath(positions, lookAts, duration);
    }

    private static List<Vector3> FindHotspots(
        IReadOnlyList<Generation> generations,
        int displayStart, int displayEnd,
        float halfSize)
    {
        int visibleCount = Math.Min(displayEnd, generations.Count - 1) - displayStart + 1;
        if (visibleCount <= 0) return new List<Vector3>();

        int segmentSize = Math.Max(1, visibleCount / HotspotSegments);
        var hotspots = new List<Vector3>();

        for (int seg = 0; seg < HotspotSegments && seg * segmentSize + displayStart < generations.Count; seg++)
        {
            int segStart = displayStart + seg * segmentSize;
            int segEnd = Math.Min(segStart + segmentSize - 1, Math.Min(displayEnd, generations.Count - 1));

            // Find the generation with the most live cells in this segment
            int bestGen = segStart;
            int bestCount = 0;
            for (int g = segStart; g <= segEnd; g++)
            {
                int count = generations[g].LiveCells.Count;
                if (count > bestCount)
                {
                    bestCount = count;
                    bestGen = g;
                }
            }

            if (bestCount == 0) continue;

            // Compute centroid of live cells in that generation
            var gen = generations[bestGen];
            var centroid = Vector3.Zero;
            foreach (var cell in gen.LiveCells)
            {
                centroid += new Vector3(cell.X - halfSize, bestGen, cell.Y - halfSize);
            }
            centroid /= gen.LiveCells.Count;
            hotspots.Add(centroid);
        }

        return hotspots;
    }
}

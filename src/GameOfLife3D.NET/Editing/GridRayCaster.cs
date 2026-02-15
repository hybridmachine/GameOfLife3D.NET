using System.Numerics;
using GameOfLife3D.NET.Engine;

namespace GameOfLife3D.NET.Editing;

public sealed class GridRayCaster
{
    public Vector2Int? ScreenToGrid(float screenX, float screenY, Matrix4x4 view, Matrix4x4 proj, int viewportWidth, int viewportHeight, int gridSize)
    {
        // Convert screen coords to NDC
        float ndcX = (2f * screenX / viewportWidth) - 1f;
        float ndcY = 1f - (2f * screenY / viewportHeight);

        // Unproject near and far points
        if (!Matrix4x4.Invert(proj, out var invProj)) return null;
        if (!Matrix4x4.Invert(view, out var invView)) return null;

        var nearNDC = new Vector4(ndcX, ndcY, -1f, 1f);
        var farNDC = new Vector4(ndcX, ndcY, 1f, 1f);

        var nearClip = Vector4.Transform(nearNDC, invProj);
        nearClip /= nearClip.W;
        var farClip = Vector4.Transform(farNDC, invProj);
        farClip /= farClip.W;

        var nearWorld = Vector4.Transform(nearClip, invView);
        var farWorld = Vector4.Transform(farClip, invView);

        var rayOrigin = new Vector3(nearWorld.X, nearWorld.Y, nearWorld.Z);
        var rayEnd = new Vector3(farWorld.X, farWorld.Y, farWorld.Z);
        var rayDir = Vector3.Normalize(rayEnd - rayOrigin);

        // Intersect with Y=0 plane (generation 0)
        if (MathF.Abs(rayDir.Y) < 1e-6f) return null;

        float t = -rayOrigin.Y / rayDir.Y;
        if (t < 0) return null;

        var hitPoint = rayOrigin + rayDir * t;

        // Convert world coords to grid coords
        float halfSize = gridSize / 2f;
        int gx = (int)MathF.Floor(hitPoint.X + halfSize + 0.5f);
        int gz = (int)MathF.Floor(hitPoint.Z + halfSize + 0.5f);

        if (gx < 0 || gx >= gridSize || gz < 0 || gz >= gridSize)
            return null;

        return new Vector2Int(gx, gz);
    }
}

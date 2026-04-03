using System.Numerics;

namespace GameOfLife3D.NET.Camera;

/// <summary>
/// Centripetal Catmull-Rom spline evaluation (alpha = 0.5).
/// Centripetal parameterization prevents cusps and overshoots when control points are unevenly spaced.
/// </summary>
public static class CatmullRomSpline
{
    public static Vector3 Evaluate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        // Centripetal knot intervals: t_i = t_{i-1} + |p_i - p_{i-1}|^0.5
        float d01 = MathF.Sqrt(Vector3.Distance(p0, p1));
        float d12 = MathF.Sqrt(Vector3.Distance(p1, p2));
        float d23 = MathF.Sqrt(Vector3.Distance(p2, p3));

        // Prevent zero-length segments
        if (d01 < 1e-6f) d01 = 1f;
        if (d12 < 1e-6f) d12 = 1f;
        if (d23 < 1e-6f) d23 = 1f;

        float t0 = 0f;
        float t1 = t0 + d01;
        float t2 = t1 + d12;
        float t3 = t2 + d23;

        // Remap input t [0,1] to [t1, t2]
        float u = t1 + t * (t2 - t1);

        // Barry and Goldman's pyramidal formulation
        Vector3 a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
        Vector3 a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
        Vector3 a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;

        Vector3 b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
        Vector3 b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;

        return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
    }
}

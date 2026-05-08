// User-editable cyclic gradient — uploaded each frame from RenderSettings.
// MAX_GRADIENT_STOPS must match RenderSettings.MaxGradientStops on the C# side.
const int MAX_GRADIENT_STOPS = 8;
uniform vec3 uGradientColors[MAX_GRADIENT_STOPS];
uniform int  uGradientStopCount;
// TODO: add a uGradientWrap uniform when RenderSettings.GradientWrap is wired
// up; branch between (seg + 1) % n (wrap, current) and clamp(seg + 1, 0, n - 1).

vec3 computeGradientColor(float worldY, float minY, float maxY, float time)
{
    float range = max(maxY - minY, 1.0);
    float offset = mod(time, range);
    float adjustedY = mod(worldY - minY - offset, range);
    float t = adjustedY / range;

    // Defensive clamp — shouldn't fire because the CPU side validates count,
    // but a malformed uniform must never index past the array bounds.
    int n = clamp(uGradientStopCount, 2, MAX_GRADIENT_STOPS);

    float scaled = t * float(n);
    int seg = int(floor(scaled));
    float k = scaled - float(seg);

    // mod() guards against floating-point drift pushing seg to n on the boundary.
    int idxA = seg - (seg / n) * n;
    int idxB = (seg + 1) - ((seg + 1) / n) * n;

    vec3 a = uGradientColors[idxA];
    vec3 b = uGradientColors[idxB];
    return mix(a, b, k);
}

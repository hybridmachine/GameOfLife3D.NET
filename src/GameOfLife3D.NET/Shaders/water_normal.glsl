// Procedural water surface normal. Two long-wavelength sine swells supply the
// primary motion; layered value noise (FBM) on top of that breaks up the
// parallel-crest look you get from sums of pure sines, so the surface reads
// as natural water rather than a regular grid of ripples.
//
// We sacrifice analytical derivatives for simplicity — the height field is
// sampled four times per fragment and the normal is reconstructed by finite
// differences. The floor is a single quad so this stays cheap.

float hash21(vec2 p)
{
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

// Value noise on a unit grid, smoothstep-interpolated for C1 continuity.
float valueNoise(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);

    float a = hash21(i);
    float b = hash21(i + vec2(1.0, 0.0));
    float c = hash21(i + vec2(0.0, 1.0));
    float d = hash21(i + vec2(1.0, 1.0));

    return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// 3-octave fractal Brownian motion. Each octave drifts in its own direction
// over time so the detail evolves rather than scrolling rigidly with the
// camera. Frequency ratios are deliberately non-integer (≈2.07, 2.13) to
// avoid the visible self-similarity of pure-power-of-two FBM.
float fbm(vec2 p, float t)
{
    float v = 0.0;
    float a = 0.5;

    p += vec2( 0.32,  0.21) * t;
    v += a * valueNoise(p);
    a *= 0.5;
    p = p * 2.07 + vec2(-0.18, 0.41) * t;
    v += a * valueNoise(p);
    a *= 0.5;
    p = p * 2.13 + vec2(0.27, -0.36) * t;
    v += a * valueNoise(p);

    return v; // ~[0, 0.875]
}

float waterHeight(vec2 p, float t)
{
    vec2 d1 = normalize(vec2( 0.7,  0.4));
    vec2 d2 = normalize(vec2(-0.3,  0.9));

    float h = 0.0;
    h += 0.55 * sin(0.7 * dot(d1, p) + 1.7 * t);
    h += 0.30 * sin(1.3 * dot(d2, p) + 1.1 * t);
    // Recenter the noise around zero so it doesn't bias the surface upward.
    h += 0.50 * (fbm(p * 0.55, t * 0.55) - 0.44);
    return h;
}

vec3 computeWaterNormal(vec2 p, float t, float strength)
{
    // Finite-difference normal. eps is tuned so the FBM's finest octave is
    // resolved without aliasing on distant fragments.
    float eps = 0.08;
    float hl = waterHeight(p - vec2(eps, 0.0), t);
    float hr = waterHeight(p + vec2(eps, 0.0), t);
    float hd = waterHeight(p - vec2(0.0, eps), t);
    float hu = waterHeight(p + vec2(0.0, eps), t);

    float dhdx = (hr - hl) / (2.0 * eps);
    float dhdz = (hu - hd) / (2.0 * eps);

    return normalize(vec3(-dhdx * strength, 1.0, -dhdz * strength));
}

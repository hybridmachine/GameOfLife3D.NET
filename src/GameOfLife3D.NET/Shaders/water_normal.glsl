// Procedural water surface normal — three superposed sine waves whose analytical
// partial derivatives yield a per-fragment normal without any texture asset.
// `strength` scales the horizontal components; 0 gives a flat mirror.

vec3 computeWaterNormal(vec2 p, float t, float strength)
{
    vec2 d1 = normalize(vec2(0.7,  0.4));
    vec2 d2 = normalize(vec2(-0.3, 0.9));
    vec2 d3 = normalize(vec2(0.5, -0.8));

    float k1 = 0.7;
    float k2 = 1.3;
    float k3 = 2.1;

    float dhdx = 0.0;
    float dhdz = 0.0;

    dhdx += 0.60 * k1 * d1.x * cos(k1 * dot(d1, p) + 1.7 * t);
    dhdz += 0.60 * k1 * d1.y * cos(k1 * dot(d1, p) + 1.7 * t);

    dhdx += 0.35 * k2 * d2.x * cos(k2 * dot(d2, p) + 1.1 * t);
    dhdz += 0.35 * k2 * d2.y * cos(k2 * dot(d2, p) + 1.1 * t);

    dhdx += 0.20 * k3 * d3.x * cos(k3 * dot(d3, p) + 2.4 * t);
    dhdz += 0.20 * k3 * d3.y * cos(k3 * dot(d3, p) + 2.4 * t);

    return normalize(vec3(-dhdx * strength, 1.0, -dhdz * strength));
}

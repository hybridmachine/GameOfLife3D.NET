// Cook-Torrance BRDF helpers shared by pbr_cell.frag.
// Included via the existing #include "brdf.glsl" mechanism in ShaderProgram.

#define PI 3.14159265358979323846

// ── Fresnel ──────────────────────────────────────────────────────────────────

/// Compute dielectric F0 from index of refraction: ((ior-1)/(ior+1))^2.
float iorToF0Scalar(float ior)
{
    float t = (ior - 1.0) / (ior + 1.0);
    return t * t;
}

/// Schlick Fresnel approximation. cosTheta = dot(V, H).
vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

/// Schlick Fresnel for a scalar F0.
float fresnelSchlickScalar(float cosTheta, float F0)
{
    return F0 + (1.0 - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

// ── GGX NDF ───────────────────────────────────────────────────────────────────

/// Trowbridge-Reitz GGX normal distribution function.
float distributionGGX(float NdotH, float roughness)
{
    float a  = roughness * roughness;
    float a2 = a * a;
    float d  = NdotH * NdotH * (a2 - 1.0) + 1.0;
    return a2 / (PI * d * d + 1e-7);
}

// ── Geometry / Visibility ────────────────────────────────────────────────────

/// Smith G2 height-correlated masking-shadowing function (joint approximation).
/// Returns 1 / (4 * NdotV * NdotL) already folded in (the combined
/// visibility term V = G / (4 NdotV NdotL)).
float visibilitySmithGGX(float NdotV, float NdotL, float roughness)
{
    float a  = roughness * roughness;
    float lambdaV = NdotL * sqrt((NdotV - NdotV * a) * NdotV + a);
    float lambdaL = NdotV * sqrt((NdotL - NdotL * a) * NdotL + a);
    return 0.5 / (lambdaV + lambdaL + 1e-7);
}

// ── Cook-Torrance specular lobe ───────────────────────────────────────────────

/// Full GGX Cook-Torrance specular BRDF value for a single directional light.
/// Returns the specular radiance contribution (multiply by NdotL * lightColor
/// outside this function).
vec3 cookTorranceSpecular(float NdotH, float NdotV, float NdotL, float VdotH,
                          float roughness, vec3 F0)
{
    float D = distributionGGX(NdotH, roughness);
    float V = visibilitySmithGGX(NdotV, NdotL, roughness);
    vec3  F = fresnelSchlick(VdotH, F0);
    // D * V already incorporates 1/(4 NdotV NdotL) via visibilitySmithGGX.
    return D * V * F;
}

// ── Diffuse ───────────────────────────────────────────────────────────────────

/// Simplified Oren-Nayar diffuse (falls back to Lambertian when sigma = 0).
/// NdotL and NdotV are clamped dot products; LdotV is dot(L, V).
float orenNayarDiffuse(float NdotL, float NdotV, float LdotV, float sigma)
{
    if (sigma < 0.001)
        return 1.0 / PI;

    float sigma2 = sigma * sigma;
    float A = 1.0 - 0.5 * sigma2 / (sigma2 + 0.33);
    float B = 0.45 * sigma2 / (sigma2 + 0.09);

    float sinThetaI = sqrt(max(0.0, 1.0 - NdotL * NdotL));
    float sinThetaR = sqrt(max(0.0, 1.0 - NdotV * NdotV));

    float cosDeltaPhi = (LdotV - NdotL * NdotV)
                      / (sinThetaI * sinThetaR + 1e-7);
    cosDeltaPhi = clamp(cosDeltaPhi, 0.0, 1.0);

    float sinAlpha, tanBeta;
    if (NdotL < NdotV)
    {
        sinAlpha = sinThetaI;
        tanBeta  = sinThetaR / (NdotV + 1e-7);
    }
    else
    {
        sinAlpha = sinThetaR;
        tanBeta  = sinThetaI / (NdotL + 1e-7);
    }

    return (A + B * cosDeltaPhi * sinAlpha * tanBeta) / PI;
}

/// Energy-conserving diffuse contribution. Returns zero for pure metals.
/// Caller multiplies by NdotL * lightColor * baseColor.
vec3 diffuseLobe(vec3 baseColor, vec3 F, float metalness,
                 float NdotL, float NdotV, float LdotV, float diffuseRoughness)
{
    vec3  kD = (1.0 - F) * (1.0 - metalness);
    float d  = orenNayarDiffuse(NdotL, NdotV, LdotV, diffuseRoughness);
    return kD * baseColor * d;
}

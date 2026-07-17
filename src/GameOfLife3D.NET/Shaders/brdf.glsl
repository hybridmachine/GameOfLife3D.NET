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

// ── Anisotropic GGX (Heitz parameterization) ─────────────────────────────────

/// Maps OpenPBR anisotropy in [-1, 1] to per-axis GGX roughness using Burley's
/// aspect parameterization. alphaT stretches along the tangent, alphaB along
/// the bitangent; a negative anisotropy swaps the axes.
void anisotropicRoughness(float roughness, float anisotropy,
                          out float alphaT, out float alphaB)
{
    float a = max(roughness * roughness, 1e-4);
    float aspect = sqrt(1.0 - 0.9 * clamp(abs(anisotropy), 0.0, 1.0));
    alphaT = a / aspect;
    alphaB = a * aspect;
    if (anisotropy < 0.0)
    {
        float t = alphaT;
        alphaT = alphaB;
        alphaB = t;
    }
}

/// Anisotropic GGX normal distribution function (Heitz 2014).
/// TdotH/BdotH are dot products with the shading tangent/bitangent.
float distributionGGXAnisotropic(float TdotH, float BdotH, float NdotH,
                                 float alphaT, float alphaB)
{
    float d = TdotH * TdotH / (alphaT * alphaT)
            + BdotH * BdotH / (alphaB * alphaB)
            + NdotH * NdotH;
    return 1.0 / (PI * alphaT * alphaB * d * d + 1e-7);
}

/// Height-correlated Smith visibility for the anisotropic GGX lobe (Heitz).
/// Like visibilitySmithGGX, the 1 / (4 NdotV NdotL) factor is folded in.
float visibilitySmithGGXAnisotropic(float TdotV, float BdotV, float NdotV,
                                    float TdotL, float BdotL, float NdotL,
                                    float alphaT, float alphaB)
{
    float lambdaV = NdotL * length(vec3(alphaT * TdotV, alphaB * BdotV, NdotV));
    float lambdaL = NdotV * length(vec3(alphaT * TdotL, alphaB * BdotL, NdotL));
    return 0.5 / (lambdaV + lambdaL + 1e-7);
}

/// Full anisotropic GGX Cook-Torrance specular BRDF for a single directional
/// light. Returns the specular radiance contribution (multiply by NdotL *
/// lightColor outside this function).
vec3 cookTorranceSpecularAnisotropic(vec3 N, vec3 V, vec3 L, vec3 H,
                                     vec3 T, vec3 B,
                                     float roughness, float anisotropy, vec3 F0)
{
    float NdotH = max(dot(N, H), 0.0);
    float NdotV = max(dot(N, V), 0.001);
    float NdotL = max(dot(N, L), 0.0);
    float VdotH = max(dot(V, H), 0.0);

    float alphaT, alphaB;
    anisotropicRoughness(roughness, anisotropy, alphaT, alphaB);

    float D = distributionGGXAnisotropic(dot(T, H), dot(B, H), NdotH, alphaT, alphaB);
    float Vis = visibilitySmithGGXAnisotropic(dot(T, V), dot(B, V), NdotV,
                                              dot(T, L), dot(B, L), NdotL,
                                              alphaT, alphaB);
    vec3  F = fresnelSchlick(VdotH, F0);
    return D * Vis * F;
}

// ── Fuzz (sheen) ─────────────────────────────────────────────────────────────

/// Charlie sheen normal distribution function (Estevez & Kulla 2017), used by
/// OpenPBR's fuzz lobe.
float distributionCharlie(float NdotH, float roughness)
{
    float alpha = max(roughness * roughness, 1e-4);
    float invAlpha = 1.0 / alpha;
    float sin2 = max(1.0 - NdotH * NdotH, 0.0);
    return (2.0 + invAlpha) * pow(sin2, invAlpha * 0.5) / (2.0 * PI);
}

/// Ashikhmin-Premoze visibility term paired with the Charlie NDF
/// (Estevez & Kulla 2017).
float visibilitySheen(float NdotV, float NdotL)
{
    return 1.0 / (4.0 * (NdotL + NdotV - NdotL * NdotV) + 1e-7);
}

// ── Thin-film iridescence ────────────────────────────────────────────────────

/// Converts per-channel F0 back to an effective IOR (inverse of iorToF0Scalar).
vec3 fresnel0ToIor(vec3 f0)
{
    vec3 sqrtF0 = sqrt(f0);
    return (vec3(1.0) + sqrtF0) / (vec3(1.0) - sqrtF0);
}

/// Per-channel F0 at an interface between incident IOR and per-channel
/// transmitted IORs (vector form of iorToF0Scalar).
vec3 iorToF0Vec3(vec3 transmittedIor, float incidentIor)
{
    vec3 t = (transmittedIor - vec3(incidentIor)) / (transmittedIor + vec3(incidentIor));
    return t * t;
}

/// CIE sensitivity functions for thin-film interference, approximated with
/// Gaussian fits (Belcour & Barla 2017, section 4.1; as used by Filament/UE).
/// opd = optical path difference in nm; shift = per-channel phase shift.
vec3 evalSensitivity(float opd, vec3 shift)
{
    float phase = 2.0 * PI * opd * 1.0e-6;
    vec3 val = vec3(5.4856e-13, 4.4201e-13, 5.2481e-13);
    vec3 pos = vec3(1.6810e+06, 1.7953e+06, 2.2084e+06);
    vec3 spread = vec3(4.3278e+09, 9.3046e+09, 6.6121e+09);
    vec3 xyz = val * sqrt(2.0 * PI * spread) * cos(pos * phase + shift) * exp(-spread * phase * phase);
    xyz.x += 9.7470e-14 * sqrt(2.0 * PI * 4.5282e+09)
           * cos(2.2399e+06 * phase + shift.x) * exp(-4.5282e+09 * phase * phase);
    return xyz;
}

/// Thin-film iridescent reflectance that modulates the base specular F0.
/// Belcour & Barla (2017) air/film/base interference model, ported from
/// Filament's brdf.fs. outsideIor is the surrounding medium (air = 1.0);
/// filmIor is the film IOR; cosTheta = dot(N, V); thickness in nanometers.
vec3 fresnelIridescent(float outsideIor, float filmIor, float cosTheta,
                       float thinFilmThickness, vec3 baseF0)
{
    // Force the film IOR toward the outside IOR as thickness → 0 so the
    // film vanishes smoothly instead of snapping off.
    float iridescenceIor = mix(outsideIor, filmIor, smoothstep(0.0, 0.03, thinFilmThickness));

    // Snell refraction into the film; total internal reflection = full reflectance.
    float ratio = outsideIor / iridescenceIor;
    float sinTheta2Sq = ratio * ratio * (1.0 - cosTheta * cosTheta);
    float cosTheta2Sq = 1.0 - sinTheta2Sq;
    if (cosTheta2Sq < 0.0)
        return vec3(1.0);
    float cosTheta2 = sqrt(cosTheta2Sq);

    // First interface (outside → film).
    float r0 = (iridescenceIor - outsideIor) / (iridescenceIor + outsideIor);
    float R0 = r0 * r0;
    float R12 = fresnelSchlickScalar(cosTheta, R0);
    float T121 = 1.0 - R12;
    float phi12 = iridescenceIor < outsideIor ? PI : 0.0;
    float phi21 = PI - phi12;

    // Second interface (film → base layer).
    vec3 baseIOR = fresnel0ToIor(clamp(baseF0, 0.0, 0.9999));
    vec3 R1 = iorToF0Vec3(baseIOR, iridescenceIor);
    vec3 R23 = fresnelSchlick(cosTheta2, R1);
    vec3 phi23 = vec3(baseIOR.x < iridescenceIor ? PI : 0.0,
                      baseIOR.y < iridescenceIor ? PI : 0.0,
                      baseIOR.z < iridescenceIor ? PI : 0.0);

    // Optical path difference and combined phase shift.
    float opd = 2.0 * iridescenceIor * thinFilmThickness * cosTheta2;
    vec3 phi = vec3(phi21) + phi23;

    // Compound terms.
    vec3 R123 = clamp(R12 * R23, vec3(1e-5), vec3(0.9999));
    vec3 r123 = sqrt(R123);
    vec3 Rs = (T121 * T121) * R23 / (vec3(1.0) - R123);

    // Reflectance term for m = 0 (DC amplitude).
    vec3 I = vec3(R12) + Rs;

    // Reflectance terms for m > 0 (pairs of Diracs).
    vec3 Cm = Rs - vec3(T121);
    for (int m = 1; m <= 2; ++m)
    {
        Cm *= r123;
        vec3 Sm = 2.0 * evalSensitivity(float(m) * opd, float(m) * phi);
        I += Cm * Sm;
    }

    // Out-of-gamut colors can be produced; negative values are clamped away.
    return max(I, vec3(0.0));
}

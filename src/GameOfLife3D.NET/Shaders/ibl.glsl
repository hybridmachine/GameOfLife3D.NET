// Image-based lighting helpers shared by pbr_cell.frag.
// Included via #include "ibl.glsl".
//
// Spherical harmonic L1 (9 coefficients) for diffuse irradiance.
// The CPU side uploads these once after init and again when the background
// changes. When background mode is Solid or Gradient a simple analytical
// hemisphere is used via the constant coefficient only (sh[0] = ambient).
// All 9 are declared so the shader API is forward-compatible with a full
// SH bake in a later revision.
//
// Specular ambient uses Brian Karis's analytic GGX split-sum approximation
// from "Real Shading in Unreal Engine 4" (2013) — no LUT texture required.

// SH coefficients set by the CPU. sh0 is the DC (average) term.
uniform vec3 uIblSh[9];

// User-controlled IBL intensity scale applied to both diffuse and specular.
uniform float uEnvIntensity;

// ── Diffuse irradiance from SH ────────────────────────────────────────────────

/// Evaluates the SH L1 (order-2) irradiance polynomial for direction N.
/// Uses the Ramamoorthi & Hanrahan basis precomputed with PI factors.
vec3 evalIrradianceSH(vec3 N)
{
    // Band 0 (L0)
    vec3 irradiance = uIblSh[0];

    // Band 1 (L1) — linear terms
    irradiance += uIblSh[1] * N.y;
    irradiance += uIblSh[2] * N.z;
    irradiance += uIblSh[3] * N.x;

    // Band 2 (L2) — quadratic terms
    irradiance += uIblSh[4] * (N.x * N.y);
    irradiance += uIblSh[5] * (N.y * N.z);
    irradiance += uIblSh[6] * (3.0 * N.z * N.z - 1.0);
    irradiance += uIblSh[7] * (N.x * N.z);
    irradiance += uIblSh[8] * (N.x * N.x - N.y * N.y);

    return max(irradiance, vec3(0.0));
}

// ── Specular ambient (analytic GGX split-sum approximation) ──────────────────

/// Analytic fit for the environment BRDF integral (Karis 2013).
/// NdotV = saturated dot(N, V); roughness = linear roughness.
/// Returns vec2(scale, bias) such that specularIBL ≈ F0 * scale + bias.
vec2 envBrdfApprox(float NdotV, float roughness)
{
    // Karis's polynomial fit to the split-sum integral LUT.
    const vec4 c0 = vec4(-1.0, -0.0275, -0.572,  0.022);
    const vec4 c1 = vec4( 1.0,  0.0425,  1.040, -0.040);
    vec4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NdotV)) * r.x + r.y;
    return vec2(-1.04, 1.04) * a004 + r.zw;
}

/// Returns the specular ambient radiance for a surface.
/// F0 = dielectric or metallic base reflectance; NdotV = dot(N, V); roughness = linear.
vec3 specularAmbient(vec3 F0, float NdotV, float roughness)
{
    vec2 brdf = envBrdfApprox(NdotV, roughness);
    // The reflected radiance from the environment in the view-reflection direction
    // is approximated by scaling the SH DC term (average ambient) by 2. This
    // assumes the glossy lobe energy is roughly twice the hemispherical average
    // for an isotropic environment — a common approximation for hero reflections
    // in PBR renderers where no mip-convolved cubemap is available.
    vec3 envColor = uIblSh[0] * 2.0;
    return envColor * (F0 * brdf.x + brdf.y);
}

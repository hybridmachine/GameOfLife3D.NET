#version 330 core

// PBR fragment shader for cell surfaces.
// Uses the same vertex outputs as cube.vert (no VAO changes required).
// Active when RenderSettings.ActiveMaterial is non-null; the legacy cube.frag
// shader is used otherwise so existing sessions are pixel-identical.

in vec3 vWorldPosition;
in vec3 vNormal;
in float vGenerationT;
in float vViewDistance;

// ── Shared uniforms (same names as cube.frag) ─────────────────────────────────
uniform bool uColorCycling;
uniform vec3 uSolidColor;
uniform float uTime;
uniform float uMinY;
uniform float uMaxY;
uniform vec3 uLightDir;

// Fog
uniform bool uFogEnabled;
uniform float uFogStart;
uniform float uFogEnd;
uniform vec3 uFogColor;

// Clip plane
uniform bool uClipEnabled;
uniform float uClipY;

// Generation fade-in
uniform float uFadeGeneration;
uniform float uFadeOpacity;

// Global alpha
uniform float uGlobalAlpha;

// ── PBR material uniforms ─────────────────────────────────────────────────────
uniform vec3  uBaseColor;            // base_color tint (multiplied with gradient)
uniform float uBaseMetalness;        // 0 = dielectric, 1 = metallic
uniform float uBaseDiffuseRoughness; // Oren-Nayar sigma (0 = Lambertian)

uniform float uSpecularRoughness;    // GGX roughness
uniform float uSpecularIor;          // IOR → F0

uniform vec3  uEmissionColor;        // emission tint
uniform float uEmissionLuminance;    // emission scale (0 = no emission)

uniform float uCoatWeight;           // clearcoat blend weight
uniform float uCoatRoughness;        // clearcoat GGX roughness
uniform float uCoatIor;              // clearcoat IOR

// ── Lighting ──────────────────────────────────────────────────────────────────
uniform vec3 uCameraPos;             // world-space camera position

out vec4 FragColor;

#include "gradient.glsl"
#include "brdf.glsl"
#include "ibl.glsl"

void main()
{
    // Clip plane
    if (uClipEnabled && vWorldPosition.y > uClipY)
        discard;

    bool isPreview = vGenerationT < 0.0;

    // ── Base color ─────────────────────────────────────────────────────────────
    // A PBR material's base_color and lighting alone define the cell — color
    // cycling is ignored. Preview cells keep their teal tint for legibility.
    vec3 baseColor = isPreview ? vec3(0.0, 1.0, 0.7) : uBaseColor;

    // ── PBR vectors ───────────────────────────────────────────────────────────
    vec3 N = normalize(vNormal);
    vec3 V = normalize(uCameraPos - vWorldPosition);
    vec3 L = normalize(uLightDir);
    vec3 H = normalize(V + L);

    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.001);
    float NdotH = max(dot(N, H), 0.0);
    float VdotH = max(dot(V, H), 0.0);
    float LdotV = dot(L, V);

    // ── Specular F0 ───────────────────────────────────────────────────────────
    float f0Scalar = iorToF0Scalar(uSpecularIor);
    // Metallic: base color becomes the reflectance; dielectric: use IOR-derived F0.
    vec3 F0 = mix(vec3(f0Scalar), baseColor, uBaseMetalness);

    // ── Direct lighting (single directional key light) ────────────────────────
    vec3 directSpecular = cookTorranceSpecular(NdotH, NdotV, NdotL, VdotH,
                                               uSpecularRoughness, F0);
    vec3 F = fresnelSchlick(VdotH, F0);

    vec3 directDiffuse = diffuseLobe(baseColor, F, uBaseMetalness,
                                     NdotL, NdotV, LdotV,
                                     uBaseDiffuseRoughness);

    // Light is white with implicit unit intensity.
    vec3 directLight = (directDiffuse + directSpecular) * NdotL;

    // ── Coat lobe (clearcoat) ─────────────────────────────────────────────────
    if (uCoatWeight > 0.001)
    {
        float coatF0Scalar = iorToF0Scalar(uCoatIor);
        vec3  coatF0       = vec3(coatF0Scalar);

        vec3  coatSpec = cookTorranceSpecular(NdotH, NdotV, NdotL, VdotH,
                                              uCoatRoughness, coatF0);
        float coatFresnel = fresnelSchlickScalar(VdotH, coatF0Scalar);

        // Attenuate the base by (1 - coatFresnel * coatWeight) and add coat specular.
        directLight *= (1.0 - uCoatWeight * coatFresnel);
        directLight += coatSpec * NdotL * uCoatWeight;
    }

    // ── Indirect / ambient (IBL) ──────────────────────────────────────────────
    vec3 ambientDiffuse = evalIrradianceSH(N) * baseColor * (1.0 - uBaseMetalness) * uEnvIntensity;
    vec3 iblSpecular = specularAmbient(F0, NdotV, uSpecularRoughness) * uEnvIntensity;
    vec3 ambient     = ambientDiffuse + iblSpecular;

    // ── Emission ──────────────────────────────────────────────────────────────
    vec3 emission = uEmissionColor * uEmissionLuminance;

    // ── Compose ───────────────────────────────────────────────────────────────
    vec3 lit = directLight + ambient + emission;

    float alpha = isPreview ? 0.3 : 1.0;

    // Generation fade-in
    if (uFadeGeneration >= 0.0 && abs(vGenerationT - uFadeGeneration) < 0.5)
        alpha *= uFadeOpacity;

    // Fog
    if (uFogEnabled)
    {
        float fogFactor = clamp((vViewDistance - uFogStart) / (uFogEnd - uFogStart), 0.0, 1.0);
        lit = mix(lit, uFogColor, fogFactor);
        if (isPreview) alpha = mix(alpha, 0.0, fogFactor);
    }

    FragColor = vec4(lit, alpha * uGlobalAlpha);
}

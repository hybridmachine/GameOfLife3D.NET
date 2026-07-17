#version 330 core

// PBR fragment shader for cell surfaces.
// Uses the same vertex outputs as cube.vert (no VAO changes required).
// Active when RenderSettings.ActiveMaterial is non-null; the legacy cube.frag
// shader is used otherwise so existing sessions are pixel-identical.

in vec3 vWorldPosition;
in vec3 vNormal;
in float vGenerationT;
in float vViewDistance;
in vec3 vLocalPosition;   // object-space position for triplanar projection

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
uniform float uBaseWeight;           // base layer blend weight

uniform float uSpecularWeight;       // specular lobe blend weight
uniform vec3  uSpecularColor;        // dielectric specular tint
uniform float uSpecularRoughness;    // GGX roughness
uniform float uSpecularAnisotropy;   // roughness anisotropy (0 = isotropic)
uniform float uSpecularIor;          // IOR → F0

uniform vec3  uEmissionColor;        // emission tint
uniform float uEmissionLuminance;    // emission scale (0 = no emission)

uniform float uCoatWeight;           // clearcoat blend weight
uniform vec3  uCoatColor;            // clearcoat tint
uniform float uCoatRoughness;        // clearcoat GGX roughness
uniform float uCoatAnisotropy;       // clearcoat roughness anisotropy
uniform float uCoatIor;              // clearcoat IOR
uniform float uCoatDarkening;        // base darkening under coat at grazing

uniform float uFuzzWeight;           // fuzz (sheen) blend weight
uniform vec3  uFuzzColor;            // fuzz tint
uniform float uFuzzRoughness;        // fuzz roughness (Charlie lobe)

uniform float uThinFilmWeight;       // thin-film interference weight
uniform float uThinFilmThickness;    // film thickness in nanometers
uniform float uThinFilmIor;          // film IOR

uniform float uGeometryOpacity;      // opacity multiplier (1 = opaque)
uniform float uTextureScale;         // triplanar tiling (1 = one repeat per cell)

// ── Material textures (fixed units 4–9; 0–1 are background/composite) ────────
// Texture semantics are constant × textureSample — the constant acts as a
// tint/scale, and the importer promotes non-identity defaults (scalars to 1,
// emission_color to white) when a texture is connected.
uniform sampler2D uTexBaseColor;     // unit 4
uniform sampler2D uTexMetalness;     // unit 5
uniform sampler2D uTexRoughness;     // unit 6
uniform sampler2D uTexNormal;        // unit 7
uniform sampler2D uTexEmission;      // unit 8
uniform sampler2D uTexOpacity;       // unit 9
uniform bool uHasTexBaseColor;
uniform bool uHasTexMetalness;
uniform bool uHasTexRoughness;
uniform bool uHasTexNormal;
uniform bool uHasTexEmission;
uniform bool uHasTexOpacity;

// ── Lighting ──────────────────────────────────────────────────────────────────
uniform vec3 uCameraPos;             // world-space camera position

out vec4 FragColor;

#include "gradient.glsl"
#include "brdf.glsl"
#include "triplanar.glsl"
#include "ibl.glsl"

void main()
{
    // Clip plane
    if (uClipEnabled && vWorldPosition.y > uClipY)
        discard;

    bool isPreview = vGenerationT < 0.0;

    vec3 N = normalize(vNormal);

    // ── Textures (triplanar projection) ──────────────────────────────────────
    // All texture branches are gated on has-texture flags so the default
    // (untextured) material costs the same as before.
    bool anyTex = uHasTexBaseColor || uHasTexMetalness || uHasTexRoughness
                || uHasTexNormal || uHasTexEmission || uHasTexOpacity;
    vec3 triWeights = anyTex ? triplanarWeights(N) : vec3(0.0);

    // Perturb the shading normal before any lighting is evaluated.
    if (uHasTexNormal)
        N = triplanarNormal(uTexNormal, vLocalPosition, uTextureScale, N);

    // ── Base color ─────────────────────────────────────────────────────────────
    // A PBR material's base_color and lighting alone define the cell — color
    // cycling is ignored. Preview cells keep their teal tint for legibility.
    vec3 baseColor = uBaseColor;
    if (uHasTexBaseColor && !isPreview)
        baseColor *= triplanarSample(uTexBaseColor, vLocalPosition, uTextureScale, triWeights).rgb;
    if (isPreview)
        baseColor = vec3(0.0, 1.0, 0.7);

    float metalness = uBaseMetalness;
    if (uHasTexMetalness)
        metalness *= triplanarSample(uTexMetalness, vLocalPosition, uTextureScale, triWeights).r;

    float specularRoughness = uSpecularRoughness;
    if (uHasTexRoughness)
        specularRoughness *= triplanarSample(uTexRoughness, vLocalPosition, uTextureScale, triWeights).r;
    // A black roughness sample would reintroduce the GGX singularity the
    // CPU-side MinRoughness clamp guards against, so clamp again here.
    specularRoughness = max(specularRoughness, 0.02);

    float opacitySample = 1.0;
    if (uHasTexOpacity)
        opacitySample = triplanarSample(uTexOpacity, vLocalPosition, uTextureScale, triWeights).r;

    // ── PBR vectors ───────────────────────────────────────────────────────────
    vec3 V = normalize(uCameraPos - vWorldPosition);
    vec3 L = normalize(uLightDir);
    vec3 H = normalize(V + L);

    float NdotL = max(dot(N, L), 0.0);
    float NdotV = max(dot(N, V), 0.001);
    float NdotH = max(dot(N, H), 0.0);
    float VdotH = max(dot(V, H), 0.0);
    float LdotV = dot(L, V);

    // ── Anisotropy tangent frame ──────────────────────────────────────────────
    // Object-space X axis orthogonalized against N (documented simplification
    // — no per-shape authored tangents). The Y seed fallback avoids a
    // degenerate frame when N is parallel to X. Computed only when either
    // anisotropy weight is non-zero.
    vec3 T = vec3(1.0, 0.0, 0.0);
    vec3 B = vec3(0.0, 1.0, 0.0);
    if (abs(uSpecularAnisotropy) > 0.001 || abs(uCoatAnisotropy) > 0.001)
    {
        vec3 tangentSeed = abs(N.x) > 0.999 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
        T = normalize(tangentSeed - N * dot(tangentSeed, N));
        B = cross(N, T);
    }

    // ── Specular F0 ───────────────────────────────────────────────────────────
    float f0Scalar = iorToF0Scalar(uSpecularIor);
    // specular_color tints the dielectric F0; metallic keeps baseColor as F0.
    vec3 F0 = mix(vec3(f0Scalar) * uSpecularColor, baseColor, metalness);

    // Thin-film iridescence modulates F0 before any specular evaluation.
    if (uThinFilmWeight > 0.001 && uThinFilmThickness > 0.0)
    {
        vec3 iridescentF0 = fresnelIridescent(1.0, uThinFilmIor, NdotV,
                                              uThinFilmThickness, F0);
        F0 = mix(F0, iridescentF0, uThinFilmWeight);
    }

    // ── Direct lighting (single directional key light) ────────────────────────
    vec3 directSpecular;
    if (abs(uSpecularAnisotropy) > 0.001)
        directSpecular = cookTorranceSpecularAnisotropic(N, V, L, H, T, B,
            specularRoughness, uSpecularAnisotropy, F0);
    else
        directSpecular = cookTorranceSpecular(NdotH, NdotV, NdotL, VdotH,
            specularRoughness, F0);
    directSpecular *= uSpecularWeight;

    vec3 F = fresnelSchlick(VdotH, F0);

    vec3 directDiffuse = diffuseLobe(baseColor, F, metalness,
                                     NdotL, NdotV, LdotV,
                                     uBaseDiffuseRoughness);

    // Light is white with implicit unit intensity. base_weight scales only
    // the base (diffuse) lobe; the specular lobe carries its own weight.
    vec3 directLight = (directDiffuse * uBaseWeight + directSpecular) * NdotL;

    // ── Fuzz (sheen) lobe ─────────────────────────────────────────────────────
    // Charlie distribution + Ashikhmin visibility. Energy compensation uses a
    // cheap grazing-angle attenuation of the base instead of a
    // directional-albedo LUT.
    if (uFuzzWeight > 0.001)
    {
        float sheenD = distributionCharlie(NdotH, uFuzzRoughness);
        float sheenV = visibilitySheen(NdotV, NdotL);
        float grazing = pow(clamp(1.0 - NdotV, 0.0, 1.0), 5.0);
        directLight *= 1.0 - uFuzzWeight * grazing;
        directLight += uFuzzColor * (sheenD * sheenV) * NdotL * uFuzzWeight;
    }

    // ── Coat lobe (clearcoat) ─────────────────────────────────────────────────
    if (uCoatWeight > 0.001)
    {
        float coatF0Scalar = iorToF0Scalar(uCoatIor);
        vec3  coatF0       = vec3(coatF0Scalar) * uCoatColor;

        vec3 coatSpec;
        if (abs(uCoatAnisotropy) > 0.001)
            coatSpec = cookTorranceSpecularAnisotropic(N, V, L, H, T, B,
                uCoatRoughness, uCoatAnisotropy, coatF0);
        else
            coatSpec = cookTorranceSpecular(NdotH, NdotV, NdotL, VdotH,
                uCoatRoughness, coatF0);
        float coatFresnel = fresnelSchlickScalar(VdotH, coatF0Scalar);

        // Attenuate the base by the coat's Fresnel transmittance and add coat
        // specular. coat_darkening scales that attenuation (OpenPBR):
        // 0 = no darkening, 1 = full physical darkening.
        directLight *= mix(1.0, 1.0 - uCoatWeight * coatFresnel, uCoatDarkening);
        directLight += coatSpec * NdotL * uCoatWeight;
    }

    // ── Indirect / ambient (IBL) ──────────────────────────────────────────────
    vec3 ambientDiffuse = evalIrradianceSH(N) * baseColor * (1.0 - metalness)
                        * uEnvIntensity * uBaseWeight;
    vec3 iblSpecular = specularAmbient(F0, NdotV, specularRoughness)
                     * uEnvIntensity * uSpecularWeight;
    vec3 ambient     = ambientDiffuse + iblSpecular;

    // ── Emission ──────────────────────────────────────────────────────────────
    vec3 emission = uEmissionColor * uEmissionLuminance;
    if (uHasTexEmission)
        emission *= triplanarSample(uTexEmission, vLocalPosition, uTextureScale, triWeights).rgb;

    // ── Compose ───────────────────────────────────────────────────────────────
    vec3 lit = directLight + ambient + emission;

    float alpha = isPreview ? 0.3 : 1.0;
    alpha *= uGeometryOpacity * opacitySample;

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

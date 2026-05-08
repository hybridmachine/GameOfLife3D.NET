#version 330 core

in vec3 vWorldPos;
in vec4 vClipPos;

uniform sampler2D uReflectionTex;
uniform vec3 uCameraPos;
uniform vec3 uWaterTint;
uniform float uReflectivity;
uniform float uReflectionStrength;
uniform float uTime;
uniform float uWaveStrength;
uniform float uWaveSpeed;
uniform float uFloorRadius;
uniform float uFadeStart;

uniform bool uFogEnabled;
uniform float uFogStart;
uniform float uFogEnd;
uniform vec3 uFogColor;

out vec4 FragColor;

#include "water_normal.glsl"

void main()
{
    vec3 N = computeWaterNormal(vWorldPos.xz, uTime * uWaveSpeed, uWaveStrength);
    vec3 V = normalize(uCameraPos - vWorldPos);
    float ndotv = clamp(dot(N, V), 0.0, 1.0);

    // Schlick Fresnel: glancing angles reflect more than top-down views.
    float fresnel = uReflectivity + (1.0 - uReflectivity) * pow(1.0 - ndotv, 5.0);
    fresnel *= uReflectionStrength;

    // Project clip-space xy into [0,1] UVs for the reflection sampler, then
    // perturb by the wave normal so ripples distort the mirrored image.
    vec2 reflUV = (vClipPos.xy / vClipPos.w) * 0.5 + 0.5;
    reflUV += N.xz * 0.04 * uReflectionStrength;
    reflUV = clamp(reflUV, 0.001, 0.999);
    vec3 reflColor = texture(uReflectionTex, reflUV).rgb;

    vec3 base = mix(uWaterTint, reflColor, fresnel);

    // Tiny specular highlight from a fixed light direction so calm surfaces
    // still pick up some sheen.
    vec3 L = normalize(vec3(0.4, 0.8, 0.3));
    vec3 H = normalize(L + V);
    float spec = pow(max(dot(N, H), 0.0), 64.0) * 0.25;
    base += vec3(spec);

    // Circular edge fade so the floor disc doesn't end on a hard line.
    float dist = length(vWorldPos.xz) / max(uFloorRadius, 0.0001);
    float edge = 1.0 - smoothstep(uFadeStart, 1.0, dist);

    if (uFogEnabled)
    {
        float vd = length(uCameraPos - vWorldPos);
        float fog = clamp((vd - uFogStart) / (uFogEnd - uFogStart), 0.0, 1.0);
        base = mix(base, uFogColor, fog);
        edge *= 1.0 - fog * 0.5;
    }

    FragColor = vec4(base, edge);
}

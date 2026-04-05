#version 330 core

in vec3 vWorldPosition;
in vec3 vNormal;
in float vGenerationT;
in float vViewDistance;

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

out vec4 FragColor;

#include "gradient.glsl"

void main()
{
    // Clip plane
    if (uClipEnabled && vWorldPosition.y > uClipY)
        discard;

    // Preview cells (GenerationT < 0)
    bool isPreview = vGenerationT < 0.0;

    vec3 baseColor;
    if (isPreview)
    {
        baseColor = vec3(0.0, 1.0, 0.7);
    }
    else if (uColorCycling)
    {
        baseColor = computeGradientColor(vWorldPosition.y, uMinY, uMaxY, uTime);
    }
    else
    {
        baseColor = uSolidColor;
    }

    // Simple directional lighting
    vec3 normal = normalize(vNormal);
    float ambient = 0.4;
    float diffuse = max(dot(normal, normalize(uLightDir)), 0.0) * 0.6;
    vec3 lit = baseColor * (ambient + diffuse);

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

    FragColor = vec4(lit, alpha);
}

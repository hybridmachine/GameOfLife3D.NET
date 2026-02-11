#version 330 core

in vec3 vWorldPosition;
in vec3 vNormal;
in float vGenerationT;

uniform bool uColorCycling;
uniform vec3 uSolidColor;
uniform float uTime;
uniform float uMinY;
uniform float uMaxY;
uniform vec3 uLightDir;

out vec4 FragColor;

#include "gradient.glsl"

void main()
{
    vec3 baseColor;
    if (uColorCycling)
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

    FragColor = vec4(lit, 1.0);
}

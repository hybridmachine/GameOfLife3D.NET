#version 330 core

in vec3 vWorldPosition;
in float vGenerationT;

uniform bool uColorCycling;
uniform vec3 uEdgeColor;
uniform float uTime;
uniform float uMinY;
uniform float uMaxY;
uniform float uHueAngle;

out vec4 FragColor;

#include "gradient.glsl"

vec3 rgb2hsl(vec3 c)
{
    float maxC = max(max(c.r, c.g), c.b);
    float minC = min(min(c.r, c.g), c.b);
    float l = (maxC + minC) / 2.0;

    if (maxC == minC) return vec3(0.0, 0.0, l);

    float d = maxC - minC;
    float s = l > 0.5 ? d / (2.0 - maxC - minC) : d / (maxC + minC);
    float h;
    if (maxC == c.r) h = (c.g - c.b) / d + (c.g < c.b ? 6.0 : 0.0);
    else if (maxC == c.g) h = (c.b - c.r) / d + 2.0;
    else h = (c.r - c.g) / d + 4.0;
    h /= 6.0;
    return vec3(h, s, l);
}

float hue2rgb(float p, float q, float t)
{
    if (t < 0.0) t += 1.0;
    if (t > 1.0) t -= 1.0;
    if (t < 1.0/6.0) return p + (q - p) * 6.0 * t;
    if (t < 1.0/2.0) return q;
    if (t < 2.0/3.0) return p + (q - p) * (2.0/3.0 - t) * 6.0;
    return p;
}

vec3 hsl2rgb(vec3 hsl)
{
    float h = hsl.x, s = hsl.y, l = hsl.z;
    if (s == 0.0) return vec3(l);
    float q = l < 0.5 ? l * (1.0 + s) : l + s - l * s;
    float p = 2.0 * l - q;
    return vec3(hue2rgb(p, q, h + 1.0/3.0), hue2rgb(p, q, h), hue2rgb(p, q, h - 1.0/3.0));
}

void main()
{
    vec3 color;
    if (uColorCycling)
    {
        vec3 faceColor = computeGradientColor(vWorldPosition.y, uMinY, uMaxY, uTime);
        vec3 hsl = rgb2hsl(faceColor);
        hsl.x = mod(hsl.x + uHueAngle / 360.0, 1.0);
        if (hsl.z < 0.1) { hsl.z = 0.5; hsl.y = 1.0; }
        color = hsl2rgb(hsl);
    }
    else
    {
        color = uEdgeColor;
    }
    FragColor = vec4(color, 0.8);
}

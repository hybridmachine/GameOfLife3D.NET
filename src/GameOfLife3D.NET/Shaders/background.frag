#version 330 core

in vec2 vTexCoord;

uniform vec3 uTopColor;
uniform vec3 uBottomColor;
uniform bool uStarfield;
uniform sampler2D uSkyTexture;
uniform mat4 uInvViewProj;

out vec4 FragColor;

const float PI = 3.14159265359;
const float TAU = 6.28318530718;

vec3 worldRay(vec2 uv)
{
    vec2 ndc = uv * 2.0 - 1.0;
    vec4 nearPoint = uInvViewProj * vec4(ndc, 0.0, 1.0);
    vec4 farPoint = uInvViewProj * vec4(ndc, 1.0, 1.0);
    nearPoint.xyz /= max(nearPoint.w, 1e-5);
    farPoint.xyz /= max(farPoint.w, 1e-5);
    return normalize(farPoint.xyz - nearPoint.xyz);
}

vec2 directionToSky(vec3 dir)
{
    float lon = atan(dir.z, dir.x);
    float lat = asin(clamp(dir.y, -1.0, 1.0));
    return vec2(lon / TAU + 0.5, lat / PI + 0.5);
}

void main()
{
    vec3 gradientColor = mix(uBottomColor, uTopColor, vTexCoord.y);
    if (!uStarfield)
    {
        FragColor = vec4(gradientColor, 1.0);
        return;
    }

    vec3 rayDir = worldRay(vTexCoord);
    vec2 skyUv = directionToSky(rayDir);
    skyUv.y = 1.0 - skyUv.y;
    vec3 skyColor = texture(uSkyTexture, skyUv).rgb;
    FragColor = vec4(skyColor, 1.0);
}

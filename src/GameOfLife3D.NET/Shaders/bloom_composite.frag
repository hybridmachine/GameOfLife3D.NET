#version 330 core

in vec2 vTexCoord;

uniform sampler2D uSceneTexture;
uniform sampler2D uBloomTexture;
uniform float uBloomIntensity;

out vec4 FragColor;

void main()
{
    vec3 scene = texture(uSceneTexture, vTexCoord).rgb;
    vec3 bloom = texture(uBloomTexture, vTexCoord).rgb;
    FragColor = vec4(scene + bloom * uBloomIntensity, 1.0);
}

#version 330 core

in vec2 vTexCoord;

uniform sampler2D uSceneTexture;
uniform sampler2D uBloomTexture;
uniform bool uBloomEnabled;
uniform float uBloomIntensity;

out vec4 FragColor;

void main()
{
    vec3 scene = texture(uSceneTexture, vTexCoord).rgb;

    if (uBloomEnabled)
    {
        vec3 bloom = texture(uBloomTexture, vTexCoord).rgb;
        scene += bloom * uBloomIntensity;
    }

    FragColor = vec4(scene, 1.0);
}

#version 330 core

in vec2 vTexCoord;

uniform sampler2D uImage;
uniform bool uHorizontal;

out vec4 FragColor;

// 9-tap Gaussian weights
const float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);

void main()
{
    vec2 texelSize = 1.0 / textureSize(uImage, 0);
    vec3 result = texture(uImage, vTexCoord).rgb * weights[0];

    if (uHorizontal)
    {
        for (int i = 1; i < 5; i++)
        {
            result += texture(uImage, vTexCoord + vec2(texelSize.x * float(i), 0.0)).rgb * weights[i];
            result += texture(uImage, vTexCoord - vec2(texelSize.x * float(i), 0.0)).rgb * weights[i];
        }
    }
    else
    {
        for (int i = 1; i < 5; i++)
        {
            result += texture(uImage, vTexCoord + vec2(0.0, texelSize.y * float(i))).rgb * weights[i];
            result += texture(uImage, vTexCoord - vec2(0.0, texelSize.y * float(i))).rgb * weights[i];
        }
    }

    FragColor = vec4(result, 1.0);
}

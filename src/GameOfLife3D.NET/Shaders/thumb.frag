#version 330 core

in vec3 vNormal;

uniform vec3 uColor;
uniform vec3 uLightDir;

out vec4 FragColor;

void main()
{
    float ambient = 0.45;
    float diffuse = max(dot(normalize(vNormal), normalize(uLightDir)), 0.0) * 0.55;
    FragColor = vec4(uColor * (ambient + diffuse), 1.0);
}

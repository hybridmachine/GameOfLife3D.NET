#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec3 aInstancePosition;
layout(location = 3) in float aGenerationT;

uniform mat4 uView;
uniform mat4 uProjection;
uniform float uCellSize;

out vec3 vWorldPosition;
out vec3 vNormal;
out float vGenerationT;

void main()
{
    vec3 worldPos = aPosition * uCellSize + aInstancePosition;
    vWorldPosition = worldPos;
    vNormal = aNormal;
    vGenerationT = aGenerationT;
    gl_Position = uProjection * uView * vec4(worldPos, 1.0);
}

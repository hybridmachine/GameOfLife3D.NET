#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec3 aInstancePosition;
layout(location = 3) in float aGenerationT;

uniform mat4 uView;
uniform mat4 uProjection;
uniform float uCellSize;

out vec3 vWorldPosition;
out float vGenerationT;
out float vViewDistance;

void main()
{
    vec3 worldPos = aPosition * uCellSize + aInstancePosition;
    vWorldPosition = worldPos;
    vGenerationT = aGenerationT;

    vec4 viewPos = uView * vec4(worldPos, 1.0);
    vViewDistance = length(viewPos.xyz);
    gl_Position = uProjection * viewPos;
}

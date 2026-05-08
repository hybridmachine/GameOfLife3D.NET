#version 330 core

layout(location = 0) in vec2 aXZ;

uniform mat4 uView;
uniform mat4 uProjection;
uniform float uFloorSize;
uniform float uFloorY;

out vec3 vWorldPos;
out vec4 vClipPos;

void main()
{
    vec3 worldPos = vec3(aXZ.x * uFloorSize, uFloorY, aXZ.y * uFloorSize);
    vWorldPos = worldPos;
    vClipPos = uProjection * uView * vec4(worldPos, 1.0);
    gl_Position = vClipPos;
}

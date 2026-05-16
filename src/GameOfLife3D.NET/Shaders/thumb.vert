#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;

uniform mat4 uMvp;

out vec3 vNormal;

void main()
{
    // Mesh has no model transform and the light direction is supplied in
    // object space, so the normal can be passed through unchanged.
    vNormal = aNormal;
    gl_Position = uMvp * vec4(aPosition, 1.0);
}

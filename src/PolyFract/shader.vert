#version 330 core

layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec3 aColor;

uniform mat4 projection;

out vec3 vColor;

void main()
{
    vColor = aColor;
    if (aColor.r >= 255) {
        gl_PointSize = 15.0;
    } else {
        gl_PointSize = 7.0;
    }

    gl_Position = projection * vec4(aPosition, 0.0, 1.0);
}

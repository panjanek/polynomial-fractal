#version 430

struct CompactComplexFloatWithColor {
    float r;
    float i;
    float colorR;
    float colorG;
    float colorB;
};

layout(std430, binding = 1) buffer OutputBuffer {
    CompactComplexFloatWithColor points[];
};

uniform mat4 projection;

layout(location=0) out vec3 vColor;

void main()
{
    uint id = gl_VertexID;

    float x = points[id].r;
    float y = points[id].i;

    gl_Position = projection * vec4(x, y, 0.0, 1.0);
    gl_PointSize = 15.0;

    if (points[id].colorR >= 255) {
        gl_PointSize = 15.0;
    } else {
        gl_PointSize = 7.0;
    }

    vColor = vec3(points[id].colorR, points[id].colorG, points[id].colorB);
}
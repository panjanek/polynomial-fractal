#version 430

struct CompactComplexFloatWithColor {
    vec2 position;
    vec4 color;
};

layout(std430, binding = 1) buffer OutputBuffer {
    CompactComplexFloatWithColor points[];
};

uniform mat4 projection;

layout(location=0) out vec3 vColor;

void main()
{
    uint id = gl_VertexID;

    float x = points[id].position.x;
    float y = points[id].position.y;

    gl_Position = projection * vec4(x, y, 0.0, 1.0);
    gl_PointSize = 15.0;

    if (points[id].color.r >= 255) {
        gl_PointSize = 15.0;
    } else {
        gl_PointSize = 7.0;
    }

    vColor = points[id].color.rgb; // vec3(points[id].colorR, points[id].colorG, points[id].colorB);
}
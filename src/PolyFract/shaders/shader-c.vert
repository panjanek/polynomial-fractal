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
    gl_Position = projection * vec4(points[id].position, 0.0, 1.0);
    gl_PointSize = 7.0;
    if (points[id].color.r >= 255) {
        gl_PointSize = 15.0;
    }

    vColor = points[id].color.rgb; 
}
#version 430

layout(location=0) in vec3 vColor;
out vec4 outputColor;

void main()
{
    vec2 uv = gl_PointCoord * 2.0 - 1.0; 
    float r = length(uv); 

    if (vColor.r >= 255) {
        if (r > 1.0)
            discard;
        if (r < 0.5) 
            outputColor = vec4(1.0, 0.0, 0.0, 1.0);
        else
            outputColor = vec4(vColor, 1);
    }
    else {
        if (r > 1.0)
            discard;

        //this is good with GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        float alpha = smoothstep(1.0, 0.0, r);
        alpha = alpha*alpha;
        outputColor = vec4(vColor*alpha, alpha);
    }

}
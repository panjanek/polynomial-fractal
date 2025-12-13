#version 330 core

in vec3 vColor;
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

        //use with GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        /*
        float inputAlpha = smoothstep(1.0, 0.0, r);
        inputAlpha = inputAlpha;
        vec3 linear = pow(vColor.rgb, vec3(2.2));  // to linear
        float a = inputAlpha * 0.2;
        vec3 premul = linear * a;
        outputColor = vec4(pow(premul, vec3(1.0/2.2)), a); // back to sRGB
        */

        //this is good with GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
        float alpha = smoothstep(1.0, 0.0, r);
        alpha = alpha*alpha;
        outputColor = vec4(vColor*alpha, alpha);

        //this makes sense with GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        //float alpha = 1.0 - smoothstep(0.0, 1.0, r);  
        //outputColor = vec4(vColor*alpha, alpha);
    }

}
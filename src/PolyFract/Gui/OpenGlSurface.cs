using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms.Integration;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using PolyFract.Maths;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Panel = System.Windows.Controls.Panel;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace PolyFract.Gui
{
    public class OpenGlSurface : ISurface
    {
        public Panel MouseEventSource => this.mouseProxy;

        public int FrameCounter => frameCounter;

        public string Name => "opengl";

        private readonly Panel placeholder;

        private readonly WindowsFormsHost host;

        private readonly GLControl glControl;

        private readonly WinFormsMouseProxy mouseProxy;

        private int frameCounter = 0;

        private Complex origin = Complex.Zero;

        private double zoom = MainWindow.DefaultZoom;

        private Solver solver;

        private Complex[] coefficients;

        private int shaderProgram;

        private PointVertex[] points;

        private int projLocation;

        private int vao;

        private int vbo;

        private Matrix4 projectionMatrix;

        public OpenGlSurface(Panel placeholder)
        {
            this.placeholder = placeholder;
            host = new WindowsFormsHost();
            host.Visibility = Visibility.Visible;
            host.HorizontalAlignment = HorizontalAlignment.Stretch;
            host.VerticalAlignment = VerticalAlignment.Stretch;
            glControl = new GLControl(new GLControlSettings
            {
                API = OpenTK.Windowing.Common.ContextAPI.OpenGL,
                APIVersion = new Version(3, 3), // OpenGL 3.3
                Profile = ContextProfile.Compatability,
                Flags = ContextFlags.Default,
                IsEventDriven = false
            });
            glControl.Dock = DockStyle.Fill;
            host.Child = glControl;
            placeholder.Children.Add(host);
            glControl.Paint += GlControl_Paint;
            mouseProxy = new WinFormsMouseProxy(glControl);
        }

        public void Draw(Solver solver, Complex[] coefficients, double intensity)
        {
            this.solver = solver;
            this.coefficients = coefficients;
            glControl.Invalidate();
        }

        private void WritePixels()
        {
            try
            {
                Parallel.ForEach(solver.threads, thread =>
                {
                    int offset = thread.from * solver.order;
                    for (int i = 0; i < thread.roots.Length; i++)
                    {
                        points[offset + i].Position = new Vector2((float)thread.roots[i].r, -(float)thread.roots[i].i);
                        points[offset + i].Color = new Vector3((float)thread.roots[i].colorR / 255.0f, (float)thread.roots[i].colorG / 255.0f, (float)thread.roots[i].colorB / 255.0f);
                    }
                });

                for (int i = 0; i < this.coefficients.Length; i++)
                {
                    points[solver.rootsCount + i].Position = new Vector2((float)this.coefficients[i].Real, -(float)this.coefficients[i].Imaginary);
                    points[solver.rootsCount + i].Color = new Vector3(255, 255, 255);
                }
            }
            catch (Exception ex)
            {
                //this can fail for some single frames
            }
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            if (solver == null)
                return;

            if (points==null || points.Length != solver.rootsCount + this.coefficients.Length)
            { 
                ResetGl();
            }

            WritePixels();

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, points.Length * Marshal.SizeOf<PointVertex>(), points);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(shaderProgram);
            GL.BindVertexArray(vao);
            GL.UniformMatrix4(projLocation, false, ref projectionMatrix);
            GL.DrawArrays(PrimitiveType.Points, 0, points.Length);

            glControl.SwapBuffers();
            frameCounter++;
        }

        private void ResetGl()
        {
            int pointsCount = solver.rootsCount + solver.coefficientsValuesCount;
            points = new PointVertex[pointsCount];

            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);   // meh
            //GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);

            GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
            
            //GL.BlendEquation(OpenTK.Graphics.OpenGL.BlendEquationMode.FuncAdd);

            GL.Enable(EnableCap.PointSprite);

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            // Upload raw data:
            GL.BufferData(BufferTarget.ArrayBuffer,
                          points.Length * Marshal.SizeOf<PointVertex>(),
                          points,
                          BufferUsageHint.StaticDraw);

            int stride = Marshal.SizeOf<PointVertex>();

            // Position attribute (location 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

            // Color attribute (location 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, Marshal.OffsetOf<PointVertex>("Color"));

            projectionMatrix = GetProjectionMatrix();
            shaderProgram = CompileAndLinkShaders();
            projLocation = GL.GetUniformLocation(shaderProgram, "projection");
            SizeChanged();
        }

        public void SetProjection(Complex origin, double zoom)
        {
            this.origin = origin;
            this.zoom = zoom;
            SizeChanged();
        }

        private Matrix4 GetProjectionMatrix()
        {
            var w = (float)(glControl.Width / zoom) / 2;
            var h = (float)(glControl.Height / zoom) / 2;
            var translate = Matrix4.CreateTranslation((float)-origin.Real,(float)origin.Imaginary, 0.0f);
            var ortho = Matrix4.CreateOrthographicOffCenter(-w, w, h, -h, -1f, 1f);
            projectionMatrix = translate * ortho;
            return projectionMatrix;
        }

        public void SizeChanged()
        {
            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            projectionMatrix = GetProjectionMatrix();
            glControl.Invalidate();
        }

        public static int CompileAndLinkShaders()
        {
            string vertexSource = @"
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
                        gl_PointSize = 3.0;
                    }

                    gl_Position = projection * vec4(aPosition, 0.0, 1.0);
                }
                ";

            string fragmentSource = @"
                    #version 330 core

                    in vec3 vColor;
                    out vec4 outputColor;

                    void main()
                    {
                        vec2 uv = gl_PointCoord * 2.0 - 1.0; 
                        float r = length(uv); 

                        if (vColor.r >= 255) {
                            if (r > 1.0 || r < 0.5)
                                discard;
                            outputColor = vec4(vColor*0.5, 0.5);
                        }
                        else {
                            if (r > 1.0)
                                discard;

//use with GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);

float inputAlpha = smoothstep(1.0, 0.0, r);
inputAlpha = inputAlpha*0.8+0.2;
vec3 linear = pow(vColor.rgb, vec3(2.2));  // to linear
float a = inputAlpha * 0.1;
vec3 premul = linear * a;
outputColor = vec4(pow(premul, vec3(1.0/2.2)), a); // back to sRGB

                            //this is good with GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                            //float alpha = smoothstep(1.0, 0.0, r);
                            //alpha = alpha*0.5+0.5;
                            //outputColor = vec4(vColor*alpha, alpha);

                            //this makes sense with GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                            //float alpha = 1.0 - smoothstep(0.0, 1.0, r);  
                            //outputColor = vec4(vColor*alpha, alpha);
                        }

                    }
                    ";

            // Compile vertex shader
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexSource);
            GL.CompileShader(vertexShader);

            GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vStatus);
            if (vStatus != (int)All.True)
            {
                string log = GL.GetShaderInfoLog(vertexShader);
                throw new Exception("Vertex shader compilation failed:\n" + log);
            }

            // Compile fragment shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentSource);
            GL.CompileShader(fragmentShader);

            GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fStatus);
            if (fStatus != (int)All.True)
            {
                string log = GL.GetShaderInfoLog(fragmentShader);
                throw new Exception("Fragment shader compilation failed:\n" + log);
            }

            // Create program and link
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertexShader);
            GL.AttachShader(program, fragmentShader);

            GL.LinkProgram(program);

            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkStatus);
            if (linkStatus != (int)All.True)
            {
                string log = GL.GetProgramInfoLog(program);
                throw new Exception("Shader program linking failed:\n" + log);
            }

            // Shaders can be detached and deleted after linking
            GL.DetachShader(program, vertexShader);
            GL.DetachShader(program, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            return program;
        }

        public void SaveToFile(string fileName)
        {
            glControl.MakeCurrent();
            int width = glControl.Width;
            int height = glControl.Height;
            byte[] pixels = new byte[width * height * 4];

            GL.ReadPixels(
                0, 0,
                width, height,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                PixelType.UnsignedByte,
                pixels
            );

            using (Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var data = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                bmp.UnlockBits(data);
                //bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
                bmp.Save(fileName, ImageFormat.Png);
            }
        }
    }

    struct PointVertex
    {
        public Vector2 Position;   // float x, y
        public Vector3 Color;      // float r, g, b
    }
}

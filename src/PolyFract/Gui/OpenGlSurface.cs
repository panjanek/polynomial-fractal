using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using OpenTK;
using OpenTK.GLControl;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
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
        private Panel placeholder;

        private WindowsFormsHost host;

        private GLControl glControl;

        private int frameCounter = 0;

        private Complex origin = Complex.Zero;

        private double zoom = MainWindow.DefaultZoom;

        private Solver solver;

        private Complex[] coefficients;

        private int shaderProgram;

        private double intensity;

        PointVertex[] points;
        int projLocation;
        int vao;
        int vbo;
        Matrix4 projectionMatrix;
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

            //glControl.Resize += GlControl_Resize;
            glControl.Paint += GlControl_Paint;
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            if (solver == null)
                return;

            if (points==null || points.Length != solver.rootsCount)
            { 
                ResetGl();
            }


            int c = 0;
            foreach (var thread in solver.threads)
            {
                for (int i = 0; i < thread.roots.Length; i++)
                {
                    points[c].Position = new Vector2((float)thread.roots[i].r, (float)thread.roots[i].i);
                    points[c].Color = new Vector3((float)thread.roots[i].colorR / 255.0f, (float)thread.roots[i].colorG / 255.0f, (float)thread.roots[i].colorB / 255.0f);
                    c++;
                }
            }
            /*
            GL.BufferData(BufferTarget.ArrayBuffer,
              points.Length * Marshal.SizeOf<PointVertex>(),
              points,
              BufferUsageHint.StaticDraw);
            */
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            // Update the whole buffer (most common case):
            GL.BufferSubData(
                BufferTarget.ArrayBuffer,
                IntPtr.Zero,
                points.Length * Marshal.SizeOf<PointVertex>(),
                points
            );

            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(shaderProgram);
            GL.BindVertexArray(vao);

            GL.UniformMatrix4(projLocation, false, ref projectionMatrix);

            GL.DrawArrays(PrimitiveType.Points, 0, points.Length);

            glControl.SwapBuffers();

        }

        private void GlControl_Resize(object? sender, EventArgs e)
        {
            SizeChanged();
        }

        public int FrameCounter => frameCounter;

        public void Draw(Solver solver, Complex[] coefficients, double intensity)
        {
            this.solver = solver;
            this.coefficients = coefficients;
            this.intensity = intensity;

            glControl.Invalidate();
        }

        private void ResetGl()
        {
            points = new PointVertex[solver.threads.Sum(t => t.roots.Length)];

            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();

            GL.Enable(EnableCap.ProgramPointSize);

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

            projectionMatrix = Matrix4.CreateOrthographicOffCenter(-4, 4, 4, -4, -1, 1);

            shaderProgram = CompileAndLinkShaders();
            projLocation = GL.GetUniformLocation(shaderProgram, "projection");
            SizeChanged();
        }

        public void SaveToFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public void SetProjection(Complex origin, double zoom)
        {
            this.origin = origin;
            this.zoom = zoom;
            SizeChanged();
        }

        public void SizeChanged()
        {
            GL.Viewport(0, 0, glControl.Width, glControl.Height);

            if (projectionMatrix != null)
            {
                var w = (float)(glControl.Width / zoom)/2;
                var h = (float)(glControl.Height / zoom)/2;
                projectionMatrix = Matrix4.CreateOrthographicOffCenter(-w, w, h, -h, -1, 1);
            }

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
                    gl_PointSize = 1.0;
                    gl_Position = projection * vec4(aPosition, 0.0, 1.0);
                }
                ";

            string fragmentSource = @"
                #version 330 core

                in vec3 vColor;
                out vec4 outputColor;

                void main()
                {
                    outputColor = vec4(vColor, 1.0);
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
    }

    struct PointVertex
    {
        public Vector2 Position;   // float x, y
        public Vector3 Color;      // float r, g, b
    }
}

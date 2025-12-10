using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Media;
using System.Windows.Threading;
using OpenTK;
using OpenTK.GLControl;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common;
using PolyFract.Maths;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;
using Vector2 = OpenTK.Mathematics.Vector2;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace PolyFract.Gui
{
    public class OpenGlSurface : ISurface
    {
        public System.Windows.Controls.Panel MouseEventSource => this.mouseProxy;

        public int FrameCounter => frameCounter;

        private Panel placeholder;

        private WindowsFormsHost host;

        private GLControl glControl;

        private Panel mouseProxy;

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
        MouseEventArgs? prevMouseMove;

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


            glControl.MouseWheel += GlControl_MouseWheel;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseUp += GlControl_MouseUp;
            glControl.MouseMove += GlControl_MouseMove;
        

            mouseProxy = new StackPanel();
        }

        private void GlControl_MouseMove(object? sender, MouseEventArgs e)
        {
            
            if (e.Button == MouseButtons.Left)
            {
                if (prevMouseMove?.X == e.X && prevMouseMove?.Y == e.Y)
                    return;

                prevMouseMove = e;

                var args = new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0);
                args.RoutedEvent = UIElement.MouseMoveEvent;
                DraggingHandler.ProxyPoint = new System.Windows.Point(e.X, e.Y); //new System.Windows.Point(_pendingMove.Value.X, _pendingMove.Value.Y);      //
                mouseProxy.RaiseEvent(args);
            }
        }

        private void GlControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
                args.RoutedEvent = UIElement.MouseLeftButtonUpEvent;
                DraggingHandler.ProxyPoint = new System.Windows.Point(e.X, e.Y);
                mouseProxy.RaiseEvent(args);
            }
        }

        private void GlControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
                args.RoutedEvent = UIElement.MouseLeftButtonDownEvent;
                DraggingHandler.ProxyPoint = new System.Windows.Point(e.X, e.Y);
                mouseProxy.RaiseEvent(args);
            }
            else if (e.Button == MouseButtons.Right)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Right);
                args.RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent;
                DraggingHandler.ProxyPoint = new System.Windows.Point(e.X, e.Y);
                mouseProxy.RaiseEvent(args);
            }

        }

        private void GlControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            var args = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, e.Delta);
            DraggingHandler.ProxyPoint = new System.Windows.Point(e.X, e.Y);
            args.RoutedEvent = UIElement.MouseWheelEvent;
            mouseProxy.RaiseEvent(args);
        }

        private void WritePixels()
        {
            Parallel.ForEach(solver.threads, thread => 
            {
                int offset = thread.from * solver.order;
                for (int i = 0; i < thread.roots.Length; i++)
                {
                    points[offset+i].Position = new Vector2((float)thread.roots[i].r, -(float)thread.roots[i].i);
                    points[offset + i].Color = new Vector3((float)thread.roots[i].colorR / 255.0f, (float)thread.roots[i].colorG / 255.0f, (float)thread.roots[i].colorB / 255.0f);
                }
            });

            for(int i=0; i<solver.coefficientsValuesCount; i++)
            {
                points[solver.rootsCount + i].Position = new Vector2((float)solver.threads[0].coeffs[i].r, -(float)solver.threads[0].coeffs[i].i);
                points[solver.rootsCount + i].Color = new Vector3(255,255,255);
            }
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            if (solver == null)
                return;

            if (points==null || points.Length != solver.rootsCount + solver.coefficientsValuesCount)
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

        private void GlControl_Resize(object? sender, EventArgs e)
        {
            SizeChanged();
        }

        public void Draw(Solver solver, Complex[] coefficients, double intensity)
        {
            this.solver = solver;
            this.coefficients = coefficients;
            this.intensity = intensity;

            glControl.Invalidate();
        }

        private void ResetGl()
        {
            int pointsCount = solver.rootsCount + solver.coefficientsValuesCount;
            points = new PointVertex[pointsCount];


            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);   // meh
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
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
                   // gl_PointSize = 1.0;
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
        float alpha = smoothstep(1.0, 0.0, r);
        alpha = alpha/2 + 0.5;
        outputColor = vec4(vColor*alpha, alpha);
    }

}

";




            /*
            string fragmentSource = @"
#version 330 core

in vec3 vColor;
out vec4 FragColor;

void main() {
    vec2 uv = gl_PointCoord * 2.0 - 1.0;
    float r2 = dot(uv, uv);

    if (r2 > 1.0)
        discard;

    float sigma = 0.4;
    float alpha = exp(-r2 / (2.0 * sigma * sigma));

    FragColor = vec4(vColor, alpha);
}

";

*/


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

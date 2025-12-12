using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using PolyFract.Maths;
using Application = System.Windows.Application;
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

        public static bool ComputeShaderSupported { get; set; }

        private readonly Panel placeholder;

        private readonly WindowsFormsHost host;

        private readonly GLControl glControl;

        private readonly WinFormsMouseProxy mouseProxy;

        private int frameCounter = 0;

        private Complex origin = Complex.Zero;

        private double zoom = MainWindow.DefaultZoom;

        private Solver solver;

        private Complex[] coefficients;

        private int computeProgram;

        private int vertexProgram;

        private int pointsCount;

        private int projLocation;

        private int vao;

        private int vbo;

        private int ubo;

        private int pointsBuffer;

        private Matrix4 projectionMatrix;

        private ComputeShaderConfig computeShaderConfig;

        bool uiPending;

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

            ComputeShaderSupported = true;
        }

        public void Draw(Solver solver, Complex[] coefficients, double intensity)
        {
            this.solver = solver;


            // schedule drawing for ui thread
            if (Application.Current?.Dispatcher != null && !uiPending)
            {
                uiPending = true;
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Background,
                        (Action)(() =>
                        {

                            try
                            {
                                if (solver != null)
                                {
                                    if (pointsCount == 0 || pointsCount != solver.rootsCount + solver.coeffValues.Length)
                                    {
                                        ResetGl();
                                    }


                                    RunShaderComputations();
                                    frameCounter++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }
                            finally
                            {
                                uiPending = false;
                            }
                        }));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public void RunShaderComputations()
        {
            //prepare config for compute shader
            unsafe
            {
                computeShaderConfig.order = solver.order;
                computeShaderConfig.coeffValuesCount = solver.coefficientsValuesCount;
                computeShaderConfig.polysCount = solver.polynomialsCount;
                for (int i = 0; i < solver.coeffValues.Length; i++)
                {
                    computeShaderConfig.coeffsValues_r[i] = solver.coeffValues[i].r;
                    computeShaderConfig.coeffsValues_i[i] = solver.coeffValues[i].i;
                }
            }

            //upload config
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ubo);
            GL.BufferSubData(
                BufferTarget.ShaderStorageBuffer,
                IntPtr.Zero,
                Marshal.SizeOf<ComputeShaderConfig>(),
                ref computeShaderConfig
            );

            //compute
            GL.UseProgram(computeProgram);
            int localSizeX = 256;
            int instanceCount = solver.polynomialsCount + solver.coefficientsValuesCount;
            GL.DispatchCompute((instanceCount + localSizeX - 1) / localSizeX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);

            glControl.Invalidate();
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            if (solver == null)
                return;

            if (ComputeShaderSupported)
                PaintUsingComputeShaders();
            else
                PaintUsingCpuComputedRoots();

            frameCounter++;
        }

        public void ResetForComputeShaders()
        {
            // allocate space for ComputeShaderConfig passed to each compute shader
            ubo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ubo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
                          Marshal.SizeOf<ComputeShaderConfig>(),
                          IntPtr.Zero,
                          BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ubo);

            // create dummy vao
            GL.GenVertexArrays(1, out vao);
            GL.BindVertexArray(vao);

            // create buffer for data emited from compute shader
            GL.GenBuffers(1, out pointsBuffer);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, pointsBuffer);
            pointsCount = solver.rootsCount + solver.coefficientsValuesCount;
            int sizeBytes = pointsCount * Marshal.SizeOf<CompactClomplexFloatWithColor>();
            GL.BufferData(BufferTarget.ShaderStorageBuffer,
                          sizeBytes,
                          IntPtr.Zero,
                          BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, pointsBuffer);

            projectionMatrix = GetProjectionMatrix();
            computeProgram = CompileAndLinkComputeShader("solver-d.comp");
            vertexProgram = CompileAndLinkVertexAndFragmetShaders("shader-c.vert", "shader-c.frag");
            projLocation = GL.GetUniformLocation(vertexProgram, "projection");
            if (projLocation == -1)
                throw new Exception("Uniform 'projection' not found. Shader optimized it out?");

            SizeChanged();
        }

        private void PaintUsingComputeShaders()
        {
            //draw
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(vertexProgram);
            GL.UniformMatrix4(projLocation, false, ref projectionMatrix);
            GL.BindVertexArray(vao);
            GL.DrawArrays(PrimitiveType.Points, 0, solver.rootsCount + solver.coefficientsValuesCount);
            glControl.SwapBuffers();
        }

        private int CompileAndLinkComputeShader(string compFile)
        {
            // Compile compute shader
            int computeShader = GL.CreateShader(ShaderType.ComputeShader);
            string source = File.ReadAllText(compFile);
            GL.ShaderSource(computeShader, source);
            GL.CompileShader(computeShader);
            GL.GetShader(computeShader, ShaderParameter.CompileStatus, out int status);
            if (status != (int)All.True)
            {
                var log = GL.GetShaderInfoLog(computeShader);
                throw new Exception(log);
            }

            int program = GL.CreateProgram();
            GL.AttachShader(program, computeShader);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out status);
            if (status != (int)All.True)
            {
                throw new Exception(GL.GetProgramInfoLog(program));
            }

            return program;
        }

        private void PaintUsingCpuComputedRoots()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            //copy roots coordinated to GPU
            nint structSize = Marshal.SizeOf<CompactClomplexFloatWithColor>();
            foreach (var thread in solver.threads)
            {
                nint offset = thread.from * thread.order;
                GL.BufferSubData(BufferTarget.ArrayBuffer, offset * structSize, thread.roots.Length * structSize, thread.roots);
            }

            //copy coeff values to GPU to draw them as bigger circles in shader
            GL.BufferSubData(BufferTarget.ArrayBuffer, solver.rootsCount * structSize, solver.coeffValues.Length * structSize, solver.coeffValues);

            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(vertexProgram);
            GL.BindVertexArray(vao);
            GL.UniformMatrix4(projLocation, false, ref projectionMatrix);
            GL.DrawArrays(PrimitiveType.Points, 0, solver.rootsCount + solver.coeffValues.Length);

            glControl.SwapBuffers();
        }

        private void ResetGl()
        {
            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            //GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);   // meh
            //GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
            //GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);   
            GL.BlendEquation(OpenTK.Graphics.OpenGL.BlendEquationMode.FuncAdd);
            GL.Enable(EnableCap.PointSprite);

            if (ComputeShaderSupported)
                ResetForComputeShaders();
            else
                ResetForCpuComputedRoots();
        }

        public void ResetForCpuComputedRoots()
        {
            vao = GL.GenVertexArray();
            vbo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            // Init VAO buffer to current point count
            pointsCount = solver.rootsCount + solver.coeffValues.Length;
            int structSize = Marshal.SizeOf<CompactClomplexFloatWithColor>();
            GL.BufferData(BufferTarget.ArrayBuffer, this.pointsCount * structSize, nint.Zero, BufferUsageHint.DynamicDraw);

            // Position attribute (location 0)
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, structSize, 0);

            // Color attribute (location 1)
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, structSize, Marshal.OffsetOf<CompactClomplexFloatWithColor>("colorR"));

            projectionMatrix = GetProjectionMatrix();
            vertexProgram = CompileAndLinkVertexAndFragmetShaders("shader.vert", "shader.frag");
            projLocation = GL.GetUniformLocation(vertexProgram, "projection");

            SizeChanged();
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
            projectionMatrix = GetProjectionMatrix();
            glControl.Invalidate();
        }

        private Matrix4 GetProjectionMatrix()
        {
            // rescale by windows display scale setting to match WPF coordinates
            var w = (float)((glControl.Width / GuiUtil.Dpi.DpiScaleX) / zoom) / 2;
            var h = (float)((glControl.Height / GuiUtil.Dpi.DpiScaleY) / zoom) / 2;
            var translate = Matrix4.CreateTranslation((float)-origin.Real, (float)-origin.Imaginary, 0.0f);
            var ortho = Matrix4.CreateOrthographicOffCenter(-w, w, -h, h, -1f, 1f);
            var matrix = translate * ortho;
            return matrix;
        }

        public static int CompileAndLinkVertexAndFragmetShaders(string vertFile, string fragFile)
        {
            string vertexSource = File.ReadAllText(vertFile);
            string fragmentSource = File.ReadAllText(fragFile);

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

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 3] = 255;   // force A = 255 for BGRA
            }

            using (Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                var data = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                );

                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                bmp.UnlockBits(data);
                bmp.Save(fileName, ImageFormat.Png);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct ComputeShaderConfig
    {
        public int order;
        public int coeffValuesCount;
        public int polysCount;
        public fixed float coeffsValues_r[16];
        public fixed float coeffsValues_i[16];
    }
}

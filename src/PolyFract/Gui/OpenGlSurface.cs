using System.Diagnostics.SymbolStore;
using System.Drawing.Imaging;
using System.IO;
using System.Numerics;
using System.Reflection;
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

        public Action NewFrame { get; set; }

        public string Name => "opengl";

        public static bool UseComputeShader { get; set; }

        private readonly Panel placeholder;

        private readonly WindowsFormsHost host;

        private readonly GLControl glControl;

        private readonly WinFormsMouseProxy mouseProxy;

        private readonly int computeProgram;

        private readonly int renderForComputeProgram;

        private readonly int renderForBufferProgram;

        private readonly int ubo;

        private readonly int projLocationCompute;

        private readonly int projLocationForBuffer;

        private readonly int maxGroupsX;

        private int frameCounter = 0;

        private Complex origin = Complex.Zero;

        private double zoom = MainWindow.DefaultZoom;

        private Solver solver;

        private int pointsCount;

        private int dummyVao;

        private int vao;

        private int vbo;

        public int emitedPointsBuffer;

        private Matrix4 projectionMatrix;

        private ComputeShaderConfig computeShaderConfig;

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
            placeholder.KeyDown += Placeholder_KeyDown;
            mouseProxy = new WinFormsMouseProxy(glControl);

            //setup required features
            GL.Enable(EnableCap.ProgramPointSize);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
            GL.BlendEquation(OpenTK.Graphics.OpenGL.BlendEquationMode.FuncAdd);
            GL.Enable(EnableCap.PointSprite);

             // allocate space for ComputeShaderConfig passed to each compute shader
            ubo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ubo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, Marshal.SizeOf<ComputeShaderConfig>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ubo);
            GL.GetInteger((OpenTK.Graphics.OpenGL.GetIndexedPName)All.MaxComputeWorkGroupCount, 0, out maxGroupsX);

            try
            {
                computeProgram = ShaderUtil.CompileAndLinkComputeShader("solver.comp");
                renderForComputeProgram = ShaderUtil.CompileAndLinkRenderShader("shader-c.vert", "shader-c.frag");
                projLocationCompute = GL.GetUniformLocation(renderForComputeProgram, "projection");
                if (projLocationCompute == -1)
                    throw new Exception("Uniform 'projection' not found. Shader optimized it out?");
                UseComputeShader = true;
            }
            catch (Exception ex)
            {
                UseComputeShader = false;
            }

            renderForBufferProgram = ShaderUtil.CompileAndLinkRenderShader("shader.vert", "shader.frag");
            projLocationForBuffer = GL.GetUniformLocation(renderForBufferProgram, "projection");
            if (projLocationForBuffer == -1)
                throw new Exception("Uniform 'projection' not found. Shader optimized it out?");
        }

        private void Placeholder_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.G)
            {
                UseComputeShader = !UseComputeShader;
                SetupBuffers();
            }
        }

        public void Draw(Solver solver, Complex[] coefficients)
        {
            if (Application.Current.MainWindow.WindowState == System.Windows.WindowState.Minimized)
                return;

            this.solver = solver;
            if (pointsCount == 0 || pointsCount != solver.rootsCount + solver.coeffValues.Length)
            {
                SetupBuffers();
            }

            if (UseComputeShader)
                RunShaderComputations();
            else
                CopyCpuDataToGpu();

            glControl.Invalidate();
        }

        private void CopyCpuDataToGpu()
        {
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

            //copy roots coordinates to GPU
            nint structSize = Marshal.SizeOf<CompactClomplexFloatWithColor>();
            foreach (var thread in solver.threads)
            {
                nint offset = thread.from * thread.order;
                GL.BufferSubData(BufferTarget.ArrayBuffer, offset * structSize, thread.roots.Length * structSize, thread.roots);
            }

            //copy coeff values to GPU to draw them as bigger circles in shader
            GL.BufferSubData(BufferTarget.ArrayBuffer, solver.rootsCount * structSize, solver.coeffValues.Length * structSize, solver.coeffValues);            
        }

        public void RunShaderComputations()
        {
            //prepare config for compute shader
            unsafe
            {
                computeShaderConfig.order = solver.order;
                computeShaderConfig.coeffValuesCount = solver.coefficientsValuesCount;
                computeShaderConfig.polysCount = solver.polynomialsCount;
                computeShaderConfig.coeffsVisible = solver.coeffsVisible ? 1 : 0;
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
            int instanceCount = solver.polynomialsCount + solver.coefficientsValuesCount;
            int dispatchGroupsX = (instanceCount + ShaderUtil.LocalSizeX - 1) / ShaderUtil.LocalSizeX;
            if (dispatchGroupsX > maxGroupsX)
                dispatchGroupsX = maxGroupsX;
            GL.DispatchCompute(dispatchGroupsX, 1, 1);
            GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
        }

        private void GlControl_Paint(object? sender, PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(UseComputeShader ? renderForComputeProgram : renderForBufferProgram);
            GL.BindVertexArray(UseComputeShader ? dummyVao : vao);
            projectionMatrix = GetProjectionMatrix();
            GL.UniformMatrix4(UseComputeShader ? projLocationCompute : projLocationForBuffer, false, ref projectionMatrix);
            GL.DrawArrays(PrimitiveType.Points, 0, pointsCount);
            glControl.SwapBuffers();
            frameCounter++;
            if (NewFrame != null)
                NewFrame();
        }

        private void SetupBuffers()
        {
            pointsCount = solver.rootsCount + solver.coeffValues.Length;
            if (UseComputeShader)
            {
                // create dummy vao
                GL.GenVertexArrays(1, out dummyVao);
                GL.BindVertexArray(dummyVao);

                // create buffer for data emited from compute shader
                GL.GenBuffers(1, out emitedPointsBuffer);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, emitedPointsBuffer);
                pointsCount = solver.rootsCount + solver.coefficientsValuesCount;
                int shaderPointStrideSize = 32; // this is stride size for struct declared in shaders only struct CompactComplexFloatWithColor { vec2 position; vec4 color; }
                int sizeBytes = pointsCount * shaderPointStrideSize;
                GL.BufferData(BufferTarget.ShaderStorageBuffer, sizeBytes, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, emitedPointsBuffer);
            }
            else
            {
                vao = GL.GenVertexArray();
                vbo = GL.GenBuffer();
                GL.BindVertexArray(vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);

                // Init VAO buffer to current point count
                int structSize = Marshal.SizeOf<CompactClomplexFloatWithColor>();
                GL.BufferData(BufferTarget.ArrayBuffer, this.pointsCount * structSize, nint.Zero, BufferUsageHint.DynamicDraw);

                // Position attribute (location 0)
                GL.EnableVertexAttribArray(0);
                GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, structSize, 0);

                // Color attribute (location 1)
                GL.EnableVertexAttribArray(1);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, structSize, Marshal.OffsetOf<CompactClomplexFloatWithColor>("colorR"));
            }

            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            glControl.Invalidate();
        }

        public void SetProjection(Complex origin, double zoom)
        {
            this.origin = origin;
            this.zoom = zoom;
            SizeChanged();
        }

        public void SizeChanged()
        {
            if (glControl.Width <= 0 || glControl.Height <= 0)
                return;

            if (!glControl.Context.IsCurrent)
                glControl.MakeCurrent();

            GL.Viewport(0, 0, glControl.Width, glControl.Height);
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

        public static string GlVendor
        {
            get
            {
                try
                {
                    return GL.GetString(StringName.Vendor);
                }
                catch
                {
                    return null;
                }
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
        public int coeffsVisible;
    }
}

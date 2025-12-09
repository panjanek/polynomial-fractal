using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PolyFract.Maths;

namespace PolyFract.Gui
{
    public class WpfSurface : ISurface
    {
        private Image Image { get; set; }

        private WriteableBitmap Bitmap { get; set; }

        private bool uiPending { get; set; }

        public int FrameCounter => frameCounter;

        private readonly Panel placeholder;

        private Complex origin = Complex.Zero;

        private double zoom = MainWindow.DefaultZoom;

        private int frameCounter = 0;

        

        private readonly double[,] coeffMarker = new double[,]
    {
                { 0.0, 0.3, 1.0, 1.0, 1.0, 1.0, 1.0, 0.3, 0.0 },
                { 0.3, 1.0, 1.0, 0.3, 0.5, 0.3, 1.0, 1.0, 0.3 },
                { 1.0, 1.0, 0.3, 0.0, 1.0, 0.0, 0.3, 1.0, 1.0 },
                { 1.0, 0.3, 0.0, 0.3, 1.0, 0.3, 0.0, 0.3, 1.0 },
                { 1.0, 0.5, 1.0, 1.0, 1.0, 1.0, 1.0, 0.5, 1.0 },
                { 1.0, 0.3, 0.0, 0.3, 1.0, 0.3, 0.0, 0.3, 1.0 },
                { 1.0, 1.0, 0.3, 0.0, 1.0, 0.0, 0.3, 1.0, 1.0 },
                { 0.3, 1.0, 1.0, 0.3, 0.5, 0.3, 1.0, 1.0, 0.3 },
                { 0.0, 0.3, 1.0, 1.0, 1.0, 1.0, 1.0, 0.3, 0.0 },
    };

        private readonly double[,] rootMarker = new double[,]
            {
                { 0.00, 0.02, 0.05, 0.02, 0.00 },
                { 0.02, 0.10, 0.20, 0.10, 0.02 },
                { 0.05, 0.20, 1.00, 0.20, 0.05 },
                { 0.02, 0.10, 0.20, 0.10, 0.02 },
                { 0.00, 0.02, 0.05, 0.02, 0.00 }
            };

        private readonly int[,] coeffMarkerInt;
        private readonly int[,] rootMarkerInt;

        public WpfSurface(Panel placeholder)
        {
            this.placeholder = placeholder;
            placeholder.Children.Clear();
            Image = new Image();
            Image.Visibility = Visibility.Visible;
            Image.HorizontalAlignment = HorizontalAlignment.Stretch;
            Image.VerticalAlignment = VerticalAlignment.Stretch;
            placeholder.Children.Add(Image);
            Bitmap = new WriteableBitmap((int)placeholder.ActualWidth, (int)placeholder.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
            Image.Source = Bitmap;
            coeffMarkerInt = DoubleMatrixToInt(coeffMarker);
            rootMarkerInt = DoubleMatrixToInt(rootMarker);
            
        }

        public void SizeChanged()
        {
            Bitmap = new WriteableBitmap((int)placeholder.ActualWidth, (int)placeholder.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
            Image.Source = Bitmap;
        }

        public void SetProjection(Complex origin, double zoom)
        {
            this.origin = origin;
            this.zoom = zoom;
        }

        public void Draw(Solver solver, Complex[] coefficients, double intensity)
        {
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
                                    InternalDraw(solver, coefficients, intensity);
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

        private (int x, int y) ToPixelCoordinates(Complex x)
        {
            return ToPixelCoordinates(x.Real, x.Imaginary);
        }

        private (int x, int y) ToPixelCoordinates(double real, double imaginary)
        {
            int ix = (int)System.Math.Round(placeholder.ActualWidth / 2 + (real - origin.Real) * zoom);
            int iy = (int)System.Math.Round(placeholder.ActualHeight / 2 - (imaginary - origin.Imaginary) * zoom);
            return (ix, iy);
        }

        private int[,] DoubleMatrixToInt(double[,] matrix)
        {
            int[,] result = new int[matrix.GetLength(0), matrix.GetLength(1)];
            for (int i = 0; i < matrix.GetLength(0); i++)
                for (int j = 0; j < matrix.GetLength(1); j++)
                    result[i, j] = (int)Math.Round(matrix[i, j] * 255);
            return result;
        }

        private void InternalDraw(Solver solver, Complex[] coefficients, double intensity)
        {
            // compute pixel coordinates
            Parallel.ForEach(solver.threads, new ParallelOptions() { MaxDegreeOfParallelism = solver.threads.Length }, thread =>
            {
                int x, y;
                for (int i = 0; i < thread.roots.Length; i++)
                {
                    var root = thread.roots[i];
                    if (root.r == Polynomials.ErrorMarker)
                    {
                        thread.pixels[i].x = Polynomials.ErrorMarker;
                    }
                    else
                    {
                        (x, y) = ToPixelCoordinates(root.r, root.i);
                        thread.pixels[i].x = x;
                        thread.pixels[i].y = y;
                        thread.pixels[i].r = root.colorR;
                        thread.pixels[i].g = root.colorG;
                        thread.pixels[i].b = root.colorB;
                    }
                }
            });

            int intensityInt = (int)System.Math.Round(255 * intensity);
            Bitmap.Lock();
            unsafe
            {
                byte* pBackBuffer = (byte*)Bitmap.BackBuffer;
                int size = Bitmap.BackBufferStride * Bitmap.PixelHeight;
                System.Runtime.CompilerServices.Unsafe.InitBlock(pBackBuffer, 0, (uint)size);
                foreach (var thread in solver.threads)
                {
                    for (int i = 0; i < thread.pixels.Length; i++)
                    {
                        var pixel = thread.pixels[i];
                        if (pixel.x != Polynomials.ErrorMarker)
                            AddGlyph(pBackBuffer, pixel.x, pixel.y, rootMarkerInt, pixel.r, pixel.g, pixel.b, intensityInt);
                    }
                }
                ;

                foreach (var coef in coefficients)
                {
                    (int cx, int cy) = ToPixelCoordinates(coef);
                    AddGlyph(pBackBuffer, cx, cy, coeffMarkerInt, Colors.Red, 255, true);
                }
            }

            Bitmap.AddDirtyRect(new Int32Rect(0, 0, Bitmap.PixelWidth, Bitmap.PixelHeight));
            Bitmap.Unlock();
        }

        private unsafe void AddGlyph(byte* pBackBuffer, int cx, int cy, int[,] map, System.Windows.Media.Color color, int intensity = 255, bool overwrite = false)
        {
            AddGlyph(pBackBuffer, cx, cy, map, color.R, color.G, color.B, intensity, overwrite);
        }

        private unsafe void AddGlyph(byte* pBackBuffer, int cx, int cy, int[,] map, int r, int g, int b, int intensity = 255, bool overwrite = false)
        {
            for (int mx = 0; mx < map.GetLength(0); mx++)
            {
                for (int my = 0; my < map.GetLength(1); my++)
                {
                    var strength = map[mx, my];
                    int x = cx - map.GetLength(0) / 2 + mx;
                    int y = cy - map.GetLength(1) / 2 + my;
                    if (x >= 0 && y >= 0 && x < Bitmap.PixelWidth && y < Bitmap.PixelHeight)
                    {
                        int coord = y * Bitmap.PixelWidth + x << 2;
                        var cr = (r * strength * intensity) >> 16;
                        var cg = (g * strength * intensity) >> 16;
                        var cb = (b * strength * intensity) >> 16;
                        AddPixel(pBackBuffer, coord, cr, cg, cb, overwrite);
                    }
                }
            }
        }

        private unsafe void AddPixel(byte* pBackBuffer, int coord, int r, int g, int b, bool overwrite = false)
        {
            if (overwrite)
            {
                if (b != 0 || g != 0 || r != 0)
                {
                    pBackBuffer[coord + 0] = (byte)b;
                    pBackBuffer[coord + 1] = (byte)g;
                    pBackBuffer[coord + 2] = (byte)r;
                }
            }
            else
            {
                pBackBuffer[coord + 0] = Blend(pBackBuffer[coord + 0], b);
                pBackBuffer[coord + 1] = Blend(pBackBuffer[coord + 1], g);
                pBackBuffer[coord + 2] = Blend(pBackBuffer[coord + 2], r);
            }

            pBackBuffer[coord + 3] = 255;
        }

        private byte Blend(int src, int dst)
        {
            int res = (246 * (src + dst)) >> 8;
            if (res > 255)
                res = 255;
            return (byte)res;
        }
    }
}

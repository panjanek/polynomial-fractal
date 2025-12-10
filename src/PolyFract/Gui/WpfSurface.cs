using System;
using System.Collections.Generic;
using System.IO;
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
using Application = System.Windows.Application;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Image = System.Windows.Controls.Image;
using Panel = System.Windows.Controls.Panel;

namespace PolyFract.Gui
{
    public class WpfSurface : ISurface
    {
        public int FrameCounter => frameCounter;

        public string Name => "wpf";

        public System.Windows.Controls.Panel MouseEventSource => this.placeholder;

        private readonly Image image;

        private WriteableBitmap bitmap;

        private readonly Panel placeholder;

        private bool uiPending;

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
            image = new Image();
            image.Visibility = Visibility.Visible;
            image.HorizontalAlignment = HorizontalAlignment.Stretch;
            image.VerticalAlignment = VerticalAlignment.Stretch;
            placeholder.Children.Add(image);
            bitmap = new WriteableBitmap((int)placeholder.ActualWidth, (int)placeholder.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
            image.Source = bitmap;
            coeffMarkerInt = DoubleMatrixToInt(coeffMarker);
            rootMarkerInt = DoubleMatrixToInt(rootMarker);
        }

        public void SizeChanged()
        {
            bitmap = new WriteableBitmap((int)placeholder.ActualWidth, (int)placeholder.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
            image.Source = bitmap;
        }

        public void SetProjection(Complex origin, double zoom)
        {
            this.origin = origin;
            this.zoom = zoom;
        }

        public void SaveToFile(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
        }

        public void Draw(Solver solver, Complex[] coefficients, double intensity)
        {
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
                                    CalculatePixelCoords(solver);
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

        private void CalculatePixelCoords(Solver solver)
        {
            int pixelWidth = (int)placeholder.ActualWidth;
            int pixelHeight = (int)placeholder.ActualHeight;
            Parallel.ForEach(solver.threads, new ParallelOptions() { MaxDegreeOfParallelism = solver.threads.Length }, thread =>
            {
                for (int i = 0; i < thread.roots.Length; i++)
                {
                    var root = thread.roots[i];
                    if (root.r == Polynomials.ErrorMarker)
                    {
                        thread.pixels[i].x = Polynomials.ErrorMarker;
                    }
                    else
                    {
                        //(int x, int y) = GuiUtil.ToPixelCoordinates(root.r, root.i, pixelWidth, pixelHeight, origin, zoom);
                        int x = unchecked((int)(pixelWidth / 2 + (root.r - origin.Real) * zoom));
                        int y = unchecked((int)(pixelHeight / 2 - (root.i - origin.Imaginary) * zoom));
                        thread.pixels[i].x = x;
                        thread.pixels[i].y = y;
                        thread.pixels[i].r = root.colorR;
                        thread.pixels[i].g = root.colorG;
                        thread.pixels[i].b = root.colorB;
                    }
                }
            });
        }

        private void InternalDraw(Solver solver, Complex[] coefficients, double intensity)
        {
            int intensityInt = (int)System.Math.Round(255 * intensity);
            bitmap.Lock();
            unsafe
            {
                byte* pBackBuffer = (byte*)bitmap.BackBuffer;
                int size = bitmap.BackBufferStride * bitmap.PixelHeight;
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
                    (int cx, int cy) = GuiUtil.ToPixelCoordinates(coef, bitmap.PixelWidth, bitmap.PixelHeight, origin, zoom);
                    AddGlyph(pBackBuffer, cx, cy, coeffMarkerInt, Colors.Red, 255, true);
                }
            }

            bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            bitmap.Unlock();
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
                    if (x >= 0 && y >= 0 && x < bitmap.PixelWidth && y < bitmap.PixelHeight)
                    {
                        int coord = y * bitmap.PixelWidth + x << 2;
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

        private static byte Blend(int src, int dst)
        {
            int res = (246 * (src + dst)) >> 8;
            if (res > 255)
                res = 255;
            return (byte)res;
        }

        private static int[,] DoubleMatrixToInt(double[,] matrix)
        {
            int[,] result = new int[matrix.GetLength(0), matrix.GetLength(1)];
            for (int i = 0; i < matrix.GetLength(0); i++)
                for (int j = 0; j < matrix.GetLength(1); j++)
                    result[i, j] = (int)Math.Round(matrix[i, j] * 255);
            return result;
        }
    }
}

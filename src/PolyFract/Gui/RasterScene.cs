using System.IO;
using System.Numerics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PolyFract.Maths;

namespace PolyFract.Gui
{
    public class RasterScene
    {
        public const int MarkerRadius = 5;

        public const double ZoomingSpeed = 0.0002;
        public Image Image { get; set; }

        public WriteableBitmap Bitmap { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public double Intensity { get; set; } = 0.5;

        public byte[] Pixels { get; set; }

        public Action DraggedOrZoommed { get; set; }

        public Complex Origin { get; set; } = Complex.Zero;
        public double Zoom { get; set; } = MainWindow.DefaultZoom;

        private Complex[] coefficients = [];

        private DraggingHandler dragging;

        double[,] coeffMarker = new double[,]
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

        double[,] rootMarker = new double[,]
            {
                { 0.00, 0.02, 0.05, 0.02, 0.00 },
                { 0.02, 0.10, 0.20, 0.10, 0.02 },
                { 0.05, 0.20, 1.00, 0.20, 0.05 },
                { 0.02, 0.10, 0.20, 0.10, 0.02 },
                { 0.00, 0.02, 0.05, 0.02, 0.00 }
            };

        int[,] coeffMarkerInt;
        int[,] rootMarkerInt;

        public RasterScene(Panel placeholder)
        {
            CreateImage(placeholder);
            coeffMarkerInt = DoubleMatrixToInt(coeffMarker);
            rootMarkerInt = DoubleMatrixToInt(rootMarker);
        }

        private int[,] DoubleMatrixToInt(double[,] matrix)
        {
            int[,] result = new int[matrix.GetLength(0), matrix.GetLength(1)];
            for (int i = 0; i < matrix.GetLength(0); i++)
                for (int j = 0; j < matrix.GetLength(1); j++)
                    result[i, j] = (int)Math.Round(matrix[i, j] * 255);
            return result;
        }

        public void Reset(Panel placeholder)
        {
            
            CreateImage(placeholder);
        }

        public void CreateImage(Panel placeholder)
        {
            placeholder.Children.Clear();
            Image = new Image();
            if (!double.IsNaN(placeholder.ActualWidth) && !double.IsNaN(placeholder.ActualHeight) && placeholder.ActualWidth>0 && placeholder.ActualHeight > 0)
            {
                Image.Width = placeholder.ActualWidth;
                Image.Height = placeholder.ActualHeight;
            }
            else
            {
                Image.Width = 1920;
                Image.Height = 1080;
            }

            Image.Visibility = Visibility.Visible;
            placeholder.Children.Add(Image);

            Width = (int)Image.Width;
            Height = (int)Image.Height;
            Bitmap = new WriteableBitmap(Width, Height, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
            Image.Source = Bitmap;
            Pixels = ClearRasterImage();

            dragging = new DraggingHandler(Image, mouse =>
            {
                foreach (var coeff in coefficients)
                {
                    (var markerX, var markerY) = ToPixelCoordinates(coeff);
                    if (MathUtil.IsInSquare(mouse.X, mouse.Y, markerX, markerY, MarkerRadius))
                        return false;
                }

                if (DraggedOrZoommed != null)
                    DraggedOrZoommed();
                return true;

            }, (prev, curr) =>
            {
                var delta = new Complex((curr.X - prev.X) / Zoom, -(curr.Y - prev.Y) / Zoom);
                Origin -= delta;
            });

            Image.MouseWheel += Image_MouseWheel;
        }

        private void Image_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(Image);

            double zoomRatio = 1.0 + ZoomingSpeed * e.Delta;

            var topLeft1 = ToComplexCoordinates(0, 0);
            var bottomRight1 = ToComplexCoordinates(Image.Width, Image.Height);
            var zoomCenter = ToComplexCoordinates(Image.Width - pos.X, Image.Height - pos.Y);

            var r1 = bottomRight1.Real - topLeft1.Real;
            var i1 = bottomRight1.Imaginary - topLeft1.Imaginary;
            var r2 = r1 * zoomRatio;
            var i2 = i1 * zoomRatio;

            var cx = zoomCenter.Real - topLeft1.Real;
            var bx = cx * r2 / r1;

            var cy = zoomCenter.Imaginary - topLeft1.Imaginary;
            var by = cy * i2 / i1;

            var topLeft2 = new Complex(zoomCenter.Real - bx, zoomCenter.Imaginary - by);
            var bottomRight2 = new Complex(topLeft2.Real + r2, topLeft2.Imaginary + i2);

            Origin = (bottomRight2 + topLeft2) / 2;
            Zoom *= zoomRatio;

            if (DraggedOrZoommed != null)
                DraggedOrZoommed();
        }

        public (int x, int y) ToPixelCoordinates(Complex x)
        {
            return ToPixelCoordinates(x.Real, x.Imaginary);
        }

        public (int x, int y) ToPixelCoordinates(double real, double imaginary)
        {
            int ix = (int)System.Math.Round(Width / 2 + (real - Origin.Real) * Zoom);
            int iy = (int)System.Math.Round(Height / 2 - (imaginary - Origin.Imaginary) * Zoom);
            return (ix, iy);
        }

        public Complex ToComplexCoordinates(double x, double y)
        {
            return new Complex((x - Width / 2)/Zoom + Origin.Real, (Height / 2 - y) / Zoom + Origin.Imaginary);
        }

        public void FastDraw(Solver solver, Complex[] coefficients)
        {
            // compute pixel coordinates
            Parallel.ForEach(solver.threads, new ParallelOptions() { MaxDegreeOfParallelism = solver.threads.Length }, thread =>
            {
                int x, y;
                for (int i = 0; i < thread.real.Length; i++)
                {
                    (x, y) = ToPixelCoordinates(thread.real[i], thread.imaginary[i]);
                    thread.pixel_x[i] = x;
                    thread.pixel_y[i] = y;
                }
            });

            this.coefficients = coefficients;
            int intensityInt = (int)System.Math.Round(255 * Intensity);
            Bitmap.Lock();
            unsafe
            {
                byte* pBackBuffer = (byte*)Bitmap.BackBuffer;
                int size = Bitmap.BackBufferStride * Bitmap.PixelHeight;
                System.Runtime.CompilerServices.Unsafe.InitBlock(pBackBuffer, 0, (uint)size);
                foreach (var thread in solver.threads)
                {
                    for (int i = 0; i < thread.real.Length; i++)
                    {
                        if (thread.real[i] != FastDurandKernerHelperNoComplex.ErrorMarker)
                            AddGlyph(pBackBuffer, thread.pixel_x[i], thread.pixel_y[i], rootMarkerInt, thread.color_r[i], thread.color_g[i], thread.color_b[i], intensityInt);
                    }
                };

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
                    if (x >= 0 && y >= 0 && x < Width && y < Height)
                    {
                        int coord = y * Width + x << 2;
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

            Pixels[coord + 3] = 255;
        }

        private void AddGlyph(int cx, int cy, double[,] map, System.Windows.Media.Color color, double intensity = 1.0, bool overwrite = false)
        {
            AddGlyph(cx, cy, map, color.R, color.G, color.B, intensity, overwrite);
        }

        private void AddGlyph(int cx, int cy, double[,] map, int r, int g, int b, double intensity = 1.0, bool overwrite = false)
        {
            for (int mx = 0; mx < map.GetLength(0); mx++)
            {
                for (int my = 0; my < map.GetLength(1); my++)
                {
                    var strength = map[mx, my];
                    int x = cx - map.GetLength(0) / 2 + mx;
                    int y = cy - map.GetLength(1) / 2 + my;
                    if (x >= 0 && y >= 0 && x < Width && y < Height)
                    {
                        int coord = y * Width + x << 2;
                        var cr = (int)System.Math.Round(r * strength * intensity);
                        var cg = (int)System.Math.Round(g * strength * intensity);
                        var cb = (int)System.Math.Round(b * strength * intensity);
                        AddPixel(coord, cr, cg, cb, overwrite);
                    }
                }
            }
        }

        private void AddPixel(int coord, int r, int g, int b, bool overwrite = false)
        {
            if (overwrite)
            {
                if (b != 0 || g != 0 || r != 0)
                {
                    Pixels[coord + 0] = (byte)b;
                    Pixels[coord + 1] = (byte)g;
                    Pixels[coord + 2] = (byte)r;
                }
            }
            else
            {
                Pixels[coord + 0] = Blend(Pixels[coord + 0], b);
                Pixels[coord + 1] = Blend(Pixels[coord + 1], g);
                Pixels[coord + 2] = Blend(Pixels[coord + 2], r);
            }

            Pixels[coord + 3] = 255;
        }

        private byte Blend(int src, int dst)
        {
            int res = (246*(src + dst)) >> 8;
            if (res > 255)
                res = 255;
            return (byte)res;
        }

        private byte[] ClearRasterImage()
        {
            byte[] pixels1d = new byte[Width * Height * 4];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    pixels1d[(y * Width + x) * 4 + 0] = 0;
                    pixels1d[(y * Width + x) * 4 + 1] = 0;
                    pixels1d[(y * Width + x) * 4 + 2] = 0;
                    pixels1d[(y * Width + x) * 4 + 3] = 255;
                }
            }

            Int32Rect rect = new Int32Rect(0, 0, Width, Height);
            Bitmap.WritePixels(rect, pixels1d, 4 * Width, 0);
            return pixels1d;
        }

        public void SaveToFile(string fileName)
        {
            using (FileStream stream = new FileStream(fileName, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(Bitmap));
                encoder.Save(stream);
            }
        }

     }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MathNet.Numerics;
using PolyFract.Math;
using static System.Formats.Asn1.AsnWriter;

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


        public RasterScene(Panel placeholder)
        {
            CreateImage(placeholder);
        }

        public void Reset(Panel placeholder)
        {
            placeholder.Children.Remove(Image);
            Image.ReleaseMouseCapture();
            CreateImage(placeholder);
        }

        public void CreateImage(Panel placeholder)
        {
            var img = new Image();
            img.Width = placeholder.Width;
            img.Height = placeholder.Height;
            img.Visibility = Visibility.Visible;
            placeholder.Children.Add(img);
            Image = img;

            Width = (int)placeholder.Width;
            Height = (int)placeholder.Height;
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
        }

        public (int x, int y) ToPixelCoordinates(Complex x)
        {
            int ix = (int)System.Math.Round(Width / 2 + (x.Real - Origin.Real) * Zoom);
            int iy = (int)System.Math.Round(Height / 2 - (x.Imaginary - Origin.Imaginary) * Zoom);
            return (ix, iy);
        }

        public Complex ToComplexCoordinates(double x, double y)
        {
            return new Complex((x - Width / 2)/Zoom + Origin.Real, (Height / 2 - y) / Zoom + Origin.Imaginary);
        }

        public void Draw(List<SolutionPoint> solutions, Complex[] coefficients)
        {
            this.coefficients = coefficients;
            Array.Fill<byte>(Pixels, 0);
            for (int i=0;  i< solutions.Count; i++)
            {
                var solution = solutions[i];
                var h = 0.5 + solution.angle / (2*System.Math.PI);
                GuiUtil.HsvToRgb(h*360, 1, 1, out var r, out var g, out var b);
                (int x, int y) = ToPixelCoordinates(solution.root);
                AddGlyph(x, y, rootMarker, r, g, b, Intensity);
            }

            foreach (var coef in coefficients)
            {
                (int cx, int cy) = ToPixelCoordinates(coef);
                AddGlyph(cx, cy, coeffMarker, Colors.Red);
            }

            Int32Rect rect = new Int32Rect(0, 0, Width, Height);
            Bitmap.WritePixels(rect, Pixels, Width * 4, 0);
        }

        private void AddGlyph(int cx, int cy, double[,] map, System.Windows.Media.Color color, double intensity = 1.0)
        {
            AddGlyph(cx, cy, map, color.R, color.G, color.B, intensity);
        }

        private void AddGlyph(int cx, int cy, double[,] map, int r, int g, int b, double intensity = 1.0)
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
                        AddPixel(coord, cr, cg, cb);
                    }
                }
            }
        }

        private void AddPixel(int coord, System.Windows.Media.Color color)
        {
            AddPixel(coord, color.R, color.G, color.B);
        }

        private void AddPixel(int coord, int r, int g, int b)
        {
            Pixels[coord + 0] = AddWithClamp(Pixels[coord + 0], b);
            Pixels[coord + 1] = AddWithClamp(Pixels[coord + 1], g);
            Pixels[coord + 2] = AddWithClamp(Pixels[coord + 2], r);
            Pixels[coord + 3] = 255;
        }

        private byte AddWithClamp(int c1, int c2)
        {
            var res = c1 + c2;
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

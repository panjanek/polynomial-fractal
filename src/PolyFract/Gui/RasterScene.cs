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
        public int MarkerRadius = 5;
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

        double[,] marker = new double[,]
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

            double zoomRatio = 1.0 + 0.0005 * e.Delta;

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

                r = (int)System.Math.Round(r * Intensity);
                g = (int)System.Math.Round(g * Intensity);
                b = (int)System.Math.Round(b * Intensity);

                int r2 = r / 2;
                int g2 = g / 2;
                int b2 = b / 2;

                (int x, int y) = ToPixelCoordinates(solution.root);
                if (x >= 1 && y >= 1 && x < Width-1 && y < Height-1)
                {
                    int coord = y * Width + x << 2;
                    AddPixel(coord, r, g, b);
                    AddPixel(coord - 4, r2, g2, b2);
                    AddPixel(coord + 4, r2, g2, b2);
                    AddPixel(coord - Width * 4, r2, g2, b2);
                    AddPixel(coord + Width * 4, r2, g2, b2);
                }
            }

            foreach (var coef in coefficients)
            {
                (int cx, int cy) = ToPixelCoordinates(coef);
                for(int mx=0;mx<marker.GetLength(0); mx++)
                {
                    for(int my=0; my<marker.GetLength(1); my++)
                    {
                        var strength = marker[mx, my]; 
                        int x = cx - marker.GetLength(0) / 2 + mx;
                        int y = cy - marker.GetLength(1) / 2 + my;
                        if (x >= 2 && y >= 2 && x < Width - 2 && y < Height - 2)
                        {
                            int coord = y * Width + x << 2;
                            var color = System.Windows.Media.Colors.Red;

                            var r = (int)System.Math.Round(color.R * strength);
                            var g = (int)System.Math.Round(color.G * strength);
                            var b = (int)System.Math.Round(color.B * strength);
                            AddPixel(coord, r,g,b);
                        }
                    }
                }
            }

            Int32Rect rect = new Int32Rect(0, 0, Width, Height);
            Bitmap.WritePixels(rect, Pixels, Width * 4, 0);
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

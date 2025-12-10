using System.IO;
using System.Numerics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PolyFract.Maths;
using Panel = System.Windows.Controls.Panel;

namespace PolyFract.Gui
{
    public class PointCloudRenderer
    {
        public const int MarkerRadius = 5;

        public const double ZoomingSpeed = 0.0002;

        public double Intensity { get; set; } = 0.5;

        public Action DraggedOrZoommed { get; set; }

        public Action<int, Complex> CoefficientChanged;

        public Complex Origin { get; private set; } = Complex.Zero;
        public double Zoom { get; private set; } = MainWindow.DefaultZoom;

        public int FrameCounter => surface?.FrameCounter ?? 0;

        private readonly ISurface surface;

        private int? coefficientDragged = null;

        private Complex[] coefficients = [];

        private readonly Panel placeholder;

        public PointCloudRenderer(Panel placeholder)
        {
            this.placeholder = placeholder;
            //surface = new WpfSurface(placeholder);
            surface = new OpenGlSurface(placeholder);
            placeholder.SizeChanged += Placeholder_SizeChanged;
            var dragging = new DraggingHandler(placeholder, mouse =>
            {
                for (int i=0; i<coefficients.Length; i++)
                {
                    var coeff = coefficients[i];
                    (var markerX, var markerY) = ToPixelCoordinates(coeff);
                    if (MathUtil.IsInSquare(mouse.X, mouse.Y, markerX, markerY, MarkerRadius))
                    {
                        coefficientDragged = i;
                        return true;
                    }
                }

                coefficientDragged = null;
                if (DraggedOrZoommed != null)
                    DraggedOrZoommed();
                return true;

            }, (prev, curr) =>
            {
                var delta = new Complex((curr.X - prev.X) / Zoom, -(curr.Y - prev.Y) / Zoom);
                if (coefficientDragged.HasValue)
                {
                    if (CoefficientChanged != null)
                        CoefficientChanged(coefficientDragged.Value, delta);
                }
                else
                {
                    SetProjection(Origin - delta, Zoom);
                }
            });

            placeholder.MouseWheel += Image_MouseWheel;
        }

        private (int x, int y) ToPixelCoordinates(Complex x)
        {
            int ix = (int)System.Math.Round(placeholder.ActualWidth / 2 + (x.Real - Origin.Real) * Zoom);
            int iy = (int)System.Math.Round(placeholder.ActualHeight / 2 - (x.Imaginary - Origin.Imaginary) * Zoom);
            return (ix, iy);
        }

        private void Placeholder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.PreviousSize.Width > 0 && e.NewSize.Width > 0)
            {
                var zoomCorrection = e.NewSize.Width / e.PreviousSize.Width;
                Zoom *= zoomCorrection;
                surface.SetProjection(Origin, Zoom);
            }

            surface.SizeChanged();
        }

        private void Image_MouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(placeholder);

            double zoomRatio = 1.0 + ZoomingSpeed * e.Delta;

            var topLeft1 = GuiUtil.ToComplexCoordinates(0, 0, (int)placeholder.ActualWidth, (int)placeholder.ActualHeight, Origin, Zoom);
            var bottomRight1 = GuiUtil.ToComplexCoordinates(placeholder.ActualWidth, placeholder.ActualHeight, (int)placeholder.ActualWidth, (int)placeholder.ActualHeight, Origin, Zoom);
            var zoomCenter = GuiUtil.ToComplexCoordinates(pos.X, pos.Y, (int)placeholder.ActualWidth, (int)placeholder.ActualHeight, Origin, Zoom);

            var currentSize = bottomRight1 - topLeft1;
            var newSize = currentSize / zoomRatio;

            var c = zoomCenter - topLeft1;
            var b = c / zoomRatio;

            var topLeft2 = zoomCenter - b; 
            var bottomRight2 = topLeft2 + newSize;  

            var newOrigin = (bottomRight2 + topLeft2) / 2;
            var newZoom = Zoom * zoomRatio;

            SetProjection(newOrigin, newZoom);
            if (DraggedOrZoommed != null)
                DraggedOrZoommed();
        }

        public void SetProjection(Complex origin, double zoom)
        {
            Origin = origin;
            Zoom = zoom;
            surface.SetProjection(origin, zoom);
        }

        public void Draw(Solver solver, Complex[] coefficients)
        {
            this.coefficients = coefficients;
            surface.Draw(solver, coefficients, Intensity);
        }

        public void SaveToFile(string fileName) =>  surface.SaveToFile(fileName);
     }
}

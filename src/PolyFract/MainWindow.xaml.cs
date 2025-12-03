using System;
using System.Data;
using System.Numerics;
using System.Resources.Extensions;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using PolyFract.Gui;
using PolyFract.Math;
using static System.Formats.Asn1.AsnWriter;

namespace PolyFract
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int MaxPixelCount = 10000000;

        public const double DefaultZoom = 300;

        private RasterScene scene;

        private DispatcherTimer graphicsTimer = new DispatcherTimer();

        private DispatcherTimer infoTimer = new DispatcherTimer();

        private Complex[] coefficients = [];

        private int order = 4;

        private long frameCount;

        private double dt = 0.0005;

        private double t = 0;

        private int? draggedCoeffIdx;

        //ffmpeg -f image2 -i frame_%05d.png -r 60 -vcodec libx264 -pix_fmt yuv420p out.mp4 -y
        //private string dir = "C:/tmp/poly-fract/slow2-vert";
        private string recordingDir = "";

        //add audio: ffmpeg -i slow2.mp4 -ss 7 -i tetsuo.mp3 -t 220 -c copy -map 0:v:0 -map 1:a:0 slow2-audio.mp4

        private DateTime lastCheckTime;

        private long lastCheckFrameCount;

        private PolyFractContextMenu contextMenu;

        private DraggingHandler coefficientsDragging;

        private Preset currentPreset;

        public MainWindow()
        {
            InitializeComponent();
            scene = new RasterScene(placeholder);

            frameCount = 0;
            lastCheckTime = DateTime.Now;
            KeyDown += MainWindow_KeyDown;

            AttachCoefficiensDragging();

            contextMenu = new PolyFractContextMenu(placeholder);
            contextMenu.OrderChanged = newOrder =>
            {
                var pixelsCount = MathUtil.IntegerPower(coefficients.Length, newOrder);
                if (pixelsCount > MainWindow.MaxPixelCount)
                {
                    MessageBox.Show($"Too many pixels: {pixelsCount}. Maximum is {MainWindow.MaxPixelCount}");
                    return;
                }

                order = newOrder;
                contextMenu.UpdateMenuHeaders(coefficients.Length, scene.Intensity, order, currentPreset?.Name);
            };

            contextMenu.IntensityChanged = newIntensity =>
            {
                scene.Intensity = newIntensity;
                contextMenu.UpdateMenuHeaders(coefficients.Length, scene.Intensity, order, currentPreset?.Name);
            };

            contextMenu.OrientationChanged = isVertical =>
            {
                scene.Reset(placeholder);
                AttachCoefficiensDragging();
            };

            contextMenu.CoefficientCountChanged = newCoefficientCount => {
                var pixelsCount = MathUtil.IntegerPower(newCoefficientCount, order);
                if (pixelsCount > MaxPixelCount)
                {
                    MessageBox.Show($"Too many pixels: {pixelsCount}. Maximum is {MaxPixelCount}");
                    return;
                }

                Complex[] newCoeff = new Complex[newCoefficientCount];
                for (int i = 0; i < newCoeff.Length; i++)
                {
                    if (i < coefficients.Length)
                        newCoeff[i] = coefficients[i];
                    else
                        newCoeff[i] = Complex.FromPolarCoordinates(0.8, i * 0.2);
                }

                coefficients = newCoeff;
                contextMenu.UpdateMenuHeaders(coefficients.Length, scene.Intensity, order, currentPreset?.Name);
            };

            contextMenu.Reset = () => SetDefaultValues();

            contextMenu.CopyPosClicked = () =>
            {
                var pos = Mouse.GetPosition(scene.Image);
                var complex = scene.ToComplexCoordinates(pos.X, pos.Y);
                string str = $"new PointOfView(new Complex({complex.Real},{complex.Imaginary}), {scene.Zoom.ToString("0.0")}, {t.ToString("0.0000")}),";
                System.Windows.Clipboard.SetText(str);
            };

            contextMenu.PresetSelected = (p, recDir) =>
            {
                ApplyPreset(p, true);
                contextMenu.UpdateMenuHeaders(coefficients.Length, scene.Intensity, order, currentPreset?.Name);
                recordingDir = recDir;
            };

            contextMenu.SaveCapture = captFileName => scene.SaveToFile(captFileName);


            SetDefaultValues();

            graphicsTimer.Interval = TimeSpan.FromSeconds(0.001);
            graphicsTimer.Tick += GraphicsTimerTick;
            graphicsTimer.Start();

            infoTimer.Interval = TimeSpan.FromSeconds(1.0);
            infoTimer.Tick += InfoTimer_Tick;
            infoTimer.Start();
        }

        private void GraphicsTimerTick(object sender, EventArgs e)
        {
            if (!contextMenu.menuPaused.IsChecked)
            {
                t += dt;
                if (contextMenu.menuAutoPOV.IsChecked)
                    AutoPointOfViewMove();

                if (contextMenu.menuAutoCoeff.IsChecked)
                    AutoCoefficientsChange();

                if (!string.IsNullOrWhiteSpace(recordingDir))
                    scene.SaveToFile($"{recordingDir}/frame_{(frameCount.ToString("00000"))}.png");

                frameCount++;
            }

            var solutions = Solver.SolveAll(coefficients, order);
            scene.Draw(solutions, contextMenu.menuShowCoeff.IsChecked ? coefficients : []);
        }

        private void AttachCoefficiensDragging()
        {
            coefficientsDragging = new DraggingHandler(scene.Image, (mouse) =>
            {
                for (int i = 0; i < coefficients.Length; i++)
                {
                    var coeff = coefficients[i];
                    (var markerX, var markerY) = scene.ToPixelCoordinates(coeff);
                    if (MathUtil.IsInSquare(mouse.X, mouse.Y, markerX, markerY, scene.MarkerRadius))
                    {
                        draggedCoeffIdx = i;
                        return true;
                    }
                }

                return false;
            }, (prev, curr) =>
            {
                var delta = new Complex((curr.X - prev.X) / scene.Zoom, -(curr.Y - prev.Y) / scene.Zoom);
                coefficients[draggedCoeffIdx.Value] += delta;
            });
        }

        private void ApplyPreset(Preset preset, bool start)
        {
            currentPreset = preset;
            order = preset.Order;
            dt = preset.DT;
            AutoCoefficientsChange();
            if (start)
            {
                t = 0;
                frameCount = 0;
                contextMenu.menuPaused.IsChecked = false;
                contextMenu.menuAutoCoeff.IsChecked = true;
                contextMenu.menuAutoPOV.IsChecked = true;
                contextMenu.UpdateMenuHeaders(coefficients.Length, scene.Intensity, order, currentPreset?.Name);
            }
        }

        private void SetDefaultValues()
        {
            ApplyPreset(Preset.GetPresets().First(), false);
            coefficients = [new Complex(-1, 0), new Complex(1, 0)];
            contextMenu.menuPaused.IsChecked = false;
            contextMenu.menuAutoCoeff.IsChecked = false;
            contextMenu.menuAutoPOV.IsChecked = false;
            contextMenu.menuShowCoeff.IsChecked = true;
            scene.Intensity = 1.0;
            scene.Origin = Complex.Zero;
            scene.Zoom = DefaultZoom;
            recordingDir = null;
            contextMenu.UpdateMenuHeaders(coefficients.Length, scene.Intensity, order, currentPreset?.Name);
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.OemPlus)
                dt = dt*1.1;
            if (e.Key == Key.OemMinus)
                dt = dt*0.9;
            if (e.Key == Key.Space)
                contextMenu.menuPaused.IsChecked = !contextMenu.menuPaused.IsChecked;
        }

        private void InfoTimer_Tick(object sender, EventArgs e)
        {
            var timespan = DateTime.Now - lastCheckTime;
            double frames = frameCount - lastCheckFrameCount;
            if (timespan.TotalSeconds >= 0.0001)
            {
                double fps = frames / timespan.TotalSeconds;
                var pos = Mouse.GetPosition(scene.Image);
                var mouseComplex = scene.ToComplexCoordinates(pos.X, pos.Y);
                var mouseStr = $"r:{mouseComplex.Real.ToString("0.0000")},i:{mouseComplex.Imaginary.ToString("0.0000")}";
                var originStr = $"r={scene.Origin.Real.ToString("0.0000")},i={scene.Origin.Imaginary.ToString("0.0000")}";
                Title = $"PolyFract. {(contextMenu.menuPaused.IsChecked ? "[pause] " : "")} pixels:{MathUtil.IntegerPower(coefficients.Length, order)} t:{t.ToString("0.000")} frameCount:{frameCount} fps:{fps.ToString("0.00")} mouse:({mouseStr}) origin:({originStr}) zoom:{scene.Zoom.ToString("0.00")} {(string.IsNullOrWhiteSpace(recordingDir) ? "" : $"recording to:{recordingDir}")}";
            }
        }

        private void AutoCoefficientsChange()
        {
            coefficients = currentPreset.GetCoefficients(t);
        }

        private void AutoPointOfViewMove()
        {
            if (currentPreset.POVMovement !=null && currentPreset.POVMovement.Count > 2)
            {
                var currentPOV = currentPreset.GetInterpolatedPointOFView(t);
                scene.Origin = currentPOV.Origin;
                scene.Zoom = currentPOV.Zoom;
            }
        }
    }
}
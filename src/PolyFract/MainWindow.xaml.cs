using System;
using System.Data;
using System.Linq.Expressions;
using System.Numerics;
using System.Resources.Extensions;
using System.Text;
using System.Text.RegularExpressions;
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
using PolyFract.Maths;
using PolyFract.Presets;
using static System.Formats.Asn1.AsnWriter;

// TODO:
// - presets: 3coeff + fractals,

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

        //combine PNGs into video: ffmpeg -f image2 -framerate 60 -i frame_%05d.png -r 60 -vcodec libx264 -pix_fmt yuv420p out.mp4 -y
        //cut: ffmpeg -ss 00:00:50 -t 00:00:50 -i vert60.mp4 -c copy tiktok.mp4
        //add audio: ffmpeg -i slow2.mp4 -ss 7 -i tetsuo.mp3 -t 220 -c copy -map 0:v:0 -map 1:a:0 slow2-audio.mp4
        private string recordingDir = "";

        private DateTime lastCheckTime;

        private long lastCheckFrameCount;

        private PolyFractContextMenu contextMenu;

        private DraggingHandler coefficientsDragging;

        private BasePreset currentPreset;

        private List<RedoItem> redo = [];

        private FullscreenWindow fullscreen = null;

        private Solver solver = null;

        public MainWindow()
        {
            InitializeComponent();

            scene = new RasterScene(placeholder);
            scene.DraggedOrZoommed = () => contextMenu.menuAutoPOV.IsChecked = false;
            AttachCoefficiensDragging();

            frameCount = 0;
            lastCheckTime = DateTime.Now;
            KeyDown += MainWindow_KeyDown;

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
                UpdateContextMenu();
            };

            contextMenu.IntensityChanged = newIntensity =>
            {
                scene.Intensity = newIntensity;
                UpdateContextMenu();
            };

            contextMenu.OrientationChanged = isVertical =>
            {
                scene.Reset(placeholder);
            };

            contextMenu.CoefficientCountChanged = newCoefficientCount => {
                var pixelsCount = MathUtil.IntegerPower(newCoefficientCount, order);
                if (pixelsCount > MaxPixelCount)
                {
                    MessageBox.Show($"Too many pixels: {pixelsCount}. Maximum is {MaxPixelCount}");
                    return;
                }

                ChangeCoefficientsCount(newCoefficientCount);
            };

            contextMenu.Reset = () => SetDefaultValues();
            contextMenu.CopyPosClicked = () => CopyCoordinatesToClipboard(contextMenu.LastRightClick);
            contextMenu.ToggleRecording = recordingDir => this.recordingDir = recordingDir;
            contextMenu.SaveCapture = captFileName => scene.SaveToFile(captFileName);
            contextMenu.PresetSelected = (p, recDir) =>
            {
                ApplyPreset(p);
                UpdateContextMenu();
                recordingDir = recDir;
            };

            
            SetDefaultValues();
            graphicsTimer.Interval = TimeSpan.FromSeconds(0.001);
            graphicsTimer.Tick += GraphicsTimerTick;
            graphicsTimer.Start();

            infoTimer.Interval = TimeSpan.FromSeconds(1.0);
            infoTimer.Tick += InfoTimer_Tick;
            infoTimer.Start();

            placeholder.SizeChanged += Placeholder_SizeChanged;
        }

        private void Placeholder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            scene.Reset(placeholder);
            AttachCoefficiensDragging();
            if (e.PreviousSize.Width > 0 && e.NewSize.Width > 0)
            {
                var zoomCorrection = e.NewSize.Width / e.PreviousSize.Width;
                scene.Zoom *= zoomCorrection;
            }
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

            if (solver == null || solver.coefficientsValuesCount != coefficients.Length || solver.order != order)
                solver = new Solver(coefficients.Length, order);

            solver.Solve(coefficients);
            scene.FastDraw(solver.real, solver.imaginary, solver.angle, contextMenu.menuShowCoeff.IsChecked ? coefficients : []);
        }

        private void CopyCoordinatesToClipboard(Point clikPoint)
        {
            var clickedOrigin = scene.ToComplexCoordinates(clikPoint.X, clikPoint.Y);
            string timeStr = t.ToString("0.0000");
            string zoomStr = scene.Zoom.ToString("0.0");
            string originStr = $"new Complex({clickedOrigin.Real}, {clickedOrigin.Imaginary})";
            string coeffsStr = string.Join(", ", coefficients.Select(c => $"new Complex({c.Real}, {c.Imaginary})"));
            string clipboard = $"AddTimePoint({timeStr}, {originStr}, {zoomStr}, [{coeffsStr}]);";
            System.Windows.Clipboard.SetText(clipboard);
            redo.Add(new RedoItem() { Coeffs = coefficients.ToArray(), Pov = new PointOfView(scene.Origin, scene.Zoom, t) });
        }

        private void ChangeCoefficientsCount(int newCoefficientCount)
        {
            Complex[] newCoeff = new Complex[newCoefficientCount];
            for (int i = 0; i < newCoeff.Length; i++)
            {
                if (i < coefficients.Length)
                    newCoeff[i] = coefficients[i];
                else
                    newCoeff[i] = Complex.FromPolarCoordinates(0.8, (i * 2 * System.Math.PI / 2) / newCoefficientCount);
            }

            coefficients = newCoeff;
            UpdateContextMenu();
        }

        private void AttachCoefficiensDragging()
        {
            coefficientsDragging = new DraggingHandler(scene.Image, (mouse) =>
            {
                for (int i = 0; i < coefficients.Length; i++)
                {
                    var coeff = coefficients[i];
                    (var markerX, var markerY) = scene.ToPixelCoordinates(coeff);
                    if (MathUtil.IsInSquare(mouse.X, mouse.Y, markerX, markerY, RasterScene.MarkerRadius))
                    {
                        draggedCoeffIdx = i;
                        contextMenu.menuAutoCoeff.IsChecked = false;
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

        private void ApplyPreset(BasePreset preset)
        {
            currentPreset = preset;
            order = preset.Order;
            dt = preset.DT;
            scene.Intensity = preset.Intensity;
            AutoCoefficientsChange();

            t = 0;
            frameCount = 0;
            contextMenu.menuPaused.IsChecked = false;
            contextMenu.menuAutoCoeff.IsChecked = true;
            contextMenu.menuAutoPOV.IsChecked = true;
            contextMenu.menuPaused.IsChecked = false;
            UpdateContextMenu();
        }

        private void SetDefaultValues()
        {
            ApplyPreset(BasePreset.AllPresets[0]);
            coefficients = [new Complex(-1, 0), new Complex(1, 0)];
            contextMenu.menuPaused.IsChecked = false;
            contextMenu.menuShowCoeff.IsChecked = true;
            scene.Intensity = 1.0;
            scene.Origin = Complex.Zero;
            scene.Zoom = DefaultZoom;
            recordingDir = null;
            UpdateContextMenu();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    contextMenu.menuPaused.IsChecked = !contextMenu.menuPaused.IsChecked;
                    break;
                case Key.OemMinus:
                    dt = dt * 0.9; 
                    break;
                case Key.OemPlus:
                    dt = dt * 1.1; 
                    break;
                case Key.Q:
                    if (order > 2)
                        order--;
                    break;
                case Key.W:
                    if (MathUtil.IntegerPower(coefficients.Length, order + 1) < MaxPixelCount)
                        order++;
                    break;
                case Key.A:
                    if (coefficients.Length > 2)
                        ChangeCoefficientsCount(coefficients.Length - 1); 
                    break;
                case Key.S:
                    if (MathUtil.IntegerPower(coefficients.Length + 1, order) < MaxPixelCount)
                        ChangeCoefficientsCount(coefficients.Length + 1);
                    break;
                case Key.C:
                    CopyCoordinatesToClipboard(Mouse.GetPosition(scene.Image));
                    break;
                case Key.O:
                    if (scene.Intensity > 0.01)
                        scene.Intensity -= 0.01;
                    break;
                case Key.P:
                    if (scene.Intensity < 1.0)
                        scene.Intensity += 0.01;
                    break;
                case Key.D1: 
                    ApplyPreset(BasePreset.AllPresets[0]); 
                    break;
                case Key.D2: 
                    ApplyPreset(BasePreset.AllPresets[1]); 
                    break;
                case Key.D3: 
                    ApplyPreset(BasePreset.AllPresets[2]); 
                    break;
                case Key.D4: 
                    ApplyPreset(BasePreset.AllPresets[3]); 
                    break;
                case Key.D5: 
                    ApplyPreset(BasePreset.AllPresets[4]); 
                    break;
                case Key.Z:
                    if (redo.Count > 0)
                    {
                        var last = redo.Last();
                        redo = redo.Take(redo.Count - 1).ToList();
                        scene.Origin = last.Pov.Origin;
                        scene.Zoom = last.Pov.Zoom;
                        coefficients = last.Coeffs;
                        t = last.Pov.Time;
                    }
                    break;
                case Key.F:
                    ToggleFullscreen();
                    break;
                case Key.Escape:
                    if (fullscreen != null)
                        ToggleFullscreen();
                    break;
            }

            UpdateContextMenu();
        }

        private void ToggleFullscreen()
        {
            if (fullscreen == null)
            {
                parent.Children.Remove(placeholder);
                fullscreen = new FullscreenWindow();
                fullscreen.KeyDown += MainWindow_KeyDown;
                fullscreen.ContentHost.Content = placeholder;
                scene.Reset(placeholder);
                fullscreen.ShowDialog();

            }
            else
            {
                fullscreen.ContentHost.Content = null;
                parent.Children.Add(placeholder);
                fullscreen.Close();
                fullscreen = null;
                scene.Reset(placeholder);
            }
        }

        private void InfoTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var timespan = now - lastCheckTime;
            double frames = frameCount - lastCheckFrameCount;
            if (timespan.TotalSeconds >= 0.0001)
            {
                double fps = frames / timespan.TotalSeconds;
                var pos = Mouse.GetPosition(scene.Image);
                var mouseComplex = scene.ToComplexCoordinates(pos.X, pos.Y);
                var mouseStr = $"r:{mouseComplex.Real.ToString("0.0000")},i:{mouseComplex.Imaginary.ToString("0.0000")}";
                var originStr = $"r={scene.Origin.Real.ToString("0.0000")},i={scene.Origin.Imaginary.ToString("0.0000")}";
                Title = $"PolyFract. " +
                        $"{(contextMenu.menuPaused.IsChecked ? "[pause] " : "")} " +
                        $"pixels:{MathUtil.IntegerPower(coefficients.Length, order) * order} " +
                        $"polys:{MathUtil.IntegerPower(coefficients.Length, order)} " +
                        $"t:{t.ToString("0.000")} " +
                        $"frameCount:{frameCount} " +
                        $"fps:{fps.ToString("0.00")} " +
                        $"mouse:({mouseStr}) origin:({originStr}) " +
                        $"zoom:{scene.Zoom.ToString("0.00")} " +
                        $"intensity:{(scene.Intensity*100).ToString("0.0")}% " +
                        $"{(string.IsNullOrWhiteSpace(recordingDir) ? "" : $"recording to:{recordingDir}")} " +
                        $"order: {order} " +
                        $"coeffsCount: {coefficients.Length}";
            }

            lastCheckFrameCount = frameCount;
            lastCheckTime = now;
        }

        private void AutoCoefficientsChange()
        {
            var newCoeff = currentPreset.GetCoefficients(t);
                coefficients = newCoeff;
        }

        private void AutoPointOfViewMove()
        {
            var newPOV = currentPreset.GetPOV(t);
            if (newPOV != null)
            {
                scene.Origin = newPOV.Origin;
                scene.Zoom = newPOV.Zoom;
            }
        }

        private void UpdateContextMenu() => contextMenu.UpdateContextMenu(coefficients.Length, scene.Intensity, order, currentPreset);
    }

    public class RedoItem
    {
        public PointOfView Pov { get; set; }

        public Complex[] Coeffs { get; set; }
    }
}
using System.Data;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PolyFract.Gui;
using PolyFract.Maths;
using PolyFract.Presets;

// TODO:
// - directx

namespace PolyFract
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int MaxPixelCount = 10000000;

        public const double DefaultZoom = 300;

        private PointCloudRenderer renderer;

        private DispatcherTimer infoTimer = new DispatcherTimer();

        private Complex[] coefficients = [];

        private int order = 4;

        private long frameCount;

        private double dt = 0.0005;

        public DateTime tStart = DateTime.Now;

        private int? draggedCoeffIdx;

        //combine PNGs into video: ffmpeg -f image2 -framerate 60 -i frame_%05d.png -r 60 -vcodec libx264 -pix_fmt yuv420p out.mp4 -y
        //cut: ffmpeg -ss 00:00:50 -t 00:00:50 -i vert60.mp4 -c copy tiktok.mp4
        //add audio: ffmpeg -i slow2.mp4 -ss 7 -i tetsuo.mp3 -t 220 -c copy -map 0:v:0 -map 1:a:0 slow2-audio.mp4
        private string recordingDir = "";

        private DateTime lastCheckTime;

        private long lastCheckFrameCount;

        private PolyFractContextMenu contextMenu;

        private BasePreset currentPreset;

        private List<RedoItem> redo = [];

        private FullscreenWindow fullscreen = null;

        private Solver solver = null;

        private volatile bool uiPending = false;

        public MainWindow()
        {
            InitializeComponent();
            Polynomials.InitNative();

            renderer = new PointCloudRenderer(placeholder);
            renderer.DraggedOrZoommed = () => { contextMenu.menuAutoPOV.IsChecked = false; UpdateContextMenu(); };
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
                renderer.Intensity = newIntensity;
                UpdateContextMenu();
            };

            contextMenu.OrientationChanged = isVertical =>
            {
                renderer.Reset(placeholder);
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
            contextMenu.SaveCapture = captFileName => renderer.SaveToFile(captFileName);
            contextMenu.PresetSelected = (p, recDir) =>
            {
                ApplyPreset(p);
                UpdateContextMenu();
                recordingDir = recDir;
            };

            
            SetDefaultValues();
            placeholder.SizeChanged += Placeholder_SizeChanged;

            infoTimer.Interval = TimeSpan.FromSeconds(1.0);
            infoTimer.Tick += InfoTimer_Tick;
            infoTimer.Start();           
            
            Thread renderThread = new Thread(() =>
            {
                Worker();
            });
            renderThread.IsBackground = true;
            renderThread.Start();
        }

        private void Worker()
        {
            while (true)
            {
                if (!contextMenu.Paused)
                {
                    var t = GetTime();

                    if (contextMenu.AutoPOV)
                        AutoPointOfViewMove();

                    if (contextMenu.AutoCoeff)
                        AutoCoefficientsChange();
                }

                if (solver == null || solver.coefficientsValuesCount != coefficients.Length || solver.order != order)
                    solver = new Solver(coefficients.Length, order);
                solver.Solve(coefficients);

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
                                    if (renderer != null && solver != null)
                                    {
                                        renderer.Draw(solver, contextMenu.menuShowCoeff.IsChecked ? coefficients : []);
                                        frameCount++;
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
                            } ));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                
            }
        }

        private void Placeholder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            renderer.Reset(placeholder);
            AttachCoefficiensDragging();
            if (e.PreviousSize.Width > 0 && e.NewSize.Width > 0)
            {
                var zoomCorrection = e.NewSize.Width / e.PreviousSize.Width;
                renderer.Zoom *= zoomCorrection;
            }
        }

        private void ResetTime()
        {
            tStart = DateTime.Now;
        }

        private double GetTime()
        {
            return (DateTime.Now - tStart).TotalSeconds * dt;
        }

        private void CopyCoordinatesToClipboard(Point clikPoint)
        {
            var clickedOrigin = renderer.ToComplexCoordinates(clikPoint.X, clikPoint.Y);
            string timeStr = GetTime().ToString("0.0000");
            string zoomStr = renderer.Zoom.ToString("0.0");
            string originStr = $"new Complex({clickedOrigin.Real}, {clickedOrigin.Imaginary})";
            string coeffsStr = string.Join(", ", coefficients.Select(c => $"new Complex({c.Real}, {c.Imaginary})"));
            string clipboard = $"AddTimePoint({timeStr}, {originStr}, {zoomStr}, [{coeffsStr}]);";
            System.Windows.Clipboard.SetText(clipboard);
            redo.Add(new RedoItem() { Coeffs = coefficients.ToArray(), Pov = new PointOfView(renderer.Origin, renderer.Zoom, GetTime()) });
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
            var coefficientsDragging = new DraggingHandler(renderer.Image, (mouse) =>
            {
                for (int i = 0; i < coefficients.Length; i++)
                {
                    var coeff = coefficients[i];
                    (var markerX, var markerY) = renderer.ToPixelCoordinates(coeff);
                    if (MathUtil.IsInSquare(mouse.X, mouse.Y, markerX, markerY, PointCloudRenderer.MarkerRadius))
                    {
                        draggedCoeffIdx = i;
                        contextMenu.menuAutoCoeff.IsChecked = false;
                        UpdateContextMenu();
                        return true;
                    }
                }

                return false;
            }, (prev, curr) =>
            {
                var delta = new Complex((curr.X - prev.X) / renderer.Zoom, -(curr.Y - prev.Y) / renderer.Zoom);
                coefficients[draggedCoeffIdx.Value] += delta;
            });
        }

        private void ApplyPreset(BasePreset preset)
        {
            currentPreset = preset;
            order = preset.Order;
            dt = preset.DT;
            renderer.Intensity = preset.Intensity;
            AutoCoefficientsChange();

            ResetTime();
            frameCount = 0;
            contextMenu.menuPaused.IsChecked = false;
            contextMenu.menuAutoCoeff.IsChecked = true;
            contextMenu.menuAutoPOV.IsChecked = true;
            UpdateContextMenu();
        }

        private void SetDefaultValues()
        {
            ApplyPreset(BasePreset.AllPresets[0]);
            coefficients = [new Complex(-1, 0), new Complex(1, 0)];
            contextMenu.menuPaused.IsChecked = false;
            contextMenu.menuShowCoeff.IsChecked = true;
            renderer.Intensity = 1.0;
            renderer.Origin = Complex.Zero;
            renderer.Zoom = DefaultZoom;
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
                    CopyCoordinatesToClipboard(Mouse.GetPosition(renderer.Image));
                    break;
                case Key.O:
                    if (renderer.Intensity > 0.01)
                        renderer.Intensity -= 0.01;
                    break;
                case Key.P:
                    if (renderer.Intensity < 1.0)
                        renderer.Intensity += 0.01;
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
                        renderer.Origin = last.Pov.Origin;
                        renderer.Zoom = last.Pov.Zoom;
                        coefficients = last.Coeffs;
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
                renderer.Reset(placeholder);
                fullscreen.ShowDialog();
            }
            else
            {
                fullscreen.ContentHost.Content = null;
                parent.Children.Add(placeholder);
                fullscreen.Close();
                fullscreen = null;
                renderer.Reset(placeholder);
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
                var pos = Mouse.GetPosition(renderer.Image);
                var mouseComplex = renderer.ToComplexCoordinates(pos.X, pos.Y);
                var mouseStr = $"r:{mouseComplex.Real.ToString("0.0000")},i:{mouseComplex.Imaginary.ToString("0.0000")}";
                var originStr = $"r={renderer.Origin.Real.ToString("0.0000")},i={renderer.Origin.Imaginary.ToString("0.0000")}";
                long pixelsCount = MathUtil.IntegerPower(coefficients.Length, order) * order;
                Title = $"PolyFract. " +
                        $"{(contextMenu.menuPaused.IsChecked ? "[pause] " : "")} " +
                        $"pixels:{pixelsCount} " +
                        $"polys:{MathUtil.IntegerPower(coefficients.Length, order)} " +
                        $"t:{GetTime().ToString("0.000")} " +
                        $"frameCount:{frameCount} " +
                        $"fps:{fps.ToString("0.00")} " +
                        $"mouse:({mouseStr}) origin:({originStr}) " +
                        $"zoom:{renderer.Zoom.ToString("0.00")} " +
                        $"intensity:{(renderer.Intensity*100).ToString("0.0")}% " +
                        $"{(string.IsNullOrWhiteSpace(recordingDir) ? "" : $"recording to:{recordingDir}")} " +
                        $"order: {order} " +
                        $"coeffsCount: {coefficients.Length} "+
                        $"threads: {Environment.ProcessorCount} " +
                        $"errors: {solver?.GetErrorsCount()} ({(100.0 * solver?.GetErrorsCount() / pixelsCount)?.ToString("0.00000")}%) "+
                        $"solver: {(Polynomials.IsNativeLibAvailable ? "[native]" : "[managed]")}";
            }

            lastCheckFrameCount = frameCount;
            lastCheckTime = now;
        }

        private void AutoCoefficientsChange()
        {
            var newCoeff = currentPreset.GetCoefficients(GetTime());
            coefficients = newCoeff;
        }

        private void AutoPointOfViewMove()
        {
            var newPOV = currentPreset.GetPOV(GetTime());
            if (newPOV != null)
            {
                renderer.Origin = newPOV.Origin;
                renderer.Zoom = newPOV.Zoom;
            }
        }

        private void UpdateContextMenu() => contextMenu.UpdateContextMenu(coefficients.Length, renderer.Intensity, order, currentPreset);
    }

    public class RedoItem
    {
        public PointOfView Pov { get; set; }

        public Complex[] Coeffs { get; set; }
    }
}
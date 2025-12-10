using System.Data;
using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PolyFract.Gui;
using PolyFract.Maths;
using PolyFract.Presets;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;

// TODO:
// - time correction on +/-
// save capture

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

        private Complex[] coefficients = [];

        private int order = 4;

        private double dt = 0.0005;

        private DateTime tStart = DateTime.Now;

        //combine PNGs into video: ffmpeg -f image2 -framerate 60 -i frame_%05d.png -r 60 -vcodec libx264 -pix_fmt yuv420p out.mp4 -y
        //cut: ffmpeg -ss 00:00:50 -t 00:00:50 -i vert60.mp4 -c copy tiktok.mp4
        //add audio: ffmpeg -i slow2.mp4 -ss 7 -i tetsuo.mp3 -t 220 -c copy -map 0:v:0 -map 1:a:0 slow2-audio.mp4
        private string recordingDir = "";

        private DateTime lastCheckTime;

        private long lastCheckFrameCount;

        private int cycleCounter = 0;

        private int lastCheckCycleCounter = 0;

        private PolyFractContextMenu contextMenu;

        private BasePreset currentPreset;

        private FullscreenWindow fullscreen;

        private Solver solver;

        private bool isOccupied;

        public MainWindow()
            : base()
        {
            InitializeComponent();
            Polynomials.InitNative();
        }
        private void parent_Loaded(object sender, RoutedEventArgs e)
        {
            renderer = new PointCloudRenderer(placeholder);
            renderer.DraggedOrZoommed = () => { contextMenu.SetCheckboxes(null, null, false, null); UpdateContextMenu(); };
            renderer.CoefficientChanged = (i, delta) =>
            {
                contextMenu.SetCheckboxes(null, false, null, null);
                UpdateContextMenu();
                coefficients[i] += delta;
            };

            lastCheckTime = DateTime.Now;
            KeyDown += MainWindow_KeyDown;
            contextMenu = new PolyFractContextMenu(placeholder, renderer.MouseEventSource);
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
            DispatcherTimer infoTimer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1.0) };
            infoTimer.Tick += InfoTimer_Tick;
            infoTimer.Start();

            /*
            Thread renderThread = new Thread(() =>
            {
                Worker();
            });
            renderThread.IsBackground = true;
            renderThread.Start();
            */

            var renderTimer = new System.Timers.Timer(15);
            renderTimer.Elapsed += RenderTimer_Elapsed;
            renderTimer.Start();

        }

        private void RenderTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!isOccupied)
            {
                isOccupied = true;
                WorkerStep();
                isOccupied = false;
            }
        }

        private void Worker()
        {
            while (true)
            {
                WorkerStep();
            }
        }

        private void WorkerStep()
        {
            if (!contextMenu.Paused)
            {
                if (contextMenu.AutoPOV)
                {
                    var newPOV = currentPreset.GetPOV(GetTime());
                    if (newPOV != null)
                        renderer.SetProjection(newPOV.Origin, newPOV.Zoom);
                }

                if (contextMenu.AutoCoeff)
                    coefficients = currentPreset.GetCoefficients(GetTime());
            }

            if (solver == null || solver.coefficientsValuesCount != coefficients.Length || solver.order != order)
                solver = new Solver(coefficients.Length, order);

            solver.Solve(coefficients);
            cycleCounter++;

            renderer.Draw(solver, contextMenu.ShowCoeff ? coefficients : []);
        }

        private double GetTime()
        {
            return (DateTime.Now - tStart).TotalSeconds * dt;
        }

        private void CopyCoordinatesToClipboard(Point clikPoint)
        {
            var clickedOrigin = GuiUtil.ToComplexCoordinates(clikPoint.X, clikPoint.Y, (int)placeholder.ActualWidth, (int)placeholder.ActualHeight, renderer.Origin, renderer.Zoom);
            string timeStr = GetTime().ToString("0.0000");
            string zoomStr = renderer.Zoom.ToString("0.0");
            string originStr = $"new Complex({clickedOrigin.Real}, {clickedOrigin.Imaginary})";
            string coeffsStr = string.Join(", ", coefficients.Select(c => $"new Complex({c.Real}, {c.Imaginary})"));
            string clipboard = $"AddTimePoint({timeStr}, {originStr}, {zoomStr}, [{coeffsStr}]);";
            System.Windows.Clipboard.SetText(clipboard);
        }

        private void ChangeCoefficientsCount(int newCoefficientCount)
        {
            Complex[] newCoeff = new Complex[newCoefficientCount];
            for (int i = 0; i < newCoeff.Length; i++)
            {
                if (i < coefficients.Length)
                    newCoeff[i] = coefficients[i];
                else
                    newCoeff[i] = Complex.FromPolarCoordinates(0.5, (i * 2 * System.Math.PI / 2) / newCoefficientCount);
            }

            coefficients = newCoeff;
            UpdateContextMenu();
        }

        private void ApplyPreset(BasePreset preset)
        {
            currentPreset = preset;
            order = preset.Order;
            dt = preset.DT;
            renderer.Intensity = preset.Intensity;
            coefficients = currentPreset.GetCoefficients(GetTime());

            tStart = DateTime.Now;
            contextMenu.SetCheckboxes(null, true, true, false);
            UpdateContextMenu();
        }

        private void SetDefaultValues()
        {
            ApplyPreset(BasePreset.AllPresets[0]);
            coefficients = [new Complex(-1, 0), new Complex(1, 0)];
            contextMenu.SetCheckboxes(true, null, null, null);
            renderer.Intensity = 1.0;
            renderer.SetProjection(Complex.Zero, DefaultZoom);
            recordingDir = null;
            UpdateContextMenu();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    contextMenu.SetCheckboxes(null, null, null, !contextMenu.Paused);
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
                    CopyCoordinatesToClipboard(Mouse.GetPosition(placeholder));
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
                fullscreen.ShowDialog();
            }
            else
            {
                fullscreen.ContentHost.Content = null;
                parent.Children.Add(placeholder);
                fullscreen.Close();
                fullscreen = null;
            }
        }

        private void InfoTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var timespan = now - lastCheckTime;
            double frames = renderer.FrameCounter - lastCheckFrameCount;
            double cycles = cycleCounter - lastCheckCycleCounter;
            if (timespan.TotalSeconds >= 0.0001)
            {
                double fps = frames / timespan.TotalSeconds;
                double cps = cycles / timespan.TotalSeconds;
                var pos = Mouse.GetPosition(placeholder);
                var mouseComplex = GuiUtil.ToComplexCoordinates(pos.X, pos.Y, (int)placeholder.ActualWidth, (int)placeholder.ActualHeight, renderer.Origin, renderer.Zoom);
                var mouseStr = $"r:{mouseComplex.Real.ToString("0.0000")},i:{mouseComplex.Imaginary.ToString("0.0000")}";
                var originStr = $"r={renderer.Origin.Real.ToString("0.0000")},i={renderer.Origin.Imaginary.ToString("0.0000")}";
                long pixelsCount = MathUtil.IntegerPower(coefficients.Length, order) * order;
                Title = $"PolyFract. " +
                        $"{(contextMenu.Paused ? "[pause] " : "")} " +
                        $"pixels:{pixelsCount} " +
                        $"polys:{MathUtil.IntegerPower(coefficients.Length, order)} " +
                        $"t:{GetTime().ToString("0.000")} " +
                        $"frameCount:{renderer.FrameCounter}/{cycleCounter} " +
                        $"fps:{fps.ToString("0.0")}/{cps.ToString("0.0")} " +
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

            lastCheckFrameCount = renderer.FrameCounter;
            lastCheckCycleCounter = cycleCounter;
            lastCheckTime = now;
        }

        private void UpdateContextMenu() => contextMenu.UpdateContextMenu(coefficients.Length, renderer.Intensity, order, currentPreset);
    }
}
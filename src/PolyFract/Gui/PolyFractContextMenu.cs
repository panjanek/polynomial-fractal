using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using Microsoft.WindowsAPICodePack.Dialogs;
using PolyFract.Presets;
using static System.Formats.Asn1.AsnWriter;
using MessageBox = System.Windows.MessageBox;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace PolyFract.Gui
{
    public class PolyFractContextMenu
    {
        public ContextMenu menu;

        private readonly MenuItem menuPreset = new MenuItem { };

        private readonly MenuItem menuCapture = new MenuItem { Header = "Save screen capture (P)" };

        private readonly MenuItem menuRecord = new MenuItem { Header = "Record frames to folder", IsCheckable = true };

        private readonly MenuItem menuPaused = new MenuItem { Header = "Paused (Space)", IsCheckable = true, IsChecked = true };

        private readonly MenuItem menuAutoCoeff = new MenuItem { Header = "Automatic coefficients movement", IsCheckable = true };

        private readonly MenuItem menuAutoPOV = new MenuItem { Header = "Automatic POV movement", IsCheckable = true };

        private readonly MenuItem menuCoeffCount = new MenuItem { };

        private readonly MenuItem menuOrder = new MenuItem { };

        private readonly MenuItem menuSolver = new MenuItem { Header = "Toggle solver (G)" };

        private readonly MenuItem menuShowCoeff = new MenuItem { Header = "Show coefficients markers", IsCheckable = true, IsChecked = true };

        private readonly MenuItem menuReset = new MenuItem { Header = "Reset to defaults" };

        private readonly Panel placeholder;

        public Action<BasePreset, string> PresetSelected { get; set; }

        public Action Reset { get; set; }

        public Action<int> CoefficientCountChanged { get; set; }

        public Action<int> OrderChanged { get; set; }

        public Action<string> SaveCapture { get; set; }

        public Action<string> ToggleRecording { get; set; }

        public Action TogglePaused { get; set; }

        public Point LastRightClick { get; set; }

        public bool AutoPOV { get; private set; }

        public bool AutoCoeff { get; private set; }

        public bool Paused { get; private set; }

        public bool ShowCoeff { get; private set; }

        public PolyFractContextMenu(Panel placeholder, Panel mouseSource)
        {
            this.placeholder = placeholder;
            menu = new ContextMenu();
            menu.Items.Add(menuPreset);
            for (int i=0; i<BasePreset.AllPresets.Length; i++)
            {
                var preset = BasePreset.AllPresets[i];
                var submenuPreset = new MenuItem { Header = $"{i+1}. {preset.Name}" };
                menuPreset.Items.Add(submenuPreset);
                var submenuPresetStart = new MenuItem { Header = "Start", Tag = preset };
                var submenuPresetRecord = new MenuItem { Header = "Start and record", Tag = preset };
                submenuPreset.Items.Add(submenuPresetStart);
                submenuPreset.Items.Add(submenuPresetRecord);
                submenuPresetStart.Click += SubmenuPreset_Click;
                submenuPresetRecord.Click += SubmenuPreset_Click;
            }

            menu.Items.Add(menuPaused);
            menu.Items.Add(menuCapture);
            menu.Items.Add(menuRecord);
            menu.Items.Add(menuAutoCoeff);
            menu.Items.Add(menuAutoPOV);
            menu.Items.Add(menuCoeffCount);
            menu.Items.Add(menuOrder);
            menu.Items.Add(menuSolver);
            menu.Items.Add(menuShowCoeff);
            menu.Items.Add(menuReset);

            menuCoeffCount.Click += MenuCoeffCount_Click;
            menuOrder.Click += MenuOrder_Click;
            menuReset.Click += (s, e) => { if (Reset != null) Reset(); };
            menuCapture.Click += MenuCapture_Click;
            menuRecord.Click += MenuRecord_Click;
            menuPaused.Click += Checkbox_Click;
            menuPaused.Click += (s, e) => { if (TogglePaused != null) TogglePaused(); };
            menuAutoPOV.Click += Checkbox_Click;
            menuAutoCoeff.Click += Checkbox_Click;
            menuShowCoeff.Click += Checkbox_Click;
            menuSolver.Click += (s, e) => { GuiUtil.EmitKeyDownEvent(placeholder, Key.G); };
            mouseSource.PreviewMouseRightButtonDown += Placeholder_PreviewMouseRightButtonDown;
            Checkbox_Click(this);
            placeholder.KeyDown += Placeholder_KeyDown;
        }

        private void Placeholder_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.P) {
                MenuCapture_Click(this, null);
                e.Handled = true;
            }
        }

        private void Checkbox_Click(object sender, RoutedEventArgs e = null)
        {
            ShowCoeff = menuShowCoeff.IsChecked;
            AutoCoeff = menuAutoCoeff.IsChecked;
            AutoPOV = menuAutoPOV.IsChecked;
            Paused = menuPaused.IsChecked;
        }

        public void SetCheckboxes(bool? showCoeff, bool? autoCoeff, bool? autoPOV, bool? paused)
        {
            if (showCoeff.HasValue)
                menuShowCoeff.IsChecked = showCoeff.Value;
            if (autoCoeff.HasValue)
                menuAutoCoeff.IsChecked = autoCoeff.Value;
            if (autoPOV.HasValue)
                menuAutoPOV.IsChecked = autoPOV.Value;
            if (paused.HasValue)
                menuPaused.IsChecked = paused.Value;
            Checkbox_Click(this);
        }

        private void MenuRecord_Click(object sender, RoutedEventArgs e)
        {
            if (menuRecord.IsChecked)
            {
                var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select folder to save frames as PNG files" };
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok && ToggleRecording != null)
                    ToggleRecording(dialog.FileName);
            }
            else if (ToggleRecording != null)
                ToggleRecording(null);
        }

        private void MenuCapture_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonSaveFileDialog { Title = "Select filename to save capture PNG", DefaultExtension = "png" };
            dialog.Filters.Add(new CommonFileDialogFilter("PNG files", "*.png"));
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok && SaveCapture != null)
                SaveCapture(dialog.FileName);
        }

        private void Placeholder_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            LastRightClick = e.GetPosition(placeholder);
            menu.PlacementTarget = placeholder;
            if (WinFormsMouseProxy.ProxyPoint.HasValue)
            {
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
                menu.HorizontalOffset = WinFormsMouseProxy.ProxyPoint.Value.X;
                menu.VerticalOffset = WinFormsMouseProxy.ProxyPoint.Value.Y;
            }
            else
            {
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            }

            menu.IsOpen = true;
            e.Handled = true;
        }

        private void SubmenuPreset_Click(object sender, RoutedEventArgs e)
        {
            var submenu = (MenuItem)sender;
            var selectedPreset = (BasePreset)(submenu.Tag);
            if (submenu.Header.ToString().Contains("record", StringComparison.InvariantCultureIgnoreCase))
            {
                var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select folder to save frames as PNG files" };
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    menuRecord.IsChecked = true;
                    if (PresetSelected != null)
                    {
                        PresetSelected(selectedPreset, dialog.FileName);

                    }
                }
            }
            else
            {
                menuRecord.IsChecked = false;
                if (PresetSelected != null)
                    PresetSelected(selectedPreset, null);
            }
        }

        private void MenuOrder_Click(object sender, RoutedEventArgs e)
        {
            var txt = GuiUtil.ShowInputDialog("Enter polynomial order. Mind that pixel count grows exponentialy", "Polynomial order");
            if (string.IsNullOrWhiteSpace(txt))
                return;
            if (int.TryParse(txt, out var newOrder))
            {
                if (OrderChanged != null)
                    OrderChanged(newOrder);
            }
            else
                MessageBox.Show("Not a number!");
        }

        private void MenuCoeffCount_Click(object sender, RoutedEventArgs e)
        {
            var txt = GuiUtil.ShowInputDialog("Enter number of values that will be used\nas coefficients. Mind that number of polynomials\ngrows exponentialy", "Coefficients values count");
            if (string.IsNullOrWhiteSpace(txt))
                return;
            if (int.TryParse(txt, out var count))
            {
                if (CoefficientCountChanged != null)
                    CoefficientCountChanged(count);
            }
            else
                MessageBox.Show("Not a number!");
        }

        public void UpdateContextMenu(int coefficientsCount, int order, BasePreset preset)
        {
            menuCoeffCount.Header = $"Number of coefficients values (A/S) [{coefficientsCount}]";
            menuOrder.Header = $"Polynomial order (Q/W) [{order}]";
            menuPreset.Header = $"Preset [{preset?.Name}]";
            menuSolver.Header = $"Toggle solver (G) [{(OpenGlSurface.UseComputeShader ? "gpu" : "cpu")}]";
            var testAutoPov = preset?.GetPOV(0);
            if (testAutoPov == null)
            {
                menuAutoPOV.IsEnabled = false;
                menuAutoPOV.IsChecked = false;
            }
            else
            {
                menuAutoPOV.IsEnabled = true;
            }

                var testAutoCoeff = preset?.GetCoefficients(0);
            if (testAutoCoeff == null)
            {
                menuAutoCoeff.IsEnabled = false;
                menuAutoCoeff.IsChecked = false;
            }
            else
            {
                menuAutoCoeff.IsEnabled = true;
            }

            AutoCoeff = menuAutoCoeff.IsChecked;
            AutoPOV = menuAutoPOV.IsChecked;
            Paused = menuPaused.IsChecked;
        }

    }
}

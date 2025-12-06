using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using PolyFract.Presets;
using static System.Formats.Asn1.AsnWriter;

namespace PolyFract.Gui
{
    public class PolyFractContextMenu
    {
        public ContextMenu menu;

        public MenuItem menuPreset = new MenuItem { };

        public MenuItem menuCapture = new MenuItem { Header = "Save screen capture" };

        public MenuItem menuRecord = new MenuItem { Header = "Record frames to folder", IsCheckable = true };

        public MenuItem menuPaused = new MenuItem { Header = "Paused", IsCheckable = true, IsChecked = true };

        public MenuItem menuAutoCoeff = new MenuItem { Header = "Automatic coefficients movement", IsCheckable = true };

        public MenuItem menuAutoPOV = new MenuItem { Header = "Automatic POV movement", IsCheckable = true };

        public MenuItem menuCopyPos = new MenuItem { Header = "Copy coordinates (C)" };

        public MenuItem menuCoeffCount = new MenuItem { };

        public MenuItem menuOrder = new MenuItem { };

        public MenuItem menuIntensity = new MenuItem { };

        public MenuItem menuShowCoeff = new MenuItem { Header = "Show coefficients markers", IsCheckable = true, IsChecked = true };

        public MenuItem menuVertical = new MenuItem { Header = "Vertical", IsCheckable = true };

        public MenuItem menuReset = new MenuItem { Header = "Reset to defaults" };

        public Panel placeholder;

        public Action<BasePreset, string> PresetSelected { get; set; }

        public Action<bool> OrientationChanged { get; set; }

        public Action Reset { get; set; }

        public Action<double> IntensityChanged { get; set; }

        public Action<int> CoefficientCountChanged { get; set; }

        public Action<int> OrderChanged { get; set; }

        public Action<string> SaveCapture { get; set; }

        public Action<string> ToggleRecording { get; set; }

        public Action CopyPosClicked { get; set; }

        public Point LastRightClick { get; set; }
            
        public PolyFractContextMenu(Panel placeholder)
        {
            this.placeholder = placeholder;
            menu = new ContextMenu();
            menu.Items.Add(menuPreset);
            foreach (var preset in BasePreset.AllPresets)
            {
                var submenuPreset = new MenuItem { Header = preset.Name };
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
            menu.Items.Add(menuCopyPos);
            menu.Items.Add(menuCoeffCount);
            menu.Items.Add(menuOrder);
            menu.Items.Add(menuIntensity);
            menu.Items.Add(menuShowCoeff);
            menu.Items.Add(menuVertical);
            menu.Items.Add(menuReset);

            menuCopyPos.Click += MenuCopyPos_Click;
            menuCoeffCount.Click += MenuCoeffCount_Click;
            menuOrder.Click += MenuOrder_Click;
            menuIntensity.Click += MenuIntensity_Click;
            menuReset.Click += MenuReset_Click;
            menuVertical.Click += MenuVertical_Click; ;
            menuCapture.Click += MenuCapture_Click;
            menuRecord.Click += MenuRecord_Click;
            placeholder.PreviewMouseRightButtonDown += Placeholder_PreviewMouseRightButtonDown;
        }

        private void MenuVertical_Click(object sender, RoutedEventArgs e)
        {
            if (menuVertical.IsChecked)
            {
                if (placeholder.Width > placeholder.Height)
                {
                    var tmp = placeholder.Width;
                    placeholder.Width = placeholder.Height;
                    placeholder.Height = tmp;
                    if (OrientationChanged != null)
                        OrientationChanged(menuVertical.IsChecked);
                }
            }
            else
            {
                if (placeholder.Width < placeholder.Height)
                {
                    var tmp = placeholder.Width;
                    placeholder.Width = placeholder.Height;
                    placeholder.Height = tmp;
                    if (OrientationChanged != null)
                        OrientationChanged(menuVertical.IsChecked);
                }
            }
        }

        private void MenuRecord_Click(object sender, RoutedEventArgs e)
        {
            if (menuRecord.IsChecked)
            {
                var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = "Select folder to save frames as PNG files" };
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    if (ToggleRecording != null)
                        ToggleRecording(dialog.FileName);
                }
            }
            else
            {
                ToggleRecording(null);
            }
        }

        private void MenuCapture_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonSaveFileDialog { Title = "Select filename to save capture PNG", DefaultExtension = "png" };
            dialog.Filters.Add(new CommonFileDialogFilter("PNG files", "*.png"));
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (SaveCapture != null)
                    SaveCapture(dialog.FileName);
            }
        }

        private void Placeholder_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            LastRightClick = e.GetPosition(placeholder);
            menu.PlacementTarget = placeholder;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
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

        private void MenuReset_Click(object sender, RoutedEventArgs e)
        {
            if (Reset != null)
                Reset();
        }

        private void MenuIntensity_Click(object sender, RoutedEventArgs e)
        {
            var txt = GuiUtil.ShowInputDialog("Enter pixel intensity: 1-100%", "Color intensity");
            if (string.IsNullOrWhiteSpace(txt))
                return;
            if (int.TryParse(txt, out var newIntensity))
            {
                if (newIntensity >= 1 && newIntensity <= 100)
                    if (IntensityChanged != null)
                        IntensityChanged(newIntensity * 0.01);
                else
                    MessageBox.Show("Invalid value!");
            }
            else
                MessageBox.Show("Not a number!");
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

        private void MenuCopyPos_Click(object sender, RoutedEventArgs e)
        {
            if (CopyPosClicked != null)
                CopyPosClicked();
        }

        public void UpdateMenuHeaders(int coefficientsCount, double intensity, int order, BasePreset preset)
        {
            menuCoeffCount.Header = $"Number of coefficients values (A/S) [{coefficientsCount}]";
            menuIntensity.Header = $"Colors intensity (O/P)  [{(int)System.Math.Round(intensity * 100)}%]";
            menuOrder.Header = $"Polynomial order (Q/W) [{order}]";
            menuPreset.Header = $"Preset [{preset?.Name}]";
            var testAutoPov = preset?.GetPOV(0);
            if (testAutoPov == null)
            {
                menuAutoPOV.IsEnabled = false;
                menuAutoPOV.IsChecked = false;
            }

            var testAutoCoeff = preset?.GetCoefficients(0);
            if (testAutoCoeff == null)
            {
                menuAutoCoeff.IsEnabled = false;
                menuAutoCoeff.IsChecked = false;
            }
        }

    }
}

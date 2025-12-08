using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace PolyFract.Gui
{
    public static class GuiUtil
    {
        public static void HsvToRgb(int h, out int r, out int g, out int b)
        {
            if (h < 0)
                h = 0;

            if (h > 255) 
                h = 255;

            // Convert 0–255 hue to 0–1535 (6 * 256)
            int x = h * 6;            // range 0–1530
            int sector = x >> 8;      // fast divide by 256 → 0..5
            int frac = x & 255;     // fractional part in [0..255]

            // For V=1 and S=1, intermediate values:
            int p = 0;
            int q = 255 - frac;
            int t = frac;

            switch (sector)
            {
                case 0: r = 255; g = t; b = 0; return;
                case 1: r = q; g = 255; b = 0; return;
                case 2: r = 0; g = 255; b = t; return;
                case 3: r = 0; g = q; b = 255; return;
                case 4: r = t; g = 0; b = 255; return;
                default:
                case 5: r = 255; g = 0; b = q; return;
            }
        }

        public static string ShowInputDialog(string text, string title)
        {
            // Window
            Window dialog = new Window()
            {
                Width = 400,
                Height = 200,
                Title = title,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Owner = Application.Current.MainWindow
            };

            // Layout
            StackPanel panel = new StackPanel() { Margin = new Thickness(10) };

            TextBlock txt = new TextBlock() { Text = text, Margin = new Thickness(0, 0, 0, 10) };
            TextBox input = new TextBox() { MinWidth = 200 };

            // Buttons
            StackPanel buttonPanel = new StackPanel()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            Button ok = new Button() { Content = "OK", Width = 70, Margin = new Thickness(5, 0, 0, 0) };
            Button cancel = new Button() { Content = "Cancel", Width = 70 };

            ok.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            cancel.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };

            buttonPanel.Children.Add(cancel);
            buttonPanel.Children.Add(ok);

            // Compose UI
            panel.Children.Add(txt);
            panel.Children.Add(input);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;

            // Show input dialog
            return dialog.ShowDialog() == true ? input.Text : null;
        }
    }
}

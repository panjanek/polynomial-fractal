using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using OpenTK.GLControl;
using Panel = System.Windows.Controls.Panel;
using Point = System.Windows.Point;

namespace PolyFract.Gui
{
    public class WinFormsMouseProxy : Panel
    {
        public static Point? ProxyPoint { get; set; }

        private GLControl glControl;

        MouseEventArgs? prevMouseMove;
        public WinFormsMouseProxy(GLControl glControl)
        {
            this.glControl = glControl;
            glControl.MouseWheel += GlControl_MouseWheel;
            glControl.MouseDown += GlControl_MouseDown;
            glControl.MouseUp += GlControl_MouseUp;
            glControl.MouseMove += GlControl_MouseMove;
        }

        private void GlControl_MouseMove(object? sender, MouseEventArgs e)
        {

            if (e.Button == MouseButtons.Left)
            {
                if (prevMouseMove?.X == e.X && prevMouseMove?.Y == e.Y)
                    return;

                prevMouseMove = e;

                var args = new System.Windows.Input.MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0);
                args.RoutedEvent = UIElement.MouseMoveEvent;
                ProxyPoint = ToWpfPoint(e);
                RaiseEvent(args);
            }
        }

        private void GlControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
                args.RoutedEvent = UIElement.MouseLeftButtonUpEvent;
                ProxyPoint = ToWpfPoint(e);
                RaiseEvent(args);
            }
        }

        private void GlControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
                args.RoutedEvent = UIElement.MouseLeftButtonDownEvent;
                ProxyPoint = ToWpfPoint(e);
                RaiseEvent(args);
            }
            else if (e.Button == MouseButtons.Right)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Right);
                args.RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent;
                ProxyPoint = ToWpfPoint(e);
                RaiseEvent(args);
            }

        }

        private void GlControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            var args = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, e.Delta);
            ProxyPoint = ToWpfPoint(e);
            args.RoutedEvent = UIElement.MouseWheelEvent;
            RaiseEvent(args);
        }
        System.Windows.Point ToWpfPoint(System.Windows.Forms.MouseEventArgs e)
        {
            // resacle by windows display scale setting to match WPF coordinates
            var p = new System.Windows.Point(e.X / GuiUtil.Dpi.DpiScaleX, e.Y / GuiUtil.Dpi.DpiScaleY);
            return p;
        }
    }
}

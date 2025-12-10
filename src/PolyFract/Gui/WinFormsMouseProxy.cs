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
                ProxyPoint = new System.Windows.Point(e.X, e.Y); //new System.Windows.Point(_pendingMove.Value.X, _pendingMove.Value.Y);      //
                RaiseEvent(args);
            }
        }

        private void GlControl_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
                args.RoutedEvent = UIElement.MouseLeftButtonUpEvent;
                ProxyPoint = new System.Windows.Point(e.X, e.Y);
                RaiseEvent(args);
            }
        }

        private void GlControl_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left);
                args.RoutedEvent = UIElement.MouseLeftButtonDownEvent;
                ProxyPoint = new System.Windows.Point(e.X, e.Y);
                RaiseEvent(args);
            }
            else if (e.Button == MouseButtons.Right)
            {
                var args = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Right);
                args.RoutedEvent = UIElement.PreviewMouseRightButtonDownEvent;
                ProxyPoint = new System.Windows.Point(e.X, e.Y);
                RaiseEvent(args);
            }

        }

        private void GlControl_MouseWheel(object? sender, MouseEventArgs e)
        {
            var args = new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, e.Delta);
            ProxyPoint = new System.Windows.Point(e.X, e.Y);
            args.RoutedEvent = UIElement.MouseWheelEvent;
            RaiseEvent(args);
        }
    }
}

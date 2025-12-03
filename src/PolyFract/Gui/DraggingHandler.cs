using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PolyFract.Gui
{
    public class DraggingHandler
    {
        public bool IsDragging { get; set; }

        FrameworkElement Parent { get; set; }

        public Point? PreviousPoint { get; set; }

        public Point? CurrentPoint { get; set; }

        public Func<Point, bool> CanDrag;

        public Action<Point, Point> Dragging;
        public DraggingHandler(FrameworkElement parent, Func<Point, bool> canDrag, Action<Point, Point> dragging)
        {
            CanDrag = canDrag;
            Dragging = dragging;
            Parent = parent;
            Parent.MouseLeftButtonDown += Parent_MouseLeftButtonDown;
            Parent.MouseMove += Parent_MouseMove;
            Parent.MouseLeftButtonUp += Parent_MouseLeftButtonUp;
        }

        private void Parent_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            IsDragging = false;
        }

        private void Parent_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (IsDragging)
            {
                CurrentPoint = e.GetPosition(Parent);
                if (Dragging != null && PreviousPoint.HasValue && CurrentPoint.HasValue)
                    Dragging(PreviousPoint.Value, CurrentPoint.Value);
                PreviousPoint = CurrentPoint;
            }
        }

        private void Parent_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var point = e.GetPosition(Parent);
            if (CanDrag == null || CanDrag(point))
            {
                IsDragging = true;
                PreviousPoint = point;
            }
        }
    }
}

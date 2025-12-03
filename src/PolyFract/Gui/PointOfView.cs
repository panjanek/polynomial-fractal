using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PolyFract.Gui
{
    public class PointOfView
    {
        public PointOfView(Complex origin, double zoom, double time)
        {
            Origin = origin;
            Zoom = zoom;
            Time = time;
        }

        public Complex Origin { get; set; }

        public double Zoom { get; set; }

        public double Time { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyFract.Maths;

namespace PolyFract.Gui
{
    public interface ISurface
    {
        int FrameCounter { get; }

        string Name { get; }

        System.Windows.Controls.Panel MouseEventSource { get; }
        void Draw(Solver solver, Complex[] coefficients, double intensity);

        void SizeChanged();

        void SetProjection(Complex origin, double zoom);

        void SaveToFile(string fileName);
    }
}

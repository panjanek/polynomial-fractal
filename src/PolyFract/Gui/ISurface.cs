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
        void Draw(Solver solver, Complex[] coefficients, double intensity);

        void SizeChanged();

        void SetProjection(Complex origin, double zoom);

        void SaveToFile(string fileName);
    }
}

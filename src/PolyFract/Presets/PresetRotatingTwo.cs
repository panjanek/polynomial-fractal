using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class PresetRotatingTwo : BasePreset
    {
        public override string Name => "Rotating two";

        public override int Order => 10;

        public override double DT => 0.01;

        public override double Intensity => 1.0;

        public override Complex[] GetCoefficients(double t)
        {
            double alpha = System.Math.PI / 2 + 10*t;
            var coeff = new Complex[2];
            coeff[0] = new Complex(-1, 0);
            coeff[1] = new Complex(1 * System.Math.Sin(alpha), 1 * System.Math.Cos(alpha));
            return coeff;
        }

        public override PointOfView GetPOV(double t)
        {
            return null;
        }
    }
}

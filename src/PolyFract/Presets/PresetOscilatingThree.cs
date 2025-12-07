using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class PresetOscilatingThree : BasePreset
    {
        public override string Name => "Oscillating Three";

        public override int Order => 8;

        public override double DT => 0.01;

        public override double Intensity => 1.0;

        private List<PointOfView> povMovement = [
            new PointOfView(new Complex(0, 0), 300, 0),
            new PointOfView(new Complex(0, 0), 310, 0.1),
            new PointOfView(new Complex(1.1312833814038368, 0.6286842631411), 500, 0.2),
            new PointOfView(new Complex(1.1312833814038368, 0.6286842631411), 1300, 0.3),
            new PointOfView(new Complex(0, 0), 500, 0.4),
            new PointOfView(new Complex(0, 0), 300, 0.5)
            ];


        public override Complex[] GetCoefficients(double t)
        {
            double alpha = 10 * t - 3.0;
            var coeff = new Complex[3];
            coeff[0] = new Complex(0, -1);
            coeff[1] = new Complex(0, 1);
            double r = System.Math.Sqrt(2.0);
            coeff[2] = new Complex(1 + r * System.Math.Cos(alpha), 0 + r * System.Math.Sin(alpha));
            return coeff;
        }
        public override PointOfView GetPOV(double t) => GetInterpolated(povMovement, t);
    }
}

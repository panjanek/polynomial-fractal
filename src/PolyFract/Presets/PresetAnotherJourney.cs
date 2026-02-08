using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class PresetAnotherJourney : BasePreset
    {
        public override string Name => "Another Journey";

        public override int Order => 7;   //11

        public override double DT => 0.01;

        private const double Speed = 3;   //0.03 -> 0.01

        public override Complex[] GetCoefficients(double t)
        {
            double alpha = System.Math.PI / 2 + 5 * t * Speed;    //20
            var coeff = new Complex[3];
            coeff[0] = new Complex(-0.25, -0.25);
            coeff[1] = new Complex(-0.25, 0.25);
            coeff[2] = new Complex(0.25 + 0.25 * System.Math.Sin(alpha), 0);
            return coeff;
        }

        public override PointOfView GetPOV(double t)
        {
            return new PointOfView(new Complex(0.6, 0), 1000 + t * 3000 * Speed, 0); //6000
        }
    }
}

using System.Numerics;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class PresetSlowJourney : BasePreset
    {
        public override string Name => "Slow Journey";

        public override int Order => 15;

        public override double DT => 0.01;

        private const double Speed = 0.15;

        public override Complex[] GetCoefficients(double t)
        {
            double alpha = 2.4 + 1.0 * t * Speed;
            var coeff = new Complex[2];
            coeff[0] = new Complex(-1, 0);
            coeff[1] = new Complex(1 * System.Math.Sin(alpha), 1 * System.Math.Cos(alpha));
            return coeff;
        }

        public override PointOfView GetPOV(double t)
        {
            //return new PointOfView(new Complex(0.5028, 0.8613), 10000 - t * 3000 * Speed, 0); //6000
            return new PointOfView(new Complex(0.5028, 0.8613), 10000 * Math.Exp(-t * 3 * Speed), 0); //6000
        }
    }
}

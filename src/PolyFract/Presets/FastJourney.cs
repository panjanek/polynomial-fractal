using System.Numerics;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class FastJourney : BasePreset
    {
        public override string Name => "Fast Journey";

        public override int Order => 10;

        public override double DT => 0.0005;

        public override double Intensity => 1.0;

        private List<PointOfView> povMovement = [];

        private List<CoefficientTimePoint> coeffMovement = [];

        public FastJourney()
        {
            double timeScale = 0.3;
            AddTimePoint(0.1 * timeScale, new Complex(-0.02666666666666667, 0.013333333333333334), 300.0, [new Complex(-0.9966666666666664, -0.0033333333333334693), new Complex(1, 0)]);
            AddTimePoint(0.2 * timeScale, new Complex(-0.023333333333333334, 0.016666666666666666), 300.0, [new Complex(0.020000000000000292, 1.0066666666666664), new Complex(0.009999999999999926, -1.0133333333333336)]);
            AddTimePoint(0.3 * timeScale, new Complex(-0.0033333333333333335, 0.02), 300.0, [new Complex(0.9800000000000004, -0.010000000000000373), new Complex(-1.0100000000000005, -2.6281060661048627E-16)]);
            AddTimePoint(0.4 * timeScale, new Complex(-0.013333333333333334, 0.006666666666666667), 300.0, [new Complex(-0.09333333333333273, -0.1666666666666669), new Complex(0.16999999999999973, 0.11999999999999969)]);
            AddTimePoint(0.5 * timeScale, new Complex(-0.016666666666666666, 0.023333333333333334), 300.0, [new Complex(-0.13666666666666594, 0.11666666666666636), new Complex(0.16999999999999973, 0.11999999999999969)]);
            AddTimePoint(0.6 * timeScale, new Complex(-0.006666666666666667, 0.08333333333333333), 300.0, [new Complex(0.003333333333333964, 0.11333333333333304), new Complex(0.03666666666666649, 0.11666666666666638)]);
        }

        private void AddTimePoint(double time, Complex origin, double zoom, Complex[] coeffs)
        {
            if (povMovement.Count == 0)
                time = 0;
            povMovement.Add(new PointOfView(origin, zoom, time));
            coeffMovement.Add(new CoefficientTimePoint(coeffs, time));
        }

        public override PointOfView GetPOV(double t) => GetInterpolated(povMovement, t);

        public override Complex[] GetCoefficients(double t) => GetInterpolated(coeffMovement, t);
    }
}

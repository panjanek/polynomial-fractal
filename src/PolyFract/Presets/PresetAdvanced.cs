using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class PresetAdvanced : BasePreset
    {
        public override string Name => "Advanced";

        public override int Order => 10;

        public override double DT => 0.0005;

        public override double Intensity => 1.0;

        private List<PointOfView> povMovement = [];

        private List<CoefficientTimePoint> coeffMovement = [];

        public PresetAdvanced()
        {
            AddTimePoint(0.0475, new Complex(0, 0), 300.0, [new Complex(-1, 0), new Complex(1, 0)]);
            AddTimePoint(0.1495, new Complex(0.36333333333333334, -0.5133333333333333), 300.0, [new Complex(-0.890000000000001, 0.46666666666666723), new Complex(1, 0)]);
            AddTimePoint(0.4115, new Complex(0.6653558333333331, -0.08028750000000046), 307.2, [new Complex(-0.6633333333333352, 0.8100000000000004), new Complex(0.966666666666667, 0.303333333333333)]);
            AddTimePoint(0.6400, new Complex(0.7404347665328375, -0.03972058779979104), 371.4, [new Complex(-0.6315300696070935, 0.8331296463463579), new Complex(0.966666666666667, 0.303333333333333)]);
            AddTimePoint(0.8660, new Complex(0.6964809984758066, 0.04559252812710582), 481.3, [new Complex(-0.6502309134579203, 0.7728713717159159), new Complex(1.047703656686918, 0.3885260664315441)]);
            AddTimePoint(1.5600, new Complex(0.6226714803744609, 0.17522177547920903), 686.9, [new Complex(-0.6273505799600969, 0.8327122439409924), new Complex(1.067173184397304, 0.40110900888857803)]);
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

    public class CoefficientTimePoint
    {
        public CoefficientTimePoint(Complex[] coeffs, double t)
        {
            Coeffs = coeffs;
            Time = t;
        }

        public Complex[] Coeffs;

        public double Time;
    }
}

using System.Numerics;
using PolyFract.Gui;

namespace PolyFract.Presets
{
    public class SlowJourney : BasePreset
    {
        public override string Name => "Slow Journey";

        public override int Order => 15;

        public override double DT => 0.0005;

        public override double Intensity => 0.15;

        private List<PointOfView> povMovement = [
                            new PointOfView(new Complex(0,0), 300, 0),
                            new PointOfView(new Complex(0,0), 310, 0.07),
                            new PointOfView(new Complex(0,0), 320, 0.15),
                            new PointOfView(new Complex(0.1788808758754369,0.3022485793768066), 401.5, 0.2540),
                            new PointOfView(new Complex(0.4292410299742795,0.3070230808199982), 506.8, 0.2980),
                            new PointOfView(new Complex(0.4547653885590368,0.1692010587987469), 678.3, 0.3470),
                            new PointOfView(new Complex(0.47281087908453556,0.07457816294580853), 1081.1, 0.4150),
                            new PointOfView(new Complex(-0.6709266555336313,0.0017293247964503267), 897.9, 0.5550),
                            new PointOfView(new Complex(-0.5362298055185547,0.29636905015696674), 701.0, 0.7350),
                            new PointOfView(new Complex(-0.65933566946349,0.719844890190539), 1018.7, 1.0110),
                            new PointOfView(new Complex(-0.10611520514400442,0.549244165964141), 997.7, 1.1960),
                            new PointOfView(new Complex(-0.3148895503519182,0.3237709706778804), 1415.3, 1.2580),
                            new PointOfView(new Complex(-0.5830051975366152,0.041896226300203246), 1781.0, 1.3930),
                            new PointOfView(new Complex(-1.0079563303248738,-0.03979370012889211), 377.7, 1.6040),
                            new PointOfView(new Complex(0.009831792219892437,0.025500170527910065), 277.2, 1.6690),
                            new PointOfView(new Complex(-0.10794180560473798,-0.8276083091546076), 491.8, 1.7870),
                            new PointOfView(new Complex(-0.16750386287878605,0.5231415749568155), 921.9, 2.0520),
                            new PointOfView(new Complex(-0.07970535135790532,-0.0635671616377362), 362.9, 2.1720),
                            new PointOfView(new Complex(0.03543009795247459,0.00770258215931021), 248.3, 2.3530),
                        ];

        public override Complex[] GetCoefficients(double t)
        {
            double alpha = t;
            double r = 1.25;
            var coeff = new Complex[2];
            coeff[0] = new Complex(r * System.Math.Sin(1 * alpha + 1), r * System.Math.Cos(5 * alpha));
            coeff[1] = new Complex(r * System.Math.Sin(6 * alpha + 2), r * System.Math.Cos(-1 * alpha));
            return coeff;
        }

        public override PointOfView GetPOV(double t)
        {
            return GetInterpolated(povMovement, t);
        }
    }
}

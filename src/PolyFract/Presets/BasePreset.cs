using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using PolyFract.Gui;
using PolyFract.Maths;
using static System.Formats.Asn1.AsnWriter;

namespace PolyFract.Presets
{
    public abstract class BasePreset
    {
        public static BasePreset[] AllPresets => 
                [
                    new PresetRotatingTwo(),
                    new PresetOscilatingThree(),
                    new PresetAnotherJourney(),
                    new FastJourney(),
                    new PresetSlowJourney(),
                    
                ];
        public abstract string Name { get; }
        public abstract int Order { get; }

        public abstract double DT { get; }

        public abstract double Intensity { get; }

        public abstract Complex[] GetCoefficients(double t);

        public abstract PointOfView GetPOV(double t);

        public static PointOfView GetInterpolated(List<PointOfView> list, double t)
        {
            var full = AddReversed(list);

            var maxT = full.Last().Time;
            var cycleT = t;
            while (cycleT > maxT)
                cycleT -= maxT;
            int before = 0;
            while (before < full.Count && full[before].Time <= cycleT)
            {
                before++;
            }
            before--;

            var P0 = (before > 0) ? full[before - 1] : full[full.Count - 1];
            var P1 = full[before % full.Count];
            var P2 = full[(before + 1) % full.Count];
            var P3 = full[(before + 2) % full.Count];
            var interpolatedOrigin = Interpolation.Interpolate(P0.Origin, P1.Origin, P2.Origin, P3.Origin, P1.Time, P2.Time, cycleT);
            var interpolatedZoom = Interpolation.Interpolate(P0.Zoom, P1.Zoom, P2.Zoom, P3.Zoom, P1.Time, P2.Time, cycleT);
            PointOfView result = new PointOfView(interpolatedOrigin, interpolatedZoom, t);
            return result;
        }

        public static Complex[] GetInterpolated(List<CoefficientTimePoint> list, double t)
        {
            var full = AddReversed(list);

            var maxT = full.Last().Time;
            var cycleT = t;
            while (cycleT > maxT)
                cycleT -= maxT;
            int before = 0;
            while (before < full.Count && full[before].Time <= cycleT)
            {
                before++;
            }
            before--;

            var P0 = (before > 0) ? full[before - 1] : full[full.Count - 1];
            var P1 = full[before % full.Count];
            var P2 = full[(before + 1) % full.Count];
            var P3 = full[(before + 2) % full.Count];
            List<Complex> interpolated = new List<Complex>();
            for (int i= 0; i < P1.Coeffs.Length; i ++)
                interpolated.Add(Interpolation.Interpolate(P0.Coeffs[1], P1.Coeffs[i], P2.Coeffs[i], P3.Coeffs[i], P1.Time, P2.Time, cycleT));
            return interpolated.ToArray();
        }

        private static List<PointOfView> AddReversed(List<PointOfView> list)
        {
            var maxT = list.Last().Time;
            var reversedCopy = list.Select(x => new PointOfView(x.Origin, x.Zoom, x.Time)).ToList();

            reversedCopy.Reverse();
            for (int i = 1; i < reversedCopy.Count; i++)
            {
                maxT += list[list.Count - i].Time - list[list.Count - i - 1].Time;
                reversedCopy[i].Time = maxT;
            }

            reversedCopy = reversedCopy.Skip(1).ToList();
            var full = new List<PointOfView>();
            full.AddRange(list);
            full.AddRange(reversedCopy);
            return full;
        }

        private static List<CoefficientTimePoint> AddReversed(List<CoefficientTimePoint> list)
        {
            var maxT = list.Last().Time;
            var reversedCopy = list.Select(x => new CoefficientTimePoint(x.Coeffs, x.Time)).ToList();

            reversedCopy.Reverse();
            for (int i = 1; i < reversedCopy.Count; i++)
            {
                maxT += list[list.Count - i].Time - list[list.Count - i - 1].Time;
                reversedCopy[i].Time = maxT;
            }

            reversedCopy = reversedCopy.Skip(1).ToList();
            var full = new List<CoefficientTimePoint>();
            full.AddRange(list);
            full.AddRange(reversedCopy);
            return full;
        }
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

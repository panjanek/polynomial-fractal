using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using PolyFract.Gui;
using PolyFract.Math;
using static System.Formats.Asn1.AsnWriter;

namespace PolyFract.Presets
{
    public abstract class BasePreset
    {
        public static BasePreset[] AllPresets => 
                [
                    new PresetFast(),
                    new PresetSlow(),
                    new PresetAdvanced()
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
            while (before < full.Count && full[before].Time < cycleT)
            {
                before++;
            }
            before--;

            var P1 = full[before % full.Count];
            var P2 = full[(before + 1) % full.Count];
            var P3 = full[(before + 2) % full.Count];
            double s = MathUtil.Map(0, 1, P1.Time, P2.Time, cycleT);

            double h1 = 2 * s * s * s - 3 * s * s + 1;          // calculate basis function 1
            double h2 = -2 * s * s * s + 3 * s * s;              // calculate basis function 2
            double h3 = s * s * s - 2 * s * s + s;         // calculate basis function 3
            double h4 = s * s * s - s * s;

            var T1 = new PointOfView(P2.Origin - P1.Origin, P2.Zoom - P1.Zoom, 0);
            var T2 = new PointOfView(P3.Origin - P2.Origin, P3.Zoom - P2.Zoom, 0);
            PointOfView res = new PointOfView(h1 * P1.Origin + h2 * P2.Origin + h3 * T1.Origin + h4 * T2.Origin,
                                              h1 * P1.Zoom + h2 * P2.Zoom + h3 * T1.Zoom + h4 * T2.Zoom, 0);
            res.Time = t;
            return res;
        }

        public static Complex[] GetInterpolated(List<CoefficientTimePoint> list, double t)
        {
            var full = AddReversed(list);

            var maxT = full.Last().Time;
            var cycleT = t;
            while (cycleT > maxT)
                cycleT -= maxT;
            int before = 0;
            while (before < full.Count && full[before].Time < cycleT)
            {
                before++;
            }
            before--;

            var P1 = full[before % full.Count];
            var P2 = full[(before + 1) % full.Count];
            var P3 = full[(before + 2) % full.Count];
            double s = MathUtil.Map(0, 1, P1.Time, P2.Time, cycleT);

            double h1 = 2 * s * s * s - 3 * s * s + 1;          // calculate basis function 1
            double h2 = -2 * s * s * s + 3 * s * s;              // calculate basis function 2
            double h3 = s * s * s - 2 * s * s + s;         // calculate basis function 3
            double h4 = s * s * s - s * s;

            List<Complex> interpolated = new List<Complex>();
            for (int i= 0; i < P1.Coeffs.Length; i ++)
            {
                var T1 = P2.Coeffs[i] - P1.Coeffs[i];
                var T2 = P3.Coeffs[i] - P2.Coeffs[i];
                interpolated.Add(h1 * P1.Coeffs[i] + h2 * P2.Coeffs[i] + h3 * T1 + h4 * T2);
            }

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
}

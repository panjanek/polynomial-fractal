using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.Statistics;
using PolyFract.Gui;

namespace PolyFract.Math
{
    public static class Interpolation
    {
        /// <summary>
        /// cubic Hermite interpolation
        /// </summary>
        /// <param name="P1">value at t1</param>
        /// <param name="P2">value at t2</param>
        /// <param name="P3">future value</param>
        /// <param name="t1">time for p1</param>
        /// <param name="t2">time for p2</param>
        /// <param name="t">current time between t1 and t2</param>
        /// <returns></returns>
        public static double Interpolate(double P1, double P2, double P3, double t1, double t2, double t)
        {
            double s = Map(0, 1, t1, t2, t);
            (double h1, double h2, double h3, double h4) = ComputeBasis(s);
            var T1 = P2 - P1;
            var T2 = P3 - P2;
            var result = h1 * P1 + h2 * P2 + h3 * T1 + h4 * T2;
            return result;
        }

        public static Complex Interpolate(Complex P1, Complex P2, Complex P3, double t1, double t2, double t)
        {
            double s = Map(0, 1, t1, t2, t);
            (double h1, double h2, double h3, double h4) = ComputeBasis(s);
            var T1 = P2 - P1;
            var T2 = P3 - P2;
            var result = h1 * P1 + h2 * P2 + h3 * T1 + h4 * T2;
            return result;
        }

        public static double Interpolate(double P0, double P1, double P2, double P3, double t1, double t2, double t)
        {
            double s = Map(0, 1, t1, t2, t);
            (double h1, double h2, double h3, double h4) = ComputeBasis(s);
            var T1 = P2 - P1; //(P2 - P0) / 2;
            var T2 = (P3 - P1) / 2;
            var result = h1 * P1 + h2 * P2 + h3 * T1 + h4 * T2;
            return result;
        }

        public static Complex Interpolate(Complex P0, Complex P1, Complex P2, Complex P3, double t1, double t2, double t)
        {
            double s = Map(0, 1, t1, t2, t);
            (double h1, double h2, double h3, double h4) = ComputeBasis(s);
            var T1 = P2 - P1; //(P2 - P0) / 2;
            var T2 = (P3 - P1) / 2;
            var result = h1 * P1 + h2 * P2 + h3 * T1 + h4 * T2;
            return result;
        }

        public static double Map(double from, double to, double tstart, double tstop, double t)
        {
            return from + (t - tstart) * (to - from) / (tstop - tstart);
        }

        public static (double, double, double, double) ComputeBasis(double s)
        {
            double h1 = 2 * s * s * s - 3 * s * s + 1;         
            double h2 = -2 * s * s * s + 3 * s * s;              
            double h3 = s * s * s - 2 * s * s + s;         
            double h4 = s * s * s - s * s;
            return (h1, h2, h3, h4);
        }
    }
}

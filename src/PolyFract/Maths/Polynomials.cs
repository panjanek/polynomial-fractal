using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using PolyFract.Gui;


namespace PolyFract.Maths
{
    public static class Polynomials
    {
        public const int MaxIterations = 32;

        public const double Tolerance = 1e-10;

        public const double ErrorMargin = 0.0001;

        public const int ErrorMarker = 1000000;

        public static bool IsNativeLibAvailable { get; private set; } = false;

        public static void InitNative()
        {
            var testArch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
            var testLocation = System.IO.Path.GetFullPath("PolyFractFastSolver.dll");
            Console.WriteLine($"Trying to load native solver dll from {testLocation}, arch {testArch}");
            try
            {

                var input = new double[3] { 1, 2, 3 };
                var output = new double[3] { 0, 0, 0 };
                TestNative(input, input.Length, output);
                IsNativeLibAvailable = (output[0] == 2 && output[1] == 4 && output[2] == 6);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"native dll not loaded: {ex.Message}");
                IsNativeLibAvailable = false;
            }

            IsNativeLibAvailable = false;
        }

        [DllImport("PolyFractFastSolver.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void TestNative(
                                        [In] double[] input,
                                        int length,
                                        [Out] double[] output);

        [DllImport("PolyFractFastSolver.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void FindRootsForPolys(
                              //actual parameters
                              int from,
                              int to,
                              [In] double[] coeffsvalues_r,
                              [In] double[] coeffsvalues_i,
                              int coeffsvalues_len,

                              //preallocated buffer for numbered polynomials
                              [In] double[] _poly_r,
                              [In] double[] _poly_i,
                              int _poly_len,

                              //preallocated buffer for kerner-durand
                              [In] double[] _monic_r,
                              [In] double[] _monic_i,
                              [In] double[] _z_r,
                              [In] double[] _z_i,
                              [In] double[] _z_a,
                              [In] double[] _newZ_r,
                              [In] double[] _newZ_i,

                              //outputs
                              [Out] double[] roots_r,
                              [Out] double[] roots_i,
                              [Out] double[] roots_a,
                              [Out] int[] color_r,
                              [Out] int[] color_g,
                              [Out] int[] color_b);

        // managed code implementation used if native code library could not be loaded
        public static void FindRootsForPolysManaged(
                                      //actual parameters
                                      int from,
                                      int to,
                                      CompactClomplex[] coeffsvalues,

                                      //preallocated buffer for numbered polynomials
                                      CompactClomplex[] _poly,

                                      //preallocated buffer for kerner-durand
                                      CompactClomplex[] _monic,
                                      CompactClomplexWithAngle[] _z,
                                      CompactClomplex[] _newZ,

                                      //outputs
                                      CompactClomplexWithAngle[] roots)
        {
            for (int i = from; i < to; i++)
            {
                int polyIdx = i;
                for (int j = 0; j < _poly.Length; j++)
                {
                    int coeffIdx = polyIdx % coeffsvalues.Length;
                    polyIdx = polyIdx / coeffsvalues.Length;
                    _poly[j] = coeffsvalues[coeffIdx];
                }

                FindRoots(_poly,

                          _monic,
                          _z,
                          _newZ);

                int targetFirstIdx = (i - from) * _z.Length;
                for (int j = 0; j < _z.Length; j++)
                {
                    int targetIdx = targetFirstIdx + j;
                    roots[targetIdx] = _z[j];

                    var h = (int)Math.Round((255 * (Math.PI + roots[targetIdx].a)) / (2 * System.Math.PI));
                    GuiUtil.HsvToRgb(h, out var r, out var g, out var b);

                    roots[targetIdx].colorR = r;
                    roots[targetIdx].colorG = g;
                    roots[targetIdx].colorB = b;
                }
            }
        }

        // managed code implementation used if native code library could not be loaded
        public static void FindRoots(
            //polynomial to solve
            CompactClomplex[] poly,

            // preallocated buffers
            CompactClomplex[] _monic,
            CompactClomplexWithAngle[] _z,
            CompactClomplex[] _newZ)
        {
            if (poly == null || poly.Length < 2)
                return;

            int n = poly.Length - 1;
            double a0_r = poly[0].r;
            double a0_i = poly[0].i;
            if (a0_r == 0 && a0_i == 0)
                return;

            // ---- Build monic coefficients into reusable _monic ----
            // monic: [1, b1, ..., bn] for z^n + b1*z^(n-1) + ... + bn
            _monic[0].r = 1;
            _monic[0].i = 0;
            for (int i = 1; i <= n; i++)
            {
                //_monic[i] = coeffsDescending[i] / a0;
                double div = a0_r * a0_r + a0_i * a0_i;
                _monic[i].r = (poly[i].r * a0_r + poly[i].i * a0_i) / div;
                _monic[i].i = (poly[i].i * a0_r - poly[i].r * a0_i) / div;
            }

            // ---- Initial radius ----
            double maxAbs = 0.0;
            for (int i = 1; i <= n; i++)
            {
                double m = Magnitude(_monic[i].r, _monic[i].i); // _monic[i].Magnitude;
                if (m > maxAbs) maxAbs = m;
            }
            double r = 1.0 + maxAbs;

            // ---- Initial guesses in reusable _z ----
            double twoPiOverN = 2.0 * Math.PI / n;
            for (int k = 0; k <= n; k++)
            {
                double angle = twoPiOverN * k;

                //_z[k] = Complex.FromPolarCoordinates(r, angle);
                _z[k].r = r * Math.Cos(angle);
                _z[k].i = r * Math.Sin(angle);
            }

            // ---- Iterations using _z and _newZ ----
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                double maxDelta = 0.0;

                for (int i = 0; i <= n; i++)
                {
                    //Complex zi = _z[i];
                    double zi_r = _z[i].r;
                    double zi_i = _z[i].i;

                    // Horner evaluation with monic coeffs
                    // Complex p = _monic[0];
                    double p_r = _monic[0].r;
                    double p_i = _monic[0].i;
                    for (int k = 1; k <= n; k++)
                    {
                        //p = p * zi + _monic[k];
                        var p_r_tmp = p_r * zi_r - p_i * zi_i + _monic[k].r;
                        var p_i_tmp = p_r * zi_i + p_i * zi_r + _monic[k].i;
                        p_r = p_r_tmp;
                        p_i = p_i_tmp;
                    }

                    //Complex denom = Complex.One;
                    double denom_r = 1;
                    double denom_i = 0;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i)
                            continue;

                        //denom *= (zi - _z[j]);
                        var mult_r = zi_r - _z[j].r;
                        var mult_i = zi_i - _z[j].i;
                        var denom_r_tmp = denom_r * mult_r - denom_i * mult_i;
                        var denom_i_tmp = denom_r * mult_i + denom_i * mult_r;
                        denom_r = denom_r_tmp;
                        denom_i = denom_i_tmp;
                    }

                    //Complex delta = p / denom;
                    double div = denom_r * denom_r + denom_i * denom_i;
                    double delta_r = (p_r * denom_r + p_i * denom_i) / div;
                    double delta_i = (p_i * denom_r - p_r * denom_i) / div;

                    //Complex ziNew = zi - delta;
                    double ziNew_r = zi_r - delta_r;
                    double ziNew_i = zi_i - delta_i;

                    //_newZ[i] = ziNew;
                    _newZ[i].r = ziNew_r;
                    _newZ[i].i = ziNew_i;

                    //double d = delta.Magnitude;
                    double d = Magnitude(delta_r, delta_i);
                    if (d > maxDelta) maxDelta = d;
                }

                // swap buffers (_z <= _newZ)
                for (int i = 0; i <= n; i++)
                {
                    //_z[i] = _newZ[i];
                    _z[i].r = _newZ[i].r;
                    _z[i].i = _newZ[i].i;
                }

                if (maxDelta < Tolerance)
                    break;
            }

            //compute angles
            for (int i = 0; i <= n; i++)
            {
                _z[i].a = AngleAt(poly, _z[i].r, _z[i].i);
            }

            //remove errors
            for (int i = 0; i <= n; i++)
            {
                var r_r = _z[i].r;
                var r_i = _z[i].i;

                double v_r = poly[0].r;
                double v_i = poly[0].i;
                for (int j = 1; j <= n; j++)
                {
                    //v = v * r + coeffsDescending[j];
                    var v_r_tmp = v_r * r_r - v_i * r_i + poly[j].r;
                    var v_i_tmp = v_r * r_i + v_i * r_r + poly[j].i;
                    v_r = v_r_tmp;
                    v_i = v_i_tmp;
                }

                var v_m = Magnitude(v_i, v_r);
                if (v_m > ErrorMargin)
                    _z[i].r = ErrorMarker;
            }
        }

        public static double Magnitude(double a, double b)
        {
            // Using
            //   sqrt(a^2 + b^2) = |a| * sqrt(1 + (b/a)^2)
            // we can factor out the larger component to dodge overflow even when a * a would overflow.

            a = Math.Abs(a);
            b = Math.Abs(b);

            double small, large;
            if (a < b)
            {
                small = a;
                large = b;
            }
            else
            {
                small = b;
                large = a;
            }

            if (small == 0.0)
            {
                return (large);
            }
            else
            {
                double ratio = small / large;
                return (large * Math.Sqrt(1.0 + ratio * ratio));
            }
        }

        public static double AngleAt(CompactClomplex[] coeffs, double x_r, double x_i)
        {
            //evaluate derivative at x
            int n = coeffs.Length - 1;
            double d_r = 0;
            double d_i = 0;

            for (int i = 0; i < n; i++)
            {
                var d_r_tmp = d_r * x_r - d_i * x_i + coeffs[i].r * (n - i);
                var d_i_tmp = d_r * x_i + d_i * x_r + coeffs[i].r * (n - 1);
                d_r = d_r_tmp;
                d_i = d_i_tmp;
            }
          
            //simplified angle of d
            return UltraFastAtan2(d_i, d_r);
        }

        public static double UltraFastAtan2(double y, double x)
        {
            double absY = Math.Abs(y) + 1e-10f;

            double angle;
            if (x >= 0)
            {
                double r = (x - absY) / (x + absY);
                angle = (double)(Math.PI / 4) - (double)(0.9675 * r);
            }
            else
            {
                double r = (x + absY) / (absY - x);
                angle = (double)(3 * Math.PI / 4) - (double)(0.9675 * r);
            }

            return (y < 0) ? -angle : angle;
        }
    }
}

public struct CompactClomplex
{
    public double r;
    public double i;
}

public struct CompactClomplexWithAngle
{
    public double r;
    public double i;
    public double a;
    public int colorR;
    public int colorG;
    public int colorB;
}

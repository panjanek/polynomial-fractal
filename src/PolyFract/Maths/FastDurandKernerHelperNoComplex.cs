using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


namespace PolyFract.Maths
{
    public class FastDurandKernerHelperNoComplex
    {
        private const int MaxIterations = 32;

        private const double Tolerance = 1e-10;

        private const double ErrorMargin = 0.0001;

        private readonly int _maxDegree;
        private readonly double[] _monic_r;
        private readonly double[] _monic_i;
        public readonly double[] _z_r;
        public readonly double[] _z_i;
        public readonly double[] _z_a;
        private readonly double[] _newZ_r;
        private readonly double[] _newZ_i;

        public FastDurandKernerHelperNoComplex(int maxDegree)
        {
            _maxDegree = maxDegree;
            _monic_r = new double[maxDegree + 1];
            _monic_i = new double[maxDegree + 1];
            _z_r = new double[maxDegree];
            _z_i = new double[maxDegree];
            _z_a = new double[maxDegree];
            _newZ_r = new double[maxDegree];
            _newZ_i = new double[maxDegree];
        }

        /// <summary>
        /// Find roots of polynomial with complex coefficients using Durand–Kerner.
        /// coeffsDescending: a0*z^n + a1*z^(n-1) + ... + an
        /// Returns roots in a new array (but you can also pass in a buffer if you want).
        /// </summary>
        public void FindRoots(double[] coeffsDescending_r, double[] coeffsDescending_i)
        {
            if (coeffsDescending_r == null || coeffsDescending_r.Length < 2)
                throw new ArgumentException("At least two coefficients required.");

            int n = coeffsDescending_r.Length - 1;
            if (n > _maxDegree)
                throw new ArgumentException("Polynomial degree exceeds solver maxDegree.");

            double a0_r = coeffsDescending_r[0];
            double a0_i = coeffsDescending_i[0];
            if (a0_r == 0 && a0_i == 0)
                throw new ArgumentException("Leading coefficient must be non-zero.");

            // ---- Build monic coefficients into reusable _monic ----
            // monic: [1, b1, ..., bn] for z^n + b1*z^(n-1) + ... + bn
            _monic_r[0] = 1;
            _monic_i[0] = 0;
            for (int i = 1; i <= n; i++)
            {
                //_monic[i] = coeffsDescending[i] / a0;
                double div = a0_r * a0_r + a0_i * a0_i;
                _monic_r[i] = (coeffsDescending_r[i] * a0_r + coeffsDescending_i[i] * a0_i) / div;
                _monic_i[i] = (coeffsDescending_i[i] * a0_r - coeffsDescending_r[i] * a0_i) / div;
            }

            // ---- Initial radius ----
            double maxAbs = 0.0;
            for (int i = 1; i <= n; i++)
            {
                double m = Magnitude(_monic_r[i], _monic_i[i]); // _monic[i].Magnitude;
                if (m > maxAbs) maxAbs = m;
            }
            double r = 1.0 + maxAbs;

            // ---- Initial guesses in reusable _z ----
            double twoPiOverN = 2.0 * Math.PI / n;
            for (int k = 0; k < n; k++)
            {
                double angle = twoPiOverN * k;

                //_z[k] = Complex.FromPolarCoordinates(r, angle);
                //new Complex(magnitude * Math.Cos(phase), magnitude * Math.Sin(phase));
                _z_r[k] = r * Math.Cos(angle);
                _z_i[k] = r * Math.Sin(angle);

            }

            // ---- Iterations using _z and _newZ ----
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                double maxDelta = 0.0;

                for (int i = 0; i < n; i++)
                {
                    //Complex zi = _z[i];
                    double zi_r = _z_r[i];
                    double zi_i = _z_i[i];

                    // Horner evaluation with monic coeffs
                    // Complex p = _monic[0];
                    double p_r = _monic_r[0];
                    double p_i = _monic_i[0];
                    for (int k = 1; k <= n; k++)
                    {
                        //p = p * zi + _monic[k];
                        var p_r_tmp = p_r * zi_r - p_i * zi_i + _monic_r[k];
                        var p_i_tmp = p_r * zi_i + p_i * zi_r + _monic_i[k];
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
                        var mult_r = zi_r - _z_r[j];
                        var mult_i = zi_i - _z_i[j];
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
                    _newZ_r[i] = ziNew_r;
                    _newZ_i[i] = ziNew_i;

                    //double d = delta.Magnitude;
                    double d = Magnitude(delta_r, delta_i);
                    if (d > maxDelta) maxDelta = d;
                }

                // swap buffers (_z <= _newZ)
                for (int i = 0; i < n; i++)
                {
                    //_z[i] = _newZ[i];
                    _z_r[i] = _newZ_r[i];
                    _z_i[i] = _newZ_i[i];
                }

                if (maxDelta < Tolerance)
                    break;
            }

            //compute angles
            for (int i = 0; i < n; i++)
            {
                _z_a[i] = AngleAt(coeffsDescending_r, coeffsDescending_i, _z_r[i], _z_i[i]);
            }

            //remove errors
            for (int i = 0; i < n; i++)
            {
                var r_r = _z_r[i];
                var r_i = _z_i[i];

                double v_r = coeffsDescending_r[0];
                double v_i = coeffsDescending_i[0];
                for (int j = 1; j <= n; j++)
                {
                    //v = v * r + coeffsDescending[j];
                    var v_r_tmp = v_r * r_r - v_i * r_i + coeffsDescending_r[j];
                    var v_i_tmp = v_r * r_i + v_i * r_r + coeffsDescending_i[j];
                    v_r = v_r_tmp;
                    v_i = v_i_tmp;
                }

                var v_m = Magnitude(v_i, v_r);
                if (v_m > ErrorMargin)
                    _z_r[i] = double.MinValue;
            }
        }

        public static Complex EvalPoly(Complex[] coeffsDescending, Complex z)
        {
            int n = coeffsDescending.Length - 1;
            Complex p = coeffsDescending[0];
            for (int i = 1; i <= n; i++)
                p = p * z + coeffsDescending[i];
            return p;
        }

        private double Magnitude(double a, double b)
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

        private double AngleAt(double[] coeffs_r, double[] coeffs_i, double x_r, double x_i)
        {
            //var derivative = MathUtil.EvaluateDerivative(coeffs, x);
            int n = coeffs_r.Length - 1;
            double d_r = 0;
            double d_i = 0;

            for (int i = 0; i < n; i++)
            {
                //d = d * x + coeffsDescending[i] * (n - i);
                var d_r_tmp = d_r * x_r - d_i * x_i + coeffs_r[i] * (n - i);
                var d_i_tmp = d_r * x_i + d_i * x_r + coeffs_i[i] * (n - 1);
                d_r = d_r_tmp;
                d_i = d_i_tmp;
            }
        
            return UltraFastAtan2(d_i, d_r);
        }

        private double UltraFastAtan2(double y, double x)
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

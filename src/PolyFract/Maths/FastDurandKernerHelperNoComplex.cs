using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;


/*
namespace PolyFract.Maths
{
    public class FastDurandKernerHelperNoComplex
    {
        private const int MaxIterations = 32;

        private double Tolerance = 1e-10;

        private readonly int _maxDegree;
        private readonly double[] _monic_r;
        private readonly double[] _monic_i;
        public readonly Complex[] _z;
        private readonly Complex[] _newZ;

        public FastDurandKernerHelperNoComplex(int maxDegree)
        {
            _maxDegree = maxDegree;
            _monic_r = new double[maxDegree + 1];
            _monic_i = new double[maxDegree + 1];
            _z = new Complex[maxDegree];
            _newZ = new Complex[maxDegree];
        }

        /// <summary>
        /// Find roots of polynomial with complex coefficients using Durand–Kerner.
        /// coeffsDescending: a0*z^n + a1*z^(n-1) + ... + an
        /// Returns roots in a new array (but you can also pass in a buffer if you want).
        /// </summary>
        public void FindRoots(double[] coeffsDescendingReal, double[] coeffsDescendingImg)
        {
            if (coeffsDescendingReal == null || coeffsDescendingReal.Length < 2)
                throw new ArgumentException("At least two coefficients required.");

            int n = coeffsDescendingReal.Length - 1;
            if (n > _maxDegree)
                throw new ArgumentException("Polynomial degree exceeds solver maxDegree.");

            double a0_r = coeffsDescendingReal[0];
            double a0_i = coeffsDescendingImg[0];
            if (a0_r == 0 && a0_i == 0)
                throw new ArgumentException("Leading coefficient must be non-zero.");

            // ---- Build monic coefficients into reusable _monic ----
            // monic: [1, b1, ..., bn] for z^n + b1*z^(n-1) + ... + bn
            _monic_r[0] = 1;
            _monic_i[0] = 1;
            for (int i = 1; i <= n; i++)
            {
                //_monic[i] = coeffsDescending[i] / a0;

            }

            // ---- Initial radius ----
            double maxAbs = 0.0;
            for (int i = 1; i <= n; i++)
            {
                double m = _monic[i].Magnitude;
                if (m > maxAbs) maxAbs = m;
            }
            double r = 1.0 + maxAbs;

            // ---- Initial guesses in reusable _z ----
            double twoPiOverN = 2.0 * Math.PI / n;
            for (int k = 0; k < n; k++)
            {
                double angle = twoPiOverN * k;
                _z[k] = Complex.FromPolarCoordinates(r, angle);
            }

            // ---- Iterations using _z and _newZ ----
            for (int iter = 0; iter < MaxIterations; iter++)
            {
                double maxDelta = 0.0;

                for (int i = 0; i < n; i++)
                {
                    Complex zi = _z[i];

                    // Horner evaluation with monic coeffs
                    Complex p = _monic[0];
                    for (int k = 1; k <= n; k++)
                        p = p * zi + _monic[k];

                    Complex denom = Complex.One;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        denom *= (zi - _z[j]);
                    }

                    Complex delta = p / denom;
                    Complex ziNew = zi - delta;
                    _newZ[i] = ziNew;

                    double d = delta.Magnitude;
                    if (d > maxDelta) maxDelta = d;
                }

                // swap buffers (_z <= _newZ)
                for (int i = 0; i < n; i++)
                    _z[i] = _newZ[i];

                if (maxDelta < Tolerance)
                    break;
            }
        }
    }
}
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PolyFract.Maths
{
    public class DurandKernerHelper
    {
        private const int MaxIterations = 32;

        private double Tolerance = 1e-10;

        private readonly int _maxDegree;
        private readonly Complex[] _monic;
        private readonly Complex[] _z;
        private readonly Complex[] _newZ;

        public DurandKernerHelper(int maxDegree)
        {
            _maxDegree = maxDegree;
            _monic = new Complex[maxDegree + 1];
            _z = new Complex[maxDegree];
            _newZ = new Complex[maxDegree];
        }

        /// <summary>
        /// Find roots of polynomial with complex coefficients using Durand–Kerner.
        /// coeffsDescending: a0*z^n + a1*z^(n-1) + ... + an
        /// Returns roots in a new array (but you can also pass in a buffer if you want).
        /// </summary>
        public Complex[] FindRoots(Complex[] coeffsDescending)
        {
            if (coeffsDescending == null || coeffsDescending.Length < 2)
                throw new ArgumentException("At least two coefficients required.");

            int n = coeffsDescending.Length - 1;
            if (n > _maxDegree)
                throw new ArgumentException("Polynomial degree exceeds solver maxDegree.");

            Complex a0 = coeffsDescending[0];
            if (a0 == Complex.Zero)
                throw new ArgumentException("Leading coefficient must be non-zero.");

            // ---- Build monic coefficients into reusable _monic ----
            // monic: [1, b1, ..., bn] for z^n + b1*z^(n-1) + ... + bn
            _monic[0] = Complex.One;
            for (int i = 1; i <= n; i++)
                _monic[i] = coeffsDescending[i] / a0;

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

            return _z;
        }
    }
}

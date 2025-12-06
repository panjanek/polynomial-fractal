using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;

namespace PolyFract.Maths
{
    public static class PolyUtil
    {
        /// <summary>
        /// Find all complex roots of polynomial with complex coefficients using Durand-Kerner.
        /// Coeffs are in descending powers: a0*z^n + a1*z^(n-1) + ... + an.
        /// </summary>
        public static Complex[] FindRootsDurandKerner(Complex[] coeffsDescending, int maxIterations, double tolerance)
        {
            if (coeffsDescending == null || coeffsDescending.Length < 2)
                throw new ArgumentException("At least two coefficients required.");

            int n = coeffsDescending.Length - 1; // degree
            Complex a0 = coeffsDescending[0];
            if (a0 == Complex.Zero)
                throw new ArgumentException("Leading coefficient must be non-zero.");

            // Make polynomial monic: z^n + b1*z^(n-1) + ... + bn
            // Store full monic coeffs: [1, b1, ..., bn]
            var monic = new Complex[n + 1];
            monic[0] = Complex.One;
            for (int i = 1; i <= n; i++)
                monic[i] = coeffsDescending[i] / a0;

            // Initial radius heuristic: 1 + max |b_k|
            double maxAbs = 0.0;
            for (int i = 1; i <= n; i++)
            {
                double m = monic[i].Magnitude;
                if (m > maxAbs) maxAbs = m;
            }
            double r = 1.0 + maxAbs;

            // Initial guesses: r * exp(2π i k / n)
            var z = new Complex[n];
            double twoPiOverN = 2.0 * Math.PI / n;
            for (int k = 0; k < n; k++)
            {
                double angle = twoPiOverN * k;
                z[k] = Complex.FromPolarCoordinates(r, angle);
            }

            // Work buffers to reduce allocations
            Complex[] newZ = new Complex[n];

            for (int iter = 0; iter < maxIterations; iter++)
            {
                double maxDelta = 0.0;

                for (int i = 0; i < n; i++)
                {
                    Complex zi = z[i];

                    // Evaluate polynomial at zi using Horner (monic)
                    Complex p = monic[0];
                    for (int k = 1; k <= n; k++)
                        p = p * zi + monic[k];

                    // Compute product ∏_{j ≠ i} (zi - zj)
                    Complex denom = Complex.One;
                    for (int j = 0; j < n; j++)
                    {
                        if (j == i) continue;
                        denom *= (zi - z[j]);
                    }

                    // Durand-Kerner update
                    Complex delta = p / denom;
                    Complex ziNew = zi - delta;
                    newZ[i] = ziNew;

                    double d = delta.Magnitude;
                    if (d > maxDelta) maxDelta = d;
                }

                // Copy back
                for (int i = 0; i < n; i++)
                    z[i] = newZ[i];

                if (maxDelta < tolerance)
                    break;
            }

            // Undo monic scaling: roots of original p(z) and monic version are the same,
            // so no extra work needed here.
            return z;
        }


        /// <summary>
        /// Finds roots of polynomial using companion-matrix + eigenvalues approach. Pretty precise, but slower
        /// </summary>
        public static MathNet.Numerics.LinearAlgebra.Vector<Complex> FindRoots(Complex[] coeffsDescending)
        {
            if (coeffsDescending == null || coeffsDescending.Length < 2)
                return null;

            int n = coeffsDescending.Length - 1;   // degree
            Complex leading = coeffsDescending[0];

            if (leading == Complex.Zero)
                return null;

            // Normalize polynomial to monic: z^n + b0*z^(n-1) + ... + b_{n-1}
            var b = new Complex[n];
            for (int j = 0; j < n; j++)
            {
                b[j] = coeffsDescending[j + 1] / leading;
            }

            // Build companion matrix (n x n, Complex)
            // [ -b0  -b1  ...  -b_{n-1} ]
            // [  1    0   ...     0     ]
            // [  0    1   ...     0     ]
            // [ ...                 ... ]
            // [  0    0   ...      1    ]
            var M = Matrix<Complex>.Build.Dense(n, n);

            // First row
            for (int j = 0; j < n; j++)
                M[0, j] = -b[j];

            // Subdiagonal ones
            for (int i = 1; i < n; i++)
                M[i, i - 1] = Complex.One;

            // Eigenvalues of companion matrix are the roots
            var evd = M.Evd();
            return evd.EigenValues;
        }

        public static Complex EvalPoly(Complex[] coeffsDescending, Complex z)
        {
            int n = coeffsDescending.Length - 1;
            Complex p = coeffsDescending[0];
            for (int i = 1; i <= n; i++)
                p = p * z + coeffsDescending[i];
            return p;
        }
    }
}

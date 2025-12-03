using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace PolyFract.Math
{
    public static class Solver
    {
        public static List<SolutionPoint> SolveAll(Complex[] coefficients, int order)
        {
            List<SolutionPoint> allRoots = new List<SolutionPoint>();

            int polynomialsCount = 1;
            for (int i = 0; i < order + 1; i++)
                polynomialsCount *= coefficients.Length;

            Parallel.For(0, polynomialsCount, new ParallelOptions() { MaxDegreeOfParallelism = 16 }, i => {

                Complex[] poly = new Complex[order + 1];
                int polyIdx = i;
                for (int j = 0; j < poly.Length; j++)
                {
                    int coeffIdx = polyIdx % coefficients.Length;
                    polyIdx = polyIdx / coefficients.Length;
                    poly[j] = coefficients[coeffIdx];
                }

                try
                {
                    var roots = FindRoots(poly);
                    var points = new List<SolutionPoint>();
                    foreach(var root in roots)
                    {
                        var point = new SolutionPoint() { root = root };

                        (int m, Complex v, double angle) = LocalDirection(poly, root);
                        point.angle = angle;

                        points.Add(point);
                    }


                    if (roots != null)
                    {
                        lock (allRoots)
                        {
                            allRoots.AddRange(points);
                        }
                    }
                }
                catch (Exception ex) { }
            });

            return allRoots;
        }

        public static Complex[] FindRoots(Complex[] coeffsDescending)
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
            return evd.EigenValues.ToArray();
        }

        public static (int m, Complex v, double angleRad) LocalDirection(Complex[] coeffs, Complex r, double tol = 1e-12)
        {
            if (coeffs == null || coeffs.Length == 0)
                throw new ArgumentException("coeffs required");

            int n = coeffs.Length - 1; // degree = n
                                       // scale tolerance by coefficient magnitudes
            double coeffScale = 0.0;
            foreach (var c in coeffs) coeffScale = System.Math.Max(coeffScale, c.Magnitude);
            double effectiveTol = tol * System.Math.Max(1.0, coeffScale);

            // prepare current derivative coefficients; start with original
            Complex[] derivCoeffs = new Complex[coeffs.Length];
            Array.Copy(coeffs, derivCoeffs, coeffs.Length);

            // iterate k = 1..n to find first nonzero derivative at r
            for (int k = 1; k <= n; ++k)
            {
                // compute derivative coefficients of order k (in-place):
                // for a polynomial of degree d, new coeff j = old coeff j * (d - j)
                int d = derivCoeffs.Length - 1;
                Complex[] next = new Complex[derivCoeffs.Length - 1];
                for (int j = 0; j < next.Length; ++j)
                {
                    next[j] = derivCoeffs[j] * (d - j);
                }
                derivCoeffs = next;

                // Evaluate derivCoeffs at r using Horner
                Complex val = HornerEval(derivCoeffs, r);
                if (val.Magnitude > effectiveTol)
                {
                    // compute v = p^{(m)}(r)/m!
                    double mfact = FactorialDouble(k); // or compute progressively if performance needed
                    Complex v = val / mfact;
                    double angle = System.Math.Atan2(v.Imaginary, v.Real); // -pi..pi
                    return (k, v, angle);
                }
            }

            // all derivatives numerically zero -> polynomial zero (or root of multiplicity > degree)
            return (0, Complex.Zero, 0.0);
        }

        private static Complex HornerEval(Complex[] coeffs, Complex x)
        {
            Complex acc = Complex.Zero;
            foreach (var c in coeffs)
            {
                acc = acc * x + c;
            }
            return acc;
        }

        private static double FactorialDouble(int k)
        {
            double f = 1.0;
            for (int i = 2; i <= k; ++i) f *= i;
            return f;
        }
    }

    public class SolutionPoint
    {
        public Complex root;

        public double angle;
    }
}

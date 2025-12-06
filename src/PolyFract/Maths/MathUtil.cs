using System.Numerics;

namespace PolyFract.Maths
{
    public static class MathUtil
    {
        public static long IntegerPower(long x, long p)
        {
            long res = 1;
            for (int i = 0; i < p; i++)
                res *= x;
            return res;
        }

        public static bool IsInSquare(double x, double y, double cx, double cy, double r)
        {
            return (x >= cx - r) && (x <= cx + r) && (y >= cy - r) && (y <= cy + r);
        }

        public static float FastAtan2(float y, float x)
        {
            const float ONEQTR_PI = (float)Math.PI / 4f;
            const float THRQTR_PI = 3f * ONEQTR_PI;

            float r, angle;

            // Avoid divide-by-zero
            float absY = Math.Abs(y) + 1e-10f;

            if (x < 0)
            {
                r = (x + absY) / (absY - x);
                angle = THRQTR_PI;
            }
            else
            {
                r = (x - absY) / (x + absY);
                angle = ONEQTR_PI;
            }

            angle += (0.1963f * r * r - 0.9817f) * r;
            return (y < 0) ? -angle : angle;
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

        public static Complex EvaluateDerivative(Complex[] coeffsDescending, Complex z)
        {
            int n = coeffsDescending.Length - 1;
            Complex d = Complex.Zero;

            for (int i = 0; i < n; i++)
            {
                d = d * z + coeffsDescending[i] * (n - i);
            }
            return d;
        }

        public static double AngleAt(Complex[] coeffs, Complex x)
        {
            var derivative = MathUtil.EvaluateDerivative(coeffs, x);
            return MathUtil.UltraFastAtan2(derivative.Imaginary, derivative.Real);
        }

        public static Complex HornerEval(Complex[] coeffs, Complex x)
        {
            Complex acc = Complex.Zero;
            foreach (var c in coeffs)
            {
                acc = acc * x + c;
            }
            return acc;
        }

        public static double FactorialDouble(int k)
        {
            double f = 1.0;
            for (int i = 2; i <= k; ++i) f *= i;
            return f;
        }
    }
}

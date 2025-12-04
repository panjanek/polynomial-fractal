using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolyFract.Math
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
    }
}

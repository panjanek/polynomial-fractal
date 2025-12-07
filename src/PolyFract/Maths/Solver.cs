using System.Numerics;

namespace PolyFract.Maths
{
    public class Solver
    {
        public int order;

        public int coefficientsValuesCount;

        public int polynomialsCount;

        public ThreadContext[] threads;

        public Solver(int coefficientsValuesCount, int order)
        {
            this.coefficientsValuesCount = coefficientsValuesCount;
            this.order = order;
            polynomialsCount = 1;
            for (int i = 0; i < order + 1; i++)
                polynomialsCount *= coefficientsValuesCount;
            int rootsCount = polynomialsCount * order;

            int threadCount = Environment.ProcessorCount;
            int polysPerThread = polynomialsCount / threadCount;
            threads = new ThreadContext[threadCount];
            for(int t=0; t<threads.Length; t++)
            {
                threads[t] = new ThreadContext();
                threads[t].order = order;
                threads[t].from = t * polysPerThread;
                threads[t].to = (t + 1) * polysPerThread;
                threads[t].poly = new Complex[order + 1];
                threads[t].durandHelper = new DurandKernerHelper(order);
                if (t == threads.Length - 1)
                {
                    threads[t].to = polynomialsCount;
                }

                int rootsInThisThread = (threads[t].to - threads[t].from) * order;
                threads[t].real = new double[rootsInThisThread];
                threads[t].imaginary = new double[rootsInThisThread];
                threads[t].angle = new double[rootsInThisThread];
            }
        }

        public void Solve(Complex[] coefficients)
        {
            if (coefficients.Length != coefficientsValuesCount)
                throw new Exception($"Solver created for {coefficientsValuesCount} coefficients count but got {coefficients.Length}");

            foreach (var thread in threads)
                thread.coeffs = coefficients.Select(x => new Complex(x.Real, x.Imaginary)).ToArray();

            Parallel.ForEach(threads, ctx => ctx.Run());
        }
        public int GetErrorsCount()
        {
            var errRate = threads.Sum(t => t.errorsCount);
            return errRate;
        }
    }

    public class ThreadContext
    {
        public int from;

        public int to;

        public Complex[] poly;

        public Complex[] coeffs;

        public int order;

        public DurandKernerHelper durandHelper;

        public int errorsCount;

        public double[] real;

        public double[] imaginary;

        public double[] angle;

        public void Run()
        {
            errorsCount = 0;
            for (int i = from; i < to; i++)
            {
                int polyIdx = i;
                for (int j = 0; j < poly.Length; j++)
                {
                    int coeffIdx = polyIdx % coeffs.Length;
                    polyIdx = polyIdx / coeffs.Length;
                    poly[j] = coeffs[coeffIdx];
                }

                var roots = durandHelper.FindRoots(poly);
                int threadTargetFirstIdx = (i - from) * order;
                for (int j = 0; j < roots.Length; j++)
                {
                    var root = roots[j];
                    int threadTargetIdx = threadTargetFirstIdx + j;

                    var test = PolyUtil.EvalPoly(poly, root);
                    if (test.Magnitude > 0.0001)
                    {
                        real[threadTargetIdx] = double.MinValue;
                        imaginary[threadTargetIdx] = double.MinValue;
                        errorsCount++;
                    }
                    else
                    {
                        real[threadTargetIdx] = root.Real;
                        imaginary[threadTargetIdx] = root.Imaginary;
                    }

                    angle[threadTargetIdx] = MathUtil.AngleAt(poly, root);
                }
            }
        }
    }
}

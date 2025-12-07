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
                threads[t].poly_r = new double[order + 1];
                threads[t].poly_i = new double[order + 1];
                threads[t].coeffs_r = new double[coefficientsValuesCount];
                threads[t].coeffs_i = new double[coefficientsValuesCount];
                threads[t].durandHelper = new FastDurandKernerHelperNoComplex(order);
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
            {
                for (int c = 0; c < coefficients.Length; c++)
                {
                    thread.coeffs_r[c] = coefficients[c].Real;
                    thread.coeffs_i[c] = coefficients[c].Imaginary;
                }
            }

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

        public double[] poly_r;

        public double[] poly_i;

        public double[] coeffs_r;

        public double[] coeffs_i;

        public int order;

        public FastDurandKernerHelperNoComplex durandHelper;

        public int errorsCount;

        public double[] real;

        public double[] imaginary;

        public double[] angle;

        public void Run()
        {
            durandHelper.FindRootsForPolys(from, to, coeffs_r, coeffs_i, real, imaginary, angle);
            errorsCount = real.Count(r => r == double.MinValue);
        }
    }
}

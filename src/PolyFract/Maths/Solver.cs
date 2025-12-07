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
            errorsCount = 0;
            for (int i = from; i < to; i++)
            {
                int polyIdx = i;
                for (int j = 0; j < poly_r.Length; j++)
                {
                    int coeffIdx = polyIdx % coeffs_r.Length;
                    polyIdx = polyIdx / coeffs_r.Length;
                    poly_r[j] = coeffs_r[coeffIdx];
                    poly_i[j] = coeffs_i[coeffIdx];
                }

                durandHelper.FindRoots(poly_r, poly_i);
                int threadTargetFirstIdx = (i - from) * order;
                for (int j = 0; j < durandHelper._z_r.Length; j++)
                {
                    var root_r = durandHelper._z_r[j];
                    var root_i = durandHelper._z_i[j];
                    int threadTargetIdx = threadTargetFirstIdx + j;
                    angle[threadTargetIdx] = durandHelper._z_a[j];
                    if (root_r == double.MinValue)
                    {
                        real[threadTargetIdx] = double.MinValue;
                        imaginary[threadTargetIdx] = double.MinValue;
                        errorsCount++;
                    }
                    else
                    {
                        real[threadTargetIdx] = root_r;
                        imaginary[threadTargetIdx] = root_i;
                    }
                    
                    
                }
            }
        }
    }
}

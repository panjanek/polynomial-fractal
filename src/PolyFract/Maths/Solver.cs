using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace PolyFract.Maths
{
    public class Solver
    {
        public const int ThreadCount = 32;

        public double[] real;

        public double[] imaginary;

        public double[] angle;

        public int order;

        public int coefficientsValuesCount;

        public int polynomialsCount;

        private ThreadContext[] threads;

        public Solver(int coefficientsValuesCount, int order)
        {
            this.coefficientsValuesCount = coefficientsValuesCount;
            this.order = order;
            polynomialsCount = 1;
            for (int i = 0; i < order + 1; i++)
                polynomialsCount *= coefficientsValuesCount;
            int rootsCount = polynomialsCount * order;
            real = new double[rootsCount];
            imaginary = new double[rootsCount];
            angle = new double[rootsCount];

            int polysPerThread = polynomialsCount / ThreadCount;
            threads = new ThreadContext[ThreadCount];
            for(int t=0; t<threads.Length; t++)
            {
                threads[t] = new ThreadContext();
                threads[t].From = t * polysPerThread;
                threads[t].To = (t + 1) * polysPerThread;
                threads[t].Poly = new Complex[order + 1];
                threads[t].DurandHelper = new DurandKernerHelper(order);
                if (t == threads.Length - 1)
                {
                    threads[t].To = polynomialsCount;
                }
            }
        }

        public void Solve(Complex[] coefficients)
        {
            if (coefficients.Length != coefficientsValuesCount)
                throw new Exception($"Solver created for {coefficientsValuesCount} coefficients count but got {coefficients.Length}");

            Parallel.For(0, threads.Length, new ParallelOptions() { MaxDegreeOfParallelism = threads.Length }, t =>
            {
                var context = threads[t];
                for (int i = context.From; i < context.To; i++)
                {
                    int polyIdx = i;
                    for (int j = 0; j < context.Poly.Length; j++)
                    {
                        int coeffIdx = polyIdx % coefficients.Length;
                        polyIdx = polyIdx / coefficients.Length;
                        context.Poly[j] = coefficients[coeffIdx];
                    }

                    var roots = context.DurandHelper.FindRoots(context.Poly);
                    int firstRootIdx = i * order;
                    for (int j = 0; j < roots.Length; j++)
                    {
                        int idx = firstRootIdx + j;
                        real[idx] = roots[j].Real;
                        imaginary[idx] = roots[j].Imaginary;
                        angle[idx] = MathUtil.AngleAt(context.Poly, roots[j]);
                    }
                }
            });
         }
    }

    public class ThreadContext
    {
        public int From;

        public int To;

        public Complex[] Poly;

        public DurandKernerHelper DurandHelper;
    }
}

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

            int threadCount = Environment.ProcessorCount;
            int polysPerThread = polynomialsCount / threadCount;
            threads = new ThreadContext[threadCount];
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

                int rootsInThisThread = (threads[t].To - threads[t].From) * order;
                threads[t].real = new double[rootsInThisThread];
                threads[t].imaginary = new double[rootsInThisThread];
                threads[t].angle = new double[rootsInThisThread];
            }
        }

        public void Solve(Complex[] coefficients)
        {
            if (coefficients.Length != coefficientsValuesCount)
                throw new Exception($"Solver created for {coefficientsValuesCount} coefficients count but got {coefficients.Length}");

            Parallel.For(0, threads.Length, new ParallelOptions() { MaxDegreeOfParallelism = threads.Length }, t =>
            {
                var context = threads[t];
                context.errorsCount = 0;
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
                    int threadTargetFirstIdx = (i - context.From)  * order;
                    for (int j = 0; j < roots.Length; j++)
                    {
                        var root = roots[j];
                        int threadTargetIdx = threadTargetFirstIdx + j;

                        var test = PolyUtil.EvalPoly(context.Poly, root);
                        if (test.Magnitude > 0.0001)
                        {
                            context.real[threadTargetIdx] = -1000;
                            context.imaginary[threadTargetIdx] = -1000;
                            context.errorsCount++;
                        }
                        else
                        {
                            context.real[threadTargetIdx] = root.Real;
                            context.imaginary[threadTargetIdx] = root.Imaginary;
                            
                        }

                        context.angle[threadTargetIdx] = MathUtil.AngleAt(context.Poly, root);
                    }
                }
            });

            for(int t=0; t<threads.Length; t++)
            {
                var thread = threads[t];
                int targetIdx = thread.From * order;
                Array.Copy(thread.real, 0, real, targetIdx, thread.real.Length);
                Array.Copy(thread.imaginary, 0, imaginary, targetIdx, thread.imaginary.Length);
                Array.Copy(thread.angle, 0, angle, targetIdx, thread.angle.Length);
            }
         }

        public int GetErrorsCount()
        {
            var errRate = threads.Sum(t => t.errorsCount);
            return errRate;
        }
    }

    public class ThreadContext
    {
        public int From;

        public int To;

        public Complex[] Poly;

        public DurandKernerHelper DurandHelper;

        public int errorsCount;

        public double[] real;

        public double[] imaginary;

        public double[] angle;
    }
}

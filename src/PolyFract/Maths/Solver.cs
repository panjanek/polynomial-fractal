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
            if (polynomialsCount < 10 * threadCount)
                threadCount = polynomialsCount / 10;
            if (threadCount == 0)
                threadCount = 1;

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
                if (t == threads.Length - 1)
                {
                    threads[t].to = polynomialsCount;
                }

                // buffer for thread output
                int rootsInThisThread = (threads[t].to - threads[t].from) * order;
                threads[t].real = new double[rootsInThisThread];
                threads[t].imaginary = new double[rootsInThisThread];
                threads[t].angle = new double[rootsInThisThread];
                threads[t].color_r = new int[rootsInThisThread];
                threads[t].color_g = new int[rootsInThisThread];
                threads[t].color_b = new int[rootsInThisThread];
                threads[t].pixel_x = new int[rootsInThisThread];
                threads[t].pixel_y = new int[rootsInThisThread];

                //buffers for solving algorithm
                threads[t]._monic_r = new double[order + 1];
                threads[t]._monic_i = new double[order + 1];
                threads[t]._z_r = new double[order];
                threads[t]._z_i = new double[order];
                threads[t]._z_a = new double[order];
                threads[t]._newZ_r = new double[order];
                threads[t]._newZ_i = new double[order];
                threads[t]._poly_r = new double[order];
                threads[t]._poly_i = new double[order];
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
        // numbered polynomials for this thread
        public int from;
        public int to;

        // max degree of polynomial
        public int order;

        //number of roots that were incorrectly estimated (should be below 1%)
        public int errorsCount;

        // coefficient values to be used
        public double[] coeffs_r;
        public double[] coeffs_i;

        // preallocated buffer for actual polynomial
        public double[] poly_r;
        public double[] poly_i;

        // output of the solver
        public double[] real;
        public double[] imaginary;
        public double[] angle;
        public int[] color_r;
        public int[] color_g;
        public int[] color_b;

        // variables allocated for DurandKerner helper
        public double[] _poly_r;
        public double[] _poly_i;
        public double[] _monic_r;
        public double[] _monic_i;
        public double[] _z_r;
        public double[] _z_i;
        public double[] _z_a;
        public double[] _newZ_r;
        public double[] _newZ_i;

        //pre allocated buffer for pixel coords
        public int[] pixel_x;
        public int[] pixel_y;

        public void Run()
        {
            if (FastDurandKernerHelperNoComplex.IsNativeLibAvailable)
            {
                FastDurandKernerHelperNoComplex.FindRootsForPolys(
                          //actual parameters
                          from,
                          to,
                          coeffs_r,
                          coeffs_i,
                          coeffs_r.Length,

                          // pre-allocated buffer for numbered polynomials
                          _poly_r,
                          _poly_i,
                          _poly_r.Length,

                          // pre-allocated buffer for durand-kerner implementation
                          _monic_r,
                          _monic_i,
                          _z_r,
                          _z_i,
                          _z_a,
                          _newZ_r,
                          _newZ_i,

                          //output
                          real,
                          imaginary,
                          angle,
                          color_r,
                          color_g,
                          color_b);
            }
            else
            {
                FastDurandKernerHelperNoComplex.FindRootsForPolysManaged(
                              //actual parameters
                              from,
                              to,
                              coeffs_r,
                              coeffs_i,

                              // pre-allocated buffer for numbered polynomials
                              _poly_r,
                              _poly_i,

                              // pre-allocated buffer for durand-kerner implementation
                              _monic_r,
                              _monic_i,
                              _z_r,
                              _z_i,
                              _z_a,
                              _newZ_r,
                              _newZ_i,

                              //output
                              real,
                              imaginary,
                              angle,
                              color_r,
                              color_g,
                              color_b);
            }

            errorsCount = real.Count(r => r == FastDurandKernerHelperNoComplex.ErrorMarker);
        }
    }
}

using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Controls.Ribbon;
using System.Windows.Media.TextFormatting;
using PolyFract.Gui;

namespace PolyFract.Maths
{
    public class Solver
    {
        public int order;

        public int coefficientsValuesCount;

        public int polynomialsCount;

        public int rootsCount;

        public bool coeffsVisible;

        public CompactClomplexFloatWithColor[] coeffValues;

        public ThreadContext[] threads;

        public Solver(int coefficientsValuesCount, int order)
        {
            this.coefficientsValuesCount = coefficientsValuesCount;
            this.coeffValues = new CompactClomplexFloatWithColor[coefficientsValuesCount];
            this.order = order;
            polynomialsCount = 1;
            for (int i = 0; i < order + 1; i++)
                polynomialsCount *= coefficientsValuesCount;
            rootsCount = polynomialsCount * order;     

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
                threads[t].poly = new CompactClomplex[order + 1];
                threads[t].coeffs = new CompactClomplex[coefficientsValuesCount];
                if (t == threads.Length - 1)
                {
                    threads[t].to = polynomialsCount;
                }

                // buffer for thread output
                int rootsInThisThread = (threads[t].to - threads[t].from) * order;
                threads[t].roots = new CompactClomplexFloatWithColor[rootsInThisThread];

                //buffer for renderer
                threads[t].pixels = new CompactPixel[rootsInThisThread];

                //buffers for solving algorithm
                threads[t]._monic = new CompactClomplex[order + 1];
                threads[t]._z = new CompactClomplexWithAngle[order];
                threads[t]._newZ = new CompactClomplex[order];
                threads[t]._poly = new CompactClomplex[order+1];
            }
        }

        public void Solve(Complex[] coefficients)
        {
            if (coefficients.Length != coefficientsValuesCount || coefficients.Length != coeffValues.Length)
                throw new Exception($"Solver created for {coefficientsValuesCount} coefficients count but got {coefficients.Length}");

            for(int c=0; c<coefficients.Length; c++)
            {
                coeffValues[c].r = (float)coefficients[c].Real;
                coeffValues[c].i = (float)coefficients[c].Imaginary;
                coeffValues[c].colorR = 255.0f;
                coeffValues[c].colorG = 255.0f;
                coeffValues[c].colorB = 255.0f;
            }

            if (OpenGlSurface.UseComputeShader) // if shader computing enabled, skip solving completely
                return;

            foreach (var thread in threads)
            {
                for (int c = 0; c < coefficients.Length; c++)
                {
                    thread.coeffs[c].r = coefficients[c].Real;
                    thread.coeffs[c].i = coefficients[c].Imaginary;
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
        public CompactClomplex[] coeffs;

        // preallocated buffer for actual polynomial
        public CompactClomplex[] poly;

        // output of the solver
        public CompactClomplexFloatWithColor[] roots;

        // variables allocated for DurandKerner helper
        public CompactClomplex[] _poly;
        public CompactClomplex[] _monic;
        public CompactClomplexWithAngle[] _z;
        public CompactClomplex[] _newZ;

        //pre allocated buffer for pixel coords
        public CompactPixel[] pixels;

        public void Run()
        {
            if (Polynomials.IsNativeLibAvailable)
            {
                Polynomials.FindRootsForPolys(
                          //actual parameters
                          from,
                          to,
                          coeffs,
                          coeffs.Length,

                          // pre-allocated buffer for numbered polynomials
                          _poly,
                          _poly.Length,

                          // pre-allocated buffer for durand-kerner implementation
                          _monic,
                          _z,
                          _newZ,

                          //output
                          roots);
            }
            else
            {
                Polynomials.FindRootsForPolysManaged(
                              //actual parameters
                              from,
                              to,
                              coeffs,

                              // pre-allocated buffer for numbered polynomials
                              _poly,

                              // pre-allocated buffer for durand-kerner implementation
                              _monic,
                              _z,
                              _newZ,

                              //output
                              roots);
            }

            errorsCount = roots.Count(x => x.r == Polynomials.ErrorMarker);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CompactPixel
    {
        public int x;
        public int y;
        public int r;
        public int g;
        public int b;
    }
}

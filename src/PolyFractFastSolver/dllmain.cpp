// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#define _USE_MATH_DEFINES
#include <cmath>

#include <omp.h>

#define MaxIterations (32)
#define Tolerance     (1e-10)
#define ErrorMargin   (0.0001)
#define ErrorMarker   (1000000)


// Use __restrict (MSVC/GCC compatible form) to help optimizer
#if defined(_MSC_VER)
#  define RESTRICT __restrict
#else
#  define RESTRICT __restrict__
#endif

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

extern "C"
{
    __declspec(dllexport)
    void TestNative(const double* input, int length, double* output)
    {
        // Example computation:
        for (int i = 0; i < length; ++i)
        {
            output[i] = input[i] * 2.0;  
        }
    }
}


static inline double mag2(double xr, double xi) noexcept {
    return xr * xr + xi * xi;
}
static inline double Magnitude(double xr, double xi) noexcept {
    return std::sqrt(mag2(xr, xi));
}

/*
double Magnitude(double a, double b)
{
    // Using
    //   sqrt(a^2 + b^2) = |a| * sqrt(1 + (b/a)^2)
    // we can factor out the larger component to dodge overflow even when a * a would overflow.

    a = std::abs(a);
    b = std::abs(b);

    double small, large;
    if (a < b)
    {
        small = a;
        large = b;
    }
    else
    {
        small = b;
        large = a;
    }

    if (small == 0.0)
    {
        return (large);
    }
    else
    {
        double ratio = small / large;
        return (large * std::sqrt(1.0 + ratio * ratio));
    }
}*/

double UltraFastAtan2(double y, double x)
{
    double absY = std::abs(y) + 1e-10f;

    double angle;
    if (x >= 0)
    {
        double r = (x - absY) / (x + absY);
        angle = (double)(M_PI / 4) - (0.9675 * r);
    }
    else
    {
        double r = (x + absY) / (absY - x);
        angle = (double)(3 * M_PI / 4) - (0.9675 * r);
    }

    return (y < 0) ? -angle : angle;
}

double AngleAt(double* coeffs_r, double* coeffs_i, int coeffs_len, double x_r, double x_i)
{
    //evaluate derivative at x
    int n = coeffs_len - 1;
    double d_r = 0;
    double d_i = 0;

    for (int i = 0; i < n; i++)
    {
        double d_r_tmp = d_r * x_r - d_i * x_i + coeffs_r[i] * (n - i);
        double d_i_tmp = d_r * x_i + d_i * x_r + coeffs_i[i] * (n - 1);
        d_r = d_r_tmp;
        d_i = d_i_tmp;
    }

    //simplified angle of d
    return UltraFastAtan2(d_i, d_r);
}

void FindRoots(
    //polynomial to solve
    double* poly_r,
    double* poly_i,
    int poly_len,

    // preallocated buffers
    double* _monic_r,
    double* _monic_i,
    double* _z_r,
    double* _z_i,
    double* _z_a,
    double* _newZ_r,
    double* _newZ_i)
{
    if (poly_len < 2)
        return;

    int n = poly_len - 1;
    double a0_r = poly_r[0];
    double a0_i = poly_i[0];
    if (a0_r == 0 && a0_i == 0)
        return;

    const double a0_mag2 = a0_r * a0_r + a0_i * a0_i;
    const double inv_a0_mag2 = 1.0 / a0_mag2;

    // ---- Build monic coefficients into reusable _monic ----
    // monic: [1, b1, ..., bn] for z^n + b1*z^(n-1) + ... + bn
    _monic_r[0] = 1;
    _monic_i[0] = 0;

    /*
    for (int i = 1; i <= n; i++)
    {
        //_monic[i] = coeffsDescending[i] / a0;
        double div = a0_r * a0_r + a0_i * a0_i;
        _monic_r[i] = (poly_r[i] * a0_r + poly_i[i] * a0_i) / div;
        _monic_i[i] = (poly_i[i] * a0_r - poly_r[i] * a0_i) / div;
    }*/

    for (int i = 1; i <= n; ++i) {
        // complex division poly[i] / a0  => (p * conj(a0)) / |a0|^2
        const double pr = poly_r[i];
        const double pi = poly_i[i];
        // multiply by conj(a0) = (a0_r - i*a0_i)
        const double num_r = pr * a0_r + pi * a0_i;
        const double num_i = pi * a0_r - pr * a0_i;
        _monic_r[i] = num_r * inv_a0_mag2;
        _monic_i[i] = num_i * inv_a0_mag2;
    }

    // ---- Initial radius ----
    double maxAbs = 0.0;
    for (int i = 1; i <= n; i++)
    {
        //double m = Magnitude(_monic_r[i], _monic_i[i]); // _monic[i].Magnitude;
        const double m2 = mag2(_monic_r[i], _monic_i[i]);
        if (m2 > maxAbs * maxAbs) 
            maxAbs = std::sqrt(m2);
    }
    double r = 1.0 + maxAbs;

    // ---- Initial guesses in reusable _z ----
    double twoPiOverN = 2.0 * M_PI / n;
    for (int k = 0; k < n; k++)
    {
        double angle = twoPiOverN * k;

        //_z[k] = Complex.FromPolarCoordinates(r, angle);
        _z_r[k] = r * std::cos(angle);
        _z_i[k] = r * std::sin(angle);
    }

    // ---- Iterations using _z and _newZ ----
    for (int iter = 0; iter < MaxIterations; iter++)
    {
        double maxDelta2 = 0.0;
        for (int i = 0; i < n; i++)
        {
            //Complex zi = _z[i];
            double zi_r = _z_r[i];
            double zi_i = _z_i[i];

            // Horner evaluation with monic coeffs
            // Complex p = _monic[0];
            double p_r = _monic_r[0];
            double p_i = _monic_i[0];
            for (int k = 1; k <= n; k++)
            {
                //p = p * zi + _monic[k];
                double p_r_tmp = p_r * zi_r - p_i * zi_i + _monic_r[k];
                double p_i_tmp = p_r * zi_i + p_i * zi_r + _monic_i[k];
                p_r = p_r_tmp;
                p_i = p_i_tmp;
            }

            //Complex denom = Complex.One;
            double denom_r = 1;
            double denom_i = 0;
            for (int j = 0; j < n; j++)
            {
                if (j == i)
                    continue;

                //denom *= (zi - _z[j]);
                double mult_r = zi_r - _z_r[j];
                double mult_i = zi_i - _z_i[j];
                double denom_r_tmp = denom_r * mult_r - denom_i * mult_i;
                double denom_i_tmp = denom_r * mult_i + denom_i * mult_r;
                denom_r = denom_r_tmp;
                denom_i = denom_i_tmp;
            }

            //Complex delta = p / denom;
            double div = denom_r * denom_r + denom_i * denom_i;
            double delta_r = (p_r * denom_r + p_i * denom_i) / div;
            double delta_i = (p_i * denom_r - p_r * denom_i) / div;

            //Complex ziNew = zi - delta;
            double ziNew_r = zi_r - delta_r;
            double ziNew_i = zi_i - delta_i;

            //_newZ[i] = ziNew;
            _newZ_r[i] = ziNew_r;
            _newZ_i[i] = ziNew_i;

            //double d = delta.Magnitude;
            //double d = Magnitude(delta_r, delta_i);
            double d2 = mag2(delta_r, delta_i);
            //if (d > maxDelta) maxDelta = d;
            if (d2 > maxDelta2) maxDelta2 = d2;
        }

        // swap buffers (_z <= _newZ)
        for (int i = 0; i < n; i++)
        {
            //_z[i] = _newZ[i];
            _z_r[i] = _newZ_r[i];
            _z_i[i] = _newZ_i[i];
        }

        //if (maxDelta < Tolerance) break;
        if (maxDelta2 < (Tolerance * Tolerance)) break;
    }

    //compute angles
    for (int i = 0; i <= n; i++)
    {
        _z_a[i] = AngleAt(poly_r, poly_i, poly_len, _z_r[i], _z_i[i]);
    }

    //remove errors
    for (int i = 0; i <= n; i++)
    {
        double r_r = _z_r[i];
        double r_i = _z_i[i];

        double v_r = poly_r[0];
        double v_i = poly_i[0];
        for (int j = 1; j <= n; j++)
        {
            //v = v * r + coeffsDescending[j];
            double v_r_tmp = v_r * r_r - v_i * r_i + poly_r[j];
            double v_i_tmp = v_r * r_i + v_i * r_r + poly_i[j];
            v_r = v_r_tmp;
            v_i = v_i_tmp;
        }

        double v_m = Magnitude(v_i, v_r);
        if (v_m > ErrorMargin)
            _z_r[i] = ErrorMarker;
    }
}

extern "C"
{
    __declspec(dllexport)
    void FindRootsForPolys(
            //actual parameters
            int from,
            int to,
            double* coeffsvalues_r,
            double* coeffsvalues_i,
            int coeffsvalues_len,

            //preallocated buffer for numbered polynomials
            double* _poly_r,
            double* _poly_i,
            int _poly_len,

            //preallocated buffer for kerner-durand
            double* _monic_r,
            double* _monic_i,
            double* _z_r,
            double* _z_i,
            double* _z_a,
            double* _newZ_r,
            double* _newZ_i,

            //outputs
            double* roots_r,
            double* roots_i,
            double* roots_a)
    {
        //int order = _poly_len - 1;
        //#pragma omp parallel
        //omp_set_num_threads(2);
        for (int i = from; i < to; i++)
        {
            int polyIdx = i;
            for (int j = 0; j < _poly_len; j++)
            {
                int coeffIdx = polyIdx % coeffsvalues_len;
                polyIdx = polyIdx / coeffsvalues_len;
                _poly_r[j] = coeffsvalues_r[coeffIdx];
                _poly_i[j] = coeffsvalues_i[coeffIdx];
            }

            FindRoots(
                _poly_r,
                _poly_i,
                _poly_len,

                _monic_r,
                _monic_i,
                _z_r,
                _z_i,
                _z_a,
                _newZ_r,
                _newZ_i);

            int targetFirstIdx = (i - from) * _poly_len;
            for (int j = 0; j < _poly_len; j++)
            {
                int targetIdx = targetFirstIdx + j;
                roots_r[targetIdx] = _z_r[j];
                roots_i[targetIdx] = _z_i[j];
                roots_a[targetIdx] = _z_a[j];
            }
        }
    }
}



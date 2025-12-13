# Polynomial Fractal Visualisation
GPU-accelerated (with compute shader) .NET GUI application (WPF) for visalisation roots of polynomials with constrained coefficients.

Inspired by this youtube video by 2swap: https://www.youtube.com/watch?v=9HIy5dJE-zQ&t=3s

## Features
* Any order of polynomial
* Any number of coefficients contraints
* 1mln+ roots rendered smoothly (60fps on modern GPUs)
* Draggable and zoomable surface
* Draggable coefficients
* Configurable with right click context menu
* Solver implemented in compute shader (fallback to native code if OpenGL not available)
* OpenGL rendering
* Durand–Kerner (Weierstrass) method of root finding
* Seriously, watch the video.

## Example captures

![order 13, coeffs 2](https://github.com/panjanek/polynomial-fractal/blob/a5dbe956196074dc35873e9517b88e9b5c0456f3/captures/order13coeff2.png "order 13, coeff 2, coeffs 3")
![order 13, coeffs 2](https://github.com/panjanek/polynomial-fractal/blob/a5dbe956196074dc35873e9517b88e9b5c0456f3/captures/order10-zoomed.png "order 10, coeffs 2")
![order 10, coeffs 3, zoomed](https://github.com/panjanek/polynomial-fractal/blob/a5dbe956196074dc35873e9517b88e9b5c0456f3/captures/order10coeff3.png "order 10, coeffs 3, zoomed")
![order 13, coeffs 2](https://github.com/panjanek/polynomial-fractal/blob/332b6bc757b8e5c534c03f474f37aecd8bce32e1/captures/context-menu.png "order 10, coeffs 2")


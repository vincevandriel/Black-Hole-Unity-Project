# M3 isolated Schwarzschild optical control

This control traces light around one stationary, nonspinning black hole. It is a separate validation scene/model, not a renderer for the SXS binary or its spinning remnant. The implementation is [`SchwarzschildReference.cs`](../BinaryBlackHole/Assets/DTET/Simulation/SchwarzschildReference.cs), shared by Unity and the portable tests.

## Reference and implementation choice

Eric Bruneton's [author page](https://ebruneton.github.io/black_hole_shader/) describes a real-time Schwarzschild renderer based on precomputed beam-deflection/intersection tables and filtered background sources. The [2020 primary paper](https://arxiv.org/html/2010.08735v1), particularly equations 1, 2 and 8 and section 3.4.2, supplies the metric, local observer frame and frequency/intensity conventions. Its thin disk is an optical demonstration, not a solved fluid evolution. The author's [preprocessing implementation](https://ebruneton.github.io/black_hole_shader/black_hole/preprocess/functions.cc.html) explains comparison of lookup outputs with directly integrated reference rays.

M3 implements an independent double-precision RK4 integration of the same Schwarzschild null-ray equation, in gravitational-radius units. This gives a small, allocation-free control suitable for validating a rendering backend. It **does not port Bruneton's lookup tables, beam filtering, moving-observer implementation or shaders**. Constant-time lookup acceleration remains deferred; published Bruneton frame rates do not apply to this implementation. No upstream code or assets were copied into the C# control.

The earlier source review pinned upstream commit `e72b3f293409893a6fa25528b29572c96fc57f57` (11 November 2020). Selected sources and license remain in the external research library under `software/bruneton/`. The upstream BSD notice is retained below so a later selected-code port can preserve the required terms. This note is not a license for Gaia imagery or other upstream assets.

## Units and ray initialization

Let `M` as a length mean `GM_physical/c²`. Set `G=c=M=1`, so the horizon is at `r=2`, the photon sphere at `r=3`, and the Schwarzschild ISCO at `r=6`. Bruneton instead sets the Schwarzschild radius to one; therefore his radii and impact parameters are half the values used here, and his inverse radius is twice ours.

The Schwarzschild lapse is `A(r)=1−2/r`. An exterior static observer supplies a unit backward ray direction in its local orthonormal frame, expressed along the radial and tangential directions of the scene axes. This is not a coordinate velocity. For radial direction cosine `n_r` and tangential magnitude `n_t`, initialize

```text
b = r_camera n_t / sqrt(A_camera)
u = 1 / r_camera
du/dphi = -n_r / b
```

Spherical symmetry keeps each ray in the plane spanned by its initial radial and tangential vectors. Integrate

```text
d²u/dphi² = 3u² − u
(du/dphi)² + u²(1 − 2u) = 1/b²
position(phi) = [cos(phi)e_r + sin(phi)e_t] / u(phi)
```

The first integral is monitored independently from the evolution update. `MaximumNullResidual` is the maximum absolute value of `b²[(du/dphi)²+u²(1−2u)]−1` over visited integration points. A low residual alone does not prove endpoint/image accuracy; quadrature and refinement checks are also required.

The critical impact parameter is `b_crit=3 sqrt(3)`. Under the author-unit conversion, `(2/b_crit)²=4/27`, matching Bruneton's critical inverse-impact value. Radial rays use their exact angular solution rather than divide by zero.

## Domain, work limits and outputs

`Trace` handles vacuum capture/escape. `TraceDisk` additionally intersects an opaque, infinitesimally thin annulus whose unit normal can have any orientation. Its inner radius must be at least six and its outer radius must lie inside the escape boundary. The first valid disk hit terminates the ray; it is not an optically thin sum of intersections or a 3D absorbing volume.

The observer can be anywhere outside `r=2.000001`, including inside the photon sphere. Each camera placement denotes a new static observer held at that position. Camera translation is inspection repositioning; it does not automatically introduce a timelike orbit, aberration or camera Doppler velocity. Horizon interiors are unsupported.

The default control uses angular increments at most `0.005 rad`, a fractional-radial-change limit of `0.04`, at most 8,192 steps, and a finite escape sphere at 1,000 M. Capture terminates at `2.000001 M`, just outside the horizon. Boundary events are refined within the last step, and disk-plane angles are located exactly before evaluating the radial solution. Escaped directions refer to that finite boundary, not an analytically extrapolated point at infinity.

Outcomes are `Captured`, `Escaped`, `DiskHit`, `StepLimit` and `CoplanarDisk`. The last two must remain identifiable as unavailable/unresolved samples; a renderer must not silently count them as captured black rays. An exactly coplanar ray in an infinitely thin opaque sheet has no unique surface intersection and returns `CoplanarDisk`. A finite-thickness volume is the separate inspection/emission control. Small angular or radial tolerance limits are numerical choices, not physical uncertainty estimates.

## Frequency and intensity

For two static observers, `g=nu_observed/nu_emitted=sqrt(A_emitter/A_observer)`. A stable circular disk emitter has local speed `beta=1/sqrt(r_emitter−2)` and Lorentz factor `gamma=1/sqrt(1−beta²)`. For forward photon direction dotted with the emitter's unit velocity `mu`,

```text
g = sqrt(A_emitter/A_observer) / [gamma (1 − beta mu)]
```

The disk rotation is prograde about its normal. The implementation obtains `mu` from the bent ray at the actual intersection and reverses the backward tracing direction before taking the dot product. This avoids reversing the bright side. Face-on, transverse Doppler and gravitational effects combine into `sqrt[(1−3/r_emitter)/A_observer]`; they must not be applied again in a material.

For vacuum transfer, a rendering layer may use `I_nu,obs(nu)=g³ I_nu,em(nu/g)`; the frequency-integrated factor is `g⁴`. A shifted thermal spectrum is evaluated at the shifted temperature, consistently with this invariant, rather than independently multiplying overlapping colour and intensity corrections. The C# optical control returns `g`; it does not choose exposure, band, opacity, spectral colour or a gas temperature. Those belong to the separately documented emission model.

## Verification contract

[`SchwarzschildReferenceTests.cs`](../Staging/Simulation.Tests/SchwarzschildReferenceTests.cs) checks radial rays inside/outside the photon sphere, capture/escape on both sides of the critical impact parameter, the null first integral, explicit work exhaustion, rotations, disk views from above/below, frequency-shift signs and zero managed allocation per ray.

The independent finite-boundary scattering check uses composite Simpson quadrature of the first integral, not another RK4 solver. For turning point `p>3`, set `x=p/r=1−t²` to remove the endpoint square-root singularity. The total swept angle from an outer sphere of radius `R` to the turning point and back is

```text
2 integral from 0 to sqrt(1−p/R) of
    2 / sqrt[1+x − (2/p)(1+x+x²)] dt, where x=1−t².
```

Tests use 20,000 quadrature intervals, six turning radii from 3.01 to 30 M, and compare angular steps 0.02 and 0.005 rad. Additional checks use analytic reciprocal static shifts, the ISCO speed `beta=0.5`, and rotational/reflection symmetries. Test definitions are not test results: the M3 validation receipt records which commands and player checks actually passed. These controls do not validate binary metrics, 3D radiation transport, time-dependent past light cones, Kerr spin or laptop performance.

## Upstream reference license

Source: [Bruneton upstream LICENSE at the pinned commit](https://github.com/ebruneton/black_hole_shader/blob/e72b3f293409893a6fa25528b29572c96fc57f57/LICENSE). Retained for source provenance and future selected-code reuse; the new C# implementation is independent.

```text
Copyright (c) 2020 Eric Bruneton
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
this list of conditions and the following disclaimer in the documentation
and/or other materials provided with the distribution.

3. Neither the name of the copyright holder nor the names of its contributors
may be used to endorse or promote products derived from this software without
specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

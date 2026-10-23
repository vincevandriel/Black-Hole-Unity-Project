# M4 prescribed binary gas field

M4 adds an **assumed gas illustration around the retained SXS:BBH:0305 horizon replay**. Its ring, stream and remnant geometry is computed directly from absolute event time. This makes seeking reversible and bounds storage without keeping a movie of gas frames. The field is three-dimensional and independent of the visitor's viewpoint. It is not a hydrodynamic or GRMHD solution, does not conserve gas, and is not a reconstruction of observed gas in GW150914.

The authoritative formulas are in `ReducedBinaryFlowModel.cs`; `ReducedBinaryFlow.hlsl` implements the same density and bolometric emission functions. Both use the [M2 data and unit contract](Data/SXS0305/README.md). No other binary's gas simulation has been attached to these horizon positions. The [M3 isolated reference](PHYSICS_M3.md) remains a separate control.

## Units and retained numerical input

Use the M2 source total mass, 64.5 nominal solar masses. Define `rg = GMtotal/c² = 95,242.31495423306 m` and `tg = GMtotal/c³ = 0.00031769416612286176 s`. Convert the already SI-scaled horizon positions by dividing by `rg`; do not divide by the raw mass normalization a second time. Map SXS `(x,y,z)` to Unity `(x,z,y)`, as in M2. `tau = elapsed source seconds / tg`; `tau0 = (elapsed - first-common-horizon elapsed) / tg`.

Each body's position, mass fraction `mi = Mi/Mtotal` and equal-area-sphere radius come from `NumericalEventHistory.Evaluate`. The first common horizon occurs at elapsed `0.9992898239188639 s`; the retained endpoint is `1.2443464499042842 s`. Individual and common horizons are never extrapolated across the data switch. Body coordinate translation velocity is a centered difference of the retained positions at `±0.25 tg`, clipped to the appropriate horizon's time coverage. That velocity is a coordinate derivative, not a local relativistic measurement.

Initial separation is `a0 = 14.431219208693975 rg`. The fixed reservoir inner radius is `1.8 a0 = 25.976194575649156 rg`; its outer radius is `3 a0 = 43.293657626081924 rg`. These factors are declared illustrative choices, not fitted measurements. The field is exactly zero outside `|x|,|z| < 46 rg`, `|y| < 9 rg`.

## Shared ring profile

For a ring centered on its stated body or the fixed origin, define cylindrical `R = sqrt(x²+z²)`, `u = (R-rin)/(rout-rin)` and `h = y/(aspect R)`. For `0<u<1` and `|h|<1`,

```
W(u) = 16 u² (1-u)²
H(h) = (1-h²)²
rho/rho0 = strength W H pattern
T/T0 = temperatureScale (rin/R)^(3/4),  T0 = 10^7 K
```

Outside that support, the ring contributes zero. Both envelopes reach zero with zero first derivative at their boundaries. The `R^(-3/4)` temperature prescription is used only as a source-temperature shape; it does not establish a thin-disk energy balance, a measured accretion rate, an equation of state, or radiative efficiency. The shader computes `T^4` as `temperatureScale^4 (rin/R)^3`, avoiding a fractional power in each ray sample.

| Component | Center | Inner / outer radii | Aspect | Strength | Temperature scale |
|---|---|---|---|---|---|
| Reservoir | Fixed SXS coordinate origin | `1.8 a0` / `3 a0` | 0.18 | 0.55 | 0.35 |
| Each minidisk | Its retained individual horizon position | `6 mi` / `0.3 a(t)` | 0.24 | `3 B smoothstep(rout-rin)` | 1.1 |
| Remnant ring | Retained common-horizon position | `6 mremnant` / `16 rg` | 0.24 | `2 C` | 1.1 |

Here `smoothstep(v) = s²(3-2s)`, `s = clamp(v,0,1)`. The minidisk width in its strength expression is divided by the declared width scale of `1 rg`. If `rout<=rin`, the minidisk is absent. Thus a shrinking tidal-size illustration cannot retain a bright ring where its assumed inner and outer radii have crossed. The `6 mi` inner cutoffs borrow a nonspinning circular-orbit scale for an illustration; the actual SXS component spins do not make them a computed Kerr ISCO or a binary stability boundary.

Only the reservoir has an angular modulation: `pattern = 1 + 0.2 cos(2(phi-phase))`, where `phase = tau/Rmid^(3/2)` and `Rmid=(rin+rout)/2`. The shader uses `(x²-z²)/R²` and `2xz/R²` with state-supplied `cos(2phase)` and `sin(2phase)` instead of an `atan2`. This is a rigid prescribed pattern. Its pattern speed is not a claim that the complete density field solves the continuity equation.

Ring coordinate velocity is `v/c = vcenter/c + (-Omega z - vr x/R, 0, Omega x - vr z/R)` with `Omega=sqrt(m/R³)`. Use `m=1`, `vr=0.004` for the reservoir; each ring's `mi` and `vr=0.008` for minidisks/remnant. No pressure, viscosity, magnetic torque, self-gravity or relativistic fluid update determines these velocities. No displayed sample marker is asserted to integrate them as a material trajectory.

## Two prescribed stream tubes

For each individual horizon, the stream starts at radius `1.03 rin,reservoir`, at that body's azimuth plus `0.55 radians`. Its endpoint is displaced from the body toward the start by `max(1.25 rhorizon, 0.8 rout,minidisk)`. The state stores start `A`, end `D`, chord `d=D-A`, and the XZ unit normal `n=(-dz,0,dx)/|dXZ|`.

At a query point `p`, let `u=dot(pXZ-AXZ,dXZ)/|dXZ|²`. For `0<u<1`, form `center=A+u d+10 u(1-u)n`, giving a maximum bow of `2.5 rg`. Define

```
q = |pXZ-centerXZ|² / 1.4² + (py-centery)² / 0.8²
rho/rho0 = 0.9 B W(u) (1-q)²           for q<1
T/T0 = 0.4 + 0.5 u
```

The curve parameter is the projection onto its chord, not an iterative closest-point solution. The field definition uses that projection consistently in C# and HLSL. The prescribed stream velocity points along the derivative of the center curve, with speed `(0.12+0.13u)c`. It does not infer a conserved mass flux or a ballistic solution of the binary metric.

## Merger transition and horizon proxies

Set `B=1-smoothstep((tau0+120)/120)` and `C=smoothstep(tau0/160)`. Binary minidisks and streams therefore fade during the final `120 tg` before the first common horizon; the remnant ring grows over the first `160 tg` after it. The persistent outer reservoir remains visible. Both transition envelopes have zero value and slope at the horizon switch. The brief inner-cavity interval is part of this assumed model, not an inferred measured gas transient or a predicted electromagnetic flash.

The combined density and emission are multiplied by `smoothstep((distance/rhorizon-1)/0.2)` for each currently supplied horizon proxy. This gives vacuum inside its coordinate equal-area sphere, with a smooth transition over the next 20% in radius. It is not the deformed apparent-horizon surface, event horizon, photon capture boundary or optical shadow. Because both inner components vanish smoothly at the switch and the reservoir lies outside all masks, the source field does not jump when M2 changes its horizon set.

## Emission, markers and fidelity

Components add `rho` and `E = sum[rho (T/T0)^4]` separately. The displayed gray transfer source is `E/rho`. `TemperatureKelvin` returns the equivalent source temperature `T0(E/rho)^(1/4)`; overlapping components do not establish a solved thermodynamic mixture. Extinction is prescribed as `alpha = 0.16 rho / rg` before the scene's opacity control. The relative density scale `rho0` is unspecified; volume integrals cannot be called kilograms and `E` integrals cannot be called calibrated luminosities. Fixed transfer coefficients, camera exposure and a false-color mapping cannot establish a measured visible spectrum.

No grid resolution, particle count, viewpoint or quality setting changes `EvaluateState` or `Sample`. Fidelity controls can change the quadrature and image resolution used to display this fixed field. Optional point markers should be labeled **density samples**: a static spatial sample has weight `rho V/N` and samples the field at the current epoch. Changes in that weight are not particle creation, mass-conserving regrouping, or a material trajectory. The continuous source field does not derive its brightness from marker count.

State reconstruction and CPU sampling allocate no per-seek arrays in the portable test. Runtime owns no full 3D grid or gas-history cache for this model. That statement excludes the existing SXS history, rendering targets, marker buffers, Unity allocations and total process memory. It is not a laptop, VR or whole-application memory benchmark.

## Independent checks and GPU fixtures

`ReducedBinaryFlowTests.Run` checks source units/axis mapping, independently evaluated reservoir/minidisk/stream/remnant points, finite fields at major epochs, compact support, horizon-proxy vacuum, bit-identical reverse seeks, zero allocated bytes in 1,000 pure reconstructions/samples, and the common-horizon transition. It also integrates the complete field using Cartesian midpoint grids and compares independent cylindrical/elliptical component integrals.

For each ring, the unmasked relative-density-volume integral is

```
2 pi aspect (16/15) strength * 16 d [rin²/30 + rin d/30 + d²/105],
d = rout-rin.
```

The independent emission quadrature replaces the radial integral by `temperatureScale^4 rin³ integral[W(R)/R dR]`. The reservoir's angular modulation integrates to zero. The unmasked stream transverse integral is `pi width height/3` and its coordinate Jacobian is chord length; integration of `W(u)` gives `8/15`. A 2,048-panel Simpson rule independently integrates the stream temperature and ring emission factors. These are component reference integrals before the smooth horizon mask; any stream overlap with a mask can reduce the full model's result, so they are not a conservation law or an assertion of an exact masked continuum integral.

| Epoch | Component mass reference | 96³ measured mass | 192³ measured mass | Component emission reference | 96³ measured emission | 192³ measured emission |
|---|---:|---:|---:|---:|---:|---:|
| Start | 7527.337314608161 | 7529.800740795233 | 7527.2020693897575 | 122.23709464970204 | 123.67304113573682 | 122.09462679209301 |
| First common horizon | 7416.785784188264 | 7416.855002884078 | 7416.793368121912 | 46.962814740559374 | 46.96328556462263 | 46.96281854284812 |
| Retained endpoint | 9563.774286157246 | 9563.870061904314 | 9563.74896888194 | 506.0546080626696 | 506.05800551154937 | 506.04267165186 |

The gates are 0.1%/0.01% mass error and 2%/0.2% emission error at 96³/192³, plus improvement of the worst-epoch errors under refinement. An individual shifted compact feature's midpoint error need not decrease monotonically. A trial at 64³ underestimated initial emission by **11.57%** because narrow initial minidisks were poorly resolved. That failed trial prompted finer independent integration; it is evidence against treating a coarse density grid as automatically accurate. These CPU volume quadratures do not prove acceptable image error for any rendering preset; the actual GPU and image checks are separate.

For an RGBAFloat GPU fixture, return `(rho,E,0,1)` directly from `SampleReducedFlow` without transfer, exposure, postprocessing or gamma conversion. Compare to CPU values at the same full-precision-derived positions after the normal float upload. Suggested points are:

| Epoch / component | Position `(x,y,z)/rg` | Density | `E` |
|---|---|---:|---:|
| Start / reservoir | `(34.63492610086554,0,0)` | 0.66 | 0.004178302734374998 |
| Start / minidisk A | `(-6.465198557671183,-0.000008524904502899349,3.4273834022595753)` | 3 | 2.841716332168965 |
| Start / stream A | `(-17.181939144653676,-0.0000042624522514496745,-7.258443971232283)` | 0.9 | 0.16065562500000002 |
| Endpoint / remnant | `(10.63009603241252,-0.00009667186740859651,-0.2642597678338739)` | 2 | 0.4265536727085955 |

Also compare vacuum points outside the bounding box and at each horizon center, and arbitrary interior samples away from profile boundaries. GPU fixture and player outcomes belong in the milestone validation receipt; this document does not itself certify a GPU pass.

## HLSL upload contract

`_FlowA`, `_FlowB`, `_FlowRemnant`: mapped position xyz and mass fraction w. `_FlowARadii`, `_FlowBRadii`, `_FlowRemnantRadii`: inner, outer, horizon-proxy radius, unused. `_FlowReservoir`: inner, outer, `cos(2phase)`, `sin(2phase)`. `_FlowWeights`: binary weight, remnant weight, common-horizon flag, unused. `_FlowBounds`: half-extent XZ, half-extent Y, unused, unused. `_FlowStreamAStart/End` and B counterparts: xyz, unused. `_FlowStreamABow` and B counterpart: unit normal xyz, chord length squared w. These form one immutable epoch; upload all parameters before drawing the new time.

M4 omits a gas equation of state, pressure forces, magnetic fields, shocks, self-gravity, radiative cooling/heating, conserved boundary fluxes, a binary spacetime metric and null-ray transfer through that metric. Its inverse-square-inspired circular rates and borrowed inner radius do not supply those missing equations. Scientific gas histories and binary observer optics remain separate milestones.

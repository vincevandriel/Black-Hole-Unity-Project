# M4 prescribed binary gas field

M4 adds an **assumed gas illustration around the retained SXS:BBH:0305 horizon replay**. Its ring, stream and remnant geometry is computed directly from absolute event time. This makes seeking reversible and bounds storage without keeping a movie of gas frames. The field is three-dimensional and independent of the visitor's viewpoint. It is not a hydrodynamic or GRMHD solution, does not conserve gas, and is not a reconstruction of observed gas in GW150914.

The current field is `assumed-SXS0305-reduced-binary-flow-v2-plunge`. Version 2 includes an explicitly assumed emitting plunge region and a shared transition ring. Version 1's `6 mi` inner cutoff left narrow annuli, removed a component's disk before merger, and made all inner gas vanish at common-horizon formation. Those were consequences of the illustration's choices, not predictions from measured gas. The numerical horizon motion and gravitational-wave data are unchanged.

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
| Each minidisk plus assumed plunge | Its retained individual horizon position | `1.25 rhorizon` / `0.3 a(t)` | 0.24 | `3 B smoothstep(rout-rin)` | 1.1 |
| Shared transition ring | Fixed SXS coordinate origin | `4 rg` / `16 rg` | 0.24 | `1.5 D` | 1.1 |
| Remnant ring plus assumed plunge | Retained common-horizon position | `1.25 rhorizon` / `16 rg` | 0.24 | `2 C` | 1.1 |

Here `smoothstep(v) = s²(3-2s)`, `s = clamp(v,0,1)`. The minidisk width in its strength expression is divided by the declared width scale of `1 rg`. If `rout<=rin`, the minidisk is absent. The outer radius is never expanded to force an overlapping disk between converging holes. The adjustable experiment keeps its mass-ratio-dependent Roche-lobe outer radius. The `1.25 rhorizon` inner boundary is an assumed emission cutoff outside the proxy, not a computed Kerr ISCO, binary stability boundary, or stable circular disk within the ISCO.

Emission inside the ISCO is physically possible in accreting black-hole models: [Mummery et al. (2024), Continuum emission from within the plunging region of black hole discs](https://doi.org/10.1093/mnras/stae1160) includes such emission and compares models with MAXI J1820+070. [Mummery and Stone (2024), The three-dimensional structure of black hole accretion flows within the plunging region](https://doi.org/10.1093/mnras/stae1643) compares analytic flow descriptions and GRMHD simulations, including spiral structure. These sources motivate allowing an emitting plunge, but do not supply our cutoff, temperature, luminosity, binary transition, or the gas in GW150914. Our coordinate velocities are still a prescribed illustration, not those papers' plunge solution.

The reservoir and transition ring have an angular modulation: `pattern = 1 + 0.2 cos(2(phi-phase))`, where `phase = tau/Rmid,reservoir^(3/2)`. The shader uses `(x²-z²)/R²` and `2xz/R²` with state-supplied `cos(2phase)` and `sin(2phase)` instead of an `atan2`. This is a rigid prescribed pattern. Its pattern speed is not a claim that the complete density field solves the continuity equation.

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

Set `B=1-smoothstep((tau0+120)/120)`, `C=smoothstep(tau0/160)`, and `D=smoothstep((tau0+600)/600)(1-C)`. Binary minidisks and streams fade during the final `120 tg`; the shared transition ring begins growing `600 tg` before common horizon and is fully present at that switch. It fades as the remnant ring grows over the next `160 tg`. The persistent outer reservoir remains present. These C1 envelopes prevent an artificial blackout; they do not predict gas survival, a measured gas transient, or an electromagnetic flash. The adjustable experiment retains its own conceptual binary fade while sharing the same `D` bridge.

The combined density and emission are multiplied by `smoothstep((distance/rhorizon-1)/0.2)` for each currently supplied horizon proxy. This gives vacuum inside its coordinate equal-area sphere, with a smooth transition over the next 20% in radius. It is not the deformed apparent-horizon surface, event horizon, photon capture boundary or optical shadow. The transition ring's inner radius lies outside the switching masks for this retained event; portable samples verify continuity across common-horizon formation.

## Emission, markers and fidelity

Components add `rho` and `E = sum[rho (T/T0)^4]` separately. `TemperatureKelvin` returns the equivalent source temperature `T0(E/rho)^(1/4)`; overlapping components do not establish a solved thermodynamic mixture. Extinction is prescribed as `alpha = 0.16 rho / rg`. The relative density scale `rho0` is unspecified; volume integrals cannot be called kilograms and `E` integrals cannot be called calibrated luminosities.

The visible transfer multiplies `E/rho` by a warm false-color palette, bounded spiral bands anchored to the event phase, and a thinner mid-plane emission window. These are display choices to make disk structure readable; the bands are not resolved turbulence, and displayed brightness is not a bolometric estimate. They leave the CPU/GPU density and `E` fixture values intact. Local quadrature uses eight subsegments near the compact emitting components, four in the broader cavity, and one outside. The ray count remains bounded by the selected fidelity. No wall-clock animation enters the gas, so pause, rewind and different viewpoints share the same field and display phase.

The separate numerical fluid mode does not use these synthetic bands. Its transfer uses resolved density and velocity for false color/emission, with a mid-plane window and a circular emission aperture fading between 0.82 and 0.98 of the grid half-extent to hide the finite square boundary. Its stored density, momentum and full-domain mass ledger are unchanged.

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
| Start | 7565.072379957854 | 7564.975835481643 | 7565.083092311621 | 67.42440638002064 | 67.4330225964646 | 67.42281196723798 |
| First common horizon | 9040.355122936902 | 9040.336281810742 | 9040.37148262631 | 200.17057068097526 | 200.15869908583613 | 200.16802261272565 |
| Retained endpoint | 9538.839355055887 | 9538.923198414466 | 9538.832620434654 | 92.19342723131109 | 92.19418480633543 | 92.19393467877272 |

The unchanged gates are 0.1%/0.01% mass error and 2%/0.2% emission error at 96³/192³, plus improvement of the worst-epoch errors under refinement. The table is the v2 portable run. Tests also require positive integrated central emission at eight epochs from `-600` to `+160 tg` and sample continuity around the horizon switch. An individual shifted compact feature's midpoint error need not decrease monotonically. These CPU volume quadratures do not prove acceptable image error for any rendering preset; the actual GPU and image checks are separate.

For an RGBAFloat GPU fixture, return `(rho,E,0,1)` directly from `SampleReducedFlow` without transfer, exposure, postprocessing or gamma conversion. Compare to CPU values at the same full-precision-derived positions after the normal float upload. Suggested points are:

| Epoch / component | Position `(x,y,z)/rg` | Density | `E` |
|---|---|---:|---:|
| Start / reservoir | `(34.63492610086554,0,0)` | 0.66 | 0.004178302734374998 |
| Start / minidisk A | Body A plus `(0,0,(rin+rout)/2)` | `3 smoothstep(rout-rin)` | `rho 1.1^4 (rin/R)^3` |
| Start / stream A | `(-17.181939144653676,-0.0000042624522514496745,-7.258443971232283)` | 0.9 | 0.16065562500000002 |
| Endpoint / remnant | Remnant plus `((rin+rout)/2,0,0)` | 2 | `2 1.1^4 (rin/R)^3` |

Also compare vacuum points outside the bounding box and at each horizon center, and arbitrary interior samples away from profile boundaries. GPU fixture and player outcomes belong in the milestone validation receipt; this document does not itself certify a GPU pass.

## HLSL upload contract

`_FlowA`, `_FlowB`, `_FlowRemnant`: mapped position xyz and mass fraction w. `_FlowARadii`, `_FlowBRadii`, `_FlowRemnantRadii`: inner, outer, horizon-proxy radius, unused. `_FlowReservoir`: inner, outer, `cos(2phase)`, `sin(2phase)`. `_FlowWeights`: binary weight, remnant weight, common-horizon flag, transition-ring weight. `_FlowBounds`: half-extent XZ, half-extent Y, unused, unused. `_FlowStreamAStart/End` and B counterparts: xyz, unused. `_FlowStreamABow` and B counterpart: unit normal xyz, chord length squared w. These form one immutable epoch; upload all parameters before drawing the new time.

M4 omits a gas equation of state, pressure forces, magnetic fields, shocks, self-gravity, radiative cooling/heating, conserved boundary fluxes, a binary spacetime metric and null-ray transfer through that metric. Its inverse-square-inspired circular rates and borrowed inner radius do not supply those missing equations. Scientific gas histories and binary observer optics remain separate milestones.

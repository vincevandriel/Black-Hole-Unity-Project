# M3: field and light reference laboratory

Launch the Windows player with `--reference-lab` (the packaged `START_M3_REFERENCE_LAB.cmd` supplies it). Without that argument the existing M1/M2 exhibit starts normally. The lab is an isolated desktop reference with its own camera and screen controls. It does not add a gas model to the SXS binary.

The lab starts paused. Space plays/pauses, comma reverses direction, R resets and the timeline seeks directly. Rate cycles through 16, 64 and 256 gravitational time units per wall-clock second. WASD and Q/E move; right-drag looks. Keys 1/2/3/4 choose overview/above/edge/below. T toggles tracers, and O selects the separate Schwarzschild optical control. Camera positions are bounded to `2.2 <= r/rg <= 90`; the observer cannot enter the horizon.

## Prescribed volume and tracers

This is an assumed isolated thick torus, not a hydrodynamic or GRMHD solution. Let `rg=GM/c²`, `tg=GM/c³`, cylindrical `R=sqrt(x²+z²)` and `phi=atan2(z,x)`. Positions and time below are dimensionless in those units. Support is `6<R<18` and `|y|<0.30R`, inside a box `[-20,20] × [-6,6] × [-20,20]`.

```text
Omega(R) = R^(-3/2)
rho/rho0 = sin²[pi(R-6)/12] * [1-(y/(0.30R))²]²
           * [1+0.30 cos(2(phi-Omega*t))]
T = 10^7 K * (R/8)^(-3/4)
vx/c = -Omega*z; vy=0; vz/c = Omega*x
```

The circular coordinate-frequency law is prescribed throughout the thickness; off-equatorial motion is not a geodesic solution. Temperature is an assumed local source temperature, not a derived thin-disk effective or midplane temperature. There is no radial accretion, pressure balance, magnetic field, heating/cooling or backreaction. Rotation increases `atan2(z,x)`, matching the optical control's selected disk normal of `-Y`; the volume and optical thin disk remain distinct models.

The continuous formula advects its density pattern without changing its spatial integral. The Cartesian grid approximates that integral and is checked at 32³ and 64³. No physical density normalization is inferred: the tracer mass measure is `integral (rho/rho0) dV/rg³`, not kilograms. Deterministic low-discrepancy samples have positive weights normalized to its analytic value, approximately `1793.9333458743354`. Changing marker count preserves that total. Markers are a diagnostic overlay; their displayed size or brightness is not emission, and the volume render never reads their count. A weighted CDF resampling utility is independently checked; the UI count change regenerates a deterministic sample of the same continuous measure.

Tracer positions evaluate the prescribed circular trajectory at absolute requested time. Rewind does not integrate a dissipative solver backward. The field has 129 indexed epochs at 8 tg spacing across `0..1024 tg`. A synchronous cache reconstructs only the adjacent two frames. Both textures are uploaded before the new epoch is published; superseded asynchronous jobs do not exist in this small reference.

## Continuous emission and opacity

Two trilinearly sampled RGBAFloat textures store `(rho, rho*T/T0, rho*vx/c, rho*vz/c)`. Density-weighted interpolation avoids inventing a cold temperature at a vacuum border. The shader integrates straight rays through the volume with 192 midpoint segments. The exact constant-segment update is

```text
alpha = 0.3 * opacityControl * (rho/rho0) / rg
S / S(T0) = (T/T0)^4, where S(T0)=sigma_SB*T0^4/pi
emittedFraction = 1-exp(-alpha*ds)
I += transmission * emittedFraction * S
transmission *= 1-emittedFraction
```

This is an assumed gray thermal source function, not synchrotron or a calibrated spectrum. RGB is explicitly false colour and exposure maps intensity using `1-exp(-exposure*I)`. The opacity control changes this reference assumption; it is not a fidelity slider. The radius-2 black region is a straight-ray coordinate proxy, not a lensed shadow. Tracers are rendered as an overlay, not transported/absorbed physical photons.

The GPU QA fixture substitutes a uniform white source in the same transfer shader and compares its rendered linear intensity with the analytic slab solution at 64 and 256 segments. Portable fixtures separately test transparent/thick limits, segment composition, Planck radiation, temperature scaling and frequency-shift invariants. These tests do not establish convergence for an arbitrary future high-gradient scientific field.

## Separate Schwarzschild optical control

The optical tab traces a **different, steady, infinitesimally thin opaque disk** around a single nonspinning hole. It does not bend the thick-torus image or claim binary optics. Each view denotes an exterior static observer; moving the camera changes its location, not a physical velocity/worldline.

The implementation directly integrates Schwarzschild null rays in double precision. It compares with Bruneton's critical impact parameter and unit conventions, and with independent turning-point quadrature. This replaces the plan's initial lookup-table port with a simpler directly testable control. Bruneton's tables, beam filtering and performance optimization remain deferred; no published upstream frame-rate claim transfers here. [Full equations, sources, license notice and limits](M3_OPTICAL_REFERENCE.md).

The displayed control uses a Newtonian zero-torque effective-temperature reference:

```text
F(R) = 3 GM Mdot/(8 pi R³) * [1-sqrt(Rin/R)]
Teff = [F/sigma_SB]^(1/4)
```

Assumptions are 35 nominal solar masses, `Mdot=5e15 kg/s`, and `Rin=6rg`, with outer edge 18rg. This is not Novikov–Thorne or a fit to Cygnus X-1/GW150914. The orbital emitters and light rays use Schwarzschild geometry while the emission profile remains this explicitly approximate reference. At the selected observed band, 1 keV (`nu=2.417989242e17 Hz`), `I_nu/nu³` invariance is applied once through `B_nu(g*Teff)`. The palette is false colour, with an exposure normalized to `B_nu(3e6 K)`.

The CPU reference image is 192×108. Four rows are computed per update, with maximum angular increment 0.012 rad and 4,096 steps per ray; a radial-step limiter further restricts integration. A changed camera/time request invalidates the old image immediately, and only a complete replacement is displayed. Unresolved/coplanar rays are purple and counted. Playback is paused in this steady control. This deliberately bounded reference is not the final interactive GPU optical renderer.

## Storage, validation and limits

At 32³, two decoded float4 arrays use exactly 1 MiB. Two RGBAFloat texture payloads use another 1 MiB on the GPU and retain about 1 MiB of readable CPU texture data for updates. Tracer arrays/mesh, optical pixels, render targets, Unity, driver allocations and UI are additional. This arithmetic and the bounded-cache tests are not a total-process peak-memory measurement. Resource owners destroy their textures, mesh and materials when the lab is destroyed; a long unload/leak soak is a later device gate.

The lab currently requires 3D RGBAFloat texture support. It does not supply the M4 startup fidelity selector or a certified laptop fallback. It uses no large gas download or new Unity package. Ten portable groups include M1/M2 regressions and the new field/optics checks:

```powershell
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
```

Build with `DTET.VisualPrototype.Editor.PrototypeBuild.BuildQaPlayer`, then run `Scripts/Test-FieldReferenceMilestone.ps1 -OutputDirectory C:/temp/dtet-m3`. Its opt-in harness validates the actual shader, replay, cache, tracer weights, controls and rendered views. Camera-only checks use Unity's supported [URP render-request API](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/rendering/renderpipeline/standardrequest). The completed milestone validation receipt records actual build/runtime results; portable passes alone do not establish them.

Remaining stages are the reduced binary flow/launcher/sky exhibit (M4), compatible scientific gas (M5), and validated binary observer optics/clocks (M6). M3 supplies reference components and evidence, not those later claims.

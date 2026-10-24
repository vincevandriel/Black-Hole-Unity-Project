# Plasma appearance: transported material and readable merger timing

24 October 2026. Runtime revision `44d45529098865e71f379207086749904eb68b0e`. Final development-player receipts are `outputs/Plasma-visual-validation/gpu-final/minidisk-integration.json` and `outputs/Plasma-visual-validation/gpu-half/minidisk-integration.json` in the research workspace. This extends the [finite mini-disk model](MINIDISK_DYNAMICS.md), whose physical limitations remain. Earlier [mini-disk graphics checks](MINIDISK_VALIDATION.md) describe their own historical revision.

The aim is a less smooth, more visibly moving emitting volume that remains freely explorable. This is conditional material detail on an existing assumed gas flow. It does not convert the unconverged Newtonian cache into GRHD/GRMHD, recover GW150914's unknown gas, or predict a measured merger image.

## Why continuous gas with passive markers

Plasma has collective pressure, transport and thermodynamics. Independent glowing bodies under gravity omit those effects. The existing surface-density/planar-momentum PDE remains the flow authority. Passive markers sample its velocity to carry material labels; they do not calculate forces, particle collisions, magnetic stresses or gas feedback.

[Ryan and MacFadyen's minidisk calculations](https://arxiv.org/html/1611.00341) show why this distinction matters: tidal spiral shocks transport angular momentum and heat the gas, and cooling participates in its calculated emission. Their physical ingredients and resolved relativistic dynamics are not supplied by making more points visible. Our cache has no energy equation or magnetic field, so added small structure is an appearance assumption.

## Passive material sidecar

The implementation uses 8192 passive material tracers on the recorded planar flow, seeded by the initial surface-density distribution. Fixed spatial labels combine smooth patches at four/eight fluid-cell scales. RK2 positions advance through linearly interpolated stored velocity history. Compact isotropic splats (Gaussian width 1.25 cells, support radius 3.5 cells, additional smooth taper) reconstruct a signed material concentration C in [-1,1] at the same 513 recorded epochs. Timesteps use a Courant parameter of 0.75 against the admitted grid speeds, with an additional 0.5-tg ceiling. There are no new forces, replenishment or respawn. The original Sigma/vx/vz cache, its hashes, source trajectories, waveform and mass ledger remain separate and unchanged.

Conceptually a material label C satisfies

```
dC/dt + v dot grad(C) = 0
dx/dt = v(x,t).
```

This describes passive transport, not a new plasma solver. Initial density-weighted sampling does not make the tags independent mass-carrying fluid particles. Finite marker sampling, interpolation, reconstruction and temporal cadence add their own errors. A fixed label carried with the flow moves and shears; generic noise animated with wall-clock time would merely boil. The label does not rotate rigidly with a black hole when the supplied velocity differs.

[Neyret's Advected Textures](https://diglib.eg.org/items/c4bf80cc-23f0-4107-958d-e03c0b9c8ac4) establishes the graphics motivation: transported detail preserves continuity, but excessive stretch, reset pops and poorly blended parameterizations need control. Our implementation is not a reproduction or validation of that paper's complete algorithm. In particular, reconstructed passive labels must not be described as resolved turbulence or MRI.

The underlying Newtonian field contains invalid relativistic speeds, including v >= c near merger. Transport rejects tags whose sampled speed is invalid. Reconstruction sets C=0 in stored cells with invalid speed or insufficient density; absent marker support also approaches neutral. The shader interpolates C and velocity separately, so this is a **stored-cell mask**, not a pointwise guarantee that every interpolated superluminal sample has zero contrast. Rendering separately caps optical velocity at 0.95 c. Neither safeguard repairs the recorded fluid or adds material. The finite gas and its depletion remain visible, with no obligatory bright rings after the inventory drains.

The current C changes the assumed source function/chromaticity only. It does not modulate density or optical depth. The label is planar and is extruded through an assumed vertical profile; it is not evolved vertical turbulence or three-dimensional material advection. A future density/opacity detail model would need a measured column-normalization error; even mean-preserving density fluctuations can change observed flux through nonlinear absorption.

## Local vertical reconstruction

The current renderer replaces the earlier uniform H=0.3-rg display layer with a local isothermal scale-height closure. For each active softened point potential, in total-mass rg/tg units,

```
omega_i^2(x,z) = m_i / (|r_planar-r_i|^2 + epsilon_i^2)^(3/2)
omega_z^2 = binary_weight*(omega_A^2+omega_B^2) + remnant_weight*omega_R^2
H = clamp(cs/sqrt(omega_z^2), max(0.025,0.4*dx), max(0.3,0.12*domain_half_extent))
rho(x,y,z) = Sigma(x,z)*exp(-y^2/(2*H^2))/(sqrt(2*pi)*H).
```

Here cs=0.04 c comes from the isothermal fluid parameters, epsilon is its softened-potential length, and dx is its planar cell width. The lower bound is 0.05 rg on the selected 192-square, half-extent-12-rg cache; the upper bound is 1.44 rg. A no-active-potential fixture falls back to its supplied constant H. The same binary/remnant blend used by the illustrative optical transition weights the height closure.

This follows the small-height expansion of a softened Newtonian potential: `dP/dy=-rho*omega_z^2*y` with `P=cs^2*rho` gives a Gaussian and H=cs/omega_z. It is an instantaneous **display reconstruction**, not a solved vertical momentum equation or proof of hydrostatic equilibrium during a rapidly changing merger. The Gaussian is centered on the existing shared plane. It omits vertical waves, shocks, warps, radiation support and magnetic pressure. Grid-related floors/caps change its inferred height. The normalization preserves `integral rho dy = Sigma` at each planar position even as H changes; a finite sampled ray can still have integration error.

## Volumetric light and colour

The gas should emit and absorb along the same curved ray. For a homogeneous segment,

```
T = exp(-tau)
I_out = I_in*T + S*(1-T),
```

where S is the source function. This gives a testable transfer rule and view-dependent self-occlusion. It is different from atmospheric fog, additive glowing sprites or a decorative ring placed around a dark sphere. A near-side optically thick patch can hide the far-side volume; lower-density material can remain translucent. Gas off or a zero-density field must emit no texture/light.

[RAPTOR II](https://doi.org/10.1051/0004-6361/202038573) distinguishes optically thick surfaces from volumetric transfer and consistently transports radiation through a specified spacetime. It motivates the separation of matter and observed light. Our weighted Schwarzschild-index binary display still lacks a validated binary metric, physical emissivity/absorption coefficients, observer worldlines and time-dependent light-cone transfer.

The renderer uses hotter chromaticity, one bounded bolometric-style g^4 factor, two-point segment sampling and selective subpixel integration. Its fixed appearance mapping is

```
u = clamp(C,-1,1)
f(u) = 0.5*(0.65*sin(13*u) + 0.35*sin(29*u))
q = f(u)*clamp(detail_strength,0,1)
theta = 2^clamp(q,-0.5,0.5)
source_amplitude_multiplier = theta^4.
```

Neutral C=0 gives theta=1. The source multiplier is bounded from 0.25 to 4. This fixed function of a transported label adds finer emissivity contrast; it is not new gas dynamics, a physical turbulence spectrum, or inferred temperature/luminosity. Its derivative can amplify label interpolation errors by up to 9.3 before exponentiation. Higher spatial frequencies require filtering/sampling; the finite sidecar and two-ray footprint are not proof of convergence. Gray opacity 8 and exposure 1.6 remain fixed. The piecewise colour palette supplies normalized chromaticity, not an integrated Planck spectrum. The gas has no solved temperature, electron distribution or physical density normalization. Gray opacity/exposure and false colour remain assumptions. The optical velocity safeguard does not repair superluminal values in the recorded fluid.

[Bruneton's interactive renderer](https://ebruneton.github.io/black_hole_shader/paper.pdf) is a useful isolated-hole optical reference, while explicitly excluding realistic accretion simulation. Its [documented disk source](https://ebruneton.github.io/black_hole_shader/black_hole/model.glsl.html) illustrates moving density features and blackbody-based shading. A blackbody-inspired colour does not by itself supply the missing binary thermodynamics. If a future absolute blackbody lookup already integrates the spectrum shifted to g*T, multiplying another g^4 into that absolute result would double-count the brightness transformation; a normalized chromaticity lookup has a different intensity contract.

The ray evaluates two ordered medium samples per segment. Height-aware steps refine the thin layer. After an initial center-ray estimate, a high-contrast footprint evaluates two additional edge-directed rays and uses their average. This is deterministic spatial sampling, with no wall-clock jitter. It reduces selected aliasing; it does not establish all-angle convergence or remove every thin-rim artifact. The gas shader returns radiance without a second local highlight compressor; the existing `ExhibitDisplay` transform `1-exp(-radiance)` is the display mapping. It is fixed across frames, so it does not normalize away gas depletion. Neither sampling nor tone mapping changes the scientific epoch.

## Optional automatic slow motion

The merger can be made easier to inspect by reparameterizing **display time**, while sampling the same scientific epoch everywhere. The implemented positive factor is

```
x = (t_source - t_common_horizon)/(100*tg)
w(x) = (x*x + 0.12)/(x*x + 1)
dt_source/dt_display = manual_rate*w(x).
```

The implementation uses an analytic monotone integral of this relation and a bounded inverse, rather than accumulating frame-dependent Euler steps through the transition. It changes how long an epoch takes to visit. It does not change the orbit, gas dynamics, physical frequency, waveform amplitude or source timing at that epoch.

For the selected 64.5-solar-mass scaling and base rate 0.02, the illustrative capture blend (-12 to +40 tg) occupies approximately 5.63 display seconds under this map. That value describes presentation timing, not a new physical merger timescale.

The interface shows the auto state and effective rate. Manual speed selection disables automatic slow motion; a visible toggle restores it. Pause freezes the event and material while the camera remains movable. Reverse, seek and cancellation retain their existing semantics. Any future sonification must declare its playback/pitch policy rather than treating a slowed playback sound as a new physical signal.

## Validation criteria and scope

The executed controls are identified in the evidence below; the broader criteria here also include outstanding scientific convergence, continuous camera-motion review and native/device performance validation, and must not all be read as passed.

- Passive zero-flow and known translation/rotation fixtures; displacement/shear direction and bounded reconstruction error. Record actual sidecar format, memory, coverage, source hashes and invalid/missing-label handling.
- Unchanged original gas cache and scientific body/waveform snapshots. No gas or emitted pattern outside coverage, under gas off or with an empty input; no marker respawn or hidden inventory restoration.
- Same epoch reached by pause, seek, rewind, manual playback and auto slow motion has the same material/scientific state. No use of engine wall-clock time for gas detail.
- Homogeneous slab transfer oracle, density/column reconstruction checks where applicable, ray/segment refinement and front/back self-occlusion at face, edge and underside views.
- Repeated actual-shader Doppler-sign, far-side curved/straight and single-hole controls; detail off/on comparisons at identical fields, time, exposure and camera.
- Image sampling at initialization, pre-merger, common horizon, remnant and the end. Inspect slow camera movement at a paused epoch for screen-space grain, stretch resets, subpixel flicker and bands.
- Auto/manual/reverse time-map fixtures and 30/60/144-fps crossing tests; physical readouts and waveform cursor must remain synchronized. Display-time smoothing cannot disguise source-data discontinuities.
- Actual Laptop/Balanced/High timing distributions and total memory on named devices, plus real-device scope. Extra field/texture/ray samples have a cost even when no particle sprites are drawn.

The acceptance target is an improved, coherent educational appearance with explicit assumptions. Numerical nonconvergence, the validity of Newtonian near-merger speeds, compatible relativistic gas/metric data, physical spectra and independently validated binary images remain separate work.

## Selected material and numerical evidence

The selected sidecar at `Assets/StreamingAssets/DTET/MiniDisks/sxs0305.mdt` is 5,863,440 bytes, SHA-256 `389b3f0a1d0881edd861e1f64ff5970c2fd2f665809ec3ff714fb495dc838736`. Its metadata identifies the unmodified fluid SHA-256 `bcef6b6c72c268ee9e47dcb1bc652841cd98472629964f4356851d47c8a01edc`, the same 192-square grid and 513 epochs. Both cover elapsed source times 0.6815956577960021–1.2443464499042842 seconds; earlier scientific epochs remain available without gas. The separate `outputs/Material-detail-validation/material-audit.json` checked all 513 compressed payloads for CRC, finite bounds and stored-cell density/velocity masks. The generation report has 15,361 substeps, maximum step 0.14017 tg, 8192 initial/1943 final live tags, 1096 invalid-speed removals and 5153 empty/outflow removals. Marker loss changes label support, not the gas inventory.

This is **not a converged material-pattern solution**. Halving the transport step changes selected late-frame C by density-weighted RMS about 0.22, compared with C's own RMS about 0.17. The source flow is already spatially unconverged; material reconstruction and chaotic stretching add further sensitivity. Deterministic replay of a fixed cache is reproducibility, not convergence or evidence of actual plasma turbulence.

An RK4 experiment was evaluated outside the shipped source and **not adopted**. Comparing Courant 0.375/0.1875 improved an early frame's pattern agreement, but selected late-frame RMS differences remained about 0.21–0.23, with centered correlations about 0.06–0.13. It did not establish temporal convergence and required longer .NET generation. See `outputs/Material-detail-validation/advection-integrator-comparison.json`. No Unity Mono timing or physical improvement is inferred from that experiment; the selected sidecar remains RK2/Courant 0.75.

The material portable suite passed 166 assertions, including frozen/analytic translating/time-varying flow, second-order solid-rotation refinement, invalid-speed/vacuum rejection, deterministic generation, identity binding, corruption and cancellation. Its analytic rotation error ratio was 3.99994 under step halving; that isolated result does not contradict the failed long-history pattern-convergence checks. The separate timing suite passed reverse/endpoints/rate bounds and 30/60/144-fps crossing comparisons. Logs are `outputs/Material-detail-validation/portable-material-tests.log` and `outputs/Plasma-visual-validation/portable-suite.log`.

## Executed graphics and integration checks

The final Float run passed **84 checks**; the forced Half run passed **77 checks**, with no failed records. Both used Unity 6000.3.25f1 / NVIDIA RTX 3060 Ti. Half applies to actual default cache textures; the standalone optical fixtures use their own Float textures in both runs. Checks cover immutable fluid/material replay, unchanged scientific bodies/radiation, pause/seek/image restoration, auto/manual timing, matching profile fields, corrupt-sidecar fallback, cancellation and resource release. Eighteen analytic Gaussian-column cases cover two constant heights and one local-height potential, three densities and two opacities, within the stated `1e-5 + 0.5%` channel tolerance.

Actual-shader fixtures measured approaching/receding radiance ratio 5.88594, curved/straight controlled far-side radiance ratio 3.19432, and isolated Schwarzschild capture-radius error 0.35213% against the analytic control. These establish those particular controls, not a validated binary metric, measured spectrum or physical merger image.

The Float run also generated/loaded a 40/25-solar-mass, initial-separation-35-rg experiment with its matching passive material, paused at its true coverage start and returned to the original replay. Its 399.6463-second elapsed time includes concurrent release-build load and is a **functional completion observation**, not a generation benchmark. Half did not repeat that long solve.

Each final run saved 240 actual default-scene renders at 1280x720, intended for 24-frame/s encoding. The ten-second sequence samples the available gas interval with the automatic display-time map compressed to the clip duration; `movie/frames.csv` records exact source epochs. It is not a continuous native-playback performance recording. Selected off/on, HUD/wrist, edge, underside, merger and end stills were inspected. Finer flow-following patches/stripes and depletion are visible; broad smooth regions, vertically elongated bands from the planar appearance reconstruction and dotted narrow Laptop rims remain. No photorealism, resolved turbulence or all-angle artifact-elimination claim follows.

All three profiles and both texture modes preserved decoded field fingerprint `62036b0b1374e325` and material fingerprint `70e8dd66df662325` at source epoch 0.8785584350339009 seconds, with the same 192-square/513-frame input. Rendering budgets differ:

| Profile | Render size | Ray budget | Float mean completed readback | Half mean completed readback |
| --- | --- | --- | --- | --- |
| Laptop | 640x360 | 192 | 9.85 ms | 10.06 ms |
| Balanced | 800x450 | 256 | 14.50 ms | 14.25 ms |
| High | 1120x630 | 384 | 39.08 ms | 39.39 ms |

These are three completed readback samples per profile, including capture overhead. The Half run ran after release builds and is the uncontended comparison; the Float run's custom solve overlapped builds. The separate 20-frame Laptop/640x360 sample mean/p95 was 16.15/29.39 ms Float and 14.50/22.90 ms Half. None is sustained native FPS, a full camera/epoch performance distribution, or a laptop/headset guarantee. Selective extra rays add visible cost.

Reported playback buffer/texture payload was 2,927,820 bytes Float and 2,337,996 bytes Half. This excludes other Unity/native allocations and is not whole-application RAM. Capture-heavy process sampling peaked at 804,782,080/791,830,528 working-set bytes and 1,781,174,272/1,787,514,880 private bytes respectively. Those runs differ in duration and adjusted-solve activity, so their whole-process peaks are not a controlled format-memory comparison.

The earlier 63-check `gpu-pilot` and 75-check `gpu-thin` receipts are historical intermediate revisions. Final technical passes do not repair the underlying fluid's spatial nonconvergence/superluminal near-merger states, establish pattern convergence, provide calibrated physical spectra or constitute human/expert validation. Packaging, extracted-player checks and native-platform limits are recorded separately in [desktop distribution](DESKTOP_DISTRIBUTION.md).

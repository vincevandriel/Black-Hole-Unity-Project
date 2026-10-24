# M4.1 shared scenario and signal model

`InteractiveBinarySession` publishes one immutable `InteractiveBinarySnapshot` for the bodies, prescribed gas, graph and illustrative optics. Camera and visual-gain settings are separate presentation state. This extension preserves the retained M4 SXS replay and adds an arbitrary-mass **bounded educational experiment**. It does not create new numerical-relativity mergers or measured gravitational-wave signals.

## Two source choices

`RepresentativeReplay` uses the existing [SXS0305 contract](Data/SXS0305/README.md), original source mass, tracks, common-horizon switch and [M4 gas model](M4_REDUCED_FLOW_MODEL.md). `SelectReplay()` restores that preset. Changing its masses independently would invalidate those tracks, so independent mass controls belong to `AdjustableExperiment`; this implementation does not expose a scaled SXS mode.

`AdjustableExperiment` accepts component masses of 5–80 nominal solar masses, smaller/larger mass ratio at least 0.1, and starting separation of 25–50 total gravitational radii. Defaults are 35 + 30 solar masses at 30 rg. Both ratio orientations are validated. Invalid parameters cannot replace the current session. A configuration commit resets time and pauses the new generation.

New calculations use `GM_sun = 1.3271244e20 m³/s²` and `c = 299792458 m/s`, consistent with M2's nominal mass scale. `rg = GMtotal/c²`, `tg=rg/c`; no separate adopted solar-mass kilogram value introduces a different `GM`. Orbit positions are in Unity's XZ plane with Y vertical. A component's nonspinning size proxy is `2 mi rg`, where `mi=Mi/Mtotal`; there is no independent physical-size slider. Retained SXS sizes continue to use the supplied equal-area proxy instead.

## Bounded inspiral and conceptual continuation

The educational model uses the same leading circular quadrupole decay as M1, expressed in total-mass units. For `R=a/rg`, `tau=t/tg`, `eta=M1 M2/Mtotal²`, define `K=64 eta/5`:

```
dR/dtau = -K/R³
R(tau) = [R0^4 - 4 K tau]^(1/4)
dphi/dtau = R^(-3/2)
phi = phi0 + 2/(5K) [R0^(5/2) - R^(5/2)]
fGW = 1/[pi tg R^(3/2)]
```

The circular, slow-motion quadrupole approximation and secular orbital decay are the basis of [Peters' 1964 calculation](https://doi.org/10.1103/PhysRev.136.B1224). The implementation stops quantitative evolution at `R=10`. That is the project's declared validity boundary, not an exact binary ISCO or a claim of high accuracy all the way to that separation. A 20,000-step independent RK4 test verifies implementation of the selected equations; it does not validate their strong-field accuracy.

Before cutoff, let `n=(cos phi,0,sin phi)`. Body A is at `(M2/Mtotal) R n`; body B is at `-(M1/Mtotal) R n`. These positions have the requested separation and a mass-weighted barycenter at the origin. Their coordinate velocities include both the analytic radial decay and angular motion.

After cutoff, the state explicitly becomes `ConceptualMerger` for 200 tg. Separation moves from 10 rg to zero with a cubic smoothstep, and a display phase continues at the cutoff angular rate. A further 400 tg holds a `ConceptualRemnant`. Neither stage has a quantitative frequency, plus/cross strain, calibrated waveform or gas-dynamics prediction. The remnant proxy uses the original total mass and radius 2 rg; the model does not invent a fitted radiated mass or final spin. `MergerProgress` allows the renderer to crossfade the overlapping component proxies and the conceptual silhouette. This visual transition is not a calculated horizon geometry.

The 35 + 30 solar-mass default has `rg=95,980.62747325811 m`, `tg=0.00032015691159668235 s`, initial `fGW=6.050696414603236 Hz`, cutoff at `20.128912968690223 s`, conceptual remnant replacement at `20.192944351009558 s`, and timeline end at `20.321007115648232 s`. Playback speed scales wall-clock viewing only.

## Physical strain has a defined observer

The educational strain has one specified, fixed, local far-field observer. Defaults are `D=10^12 m`, inclination `i=pi/3`, and polarization-basis angle `psi=0`. This distance is not the camera distance, binary separation, LIGO distance, or a cosmological luminosity-distance estimate. Parameters require `D` to exceed 100 times the longest modeled GW wavelength, `pi R0^(3/2) rg`; near-zone queries cannot be labeled far-field strain. Inclination lies in `[0,pi]`; finite cyclic phase/basis angles are reduced modulo their periods before evaluation.

In the chosen transverse polarization basis, define

```
A = 4 eta rg / (D R)
hPlus0  = -A (1+cos² i)/2 cos(2 phi)
hCross0 = -A cos i sin(2 phi)
hPlus   =  hPlus0 cos(2 psi) + hCross0 sin(2 psi)
hCross  = -hPlus0 sin(2 psi) + hCross0 cos(2 psi)
```

The quadrupole far-zone polarization convention and retarded field are described in [Hodge's LIGO-hosted thesis, equations 2.7–2.11](https://dcc.ligo.org/public/0114/P1400091/001/khodge_thesis_20140603.pdf). Overall signs follow the declared phase/basis convention. `StrainEnvelope` is A before inclination; it is not the norm of the instantaneous polarized sample. No detector antenna projection, cosmological redshift, spin, precession, eccentricity or higher harmonics are added. The default initial `A=3.1804231588771917e-9` and `hPlus=-1.9877644742982447e-9` refer to that deliberately close model observer.

`ReferenceStrainAmplitude=1e-8` is a fixed display denominator shared by all educational configurations. Magnification settings must act after that mapping. The physical `HPlus`, `HCross`, observer definition and frequency never change when a visual-gain slider moves. Normalizing each experiment to its own peak would hide mass/distance amplitude changes and is not this contract.

At fixed `a/rg`, doubling both masses doubles rg, duration and strain at fixed observer metres while halving frequency. At fixed separation in metres and fixed mass ratio, frequency scales as `sqrt(Mtotal)` and strain as `Mtotal²`. These distinct comparisons are tested explicitly. Increasing observer distance changes strain as `1/D` while leaving the source orbit/frequency unchanged.

## Retained SXS signal remains a different quantity

In replay mode, `DimensionlessRealH22` and `DimensionlessImaginaryH22` contain the retained `H22=r h22/M` values. `NormalizedRealH22` and `NormalizedImaginaryH22` divide them once by the retained maximum complex-mode amplitude for display; consumers must not divide again. The complex amplitude is exactly one at the selected peak. They are not `HPlus` or `HCross`; those fields remain null in replay mode.

Replay frequency, where available, is estimated from minus the complex H22 phase difference divided by `2 pi delta time`, using a symmetric `±0.5 tg` interval clipped to supported coverage. It is a source-mode phase derivative, not measured detector frequency. Nonpositive or undefined derivatives remain unavailable. Raw horizon coordinate time and radiation retarded time retain M2's independent reference-epoch alignment; this adapter does not turn them into a local spacetime metric.

## Retarded display lookup and missing history

`EvaluateSignal(emissionElapsedSeconds)` never clamps missing signal times to an endpoint. It reports `Available`, `BeforeHistory`, `AfterHistory` or `ModelCutoff`. The adjustable signal stops at the exact validity cutoff. Replay stops whenever the independently aligned retained radiation record is absent, even though the late common-horizon animation continues.

`EvaluateRetardedSignal(receptionElapsedSeconds,radiusRg)` evaluates the source at `receptionElapsedSeconds-radiusRg*tg`. Thus the delay uses one rg per tg, equivalent to c in scenario units. Previously emitted valid samples can still be queried after the source cutoff; no new merger signal is generated. Queries before the beginning of the history return unavailable instead of wrapping.

This method supplies historical samples for a **magnified visual ripple**. For the educational mode, amplitudes remain those at the fixed far-field observer D. Query radius selects a display delay; it does not rescale the strain as a near-hole `1/r` metric. For replay, the complex mode is a radiation-history driver only. A camera-local screen distortion made from either source is not a solved propagation effect through a binary spacetime.

The current renderer compresses the displayed radial spacing by **12:1**, querying this unchanged history at twelve times the displayed radius. This makes outgoing fronts visible at the close inspection scale. A fixed amplitude gain magnifies the signed displacement; neither display choice changes source time, frequency, physical strain or wrist graph values. The shader samples 512 covered/missing history values and displaces the star/gas image without adding luminous wave rings. Static mass bending moves the background star centers before gas composition, using a bounded forward weak-lens approximation. It preserves the star point-spread function and prescribed display flux; it does not solve strong-field multiple images or magnification. It is deliberately excluded from the composed gas image, which cannot supply radiation hidden behind a horizon. Wave displacement remains on the combined image and fades beside the horizon silhouettes.

## Prescribed gas response

The M4 source-field equations and HLSL sampling remain shared. The new scenario constructs a `ReducedBinaryFlowState` at the same epoch as its bodies and signal. Reservoir inner/outer radii are `1.8 R0` and `3 R0`, with bounds rebuilt accordingly. The default box half-extents are 92 rg in X/Z and 18 rg vertically; changing R0 is therefore a real configuration change, not a camera zoom.

The v2 assumed gas model extends its emitting inner edge to `1.25 rhorizon`, including an explicitly prescribed plunging region. This is not a stable circular disk inside the ISCO. Its outer radius remains `0.8 a f(qi)`, using its own body-to-companion ratio and

```
f(q) = 0.49 q^(2/3) / [0.6 q^(2/3) + ln(1+q^(1/3))].
```

This uses the effective-volume Roche-lobe fit from [Eggleton (1983), equation 2](https://articles.adsabs.harvard.edu/pdf/1983ApJ...268..368E) as an explicitly Newtonian geometric scale. The additional 0.8 factor is this exhibit's declared truncation choice, not a fitted relativistic disk edge. Neither the assumed emitting inner cutoff nor a stellar-binary lobe fit proves disk stability around the spinning/inspiraling black holes. The function is exposed only over the supported ratio range 0.1–10. Reference values are `f(.1)=0.20677292414284862`, `f(1)=0.3789205183804563`, `f(10)=0.5781687303325036`; tests ensure the correct reciprocal is used for the companion.

If a minidisk's outer boundary falls inside its inner boundary, its density vanishes through M4's smooth width envelope. Stream geometry is regenerated from the same component positions. Binary stream/disk strengths fade with the conceptual merger progress; the remnant ring grows over the first 160 tg after proxy replacement. A prescribed shared ring grows during the final 600 tg and fades as the remnant ring rises, preventing a presentation blackout. Its survival is an assumed gas configuration, not a hydrodynamic prediction. See [the v2 gas prescription and research limits](M4_REDUCED_FLOW_MODEL.md). The reservoir persists. Body masses, radius proxies, gas positions and displayed phase therefore cannot belong to different scenario generations.

Temperature normalization, gray opacity and reservoir density normalization remain declared assumptions. Rescaling a vacuum binary does not infer a new plasma supply rate. This response does not solve continuity, pressure, viscosity, magnetic fields, shocks, energy balance or physical GW heating. Optical ripple settings do not kick the gas or modify these field values.

## API and state discipline

`InteractiveBinarySession.Model` is the active immutable scenario. `Snapshot` includes generation, context version, scenario/mode/phase, elapsed/source time, duration, rg/tg, component/total masses, separation, phase, optional body proxies, `GasState` and `Signal`. `SelectExperiment(parameters)` and `SelectReplay()` build the replacement before publishing it, increment generation, and reset/pause. Seek/reset change context version while preserving scenario generation. Signed playback rates from 0.01× to 4× are bounded at both timeline ends.

`InteractiveBinaryScenario.Evaluate` clamps scene time to its finite timeline; signal-history methods deliberately do not. `GasSampler`, `SampleGas`, `HalfExtentXZ` and `HalfExtentY` expose the matching field sampler/domain. Replay and adjustable modes share the v2 gas assumptions; the internal adjustable constructor is sampled through the scenario's state, without fabricating an SXS data file. Retained numerical body and waveform histories are unchanged.

No snapshot stores a mutable grid or a growing history. Portable checks observe zero allocated bytes across 1,000 adjustable epoch evaluations after warmup. Parameter replacement can allocate a new scenario; render targets, interface objects, numerical-data loading and total application memory are separate concerns.

`InteractiveBinaryScenarioTests.Run()` covers an independent 20,000-step RK4 orbit/phase calculation, nominal-GM agreement with the existing bounded implementation, polarization/observer/parameter trends, source-domain cutoff, retarded lookup coverage, mass-ratio gas geometry and finite endpoints, exact replay preservation, one-time SXS display normalization, atomic replacement and deterministic rewind. These model checks do not certify the GPU approximation, rendered plots, wrist interaction, device comfort, or laptop performance; those require the integrated player receipt.

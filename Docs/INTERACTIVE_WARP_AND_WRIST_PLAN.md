# M4.1 requirements: adjustable experiments, light warping and suit hologram

**Requested 23 October 2026; implemented in the subsequent interactive desktop observatory.** This remains the original design record. See [current controls and behavior](INTERACTIVE_OBSERVATORY.md), [the release validation](INTERACTIVE_OBSERVATORY_VALIDATION.md) and [remaining scientific/device gates](FINAL_DESKTOP_SCOPE.md). The earlier M4 package does not contain this extension. M5/M6 remain separate scientific work.

The delivered adapter uses its own immutable `InteractiveBinarySession` and a full-resolution illustrated suit projection, rather than embedding the old human-scale wrist rig in astronomical coordinates. The fixed replay is preserved; arbitrary masses/separation belong to the bounded educational experiment. Its far-field waveform observer is declared and fixed. Optional replay mass scaling and tracked XR were not required for this desktop release. The original requirements below preserve the reasoning and optional future directions.

## Experience and parameter controls

The user raises a spacesuit wrist device. A small emitter lights up, a restrained projection cone opens and graph panels unfold above the cuff. The device supplies experiment settings, timeline controls and live readouts while the user explores the binary. Provide a pin/enlarge option for reading graphs on desktop, and an animation-reduction option. Opening the device must also work while the event is paused.

Provide two clear scenario choices:

| Choice | Controls and meaning |
| --- | --- |
| Representative event replay | Preserve the SXS0305 mass ratio, spins and dimensionless tracks. The separation readout and timeline can select a supported recorded epoch. An optional common total-mass scale changes physical lengths and times together; it is then labelled a scaled representative scenario. Preserve the original GW150914-inspired preset as a one-action reset. |
| Adjustable educational experiment | Independent component masses and starting separation create a new scenario. Horizon-size proxies derive from mass and the selected spin/model convention. Recompute the bounded inspiral, graph, assumed gas and optical parameters together. It is not a newly computed SXS merger. |

Use mass as the physical control behind a "mass / size" setting. Do not allow a physical horizon radius inconsistent with that mass/model. An optional display-only enlargement has its own label and cannot alter gravity. For a Schwarzschild reference, use r_s=2GM/c^2; retained spinning SXS area proxies keep their own convention.

Label three different distances explicitly: **binary centre separation**, **camera-to-system distance**, and any **waveform observer distance**. They have different effects. Changing the camera position changes the view; changing binary separation changes the scenario or selects a replay epoch; observer distance controls a defined waveform prediction, not the orbital solution. Show separation in km and total gravitational radii so the user can tell what stays fixed when mass changes.

Reuse the existing bounded circular-inspiral model for the first arbitrary-parameter experiment. In that domain, f_GW=sqrt(GM/a^3)/pi and the existing quadrupole decay connects mass/separation to the chirp. Keep its validity cutoff. Beyond it, arbitrary parameters require a supported calibrated waveform/configuration family or a separately labelled illustrative merger transition; do not extend the early-inspiral equations through merger and call that numerical relativity. A smooth conceptual transition may be offered for exhibit continuity with a visible model-state label and no fabricated quantitative merger graph.

## Two connected optical effects

**Mass-dependent bending around the holes.** Add a bounded approximate lensing layer for stars and emitting gas. Use physical mass and view geometry from the active scenario. The weak-deflection trend alpha=4GM/(c^2 b) is a reference check in its valid domain; a softened/clamped image warp near the holes is an explicitly illustrative extension. A sum of two single-hole image distortions is not an exact binary metric. Use depth/mask information to avoid bending the suit or treating foreground gas as if it were all behind both lenses. Handle moving/overlapping horizons and the remnant continuously. Keep the existing M3 isolated optical control available for comparison.

**Outgoing GW-related distortion.** Add a controllable, magnified transverse stretch/squeeze pattern that travels outward from the system, with phase/envelope linked to the active waveform model and event clock. It should visibly chirp and change when the experiment's masses or separation change. The plus/cross pattern is the physical inspiration, not a claim that a screen-space displacement solves photon propagation through a binary spacetime. [LIGO's polarization explanation](https://ligo.org/science-summaries/gw170814/) and [light-travel discussion](https://ligo.org/faq/) describe the underlying effect.

Use a defined retarded-phase mapping for the approximate display. Express propagation in the same length/time units as the scenario; evaluate historical phase on seek instead of advancing a shader-only phase accumulator. The SXS horizon/radiation records have distinct coordinates and comparison-epoch alignment: their retained h22 is not a measured near-field metric at the camera. If the necessary phase history is unavailable, fade/mark the effect unavailable rather than looping or inventing history. A replay-based ripple is explicitly a visualization driven by that radiation history.

Provide **off / subtle / enhanced** settings and a bounded visual-gain control. Gain changes display displacement only: it must not change waveform values, binary evolution, gas mass or graph axes. If spatial compression or slowed propagation is needed for a teaching view, label that display transform separately. Optional wavefront guides are overlays, not luminous matter. The wrist display should show when magnification or an approximation is active. Reference light-deflection behavior against [Einstein Online](https://www.einstein-online.info/en/spotlight/light_deflection/) and the project's retained optical fixtures; the weak-field coefficient is also documented in this [primary lensing experiment](https://doi.org/10.1088/1367-2630/ace98e).

## Accretion response and shared state

Changing masses/separation must rebuild the reduced gas configuration consistently: component-scaled inner radii, supported disk extent, stream geometry, velocity diagnostic, emissivity inputs and transition timing. Derive and test the chosen mass-ratio/truncation prescription before exposing it. Preserve declared gas normalization and thermodynamic assumptions; vacuum mass scaling does not automatically scale every plasma/emission process.

M4 currently uses an analytic, nonconservative prescribed field and fixed weighted density samples. Its first adjustable extension can provide an explicitly **prescribed response to the new binary parameters**. True pressure, magnetic transport and hydrodynamic evolution require the later fluid backend. The optical wave visualization may distort the observed gas image; it must not apply a radial "wave kick" to the gas or become an additional gravity force. Any later physical GW/gas coupling belongs to a consistent metric/fluid model.

Introduce one immutable scenario snapshot consumed by bodies, gas, optics and every display. Include scenario ID/generation, source time, masses, radii convention, separation, orbital phase, waveform source/coverage, signal values, gas-model parameters and units. Keep camera/presentation settings separate. Apply parameter changes as one new generation with a defined reset/pause; coalesce slider previews and discard stale work so a new graph cannot appear beside old gas. Playback rate changes the pace of viewing, not the physical frequency at a given epoch.

## Wrist graphs and rendering integration

Reuse the old `VirtualWristRig`'s pose boundary and captured raise/lower behavior, but connect it to **M4's** `ReducedBinaryExhibit` through a presenter adapter. `PartnerLessonHud` belongs to the older lesson path: instantiating it must not create a second simulation or independent controls.

The wrist pages should offer:

- Source masses, derived size proxies and binary separation, with units and model labels.
- The active waveform and cursor; preserve normalized Re(H22), comparison-epoch alignment and unavailable coverage in SXS mode. Show strain only when a defined distance/orientation/polarization model actually supplies it.
- GW frequency where valid, plus amplitude/visual-gain readouts that distinguish signal from magnification. Use explicit axes/scales; automatic normalization must not hide parameter-driven amplitude changes.
- Gas-model assumptions/diagnostics, playback state, scenario settings and light-warp controls.

All plots and numerical labels read the same scenario generation and time as the scene. Raw H1/L1 observations and any future ET forecast stay separately identified. The existing [GW150914 detection paper](https://arxiv.org/abs/1602.03837) remains an observed-event reference, not evidence for a custom experiment or its bright gas.

M4's astronomical camera renders to a capped target, then a separate full-resolution interface camera presents it. Apply lens/ripple effects to the astronomical scene **before** composing the spacesuit, wrist projection, graph text and pointer targets. This maintains readable data and avoids making the entire interface wobble. Allocate at most a bounded additional target per eye/path if needed; reuse it across frames and release it on return to the launcher. Lower profiles can reduce effect resolution/sampling while preserving phase and scenario data.

Define the suit/pose presentation scale explicitly: M4 world units are total gravitational radii, whereas the old wrist rig uses human-sized offsets and `Camera.main` resolves to M4's interface camera. Supply the intended camera/pose transform instead of blindly parenting a human-sized hand into astronomical coordinates.

Use unscaled interface time for the cuff activation, projection reveal and input feedback; use event time for wave phase and graph cursors. Prefer a short stable reveal with a readable backing surface over perpetual text flicker. Desktop V raise/lower and mouse controls come first, with camera input suspended while interacting with the device. Later tracked XR needs per-eye rendering, pointer/hand input, pose loss/reacquisition, recentering, seated reach and real hardware comfort tests; the existing tracked-pose hook alone is not a working XR implementation.

## Implementation order and acceptance

1. Add the scenario/presenter contract and adjustable experiment with bounded units/validity checks; retain the original replay preset.
2. Connect one signal provider to scene, gas and graphs. Validate parameter changes and deterministic seek before adding distortion.
3. Add mass-dependent approximate lensing, then the separate waveform-driven ripple with off/gain controls, depth/mask handling and quality budgets.
4. Move M4 graphs/settings into the wrist projection, add the short suit-device animation and desktop pin/enlarge fallback.
5. Build/test the combined extension and publish a separate M4.1 receipt/package. Retain the existing M4 device-test obligations; do not mark M5/M6 complete.

Required checks: expected parameter trends at explicitly fixed units (mass scaling at fixed a/rg is different from increasing mass at fixed metres); valid domain and finite near-hole output; off/zero-gain restoration; unchanged physical signal under cosmetic gain; graph/body/gas/warp generation agreement; repeat-seek determinism and missing-history handling; smooth merger display transition; readable above/edge/below views; paused event with responsive wrist animation; input focus; repeated open/close/unload allocations; and measured performance with both optics and the wrist enabled. Scientific tests and illustrative visual checks must have separate names and tolerances.

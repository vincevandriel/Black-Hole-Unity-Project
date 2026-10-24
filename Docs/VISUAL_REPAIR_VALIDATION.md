# Disk, inspiral and outgoing-wave visual repair

Historical receipt for the VisualFix package. The later [stellar-light update](STELLAR_LIGHT_VALIDATION.md) replaces the wave composite described below: mass and wave effects now act only on background stellar sources. Use [desktop distribution](DESKTOP_DISTRIBUTION.md) for the current package.

Runtime source: `070827f36f18fc16faa29e62b68600c7d14e0f14`, 24 October 2026. Unity **6000.3.25f1**, Windows Direct3D12, NVIDIA GeForce RTX 3060 Ti. This receipt covers the revised desktop observatory and shared gas display. It does not replace the numerical convergence limits in [generated fluid validation](GENERATED_FLUID_VALIDATION.md).

## Defects and resulting behavior

The former static optical pass sampled a completed image containing gas and black horizon proxies. Its near-hole mapping created repeated white arcs and disconnected copies of emitting gas. Static mass bending now moves only background star centres, before gas composition. The original stellar point-spread profiles and fluxes are retained; the effect no longer has access to gas pixels that it could duplicate. This is bounded weak deflection, not strong-field disk-image lensing.

The old illustrative inner edge left very narrow emitting annuli. Later tidal truncation could leave no disk support, and component weights reached zero before the remnant field became visible. The v2 prescription adds explicitly assumed emission inside the old inner boundary, plus a shared ring over the final 600 gravitational time units that hands over to the remnant. Warm spiral emission bands and finer bounded quadrature near the holes make the field legible. These changes do not assert a measured bright GW150914 environment or a new binary fluid solution; [the model](M4_REDUCED_FLOW_MODEL.md) records the assumptions and literature.

The starting camera now frames the pair rather than the full reservoir, and the wrist starts lowered. PLAY INSPIRAL, RESTART and WATCH MERGER are visible without opening the wrist. WATCH MERGER frames the late binary and plays it at 0.01 source seconds per real second. The camera then remains independent of the event; pause, seek, reverse and arbitrary viewpoints remain available.

The outgoing optical disturbance samples the same signed, retarded waveform as the scenario. It displaces existing star and gas light without adding emissive wave objects. Default gains are mass 0.35 and wave 1.3. Displayed radial spacing is compressed **12:1**, with magnified displacement, so propagation is visible at exhibit scale. The wrist labels this choice. Physical source strain, frequency, numerical tracks and graph values are unchanged. [The optical contract](INTERACTIVE_OPTICS.md) describes the separate rendering stages and limits.

Generated fluid keeps the same solver, cached density and momentum, grid, physical cadence and mass ledger. Its emission follows the computed field rather than adding the illustrative spiral bands. A circular display fade hides the square grid edge; the full simulation domain remains in the stored diagnostics. Selecting generated fluid frames its wider reservoir so the camera does not open inside an empty cavity.

## Automated and visual evidence

The complete portable model suite passed with exit 0. The final QA player was copied to a separate immutable test directory before execution, so subsequent release builds could not overwrite a running player's resources.

Both observatory receipts report success with no failed check records:

| Final observatory run | Non-capture assertions | Capture-write checks | Saved images |
| --- | ---: | ---: | ---: |
| Normal Float | 210 | 162, including 120 sequence frames | 166 |
| Forced supported Half | 212 | 42 | 46 |

Four analytical-mask overlays are saved independently of the capture-write check list. The Float receipt therefore contains 372 PASS records and the Half receipt 254; sequence writes are not counted as additional physics assertions.

Evidence includes:

- Actual proxy meshes compared with the actual camera's optical-ray mask in four viewpoints: 98.52–99.03% intersection/union; maximum centroid disagreement 0.0991 pixels. This checks projection alignment rather than only isolated formulas.
- Nine GPU fixtures call the same forward-deflection helper as the production sky shader; maximum CPU/GPU absolute discrepancy is 4.09e-9.
- Wave-only versus off images differ in all four viewpoints, with mean absolute RGB differences 3.76e-4–8.20e-4 in the tested Float views. Signed-history fixtures independently check propagation, zero gain, supported history and rewind.
- All 512 uploaded waveform bins are read back and decoded using the real Float/Half convention. Worst Half discrepancies are 1.205e-4 for replay and 6.100e-5 for the adjustable signal in normalized display units, within the declared tolerance. CPU scientific values retain their precision.
- Separate warm-emission checks around both projected holes, late inspiral framing, common-horizon CPU/GPU gas samples, retained camera position through merger, restart semantics, parameter transactions, graphs and disposal.

Final generated-fluid integration passed **41 check records / 10 images** with Float and **42 check records / 10 images** with forced supported Half, including GPU density/velocity agreement, rewind, coverage, cancellation, parameter invalidation and resource release. Ten records in each run check capture writes; the remaining 31 and 32 are non-capture assertions. The solved history retains 14,277,720 bytes of array payload.

Visual inspection covered the first view, above/edge/below, late inspiral, common horizon, post-merger and frames from a 120-frame sequence. The detached white glow and repeated static gas copies are absent. Both illustrative disks remain readable before coalescence, with emitting gas through the transition. Strong amplified waves wrinkle the surrounding image during merger. Generated-fluid captures show the resolved circumbinary ring and its later depletion/asymmetry, without a luminous square boundary. They do not show the detailed prescribed mini-disks; that distinction is intentional.

Balanced/Laptop images remain limited by their render resolution and volume samples, especially nearly edge-on. The computed fluid is coarse and smooth. Neither the visual review nor the automated image differences establish physical strong-field optical accuracy.

## Retained evidence and execution limits

The local research workspace retains `outputs/Visual-fix-validation/`: `portable-wave-tests.txt`, `portable-v2.log`, `qa-build-final.log`, final Float/Half observatory and fluid directories, `verified-runtime/`, release build logs, platform assembly audits, `macos-static-verification.json`, and the extracted Windows startup receipt. Delivery files are in `outputs/Visual-fix-release/`, outside Git and the Unity source.

The `first-player` and `second-player` candidates passed automated checks but were rejected after visual inspection because residual optical artifacts remained. `first-fluid` is an invalid interrupted diagnostic: its live runtime directory was overwritten by a rebuild before its final resource reload. It is not a passing receipt. Final tests use the copied runtime; no production code was weakened to bypass that failure.

Both non-development releases were rebuilt from the runtime revision above. Their assemblies contain the direct-startup marker and omit development capture classes, forced-Half test switches and the QA-only texture accessor. [Desktop distribution](DESKTOP_DISTRIBUTION.md) records the exact package and platform validation. The Mac build is universal and ad-hoc signed, not notarized or runtime-tested on a Mac. Windows captures are hidden/offscreen, not a native-window usability study, laptop certification or tracked XR validation. Build timings overlapped independent work and are not controlled performance benchmarks.

SXS0305 remains a representative numerical replay associated with GW150914, not its unique posterior reconstruction. Bright gas, plunge emission, shared merger emission and amplified optics remain declared assumptions. The generated fluid remains a 2D Newtonian/isothermal approximation, with detailed flow not resolution-converged. This repair does not provide GRMHD or evolved binary metric data.

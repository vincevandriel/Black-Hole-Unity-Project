# Background stellar light: validation record

Runtime source: `605b8b9c1eaad39e4115b16d277d38a800ae8a61`, 24 October 2026. The corrected Windows observatory passes both Float and forced-supported Half integration runs, with independently reviewed source-light captures. The implementation is described in [INTERACTIVE_OPTICS.md](INTERACTIVE_OPTICS.md). The preceding full-image wave implementation and its historical release are documented separately in [VISUAL_REPAIR_VALIDATION.md](VISUAL_REPAIR_VALIDATION.md).

## Current status

The third QA build completed with exit 0. Both corrected observatory receipts report success, with **492 non-capture assertions**, **232 capture-write records** and **240 physically present PNGs** in total. Zero gains after active bending now reproduce the actual optical-off image, and the main bending control restores visible defaults after wrist Off. These pixel/control checks pass for all four profiles in both storage modes. The visible control label reflects the current effective state.

Both non-development players built successfully. The extracted Windows release passed its 15-second default-startup smoke check. The universal Mac bundle passed static architecture, dependency and embedded code-page-hash inspection. The archive and independent extracted inventories match, and a separate 16-check archive audit passed. Both generated-fluid regressions also passed, bringing the four runs to **555 non-capture assertions**, **252 capture-write checks** and **260 images**. The Mac has not been runtime-tested or notarized.

## Implemented behavior

Mass bending and the amplified outgoing-wave displacement act only while rendering background stellar light. Distant stars retain normalized, compact point-spread functions with varied brightness, color and apparent size. Eight brighter references occupy the opening view; adding higher-quality catalogue entries preserves existing source values.

The featured source has an assumed physical solar radius at one AU from the system barycentre. It subtends approximately 0.53 degrees near the binary, rather than filling the sky. Its fixed world position is composed for the demonstration, not inferred from GW150914. An inverse summed thin-lens equation evaluates its finite limb-darkened source, producing secondary images and arcs as the binary moves. The `STARLIGHT VIEW` control exposes that source without foreground gas, while `BENDING: ON/OFF` permits comparison with the unbent source. Restart restores the accretion view.

Gas and horizon proxies render after stellar light; neither is sampled through a completed-image optical warp. The stellar pipeline owns no additional image-sized render target and performs no post-render scene blit. It reuses the 512-entry waveform texture and fixed CPU buffer, plus an immutable sky mesh and featured-source quad. The finite source adds fragment work, not particle groups or an image-sized cache.

## Completed observatory evidence

The audited receipts are `outputs/Stellar-light-validation/release-float-observatory/interactive-exhibit-integration.json` and `release-half-observatory/interactive-exhibit-integration.json` in the local task workspace. Both report `success=true`, `performanceOnly=false`, Unity **6000.3.25f1**, **Direct3D12**, **NVIDIA GeForce RTX 3060 Ti**, and **AMD Ryzen 5 5600X**. All recorded check strings begin with `PASS`.

| Final observatory receipt | Non-capture assertions | Capture-write checks | Total records | PNGs | Sequence frames |
| --- | ---: | ---: | ---: | ---: | ---: |
| Float | 245 | 176 | 421 | 180 | 120 |
| Forced supported Half | 247 | 56 | 303 | 60 | 0 |

Four analytical projection overlays per run are recorded as images outside the capture-write assertions, explaining the image-count difference. Capture writes are not counted as extra physics tests. The Float run's 120-frame sequence covers early recorded inspiral through `min(0.5 seconds, 0.7*merger_time)`; it is a stellar-image sequence, not a full merger movie.

Specific completed evidence includes:

- Actual finite-source image centroids versus Unity's independent world projection at four yaw/pitch combinations: maximum disagreement **0.02586 pixels**, below the 1.25-pixel limit. Both vertical signs were tested to expose render-target orientation errors.
- Twelve fixtures exercise the actual featured-star shader with distant, finite-depth, foreground and bounded-core configurations. Maximum CPU/GPU absolute deflection discrepancy: **4.918e-8**.
- Nine fixtures exercise the same bounded point-star helper as the production sky shader. Maximum discrepancy: **4.090e-9**.
- All 512 actual uploaded waveform bins are decoded against CPU requests. Float's worst replay discrepancy is **1.478e-8** and adjustable-signal discrepancy **1.103e-8**. Half's corresponding maxima are **1.205e-4** and **6.100e-5**, within the declared tolerance. These are normalized display units; CPU scientific signal values retain their precision.
- Four independent horizon-mask comparisons retain **98.52–99.03%** intersection/union and maximum centroid disagreement **0.0991 pixels**. These test actual camera/mesh projection, not just a repeated isolated formula.
- Wave-on/off stellar images differ in all four viewpoints and both storage modes: mean absolute RGB differences **1.851e-5–3.968e-4**. Those metrics confirm image response; they do not quantify a physical astrometric effect.
- With both star renderers hidden, varying mass/wave controls leaves the foreground gas image unchanged within the integration threshold. The finite source is resolved and occupies less than 1% of scene pixels when unbent. Source coordinates remain fixed while the observer moves at a paused epoch.
- Rewind restores the prior finite-source image. Starlight/restart control behavior, source-history coverage, per-hole gas visibility, scenario/graph transactions, wrist input, resource disposal and the compatibility path pass the recorded checks.
- After previously active bending, zero gains restore the actual optical-off pixels. Two additional transport checks per profile verify that the main bending button re-enables visible defaults after wrist Off and subsequently restores the source image without seeking. These checks exercise the corrected behavior rather than only checking which render target is returned.

The passing `portable-final.log` includes catalogue/PSF invariants, independent analytic single-lens image positions and Einstein-ring checks, finite-source geometry and physical-size rescaling, waveform history, reduced gas, and existing generated-fluid PDE/conservation tests. Its generated default history reports **19,541 steps**, **14,277,720 bytes** of array payload and mass residual **-3.5811e-11**. The final UI/uniform-gating corrections do not change those pure C# model implementations.

Fresh Unity generated-fluid integration passed **31 non-capture assertions / 10 captures** in Float and **32 / 10** in forced supported Half. Both retain **14,277,720 bytes** of history payload, with solve times **97.74 and 94.03 seconds**. GPU density/velocity checks, coverage, rewind, parameter invalidation, cancellation and cleanup passed. These runs verify integration with the revised stellar pipeline; they do not change the existing solver's scientific validity or convergence limits. Independent work ran concurrently, so these are not controlled performance comparisons.

## Visual review performed

Direct inspection of corrected final images covered Float's `balanced-first-view.png`, isolated unbent/lensed/wave source captures and sequence frames **000, 030, 060, 090 and 119**, plus Half's `starlight-view.png` and `revised-common-horizon.png`. The candidate's source-projection capture was also reviewed separately; final automated projection measurements match it.

The opening view contains both warm accretion structures and a varied star population. The isolated unbent featured source is a compact disk. Its lensed images appear as distinct bright patches/arcs and change position and shape across the inspected sequence frames. Adding the wave contribution changes those source images. The common-horizon gas remains visible. The bright stellar arcs are intentionally mapped background-source light; they are not gas copies or emissive wave rings.

This review inspected selected frames, not every frame as a continuously played video. It does not establish smooth native-window playback, physical thin-lens accuracy near the binary or arbitrary-device performance. Both final storage modes retain the intended stellar images and visible foreground gas.

## Timing and log limits

Each final receipt has 16 short, approximately two-second timing windows. They include the offscreen scene with source-level stellar optics, complete interface, player-loop yield and one-pixel GPU completion readback; PNG writes and full-image readbacks are outside those windows. The pipeline has no separate optical composite target or blit. Float results are:

| Profile | Scene resolution | Mean frame time across tested modes | Highest p95 among modes |
| --- | --- | ---: | ---: |
| Laptop | 640 x 360 | 3.74–5.82 ms | 6.41 ms |
| Balanced | 800 x 450 | 5.51–7.45 ms | 7.88 ms |
| High | 1120 x 630 | 10.78–12.77 ms | 13.49 ms |
| Compatibility | 640 x 360 | 2.61–4.78 ms | 6.50 ms |

Every measured frame in both runs reports a completed readback. The Half run's highest per-mode p95 was 16.30 ms on High. These short desktop results are not native presentation FPS, standalone GPU timing, a long-duration performance test, a controlled Float-versus-Half speed comparison or certification of actual laptop hardware.

The third QA build reports successful completion but also contains Unity licensing diagnostics and URP `TraceRenderingLayerMask.urtshader` host-type error messages. Both final player logs contain a D3D12 debug info-queue query warning and end with `DTET_INTERACTIVE_EXHIBIT_INTEGRATION_PASS`. This audit found no runtime exception or shader compilation failure in either player log. The successful builds/runs are not a clean-Editor-Console claim.

## Retained provenance and limits

Final evidence remains outside Git under `C:/Users/vince/Documents/Codex/2026-10-23/i-w/outputs/Stellar-light-validation/`. Its hashes are:

| Evidence | SHA-256 |
| --- | --- |
| `release-float-observatory/interactive-exhibit-integration.json` | `6ac608b4d380737f8c1eeff3f97976a363c65347763ae0fb0869f8e590e1a6b1` |
| `release-half-observatory/interactive-exhibit-integration.json` | `790d34e5e5103896ec0eb488d84a9fcfacc8ae1c232eff1bb1e6f3e47bbc4e72` |
| `release-float-fluid/generated-fluid-integration.json` | `679e3d16857526e07b61179fe13ffdbc42d9e9fe582e797e70f13fe838346e0d` |
| `release-half-fluid/generated-fluid-integration.json` | `b6bb386dcd186ef142ceaca75ec2d8eea3ef7076e244d51821b387b3e2fb3af3` |
| `portable-final.log` | `8f4943bb0924e72dd28a6fb49029ba2bd7347907db6adad98cb1e3329c873c8a` |
| `qa-build-3.log` | `364ee910225691c8a86748e892fd74d6d1bd0c0632040862c21dac9a018e2456` |

Earlier `final-float-observatory/` and `final-half-observatory/` directories retain superseded candidate evidence. Review after that candidate found stale optical uniforms when both gains became zero; the earlier target-identity test did not compare pixels. QA3 clears the uniforms, replaces that weak assertion with rendered image identity, and adds transport-control regressions. Final results above are from the corrected runtime, not reused candidate receipts.

SXS0305 is a representative GW150914-inspired replay, not a unique reconstruction. Bright gas and the featured star are assumed. Point-star optics use bounded first-order centre shifts; finite-source optics use an approximate thin-lens inverse mapping with a bounded core, not an evolved binary metric. No spin-dependent geodesics, photon orbits, relativistic lens-motion corrections or image travel-time differences are integrated. Wave displacement is amplified and radial spacing compressed **12:1**; source waveforms and scientific graphs remain unchanged. The fluid solver remains 2D Newtonian/isothermal and detailed binary morphology is not resolution-converged.

All completed image evidence here is Windows offscreen QA. The Mac release has Intel and Apple Silicon slices and ad-hoc signatures; its static checks do not establish native runtime behavior, Gatekeeper trust or notarization. No tracked XR, human usability or laptop hardware result is established by this report. [Desktop distribution](DESKTOP_DISTRIBUTION.md) records the exact package and execution limits.

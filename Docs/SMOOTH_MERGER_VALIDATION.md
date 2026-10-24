# Smooth merger validation

Evidence record, 24 October 2026, Unity 6000.3.25f1. Runtime revision: `9092276b7e6f8128a99e315d3388644810b6ba99`. The corrected merger passed the final Float, forced supported Half and generated-fluid integration runs, sampled visual review, release builds and package checks described below. These are desktop technical checks with the scientific and platform limits stated at the end.

All local receipt paths below are relative to `C:\Users\vince\Documents\Codex\2026-10-23\i-w\outputs\Smooth-merger-validation`. Test players, captures, audit scripts, logs and previews remain outside the player ZIPs.

## Implemented presentation

The numerical replay retains its original SXS body centres, equal-area radii, common-horizon switch and scientific signal. The adjustable experiment retains its existing model cutoff and conceptual merger. A separate opaque presentation surface joins the displayed spheres smoothly; it does not change those snapshots, graphs, physical transforms or the gas solver's forcing.

The retained SXS sphere proxies explain the original visual jump. At the first common-horizon epoch, the individual radii are approximately 1.084886 and 0.879373 Rg, with separation 2.275712 Rg. Their displayed surfaces still have a 0.311454 Rg gap. The first common sphere has radius 1.673167 Rg and does not enclose both previous sphere proxies. These selected data provide centres and equal-area radii, not a measured horizon-surface mesh.

`MergerSurfaceView` displays an illustrative signed-distance surface during the closed interval `[T - 12 tg, T + 40 tg]`, where `T` is the scenario's merger epoch and `tg = GM/c^3`. For the retained 64.5-solar-mass replay this interval is approximately 16.52 ms of source time, or 1.65 presentation seconds at playback rate 0.01. Outside this interval the ordinary sphere renderers are used.

For normalized interval coordinate `u` clamped to `[0,1]`, the blend is:

```text
w = 6u^5 - 15u^4 + 10u^3
k = 2(rA + rB) w(1 - w)
h = clamp(0.5 + 0.5(dB - dA)/k, 0, 1)
dBinary = (1 - h)dB + h dA - k h(1 - h)
dSurface = (1 - w)dBinary + w dRemnant
```

Here each `d` is distance from a sphere centre minus its radius. At zero smoothing width, the binary field uses `min(dA,dB)`. The interval endpoints use analytic sphere intersections: two unchanged sphere surfaces at the beginning and the current remnant sphere at the end. Individual display inputs follow the snapshot before the switch and are held just before the last binary epoch afterward. The remnant display input is held at its first retained value before the switch and follows the snapshot afterward. These held inputs are presentation choices, not extrapolated scientific measurements.

The surface is opaque black and writes depth at the actual ray hit. Its shared intersection function also clips the illustrative and generated gas renderers, so gas behind the displayed silhouette cannot shine through it. Physical proxy objects and transforms remain available; only their mesh renderers are suppressed while the joining surface is visible. The HUD labels this interval `ILLUSTRATIVE SMOOTH JOIN`.

Surface state is evaluated directly from scenario identity, generation and event time, including reverse seeks. The component caches repeated epochs and lazily allocates one material and an uploaded eight-vertex/36-index box. Vertex/index payload is 168 bytes, excluding engine overhead and upload storage. It adds no texture or image-sized render target. Ray work is bounded within a world-space box and capped at 256 steps. The first candidate's 64-step cap was corrected after the grazing-ray defect described below.

## Prescribed gas change

The illustrative stream strength now includes the same radial-availability taper used by each mini-disk. Its weight is `binaryWeight * S(outerRadiusRg - innerRadiusRg)`, where `S(x)` is cubic smoothstep after clamping `x` to `[0,1]`. Thus a stream fades as its receiving emitting annulus closes over a one-Rg interval and vanishes when the annulus has no positive radial extent. Both CPU sampling and the corresponding shader use this expression. The prescribed field identifier changes to `assumed-SXS0305-reduced-binary-flow-v3-stream-taper`.

This removes detached bright stream remnants without adding a conservation claim. The illustrative gas is still a prescribed model. The separate generated-fluid equations are unchanged; its renderer shares the new presentation occluder.

## Completed first-candidate evidence

The following counts were independently read from each JSON receipt, separated by `PASS capture` records, and checked against the PNGs physically present. These are **historical first-candidate results**, superseded by the final runs below.

| Initial run | Non-capture assertions | Capture-write checks | Total records | Actual PNGs |
| --- | ---: | ---: | ---: | ---: |
| Observatory, Float | 546 | 221 | 767 | 225 |
| Observatory, forced supported Half | 548 | 101 | 649 | 105 |
| Generated fluid, Float | 44 | 15 | 59 | 15 |
| Total | 1,138 | 337 | 1,475 | 345 |

All three receipts report success and every recorded check begins with `PASS`. The observatory receipts report Direct3D12 on an NVIDIA GeForce RTX 3060 Ti and AMD Ryzen 5 5600X. Four observatory projection-overlay images per run are outside the capture-write records; they account for the image-count differences. Repeated profile/format assertions and image-write checks are not independent scientific validations.

The first observatory matrix includes 72 local silhouette comparisons per storage mode across four viewpoints, both scenarios and the three transition boundaries, repeated across Laptop/Balanced/High profiles. The largest recorded silhouette symmetric-difference fraction was 0.014475, below the existing 0.035 threshold. The checks also compare the opaque surface with the gas occluder, preserve authoritative positions and signals, verify deterministic rewind, and retain the earlier star-finder, wave-map and accretion checks. A shared renderer/occluder implementation can agree with itself while missing the same grazing ray; these checks did not exclude the defect below.

The complete portable .NET Release suite exited 0 after the stream-taper change. The inspected `portable-test-execution.json` records the executing agent's command, session, exit code and selected output, including stream closure and the existing numerical/field tests. It is a retained execution summary, not raw stdout or a second independent execution. The command was `dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release`. Portable tests do not validate Unity shader rendering.

Initial receipts are under `observatory-final`, `observatory-half`, and `fluid-final`. Despite the historical directory names, these are the first candidate, superseded for release acceptance by the refined runs.

## Defect found after the initial pass

Independent review identified faint bright flecks near the joining neck. A separate ray-reference comparison reproduced 13 reference intersections missed by the 64-step surface marcher in a Laptop-resolution sample. The finite iteration cap could terminate before convergence for grazing rays. Matching visual-surface and gas-occluder checks did not detect this because both use the same intersection helper.

The correction raises the shared cap to 256 and adds a targeted regression with an independent intersection certificate, plus explicit merger mesh/material unload checks. Each test ray contains a CPU-evaluated point strictly inside the signed-distance surface, proving an intersection without relying on another sphere marcher. The rendered surface and gas occluder must each block a bright background on that ray. The original sphere data, merger epoch, signed-distance blend and scientific waveform are unchanged.

## Final integration and visual evidence

All three final receipts report success, every recorded check begins with `PASS`, and physical PNG counts match the receipt image counts. The orchestrating run also reported all three processes exited 0. The final runs used Direct3D12 on the RTX 3060 Ti; forced supported Half exercises half-precision storage on this same hardware, rather than a separate laptop.

| Final run | Non-capture assertions | Capture-write checks | Total records | Actual PNGs |
| --- | ---: | ---: | ---: | ---: |
| `observatory-refined/interactive-exhibit-integration.json` | 603 | 221 | 824 | 225 |
| `observatory-half-refined/interactive-exhibit-integration.json` | 605 | 101 | 706 | 105 |
| `fluid-refined/generated-fluid-integration.json` | 44 | 15 | 59 | 15 |
| Total | 1,252 | 337 | 1,589 | 345 |

Each observatory format passed 18 grazing-ray fixtures: six seed rays across Laptop, Balanced and High profiles. The maximum captured light channel was zero for both the visible surface and shared gas occluder; the least-negative certified interior value was approximately -0.100314 Rg, safely below the -0.01 Rg test bound. All 72 local silhouette boundary comparisons passed in each format; the maximum symmetric-difference fraction was 0.016509 against the 0.035 threshold. Unload checks found no surviving owned merger mesh/material or astronomical render targets. The generated-fluid run separately verified binding and silhouette agreement from another viewpoint, and captured the merger at -6, 0, +12 and +42 tg.

Independent still-image review inspected the final Balanced frames at -6, 0, +12 and +42 tg: separated black lobes become a connected neck, then an elongated silhouette, then the single remnant, with surrounding gas retained. The corrected neck showed no bright pinholes in those inspected frames. A separate reviewer inspected yielded sequence frames 000, 030, 060, 090 and 119 and reported no blocking visual defect. This is sampled-frame review, not a claim that every frame or continuous interactive playback was inspected. The 120-frame, 30-fps `Smooth-Merger-Preview.mp4` is a separate preview outside the ZIPs.

One direct Balanced +12-tg capture has an isolated HUD glyph omission; the inspected adjacent 0 and +42-tg captures display normally. This does not establish a persistent player fault, and no claim is made that every UI capture is perfect. Runtime UI was unchanged by this task.

## Release evidence

The corrected QA build log contains `DTET_QA_BUILD_SUCCEEDED`. Both non-development release logs contain `DTET_DESKTOP_BUILD_SUCCEEDED`. The retained logs are `qa-build-refined.log`, `release-Windows.log` and `release-Mac.log`.

The independent read-only package audit completed 45 checks successfully. It read the actual archives and compared all 166 Windows and 169 Mac runtime files against the current build outputs, plus complete ZIP manifests, hashes, CRCs, path safety, notices and instructions. It confirmed the short root layouts, required runtime dependencies, absence of source/scripts/test/audit/benchmark/capture/debug extras, and all six archived Mac native files having Unix mode 0755. Instructions identify the runtime revision above and retain the current discovery controls. Results and verifier hash are in `independent-package-audit.json`.

Release assemblies retain `MergerSurfaceView`, `WaveSurfaceView`, `StarSourceLocator`, the featured star, exhibit and startup marker. All nine QA classes, two QA APIs, 21 discovered QA environment switches and the additional checked portable-test/editor symbols were absent. This is UTF-8/UTF-16 byte-symbol inspection at every offset, not managed-code decompilation. Both release logs provide stripping evidence for all five shader fixtures. Declared source revision and file equality do not constitute reproducible-build proof.

| Archive in `C:\Users\vince\Downloads\DTET-Merge` | Files | Bytes | SHA-256 |
| --- | ---: | ---: | --- |
| `DTET-Win.zip` | 170 | 39,141,419 | `e2cfe25d4b50351935c059c09e96caf286b104b5285b904ceb4f447cf000dac5` |
| `DTET-All.zip` | 339 | 86,829,999 | `2ec4e641e1cfcd19ec16688458ae6a714556be1ef4781e80a1a81843c6f98566` |

`windows-extraction-startup.json` records Windows PowerShell 5.1 `Expand-Archive` to the short `DTET-Merge\Play` directory. All 170 extracted file sizes and hashes matched; the longest absolute path was 138 UTF-16 units. The player remained running after 15 seconds, emitted its installation marker and had no matched error patterns. This was a hidden startup check, not Explorer UI or scene-visibility validation.

`macos-static-verification.json` reports six native files with 12 architecture slices, no errors and no warnings; its hashes match the archived native files. This verifies bundle structure, dependencies and embedded code-page hashes on Windows. Mac execution and trust acceptance remain untested.

## Scientific and platform limits

The joining surface is an illustrative interpolation of sphere proxies. It is not a solved event/apparent-horizon shape, a binary metric, a geodesic ray tracer or evidence of a new physical result. The bright gas, nearby Sun-like source and sky remain assumptions; no measured GW150914 gas counterpart is reconstructed. SXS:BBH:0305 is representative, and GW150914 was observed by LIGO. No Einstein Telescope event readings are claimed.

The generated fluid remains a two-dimensional isothermal Newtonian calculation with prescribed forcing and assumed vertical structure/emission. Passing its integration tests does not establish GRMHD fidelity or resolution convergence. Source time, waveform coverage and physical values retain their existing definitions.

Windows offscreen captures, short timing windows and hidden startup checks do not establish native-window frame pacing, laptop suitability, headset behavior or human usability. Mac build/static checks do not establish Mac execution, Metal behavior, Gatekeeper acceptance or notarization.

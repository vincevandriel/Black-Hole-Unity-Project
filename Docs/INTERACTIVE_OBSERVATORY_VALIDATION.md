# Interactive observatory: desktop validation receipt

**Passed on 23 October 2026 for runtime revision `0f29dcaeb107490ddf0e18bc5535311056bbda05`.** This is a Windows x64 verification/development candidate. It implements the desktop M4.1 extension; it does not certify scientific GRMHD, exact binary optics, laptop performance, tracked XR or human usability.

## Source and build provenance

Repository: `vincevandriel/Black-Hole-Unity-Project`, branch `physics/interactive-suit-release`, based on the existing M4 branch. No merge or force push to the default branch is required for this candidate.

A new local Git clone was made at `07f42d5` without copying Unity Library state. It was imported and fast-forwarded through the final runtime revision above, then built with Unity **6000.3.25f1**, URP **17.3.0**, Input System **1.17.0**, and `DTET.VisualPrototype.Editor.PrototypeBuild.BuildQaPlayer`. The build checked all twelve custom shaders and reported `DTET_QA_BUILD_SUCCEEDED`. The final build log contains no C# compiler-warning/error or shader-error matches. This establishes a batch build, not a clean interactive Editor Console.

Unity import rewrote two URP probe-rendering resource references and normalized project-settings whitespace in the disposable build clone. Those changes were inspected and were not copied into the editable source. This was a fresh **local-source** clone/import, not an initial download from GitHub.

Documentation and packaging commits may follow the tested revision; their changes do not alter the tested runtime. The package's `runtime-manifest.json` identifies the tested revision and every copied runtime file by SHA-256. The outer package manifest and summary identify the complete archive and extraction verification.

## Passed checks

| Gate | Result |
| --- | --- |
| Portable shared-source suite | **14 groups passed**, including the new adjustable scenario and optics reference tests |
| Interactive observatory | **193 checks passed**, 30 fresh captures, all three rendering profiles and forced compatibility |
| Separate steady High process | **7 checks passed**, four five-second completed-render windows |
| Original M4 regression | **254 checks passed** |
| M3 field/optical laboratory regression | **59 checks passed** |
| M2 numerical replay regression | **42 checks passed** |
| M1 physical foundation regression | **50 checks passed** |
| Artistic/lesson Graph capture regression | **9 groups passed** |

The new suite covers atomic parameter application; physical proxy scaling; invalid-input recovery; shared body/gas/wrist/optics generations; exact instantaneous graph values; cutoff and coverage gaps; a phase-independent envelope for unresolved cycles; the maximum admitted mass envelope fitting the fixed strain axis; pause/reverse/seek behavior; optical off/zero-gain identity; unchanged physics under cosmetic gain; deterministic optical image restoration; normal end-camera composition; normal live-update ordering; paused waveform-texture reuse; UI-time projection animation; pin/reduced-motion behavior; screen-space raycasts and dispatched button actions; selected-control focus; viewport containment at 1600×900 and 1280×720; and owned target release after every unload.

Ten floating-point GPU fixtures compare the actual optical shader against independent lens/polarization formulas and the adjustable gas shader against the shared CPU field. The largest absolute difference was **2.952e-7** (gas), within the declared 3e-4 gas and 2e-6 optical tolerances. These validate implementation of the chosen approximation, not agreement with a scientific binary ray tracer.

The first candidate passed its state checks but exposed an off-screen nested canvas on image inspection. The corrected layout is now covered by viewport/raycast assertions. Later review also fixed under-resolved waveform plotting, high-mass axis clipping, UI update order, focus capture, oversized handles and active-render-target cleanup. The final interactive player log has no matched exception, failure marker, shader error or active-target release warning.

## Rendered-image review

The final laptop-profile experiment, centered/pinned panel and 1280×720 wrist images were independently inspected. Controls/readouts remain within the viewport; slider handles no longer cross labels; the fixed-axis/envelope distinction, current cursor, model labels and cuff/projection are visible. The parent also inspected the unobstructed subtle-optics, edge-on and recorded-remnant images. No critical clipping or overlapping controls remained in those inspected captures. The 1280×720 text is compact; image inspection does not replace a human reading/interaction study.

Thirty automated images include launcher, replay, numerical remnant, adjustable experiment, conceptual remnant, pin, above/edge/below/close views, sample overlay, gas off, optical off/subtle comparison and compact layout. The existence of the other captures is not a claim that every frame or animation was manually reviewed. Native OS pointer input and visible-window presentation were not measured; the harness uses Unity raycast/event dispatch and public commands.

## Performance and memory

Test system: **AMD Ryzen 5 5600X, NVIDIA GeForce RTX 3060 Ti, Direct3D 12**. The final run rendered a 1600×900 interface with the profile-capped astronomical target. Both optics and wrist were enabled. Each timed sample explicitly submitted the scene, optical composite and interface, then completed a one-pixel GPU readback fence. Automatic cameras were disabled during these windows to avoid duplicate passes. No PNG encoding, full-image readback or file writes occur inside a timed window. Stopwatch time includes the player-loop yield and CPU/GPU work; it is **not native presentation FPS or isolated GPU timing**.

The separate High process had a 1120×630 astronomical target and a five-second window for each workload:

| Workload | Completed frames | Mean ms | p95 ms | p99 ms | Maximum ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Paused replay | 777 | 6.44 | 6.83 | 19.68 | 22.10 |
| Experiment playback | 594 | 8.43 | 9.40 | 10.09 | 11.21 |
| Moving edge-on camera | 1,061 | 4.72 | 5.28 | 6.07 | 11.97 |
| Repeated seeks | 599 | 8.36 | 8.82 | 9.28 | 9.90 |

Every timed frame completed its readback. The short windows characterize this run; they are not a long-duration thermal/driver-stability guarantee. The full integration process also recorded sixteen three-second windows across Laptop, Balanced, High and compatibility; these are retained separately from the steady-process figures above.

Windows process memory was sampled every 250 ms. The steady High process peaked at **558,313,472 bytes working set** and **1,081,057,280 bytes private memory**. The full multi-profile/capture test peaked at **996,405,248 bytes working set** and **1,711,845,376 bytes private memory**. The latter includes validation-only image capture/readback and profile cycling. These are process measurements, not dedicated GPU VRAM or guaranteed transient peaks. Unload checks found no surviving owned astronomical render targets; they do not establish absence of all Unity/driver allocations or all possible leaks. Unity's development shutdown telemetry includes residual engine allocations.

## Reproduction and distribution

Run the portable suite, build the QA player, then execute `Scripts/Test-InteractiveExhibit.ps1 -OutputDirectory <fresh-directory>`. Use `-PerformanceOnly -BenchmarkSeconds 5` in a separate process for the steady High path. Run the earlier milestone scripts sequentially; avoid concurrent builds or GPU workloads when collecting timing evidence.

`Scripts/Package-InteractiveExhibit.ps1` accepts the exact build directory, tested revision, passing full receipt and build log. It copies only allowlisted runtime files, selected guides/licenses and opt-in device-check scripts; caches, source checkout, logs, symbols and backup artifacts are excluded. The build log is verified and hashed, not redistributed. It verifies copied runtime bytes, ZIP contents and optional fresh extraction. Keep the executable and all runtime folders together. Start with `START_OBSERVATORY.cmd`; Editor installation is unnecessary for the packaged Windows player.

The working research receipts live in `outputs/Interactive-validation/` in the active research workspace. The package includes the full interactive receipt and runtime provenance; raw build/test logs and captures remain outside editable source.

Before calling this a validated laptop/XR or scientific release, complete the explicit [device and scientific gates](FINAL_DESKTOP_SCOPE.md). Compatible gas/metric acquisition is still required for M5/M6; a higher particle count or an attractive warped image cannot substitute for it.

## Published desktop candidate

[Draft PR #6](https://github.com/vincevandriel/Black-Hole-Unity-Project/pull/6) publishes the source, stacked on M4. `DTET_Observatory_Windows.zip` contains **196 files**, including **168 exact runtime files**, and is **66,132,037 bytes**. SHA-256: `ea0d7233a3a582113dcca8832642a3754679a6cfeeb1c1f5f5b70945a566a7f2`. Every ZIP entry and freshly extracted file was hash-verified. The adapted device-check runner then passed **193 checks** from the extracted package itself. The package includes the preceding validation text; this archive identity and extracted-run confirmation were added to source after packaging to avoid a self-referential ZIP hash.

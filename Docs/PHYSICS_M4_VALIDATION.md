# M4 validation receipt

**Desktop implementation and scoped automated checks: PASS — 23 October 2026. Designated-laptop and visible interactive presentation gates: pending.** This is a reviewable development release of the [reduced binary exhibit](PHYSICS_M4.md). It does not close every device gate in the original M4 plan or start M5.

## Revisions and prerequisite

- Tested runtime: `6176f2eb45953ce05292de543a198f824386589d`, branch `physics/reduced-binary-exhibit`. Subsequent receipt/documentation changes do not alter that runtime.
- M3 prerequisite reverified before M4: published head `890fbf8b51f00d44402013059a3f7553eb8e5f4d`, tested runtime `aa9b3bd2d77a7a2ecdf77acb5a3798f8497c1912`. Retained receipts and the M3 package were checked; its ZIP SHA-256 remained `2e0748c1f7bcaceb89a4b19861eccec6023873e7169311985633bd62efd337fd`.
- Unity `6000.3.25f1`; pinned URP `17.3.0`, Input System `1.17.0`, UGUI `2.0.0`.
- Windows 10 x64 development player, Direct3D 12; NVIDIA GeForce RTX 3060 Ti (8,024 MiB reported), AMD Ryzen 5 5600X, 65,423 MiB reported system memory.

A fresh Git clone of the local implementation branch was created at `bc0e7f9`, without copying generated Library state. It was imported, then fast-forwarded and rebuilt through `6176f2e`. The final portable suite and all final player receipts use that revision. Unity import rewrote two URP probe-rendering resource references in the disposable build clone and normalized settings whitespace; those generated changes were inspected and were not copied into editable source. This was a fresh local-source clone/build workflow; it was not initially downloaded from GitHub. The default branch remains unchanged.

## Passed checks

| Layer | Result and scope |
| --- | --- |
| Portable C# | 12 groups passed, including retained M1–M3 fixtures, reduced-field units/support/continuity, deterministic seeks, independent component integrals and grid refinement, zero-allocation pure evaluations, quality admission and photometric sky/PSF invariants. |
| Unity import/build | Final build exit 0, `DTET_QA_BUILD_SUCCEEDED`, all 11 custom shaders validated. No C# compiler or shader errors matched the final build log. |
| M4 integration | 254 checks passed: pre-allocation launcher, three budgets and forced fallback, shared binary/radiation/gas time, pause/reverse/seek controls, independent exterior camera, common horizon/remnant, 96 distant seeks per profile, resource unload/reload, 18 CPU/GPU field fixtures, 15 image-convergence comparisons, sky photometry and 19 captures. Counts are integration assertions, not independent astrophysical validations. |
| Separate High run | 14 checks passed in a new process with five five-second completed-render windows. Full-image fixtures, PNG captures and profile cycling were skipped. |
| M3 / M2 / M1 regression | 59 / 42 / 50 player checks passed respectively in the final M4 player. |
| Original exhibit/input | Nine retained assertion groups passed; Graph variant captured. This is not a new exhaustive legacy visual audit. |
| Visual review | Launcher, overview, above/below, edge, close, remnant, sample overlay and forced fallback captures inspected. Close-view banding was reduced with inner-region ray refinement; controls and scientific labels are legible. Captures are camera output, not evidence of human input usability or OS presentation performance. |
| Source/log review | Independent read-only review confirmed the final runtime and receipts. No failed assertions, C# errors, shader errors or exceptions matched the final player logs. |
| Package | 168 runtime files match the tested build by SHA-256. ZIP CRC verified; all runtime hashes verified again after extraction. The portable runner was executed from the extracted package. Debug symbols and DoNotShip folders are excluded. |

## Numerical and rendered evidence

CPU and GPU evaluate the same prescribed density and relative bolometric emission. Eighteen field fixtures span four event epochs and include component/support/mask cases. These check implementation agreement; they do not make the analytic gas a solution of the fluid equations.

Independent component integrals were compared with 96-cubed and 192-cubed midpoint grids. At the initial epoch, the largest relative emission errors were about 1.175% and 0.1166% respectively; density-integral errors were about 0.0327% and 0.0018%. An earlier 64-cubed trial missed the emission integral by 11.57%, so it was not retained as an adequate reference.

Five rendered views per profile were compared in linear HDR with a 256-base-segment reference, using the same fourfold inner-region subdivision. The reference is a refinement comparison, not an exact optical solution.

| Profile | Base segments | Maximum relative image L1 error | Maximum integrated-flux relative error | Image L1 threshold |
| --- | ---: | ---: | ---: | ---: |
| Laptop | 64 | 0.1433% | 0.02455% | 2% |
| Balanced | 96 | 0.04961% | 0.008113% | 1% |
| High | 144 | 0.01610% | 0.003421% | 0.5% |

The sky test aims at catalogue star 0 and compares its native HDR pixels with the integrated Gaussian PSF prediction. Maximum central-channel error was **0.1128%** (0.5% limit); integrated-flux error was **0.1261%** (1% limit). Translation produced exactly zero image difference in the test, while rotation changed the sky. The earlier arbitrary brightness threshold on an unselected faint-star view was replaced with this defined photometric fixture.

## Completed-render timing and memory

The hidden development player explicitly renders both the capped scene and the 1600×900 interface. A one-pixel GPU readback and completion wait establish that rendering finished. Stopwatch intervals include source update, requested camera/seek work, canvas update, both renders and synchronization overhead. PNG encoding, file I/O and full-image fixture reads are outside timed windows. Each case has a rendered warmup. Unity's separate GPU timing samples were unavailable and are not reported as zero-cost GPU work.

Five three-second windows per profile cover paused overview, edge view, merger playback, camera motion and repeated seeking. Across those five windows:

| Admitted rendering path | Scene target | Range of p95 completed-frame times | Worst p99 | Largest individual interval |
| --- | --- | ---: | ---: | ---: |
| Laptop | 640×360 | 3.99–5.13 ms | 6.68 ms | 14.95 ms |
| Balanced | 800×450 | 3.94–5.30 ms | 6.06 ms | 18.56 ms |
| High | 1120×630 | 5.23–7.58 ms | 7.92 ms | 9.54 ms |
| High requested, forced compatibility / Laptop budget | 640×360 | 3.54–4.78 ms | 5.94 ms | 20.29 ms |

These are short, completed offscreen measurements on this desktop, not native-window FPS, standalone GPU times or laptop certification. Earlier hidden-player update-loop timings did not establish rendering and were discarded. The retained legacy Graph harness's loop timing is also not used as M4 rendering evidence.

The separate High-only process used five five-second windows at 1120×630, with p95 values of 6.59 / 5.22 / 6.71 / 6.48 / 7.89 ms for the cases above. Its largest individual interval was 8.68 ms. A preliminary High-only run overlapped briefly with another regression player and was excluded; `steady-high-final` is the isolated retained run.

Windows process memory was sampled every 250 ms:

| Run | Peak sampled working set | Peak sampled private bytes |
| --- | ---: | ---: |
| Full integration/capture/convergence QA | 1,110,953,984 bytes (1,059.5 MiB) | 1,905,139,712 bytes (1,816.9 MiB) |
| Separate High-only process | 539,537,408 bytes (514.5 MiB) | 1,025,671,168 bytes (978.2 MiB) |

The full QA process includes validation-only full-image arrays and captures. The High-only process avoids those allocations but still includes the development player, timing harness and one-pixel completion readbacks. These are short sampled process peaks, not a long-duration leak test or dedicated VRAM totals; shorter transient peaks can be missed. The gas field itself is analytic, with no grid-history cache. Render-target payloads of 2.64 / 4.12 / 8.07 MiB are only one component of memory and must not be presented as total application memory.

## Delivery and reproduction

```text
DTET_M4_Windows.zip
bytes: 64623034
SHA-256: ed3caa187633918c2bf3b7566a814cdd51f9f05685820bb1ac4bf20611a40753
archive files: 181
```

Local delivery: `C:\Users\vince\Documents\Codex\2026-10-23\i-w\outputs\DTET_M4_Windows.zip`.

Extract the whole folder and run **START_M4_EXHIBIT.cmd**. Select a budget and enter; the scene starts paused at 0.05×. Space plays/pauses, comma reverses, R resets, M seeks to the common horizon and the timeline seeks while paused. WASD/Q/E and right-drag move/look; 1–5 select views. The run guide includes all controls and model limits. Unity Editor is not required. Separate launchers retain M3 and the M1/M2 lessons.

**RUN_DEVICE_CHECK.cmd** writes the full automated suite to `DeviceCheck`; **RUN_PERFORMANCE_CHECK.cmd** writes the separate High-requested run to `PerformanceCheck`. Capability admission may downgrade that request. Preserve both receipts and memory traces when testing another device. Interactive input, resizing, presentation, comfort and sustained performance still need a visible run on the intended machine.

From source, use the commands in [the exhibit guide](PHYSICS_M4.md), build with **DT-ET > Validate Assets and Build QA Player**, and run the retained M3/M2/M1/artistic runners. Editor entry is **DT-ET > Startup Exhibit > M4 Reduced Binary**, then Play in the staging scene. The Editor selector compiled successfully; automated integration evidence here is from the standalone player.

Evidence is retained outside editable source at `C:\Users\vince\Documents\Codex\2026-10-23\i-w\outputs\M4-validation`:

- `m3-prerequisite.json`, `final-portable.log`, `final-build.log`.
- `reduced/reduced-exhibit-integration.json`, `process-memory.json`, player log and PNGs.
- `steady-high-final/reduced-exhibit-integration.json`, process-memory trace and player log.
- `field`, `numerical`, `physical`, `artistic`: final regression receipts/logs/captures.
- `package-final-smoke`: final extracted-package runner result; `summary.json`: package/publication summary.
- Delivered `runtime-manifest.json`: exact source revision and 168 copied runtime hashes.

## Remaining gates and scientific limits

M4 uses an explicit **prescribed educational field**: circumbinary reservoir, fading minidisks, bowed streams and growing remnant ring. It does not conserve fluid mass or solve GRMHD. Weighted density samples are fixed spatial diagnostics, not integrated material trajectories. The straight-ray renderer and equal-area horizon proxies are not binary strong-field optics. All quality profiles preserve the analytic field and recorded event; their sampling and faint-star tail differ.

The binary is a representative SXS0305 replay scaled for GW150914 inspiration. Its normalized radiation mode is not detector strain or Einstein Telescope observations. Scientific compatible gas history remains M5; compatible metric/light-cone transfer remains M6. Neither has been established by these checks.

The original M4 plan requires actual laptop/desktop presentation measurements. Only desktop offscreen rendering is measured here, so the source is published as a **draft PR** without a passed-device release tag. A forced fallback on this GPU does not validate an old GPU/driver. Dark horizon proxies can be difficult to distinguish against the background at overview scale in gas-off mode or exactly at merger; visibility is a specific item for the pending human usability review. Headset performance, human usability, full VRAM accounting, sustained leak testing and an interactive clean-Console audit are unverified. Logs retain Unity's D3D12 info-queue diagnostic and shutdown native-allocation telemetry; passing scoped checks does not imply an entirely diagnostic-free log or proof of no leaks.

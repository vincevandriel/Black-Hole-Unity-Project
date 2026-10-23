# M3 validation receipt

**Gate: PASS — 23 October 2026.** M3 began after the M2 prerequisite was reverified. This receipt covers the isolated [field and light reference laboratory](PHYSICS_M3.md), with no unresolved release blockers found in the reviewed scope.

## Revisions and prerequisite

- Tested runtime: `aa9b3bd2d77a7a2ecdf77acb5a3798f8497c1912`, branch `physics/field-rendering-reference`.
- M2 prerequisite: `315a5b96eb27241a67fb566a5bc7b2f15d023787`; its tested runtime was `cbacffd1a237b464b9d18c328a67cf8745108b2b`. The intervening changes were attributes/documentation, with no runtime source drift. Its retained build/conversion/test receipts and all 168 delivered runtime file hashes were checked before M3 work. The M2 ZIP SHA-256 remained `d2f42df9000f38348498ee4fdf8d90651d2bf085040e6dbe456f1c989fa890e9`.
- Unity `6000.3.25f1`, pinned URP `17.3.0`, Input System `1.17.0`, UGUI `2.0.0`.
- Windows x64 development player, Direct3D 12, NVIDIA GeForce RTX 3060 Ti.

A fresh Git clone of the local implementation branch was created at `57b15f5` without copying Unity's generated Library. After the initial import/build, it was fast-forwarded and rebuilt for the rendering/UI corrections through `aa9b3bd`. All final player receipts below use that final build. This is a fresh Git source/build workflow, not a claim that the pre-publication clone was downloaded from GitHub. Subsequent documentation commits do not change the tested runtime.

## Passed checks

| Layer | Result and scope |
| --- | --- |
| Portable C# | Ten groups passed at the final runtime revision, including existing M1/M2 fixtures, analytic field mass, grid convergence, two-frame cache, deterministic weighted tracers/resampling, zero-allocation pure evaluation, emission/opacity limits and frequency-shift identities. Schwarzschild tests include independent turning-point quadrature, refinement, capture/escape, disk intersections, view rotations and null residuals. |
| Unity import/build | Final build succeeded, exit 0 and `DTET_QA_BUILD_SUCCEEDED`. All seven custom shaders were validated; no C# compiler or shader errors were found in the final build log. |
| M3 player | 59 checks passed: isolated startup, paused state, timeline/opacity control values and fill geometry, weighted tracer count changes, repeat seeks, camera traversal while paused, forward/reverse endpoints, 96 distant seeks, bounded cache, GPU transfer fixtures, view captures and optical image invalidation/rebuild. These are integration assertions, not 59 independent validations of astrophysics. |
| M2 regression | 42 numerical player checks passed in the final M3 build. |
| M1 regression | 50 physical player checks passed in the final M3 build. |
| Original exhibit/input | Nine retained assertion groups passed; Graph variant captured. This is one legacy capture variant, not a new exhaustive visual audit. |
| Visual review | Final controls, optical overview, above/below/edge-on volume, camera inside the volume and weighted tracer views inspected. UI is legible; optical controls that do not apply to the steady model are disabled. Optical overview reports zero unresolved rays for that view. Repeat-seek volume and optical PNGs are byte-identical. |
| Source/log review | A separate read-only review found no release blockers, confirmed matching runtime sources and checked all three final player receipts/logs. No failed assertions, C# errors, shader errors or exceptions were found. |
| Package | All 168 copied runtime files SHA-256 match the tested build. Two launchers, run guide, field/optical guides, SXS attribution and runtime manifest give 175 archive files. ZIP CRC verification passed; debug symbols and DoNotShip folders are excluded. |

An initial optical capture was black because the hidden-window backbuffer capture did not include the intended camera/UI result. The corrected harness requests an actual URP camera render, with the laboratory UI bound to that camera. Camera-only fixtures temporarily hide the canvases. Visual review then caught overflowing slider fills; their geometry and value checks were added. The final receipts use both corrections. Failed candidate evidence is retained separately and is not counted as passing evidence.

## GPU transfer and memory evidence

The same volume shader renders a uniform white slab to an explicitly linear ARGBFloat target. Its expected exposure-mapped linear value is `0.5028213138817772`.

| Fixture | Measured value | Absolute error |
| --- | ---: | ---: |
| 64 segments | 0.5028219223022461 | 6.084204688949768e-7 |
| 256 segments | 0.5028232336044312 | 1.9197226539535706e-6 |
| Very small opacity, 256 segments | 1.1999855814792681e-5 | 1.8662849043948881e-13 |

The very-small-opacity expected value is `1.1999856001421172e-5`. The 64/256 difference is `1.3113021850585938e-6`. These constant-slab tests check shader transfer and cancellation behavior; they do not establish convergence for arbitrary high-gradient scientific data.

Observed field cache peak: **two frames, 1,048,576 bytes**. That is the two 32-cubed float4 arrays only. Two texture payloads add 1 MiB GPU storage and approximately 1 MiB readable CPU shadow data. Other application/driver allocations are additional. No total-process memory peak or sustained leak test was performed.

## Delivery and reproduction

```text
DTET_M3_Windows.zip
bytes: 64569633
SHA-256: 2e0748c1f7bcaceb89a4b19861eccec6023873e7169311985633bd62efd337fd
```

Local delivery: `C:\Users\vince\Documents\Codex\2026-10-23\i-w\outputs\DTET_M3_Windows.zip`.

Extract the complete folder and run **START_M3_REFERENCE_LAB.cmd**. Unity Editor is not required. Space plays/pauses; comma reverses; the timeline seeks; WASD/Q/E and right-drag move/look; 1/2/3/4 select views; T toggles tracers; O selects the separate optical control. **START_EVENT_EXHIBIT.cmd** opens the retained M1/M2 exhibit; N then Space selects and plays its numerical replay.

Evidence is retained outside editable source at `C:\Users\vince\Documents\Codex\2026-10-23\i-w\outputs\M3-validation`:

- `m2-prerequisite.json`, `final-portable.log`, `final-build.log`.
- `field/field-reference-integration.json`, player log and PNGs.
- `numerical/numerical-integration.json` and `physical/physical-integration.json`, associated logs/captures.
- `artistic/capture-receipts.json`, player log and Graph capture.
- `summary.json`: combined test/package receipt; delivered `runtime-manifest.json`: all copied runtime file hashes.

From a source checkout:

```powershell
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
# Build with DT-ET > Validate Assets and Build QA Player in Unity 6000.3.25f1.
& .\Scripts\Test-FieldReferenceMilestone.ps1 -OutputDirectory C:/temp/dtet-m3/field
& .\Scripts\Test-NumericalMilestone.ps1 -OutputDirectory C:/temp/dtet-m3/numerical
& .\Scripts\Test-PhysicsMilestone.ps1 -OutputDirectory C:/temp/dtet-m3/physical
& .\Scripts\Capture-PrototypeAudit.ps1 -OutputDirectory C:/temp/dtet-m3/artistic -Variants @('Graph')
```

## Evidence boundaries and plan adjustment

M3's volume is a prescribed isolated torus with assumed gray emission/absorption. It is not GRMHD, radial accretion or measured GW150914 gas. Weighted markers visualize the prescribed field; their count does not drive the emitting volume. The separate optical image is a steady thin-disk Schwarzschild control with an approximate Newtonian emission profile and a labelled false-colour band.

The plan's initial Bruneton lookup-table implementation was replaced by independently tested direct double-precision ray integration for this control. Critical-impact/unit conventions and an independent quadrature provide reference comparisons. Bruneton lookup tables, beam filtering and GPU performance work remain deferred. This is not a validated binary renderer or a full port of Bruneton's renderer.

Captures verify Unity camera output, not an OS-desktop/backbuffer capture or human input study. The CPU optical image is deliberately only 192x108 and progressively rebuilt; it is not the final cinematic renderer. Laptop/VR performance, startup fidelity profiles, total memory/leak profiling and compatible binary fluid/metric data remain later gates. Logs contain Unity's D3D12 info-queue diagnostic and shutdown native-allocation telemetry; build startup licensing diagnostics recovered. The passing scoped checks do not imply an entirely diagnostic-free log, clean interactive Editor Console or proof of no leaks.

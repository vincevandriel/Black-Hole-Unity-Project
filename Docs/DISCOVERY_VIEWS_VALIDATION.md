# Wave map and star finder validation

Runtime source `89b9d82107ba753e86f014d1260a55f12891a213`, 24 October 2026, Unity 6000.3.25f1.

## Delivered views

WAVE MAP adds an optional translucent orbital-plane strain diagram. It uses the existing retarded waveform texture, coverage and event clock, with cyan/amber phases, exaggerated height and 12:1 radial compression. It opens near merger with gas hidden. Turning it off preserves time/playback, restores gas and returns to the normal camera. Stellar bending can be disabled independently without freezing the map's history.

FIND SUN-LIKE STAR pauses the current epoch and centers the fixed hypothetical source in a 12-degree telescope view. The one-solar-radius, one-AU dimensions do not change. BENDING compares its original disk and displaced images; the bracket explicitly identifies the unbent direction. The caption sits below the bracket when bending is enabled. Normal camera presets restore the prior optical settings and 52-degree field of view; ACCRETION VIEW also restores gas.

## Final Windows matrix

| Run | Non-capture assertions | Capture-write checks | Images |
| --- | ---: | ---: | ---: |
| Observatory, Float | 373 | 205 | 209 |
| Observatory, forced supported Half | 374 | 83 | 87 |
| Total | 747 | 288 | 296 |

Both final processes exited zero, report success and contain no failed check or matched exception/shader-error marker. Counts include repeated profile/format checks, not independent scientific validations. Tests ran on an RTX 3060 Ti with Direct3D12. The normal-camera tests use the existing QA-only resolution warmup; other captures use explicit render requests. Concurrent release builds mean timing observations are not controlled performance comparisons.

New checks isolate the star's actual contribution by toggling only its renderer. Its unbent core spans 16/20/28 pixels at Laptop/Balanced/High scene resolutions; the centroid differs from Unity's world projection by less than 0.04 pixels. The finder preserves physical dimensions, source epoch and pause state. Lensed images retain measurable source flux; switching bending off restores the reference. Repeated finder entry and normal-view return preserve the saved optical settings.

The grid checks actual renderable geometry, coverage/culling bounds, deterministic rewind with stellar optics Off and with both gains zero, paused viewpoint changes, unchanged scientific state and no extra persistent render target. Toggle-off preserves the event epoch and play state. Scene exit releases the mesh and owned resources.

| Profile | Vertices | Triangles | Mesh payload, bytes | Display grid segments |
| --- | ---: | ---: | ---: | ---: |
| Laptop | 9,216 | 18,050 | 218,892 | 32 |
| Balanced | 25,600 | 50,562 | 610,572 | 40 |
| High | 36,864 | 72,962 | 880,140 | 48 |

These are vertex/index payload estimates, excluding engine overhead and temporary upload copies. The mesh is allocated only on first map use, uploaded without a retained CPU copy and uses the shared history texture. No image buffer is added.

The initial coarser mesh passed functional checks but produced broad angular ridges. The final denser mesh separates geometry resolution from displayed grid density. Actual final Laptop/Balanced stills and movie frames 000/030/060/090/119 were independently inspected: curved bands are connected, phase colors readable, and the star caption no longer overlaps its displaced image. Lower-resolution raster stair-steps remain. This is sampled-frame review, not continuous-playback or native frame-pacing validation.

The separate preview contains 120 actual rendered frames at 30 fps: four presentation seconds and 0.04 source seconds. It is outside the application ZIPs. Shared-clock assertions and frame differences support deterministic evolution; they do not establish a solved metric or measured propagation speed from the pictures.

## Scope and evidence

The QA, Windows release and universal Mac builds succeeded. An independent 45-check package audit verified both ZIPs, all 166 Windows/169 Mac runtime files against their builds, manifests, CRCs, safe paths, notices, current controls and revision links. Nine QA classes, two QA-only APIs and all 21 discovered QA environment switches are absent from both release assemblies by byte-presence checks. Both new production view classes remain. Build logs confirm the five shader-fixture variants are stripped. No test, benchmark, audit, source script, debug symbol, log or capture is included in either ZIP.

Standard Windows PowerShell 5.1 extraction compared all 170 files by size/hash; the longest absolute path is 138 UTF-16 units. The extracted executable remained running for a 15-second hidden startup check with its installation marker and no matched runtime error. This is not an Explorer UI or native-window usability test. Six Mac native files contain twelve Intel/ARM slices with matching embedded code-page hashes and resolved dependencies; ZIP executable modes remain 0755. The Mac bundle is ad-hoc signed and unnotarized; these static checks do not establish runtime or Gatekeeper acceptance.

Final local evidence is in `outputs/Discovery-view-validation`: `observatory-refined`, `observatory-half-refined`, QA/release logs, `independent-package-audit.json`, `macos-static-verification.json`, `windows-extraction-startup.json` and `Wave-Map-Preview.mp4`. Earlier candidate directories remain explicitly separate. [Desktop distribution](DESKTOP_DISTRIBUTION.md) records final package checks and hashes.

No fluid solver or scientific waveform formula changed. This task did not rerun the portable physics suite or generated-fluid integration; their prior results remain in [outgoing-wave validation](OUTGOING_WAVES_VALIDATION.md). The current observatory matrix retains existing gas, optical-reference, scenario and wrist checks.

The source star, sky and bright gas are assumptions, not measured GW150914 counterparts. The map is a scalar strain diagram, not an embedding of spacetime or a full angular radiation solution. SXS0305 remains representative. Mac runtime, Gatekeeper, Metal, laptop and headset performance, and human usability remain unverified.

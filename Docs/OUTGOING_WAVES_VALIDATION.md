# Outgoing stellar-wave display and clean release

Runtime source `40096644fbf2942d169cf79f0f1bdd1c79ce3d09`, 24 October 2026, Unity 6000.3.25f1.

## Change

A fixed procedural stellar backdrop supplies dense faint stars and synthetic galactic light that can visibly stretch into curved bands. It uses the existing signed retarded-wave history and bounded mass lens before foreground gas is drawn. No glowing wave geometry, independent wave clock, new particle population, image texture or extra render target was added. The optical equations, waveform data, gas solver and scientific graphs are unchanged.

OUTGOING WAVES restores illustrative disks, lowers the wrist, seeks to merger minus 250 gravitational time units, selects rate 0.01 and gains 0.35/2, then frames the system at 1.6 times the normal binary-view distance. Both disks remain visible while more surrounding light is exposed. Existing pause, rewind, camera, scenario and wrist bindings remain shared. The adjustable preset begins 50 gravitational time units before its signal cutoff.

## Final validation

| Windows run | Non-capture assertions | Capture-write checks | Images |
| --- | ---: | ---: | ---: |
| Observatory, Float | 306 | 254 | 258 |
| Observatory, forced supported Half | 307 | 74 | 78 |
| Generated fluid, Float | 40 | 11 | 11 |
| Total | 653 | 339 | 347 |

All three processes exited zero, each receipt reports success, and no failed assertion or matched exception/shader-error marker was found. The counts include repeated profile/format checks, not 653 independent scientific validations. The Float observatory includes 180 movie frames for six presentation seconds (source time 0.9198662823881485 to 0.9798662823881485). Four projection overlays are additional images in each observatory run.

The portable Release model suite also exited zero, covering model/session controls, units, retarded coverage, optical references, numerical tracks, radiative transfer and fluid fixtures. No physics implementation changed in this update. Existing shader fixtures still pass after their executable branches were placed behind development-only variants.

New outgoing checks isolate the backdrop from point stars, the featured star and gas in four viewpoints; wave-on changes the pixels, zero gain restores the reference and rewind reproduces the same image. At event time zero there is no positive-radius prehistory. GPU samples follow the same signed emission signal to larger radii at later reception times, and the covered front advances outward. These tests establish the declared retarded mapping; pixel differences alone do not establish visual clarity.

Final actual render captures were inspected separately. Both mini-disks remain recognizable; the wider view exposes curved stellar streaks and distorted galactic bands on both sides and below the reservoir. Independent review inspected the outgoing still and frames 045, 090, 135 and 179. This was still-image review, not continuous playback, native frame-pacing or human usability validation. The initial early-epoch candidate passed numerical checks but was too subtle visually; the later close view obscured the sky with gas. The accepted wider view fixes that composition. A rejected 0.008-rate preview hit the existing 0.01 minimum and was corrected before final runs.

Automatic disk-visibility checks on Laptop/Balanced/High use the existing QA-only windowed-resolution warmup, then read normal camera frames without manually drawing inside those measurements. Other captures request explicit renders. This does not prove untouched hidden cold-start rendering or hardware-specific laptop/XR behavior.

The fluid regression retains visible gas, identical rewind, parameter invalidation, coverage handling, cancellation, GPU field sampling and disposal. Initial generation took 100.36 seconds with 14,277,720 bytes of retained array payload. Builds overlapped part of validation; timings are observations, not controlled performance comparisons. No new forced-Half fluid run was performed; the Half observatory does cover current stellar history and gas-reference fixtures.

## Clean production artifacts

Both non-development builds succeeded. Independent inspection confirms all five shader fixture variants are stripped; nine QA/capture types, two QA-only APIs and 20 QA environment switches are absent from each release assembly. Production types and direct-launch marker remain. Shader property names can remain harmless serialized metadata. Editor licensing/URP diagnostics remain in logs; no clean-Console claim is made.

Repository comments were shortened to concise method/section explanations, units and essential numerical assumptions. Legal notices remain. Reproducible source tests stay in the repository but are excluded from the user ZIPs. The packager rejects source/script extensions and test/benchmark/audit/capture directories, while retaining required Unity runtime files.

Both ZIPs passed 46 independent package checks: paths, CRC, manifest inventories, source-byte matches, notices, revision links, updated controls and Mac permissions. All 166 Windows and 169 Mac runtime files match their current builds. Standard Windows PowerShell 5.1 extraction compared every Windows file and the extracted executable passed a 15-second hidden startup check. Six Mac native files contain both x86_64 and arm64 slices; all twelve slices passed static dependency and embedded code-page-hash checks. Mac execution, Metal, Gatekeeper and notarization remain unverified.

## Evidence and limits

Full local receipts and captures are outside the ZIP and repository in `outputs/Outgoing-wave-validation` in the active Codex workspace: `observatory-final`, `observatory-half`, `fluid-final`, the final QA/release logs, `release-exclusion-audit.json`, `independent-package-audit.json`, `macos-static-verification.json` and `windows-extraction-startup.json`. `Outgoing-Waves-Preview.mp4` is encoded from the 180 actual captured frames, outside the archives. [Desktop distribution](DESKTOP_DISTRIBUTION.md) records the current packages and hashes.

The background, featured star and bright gas are assumptions, not a measured optical counterpart to GW150914. SXS0305 is representative. The effect is an amplified tensor analogy with 12:1 display compression, not light propagation through a solved binary metric. The generated gas remains a coarse 2D Newtonian/isothermal model whose detailed morphology is not converged. See [optical equations](INTERACTIVE_OPTICS.md) and [fluid model](GENERATED_FLUID_MODEL.md).

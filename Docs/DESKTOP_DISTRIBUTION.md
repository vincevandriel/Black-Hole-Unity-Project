# Native desktop distribution

The current visual-repair release opens the interactive observatory and fidelity selector directly from `DTET_Binary_Explorer.exe` on Windows or `DTET_Binary_Explorer.app` on macOS. Unity Editor and mode arguments are not required. Extract the complete ZIP and retain each application's runtime files.

## Current release, 24 October 2026

**DTET_Binary_Explorer_VisualFix.zip** contains Windows x64 and a universal Intel/Apple Silicon app for macOS 12.0 or later. Size: **86,827,666 bytes**, **339 files**. SHA-256:

`2d33e1bbd7c11d111457e86bcc5a601cac2dc41b1416faa70238d391fa87f547`

Both platform runtimes were built from `070827f36f18fc16faa29e62b68600c7d14e0f14` with Unity **6000.3.25f1**. This replaces the earlier desktop ZIP's visuals. The older ZIP and its historical receipts remain outside the repository; they are not the repaired release.

The repair removes detached static optical copies of gas, keeps the illustrative disks legible through inspiral and merger, and makes waveform-driven image distortion visible. The wrist starts lowered, with PLAY INSPIRAL, RESTART and WATCH MERGER controls in the main view. See [visual repair validation](VISUAL_REPAIR_VALIDATION.md) and [controls](INTERACTIVE_OBSERVATORY.md).

Windows development-player integration, extracted release startup and archive verification passed. The Mac build and static inspections passed; it is **ad-hoc signed, not notarized and not runtime-tested on a Mac**. Metal rendering, native signature/resource-seal validation and Gatekeeper acceptance remain untested. The included README links to Apple's per-app first-launch instructions.

## Validation

The final portable model suite exited zero. The QA player and both non-development releases built successfully with no matched C# or shader warnings/errors. All final development-player runs completed with success and no failed check records:

| Windows QA run | Non-capture assertions | Capture-write checks | Saved images |
| --- | ---: | ---: | ---: |
| Observatory, Float | 210 | 162 | 166 |
| Observatory, forced supported Half | 212 | 42 | 46 |
| Generated fluid, Float | 31 | 10 | 10 |
| Generated fluid, forced supported Half | 32 | 10 | 10 |

This totals **485 non-capture assertions**. The Float observatory additionally saves 120 frames for the merger preview, included in its capture counts. Four ray-projection overlays in each observatory run are saved separately from capture-write checks. These are repeated checks across formats, not 485 distinct scientific claims.

The GPU checks cover actual camera projection, shared star-deflection math, all 512 waveform bins, both visible disks, transition gas, parameter/graph binding, rewind, fluid density and velocity, history coverage, cancellation and disposal. Saved default, orbital, merger and fluid views were visually inspected, including selected frames of the rendered sequence. Details and the rejected interim candidates are recorded in the [repair receipt](VISUAL_REPAIR_VALIDATION.md).

The final normal and Half fluid histories took 96.21 and 94.61 seconds on the desktop. Their retained array payload is 14,277,720 bytes. These are correctness runs with independent work occurring on the host, not controlled performance comparisons. The physical fluid grid and cadence remain independent of image quality.

The freshly extracted non-development Windows executable was launched without a mode argument. It logged `DTET_DESKTOP_OBSERVATORY_INSTALLED`, remained running for the 15-second smoke interval, and showed no matched exception, shader-error or project-failure marker. The owned smoke process was then stopped. The engine's unavailable Direct3D debug info-queue message remains; it did not prevent startup. This is a hidden startup check, not native-window or human usability validation.

Both release assemblies retain the direct-launch marker and production classes, and exclude `InteractiveExhibitQaCapture`, `GeneratedFluidQaCapture`, `DTET_FORCE_HALF_TEXTURES_QA` and the QA-only texture accessor.

The Mac static audit found six native Mach-O files, all with `x86_64` and `arm64` slices. Dependencies resolve within the bundle or to system paths. All twelve architecture slices have ad-hoc signatures and matching supported embedded code-page hashes; no Developer ID CMS signature is present. `Info.plist` identifies `org.dtet.binaryexplorer`, executable `DT-ET Binary Explorer`, and macOS 12.0 minimum. `_CodeSignature/CodeResources` is retained. This Windows inspection does not replace Apple's `codesign`, Gatekeeper, Metal or runtime tests.

## Build and test reproduction

Release builds use Mono, `BuildOptions.None`, and the per-build `DTET_DESKTOP_RELEASE` define. The Mac builder requests `x64arm64`. Explicit `--event-lessons`, `--reference-lab` and `--reduced-exhibit` modes remain available. The editor must already be running with the requested build target; matching platform modules and an active Unity license are required.

```powershell
& <Unity.exe> -batchmode -nographics -quit -buildTarget StandaloneWindows64 `
  -projectPath <BinaryBlackHole> `
  -executeMethod DTET.VisualPrototype.Editor.PrototypeBuild.BuildDesktopWindowsPlayer `
  -logFile <windows-build.log>

& <Unity.exe> -batchmode -nographics -quit -buildTarget StandaloneOSX `
  -projectPath <BinaryBlackHole> `
  -executeMethod DTET.VisualPrototype.Editor.PrototypeBuild.BuildDesktopMacPlayer `
  -logFile <mac-build.log>
```

Outputs are `BinaryBlackHole/Builds/DesktopWindows/DTET_Binary_Explorer.exe` and `BinaryBlackHole/Builds/DesktopMac/DTET_Binary_Explorer.app`. Review and restore only incidental editor settings after building; preserve unrelated changes.

Build the development counterpart with `BuildDesktopWindowsQaPlayer`. Copy its complete output to a separate test directory before another build, so live files cannot be overwritten. Run the copied executable with `DTET_INTERACTIVE_QA_PATH` or `DTET_GENERATED_FLUID_QA_PATH` pointing to a new receipt directory. Use `-batchmode` but retain graphics; do not pass `-nographics` to the QA player. Set `DTET_INTERACTIVE_QA_BENCHMARK_SECONDS=2` for the observatory checks. Repeat sequentially with `DTET_FORCE_HALF_TEXTURES_QA=1`, then remove it. Optional `DTET_VISUAL_MOVIE=1` saves the 120-frame merger sequence during observatory QA. These switches and capture harnesses are excluded from releases.

Float field/wave textures require both sampling and linear-filter support, with a checked Half fallback. Half waveform storage uses display normalization to prevent physical strains underflowing, while CPU scientific values retain their precision. Testing Half on this Windows GPU checks format conversion, not Metal or any particular Mac.

## Packaging and evidence

```powershell
.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> `
  -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision 070827f36f18fc16faa29e62b68600c7d14e0f14 `
  -OutputDirectory <new-delivery-folder> `
  -PackageName DTET_Binary_Explorer_VisualFix -MacSigningStatus ad-hoc
```

The packager includes only the two applications, runtime dependencies, short instructions and retained notices. Source, caches, symbols, logs, test captures and the optional preview video remain outside the ZIP. Every archive path, size and SHA-256 matches its input inventory; an independently extracted inventory also matches. All six Mac native entries retain Unix 0755 permissions. This bundle contains no symbolic links, so the verification extraction is complete. Future link entries are preserved and explicitly reported if Windows cannot extract them; they are never flattened.

The local workspace retains `outputs/Visual-fix-release/` for the ZIP, manifest, summary, verification extraction and separate preview MP4, and `outputs/Visual-fix-validation/` for test receipts, captures, build logs, platform assembly audits, static Mac verification and extracted Windows startup. Runtime source association is the release operator's assertion; hashes identify the actual tested and packaged bytes.

The bright gas and amplified, spatially compressed optical waves are declared display assumptions. SXS0305 remains a representative replay, not a unique reconstruction of GW150914. The computed gas is a coarse 2D Newtonian/isothermal model; its detailed morphology is not resolution-converged. This release does not establish GRMHD, a solved binary spacetime, laptop certification, tracked XR or Mac runtime validation. See [generated fluid validation](GENERATED_FLUID_VALIDATION.md) and [desktop scope](FINAL_DESKTOP_SCOPE.md).

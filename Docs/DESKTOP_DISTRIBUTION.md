# Native desktop distribution

The current stellar-light release opens the interactive observatory and fidelity selector directly from `DTET_Binary_Explorer.exe` on Windows or `DTET_Binary_Explorer.app` on macOS. Unity Editor and mode arguments are not required. Extract the complete ZIP and retain each application's runtime files.

## Current release, 24 October 2026

**DTET_Binary_Explorer_Starlight.zip** contains Windows x64 and a universal Intel/Apple Silicon app for macOS 12.0 or later. Size: **86,829,310 bytes**, **339 files**. SHA-256:

`8ef9f1c1f901f0231c53071520d74bed4ca9f70ee8f46869fa20c96e534abb1d`

Both platform runtimes were built from `605b8b9c1eaad39e4115b16d277d38a800ae8a61` with Unity **6000.3.25f1**. This replaces the earlier VisualFix ZIP's optical pipeline. Older ZIPs and historical receipts remain outside the repository.

Mass lensing and amplified waveform displacement now act only on background stellar light. A hypothetical Sun-sized source at one AU forms arcs and secondary images as the binary moves, while the wider sky has varied apparent star sizes and brightness. The accretion view remains legible. STARLIGHT VIEW hides gas to expose the stellar images; BENDING: ON/OFF compares them with the original sources. PLAY INSPIRAL, RESTART and WATCH MERGER remain available. See [stellar-light validation](STELLAR_LIGHT_VALIDATION.md) and [controls](INTERACTIVE_OBSERVATORY.md).

Windows development-player integration, extracted release startup and archive verification passed. The Mac build and static inspections passed; it is **ad-hoc signed, not notarized and not runtime-tested on a Mac**. Metal rendering, native signature/resource-seal validation and Gatekeeper acceptance remain untested. The included README links to Apple's per-app first-launch instructions.

## Validation

The portable model suite exited zero. The corrected QA player and both non-development releases built successfully with no matched C# or shader compilation warnings/errors. Editor logs retain Unity licensing and URP host-type diagnostics; this is not a clean-Console claim. All four final runs report success and no failed check records:

| Windows QA run | Non-capture assertions | Capture-write checks | Saved images |
| --- | ---: | ---: | ---: |
| Observatory, Float | 245 | 176 | 180 |
| Observatory, forced supported Half | 247 | 56 | 60 |
| Generated fluid, Float | 31 | 10 | 10 |
| Generated fluid, forced supported Half | 32 | 10 | 10 |

The four runs total **555 non-capture assertions**, **252 capture-write checks** and **260 saved images**. The Float observatory additionally saves 120 frames for the five-second stellar-image preview, included in its capture counts. Four ray-projection overlays in each observatory run are saved separately from capture-write checks. These are repeated checks across formats, not distinct scientific claims.

The GPU checks cover actual camera projection, the featured source and point-star deflection math, all 512 waveform bins, foreground-gas isolation, zero-gain image identity, on/off controls, both visible disks, transition gas, parameter/graph binding, rewind and disposal. Default, isolated-star, merger and selected sequence captures were visually inspected. Details and superseded candidates are recorded in the [stellar-light receipt](STELLAR_LIGHT_VALIDATION.md).

The final Float and Half fluid histories took **97.74 and 94.03 seconds**, each retaining **14,277,720 bytes** of array payload. Density/velocity GPU sampling, coverage, rewind, parameter invalidation, cancellation and disposal passed. The physical fluid grid and cadence remain independent of image quality. Concurrent build/package work prevents treating these correctness runs as controlled performance comparisons.

The freshly extracted non-development Windows executable was launched without a mode argument. It logged `DTET_DESKTOP_OBSERVATORY_INSTALLED`, remained running for the 15-second smoke interval, and showed no matched exception, shader-error or project-failure marker. The owned smoke process was then stopped. The engine's unavailable Direct3D debug info-queue message remains; it did not prevent startup. This is a hidden startup check, not native-window or human usability validation.

Both release assemblies retain the direct-launch marker and production classes, including `FeaturedBackgroundStar`, and exclude `InteractiveExhibitQaCapture`, `GeneratedFluidQaCapture`, `DTET_FORCE_HALF_TEXTURES_QA` and the QA-only texture accessor. UTF-8/UTF-16 symbol checks are packaging checks, not functional execution tests.

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

Build the development counterpart with `BuildDesktopWindowsQaPlayer`. Copy its complete output to a separate test directory before another build, so live files cannot be overwritten. Run the copied executable with `DTET_INTERACTIVE_QA_PATH` or `DTET_GENERATED_FLUID_QA_PATH` pointing to a new receipt directory. Use `-batchmode` but retain graphics; do not pass `-nographics` to the QA player. Set `DTET_INTERACTIVE_QA_BENCHMARK_SECONDS=2` for the observatory checks. Repeat sequentially with `DTET_FORCE_HALF_TEXTURES_QA=1`, then remove it. Optional `DTET_STAR_MOVIE=1` saves the 120-frame stellar-image sequence; `DTET_VISUAL_MOVIE=1` retains the separate merger-sequence option. These switches and capture harnesses are excluded from releases.

Float field/wave textures require both sampling and linear-filter support, with a checked Half fallback. Half waveform storage uses display normalization to prevent physical strains underflowing, while CPU scientific values retain their precision. Testing Half on this Windows GPU checks format conversion, not Metal or any particular Mac.

## Packaging and evidence

```powershell
.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> `
  -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision 605b8b9c1eaad39e4115b16d277d38a800ae8a61 `
  -OutputDirectory <new-delivery-folder> `
  -PackageName DTET_Binary_Explorer_Starlight -MacSigningStatus ad-hoc
```

The packager includes only the two applications, runtime dependencies, short instructions and retained notices. Source, caches, symbols, logs, test captures and the optional preview video remain outside the ZIP. Every archive path, size and SHA-256 matches its input inventory; an independently extracted inventory also matches. All six Mac native entries retain Unix 0755 permissions. This bundle contains no symbolic links, so the verification extraction is complete. Future link entries are preserved and explicitly reported if Windows cannot extract them; they are never flattened.

The local workspace retains `outputs/Stellar-light-release/` for the ZIP, manifest, summary, verification extraction and separate preview MP4, and `outputs/Stellar-light-validation/` for test receipts, captures, build logs, platform assembly audits, static Mac verification and extracted Windows startup. Runtime source association is the release operator's assertion; hashes identify the actual tested and packaged bytes.

The bright gas, featured star and amplified, spatially compressed optical waves are declared display assumptions. SXS0305 remains a representative replay, not a unique reconstruction of GW150914. The finite-source thin lens is not a binary spacetime solution. The computed gas is a coarse 2D Newtonian/isothermal model; its detailed morphology is not resolution-converged. This release does not establish GRMHD, a solved binary spacetime, laptop certification, tracked XR or Mac runtime validation. See [generated fluid validation](GENERATED_FLUID_VALIDATION.md) and [desktop scope](FINAL_DESKTOP_SCOPE.md).

# Native desktop distribution

The current accretion-view release opens the interactive observatory and fidelity selector directly from `DTET_Binary_Explorer.exe` on Windows or `DTET_Binary_Explorer.app` on macOS. Unity Editor and mode arguments are not required. Extract the complete ZIP and retain each application's runtime files. Select normal **ENTER OBSERVATORY** for visible disks; **COMPATIBILITY / NO GAS** deliberately omits them.

## Current release, 24 October 2026

**DTET_Binary_Explorer_AccretionView.zip** contains Windows x64 and a universal Intel/Apple Silicon app for macOS 12.0 or later. Size: **86,832,709 bytes**, **339 files**. SHA-256:

`25c56fe8ccb643e7f2e8ef6840ab99f02342f10929b7c837dccab71be1b44bd4`

Both platform runtimes were built from `2f1d3a76c235cc41d7d1ac578a1e14044370232f` with Unity **6000.3.25f1**. This replaces the Starlight ZIP with corrected gas-view transitions. No gas shader, fluid solver, waveform or stellar-light mapping changed in this repair. Older ZIPs and historical receipts remain outside the repository.

**ACCRETION VIEW** and **RESTART** restore visible illustrative disks, close framing and the paused beginning, including after gas was hidden or generated-fluid mode selected. **WATCH MERGER** selects visible illustrative gas. Selecting generated fluid enables gas and seeks covered time when needed; selecting illustrative gas also restores visibility. **STARLIGHT ONLY** intentionally hides gas and the header reports it. See [accretion-view validation](ACCRETION_VIEW_VALIDATION.md) and [controls](INTERACTIVE_OBSERVATORY.md).

The existing optical pipeline bends background stellar light. A hypothetical Sun-sized source at one AU forms arcs and secondary images as the binary moves, while the wider sky has varied apparent star sizes and brightness. **BENDING: ON/OFF** compares the original and bent stellar images. Foreground gas is unaffected by these stellar effects. See [stellar-light validation](STELLAR_LIGHT_VALIDATION.md).

Windows development-player integration, extracted release startup and archive verification passed. The Mac build and static inspections passed; it is **ad-hoc signed, not notarized and not runtime-tested on a Mac**. Metal rendering, native signature/resource-seal validation and Gatekeeper acceptance remain untested. The included README links to Apple's per-app first-launch instructions.

## Validation

The corrected QA player and both non-development releases built successfully. Editor logs retain Unity licensing and URP host-type diagnostics; this is not a clean-Console claim. Both final Windows runs report success, exit zero and no failed check records:

| Windows QA run | Non-capture assertions | Capture-write checks | Saved images |
| --- | ---: | ---: | ---: |
| Observatory, Float | 281 | 185 | 189 |
| Generated fluid, Float | 40 | 11 | 11 |

The two runs total **321 non-capture assertions**, **196 capture-write checks** and **200 saved images**. The observatory saves 120 frames for a five-second gas-visible early-inspiral preview and nine raw automatic scene images, included in its capture counts. Four ray-projection overlays are saved separately from capture-write checks. These are repeated integration checks, not distinct scientific claims.

The new ordinary-camera measurements show both disks at the paused beginning and after STARLIGHT ONLY / ACCRETION VIEW on Laptop, Balanced and High. Restored images exactly match their initial counterparts. They read the existing scene target after automatic callbacks, without manually drawing inside the measurements. This evidence follows a QA-only windowed-resolution warmup; the two earlier hidden-launch diagnostics failed automatic rendering and remain recorded. It does not establish untouched hidden cold-start behavior. Presentation and raw images were inspected. See [accretion visibility evidence](ACCRETION_VIEW_VALIDATION.md).

The final fluid run checks restoration from hidden/generated modes, restart outside bounded history coverage, generated-fluid reselection and correct framing after a scenario switch. GPU density/velocity, coverage, rewind, parameter invalidation, cancellation and disposal also passed. The first history took **94.97 seconds**, retaining **14,277,720 bytes** of array payload. The physical grid and cadence remain independent of image quality. Concurrent release work prevents treating this as a controlled performance comparison.

The existing stellar optics, graph binding and disposal checks remain in the final observatory run. The pure-model suite and forced-Half matrix were not rerun for this controls-only repair; the unchanged implementations' earlier passes remain historical evidence in [STELLAR_LIGHT_VALIDATION.md](STELLAR_LIGHT_VALIDATION.md), not fresh counts for this revision.

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

Build the development counterpart with `BuildDesktopWindowsQaPlayer`. Copy its complete output to a separate test directory before another build, so live files cannot be overwritten. Run the copied executable with `DTET_INTERACTIVE_QA_PATH` or `DTET_GENERATED_FLUID_QA_PATH` pointing to a new receipt directory. Keep graphics enabled: do not pass `-nographics` to a QA player. The final observatory run omits `-batchmode` and uses the harness's windowed-resolution warmup before scene allocation to exercise automatic frames; the generated-fluid run uses `-batchmode`. Set `DTET_INTERACTIVE_QA_BENCHMARK_SECONDS=2` for observatory checks. Optional `DTET_DISK_MOVIE=1` saves 120 early-inspiral frames with gas visible; `DTET_STAR_MOVIE=1` saves the stellar-only sequence and `DTET_VISUAL_MOVIE=1` the separate merger sequence. The existing `DTET_FORCE_HALF_TEXTURES_QA=1` switch remains available for a separate supported-Half run. Remove environment overrides between runs. All these switches and capture harnesses are excluded from releases.

Float field/wave textures require both sampling and linear-filter support, with a checked Half fallback. Half waveform storage uses display normalization to prevent physical strains underflowing, while CPU scientific values retain their precision. Testing Half on this Windows GPU checks format conversion, not Metal or any particular Mac.

## Packaging and evidence

```powershell
.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> `
  -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision 2f1d3a76c235cc41d7d1ac578a1e14044370232f `
  -OutputDirectory <new-delivery-folder> `
  -PackageName DTET_Binary_Explorer_AccretionView -MacSigningStatus ad-hoc
```

The packager includes only the two applications, runtime dependencies, short instructions and retained notices. Source, caches, symbols, logs, test captures and the optional preview video remain outside the ZIP. Every archive path, size and SHA-256 matches its input inventory; an independently extracted inventory also matches. An independent **19-check** audit confirms archive CRC, exclusions, Windows x64 headers, both audited assemblies, Mac hashes/permissions and the new README instructions/source links. All six Mac native entries retain Unix 0755 permissions. This bundle contains no symbolic links, so the verification extraction is complete. Future link entries are preserved and explicitly reported if Windows cannot extract them; they are never flattened.

The local workspace retains `outputs/Accretion-view-release/` for the ZIP, manifest, summary, verification extraction, screenshot and separate preview MP4, and `outputs/Accretion-view-validation/` for test receipts, captures, build logs, platform assembly audits, static Mac verification and extracted Windows startup. Runtime source association is the release operator's assertion; hashes identify the actual tested and packaged bytes.

The bright gas, featured star and amplified, spatially compressed optical waves are declared display assumptions. SXS0305 remains a representative replay, not a unique reconstruction of GW150914. The finite-source thin lens is not a binary spacetime solution. The computed gas is a coarse 2D Newtonian/isothermal model; its detailed morphology is not resolution-converged. This release does not establish GRMHD, a solved binary spacetime, laptop certification, tracked XR or Mac runtime validation. See [generated fluid validation](GENERATED_FLUID_VALIDATION.md) and [desktop scope](FINAL_DESKTOP_SCOPE.md).

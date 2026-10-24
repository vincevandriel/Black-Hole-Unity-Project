# Native desktop distribution

The Windows desktop release opens the interactive observatory and its fidelity selector when the user double-clicks `DTET_Binary_Explorer.exe`. Unity Editor, a command script and mode arguments are not required. Extract the complete ZIP and retain the executable's neighbouring runtime/data directories.

## Distribution status, 24 October 2026

The clean **Windows x64** release is built, archive-verified and tested. Its extracted executable passed a default-launch startup check. The final Windows integration runs passed all **495 checks** across the normal and fallback texture formats.

The **Mac application has not been built or delivered**. The matching Unity Mac Build Support (Mono) installer was downloaded through Unity Hub, but Windows is waiting for administrator approval to install it under Program Files. The Windows ZIP contains no Mac placeholder. The source includes a universal Intel/Apple-silicon build entrypoint and a combined packager, but those do not establish a successful Mac build, runtime test, signing or notarization.

## Build and runtime provenance

Runtime source and observatory QA: `f667a0a27d984891a36cd5a80356a94cae415296`. The fluid QA-only follow-up is `90e80bfb66f013d151f327e8e9ea8cd0cb0a0383`; it changes no non-development runtime code. Packaging source: `f553d54`. Unity version: **6000.3.25f1**. Release builds use Mono and `BuildOptions.None`; the development counterpart retains the opt-in test harnesses. Both use the per-build `DTET_DESKTOP_RELEASE` define for the same default-startup selection. Explicit `--event-lessons`, `--reference-lab` and `--reduced-exhibit` modes remain available.

Batch commands, using the installed Unity executable and absolute paths:

```powershell
& <Unity.exe> -batchmode -nographics -quit -buildTarget StandaloneWindows64 `
  -projectPath <BinaryBlackHole> `
  -executeMethod DTET.VisualPrototype.Editor.PrototypeBuild.BuildDesktopWindowsPlayer `
  -logFile <windows-build.log>

# Requires the matching Mac Build Support (Mono) module:
& <Unity.exe> -batchmode -nographics -quit -buildTarget StandaloneOSX `
  -projectPath <BinaryBlackHole> `
  -executeMethod DTET.VisualPrototype.Editor.PrototypeBuild.BuildDesktopMacPlayer `
  -logFile <mac-build.log>
```

The Mac builder requests `Architecture=x64arm64` and a native `.app` rather than an Xcode project. The editor must already be running with the requested build target. The builder checks all thirteen custom shaders, then requires Unity build success. Review and restore only incidental editor changes after building; preserve unrelated user edits.

Outputs are `BinaryBlackHole/Builds/DesktopWindows/DTET_Binary_Explorer.exe` and, after a successful Mac build, `BinaryBlackHole/Builds/DesktopMac/DTET_Binary_Explorer.app`.

## Graphics compatibility

Floating-point texture availability alone does not guarantee linear filtering. The generated-fluid fields and wave-history texture now require both sampling and linear-filter support for Float, then try the supported Half format. The CPU fluid history and waveform/graph values retain their original precision. Physical strains stored in Half use the existing display normalization before quantization so visible wave distortion does not disappear through underflow. If neither format is supported, generated fluid is unavailable and the optical renderer retains its existing lens-only fallback.

Unity's installed 6000.3 API supplies `SystemInfo.IsFormatSupported` with separate `GraphicsFormatUsage.Sample` and `Linear` checks. Metal likewise exposes [32-bit float filtering capability](https://developer.apple.com/documentation/metal/mtldevice/supports32bitfloatfiltering). Exercising the Half path on the Windows GPU is a format-conversion check, not a test of Metal or a particular Mac.

## Validation and retained evidence

Both final Windows QA and non-development release builds succeeded without matched C# or shader warnings/errors. The freshly extracted release started without a mode argument, logged `DTET_DESKTOP_OBSERVATORY_INSTALLED`, remained running for the 15-second smoke interval, and had no matched exception, shader-error or project-failure marker. This is a startup check, not a native-window visual/usability assessment. The engine also logs an unavailable Direct3D debug info-queue interface; this did not prevent startup. The release assembly excludes the two integration-capture classes and the development-only forced-Half environment switch.

Final integration status: **passed**. All four receipts report success with every check passing and fresh captures:

| Windows development-player check | Normal Float | Forced supported Half |
| --- | --- | --- |
| Generated fluid through default launch | 41 passed / 10 captures | 42 passed / 10 captures |
| Observatory and GPU wave-history regression | 205 passed / 30 captures | 207 passed / 30 captures |

The QA build uses the same startup define. Wave fixtures read the actual GPU texture and decode with the real display uniform before comparing against CPU retarded-wave values. Half tolerance is 0.2% plus an absolute sampling allowance; CPU scientific values are not reduced to Half. The worst Half wave errors in the tested replay/adjustable samples were approximately `1.21e-4` and `6.08e-5` in normalized display units; Float errors were below `1.5e-8`. Generated-fluid checks compare GPU density and velocity, deterministic rewind, covered history, cancellation and resource release. The initial fluid histories took 96.18 seconds with Half rendering and 94.47 seconds with Float rendering on this desktop; these runs are correctness checks, not a controlled performance comparison.

Build the development counterpart using `BuildDesktopWindowsQaPlayer`, then run each harness sequentially. Set `DTET_FORCE_HALF_TEXTURES_QA=1` in the launching process to exercise the Half path, and remove it for the normal-format run. This switch is compiled out of non-development players:

```powershell
$env:DTET_FORCE_HALF_TEXTURES_QA = '1'
.\Scripts\Test-GeneratedFluid.ps1 -DefaultLaunch -OutputDirectory <fresh-half-fluid> -TimeoutSeconds 900
.\Scripts\Test-InteractiveExhibit.ps1 -OutputDirectory <fresh-half-observatory> -BenchmarkSeconds 2
[Environment]::SetEnvironmentVariable('DTET_FORCE_HALF_TEXTURES_QA', $null, 'Process')
# Repeat the two harnesses in fresh output directories for the normal format.
```

The local research workspace retains `outputs/Desktop-release-validation/`: build logs, `source-build.json`, `verified-half-fluid/`, `final-half-observatory/`, `verified-float-fluid/`, `final-float-observatory/`, and the extracted release startup receipt. The earlier `final-half-fluid/` run is a failed development diagnostic and is superseded by the owned-texture checks: the original material-property lookup did not expose the sampler texture, so the fixture now inspects the renderer's actual owned texture. The production rendering path did not change. Build-to-source association is the release operator's assertion; hashes establish the identity of the tested/copied bytes. Existing numerical/model validation is documented in [generated fluid validation](GENERATED_FLUID_VALIDATION.md); this packaging change does not claim new convergence, GRMHD, measured bright GW150914 gas or XR validation.

## Clean archive

The Windows ZIP is `DTET_Binary_Explorer_Windows.zip`, **39,138,371 bytes**, containing **170 files**. SHA-256: `fa6747fe49d74251cd043060f8ee9a2faa4ec70da7abc5a07d1375a532769844`.

```powershell
.\Scripts\Package-DesktopPlayers.ps1 -WindowsOnly `
  -WindowsBuildDirectory <DesktopWindows> `
  -SourceRevision f667a0a27d984891a36cd5a80356a94cae415296 `
  -OutputDirectory <delivery> -PackageName DTET_Binary_Explorer_Windows

# Only after producing and inspecting an actual Mac app:
.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision <full-runtime-commit> -OutputDirectory <new-delivery-folder> `
  -MacSigningStatus <verified-or-unknown-status>
```

The packager includes only the selected applications, runtime dependencies, short instructions and necessary retained notices. It excludes source, caches, debug symbols, build logs and QA captures. Every ZIP entry is hash-checked, then an independently extracted inventory is compared. The Windows archive passed all these checks. Manifests, verification extractions and test receipts remain outside the user ZIP.

For a future Mac bundle the ZIP preserves Unix executable permissions and symbolic links. On a Windows account without symlink privileges, the verifier records deferred link extraction explicitly while preserving and checking the real link entries in the ZIP; it never flattens them. These checks do not verify Mac execution, signing or notarization. Unity documents the cross-build details in [Building macOS applications](https://docs.unity3d.com/6000.3/Documentation/Manual/macos-building.html) and [macOS code signing](https://docs.unity3d.com/6000.3/Documentation/Manual/macoscodesigning.html).

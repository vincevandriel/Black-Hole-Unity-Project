# Desktop distribution

Current runtime source: `9092276b7e6f8128a99e315d3388644810b6ba99`, built with Unity **6000.3.25f1** on 24 October 2026. Both applications open the fidelity selector directly and do not require Unity Editor. This release adds an illustrative smooth merger surface with matching gas occlusion and tapers assumed feeding streams as their mini-disks close. WAVE MAP, FIND SUN-LIKE STAR, accretion recovery and outgoing stellar-light distortion remain available.

| ZIP | Contents and layout | Bytes | Files | Longest member |
| --- | --- | ---: | ---: | ---: |
| `DTET-Win.zip` | Windows x64 application at the extraction root | 39,141,419 | 170 | 97 UTF-16 units |
| `DTET-All.zip` | `Windows/` and `macOS/`; universal Intel/Apple Silicon Mac app | 86,829,999 | 339 | 126 UTF-16 units |

SHA-256:

- Windows: `e2cfe25d4b50351935c059c09e96caf286b104b5285b904ceb4f447cf000dac5`
- Combined: `2ec4e641e1cfcd19ec16688458ae6a714556be1ef4781e80a1a81843c6f98566`

Local delivery directory: `C:/Users/vince/Downloads/DTET-Merge`. The ready-to-run Windows application is `Play/DTET_Binary_Explorer.exe`. Mac uses `macOS/DTET_Binary_Explorer.app` from the combined archive and requires macOS 12.0 or later. It is ad-hoc signed, unnotarized and has not been run on a Mac.

## Launch and extraction

Extract the entire ZIP to a short destination, such as `C:\DTET`, then open `DTET_Binary_Explorer.exe`. Keep all neighbouring data/engine files; do not skip or rename DLLs. The Windows ZIP is flat and the combined ZIP has no repeated outer directory. If Explorer reports **0x80010135 / Path too long**, cancel and choose a shorter destination. Mac users should keep the app bundle intact.

Start with Laptop on modest hardware and choose ENTER OBSERVATORY for gas. The top-right WAVE MAP button opens a coloured strain diagram; FIND SUN-LIKE STAR centers and labels the fixed hypothetical source. BENDING compares original and distorted stellar light. ACCRETION VIEW or RESTART restores visible illustrative disks and close framing. OUTGOING WAVES uses a slow, wider view near merger to expose warped background light while keeping the disks visible. Space pauses, comma reverses, WASD/QE move and right-drag looks. V opens the wrist instrument. STARLIGHT ONLY deliberately hides gas. See [full controls](INTERACTIVE_OBSERVATORY.md).

The archive contains only the chosen applications, necessary runtime files, instructions and notices. Source/scripts, tests, benchmarks, audits, logs, caches, debug symbols, captures and preview video are excluded. Required Unity runtime DLLs retain their original names. Package manifests and verification reports remain beside the archives, outside them.

## Verification

The QA build and both release builds succeeded. Final Windows Float/Half observatory runs passed **1,208 non-capture assertions**, **322 capture-write checks** and produced **330 images**. Generated-fluid integration passed another **44 assertions and 15 capture checks**, producing 15 images. The new checks cover silhouette continuity, both gas occluders, rewind, preserved scientific state, six independently certified grazing rays per profile and surface cleanup. Actual final frames show the joining sequence and corrected edge pinholes; a four-second preview uses 120 rendered frames. These are repeated technical checks, not scientific validation or a controlled performance comparison. The portable physics suite passed, including disk-linked stream closure. Full results and boundaries are in [SMOOTH_MERGER_VALIDATION.md](SMOOTH_MERGER_VALIDATION.md).

All five executable shader-fixture variants are stripped from releases. Nine QA/capture classes, two QA-only APIs and 21 environment switches are absent from each release assembly. The merger surface and prior production view classes remain. Harmless shader property metadata remains. Comments in project code were shortened while preserving important assumptions, units and legal notices.

An independent **45-check** package audit confirms CRC, safe paths, complete manifests, correct build bytes, notices, controls, source revision and Mac modes. All **166 Windows and 169 Mac runtime files** match the current build outputs; six Mac native files retain Unix 0755. Assembly exclusions are byte-presence checks, with build-log evidence for shader stripping. This bundle contains no symlinks. The packager preserves future symlinks and reports any Windows extraction restriction rather than flattening them.

Windows PowerShell **5.1.19041.7802 Expand-Archive** extracted all **170** Windows files to `C:/Users/vince/Downloads/DTET-Merge/Play`; every size and SHA-256 matched. The longest absolute file path was **138 UTF-16 units**. The extracted executable remained running for a 15-second hidden startup check, logged its desktop installation marker and produced no matched runtime error. This verifies standard PowerShell extraction and startup, not Explorer UI or native-window usability.

The packager checks a conservative **240-unit** verification-path budget before creating output. Maximum recommended extraction-directory lengths are 142 units for Windows-only and 113 for combined, excluding the trailing separator. These are conservative packaging budgets, not guarantees for arbitrary extraction tools or destinations.

Mac static inspection found six native files, each with x86_64 and arm64 slices. All twelve slices passed supported embedded code-page-hash and dependency checks. No Developer ID CMS signature is present. `Info.plist` identifies `org.dtet.binaryexplorer`, executable `DT-ET Binary Explorer` and macOS 12.0 minimum. This does not replace Apple's codesign, Gatekeeper, Metal or runtime tests.

## Reproduce the builds and packages

Build with matching installed platform modules and a valid Unity licence:

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

Outputs are `BinaryBlackHole/Builds/DesktopWindows` and `BinaryBlackHole/Builds/DesktopMac/DTET_Binary_Explorer.app`. Releases use Mono, BuildOptions.None and DTET_DESKTOP_RELEASE; the Mac builder requests x64arm64. Review and restore only incidental editor settings after building.

```powershell
.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -WindowsOnly `
  -SourceRevision 9092276b7e6f8128a99e315d3388644810b6ba99 `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Merge -PackageName DTET-Win

.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision 9092276b7e6f8128a99e315d3388644810b6ba99 `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Merge `
  -PackageName DTET-All -MacSigningStatus ad-hoc
```

For QA use BuildDesktopWindowsQaPlayer, copy its complete output to an isolated directory and run with graphics enabled. DTET_INTERACTIVE_QA_PATH selects the observatory harness; DTET_FORCE_HALF_TEXTURES_QA=1 requests the supported Half path. DTET_VISUAL_MOVIE=1 adds the 120-frame merger sequence from -30 to +70 tg. DTET_DISCOVERY_MOVIE=1 adds the 120-frame map sequence and star comparison captures; DTET_OUTGOING_WAVE_MOVIE=1 adds the separate 180-frame stellar-wave sequence. DTET_WAVE_PREVIEW_ONLY=1 writes a clearly labelled partial preview receipt and must not be counted as a full integration run. DTET_GENERATED_FLUID_QA_PATH selects the fluid harness. Remove overrides between runs. These handlers and switches are absent from release assemblies.

The current final observatory runs use the harness's windowed-resolution warmup for automatic camera checks. All technical QA remains separate from laptop, headset, continuous-playback and human usability validation. The scientific display and gas limitations are described in [the validation receipt](SMOOTH_MERGER_VALIDATION.md).

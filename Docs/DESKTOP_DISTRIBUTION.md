# Desktop distribution

Current runtime source: `89b9d82107ba753e86f014d1260a55f12891a213`, built with Unity **6000.3.25f1** on 24 October 2026. Both applications open the fidelity selector directly and do not require Unity Editor. This release adds WAVE MAP and FIND SUN-LIKE STAR, retaining visible accretion recovery and the outgoing stellar-light display.

| ZIP | Contents and layout | Bytes | Files | Longest member |
| --- | --- | ---: | ---: | ---: |
| `DTET-Win.zip` | Windows x64 application at the extraction root | 39,136,100 | 170 | 97 UTF-16 units |
| `DTET-All.zip` | `Windows/` and `macOS/`; universal Intel/Apple Silicon Mac app | 86,815,887 | 339 | 126 UTF-16 units |

SHA-256:

- Windows: `ae74326acaaac2b2eb6c444e07358452e94812cc76b358d91895c301f55ab9e5`
- Combined: `2bc92ed3bb9c976679c00c7074a2766fb3b775caebcc8824467c7b3985ee6eff`

Local delivery directory: `C:/Users/vince/Downloads/DTET-Views`. The ready-to-run Windows application is `Play/DTET_Binary_Explorer.exe`. Mac uses `macOS/DTET_Binary_Explorer.app` from the combined archive and requires macOS 12.0 or later. It is ad-hoc signed, unnotarized and has not been run on a Mac.

## Launch and extraction

Extract the entire ZIP to a short destination, such as `C:\DTET`, then open `DTET_Binary_Explorer.exe`. Keep all neighbouring data/engine files; do not skip or rename DLLs. The Windows ZIP is flat and the combined ZIP has no repeated outer directory. If Explorer reports **0x80010135 / Path too long**, cancel and choose a shorter destination. Mac users should keep the app bundle intact.

Start with Laptop on modest hardware and choose ENTER OBSERVATORY for gas. The top-right WAVE MAP button opens a coloured strain diagram; FIND SUN-LIKE STAR centers and labels the fixed hypothetical source. BENDING compares original and distorted stellar light. ACCRETION VIEW or RESTART restores visible illustrative disks and close framing. OUTGOING WAVES uses a slow, wider view near merger to expose warped background light while keeping the disks visible. Space pauses, comma reverses, WASD/QE move and right-drag looks. V opens the wrist instrument. STARLIGHT ONLY deliberately hides gas. See [full controls](INTERACTIVE_OBSERVATORY.md).

The archive contains only the chosen applications, necessary runtime files, instructions and notices. Source/scripts, tests, benchmarks, audits, logs, caches, debug symbols, captures and preview video are excluded. Required Unity runtime DLLs retain their original names. Package manifests and verification reports remain beside the archives, outside them.

## Verification

The QA build and both release builds succeeded. Final Windows Float/Half observatory runs passed **747 non-capture assertions**, **288 capture-write checks** and produced **296 images**. Discovery views were inspected in actual captures; a four-second preview uses 120 rendered frames. Full details and boundaries are in [DISCOVERY_VIEWS_VALIDATION.md](DISCOVERY_VIEWS_VALIDATION.md). No physics formula or fluid solver changed; the portable suite and fluid integration were not rerun for this view update, and their earlier results remain in [OUTGOING_WAVES_VALIDATION.md](OUTGOING_WAVES_VALIDATION.md).

All five executable shader-fixture variants are stripped from releases. Nine QA/capture classes, two QA-only APIs and 21 environment switches are absent from each release assembly. Both new production view classes remain. Harmless shader property metadata remains. Comments in project code were shortened while preserving important assumptions, units and legal notices.

An independent **45-check** package audit confirms CRC, safe paths, complete manifests, correct build bytes, notices, controls, source revision and Mac modes. All **166 Windows and 169 Mac runtime files** match the current build outputs; six Mac native files retain Unix 0755. Assembly exclusions are byte-presence checks, with build-log evidence for shader stripping. This bundle contains no symlinks. The packager preserves future symlinks and reports any Windows extraction restriction rather than flattening them.

Windows PowerShell **5.1.19041.7802 Expand-Archive** extracted all **170** Windows files to `C:/Users/vince/Downloads/DTET-Views/Play`; every size and SHA-256 matched. The longest absolute file path was **138 UTF-16 units**. The extracted executable remained running for a 15-second hidden startup check, logged its desktop installation marker and produced no matched runtime error. This verifies standard PowerShell extraction and startup, not Explorer UI or native-window usability.

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
  -SourceRevision 89b9d82107ba753e86f014d1260a55f12891a213 `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Views -PackageName DTET-Win

.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision 89b9d82107ba753e86f014d1260a55f12891a213 `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Views `
  -PackageName DTET-All -MacSigningStatus ad-hoc
```

For QA use BuildDesktopWindowsQaPlayer, copy its complete output to an isolated directory and run with graphics enabled. DTET_INTERACTIVE_QA_PATH selects the observatory harness; DTET_FORCE_HALF_TEXTURES_QA=1 requests the supported Half path. DTET_DISCOVERY_MOVIE=1 adds the 120-frame map sequence and star comparison captures; DTET_OUTGOING_WAVE_MOVIE=1 adds the separate 180-frame stellar-wave sequence. DTET_WAVE_PREVIEW_ONLY=1 writes a clearly labelled partial preview receipt and must not be counted as a full integration run. DTET_GENERATED_FLUID_QA_PATH selects the fluid harness. Remove overrides between runs. These handlers and switches are absent from release assemblies.

The current final observatory runs use the harness's windowed-resolution warmup for automatic camera checks. All technical QA remains separate from laptop, headset, continuous-playback and human usability validation. The scientific display and gas limitations are described in [the validation receipt](DISCOVERY_VIEWS_VALIDATION.md).

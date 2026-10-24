# Desktop distribution

Runtime source: `7a69524804d57e8cb2ecacb01c263e53b788ed4d`, built with Unity **6000.3.25f1** on 24 October 2026. Both applications open the fidelity selector and run without Unity Editor. The default streams evolved mini-disk fluid with approximate curved gas optics. It has no prescribed feeding tubes. Pressure, gravity, shocks and sinks change a finite, explicitly assumed gas inventory during late inspiral and merger.

| ZIP | Contents and layout | Bytes | Files | Longest member |
| --- | --- | ---: | ---: | ---: |
| `DTET-Win.zip` | Windows x64 application at the extraction root | 104,175,393 | 171 | 97 UTF-16 units |
| `DTET-All.zip` | `Windows/` and `macOS/`; universal Intel/Apple Silicon Mac app | 216,900,151 | 341 | 126 UTF-16 units |

SHA-256:

- Windows: `35ec65c569297c30ac84bf1be9d6c541a5a17d7193f457816b29ad49c8d13bd8`
- Combined: `46ce81ef687921d07df334fcfab197f63c8200175b77464e692a7073bdce5b84`

Local delivery directory: `C:/Users/vince/Downloads/DTET-Flow`. The extracted Windows application is `Play/DTET_Binary_Explorer.exe`. Mac uses `macOS/DTET_Binary_Explorer.app` from the combined archive and requires macOS 12.0 or later. It is ad-hoc signed, unnotarized and has not been run on a Mac. The separate `DTET-Flow-preview.mp4` contains 240 actual rendered frames at 1280x720/24 fps, covering the full recorded gas interval in ten seconds.

## Launch and controls

Extract the entire ZIP to a short destination, such as `C:\DTET`, then open `DTET_Binary_Explorer.exe`. Keep all neighbouring data/engine files; do not skip or rename DLLs. The Windows ZIP is flat and the combined ZIP has no repeated outer directory. If Explorer reports **0x80010135 / Path too long**, cancel and choose a shorter destination. Keep the Mac app bundle intact.

Choose a fidelity preset and ENTER OBSERVATORY. The reference replay starts at the prepared gas initialization epoch, 1000 gravitational times before common-horizon formation, and plays at 0.02 source seconds per real second. All presets use the same 192x192 fluid history and 513 recorded frames; image resolution and curved-ray budgets differ. Earlier source history remains seekable and explicitly has no gas before initialization. Changing masses or separation prepares matching fluid in the background while playback is paused; an actual 40/25-solar-mass experiment took about 6.5 minutes on this machine. The prepared reference replay needs no interactive solve.

ACCRETION VIEW restores the gas start and close framing. Space pauses, comma reverses, WASD/QE move and right-drag looks. V opens the wrist instrument. WAVE MAP shows an exaggerated strain surface; FIND SUN-LIKE STAR centers the hypothetical source. BENDING compares light mapping. OUTGOING WAVES selects a slow, wider view near merger. STARLIGHT ONLY hides gas. See [full controls](INTERACTIVE_OBSERVATORY.md).

The ZIPs contain only the applications, necessary runtime files, prepared gas data, instructions and notices. Source scripts, tests, benchmarks, audits, Unity import caches, logs, debug symbols, captures and preview video are excluded. The 65,024,677-byte `sxs0305.mdf` is required simulation playback data, not a disposable Unity cache. Manifests and verification reports remain outside the archives.

## Verification and boundaries

QA, Windows release and universal Mac builds passed. Final Float and Half GPU runs each passed **43 integration checks**, including immutable rewind, gas coverage, source-state preservation, free viewpoints, actual velocity-dependent Doppler brightness, curved far-side emission, a single-hole optical control, cancellation and resource cleanup. Each recorded 240 scene frames. All three image presets preserved the same decoded physical field. The portable mini-disk checks passed 28 assertions; the broader portable physics suite passed. Legacy observatory and optional reservoir regression runs also passed, with their separate scope retained. [MINIDISK_VALIDATION.md](MINIDISK_VALIDATION.md) records exact receipts and limitations.

Independent archive inspection passed **50 checks** covering CRC, safe paths, complete manifests, exact build bytes, notices, controls, source revision, shader stripping and the complete bundled fluid stream. Ten QA/capture classes, four QA APIs and all 23 discovered QA environment switches were absent from both release assemblies by byte-presence checks; five executable shader-fixture variants were stripped. All eighteen production shaders passed the source/build preflight. No audit or benchmarking source is shipped.

Windows PowerShell **5.1.19041.7802 Expand-Archive** extracted all **171** Windows files to `C:/Users/vince/Downloads/DTET-Flow/Play`; every size and SHA-256 matched. The longest absolute path was **137 UTF-16 units**. The extracted executable remained running for a 15-second hidden startup check, logged its installation marker and produced no matched runtime error. This verifies PowerShell extraction and startup, not Explorer UI or native-window usability.

The packager checks a conservative **240-unit** verification-path budget. Maximum recommended extraction-directory lengths are 142 units for Windows-only and 113 for combined, excluding the trailing separator. These budgets do not guarantee arbitrary extraction tools or destinations. The current bundle has no symbolic links; the packager preserves their metadata when present.

Mac inspection found six native files, each with x86_64 and arm64 slices. All twelve slices passed supported embedded code-page-hash and dependency checks; all six files retain Unix 0755 in the ZIP. No Developer ID CMS signature is present. `Info.plist` identifies `org.dtet.binaryexplorer`, executable `DT-ET Binary Explorer` and macOS 12.0 minimum. This does not replace Apple's codesign, Gatekeeper, Metal or runtime tests.

The gas is a conditional 2D Newtonian/isothermal approximation. Detailed morphology is not resolution-converged, and the surrogate develops superluminal speeds near merger; the rendering speed cap does not repair that physical limitation. Curved binary optics and the joining horizon surface are approximate. Bright gas is not a measured counterpart of GW150914. See [model](MINIDISK_DYNAMICS.md) and [numerical evidence](MINIDISK_NUMERICS.md). Sampled images were inspected; laptop/headset performance, continuous native playback, Mac runtime and human usability are unverified.

## Reproduce

With matching platform modules and a valid Unity licence:

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

Outputs are `BinaryBlackHole/Builds/DesktopWindows` and `BinaryBlackHole/Builds/DesktopMac/DTET_Binary_Explorer.app`. Releases use Mono, BuildOptions.None and DTET_DESKTOP_RELEASE; Mac requests x64arm64. Review and restore only incidental editor settings after building.

```powershell
.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -WindowsOnly `
  -SourceRevision 7a69524804d57e8cb2ecacb01c263e53b788ed4d `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Flow -PackageName DTET-Win

.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision 7a69524804d57e8cb2ecacb01c263e53b788ed4d `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Flow `
  -PackageName DTET-All -MacSigningStatus ad-hoc
```

For QA use BuildDesktopWindowsQaPlayer and an isolated copy of its complete output with graphics enabled. `DTET_MINIDISK_QA_PATH` selects the current harness; `DTET_FORCE_HALF_TEXTURES_QA=1` requests Half textures. The harness records its 240-frame sequence and optical controls. Optional `DTET_MINIDISK_EXPERIMENT_QA=1` adds a complete custom-parameter solve. Remove overrides between runs. QA handlers and switches are absent from release assemblies.

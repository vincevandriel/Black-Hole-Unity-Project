# Desktop distribution

Runtime source: `44d45529098865e71f379207086749904eb68b0e`, built with Unity **6000.3.25f1** on 24 October 2026. Both applications open the fidelity selector and run without Unity Editor. This release adds material detail transported through the evolved mini-disk flow, a local vertical-thickness approximation, revised volumetric emission and optional automatic merger slow motion. [Plasma appearance](PLASMA_APPEARANCE.md) records the implementation and its limits.

| ZIP | Contents and layout | Bytes | Files | Longest member |
| --- | --- | ---: | ---: | ---: |
| `DTET-Win.zip` | Windows x64 application at the extraction root | 110,010,491 | 172 | 97 UTF-16 units |
| `DTET-All.zip` | `Windows/` and `macOS/`; universal Intel/Apple Silicon Mac app | 228,573,214 | 343 | 126 UTF-16 units |

SHA-256:

- Windows: `6450ebaa50900bb2178569a25b6606716f044bf442436df3f3dd6f73d66a0d29`
- Combined: `636ad65328bcb581b877ea53b50766dd515954bf4f9f7143cf8b7afa0ebbbb55`

Local delivery directory: `C:/Users/vince/Downloads/DTET-Plasma`. The extracted Windows launcher is `Play/DTET_Binary_Explorer.exe`. The combined archive contains `macOS/DTET_Binary_Explorer.app`, requiring macOS 12.0 or later. Mac is ad-hoc signed, unnotarized and has not been run on a Mac. The separate `DTET-Plasma-preview.mp4` contains 240 actual rendered frames at 1280x720/24 fps, sampling the recorded gas interval in ten seconds using the monotone display-time map. It is not a real-time performance recording.

## Launch and controls

Extract the complete ZIP to a short destination such as `C:\DTET`, then open `DTET_Binary_Explorer.exe`. Keep all neighbouring data/engine files. If Explorer reports **0x80010135 / Path too long**, cancel and use a shorter destination; do not skip or rename DLLs. Windows-only is flat and combined has no repeated outer directory. Keep the Mac app bundle intact.

Choose a fidelity preset and ENTER OBSERVATORY. The reference starts at the prepared gas initialization epoch, 1000 gravitational times before common horizon. Its base rate is 0.02 source seconds per real second, with automatic slow motion around merger. **MERGER SLOW MOTION** toggles that map; selecting a manual wrist speed disables it. The footer reports AUTO/MANUAL and effective rate. All readouts use the same scientific source epoch.

All presets use the same 192x192 fluid/material history and 513 epochs; image resolution and ray budgets differ. Start with Laptop on modest hardware. Earlier source history remains seekable with explicit missing gas coverage. Adjusting masses/separation pauses playback while preparing matching gas and transported material. It can take several minutes; the prepared reference needs no interactive solve.

ACCRETION VIEW restores the gas start and close framing. WATCH MERGER plays from shortly before coalescence. Space pauses, comma reverses, WASD/QE move and right-drag looks. V opens the wrist. WAVE MAP shows an exaggerated strain surface; FIND SUN-LIKE STAR centers the hypothetical source; BENDING compares the light mapping. OUTGOING WAVES selects a wider slow view near merger. STARLIGHT ONLY hides gas. See [full controls](INTERACTIVE_OBSERVATORY.md).

## Clean payload

The ZIPs contain only applications, necessary runtime/data files, instructions and notices. Source scripts, tests, benchmarks, audits, Unity import caches, logs, debug symbols, captures and preview video are excluded. Prepared `sxs0305.mdf` (65,024,677 bytes) and `sxs0305.mdt` (5,863,440 bytes) are required playback data. Only two epochs are resident in the rendering textures; the material scalar uses their existing alpha channel. Playback payload is 2,927,820 bytes with Float textures and 2,337,996 with Half textures, excluding the rest of Unity, scene targets and process memory. Manifests and audit reports remain outside the ZIPs.

## Verification

QA, Windows and universal Mac builds passed. Final Float passed **84 integration checks**, including a completed 40/25-solar-mass, 35rg custom fluid/material generation and return to the reference. Half passed **77 checks**. Both recorded 240 frames. Checks cover original scientific-state preservation, material off/on/restoration, pause/rewind, manual/automatic timing, malformed-material fallback, cleanup, all three profile identities, actual-shader Doppler/curvature controls and 18 Gaussian-column transfer fixtures. The custom solve took 399.6 seconds while release builds overlapped part of it; this is functional completion evidence, not a generation benchmark.

The portable physics suite, frame-independent time-map fixtures and 166 transport/cache assertions passed. Independent decoders checked every one of the 513 fluid and material frames, their hashes, timeline binding, CRCs, finite bounds and stored-cell neutral masks. See [appearance evidence](PLASMA_APPEARANCE.md). Earlier mini-disk, observatory and reservoir receipts remain historical evidence for their own revisions.

The independent package audit passed **57 checks**. Both custom managed assemblies were inspected for relevant production symbols and test/editor/QA exclusions. Ten QA/capture classes, four QA APIs and all 23 discovered QA environment switches were absent by byte-presence checks. Five executable shader-fixture variants were stripped; all eighteen production shaders passed preflight, as did both prepared-data gates. Byte-symbol checks are not complete decompilation or reproducible-build proof.

Windows PowerShell **5.1.19041.7802 Expand-Archive** extracted and hash-compared all **172** files to `DTET-Plasma/Play`; longest absolute path **139 UTF-16 units**. The extracted application remained running for a 15-second hidden startup check with its installation marker and no matched runtime errors. This tests PowerShell extraction/startup, not Explorer UI or native-window usability.

The packager enforces a conservative **240-unit** verification-path budget. Recommended maximum extraction-directory lengths are 142 units for Windows and 113 for combined, excluding the trailing separator. These do not guarantee arbitrary tools/destinations. There are no symbolic links in this payload.

Mac inspection found six native files, each with x86_64 and arm64 slices. All twelve slices passed supported embedded code-page-hash/dependency checks and all six native files retain Unix 0755 in the ZIP. No Developer ID CMS signature is present. `Info.plist` identifies `org.dtet.binaryexplorer` and macOS 12.0 minimum. This does not replace codesign, Gatekeeper, Metal or Mac runtime tests.

## Physical and visual limits

The bright gas is conditional, two-dimensional, Newtonian and isothermal. Its detailed flow and passive material patterns are not converged, and some near-merger velocities exceed the model's physical validity; clipping rendering velocity does not repair this. Thickness, colour and emission structure are assumptions. The binary optics and joining horizon shape remain approximate. This is not a predicted image of GW150914 or a GRMHD simulation.

Reviewed frames show finer moving contrast, view-dependent curved emission and a depleted remnant. Planar-label extrusion can leave vertical bands, and narrow bright rims still alias at low resolution. Laptop/headset performance, continuous native playback and human usability are unverified. The High preset is substantially more expensive than Balanced on the tested desktop; small completed-readback samples are recorded in the appearance note, not presented as sustained FPS.

## Reproduce

Use Unity with the Windows and macOS modules and a valid licence. Build methods in `DTET.VisualPrototype.Editor.PrototypeBuild` are `BuildDesktopWindowsPlayer` and `BuildDesktopMacPlayer`; targets are `StandaloneWindows64` and `StandaloneOSX`. Pass the `BinaryBlackHole` project with `-batchmode -nographics -quit -executeMethod` and a log path. Outputs are `Builds/DesktopWindows` and `Builds/DesktopMac/DTET_Binary_Explorer.app`. Releases use Mono, BuildOptions.None and DTET_DESKTOP_RELEASE; Mac requests x64arm64. Restore only incidental editor settings after building.

```powershell
.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -WindowsOnly `
  -SourceRevision 44d45529098865e71f379207086749904eb68b0e `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Plasma -PackageName DTET-Win

.\Scripts\Package-DesktopPlayers.ps1 `
  -WindowsBuildDirectory <DesktopWindows> -MacAppPath <DTET_Binary_Explorer.app> `
  -SourceRevision 44d45529098865e71f379207086749904eb68b0e `
  -OutputDirectory C:\Users\vince\Downloads\DTET-Plasma `
  -PackageName DTET-All -MacSigningStatus ad-hoc
```

For QA use `BuildDesktopWindowsQaPlayer` and an isolated copy of its complete output with graphics enabled. `DTET_MINIDISK_QA_PATH` selects the harness; `DTET_FORCE_HALF_TEXTURES_QA=1` requests Half textures. `DTET_MINIDISK_EXPERIMENT_QA=1` adds the complete custom fluid/material solve. Remove overrides between runs. QA code and switches are excluded from release players.

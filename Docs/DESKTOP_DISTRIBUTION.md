# Desktop distribution

Runtime source: `38696ecb5684e651717b061203f601870fbd839f`, built with Unity **6000.3.25f1** on 24 October 2026. The observatory now includes the original ApproachHUD asset, five guided exploration stops, gaze-triggered opening/reverse closing, proximity-based inspection cards and optional replaceable offline narration. [Guided observations](GUIDED_OBSERVATIONS.md) describes the interaction; [narration instructions](GUIDED_TOUR_NARRATION.md) identify the editable scripts and recordings.

| ZIP | Contents and layout | Bytes | Files | Longest member |
| --- | --- | ---: | ---: | ---: |
| `DTET-Win.zip` | Windows x64 application at extraction root | 121,629,292 | 173 | 97 UTF-16 units |
| `DTET-All.zip` | `Windows/` and `macOS/`; universal Intel/Apple Silicon Mac app | 251,810,626 | 345 | 126 UTF-16 units |

SHA-256:

- Windows: `5b62144de9e3c285cb9c626654011a131346b48a3784e144cb63339af5f8307e`
- Combined: `31c409ab56a8f0b6c3cb3346f206a791b2b6c1a0ea685d358d7a9f0ae79550c7`

Local delivery: `C:/Users/vince/Downloads/DTET-Guide`. The extracted Windows launcher is `Play/DTET_Binary_Explorer.exe`. The combined ZIP contains `macOS/DTET_Binary_Explorer.app`, requiring macOS 12.0 or later. Mac is ad-hoc signed, unnotarized and has not been run on a Mac.

## Launch and exploration

Extract the complete ZIP to a short folder such as `C:\DTET`, then open `DTET_Binary_Explorer.exe`. Keep the neighboring data and engine files. Do not skip or rename DLLs if Explorer reports 0x80010135 / Path too long; choose a shorter destination. The Windows-only package is flat, and the combined package has no repeated outer directory.

Choose a fidelity preset and ENTER OBSERVATORY. Guided stops begin at the prepared gas initialization epoch. They cover the intact binary, a distinct inspiral and tidal-gas view, final approach, common-horizon formation and the remnant. Each stop waits for the user. The initial scene tags the system, each black hole, gas and outgoing waves. Select a name to face its location, or look around freely. Approach its animated square within 18 displayed gravitational radii and press F or click it to read. PLAY VOICE is optional. Looking away reverses the opening animation and stops narration. The scripts and fifteen bundled clips can be replaced without changing interaction code.

CONTINUE or Space advances to the next stop. GUIDED STOPS: OFF enables uninterrupted playback. RESTART rearms stops; seeks and rewinds rearm future checkpoints. WASD/QE move, right-drag looks, the wheel approaches, 1–5 select viewpoints and V raises the wrist. All bodies, gas and graphs retain one scientific source epoch while the camera and HUD can move independently during pauses. Existing automatic merger slow motion, the star finder, wave map, optical controls and adjustable masses remain available.

The same prepared 192×192 gas/material history and 513 epochs serve all render presets. Only two epochs are streamed into the render textures. Matching adjustable gas is prepared in the background; earlier uncovered epochs show no invented gas. [Plasma appearance](PLASMA_APPEARANCE.md) and [mini-disk dynamics](MINIDISK_DYNAMICS.md) retain the model details.

## Verification and payload

- The pure guided state suite passed 107 assertions. The actual Windows development player passed 52 guided checks and 77 gas/rendering regression checks; the latter captured 240 rendered sequence frames. Reviewed images cover all five stops and an open narrated card. They are controlled integration checks, not a human usability study or native mouse/headset certification.
- Both release builds passed preflight for nineteen production shaders, ten content topics, fifteen narration resources and the exact fluid/material pair. Managed assembly inspection confirms the guided/HUD runtime types and excludes the two new QA helpers along with the existing test/editor types and QA environment switches. Independent ZIP/source/build comparisons and all-frame fluid/material decoding passed.
- Windows PowerShell 5.1.19041.7802 extracted and size/hash-compared all 173 files. The longest absolute extracted path was 138 UTF-16 units. The extracted application remained running through the 15-second hidden startup check with the expected observatory marker and no matched application errors.
- Six Mac native files retain executable metadata. Their Intel/ARM slices passed supported static dependency and code-page-hash checks with no reported errors. This does not establish Gatekeeper trust, notarization, Metal rendering or actual Mac playback.
- ZIPs contain only applications, required data/audio, launch instructions and notices. They exclude source/test/benchmark/audit scripts, debug symbols, Unity caches, captures, previews and logs. Detailed receipts remain outside the archives in `outputs/Guided-tour-validation` in the task workspace.

## Scientific bounds

GW150914 was observed by LIGO. SXS0305 is representative; no Einstein Telescope event readings or measured bright disk are claimed. The gas is an assumed finite, isothermal Newtonian environment with no magnetic field or energy equation; detailed flow is not converged and some near-merger speeds exceed model validity. Vertical structure, material emission, binary optics, wave-light distortion and the smooth joining capture surface are approximate. Adjustable merger/ringdown cards explicitly identify the model cutoff. This is an educational visualization, not GRMHD or a predicted merger image. Laptop/headset performance, sustained native playback and human voice intelligibility remain separately unverified.

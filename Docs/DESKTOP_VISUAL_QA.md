# Desktop visual prototype verification

Verified on Windows with Unity Editor **6000.3.25f1 (Unity 6.3 LTS)** and an assigned Unity Personal license. The current scene is visual staging; it does **not** use the separately tested physical inspiral model to drive positions or graph values.

## Run

1. Add `BinaryBlackHole` in Unity Hub and open it with Editor 6000.3.25f1.
2. Open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity` and press Play.
3. In the observation view, right-drag to orbit, wheel to zoom, WASD to move, Q/E to change height, Shift to move faster, and middle-drag to pan. C toggles free flight; right-drag then looks around. F or Home returns to the overview. Above / Side and Focus A / B provide alternate or tracked views. Pause and seek are available in the compact toolbar.
4. H opens the learning panels. Try Story / Graph / Model, A / B, the initial-separation slider followed by **Apply Start**, Pause / Play, Reset, story seek, visual playback rate, the quick check, and Dim / Normal. Space toggles playback, Esc pauses, R resets, B toggles dimming, and 1/2/3 open the respective content layer. H returns to observation without resetting the storyboard or camera.
5. For a Windows development player, use **DT-ET > Validate Assets and Build QA Player**. Output is `BinaryBlackHole/Builds/VisualQA/BinaryBlackHole_VisualQA.exe`. This menu validates the scene and all four custom shaders before building.

## Completed checks

- Editor batch import and compilation returned code 0.
- Windows development-player build succeeded and custom-shader validation passed.
- The original visual milestone passed Story, Graph, Model, Endpoint, and ScenarioB receipts. The observation update adds Observe, Above, Side, and Close receipts at 1600×900, plus a refreshed Story receipt. Screenshots were inspected for view differences, close-range detail, optional learning layout, and clipping.
- Automated UI-command checks invoked the button callbacks for all three layers, both scenarios, Apply Start, Reset, the correct-answer feedback, and dim/normal. They also checked that a changed starting separation remains pending until applied and that Apply Start resets seek.
- Camera-command checks exercise preset recovery, perspective projection, focus selection, zoom bounds, extreme orbit input, free-flight movement, and learning viewport changes without modifying storyboard state.
- Input System checks inject temporary mouse/keyboard devices in development QA: right-drag changes angle, wheel zoom works over the scene and is suppressed over UI, C switches to flight, W/E/Shift moves the observer, F returns home, and H opens/closes learning panels. A seek change verifies the focused camera follows the moving body with its view offset retained. These devices and the screenshot harness run only when `DTET_CAPTURE_PATH` is set, and are excluded from release builds.
- `dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release` passed. This tests the staging model and tutor snapshot boundary, not Unity-model integration.

## Remaining checks and limits

- The Editor's native Play-window interaction and manual mouse/keyboard feel were not observed directly. The Windows capture helper failed twice (`FrameArrived timed out`, then `window capture timed out`); automated player Input System and rendering checks were used. Human usability and headset comfort remain to be tested.
- On batch import, Unity 6.3/URP 17.3 logs `Host type is not matching any asset type` for `TraceRenderingLayerMask.urtshader` while ensuring URP global settings. The import exits 0, shaders validate, and the player renders; the Editor diagnostic remains unresolved.
- A transient licensing message says an access token was unavailable during Editor startup; `unity license status --format json` independently reports `active: true`, signed in, Unity Personal assigned, and builds complete.
- No physical inspiral, scientific graph coupling, functional AI tutor, XR interaction, headset build, or measured learning outcome is claimed for this visual milestone.

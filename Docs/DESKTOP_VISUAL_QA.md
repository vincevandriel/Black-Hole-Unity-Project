# Desktop visual prototype verification

Verified on Windows with Unity Editor **6000.3.25f1 (Unity 6.3 LTS)** and an assigned Unity Personal license. The current scene is visual staging; it does **not** use the separately tested physical inspiral model to drive positions or graph values.

## Run

1. Add `BinaryBlackHole` in Unity Hub and open it with Editor 6000.3.25f1.
2. Open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity` and press Play.
3. Try Story / Graph / Model, A / B, the initial-separation slider followed by **Apply Start**, Pause / Play, Reset, story seek, visual playback rate, the quick check, and Dim / Normal. Space toggles playback, Esc pauses, R resets, B toggles dimming, and 1/2/3 choose the content layer.
4. For a Windows development player, use **DT-ET > Validate Assets and Build QA Player**. Output is `BinaryBlackHole/Builds/VisualQA/BinaryBlackHole_VisualQA.exe`. This menu validates the scene and all four custom shaders before building.

## Completed checks

- Editor batch import and compilation returned code 0.
- Windows development-player build succeeded and custom-shader validation passed.
- The development player exited code 0 for `Story`, `Graph`, `Model`, `Endpoint`, and `ScenarioB` visual receipts at 1600×900. Each run logged `DTET_UI_COMMANDS_PASS` and no player exception. Screenshots were inspected for clipping, visible disks, graph curve, labels, and the conceptual endpoint.
- Automated UI-command checks invoked the button callbacks for all three layers, both scenarios, Apply Start, Reset, the correct-answer feedback, and dim/normal. They also checked that a changed starting separation remains pending until applied and that Apply Start resets seek.
- `dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release` passed. This tests the staging model and tutor snapshot boundary, not Unity-model integration.

## Remaining checks and limits

- The Editor's native Play-window interaction and manual mouse/keyboard feel were not observed directly. The development player was run for rendered and automated callback QA, but that is not a substitute for a human usability test.
- On batch import, Unity 6.3/URP 17.3 logs `Host type is not matching any asset type` for `TraceRenderingLayerMask.urtshader` while ensuring URP global settings. The import exits 0, shaders validate, and the player renders; the Editor diagnostic remains unresolved.
- A transient licensing message says an access token was unavailable during Editor startup; `unity license status --format json` independently reports `active: true`, signed in, Unity Personal assigned, and builds complete.
- No physical inspiral, scientific graph coupling, functional AI tutor, XR interaction, headset build, or measured learning outcome is claimed for this visual milestone.

# Black-Hole-Unity-Project

Desktop-first DT-ET learning prototype for university students aged 18–30, across different prior knowledge and interest levels. The current Unity scene is a **visual staging prototype**, not a physics demonstration. A separate deterministic inspiral model and tests are preserved under `Staging/` for a later integration milestone.

## Open the visual prototype

1. Install Unity Hub and Unity Editor **6000.3.25f1 (Unity 6.3 LTS)**, then sign in and activate a valid Unity license.
2. In Unity Hub, add the project folder `BinaryBlackHole` (not the repository root). Let Package Manager import the Universal Render Pipeline dependencies.
3. Open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity` and press Play. The scene is also the sole enabled Build Settings scene.
4. The scene opens in a perspective observation view. Right-drag to orbit, use the mouse wheel or Zoom buttons to move closer/farther, and use WASD plus Q/E to move horizontally and vertically. Hold Shift to move faster. Middle-drag pans the view. **Orbit / Fly** (C) switches to free flight, where right-drag looks around and WASD moves in the viewing direction. **Overview** (F or Home) returns to the event; Above and Side provide alternate viewpoints, and Focus A / B follows a body at close range.
5. Open **Learning** (H or 1/2/3) for Story / Graph / Model, scenario A / B, bounded initial-separation preview, visual playback rate, and the quick check. H returns to observation. A slider change is pending until **Apply Start** is pressed; applying resets and pauses the visual storyboard. At the endpoint the two markers give way to a **conceptual** remnant marker. Camera navigation works while playback is paused and does not change the storyboard.

Keyboard shortcuts: Space toggles pause/play, Esc pauses immediately, R resets, B toggles a dim scene view, and 1/2/3 select Story/Graph/Model. The always-visible Exit App button closes a desktop player; in the Editor, use the Play toolbar to stop. The opening learning goal and one-question quick check support an unassisted explanation before revealing feedback. This check is formative content, not a measured learning outcome.

The scene uses the Input System for its UI and a runtime bootstrap to create procedural, editable-in-code visuals: paired dark silhouettes, layered animated annulus meshes with a shallow thickness visible from below/edge-on, soft inner and outer rims, fading motion trails, orbital reference rings/ticks, a surrounding single-mesh deterministic starfield, a chirp illustration with story-position cursor, and optional learning panels. Custom URP shaders are in `Assets/Resources/DTET/`; the geometry and material parameters are in `Assets/DTET/VisualAssetFactory.cs`. There is no imported third-party art. The luminous disks are an artistic visual aid, **not** predicted gas emission, ray tracing, or a binary-spacetime model. The graph is explicitly not live data, the controls are visual choreography rather than physical time, and the tutor badge points to an offline staging adapter rather than a functional AI tutor.

This is the desktop observation preview for the intended VR exhibit. The camera uses perspective and a separate navigation component; the current screen overlay and desktop locomotion still need to be replaced with tracked-headset input, readable world-space UI, and device-tested comfort controls when the headset and deployment target are selected. The Zoom controls move the observer rather than altering the bodies or their separation.

## Source layout

- `BinaryBlackHole/`: Unity 6.3 LTS Universal 3D project, derived from Unity's included `com.unity.template.3d-cross-platform-17.0.14` template.
- `BinaryBlackHole/Assets/DTET/VisualPrototypeBootstrap.cs`: interactive visual composition and storyboard controls.
- `BinaryBlackHole/Assets/DTET/ExhibitCameraRig.cs`: independent orbit/fly navigation, tracking, zoom limits, and recoverable viewpoints.
- `BinaryBlackHole/Assets/DTET/VisualAssetFactory.cs`: reproducible geometry/material construction; no external texture dependency.
- `BinaryBlackHole/Assets/Resources/DTET/`: four URP shaders for illustrated disks, dark silhouettes, star points, and glow lines. Kept in Resources so a player build can retain them without a serialized material reference.
- `Staging/Simulation/`: pure C# circular-inspiral, session, graph, and tutor-provider boundary, not yet bound into Unity.
- `Staging/Simulation.Tests/`: reproducible source tests; see `Staging/README.md`.

The partner's `InspiralDemo_Build.zip` was a Unity **6000.5.10f1 compiled Windows player**, with no `Assets`, `Packages`, `ProjectSettings`, or source scene. It informed the visual checklist but was not imported into this 6.3 project. Please provide the editable partner project if direct asset/scene integration is desired.

## Evidence and limitations

Unity 6.3.25f1 import is now licensed and the visual C# compiles. The template's Input System 1.12.0 failed with `BuildTarget.ReservedCFE`, so the project uses Input System 1.17.0; the Editor resolves built-in URP 17.3.0. Unused Visual Scripting, Plastic collaboration, navigation and Timeline template packages were removed after first-import errors. The source model tests pass but remain unbound to the scene. The QA build menu is `DT-ET > Validate Assets and Build QA Player`; it checks all four custom shaders and builds a Windows development player. Set `DTET_CAPTURE_PATH` and optionally `DTET_CAPTURE_VARIANT` (`Observe`, `Above`, `Side`, `Close`, `Story`, `Graph`, `Model`, `Endpoint`, or `ScenarioB`) before running that development player to execute automated UI/camera checks and write a screenshot. This harness is omitted from release builds. No XR headset/build target is needed for this desktop preview.

# Black-Hole-Unity-Project

Desktop-first DT-ET learning prototype for university students aged 18–30, across different prior knowledge and interest levels. The current Unity scene is a **visual staging prototype**, not a physics demonstration. A separate deterministic inspiral model and tests are preserved under `Staging/` for a later integration milestone.

## Open the visual prototype

1. Install Unity Hub and Unity Editor **6000.3.25f1 (Unity 6.3 LTS)**, then sign in and activate a valid Unity license.
2. In Unity Hub, add the project folder `BinaryBlackHole` (not the repository root). Let Package Manager import the Universal Render Pipeline dependencies.
3. Open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity` and press Play. The scene is also the sole enabled Build Settings scene.
4. Use Story / Graph / Model, scenario A / B, bounded initial-separation preview, visual playback rate, pause/play, reset, and storyboard seek. At the endpoint the two markers give way to a **conceptual** remnant marker.

The scene uses the Input System for its UI and a runtime bootstrap to create procedural, editable-in-code visuals: paired dark silhouettes, layered animated annulus meshes, soft inner and outer rims, fading motion trails, orbital reference rings/ticks, a single-mesh deterministic starfield, a chirp illustration with story-position cursor, and a desktop learning HUD. Custom URP shaders are in `Assets/Resources/DTET/`; the geometry and material parameters are in `Assets/DTET/VisualAssetFactory.cs`. There is no imported third-party art. The luminous disks are an artistic visual aid, **not** predicted gas emission, ray tracing, or a binary-spacetime model. The graph is explicitly not live data, the controls are visual choreography rather than physical time, and the tutor badge points to an offline staging adapter rather than a functional AI tutor.

## Source layout

- `BinaryBlackHole/`: Unity 6.3 LTS Universal 3D project, derived from Unity's included `com.unity.template.3d-cross-platform-17.0.14` template.
- `BinaryBlackHole/Assets/DTET/VisualPrototypeBootstrap.cs`: interactive visual composition and storyboard controls.
- `BinaryBlackHole/Assets/DTET/VisualAssetFactory.cs`: reproducible geometry/material construction; no external texture dependency.
- `BinaryBlackHole/Assets/Resources/DTET/`: three URP shaders for illustrated disks, star points, and glow lines. Kept in Resources so a player build can retain them without a serialized material reference.
- `Staging/Simulation/`: pure C# circular-inspiral, session, graph, and tutor-provider boundary, not yet bound into Unity.
- `Staging/Simulation.Tests/`: reproducible source tests; see `Staging/README.md`.

The partner's `InspiralDemo_Build.zip` was a Unity **6000.5.10f1 compiled Windows player**, with no `Assets`, `Packages`, `ProjectSettings`, or source scene. It informed the visual checklist but was not imported into this 6.3 project. Please provide the editable partner project if direct asset/scene integration is desired.

## Evidence and limitations

The visual script has been compiled against the Unity 6.3 editor's supplied assemblies. The source model tests pass. Unity Editor import, Play Mode, visual inspection, and desktop build remain **unverified** until a Unity license is active on this machine. No XR headset/build target is needed for this desktop milestone.

# Partner HUD reconstruction

**M1 update:** [the shared SI lesson](PHYSICS_M1.md) is now the default mode. Historical storyboard-only descriptions below apply to the separately retained artistic mode. Current physical readouts, graph and playback use one session; merger and clocks remain outside this milestone.

The supplied `InspiralDemo_Build.zip` contains a Windows player made with Unity 6000.5.10f1, not an editable Unity source project. Its scene serialization (`My project1_Data/level0`) and `Assembly-CSharp.dll` metadata were inspected read-only. No compiled partner assembly, newer-Editor project, or external art was imported. This implementation remains on **6000.3.25f1 / Unity 6.3 LTS**, URP 17.3.0, Input System 1.17.0, UGUI 2.0.0.

## What was carried across

| Verified partner feature | Current desktop implementation |
| --- | --- |
| `LessonHud`: separation, orbits per second, wave frequency, time left, honesty pill | Rounded readout card with explicit availability labels |
| `ChirpCard`: “Detector-like signal, not a picture”, scaled-height chip, time axis, played trace and cursor | Qualitative chirp waveform, played/unplayed colors, synchronized cursor |
| Tutor orb and narrative bubble | Local, progress-sensitive offline staging guide; optional Predict / Learn opens the existing formative check |
| `WristMenu`: circular progress, play/replay, speed button, stage chips | World-space wrist menu with progress ring, play/pause, replay, 0.5× / 1× / 2× cycle, Start / Halfway / End |
| Dark rounded cards, white translucent borders, teal accent and gold cautions | Procedural rounded UI using recovered colors: accent (0.37, 0.88, 0.83), caution (0.95, 0.70, 0.36) |

The partner scene used three world-space canvases and a wrist/desk placement script. The exhibit now uses **world-space canvases only**, including a control canvas parented to a visible virtual wrist. The earlier desktop dock was removed after the exhibit design was clarified. Keyboard/mouse emulate visitor/head and hand poses, not a different desktop layout. The original third chip was “Last orbits”; this adaptation uses **End** to make the conceptual endpoint inspectable. Prediction-panel references in the supplied scene were null; the current button opens the existing formative check. A six-stage lesson/unlock flow is not claimed here.

## Data and scope

`LessonHudSnapshot` / `ILessonHudProvider` form the presentation boundary. `VisualPrototypeBootstrap` supplies the current storyboard state and handles every HUD command. The HUD owns no second clock, orbital motion, or simulation. Learning sliders, observation seek, radial progress, narration, and stage selection all observe the same state.

Physics remains deferred per the current prototype scope:

- Separation is the **rendered separation as a percentage of its visual starting separation**, not kilometres. This follows the storyboard radius; it is independent of camera zoom.
- Orbits per second and wave frequency are nullable physical fields and display **Not connected**. They are not derived from decorative rotation or invented Hz values. A later model integration must supply these measurements and revise the presentation's staging labels together.
- “Playback left” is remaining wall-clock playback seconds at the chosen speed, not a physical coalescence time. Paused time is marked explicitly.
- The waveform is authored qualitative artwork with normalized height, not calculated detector strain, measured data, or a physically coupled chirp. Its cursor and played portion follow storyboard progress.
- The tutor is **local scripted narration**, explicitly labeled offline staging / not AI. It does not invoke an API, send data, or claim use of the separately staged AI provider.
- At 98% onward the remnant and narration explicitly identify a **conceptual** endpoint. No merger/ringdown physics is claimed.

## Use

Open `BinaryBlackHole/Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity` in Unity 6.3 LTS and press Play. The visitor starts in first-person view with the wrist lowered. **V** raises/lowers the wrist; left-click its controls. **U** hides/restores the partner interface. **T** approaches the spatial information panels; **H** opens and approaches the physical learning board. Wrist controls remain available beside the board; learning never shrinks the head viewport. **F/Home** returns to the exhibit. Right-drag emulates head look, WASD movement, Q/E height, wheel forward/back, and Shift faster movement. C selects optional orbital inspection.

Replay resets and pauses; stage chips seek and pause; Play at the endpoint starts again. Speed cycling synchronizes the learning rate slider. Space and Esc remain available with the wrist lowered. An entrance plaque explains emulation bindings; it is a physical scene object, not a screen overlay.

The raised wrist pose is captured relative to the visitor when raised. Later head-look rotation does **not** drag the wrist UI around the screen. Visitor translation carries the wrist immediately; raise/lower offsets are animated. Sleeve, glove, fingers, cuff, mount, and controls share a wrist pose. `VirtualWristRig.BindTrackedWrist(Transform)` accepts a later tracked pose and can hide the preview glove. That boundary is not a configured headset/controller implementation.

Code: `PartnerLessonHud.cs` (spatial cards and narration), `VirtualWristRig.cs` (hand pose/mount), `LessonHudSnapshot.cs` (provider contract), `HudSignalGraphic.cs` and `HudProgressGraphic.cs` (UI artwork), and `VisualPrototypeBootstrap.cs` (state and scene), under `BinaryBlackHole/Assets/DTET/`. `WristSurface.shader` gives the procedural glove/cuff shaded volume.

## Verification

The development harness checks shared playback, rate cycle/clamps, slider synchronization, missing physical values, playback time, endpoint narration, learning transitions, and visibility. World-input checks inject V and a real mouse click, verify world-space-only canvases, wrist parenting, gaze-independent pose, body-relative translation, and lowering. `Wrist`/`Hud` show the raised wrist; `Observe` shows it lowered; Story/Graph/Model approach the learning board. The harness only activates with `DTET_CAPTURE_PATH`; background/focus overrides are test-only. See `DESKTOP_VISUAL_QA.md` for verified receipts.

Manual headset readability, wrist placement, comfort, controller interaction, and the Editor Play-window feel are **not verified**. A selected headset/build target and device testing are required for that stage. See `DESKTOP_VISUAL_QA.md` for player checks and existing Editor diagnostics.

# Editable Unity project

Open this folder in Unity Hub with **Unity 6000.3.25f1**. This folder contains the editable `Assets`, `Packages` and `ProjectSettings`; the repository root is one level above the Unity project root.

1. Download the repository using **Code > Download ZIP**, then extract it to a short folder, or clone the repository.
2. In Unity Hub, choose **Add project from disk** and select this `BinaryBlackHole` folder.
3. Open with Unity 6000.3.25f1 and allow the package import to finish.
4. Open [Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity](Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity).
5. Select **DT-ET > Startup Exhibit > Interactive Suit Observatory**, then press Play.

## Edit the source

- [Assets/DTET](Assets/DTET/): C# presenters, camera, gas playback, rendering controls and interface.
- [Assets/DTET/Simulation](Assets/DTET/Simulation/): physics models, shared timeline, waveform and guided checkpoints.
- [InteractiveBinaryExhibit.cs](Assets/DTET/InteractiveBinaryExhibit.cs): current observatory controller.
- [GuidedObservationController.cs](Assets/DTET/GuidedObservationController.cs): gaze, inspection cards and optional audio.
- [Assets/DTET/ApproachHUD](Assets/DTET/ApproachHUD/): procedural square geometry and animation.
- [Assets/Resources](Assets/Resources/): shaders, event input, guided text and replaceable narration.
- [Assets/StreamingAssets](Assets/StreamingAssets/): prepared fluid and material histories required by the default replay.
- [Packages](Packages/) and [ProjectSettings](ProjectSettings/): dependency lock and Unity configuration.

Retain asset `.meta` files when copying or editing the project. Their identifiers preserve scene and resource references. Unity creates its import cache and IDE project files locally; those generated files are not required in the source download.

Build the current application with **DT-ET > Build Desktop Release > Windows** or **macOS Universal**. Install the corresponding build module in Unity Hub first. Outputs are under `Builds/DesktopWindows` and `Builds/DesktopMac`.

The separate `DTET-Win.zip` and `DTET-All.zip` distributions contain runnable applications. To edit the project, use the repository source described here. [Narration replacement](../Docs/GUIDED_TOUR_NARRATION.md), [guided exploration](../Docs/GUIDED_OBSERVATIONS.md) and [current distribution details](../Docs/DESKTOP_DISTRIBUTION.md) are documented alongside the source.

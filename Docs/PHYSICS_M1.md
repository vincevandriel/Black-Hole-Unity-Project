# M1: shared SI inspiral in Unity

[Validation receipt](PHYSICS_M1_VALIDATION.md): seven portable test groups, successful fresh-clone Windows build, 43 physical runtime checks and nine retained artistic regression groups.

The default exhibit mode now binds the existing circular quadrupole inspiral to the Unity bodies, source-time controls, spatial readouts and frequency graph. The original artistic storyboard is separately selectable. This is the first model-integration milestone; it is not the numerical GW150914 merger, gas model, binary metric or a calibrated detector waveform.

## Run and compare

Open `BinaryBlackHole` with Unity **6000.3.25f1**, open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity` and press Play. Physical mode starts paused. Space/wrist Play starts it; R resets. H approaches the learning board; T approaches the readouts; F returns to the exhibit. Camera movement remains available while paused.

On the learning board:

- **A:** original 30+30-solar-mass lesson, start 30 GM/c².
- **B:** original 30+30-solar-mass lesson, start 45 GM/c².
- **C:** 20+40-solar-mass teaching example, start 30 GM/c². The smaller mass has the larger barycentric orbit.
- Initial-distance slider + **Apply Start** creates a new paused scenario, preserving masses.
- The time slider and wrist Start/Halfway/End seek the same source-time session and pause it.
- Rate changes playback speed, not instantaneous physical frequency. **Reverse** or comma reverses the direction; press Play if paused. Reverse can start at the final endpoint. Magnitude is bounded to 0.1×–4×.
- **Physics** selects the physical lesson; **Storyboard** selects retained artwork. P switches modes. A mode change resets to its paused baseline.

The graph in physical mode is **frequency versus source time**, sampled from the same model instance as the bodies. It is not a strain waveform. Both spatial readouts and the board use the same time and model. Marker sizes are enlarged; their centre positions use one fixed, disclosed metres-per-display-unit mapping. Artistic annuli are hidden in physical mode. Approximate thin-lens stars remain an independently labelled exhibit feature.

## Shared source and scope

The canonical pure model is `BinaryBlackHole/Assets/DTET/Simulation/`, with the no-engine-reference `DTET.Simulation` assembly. The portable `Staging/Simulation/*.csproj` links that source; it contains no copied equation implementation. `PhysicalSceneSnapshot` captures an immutable model/state, scenario and context generation, source time, rate and render scale. The MonoBehaviour maps its SI coordinates into display coordinates; there is no Rigidbody orbit solver.

The original model constants are deliberately preserved: G=6.67430e−11 SI, c=299792458 m/s and solar mass=1.98847e30 kg. These differ by about 30 ppm in solar GM from the newer IAU nominal-GM reference. M1's figures and tests use its original convention consistently. A later event-data importer must implement the research conversion contract explicitly rather than mixing them.

At 10 GM/c² the circular approximation stops. Frequencies become unavailable. The separate endpoint/remnant marker is **conceptual**, and its presentation interval does not imply computed merger/ringdown dynamics. Physical source time clamps at the approximation boundary. No gas evolution, numerical horizon shape, observer proper time or full relativistic optics is claimed.

GW150914's measurement card should use GWTC-2.1-v4. This M1 lesson's A/B/C masses are teaching configurations, not those inferred medians or a numerical reconstruction. M2 will import the inspected SXS:BBH:0305 fixed configuration with the research's unit/frame/time contracts. Scientific bright gas needs compatible fluid/metric data; quality presets will sample the same underlying history.

## Reproduce validation

Portable numerical/command checks:

```powershell
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
```

The additional physical-binding group tests unit mapping, unequal-mass barycentre/orbit radii, immutable snapshots, rate independence of frequency, reverse replay, cutoff availability, custom-start mass preservation, invalid scales and 30/60/144-fps time/phase invariance. Its arbitrary-SI frequency fixture is independently evaluated. Existing analytic clock/lens/storyboard checks retain their own limited scope.

Use the Editor menu **DT-ET > Validate Assets and Build QA Player**, or batch execute `DTET.VisualPrototype.Editor.PrototypeBuild.BuildQaPlayer`. It creates a development player in `BinaryBlackHole/Builds/VisualQA/`. For an automated built-player physical integration run:

```powershell
./Scripts/Test-PhysicsMilestone.ps1 -OutputDirectory C:/temp/dtet-m1-physical
```

The opt-in QA exercises real rendered transforms against SI snapshots, slider and scenario callbacks, source-time advance, pause with camera movement, seek replay, unequal masses, custom distance, reverse and reverse-from-end, shared graph models, displayed Hz, hidden artistic gas and cutoff readouts. It writes a machine-readable receipt, player log and four screenshots. The normal app does not run the QA unless `DTET_PHYSICS_QA_PATH` is set. `VisualQaCapture` continues to audit the artistic mode and its prior world/wrist interaction controls when its own capture environment is set.

These are automated integration checks on the machine used. They do not establish human usability, all-laptop performance, headset tracking/comfort, expert scientific review or a clean interactive Editor Console. Keep raw build/player evidence outside Assets and Git; summarize actual results in the milestone receipt/PR.

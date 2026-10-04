# Desktop inspiral foundation (source staging)

The repository now includes a separate Unity 6.3 LTS **visual staging** project at `BinaryBlackHole/`. This portable source remains a foundation for the later physics milestone; it is **not yet bound to the Unity scene**. The visual choreography and chirp illustration must not be presented as calculations from this model.

## Model and controls

All physics inputs and outputs use SI units and `double`. `InspiralParameters` validates positive finite masses and separations and converts solar masses only at the boundary. `CircularInspiral` analytically evaluates the leading-order circular inspiral at any playback time. It ends at a configurable center-to-center separation. At and after cutoff, frequency values are `null` and the state is explicitly `ConceptualMerger`, then `ConceptualRemnant` after 1.5 **display** seconds. Those later states are labels/visual states, not predicted merger dynamics.

`SimulationSession` is the deterministic command boundary for play, pause, seek, reset, playback rate, two presets, initial separation, and Story/Graph/Model layer selection. A separation change commits a new scenario and resets the clock. It cannot silently change the current orbit. `FrequencyGraph.Sample` reevaluates that same model, from the start to the selected time; it stops short of the conceptual cutoff. Seek/reset/scenario changes increment `ContextVersion` so graph/tutor consumers can discard previous context. Layer selection leaves model state unchanged.

The presets use 30 + 30 solar masses and initial separations of 30 and 45 `GM/c²` of the total mass. The staging slider is bounded to 25–50 `GM/c²`, with cutoff at 10 `GM/c²`. This deliberately keeps the initial states outside the strongest-field region; even so, the leading-order model is approximate and is **not** a validated binary spacetime or an accurate merger prediction. The cutoff and range need review with the project team/domain expert. For these constants, one gravitational radius is about 88.6 km; the 30 and 45 `GM/c²` runs last about 18.47 and 94.45 physical seconds to cutoff, respectively. Playback rate 0.1×–4× changes traversal wall time, not physical frequencies.

The unwrapped physical phase is returned in radians. A scene can use `Body1X/ZMeters` and `Body2X/ZMeters` for center-of-mass placement, then divide by a documented meters-per-Unity-unit scale. Marker radius is illustrative. A normal frame rate may visually alias these fast orbits; the eventual scene must disclose any separate visual phase slowdown and keep physical values and graph unchanged.

`TutorSnapshot` captures an immutable versioned state when a question is accepted, including UTC capture time, scenario/context versions, masses, SI values, model phase, selected information layer, playback rate and display mapping. `ITutorProvider` is the integration boundary. `OfflineStagingTutorProvider` is scripted, local and visibly labeled; it is **not the functional AI component** required by the course. A consumer must check `TutorReplyValidity.IsCurrent` before displaying a pending answer after seek/reset/scenario changes. The eventual AI service must take the snapshot as data, never as permission to alter equations or execute arbitrary commands.

## Reproduce the source checks

With .NET SDK 9 available:

```powershell
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
```

The console checks frequency scaling and ratio, monotonic finite inspiral, equal/unequal center of mass, deterministic seeking, pause/reset, scenario replacement, layer independence, cutoff behavior, graph linkage, stale tutor replies, and invalid input recovery. Numerical comparisons use a relative tolerance of `1e-12` for the primary model quantities, scaled by `max(1, |expected|)`; center-of-mass residual checks use an absolute `1e-8 m` allowance in the test's chosen representation. These are source checks, not Unity Edit Mode or Play Mode evidence.

## Future Unity physics integration gate

When the visual prototype is ready for model integration:

1. Recheck every applicable `AGENTS.md`, Editor version, render pipeline, input system, XR packages, and resolved package versions after a licensed Editor import.
2. Move or copy the pure `Simulation/*.cs` sources into a runtime assembly under the project `Assets` tree. Keep `.NET` console test project outside `Assets`; create Unity Edit Mode tests using the installed Test Framework version.
3. Build an idempotent `BinaryBlackHole_Staging` scene with a stationary desktop camera, two model-driven markers, an orbital-plane reference, a readable linked frequency graph, and Story/Graph/Model panels. Bind UI commands to one `SimulationSession`; use unscaled UI input when simulation is paused.
4. Show separation (m or km), orbital frequency (Hz), GW frequency (Hz), physical time (s), playback rate, scenario ID, and explicit display-scale/merger labels. Show `N/A` for post-cutoff frequencies. Draw graph and body positions from the same evaluated state after every command.
5. Run Unity compilation, Edit Mode tests, and manual Play Mode checks in the matching Editor. Record actual evidence and any device-specific XR work separately.

## Source material and scope

The 4 October 2026 `CODEX_START_HERE.md` handoff is the implementation brief. The supplied `XR_DT_ET_Project_5(1).pdf` describes the causal model/XR MVP; `SA01_ProjectDescription_20260827(1).pdf` describes course requirements. The old draft report's novice-only target is superseded: the audience is university students aged 18–30 across knowledge and interest levels, and Story/Graph/Model depth is user selected. The handoff also names focus-group and design files that have not been supplied in this repository or selected workspace, so no usability or learning claims are made from them.

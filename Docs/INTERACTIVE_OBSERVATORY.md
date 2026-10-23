# Interactive binary observatory

The desktop observatory combines the recorded SXS0305 replay, an adjustable educational binary, two gas modes, approximate light distortion and a spacesuit wrist projector. All scene components and graphs share one scenario and event clock.

Launch the packaged `START_OBSERVATORY.cmd`, or run the player with `--interactive-exhibit`. In Unity 6000.3.25f1, open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity`, select **DT-ET > Startup Exhibit > Interactive Suit Observatory**, then press Play.

## First visit

Choose Auto, Laptop, Balanced or High before entering. The profiles vary astronomical rendering resolution, ray samples, sky detail and diagnostic sample count; event parameters stay the same. Compatibility keeps the binary, sky, navigation and interface with gas and optical distortion disabled. Auto admission uses reported capabilities, not a measured guarantee of performance on every computer.

The scene starts paused. Use the wrist's Play button or Space. V lowers the projection for an unobstructed view and raises it again. Pin/enlarge provides a larger reading panel. Motion: reduced opens the projection immediately; the setting is saved locally. The projector animation uses interface time, so it can open while the event is paused.

| Control | Action |
| --- | --- |
| WASD; Q/E | Move freely; descend/ascend |
| Shift | Faster movement |
| Right-drag; mouse wheel | Look; move closer/farther |
| 1 / 2 / 3 / 4 / 5 | Overview / above / edge / below / close |
| V | Raise/lower wrist |
| Space; Esc | Toggle playback; pause immediately |
| Comma | Reverse playback direction |
| R; M | Reset time; pause at the merger transition |
| G; T | Toggle the selected gas; weighted density samples (illustrative gas only) |
| Wrist timeline | Pause and seek to the chosen event time |
| Wrist Launcher | Release the scene and choose another quality profile |

Camera movement and look are suspended while the pointer is over the raised instrument panel. Esc remains available to pause. Moving the camera does not change the binary separation, physical frequency or waveform observer distance.

## Generate a fluid history without external gas files

Open **FLUID LAB / SOLVE GAS** on the left and press **SOLVE GAS**. A cancellable background calculation evolves the assumed disk using surface-density and planar-momentum conservation, pressure, softened Newtonian gravity and sinks. When it finishes, the player selects the computed gas and pauses at the covered start. Use the existing timeline, reverse, camera and wrist controls to inspect it; the mass ledger in the fluid panel follows the same cursor.

The physical grid and cached cadence are independent of Laptop/Balanced/High image settings. Only the final bounded interval is generated; the panel gives its exact coverage. Earlier times show no computed gas. Applying different binary parameters discards the old history; press SOLVE GAS again to generate a matching run. **ILLUSTRATIVE GAS** and **GENERATED FLUID** switch renderings without combining their fields. **GO TO COVERAGE START** returns to the computed interval. Closing the fluid panel restores an unobstructed view.

This is a two-dimensional isothermal hydrodynamics approximation with a prescribed Gaussian thickness, uncalibrated bright emission and external binary forcing. It is not measured GW150914 gas, a three-dimensional GRMHD calculation, or a binary spacetime solution. The current light warp remains illustrative and uses the selected gas field for its coarse foreground weighting. The [model contract](GENERATED_FLUID_MODEL.md) describes equations, assumptions and limitations, and the [public-source route](PUBLIC_SIMULATION_ROUTE.md) explains how more complete fields can also be generated without private data.

## Event replay and your experiment

**Event replay** preserves the licensed SXS0305 numerical tracks and radiation. It is a representative configuration associated with GW150914, not a unique reconstruction of the observed event. Its horizon spheres are area proxies, not photon shadows. The plot shows normalized Re(H22), with a separate frequency view and retained coverage; it is not H1/L1 detector strain. The original measured-event research and detector data remain in the references library.

**Your experiment** permits component masses from 5 to 80 solar masses, with smaller/larger mass ratio at least 0.1, and initial separation from 25 to 50 total gravitational radii (`rg = GM/c²`). Sliders are pending until **Apply & pause**. Apply commits bodies, gas, waveform and plots together at the new scenario's paused start. Invalid combinations leave the existing scenario intact and show feedback.

Sizes derive from mass using nonspinning Schwarzschild radius proxies. Increasing mass at fixed separation in rg also increases the separation in metres. The instrument shows both units. Applying new masses preserves the camera's physical distance where exterior safety bounds permit; selecting a scenario resets to an overview. These choices make size and angular changes interpretable.

The arbitrary-parameter inspiral uses a bounded leading-order circular model. Quantitative frequency and strain stop at 10 rg. A short, explicitly **illustrative** merger and remnant continue the visual timeline without fabricated quantitative merger data. The default experiment's signal is leading-order h+ for a declared far-field observer at 10¹² m, inclination 60 degrees and zero polarization angle; it is not a prediction at the freely moving inspection camera.

The waveform axis is fixed within each mode so changes in amplitude remain visible. Where cycles are too dense to resolve in the small plot, the graph uses a labelled envelope display. A frequency view, source-time cursor and instantaneous numerical signal remain available. Missing coverage is a gap.

## Light and gas

Off / subtle / enhanced and separate lens/wave gains control the visual effect. Off and zero gains directly show the original astronomical target. The suit, text and controls are composed afterward, so they remain stable.

The lenses use mass-dependent weak-deflection trends with bounded near-hole styling and approximate foreground-gas weighting. The magnified outgoing stretch/squeeze samples past waveform history using the scenario's units and clock; it responds to parameter changes and rewinds deterministically. It is a waveform-driven illustration, not photon propagation through a solved binary spacetime. Missing history produces no invented wave signal. Visual gain never changes orbital evolution or graph values.

ILLUSTRATIVE GAS uses a continuous prescribed three-dimensional density/emission field, with component-scale inner boundaries and mass-ratio-dependent truncation. Its optional points are weighted spatial density samples, not colliding plasma particles or independently simulated fluid parcels. This choice keeps free viewpoints and deterministic rewind inexpensive, but does not solve pressure, magnetic fields, turbulence or gas conservation. The separately selected GENERATED FLUID mode evolves pressure and planar conservative gas equations as described above. Both assume a bright environment; no bright disk was measured for GW150914.

The synthetic sky varies stellar flux, colour and image footprint with stable distant directions and bounded glare. It is not a measured sky at the source; apparent star size is a point-spread effect.

## Verification and scope

See the [original observatory receipt](INTERACTIVE_OBSERVATORY_VALIDATION.md) and [generated-fluid receipt](GENERATED_FLUID_VALIDATION.md) for their tested revisions, builds, renderer checks and performance methods. [The scope and scientific dependencies](FINAL_DESKTOP_SCOPE.md) distinguish the working desktop implementation from the remaining relativistic fluid/metric generation and laptop/XR measurements. The [scenario equations](INTERACTIVE_SCENARIO_MODEL.md) and [optical assumptions](INTERACTIVE_OPTICS.md) document the implementation in detail.

The M1/M2 lessons, M3 reference laboratory and original M4 exhibit remain separate entry points for comparison and regression. The new suit projection is a desktop illustration; tracked hands, stereo rendering and headset comfort are separate work.

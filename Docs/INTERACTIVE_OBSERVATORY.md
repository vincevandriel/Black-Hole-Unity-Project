# Interactive binary observatory

The desktop observatory combines the recorded SXS0305 replay, an adjustable educational binary, two gas modes, approximate light distortion and a spacesuit wrist projector. All scene components and graphs share one scenario and event clock.

The native desktop release opens this observatory directly from its Windows executable or Mac app. Earlier packages use `START_OBSERVATORY.cmd`; development players accept `--interactive-exhibit`. In Unity 6000.3.25f1, open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity`, select **DT-ET > Startup Exhibit > Interactive Suit Observatory**, then press Play.

## First visit

Choose Auto, Laptop, Balanced or High before entering. The profiles vary astronomical rendering resolution, ray samples, sky detail and diagnostic sample count; event parameters stay the same. Compatibility keeps the binary, sky, navigation and interface with gas and optical distortion disabled. Auto admission uses reported capabilities, not a measured guarantee of performance on every computer.

The scene starts paused with the wrist lowered and the camera framing both black holes. **PLAY INSPIRAL** or Space begins playback. **WATCH MERGER** jumps to the final 600 gravitational time units, frames the binary and plays at 0.01 source seconds per real second. **RESTART** returns to the beginning, pauses, restores the starting view and sets the rate to 0.05. V raises or lowers the wrist. Pin/enlarge provides a larger reading panel. Motion: reduced opens the projection immediately; the setting is saved locally. The projector animation uses interface time, so it can open while the event is paused.

| Control | Action |
| --- | --- |
| WASD; Q/E | Move freely; descend/ascend |
| Shift | Faster movement |
| Right-drag; mouse wheel | Look; move closer/farther |
| 1 / 2 / 3 / 4 / 5 | Binary / above / edge / below / wide reservoir |
| V | Raise/lower wrist |
| Space; Esc | Toggle playback; pause immediately |
| Comma | Reverse playback direction |
| R; M | Reset time; pause at the merger transition |
| G; T | Toggle the selected gas; weighted density samples (illustrative gas only) |
| Wrist timeline | Pause and seek to the chosen event time |
| Wrist Launcher | Release the scene and choose another quality profile |

Camera movement and look are suspended while the pointer is over the raised instrument panel. Esc remains available to pause. Moving the camera does not change the binary separation, physical frequency or waveform observer distance.

**STARLIGHT VIEW** resets the current event, hides gas and plays at 0.03 source seconds per real second so the moving lenses can be inspected against the stars. **BENDING: ON/OFF** compares the unbent source with its lensed images without changing time. RESTART or WATCH MERGER restores the gas; G toggles it independently. The prominently lensed source is a hypothetical Sun-sized star at 1 AU, not a measured star accompanying GW150914. It remains fixed in world space as the observer moves.

## Generate a fluid history without external gas files

Open **FLUID LAB / SOLVE GAS** on the left and press **SOLVE GAS**. A cancellable background calculation evolves the assumed disk using surface-density and planar-momentum conservation, pressure, softened Newtonian gravity and sinks. When it finishes, the player selects the computed gas and pauses at the covered start. Use the existing timeline, reverse, camera and wrist controls to inspect it; the mass ledger in the fluid panel follows the same cursor.

The physical grid and cached cadence are independent of Laptop/Balanced/High image settings. Only the final bounded interval is generated; the panel gives its exact coverage. Earlier times show no computed gas. Applying different binary parameters discards the old history; press SOLVE GAS again to generate a matching run. **ILLUSTRATIVE GAS** and **GENERATED FLUID** switch renderings without combining their fields. Selecting the computed field frames its wider reservoir, rather than cropping the view inside its cavity. **GO TO COVERAGE START** returns to the computed interval. Closing the fluid panel restores an unobstructed view.

This is a two-dimensional isothermal hydrodynamics approximation with a prescribed Gaussian thickness, uncalibrated bright emission and external binary forcing. It is not measured GW150914 gas, a three-dimensional GRMHD calculation, or a binary spacetime solution. Stellar optical effects are applied before the gas is drawn, leaving its emission undistorted. The [model contract](GENERATED_FLUID_MODEL.md) describes equations, assumptions and limitations, and the [public-source route](PUBLIC_SIMULATION_ROUTE.md) explains how more complete fields can also be generated without private data.

## Event replay and your experiment

**Event replay** preserves the licensed SXS0305 numerical tracks and radiation. It is a representative configuration associated with GW150914, not a unique reconstruction of the observed event. Its horizon spheres are area proxies, not photon shadows. The plot shows normalized Re(H22), with a separate frequency view and retained coverage; it is not H1/L1 detector strain. The original measured-event research and detector data remain in the references library.

**Your experiment** permits component masses from 5 to 80 solar masses, with smaller/larger mass ratio at least 0.1, and initial separation from 25 to 50 total gravitational radii (`rg = GM/c²`). Sliders are pending until **Apply & pause**. Apply commits bodies, gas, waveform and plots together at the new scenario's paused start. Invalid combinations leave the existing scenario intact and show feedback.

Sizes derive from mass using nonspinning Schwarzschild radius proxies. Increasing mass at fixed separation in rg also increases the separation in metres. The instrument shows both units. Applying new masses preserves the camera's physical distance where exterior safety bounds permit; selecting a scenario resets to an overview. These choices make size and angular changes interpretable.

The arbitrary-parameter inspiral uses a bounded leading-order circular model. Quantitative frequency and strain stop at 10 rg. A short, explicitly **illustrative** merger and remnant continue the visual timeline without fabricated quantitative merger data. The default experiment's signal is leading-order h+ for a declared far-field observer at 10¹² m, inclination 60 degrees and zero polarization angle; it is not a prediction at the freely moving inspection camera.

The waveform axis is fixed within each mode so changes in amplitude remain visible. Where cycles are too dense to resolve in the small plot, the graph uses a labelled envelope display. A frequency view, source-time cursor and instantaneous numerical signal remain available. Missing coverage is a gap.

## Light and gas

Off / subtle / enhanced and separate lens/wave gains control the visual effect. Off and zero gains directly show the original astronomical target. The suit, text and controls are composed afterward, so they remain stable.

Both mass lensing and the outgoing wave display affect background starlight before the gas and horizon proxies are drawn. Distant point stars use bounded centre deflection with preserved point-spread profiles; the nearby extended source uses an inverse thin-lens mapping that can produce arcs and multiple images. It has a finite emitting surface and compact glare, rather than a light attached to a black hole. Wave displacement samples past waveform history. **Enhanced** is the default, with magnified displacement and displayed radial spacing compressed **12:1**. Parameter changes and rewind use the shared event clock. These are approximate source-image models, not photon propagation through a solved binary spacetime. Missing history produces no invented wave signal. Visual gain and spacing never change orbital evolution or graph values.

ILLUSTRATIVE GAS uses a continuous prescribed three-dimensional density/emission field, with component-scale inner boundaries and mass-ratio-dependent truncation. Warm spiral bands make the emitting disks readable. The v2 prescription includes assumed emitting plunge regions and a shared gas ring through the common-horizon transition; these are presentation assumptions, not predictions of binary fluid dynamics. Its optional points are weighted spatial density samples, not colliding plasma particles or independently simulated fluid parcels. This choice keeps free viewpoints and deterministic rewind inexpensive, but does not solve pressure, magnetic fields, turbulence or gas conservation. The separately selected GENERATED FLUID mode evolves pressure and planar conservative gas equations as described above. Its display follows the solved density/velocity and does not add the illustrative spiral bands. Both assume a bright environment; no bright disk was measured for GW150914.

The distant synthetic sky varies stellar flux, colour and image footprint with stable directions and bounded point-spread widths. Eight bright sources are composed into the opening view, with many faint points behind them. Their apparent sizes are display point-spread effects. The separate featured source has an assumed solar radius at 1 AU, giving an unbent angular diameter of about 0.53 degrees. The sky and source are illustrative, not a measured field near GW150914. See [stellar-light validation](STELLAR_LIGHT_VALIDATION.md).

## Verification and scope

See the [visual repair validation](VISUAL_REPAIR_VALIDATION.md), [original observatory receipt](INTERACTIVE_OBSERVATORY_VALIDATION.md) and [generated-fluid receipt](GENERATED_FLUID_VALIDATION.md) for their tested revisions, builds, renderer checks and performance methods. [The scope and scientific dependencies](FINAL_DESKTOP_SCOPE.md) distinguish the working desktop implementation from the remaining relativistic fluid/metric generation and laptop/XR measurements. The [scenario equations](INTERACTIVE_SCENARIO_MODEL.md) and [optical assumptions](INTERACTIVE_OPTICS.md) document the implementation in detail.

The M1/M2 lessons, M3 reference laboratory and original M4 exhibit remain separate entry points for comparison and regression. The new suit projection is a desktop illustration; tracked hands, stereo rendering and headset comfort are separate work.

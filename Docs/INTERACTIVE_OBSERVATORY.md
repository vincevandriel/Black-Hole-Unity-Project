# Interactive binary observatory

The desktop observatory combines the recorded SXS0305 replay, an adjustable educational binary, evolved finite mini-disks, an optional outer-reservoir experiment, approximate curved light and a spacesuit wrist projector. The bodies and graphs share one scientific scenario and event clock; the assumed gas history has its own explicit coverage on that same clock.

The native desktop release opens this observatory directly from its Windows executable or Mac app. Earlier packages use `START_OBSERVATORY.cmd`; development players accept `--interactive-exhibit`. In Unity 6000.3.25f1, open `Assets/DTET/Scenes/BinaryBlackHole_VisualStaging.unity`, select **DT-ET > Startup Exhibit > Interactive Suit Observatory**, then press Play.

## First visit

Choose Auto, Laptop, Balanced or High before entering. The profiles vary astronomical rendering resolution, ray samples, sky detail and diagnostic sample count; event parameters stay the same. Compatibility keeps the binary, sky, navigation and interface with gas and optical distortion disabled. Auto admission uses reported capabilities, not a measured guarantee of performance on every computer.

The normal default loads an evolved mini-disk cache, lowers the wrist, frames both black holes and **starts with base playback rate 0.02 and automatic merger slow motion enabled**. The visible auto toggle and effective-rate readout show the changing source-time rate, reaching 0.12 times the base rate at common horizon. The same source epoch drives bodies, gas, material and waveform; this changes display timing, not physical dynamics or frequencies. Selecting a wrist speed disables auto, and the toggle restores it. The finite gas is initialized late, at `max(0, common_horizon_time - 1000*tg)`, so playback begins at that covered epoch rather than pretending the gas was evolved from the beginning of the source record. Earlier SXS body/waveform history remains available through the timeline, with no invented gas before initialization. Space pauses or resumes.

**ACCRETION VIEW** or **RESTART** selects the default disks, seeks their initialization epoch, pauses, restores binary framing and sets base rate 0.02 with auto slow motion enabled. It preserves current masses/scenario and reusable cache. **WATCH MERGER** selects default gas, jumps to the final 600 gravitational time units before the transition, frames the binary and plays at base rate 0.02 with auto enabled. V raises or lowers the wrist. Pin/enlarge provides a larger reading panel. Motion: reduced opens the projection immediately; the setting is saved locally. The projector animation uses interface time, so it can open while the event is paused.

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
| G; T | Toggle the selected gas; legacy weighted density samples where available |
| Wrist timeline | Pause and seek to the chosen event time |
| Merger slow motion | Toggle display slowdown near common horizon; see the effective source-time rate |
| Wrist Launcher | Release the scene and choose another quality profile |

Camera movement and look are suspended while the pointer is over the raised instrument panel. Esc remains available to pause. Moving the camera does not change the binary separation, physical frequency or waveform observer distance.

The default gas density and velocity evolve under planar pressure, softened Newtonian gravity and sinks. Its tidal deformation, depletion and any streams come from that numerical evolution, rather than prescribed rotating arms. The finite initial inventory is not continuously refilled, and two bright disks are not forced to survive until merger. A short illustrative capture-surface blend joins the black regions into the remnant. The retained source positions, radii and radiation remain unchanged; the source data do not supply a measured deforming horizon surface or a full binary optical spacetime.

**STARLIGHT ONLY** resets the current event, hides gas and plays at base rate 0.03, preserving the current auto-timing setting, so moving lenses can be inspected against the stars. **ACCRETION VIEW** restores the initialized disks. **BENDING: ON/OFF** toggles the approximate curved gas rays and stellar warp without changing event time; G toggles gas visibility independently. The prominently lensed source is a hypothetical Sun-sized star at 1 AU, not a measured star accompanying GW150914. It remains fixed in world space as the observer moves. **COMPATIBILITY / NO GAS** is a diagnostic mode with gas disabled; use normal **ENTER OBSERVATORY** for disks.

**OUTGOING WAVES** restores default gas and starts at `max(0, merger_time - 250*tg)`, with base rate 0.01 and auto merger slowdown. It lowers the wrist and moves the binary-view camera to 1.6 times its usual distance, exposing more background around the disks. Enhanced stellar effects use mass gain 0.35 and wave gain 2. The same scenario clock drives bodies, gas, material, waveform and instrument; pause, reverse and free movement remain available. In an adjustable experiment this start is 50 gravitational time units before its waveform cutoff. No unavailable signal is extrapolated after that cutoff.

The fixed synthetic galactic glow and faint stars make curved wave distortion easier to see. They use the shared signed retarded history with declared display amplification and 12:1 radial compression; this is an optical illustration, not a solved binary metric. See [outgoing-wave checks](OUTGOING_WAVES_VALIDATION.md).

**WAVE MAP** opens a separate, coloured strain diagram above the orbital plane. It starts slow playback near merger, hides gas and frames the outgoing waves. Cyan and amber indicate opposite strain phases; height is exaggerated and radial distance is compressed 12:1. The surface reads the same waveform history and event clock as the optical display and wrist instrument. Space pauses, comma reverses, and free movement remains available. Turning the map off preserves the current epoch and playback state, restores gas and returns to the binary view. ACCRETION VIEW restores the paused opening disks. The map is unavailable in Compatibility mode.

**FIND SUN-LIKE STAR** pauses the current epoch, hides gas and points a 12-degree telescope view at the assumed source. It enlarges the view without changing the star's physical size or position. A bracket labels its unbent direction. Press **BENDING** to compare that direction with the displaced stellar images; the caption explains why those images move away from the bracket. The source has one solar radius and lies one AU from the binary centre; it is hypothetical. Selecting a normal view with 1–5 restores the 52-degree field of view and the optical settings from before the finder. ACCRETION VIEW also restores the gas.

## Default disks and the separate reservoir experiment

The default mini-disks are already a computed fluid history, streamed from disk. No online gas files or new solve are needed to watch the recorded example. Their pressure/velocity evolution, finite inventory and late initialization are documented in [the mini-disk model](MINIDISK_DYNAMICS.md). The current [appearance contract](PLASMA_APPEARANCE.md) describes passive flow-following emission detail, the normalized local-height reconstruction, image sampling and display timing; none adds relativistic plasma physics.

The **FLUID LAB** also exposes a separate **SOLVE RESERVOIR** / **SOLVE GAS** experiment. This cancellable background calculation begins from a broad assumed reservoir and evolves surface-density and planar-momentum conservation, pressure, softened Newtonian gravity and sinks. It is the older coarse outer-flow experiment, not a required step to activate the default mini-disks. Completion selects that reservoir history and pauses at its covered start. The fluid-panel mass ledger follows its cursor.

**DEFAULT DISKS** and **GENERATED FLUID** switch the selected histories without combining their fields. If the current epoch is outside the default disk cache, **DEFAULT DISKS** seeks its covered start. **ACCRETION VIEW** restores the default disks at their initialization epoch. **GO TO COVERAGE START** seeks the reservoir's recorded interval. Closing the panel restores an unobstructed view. Physical grids and cached cadence are separate from Laptop/Balanced/High image settings; no field is extrapolated beyond its coverage. Changing source parameters invalidates mismatched gas.

Both modes are two-dimensional isothermal approximations with a prescribed vertical display profile and external binary forcing. Neither is measured GW150914 gas, three-dimensional GRMHD or a coupled spacetime solution. The reservoir's separate straight-volume renderer retains its documented limitations. See [its model contract](GENERATED_FLUID_MODEL.md) and [the public-source route](PUBLIC_SIMULATION_ROUTE.md) for the larger scientific target.

## Event replay and your experiment

**Event replay** preserves the licensed SXS0305 numerical tracks and radiation. It is a representative configuration associated with GW150914, not a unique reconstruction of the observed event. Retained horizon radii are area proxies; the display's approximate capture regions do not turn them into measured photon shadows. The plot shows normalized Re(H22), with a separate frequency view and retained coverage; it is not H1/L1 detector strain. The original measured-event research and detector data remain in the references library.

**Your experiment** permits component masses from 5 to 80 solar masses, with smaller/larger mass ratio at least 0.1, and initial separation from 25 to 50 total gravitational radii (`rg = GM/c²`). Sliders are pending until **Apply & pause**. Apply commits the new body/waveform scenario and automatically prepares matching mini-disk dynamics while playback stays paused. On successful completion, it loads that cache and seeks its actual initialization epoch. Old gas must not appear under new parameters. Cancellation or selecting the recorded event abandons the pending result. Invalid combinations leave the existing scenario intact and show feedback.

Sizes derive from mass using nonspinning Schwarzschild radius proxies. Increasing mass at fixed separation in rg also increases the separation in metres. The instrument shows both units. Applying new masses preserves the camera's physical distance where exterior safety bounds permit; selecting a scenario resets to an overview. These choices make size and angular changes interpretable.

The arbitrary-parameter inspiral uses a bounded leading-order circular model. Quantitative frequency and strain stop at 10 rg. A short, explicitly **illustrative** merger and remnant continue the visual timeline without fabricated quantitative merger data. The default experiment's signal is leading-order h+ for a declared far-field observer at 10¹² m, inclination 60 degrees and zero polarization angle; it is not a prediction at the freely moving inspection camera.

The waveform axis is fixed within each mode so changes in amplitude remain visible. Where cycles are too dense to resolve in the small plot, the graph uses a labelled envelope display. A frequency view, source-time cursor and instantaneous numerical signal remain available. Missing coverage is a gap.

## Light and gas

Off / subtle / enhanced and separate stellar lens/wave gains control the visual effect. Off selects straight gas rays and disables stellar warp. Stellar gain sliders control the background effect; setting their gains to zero does not alter the fluid solution. The suit, text and controls are composed afterward, so they remain stable.

Both mass lensing and the outgoing wave display affect background starlight before the gas and horizon proxies are drawn. Distant point stars use bounded centre deflection with preserved point-spread profiles; the nearby extended source uses an inverse thin-lens mapping that can produce arcs and multiple images. It has a finite emitting surface and compact glare, rather than a light attached to a black hole. Wave displacement samples past waveform history. **Enhanced** is the default, with magnified displacement and displayed radial spacing compressed **12:1**. Parameter changes and rewind use the shared event clock. These are approximate source-image models, not photon propagation through a solved binary spacetime. Missing history produces no invented wave signal. Visual gain and spacing never change orbital evolution or graph values.

The default continuous gas display samples recorded density/velocity, a passive planar material-label history and a normalized Gaussian height inferred from an assumed local isothermal relation. The labels modulate emission, not gas mass or forces; they are not resolved turbulence. Curved rays can reveal far-side emission above/below the direct plane; velocity-dependent brightness has a separately tested approaching/receding sign. An isotropic Schwarzschild index supplies a single-hole control, while the binary index blend and smooth merger capture remain optical approximations. They are not a time-dependent binary metric or full relativistic radiative transfer. Gray opacity, exposure and false colour are assumptions, not measured optical depth, temperature or luminosity. Thin rims can retain sampling artifacts, especially in Laptop mode. The former prescribed rings/spiral arms are not the default fluid solution. No bright disk was measured for GW150914.

The distant synthetic sky varies stellar flux, colour and image footprint with stable directions and bounded point-spread widths. Eight bright sources are composed into the opening view, with many faint points behind them. Their apparent sizes are display point-spread effects. The separate featured source has an assumed solar radius at 1 AU, giving an unbent angular diameter of about 0.53 degrees. The sky and source are illustrative, not a measured field near GW150914. See [stellar-light validation](STELLAR_LIGHT_VALIDATION.md).

## Verification and scope

See the [current appearance contract/evidence](PLASMA_APPEARANCE.md). The [visual repair validation](VISUAL_REPAIR_VALIDATION.md), [original observatory receipt](INTERACTIVE_OBSERVATORY_VALIDATION.md), [prior mini-disk receipt](MINIDISK_VALIDATION.md) and [generated-fluid receipt](GENERATED_FLUID_VALIDATION.md) are historical evidence for their identified revisions; they do not certify later changes. [The scope and scientific dependencies](FINAL_DESKTOP_SCOPE.md) distinguish the working desktop implementation from the remaining relativistic fluid/metric generation and laptop/XR measurements. The [scenario equations](INTERACTIVE_SCENARIO_MODEL.md) and [optical assumptions](INTERACTIVE_OPTICS.md) document the implementation in detail.

The M1/M2 lessons, M3 reference laboratory and original M4 exhibit remain separate entry points for comparison and regression. The new suit projection is a desktop illustration; tracked hands, stereo rendering and headset comfort are separate work.

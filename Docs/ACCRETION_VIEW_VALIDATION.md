# Accretion view: controls and automatic-render validation

Runtime source: `2f1d3a76c235cc41d7d1ac578a1e14044370232f`, 24 October 2026. The final Windows observatory run passes **281 non-capture assertions and 185 capture-write checks**, with **189 PNGs** present. It verifies visible initial disks and recovery from the gas-hidden starlight mode on Laptop, Balanced and High profiles using genuine automatic camera frames **after a windowed-resolution warmup**. It does not establish untouched hidden cold-start rendering or reproduce the user's exact installation and display state.

This change repairs presentation controls and extends integration QA. It does not change the gas shader, physics formulas, fluid solver, waveform data or stellar-light mapping. The existing scientific and optical model boundaries remain those in [INTERACTIVE_OPTICS.md](INTERACTIVE_OPTICS.md), [STELLAR_LIGHT_VALIDATION.md](STELLAR_LIGHT_VALIDATION.md) and [GENERATED_FLUID_MODEL.md](GENERATED_FLUID_MODEL.md).

## Controls and visible state

- **ACCRETION VIEW** selects the illustrative gas, enables it when the profile supports gas, hides density samples, resets the current scenario to its paused beginning, sets playback rate to 0.05, restores the opening camera and lowers the wrist. It preserves the selected binary parameters and an existing generated-fluid history.
- **RESTART** uses the same recovery action. **WATCH MERGER** selects the illustrative gas and enables it before seeking to the merger presentation, so a previously hidden or uncovered generated-gas display does not silently carry into that action.
- **STARLIGHT ONLY** explicitly names the mode that hides foreground gas to expose the background-source lensing demonstration. It is expected to remove the disks. The new accretion button provides a direct return route.
- Selecting generated fluid restores gas visibility; if the current event time lies outside the cached history, it seeks to the start of that coverage. This changes display selection and time, not the cached simulation itself.
- The phase label distinguishes illustrative assumed gas, gas hidden, generated Newtonian 2D fluid, missing generated coverage and compatibility mode. Compatibility deliberately has no gas; the recovery button does not override capability admission.

The recovered illustrative disks are not newly simulated or brightened by this patch. Earlier manual captures already showed them. The defect addressed here is ambiguous or persistent display state and the lack of a direct, clearly labeled recovery path.

## Final automatic-camera evidence

Evidence is retained outside Git under `C:/Users/vince/Documents/Codex/2026-10-23/i-w/outputs/Accretion-view-validation/`. The final receipt is `final-observatory/interactive-exhibit-integration.json`; it reports `success=true`, `performanceOnly=false`, and every one of its 466 records starts with `PASS`. Its player log ends with `DTET_INTERACTIVE_EXHIBIT_INTEGRATION_PASS`.

Environment: Unity **6000.3.25f1**, **Direct3D12**, **NVIDIA GeForce RTX 3060 Ti**, **AMD Ryzen 5 5600X**. The waveform display uses the normal Float storage route. The second QA build and final player run completed with exit 0.

Before scene allocation, the QA coroutine requests windowed 1280 x 720, waits 0.2 seconds, requests windowed 1600 x 900, then waits another 0.2 seconds. This prepares the presentation surface without an explicit/manual camera draw. Unity documents that [Screen.SetResolution applies at the end of the current frame](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Screen.SetResolution.html). The resulting test is automatic rendering after this setup, not an untouched cold launch.

For each gas-capable profile, both cameras are enabled. The test yields ordinary player-loop frames and reads the existing `SceneTarget` directly at the paused initial epoch. It compares gas on, gas off, and the image restored by `STARLIGHT ONLY` followed by `ACCRETION VIEW`. **No `RenderFrame`, `SubmitRenderRequest`, alternate camera target or manual camera draw occurs inside these measurements.** At least two automatic scene-camera callbacks must arrive for each image. Later UI captures and timing tests still use explicit render requests and are separate evidence.

| Profile | Scene pixels / base gas steps | Automatic callbacks: initial / off / restored | Warm gas pixels: measured / minimum | Disk A warm pixels: measured / minimum | Disk B warm pixels: measured / minimum |
| --- | --- | --- | --- | --- | --- |
| Laptop | 640 x 360 / 64 | 6 / 21 / 10 | 3,795 / 691.2 | 1,667 / 158.7 | 1,179 / 153.1 |
| Balanced | 800 x 450 / 96 | 11 / 44 / 13 | 5,930 / 1,080.0 | 2,595 / 248.0 | 1,848 / 239.2 |
| High | 1120 x 630 / 144 | 7 / 42 / 7 | 11,608 / 2,116.8 | 5,097 / 486.1 | 3,604 / 468.9 |

The visibility measure uses 8-bit readbacks of the linear scene: a pixel qualifies when gas-on red exceeds gas-off red by more than 20 levels and red exceeds blue by a factor of 1.15. More than 0.3% of all pixels must qualify. Each independently projected compact-disk region must be fully framed, have a radius of at least four pixels, and contain more than `max(16, 0.025*pi*radius^2)` qualifying pixels. The projected radii are **44.96 / 44.15 pixels** on Laptop, **56.20 / 55.19** on Balanced and **78.67 / 77.26** on High. These are image-visibility thresholds, not physical luminosity measurements.

All three restored images have **zero measured mean absolute RGB difference** from their respective initial images, below the `1e-6` threshold. The checks also confirm gas is enabled, the event is paused at time zero, the wrist is lowered and generated fluid is not selected after the recovery action.

There are nine direct automatic scene captures, three per gas-capable profile. These PNGs contain the raw linear scene without the interface or its display tone mapping, so they appear darker than the presentation captures. The complete image count is 189: 185 capture-write records plus four separately saved projection overlays. The 120 preview frames are included in these totals; capture writes are not additional physics tests.

## Visual review and preview

Direct inspection covered the final `laptop-first-view.png`, `laptop-automatic-start-gas.png`, and `accretion-movie/frame-000.png`, `frame-060.png` and `frame-119.png`. Both compact orange accretion structures are visible around dark horizon proxies in the Laptop opening view and the raw automatic scene. The interface exposes **STARLIGHT ONLY** and **ACCRETION VIEW**, and the phase label identifies the gas as illustrative and assumed.

The reviewed Balanced preview frames retain both disks and their extended streams while their positions and orientation change through the early inspiral. White features are the existing mapped background-star images, distinct from the orange gas. The preview is captured with gas on, the opening camera, mass-display gain 0.35 and wave-display gain 1.3. Its 120 deterministic, explicitly rendered samples span time zero through `min(0.5 seconds, 0.7*merger_time)`; it is an early-inspiral preview, not a complete merger or a recording of native live playback. Selected-frame inspection does not establish continuous playback smoothness.

## Failed diagnostics retained

The first `observatory/` run used a hidden batch player. Its log records zero automatic callbacks and zero qualifying gas pixels in the new measurements, including High. It did not produce a completed integration receipt. Manual rendering elsewhere in the diagnostic remained possible. This is failed/incomplete automatic-render evidence, not a pass.

The subsequent hidden non-batch `ordinary-observatory/` run produced a receipt with `success=false` and **12 failed checks**. Laptop and Balanced initially received zero automatic callbacks. High later received **7 / 42 / 7** callbacks and passed its disk measurements, after the pre-existing interface-resolution tests had requested 1280 x 720 and then 1600 x 900. That observation motivated moving the normal resolution setup before all profile measurements. The final run passes the same callback and pixel thresholds; they were not weakened or replaced with manual rendering.

These observations are consistent with delayed initialization of the hidden player's presentation surface, but the exact native-engine cause is unconfirmed. The final setup does not prove that an untouched hidden launch renders automatically, nor that the user's reported missing disks had this cause.

The latest ordinary local player log inspected for diagnosis was `C:/Users/vince/AppData/LocalLow/DT-ET Learning Project/DT-ET Binary Explorer/Player.log`, modified **24 October 2026 at 03:16:07 local time**. Its managed assembly path points to the older `outputs/Desktop-release/DTET_Binary_Explorer_Windows/DTET_Binary_Explorer_Windows/Windows/` extraction. This identifies that logged launch, not necessarily the version, operating system or gas mode involved in the user's report. Those details were not confirmed. It must not be treated as a log from this corrected release.

## Generated-fluid recovery regression

The final `final-fluid/generated-fluid-integration.json` reports success with **40 non-capture assertions, 11 capture-write checks and 11 saved images**. Its player exited zero. The two final runs therefore total **321 non-capture assertions, 196 capture-write checks and 200 images**. These are repeated integration checks, not distinct scientific validations.

The fluid run verifies that illustrative and generated selections restore hidden gas, ACCRETION VIEW and RESTART return to visible paused illustrative disks without discarding the reusable history, WATCH MERGER selects the illustrative merger gas, and a scenario change frames the new binary after invalidating the old wide reservoir. A second adjustable history starts after time zero: restarting restores illustrative disks outside that interval, and reselecting generated fluid seeks the covered start. GPU density/velocity, coverage, deterministic seek, cancellation and disposal checks remain included. `accretion-restored-from-fluid.png` was inspected and shows both orange compact disks after the mode switch.

The first history took **94.97 seconds** and retained **14,277,720 bytes** of array payload. Its mean completed offscreen frame time was **3.97 ms**, with maximum **8.05 ms**; these are correctness-run timings during other release work, not controlled performance comparisons. The fluid equations and sampling grid were unchanged.

## Scope, timing and release checks

The final receipt also retains the existing interface, signal, stellar optics, rewind, projection and resource-disposal checks. Its 16 short timing windows use completed offscreen rendering with a one-pixel GPU readback fence and player-loop yield; they are not measurements of automatic/native presentation. Across the four measured modes, Laptop mean times were **3.69–5.80 ms** with highest p95 **6.60 ms**, Balanced **5.57–7.54 ms / 8.05 ms**, and High **11.53–13.52 ms / 14.95 ms**. Every measured frame completed its readback. These results are not actual-laptop, Mac, headset or long-duration performance certification.

The final player log retains the D3D12 debug info-queue query warning. Inspection found no runtime exception or shader-compilation failure in that player log. Successful builds and captures do not establish a clean Editor Console or human usability.

No new pure-model test run or forced-Half rerun was performed for this control/QA patch. Those implementations were unchanged; the earlier portable and Float/Half results in [STELLAR_LIGHT_VALIDATION.md](STELLAR_LIGHT_VALIDATION.md) remain historical evidence at their stated commits, rather than being recounted as fresh tests. Both non-development platform builds completed successfully. [DESKTOP_DISTRIBUTION.md](DESKTOP_DISTRIBUTION.md) records the replacement package, archive checks, extracted Windows startup and static Mac inspection. Mac runtime and Gatekeeper acceptance remain unverified.

SXS0305 remains a representative GW150914-inspired replay, not a unique reconstruction. The bright gas and one-AU solar-radius featured star are assumed, not observed around GW150914. Illustrative gas is prescribed; the separate generated mode uses a reduced Newtonian 2D fluid model, not GRMHD or an evolved binary spacetime. Stellar lensing is bounded approximate mapping, and the amplified outgoing-wave display retains its 12:1 spatial compression. The stars, gas and waveform displays do not establish an observed electromagnetic counterpart, exact strong-field light propagation or Einstein Telescope measurements of this historical event.

Final observatory receipt SHA-256: `63e2840a49ae0b6aef38eb1ff38d41eb9a39b86fbdf9fa860480f62c88d3c55d`.

Final generated-fluid receipt SHA-256: `4651ecca608f734f770f2e02824ff08ca5e6901163e291c122417b2ce65af1d1`.

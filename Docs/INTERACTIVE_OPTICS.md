# M4.1 bounded lens and outgoing-wave display

This is an explicitly illustrative optical extension for the adjustable desktop exhibit. It uses the active scenario's masses, positions, clock and waveform history, but does not integrate null geodesics in a binary metric. It does not replace the isolated Schwarzschild control in M3 or the compatible gas/metric work planned for M5/M6.

## Placement and resource contract

`InteractiveOptics.Apply(source, camera, scenario, snapshot, enabled, lensGain, waveGain, gasVisible)` consumes the astronomical camera's completed linear render target. It returns one additional target of the same capped dimensions. The old `ExhibitDisplay` maps that result for presentation; the spacesuit, wrist, graph text and input targets are composed later and never pass through this warp.

Off and two zero gains return the original target object, without a blit, resampling or altered tone mapping. On uses one full-screen pass, two image samples, one small waveform lookup and at most 24 field samples per pixel. It requires no compute shader, 3D texture or full time-history cache. A half-float source keeps a linear half-float output; the LDR fallback retains its existing clipping limitation. The additional target is 8 bytes per pixel in HDR or 4 in LDR, with no depth buffer. A 256-entry RGBAFloat wave texture, its readable texture copy and the fixed CPU Color array add approximately 12 KiB of payload; Unity/driver overhead is excluded.

The target is reused until size/format changes and released with its material/texture on return or destruction. The fixed CPU array lives with the component. Missing shader or target allocation support produces the original scene with a status reason. If RGBAFloat sampling is unavailable, lenses can remain active but the outgoing-wave distortion is disabled explicitly. The parent must call `RenderNow()` after an explicit astronomical camera render in hidden QA; normal presentation runs the warp after that camera's completed render.

## Mass-dependent mapping

All geometric quantities use the scenario's total-mass gravitational radius, `rg = GM_total/c^2`. A component has dimensionless mass `mu = M_component/M_total`. A projected lens has positive camera depth `D_l`; `theta` is the tangent-plane angular displacement from that lens. For a source at depth `D_s`, its finite-source factor is

`g = max(0, 1 - D_l/D_s)` for `D_s > D_l`, otherwise zero.

The sky limit is `g=1`. The inverse image lookup uses

`delta_theta = -gain * (4 mu / D_l) * g * theta / (|theta|^2 + epsilon^2)`,

`epsilon = max(0.75 r_proxy, 0.15 mu) / D_l`.

At large impact parameter `b = D_l |theta|`, the unsoftened limit has the reference trend `alpha = 4 mu / b = 4GM_component/(c^2 b_physical)`. [Einstein Online's article by Steven and Irwin Shapiro](https://www.einstein-online.info/en/spotlight/light_deflection/) describes the inverse-impact-parameter behavior and the doubled relativistic deflection relative to the early Newtonian result. The finite-source geometric factor is a thin-lens approximation, not a strong-field binary solution.

Each component offset is limited to 0.16 tangent-angle units and their sum to 0.22. Gains are restricted to 0–2. The soft core, limits and near-hole extension are presentation choices. They give bounded, mass-responsive arcs and displacement; they do not predict the size of a relativistic shadow, photon ring, caustic or measured optical counterpart. The coordinate sphere silhouettes are retained as equal-area proxies by protecting rays that intersect them.

As the illustrative merger proceeds, the two displayed lens masses blend smoothly toward the common lens using the scenario's merger progress. For the representative replay, a declared 50-tg visual bridge precedes common-horizon formation. This changes only the optical display. The body proxies, gas, tracks, masses and waveform continue to use the immutable scenario snapshot. The remnant's retained radius/mass conventions determine the final lens. At fixed dimensionless geometry, a common total-mass scaling need not change the image: physical lengths and times scale together.

## Foreground handling and limits

Warping the whole image as if every pixel were a distant star would also bend gas in front of a hole. The shader instead samples the same analytic gas field along each original viewing ray. Twenty-four midpoint samples estimate optical transmission and emission-weighted source depth. For each lens it accumulates the finite-source factor only from emission behind that lens. The final lens weight blends that gas factor with the transparent-background factor using the estimated transmission:

`weight_lens = transmission + (1-transmission) * emission_weighted_mean(g)`.

Empty transparent pixels retain weight one, so lensing can move a previously off-pixel star into view. The mask never depends on whether the original pixel happened to contain a bright star. Fully foreground, opaque emitting gas drives the lens weight toward zero; gas behind the lens retains its finite-depth response. Turning the actual gas off disables this field mask.

This is a representative depth mask, not exact separation of all overlapping layers. The coarse quadrature can miss thin structures, mixed foreground/background pixels receive one compromise offset, and no newly revealed off-screen content is generated. A 4% edge fade and half-pixel clamping keep every lookup inside its source texture. These limits must remain visible in the model description; the method is useful for exploration, not quantitative binary ray tracing.

## Retarded waveform-driven distortion

The CPU fills 256 fixed radial samples over `0 <= r <= 4 * scenario.HalfExtentXZ`. Each bin requests

`scenario.EvaluateRetardedSignal(t, r) = EvaluateSignal(t - r * tg)`.

Thus the propagation mapping is one `rg` per `tg` (c in the scenario's units). No shader-only phase accumulator, wall-clock animation, looping or invented continuation is used. Seeking regenerates the same signed values from the same historical epoch. Missing history is stored with a coverage mask; interpolation touching an unavailable bin is rejected rather than extending the signal. Delayed waves may remain visible after the source signal reaches its cutoff, provided the corresponding earlier emission time is supported.

For the experiment, texture channels retain physical `h_plus` and `h_cross` at the model's declared observer distance and orientation. They are divided only for display by the fixed `ReferenceStrainAmplitude = 1e-8`; mass/distance-dependent changes are not hidden by per-scenario peak normalization. The default observer distance is 1e12 m, not an Earth detector. Representative replay uses the separately identified peak-normalized real and imaginary SXS h22 mode directly; that mode is not detector strain or a measured near-field metric. The display does not reinterpret its radial lookup as a new physical 1/r amplitude model.

The shader evaluates radial delay at an estimated emitting location, or at the source-centred image plane for the background. Two orthogonal transverse basis directions are projected from a source-to-camera frame into the image. The plus/cross display tensor is

`[ h_plus   h_cross ; h_cross  -h_plus ]`.

Its action on the source-centred image angle supplies a stretch/squeeze offset. [LIGO/Virgo's GW170814 explanation](https://ligo.org/science-summaries/gw170814/) and the [LIGO FAQ](https://ligo.org/faq/) provide the polarization and light-travel context. The implementation is a magnified visual analogy, not photon propagation through that tensor field. A fixed 0.025 presentation scale, normalized-signal clamp of +/-4, gain clamp of 0–2, maximum 0.035 tangent-angle displacement and outer radial fade keep the effect bounded. Gain never changes source strain, frequency, orbit, gas density or graph axes.

## Verification contract

`InteractiveOpticsTests.Run()` independently checks the weak-field coefficient and mass/impact trends; finite-source geometry; zero response for a foreground source; bounded near-hole output; plus/cross transverse behavior; zero-gain identity; all 256 retarded radial requests against explicit delayed source samples; missing history; retained signal kind after cutoff; and unchanged physical signal under visual gain.

The shader exposes a diagnostic branch for a linear floating-point GPU fixture. `_OpticsFixture=1`, `_FixtureInput.xy=theta`, `_FixtureLens=(D_l, mu, r_proxy, D_s)` and `_Optics.x=gain` return `(delta_x, delta_y, geometry, 1)`. Mode 2 uses `_FixtureInput.zw` as normalized plus/cross and `_Optics.y` as wave gain. Negative offsets require a floating-point readback. These fixtures are diagnostic APIs, never normal exhibit controls.

Portable formula checks do not establish shader correctness or visual usability. The combined player must additionally verify target identity when off, finite HDR output, actual foreground/background fixture response, repeated-seek image consistency, generation/time agreement with graphs and gas, resource release, and readability while the unwarped wrist is open. Runtime performance claims require measurements with the optical pass enabled; no headset or laptop certification follows from a desktop build.

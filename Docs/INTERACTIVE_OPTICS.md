# Bounded star lensing and outgoing-wave display

This is an illustrative optical extension driven by the active scenario's masses, positions, clock and waveform history. It does not integrate null geodesics in a binary metric. It does not replace the isolated Schwarzschild control or the compatible fluid/metric work described in the scientific model documents. The bright gas is assumed emission, not a measured optical reconstruction of GW150914.

## Rendering order and resource contract

Static mass lensing and outgoing-wave distortion now have separate rendering stages:

1. `ReducedExhibitSky.SetLensing(snapshot, gain)` binds the current bodies to the batched sky material. `ExhibitSky.shader` shifts each immutable star centre before building its pixel-integrated point-spread function (PSF). Gas and horizon proxies are rendered afterward.
2. `InteractiveOptics.Apply(...)` consumes that completed linear astronomical target and applies only the amplified, spatially compressed wave distortion. `ExhibitDisplay` maps the result for presentation. The spacesuit, wrist, graph text and input targets are composed later.

The exhibit binds sky lensing before normal camera rendering and before explicit `RenderFrame()` requests. The default display uses mass gain **0.35** and wave gain **1.3**, with both controls bounded to 0–2. Off resets sky lens gain to zero and returns the original scene target. Two zero gains also return the source target without a blit. A nonzero mass gain with zero wave gain changes the rendered star positions; the optical pass then copies its input without distorting gas.

The sky mesh is uploaded once and remains immutable. Lensing changes material uniforms, with no per-frame mesh rebuild, source duplication, new star objects or changes to the catalog fluxes and PSF widths. Sky lensing does not add another image target.

The wave pass owns one target at the capped scene dimensions: 8 bytes per pixel for half-float HDR or 4 for the LDR fallback, with no depth buffer. Its **512-entry** readable waveform texture plus fixed CPU `Color` array use approximately **24 KiB** with RGBAFloat, or **16 KiB** with RGBAHalf, counting the texture payload and its readable copy but excluding Unity/driver overhead. The pass uses one final displaced image sample and at most 24 field samples to estimate an emitting location; it requires no compute shader or 3D texture. The target is reused until its dimensions or format change and is released with the material/texture on exit.

RGBAFloat is selected only when sampling and linear filtering are supported, with a checked RGBAHalf fallback. If neither format is suitable, wave distortion is explicitly disabled while the independently supported sky lens can remain active. A missing optical shader or failed optical-target allocation also returns the already-rendered astronomical image with a status reason. Half-float HDR output preserves linear values; the LDR path retains its clipping limitation.

## Static mass lensing of distant stars

All geometry uses total-mass gravitational radii, `rg = GM_total/c^2`. A component has mass fraction `mu = M_component/M_total`, positive camera depth `D_l`, and projected tangent-plane displacement `theta` from the star's source direction. The shared `BoundedBinaryLensing.hlsl` helper and `InteractiveOpticsReference` use the weak inverse-lookup reference

`delta_inverse = -gain * (4 mu / D_l) * g * theta / (|theta|^2 + epsilon^2)`,

`epsilon = max(0.75 r_proxy, 0.15 mu) / D_l`.

For the distant sky, `g=1`. The finite-source diagnostic retains `g=max(0,1-D_l/D_s)` for `D_s>D_l`, and zero otherwise; this finite-source formula is no longer used to remap the composite gas image. At large impact `b=D_l |theta|`, the weak trend is `alpha=4 mu/b = 4GM_component/(c^2 b_physical)`. The source-centre display uses the forward sign, `delta_apparent = -delta_inverse`, evaluated at the source angle. This is a bounded first-order placement of one apparent centre per star, not a solution for strong-field image multiplicity.

Near a lens, the scalar displacement/impact ratio is smoothly saturated at `0.30 * min(mu,1)`. If the uncapped ratio divided by this limit is `q`, it is unchanged for `q<=0.5`, follows `limit * (1 - 0.5*(1.5-q)^2)` for `0.5<q<1.5`, and equals the limit for `q>=1.5`. Each component offset is additionally capped at 0.16 tangent-angle units and the summed sky displacement at 0.22. These bounds are display choices, not predictions of shadow or photon-ring size. They keep the weak far-field limit while preventing the old unbounded near-field extension from folding source placement through itself.

Every star's four quad vertices receive the same centre shift. The existing PSF is then constructed and integrated over pixels unchanged, preserving the source's assigned flux and width rather than stretching its image or adding a luminous ring. The active binary or remnant masses and positions come directly from the snapshot. At fixed dimensionless geometry, a common total-mass rescaling need not change the picture because physical lengths and times scale together.

## Why the gas is no longer statically remapped

The previous full-image static lens attempted a finite-source weighting from a coarse gas quadrature. It still sampled a composite that contained foreground gas and black proxy silhouettes, without access to hidden radiance. Near the holes this produced repeated bright arcs, disconnected gas copies and false black silhouettes. A near-hole displacement cap alone did not remove every artifact.

The current separation removes that mechanism: the static lens is applied to the background star centres before any gas is composed. It cannot sample a stream or disk into a second luminous patch. No brightness threshold or colour classifier is used to identify gas. Strong-field bending of disk emission is not supplied by this static star approximation. The separate wave display can distort both the existing gas image and the starfield, with the limits below.

## Retarded waveform and 12:1 display compression

The CPU fills 512 radial bins over

`0 <= R_history <= 12 * (2 * scenario.HalfExtentXZ)`.

Each bin requests `EvaluateRetardedSignal(t,R_history)`, equivalent to `EvaluateSignal(t-R_history*tg)`. The shader maps a displayed probe radius to that source-history radius using

`R_history = 12 * R_display`.

Thus physical waveform wavelengths occupy one twelfth of their physical radial extent in the visual scene. The displayed propagation speed is `c/12` in scene-length units at unit event-clock rate. This is an explicit spatial presentation compression, not a changed wave frequency, a claim about physical propagation speed, or a modified binary evolution. `WaveRadiusRg` exposes the actual history radius used by the CPU/GPU readback tests; `DisplayWaveRadiusRg` exposes its value divided by twelve.

No independent shader clock, looping sine, arbitrary source frequency or invented continuation is used. Seeking samples the same source epoch again. Missing history has a coverage mask, and interpolation touching an unavailable bin is rejected. Delayed waves can remain available after the source waveform cutoff only while their corresponding earlier emission times are supported.

For adjustable experiments, the scientific signal remains physical `h_plus` and `h_cross` at the declared observer distance and orientation. The fixed display normalization is `ReferenceStrainAmplitude=1e-8`; there is no per-scenario peak normalization hiding mass/distance changes. The default observer distance is 1e12 m, not an Earth detector. An RGBAFloat texture stores the physical strains and applies the fixed normalization in the shader. RGBAHalf stores their already-normalized display values to avoid underflow, without changing CPU scientific values or graphs.

Representative replay uses the separately identified peak-normalized real and imaginary SXS h22 mode. This mode is not detector strain or a measured near-field metric. Radius selects a retarded time; the display does not invent a local physical `1/r` strain law.

## Wave geometry, amplification and limits

For each camera ray, the display probes its closest forward approach to the system centre, or an estimated foreground emitting location when that is nearer. The emitting location comes from 24 midpoint samples of the active prescribed or generated gas field, using the same display-emission helpers as its volume renderer. This is a coarse representative location, not a multi-layer radiative-transfer solution; thin structures can be missed and foreground/background mixtures can have approximate visual phase.

Two orthogonal transverse directions from the source-to-camera frame are projected into the screen. The local transverse probe position is divided by `max(1,camera_radius)` to obtain `u`. The signed display tensor is

`H = [ h_plus h_cross ; h_cross -h_plus ]`.

The local displacement is

`delta_wave = 0.055 * clamp(gain,0,2) * H*u / (0.18 + |u|)`.

Normalized plus/cross inputs are limited to +/-4 and the displacement is capped at 0.035 tangent-angle units. The regular angular core makes the centre finite; opposite waveform phases reverse the stretch/squeeze. The tensor is a magnified visual analogy, not photon integration through a spacetime metric. Visual gain and radial compression never change source strain, frequency, orbit, gas density, time controls or graph axes.

The wave fades near projected horizon silhouettes, across the outer 15% of the covered display radius and within 4% of the image edge. Original rays intersecting a horizon retain the source pixel; displaced lookups into a horizon also retain the source pixel to avoid copying a proxy silhouette. Lookup coordinates remain clamped inside the source texture. The wave-mask blend around the common horizon is a display guard, separate from the sky's snapshot-driven masses and positions.

Large amplified gradients can noticeably wrinkle the disk image and stretch nearby star images during merger. The amplification and compression are labelled in the wrist controls/status. They are intended for inspection and visible propagation, not quantitative strong-field optical predictions. The pass adds no emissive wave rings.

## Verification contract

Portable tests check the weak-field coefficient and mass/impact trends, finite-source reference, near-field bounds, monotonic lens mapping over the supported gains and masses, the forward star-centre sign, zero-gain identities, transverse plus/cross response, a finite wave centre, visible amplified response, and opposite signed phases. They also compare retarded requests to explicitly delayed source samples, verify supported/unavailable coverage, preserve signal kind after cutoff and prove that one signed front propagates between two compressed display radii with the exact source-history delay. Display gain does not mutate the source signal.

The optical shader exposes diagnostic branches, not exhibit controls:

- Mode 1: `_FixtureInput.xy=theta`, `_FixtureLens=(D_l,mu,r_proxy,D_s)`, `_Optics.x=gain`; returns the bounded inverse displacement and finite-source geometry.
- Mode 2: `_FixtureInput.zw` supplies normalized plus/cross and `_Optics.y` supplies wave gain; returns the bounded wave displacement.
- Mode 3: uses the actual bound camera rays and horizon data to return a red analytical silhouette mask across the complete target.
- Mode 4: calls the exact shared `BoundedSkyApparentOffset` helper used by the production sky vertex shader and returns its forward star displacement. Nine combinations of source angle and gain are compared with the CPU reference.

Signed formula fixtures use a linear floating-point readback. The mode-3 test separately renders real proxy meshes in green through the actual scene camera, then compares their pixels with the analytical optical mask. Four viewpoints must exceed 90% intersection/union and remain within 1.5 pixels of centroid agreement; this catches projection/aspect/UV errors that isolated formula fixtures cannot. Diagnostic overlays use yellow for agreement, red for mask-only and green for mesh-only pixels, and restore material, rendering and visibility state afterward.

The GPU history test reads all 512 bins from the actual texture, decodes the active Float/Half storage convention and compares coverage and signed magnitudes to CPU source-history requests. Image tests also compare wave-on/off results at four viewpoints, test deterministic rewind and check per-hole emission and framing. Formula agreement and image differences are evidence of functioning bindings, not proof of visual quality; saved captures and a rendered merger sequence are reviewed separately.

The earlier M4.1 evidence described the now-replaced 256-bin, full-image static lens. Current repair results and their runtime/platform boundaries are recorded in [VISUAL_REPAIR_VALIDATION.md](VISUAL_REPAIR_VALIDATION.md). Desktop builds and automated captures do not establish headset operation, laptop certification, native presentation performance, or a validated relativistic fluid/metric simulation.

# Background starlight lensing and outgoing-wave display

The optical display bends background stellar light using the active scenario's masses, positions, clock and waveform history. Foreground accretion emission and black horizon proxies render afterward and remain undistorted by these effects. This is an illustrative thin-lens and amplified-wave extension, not integration of null geodesics in a binary metric. Bright gas, the sky catalogue and the featured background star are assumed sources, not an optical reconstruction of GW150914.

## Rendering order and resources

1. `InteractiveOptics.Prepare(...)` binds camera/lens uniforms and retarded waveform history before the astronomical camera renders.
2. `ReducedExhibitSky` draws `StellarBackdrop.shader`: fixed world-direction faint stars and unresolved synthetic galactic light. An inverse lookup applies the shared retarded wave displacement and bounded mass deflections before sampling that radiance.
3. `ExhibitSky.shader` draws the original distant points. Each immutable star centre receives a bounded forward mass shift and the negative of the first-order inverse wave shift, then its normalized pixel-integrated point-spread function (PSF) is constructed.
4. `FeaturedBackgroundStar.shader` evaluates a finite stellar source through the inverse thin-lens equation and the same wave lookup. Extended images derive from that source's light.
5. Horizon proxies and prescribed/generated gas render over the background. The optional wave map draws a translucent strain diagram before gas. `ExhibitDisplay` maps the linear scene for presentation. Spacesuit, wrist, graph text and input targets are composed later.

Normal camera rendering and explicit `RenderFrame()` captures prepare stellar uniforms before drawing. Default mass gain is **0.35** and wave gain **1.3**, with both controls bounded to 0–2. Off zeroes both stellar effects. Display gains do not change source geometry or intrinsic emission, binary evolution, gas fields or scientific graphs.

There is **no additional optical image target or post-render image blit**. `InteractiveOptics.Output` and `RenderNow()` return the existing scene target. The distant-sky mesh is uploaded once; lensing changes uniforms without a per-frame mesh rebuild. The featured star uses one immutable screen quad and material, analytic source evaluation and no source image texture. Its full-screen evaluation adds fragment work, but no image-sized buffer or particle population.

The shared **512-entry** readable waveform texture plus fixed CPU `Color` array use approximately **24 KiB** with RGBAFloat or **16 KiB** with RGBAHalf, including texture payload/readable copy but excluding Unity/driver overhead. RGBAFloat requires sampling and linear-filter support; RGBAHalf is the checked fallback. If neither works, wave gain is zero while independently supported mass lensing may remain active. Shader availability/status is reported and owned resources are released on exit. This stellar display requires no compute shader, 3D texture or foreground gas-depth quadrature.

## Fixed stellar backdrop

`StellarBackdrop.shader` evaluates two dense faint-star layers and a synthetic galactic band from the world ray direction. Four bounded noise evaluations provide fixed cloud/dust structure. There is no independent time variable: only camera movement and the shared mass/wave lookup change its apparent image. The shader includes the render-target vertical sign and refreshes camera basis/FOV even when bending is off.

The backdrop adds one draw, one material and a 60-byte mesh payload, excluding Unity/driver overhead. It imports no sky texture and allocates no additional render target. Its continuous structure makes alternating compression and stretching visible between isolated point stars. `ReducedExhibitSky.Visible` hides both its original point batch and this layer, preserving foreground-isolation checks.

**OUTGOING WAVES** starts at `max(0, merger_time - 250*tg)`, rate 0.01, gains 0.35/2 and 1.6 times the usual binary-view camera distance. It keeps illustrative gas visible. The waveform formula, normalization and 12:1 compression below are unchanged. Adjustable-mode start precedes its waveform cutoff by 50 gravitational time units.

## Distant stars: brightness, apparent size and mass shifts

`SyntheticSkyCatalog` remains index/seed-stable, with positive apparent fluxes, fixed directions and quality-stable prefixes. Eight bright synthetic references with warm/cool colors lie in the initial view; their world directions stay fixed while the user turns or travels. Many other stars remain faint pinpoints. Their widths represent display PSFs, not resolved stellar radii or measured distances.

For relative flux `F` and image height `H`,

`sigma_pixels = max(0.65, min(3, 0.52 + 1.8 sqrt(F)) * H / 900)`.

The truncated, pixel-integrated Gaussian preserves assigned total flux across widths, resolutions and subpixel positions. At 900 pixels high, bright sources reach three-pixel sigma while faint ones remain near the minimum. Mass/wave shifts translate each complete quad without stretching, magnifying or duplicating its PSF. Multiple images and magnification are demonstrated only by the finite source below.

Geometry uses `rg = GM_total/c^2`. For component mass fraction `mu`, positive lens depth `D_l` and tangent-plane source offset `theta`, the shared bounded helper uses

`delta_inverse = -gain * (4 mu / D_l) * g * theta / (|theta|^2 + epsilon^2)`,

`epsilon = max(0.75 r_proxy, 0.15 mu) / D_l`.

Distant stars use `g=1`. The finite-source diagnostic retains `g=max(0,1-D_l/D_s)` for sources behind a lens and zero otherwise. The displayed centre uses the forward sign `delta_apparent=-delta_inverse`, evaluated at the source angle. At large impact `b=D_l |theta|`, the underlying trend is `alpha=4 mu/b`.

Near a lens, the displacement/impact ratio saturates at `limit=0.30 min(mu,1)`. For `q=uncapped_ratio/limit`, the ratio stays unchanged below 0.5, becomes `limit*(1-0.5*(1.5-q)^2)` between 0.5 and 1.5, and equals `limit` above 1.5. Component offsets are also capped at 0.16 tangent-angle units and their sum at 0.22. These display bounds prevent source placement folding through itself; they do not predict shadows or photon rings. A common mass rescaling at fixed dimensionless geometry need not change the distant-star view because physical lengths scale together.

## Featured finite source: one solar radius at one AU

The added source is a **hypothetical star** of radius `695,700,000 m`, at fixed barycentric distance `149,597,870,700 m` (one AU), along normalized world direction `(0.10,-0.547,0.831)`. It is deliberately positioned behind the opening binary view, not identified as an observed GW150914 companion or background star. Its placement is not an orbital solution or a claim of a stable stellar configuration.

`StellarLensReference.SourceDimensions` converts those physical lengths to current scenario `rg` units. Mass adjustments therefore retain the assumed physical size and distance. Camera motion recomputes the actual source projection rather than making it follow the observer. Near the binary its unbent diameter is about **0.53 degrees**, modest within the 52-degree view.

The renderer maps each image angle backward into the source plane:

`theta_wave = theta + delta_wave(theta)`,

`beta = theta_wave - sum_i alpha_i(theta_wave)`.

For lens angle `theta_i`, depth `D_i`, mass fraction `mu_i` and source depth `D_s`,

`alpha_i = A_i*(theta_wave-theta_i) / max(|theta_wave-theta_i|^2, core_i^2, 1e-12)`,

`A_i = 4 mu_i*clamp(gain,0,2)*max(0,1-D_i/D_s)/D_i`,

`core_i = max(0.25 r_proxy_i,0.03 mu_i)/D_i`.

Lenses behind the source or camera contribute zero. Each deflection is capped at one tangent-angle unit to keep the unresolved core finite. Outside that core, the inverse map retains the point-mass equation, including secondary images, rather than merely shifting a source centre. Thin-lens coefficients, distance factors, two point-mass images and Einstein radius follow [Narayan and Bartelmann, sections 2.1.1–2.1.5](https://www.astro.umd.edu/~richard/ASTR680/Naryan_Bartlemann_Lensing.pdf). The summed point-mass equation also appears in [An (2007), equation 2](https://doi.org/10.1111/j.1365-2966.2007.11562.x); that paper's lattice analysis is not a model of this binary.

For `r=|beta-beta_source|/angular_radius`, disk brightness is `I(r)/I(0)=0.45+0.55 sqrt(1-r^2)` within `r<=1`, with derivative-based edge antialiasing. A small glare term `0.04 exp(-0.75 r^2)` fades between 2.5 and 3 source radii and passes through the same mapping. Color, exposure, limb coefficient and glare are display assumptions, not a fitted stellar atmosphere. The lens emits nothing: visible stellar images sample this source distribution.

Inverse surface-brightness evaluation permits total received flux to change with image area. The finite star does not use the distant-catalogue PSF's fixed integrated flux. This follows the distinction between surface-brightness preservation and solid-angle magnification in [Narayan and Bartelmann, section 2.1.5](https://www.astro.umd.edu/~richard/ASTR680/Naryan_Bartlemann_Lensing.pdf).

The mapping is exact only for the implemented bounded equation, not for the physical binary spacetime. It sums approximate projected deflections without coupled multi-plane propagation, spin-dependent paths, relativistic lens motion, image travel-time differences or strong-field photon orbits. Shared lens uniforms smoothly transfer binary strength to the common lens over the presentation bridge; bodies, gas and waves retain their scenario snapshots. Near-binary viewpoints and amplified controls exceed the regime in which a weak-field thin-screen approximation should be read quantitatively.

## Foreground isolation

Prior full-image effects could remap foreground gas and black proxies into detached arcs, repeated disks and false silhouettes. Current mass and wave effects operate only while rendering stellar sources. Neither samples the completed gas image. Gas retains its renderer's density, emission and geometry; existing scene order lets horizon proxies occlude the background.

This implements background-starlight bending, not strong-field disk-emission bending. With all stellar layers hidden, changing lens/wave controls must leave foreground gas unchanged; integration QA contains that isolation check. **STARLIGHT ONLY** hides gas to expose the stellar images. **ACCRETION VIEW** and **RESTART** restore visible illustrative disks and the opening camera, including after generated-fluid mode was selected.

## Retarded waveform and 12:1 display compression

The CPU fills 512 radial bins over `0 <= R_history <= 12*(2*scenario.HalfExtentXZ)`. Each calls `EvaluateRetardedSignal(t,R_history)`, equivalent to `EvaluateSignal(t-R_history*tg)`. Stellar shaders use

`R_history = 12*R_display`.

Physical wavelengths occupy one twelfth of their physical radial extent in the scene, giving displayed propagation speed `c/12` at unit event-clock rate. This is spatial presentation compression, not altered physical propagation, frequency or binary evolution. `WaveRadiusRg` exposes the history extent and `DisplayWaveRadiusRg` its value divided by twelve.

There is no independent shader clock, looping sine or invented continuation. Rewinding samples the same epoch. Missing history carries a coverage mask, and interpolation touching an unavailable bin is rejected. Delayed waves remain available after source cutoff only while their earlier emission times are supported.

Adjustable experiments retain physical `h_plus` and `h_cross` at declared observer distance/orientation. The fixed display normalization is `ReferenceStrainAmplitude=1e-8`; per-scenario normalization cannot hide mass/distance changes. Default observer distance is 1e12 m, not an Earth detector. RGBAFloat stores physical strain with shader normalization; RGBAHalf stores already-normalized display values to avoid underflow. CPU scientific values and graph axes remain unchanged.

Representative replay uses separately identified peak-normalized real/imaginary SXS h22 mode values, not detector strain or a measured near-field metric. Radius chooses retarded time, without introducing a local physical `1/r` strain law.

## Wave geometry and amplification

`StellarWaveWarp.hlsl` probes each camera ray's closest forward approach to the system centre, with no estimated gas depth. Two source-to-camera transverse directions define `u`, the probe's transverse position divided by `max(1,camera_radius)`. For signed tensor `H=[h_plus h_cross; h_cross -h_plus]`,

`delta_wave = 0.055*clamp(gain,0,2)*H*u/(0.18+|u|)`.

Normalized strains are limited to +/-4, and displacement to 0.035 tangent-angle units. It is projected into the screen basis and fades over the outer 15% of supported radius. The core remains finite; opposite waveform phases reverse stretch/squeeze. Distant points take the negative displacement at their mass-shifted centre as a first-order forward approximation. The finite star applies the inverse lookup per fragment, so its resolved images can deform.

An explicit texture LOD permits the same history helper in vertex and fragment stages. The stellar distortion adds no luminous wave rings or source-free light. This is a magnified tensor analogy, not photon propagation through a dynamic metric. Wave gain/compression leave source strain, frequency, orbit, gas density, clock controls and scientific graphs unchanged. Highly visible gradients are not quantitative astrometric predictions.

## Optional wave map and star finder

`WaveSurfaceView` adds an explicitly illustrative orbital-plane diagram when WAVE MAP is selected. It samples the existing 512-bin history at `R_history = 12 R_display`, including its coverage mask and Float/Half normalization. For orbital-plane angle phi, its signed display signal is `s = h_plus cos(2 phi) + h_cross sin(2 phi)`. Replay uses the declared normalized h22 components. This scalar projection is an explanatory diagram, not a metric embedding or a full angular radiation solution.

Height is `min(18 rg, 0.12 R_max) * clamp(s,-1,1) * envelope(R/R_max)`. The envelope suppresses the central singular direction and outer edge; unavailable history is hidden. Cyan and amber distinguish opposite signs. Geometry and grid-line density are independent so smooth waves do not require a dense wire overlay. Laptop/Balanced/High use 96/160/192 vertices per side and 32/40/48 displayed grid segments, with mesh payloads of 218,892 / 610,572 / 880,140 bytes, excluding engine overhead and upload copies. The immutable UInt16 mesh is uploaded without a retained CPU copy, allocated only on first use and released on scene exit. It reuses the waveform texture and adds no image buffer. History remains current while the map is visible even if stellar bending is Off or both optical gains are zero. Pause and rewind therefore reproduce the same geometry.

The star finder changes the camera to a 12-degree telescope view and pauses the current epoch. It keeps the physical one-solar-radius, one-AU source fixed. The UI bracket uses the camera's unbent projection, and the bent-view caption identifies displaced images separately. Selecting a normal camera preset restores its 52-degree field of view and prior optical settings. This makes a small source discoverable without inflating its physical radius or attaching it to the camera.

## Verification contract and evidence

Portable tests check weak-field coefficients, mass/impact trends, bounded mapping and forward signs, catalogue determinism, opening-view bright/faint/color/size variation, independently integrated PSF flux, zero-gain identities, transverse plus/cross response and delayed-wave coverage. `StellarLensTests` checks physical source dimensions under mass rescaling, source-depth geometry, independent analytic point-mass image locations, the Einstein ring and bounded core. These test declared formulas, not a physical binary metric.

Development builds retain optical fixture modes 1–4, direct stellar deflection, gas-field sampling and the reference slab fixture. Their executable code is guarded by `DTET_QA_FIXTURES` in five shaders. `ShaderFixtureStripper` removes keyword-enabled variants from non-development builds; `PrototypeBuild` enables them only for development builds and clears the flag in `finally`. Hidden ShaderLab property names and the reference material's unused layout field can remain as metadata; they do not enable executable release fixtures. QA call sites enable the keyword only where needed and restore prior state.

GPU history checks compare all 512 actual bins with CPU values in Float/Half conventions. Independent projection checks compare emitted pixels with Unity's world-to-screen projection, including both vertical signs to detect render-target flips.

Updated integration QA checks foreground isolation with stars hidden, modest unbent source area, visible mass/wave response, fixed source position under camera movement, deterministic rewind and starlight/restart controls. Epoch/viewpoint captures and an optional rendered sequence support visual review. Formula agreement and image differences alone do not establish good appearance.

Current discovery-view results and platform limits are recorded in [DISCOVERY_VIEWS_VALIDATION.md](DISCOVERY_VIEWS_VALIDATION.md). [OUTGOING_WAVES_VALIDATION.md](OUTGOING_WAVES_VALIDATION.md) and [STELLAR_LIGHT_VALIDATION.md](STELLAR_LIGHT_VALIDATION.md) retain the earlier stellar-display matrices. [VISUAL_REPAIR_VALIDATION.md](VISUAL_REPAIR_VALIDATION.md) records the preceding repair, whose composite-image wave path has now been replaced. Builds and hidden captures do not establish headset operation, laptop certification, native presentation performance or validated relativistic fluid/metric simulation.

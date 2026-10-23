# M4 startup rendering budgets and synthetic sky

M4 chooses a rendering budget before constructing the reduced binary exhibit. Every profile uses the same event samples, reduced gas formula, physical epoch and source parameters. Ray integration, render dimensions, tracer display count and the faint end of the synthetic sky are the adjustable costs. More rays or markers do not turn the prescribed gas into a fluid simulation or improve the source event's measurement uncertainty.

## Startup budget contract

`ExhibitQuality.Resolve` is a pure decision function with explicit reported capabilities. The Unity launcher supplies `SystemInfo` values and can deliberately request the unsupported-shader path for its diagnostic QA. The result preserves both the requested and effective profile and supplies a readable reason.

| Effective profile | Gas ray steps | Render scale | Maximum scene target | Sky sources | Tracers | Render/mesh allowance |
| --- | ---: | ---: | --- | ---: | ---: | ---: |
| Laptop | 64 | 0.5 | 640 x 360 | 1,024 | 256 | 8,028,160 bytes |
| Balanced | 96 | 0.75 | 800 x 450 | 2,048 | 768 | 12,961,792 bytes |
| High | 144 | 1.0 | 1,120 x 630 | 4,096 | 2,048 | 25,724,928 bytes |

These are initial budgets, not measured laptop or high-end frame-rate guarantees. The lower-resolution scene is presented under a full-resolution interface. `RenderDimensions` chooses the lesser of the requested scale and both maximum dimensions, preserves the display aspect to even-pixel rounding, and never enlarges the source display.

Auto selects Balanced only when the device reports at least 2,048 MB graphics memory and 8,192 MB system memory. Otherwise it selects Laptop; unknown memory is treated conservatively. Auto never selects High. A known report below 1,024 MB graphics or 4,096 MB system memory reduces an explicit higher selection to Laptop. High requires the larger reported memory thresholds; otherwise it reduces to Balanced. These checks cannot distinguish every shared-memory GPU or background load and are not a hardware benchmark.

The gas renderer uses an analytic shader and needs shader model 3, but requires neither 3D textures nor compute shaders. A shader level below 30 selects the labelled diagnostic fallback: zero gas ray steps and no gas. Bodies, waveform and the free camera remain available through the launcher. `GasEnabled` is also checked against actual shader availability by the Unity integration. The sky has its own shader-availability check and reports a solid-background diagnostic if it cannot render.

The estimate is `32 * width * height + 512 * stars + 512 * tracers` bytes. It reserves a conservative allowance for scene color/depth/working targets and mesh/source-array payload. It excludes the Unity engine, loaded event data, general scene/UI, operating system, driver bookkeeping and allocations elsewhere in the player. It is **not** a measurement of total memory or a guarantee against allocation failure. M4 creates no time-history cache or 3D field textures for this reduced renderer.

## Synthetic stars and physical interpretation

`SyntheticSkyCatalog` is a deterministic synthetic population with seed 150914. It is not a catalogue of measured stars near GW150914, an observer reconstruction at the source, or a simulation of the Milky Way. The first 1,024 source values are identical in every profile; higher profiles add successively fainter sources without relocating, brightening or recoloring existing sources. The chosen cutoff is disclosed in the sky status text.

For source index `i`, apparent magnitude is prescribed as

`m_i = -1.46 + (5/3) log10(1 + 12 i)`.

The normalization and source count are an illustrative population choice. Its familiar relative-flux relation is

`F_i / F_(m=0) = 10^(-0.4 m_i)`.

This logarithmic definition gives a factor of 100 for a five-magnitude difference. [NASA's stellar magnitude teaching material](https://www.nasa.gov/wp-content/uploads/2013/05/752218main_exploring_stars.pdf) supplies the magnitude/brightness relation. Apparent brightness is not interpreted as distance: an intrinsically luminous distant star can appear brighter than a closer faint one. The exhibit deliberately renders no nearby star spheres or invented stellar distances.

Independent seeded uniform variables generate `y = 2u - 1`, `phi = 2 pi v` and direction `(sqrt(1-y^2) cos(phi), y, sqrt(1-y^2) sin(phi))`, which sample solid angle uniformly. The vertex shader uses the rotational part of the view matrix only. Translating the camera within the binary scene therefore produces no artificial parallax. Rotation changes the visible directions normally. No random animation, atmospheric twinkle or time-dependent source brightness is applied. The sky is unlensed in this reduced rendering path; M3 remains the separate Schwarzschild optical control.

Each source receives a synthetic temperature from 2,800 to 12,000 K. A gentle color cue uses the Planck function sampled at 610, 545 and 460 nm, divided channel-by-channel by the corresponding 6,500 K value. For a fixed wavelength the common numerator cancels, leaving

`R(lambda,T) = [exp(hc/(lambda k 6500))-1] / [exp(hc/(lambda k T))-1]`.

The three values are normalized to linear RGB luminance weights `(0.2126, 0.7152, 0.0722)`, then mixed 60% with those values and 40% white. The luminance remains one, so color does not alter the assigned relative flux. This narrow-band color cue is not spectral photometry, a stellar atmosphere calculation or a measured distribution. See the [CIE definition of Planck's law](https://cie.co.at/eilvterm/17-24-005) and [NIST radiance-temperature calibration reference](https://nvlpubs.nist.gov/nistpubs/Legacy/SP/nistspecialpublication250-43.pdf) for the physical spectral-radiance relation.

## Point-spread function and resolution

Larger apparent sources are optical/display point-spread functions (PSFs), not physically resolved star surfaces. At render height `H` their Gaussian width is

`sigma_px = max(0.65, [0.65 + 0.55 F^(1/4)] H / 900)`.

The 0.65-pixel floor broadens undersampled points. Each star has the same integrated flux before display transformations even when its PSF covers more pixels. The shader integrates a separable Gaussian over each pixel rather than evaluating a glow at the pixel center. It clips each axis at `+/-3 sigma`, divides by `erf(3/sqrt(2))^2`, and computes pixel weights from differences of `erf` at pixel edges. The quad includes a half-pixel border so partial edge pixels retain their contribution. The erf approximation is shared with a portable reference and checked using complete pixel-lattice sums for multiple subpixel phases.

The total linear pixel sum for one source is `12 F (H/900)^2`; division by the resolution-area factor recovers the same relative angular flux. The factor 12 is an explicit illustrative exposure, not a detector calibration. Brightness is not renormalized by the number of visible stars.

The Unity scene uses a linear `ARGBHalf` render target when the platform supports it, with HDR allowed on the scene camera. The sky contributes linear radiance first; the volume blends its own premultiplied radiance with `One, OneMinusSrcAlpha`, so gas attenuation acts on the background before display mapping. `ExhibitDisplay` then applies `1 - exp(-radiance)` once when presenting the scene beneath the full-resolution interface. This avoids mapping individual layers before absorption. HDR target color/depth payload is estimated at 12 bytes per pixel (8 for color plus a conservative 4 for depth); the larger budget allowance above also covers working surfaces.

If half-float render targets are unsupported, the renderer uses linear `ARGB32`, estimated at 8 bytes per pixel including depth. This compatibility path can clip bright star or gas radiance before the final mapping. Flux normalization applies to the linear shader contribution and the unclipped HDR target; the final nonlinear exposure map and display gamma are intentionally not flux-conserving. Very low resolution necessarily changes the visible PSF and faint-source visibility. M4 does not claim photometric detector calibration, bloom integration, or a native mobile rendering audit.

## Allocation and evidence boundary

The Unity sky creates one batched mesh and one material, not one GameObject per source. A source uses four vertices (position/direction, UV, float RGBA) and six UInt16 indices: 156 bytes of uploaded attribute/index payload, excluding object overhead. The renderer calls `UploadMeshData(true)` and retains no readable CPU mesh or catalogue array after construction. Shader directions are immutable; the per-frame update changes only render dimensions. Reinitialization disables the old batch before destroying its mesh/material, preventing an overlapping bright frame.

`ExhibitQualitySkyTests.Run()` checks explicit/Auto/unsupported capability decisions; budgets and aspect caps; deterministic replay; immutable bright prefixes; unit directions and population moments; constant color luminance; absence of translation parallax in the reference contract; and integrated PSF flux across six image heights, four fluxes and three subpixel phases. The latter is an independent discrete integration check rather than one formula compared with itself. These portable checks do not prove shader rendering, UI behavior, sustained performance or device compatibility. The M4 player validation report records the actual Unity integration evidence separately.

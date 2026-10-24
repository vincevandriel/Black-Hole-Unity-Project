# Evolved mini-disk numerics

The default gas is an **experimental, evolving Newtonian fluid demonstration**. It solves a specified PDE rather than moving a prescribed density pattern, but its binary morphology is not converged and its strong-field speeds exceed its physical domain. It is not a quantitative relativistic merger prediction or a reconstruction of measured GW150914 gas. M5/M6 relativistic-fluid requirements remain open.

## Shipped cache

| Quantity | Selected value |
| --- | --- |
| Model | `finite-minidisks-newtonian-muscl-ssprk2-v1` |
| Binary forcing | Representative `SXS-BBH-0305-Lev6-N2-v1` coordinate tracks |
| Bundled binary source SHA-256 | `67345f1a8ecb2301890e67f3856dbb78170e0740ee2838021e3acdbe8b2657a4` |
| Asset | `BinaryBlackHole/Assets/StreamingAssets/DTET/MiniDisks/sxs0305.mdf` |
| Asset SHA-256 | `bcef6b6c72c268ee9e47dcb1bc652841cd98472629964f4356851d47c8a01edc` |
| Asset bytes | 65,024,677 |
| Spatial grid | 192 x 192 cell averages; square half extent 12 total-mass rg; dx = 0.125 rg |
| Gas coverage | 0.6815956577960021 to 1.2443464499042842 source seconds |
| Coverage relative to common horizon | -1000 to +771.3601699902 tg |
| Stored frames | 513; uniform 0.001099122640836489 s = 3.459687832012078 tg |
| Initial radial annulus width | 17.1077 / 16.7834 cells for A / B |
| Reader array payload | 460,639 bytes, excluding objects, two player frames and GPU textures |

The source calculation wrote 1,025 frames (129,984,073 bytes). To stay below GitHub's ordinary file-size limit, the selected cache copies **exactly every second compressed frame**, including endpoints and full-double diagnostics. All 513 selected compressed payloads and diagnostics were checked against the original; there is no PDE rerun or additional quantization. The original remains in validation outputs, outside the player and source repository. The selected cadence is an interpolation/display approximation, not a time-resolution convergence result.

The reader retains the index and fixed raw/compressed scratch buffers. Playback uploads nearby frames into two textures. It does not decompress the entire history into memory. Render quality changes rendering work rather than this cache's PDE resolution.

## Physical and numerical contract

The solver advances height-integrated surface density and planar momentum using inviscid isothermal Euler equations, MUSCL primitive reconstruction, Rusanov fluxes, SSPRK2 transport and symmetric frozen-midpoint gravity/sink source splitting. Position uses total-mass rg = GM/c² and time uses tg = GM/c³. Surface density and reported gas mass have an arbitrary common normalization; they are not solar masses or a measured accretion rate.

Two finite prograde annuli are initialized once at the declared gas start. Their density tapers smoothly to zero excess at 1.3 host equal-area horizon radii and 0.8 Eggleton Roche-lobe radii. Initial velocities combine host coordinate velocity with softened Newtonian circular velocity. A disclosed 10% m=2 density disturbance is part of the initial condition and then evolves under the PDE. This is not exact binary or pressure equilibrium; an initial transient is expected. No pre-relaxation, imposed tubes, continually reset patterns, reservoir or replenishment is used.

| Setting | Value |
| --- | --- |
| Isothermal sound speed | 0.04 c |
| Density floor | 1e-7 |
| Plummer softening | 0.5 host equal-area horizon radius |
| Sink radius | 1.1 host equal-area horizon radius |
| Sink e-folding parameter | 4 tg, multiplied by the smooth radial sink profile |
| Boundary | Outflow only, no external mass input |

The sink removes the corresponding momentum with its removed mass. The floor contribution and boundary flux are separately included in the mass ledger. At common-horizon formation, one softened remnant source replaces the two binary sources. This is a declared Newtonian forcing switch, not an Einstein-equation solution or a relativistic spacetime. Gas self-gravity/backreaction, physical viscosity, magnetic fields, energy evolution and radiation transfer are absent. The inner annulus and sink radii do not establish relativistic stable orbits. Vertical reconstruction, opacity, emission and the bounded Doppler display are separate assumed rendering closures.

Replay uses the compact 12 rg half domain only when both initialized disk axis extents lie within 0.85 of that half width; otherwise the half width is 18 rg. Adjustable scenarios use 18 rg. The open computational boundary can remove gas and supplies none from an unresolved outer disk.

## Executed checks

The 192² source run completed 123,455 steps in 847.95 s on the development PC under .NET. Its largest measured Courant number was 0.19952078, minimum density was 1e-7 and no reconstruction fallback occurred. Generation working-array payload was 6,258,688 bytes, excluding runtime/object overhead. This timing is not a laptop, Unity Mono, VR or frame-rate benchmark.

At the final source epoch, the double-precision ledger was:

`M0=33.1522830503; M=1.45685379388; sink=28.1077554864; outflow=3.59006418510; floor=0.002390415008`.

The final residual `M - (M0 - sink - outflow + floor)` was 5.876e-11. Across selected frames, the maximum absolute residual divided by initial mass was 2.492e-12. This checks accounting; it does not establish the physical validity or spatial convergence of the model.

All 513 selected gzip frames passed CRC, expected byte-count, finite-field and nonnegative-density checks. Planar Sigma/vx/vz channels use IEEE binary16. Density is always retained to that precision; velocities are stored as zero only where the pre-encoding density is at most 1e-6. This is an explicit loss of vacuum velocity information. Maximum selected mass-integral error against full-double diagnostics was 1.5333e-5 relative. The original 1,025-frame writer measured a maximum dense-velocity quantization error of 6.887e-4 c.

Selected density evolution demonstrates retained material, without normalizing brightness or restoring drained mass:

| Time relative to common horizon (tg) | M / M0 | Peak Sigma |
| --- | ---: | ---: |
| -1000 | 1.0000 | 1.0996 |
| -557.160 | 0.78629 | 1.0244 |
| -114.320 | 0.42090 | 1.1514 |
| -0.1502 | 0.17988 | 1.7480 |
| +328.520 | 0.04607 | 0.022781 |
| +771.360 | 0.04394 | 0.019104 |

The PDE's largest all-cell speed was **2.1083 c**. Among selected frames, the largest dense-cell speed was 1.8168 c; the largest fraction of remaining mass with Newtonian speed above c was **4.2111%**, at source time 0.9981429784 s. At the tabulated near-merger frame that fraction was 2.6617%. These are failures of the Newtonian model's physical applicability, not superluminal physical predictions. The shader's separate Doppler speed bound is a numerical rendering safeguard and does not repair the fluid solution.

The standard portable simulation suite passed. The separate mini-disk model/cache suite passed 28 assertions covering the finite prograde seed, actual evolution, ledger/CFL, IEEE binary16 reference values, backward seeks, coverage, corruption/truncation rejection, cancellation preservation and exact temporal subsampling. Graphics and application checks are recorded separately; these numerical checks do not establish usability or predictive physics.

## Refinement evidence: not converged

The 128² and 192² full late-window calculations use the same physical 12 rg half domain, initial condition and source clock. Comparison conservatively area-averages fine-grid density and momentum onto coarse cells, including the 3:2 grid ratio. The relative density L1 denominator is the summed restricted fine density. Stored double-precision time steps vary with each run's output boundaries; this is a practical spatial-resolution comparison, not a proof isolating every truncation error.

| Comparison epoch relative to common horizon (tg) | Relative density L1 | Relative mass difference |
| --- | ---: | ---: |
| -1000 | 0.01927 | 0.0000140 |
| -557.160 | 0.40412 | 0.19670 |
| -114.320 | 0.74813 | 0.53485 |
| +328.520 | 0.78813 | 0.76223 |
| +771.360 | 0.78899 | 0.76702 |

A separate matched 128² to 256² calculation covers only the first 200 tg after gas initialization in the same 12 rg half domain. Density L1 at elapsed 0/50/100/150/200 tg was 0.00574/0.26450/0.38443/0.40856/0.39415. At 200 tg the mass difference was 10.5995%; retained mass was 86.6876% versus 96.9627%. This short test does not validate merger morphology or the full 192² history. Numerical diffusion and unresolved transport remain material. No density-convergence, accurate accretion-rate, real plasma or GRMHD claim is supported.

## Why the gas starts late

An earlier full-retained-history 128², half-18-rg finite-disk run lost most of its inventory before the merger: M/M0 was 0.393 at 979 tg, 0.0765 at 1958 tg, 0.000351 at 2938 tg and 0.0000268 at the end. It was rejected for the intended demonstration. The default now assumes a newly initialized finite gas packet 1000 tg before common-horizon formation. This is a different, explicit initial-condition experiment. It does not reconstruct earlier gas or imply that real GW150914 contained these disks.

The full black-hole source clock remains available; gas is unavailable outside its own cache interval. There is no fabricated earlier fill or post-merger replenishment. A physically supplied long-lived disk would require a resolved outer reservoir/inflow model and stronger fluid/metric validation.

## Reproduction and evidence

Run from the repository root:

```powershell
dotnet run --project Staging/MiniDisk.Exporter/MiniDisk.Exporter.csproj -c Release -- --self-test
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
dotnet run --project Staging/MiniDisk.Exporter/MiniDisk.Exporter.csproj -c Release -- --grid 192 --frames 1025 --output <output-path>
```

The exporter also supports `--lead-tg`, `--run-tg`, `--inspect`, `--compare`, and exact `--decimate <source> --stride 2 --output <destination>`. `--m1 40 --m2 25 --separation 35` selects a generated adjustable scenario. The executed adjusted 128² case completed in 119.44 s under .NET with finite output, 40,800 steps, Courant maximum 0.19788 and ledger residual -1.337e-11; it is a different, lower-resolution experiment.

Raw reports remain outside the repository/player in the research workspace's `outputs/Evolved-disks-validation/`: `final-cache-generation-receipt.json` records exact source/selected hashes, source frame mapping and generator assembly hashes; `late-full-128-vs192.log` and `late-pilot-128-vs256.log` contain refinement values; `final-cache-audit.log` records every-frame checks; `minidisk-portable-tests.log` and `portable-suite.log` retain test evidence. The source generation report and selected-frame inspection retain full numerical diagnostics. No validation caches, tools, recordings or raw reports belong in the player ZIP.

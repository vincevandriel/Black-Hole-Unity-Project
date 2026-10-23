# Generated planar fluid history

This mode solves a fluid PDE locally and caches its output. It is a **Newtonian, two-dimensional, isothermal educational surrogate**, driven by prescribed binary coordinates. It is not GRHD, GRMHD, a physical bright-disk reconstruction of GW150914, a self-consistent gas/binary evolution, or a replacement for the compatible relativistic-fluid and metric-data gates in [FINAL_DESKTOP_SCOPE.md](FINAL_DESKTOP_SCOPE.md). No private dataset or external solver service is needed to generate this particular model.

`GeneratedFluidSolver.cs` evolves conservative surface mass and planar momentum. `GeneratedFluidHistory.cs` prepares the assumed initial condition, evaluates the same `InteractiveBinaryScenario` timeline used by the bodies, and returns an immutable history. The model identifier is `generated-isothermal-newtonian-muscl-ssprk2-v1`.

## Units and equations

Coordinates are Unity's orbital XZ plane, in the scenario's total-mass gravitational radius `rg=GMtotal/c²`. Time is in `tg=GMtotal/c³`, so velocity in `rg/tg` equals velocity divided by `c`. The source masses are fractions of that same total mass. Surface density `Sigma` is in an arbitrary normalization `Sigma0`: no gas mass in kilograms, accretion rate in kilograms per second, temperature in kelvin, optical depth calibration, or luminosity in watts is inferred. A dimensionless mass integral is `sum(Sigma*dx²)`.

The evolved state and isothermal pressure are

```
U = (Sigma, Sigma*vx, Sigma*vz),       P = cs² Sigma,       cs = 0.04.
F_x = (Sigma*vx, Sigma*vx²+P, Sigma*vx*vz)
F_z = (Sigma*vz, Sigma*vx*vz, Sigma*vz²+P).
```

Ignoring the separately recorded numerical floor, the PDE is

```
dSigma/dt + div(Sigma v) = -s (Sigma - Sigma_floor)
d(Sigma v)/dt + div(Sigma v v + P I)
    = Sigma g - s (Sigma - Sigma_floor) v.
```

Here `Sigma_floor=1e-7`. The positive sink removes gas and its proportional momentum while preserving the remaining cell's velocity. Its removal is not added to the prescribed black-hole masses. Gas self-gravity and gas backreaction on the binary are absent. There is no energy equation, physical viscosity, magnetic field, resistivity, radiation cooling/heating, relativistic stress tensor, or radiation-transfer solution. The isothermal closure imposes the pressure law; it does not calculate the thermodynamics of an accretion disk.

For each prescribed source `b`, with planar position `xb`, mass fraction `mu_b`, and `epsilon=2 rg`,

```
Phi_b = -mu_b / sqrt(|x-xb|² + epsilon²)
g_b = -mu_b (x-xb) / (|x-xb|² + epsilon²)^(3/2).
```

The sink rate is the sum of compact smooth rates

```
s_b = (1-r_b²/rsink²)² / tsink   if r_b < rsink; otherwise 0
rsink = 3 rg;                    tsink = 4 tg.
```

These fixed lengths are independent of grid resolution and rendering quality. They regularize unresolved inner flow; they are not horizon radii or calibrated relativistic accretion boundaries. The exact global acceleration bound used for timestep admission is `sum(2 mu_b/(3 sqrt(3) epsilon²))`.

For replay, the source positions/masses come from the retained SXS tracks at each integration midpoint, projected into XZ; the vertical coordinate is omitted. Using coordinate tracks in a Newtonian potential is an explicitly approximate forcing prescription, not a coordinate transformation of a relativistic fluid solution. The common-horizon epoch replaces the binary sources with the scenario's remnant source and can change the forcing discontinuously. For adjustable experiments the source follows the bounded inspiral and its labeled conceptual merger/remnant. The gas model does not make that conceptual segment quantitatively relativistic. No waveform is synthesized by this gas solver.

## Assumed initial condition and limited coverage

Only the final `min(4000 tg, scenario duration)` is generated. The initial ring is freshly initialized at that interval's absolute starting epoch. The missing early gas history is not extrapolated or silently represented by this ring. The current SXS record spans approximately `3916.80611 tg`, so this bound includes that retained scene timeline.

Let `a=max(8, separationRg at cache start)`, `r0=2.1a`, `w=0.35a`. The square domain has half-extent `max(32,3.5a)` in rg. The initial state is

```
Sigma_ring = exp[-0.5 ((r-r0)/w)²]
Sigma = Sigma_floor + Sigma_ring
vphi² = max(0, r²/(r²+epsilon²)^(3/2) - cs² r(r-r0)/w²)
vphi = sqrt(vphi²) * Sigma_ring / Sigma
(vx,vz) = vphi*(-z/r,x/r), with zero velocity at r=0.
```

The rotation includes the Gaussian ring's pressure-gradient term and softened monopole attraction. It is an assumed prograde ring with stationary floor atmosphere, not exact binary equilibrium or a measured reservoir. No minidisk is painted into the initial data: the small sink/softening scales do not justify resolving physical inner minidisks on the default grid. Any streams, clumps and local concentrations in the later field arise from the numerical PDE under these assumptions; their appearance alone is not evidence of physical accuracy.

## Conservative numerical method

The square mesh is fixed at **96 by 96 cells** in the interactive default, with **129 cached epochs**. Rendering profiles do not change that physical solution. A separate settings overload accepts bounded resolutions for verification; it is not tied to the Laptop/Balanced/High renderer setting.

The solver uses componentwise MUSCL reconstruction of the primitive variables `(Sigma,vx,vz)`. In each direction the slope is

```
minmod(1.5*(q_i-q_left), 0.5*(q_right-q_left), 1.5*(q_right-q_i)).
```

The two density slopes are reduced together when necessary so that
`Sigma - 0.5*(|slopeX|+|slopeZ|) >= Sigma_floor`. Reconstructed face primitive variables are converted to conserved quantities. A Rusanov/local Lax–Friedrichs flux is applied at each face, with `alpha=max(|vnormal,left|,|vnormal,right|)+cs`:

```
Fface = 0.5*(Fleft+Fright) - 0.5*alpha*(Uright-Uleft).
```

Each internal face updates its two neighboring cells with equal and opposite fluxes. Both directions are updated in the same transport operator. Time integration is SSPRK2:

```
U1 = U0 + dt L(U0)
Unew = 0.5 U0 + 0.5 [U1 + dt L(U1)].
```

Midpoint source forcing is applied with symmetric half-steps around this transport update. The source map integrates `dv/dt=g` and exponential relaxation of `Sigma-Sigma_floor` exactly for a frozen midpoint source. This is a second-order strategy in smooth regions with smooth forcing. Limiting, the open edge, source switching, shocks, and the numerical floor reduce that order locally. It is not a proof of second-order convergence of the complete binary exhibit.

Timestep selection uses the positive root of
`dt*(sx+sz+2*gBound*dt) <= 0.20*dx`, where `sx,sz` are the global directional maxima of `|v|+cs`, plus a maximum step of `2 tg` and the next stored-epoch boundary. Every SSPRK stage measures its actual Courant number. Stages above `0.24` use first-order paired fluxes; a stage above `0.35` rejects generation. A nonpositive/nonfinite reconstructed update is recomputed as a **whole stage** with first-order paired fluxes, and a failed fallback rejects generation. No individual negative mass or velocity is clipped. `ReconstructionFallbackCount` records these conservative stage fallbacks.

Positive density below the declared floor is lifted to the floor while retaining velocity; the added mass is recorded. Both stage floor additions and boundary fluxes carry the correct one-half SSPRK weight. The source half-steps have their full physical weights. The final mass ledger is

```
Mfinal = Minitial - Msinks - Moutflow + Mfloor + residual.
```

Outflow boundaries impose no external mass inflow. Their face-normal velocity is clamped to its outward part, tangential velocity is retained, and normal pressure remains. This is an open/diode boundary prescription, not an infinite reservoir or perfectly nonreflecting condition. Periodic boundaries are available only in the standalone solver for verification problems.

The numerical method follows standard reconstruction, flux and SSP time-discretization ideas, independently implemented here. [Ammar Hakim's author note](https://www.ammar-hakim.org/hancock-muscl.html) documents primitive reconstruction, minmod limiting, positivity precautions and the Rusanov flux. This implementation uses SSPRK2 rather than that note's Hancock predictor. [Gottlieb, Shu and Tadmor's original SSP paper](https://doi.org/10.1137/S003614450036757X) supplies the convex-combination time-integration framework. [Jordan and Rometsch's binary-disk methods study](https://www.aanda.org/articles/aa/pdf/2025/01/aa50383-24.pdf) is context for how density floors, softened forces and source treatment affect binary-disk calculations, not a validation of this exhibit's parameters or relativistic applicability.

## Cache, traversal and renderer contract

`Generate(scenario, cancellationToken, progress)` is synchronous pure C# and may run on `Task.Run`. It calls no Unity API. Cancellation is checked before generation, between finite-volume steps, at frame boundaries, and before publication. Progress is monotone on `[0,1]`; its callback runs on the generating thread. The caller must discard stale results after a scenario-generation change.

The returned history owns three float arrays: surface density, `vx`, and `vz` at every cached epoch. Conservative evolution and diagnostics use doubles; primitives are converted to float only for the display cache. `CopyFrame` writes into caller-owned arrays and cannot mutate the history. The layout is `index=z*GridSize+x`, at cell centers `-HalfExtentRg+(i+0.5)*CellWidthRg`. `FrameTimes` and `FrameDiagnostics` use read-only wrappers.

`TryGetFrames` and `TrySample` reject times outside `[StartSeconds,EndSeconds]`; they never replace missing coverage with a static field. Within the physical square, spatial interpolation is bilinear, clamped at the edge cell centers, matching a clamped texture with `uv=(position+HalfExtentRg)/(2*HalfExtentRg)`. Points beyond that square are uncovered. Time interpolation is linear in the three stored primitive channels. Interpolated display frames are not additional conservative PDE steps. Rewind and arbitrary camera traversal sample the same immutable history; they do not rerun the gas or depend on wall-clock time.

The 129 snapshots are approximately `30.60 tg` apart for the retained SXS interval, and at most `31.25 tg` apart for the default 4000-tg bound. The conservative integrator takes far smaller steps internally. A fast near-body feature can cross many grid cells between snapshots: smooth interpolation does not recover missing high-frequency evolution. This cache has no demonstrated temporal image-error or radiative-transfer accuracy bound. More stored frames or a separately validated temporal reconstruction would be needed before making such claims.

For volume display only, the surface density is extruded with a fixed Gaussian:

```
rho_display(x,y,z) = Sigma(x,z) exp[-y²/(2 H²)] / (sqrt(2 pi)*H), H=2 rg.
```

Its vertical integral equals `Sigma`. This prescribed height is not a solved vertical hydrostatic structure, and is not derived from the sound speed. There is no vertical gas velocity. Brightness proportional to this reconstructed density is a display proxy, not calibrated emissivity, temperature, observed spectrum, or luminosity. A user can inspect this geometry from multiple viewpoints, but that does not turn the planar evolution into 3D hydrodynamics.

The default cache array payload is **14,277,720 bytes** (three float histories, frame times, and frame diagnostics). The peak generator array payload is **15,604,824 bytes**, including 18 working double grids. These counts exclude object headers, the existing scenario, textures, renderer buffers, managed runtime, and process/driver overhead. A profiler/process measurement is still required for device memory claims.

## Verification and numerical limitations

`GeneratedFluidTests.cs` checks uniform-state preservation; periodic conservative budgets; an exact advected transverse-velocity wave; a two-dimensional diagonal transverse wave; small-amplitude sound propagation against the independent linearized solution; and a nonlinear isothermal Riemann problem. For the latter, an independent root solve finds the star density from `ln(rho*/4)+(rho*-1)/sqrt(rho*)=0`, giving `rho*=1.98649322`. The code compares the rarefaction, star state and shock against this solution.

Other tests cover analytic softened-force/sink fixtures, actual stage CFL, positive density, the separate sink/outflow/floor mass ledgers, cancellation, immutable copies, explicit coverage, independently calculated cell-center/bilinear samples, deterministic regeneration/seek, and vertical quadrature of the display closure. Both SXS replay and an unequal-mass educational scenario are exercised.

The current smooth-fixture normalized L1 errors are approximately:

| Test | Coarse | Double resolution | Further doubling |
| --- | ---: | ---: | ---: |
| Transverse advection, 32/64 | 0.009675 | 0.002469 | — |
| Linearized sound, 32/64/128 | 0.009302 | 0.002232 | 0.000558 |
| Isothermal shock tube, 64/128 | 0.029574 | 0.015038 | — |
| Diagonal transverse advection, 24/48 | 0.020529 | 0.005392 | — |

The short unequal-mass generated-history comparison at 24/48 cells has about **0.0851%** final integrated-mass difference. This does not establish convergence of the longer binary morphology or near-body accretion rate. The initial first-order method was rejected after a full 96/192 audit found about 68% final density disagreement and excessive disk depletion; it is not the delivered default method.

The default SXS solve currently takes about **31 seconds in the local .NET verification process**, with 19,541 steps and maximum stage Courant number about 0.196. This is not Unity-device timing or a laptop guarantee. Its initial dimensionless mass is about 2410.78 and its final mass about 835.67, with roughly 103.29 removed by sinks and 1471.82 leaving the finite square. Floor addition is approximately `2.55e-5`; the ledger residual is about `-3.6e-11`. Final density contrast remains substantial (peak/mean approximately 5.73). These diagnostics are model outputs, not observations of a black-hole system.

Some simulated speeds reach approximately **0.72c**, despite the Newtonian equations. Softening and stability do not make that regime physically Newtonian. Velocities are reported rather than silently clipped. Strong-field trajectories, relativistic pressure/energy, magnetic angular-momentum transport, calibrated emission and accurate merger gas require a separately validated GRHD/GRMHD calculation. This generated model therefore demonstrates a real, testable reduced fluid PDE and its numerical limitations; it does not close the physical M5/M6 science gates.

### Full default versus doubled-grid audit

Both runs use the same retained SXS input, initial ring formula, physical domain, softening, sink law, sound speed, 129 stored epochs and complete `3916.80611 tg` interval. Only the spatial grid changes. Source runtime was frozen at commit `6cc36ad86036fbc0e65b3dd8e22b61f8c68fcb70` for this comparison. The following values come from the local .NET audit, not a physical hardware or Unity performance certification.

| Observable | 96 by 96 | 192 by 192 |
| --- | ---: | ---: |
| Initial mass | 2410.781868 | 2410.781144 |
| Midpoint mass | 1953.840912 | 1809.708153 |
| Final mass | 835.667817 | 694.001933 |
| Final fraction of initial mass | 34.66% | 28.79% |
| Midpoint maximum surface density | 0.707286 | 0.720308 |
| Final maximum surface density | 0.469219 | 0.349794 |
| Final density-weighted mean radius, rg | 33.39443 | 33.92069 |
| Final planar angular momentum integral | 4192.15437 | 3535.11498 |
| Final removed sink mass | 103.292345 | 116.276235 |
| Final net outflow mass | 1471.821731 | 1600.503014 |
| Added floor mass | 0.0000254663 | 0.0000380061 |
| Final mass-ledger residual | -3.6e-11 | -7.0e-11 |
| Integration steps | 19,541 | 38,680 |
| Local .NET generation time | 32.85 s | 256.73 s |

The final integrated-mass difference is **20.4%** relative to the doubled grid, while the final mean-radius difference is about **1.55%**. For a direct spatial field comparison, sample both cached density fields at the 96-grid cell centers at the same absolute epoch and calculate `sum(abs(Sigma96-Sigma192))/sum(Sigma192)`. This relative sampled L1 difference is **36.25% at the midpoint and 63.14% at the end**. It is not a norm of error against an exact solution; it is evidence of substantial unresolved resolution sensitivity. The detailed binary gas morphology, mass loss and accretion rate are therefore **not resolution-converged at the default grid**. The improved scheme retains a useful structured field and passes independent PDE fixtures, but is still a coarse educational simulation.

At the midpoint/final epoch, respectively, 99.96%/99.99% of the 96-grid mass and 99.87%/99.92% of the 192-grid mass lie more than `6 rg` from every active source center. The diagnostic is the density-weighted cell mass outside those planar circles; it is not a proper-distance or relativistic validity test. Among cells above `Sigma=1e-5` outside those circles, speeds still reach approximately `0.425c` in the audit. Merely excluding the sinks does not make the remaining flow a calibrated Newtonian approximation to GW150914.

Reproduction uses `GeneratedFluidHistory.Generate` twice with `new GeneratedFluidSettings(96,129,4000)` and `(192,129,4000)` on a scenario loaded from the retained `SXS0305.bytes`. The input SHA-256 is recorded in [the SXS provenance](Data/SXS0305/provenance.json). Compare frames 0, 64 and 128 using `CopyFrame` and each history's exact cell centers and area; the two half-extents must agree. The fine run is deliberately an offline audit, not a selectable high-fidelity runtime profile. A third refinement, temporal-cache refinement, domain/boundary study, inner-source prescription study and independent solver comparison are still needed for a numerical-accuracy claim about this forced binary setup.

The repository includes the [standalone audit runner and run instructions](../Staging/Fluid.RefinementAudit/README.md), with the exact already-executed `Program.cs` and a relative project reference. [The retained results and provenance](Data/GeneratedFluid/README.md) contain the complete captured stdout, parsed printed metrics, pinned runtime commit, input hash and exact program/evidence hashes. This source-controlled project was built without rerunning the expensive fine calculation. The external research workspace additionally preserves the original local project record and full manifest under `outputs/references/GW150914/generated-fluid/`; personal paths are omitted from the repository evidence.

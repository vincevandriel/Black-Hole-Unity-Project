# Evolved finite mini-disks: model and evidence boundaries

This mode supplies a locally generated, recorded gas evolution. The initial inventory consists of assumed disks around the two black holes. Their subsequent density and planar velocity evolve under a fluid PDE and the prescribed binary forcing; their streams and non-axisymmetric evolution must not be drawn by a rotating-arm formula. The intended default is a streamed cache prepared offline, so laptop playback does not repeat the fluid calculation.

This is a **Newtonian, two-dimensional, inviscid, isothermal educational model**, with softened gravity and compact accretion sinks. It is not GRHD/GRMHD, a measured accretion environment for GW150914, a coupled gas/binary simulation, or a validated prediction of the final strong-field merger. No external fluid dataset is needed for this reduced calculation. Its binary coordinates and linked radiation retain their separate numerical-event provenance.

The default finite gas inventory is initialized late: `t_start=max(0,t_commonAH-1000 tg)`, with cache coverage continuing to the retained source-history end. This is an explicitly assumed late initial condition, not gas evolved from the first SXS sample or an earlier steady accretion solution. The entire scientific body/waveform timeline remains available and unchanged. Before the gas initialization time, hide this gas and identify its missing coverage; the accretion-view control seeks the actual initialization epoch. The movie samples the **available gas interval**, not an invented extension over the earlier source history.

## What evolves

In total-mass units `rg=GM/c²` and `tg=GM/c³`, the evolved conservative quantities are surface density and planar momentum:

```
U = (Sigma, Sigma*vx, Sigma*vz),       P = cs²*Sigma.

dSigma/dt + div(Sigma v) = -s*(Sigma - Sigma_floor)
d(Sigma v)/dt + div(Sigma v v + P I)
    = Sigma*g - s*(Sigma - Sigma_floor)*v.

Phi_i = -mu_i/sqrt(|x-x_i|² + epsilon_i²)
g_i = -mu_i*(x-x_i)/(|x-x_i|² + epsilon_i²)^(3/2).
```

The remaining gas keeps its velocity when the sink removes mass and proportional momentum. Gas mass is not added to the prescribed hole masses. Open boundaries remove outflow, the finite initial inventory is not continuously replenished by a painted stream, and any density-floor addition belongs in the conservation ledger. There is no physical viscosity or magnetic stress. Shocks/tides can redistribute momentum, but numerical diffusion also transports angular momentum; a visually plausible accretion rate is not a resolution-converged physical prediction.

The existing finite-volume method uses limited reconstruction, Rusanov fluxes and SSPRK2. The generator must admit timesteps using both fluid-wave speeds and the force bound, retain positivity/fallback diagnostics, and export actual evolved states. See [the solver description](GENERATED_FLUID_MODEL.md) for the numerical algorithm; that older mode's broad ring, 96-cell grid, softening, sinks and coverage are not this mode's initial conditions or cache specification.

## Initial rotating disks

Construct compact prograde surface-density profiles relative to each moving source. A useful isolated softened radial-balance initializer is

```
v_phi² = mu_i*r²/(r²+epsilon_i²)^(3/2) + (r/Sigma)*dP/dr
v_world = v_body + v_phi*e_phi.
```

It is only an initializer: companion tides, the accelerating source frame and the finite taper mean it is not exact binary equilibrium. Smooth inner/outer tapers and any seeded azimuthal perturbation are explicit assumptions. A taper where the pressure term would demand negative `v_phi²` needs a documented non-equilibrium treatment; numerical clipping must not be described as a solved equilibrium.

The delivered initializer deliberately uses the **softened-gravity term only**, adding the source velocity; it does not implement the pressure-gradient term above or a pre-relaxation solve. With `R_h` the scenario's coordinate horizon-radius proxy, its seed annulus is `1.3 R_h < r < 0.8 R_Roche`, where the Roche-lobe estimate is computed for the source mass ratio and initial separation. Within that annulus, `u=(r-r_inner)/(r_outer-r_inner)` and

```
Sigma_seed = 16*u²*(1-u)² * [1 + 0.1*cos(2*phi + phase_i)]
phase_A = 0, phase_B = pi/3.
```

The ten-percent disturbance is an assumed initial non-axisymmetric density perturbation, not measured turbulence. The floor atmosphere is initially stationary, and overlapping contributions combine by density-weighted momentum. The constants are `cs=0.04`, `Sigma_floor=1e-7`, `epsilon=0.5 R_h`, sink radius `1.1 R_h`, and sink time `4 tg`. Pressure and tides immediately drive relaxation/transients; these must not be presented as an initially steady accretion solution. A compact replay square of half-extent `12 rg` is selected when the initial disks fit within its safety margin; otherwise the model uses half-extent `18 rg`, including adjustable examples. Read the actual physical grid and stored cadence from each cache's metadata. They are independent of the rendering preset.

Moving the open edge inward changes the physical initial/boundary-value problem as well as the available cells across a disk. A 128-cell, half-extent-18 pilot and a 192-cell, half-extent-12 run are not a matched resolution-convergence pair. A useful comparison must hold the domain, seed, sources, sinks and coverage fixed while changing spatial resolution, then investigate boundary location and stored-frame cadence separately.

Tidal truncation estimates near `0.3` times binary separation are guides, not a rule imposing permanent circular disk edges. Their validity depends on mass ratio, thermodynamics, separation and coordinate convention. A sink or softening comparable to the disk's surviving width erases its structure. Record cell width, inner/outer seed radii, sink radius, softening, seed disturbance, and cells across the remaining annulus. Compare a finer grid before making claims about detailed morphology, disk survival or accretion.

Softened Newtonian gravity has no physical innermost stable circular orbit. Its sink is not the event horizon or a calibrated Kerr ISCO. Replaying SXS coordinate tracks in this potential is an approximate external forcing prescription, not a coordinate transformation of a relativistic fluid solution. The common-horizon transition can change that forcing. The scientific waveform and body history must remain independent of this assumed gas calculation.

## Appearance, movement and time

Runtime interpolation reads recorded density and velocity at the requested event epoch. The default renders a continuous volume, enriched by a separate passive material-label history transported through that velocity. Its markers are not visible particle sprites, an independent mass solver or additional plasma physics. Density patterns may be nearly stationary while fluid moves through them. Backward playback samples recorded epochs rather than reversing the dissipative/numerically dissipative integration. See [the appearance contract](PLASMA_APPEARANCE.md) for exact transport, reconstruction, emission and convergence limits.

Each cache frame is independently gzip-compressed and stores binary16 surface density and planar velocities. Velocities at `Sigma <= 1e-6` are encoded as zero to avoid giving the near-vacuum atmosphere an optical velocity; this changes the display cache, not the evolved momentum. Record encoded mass and dense-velocity errors against the solver before distribution. Playback retains the index and small decode/texture buffers rather than the full history in RAM.

The displayed vertical Gaussian supplies volume thickness to a planar solution. Its current local scale-height closure uses an assumed small-height isothermal hydrostatic relation in the softened Newtonian potential, with explicit grid/domain floors and caps; vertical momentum, turbulence and gas motion are not solved. The Gaussian remains normalized to the supplied surface density. Brightness/opacity is a display closure without an energy equation, electron model, physical density normalization or calibrated spectrum. The [appearance contract](PLASMA_APPEARANCE.md) also describes optional shared-clock display slow motion. Local Doppler styling and curved-ray rendering have their own implementation and validation receipts. Curving rays around prescribed sources does not establish binary relativistic radiation transfer, a metric-derived event-horizon image or a physical merger light curve.

The Newtonian solver can produce relativistically large or even superluminal numerical velocities near its unresolved, moving sources. Report those values and their represented mass fractions; stability and positivity do not establish relativistic validity. The renderer limits the velocity used for its optical styling to `0.95c`; that display safeguard does not change the stored solution or repair its physics. Gray opacity/exposure choices likewise change appearance, not gas dynamics or physical emissivity.

## Relevant primary research

- [Farris et al. 2014, circumbinary accretion, methods and gas dynamics](https://arxiv.org/html/1310.0492): a two-dimensional pressure/viscosity model produces cavity streams feeding mini-disks. Its relaxed, wide, fixed binary is physical context for evolving fluid rather than prescribing arms; its viscosity, long preparation and parameter choices are not implemented by naming this reduced mode.
- [Farris et al. 2014, inspiral and merger](https://arxiv.org/html/1409.5124): hydrodynamic initial relaxation precedes imposed orbital shrinkage; accretion need not halt abruptly at nominal viscous decoupling. This does not validate Newtonian gas at a relativistic stellar-mass merger.
- [Bowen et al. 2017, relativistic mini-disk dynamics](https://arxiv.org/html/1612.02373v2): seeded disks relax and become tidally distorted; relativistic effects change stream exchange and spiral modes. A Newtonian reduced model omits those effects.
- [Bowen et al. 2018, fed relativistic mini-disks](https://arxiv.org/html/1712.05451): mini-disks can deplete/refill asymmetrically, and their available radial extent shrinks relative to the ISCO. Two permanent identical rings are not an acceptance criterion.

## Required evidence

The selected fluid cache has executed numerical evidence: see [the numerical receipt](MINIDISK_NUMERICS.md). The earlier [application/graphics receipt](MINIDISK_VALIDATION.md) records 43 passing technical records in each Float/Half run for its explicitly identified prior revision. It is historical evidence, not validation of subsequent material-label, height, sampling or timing changes; their separate status is in [the current appearance contract](PLASMA_APPEARANCE.md). These checks do not establish physical convergence, continuous native playback, laptop/headset support or expert approval.

Executed gates include finite positive fields and mass accounting, prograde seed/actual temporal evolution, immutable cache sampling/coverage, preserved scientific bodies/radiation, reverse-seek image reproducibility, bounded playback payloads, multiple viewpoints, a controlled far-side curved/straight emitter and a production-shader Doppler sign fixture. The receipts state the exact configurations and limits. A nonblank screenshot alone proves neither optical effect.

Report each disk's inventory and resolved extent at sampled epochs instead of assuming it survives until merger. Compare spatial and temporal refinements and inner-boundary choices. Any substantial disagreement remains an explicit numerical limitation; none of these checks substitutes for a compatible relativistic-fluid/metric calculation.

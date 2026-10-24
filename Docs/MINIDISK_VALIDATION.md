# Evolved mini-disks: application and graphics validation

24 October 2026. Runtime source: **`7a69524804d57e8cb2ecacb01c263e53b788ed4d`**, Unity **6000.3.25f1**. The executed development-player checks used an **NVIDIA GeForce RTX 3060 Ti**. This receipt records integration, controlled graphics checks and sampled visual inspection. It does not establish a predictive relativistic gas model, a solved binary spacetime, native presentation FPS, laptop/headset compatibility or human usability.

The default now replays evolving finite mini-disks and curves their displayed light, rather than using prescribed rotating gas arms and an independently drawn flat black mask. The [model contract](MINIDISK_DYNAMICS.md) describes that choice; [the numerical receipt](MINIDISK_NUMERICS.md) records the actual fluid equations, source run, quantization, superluminal numerical speeds and substantial unresolved refinement sensitivity. Those physical limitations remain after the graphics checks pass.

## Exact selected data and display

| Item | Tested selection |
| --- | --- |
| Binary source | Representative `SXS-BBH-0305-Lev6-N2-v1`; scientific body/radiation history retained |
| Gas model | `finite-minidisks-newtonian-muscl-ssprk2-v1` |
| Asset | `Assets/StreamingAssets/DTET/MiniDisks/sxs0305.mdf` in the Unity project |
| Asset SHA-256 | `bcef6b6c72c268ee9e47dcb1bc652841cd98472629964f4356851d47c8a01edc` |
| Asset size | 65,024,677 bytes |
| Physical mesh/history | 192 × 192; half-extent 12 total-mass rg; 513 recorded epochs |
| Gas coverage | 0.6815956577960021–1.2443464499042842 seconds on the retained source-frame replay clock |
| Relation to common horizon | −1000 to +771.3601699902 tg |
| Selected cadence | 3.459687832012078 tg |
| Assumed vertical profile | Gaussian height 0.3 rg; planar evolution, no vertical fluid solver |
| Fixed display closure | Gray opacity 8, exposure 1.6; false colour and bounded velocity-dependent brightness |

The complete earlier binary/waveform record stays seekable. Before gas initialization, the renderer shows no invented gas and the interface explains its coverage. ACCRETION VIEW seeks the initialized gas epoch. Cache times are elapsed source-frame replay times; the movie CSV also retains the separately offset source-coordinate time. These are not distant-detector arrival times or observer proper time.

The selected 513-frame asset copies every second compressed frame of the original 1025-frame source cache. The numerical audit checked all selected gzip payloads, CRCs, lengths and finite nonnegative density, and checked exact frame/diagnostic correspondence. It did not establish temporal or spatial convergence.

## Actual default-player checks

Both the normal Float-texture run and the forced Half-texture run passed **43 check records**. Each produced **31 named still captures** and **240 movie frames**. Their raw receipts are `gpu-final/minidisk-integration.json` and `gpu-final-half/minidisk-integration.json` under the research workspace's `outputs/Evolved-disks-validation/`.

The development-only [harness](../BinaryBlackHole/Assets/DTET/MiniDiskQaCapture.cs) exercised the normal default selection rather than opting into the older gas mode. Checks cover:

- Prepared-cache loading without a new default solve; finite positive initial density and actual mass-weighted motion; changing recorded density; immutable frame reads and rejected out-of-coverage epochs.
- Unchanged numerical body positions/masses, source time and raw complex H22 samples at four epochs across the full source record; gas visibility does not change those scientific values.
- Explicit missing early gas, recovery to the initialized epoch, and the wrist's coverage explanation.
- Actual gas radiance, repeatable forward/backward image seeks, camera movement while paused and cache-file disposal on leaving the scene.
- Cancellation of an immediately abandoned adjusted scenario and restoration of the numerical replay's matching cache identity.
- All three admitted render profiles, with the same decoded physical field and event cursor.

The independently read lower-frame density/velocity fingerprint was **`62036b0b1374e325`** in Laptop, Balanced and High, in both texture runs. This FNV fingerprint compares the decoded Sigma/vx/vz values at the selected epoch; it is not the asset's cryptographic hash or a test of every GPU sample. Each profile retained the 192²/513-frame physical history and cursor **0.8785584350339009 s**.

| Profile | Scene target | Curved-ray budget | Float mean completed readback | Half mean completed readback |
| --- | --- | ---: | ---: | ---: |
| Laptop | 640 × 360 | 192 | 7.30 ms | 6.13 ms |
| Balanced | 800 × 450 | 256 | 9.14 ms | 9.54 ms |
| High | 1120 × 630 | 384 | 16.84 ms | 17.27 ms |

These are **three explicit completed render/readback samples per profile on the named desktop GPU**, following a prepared frame. They include CPU/readback costs and are not native-window FPS, sustained thermal behavior or an integrated-GPU laptop benchmark. The separate 20-sample default-frame series measured means/p95 of 10.67/15.27 ms for Float and 10.68/13.27 ms for Half. Different run conditions and small samples preclude a general Float-versus-Half performance conclusion.

Reported mini-disk playback array/texture payload was **2,672,479 bytes for Float** and **2,082,655 bytes for Half**. This excludes the rest of Unity, sky, render targets, driver allocations and capture buffers. During the complete capture-heavy runs, 250 ms process sampling observed peak working/private bytes of 1,099,706,368 / 1,946,644,480 for Float and 1,111,031,808 / 1,933,950,976 for Half. These sampled process peaks are not dedicated VRAM measurements or proof that the entire application uses only the mini-disk payload.

## Controlled production-shader evidence

The fixtures use **the actual `CurvedDiskVolume` pass**, isolated cameras and synthetic input textures. They do not add a test-only formula branch. The ray model uses the Schwarzschild isotropic Fermat index

```
n(r) = (1 + m/(2r))³ / (1 - m/(2r)).
```

Bounded RK2 steps integrate the projected gradient of log n. The binary uses weighted sums of individual log-index gradients and an illustrative smooth transition to the remnant. Body coordinates and capture proxies are not demonstrated to be an isotropic binary metric. The method is a single-hole reference and an approximate binary display, not full dynamical-spacetime ray tracing. [Source method](../BinaryBlackHole/Assets/Resources/DTET/CurvedDiskOptics.hlsl); [primary interactive optical reference](https://ebruneton.github.io/black_hole_shader/).

| Controlled case | Executed result and boundary |
| --- | --- |
| Equal opposite speeds, Doppler disabled | Equal rendering within the specified comparison tolerance; confirms the control removes the signed velocity effect. |
| Uniform emitting patch with vz = ±0.2c | Approaching/receding integrated radiance ratio **6.24095**. This tests the sign in the selected false-colour/brightness closure, not a universal physical luminosity ratio. |
| Far-side-only emitter, curved versus straight rays | Off-plane region radiance ratio **152.076**. Bent paths reveal the actual supplied emitter outside its directly viewed thin plane. This is a synthetic image diagnostic. |
| Same curved geometry, empty emitter texture | No decorative gas light is produced. Curvature alone does not draw a luminous arc. |
| Single isolated hole, empty gas and white background | Apparent capture-radius error **0.2511%** against the analytic critical impact parameter `3 sqrt(3) m`, including the finite observer index at r = 16m; below the two-pixel-plus-0.5% gate. |

The optical reference uses the isolated-hole capture boundary at isotropic r = 0.5m, a 513² target and a 512-step budget. Production capture uses the declared source/proxy model. This one reference does not validate all caustics, photon rings, binary blending, merger capture shapes, gravitational shifts or ray-convergence behavior across viewpoints. The Half run exercises Half storage in the default player; its standalone synthetic optical fixtures deliberately retain Float input textures, so it is not an independent Half-arithmetic fixture suite.

The display applies bounded Doppler/gravitational styling and a bolometric-style shift-to-the-fourth-power brightness factor. The shader caps the optical velocity at 0.95c and bounds its shift. Those presentation safeguards do not alter or repair superluminal values in the Newtonian fluid cache. Opacity/exposure comparisons at initialization, −250tg, common horizon, +20tg and the end held the actual fluid data and camera fixed. The adopted 8/1.6 closure improves visibility without injecting gas, resetting mass or recalculating a temperature. Neither closure is calibrated emission physics.

## Visual sampling and movie

Direct inspection of selected final HUD/wrist, seed, edge-on, underside, common-horizon and movie frames 0/120/239 shows that the revised opening camera contains both disks, the gas changes/depletes, bent far-side emission is visible and approaching/receding sides differ. The wrist identifies gas seeded at approximately 0.682 s and the Newtonian/curved-light scope. The remnant retains faint remaining material rather than a compulsory flare or replenished bright ring.

Limits remain visible: smooth low-resolution structures, bright narrow rims, occasional subpixel/dotted edges and bands, and strong viewpoint-dependent brightness. These are not resolved turbulent plasma or validated photon-ring structure. Inspecting selected stills is not continuous native playback, a survey of every camera/epoch, an expert review or a human usability study.

Each final run writes 240 **actual 1280 × 720 scene renders**, with the Laptop ray budget, one fixed oblique camera and the same `1-exp(-radiance)` display transfer used by the interface, followed by sRGB file encoding. `movie/frames.csv` records every exact scientific epoch. Intended encoding is 24 fps, or 10 seconds; the gas interval is uniformly time-scaled. Increasing capture resolution does not constitute a measured 1280 × 720 Laptop performance claim. No wrist/menu or decorative external gas overlay is included. The earlier pilot's deferred optical-fixture leak into its first movie frame was corrected by immediate fixture deactivation; pilot imagery is not substituted for this final sequence.

## Adjusted scenario and regression scope

The earlier `gpu-late` run passed **42 records**, including a complete **40/25-solar-mass, initial-separation-35rg** background generation/load in **390.1016 seconds** under the Unity development player. It checked paused preparation, exact cache parameter/mode identity, generation matching, the covered-start cursor, actual emitted gas, and return to the original numerical replay. This was a pre-final-cache candidate, before later camera/material/memory changes; it does not certify every adjustable scenario or claim the final 192² replay mesh is regenerated in that time. The final Float/Half runs repeated immediate cancellation/stale-result protection but did not repeat the long successful solve.

The explicitly selected **`LegacyGasForQa`** regression paths also passed: the older observatory harness recorded **704 checks and 105 images**, and the broad-reservoir fluid harness **59 checks and 15 images**. Those retain existing interaction/legacy-renderer coverage and do not prove the new default by themselves. The portable simulation suite passed, and the separate model/cache suite passed 28 assertions; [the numerics receipt](MINIDISK_NUMERICS.md) states their scope.

## Evidence, reproduction and delivery

The development harness is opt-in through `DTET_MINIDISK_QA_PATH`. `DTET_FORCE_HALF_TEXTURES_QA=1` requests the supported Half player path. `DTET_MINIDISK_EXPERIMENT_QA=1` adds the long adjusted solve. Use a fresh output directory, a graphics-enabled development player and one harness at a time. Raw receipts, logs, stills, exact frame CSVs, model generation/audits and process-memory samples remain in the research workspace's `outputs/Evolved-disks-validation/`, outside the repository/player package.

Release builds, extraction/package audits, platform limitations and download artifacts are recorded separately in [Desktop distribution](DESKTOP_DISTRIBUTION.md). This graphics receipt does not pre-approve a Windows archive, Mac runtime, signing/notarization, headset or laptop result. M5/M6 compatible relativistic gas/metric work, convergence, temporal-image accuracy and real-device evidence remain open.

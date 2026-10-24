# Generated fluid: development milestone validation

Validated on **24 October 2026** against runtime commit **`7a01ece7036642ca2544c11a66cd4c375a1938b7`**. This optional mode produces a reduced fluid history from public binary inputs and assumed initial gas conditions. It is an experimental two-dimensional Newtonian/isothermal model. Passing its software checks does not establish a converged accretion calculation, measured GW150914 gas, GRMHD, binary metric evolution, native-window performance or tracked XR.

## Source and build

The branch is `physics/generated-fluid-lab` in `vincevandriel/Black-Hole-Unity-Project`, based on `physics/interactive-suit-release`. The numerical solver was frozen at `6cc36ad86036fbc0e65b3dd8e22b61f8c68fcb70`; subsequent runtime commits clarify labels and fix shader sampling. The default SXS input remains the retained licensed asset with SHA-256 `67345f1a8ecb2301890e67f3856dbb78170e0740ee2838021e3acdbe8b2657a4`.

Unity **6000.3.25f1** built the Windows x64 development player with `DTET.VisualPrototype.Editor.PrototypeBuild.BuildQaPlayer`. All thirteen custom shader assets were checked, the build returned zero and logged `DTET_QA_BUILD_SUCCEEDED`. The final log has no matched C# compiler warnings/errors or shader warnings/errors. This was a batch build from the existing checkout, not a fresh Unity import or interactive Console review. Unity-only project-settings whitespace was restored after building.

| Final build artifact | SHA-256 |
| --- | --- |
| `build-final.log` | `7b3c020ff1cc4cd245f7a444121b9a0fe1c4f547440e0a99037ecb182fddec90` |
| `DTET.Simulation.dll` | `7b93f8fb1e4eb9f82fa94f3a8f288a8e699bb3eee0fbd513cf608b0a234b5962` |
| `Assembly-CSharp.dll` | `6202ef7149b291fb2e5dd40dbc45045134bb05cc0d9a447805b78fabdd482bc8` |

An intermediate fresh shader compile exposed implicit texture derivatives in a variable-length ray loop. The delivered shader uses explicit level-zero sampling, consistent with the single-level fluid textures; the same shared sampler serves volume emission and illustrative foreground weighting. The sky's fourth-root expression now handles its nonnegative input explicitly. The final GPU fixtures were rerun after these corrections. Documentation, standalone audit and packaging commits may follow the tested runtime revision without changing those runtime sources.

## Passed software checks

| Check | Result |
| --- | --- |
| Portable shared-source suite | Passed, including the existing physics/session/optics suites and new fluid fixtures |
| Standalone refinement runner | Release build passed with zero warnings/errors; retained full audit results documented separately |
| Generated-fluid player integration | **40 checks passed**, 10 fresh captures |
| Existing interactive-observatory regression | **193 checks passed**, 30 fresh captures, all three rendering profiles and forced compatibility |

The numerical tests cover uniform preservation, conservative periodic budgets, transverse and diagonal advection, linearized sound, an independently solved nonlinear isothermal Riemann problem, softened force and sinks, positivity, actual-stage CFL, explicit floor/outflow accounting, cancellation, independent interpolation fixtures, immutable copies, coverage rejection, deterministic seeks/regeneration and vertical-density quadrature. The [model document](GENERATED_FLUID_MODEL.md) records equations and errors; the [standalone audit](../Staging/Fluid.RefinementAudit/README.md) reproduces the complete 96/192-grid comparison.

The fluid player checks opt-in asynchronous generation, fixed physics resolution, finite cache budget, automatic selection and covered-start pause, actual visible gas contribution, deterministic rendered seeks, free camera movement while paused, multiple viewpoints and epochs, shared scenario generations, CPU/GPU density and velocity sampling, normal camera-completion composition, mode switching, regeneration for an adjustable binary, bounded late-time coverage, explicit absence of uncovered gas, cancellation, stale-result rejection, release of owned textures/history, and the unsupported-renderer fallback.

The camera-completion check submits a normal pipeline render request outside the manual-composite bypass. It exercises the render callback but is not proof of native window presentation. CPU/GPU fixtures and image restoration verify the implemented model, not agreement with relativistic radiative transfer. Runtime logs were scanned for exceptions, failure markers and shader errors; Unity development shutdown telemetry still reports engine allocations, so no universal leak-free claim is made.

## Visual review and measured costs

The 1600 by 900 fluid-panel capture was inspected: labels, relative mass ledger, coverage, paused time and graph cursor are readable and consistent. Independent inspection of the preceding UI-identical candidate covered oblique, face-on, edge-on, initial/middle/remnant and enhanced-warp images. The numerical gas is visibly present and evolves. The square outer simulation boundary remains visible in some views; the Laptop profile is soft, and small horizon proxies can be subdued by emission. These are retained presentation/model limits. Static captures do not establish smooth interaction or a human usability result.

On the development desktop (Ryzen 5 5600X, RTX 3060 Ti), the final default solve took **93.53 seconds in the Unity development player**. The same physics took about **31 seconds under the .NET Release test process**. These are distinct runtime measurements, not a laptop guarantee. Generation is cancellable and runs outside the main rendering thread.

The immutable default history payload is **14,277,720 bytes**. Generator array payload including its working grids is **15,604,824 bytes**. The fluid panel's approximately **14.4 MiB playback payload** additionally counts the player's CPU upload arrays and texture payload estimates; none of these is total process memory. The full fluid test, including two solves, camera captures and GPU readbacks, peaked at **1,000,423,424 bytes working set** and **1,719,873,536 bytes private memory**, sampled every 250 ms. Dedicated VRAM and shorter transients were not measured.

The short Laptop-profile completed-render sample averaged **5.24 ms**, maximum **10.98 ms**, on that desktop. Each sample completes explicit offscreen rendering and a one-pixel GPU readback. This is not native presentation FPS, steady thermal performance, or a measurement on laptop/headset hardware.

## Scientific result and remaining limits

The improved MUSCL/SSPRK2 solver passes independent PDE fixtures, but the full binary problem is **not resolution-converged**. Doubling the grid from 96 to 192 changes final integrated mass by **20.4%** relative to the finer run; the final sampled-density disagreement is **63.14%**. The exact norm and retained output are in [the refinement evidence](Data/GeneratedFluid/README.md). Do not turn mass conservation, positivity or a more attractive image into a physical-accuracy claim.

The Newtonian calculation also reaches approximately **0.72c**, beyond a calibrated Newtonian flow regime. Isothermality, softening, sinks, finite boundaries, the initial ring, vertical Gaussian thickness and bright emission are declared assumptions. There is no evolved energy, magnetic field, radiation feedback, self-consistent binary backreaction, binary metric or null-ray transfer. Temporal-cache and boundary/domain convergence remain unestablished. This milestone provides an equation-evolved educational flow and a testable public-input workflow; full M5/M6 remain open through the [public-resource simulation route](PUBLIC_SIMULATION_ROUTE.md). Private data is not a prerequisite for that route.

## Reproduction and package provenance

From the repository root:

```powershell
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
dotnet build Staging/Fluid.RefinementAudit/Fluid.RefinementAudit.csproj -c Release
# Build the QA player with Unity, then run each GPU harness sequentially:
.\Scripts\Test-GeneratedFluid.ps1 -OutputDirectory <new-fluid-folder> -TimeoutSeconds 900
.\Scripts\Test-InteractiveExhibit.ps1 -OutputDirectory <new-observatory-folder> -BenchmarkSeconds 2
```

The research workspace retains `outputs/Generated-fluid-validation/` with `source-build.json`, final build/test logs, `verified-player/`, `regression-release/`, captures and process-memory receipts. Earlier exploratory receipts remain separate and are superseded by those final paths. `outputs/references/GW150914/generated-fluid/` retains the exact full-grid audit source and results. Run the separate refinement executable only when reproducing that more expensive study.

Package with `Scripts/Package-InteractiveExhibit.ps1`, supplying the full runtime commit above, the final build log, full observatory receipt **and** `-GeneratedFluidReceipt` pointing to the final fluid receipt. The package manifests identify the exact copied runtime bytes and documentation revision. Build/receipt association is the release operator's recorded assertion; hashes verify bytes, not scientific validity. The packager verifies runtime copies, every ZIP entry and optional fresh extraction, and includes the integration receipts. Launch `START_OBSERVATORY.cmd`, choose rendering quality, open **FLUID LAB / SOLVE GAS**, and press **SOLVE GAS**.

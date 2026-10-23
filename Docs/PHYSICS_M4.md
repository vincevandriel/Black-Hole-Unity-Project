# M4: reduced binary desktop exhibit

Launch a player with `--reduced-exhibit`. The entry screen offers **Auto, Laptop, Balanced and High** before loading the numerical history or allocating scene targets. The packaged launcher supplies the argument. The original M1/M2 exhibit and `--reference-lab` M3 control remain separate entry points.

In Unity Editor, open the existing `BinaryBlackHole_VisualStaging` scene, choose **DT-ET > Startup Exhibit > M4 Reduced Binary**, then press Play. That menu also selects the M3 lab or original lessons. Its selection lasts for the current Editor session; standalone players use their launch arguments.

The exhibit combines the retained SXS:BBH:0305 representative numerical binary, a prescribed educational gas field and a synthetic distant sky. It supports the complete retained inspiral/common-horizon/remnant interval, direct timeline seeking, pause, reverse and free exterior camera movement. The shared clock is `NumericalEventSession`: rendering quality does not change the event, source parameters, transition epoch or analytic gas field.

## Controls

| Action | Input |
| --- | --- |
| Play / pause | Space or transport button |
| Reverse | Comma or Reverse button |
| Reset / common horizon | R / M, or To Merger button |
| Seek | Timeline; seeking pauses playback |
| Rate | Rate button cycles 0.01x, 0.05x, 0.2x, retaining direction |
| Move / look | WASD, Q/E, right-drag, wheel; Shift moves faster |
| Overview / above / edge / below / close | 1 / 2 / 3 / 4 / 5 |
| Gas / weighted density samples | G / T |
| Model and controls explanation | H |
| Change quality | Quality / Return releases the scene and returns to the launcher |

The camera stays outside the moving equal-area horizon proxies, including after a seek. Its position may be adjusted if a recorded horizon moves over it. Camera translation and rotation while paused do not advance the event or gas time. No observer worldline, proper time, Doppler motion or horizon interior is inferred from this inspection camera.

## Model and images

[The reduced model specification](M4_REDUCED_FLOW_MODEL.md) records every equation, numerical input, prescribed coefficient, interpolation boundary and omitted effect. The reservoir, diminishing minidisks, bowed stream tubes and remnant ring are analytic functions of the requested SXS epoch. There is no stored gas movie, runtime fluid solver or grid history cache. This is an intentional small-memory implementation of the reduced M4 path; compatible physical fluid history remains M5.

CPU samples and the HLSL function evaluate the same relative density and bolometric emission. The GPU integrates straight rays through an actual three-dimensional volume. Gray opacity uses the declared coefficient `0.16 rho / rg`, and the source function is the summed relative `rho T^4` divided by density. Exact constant-segment absorption uses `1-exp(-tau)`, with a small-tau expansion to avoid cancellation. This does not add strong-field lensing or a relativistic spectrum.

Each profile selects a base segment count. Segments intersecting the thin inner-flow region receive four midpoint subsegments to reduce banding in narrow minidisks and streams. Region admission is expanded by a base segment around its boundary. The shader therefore uses between one and four times the base work, depending on the ray; the launcher reports base segments rather than implying a fixed total sample count. Validation compares the rendered linear image with a 256-base-segment reference using the same refinement; that reference is a convergence comparison, not an exact optical solution.

Gas radiance is premultiplied and composed over the sky with transmission `1-opacity`. A bounded linear HDR target retains bright source radiance, followed by one `1-exp(-L)` display map. An unsupported HDR format falls back to LDR with a visible clipping limitation. Colours and exposure are chosen for inspection and are not calibrated visible-light photometry. Apparent-horizon spheres are equal-area coordinate proxies, not ray-traced shadows.

Optional cyan markers are **weighted spatial density samples**, not particle fluid elements or material trajectories. Each deterministic low-discrepancy location carries `rho * boxVolume / N`. Changing their count changes diagnostic sampling error; it does not change the continuous emitting field. The total estimate may vary with time because this prescribed field is not a conservative fluid solution. The CPU velocity diagnostic is not a claim that these fixed sampling locations integrate the fluid velocity.

The radiation panel uses `Re(H22)/peak|H22|` from the retained numerical mode. It preserves M2's distinct horizon/radiation coordinate histories and explicit comparison-epoch alignment. Gray coverage and text indicate an unavailable radiation epoch; no waveform is fabricated beyond the record. This is neither detector strain nor Einstein Telescope data. See [M2](PHYSICS_M2.md).

## Quality, sky and resource ownership

[Quality and sky details](M4_QUALITY_AND_SKY.md) specify caps, capability admission, magnitude/flux distributions and pixel-integrated point-spread functions. Stars are effectively at infinity for these local camera translations. Bright sources have brighter/larger display footprints, rather than being giant nearby spheres. There is no vacuum twinkling. All profiles retain the same bright catalogue prefix; lower profiles omit a declared faint tail.

The UI renders at the window resolution while the scene uses a capped target. Auto uses conservative reported-memory hints and never selects High automatically. Unsupported gas shaders use the labelled compatibility view, preserving bodies, radiation and navigation. No 3D textures or compute shaders are required for this reduced model. The fallback was designed for degraded capabilities; forced fallback on a modern GPU does not certify an unknown older GPU or driver.

Each scene owns its target, materials, sample mesh, sky mesh and graph texture. Returning to the launcher disables the old root immediately, destroys owned resources and releases references to the event/model arrays. The bounded target/mesh arithmetic is not a total process or VRAM figure. Actual process samples and frame distributions belong in the validation receipt.

## Validation and remaining gates

See the [M4 validation receipt](PHYSICS_M4_VALIDATION.md) for the tested revision, results, package hash and remaining device gates.

Run the portable suite, build the development QA player, then run:

```powershell
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
& .\Scripts\Test-ReducedExhibitMilestone.ps1 -OutputDirectory C:/temp/dtet-m4 -BenchmarkSeconds 3
& .\Scripts\Test-ReducedExhibitMilestone.ps1 -OutputDirectory C:/temp/dtet-m4-steady -PerformanceOnly -BenchmarkSeconds 5
```

The M4 harness exercises startup/profile selection, timeline and mode controls, recorded body/radiation bindings, deterministic repeated seeks, camera independence, fallback, unload/reload, CPU/GPU field fixtures and actual rendered views. Performance windows explicitly render the scene and interface, then wait for a one-pixel GPU readback to establish completion. This measures completed offscreen frames, including that synchronization overhead, rather than treating a hidden player's update-loop cadence as rendering performance. PNG encoding, file I/O and full-image readbacks are outside those windows. The PowerShell runner samples Windows working set/private memory every 250 ms; these sampled peaks include QA captures/fixtures and exclude dedicated VRAM accounting.

The separate `-PerformanceOnly` process requests High (subject to capability admission), skips profile cycling, PNG captures and full-image validation reads, and measures five completed-render windows. Its process-memory trace is reported separately from the full QA run. In the portable package, `RUN_DEVICE_CHECK.cmd` and `RUN_PERFORMANCE_CHECK.cmd` run these two paths and preserve their receipts next to the player.

A laptop profile measured on a desktop is not a laptop benchmark. The plan's designated laptop performance gate requires a real laptop run. Scientific GRMHD, compatible binary metric data, observer light-cone rendering, headset performance and human usability remain outside this reduced desktop milestone.

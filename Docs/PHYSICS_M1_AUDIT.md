# M1 audit — 23 October 2026

**PASS after one corrected P2 finding; no unresolved blocker in M1 scope.** Audit requested before M2. Reviewed published M1 `02bca11` against foundation `f41b2e3`, with separate pure-model and Unity/HUD code reviews. The corrected implementation is `cad2d39174e4fed5ec3640d0a7bfb8e13c8b2ea8` (fix `ca54bf6`, expanded regression `cad2d39`). Later receipt changes are documentation only.

## Finding and correction

**P2 — stale physical units in artistic mode.** Launch in physical mode, switch to Storyboard, and select Graph on the learning board. The qualitative chirp retained the physical `f_GW (Hz)` axis, numeric frequency/source-time legend and source-time playback caption. The physical-frequency promise also remained in the rate readout.

The correction makes those labels follow the active mode and refreshes the storyboard graph immediately. Built-player checks now exercise both directions of the mode transition and verify the labels and graph bindings, including the spatial HUD. A capture of the artistic graph records the corrected presentation. The simulation equations are unchanged.

No further actionable defect was found in the bounded physical model or reviewed Unity binding. This is a software/scientific-consistency audit of M1, not external expert certification.

## Independent numerical audit

An independent dimensionless RK4 integration of circular quadrupole radiation reaction used 100,000 steps per configuration. Eight cases covered 30+30 and 20+40 solar masses at starting separations 25, 30, 45 and 50 total GM/c², ending at 10 GM/c². Comparison at four checkpoints per case gave maximum relative errors of 3.88e-12 in separation, 2.18e-13 in integrated phase and 4.58e-14 in available GW frequency. Sampled mass-weighted barycentre residuals were zero. Signed rates and endpoint clamping also passed.

The seven existing portable test groups passed. One concurrent reviewer invocation initially encountered a locked compiler output; its independent scratch audit used separate build outputs. That collision was not a source defect.

## Runtime gate

The repository was cloned from GitHub into a new directory with no Unity Library. Unity 6000.3.25f1 resolved dependencies and successfully built the published baseline. The two local correction commits were then fast-forwarded into that clone and the Windows development player rebuilt successfully. Both builds logged `DTET_QA_BUILD_SUCCEEDED`; no C# compiler error or runtime exception was found in the gate logs. Generated Unity settings changes in the validation clone are not source changes or publication inputs.

The corrected player passed **50/50 physical integration checks** and **nine retained artistic/input regression groups** on an NVIDIA GeForce RTX 3060 Ti. The expanded checks cover graph model identity and matching axis/legend/rate labels in both modes. Actual camera-render captures of the physical graph, physical readouts and artistic graph were inspected; labels were legible and mode-appropriate. Seven portable groups passed on the corrected source, 58 asset GUIDs were unique, and package manifests were unchanged.

The replacement Windows ZIP is built from this tested clone; all copied runtime file hashes match the build and the ZIP CRC is verified. Raw logs, captures, independent RK4 source/results and the package/hash receipt are in the task's `outputs/M1-audit/`, outside Git and Unity Assets. Use the audited package in preference to the earlier M1 ZIP. The earlier [M1 validation receipt](PHYSICS_M1_VALIDATION.md) remains historical evidence.

This passes the M1 software gate and permits M2 to begin. It does not imply a clean interactive Editor Console, a human usability study, external expert review, or a display-framerate benchmark; hidden-window timings are not performance evidence.

M1 remains a circular SI teaching model with an explicit cutoff and conceptual endpoint. Numerical merger, gas, detector strain, binary optics, tracked XR and broad hardware performance remain outside this gate.

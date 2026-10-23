# M2: numerical inspiral, common horizon and remnant replay

M2 adds a selectable **SXS:BBH:0305 Lev6** numerical replay to the existing exhibit. Press **N** or choose **SXS Replay** on the learning board. It starts paused at 0.05×; Space plays, R resets, the time slider/wrist controls seek, and comma/Reverse changes direction. The rate range is 0.01×–4×. Navigation remains independent of time. **TO MERGER** jumps exactly to the first supplied common apparent-horizon sample. P or the A/B/C buttons return to the SI lesson; Storyboard retains the original artwork.

## What is computed and displayed

The native horizon centres, masses, irreducible masses and spins come from the published numerical solution. A cyan and a violet proxy follow the two individual horizons. At the first common-horizon timestamp they are replaced by a gold common-horizon proxy, which follows the remaining numerical history. Marker radius is the radius of an equal-area sphere, calculated from irreducible mass; it does not reconstruct the deformed horizon surface, event horizon or optical shadow. The transition is triggered by the data, not by two spheres touching. The initially settling common-horizon mass is retained rather than replaced with the final remnant mass.

SXS right-handed coordinates `(x,y,z)` map to Unity display coordinates `(x,z,y)`, centred on the exhibit. This is a declared reflection into Unity's y-up display convention, not a new physical frame transformation. One scene unit represents 250 km. No barycentre recentering, orbit solver or view-dependent force is added. Trails sample previous positions in the same recorded history. Approximate thin-lens controls are unavailable in this mode because this dataset supplies no metric; returning to a previous mode restores its lens settings.

The graph uses the published complex `(ell,m)=(2,2)` radiation mode at extrapolation order N=2, with its supplied centre-of-mass correction. Define `H22 = r h22/M`. Its real component is displayed with height divided by the largest sampled `|H22|`; the readout shows the unscaled dimensionless amplitude. Graph tessellation uses 1,536 uniformly spaced display samples; the authoritative asset retains every native binary64 sample. It is neither measured LIGO detector strain nor a waveform projected for the visitor's viewing direction. Moving the camera therefore does not change this fixed-basis radiation plot.

## Time, coverage and configuration

The source total mass scale is 64.5 nominal solar masses, with separate horizon and radiation conversion rules. Native horizons use `t_g/C` seconds per code unit; the already-normalized radiation uses `t_g`, where `C=1.000016001308`. No cosmological redshift is applied. This convention is independent of the legacy M1 solar-mass constants. See the complete [data/unit contract](Data/SXS0305/README.md).

Replay begins at the metadata's relaxed coordinate time 540 and ends at the last common-horizon row. The visible configuration has relaxed component masses approximately 35.46 and 29.04 solar masses and aligned spins +0.330 and −0.440. It is a fixed GW150914-inspired numerical configuration, not the updated marginal measurement medians or a coherent posterior draw.

Horizon coordinate time and radiation retarded time remain distinct. For comparison, the first common apparent horizon and the largest sampled radiation amplitude independently define zero epochs. Equal source-second offsets from those epochs are shown together. This is explicitly a presentation alignment, not simultaneity, a measured propagation delay, a gauge transformation or a past-light-cone render.

The coordinate replay spans about −0.9993 to +0.2451 seconds relative to its epoch. Radiation ends about +0.04819 seconds after its own epoch. Later remnant states continue while the readout says **Outside waveform coverage** and the graph contains no extrapolated signal. Seek/reset/reverse use absolute-time evaluation with bounded interpolation, so replay is deterministic and independent of render frame rate.

GW150914 was detected by LIGO. There are no Einstein Telescope readings for this event. Bright gas, fluid histories, radiation transfer, binary optics, quality presets and the richer sky remain later milestones. This is reuse of published numerical-relativity data, not a new numerical solution or a first-ever scientific reconstruction.

## Build, data and checks

The 1,788,476-byte licensed runtime asset is included, so a clean source clone can build without Python or downloading raw HDF5 files. [Data provenance, checksums, attribution and regeneration instructions](Data/SXS0305/README.md) accompany it. Runtime parsing validates the schema, constants, finite values, monotonic times, coverage and bounded row counts. The model stores approximately 1.8 MB of numerical rows; this is not a complete application memory benchmark.

Run the eight portable groups with:

```powershell
dotnet run --project Staging/Simulation.Tests/BlackHole.Staging.Simulation.Tests.csproj -c Release
```

Build the development player using `DTET.VisualPrototype.Editor.PrototypeBuild.BuildQaPlayer`, then run:

```powershell
./Scripts/Test-NumericalMilestone.ps1 -OutputDirectory C:/temp/dtet-m2
./Scripts/Test-PhysicsMilestone.ps1 -OutputDirectory C:/temp/dtet-m1-regression
./Scripts/Capture-PrototypeAudit.ps1 -OutputDirectory C:/temp/dtet-art-regression -Variants Graph
```

The numerical harness is opt-in through `DTET_NUMERICAL_QA_PATH`; ordinary player sessions do not run it. Runtime evidence and fresh-clone/package validation will be recorded in the milestone validation receipt after the gate runs. Portable tests alone do not prove rendered integration, laptop performance, tracked XR, clean interactive Editor Console status or human usability.

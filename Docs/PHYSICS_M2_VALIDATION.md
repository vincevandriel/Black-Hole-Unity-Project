# M2 validation receipt

**Gate: PASS — 23 October 2026.** M2 implementation began after the [M1 audit](PHYSICS_M1_AUDIT.md) passed. This receipt covers the numerical replay defined in [PHYSICS_M2.md](PHYSICS_M2.md), with no unresolved release blockers found in the reviewed scope.

## Revisions and environment

- Runtime implementation and fresh-clone build: `cbacffd1a237b464b9d18c328a67cf8745108b2b`.
- Source reproducibility follow-up: `d1eba368c2a6403f34d95475b43aed99ef9c18a9`. This changes only `.gitattributes`: generated provenance/fixture JSON has LF endings on Windows, and `.bytes` assets are binary. Later receipt/documentation commits do not change the tested runtime.
- M1 prerequisite branch: `physics/model-binding`, audited publication `1b75391`; M2 branch: `physics/numerical-event-replay`.
- Unity `6000.3.25f1`; existing pinned URP `17.3.0`, Input System `1.17.0`, UGUI `2.0.0`.
- Windows x64 development player, Direct3D 12, NVIDIA GeForce RTX 3060 Ti. The automated harness controls the player and captures actual rendered frames.
- Dataset: SXS:BBH:0305 Lev6, pinned Zenodo record 1215528, revision 13, CC BY 4.0. [Attribution, source hashes and conversion contract](Data/SXS0305/README.md).

The source clone was created through Git from the local implementation branch, without copying Unity's generated Library. A separate fresh clone at `d1eba36` checked the Windows source-data reproduction fix. This is a fresh Git source/build test; it does not claim that the pre-publication clone was downloaded from GitHub.

## Passed checks

| Layer | Result and scope |
| --- | --- |
| Original data and converter | Pinned file sizes and registered MD5 hashes, finite/monotonic grids, mass and spin identities, exact selected-array binary round trip. Fresh-source `Import-Sxs0305.py --check` reproduced asset, provenance and fixtures byte for byte. |
| Portable C# | Eight groups passed, including independent Python-derived SXS SI fixtures, separate horizon/radiation conversions, common-horizon transition, out-of-coverage radiation, deterministic seeking, rate/endpoints, malformed-data rejection and allocation checks. |
| Unity import/build | Fresh-clone QA player built successfully, with `DTET_QA_BUILD_SUCCEEDED` and exit code 0. No C# compiler errors or shader errors found in the final build log. |
| Numerical player | 42 checks passed. Includes hidden-board keyboard entry/exit, scene positions/radii, 0.01x forward/reverse playback, repeated seek, pause with camera movement, exact first common horizon, all HUD/graph bindings, restoration on rewind, radiation unavailability, endpoints, mode round trips and six image captures. These are integration assertions, not 42 independent validations of general relativity. |
| M1 regression | 50 physical player checks passed against the M2 build. |
| Artistic/input regression | Nine retained assertion groups passed; Graph variant captured. This is one capture variant, not a new exhaustive visual audit of every legacy view. |
| Visual inspection | Final numerical overview, above, merger, radiation board and post-waveform readout captures inspected. Markers, fixed-history controls and coverage labels are visible. |
| Source review | Final read-only review found no concrete release blockers and confirmed that the documentation states the data and rendering limits. |
| Packaging | 168 copied runtime files individually SHA-256 matched the fresh build. Launcher, run guide, runtime manifest and SXS attribution added; 172 archive files total. ZIP CRC check passed. Debug-symbol and DoNotShip folders excluded. |

Validation found and corrected an inactive-UI lookup failure, hidden-board mode-switch handling, the float/double minimum playback-rate boundary and Windows JSON newline reproducibility. The final receipts above are from the corrected implementation. Failed intermediate attempts remain in the local evidence folder and are not counted as passes.

## Data and delivery hashes

```text
SXS0305.bytes
bytes: 1788476
SHA-256: 67345f1a8ecb2301890e67f3856dbb78170e0740ee2838021e3acdbe8b2657a4

DTET_M2_Windows.zip
bytes: 64534089
SHA-256: d2f42df9000f38348498ee4fdf8d90651d2bf085040e6dbe456f1c989fa890e9
```

The Windows package is generated outside Git at:

```text
C:\Users\vince\Documents\Codex\2026-10-23\i-w\outputs\DTET_M2_Windows.zip
```

Extract the complete folder and run `START_DTET_M2.cmd`. Press **N** to select the numerical replay, then **Space**. **H** opens the board and **TO MERGER** selects the common-horizon epoch. Use the timeline to seek and Reverse/comma plus Play to rewind. The default scene remains the paused M1 lesson.

## Retained evidence and reproduction

Machine-generated evidence is kept outside editable source in `C:\Users\vince\Documents\Codex\2026-10-23\i-w\outputs\M2-validation`:

- `clean-source-importer.log`: byte-reproduction PASS at `d1eba36`.
- `clean-clone-portable.log`: all eight portable groups.
- `clean-clone-build.log`: fresh Unity import/build result.
- `clean-clone-numerical/numerical-integration.json`, `player.log` and six PNGs: 42 numerical checks.
- `clean-clone-physical/physical-integration.json`, player log and captures: 50 M1 checks.
- `clean-clone-artistic/capture-receipts.json`, log and Graph PNG: nine regression groups.
- `summary.json`: combined package/test receipt. The package's `runtime-manifest.json` records all 168 copied runtime hashes.

Reproduction commands and required raw-data filenames are in [the milestone guide](PHYSICS_M2.md#build-data-and-checks) and [data guide](Data/SXS0305/README.md#reproduce-and-verify). Building the included runtime data requires neither Python nor the large original HDF5 files. Reproducing the data conversion requires those separately retained originals plus numpy and h5py.

## Evidence boundaries

This validates the imported data, unit conversion, recorded-history replay and Unity bindings. It is not an independent validation of the underlying SXS numerical-relativity solution. Linear interpolation and area-equivalent markers do not recover deformed horizon surfaces, an event horizon, an optical shadow or a compatible spacetime metric. The graph is a normalized radiation mode with an explicit reference-epoch alignment, not observed detector strain or a light-cone calculation.

It does not certify bright gas physics, arbitrary-camera binary optics, Einstein Telescope observations, tracked XR, human usability or scientific peer review. No sustained laptop/VR, GPU, complete memory-peak or leak-soak benchmark was performed. Short automated frame timing and zero allocations in a pure evaluation test do not establish those properties. Automated player logs do not establish a clean interactive Editor Console.

# SXS:BBH:0305 runtime data

`BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes` contains the complete selected Lev6 apparent-horizon histories and the complex (ell,m)=(2,2) radiation mode at extrapolation order N=2 from [SXS:BBH:0305, pinned Zenodo record 1215528](https://doi.org/10.5281/zenodo.1215528). It is a representative vacuum numerical-relativity simulation scaled to an illustrative source-frame total mass of 64.5 nominal solar masses. It is not a new numerical-relativity calculation or an exact measured reconstruction of GW150914. No bright gas reconstruction is implied.

The asset is 1,788,476 bytes. It preserves every binary64 value in the selected arrays, including all samples before the chosen replay start and after first common-horizon formation. Large raw HDF5 files are kept in the external reference library, outside Unity and Git. Attribution and license details are in [LICENSE.txt](LICENSE.txt), the binary contract and original checksums in [provenance.json](provenance.json), and independently calculated reference values in [fixtures.json](fixtures.json).

## Reproduce and verify

Use Python 3.10 or newer, numpy and h5py. The importer reads the three pinned originals and the cached Zenodo record from a directory provided by the operator; it performs no downloads. Required filenames:

- `sxs0305_Lev6_Horizons.h5`
- `sxs0305_Lev6_rhOverM_Asymptotic_GeometricUnits_CoM.h5`
- `sxs0305_Lev6_metadata.json`
- `zenodo_sxs_0305_1215528.json`

The source URLs, SHA-256 values, registered MD5 values and byte lengths are included in `provenance.json`. The record's CC-BY-4.0 declaration was also checked against the live Zenodo API on 2026-10-23 (record revision 13). The converter verifies the pinned record ID, license, simulation name, original file sizes and all three registered MD5 values before producing outputs.

From the repository root:

```powershell
python Scripts/Import-Sxs0305.py --source-dir C:/path/to/references/GW150914/data/raw
python Scripts/Import-Sxs0305.py --source-dir C:/path/to/references/GW150914/data/raw --check
```

`--check` regenerates the binary, provenance and fixtures in memory and compares all bytes with the existing files. It never writes them. The numerical values and output hashes are deterministic; reports do not include machine paths or generation timestamps. Custom `--output` and `--docs-dir` paths support an isolated comparison.

The importer checks finite values, strictly increasing times, exact equality of the selected horizon time grids, positive masses, subextremal spin, the Christodoulou identity, agreement with published dimensionless spin, and a complete binary round trip. The packed arrays match the originals bit for bit. An additional diagnostic omits odd samples and reconstructs their centres from even neighbours. This doubles the interpolation interval; its reported residuals characterize source cadence and local variation, not a rigorous bound for full-data interpolation, a physical distance uncertainty, or numerical-relativity accuracy. Runtime interpolation is validated separately against `fixtures.json`.

## Binary layout

All integers and IEEE754 binary64 values are little endian. There is no padding, compression or trailer. The fields occur in this order:

| Field | Encoding |
| --- | --- |
| Magic | Eight ASCII bytes `DTETSXS2` |
| Schema version | uint32, currently 1 |
| Relaxed native mass sum C | float64, 1.000016001308 |
| Illustrative source total mass in nominal solar masses | float64, 64.5 |
| Nominal solar GM, m³/s² | float64, 1.3271244e20 |
| Speed of light, m/s | float64, 299792458 |
| Relaxed native coordinate time | float64, 540 |
| Horizon A | uint32 row count 7376, then 9 float64 per row |
| Horizon B | uint32 row count 7376, then 9 float64 per row |
| Common horizon C | uint32 row count 5464, then 9 float64 per row |
| Radiation mode | uint32 row count 13869, then 3 float64 per row |

Horizon rows are `[t, x, y, z, M_Christodoulou, M_irreducible, Sx, Sy, Sz]`, in unaltered native code units. Values come from `CoordCenterInertial.dat`, `ChristodoulouMass.dat`, `ArealMass.dat` and `DimensionfulInertialSpin.dat` inside `AhA.dir`, `AhB.dir` or `AhC.dir`.

Radiation rows are `[u, Re(r h22/M), Im(r h22/M)]` from `Extrapolated_N2.dir/Y_l2_m2.dat` in the CoM-corrected asymptotic waveform. The first column already uses the relaxed total mass normalization. The label `u` here distinguishes this extrapolated radiation time from native coordinate time; it does not assert a new coordinate transformation.

## Units and presentation epochs

The [SXS data documentation](https://data.black-holes.org/waveforms/documentation.html) describes the distinct native horizon and normalized asymptotic-waveform conventions. With total source mass `M = 64.5`, nominal `GM_sun = 1.3271244e20 m³/s²`, `c = 299792458 m/s`, and native relaxed mass sum `C`:

```text
t_g = M * GM_sun / c³
native horizon seconds = native_t * t_g / C
native horizon metres = native_coordinate * M * GM_sun / (c² * C)
native horizon mass in solar masses = native_mass * M / C
waveform seconds = normalized_u * t_g
dimensionless spin = native_S / native_Christodoulou_mass²
area-equivalent radius in metres = 2 * native_M_irreducible * M * GM_sun / (c² * C)
```

Do not divide waveform time by C again. The asset itself retains native values; the runtime applies these conversions. No redshift is applied. Source seconds and detector seconds are distinct concepts, and the selected 64.5 total is an illustrative scale rather than a coherent posterior draw.

Replay begins at the metadata's relaxation time 540. Metadata masses are summaries and differ slightly from the instantaneous samples at t=540; the runtime's changing mass readouts use the samples. Apparent-horizon centres and scalar properties are gauge-dependent simulation measurements. Area-equivalent spheres are markers: their radius preserves area but does not reconstruct a deformed horizon, an event horizon, or a lensed optical shadow.

The coordinate and radiation timelines have independent zero epochs:

- Coordinate epoch: first supplied common apparent-horizon sample, native t = 3685.496267868691.
- Radiation epoch: largest sampled h22 amplitude, native u = 3692.7480095302817, sample index 12351. This is the discrete maximum, without fitting a sub-sample peak.

The display compares equal source-second offsets from these separate epochs. This reference alignment is an explicit presentation convention. It does not measure propagation delay, establish simultaneity, recover light cones, or transform the raw horizon frame into the waveform's CoM-corrected frame.

The replay covers -0.9992898239188639 to +0.2450566259854202 source seconds relative to its coordinate epoch. The radiation series covers -1.2074333538852877 to +0.04818601099494206 seconds relative to its radiation epoch. Keep the common-horizon history after the waveform ends and display radiation as unavailable outside its native coverage. Do not extrapolate, clamp to a fake sustained signal, or loop the waveform.

The mode is complex radiation data at infinity. An individual h22 mode is neither measured detector strain nor a complete waveform for an arbitrary viewing direction. Projection would require the requested mode content, angular basis, distance, observer orientation and detector response. This asset contains no gas solution, emission, opacity, spacetime metric, photon propagation or Einstein Telescope observations.

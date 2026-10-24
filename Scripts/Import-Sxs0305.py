#!/usr/bin/env python3
"""Build compact SXS:BBH:0305 Lev6 runtime assets from pinned originals.

Requires Python 3.10+, numpy and h5py; --source-dir points to data/raw.
--check compares in-memory output with the checkout. No network or Unity needed.
Retains selected binary64 samples without thinning or SI conversion.
See Docs/Data/SXS0305/README.md for conventions.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import struct
from pathlib import Path

import h5py
import numpy as np


MAGIC = b"DTETSXS2"
VERSION = 1
SOURCE_MASS_SOLAR = 64.5
GM_SOLAR = 1.3271244e20
LIGHT_SPEED = 299792458.0
RECORD_NAME = "zenodo_sxs_0305_1215528.json"
PREFIX = "sxs0305_Lev6_"
SELECTED = ("Horizons.h5", "rhOverM_Asymptotic_GeometricUnits_CoM.h5", "metadata.json")
WAVE_PATH = "Extrapolated_N2.dir/Y_l2_m2.dat"
TRACK_COLUMNS = ["native_coordinate_time", "x", "y", "z", "christodoulou_mass",
                 "irreducible_mass", "spin_x", "spin_y", "spin_z"]
WAVE_COLUMNS = ["native_normalized_radiation_time", "real_r_h22_over_M", "imag_r_h22_over_M"]


def digest(path: Path, algorithm: str = "sha256") -> str:
    h = hashlib.new(algorithm)
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def checked_rows(rows: np.ndarray, columns: int, name: str) -> np.ndarray:
    rows = np.asarray(rows, dtype="<f8")
    require(rows.ndim == 2 and rows.shape[1] == columns and len(rows) >= 2,
            f"{name}: invalid shape")
    require(bool(np.isfinite(rows).all()), f"{name}: nonfinite sample")
    require(bool((np.diff(rows[:, 0]) > 0).all()), f"{name}: nonmonotonic times")
    return np.ascontiguousarray(rows)


def read_track(horizons: h5py.File, label: str) -> np.ndarray:
    group = horizons[f"Ah{label}.dir"]
    centres = checked_rows(group["CoordCenterInertial.dat"][:], 4, label + " centres")
    components = [centres]
    for dataset, width in (("ChristodoulouMass.dat", 2), ("ArealMass.dat", 2),
                           ("DimensionfulInertialSpin.dat", 4)):
        values = checked_rows(group[dataset][:], width, label + " " + dataset)
        require(np.array_equal(values[:, 0], centres[:, 0]), f"{label}: time grids differ")
        components.append(values[:, 1:])
    rows = checked_rows(np.column_stack(components), 9, label)
    require(bool((rows[:, 4:6] > 0).all()), f"{label}: nonpositive masses")
    chi = rows[:, 6:9] / rows[:, 4:5] ** 2
    require(bool((np.linalg.norm(chi, axis=1) < 1.001).all()), f"{label}: unphysical spin")
    direct_chi = checked_rows(group["chiInertial.dat"][:], 4, label + " chi")
    require(np.array_equal(direct_chi[:, 0], rows[:, 0]), f"{label}: chi time grids differ")
    require(bool(np.allclose(chi, direct_chi[:, 1:], rtol=2e-13, atol=1e-15)),
            f"{label}: dimensionful spin disagrees with published chi")
    return rows


def interpolate(rows: np.ndarray, native_time: float) -> np.ndarray:
    """Independent fixture calculation, restricted to the source support."""
    require(rows[0, 0] <= native_time <= rows[-1, 0], "Fixture outside source support")
    return np.array([native_time] + [float(np.interp(native_time, rows[:, 0], rows[:, c]))
                                  for c in range(1, rows.shape[1])])


def selected_fixture(rows: np.ndarray, native_time: float, length_scale: float,
                     mass_scale: float) -> dict:
    row = interpolate(rows, native_time)
    return {"native_row": row.tolist(), "position_metres": (row[1:4] * length_scale).tolist(),
            "mass_solar": float(row[4] * mass_scale),
            "areal_radius_metres": float(2 * row[5] * length_scale),
            "dimensionless_spin": (row[6:9] / row[4] ** 2).tolist()}


def reconstruct_and_check(blob: bytes, constants: tuple, tracks: list, wave: np.ndarray) -> None:
    stream = io.BytesIO(blob)
    require(stream.read(8) == MAGIC, "Binary magic roundtrip failed")
    require(struct.unpack("<I", stream.read(4))[0] == VERSION, "Binary version roundtrip failed")
    require(struct.unpack("<5d", stream.read(40)) == constants, "Header roundtrip failed")
    for expected in tracks + [wave]:
        n = struct.unpack("<I", stream.read(4))[0]
        require(n == len(expected), "Binary row count roundtrip failed")
        packed = stream.read(expected.nbytes)
        # Compare every binary64 bit, not rounded JSON values.
        require(packed == expected.tobytes(order="C"), "Native double bits changed")
    require(stream.read(1) == b"", "Trailing data in binary roundtrip")


def convert(source: Path) -> tuple[bytes, dict, dict]:
    record_path = source / RECORD_NAME
    record = json.loads(record_path.read_text(encoding="utf-8"))
    require(record["id"] == 1215528, "Wrong pinned Zenodo record")
    require(record["metadata"]["license"]["id"] == "cc-by-4.0", "Unexpected source license")
    upstream = {item["key"]: item for item in record["files"]}
    originals = []
    for name in SELECTED:
        path = source / (PREFIX + name)
        entry = upstream[f"SXS:BBH:0305/Lev6/{name}"]
        require(path.stat().st_size == entry["size"], f"Source size mismatch: {name}")
        require("md5:" + digest(path, "md5") == entry["checksum"], f"Source checksum mismatch: {name}")
        originals.append({"filename": path.name, "source_key": entry["key"],
                          "bytes": path.stat().st_size, "sha256": digest(path),
                          "verified_upstream_checksum": entry["checksum"],
                          "url": entry["links"]["self"]})
    metadata = json.loads((source / (PREFIX + "metadata.json")).read_text(encoding="utf-8"))
    require("SXS:BBH:0305" in metadata["alternative_names"], "Wrong SXS simulation")
    c_mass = metadata["relaxed_mass1"] + metadata["relaxed_mass2"]
    relaxed = metadata["relaxed_measurement_time"]
    require(abs(c_mass - 1.000016001308) < 1e-14 and relaxed == 540.0,
            "Pinned metadata conventions changed")
    constants = (c_mass, SOURCE_MASS_SOLAR, GM_SOLAR, LIGHT_SPEED, relaxed)
    with h5py.File(source / (PREFIX + "Horizons.h5"), "r") as horizons:
        tracks = [read_track(horizons, label) for label in "ABC"]
    with h5py.File(source / (PREFIX + SELECTED[1]), "r") as waveform:
        wave = checked_rows(waveform[WAVE_PATH][:], 3, "h22")
    require([len(t) for t in tracks] == [7376, 7376, 5464] and len(wave) == 13869,
            "Unexpected sample counts for pinned release")
    blob = MAGIC + struct.pack("<I5d", VERSION, *constants)
    for rows in tracks + [wave]:
        blob += struct.pack("<I", len(rows)) + rows.tobytes(order="C")
    reconstruct_and_check(blob, constants, tracks, wave)

    tg = SOURCE_MASS_SOLAR * GM_SOLAR / LIGHT_SPEED ** 3
    horizon_time_scale = tg / c_mass
    length_scale = SOURCE_MASS_SOLAR * GM_SOLAR / LIGHT_SPEED ** 2 / c_mass
    mass_scale = SOURCE_MASS_SOLAR / c_mass
    peak_index = int(np.argmax(np.hypot(wave[:, 1], wave[:, 2])))
    peak = wave[peak_index]
    common_time = tracks[2][0, 0]
    diagnostics = {}
    for label, rows in zip("ABC", tracks):
        spin_squared = np.sum(rows[:, 6:9] ** 2, axis=1)
        expected_mass_squared = rows[:, 5] ** 2 + spin_squared / (4 * rows[:, 5] ** 2)
        mass_residual = np.max(np.abs(expected_mass_squared - rows[:, 4] ** 2) / rows[:, 4] ** 2)
        # Reconstruct omitted interior odd samples at twice the stored interval.
        # This measures cadence sensitivity, not NR or full-data interpolation error.
        evens = np.arange(0, len(rows), 2)
        held = np.arange(1, evens[-1], 2)
        predicted = np.column_stack([np.interp(rows[held, 0], rows[evens, 0], rows[evens, k])
                                     for k in range(1, 4)])
        errors = np.linalg.norm(predicted - rows[held, 1:4], axis=1) * length_scale
        diagnostics[label] = {
            "max_christodoulou_identity_relative_residual": float(mass_residual),
            "held_out_odd_samples": len(held),
            "two_interval_linear_position_max_error_metres": float(errors.max()),
            "two_interval_linear_position_rms_error_metres": float(np.sqrt(np.mean(errors ** 2))),
        }
        require(mass_residual < 2e-12, f"{label}: Christodoulou identity failed")

    provenance = {
        "schema": "DTETSXS2-v1", "dataset": "SXS:BBH:0305", "resolution": "Lev6",
        "doi": "10.5281/zenodo.1215528", "license": "CC-BY-4.0",
        "license_url": "https://creativecommons.org/licenses/by/4.0/",
        "creators": [creator["name"] for creator in record["metadata"]["creators"]],
        "source_record_snapshot": {"filename": RECORD_NAME, "sha256": digest(record_path)},
        "original_files": originals,
        "selected_horizon_datasets": [f"Ah{{A,B,C}}.dir/{name}" for name in
                                      ["CoordCenterInertial.dat", "ChristodoulouMass.dat",
                                       "ArealMass.dat", "DimensionfulInertialSpin.dat"]],
        "selected_waveform_dataset": WAVE_PATH,
        "horizon_columns": TRACK_COLUMNS, "waveform_columns": WAVE_COLUMNS,
        "changes": "Selected datasets interleaved in little-endian binary64; all native sample values retained bit for bit. No resampling, SI conversion, frame transformation, or phase rotation in asset.",
        "sample_counts": dict(zip(["AhA", "AhB", "AhC", "h22"], [len(t) for t in tracks] + [len(wave)])),
        "runtime_asset": {"path": "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes",
                          "bytes": len(blob), "sha256": hashlib.sha256(blob).hexdigest()},
        "conversion": {"relaxed_mass_sum_code_units": c_mass, "source_total_mass_solar": SOURCE_MASS_SOLAR,
                       "nominal_solar_GM_m3_s2": GM_SOLAR, "c_m_s": LIGHT_SPEED,
                       "horizon_metres_per_code_length": length_scale,
                       "horizon_seconds_per_code_time": horizon_time_scale,
                       "waveform_seconds_per_normalized_time": tg,
                       "solar_masses_per_code_mass": mass_scale},
        "epochs": {"relaxed_horizon_native_time": relaxed,
                   "common_horizon_first_native_time": float(common_time),
                   "radiation_sampled_amplitude_peak_native_time": float(peak[0]),
                   "radiation_peak_index": peak_index,
                   "alignment": "The first common apparent-horizon sample and sampled h22 amplitude peak are separate zero epochs. Equal offsets in source seconds are a presentation convention, not a propagation delay or gauge/coordinate transformation."},
        "coverage_source_seconds_from_respective_epoch": {
            "horizon_replay_start": float((relaxed - common_time) * horizon_time_scale),
            "horizon_replay_end": float((tracks[2][-1, 0] - common_time) * horizon_time_scale),
            "radiation_start": float((wave[0, 0] - peak[0]) * tg),
            "radiation_end": float((wave[-1, 0] - peak[0]) * tg)},
        "validation": {"upstream_md5_checks_passed": 3, "native_sample_bit_roundtrip": True,
                       "all_times_strictly_increasing": True, "all_values_finite": True,
                       "published_chi_matches_derived_spin": True,
                       "interpolation_diagnostic_scope": "Omitted odd knots reconstructed from their even neighbours; this doubles the native interval and is not a strict interpolation error bound or an NR accuracy estimate.",
                       "horizons": diagnostics},
        "limits": ["Representative vacuum NR simulation, not an exact measured reconstruction of GW150914.",
                   "Native horizon coordinates are gauge dependent; CoM-corrected waveform uses a different frame and extrapolated radiation time.",
                   "Areal mass permits a spherical area-equivalent marker radius; no horizon surface, optical shadow or spacetime metric is supplied.",
                   "h22 is one complex radiation mode, not detector strain or a full observer-projected waveform.",
                   "No gas, accretion emission, lensing, redshift, detector response or Einstein Telescope event data is contained.",
                   "Keep full common-horizon duration; radiation is unavailable beyond its support and must never be extrapolated."]
    }
    fixtures = {
        "schema": "SXS0305-fixtures-v1", "asset_sha256": hashlib.sha256(blob).hexdigest(),
        "method": "Full native rows or numpy.interp per component for off-knot fixtures; no time/frame alignment applied to stored rows.",
        "relaxed_mass1_code": metadata["relaxed_mass1"], "relaxed_mass2_code": metadata["relaxed_mass2"],
        "horizons": {
            "A_relaxed": selected_fixture(tracks[0], relaxed, length_scale, mass_scale),
            "B_relaxed": selected_fixture(tracks[1], relaxed, length_scale, mass_scale),
            "A_off_knot": selected_fixture(tracks[0], 542.125, length_scale, mass_scale),
            "B_off_knot": selected_fixture(tracks[1], 542.125, length_scale, mass_scale),
            "C_first": selected_fixture(tracks[2], tracks[2][0, 0], length_scale, mass_scale),
            "C_last": selected_fixture(tracks[2], tracks[2][-1, 0], length_scale, mass_scale)},
        "radiation": {"first_native_row": wave[0].tolist(), "peak_native_row": peak.tolist(),
                      "last_native_row": wave[-1].tolist(), "peak_index": peak_index,
                      "peak_amplitude": float(np.hypot(peak[1], peak[2])),
                      "off_knot_native_row": interpolate(wave, (wave[12000, 0] + wave[12001, 0]) / 2).tolist()},
        "source_seconds_from_reference_epochs": provenance["coverage_source_seconds_from_respective_epoch"]}
    return blob, provenance, fixtures


def json_bytes(value: dict) -> bytes:
    return (json.dumps(value, indent=2, ensure_ascii=True, allow_nan=False) + "\n").encode("utf-8")


def main() -> None:
    repo = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-dir", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=repo / "BinaryBlackHole/Assets/Resources/Physics/SXS0305.bytes")
    parser.add_argument("--docs-dir", type=Path, default=repo / "Docs/Data/SXS0305")
    parser.add_argument("--check", action="store_true", help="Compare outputs without writing any files")
    args = parser.parse_args()
    blob, provenance, fixtures = convert(args.source_dir)
    artifacts = {args.output: blob, args.docs_dir / "provenance.json": json_bytes(provenance),
                 args.docs_dir / "fixtures.json": json_bytes(fixtures)}
    for path, content in artifacts.items():
        if args.check:
            require(path.exists() and path.read_bytes() == content, f"Generated artifact differs: {path}")
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
    print(json.dumps({"status": "PASS", "check_only": args.check,
                      "runtime_asset": provenance["runtime_asset"],
                      "sample_counts": provenance["sample_counts"],
                      "validation": provenance["validation"]}, indent=2))


if __name__ == "__main__":
    main()

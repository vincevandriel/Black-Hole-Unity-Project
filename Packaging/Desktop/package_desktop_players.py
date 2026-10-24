#!/usr/bin/env python3
"""Build a minimal desktop delivery ZIP without flattening macOS bundle links.

Only existing release players are inputs. ZIP contents, metadata and an independent
file extraction are checked. Where Windows cannot create symbolic links, their
ZIP entries are verified and reported without creating misleading regular files.
These checks do not establish macOS runtime compatibility,
code-signing validity, notarization, or binary-to-source provenance.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import plistlib
import posixpath
import re
import shutil
import stat
import struct
import subprocess
import zipfile

APP_NAME = "DTET_Binary_Explorer"
SOURCE_URL = "https://github.com/vincevandriel/Black-Hole-Unity-Project"
# Leave headroom below legacy Windows file and directory path limits. This is a
# packaging budget, not a promise about every extractor or destination chosen.
PATH_BUDGET = 240
FORBIDDEN = re.compile(
    r"(?i)(DoNotShip|DontShip|BurstDebugInformation|(^|/)(Library|Temp|obj|logs?|backups?|\.git|\.vs|__pycache__)(/|$)"
    r"|\.(pdb|mdb|bak|orig|log|tmp|csproj|sln)$|(^|/)\.DS_Store$|~$)"
)
NATIVE_MAGIC = {b"\xfe\xed\xfa\xce", b"\xce\xfa\xed\xfe", b"\xfe\xed\xfa\xcf", b"\xcf\xfa\xed\xfe",
                b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca", b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca"}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def file_hash(path: Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def path_length(value: str) -> int:
    """Windows paths count UTF-16 code units, including surrogate pairs."""
    return len(value.encode("utf-16-le")) // 2


def within(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def mac_architectures(path: Path) -> list[str]:
    with path.open("rb") as stream:
        header = stream.read(4096)
    require(header[:4] in NATIVE_MAGIC, f"Mac bundle executable is not Mach-O: {path}")
    magic = header[:4]
    big = magic in {b"\xfe\xed\xfa\xce", b"\xfe\xed\xfa\xcf", b"\xca\xfe\xba\xbe", b"\xca\xfe\xba\xbf"}
    endian = ">" if big else "<"
    if magic in {b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca", b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca"}:
        count = struct.unpack_from(endian + "I", header, 4)[0]
        stride = 32 if magic in {b"\xca\xfe\xba\xbf", b"\xbf\xba\xfe\xca"} else 20
        require(0 < count <= 32 and 8 + count * stride <= len(header), "Invalid universal Mach-O header")
        cpus = [struct.unpack_from(endian + "I", header, 8 + i * stride)[0] for i in range(count)]
    else:
        cpus = [struct.unpack_from(endian + "I", header, 4)[0]]
    names = {0x01000007: "Intel x86_64", 0x0100000C: "Apple silicon arm64"}
    require(all(cpu in names for cpu in cpus), f"Unsupported Mac architecture(s): {cpus}")
    return sorted({names[cpu] for cpu in cpus})


def check_windows_exe(path: Path) -> None:
    with path.open("rb") as stream:
        require(stream.read(2) == b"MZ", "Windows executable lacks an MZ header")
        stream.seek(0x3C)
        offset = struct.unpack("<I", stream.read(4))[0]
        stream.seek(offset)
        require(stream.read(4) == b"PE\0\0", "Windows executable lacks a PE header")
        require(struct.unpack("<H", stream.read(2))[0] == 0x8664, "Windows player must be x86_64")


def source_record(repo: Path, revision: str) -> tuple[str, int]:
    require(re.fullmatch(r"[0-9a-fA-F]{40}", revision) is not None, "Source revision must be a full commit hash")
    resolved = subprocess.check_output(["git", "-C", str(repo), "rev-parse", "--verify", revision + "^{commit}"], text=True).strip()
    require(resolved == revision.lower(), "Source revision mismatch")
    epoch = int(subprocess.check_output(["git", "-C", str(repo), "show", "-s", "--format=%ct", resolved], text=True).strip())
    return resolved, epoch


def collect_tree(path: Path, destination: str, root: Path, mac: bool, entries: list[dict]) -> None:
    relative = path.relative_to(root).as_posix()
    if FORBIDDEN.search(relative):
        require(not mac, f"Debug/temporary content is inside the Mac bundle: {relative}. Rebuild or clean and re-sign before packaging.")
        return
    value = path.lstat()
    require(not (getattr(value, "st_file_attributes", 0) & 0x400) or stat.S_ISLNK(value.st_mode),
            f"Unsupported filesystem reparse point: {path}")
    if stat.S_ISLNK(value.st_mode):
        require(mac, f"Windows runtime must not contain symbolic links: {path}")
        target = os.readlink(path).replace("\\", "/")
        require(not PurePosixPath(target).is_absolute() and not re.match(r"^[A-Za-z]:", target), f"Absolute bundle symlink: {path}")
        require(within(path, root), f"Bundle link escapes its bundle: {path} -> {target}")
        require(path.exists(), f"Dangling bundle link: {path} -> {target}")
        data = target.encode("utf-8")
        entries.append(dict(path=destination, kind="symlink", mode=stat.S_IFLNK | 0o777,
                            bytes=len(data), sha256=digest(data), inline=data, link_target=target))
    elif stat.S_ISDIR(value.st_mode):
        entries.append(dict(path=destination.rstrip("/") + "/", kind="directory", mode=stat.S_IFDIR | 0o755,
                            bytes=0, sha256=digest(b""), inline=b""))
        for child in sorted(path.iterdir(), key=lambda item: item.name):
            collect_tree(child, destination.rstrip("/") + "/" + child.name, root, mac, entries)
    elif stat.S_ISREG(value.st_mode):
        executable = False
        if mac:
            with path.open("rb") as stream:
                start = stream.read(4)
            executable = (start in NATIVE_MAGIC or start.startswith(b"#!") or bool(value.st_mode & 0o111))
        entries.append(dict(path=destination, kind="file", mode=stat.S_IFREG | (0o755 if executable else 0o644),
                            bytes=value.st_size, sha256=file_hash(path), source=path))
    else:
        raise ValueError(f"Unsupported runtime file type: {path}")


def add_text(entries: list[dict], path: str, text: str) -> None:
    data = text.replace("\r\n", "\n").encode("utf-8")
    entries.append(dict(path=path, kind="file", mode=stat.S_IFREG | 0o644,
                        bytes=len(data), sha256=digest(data), inline=data))


def public_entry(entry: dict) -> dict:
    return {key: value for key, value in entry.items() if key not in {"source", "inline"}}


def write_zip(path: Path, entries: list[dict], timestamp: tuple) -> None:
    with zipfile.ZipFile(path, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9, allowZip64=True) as archive:
        for entry in entries:
            info = zipfile.ZipInfo(entry["path"], timestamp)
            info.create_system = 3  # Unix: macOS Archive Utility sees executable/symlink bits.
            info.external_attr = entry["mode"] << 16
            if entry["kind"] == "directory":
                info.external_attr |= 0x10
            info.compress_type = zipfile.ZIP_STORED if entry["kind"] != "file" else zipfile.ZIP_DEFLATED
            if "inline" in entry:
                archive.writestr(info, entry["inline"])
            else:
                with archive.open(info, "w", force_zip64=True) as target, entry["source"].open("rb") as source:
                    shutil.copyfileobj(source, target, length=1024 * 1024)


def validate_zip(path: Path, entries: list[dict]) -> None:
    with zipfile.ZipFile(path) as archive:
        infos = archive.infolist()
        require([item.filename for item in infos] == [entry["path"] for entry in entries], "ZIP inventory mismatch")
        require(len({item.filename.casefold() for item in infos}) == len(infos), "ZIP has case-insensitive duplicate names")
        for info, expected in zip(infos, entries):
            require(info.create_system == 3 and info.external_attr >> 16 == expected["mode"], f"ZIP Unix permissions mismatch: {info.filename}")
            require(info.file_size == expected["bytes"], f"ZIP size mismatch: {info.filename}")
            value = hashlib.sha256()
            with archive.open(info) as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    value.update(block)
            require(value.hexdigest() == expected["sha256"], f"ZIP SHA-256 mismatch: {info.filename}")


def extract_and_inventory(archive_path: Path, destination: Path) -> tuple[list[dict], list[dict]]:
    """Read ZIP afresh; report links denied by Windows instead of flattening them."""
    destination.mkdir()
    deferred_links = []
    with zipfile.ZipFile(archive_path) as archive:
        all_infos = archive.infolist()
        known_paths = {info.filename.rstrip("/"): info for info in all_infos}
        require(len(known_paths) == len(all_infos), "Colliding ZIP file/directory names")
        link_targets = {info.filename: archive.read(info).decode("utf-8") for info in all_infos
                        if stat.S_ISLNK(info.external_attr >> 16)}

        def resolved_link_target(name: str, target: str) -> str:
            require(target and not PurePosixPath(target).is_absolute() and ":" not in target
                    and "\\" not in target, f"Unsafe ZIP symlink target: {name}")
            resolved = posixpath.normpath(posixpath.join(posixpath.dirname(name), target))
            seen = set()
            while True:
                require(len(seen) < 128, f"Excessive or cyclic ZIP symlink expansion: {name}")
                require(resolved != ".." and not resolved.startswith("../") and not resolved.startswith("/"),
                        f"ZIP symlink escapes archive: {name}")
                require(resolved not in seen, f"Cyclic ZIP symlink: {name}")
                seen.add(resolved)
                parts = PurePosixPath(resolved).parts
                intermediate = next(("/".join(parts[:index]) for index in range(1, len(parts) + 1)
                                     if "/".join(parts[:index]) in link_targets), None)
                if intermediate is None:
                    require(resolved in known_paths, f"Dangling ZIP symlink: {name} -> {target}")
                    return resolved
                next_target = link_targets[intermediate]
                require(next_target and not PurePosixPath(next_target).is_absolute()
                        and ":" not in next_target and "\\" not in next_target, "Unsafe nested ZIP symlink")
                suffix = resolved[len(intermediate):].lstrip("/")
                resolved = posixpath.normpath(posixpath.join(posixpath.dirname(intermediate), next_target, suffix))

        resolved_targets = {name: resolved_link_target(name, target) for name, target in link_targets.items()}
        links = []
        for info in all_infos:
            parts = PurePosixPath(info.filename).parts
            require(parts and not PurePosixPath(info.filename).is_absolute() and ".." not in parts
                    and "\\" not in info.filename and ":" not in info.filename, "Unsafe ZIP path")
            require(not any("/".join(parts[:index]) in link_targets for index in range(1, len(parts))),
                    f"ZIP entry is nested beneath a symbolic link: {info.filename}")
            path = destination.joinpath(*parts)
            mode = info.external_attr >> 16
            if stat.S_ISDIR(mode):
                path.mkdir(parents=True, exist_ok=True)
            elif stat.S_ISLNK(mode):
                links.append((info.filename, path, link_targets[info.filename]))
            elif stat.S_ISREG(mode):
                path.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(info) as source, path.open("xb") as target:
                    shutil.copyfileobj(source, target, length=1024 * 1024)
                if os.name != "nt":
                    path.chmod(stat.S_IMODE(mode))
            else:
                raise ValueError(f"Unexpected ZIP type: {info.filename}")
        for name, path, target in links:
            path.parent.mkdir(parents=True, exist_ok=True)
            target_info = known_paths[resolved_targets[name]]
            try:
                os.symlink(target, path, target_is_directory=stat.S_ISDIR(target_info.external_attr >> 16))
            except OSError as error:
                if os.name != "nt" or getattr(error, "winerror", None) != 1314:
                    raise
                # Do not write a stub file or dereference/copy a linked directory:
                # that would disguise an incomplete bundle as a runnable app.
                data = target.encode("utf-8")
                deferred_links.append(dict(path=name, kind="symlink", bytes=len(data), sha256=digest(data), link_target=target))
    records = []

    def inspect(folder: Path) -> None:
        for path in sorted(folder.iterdir(), key=lambda item: item.name):
            relative = path.relative_to(destination).as_posix()
            if path.is_symlink():
                target = os.readlink(path).replace("\\", "/")
                data = target.encode("utf-8")
                records.append(dict(path=relative, kind="symlink", bytes=len(data), sha256=digest(data), link_target=target))
            elif path.is_dir():
                records.append(dict(path=relative + "/", kind="directory", bytes=0, sha256=digest(b"")))
                inspect(path)
            else:
                records.append(dict(path=relative, kind="file", bytes=path.stat().st_size, sha256=file_hash(path)))
    inspect(destination)
    return sorted(records, key=lambda item: item["path"]), sorted(deferred_links, key=lambda item: item["path"])


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, required=True)
    parser.add_argument("--windows-build", type=Path, required=True)
    parser.add_argument("--mac-app", type=Path)
    parser.add_argument("--windows-only", action="store_true", help="Explicitly package Windows only; omit --mac-app")
    parser.add_argument("--source-revision", required=True)
    parser.add_argument("--mac-source-revision", help="Mac runtime source commit; defaults to --source-revision")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--package-name", default="DTET")
    parser.add_argument("--mac-signing-status", choices=("unknown", "unsigned", "ad-hoc", "developer-id-notarized"), default="unknown")
    args = parser.parse_args()
    require(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,79}", args.package_name) is not None, "Unsafe package name")
    require(not (args.windows_only and args.mac_app), "Do not supply --mac-app with --windows-only")
    require(not (args.windows_only and args.mac_source_revision), "Do not supply --mac-source-revision with --windows-only")
    require(args.windows_only or args.mac_app is not None, "--mac-app is required unless --windows-only is selected")
    repo, windows, output = (value.resolve() for value in (args.repo, args.windows_build, args.output))
    app = None if args.windows_only else args.mac_app.resolve()
    require(windows.is_dir(), "An existing Windows player directory is required")
    if app is not None:
        require(app.is_dir(), "An existing Mac app directory is required")
        require(app.name == APP_NAME + ".app", f"Mac app must be named {APP_NAME}.app")
    require(not within(output, windows) and (app is None or not within(output, app)),
            "Output must be outside the input player directories")
    revision, epoch = source_record(repo, args.source_revision)
    mac_revision = None
    if app is not None:
        mac_revision, _ = source_record(repo, args.mac_source_revision or revision)
    platform_revisions = {"Windows x64": revision}
    if mac_revision is not None:
        platform_revisions["macOS"] = mac_revision
    common_revision = revision if mac_revision in (None, revision) else None
    commit_time = dt.datetime.fromtimestamp(epoch, dt.timezone.utc)
    require(1980 <= commit_time.year <= 2107, "Commit timestamp is outside the ZIP range")
    timestamp = tuple(commit_time.timetuple()[:5]) + (commit_time.second // 2 * 2,)
    exe = windows / (APP_NAME + ".exe")
    check_windows_exe(exe)
    require((windows / "UnityPlayer.dll").is_file() and (windows / (APP_NAME + "_Data")).is_dir(), "Incomplete Windows player")
    require((windows / "MonoBleedingEdge").is_dir() or (windows / "GameAssembly.dll").is_file(), "Missing Windows scripting runtime")
    architectures, minimum_os = [], None
    if app is not None:
        with (app / "Contents/Info.plist").open("rb") as stream:
            plist = plistlib.load(stream)
        executable_name = plist.get("CFBundleExecutable", "")
        require(executable_name and Path(executable_name).name == executable_name, "Invalid CFBundleExecutable")
        executable = app / "Contents/MacOS" / executable_name
        architectures = mac_architectures(executable)
        minimum_os = str(plist.get("LSMinimumSystemVersion", "unspecified"))
        require(minimum_os != "unspecified", "Mac bundle must declare its minimum OS version")
    entries: list[dict] = []
    # Explorer already offers a destination named after the ZIP. Do not repeat
    # that name inside it. A Windows-only ZIP needs no platform folder either.
    root = ""
    windows_prefix = "" if args.windows_only else "Windows/"
    platforms = [windows_prefix] + (["macOS/"] if app is not None else [])
    for directory in ["Notices/"] + [platform for platform in platforms if platform]:
        entries.append(dict(path=directory, kind="directory", mode=stat.S_IFDIR | 0o755,
                            bytes=0, sha256=digest(b""), inline=b""))
    roots = [APP_NAME + ".exe", "UnityPlayer.dll", "UnityCrashHandler64.exe", APP_NAME + "_Data",
             "MonoBleedingEdge", "GameAssembly.dll", "D3D12", "WinPixEventRuntime.dll"]
    for name in roots:
        path = windows / name
        if path.exists():
            collect_tree(path, windows_prefix + name, windows, False, entries)
    if app is not None:
        collect_tree(app, root + "macOS/" + app.name, app, True, entries)
    source_notice = ("Caller-declared runtime source revision: " + revision if app is None else
                     "Caller-declared Windows runtime source revision: " + revision + "\n" +
                     "Caller-declared Mac runtime source revision: " + mac_revision)
    replacements = {"@@SOURCE_URL@@": SOURCE_URL, "@@SOURCE_REVISION@@": revision,
                    "@@WINDOWS_SOURCE_REVISION@@": revision, "@@MAC_SOURCE_REVISION@@": mac_revision or "",
                    "@@RUNTIME_SOURCE_NOTICE@@": source_notice,
                    "@@MAC_ARCHITECTURES@@": " and ".join(architectures), "@@MAC_MINIMUM_VERSION@@": minimum_os or "",
                    "@@MAC_SIGNING_STATUS@@": args.mac_signing_status}
    template_dir = repo / "Packaging/Desktop"
    readme_template = "READ_ME_WINDOWS.txt" if args.windows_only else "READ_ME.txt"
    for source, target in ((readme_template, "READ_ME.txt"), ("NOTICES.txt", "Notices/NOTICES.txt")):
        text = (template_dir / source).read_text(encoding="utf-8")
        for before, after in replacements.items():
            text = text.replace(before, after)
        require("@@" not in text, f"Unresolved template token in {source}")
        add_text(entries, root + target, text)
    add_text(entries, root + "Notices/SXS0305.txt", (repo / "Docs/Data/SXS0305/LICENSE.txt").read_text(encoding="utf-8"))
    reference = (repo / "Docs/M3_OPTICAL_REFERENCE.md").read_text(encoding="utf-8")
    match = re.search(r"## Upstream reference license.*?```text\s*\n(.*?)\n```", reference, re.S)
    require(match is not None and "Copyright (c) 2020 Eric Bruneton" in match.group(1), "Bruneton notice not found")
    add_text(entries, root + "Notices/Bruneton-BSD-3-Clause.txt", match.group(1) + "\n")
    entries.sort(key=lambda item: item["path"])
    require(len({entry["path"].casefold() for entry in entries}) == len(entries), "Duplicate/case-colliding package entries")
    for platform in platforms:
        require(sum(entry["kind"] == "file" and entry["path"].startswith(root + platform) for entry in entries) >= 10,
                f"Unexpectedly small runtime inventory: {platform}")
    zip_path = output / (args.package_name + ".zip")
    manifest_path = output / (args.package_name + ".manifest.json")
    summary_path = output / (args.package_name + ".summary.json")
    extracted = output / (args.package_name + "_verified")
    longest_entry = max(entries, key=lambda entry: path_length(entry["path"].rstrip("/")))
    longest_relative = path_length(longest_entry["path"].rstrip("/"))
    # Also keep the generated verification copy usable by Windows Explorer.
    # Check before writing anything: a successful Python extraction alone can
    # hide paths that legacy GUI extractors cannot handle.
    longest_extracted = max(path_length(str(extracted.joinpath(*PurePosixPath(entry["path"]).parts)))
                            for entry in entries)
    require(longest_extracted <= PATH_BUDGET,
            f"Verification path would be {longest_extracted} UTF-16 units (budget {PATH_BUDGET}). "
            "Use a shorter --output directory and --package-name.")
    for path in (zip_path, manifest_path, summary_path, extracted):
        require(not path.exists(), f"Refusing to overwrite: {path}")
    output.mkdir(parents=True, exist_ok=True)
    write_zip(zip_path, entries, timestamp)
    validate_zip(zip_path, entries)
    extracted_inventory, deferred_links = extract_and_inventory(zip_path, extracted)
    expected_extraction = [{key: value for key, value in public_entry(entry).items() if key != "mode"} for entry in entries]
    require(sorted(extracted_inventory + deferred_links, key=lambda item: item["path"]) == expected_extraction,
            "Independent extracted file inventory and ZIP link records do not match source payload")
    manifest = dict(schema="dtet.desktop-package.v2", packageName=args.package_name,
                    archiveLayout="windows-flat" if args.windows_only else "platform-folders",
                    includedPlatforms=["Windows x64"] + (["macOS"] if app is not None else []),
                    callerDeclaredRuntimeSourceRevision=common_revision,
                    callerDeclaredPlatformRuntimeSourceRevisions=platform_revisions, sourceUrl=SOURCE_URL,
                    files=[public_entry(entry) for entry in entries])
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    summary = dict(success=True, package=str(zip_path), bytes=zip_path.stat().st_size, sha256=file_hash(zip_path),
                   archiveLayout=manifest["archiveLayout"],
                   longestRelativePath=longest_entry["path"].rstrip("/"),
                   longestRelativePathUtf16=longest_relative,
                   longestVerifiedPathUtf16=longest_extracted,
                   conservativePathBudgetUtf16=PATH_BUDGET,
                   recommendedMaximumExtractionDirectoryUtf16=PATH_BUDGET - 1 - longest_relative,
                   includedPlatforms=manifest["includedPlatforms"], macIncluded=app is not None,
                   fileCount=sum(entry["kind"] == "file" for entry in entries),
                   symlinkCount=sum(entry["kind"] == "symlink" for entry in entries),
                   extractedSymlinkCount=sum(entry["kind"] == "symlink" for entry in extracted_inventory),
                   deferredSymbolicLinks=deferred_links,
                   extractedBundleComplete=not deferred_links,
                   macExecutableEntries=sum(entry["kind"] == "file" and entry["path"].startswith(root + "macOS/")
                                            and bool(entry["mode"] & 0o111) for entry in entries),
                   macArchitectures=architectures, macMinimumVersion=minimum_os,
                   callerDeclaredMacSigningStatus=args.mac_signing_status if app is not None else None,
                   macRuntimeTested=False, macSignatureVerified=False,
                   callerDeclaredRuntimeSourceRevision=common_revision,
                   callerDeclaredPlatformRuntimeSourceRevisions=platform_revisions,
                   verification=["All ZIP paths, sizes and SHA-256 hashes match the input inventory",
                                 "ZIP Unix executable and symbolic-link attributes match the intended payload",
                                 "Independent extracted file/directory inventory and separately read ZIP link records match",
                                 "Windows x64 PE header inspected"]
                                 + (["Mac Mach-O architecture headers inspected"] if app is not None else []),
                   extractedDirectory=str(extracted), manifest=str(manifest_path),
                   limitations=["Packaging does not execute the included player(s)",
                                "Source revision is supplied by the release operator"]
                   + (["Mac signing status is supplied by the release operator; packaging does not validate a Mac signature/notarization",
                       "Unix permissions are verified in ZIP metadata; Windows cannot exercise macOS launch semantics"]
                      if app is not None else ["This explicit Windows-only package contains no Mac application"])
                   + (["Windows denied symbolic-link creation (1314); those links remain intact in the ZIP but are absent from the verification extraction"]
                      if deferred_links else []))
    summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()

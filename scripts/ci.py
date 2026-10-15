#!/usr/bin/env python3
"""Shared CI version, archive and native CLI checks (standard library only)."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
SEMVER = re.compile(
    r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    r"(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
)


def resolve_version(event, ref_type, ref_name):
    # Docker tags (main, dev, pr-123) are independent of the assembly version.
    if event == "release" or (event == "workflow_dispatch" and ref_type == "tag"):
        version = ref_name.removeprefix("v")
    else:
        version = ET.parse(ROOT / "template_builder.csproj").findtext(".//Version")
    match = SEMVER.fullmatch(version or "")
    if not match or any(
        part.isdigit() and len(part) > 1 and part.startswith("0")
        for part in (match.group(4) or "").split(".")
    ):
        raise ValueError(f"Invalid application SemVer: {version!r}")
    return version


def sha256(path):
    with path.open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def package(runtime, publish_dir):
    publish_dir = Path(publish_dir)
    folder = publish_dir / f"template_builder-{runtime}"
    binary = folder / ("template_builder.exe" if runtime.startswith("win-") else "template_builder")
    if not binary.is_file():
        raise FileNotFoundError(binary)
    for file in folder.rglob("*"):
        if file.is_file() and (file.suffix in (".pdb", ".xml") or file.name == ".DS_Store"):
            file.unlink()
    # All platforms preserve the same top-level directory.
    archive = Path(shutil.make_archive(
        str(folder), "zip" if runtime.startswith("win-") else "gztar",
        root_dir=publish_dir, base_dir=folder.name,
    ))
    archive.with_name(archive.name + ".sha256").write_text(
        f"{sha256(archive)}  {archive.name}\n", encoding="ascii"
    )
    return archive


def smoke(command, work_dir, version, pdf=False):
    work_dir = Path(work_dir).resolve()
    work_dir.mkdir(parents=True, exist_ok=True)

    def run(*args, expected=0):
        result = subprocess.run(
            [*command, *map(str, args)], cwd=work_dir, capture_output=True,
            text=True, encoding="utf-8", errors="replace", timeout=180,
        )
        print(result.stdout, end="")
        print(result.stderr, end="", file=sys.stderr)
        if result.returncode != expected:
            raise RuntimeError(f"{result.args}: expected exit {expected}, got {result.returncode}")
        return result.stdout

    actual_version = run("--version").strip().split("+", 1)[0]
    if actual_version != version:
        raise RuntimeError(f"Expected version {version}, got {actual_version}")
    run("--help")
    source = work_dir / "src" / "算法 [sample]"
    source.mkdir(parents=True, exist_ok=True)
    (source / "main.cpp").write_text("// 中文注释\nint main() { return 0; }\n", encoding="utf-8")
    config = work_dir / "config.jsonc"
    run("init", "-o", config, "--force")
    if not (work_dir / "DefaultConfig.schema.json").is_file():
        raise RuntimeError("init did not create the embedded schema")
    report = json.loads(run("validate", "-s", work_dir / "src", "-c", config, "--format", "json"))
    if not report["overallOk"] or report["errorCount"] != 0:
        raise RuntimeError(f"Validation failed: {report}")
    rejected = json.loads(run("validate", "-s", work_dir / "missing", "-c", config, "--format", "json", expected=2))
    if rejected["overallOk"] or rejected["errorCount"] == 0:
        raise RuntimeError(f"Missing source did not produce a validation error: {rejected}")
    if pdf:
        run("build", "-s", work_dir / "src", "-c", config, "-o", work_dir / "output.pdf", "--sample", "1")
        if not (work_dir / "output.pdf").read_bytes().startswith(b"%PDF-"):
            raise RuntimeError("Invalid PDF header")


def verify(runtime, publish_dir, version):
    publish_dir = Path(publish_dir)
    suffix = ".zip" if runtime.startswith("win-") else ".tar.gz"
    archive = publish_dir / f"template_builder-{runtime}{suffix}"
    expected = archive.with_name(archive.name + ".sha256").read_text(encoding="ascii").strip()
    actual = f"{sha256(archive)}  {archive.name}"
    if expected != actual:
        raise RuntimeError(f"Checksum mismatch: {archive}")
    with tempfile.TemporaryDirectory(prefix="ci-unpack-") as unpack:
        if suffix == ".zip":
            shutil.unpack_archive(archive, unpack)
        else:
            shutil.unpack_archive(archive, unpack, filter="data")
        binary = Path(unpack) / f"template_builder-{runtime}" / (
            "template_builder.exe" if runtime.startswith("win-") else "template_builder"
        )
        if not binary.is_file():
            raise FileNotFoundError(binary)
        smoke([str(binary)], publish_dir / "verify_smoke", version)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("version")
    for name in ("package", "verify"):
        subparser = commands.add_parser(name)
        subparser.add_argument("runtime")
        subparser.add_argument("--publish-dir", default="publish")
        if name == "verify":
            subparser.add_argument("--version", required=True)
    smoke_parser = commands.add_parser("smoke")
    smoke_parser.add_argument("--version", required=True)
    smoke_parser.add_argument("--work-dir", required=True)
    smoke_parser.add_argument("--pdf", action="store_true")
    smoke_parser.add_argument("executable", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.command == "version":
        version = resolve_version(os.getenv("GITHUB_EVENT_NAME"), os.getenv("GITHUB_REF_TYPE"), os.getenv("GITHUB_REF_NAME", ""))
        if os.getenv("GITHUB_EVENT_NAME") == "release":
            event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text(encoding="utf-8"))
            if "-" in version and not event["release"]["prerelease"]:
                raise ValueError("A prerelease version must be marked as a prerelease on GitHub")
        print(version)
    elif args.command == "package":
        print(package(args.runtime, args.publish_dir))
    elif args.command == "verify":
        verify(args.runtime, args.publish_dir, args.version)
    else:
        executable = args.executable
        if executable and executable[0] == "--":
            executable = executable[1:]
        if not executable:
            parser.error("smoke requires an executable after --")
        smoke(executable, args.work_dir, args.version, args.pdf)


if __name__ == "__main__":
    # Redirected Windows streams can default to cp1252 even though the app emits UTF-8.
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    main()

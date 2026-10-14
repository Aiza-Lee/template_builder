"""Regression checks for version resolution and release archive contracts."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location("ci", Path(__file__).resolve().parents[2] / "scripts/ci.py")
ci = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ci)


class VersionTests(unittest.TestCase):
    def test_branch_and_pr_use_project_version(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(ci, "ROOT", Path(tmp)):
            (Path(tmp) / "template_builder.csproj").write_text(
                "<Project><PropertyGroup><Version>3.1.4-rc.2</Version></PropertyGroup></Project>",
                encoding="utf-8",
            )
            for event, ref_type, ref in (
                ("push", "branch", "main"),
                ("pull_request", "branch", "123/merge"),
                ("workflow_dispatch", "branch", "feature/test"),
            ):
                with self.subTest(event=event):
                    self.assertEqual("3.1.4-rc.2", ci.resolve_version(event, ref_type, ref))

    def test_release_and_manual_tag_use_validated_tag(self):
        for event in ("release", "workflow_dispatch"):
            for tag in ("v2.1.0", "v2.1.0-rc.2", "2.1.0-beta.1"):
                with self.subTest(event=event, tag=tag):
                    self.assertEqual(tag.removeprefix("v"), ci.resolve_version(event, "tag", tag))

    def test_rejects_invalid_release_versions_before_build(self):
        for tag in ("main", "v2", "v2.0", "v02.0.0", "v2.0.0-rc.01", "v2.0.0\n-p:X=1"):
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                ci.resolve_version("release", "tag", tag)

    def test_prerelease_cannot_publish_stable_tags(self):
        with tempfile.TemporaryDirectory() as tmp:
            event_path = Path(tmp) / "event.json"
            event_path.write_text(json.dumps({"release": {"prerelease": False}}), encoding="utf-8")
            with patch.dict(os.environ, {
                "GITHUB_EVENT_NAME": "release", "GITHUB_REF_TYPE": "tag",
                "GITHUB_REF_NAME": "v2.1.0-rc.1", "GITHUB_EVENT_PATH": str(event_path),
            }), patch("sys.argv", ["ci.py", "version"]):
                with self.assertRaisesRegex(ValueError, "marked as a prerelease"):
                    ci.main()


class ArchiveTests(unittest.TestCase):
    def test_tar_and_zip_verify_the_actual_nested_executable(self):
        for runtime in ("linux-x64", "linux-arm64", "osx-x64", "osx-arm64", "win-x64"):
            with self.subTest(runtime=runtime), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                folder = root / f"template_builder-{runtime}"
                folder.mkdir()
                name = "template_builder.exe" if runtime.startswith("win-") else "template_builder"
                binary = folder / name
                binary.write_bytes(b"binary fixture")
                binary.chmod(0o755)
                (folder / "debug.pdb").write_bytes(b"debug")
                ci.package(runtime, root)
                self.assertFalse((folder / "debug.pdb").exists())

                def check_smoke(command, work_dir, version):
                    self.assertEqual(Path(command[0]).parent.name, folder.name)
                    self.assertEqual(Path(command[0]).name, name)
                    self.assertEqual(Path(command[0]).read_bytes(), b"binary fixture")
                    if not runtime.startswith("win-"):
                        self.assertTrue(Path(command[0]).stat().st_mode & 0o111)
                    self.assertEqual(version, "2.1.0-rc.1")

                with patch.object(ci, "smoke", side_effect=check_smoke) as smoke:
                    ci.verify(runtime, root, "2.1.0-rc.1")
                    smoke.assert_called_once()

    def test_modified_archive_fails_checksum(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp) / "template_builder-linux-x64"
            folder.mkdir()
            (folder / "template_builder").write_bytes(b"fixture")
            archive = ci.package("linux-x64", tmp)
            with archive.open("ab") as output:
                output.write(b"corruption")
            with self.assertRaisesRegex(RuntimeError, "Checksum mismatch"):
                ci.verify("linux-x64", tmp, "2.1.0")

    def test_missing_binary_fails_before_packaging(self):
        with tempfile.TemporaryDirectory() as tmp:
            with self.assertRaises(FileNotFoundError):
                ci.package("win-x64", tmp)


class SmokeTests(unittest.TestCase):
    def test_nonzero_native_exit_is_not_masked_by_later_commands(self):
        with tempfile.TemporaryDirectory() as tmp:
            result = subprocess.CompletedProcess(["fixture"], 7, "", "failed")
            with patch.object(ci.subprocess, "run", return_value=result) as run, patch("builtins.print"):
                with self.assertRaisesRegex(RuntimeError, "got 7"):
                    ci.smoke(["fixture"], tmp, "2.1.0")
                run.assert_called_once()


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

import os
from pathlib import Path
import shutil
import subprocess
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace


class LockfileNormalizationTests(unittest.TestCase):
    @unittest.skipUnless(os.name == "nt", "the repository bootstrap targets Windows")
    def test_root_restore_normalizer_is_atomic_idempotent_and_fail_closed(self) -> None:
        powershell = shutil.which("powershell.exe")
        git = shutil.which("git.exe") or shutil.which("git")
        if powershell is None or git is None:
            self.skipTest("PowerShell and Git are required")

        with TemporaryWorkspace() as workspace:
            repository = workspace.path / "repository"
            lock_path = repository / "src" / "Product" / "packages.lock.json"
            lock_path.parent.mkdir(parents=True)
            (repository / ".gitattributes").write_text(
                "*.json text eol=lf\n",
                encoding="utf-8",
                newline="\n",
            )
            crlf = b'{\r\n  "version": 2,\r\n  "dependencies": {}\r\n}\r\n'
            lock_path.write_bytes(crlf)
            self._run([git, "init", "--quiet"], repository)
            self._run([git, "add", ".gitattributes", "src/Product/packages.lock.json"], repository)

            environment = dict(os.environ)
            environment["GONIEGONIE_COMMON_SCRIPT"] = str(
                REPOSITORY_ROOT / "scripts" / "common.ps1"
            )
            environment["GONIEGONIE_NORMALIZE_ROOT"] = str(repository)
            command = (
                ". $env:GONIEGONIE_COMMON_SCRIPT; "
                "Normalize-TrackedPackageLockLineEndings "
                "-RepositoryRoot $env:GONIEGONIE_NORMALIZE_ROOT"
            )
            first = self._run(
                [
                    powershell,
                    "-NoLogo",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    command,
                ],
                repository,
                environment,
            )
            self.assertIn("1 changed, 1 checked", first.stdout)
            expected = crlf.replace(b"\r\n", b"\n")
            self.assertEqual(expected, lock_path.read_bytes())

            second = self._run(
                [
                    powershell,
                    "-NoLogo",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    command,
                ],
                repository,
                environment,
            )
            self.assertIn("0 changed, 1 checked", second.stdout)
            self.assertEqual(expected, lock_path.read_bytes())

            mixed = b'{\r\n  "version": 2,\n  "dependencies": {}\r\n}\r\n'
            lock_path.write_bytes(mixed)
            rejected = subprocess.run(
                [
                    powershell,
                    "-NoLogo",
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    command,
                ],
                cwd=repository,
                env=environment,
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn("mixed LF and CRLF endings", rejected.stderr)
            self.assertEqual(mixed, lock_path.read_bytes())
            self.assertFalse(
                any(".goniegonie-" in path.name for path in repository.rglob("*"))
            )

    def _run(
        self,
        command: list[str],
        cwd: Path,
        environment: dict[str, str] | None = None,
    ) -> subprocess.CompletedProcess[str]:
        completed = subprocess.run(
            command,
            cwd=cwd,
            env=environment,
            capture_output=True,
            text=True,
            check=False,
        )
        self.assertEqual(
            0,
            completed.returncode,
            msg=f"command failed: {command}\n{completed.stdout}\n{completed.stderr}",
        )
        return completed


if __name__ == "__main__":
    unittest.main()

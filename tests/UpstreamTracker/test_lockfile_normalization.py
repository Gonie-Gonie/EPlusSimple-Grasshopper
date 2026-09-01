from __future__ import annotations

import base64
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import time
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace


CRLF_LOCK = b'{\r\n  "version": 2,\r\n  "dependencies": {}\r\n}\r\n'
LF_LOCK = CRLF_LOCK.replace(b"\r\n", b"\n")
MIXED_LOCK = b'{\r\n  "version": 2,\n  "dependencies": {}\r\n}\r\n'
CONCURRENT_CRLF_LOCK = (
    b'{\r\n  "version": 2,\r\n  "dependencies": {"Concurrent": {}}\r\n}\r\n'
)


@unittest.skipUnless(os.name == "nt", "the repository bootstrap targets Windows")
class LockfileNormalizationTests(unittest.TestCase):
    def test_success_is_idempotent_and_leaves_no_transaction_residue(self) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": CRLF_LOCK},
            )

            first = self._normalize(powershell, repository)
            self.assertEqual(0, first.returncode, self._failure_message(first))
            self.assertIn("1 changed, 1 checked", first.stdout)
            self.assertEqual(LF_LOCK, locks["src/Product/packages.lock.json"].read_bytes())
            self._assert_no_transaction_children(repository)

            second = self._normalize(powershell, repository)
            self.assertEqual(0, second.returncode, self._failure_message(second))
            self.assertIn("0 changed, 1 checked", second.stdout)
            self.assertEqual(LF_LOCK, locks["src/Product/packages.lock.json"].read_bytes())
            self._assert_no_transaction_children(repository)

    def test_preflight_rejects_later_mixed_lock_without_mutating_earlier_lock(self) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {
                    "src/A/packages.lock.json": CRLF_LOCK,
                    "src/Z/packages.lock.json": MIXED_LOCK,
                },
            )

            rejected = self._normalize(powershell, repository)

            self.assertNotEqual(0, rejected.returncode)
            self.assertIn("mixed LF and CRLF endings", self._combined_output(rejected))
            self.assertEqual(CRLF_LOCK, locks["src/A/packages.lock.json"].read_bytes())
            self.assertEqual(MIXED_LOCK, locks["src/Z/packages.lock.json"].read_bytes())
            self._assert_no_transaction_children(repository)

    def test_later_replace_failure_rolls_back_entire_batch_and_cleans_transaction(
        self,
    ) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {
                    "src/A/packages.lock.json": CRLF_LOCK,
                    "src/Z/packages.lock.json": CRLF_LOCK,
                },
            )
            environment = self._powershell_environment(repository)
            environment["DRAGONS_HELD_LOCK"] = str(
                locks["src/Z/packages.lock.json"]
            )
            marker = workspace.path / "replace-holder.ready"
            environment["DRAGONS_HOLDER_READY"] = str(marker)
            holder_command = (
                "$ErrorActionPreference = 'Stop'; "
                "$stream = New-Object System.IO.FileStream("
                "$env:DRAGONS_HELD_LOCK, "
                "[System.IO.FileMode]::Open, "
                "[System.IO.FileAccess]::Read, "
                "[System.IO.FileShare]::Read); "
                "try { "
                "$null = New-Item -ItemType File -Path $env:DRAGONS_HOLDER_READY; "
                "$null = [Console]::In.ReadLine() "
                "} finally { $stream.Dispose() }"
            )
            holder = self._start_holder(
                powershell,
                repository,
                environment,
                holder_command,
                marker,
            )
            try:
                rejected = self._normalize(powershell, repository)
            finally:
                holder_result = self._stop_holder(holder)

            self.assertEqual(0, holder_result.returncode, self._failure_message(holder_result))
            self.assertNotEqual(0, rejected.returncode)
            self.assertEqual(CRLF_LOCK, locks["src/A/packages.lock.json"].read_bytes())
            self.assertEqual(CRLF_LOCK, locks["src/Z/packages.lock.json"].read_bytes())
            self._assert_no_transaction_children(repository)

    def test_failed_action_is_reported_after_its_crlf_output_is_normalized(self) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": LF_LOCK},
            )
            environment = self._powershell_environment(repository)
            environment["DRAGONS_ACTION_LOCK"] = str(
                locks["src/Product/packages.lock.json"]
            )
            environment["DRAGONS_ACTION_BYTES"] = base64.b64encode(CRLF_LOCK).decode(
                "ascii"
            )
            command = (
                ". $env:DRAGONS_COMMON_SCRIPT; "
                "Invoke-WithTrackedPackageLockNormalization "
                "-RepositoryRoot $env:DRAGONS_NORMALIZE_ROOT "
                "-Action { "
                "[System.IO.File]::WriteAllBytes("
                "$env:DRAGONS_ACTION_LOCK, "
                "[System.Convert]::FromBase64String($env:DRAGONS_ACTION_BYTES)); "
                "throw 'synthetic restore failure' "
                "}"
            )

            rejected = self._run_powershell(
                powershell,
                repository,
                environment,
                command,
            )

            self.assertNotEqual(0, rejected.returncode)
            self.assertIn("synthetic restore failure", self._combined_output(rejected))
            self.assertEqual(LF_LOCK, locks["src/Product/packages.lock.json"].read_bytes())
            self._assert_no_transaction_children(repository)

    def test_concurrent_workflow_is_rejected_without_mutation(self) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": CRLF_LOCK},
            )
            environment = self._powershell_environment(repository)
            marker = workspace.path / "workflow-holder.ready"
            environment["DRAGONS_HOLDER_READY"] = str(marker)
            holder_command = (
                "$ErrorActionPreference = 'Stop'; "
                ". $env:DRAGONS_COMMON_SCRIPT; "
                "$workflow = Enter-TrackedPackageLockWorkflow "
                "-RepositoryRoot $env:DRAGONS_NORMALIZE_ROOT; "
                "try { "
                "$null = New-Item -ItemType File -Path $env:DRAGONS_HOLDER_READY; "
                "$null = [Console]::In.ReadLine() "
                "} finally { $workflow.Dispose() }"
            )
            holder = self._start_holder(
                powershell,
                repository,
                environment,
                holder_command,
                marker,
            )
            try:
                rejected = self._normalize(powershell, repository)
            finally:
                holder_result = self._stop_holder(holder)

            self.assertEqual(0, holder_result.returncode, self._failure_message(holder_result))
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn("already running", self._combined_output(rejected))
            self.assertEqual(CRLF_LOCK, locks["src/Product/packages.lock.json"].read_bytes())
            self._assert_no_transaction_children(repository)

    def test_stale_transaction_fails_closed_and_is_preserved(self) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": CRLF_LOCK},
            )
            stale = (
                repository
                / ".tools"
                / "package-lock-normalization"
                / "stale-transaction"
            )
            stale.mkdir(parents=True)
            recovery = stale / "original.bin"
            recovery_bytes = b"recovery evidence\x00\xff"
            recovery.write_bytes(recovery_bytes)

            rejected = self._normalize(powershell, repository)

            self.assertNotEqual(0, rejected.returncode)
            self.assertIn(
                "incomplete NuGet lock-file normalization transaction",
                self._combined_output(rejected),
            )
            self.assertEqual(CRLF_LOCK, locks["src/Product/packages.lock.json"].read_bytes())
            self.assertTrue(stale.is_dir())
            self.assertEqual(recovery_bytes, recovery.read_bytes())

    def test_commit_time_concurrent_update_is_restored_and_retained_for_recovery(
        self,
    ) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": CRLF_LOCK},
            )
            lock_path = locks["src/Product/packages.lock.json"]
            environment = self._powershell_environment(repository)
            environment["DRAGONS_CONCURRENT_BYTES"] = base64.b64encode(
                CONCURRENT_CRLF_LOCK
            ).decode("ascii")
            command = (
                ". $env:DRAGONS_COMMON_SCRIPT; "
                "$script:DragonsCommitReplaceCalls = 0; "
                "function Invoke-PackageLockCommitReplace { "
                "param("
                "[string] $SourcePath, "
                "[string] $DestinationPath, "
                "[string] $BackupPath); "
                "$script:DragonsCommitReplaceCalls += 1; "
                "if ($script:DragonsCommitReplaceCalls -eq 1) { "
                "[System.IO.File]::WriteAllBytes("
                "$DestinationPath, "
                "[System.Convert]::FromBase64String("
                "$env:DRAGONS_CONCURRENT_BYTES)) "
                "}; "
                "[System.IO.File]::Replace("
                "$SourcePath, $DestinationPath, $BackupPath, $true) "
                "}; "
                "Normalize-TrackedPackageLockLineEndings "
                "-RepositoryRoot $env:DRAGONS_NORMALIZE_ROOT"
            )

            rejected = self._run_powershell(
                powershell,
                repository,
                environment,
                command,
            )

            self.assertNotEqual(0, rejected.returncode)
            self.assertIn(
                "preserving a concurrent update",
                self._combined_output(rejected),
            )
            self.assertIn("Recovery files were retained", self._combined_output(rejected))
            self.assertEqual(CONCURRENT_CRLF_LOCK, lock_path.read_bytes())
            transaction_root = repository / ".tools" / "package-lock-normalization"
            transaction_children = list(transaction_root.iterdir())
            self.assertEqual(1, len(transaction_children))
            self.assertTrue(transaction_children[0].is_dir())
            replace_backups = list(transaction_children[0].glob("*.replace.bak"))
            self.assertEqual(1, len(replace_backups))
            self.assertEqual(CONCURRENT_CRLF_LOCK, replace_backups[0].read_bytes())
            manifest = json.loads(
                (transaction_children[0] / "transaction.json").read_text(
                    encoding="utf-8"
                )
            )
            self.assertEqual(1, len(manifest["files"]))
            manifest_entry = manifest["files"][0]
            self.assertEqual(replace_backups[0].name, manifest_entry["replaceBackup"])
            discard = transaction_children[0] / manifest_entry["rollbackDiscard"]
            self.assertEqual(LF_LOCK, discard.read_bytes())

            shutil.rmtree(transaction_root)
            self.assertFalse(transaction_root.exists())

    def test_fresh_repository_what_if_creates_no_operational_directories(self) -> None:
        with TemporaryWorkspace() as workspace:
            repository, locks, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": CRLF_LOCK},
            )
            environment = self._powershell_environment(repository)
            command = (
                ". $env:DRAGONS_COMMON_SCRIPT; "
                "Normalize-TrackedPackageLockLineEndings "
                "-RepositoryRoot $env:DRAGONS_NORMALIZE_ROOT "
                "-WhatIf"
            )

            preview = self._run_powershell(
                powershell,
                repository,
                environment,
                command,
            )

            self.assertEqual(0, preview.returncode, self._failure_message(preview))
            self.assertIn("What if: normalize tracked NuGet lock file", preview.stdout)
            self.assertEqual(CRLF_LOCK, locks["src/Product/packages.lock.json"].read_bytes())
            self.assertFalse((repository / ".tools" / "package-lock-workflow").exists())
            self.assertFalse(
                (repository / ".tools" / "package-lock-normalization").exists()
            )

    def test_temp_clean_is_rejected_while_package_lock_workflow_is_running(self) -> None:
        with TemporaryWorkspace() as workspace:
            repository, _, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": LF_LOCK},
            )
            fixture_scripts = repository / "scripts"
            fixture_scripts.mkdir()
            shutil.copy2(REPOSITORY_ROOT / "scripts" / "common.ps1", fixture_scripts)
            shutil.copy2(REPOSITORY_ROOT / "scripts" / "clean.ps1", fixture_scripts)
            shutil.copy2(REPOSITORY_ROOT / "global.json", repository)
            shutil.copy2(REPOSITORY_ROOT / "NuGet.config", repository)
            sentinel = repository / "temp" / "keep" / "sentinel.txt"
            sentinel.parent.mkdir(parents=True)
            sentinel_bytes = b"do not delete while normalization is running\r\n"
            sentinel.write_bytes(sentinel_bytes)

            environment = self._powershell_environment(repository)
            environment["DRAGONS_CLEAN_SCRIPT"] = str(
                fixture_scripts / "clean.ps1"
            )
            marker = workspace.path / "clean-holder.ready"
            environment["DRAGONS_HOLDER_READY"] = str(marker)
            holder_command = (
                "$ErrorActionPreference = 'Stop'; "
                ". $env:DRAGONS_COMMON_SCRIPT; "
                "$workflow = Enter-TrackedPackageLockWorkflow "
                "-RepositoryRoot $env:DRAGONS_NORMALIZE_ROOT; "
                "try { "
                "$null = New-Item -ItemType File -Path $env:DRAGONS_HOLDER_READY; "
                "$null = [Console]::In.ReadLine() "
                "} finally { $workflow.Dispose() }"
            )
            holder = self._start_holder(
                powershell,
                repository,
                environment,
                holder_command,
                marker,
            )
            try:
                workflow_lock = (
                    repository
                    / ".tools"
                    / "package-lock-workflow"
                    / "workflow.lock"
                )
                self.assertTrue(workflow_lock.is_file())
                self.assertFalse(
                    (repository / "temp" / "package-lock-workflow").exists()
                )
                rejected = self._run_powershell(
                    powershell,
                    repository,
                    environment,
                    "& $env:DRAGONS_CLEAN_SCRIPT -TempOnly -Confirm:$false",
                )
            finally:
                holder_result = self._stop_holder(holder)

            self.assertEqual(0, holder_result.returncode, self._failure_message(holder_result))
            self.assertNotEqual(0, rejected.returncode)
            self.assertIn("already running", self._combined_output(rejected))
            self.assertTrue(sentinel.is_file())
            self.assertEqual(sentinel_bytes, sentinel.read_bytes())

    def test_temp_clean_removes_temp_and_preserves_stale_normalization_recovery(
        self,
    ) -> None:
        with TemporaryWorkspace() as workspace:
            repository, _, powershell = self._create_repository(
                workspace,
                {"src/Product/packages.lock.json": LF_LOCK},
            )
            fixture_scripts = repository / "scripts"
            fixture_scripts.mkdir()
            shutil.copy2(REPOSITORY_ROOT / "scripts" / "common.ps1", fixture_scripts)
            shutil.copy2(REPOSITORY_ROOT / "scripts" / "clean.ps1", fixture_scripts)
            shutil.copy2(REPOSITORY_ROOT / "global.json", repository)
            shutil.copy2(REPOSITORY_ROOT / "NuGet.config", repository)
            recovery = (
                repository
                / ".tools"
                / "package-lock-normalization"
                / "interrupted-transaction"
                / "0000.replace.bak"
            )
            recovery.parent.mkdir(parents=True)
            recovery_bytes = b"captured concurrent package-lock update\r\n\x00\xff"
            recovery.write_bytes(recovery_bytes)
            sentinel = repository / "temp" / "unrelated" / "sentinel.txt"
            sentinel.parent.mkdir(parents=True)
            sentinel_bytes = b"fully disposable temp tree\n"
            sentinel.write_bytes(sentinel_bytes)
            environment = self._powershell_environment(repository)
            environment["DRAGONS_CLEAN_SCRIPT"] = str(
                fixture_scripts / "clean.ps1"
            )

            completed = self._run_powershell(
                powershell,
                repository,
                environment,
                "& $env:DRAGONS_CLEAN_SCRIPT -TempOnly -Confirm:$false",
            )

            self.assertEqual(0, completed.returncode, self._failure_message(completed))
            self.assertIn("Removed disposable tree", completed.stdout)
            self.assertTrue(recovery.is_file())
            self.assertEqual(recovery_bytes, recovery.read_bytes())
            self.assertFalse(sentinel.exists())
            self.assertFalse((repository / "temp").exists())

    def test_setup_and_build_wrap_restore_with_failure_safe_normalization(self) -> None:
        wrapper_pattern = re.compile(
            r"Invoke-WithTrackedPackageLockNormalization\s+`?\s*"
            r"-RepositoryRoot\s+\$repositoryRoot\s+`?\s*"
            r"-Action\s*\{[\s\S]{0,1200}?"
            r"Invoke-LoggedNativeCommand[\s\S]{0,600}?['\"]restore['\"]",
            re.MULTILINE,
        )
        direct_adjacency = re.compile(
            r"Invoke-LoggedNativeCommand[\s\S]{0,1000}?['\"]restore['\"]"
            r"[\s\S]{0,1000}?Normalize-TrackedPackageLockLineEndings",
            re.MULTILINE,
        )

        for relative_path in ("scripts/setup.ps1", "scripts/build.ps1"):
            with self.subTest(script=relative_path):
                source = (REPOSITORY_ROOT / relative_path).read_text(encoding="utf-8")
                self.assertEqual(
                    1,
                    source.count("Invoke-WithTrackedPackageLockNormalization"),
                )
                self.assertRegex(source, wrapper_pattern)
                self.assertNotIn("Normalize-TrackedPackageLockLineEndings", source)
                self.assertIsNone(direct_adjacency.search(source))

    def _create_repository(
        self,
        workspace: TemporaryWorkspace,
        lock_bytes: dict[str, bytes],
    ) -> tuple[Path, dict[str, Path], str]:
        powershell = shutil.which("powershell.exe")
        git = shutil.which("git.exe") or shutil.which("git")
        if powershell is None or git is None:
            self.skipTest("PowerShell and Git are required")

        repository = workspace.path / "repository"
        repository.mkdir()
        (repository / ".gitattributes").write_text(
            "*.json text eol=lf\n",
            encoding="utf-8",
            newline="\n",
        )
        paths: dict[str, Path] = {}
        for relative_path, payload in lock_bytes.items():
            path = repository.joinpath(*relative_path.split("/"))
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(payload)
            paths[relative_path] = path

        self._run([git, "init", "--quiet"], repository)
        self._run(
            [git, "add", ".gitattributes", *lock_bytes.keys()],
            repository,
        )
        return repository, paths, powershell

    def _normalize(
        self,
        powershell: str,
        repository: Path,
    ) -> subprocess.CompletedProcess[str]:
        environment = self._powershell_environment(repository)
        command = (
            ". $env:DRAGONS_COMMON_SCRIPT; "
            "Normalize-TrackedPackageLockLineEndings "
            "-RepositoryRoot $env:DRAGONS_NORMALIZE_ROOT"
        )
        return self._run_powershell(
            powershell,
            repository,
            environment,
            command,
        )

    def _powershell_environment(self, repository: Path) -> dict[str, str]:
        environment = dict(os.environ)
        environment["DRAGONS_COMMON_SCRIPT"] = str(
            REPOSITORY_ROOT / "scripts" / "common.ps1"
        )
        environment["DRAGONS_NORMALIZE_ROOT"] = str(repository)
        return environment

    def _run_powershell(
        self,
        powershell: str,
        repository: Path,
        environment: dict[str, str],
        command: str,
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
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

    def _start_holder(
        self,
        powershell: str,
        repository: Path,
        environment: dict[str, str],
        command: str,
        marker: Path,
    ) -> subprocess.Popen[str]:
        holder = subprocess.Popen(
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
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
        )
        deadline = time.monotonic() + 10.0
        while not marker.exists():
            if holder.poll() is not None:
                completed = self._stop_holder(holder)
                self.fail(
                    f"holder exited before readiness: {self._failure_message(completed)}"
                )
            if time.monotonic() >= deadline:
                holder.kill()
                completed = self._stop_holder(holder)
                self.fail(
                    f"holder did not become ready: {self._failure_message(completed)}"
                )
            time.sleep(0.025)
        return holder

    def _stop_holder(
        self,
        holder: subprocess.Popen[str],
    ) -> subprocess.CompletedProcess[str]:
        try:
            stdout, stderr = holder.communicate(
                input="\n" if holder.poll() is None else None,
                timeout=10,
            )
        except subprocess.TimeoutExpired:
            holder.kill()
            stdout, stderr = holder.communicate(timeout=5)
        return subprocess.CompletedProcess(
            args=holder.args,
            returncode=holder.returncode,
            stdout=stdout,
            stderr=stderr,
        )

    def _assert_no_transaction_children(self, repository: Path) -> None:
        transaction_root = repository / ".tools" / "package-lock-normalization"
        if transaction_root.exists():
            self.assertEqual([], list(transaction_root.iterdir()))

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

    @staticmethod
    def _combined_output(completed: subprocess.CompletedProcess[str]) -> str:
        return f"{completed.stdout}\n{completed.stderr}"

    @staticmethod
    def _failure_message(completed: subprocess.CompletedProcess[str]) -> str:
        return (
            f"command failed: {completed.args}\n"
            f"stdout:\n{completed.stdout}\n"
            f"stderr:\n{completed.stderr}"
        )


if __name__ == "__main__":
    unittest.main()

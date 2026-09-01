from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

from support import (
    REPOSITORY_ROOT,
    TOOL_ROOT,
    TemporaryWorkspace,
    bind_exception_hash,
    write_configuration,
)

from dragons_upstream_tracker.classifier import (
    ChangeClassification,
    compare_sources,
    inspect_source_identity,
)
from dragons_upstream_tracker.config import load_configuration
from dragons_upstream_tracker.errors import SourceError
from dragons_upstream_tracker.reporting import (
    render_json,
    render_markdown,
    render_sync_branch,
    write_reports,
)
from dragons_upstream_tracker.symbols import build_snapshot


class MappingAndReportTests(unittest.TestCase):
    def test_pinned_identity_ignores_inherited_git_directory(self) -> None:
        with TemporaryWorkspace() as real, TemporaryWorkspace() as fake:
            real.write("src/Service.cs", "class Real { }\n")
            fake.write("src/Service.cs", "class Forged { }\n")
            real_commit = self._commit_repository(real.path, "real")
            self._commit_repository(fake.path, "fake")
            subprocess.run(
                ["git", "remote", "add", "origin", "https://example.invalid/real.git"],
                cwd=real.path,
                check=True,
            )

            with patch.dict(
                os.environ,
                {"GIT_DIR": str(fake.path / ".git")},
                clear=False,
            ):
                identity = inspect_source_identity(
                    real.path,
                    expected_commit=real_commit,
                    expected_repository="https://example.invalid/real.git",
                )

            self.assertTrue(identity.pin_verified)
            self.assertEqual(real_commit, identity.commit)

    def test_pinned_identity_ignores_replacement_tree(self) -> None:
        with TemporaryWorkspace() as workspace:
            workspace.write("src/Service.cs", "class Real { }\n")
            commit = self._commit_repository(workspace.path, "real")
            subprocess.run(
                ["git", "remote", "add", "origin", "https://example.invalid/real.git"],
                cwd=workspace.path,
                check=True,
            )
            real_tree = self._git_output(workspace.path, "rev-parse", "HEAD^{tree}")
            workspace.write("src/Service.cs", "class Forged { }\n")
            subprocess.run(
                ["git", "add", "src/Service.cs"],
                cwd=workspace.path,
                check=True,
            )
            forged_tree = self._git_output(workspace.path, "write-tree")
            subprocess.run(
                [
                    "git",
                    "update-ref",
                    f"refs/replace/{real_tree}",
                    forged_tree,
                ],
                cwd=workspace.path,
                check=True,
            )
            subprocess.run(
                ["git", "read-tree", "HEAD"],
                cwd=workspace.path,
                check=True,
            )

            with self.assertRaisesRegex(SourceError, "clean"):
                inspect_source_identity(
                    workspace.path,
                    expected_commit=commit,
                    expected_repository="https://example.invalid/real.git",
                )

    def test_pinned_identity_rejects_hidden_tracked_changes(self) -> None:
        for flag in ("--assume-unchanged", "--skip-worktree"):
            with self.subTest(flag=flag), TemporaryWorkspace() as workspace:
                workspace.write("src/data.csv", "value\n1\n")
                commit = self._commit_repository(workspace.path, "real")
                subprocess.run(
                    ["git", "remote", "add", "origin", "https://example.invalid/real.git"],
                    cwd=workspace.path,
                    check=True,
                )
                workspace.write("src/data.csv", "value\n999\n")
                subprocess.run(
                    ["git", "update-index", flag, "src/data.csv"],
                    cwd=workspace.path,
                    check=True,
                )

                with self.assertRaisesRegex(SourceError, "clean"):
                    inspect_source_identity(
                        workspace.path,
                        expected_commit=commit,
                        expected_repository="https://example.invalid/real.git",
                    )

    def test_pinned_identity_rejects_untracked_files(self) -> None:
        with TemporaryWorkspace() as workspace:
            workspace.write("src/data.csv", "value\n1\n")
            commit = self._commit_repository(workspace.path, "real")
            subprocess.run(
                ["git", "remote", "add", "origin", "https://example.invalid/real.git"],
                cwd=workspace.path,
                check=True,
            )
            workspace.write("src/forged.py", "FORGED = True\n")

            with self.assertRaisesRegex(SourceError, "clean"):
                inspect_source_identity(
                    workspace.path,
                    expected_commit=commit,
                    expected_repository="https://example.invalid/real.git",
                )

    def test_exact_mapping_exception_and_reports_are_deterministic(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(
                workspace,
                exception_symbol="Service.run",
            )
            configuration = load_configuration(lock, port_map, exceptions)
            baseline = workspace.path / "baseline"
            current = workspace.path / "current"
            workspace.write(
                "baseline/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            workspace.write(
                "current/src/source/service.py",
                "class Service:\n    def run(self):\n        return 2\n",
            )

            baseline_snapshot = build_snapshot(
                baseline,
                configuration.tracked_paths,
                require_tracked_paths=True,
            )
            baseline_symbol = baseline_snapshot.files_by_path[
                "src/source/service.py"
            ].symbols_by_name["Service.run"]
            configuration = bind_exception_hash(
                configuration,
                "src/source/service.py",
                "Service.run",
                baseline_symbol.hash,
            )

            report = compare_sources(configuration, baseline, current)
            change = next(item for item in report.changes if item.symbol == "Service.run")

            self.assertEqual(ChangeClassification.BODY_CHANGED, change.classification)
            self.assertEqual("symbol", change.mappings[0].match)
            self.assertEqual(("ServiceParityTests",), report.impacted.tests)
            self.assertEqual(("reviewed-service-difference",), change.compatibility_exceptions)
            self.assertEqual(render_json(report), render_json(report))
            self.assertEqual(render_markdown(report), render_markdown(report))

            template = (TOOL_ROOT / "templates" / "sync-branch.md").read_text(encoding="utf-8")
            first_sync = render_sync_branch(report, template)
            second_sync = render_sync_branch(report, template)
            self.assertEqual(first_sync, second_sync)
            self.assertIn("sync/invisibledragon-simpledragon-", first_sync)

            output = workspace.path / "reports"
            first_paths = write_reports(report, output, REPOSITORY_ROOT, TOOL_ROOT / "templates" / "sync-branch.md")
            first_bytes = tuple(path.read_bytes() for path in first_paths)
            second_paths = write_reports(report, output, REPOSITORY_ROOT, TOOL_ROOT / "templates" / "sync-branch.md")
            self.assertEqual(first_bytes, tuple(path.read_bytes() for path in second_paths))
            parsed = json.loads((output / "report.json").read_text(encoding="utf-8"))
            self.assertEqual("dragons.upstream-diff-report.v1", parsed["schema"])
            with self.assertRaisesRegex(SourceError, "beneath"):
                write_reports(
                    report,
                    REPOSITORY_ROOT / "tools" / "upstream-tracker" / "generated",
                    REPOSITORY_ROOT,
                    TOOL_ROOT / "templates" / "sync-branch.md",
                )

    def test_unresolved_descriptor_maps_at_path_scope(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(
                workspace,
                mapping_symbol="service domain",
            )
            configuration = load_configuration(lock, port_map, exceptions)
            baseline = workspace.path / "baseline"
            current = workspace.path / "current"
            workspace.write(
                "baseline/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            workspace.write(
                "current/src/source/service.py",
                "class Service:\n    def run(self):\n        return 2\n",
            )

            report = compare_sources(configuration, baseline, current)
            change = next(item for item in report.changes if item.symbol == "Service.run")

            self.assertEqual("path", change.mappings[0].match)
            self.assertEqual(0, report.unmapped_change_count)

    @staticmethod
    def _commit_repository(repository: Path, message: str) -> str:
        subprocess.run(["git", "init", "--quiet"], cwd=repository, check=True)
        subprocess.run(
            ["git", "config", "core.autocrlf", "false"],
            cwd=repository,
            check=True,
        )
        subprocess.run(
            ["git", "-c", "core.autocrlf=false", "add", "--all"],
            cwd=repository,
            check=True,
        )
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=Dragons Test",
                "-c",
                "user.email=test@dragons.invalid",
                "commit",
                "--quiet",
                "-m",
                message,
            ],
            cwd=repository,
            check=True,
        )
        return MappingAndReportTests._git_output(repository, "rev-parse", "HEAD")

    @staticmethod
    def _git_output(repository: Path, *arguments: str) -> str:
        return subprocess.run(
            ["git", *arguments],
            cwd=repository,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()


if __name__ == "__main__":
    unittest.main()

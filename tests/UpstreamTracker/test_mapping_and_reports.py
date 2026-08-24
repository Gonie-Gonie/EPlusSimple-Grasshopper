from __future__ import annotations

import json
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace, TOOL_ROOT, write_configuration

from goniegonie_upstream_tracker.classifier import ChangeClassification, compare_sources
from goniegonie_upstream_tracker.config import load_configuration
from goniegonie_upstream_tracker.errors import SourceError
from goniegonie_upstream_tracker.reporting import (
    render_json,
    render_markdown,
    render_sync_branch,
    write_reports,
)


class MappingAndReportTests(unittest.TestCase):
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
            self.assertEqual("goniegonie.upstream-diff-report.v1", parsed["schema"])
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


if __name__ == "__main__":
    unittest.main()

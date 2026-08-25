from __future__ import annotations

import json
from pathlib import Path
import unittest

from support import TemporaryWorkspace, write_configuration

from goniegonie_upstream_tracker.compatibility import (
    CompatibilityConfiguration,
    CompatibilityMatrix,
    MatrixEntry,
    build_compatibility_report,
    build_public_inventory,
    build_reverification_matrix,
    load_compatibility_configuration,
    load_compatibility_scope,
    load_compatibility_matrix,
    load_public_inventory,
    render_compatibility_report,
)
from goniegonie_upstream_tracker.config import TrackerConfiguration, load_configuration
from goniegonie_upstream_tracker.errors import ConfigurationError


class CompatibilityTests(unittest.TestCase):
    def test_public_inventory_policy_covers_public_and_dunder_class_api(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope = self._tracker_and_scope(workspace)
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                """VISIBLE = 1
COMPUTED = factory()
runtime_value = factory()
_PRIVATE_CONSTANT = 2

def public_function(value):
    return value

def _private_function():
    return None

class PublicService:
    FLAG = 3
    _PRIVATE_FLAG = 4

    def __init__(self, value):
        self.value = value

    def __eq__(self, other):
        return self.value == other.value

    def run(self):
        return self.value

    def _helper(self):
        return self.value

class _PrivateService:
    def run(self):
        return 0
""",
            )

            inventory = build_public_inventory(source, scope)
            names = {item.symbol for item in inventory.symbols}

            self.assertEqual(
                {
                    "COMPUTED",
                    "PublicService",
                    "PublicService.FLAG",
                    "PublicService.__eq__",
                    "PublicService.__init__",
                    "PublicService.run",
                    "VISIBLE",
                    "public_function",
                },
                names,
            )
            self.assertEqual(1, len(inventory.files))
            self.assertEqual(tracker.lock.commit, inventory.upstream_commit)

    def test_matrix_template_is_honest_and_uses_only_exact_registered_exception(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope = self._tracker_and_scope(
                workspace,
                exception_symbol="Service.run",
            )
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                """class Service:
    def __init__(self):
        self.value = 1

    def run(self):
        return self.value
""",
            )
            inventory = build_public_inventory(source, scope)
            matrix = build_reverification_matrix(inventory, tracker.exceptions)
            by_symbol = {item.symbol: item for item in matrix.entries}

            self.assertEqual("exception", by_symbol["Service.run"].classification)
            self.assertEqual(
                "reviewed-service-difference",
                by_symbol["Service.run"].exception_id,
            )
            self.assertEqual(
                "needs_reverification",
                by_symbol["Service"].classification,
            )
            self.assertEqual(
                "needs_reverification",
                by_symbol["Service.__init__"].classification,
            )
            self.assertFalse(any(item.classification == "equivalent" for item in matrix.entries))

    def test_missing_or_invalid_symbol_classification_fails_closed(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope = self._tracker_and_scope(workspace)
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            inventory = build_public_inventory(source, scope)
            inventory_path = self._write_json(
                workspace,
                "config/public-symbol-inventory.json",
                inventory.to_data(),
            )
            loaded_inventory = load_public_inventory(inventory_path, scope)
            matrix = build_reverification_matrix(loaded_inventory, tracker.exceptions)

            missing = matrix.to_data()
            missing["classifications"] = missing["classifications"][:-1]
            missing_path = self._write_json(
                workspace,
                "config/missing-matrix.json",
                missing,
            )
            with self.assertRaisesRegex(ConfigurationError, "classify every public inventory symbol"):
                load_compatibility_matrix(
                    missing_path,
                    scope,
                    loaded_inventory,
                    tracker.exceptions,
                )

            invalid = matrix.to_data()
            invalid["classifications"][0] = "implemented"
            invalid_path = self._write_json(
                workspace,
                "config/invalid-matrix.json",
                invalid,
            )
            with self.assertRaisesRegex(ConfigurationError, "must be one of"):
                load_compatibility_matrix(
                    invalid_path,
                    scope,
                    loaded_inventory,
                    tracker.exceptions,
                )

    def test_complete_classifications_require_evidence_and_exact_exception_reference(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope = self._tracker_and_scope(
                workspace,
                exception_symbol="Service.run",
            )
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            inventory = build_public_inventory(source, scope)
            inventory_path = self._write_json(
                workspace,
                "config/public-symbol-inventory.json",
                inventory.to_data(),
            )
            loaded_inventory = load_public_inventory(inventory_path, scope)
            matrix = build_reverification_matrix(loaded_inventory, tracker.exceptions)

            unsupported = matrix.to_data()
            unsupported["classifications"][0] = "equivalent"
            unsupported_path = self._write_json(
                workspace,
                "config/unsupported-equivalent.json",
                unsupported,
            )
            with self.assertRaisesRegex(ConfigurationError, "requires evidence detail"):
                load_compatibility_matrix(
                    unsupported_path,
                    scope,
                    loaded_inventory,
                    tracker.exceptions,
                )

            wrong_exception = matrix.to_data()
            detail = wrong_exception["details"][0]
            exception_index = detail["index"]
            wrong_index = next(
                index
                for index, symbol in enumerate(loaded_inventory.symbols)
                if symbol.symbol == "Service"
            )
            wrong_exception["classifications"][exception_index] = "needs_reverification"
            wrong_exception["classifications"][wrong_index] = "exception"
            detail["index"] = wrong_index
            wrong_exception_path = self._write_json(
                workspace,
                "config/wrong-exception.json",
                wrong_exception,
            )
            with self.assertRaisesRegex(ConfigurationError, "does not identify"):
                load_compatibility_matrix(
                    wrong_exception_path,
                    scope,
                    loaded_inventory,
                    tracker.exceptions,
                )

    def test_report_requires_reverification_and_verified_matching_pin(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope = self._tracker_and_scope(workspace)
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            inventory = build_public_inventory(source, scope)
            matrix = build_reverification_matrix(inventory, tracker.exceptions)
            configuration = CompatibilityConfiguration(tracker, scope, inventory, matrix)

            report = build_compatibility_report(
                configuration,
                source_root=source,
                source_identity={"pin_verified": True},
            )
            self.assertTrue(report.source_matches_inventory)
            self.assertFalse(report.classification_complete)
            self.assertFalse(report.passed)
            data = json.loads(render_compatibility_report(report))
            self.assertEqual("goniegonie.upstream-compatibility-report.v1", data["schema"])
            self.assertEqual(len(inventory.symbols), len(data["unresolved"]))

            reviewed_entries = tuple(
                MatrixEntry(
                    item.path,
                    item.symbol,
                    "out_of_scope",
                    "Test-only reviewed scope decision.",
                    (),
                    None,
                )
                for item in inventory.symbols
            )
            reviewed = CompatibilityConfiguration(
                tracker,
                scope,
                inventory,
                CompatibilityMatrix(
                    inventory.upstream_commit,
                    inventory.content_sha256,
                    reviewed_entries,
                ),
            )
            unverified = build_compatibility_report(
                reviewed,
                source_root=source,
                source_identity={"pin_verified": False},
            )
            self.assertTrue(unverified.classification_complete)
            self.assertTrue(unverified.source_matches_inventory)
            self.assertFalse(unverified.passed)
            verified = build_compatibility_report(
                reviewed,
                source_root=source,
                source_identity={"pin_verified": True},
            )
            self.assertTrue(verified.passed)

            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self):\n        return 2\n",
            )
            drifted = build_compatibility_report(
                reviewed,
                source_root=source,
                source_identity={"pin_verified": True},
            )
            self.assertFalse(drifted.source_matches_inventory)
            self.assertFalse(drifted.passed)

    def test_full_loader_rejects_inventory_source_hash_tampering(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope = self._tracker_and_scope(workspace)
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            inventory = build_public_inventory(source, scope)
            matrix = build_reverification_matrix(inventory, tracker.exceptions)
            scope_path = self._write_json(workspace, "config/scope-copy.json", scope.to_data())
            inventory_data = inventory.to_data()
            inventory_data["files"][0]["content_hash"] = "sha256:" + ("0" * 64)
            inventory_path = self._write_json(
                workspace,
                "config/tampered-inventory.json",
                inventory_data,
            )
            matrix_path = self._write_json(
                workspace,
                "config/matrix.json",
                matrix.to_data(),
            )

            with self.assertRaisesRegex(ConfigurationError, "content hash is invalid"):
                load_compatibility_configuration(
                    tracker,
                    scope_path,
                    inventory_path,
                    matrix_path,
                )

    def test_public_inventory_rejects_file_outside_declared_scope(self) -> None:
        with TemporaryWorkspace() as workspace:
            _, scope = self._tracker_and_scope(workspace)
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "def run():\n    return 1\n",
            )
            inventory = build_public_inventory(source, scope)
            data = inventory.to_data()
            data["files"][0]["path"] = "src/outside/service.py"
            path = self._write_json(
                workspace,
                "config/outside-scope-inventory.json",
                data,
            )

            with self.assertRaisesRegex(ConfigurationError, "outside compatibility scope"):
                load_public_inventory(path, scope)

    def _tracker_and_scope(
        self,
        workspace: TemporaryWorkspace,
        *,
        exception_symbol: str | None = None,
    ) -> tuple[TrackerConfiguration, object]:
        lock, port_map, exceptions = write_configuration(
            workspace,
            exception_symbol=exception_symbol,
        )
        tracker = load_configuration(lock, port_map, exceptions)
        scope_data = {
            "schema": "goniegonie.upstream-compatibility-scope.v1",
            "upstream_commit": tracker.lock.commit,
            "module_paths": list(tracker.lock.module_paths),
            "inventory_policy": {
                "language": "python",
                "python_feature_version": "3.12",
                "symbol_universe": "ast_declared_public",
                "include_kinds": ["class", "constant", "function"],
                "constant_rule": "uppercase_assignment_target",
                "include_public_top_level": True,
                "include_public_class_members": True,
                "include_dunder_class_members": True,
                "exclude_private_top_level": True,
                "exclude_private_class_members": True,
                "exclude_import_aliases": True,
                "exclude_nested_function_locals": True,
            },
            "classifications": {
                "allowed": [
                    "equivalent",
                    "exception",
                    "out_of_scope",
                    "needs_reverification",
                ],
                "complete": ["equivalent", "exception", "out_of_scope"],
            },
            "completion_gate": {
                "forbid": ["needs_reverification"],
                "require_exact_inventory_coverage": True,
                "require_inventory_matches_pinned_source": True,
            },
        }
        scope_path = self._write_json(workspace, "config/compatibility-scope.json", scope_data)
        return tracker, load_compatibility_scope(scope_path, tracker)

    @staticmethod
    def _write_json(
        workspace: TemporaryWorkspace,
        relative: str,
        value: object,
    ) -> Path:
        return workspace.write(
            relative,
            json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        )


if __name__ == "__main__":
    unittest.main()

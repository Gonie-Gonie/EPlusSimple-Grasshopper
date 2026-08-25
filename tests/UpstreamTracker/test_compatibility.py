from __future__ import annotations

import json
from pathlib import Path
import subprocess
import unittest

from support import TemporaryWorkspace, bind_exception_hash, write_configuration

from goniegonie_upstream_tracker.compatibility import (
    CompatibilityConfiguration,
    CompatibilityMatrix,
    MatrixEntry,
    PublicFile,
    PublicSymbolInventory,
    _RepositoryManifestReceipt,
    _tracker_content_sha256,
    build_compatibility_report,
    build_public_inventory,
    build_reverification_matrix,
    load_compatibility_configuration,
    load_compatibility_scope,
    load_compatibility_matrix,
    load_public_inventory,
    rebase_compatibility_inventory,
    render_compatibility_report,
)
from goniegonie_upstream_tracker.config import TrackerConfiguration, load_configuration
from goniegonie_upstream_tracker.errors import ConfigurationError
from goniegonie_upstream_tracker.evidence import (
    ScopeDecision,
    ScopeDecisionRegistry,
    empty_scope_decisions,
    empty_symbol_evidence,
)


class CompatibilityTests(unittest.TestCase):
    def test_public_inventory_v2_preserves_symbol_signature_and_body_hashes(self) -> None:
        with TemporaryWorkspace() as workspace:
            _, scope = self._tracker_and_scope(workspace)
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self, value):\n        return value + 1\n",
            )
            first = build_public_inventory(source, scope).symbols_by_key[
                ("src/source/service.py", "Service.run")
            ]

            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self, value):\n        return value + 2\n",
            )
            body_changed = build_public_inventory(source, scope).symbols_by_key[
                ("src/source/service.py", "Service.run")
            ]
            self.assertEqual(first.signature_hash, body_changed.signature_hash)
            self.assertNotEqual(first.body_hash, body_changed.body_hash)
            self.assertNotEqual(first.symbol_hash, body_changed.symbol_hash)

            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self, value, scale=1):\n        return value + 2\n",
            )
            signature_changed = build_public_inventory(source, scope).symbols_by_key[
                ("src/source/service.py", "Service.run")
            ]
            self.assertNotEqual(body_changed.signature_hash, signature_changed.signature_hash)
            self.assertEqual(body_changed.body_hash, signature_changed.body_hash)
            self.assertNotEqual(body_changed.symbol_hash, signature_changed.symbol_hash)

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
            self.assertTrue(
                all(item.symbol_hash.startswith("sha256:") for item in inventory.symbols)
            )
            self.assertTrue(
                all(item.signature_hash.startswith("sha256:") for item in inventory.symbols)
            )
            self.assertTrue(
                all(item.body_hash.startswith("sha256:") for item in inventory.symbols)
            )

    def test_matrix_template_keeps_registered_exception_fail_closed_without_receipt(self) -> None:
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
            tracker = bind_exception_hash(
                tracker,
                "src/source/service.py",
                "Service.run",
                inventory.symbols_by_key[("src/source/service.py", "Service.run")].symbol_hash,
            )
            matrix = build_reverification_matrix(inventory, tracker.exceptions)
            by_symbol = {item.symbol: item for item in matrix.entries}

            self.assertEqual("needs_reverification", by_symbol["Service.run"].classification)
            self.assertIsNone(by_symbol["Service.run"].exception_id)
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
            tracker = bind_exception_hash(
                tracker,
                "src/source/service.py",
                "Service.run",
                inventory.symbols_by_key[("src/source/service.py", "Service.run")].symbol_hash,
            )
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
            exception_index = next(
                index
                for index, symbol in enumerate(loaded_inventory.symbols)
                if symbol.symbol == "Service.run"
            )
            wrong_exception["classifications"][exception_index] = "exception"
            detail = {
                "evidence": [
                    "upstream/compatibility-exceptions.yml#reviewed-service-difference"
                ],
                "exception_id": "reviewed-service-difference",
                "index": exception_index,
                "rationale": "Test-only reviewed exception.",
            }
            wrong_exception["details"].append(detail)
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
            self.assertFalse(report.pin_verified)
            self.assertFalse(report.classification_complete)
            self.assertFalse(report.passed)
            data = json.loads(render_compatibility_report(report))
            self.assertEqual("goniegonie.upstream-compatibility-report.v2", data["schema"])
            self.assertEqual(len(inventory.symbols), len(data["unresolved"]))

            decisions = tuple(
                ScopeDecision(
                    f"test-scope-{index}",
                    item.path,
                    item.symbol,
                    item.symbol_hash,
                    "out_of_scope",
                    "compiled_rhino_grasshopper_product",
                    "Test-only reviewed scope decision.",
                    "docs/compatibility.md#declared-product-compatibility-scope",
                    "approved",
                )
                for index, item in enumerate(inventory.symbols)
            )
            reviewed_entries = tuple(
                MatrixEntry(
                    item.path,
                    item.symbol,
                    "out_of_scope",
                    "Test-only reviewed scope decision.",
                    (f"upstream/scope-decisions.json#test-scope-{index}",),
                    None,
                )
                for index, item in enumerate(inventory.symbols)
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
                empty_symbol_evidence(inventory),
                ScopeDecisionRegistry(
                    inventory.upstream_commit,
                    inventory.content_sha256,
                    decisions,
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
            self.assertFalse(verified.passed)

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

    def test_direct_receipt_cannot_substitute_objects_not_loaded_from_manifests(self) -> None:
        with TemporaryWorkspace() as workspace:
            initial_tracker, scope = self._tracker_and_scope(workspace)
            manifest_names = (
                "upstream.lock.json",
                "port-map.yml",
                "compatibility-exceptions.yml",
            )
            for source_path, name in zip(initial_tracker.manifest_paths, manifest_names):
                workspace.write(
                    f"upstream/{name}",
                    source_path.read_text(encoding="utf-8"),
                )
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            inventory = build_public_inventory(source, scope)
            canonical_matrix = build_reverification_matrix(
                inventory,
                initial_tracker.exceptions,
            )
            evidence = empty_symbol_evidence(inventory)
            canonical_decisions = empty_scope_decisions(inventory)
            self._write_json(
                workspace,
                "upstream/compatibility-scope.json",
                scope.to_data(),
            )
            self._write_json(
                workspace,
                "upstream/public-symbol-inventory.json",
                inventory.to_data(),
            )
            self._write_json(
                workspace,
                "upstream/compatibility-matrix.json",
                canonical_matrix.to_data(),
            )
            self._write_json(
                workspace,
                "upstream/symbol-evidence.json",
                evidence.to_data(),
            )
            self._write_json(
                workspace,
                "upstream/scope-decisions.json",
                canonical_decisions.to_data(),
            )
            workspace.write(
                "docs/compatibility.md",
                "## Declared product compatibility scope\n",
            )
            self._commit_repository(workspace.path)

            tracker = load_configuration(
                workspace.path / "upstream/upstream.lock.json",
                workspace.path / "upstream/port-map.yml",
                workspace.path / "upstream/compatibility-exceptions.yml",
            )
            canonical = load_compatibility_configuration(
                tracker,
                workspace.path / "upstream/compatibility-scope.json",
                workspace.path / "upstream/public-symbol-inventory.json",
                workspace.path / "upstream/compatibility-matrix.json",
                repository_root=workspace.path,
            )
            self.assertTrue(canonical.exact_registry_coverage)

            decisions = ScopeDecisionRegistry(
                inventory.upstream_commit,
                inventory.content_sha256,
                tuple(
                    ScopeDecision(
                        f"forged-scope-{index}",
                        item.path,
                        item.symbol,
                        item.symbol_hash,
                        "out_of_scope",
                        "compiled_rhino_grasshopper_product",
                        "Forged direct-API decision.",
                        "docs/compatibility.md#declared-product-compatibility-scope",
                        "approved",
                    )
                    for index, item in enumerate(inventory.symbols)
                ),
            )
            matrix = CompatibilityMatrix(
                inventory.upstream_commit,
                inventory.content_sha256,
                tuple(
                    MatrixEntry(
                        item.path,
                        item.symbol,
                        "out_of_scope",
                        "Forged direct-API decision.",
                        (f"upstream/scope-decisions.json#forged-scope-{index}",),
                        None,
                    )
                    for index, item in enumerate(inventory.symbols)
                ),
            )
            receipt = _RepositoryManifestReceipt(
                workspace.path.resolve(),
                (
                    "upstream/upstream.lock.json",
                    "upstream/port-map.yml",
                    "upstream/compatibility-exceptions.yml",
                    "upstream/compatibility-scope.json",
                    "upstream/public-symbol-inventory.json",
                    "upstream/compatibility-matrix.json",
                    "upstream/symbol-evidence.json",
                    "upstream/scope-decisions.json",
                ),
                _tracker_content_sha256(tracker),
                scope.content_sha256,
                inventory.content_sha256,
                matrix.content_sha256,
                evidence.content_sha256,
                decisions.content_sha256,
            )
            forged = CompatibilityConfiguration(
                tracker,
                scope,
                inventory,
                matrix,
                evidence,
                decisions,
                receipt,
            )

            self.assertFalse(forged.exact_registry_coverage)
            self.assertFalse(
                build_compatibility_report(
                    forged,
                    source_root=source,
                    source_identity={"pin_verified": True},
                ).passed
            )

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

    def test_inventory_rebase_allows_only_byte_drift_with_same_ast_contract(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope = self._tracker_and_scope(workspace)
            source = workspace.path / "source"
            workspace.write(
                "source/src/source/service.py",
                "class Service:\n    def run(self):\n        return 1\n",
            )
            inventory = build_public_inventory(source, scope)
            configuration = CompatibilityConfiguration(
                tracker,
                scope,
                inventory,
                build_reverification_matrix(inventory, tracker.exceptions),
                empty_symbol_evidence(inventory),
                empty_scope_decisions(inventory),
            )
            replacement = PublicSymbolInventory(
                inventory.upstream_commit,
                inventory.scope_sha256,
                tuple(
                    PublicFile(
                        item.path,
                        "sha256:" + ("f" * 64),
                        item.ast_hash,
                    )
                    for item in inventory.files
                ),
                inventory.symbols,
            )

            rebased = rebase_compatibility_inventory(configuration, replacement)

            self.assertEqual(replacement.content_sha256, rebased.matrix.inventory_sha256)
            self.assertEqual(
                replacement.content_sha256,
                rebased.symbol_evidence.inventory_sha256,
            )
            self.assertEqual(
                replacement.content_sha256,
                rebased.scope_decisions.inventory_sha256,
            )
            self.assertEqual(configuration.matrix.entries, rebased.matrix.entries)

            changed_ast = PublicSymbolInventory(
                replacement.upstream_commit,
                replacement.scope_sha256,
                (
                    PublicFile(
                        replacement.files[0].path,
                        replacement.files[0].content_hash,
                        "sha256:" + ("e" * 64),
                    ),
                    *replacement.files[1:],
                ),
                replacement.symbols,
            )
            with self.assertRaisesRegex(ConfigurationError, "AST hash"):
                rebase_compatibility_inventory(configuration, changed_ast)

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

    @staticmethod
    def _commit_repository(repository: Path) -> None:
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
                "user.name=GonieGonie Test",
                "-c",
                "user.email=test@goniegonie.invalid",
                "commit",
                "--quiet",
                "-m",
                "canonical manifests",
            ],
            cwd=repository,
            check=True,
        )


if __name__ == "__main__":
    unittest.main()

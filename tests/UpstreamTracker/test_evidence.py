from __future__ import annotations

import json
import hashlib
import os
from pathlib import Path
import subprocess
import unittest
from unittest.mock import patch

from support import TemporaryWorkspace, write_configuration

from dragons_upstream_tracker.compatibility import (
    CompatibilityConfiguration,
    CompatibilityMatrix,
    MatrixEntry,
    build_compatibility_report,
    build_public_inventory,
    load_compatibility_scope,
)
from dragons_upstream_tracker.config import load_configuration
from dragons_upstream_tracker.errors import ConfigurationError
from dragons_upstream_tracker.evidence import (
    EvidenceReceipt,
    EvidenceResults,
    ExecutedAssertion,
    ScopeDecision,
    ScopeDecisionRegistry,
    SymbolEvidence,
    SymbolEvidenceRegistry,
    _csharp_declares_symbol,
    _resolve_repository_file,
    empty_scope_decisions,
    load_evidence_results,
    load_scope_decisions,
    load_symbol_evidence,
)


OUTPUT_HASH = "sha256:" + ("1" * 64)
OTHER_HASH = "sha256:" + ("2" * 64)
IMPLEMENTATION_SOURCE = (
    "namespace Dragons.InvisibleDragon.Model;\n"
    "public class Service {\n"
    "  public int Run() => Helper.Missing();\n"
    "#if NEVER_DEFINED\n"
    "  public int Disabled() => 2;\n"
    "#endif\n"
    "}\n"
    "public static class Helper { public static int Missing() => 1; }\n"
    "public class Missing { }\n"
)
TEST_SOURCE = "public class ServiceParityTests { public void RunMatchesUpstream() { } }\n"
COLLECTOR_SOURCE = "public class EvidenceCollector { public void Emit() { } }\n"


def source_hash(value: str) -> str:
    return "sha256:" + hashlib.sha256(value.encode("utf-8")).hexdigest()


class ExactEvidenceTests(unittest.TestCase):
    def test_csharp_binding_resolves_fully_qualified_types_and_masks_raw_strings(self) -> None:
        source = '''namespace Dragons.Model;
public class Owner
{
    private string Text = """" inside """ ; public int Ghost; """";
    public int Real() => 1;
    public class Nested { public void Run() { } }
}
'''

        self.assertTrue(_csharp_declares_symbol(source, "Dragons.Model.Owner"))
        self.assertTrue(_csharp_declares_symbol(source, "Dragons.Model.Owner.Real"))
        self.assertTrue(
            _csharp_declares_symbol(source, "Dragons.Model.Owner.Nested")
        )
        self.assertTrue(
            _csharp_declares_symbol(
                source,
                "Dragons.Model.Owner.Nested.Run",
            )
        )
        self.assertFalse(_csharp_declares_symbol(source, "Owner.Ghost"))
        self.assertFalse(_csharp_declares_symbol(source, "Owner.Run"))
        self.assertFalse(_csharp_declares_symbol(source, "Owner.Real"))
        self.assertFalse(_csharp_declares_symbol(source, "Model.Owner.Real"))

    def test_csharp_destructor_is_not_bound_as_constructor(self) -> None:
        source = "namespace N; public class Owner { ~Owner() { } }"

        self.assertFalse(_csharp_declares_symbol(source, "N.Owner.Owner"))

    def test_csharp_binding_recognizes_tuple_return_method_declarations(self) -> None:
        source = """namespace Dragons.InvisibleDragon.Profile;
public sealed class Schedule
{
    public static (
        IReadOnlyList<SchedulePeriod> Left,
        IReadOnlyList<SchedulePeriod> Right) UnifyCompactizedSchedules(
            IReadOnlyList<SchedulePeriod> left,
            IReadOnlyList<SchedulePeriod> right)
    {
        return (left, right);
    }

}
"""

        self.assertTrue(
            _csharp_declares_symbol(
                source,
                "Dragons.InvisibleDragon.Profile.Schedule.UnifyCompactizedSchedules",
            )
        )

    def test_csharp_tuple_return_binding_rejects_calls_locals_and_forgeries(
        self,
    ) -> None:
        source = """namespace N;
public sealed class Owner
{
    private static readonly (int Left, int Right) Cached =
        Helper.UnifyCompactizedSchedules();

    public (int Left, int Right) Wrapper() =>
        Helper.UnifyCompactizedSchedules();

    public void Outer()
    {
        static (int Left, int Right) UnifyCompactizedSchedules() => (1, 2);
        _ = UnifyCompactizedSchedules();
    }

    public static (int Left, int Right) Unbalanced(
        int value;
}
public static class Helper
{
    public static (int Left, int Right) UnifyCompactizedSchedules()
    {
        return (1, 2);
    }
}
"""

        self.assertFalse(
            _csharp_declares_symbol(source, "N.Owner.UnifyCompactizedSchedules")
        )
        self.assertFalse(_csharp_declares_symbol(source, "N.Owner.Unbalanced"))
        self.assertTrue(
            _csharp_declares_symbol(source, "N.Helper.UnifyCompactizedSchedules")
        )

    def test_csharp_tuple_return_binding_rejects_malformed_declarations(
        self,
    ) -> None:
        malformed = (
            "public static (int A extra, string B) U() { return default; }",
            "(public static, private readonly) U() { return default; }",
            "public static (int A, string B) U(int a int b) { return default; }",
            "public static (int A, string B) U(a + b) { return default; }",
            "public static (int A, string B) U<<T>() { return default; }",
            "public static (int A, string B) U<T>() where T { return default; }",
            "public static (int int, string string) U() { return default; }",
            "public static (if A, while B) U() { return default; }",
            "public static (int A, string B) U(int int) { return default; }",
            "public static (int A, string B) U(int if) { return default; }",
            "public static (int?? A, string B) U() { return default; }",
            "public static (int[bogus] A, string B) U() { return default; }",
            "public [A] static (int A, string B) U() { return default; }",
            "public static (A.B::C A, string B) U() { return default; }",
            "public static (int.Foo A, string B) U() { return default; }",
            "public static (int<string> A, string B) U() { return default; }",
            "public static (global::int A, string B) U() { return default; }",
            "public static (int A, string B) U(int.Foo value) { return default; }",
            "public static (int A, string B) U() =>;",
            "public static (int A, string B) U() => (1, 2);",
            "public static (int A, string B) U() => return;",
            "public static (int A, string B) U() => { };",
            "public static (int A, string B) U();",
            "private (int A, string B) U() { return default; }",
        )

        for declaration in malformed:
            source = f"namespace N; public sealed class Owner {{ {declaration} }}"
            with self.subTest(declaration=declaration):
                self.assertFalse(_csharp_declares_symbol(source, "N.Owner.U"))

    def test_csharp_binding_recognizes_first_middle_and_last_enum_members(self) -> None:
        source = """namespace Dragons.InvisibleDragon.Profile;
public enum ScheduleType
{
    [System.Obsolete] Temperature = -1,
    OnOff,
    Fraction = 4,
    Real,
}
public class Other
{
    public void Run() { int Temperature = 0; }
}
"""

        for member in ("Temperature", "OnOff", "Fraction", "Real"):
            self.assertTrue(
                _csharp_declares_symbol(
                    source,
                    f"Dragons.InvisibleDragon.Profile.ScheduleType.{member}",
                ),
                member,
            )
        self.assertFalse(
            _csharp_declares_symbol(
                source,
                "Dragons.InvisibleDragon.Profile.ScheduleType.Missing",
            )
        )
        self.assertFalse(
            _csharp_declares_symbol(
                source,
                "Dragons.InvisibleDragon.Profile.Other.Temperature",
            )
        )

    def test_csharp_binding_maps_only_closed_operator_metadata_names(self) -> None:
        source = r'''namespace Dragons.InvisibleDragon.Profile;
public sealed class DaySchedule
{
    public static DaySchedule operator +(DaySchedule left, DaySchedule right) => left;
    public static DaySchedule operator -(DaySchedule left, DaySchedule right) => left;
    public static DaySchedule operator *(DaySchedule left, double right) => left;
    public static DaySchedule operator /(DaySchedule left, double right) => left;
    public static DaySchedule operator &(DaySchedule left, DaySchedule right) => left;
    public static DaySchedule operator |(DaySchedule left, DaySchedule right) => left;
    public static DaySchedule operator !(DaySchedule value) => value;

    public void op_Increment() { }
    private string Forged = "operator %(DaySchedule value)";
    // operator %(DaySchedule value)

    public sealed class Nested
    {
        public static Nested operator %(Nested value, int other) => value;
    }
}
public sealed class Other
{
    public static Other operator +(Other left, Other right) => left;
}
public sealed class UnaryOnly
{
    public static UnaryOnly operator +(UnaryOnly value) => value;
    public static UnaryOnly operator -(UnaryOnly value) => value;
}
'''
        expected = {
            "op_Addition",
            "op_Subtraction",
            "op_Multiply",
            "op_Division",
            "op_BitwiseAnd",
            "op_BitwiseOr",
            "op_LogicalNot",
        }
        for metadata_name in expected:
            self.assertTrue(
                _csharp_declares_symbol(
                    source,
                    f"Dragons.InvisibleDragon.Profile.DaySchedule.{metadata_name}",
                ),
                metadata_name,
            )

        for metadata_name in (
            "op_Increment",
            "op_Modulus",
            "op_Equality",
            "op_Implicit",
            "op_Explicit",
        ):
            self.assertFalse(
                _csharp_declares_symbol(
                    source,
                    f"Dragons.InvisibleDragon.Profile.DaySchedule.{metadata_name}",
                ),
                metadata_name,
            )
        self.assertFalse(
            _csharp_declares_symbol(
                source,
                "Dragons.InvisibleDragon.Profile.Other.op_Subtraction",
            )
        )
        self.assertFalse(
            _csharp_declares_symbol(
                source,
                "Dragons.InvisibleDragon.Profile.UnaryOnly.op_Addition",
            )
        )
        self.assertFalse(
            _csharp_declares_symbol(
                source,
                "Dragons.InvisibleDragon.Profile.UnaryOnly.op_Subtraction",
            )
        )

    def test_csharp_operator_binding_masks_nested_and_conditional_declarations(self) -> None:
        source = r'''namespace N;
public sealed class Owner
{
    private string Text = "operator +(Owner left, Owner right)";
    public void op_Addition() { }
    public sealed class Nested
    {
        public static Nested operator +(Nested left, Nested right) => left;
    }
#if FEATURE
    public static Owner operator -(Owner left, Owner right) => left;
#endif
#region operator +(Owner left, Owner right)
#endregion
#warning operator +(Owner left, Owner right)
}
'''
        self.assertFalse(_csharp_declares_symbol(source, "N.Owner.op_Addition"))
        self.assertFalse(_csharp_declares_symbol(source, "N.Owner.op_Subtraction"))
        self.assertTrue(_csharp_declares_symbol(source, "N.Owner.Nested.op_Addition"))

    def test_csharp_operator_binding_rejects_type_enum_and_escaped_name_forgeries(self) -> None:
        source = r'''namespace N;
public sealed class Helper
{
    public static Helper @operator => new Helper();
    public static Helper operator +(Helper left, (int X, int Y) right) => left;
}
public sealed class Owner
{
    private Helper Value = Helper.@operator + (1, 2);
    public sealed class op_Addition { }
}
public enum ForgedEnum
{
    op_Addition,
}
'''
        self.assertTrue(_csharp_declares_symbol(source, "N.Helper.op_Addition"))
        self.assertFalse(_csharp_declares_symbol(source, "N.Owner.op_Addition"))
        self.assertFalse(_csharp_declares_symbol(source, "N.ForgedEnum.op_Addition"))

    def test_head_validation_ignores_inherited_git_directory(self) -> None:
        with TemporaryWorkspace() as real, TemporaryWorkspace() as fake:
            real.write("src/Service.cs", "class Real { }\n")
            fake.write("src/Service.cs", "class Forged { }\n")
            self._commit_repository(real.path, "real")
            self._commit_repository(fake.path, "fake")
            real.write("src/Service.cs", "class Forged { }\n")

            with patch.dict(
                os.environ,
                {"GIT_DIR": str(fake.path / ".git")},
                clear=False,
            ):
                with self.assertRaisesRegex(
                    ConfigurationError,
                    "differs from the repository HEAD tree",
                ):
                    _resolve_repository_file(
                        real.path,
                        "src/Service.cs",
                        "test source",
                    )

    def test_head_validation_ignores_replacement_trees(self) -> None:
        with TemporaryWorkspace() as workspace:
            workspace.write("src/Service.cs", "class Real { }\n")
            self._commit_repository(workspace.path, "real")
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

            with self.assertRaisesRegex(
                ConfigurationError,
                "differs from the repository HEAD tree",
            ):
                _resolve_repository_file(
                    workspace.path,
                    "src/Service.cs",
                    "test source",
                )

    def test_prepared_registry_never_promotes_needs_reverification(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope, inventory = self._inventory(workspace)
            symbol = next(item for item in inventory.symbols if item.symbol == "Service.run")
            evidence = self._evidence(inventory, symbol)
            entries = tuple(
                MatrixEntry(
                    item.path,
                    item.symbol,
                    "needs_reverification",
                    "No exact executed receipt has been reviewed.",
                    (),
                    None,
                )
                for item in inventory.symbols
            )
            configuration = CompatibilityConfiguration(
                tracker,
                scope,
                inventory,
                CompatibilityMatrix(inventory.upstream_commit, inventory.content_sha256, entries),
                evidence,
                empty_scope_decisions(inventory),
            )
            result = EvidenceResults(
                inventory.upstream_commit,
                inventory.content_sha256,
                evidence.content_sha256,
                "tools/EvidenceCollector.cs",
                "EvidenceCollector.Emit",
                source_hash(COLLECTOR_SOURCE),
                (self._executed(),),
                "net8.0-windows",
            )

            report = build_compatibility_report(
                configuration,
                source_identity={"pin_verified": True},
                evidence_results=(result,),
            )

            self.assertFalse(report.classification_complete)
            self.assertFalse(report.passed)
            self.assertEqual(0, report.evidence_execution.required_assertion_ids.__len__())

    def test_equivalent_requires_collected_assertion_and_exact_output_hash(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope, inventory = self._inventory(workspace)
            target = next(item for item in inventory.symbols if item.symbol == "Service.run")
            evidence = self._evidence(inventory, target)
            decisions = []
            matrix_entries = []
            for index, symbol in enumerate(inventory.symbols):
                if symbol.key == target.key:
                    matrix_entries.append(
                        MatrixEntry(
                            symbol.path,
                            symbol.symbol,
                            "equivalent",
                            "Exact behavioral assertion reviewed.",
                            ("upstream/symbol-evidence.json#service-run-parity",),
                            None,
                        )
                    )
                else:
                    identifier = f"test-scope-{index}"
                    decisions.append(self._decision(symbol, identifier))
                    matrix_entries.append(
                        MatrixEntry(
                            symbol.path,
                            symbol.symbol,
                            "out_of_scope",
                            "Test-only exact scope decision.",
                            (f"upstream/scope-decisions.json#{identifier}",),
                            None,
                        )
                    )
            configuration = CompatibilityConfiguration(
                tracker,
                scope,
                inventory,
                CompatibilityMatrix(
                    inventory.upstream_commit,
                    inventory.content_sha256,
                    tuple(matrix_entries),
                ),
                evidence,
                ScopeDecisionRegistry(
                    inventory.upstream_commit,
                    inventory.content_sha256,
                    tuple(decisions),
                ),
            )

            missing = build_compatibility_report(
                configuration,
                source_identity={"pin_verified": True},
            )
            self.assertFalse(missing.evidence_execution.passed)
            self.assertEqual(("service-run-parity",), missing.evidence_execution.missing_assertion_ids)

            wrong_hash = EvidenceResults(
                inventory.upstream_commit,
                inventory.content_sha256,
                evidence.content_sha256,
                "tools/EvidenceCollector.cs",
                "EvidenceCollector.Emit",
                source_hash(COLLECTOR_SOURCE),
                (self._executed(output_hash=OTHER_HASH),),
                "net8.0-windows",
            )
            mismatch = build_compatibility_report(
                configuration,
                source_identity={"pin_verified": True},
                evidence_results=(wrong_hash,),
            )
            self.assertEqual(
                ("service-run-parity",),
                mismatch.evidence_execution.output_hash_mismatch_ids,
            )

            overclaim = EvidenceResults(
                inventory.upstream_commit,
                inventory.content_sha256,
                evidence.content_sha256,
                "tools/EvidenceCollector.cs",
                "EvidenceCollector.Emit",
                source_hash(COLLECTOR_SOURCE),
                (
                    self._executed(
                        outcome="skipped",
                        skipped=True,
                        structural_only=True,
                        exercised_load="zero",
                    ),
                ),
                "net8.0-windows",
            )
            rejected = build_compatibility_report(
                configuration,
                source_identity={"pin_verified": True},
                evidence_results=(overclaim,),
            )
            self.assertEqual(("service-run-parity",), rejected.evidence_execution.skipped_assertion_ids)
            self.assertEqual(
                ("service-run-parity",),
                rejected.evidence_execution.structural_only_assertion_ids,
            )
            self.assertEqual(("service-run-parity",), rejected.evidence_execution.load_mismatch_ids)

            passed = EvidenceResults(
                inventory.upstream_commit,
                inventory.content_sha256,
                evidence.content_sha256,
                "tools/EvidenceCollector.cs",
                "EvidenceCollector.Emit",
                source_hash(COLLECTOR_SOURCE),
                (self._executed(),),
                "net8.0-windows",
            )
            verified = build_compatibility_report(
                configuration,
                source_identity={"pin_verified": True},
                evidence_results=(passed,),
            )
            self.assertTrue(verified.evidence_execution.assertions_satisfied)
            self.assertFalse(verified.evidence_execution.passed)


    def test_registry_rejects_stale_duplicate_broad_and_overclaiming_receipts(self) -> None:
        with TemporaryWorkspace() as workspace:
            _, _, inventory = self._inventory(workspace)
            symbol = next(item for item in inventory.symbols if item.symbol == "Service.run")
            valid = self._evidence(inventory, symbol).to_data()
            loaded = load_symbol_evidence(
                self._write_json(workspace, "config/valid-evidence.json", valid),
                inventory,
                repository_root=workspace.path,
            )
            self.assertEqual(symbol.symbol_hash, loaded.entries[0].upstream_symbol_hash)

            mutations = {
                "stale upstream symbol hash": lambda data: data["entries"][0].__setitem__(
                    "upstream_symbol_hash", OTHER_HASH
                ),
                "glob or broad reference": lambda data: data["entries"][0]["receipts"][0].__setitem__(
                    "test_path", "tests/**/*.cs"
                ),
                "skipped test": lambda data: data["entries"][0]["receipts"][0].__setitem__(
                    "skipped", True
                ),
                "structural-only evidence": lambda data: data["entries"][0]["receipts"][0].__setitem__(
                    "structural_only", True
                ),
                "zero or non-applicable load": lambda data: data["entries"][0]["receipts"][0].__setitem__(
                    "claims_active_load", True
                ),
                "exact relative POSIX file path": lambda data: data["entries"][0][
                    "implementation"
                ].__setitem__("path", "src/Model/Service.cs:forged.cs"),
                "Windows-ambiguous trailing dot or space": lambda data: data[
                    "entries"
                ][0]["implementation"].__setitem__(
                    "path", "src/Model/Service.cs."
                ),
            }
            for expected, mutate in mutations.items():
                with self.subTest(expected=expected):
                    data = json.loads(json.dumps(valid))
                    mutate(data)
                    path = self._write_json(workspace, f"config/invalid-{len(expected)}.json", data)
                    with self.assertRaisesRegex(ConfigurationError, expected):
                        load_symbol_evidence(path, inventory, repository_root=workspace.path)

            duplicate = json.loads(json.dumps(valid))
            duplicate["entries"].append(json.loads(json.dumps(duplicate["entries"][0])))
            duplicate_path = self._write_json(workspace, "config/duplicate-evidence.json", duplicate)
            with self.assertRaisesRegex(ConfigurationError, "unique and sorted"):
                load_symbol_evidence(
                    duplicate_path,
                    inventory,
                    repository_root=workspace.path,
                )

            untracked_source = (
                "namespace Dragons.InvisibleDragon.Model;\n"
                "public class Untracked { public int Run() => 1; }\n"
            )
            workspace.write("src/Model/Untracked.cs", untracked_source)
            untracked = json.loads(json.dumps(valid))
            untracked["entries"][0]["implementation"].update(
                {
                    "path": "src/Model/Untracked.cs",
                    "source_sha256": source_hash(untracked_source),
                    "symbol": "Dragons.InvisibleDragon.Model.Untracked.Run",
                }
            )
            with self.assertRaisesRegex(
                ConfigurationError,
                "must exist in the repository HEAD tree",
            ):
                load_symbol_evidence(
                    self._write_json(
                        workspace,
                        "config/untracked-evidence.json",
                        untracked,
                    ),
                    inventory,
                    repository_root=workspace.path,
                )

            ephemeral = json.loads(json.dumps(untracked))
            workspace.write("temp/Untracked.cs", untracked_source)
            ephemeral["entries"][0]["implementation"]["path"] = "temp/Untracked.cs"
            with self.assertRaisesRegex(
                ConfigurationError,
                "must not use an ephemeral output directory",
            ):
                load_symbol_evidence(
                    self._write_json(
                        workspace,
                        "config/ephemeral-evidence.json",
                        ephemeral,
                    ),
                    inventory,
                    repository_root=workspace.path,
                )

            for field, value, expected in (
                ("path", "src/Model/Missing.cs", "does not exist"),
                ("symbol", "Service.Missing", "does not declare exact symbol"),
                ("symbol", "Service.Disabled", "does not declare exact symbol"),
            ):
                with self.subTest(binding=field):
                    missing = json.loads(json.dumps(valid))
                    missing["entries"][0]["implementation"][field] = value
                    with self.assertRaisesRegex(ConfigurationError, expected):
                        load_symbol_evidence(
                            self._write_json(
                                workspace,
                                f"config/missing-binding-{field}.json",
                                missing,
                            ),
                            inventory,
                            repository_root=workspace.path,
                        )

            for field, value, expected in (
                ("test_path", "tests/MissingParityTests.cs", "does not exist"),
                ("test_symbol", "ServiceParityTests.Missing", "does not declare exact symbol"),
            ):
                with self.subTest(test_binding=field):
                    missing = json.loads(json.dumps(valid))
                    missing["entries"][0]["receipts"][0][field] = value
                    with self.assertRaisesRegex(ConfigurationError, expected):
                        load_symbol_evidence(
                            self._write_json(
                                workspace,
                                f"config/missing-test-binding-{field}.json",
                                missing,
                            ),
                            inventory,
                            repository_root=workspace.path,
                        )

            dirty_source = IMPLEMENTATION_SOURCE.replace(
                "public int Run() => Helper.Missing();",
                "public int Run() => Helper.Missing() + 1;",
            )
            workspace.write("src/Model/Service.cs", dirty_source)
            subprocess.run(
                [
                    "git",
                    "update-index",
                    "--assume-unchanged",
                    "src/Model/Service.cs",
                ],
                cwd=workspace.path,
                check=True,
            )
            dirty = json.loads(json.dumps(valid))
            dirty["entries"][0]["implementation"]["source_sha256"] = source_hash(
                dirty_source
            )
            with self.assertRaisesRegex(
                ConfigurationError,
                "differs from the repository HEAD tree",
            ):
                load_symbol_evidence(
                    self._write_json(workspace, "config/dirty-evidence.json", dirty),
                    inventory,
                    repository_root=workspace.path,
                )

            subprocess.run(
                [
                    "git",
                    "update-index",
                    "--no-assume-unchanged",
                    "src/Model/Service.cs",
                ],
                cwd=workspace.path,
                check=True,
            )
            subprocess.run(
                [
                    "git",
                    "update-index",
                    "--skip-worktree",
                    "src/Model/Service.cs",
                ],
                cwd=workspace.path,
                check=True,
            )
            with self.assertRaisesRegex(
                ConfigurationError,
                "differs from the repository HEAD tree",
            ):
                load_symbol_evidence(
                    self._write_json(
                        workspace,
                        "config/skip-worktree-evidence.json",
                        dirty,
                    ),
                    inventory,
                    repository_root=workspace.path,
                )

    def test_scope_decisions_are_exact_hash_bound_and_required_for_out_of_scope(self) -> None:
        with TemporaryWorkspace() as workspace:
            tracker, scope, inventory = self._inventory(workspace)
            symbol = inventory.symbols[0]
            decision = self._decision(symbol, "test-scope-one")
            registry = ScopeDecisionRegistry(
                inventory.upstream_commit,
                inventory.content_sha256,
                (decision,),
            )
            loaded = load_scope_decisions(
                self._write_json(workspace, "config/scope-decisions.json", registry.to_data()),
                inventory,
                repository_root=workspace.path,
            )
            self.assertEqual(decision.exact_key, loaded.decisions[0].exact_key)

            stale = registry.to_data()
            stale["decisions"][0]["upstream_symbol_hash"] = OTHER_HASH
            with self.assertRaisesRegex(ConfigurationError, "stale upstream symbol hash"):
                load_scope_decisions(
                    self._write_json(workspace, "config/stale-scope.json", stale),
                    inventory,
                    repository_root=workspace.path,
                )

            matrix_entries = tuple(
                MatrixEntry(
                    item.path,
                    item.symbol,
                    "out_of_scope" if item.key == symbol.key else "needs_reverification",
                    "Test decision.",
                    (
                        ("upstream/scope-decisions.json#test-scope-one",)
                        if item.key == symbol.key
                        else ()
                    ),
                    None,
                )
                for item in inventory.symbols
            )
            with self.assertRaisesRegex(ConfigurationError, "missing out-of-scope symbol"):
                CompatibilityConfiguration(
                    tracker,
                    scope,
                    inventory,
                    CompatibilityMatrix(
                        inventory.upstream_commit,
                        inventory.content_sha256,
                        matrix_entries,
                    ),
                    SymbolEvidenceRegistry(
                        inventory.upstream_commit,
                        inventory.content_sha256,
                        (),
                    ),
                    empty_scope_decisions(inventory),
                )

    def test_result_artifact_rejects_duplicate_and_stale_assertions_deterministically(self) -> None:
        with TemporaryWorkspace() as workspace:
            _, _, inventory = self._inventory(workspace)
            valid = EvidenceResults(
                inventory.upstream_commit,
                inventory.content_sha256,
                self._evidence(
                    inventory,
                    next(item for item in inventory.symbols if item.symbol == "Service.run"),
                ).content_sha256,
                "tools/EvidenceCollector.cs",
                "EvidenceCollector.Emit",
                source_hash(COLLECTOR_SOURCE),
                (self._executed(),),
                "net8.0-windows",
            ).to_data()
            loaded = load_evidence_results(
                self._write_json(workspace, "config/results.json", valid),
                inventory,
                self._evidence(
                    inventory,
                    next(item for item in inventory.symbols if item.symbol == "Service.run"),
                ),
                repository_root=workspace.path,
            )
            self.assertEqual(OUTPUT_HASH, loaded.assertions[0].output_sha256)

            duplicate = json.loads(json.dumps(valid))
            duplicate["assertions"].append(json.loads(json.dumps(duplicate["assertions"][0])))
            with self.assertRaisesRegex(ConfigurationError, "unique and sorted"):
                load_evidence_results(
                    self._write_json(workspace, "config/duplicate-results.json", duplicate),
                    inventory,
                    self._evidence(
                        inventory,
                        next(item for item in inventory.symbols if item.symbol == "Service.run"),
                    ),
                    repository_root=workspace.path,
                )

            stale = json.loads(json.dumps(valid))
            stale["inventory_sha256"] = OTHER_HASH
            with self.assertRaisesRegex(ConfigurationError, "inventory hash is stale"):
                load_evidence_results(
                    self._write_json(workspace, "config/stale-results.json", stale),
                    inventory,
                    self._evidence(
                        inventory,
                        next(item for item in inventory.symbols if item.symbol == "Service.run"),
                    ),
                    repository_root=workspace.path,
                )

            stale_registry = json.loads(json.dumps(valid))
            stale_registry["symbol_evidence_sha256"] = OTHER_HASH
            with self.assertRaisesRegex(
                ConfigurationError,
                "symbol-evidence registry hash is stale",
            ):
                load_evidence_results(
                    self._write_json(
                        workspace,
                        "config/stale-registry-results.json",
                        stale_registry,
                    ),
                    inventory,
                    self._evidence(
                        inventory,
                        next(
                            item
                            for item in inventory.symbols
                            if item.symbol == "Service.run"
                        ),
                    ),
                    repository_root=workspace.path,
                )

    def _inventory(self, workspace: TemporaryWorkspace):
        lock, port_map, exceptions = write_configuration(workspace)
        tracker = load_configuration(lock, port_map, exceptions)
        scope_data = {
            "schema": "dragons.upstream-compatibility-scope.v1",
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
        scope = load_compatibility_scope(
            self._write_json(workspace, "config/compatibility-scope.json", scope_data),
            tracker,
        )
        source = workspace.path / "source"
        workspace.write(
            "source/src/source/service.py",
            "class Service:\n    def run(self):\n        return 1\n",
        )
        workspace.write("src/Model/Service.cs", IMPLEMENTATION_SOURCE)
        workspace.write("tests/ServiceParityTests.cs", TEST_SOURCE)
        workspace.write("tools/EvidenceCollector.cs", COLLECTOR_SOURCE)
        workspace.write(
            "docs/development/compatibility-policy.md",
            "## Declared product compatibility scope\n",
        )
        subprocess.run(
            ["git", "init", "--quiet"],
            cwd=workspace.path,
            check=True,
        )
        subprocess.run(
            ["git", "config", "core.autocrlf", "false"],
            cwd=workspace.path,
            check=True,
        )
        subprocess.run(
            [
                "git",
                "-c",
                "core.autocrlf=false",
                "add",
                "--all",
            ],
            cwd=workspace.path,
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
                "test fixture",
            ],
            cwd=workspace.path,
            check=True,
        )
        return tracker, scope, build_public_inventory(source, scope)

    @staticmethod
    def _commit_repository(repository: Path, message: str) -> None:
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

    @staticmethod
    def _git_output(repository: Path, *arguments: str) -> str:
        return subprocess.run(
            ["git", *arguments],
            cwd=repository,
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()

    @staticmethod
    def _evidence(inventory, symbol) -> SymbolEvidenceRegistry:
        receipt = EvidenceReceipt(
            "service-run-parity",
            "tests/ServiceParityTests.cs",
            "ServiceParityTests.RunMatchesUpstream",
            source_hash(TEST_SOURCE),
            "The exact return value matches the pinned Python contract.",
            "cross_language",
            "passed",
            False,
            False,
            "not_applicable",
            False,
            OUTPUT_HASH,
        )
        return SymbolEvidenceRegistry(
            inventory.upstream_commit,
            inventory.content_sha256,
            (
                SymbolEvidence(
                    symbol.path,
                    symbol.symbol,
                    symbol.symbol_hash,
                    "src/Model/Service.cs",
                    "Dragons.InvisibleDragon.Model.Service.Run",
                    source_hash(IMPLEMENTATION_SOURCE),
                    (receipt,),
                ),
            ),
        )

    @staticmethod
    def _decision(symbol, identifier: str) -> ScopeDecision:
        return ScopeDecision(
            identifier,
            symbol.path,
            symbol.symbol,
            symbol.symbol_hash,
            "out_of_scope",
            "compiled_rhino_grasshopper_product",
            "Test-only exact product-contract decision.",
            "docs/development/compatibility-policy.md#declared-product-compatibility-scope",
            "approved",
        )

    @staticmethod
    def _executed(
        *,
        output_hash: str = OUTPUT_HASH,
        outcome: str = "passed",
        skipped: bool = False,
        structural_only: bool = False,
        exercised_load: str = "not_applicable",
    ) -> ExecutedAssertion:
        return ExecutedAssertion(
            "service-run-parity",
            "tests/ServiceParityTests.cs",
            "ServiceParityTests.RunMatchesUpstream",
            source_hash(TEST_SOURCE),
            outcome,
            skipped,
            structural_only,
            exercised_load,
            output_hash,
        )

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

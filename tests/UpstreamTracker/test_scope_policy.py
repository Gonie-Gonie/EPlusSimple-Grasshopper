from __future__ import annotations

from dataclasses import replace
import unittest

from support import REPOSITORY_ROOT

from goniegonie_upstream_tracker.compatibility import (
    CompatibilityMatrix,
    MatrixEntry,
    PublicSymbolInventory,
    load_compatibility_configuration,
)
from goniegonie_upstream_tracker.config import load_configuration
from goniegonie_upstream_tracker.errors import ConfigurationError
from goniegonie_upstream_tracker.evidence import ScopeDecision, ScopeDecisionRegistry
from goniegonie_upstream_tracker.scope_policy import (
    BASELINE_SCOPE_KEYS,
    EXPECTED_BASELINE_DECISION_COUNT,
    EXPECTED_SAFE_SCOPE_COUNT,
    EXPECTED_SELECTION_SHA256,
    EXPECTED_SYMBOL_CONTRACT_SHA256,
    RISKY_AUTHORING_KEYS,
    build_safe_scope_plan,
)


NEEDS_RATIONALE = (
    "No symbol-level equivalence, verified exception, or out-of-scope evidence is registered."
)
EXPECTED_FINAL_DECISIONS_SHA256 = (
    "sha256:7550b201dba05d5a277948f7b494b455c7069ecbab2fbbef819e3df33aff1cd6"
)
EXPECTED_FINAL_MATRIX_SHA256 = (
    "sha256:07cadb09328da21726e6252a3bc18b7f1729a93fc51f8220253b878c6902af81"
)


class SafeScopePolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        tracker = load_configuration(
            REPOSITORY_ROOT / "upstream/upstream.lock.json",
            REPOSITORY_ROOT / "upstream/port-map.yml",
            REPOSITORY_ROOT / "upstream/compatibility-exceptions.yml",
        )
        cls.configuration = load_compatibility_configuration(
            tracker,
            REPOSITORY_ROOT / "upstream/compatibility-scope.json",
            REPOSITORY_ROOT / "upstream/public-symbol-inventory.json",
            REPOSITORY_ROOT / "upstream/compatibility-matrix.json",
            REPOSITORY_ROOT / "upstream/symbol-evidence.json",
            REPOSITORY_ROOT / "upstream/scope-decisions.json",
            REPOSITORY_ROOT,
        )

    def test_baseline_integration_adds_exactly_236_reviewed_decisions(self) -> None:
        baseline_matrix, baseline_decisions = self._baseline()

        plan = build_safe_scope_plan(
            self.configuration.inventory,
            baseline_matrix,
            baseline_decisions,
        )

        self.assertEqual(EXPECTED_BASELINE_DECISION_COUNT, plan.previous_decision_count)
        self.assertEqual(236, plan.new_decision_count)
        self.assertEqual(EXPECTED_SAFE_SCOPE_COUNT, len(plan.decisions.decisions))
        self.assertEqual(
            {
                "equivalent": 126,
                "exception": 111,
                "needs_reverification": 753,
                "out_of_scope": 252,
            },
            plan.classification_counts,
        )
        self.assertEqual(EXPECTED_SELECTION_SHA256, plan.selection_sha256)
        self.assertEqual(EXPECTED_SYMBOL_CONTRACT_SHA256, plan.symbol_contract_sha256)
        self.assertEqual(
            EXPECTED_FINAL_DECISIONS_SHA256,
            plan.decisions.content_sha256,
        )
        self.assertEqual(EXPECTED_FINAL_MATRIX_SHA256, plan.matrix.content_sha256)
        self.assertEqual(
            baseline_decisions.decisions,
            tuple(
                item
                for item in plan.decisions.decisions
                if item.key in BASELINE_SCOPE_KEYS
            ),
        )

    def test_risky_authoring_symbols_remain_unresolved(self) -> None:
        baseline_matrix, baseline_decisions = self._baseline()
        plan = build_safe_scope_plan(
            self.configuration.inventory,
            baseline_matrix,
            baseline_decisions,
        )
        entries = plan.matrix.entries_by_key

        self.assertEqual(11, len(RISKY_AUTHORING_KEYS))
        self.assertFalse(RISKY_AUTHORING_KEYS & set(plan.decisions.decisions_by_key))
        self.assertTrue(
            all(
                entries[key].classification == "needs_reverification"
                for key in RISKY_AUTHORING_KEYS
            )
        )
        self.assertEqual(
            "needs_reverification",
            entries[("src/idragon/imugi.py", "IdfObjectList.set_wwr")].classification,
        )

    def test_terminal_scope_additions_are_exactly_bound(self) -> None:
        plan = build_safe_scope_plan(
            self.configuration.inventory,
            self.configuration.matrix,
            self.configuration.scope_decisions,
        )
        decisions = plan.decisions.decisions_by_key
        entries = plan.matrix.entries_by_key
        expected = {
            ("src/epsimple/core/model.py", "GreenRetrofitModel.from_excel"): {
                "id": "scope-src-epsimple-core-model-py-greenretrofitmodel-from-excel-46935cc1",
                "hash": "sha256:46935cc1aaff18b83281df944eb9f099d53c5894f7427ee98fedb8dccefdc206",
                "rationale": (
                    "Historical GREXCEL factory adapter only. The upstream production graph reaches "
                    "it only from the already excluded EXCEL-to-IDF `convert_inputformat` branch, "
                    "while `read_grexcel` is a package alias and `run_grexcel` calls `excel2grjson` "
                    "directly. The body delegates to the already excluded `excel2grjson`, then to "
                    "separately gated `from_grjson`, and deletes the temporary JSON; it adds no GRM, "
                    "IDF, or result semantics. The compiled Grasshopper products expose GRM JSON "
                    "read/write only, and Excel/GREXCEL input conversion and execution are explicitly "
                    "outside the declared product scope."
                ),
            },
            ("src/idragon/constants.py", "SpecialTag"): {
                "id": "scope-src-idragon-constants-py-specialtag-3a4b3781",
                "hash": "sha256:3a4b37818bef17a26ede76602478983f0d70840c5a61fce8475f47e491466e41",
                "rationale": (
                    "The IDragon `SpecialTag` class has no production references outside its own "
                    "docstring examples, declares no enum values, and has no C# product counterpart. "
                    "Its only public members are `__format__`, `__repr__`, and `__str__`, all already "
                    "approved out of scope; the used EPlusSimple tag family remains separately gated. "
                    "Keeping the orphan class declaration in `needs_reverification` would therefore "
                    "reintroduce the excluded Python representation/API surface."
                ),
            },
        }
        for key, values in expected.items():
            decision = decisions[key]
            self.assertEqual(values["id"], decision.identifier)
            self.assertEqual(values["hash"], decision.upstream_symbol_hash)
            self.assertEqual(values["rationale"], decision.rationale)
            self.assertEqual("approved", decision.approval)
            self.assertEqual("out_of_scope", entries[key].classification)
            self.assertEqual(values["rationale"], entries[key].rationale)
            self.assertEqual(
                (f"upstream/scope-decisions.json#{values['id']}",),
                entries[key].evidence,
            )

    def test_plan_is_idempotent_after_integration(self) -> None:
        baseline_matrix, baseline_decisions = self._baseline()
        first = build_safe_scope_plan(
            self.configuration.inventory,
            baseline_matrix,
            baseline_decisions,
        )
        second = build_safe_scope_plan(
            self.configuration.inventory,
            first.matrix,
            first.decisions,
        )

        self.assertEqual(0, second.new_decision_count)
        self.assertEqual(first.decisions, second.decisions)
        self.assertEqual(first.matrix, second.matrix)

    def test_reclassified_insert_decision_is_rejected(self) -> None:
        baseline_matrix, baseline_decisions = self._baseline()
        final = build_safe_scope_plan(
            self.configuration.inventory,
            baseline_matrix,
            baseline_decisions,
        )
        key = ("src/idragon/imugi.py", "IdfObjectList.insert")
        symbol = self.configuration.inventory.symbols_by_key[key]
        forbidden = ScopeDecision(
            "scope-src-idragon-imugi-py-idfobjectlist-insert-"
            + symbol.symbol_hash.removeprefix("sha256:")[:8],
            symbol.path,
            symbol.symbol,
            symbol.symbol_hash,
            "out_of_scope",
            "compiled_rhino_grasshopper_product",
            "Python mutable-container editing entry point.",
            "docs/compatibility.md#declared-product-compatibility-scope",
            "approved",
        )
        invalid_decisions = ScopeDecisionRegistry(
            final.decisions.upstream_commit,
            final.decisions.inventory_sha256,
            tuple(sorted((*final.decisions.decisions, forbidden), key=lambda item: item.exact_key)),
        )
        legacy_entries = tuple(
            MatrixEntry(
                item.path,
                item.symbol,
                "out_of_scope",
                forbidden.rationale,
                (f"upstream/scope-decisions.json#{forbidden.identifier}",),
                None,
            )
            if item.key == key
            else item
            for item in final.matrix.entries
        )
        invalid_matrix = CompatibilityMatrix(
            final.matrix.upstream_commit,
            final.matrix.inventory_sha256,
            legacy_entries,
        )

        with self.assertRaisesRegex(ConfigurationError, "out-of-policy decision"):
            build_safe_scope_plan(
                self.configuration.inventory,
                invalid_matrix,
                invalid_decisions,
            )

    def test_existing_out_of_policy_decision_is_rejected(self) -> None:
        baseline_matrix, baseline_decisions = self._baseline()
        key = sorted(RISKY_AUTHORING_KEYS)[0]
        symbol = self.configuration.inventory.symbols_by_key[key]
        forged = ScopeDecision(
            "scope-forged-risky-authoring-decision",
            symbol.path,
            symbol.symbol,
            symbol.symbol_hash,
            "out_of_scope",
            "compiled_rhino_grasshopper_product",
            "forged",
            "docs/compatibility.md#declared-product-compatibility-scope",
            "approved",
        )
        decisions = ScopeDecisionRegistry(
            baseline_decisions.upstream_commit,
            baseline_decisions.inventory_sha256,
            tuple(sorted((*baseline_decisions.decisions, forged), key=lambda item: item.exact_key)),
        )
        entries = tuple(
            MatrixEntry(
                item.path,
                item.symbol,
                "out_of_scope",
                forged.rationale,
                (f"upstream/scope-decisions.json#{forged.identifier}",),
                None,
            )
            if item.key == key
            else item
            for item in baseline_matrix.entries
        )
        matrix = CompatibilityMatrix(
            baseline_matrix.upstream_commit,
            baseline_matrix.inventory_sha256,
            entries,
        )

        with self.assertRaisesRegex(ConfigurationError, "out-of-policy decision"):
            build_safe_scope_plan(self.configuration.inventory, matrix, decisions)

    def test_selected_symbol_hash_drift_is_fail_closed(self) -> None:
        baseline_matrix, baseline_decisions = self._baseline()
        selected_key = sorted(BASELINE_SCOPE_KEYS)[0]
        symbols = tuple(
            replace(item, symbol_hash="sha256:" + "0" * 64)
            if item.key == selected_key
            else item
            for item in self.configuration.inventory.symbols
        )
        inventory = PublicSymbolInventory(
            self.configuration.inventory.upstream_commit,
            self.configuration.inventory.scope_sha256,
            self.configuration.inventory.files,
            symbols,
        )
        matrix = CompatibilityMatrix(
            baseline_matrix.upstream_commit,
            inventory.content_sha256,
            baseline_matrix.entries,
        )
        decisions = ScopeDecisionRegistry(
            baseline_decisions.upstream_commit,
            inventory.content_sha256,
            baseline_decisions.decisions,
        )

        with self.assertRaisesRegex(ConfigurationError, "symbol contract changed"):
            build_safe_scope_plan(inventory, matrix, decisions)

    def _baseline(self) -> tuple[CompatibilityMatrix, ScopeDecisionRegistry]:
        current_plan = build_safe_scope_plan(
            self.configuration.inventory,
            self.configuration.matrix,
            self.configuration.scope_decisions,
        )
        baseline_decisions = ScopeDecisionRegistry(
            self.configuration.inventory.upstream_commit,
            self.configuration.inventory.content_sha256,
            tuple(
                item
                for item in current_plan.decisions.decisions
                if item.key in BASELINE_SCOPE_KEYS
            ),
        )
        baseline_entries = tuple(
            MatrixEntry(
                item.path,
                item.symbol,
                "needs_reverification",
                NEEDS_RATIONALE,
                (),
                None,
            )
            if item.key in current_plan.decisions.decisions_by_key
            and item.key not in BASELINE_SCOPE_KEYS
            else item
            for item in current_plan.matrix.entries
        )
        return (
            CompatibilityMatrix(
                self.configuration.inventory.upstream_commit,
                self.configuration.inventory.content_sha256,
                baseline_entries,
            ),
            baseline_decisions,
        )


if __name__ == "__main__":
    unittest.main()

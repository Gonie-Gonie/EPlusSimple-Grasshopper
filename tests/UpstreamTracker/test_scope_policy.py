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
    "sha256:b7c4d4d9ce37e66376078359b3c3dcb3b9805bb94e9c1ad6c1fa24a4b0264ec1"
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
                "equivalent": 160,
                "exception": 214,
                "needs_reverification": 616,
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

    def test_constants_metadata_promotion_preserves_adjacent_scope(self) -> None:
        entries = self.configuration.matrix.entries
        expected = {
            568: ("Directory", "resolved-native-runtime-and-resource-layout"),
            569: (
                "Directory.ENERGYPLUS_DIR",
                "explicit-validated-native-energyplus-runtime-root",
            ),
            570: ("Directory.IDD_DIR", "validated-native-idd-path-resolution"),
            571: (
                "Directory.PROFILE_DIR",
                "typed-native-profile-data-without-package-profile-directory",
            ),
            572: ("PackageInfo", "static-native-package-information"),
            573: ("PackageInfo.NAME", "native-invisibledragon-package-name"),
            574: (
                "PackageInfo.REQUIRED_PYTHON",
                "compiled-native-target-framework-contract",
            ),
            575: ("PackageInfo.VERSION", "native-semantic-version-string"),
        }
        for index, (symbol, exception_id) in expected.items():
            entry = entries[index]
            self.assertEqual(("src/idragon/constants.py", symbol), entry.key)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)

        for index, symbol in enumerate(
            (
                "SpecialTag",
                "SpecialTag.__format__",
                "SpecialTag.__repr__",
                "SpecialTag.__str__",
            ),
            start=576,
        ):
            entry = entries[index]
            self.assertEqual(("src/idragon/constants.py", symbol), entry.key)
            self.assertEqual("out_of_scope", entry.classification, symbol)

        for index, symbol in enumerate(
            (
                "Directory",
                "Directory.CONSTRUCTION_DIR",
                "Directory.PROFILE_DIR",
                "Directory.WEATHER_DATA_DIR",
                "Directory.WEATHER_META_DIR",
                "PackageInfo",
                "PackageInfo.NAME",
                "PackageInfo.REQUIRED_PYTHON",
                "PackageInfo.VERSION",
            ),
            start=31,
        ):
            entry = entries[index]
            self.assertEqual(("src/epsimple/constants.py", symbol), entry.key)
            self.assertEqual("needs_reverification", entry.classification, symbol)

    def test_epsimple_numeric_constants_promotion_preserves_adjacent_scope(self) -> None:
        entries = self.configuration.matrix.entries
        class_targets = {
            28: (
                "ConvectionHeatTransfer",
                "native-simpledragon-convection-constant-container",
            ),
            40: ("Site2CO2", "native-simpledragon-site-to-carbon-dispatch"),
            46: ("Site2Cost", "native-simpledragon-site-to-cost-dispatch"),
            52: ("Site2Source", "native-simpledragon-site-to-source-dispatch"),
            67: ("Unit", "native-simpledragon-unit-conversion-constants"),
        }
        equivalent_targets = {
            29: "ConvectionHeatTransfer.IN",
            30: "ConvectionHeatTransfer.OUT",
            41: "Site2CO2.DISTRICTHEATING",
            42: "Site2CO2.ELECTRICITY",
            43: "Site2CO2.LPG",
            44: "Site2CO2.NATURALGAS",
            45: "Site2CO2.OIL",
            47: "Site2Cost.DISTRICTHEATING",
            48: "Site2Cost.ELECTRICITY",
            49: "Site2Cost.LPG",
            50: "Site2Cost.NATURALGAS",
            51: "Site2Cost.OIL",
            53: "Site2Source.DISTRICTHEATING",
            54: "Site2Source.ELECTRICITY",
            55: "Site2Source.LPG",
            56: "Site2Source.NATURALGAS",
            57: "Site2Source.OIL",
            68: "Unit.ACH50_TO_ACH",
            69: "Unit.FRACTION_TO_PERCENT",
            70: "Unit.M3_PER_S_TO_CMH",
            71: "Unit.MM_TO_M",
            72: "Unit.M_TO_MM",
            73: "Unit.PERCENT_TO_FRACTION",
            74: "Unit.W_TO_KW",
        }
        target_indices = (*range(28, 31), *range(40, 58), *range(67, 75))
        self.assertEqual(
            target_indices,
            tuple(sorted((*class_targets, *equivalent_targets))),
        )
        self.assertEqual(5, len(class_targets))
        self.assertEqual(24, len(equivalent_targets))

        for index, (symbol, exception_id) in class_targets.items():
            entry = entries[index]
            self.assertEqual(("src/epsimple/constants.py", symbol), entry.key)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)

        for index, symbol in equivalent_targets.items():
            entry = entries[index]
            self.assertEqual(("src/epsimple/constants.py", symbol), entry.key)
            self.assertEqual("equivalent", entry.classification, symbol)
            self.assertIsNone(entry.exception_id, symbol)

        self.assertEqual(
            5,
            sum(entries[index].classification == "exception" for index in target_indices),
        )
        self.assertEqual(
            24,
            sum(entries[index].classification == "equivalent" for index in target_indices),
        )

        adjacent_families = (
            (
                10,
                (
                    ("AUTOID_PREFIX", "needs_reverification"),
                    ("AUTOID_PREFIX.DAY_SCHEDULE", "needs_reverification"),
                    ("AUTOID_PREFIX.FENESTRATION", "needs_reverification"),
                    ("AUTOID_PREFIX.FENESTRATION_CONSTRUCTION", "needs_reverification"),
                    ("AUTOID_PREFIX.HEAT_EXCHANGER", "needs_reverification"),
                    ("AUTOID_PREFIX.MATERIAL", "needs_reverification"),
                    ("AUTOID_PREFIX.PROFILE", "needs_reverification"),
                    ("AUTOID_PREFIX.PV_PANEL", "needs_reverification"),
                    ("AUTOID_PREFIX.RULESET", "needs_reverification"),
                    ("AUTOID_PREFIX.SCHEDULE", "needs_reverification"),
                    ("AUTOID_PREFIX.SOURCE_SYSTEM", "needs_reverification"),
                    ("AUTOID_PREFIX.SUPPLY_SYSTEM", "needs_reverification"),
                    ("AUTOID_PREFIX.SURFACE", "needs_reverification"),
                    ("AUTOID_PREFIX.SURFACE_CONSTRUCTION", "needs_reverification"),
                    ("AUTOID_PREFIX.ZONE", "needs_reverification"),
                    ("AUTOID_PREFIX.__format__", "needs_reverification"),
                    ("AUTOID_PREFIX.__repr__", "out_of_scope"),
                    ("AUTOID_PREFIX.__str__", "needs_reverification"),
                ),
            ),
            (
                31,
                (
                    ("Directory", "needs_reverification"),
                    ("Directory.CONSTRUCTION_DIR", "needs_reverification"),
                    ("Directory.PROFILE_DIR", "needs_reverification"),
                    ("Directory.WEATHER_DATA_DIR", "needs_reverification"),
                    ("Directory.WEATHER_META_DIR", "needs_reverification"),
                    ("PackageInfo", "needs_reverification"),
                    ("PackageInfo.NAME", "needs_reverification"),
                    ("PackageInfo.REQUIRED_PYTHON", "needs_reverification"),
                    ("PackageInfo.VERSION", "needs_reverification"),
                ),
            ),
            (
                58,
                (
                    ("SpecialTag", "needs_reverification"),
                    ("SpecialTag.CLONE", "needs_reverification"),
                    ("SpecialTag.COOLROOF", "needs_reverification"),
                    ("SpecialTag.DB", "needs_reverification"),
                    ("SpecialTag.FLIP", "needs_reverification"),
                    ("SpecialTag.SPECIAL", "needs_reverification"),
                    ("SpecialTag.__format__", "needs_reverification"),
                    ("SpecialTag.__repr__", "out_of_scope"),
                    ("SpecialTag.__str__", "needs_reverification"),
                ),
            ),
        )
        for start, expected in adjacent_families:
            for offset, (symbol, classification) in enumerate(expected):
                entry = entries[start + offset]
                self.assertEqual(("src/epsimple/constants.py", symbol), entry.key)
                self.assertEqual(classification, entry.classification, symbol)
                self.assertIsNone(entry.exception_id, symbol)

    def test_air_boundary_core_promotion_preserves_adjacent_construction_scope(self) -> None:
        entries = self.configuration.matrix.entries
        expected_targets = {
            588: (
                "AirBoundary",
                "permissive-mutable-python-air-boundary-state-fd8f9bb9",
            ),
            589: (
                "AirBoundary.__init__",
                "unchecked-python-air-boundary-construction-a69bf707",
            ),
        }
        for index, (symbol, exception_id) in expected_targets.items():
            entry = entries[index]
            self.assertEqual(("src/idragon/dragon/construction.py", symbol), entry.key)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)

        expected_adjacent_classifications = (
            "out_of_scope",
            "out_of_scope",
            "exception",
            "needs_reverification",
            "needs_reverification",
            "exception",
            "exception",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "exception",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "out_of_scope",
            "out_of_scope",
            "exception",
            "needs_reverification",
            "needs_reverification",
            "exception",
            "exception",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "exception",
            "needs_reverification",
            "exception",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "needs_reverification",
            "out_of_scope",
            "out_of_scope",
            "exception",
        )
        self.assertEqual(
            expected_adjacent_classifications,
            tuple(entry.classification for entry in entries[590:641]),
        )
        self.assertEqual(
            ("src/idragon/dragon/construction.py", "AirBoundary.__repr__"),
            entries[590].key,
        )
        self.assertEqual(
            ("src/idragon/dragon/construction.py", "AirBoundary.__str__"),
            entries[591].key,
        )
        self.assertEqual(
            ("src/idragon/dragon/construction.py", "AirBoundary.to_idf_object"),
            entries[592].key,
        )
        self.assertEqual(
            "model-context-air-boundary-idf-emission",
            entries[592].exception_id,
        )
        self.assertEqual(
            ("src/idragon/dragon/construction.py", "NoMassConstruction.to_idf_object"),
            entries[640].key,
        )
        self.assertEqual(
            "model-context-no-mass-construction-idf-emission",
            entries[640].exception_id,
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

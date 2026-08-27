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
    "sha256:832744a78e146de7b469eea11b2a8de6177cd075030f07ea4bc6750292d96178"
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
                "equivalent": 296,
                "exception": 373,
                "needs_reverification": 321,
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

        expected_epsimple_metadata = {
            31: ("Directory", "embedded-explicit-native-resource-layout-5b876ad7"),
            32: (
                "Directory.CONSTRUCTION_DIR",
                "embedded-native-construction-resources-91c573a0",
            ),
            33: (
                "Directory.PROFILE_DIR",
                "embedded-native-profile-resources-f65d5eae",
            ),
            34: (
                "Directory.WEATHER_DATA_DIR",
                "caller-supplied-native-weather-data-root-8a5bf654",
            ),
            35: (
                "Directory.WEATHER_META_DIR",
                "embedded-native-weather-metadata-resources-15e81d1d",
            ),
            36: (
                "PackageInfo",
                "static-native-simpledragon-package-information-aaf5b98d",
            ),
            37: ("PackageInfo.NAME", "native-simpledragon-package-name-537c8c3b"),
            38: (
                "PackageInfo.REQUIRED_PYTHON",
                "compiled-simpledragon-target-framework-contract-cf74d0eb",
            ),
            39: (
                "PackageInfo.VERSION",
                "native-simpledragon-and-upstream-version-identity-a8260e5f",
            ),
        }
        for index, (symbol, exception_id) in expected_epsimple_metadata.items():
            entry = entries[index]
            self.assertEqual(("src/epsimple/constants.py", symbol), entry.key)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)

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
                    (
                        "AUTOID_PREFIX",
                        "exception",
                        "immutable-native-auto-id-prefix-catalog-9a7c270a",
                    ),
                    ("AUTOID_PREFIX.DAY_SCHEDULE", "equivalent", None),
                    ("AUTOID_PREFIX.FENESTRATION", "equivalent", None),
                    ("AUTOID_PREFIX.FENESTRATION_CONSTRUCTION", "equivalent", None),
                    ("AUTOID_PREFIX.HEAT_EXCHANGER", "equivalent", None),
                    ("AUTOID_PREFIX.MATERIAL", "equivalent", None),
                    ("AUTOID_PREFIX.PROFILE", "equivalent", None),
                    ("AUTOID_PREFIX.PV_PANEL", "equivalent", None),
                    ("AUTOID_PREFIX.RULESET", "equivalent", None),
                    ("AUTOID_PREFIX.SCHEDULE", "equivalent", None),
                    ("AUTOID_PREFIX.SOURCE_SYSTEM", "equivalent", None),
                    ("AUTOID_PREFIX.SUPPLY_SYSTEM", "equivalent", None),
                    ("AUTOID_PREFIX.SURFACE", "equivalent", None),
                    ("AUTOID_PREFIX.SURFACE_CONSTRUCTION", "equivalent", None),
                    ("AUTOID_PREFIX.ZONE", "equivalent", None),
                    ("AUTOID_PREFIX.__format__", "equivalent", None),
                    ("AUTOID_PREFIX.__repr__", "out_of_scope", None),
                    ("AUTOID_PREFIX.__str__", "equivalent", None),
                ),
            ),
            (
                31,
                (
                    (
                        "Directory",
                        "exception",
                        "embedded-explicit-native-resource-layout-5b876ad7",
                    ),
                    (
                        "Directory.CONSTRUCTION_DIR",
                        "exception",
                        "embedded-native-construction-resources-91c573a0",
                    ),
                    (
                        "Directory.PROFILE_DIR",
                        "exception",
                        "embedded-native-profile-resources-f65d5eae",
                    ),
                    (
                        "Directory.WEATHER_DATA_DIR",
                        "exception",
                        "caller-supplied-native-weather-data-root-8a5bf654",
                    ),
                    (
                        "Directory.WEATHER_META_DIR",
                        "exception",
                        "embedded-native-weather-metadata-resources-15e81d1d",
                    ),
                    (
                        "PackageInfo",
                        "exception",
                        "static-native-simpledragon-package-information-aaf5b98d",
                    ),
                    (
                        "PackageInfo.NAME",
                        "exception",
                        "native-simpledragon-package-name-537c8c3b",
                    ),
                    (
                        "PackageInfo.REQUIRED_PYTHON",
                        "exception",
                        "compiled-simpledragon-target-framework-contract-cf74d0eb",
                    ),
                    (
                        "PackageInfo.VERSION",
                        "exception",
                        "native-simpledragon-and-upstream-version-identity-a8260e5f",
                    ),
                ),
            ),
            (
                58,
                (
                    (
                        "SpecialTag",
                        "exception",
                        "immutable-native-special-tag-catalog-a66e2175",
                    ),
                    ("SpecialTag.CLONE", "equivalent", None),
                    ("SpecialTag.COOLROOF", "equivalent", None),
                    ("SpecialTag.DB", "equivalent", None),
                    ("SpecialTag.FLIP", "equivalent", None),
                    ("SpecialTag.SPECIAL", "equivalent", None),
                    ("SpecialTag.__format__", "equivalent", None),
                    ("SpecialTag.__repr__", "out_of_scope", None),
                    ("SpecialTag.__str__", "equivalent", None),
                ),
            ),
        )
        for start, expected in adjacent_families:
            for offset, (symbol, classification, exception_id) in enumerate(expected):
                entry = entries[start + offset]
                self.assertEqual(("src/epsimple/constants.py", symbol), entry.key)
                self.assertEqual(classification, entry.classification, symbol)
                self.assertEqual(exception_id, entry.exception_id, symbol)

        identifier_indices = (
            *range(10, 26),
            27,
            *range(31, 40),
            *range(58, 65),
            66,
        )
        self.assertEqual(34, len(identifier_indices))
        self.assertEqual(
            {"equivalent": 23, "exception": 11},
            {
                classification: sum(
                    entries[index].classification == classification
                    for index in identifier_indices
                )
                for classification in ("equivalent", "exception")
            },
        )
        for index in identifier_indices:
            entry = entries[index]
            inventory_symbol = self.configuration.inventory.symbols[index]
            assertion_id = (
                f"epsimple-identifier-conventions-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if entry.exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{entry.exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, entry.key)
        for index, symbol in (
            (26, "AUTOID_PREFIX.__repr__"),
            (65, "SpecialTag.__repr__"),
        ):
            entry = entries[index]
            self.assertEqual(("src/epsimple/constants.py", symbol), entry.key)
            self.assertEqual("out_of_scope", entry.classification, symbol)
            self.assertEqual(1, len(entry.evidence), symbol)
            self.assertTrue(
                entry.evidence[0].startswith("upstream/scope-decisions.json#"),
                symbol,
            )

    def test_construction_core_promotion_preserves_adjacent_construction_scope(self) -> None:
        entries = self.configuration.matrix.entries
        expected_targets = {
            588: ("AirBoundary", "exception", "permissive-mutable-python-air-boundary-state-fd8f9bb9"),
            589: ("AirBoundary.__init__", "exception", "unchecked-python-air-boundary-construction-a69bf707"),
            593: ("Construction", "exception", "immutable-validated-native-construction-451c832a"),
            594: ("Construction.U", "equivalent", None),
            597: ("Construction.__init__", "exception", "typed-nonempty-native-construction-init-c99eac6b"),
            598: ("Construction.heat_capacity", "equivalent", None),
            599: ("Construction.reversed", "exception", "immutable-validated-native-construction-reverse-f3f8b2b1"),
            600: ("Construction.thickness", "equivalent", None),
            602: ("Glazing", "exception", "immutable-validated-native-glazing-5615eebb"),
            603: ("Glazing.G", "exception", "immutable-bounded-native-glazing-g-cb8ad4be"),
            604: ("Glazing.U", "exception", "immutable-finite-native-glazing-u-98ebe259"),
            605: ("Glazing.__init__", "exception", "validated-immutable-native-glazing-init-bfe7247a"),
            609: ("Layer", "exception", "immutable-validated-native-layer-e6a3fe0d"),
            610: ("Layer.U", "equivalent", None),
            613: ("Layer.__init__", "exception", "validated-immutable-native-layer-init-60e437a1"),
            614: ("Layer.heat_capacity", "equivalent", None),
            615: ("Layer.material", "exception", "immutable-required-native-layer-material-6454844c"),
            616: ("Layer.thickness", "exception", "immutable-finite-native-layer-thickness-d7d789d7"),
            618: ("Material", "exception", "immutable-validated-native-material-15ad6614"),
            620: ("Material.__init__", "exception", "validated-immutable-native-material-init-d78cab39"),
            621: ("Material.conductivity", "exception", "immutable-finite-native-material-conductivity-b733b56b"),
            622: ("Material.density", "exception", "immutable-finite-native-material-density-23136324"),
            623: ("Material.roughness", "exception", "immutable-strongly-typed-native-material-roughness-be23eedd"),
            624: ("Material.solar_absorptance", "exception", "immutable-finite-native-material-solar-absorptance-ae7ce02b"),
            625: ("Material.specific_heat", "exception", "immutable-finite-native-material-specific-heat-abf4a2ea"),
            626: ("Material.thermal_absorptance", "exception", "immutable-finite-native-material-thermal-absorptance-f17730ed"),
            627: ("Material.visible_absorptance", "exception", "immutable-finite-native-material-visible-absorptance-ecf6d77d"),
            628: ("MaterialRoughness", "exception", "strongly-typed-native-material-roughness-enum-fc281859"),
            629: ("MaterialRoughness.MEDIUMROUGH", "equivalent", None),
            630: ("MaterialRoughness.MEDIUMSMOOTH", "equivalent", None),
            631: ("MaterialRoughness.ROUGH", "equivalent", None),
            632: ("MaterialRoughness.SMOOTH", "equivalent", None),
            633: ("MaterialRoughness.VERYROUGH", "equivalent", None),
            634: ("MaterialRoughness.__str__", "equivalent", None),
            635: ("NoMassConstruction", "exception", "immutable-validated-native-no-mass-construction-9dff867c"),
            636: ("NoMassConstruction.U", "exception", "immutable-finite-native-no-mass-u-98ebe259"),
            637: ("NoMassConstruction.__init__", "exception", "validated-immutable-native-no-mass-init-47497892"),
        }
        for index, (symbol, classification, exception_id) in expected_targets.items():
            entry = entries[index]
            self.assertEqual(("src/idragon/dragon/construction.py", symbol), entry.key)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)

        construction_core_targets = {
            index: values for index, values in expected_targets.items() if index >= 593
        }
        self.assertEqual(35, len(construction_core_targets))
        self.assertEqual(
            {"equivalent": 11, "exception": 24},
            {
                classification: sum(
                    values[1] == classification
                    for values in construction_core_targets.values()
                )
                for classification in ("equivalent", "exception")
            },
        )
        expected_adjacent_overrides = {
            590: "out_of_scope",
            591: "out_of_scope",
            592: "exception",
            595: "exception",
            596: "exception",
            601: "exception",
            606: "out_of_scope",
            607: "out_of_scope",
            608: "exception",
            611: "exception",
            612: "exception",
            617: "exception",
            619: "exception",
            638: "out_of_scope",
            639: "out_of_scope",
            640: "exception",
            **{
                index: values[1]
                for index, values in construction_core_targets.items()
            },
        }
        expected_adjacent_classifications = tuple(
            expected_adjacent_overrides.get(index, "needs_reverification")
            for index in range(590, 641)
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

    def test_epsimple_model_core_promotion_is_exact_and_bounded(self) -> None:
        entries = self.configuration.matrix.entries
        targets = {
            337: ("ADDR_WEATHER_TABLE", "typed-packaged-weather-database-rather-than-mutable-dataframe-1a4029a1"),
            338: ("CLIMATE_TABLE", "typed-date-indexed-weather-database-rather-than-mutable-dataframe-fbfb5af8"),
            339: ("EnergyPlusError", "structured-diagnostics-rather-than-throwing-table-wrapper-3ed10042"),
            340: ("EnergyPlusError.__init__", "energyplus-failure-and-result-builder-diagnostics-328cf73b"),
            341: ("GreenRetrofitModel", "immutable-floor-and-catalog-aggregate-rather-than-mutable-zone-list-fb39a800"),
            342: ("GreenRetrofitModel.__init__", "immutable-defensive-copy-constructor-with-explicit-weather-e8bd64b7"),
            345: ("GreenRetrofitModel.address", "readonly-address-with-explicit-weather-selection-df358686"),
            346: ("GreenRetrofitModel.area", None),
            347: ("GreenRetrofitModel.averaged_exteriorfloor_Uvalue", "nullable-construction-filter-rather-than-singleton-identity-regulation-ef752eff"),
            348: ("GreenRetrofitModel.averaged_exteriorroof_Uvalue", "nullable-construction-filter-rather-than-singleton-identity-regulation-871c1b93"),
            349: ("GreenRetrofitModel.averaged_exteriorwall_Uvalue", "nullable-construction-filter-rather-than-singleton-identity-regulation-13f93b86"),
            350: ("GreenRetrofitModel.averaged_infiltration", None),
            351: ("GreenRetrofitModel.averaged_lightdensity", "nullable-light-density-excluded-from-weight-denominator-695c215a"),
            352: ("GreenRetrofitModel.averaged_window_Uvalue", "native-window-projection-also-includes-glass-doors-235f45cc"),
            353: ("GreenRetrofitModel.climate", None),
            354: ("GreenRetrofitModel.exteriorfloors", None),
            355: ("GreenRetrofitModel.exteriorroofs", None),
            356: ("GreenRetrofitModel.exteriorwalls", None),
            357: ("GreenRetrofitModel.exteriorwindows", "native-window-projection-also-includes-glass-doors-d363d717"),
            359: ("GreenRetrofitModel.from_grjson", None),
            360: ("GreenRetrofitModel.get_unique_fenestration_constructions", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-0963ad71"),
            361: ("GreenRetrofitModel.get_unique_materials", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-ecb20cb3"),
            362: ("GreenRetrofitModel.get_unique_profiles", "database-resolved-zone-profiles-rather-than-derived-overwrite-map-13af13a1"),
            363: ("GreenRetrofitModel.get_unique_surface_constructions", "explicit-validated-model-catalog-rather-than-derived-overwrite-map-a05748b1"),
            364: ("GreenRetrofitModel.north_axis", None),
            365: ("GreenRetrofitModel.run", "async-runner-and-result-builder-diagnostic-boundary-bf192ec8"),
            366: ("GreenRetrofitModel.source_system", "immutable-explicit-catalog-rather-than-computed-plus-unvalidated-merge-b2b62b80"),
            367: ("GreenRetrofitModel.terrain", None),
            368: ("GreenRetrofitModel.to_dragon", "nonthrowing-aggregate-conversion-result-with-diagnostics-5e2e21f3"),
            369: ("GreenRetrofitModel.to_idf", "native-idf-document-conversion-result-with-diagnostics-e8d26d72"),
            370: ("GreenRetrofitModel.vintage", None),
            371: ("GreenRetrofitModel.weather", None),
            372: ("GreenRetrofitModel.weather_filepath", "epw-filename-with-caller-owned-directory-resolution-fa174585"),
            387: ("InvalidAddressError", "lookup-diagnostic-rather-than-address-exception-aee12b8f"),
            388: ("address_to_weather", "typed-nonthrowing-weather-selection-result-6e86f546"),
        }
        self.assertEqual(35, len(targets))
        self.assertEqual(24, sum(adaptation is not None for _, adaptation in targets.values()))
        self.assertEqual(11, sum(adaptation is None for _, adaptation in targets.values()))

        for index, (symbol, adaptation) in targets.items():
            entry = entries[index]
            inventory_symbol = self.configuration.inventory.symbols[index]
            self.assertEqual(
                ("src/epsimple/core/model.py", symbol),
                inventory_symbol.key,
                symbol,
            )
            classification = "exception" if adaptation is not None else "equivalent"
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(adaptation, entry.exception_id, symbol)
            assertion_id = (
                f"epsimple-model-core-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if adaptation is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{adaptation}"
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, symbol)
            for exact_binding in (
                assertion_id,
                "commit d48f97a",
                "sha256:e5cfdc9ba823dc891693864051ffb8cbc06cd08137becef9d6c06fd0c2942cf6",
                "sha256:1d3679ac9c0f3aa9469434235cedb17099bc728ede8a2afd9cb0c0b8af6f9832",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)

        excluded = {
            343: "GreenRetrofitModel.__repr__",
            344: "GreenRetrofitModel.__str__",
            358: "GreenRetrofitModel.from_excel",
        }
        deferred = {
            index: self.configuration.inventory.symbols[index].symbol
            for index in range(373, 387)
        }
        self.assertEqual(
            set(range(337, 389)),
            set(targets) | set(excluded) | set(deferred),
        )
        for index, symbol in excluded.items():
            entry = entries[index]
            self.assertEqual(("src/epsimple/core/model.py", symbol), entry.key, symbol)
            self.assertEqual("out_of_scope", entry.classification, symbol)
            self.assertIsNone(entry.exception_id, symbol)

    def test_epsimple_hvac_enums_base_promotion_is_exact_and_bounded(self) -> None:
        entries = self.configuration.matrix.entries
        target_symbols = {
            185: "CompressorType",
            186: "CompressorType.RECIPROCATING",
            187: "CompressorType.SCREW",
            188: "CompressorType.TURBO",
            189: "CompressorType.__str__",
            190: "CompressorType.to_dragon",
            191: "CoolingTowerControl",
            192: "CoolingTowerControl.SINGLESPEED",
            193: "CoolingTowerControl.TWOSPEED",
            194: "CoolingTowerControl.__str__",
            195: "CoolingTowerType",
            196: "CoolingTowerType.CLOSED",
            197: "CoolingTowerType.OPEN",
            198: "CoolingTowerType.__str__",
            240: "Fuel",
            241: "Fuel.DISTRICTHEATING",
            242: "Fuel.ELECTRICITY",
            243: "Fuel.LPG",
            244: "Fuel.NATURALGAS",
            245: "Fuel.OIL",
            246: "Fuel.__str__",
            247: "Fuel.to_dragon",
            267: "NoneSource",
            268: "NoneSource.ID",
            269: "NoneSource.__new__",
            270: "NoneSource.to_dragon",
            319: "SourceSystem",
            320: "SourceSystem.TYPE_MAPPER",
        }
        exception_ids = {
            "CompressorType.__str__": "compressor-type-grm-vocabulary-rather-than-native-enum-tostring-f40e4929",
            "CoolingTowerControl.__str__": "cooling-tower-control-grm-vocabulary-rather-than-native-enum-tostring-f40e4929",
            "CoolingTowerType.__str__": "cooling-tower-type-grm-vocabulary-rather-than-native-enum-tostring-f40e4929",
            "Fuel.__str__": "fuel-grm-vocabulary-rather-than-native-enum-tostring-f40e4929",
            "NoneSource": "nullable-resolved-source-reference-rather-than-singleton-sentinel-8824a756",
            "NoneSource.ID": "null-source-reference-rather-than-special-string-identifier-dbf0ef4b",
            "NoneSource.__new__": "nullable-source-state-rather-than-process-global-singleton-758d9c0b",
            "NoneSource.to_dragon": "aggregate-converter-diagnostic-for-unresolved-source-rather-than-null-return-c8347dc8",
            "SourceSystem": "sealed-validated-domain-aggregate-rather-than-empty-python-base-9b6905f8",
            "SourceSystem.TYPE_MAPPER": "grm-reader-enum-dispatch-rather-than-public-mutable-class-map-813567e3",
        }
        self.assertEqual(28, len(target_symbols))
        self.assertEqual(10, len(exception_ids))

        for index, symbol in target_symbols.items():
            entry = entries[index]
            inventory_symbol = self.configuration.inventory.symbols[index]
            self.assertEqual(
                ("src/epsimple/core/hvac.py", symbol),
                inventory_symbol.key,
                symbol,
            )
            classification = "exception" if symbol in exception_ids else "equivalent"
            exception_id = exception_ids.get(symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            assertion_id = (
                f"epsimple-hvac-enums-base-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, symbol)
            for exact_binding in (
                assertion_id,
                "commit 85264dd",
                "sha256:5bf5e8f88a2050232aa45e79c48894a54897eea57cddaf75697ab914d9715b7c",
                "sha256:eaa5691d29c341844097c8690f0e12970824494f1e00e8287811b7876ba3df0d",
                "sha256:b6331cef12c6ff6809c4beb569f73ab528b04dde3f8f032db6651c5d418d0428",
                "sha256:fd9d587384f4fd980d9765723aac63b5625619b51dd4645a6e0a14882381c1c4",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                self.assertIn(exception_id, entry.rationale, symbol)

        self.assertEqual(
            {"equivalent": 18, "exception": 10},
            {
                classification: sum(
                    entries[index].classification == classification
                    for index in target_symbols
                )
                for classification in ("equivalent", "exception")
            },
        )
        excluded_indices = {
            137,
            138,
            140,
            141,
            149,
            150,
            152,
            153,
            159,
            160,
            162,
            163,
            172,
            173,
            175,
            176,
            201,
            202,
            204,
            205,
            211,
            212,
            214,
            215,
            221,
            222,
            224,
            225,
            232,
            233,
            235,
            236,
            249,
            250,
            255,
            256,
            258,
            259,
            273,
            274,
            276,
            277,
            285,
            286,
            288,
            289,
            298,
            299,
            301,
            302,
            310,
            311,
            313,
            314,
            327,
            328,
            330,
            331,
        }
        thermal_target_indices = {
            135,
            136,
            139,
            *range(142, 147),
            157,
            158,
            161,
            *range(164, 170),
            170,
            171,
            174,
            *range(177, 185),
            199,
            200,
            203,
            *range(206, 209),
            248,
            251,
            252,
            253,
            254,
            257,
            *range(260, 267),
        }
        deferred_indices = (
            set(range(135, 337))
            - set(target_symbols)
            - excluded_indices
            - thermal_target_indices
        )
        self.assertEqual(58, len(excluded_indices))
        self.assertEqual(47, len(thermal_target_indices))
        self.assertEqual(69, len(deferred_indices))
        self.assertEqual(
            set(range(135, 337)),
            set(target_symbols)
            | thermal_target_indices
            | excluded_indices
            | deferred_indices,
        )
        for index in excluded_indices:
            entry = entries[index]
            self.assertEqual("out_of_scope", entry.classification, index)
            self.assertIsNone(entry.exception_id, index)
        for index in deferred_indices:
            entry = entries[index]
            self.assertEqual("needs_reverification", entry.classification, index)
            self.assertIsNone(entry.exception_id, index)
            self.assertEqual((), entry.evidence, index)
            self.assertEqual(NEEDS_RATIONALE, entry.rationale, index)

    def test_epsimple_hvac_thermal_source_promotion_is_exact_and_bounded(self) -> None:
        entries = self.configuration.matrix.entries
        targets = {
            135: 'AbsorptionChiller',
            136: 'AbsorptionChiller.ID',
            139: 'AbsorptionChiller.__init__',
            142: 'AbsorptionChiller.boiler_efficiency',
            143: 'AbsorptionChiller.capacity',
            144: 'AbsorptionChiller.cop',
            145: 'AbsorptionChiller.from_json',
            146: 'AbsorptionChiller.to_dragon',
            157: 'Boiler',
            158: 'Boiler.ID',
            161: 'Boiler.__init__',
            164: 'Boiler.capacity',
            165: 'Boiler.efficiency',
            166: 'Boiler.from_json',
            167: 'Boiler.fuel',
            168: 'Boiler.hotwater_supply',
            169: 'Boiler.to_dragon',
            170: 'Chiller',
            171: 'Chiller.ID',
            174: 'Chiller.__init__',
            177: 'Chiller.capacity',
            178: 'Chiller.compressor_type',
            179: 'Chiller.coolingtower_capacity',
            180: 'Chiller.coolingtower_control',
            181: 'Chiller.coolingtower_type',
            182: 'Chiller.cop',
            183: 'Chiller.from_json',
            184: 'Chiller.to_dragon',
            199: 'DistrictHeating',
            200: 'DistrictHeating.ID',
            203: 'DistrictHeating.__init__',
            206: 'DistrictHeating.from_json',
            207: 'DistrictHeating.hotwater_supply',
            208: 'DistrictHeating.to_dragon',
            248: 'GeothermalHeatPump',
            251: 'GeothermalHeatPump.from_json',
            252: 'GeothermalHeatPump.to_dragon',
            253: 'HeatPump',
            254: 'HeatPump.ID',
            257: 'HeatPump.__init__',
            260: 'HeatPump.cooling_capacity',
            261: 'HeatPump.cooling_cop',
            262: 'HeatPump.from_json',
            263: 'HeatPump.fuel',
            264: 'HeatPump.heating_capacity',
            265: 'HeatPump.heating_cop',
            266: 'HeatPump.to_dragon',
        }
        exception_symbols = {
            'AbsorptionChiller',
            'AbsorptionChiller.__init__',
            'AbsorptionChiller.from_json',
            'AbsorptionChiller.to_dragon',
            'Boiler',
            'Boiler.__init__',
            'Boiler.from_json',
            'Boiler.to_dragon',
            'Chiller',
            'Chiller.__init__',
            'Chiller.from_json',
            'Chiller.to_dragon',
            'DistrictHeating',
            'DistrictHeating.__init__',
            'DistrictHeating.from_json',
            'DistrictHeating.to_dragon',
            'GeothermalHeatPump',
            'GeothermalHeatPump.from_json',
            'GeothermalHeatPump.to_dragon',
            'HeatPump',
            'HeatPump.__init__',
            'HeatPump.from_json',
            'HeatPump.to_dragon',
        }
        self.assertEqual(47, len(targets))
        self.assertEqual(23, len(exception_symbols))

        for index, symbol in targets.items():
            entry = entries[index]
            inventory_symbol = self.configuration.inventory.symbols[index]
            self.assertEqual(
                ("src/epsimple/core/hvac.py", symbol),
                inventory_symbol.key,
                symbol,
            )
            classification = (
                "exception" if symbol in exception_symbols else "equivalent"
            )
            exception_id = (
                "reviewed-native-discriminated-source-aggregate-and-conversion-route-"
                + inventory_symbol.symbol_hash.removeprefix("sha256:")[:8]
                if classification == "exception"
                else None
            )
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            assertion_id = (
                f"epsimple-hvac-thermal-source-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, symbol)
            for exact_binding in (
                assertion_id,
                "commit 0ef3a7d",
                "sha256:e78e8bcbe42cd236775db63d50088bad82a9e9c5328e5fa5de6873d069984391",
                "sha256:e930c9242c76b48500010e76f625e41baa07de96e4629b447df61db6c571e51c",
                "sha256:ca7fb52d4a68ada17437d9e4590b129cf22cce842b37147aacf76d4f17c92265",
                "sha256:b7c9f676404298d22903b3f4c038eb37f9648612a41e6ee05cbe60368b93aee3",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                self.assertIn(exception_id, entry.rationale, symbol)

        self.assertEqual(
            {"equivalent": 24, "exception": 23},
            {
                classification: sum(
                    entries[index].classification == classification
                    for index in targets
                )
                for classification in ("equivalent", "exception")
            },
        )
        enum_indices = {
            *range(185, 199),
            *range(240, 248),
            *range(267, 271),
            319,
            320,
        }
        supply_indices = {
            147,
            148,
            151,
            154,
            155,
            156,
            209,
            210,
            213,
            216,
            217,
            218,
            219,
            220,
            223,
            226,
            227,
            228,
            229,
            230,
            231,
            234,
            237,
            238,
            239,
            271,
            272,
            275,
            278,
            279,
            280,
            281,
            282,
            296,
            297,
            300,
            303,
            304,
            305,
            306,
            307,
            308,
            309,
            312,
            315,
            316,
            317,
            318,
            321,
            322,
            323,
            324,
        }
        other_indices = {
            283,
            284,
            287,
            290,
            291,
            292,
            293,
            294,
            295,
            325,
            326,
            329,
            332,
            333,
            334,
            335,
            336,
        }
        excluded_indices = (
            set(range(135, 337))
            - set(targets)
            - enum_indices
            - supply_indices
            - other_indices
        )
        self.assertEqual(28, len(enum_indices))
        self.assertEqual(52, len(supply_indices))
        self.assertEqual(17, len(other_indices))
        self.assertEqual(58, len(excluded_indices))
        self.assertEqual(
            set(range(135, 337)),
            set(targets)
            | enum_indices
            | supply_indices
            | other_indices
            | excluded_indices,
        )
        self.assertEqual(
            {"equivalent": 18, "exception": 10},
            {
                classification: sum(
                    entries[index].classification == classification
                    for index in enum_indices
                )
                for classification in ("equivalent", "exception")
            },
        )
        for index in supply_indices | other_indices:
            entry = entries[index]
            self.assertEqual("needs_reverification", entry.classification, index)
            self.assertIsNone(entry.exception_id, index)
            self.assertEqual((), entry.evidence, index)
            self.assertEqual(NEEDS_RATIONALE, entry.rationale, index)
        for index in excluded_indices:
            entry = entries[index]
            self.assertEqual("out_of_scope", entry.classification, index)
            self.assertIsNone(entry.exception_id, index)

    def test_epsimple_model_result_promotion_is_exact_and_bounded(self) -> None:
        entries = self.configuration.matrix.entries
        targets = {
            373: (
                "GreenRetrofitResult",
                "exception",
                "reviewed-native-adaptation-immutable-complete-result-tree-rather-than-model-result-wrapper-8b407386",
            ),
            374: (
                "GreenRetrofitResult.VALID_DIGITS",
                "equivalent",
                "direct-native-greenretrofitresult-valid-digits-ff1cddac",
            ),
            375: (
                "GreenRetrofitResult.__init__",
                "exception",
                "reviewed-native-adaptation-validated-factory-and-diagnostic-build-result-boundary-856dd66b",
            ),
            376: (
                "GreenRetrofitResult.area",
                "equivalent",
                "direct-native-greenretrofitresult-area-37a89b1c",
            ),
            377: (
                "GreenRetrofitResult.calc_domestic_hotwater_site_energy",
                "exception",
                "reviewed-native-adaptation-typed-server-filtering-first-id-wins-and-structured-diagnostics-4e80e0ef",
            ),
            378: (
                "GreenRetrofitResult.get_dhw_servers",
                "exception",
                "reviewed-native-adaptation-typed-boiler-district-filtering-rather-than-arbitrary-hotwater-object-a63f6fa2",
            ),
            379: (
                "GreenRetrofitResult.get_domestic_hotwater_energy",
                "equivalent",
                "direct-native-greenretrofitresult-get-domestic-hotwater-energy-b7774317",
            ),
            380: (
                "GreenRetrofitResult.summarize",
                "equivalent",
                "direct-native-greenretrofitresult-summarize-93d2bbd8",
            ),
            381: (
                "GreenRetrofitResult.to_co2",
                "equivalent",
                "direct-native-greenretrofitresult-to-co2-72b97e85",
            ),
            382: (
                "GreenRetrofitResult.to_cost",
                "equivalent",
                "direct-native-greenretrofitresult-to-cost-7d1d1cd9",
            ),
            383: (
                "GreenRetrofitResult.to_dict",
                "equivalent",
                "direct-native-greenretrofitresult-to-dict-010fb599",
            ),
            384: (
                "GreenRetrofitResult.to_site_uses",
                "equivalent",
                "direct-native-greenretrofitresult-to-site-uses-48114e14",
            ),
            385: (
                "GreenRetrofitResult.to_source_uses",
                "equivalent",
                "direct-native-greenretrofitresult-to-source-uses-842eb853",
            ),
            386: (
                "GreenRetrofitResult.write",
                "exception",
                "reviewed-native-adaptation-deterministic-grr-writer-with-terminal-newline-67ef521c",
            ),
        }
        self.assertEqual(14, len(targets))
        self.assertEqual(
            {"equivalent": 9, "exception": 5},
            {
                classification: sum(
                    target_classification == classification
                    for _, target_classification, _ in targets.values()
                )
                for classification in ("equivalent", "exception")
            },
        )

        for index, (symbol, classification, adaptation_family) in targets.items():
            entry = entries[index]
            inventory_symbol = self.configuration.inventory.symbols[index]
            self.assertEqual(
                ("src/epsimple/core/model.py", symbol),
                inventory_symbol.key,
                symbol,
            )
            self.assertEqual(classification, entry.classification, symbol)
            exception_id = (
                adaptation_family if classification == "exception" else None
            )
            self.assertEqual(exception_id, entry.exception_id, symbol)
            assertion_id = (
                f"epsimple-model-result-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, symbol)
            for exact_binding in (
                assertion_id,
                adaptation_family,
                "commit 61bb21b",
                "sha256:55d19ad2df41112fa0bb8bb1585f9e9822b68cfa4332c52b90e2aacbfd57c520",
                "sha256:d3ed6f576696d32cdf4c5f59f0a6d5c805f3d4541bdd375720ec80feb280f7e4",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)

        excluded = {
            343: "GreenRetrofitModel.__repr__",
            344: "GreenRetrofitModel.__str__",
            358: "GreenRetrofitModel.from_excel",
        }
        model_core_indices = (
            set(range(337, 373)) - set(excluded)
        ) | {387, 388}
        self.assertEqual(35, len(model_core_indices))
        self.assertEqual(
            {"equivalent": 11, "exception": 24},
            {
                classification: sum(
                    entries[index].classification == classification
                    for index in model_core_indices
                )
                for classification in ("equivalent", "exception")
            },
        )
        self.assertEqual(
            set(range(337, 389)),
            set(targets) | model_core_indices | set(excluded),
        )
        for index, symbol in excluded.items():
            entry = entries[index]
            self.assertEqual(("src/epsimple/core/model.py", symbol), entry.key, symbol)
            self.assertEqual("out_of_scope", entry.classification, symbol)
            self.assertIsNone(entry.exception_id, symbol)

    def test_epsimple_shape_core_promotion_is_exact_and_bounded(self) -> None:
        entries = self.configuration.matrix.entries
        target_symbols = {
            405: "BlindType",
            406: "BlindType.SHADE",
            407: "BlindType.VENETIAN",
            408: "BlindType.__str__",
            409: "Door",
            410: "Door.construction",
            411: "Door.from_json",
            412: "Door.to_dragon",
            413: "Fenestration",
            414: "Fenestration.ID",
            415: "Fenestration.__deepcopy__",
            417: "Fenestration.__init__",
            418: "Fenestration.construction",
            419: "Fenestration.from_json",
            420: "Fenestration.to_dragon",
            421: "GlassDoor",
            422: "Surface",
            423: "Surface.ID",
            424: "Surface.__deepcopy__",
            426: "Surface.__init__",
            429: "Surface.adjacent_zone",
            430: "Surface.area",
            431: "Surface.azimuth",
            432: "Surface.boundary",
            433: "Surface.construction",
            434: "Surface.flip",
            435: "Surface.from_json",
            436: "Surface.get_unique_fenestration_constructions",
            437: "Surface.num_doors",
            438: "Surface.num_windows",
            439: "Surface.reflectance",
            440: "Surface.to_dragon",
            441: "Surface.type",
            442: "Window",
            443: "Window.__init__",
            444: "Window.blind",
            445: "Window.construction",
            446: "Window.from_json",
            447: "Window.to_dragon",
            448: "Zone",
            449: "Zone.ID",
            451: "Zone.__init__",
            452: "Zone.area",
            453: "Zone.cooling_supply_systems",
            454: "Zone.from_json",
            455: "Zone.get_unique_fenestration_constructions",
            456: "Zone.get_unique_materials",
            457: "Zone.get_unique_surface_constructions",
            458: "Zone.heating_supply_systems",
            459: "Zone.height",
            460: "Zone.infiltration",
            461: "Zone.supply_systems",
            462: "Zone.to_dragon",
        }
        exception_ids = {
            408: "grm-vocabulary-rather-than-native-enum-tostring-f40e4929",
            409: "unified-immutable-fenestration-with-door-discriminator-8c468e24",
            413: "sealed-discriminated-native-fenestration-rather-than-abc-43d44ea1",
            415: "immutable-native-fenestration-explicit-reconstruction-a0dbc411",
            417: "deterministic-native-id-and-discriminated-constructor-1b22b2f1",
            418: "immutable-resolved-native-construction-reference-0b0cbf2f",
            420: "aggregate-native-converter-rather-than-abstract-instance-method-ede823e2",
            421: "unified-immutable-fenestration-with-glassdoor-discriminator-1981a404",
            424: "immutable-native-surface-explicit-reconstruction-0d951ae6",
            426: "deterministic-native-id-and-immutable-constructor-bd742aa0",
            429: "native-adjacent-zone-id-rather-than-object-reference-cf314ac6",
            434: "pure-deterministic-native-flip-without-inplace-mutation-8e01b8fa",
            436: "model-catalog-native-aggregation-72d9807c",
            442: "unified-immutable-fenestration-with-window-discriminator-00f305af",
            443: "unified-native-fenestration-constructor-e8fad25a",
            451: "deterministic-native-id-and-immutable-zone-constructor-a5f3cee1",
            455: "model-level-native-fenestration-catalog-d8077110",
            456: "model-level-native-material-catalog-ecb20cb3",
            457: "model-level-native-surface-catalog-486d73d3",
            462: "native-greenretrofit-converter-implements-upstream-missing-operation-da336048",
        }
        target_indices = (
            405,
            406,
            407,
            408,
            409,
            410,
            411,
            412,
            413,
            414,
            415,
            417,
            418,
            419,
            420,
            421,
            422,
            423,
            424,
            426,
            429,
            430,
            431,
            432,
            433,
            434,
            435,
            436,
            437,
            438,
            439,
            440,
            441,
            442,
            443,
            444,
            445,
            446,
            447,
            448,
            449,
            451,
            452,
            453,
            454,
            455,
            456,
            457,
            458,
            459,
            460,
            461,
            462,
        )
        self.assertEqual(target_indices, tuple(target_symbols))
        self.assertEqual(53, len(target_symbols))
        self.assertEqual(20, len(exception_ids))
        self.assertEqual(33, len(target_symbols) - len(exception_ids))

        for index, symbol in target_symbols.items():
            entry = entries[index]
            inventory_symbol = self.configuration.inventory.symbols[index]
            self.assertEqual(
                ("src/epsimple/core/shape.py", symbol),
                inventory_symbol.key,
                symbol,
            )
            classification = "exception" if index in exception_ids else "equivalent"
            exception_id = exception_ids.get(index)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            assertion_id = (
                f"epsimple-shape-core-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, symbol)
            self.assertIn(assertion_id, entry.rationale, symbol)
            self.assertIn("commit a198a7c", entry.rationale, symbol)

        excluded = {
            416: "Fenestration.__hash__",
            425: "Surface.__hash__",
            427: "Surface.__repr__",
            428: "Surface.__str__",
            450: "Zone.__hash__",
        }
        self.assertEqual(set(range(405, 463)), set(target_symbols) | set(excluded))
        for index, symbol in excluded.items():
            entry = entries[index]
            self.assertEqual(("src/epsimple/core/shape.py", symbol), entry.key, symbol)
            self.assertEqual("out_of_scope", entry.classification, symbol)
            self.assertIsNone(entry.exception_id, symbol)

        expected_adjacent = {
            400: ("KoreanUsageProfile.to_dragon", "equivalent"),
            401: ("KoreanUsageProfileExtended", "exception"),
            402: ("Profile", "exception"),
            403: ("Profile.get_DB", "exception"),
            404: ("read_csv_without_units", "exception"),
            463: ("BlindForNonOutdoorWindow", "out_of_scope"),
            464: ("BlindForNonOutdoorWindow.__init__", "out_of_scope"),
            465: ("BlindForNonOutdoorWindow.inspect", "out_of_scope"),
        }
        for index, (symbol, classification) in expected_adjacent.items():
            self.assertEqual(symbol, entries[index].symbol)
            self.assertEqual(classification, entries[index].classification, symbol)

    def test_epsimple_construction_core_promotion_is_exact_and_bounded(self) -> None:
        entries = self.configuration.matrix.entries
        target_symbols = {
            75: "FenestrationConstruction",
            76: "FenestrationConstruction.ID",
            79: "FenestrationConstruction.__init__",
            82: "FenestrationConstruction.from_json",
            83: "FenestrationConstruction.g",
            84: "FenestrationConstruction.get_DB",
            85: "FenestrationConstruction.is_transparent",
            86: "FenestrationConstruction.load_DB",
            87: "FenestrationConstruction.to_dict",
            88: "FenestrationConstruction.to_dragon",
            89: "FenestrationConstruction.u",
            90: "Material",
            91: "Material.ID",
            94: "Material.__init__",
            97: "Material.conductivity",
            98: "Material.density",
            99: "Material.from_json",
            100: "Material.get_DB",
            101: "Material.load_DB",
            102: "Material.specific_heat",
            103: "Material.to_dict",
            104: "Material.to_dragon",
            105: "OpenConstruction",
            106: "OpenConstruction.ID",
            107: "OpenConstruction.to_dragon",
            108: "SpecialConstruction",
            109: "SpecialConstruction.__new__",
            110: "SpecialConstruction.get_unique_materials",
            111: "SpecialConstruction.reversed",
            112: "SurfaceConstruction",
            113: "SurfaceConstruction.ID",
            114: "SurfaceConstruction.U_internal",
            117: "SurfaceConstruction.__init__",
            120: "SurfaceConstruction.create_simply",
            121: "SurfaceConstruction.depth",
            122: "SurfaceConstruction.from_json",
            123: "SurfaceConstruction.get_DB",
            124: "SurfaceConstruction.get_U",
            125: "SurfaceConstruction.get_regulated_construction",
            126: "SurfaceConstruction.get_unique_materials",
            127: "SurfaceConstruction.heat_capacity",
            128: "SurfaceConstruction.load_DB",
            129: "SurfaceConstruction.reversed",
            130: "SurfaceConstruction.to_dict",
            131: "SurfaceConstruction.to_dragon",
            132: "UnknownConstruction",
            133: "UnknownConstruction.ID",
            134: "UnknownConstruction.to_dragon",
        }
        equivalent_indices = {85, 107, 114, 121, 124, 126, 127}
        exception_ids = {
            75: "reviewed-native-adaptation-fenestrationconstruction-f86ec154",
            76: "reviewed-native-adaptation-fenestrationconstruction-id-246156d9",
            79: "reviewed-native-adaptation-fenestrationconstruction-init-92969825",
            82: "reviewed-native-adaptation-fenestrationconstruction-from-json-e3c4284e",
            83: "reviewed-native-adaptation-fenestrationconstruction-g-5025a060",
            84: "reviewed-native-adaptation-fenestrationconstruction-get-db-87537fa6",
            86: "reviewed-native-adaptation-fenestrationconstruction-load-db-538b0465",
            87: "reviewed-native-adaptation-fenestrationconstruction-to-dict-8aaf803c",
            88: "reviewed-native-adaptation-fenestrationconstruction-to-dragon-f430c29b",
            89: "reviewed-native-adaptation-fenestrationconstruction-u-72e986b6",
            90: "reviewed-native-adaptation-material-590c4070",
            91: "reviewed-native-adaptation-material-id-246156d9",
            94: "reviewed-native-adaptation-material-init-d909f493",
            97: "reviewed-native-adaptation-material-conductivity-b733b56b",
            98: "reviewed-native-adaptation-material-density-23136324",
            99: "reviewed-native-adaptation-material-from-json-f2772e15",
            100: "reviewed-native-adaptation-material-get-db-c3fc9501",
            101: "reviewed-native-adaptation-material-load-db-f6b33018",
            102: "reviewed-native-adaptation-material-specific-heat-abf4a2ea",
            103: "reviewed-native-adaptation-material-to-dict-7326bc5b",
            104: "reviewed-native-adaptation-material-to-dragon-352f66b1",
            105: "reviewed-native-adaptation-openconstruction-3257fd04",
            106: "reviewed-native-adaptation-openconstruction-id-45236b5b",
            108: "reviewed-native-adaptation-specialconstruction-9f449287",
            109: "reviewed-native-adaptation-specialconstruction-new-758d9c0b",
            110: "reviewed-native-adaptation-specialconstruction-get-unique-materials-4f9ce2c0",
            111: "reviewed-native-adaptation-specialconstruction-reversed-119ed204",
            112: "reviewed-native-adaptation-surfaceconstruction-f3d6bd23",
            113: "reviewed-native-adaptation-surfaceconstruction-id-246156d9",
            117: "reviewed-native-adaptation-surfaceconstruction-init-6e437543",
            120: "reviewed-native-adaptation-surfaceconstruction-create-simply-23907b76",
            122: "reviewed-native-adaptation-surfaceconstruction-from-json-b1bb16e6",
            123: "reviewed-native-adaptation-surfaceconstruction-get-db-d21ed4db",
            125: "reviewed-native-adaptation-surfaceconstruction-get-regulated-construction-a806c4c3",
            128: "reviewed-native-adaptation-surfaceconstruction-load-db-fec259a4",
            129: "reviewed-native-adaptation-surfaceconstruction-reversed-d72c2143",
            130: "reviewed-native-adaptation-surfaceconstruction-to-dict-59426aa2",
            131: "reviewed-native-adaptation-surfaceconstruction-to-dragon-a204e680",
            132: "reviewed-native-adaptation-unknownconstruction-d803cd9d",
            133: "reviewed-native-adaptation-unknownconstruction-id-d6777d2d",
            134: "reviewed-native-adaptation-unknownconstruction-to-dragon-558da4a7",
        }
        self.assertEqual(48, len(target_symbols))
        self.assertEqual(7, len(equivalent_indices))
        self.assertEqual(41, len(exception_ids))
        self.assertEqual(set(target_symbols), equivalent_indices | set(exception_ids))

        for index, symbol in target_symbols.items():
            entry = entries[index]
            inventory_symbol = self.configuration.inventory.symbols[index]
            self.assertEqual(
                ("src/epsimple/core/construction.py", symbol),
                inventory_symbol.key,
                symbol,
            )
            exception_id = exception_ids.get(index)
            classification = "exception" if exception_id is not None else "equivalent"
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            assertion_id = (
                f"epsimple-construction-core-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, symbol)
            self.assertIn(assertion_id, entry.rationale, symbol)
            self.assertIn("commit 3053e74", entry.rationale, symbol)

        excluded = {
            77: "FenestrationConstruction.__eq__",
            78: "FenestrationConstruction.__hash__",
            80: "FenestrationConstruction.__repr__",
            81: "FenestrationConstruction.__str__",
            92: "Material.__eq__",
            93: "Material.__hash__",
            95: "Material.__repr__",
            96: "Material.__str__",
            115: "SurfaceConstruction.__eq__",
            116: "SurfaceConstruction.__hash__",
            118: "SurfaceConstruction.__repr__",
            119: "SurfaceConstruction.__str__",
        }
        self.assertEqual(set(range(75, 135)), set(target_symbols) | set(excluded))
        for index, symbol in excluded.items():
            entry = entries[index]
            self.assertEqual(
                ("src/epsimple/core/construction.py", symbol), entry.key, symbol
            )
            self.assertEqual("out_of_scope", entry.classification, symbol)
            self.assertIsNone(entry.exception_id, symbol)

        self.assertEqual("Unit.W_TO_KW", entries[74].symbol)
        self.assertEqual("equivalent", entries[74].classification)
        self.assertEqual("AbsorptionChiller", entries[135].symbol)
        self.assertEqual("exception", entries[135].classification)
        self.assertEqual(
            "reviewed-native-discriminated-source-aggregate-and-conversion-route-c44e12f9",
            entries[135].exception_id,
        )

    def test_energy_model_class_promotion_preserves_adjacent_model_scope(self) -> None:
        entries = self.configuration.matrix.entries
        self.assertEqual(
            (
                (
                    "src/idragon/dragon/hvac.py",
                    "ZoneTerminalUnitAppender.run",
                    "needs_reverification",
                    None,
                ),
                (
                    "src/idragon/dragon/model.py",
                    "EnergyModel",
                    "exception",
                    "sealed-read-only-native-energy-model-class-a7582a41",
                ),
                (
                    "src/idragon/dragon/model.py",
                    "EnergyModel.__init__",
                    "exception",
                    "immutable-validated-energy-model-construction",
                ),
            ),
            tuple(
                (
                    entry.path,
                    entry.symbol,
                    entry.classification,
                    entry.exception_id,
                )
                for entry in entries[814:817]
            ),
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#sealed-read-only-native-energy-model-class-a7582a41",
                "upstream/symbol-evidence.json#dragon-model-energy-model-class-a7582a41",
            ),
            entries[815].evidence,
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

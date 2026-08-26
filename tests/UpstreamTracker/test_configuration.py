from __future__ import annotations

from pathlib import Path
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace, write_configuration

from goniegonie_upstream_tracker.config import load_configuration
from goniegonie_upstream_tracker.compatibility import load_compatibility_configuration
from goniegonie_upstream_tracker.errors import ConfigurationError
from goniegonie_upstream_tracker.yaml_subset import parse_yaml_subset


class ConfigurationTests(unittest.TestCase):
    def test_repository_manifests_validate_as_one_configuration(self) -> None:
        configuration = load_configuration(
            REPOSITORY_ROOT / "upstream" / "upstream.lock.json",
            REPOSITORY_ROOT / "upstream" / "port-map.yml",
            REPOSITORY_ROOT / "upstream" / "compatibility-exceptions.yml",
        )

        self.assertEqual("goniegonie.upstream-lock.v1", configuration.lock.schema)
        self.assertGreater(len(configuration.mappings), 0)
        self.assertTrue(
            all(
                mapping.dotnet_project.startswith("GonieGonie.")
                for mapping in configuration.mappings
            )
        )
        self.assertEqual(220, len(configuration.exceptions))
        compatibility = load_compatibility_configuration(
            configuration,
            REPOSITORY_ROOT / "upstream" / "compatibility-scope.json",
            REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json",
            REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json",
            repository_root=REPOSITORY_ROOT,
        )
        self.assertEqual(24, len(compatibility.inventory.files))
        self.assertEqual(1242, len(compatibility.inventory.symbols))
        self.assertEqual(
            len(compatibility.inventory.symbols),
            len(compatibility.matrix.entries),
        )
        self.assertEqual(616, len(compatibility.needs_reverification))
        self.assertEqual(
            160,
            sum(
                entry.classification == "equivalent"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertEqual(
            214,
            sum(
                entry.classification == "exception"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertIsNotNone(compatibility.symbol_evidence)
        symbol_evidence = compatibility.symbol_evidence
        assert symbol_evidence is not None
        self.assertEqual(374, len(symbol_evidence.entries))
        self.assertEqual(374, len(symbol_evidence.receipts))
        self.assertEqual(
            "sha256:7abaac72229370cca3b9b8576c4c89164ff57bab0189405760671aa4f825ed68",
            symbol_evidence.content_sha256,
        )
        self.assertEqual(
            252,
            sum(
                entry.classification == "out_of_scope"
                for entry in compatibility.matrix.entries
            ),
        )

        by_key = compatibility.matrix.entries_by_key
        numeric_indices = (
            *range(28, 31),
            *range(40, 58),
            *range(67, 75),
        )
        numeric_exception_ids = {
            28: "native-simpledragon-convection-constant-container",
            40: "native-simpledragon-site-to-carbon-dispatch",
            46: "native-simpledragon-site-to-cost-dispatch",
            52: "native-simpledragon-site-to-source-dispatch",
            67: "native-simpledragon-unit-conversion-constants",
        }
        numeric_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "ConstantsNumericOracleParityTests.cs"
        )
        numeric_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.ConstantsNumericOracleParityTests."
            "MatchesPinnedPythonConstantsNumeric"
        )
        numeric_test_hash = (
            "sha256:29ad9aa6d5cdffd240ec7727ff253812537f5aee5bfee4160bb20eb1ba36603a"
        )
        numeric_implementation_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Constants/"
            "SimpleDragonConstants.cs"
        )
        numeric_implementation_hash = (
            "sha256:dd6cbe124a3b07b6cee8eb3698077db95912062281a3fac5d9d53ec74da4e2a7"
        )
        numeric_exceptions = {
            item.identifier: item for item in configuration.exceptions
        }
        for index in numeric_indices:
            inventory_symbol = compatibility.inventory.symbols[index]
            key = inventory_symbol.key
            self.assertEqual("src/epsimple/constants.py", key[0], key)
            entry = compatibility.matrix.entries[index]
            self.assertEqual(entry, by_key[key], key)
            exception_id = numeric_exception_ids.get(index)
            self.assertEqual(
                "exception" if exception_id is not None else "equivalent",
                entry.classification,
                key,
            )
            self.assertEqual(exception_id, entry.exception_id, key)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                numeric_implementation_path,
                evidence_entry.implementation_path,
                key,
            )
            self.assertEqual(
                numeric_implementation_hash,
                evidence_entry.implementation_source_sha256,
                key,
            )
            self.assertTrue(
                evidence_entry.implementation_symbol.startswith(
                    "GonieGonie.SimpleDragon."
                ),
                key,
            )
            self.assertEqual(1, len(evidence_entry.receipts), key)
            receipt = evidence_entry.receipts[0]
            self.assertTrue(
                receipt.identifier.startswith("epsimple-constants-numeric-"),
                key,
            )
            self.assertEqual(entry.rationale, receipt.assertion, key)
            self.assertEqual(1, receipt.assertion.count("sha256:"), key)
            self.assertEqual(numeric_test_path, receipt.test_path, key)
            self.assertEqual(numeric_test_symbol, receipt.test_symbol, key)
            self.assertEqual(numeric_test_hash, receipt.test_source_sha256, key)
            self.assertEqual("cross_language", receipt.verification_kind, key)
            self.assertEqual("passed", receipt.outcome, key)
            self.assertFalse(receipt.skipped, key)
            self.assertFalse(receipt.structural_only, key)
            self.assertFalse(receipt.claims_active_load, key)
            self.assertEqual("not_applicable", receipt.exercised_load, key)

            expected_evidence = [
                f"upstream/symbol-evidence.json#{receipt.identifier}"
            ]
            if exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
                exception = numeric_exceptions[exception_id]
                self.assertEqual(
                    key,
                    (exception.upstream_path, exception.upstream_symbol),
                    key,
                )
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    key,
                )
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, key)
        self.assertEqual(29, len(numeric_indices))

        expected_construction_family = {
            "AirBoundary.to_idf_object": (
                592,
                "model-context-air-boundary-idf-emission",
                "dragon-construction-air-boundary-to-idf-object-639a205f",
            ),
            "Construction.to_idf_object": (
                601,
                "model-context-construction-idf-emission",
                "dragon-construction-construction-to-idf-object-71a76f27",
            ),
            "Glazing.to_idf_object": (
                608,
                "model-context-glazing-idf-emission",
                "dragon-construction-glazing-to-idf-object-3350beaf",
            ),
            "Layer.to_idf_object": (
                617,
                "model-context-layer-idf-emission",
                "dragon-construction-layer-to-idf-object-66e6d458",
            ),
            "NoMassConstruction.to_idf_object": (
                640,
                "model-context-no-mass-construction-idf-emission",
                "dragon-construction-no-mass-construction-to-idf-object-2bc3fe98",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in (
            expected_construction_family.items()
        ):
            key = ("src/idragon/dragon/construction.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        expected_air_boundary_core = {
            "AirBoundary": (
                588,
                "permissive-mutable-python-air-boundary-state-fd8f9bb9",
                "dragon-construction-air-boundary-core-588-fd8f9bb9",
                "sha256:e94adada7522d56edce498e3d9caf6fe390d5926cf42038c689b15b1df8a1be3",
                "sha256:83167c1eb59ce60b50cd6fbb2e7eebbe87e1452243d6b5ff50287691c3e3f4b7",
                "GonieGonie.InvisibleDragon.Construction.AirBoundary",
            ),
            "AirBoundary.__init__": (
                589,
                "unchecked-python-air-boundary-construction-a69bf707",
                "dragon-construction-air-boundary-core-589-a69bf707",
                "sha256:53e6bdb13392529e182b4b16a24fc72d37116abf93472e49e6648d5e0cb8458a",
                "sha256:a6bc52d12c81f6a4463421cb5c77decd1ba956e797afab0e7c7e19425bf6264f",
                "GonieGonie.InvisibleDragon.Construction.AirBoundary.AirBoundary",
            ),
        }
        air_boundary_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Construction/"
            "AirBoundaryCoreOracleParityTests.cs"
        )
        air_boundary_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Construction."
            "AirBoundaryCoreOracleParityTests."
            "MatchesPinnedAirBoundaryCoreThroughTypedNativeRoutes"
        )
        air_boundary_test_hash = (
            "sha256:64adf39ee35dc626606071fcf8efd9a46a6e73f21536b2b355834a0611389766"
        )
        air_boundary_implementation_path = (
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Construction/"
            "SimpleConstructions.cs"
        )
        air_boundary_implementation_hash = (
            "sha256:4141d1125d33c40092caaf8b7e472bb50477a8c05b56b24ddf330ca72be22292"
        )
        air_boundary_exceptions = {
            item.identifier: item for item in configuration.exceptions
        }
        air_boundary_assertions = {}
        for symbol, (
            index,
            exception_id,
            assertion_id,
            direct_receipt_hash,
            collector_output_hash,
            implementation_symbol,
        ) in expected_air_boundary_core.items():
            key = ("src/idragon/dragon/construction.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
            exception = air_boundary_exceptions[exception_id]
            self.assertEqual(key, (exception.upstream_path, exception.upstream_symbol))
            self.assertEqual(
                compatibility.inventory.symbols[index].symbol_hash,
                exception.upstream_symbol_hash,
                symbol,
            )
            self.assertIn(("engineering_result", entry.rationale), exception.effects)
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                air_boundary_implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                air_boundary_implementation_hash,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash,
                receipt.expected_output_sha256,
                symbol,
            )
            self.assertEqual(air_boundary_test_path, receipt.test_path, symbol)
            self.assertEqual(air_boundary_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(air_boundary_test_hash, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            air_boundary_assertions[symbol] = receipt.assertion
        self.assertEqual(2, len(expected_air_boundary_core))
        self.assertFalse(set(expected_air_boundary_core) & set(expected_construction_family))
        expected_adjacent_receipts = {
            592: "dragon-construction-air-boundary-to-idf-object-639a205f",
            595: "idragon-construction-equality-native-null-adaptation",
            596: "idragon-construction-hash-native-runtime-adaptation",
            601: "dragon-construction-construction-to-idf-object-71a76f27",
            608: "dragon-construction-glazing-to-idf-object-3350beaf",
            611: "idragon-layer-equality-native-null-adaptation",
            612: "idragon-layer-hash-native-runtime-adaptation",
            617: "dragon-construction-layer-to-idf-object-66e6d458",
            619: "idragon-material-equality-native-null-adaptation",
            640: "dragon-construction-no-mass-construction-to-idf-object-2bc3fe98",
        }
        for index in range(590, 641):
            adjacent_key = compatibility.inventory.symbols[index].key
            if index in expected_adjacent_receipts:
                adjacent_evidence = symbol_evidence.entries_by_key[adjacent_key]
                self.assertEqual(
                    (expected_adjacent_receipts[index],),
                    tuple(item.identifier for item in adjacent_evidence.receipts),
                    adjacent_key,
                )
            else:
                self.assertNotIn(adjacent_key, symbol_evidence.entries_by_key, adjacent_key)
        self.assertIn(
            "record equality, hashing, string representation, copy or deconstruction",
            air_boundary_assertions["AirBoundary"],
        )
        self.assertIn(
            "decimal, fraction, complex or huge-integer ACH",
            air_boundary_assertions["AirBoundary.__init__"],
        )
        self.assertIn(
            "IDF emission and parent integration are not claimed",
            air_boundary_assertions["AirBoundary.__init__"],
        )
        expected_zone_idf = {
            "Zone.to_idf_hvac_default_object": (
                1092,
                "model-context-zone-hvac-default-idf-emission",
                "dragon-shape-zone-to-idf-hvac-default-object-ff678ec2",
            ),
            "Zone.to_idf_load_object": (
                1093,
                "model-context-zone-load-idf-emission",
                "dragon-shape-zone-to-idf-load-object-d19165f0",
            ),
            "Zone.to_idf_object": (
                1094,
                "model-context-zone-idf-emission",
                "dragon-shape-zone-to-idf-object-479f4d74",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in expected_zone_idf.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        expected_surface_idf = {
            "Surface.to_idf_object": (
                1045,
                "legacy-rectangular-surface-idf-emission",
                "dragon-shape-surface-to-idf-object-a03c4d52",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in (
            expected_surface_idf.items()
        ):
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        expected_opening_adjacency = {
            "Blind": (
                1025,
                "permissive-python-blind-state-75f7c91c",
                "dragon-shape-opening-adjacency-core-1025-75f7c91c",
                "sha256:64dcaf9393ca788505441f2b62efe2153d4afd78e3072f53d302282e9a8a31bd",
                "permissive-python-blind-state",
            ),
            "Blind.__init__": (
                1026,
                "permissive-python-blind-state-574e9b5a",
                "dragon-shape-opening-adjacency-core-1026-574e9b5a",
                "sha256:d24f4d29a44afa671c4d2d487eaf6a2e837fa2edb140385e9d296d89a0b294b9",
                "permissive-python-blind-state",
            ),
            "Door": (
                1028,
                "permissive-python-door-state-717d717a",
                "dragon-shape-opening-adjacency-core-1028-717d717a",
                "sha256:73603b3c0d9b6e08472014c76fc2dcd4e4fca15b7b8cdf6ec2919f1bc34b4c2d",
                "permissive-python-door-state",
            ),
            "Door.__init__": (
                1029,
                "permissive-python-door-state-efd71c81",
                "dragon-shape-opening-adjacency-core-1029-efd71c81",
                "sha256:b5dbb581ff8eb8ecc7cfa2cfffe9e39cf63f00866df25a3e0a17c84240e33fce",
                "permissive-python-door-state",
            ),
            "Shade": (
                1030,
                "permissive-python-shade-state-9404da04",
                "dragon-shape-opening-adjacency-core-1030-9404da04",
                "sha256:3e57b061272e3ee577e31c07e45e09bb19a09457ab3f48558c5347a700fa9ac3",
                "permissive-python-shade-state",
            ),
            "Shade.__init__": (
                1031,
                "permissive-python-shade-state-f76ed298",
                "dragon-shape-opening-adjacency-core-1031-f76ed298",
                "sha256:f228d23cf189b9c5f778e2760952e21b782bc686c994281f400b6399b386269e",
                "permissive-python-shade-state",
            ),
            "Shading": (
                1033,
                "directly-instantiable-empty-python-shading-4dba9833",
                "dragon-shape-opening-adjacency-core-1033-4dba9833",
                "sha256:4bd856667c24d93adfa228e0724cf9f07f14e90140544db02e4b0b31b240a2af",
                "directly-instantiable-empty-python-shading",
            ),
            "Surface.__init__": (
                1035,
                "aliased-python-surface-opening-inputs-ef349ef4",
                "dragon-shape-opening-adjacency-core-1035-ef349ef4",
                "sha256:0b11e25629db08b0498b96a5544d7a011819e1a01555a89ef369819c2920697b",
                "aliased-python-surface-opening-inputs",
            ),
            "Surface.blinded_window": (
                1039,
                "fresh-python-blinded-window-projection-f520fbfe",
                "dragon-shape-opening-adjacency-core-1039-f520fbfe",
                "sha256:bb2cff5eb33003d79f28bf47bd7dbe705fd2225741797c56f33954d6304b69a0",
                "fresh-python-blinded-window-projection",
            ),
            "Surface.boundary": (
                1040,
                "mutable-reciprocal-python-surface-adjacency-7753d967",
                "dragon-shape-opening-adjacency-core-1040-7753d967",
                "sha256:86ecb839d0652cbcaea8c7310566de4d4f1795b1a20ca0adf95dae0f2cc41253",
                "mutable-reciprocal-python-surface-adjacency",
            ),
            "Surface.get_subsurface": (
                1042,
                "legacy-linear-scale-subsurface-projection-7e43708d",
                "dragon-shape-opening-adjacency-core-1042-7e43708d",
                "sha256:dfc4a7c265d365fd34017f89278b9e47d95d68da99dc7d4c994e4fa4c4dfbaec",
                "legacy-linear-scale-subsurface-projection",
            ),
            "SurfaceBoundaryCondition": (
                1048,
                "lowercase-python-surface-boundary-enum-73a8b86f",
                "dragon-shape-opening-adjacency-core-1048-73a8b86f",
                "sha256:c45c68a457705c686699c6dbdfdc235ee5ad0cb4638a8456bec5237e6f4ee8f8",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ADIABATIC": (
                1049,
                "lowercase-python-surface-boundary-enum-1d0e3d46",
                "dragon-shape-opening-adjacency-core-1049-1d0e3d46",
                "sha256:08e570845c119718b6fa052862fa05f0792391a0e76797671c8c314c0d397c56",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.GROUND": (
                1050,
                "lowercase-python-surface-boundary-enum-0992cbf6",
                "dragon-shape-opening-adjacency-core-1050-0992cbf6",
                "sha256:b1b7efac6274046b0596a8c2f4c3a6359f8bf2f2cb0e1f32c722af7f4499260b",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.OUTDOOR": (
                1051,
                "lowercase-python-surface-boundary-enum-8560160a",
                "dragon-shape-opening-adjacency-core-1051-8560160a",
                "sha256:d107db8406311b734dda94e2a0b4f9024f3cb294eefff3d951f62589dc26a73c",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ZONE": (
                1052,
                "lowercase-python-surface-boundary-enum-3ec06789",
                "dragon-shape-opening-adjacency-core-1052-3ec06789",
                "sha256:e584aa44381399a4daaadb5d004eff0a3291c94e6bfa438d7d5b1880db0b7d16",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.__str__": (
                1053,
                "lowercase-python-surface-boundary-enum-f40e4929",
                "dragon-shape-opening-adjacency-core-1053-f40e4929",
                "sha256:69b03cccaa9c2bfd9e0b35d8658ba772e0875f5b8716183f3827129dd9607943",
                "lowercase-python-surface-boundary-enum",
            ),
            "Window": (
                1081,
                "permissive-python-window-state-af640a9a",
                "dragon-shape-opening-adjacency-core-1081-af640a9a",
                "sha256:6f704712439b980642cf4a9a44ea28a243300af2f466140457ad71286897bd7d",
                "permissive-python-window-state",
            ),
            "Window.__init__": (
                1082,
                "permissive-python-window-state-3ce851bd",
                "dragon-shape-opening-adjacency-core-1082-3ce851bd",
                "sha256:d7fc537b98a3772775b7cb997bc857c63faf6c006d30be3d40318e3aa68ad03b",
                "permissive-python-window-state",
            ),
        }
        native_source_hashes = {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Shading.cs": "sha256:99b426d76894461ca1f29e41dfba08204ee43a72f6133f3588eedd7e79b3affd",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Openings.cs": "sha256:4da15fd6ee228d471bc1a249abf23f7dbff5687ff0f1dabb9dc820b512aee494",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Surface.cs": "sha256:545dc79dd89e84acf6d714e79da7b2cda059dfcaa3b4f74d291ad572ebd51264",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/SurfaceBoundary.cs": "sha256:c0ba4cf5a93eb2678aee2c698320121f5bfbd68f7febb3dc901fe700da1499d9",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/SurfaceAdjacency.cs": "sha256:83d67c465446be31133fcd17d2e3cbbab9b6b320a28a3f2608ad55c99450fb59",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs": "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905",
        }
        expected_native_symbols = {
            "Blind": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Blind"),
            "Blind.__init__": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Blind.Blind"),
            "Door": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Door"),
            "Door.__init__": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Door.Door"),
            "Shade": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Shade"),
            "Shade.__init__": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.Shade.Shade"),
            "Shading": ("Shape/Shading.cs", "GonieGonie.InvisibleDragon.Shape.IShadingDevice"),
            "Surface.__init__": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Surface"),
            "Surface.blinded_window": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Windows"),
            "Surface.boundary": ("Shape/SurfaceAdjacency.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceAdjacency.Match"),
            "Surface.get_subsurface": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.CreateCenteredSubsurface"),
            "SurfaceBoundaryCondition": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition"),
            "SurfaceBoundaryCondition.ADIABATIC": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Adiabatic"),
            "SurfaceBoundaryCondition.GROUND": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Ground"),
            "SurfaceBoundaryCondition.OUTDOOR": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Outdoors"),
            "SurfaceBoundaryCondition.ZONE": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceBoundaryCondition.Zone"),
            "SurfaceBoundaryCondition.__str__": ("Model/EnergyModelIdfAssembler.cs", "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.BuildingSurface"),
            "Window": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Window"),
            "Window.__init__": ("Shape/Openings.cs", "GonieGonie.InvisibleDragon.Shape.Window.Window"),
        }
        test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/"
            "OpeningAdjacencyCoreOracleParityTests.cs"
        )
        test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Model."
            "OpeningAdjacencyCoreOracleParityTests."
            "MatchesPinnedOpeningAdjacencyCoreThroughBoundedNativeRoutes"
        )
        test_hash = (
            "sha256:4e381e78334e6d976a1a4e1d19feab502769210c9b61c980c373587f505690b0"
        )
        opening_families = set()
        for symbol, (
            index,
            exception_id,
            assertion_id,
            receipt_hash,
            adaptation_family,
        ) in expected_opening_adjacency.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertTrue(exception_id.startswith(adaptation_family + "-"), symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(1, len(evidence_entry.receipts))
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(receipt_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(test_path, receipt.test_path, symbol)
            self.assertEqual(test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(test_hash, receipt.test_source_sha256, symbol)
            self.assertIn(f"Adaptation family {adaptation_family}", receipt.assertion)
            suffix, native_symbol = expected_native_symbols[symbol]
            self.assertTrue(evidence_entry.implementation_path.endswith(suffix), symbol)
            self.assertEqual(native_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                native_source_hashes[evidence_entry.implementation_path],
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            opening_families.add(adaptation_family)
        self.assertEqual(19, len(expected_opening_adjacency))
        self.assertEqual(10, len(opening_families))
        self.assertNotIn("Surface.to_idf_object", expected_opening_adjacency)
        expected_geometry_core = {
            "Surface": (
                1034,
                "exception",
                "permissive-python-surface-polygon-model-cb620c55",
                "dragon-shape-geometry-core-1034-cb620c55",
                "sha256:001cc076b64e25c31e353ffda4e59922fca2246b0631474d6513ff9daa11da3a",
                "permissive-python-surface-polygon-model",
            ),
            "Surface.area": (
                1038,
                "exception",
                "first-triple-oriented-python-surface-area-f254ab66",
                "dragon-shape-geometry-core-1038-f254ab66",
                "sha256:684f552a5733fce9458c077f4763d475ad571a29e543ad171e634013c1effdc1",
                "first-triple-oriented-python-surface-area",
            ),
            "Surface.center": (
                1041,
                "exception",
                "vertex-mean-python-surface-center-f0c05c2b",
                "dragon-shape-geometry-core-1041-f0c05c2b",
                "sha256:c76c774f5ac7f38eb3a1610592a63dc9d4e2e3a3a8ee1676f56954cfbfda45a0",
                "vertex-mean-python-surface-center",
            ),
            "Surface.height": (
                1043,
                "exception",
                "z-span-python-surface-height-d479fe2f",
                "dragon-shape-geometry-core-1043-d479fe2f",
                "sha256:936825eba1643e89a2178e394dd938fafa96b38454f05a0dc79a10dc8398ca9f",
                "z-span-python-surface-height",
            ),
            "Surface.normal": (
                1044,
                "exception",
                "first-triple-python-surface-normal-3f089c8c",
                "dragon-shape-geometry-core-1044-3f089c8c",
                "sha256:f53cb86260ed57b04b628af86246332566d6e4ae2165cefcd7adfb73a8c1c4c3",
                "first-triple-python-surface-normal",
            ),
            "Surface.type": (
                1046,
                "exception",
                "mutable-string-coerced-python-surface-type-ae4bdcc7",
                "dragon-shape-geometry-core-1046-ae4bdcc7",
                "sha256:0e8ede1b3c0bbb43c3ea695ffc5c874a1e197e8b407d1aa3c7702f8915bcdc34",
                "mutable-string-coerced-python-surface-type",
            ),
            "Surface.vertex": (
                1047,
                "exception",
                "aliased-mutable-python-surface-vertices-7ed5c6b3",
                "dragon-shape-geometry-core-1047-7ed5c6b3",
                "sha256:5d9c644d686983198550afed9855ca8a21276e9235143b66ffd68ef6a8ba2744",
                "aliased-mutable-python-surface-vertices",
            ),
            "SurfaceType": (
                1054,
                "exception",
                "lowercase-python-surface-type-enum-61a37f9d",
                "dragon-shape-geometry-core-1054-61a37f9d",
                "sha256:95bbe8b412e9ec5edac4389e0602fc618162553eb84a7f794f229e26f6b65143",
                "lowercase-python-surface-type-enum",
            ),
            "SurfaceType.CEILING": (
                1055,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1055-9ece8323",
                "sha256:bc403ff8d739932e5be1b809dffb9fb808395d2fc048f127435d0d7854c90ee6",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.FLOOR": (
                1056,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1056-c8c4f240",
                "sha256:44c5e3114d165607332730dc4256d93a8b44de8c629c90b30d7f7593aa6c4325",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.WALL": (
                1057,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1057-ca6d5593",
                "sha256:b27671d54451df410e7465303096af339da1080779350bc8ea89fb314441bfa4",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.__str__": (
                1058,
                "exception",
                "lowercase-python-surface-type-enum-f40e4929",
                "dragon-shape-geometry-core-1058-f40e4929",
                "sha256:afb7c2734e360938563c7b6ae371eb32ef29649182fc5591b98987f330162929",
                "lowercase-python-surface-type-enum",
            ),
            "Vertex": (
                1059,
                "exception",
                "permissive-mutable-python-vertex-state-78650289",
                "dragon-shape-geometry-core-1059-78650289",
                "sha256:a8cdbcc351ae243dff644bb913ec3ed634e681565b678995a38641591e80803a",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.__add__": (
                1060,
                "exception",
                "untyped-python-vertex-algebra-a5c7ecea",
                "dragon-shape-geometry-core-1060-a5c7ecea",
                "sha256:26eb7fea9168cfe5acdd0eaec64450410c69878fe1a2298c0c8372f939a0a607",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__deepcopy__": (
                1061,
                "exception",
                "python-vertex-copy-iteration-zero-addition-2c79da1a",
                "dragon-shape-geometry-core-1061-2c79da1a",
                "sha256:2f2cb2440ab0ffe71ae49bb013f9fb2f371f231704b8f971a00c734e428528f6",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__init__": (
                1063,
                "exception",
                "permissive-mutable-python-vertex-state-be3c69c5",
                "dragon-shape-geometry-core-1063-be3c69c5",
                "sha256:9c19b6b133dffec3652af707482128b9776c16b0f71ba6dfa8f1dc21f0803c75",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.__iter__": (
                1064,
                "exception",
                "python-vertex-copy-iteration-zero-addition-e95d7ce5",
                "dragon-shape-geometry-core-1064-e95d7ce5",
                "sha256:924ef9477e390a62d81c5842dce9d0edb34dd72a194e1e0994804e39e6b19c4f",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__mul__": (
                1065,
                "exception",
                "untyped-python-vertex-algebra-323878e1",
                "dragon-shape-geometry-core-1065-323878e1",
                "sha256:d7d0cb22d793d35c187518b149887f515e3d182a9511b6a9b929071e0249e8a9",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__radd__": (
                1066,
                "exception",
                "python-vertex-copy-iteration-zero-addition-a473d0f3",
                "dragon-shape-geometry-core-1066-a473d0f3",
                "sha256:7ab4d1a06246f8c981604ea66c56794a28df7e901393637d9c30daf9c416a3ac",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__rmul__": (
                1068,
                "exception",
                "untyped-python-vertex-algebra-1dbe33d3",
                "dragon-shape-geometry-core-1068-1dbe33d3",
                "sha256:341eac15a4f31865fdf1366fd5279f5c5cdf3cf52459789a91837abbe60c34f8",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__sub__": (
                1070,
                "exception",
                "untyped-python-vertex-algebra-4ee38e65",
                "dragon-shape-geometry-core-1070-4ee38e65",
                "sha256:ac54feab78509bac23da06bb27a7cbd76267dc4b548bcd6e740453eb17e38708",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__truediv__": (
                1071,
                "exception",
                "untyped-python-vertex-algebra-94f397b8",
                "dragon-shape-geometry-core-1071-94f397b8",
                "sha256:76d545bff2e10e5d891744aaa88451d3ddbfe4dce12c6504c28ee99fbaf734d7",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.are_coplanar": (
                1072,
                "exception",
                "legacy-first-triple-angular-coplanarity-905ebbf2",
                "dragon-shape-geometry-core-1072-905ebbf2",
                "sha256:f40ec2da2cf4de165fd9c5aeca368d5107eb595b8ea609d69a05ac264587a1d7",
                "legacy-first-triple-angular-coplanarity",
            ),
            "Vertex.cross": (
                1073,
                "exception",
                "untyped-python-vertex-metrics-6bc5db49",
                "dragon-shape-geometry-core-1073-6bc5db49",
                "sha256:4422f19acb2d8d08fa0df5d803a0d2c107c499b8030ad2a49c7887b39795354e",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.distance": (
                1074,
                "exception",
                "untyped-python-vertex-metrics-88c4cb9f",
                "dragon-shape-geometry-core-1074-88c4cb9f",
                "sha256:47dd5385e06816a19e6a3c2ad2a1bef9b47abf0e5da7e263f8a2d67e4d521d6b",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.dot": (
                1075,
                "exception",
                "untyped-python-vertex-metrics-1aaf5930",
                "dragon-shape-geometry-core-1075-1aaf5930",
                "sha256:ca2205f398613ceef6bef35c2e1f9df5676cff9bcf968be9902e0189f4e9c9cf",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.norm": (
                1076,
                "exception",
                "untyped-python-vertex-metrics-e41eae31",
                "dragon-shape-geometry-core-1076-e41eae31",
                "sha256:d792c5d6e0e0d9c943d2fb6a2054fcf7bba2feaf351a80d9cd4b1a4bf32d038a",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.unit": (
                1077,
                "exception",
                "zero-preserving-python-vertex-unit-4267bc06",
                "dragon-shape-geometry-core-1077-4267bc06",
                "sha256:94485adc8570643a7ece3586d3cb1b77f9b1976067b1261a5ff3ff8e9a4d9063",
                "zero-preserving-python-vertex-unit",
            ),
            "Vertex.x": (
                1078,
                "exception",
                "permissive-mutable-python-vertex-state-d859bad0",
                "dragon-shape-geometry-core-1078-d859bad0",
                "sha256:d954a38434d10eb4902650f925247ebaabeb0df0cdeaede281010c5b436ed970",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.y": (
                1079,
                "exception",
                "permissive-mutable-python-vertex-state-ff0bcc12",
                "dragon-shape-geometry-core-1079-ff0bcc12",
                "sha256:0842a605a3acf284cacb4615cb15f00d8298ac13daf60b386080e2c04d6ac759",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.z": (
                1080,
                "exception",
                "permissive-mutable-python-vertex-state-64899aff",
                "dragon-shape-geometry-core-1080-64899aff",
                "sha256:8aa2468d2a77e4239ec962033c058a41f72c9cdbf16cbc5dbfdbf441aa00017d",
                "permissive-mutable-python-vertex-state",
            ),
        }
        geometry_native_source_hashes = {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/PlanarPolygon.cs": "sha256:73a1dd052fb12ed0802a6236d21484e2b680cbe3f0f4005ade6a61995111c653",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Surface.cs": "sha256:545dc79dd89e84acf6d714e79da7b2cda059dfcaa3b4f74d291ad572ebd51264",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/SurfaceBoundary.cs": "sha256:c0ba4cf5a93eb2678aee2c698320121f5bfbd68f7febb3dc901fe700da1499d9",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Vector3.cs": "sha256:02536827db9d1c6ff48a46678871e4d736d9536228f0de370a9fb2c5294b9ede",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Vertex.cs": "sha256:f37b229b45b23c23ddc54ed85aea1b93a201a74c30c7b29793f268e364435a67",
        }
        expected_geometry_native_symbols = {
            "Surface": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface"),
            "Surface.area": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.GrossArea"),
            "Surface.center": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Center"),
            "Surface.height": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Height"),
            "Surface.normal": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Normal"),
            "Surface.type": ("Shape/Surface.cs", "GonieGonie.InvisibleDragon.Shape.Surface.Type"),
            "Surface.vertex": ("Shape/PlanarPolygon.cs", "GonieGonie.InvisibleDragon.Shape.PlanarPolygon.Vertices"),
            "SurfaceType": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType"),
            "SurfaceType.CEILING": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType.Ceiling"),
            "SurfaceType.FLOOR": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType.Floor"),
            "SurfaceType.WALL": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType.Wall"),
            "SurfaceType.__str__": ("Shape/SurfaceBoundary.cs", "GonieGonie.InvisibleDragon.Shape.SurfaceType"),
            "Vertex": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex"),
            "Vertex.__add__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.op_Addition"),
            "Vertex.__deepcopy__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex"),
            "Vertex.__init__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.Vertex"),
            "Vertex.__iter__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.X"),
            "Vertex.__mul__": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.op_Multiply"),
            "Vertex.__radd__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.op_Addition"),
            "Vertex.__rmul__": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.op_Multiply"),
            "Vertex.__sub__": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.op_Subtraction"),
            "Vertex.__truediv__": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.op_Division"),
            "Vertex.are_coplanar": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.AreCoplanar"),
            "Vertex.cross": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Cross"),
            "Vertex.distance": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.DistanceTo"),
            "Vertex.dot": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Dot"),
            "Vertex.norm": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Length"),
            "Vertex.unit": ("Shape/Vector3.cs", "GonieGonie.InvisibleDragon.Shape.Vector3.Normalize"),
            "Vertex.x": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.X"),
            "Vertex.y": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.Y"),
            "Vertex.z": ("Shape/Vertex.cs", "GonieGonie.InvisibleDragon.Shape.Vertex.Z"),
        }
        geometry_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/"
            "GeometryCoreOracleParityTests.cs"
        )
        geometry_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Model.GeometryCoreOracleParityTests."
            "MatchesPinnedGeometryCoreThroughBoundedNativeRoutes"
        )
        geometry_test_hash = (
            "sha256:6b9541530d1cd8f029ebd4596c87b019f6b2fecccd7426b12d62400fdb553edf"
        )
        geometry_families = set()
        geometry_assertions = {}
        equivalent_geometry_symbols = set()
        exception_geometry_symbols = set()
        for symbol, (
            index,
            classification,
            exception_id,
            assertion_id,
            receipt_hash,
            adaptation_family,
        ) in expected_geometry_core.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual(classification, entry.classification, symbol)
            expected_refs = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if classification == "exception":
                assert exception_id is not None
                self.assertEqual(exception_id, entry.exception_id, symbol)
                self.assertTrue(exception_id.startswith(adaptation_family + "-"), symbol)
                expected_refs.insert(
                    0, f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
                exception_geometry_symbols.add(symbol)
            else:
                self.assertEqual("equivalent", classification, symbol)
                self.assertIsNone(entry.exception_id, symbol)
                equivalent_geometry_symbols.add(symbol)
            self.assertEqual(tuple(expected_refs), entry.evidence, symbol)
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertEqual(receipt_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(geometry_test_path, receipt.test_path, symbol)
            self.assertEqual(geometry_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(geometry_test_hash, receipt.test_source_sha256, symbol)
            self.assertIn(f"Adaptation family {adaptation_family}", receipt.assertion)
            self.assertIn(
                "Other facts co-recorded in the same case observations are context-only",
                receipt.assertion,
                symbol,
            )
            suffix, native_symbol = expected_geometry_native_symbols[symbol]
            self.assertTrue(evidence_entry.implementation_path.endswith(suffix), symbol)
            self.assertEqual(native_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                geometry_native_source_hashes[evidence_entry.implementation_path],
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            geometry_families.add(adaptation_family)
            geometry_assertions[symbol] = receipt.assertion
        self.assertEqual(31, len(expected_geometry_core))
        self.assertEqual(15, len(geometry_families))
        self.assertEqual(
            {"SurfaceType.CEILING", "SurfaceType.FLOOR", "SurfaceType.WALL"},
            equivalent_geometry_symbols,
        )
        self.assertEqual(28, len(exception_geometry_symbols))
        self.assertIn(
            "V04 zero, nonfinite and exception observations are absent and are not claimed",
            geometry_assertions["Vertex.__rmul__"],
        )
        self.assertIn(
            "No V04 behavior or native exception boundary is claimed",
            geometry_assertions["Vertex.__sub__"],
        )
        self.assertIn(
            "The V04 multiplication and division exceptions are context-only",
            geometry_assertions["Vertex.__add__"],
        )
        self.assertIn(
            "coordinate-projection and reverse-addition observations are context-only",
            geometry_assertions["Vertex.__deepcopy__"],
        )
        self.assertIn(
            "No Vertex.ToVector implementation binding, copy behavior or reverse-addition behavior is claimed",
            geometry_assertions["Vertex.__iter__"],
        )
        self.assertIn(
            "Copy, iteration, multiplication and division observations are context-only",
            geometry_assertions["Vertex.__radd__"],
        )
        self.assertIn(
            "T14 parsing and integer-cast observations co-recorded in the receipt are context-only",
            geometry_assertions["SurfaceType.__str__"],
        )
        self.assertFalse(set(expected_geometry_core) & set(expected_opening_adjacency))
        self.assertFalse(set(expected_geometry_core) & set(expected_zone_idf))
        for preserved_out_of_scope in (
            "Surface.__repr__",
            "Surface.__str__",
            "Vertex.__eq__",
            "Vertex.__repr__",
            "Vertex.__str__",
        ):
            self.assertEqual(
                "out_of_scope",
                by_key[("src/idragon/dragon/shape.py", preserved_out_of_scope)].classification,
                preserved_out_of_scope,
            )
        expected_zone_core = {
            "Zone": (
                1083,
                "permissive-mutable-python-zone-container",
                "dragon-shape-zone-core-1083-4830290e",
                "sha256:559e1ff5e78db8af2a73c3fb32f39d50494680ce9fe950ad434497bbb08a4c3e",
                "permissive-mutable-python-zone-container",
            ),
            "Zone.__init__": (
                1084,
                "unchecked-aliased-python-zone-construction",
                "dragon-shape-zone-core-1084-fad03092",
                "sha256:eaaf8e16d643b7da83ea69ffed2cfa5705a1a981fd2645049886423e7a6986e2",
                "unchecked-aliased-python-zone-construction",
            ),
            "Zone.floor_area": (
                1085,
                "python-floor-identity-filter-and-dynamic-sum",
                "dragon-shape-zone-core-1085-21fe276d",
                "sha256:f62ada88673b4d66bdf3622c9f3b8574e4e28bece58db1e7f0df4d239e1a61a9",
                "python-floor-identity-filter-and-dynamic-sum",
            ),
            "Zone.floor_surface": (
                1086,
                "python-floor-identity-filter-and-fresh-list",
                "dragon-shape-zone-core-1086-53382328",
                "sha256:dd2bdca0b834218b224bcbe5f5a0e736abba096d14e8e3a1aaca319d03c8c554",
                "python-floor-identity-filter-and-fresh-list",
            ),
            "Zone.idf_airexhaustnodelistname": (
                1087,
                "mutable-unvalidated-python-zone-name-formatting-48c6fddb",
                "dragon-shape-zone-core-1087-48c6fddb",
                "sha256:7e3a36b02e12c4ba1e17f94e04c9827be991487685b1f5bacc6d6fcb51057990",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.idf_airinletnodelistname": (
                1088,
                "mutable-unvalidated-python-zone-name-formatting-97745304",
                "dragon-shape-zone-core-1088-97745304",
                "sha256:625c6d4ee5c179618a75f4263dc8d3189c83bad52cbacd1f45aa5a57eff314e8",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.idf_equipmentlistname": (
                1089,
                "mutable-unvalidated-python-zone-name-formatting-ad9ccd78",
                "dragon-shape-zone-core-1089-ad9ccd78",
                "sha256:186e8228fb0083efdfd01beb135eca5ed29f2fedcf377a7a84a7a6a4c8eecbf8",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.supply": (
                1091,
                "embedded-python-zone-supply-coercion-and-mutation",
                "dragon-shape-zone-core-1091-1b5900c0",
                "sha256:99e5f1c10bc7d526475e01e58793fd99fb0ffbed95e0e310c6ad2e8b5c6fc0d0",
                "embedded-python-zone-supply-coercion-and-mutation",
            ),
        }
        zone_native_sources = {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Shape/Zone.cs": "sha256:37bd33ef649a03988255edd9f95bbb0f1ffb7c63cbf8fd1ddb784ebb071b8920",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs": "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905",
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/HvacAbstractions.cs": "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a",
        }
        expected_zone_native_symbols = {
            "Zone": ("Shape/Zone.cs", "GonieGonie.InvisibleDragon.Shape.Zone"),
            "Zone.__init__": (
                "Shape/Zone.cs",
                "GonieGonie.InvisibleDragon.Shape.Zone.Zone",
            ),
            "Zone.floor_area": (
                "Shape/Zone.cs",
                "GonieGonie.InvisibleDragon.Shape.Zone.FloorArea",
            ),
            "Zone.floor_surface": (
                "Shape/Zone.cs",
                "GonieGonie.InvisibleDragon.Shape.Zone.FloorSurfaces",
            ),
            "Zone.idf_airexhaustnodelistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.idf_airinletnodelistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.idf_equipmentlistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "GonieGonie.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.supply": (
                "Hvac/HvacAbstractions.cs",
                "GonieGonie.InvisibleDragon.Hvac.ZoneHvacAssignment",
            ),
        }
        zone_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/"
            "ZoneCoreOracleParityTests.cs"
        )
        zone_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Model.ZoneCoreOracleParityTests."
            "MatchesPinnedZoneCoreThroughTypedNativeRoutes"
        )
        zone_test_hash = (
            "sha256:5d32682cfb81f5e2c4a1f0a34dd183e99aac1cd5972626a102566d7b1616899a"
        )
        zone_assertions = {}
        zone_families = set()
        for symbol, (
            index,
            exception_id,
            assertion_id,
            receipt_hash,
            adaptation_family,
        ) in expected_zone_core.items():
            key = ("src/idragon/dragon/shape.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertTrue(
                exception_id == adaptation_family
                or exception_id.startswith(adaptation_family + "-"),
                symbol,
            )
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertEqual(receipt_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(zone_test_path, receipt.test_path, symbol)
            self.assertEqual(zone_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(zone_test_hash, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            self.assertIn(f"Adaptation family {adaptation_family}", receipt.assertion)
            suffix, native_symbol = expected_zone_native_symbols[symbol]
            self.assertTrue(evidence_entry.implementation_path.endswith(suffix), symbol)
            self.assertEqual(native_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                zone_native_sources[evidence_entry.implementation_path],
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            zone_assertions[symbol] = receipt.assertion
            zone_families.add(adaptation_family)
        self.assertEqual(8, len(expected_zone_core))
        self.assertEqual(6, len(zone_families))
        self.assertIn("not a deep-copy claim", zone_assertions["Zone.floor_surface"])
        self.assertIn(
            "nonfinite, huge or mixed overflow and coercion, missing or raising area",
            zone_assertions["Zone.floor_area"],
        )
        self.assertIn(
            "no deep-copy claim is made",
            zone_assertions["Zone.__init__"],
        )
        for naming_symbol in (
            "Zone.idf_airexhaustnodelistname",
            "Zone.idf_airinletnodelistname",
            "Zone.idf_equipmentlistname",
        ):
            self.assertIn("custom string-conversion", zone_assertions[naming_symbol])
        self.assertIn(
            "context HVAC symbols, virtual subclasses, descriptor tampering",
            zone_assertions["Zone.supply"],
        )
        self.assertFalse(set(expected_zone_core) & set(expected_geometry_core))
        self.assertFalse(set(expected_zone_core) & set(expected_opening_adjacency))
        self.assertFalse(set(expected_zone_core) & set(expected_zone_idf))
        for preserved_symbol, expected_classification in {
            "Window": "exception",
            "Window.__init__": "exception",
            "Zone.is_conditioned": "exception",
            "Zone.to_idf_hvac_default_object": "exception",
            "Zone.to_idf_load_object": "exception",
            "Zone.to_idf_object": "exception",
            "SurfaceType.FLOOR": "equivalent",
        }.items():
            self.assertEqual(
                expected_classification,
                by_key[("src/idragon/dragon/shape.py", preserved_symbol)].classification,
                preserved_symbol,
            )
        for context_symbol, expected_classification in {
            "ElectricRadiator": "needs_reverification",
            "ElectricRadiator.__init__": "needs_reverification",
            "ElectricRadiator.heatable": "needs_reverification",
            "SupplyGroup": "needs_reverification",
            "SupplyGroup.__init__": "exception",
            "SupplySystem": "needs_reverification",
        }.items():
            self.assertEqual(
                expected_classification,
                by_key[("src/idragon/dragon/hvac.py", context_symbol)].classification,
                context_symbol,
            )
        expected_source_system_idf = {
            "AbsorptionChiller.to_idf_object": (
                644,
                "legacy-context-absorption-chiller-idf-emission",
                "dragon-hvac-absorption-chiller-to-idf-object-17d5fb8a",
            ),
            "Boiler.to_idf_object": (
                655,
                "compact-native-boiler-idf-emission",
                "dragon-hvac-boiler-to-idf-object-b63a454b",
            ),
            "Boiler.to_idf_object_as_generator": (
                656,
                "fresh-native-boiler-generator-idf-emission",
                "dragon-hvac-boiler-to-idf-object-as-generator-d239b10e",
            ),
            "Chiller.to_idf_object": (
                660,
                "legacy-context-chiller-idf-emission",
                "dragon-hvac-chiller-to-idf-object-fc75129f",
            ),
            "ClosedSingleSpeedCoolingTower.to_idf_main_object": (
                663,
                "cooling-tower-context-closed-single-speed-main-idf-emission",
                "dragon-hvac-closed-single-speed-cooling-tower-to-idf-main-object-0e14065a",
            ),
            "ClosedTwoSpeedCoolingTower.to_idf_main_object": (
                666,
                "cooling-tower-context-closed-two-speed-main-idf-emission",
                "dragon-hvac-closed-two-speed-cooling-tower-to-idf-main-object-30402683",
            ),
            "CompressorType.to_idf_curve_object": (
                672,
                "chiller-context-compressor-curve-idf-emission",
                "dragon-hvac-compressor-type-to-idf-curve-object-8ca6c2d0",
            ),
            "CoolingTower.to_idf_main_object": (
                684,
                "contextual-native-cooling-tower-main-idf-contract",
                "dragon-hvac-cooling-tower-to-idf-main-object-4615e08c",
            ),
            "CoolingTower.to_idf_object": (
                685,
                "legacy-context-cooling-tower-idf-emission",
                "dragon-hvac-cooling-tower-to-idf-object-74287ab5",
            ),
            "HeatPump.to_idf_object": (
                743,
                "compact-native-heat-pump-idf-emission",
                "dragon-hvac-heat-pump-to-idf-object-b8cb28ab",
            ),
            "OpenSingleSpeedCoolingTower.to_idf_main_object": (
                746,
                "cooling-tower-context-open-single-speed-main-idf-emission",
                "dragon-hvac-open-single-speed-cooling-tower-to-idf-main-object-102bccd9",
            ),
            "OpenTwoSpeedCoolingTower.to_idf_main_object": (
                749,
                "cooling-tower-context-open-two-speed-main-idf-emission",
                "dragon-hvac-open-two-speed-cooling-tower-to-idf-main-object-7fd75338",
            ),
            "SourceSystem.to_idf_object": (
                788,
                "contextual-native-source-system-idf-contract",
                "dragon-hvac-source-system-to-idf-object-63aa5eab",
            ),
        }
        for symbol, (index, exception_id, assertion_id) in (
            expected_source_system_idf.items()
        ):
            key = ("src/idragon/dragon/hvac.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
        api_entries = [
            entry
            for entry in compatibility.matrix.entries
            if entry.path == "src/epsimple/api.py"
        ]
        self.assertEqual(10, len(api_entries))
        self.assertTrue(
            all(entry.classification == "out_of_scope" for entry in api_entries)
        )
        self.assertTrue(
            all(
                entry.evidence[0].startswith("upstream/scope-decisions.json#")
                for entry in api_entries
            )
        )
        self.assertEqual(
            "exception",
            by_key[("src/epsimple/utils.py", "GRJSON_FORMAT")].classification,
        )
        self.assertEqual(
            "immutable-validated-grm-template",
            by_key[("src/epsimple/utils.py", "GRJSON_FORMAT")].exception_id,
        )
        expected_constants_metadata = {
            "Directory": (
                568,
                "resolved-native-runtime-and-resource-layout",
                "constants-metadata-568-5b876ad7",
                "sha256:b010e27fab04726eca7bad08cc9862c6f9614f44bd86658c6fa519f909de7c58",
                "sha256:1260f23bad5142f44afb51fbbaf1a335b256712d9d1fe3670f3c84f238ecc1ae",
                "src/Shared/GonieGonie.EnergyPlus.Runtime/RuntimeResolver.cs",
                "GonieGonie.EnergyPlus.Runtime.RuntimeResolver",
                "sha256:5c4170c2f4648a5fab93ff092c2c307589bd909d436437ccc66280bf4ac487f6",
            ),
            "Directory.ENERGYPLUS_DIR": (
                569,
                "explicit-validated-native-energyplus-runtime-root",
                "constants-metadata-569-7e01ceac",
                "sha256:e245c641ed9b9a37d6e2f7f17c52f6f44ae274516b4edfe740c4866e81436960",
                "sha256:9197d216c12052e6553557ef964bd06d48f09c212f4598773f1d7f299e763eba",
                "src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusRuntimeLayout.cs",
                "GonieGonie.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.RootPath",
                "sha256:3b2beace10108918cfc69b06be42da966fc138e3f2fcff1c9bf39d2d5cdce84c",
            ),
            "Directory.IDD_DIR": (
                570,
                "validated-native-idd-path-resolution",
                "constants-metadata-570-1f0c2815",
                "sha256:bcdb44f08ca85537eca206313be08bf31384dec386fd842bce38e96b3553d1f9",
                "sha256:ef814651c1d0e843ae53c9dd6fc834ccd2271ea975d2e132d809d644eb53cc88",
                "src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusRuntimeLayout.cs",
                "GonieGonie.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.IddPath",
                "sha256:3b2beace10108918cfc69b06be42da966fc138e3f2fcff1c9bf39d2d5cdce84c",
            ),
            "Directory.PROFILE_DIR": (
                571,
                "typed-native-profile-data-without-package-profile-directory",
                "constants-metadata-571-f65d5eae",
                "sha256:5536cae6af137a72c7c927cc4221cd1eea35e0f541b74922275683420ec267eb",
                "sha256:0c358b1bdfba0186aaf1fbae8d5c9768b2e34c3528cf02df1b720e145eabb25d",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Profile/Profile.cs",
                "GonieGonie.InvisibleDragon.Profile.Profile",
                "sha256:99c3e0557ba737aa74cfb0f15faf0730d9f7215a6b66f7f6b6b2044cf4013c72",
            ),
            "PackageInfo": (
                572,
                "static-native-package-information",
                "constants-metadata-572-aaf5b98d",
                "sha256:a3741a5a5870ff30f6e840e266c436abda507ff96c93e974cbc68df211e28168",
                "sha256:9add953f90477a1c6294d5c2eba362c862fa6f0aef2bd1b25c4f598ac5d3910f",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
            "PackageInfo.NAME": (
                573,
                "native-invisibledragon-package-name",
                "constants-metadata-573-3942a963",
                "sha256:1539ac6658af20566740884fc3f1d4802e99a0a96b9760a06d69f0f37420804b",
                "sha256:ce337d684f985b7fe8402969f81e473a06ad4a872a1fb0acf66469ddf6f58d73",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo.Name",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
            "PackageInfo.REQUIRED_PYTHON": (
                574,
                "compiled-native-target-framework-contract",
                "constants-metadata-574-cf74d0eb",
                "sha256:c666b0f9e6499a34acc0a98f3f01584e73add9a9d64dd3e66842e79012714aad",
                "sha256:d16c7e2050430c23a42db97ee0e60db6e4b9d34667a037539879581bdbeaf7aa",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
            "PackageInfo.VERSION": (
                575,
                "native-semantic-version-string",
                "constants-metadata-575-a8260e5f",
                "sha256:9ab52755f1c9d6600068d446d4df9420aa0c37df3a365668dc241334f5ca63d7",
                "sha256:450d23845ac4b76a1a1b9b129d52ef1fc8f824d22eb94a17f43126bb22caab00",
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/PackageInfo.cs",
                "GonieGonie.InvisibleDragon.PackageInfo.Version",
                "sha256:e4851f596d1761301e6f8a30d30cab04c28a96ab59c3d5419337174839f8ea13",
            ),
        }
        constants_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Common/"
            "ConstantsMetadataOracleParityTests.cs"
        )
        constants_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Common.ConstantsMetadataOracleParityTests."
            "MatchesPinnedConstantsMetadataThroughBoundedNativeAdaptations"
        )
        constants_test_hash = (
            "sha256:fe0809967c5fcc94c70e1805a215e670d709dbf72d3ba1888d78b5bd55e404ef"
        )
        exceptions_by_id = {
            item.identifier: item for item in configuration.exceptions
        }
        for symbol, (
            index,
            exception_id,
            assertion_id,
            direct_hash,
            collector_hash,
            implementation_path,
            implementation_symbol,
            implementation_hash,
        ) in expected_constants_metadata.items():
            key = ("src/idragon/constants.py", symbol)
            entry = by_key[key]
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(entry, compatibility.matrix.entries[index], symbol)
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(
                (
                    f"upstream/compatibility-exceptions.yml#{exception_id}",
                    f"upstream/symbol-evidence.json#{assertion_id}",
                ),
                entry.evidence,
                symbol,
            )
            exception = exceptions_by_id[exception_id]
            self.assertEqual(key, (exception.upstream_path, exception.upstream_symbol))
            self.assertEqual(
                compatibility.inventory.symbols[index].symbol_hash,
                exception.upstream_symbol_hash,
                symbol,
            )
            self.assertIn(("engineering_result", entry.rationale), exception.effects)
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(implementation_path, evidence_entry.implementation_path, symbol)
            self.assertEqual(implementation_symbol, evidence_entry.implementation_symbol, symbol)
            self.assertEqual(
                implementation_hash,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_hash, receipt.assertion, symbol)
            self.assertEqual(collector_hash, receipt.expected_output_sha256, symbol)
            self.assertEqual(constants_test_path, receipt.test_path, symbol)
            self.assertEqual(constants_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(constants_test_hash, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
        self.assertEqual(8, len(expected_constants_metadata))
        for index, symbol in enumerate(
            (
                "SpecialTag",
                "SpecialTag.__format__",
                "SpecialTag.__repr__",
                "SpecialTag.__str__",
            ),
            start=576,
        ):
            key = ("src/idragon/constants.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual("out_of_scope", by_key[key].classification, symbol)
            self.assertTrue(
                by_key[key].evidence[0].startswith("upstream/scope-decisions.json#"),
                symbol,
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key, symbol)
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
            key = ("src/epsimple/constants.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual("needs_reverification", by_key[key].classification, symbol)
            self.assertNotIn(key, symbol_evidence.entries_by_key, symbol)
        expected_common_core = {
            "Setting": ("equivalent", None),
            "Setting.DEFAULT_EP_VERSION": ("equivalent", None),
            "Setting.DEFAULT_YEAR": ("equivalent", None),
            "Version": ("exception", "native-energyplus-version-descriptor"),
            "Version.__format__": ("equivalent", None),
            "Version.__init__": (
                "exception",
                "validated-energyplus-version-construction",
            ),
            "Version.__iter__": ("equivalent", None),
            "Version.ep_dirname": ("equivalent", None),
            "Version.iddname": ("equivalent", None),
            "Version.major": ("equivalent", None),
            "Version.minor": ("equivalent", None),
            "Version.patch": ("equivalent", None),
            "Version.to_version_anyway": (
                "exception",
                "strongly-typed-energyplus-version-coercion",
            ),
        }
        for symbol, (classification, exception_id) in expected_common_core.items():
            entry = by_key[("src/idragon/common.py", symbol)]
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
        expected_launcher_results = {
            "EnergyPlusResult": "immutable-structured-energyplus-result",
            "EnergyPlusResult.__init__": "validated-energyplus-result-file-loading",
            "EnergyPlusResult.parse_audit": "ordered-typed-energyplus-audit-parsing",
            "EnergyPlusResult.parse_bnd": "csv-aware-energyplus-boundary-parsing",
            "EnergyPlusResult.parse_err": "structured-energyplus-error-log-parsing",
            "EnergyPlusResult.parse_eso": "explicitly-unsupported-energyplus-eso",
            "EnergyPlusResult.parse_table": "typed-energyplus-tabular-parsing",
        }
        for symbol, exception_id in expected_launcher_results.items():
            entry = by_key[("src/idragon/launcher.py", symbol)]
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
        terminal_scope = {
            (
                "src/epsimple/core/model.py",
                "GreenRetrofitModel.from_excel",
            ): "scope-src-epsimple-core-model-py-greenretrofitmodel-from-excel-46935cc1",
            (
                "src/idragon/constants.py",
                "SpecialTag",
            ): "scope-src-idragon-constants-py-specialtag-3a4b3781",
        }
        for key, decision_id in terminal_scope.items():
            self.assertEqual("out_of_scope", by_key[key].classification)
            self.assertEqual(
                (f"upstream/scope-decisions.json#{decision_id}",),
                by_key[key].evidence,
            )
        self.assertEqual(
            "needs_reverification",
            by_key[("src/idragon/imugi.py", "IdfObjectList.set_wwr")].classification,
        )
        people_activity = by_key[
            (
                "src/idragon/dragon/model.py",
                "EnergyModel.create_default_idf",
            )
        ]
        self.assertEqual("equivalent", people_activity.classification)
        self.assertIsNone(people_activity.exception_id)
        self.assertEqual(
            (
                "upstream/symbol-evidence.json#dragon-model-construction-defaults-create-default-idf-585b5368",
            ),
            people_activity.evidence,
        )
        add_supply_system = by_key[
            (
                "src/idragon/dragon/model.py",
                "EnergyModel.add_supply_system",
            )
        ]
        self.assertEqual("exception", add_supply_system.classification)
        self.assertEqual(
            "model-context-supply-system-assembly",
            add_supply_system.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#model-context-supply-system-assembly",
                "upstream/symbol-evidence.json#dragon-model-add-supply-system-174532d0",
            ),
            add_supply_system.evidence,
        )
        photovoltaic_to_idf_key = (
            "src/idragon/dragon/hvac.py",
            "PhotoVoltaicPanel.to_idf_object",
        )
        photovoltaic_to_idf = by_key[photovoltaic_to_idf_key]
        self.assertEqual(
            photovoltaic_to_idf_key,
            compatibility.inventory.symbols[761].key,
        )
        self.assertEqual(photovoltaic_to_idf, compatibility.matrix.entries[761])
        self.assertEqual("exception", photovoltaic_to_idf.classification)
        self.assertEqual(
            "compact-native-photovoltaic-idf-emission",
            photovoltaic_to_idf.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#"
                "compact-native-photovoltaic-idf-emission",
                "upstream/symbol-evidence.json#"
                "dragon-hvac-photovoltaic-to-idf-object-4723273d",
            ),
            photovoltaic_to_idf.evidence,
        )
        expected_supply_group_core = {
            "SupplyGroup.__init__": (
                "exception",
                "immutable-validated-supply-group-construction",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "immutable-validated-supply-group-construction",
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-init-02b3c43a",
                ),
            ),
            "SupplyGroup.coolable": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-coolable-0f6f3f1a",
                ),
            ),
            "SupplyGroup.cooling_systems": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-cooling-systems-e2ee9492",
                ),
            ),
            "SupplyGroup.heatable": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-heatable-ab11abdd",
                ),
            ),
            "SupplyGroup.heating_systems": (
                "equivalent",
                None,
                (
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-heating-systems-1fdfba66",
                ),
            ),
            "SupplyGroup.sources": (
                "exception",
                "stable-entity-id-supply-source-deduplication",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "stable-entity-id-supply-source-deduplication",
                    "upstream/symbol-evidence.json#"
                    "dragon-hvac-supply-group-core-sources-482d0fa2",
                ),
            ),
        }
        for symbol, (classification, exception_id, evidence) in (
            expected_supply_group_core.items()
        ):
            entry = by_key[("src/idragon/dragon/hvac.py", symbol)]
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(evidence, entry.evidence, symbol)
        self.assertEqual(
            "needs_reverification",
            by_key[("src/idragon/dragon/hvac.py", "SupplyGroup")].classification,
        )
        supply_group_to_idf = by_key[
            ("src/idragon/dragon/hvac.py", "SupplyGroup.to_idf_object")
        ]
        self.assertEqual("exception", supply_group_to_idf.classification)
        self.assertEqual(
            "model-context-supply-group-idf-assembly",
            supply_group_to_idf.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#"
                "model-context-supply-group-idf-assembly",
                "upstream/symbol-evidence.json#"
                "dragon-hvac-supply-group-to-idf-object-3f9c508c",
            ),
            supply_group_to_idf.evidence,
        )
        expected_shading_material = {
            "Blind.to_idf_object": (
                "model-context-blind-shading-material-emission",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "model-context-blind-shading-material-emission",
                    "upstream/symbol-evidence.json#"
                    "dragon-shape-blind-to-idf-object-16e27412",
                ),
            ),
            "Shade.to_idf_object": (
                "model-context-shade-shading-material-emission",
                (
                    "upstream/compatibility-exceptions.yml#"
                    "model-context-shade-shading-material-emission",
                    "upstream/symbol-evidence.json#"
                    "dragon-shape-shade-to-idf-object-75e6c8e6",
                ),
            ),
        }
        for symbol, (exception_id, evidence) in expected_shading_material.items():
            entry = by_key[("src/idragon/dragon/shape.py", symbol)]
            self.assertEqual("exception", entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            self.assertEqual(evidence, entry.evidence, symbol)
        energy_model_to_idf_key = (
            "src/idragon/dragon/model.py",
            "EnergyModel.to_idf",
        )
        energy_model_to_idf = by_key[energy_model_to_idf_key]
        self.assertEqual(energy_model_to_idf_key, compatibility.inventory.symbols[821].key)
        self.assertEqual(energy_model_to_idf, compatibility.matrix.entries[821])
        self.assertEqual("exception", energy_model_to_idf.classification)
        self.assertEqual(
            "validated-fresh-energy-model-idf-assembly",
            energy_model_to_idf.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#"
                "validated-fresh-energy-model-idf-assembly",
                "upstream/symbol-evidence.json#"
                "dragon-model-energy-model-to-idf-de10251f",
            ),
            energy_model_to_idf.evidence,
        )

    def test_rejects_non_goniegonie_product_ownership(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(
                workspace,
                project="OtherCompany.Product.Core",
            )

            with self.assertRaisesRegex(ConfigurationError, "GonieGonie"):
                load_configuration(lock, port_map, exceptions)

    def test_rejects_port_path_outside_locked_modules(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(
                workspace,
                mapping_path="outside/service.py",
            )

            with self.assertRaisesRegex(ConfigurationError, "outside every locked module"):
                load_configuration(lock, port_map, exceptions)

    def test_yaml_subset_rejects_anchors_and_duplicate_keys(self) -> None:
        self.assertEqual([], parse_yaml_subset("[]\n", source_name="test.yml"))
        with self.assertRaisesRegex(ConfigurationError, "anchors"):
            parse_yaml_subset("value: &shared text\n", source_name="test.yml")
        with self.assertRaisesRegex(ConfigurationError, "duplicate key"):
            parse_yaml_subset("value: first\nvalue: second\n", source_name="test.yml")


if __name__ == "__main__":
    unittest.main()

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
        self.assertEqual(694, len(compatibility.needs_reverification))
        self.assertEqual(
            133,
            sum(
                entry.classification == "equivalent"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertEqual(
            163,
            sum(
                entry.classification == "exception"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertIsNotNone(compatibility.symbol_evidence)
        symbol_evidence = compatibility.symbol_evidence
        assert symbol_evidence is not None
        self.assertEqual(296, len(symbol_evidence.entries))
        self.assertEqual(296, len(symbol_evidence.receipts))
        self.assertEqual(
            "sha256:fbd996372aa33c3c14bbb14e3c16effbf66ff1be51384d00e7aa3d4cc64657fe",
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
                "sha256:c075c45deb7cda1407a72808c0d277d6efd5316085b144095d2bf517386973f7",
                "permissive-python-blind-state",
            ),
            "Blind.__init__": (
                1026,
                "permissive-python-blind-state-574e9b5a",
                "dragon-shape-opening-adjacency-core-1026-574e9b5a",
                "sha256:5388e0665f85bc41bb818f26c8f6fa89cf76898e26e6df4d36abe0c373570bee",
                "permissive-python-blind-state",
            ),
            "Door": (
                1028,
                "permissive-python-door-state-717d717a",
                "dragon-shape-opening-adjacency-core-1028-717d717a",
                "sha256:fa39e26798264f38d0e9a43f1940c2863f70f05203b2b1e8c74faa3c5eb8afd9",
                "permissive-python-door-state",
            ),
            "Door.__init__": (
                1029,
                "permissive-python-door-state-efd71c81",
                "dragon-shape-opening-adjacency-core-1029-efd71c81",
                "sha256:a7a103e8899887ec153199f974e4050ca493cda73d31b7c64e562ebba60ab30e",
                "permissive-python-door-state",
            ),
            "Shade": (
                1030,
                "permissive-python-shade-state-9404da04",
                "dragon-shape-opening-adjacency-core-1030-9404da04",
                "sha256:45b6423f0139944ee5c8cb4726894b9dc4585b05cdd343bddb24b830fe43c35f",
                "permissive-python-shade-state",
            ),
            "Shade.__init__": (
                1031,
                "permissive-python-shade-state-f76ed298",
                "dragon-shape-opening-adjacency-core-1031-f76ed298",
                "sha256:6d4294f2c7584ecee38272e92a20d323ba5d26bbe1b6cf488d5749dfdfc8c496",
                "permissive-python-shade-state",
            ),
            "Shading": (
                1033,
                "directly-instantiable-empty-python-shading-4dba9833",
                "dragon-shape-opening-adjacency-core-1033-4dba9833",
                "sha256:8b9e5ab60f25bf306e117d4df92ea08a784482435e73166da9ecdb52ab860bbc",
                "directly-instantiable-empty-python-shading",
            ),
            "Surface.__init__": (
                1035,
                "aliased-python-surface-opening-inputs-ef349ef4",
                "dragon-shape-opening-adjacency-core-1035-ef349ef4",
                "sha256:4b3ebe4b809eb5da6aa98988847d7dff288a1fd3370583166b27b0210feb31ee",
                "aliased-python-surface-opening-inputs",
            ),
            "Surface.blinded_window": (
                1039,
                "fresh-python-blinded-window-projection-f520fbfe",
                "dragon-shape-opening-adjacency-core-1039-f520fbfe",
                "sha256:69f056b8beb1e306a0c7ad7c83365a09e184365bb579bddfec8a349663b17d03",
                "fresh-python-blinded-window-projection",
            ),
            "Surface.boundary": (
                1040,
                "mutable-reciprocal-python-surface-adjacency-7753d967",
                "dragon-shape-opening-adjacency-core-1040-7753d967",
                "sha256:84d8d135700b571157f91ee6f2c3b9ae859d233d0dc5beadfd4eb16704174189",
                "mutable-reciprocal-python-surface-adjacency",
            ),
            "Surface.get_subsurface": (
                1042,
                "legacy-linear-scale-subsurface-projection-7e43708d",
                "dragon-shape-opening-adjacency-core-1042-7e43708d",
                "sha256:0705e84c47890ec56447c65eff16bb31d5340d79e19921ae7b985ecb037855b3",
                "legacy-linear-scale-subsurface-projection",
            ),
            "SurfaceBoundaryCondition": (
                1048,
                "lowercase-python-surface-boundary-enum-73a8b86f",
                "dragon-shape-opening-adjacency-core-1048-73a8b86f",
                "sha256:08ce2c6a908b36f58f12d985c5e38cb513acbae7176e30362366de85347ed0b0",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ADIABATIC": (
                1049,
                "lowercase-python-surface-boundary-enum-1d0e3d46",
                "dragon-shape-opening-adjacency-core-1049-1d0e3d46",
                "sha256:71a4f989a069bbfa082ed1c95b6d592cb559164bbc3e59bbd3e4516acbd121b8",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.GROUND": (
                1050,
                "lowercase-python-surface-boundary-enum-0992cbf6",
                "dragon-shape-opening-adjacency-core-1050-0992cbf6",
                "sha256:93821fe3c3d6a6237b2810173fd5930d7650eead430add543f323b70ec45cc48",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.OUTDOOR": (
                1051,
                "lowercase-python-surface-boundary-enum-8560160a",
                "dragon-shape-opening-adjacency-core-1051-8560160a",
                "sha256:fa3365775b6703acd140afe11e50b9f546326dcb8b74d608086d15e6f3393b20",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ZONE": (
                1052,
                "lowercase-python-surface-boundary-enum-3ec06789",
                "dragon-shape-opening-adjacency-core-1052-3ec06789",
                "sha256:ce75591156a712c29b635ee22f70387d62faee6d006cf41308a4649819ed6abc",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.__str__": (
                1053,
                "lowercase-python-surface-boundary-enum-f40e4929",
                "dragon-shape-opening-adjacency-core-1053-f40e4929",
                "sha256:334050b16537ed04fbda77c181a7442d51c502b0d35014eedc670fd651e85786",
                "lowercase-python-surface-boundary-enum",
            ),
            "Window": (
                1081,
                "permissive-python-window-state-af640a9a",
                "dragon-shape-opening-adjacency-core-1081-af640a9a",
                "sha256:19224d3273d70c3effeced094b414ca5df1fdb1316e39f9ba9f79236dd760c51",
                "permissive-python-window-state",
            ),
            "Window.__init__": (
                1082,
                "permissive-python-window-state-3ce851bd",
                "dragon-shape-opening-adjacency-core-1082-3ce851bd",
                "sha256:dc72750acd6bdc3c06076d2f61c0cd7e8b1ef02b5e7499c142a0142d1bc426d9",
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
            "sha256:7ad3b9251f5c73e5a710d4ce7ef836d63e79000c2c3e6a00952ca61ccfaa5aa2"
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

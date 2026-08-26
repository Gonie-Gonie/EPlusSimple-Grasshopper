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
        self.assertEqual(713, len(compatibility.needs_reverification))
        self.assertEqual(
            133,
            sum(
                entry.classification == "equivalent"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertEqual(
            144,
            sum(
                entry.classification == "exception"
                for entry in compatibility.matrix.entries
            ),
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

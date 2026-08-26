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
        self.assertEqual(770, len(compatibility.needs_reverification))
        self.assertEqual(
            113,
            sum(
                entry.classification == "equivalent"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertEqual(
            107,
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
        self.assertEqual("needs_reverification", people_activity.classification)
        self.assertIsNone(people_activity.exception_id)

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

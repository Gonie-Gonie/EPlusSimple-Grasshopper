from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
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
        self.assertEqual(582, len(configuration.exceptions))
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
        self.assertEqual(0, len(compatibility.needs_reverification))
        self.assertEqual(
            413,
            sum(
                entry.classification == "equivalent"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertEqual(
            577,
            sum(
                entry.classification == "exception"
                for entry in compatibility.matrix.entries
            ),
        )
        self.assertIsNotNone(compatibility.symbol_evidence)
        symbol_evidence = compatibility.symbol_evidence
        assert symbol_evidence is not None
        self.assertEqual(
            "sha256:b0c61e0a167210bbd78e6e166295f3b16ec489a513381d7bf6b77ab0f9fcbb9f",
            compatibility.matrix.content_sha256,
        )
        self.assertEqual(990, len(symbol_evidence.entries))
        self.assertEqual(990, len(symbol_evidence.receipts))
        self.assertEqual(
            "sha256:e026066f4d14bb4903d38332e3eda9c40e2ea69f6eb76feba1ad22385069d9f6",
            symbol_evidence.content_sha256,
        )
        self.assertEqual(
            252,
            sum(
                entry.classification == "out_of_scope"
                for entry in compatibility.matrix.entries
            ),
        )

        source_tower_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/dragon-hvac-source-tower-core-oracle.json"
        )
        source_tower_fixture = json.loads(
            source_tower_fixture_path.read_text(encoding="utf-8")
        )
        source_tower_contract = source_tower_fixture["consumer_contract"]
        source_tower_targets = tuple(source_tower_fixture["target_receipts"])
        source_tower_target_indices = tuple(
            item["inventory_index"] for item in source_tower_targets
        )
        source_tower_exception_ids = set(
            source_tower_contract["adaptations"].values()
        )
        source_tower_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "dragon-hvac-source-tower-core-"
            )
        )
        self.assertEqual(59, len(source_tower_target_indices))
        self.assertEqual(32, len(source_tower_exception_ids))
        self.assertEqual(59, len(source_tower_evidence_entries))

        supply_core_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/dragon-hvac-supply-core-oracle.json"
        )
        supply_core_fixture = json.loads(
            supply_core_fixture_path.read_text(encoding="utf-8")
        )
        supply_core_contract = supply_core_fixture["consumer_contract"]
        supply_core_targets = tuple(supply_core_fixture["target_receipts"])
        supply_core_target_indices = tuple(
            item["inventory_index"] for item in supply_core_targets
        )
        supply_core_exception_ids = set(supply_core_contract["adaptations"].values())
        supply_core_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith("dragon-hvac-supply-core-")
        )
        self.assertEqual(49, len(supply_core_target_indices))
        self.assertEqual(31, len(supply_core_exception_ids))
        self.assertEqual(49, len(supply_core_evidence_entries))

        appender_controller_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/dragon-hvac-appenders-controllers-oracle.json"
        )
        appender_controller_fixture = json.loads(
            appender_controller_fixture_path.read_text(encoding="utf-8")
        )
        appender_controller_contract = appender_controller_fixture["consumer_contract"]
        appender_controller_targets = tuple(
            appender_controller_fixture["target_receipts"]
        )
        appender_controller_target_indices = tuple(
            item["inventory_index"] for item in appender_controller_targets
        )
        appender_controller_exception_ids = set(
            appender_controller_contract["adaptations"].values()
        )
        appender_controller_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "dragon-hvac-appenders-controllers-"
            )
        )
        self.assertEqual(24, len(appender_controller_target_indices))
        self.assertEqual(24, len(appender_controller_exception_ids))
        self.assertEqual(24, len(appender_controller_evidence_entries))

        misc_systems_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/dragon-hvac-misc-systems-core-oracle.json"
        )
        misc_systems_fixture = json.loads(
            misc_systems_fixture_path.read_text(encoding="utf-8")
        )
        misc_systems_contract = misc_systems_fixture["consumer_contract"]
        misc_systems_targets = tuple(misc_systems_fixture["target_receipts"])
        misc_systems_target_indices = tuple(
            item["inventory_index"] for item in misc_systems_targets
        )
        misc_systems_exception_ids = {
            f"{misc_systems_contract['adaptations'][item['symbol']]}-{item['inventory_index']}"
            for item in misc_systems_targets
            if misc_systems_contract["classifications"][item["symbol"]] == "exception"
        }
        misc_systems_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "dragon-hvac-misc-systems-core-"
            )
        )
        self.assertEqual(15, len(misc_systems_target_indices))
        self.assertEqual(8, len(misc_systems_exception_ids))
        self.assertEqual(15, len(misc_systems_evidence_entries))

        imugi_idd_definitions_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/imugi-idd-definitions-core-oracle.json"
        )
        imugi_idd_definitions_fixture = json.loads(
            imugi_idd_definitions_fixture_path.read_text(encoding="utf-8")
        )
        imugi_idd_definitions_contract = imugi_idd_definitions_fixture[
            "consumer_contract"
        ]
        imugi_idd_definitions_targets = tuple(
            imugi_idd_definitions_fixture["target_receipts"]
        )
        imugi_idd_definitions_target_indices = tuple(
            item["inventory_index"] for item in imugi_idd_definitions_targets
        )
        imugi_idd_definitions_exception_ids = set(
            imugi_idd_definitions_contract["adaptations"].values()
        )
        imugi_idd_definitions_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "imugi-idd-definitions-core-"
            )
        )
        self.assertEqual(40, len(imugi_idd_definitions_target_indices))
        self.assertEqual(22, len(imugi_idd_definitions_exception_ids))
        self.assertEqual(40, len(imugi_idd_definitions_evidence_entries))

        imugi_idd_schema_static_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/imugi-idd-schema-static-core-oracle.json"
        )
        imugi_idd_schema_static_fixture = json.loads(
            imugi_idd_schema_static_fixture_path.read_text(encoding="utf-8")
        )
        imugi_idd_schema_static_contract = imugi_idd_schema_static_fixture[
            "consumer_contract"
        ]
        imugi_idd_schema_static_targets = tuple(
            imugi_idd_schema_static_fixture["target_receipts"]
        )
        imugi_idd_schema_static_target_indices = tuple(
            item["inventory_index"] for item in imugi_idd_schema_static_targets
        )
        imugi_idd_schema_static_exception_ids = set(
            imugi_idd_schema_static_contract["adaptations"].values()
        )
        imugi_idd_schema_static_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "imugi-idd-schema-static-core-"
            )
        )
        self.assertEqual(21, len(imugi_idd_schema_static_target_indices))
        self.assertEqual(12, len(imugi_idd_schema_static_exception_ids))
        self.assertEqual(21, len(imugi_idd_schema_static_evidence_entries))

        imugi_idf_object_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/imugi-idf-object-core-oracle.json"
        )
        imugi_idf_object_fixture = json.loads(
            imugi_idf_object_fixture_path.read_text(encoding="utf-8")
        )
        imugi_idf_object_contract = imugi_idf_object_fixture["consumer_contract"]
        imugi_idf_object_targets = tuple(imugi_idf_object_fixture["target_receipts"])
        imugi_idf_object_target_indices = tuple(
            item["inventory_index"] for item in imugi_idf_object_targets
        )
        imugi_idf_object_exception_ids = {
            f"typed-immutable-native-idf-adaptation-{item['inventory_index']}"
            for item in imugi_idf_object_targets
            if imugi_idf_object_contract["classifications"][item["symbol"]]
            == "exception"
        }
        imugi_idf_object_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith("imugi-idf-object-core-")
        )
        self.assertEqual(25, len(imugi_idf_object_target_indices))
        self.assertEqual(19, len(imugi_idf_object_exception_ids))
        self.assertEqual(25, len(imugi_idf_object_evidence_entries))

        imugi_idf_object_list_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/imugi-idf-object-list-core-oracle.json"
        )
        imugi_idf_object_list_fixture = json.loads(
            imugi_idf_object_list_fixture_path.read_text(encoding="utf-8")
        )
        imugi_idf_object_list_contract = imugi_idf_object_list_fixture[
            "consumer_contract"
        ]
        imugi_idf_object_list_targets = tuple(
            imugi_idf_object_list_fixture["target_receipts"]
        )
        imugi_idf_object_list_target_indices = tuple(
            item["inventory_index"] for item in imugi_idf_object_list_targets
        )
        imugi_idf_object_list_exception_ids = {
            f"typed-native-collection-adaptation-{item['inventory_index']}"
            for item in imugi_idf_object_list_targets
            if imugi_idf_object_list_contract["classifications"][item["symbol"]]
            == "exception"
        }
        imugi_idf_object_list_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "imugi-idf-object-list-core-"
            )
        )
        self.assertEqual(19, len(imugi_idf_object_list_target_indices))
        self.assertEqual(15, len(imugi_idf_object_list_exception_ids))
        self.assertEqual(19, len(imugi_idf_object_list_evidence_entries))

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

        expected_identifier_conventions = {
            10: ("AUTOID_PREFIX", "exception", "immutable-native-auto-id-prefix-catalog-9a7c270a", "sha256:49d889ac24e0edaf914c064f03a2ee0f22cd61794c90f41a36730989ae5c4b70", "sha256:44de3af1dd40f2c56ec0db2bdf902f628dc57024a6e4f7d91be9132584287863", "auto"),
            11: ("AUTOID_PREFIX.DAY_SCHEDULE", "equivalent", None, "sha256:ebe9020f32dda40ac41369079883b4c266213f5fd658ef18e2e49809a5ec671c", "sha256:9c6e4882cd3d2033e0f0b8f52a4dfea23ad588f0e315a3479d9d136e125903f1", "auto"),
            12: ("AUTOID_PREFIX.FENESTRATION", "equivalent", None, "sha256:29415a4532cc1e828c92e40db1a99b5b72c49735f5ead79e8af19d254bd6bd0a", "sha256:72887bc90df3d0780aafc90be4f57e2df76a86bd9015b2d4d2b79dc4f89f1503", "auto"),
            13: ("AUTOID_PREFIX.FENESTRATION_CONSTRUCTION", "equivalent", None, "sha256:d73cc7faf13a747201d43ac4dc9fb268370fcaac2da9f95df1a38622ad765a19", "sha256:d03ff1f4409c50dd96e733c6feb286faca995480f2e7b0f8e837b0beed4ae346", "auto"),
            14: ("AUTOID_PREFIX.HEAT_EXCHANGER", "equivalent", None, "sha256:3d33238b729bde87f712aca9da80093919f0b43c0880d602999714686ce32b2b", "sha256:15fdac6aa3c2870098875845fc83aba1fc0b6a7a1deee620a29bda24704548ed", "auto"),
            15: ("AUTOID_PREFIX.MATERIAL", "equivalent", None, "sha256:3aeea8d72293d62438ae13a2c57537dc478574fde9f1b591baab94f85dd2dc60", "sha256:e508f03bebcd401287170453961b635725b7f0fb2248a0e9b377548243daa47d", "auto"),
            16: ("AUTOID_PREFIX.PROFILE", "equivalent", None, "sha256:6215e1cc9f38731e1104d86f5b6d687b63a9c00f35c6ef4c07e2d1f9b5313f1f", "sha256:486ebba7356f58e01d1f834deae093f72221701ac6a2577e1c98eeefa34eecf0", "auto"),
            17: ("AUTOID_PREFIX.PV_PANEL", "equivalent", None, "sha256:b06fe3cb29b4b1fab699cbfb0de790244672b4fad79a155bc807dea55cee48ee", "sha256:81af3c23b8cfa3aa583ee86bf40b444c24ed94de1d6f5958a49289c1d425351a", "auto"),
            18: ("AUTOID_PREFIX.RULESET", "equivalent", None, "sha256:48b354d76e74a079e51e615a56c119961e382e6145958191f25c781ec2c9e965", "sha256:2f708270eabcb1873b15982921b88992d561a0f5be1a02bc394a14eda67f2b39", "auto"),
            19: ("AUTOID_PREFIX.SCHEDULE", "equivalent", None, "sha256:6a4a09541192424c81adad75b95e4ecff281d4316a9642e985f157ccce61ab55", "sha256:054b596bbb75b830e88b1d54ec77a138d1eab4b47f6db4aa3f24d0720bcb1dc4", "auto"),
            20: ("AUTOID_PREFIX.SOURCE_SYSTEM", "equivalent", None, "sha256:94db04792ebac4b485dc4750379547beb01116d42eff755b41e7e0d448dfd212", "sha256:2f4ca82faf4d8ec20013b34e493e9816947cbff83f121c3256fb27feb3d92cba", "auto"),
            21: ("AUTOID_PREFIX.SUPPLY_SYSTEM", "equivalent", None, "sha256:8269d73a695adb90cf51918761289a83a740e13be9540a52c5c8868f2fe700b0", "sha256:0783775d89f3599fc80be928fa0a23e1e8993a611feb17ea51e3f4e139fb9c83", "auto"),
            22: ("AUTOID_PREFIX.SURFACE", "equivalent", None, "sha256:0676d0a98539129914b681e798b057b39e6f488494c801e75d91d444014ac754", "sha256:07964ba62bcb30a3341d6a19e50012ed190ad7dc26f03f430302c967c6e4091b", "auto"),
            23: ("AUTOID_PREFIX.SURFACE_CONSTRUCTION", "equivalent", None, "sha256:bb50f2ec9729a2ab097de35594573b0e6f0166d0618238ba5b7e51d605d7fbc7", "sha256:ce6cba04d803585b8360a61dc19a4759e2ad2aa5f45a34d84cf4c9ccf1ac474f", "auto"),
            24: ("AUTOID_PREFIX.ZONE", "equivalent", None, "sha256:c0500033c21c3e4e5e8d92f6d4639faf962ec1d89a666a05cf4bddfc05b03fa5", "sha256:1a6e2def5fdc83b7a2fde8dc96e0d3b0bca8af48b32ff1405df9ccf6569203eb", "auto"),
            25: ("AUTOID_PREFIX.__format__", "equivalent", None, "sha256:a627c9b091c57e8e91bda29501e39587fa4128fec124360ffa395cc84b028174", "sha256:8f5fb836e849a9d24184c7e347f78a79312bb003d8746d3ce40cb3e3d0a1571e", "auto_format"),
            27: ("AUTOID_PREFIX.__str__", "equivalent", None, "sha256:3ee104cfac65d9513a3e97d4cc0c07581457c16ff078b76f4b4a4ab92c22c24c", "sha256:ad46a72c98ee8f526303e338271ae940b2fdd6bd1f3bdd06a8b7055247f6cb6e", "auto_format"),
            31: ("Directory", "exception", "embedded-explicit-native-resource-layout-5b876ad7", "sha256:96d49d0b47a0ad1c45328cbbb76b5d2e1baa51ce2f750513eef521a4e257ea4f", "sha256:43228d711c70b31774527ea28cea460b751c0410f40d745f30e68d720516cfa8", "embedded"),
            32: ("Directory.CONSTRUCTION_DIR", "exception", "embedded-native-construction-resources-91c573a0", "sha256:5601e4973e167f2cf635b09bd098b8da045523939e08b1706211a8b534a7c6f8", "sha256:f92ba3544e89fdd790dfe8539c5a8b9477e89f2d7a24501dc5f972f36142da16", "embedded"),
            33: ("Directory.PROFILE_DIR", "exception", "embedded-native-profile-resources-f65d5eae", "sha256:c5040c75bc3bbedd9f5528ad1b0820962bdcc558816eb3014a1a640846e6dea9", "sha256:82806be75ab97546a1de7d4b8bba87154043de51df0d412956f52ae36354e846", "embedded"),
            34: ("Directory.WEATHER_DATA_DIR", "exception", "caller-supplied-native-weather-data-root-8a5bf654", "sha256:c30aab809c885a5e192d5681f963e2b24b27ba40215e300da4967dee12d6701a", "sha256:587a80d5ad2e9d75fd2cb0c1bec530b1111acd2c7b30085bd83e8ca71631033f", "weather"),
            35: ("Directory.WEATHER_META_DIR", "exception", "embedded-native-weather-metadata-resources-15e81d1d", "sha256:8eed8cb92b6c4dfa07182888a2c27b6f1cfcf36b1a07cbca06962f0d15017754", "sha256:03f892f9f5caba0d9a5aba6a679eb18dba7f7ba3ee3351f302fc0ba494c0b018", "embedded"),
            36: ("PackageInfo", "exception", "static-native-simpledragon-package-information-aaf5b98d", "sha256:fe4d4276c13cd499976dd33417c45fc79d36d9506656fdb03b1073db3729f55a", "sha256:3b078749b64c67f6df80f9cfb5ca8bae07bb619c48bde3707d0d3d836e766557", "package"),
            37: ("PackageInfo.NAME", "exception", "native-simpledragon-package-name-537c8c3b", "sha256:184e11cb30735a077ff9d958a7773aa3a2c6ef862487054d29438b56882ad81a", "sha256:c5fd9d6624ce6b7659868db66ccec6f730501ef8030d57b5d001611765a19421", "package_name"),
            38: ("PackageInfo.REQUIRED_PYTHON", "exception", "compiled-simpledragon-target-framework-contract-cf74d0eb", "sha256:a45b04c19582cb9035964aceb6884fcf90a08480eb82bfa384fa905532037ecd", "sha256:84ef8847847386c99b3af975bda9c91814c4cef4cb40e095d79fddf41e8eaa84", "package"),
            39: ("PackageInfo.VERSION", "exception", "native-simpledragon-and-upstream-version-identity-a8260e5f", "sha256:a60e227a3d77b56dd90d1b9fa634d8291e3f18509e30c4dd26f37548e87509cc", "sha256:15d58234c6e87baf36b10b60476fa1ae06595c40c814f0567f9e3f823d6f38a9", "package_version"),
            58: ("SpecialTag", "exception", "immutable-native-special-tag-catalog-a66e2175", "sha256:c8f848c9aec6699e487b4b0866886dfa218a4075125e8833b0e7f7681da2e1f4", "sha256:f8a80fe6c2fe95142c72a30a4e794a2292fa32ff1762bfe1eaffdfed4a3391d8", "special"),
            59: ("SpecialTag.CLONE", "equivalent", None, "sha256:f10ee7f4a6d020f1afb791cdf3066590a4a18ca7390f36099874ec5f21349d54", "sha256:81eb7fbf2c1fdeca30becb4919cdee536481cacdd8bff72adcdece0da449c48e", "special"),
            60: ("SpecialTag.COOLROOF", "equivalent", None, "sha256:98c47babdc5247193f160c1ef8035134d049f4436ada946221c421a19f96547e", "sha256:ee5713704623715f0627b4fa08293c2ae8eb9379f2063668069ad42ec5e4a757", "special"),
            61: ("SpecialTag.DB", "equivalent", None, "sha256:5808de1507c015662fc179ce752174c57abc948bc4f15d7382370f8d4df66a55", "sha256:2339e20009695112c85305eae94449ce45669f19a83f45f156c142f66dfc257d", "special"),
            62: ("SpecialTag.FLIP", "equivalent", None, "sha256:df02a310048126e3a6222b35cfb4dac490167ab7bd8486e99d9d01af0f65c6fb", "sha256:996be98a93c9f4157aa7ceeca396c53fe8c1929f49736262f8e336aafa216b64", "special"),
            63: ("SpecialTag.SPECIAL", "equivalent", None, "sha256:66bb79bee5f9bd8a99280e2fd1c975d2edda8ed6ff26f5366e67c20c8c0a8963", "sha256:86dc4d3e380efe8c689e0a3551c381cf2dcf8c8cdf4be802bc8588db48d88955", "special"),
            64: ("SpecialTag.__format__", "equivalent", None, "sha256:6d5c42990ca2a5a741e08ddb21f1a878e3743266e9bb350c4e08348e4d7b1166", "sha256:127466bb510850f70b2d186e063e5b23d9e0ddbafade3da0fa26a90499954ec5", "special_format"),
            66: ("SpecialTag.__str__", "equivalent", None, "sha256:a56b63ef6e2feb46d2f49883fd884c227c18f74964f1b59dcb1181d1a195d3b9", "sha256:d6dd2872ca904aa23c2ab29095a30dcfe419479b0ca9ff78f8d79687bf6a4e7c", "special_format"),
        }
        identifier_implementations = {
            "auto": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "GonieGonie.SimpleDragon.AutoIdPrefix",
                "sha256:0dfe1e82e58c30dcbe9d5cc031363950a7b5c0ddd10c85c6ea29003eaf90d012",
            ),
            "auto_format": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "GonieGonie.SimpleDragon.AutoIdPrefix.ToString",
                "sha256:0dfe1e82e58c30dcbe9d5cc031363950a7b5c0ddd10c85c6ea29003eaf90d012",
            ),
            "embedded": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Data/SimpleDragonEmbeddedData.cs",
                "GonieGonie.SimpleDragon.SimpleDragonEmbeddedData",
                "sha256:76915a821bccc2dbc8e3f185c1faf6c3da07dfe64cd50301b336367d8c5d2d81",
            ),
            "weather": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Weather/WeatherDatabase.cs",
                "GonieGonie.SimpleDragon.WeatherSelection.ResolveEpwPath",
                "sha256:c7ddc71015eb375e56565a2898d7998cf865fb50d0c8626374f0f642644e9e98",
            ),
            "package": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/PackageInfo.cs",
                "GonieGonie.SimpleDragon.PackageInfo",
                "sha256:29de3c056446d3ad69084ae681d05a73b8185c881ab0e2d9863423e0ecf3c5f0",
            ),
            "package_name": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/PackageInfo.cs",
                "GonieGonie.SimpleDragon.PackageInfo.Name",
                "sha256:29de3c056446d3ad69084ae681d05a73b8185c881ab0e2d9863423e0ecf3c5f0",
            ),
            "package_version": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/PackageInfo.cs",
                "GonieGonie.SimpleDragon.PackageInfo.Version",
                "sha256:29de3c056446d3ad69084ae681d05a73b8185c881ab0e2d9863423e0ecf3c5f0",
            ),
            "special": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "GonieGonie.SimpleDragon.SpecialTag",
                "sha256:0dfe1e82e58c30dcbe9d5cc031363950a7b5c0ddd10c85c6ea29003eaf90d012",
            ),
            "special_format": (
                "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "GonieGonie.SimpleDragon.SpecialTag.ToString",
                "sha256:0dfe1e82e58c30dcbe9d5cc031363950a7b5c0ddd10c85c6ea29003eaf90d012",
            ),
        }
        identifier_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "IdentifierConventionsOracleParityTests.cs"
        )
        identifier_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.IdentifierConventionsOracleParityTests."
            "MatchesPinnedPythonIdentifierAndMetadataConventions"
        )
        identifier_test_hash = (
            "sha256:fbe6faced3c85dfe791627d637c92f0e1d1e49772b7d615565a29bf2161d32fc"
        )
        identifier_exceptions = {
            item.identifier: item for item in configuration.exceptions
        }
        self.assertEqual(
            (*range(10, 26), 27, *range(31, 40), *range(58, 65), 66),
            tuple(expected_identifier_conventions),
        )
        for index, (
            symbol,
            classification,
            exception_id,
            direct_hash,
            collector_hash,
            implementation_key,
        ) in expected_identifier_conventions.items():
            key = ("src/epsimple/constants.py", symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            self.assertEqual(key, inventory_symbol.key, key)
            entry = compatibility.matrix.entries[index]
            self.assertEqual(entry, by_key[key], key)
            self.assertEqual(classification, entry.classification, key)
            self.assertEqual(exception_id, entry.exception_id, key)
            assertion_id = (
                f"epsimple-identifier-conventions-{index}-"
                f"{inventory_symbol.symbol_hash.removeprefix('sha256:')[:8]}"
            )
            expected_evidence = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_evidence.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
                exception = identifier_exceptions[exception_id]
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
                self.assertIn(("engineering_result", entry.rationale), exception.effects)
            self.assertEqual(tuple(sorted(expected_evidence)), entry.evidence, key)

            evidence_entry = symbol_evidence.entries_by_key[key]
            expected_implementation = identifier_implementations[implementation_key]
            self.assertEqual(
                expected_implementation,
                (
                    evidence_entry.implementation_path,
                    evidence_entry.implementation_symbol,
                    evidence_entry.implementation_source_sha256,
                ),
                key,
            )
            self.assertEqual(1, len(evidence_entry.receipts), key)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, key)
            self.assertEqual(entry.rationale, receipt.assertion, key)
            self.assertIn(direct_hash, receipt.assertion, key)
            self.assertEqual(collector_hash, receipt.expected_output_sha256, key)
            self.assertEqual(identifier_test_path, receipt.test_path, key)
            self.assertEqual(identifier_test_symbol, receipt.test_symbol, key)
            self.assertEqual(identifier_test_hash, receipt.test_source_sha256, key)
            self.assertEqual("cross_language", receipt.verification_kind, key)
            self.assertEqual("passed", receipt.outcome, key)
            self.assertFalse(receipt.skipped, key)
            self.assertFalse(receipt.structural_only, key)
            self.assertFalse(receipt.claims_active_load, key)
            self.assertEqual("not_applicable", receipt.exercised_load, key)
        self.assertEqual(
            {"equivalent": 23, "exception": 11},
            {
                classification: sum(
                    values[1] == classification
                    for values in expected_identifier_conventions.values()
                )
                for classification in ("equivalent", "exception")
            },
        )
        for index, symbol in (
            (26, "AUTOID_PREFIX.__repr__"),
            (65, "SpecialTag.__repr__"),
        ):
            key = ("src/epsimple/constants.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, key)
            adjacent = by_key[key]
            self.assertEqual("out_of_scope", adjacent.classification, key)
            self.assertEqual(1, len(adjacent.evidence), key)
            self.assertTrue(
                adjacent.evidence[0].startswith("upstream/scope-decisions.json#"),
                key,
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key, key)

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
        expected_construction_core = {
            593: ("Construction", "exception", "immutable-validated-native-construction-451c832a", "dragon-construction-core-593-451c832a"),
            594: ("Construction.U", "equivalent", None, "dragon-construction-core-594-a29f2b11"),
            597: ("Construction.__init__", "exception", "typed-nonempty-native-construction-init-c99eac6b", "dragon-construction-core-597-c99eac6b"),
            598: ("Construction.heat_capacity", "equivalent", None, "dragon-construction-core-598-cebc9acb"),
            599: ("Construction.reversed", "exception", "immutable-validated-native-construction-reverse-f3f8b2b1", "dragon-construction-core-599-f3f8b2b1"),
            600: ("Construction.thickness", "equivalent", None, "dragon-construction-core-600-bfcb0ba0"),
            602: ("Glazing", "exception", "immutable-validated-native-glazing-5615eebb", "dragon-construction-core-602-5615eebb"),
            603: ("Glazing.G", "exception", "immutable-bounded-native-glazing-g-cb8ad4be", "dragon-construction-core-603-cb8ad4be"),
            604: ("Glazing.U", "exception", "immutable-finite-native-glazing-u-98ebe259", "dragon-construction-core-604-98ebe259"),
            605: ("Glazing.__init__", "exception", "validated-immutable-native-glazing-init-bfe7247a", "dragon-construction-core-605-bfe7247a"),
            609: ("Layer", "exception", "immutable-validated-native-layer-e6a3fe0d", "dragon-construction-core-609-e6a3fe0d"),
            610: ("Layer.U", "equivalent", None, "dragon-construction-core-610-be30888f"),
            613: ("Layer.__init__", "exception", "validated-immutable-native-layer-init-60e437a1", "dragon-construction-core-613-60e437a1"),
            614: ("Layer.heat_capacity", "equivalent", None, "dragon-construction-core-614-ab4d9ecc"),
            615: ("Layer.material", "exception", "immutable-required-native-layer-material-6454844c", "dragon-construction-core-615-6454844c"),
            616: ("Layer.thickness", "exception", "immutable-finite-native-layer-thickness-d7d789d7", "dragon-construction-core-616-d7d789d7"),
            618: ("Material", "exception", "immutable-validated-native-material-15ad6614", "dragon-construction-core-618-15ad6614"),
            620: ("Material.__init__", "exception", "validated-immutable-native-material-init-d78cab39", "dragon-construction-core-620-d78cab39"),
            621: ("Material.conductivity", "exception", "immutable-finite-native-material-conductivity-b733b56b", "dragon-construction-core-621-b733b56b"),
            622: ("Material.density", "exception", "immutable-finite-native-material-density-23136324", "dragon-construction-core-622-23136324"),
            623: ("Material.roughness", "exception", "immutable-strongly-typed-native-material-roughness-be23eedd", "dragon-construction-core-623-be23eedd"),
            624: ("Material.solar_absorptance", "exception", "immutable-finite-native-material-solar-absorptance-ae7ce02b", "dragon-construction-core-624-ae7ce02b"),
            625: ("Material.specific_heat", "exception", "immutable-finite-native-material-specific-heat-abf4a2ea", "dragon-construction-core-625-abf4a2ea"),
            626: ("Material.thermal_absorptance", "exception", "immutable-finite-native-material-thermal-absorptance-f17730ed", "dragon-construction-core-626-f17730ed"),
            627: ("Material.visible_absorptance", "exception", "immutable-finite-native-material-visible-absorptance-ecf6d77d", "dragon-construction-core-627-ecf6d77d"),
            628: ("MaterialRoughness", "exception", "strongly-typed-native-material-roughness-enum-fc281859", "dragon-construction-core-628-fc281859"),
            629: ("MaterialRoughness.MEDIUMROUGH", "equivalent", None, "dragon-construction-core-629-eda0d7d5"),
            630: ("MaterialRoughness.MEDIUMSMOOTH", "equivalent", None, "dragon-construction-core-630-6d574d54"),
            631: ("MaterialRoughness.ROUGH", "equivalent", None, "dragon-construction-core-631-beaf152f"),
            632: ("MaterialRoughness.SMOOTH", "equivalent", None, "dragon-construction-core-632-fce6deeb"),
            633: ("MaterialRoughness.VERYROUGH", "equivalent", None, "dragon-construction-core-633-9848a0c6"),
            634: ("MaterialRoughness.__str__", "equivalent", None, "dragon-construction-core-634-f40e4929"),
            635: ("NoMassConstruction", "exception", "immutable-validated-native-no-mass-construction-9dff867c", "dragon-construction-core-635-9dff867c"),
            636: ("NoMassConstruction.U", "exception", "immutable-finite-native-no-mass-u-98ebe259", "dragon-construction-core-636-98ebe259"),
            637: ("NoMassConstruction.__init__", "exception", "validated-immutable-native-no-mass-init-47497892", "dragon-construction-core-637-47497892"),
        }
        construction_core_implementation_files = {
            "Construction": (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Construction/Construction.cs",
                "sha256:935cfdeb3c6a5ced1c8fc0bbdb5ae91f46cc98f04ac74aa5ff0beadc3f6716a1",
            ),
            "Glazing": (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Construction/SimpleConstructions.cs",
                "sha256:4141d1125d33c40092caaf8b7e472bb50477a8c05b56b24ddf330ca72be22292",
            ),
            "Layer": (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Construction/Layer.cs",
                "sha256:bed26e36a5a65900291b62dd326d6175283dca3978ef0b2dc7093e9c052109fc",
            ),
            "Material": (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Construction/Material.cs",
                "sha256:f0bb5f09769036ce9f2611520f29a2a370bf405ecf10ded77665876f53195f07",
            ),
            "MaterialRoughness": (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Construction/MaterialRoughness.cs",
                "sha256:3e51b913e6323ed92af5d1121337ad9223113b349468866fa9e76c3f7634c6cf",
            ),
            "NoMassConstruction": (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Construction/SimpleConstructions.cs",
                "sha256:4141d1125d33c40092caaf8b7e472bb50477a8c05b56b24ddf330ca72be22292",
            ),
        }
        construction_core_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Construction/"
            "ConstructionCoreOracleParityTests.cs"
        )
        construction_core_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Construction."
            "ConstructionCoreOracleParityTests."
            "MatchesPinnedDragonConstructionCoreThroughTypedNativeRoutes"
        )
        construction_core_test_hash = (
            "sha256:45fd9efec179e9c5e0018b2ce28d5ece3cfdd60f09cc47ace17036348edd664f"
        )
        construction_core_exceptions = {
            item.identifier: item for item in configuration.exceptions
        }
        for index, (
            symbol,
            classification,
            exception_id,
            assertion_id,
        ) in expected_construction_core.items():
            key = ("src/idragon/dragon/construction.py", symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, inventory_symbol.key, symbol)
            self.assertEqual(entry, by_key[key], symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
                exception = construction_core_exceptions[exception_id]
                self.assertEqual(
                    key,
                    (exception.upstream_path, exception.upstream_symbol),
                    symbol,
                )
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(("engineering_result", entry.rationale), exception.effects)
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            owner = symbol.split(".", 1)[0]
            implementation_path, implementation_hash = (
                construction_core_implementation_files[owner]
            )
            self.assertEqual(implementation_path, evidence_entry.implementation_path, symbol)
            self.assertEqual(
                implementation_hash,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertTrue(
                evidence_entry.implementation_symbol.startswith(
                    f"GonieGonie.InvisibleDragon.Construction.{owner}"
                ),
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertEqual(1, receipt.assertion.count("sha256:"), symbol)
            self.assertIn("The canonical direct receipt is sha256:", receipt.assertion, symbol)
            self.assertEqual(construction_core_test_path, receipt.test_path, symbol)
            self.assertEqual(construction_core_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(construction_core_test_hash, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
        self.assertEqual(35, len(expected_construction_core))
        self.assertEqual(
            {"equivalent": 11, "exception": 24},
            {
                classification: sum(
                    values[1] == classification
                    for values in expected_construction_core.values()
                )
                for classification in ("equivalent", "exception")
            },
        )
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
        expected_adjacent_receipts.update(
            {
                index: values[3]
                for index, values in expected_construction_core.items()
            }
        )
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
            "ElectricRadiator": "exception",
            "ElectricRadiator.__init__": "exception",
            "ElectricRadiator.heatable": "equivalent",
            "SupplyGroup": "exception",
            "SupplyGroup.__init__": "exception",
            "SupplySystem": "equivalent",
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
            key = ("src/epsimple/constants.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual("exception", by_key[key].classification, symbol)
            self.assertEqual(exception_id, by_key[key].exception_id, symbol)
            self.assertIn(key, symbol_evidence.entries_by_key, symbol)
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
            "exception",
            by_key[("src/idragon/imugi.py", "IdfObjectList.set_wwr")].classification,
        )
        self.assertEqual(
            "typed-native-collection-adaptation-1215",
            by_key[("src/idragon/imugi.py", "IdfObjectList.set_wwr")].exception_id,
        )
        energy_model_key = ("src/idragon/dragon/model.py", "EnergyModel")
        energy_model = by_key[energy_model_key]
        self.assertEqual(energy_model_key, compatibility.inventory.symbols[815].key)
        self.assertEqual(energy_model, compatibility.matrix.entries[815])
        self.assertEqual("exception", energy_model.classification)
        self.assertEqual(
            "sealed-read-only-native-energy-model-class-a7582a41",
            energy_model.exception_id,
        )
        self.assertEqual(
            (
                "upstream/compatibility-exceptions.yml#sealed-read-only-native-energy-model-class-a7582a41",
                "upstream/symbol-evidence.json#dragon-model-energy-model-class-a7582a41",
            ),
            energy_model.evidence,
        )
        self.assertIn(
            "sha256:bc64f0fa26cb1a352a7a96a8333038ae7922d30cdd75c27a78a45649f9a9a96e",
            energy_model.rationale,
        )
        energy_model_exception = next(
            item
            for item in configuration.exceptions
            if item.identifier
            == "sealed-read-only-native-energy-model-class-a7582a41"
        )
        self.assertEqual(
            energy_model_key,
            (
                energy_model_exception.upstream_path,
                energy_model_exception.upstream_symbol,
            ),
        )
        self.assertEqual(
            compatibility.inventory.symbols[815].symbol_hash,
            energy_model_exception.upstream_symbol_hash,
        )
        self.assertIn(
            ("engineering_result", energy_model.rationale),
            energy_model_exception.effects,
        )
        energy_model_evidence = symbol_evidence.entries_by_key[energy_model_key]
        self.assertEqual(
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs",
            energy_model_evidence.implementation_path,
        )
        self.assertEqual(
            "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3",
            energy_model_evidence.implementation_source_sha256,
        )
        self.assertEqual(
            "GonieGonie.InvisibleDragon.Model.EnergyModel",
            energy_model_evidence.implementation_symbol,
        )
        self.assertEqual(1, len(energy_model_evidence.receipts))
        energy_model_receipt = energy_model_evidence.receipts[0]
        self.assertEqual(
            "dragon-model-energy-model-class-a7582a41",
            energy_model_receipt.identifier,
        )
        self.assertEqual(energy_model.rationale, energy_model_receipt.assertion)
        self.assertEqual(
            "sha256:c2774991d73b05365682f5b0154453cf664dd1bc5f0b2ed2293a29be60703288",
            energy_model_receipt.expected_output_sha256,
        )
        self.assertEqual(
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Model/"
            "EnergyModelClassOracleParityTests.cs",
            energy_model_receipt.test_path,
        )
        self.assertEqual(
            "GonieGonie.InvisibleDragon.Tests.Model.EnergyModelClassOracleParityTests."
            "MatchesPinnedPythonEnergyModelClassThroughTypedNativeRoutes",
            energy_model_receipt.test_symbol,
        )
        self.assertEqual(
            "sha256:7873e0930e752467003931854872b1006ce529265cdef5e62830ee1091f045c2",
            energy_model_receipt.test_source_sha256,
        )
        self.assertEqual("cross_language", energy_model_receipt.verification_kind)
        self.assertEqual("passed", energy_model_receipt.outcome)
        self.assertFalse(energy_model_receipt.skipped)
        self.assertFalse(energy_model_receipt.structural_only)
        self.assertFalse(energy_model_receipt.claims_active_load)
        self.assertEqual("not_applicable", energy_model_receipt.exercised_load)
        self.assertEqual(
            "exception",
            compatibility.matrix.entries[814].classification,
        )
        self.assertEqual(
            "immutable-validated-energy-model-construction",
            compatibility.matrix.entries[816].exception_id,
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
            "exception",
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

        model_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-model-core-oracle.json"
        )
        model_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "ModelCoreOracleParityTests.cs"
        )
        model_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.ModelCoreOracleParityTests."
            "MatchesPinnedModelCoreThroughProductionPublicRoutes"
        )
        model_fixture_sha256 = (
            "sha256:e5cfdc9ba823dc891693864051ffb8cbc06cd08137becef9d6c06fd0c2942cf6"
        )
        model_test_sha256 = (
            "sha256:3e31b35e5858eca554f60c6bdc1e391d5777faf954c8cd5cf25773bf9f72a02e"
        )
        model_fixture_bytes = model_fixture_path.read_bytes()
        self.assertEqual(
            model_fixture_sha256,
            "sha256:" + hashlib.sha256(model_fixture_bytes).hexdigest(),
        )
        model_fixture = json.loads(model_fixture_bytes.decode("utf-8"))
        model_contract = model_fixture["consumer_contract"]
        model_target_indices = (
            337,
            338,
            339,
            340,
            341,
            342,
            345,
            346,
            347,
            348,
            349,
            350,
            351,
            352,
            353,
            354,
            355,
            356,
            357,
            359,
            360,
            361,
            362,
            363,
            364,
            365,
            366,
            367,
            368,
            369,
            370,
            371,
            372,
            387,
            388,
        )
        model_targets = model_fixture["target_receipts"]
        self.assertEqual(
            model_target_indices,
            tuple(item["inventory_index"] for item in model_targets),
        )
        self.assertEqual(
            tuple(model_contract["target_symbols"]),
            tuple(item["symbol"] for item in model_targets),
        )
        self.assertEqual(35, model_contract["closure"]["target_count"])
        self.assertEqual(3, model_contract["closure"]["out_of_scope_exclusion_count"])
        self.assertEqual(
            14, model_contract["closure"]["deferred_greenretrofitresult_count"]
        )
        self.assertEqual(
            {"equivalent": 11, "exception": 24},
            {
                classification: sum(
                    value == classification
                    for value in model_contract["classifications"].values()
                )
                for classification in ("equivalent", "exception")
            },
        )

        model_case_by_symbol = {}
        for case in model_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, model_case_by_symbol)
                model_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(set(model_contract["target_symbols"]), set(model_case_by_symbol))

        model_test_bytes = (REPOSITORY_ROOT / model_test_path).read_bytes()
        self.assertEqual(
            model_test_sha256,
            "sha256:" + hashlib.sha256(model_test_bytes).hexdigest(),
        )
        model_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            model_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(model_direct_receipt_hash_block)
        assert model_direct_receipt_hash_block is not None
        model_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                model_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(35, len(model_direct_receipt_hashes))
        self.assertEqual(35, len(set(model_direct_receipt_hashes)))

        model_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            model_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(model_collector_output_hash_block)
        assert model_collector_output_hash_block is not None
        model_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                model_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(35, len(model_collector_output_hashes))
        self.assertEqual(35, len(set(model_collector_output_hashes)))

        model_native_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Model/"
            "GreenRetrofitModel.cs"
        )
        weather_native_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Weather/"
            "WeatherDatabase.cs"
        )
        reader_native_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmReader.cs"
        )
        conversion_native_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Conversion/"
            "GreenRetrofitConversion.cs"
        )
        failure_native_path = (
            "src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusFailure.cs"
        )
        runner_native_path = (
            "src/Shared/GonieGonie.EnergyPlus.Runtime/EnergyPlusRunner.cs"
        )
        model_members = {
            "GreenRetrofitModel": "GonieGonie.SimpleDragon.GreenRetrofitModel",
            "GreenRetrofitModel.__init__": "GonieGonie.SimpleDragon.GreenRetrofitModel",
            "GreenRetrofitModel.address": "GonieGonie.SimpleDragon.GreenRetrofitModel.Address",
            "GreenRetrofitModel.area": "GonieGonie.SimpleDragon.GreenRetrofitModel.Area",
            "GreenRetrofitModel.averaged_exteriorfloor_Uvalue": "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageExteriorFloorUValue",
            "GreenRetrofitModel.averaged_exteriorroof_Uvalue": "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageExteriorRoofUValue",
            "GreenRetrofitModel.averaged_exteriorwall_Uvalue": "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageExteriorWallUValue",
            "GreenRetrofitModel.averaged_infiltration": "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageInfiltration",
            "GreenRetrofitModel.averaged_lightdensity": "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageLightDensity",
            "GreenRetrofitModel.averaged_window_Uvalue": "GonieGonie.SimpleDragon.GreenRetrofitModel.AverageWindowUValue",
            "GreenRetrofitModel.climate": "GonieGonie.SimpleDragon.GreenRetrofitModel.Weather",
            "GreenRetrofitModel.exteriorfloors": "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorFloors",
            "GreenRetrofitModel.exteriorroofs": "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorRoofs",
            "GreenRetrofitModel.exteriorwalls": "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorWalls",
            "GreenRetrofitModel.exteriorwindows": "GonieGonie.SimpleDragon.GreenRetrofitModel.ExteriorWindows",
            "GreenRetrofitModel.get_unique_fenestration_constructions": "GonieGonie.SimpleDragon.GreenRetrofitModel.FenestrationConstructions",
            "GreenRetrofitModel.get_unique_materials": "GonieGonie.SimpleDragon.GreenRetrofitModel.Materials",
            "GreenRetrofitModel.get_unique_profiles": "GonieGonie.SimpleDragon.GreenRetrofitModel.Zones",
            "GreenRetrofitModel.get_unique_surface_constructions": "GonieGonie.SimpleDragon.GreenRetrofitModel.SurfaceConstructions",
            "GreenRetrofitModel.north_axis": "GonieGonie.SimpleDragon.GreenRetrofitModel.NorthAxis",
            "GreenRetrofitModel.source_system": "GonieGonie.SimpleDragon.GreenRetrofitModel.SourceSystems",
            "GreenRetrofitModel.terrain": "GonieGonie.SimpleDragon.GreenRetrofitModel.Weather",
            "GreenRetrofitModel.vintage": "GonieGonie.SimpleDragon.GreenRetrofitModel.Vintage",
            "GreenRetrofitModel.weather": "GonieGonie.SimpleDragon.GreenRetrofitModel.Weather",
        }

        def expected_model_implementation(symbol: str) -> tuple[str, str]:
            if symbol in {
                "ADDR_WEATHER_TABLE",
                "CLIMATE_TABLE",
                "InvalidAddressError",
                "address_to_weather",
            }:
                return (
                    weather_native_path,
                    "GonieGonie.SimpleDragon.WeatherDatabase.FindByAddress",
                )
            if symbol in {"EnergyPlusError", "EnergyPlusError.__init__"}:
                return (
                    failure_native_path,
                    "GonieGonie.EnergyPlus.Runtime.EnergyPlusFailure",
                )
            special_routes = {
                "GreenRetrofitModel.from_grjson": (
                    reader_native_path,
                    "GonieGonie.SimpleDragon.GrmReader.ReadFile",
                ),
                "GreenRetrofitModel.run": (
                    runner_native_path,
                    "GonieGonie.EnergyPlus.Runtime.EnergyPlusRunner.RunAsync",
                ),
                "GreenRetrofitModel.to_dragon": (
                    conversion_native_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
                "GreenRetrofitModel.to_idf": (
                    conversion_native_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.ToIdfDocument",
                ),
                "GreenRetrofitModel.weather_filepath": (
                    weather_native_path,
                    "GonieGonie.SimpleDragon.WeatherSelection.ResolveEpwPath",
                ),
            }
            if symbol in special_routes:
                return special_routes[symbol]
            return model_native_path, model_members[symbol]

        exceptions_by_id = {
            item.identifier: item for item in configuration.exceptions
        }
        for target, direct_receipt_hash, collector_output_hash in zip(
            model_targets,
            model_direct_receipt_hashes,
            model_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            classification = model_contract["classifications"][symbol]
            adaptation = model_contract["adaptations"].get(symbol)
            assertion_id = model_contract["assertion_ids"][symbol]
            native_route = model_contract["native_routes"][symbol]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(adaptation, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if adaptation is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{adaptation}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_model_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(model_test_path, receipt.test_path, symbol)
            self.assertEqual(model_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(model_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            case_code, case_id = model_case_by_symbol[symbol]
            for exact_binding in (
                model_fixture_sha256,
                model_test_sha256,
                "commit d48f97a",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                case_code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if adaptation is not None:
                exception = exceptions_by_id[adaptation]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(("engineering_result", entry.rationale), exception.effects)
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )

        model_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith("epsimple-model-core-")
        )
        self.assertEqual(35, len(model_evidence_entries))
        self.assertEqual(
            set(model_contract["target_symbols"]),
            {item.symbol for item in model_evidence_entries},
        )
        model_core_exception_ids = set(model_contract["adaptations"].values())
        self.assertEqual(
            set(model_contract["adaptations"])
            | {"GreenRetrofitModel._dragonize_surface"},
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in model_core_exception_ids
                or item.identifier == "immutable-conversion"
            },
        )
        self.assertEqual(
            545,
            sum(
                item.path != "src/epsimple/core/model.py"
                and not item.receipts[0].identifier.startswith(
                    "epsimple-hvac-enums-base-"
                )
                and not item.receipts[0].identifier.startswith(
                    "epsimple-hvac-thermal-source-"
                )
                and not item.receipts[0].identifier.startswith(
                    "epsimple-hvac-supply-system-"
                )
                and not item.receipts[0].identifier.startswith(
                    "epsimple-hvac-other-systems-"
                )
                and not item.receipts[0].identifier.startswith(
                    "dragon-hvac-source-tower-core-"
                )
                and not item.receipts[0].identifier.startswith(
                    "dragon-hvac-supply-core-"
                )
                and not item.receipts[0].identifier.startswith(
                    "dragon-hvac-appenders-controllers-"
                )
                and not item.receipts[0].identifier.startswith(
                    "dragon-hvac-misc-systems-core-"
                )
                and not item.receipts[0].identifier.startswith(
                    "imugi-idd-definitions-core-"
                )
                and not item.receipts[0].identifier.startswith(
                    "imugi-idd-schema-static-core-"
                )
                and not item.receipts[0].identifier.startswith(
                    "imugi-idf-object-core-"
                )
                and not item.receipts[0].identifier.startswith(
                    "imugi-idf-object-list-core-"
                )
                for item in symbol_evidence.entries
            ),
        )
        model_excluded = tuple(model_fixture["excluded_receipts"])
        model_deferred = tuple(model_fixture["deferred_receipts"])
        self.assertEqual((343, 344, 358), tuple(item["inventory_index"] for item in model_excluded))
        self.assertEqual(tuple(range(373, 387)), tuple(item["inventory_index"] for item in model_deferred))
        self.assertEqual(
            set(range(337, 389)),
            set(model_target_indices)
            | {item["inventory_index"] for item in model_excluded}
            | {item["inventory_index"] for item in model_deferred},
        )
        for receipt in model_excluded:
            index = receipt["inventory_index"]
            key = (receipt["path"], receipt["symbol"])
            self.assertEqual(key, compatibility.inventory.symbols[index].key)
            self.assertEqual(
                "out_of_scope", compatibility.matrix.entries[index].classification
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key)

        result_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-model-result-oracle.json"
        )
        result_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "ModelResultOracleParityTests.cs"
        )
        result_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.ModelResultOracleParityTests."
            "MatchesPinnedModelResultThroughProductionPublicRoutes"
        )
        result_fixture_sha256 = (
            "sha256:55d19ad2df41112fa0bb8bb1585f9e9822b68cfa4332c52b90e2aacbfd57c520"
        )
        result_test_sha256 = (
            "sha256:1cfc16db5802c21bafe6157b7c52b4fa490f66379ac7139223aecde9c45ebf02"
        )
        result_fixture_bytes = result_fixture_path.read_bytes()
        self.assertEqual(
            result_fixture_sha256,
            "sha256:" + hashlib.sha256(result_fixture_bytes).hexdigest(),
        )
        result_fixture = json.loads(result_fixture_bytes.decode("utf-8"))
        result_contract = result_fixture["consumer_contract"]
        result_target_indices = tuple(range(373, 387))
        result_targets = result_fixture["target_receipts"]
        self.assertEqual(
            result_target_indices,
            tuple(item["inventory_index"] for item in result_targets),
        )
        self.assertEqual(
            tuple(result_contract["closure"]["target_symbols"]),
            tuple(item["symbol"] for item in result_targets),
        )
        self.assertEqual(14, result_contract["closure"]["target_count"])
        self.assertEqual(11, result_contract["case_count"])
        self.assertEqual(11, len(result_fixture["cases"]))
        self.assertEqual(
            {"equivalent": 9, "exception": 5},
            result_contract["classification_counts"],
        )
        self.assertTrue(
            result_contract["closure"]["exact_one_case_target_partition"]
        )
        self.assertTrue(result_contract["closure"]["full_model_source_partition"])

        result_case_by_symbol = {}
        for case in result_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, result_case_by_symbol)
                result_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(result_contract["closure"]["target_symbols"]),
            set(result_case_by_symbol),
        )
        self.assertEqual(
            result_contract["coverage_by_symbol"],
            {
                symbol: case_id
                for symbol, (_, case_id) in result_case_by_symbol.items()
            },
        )

        result_test_bytes = (REPOSITORY_ROOT / result_test_path).read_bytes()
        self.assertEqual(
            result_test_sha256,
            "sha256:" + hashlib.sha256(result_test_bytes).hexdigest(),
        )
        result_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            result_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(result_direct_receipt_hash_block)
        assert result_direct_receipt_hash_block is not None
        result_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                result_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(14, len(result_direct_receipt_hashes))
        self.assertEqual(14, len(set(result_direct_receipt_hashes)))

        result_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            result_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(result_collector_output_hash_block)
        assert result_collector_output_hash_block is not None
        result_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                result_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(14, len(result_collector_output_hashes))
        self.assertEqual(14, len(set(result_collector_output_hashes)))

        result_models_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/"
            "GreenRetrofitResultModels.cs"
        )
        result_builder_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/"
            "GreenRetrofitResultBuilder.cs"
        )
        result_writer_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Results/GrrWriter.cs"
        )
        result_implementations = {
            "GreenRetrofitResult": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult",
            ),
            "GreenRetrofitResult.VALID_DIGITS": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult.ValidDigits",
            ),
            "GreenRetrofitResult.__init__": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult.FromSiteUses",
            ),
            "GreenRetrofitResult.area": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult.TotalArea",
            ),
            "GreenRetrofitResult.calc_domestic_hotwater_site_energy": (
                result_builder_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.get_dhw_servers": (
                result_builder_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.get_domestic_hotwater_energy": (
                result_builder_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.summarize": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult.PerAreaSummaries",
            ),
            "GreenRetrofitResult.to_co2": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult.Carbon",
            ),
            "GreenRetrofitResult.to_cost": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult.Cost",
            ),
            "GreenRetrofitResult.to_dict": (
                result_writer_path,
                "GonieGonie.SimpleDragon.GrrWriter.Serialize",
            ),
            "GreenRetrofitResult.to_site_uses": (
                result_builder_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.to_source_uses": (
                result_models_path,
                "GonieGonie.SimpleDragon.GreenRetrofitResult.SourceUses",
            ),
            "GreenRetrofitResult.write": (
                result_writer_path,
                "GonieGonie.SimpleDragon.GrrWriter.WriteFile",
            ),
        }
        result_exception_ids = {
            result_contract["adaptations"][symbol]
            for symbol, classification in result_contract["classifications"].items()
            if classification == "exception"
        }
        self.assertEqual(5, len(result_exception_ids))

        for target, direct_receipt_hash, collector_output_hash in zip(
            result_targets,
            result_direct_receipt_hashes,
            result_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            classification = result_contract["classifications"][symbol]
            adaptation_family = result_contract["adaptations"][symbol]
            exception_id = (
                adaptation_family if classification == "exception" else None
            )
            assertion_id = result_contract["assertion_ids"][symbol]
            native_route = result_contract["native_routes"][symbol]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = result_implementations[symbol]
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(result_test_path, receipt.test_path, symbol)
            self.assertEqual(result_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(result_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            case_code, case_id = result_case_by_symbol[symbol]
            for exact_binding in (
                result_fixture_sha256,
                result_test_sha256,
                "commit 61bb21b",
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                adaptation_family,
                case_code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(("engineering_result", entry.rationale), exception.effects)
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )
            else:
                self.assertNotIn(adaptation_family, exceptions_by_id, symbol)

        result_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith("epsimple-model-result-")
        )
        self.assertEqual(14, len(result_evidence_entries))
        self.assertEqual(
            set(result_contract["closure"]["target_symbols"]),
            {item.symbol for item in result_evidence_entries},
        )
        self.assertEqual(
            {
                symbol
                for symbol, classification in result_contract["classifications"].items()
                if classification == "exception"
            },
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in result_exception_ids
            },
        )
        self.assertEqual(
            545,
            len(symbol_evidence.entries)
            - len(model_evidence_entries)
            - len(result_evidence_entries)
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-enums-base-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-thermal-source-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-supply-system-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-other-systems-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-source-tower-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-supply-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-appenders-controllers-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-misc-systems-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-definitions-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-schema-static-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-list-core-"
                )
                for item in symbol_evidence.entries
            ),
        )
        self.assertEqual(
            317,
            sum(
                item.identifier
                not in model_core_exception_ids | result_exception_ids
                and item.upstream_path != "src/epsimple/core/hvac.py"
                and item.identifier not in source_tower_exception_ids
                and item.identifier not in supply_core_exception_ids
                and item.identifier not in appender_controller_exception_ids
                and item.identifier not in misc_systems_exception_ids
                and item.identifier not in imugi_idd_definitions_exception_ids
                and item.identifier not in imugi_idd_schema_static_exception_ids
                and item.identifier not in imugi_idf_object_exception_ids
                and item.identifier not in imugi_idf_object_list_exception_ids
                for item in configuration.exceptions
            ),
        )
        self.assertEqual(
            tuple(result_contract["closure"]["adjacent_indices"]),
            tuple(
                sorted(
                    model_target_indices
                    + tuple(item["inventory_index"] for item in model_excluded)
                )
            ),
        )
        self.assertEqual(
            set(range(337, 389)),
            set(result_target_indices)
            | set(result_contract["closure"]["adjacent_indices"]),
        )
        self.assertEqual(
            {"equivalent": 11, "exception": 24, "out_of_scope": 3},
            {
                classification: sum(
                    compatibility.matrix.entries[index].classification
                    == classification
                    for index in result_contract["closure"]["adjacent_indices"]
                )
                for classification in (
                    "equivalent",
                    "exception",
                    "out_of_scope",
                )
            },
        )

        shape_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-shape-core-oracle.json"
        )
        shape_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "ShapeCoreOracleParityTests.cs"
        )
        shape_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.ShapeCoreOracleParityTests."
            "MatchesPinnedShapeCoreThroughProductionPublicRoutes"
        )
        shape_fixture_sha256 = (
            "sha256:802bcf3d1bc05828329a659ec9013c498325ea5be8f647975dcbb4cb3eee2ba5"
        )
        shape_test_sha256 = (
            "sha256:3c74009029f33d199702e6dc9eaab1bb10bf88c6a14b53279be33751115fcb96"
        )
        shape_fixture_bytes = shape_fixture_path.read_bytes()
        self.assertEqual(
            shape_fixture_sha256,
            "sha256:" + hashlib.sha256(shape_fixture_bytes).hexdigest(),
        )
        shape_fixture = json.loads(shape_fixture_bytes.decode("utf-8"))
        shape_contract = shape_fixture["consumer_contract"]
        shape_target_indices = (
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
        shape_excluded_indices = (416, 425, 427, 428, 450)
        self.assertEqual(
            shape_target_indices,
            tuple(shape_contract["closure"]["target_indices"]),
        )
        self.assertEqual(
            shape_excluded_indices,
            tuple(shape_contract["closure"]["excluded_indices"]),
        )
        self.assertEqual(
            {"equivalent": 33, "exception": 20},
            shape_contract["classification_counts"],
        )
        shape_targets = shape_fixture["target_receipts"]
        self.assertEqual(
            shape_target_indices,
            tuple(item["inventory_index"] for item in shape_targets),
        )
        self.assertEqual(
            tuple(shape_contract["target_symbols"]),
            tuple(item["symbol"] for item in shape_targets),
        )
        shape_case_by_symbol = {
            symbol: (case["code"], case["id"])
            for case in shape_fixture["cases"]
            for symbol in case["target_symbols"]
        }
        self.assertEqual(53, len(shape_case_by_symbol))

        shape_test_bytes = (REPOSITORY_ROOT / shape_test_path).read_bytes()
        self.assertEqual(
            shape_test_sha256,
            "sha256:" + hashlib.sha256(shape_test_bytes).hexdigest(),
        )
        shape_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            shape_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(shape_direct_receipt_hash_block)
        assert shape_direct_receipt_hash_block is not None
        shape_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                shape_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(53, len(shape_direct_receipt_hashes))
        self.assertEqual(53, len(set(shape_direct_receipt_hashes)))

        shape_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            shape_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(shape_collector_output_hash_block)
        assert shape_collector_output_hash_block is not None
        shape_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                shape_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(53, len(shape_collector_output_hashes))
        self.assertEqual(53, len(set(shape_collector_output_hashes)))

        fenestration_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Fenestration.cs"
        )
        surface_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Surface.cs"
        )
        zone_path = "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Shape/Zone.cs"
        reader_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmReader.cs"
        )
        writer_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmWriter.cs"
        )
        converter_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Conversion/"
            "GreenRetrofitConversion.cs"
        )
        model_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Model/GreenRetrofitModel.cs"
        )

        def expected_shape_implementation(symbol: str) -> tuple[str, str]:
            if symbol == "BlindType":
                return fenestration_path, "GonieGonie.SimpleDragon.BlindType"
            if symbol == "BlindType.SHADE":
                return fenestration_path, "GonieGonie.SimpleDragon.BlindType.Shade"
            if symbol == "BlindType.VENETIAN":
                return fenestration_path, "GonieGonie.SimpleDragon.BlindType.Venetian"
            if symbol == "BlindType.__str__":
                return writer_path, "GonieGonie.SimpleDragon.GrmWriter.Serialize"
            if symbol.endswith(".from_json"):
                return reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    converter_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if symbol in {"Door", "Fenestration", "GlassDoor", "Window"}:
                return fenestration_path, "GonieGonie.SimpleDragon.Fenestration"
            if symbol.startswith(("Door.", "Fenestration.", "Window.")):
                member = {
                    "construction": "Construction",
                    "ID": "Id",
                    "__deepcopy__": "Fenestration",
                    "__init__": "Fenestration",
                    "blind": "Blind",
                }[symbol.split(".", 1)[1]]
                return (
                    fenestration_path,
                    "GonieGonie.SimpleDragon.Fenestration." + member,
                )
            if symbol == "Surface":
                return surface_path, "GonieGonie.SimpleDragon.Surface"
            if symbol.startswith("Surface."):
                member_name = symbol.split(".", 1)[1]
                if member_name == "get_unique_fenestration_constructions":
                    return (
                        model_path,
                        "GonieGonie.SimpleDragon.GreenRetrofitModel."
                        "FenestrationConstructions",
                    )
                member = {
                    "ID": "Id",
                    "__deepcopy__": "Surface",
                    "__init__": "Surface",
                    "adjacent_zone": "AdjacentZoneId",
                    "area": "Area",
                    "azimuth": "Azimuth",
                    "boundary": "BoundaryCondition",
                    "construction": "Construction",
                    "flip": "Flip",
                    "num_doors": "DoorCount",
                    "num_windows": "WindowCount",
                    "reflectance": "CoolRoofReflectance",
                    "type": "Type",
                }[member_name]
                return surface_path, "GonieGonie.SimpleDragon.Surface." + member
            if symbol == "Zone":
                return zone_path, "GonieGonie.SimpleDragon.Zone"
            if symbol.startswith("Zone."):
                member_name = symbol.split(".", 1)[1]
                catalogs = {
                    "get_unique_fenestration_constructions": (
                        "FenestrationConstructions"
                    ),
                    "get_unique_materials": "Materials",
                    "get_unique_surface_constructions": "SurfaceConstructions",
                }
                if member_name in catalogs:
                    return (
                        model_path,
                        "GonieGonie.SimpleDragon.GreenRetrofitModel."
                        + catalogs[member_name],
                    )
                member = {
                    "ID": "Id",
                    "__init__": "Zone",
                    "area": "Area",
                    "cooling_supply_systems": "CoolingSupplySystems",
                    "heating_supply_systems": "HeatingSupplySystems",
                    "height": "Height",
                    "infiltration": "Infiltration",
                    "supply_systems": "SupplySystems",
                }[member_name]
                return zone_path, "GonieGonie.SimpleDragon.Zone." + member
            self.fail(f"Missing expected shape implementation for {symbol}")
            raise AssertionError(symbol)

        exceptions_by_id = {
            item.identifier: item for item in configuration.exceptions
        }
        shape_exception_symbols = set(shape_contract["adaptations"])
        for target, direct_receipt_hash, collector_output_hash in zip(
            shape_targets,
            shape_direct_receipt_hashes,
            shape_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            classification = shape_contract["classifications"][symbol]
            adaptation = shape_contract["adaptations"].get(symbol)
            assertion_id = shape_contract["assertion_ids"][symbol]
            native_route = shape_contract["native_routes"][symbol]
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(adaptation, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if adaptation is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{adaptation}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_shape_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(shape_test_path, receipt.test_path, symbol)
            self.assertEqual(shape_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(shape_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            case_code, case_id = shape_case_by_symbol[symbol]
            for exact_binding in (
                shape_fixture_sha256,
                shape_test_sha256,
                "commit a198a7c",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                case_code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if adaptation is not None:
                exception = exceptions_by_id[adaptation]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(("engineering_result", entry.rationale), exception.effects)
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )

        shape_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.path == "src/epsimple/core/shape.py"
        )
        self.assertEqual(53, len(shape_evidence_entries))
        self.assertEqual(
            set(shape_contract["target_symbols"]),
            {item.symbol for item in shape_evidence_entries},
        )
        self.assertEqual(
            20,
            sum(
                item.classification == "exception"
                for item in compatibility.matrix.entries[405:463]
            ),
        )
        self.assertEqual(
            shape_exception_symbols,
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.upstream_path == "src/epsimple/core/shape.py"
            },
        )
        for index, symbol in zip(
            shape_excluded_indices,
            shape_contract["closure"]["excluded_symbols"],
            strict=True,
        ):
            key = ("src/epsimple/core/shape.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(
                "out_of_scope",
                compatibility.matrix.entries[index].classification,
                symbol,
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key, symbol)

        construction_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-construction-core-oracle.json"
        )
        construction_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "ConstructionCoreOracleParityTests.cs"
        )
        construction_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.ConstructionCoreOracleParityTests."
            "MatchesPinnedConstructionCoreThroughProductionPublicRoutes"
        )
        construction_fixture_sha256 = (
            "sha256:d4e9421c40c39dfaef948054798b03fb046fa31d1a5742cb8a53484c87d819f9"
        )
        construction_test_sha256 = (
            "sha256:d84adeb2aede8e6cb0c42e5d132b7d491cca6abd0fb69697d19c897db1ef0d98"
        )
        construction_fixture_bytes = construction_fixture_path.read_bytes()
        self.assertEqual(
            construction_fixture_sha256,
            "sha256:" + hashlib.sha256(construction_fixture_bytes).hexdigest(),
        )
        construction_fixture = json.loads(construction_fixture_bytes.decode("utf-8"))
        construction_contract = construction_fixture["consumer_contract"]
        construction_target_indices = (
            75,
            76,
            79,
            82,
            83,
            84,
            85,
            86,
            87,
            88,
            89,
            90,
            91,
            94,
            97,
            98,
            99,
            100,
            101,
            102,
            103,
            104,
            105,
            106,
            107,
            108,
            109,
            110,
            111,
            112,
            113,
            114,
            117,
            120,
            121,
            122,
            123,
            124,
            125,
            126,
            127,
            128,
            129,
            130,
            131,
            132,
            133,
            134,
        )
        construction_excluded_indices = (
            77,
            78,
            80,
            81,
            92,
            93,
            95,
            96,
            115,
            116,
            118,
            119,
        )
        self.assertEqual(
            construction_target_indices,
            tuple(construction_contract["closure"]["target_indices"]),
        )
        self.assertEqual(
            construction_excluded_indices,
            tuple(construction_contract["closure"]["excluded_indices"]),
        )
        self.assertEqual(
            {"equivalent": 7, "exception": 41},
            construction_contract["classification_counts"],
        )
        construction_targets = construction_fixture["target_receipts"]
        self.assertEqual(
            construction_target_indices,
            tuple(item["inventory_index"] for item in construction_targets),
        )
        self.assertEqual(
            tuple(construction_contract["closure"]["target_symbols"]),
            tuple(item["symbol"] for item in construction_targets),
        )
        construction_case_by_symbol = {
            symbol: (case["code"], case["id"])
            for case in construction_fixture["cases"]
            for symbol in case["target_symbols"]
        }
        self.assertEqual(48, len(construction_case_by_symbol))

        construction_test_bytes = (
            REPOSITORY_ROOT / construction_test_path
        ).read_bytes()
        self.assertEqual(
            construction_test_sha256,
            "sha256:" + hashlib.sha256(construction_test_bytes).hexdigest(),
        )
        construction_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            construction_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(construction_direct_receipt_hash_block)
        assert construction_direct_receipt_hash_block is not None
        construction_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                construction_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(48, len(construction_direct_receipt_hashes))
        self.assertEqual(48, len(set(construction_direct_receipt_hashes)))

        construction_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            construction_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(construction_collector_output_hash_block)
        assert construction_collector_output_hash_block is not None
        construction_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                construction_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(48, len(construction_collector_output_hashes))
        self.assertEqual(48, len(set(construction_collector_output_hashes)))

        material_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/Material.cs"
        )
        fenestration_construction_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/"
            "FenestrationConstruction.cs"
        )
        surface_construction_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/"
            "SurfaceConstruction.cs"
        )
        construction_database_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Construction/"
            "ConstructionDatabases.cs"
        )
        database_aggregate_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Data/"
            "SimpleDragonDatabase.cs"
        )

        def expected_construction_implementation(symbol: str) -> tuple[str, str]:
            if symbol == "FenestrationConstruction":
                return (
                    fenestration_construction_path,
                    "GonieGonie.SimpleDragon.FenestrationConstruction",
                )
            if symbol.startswith("FenestrationConstruction."):
                member_name = symbol.split(".", 1)[1]
                routed = {
                    "from_json": (reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"),
                    "to_dict": (writer_path, "GonieGonie.SimpleDragon.GrmWriter.Serialize"),
                    "to_dragon": (
                        converter_path,
                        "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                    ),
                    "get_DB": (
                        construction_database_path,
                        "GonieGonie.SimpleDragon.FenestrationConstructionDatabase.Find",
                    ),
                    "load_DB": (
                        database_aggregate_path,
                        "GonieGonie.SimpleDragon.SimpleDragonDatabase.LoadEmbedded",
                    ),
                }
                if member_name in routed:
                    return routed[member_name]
                native_member = {
                    "ID": "Id",
                    "__init__": None,
                    "g": "SolarHeatGainCoefficient",
                    "is_transparent": "IsTransparent",
                    "u": "UValue",
                }[member_name]
                suffix = "" if native_member is None else "." + native_member
                return (
                    fenestration_construction_path,
                    "GonieGonie.SimpleDragon.FenestrationConstruction" + suffix,
                )
            if symbol == "Material":
                return material_path, "GonieGonie.SimpleDragon.Material"
            if symbol.startswith("Material."):
                member_name = symbol.split(".", 1)[1]
                routed = {
                    "from_json": (reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"),
                    "to_dict": (writer_path, "GonieGonie.SimpleDragon.GrmWriter.Serialize"),
                    "to_dragon": (
                        converter_path,
                        "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                    ),
                    "get_DB": (
                        construction_database_path,
                        "GonieGonie.SimpleDragon.MaterialDatabase.Find",
                    ),
                    "load_DB": (
                        database_aggregate_path,
                        "GonieGonie.SimpleDragon.SimpleDragonDatabase.LoadEmbedded",
                    ),
                }
                if member_name in routed:
                    return routed[member_name]
                native_member = {
                    "ID": "Id",
                    "__init__": None,
                    "conductivity": "Conductivity",
                    "density": "Density",
                    "specific_heat": "SpecificHeat",
                }[member_name]
                suffix = "" if native_member is None else "." + native_member
                return material_path, "GonieGonie.SimpleDragon.Material" + suffix
            special_routes = {
                "OpenConstruction": (
                    surface_path,
                    "GonieGonie.SimpleDragon.SurfaceConstructionReferenceKind.Open",
                ),
                "OpenConstruction.ID": (
                    surface_path,
                    "GonieGonie.SimpleDragon.Surface.ConstructionId",
                ),
                "OpenConstruction.to_dragon": (
                    converter_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
                "SpecialConstruction": (
                    surface_path,
                    "GonieGonie.SimpleDragon.SurfaceConstructionReferenceKind",
                ),
                "SpecialConstruction.__new__": (
                    surface_path,
                    "GonieGonie.SimpleDragon.Surface.ConstructionReferenceKind",
                ),
                "SpecialConstruction.get_unique_materials": (
                    converter_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
                "SpecialConstruction.reversed": (
                    surface_path,
                    "GonieGonie.SimpleDragon.Surface.Flip",
                ),
                "UnknownConstruction": (
                    surface_path,
                    "GonieGonie.SimpleDragon.SurfaceConstructionReferenceKind.Unknown",
                ),
                "UnknownConstruction.ID": (
                    surface_path,
                    "GonieGonie.SimpleDragon.Surface.ConstructionId",
                ),
                "UnknownConstruction.to_dragon": (
                    converter_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
            }
            if symbol in special_routes:
                return special_routes[symbol]
            if symbol == "SurfaceConstruction":
                return (
                    surface_construction_path,
                    "GonieGonie.SimpleDragon.SurfaceConstruction",
                )
            if symbol.startswith("SurfaceConstruction."):
                member_name = symbol.split(".", 1)[1]
                routed = {
                    "from_json": (reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"),
                    "to_dict": (writer_path, "GonieGonie.SimpleDragon.GrmWriter.Serialize"),
                    "to_dragon": (
                        converter_path,
                        "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                    ),
                    "get_DB": (
                        construction_database_path,
                        "GonieGonie.SimpleDragon.SurfaceConstructionDatabase.Find",
                    ),
                    "get_regulated_construction": (
                        construction_database_path,
                        "GonieGonie.SimpleDragon.SurfaceConstructionDatabase.FindRegulated",
                    ),
                    "load_DB": (
                        database_aggregate_path,
                        "GonieGonie.SimpleDragon.SimpleDragonDatabase.LoadEmbedded",
                    ),
                }
                if member_name in routed:
                    return routed[member_name]
                native_member = {
                    "ID": "Id",
                    "U_internal": "InternalUValue",
                    "__init__": None,
                    "create_simply": "CreateSimple",
                    "depth": "Depth",
                    "get_U": "GetUValue",
                    "get_unique_materials": "Layers",
                    "heat_capacity": "HeatCapacity",
                    "reversed": "Reverse",
                }[member_name]
                suffix = "" if native_member is None else "." + native_member
                return (
                    surface_construction_path,
                    "GonieGonie.SimpleDragon.SurfaceConstruction" + suffix,
                )
            self.fail(f"Missing expected construction implementation for {symbol}")
            raise AssertionError(symbol)

        construction_exception_symbols = {
            symbol
            for symbol, classification in construction_contract[
                "classifications"
            ].items()
            if classification == "exception"
        }
        construction_exception_ids = {
            construction_contract["adaptations"][symbol]
            for symbol in construction_exception_symbols
        }
        self.assertEqual(41, len(construction_exception_ids))
        self.assertTrue(
            construction_exception_ids.isdisjoint(
                {
                    construction_contract["adaptations"][symbol]
                    for symbol, classification in construction_contract[
                        "classifications"
                    ].items()
                    if classification == "equivalent"
                }
            )
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            construction_targets,
            construction_direct_receipt_hashes,
            construction_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            classification = construction_contract["classifications"][symbol]
            adaptation = construction_contract["adaptations"][symbol]
            assertion_id = construction_contract["assertion_ids"][symbol]
            native_route = construction_contract["native_routes"][symbol]
            exception_id = adaptation if classification == "exception" else None
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            implementation_path, implementation_symbol = (
                expected_construction_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            self.assertEqual(
                implementation_path, evidence_entry.implementation_path, symbol
            )
            self.assertEqual(
                implementation_symbol, evidence_entry.implementation_symbol, symbol
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(construction_test_path, receipt.test_path, symbol)
            self.assertEqual(construction_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(construction_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            case_code, case_id = construction_case_by_symbol[symbol]
            for exact_binding in (
                construction_fixture_sha256,
                construction_test_sha256,
                "commit 3053e74",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                adaptation,
                native_route,
                case_code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale), exception.effects
                )
                self.assertEqual(
                    "accepted-native-api-adaptation", exception.approval, symbol
                )
            else:
                self.assertNotIn(adaptation, exceptions_by_id, symbol)

        construction_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.path == "src/epsimple/core/construction.py"
        )
        self.assertEqual(48, len(construction_evidence_entries))
        self.assertEqual(
            set(construction_contract["closure"]["target_symbols"]),
            {item.symbol for item in construction_evidence_entries},
        )
        self.assertEqual(
            construction_exception_symbols,
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.upstream_path == "src/epsimple/core/construction.py"
            },
        )
        self.assertEqual(
            construction_exception_ids,
            {
                item.identifier
                for item in configuration.exceptions
                if item.upstream_path == "src/epsimple/core/construction.py"
            },
        )
        for index, symbol in zip(
            construction_excluded_indices,
            construction_contract["closure"]["excluded_symbols"],
            strict=True,
        ):
            key = ("src/epsimple/core/construction.py", symbol)
            self.assertEqual(key, compatibility.inventory.symbols[index].key, symbol)
            self.assertEqual(
                "out_of_scope",
                compatibility.matrix.entries[index].classification,
                symbol,
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key, symbol)

        hvac_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-hvac-enums-base-oracle.json"
        )
        hvac_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_epsimple_hvac_enums_base_oracle.py"
        )
        hvac_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_epsimple_hvac_enums_base_oracle.py"
        )
        hvac_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "HvacEnumsBaseOracleParityTests.cs"
        )
        hvac_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.HvacEnumsBaseOracleParityTests."
            "MatchesPinnedHvacEnumsBaseThroughProductionPublicRoutes"
        )
        hvac_fixture_sha256 = (
            "sha256:5bf5e8f88a2050232aa45e79c48894a54897eea57cddaf75697ab914d9715b7c"
        )
        hvac_generator_sha256 = (
            "sha256:eaa5691d29c341844097c8690f0e12970824494f1e00e8287811b7876ba3df0d"
        )
        hvac_validator_sha256 = (
            "sha256:b6331cef12c6ff6809c4beb569f73ab528b04dde3f8f032db6651c5d418d0428"
        )
        hvac_test_sha256 = (
            "sha256:5f4360181c32738c4c742d365529a3b6a07206cde0a68de573c5a65ed59a92d3"
        )
        for pinned_path, expected_sha256 in (
            (hvac_fixture_path, hvac_fixture_sha256),
            (hvac_generator_path, hvac_generator_sha256),
            (hvac_validator_path, hvac_validator_sha256),
            (REPOSITORY_ROOT / hvac_test_path, hvac_test_sha256),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        hvac_fixture = json.loads(hvac_fixture_path.read_text(encoding="utf-8"))
        hvac_contract = hvac_fixture["consumer_contract"]
        hvac_target_indices = (
            *range(185, 199),
            *range(240, 248),
            *range(267, 271),
            319,
            320,
        )
        thermal_target_indices = (
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
        )
        supply_target_indices = (
            147,
            148,
            151,
            *range(154, 157),
            209,
            210,
            213,
            *range(216, 219),
            219,
            220,
            223,
            *range(226, 230),
            230,
            231,
            234,
            *range(237, 240),
            271,
            272,
            275,
            *range(278, 283),
            296,
            297,
            300,
            *range(303, 308),
            308,
            309,
            312,
            *range(315, 319),
            *range(321, 325),
        )
        other_target_indices = (
            283,
            284,
            287,
            *range(290, 296),
            325,
            326,
            329,
            *range(332, 337),
        )
        self.assertEqual(47, len(thermal_target_indices))
        self.assertEqual(52, len(supply_target_indices))
        self.assertEqual(17, len(other_target_indices))
        hvac_targets = hvac_fixture["target_receipts"]
        self.assertEqual(
            hvac_target_indices,
            tuple(item["inventory_index"] for item in hvac_targets),
        )
        self.assertEqual(
            tuple(hvac_contract["target_symbols"]),
            tuple(item["symbol"] for item in hvac_targets),
        )
        self.assertEqual(6, hvac_contract["case_count"])
        self.assertEqual(6, len(hvac_fixture["cases"]))
        self.assertEqual(
            {"equivalent": 18, "exception": 10},
            hvac_contract["classification_counts"],
        )
        self.assertEqual(28, hvac_contract["closure"]["target_count"])
        self.assertEqual(58, hvac_contract["closure"]["excluded_count"])
        self.assertEqual(116, hvac_contract["closure"]["deferred_count"])
        self.assertTrue(hvac_contract["closure"]["full_source_partition"])
        self.assertTrue(
            hvac_contract["closure"]["exact_one_case_target_partition"]
        )

        hvac_case_by_symbol = {}
        for case in hvac_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, hvac_case_by_symbol)
                hvac_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(set(hvac_contract["target_symbols"]), set(hvac_case_by_symbol))

        hvac_test_bytes = (REPOSITORY_ROOT / hvac_test_path).read_bytes()
        hvac_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\[(?P<body>.*?)\n\s*\];",
            hvac_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(hvac_direct_receipt_hash_block)
        assert hvac_direct_receipt_hash_block is not None
        hvac_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                hvac_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(28, len(hvac_direct_receipt_hashes))
        self.assertEqual(28, len(set(hvac_direct_receipt_hashes)))

        hvac_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\[(?P<body>.*?)\n\s*\];",
            hvac_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(hvac_collector_output_hash_block)
        assert hvac_collector_output_hash_block is not None
        hvac_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                hvac_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(28, len(hvac_collector_output_hashes))
        self.assertEqual(28, len(set(hvac_collector_output_hashes)))

        hvac_source_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Hvac/SourceSystem.cs"
        )
        hvac_supply_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Hvac/SupplySystem.cs"
        )
        hvac_reader_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmReader.cs"
        )
        hvac_writer_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Serialization/GrmWriter.cs"
        )
        hvac_conversion_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Conversion/"
            "GreenRetrofitConversion.cs"
        )

        def expected_hvac_implementation(symbol: str) -> tuple[str, str]:
            enum_members = {
                "CompressorType": "GonieGonie.SimpleDragon.CompressorType",
                "CompressorType.RECIPROCATING": "GonieGonie.SimpleDragon.CompressorType.Reciprocating",
                "CompressorType.SCREW": "GonieGonie.SimpleDragon.CompressorType.Screw",
                "CompressorType.TURBO": "GonieGonie.SimpleDragon.CompressorType.Turbo",
                "CoolingTowerControl": "GonieGonie.SimpleDragon.CoolingTowerControl",
                "CoolingTowerControl.SINGLESPEED": "GonieGonie.SimpleDragon.CoolingTowerControl.SingleSpeed",
                "CoolingTowerControl.TWOSPEED": "GonieGonie.SimpleDragon.CoolingTowerControl.TwoSpeed",
                "CoolingTowerType": "GonieGonie.SimpleDragon.CoolingTowerType",
                "CoolingTowerType.CLOSED": "GonieGonie.SimpleDragon.CoolingTowerType.Closed",
                "CoolingTowerType.OPEN": "GonieGonie.SimpleDragon.CoolingTowerType.Open",
                "Fuel": "GonieGonie.SimpleDragon.FuelType",
                "Fuel.DISTRICTHEATING": "GonieGonie.SimpleDragon.FuelType.DistrictHeating",
                "Fuel.ELECTRICITY": "GonieGonie.SimpleDragon.FuelType.Electricity",
                "Fuel.LPG": "GonieGonie.SimpleDragon.FuelType.LiquefiedPetroleumGas",
                "Fuel.NATURALGAS": "GonieGonie.SimpleDragon.FuelType.NaturalGas",
                "Fuel.OIL": "GonieGonie.SimpleDragon.FuelType.Oil",
                "SourceSystem": "GonieGonie.SimpleDragon.SourceSystem",
            }
            if symbol in enum_members:
                return hvac_source_path, enum_members[symbol]
            if symbol.endswith(".__str__"):
                return hvac_writer_path, "GonieGonie.SimpleDragon.GrmWriter.Serialize"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if symbol == "NoneSource.ID":
                return (
                    hvac_supply_path,
                    "GonieGonie.SimpleDragon.SupplySystem.SourceSystemId",
                )
            if symbol in {"NoneSource", "NoneSource.__new__"}:
                return (
                    hvac_supply_path,
                    "GonieGonie.SimpleDragon.SupplySystem.SourceSystem",
                )
            if symbol == "SourceSystem.TYPE_MAPPER":
                return hvac_reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"
            raise AssertionError(symbol)

        hvac_exception_ids = {
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
        self.assertEqual(10, len(hvac_exception_ids))

        for target, direct_receipt_hash, collector_output_hash in zip(
            hvac_targets,
            hvac_direct_receipt_hashes,
            hvac_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            classification = hvac_contract["classifications"][symbol]
            exception_id = hvac_exception_ids.get(symbol)
            assertion_id = hvac_contract["assertion_ids"][symbol]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_hvac_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path, evidence_entry.implementation_path, symbol
            )
            self.assertEqual(
                implementation_symbol, evidence_entry.implementation_symbol, symbol
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(hvac_test_path, receipt.test_path, symbol)
            self.assertEqual(hvac_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(hvac_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            code, case_id = hvac_case_by_symbol[symbol]
            for exact_binding in (
                hvac_fixture_sha256,
                hvac_generator_sha256,
                hvac_validator_sha256,
                hvac_test_sha256,
                "commit 85264dd",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                hvac_contract["native_routes"][symbol],
                code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            adaptation = hvac_contract["adaptations"].get(symbol)
            if exception_id is not None:
                self.assertIsNotNone(adaptation, symbol)
                assert adaptation is not None
                self.assertIn(adaptation, entry.rationale, symbol)
                self.assertIn(exception_id, entry.rationale, symbol)
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale), exception.effects
                )
                self.assertEqual(
                    "accepted-native-api-adaptation", exception.approval, symbol
                )

        hvac_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith("epsimple-hvac-enums-base-")
        )
        self.assertEqual(28, len(hvac_evidence_entries))
        self.assertEqual(
            set(hvac_contract["target_symbols"]),
            {item.symbol for item in hvac_evidence_entries},
        )
        self.assertEqual(
            594,
            len(symbol_evidence.entries)
            - len(hvac_evidence_entries)
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-thermal-source-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-supply-system-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-other-systems-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-source-tower-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-supply-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-appenders-controllers-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-misc-systems-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-definitions-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-schema-static-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-list-core-"
                )
                for item in symbol_evidence.entries
            ),
        )
        self.assertEqual(
            346,
            sum(
                item.identifier not in set(hvac_exception_ids.values())
                and not item.identifier.startswith(
                    "reviewed-native-discriminated-source-aggregate-and-conversion-route-"
                )
                and "reviewed-native-discriminated-supply-aggregate-and-conversion-route-"
                not in item.identifier
                and not item.identifier.startswith(
                    "reviewed-native-immutable-other-system-and-aggregate-route-"
                )
                and item.identifier not in source_tower_exception_ids
                and item.identifier not in supply_core_exception_ids
                and item.identifier not in appender_controller_exception_ids
                and item.identifier not in misc_systems_exception_ids
                and item.identifier not in imugi_idd_definitions_exception_ids
                and item.identifier not in imugi_idd_schema_static_exception_ids
                and item.identifier not in imugi_idf_object_exception_ids
                and item.identifier not in imugi_idf_object_list_exception_ids
                for item in configuration.exceptions
            ),
        )

        hvac_excluded = tuple(hvac_fixture["excluded_receipts"])
        hvac_deferred = tuple(hvac_fixture["deferred_receipts"])
        self.assertEqual(58, len(hvac_excluded))
        self.assertEqual(116, len(hvac_deferred))
        self.assertEqual(
            set(thermal_target_indices),
            {
                item["inventory_index"]
                for item in hvac_deferred
                if item["inventory_index"] in thermal_target_indices
            },
        )
        self.assertEqual(
            set(supply_target_indices),
            {
                item["inventory_index"]
                for item in hvac_deferred
                if item["inventory_index"] in supply_target_indices
            },
        )
        self.assertEqual(
            set(other_target_indices),
            {
                item["inventory_index"]
                for item in hvac_deferred
                if item["inventory_index"] in other_target_indices
            },
        )
        self.assertEqual(
            set(range(135, 337)),
            set(hvac_target_indices)
            | {item["inventory_index"] for item in hvac_excluded}
            | {item["inventory_index"] for item in hvac_deferred},
        )
        for receipt in hvac_excluded:
            index = receipt["inventory_index"]
            key = (receipt["path"], receipt["symbol"])
            self.assertEqual(key, compatibility.inventory.symbols[index].key)
            self.assertEqual(
                "out_of_scope", compatibility.matrix.entries[index].classification
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key)
        for receipt in hvac_deferred:
            index = receipt["inventory_index"]
            if (
                index in thermal_target_indices
                or index in supply_target_indices
                or index in other_target_indices
            ):
                continue
            key = (receipt["path"], receipt["symbol"])
            self.assertEqual(key, compatibility.inventory.symbols[index].key)
            self.assertEqual(
                "needs_reverification",
                compatibility.matrix.entries[index].classification,
            )
            self.assertNotIn(key, symbol_evidence.entries_by_key)

        thermal_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-hvac-thermal-source-oracle.json"
        )
        thermal_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_epsimple_hvac_thermal_source_oracle.py"
        )
        thermal_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_epsimple_hvac_thermal_source_oracle.py"
        )
        thermal_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "HvacThermalSourceOracleParityTests.cs"
        )
        thermal_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.HvacThermalSourceOracleParityTests."
            "MatchesPinnedHvacThermalSourcesThroughProductionPublicRoutes"
        )
        thermal_fixture_sha256 = (
            "sha256:e78e8bcbe42cd236775db63d50088bad82a9e9c5328e5fa5de6873d069984391"
        )
        thermal_generator_sha256 = (
            "sha256:e930c9242c76b48500010e76f625e41baa07de96e4629b447df61db6c571e51c"
        )
        thermal_validator_sha256 = (
            "sha256:ca7fb52d4a68ada17437d9e4590b129cf22cce842b37147aacf76d4f17c92265"
        )
        thermal_test_sha256 = (
            "sha256:1d87d2cc6f8e356d4421a309ac0ce80e3f5b8d0796bfae4677b6167dbf24a40e"
        )
        for pinned_path, expected_sha256 in (
            (thermal_fixture_path, thermal_fixture_sha256),
            (thermal_generator_path, thermal_generator_sha256),
            (thermal_validator_path, thermal_validator_sha256),
            (REPOSITORY_ROOT / thermal_test_path, thermal_test_sha256),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        thermal_fixture = json.loads(
            thermal_fixture_path.read_text(encoding="utf-8")
        )
        thermal_contract = thermal_fixture["consumer_contract"]
        thermal_targets = tuple(thermal_fixture["target_receipts"])
        self.assertEqual(
            thermal_target_indices,
            tuple(item["inventory_index"] for item in thermal_targets),
        )
        self.assertEqual(
            tuple(thermal_contract["closure"]["target_symbols"]),
            tuple(item["symbol"] for item in thermal_targets),
        )
        self.assertEqual(6, thermal_contract["case_count"])
        self.assertEqual(6, len(thermal_fixture["cases"]))
        self.assertEqual(
            {"equivalent": 24, "exception": 23},
            thermal_contract["classification_counts"],
        )
        self.assertEqual(47, thermal_contract["closure"]["target_count"])
        self.assertEqual(155, thermal_contract["closure"]["adjacent_count"])
        self.assertEqual(202, thermal_contract["closure"]["source_declaration_count"])
        self.assertTrue(
            thermal_contract["closure"]["exact_one_case_target_partition"]
        )
        self.assertTrue(thermal_contract["closure"]["full_hvac_source_partition"])
        self.assertEqual(
            set(range(135, 337)),
            set(thermal_target_indices)
            | set(thermal_contract["closure"]["adjacent_indices"]),
        )

        thermal_case_by_symbol = {}
        for case in thermal_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, thermal_case_by_symbol)
                thermal_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(thermal_contract["closure"]["target_symbols"]),
            set(thermal_case_by_symbol),
        )

        thermal_test_bytes = (REPOSITORY_ROOT / thermal_test_path).read_bytes()
        thermal_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\[(?P<body>.*?)\n\s*\];",
            thermal_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(thermal_direct_receipt_hash_block)
        assert thermal_direct_receipt_hash_block is not None
        thermal_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                thermal_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(47, len(thermal_direct_receipt_hashes))
        self.assertEqual(47, len(set(thermal_direct_receipt_hashes)))

        thermal_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\[(?P<body>.*?)\n\s*\];",
            thermal_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(thermal_collector_output_hash_block)
        assert thermal_collector_output_hash_block is not None
        thermal_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                thermal_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(47, len(thermal_collector_output_hashes))
        self.assertEqual(47, len(set(thermal_collector_output_hashes)))

        def expected_thermal_implementation(symbol: str) -> tuple[str, str]:
            route = thermal_contract["native_routes"][symbol]
            if symbol.endswith(".from_json"):
                return hvac_reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if "." not in symbol or symbol.endswith(".__init__"):
                return hvac_source_path, "GonieGonie.SimpleDragon.SourceSystem"
            return hvac_source_path, route

        thermal_exception_ids = set(thermal_contract["adaptations"].values())
        self.assertEqual(23, len(thermal_exception_ids))
        self.assertEqual(
            set(thermal_contract["adaptations"]),
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in thermal_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            thermal_targets,
            thermal_direct_receipt_hashes,
            thermal_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            classification = thermal_contract["classifications"][symbol]
            exception_id = thermal_contract["adaptations"].get(symbol)
            assertion_id = thermal_contract["assertion_ids"][symbol]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_thermal_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path, evidence_entry.implementation_path, symbol
            )
            self.assertEqual(
                implementation_symbol, evidence_entry.implementation_symbol, symbol
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(thermal_test_path, receipt.test_path, symbol)
            self.assertEqual(thermal_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(thermal_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            code, case_id = thermal_case_by_symbol[symbol]
            for exact_binding in (
                thermal_fixture_sha256,
                thermal_generator_sha256,
                thermal_validator_sha256,
                thermal_test_sha256,
                "commit 0ef3a7d",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                thermal_contract["native_routes"][symbol],
                code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                exception = exceptions_by_id[exception_id]
                self.assertIn(exception_id, entry.rationale, symbol)
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale), exception.effects
                )
                self.assertEqual(
                    "accepted-native-api-adaptation", exception.approval, symbol
                )

        thermal_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "epsimple-hvac-thermal-source-"
            )
        )
        self.assertEqual(47, len(thermal_evidence_entries))
        self.assertEqual(
            set(thermal_contract["closure"]["target_symbols"]),
            {item.symbol for item in thermal_evidence_entries},
        )
        self.assertEqual(
            622,
            len(symbol_evidence.entries)
            - len(thermal_evidence_entries)
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-supply-system-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-other-systems-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-source-tower-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-supply-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-appenders-controllers-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-misc-systems-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-definitions-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-schema-static-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-list-core-"
                )
                for item in symbol_evidence.entries
            ),
        )
        self.assertEqual(
            356,
            sum(
                item.identifier not in thermal_exception_ids
                and "reviewed-native-discriminated-supply-aggregate-and-conversion-route-"
                not in item.identifier
                and not item.identifier.startswith(
                    "reviewed-native-immutable-other-system-and-aggregate-route-"
                )
                and item.identifier not in source_tower_exception_ids
                and item.identifier not in supply_core_exception_ids
                and item.identifier not in appender_controller_exception_ids
                and item.identifier not in misc_systems_exception_ids
                and item.identifier not in imugi_idd_definitions_exception_ids
                and item.identifier not in imugi_idd_schema_static_exception_ids
                and item.identifier not in imugi_idf_object_exception_ids
                and item.identifier not in imugi_idf_object_list_exception_ids
                for item in configuration.exceptions
            ),
        )

        supply_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-hvac-supply-system-oracle.json"
        )
        supply_fixture = json.loads(
            supply_fixture_path.read_text(encoding="utf-8")
        )
        other_fixture_path = (
            REPOSITORY_ROOT
            / "fixtures/reference/python-0.7.0/epsimple-hvac-other-systems-oracle.json"
        )
        other_fixture = json.loads(
            other_fixture_path.read_text(encoding="utf-8")
        )
        supply_indices = {
            item["inventory_index"] for item in supply_fixture["target_receipts"]
        }
        other_indices = {
            item["inventory_index"] for item in other_fixture["target_receipts"]
        }
        self.assertEqual(52, len(supply_indices))
        self.assertEqual(17, len(other_indices))
        self.assertEqual(set(), supply_indices & other_indices)
        self.assertEqual(
            supply_indices | other_indices,
            {
                item["inventory_index"]
                for item in hvac_deferred
                if item["inventory_index"] not in thermal_target_indices
            },
        )
        supply_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_epsimple_hvac_supply_system_oracle.py"
        )
        supply_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_epsimple_hvac_supply_system_oracle.py"
        )
        supply_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "HvacSupplySystemOracleParityTests.cs"
        )
        supply_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.HvacSupplySystemOracleParityTests."
            "MatchesPinnedHvacSupplySystemsThroughProductionPublicRoutes"
        )
        supply_fixture_sha256 = (
            "sha256:b9a98ea739bf4181a4f93c8bed161f559c03bb93a4926ee56dccc100ddd49d65"
        )
        supply_generator_sha256 = (
            "sha256:e7874d74d2338c4fa71ab7ddf3cf33b17ce713dcefa0a3d6519cd5a5dd28780d"
        )
        supply_validator_sha256 = (
            "sha256:91d1c96ea25e25804b747999e80b78993ff1b58fe8563dc32e0ba8f1a73d9534"
        )
        supply_test_sha256 = (
            "sha256:9cef29fa99faddea2376c38f8b1749464b1b608914b2a8f8674cfcb703bd91f1"
        )
        for pinned_path, expected_sha256 in (
            (supply_fixture_path, supply_fixture_sha256),
            (supply_generator_path, supply_generator_sha256),
            (supply_validator_path, supply_validator_sha256),
            (REPOSITORY_ROOT / supply_test_path, supply_test_sha256),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        supply_contract = supply_fixture["consumer_contract"]
        supply_targets = tuple(supply_fixture["target_receipts"])
        self.assertEqual(
            supply_target_indices,
            tuple(item["inventory_index"] for item in supply_targets),
        )
        self.assertEqual(
            tuple(supply_contract["closure"]["target_symbols"]),
            tuple(item["symbol"] for item in supply_targets),
        )
        self.assertEqual(8, supply_contract["case_count"])
        self.assertEqual(8, len(supply_fixture["cases"]))
        self.assertEqual(
            {"equivalent": 19, "exception": 33},
            supply_contract["classification_counts"],
        )
        self.assertEqual(52, supply_contract["closure"]["target_count"])
        self.assertEqual(150, supply_contract["closure"]["adjacent_count"])
        self.assertEqual(202, supply_contract["closure"]["source_declaration_count"])
        self.assertTrue(
            supply_contract["closure"]["exact_one_case_target_partition"]
        )
        self.assertTrue(supply_contract["closure"]["full_hvac_source_partition"])
        self.assertEqual(
            set(range(135, 337)),
            set(supply_target_indices)
            | set(supply_contract["closure"]["adjacent_indices"]),
        )

        supply_case_by_symbol = {}
        for case in supply_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, supply_case_by_symbol)
                supply_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(supply_contract["closure"]["target_symbols"]),
            set(supply_case_by_symbol),
        )

        supply_test_bytes = (REPOSITORY_ROOT / supply_test_path).read_bytes()
        supply_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            supply_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(supply_direct_receipt_hash_block)
        assert supply_direct_receipt_hash_block is not None
        supply_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                supply_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(52, len(supply_direct_receipt_hashes))
        self.assertEqual(52, len(set(supply_direct_receipt_hashes)))

        supply_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            supply_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(supply_collector_output_hash_block)
        assert supply_collector_output_hash_block is not None
        supply_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                supply_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(52, len(supply_collector_output_hashes))
        self.assertEqual(52, len(set(supply_collector_output_hashes)))

        def expected_supply_implementation(symbol: str) -> tuple[str, str]:
            route = supply_contract["native_routes"][symbol]
            if symbol.endswith(".from_json") or symbol == "SupplySystem.TYPE_MAPPER":
                return hvac_reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if "." not in symbol or symbol.endswith(".__init__"):
                return hvac_supply_path, "GonieGonie.SimpleDragon.SupplySystem"
            return (
                hvac_supply_path,
                route.replace(
                    "GonieGonie.SimpleDragon.SourceSystem.",
                    "GonieGonie.SimpleDragon.SupplySystem.",
                ),
            )

        supply_adaptation_counts = {
            adaptation: tuple(supply_contract["adaptations"].values()).count(
                adaptation
            )
            for adaptation in supply_contract["adaptations"].values()
        }

        def expected_supply_exception_id(symbol: str) -> str | None:
            adaptation = supply_contract["adaptations"].get(symbol)
            if adaptation is None:
                return None
            if supply_adaptation_counts[adaptation] == 1:
                return adaptation
            owner = symbol.split(".", 1)[0]
            owner_slug = re.sub(r"(?<!^)(?=[A-Z])", "-", owner).lower()
            return f"{owner_slug}-{adaptation}"

        supply_exception_ids = {
            expected_supply_exception_id(symbol)
            for symbol in supply_contract["adaptations"]
        }
        self.assertNotIn(None, supply_exception_ids)
        self.assertEqual(33, len(supply_exception_ids))
        self.assertEqual(
            set(supply_contract["adaptations"]),
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in supply_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            supply_targets,
            supply_direct_receipt_hashes,
            supply_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            classification = supply_contract["classifications"][symbol]
            exception_id = expected_supply_exception_id(symbol)
            assertion_id = supply_contract["assertion_ids"][symbol]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_supply_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path, evidence_entry.implementation_path, symbol
            )
            self.assertEqual(
                implementation_symbol, evidence_entry.implementation_symbol, symbol
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(supply_test_path, receipt.test_path, symbol)
            self.assertEqual(supply_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(supply_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            code, case_id = supply_case_by_symbol[symbol]
            for exact_binding in (
                supply_fixture_sha256,
                supply_generator_sha256,
                supply_validator_sha256,
                supply_test_sha256,
                "commit 6517fb9",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                supply_contract["native_routes"][symbol],
                code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            adaptation = supply_contract["adaptations"].get(symbol)
            if exception_id is not None:
                assert adaptation is not None
                exception = exceptions_by_id[exception_id]
                self.assertIn(adaptation, entry.rationale, symbol)
                self.assertIn(exception_id, entry.rationale, symbol)
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale), exception.effects
                )
                self.assertEqual(
                    "accepted-native-api-adaptation", exception.approval, symbol
                )

        supply_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "epsimple-hvac-supply-system-"
            )
        )
        self.assertEqual(52, len(supply_evidence_entries))
        self.assertEqual(
            set(supply_contract["closure"]["target_symbols"]),
            {item.symbol for item in supply_evidence_entries},
        )
        self.assertEqual(
            669,
            len(symbol_evidence.entries)
            - len(supply_evidence_entries)
            - sum(
                item.receipts[0].identifier.startswith(
                    "epsimple-hvac-other-systems-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-source-tower-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-supply-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-appenders-controllers-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "dragon-hvac-misc-systems-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-definitions-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idd-schema-static-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-core-"
                )
                for item in symbol_evidence.entries
            )
            - sum(
                item.receipts[0].identifier.startswith(
                    "imugi-idf-object-list-core-"
                )
                for item in symbol_evidence.entries
            ),
        )
        self.assertEqual(
            379,
            sum(
                item.identifier not in supply_exception_ids
                and not item.identifier.startswith(
                    "reviewed-native-immutable-other-system-and-aggregate-route-"
                )
                and item.identifier not in source_tower_exception_ids
                and item.identifier not in supply_core_exception_ids
                and item.identifier not in appender_controller_exception_ids
                and item.identifier not in misc_systems_exception_ids
                and item.identifier not in imugi_idd_definitions_exception_ids
                and item.identifier not in imugi_idd_schema_static_exception_ids
                and item.identifier not in imugi_idf_object_exception_ids
                and item.identifier not in imugi_idf_object_list_exception_ids
                for item in configuration.exceptions
            ),
        )
        self.assertEqual(
            set(range(135, 337)),
            set(hvac_target_indices)
            | set(thermal_target_indices)
            | set(supply_target_indices)
            | {item["inventory_index"] for item in hvac_excluded}
            | other_indices,
        )

        other_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_epsimple_hvac_other_systems_oracle.py"
        )
        other_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_epsimple_hvac_other_systems_oracle.py"
        )
        other_test_path = (
            "tests/SimpleDragon/GonieGonie.SimpleDragon.Core.Tests/"
            "HvacOtherSystemsOracleParityTests.cs"
        )
        other_test_symbol = (
            "GonieGonie.SimpleDragon.Tests.HvacOtherSystemsOracleParityTests."
            "MatchesPinnedHvacOtherSystemsThroughProductionPublicRoutes"
        )
        other_fixture_sha256 = (
            "sha256:baab4b84afb2f387267fa49e4b7907f0d74b3a49076d5a0e7562d421a8c5cedc"
        )
        other_generator_sha256 = (
            "sha256:febce413e0c12adc4e75441a61de37f7a1f04744dd3cb1b7e71c4325a5c1e02b"
        )
        other_validator_sha256 = (
            "sha256:d2d1fa88d554d967065508272e881718a6b0f440a185506a8dab10c6976d4b22"
        )
        other_test_sha256 = (
            "sha256:8dcbb92391ab55dc808d6cfcb85839127f3f4dcf026b4c81e07213f2ede21326"
        )
        for pinned_path, expected_sha256 in (
            (other_fixture_path, other_fixture_sha256),
            (other_generator_path, other_generator_sha256),
            (other_validator_path, other_validator_sha256),
            (REPOSITORY_ROOT / other_test_path, other_test_sha256),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        other_contract = other_fixture["consumer_contract"]
        other_targets = tuple(other_fixture["target_receipts"])
        self.assertEqual(
            other_target_indices,
            tuple(item["inventory_index"] for item in other_targets),
        )
        self.assertEqual(
            tuple(other_contract["closure"]["target_symbols"]),
            tuple(item["symbol"] for item in other_targets),
        )
        self.assertEqual(2, other_contract["case_count"])
        self.assertEqual(2, len(other_fixture["cases"]))
        self.assertEqual(
            {"equivalent": 9, "exception": 8},
            other_contract["classification_counts"],
        )
        self.assertEqual(17, other_contract["closure"]["target_count"])
        self.assertEqual(185, other_contract["closure"]["adjacent_count"])
        self.assertEqual(202, other_contract["closure"]["source_declaration_count"])
        self.assertTrue(other_contract["closure"]["exact_one_case_target_partition"])
        self.assertTrue(other_contract["closure"]["full_hvac_source_partition"])
        self.assertEqual(
            set(range(135, 337)),
            set(other_target_indices)
            | set(other_contract["closure"]["adjacent_indices"]),
        )

        other_case_by_symbol = {}
        for case in other_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, other_case_by_symbol)
                other_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(other_contract["closure"]["target_symbols"]),
            set(other_case_by_symbol),
        )

        other_test_bytes = (REPOSITORY_ROOT / other_test_path).read_bytes()
        other_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            other_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(other_direct_receipt_hash_block)
        assert other_direct_receipt_hash_block is not None
        other_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                other_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(17, len(other_direct_receipt_hashes))
        self.assertEqual(17, len(set(other_direct_receipt_hashes)))

        other_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            other_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(other_collector_output_hash_block)
        assert other_collector_output_hash_block is not None
        other_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                other_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(17, len(other_collector_output_hashes))
        self.assertEqual(17, len(set(other_collector_output_hashes)))

        hvac_other_path = (
            "src/SimpleDragon/GonieGonie.SimpleDragon.Core/Hvac/OtherSystems.cs"
        )

        def expected_other_implementation(symbol: str) -> tuple[str, str]:
            if symbol.endswith(".from_json"):
                return hvac_reader_path, "GonieGonie.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "GonieGonie.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            owner = (
                "GonieGonie.SimpleDragon.PhotovoltaicSystem"
                if symbol.startswith("PhotoVoltaicSystem")
                else "GonieGonie.SimpleDragon.VentilationSystem"
            )
            if "." not in symbol or symbol.endswith(".__init__"):
                return hvac_other_path, owner
            return hvac_other_path, other_contract["native_routes"][symbol]

        other_exception_ids = set(other_contract["adaptations"].values())
        self.assertEqual(8, len(other_exception_ids))
        self.assertEqual(
            set(other_contract["adaptations"]),
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in other_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            other_targets,
            other_direct_receipt_hashes,
            other_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            key = (target["path"], symbol)
            entry = compatibility.matrix.entries[index]
            classification = other_contract["classifications"][symbol]
            exception_id = other_contract["adaptations"].get(symbol)
            assertion_id = other_contract["assertion_ids"][symbol]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_other_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path, evidence_entry.implementation_path, symbol
            )
            self.assertEqual(
                implementation_symbol, evidence_entry.implementation_symbol, symbol
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(other_test_path, receipt.test_path, symbol)
            self.assertEqual(other_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(other_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            code, case_id = other_case_by_symbol[symbol]
            for exact_binding in (
                other_fixture_sha256,
                other_generator_sha256,
                other_validator_sha256,
                other_test_sha256,
                "commit 7e69d81",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                other_contract["native_routes"][symbol],
                code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                exception = exceptions_by_id[exception_id]
                self.assertIn(exception_id, entry.rationale, symbol)
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale), exception.effects
                )
                self.assertEqual(
                    "accepted-native-api-adaptation", exception.approval, symbol
                )

        other_evidence_entries = tuple(
            item
            for item in symbol_evidence.entries
            if item.receipts[0].identifier.startswith(
                "epsimple-hvac-other-systems-"
            )
        )
        self.assertEqual(17, len(other_evidence_entries))
        self.assertEqual(
            set(other_contract["closure"]["target_symbols"]),
            {item.symbol for item in other_evidence_entries},
        )
        self.assertEqual(
            721,
            len(symbol_evidence.entries)
            - len(other_evidence_entries)
            - len(source_tower_evidence_entries)
            - len(supply_core_evidence_entries)
            - len(appender_controller_evidence_entries)
            - len(misc_systems_evidence_entries)
            - len(imugi_idd_definitions_evidence_entries)
            - len(imugi_idd_schema_static_evidence_entries)
            - len(imugi_idf_object_evidence_entries)
            - len(imugi_idf_object_list_evidence_entries),
        )
        self.assertEqual(
            412,
            sum(
                item.identifier not in other_exception_ids
                and item.identifier not in source_tower_exception_ids
                and item.identifier not in supply_core_exception_ids
                and item.identifier not in appender_controller_exception_ids
                and item.identifier not in misc_systems_exception_ids
                and item.identifier not in imugi_idd_definitions_exception_ids
                and item.identifier not in imugi_idd_schema_static_exception_ids
                and item.identifier not in imugi_idf_object_exception_ids
                and item.identifier not in imugi_idf_object_list_exception_ids
                for item in configuration.exceptions
            ),
        )
        self.assertEqual(
            0,
            sum(
                entry.path == "src/epsimple/core/hvac.py"
                and entry.classification == "needs_reverification"
                for entry in compatibility.matrix.entries
            ),
        )

        source_tower_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_dragon_hvac_source_tower_core_oracle.py"
        )
        source_tower_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_dragon_hvac_source_tower_core_oracle.py"
        )
        source_tower_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Hvac/"
            "SourceTowerCoreOracleParityTests.cs"
        )
        source_tower_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Hvac.SourceTowerCoreOracleParityTests."
            "MatchesPinnedSourceTowerCoreThroughProductionPublicRoutes"
        )
        source_tower_fixture_sha256 = (
            "sha256:60e0a2353620437049bba8420a0154e638fe86e5c915b4231793e397bb5c4fc5"
        )
        source_tower_generator_sha256 = (
            "sha256:e9c78f72ae62dc65f229c9766322fb53062b0f8e037bd1b62b5ac5050d8ce2d5"
        )
        source_tower_validator_sha256 = (
            "sha256:75762179ea1614ca74fd275accd132c1f0169f7d836b2e46e87a1a23e740f058"
        )
        source_tower_test_sha256 = (
            "sha256:2dcced74b037732c9147ce60e71569ff3563a757b3b63b0eadaad80aaa7a4ed6"
        )
        for pinned_path, expected_sha256 in (
            (source_tower_fixture_path, source_tower_fixture_sha256),
            (source_tower_generator_path, source_tower_generator_sha256),
            (source_tower_validator_path, source_tower_validator_sha256),
            (REPOSITORY_ROOT / source_tower_test_path, source_tower_test_sha256),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        source_tower_closure = source_tower_contract["closure"]
        self.assertEqual(source_tower_target_indices, tuple(source_tower_closure["target_indices"]))
        self.assertEqual(59, source_tower_closure["target_count"])
        self.assertEqual(15, source_tower_closure["adjacent_count"])
        self.assertEqual(174, source_tower_closure["source_declaration_count"])
        self.assertEqual(74, source_tower_closure["source_tower_family_count"])
        self.assertTrue(source_tower_closure["exact_one_case_target_partition"])
        self.assertTrue(source_tower_closure["full_hvac_source_partition"])
        self.assertTrue(source_tower_closure["full_source_tower_family_closure"])
        self.assertEqual(
            {"equivalent": 27, "exception": 32},
            source_tower_contract["classification_counts"],
        )
        self.assertEqual(10, source_tower_contract["case_count"])
        self.assertEqual(10, len(source_tower_fixture["cases"]))
        self.assertEqual(
            set(range(641, 815)),
            set(source_tower_target_indices)
            | set(source_tower_closure["adjacent_indices"])
            | set(source_tower_closure["deferred_indices"]),
        )

        source_tower_case_by_symbol = {}
        for case in source_tower_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, source_tower_case_by_symbol)
                source_tower_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(source_tower_closure["target_symbols"]),
            set(source_tower_case_by_symbol),
        )

        source_tower_test_bytes = (
            REPOSITORY_ROOT / source_tower_test_path
        ).read_bytes()
        source_tower_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            source_tower_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(source_tower_direct_receipt_hash_block)
        assert source_tower_direct_receipt_hash_block is not None
        source_tower_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                source_tower_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(59, len(source_tower_direct_receipt_hashes))
        self.assertEqual(59, len(set(source_tower_direct_receipt_hashes)))

        source_tower_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            source_tower_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(source_tower_collector_output_hash_block)
        assert source_tower_collector_output_hash_block is not None
        source_tower_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                source_tower_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(59, len(source_tower_collector_output_hashes))
        self.assertEqual(59, len(set(source_tower_collector_output_hashes)))

        def expected_source_tower_implementation(
            symbol: str,
            native_route: str,
        ) -> tuple[str, str]:
            if symbol.startswith(("AbsorptionChiller", "Chiller", "CompressorType")):
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/Chillers.cs"
                )
            elif symbol.startswith(("CoolingTower", "Closed", "Open")):
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "CoolingTowers.cs"
                )
            elif symbol.startswith("GeothermalHeatPump"):
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "GeothermalHeatPump.cs"
                )
            elif symbol == "SourceSystem.idf_terminalunitlistname" or symbol.startswith(
                ("Boiler", "HeatPump")
            ):
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "SourceSystems.cs"
                )
            else:
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs"
                )

            if ".__init__" in symbol or "." not in symbol:
                owner = symbol.split(".", 1)[0]
                implementation_symbol = f"GonieGonie.InvisibleDragon.Hvac.{owner}"
            elif "ToIdfObjects(...) ->" in native_route:
                owner = symbol.split(".", 1)[0]
                implementation_symbol = (
                    f"GonieGonie.InvisibleDragon.Hvac.{owner}.ToIdfObjects"
                )
            else:
                implementation_symbol = native_route
                if "(" in implementation_symbol:
                    implementation_symbol = implementation_symbol.split("(", 1)[0]
            if symbol == "GeothermalHeatPump.idf_objtypename":
                implementation_symbol = (
                    "GonieGonie.InvisibleDragon.Hvac.GeothermalHeatPump"
                )
            return implementation_path, implementation_symbol

        self.assertEqual(
            set(source_tower_contract["adaptations"]),
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in source_tower_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            source_tower_targets,
            source_tower_direct_receipt_hashes,
            source_tower_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            classification = source_tower_contract["classifications"][symbol]
            exception_id = source_tower_contract["adaptations"].get(symbol)
            assertion_id = source_tower_contract["assertion_ids"][symbol]
            native_route = source_tower_contract["native_routes"][symbol]
            code, case_id = source_tower_case_by_symbol[symbol]

            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_source_tower_implementation(symbol, native_route)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(source_tower_test_path, receipt.test_path, symbol)
            self.assertEqual(source_tower_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(source_tower_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                source_tower_fixture_sha256,
                source_tower_generator_sha256,
                source_tower_validator_sha256,
                source_tower_test_sha256,
                "commit 33d0be9",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale),
                    exception.effects,
                )
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )

        self.assertEqual(
            set(source_tower_contract["closure"]["target_symbols"]),
            {item.symbol for item in source_tower_evidence_entries},
        )
        for symbol, expected_classification in source_tower_closure[
            "adjacent_classifications"
        ].items():
            self.assertEqual(
                expected_classification,
                by_key[("src/idragon/dragon/hvac.py", symbol)].classification,
                symbol,
            )
        self.assertEqual(
            88,
            len(supply_core_target_indices)
            + len(appender_controller_target_indices)
            + len(misc_systems_target_indices)
            + sum(
                entry.path == "src/idragon/dragon/hvac.py"
                and entry.classification == "needs_reverification"
                for entry in compatibility.matrix.entries
            ),
        )

        supply_core_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_dragon_hvac_supply_core_oracle.py"
        )
        supply_core_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_dragon_hvac_supply_core_oracle.py"
        )
        supply_core_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Hvac/"
            "SupplyCoreOracleParityTests.cs"
        )
        supply_core_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Hvac.SupplyCoreOracleParityTests."
            "MatchesPinnedSupplyCoreThroughProductionPublicRoutes"
        )
        supply_core_fixture_sha256 = (
            "sha256:dcf355329a083f9fac82434e18fc3b847a44bc134eb7f593f497c0aeae4c6b9f"
        )
        supply_core_generator_sha256 = (
            "sha256:3f1bcbf28df62c3426f8d343dab3f123b9c730bcdd234e3c570aaff21b87cd97"
        )
        supply_core_validator_sha256 = (
            "sha256:6832bde12cb4e5ab213f2f12307267ebe571de1bf2fc1a8ffa37db728014eabd"
        )
        supply_core_test_sha256 = (
            "sha256:4dac9b429af4833841d19c1449589e5501ca2f66f88cb55385a9642eff8e66c6"
        )
        for pinned_path, expected_sha256 in (
            (supply_core_fixture_path, supply_core_fixture_sha256),
            (supply_core_generator_path, supply_core_generator_sha256),
            (supply_core_validator_path, supply_core_validator_sha256),
            (REPOSITORY_ROOT / supply_core_test_path, supply_core_test_sha256),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        supply_core_closure = supply_core_contract["closure"]
        self.assertEqual(
            (
                645,
                *range(647, 652),
                *range(700, 714),
                *range(720, 726),
                *range(750, 753),
                *range(762, 774),
                789,
                *range(797, 804),
            ),
            supply_core_target_indices,
        )
        self.assertEqual(
            supply_core_target_indices,
            tuple(supply_core_closure["target_indices"]),
        )
        self.assertEqual(49, supply_core_closure["target_count"])
        self.assertEqual(8, len(supply_core_closure["adjacent_indices"]))
        self.assertEqual(9, supply_core_closure["family_count"])
        self.assertEqual(57, supply_core_closure["family_declaration_count"])
        self.assertTrue(supply_core_closure["full_family_closure"])
        self.assertEqual(9, len(supply_core_fixture["cases"]))
        self.assertEqual(
            {"equivalent": 18, "exception": 31, "total": 49},
            supply_core_fixture["native_review"]["counts"],
        )
        self.assertEqual(
            set(supply_core_target_indices)
            | set(supply_core_closure["adjacent_indices"]),
            {
                item["inventory_index"]
                for item in supply_core_targets
            }
            | set(supply_core_closure["adjacent_indices"]),
        )
        self.assertEqual(
            57,
            len(
                set(supply_core_target_indices)
                | set(supply_core_closure["adjacent_indices"])
            ),
        )

        supply_core_case_by_symbol = {}
        for case in supply_core_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, supply_core_case_by_symbol)
                supply_core_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(supply_core_closure["target_symbols"]),
            set(supply_core_case_by_symbol),
        )

        supply_core_test_bytes = (
            REPOSITORY_ROOT / supply_core_test_path
        ).read_bytes()
        supply_core_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            supply_core_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(supply_core_direct_receipt_hash_block)
        assert supply_core_direct_receipt_hash_block is not None
        supply_core_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                supply_core_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(49, len(supply_core_direct_receipt_hashes))
        self.assertEqual(49, len(set(supply_core_direct_receipt_hashes)))

        supply_core_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            supply_core_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(supply_core_collector_output_hash_block)
        assert supply_core_collector_output_hash_block is not None
        supply_core_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                supply_core_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(49, len(supply_core_collector_output_hashes))
        self.assertEqual(49, len(set(supply_core_collector_output_hashes)))

        def expected_supply_core_implementation(
            symbol: str,
            native_route: str,
        ) -> tuple[str, str]:
            owner = symbol.split(".", 1)[0]
            if native_route.startswith(
                "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument"
            ):
                return (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/"
                    "EnergyModel.cs",
                    "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument",
                )
            if symbol in {
                "ElectricRadiantFloor.source",
                "ElectricRadiator.source",
            }:
                return (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs",
                    "GonieGonie.InvisibleDragon.Hvac.SupplySystem.Source",
                )
            if symbol == "PackagedAirConditioner.coolable":
                return (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "SupplySystems.cs",
                    "GonieGonie.InvisibleDragon.Hvac.AirHandlingUnit.CanCool",
                )
            if symbol == "SupplySystem.idf_get_objname":
                return (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs",
                    "GonieGonie.InvisibleDragon.Hvac.SupplySystem.ObjectNameFor",
                )
            if owner in {"SupplyGroup", "SupplySystem"}:
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs"
                )
            elif owner in {"FanCoilUnit", "Radiator"}:
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "HydronicSupplySystems.cs"
                )
            else:
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "SupplySystems.cs"
                )

            if ".__init__" in symbol or "." not in symbol:
                implementation_symbol = f"GonieGonie.InvisibleDragon.Hvac.{owner}"
            else:
                implementation_symbol = native_route
                if " public " in implementation_symbol:
                    implementation_symbol = f"GonieGonie.InvisibleDragon.Hvac.{owner}"
                if "(" in implementation_symbol:
                    implementation_symbol = implementation_symbol.split("(", 1)[0]
            return implementation_path, implementation_symbol

        self.assertEqual(
            set(supply_core_contract["adaptations"]),
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in supply_core_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            supply_core_targets,
            supply_core_direct_receipt_hashes,
            supply_core_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            classification = supply_core_contract["classifications"][symbol]
            exception_id = supply_core_contract["adaptations"].get(symbol)
            assertion_id = supply_core_contract["assertion_ids"][symbol]
            registry_id = re.sub(r"[^a-z0-9]+", "-", assertion_id).strip("-")
            self.assertRegex(registry_id, r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            native_route = supply_core_contract["native_routes"][symbol]
            code, case_id = supply_core_case_by_symbol[symbol]

            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [f"upstream/symbol-evidence.json#{registry_id}"]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_supply_core_implementation(symbol, native_route)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(registry_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(supply_core_test_path, receipt.test_path, symbol)
            self.assertEqual(supply_core_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(supply_core_test_sha256, receipt.test_source_sha256, symbol)
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                "Oracle commit 07bcb7e",
                supply_core_fixture_sha256,
                supply_core_generator_sha256,
                supply_core_validator_sha256,
                supply_core_test_sha256,
                "commit 606f247",
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                code,
                case_id,
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale),
                    exception.effects,
                )
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )

        self.assertEqual(
            set(supply_core_closure["target_symbols"]),
            {item.symbol for item in supply_core_evidence_entries},
        )
        for symbol, expected_classification in supply_core_closure[
            "adjacent_existing_status"
        ].items():
            self.assertEqual(
                expected_classification,
                by_key[("src/idragon/dragon/hvac.py", symbol)].classification,
                symbol,
            )
        self.assertEqual(
            39,
            len(appender_controller_target_indices)
            + len(misc_systems_target_indices)
            + sum(
                entry.path == "src/idragon/dragon/hvac.py"
                and entry.classification == "needs_reverification"
                for entry in compatibility.matrix.entries
            ),
        )

        appender_controller_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/"
            "generate_dragon_hvac_appenders_controllers_oracle.py"
        )
        appender_controller_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_dragon_hvac_appenders_controllers_oracle.py"
        )
        appender_controller_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Hvac/"
            "AppendersControllersOracleParityTests.cs"
        )
        appender_controller_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Hvac."
            "AppendersControllersOracleParityTests."
            "MatchesPinnedAppendersControllersThroughPublicAggregateRoute"
        )
        appender_controller_fixture_sha256 = (
            "sha256:2d5034714366592c720d0872b616e409f62f50362abc58c48d970b904eb4b054"
        )
        appender_controller_generator_sha256 = (
            "sha256:00da10485dbd576286b222a016171390199d6148b99c1e45f64c1b5eaa63ad31"
        )
        appender_controller_validator_sha256 = (
            "sha256:253e64cd09b57af1dfcb00bf164d49586af6713119dbbd97d3e60dab95074dcf"
        )
        appender_controller_test_sha256 = (
            "sha256:5aa8742d1090473cb9af8420fab2fc1159c20b2c9603e09712e7481daf03d678"
        )
        for pinned_path, expected_sha256 in (
            (appender_controller_fixture_path, appender_controller_fixture_sha256),
            (appender_controller_generator_path, appender_controller_generator_sha256),
            (appender_controller_validator_path, appender_controller_validator_sha256),
            (
                REPOSITORY_ROOT / appender_controller_test_path,
                appender_controller_test_sha256,
            ),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        appender_controller_closure = appender_controller_contract["closure"]
        self.assertEqual(
            (
                *range(686, 693),
                *range(717, 720),
                *range(774, 777),
                *range(804, 815),
            ),
            appender_controller_target_indices,
        )
        self.assertEqual(
            appender_controller_target_indices,
            tuple(appender_controller_closure["target_indices"]),
        )
        self.assertEqual(24, appender_controller_closure["target_count"])
        self.assertEqual(149, appender_controller_closure["deferred_count"])
        self.assertEqual(1, appender_controller_closure["resolved_support_count"])
        self.assertEqual((796,), tuple(appender_controller_closure["resolved_support_indices"]))
        self.assertEqual(174, appender_controller_closure["source_declaration_count"])
        self.assertTrue(appender_controller_closure["exact_disjoint_source_partition"])
        self.assertTrue(appender_controller_closure["exact_one_case_target_partition"])
        self.assertTrue(appender_controller_closure["full_hvac_source_partition"])
        self.assertFalse(appender_controller_closure["target_support_overlap"])
        self.assertEqual(
            set(range(641, 815)),
            set(appender_controller_target_indices)
            | set(appender_controller_closure["resolved_support_indices"])
            | set(appender_controller_closure["deferred_indices"]),
        )
        self.assertEqual(
            {"equivalent": 0, "exception": 24},
            appender_controller_contract["classification_counts"],
        )
        self.assertEqual(6, appender_controller_contract["case_count"])
        self.assertEqual(6, len(appender_controller_fixture["cases"]))
        self.assertTrue(
            appender_controller_contract["evidence_contract"][
                "target_coverage_complete"
            ]
        )
        self.assertFalse(
            appender_controller_contract["evidence_contract"][
                "internal_native_route_claim"
            ]
        )
        self.assertFalse(
            appender_controller_contract["evidence_contract"][
                "active_energyplus_process_claim"
            ]
        )

        appender_controller_case_by_symbol = {}
        for case in appender_controller_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, appender_controller_case_by_symbol)
                appender_controller_case_by_symbol[symbol] = (
                    case["code"],
                    case["id"],
                )
        self.assertEqual(
            set(appender_controller_closure["target_symbols"]),
            set(appender_controller_case_by_symbol),
        )

        appender_controller_test_bytes = (
            REPOSITORY_ROOT / appender_controller_test_path
        ).read_bytes()
        appender_controller_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            appender_controller_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(appender_controller_direct_receipt_hash_block)
        assert appender_controller_direct_receipt_hash_block is not None
        appender_controller_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                appender_controller_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(24, len(appender_controller_direct_receipt_hashes))
        self.assertEqual(24, len(set(appender_controller_direct_receipt_hashes)))

        appender_controller_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            appender_controller_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(appender_controller_collector_output_hash_block)
        assert appender_controller_collector_output_hash_block is not None
        appender_controller_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                appender_controller_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(24, len(appender_controller_collector_output_hashes))
        self.assertEqual(24, len(set(appender_controller_collector_output_hashes)))

        appender_controller_implementation_path = (
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs"
        )
        appender_controller_implementation_symbol = (
            "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument"
        )
        appender_controller_implementation_sha256 = (
            "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / appender_controller_implementation_path).read_bytes()
            ).hexdigest()
        )
        self.assertEqual(
            set(appender_controller_contract["adaptations"]),
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in appender_controller_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            appender_controller_targets,
            appender_controller_direct_receipt_hashes,
            appender_controller_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            assertion_id = appender_controller_contract["assertion_ids"][symbol]
            self.assertRegex(assertion_id, r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            exception_id = appender_controller_contract["adaptations"][symbol]
            native_route = appender_controller_contract["native_routes"][symbol]
            code, case_id = appender_controller_case_by_symbol[symbol]

            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
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

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            self.assertEqual(
                appender_controller_implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                appender_controller_implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                appender_controller_implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(appender_controller_test_path, receipt.test_path, symbol)
            self.assertEqual(appender_controller_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(
                appender_controller_test_sha256,
                receipt.test_source_sha256,
                symbol,
            )
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                "Oracle commit d14de9e",
                appender_controller_fixture_sha256,
                appender_controller_generator_sha256,
                appender_controller_validator_sha256,
                appender_controller_test_sha256,
                "commit c33fa05",
                appender_controller_implementation_path
                + "@"
                + appender_controller_implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                code,
                case_id,
                "No standalone or internal postprocessor API equivalence is claimed.",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)

            exception = exceptions_by_id[exception_id]
            self.assertEqual(target["path"], exception.upstream_path, symbol)
            self.assertEqual(symbol, exception.upstream_symbol, symbol)
            self.assertEqual(
                inventory_symbol.symbol_hash,
                exception.upstream_symbol_hash,
                symbol,
            )
            self.assertIn(
                ("engineering_result", entry.rationale),
                exception.effects,
            )
            self.assertEqual(
                "accepted-native-api-adaptation",
                exception.approval,
                symbol,
            )

        self.assertEqual(
            set(appender_controller_closure["target_symbols"]),
            {item.symbol for item in appender_controller_evidence_entries},
        )
        support_entry = compatibility.matrix.entries[796]
        self.assertEqual(
            ("src/idragon/dragon/hvac.py", "SupplyGroup.to_idf_object"),
            support_entry.key,
        )
        self.assertEqual("exception", support_entry.classification)
        self.assertNotIn(
            796,
            appender_controller_target_indices,
        )
        self.assertEqual(
            15,
            len(misc_systems_target_indices)
            + sum(
                entry.path == "src/idragon/dragon/hvac.py"
                and entry.classification == "needs_reverification"
                for entry in compatibility.matrix.entries
            ),
        )

        misc_systems_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_dragon_hvac_misc_systems_core_oracle.py"
        )
        misc_systems_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_dragon_hvac_misc_systems_core_oracle.py"
        )
        misc_systems_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Hvac/"
            "MiscSystemsCoreOracleParityTests.cs"
        )
        misc_systems_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Hvac.MiscSystemsCoreOracleParityTests."
            "MatchesPinnedMiscSystemsThroughPublicProductionApis"
        )
        misc_systems_fixture_sha256 = (
            "sha256:2b2e5d3a5a6fc76247e6faec469dc23039ad53ae0c64a36553974633f2da9f89"
        )
        misc_systems_generator_sha256 = (
            "sha256:4d32b8eb44c810ee1210448be2e1fc8c94dee90a18159099304a2e74743dc421"
        )
        misc_systems_validator_sha256 = (
            "sha256:ef66a678175883a24ca4eedd29f0f16570d321a8379f3eceba1e8e123b0a2117"
        )
        misc_systems_test_sha256 = (
            "sha256:4e2f01a04b3454faf08e82b3710b244396a01dffd6f6d09ffcda0f1704ebb519"
        )
        for pinned_path, expected_sha256 in (
            (misc_systems_fixture_path, misc_systems_fixture_sha256),
            (misc_systems_generator_path, misc_systems_generator_sha256),
            (misc_systems_validator_path, misc_systems_validator_sha256),
            (REPOSITORY_ROOT / misc_systems_test_path, misc_systems_test_sha256),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        self.assertEqual(
            (693, 694, 697, 698, 699, 714, 715, 716, 753, 754, 756, 757, 758, 759, 760),
            misc_systems_target_indices,
        )
        misc_systems_closure = misc_systems_contract["closure"]
        self.assertEqual(
            misc_systems_target_indices,
            tuple(misc_systems_closure["partition_indices"]["misc_systems_core"]),
        )
        self.assertEqual(15, misc_systems_closure["target_count"])
        self.assertEqual(174, misc_systems_closure["source_declaration_count"])
        self.assertTrue(misc_systems_closure["exact_disjoint_source_partition"])
        self.assertTrue(misc_systems_closure["full_hvac_source_partition"])
        self.assertEqual(
            {
                "appenders_controllers": 24,
                "misc_systems_core": 15,
                "out_of_scope": 6,
                "resolved": 21,
                "source_tower_core": 59,
                "supply_core": 49,
            },
            misc_systems_closure["partition_counts"],
        )
        self.assertEqual(
            set(range(641, 815)),
            set().union(
                *(
                    set(indices)
                    for indices in misc_systems_closure["partition_indices"].values()
                )
            ),
        )
        self.assertEqual(
            {"equivalent": 7, "exception": 8},
            misc_systems_contract["classification_counts"],
        )
        self.assertEqual(6, len(misc_systems_fixture["cases"]))
        self.assertFalse(
            misc_systems_contract["evidence_contract"][
                "active_energyplus_process_claim"
            ]
        )
        self.assertFalse(
            misc_systems_contract["evidence_contract"][
                "native_runtime_executed_by_python_oracle"
            ]
        )
        self.assertFalse(
            misc_systems_contract["evidence_contract"][
                "photovoltaic_index_761_emission_executed"
            ]
        )
        self.assertTrue(
            misc_systems_fixture["native_review"][
                "domestic_hot_water_direct_public_api_only"
            ]
        )
        self.assertTrue(
            misc_systems_fixture["native_review"][
                "energy_recovery_ventilator_public_aggregate_route"
            ]
        )
        self.assertTrue(
            misc_systems_fixture["native_review"]["photovoltaic_public_api_only"]
        )
        self.assertFalse(
            misc_systems_fixture["native_review"]["internal_generate_route_claimed"]
        )

        misc_systems_case_by_symbol = {}
        for case in misc_systems_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, misc_systems_case_by_symbol)
                misc_systems_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(misc_systems_contract["classifications"]),
            set(misc_systems_case_by_symbol),
        )

        misc_systems_test_bytes = (
            REPOSITORY_ROOT / misc_systems_test_path
        ).read_bytes()
        misc_systems_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            misc_systems_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(misc_systems_direct_receipt_hash_block)
        assert misc_systems_direct_receipt_hash_block is not None
        misc_systems_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                misc_systems_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(15, len(misc_systems_direct_receipt_hashes))
        self.assertEqual(15, len(set(misc_systems_direct_receipt_hashes)))

        misc_systems_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            misc_systems_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(misc_systems_collector_output_hash_block)
        assert misc_systems_collector_output_hash_block is not None
        misc_systems_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                misc_systems_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(15, len(misc_systems_collector_output_hashes))
        self.assertEqual(15, len(set(misc_systems_collector_output_hashes)))

        def expected_misc_systems_implementation(
            symbol: str,
            native_route: str,
        ) -> tuple[str, str]:
            owner = symbol.split(".", 1)[0]
            if symbol == "EnergyRecoveryVentilator.to_idf_object":
                return (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs",
                    "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument",
                )
            if owner == "DomesticHotWater":
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "DomesticHotWater.cs"
                )
            else:
                self.assertIn(owner, {"EnergyRecoveryVentilator", "PhotoVoltaicPanel"})
                implementation_path = (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/"
                    "VentilationAndPv.cs"
                )
            if ".__init__" in symbol or "." not in symbol:
                native_owner = {
                    "DomesticHotWater": "DomesticHotWater",
                    "EnergyRecoveryVentilator": "EnergyRecoveryVentilator",
                    "PhotoVoltaicPanel": "PhotovoltaicPanel",
                }[owner]
                implementation_symbol = f"GonieGonie.InvisibleDragon.Hvac.{native_owner}"
            else:
                implementation_symbol = native_route.split("(", 1)[0]
            return implementation_path, implementation_symbol

        self.assertEqual(
            {
                item["symbol"]
                for item in misc_systems_targets
                if misc_systems_contract["classifications"][item["symbol"]]
                == "exception"
            },
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in misc_systems_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            misc_systems_targets,
            misc_systems_direct_receipt_hashes,
            misc_systems_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)

            raw_assertion_id = misc_systems_contract["assertion_ids"][symbol]
            registry_id = re.sub(r"[^a-z0-9]+", "-", raw_assertion_id).strip("-")
            self.assertRegex(registry_id, r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            classification = misc_systems_contract["classifications"][symbol]
            adaptation_family = misc_systems_contract["adaptations"][symbol]
            native_route = misc_systems_contract["native_routes"][symbol]
            code, case_id = misc_systems_case_by_symbol[symbol]
            exception_id = (
                f"{adaptation_family}-{index}"
                if classification == "exception"
                else None
            )

            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [f"upstream/symbol-evidence.json#{registry_id}"]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_misc_systems_implementation(symbol, native_route)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(registry_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(misc_systems_test_path, receipt.test_path, symbol)
            self.assertEqual(misc_systems_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(
                misc_systems_test_sha256,
                receipt.test_source_sha256,
                symbol,
            )
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                "Oracle commit c99f216",
                "commit 597bf21",
                misc_systems_fixture_sha256,
                misc_systems_generator_sha256,
                misc_systems_validator_sha256,
                misc_systems_test_sha256,
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                raw_assertion_id,
                registry_id,
                native_route,
                code,
                case_id,
                "No active EnergyPlus process or internal generate route is claimed.",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)

            if exception_id is not None:
                self.assertIn(adaptation_family, entry.rationale, symbol)
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale),
                    exception.effects,
                )
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )

        self.assertEqual(
            set(misc_systems_contract["classifications"]),
            {item.symbol for item in misc_systems_evidence_entries},
        )
        support_entry = compatibility.matrix.entries[761]
        self.assertEqual(
            ("src/idragon/dragon/hvac.py", "PhotoVoltaicPanel.to_idf_object"),
            support_entry.key,
        )
        self.assertEqual("exception", support_entry.classification)
        self.assertEqual(
            "compact-native-photovoltaic-idf-emission",
            support_entry.exception_id,
        )
        self.assertNotIn(761, misc_systems_target_indices)
        self.assertFalse(misc_systems_fixture["support"]["target_promoted"])
        self.assertEqual(
            "sha256:07c383c316989ccb22ac3eadcf9d8388764f76effbbf03c13b7a54f8af20f22b",
            misc_systems_fixture["support"]["sha256"],
        )
        self.assertEqual(
            0,
            sum(
                entry.path == "src/idragon/dragon/hvac.py"
                and entry.classification == "needs_reverification"
                for entry in compatibility.matrix.entries
            ),
        )

        imugi_idd_definitions_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_imugi_idd_definitions_core_oracle.py"
        )
        imugi_idd_definitions_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_imugi_idd_definitions_core_oracle.py"
        )
        imugi_idd_definitions_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idd/"
            "ImugiIddDefinitionsCoreOracleParityTests.cs"
        )
        imugi_idd_definitions_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Idd."
            "ImugiIddDefinitionsCoreOracleParityTests."
            "MatchesPinnedImugiIddDefinitionsThroughPublicProductionApis"
        )
        imugi_idd_definitions_fixture_sha256 = (
            "sha256:3e56e7fe6026fef3146a62aadf3248940c65aa9a2b5c624b519fbc0e3d99dd69"
        )
        imugi_idd_definitions_generator_sha256 = (
            "sha256:fa70dfc565a30542f58697cee512701356cf2200b3f07332de4e345f0b7b1398"
        )
        imugi_idd_definitions_validator_sha256 = (
            "sha256:b797ab5cb57509672d644bdc733ff2b8bd8534c4d697972f7722b944a7ff66f9"
        )
        imugi_idd_definitions_test_sha256 = (
            "sha256:86b20cc221c58489f4815fcfd591e9635a197d9ba560bff40fa7425b1f9b320c"
        )
        for pinned_path, expected_sha256 in (
            (
                imugi_idd_definitions_fixture_path,
                imugi_idd_definitions_fixture_sha256,
            ),
            (
                imugi_idd_definitions_generator_path,
                imugi_idd_definitions_generator_sha256,
            ),
            (
                imugi_idd_definitions_validator_path,
                imugi_idd_definitions_validator_sha256,
            ),
            (
                REPOSITORY_ROOT / imugi_idd_definitions_test_path,
                imugi_idd_definitions_test_sha256,
            ),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        self.assertEqual(
            (
                1123,
                1124,
                1125,
                *range(1128, 1148),
                1148,
                1149,
                1150,
                *range(1153, 1167),
            ),
            imugi_idd_definitions_target_indices,
        )
        imugi_idd_definitions_closure = imugi_idd_definitions_contract["closure"]
        self.assertEqual(
            imugi_idd_definitions_target_indices,
            tuple(imugi_idd_definitions_closure["target_indices"]),
        )
        self.assertEqual(
            tuple(item["symbol"] for item in imugi_idd_definitions_targets),
            tuple(imugi_idd_definitions_closure["target_symbols"]),
        )
        self.assertEqual(40, imugi_idd_definitions_closure["target_count"])
        self.assertEqual(65, imugi_idd_definitions_closure["deferred_count"])
        self.assertEqual(28, imugi_idd_definitions_closure["out_of_scope_count"])
        self.assertEqual(
            133,
            imugi_idd_definitions_closure["source_declaration_count"],
        )
        self.assertTrue(
            imugi_idd_definitions_closure["exact_one_case_target_partition"]
        )
        self.assertTrue(imugi_idd_definitions_closure["full_imugi_source_partition"])
        self.assertEqual(
            set(range(1095, 1228)),
            set(imugi_idd_definitions_target_indices)
            | set(imugi_idd_definitions_closure["deferred_indices"])
            | set(imugi_idd_definitions_closure["out_of_scope_indices"]),
        )
        self.assertFalse(
            set(imugi_idd_definitions_target_indices)
            & set(imugi_idd_definitions_closure["deferred_indices"])
        )
        self.assertFalse(
            set(imugi_idd_definitions_target_indices)
            & set(imugi_idd_definitions_closure["out_of_scope_indices"])
        )
        self.assertFalse(
            set(imugi_idd_definitions_closure["deferred_indices"])
            & set(imugi_idd_definitions_closure["out_of_scope_indices"])
        )
        self.assertEqual(
            {"equivalent": 18, "exception": 22},
            imugi_idd_definitions_contract["classification_counts"],
        )
        self.assertEqual(8, imugi_idd_definitions_contract["case_count"])
        self.assertEqual(8, len(imugi_idd_definitions_fixture["cases"]))
        imugi_idd_definitions_evidence_contract = (
            imugi_idd_definitions_contract["evidence_contract"]
        )
        self.assertEqual(
            40,
            imugi_idd_definitions_evidence_contract["expected_receipt_count"],
        )
        self.assertTrue(
            imugi_idd_definitions_evidence_contract["target_coverage_complete"]
        )
        self.assertTrue(
            imugi_idd_definitions_evidence_contract["exact_cpython_behavior_oracle"]
        )
        self.assertTrue(
            imugi_idd_definitions_evidence_contract[
                "path_independent_relocated_import"
            ]
        )
        self.assertTrue(
            imugi_idd_definitions_evidence_contract[
                "full_energyplus_idd_support_hash_pinned"
            ]
        )
        self.assertFalse(
            imugi_idd_definitions_evidence_contract[
                "active_energyplus_process_claim"
            ]
        )
        self.assertFalse(
            imugi_idd_definitions_evidence_contract[
                "native_runtime_executed_by_python_oracle"
            ]
        )
        self.assertTrue(
            imugi_idd_definitions_fixture["native_review"][
                "public_production_routes_only"
            ]
        )
        self.assertFalse(
            imugi_idd_definitions_fixture["native_review"][
                "python_executes_native_runtime"
            ]
        )

        imugi_idd_definitions_support = imugi_idd_definitions_fixture["support"]
        self.assertEqual(
            {
                "bytes": 585482,
                "path": "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz",
                "sha256": "sha256:f2dfc27d39f788f945ef5cc3b79ffce2a516a568075717bd67088d900a75c705",
            },
            imugi_idd_definitions_support["fixture"],
        )
        self.assertEqual(
            {
                "bytes": 38634,
                "path": "tools/python-reference/generate_idd_schema_oracle.py",
                "sha256": "sha256:64986549c0e3a3aadfef16606396006257d1be4e3b301058098ce364db8391f0",
            },
            imugi_idd_definitions_support["generator"],
        )
        self.assertEqual(
            "sha256:7e37ecb64566277e54a8c406dffd8df81517df6babfecba1a5a6feb6a9ba15af",
            imugi_idd_definitions_support["full_schema_identity_sha256"],
        )
        for support_receipt in (
            imugi_idd_definitions_support["fixture"],
            imugi_idd_definitions_support["generator"],
        ):
            self.assertEqual(
                support_receipt["sha256"],
                "sha256:"
                + hashlib.sha256(
                    (REPOSITORY_ROOT / support_receipt["path"]).read_bytes()
                ).hexdigest(),
                support_receipt["path"],
            )

        imugi_idd_definitions_native_sources = {
            item["path"]: item
            for item in imugi_idd_definitions_fixture["native_review"][
                "source_receipts"
            ]
        }
        for native_path, expected_receipt in {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddDefinitions.cs": {
                "bytes": 13005,
                "sha256": "sha256:5e716db28821b68ae147ab0700380fdc6d406bb2666367903f3c12c2b54427ed",
            },
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddParser.cs": {
                "bytes": 19960,
                "sha256": "sha256:0f932fe250ca0e63b8734032abc34adf98c31ade16405caa547f5ac67c76823f",
            },
        }.items():
            self.assertEqual(
                {"path": native_path, **expected_receipt},
                imugi_idd_definitions_native_sources[native_path],
            )
            self.assertEqual(
                expected_receipt["sha256"],
                "sha256:"
                + hashlib.sha256((REPOSITORY_ROOT / native_path).read_bytes()).hexdigest(),
                native_path,
            )

        imugi_idd_definitions_case_by_symbol = {}
        for case in imugi_idd_definitions_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, imugi_idd_definitions_case_by_symbol)
                imugi_idd_definitions_case_by_symbol[symbol] = (
                    case["code"],
                    case["id"],
                )
        self.assertEqual(
            set(imugi_idd_definitions_contract["classifications"]),
            set(imugi_idd_definitions_case_by_symbol),
        )

        imugi_idd_definitions_test_bytes = (
            REPOSITORY_ROOT / imugi_idd_definitions_test_path
        ).read_bytes()
        imugi_idd_definitions_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            imugi_idd_definitions_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(imugi_idd_definitions_direct_receipt_hash_block)
        assert imugi_idd_definitions_direct_receipt_hash_block is not None
        imugi_idd_definitions_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                imugi_idd_definitions_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(40, len(imugi_idd_definitions_direct_receipt_hashes))
        self.assertEqual(40, len(set(imugi_idd_definitions_direct_receipt_hashes)))

        imugi_idd_definitions_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            imugi_idd_definitions_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(imugi_idd_definitions_collector_output_hash_block)
        assert imugi_idd_definitions_collector_output_hash_block is not None
        imugi_idd_definitions_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                imugi_idd_definitions_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(40, len(imugi_idd_definitions_collector_output_hashes))
        self.assertEqual(40, len(set(imugi_idd_definitions_collector_output_hashes)))

        def expected_imugi_idd_definitions_implementation(
            symbol: str,
            native_route: str,
        ) -> tuple[str, str]:
            if symbol.endswith(".from_text"):
                return (
                    "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddParser.cs",
                    "GonieGonie.InvisibleDragon.Idd.IddParser.Parse",
                )
            implementation_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/"
                "IddDefinitions.cs"
            )
            owner = (
                "IddFieldDefinition"
                if symbol.startswith("IddField")
                else "IddObjectDefinition"
            )
            if "." not in symbol or symbol.endswith((".__init__", ".__eq__")):
                return (
                    implementation_path,
                    f"GonieGonie.InvisibleDragon.Idd.{owner}",
                )
            implementation_symbol = native_route
            for separator in (
                " projection",
                " public properties",
                "(...) constructor",
                " and ",
            ):
                implementation_symbol = implementation_symbol.split(separator, 1)[0]
            return implementation_path, implementation_symbol

        self.assertEqual(
            {
                item["symbol"]
                for item in imugi_idd_definitions_targets
                if imugi_idd_definitions_contract["classifications"][item["symbol"]]
                == "exception"
            },
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in imugi_idd_definitions_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            imugi_idd_definitions_targets,
            imugi_idd_definitions_direct_receipt_hashes,
            imugi_idd_definitions_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)

            assertion_id = imugi_idd_definitions_contract["assertion_ids"][symbol]
            self.assertRegex(assertion_id, r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            classification = imugi_idd_definitions_contract["classifications"][symbol]
            exception_id = imugi_idd_definitions_contract["adaptations"].get(symbol)
            native_route = imugi_idd_definitions_contract["native_routes"][symbol]
            code, case_id = imugi_idd_definitions_case_by_symbol[symbol]

            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_imugi_idd_definitions_implementation(symbol, native_route)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(imugi_idd_definitions_test_path, receipt.test_path, symbol)
            self.assertEqual(imugi_idd_definitions_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(
                imugi_idd_definitions_test_sha256,
                receipt.test_source_sha256,
                symbol,
            )
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                "Oracle commit f208041",
                "commit adcda65",
                imugi_idd_definitions_fixture_sha256,
                imugi_idd_definitions_generator_sha256,
                imugi_idd_definitions_validator_sha256,
                imugi_idd_definitions_test_sha256,
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                code,
                case_id,
                f"Only inventory index {index}",
                "all previous receipts and every non-target Imugi, InvisibleDragon HVAC, SimpleDragon, and adjacent symbol remain unchanged.",
                "No active EnergyPlus process, internal native route, or broad Python source/API compatibility is claimed.",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)

            if exception_id is not None:
                self.assertIn(exception_id, entry.rationale, symbol)
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale),
                    exception.effects,
                )
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )

        self.assertEqual(
            set(imugi_idd_definitions_contract["classifications"]),
            {item.symbol for item in imugi_idd_definitions_evidence_entries},
        )
        self.assertTrue(
            all(
                compatibility.matrix.entries[index].classification
                == "needs_reverification"
                for index in imugi_idd_definitions_closure["deferred_indices"]
                if index not in set(imugi_idd_schema_static_target_indices)
                and index not in set(imugi_idf_object_target_indices)
                and index not in set(imugi_idf_object_list_target_indices)
            )
        )
        self.assertTrue(
            all(
                compatibility.matrix.entries[index].classification == "out_of_scope"
                for index in imugi_idd_definitions_closure["out_of_scope_indices"]
            )
        )
        self.assertEqual(
            {
                "equivalent": 37,
                "exception": 68,
                "needs_reverification": 0,
                "out_of_scope": 28,
            },
            {
                classification: sum(
                    compatibility.matrix.entries[index].classification
                    == classification
                    for index in range(1095, 1228)
                )
                for classification in (
                    "equivalent",
                    "exception",
                    "needs_reverification",
                    "out_of_scope",
                )
            },
        )

        imugi_idd_schema_static_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_imugi_idd_schema_static_core_oracle.py"
        )
        imugi_idd_schema_static_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_imugi_idd_schema_static_core_oracle.py"
        )
        imugi_idd_schema_static_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idd/"
            "ImugiIddSchemaStaticCoreOracleParityTests.cs"
        )
        imugi_idd_schema_static_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Idd."
            "ImugiIddSchemaStaticCoreOracleParityTests."
            "MatchesPinnedImugiIddSchemaStaticSemanticsThroughPublicProductionApis"
        )
        imugi_idd_schema_static_fixture_sha256 = (
            "sha256:86f8dedc692e58dd7f3836d295a78bd9a9ef3dd71e84dee75be6ef44f228eea0"
        )
        imugi_idd_schema_static_generator_sha256 = (
            "sha256:aae0ce640c69f571dda0e82b0a02e303505a22331a96083115174421a15f1a83"
        )
        imugi_idd_schema_static_validator_sha256 = (
            "sha256:e2029fe7810eeaa4ad046a6102926245740ad1e2ed11a746ba45c57f2909b242"
        )
        imugi_idd_schema_static_test_sha256 = (
            "sha256:69d5187942a9da3b20fec19ef225685e7f07a47861bc0956aa7d4e57dfa29208"
        )
        for pinned_path, expected_sha256 in (
            (
                imugi_idd_schema_static_fixture_path,
                imugi_idd_schema_static_fixture_sha256,
            ),
            (
                imugi_idd_schema_static_generator_path,
                imugi_idd_schema_static_generator_sha256,
            ),
            (
                imugi_idd_schema_static_validator_path,
                imugi_idd_schema_static_validator_sha256,
            ),
            (
                REPOSITORY_ROOT / imugi_idd_schema_static_test_path,
                imugi_idd_schema_static_test_sha256,
            ),
        ):
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(pinned_path.read_bytes()).hexdigest(),
                pinned_path,
            )

        self.assertEqual(
            (1095, 1097, *range(1100, 1108), *range(1217, 1228)),
            imugi_idd_schema_static_target_indices,
        )
        imugi_idd_schema_static_closure = imugi_idd_schema_static_contract[
            "closure"
        ]
        self.assertEqual(
            imugi_idd_schema_static_target_indices,
            tuple(imugi_idd_schema_static_closure["target_indices"]),
        )
        self.assertEqual(
            tuple(item["symbol"] for item in imugi_idd_schema_static_targets),
            tuple(imugi_idd_schema_static_closure["target_symbols"]),
        )
        self.assertEqual(21, imugi_idd_schema_static_closure["target_count"])
        self.assertEqual(
            40,
            imugi_idd_schema_static_closure["batch1_resolved_count"],
        )
        self.assertEqual(44, imugi_idd_schema_static_closure["deferred_count"])
        self.assertEqual(28, imugi_idd_schema_static_closure["out_of_scope_count"])
        self.assertEqual(
            133,
            imugi_idd_schema_static_closure["source_declaration_count"],
        )
        self.assertTrue(
            imugi_idd_schema_static_closure["exact_one_case_target_partition"]
        )
        self.assertTrue(
            imugi_idd_schema_static_closure["full_imugi_source_partition"]
        )
        self.assertTrue(
            imugi_idd_schema_static_closure["matrix_batch1_promotion_deferred"]
        )
        self.assertEqual(
            imugi_idd_definitions_target_indices,
            tuple(imugi_idd_schema_static_closure["batch1_resolved_indices"]),
        )
        self.assertEqual(
            set(range(1095, 1228)),
            set(imugi_idd_schema_static_target_indices)
            | set(imugi_idd_schema_static_closure["batch1_resolved_indices"])
            | set(imugi_idd_schema_static_closure["deferred_indices"])
            | set(imugi_idd_schema_static_closure["out_of_scope_indices"]),
        )
        imugi_idd_schema_static_partitions = (
            set(imugi_idd_schema_static_target_indices),
            set(imugi_idd_schema_static_closure["batch1_resolved_indices"]),
            set(imugi_idd_schema_static_closure["deferred_indices"]),
            set(imugi_idd_schema_static_closure["out_of_scope_indices"]),
        )
        for left_index, left_partition in enumerate(
            imugi_idd_schema_static_partitions
        ):
            for right_partition in imugi_idd_schema_static_partitions[
                left_index + 1 :
            ]:
                self.assertFalse(left_partition & right_partition)
        self.assertEqual(
            {"equivalent": 9, "exception": 12},
            imugi_idd_schema_static_contract["classification_counts"],
        )
        self.assertEqual(8, imugi_idd_schema_static_contract["case_count"])
        self.assertEqual(8, len(imugi_idd_schema_static_fixture["cases"]))

        for receipt_group, expected_indices in (
            (
                imugi_idd_schema_static_fixture["batch1_resolved_receipts"],
                imugi_idd_schema_static_closure["batch1_resolved_indices"],
            ),
            (
                imugi_idd_schema_static_fixture["deferred_receipts"],
                imugi_idd_schema_static_closure["deferred_indices"],
            ),
            (
                imugi_idd_schema_static_fixture["out_of_scope_receipts"],
                imugi_idd_schema_static_closure["out_of_scope_indices"],
            ),
        ):
            self.assertEqual(
                tuple(expected_indices),
                tuple(item["inventory_index"] for item in receipt_group),
            )
            for receipt_descriptor in receipt_group:
                descriptor = dict(receipt_descriptor)
                index = descriptor.pop("inventory_index")
                self.assertEqual(
                    descriptor,
                    compatibility.inventory.symbols[index].to_data(),
                )

        imugi_idd_schema_static_evidence_contract = (
            imugi_idd_schema_static_contract["evidence_contract"]
        )
        self.assertEqual(
            21,
            imugi_idd_schema_static_evidence_contract["expected_receipt_count"],
        )
        self.assertTrue(
            imugi_idd_schema_static_evidence_contract["target_coverage_complete"]
        )
        self.assertTrue(
            imugi_idd_schema_static_evidence_contract[
                "exact_cpython_behavior_oracle"
            ]
        )
        self.assertTrue(
            imugi_idd_schema_static_evidence_contract[
                "path_independent_relocated_import"
            ]
        )
        self.assertTrue(
            imugi_idd_schema_static_evidence_contract[
                "full_energyplus_idd_support_hash_pinned"
            ]
        )
        self.assertFalse(imugi_idd_schema_static_evidence_contract["structural_only"])
        self.assertFalse(
            imugi_idd_schema_static_evidence_contract[
                "active_energyplus_process_claim"
            ]
        )
        self.assertFalse(
            imugi_idd_schema_static_evidence_contract[
                "native_runtime_executed_by_python_oracle"
            ]
        )
        imugi_idd_schema_static_native_review = imugi_idd_schema_static_fixture[
            "native_review"
        ]
        self.assertTrue(
            imugi_idd_schema_static_native_review["public_production_routes_only"]
        )
        self.assertFalse(
            imugi_idd_schema_static_native_review["python_executes_native_runtime"]
        )
        self.assertFalse(
            imugi_idd_schema_static_native_review[
                "python_source_compatibility_claimed"
            ]
        )
        self.assertFalse(
            imugi_idd_schema_static_native_review[
                "python_api_compatibility_claimed"
            ]
        )

        imugi_idd_schema_static_support = imugi_idd_schema_static_fixture["support"]
        self.assertEqual(
            {
                "bytes": 70965,
                "path": "tools/python-reference/generate_imugi_idd_definitions_core_oracle.py",
                "sha256": "sha256:fa70dfc565a30542f58697cee512701356cf2200b3f07332de4e345f0b7b1398",
            },
            imugi_idd_schema_static_support["base_generator"],
        )
        self.assertEqual(
            "sha256:7e37ecb64566277e54a8c406dffd8df81517df6babfecba1a5a6feb6a9ba15af",
            imugi_idd_schema_static_support["energyplus_idd"][
                "full_schema_identity_sha256"
            ],
        )
        for support_receipt in (
            imugi_idd_schema_static_support["base_generator"],
            imugi_idd_schema_static_support["energyplus_idd"]["fixture"],
            imugi_idd_schema_static_support["energyplus_idd"]["generator"],
        ):
            self.assertEqual(
                support_receipt["sha256"],
                "sha256:"
                + hashlib.sha256(
                    (REPOSITORY_ROOT / support_receipt["path"]).read_bytes()
                ).hexdigest(),
                support_receipt["path"],
            )

        imugi_idd_schema_static_native_sources = {
            item["path"]: item
            for item in imugi_idd_schema_static_native_review["source_receipts"]
        }
        for native_path, expected_receipt in {
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddDefinitions.cs": {
                "bytes": 13005,
                "sha256": "sha256:5e716db28821b68ae147ab0700380fdc6d406bb2666367903f3c12c2b54427ed",
            },
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddParser.cs": {
                "bytes": 19960,
                "sha256": "sha256:0f932fe250ca0e63b8734032abc34adf98c31ade16405caa547f5ac67c76823f",
            },
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddSchemaCache.cs": {
                "bytes": 11254,
                "sha256": "sha256:80f2e2a803128b52aec6df95b0ff2567a5b53bd51e72b1154e7c9a8a3ebf9e4b",
            },
            "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Common/EnergyPlusVersion.cs": {
                "bytes": 4954,
                "sha256": "sha256:ea908729f5517e3c9d301210f882019bc8b026da8e3055caeb187d80db86a685",
            },
        }.items():
            self.assertEqual(
                {"path": native_path, **expected_receipt},
                imugi_idd_schema_static_native_sources[native_path],
            )
            self.assertEqual(
                expected_receipt["sha256"],
                "sha256:"
                + hashlib.sha256((REPOSITORY_ROOT / native_path).read_bytes()).hexdigest(),
                native_path,
            )

        imugi_idd_schema_static_case_by_symbol = {}
        for case in imugi_idd_schema_static_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, imugi_idd_schema_static_case_by_symbol)
                imugi_idd_schema_static_case_by_symbol[symbol] = (
                    case["code"],
                    case["id"],
                )
        self.assertEqual(
            set(imugi_idd_schema_static_contract["classifications"]),
            set(imugi_idd_schema_static_case_by_symbol),
        )

        imugi_idd_schema_static_test_bytes = (
            REPOSITORY_ROOT / imugi_idd_schema_static_test_path
        ).read_bytes()
        imugi_idd_schema_static_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\[(?P<body>.*?)\n\s*\];",
            imugi_idd_schema_static_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(imugi_idd_schema_static_direct_receipt_hash_block)
        assert imugi_idd_schema_static_direct_receipt_hash_block is not None
        imugi_idd_schema_static_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                imugi_idd_schema_static_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(21, len(imugi_idd_schema_static_direct_receipt_hashes))
        self.assertEqual(21, len(set(imugi_idd_schema_static_direct_receipt_hashes)))

        imugi_idd_schema_static_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\[(?P<body>.*?)\n\s*\];",
            imugi_idd_schema_static_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(imugi_idd_schema_static_collector_output_hash_block)
        assert imugi_idd_schema_static_collector_output_hash_block is not None
        imugi_idd_schema_static_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                imugi_idd_schema_static_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(21, len(imugi_idd_schema_static_collector_output_hashes))
        self.assertEqual(21, len(set(imugi_idd_schema_static_collector_output_hashes)))

        def expected_imugi_idd_schema_static_implementation(
            symbol: str,
            native_route: str,
        ) -> tuple[str, str]:
            definitions_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/"
                "IddDefinitions.cs"
            )
            parser_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/"
                "IddParser.cs"
            )
            cache_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/"
                "IddSchemaCache.cs"
            )
            if symbol == "IDD.load":
                return (
                    cache_path,
                    "GonieGonie.InvisibleDragon.Idd.IddSchemaCache.Read",
                )
            if symbol == "IDD.to_pickle":
                return (
                    cache_path,
                    "GonieGonie.InvisibleDragon.Idd.IddSchemaCache.Write",
                )
            if symbol == "IDD.read_idd":
                return (
                    parser_path,
                    "GonieGonie.InvisibleDragon.Idd.IddParser.ParseFile",
                )
            if symbol == "VersionIdentificationError":
                return (
                    parser_path,
                    "GonieGonie.InvisibleDragon.Idd.IddParser.Parse",
                )
            if symbol == "InvalidFieldValue":
                return (
                    definitions_path,
                    "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition",
                )
            if symbol == "InvalidParentManagement":
                return (
                    definitions_path,
                    "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition",
                )
            if symbol in {
                "IDD",
                "IDD.__init__",
                "StaticIndexedDict",
                "StaticIndexedDict.__getitem__",
                "StaticIndexedDict.__init__",
            }:
                return (
                    definitions_path,
                    "GonieGonie.InvisibleDragon.Idd.IddSchema",
                )
            if symbol.startswith("StaticIndexedDict."):
                return (
                    definitions_path,
                    "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects",
                )
            implementation_symbol = native_route
            for separator in ("/", " projection", "(...) constructor", " and "):
                implementation_symbol = implementation_symbol.split(separator, 1)[0]
            return definitions_path, implementation_symbol

        self.assertEqual(
            {
                item["symbol"]
                for item in imugi_idd_schema_static_targets
                if imugi_idd_schema_static_contract["classifications"][item["symbol"]]
                == "exception"
            },
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in imugi_idd_schema_static_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            imugi_idd_schema_static_targets,
            imugi_idd_schema_static_direct_receipt_hashes,
            imugi_idd_schema_static_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)

            assertion_id = imugi_idd_schema_static_contract["assertion_ids"][symbol]
            self.assertRegex(assertion_id, r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            classification = imugi_idd_schema_static_contract["classifications"][symbol]
            exception_id = imugi_idd_schema_static_contract["adaptations"].get(symbol)
            native_route = imugi_idd_schema_static_contract["native_routes"][symbol]
            code, case_id = imugi_idd_schema_static_case_by_symbol[symbol]

            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [
                f"upstream/symbol-evidence.json#{assertion_id}"
            ]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_imugi_idd_schema_static_implementation(symbol, native_route)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path,
                evidence_entry.implementation_path,
                symbol,
            )
            self.assertEqual(
                implementation_symbol,
                evidence_entry.implementation_symbol,
                symbol,
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(imugi_idd_schema_static_test_path, receipt.test_path, symbol)
            self.assertEqual(imugi_idd_schema_static_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(
                imugi_idd_schema_static_test_sha256,
                receipt.test_source_sha256,
                symbol,
            )
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                "Oracle commit 2fa8cf5",
                "commit 20135d0",
                imugi_idd_schema_static_fixture_sha256,
                imugi_idd_schema_static_generator_sha256,
                imugi_idd_schema_static_validator_sha256,
                imugi_idd_schema_static_test_sha256,
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                code,
                case_id,
                f"Only inventory index {index}",
                "all previous receipts, batch1 IDD definition evidence, and every non-target Imugi, InvisibleDragon HVAC, SimpleDragon, and adjacent symbol remain unchanged.",
                "No active EnergyPlus process, internal native route, or broad Python source/API compatibility is claimed.",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)

            if exception_id is not None:
                self.assertIn(exception_id, entry.rationale, symbol)
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale),
                    exception.effects,
                )
                self.assertEqual(
                    "accepted-native-api-adaptation",
                    exception.approval,
                    symbol,
                )

        self.assertEqual(
            set(imugi_idd_schema_static_contract["classifications"]),
            {item.symbol for item in imugi_idd_schema_static_evidence_entries},
        )
        self.assertTrue(
            all(
                compatibility.matrix.entries[index].classification
                == "needs_reverification"
                for index in imugi_idd_schema_static_closure["deferred_indices"]
                if index not in set(imugi_idf_object_target_indices)
                and index not in set(imugi_idf_object_list_target_indices)
            )
        )
        self.assertTrue(
            all(
                compatibility.matrix.entries[index].classification == "out_of_scope"
                for index in imugi_idd_schema_static_closure["out_of_scope_indices"]
            )
        )
        self.assertEqual(
            {
                "equivalent": 37,
                "exception": 68,
                "needs_reverification": 0,
                "out_of_scope": 28,
            },
            {
                classification: sum(
                    compatibility.matrix.entries[index].classification
                    == classification
                    for index in range(1095, 1228)
                )
                for classification in (
                    "equivalent",
                    "exception",
                    "needs_reverification",
                    "out_of_scope",
                )
            },
        )

        imugi_idf_object_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_imugi_idf_object_core_oracle.py"
        )
        imugi_idf_object_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_imugi_idf_object_core_oracle.py"
        )
        imugi_idf_object_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idf/"
            "ImugiIdfObjectCoreOracleParityTests.cs"
        )
        imugi_idf_object_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Idf."
            "ImugiIdfObjectCoreOracleParityTests."
            "MatchesPinnedImugiIdfObjectThroughPublicProductionApis"
        )
        imugi_idf_object_fixture_sha256 = (
            "sha256:7237e974d6d938c6f8f7215661f54db4f26a2a7afc664765b895656a7720babd"
        )
        imugi_idf_object_generator_sha256 = (
            "sha256:8589497feab58cc9d9c05479c50264a091182c2d68531398d1decddd24f7cc43"
        )
        imugi_idf_object_validator_sha256 = (
            "sha256:5c296ed4b6129dfbb40523136f91877169191a4ff42b5be63411d46bc91e5c73"
        )
        imugi_idf_object_test_sha256 = (
            "sha256:7e5826a6cd5bfe5227a86a0fc0a7b840960a8175944c11bc32136958bbcab67f"
        )
        for pinned_path, expected_bytes, expected_sha256 in (
            (
                imugi_idf_object_fixture_path,
                119205,
                imugi_idf_object_fixture_sha256,
            ),
            (
                imugi_idf_object_generator_path,
                30116,
                imugi_idf_object_generator_sha256,
            ),
            (
                imugi_idf_object_validator_path,
                11115,
                imugi_idf_object_validator_sha256,
            ),
            (
                REPOSITORY_ROOT / imugi_idf_object_test_path,
                37982,
                imugi_idf_object_test_sha256,
            ),
        ):
            content = pinned_path.read_bytes()
            self.assertEqual(expected_bytes, len(content), pinned_path)
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(content).hexdigest(),
                pinned_path,
            )

        self.assertEqual(
            (
                1108,
                1109,
                *range(1112, 1117),
                1118,
                1119,
                1121,
                1122,
                1167,
                1170,
                1171,
                *range(1173, 1184),
            ),
            imugi_idf_object_target_indices,
        )
        imugi_idf_object_closure = imugi_idf_object_contract["closure"]
        self.assertEqual(
            imugi_idf_object_target_indices,
            tuple(imugi_idf_object_closure["target_indices"]),
        )
        self.assertEqual(25, imugi_idf_object_closure["target_count"])
        self.assertEqual(40, imugi_idf_object_closure["batch1_count"])
        self.assertEqual(21, imugi_idf_object_closure["batch2_count"])
        self.assertEqual(19, imugi_idf_object_closure["batch4_count"])
        self.assertEqual(28, imugi_idf_object_closure["out_of_scope_count"])
        self.assertEqual(133, imugi_idf_object_closure["source_declaration_count"])
        self.assertTrue(imugi_idf_object_closure["exact_disjoint_source_partition"])
        self.assertEqual(
            imugi_idd_definitions_target_indices,
            tuple(imugi_idf_object_closure["batch1_indices"]),
        )
        self.assertEqual(
            imugi_idd_schema_static_target_indices,
            tuple(imugi_idf_object_closure["batch2_indices"]),
        )
        imugi_idf_object_partitions = tuple(
            set(imugi_idf_object_closure[name])
            for name in (
                "target_indices",
                "batch1_indices",
                "batch2_indices",
                "batch4_indices",
                "out_of_scope_indices",
            )
        )
        self.assertEqual(
            set(range(1095, 1228)),
            set().union(*imugi_idf_object_partitions),
        )
        for left_index, left_partition in enumerate(imugi_idf_object_partitions):
            for right_partition in imugi_idf_object_partitions[left_index + 1 :]:
                self.assertFalse(left_partition & right_partition)
        self.assertEqual(
            {"equivalent": 6, "exception": 19},
            imugi_idf_object_contract["classification_counts"],
        )
        self.assertEqual(7, len(imugi_idf_object_fixture["cases"]))

        imugi_idf_object_evidence_contract = imugi_idf_object_contract[
            "evidence_contract"
        ]
        self.assertEqual(
            25,
            imugi_idf_object_evidence_contract["expected_receipt_count"],
        )
        self.assertTrue(
            imugi_idf_object_evidence_contract["path_independent_relocated_import"]
        )
        for false_claim in (
            "structural_only",
            "active_energyplus_process_claim",
            "native_runtime_executed_by_python_oracle",
            "python_api_or_source_compatibility_claim",
        ):
            self.assertFalse(imugi_idf_object_evidence_contract[false_claim])
        imugi_idf_object_native_review = imugi_idf_object_fixture["native_review"]
        self.assertTrue(
            imugi_idf_object_native_review["public_production_routes_only"]
        )
        self.assertFalse(imugi_idf_object_native_review["python_executes_native_runtime"])
        self.assertTrue(
            imugi_idf_object_native_review[
                "no_python_api_or_source_compatibility_claim"
            ]
        )
        for source_receipt in imugi_idf_object_native_review["sources"]:
            source_content = (REPOSITORY_ROOT / source_receipt["path"]).read_bytes()
            self.assertEqual(source_receipt["bytes"], len(source_content))
            self.assertEqual(
                source_receipt["sha256"],
                "sha256:" + hashlib.sha256(source_content).hexdigest(),
                source_receipt["path"],
            )

        imugi_idf_object_case_by_symbol = {}
        for case in imugi_idf_object_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, imugi_idf_object_case_by_symbol)
                imugi_idf_object_case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(imugi_idf_object_contract["classifications"]),
            set(imugi_idf_object_case_by_symbol),
        )

        imugi_idf_object_test_bytes = (
            REPOSITORY_ROOT / imugi_idf_object_test_path
        ).read_bytes()
        imugi_idf_object_direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            imugi_idf_object_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(imugi_idf_object_direct_receipt_hash_block)
        assert imugi_idf_object_direct_receipt_hash_block is not None
        imugi_idf_object_direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                imugi_idf_object_direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(25, len(imugi_idf_object_direct_receipt_hashes))
        self.assertEqual(25, len(set(imugi_idf_object_direct_receipt_hashes)))

        imugi_idf_object_collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            imugi_idf_object_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(imugi_idf_object_collector_output_hash_block)
        assert imugi_idf_object_collector_output_hash_block is not None
        imugi_idf_object_collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                imugi_idf_object_collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(25, len(imugi_idf_object_collector_output_hashes))
        self.assertEqual(25, len(set(imugi_idf_object_collector_output_hashes)))

        def expected_imugi_idf_object_implementation(
            symbol: str,
        ) -> tuple[str, str]:
            model_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/"
                "IdfModel.cs"
            )
            parser_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/"
                "IdfParser.cs"
            )
            writer_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/"
                "IdfWriter.cs"
            )
            if symbol in {"IDF.__str__", "IdfObject.__str__"}:
                return writer_path, "GonieGonie.InvisibleDragon.Idf.IdfWriter.Write"
            if symbol == "IDF.append":
                return model_path, "GonieGonie.InvisibleDragon.Idf.IdfDocument.Append"
            if symbol == "IDF.read_idf":
                return parser_path, "GonieGonie.InvisibleDragon.Idf.IdfParser.ParseFile"
            if symbol == "IdfObject.idd":
                return model_path, "GonieGonie.InvisibleDragon.Idf.IdfObject.Definition"
            if symbol == "IDF" or symbol.startswith("IDF."):
                return model_path, "GonieGonie.InvisibleDragon.Idf.IdfDocument"
            return model_path, "GonieGonie.InvisibleDragon.Idf.IdfObject"

        self.assertEqual(
            "GonieGonie.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)",
            imugi_idf_object_contract["native_routes"]["IDF.append"],
        )
        idf_model_text = (
            REPOSITORY_ROOT
            / "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/IdfModel.cs"
        ).read_text(encoding="utf-8")
        self.assertIn("public void Append(IdfObject value)", idf_model_text)

        self.assertEqual(
            {
                item["symbol"]
                for item in imugi_idf_object_targets
                if imugi_idf_object_contract["classifications"][item["symbol"]]
                == "exception"
            },
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in imugi_idf_object_exception_ids
            },
        )
        for target, direct_receipt_hash, collector_output_hash in zip(
            imugi_idf_object_targets,
            imugi_idf_object_direct_receipt_hashes,
            imugi_idf_object_collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)

            assertion_id = imugi_idf_object_contract["assertion_ids"][symbol]
            self.assertRegex(assertion_id, r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            classification = imugi_idf_object_contract["classifications"][symbol]
            exception_id = (
                f"typed-immutable-native-idf-adaptation-{index}"
                if classification == "exception"
                else None
            )
            native_route = imugi_idf_object_contract["native_routes"][symbol]
            code, case_id = imugi_idf_object_case_by_symbol[symbol]

            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)

            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_imugi_idf_object_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path, evidence_entry.implementation_path, symbol
            )
            self.assertEqual(
                implementation_symbol, evidence_entry.implementation_symbol, symbol
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(imugi_idf_object_test_path, receipt.test_path, symbol)
            self.assertEqual(imugi_idf_object_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(
                imugi_idf_object_test_sha256,
                receipt.test_source_sha256,
                symbol,
            )
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                "Oracle commit aa53eda",
                "commit d616392",
                imugi_idf_object_fixture_sha256,
                imugi_idf_object_generator_sha256,
                imugi_idf_object_validator_sha256,
                imugi_idf_object_test_sha256,
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                code,
                case_id,
                f"Only inventory index {index}",
                "all previous receipts, batch1 IDD definition evidence, batch2 IDD schema/static evidence, and every non-target Imugi",
                "No active EnergyPlus process, internal native route, or broad Python source/API compatibility is claimed.",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                self.assertIn("typed-immutable-native-idf-adaptation", entry.rationale)
                self.assertIn(exception_id, entry.rationale)
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale), exception.effects
                )
                self.assertEqual(
                    "accepted-native-api-adaptation", exception.approval, symbol
                )

        self.assertEqual(
            set(imugi_idf_object_contract["classifications"]),
            {item.symbol for item in imugi_idf_object_evidence_entries},
        )
        self.assertTrue(
            all(
                compatibility.matrix.entries[index].classification
                in {"equivalent", "exception"}
                for index in imugi_idf_object_closure["batch4_indices"]
            )
        )
        self.assertTrue(
            all(
                compatibility.matrix.entries[index].classification == "out_of_scope"
                for index in imugi_idf_object_closure["out_of_scope_indices"]
            )
        )
        self.assertEqual(
            {
                "equivalent": 37,
                "exception": 68,
                "needs_reverification": 0,
                "out_of_scope": 28,
            },
            {
                classification: sum(
                    compatibility.matrix.entries[index].classification
                    == classification
                    for index in range(1095, 1228)
                )
                for classification in (
                    "equivalent",
                    "exception",
                    "needs_reverification",
                    "out_of_scope",
                )
            },
        )

        imugi_idf_object_list_generator_path = (
            REPOSITORY_ROOT
            / "tools/python-reference/generate_imugi_idf_object_list_core_oracle.py"
        )
        imugi_idf_object_list_validator_path = (
            REPOSITORY_ROOT
            / "tests/PythonReference/test_imugi_idf_object_list_core_oracle.py"
        )
        imugi_idf_object_list_test_path = (
            "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idf/"
            "ImugiIdfObjectListCoreOracleParityTests.cs"
        )
        imugi_idf_object_list_test_symbol = (
            "GonieGonie.InvisibleDragon.Tests.Idf."
            "ImugiIdfObjectListCoreOracleParityTests."
            "MatchesPinnedImugiIdfObjectListThroughPublicProductionApis"
        )
        imugi_idf_object_list_fixture_sha256 = (
            "sha256:6047f16dc92ae8b8e3e93daf43149ec0d8041ac15f748619e143d6efc0f7aaba"
        )
        imugi_idf_object_list_generator_sha256 = (
            "sha256:cc504d32c9b6926093185f0bb7e4c988c4bfe9b27d035330768f5f8b980fa8c4"
        )
        imugi_idf_object_list_validator_sha256 = (
            "sha256:56c31b542ec2bdefb75d7402f2dbbb32217e2634be826dae3566069b475e56ef"
        )
        imugi_idf_object_list_test_sha256 = (
            "sha256:6135638ec726c95d1d858d5dd43f1322de1e37aff5827642bd21a87adf095771"
        )
        for pinned_path, expected_bytes, expected_sha256 in (
            (
                imugi_idf_object_list_fixture_path,
                105236,
                imugi_idf_object_list_fixture_sha256,
            ),
            (
                imugi_idf_object_list_generator_path,
                22838,
                imugi_idf_object_list_generator_sha256,
            ),
            (
                imugi_idf_object_list_validator_path,
                7509,
                imugi_idf_object_list_validator_sha256,
            ),
            (
                REPOSITORY_ROOT / imugi_idf_object_list_test_path,
                23672,
                imugi_idf_object_list_test_sha256,
            ),
        ):
            content = pinned_path.read_bytes()
            self.assertEqual(expected_bytes, len(content), pinned_path)
            self.assertEqual(
                expected_sha256,
                "sha256:" + hashlib.sha256(content).hexdigest(),
                pinned_path,
            )

        self.assertEqual(
            (
                1190,
                1194,
                1195,
                *range(1197, 1200),
                1201,
                *range(1203, 1213),
                1214,
                1215,
            ),
            imugi_idf_object_list_target_indices,
        )
        self.assertEqual(
            "IdfObjectList.is_containor",
            imugi_idf_object_list_targets[14]["symbol"],
        )
        self.assertEqual(
            "IdfObjectList.is_containor",
            compatibility.inventory.symbols[1210].symbol,
        )
        self.assertNotIn(
            "IdfObjectList.is_container",
            imugi_idf_object_list_contract["classifications"],
        )

        imugi_idf_object_list_closure = imugi_idf_object_list_contract["closure"]
        self.assertEqual(
            imugi_idf_object_list_target_indices,
            tuple(imugi_idf_object_list_closure["target_indices"]),
        )
        self.assertEqual(19, imugi_idf_object_list_closure["target_count"])
        self.assertEqual(40, imugi_idf_object_list_closure["batch1_count"])
        self.assertEqual(21, imugi_idf_object_list_closure["batch2_count"])
        self.assertEqual(25, imugi_idf_object_list_closure["batch3_count"])
        self.assertEqual(28, imugi_idf_object_list_closure["out_of_scope_count"])
        self.assertEqual(133, imugi_idf_object_list_closure["source_declaration_count"])
        self.assertTrue(
            imugi_idf_object_list_closure["exact_disjoint_source_partition"]
        )
        imugi_idf_object_list_partitions = {
            name: tuple(item["inventory_index"] for item in receipts)
            for name, receipts in imugi_idf_object_list_fixture["partitions"].items()
        }
        self.assertEqual(
            imugi_idd_definitions_target_indices,
            imugi_idf_object_list_partitions["batch1"],
        )
        self.assertEqual(
            imugi_idd_schema_static_target_indices,
            imugi_idf_object_list_partitions["batch2"],
        )
        self.assertEqual(
            imugi_idf_object_target_indices,
            imugi_idf_object_list_partitions["batch3"],
        )
        self.assertEqual(
            imugi_idf_object_list_target_indices,
            imugi_idf_object_list_partitions["target"],
        )
        partition_sets = tuple(
            set(indices) for indices in imugi_idf_object_list_partitions.values()
        )
        self.assertEqual(set(range(1095, 1228)), set().union(*partition_sets))
        for left_index, left_partition in enumerate(partition_sets):
            for right_partition in partition_sets[left_index + 1 :]:
                self.assertFalse(left_partition & right_partition)
        for receipts in imugi_idf_object_list_fixture["partitions"].values():
            for descriptor in receipts:
                expected_descriptor = dict(descriptor)
                index = expected_descriptor.pop("inventory_index")
                self.assertEqual(
                    expected_descriptor,
                    compatibility.inventory.symbols[index].to_data(),
                    index,
                )
        self.assertEqual(
            {"equivalent": 4, "exception": 15},
            imugi_idf_object_list_contract["classification_counts"],
        )
        self.assertEqual(5, len(imugi_idf_object_list_fixture["cases"]))

        evidence_contract = imugi_idf_object_list_contract["evidence_contract"]
        self.assertEqual(19, evidence_contract["expected_receipt_count"])
        self.assertTrue(evidence_contract["path_independent_relocated_import"])
        for false_claim in (
            "structural_only",
            "active_energyplus_process_claim",
            "internal_native_route_claim",
            "native_runtime_executed_by_python_oracle",
            "python_api_or_source_compatibility_claim",
        ):
            self.assertFalse(evidence_contract[false_claim])
        native_review = imugi_idf_object_list_fixture["native_review"]
        self.assertTrue(native_review["public_production_routes_only"])
        self.assertTrue(native_review["no_python_api_or_source_compatibility_claim"])
        self.assertFalse(native_review["python_executes_native_runtime"])
        for source_receipt in native_review["sources"]:
            source_content = (REPOSITORY_ROOT / source_receipt["path"]).read_bytes()
            self.assertEqual(source_receipt["bytes"], len(source_content))
            self.assertEqual(
                source_receipt["sha256"],
                "sha256:" + hashlib.sha256(source_content).hexdigest(),
                source_receipt["path"],
            )
        for support_receipt in imugi_idf_object_list_fixture["support"]:
            support_content = (
                REPOSITORY_ROOT / support_receipt["path"]
            ).read_bytes()
            self.assertEqual(support_receipt["bytes"], len(support_content))
            self.assertEqual(
                support_receipt["sha256"],
                "sha256:" + hashlib.sha256(support_content).hexdigest(),
                support_receipt["path"],
            )

        case_by_symbol = {}
        for case in imugi_idf_object_list_fixture["cases"]:
            for symbol in case["target_symbols"]:
                self.assertNotIn(symbol, case_by_symbol)
                case_by_symbol[symbol] = (case["code"], case["id"])
        self.assertEqual(
            set(imugi_idf_object_list_contract["classifications"]),
            set(case_by_symbol),
        )
        native_test_bytes = (
            REPOSITORY_ROOT / imugi_idf_object_list_test_path
        ).read_bytes()
        direct_receipt_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedReceiptHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            native_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(direct_receipt_hash_block)
        assert direct_receipt_hash_block is not None
        direct_receipt_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                direct_receipt_hash_block.group("body"),
            )
        )
        self.assertEqual(19, len(direct_receipt_hashes))
        self.assertEqual(19, len(set(direct_receipt_hashes)))

        collector_output_hash_block = re.search(
            rb"private static readonly string\[\] ExpectedCollectorOutputHashes\s*=\s*"
            rb"\{(?P<body>.*?)\n\s*\};",
            native_test_bytes,
            re.DOTALL,
        )
        self.assertIsNotNone(collector_output_hash_block)
        assert collector_output_hash_block is not None
        collector_output_hashes = tuple(
            item.decode("ascii")
            for item in re.findall(
                rb'"(sha256:[0-9a-f]{64})"',
                collector_output_hash_block.group("body"),
            )
        )
        self.assertEqual(19, len(collector_output_hashes))
        self.assertEqual(19, len(set(collector_output_hashes)))

        def expected_imugi_idf_object_list_implementation(
            symbol: str,
        ) -> tuple[str, str]:
            model_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/"
                "IdfModel.cs"
            )
            validator_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/"
                "IdfValidator.cs"
            )
            writer_path = (
                "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/"
                "IdfWriter.cs"
            )
            if symbol == "IdfObjectList.__str__":
                return writer_path, "GonieGonie.InvisibleDragon.Idf.IdfWriter.Write"
            if symbol == "IdfObjectList.check_validity":
                return (
                    validator_path,
                    "GonieGonie.InvisibleDragon.Idf.IdfValidator.Validate",
                )
            if symbol == "IdfObjectList.append":
                return (
                    model_path,
                    "GonieGonie.InvisibleDragon.Idf.IdfObjectCollection.Append",
                )
            if symbol == "IdfObjectList.insert":
                return (
                    model_path,
                    "GonieGonie.InvisibleDragon.Idf.IdfObjectCollection.Insert",
                )
            if symbol == "IdfObjectList.names":
                return model_path, "GonieGonie.InvisibleDragon.Idf.IdfObject.Name"
            return (
                model_path,
                "GonieGonie.InvisibleDragon.Idf.IdfObjectCollection",
            )

        self.assertEqual(
            "GonieGonie.InvisibleDragon.Idf.IdfObjectCollection.Append(IdfObject)",
            imugi_idf_object_list_contract["native_routes"][
                "IdfObjectList.append"
            ],
        )
        self.assertEqual(
            "GonieGonie.InvisibleDragon.Idf.IdfObjectCollection.Insert(int, IdfObject)",
            imugi_idf_object_list_contract["native_routes"][
                "IdfObjectList.insert"
            ],
        )
        self.assertEqual(
            {
                item["symbol"]
                for item in imugi_idf_object_list_targets
                if imugi_idf_object_list_contract["classifications"][item["symbol"]]
                == "exception"
            },
            {
                item.upstream_symbol
                for item in configuration.exceptions
                if item.identifier in imugi_idf_object_list_exception_ids
            },
        )

        for target, direct_receipt_hash, collector_output_hash in zip(
            imugi_idf_object_list_targets,
            direct_receipt_hashes,
            collector_output_hashes,
            strict=True,
        ):
            index = target["inventory_index"]
            symbol = target["symbol"]
            key = (target["path"], symbol)
            inventory_symbol = compatibility.inventory.symbols[index]
            expected_descriptor = dict(target)
            expected_descriptor.pop("inventory_index")
            self.assertEqual(expected_descriptor, inventory_symbol.to_data(), symbol)
            assertion_id = imugi_idf_object_list_contract["assertion_ids"][symbol]
            self.assertRegex(assertion_id, r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
            classification = imugi_idf_object_list_contract["classifications"][symbol]
            exception_id = (
                f"typed-native-collection-adaptation-{index}"
                if classification == "exception"
                else None
            )
            native_route = imugi_idf_object_list_contract["native_routes"][symbol]
            code, case_id = case_by_symbol[symbol]
            entry = compatibility.matrix.entries[index]
            self.assertEqual(key, entry.key, symbol)
            self.assertEqual(classification, entry.classification, symbol)
            self.assertEqual(exception_id, entry.exception_id, symbol)
            expected_references = [f"upstream/symbol-evidence.json#{assertion_id}"]
            if exception_id is not None:
                expected_references.append(
                    f"upstream/compatibility-exceptions.yml#{exception_id}"
                )
            self.assertEqual(tuple(sorted(expected_references)), entry.evidence, symbol)
            evidence_entry = symbol_evidence.entries_by_key[key]
            self.assertEqual(
                inventory_symbol.symbol_hash,
                evidence_entry.upstream_symbol_hash,
                symbol,
            )
            implementation_path, implementation_symbol = (
                expected_imugi_idf_object_list_implementation(symbol)
            )
            implementation_sha256 = "sha256:" + hashlib.sha256(
                (REPOSITORY_ROOT / implementation_path).read_bytes()
            ).hexdigest()
            self.assertEqual(
                implementation_path, evidence_entry.implementation_path, symbol
            )
            self.assertEqual(
                implementation_symbol, evidence_entry.implementation_symbol, symbol
            )
            self.assertEqual(
                implementation_sha256,
                evidence_entry.implementation_source_sha256,
                symbol,
            )
            self.assertEqual(1, len(evidence_entry.receipts), symbol)
            receipt = evidence_entry.receipts[0]
            self.assertEqual(assertion_id, receipt.identifier, symbol)
            self.assertEqual(entry.rationale, receipt.assertion, symbol)
            self.assertIn(direct_receipt_hash, receipt.assertion, symbol)
            self.assertEqual(
                collector_output_hash, receipt.expected_output_sha256, symbol
            )
            self.assertEqual(imugi_idf_object_list_test_path, receipt.test_path, symbol)
            self.assertEqual(imugi_idf_object_list_test_symbol, receipt.test_symbol, symbol)
            self.assertEqual(
                imugi_idf_object_list_test_sha256,
                receipt.test_source_sha256,
                symbol,
            )
            self.assertEqual("cross_language", receipt.verification_kind, symbol)
            self.assertEqual("passed", receipt.outcome, symbol)
            self.assertFalse(receipt.skipped, symbol)
            self.assertFalse(receipt.structural_only, symbol)
            self.assertFalse(receipt.claims_active_load, symbol)
            self.assertEqual("not_applicable", receipt.exercised_load, symbol)
            for exact_binding in (
                "Oracle commit db1f31e",
                "commit 9adac2b",
                imugi_idf_object_list_fixture_sha256,
                imugi_idf_object_list_generator_sha256,
                imugi_idf_object_list_validator_sha256,
                imugi_idf_object_list_test_sha256,
                implementation_path + "@" + implementation_sha256,
                direct_receipt_hash,
                collector_output_hash,
                assertion_id,
                native_route,
                code,
                case_id,
                f"Only inventory index {index}",
                "all previous receipts and batch1 IDD definition, batch2 IDD schema/static, batch3 IDF/IdfObject evidence",
                "No active EnergyPlus process, internal native route, or broad Python source/API compatibility is claimed.",
            ):
                self.assertIn(exact_binding, entry.rationale, symbol)
            if exception_id is not None:
                self.assertIn("typed-native-collection-adaptation", entry.rationale)
                self.assertIn(exception_id, entry.rationale)
                exception = exceptions_by_id[exception_id]
                self.assertEqual(target["path"], exception.upstream_path, symbol)
                self.assertEqual(symbol, exception.upstream_symbol, symbol)
                self.assertEqual(
                    inventory_symbol.symbol_hash,
                    exception.upstream_symbol_hash,
                    symbol,
                )
                self.assertIn(
                    ("engineering_result", entry.rationale), exception.effects
                )
                self.assertEqual(
                    "accepted-native-api-adaptation", exception.approval, symbol
                )

        self.assertEqual(
            set(imugi_idf_object_list_contract["classifications"]),
            {item.symbol for item in imugi_idf_object_list_evidence_entries},
        )
        self.assertEqual(
            {
                "equivalent": 37,
                "exception": 68,
                "needs_reverification": 0,
                "out_of_scope": 28,
            },
            {
                classification: sum(
                    compatibility.matrix.entries[index].classification
                    == classification
                    for index in range(1095, 1228)
                )
                for classification in (
                    "equivalent",
                    "exception",
                    "needs_reverification",
                    "out_of_scope",
                )
            },
        )
        self.assertFalse(compatibility.needs_reverification)

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

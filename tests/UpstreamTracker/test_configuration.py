from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import unittest

from support import REPOSITORY_ROOT, TemporaryWorkspace, write_configuration

from dragons_upstream_tracker.config import load_configuration
from dragons_upstream_tracker.compatibility import load_compatibility_configuration
from dragons_upstream_tracker.errors import ConfigurationError
from dragons_upstream_tracker.yaml_subset import parse_yaml_subset


class ConfigurationTests(unittest.TestCase):
    def test_repository_manifests_validate_as_one_configuration(self) -> None:
        configuration = load_configuration(
            REPOSITORY_ROOT / "upstream" / "upstream.lock.json",
            REPOSITORY_ROOT / "upstream" / "port-map.yml",
            REPOSITORY_ROOT / "upstream" / "compatibility-exceptions.yml",
        )

        self.assertEqual("dragons.upstream-lock.v1", configuration.lock.schema)
        self.assertGreater(len(configuration.mappings), 0)
        self.assertTrue(
            all(
                mapping.dotnet_project.startswith("Dragons.")
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
            "sha256:edc4f0c1b9a316d888056ec4139796ae748149e42f09a97813a313da247b5d08",
            compatibility.matrix.content_sha256,
        )
        self.assertEqual(990, len(symbol_evidence.entries))
        self.assertEqual(990, len(symbol_evidence.receipts))
        self.assertEqual(
            "sha256:a13a9e5c34816dd6a4c18cfa7a312144f5d9b58c510224396377ea6ccb70981a",
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "ConstantsNumericOracleParityTests.cs"
        )
        numeric_test_symbol = (
            "Dragons.SimpleDragon.Tests.ConstantsNumericOracleParityTests."
            "MatchesPinnedPythonConstantsNumeric"
        )
        numeric_test_hash = (
            "sha256:90ad09809698e55202778741cf4fb9108e28e0e9f5674023d33565646bc41a65"
        )
        numeric_implementation_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Constants/"
            "SimpleDragonConstants.cs"
        )
        numeric_implementation_hash = (
            "sha256:d3121495d33dc9528d215b6cffac1946f13f10516d6f0210d5dc63a633bc2fe4"
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
                    "Dragons.SimpleDragon."
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
            10: ("AUTOID_PREFIX", "exception", "immutable-native-auto-id-prefix-catalog-9a7c270a", "sha256:22696419df2acdd2f9657b84b3107ba0fb3b72d1de6cd9f1778286a0456a524c", "sha256:e4a4dfa53f01c48fe9bb3c1fce7b43bd4cdc5074e1ee1a73a6c4086e2cd7df40", "auto"),
            11: ("AUTOID_PREFIX.DAY_SCHEDULE", "equivalent", None, "sha256:e48c9adec39a3fa24f0b8613d1675c25a2064237664a8105f642e656faa6dce9", "sha256:be240b2298a0862f21631b55f2e6c9b144513c792e0a98a3590a45eed847e997", "auto"),
            12: ("AUTOID_PREFIX.FENESTRATION", "equivalent", None, "sha256:c1a225945c08ead821d72ba45d489914d8a80d84f98bcfe5be7da0ad55aed742", "sha256:bbfc388dddc3c2c9c208ae821a312fc99395e0f52440cfc5ebf685f0934a8ad6", "auto"),
            13: ("AUTOID_PREFIX.FENESTRATION_CONSTRUCTION", "equivalent", None, "sha256:da6bcc3e44802b13e1fde57e30d719eb05f13ce4230d0c4fe5d31dae9bb57884", "sha256:23ff7ad5de3ce2f024d86d0ab01e1789639a3bf8ca9cf3f6a7a43933e7698324", "auto"),
            14: ("AUTOID_PREFIX.HEAT_EXCHANGER", "equivalent", None, "sha256:988f5d0e56e157fbf86698520ae5f1316fa1b7f43278f0ea89550d47123da874", "sha256:29b92b59871440cc4ed6a3e327d0e1441d7b516a7aa8c0af3b7606136de273f9", "auto"),
            15: ("AUTOID_PREFIX.MATERIAL", "equivalent", None, "sha256:25e1db34e06e98f822489a2fd81ff0116e13fc5cb4c599bfc3e848340f6a4bdd", "sha256:8d85f380aaf7b983d89dfd773d49e3484a649b1a3e7ad336b79b74821ac941e2", "auto"),
            16: ("AUTOID_PREFIX.PROFILE", "equivalent", None, "sha256:579ddc67359dedbabe5938500359fc7e32e8ffaa61fa335d2636548c6b6f7e35", "sha256:75a4e84557e55829538b4936ecd9c2c0de4b7c3221e2ab29219b0ef659f09da7", "auto"),
            17: ("AUTOID_PREFIX.PV_PANEL", "equivalent", None, "sha256:85555a35d41989361b4a446c3fae28b3950be8f3d58639032a0a507da39f1de7", "sha256:2e09ed3459b99fc76838f5a64b9bcd53bc1da373436bb23125fb21134eb03fd9", "auto"),
            18: ("AUTOID_PREFIX.RULESET", "equivalent", None, "sha256:eee17dde11d175df92e7be0226a04d551351cf91652de6ae0fea319303ffc68b", "sha256:8f7db0a6e8326116ff152965efd09e4468915cd6255b64e6818f885b42f99c19", "auto"),
            19: ("AUTOID_PREFIX.SCHEDULE", "equivalent", None, "sha256:43f2ebe3ecc509b36aef4f5b8f7b5066e74f6f33601ffb58439c1ec7344e0ed7", "sha256:8a857f7362123599a6e0614555b81c89198d795a4ea9213a285a0072109b010e", "auto"),
            20: ("AUTOID_PREFIX.SOURCE_SYSTEM", "equivalent", None, "sha256:09e744fab78384c339d40df89257da74aa0520daeacfeefffa408aa346cd0e62", "sha256:c96a5bc1537dcc4a0a5666d1f69304e9e1e60be71eb543a9bf14385eb9a6c89b", "auto"),
            21: ("AUTOID_PREFIX.SUPPLY_SYSTEM", "equivalent", None, "sha256:4a60dd64ad48ead389732bc329ece5dcac495c27a96eaba1cff1dae4064cb921", "sha256:9ec836ef085743b8200c40f5a377cc6ae3eeeb37805d6253d867cec7e5578227", "auto"),
            22: ("AUTOID_PREFIX.SURFACE", "equivalent", None, "sha256:7b4e6369ce085157ba4c2b5921a2435357723de2ba720889c0f952090d8fec6a", "sha256:c58686b455dc044b0b3d4d4cae58138f607de691a3deeece1c18a32236f3888e", "auto"),
            23: ("AUTOID_PREFIX.SURFACE_CONSTRUCTION", "equivalent", None, "sha256:1e72a76dd67146ebe41b02f98959c85b09593e3063d2d8213fb756a118f951cf", "sha256:643a25fb859547fe267bb346bbc77023bea8aa1f333a93a37839de29bf9cb664", "auto"),
            24: ("AUTOID_PREFIX.ZONE", "equivalent", None, "sha256:2720c09e009d3bdf9a85e0954fb5f8398bc8b1a0ae1aa12af6eb9e84d0b2cb3f", "sha256:46c0996bef563fe74fede22d04aa720df23e5b3a32c60552bdd14af601eac401", "auto"),
            25: ("AUTOID_PREFIX.__format__", "equivalent", None, "sha256:a816cddeda045574ece80e4d280477f6f6d5ba719bd250848d41e9cc24df964d", "sha256:9f557ea7ab157fc18c15cb4a024c17ebaa80fb0681d214f07f181ebcae1478f2", "auto_format"),
            27: ("AUTOID_PREFIX.__str__", "equivalent", None, "sha256:63a868daac14f7900d160c3b9b93a4e25e017e473ed17ba9fa6484c9ee8c89db", "sha256:062b39444f424a91cd344225a8605ad9eb4b9144135af3433794a46ec19b9a13", "auto_format"),
            31: ("Directory", "exception", "embedded-explicit-native-resource-layout-5b876ad7", "sha256:723fb76aee65e3c0a549836c678b87ad1adcec3c3997762adac6332cd37028fc", "sha256:211c624ed64926026161b5980a575aa7f5928a6a9e5856158ac3057ca6865fa7", "embedded"),
            32: ("Directory.CONSTRUCTION_DIR", "exception", "embedded-native-construction-resources-91c573a0", "sha256:2224fc383d4d95587bada9e2a53e8d225ee4b04b14d6827239d8f4769df12f77", "sha256:379985d0bd07aa196673e200bc7e8e068447bbb6e6166b5fe19ab980da2910a8", "embedded"),
            33: ("Directory.PROFILE_DIR", "exception", "embedded-native-profile-resources-f65d5eae", "sha256:c032da9509cd77fe6ccd80716e6fd2f2cd99106314c9c69dd5942f5f7a4888f0", "sha256:85b3fd7f2ecdf4fb58acb30027f67c51f97647072b414e59b8c2480aea889461", "embedded"),
            34: ("Directory.WEATHER_DATA_DIR", "exception", "caller-supplied-native-weather-data-root-8a5bf654", "sha256:60035d4ba11ad55d8db9be9ec210b13b0006aebff5d52925e4cc2bb7c72e9f71", "sha256:5290a367de8fc8feddfe7dbb376e8d3c1a4716fa634bbce58eef28a80e1646d5", "weather"),
            35: ("Directory.WEATHER_META_DIR", "exception", "embedded-native-weather-metadata-resources-15e81d1d", "sha256:74d71a4020250fed6f7b673755272d8daf3718a2bfd0e41df7a9e08760e3ba2c", "sha256:996340a8f6b7d1daa039220c2817b3986d8c4d065ffe430db4a9373065a30a2f", "embedded"),
            36: ("PackageInfo", "exception", "static-native-simpledragon-package-information-aaf5b98d", "sha256:2b6c246086882d342beb6bb57d37a75fc2ae22c9bf79d8f9be87fdc9cb4733be", "sha256:321a4d4ba1eb004b5566096831e613c227dc63461a8f8c1b59d85bfd7249c8e6", "package"),
            37: ("PackageInfo.NAME", "exception", "native-simpledragon-package-name-537c8c3b", "sha256:3ad9dca79d585649de8f049226cad044f8a0059655de5e659794d405f5860137", "sha256:4eb6514fe149a35ae7b88125211cf42040164ccbe9993db2fbbaa97692ce70e2", "package_name"),
            38: ("PackageInfo.REQUIRED_PYTHON", "exception", "compiled-simpledragon-target-framework-contract-cf74d0eb", "sha256:472a512d8c6a348b78562a2ba90743ca1cd94d3b4d1d40eabdf9c65ddf7505a0", "sha256:267e58b39c3243e9c3ab289561c5916664ea0791f8e4124cf4588ca945c14f2c", "package"),
            39: ("PackageInfo.VERSION", "exception", "native-simpledragon-and-upstream-version-identity-a8260e5f", "sha256:691a7ac0839d15f54120e4247d22426a1e9a73be013bdcc5d06caed6d3723d5a", "sha256:bbf98958a9d0771823aae2f45ab3ee542aec908445c0db366e6d53ddfa03e37d", "package_version"),
            58: ("SpecialTag", "exception", "immutable-native-special-tag-catalog-a66e2175", "sha256:2b09900dfa47263d6e1581ba5292bc3e3836983ee1f110fb9a0d25d970738e13", "sha256:e3e63d3889985e9df3f38c88b568bbdbb40eaf49c4d2e618e6a6fbfe90e7d6d4", "special"),
            59: ("SpecialTag.CLONE", "equivalent", None, "sha256:68caa0f055733d4fb0355492e9141f69cf3d496b22b23f65c8bc31360c0d7fe4", "sha256:8ae821188f1f95efc6bdc6a69998ac64d2a799f000426c9dcc3f7220b5a84e82", "special"),
            60: ("SpecialTag.COOLROOF", "equivalent", None, "sha256:e017c5ab929de08f39938b2a8b49f3614baeeb307cdc3aac77d3b341e8acbbcf", "sha256:58f353a5e1560f93d260000878e7aafe3c0c113419d1836a625fd2e14af64bac", "special"),
            61: ("SpecialTag.DB", "equivalent", None, "sha256:16b8ccdd55935eb917e956a6d3420b2cb68f8d9aef9de50cfe9df1d349145e9e", "sha256:7d4addfcdef93cccad903edf046db1925467446ec0bda74cd98a4d5bcd9f78bd", "special"),
            62: ("SpecialTag.FLIP", "equivalent", None, "sha256:3a57d524c6ab76a5960245ea23c1fb40871f633297a0e8ae2284c13cfe2c9066", "sha256:1fd7d6b2bab642a2a30417b8c63549b46b94cdb89dbd8bbc9139365d095fccaf", "special"),
            63: ("SpecialTag.SPECIAL", "equivalent", None, "sha256:c7b1ce68b80c60a900cf68b1967195db31ea399bdfc85c88c445cec00d7ff92c", "sha256:5db6b86d948ae4b176d07d50021d900e79f78ec0b7b8da36b134c78b0d918379", "special"),
            64: ("SpecialTag.__format__", "equivalent", None, "sha256:7fab4ad475da909182182f598471198ad3e3850b8c2448086b09192a8a806c8f", "sha256:f4c704c491fb9763fac05e962bf53a120e2e7a1141436c8839c7e788a60d3fcf", "special_format"),
            66: ("SpecialTag.__str__", "equivalent", None, "sha256:80ca5168bcdac3483d97de5f3041641f0ba952bc250855f62aabb7ef329a1541", "sha256:e73b3caf39330488e4a5fdbf91270e5aca19a2f3fc1ecbe3790f5b8fbe05bb38", "special_format"),
        }
        identifier_implementations = {
            "auto": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "Dragons.SimpleDragon.AutoIdPrefix",
                "sha256:33d0281782b82837646804bbdfaa3ffd083a08a48bad98abfb7db4352aa43a3c",
            ),
            "auto_format": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "Dragons.SimpleDragon.AutoIdPrefix.ToString",
                "sha256:33d0281782b82837646804bbdfaa3ffd083a08a48bad98abfb7db4352aa43a3c",
            ),
            "embedded": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/Data/SimpleDragonEmbeddedData.cs",
                "Dragons.SimpleDragon.SimpleDragonEmbeddedData",
                "sha256:ae2cb7c89e4dcef7195e528fc7831c5abdba560651a244281ffeaaa83c60fc9f",
            ),
            "weather": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/Weather/WeatherDatabase.cs",
                "Dragons.SimpleDragon.WeatherSelection.ResolveEpwPath",
                "sha256:28f3885362fe08663ba6393bae545b70d17284d1751aa5a97cd0194e1b271b34",
            ),
            "package": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/PackageInfo.cs",
                "Dragons.SimpleDragon.PackageInfo",
                "sha256:ef73d4b6f9c9bd8948d73c225bb88012ab1616bcf4f6fc89b8d84f46cb95efe0",
            ),
            "package_name": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/PackageInfo.cs",
                "Dragons.SimpleDragon.PackageInfo.Name",
                "sha256:ef73d4b6f9c9bd8948d73c225bb88012ab1616bcf4f6fc89b8d84f46cb95efe0",
            ),
            "package_version": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/PackageInfo.cs",
                "Dragons.SimpleDragon.PackageInfo.Version",
                "sha256:ef73d4b6f9c9bd8948d73c225bb88012ab1616bcf4f6fc89b8d84f46cb95efe0",
            ),
            "special": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "Dragons.SimpleDragon.SpecialTag",
                "sha256:33d0281782b82837646804bbdfaa3ffd083a08a48bad98abfb7db4352aa43a3c",
            ),
            "special_format": (
                "src/SimpleDragon/Dragons.SimpleDragon.Core/Constants/IdentifierConventions.cs",
                "Dragons.SimpleDragon.SpecialTag.ToString",
                "sha256:33d0281782b82837646804bbdfaa3ffd083a08a48bad98abfb7db4352aa43a3c",
            ),
        }
        identifier_test_path = (
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "IdentifierConventionsOracleParityTests.cs"
        )
        identifier_test_symbol = (
            "Dragons.SimpleDragon.Tests.IdentifierConventionsOracleParityTests."
            "MatchesPinnedPythonIdentifierAndMetadataConventions"
        )
        identifier_test_hash = (
            "sha256:6c3c7f613c892ef605099b3e39df32a95a660bab0e4c5dceabf604ce5d39ae40"
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
                "sha256:aeca690d5e7596cad9a368b8812bf18f885eab9ec0ac5881d717ca7842f25b72",
                "sha256:3f46341428e2f78124303174fcc2606fb21b3654f8b20dcf36aa7f85f0375868",
                "Dragons.InvisibleDragon.Construction.AirBoundary",
            ),
            "AirBoundary.__init__": (
                589,
                "unchecked-python-air-boundary-construction-a69bf707",
                "dragon-construction-air-boundary-core-589-a69bf707",
                "sha256:d6fe3435a79549e9cba78b44d7d73603cf3164899924df872fdb7a0e356c82f3",
                "sha256:3189ed8bdc344e24d857162ffacfd9af9016ed3d416606491e0c0894cd4df415",
                "Dragons.InvisibleDragon.Construction.AirBoundary.AirBoundary",
            ),
        }
        air_boundary_test_path = (
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Construction/"
            "AirBoundaryCoreOracleParityTests.cs"
        )
        air_boundary_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Construction."
            "AirBoundaryCoreOracleParityTests."
            "MatchesPinnedAirBoundaryCoreThroughTypedNativeRoutes"
        )
        air_boundary_test_hash = (
            "sha256:a803684aad5bfab4f2b33ac1cd851f7d440941fdee6a173370c9d1a22a3def5c"
        )
        air_boundary_implementation_path = (
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/"
            "SimpleConstructions.cs"
        )
        air_boundary_implementation_hash = (
            "sha256:a72caa2d2c70ea18bf080bf623837ef3a0c7869a4991a7977255b00021d9e762"
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
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/Construction.cs",
                "sha256:85f4e6d46ef7eaed3d68ab7699508c7182aa50f16e1e5f4f5079dca56d616795",
            ),
            "Glazing": (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/SimpleConstructions.cs",
                "sha256:a72caa2d2c70ea18bf080bf623837ef3a0c7869a4991a7977255b00021d9e762",
            ),
            "Layer": (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/Layer.cs",
                "sha256:381d4b41f654233414e59710685120d7832363552dd80f9cc43d371de2109475",
            ),
            "Material": (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/Material.cs",
                "sha256:dd3027aaed2917eb78120f4280b86ff1e510627632d913a289d52690470112fe",
            ),
            "MaterialRoughness": (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/MaterialRoughness.cs",
                "sha256:76f351811fa91d043275601a688e583d5ca9bb5c74a16a55e7c595c7710f0012",
            ),
            "NoMassConstruction": (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Construction/SimpleConstructions.cs",
                "sha256:a72caa2d2c70ea18bf080bf623837ef3a0c7869a4991a7977255b00021d9e762",
            ),
        }
        construction_core_test_path = (
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Construction/"
            "ConstructionCoreOracleParityTests.cs"
        )
        construction_core_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Construction."
            "ConstructionCoreOracleParityTests."
            "MatchesPinnedDragonConstructionCoreThroughTypedNativeRoutes"
        )
        construction_core_test_hash = (
            "sha256:e919ffa54a6c73b0261d21beb313e9bc5c09bc4a12cfd2efdde864f878db6091"
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
                    f"Dragons.InvisibleDragon.Construction.{owner}"
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
                "sha256:d2bf62549499960f4dc2318efd4888d7f0d7fb77b6385ccc06c67933d44241f5",
                "permissive-python-blind-state",
            ),
            "Blind.__init__": (
                1026,
                "permissive-python-blind-state-574e9b5a",
                "dragon-shape-opening-adjacency-core-1026-574e9b5a",
                "sha256:30f2c7150bf8464bf18900f998f2fbe2883ab1becbbe5f68b125e24a9a04ed0e",
                "permissive-python-blind-state",
            ),
            "Door": (
                1028,
                "permissive-python-door-state-717d717a",
                "dragon-shape-opening-adjacency-core-1028-717d717a",
                "sha256:01f00005932e294b567ec4d7d6dbd86464723f8f1148f64fe1ffde6447abefb5",
                "permissive-python-door-state",
            ),
            "Door.__init__": (
                1029,
                "permissive-python-door-state-efd71c81",
                "dragon-shape-opening-adjacency-core-1029-efd71c81",
                "sha256:c491c454ddd794a6ef66380f421b2625c55cb133caa7160a8a41fad0fdc39b2e",
                "permissive-python-door-state",
            ),
            "Shade": (
                1030,
                "permissive-python-shade-state-9404da04",
                "dragon-shape-opening-adjacency-core-1030-9404da04",
                "sha256:84db359cd294c3d07af1d11e7ef3eb1991548072b2d2f766261ee3f6bc7cd40d",
                "permissive-python-shade-state",
            ),
            "Shade.__init__": (
                1031,
                "permissive-python-shade-state-f76ed298",
                "dragon-shape-opening-adjacency-core-1031-f76ed298",
                "sha256:3022ef92bf5b45ba134e4f74f776ba9b680de50881e9f35da682401c13f4a4b4",
                "permissive-python-shade-state",
            ),
            "Shading": (
                1033,
                "directly-instantiable-empty-python-shading-4dba9833",
                "dragon-shape-opening-adjacency-core-1033-4dba9833",
                "sha256:f5ae3f0cb6e7c8a1d2d3226c390335646ffa49242175b2ffbd52b0c32f819931",
                "directly-instantiable-empty-python-shading",
            ),
            "Surface.__init__": (
                1035,
                "aliased-python-surface-opening-inputs-ef349ef4",
                "dragon-shape-opening-adjacency-core-1035-ef349ef4",
                "sha256:053cd25c52186802d691baf04dc856c9107259d774e81be6d495ea8f2a3d847e",
                "aliased-python-surface-opening-inputs",
            ),
            "Surface.blinded_window": (
                1039,
                "fresh-python-blinded-window-projection-f520fbfe",
                "dragon-shape-opening-adjacency-core-1039-f520fbfe",
                "sha256:105727647225de926676a8d27f0ce35042ec44d16efe3f10f162b6113bf34a2d",
                "fresh-python-blinded-window-projection",
            ),
            "Surface.boundary": (
                1040,
                "mutable-reciprocal-python-surface-adjacency-7753d967",
                "dragon-shape-opening-adjacency-core-1040-7753d967",
                "sha256:cd50275ce93c7afefbdac5591f3f47b205d83f3bc6e623cc3ff0167a0008969a",
                "mutable-reciprocal-python-surface-adjacency",
            ),
            "Surface.get_subsurface": (
                1042,
                "legacy-linear-scale-subsurface-projection-7e43708d",
                "dragon-shape-opening-adjacency-core-1042-7e43708d",
                "sha256:c6caf385f45843f7e17ebe093363df8e60897b794cb4ff545c08e767228b210c",
                "legacy-linear-scale-subsurface-projection",
            ),
            "SurfaceBoundaryCondition": (
                1048,
                "lowercase-python-surface-boundary-enum-73a8b86f",
                "dragon-shape-opening-adjacency-core-1048-73a8b86f",
                "sha256:04f1c1ceb0ff96cb10e11d8bd2e9005e9699f8cc55845bffa709512a071e14cf",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ADIABATIC": (
                1049,
                "lowercase-python-surface-boundary-enum-1d0e3d46",
                "dragon-shape-opening-adjacency-core-1049-1d0e3d46",
                "sha256:e8fc4b8ef7b28d18005a2e4ad06487be14eb2c6da71aad502b8ca3cf7135be3f",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.GROUND": (
                1050,
                "lowercase-python-surface-boundary-enum-0992cbf6",
                "dragon-shape-opening-adjacency-core-1050-0992cbf6",
                "sha256:3217c26f766552b5b995fba936c6ea88f499d041221f8b105530fd5e4e478ee3",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.OUTDOOR": (
                1051,
                "lowercase-python-surface-boundary-enum-8560160a",
                "dragon-shape-opening-adjacency-core-1051-8560160a",
                "sha256:eeb6e95d9d39f8673670928ee0f4add5aee1041715d290729842d1321cc306f9",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.ZONE": (
                1052,
                "lowercase-python-surface-boundary-enum-3ec06789",
                "dragon-shape-opening-adjacency-core-1052-3ec06789",
                "sha256:7ee528b4d5289ba4d1515ddf5e749e087137999a4295815fefb2f89de0d5b651",
                "lowercase-python-surface-boundary-enum",
            ),
            "SurfaceBoundaryCondition.__str__": (
                1053,
                "lowercase-python-surface-boundary-enum-f40e4929",
                "dragon-shape-opening-adjacency-core-1053-f40e4929",
                "sha256:8c4741a25e601c345f1d704906270e19998d0263639c24927ce0ce61d74aacf8",
                "lowercase-python-surface-boundary-enum",
            ),
            "Window": (
                1081,
                "permissive-python-window-state-af640a9a",
                "dragon-shape-opening-adjacency-core-1081-af640a9a",
                "sha256:f2dcb2ac2fc321395374902b90a846b5d9d53c9134d0f2f131252d8836579a89",
                "permissive-python-window-state",
            ),
            "Window.__init__": (
                1082,
                "permissive-python-window-state-3ce851bd",
                "dragon-shape-opening-adjacency-core-1082-3ce851bd",
                "sha256:02a60447fdce77f39307c0d2545615e2585a03f1dab33ca924c8d31bfdfa1506",
                "permissive-python-window-state",
            ),
        }
        native_source_hashes = {
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Shading.cs": "sha256:e125e43e56a69fbb4707e1553d8a3318280b1d3356ec8c403256a6adc5001ef3",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Openings.cs": "sha256:3bca5b2a25574c58318eb55fd7f9a2c121a05e5c5645224a8e91c9ba92474588",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Surface.cs": "sha256:a4d2d35982c8aff254c0c8d74982e13394db2a770f38691710f9739f8b0a38e8",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/SurfaceBoundary.cs": "sha256:fc745e92061a0e8b1429399836f8a268b0d551e644f75f800a9cf987712c9d7a",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/SurfaceAdjacency.cs": "sha256:d78880fc40340ac3a2cfa4c63ff048f68347cabf114dc856ff18dc9666051190",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs": "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4",
        }
        expected_native_symbols = {
            "Blind": ("Shape/Shading.cs", "Dragons.InvisibleDragon.Shape.Blind"),
            "Blind.__init__": ("Shape/Shading.cs", "Dragons.InvisibleDragon.Shape.Blind.Blind"),
            "Door": ("Shape/Openings.cs", "Dragons.InvisibleDragon.Shape.Door"),
            "Door.__init__": ("Shape/Openings.cs", "Dragons.InvisibleDragon.Shape.Door.Door"),
            "Shade": ("Shape/Shading.cs", "Dragons.InvisibleDragon.Shape.Shade"),
            "Shade.__init__": ("Shape/Shading.cs", "Dragons.InvisibleDragon.Shape.Shade.Shade"),
            "Shading": ("Shape/Shading.cs", "Dragons.InvisibleDragon.Shape.IShadingDevice"),
            "Surface.__init__": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.Surface"),
            "Surface.blinded_window": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.Windows"),
            "Surface.boundary": ("Shape/SurfaceAdjacency.cs", "Dragons.InvisibleDragon.Shape.SurfaceAdjacency.Match"),
            "Surface.get_subsurface": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.CreateCenteredSubsurface"),
            "SurfaceBoundaryCondition": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceBoundaryCondition"),
            "SurfaceBoundaryCondition.ADIABATIC": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceBoundaryCondition.Adiabatic"),
            "SurfaceBoundaryCondition.GROUND": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceBoundaryCondition.Ground"),
            "SurfaceBoundaryCondition.OUTDOOR": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceBoundaryCondition.Outdoors"),
            "SurfaceBoundaryCondition.ZONE": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceBoundaryCondition.Zone"),
            "SurfaceBoundaryCondition.__str__": ("Model/EnergyModelIdfAssembler.cs", "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.BuildingSurface"),
            "Window": ("Shape/Openings.cs", "Dragons.InvisibleDragon.Shape.Window"),
            "Window.__init__": ("Shape/Openings.cs", "Dragons.InvisibleDragon.Shape.Window.Window"),
        }
        test_path = (
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Model/"
            "OpeningAdjacencyCoreOracleParityTests.cs"
        )
        test_symbol = (
            "Dragons.InvisibleDragon.Tests.Model."
            "OpeningAdjacencyCoreOracleParityTests."
            "MatchesPinnedOpeningAdjacencyCoreThroughBoundedNativeRoutes"
        )
        test_hash = (
            "sha256:b3a40cc4be8c19e496982eee3014887b450595db3d24f8c92af26154b8e7439c"
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
                "sha256:1c537374cdf927b6d5b3c579434a98e187100ce94c77045a03808907763593d8",
                "permissive-python-surface-polygon-model",
            ),
            "Surface.area": (
                1038,
                "exception",
                "first-triple-oriented-python-surface-area-f254ab66",
                "dragon-shape-geometry-core-1038-f254ab66",
                "sha256:02b7d781edcba2a3e377cc5d11cc6453e76c85c278129710c88bae143cf42e8e",
                "first-triple-oriented-python-surface-area",
            ),
            "Surface.center": (
                1041,
                "exception",
                "vertex-mean-python-surface-center-f0c05c2b",
                "dragon-shape-geometry-core-1041-f0c05c2b",
                "sha256:6805d035136d6ab3208c183752ac03551433c425994ead889571ff2170c868f4",
                "vertex-mean-python-surface-center",
            ),
            "Surface.height": (
                1043,
                "exception",
                "z-span-python-surface-height-d479fe2f",
                "dragon-shape-geometry-core-1043-d479fe2f",
                "sha256:fd3d4768fb98681ebb221d43794712c1e5e9e6e35e9609232c1bb6a9e5bcb3bd",
                "z-span-python-surface-height",
            ),
            "Surface.normal": (
                1044,
                "exception",
                "first-triple-python-surface-normal-3f089c8c",
                "dragon-shape-geometry-core-1044-3f089c8c",
                "sha256:10d1f828ad052c5fdb4d57db89b0aaf2cc77470653a6d83a48c37638702e6f73",
                "first-triple-python-surface-normal",
            ),
            "Surface.type": (
                1046,
                "exception",
                "mutable-string-coerced-python-surface-type-ae4bdcc7",
                "dragon-shape-geometry-core-1046-ae4bdcc7",
                "sha256:d4ab765abb52497de404ecee328a075e6129322ff37abc4bd0f76bbe35a89de2",
                "mutable-string-coerced-python-surface-type",
            ),
            "Surface.vertex": (
                1047,
                "exception",
                "aliased-mutable-python-surface-vertices-7ed5c6b3",
                "dragon-shape-geometry-core-1047-7ed5c6b3",
                "sha256:7a05c1ddc6746db7233e0b05a827e51146ff279536ecab6e337228b449d4a2a1",
                "aliased-mutable-python-surface-vertices",
            ),
            "SurfaceType": (
                1054,
                "exception",
                "lowercase-python-surface-type-enum-61a37f9d",
                "dragon-shape-geometry-core-1054-61a37f9d",
                "sha256:10d4ed649955506edd39f5e4f68b9f0c6179902a6cc5c8a8caa5e8240678b600",
                "lowercase-python-surface-type-enum",
            ),
            "SurfaceType.CEILING": (
                1055,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1055-9ece8323",
                "sha256:d8cbeaf2168c3fae10c959126550e2b99425d8fab863e4afa24113fe2fbd363b",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.FLOOR": (
                1056,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1056-c8c4f240",
                "sha256:42ac052a53c0b8db329c742c6db3641625e90b15a7a095492bb6a344a2c35bab",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.WALL": (
                1057,
                "equivalent",
                None,
                "dragon-shape-geometry-core-1057-ca6d5593",
                "sha256:d972564bbae0d858d1586f48531ea363a300d1bd6390ae6b6e6d8a3e9417180a",
                "direct-surface-type-member-mapping",
            ),
            "SurfaceType.__str__": (
                1058,
                "exception",
                "lowercase-python-surface-type-enum-f40e4929",
                "dragon-shape-geometry-core-1058-f40e4929",
                "sha256:b42a49f8a7070924303c7596bc58f579e506c7758af7394bcccea75fd63684a2",
                "lowercase-python-surface-type-enum",
            ),
            "Vertex": (
                1059,
                "exception",
                "permissive-mutable-python-vertex-state-78650289",
                "dragon-shape-geometry-core-1059-78650289",
                "sha256:ca167d3b6675d2213444e67fbe95690903c4bba99c406e66734f155ef85eef4b",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.__add__": (
                1060,
                "exception",
                "untyped-python-vertex-algebra-a5c7ecea",
                "dragon-shape-geometry-core-1060-a5c7ecea",
                "sha256:708559f4eed81314e0baf6ccff1000f0d78f766735455854815f5ab24adfc07c",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__deepcopy__": (
                1061,
                "exception",
                "python-vertex-copy-iteration-zero-addition-2c79da1a",
                "dragon-shape-geometry-core-1061-2c79da1a",
                "sha256:1337c76ce96072ffdd93abad8d73929fd092a8e00f45ff8ea658e2e7555870dd",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__init__": (
                1063,
                "exception",
                "permissive-mutable-python-vertex-state-be3c69c5",
                "dragon-shape-geometry-core-1063-be3c69c5",
                "sha256:e7f10c42017969e7e6ed14aa31259fd23f4c3ba5767890144c48cbf151fd8a13",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.__iter__": (
                1064,
                "exception",
                "python-vertex-copy-iteration-zero-addition-e95d7ce5",
                "dragon-shape-geometry-core-1064-e95d7ce5",
                "sha256:0836ae0aab13a817f45512310005f7337b8d8ff2b35512cf6b8350b6c5334270",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__mul__": (
                1065,
                "exception",
                "untyped-python-vertex-algebra-323878e1",
                "dragon-shape-geometry-core-1065-323878e1",
                "sha256:ca5d61ba6f47249db73d2c07c91932da94d6a04cbcf40ee707526dd2dfdc9868",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__radd__": (
                1066,
                "exception",
                "python-vertex-copy-iteration-zero-addition-a473d0f3",
                "dragon-shape-geometry-core-1066-a473d0f3",
                "sha256:5647294da8fdfd2e6da5d5825f3feb0b09ea033f2ed0b3383e7ca83b36b7ccea",
                "python-vertex-copy-iteration-zero-addition",
            ),
            "Vertex.__rmul__": (
                1068,
                "exception",
                "untyped-python-vertex-algebra-1dbe33d3",
                "dragon-shape-geometry-core-1068-1dbe33d3",
                "sha256:a8607c6f4ed285fa2036fb66bda3968e05c02bec513b8b57e94ceabcb0de1435",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__sub__": (
                1070,
                "exception",
                "untyped-python-vertex-algebra-4ee38e65",
                "dragon-shape-geometry-core-1070-4ee38e65",
                "sha256:9d4b1f38d2a41bc3a1065282bb32fa0ccd076d4fe95438772af14436139b267f",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.__truediv__": (
                1071,
                "exception",
                "untyped-python-vertex-algebra-94f397b8",
                "dragon-shape-geometry-core-1071-94f397b8",
                "sha256:03b29d0c5765a502961692d2763e20543a7403513aef92df2ffa843ccca80259",
                "untyped-python-vertex-algebra",
            ),
            "Vertex.are_coplanar": (
                1072,
                "exception",
                "legacy-first-triple-angular-coplanarity-905ebbf2",
                "dragon-shape-geometry-core-1072-905ebbf2",
                "sha256:cbe585a06057cc53eddd93fceeb52d85cb343c937345f4db622ebec8ee7614e7",
                "legacy-first-triple-angular-coplanarity",
            ),
            "Vertex.cross": (
                1073,
                "exception",
                "untyped-python-vertex-metrics-6bc5db49",
                "dragon-shape-geometry-core-1073-6bc5db49",
                "sha256:086f3ab50e3fbe91567b903b7df504e7d7587bc57ba763649b6db421e7eebfec",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.distance": (
                1074,
                "exception",
                "untyped-python-vertex-metrics-88c4cb9f",
                "dragon-shape-geometry-core-1074-88c4cb9f",
                "sha256:dfe4363d9330973bfbb8406f1ebf9859677a844c532c18b279f300c9d45c3926",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.dot": (
                1075,
                "exception",
                "untyped-python-vertex-metrics-1aaf5930",
                "dragon-shape-geometry-core-1075-1aaf5930",
                "sha256:22df984d0ccafada54b3afaf12c160e3144f8a01822ba4b4c082933eaef25686",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.norm": (
                1076,
                "exception",
                "untyped-python-vertex-metrics-e41eae31",
                "dragon-shape-geometry-core-1076-e41eae31",
                "sha256:f1171354cd9adfb47af34d9860941f5dda8d5b9f56b33d0ec706485407ed46a7",
                "untyped-python-vertex-metrics",
            ),
            "Vertex.unit": (
                1077,
                "exception",
                "zero-preserving-python-vertex-unit-4267bc06",
                "dragon-shape-geometry-core-1077-4267bc06",
                "sha256:b7a4585ce5785d75a8f07306fa66551d16b98d551ed30de18980d29f5f5c99a0",
                "zero-preserving-python-vertex-unit",
            ),
            "Vertex.x": (
                1078,
                "exception",
                "permissive-mutable-python-vertex-state-d859bad0",
                "dragon-shape-geometry-core-1078-d859bad0",
                "sha256:98431c0c5670a310f2cb95c6526ac132f8811d43902f45c4bcc4560c6d98f47e",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.y": (
                1079,
                "exception",
                "permissive-mutable-python-vertex-state-ff0bcc12",
                "dragon-shape-geometry-core-1079-ff0bcc12",
                "sha256:57d5848e9cab9138cba788e79cfce07a562cc5cf399b2c906eea0ede528b4316",
                "permissive-mutable-python-vertex-state",
            ),
            "Vertex.z": (
                1080,
                "exception",
                "permissive-mutable-python-vertex-state-64899aff",
                "dragon-shape-geometry-core-1080-64899aff",
                "sha256:46fe03361758eee65ac1701286d240c98319ad56ccd68689aa3fb558237387e4",
                "permissive-mutable-python-vertex-state",
            ),
        }
        geometry_native_source_hashes = {
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/PlanarPolygon.cs": "sha256:8b8ff21d647c63bd4c4eee0d46febb097372da58f570f3fec0b214b9393dfd7a",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Surface.cs": "sha256:a4d2d35982c8aff254c0c8d74982e13394db2a770f38691710f9739f8b0a38e8",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/SurfaceBoundary.cs": "sha256:fc745e92061a0e8b1429399836f8a268b0d551e644f75f800a9cf987712c9d7a",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Vector3.cs": "sha256:e0b6bfd123f839ca8211f8085c843f2430f4ca62995abcc8e97e500e39e8f683",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Vertex.cs": "sha256:5ddd24dd82e84c63ef4123c961ef932988298056c66be8c2c40010d7239e3534",
        }
        expected_geometry_native_symbols = {
            "Surface": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface"),
            "Surface.area": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.GrossArea"),
            "Surface.center": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.Center"),
            "Surface.height": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.Height"),
            "Surface.normal": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.Normal"),
            "Surface.type": ("Shape/Surface.cs", "Dragons.InvisibleDragon.Shape.Surface.Type"),
            "Surface.vertex": ("Shape/PlanarPolygon.cs", "Dragons.InvisibleDragon.Shape.PlanarPolygon.Vertices"),
            "SurfaceType": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceType"),
            "SurfaceType.CEILING": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceType.Ceiling"),
            "SurfaceType.FLOOR": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceType.Floor"),
            "SurfaceType.WALL": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceType.Wall"),
            "SurfaceType.__str__": ("Shape/SurfaceBoundary.cs", "Dragons.InvisibleDragon.Shape.SurfaceType"),
            "Vertex": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex"),
            "Vertex.__add__": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.op_Addition"),
            "Vertex.__deepcopy__": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex"),
            "Vertex.__init__": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.Vertex"),
            "Vertex.__iter__": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.X"),
            "Vertex.__mul__": ("Shape/Vector3.cs", "Dragons.InvisibleDragon.Shape.Vector3.op_Multiply"),
            "Vertex.__radd__": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.op_Addition"),
            "Vertex.__rmul__": ("Shape/Vector3.cs", "Dragons.InvisibleDragon.Shape.Vector3.op_Multiply"),
            "Vertex.__sub__": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.op_Subtraction"),
            "Vertex.__truediv__": ("Shape/Vector3.cs", "Dragons.InvisibleDragon.Shape.Vector3.op_Division"),
            "Vertex.are_coplanar": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.AreCoplanar"),
            "Vertex.cross": ("Shape/Vector3.cs", "Dragons.InvisibleDragon.Shape.Vector3.Cross"),
            "Vertex.distance": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.DistanceTo"),
            "Vertex.dot": ("Shape/Vector3.cs", "Dragons.InvisibleDragon.Shape.Vector3.Dot"),
            "Vertex.norm": ("Shape/Vector3.cs", "Dragons.InvisibleDragon.Shape.Vector3.Length"),
            "Vertex.unit": ("Shape/Vector3.cs", "Dragons.InvisibleDragon.Shape.Vector3.Normalize"),
            "Vertex.x": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.X"),
            "Vertex.y": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.Y"),
            "Vertex.z": ("Shape/Vertex.cs", "Dragons.InvisibleDragon.Shape.Vertex.Z"),
        }
        geometry_test_path = (
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Model/"
            "GeometryCoreOracleParityTests.cs"
        )
        geometry_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Model.GeometryCoreOracleParityTests."
            "MatchesPinnedGeometryCoreThroughBoundedNativeRoutes"
        )
        geometry_test_hash = (
            "sha256:ddc71e4fef7c994f7c0df1cdcc7114dc07f20c599cf052edd060c8899216fe17"
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
                "sha256:61f8b3a1dccf11999c0cae48eb07266f68987da4bb05b2f889617b8995dceb4e",
                "permissive-mutable-python-zone-container",
            ),
            "Zone.__init__": (
                1084,
                "unchecked-aliased-python-zone-construction",
                "dragon-shape-zone-core-1084-fad03092",
                "sha256:5ee7f76c0292ed1dc41f71eb930397e819d9841e5edb9de4acfe5ef571bd8aa9",
                "unchecked-aliased-python-zone-construction",
            ),
            "Zone.floor_area": (
                1085,
                "python-floor-identity-filter-and-dynamic-sum",
                "dragon-shape-zone-core-1085-21fe276d",
                "sha256:6331d622e84ae4cbe4b07ce8de5cc72bacaa42c8fe3e67e6671696020daf2624",
                "python-floor-identity-filter-and-dynamic-sum",
            ),
            "Zone.floor_surface": (
                1086,
                "python-floor-identity-filter-and-fresh-list",
                "dragon-shape-zone-core-1086-53382328",
                "sha256:5c79c37900c1322c61e3ba4e6e1d71d99d658f954d3bb5b6a7f9eea093fb279c",
                "python-floor-identity-filter-and-fresh-list",
            ),
            "Zone.idf_airexhaustnodelistname": (
                1087,
                "mutable-unvalidated-python-zone-name-formatting-48c6fddb",
                "dragon-shape-zone-core-1087-48c6fddb",
                "sha256:020ad70838342139173e98831a048030dd5e6e3d6663bbce930e447af0518857",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.idf_airinletnodelistname": (
                1088,
                "mutable-unvalidated-python-zone-name-formatting-97745304",
                "dragon-shape-zone-core-1088-97745304",
                "sha256:74eccfd8a671d66c1261a3e00b8f95fae66090c317c32506785e7ef73a54e75e",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.idf_equipmentlistname": (
                1089,
                "mutable-unvalidated-python-zone-name-formatting-ad9ccd78",
                "dragon-shape-zone-core-1089-ad9ccd78",
                "sha256:ee3dd89cfb86951e45684263ae836a880480315ab2258178e7b4d803ab1943b5",
                "mutable-unvalidated-python-zone-name-formatting",
            ),
            "Zone.supply": (
                1091,
                "embedded-python-zone-supply-coercion-and-mutation",
                "dragon-shape-zone-core-1091-1b5900c0",
                "sha256:a683f4de7f34051ce2754c0e258b7b7e9c3da3e16966fe94c299f85a65d09eff",
                "embedded-python-zone-supply-coercion-and-mutation",
            ),
        }
        zone_native_sources = {
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Shape/Zone.cs": "sha256:17423d03e67e5d19ee681f138291bb011a81b84e42b4a188825d570854235ffa",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs": "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4",
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs": "sha256:fcbe9c38cacade8002d121b0834a4441560086052571dd654f3c185a0c897249",
        }
        expected_zone_native_symbols = {
            "Zone": ("Shape/Zone.cs", "Dragons.InvisibleDragon.Shape.Zone"),
            "Zone.__init__": (
                "Shape/Zone.cs",
                "Dragons.InvisibleDragon.Shape.Zone.Zone",
            ),
            "Zone.floor_area": (
                "Shape/Zone.cs",
                "Dragons.InvisibleDragon.Shape.Zone.FloorArea",
            ),
            "Zone.floor_surface": (
                "Shape/Zone.cs",
                "Dragons.InvisibleDragon.Shape.Zone.FloorSurfaces",
            ),
            "Zone.idf_airexhaustnodelistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.idf_airinletnodelistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.idf_equipmentlistname": (
                "Model/EnergyModelIdfAssembler.cs",
                "Dragons.InvisibleDragon.Model.EnergyModelIdfAssembler.AppendZoneEquipment",
            ),
            "Zone.supply": (
                "Hvac/HvacAbstractions.cs",
                "Dragons.InvisibleDragon.Hvac.ZoneHvacAssignment",
            ),
        }
        zone_test_path = (
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Model/"
            "ZoneCoreOracleParityTests.cs"
        )
        zone_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Model.ZoneCoreOracleParityTests."
            "MatchesPinnedZoneCoreThroughTypedNativeRoutes"
        )
        zone_test_hash = (
            "sha256:b989d8ea04b42c3e8449f67ac76a582626b0eefc6007a81d436ee7efc7f6de61"
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
                "sha256:0f2d14c42d42f298582eea56c68ccff8bcdb529b6b46f62abe32f7008089b037",
                "sha256:399dc79ecd5f0c9aa520efac66b926af9694c102fd6318ba919cff6eecef3896",
                "src/Shared/Dragons.EnergyPlus.Runtime/RuntimeResolver.cs",
                "Dragons.EnergyPlus.Runtime.RuntimeResolver",
                "sha256:ae290360e832f99eb6190744684624fda003172428b668cb7d47ba84f28f35b2",
            ),
            "Directory.ENERGYPLUS_DIR": (
                569,
                "explicit-validated-native-energyplus-runtime-root",
                "constants-metadata-569-7e01ceac",
                "sha256:c04ff6693002dd618726423aef024fecf018187e4da672c671d300081e5b20cf",
                "sha256:22d61c40bebb9817197e78cbb6b3353c48c471676242689ba64234a8a3d5a85a",
                "src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusRuntimeLayout.cs",
                "Dragons.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.RootPath",
                "sha256:5552379c29e2f60e0edd5d2762d3468c605fd5a8e47aec65f3eab9f6c758458b",
            ),
            "Directory.IDD_DIR": (
                570,
                "validated-native-idd-path-resolution",
                "constants-metadata-570-1f0c2815",
                "sha256:728d979a870b16310d0be9c1b5be1f016f6732fe461cbb3eaae327ed97eab6f3",
                "sha256:1ed232b36511c05220553039d2d774decc637ad81be76c18dd55f1f23f68b006",
                "src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusRuntimeLayout.cs",
                "Dragons.EnergyPlus.Runtime.EnergyPlusRuntimeLayout.IddPath",
                "sha256:5552379c29e2f60e0edd5d2762d3468c605fd5a8e47aec65f3eab9f6c758458b",
            ),
            "Directory.PROFILE_DIR": (
                571,
                "typed-native-profile-data-without-package-profile-directory",
                "constants-metadata-571-f65d5eae",
                "sha256:ead05bd05e2dc66a464732dc248106e747ee1263474335d3c53c7f2d0872e71f",
                "sha256:13506910df2c33a01fcb5376ed85aecf1240e6c509ce89d1aa9455928d4f0166",
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Profile/Profile.cs",
                "Dragons.InvisibleDragon.Profile.Profile",
                "sha256:670c41d252c47be93f5bc839967332a1aba33061a2eb832b532e658b1b3683fd",
            ),
            "PackageInfo": (
                572,
                "static-native-package-information",
                "constants-metadata-572-aaf5b98d",
                "sha256:ad76215fd35edababaa90be19f8dd585769ebb53f75f284fc2095d3317463de6",
                "sha256:75b93bdfe73fefcb963b9b2e73dd200f8abc406b0e6d61e6a6f414aef4fe3a8d",
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/PackageInfo.cs",
                "Dragons.InvisibleDragon.PackageInfo",
                "sha256:933a0d70a9cfed35e91a4ea0f31452c487e56ff43984387fb8d030b2fdc28385",
            ),
            "PackageInfo.NAME": (
                573,
                "native-invisibledragon-package-name",
                "constants-metadata-573-3942a963",
                "sha256:e118b7662e7b419f407cbc830d22ebd2a9f47ffbac8de33d000e82e551d0b857",
                "sha256:a567ed0d0b1ad33d735ebc678f5938fd3ec7c1c2e8835ce07ae977ab30fe8d4b",
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/PackageInfo.cs",
                "Dragons.InvisibleDragon.PackageInfo.Name",
                "sha256:933a0d70a9cfed35e91a4ea0f31452c487e56ff43984387fb8d030b2fdc28385",
            ),
            "PackageInfo.REQUIRED_PYTHON": (
                574,
                "compiled-native-target-framework-contract",
                "constants-metadata-574-cf74d0eb",
                "sha256:9921458bc9c834d7f27a432c089cff812d473192197e4998ea959fd93b91c8f5",
                "sha256:48642b169fedda75b44969a49380bc77ef6c43cbbb14af69925e5cc560ac016d",
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/PackageInfo.cs",
                "Dragons.InvisibleDragon.PackageInfo",
                "sha256:933a0d70a9cfed35e91a4ea0f31452c487e56ff43984387fb8d030b2fdc28385",
            ),
            "PackageInfo.VERSION": (
                575,
                "native-semantic-version-string",
                "constants-metadata-575-a8260e5f",
                "sha256:bbfeaa6056580d29f18bf1bcd687dc180aacfddec7c43d5637045f905f458930",
                "sha256:bda279b108ed40b8e0c8751a2c178e11192c4f8341e199e957f8fe8c981037ec",
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/PackageInfo.cs",
                "Dragons.InvisibleDragon.PackageInfo.Version",
                "sha256:933a0d70a9cfed35e91a4ea0f31452c487e56ff43984387fb8d030b2fdc28385",
            ),
        }
        constants_test_path = (
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Common/"
            "ConstantsMetadataOracleParityTests.cs"
        )
        constants_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Common.ConstantsMetadataOracleParityTests."
            "MatchesPinnedConstantsMetadataThroughBoundedNativeAdaptations"
        )
        constants_test_hash = (
            "sha256:c5d5162dbcf874f6b662db12d34791da40fe87c1e58e6ebdace47043c5429e31"
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
            "sha256:d922bee6172dff616937ce6404af82d5e0826dad03010fa6644ed5ecabc72b5a",
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
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs",
            energy_model_evidence.implementation_path,
        )
        self.assertEqual(
            "sha256:60357af37bea1f6e7dd0640254a30761ed4097d53751183e5902c2efa62a0f28",
            energy_model_evidence.implementation_source_sha256,
        )
        self.assertEqual(
            "Dragons.InvisibleDragon.Model.EnergyModel",
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
            "sha256:5cd000c0a672d4bb0a8cda537af082ccd23a209692e2abe9760732b594a431b4",
            energy_model_receipt.expected_output_sha256,
        )
        self.assertEqual(
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Model/"
            "EnergyModelClassOracleParityTests.cs",
            energy_model_receipt.test_path,
        )
        self.assertEqual(
            "Dragons.InvisibleDragon.Tests.Model.EnergyModelClassOracleParityTests."
            "MatchesPinnedPythonEnergyModelClassThroughTypedNativeRoutes",
            energy_model_receipt.test_symbol,
        )
        self.assertEqual(
            "sha256:70c56d75d9ae110055ee54b40a3c043bcee9e1344b231f6e6b5db6b021284010",
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "ModelCoreOracleParityTests.cs"
        )
        model_test_symbol = (
            "Dragons.SimpleDragon.Tests.ModelCoreOracleParityTests."
            "MatchesPinnedModelCoreThroughProductionPublicRoutes"
        )
        model_fixture_sha256 = (
            "sha256:85c6f251087083b59c889725b19cbc5f9fb2c9c28b29135c38ce38fe7f65f61d"
        )
        model_test_sha256 = (
            "sha256:db7a36464d768c73170454f87f6b0b2263eb3934eb1992312f09f988809ee0c0"
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
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Model/"
            "GreenRetrofitModel.cs"
        )
        weather_native_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Weather/"
            "WeatherDatabase.cs"
        )
        reader_native_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs"
        )
        conversion_native_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/"
            "GreenRetrofitConversion.cs"
        )
        failure_native_path = (
            "src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusFailure.cs"
        )
        runner_native_path = (
            "src/Shared/Dragons.EnergyPlus.Runtime/EnergyPlusRunner.cs"
        )
        model_members = {
            "GreenRetrofitModel": "Dragons.SimpleDragon.GreenRetrofitModel",
            "GreenRetrofitModel.__init__": "Dragons.SimpleDragon.GreenRetrofitModel",
            "GreenRetrofitModel.address": "Dragons.SimpleDragon.GreenRetrofitModel.Address",
            "GreenRetrofitModel.area": "Dragons.SimpleDragon.GreenRetrofitModel.Area",
            "GreenRetrofitModel.averaged_exteriorfloor_Uvalue": "Dragons.SimpleDragon.GreenRetrofitModel.AverageExteriorFloorUValue",
            "GreenRetrofitModel.averaged_exteriorroof_Uvalue": "Dragons.SimpleDragon.GreenRetrofitModel.AverageExteriorRoofUValue",
            "GreenRetrofitModel.averaged_exteriorwall_Uvalue": "Dragons.SimpleDragon.GreenRetrofitModel.AverageExteriorWallUValue",
            "GreenRetrofitModel.averaged_infiltration": "Dragons.SimpleDragon.GreenRetrofitModel.AverageInfiltration",
            "GreenRetrofitModel.averaged_lightdensity": "Dragons.SimpleDragon.GreenRetrofitModel.AverageLightDensity",
            "GreenRetrofitModel.averaged_window_Uvalue": "Dragons.SimpleDragon.GreenRetrofitModel.AverageWindowUValue",
            "GreenRetrofitModel.climate": "Dragons.SimpleDragon.GreenRetrofitModel.Weather",
            "GreenRetrofitModel.exteriorfloors": "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorFloors",
            "GreenRetrofitModel.exteriorroofs": "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorRoofs",
            "GreenRetrofitModel.exteriorwalls": "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorWalls",
            "GreenRetrofitModel.exteriorwindows": "Dragons.SimpleDragon.GreenRetrofitModel.ExteriorWindows",
            "GreenRetrofitModel.get_unique_fenestration_constructions": "Dragons.SimpleDragon.GreenRetrofitModel.FenestrationConstructions",
            "GreenRetrofitModel.get_unique_materials": "Dragons.SimpleDragon.GreenRetrofitModel.Materials",
            "GreenRetrofitModel.get_unique_profiles": "Dragons.SimpleDragon.GreenRetrofitModel.Zones",
            "GreenRetrofitModel.get_unique_surface_constructions": "Dragons.SimpleDragon.GreenRetrofitModel.SurfaceConstructions",
            "GreenRetrofitModel.north_axis": "Dragons.SimpleDragon.GreenRetrofitModel.NorthAxis",
            "GreenRetrofitModel.source_system": "Dragons.SimpleDragon.GreenRetrofitModel.SourceSystems",
            "GreenRetrofitModel.terrain": "Dragons.SimpleDragon.GreenRetrofitModel.Weather",
            "GreenRetrofitModel.vintage": "Dragons.SimpleDragon.GreenRetrofitModel.Vintage",
            "GreenRetrofitModel.weather": "Dragons.SimpleDragon.GreenRetrofitModel.Weather",
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
                    "Dragons.SimpleDragon.WeatherDatabase.FindByAddress",
                )
            if symbol in {"EnergyPlusError", "EnergyPlusError.__init__"}:
                return (
                    failure_native_path,
                    "Dragons.EnergyPlus.Runtime.EnergyPlusFailure",
                )
            special_routes = {
                "GreenRetrofitModel.from_grjson": (
                    reader_native_path,
                    "Dragons.SimpleDragon.GrmReader.ReadFile",
                ),
                "GreenRetrofitModel.run": (
                    runner_native_path,
                    "Dragons.EnergyPlus.Runtime.EnergyPlusRunner.RunAsync",
                ),
                "GreenRetrofitModel.to_dragon": (
                    conversion_native_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
                "GreenRetrofitModel.to_idf": (
                    conversion_native_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.ToIdfDocument",
                ),
                "GreenRetrofitModel.weather_filepath": (
                    weather_native_path,
                    "Dragons.SimpleDragon.WeatherSelection.ResolveEpwPath",
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "ModelResultOracleParityTests.cs"
        )
        result_test_symbol = (
            "Dragons.SimpleDragon.Tests.ModelResultOracleParityTests."
            "MatchesPinnedModelResultThroughProductionPublicRoutes"
        )
        result_fixture_sha256 = (
            "sha256:d639c5c1047dca6a3682c9c2cfdac5fd1da99b5743c11d591d50942ae5322c02"
        )
        result_test_sha256 = (
            "sha256:bd9be7390618ed4e5f39bea28e0face69b39077815635872c572a41ba0e6af49"
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
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Results/"
            "GreenRetrofitResultModels.cs"
        )
        result_builder_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Results/"
            "GreenRetrofitResultBuilder.cs"
        )
        result_writer_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Results/GrrWriter.cs"
        )
        result_implementations = {
            "GreenRetrofitResult": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult",
            ),
            "GreenRetrofitResult.VALID_DIGITS": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult.ValidDigits",
            ),
            "GreenRetrofitResult.__init__": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult.FromSiteUses",
            ),
            "GreenRetrofitResult.area": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult.TotalArea",
            ),
            "GreenRetrofitResult.calc_domestic_hotwater_site_energy": (
                result_builder_path,
                "Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.get_dhw_servers": (
                result_builder_path,
                "Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.get_domestic_hotwater_energy": (
                result_builder_path,
                "Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.summarize": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult.PerAreaSummaries",
            ),
            "GreenRetrofitResult.to_co2": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult.Carbon",
            ),
            "GreenRetrofitResult.to_cost": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult.Cost",
            ),
            "GreenRetrofitResult.to_dict": (
                result_writer_path,
                "Dragons.SimpleDragon.GrrWriter.Serialize",
            ),
            "GreenRetrofitResult.to_site_uses": (
                result_builder_path,
                "Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build",
            ),
            "GreenRetrofitResult.to_source_uses": (
                result_models_path,
                "Dragons.SimpleDragon.GreenRetrofitResult.SourceUses",
            ),
            "GreenRetrofitResult.write": (
                result_writer_path,
                "Dragons.SimpleDragon.GrrWriter.WriteFile",
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
            316,
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "ShapeCoreOracleParityTests.cs"
        )
        shape_test_symbol = (
            "Dragons.SimpleDragon.Tests.ShapeCoreOracleParityTests."
            "MatchesPinnedShapeCoreThroughProductionPublicRoutes"
        )
        shape_fixture_sha256 = (
            "sha256:1beff8671a20e03e968dd0570aae174282752b5b28feefcd035ca136d023f90f"
        )
        shape_test_sha256 = (
            "sha256:5a7b5216167ed6475d60695cf251cc1b5ca63bbac77baa0f05bb46b106243b13"
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
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Fenestration.cs"
        )
        surface_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Surface.cs"
        )
        zone_path = "src/SimpleDragon/Dragons.SimpleDragon.Core/Shape/Zone.cs"
        reader_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs"
        )
        writer_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs"
        )
        converter_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/"
            "GreenRetrofitConversion.cs"
        )
        model_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Model/GreenRetrofitModel.cs"
        )

        def expected_shape_implementation(symbol: str) -> tuple[str, str]:
            if symbol == "BlindType":
                return fenestration_path, "Dragons.SimpleDragon.BlindType"
            if symbol == "BlindType.SHADE":
                return fenestration_path, "Dragons.SimpleDragon.BlindType.Shade"
            if symbol == "BlindType.VENETIAN":
                return fenestration_path, "Dragons.SimpleDragon.BlindType.Venetian"
            if symbol == "BlindType.__str__":
                return writer_path, "Dragons.SimpleDragon.GrmWriter.Serialize"
            if symbol.endswith(".from_json"):
                return reader_path, "Dragons.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    converter_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if symbol in {"Door", "Fenestration", "GlassDoor", "Window"}:
                return fenestration_path, "Dragons.SimpleDragon.Fenestration"
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
                    "Dragons.SimpleDragon.Fenestration." + member,
                )
            if symbol == "Surface":
                return surface_path, "Dragons.SimpleDragon.Surface"
            if symbol.startswith("Surface."):
                member_name = symbol.split(".", 1)[1]
                if member_name == "get_unique_fenestration_constructions":
                    return (
                        model_path,
                        "Dragons.SimpleDragon.GreenRetrofitModel."
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
                return surface_path, "Dragons.SimpleDragon.Surface." + member
            if symbol == "Zone":
                return zone_path, "Dragons.SimpleDragon.Zone"
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
                        "Dragons.SimpleDragon.GreenRetrofitModel."
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
                return zone_path, "Dragons.SimpleDragon.Zone." + member
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "ConstructionCoreOracleParityTests.cs"
        )
        construction_test_symbol = (
            "Dragons.SimpleDragon.Tests.ConstructionCoreOracleParityTests."
            "MatchesPinnedConstructionCoreThroughProductionPublicRoutes"
        )
        construction_fixture_sha256 = (
            "sha256:8fad664f712facf9eef8627d80e9bafcf468e4b0c63d4cf09d9632db814246b4"
        )
        construction_test_sha256 = (
            "sha256:2c42a1efb2c2afdf3d81f7f31b47abb96aa312127d3cb5c861384a4c4b65dd51"
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
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/Material.cs"
        )
        fenestration_construction_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/"
            "FenestrationConstruction.cs"
        )
        surface_construction_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/"
            "SurfaceConstruction.cs"
        )
        construction_database_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Construction/"
            "ConstructionDatabases.cs"
        )
        database_aggregate_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Data/"
            "SimpleDragonDatabase.cs"
        )

        def expected_construction_implementation(symbol: str) -> tuple[str, str]:
            if symbol == "FenestrationConstruction":
                return (
                    fenestration_construction_path,
                    "Dragons.SimpleDragon.FenestrationConstruction",
                )
            if symbol.startswith("FenestrationConstruction."):
                member_name = symbol.split(".", 1)[1]
                routed = {
                    "from_json": (reader_path, "Dragons.SimpleDragon.GrmReader.Read"),
                    "to_dict": (writer_path, "Dragons.SimpleDragon.GrmWriter.Serialize"),
                    "to_dragon": (
                        converter_path,
                        "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                    ),
                    "get_DB": (
                        construction_database_path,
                        "Dragons.SimpleDragon.FenestrationConstructionDatabase.Find",
                    ),
                    "load_DB": (
                        database_aggregate_path,
                        "Dragons.SimpleDragon.SimpleDragonDatabase.LoadEmbedded",
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
                    "Dragons.SimpleDragon.FenestrationConstruction" + suffix,
                )
            if symbol == "Material":
                return material_path, "Dragons.SimpleDragon.Material"
            if symbol.startswith("Material."):
                member_name = symbol.split(".", 1)[1]
                routed = {
                    "from_json": (reader_path, "Dragons.SimpleDragon.GrmReader.Read"),
                    "to_dict": (writer_path, "Dragons.SimpleDragon.GrmWriter.Serialize"),
                    "to_dragon": (
                        converter_path,
                        "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                    ),
                    "get_DB": (
                        construction_database_path,
                        "Dragons.SimpleDragon.MaterialDatabase.Find",
                    ),
                    "load_DB": (
                        database_aggregate_path,
                        "Dragons.SimpleDragon.SimpleDragonDatabase.LoadEmbedded",
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
                return material_path, "Dragons.SimpleDragon.Material" + suffix
            special_routes = {
                "OpenConstruction": (
                    surface_path,
                    "Dragons.SimpleDragon.SurfaceConstructionReferenceKind.Open",
                ),
                "OpenConstruction.ID": (
                    surface_path,
                    "Dragons.SimpleDragon.Surface.ConstructionId",
                ),
                "OpenConstruction.to_dragon": (
                    converter_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
                "SpecialConstruction": (
                    surface_path,
                    "Dragons.SimpleDragon.SurfaceConstructionReferenceKind",
                ),
                "SpecialConstruction.__new__": (
                    surface_path,
                    "Dragons.SimpleDragon.Surface.ConstructionReferenceKind",
                ),
                "SpecialConstruction.get_unique_materials": (
                    converter_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
                "SpecialConstruction.reversed": (
                    surface_path,
                    "Dragons.SimpleDragon.Surface.Flip",
                ),
                "UnknownConstruction": (
                    surface_path,
                    "Dragons.SimpleDragon.SurfaceConstructionReferenceKind.Unknown",
                ),
                "UnknownConstruction.ID": (
                    surface_path,
                    "Dragons.SimpleDragon.Surface.ConstructionId",
                ),
                "UnknownConstruction.to_dragon": (
                    converter_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                ),
            }
            if symbol in special_routes:
                return special_routes[symbol]
            if symbol == "SurfaceConstruction":
                return (
                    surface_construction_path,
                    "Dragons.SimpleDragon.SurfaceConstruction",
                )
            if symbol.startswith("SurfaceConstruction."):
                member_name = symbol.split(".", 1)[1]
                routed = {
                    "from_json": (reader_path, "Dragons.SimpleDragon.GrmReader.Read"),
                    "to_dict": (writer_path, "Dragons.SimpleDragon.GrmWriter.Serialize"),
                    "to_dragon": (
                        converter_path,
                        "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                    ),
                    "get_DB": (
                        construction_database_path,
                        "Dragons.SimpleDragon.SurfaceConstructionDatabase.Find",
                    ),
                    "get_regulated_construction": (
                        construction_database_path,
                        "Dragons.SimpleDragon.SurfaceConstructionDatabase.FindRegulated",
                    ),
                    "load_DB": (
                        database_aggregate_path,
                        "Dragons.SimpleDragon.SimpleDragonDatabase.LoadEmbedded",
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
                    "Dragons.SimpleDragon.SurfaceConstruction" + suffix,
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "HvacEnumsBaseOracleParityTests.cs"
        )
        hvac_test_symbol = (
            "Dragons.SimpleDragon.Tests.HvacEnumsBaseOracleParityTests."
            "MatchesPinnedHvacEnumsBaseThroughProductionPublicRoutes"
        )
        hvac_fixture_sha256 = (
            "sha256:878c2970a95d4a80e87408cc07a7f5ea2c97c764385e990d8459c536a199a208"
        )
        hvac_generator_sha256 = (
            "sha256:a397d3169f61a375b12a3934a2270874bfef1f3713a635cfd5e342668d12046b"
        )
        hvac_validator_sha256 = (
            "sha256:236069bdde9be3a556559da1f12150114b75bbf08fbd951918744459f015a491"
        )
        hvac_test_sha256 = (
            "sha256:ee93d3b14f82f0c3c290e2faa229b9899af171ce92a5547d3908917e05a4522f"
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
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SourceSystem.cs"
        )
        hvac_supply_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SupplySystem.cs"
        )
        hvac_reader_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs"
        )
        hvac_writer_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs"
        )
        hvac_conversion_path = (
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/"
            "GreenRetrofitConversion.cs"
        )

        def expected_hvac_implementation(symbol: str) -> tuple[str, str]:
            enum_members = {
                "CompressorType": "Dragons.SimpleDragon.CompressorType",
                "CompressorType.RECIPROCATING": "Dragons.SimpleDragon.CompressorType.Reciprocating",
                "CompressorType.SCREW": "Dragons.SimpleDragon.CompressorType.Screw",
                "CompressorType.TURBO": "Dragons.SimpleDragon.CompressorType.Turbo",
                "CoolingTowerControl": "Dragons.SimpleDragon.CoolingTowerControl",
                "CoolingTowerControl.SINGLESPEED": "Dragons.SimpleDragon.CoolingTowerControl.SingleSpeed",
                "CoolingTowerControl.TWOSPEED": "Dragons.SimpleDragon.CoolingTowerControl.TwoSpeed",
                "CoolingTowerType": "Dragons.SimpleDragon.CoolingTowerType",
                "CoolingTowerType.CLOSED": "Dragons.SimpleDragon.CoolingTowerType.Closed",
                "CoolingTowerType.OPEN": "Dragons.SimpleDragon.CoolingTowerType.Open",
                "Fuel": "Dragons.SimpleDragon.FuelType",
                "Fuel.DISTRICTHEATING": "Dragons.SimpleDragon.FuelType.DistrictHeating",
                "Fuel.ELECTRICITY": "Dragons.SimpleDragon.FuelType.Electricity",
                "Fuel.LPG": "Dragons.SimpleDragon.FuelType.LiquefiedPetroleumGas",
                "Fuel.NATURALGAS": "Dragons.SimpleDragon.FuelType.NaturalGas",
                "Fuel.OIL": "Dragons.SimpleDragon.FuelType.Oil",
                "SourceSystem": "Dragons.SimpleDragon.SourceSystem",
            }
            if symbol in enum_members:
                return hvac_source_path, enum_members[symbol]
            if symbol.endswith(".__str__"):
                return hvac_writer_path, "Dragons.SimpleDragon.GrmWriter.Serialize"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if symbol == "NoneSource.ID":
                return (
                    hvac_supply_path,
                    "Dragons.SimpleDragon.SupplySystem.SourceSystemId",
                )
            if symbol in {"NoneSource", "NoneSource.__new__"}:
                return (
                    hvac_supply_path,
                    "Dragons.SimpleDragon.SupplySystem.SourceSystem",
                )
            if symbol == "SourceSystem.TYPE_MAPPER":
                return hvac_reader_path, "Dragons.SimpleDragon.GrmReader.Read"
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
            345,
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "HvacThermalSourceOracleParityTests.cs"
        )
        thermal_test_symbol = (
            "Dragons.SimpleDragon.Tests.HvacThermalSourceOracleParityTests."
            "MatchesPinnedHvacThermalSourcesThroughProductionPublicRoutes"
        )
        thermal_fixture_sha256 = (
            "sha256:a82d1b26673cada47b45b8cbd61f03beeb6ce39495090e6b731bc1b4114bcdf2"
        )
        thermal_generator_sha256 = (
            "sha256:7a3ad0eb70b31542a04b6927389aad67fdcac37a0426632a00a55bdbc40f182d"
        )
        thermal_validator_sha256 = (
            "sha256:8d3026ebea8b4484fae93331b62ac010ba8b9bc1a536f36c4c3b12104c348dfc"
        )
        thermal_test_sha256 = (
            "sha256:623ce370139050bd336391ef00b0a7702c1f5905f4d0108e31a5290d9d1eb86f"
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
                return hvac_reader_path, "Dragons.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if "." not in symbol or symbol.endswith(".__init__"):
                return hvac_source_path, "Dragons.SimpleDragon.SourceSystem"
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
            355,
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "HvacSupplySystemOracleParityTests.cs"
        )
        supply_test_symbol = (
            "Dragons.SimpleDragon.Tests.HvacSupplySystemOracleParityTests."
            "MatchesPinnedHvacSupplySystemsThroughProductionPublicRoutes"
        )
        supply_fixture_sha256 = (
            "sha256:61ae6f650e0cd05db76b18b68477fff72e1357ae1842892170fefa01cb4285c2"
        )
        supply_generator_sha256 = (
            "sha256:a4bb12756e28697389d1850f81f2d231d8266ab6a72259a20085a59835b6b8d9"
        )
        supply_validator_sha256 = (
            "sha256:52b11bb8f4afc05feedd74fd475940c1b248371effd6dcaea59fd2d8eb5ba033"
        )
        supply_test_sha256 = (
            "sha256:4dd7ec78dbc87bdee6180edc1db718a39896b2723e9ce99e39e6b167da7c0710"
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
                return hvac_reader_path, "Dragons.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            if "." not in symbol or symbol.endswith(".__init__"):
                return hvac_supply_path, "Dragons.SimpleDragon.SupplySystem"
            return (
                hvac_supply_path,
                route.replace(
                    "Dragons.SimpleDragon.SourceSystem.",
                    "Dragons.SimpleDragon.SupplySystem.",
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
            378,
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
            "tests/SimpleDragon/Dragons.SimpleDragon.Core.Tests/"
            "HvacOtherSystemsOracleParityTests.cs"
        )
        other_test_symbol = (
            "Dragons.SimpleDragon.Tests.HvacOtherSystemsOracleParityTests."
            "MatchesPinnedHvacOtherSystemsThroughProductionPublicRoutes"
        )
        other_fixture_sha256 = (
            "sha256:e93876b839672d4de1f5b0c205c87f1b03a894c08e391cef2170b090f2645dc4"
        )
        other_generator_sha256 = (
            "sha256:f749032884f2336a2d672a2a59af432859fe9d40498cf4399cb969f0cec9f277"
        )
        other_validator_sha256 = (
            "sha256:5f394ab6811e6d174443278f93ec3956a07ad41eba186073b2c339baa2373db7"
        )
        other_test_sha256 = (
            "sha256:c2126e253465b6e583f465de124daa8a22d514438a52317ba17c4669a2eb1386"
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
            "src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/OtherSystems.cs"
        )

        def expected_other_implementation(symbol: str) -> tuple[str, str]:
            if symbol.endswith(".from_json"):
                return hvac_reader_path, "Dragons.SimpleDragon.GrmReader.Read"
            if symbol.endswith(".to_dragon"):
                return (
                    hvac_conversion_path,
                    "Dragons.SimpleDragon.GreenRetrofitConverter.Convert",
                )
            owner = (
                "Dragons.SimpleDragon.PhotovoltaicSystem"
                if symbol.startswith("PhotoVoltaicSystem")
                else "Dragons.SimpleDragon.VentilationSystem"
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
            411,
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Hvac/"
            "SourceTowerCoreOracleParityTests.cs"
        )
        source_tower_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Hvac.SourceTowerCoreOracleParityTests."
            "MatchesPinnedSourceTowerCoreThroughProductionPublicRoutes"
        )
        source_tower_fixture_sha256 = (
            "sha256:6fd214450197ea5effac61353819ab6ce4ab30b0fcd4a18fcd34816095015620"
        )
        source_tower_generator_sha256 = (
            "sha256:3f4ac211b3449ffac3b3f2a2048fc90320d45b5019d22ea59d6715a3e773353e"
        )
        source_tower_validator_sha256 = (
            "sha256:481e26fcbc6382f30e424d1ee1aac7d1574b44877d86bd7e621cd92b75ccfcf9"
        )
        source_tower_test_sha256 = (
            "sha256:3c8549bea4aaa4cd97dde942e0fa17a6b908e44e35847657bafea93805a669cd"
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
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/Chillers.cs"
                )
            elif symbol.startswith(("CoolingTower", "Closed", "Open")):
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "CoolingTowers.cs"
                )
            elif symbol.startswith("GeothermalHeatPump"):
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "GeothermalHeatPump.cs"
                )
            elif symbol == "SourceSystem.idf_terminalunitlistname" or symbol.startswith(
                ("Boiler", "HeatPump")
            ):
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "SourceSystems.cs"
                )
            else:
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs"
                )

            if ".__init__" in symbol or "." not in symbol:
                owner = symbol.split(".", 1)[0]
                implementation_symbol = f"Dragons.InvisibleDragon.Hvac.{owner}"
            elif "ToIdfObjects(...) ->" in native_route:
                owner = symbol.split(".", 1)[0]
                implementation_symbol = (
                    f"Dragons.InvisibleDragon.Hvac.{owner}.ToIdfObjects"
                )
            else:
                implementation_symbol = native_route
                if "(" in implementation_symbol:
                    implementation_symbol = implementation_symbol.split("(", 1)[0]
            if symbol == "GeothermalHeatPump.idf_objtypename":
                implementation_symbol = (
                    "Dragons.InvisibleDragon.Hvac.GeothermalHeatPump"
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Hvac/"
            "SupplyCoreOracleParityTests.cs"
        )
        supply_core_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Hvac.SupplyCoreOracleParityTests."
            "MatchesPinnedSupplyCoreThroughProductionPublicRoutes"
        )
        supply_core_fixture_sha256 = (
            "sha256:657b53b768c90a2915ca10c781ff63ab5a21323bb09f534d4d5da3178fe99194"
        )
        supply_core_generator_sha256 = (
            "sha256:7ce1af80729c2f2aa333016ba95db3963b25db24e1b23d2c89f49ea2694590e2"
        )
        supply_core_validator_sha256 = (
            "sha256:863eb92bbec8fe415e3c917ddf690e106beea5611bf39dbc1850a896c8d23622"
        )
        supply_core_test_sha256 = (
            "sha256:a4ff410f9ca074063ad50346d90217764e02cb9095053533e3b5241334fbc2e6"
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
                "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument"
            ):
                return (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/"
                    "EnergyModel.cs",
                    "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument",
                )
            if symbol in {
                "ElectricRadiantFloor.source",
                "ElectricRadiator.source",
            }:
                return (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs",
                    "Dragons.InvisibleDragon.Hvac.SupplySystem.Source",
                )
            if symbol == "PackagedAirConditioner.coolable":
                return (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "SupplySystems.cs",
                    "Dragons.InvisibleDragon.Hvac.AirHandlingUnit.CanCool",
                )
            if symbol == "SupplySystem.idf_get_objname":
                return (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs",
                    "Dragons.InvisibleDragon.Hvac.SupplySystem.ObjectNameFor",
                )
            if owner in {"SupplyGroup", "SupplySystem"}:
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "HvacAbstractions.cs"
                )
            elif owner in {"FanCoilUnit", "Radiator"}:
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "HydronicSupplySystems.cs"
                )
            else:
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "SupplySystems.cs"
                )

            if ".__init__" in symbol or "." not in symbol:
                implementation_symbol = f"Dragons.InvisibleDragon.Hvac.{owner}"
            else:
                implementation_symbol = native_route
                if " public " in implementation_symbol:
                    implementation_symbol = f"Dragons.InvisibleDragon.Hvac.{owner}"
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Hvac/"
            "AppendersControllersOracleParityTests.cs"
        )
        appender_controller_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Hvac."
            "AppendersControllersOracleParityTests."
            "MatchesPinnedAppendersControllersThroughPublicAggregateRoute"
        )
        appender_controller_fixture_sha256 = (
            "sha256:24b6994b1a39aa363fb0127ea6bfd93bcd12c803768e04f634ed615f08f815eb"
        )
        appender_controller_generator_sha256 = (
            "sha256:357763c4c73e48db275833ab884bf550ea5e143126f550520e9a748bb17154d6"
        )
        appender_controller_validator_sha256 = (
            "sha256:f6699787a997dc3daad0b8606b5581e93220d1a97476cad44f42052503730eb3"
        )
        appender_controller_test_sha256 = (
            "sha256:ef591e091d2966bb72ad147a1af8ef882fbdae2d8a8737d30ad62a6876983ca4"
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
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs"
        )
        appender_controller_implementation_symbol = (
            "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument"
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Hvac/"
            "MiscSystemsCoreOracleParityTests.cs"
        )
        misc_systems_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Hvac.MiscSystemsCoreOracleParityTests."
            "MatchesPinnedMiscSystemsThroughPublicProductionApis"
        )
        misc_systems_fixture_sha256 = (
            "sha256:c875ac4cd72e80aaa9de793807247597c5084cb70c96fab879d95747fdba962b"
        )
        misc_systems_generator_sha256 = (
            "sha256:ff4bb943baeefbee48be4a0e1a0eb467674cd6722c7c88c53b5e372d9f4ddc2f"
        )
        misc_systems_validator_sha256 = (
            "sha256:4dca901f5340002f9be7dcd3e669397d56b4413976601012d6da267c248159d6"
        )
        misc_systems_test_sha256 = (
            "sha256:ac8a4a7f670851ba181867d7c0ae9129d9a524f959a88517f573538d5528a958"
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
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs",
                    "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument",
                )
            if owner == "DomesticHotWater":
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "DomesticHotWater.cs"
                )
            else:
                self.assertIn(owner, {"EnergyRecoveryVentilator", "PhotoVoltaicPanel"})
                implementation_path = (
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/"
                    "VentilationAndPv.cs"
                )
            if ".__init__" in symbol or "." not in symbol:
                native_owner = {
                    "DomesticHotWater": "DomesticHotWater",
                    "EnergyRecoveryVentilator": "EnergyRecoveryVentilator",
                    "PhotoVoltaicPanel": "PhotovoltaicPanel",
                }[owner]
                implementation_symbol = f"Dragons.InvisibleDragon.Hvac.{native_owner}"
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
            "sha256:34793bc83100d9c527f1a7dce5e16dd4527ccb0e193e8db48e70ae35d3dfc7e1",
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Idd/"
            "ImugiIddDefinitionsCoreOracleParityTests.cs"
        )
        imugi_idd_definitions_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Idd."
            "ImugiIddDefinitionsCoreOracleParityTests."
            "MatchesPinnedImugiIddDefinitionsThroughPublicProductionApis"
        )
        imugi_idd_definitions_fixture_sha256 = (
            "sha256:5b586ac030309bed3ab840525b4c9cff207b97919cff76bb48e8003b9135bcf9"
        )
        imugi_idd_definitions_generator_sha256 = (
            "sha256:6b69716bca218db814bc1eb2411e19f1d9614cb5857f70e93e461e5c95fb1c0e"
        )
        imugi_idd_definitions_validator_sha256 = (
            "sha256:5135e0f97382969393313dbd4be353916f33454ad1241dffb257fdbcba303930"
        )
        imugi_idd_definitions_test_sha256 = (
            "sha256:f2e8895d44b6734b1eaa3fea7f1516a005b98b6bba8bacaabc1979e1d9a125e4"
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
                "bytes": 585481,
                "path": "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz",
                "sha256": "sha256:75f9d6c2efa32349704489aae4622b8647ac07f542e61cf3130624786436fa26",
            },
            imugi_idd_definitions_support["fixture"],
        )
        self.assertEqual(
            {
                "bytes": 38631,
                "path": "tools/python-reference/generate_idd_schema_oracle.py",
                "sha256": "sha256:29287f01c865d01c67bb25f1cb3e6d3f1466bed7859379342d7276124cf4cfc7",
            },
            imugi_idd_definitions_support["generator"],
        )
        self.assertEqual(
            "sha256:8225b83bdf960137d81363da69b81acd639b309eb394e845648dd041c3cff8f0",
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
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddDefinitions.cs": {
                "bytes": 12999,
                "sha256": "sha256:b6be5a2ac41a05f519d8103a816d90a0153fe21d64916671ff430c964c516f66",
            },
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddParser.cs": {
                "bytes": 19954,
                "sha256": "sha256:555b79f49740c1da4149002b9cb8e4507ea806eac10866098057f040e4fc55b3",
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
                    "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddParser.cs",
                    "Dragons.InvisibleDragon.Idd.IddParser.Parse",
                )
            implementation_path = (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/"
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
                    f"Dragons.InvisibleDragon.Idd.{owner}",
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Idd/"
            "ImugiIddSchemaStaticCoreOracleParityTests.cs"
        )
        imugi_idd_schema_static_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Idd."
            "ImugiIddSchemaStaticCoreOracleParityTests."
            "MatchesPinnedImugiIddSchemaStaticSemanticsThroughPublicProductionApis"
        )
        imugi_idd_schema_static_fixture_sha256 = (
            "sha256:93a074d69a9cc386a5898a3af5ed5580b05d523300073fe0fb6c0d93cd29a4ac"
        )
        imugi_idd_schema_static_generator_sha256 = (
            "sha256:9ad86909322e70b861f49640174b1f98fe9e0642433ea4bfe9b5ec0f33ffdd3e"
        )
        imugi_idd_schema_static_validator_sha256 = (
            "sha256:0bfd957baff75de2fa70302f3c0577a09e74633fa076d2b467d18c398551c23b"
        )
        imugi_idd_schema_static_test_sha256 = (
            "sha256:2d73d4435ccfdc78a2256b7f0cbe57d15dccbad19631efdb281e220d6851490c"
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
                "bytes": 70938,
                "path": "tools/python-reference/generate_imugi_idd_definitions_core_oracle.py",
                "sha256": "sha256:6b69716bca218db814bc1eb2411e19f1d9614cb5857f70e93e461e5c95fb1c0e",
            },
            imugi_idd_schema_static_support["base_generator"],
        )
        self.assertEqual(
            "sha256:8225b83bdf960137d81363da69b81acd639b309eb394e845648dd041c3cff8f0",
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
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddDefinitions.cs": {
                "bytes": 12999,
                "sha256": "sha256:b6be5a2ac41a05f519d8103a816d90a0153fe21d64916671ff430c964c516f66",
            },
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddParser.cs": {
                "bytes": 19954,
                "sha256": "sha256:555b79f49740c1da4149002b9cb8e4507ea806eac10866098057f040e4fc55b3",
            },
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/IddSchemaCache.cs": {
                "bytes": 11242,
                "sha256": "sha256:55ddd0da5501f24296b36c2ae6c31fc52e8a50832e3ffc8f783849e51b6af3c7",
            },
            "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Common/EnergyPlusVersion.cs": {
                "bytes": 4951,
                "sha256": "sha256:e28760c5903fa7c4e842620a7ba91c15947eb3378812a72262041af4397bd5a1",
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
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/"
                "IddDefinitions.cs"
            )
            parser_path = (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/"
                "IddParser.cs"
            )
            cache_path = (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idd/"
                "IddSchemaCache.cs"
            )
            if symbol == "IDD.load":
                return (
                    cache_path,
                    "Dragons.InvisibleDragon.Idd.IddSchemaCache.Read",
                )
            if symbol == "IDD.to_pickle":
                return (
                    cache_path,
                    "Dragons.InvisibleDragon.Idd.IddSchemaCache.Write",
                )
            if symbol == "IDD.read_idd":
                return (
                    parser_path,
                    "Dragons.InvisibleDragon.Idd.IddParser.ParseFile",
                )
            if symbol == "VersionIdentificationError":
                return (
                    parser_path,
                    "Dragons.InvisibleDragon.Idd.IddParser.Parse",
                )
            if symbol == "InvalidFieldValue":
                return (
                    definitions_path,
                    "Dragons.InvisibleDragon.Idd.IddFieldDefinition",
                )
            if symbol == "InvalidParentManagement":
                return (
                    definitions_path,
                    "Dragons.InvisibleDragon.Idd.IddObjectDefinition",
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
                    "Dragons.InvisibleDragon.Idd.IddSchema",
                )
            if symbol.startswith("StaticIndexedDict."):
                return (
                    definitions_path,
                    "Dragons.InvisibleDragon.Idd.IddSchema.Objects",
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Idf/"
            "ImugiIdfObjectCoreOracleParityTests.cs"
        )
        imugi_idf_object_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Idf."
            "ImugiIdfObjectCoreOracleParityTests."
            "MatchesPinnedImugiIdfObjectThroughPublicProductionApis"
        )
        imugi_idf_object_fixture_sha256 = (
            "sha256:61c137044af671cd9a1a935fea516b3d72eaa74f3d3c5122b3a61acef981cc93"
        )
        imugi_idf_object_generator_sha256 = (
            "sha256:3e87aaf0501d1176ab1ffb2be07710d1c8e6c58ef061101b4a70b14eb6f8b7f7"
        )
        imugi_idf_object_validator_sha256 = (
            "sha256:054a927afa780027119b67634e6b84196404160ac23dd9b10c99049444b16a25"
        )
        imugi_idf_object_test_sha256 = (
            "sha256:a528717ddb7b5208edbff92f0f00f2e6ec00ca274dcc8dfed41d3b229ee707cf"
        )
        for pinned_path, expected_bytes, expected_sha256 in (
            (
                imugi_idf_object_fixture_path,
                119037,
                imugi_idf_object_fixture_sha256,
            ),
            (
                imugi_idf_object_generator_path,
                30077,
                imugi_idf_object_generator_sha256,
            ),
            (
                imugi_idf_object_validator_path,
                11109,
                imugi_idf_object_validator_sha256,
            ),
            (
                REPOSITORY_ROOT / imugi_idf_object_test_path,
                37916,
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
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/"
                "IdfModel.cs"
            )
            parser_path = (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/"
                "IdfParser.cs"
            )
            writer_path = (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/"
                "IdfWriter.cs"
            )
            if symbol in {"IDF.__str__", "IdfObject.__str__"}:
                return writer_path, "Dragons.InvisibleDragon.Idf.IdfWriter.Write"
            if symbol == "IDF.append":
                return model_path, "Dragons.InvisibleDragon.Idf.IdfDocument.Append"
            if symbol == "IDF.read_idf":
                return parser_path, "Dragons.InvisibleDragon.Idf.IdfParser.ParseFile"
            if symbol == "IdfObject.idd":
                return model_path, "Dragons.InvisibleDragon.Idf.IdfObject.Definition"
            if symbol == "IDF" or symbol.startswith("IDF."):
                return model_path, "Dragons.InvisibleDragon.Idf.IdfDocument"
            return model_path, "Dragons.InvisibleDragon.Idf.IdfObject"

        self.assertEqual(
            "Dragons.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)",
            imugi_idf_object_contract["native_routes"]["IDF.append"],
        )
        idf_model_text = (
            REPOSITORY_ROOT
            / "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs"
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
            "tests/InvisibleDragon/Dragons.InvisibleDragon.Core.Tests/Idf/"
            "ImugiIdfObjectListCoreOracleParityTests.cs"
        )
        imugi_idf_object_list_test_symbol = (
            "Dragons.InvisibleDragon.Tests.Idf."
            "ImugiIdfObjectListCoreOracleParityTests."
            "MatchesPinnedImugiIdfObjectListThroughPublicProductionApis"
        )
        imugi_idf_object_list_fixture_sha256 = (
            "sha256:4c4da9b23f38805b4550aa5c75c5f2899ebec336a43c7718188e267f77373767"
        )
        imugi_idf_object_list_generator_sha256 = (
            "sha256:8243d0a6f8289209d088a7e679bf84da53cc0cedf75dbdea140596d2e0a452ca"
        )
        imugi_idf_object_list_validator_sha256 = (
            "sha256:e66605a5c403fc186be87427bd64a9f832c3e7085768788774d760f86b9bad81"
        )
        imugi_idf_object_list_test_sha256 = (
            "sha256:e90e239b3f8445120cb7774b8081692af09cc998a05385d5af3aa20b1a120f14"
        )
        for pinned_path, expected_bytes, expected_sha256 in (
            (
                imugi_idf_object_list_fixture_path,
                105110,
                imugi_idf_object_list_fixture_sha256,
            ),
            (
                imugi_idf_object_list_generator_path,
                22811,
                imugi_idf_object_list_generator_sha256,
            ),
            (
                imugi_idf_object_list_validator_path,
                7509,
                imugi_idf_object_list_validator_sha256,
            ),
            (
                REPOSITORY_ROOT / imugi_idf_object_list_test_path,
                23633,
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
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/"
                "IdfModel.cs"
            )
            validator_path = (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/"
                "IdfValidator.cs"
            )
            writer_path = (
                "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/"
                "IdfWriter.cs"
            )
            if symbol == "IdfObjectList.__str__":
                return writer_path, "Dragons.InvisibleDragon.Idf.IdfWriter.Write"
            if symbol == "IdfObjectList.check_validity":
                return (
                    validator_path,
                    "Dragons.InvisibleDragon.Idf.IdfValidator.Validate",
                )
            if symbol == "IdfObjectList.append":
                return (
                    model_path,
                    "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Append",
                )
            if symbol == "IdfObjectList.insert":
                return (
                    model_path,
                    "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Insert",
                )
            if symbol == "IdfObjectList.names":
                return model_path, "Dragons.InvisibleDragon.Idf.IdfObject.Name"
            return (
                model_path,
                "Dragons.InvisibleDragon.Idf.IdfObjectCollection",
            )

        self.assertEqual(
            "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Append(IdfObject)",
            imugi_idf_object_list_contract["native_routes"][
                "IdfObjectList.append"
            ],
        )
        self.assertEqual(
            "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Insert(int, IdfObject)",
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

    def test_rejects_non_dragons_product_ownership(self) -> None:
        with TemporaryWorkspace() as workspace:
            lock, port_map, exceptions = write_configuration(
                workspace,
                project="OtherCompany.Product.Core",
            )

            with self.assertRaisesRegex(ConfigurationError, "InvisibleDragon or SimpleDragon"):
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

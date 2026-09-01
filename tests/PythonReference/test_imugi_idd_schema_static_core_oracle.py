"""Fail-closed tests for the Imugi IDD/schema/static-container oracle."""

from __future__ import annotations

from collections import Counter
import copy
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEPENDENCY_ROOT = (
    REPOSITORY_ROOT / ".tools" / "python-reference" / "3.12.7" / "site-packages"
)
if DEPENDENCY_ROOT.is_dir():
    sys.path.insert(0, str(DEPENDENCY_ROOT))
GENERATOR_PATH = (
    REPOSITORY_ROOT
    / "tools"
    / "python-reference"
    / "generate_imugi_idd_schema_static_core_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "imugi-idd-schema-static-core-oracle.json"
)
FINAL_PROMOTION_FIXTURE_PATHS = tuple(
    FIXTURE_PATH.parent / name
    for name in (
        "imugi-idd-definitions-core-oracle.json",
        "imugi-idd-schema-static-core-oracle.json",
        "imugi-idf-object-core-oracle.json",
        "imugi-idf-object-list-core-oracle.json",
    )
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_imugi_idd_schema_static_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load Imugi batch-2 generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 50_620
EXPECTED_GENERATOR_SHA256 = (
    "sha256:aae0ce640c69f571dda0e82b0a02e303505a22331a96083115174421a15f1a83"
)
EXPECTED_FIXTURE_BYTES = 124_762
EXPECTED_FIXTURE_SHA256 = (
    "sha256:86f8dedc692e58dd7f3836d295a78bd9a9ef3dd71e84dee75be6ef44f228eea0"
)


class ImugiIddSchemaStaticCoreOracleTests(unittest.TestCase):
    fixture_value: dict[str, object]

    @classmethod
    def setUpClass(cls) -> None:
        cls.fixture_value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(cls.fixture_value)

    @classmethod
    def fixture(cls) -> dict[str, object]:
        return cls.fixture_value

    @classmethod
    def changed_fixture(cls) -> dict[str, object]:
        return copy.deepcopy(cls.fixture_value)

    @classmethod
    def facts(cls, code: str) -> dict[str, object]:
        matches = [
            case["python"]["facts"]
            for case in cls.fixture_value["cases"]
            if case["code"] == code
        ]
        if len(matches) != 1:
            raise AssertionError(f"Expected one Imugi batch-2 case {code}.")
        return matches[0]

    @staticmethod
    def decoded(value: dict[str, object]) -> object:
        kind = value.get("kind")
        if kind == "none":
            return None
        if kind in {"bool", "str"}:
            return value["value"]
        if kind == "int":
            return int(value["value"])
        if kind == "float":
            result = float.fromhex(value["hex"])
            if not math.isfinite(result) or repr(result) != value["repr"]:
                raise AssertionError("Canonical finite float drifted.")
            return result
        if kind in {"list", "tuple"}:
            items = [ImugiIddSchemaStaticCoreOracleTests.decoded(item) for item in value["items"]]
            return items if kind == "list" else tuple(items)
        if kind == "dict":
            return {
                ImugiIddSchemaStaticCoreOracleTests.decoded(item["key"]):
                ImugiIddSchemaStaticCoreOracleTests.decoded(item["value"])
                for item in value["items"]
            }
        raise AssertionError(f"Unsupported encoded value: {value!r}")

    @staticmethod
    def regenerate(output: Path) -> None:
        environment = os.environ.copy()
        environment["PYTHONHASHSEED"] = "0"
        environment["PYTHONUTF8"] = "1"
        environment["PYTHONDONTWRITEBYTECODE"] = "1"
        subprocess.run(
            [
                sys.executable,
                "-B",
                "-X",
                "utf8",
                str(BOOTSTRAP_PATH),
                "--dependency-root",
                str(DEPENDENCY_ROOT),
                "--upstream-source",
                str(PINNED_SOURCE_ROOT),
                "--generator",
                str(GENERATOR_PATH),
                "--",
                "--inventory",
                str(INVENTORY_PATH),
                "--output",
                str(output),
                "--upstream-commit",
                generator.EXPECTED_UPSTREAM_COMMIT,
            ],
            cwd=REPOSITORY_ROOT,
            env=environment,
            check=True,
            capture_output=True,
            text=True,
        )

    @staticmethod
    def reseal(value: dict[str, object]) -> None:
        value["fact_sha256"] = {
            case["id"]: generator.canonical_sha256(case["python"]["facts"])
            for case in value["cases"]
        }
        for case in value["cases"]:
            case["python"]["facts_sha256"] = value["fact_sha256"][case["id"]]
        value["case_sha256"] = generator.case_sha256(value["cases"])
        value["cases_sha256"] = generator.cases_sha256(value["cases"])

    def test_generator_fixture_and_all_hash_layers_are_exact(self) -> None:
        value = self.fixture()
        generator_raw = GENERATOR_PATH.read_bytes()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_raw))
        self.assertEqual(EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH))
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(generator.EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertEqual(8, len(value["cases"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )
        self.assertEqual(
            {
                "batch1_resolved_receipts",
                "case_sha256",
                "cases",
                "cases_sha256",
                "consumer_contract",
                "deferred_receipts",
                "fact_sha256",
                "native_review",
                "out_of_scope_receipts",
                "runtime",
                "schema",
                "support",
                "symbols",
                "target_receipts",
                "upstream",
            },
            set(value),
        )

    def test_two_independent_bootstrap_regenerations_are_byte_identical(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="imugi-idd-schema-static-regeneration-", dir=TEST_TEMP_ROOT
        ) as temporary:
            first = Path(temporary) / "first.json"
            second = Path(temporary) / "second.json"
            self.regenerate(first)
            self.regenerate(second)
            baseline = FIXTURE_PATH.read_bytes()
            self.assertEqual(baseline, first.read_bytes())
            self.assertEqual(first.read_bytes(), second.read_bytes())
            self.assertEqual(
                EXPECTED_FIXTURE_SHA256,
                "sha256:" + hashlib.sha256(baseline).hexdigest(),
            )

    def test_exact_133_declaration_partition_and_matrix_state(self) -> None:
        value = self.fixture()
        expected_targets = [1095, 1097, *range(1100, 1108), *range(1217, 1228)]
        self.assertEqual(expected_targets, list(generator.TARGET_INDICES))
        self.assertEqual(21, len(value["target_receipts"]))
        self.assertEqual(40, len(value["batch1_resolved_receipts"]))
        self.assertEqual(44, len(value["deferred_receipts"]))
        self.assertEqual(28, len(value["out_of_scope_receipts"]))
        partitions = (
            set(generator.TARGET_INDICES),
            set(generator.BATCH1_RESOLVED_INDICES),
            set(generator.DEFERRED_INDICES),
            set(generator.OUT_OF_SCOPE_INDICES),
        )
        for index, left in enumerate(partitions):
            for right in partitions[index + 1:]:
                self.assertFalse(left & right)
        self.assertEqual(
            list(range(1095, 1228)), sorted(set().union(*partitions))
        )
        receipt_specs = (
            ("target_receipts", generator.EXPECTED_TARGET_RECEIPTS_SHA256),
            (
                "batch1_resolved_receipts",
                generator.EXPECTED_BATCH1_RESOLVED_RECEIPTS_SHA256,
            ),
            ("deferred_receipts", generator.EXPECTED_DEFERRED_RECEIPTS_SHA256),
            (
                "out_of_scope_receipts",
                generator.EXPECTED_OUT_OF_SCOPE_RECEIPTS_SHA256,
            ),
        )
        for key, expected_hash in receipt_specs:
            self.assertEqual(expected_hash, generator.canonical_sha256(value[key]))
        closure = value["consumer_contract"]["closure"]
        self.assertEqual(133, closure["source_declaration_count"])
        self.assertEqual(21, closure["target_count"])
        self.assertEqual(40, closure["batch1_resolved_count"])
        self.assertEqual(44, closure["deferred_count"])
        self.assertEqual(28, closure["out_of_scope_count"])
        self.assertTrue(closure["matrix_batch1_promotion_deferred"])

        matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        self.assertEqual(
            {
                "equivalent": 413,
                "exception": 577,
                "needs_reverification": 0,
                "out_of_scope": 252,
            },
            matrix["summary"]["classification_counts"],
        )

        promoted_by_index = {}
        for fixture_path in FINAL_PROMOTION_FIXTURE_PATHS:
            promoted_fixture = generator.load_json_without_duplicates(fixture_path)
            classifications = promoted_fixture["consumer_contract"]["classifications"]
            receipts = promoted_fixture["target_receipts"]
            self.assertEqual(
                set(classifications), {receipt["symbol"] for receipt in receipts}
            )
            for receipt in receipts:
                inventory_index = receipt["inventory_index"]
                self.assertNotIn(inventory_index, promoted_by_index)
                promoted_by_index[inventory_index] = (
                    receipt["symbol"],
                    classifications[receipt["symbol"]],
                )

        promoted_indices = set().union(
            set(generator.TARGET_INDICES),
            set(generator.BATCH1_RESOLVED_INDICES),
            set(generator.DEFERRED_INDICES),
        )
        self.assertEqual(promoted_indices, set(promoted_by_index))
        self.assertEqual(
            Counter({"equivalent": 37, "exception": 68}),
            Counter(classification for _, classification in promoted_by_index.values()),
        )
        for key in (
            "target_receipts",
            "batch1_resolved_receipts",
            "deferred_receipts",
        ):
            for receipt in value[key]:
                expected_symbol, expected_classification = promoted_by_index[
                    receipt["inventory_index"]
                ]
                self.assertEqual(expected_symbol, receipt["symbol"])
                self.assertEqual(
                    expected_classification,
                    matrix["classifications"][receipt["inventory_index"]],
                    receipt["symbol"],
                )
        for receipt in value["out_of_scope_receipts"]:
            self.assertEqual(
                "out_of_scope",
                matrix["classifications"][receipt["inventory_index"]],
                receipt["symbol"],
            )

    def test_consumer_contract_is_exact_9_equivalent_12_exception(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"exception": 12, "equivalent": 9}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.EXCEPTION_SYMBOLS, set(contract["adaptations"]))
        self.assertEqual(21, len(contract["assertion_ids"]))
        self.assertEqual(21, len(set(contract["assertion_ids"].values())))
        self.assertEqual(21, len(contract["coverage_by_symbol"]))
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(contract["expectations"]))
        for symbol, expectation in contract["expectations"].items():
            self.assertEqual(
                generator.CLASSIFICATIONS[symbol], expectation["classification"]
            )
            self.assertEqual(generator.NATIVE_ROUTES[symbol], expectation["native_route"])
            self.assertIn("Dragons.InvisibleDragon.Idd", expectation["native_route"])
            self.assertNotIn(".Internal", expectation["native_route"])
            self.assertEqual(
                generator.ADAPTATIONS.get(symbol, "not_applicable"),
                expectation["adaptation"],
            )
        evidence = contract["evidence_contract"]
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertFalse(evidence["native_runtime_executed_by_python_oracle"])
        self.assertFalse(evidence["structural_only"])
        self.assertTrue(evidence["exact_cpython_behavior_oracle"])
        self.assertTrue(evidence["path_independent_relocated_import"])
        self.assertTrue(evidence["full_energyplus_idd_support_hash_pinned"])

    def test_runtime_relocation_sources_and_full_idd_support_are_exact(self) -> None:
        value = self.fixture()
        runtime = value["runtime"]
        self.assertEqual("cpython", runtime["implementation"])
        self.assertEqual("3.12.7", runtime["python_version"])
        self.assertEqual(generator.EXPECTED_DEPENDENCIES, runtime["dependencies"])
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(
                value["consumer_contract"]["runtime_signatures"]
            ),
        )
        isolated = value["upstream"]["isolated_import"]
        self.assertEqual(2, isolated["source_location_count"])
        self.assertEqual(
            generator.EXPECTED_LOADED_LOCAL_MODULES_SHA256,
            isolated["loaded_local_modules_sha256"],
        )
        self.assertEqual(
            generator.EXPECTED_RELOCATED_OBSERVATIONS_SHA256,
            isolated["relocated_observations_sha256"],
        )
        self.assertEqual(generator._native_review(), value["native_review"])
        self.assertEqual(
            generator.EXPECTED_NATIVE_REVIEW_SHA256,
            generator.canonical_sha256(value["native_review"]),
        )
        self.assertEqual(
            generator.BASE_GENERATOR_RECEIPT, value["support"]["base_generator"]
        )
        support = value["support"]["energyplus_idd"]
        self.assertEqual(generator._base._support_receipt(), support)
        identity = support["full_schema_identity"]
        self.assertEqual("24.2.0", identity["energyplus_version"])
        self.assertEqual("94a887817b", identity["energyplus_build"])
        self.assertEqual(848, identity["object_count"])
        self.assertEqual(13_702, identity["field_count"])

    def test_exception_and_static_indexed_dict_behavior_is_exact(self) -> None:
        exceptions = self.facts("A01")
        self.assertTrue(exceptions["types_are_distinct"])
        for name in (
            "InvalidFieldValue",
            "InvalidParentManagement",
            "VersionIdentificationError",
        ):
            self.assertTrue(exceptions["classes"][name]["subclass_exception"])
            self.assertEqual("Exception", exceptions["classes"][name]["class_shape"]["bases"][0])
            self.assertEqual(name, exceptions["raised"][name]["type"])

        construction = self.facts("B01")
        self.assertEqual(("Alpha", "Beta"), self.decoded(construction["allowed_keys_after_source_mutation"]))
        self.assertFalse(construction["allowed_keys_identity_preserved"])
        self.assertEqual("AttributeError", construction["allowed_keys_property_assignment"]["type"])
        self.assertEqual("KeyError", construction["initial_unallowed_key"]["type"])
        self.assertEqual("AttributeError", construction["non_string_allowed_key"]["type"])

        read = self.facts("C01")
        self.assertEqual(2, self.decoded(read["bool_index"]))
        self.assertEqual(1, self.decoded(read["case_insensitive"]))
        self.assertEqual(2, self.decoded(read["negative_integer"]))
        self.assertEqual("IndexError", read["index_at_count"]["type"])
        self.assertEqual("KeyError", read["missing_string"]["type"])
        self.assertEqual("TypeError", read["unsupported_key_type"]["type"])

        write = self.facts("D01")
        self.assertEqual(
            [("Alpha", 10), ("Beta", 20)],
            self.decoded(write["after_negative_integer_write"]),
        )
        self.assertEqual("KeyError", write["new_key_rejected"]["type"])
        self.assertEqual("IndexError", write["index_at_count"]["type"])

        views = self.facts("E01")
        self.assertEqual(
            {"items": "dict_items", "keys": "dict_keys", "values": "dict_values"},
            views["view_types"],
        )
        self.assertEqual(
            [99, 2], self.decoded(views["after_value_update"]["values"])
        )

    def test_idd_construction_read_and_pickle_cache_behavior_is_exact(self) -> None:
        constructed = self.facts("F01")
        self.assertTrue(constructed["case_insensitive_object_lookup"])
        self.assertTrue(constructed["integer_object_lookup"])
        self.assertEqual(["Source:Object"], self.decoded(constructed["required_objects"]))
        self.assertEqual((24, 2, 0), self.decoded(constructed["version"]["components"]))
        self.assertEqual("24.2.0", constructed["version"]["formatted_dot"])
        self.assertEqual("AttributeError", constructed["referenced_map_obj"]["type"])
        self.assertEqual(1, constructed["duplicate_object_resolution"]["count"])
        self.assertTrue(constructed["duplicate_object_resolution"]["stored_second_identity"])
        self.assertEqual(
            {"SourceClasses": ["Source:Object"]},
            self.decoded(constructed["reference_map_cls"]),
        )

        read = self.facts("G01")
        self.assertEqual("VersionIdentificationError", read["invalid_version_marker"]["type"])
        self.assertEqual("Version", read["parsed"]["object_name"])
        self.assertEqual("Version Identifier", read["parsed"]["field_name"])
        self.assertEqual(24.2, self.decoded(read["parsed"]["field_default"]))
        self.assertEqual((24, 2, 0), self.decoded(read["parsed"]["version"]))

        cache = self.facts("H01")
        self.assertTrue(cache["cached_identity"])
        self.assertTrue(cache["roundtrip_is_distinct_instance"])
        self.assertEqual("idd_V24-2-0.pkl", cache["file_name"])
        self.assertEqual(1_685, cache["file_bytes"])
        self.assertEqual(
            "sha256:0e4d7b54fa6aa4b98fc270a44e93d9942cabaf1d76f54136e75b25e769ef0516",
            cache["file_sha256"],
        )
        self.assertEqual(
            ["Source:Object", "Target:Object"],
            self.decoded(cache["loaded_object_names"]),
        )

    def test_validation_rejects_resealed_and_structural_tampering(self) -> None:
        changed = self.changed_fixture()
        changed["cases"][2]["python"]["facts"]["bool_index"]["value"] = "1"
        self.reseal(changed)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["consumer_contract"]["classifications"]["IDD.read_idd"] = "exception"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["target_receipts"][0]["symbol"] = "IDD.Drifted"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["batch1_resolved_receipts"].pop()
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["deferred_receipts"].pop()
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["support"]["energyplus_idd"]["full_schema_identity"]["object_count"] = 847
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["upstream"]["isolated_import"]["source_location_count"] = 1
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

    def test_duplicate_keys_paths_addresses_guids_timestamps_and_raw_floats_fail(self) -> None:
        with self.assertRaises(generator.DuplicateJsonKeyError):
            generator.load_json_without_duplicates_text('{"a":1,"a":2}')
        with self.assertRaises(generator.NonFiniteJsonConstantError):
            generator.load_json_without_duplicates_text('{"a":NaN}')
        with self.assertRaises(ValueError):
            generator.strict_json_dumps({"raw": float("inf")})
        unsafe = (
            r"C:\\Users\\someone\\fixture.json",
            "/tmp/fixture.json",
            "object at 0x1234abcd",
            "123e4567-e89b-12d3-a456-426614174000",
            "2026-08-28T12:34:56",
        )
        for value in unsafe:
            with self.subTest(value=value), self.assertRaises(RuntimeError):
                generator._base._validate_safe_tree({"unsafe": value})


if __name__ == "__main__":
    unittest.main()

"""Fail-closed tests for the Imugi IDD definition core oracle."""

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
    / "generate_imugi_idd_definitions_core_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "imugi-idd-definitions-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_imugi_idd_definitions_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load Imugi IDD definition generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 70_965
EXPECTED_GENERATOR_SHA256 = (
    "sha256:fa70dfc565a30542f58697cee512701356cf2200b3f07332de4e345f0b7b1398"
)
EXPECTED_FIXTURE_BYTES = 165_323
EXPECTED_FIXTURE_SHA256 = (
    "sha256:3e56e7fe6026fef3146a62aadf3248940c65aa9a2b5c624b519fbc0e3d99dd69"
)


class ImugiIddDefinitionsCoreOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one Imugi IDD definition case {code}.")
        return matches[0]

    @staticmethod
    def finite(value: dict[str, str]) -> float:
        if value.get("kind") != "float":
            raise AssertionError(f"Expected a canonical finite float: {value!r}")
        result = float.fromhex(value["hex"])
        if not math.isfinite(result) or repr(result) != value["repr"]:
            raise AssertionError("Canonical finite float drifted.")
        return result

    @staticmethod
    def scalar(value: dict[str, object]) -> object:
        kind = value.get("kind")
        if kind == "none":
            return None
        if kind in {"bool", "str"}:
            return value["value"]
        if kind == "int":
            return int(value["value"])
        if kind == "float":
            return ImugiIddDefinitionsCoreOracleTests.finite(value)
        raise AssertionError(f"Expected an encoded scalar: {value!r}")

    @staticmethod
    def sequence(value: dict[str, object]) -> list[object]:
        if value.get("kind") not in {"list", "tuple"}:
            raise AssertionError(f"Expected an encoded sequence: {value!r}")
        return [
            ImugiIddDefinitionsCoreOracleTests.scalar(item)
            for item in value["items"]
        ]

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

    def test_generator_fixture_and_every_hash_layer_are_exact(self) -> None:
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
        self.assertEqual(8, len(value["fact_sha256"]))
        self.assertEqual(8, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )
        self.assertEqual(
            {
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
            prefix="imugi-idd-definitions-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_matrix_and_full_133_declaration_partition_are_exact(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_targets = [
            *range(1123, 1126),
            *range(1128, 1151),
            *range(1153, 1167),
        ]
        self.assertEqual(expected_targets, list(generator.TARGET_INDICES))
        self.assertEqual(40, len(inventory["target_receipts"]))
        self.assertEqual(65, len(inventory["deferred_receipts"]))
        self.assertEqual(28, len(inventory["out_of_scope_receipts"]))
        self.assertEqual(
            list(range(1095, 1228)),
            sorted(
                (
                    *generator.TARGET_INDICES,
                    *generator.DEFERRED_INDICES,
                    *generator.OUT_OF_SCOPE_INDICES,
                )
            ),
        )
        self.assertFalse(set(generator.TARGET_INDICES) & set(generator.DEFERRED_INDICES))
        self.assertFalse(
            set(generator.TARGET_INDICES) & set(generator.OUT_OF_SCOPE_INDICES)
        )
        self.assertFalse(
            set(generator.DEFERRED_INDICES) & set(generator.OUT_OF_SCOPE_INDICES)
        )
        self.assertEqual(
            generator.EXPECTED_TARGET_RECEIPTS_SHA256,
            generator.canonical_sha256(value["target_receipts"]),
        )
        self.assertEqual(
            generator.EXPECTED_DEFERRED_RECEIPTS_SHA256,
            generator.canonical_sha256(value["deferred_receipts"]),
        )
        self.assertEqual(
            generator.EXPECTED_OUT_OF_SCOPE_RECEIPTS_SHA256,
            generator.canonical_sha256(value["out_of_scope_receipts"]),
        )

        closure = value["consumer_contract"]["closure"]
        self.assertTrue(closure["exact_one_case_target_partition"])
        self.assertTrue(closure["full_imugi_source_partition"])
        self.assertEqual(133, closure["source_declaration_count"])
        self.assertEqual(40, closure["target_count"])
        self.assertEqual(65, closure["deferred_count"])
        self.assertEqual(28, closure["out_of_scope_count"])

        matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        for receipt in value["target_receipts"]:
            classification = matrix["classifications"][receipt["inventory_index"]]
            self.assertIn(
                classification,
                {"needs_reverification", generator.CLASSIFICATIONS[receipt["symbol"]]},
            )
        for receipt in value["deferred_receipts"]:
            self.assertIn(
                matrix["classifications"][receipt["inventory_index"]],
                {"needs_reverification", "equivalent", "exception"},
            )
        for receipt in value["out_of_scope_receipts"]:
            self.assertEqual(
                "out_of_scope",
                matrix["classifications"][receipt["inventory_index"]],
                receipt["symbol"],
            )

    def test_consumer_contract_is_exact_18_equivalent_22_exception(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"exception": 22, "equivalent": 18}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.EXCEPTION_SYMBOLS, set(contract["adaptations"]))
        self.assertEqual(40, len(contract["assertion_ids"]))
        self.assertEqual(40, len(set(contract["assertion_ids"].values())))
        self.assertEqual(40, len(contract["coverage_by_symbol"]))
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(contract["expectations"]))
        self.assertEqual(
            Counter({symbol: 1 for symbol in generator.TARGET_SYMBOLS}),
            Counter(
                symbol
                for case in self.fixture()["cases"]
                for symbol in case["target_symbols"]
            ),
        )
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
        self.assertTrue(evidence["exact_cpython_behavior_oracle"])
        self.assertTrue(evidence["path_independent_relocated_import"])
        self.assertTrue(evidence["full_energyplus_idd_support_hash_pinned"])

    def test_runtime_relocation_dependencies_native_sources_and_support_are_exact(self) -> None:
        value = self.fixture()
        runtime = value["runtime"]
        self.assertEqual("cpython", runtime["implementation"])
        self.assertEqual("3.12.7", runtime["python_version"])
        self.assertEqual(generator.EXPECTED_DEPENDENCIES, runtime["dependencies"])
        self.assertEqual(
            generator.canonical_sha256(generator.EXPECTED_DEPENDENCIES),
            runtime["dependencies_sha256"],
        )
        signatures = value["consumer_contract"]["runtime_signatures"]
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(signatures),
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
        self.assertEqual(
            [item["module"] for item in generator.SOURCE_SPECS],
            [item["module"] for item in isolated["loaded_local_modules"]],
        )
        self.assertEqual(generator._native_review(), value["native_review"])
        self.assertEqual(
            generator.EXPECTED_NATIVE_REVIEW_SHA256,
            generator.canonical_sha256(value["native_review"]),
        )
        self.assertEqual(generator._support_receipt(), value["support"])
        support = value["support"]
        self.assertEqual(848, support["full_schema_identity"]["object_count"])
        self.assertEqual(13_702, support["full_schema_identity"]["field_count"])
        self.assertEqual(
            generator.SUPPORT_FIXTURE_RECEIPT,
            support["fixture"],
        )
        self.assertEqual(
            generator.SUPPORT_GENERATOR_RECEIPT,
            support["generator"],
        )

    def test_field_construction_properties_equality_and_fragment_parser_are_exact(self) -> None:
        construction = self.facts("A01")
        self.assertEqual(["object"], construction["class_shape"]["bases"])
        self.assertIn("key=[]", construction["constructor_signature"])
        self.assertEqual(
            {
                "key": True,
                "object_list": True,
                "reference": True,
                "reference_cls": True,
                "referenceable": False,
            },
            construction["mutable_default_aliases"],
        )
        self.assertTrue(construction["shared_default_key_mutation_visible"])
        self.assertEqual("TypeError", construction["keyword_only_rejection"]["type"])
        explicit = construction["explicit_state"]
        self.assertEqual("Mode", self.scalar(explicit["name"]))
        self.assertEqual("- First sentence.\n- Second sentence.", self.scalar(explicit["memo"]))
        self.assertEqual(1.25, self.scalar(explicit["default"]))
        self.assertEqual(["On", "Off"], self.sequence(explicit["key"]))

        equality = self.facts("B01")
        self.assertTrue(equality["equal_before_mutation"])
        self.assertTrue(equality["equal_referenceable_ignored"])
        self.assertFalse(equality["different_key"])
        self.assertFalse(equality["equality_after_mutating_other_key"])
        self.assertEqual("TypeError", equality["wrong_type"]["type"])

        parsed = self.facts("C01")
        self.assertEqual("BasedOn:A2", self.scalar(parsed["based_on_field_unit"]))
        self.assertEqual("TypeError", parsed["exclusive_minimum_failure"]["type"])
        self.assertEqual("TypeError", parsed["exclusive_maximum_failure"]["type"])
        defaults = parsed["numeric_default_coercion"]
        self.assertEqual(0.0, self.scalar(defaults["0"]))
        self.assertEqual(12.5, self.scalar(defaults["12.5"]))
        self.assertEqual("-1", self.scalar(defaults["-1"]))
        self.assertEqual("1e3", self.scalar(defaults["1e3"]))
        self.assertEqual("KeyError", parsed["unknown_directive"]["type"])
        representative = parsed["representative_state"]
        self.assertEqual("m", self.scalar(representative["unit"]))
        self.assertEqual(["ReferenceClasses"], self.sequence(representative["reference_cls"]))

        properties = self.facts("D01")
        self.assertEqual(
            [
                "name",
                "memo",
                "is_required",
                "is_extensible",
                "unit",
                "minimum",
                "maximum",
                "default",
                "is_deprecated",
                "is_autosizable",
                "is_autocalculatable",
                "type",
                "is_retaincase",
                "key",
                "object_list",
                "external_list",
                "reference",
                "reference_cls",
                "referenceable",
            ],
            properties["property_names"],
        )
        self.assertEqual(
            ["A", "B", "C"],
            self.sequence(properties["after_external_list_mutation"]["key"]),
        )

    def test_object_construction_properties_equality_and_fragment_parser_are_exact(self) -> None:
        construction = self.facts("E01")
        self.assertEqual(["StaticIndexedDict"], construction["class_shape"]["bases"])
        self.assertEqual(
            {"field_count": 1, "stored_second_identity": True},
            construction["duplicate_field_resolution"],
        )
        self.assertEqual("TypeError", construction["invalid_positional_field"]["type"])
        explicit = construction["explicit_state"]
        self.assertEqual("Test:Object", self.scalar(explicit["name"]))
        self.assertEqual(["A1", "N1"], self.sequence(explicit["idd_index"]))
        self.assertEqual(["Name"], self.sequence(explicit["required_fields"]))
        self.assertEqual(["Unnamed", 1.5], self.sequence(explicit["default"]))

        equality = self.facts("F01")
        self.assertTrue(equality["equal"])
        self.assertTrue(equality["identity"])
        self.assertTrue(equality["idd_index_ignored"])
        self.assertFalse(equality["different_field"])
        self.assertFalse(equality["different_attribute"])
        self.assertEqual("TypeError", equality["wrong_type"]["type"])

        parsed = self.facts("G01")
        representative = parsed["representative_state"]
        self.assertEqual("Test:Object", self.scalar(representative["name"]))
        self.assertEqual(2, self.scalar(representative["extensible"]))
        self.assertEqual("A2", self.scalar(representative["begin_extensible"]))
        self.assertEqual(
            ["A1", "N1", "A2", "N2"],
            self.sequence(representative["idd_index"]),
        )
        self.assertTrue(self.scalar(representative["is_obsolete"]))
        self.assertEqual("KeyError", parsed["unknown_directive"]["type"])
        self.assertEqual("AttributeError", parsed["no_fields"]["type"])

        properties = self.facts("H01")
        before = properties["before_private_field_mutation"]
        after = properties["after_private_field_mutation"]
        self.assertEqual(
            self.sequence(before["default"]),
            self.sequence(after["default"]),
        )
        self.assertEqual(
            self.sequence(before["required_fields"]),
            self.sequence(after["required_fields"]),
        )
        self.assertEqual(
            [
                "name",
                "memo",
                "is_unique",
                "is_required",
                "is_obsolete",
                "min_fields",
                "extensible",
                "begin_extensible",
                "format",
                "reference",
                "idd_index",
                "required_fields",
                "default",
            ],
            properties["property_names"],
        )

    def test_validation_is_fail_closed_against_resealed_tampering(self) -> None:
        changed = self.changed_fixture()
        changed["cases"][0]["python"]["facts"]["shared_default_key_mutation_visible"] = False
        self.reseal(changed)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        symbol = "IddField.name"
        changed["consumer_contract"]["classifications"][symbol] = "exception"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["target_receipts"][0]["symbol"] = "IddField.Drifted"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["deferred_receipts"].pop()
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["out_of_scope_receipts"][0]["inventory_index"] = 1095
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        changed = self.changed_fixture()
        changed["support"]["full_schema_identity"]["object_count"] = 847
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
                generator._validate_safe_tree({"unsafe": value})


if __name__ == "__main__":
    unittest.main()

"""Fail-closed tests for the EPlusSimple HVAC enum/base behavior oracle."""

from __future__ import annotations

from collections import Counter
import copy
import hashlib
import importlib.util
import json
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
    / "generate_epsimple_hvac_enums_base_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-hvac-enums-base-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_hvac_enums_base_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load HVAC enum/base generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 61_458
EXPECTED_GENERATOR_SHA256 = (
    "sha256:eaa5691d29c341844097c8690f0e12970824494f1e00e8287811b7876ba3df0d"
)
EXPECTED_FIXTURE_BYTES = 160_001
EXPECTED_FIXTURE_SHA256 = (
    "sha256:5bf5e8f88a2050232aa45e79c48894a54897eea57cddaf75697ab914d9715b7c"
)


class EPlusSimpleHvacEnumsBaseOracleTests(unittest.TestCase):
    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def facts(value: dict[str, object], code: str) -> dict[str, object]:
        matches = [
            case["python"]["facts"]
            for case in value["cases"]
            if case["code"] == code
        ]
        if len(matches) != 1:
            raise AssertionError(f"Expected one HVAC enum/base case {code}.")
        return matches[0]

    @staticmethod
    def regenerate(output: Path) -> None:
        environment = os.environ.copy()
        environment["PYTHONHASHSEED"] = "0"
        environment["PYTHONDONTWRITEBYTECODE"] = "1"
        subprocess.run(
            [
                sys.executable,
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
        self.assertEqual(6, len(value["cases"]))
        self.assertEqual(6, len(value["fact_sha256"]))
        self.assertEqual(6, len(value["case_sha256"]))
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
                "excluded_receipts",
                "fact_sha256",
                "native_audit",
                "runtime",
                "schema",
                "symbols",
                "target_receipts",
                "upstream",
            },
            set(value),
        )

    def test_two_independent_bootstrap_regenerations_are_byte_identical(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="epsimple-hvac-enums-base-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_and_matrix_form_the_exact_28_116_58_partition(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        targets = [
            185, 186, 187, 188, 189, 190, 191, 192, 193, 194, 195, 196,
            197, 198, 240, 241, 242, 243, 244, 245, 246, 247, 267, 268,
            269, 270, 319, 320,
        ]
        deferred = list(generator.EXPECTED_DEFERRED_INDICES)
        excluded = list(generator.EXPECTED_EXCLUDED_INDICES)
        self.assertEqual(targets, [item["inventory_index"] for item in value["target_receipts"]])
        self.assertEqual(deferred, [item["inventory_index"] for item in value["deferred_receipts"]])
        self.assertEqual(excluded, [item["inventory_index"] for item in value["excluded_receipts"]])
        self.assertEqual(28, len(inventory["target_receipts"]))
        self.assertEqual(116, len(inventory["deferred_receipts"]))
        self.assertEqual(58, len(inventory["excluded_receipts"]))
        self.assertEqual(list(range(135, 337)), sorted(targets + deferred + excluded))
        self.assertEqual(inventory["target_receipts"], value["target_receipts"])
        self.assertEqual(inventory["deferred_receipts"], value["deferred_receipts"])
        self.assertEqual(inventory["excluded_receipts"], value["excluded_receipts"])
        self.assertEqual(
            generator.EXPECTED_TARGET_RECEIPTS_SHA256,
            generator.canonical_sha256(value["target_receipts"]),
        )
        self.assertEqual(
            generator.EXPECTED_DEFERRED_RECEIPTS_SHA256,
            generator.canonical_sha256(value["deferred_receipts"]),
        )
        self.assertEqual(
            generator.EXPECTED_EXCLUDED_RECEIPTS_SHA256,
            generator.canonical_sha256(value["excluded_receipts"]),
        )

        matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        for receipt in value["target_receipts"]:
            index = receipt["inventory_index"]
            symbol = receipt["symbol"]
            self.assertIn(
                matrix["classifications"][index],
                {"needs_reverification", generator.CLASSIFICATIONS[symbol]},
            )
        self.assertEqual(
            ["out_of_scope"] * 58,
            [matrix["classifications"][index] for index in excluded],
        )
        self.assertTrue(
            all(
                matrix["classifications"][index]
                in {"needs_reverification", "equivalent", "exception"}
                for index in deferred
            )
        )

    def test_consumer_contract_routes_classifications_and_claims_are_exact(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"equivalent": 18, "exception": 10}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.EXCEPTION_SYMBOLS, set(contract["adaptations"]))
        closure = contract["closure"]
        self.assertTrue(closure["exact_one_case_target_partition"])
        self.assertTrue(closure["full_source_partition"])
        self.assertEqual(202, closure["source_declaration_count"])
        self.assertEqual(28, closure["target_count"])
        self.assertEqual(116, closure["deferred_count"])
        self.assertEqual(58, closure["excluded_count"])
        evidence = contract["evidence_contract"]
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertFalse(evidence["full_hvac_declaration_parity_claim"])
        self.assertFalse(evidence["native_runtime_executed_by_python_oracle"])
        self.assertTrue(evidence["python_behavior_oracle_only"])
        self.assertTrue(evidence["relocatable_import_claim"])
        for route in contract["native_routes"].values():
            self.assertIn("GonieGonie.SimpleDragon", route)
            self.assertNotIn(".Internal", route)
            self.assertNotIn("GrmVocabulary", route)

    def test_runtime_source_dependency_resource_relocation_and_native_pins_are_exact(self) -> None:
        value = self.fixture()
        runtime = value["runtime"]
        self.assertEqual(generator._runtime_receipt(), runtime)
        self.assertEqual(generator.EXPECTED_DEPENDENCIES, runtime["dependencies"])
        self.assertEqual(
            generator.canonical_sha256(generator.EXPECTED_DEPENDENCIES),
            runtime["dependencies_sha256"],
        )
        upstream = value["upstream"]
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, upstream["source"]["source_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_AST_SHA256, upstream["source"]["ast_sha256"])
        self.assertEqual([], upstream["resource_receipts"])
        self.assertEqual(generator.canonical_sha256([]), upstream["resource_receipts_sha256"])
        isolated = upstream["isolated_import"]
        self.assertEqual(2, isolated["source_location_count"])
        self.assertFalse(isolated["epsimple_package_initializer_executed"])
        self.assertFalse(isolated["epsimple_core_initializer_executed"])
        self.assertEqual(16, len(isolated["loaded_local_modules"]))
        self.assertEqual(
            generator.EXPECTED_LOADED_LOCAL_MODULES_SHA256,
            generator.canonical_sha256(isolated["loaded_local_modules"]),
        )
        self.assertEqual(
            generator.EXPECTED_RELOCATION_SNAPSHOT_SHA256,
            isolated["relocation_snapshot_sha256"],
        )
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(value["consumer_contract"]["runtime_signatures"]),
        )
        native = value["native_audit"]
        self.assertTrue(native["public_production_routes_only"])
        self.assertEqual(list(generator.NATIVE_SOURCE_RECEIPTS), native["source_receipts"])
        self.assertEqual(
            generator.EXPECTED_NATIVE_AUDIT_SHA256,
            generator.canonical_sha256(native),
        )

    def test_compressor_and_cooling_tower_enum_topology_is_exact(self) -> None:
        value = self.fixture()
        compressor = self.facts(value, "C01")
        self.assertEqual(["TURBO", "SCREW", "RECIPROCATING"], compressor["iteration_names"])
        self.assertEqual(["turbo", "screw", "reciprocating"], compressor["iteration_values"])
        self.assertEqual(0, compressor["duplicate_alias_count"])
        self.assertTrue(compressor["is_str_subclass"])
        self.assertTrue(compressor["is_enum_subclass"])
        self.assertEqual("ValueError", compressor["invalid_value"]["type"])
        self.assertEqual("KeyError", compressor["invalid_name"]["type"])
        self.assertEqual("ValueError", compressor["wrong_case_value"]["type"])
        for member in compressor["members"]:
            self.assertTrue(member["value_lookup_is_same"])
            self.assertTrue(member["name_lookup_is_same"])
            self.assertTrue(member["equal_to_raw_string"])
            self.assertEqual(member["value"], member["string"])
            self.assertEqual(member["value"], member["dragon"]["value"])
            self.assertEqual(member["name"], member["dragon"]["name"])
            self.assertTrue(member["dragon_repeat_same_identity"])

        control = self.facts(value, "C02")
        self.assertEqual(["SINGLESPEED", "TWOSPEED"], control["iteration_names"])
        self.assertEqual(["single-speed", "two-speed"], control["iteration_values"])
        self.assertTrue(all(item["string"] == item["value"] for item in control["members"]))
        tower = self.facts(value, "C03")
        self.assertEqual(["CLOSED", "OPEN"], tower["iteration_names"])
        self.assertEqual(["closed", "open"], tower["iteration_values"])
        self.assertTrue(all(item["string"] == item["value"] for item in tower["members"]))

    def test_fuel_lookup_string_and_dragon_conversion_mapping_is_exact(self) -> None:
        fuel = self.facts(self.fixture(), "F01")
        self.assertEqual(
            ["ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING"],
            fuel["iteration_names"],
        )
        self.assertEqual(
            ["electricity", "natural_gas", "lpg", "oil", "district_heating"],
            fuel["iteration_values"],
        )
        self.assertEqual(
            {
                "ELECTRICITY": ("ELECTRICITY", "Electricity"),
                "NATURALGAS": ("NATURALGAS", "NaturalGas"),
                "LPG": ("PROPANE", "Propane"),
                "OIL": ("DIESEL", "Diesel"),
                "DISTRICTHEATING": ("OTHERFUEL1", "OtherFuel1"),
            },
            {
                item["name"]: (item["dragon"]["name"], item["dragon"]["value"])
                for item in fuel["members"]
            },
        )
        self.assertTrue(all(item["is_str_instance"] for item in fuel["members"]))
        self.assertTrue(all(item["name_lookup_is_same"] for item in fuel["members"]))
        self.assertTrue(all(item["value_lookup_is_same"] for item in fuel["members"]))

    def test_none_source_singleton_id_new_and_null_conversion_quirks_are_exact(self) -> None:
        facts = self.facts(self.fixture(), "N01")
        self.assertFalse(facts["first_instance_preexisting"])
        self.assertEqual("$SPECIAL$:SRCE--NONE", facts["class_id"])
        self.assertEqual(facts["class_id"], facts["instance_id"])
        self.assertEqual(["SourceSystem"], facts["base_classes"])
        self.assertTrue(facts["constructor_is_direct_new"])
        self.assertTrue(facts["constructor_arguments_same_identity"])
        self.assertTrue(facts["direct_new_arguments_ignored"])
        self.assertTrue(facts["repeat_constructor_same_identity"])
        self.assertEqual("returned", facts["constructor_arguments"]["outcome"])
        self.assertTrue(facts["instance_is_source_system"])
        self.assertTrue(facts["instance_dictionary_empty"])
        self.assertTrue(facts["mapper_inherited_by_identity"])
        self.assertTrue(facts["to_dragon_is_none"])
        self.assertTrue(facts["to_dragon_repeat_is_none"])

    def test_source_system_empty_base_mapper_topology_and_error_boundaries_are_exact(self) -> None:
        facts = self.facts(self.fixture(), "S01")
        self.assertEqual("()", facts["constructor_signature"])
        self.assertEqual(["TYPE_MAPPER"], facts["declared_public_members"])
        self.assertTrue(facts["fresh_instances_are_distinct"])
        self.assertTrue(facts["fresh_instances_empty"])
        self.assertFalse(facts["has_to_dragon"])
        self.assertEqual("dict", facts["mapper_type"])
        self.assertEqual(
            [
                "heatpump",
                "geothermal_heatpump",
                "chiller",
                "absorption_chiller",
                "boiler",
                "district_heating",
            ],
            facts["mapper_keys"],
        )
        self.assertEqual(
            [
                "HeatPump",
                "GeothermalHeatPump",
                "Chiller",
                "AbsorptionChiller",
                "Boiler",
                "DistrictHeating",
            ],
            [item["type"] for item in facts["mapped_types"]],
        )
        self.assertTrue(all(item["callable"] for item in facts["mapped_types"]))
        self.assertTrue(all(item["is_source_subclass"] for item in facts["mapped_types"]))
        self.assertTrue(all(item["inherited_mapper_by_identity"] for item in facts["mapped_types"]))
        self.assertTrue(facts["none_source_absent_from_values"])
        self.assertTrue(facts["mapper_identity_across_accesses"])
        self.assertTrue(facts["mapper_copy_mutation_preserves_original"])
        self.assertEqual("KeyError", facts["missing_key_error"]["type"])
        self.assertEqual("TypeError", facts["unhashable_key_error"]["type"])
        self.assertEqual("TypeError", facts["positional_constructor_error"]["type"])
        self.assertEqual("TypeError", facts["keyword_constructor_error"]["type"])

    def test_resealed_fact_contract_receipt_native_and_order_tampering_fail_closed(self) -> None:
        original = self.fixture()

        facts = copy.deepcopy(original)
        self.facts(facts, "F01")["iteration_values"][0] = "tampered"
        self.reseal(facts)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(facts)

        classification = copy.deepcopy(original)
        classification["consumer_contract"]["classifications"]["SourceSystem"] = "equivalent"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(classification)

        route = copy.deepcopy(original)
        route["consumer_contract"]["native_routes"]["Fuel"] = "wrong"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(route)

        target = copy.deepcopy(original)
        target["target_receipts"][0]["inventory_index"] = 0
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(target)

        deferred = copy.deepcopy(original)
        deferred["deferred_receipts"][0]["symbol"] = "wrong"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(deferred)

        native = copy.deepcopy(original)
        native["native_audit"]["source_receipts"][0]["bytes"] = 1
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(native)

        loaded = copy.deepcopy(original)
        loaded["upstream"]["isolated_import"]["loaded_local_modules"][0]["module"] = "wrong"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(loaded)

        order = copy.deepcopy(original)
        order["cases"][0], order["cases"][1] = order["cases"][1], order["cases"][0]
        self.reseal(order)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(order)

    def test_strict_json_unsafe_values_duplicate_keys_and_inventory_tampering_fail_closed(self) -> None:
        for unsafe in (
            {"raw": 1.25},
            {"raw": float("nan")},
            {"raw": "object at 0x123456789abcdef0"},
            {"raw": r"C:\unsafe\fixture.json"},
            {"raw": "/tmp/unsafe/fixture.json"},
            {"raw": "2026-08-27T12:34:56"},
            {7: "non-string-key"},
        ):
            with self.subTest(unsafe=unsafe), self.assertRaises(RuntimeError):
                generator._validate_safe_tree(unsafe)

        with self.assertRaises(ValueError):
            generator.load_json_without_duplicates_text(
                '{"schema":"first","schema":"second"}'
            )
        with self.assertRaises(ValueError):
            generator.load_json_without_duplicates_text('{"value":NaN}')

        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="epsimple-hvac-enums-base-tamper-", dir=TEST_TEMP_ROOT
        ) as temporary:
            inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
            inventory["symbols"][185]["symbol_hash"] = "sha256:" + "0" * 64
            tampered = Path(temporary) / "inventory.json"
            tampered.write_text(json.dumps(inventory), encoding="utf-8", newline="\n")
            with self.assertRaises(SystemExit):
                generator.load_exact_inventory(
                    tampered, generator.EXPECTED_UPSTREAM_COMMIT
                )


if __name__ == "__main__":
    unittest.main()

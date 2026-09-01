"""Fail-closed tests for the EPlusSimple HVAC supply-system oracle."""

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
    / "generate_epsimple_hvac_supply_system_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-hvac-supply-system-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_hvac_supply_system_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load HVAC supply-system generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 75_411
EXPECTED_GENERATOR_SHA256 = (
    "sha256:e7874d74d2338c4fa71ab7ddf3cf33b17ce713dcefa0a3d6519cd5a5dd28780d"
)
EXPECTED_FIXTURE_BYTES = 168_146
EXPECTED_FIXTURE_SHA256 = (
    "sha256:b9a98ea739bf4181a4f93c8bed161f559c03bb93a4926ee56dccc100ddd49d65"
)


class EPlusSimpleHvacSupplySystemOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one HVAC supply-system case {code}.")
        return matches[0]

    @staticmethod
    def finite(value: dict[str, str]) -> float:
        if value.get("kind") != "float":
            raise AssertionError(f"Expected canonical float, got {value!r}.")
        result = float.fromhex(value["hex"])
        if repr(result) != value["repr"]:
            raise AssertionError("Canonical float repr drifted.")
        return result

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
                "fact_sha256",
                "native_review",
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
            prefix="epsimple-hvac-supply-system-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_and_matrix_form_exact_52_target_150_adjacent_closure(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_targets = [
            147, 148, 151, 154, 155, 156,
            209, 210, 213, 216, 217, 218,
            219, 220, 223, 226, 227, 228, 229,
            230, 231, 234, 237, 238, 239,
            271, 272, 275, 278, 279, 280, 281, 282,
            296, 297, 300, 303, 304, 305, 306, 307,
            308, 309, 312, 315, 316, 317, 318,
            321, 322, 323, 324,
        ]
        self.assertEqual(expected_targets, list(generator.TARGET_INDICES))
        self.assertEqual(
            expected_targets,
            [item["inventory_index"] for item in value["target_receipts"]],
        )
        self.assertEqual(52, len(inventory["target_receipts"]))
        self.assertEqual(150, len(generator.ADJACENT_INDICES))
        self.assertEqual(
            list(range(135, 337)),
            sorted((*generator.TARGET_INDICES, *generator.ADJACENT_INDICES)),
        )
        self.assertEqual(
            generator.EXPECTED_TARGET_RECEIPTS_SHA256,
            generator.canonical_sha256(value["target_receipts"]),
        )
        self.assertEqual(
            generator.EXPECTED_ADJACENT_RECEIPTS_SHA256,
            value["upstream"]["adjacent_receipts_sha256"],
        )
        closure = value["consumer_contract"]["closure"]
        self.assertTrue(closure["exact_one_case_target_partition"])
        self.assertTrue(closure["full_hvac_source_partition"])
        self.assertEqual(202, closure["source_declaration_count"])
        self.assertEqual(52, closure["target_count"])
        self.assertEqual(150, closure["adjacent_count"])
        self.assertEqual(expected_targets, closure["target_indices"])
        self.assertEqual(list(generator.ADJACENT_INDICES), closure["adjacent_indices"])

        matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        for receipt in value["target_receipts"]:
            index = receipt["inventory_index"]
            symbol = receipt["symbol"]
            self.assertIn(
                matrix["classifications"][index],
                {"needs_reverification", generator.CLASSIFICATIONS[symbol]},
            )

    def test_consumer_contract_routes_classifications_and_native_review_are_exact(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"equivalent": 19, "exception": 33}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.EXCEPTION_SYMBOLS, set(contract["adaptations"]))
        self.assertEqual(52, len(contract["coverage_by_symbol"]))
        self.assertEqual(52, len(set(contract["assertion_ids"].values())))
        for symbol, expectation in contract["expectations"].items():
            self.assertEqual(generator.CLASSIFICATIONS[symbol], expectation["classification"])
            self.assertEqual(generator.NATIVE_ROUTES[symbol], expectation["native_route"])
            self.assertIn("Dragons.SimpleDragon", expectation["native_route"])
            expected_adaptation = generator.ADAPTATIONS.get(symbol, "not_applicable")
            self.assertEqual(expected_adaptation, expectation["adaptation"])
        evidence = contract["evidence_contract"]
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertTrue(evidence["exact_cpython_behavior_oracle"])
        self.assertEqual(52, evidence["expected_receipt_count"])
        self.assertFalse(evidence["native_runtime_executed_by_python_oracle"])
        self.assertTrue(evidence["path_independent_relocated_import"])
        self.assertTrue(evidence["target_coverage_complete"])
        review = value["native_review"]
        self.assertTrue(review["public_production_routes_only"])
        self.assertFalse(review["python_executes_native_runtime"])
        self.assertEqual(4, len(review["source_receipts"]))
        self.assertEqual(
            generator.EXPECTED_NATIVE_REVIEW_SHA256,
            generator.canonical_sha256(review),
        )

    def test_air_and_fan_coil_source_capability_json_validation_and_dragon_are_exact(self) -> None:
        value = self.fixture()
        air = self.facts(value, "A01")
        self.assertEqual(["SupplySystem"], air["base_classes"])
        self.assertEqual(
            "GeothermalHeatPump",
            air["explicit_after_source_mutation"]["source"]["class"],
        )
        self.assertTrue(
            air["explicit_after_source_mutation"]["values"]["heatable"]["value"]
        )
        self.assertTrue(
            air["explicit_after_source_mutation"]["values"]["coolable"]["value"]
        )
        self.assertEqual("SUP-AHU-JSON", air["from_json"]["values"]["ID"])
        self.assertEqual("AirHandlingUnit", air["dragon_explicit"]["class"])
        self.assertTrue(air["dragon_repeat_fresh"])
        self.assertEqual(
            {"KeyError", "TypeError"},
            {item["type"] for item in air["errors"].values()},
        )
        self.assertTrue(
            all(item["outcome"] == "raised" for item in air["errors"].values())
        )

        fan = self.facts(value, "F01")
        self.assertEqual(["AirHandlingUnit"], fan["base_classes"])
        self.assertEqual(
            ["boiler", "district", "chiller", "absorption"],
            [item["branch"] for item in fan["branches"]],
        )
        self.assertEqual(
            [(True, False), (True, False), (False, True), (False, True)],
            [
                (
                    item["python"]["values"]["heatable"]["value"],
                    item["python"]["values"]["coolable"]["value"],
                )
                for item in fan["branches"]
            ],
        )
        self.assertTrue(
            all(item["dragon"]["class"] == "FanCoilUnit" for item in fan["branches"])
        )
        self.assertEqual("SUP-FCU-JSON", fan["from_json"]["values"]["ID"])
        self.assertEqual(
            {"KeyError", "TypeError"},
            {item["type"] for item in fan["errors"].values()},
        )

    def test_electric_supply_defaults_capacity_null_source_and_dragon_are_exact(self) -> None:
        value = self.fixture()
        floor = self.facts(value, "E01")
        self.assertEqual(["SupplySystem"], floor["base_classes"])
        self.assertEqual("NoneSource", floor["explicit"]["source"]["class"])
        self.assertTrue(floor["none_source_singleton_identity"])
        self.assertTrue(floor["explicit"]["values"]["heatable"]["value"])
        self.assertFalse(floor["explicit"]["values"]["coolable"]["value"])
        self.assertEqual("ElectricRadiantFloor", floor["dragon"]["class"])
        self.assertEqual("NoneType", floor["dragon"]["source"]["class"])
        self.assertTrue(floor["dragon_repeat_fresh"])
        self.assertEqual(
            {"AttributeError", "TypeError"},
            {item["type"] for item in floor["errors"].values()},
        )

        radiator = self.facts(value, "E02")
        self.assertIsNone(radiator["default"]["values"]["capacity"])
        self.assertEqual(
            "3000", radiator["explicit_mutated"]["values"]["capacity"]["value"]
        )
        self.assertEqual(
            3100.0,
            self.finite(
                radiator["from_json_explicit"]["values"]["capacity"]
            ),
        )
        self.assertEqual("NoneSource", radiator["explicit_mutated"]["source"]["class"])
        self.assertEqual("NoneType", radiator["dragon_explicit"]["source"]["class"])
        self.assertTrue(radiator["none_source_singleton_identity"])
        self.assertEqual(
            {"TypeError", "ValueError"},
            {item["type"] for item in radiator["errors"].values()},
        )
        self.assertTrue(
            all(
                item["outcome"] == "raised"
                for item in radiator["errors"].values()
            )
        )

    def test_packaged_defaults_validation_json_dedicated_source_and_dragon_are_exact(self) -> None:
        packaged = self.facts(self.fixture(), "P01")
        self.assertEqual(["SupplySystem"], packaged["base_classes"])
        self.assertEqual(3.0, self.finite(packaged["default"]["values"]["cop"]))
        self.assertIsNone(packaged["default"]["values"]["capacity"])
        self.assertTrue(packaged["none_source_singleton_identity"])
        self.assertFalse(packaged["explicit_mutated"]["values"]["heatable"]["value"])
        self.assertTrue(packaged["explicit_mutated"]["values"]["coolable"]["value"])
        self.assertEqual("PackagedAirConditioner", packaged["dragon_first"]["class"])
        self.assertEqual("HeatPump", packaged["dedicated_source"]["class"])
        self.assertEqual(
            "1", packaged["source_dict_count_after_first"]["value"]
        )
        self.assertEqual(
            "2", packaged["source_dict_count_after_second"]["value"]
        )
        self.assertTrue(packaged["source_dict_values_distinct"])
        self.assertEqual(
            {"TypeError", "ValueError"},
            {item["type"] for item in packaged["errors"].values()},
        )
        self.assertTrue(
            all(
                item["outcome"] == "raised"
                for item in packaged["errors"].values()
            )
        )

    def test_hydronic_floor_radiator_and_base_mapper_semantics_are_exact(self) -> None:
        value = self.fixture()
        floor = self.facts(value, "R01")
        self.assertTrue(floor["boiler"]["values"]["heatable"]["value"])
        self.assertFalse(floor["boiler"]["values"]["coolable"]["value"])
        self.assertEqual("Boiler", floor["boiler"]["source"]["class"])
        self.assertEqual("DistrictHeating", floor["district"]["source"]["class"])
        self.assertEqual("RadiantFloor", floor["dragon_boiler"]["class"])
        self.assertTrue(
            all(item["outcome"] == "raised" for item in floor["errors"].values())
        )

        radiator = self.facts(value, "R02")
        self.assertIsNone(radiator["default"]["values"]["capacity"])
        self.assertEqual(
            "6000", radiator["explicit_mutated"]["values"]["capacity"]["value"]
        )
        self.assertEqual(
            6250.0,
            self.finite(
                radiator["from_json_explicit"]["values"]["capacity"]
            ),
        )
        self.assertEqual("Radiator", radiator["dragon_default"]["class"])
        self.assertEqual(
            {"KeyError", "TypeError", "ValueError"},
            {item["type"] for item in radiator["errors"].values()},
        )

        base = self.facts(value, "S01")
        self.assertTrue(base["base_instance_dictionary_empty"])
        self.assertEqual(
            {"coolable": "property", "heatable": "property"},
            base["property_descriptors"],
        )
        self.assertEqual(
            [
                "packaged_air_conditioner",
                "air_handling_unit",
                "fan_coil_unit",
                "radiator",
                "electric_radiator",
                "radiant_floor",
                "electric_radiant_floor",
            ],
            base["mapper_keys"],
        )
        self.assertTrue(base["mapper_identity_across_accesses"])
        self.assertTrue(base["mapper_copy_mutation_preserves_original"])
        self.assertEqual(
            {"coolable": False, "heatable": False},
            base["probe_exact_type_behavior"]["geothermal_subclass"],
        )
        self.assertEqual(
            {"coolable": False, "heatable": True},
            base["probe_exact_type_behavior"]["heatpump"],
        )
        self.assertEqual(
            {"coolable": True, "heatable": False},
            base["probe_exact_type_behavior"]["chiller"],
        )
        self.assertEqual(
            {"AttributeError"},
            {
                item["type"]
                for item in base["base_property_errors"].values()
            },
        )


    def test_runtime_source_relocation_and_support_pins_are_exact(self) -> None:
        value = self.fixture()
        runtime = value["runtime"]
        self.assertEqual("cpython", runtime["implementation"])
        self.assertEqual("win32", runtime["platform"])
        self.assertEqual("3.12.7", runtime["python_version"])
        self.assertEqual(0, runtime["python_hash_seed"])
        self.assertEqual(
            generator.EXPECTED_BASE_SHA256,
            runtime["supply_system_support"]["sha256"],
        )
        upstream = value["upstream"]
        self.assertEqual(generator.EXPECTED_UPSTREAM_COMMIT, upstream["commit"])
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, upstream["source"]["source_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_AST_SHA256, upstream["source"]["ast_sha256"])
        isolated = upstream["isolated_import"]
        self.assertFalse(isolated["epsimple_package_initializer_executed"])
        self.assertFalse(isolated["epsimple_core_initializer_executed"])
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
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(
                value["consumer_contract"]["runtime_signatures"]
            ),
        )

    def test_tampering_wrong_commit_duplicate_keys_and_non_target_promotion_fail_closed(self) -> None:
        value = self.fixture()
        tampered = copy.deepcopy(value)
        tampered["target_receipts"][0]["inventory_index"] = 137
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(tampered)

        tampered = copy.deepcopy(value)
        tampered["cases"][0]["python"]["facts"]["from_json"]["values"]["ID"] = "DRIFT"
        self.reseal(tampered)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(tampered)

        tampered = copy.deepcopy(value)
        tampered["consumer_contract"]["closure"]["target_indices"].append(137)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(tampered)

        with self.assertRaises(ValueError):
            generator.load_json_without_duplicates_text('{"a":1,"a":2}')
        with self.assertRaises(SystemExit):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

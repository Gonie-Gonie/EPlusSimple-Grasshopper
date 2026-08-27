"""Fail-closed tests for the EPlusSimple HVAC thermal-source oracle."""

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
    / "generate_epsimple_hvac_thermal_source_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-hvac-thermal-source-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_hvac_thermal_source_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load HVAC thermal-source generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 63_818
EXPECTED_GENERATOR_SHA256 = (
    "sha256:e930c9242c76b48500010e76f625e41baa07de96e4629b447df61db6c571e51c"
)
EXPECTED_FIXTURE_BYTES = 135_657
EXPECTED_FIXTURE_SHA256 = (
    "sha256:e78e8bcbe42cd236775db63d50088bad82a9e9c5328e5fa5de6873d069984391"
)


class EPlusSimpleHvacThermalSourceOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one HVAC thermal-source case {code}.")
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
            prefix="epsimple-hvac-thermal-source-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_and_matrix_form_exact_47_target_155_adjacent_closure(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_targets = [
            135, 136, 139, 142, 143, 144, 145, 146,
            157, 158, 161, 164, 165, 166, 167, 168, 169,
            170, 171, 174, 177, 178, 179, 180, 181, 182, 183, 184,
            199, 200, 203, 206, 207, 208,
            248, 251, 252,
            253, 254, 257, 260, 261, 262, 263, 264, 265, 266,
        ]
        self.assertEqual(expected_targets, list(generator.TARGET_INDICES))
        self.assertEqual(
            expected_targets,
            [item["inventory_index"] for item in value["target_receipts"]],
        )
        self.assertEqual(47, len(inventory["target_receipts"]))
        self.assertEqual(155, len(generator.ADJACENT_INDICES))
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
        self.assertEqual(47, closure["target_count"])
        self.assertEqual(155, closure["adjacent_count"])
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
            Counter({"equivalent": 24, "exception": 23}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.EXCEPTION_SYMBOLS, set(contract["adaptations"]))
        self.assertEqual(47, len(contract["coverage_by_symbol"]))
        self.assertEqual(47, len(set(contract["assertion_ids"].values())))
        for symbol, expectation in contract["expectations"].items():
            self.assertEqual(generator.CLASSIFICATIONS[symbol], expectation["classification"])
            self.assertEqual(generator.NATIVE_ROUTES[symbol], expectation["native_route"])
            self.assertIn("GonieGonie.SimpleDragon", expectation["native_route"])
            expected_adaptation = generator.ADAPTATIONS.get(symbol, "not_applicable")
            self.assertEqual(expected_adaptation, expectation["adaptation"])
        evidence = contract["evidence_contract"]
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertTrue(evidence["exact_cpython_behavior_oracle"])
        self.assertEqual(47, evidence["expected_receipt_count"])
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

    def test_absorption_and_boiler_state_defaults_json_validation_and_dragon_are_exact(self) -> None:
        value = self.fixture()
        absorption = self.facts(value, "A01")
        self.assertEqual(["SourceSystem"], absorption["base_classes"])
        self.assertEqual("SRC-ABS-DEFAULT", absorption["default"]["values"]["ID"])
        self.assertEqual(0.9, self.finite(absorption["default"]["values"]["cop"]))
        self.assertEqual(0.85, self.finite(absorption["default"]["values"]["boiler_efficiency"]))
        self.assertIsNone(absorption["default"]["values"]["capacity"])
        self.assertEqual("AbsorptionChiller", absorption["dragon"]["type"])
        self.assertTrue(absorption["dragon_repeat_fresh"])
        self.assertEqual(
            {"ValueError"},
            {item["type"] for item in absorption["errors"].values()},
        )

        boiler = self.facts(value, "B01")
        self.assertEqual("natural_gas", boiler["default"]["values"]["fuel"])
        self.assertFalse(boiler["default"]["values"]["hotwater_supply"]["value"])
        self.assertEqual(0.85, self.finite(boiler["default"]["values"]["efficiency"]))
        self.assertEqual("district_heating", boiler["mutated"]["values"]["fuel"])
        self.assertEqual("Boiler", boiler["dragon"]["type"])
        self.assertEqual("SRC-BOILER-JSON", boiler["from_json"]["values"]["ID"])
        self.assertTrue(boiler["dragon_repeat_fresh"])
        self.assertTrue(all(item["outcome"] == "raised" for item in boiler["errors"].values()))

    def test_chiller_all_four_tower_branches_and_boundaries_are_exact(self) -> None:
        chiller = self.facts(self.fixture(), "C01")
        self.assertEqual(3.0, self.finite(chiller["default"]["values"]["cop"]))
        self.assertIsNone(chiller["default"]["values"]["capacity"])
        self.assertEqual("closed", chiller["mutated"]["values"]["coolingtower_type"])
        self.assertEqual("two-speed", chiller["mutated"]["values"]["coolingtower_control"])
        branches = chiller["tower_branches"]
        self.assertEqual(
            [
                ("open", "single-speed", "OpenSingleSpeedCoolingTower"),
                ("open", "two-speed", "OpenTwoSpeedCoolingTower"),
                ("closed", "single-speed", "ClosedSingleSpeedCoolingTower"),
                ("closed", "two-speed", "ClosedTwoSpeedCoolingTower"),
            ],
            [
                (
                    item["tower_type"],
                    item["control"],
                    item["dragon"]["attributes"]["coolingtower"]["type"],
                )
                for item in branches
            ],
        )
        self.assertTrue(all(item["dragon"]["type"] == "Chiller" for item in branches))
        self.assertTrue(all(item["outcome"] == "raised" for item in chiller["errors"].values()))

    def test_district_geothermal_and_heatpump_semantics_are_exact(self) -> None:
        value = self.fixture()
        district = self.facts(value, "D01")
        self.assertTrue(district["false_then_mutated"]["values"]["hotwater_supply"]["value"])
        self.assertEqual("Boiler", district["dragon"]["type"])
        self.assertEqual("OtherFuel1", district["dragon"]["attributes"]["fuel"])
        self.assertEqual(1.0, self.finite(district["dragon"]["attributes"]["efficiency"]))
        self.assertTrue(district["dragon_repeat_fresh"])
        self.assertTrue(all(item["outcome"] == "raised" for item in district["errors"].values()))

        geothermal = self.facts(value, "G01")
        self.assertEqual(["HeatPump"], geothermal["base_classes"])
        self.assertTrue(geothermal["is_heatpump"])
        self.assertEqual("GeothermalHeatPump", geothermal["explicit"]["class"])
        self.assertEqual("HeatPump", geothermal["dragon_type"])
        self.assertTrue(geothermal["dragon_repeat_fresh"])

        heatpump = self.facts(value, "H01")
        self.assertEqual(3.0, self.finite(heatpump["default"]["values"]["heating_cop"]))
        self.assertEqual(3.0, self.finite(heatpump["default"]["values"]["cooling_cop"]))
        self.assertIsNone(heatpump["default"]["values"]["heating_capacity"])
        self.assertIsNone(heatpump["default"]["values"]["cooling_capacity"])
        self.assertEqual("oil", heatpump["mutated"]["values"]["fuel"])
        self.assertEqual("HeatPump", heatpump["dragon"]["type"])
        self.assertTrue(heatpump["dragon_repeat_fresh"])
        self.assertTrue(all(item["outcome"] == "raised" for item in heatpump["errors"].values()))

    def test_runtime_source_relocation_and_support_pins_are_exact(self) -> None:
        value = self.fixture()
        runtime = value["runtime"]
        self.assertEqual("cpython", runtime["implementation"])
        self.assertEqual("win32", runtime["platform"])
        self.assertEqual("3.12.7", runtime["python_version"])
        self.assertEqual(0, runtime["python_hash_seed"])
        self.assertEqual(
            generator.EXPECTED_BASE_SHA256,
            runtime["thermal_source_support"]["sha256"],
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
        tampered["cases"][0]["python"]["facts"]["default"]["values"]["ID"] = "DRIFT"
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

"""Fail-closed tests for the EPlusSimple HVAC other-systems oracle."""

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
    / "generate_epsimple_hvac_other_systems_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-hvac-other-systems-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_hvac_other_systems_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load HVAC other-systems generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 53_619
EXPECTED_GENERATOR_SHA256 = (
    "sha256:febce413e0c12adc4e75441a61de37f7a1f04744dd3cb1b7e71c4325a5c1e02b"
)
EXPECTED_FIXTURE_BYTES = 72_791
EXPECTED_FIXTURE_SHA256 = (
    "sha256:baab4b84afb2f387267fa49e4b7907f0d74b3a49076d5a0e7562d421a8c5cedc"
)


class EPlusSimpleHvacOtherSystemsOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one HVAC other-systems case {code}.")
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
    def integer(value: dict[str, str]) -> int:
        if value.get("kind") != "int":
            raise AssertionError(f"Expected canonical int, got {value!r}.")
        return int(value["value"])

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
        self.assertEqual(2, len(value["cases"]))
        self.assertEqual(2, len(value["fact_sha256"]))
        self.assertEqual(2, len(value["case_sha256"]))
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
            prefix="epsimple-hvac-other-systems-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_and_matrix_form_exact_17_target_185_adjacent_closure(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_targets = [
            283, 284, 287, 290, 291, 292, 293, 294, 295,
            325, 326, 329, 332, 333, 334, 335, 336,
        ]
        self.assertEqual(expected_targets, list(generator.TARGET_INDICES))
        self.assertEqual(
            expected_targets,
            [item["inventory_index"] for item in value["target_receipts"]],
        )
        self.assertEqual(17, len(inventory["target_receipts"]))
        self.assertEqual(185, len(generator.ADJACENT_INDICES))
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
        self.assertEqual(17, closure["target_count"])
        self.assertEqual(185, closure["adjacent_count"])
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

    def test_contract_routes_classifications_and_native_review_are_exact(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"equivalent": 9, "exception": 8}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.EXCEPTION_SYMBOLS, set(contract["adaptations"]))
        self.assertEqual(17, len(contract["coverage_by_symbol"]))
        self.assertEqual(17, len(set(contract["assertion_ids"].values())))
        for symbol, expectation in contract["expectations"].items():
            self.assertEqual(generator.CLASSIFICATIONS[symbol], expectation["classification"])
            self.assertEqual(generator.NATIVE_ROUTES[symbol], expectation["native_route"])
            self.assertIn("Dragons.SimpleDragon", expectation["native_route"])
            self.assertEqual(
                generator.ADAPTATIONS.get(symbol, "not_applicable"),
                expectation["adaptation"],
            )
        evidence = contract["evidence_contract"]
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertFalse(evidence["adjacent_behavior_promoted"])
        self.assertTrue(evidence["exact_cpython_behavior_oracle"])
        self.assertEqual(17, evidence["expected_receipt_count"])
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
        semantics = review["reviewed_semantics"]
        self.assertTrue(all(semantics.values()))

    def test_photovoltaic_state_json_dragon_and_boundaries_are_exact(self) -> None:
        photovoltaic = self.facts(self.fixture(), "P01")
        self.assertEqual(["object"], photovoltaic["base_classes"])
        self.assertFalse(photovoltaic["adjacent_behavior_executed"])
        self.assertEqual(
            {
                "distinct_live_instances": True,
                "first_matches_process_identity_pattern": True,
                "prefix": "PVPN-",
                "second_matches_process_identity_pattern": True,
            },
            photovoltaic["auto_id"],
        )
        explicit = photovoltaic["explicit"]["values"]
        self.assertEqual("PV-EXPLICIT", explicit["ID"])
        self.assertEqual("Roof PV", explicit["name"])
        self.assertEqual(24.0, self.finite(explicit["area"]))
        self.assertEqual(0.2, self.finite(explicit["efficiency"]))
        self.assertEqual(180.0, self.finite(explicit["azimuth"]))
        self.assertEqual(30.0, self.finite(explicit["tilt"]))
        self.assertEqual("PV-JSON", photovoltaic["from_json"]["values"]["ID"])
        self.assertEqual("Mutated PV", photovoltaic["mutated"]["values"]["name"])
        self.assertEqual(1.0, self.finite(photovoltaic["mutated"]["values"]["efficiency"]))
        self.assertEqual(90, self.integer(photovoltaic["mutated"]["values"]["tilt"]))
        self.assertEqual("PhotoVoltaicPanel", photovoltaic["dragon"]["type"])
        self.assertEqual("PV-EXPLICIT", photovoltaic["dragon"]["attributes"]["name"])
        self.assertTrue(photovoltaic["dragon_repeat_fresh"])
        self.assertEqual(
            {"AttributeError", "TypeError", "ValueError"},
            {item["type"] for item in photovoltaic["errors"].values()},
        )
        self.assertTrue(all(item["outcome"] == "raised" for item in photovoltaic["errors"].values()))
        specials = photovoltaic["accepted_specials"]
        self.assertEqual({"kind": "bool", "value": True}, specials["area_bool"]["values"]["area"])
        self.assertEqual(
            {"kind": "float-nonfinite", "value": "positive-infinity"},
            specials["area_infinity"]["values"]["area"],
        )
        for key, field in (
            ("area_nan", "area"),
            ("azimuth_nan", "azimuth"),
            ("efficiency_nan", "efficiency"),
            ("tilt_nan", "tilt"),
        ):
            self.assertEqual(
                {"kind": "float-nonfinite", "value": "nan"},
                specials[key]["values"][field],
            )
        self.assertEqual("", specials["blank_name"]["values"]["name"])
        boundaries = photovoltaic["accepted_boundaries"]
        self.assertEqual(float.fromhex("0x0.0000000000001p-1022"), self.finite(boundaries["area_nextafter_zero"]))
        self.assertEqual(1, self.integer(boundaries["efficiency_one"]))
        self.assertEqual(90, self.integer(boundaries["tilt_ninety"]))

    def test_ventilation_defaults_state_json_dragon_and_boundaries_are_exact(self) -> None:
        ventilation = self.facts(self.fixture(), "V01")
        self.assertEqual(["object"], ventilation["base_classes"])
        self.assertFalse(ventilation["adjacent_behavior_executed"])
        self.assertEqual("ERVT-", ventilation["auto_id"]["prefix"])
        self.assertTrue(ventilation["auto_id"]["distinct_live_instances"])
        self.assertTrue(ventilation["auto_id"]["first_matches_process_identity_pattern"])
        default = ventilation["default"]["values"]
        self.assertEqual("ERV-DEFAULT", default["ID"])
        self.assertEqual(0.5, self.finite(default["airflow_rate"]))
        self.assertEqual(0.7, self.finite(default["heating_efficiency"]))
        self.assertEqual(0.45, self.finite(default["cooling_efficiency"]))
        from_json_default = ventilation["from_json_default"]["values"]
        self.assertEqual(0.7, self.finite(from_json_default["heating_efficiency"]))
        self.assertEqual(0.45, self.finite(from_json_default["cooling_efficiency"]))
        self.assertEqual("ERV-JSON-EXPLICIT", ventilation["from_json_explicit"]["values"]["ID"])
        self.assertEqual("Mutated ERV", ventilation["mutated"]["values"]["name"])
        self.assertEqual("EnergyRecoveryVentilator", ventilation["dragon"]["type"])
        self.assertEqual("ERV-EXPLICIT", ventilation["dragon"]["attributes"]["name"])
        self.assertNotIn("airflow_rate", ventilation["dragon"]["attributes"])
        self.assertTrue(ventilation["dragon_repeat_fresh"])
        self.assertEqual(
            {"AttributeError", "TypeError", "ValueError"},
            {item["type"] for item in ventilation["errors"].values()},
        )
        self.assertTrue(all(item["outcome"] == "raised" for item in ventilation["errors"].values()))
        specials = ventilation["accepted_specials"]
        self.assertEqual({"kind": "bool", "value": True}, specials["airflow_bool"]["values"]["airflow_rate"])
        self.assertEqual(
            {"kind": "float-nonfinite", "value": "positive-infinity"},
            specials["airflow_infinity"]["values"]["airflow_rate"],
        )
        for key, field in (
            ("airflow_nan", "airflow_rate"),
            ("cooling_nan", "cooling_efficiency"),
            ("heating_nan", "heating_efficiency"),
        ):
            self.assertEqual(
                {"kind": "float-nonfinite", "value": "nan"},
                specials[key]["values"][field],
            )
        self.assertEqual("", specials["blank_name"]["values"]["name"])
        boundaries = ventilation["accepted_boundaries"]
        self.assertEqual(float.fromhex("0x0.0000000000001p-1022"), self.finite(boundaries["airflow_nextafter_zero"]))
        self.assertEqual(math.nextafter(1.0, -math.inf), self.finite(boundaries["heating_nextafter_one"]))

    def test_runtime_source_relocation_and_support_pins_are_exact(self) -> None:
        value = self.fixture()
        runtime = value["runtime"]
        self.assertEqual("cpython", runtime["implementation"])
        self.assertEqual("win32", runtime["platform"])
        self.assertEqual("3.12.7", runtime["python_version"])
        self.assertEqual(0, runtime["python_hash_seed"])
        self.assertEqual(
            generator.EXPECTED_BASE_SHA256,
            runtime["other_systems_support"]["sha256"],
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
            generator.canonical_sha256(value["consumer_contract"]["runtime_signatures"]),
        )

    def test_tampering_wrong_commit_duplicate_keys_and_adjacent_promotion_fail_closed(self) -> None:
        value = self.fixture()
        tampered = copy.deepcopy(value)
        tampered["target_receipts"][0]["inventory_index"] = 285
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(tampered)

        tampered = copy.deepcopy(value)
        tampered["cases"][0]["python"]["facts"]["accepted_specials"]["blank_name"]["values"]["name"] = "drift"
        self.reseal(tampered)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(tampered)

        tampered = copy.deepcopy(value)
        tampered["consumer_contract"]["closure"]["target_indices"].append(285)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(tampered)

        tampered = copy.deepcopy(value)
        tampered["cases"][1]["python"]["facts"]["adjacent_behavior_executed"] = True
        self.reseal(tampered)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(tampered)

        with self.assertRaises(ValueError):
            generator.load_json_without_duplicates_text('{"a":1,"a":2}')
        with self.assertRaises(SystemExit):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

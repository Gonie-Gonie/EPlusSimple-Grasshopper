"""Fail-closed tests for the dragon HVAC source/tower core oracle."""

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
    / "generate_dragon_hvac_source_tower_core_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-hvac-source-tower-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_dragon_hvac_source_tower_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load source/tower generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 68_752
EXPECTED_GENERATOR_SHA256 = (
    "sha256:e9c78f72ae62dc65f229c9766322fb53062b0f8e037bd1b62b5ac5050d8ce2d5"
)
EXPECTED_FIXTURE_BYTES = 172_950
EXPECTED_FIXTURE_SHA256 = (
    "sha256:60e0a2353620437049bba8420a0154e638fe86e5c915b4231793e397bb5c4fc5"
)


class DragonHvacSourceTowerCoreOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one source/tower case {code}.")
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
        self.assertEqual(10, len(value["cases"]))
        self.assertEqual(10, len(value["fact_sha256"]))
        self.assertEqual(10, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )
        self.assertEqual(
            {
                "adjacent_receipts",
                "case_sha256",
                "cases",
                "cases_sha256",
                "consumer_contract",
                "fact_sha256",
                "native_review",
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
            prefix="dragon-hvac-source-tower-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_matrix_and_74_declaration_family_closure_are_exact(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_targets = [
            *range(641, 644),
            *range(652, 655),
            *range(657, 660),
            *range(661, 663),
            *range(664, 666),
            *range(667, 671),
            *range(673, 684),
            *range(726, 737),
            *range(738, 743),
            *range(744, 746),
            *range(747, 749),
            *range(777, 788),
        ]
        expected_adjacent = [
            644, 655, 656, 660, 663, 666, 671, 672,
            684, 685, 737, 743, 746, 749, 788,
        ]
        self.assertEqual(expected_targets, list(generator.TARGET_INDICES))
        self.assertEqual(expected_adjacent, list(generator.ADJACENT_INDICES))
        self.assertEqual(59, len(inventory["target_receipts"]))
        self.assertEqual(15, len(inventory["adjacent_receipts"]))
        self.assertEqual(74, len(generator.FAMILY_INDICES))
        self.assertEqual(100, len(generator.DEFERRED_INDICES))
        self.assertFalse(set(generator.TARGET_INDICES) & set(generator.ADJACENT_INDICES))
        self.assertEqual(
            list(range(641, 815)),
            sorted(
                (
                    *generator.TARGET_INDICES,
                    *generator.ADJACENT_INDICES,
                    *generator.DEFERRED_INDICES,
                )
            ),
        )
        self.assertEqual(
            generator.EXPECTED_TARGET_RECEIPTS_SHA256,
            generator.canonical_sha256(value["target_receipts"]),
        )
        self.assertEqual(
            generator.EXPECTED_ADJACENT_RECEIPTS_SHA256,
            generator.canonical_sha256(value["adjacent_receipts"]),
        )

        closure = value["consumer_contract"]["closure"]
        self.assertTrue(closure["exact_one_case_target_partition"])
        self.assertTrue(closure["full_source_tower_family_closure"])
        self.assertTrue(closure["full_hvac_source_partition"])
        self.assertEqual(174, closure["source_declaration_count"])
        self.assertEqual(74, closure["source_tower_family_count"])
        self.assertEqual(59, closure["target_count"])
        self.assertEqual(15, closure["adjacent_count"])
        self.assertEqual(100, closure["deferred_count"])

        matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        for receipt in value["target_receipts"]:
            classification = matrix["classifications"][receipt["inventory_index"]]
            self.assertIn(
                classification,
                {"needs_reverification", generator.CLASSIFICATIONS[receipt["symbol"]]},
            )
        for index, symbol, expected in generator.ADJACENT_IDENTITIES:
            self.assertEqual(expected, matrix["classifications"][index], symbol)

    def test_consumer_contract_is_exact_27_equivalent_32_exception(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"equivalent": 27, "exception": 32}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.EXCEPTION_SYMBOLS, set(contract["adaptations"]))
        self.assertEqual(59, len(contract["assertion_ids"]))
        self.assertEqual(59, len(set(contract["assertion_ids"].values())))
        self.assertEqual(59, len(contract["coverage_by_symbol"]))
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
            self.assertEqual(generator.CLASSIFICATIONS[symbol], expectation["classification"])
            self.assertEqual(generator.NATIVE_ROUTES[symbol], expectation["native_route"])
            self.assertIn("GonieGonie.InvisibleDragon", expectation["native_route"])
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
        self.assertTrue(evidence["resolved_idf_behavior_reused_from_support"])

    def test_support_generator_fixture_and_resolved_adjacency_are_hash_pinned(self) -> None:
        support = self.fixture()["support"]
        self.assertEqual(generator._support_receipt(), support)
        self.assertEqual(20, support["case_count"])
        self.assertEqual(generator.SUPPORT.SCHEMA, support["schema"])
        self.assertEqual(
            generator.EXPECTED_SUPPORT_CASES_SHA256,
            support["cases_sha256"],
        )
        expected_resolved = [
            symbol
            for _, symbol, classification in generator.ADJACENT_IDENTITIES
            if classification == "exception"
        ]
        self.assertEqual(expected_resolved, support["resolved_adjacent_symbols"])
        self.assertEqual(13, len(expected_resolved))
        self.assertEqual(
            ["CompressorType.__str__", "Fuel.__str__"],
            [
                symbol
                for _, symbol, classification in generator.ADJACENT_IDENTITIES
                if classification == "out_of_scope"
            ],
        )

    def test_runtime_dependency_relocation_and_native_source_receipts_are_exact(self) -> None:
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
        self.assertEqual(59, len(signatures))
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
        self.assertEqual(12, len(isolated["loaded_local_modules"]))
        review = value["native_review"]
        self.assertTrue(review["public_production_routes_only"])
        self.assertFalse(review["python_executes_native_runtime"])
        self.assertEqual(5, len(review["source_receipts"]))
        self.assertEqual(
            generator.EXPECTED_NATIVE_REVIEW_SHA256,
            generator.canonical_sha256(review),
        )

    def test_fuel_and_compressor_enum_order_lookup_and_errors_are_exact(self) -> None:
        compressor = self.facts("D01")
        self.assertEqual(
            [("TURBO", "turbo"), ("SCREW", "screw"), ("RECIPROCATING", "reciprocating")],
            [(item["name"], item["value"]) for item in compressor["members"]],
        )
        self.assertTrue(all(compressor["equality_to_declared_value"]))
        self.assertTrue(all(item["lookup_by_name_identity"] for item in compressor["members"]))
        self.assertEqual("ValueError", compressor["invalid_value"]["type"])
        self.assertEqual("KeyError", compressor["invalid_name"]["type"])

        fuel = self.facts("G01")
        self.assertEqual(
            [
                "ELECTRICITY", "NATURALGAS", "PROPANE", "FUELOILNO1",
                "FUELOILNO2", "COAL", "DIESEL", "GASOLINE", "OTHERFUEL1",
                "OTHERFUEL2",
            ],
            [item["name"] for item in fuel["members"]],
        )
        self.assertEqual(
            [
                "Electricity", "NaturalGas", "Propane", "FuelOilNo1",
                "FuelOilNo2", "Coal", "Diesel", "Gasoline", "OtherFuel1",
                "OtherFuel2",
            ],
            [item["value"] for item in fuel["members"]],
        )
        self.assertTrue(all(item["is_str_instance"] for item in fuel["members"]))
        self.assertTrue(fuel["unique_values"])

    def test_source_and_cooling_tower_abstract_name_topology_is_exact(self) -> None:
        source = self.facts("J01")
        self.assertTrue(source["class_shape"]["abstract"])
        self.assertEqual(
            ["idf_objtypename", "to_idf_object"],
            source["class_shape"]["abstract_methods"],
        )
        self.assertEqual({"kind": "none"}, source["abstract_idf_object_type_body"])
        self.assertEqual({"kind": "none"}, source["abstract_to_idf_body"])
        before = source["names_before_mutation"]
        self.assertEqual("ProbeSource_named_Source Name", before["object"])
        self.assertEqual("Loop_for_Source Name", before["loop"])
        self.assertEqual(
            "Terminal_Units_for_ProbeSource_named_Source Name",
            before["terminal_unit_list"],
        )
        self.assertEqual("Loop_for_Renamed Source", source["names_after_mutation"]["loop"])
        self.assertTrue(source["fresh_probe_results"])

        tower = self.facts("F01")
        self.assertTrue(tower["class_shape"]["abstract"])
        self.assertEqual(
            ["idf_objtypename", "to_idf_main_object"],
            tower["class_shape"]["abstract_methods"],
        )
        self.assertEqual("CT_for_Chiller_named_Name Context", tower["names"]["object"])
        self.assertEqual(
            "Loop_for_CT_for_Chiller_named_Name Context",
            tower["names"]["loop"],
        )
        self.assertTrue(tower["tower_name_not_used_in_context_names"])
        self.assertEqual("nan", tower["permissive_state"]["pump_efficiency"]["token"])

    def test_source_constructors_state_validation_quirks_and_types_are_exact(self) -> None:
        absorption = self.facts("A01")
        self.assertEqual("Chiller:Absorption", absorption["default_state"]["idf_object_type"])
        self.assertTrue(absorption["default_state"]["heatsource_identity_preserved"])
        self.assertEqual("nan", absorption["permissive_state"]["pump_efficiency"]["token"])
        self.assertEqual("TypeError", absorption["missing_required_arguments"]["type"])

        boiler = self.facts("B01")
        self.assertEqual("Boiler:HotWater", boiler["default_state"]["idf_object_type"])
        self.assertTrue(boiler["permissive_state"]["fuel_not_coerced"])
        self.assertEqual("positive-infinity", boiler["permissive_state"]["setpoint_temperature"]["token"])

        chiller = self.facts("C01")
        self.assertEqual("Chiller:Electric:EIR", chiller["default_state"]["idf_object_type"])
        self.assertEqual("Chiller:Electric:EIR", chiller["screw_object_type"])
        self.assertEqual("ValueError", chiller["invalid_compressor"]["type"])
        self.assertEqual("TypeError", chiller["missing_capacity"]["type"])
        self.assertTrue(chiller["permissive_state"]["coolingtower_identity_preserved"])

        heatpump = self.facts("I01")
        self.assertTrue(heatpump["string_fuel_coerced"])
        self.assertEqual("ValueError", heatpump["invalid_fuel"]["type"])
        self.assertEqual("nan", heatpump["permissive_state"]["heating_cop"]["token"])
        self.assertEqual("0", heatpump["permissive_state"]["cooling_cop"]["value"])
        self.assertEqual(
            "AirConditioner:VariableRefrigerantFlow",
            heatpump["state_before_mutation"]["idf_object_type"],
        )

        geothermal = self.facts("H01")
        self.assertTrue(geothermal["class_shape"]["abstract"])
        self.assertEqual(["to_idf_object"], geothermal["class_shape"]["abstract_methods"])
        self.assertEqual({"kind": "none"}, geothermal["direct_idf_object_type_body"])
        self.assertEqual("TypeError", geothermal["direct_instantiation"]["type"])

    def test_all_four_tower_types_capacity_precedence_and_legacy_omission_are_exact(self) -> None:
        concrete = self.facts("E01")
        families = {item["type"]: item for item in concrete["families"]}
        self.assertEqual(
            {
                "ClosedSingleSpeedCoolingTower",
                "ClosedTwoSpeedCoolingTower",
                "OpenSingleSpeedCoolingTower",
                "OpenTwoSpeedCoolingTower",
            },
            set(families),
        )
        expected_types = {
            "ClosedSingleSpeedCoolingTower": "FluidCooler:SingleSpeed",
            "ClosedTwoSpeedCoolingTower": "FluidCooler:TwoSpeed",
            "OpenSingleSpeedCoolingTower": "CoolingTower:SingleSpeed",
            "OpenTwoSpeedCoolingTower": "CoolingTower:TwoSpeed",
        }
        for name, family in families.items():
            self.assertEqual(expected_types[name], family["idf_object_type"])
            self.assertEqual(
                ["tower-capacity", "source-capacity", "fallback-capacity"],
                [branch["branch"] for branch in family["branches"]],
            )
            self.assertTrue(all(branch["fresh_result"] for branch in family["branches"]))
            self.assertTrue(all(branch["fresh_object"] for branch in family["branches"]))
            self.assertTrue(all(branch["state_unchanged"] for branch in family["branches"]))
            self.assertTrue(
                all(
                    branch["name"] == "CT_for_Chiller_named_Capacity Source"
                    for branch in family["branches"]
                )
            )
        for name in (
            "ClosedSingleSpeedCoolingTower",
            "OpenSingleSpeedCoolingTower",
            "OpenTwoSpeedCoolingTower",
        ):
            values = [self.finite(branch["capacity_value"]) for branch in families[name]["branches"]]
            self.assertEqual([111_000.0, 222_000.0, 1_000_000.0], values)
        self.assertEqual(
            [{"kind": "none"}] * 3,
            [
                branch["capacity_value"]
                for branch in families["ClosedTwoSpeedCoolingTower"]["branches"]
            ],
        )

    def test_validation_is_fail_closed_against_resealed_tampering(self) -> None:
        changed = self.changed_fixture()
        changed["cases"][0]["python"]["facts"]["default_state"]["name"] = "tampered"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        resealed = self.changed_fixture()
        resealed["cases"][0]["python"]["facts"]["default_state"]["name"] = "tampered"
        self.reseal(resealed)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(resealed)

        route = self.changed_fixture()
        route["consumer_contract"]["native_routes"]["Fuel"] = "invented"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(route)

        receipt = self.changed_fixture()
        receipt["target_receipts"][0]["symbol_hash"] = "sha256:" + "0" * 64
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(receipt)

        adjacent = self.changed_fixture()
        adjacent["consumer_contract"]["closure"]["adjacent_classifications"][
            "Fuel.__str__"
        ] = "equivalent"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(adjacent)

        native = self.changed_fixture()
        native["native_review"]["public_production_routes_only"] = False
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(native)

    def test_duplicate_keys_paths_addresses_and_raw_nonfinite_values_fail_closed(self) -> None:
        with self.assertRaises(ValueError):
            generator.load_json_without_duplicates_text('{"x": 1, "x": 2}')
        raw = FIXTURE_PATH.read_text(encoding="utf-8")
        self.assertNotIn(str(REPOSITORY_ROOT), raw)
        self.assertNotIn("C:\\\\", raw)
        self.assertNotRegex(raw, r"0x[0-9a-fA-F]{8,}")

        unsafe = self.changed_fixture()
        unsafe["cases"][0]["python"]["facts"]["unsafe"] = float("nan")
        with self.assertRaises(ValueError):
            generator.strict_json_dumps(unsafe)
        with self.assertRaises(ValueError):
            self.reseal(unsafe)

        noncanonical = self.changed_fixture()
        noncanonical["runtime"]["python_version"] = "3.12.8"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(noncanonical)


if __name__ == "__main__":
    unittest.main()

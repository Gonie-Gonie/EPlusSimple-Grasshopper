"""Fail-closed tests for the dragon HVAC misc-systems core oracle."""

from __future__ import annotations

from collections import Counter
import copy
import importlib.util
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
    / "generate_dragon_hvac_misc_systems_core_oracle.py"
)
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-hvac-misc-systems-core-oracle.json"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
BOOTSTRAP_PATH = (
    REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
)

EXPECTED_GENERATOR_BYTES = 53_922
EXPECTED_GENERATOR_SHA256 = (
    "sha256:ff4bb943baeefbee48be4a0e1a0eb467674cd6722c7c88c53b5e372d9f4ddc2f"
)
EXPECTED_FIXTURE_BYTES = 290_302
EXPECTED_FIXTURE_SHA256 = (
    "sha256:c875ac4cd72e80aaa9de793807247597c5084cb70c96fab879d95747fdba962b"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_hvac_misc_systems_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load misc-system generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class DragonHvacMiscSystemsCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-hvac-misc-systems-validator-"
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def case(value: dict[str, object], code: str) -> dict[str, object]:
        return next(item for item in value["cases"] if item["code"] == code)

    @classmethod
    def facts(cls, value: dict[str, object], code: str) -> dict[str, object]:
        return cls.case(value, code)["python"]["facts"]

    @staticmethod
    def scalar(value: dict[str, object]) -> object:
        kind = value["kind"]
        if kind == "none":
            return None
        if kind == "bool":
            return value["value"]
        if kind == "int":
            return int(value["value"])
        if kind == "float":
            return float.fromhex(value["hex"])
        if kind == "str":
            return value["value"]
        if kind == "special-float":
            return value["token"]
        return (kind, value.get("type"))

    @staticmethod
    def reseal(value: dict[str, object]) -> None:
        for case in value["cases"]:
            case["python"]["facts_sha256"] = generator.canonical_sha256(
                case["python"]["facts"]
            )
            without_hash = {
                key: item for key, item in case.items() if key != "case_sha256"
            }
            case["case_sha256"] = generator.canonical_sha256(without_hash)
        value["fact_sha256"] = {
            case["id"]: case["python"]["facts_sha256"] for case in value["cases"]
        }
        value["case_sha256"] = {
            case["id"]: case["case_sha256"] for case in value["cases"]
        }
        value["cases_sha256"] = generator.canonical_sha256(value["cases"])
        value["upstream"]["relocation"]["observations_sha256"] = value[
            "cases_sha256"
        ]

    def test_generator_and_fixture_are_exact_strict_utf8_receipts(self) -> None:
        self.assertEqual(EXPECTED_GENERATOR_BYTES, GENERATOR_PATH.stat().st_size)
        self.assertEqual(
            EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH)
        )
        self.assertEqual(EXPECTED_FIXTURE_BYTES, FIXTURE_PATH.stat().st_size)
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        raw = FIXTURE_PATH.read_bytes()
        self.assertTrue(raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", raw)
        value = self.fixture()
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            raw.decode("utf-8"),
        )

    def test_exact_15_targets_and_full_174_disjoint_source_partition(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        targets = inventory["target_receipts"]
        self.assertEqual(15, len(targets))
        self.assertEqual(
            generator.TARGET_INDEX_SYMBOLS,
            tuple((item["inventory_index"], item["symbol"]) for item in targets),
        )
        partitions = inventory["partitions"]
        self.assertEqual(set(generator.PARTITION_INDICES), set(partitions))
        flat = []
        for name, indices in generator.PARTITION_INDICES.items():
            receipts = partitions[name]
            self.assertEqual(list(indices), [item["inventory_index"] for item in receipts])
            self.assertEqual(
                generator.EXPECTED_PARTITION_RECEIPTS_SHA256[name],
                generator.canonical_sha256(receipts),
            )
            flat.extend(indices)
        self.assertEqual(174, len(flat))
        self.assertEqual(174, len(set(flat)))
        self.assertEqual(list(range(641, 815)), sorted(flat))
        full = sorted(
            (item for receipts in partitions.values() for item in receipts),
            key=lambda item: item["inventory_index"],
        )
        self.assertEqual(
            generator.EXPECTED_FULL_SOURCE_RECEIPTS_SHA256,
            generator.canonical_sha256(full),
        )

    def test_cases_partition_targets_once_and_hash_pins_are_exact(self) -> None:
        value = self.fixture()
        definitions = generator.case_definitions()
        self.assertEqual(6, len(definitions))
        counts = Counter(
            symbol for item in definitions for symbol in item["target_symbols"]
        )
        self.assertEqual(
            Counter({symbol: 1 for symbol in generator.TARGET_SYMBOLS}), counts
        )
        self.assertEqual(
            generator.EXPECTED_CASE_IDS, tuple(item["id"] for item in definitions)
        )
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(generator.EXPECTED_CASES_SHA256, value["cases_sha256"])

    def test_contract_is_conservative_7_equivalent_8_exception_public_only(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(
            Counter({"equivalent": 7, "exception": 8}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            {
                "DomesticHotWater.efficiency",
                "DomesticHotWater.to_idf_object",
                "PhotoVoltaicPanel.area",
                "PhotoVoltaicPanel.azimuth",
                "PhotoVoltaicPanel.effective_area_ratio",
                "PhotoVoltaicPanel.efficiency",
                "PhotoVoltaicPanel.tilt",
            },
            {
                symbol
                for symbol, classification in contract["classifications"].items()
                if classification == "equivalent"
            },
        )
        self.assertFalse(contract["internal_generate_claimed"])
        self.assertTrue(
            all(".Generate" not in route for route in contract["native_routes"].values())
        )
        self.assertIn(
            "ZoneVentilationAssignment ->",
            contract["native_routes"]["EnergyRecoveryVentilator.to_idf_object"],
        )
        self.assertNotIn(
            "EnergyModel",
            contract["native_routes"]["DomesticHotWater.to_idf_object"],
        )
        self.assertEqual(15, len({item["assertion_id"] for item in value["symbols"]}))

    def test_index_761_photovoltaic_emission_is_immutable_support_only(self) -> None:
        value = self.fixture()
        support = value["support"]
        self.assertEqual(generator.PV_SUPPORT_FIXTURE["path"], support["path"])
        self.assertEqual(generator.PV_SUPPORT_FIXTURE["schema"], support["schema"])
        self.assertEqual(
            generator.PV_SUPPORT_FIXTURE["cases_sha256"], support["cases_sha256"]
        )
        self.assertEqual(
            "immutable-index-761-photovoltaic-idf-emission-support-only",
            support["role"],
        )
        self.assertFalse(support["target_promoted"])
        self.assertEqual(
            [(761, "PhotoVoltaicPanel.to_idf_object")],
            [
                (item["inventory_index"], item["symbol"])
                for item in support["resolved_receipts"]
            ],
        )
        self.assertNotIn("PhotoVoltaicPanel.to_idf_object", generator.TARGET_SYMBOLS)
        evidence = value["consumer_contract"]["evidence_contract"]
        self.assertFalse(evidence["photovoltaic_index_761_emission_executed"])
        self.assertTrue(evidence["photovoltaic_index_761_support_reused"])

    def test_native_runtime_source_and_relocation_pins_are_exact(self) -> None:
        value = self.fixture()
        review = value["native_review"]
        self.assertEqual(generator.NATIVE_IMPLEMENTATION_COMMIT, review["native_implementation_commit"])
        self.assertEqual(
            generator.EXPECTED_NATIVE_REVIEW_SHA256,
            generator.canonical_sha256(review),
        )
        self.assertEqual(6, len(review["sources"]))
        self.assertTrue(review["domestic_hot_water_direct_public_api_only"])
        self.assertTrue(review["energy_recovery_ventilator_public_aggregate_route"])
        self.assertTrue(review["photovoltaic_public_api_only"])
        self.assertFalse(review["internal_generate_route_claimed"])
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(
                value["consumer_contract"]["runtime_signatures"]
            ),
        )
        self.assertEqual(
            generator.EXPECTED_LOADED_LOCAL_MODULES_SHA256,
            value["upstream"]["loaded_local_modules_sha256"],
        )
        self.assertEqual(12, len(value["upstream"]["loaded_local_modules"]))
        self.assertEqual(
            generator.EXPECTED_RELOCATED_OBSERVATIONS_SHA256,
            value["upstream"]["relocation"]["observations_sha256"],
        )
        self.assertTrue(value["upstream"]["relocation"]["path_independent"])
        self.assertFalse(
            value["consumer_contract"]["evidence_contract"][
                "native_runtime_executed_by_python_oracle"
            ]
        )
        self.assertFalse(
            value["consumer_contract"]["evidence_contract"][
                "active_energyplus_process_claim"
            ]
        )

    def test_domestic_hot_water_fuel_enum_validation_and_mutation_are_exact(self) -> None:
        facts = self.facts(self.fixture(), "A01")
        self.assertEqual(10, len(facts["fuel_enum"]))
        self.assertEqual("Electricity", facts["fuel_enum"][0]["value"])
        constructors = facts["fuel_constructor_matrix"]
        self.assertEqual("returned", constructors["enum-member"]["outcome"])
        self.assertEqual("Fuel", constructors["enum-member"]["fuel_storage_type"])
        self.assertEqual("returned", constructors["exact-value-string"]["outcome"])
        self.assertEqual("str", constructors["exact-value-string"]["fuel_storage_type"])
        for label in ("enum-name-string", "integer", "true", "none"):
            self.assertEqual("ValueError", constructors[label]["type"])
        mutations = facts["fuel_mutation_matrix"]
        self.assertEqual("returned", mutations["exact-value-string"]["outcome"]["outcome"])
        self.assertEqual("str", mutations["exact-value-string"]["storage_type_after"])
        self.assertEqual(
            "Electricity", self.scalar(mutations["enum-name-string"]["state_after"]["fuel"])
        )
        self.assertTrue(facts["name_is_mutable"])

    def test_domestic_efficiency_boundaries_nonfinite_bool_and_empty_lists_are_exact(self) -> None:
        facts = self.facts(self.fixture(), "A02")
        constructors = facts["constructor_matrix"]
        for label in (
            "minimum-positive-subnormal", "one", "nan", "true", "integer-one"
        ):
            self.assertEqual("returned", constructors[label]["outcome"], label)
        for label in (
            "negative-infinity", "negative-one", "negative-zero", "zero",
            "nextafter-one-up", "positive-infinity", "false",
        ):
            self.assertEqual("ValueError", constructors[label]["type"], label)
        self.assertEqual("TypeError", constructors["numeric-string"]["type"])
        self.assertEqual("TypeError", constructors["none"]["type"])
        mutations = facts["mutation_matrix"]
        self.assertEqual("bool", mutations["true"]["storage_type_after"])
        self.assertEqual(
            "nan", self.scalar(mutations["nan"]["state_after"]["efficiency"])
        )
        self.assertEqual(
            0.8, self.scalar(mutations["zero"]["state_after"]["efficiency"])
        )
        emission = facts["emission"]
        self.assertEqual([], emission["first"])
        self.assertEqual([], emission["second"])
        self.assertEqual("list", emission["result_type"])
        self.assertTrue(emission["fresh_result_list"])

    def test_erv_is_permissive_mutable_and_returns_fresh_empty_lists(self) -> None:
        facts = self.facts(self.fixture(), "B01")
        self.assertTrue(all(facts["aliases_before_mutation"].values()))
        self.assertTrue(
            all(
                item["outcome"] == "returned"
                for item in facts["constructor_matrix"].values()
            )
        )
        self.assertEqual(
            "nan",
            self.scalar(
                facts["constructor_matrix"]["none-bool-nan"]["state"][
                    "cooling_efficiency"
                ]
            ),
        )
        self.assertEqual("TypeError", facts["arity_errors"]["missing"]["type"])
        self.assertEqual("TypeError", facts["arity_errors"]["extra"]["type"])
        mutation = facts["mutation"]
        self.assertTrue(
            all(item["outcome"] == "returned" for item in mutation["outcomes"].values())
        )
        self.assertEqual(17, self.scalar(mutation["state_after"]["name"]))
        self.assertIsNone(self.scalar(mutation["state_after"]["cooling_efficiency"]))
        self.assertEqual([], facts["emission"]["first"])
        self.assertEqual([], facts["emission"]["second"])
        self.assertTrue(facts["emission"]["fresh_result_list"])

    def test_photovoltaic_constructor_shape_defaults_and_keyword_quirks_are_exact(self) -> None:
        facts = self.facts(self.fixture(), "C01")
        shape = facts["class_shape"]
        self.assertIn("effective_area_ratio", shape["class_signature"])
        self.assertIn("*", shape["class_signature"])
        self.assertEqual(0.7, self.scalar(facts["default_effective_area_ratio"]))
        self.assertEqual(0.7, self.scalar(facts["default_state"]["effective_area_ratio"]))
        self.assertIsNone(self.scalar(facts["keyword_state"]["azimuth"]))
        self.assertIs(self.scalar(facts["keyword_state"]["efficiency"]), True)
        self.assertEqual(
            "TypeError", facts["keyword_only_ratio_rejects_positional"]["type"]
        )
        self.assertEqual("returned", facts["name_is_unvalidated"]["outcome"])
        self.assertIsNone(self.scalar(facts["name_is_unvalidated"]["state"]["name"]))

    def test_photovoltaic_geometry_boundaries_nonfinite_bool_none_and_mutation_are_exact(self) -> None:
        facts = self.facts(self.fixture(), "C02")
        expected_returned = {
            "area": {
                "minimum-positive-subnormal", "one", "positive-infinity", "nan", "true"
            },
            "azimuth": {
                "zero", "maximum-below-360", "nan", "false", "true", "none"
            },
            "tilt": {"zero", "ninety", "nan", "false", "true"},
        }
        for name, matrix in facts.items():
            constructors = matrix["constructor_matrix"]
            returned = {
                label for label, item in constructors.items() if item["outcome"] == "returned"
            }
            self.assertEqual(expected_returned[name], returned, name)
            mutations = matrix["mutation_matrix"]
            self.assertEqual(
                {label: item["outcome"] for label, item in constructors.items()},
                {label: item["outcome"]["outcome"] for label, item in mutations.items()},
                name,
            )
            self.assertTrue(
                all(
                    item["failed_state_unchanged"] is True
                    for item in mutations.values()
                    if item["outcome"]["outcome"] == "raised"
                )
            )
        self.assertIsNone(
            self.scalar(
                facts["azimuth"]["constructor_matrix"]["none"]["state"]["azimuth"]
            )
        )

    def test_photovoltaic_efficiency_boundaries_nonfinite_bool_and_mutation_are_exact(self) -> None:
        facts = self.facts(self.fixture(), "C03")
        expected_returned = {
            "minimum-positive-subnormal", "one", "nan", "true", "integer-one"
        }
        for name, matrix in facts.items():
            constructors = matrix["constructor_matrix"]
            returned = {
                label for label, item in constructors.items() if item["outcome"] == "returned"
            }
            self.assertEqual(expected_returned, returned, name)
            self.assertEqual("TypeError", constructors["numeric-string"]["type"])
            self.assertEqual("TypeError", constructors["none"]["type"])
            mutations = matrix["mutation_matrix"]
            self.assertTrue(
                all(
                    item["failed_state_unchanged"] is True
                    for item in mutations.values()
                    if item["outcome"]["outcome"] == "raised"
                )
            )
            self.assertEqual(
                "nan",
                self.scalar(mutations["nan"]["state_after"][name]),
            )

    def test_validation_rejects_fact_reseal_contract_partition_route_and_support_drift(self) -> None:
        changed = copy.deepcopy(self.fixture())
        self.facts(changed, "A01")["name_is_mutable"] = False
        with self.assertRaises((ValueError, RuntimeError)):
            generator.validate_oracle(changed)

        resealed = copy.deepcopy(self.fixture())
        self.facts(resealed, "A01")["name_is_mutable"] = False
        self.reseal(resealed)
        with self.assertRaises((ValueError, RuntimeError)):
            generator.validate_oracle(resealed)

        classification = copy.deepcopy(self.fixture())
        classification["consumer_contract"]["classifications"]["DomesticHotWater"] = "equivalent"
        with self.assertRaises((ValueError, RuntimeError)):
            generator.validate_oracle(classification)

        partition = copy.deepcopy(self.fixture())
        partition["upstream"]["partitions"]["resolved"][0]["inventory_index"] = 693
        with self.assertRaises((ValueError, RuntimeError)):
            generator.validate_oracle(partition)

        route = copy.deepcopy(self.fixture())
        route["consumer_contract"]["native_routes"]["EnergyRecoveryVentilator.to_idf_object"] = "Internal.Generate"
        with self.assertRaises((ValueError, RuntimeError)):
            generator.validate_oracle(route)

        support = copy.deepcopy(self.fixture())
        support["support"]["target_promoted"] = True
        with self.assertRaises((ValueError, RuntimeError)):
            generator.validate_oracle(support)

    def test_fixture_contains_no_paths_addresses_or_raw_nonfinite_values(self) -> None:
        raw = FIXTURE_PATH.read_text(encoding="utf-8")
        self.assertNotIn(str(REPOSITORY_ROOT), raw)
        self.assertNotIn("C:\\", raw)
        self.assertNotRegex(raw, r"0x[0-9a-fA-F]{8,}")
        nonfinite = copy.deepcopy(self.fixture())
        self.facts(nonfinite, "A01")["unsafe"] = float("nan")
        with self.assertRaises(ValueError):
            generator.strict_json_dumps(nonfinite)

    def test_generator_reproduces_fixture_twice_byte_for_byte(self) -> None:
        self.assertTrue(PINNED_SOURCE_ROOT.is_dir())
        outputs = [self.temp_root / "first.json", self.temp_root / "second.json"]
        environment = os.environ.copy()
        environment.update(
            {
                "PYTHONHASHSEED": "0",
                "PYTHONUTF8": "1",
                "PYTHONDONTWRITEBYTECODE": "1",
            }
        )
        for output in outputs:
            command = [
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
            ]
            completed = subprocess.run(
                command,
                cwd=REPOSITORY_ROOT,
                env=environment,
                check=False,
                capture_output=True,
                text=True,
                encoding="utf-8",
                timeout=60,
            )
            self.assertEqual(0, completed.returncode, completed.stderr)
        expected = FIXTURE_PATH.read_bytes()
        self.assertEqual(expected, outputs[0].read_bytes())
        self.assertEqual(expected, outputs[1].read_bytes())


if __name__ == "__main__":
    unittest.main()

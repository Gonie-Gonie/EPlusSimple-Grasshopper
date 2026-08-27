"""Fail-closed tests for the EPlusSimple area-based shape-core oracle."""

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
DEPENDENCY_ROOT = REPOSITORY_ROOT / ".tools" / "python-reference" / "3.12.7" / "site-packages"
if DEPENDENCY_ROOT.is_dir():
    sys.path.insert(0, str(DEPENDENCY_ROOT))

GENERATOR_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "generate_epsimple_shape_core_oracle.py"
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = REPOSITORY_ROOT / "fixtures" / "reference" / "python-0.7.0" / "epsimple-shape-core-oracle.json"
PINNED_SOURCE_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
PINNED_SOURCE = PINNED_SOURCE_ROOT / "epsimple" / "core" / "shape.py"
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_shape_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load EPlusSimple shape generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 73_269
EXPECTED_GENERATOR_SHA256 = "sha256:40431189b32b4592b949d48a04092634618d84d1a2bfaa3db11b00a346b501a2"
EXPECTED_FIXTURE_BYTES = 108_435
EXPECTED_FIXTURE_SHA256 = "sha256:802bcf3d1bc05828329a659ec9013c498325ea5be8f647975dcbb4cb3eee2ba5"
EXPECTED_CASES_SHA256 = "sha256:1b6be41823b3a165d1e5c923f46278a44ae8ff68ccef1a0edd08d72ab637398e"


class EpsimpleShapeCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(
            prefix="epsimple-shape-core-tests-", dir=TEST_TEMP_ROOT
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
        matches = [item for item in value["cases"] if item["code"] == code]
        if len(matches) != 1:
            raise AssertionError(f"Expected exactly one shape case {code}.")
        return matches[0]

    @classmethod
    def facts(cls, value: dict[str, object], code: str) -> dict[str, object]:
        return cls.case(value, code)["python"]["facts"]

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

    def test_generator_fixture_and_every_case_hash_layer_are_exact(self) -> None:
        value = self.fixture()
        generator_raw = GENERATOR_PATH.read_bytes()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_raw))
        self.assertEqual(EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH))
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(17, len(value["fact_sha256"]))
        self.assertEqual(17, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_inventory_source_runtime_and_artifact_receipts_are_exact(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(inventory["symbols"], value["symbols"])
        self.assertEqual(inventory["target_receipts"], value["target_receipts"])
        self.assertEqual(inventory["excluded_receipts"], value["excluded_receipts"])
        self.assertEqual(generator._expected_runtime(), value["runtime"])
        self.assertEqual(generator._expected_artifacts(), value["artifacts"])
        self.assertEqual(
            generator._expected_upstream(generator._expected_loaded_sources()),
            value["upstream"],
        )
        self.assertEqual(generator.EXPECTED_SOURCE_BYTES, PINNED_SOURCE.stat().st_size)
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, generator.sha256_file(PINNED_SOURCE))

    def test_scope_is_exactly_the_requested_fifty_three_indices(self) -> None:
        value = self.fixture()
        expected = (
            list(range(405, 416))
            + list(range(417, 425))
            + [426]
            + [index for index in range(429, 463) if index != 450]
        )
        self.assertEqual(53, len(expected))
        self.assertEqual(expected, [item[0] for item in generator.EXPECTED_TARGETS])
        self.assertEqual(expected, [item["inventory_index"] for item in value["target_receipts"]])
        self.assertEqual([416, 425, 427, 428, 450], [item["inventory_index"] for item in value["excluded_receipts"]])
        targeted = [
            symbol for case in value["cases"] for symbol in case["target_symbols"]
        ]
        self.assertEqual(Counter(generator.TARGET_SYMBOLS), Counter(targeted))
        self.assertTrue(all(count == 1 for count in Counter(targeted).values()))
        all_case_symbols = {
            symbol
            for case in value["cases"]
            for symbol in (*case["target_symbols"], *case["context_symbols"])
        }
        self.assertTrue(set(generator.EXCLUDED_SYMBOLS).isdisjoint(all_case_symbols))

    def test_consumer_contract_is_reviewed_and_uses_only_public_native_routes(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(Counter({"equivalent": 33, "exception": 20}), Counter(contract["classifications"].values()))
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.EXCEPTION_ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(contract["native_routes"]))
        self.assertTrue(all(route.startswith("GonieGonie.SimpleDragon.") for route in contract["native_routes"].values()))
        self.assertTrue(all(".Internal." not in route and "GrmVocabulary" not in route for route in contract["native_routes"].values()))

    def test_blind_and_fenestration_abstract_dispatch_deepcopy_semantics(self) -> None:
        value = self.fixture()
        blind = self.facts(value, "B01")
        abstract = self.facts(value, "F01")
        dispatch = self.facts(value, "F02")
        self.assertEqual([("SHADE", "shade"), ("VENETIAN", "venetian")], [(item["name"], item["string"]) for item in blind["members"]])
        self.assertEqual("ValueError", blind["invalid"]["error"]["type"])
        self.assertTrue(abstract["is_abstract"])
        self.assertEqual(["construction", "to_dragon"], abstract["abstract_methods"])
        self.assertEqual("TypeError", abstract["direct_instantiation"]["error"]["type"])
        self.assertEqual("FNST-AUTOID<address>", dispatch["automatic_id"])
        self.assertEqual(["Window", "Door", "Window"], [item["type"] for item in dispatch["factory_dispatch"]])
        self.assertEqual([None, None, None], [item["copy"]["blind"] for item in dispatch["deepcopies"]])
        self.assertTrue(all(item["construction_shared"] and not item["same_object"] for item in dispatch["deepcopies"]))

    def test_door_glassdoor_and_window_validation_and_conversion(self) -> None:
        value = self.fixture()
        door = self.facts(value, "D01")
        glassdoor = self.facts(value, "G01")
        window = self.facts(value, "W01")
        mapping = self.facts(value, "W02")
        self.assertTrue(door["class_topology"]["is_fenestration_subclass"])
        self.assertEqual("NoMassConstruction", door["to_dragon"]["construction_type"])
        self.assertEqual("ValueError", door["construction_validation"]["transparent_error"]["error"]["type"])
        self.assertTrue(glassdoor["subclass_of_window"])
        self.assertEqual(("Window", "Blind"), (glassdoor["to_dragon"]["output_type"], glassdoor["to_dragon"]["blind_type"]))
        self.assertEqual(["shade", "venetian", None], window["blind_transitions"])
        self.assertEqual("ValueError", window["validation"]["opaque_construction"]["error"]["type"])
        self.assertEqual([None, "Shade", "Blind"], [item["output_blind_type"] for item in mapping["to_dragon_mappings"]])

    def test_surface_properties_constraints_deepcopy_and_flip_are_observed(self) -> None:
        value = self.fixture()
        properties = self.facts(value, "S01")
        copies = self.facts(value, "S02")
        self.assertEqual("SURF-AUTOID<address>", properties["automatic_id"])
        self.assertEqual(["wall", "floor", "ceiling"], [item["type"] for item in properties["states"]])
        self.assertEqual("zone", properties["boundary_coupling"]["after_adjacent_assignment"]["boundary"])
        self.assertTrue(properties["boundary_coupling"]["after_boundary_reset"]["adjacent_is_none"])
        self.assertTrue(all(item["outcome"] == "raised" for item in properties["validation"].values()))
        self.assertEqual(["wall", "ceiling", "floor"], [item["type"] for item in copies["flips"]])
        self.assertEqual([270, None, None], [item["azimuth"] for item in copies["flips"]])
        self.assertTrue(copies["inplace"]["returned_none"])
        self.assertEqual(225, copies["inplace"]["azimuth"])

    def test_surface_json_special_constructions_counts_unique_and_dragon_geometry(self) -> None:
        value = self.fixture()
        parsed = self.facts(value, "S03")
        counts = self.facts(value, "S04")
        converted = self.facts(value, "S05")
        self.assertEqual(["OpenConstruction", "UnknownConstruction", "SurfaceConstruction"], [item["construction_type"] for item in parsed["construction_branches"]])
        self.assertEqual(["Window", "Door"], [item["type"] for item in parsed["defined_wall"]["fenestrations"]])
        self.assertEqual((3, 1, 4), (counts["window_count"], counts["door_count"], counts["fenestration_count"]))
        self.assertEqual(["FC-G", "FC-D"], counts["unique_construction_keys"])
        self.assertEqual(["WIN-A", "GLASSDOOR-A"], converted["window_names"])
        self.assertEqual(["DOOR-A"], converted["door_names"])
        self.assertEqual(4, len(converted["vertices"]))
        self.assertEqual(["Shade", "Blind"], converted["window_blind_types"])

    def test_zone_validation_area_infiltration_supply_and_json_counts(self) -> None:
        value = self.fixture()
        constructor = self.facts(value, "Z01")
        metrics = self.facts(value, "Z02")
        parsed = self.facts(value, "Z03")
        self.assertEqual("ZONE-AUTOID<address>", constructor["automatic_id"])
        self.assertEqual(["SYS-HEAT"], constructor["defensive_supply_copy"])
        self.assertEqual({"TypeError", "ValueError"}, {item["error"]["type"] for item in constructor["validation"].values()})
        self.assertEqual(40, metrics["area"])
        self.assertEqual(["SYS-HEAT", "SYS-BOTH"], metrics["heating_ids"])
        self.assertEqual(["SYS-COOL", "SYS-BOTH"], metrics["cooling_ids"])
        self.assertEqual([0, 0, "1.5", "1.5"], [item["value"] if isinstance(item["value"], int) else item["value"]["decimal"] for item in metrics["infiltration_cases"]])
        self.assertTrue(parsed["profile_identity_preserved"])
        self.assertTrue(parsed["ventilation_alias_identity"])
        self.assertEqual(["VENT-A", "VENT-A", "VENT-A"], parsed["ventilation_ids"])

    def test_zone_unique_aggregations_and_upstream_to_dragon_failure_are_observed(self) -> None:
        value = self.fixture()
        unique = self.facts(value, "Z04")
        failure = self.facts(value, "Z05")
        self.assertEqual(["FC-G", "FC-D"], [item["id"] for item in unique["fenestration_constructions"]])
        self.assertEqual(["MAT-B", "MAT-A"], [item["id"] for item in unique["materials"]])
        self.assertEqual(["SC-B", "SC-A"], [item["id"] for item in unique["surface_constructions"]])
        self.assertEqual("raised", failure["to_dragon"]["outcome"])
        self.assertEqual("NotImplementedError", failure["to_dragon"]["error"]["type"])

    def test_fact_contract_receipt_and_source_tampering_fail_closed(self) -> None:
        mutations = []
        fact = copy.deepcopy(self.fixture())
        self.facts(fact, "S04")["window_count"] = 99
        self.reseal(fact)
        mutations.append(fact)
        contract = copy.deepcopy(self.fixture())
        contract["consumer_contract"]["classifications"]["Surface.area"] = "exception"
        mutations.append(contract)
        receipt = copy.deepcopy(self.fixture())
        receipt["target_receipts"][0]["body_hash"] = "sha256:" + ("0" * 64)
        receipt["symbols"][0]["body_hash"] = "sha256:" + ("0" * 64)
        mutations.append(receipt)
        source = copy.deepcopy(self.fixture())
        source["upstream"]["source"]["source_sha256"] = "sha256:" + ("0" * 64)
        mutations.append(source)
        for mutated in mutations:
            with self.assertRaises(RuntimeError):
                generator.validate_oracle(mutated)

    def test_nondeterministic_and_noncanonical_values_fail_closed(self) -> None:
        address = copy.deepcopy(self.fixture())
        self.facts(address, "B01")["escaped"] = "object at 0x1234abcd"
        self.reseal(address)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(address)
        raw_float = copy.deepcopy(self.fixture())
        self.facts(raw_float, "B01")["escaped"] = 0.5
        self.reseal(raw_float)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(raw_float)

    def test_duplicate_keys_and_nonfinite_json_are_rejected(self) -> None:
        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"a","schema":"b"}', encoding="utf-8")
        with self.assertRaises(SystemExit):
            generator.load_json_without_duplicates(duplicate)
        nonfinite = self.temp_root / "nonfinite.json"
        nonfinite.write_text('{"value":NaN}', encoding="utf-8")
        with self.assertRaises(SystemExit):
            generator.load_json_without_duplicates(nonfinite)

    def test_generator_regenerates_fixture_byte_for_byte(self) -> None:
        output = self.temp_root / "regenerated.json"
        environment = os.environ.copy()
        environment["PYTHONHASHSEED"] = "0"
        environment["PYTHONDONTWRITEBYTECODE"] = "1"
        command = [
            sys.executable,
            str(BOOTSTRAP_PATH),
            "--dependency-root", str(DEPENDENCY_ROOT),
            "--upstream-source", str(PINNED_SOURCE_ROOT),
            "--generator", str(GENERATOR_PATH),
            "--",
            "--inventory", str(INVENTORY_PATH),
            "--output", str(output),
            "--upstream-commit", generator.EXPECTED_UPSTREAM_COMMIT,
        ]
        result = subprocess.run(
            command,
            cwd=REPOSITORY_ROOT,
            env=environment,
            capture_output=True,
            text=True,
            timeout=60,
            check=False,
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(FIXTURE_PATH.read_bytes(), output.read_bytes())


if __name__ == "__main__":
    unittest.main()

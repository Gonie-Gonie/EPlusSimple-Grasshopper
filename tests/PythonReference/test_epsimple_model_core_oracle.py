"""Fail-closed tests for the EPlusSimple GreenRetrofitModel behavior oracle."""

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
    / "generate_epsimple_model_core_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-model-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_model_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load model-core generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 80_750
EXPECTED_GENERATOR_SHA256 = (
    "sha256:39ce166f6fcc2d51056bf1bb5a06416891c04d34375b898ac709a53fb7abd70e"
)
EXPECTED_FIXTURE_BYTES = 102_172
EXPECTED_FIXTURE_SHA256 = (
    "sha256:e5cfdc9ba823dc891693864051ffb8cbc06cd08137becef9d6c06fd0c2942cf6"
)


def decode_number(value: object) -> int | float | bool:
    if not isinstance(value, dict):
        raise AssertionError(f"Expected typed number, got {type(value).__name__}.")
    kind = value["kind"]
    if kind == "bool":
        return value["value"]
    if kind == "int":
        return int(value["value"])
    if kind == "float":
        return float.fromhex(value["hex"])
    if kind == "float-nonfinite":
        return {
            "nan": math.nan,
            "positive-infinity": math.inf,
            "negative-infinity": -math.inf,
        }[value["value"]]
    raise AssertionError(f"Unexpected typed number kind: {kind}")


class EPlusSimpleModelCoreOracleTests(unittest.TestCase):
    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def facts(value: dict[str, object], code: str) -> dict[str, object]:
        matches = [
            case["python"]["facts"] for case in value["cases"] if case["code"] == code
        ]
        if len(matches) != 1:
            raise AssertionError(f"Expected one model-core case {code}.")
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
        self.assertEqual(11, len(value["cases"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_two_independent_bootstrap_regenerations_are_byte_identical(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="epsimple-model-core-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_and_matrix_closure_are_exact(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        targets = [
            *range(337, 343),
            *range(345, 358),
            *range(359, 373),
            387,
            388,
        ]
        excluded = [343, 344, 358]
        deferred = list(range(373, 387))
        self.assertEqual(targets, [item["inventory_index"] for item in value["target_receipts"]])
        self.assertEqual(excluded, [item["inventory_index"] for item in value["excluded_receipts"]])
        self.assertEqual(deferred, [item["inventory_index"] for item in value["deferred_receipts"]])
        self.assertEqual(35, len(inventory["target_receipts"]))
        self.assertEqual(3, len(inventory["excluded_receipts"]))
        self.assertEqual(14, len(inventory["deferred_receipts"]))
        self.assertEqual(list(range(337, 389)), sorted(targets + excluded + deferred))

        matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        for receipt in value["target_receipts"]:
            index = receipt["inventory_index"]
            symbol = receipt["symbol"]
            self.assertIn(
                matrix["classifications"][index],
                {"needs_reverification", generator.CLASSIFICATIONS[symbol]},
            )
        self.assertEqual(
            ["out_of_scope"] * 3,
            [matrix["classifications"][index] for index in excluded],
        )
        self.assertTrue(
            all(
                matrix["classifications"][index] in {"needs_reverification", "equivalent", "exception"}
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
            Counter({"exception": 24, "equivalent": 11}),
            Counter(contract["classifications"].values()),
        )
        self.assertTrue(contract["closure"]["exact_one_case_target_partition"])
        self.assertTrue(contract["closure"]["full_source_partition"])
        self.assertEqual(52, contract["closure"]["source_declaration_count"])
        evidence = contract["evidence_contract"]
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertFalse(evidence["full_idf_semantic_parity_claim"])
        self.assertTrue(evidence["run_boundary_instrumented"])
        self.assertTrue(all(route.startswith("GonieGonie.") for route in contract["native_routes"].values()))

    def test_runtime_source_dependency_resource_and_relocation_receipts_are_exact(self) -> None:
        value = self.fixture()
        runtime = value["runtime"]
        self.assertEqual(generator.EXPECTED_DEPENDENCIES, runtime["dependencies"])
        self.assertEqual(
            generator.canonical_sha256(generator.EXPECTED_DEPENDENCIES),
            runtime["dependencies_sha256"],
        )
        self.assertEqual("3.12.7", runtime["python_version"])
        upstream = value["upstream"]
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, upstream["source"]["source_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_AST_SHA256, upstream["source"]["ast_sha256"])
        self.assertEqual(list(generator.WEATHER_RESOURCES), upstream["weather_resources"])
        self.assertEqual(generator.MODEL_RESOURCE, upstream["model_resource"])
        isolated = upstream["isolated_import"]
        self.assertEqual(2, isolated["source_location_count"])
        self.assertEqual(23, len(isolated["loaded_local_modules"]))
        self.assertEqual(
            generator.EXPECTED_LOADED_LOCAL_MODULES_SHA256,
            isolated["loaded_local_modules_sha256"],
        )
        self.assertEqual(
            generator.EXPECTED_RELOCATION_SNAPSHOT_SHA256,
            isolated["relocation_snapshot_sha256"],
        )
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(value["consumer_contract"]["runtime_signatures"]),
        )

    def test_weather_tables_address_selection_and_errors_are_pinned(self) -> None:
        value = self.fixture()
        tables = self.facts(value, "T01")
        self.assertEqual([252, 9], tables["address_table"]["shape"])
        self.assertEqual([252, 5], tables["climate_table"]["shape"])
        self.assertTrue(tables["address_table"]["index_is_unique"])
        self.assertTrue(tables["climate_table"]["columns_are_yyyymmdd"])
        self.assertFalse(tables["climate_table"]["index_equals_address_table"])

        address = self.facts(value, "A01")
        self.assertEqual("InvalidAddressError", address["invalid_address"]["type"])
        self.assertEqual(["Exception"], address["invalid_error_bases"])
        self.assertEqual(2, len(address["valid_observations"]))
        self.assertNotEqual(
            address["valid_observations"][0]["climate"],
            address["valid_observations"][1]["climate"],
        )
        self.assertTrue(
            all(
                item["path_parent_matches_declared_weather_directory"]
                and item["epw_filename"].endswith(".epw")
                for item in address["valid_observations"]
            )
        )

    def test_energyplus_error_constructor_mutable_alias_and_validation_quirks_are_pinned(self) -> None:
        value = self.fixture()
        error = self.facts(value, "E01")
        self.assertEqual(
            "===EnergyPlusError===\nsevere-safe\nfatal-safe",
            error["filtered_message"],
        )
        self.assertNotIn("warning-safe", error["filtered_message"])
        self.assertEqual(error["missing_args"][0], error["missing_message"])

        model = self.facts(value, "M01")
        self.assertTrue(model["address_retained_after_failure"])
        self.assertEqual("2024-05-06", model["vintage_from_list"])
        self.assertEqual("ValueError", model["invalid_vintage"]["type"])
        self.assertTrue(all(model["mutable_default_alias"].values()))
        self.assertEqual(
            ["returned", "returned", "raised", "raised", "raised"],
            [item["outcome"]["outcome"] for item in model["north_axis_probes"]],
        )

    def test_area_exterior_and_six_weighted_averages_are_pinned(self) -> None:
        value = self.fixture()
        projection = self.facts(value, "P01")
        self.assertEqual(60.0, decode_number(projection["area"]))
        self.assertEqual(["SURF-W1", "SURF-W2"], projection["exterior_wall_ids"])
        self.assertEqual(["SURF-R1", "SURF-R2"], projection["exterior_roof_ids"])
        self.assertEqual(["SURF-F1", "SURF-F2"], projection["exterior_floor_ids"])
        self.assertEqual(["WIN-1"], projection["exterior_window_ids"])

        weighted = self.facts(value, "W01")
        self.assertEqual(6, len(weighted["weighted"]))
        self.assertEqual(6, len(weighted["zero"]))
        self.assertTrue(
            all(decode_number(item) == 0 for item in weighted["zero"].values())
        )
        self.assertAlmostEqual(0.9, decode_number(weighted["weighted"]["averaged_infiltration"]))
        self.assertAlmostEqual(
            20 / 3,
            decode_number(weighted["weighted"]["averaged_lightdensity"]),
        )
        self.assertTrue(weighted["unknown_identity_comparison"]["fresh_constructor_is_same_singleton"])

    def test_source_merge_unique_catalog_full_graph_conversion_and_run_boundaries_are_pinned(self) -> None:
        value = self.fixture()
        source = self.facts(value, "S01")
        self.assertTrue(source["computed_first_source_is_overwritten"])
        self.assertTrue(source["explicit_invalid_item_preserved"])
        self.assertTrue(source["explicit_source_duplicates_computed_source"])
        self.assertTrue(source["none_source_excluded"])
        self.assertEqual("TypeError", source["non_iterable_assignment"]["type"])

        unique = self.facts(value, "U01")
        self.assertEqual(["CON-WIN"], unique["fenestration_construction_ids"])
        self.assertEqual(["MAT-A", "MAT-B"], unique["material_ids"])
        self.assertEqual(["PROFILE-A", "PROFILE-B"], unique["profile_ids"])

        graph = self.facts(value, "J01")
        self.assertTrue(graph["adjacent_object_allocated"])
        self.assertEqual("ZONE-ADJ", graph["adjacent_zone_id"])
        self.assertEqual(2, len(graph["zone_ids"]))
        self.assertTrue(graph["unused_source_preserved"])
        self.assertEqual(8, graph["surface_count"])

        conversion = self.facts(value, "C01")
        self.assertEqual("idragon.dragon.model.EnergyModel", conversion["dragon"]["runtime_type"])
        self.assertEqual(1, conversion["dragon"]["zone_count"])
        self.assertEqual("idragon.imugi.IDF", conversion["idf"]["runtime_type"])
        self.assertEqual([24, 2, 0], conversion["idf"]["version"])
        self.assertGreater(conversion["idf"]["nonempty_object_class_count"], 0)

        run = self.facts(value, "R01")
        self.assertFalse(run["energyplus_process_started"])
        self.assertEqual("GreenRetrofitResult", run["success"]["runtime_type"])
        self.assertTrue(run["success"]["model_identity_retained"])
        self.assertTrue(run["success"]["result_identity_retained"])
        self.assertEqual("EnergyPlusError", run["failure"]["type"])
        self.assertEqual(run["success"]["weather_calls"], run["failure_calls"])

    def test_resealed_fact_contract_receipt_route_and_order_tampering_fail_closed(self) -> None:
        original = self.fixture()

        fact = copy.deepcopy(original)
        self.facts(fact, "P01")["exterior_wall_ids"].append("SURF-EVIL")
        self.reseal(fact)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(fact)

        classification = copy.deepcopy(original)
        classification["consumer_contract"]["classifications"]["GreenRetrofitModel.area"] = "exception"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(classification)

        route = copy.deepcopy(original)
        route["consumer_contract"]["native_routes"]["GreenRetrofitModel.area"] = "wrong"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(route)

        receipt = copy.deepcopy(original)
        receipt["target_receipts"][0]["inventory_index"] = 0
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(receipt)

        resource = copy.deepcopy(original)
        resource["upstream"]["weather_resources"][0]["bytes"] = 1
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(resource)

        order = copy.deepcopy(original)
        order["cases"][0], order["cases"][1] = order["cases"][1], order["cases"][0]
        self.reseal(order)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(order)

    def test_strict_json_duplicate_keys_unsafe_values_and_inventory_tampering_fail_closed(self) -> None:
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

        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="epsimple-model-core-tamper-", dir=TEST_TEMP_ROOT
        ) as temporary:
            inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
            inventory["symbols"][337]["symbol_hash"] = "sha256:" + "0" * 64
            path = Path(temporary) / "inventory.json"
            path.write_text(json.dumps(inventory), encoding="utf-8", newline="\n")
            with self.assertRaises(SystemExit):
                generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)


if __name__ == "__main__":
    unittest.main()

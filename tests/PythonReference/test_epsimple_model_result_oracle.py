"""Fail-closed tests for the EPlusSimple GreenRetrofitResult oracle."""

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
    / "generate_epsimple_model_result_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
MATRIX_PATH = REPOSITORY_ROOT / "upstream" / "compatibility-matrix.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-model-result-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_model_result_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load model-result generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 71_720
EXPECTED_GENERATOR_SHA256 = (
    "sha256:1fa83c6072574bb7577021b71be0a093fcd222bc35dd9e0642888056c99f3c8a"
)
EXPECTED_FIXTURE_BYTES = 763_624
EXPECTED_FIXTURE_SHA256 = (
    "sha256:d639c5c1047dca6a3682c9c2cfdac5fd1da99b5743c11d591d50942ae5322c02"
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


def frame_cell(frame: dict[str, object], index: str, column: str) -> object:
    row = frame["index"].index(index)
    col = frame["columns"].index(column)
    return frame["data"][row][col]


class EPlusSimpleModelResultOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one model-result case {code}.")
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

    def test_exact_runtime_generator_fixture_and_hash_layers(self) -> None:
        self.assertEqual((3, 12, 7), sys.version_info[:3])
        self.assertEqual("cpython", sys.implementation.name)
        self.assertEqual("win32", sys.platform)
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
            prefix="epsimple-model-result-regeneration-", dir=TEST_TEMP_ROOT
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

    def test_inventory_source_dependency_and_relocation_closure(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        targets = list(range(373, 387))
        adjacent = [*range(337, 373), 387, 388]
        self.assertEqual(targets, [item["inventory_index"] for item in value["target_receipts"]])
        self.assertEqual(14, len(inventory["target_receipts"]))
        self.assertEqual(list(range(337, 389)), sorted(targets + adjacent))
        self.assertEqual(
            generator.EXPECTED_ADJACENT_RECEIPTS_SHA256,
            value["upstream"]["adjacent_receipts_sha256"],
        )
        matrix = json.loads(MATRIX_PATH.read_text(encoding="utf-8"))
        for receipt in value["target_receipts"]:
            index = receipt["inventory_index"]
            symbol = receipt["symbol"]
            self.assertIn(
                matrix["classifications"][index],
                {"needs_reverification", generator.CLASSIFICATIONS[symbol]},
            )

        runtime = value["runtime"]
        self.assertEqual(generator.EXPECTED_DEPENDENCIES, runtime["dependencies"])
        self.assertEqual(
            generator.canonical_sha256(generator.EXPECTED_DEPENDENCIES),
            runtime["dependencies_sha256"],
        )
        upstream = value["upstream"]
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, upstream["source"]["source_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_AST_SHA256, upstream["source"]["ast_sha256"])
        isolated = upstream["isolated_import"]
        self.assertEqual(2, isolated["source_location_count"])
        self.assertEqual(23, len(isolated["loaded_local_modules"]))
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

    def test_native_classifications_routes_and_reviewed_sources_are_exact(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"equivalent": 9, "exception": 5}),
            Counter(contract["classifications"].values()),
        )
        closure = contract["closure"]
        self.assertEqual(list(range(373, 387)), closure["target_indices"])
        self.assertEqual(38, closure["adjacent_count"])
        self.assertEqual(52, closure["source_declaration_count"])
        self.assertTrue(closure["exact_one_case_target_partition"])
        self.assertTrue(closure["full_model_source_partition"])

        audit = contract["native_route_audit"]
        self.assertEqual(
            {
                "from_csv",
                "from_result",
                "from_sqlite",
                "to_dict",
                "to_json",
                "to_monthly_csv",
                "to_monthly_json",
                "write",
            },
            set(audit),
        )
        self.assertEqual("intentional-absence", audit["from_sqlite"]["status"])
        self.assertEqual("native-extension", audit["to_monthly_csv"]["status"])
        self.assertFalse(contract["evidence_contract"]["native_csv_or_sqlite_execution_claim"])
        self.assertEqual(
            list(generator.NATIVE_SOURCE_RECEIPTS),
            value["native_review"]["source_receipts"],
        )
        for receipt in generator.NATIVE_SOURCE_RECEIPTS:
            path = REPOSITORY_ROOT / receipt["path"]
            self.assertEqual(receipt["bytes"], path.stat().st_size)
            self.assertEqual(receipt["sha256"], generator.sha256_file(path))

    def test_lifecycle_dhw_calendar_server_and_calculation_boundaries(self) -> None:
        value = self.fixture()
        lifecycle = self.facts(value, "R01")
        self.assertEqual(2, decode_number(lifecycle["valid_digits"]))
        self.assertTrue(lifecycle["model_identity_retained"])
        self.assertTrue(lifecycle["result_identity_retained"])
        self.assertTrue(lifecycle["empty_table_is_accepted"])
        self.assertEqual("EnergyPlusError", lifecycle["tbl_none"]["type"])
        self.assertEqual("AttributeError", lifecycle["missing_tbl"]["type"])
        self.assertTrue(all(not present for present in lifecycle["requested_route_member_presence"].values()))
        self.assertEqual(0.0, decode_number(lifecycle["area_probes"][0]["observed"]))
        self.assertTrue(math.isnan(decode_number(lifecycle["area_probes"][2]["observed"])))

        demand = self.facts(value, "D01")
        self.assertEqual(365, decode_number(demand["calendar"]["days"]))
        self.assertEqual("2026-01-01", demand["calendar"]["first"])
        self.assertEqual([11.0, 5.0], [decode_number(item) for item in demand["energy"][:2]])
        self.assertEqual(6.0, decode_number(demand["energy"][-1]))
        self.assertEqual("TypeError", demand["invalid_domestic_hotwater"]["type"])
        self.assertEqual("AttributeError", demand["zone_without_profile"]["type"])

        servers = self.facts(value, "D02")
        self.assertTrue(servers["duplicate_last_write_wins"])
        self.assertTrue(servers["fallback"]["id_has_generated_source_prefix"])
        self.assertEqual("Boiler", servers["fallback"]["snapshot_without_unstable_id"]["type"])
        self.assertEqual(
            ["Boiler", "DistrictHeating", "SimpleNamespace"],
            [item["type"] for item in servers["selected"]],
        )
        self.assertTrue(servers["unsupported_hotwater_object_is_selected"])

        calculation = self.facts(value, "D03")
        mixed = calculation["mixed_boiler_district"]
        self.assertAlmostEqual(0.5, decode_number(mixed["NATURALGAS"][0]))
        self.assertAlmostEqual(0.4, decode_number(mixed["DISTRICTHEATING"][0]))
        self.assertEqual("RuntimeError", calculation["unsupported_server"]["type"])
        self.assertEqual("ZeroDivisionError", calculation["zero_area"]["type"])
        self.assertEqual("ZeroDivisionError", calculation["zero_efficiency"]["type"])
        self.assertEqual("ZeroDivisionError", calculation["no_servers"]["type"])

    def test_site_source_carbon_and_cost_metric_behaviors(self) -> None:
        value = self.fixture()
        site = self.facts(value, "S01")
        frame = site["full_frame"]
        self.assertEqual(5, len(frame["index"]))
        self.assertEqual(7, len(frame["columns"]))
        generators = frame_cell(frame, "ELECTRICITY", "generators")
        self.assertAlmostEqual(0.8, decode_number(generators[0]))
        self.assertEqual(0.0, decode_number(generators[1]))
        self.assertEqual(
            ["ELECTRICITYPRODUCED:FACILITY", "ELECTRICITYSURPLUSSOLD:FACILITY"],
            site["balance_columns_mutated"],
        )
        self.assertTrue(site["water_system_table_is_overwritten"])
        self.assertEqual(2, len(frame_cell(site["short_table_frame"], "ELECTRICITY", "heating")))
        self.assertEqual("KeyError", site["malformed_balance"]["type"])

        source = self.facts(value, "S02")
        self.assertEqual(3, decode_number(source["enum_iteration_value_count"]))
        self.assertEqual(
            [2.75, 1.1, 0.728],
            [decode_number(item) for item in source["enum_iteration_values"]],
        )
        converted = source["converted"]
        self.assertAlmostEqual(
            410.25,
            decode_number(frame_cell(converted, "OIL", "heating")[0]),
        )
        self.assertAlmostEqual(
            round(310.25 * 0.728, 2),
            decode_number(frame_cell(converted, "LPG", "heating")[0]),
        )
        self.assertEqual(1, decode_number(source["site_call_count"]))
        self.assertTrue(source["input_unchanged"])

        carbon = self.facts(value, "S03")
        cost = self.facts(value, "S04")
        self.assertEqual(5, decode_number(carbon["enum_iteration_value_count"]))
        self.assertEqual(5, decode_number(cost["enum_iteration_value_count"]))
        self.assertAlmostEqual(
            round(110.25 * 0.4541, 2),
            decode_number(frame_cell(carbon["converted"], "ELECTRICITY", "heating")[0]),
        )
        self.assertAlmostEqual(
            round(110.25 * 162.92, 2),
            decode_number(frame_cell(cost["converted"], "ELECTRICITY", "heating")[0]),
        )

    def test_summary_dictionary_and_write_behavior_are_pinned(self) -> None:
        value = self.fixture()
        summary = self.facts(value, "S05")
        self.assertEqual("TypeError", summary["empty_rows"]["type"])
        self.assertEqual("KeyError", summary["missing_generators"]["type"])
        self.assertEqual(11, len(summary["ragged_monthly_truncation"]["total_monthly"]))
        per_area = decode_number(summary["per_area"]["total_annual"])
        gross = decode_number(summary["gross"]["total_annual"])
        self.assertEqual(round(per_area * 12.5, 2), gross)
        self.assertLess(decode_number(summary["negative_area_gross"]["total_annual"]), 0)

        dictionary = self.facts(value, "J01")
        self.assertEqual(
            [
                "building",
                "constants",
                "site_uses",
                "source_uses",
                "co2",
                "cost",
                "summary_per_area",
                "summary_gross",
            ],
            dictionary["root_order"],
        )
        self.assertEqual(4, decode_number(dictionary["site_call_count"]))
        self.assertEqual("ValueError", dictionary["site_failure_propagation"]["type"])
        self.assertEqual(
            ["site_uses", "source_uses", "co2", "cost"],
            dictionary["summary_metric_order"],
        )
        self.assertEqual(
            321.5,
            decode_number(dictionary["full_tree"]["building"]["total_area"]),
        )

        write = self.facts(value, "J02")
        self.assertTrue(write["returned_none"])
        self.assertFalse(write["first"]["bom"])
        self.assertFalse(write["first"]["ends_with_newline"])
        self.assertTrue(write["first"]["series_compacted"])
        self.assertTrue(write["first"]["nested_inner_lists_compacted"])
        self.assertTrue(write["first"]["bracket_text_whitespace_collapsed"])
        self.assertTrue(write["overwrite"]["changed"])
        self.assertEqual("FileNotFoundError", write["missing_parent"]["type"])
        self.assertEqual("PermissionError", write["directory_target"]["type"])

    def test_resealed_fact_contract_route_receipt_and_order_tampering_fail_closed(self) -> None:
        original = self.fixture()

        fact = copy.deepcopy(original)
        self.facts(fact, "D01")["energy"].append({"kind": "int", "value": "999"})
        self.reseal(fact)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(fact)

        classification = copy.deepcopy(original)
        classification["consumer_contract"]["classifications"]["GreenRetrofitResult.area"] = "exception"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(classification)

        route = copy.deepcopy(original)
        route["consumer_contract"]["native_route_audit"]["from_sqlite"]["status"] = "equivalent-output-route"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(route)

        receipt = copy.deepcopy(original)
        receipt["target_receipts"][0]["inventory_index"] = 0
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(receipt)

        native = copy.deepcopy(original)
        native["native_review"]["source_receipts"][0]["bytes"] = 1
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(native)

        adjacent = copy.deepcopy(original)
        adjacent["upstream"]["adjacent_receipts_sha256"] = "sha256:" + "0" * 64
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(adjacent)

        order = copy.deepcopy(original)
        order["cases"][0], order["cases"][1] = order["cases"][1], order["cases"][0]
        self.reseal(order)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(order)

    def test_strict_json_unsafe_values_duplicates_and_inventory_tampering_fail_closed(self) -> None:
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
            prefix="epsimple-model-result-tamper-", dir=TEST_TEMP_ROOT
        ) as temporary:
            inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
            inventory["symbols"][373]["symbol_hash"] = "sha256:" + "0" * 64
            path = Path(temporary) / "inventory.json"
            path.write_text(json.dumps(inventory), encoding="utf-8", newline="\n")
            with self.assertRaises(SystemExit):
                generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)


if __name__ == "__main__":
    unittest.main()

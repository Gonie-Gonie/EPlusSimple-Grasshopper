"""Fail-closed tests for the dragon HVAC appender/controller oracle."""

from __future__ import annotations

from collections import Counter
import copy
import hashlib
import importlib.util
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
    / "generate_dragon_hvac_appenders_controllers_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-hvac-appenders-controllers-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

specification = importlib.util.spec_from_file_location(
    "generate_dragon_hvac_appenders_controllers_oracle", GENERATOR_PATH
)
if specification is None or specification.loader is None:
    raise RuntimeError(f"Cannot load appender/controller generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(specification)
specification.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 77_246
EXPECTED_GENERATOR_SHA256 = (
    "sha256:357763c4c73e48db275833ab884bf550ea5e143126f550520e9a748bb17154d6"
)
EXPECTED_FIXTURE_BYTES = 178_786
EXPECTED_FIXTURE_SHA256 = (
    "sha256:24b6994b1a39aa363fb0127ea6bfd93bcd12c803768e04f634ed615f08f815eb"
)


class DragonHvacAppendersControllersOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one appender/controller case {code}.")
        return matches[0]

    @staticmethod
    def scalar(value: dict[str, object]) -> object:
        kind = value.get("kind")
        if kind == "none":
            return None
        if kind == "bool":
            return value["value"]
        if kind == "int":
            return int(value["value"])
        if kind == "str":
            return value["value"]
        if kind == "float":
            result = float.fromhex(value["hex"])
            if not math.isfinite(result) or repr(result) != value["repr"]:
                raise AssertionError("Canonical finite float drifted.")
            return result
        raise AssertionError(f"Unsupported encoded scalar: {value!r}")

    @classmethod
    def fields(cls, value: dict[str, object]) -> dict[str, object]:
        return {
            item["name"]: cls.scalar(item["value"])
            for item in value["fields"]
        }

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
                "resolved_support_receipts",
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
            prefix="dragon-hvac-appenders-controllers-regeneration-",
            dir=TEST_TEMP_ROOT,
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

    def test_inventory_has_exact_24_target_1_support_149_deferred_partition(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_targets = [
            *range(686, 693),
            *range(717, 720),
            *range(774, 777),
            *range(804, 815),
        ]
        self.assertEqual(expected_targets, list(generator.TARGET_INDICES))
        self.assertEqual([796], list(generator.RESOLVED_SUPPORT_INDICES))
        self.assertEqual(149, len(generator.DEFERRED_INDICES))
        self.assertFalse(set(generator.TARGET_INDICES) & {796})
        self.assertEqual(
            list(range(641, 815)),
            sorted(
                (
                    *generator.TARGET_INDICES,
                    *generator.RESOLVED_SUPPORT_INDICES,
                    *generator.DEFERRED_INDICES,
                )
            ),
        )
        self.assertEqual(24, len(inventory["target_receipts"]))
        self.assertEqual(1, len(inventory["resolved_support_receipts"]))
        self.assertEqual(
            generator.EXPECTED_TARGET_RECEIPTS_SHA256,
            generator.canonical_sha256(value["target_receipts"]),
        )
        self.assertEqual(
            generator.EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256,
            generator.canonical_sha256(value["resolved_support_receipts"]),
        )
        closure = value["consumer_contract"]["closure"]
        self.assertTrue(closure["exact_disjoint_source_partition"])
        self.assertTrue(closure["full_hvac_source_partition"])
        self.assertTrue(closure["exact_one_case_target_partition"])
        self.assertFalse(closure["target_support_overlap"])
        self.assertEqual(174, closure["source_declaration_count"])
        self.assertEqual(24, closure["target_count"])
        self.assertEqual(1, closure["resolved_support_count"])
        self.assertEqual(149, closure["deferred_count"])
        self.assertEqual(
            generator.EXPECTED_DEFERRED_RECEIPTS_SHA256,
            closure["deferred_receipts_sha256"],
        )
        self.assertEqual(
            generator.EXPECTED_FULL_SOURCE_RECEIPTS_SHA256,
            closure["full_source_receipts_sha256"],
        )

    def test_contract_is_exactly_24_conservative_public_route_exceptions(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"exception": 24}), Counter(contract["classifications"].values())
        )
        self.assertEqual({"equivalent": 0, "exception": 24}, contract["classification_counts"])
        self.assertEqual(24, len(contract["assertion_ids"]))
        self.assertEqual(24, len(set(contract["assertion_ids"].values())))
        self.assertEqual(24, len(contract["coverage_by_symbol"]))
        self.assertEqual(
            Counter({symbol: 1 for symbol in generator.TARGET_SYMBOLS}),
            Counter(
                symbol
                for case in self.fixture()["cases"]
                for symbol in case["target_symbols"]
            ),
        )
        for symbol, expectation in contract["expectations"].items():
            self.assertEqual("exception", expectation["classification"], symbol)
            self.assertEqual(generator.PUBLIC_NATIVE_ROUTE, expectation["native_route"])
            self.assertEqual(generator.ADAPTATIONS[symbol], expectation["adaptation"])
            for forbidden in ("SupplyIdfFragment", "EnergyModelIdfAssembler", ".Generate"):
                self.assertNotIn(forbidden, expectation["native_route"])
        evidence = contract["evidence_contract"]
        self.assertTrue(evidence["idf_objects_are_bounded_instrumented_stubs"])
        self.assertTrue(evidence["resolved_index_796_reused_from_support"])
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertFalse(evidence["native_runtime_executed_by_python_oracle"])
        self.assertFalse(evidence["internal_native_route_claim"])

    def test_supply_core_index_796_support_is_immutable_and_unpromoted(self) -> None:
        value = self.fixture()
        support = value["support"]
        self.assertEqual(
            generator._support_receipt(value["resolved_support_receipts"]), support
        )
        self.assertEqual(9, support["case_count"])
        self.assertEqual(generator.SUPPORT.SCHEMA, support["schema"])
        self.assertEqual(generator.EXPECTED_SUPPORT_CASES_SHA256, support["cases_sha256"])
        self.assertEqual(
            "immutable-index-796-supply-group-conversion-support-only",
            support["role"],
        )
        self.assertFalse(support["target_promoted"])
        self.assertEqual(
            [(796, "SupplyGroup.to_idf_object")],
            [
                (item["inventory_index"], item["symbol"])
                for item in support["resolved_receipts"]
            ],
        )
        self.assertNotIn("SupplyGroup.to_idf_object", generator.TARGET_SYMBOLS)

    def test_runtime_dependency_relocation_and_native_pins_are_exact(self) -> None:
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
        self.assertEqual(24, len(signatures))
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(signatures),
        )
        isolated = value["upstream"]["isolated_import"]
        self.assertEqual(2, isolated["source_location_count"])
        self.assertEqual(
            "two-byte-identical-repository-temp-copies",
            isolated["relocated_source_copy"],
        )
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
        self.assertFalse(review["internal_generate_route_claimed"])
        self.assertFalse(review["internal_postprocessor_type_route_claimed"])
        self.assertEqual(5, len(review["source_receipts"]))
        self.assertEqual(
            generator.EXPECTED_NATIVE_REVIEW_SHA256,
            generator.canonical_sha256(review),
        )

    def test_postprocessor_abstract_constructor_source_and_run_are_exact(self) -> None:
        facts = self.facts("D01")
        self.assertTrue(facts["class_shape"]["abstract"])
        self.assertEqual(["run"], facts["class_shape"]["abstract_methods"])
        self.assertTrue(facts["constructor_final_marker"])
        self.assertEqual("TypeError", facts["direct_instantiation"]["type"])
        self.assertEqual("returned", facts["abstract_run_body"]["outcome"])
        self.assertIsNone(self.scalar(facts["abstract_run_body"]["value"]))
        probe = facts["probe"]
        self.assertFalse(probe["class_shape"]["abstract"])
        self.assertTrue(probe["supply_alias_preserved"])
        self.assertTrue(probe["zone_alias_preserved"])
        self.assertTrue(probe["source_identity_before_mutation"])
        self.assertTrue(probe["source_dynamic_after_supply_mutation"])
        self.assertTrue(probe["run_argument_identity_preserved"])
        self.assertIsNone(self.scalar(probe["run_return"]))

    def test_demand_branch_counts_append_order_failure_prefix_and_rerun_are_exact(self) -> None:
        facts = self.facts("A01")
        self.assertEqual(
            {
                "branchlist_two_nonnull": 2,
                "connector_extra_nonbranch_is_counted": 2,
                "connector_one": 1,
                "connector_zero": 0,
            },
            facts["count_probes"],
        )
        first = facts["run_and_rerun"]["first_events"]
        self.assertEqual(
            [
                "Outlet Branch 2 Name",
                "Inlet Branch 2 Name",
                "Branch 3 Name",
                "Branch 4 Name",
            ],
            [item["field"] for item in first],
        )
        second = facts["run_and_rerun"]["second_events"]
        self.assertEqual(
            [
                "Outlet Branch 3 Name",
                "Inlet Branch 3 Name",
                "Branch 4 Name",
                "Branch 5 Name",
            ],
            [item["field"] for item in second],
        )
        second_branch = self.fields(
            facts["run_and_rerun"]["second_state"]["BranchList"][0]
        )
        self.assertEqual("Demand Inlet", second_branch["Branch 1 Name"])
        self.assertEqual("Demand Bypass", second_branch["Branch 2 Name"])
        self.assertEqual(
            "DemandBranch_Demand Supply_for_Demand Zone",
            second_branch["Branch 3 Name"],
        )
        self.assertEqual(
            "DemandBranch_Demand Supply_for_Demand Zone",
            second_branch["Branch 4 Name"],
        )
        self.assertEqual("Demand Outlet", second_branch["Branch 5 Name"])
        failure = facts["failure_prefix_missing_mixer"]
        self.assertEqual("KeyError", failure["outcome"]["type"])
        self.assertEqual(1, len(failure["events"]))
        self.assertEqual("Outlet Branch 2 Name", failure["events"][0]["field"])
        standalone = facts["standalone_methods"]
        self.assertEqual(4, len(standalone["events"]))
        self.assertTrue(all(self.scalar(item) is None for item in standalone["returns"]))

    def test_equipment_count_hole_99_limit_and_rerun_are_exact(self) -> None:
        facts = self.facts("B01")
        counts = {key: self.scalar(value) for key, value in facts["count_probes"].items()}
        self.assertEqual(
            {
                "empty": 0,
                "first_hole_stops_scan": 1,
                "full_98": 98,
                "full_99_falls_through": None,
                "one": 1,
            },
            counts,
        )
        first = facts["run_and_rerun"]["first_events"]
        second = facts["run_and_rerun"]["second_events"]
        self.assertEqual(4, len(first))
        self.assertEqual(4, len(second))
        self.assertTrue(all("Zone Equipment 2" in item["field"] for item in first))
        self.assertTrue(all("Zone Equipment 3" in item["field"] for item in second))
        hole = self.fields(facts["run_first_hole_overwrites_slot"]["state"])
        self.assertEqual("First", hole["Zone Equipment 1 Name"])
        self.assertEqual(
            "Equipment Supply_for_Equipment Zone", hole["Zone Equipment 2 Name"]
        )
        self.assertEqual("Third", hole["Zone Equipment 3 Name"])
        limit = facts["ninety_nine_limit"]
        self.assertEqual("TypeError", limit["outcome"]["type"])
        self.assertEqual([], limit["events"])
        self.assertTrue(limit["state_unchanged"])
        self.assertEqual("KeyError", facts["missing_equipment_list"]["type"])

    def test_zone_air_node_absent_existing_missing_and_rerun_are_exact(self) -> None:
        facts = self.facts("E01")
        self.assertEqual({"two": 2, "zero_none_ignored": 0}, facts["count_probes"])
        missing_lists = facts["missing_lists_run_and_rerun"]
        self.assertEqual(
            [
                "collection.append",
                "field.set",
                "collection.append",
                "field.set",
                "field.set",
                "field.set",
            ],
            [item["event"] for item in missing_lists["first_events"]],
        )
        self.assertEqual(
            ["Node 2 Name", "Node 2 Name"],
            [item["field"] for item in missing_lists["second_events"]],
        )
        self.assertEqual(2, len(missing_lists["second_state"]["nodelists"]))
        existing = facts["existing_lists"]
        connection = self.fields(existing["connection_unchanged"])
        self.assertEqual("Authored Inlet", connection["Zone Air Inlet Node or NodeList Name"])
        self.assertEqual("Authored Exhaust", connection["Zone Air Exhaust Node or NodeList Name"])
        self.assertEqual(["field.set", "field.set"], [item["event"] for item in existing["events"]])
        failure = facts["missing_connection_failure_prefix"]
        self.assertEqual("IndexError", failure["outcome"]["type"])
        self.assertEqual(["collection.append"], [item["event"] for item in failure["events"]])
        self.assertEqual(1, len(failure["nodelists"]))

    def test_zone_terminal_count_missing_and_rerun_are_exact(self) -> None:
        facts = self.facts("F01")
        self.assertEqual(
            {"existing_one": 1, "three_after_rerun": 3, "zero_none_ignored": 0},
            facts["count_probes"],
        )
        run = facts["run_and_rerun"]
        self.assertEqual(["Zone Terminal Unit Name 2"], [item["field"] for item in run["first_events"]])
        self.assertEqual(["Zone Terminal Unit Name 3"], [item["field"] for item in run["second_events"]])
        final = self.fields(run["second_state"])
        self.assertEqual("Existing Terminal", final["Zone Terminal Unit Name 1"])
        self.assertEqual(
            "Terminal Supply_for_Terminal Zone",
            final["Zone Terminal Unit Name 2"],
        )
        self.assertEqual(final["Zone Terminal Unit Name 2"], final["Zone Terminal Unit Name 3"])
        self.assertEqual("IndexError", facts["missing_list"]["outcome"]["type"])
        self.assertEqual([], facts["missing_list"]["events"])

    def test_sequential_lookup_zero_one_multi_epsilon_failure_and_rerun_are_exact(self) -> None:
        facts = self.facts("C01")
        self.assertEqual(2, facts["find_target"]["found_second"])
        self.assertEqual("ValueError", facts["find_target"]["missing_at_first_empty"]["type"])
        self.assertEqual(
            "Cannot find 'Gamma' in 'EquipmentList_for_Lookup Zone'.",
            facts["find_target"]["missing_at_first_empty"]["message"],
        )
        self.assertEqual("ValueError", facts["find_target"]["overflow_after_99"]["type"])
        self.assertEqual(1.0e-10, self.scalar(facts["epsilon"]))

        zero = facts["zero_active"]
        self.assertEqual([], zero["appended_schedules"])
        zero_fields = {
            item["name"]: self.scalar(item["value"])
            for item in zero["equipment_fields_final"]
        }
        self.assertEqual(
            "ALLOFF",
            zero_fields["Zone Equipment 1 Sequential Heating Fraction Schedule Name"],
        )
        self.assertEqual(
            "ALLOFF",
            zero_fields["Zone Equipment 1 Sequential Cooling Fraction Schedule Name"],
        )

        one = facts["one_active"]
        self.assertEqual(1, one["first_append_count"])
        self.assertEqual(2, len(one["appended_schedules"]))
        one_names = [self.fields(item)["Name"] for item in one["appended_schedules"]]
        self.assertEqual(
            ["heating_fraction_for_Heat Only_for_Fraction Zone"] * 2,
            one_names,
        )
        trace_events = [item["event"] for item in one["schedule_trace"]]
        self.assertIn("schedule.changetype", trace_events)
        self.assertEqual(2, trace_events.count("schedule.to_idf_object"))

        multi = facts["multi_active"]
        self.assertEqual(4, multi["first_append_count"])
        self.assertEqual(8, len(multi["appended_schedules"]))
        first_four = [self.fields(item) for item in multi["appended_schedules"][:4]]
        self.assertEqual(
            [
                "heating_fraction_for_Dual_for_Fraction Zone",
                "heating_fraction_for_Heat_for_Fraction Zone",
                "cooling_fraction_for_Dual_for_Fraction Zone",
                "cooling_fraction_for_Cool_for_Fraction Zone",
            ],
            [item["Name"] for item in first_four],
        )
        self.assertEqual(
            [
                0.249999999975,
                0.9999999998666667,
                0.199999999984,
                0.9999999999,
            ],
            [item["Observed Value"] for item in first_four],
        )
        self.assertEqual(4, sum(item["event"] == "idf.append" for item in multi["first_events"]))
        self.assertEqual(4, sum(item["event"] == "idf.append" for item in multi["rerun_events"]))

        failure = facts["partial_failure_after_schedule_append"]
        self.assertEqual("ValueError", failure["first_outcome"]["type"])
        self.assertEqual(2, failure["first_append_count"])
        self.assertEqual(2, len(failure["appended_schedules"]))
        failure_fields = {
            item["name"]: self.scalar(item["value"])
            for item in failure["equipment_fields_after_first"]
        }
        self.assertEqual(
            "heating_fraction_for_Present_for_Fraction Zone",
            failure_fields["Zone Equipment 1 Sequential Heating Fraction Schedule Name"],
        )
        self.assertEqual(
            "ALLOFF",
            failure_fields["Zone Equipment 1 Sequential Cooling Fraction Schedule Name"],
        )

    def test_validation_rejects_resealed_fact_contract_receipt_and_support_drift(self) -> None:
        changed = self.changed_fixture()
        changed["cases"][0]["python"]["facts"]["count_probes"]["connector_zero"] = 9
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(changed)

        resealed = self.changed_fixture()
        resealed["cases"][0]["python"]["facts"]["count_probes"]["connector_zero"] = 9
        self.reseal(resealed)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(resealed)

        classification = self.changed_fixture()
        classification["consumer_contract"]["classifications"]["DemandBranchAppender"] = "equivalent"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(classification)

        route = self.changed_fixture()
        route["consumer_contract"]["native_routes"]["EquipmentListAppender"] = "Internal.Generate"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(route)

        receipt = self.changed_fixture()
        receipt["target_receipts"][0]["symbol_hash"] = "sha256:" + "0" * 64
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(receipt)

        support = self.changed_fixture()
        support["resolved_support_receipts"][0]["inventory_index"] = 795
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(support)

        closure = self.changed_fixture()
        closure["consumer_contract"]["closure"]["deferred_indices"][0] = 686
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(closure)

    def test_duplicate_keys_paths_addresses_nonfinite_and_noncanonical_fail_closed(self) -> None:
        with self.assertRaises(ValueError):
            generator.load_json_without_duplicates_text('{"x": 1, "x": 2}')
        raw = FIXTURE_PATH.read_text(encoding="utf-8")
        self.assertNotIn(str(REPOSITORY_ROOT), raw)
        self.assertNotIn("C:\\", raw)
        self.assertNotRegex(raw, r"0x[0-9a-fA-F]{8,}")

        nonfinite = self.changed_fixture()
        nonfinite["cases"][0]["python"]["facts"]["unsafe"] = float("nan")
        with self.assertRaises(ValueError):
            generator.strict_json_dumps(nonfinite)

        path = self.changed_fixture()
        path["cases"][0]["python"]["facts"]["unsafe"] = "C:\\host\\leak"
        self.reseal(path)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(path)

        noncanonical = self.changed_fixture()
        noncanonical["runtime"]["python_version"] = "3.12.8"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(noncanonical)


if __name__ == "__main__":
    unittest.main()

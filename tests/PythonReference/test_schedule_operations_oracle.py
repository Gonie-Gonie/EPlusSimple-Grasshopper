"""Fail-closed tests for the annual Schedule operations oracle generator."""

from __future__ import annotations

from collections import Counter
import copy
import importlib.util
import json
import math
from pathlib import Path
import shutil
import unittest
import uuid


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = (
    REPOSITORY_ROOT
    / "tools"
    / "python-reference"
    / "generate_schedule_operations_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "schedule_operations_oracle_generator",
    GENERATOR_PATH,
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class ScheduleOperationsOracleGeneratorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        root = REPOSITORY_ROOT / "temp" / "python-reference-tests"
        root.mkdir(parents=True, exist_ok=True)
        cls.temp_root = root / str(uuid.uuid4())
        cls.temp_root.mkdir()

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.temp_root)

    def write_inventory(self, name: str, value: object) -> Path:
        path = self.temp_root / name
        path.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        return path

    def test_exact_inventory_binds_all_twenty_eight_symbols_and_profile_source(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH,
            generator.EXPECTED_UPSTREAM_COMMIT,
        )

        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"])
        self.assertEqual(
            list(generator.TARGET_SYMBOLS),
            [item["symbol"] for item in inventory["symbols"]],
        )
        self.assertEqual(
            generator.EXPECTED_SYMBOL_HASHES,
            {item["symbol"]: item["symbol_hash"] for item in inventory["symbols"]},
        )

    def test_cases_are_exact_unique_sorted_and_cover_all_symbols(self) -> None:
        cases = generator.case_definitions()
        identifiers = [item["id"] for item in cases]

        self.assertEqual(329, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(generator.EXPECTED_CASE_COUNT, len(cases))
        self.assertEqual(sorted(identifiers), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(set(generator.TARGET_SYMBOLS), {item["symbol"] for item in cases})
        self.assertTrue(all(item["inputs"] for item in cases))
        self.assertTrue(all(item["repair_reference"] is False for item in cases))
        generator.strict_json_dumps(cases)

    def test_cardinality_promotes_rule_corpus_except_noncontract_scalar_extrema(self) -> None:
        actual = Counter(item["symbol"] for item in generator.case_definitions())
        expected = Counter(
            item["symbol"].replace("RuleSet.", "Schedule.", 1)
            for item in generator.RULE.case_definitions()
            if not item["repair_reference"]
        )
        expected["Schedule.where"] += 19

        self.assertEqual(expected, actual)
        self.assertEqual(5, actual["Schedule.element_min"])
        self.assertEqual(5, actual["Schedule.element_max"])
        self.assertEqual(50, actual["Schedule.where"])

    def test_annual_temporal_topologies_are_asymmetric_and_cover_every_day(self) -> None:
        left = generator._temporal_ranges("real-left")
        right = generator._temporal_ranges("real-right")
        condition = generator._temporal_ranges("condition")
        condition_all_false = generator._temporal_ranges("condition-all-false")
        if_true = generator._temporal_ranges("where-true")
        if_false = generator._temporal_ranges("where-false")

        self.assertEqual(((0, 89), (90, 239), (240, 364)), left)
        self.assertEqual(((0, 44), (45, 179), (180, 289), (290, 364)), right)
        self.assertEqual(((0, 119), (120, 249), (250, 364)), condition)
        self.assertEqual(condition, condition_all_false)
        self.assertEqual(((0, 59), (60, 199), (200, 364)), if_true)
        self.assertEqual(((0, 149), (150, 299), (300, 364)), if_false)
        self.assertEqual(365, sum(end - start + 1 for start, end in left))
        self.assertEqual(365, sum(end - start + 1 for start, end in right))

        binary_boundaries = sorted(
            {start for start, _ in left + right}
            | {end + 1 for _, end in left + right}
        )
        where_boundaries = sorted(
            {start for ranges in (condition, if_true, if_false) for start, _ in ranges}
            | {end + 1 for ranges in (condition, if_true, if_false) for _, end in ranges}
        )
        self.assertEqual([0, 45, 90, 180, 240, 290, 365], binary_boundaries)
        self.assertEqual([0, 60, 120, 150, 200, 250, 300, 365], where_boundaries)

    def test_binary_and_scalar_specs_retain_distinct_dispatch_shapes(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        binary = cases["arithmetic.add.schedule.real-real"]
        scalar = cases["arithmetic.add.scalar.real"]

        self.assertEqual(("schedule", "real-left"), binary["inputs"]["receiver"])
        self.assertEqual(("schedule", "real-right"), binary["inputs"]["other"])
        self.assertEqual(("schedule", "real-left"), scalar["inputs"]["receiver"])
        self.assertEqual("scalar", scalar["inputs"]["other"][0])

    def test_where_covers_schedule_ruleset_day_and_scalar_branches(self) -> None:
        cases = {
            item["id"]: item
            for item in generator.case_definitions()
            if item["symbol"] == "Schedule.where"
        }
        required = {
            "where.all-plain.schedule-schedule",
            "where.branch.day-ruleset.inferred",
            "where.branch.ruleset-day.inferred",
            "where.branch.ruleset-ruleset.inferred",
            "where.branch.ruleset-schedule.inferred",
            "where.branch.schedule-ruleset.inferred",
            "where.branch.scalar-ruleset.explicit-fraction",
            "where.bool.selected-false-value",
            "where.bool.selected-true-value",
            "where.day-day.inferred",
            "where.schedule-schedule.inferred",
            "where.scalar-scalar.inferred-real",
        }
        self.assertLessEqual(required, set(cases))
        kinds = {
            kind
            for item in cases.values()
            for kind, _ in item["inputs"].values()
        }
        self.assertLessEqual(
            {"day-schedule", "ruleset", "scalar", "schedule", "schedule-type"},
            kinds,
        )
        self.assertTrue(
            all(item["inputs"]["condition"][0] == "schedule" for item in cases.values())
        )

    def test_where_nonfinite_matrix_is_complete_and_selection_bound(self) -> None:
        cases = {
            item["id"]: item
            for item in generator.case_definitions()
            if item["id"].startswith("nonfinite.where.")
        }
        self.assertEqual(generator.EXPECTED_WHERE_NONFINITE_MATRIX_IDS, set(cases))
        self.assertEqual(12, len(cases))

        for selection in ("selected", "unselected"):
            for branch in ("false", "true"):
                for token in generator.NONFINITE_TOKENS:
                    identifier = f"nonfinite.where.{selection}-{branch}.{token}"
                    case = cases[identifier]
                    selected = selection == "selected"
                    true_branch = branch == "true"
                    condition = (
                        "condition-all-true"
                        if selected == true_branch
                        else "condition-all-false"
                    )
                    nonfinite_branch = "if_true" if true_branch else "if_false"
                    finite_branch = "if_false" if true_branch else "if_true"
                    self.assertEqual(("schedule", condition), case["inputs"]["condition"])
                    self.assertEqual(
                        ("nonfinite", token),
                        case["inputs"][nonfinite_branch],
                    )
                    self.assertEqual(("scalar", 0), case["inputs"][finite_branch])
                    self.assertIsNone(case["expected_exception"])
                    expected = {
                        "adaptation": generator.WHERE_ADAPTATION,
                        "outcome": "raised" if selected else "returned",
                        "policy": (
                            "reject-nonfinite-result"
                            if selected
                            else "deterministic-slot-names"
                        ),
                    }
                    if selected:
                        expected["error_category"] = "domain"
                    self.assertEqual(expected, case["expected_dotnet"])

        self.assertEqual(
            6,
            sum(
                item["expected_dotnet"]["outcome"] == "raised"
                for item in cases.values()
            ),
        )
        self.assertEqual(
            6,
            sum(
                item["expected_dotnet"]["outcome"] == "returned"
                for item in cases.values()
            ),
        )

    def test_where_bool_cases_preserve_python_kind_name_and_selected_value(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}

        class FakeDaySchedule:
            pass

        class FakeRuleSet:
            pass

        class FakeSchedule:
            pass

        class FakeScheduleType:
            pass

        expected = {
            "where.bool.selected-false-value": (False, "WhereBoolFalse"),
            "where.bool.selected-true-value": (True, "WhereBoolTrue"),
        }
        self.assertEqual(generator.EXPECTED_WHERE_BOOL_CASE_IDS, set(expected))
        for identifier, (value, name) in expected.items():
            case = cases[identifier]
            self.assertEqual(
                ("schedule", "condition-all-true"),
                case["inputs"]["condition"],
            )
            self.assertEqual("scalar", case["inputs"]["if_true"][0])
            self.assertIs(type(case["inputs"]["if_true"][1]), bool)
            self.assertIs(value, case["inputs"]["if_true"][1])
            self.assertEqual(("text", name), case["inputs"]["name"])
            self.assertEqual(
                {
                    "kind": "scalar",
                    "python_type": "bool",
                    "value": value,
                },
                generator.input_descriptor(
                    value,
                    FakeDaySchedule,
                    FakeRuleSet,
                    FakeSchedule,
                    FakeScheduleType,
                ),
            )
            self.assertEqual(
                {
                    "adaptation": generator.WHERE_ADAPTATION,
                    "outcome": "returned",
                    "policy": "deterministic-slot-names",
                },
                case["expected_dotnet"],
            )

    def test_where_mixed_schedule_ruleset_pairs_cover_both_asymmetric_directions(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        schedule_ruleset = cases["where.branch.schedule-ruleset.inferred"]
        ruleset_schedule = cases["where.branch.ruleset-schedule.inferred"]

        self.assertEqual(
            ("schedule", "where-true"),
            schedule_ruleset["inputs"]["if_true"],
        )
        self.assertEqual(
            ("ruleset", "where-false"),
            schedule_ruleset["inputs"]["if_false"],
        )
        self.assertEqual(
            ("ruleset", "where-true"),
            ruleset_schedule["inputs"]["if_true"],
        )
        self.assertEqual(
            ("schedule", "where-false"),
            ruleset_schedule["inputs"]["if_false"],
        )
        self.assertNotEqual(
            generator._temporal_ranges("where-true"),
            generator._temporal_ranges("where-false"),
        )
        self.assertEqual(
            frozenset({"tuesday", "friday", "sunday"}),
            generator.RULE._topology_for_template("where-true", None),
        )
        self.assertEqual(
            frozenset({"wednesday", "saturday", "holiday"}),
            generator.RULE._topology_for_template("where-false", None),
        )
        self.assertTrue(
            all(
                item["expected_dotnet"]
                == {
                    "adaptation": generator.WHERE_ADAPTATION,
                    "outcome": "returned",
                    "policy": "deterministic-period-child-names",
                }
                for item in (schedule_ruleset, ruleset_schedule)
            )
        )

    def test_where_pins_invalid_nonfinite_type_and_name_boundaries(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        self.assertEqual(
            "OverflowError",
            cases["large-int.unbounded.where.selected-real-overflow-error"][
                "expected_exception"
            ],
        )
        self.assertEqual(
            "ScheduleOperationError",
            cases["where.error.mixed-schedule-types"]["expected_exception"],
        )
        self.assertEqual(
            {
                "adaptation": generator.WHERE_ADAPTATION,
                "error_category": "type",
                "outcome": "raised",
                "policy": "reject-invalid-name",
            },
            cases["where.name.whitespace-native-invalid"]["expected_dotnet"],
        )
        self.assertEqual(
            {
                "adaptation": generator.WHERE_ADAPTATION,
                "outcome": "returned",
                "policy": "trim-name-and-deterministic-slot-names",
                "result_name": "Selected",
            },
            cases["where.name.surrounding-whitespace-trimmed"]["expected_dotnet"],
        )

    def test_normalize_pins_annual_name_type_and_nonfinite_boundaries(self) -> None:
        cases = {
            item["id"]: item
            for item in generator.case_definitions()
            if item["symbol"] == "Schedule.normalize_by_max"
        }
        self.assertEqual(11, len(cases))
        self.assertTrue(
            all(set(item["inputs"]) == {"new_name", "receiver"} for item in cases.values())
        )
        self.assertEqual(
            generator.NORMALIZE_ADAPTATION,
            cases["normalize.copy.finite-input-negative-infinity"]["expected_dotnet"][
                "adaptation"
            ],
        )
        self.assertEqual(
            "trim-result-name",
            cases["normalize.name.surrounding-whitespace-trimmed"]["expected_dotnet"][
                "policy"
            ],
        )
        self.assertEqual(
            "reject-invalid-name",
            cases["normalize.name.whitespace-native-invalid"]["expected_dotnet"][
                "policy"
            ],
        )

    def test_static_adaptations_are_exact_and_schedule_symbol_bound(self) -> None:
        adapted = [
            item["expected_dotnet"]
            for item in generator.case_definitions()
            if item["expected_dotnet"] is not None
        ]
        counts = Counter(item["adaptation"] for item in adapted)

        self.assertEqual(56, len(adapted))
        self.assertEqual(set(generator.EXPECTED_ADAPTATION_IDS), set(counts))
        self.assertEqual(36, counts[generator.WHERE_ADAPTATION])
        self.assertEqual(4, counts[generator.NORMALIZE_ADAPTATION])
        for adaptation in generator.ARITHMETIC_NONFINITE_ADAPTATIONS.values():
            self.assertEqual(2, counts[adaptation])
        self.assertTrue(all("ruleset" not in identifier for identifier in counts))

    def test_dotnet_expectation_invariants_fail_closed(self) -> None:
        scenarios = (
            (
                "raised-requires-error-category",
                "nonfinite.where.selected-true.positive-infinity",
                lambda value: value.pop("error_category"),
                "invalid error category",
            ),
            (
                "returned-forbids-error-category",
                "where.bool.selected-true-value",
                lambda value: value.__setitem__("error_category", "domain"),
                "invalid error category",
            ),
            (
                "policy-outcome-is-bound",
                "where.bool.selected-true-value",
                lambda value: value.update(
                    {"error_category": "domain", "outcome": "raised"}
                ),
                "policy/outcome mismatch",
            ),
            (
                "deterministic-policy-forbids-result-name",
                "where.bool.selected-false-value",
                lambda value: value.__setitem__("result_name", "Unexpected"),
                "unexpected result name",
            ),
            (
                "trim-policy-requires-normalized-result-name",
                "where.name.surrounding-whitespace-trimmed",
                lambda value: value.__setitem__("result_name", "  Selected  "),
                "malformed trimmed name",
            ),
            (
                "unknown-expectation-key-is-rejected",
                "where.bool.selected-true-value",
                lambda value: value.__setitem__("unexpected", "value"),
                "malformed .NET expectation keys",
            ),
        )
        for label, identifier, mutate, message in scenarios:
            with self.subTest(label=label):
                definitions = copy.deepcopy(list(generator.case_definitions()))
                target = next(item for item in definitions if item["id"] == identifier)
                mutate(target["expected_dotnet"])
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_case_definitions(definitions)

    def test_bool_float_large_integer_and_reverse_name_cases_are_preserved(self) -> None:
        cases = generator.case_definitions()
        bool_cases = [
            item
            for item in cases
            if item["id"].startswith("scalar-name.")
            and any(
                kind == "scalar" and type(value) is bool
                for kind, value in item["inputs"].values()
            )
        ]
        self.assertEqual(28, len(bool_cases))
        self.assertEqual(
            {False, True},
            {
                value
                for item in bool_cases
                for kind, value in item["inputs"].values()
                if kind == "scalar" and type(value) is bool
            },
        )
        reverse_symbols = {
            "Schedule.__radd__",
            "Schedule.__rmul__",
            "Schedule.__rsub__",
            "Schedule.__rtruediv__",
        }
        self.assertTrue(all(any(item["symbol"] == symbol for item in cases) for symbol in reverse_symbols))
        self.assertEqual((1 << 53) + 1, generator.INEXACT_BINARY64_INTEGER)
        self.assertEqual(10**400, generator.UNBOUNDED_INTEGER)
        serialized = generator.strict_json_dumps(cases)
        self.assertIn("9007199254740993", serialized)
        self.assertIn('"scalar-name.radd.bool-true"', serialized)
        self.assertNotIn("9007199254740993.0", serialized)

    def test_unbounded_integer_descriptor_is_a_decimal_string(self) -> None:
        class FakeDaySchedule:
            pass

        class FakeRuleSet:
            pass

        class FakeSchedule:
            pass

        class FakeScheduleType:
            pass

        descriptor = generator.input_descriptor(
            generator.UNBOUNDED_INTEGER,
            FakeDaySchedule,
            FakeRuleSet,
            FakeSchedule,
            FakeScheduleType,
        )
        self.assertEqual("scalar", descriptor["kind"])
        self.assertEqual("int", descriptor["python_type"])
        self.assertEqual("decimal-string", descriptor["value"]["kind"])
        self.assertEqual("1" + ("0" * 400), descriptor["value"]["value"])

    def test_nonfinite_values_are_tagged_and_strict_json_rejects_raw_constants(self) -> None:
        expected = {
            math.nan: "nan",
            math.inf: "positive-infinity",
            -math.inf: "negative-infinity",
        }
        for value, token in expected.items():
            with self.subTest(token=token):
                encoded = generator.compact_values([value] * 144, allow_nonfinite=True)
                self.assertEqual(
                    [{"kind": "nonfinite", "value": token}],
                    encoded["pattern"],
                )
                self.assertNotIn("Infinity", generator.strict_json_dumps(encoded))
        with self.assertRaises(ValueError):
            generator.strict_json_dumps({"raw": math.inf})

    def test_runtime_identity_child_names_are_normalized(self) -> None:
        self.assertEqual(
            {"policy": "runtime-identity-hex"},
            generator.RULE._name_descriptor("0xdeadbeef", "runtime-identity-hex"),
        )
        self.assertNotIn(
            "0xdeadbeef",
            generator.strict_json_dumps(
                generator.RULE._name_descriptor("0xdeadbeef", "runtime-identity-hex")
            ),
        )
        with self.assertRaisesRegex(RuntimeError, "raw runtime identity"):
            generator.RULE._name_descriptor("0xdeadbeef", "literal")
        self.assertIsNotNone(
            generator.RAW_AUTO_NAME_PATTERN.search('{"name":"0xdeadbeef"}')
        )

    def test_duplicate_nonfinite_and_tampered_inventory_are_rejected(self) -> None:
        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n',
            encoding="utf-8",
            newline="\n",
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(duplicate, generator.EXPECTED_UPSTREAM_COMMIT)

        nonfinite = self.temp_root / "nonfinite.json"
        nonfinite.write_text(
            '{"schema":Infinity}\n',
            encoding="utf-8",
            newline="\n",
        )
        with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
            generator.load_exact_inventory(nonfinite, generator.EXPECTED_UPSTREAM_COMMIT)

        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        value["content_sha256"] = "sha256:" + ("0" * 64)
        tampered = self.write_inventory("tampered-content-hash.json", value)
        with self.assertRaisesRegex(SystemExit, "content hash is invalid"):
            generator.load_exact_inventory(tampered, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_tampered_source_and_symbol_hashes_fail_closed(self) -> None:
        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        source = next(
            item for item in value["files"] if item["path"] == generator.SOURCE_PATH
        )
        source["content_hash"] = "sha256:" + ("0" * 64)
        value["content_sha256"] = generator.canonical_sha256(
            {
                "files": value["files"],
                "scope_sha256": value["scope_sha256"],
                "symbols": value["symbols"],
                "upstream_commit": value["upstream_commit"],
            }
        )
        tampered_source = self.write_inventory("tampered-source-hash.json", value)
        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(
                tampered_source,
                generator.EXPECTED_UPSTREAM_COMMIT,
            )

        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        symbol = next(
            item for item in value["symbols"] if item["symbol"] == "Schedule.where"
        )
        symbol["symbol_hash"] = "sha256:" + ("0" * 64)
        value["content_sha256"] = generator.canonical_sha256(
            {
                "files": value["files"],
                "scope_sha256": value["scope_sha256"],
                "symbols": value["symbols"],
                "upstream_commit": value["upstream_commit"],
            }
        )
        tampered_symbol = self.write_inventory("tampered-symbol-hash.json", value)
        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(
                tampered_symbol,
                generator.EXPECTED_UPSTREAM_COMMIT,
            )

    def test_wrong_commit_is_rejected_before_schedule_binding(self) -> None:
        with self.assertRaisesRegex(SystemExit, "not the pinned DaySchedule commit"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

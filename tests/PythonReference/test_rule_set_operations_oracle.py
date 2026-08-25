"""Fail-closed tests for the RuleSet operations reference generator."""

from __future__ import annotations

from collections import Counter
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
    / "generate_rule_set_operations_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "rule_set_operations_oracle_generator",
    GENERATOR_PATH,
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class RuleSetOperationsOracleGeneratorTests(unittest.TestCase):
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

        self.assertEqual(334, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(generator.EXPECTED_CASE_COUNT, len(cases))
        self.assertEqual(sorted(identifiers), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(set(generator.TARGET_SYMBOLS), {item["symbol"] for item in cases})
        self.assertTrue(all(item["inputs"] for item in cases))
        generator.strict_json_dumps(cases)

    def test_case_cardinality_preserves_day_corpus_and_adds_where_shapes(self) -> None:
        counts = Counter(item["symbol"] for item in generator.case_definitions())
        self.assertEqual(31, counts.pop("RuleSet.where"))
        self.assertEqual(11, counts.pop("RuleSet.normalize_by_max"))
        expected_day_counts = Counter(
            item["symbol"].replace("DaySchedule.", "RuleSet.", 1)
            for item in generator.DAY.case_definitions()
            if item["symbol"]
            not in {"DaySchedule.normalize_by_max", "DaySchedule.where"}
        )
        self.assertEqual(expected_day_counts, counts)

    def test_asymmetric_topologies_cover_defaults_and_every_override(self) -> None:
        left = generator._topology_for_template("real-left", None)
        right = generator._topology_for_template("real-right", None)
        condition = generator._topology_for_template("condition", None)
        if_true = generator._topology_for_template("where-true", None)
        if_false = generator._topology_for_template("where-false", None)

        self.assertEqual({"monday", "saturday", "holiday"}, set(left))
        self.assertEqual({"tuesday", "sunday"}, set(right))
        self.assertTrue(left - right)
        self.assertTrue(right - left)
        self.assertEqual(set(generator.OVERRIDE_KEYS), set(condition | if_true | if_false))
        self.assertEqual(
            frozenset(),
            generator._topology_for_template("condition", "plain"),
        )
        self.assertEqual(("real-left", "plain"), generator._split_template_name("real-left@plain"))

    def test_scalar_and_binary_topology_specs_are_distinct(self) -> None:
        cases = generator.case_definitions()
        binary = next(item for item in cases if item["id"] == "arithmetic.add.schedule.real-real")
        scalar = next(item for item in cases if item["id"] == "arithmetic.add.scalar.real")

        self.assertEqual("ruleset", binary["inputs"]["receiver"][0])
        self.assertEqual("ruleset", binary["inputs"]["other"][0])
        self.assertEqual("ruleset", scalar["inputs"]["receiver"][0])
        self.assertEqual("scalar", scalar["inputs"]["other"][0])

    def test_normalize_has_no_inplace_input_and_covers_native_name_policy(self) -> None:
        cases = [
            item
            for item in generator.case_definitions()
            if item["symbol"] == "RuleSet.normalize_by_max"
        ]
        self.assertEqual(11, len(cases))
        self.assertTrue(all(set(item["inputs"]) == {"new_name", "receiver"} for item in cases))
        self.assertFalse(any("inplace" in item["id"] for item in cases))
        overflow = next(
            item
            for item in cases
            if item["id"] == "normalize.copy.finite-input-negative-infinity"
        )
        self.assertEqual(
            generator.NORMALIZE_ADAPTATION,
            overflow["expected_dotnet"]["adaptation"],
        )
        invalid_name_cases = {
            item["id"]: item
            for item in cases
            if item["id"]
            in {
                "normalize.name.empty-native-invalid",
                "normalize.name.whitespace-native-invalid",
            }
        }
        self.assertEqual(
            {
                "normalize.name.empty-native-invalid",
                "normalize.name.whitespace-native-invalid",
            },
            set(invalid_name_cases),
        )
        for item in invalid_name_cases.values():
            self.assertIsNone(item["expected_exception"])
            self.assertEqual(
                {
                    "adaptation": generator.NORMALIZE_ADAPTATION,
                    "error_category": "type",
                    "outcome": "raised",
                    "policy": "reject-invalid-name",
                },
                item["expected_dotnet"],
            )
        trimmed = next(
            item
            for item in cases
            if item["id"] == "normalize.name.surrounding-whitespace-trimmed"
        )
        self.assertEqual(("text", "  Normalized  "), trimmed["inputs"]["new_name"])
        self.assertEqual(
            {
                "adaptation": generator.NORMALIZE_ADAPTATION,
                "outcome": "returned",
                "policy": "trim-result-name",
                "result_name": "Normalized",
            },
            trimmed["expected_dotnet"],
        )

    def test_where_covers_ruleset_day_and_scalar_branches(self) -> None:
        cases = {
            item["id"]: item
            for item in generator.case_definitions()
            if item["symbol"] == "RuleSet.where"
        }
        required = {
            "where.all-plain.ruleset-ruleset",
            "where.day-day.inferred",
            "where.day-ruleset.inferred",
            "where.ruleset-day.inferred",
            "where.day-scalar.explicit-fraction",
            "where.scalar-day.explicit-fraction",
            "where.ruleset-ruleset.inferred",
            "where.scalar-scalar.inferred-real",
        }
        self.assertLessEqual(required, set(cases))
        kinds = {
            kind
            for item in cases.values()
            for kind, _ in item["inputs"].values()
        }
        self.assertLessEqual(
            {"day-schedule", "ruleset", "scalar", "schedule-type"},
            kinds,
        )

    def test_where_pins_eager_branch_coercion_and_untyped_scalar_real_behavior(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        expected = {
            "where.unselected-invalid-scalar": "ValueError",
            "large-int.unbounded.where.unselected-fraction-success": "ValueError",
            "where.ruleset-scalar.inferred": "ScheduleOperationError",
            "where.scalar-ruleset.inferred": "ScheduleOperationError",
        }
        self.assertEqual(
            expected,
            {identifier: cases[identifier]["expected_exception"] for identifier in expected},
        )
        self.assertTrue(all(cases[identifier]["expected_dotnet"] is None for identifier in expected))

    def test_where_pins_negative_zero_empty_and_whitespace_name_boundaries(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        negative_zero = cases[
            "where.onoff.selected-negative-zero-normalizes-positive-zero"
        ]
        self.assertEqual(("scalar", -0.0), negative_zero["inputs"]["if_true"])
        self.assertEqual(
            ("schedule-type", "onoff"),
            negative_zero["inputs"]["type"],
        )
        self.assertEqual(
            "deterministic-slot-names",
            negative_zero["expected_dotnet"]["policy"],
        )

        empty = cases["where.name.empty-falls-back-to-where"]
        self.assertEqual(("text", ""), empty["inputs"]["name"])
        self.assertEqual("returned", empty["expected_dotnet"]["outcome"])

        whitespace = cases["where.name.whitespace-native-invalid"]
        self.assertTrue(whitespace["inputs"]["name"][1].isspace())
        self.assertEqual(
            {
                "adaptation": generator.WHERE_ADAPTATION,
                "error_category": "type",
                "outcome": "raised",
                "policy": "reject-invalid-name",
            },
            whitespace["expected_dotnet"],
        )
        surrounding = cases["where.name.surrounding-whitespace-trimmed"]
        self.assertEqual(("text", "  Selected  "), surrounding["inputs"]["name"])
        self.assertEqual(
            {
                "adaptation": generator.WHERE_ADAPTATION,
                "outcome": "returned",
                "policy": "trim-name-and-deterministic-slot-names",
                "result_name": "Selected",
            },
            surrounding["expected_dotnet"],
        )

    def test_static_adaptations_are_exact_before_extrema_repair_execution(self) -> None:
        cases = generator.case_definitions()
        adapted = [item["expected_dotnet"] for item in cases if item["expected_dotnet"]]
        counts = Counter(item["adaptation"] for item in adapted)

        self.assertEqual(37, len(adapted))
        self.assertEqual(17, counts[generator.WHERE_ADAPTATION])
        self.assertEqual(4, counts[generator.NORMALIZE_ADAPTATION])
        for adaptation in generator.ARITHMETIC_NONFINITE_ADAPTATIONS.values():
            self.assertEqual(2, counts[adaptation])
        self.assertFalse(set(generator.SCALAR_EXTREMA_ADAPTATIONS.values()) & set(counts))

    def test_scalar_extrema_are_exact_attribute_error_repair_cases(self) -> None:
        repairs = [item for item in generator.case_definitions() if item["repair_reference"]]
        self.assertEqual(24, len(repairs))
        self.assertEqual(
            {"RuleSet.element_max", "RuleSet.element_min"},
            {item["symbol"] for item in repairs},
        )
        self.assertTrue(all(item["expected_exception"] == "AttributeError" for item in repairs))
        self.assertTrue(all(item["expected_dotnet"] is None for item in repairs))
        self.assertTrue(all(item["inputs"]["other"][0] in {"scalar", "nonfinite"} for item in repairs))

    def test_bool_float_and_large_integer_name_boundaries_are_preserved(self) -> None:
        cases = generator.case_definitions()
        bool_cases = [
            item
            for item in cases
            if item["id"].startswith("scalar-name.")
            and any(kind == "scalar" and type(value) is bool for kind, value in item["inputs"].values())
        ]
        self.assertEqual(32, len(bool_cases))
        self.assertEqual({False, True}, {
            value
            for item in bool_cases
            for kind, value in item["inputs"].values()
            if kind == "scalar" and type(value) is bool
        })
        self.assertEqual((1 << 53) + 1, generator.INEXACT_BINARY64_INTEGER)
        self.assertEqual(10**400, generator.UNBOUNDED_INTEGER)
        serialized = generator.strict_json_dumps(cases)
        self.assertIn("9007199254740993", serialized)
        self.assertNotIn("9007199254740993.0", serialized)

    def test_unbounded_integer_descriptor_is_a_decimal_string(self) -> None:
        class FakeDaySchedule:
            pass

        class FakeRuleSet:
            pass

        class FakeScheduleType:
            pass

        descriptor = generator.input_descriptor(
            generator.UNBOUNDED_INTEGER,
            FakeDaySchedule,
            FakeRuleSet,
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
            generator._name_descriptor("0xdeadbeef", "runtime-identity-hex"),
        )
        self.assertNotIn(
            "0xdeadbeef",
            generator.strict_json_dumps(
                generator._name_descriptor("0xdeadbeef", "runtime-identity-hex")
            ),
        )
        with self.assertRaisesRegex(RuntimeError, "raw runtime identity"):
            generator._name_descriptor("0xdeadbeef", "literal")
        self.assertIsNotNone(
            generator.RAW_AUTO_NAME_PATTERN.search('{"name":"0xdeadbeef"}')
        )

    def test_duplicate_and_tampered_inventory_are_rejected(self) -> None:
        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n',
            encoding="utf-8",
            newline="\n",
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(duplicate, generator.EXPECTED_UPSTREAM_COMMIT)

        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        value["content_sha256"] = "sha256:" + ("0" * 64)
        tampered = self.write_inventory("tampered-content-hash.json", value)
        with self.assertRaisesRegex(SystemExit, "content hash is invalid"):
            generator.load_exact_inventory(tampered, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_wrong_commit_is_rejected_before_rule_set_binding(self) -> None:
        with self.assertRaisesRegex(SystemExit, "not the pinned DaySchedule commit"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

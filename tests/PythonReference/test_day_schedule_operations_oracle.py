"""Fail-closed tests for the DaySchedule operations reference generator."""

from __future__ import annotations

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
    / "generate_day_schedule_operations_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "day_schedule_operations_oracle_generator",
    GENERATOR_PATH,
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class DayScheduleOperationsOracleGeneratorTests(unittest.TestCase):
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

    def test_operation_cases_are_exact_unique_sorted_and_cover_every_symbol(self) -> None:
        cases = generator.case_definitions()
        identifiers = [item["id"] for item in cases]

        self.assertEqual(generator.EXPECTED_CASE_COUNT, len(cases))
        self.assertEqual(sorted(identifiers), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(set(generator.TARGET_SYMBOLS), {item["symbol"] for item in cases})
        self.assertTrue(all(item["inputs"] for item in cases))
        generator.strict_json_dumps(cases)

    def test_bool_scalar_boundaries_cover_all_scalar_name_operations(self) -> None:
        cases = generator.case_definitions()
        bool_cases = [item for item in cases if item["id"].startswith("scalar-name.") and any(
            kind == "scalar" and type(value) is bool
            for kind, value in item["inputs"].values()
        )]

        self.assertEqual(32, len(bool_cases))
        self.assertEqual({False, True}, {
            value
            for item in bool_cases
            for kind, value in item["inputs"].values()
            if kind == "scalar" and type(value) is bool
        })

    def test_inexact_binary64_integer_cases_preserve_exact_python_int_inputs(self) -> None:
        cases = generator.case_definitions()
        large_cases = [item for item in cases if item["id"].startswith("large-int.")]

        self.assertEqual((1 << 53) + 1, generator.INEXACT_BINARY64_INTEGER)
        self.assertEqual(20, len(large_cases))
        self.assertEqual(
            {
                "DaySchedule.__ge__",
                "DaySchedule.__gt__",
                "DaySchedule.__le__",
                "DaySchedule.__lt__",
                "DaySchedule.element_eq",
                "DaySchedule.element_max",
                "DaySchedule.element_min",
                "DaySchedule.element_ne",
                "DaySchedule.is_between",
                "DaySchedule.where",
            },
            {item["symbol"] for item in large_cases},
        )
        exact_inputs = [
            value
            for item in large_cases
            for kind, value in item["inputs"].values()
            if kind == "scalar" and value == generator.INEXACT_BINARY64_INTEGER
        ]
        self.assertTrue(exact_inputs)
        self.assertTrue(all(type(value) is int for value in exact_inputs))
        serialized = generator.strict_json_dumps(large_cases)
        self.assertIn('9007199254740993', serialized)
        self.assertNotIn('9007199254740993.0', serialized)

    def test_unbounded_integer_cases_use_canonical_decimal_string_descriptors(self) -> None:
        cases = generator.case_definitions()
        unbounded = [
            item for item in cases if item["id"].startswith("large-int.unbounded.")
        ]

        self.assertEqual(10**400, generator.UNBOUNDED_INTEGER)
        self.assertEqual(7, len(unbounded))
        self.assertEqual(
            {None: 1, "OverflowError": 1, "ValueError": 5},
            {
                outcome: sum(item["expected_exception"] == outcome for item in unbounded)
                for outcome in (None, "OverflowError", "ValueError")
            },
        )
        self.assertTrue(all(item["expected_dotnet"] is None for item in unbounded))

        class FakeSchedule:
            pass

        class FakeScheduleType:
            pass

        positive = generator.input_descriptor(
            generator.UNBOUNDED_INTEGER,
            FakeSchedule,
            FakeScheduleType,
        )
        negative = generator.input_descriptor(
            -generator.UNBOUNDED_INTEGER,
            FakeSchedule,
            FakeScheduleType,
        )
        adjacent = generator.input_descriptor(
            generator.INEXACT_BINARY64_INTEGER,
            FakeSchedule,
            FakeScheduleType,
        )
        self.assertEqual("scalar", positive["kind"])
        self.assertEqual("int", positive["python_type"])
        self.assertEqual("decimal-string", positive["value"]["kind"])
        self.assertEqual("1" + ("0" * 400), positive["value"]["value"])
        self.assertEqual("-" + positive["value"]["value"], negative["value"]["value"])
        self.assertEqual(generator.INEXACT_BINARY64_INTEGER, adjacent["value"])
        generator.strict_json_dumps({"negative": negative, "positive": positive})

    def test_declared_schedule_patterns_and_scalars_are_finite_typed_values(self) -> None:
        for name, template in generator.SCHEDULE_TEMPLATES.items():
            with self.subTest(template=name):
                self.assertTrue(template["pattern"])
                self.assertEqual(
                    {"name", "pattern", "type", "unit"},
                    set(template),
                )
                for value in template["pattern"]:
                    generator.require_finite_scalar(value, f"template {name}")

        for case in generator.case_definitions():
            has_nonfinite_input = False
            for input_name, (kind, value) in case["inputs"].items():
                if kind == "scalar":
                    with self.subTest(case=case["id"], input=input_name):
                        generator.require_finite_scalar(value, input_name)
                elif kind == "nonfinite":
                    has_nonfinite_input = True
                    self.assertIn(
                        value,
                        {"nan", "negative-infinity", "positive-infinity"},
                    )
            if has_nonfinite_input:
                self.assertIsNotNone(case["expected_dotnet"])

    def test_nonfinite_adaptations_are_exact_and_finite_overflow_is_explicit(self) -> None:
        cases = generator.case_definitions()
        adapted = [item for item in cases if item["expected_dotnet"] is not None]
        finite_overflow = [
            item for item in adapted if item["id"].startswith("overflow.finite-input.")
        ]
        tagged_scalar = [
            item for item in adapted if item["id"].startswith("overflow.tagged-scalar.")
        ]

        self.assertEqual(22, len(adapted))
        self.assertEqual(8, len(finite_overflow))
        self.assertEqual(8, len(tagged_scalar))
        self.assertTrue(all(
            kind != "nonfinite"
            for item in finite_overflow
            for kind, _ in item["inputs"].values()
        ))
        self.assertTrue(all(
            any(kind == "nonfinite" for kind, _ in item["inputs"].values())
            for item in tagged_scalar
        ))
        self.assertEqual(
            set(generator.ARITHMETIC_NONFINITE_ADAPTATIONS.values()),
            {
                item["expected_dotnet"]["adaptation"]
                for item in finite_overflow + tagged_scalar
            },
        )
        self.assertTrue(all(
            item["expected_dotnet"]["outcome"] == "raised"
            for item in adapted
        ))
        normalize_overflow = next(
            item
            for item in adapted
            if item["id"] == "normalize.copy.finite-input-negative-infinity"
        )
        self.assertEqual(
            "immutable-day-schedule-normalize-by-max",
            normalize_overflow["expected_dotnet"]["adaptation"],
        )
        self.assertTrue(all(
            kind != "nonfinite"
            for kind, _ in normalize_overflow["inputs"].values()
        ))

    def test_value_encoding_uses_repeat_only_for_a_shorter_exact_period(self) -> None:
        repeated = generator.compact_values(([0, 1] * 72))
        aperiodic = generator.compact_values(list(range(144)))

        self.assertEqual(
            {"encoding": "repeat", "length": 144, "pattern": [0, 1]},
            repeated,
        )
        self.assertEqual("full", aperiodic["encoding"])
        self.assertEqual(list(range(144)), aperiodic["items"])

    def test_wrong_length_and_nonfinite_observations_are_rejected(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "not length 144"):
            generator.compact_values([0] * 24)
        for value in (math.nan, math.inf, -math.inf):
            with self.subTest(value=value):
                with self.assertRaisesRegex(RuntimeError, "non-finite"):
                    generator.compact_values(([0.0] * 143) + [value])

    def test_nonfinite_result_values_are_tagged_for_strict_json(self) -> None:
        expected = {
            math.nan: "nan",
            math.inf: "positive-infinity",
            -math.inf: "negative-infinity",
        }
        for value, token in expected.items():
            with self.subTest(token=token):
                encoded = generator.compact_values(
                    [value] * 144,
                    allow_nonfinite=True,
                )
                self.assertEqual(
                    [{"kind": "nonfinite", "value": token}],
                    encoded["pattern"],
                )
                serialized = generator.strict_json_dumps(encoded)
                self.assertNotIn(":NaN", serialized)
                self.assertNotIn(":Infinity", serialized)
        with self.assertRaises(ValueError):
            generator.strict_json_dumps({"raw": math.inf})

    def test_runtime_identity_names_are_normalized_and_never_serialized(self) -> None:
        class FakeType:
            value = "real"

        class FakeSchedule:
            def __init__(self, name: str) -> None:
                self.data = [1.0] * 144
                self.name = name
                self.type = FakeType()
                self.unit = None

        normalized = generator.schedule_result_descriptor(
            FakeSchedule("0xdeadbeef"),
            "runtime-identity-hex",
            FakeSchedule,
        )
        self.assertEqual({"policy": "runtime-identity-hex"}, normalized["name"])
        self.assertNotIn("0xdeadbeef", generator.strict_json_dumps(normalized))
        self.assertIsNotNone(
            generator.RAW_AUTO_NAME_PATTERN.search('{"name":"0xdeadbeef"}')
        )
        with self.assertRaisesRegex(RuntimeError, "raw runtime identity"):
            generator.schedule_result_descriptor(
                FakeSchedule("0xdeadbeef"),
                "literal",
                FakeSchedule,
            )

    def test_duplicate_json_object_key_is_rejected(self) -> None:
        path = self.temp_root / "duplicate.json"
        path.write_text(
            '{"schema":"first","schema":"second"}\n',
            encoding="utf-8",
            newline="\n",
        )

        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_nonfinite_json_constants_are_rejected(self) -> None:
        for index, value in enumerate(("NaN", "Infinity", "-Infinity")):
            with self.subTest(value=value):
                path = self.temp_root / f"nonfinite-{index}.json"
                path.write_text(
                    '{"schema":' + value + "}\n",
                    encoding="utf-8",
                    newline="\n",
                )
                with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                    generator.load_exact_inventory(
                        path,
                        generator.EXPECTED_UPSTREAM_COMMIT,
                    )

    def test_tampered_inventory_content_hash_is_rejected(self) -> None:
        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        value["content_sha256"] = "sha256:" + ("0" * 64)
        path = self.write_inventory("tampered-content-hash.json", value)

        with self.assertRaisesRegex(SystemExit, "content hash is invalid"):
            generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_tampered_profile_source_is_rejected_even_when_resealed(self) -> None:
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
        path = self.write_inventory("tampered-source-hash.json", value)

        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_wrong_commit_is_rejected_before_inventory_use(self) -> None:
        with self.assertRaisesRegex(SystemExit, "not the pinned DaySchedule commit"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

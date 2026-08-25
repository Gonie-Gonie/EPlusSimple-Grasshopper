"""Fail-closed tests for the pinned DaySchedule core oracle generator."""

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
    / "generate_day_schedule_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "day_schedule_core_oracle_generator", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class DayScheduleCoreOracleGeneratorTests(unittest.TestCase):
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

    @staticmethod
    def recalculate_inventory_hash(value: dict[str, object]) -> None:
        value["content_sha256"] = generator.canonical_sha256(
            {
                "files": value["files"],
                "scope_sha256": value["scope_sha256"],
                "symbols": value["symbols"],
                "upstream_commit": value["upstream_commit"],
            }
        )

    def test_exact_inventory_binds_fourteen_symbols_and_profile_source(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )

        self.assertEqual(
            generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"]
        )
        self.assertEqual(
            generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"]
        )
        self.assertEqual(
            list(generator.TARGET_SYMBOLS),
            [item["symbol"] for item in inventory["symbols"]],
        )
        self.assertEqual(
            generator.EXPECTED_SYMBOL_HASHES,
            {item["symbol"]: item["symbol_hash"] for item in inventory["symbols"]},
        )

    def test_case_definitions_are_exact_unique_sorted_and_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = [item["id"] for item in definitions]
        counts = Counter(item["symbol"] for item in definitions)

        self.assertEqual(42, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(generator.EXPECTED_CASE_COUNT, len(definitions))
        self.assertEqual(sorted(identifiers), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(counts))
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS}, dict(counts)
        )
        self.assertTrue(all(item["executor"] for item in definitions))
        generator.strict_json_dumps(definitions)

    def test_symbol_classifications_bind_exactly_ten_adapted_symbols(self) -> None:
        expected_adaptations = {
            "DaySchedule": "immutable-day-schedule-value-object",
            "DaySchedule.__deepcopy__": "native-day-schedule-deepcopy-memo",
            "DaySchedule.__init__": (
                "immutable-deterministic-day-schedule-construction"
            ),
            "DaySchedule.__setitem__": "immutable-day-schedule-item-update",
            "DaySchedule.astype": "immutable-day-schedule-astype",
            "DaySchedule.clip": "immutable-day-schedule-clip",
            "DaySchedule.from_compact": (
                "validated-deterministic-day-schedule-from-compact"
            ),
            "DaySchedule.from_constant": (
                "deterministic-finite-day-schedule-from-constant"
            ),
            "DaySchedule.from_windows": (
                "validated-deterministic-day-schedule-from-windows"
            ),
            "DaySchedule.type": "immutable-validated-day-schedule-type",
        }
        equivalent = {
            "DaySchedule.compactize",
            "DaySchedule.summary",
            "DaySchedule.time_tuple",
            "DaySchedule.to_idf_compactexpr",
        }

        self.assertEqual(
            expected_adaptations,
            generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        )
        self.assertEqual(equivalent, set(generator.EXPECTED_EQUIVALENT_SYMBOLS))
        self.assertEqual(
            set(generator.TARGET_SYMBOLS), set(expected_adaptations) | equivalent
        )

        definitions = generator.case_definitions()
        for symbol, adaptation in expected_adaptations.items():
            with self.subTest(symbol=symbol):
                adapted = [
                    item["expected_dotnet"]
                    for item in definitions
                    if item["symbol"] == symbol
                    and item["expected_dotnet"] is not None
                ]
                self.assertTrue(adapted)
                self.assertEqual(
                    {adaptation}, {item["adaptation"] for item in adapted}
                )
        self.assertFalse(
            any(
                item["expected_dotnet"] is not None
                for item in definitions
                if item["symbol"] in equivalent
            )
        )

    def test_oracle_schema_and_case_receipt_shapes_are_fail_closed(self) -> None:
        self.assertEqual(
            {
                "cases",
                "cases_sha256",
                "consumer_contract",
                "runtime",
                "schema",
                "symbols",
                "upstream",
            },
            generator.ORACLE_KEYS,
        )
        self.assertEqual(
            {"executor", "id", "python", "symbol"}, generator.CASE_KEYS
        )
        self.assertEqual(
            {"adaptation", "outcome"}, generator.EXPECTED_DOTNET_KEYS
        )
        self.assertEqual(
            {"adaptation", "error_category", "outcome"},
            generator.EXPECTED_DOTNET_ERROR_KEYS,
        )
        self.assertEqual(
            {"facts", "outcome"}, generator.PYTHON_RETURN_KEYS
        )
        self.assertEqual(
            {
                "error_category",
                "exception_type",
                "facts",
                "message",
                "outcome",
            },
            generator.PYTHON_RAISE_KEYS,
        )

    def test_float_encoding_is_recursive_exact_and_strict_json_safe(self) -> None:
        value = {
            "finite": [-0.0, 1.5],
            "nonfinite": [math.nan, math.inf, -math.inf],
        }
        normalized = generator.normalize(value)

        self.assertEqual(
            {"hex_without_prefix": "-0.0p+0", "kind": "binary64"},
            normalized["finite"][0],
        )
        self.assertEqual(
            {
                "hex_without_prefix": "1.8000000000000p+0",
                "kind": "binary64",
            },
            normalized["finite"][1],
        )
        self.assertEqual(
            [
                {"hex_without_prefix": "nan", "kind": "binary64"},
                {"hex_without_prefix": "inf", "kind": "binary64"},
                {"hex_without_prefix": "-inf", "kind": "binary64"},
            ],
            normalized["nonfinite"],
        )
        serialized = generator.strict_json_dumps(normalized)
        self.assertNotIn("0x", serialized)
        self.assertNotIn(":NaN", serialized)
        self.assertNotIn(":Infinity", serialized)
        with self.assertRaises(ValueError):
            generator.strict_json_dumps({"raw": math.nan})

    def test_runtime_hex_identity_names_are_policy_normalized(self) -> None:
        normalizer = generator.IdentityNormalizer()

        self.assertEqual(
            {"policy": "runtime-identity-hex"},
            normalizer.name("0xdeadbeef"),
        )
        self.assertEqual(
            {"policy": "literal", "value": "literal"},
            normalizer.name("literal"),
        )
        self.assertNotIn(
            "0xdeadbeef",
            generator.strict_json_dumps(normalizer.name("0xdeadbeef")),
        )
        self.assertIsNotNone(
            generator.RAW_AUTO_NAME_PATTERN.search('{"name":"0xdeadbeef"}')
        )

    def test_time_tuple_fresh_case_observes_independence_without_mutation(self) -> None:
        class FakeDaySchedule:
            @staticmethod
            def time_tuple() -> list[tuple[int, int]]:
                return [(index // 6, ((index % 6) + 1) * 10 % 60) for index in range(144)]

        observation = generator._execute(
            "time-tuple.fresh", FakeDaySchedule, object
        )

        self.assertEqual("returned", observation["outcome"])
        self.assertIs(True, observation["facts"]["distinct"])
        self.assertIs(True, observation["facts"]["same_values"])
        self.assertEqual(144, observation["facts"]["left_count"])
        self.assertEqual(144, observation["facts"]["right_count"])
        self.assertNotEqual(145, observation["facts"]["left_count"])

    def test_cases_sha256_binds_only_the_ordered_case_array(self) -> None:
        cases = [
            {
                "executor": "probe",
                "id": "probe.case",
                "python": {"facts": {"value": 1}, "outcome": "returned"},
                "symbol": "DaySchedule",
            }
        ]
        expected = generator.canonical_sha256(cases)

        self.assertEqual(expected, generator.cases_sha256(cases))
        mutated = copy.deepcopy(cases)
        mutated[0]["python"]["facts"]["value"] = 2
        self.assertNotEqual(expected, generator.cases_sha256(mutated))

    def test_validate_oracle_rejects_extra_root_and_receipt_keys(self) -> None:
        malformed_root = {key: None for key in generator.ORACLE_KEYS}
        malformed_root["schema"] = generator.SCHEMA
        malformed_root["cases"] = []
        malformed_root["cases_sha256"] = generator.cases_sha256([])
        malformed_root["unexpected"] = True
        with self.assertRaisesRegex(RuntimeError, "top-level|root|schema"):
            generator.validate_oracle(malformed_root)

        definitions = generator.case_definitions()
        cases = []
        for definition in definitions:
            case = {
                "executor": definition["executor"],
                "id": definition["id"],
                "python": {"facts": {}, "outcome": "returned"},
                "symbol": definition["symbol"],
            }
            if definition["expected_dotnet"] is not None:
                case["expected_dotnet"] = definition["expected_dotnet"]
            cases.append(case)
        cases[0]["unexpected"] = True
        malformed_receipt = {key: None for key in generator.ORACLE_KEYS}
        malformed_receipt.update(
            {
                "cases": cases,
                "cases_sha256": generator.cases_sha256(cases),
                "schema": generator.SCHEMA,
            }
        )
        with self.assertRaisesRegex(RuntimeError, "case|receipt|keys"):
            generator.validate_oracle(malformed_receipt)

    def test_duplicate_json_object_key_is_rejected(self) -> None:
        path = self.temp_root / "duplicate.json"
        path.write_text(
            '{"schema":"first","schema":"second"}\n',
            encoding="utf-8",
            newline="\n",
        )

        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(
                path, generator.EXPECTED_UPSTREAM_COMMIT
            )

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
                        path, generator.EXPECTED_UPSTREAM_COMMIT
                    )

    def test_tampered_inventory_content_hash_is_rejected(self) -> None:
        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        value["content_sha256"] = "sha256:" + ("0" * 64)
        path = self.write_inventory("tampered-content-hash.json", value)

        with self.assertRaisesRegex(SystemExit, "content hash is invalid"):
            generator.load_exact_inventory(
                path, generator.EXPECTED_UPSTREAM_COMMIT
            )

    def test_tampered_profile_source_is_rejected_even_when_resealed(self) -> None:
        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        source = next(
            item for item in value["files"] if item["path"] == generator.SOURCE_PATH
        )
        source["content_hash"] = "sha256:" + ("0" * 64)
        self.recalculate_inventory_hash(value)
        path = self.write_inventory("tampered-source-hash.json", value)

        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(
                path, generator.EXPECTED_UPSTREAM_COMMIT
            )

    def test_wrong_commit_is_rejected_before_inventory_use(self) -> None:
        with self.assertRaisesRegex(SystemExit, "not the pinned"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

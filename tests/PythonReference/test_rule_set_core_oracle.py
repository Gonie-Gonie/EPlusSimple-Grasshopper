"""Fail-closed tests for the pinned RuleSet core oracle generator."""

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
    / "generate_rule_set_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "rule_set_core_oracle_generator", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_CASE_IDS = (
    "astype.inplace-stale-type",
    "astype.outplace-string",
    "astype.partial-failure",
    "class.alias-topology",
    "class.mutable-slot",
    "class.slot-inventory",
    "clip.bounds-empty-name",
    "clip.inplace",
    "clip.reversed",
    "deepcopy.alias-topology",
    "deepcopy.memo-hit",
    "deepcopy.repeated",
    "friday.clear",
    "friday.explicit",
    "friday.mixed-type",
    "from-constant.day-alias",
    "from-constant.nonfinite",
    "from-constant.scalar-distinct",
    "from-days.day-ignores-type",
    "from-days.mixed-types",
    "from-days.scalar-overrides",
    "get-dayschedule.integer-indices",
    "get-dayschedule.invalid-index",
    "get-dayschedule.string-fallback",
    "holiday.clear",
    "holiday.explicit",
    "holiday.mixed-type",
    "init.default-anonymous",
    "init.explicit-padded",
    "init.mixed-types",
    "max.defaults",
    "max.override",
    "max.signed-zero",
    "min.defaults",
    "min.override",
    "min.signed-zero",
    "monday.clear",
    "monday.explicit",
    "monday.mixed-type",
    "saturday.clear",
    "saturday.explicit",
    "saturday.mixed-type",
    "summary.default-normalized",
    "summary.exclude-days",
    "summary.override-rich",
    "sunday.clear",
    "sunday.explicit",
    "sunday.mixed-type",
    "thursday.clear",
    "thursday.explicit",
    "thursday.mixed-type",
    "to-dict.aliases",
    "to-dict.nulls",
    "to-dict.order",
    "to-idf.defaults",
    "to-idf.weekday-expansion",
    "to-idf.weekend-holiday",
    "tuesday.clear",
    "tuesday.explicit",
    "tuesday.mixed-type",
    "type.default-real",
    "type.explicit-token",
    "type.inferred-day",
    "wednesday.clear",
    "wednesday.explicit",
    "wednesday.mixed-type",
    "weekdays.explicit",
    "weekdays.mixed-type",
    "weekdays.replace",
    "weekends.explicit",
    "weekends.mixed-type",
    "weekends.replace",
)


class RuleSetCoreOracleGeneratorTests(unittest.TestCase):
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

    @staticmethod
    def synthetic_oracle() -> dict[str, object]:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
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
                case["expected_dotnet"] = copy.deepcopy(
                    definition["expected_dotnet"]
                )
            cases.append(case)
        classifications = {
            symbol: "equivalent"
            if symbol in generator.EXPECTED_EQUIVALENT_SYMBOLS
            else "exception"
            for symbol in generator.TARGET_SYMBOLS
        }
        return {
            "cases": cases,
            "cases_sha256": generator.cases_sha256(cases),
            "consumer_contract": {
                "adaptations": generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
                "case_count": generator.EXPECTED_CASE_COUNT,
                "case_ids": [item["id"] for item in definitions],
                "classifications": classifications,
                "float_encoding": "python-binary64-hex-without-0x-prefix",
                "runtime_names": "policy-token-no-raw-address",
                "target_symbols": list(generator.TARGET_SYMBOLS),
            },
            "runtime": {
                "implementation": "cpython",
                "python_hash_algorithm": generator.REQUIRED_HASH_ALGORITHM,
                "python_hash_seed": 0,
                "python_hash_width_bits": generator.REQUIRED_HASH_WIDTH_BITS,
                "python_version": ".".join(map(str, generator.REQUIRED_PYTHON)),
            },
            "schema": generator.SCHEMA,
            "symbols": inventory["symbols"],
            "upstream": {
                "commit": generator.EXPECTED_UPSTREAM_COMMIT,
                "inventory_sha256": generator.EXPECTED_INVENTORY_SHA256,
                "path": generator.SOURCE_PATH,
                "source_sha256": generator.EXPECTED_SOURCE_SHA256,
            },
        }

    def test_exact_inventory_binds_twenty_four_symbols_and_profile_source(self) -> None:
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
        identifiers = tuple(item["id"] for item in definitions)
        counts = Counter(item["symbol"] for item in definitions)

        self.assertEqual(72, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(sorted(identifiers), list(identifiers))
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(counts))
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS}, dict(counts)
        )
        self.assertTrue(all(item["executor"] for item in definitions))
        generator.strict_json_dumps(definitions)

    def test_symbol_classifications_bind_seventeen_unique_adaptations(self) -> None:
        expected_adaptations = {
            "RuleSet": "immutable-ruleset-value-object",
            "RuleSet.__deepcopy__": "native-ruleset-deepcopy-memo",
            "RuleSet.__init__": "immutable-deterministic-ruleset-construction",
            "RuleSet.astype": "immutable-ruleset-astype",
            "RuleSet.clip": "immutable-ruleset-clip",
            "RuleSet.friday": "immutable-ruleset-friday-update",
            "RuleSet.from_constant": "deterministic-finite-ruleset-from-constant",
            "RuleSet.from_days": "validated-deterministic-ruleset-from-days",
            "RuleSet.holiday": "immutable-ruleset-holiday-update",
            "RuleSet.monday": "immutable-ruleset-monday-update",
            "RuleSet.saturday": "immutable-ruleset-saturday-update",
            "RuleSet.sunday": "immutable-ruleset-sunday-update",
            "RuleSet.thursday": "immutable-ruleset-thursday-update",
            "RuleSet.tuesday": "immutable-ruleset-tuesday-update",
            "RuleSet.wednesday": "immutable-ruleset-wednesday-update",
            "RuleSet.weekdays": "immutable-ruleset-weekdays-update",
            "RuleSet.weekends": "immutable-ruleset-weekends-update",
        }
        equivalent = {
            "RuleSet.get_dayschedule",
            "RuleSet.max",
            "RuleSet.min",
            "RuleSet.summary",
            "RuleSet.to_dict",
            "RuleSet.to_idf_compactexpr",
            "RuleSet.type",
        }

        self.assertEqual(
            expected_adaptations,
            generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        )
        self.assertEqual(
            len(expected_adaptations), len(set(expected_adaptations.values()))
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
                ]
                self.assertEqual(3, len(adapted))
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

    def test_get_dayschedule_is_equivalent_and_pins_range_failure(self) -> None:
        definitions = {
            item["id"]: item
            for item in generator.case_definitions()
            if item["symbol"] == "RuleSet.get_dayschedule"
        }
        self.assertEqual(
            {
                "get-dayschedule.integer-indices",
                "get-dayschedule.invalid-index",
                "get-dayschedule.string-fallback",
            },
            set(definitions),
        )
        self.assertTrue(
            all(item["expected_dotnet"] is None for item in definitions.values())
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
        self.assertEqual({"facts", "outcome"}, generator.PYTHON_RETURN_KEYS)
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
            {"policy": "runtime-identity-hex"}, normalizer.name("0xdeadbeef")
        )
        self.assertEqual(
            {"policy": "literal", "value": "literal"},
            normalizer.name("literal"),
        )
        self.assertNotIn(
            "0xdeadbeef",
            generator.strict_json_dumps(normalizer.name("0xdeadbeef")),
        )

    def test_ruleset_snapshot_uses_stable_reference_tokens_for_aliases(self) -> None:
        class FakeType:
            value = "real"

        class FakeDaySchedule:
            def __init__(self, name: str, value: float) -> None:
                self.name = name
                self.type = FakeType()
                self.unit = None
                self.data = [value] * 144

        class FakeRuleSet:
            def __init__(self) -> None:
                shared = FakeDaySchedule("shared", 1.0)
                other = FakeDaySchedule("other", 2.0)
                self.name = "rules"
                self.type = FakeType()
                self.weekdays = shared
                self.weekends = other
                self.monday = shared
                self.tuesday = None
                self.wednesday = None
                self.thursday = None
                self.friday = None
                self.saturday = None
                self.sunday = None
                self.holiday = other

        snapshot = generator._ruleset(
            FakeRuleSet(), FakeRuleSet, FakeDaySchedule
        )
        self.assertEqual("ruleset", snapshot["kind"])
        self.assertEqual("day-01", snapshot["slots"]["weekdays"])
        self.assertEqual("day-01", snapshot["slots"]["monday"])
        self.assertEqual("day-02", snapshot["slots"]["weekends"])
        self.assertEqual("day-02", snapshot["slots"]["holiday"])
        self.assertIsNone(snapshot["slots"]["tuesday"])
        self.assertEqual(2, len(snapshot["days"]))
        self.assertNotIn("0x", generator.strict_json_dumps(snapshot))

    def test_cases_sha256_binds_only_the_ordered_case_array(self) -> None:
        cases = [
            {
                "executor": "probe",
                "id": "probe.case",
                "python": {"facts": {"value": 1}, "outcome": "returned"},
                "symbol": "RuleSet",
            }
        ]
        expected = generator.canonical_sha256(cases)

        self.assertEqual(expected, generator.cases_sha256(cases))
        mutated = copy.deepcopy(cases)
        mutated[0]["python"]["facts"]["value"] = 2
        self.assertNotEqual(expected, generator.cases_sha256(mutated))

    def test_validate_oracle_accepts_only_exact_contract_and_hash(self) -> None:
        oracle = self.synthetic_oracle()
        generator.validate_oracle(oracle)

        malformed_root = copy.deepcopy(oracle)
        malformed_root["unexpected"] = True
        with self.assertRaisesRegex(RuntimeError, "top-level|root|key"):
            generator.validate_oracle(malformed_root)

        malformed_case = copy.deepcopy(oracle)
        malformed_case["cases"][0]["unexpected"] = True
        malformed_case["cases_sha256"] = generator.cases_sha256(
            malformed_case["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "case|Case|key"):
            generator.validate_oracle(malformed_case)

        malformed_python_receipt = copy.deepcopy(oracle)
        malformed_python_receipt["cases"][0]["python"]["unexpected"] = True
        malformed_python_receipt["cases_sha256"] = generator.cases_sha256(
            malformed_python_receipt["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "Python return receipt|key"):
            generator.validate_oracle(malformed_python_receipt)

        malformed_native_receipt = copy.deepcopy(oracle)
        adapted_case = next(
            case
            for case in malformed_native_receipt["cases"]
            if "expected_dotnet" in case
        )
        adapted_case["expected_dotnet"]["unexpected"] = True
        malformed_native_receipt["cases_sha256"] = generator.cases_sha256(
            malformed_native_receipt["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "native expectation"):
            generator.validate_oracle(malformed_native_receipt)

        malformed_hash = copy.deepcopy(oracle)
        malformed_hash["cases_sha256"] = "sha256:" + ("0" * 64)
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(malformed_hash)

    def test_validate_oracle_rejects_contract_and_identity_leakage(self) -> None:
        oracle = self.synthetic_oracle()

        classification = copy.deepcopy(oracle)
        classification["consumer_contract"]["classifications"]["RuleSet"] = (
            "equivalent"
        )
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(classification)

        adaptation = copy.deepcopy(oracle)
        adaptation["consumer_contract"]["adaptations"]["RuleSet.monday"] = (
            "immutable-ruleset-tuesday-update"
        )
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(adaptation)

        identity = copy.deepcopy(oracle)
        identity["cases"][0]["python"]["facts"]["name"] = "0xdeadbeef"
        identity["cases_sha256"] = generator.cases_sha256(identity["cases"])
        with self.assertRaisesRegex(RuntimeError, "runtime identity"):
            generator.validate_oracle(identity)

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

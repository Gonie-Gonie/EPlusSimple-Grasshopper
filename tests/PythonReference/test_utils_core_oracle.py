from __future__ import annotations

from collections import Counter
import copy
import importlib.util
import json
import math
from pathlib import Path
import tempfile
import unittest


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = (
    REPOSITORY_ROOT
    / "tools"
    / "python-reference"
    / "generate_utils_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location("generate_utils_core_oracle", GENERATOR_PATH)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load utils core generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_CASE_IDS = (
    "grjson-format.copy-isolation",
    "grjson-format.exact-defaults",
    "grjson-format.shared-global-mutation",
    "validate-enum.accepted-members-and-raw-values",
    "validate-enum.none-and-wraps",
    "validate-enum.rejection-surface",
    "validate-range.inclusive-boundaries",
    "validate-range.none-and-nonfinite",
    "validate-range.rejection-surface",
    "validate-type.allow-none-and-wraps",
    "validate-type.rejection-surface",
    "validate-type.union-subclass-and-bool",
)


class UtilsCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="utils-core-oracle-tests-")
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def returned(value: object = 0, python_type: str = "int") -> dict[str, object]:
        return {
            "outcome": "returned",
            "result": {"python_type": python_type, "value": value},
        }

    @staticmethod
    def raised(
        exception_type: str,
        message: str,
        error_category: str = "type",
    ) -> dict[str, object]:
        return {
            "error_category": error_category,
            "exception_type": exception_type,
            "message": message,
            "outcome": "raised",
        }

    @classmethod
    def facts(cls, identifier: str) -> dict[str, object]:
        if identifier == "grjson-format.copy-isolation":
            return {key: True for key in generator.CASE_FACT_KEYS[identifier]}
        if identifier == "grjson-format.exact-defaults":
            return {
                "building_key_order": list(generator.EXPECTED_BUILDING_KEY_ORDER),
                "root_key_order": list(generator.EXPECTED_ROOT_KEY_ORDER),
                "snapshot": copy.deepcopy(generator.EXPECTED_GRJSON_TEMPLATE),
            }
        if identifier == "grjson-format.shared-global-mutation":
            return {key: True for key in generator.CASE_FACT_KEYS[identifier]}
        if identifier == "validate-enum.rejection-surface":
            return {
                "observations": [
                    cls.raised(
                        "ValueError",
                        "Invalid value 'unknown' for mode. Allowed values: alpha,beta",
                        "domain",
                    ),
                    cls.raised(
                        "TypeError",
                        "sequence item 2: expected str instance, NoneType found",
                    ),
                    cls.raised(
                        "TypeError",
                        "sequence item 0: expected str instance, NumberMode found",
                    ),
                ]
            }
        if identifier == "validate-range.none-and-nonfinite":
            return {
                "observations": [
                    cls.returned(None, "NoneType"),
                    cls.returned(
                        {"hex_without_prefix": "nan", "kind": "binary64"},
                        "float",
                    ),
                    cls.raised(
                        "ValueError",
                        "Value 'inf' for fraction is below the maxmimum 1.",
                        "domain",
                    ),
                    cls.raised(
                        "ValueError",
                        "Value '-inf' for fraction is below the minimum 0.",
                        "domain",
                    ),
                ]
            }
        if identifier == "validate-range.rejection-surface":
            return {
                "observations": [
                    cls.raised(
                        "ValueError",
                        "Value '-1' for fraction is below the minimum 0.",
                        "domain",
                    ),
                    cls.raised(
                        "ValueError",
                        "Value '2' for fraction is below the maxmimum 1.",
                        "domain",
                    ),
                    cls.raised(
                        "TypeError",
                        "'<' not supported between instances of 'str' and 'int'",
                    ),
                ]
            }
        if identifier == "validate-type.union-subclass-and-bool":
            return {
                "observations": [
                    cls.returned(7, "int"),
                    cls.returned("seven", "str"),
                    cls.returned(True, "bool"),
                    cls.returned(8, "IntChild"),
                ]
            }

        result: dict[str, object] = {}
        if "metadata" in generator.CASE_FACT_KEYS[identifier]:
            result["metadata"] = {
                "doc": "probe",
                "has_wrapped": True,
                "name": "probe",
            }
        if "observations" in generator.CASE_FACT_KEYS[identifier]:
            result["observations"] = [cls.returned()]
        return result

    @classmethod
    def synthetic_oracle(cls) -> dict[str, object]:
        definitions = generator.case_definitions()
        cases = [
            {
                "executor": item["executor"],
                "expected_dotnet": copy.deepcopy(item["expected_dotnet"]),
                "id": item["id"],
                "python": {
                    "facts": cls.facts(item["id"]),
                    "outcome": "returned",
                },
                "symbol": item["symbol"],
            }
            for item in definitions
        ]
        return {
            "cases": cases,
            "cases_sha256": generator.cases_sha256(cases),
            "consumer_contract": {
                "adaptations": generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
                "case_count": generator.EXPECTED_CASE_COUNT,
                "case_ids": [item["id"] for item in definitions],
                "classifications": {
                    symbol: "exception" for symbol in generator.TARGET_SYMBOLS
                },
                "float_encoding": "python-binary64-hex-without-0x-prefix",
                "runtime_names": "policy-token-no-raw-address",
                "target_symbols": list(generator.TARGET_SYMBOLS),
            },
            "runtime": {
                "implementation": "cpython",
                "python_hash_algorithm": "siphash13",
                "python_hash_seed": 0,
                "python_hash_width_bits": 64,
                "python_version": "3.12.7",
            },
            "schema": generator.SCHEMA,
            "symbols": [
                {
                    **generator.EXPECTED_SYMBOL_RECEIPTS[symbol],
                    "symbol": symbol,
                }
                for symbol in generator.TARGET_SYMBOLS
            ],
            "upstream": {
                "commit": generator.EXPECTED_UPSTREAM_COMMIT,
                "inventory_sha256": generator.EXPECTED_INVENTORY_SHA256,
                "sources": list(generator.EXPECTED_SOURCES),
            },
        }

    def write_inventory(self, name: str, value: dict[str, object]) -> Path:
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

    def test_inventory_binds_two_exact_sources_and_four_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(
            list(generator.TARGET_SYMBOLS),
            [item["symbol"] for item in inventory["symbols"]],
        )
        self.assertEqual(
            list(generator.EXPECTED_SOURCES),
            [
                {"path": item["path"], "source_sha256": item["content_hash"]}
                for item in inventory["files"]
            ],
        )
        self.assertEqual(
            [
                {**generator.EXPECTED_SYMBOL_RECEIPTS[symbol], "symbol": symbol}
                for symbol in generator.TARGET_SYMBOLS
            ],
            inventory["symbols"],
        )

    def test_case_definitions_are_sorted_unique_and_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(12, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(sorted(identifiers), list(identifiers))
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS},
            dict(Counter(item["symbol"] for item in definitions)),
        )

    def test_all_four_symbols_are_exact_reviewed_adaptations(self) -> None:
        self.assertEqual(
            {
                "GRJSON_FORMAT": "immutable-validated-grm-template",
                "validate_enum": "strongly-typed-native-enum-validation",
                "validate_range": "finite-native-range-validation",
                "validate_type": "strongly-typed-native-type-validation",
            },
            generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        )
        self.assertEqual(
            set(generator.TARGET_SYMBOLS),
            set(generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS),
        )
        self.assertEqual(
            4, len(set(generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.values()))
        )
        for definition in generator.case_definitions():
            self.assertEqual(
                {
                    "adaptation": generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS[
                        definition["symbol"]
                    ],
                    "outcome": "returned",
                },
                definition["expected_dotnet"],
            )

    def test_schema_and_receipt_key_sets_are_fail_closed(self) -> None:
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
            {"executor", "expected_dotnet", "id", "python", "symbol"},
            generator.CASE_KEYS,
        )
        self.assertEqual(
            {"body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash"},
            generator.SYMBOL_KEYS,
        )
        self.assertEqual({"commit", "inventory_sha256", "sources"}, generator.UPSTREAM_KEYS)

    def test_synthetic_oracle_and_strict_round_trip_validate(self) -> None:
        oracle = self.synthetic_oracle()
        generator.validate_oracle(oracle)
        serialized = generator.strict_json_dumps(oracle, indent=2)
        generator.validate_oracle(json.loads(serialized))
        self.assertNotRegex(serialized, generator.RAW_ADDRESS_PATTERN)
        self.assertNotIn(": NaN", serialized)

    def test_specific_grjson_enum_range_and_type_semantics_fail_closed(self) -> None:
        changes = (
            (
                "grjson-format.exact-defaults",
                lambda facts: facts["snapshot"]["building"].__setitem__("north_axis", 1),
                "GRJSON_FORMAT",
            ),
            (
                "validate-enum.rejection-surface",
                lambda facts: facts["observations"][0].__setitem__("exception_type", "TypeError"),
                "validate_enum",
            ),
            (
                "validate-range.none-and-nonfinite",
                lambda facts: facts["observations"][1]["result"].__setitem__(
                    "value", {"hex_without_prefix": "inf", "kind": "binary64"}
                ),
                "validate_range",
            ),
            (
                "validate-type.union-subclass-and-bool",
                lambda facts: facts["observations"][2]["result"].__setitem__(
                    "python_type", "int"
                ),
                "validate_type",
            ),
        )
        for identifier, mutate, message in changes:
            with self.subTest(identifier=identifier):
                malformed = self.synthetic_oracle()
                case = next(item for item in malformed["cases"] if item["id"] == identifier)
                mutate(case["python"]["facts"])
                malformed["cases_sha256"] = generator.cases_sha256(malformed["cases"])
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_root_case_hash_runtime_symbol_source_and_consumer_tampering_fails(self) -> None:
        malformed_values: list[tuple[dict[str, object], str]] = []
        root = self.synthetic_oracle()
        root["unexpected"] = True
        malformed_values.append((root, "root|key"))

        case = self.synthetic_oracle()
        case["cases"][0]["unexpected"] = True
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        malformed_values.append((case, "case|key"))

        digest = self.synthetic_oracle()
        digest["cases_sha256"] = "sha256:" + ("0" * 64)
        malformed_values.append((digest, "cases hash"))

        runtime = self.synthetic_oracle()
        runtime["runtime"]["python_version"] = "3.12.8"
        malformed_values.append((runtime, "runtime"))

        symbol = self.synthetic_oracle()
        symbol["symbols"][0]["body_hash"] = "sha256:" + ("0" * 64)
        malformed_values.append((symbol, "Symbol receipt"))

        source = self.synthetic_oracle()
        source["upstream"]["sources"].reverse()
        malformed_values.append((source, "source receipts"))

        consumer = self.synthetic_oracle()
        consumer["consumer_contract"]["classifications"]["GRJSON_FORMAT"] = "equivalent"
        malformed_values.append((consumer, "consumer contract"))

        for malformed, message in malformed_values:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_float_encoding_is_recursive_exact_and_strict_json_safe(self) -> None:
        normalized = generator.normalize(
            {"values": [-0.0, 1.5, math.nan, math.inf, -math.inf]}
        )
        self.assertEqual(
            {"hex_without_prefix": "-0.0p+0", "kind": "binary64"},
            normalized["values"][0],
        )
        self.assertEqual(
            {"hex_without_prefix": "1.8000000000000p+0", "kind": "binary64"},
            normalized["values"][1],
        )
        self.assertEqual(
            ["nan", "inf", "-inf"],
            [item["hex_without_prefix"] for item in normalized["values"][2:]],
        )
        with self.assertRaises(ValueError):
            generator.strict_json_dumps({"raw": math.nan})

    def test_duplicate_and_nonfinite_inventory_json_are_rejected(self) -> None:
        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n',
            encoding="utf-8",
            newline="\n",
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(
                duplicate, generator.EXPECTED_UPSTREAM_COMMIT
            )

        for index, constant in enumerate(("NaN", "Infinity", "-Infinity")):
            with self.subTest(constant=constant):
                path = self.temp_root / f"nonfinite-{index}.json"
                path.write_text(
                    '{"schema":' + constant + "}\n",
                    encoding="utf-8",
                    newline="\n",
                )
                with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                    generator.load_exact_inventory(
                        path, generator.EXPECTED_UPSTREAM_COMMIT
                    )

    def test_tampered_inventory_commit_source_and_symbol_are_rejected(self) -> None:
        with self.assertRaisesRegex(SystemExit, "not the pinned"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)

        content = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        content["content_sha256"] = "sha256:" + ("0" * 64)
        with self.assertRaisesRegex(SystemExit, "content hash is invalid"):
            generator.load_exact_inventory(
                self.write_inventory("content.json", content),
                generator.EXPECTED_UPSTREAM_COMMIT,
            )

        source = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        source_item = next(
            item
            for item in source["files"]
            if item["path"] == generator.EXPECTED_SOURCES[0]["path"]
        )
        source_item["content_hash"] = "sha256:" + ("0" * 64)
        self.recalculate_inventory_hash(source)
        with self.assertRaisesRegex(SystemExit, "exact pinned inventory"):
            generator.load_exact_inventory(
                self.write_inventory("source.json", source),
                generator.EXPECTED_UPSTREAM_COMMIT,
            )

        symbol = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        symbol_item = next(
            item
            for item in symbol["symbols"]
            if item["path"] == generator.EXPECTED_SOURCES[1]["path"]
            and item["symbol"] == "validate_enum"
        )
        symbol_item["symbol_hash"] = "sha256:" + ("0" * 64)
        self.recalculate_inventory_hash(symbol)
        with self.assertRaisesRegex(SystemExit, "exact pinned inventory"):
            generator.load_exact_inventory(
                self.write_inventory("symbol.json", symbol),
                generator.EXPECTED_UPSTREAM_COMMIT,
            )


if __name__ == "__main__":
    unittest.main()

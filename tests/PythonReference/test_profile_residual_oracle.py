"""Fail-closed tests for the pinned Profile residual oracle generator."""

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
    / "generate_profile_residual_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "profile_residual_oracle_generator", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_CASE_IDS = (
    "profile-idf.empty",
    "profile-idf.ordered-seven",
    "profile-idf.repeated-reference",
    "profile-init.defaults",
    "profile-init.unvalidated-inputs",
    "profile-init.valid-seven-slots",
    "profile.alias-topology",
    "profile.identity-equality",
    "profile.mutable-surface",
    "schedule-operation-error.args",
    "schedule-operation-error.catch-family",
    "schedule-operation-error.inheritance",
    "schedule.alias-container",
    "schedule.default-topology",
    "schedule.mutable-userlist",
)


class ProfileResidualOracleGeneratorTests(unittest.TestCase):
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
            facts = {}
            if definition["id"] == "profile-idf.empty":
                facts = {
                    "count": 0,
                    "null_slots_omitted": True,
                    "objects": [],
                    "repeated_call_count": 0,
                    "results_are_fresh": True,
                }
            elif definition["id"] == "profile-idf.ordered-seven":
                facts = {
                    "count": 0,
                    "objects": [],
                    "schedule_names": [],
                    "type_limit_names": [],
                }
            elif definition["id"] == "profile-idf.repeated-reference":
                facts = {
                    "converted_objects_are_fresh": True,
                    "converted_values_match": True,
                    "count": 0,
                    "duplicate_positions_preserved": True,
                    "objects": [],
                }
            case = {
                "executor": definition["executor"],
                "id": definition["id"],
                "python": {"facts": facts, "outcome": "returned"},
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

    def test_exact_inventory_binds_five_symbols_and_all_receipt_hashes(self) -> None:
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
        expected_receipts = [
            {
                **generator.EXPECTED_SYMBOL_RECEIPTS[symbol],
                "path": generator.SOURCE_PATH,
                "symbol": symbol,
            }
            for symbol in generator.TARGET_SYMBOLS
        ]
        self.assertEqual(expected_receipts, inventory["symbols"])

    def test_case_definitions_are_exact_unique_sorted_and_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        counts = Counter(item["symbol"] for item in definitions)

        self.assertEqual(15, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(sorted(identifiers), list(identifiers))
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(counts))
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS}, dict(counts)
        )
        self.assertTrue(all(item["executor"] for item in definitions))
        generator.strict_json_dumps(definitions)

    def test_classifications_bind_four_unique_adaptations_and_one_equivalent(self) -> None:
        expected_adaptations = {
            "Profile": "immutable-profile-value-object",
            "Profile.__init__": "validated-immutable-profile-construction",
            "Schedule": "immutable-schedule-value-object",
            "ScheduleOperationError": "native-schedule-operation-exception-family",
        }
        equivalent = {"Profile.to_idf_object"}

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
        equivalent_definitions = [
            item for item in definitions if item["symbol"] in equivalent
        ]
        self.assertEqual(3, len(equivalent_definitions))
        self.assertTrue(
            all(item["expected_dotnet"] is None for item in equivalent_definitions)
        )

        unvalidated = next(
            item
            for item in definitions
            if item["id"] == "profile-init.unvalidated-inputs"
        )
        self.assertEqual(
            {
                "adaptation": "validated-immutable-profile-construction",
                "error_category": "type",
                "outcome": "raised",
            },
            unvalidated["expected_dotnet"],
        )

    def test_oracle_and_receipt_key_sets_are_fail_closed(self) -> None:
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
        self.assertEqual(
            {
                "body_hash",
                "kind",
                "path",
                "signature_hash",
                "symbol",
                "symbol_hash",
            },
            generator.SYMBOL_KEYS,
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

    def test_runtime_names_and_reference_tokens_never_expose_raw_identity(self) -> None:
        names = generator.IdentityNormalizer()
        references = generator.ReferenceNormalizer()
        shared = object()
        other = object()

        self.assertEqual(
            {"policy": "runtime-identity-hex"}, names.name("0xdeadbeef")
        )
        self.assertEqual(
            {"policy": "literal", "value": "literal"}, names.name("literal")
        )
        self.assertEqual("schedule-01", references.reference(shared, "schedule"))
        self.assertEqual("schedule-01", references.reference(shared, "schedule"))
        self.assertEqual("schedule-02", references.reference(other, "schedule"))
        serialized = generator.strict_json_dumps(
            {
                "name": names.name("0xdeadbeef"),
                "references": [
                    references.reference(shared, "schedule"),
                    references.reference(other, "schedule"),
                ],
            }
        )
        self.assertNotIn("0xdeadbeef", serialized)

    def test_profile_snapshot_uses_stable_alias_topology(self) -> None:
        class FakeType:
            value = "real"

        class FakeSchedule:
            def __init__(self, name: str) -> None:
                self.name = name
                self.type = FakeType()

            def __len__(self) -> int:
                return 365

        class FakeProfile:
            def __init__(self) -> None:
                shared = FakeSchedule("shared")
                other = FakeSchedule("other")
                self.name = "profile"
                self.heating_setpoint = shared
                self.cooling_setpoint = shared
                self.hvac_availability = None
                self.occupant = other
                self.lighting = None
                self.equipment = other
                self.hotwater = other

        snapshot = generator._profile(
            FakeProfile(), FakeProfile, FakeSchedule
        )
        self.assertEqual("profile", snapshot["kind"])
        self.assertEqual("schedule-01", snapshot["slots"]["heating_setpoint"])
        self.assertEqual("schedule-01", snapshot["slots"]["cooling_setpoint"])
        self.assertEqual("schedule-02", snapshot["slots"]["occupant"])
        self.assertEqual("schedule-02", snapshot["slots"]["equipment"])
        self.assertEqual("schedule-02", snapshot["slots"]["hotwater"])
        self.assertIsNone(snapshot["slots"]["lighting"])
        self.assertEqual(2, len(snapshot["objects"]))
        self.assertNotIn("0x", generator.strict_json_dumps(snapshot))

    def test_idf_descriptor_preserves_order_and_trims_only_trailing_nulls(self) -> None:
        class FakeIdd:
            name = "Schedule:Compact"

        class FakeIdfObject:
            def __init__(self) -> None:
                self.idd = FakeIdd()
                self.data = {
                    "Name": "annual",
                    "Schedule Type Limits Name": "ScheduleTypeLimits:Real",
                    "Field 1": None,
                }
                self.__extended_input = ["Through: 12/31", None, None]

        descriptor = generator._idf_object(FakeIdfObject())

        self.assertEqual(generator.IDF_OBJECT_KEYS, set(descriptor))
        self.assertEqual("Schedule:Compact", descriptor["object_type"])
        self.assertEqual(
            [
                "annual",
                "ScheduleTypeLimits:Real",
                None,
                "Through: 12/31",
            ],
            descriptor["fields"],
        )

    def test_equivalent_idf_facts_are_neutral_and_fail_closed(self) -> None:
        self.assertEqual(
            {
                "profile-idf.empty": {
                    "count",
                    "null_slots_omitted",
                    "objects",
                    "repeated_call_count",
                    "results_are_fresh",
                },
                "profile-idf.ordered-seven": {
                    "count",
                    "objects",
                    "schedule_names",
                    "type_limit_names",
                },
                "profile-idf.repeated-reference": {
                    "converted_objects_are_fresh",
                    "converted_values_match",
                    "count",
                    "duplicate_positions_preserved",
                    "objects",
                },
            },
            generator.PROFILE_IDF_FACT_KEYS,
        )
        self.assertEqual(
            ("append", "list", "mutability", "mutable"),
            generator.FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS,
        )

        oracle = self.synthetic_oracle()
        generator.validate_oracle(oracle)
        for fragment in generator.FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS:
            with self.subTest(fragment=fragment):
                malformed = copy.deepcopy(oracle)
                case = next(
                    item
                    for item in malformed["cases"]
                    if item["id"] == "profile-idf.empty"
                )
                case["python"]["facts"][f"python_{fragment}_fact"] = True
                malformed["cases_sha256"] = generator.cases_sha256(
                    malformed["cases"]
                )
                with self.assertRaisesRegex(
                    RuntimeError, "fact key set|Python-container-only"
                ):
                    generator.validate_oracle(malformed)

    def test_cases_sha256_binds_only_the_ordered_case_array(self) -> None:
        cases = [
            {
                "executor": "profile",
                "id": "probe.case",
                "python": {"facts": {"value": 1}, "outcome": "returned"},
                "symbol": "Profile",
            }
        ]
        expected = generator.canonical_sha256(cases)

        self.assertEqual(expected, generator.cases_sha256(cases))
        mutated = copy.deepcopy(cases)
        mutated[0]["python"]["facts"]["value"] = 2
        self.assertNotEqual(expected, generator.cases_sha256(mutated))

    def test_validate_oracle_accepts_only_exact_contract_receipts_and_hash(self) -> None:
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

        malformed_symbol_receipt = copy.deepcopy(oracle)
        malformed_symbol_receipt["symbols"][0]["body_hash"] = "sha256:" + (
            "0" * 64
        )
        with self.assertRaisesRegex(RuntimeError, "Symbol receipt"):
            generator.validate_oracle(malformed_symbol_receipt)

        malformed_hash = copy.deepcopy(oracle)
        malformed_hash["cases_sha256"] = "sha256:" + ("0" * 64)
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(malformed_hash)

    def test_validate_oracle_rejects_contract_and_identity_leakage(self) -> None:
        oracle = self.synthetic_oracle()

        classification = copy.deepcopy(oracle)
        classification["consumer_contract"]["classifications"]["Profile"] = (
            "equivalent"
        )
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(classification)

        adaptation = copy.deepcopy(oracle)
        adaptation["consumer_contract"]["adaptations"]["Schedule"] = (
            "immutable-profile-value-object"
        )
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(adaptation)

        equivalent_receipt = copy.deepcopy(oracle)
        equivalent_case = next(
            case
            for case in equivalent_receipt["cases"]
            if case["symbol"] == "Profile.to_idf_object"
        )
        equivalent_case["expected_dotnet"] = {
            "adaptation": "immutable-profile-value-object",
            "outcome": "returned",
        }
        equivalent_receipt["cases_sha256"] = generator.cases_sha256(
            equivalent_receipt["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "case|Case|key"):
            generator.validate_oracle(equivalent_receipt)

        identity = copy.deepcopy(oracle)
        identity_case = next(
            case for case in identity["cases"] if case["id"] == "profile.alias-topology"
        )
        identity_case["python"]["facts"]["name"] = "0xdeadbeef"
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

    def test_tampered_source_or_symbol_is_rejected_even_when_resealed(self) -> None:
        source_value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        source = next(
            item
            for item in source_value["files"]
            if item["path"] == generator.SOURCE_PATH
        )
        source["content_hash"] = "sha256:" + ("0" * 64)
        self.recalculate_inventory_hash(source_value)
        source_path = self.write_inventory("tampered-source-hash.json", source_value)
        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(
                source_path, generator.EXPECTED_UPSTREAM_COMMIT
            )

        symbol_value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        symbol = next(
            item
            for item in symbol_value["symbols"]
            if item["path"] == generator.SOURCE_PATH
            and item["symbol"] == "Profile"
        )
        symbol["body_hash"] = "sha256:" + ("0" * 64)
        self.recalculate_inventory_hash(symbol_value)
        symbol_path = self.write_inventory("tampered-symbol-hash.json", symbol_value)
        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(
                symbol_path, generator.EXPECTED_UPSTREAM_COMMIT
            )

    def test_wrong_commit_is_rejected_before_inventory_use(self) -> None:
        with self.assertRaisesRegex(SystemExit, "not the pinned"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

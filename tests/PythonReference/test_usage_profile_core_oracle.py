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
    / "generate_usage_profile_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "generate_usage_profile_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load UsageProfile core generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_CASE_IDS = (
    "dhw-heat-per-liter.database-factors",
    "dhw-heat-per-liter.numeric-kind",
    "dhw-heat-per-liter.value",
    "occupied-hours.daytime",
    "occupied-hours.equal-full-day",
    "occupied-hours.overnight",
    "operating-days.all",
    "operating-days.none",
    "operating-days.sparse-order",
    "people-activity-level.database-factors",
    "people-activity-level.numeric-kind",
    "people-activity-level.value",
    "profile-csv.greedy-header-and-quotes",
    "profile-csv.packaged-sources",
    "profile-csv.strip-unit-headers",
    "usage-profile-database.alias-topology",
    "usage-profile-database.mutable-registry",
    "usage-profile-database.type-topology",
    "usage-profile-dict.exact-order",
    "usage-profile-dict.sparse-days",
    "usage-profile-dict.vacations",
    "usage-profile-dragon.all-database-profiles",
    "usage-profile-dragon.lighting-tie",
    "usage-profile-dragon.overnight-vacation",
    "usage-profile-extended.database-membership",
    "usage-profile-extended.datapath",
    "usage-profile-extended.subclass-topology",
    "usage-profile-id.explicit",
    "usage-profile-id.private-mutation",
    "usage-profile-id.runtime-default",
    "usage-profile-init.complete",
    "usage-profile-init.mutable-inputs",
    "usage-profile-init.unvalidated",
    "usage-profile-lookup.all",
    "usage-profile-lookup.found-and-path",
    "usage-profile-lookup.missing",
    "usage-profile.alias-topology",
    "usage-profile.identity-equality",
    "usage-profile.mutable-surface",
)


class UsageProfileCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="usage-profile-core-oracle-tests-"
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def schedule_descriptor(name: str) -> dict[str, object]:
        return {
            "idf_fields": [name, "ScheduleTypeLimits:Real"],
            "maximum": 0,
            "minimum": 0,
            "name": {"policy": "literal", "value": name},
            "schedule_type": "real",
            "value_count": 365 * 144,
            "values_encoding": "binary64-hex-without-prefix-lines",
            "values_sha256": "sha256:" + ("1" * 64),
        }

    @classmethod
    def converted_profile(cls, *, database: bool) -> dict[str, object]:
        identity = {"policy": "literal", "value": "PROFILE-ID"}
        if database:
            native_identity: dict[str, object] = {
                "adaptation": "deterministic-native-usage-profile-identity",
                "comparison": "native-only-output-id-equals-native-source-usage-profile-id",
                "python_counterpart": "absent",
            }
            source = "standard"
        else:
            native_identity = {
                "comparison": "native-only-output-id-equals-exact-source-usage-profile-id",
                "python_counterpart": "absent",
            }
            source = "custom"
        return {
            "domestic_hotwater": 40,
            "name": "probe",
            "native_output_identity": native_identity,
            "occupied_hours": 8,
            "operating_days": ["monday"],
            "output_name": copy.deepcopy(identity),
            "schedules": {
                slot: cls.schedule_descriptor(slot)
                for slot in generator.SCHEDULE_SLOTS
            },
            "source": source,
            "source_identity": copy.deepcopy(identity),
            "upstream_output_name_equals_source_identity": True,
            "vacations": [],
            "ventilation": 1,
        }

    @classmethod
    def equivalent_facts(cls, identifier: str) -> dict[str, object]:
        facts = {key: None for key in generator.EQUIVALENT_FACT_KEYS[identifier]}
        if identifier == "usage-profile-dragon.all-database-profiles":
            profiles = [cls.converted_profile(database=True) for _ in range(24)]
            return {
                "profile_count": 24,
                "profiles": profiles,
                "schedule_slots": list(generator.SCHEDULE_SLOTS),
            }
        if identifier == "usage-profile-dragon.lighting-tie":
            return {
                "fractional_lighting_value_count": 522,
                "fractional_lighting_values": [
                    {
                        "hex_without_prefix": "1.8000000000000p-1",
                        "kind": "binary64",
                    }
                ],
                "profile": cls.converted_profile(database=False),
                "schedule_slots": list(generator.SCHEDULE_SLOTS),
            }
        if identifier == "usage-profile-dragon.overnight-vacation":
            return {
                "leap_day_failure": {
                    "error_category": "domain",
                    "exception_type": "ValueError",
                    "facts": {"end": "03/01", "start": "02/29"},
                    "message": "day is out of range for month",
                    "outcome": "raised",
                },
                "overnight": True,
                "profile": cls.converted_profile(database=False),
                "schedule_slots": list(generator.SCHEDULE_SLOTS),
                "vacation_count": 1,
                "wrapped_vacation_noop": {
                    "end": "01/03",
                    "schedule_slots_equal_without_vacation": list(
                        generator.SCHEDULE_SLOTS
                    ),
                    "start": "12/29",
                    "vacation_mask_positive_days": 0,
                },
            }
        return facts

    @classmethod
    def synthetic_oracle(cls) -> dict[str, object]:
        definitions = generator.case_definitions()
        cases: list[dict[str, object]] = []
        for definition in definitions:
            identifier = definition["id"]
            if identifier == "usage-profile-lookup.missing":
                observation: dict[str, object] = {
                    "error_category": "range",
                    "exception_type": "KeyError",
                    "facts": {},
                    "message": "missing",
                    "outcome": "raised",
                }
            else:
                facts = (
                    cls.equivalent_facts(identifier)
                    if definition["symbol"] in generator.EXPECTED_EQUIVALENT_SYMBOLS
                    else {}
                )
                observation = {"facts": facts, "outcome": "returned"}
            case: dict[str, object] = {
                "executor": definition["executor"],
                "id": identifier,
                "python": observation,
                "symbol": definition["symbol"],
            }
            if definition["expected_dotnet"] is not None:
                case["expected_dotnet"] = copy.deepcopy(
                    definition["expected_dotnet"]
                )
            cases.append(case)
        return {
            "cases": cases,
            "cases_sha256": generator.cases_sha256(cases),
            "consumer_contract": {
                "adaptations": generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
                "case_count": generator.EXPECTED_CASE_COUNT,
                "case_ids": [item["id"] for item in definitions],
                "classifications": {
                    symbol: (
                        "equivalent"
                        if symbol in generator.EXPECTED_EQUIVALENT_SYMBOLS
                        else "exception"
                    )
                    for symbol in generator.TARGET_SYMBOLS
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
                    "path": generator.SOURCE_PATH,
                    "symbol": symbol,
                }
                for symbol in generator.TARGET_SYMBOLS
            ],
            "upstream": {
                "commit": generator.EXPECTED_UPSTREAM_COMMIT,
                "inventory_sha256": generator.EXPECTED_INVENTORY_SHA256,
                "path": generator.SOURCE_PATH,
                "source_sha256": generator.EXPECTED_SOURCE_SHA256,
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

    def test_inventory_receipts_are_exact_and_fail_closed(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"])
        self.assertEqual(
            list(generator.TARGET_SYMBOLS),
            [item["symbol"] for item in inventory["symbols"]],
        )
        self.assertEqual(
            [
                {
                    **generator.EXPECTED_SYMBOL_RECEIPTS[symbol],
                    "path": generator.SOURCE_PATH,
                    "symbol": symbol,
                }
                for symbol in generator.TARGET_SYMBOLS
            ],
            inventory["symbols"],
        )

    def test_case_definitions_are_exact_sorted_and_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        counts = Counter(item["symbol"] for item in definitions)
        self.assertEqual(39, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(sorted(identifiers), list(identifiers))
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS}, dict(counts)
        )
        generator.strict_json_dumps(definitions)

    def test_classifications_bind_five_equivalents_and_eight_unique_adaptations(self) -> None:
        equivalent = {
            "KoreanUsageProfile.DHW_HEAT_PER_LITER",
            "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL",
            "KoreanUsageProfile.occupied_hours",
            "KoreanUsageProfile.operating_days",
            "KoreanUsageProfile.to_dragon",
        }
        adaptations = {
            "KoreanUsageProfile": "immutable-validated-usage-profile-value-object",
            "KoreanUsageProfile.ID": "deterministic-native-usage-profile-identity",
            "KoreanUsageProfile.__init__": "validated-immutable-usage-profile-construction",
            "KoreanUsageProfile.to_dict": "typed-usage-profile-serialization",
            "KoreanUsageProfileExtended": "usage-profile-source-discriminator",
            "Profile": "immutable-usage-profile-database",
            "Profile.get_DB": "diagnostic-usage-profile-lookup",
            "read_csv_without_units": "strict-invariant-profile-csv-reader",
        }
        self.assertEqual(equivalent, set(generator.EXPECTED_EQUIVALENT_SYMBOLS))
        self.assertEqual(adaptations, generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS)
        self.assertEqual(8, len(set(adaptations.values())))
        self.assertEqual(set(generator.TARGET_SYMBOLS), equivalent | set(adaptations))
        definitions = generator.case_definitions()
        for definition in definitions:
            symbol = definition["symbol"]
            if symbol in equivalent:
                self.assertIsNone(definition["expected_dotnet"])
            else:
                self.assertEqual(
                    adaptations[symbol], definition["expected_dotnet"]["adaptation"]
                )
        unvalidated = next(
            item for item in definitions if item["id"] == "usage-profile-init.unvalidated"
        )
        self.assertEqual(
            {
                "adaptation": "validated-immutable-usage-profile-construction",
                "error_category": "type",
                "outcome": "raised",
            },
            unvalidated["expected_dotnet"],
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
        self.assertEqual({"executor", "id", "python", "symbol"}, generator.CASE_KEYS)
        self.assertEqual({"adaptation", "outcome"}, generator.EXPECTED_DOTNET_KEYS)
        self.assertEqual(
            {"adaptation", "error_category", "outcome"},
            generator.EXPECTED_DOTNET_ERROR_KEYS,
        )
        self.assertEqual({"facts", "outcome"}, generator.PYTHON_RETURN_KEYS)
        self.assertEqual(
            {"error_category", "exception_type", "facts", "message", "outcome"},
            generator.PYTHON_RAISE_KEYS,
        )
        self.assertEqual(
            {"body_hash", "kind", "path", "signature_hash", "symbol", "symbol_hash"},
            generator.SYMBOL_KEYS,
        )

    def test_float_encoding_is_recursive_exact_and_strict_json_safe(self) -> None:
        normalized = generator.normalize(
            {"finite": [-0.0, 1.5], "nonfinite": [math.nan, math.inf, -math.inf]}
        )
        self.assertEqual(
            {"hex_without_prefix": "-0.0p+0", "kind": "binary64"},
            normalized["finite"][0],
        )
        self.assertEqual(
            {"hex_without_prefix": "1.8000000000000p+0", "kind": "binary64"},
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
        with self.assertRaises(ValueError):
            generator.strict_json_dumps({"raw": math.nan})

    def test_embedded_runtime_identities_are_tokenized_without_leakage(self) -> None:
        names = generator.IdentityNormalizer()
        first = names.name("PREFIX-AUTOID0xdeadbeef-SUFFIX")
        repeated = names.name("again:0xdeadbeef")
        other = names.name("again:0xcafebabe")
        self.assertEqual("tokenized-runtime-identities", first["policy"])
        self.assertIn("runtime-identity-0001", first["value"])
        self.assertIn("runtime-identity-0001", repeated["value"])
        self.assertIn("runtime-identity-0002", other["value"])
        self.assertEqual(
            {"policy": "literal", "value": "literal"}, names.name("literal")
        )
        serialized = generator.strict_json_dumps([first, repeated, other])
        self.assertNotRegex(serialized, generator.RAW_RUNTIME_IDENTITY_PATTERN)

    def test_synthetic_oracle_validates_with_exact_identity_and_schedule_contracts(self) -> None:
        oracle = self.synthetic_oracle()
        generator.validate_oracle(oracle)
        generator.validate_oracle(
            json.loads(generator.strict_json_dumps(oracle, indent=2))
        )
        converted = next(
            case
            for case in oracle["cases"]
            if case["id"] == "usage-profile-dragon.all-database-profiles"
        )["python"]["facts"]["profiles"][0]
        self.assertEqual(generator.CONVERTED_PROFILE_KEYS, set(converted))
        self.assertEqual(
            generator.DATABASE_OUTPUT_IDENTITY_KEYS,
            set(converted["native_output_identity"]),
        )
        self.assertEqual(list(generator.SCHEDULE_SLOTS), list(converted["schedules"]))
        boundary = next(
            case
            for case in oracle["cases"]
            if case["id"] == "usage-profile-dragon.overnight-vacation"
        )["python"]["facts"]
        self.assertEqual(
            {
                "end": "01/03",
                "schedule_slots_equal_without_vacation": list(
                    generator.SCHEDULE_SLOTS
                ),
                "start": "12/29",
                "vacation_mask_positive_days": 0,
            },
            boundary["wrapped_vacation_noop"],
        )
        self.assertEqual(
            {
                "error_category": "domain",
                "exception_type": "ValueError",
                "facts": {"end": "03/01", "start": "02/29"},
                "message": "day is out of range for month",
                "outcome": "raised",
            },
            boundary["leap_day_failure"],
        )

    def test_native_only_identity_policy_is_fail_closed_for_database_and_custom(self) -> None:
        oracle = self.synthetic_oracle()
        database_case = next(
            case
            for case in oracle["cases"]
            if case["id"] == "usage-profile-dragon.all-database-profiles"
        )
        converted = database_case["python"]["facts"]["profiles"][0]
        converted["native_output_identity"]["comparison"] = "exact"
        oracle["cases_sha256"] = generator.cases_sha256(oracle["cases"])
        with self.assertRaisesRegex(RuntimeError, "identity policy"):
            generator.validate_oracle(oracle)

        custom = self.synthetic_oracle()
        custom_case = next(
            case
            for case in custom["cases"]
            if case["id"] == "usage-profile-dragon.lighting-tie"
        )
        custom_profile = custom_case["python"]["facts"]["profile"]
        custom_profile["output_name"]["value"] = "different"
        custom["cases_sha256"] = generator.cases_sha256(custom["cases"])
        with self.assertRaisesRegex(RuntimeError, "output name|source identity"):
            generator.validate_oracle(custom)

    def test_equivalent_facts_are_neutral_exact_and_reject_container_claims(self) -> None:
        self.assertEqual(
            ("append", "container", "list", "mutability", "mutable", "python_type"),
            generator.FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS,
        )
        self.assertEqual(15, len(generator.EQUIVALENT_FACT_KEYS))
        oracle = self.synthetic_oracle()
        generator.validate_oracle(oracle)
        for fragment in generator.FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS:
            with self.subTest(fragment=fragment):
                malformed = copy.deepcopy(oracle)
                case = next(
                    item
                    for item in malformed["cases"]
                    if item["id"] == "dhw-heat-per-liter.value"
                )
                case["python"]["facts"][f"python_{fragment}_claim"] = True
                malformed["cases_sha256"] = generator.cases_sha256(
                    malformed["cases"]
                )
                with self.assertRaisesRegex(
                    RuntimeError, "fact key set|Python-container-only"
                ):
                    generator.validate_oracle(malformed)

    def test_schedule_descriptor_and_all_24_profile_coverage_are_fail_closed(self) -> None:
        missing_field = self.synthetic_oracle()
        case = next(
            item
            for item in missing_field["cases"]
            if item["id"] == "usage-profile-dragon.lighting-tie"
        )
        descriptor = case["python"]["facts"]["profile"]["schedules"]["hotwater"]
        descriptor.pop("idf_fields")
        missing_field["cases_sha256"] = generator.cases_sha256(
            missing_field["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "Schedule descriptor|key"):
            generator.validate_oracle(missing_field)

        short_database = self.synthetic_oracle()
        database_case = next(
            item
            for item in short_database["cases"]
            if item["id"] == "usage-profile-dragon.all-database-profiles"
        )
        database_case["python"]["facts"]["profiles"].pop()
        short_database["cases_sha256"] = generator.cases_sha256(
            short_database["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "exactly 24"):
            generator.validate_oracle(short_database)

        wrapped_drift = self.synthetic_oracle()
        boundary_case = next(
            item
            for item in wrapped_drift["cases"]
            if item["id"] == "usage-profile-dragon.overnight-vacation"
        )
        boundary_case["python"]["facts"]["wrapped_vacation_noop"][
            "vacation_mask_positive_days"
        ] = 1
        wrapped_drift["cases_sha256"] = generator.cases_sha256(
            wrapped_drift["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "wrapped vacation"):
            generator.validate_oracle(wrapped_drift)

        leap_drift = self.synthetic_oracle()
        boundary_case = next(
            item
            for item in leap_drift["cases"]
            if item["id"] == "usage-profile-dragon.overnight-vacation"
        )
        boundary_case["python"]["facts"]["leap_day_failure"][
            "error_category"
        ] = "range"
        leap_drift["cases_sha256"] = generator.cases_sha256(
            leap_drift["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "leap-day"):
            generator.validate_oracle(leap_drift)

    def test_cases_sha256_binds_only_the_ordered_case_array(self) -> None:
        cases = [
            {
                "executor": "probe",
                "id": "probe.case",
                "python": {"facts": {"value": 1}, "outcome": "returned"},
                "symbol": "Profile",
            }
        ]
        expected = generator.canonical_sha256(cases)
        self.assertEqual(expected, generator.cases_sha256(cases))
        cases[0]["python"]["facts"]["value"] = 2
        self.assertNotEqual(expected, generator.cases_sha256(cases))

    def test_validate_oracle_rejects_malformed_root_case_receipts_and_hash(self) -> None:
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

        malformed_python = copy.deepcopy(oracle)
        malformed_python["cases"][0]["python"]["unexpected"] = True
        malformed_python["cases_sha256"] = generator.cases_sha256(
            malformed_python["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "Python return receipt|key"):
            generator.validate_oracle(malformed_python)

        malformed_native = copy.deepcopy(oracle)
        adapted = next(
            item for item in malformed_native["cases"] if "expected_dotnet" in item
        )
        adapted["expected_dotnet"]["unexpected"] = True
        malformed_native["cases_sha256"] = generator.cases_sha256(
            malformed_native["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "native expectation"):
            generator.validate_oracle(malformed_native)

        malformed_symbol = copy.deepcopy(oracle)
        malformed_symbol["symbols"][0]["body_hash"] = "sha256:" + ("0" * 64)
        with self.assertRaisesRegex(RuntimeError, "Symbol receipt"):
            generator.validate_oracle(malformed_symbol)

        malformed_hash = copy.deepcopy(oracle)
        malformed_hash["cases_sha256"] = "sha256:" + ("0" * 64)
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(malformed_hash)

    def test_validate_oracle_rejects_consumer_outcome_and_raw_identity_drift(self) -> None:
        oracle = self.synthetic_oracle()

        classification = copy.deepcopy(oracle)
        classification["consumer_contract"]["classifications"]["Profile"] = "equivalent"
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(classification)

        adaptation = copy.deepcopy(oracle)
        adaptation["consumer_contract"]["adaptations"]["Profile"] = "wrong"
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(adaptation)

        outcome = copy.deepcopy(oracle)
        missing = next(
            item
            for item in outcome["cases"]
            if item["id"] == "usage-profile-lookup.missing"
        )
        missing["python"] = {"facts": {}, "outcome": "returned"}
        outcome["cases_sha256"] = generator.cases_sha256(outcome["cases"])
        with self.assertRaisesRegex(RuntimeError, "Python outcome"):
            generator.validate_oracle(outcome)

        identity = copy.deepcopy(oracle)
        identity_case = next(
            item
            for item in identity["cases"]
            if item["id"] == "usage-profile.alias-topology"
        )
        identity_case["python"]["facts"]["raw"] = "AUTOID0xdeadbeef"
        identity["cases_sha256"] = generator.cases_sha256(identity["cases"])
        with self.assertRaisesRegex(RuntimeError, "runtime identity"):
            generator.validate_oracle(identity)

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

    def test_tampered_inventory_source_symbol_and_commit_are_rejected(self) -> None:
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
            item for item in source["files"] if item["path"] == generator.SOURCE_PATH
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
            if item["path"] == generator.SOURCE_PATH
            and item["symbol"] == "KoreanUsageProfile"
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

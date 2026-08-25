"""Fail-closed tests for the remaining annual Schedule oracle generator."""

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
    / "generate_schedule_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location(
    "schedule_core_oracle_generator", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class ScheduleCoreOracleGeneratorTests(unittest.TestCase):
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

    def test_exact_inventory_binds_twenty_two_symbols_and_profile_source(self) -> None:
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

    def test_cases_are_exact_unique_sorted_and_cover_all_symbols(self) -> None:
        definitions = generator.case_definitions()
        identifiers = [item["id"] for item in definitions]

        self.assertEqual(104, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(generator.EXPECTED_CASE_COUNT, len(definitions))
        self.assertEqual(sorted(identifiers), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(
            set(generator.TARGET_SYMBOLS),
            {item["symbol"] for item in definitions},
        )
        generator.strict_json_dumps(definitions)

    def test_symbol_case_cardinalities_are_pinned(self) -> None:
        actual = Counter(item["symbol"] for item in generator.case_definitions())
        expected = Counter(
            {
                "Schedule.FIXED_LENGTH": 1,
                "Schedule.TIME_TUPLE": 1,
                "Schedule.__deepcopy__": 3,
                "Schedule.__init__": 11,
                "Schedule.apply": 9,
                "Schedule.astype": 4,
                "Schedule.average": 3,
                "Schedule.clip": 8,
                "Schedule.compactize": 4,
                "Schedule.dayschedules": 2,
                "Schedule.from_compact": 9,
                "Schedule.from_constant": 8,
                "Schedule.from_windows": 11,
                "Schedule.integral": 3,
                "Schedule.max": 2,
                "Schedule.min": 2,
                "Schedule.positive_average": 3,
                "Schedule.summary": 4,
                "Schedule.to_idf_object": 4,
                "Schedule.type": 2,
                "Schedule.unify_compactized_schedules": 5,
                "Schedule.unify_compactized_schedules_many": 5,
            }
        )
        self.assertEqual(expected, actual)

    def test_ten_exception_adaptation_ids_are_exact(self) -> None:
        adapted = [
            item["expected_dotnet"]
            for item in generator.case_definitions()
            if item["expected_dotnet"] is not None
        ]
        counts = Counter(item["adaptation"] for item in adapted)

        self.assertEqual(48, len(adapted))
        self.assertEqual(set(generator.ADAPTATION_IDS), set(counts))
        self.assertEqual(
            {
                "deterministic-schedule-from-constant-child-names": 7,
                "immutable-deterministic-schedule-construction": 7,
                "immutable-schedule-apply": 9,
                "immutable-schedule-astype": 2,
                "immutable-schedule-clip": 3,
                "immutable-schedule-time-tuple": 1,
                "native-schedule-deepcopy-memo": 1,
                "validated-deterministic-schedule-from-compact": 7,
                "validated-deterministic-schedule-from-windows": 9,
                "validated-schedule-unify-coverage": 2,
            },
            dict(counts),
        )
        adapted_symbols = {
            item["symbol"]
            for item in generator.case_definitions()
            if item["expected_dotnet"] is not None
        }
        self.assertEqual(
            set(generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS), adapted_symbols
        )
        self.assertEqual(10, len(adapted_symbols))
        self.assertEqual(12, len(generator.EXPECTED_EQUIVALENT_SYMBOLS))
        self.assertEqual(
            set(generator.TARGET_SYMBOLS),
            adapted_symbols | set(generator.EXPECTED_EQUIVALENT_SYMBOLS),
        )

    def test_constructor_and_alias_topology_cases_are_present(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        required = {
            "init.anonymous",
            "init.default-fraction",
            "init.default-real",
            "init.empty-name",
            "init.surrounding-space-name",
            "init.supplied-list-alias",
            "init.whitespace-name",
            "from-compact.distinct-equal-adjacent",
            "from-compact.leap-day",
            "from-compact.overlap-later-wins",
            "from-compact.same-ref-adjacent",
            "from-compact.single-gap",
            "from-constant.bool",
            "from-constant.day-explicit-type-ignored",
            "from-constant.real-nan",
            "from-constant.ruleset-explicit-type-ignored",
            "from-constant.surrounding-space-name",
            "from-constant.unsupported-object",
            "from-windows.day-alias",
            "from-windows.empty",
            "from-windows.leap-day",
            "from-windows.repeated-day-wrappers",
            "from-windows.repeated-scalar-wrappers",
            "from-windows.ruleset-alias",
            "from-windows.scalar-overlap",
            "from-windows.scalar-positive-infinity",
        }
        self.assertLessEqual(required, set(cases))
        self.assertEqual(
            "immutable-deterministic-schedule-construction",
            cases["init.supplied-list-alias"]["expected_dotnet"]["adaptation"],
        )
        self.assertEqual(
            "validated-deterministic-schedule-from-compact",
            cases["from-compact.single-gap"]["expected_dotnet"]["adaptation"],
        )

    def test_mutation_partial_failure_and_stale_type_cases_are_explicit(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        expected = {
            "apply.inplace-inclusive-mmdd": (None, "immutable-schedule-apply"),
            "apply.noninplace-deepcopy": (None, "immutable-schedule-apply"),
            "apply.type-unchecked": (None, "immutable-schedule-apply"),
            "astype.inplace-partial": ("ValueError", "immutable-schedule-astype"),
            "astype.inplace-stale": (None, "immutable-schedule-astype"),
            "clip.inplace-distinct": (None, "immutable-schedule-clip"),
            "clip.inplace-partial": ("ValueError", "immutable-schedule-clip"),
        }
        for identifier, (exception, adaptation) in expected.items():
            with self.subTest(identifier=identifier):
                self.assertEqual(exception, cases[identifier]["expected_exception"])
                self.assertEqual(
                    adaptation,
                    cases[identifier]["expected_dotnet"]["adaptation"],
                )

        self.assertEqual(
            "preserve-native-name-and-unchanged-source-child-references",
            cases["apply.noninplace-deepcopy"]["expected_dotnet"]["policy"],
        )

    def test_fixed_domain_and_promised_boundary_cases_are_explicit(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}

        self.assertNotIn("compactize.empty-data", cases)
        self.assertNotIn("compactize.shortened-data", cases)
        self.assertEqual(
            {
                "compactize.default-distinct",
                "compactize.equal-distinct",
                "compactize.full-run",
                "compactize.identity-runs",
            },
            {
                identifier
                for identifier, item in cases.items()
                if item["symbol"] == "Schedule.compactize"
            },
        )
        self.assertLessEqual(
            {
                "clip.outplace-lower-only",
                "clip.outplace-upper-only",
                "from-compact.leap-day",
                "from-constant.real-nan",
                "from-constant.surrounding-space-name",
                "from-constant.unsupported-object",
                "from-windows.empty",
                "from-windows.leap-day",
                "from-windows.repeated-day-wrappers",
                "from-windows.repeated-scalar-wrappers",
                "from-windows.scalar-positive-infinity",
                "unify-pair.interior-gap",
            },
            set(cases),
        )
        self.assertIsNone(cases["clip.outplace-lower-only"]["expected_dotnet"])
        self.assertIsNone(cases["clip.outplace-upper-only"]["expected_dotnet"])
        for identifier, adaptation, outcome in (
            (
                "from-constant.real-nan",
                "deterministic-schedule-from-constant-child-names",
                "raised",
            ),
            (
                "from-windows.scalar-positive-infinity",
                "validated-deterministic-schedule-from-windows",
                "raised",
            ),
            (
                "from-compact.leap-day",
                "validated-deterministic-schedule-from-compact",
                "raised",
            ),
            (
                "from-windows.leap-day",
                "validated-deterministic-schedule-from-windows",
                "raised",
            ),
            (
                "unify-pair.interior-gap",
                "validated-schedule-unify-coverage",
                "raised",
            ),
        ):
            with self.subTest(identifier=identifier):
                expectation = cases[identifier]["expected_dotnet"]
                self.assertEqual(adaptation, expectation["adaptation"])
                self.assertEqual(outcome, expectation["outcome"])
                self.assertEqual("domain", expectation["error_category"])

    def test_consumer_contract_maps_native_container_and_raw_idf_fields(self) -> None:
        self.assertEqual(
            {
                "dotnet": "fresh-read-only-collection-on-every-property-access",
                "preserved": "length-order-and-DaySchedule-reference-identity",
                "python": "fresh-mutable-list-on-every-property-access",
            },
            generator.CONSUMER_CONTRACT["native_container_mappings"][
                "Schedule.dayschedules"
            ],
        )
        self.assertEqual(
            {
                "excluded": (
                    "rendered-IdfObject-text-escaping-and-sanitization; "
                    "covered-by-the-separate-IdfObject-serializer-contract"
                ),
                "included": (
                    "raw-logical-Schedule:Compact-object-type-field-order-"
                    "field-values-and-extended-input"
                ),
            },
            generator.CONSUMER_CONTRACT["idf_observation_scope"],
        )
        self.assertEqual(
            {
                "dotnet": "one-contiguous-IdfObject.Fields-logical-value-sequence",
                "fixture_validation_metadata": (
                    "python-field-names-and-primary-extension-boundary-only; "
                    "native-IdfObject-has-no-separate-extended-collection"
                ),
                "normalization": "only-trailing-null-primary-slots-may-be-omitted",
                "preserved": (
                    "object-type-exact-non-null-primary-prefix-in-field-position-"
                    "order-and-exact-extension-continuation"
                ),
                "python": (
                    "ordered-fixed-153-primary-data-entries-plus-ordered-"
                    "extended_input"
                ),
            },
            generator.CONSUMER_CONTRACT["native_container_mappings"][
                "Schedule.to_idf_object"
            ],
        )

        class LogicalIdfObject:
            def __init__(self) -> None:
                self.idd = type("Idd", (), {"name": "Schedule:Compact"})()
                values = [
                    "A,B;!",
                    "ScheduleTypeLimits:Real",
                    *([None] * 151),
                ]
                self.data = dict(zip(generator.IDF_UPSTREAM_DATA_KEYS, values))
                self.__extended_input = []

            def __str__(self) -> str:
                raise AssertionError("The Schedule oracle must not render IdfObject text.")

        descriptor = generator._idf_descriptor(
            LogicalIdfObject(), generator.IdentityNormalizer()
        )
        self.assertEqual(
            {"data_entries", "extended_input", "kind", "object_type"},
            set(descriptor),
        )
        self.assertEqual("A,B;!", descriptor["data_entries"][0]["value"])
        self.assertEqual(
            list(generator.IDF_PRIMARY_FIELD_NAMES),
            [entry["field"] for entry in descriptor["data_entries"]],
        )
        self.assertEqual("Field 151", descriptor["data_entries"][-1]["field"])
        self.assertNotIn("rendered", descriptor)

    def test_every_idf_case_pins_primary_prefix_and_strict_extension(self) -> None:
        self.assertEqual(
            {
                "idf.constant-real": {
                    "extended_count": 0,
                    "primary_non_null_count": 12,
                },
                "idf.default-expanded-fields": {
                    "extended_count": 3499,
                    "primary_non_null_count": 153,
                },
                "idf.multiple-periods": {
                    "extended_count": 0,
                    "primary_non_null_count": 22,
                },
                "idf.rich-overrides": {
                    "extended_count": 0,
                    "primary_non_null_count": 32,
                },
            },
            generator.IDF_CASE_SHAPES,
        )
        self.assertEqual(
            (
                "Name",
                "Schedule Type Limits Name",
                *(f"Field {index}" for index in range(1, 152)),
            ),
            generator.IDF_PRIMARY_FIELD_NAMES,
        )

        descriptors = {}
        for identifier, shape in generator.IDF_CASE_SHAPES.items():
            prefix_count = shape["primary_non_null_count"]
            descriptor = {
                "data_entries": [
                    {
                        "field": field,
                        "value": f"primary-{index}" if index < prefix_count else None,
                    }
                    for index, field in enumerate(generator.IDF_PRIMARY_FIELD_NAMES)
                ],
                "extended_input": [
                    f"extended-{index}"
                    for index in range(shape["extended_count"])
                ],
                "kind": "idf-object",
                "object_type": "Schedule:Compact",
            }
            generator._validate_idf_descriptor(identifier, descriptor)
            descriptors[identifier] = descriptor

        malformed = copy.deepcopy(descriptors["idf.constant-real"])
        malformed["data_entries"][12]["value"] = None
        malformed["data_entries"][13]["value"] = "after-null"
        with self.assertRaisesRegex(RuntimeError, "non-null value after its null tail"):
            generator._validate_idf_descriptor(None, malformed)

        malformed = copy.deepcopy(descriptors["idf.constant-real"])
        malformed["data_entries"][-1]["field"] = ""
        with self.assertRaisesRegex(RuntimeError, "primary field order drifted"):
            generator._validate_idf_descriptor(None, malformed)

        malformed = copy.deepcopy(descriptors["idf.constant-real"])
        malformed["extended_input"] = ["unexpected-continuation"]
        with self.assertRaisesRegex(RuntimeError, "does not continue a full primary"):
            generator._validate_idf_descriptor(None, malformed)

        malformed = copy.deepcopy(descriptors["idf.default-expanded-fields"])
        malformed["extended_input"][-1] = None
        with self.assertRaisesRegex(RuntimeError, "not an ordered text list"):
            generator._validate_idf_descriptor(None, malformed)

    def test_numeric_cases_pin_compensation_overflow_subnormal_and_negative_zero(self) -> None:
        identifiers = {item["id"] for item in generator.case_definitions()}
        required = {
            "average.catastrophic",
            "average.minimum-subnormal",
            "average.negative-zero",
            "integral.catastrophic",
            "integral.minimum-subnormal",
            "integral.overflow",
            "max.negative-zero",
            "min.negative-zero",
            "positive-average.catastrophic",
            "positive-average.minimum-subnormal",
            "positive-average.none",
        }
        self.assertLessEqual(required, identifiers)
        self.assertEqual(
            {"hex_without_prefix": "-0.0p+0", "kind": "binary64", "value": -0.0},
            generator.scalar_descriptor(-0.0),
        )
        self.assertEqual(
            "0.0000000000001p-1022",
            generator.scalar_descriptor(generator.FLOAT_MIN_SUBNORMAL)[
                "hex_without_prefix"
            ],
        )
        self.assertEqual(
            "1.fffffffffffffp+1023",
            generator.scalar_descriptor(generator.FLOAT_MAX)["hex_without_prefix"],
        )
        self.assertEqual(
            {"kind": "nonfinite", "value": "positive-infinity"},
            generator.scalar_descriptor(math.inf),
        )

    def test_summary_and_idf_contract_cases_are_exactly_routed(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        self.assertEqual(
            {
                "idf.constant-real",
                "idf.default-expanded-fields",
                "idf.multiple-periods",
                "idf.rich-overrides",
            },
            {
                identifier
                for identifier, item in cases.items()
                if item["symbol"] == "Schedule.to_idf_object"
            },
        )
        self.assertEqual(
            {
                "summary.exact-rich",
                "summary.invalid-period-limit",
                "summary.negative-period-limit",
                "summary.zero-period-limit",
            },
            {
                identifier
                for identifier, item in cases.items()
                if item["symbol"] == "Schedule.summary"
            },
        )
        self.assertEqual(
            "TypeError", cases["summary.invalid-period-limit"]["expected_exception"]
        )

    def test_pair_and_many_unification_pin_different_missing_coverage_behavior(self) -> None:
        cases = {item["id"]: item for item in generator.case_definitions()}
        pair = cases["unify-pair.missing-coverage"]
        interior = cases["unify-pair.interior-gap"]
        many = cases["unify-many.missing-coverage"]

        self.assertIsNone(pair["expected_exception"])
        self.assertEqual(
            "validated-schedule-unify-coverage",
            pair["expected_dotnet"]["adaptation"],
        )
        self.assertIsNone(interior["expected_exception"])
        self.assertEqual(
            "validated-schedule-unify-coverage",
            interior["expected_dotnet"]["adaptation"],
        )
        self.assertEqual("ValueError", many["expected_exception"])
        self.assertIsNone(many["expected_dotnet"])
        self.assertIn("unify-many.zero", cases)
        self.assertIn("unify-many.one-empty", cases)

    def test_error_inventory_and_categories_are_fail_closed(self) -> None:
        cases = generator.case_definitions()
        raised = {
            item["id"]: item["expected_exception"]
            for item in cases
            if item["expected_exception"] is not None
        }
        self.assertEqual(17, len(raised))
        self.assertEqual("TypeError", raised["apply.invalid-date"])
        self.assertEqual("ValueError", raised["from-compact.leap-day"])
        self.assertEqual("TypeError", raised["from-constant.unsupported-object"])
        self.assertEqual("ValueError", raised["from-windows.leap-day"])
        self.assertEqual("TypeError", raised["from-windows.unsupported-object"])
        self.assertEqual("ValueError", raised["unify-many.missing-coverage"])
        self.assertEqual("domain", generator.python_error_category(ValueError()))
        self.assertEqual("type", generator.python_error_category(TypeError()))
        with self.assertRaisesRegex(RuntimeError, "Unknown Python core exception"):
            generator.python_error_category(RuntimeError())

    def test_identity_normalizer_links_runtime_names_without_serializing_addresses(self) -> None:
        normalizer = generator.IdentityNormalizer()
        source = object()
        copied = object()
        source_group = normalizer.identity(source, "schedule")

        direct = normalizer.name(hex(id(source)), source, "schedule")
        composite = normalizer.name(
            f"{hex(id(source))}:COPY", copied, "schedule"
        )

        self.assertEqual(
            {"identity_group": source_group, "policy": "runtime-identity-hex"},
            direct,
        )
        self.assertEqual(
            [
                {"kind": "runtime-identity", "value": source_group},
                {"kind": "literal", "value": ":COPY"},
            ],
            composite["segments"],
        )
        serialized = generator.strict_json_dumps({"direct": direct, "copy": composite})
        self.assertIsNone(generator.RAW_AUTO_NAME_PATTERN.search(serialized))

    def test_scalar_compaction_and_reference_runs_preserve_exact_partitions(self) -> None:
        values = generator.compact_scalar_values([0.0, -0.0] * 72)
        self.assertEqual("repeat", values["encoding"])
        self.assertEqual(144, values["length"])
        self.assertEqual(2, len(values["pattern"]))
        self.assertNotEqual(
            values["pattern"][0]["hex_without_prefix"],
            values["pattern"][1]["hex_without_prefix"],
        )
        self.assertEqual(
            [
                {"count": 2, "value": "ruleset:0"},
                {"count": 1, "value": "ruleset:1"},
                {"count": 2, "value": "ruleset:0"},
            ],
            generator._run_length_encode(
                ["ruleset:0", "ruleset:0", "ruleset:1", "ruleset:0", "ruleset:0"]
            ),
        )

    def test_strict_json_rejects_raw_nonfinite_constants(self) -> None:
        tagged = {
            token: generator.scalar_descriptor(value)
            for token, value in {
                "nan": math.nan,
                "negative-infinity": -math.inf,
                "positive-infinity": math.inf,
            }.items()
        }
        serialized = generator.strict_json_dumps(tagged)
        self.assertNotIn("NaN", serialized)
        self.assertNotIn("Infinity", serialized)
        with self.assertRaises(ValueError):
            generator.strict_json_dumps({"raw": math.inf})

    def test_duplicate_nonfinite_and_wrong_commit_inventories_are_rejected(self) -> None:
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

        nonfinite = self.temp_root / "nonfinite.json"
        nonfinite.write_text(
            '{"schema":Infinity}\n', encoding="utf-8", newline="\n"
        )
        with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
            generator.load_exact_inventory(
                nonfinite, generator.EXPECTED_UPSTREAM_COMMIT
            )

        with self.assertRaisesRegex(SystemExit, "not the pinned DaySchedule commit"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)

    def test_tampered_source_and_symbol_hashes_fail_closed(self) -> None:
        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        source = next(
            item for item in value["files"] if item["path"] == generator.SOURCE_PATH
        )
        source["content_hash"] = "sha256:" + ("0" * 64)
        self.recalculate_inventory_hash(value)
        tampered_source = self.write_inventory("tampered-source.json", value)
        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(
                tampered_source, generator.EXPECTED_UPSTREAM_COMMIT
            )

        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        symbol = next(
            item
            for item in value["symbols"]
            if item["symbol"] == "Schedule.from_compact"
        )
        symbol["symbol_hash"] = "sha256:" + ("0" * 64)
        self.recalculate_inventory_hash(value)
        tampered_symbol = self.write_inventory("tampered-symbol.json", value)
        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(
                tampered_symbol, generator.EXPECTED_UPSTREAM_COMMIT
            )

    def test_case_definition_mutations_fail_closed(self) -> None:
        definitions = copy.deepcopy(list(generator.case_definitions()))
        with self.assertRaisesRegex(RuntimeError, "Expected 104"):
            generator.validate_case_definitions(definitions[:-1])

        definitions = copy.deepcopy(list(generator.case_definitions()))
        definitions[1]["id"] = definitions[0]["id"]
        definitions.sort(key=lambda item: item["id"])
        with self.assertRaisesRegex(RuntimeError, "not unique and sorted"):
            generator.validate_case_definitions(definitions)

        definitions = copy.deepcopy(list(generator.case_definitions()))
        target = next(item for item in definitions if item["id"] == "constant.time-tuple")
        target["expected_dotnet"]["adaptation"] = "unknown-adaptation"
        with self.assertRaisesRegex(RuntimeError, "unknown adaptation"):
            generator.validate_case_definitions(definitions)

        definitions = copy.deepcopy(list(generator.case_definitions()))
        target = next(item for item in definitions if item["id"] == "apply.reversed-noop")
        target["expected_dotnet"].pop("error_category")
        with self.assertRaisesRegex(RuntimeError, "invalid error category"):
            generator.validate_case_definitions(definitions)

    def test_no_raw_runtime_name_or_binary64_prefix_can_enter_static_corpus(self) -> None:
        serialized = generator.strict_json_dumps(generator.case_definitions())
        self.assertIsNone(generator.RAW_AUTO_NAME_PATTERN.search(serialized))
        for value in (-0.0, 0.0, generator.FLOAT_MIN_SUBNORMAL, generator.FLOAT_MAX):
            descriptor = generator.scalar_descriptor(value)
            self.assertNotIn("0x", descriptor["hex_without_prefix"])
            self.assertIsNone(
                generator.RAW_AUTO_NAME_PATTERN.search(
                    generator.strict_json_dumps(descriptor)
                )
            )


if __name__ == "__main__":
    unittest.main()

from __future__ import annotations

from collections import Counter
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = (
    REPOSITORY_ROOT
    / "tools"
    / "python-reference"
    / "generate_common_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "common-core-oracle.json"
)

spec = importlib.util.spec_from_file_location("generate_common_core_oracle", GENERATOR_PATH)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load common core generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_CASE_IDS = (
    "setting-default-ep-version.components",
    "setting-default-ep-version.formatted-identities",
    "setting-default-ep-version.semantic-shape",
    "setting-default-year.calendar",
    "setting-default-year.run-period",
    "setting-default-year.scalar",
    "setting.baseline-values",
    "setting.default-version-roundtrip",
    "setting.engineering-shape",
    "version-class.descriptor",
    "version-class.identity-equality",
    "version-class.readonly-properties",
    "version-coerce.existing-identity",
    "version-coerce.failure-surface",
    "version-coerce.strings-and-sequences",
    "version-ep-dirname.default",
    "version-ep-dirname.legacy",
    "version-ep-dirname.zero-and-large",
    "version-format.default-direct",
    "version-format.delimiters",
    "version-format.empty-spec",
    "version-iddname.default",
    "version-iddname.legacy",
    "version-iddname.zero-and-large",
    "version-init.failure-surface",
    "version-init.integer-overloads",
    "version-init.string-tokenization",
    "version-iter.conversions",
    "version-iter.fresh-generators",
    "version-iter.ordered-exhaustion",
    "version-major.default-baseline",
    "version-major.explicit-three",
    "version-major.two-component-default",
    "version-minor.default-baseline",
    "version-minor.explicit-three",
    "version-minor.two-component-default",
    "version-patch.default-baseline",
    "version-patch.explicit-three",
    "version-patch.two-component-default",
)
EXPECTED_FIXTURE_BYTES = 34828
EXPECTED_FIXTURE_SHA256 = (
    "sha256:3510b6b3c561019457501391d2847c5e45ed2dc6dd4479842df9bf7db8446f7e"
)


class CommonCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="common-core-oracle-tests-")
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.BASE.BASE.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def case(value: dict[str, object], identifier: str) -> dict[str, object]:
        return next(item for item in value["cases"] if item["id"] == identifier)

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

    def test_fixture_is_exact_strict_and_self_validating(self) -> None:
        value = self.fixture()
        raw = FIXTURE_PATH.read_bytes()

        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertTrue(raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            raw.decode("utf-8"),
        )
        self.assertNotRegex(raw.decode("utf-8"), generator.RAW_ADDRESS_PATTERN)

    def test_inventory_binds_one_exact_source_and_thirteen_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )

        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator.SOURCE_PATH, inventory["file"]["path"])
        self.assertEqual(
            generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"]
        )
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

    def test_classifications_are_exactly_ten_equivalent_and_three_adapted(self) -> None:
        expected_equivalent = {
            "Setting",
            "Setting.DEFAULT_EP_VERSION",
            "Setting.DEFAULT_YEAR",
            "Version.__format__",
            "Version.__iter__",
            "Version.ep_dirname",
            "Version.iddname",
            "Version.major",
            "Version.minor",
            "Version.patch",
        }
        expected_adaptations = {
            "Version": "native-energyplus-version-descriptor",
            "Version.__init__": "validated-energyplus-version-construction",
            "Version.to_version_anyway": "strongly-typed-energyplus-version-coercion",
        }

        self.assertEqual(expected_equivalent, set(generator.EXPECTED_EQUIVALENT_SYMBOLS))
        self.assertEqual(
            expected_adaptations, generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
        )
        self.assertEqual(
            set(generator.TARGET_SYMBOLS),
            expected_equivalent | set(expected_adaptations),
        )
        self.assertEqual(3, len(set(expected_adaptations.values())))
        for definition in generator.case_definitions():
            adaptation = expected_adaptations.get(definition["symbol"])
            if adaptation is None:
                self.assertIsNone(definition["expected_dotnet"])
            else:
                self.assertEqual(
                    {"adaptation": adaptation, "outcome": "returned"},
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
            {"executor", "id", "python", "symbol"}, generator.CASE_KEYS
        )
        self.assertEqual(
            {"executor", "expected_dotnet", "id", "symbol"},
            generator.CASE_DEFINITION_KEYS,
        )
        self.assertEqual(
            {"adaptation", "outcome"}, generator.EXPECTED_DOTNET_KEYS
        )
        self.assertEqual({"facts", "outcome"}, generator.PYTHON_RETURN_KEYS)
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

    def test_fixture_pins_constructor_and_formatting_quirks(self) -> None:
        value = self.fixture()
        integers = self.case(value, "version-init.integer-overloads")["python"][
            "facts"
        ]["observations"]
        tokenized = self.case(value, "version-init.string-tokenization")["python"][
            "facts"
        ]["observations"]
        failures = self.case(value, "version-init.failure-surface")["python"][
            "facts"
        ]["observations"]
        empty_format = self.case(value, "version-format.empty-spec")["python"][
            "facts"
        ]

        self.assertEqual([-1, 2, 3], integers[2]["result"]["components"])
        self.assertEqual([True, 2, 0], integers[3]["result"]["components"])
        self.assertEqual(
            ["bool", "int", "int"], integers[3]["result"]["component_types"]
        )
        self.assertEqual([1, 2, 0], tokenized[3]["result"]["components"])
        self.assertEqual([12, 2, 0], tokenized[5]["result"]["components"])
        self.assertEqual(
            [
                "ValueError",
                "ValueError",
                "ValueError",
                "TypeError",
                "TypeError",
                "ValueError",
            ],
            [item["exception_type"] for item in failures],
        )
        self.assertEqual({"2420"}, set(empty_format.values()))

    def test_fixture_pins_identity_names_iteration_and_coercion(self) -> None:
        value = self.fixture()
        identity = self.case(value, "version-class.identity-equality")["python"][
            "facts"
        ]
        ep_name = self.case(value, "version-ep-dirname.default")["python"]["facts"]
        idd_name = self.case(value, "version-iddname.default")["python"]["facts"]
        iteration = self.case(value, "version-iter.ordered-exhaustion")["python"][
            "facts"
        ]
        coercion = self.case(value, "version-coerce.existing-identity")["python"][
            "facts"
        ]

        self.assertFalse(identity["separate_instances_equal"])
        self.assertTrue(identity["components_equal"])
        self.assertEqual("EnergyPlusV24-2-0", ep_name["value"])
        self.assertEqual("V24-2-0-Energy+.idd", idd_name["value"])
        self.assertEqual({"exhausted": True, "values": [24, 2, 0]}, iteration)
        self.assertTrue(coercion["same_identity"])

    def test_case_hash_binds_only_the_exact_ordered_case_array(self) -> None:
        value = self.fixture()
        self.assertEqual(value["cases_sha256"], generator.cases_sha256(value["cases"]))
        changed = copy.deepcopy(value["cases"])
        changed[0]["python"]["facts"]["components"][0] = 25
        self.assertNotEqual(value["cases_sha256"], generator.cases_sha256(changed))

    def test_root_case_runtime_source_symbol_and_contract_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []

        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root|key"))

        case = self.fixture()
        case["cases"][0]["unexpected"] = True
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case|key"))

        digest = self.fixture()
        digest["cases_sha256"] = "sha256:" + ("0" * 64)
        changes.append((digest, "cases hash"))

        runtime = self.fixture()
        runtime["runtime"]["python_version"] = "3.12.8"
        changes.append((runtime, "runtime"))

        source = self.fixture()
        source["upstream"]["source_sha256"] = "sha256:" + ("0" * 64)
        changes.append((source, "upstream"))

        symbol = self.fixture()
        symbol["symbols"][0]["body_hash"] = "sha256:" + ("0" * 64)
        changes.append((symbol, "Symbol receipt"))

        classification = self.fixture()
        classification["consumer_contract"]["classifications"]["Version"] = (
            "equivalent"
        )
        changes.append((classification, "consumer contract"))

        adaptation = self.fixture()
        adaptation["consumer_contract"]["adaptations"]["Version"] = "wrong"
        changes.append((adaptation, "consumer contract"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_fact_observation_and_native_expectation_keys_fail_closed(self) -> None:
        fact = self.fixture()
        self.case(fact, "version-format.delimiters")["python"]["facts"][
            "unexpected"
        ] = True
        fact["cases_sha256"] = generator.cases_sha256(fact["cases"])
        with self.assertRaisesRegex(RuntimeError, "Facts|key set"):
            generator.validate_oracle(fact)

        observation = self.fixture()
        item = self.case(observation, "version-init.failure-surface")["python"][
            "facts"
        ]["observations"][0]
        item["unexpected"] = True
        observation["cases_sha256"] = generator.cases_sha256(observation["cases"])
        with self.assertRaisesRegex(RuntimeError, "version-init|key set"):
            generator.validate_oracle(observation)

        native = self.fixture()
        self.case(native, "version-class.descriptor")["expected_dotnet"][
            "unexpected"
        ] = True
        native["cases_sha256"] = generator.cases_sha256(native["cases"])
        with self.assertRaisesRegex(RuntimeError, "native expectation"):
            generator.validate_oracle(native)

        equivalent = self.fixture()
        self.case(equivalent, "version-format.delimiters")["expected_dotnet"] = {
            "adaptation": "native-energyplus-version-descriptor",
            "outcome": "returned",
        }
        equivalent["cases_sha256"] = generator.cases_sha256(equivalent["cases"])
        with self.assertRaisesRegex(RuntimeError, "case|key"):
            generator.validate_oracle(equivalent)

    def test_semantic_and_raw_address_tampering_fails(self) -> None:
        semantic = self.fixture()
        self.case(semantic, "version-format.empty-spec")["python"]["facts"][
            "builtin_format"
        ] = "24-2-0"
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        with self.assertRaisesRegex(RuntimeError, "empty-format"):
            generator.validate_oracle(semantic)

        address = self.fixture()
        self.case(address, "version-major.explicit-three")["python"]["facts"][
            "components"
        ][0] = "0xdeadbeef"
        address["cases_sha256"] = generator.cases_sha256(address["cases"])
        with self.assertRaisesRegex(RuntimeError, "runtime address"):
            generator.validate_oracle(address)

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
            if item["path"] == generator.SOURCE_PATH and item["symbol"] == "Version"
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

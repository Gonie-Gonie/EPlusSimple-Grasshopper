from __future__ import annotations

from collections import Counter
import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
DEPENDENCY_ROOT = (
    REPOSITORY_ROOT / ".tools" / "python-reference" / "3.12.7" / "site-packages"
)
if DEPENDENCY_ROOT.is_dir():
    sys.path.insert(0, str(DEPENDENCY_ROOT))

GENERATOR_PATH = (
    REPOSITORY_ROOT
    / "tools"
    / "python-reference"
    / "generate_launcher_result_parser_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "launcher-result-parser-oracle.json"
)
PINNED_SOURCE = (
    REPOSITORY_ROOT
    / "temp"
    / "reference"
    / "upstream"
    / "eplussimple"
    / "src"
    / "idragon"
    / "launcher.py"
)

spec = importlib.util.spec_from_file_location(
    "generate_launcher_result_parser_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load launcher result generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_CASE_IDS = (
    "energyplus-result.class-descriptors",
    "energyplus-result.class-dynamic-identity",
    "energyplus-result.class-static-bindings",
    "energyplus-result.init-defaults",
    "energyplus-result.init-dispatch-overwrite",
    "energyplus-result.init-failure-transactionality",
    "energyplus-result.parse-audit-duplicates-unicode",
    "energyplus-result.parse-audit-failure-surface",
    "energyplus-result.parse-audit-recognition-boundaries",
    "energyplus-result.parse-bnd-duplicates-padding",
    "energyplus-result.parse-bnd-failure-grammar",
    "energyplus-result.parse-bnd-records",
    "energyplus-result.parse-err-diagnostics",
    "energyplus-result.parse-err-failure-surface",
    "energyplus-result.parse-err-time-empty",
    "energyplus-result.parse-eso-arity",
    "energyplus-result.parse-eso-opaque",
    "energyplus-result.parse-eso-values",
    "energyplus-result.parse-table-csv-multi-report",
    "energyplus-result.parse-table-failure-surface",
    "energyplus-result.parse-table-grammar-duplicates",
)
EXPECTED_FIXTURE_BYTES = 43605
EXPECTED_FIXTURE_SHA256 = (
    "sha256:e7fc86fd859eb054022796fdf7163bcc040b738d23fdd3466944362558ba6a94"
)
EXPECTED_CASES_SHA256 = (
    "sha256:a0464a29bfd0bd1712deacbac50d3f87f6ea15e4ba9f4d19a70e88e896be38dd"
)


class LauncherResultParserOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="launcher-result-parser-oracle-tests-"
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.BASE.BASE.BASE.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def case(value: dict[str, object], identifier: str) -> dict[str, object]:
        return next(item for item in value["cases"] if item["id"] == identifier)

    def test_fixture_is_exact_strict_and_self_validating(self) -> None:
        value = self.fixture()
        raw = FIXTURE_PATH.read_bytes()

        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertTrue(raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            raw.decode("utf-8"),
        )
        self.assertNotRegex(raw.decode("utf-8"), generator.RAW_ADDRESS_PATTERN)

    def test_inventory_binds_one_exact_source_and_seven_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator.SOURCE_PATH, inventory["file"]["path"])
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"])
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

    def test_cases_are_exact_sorted_unique_and_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        counts = Counter(item["symbol"] for item in definitions)

        self.assertEqual(EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(sorted(identifiers), list(identifiers))
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(21, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS}, dict(counts)
        )

    def test_all_seven_reviewed_exception_bindings_are_exact(self) -> None:
        expected_adaptations = {
            "EnergyPlusResult": "immutable-structured-energyplus-result",
            "EnergyPlusResult.__init__": "validated-energyplus-result-file-loading",
            "EnergyPlusResult.parse_audit": "ordered-typed-energyplus-audit-parsing",
            "EnergyPlusResult.parse_bnd": "csv-aware-energyplus-boundary-parsing",
            "EnergyPlusResult.parse_err": "structured-energyplus-error-log-parsing",
            "EnergyPlusResult.parse_eso": "explicitly-unsupported-energyplus-eso",
            "EnergyPlusResult.parse_table": "typed-energyplus-tabular-parsing",
        }
        expected_assertions = {
            "EnergyPlusResult": "launcher-result-energyplus-result-eab88d95",
            "EnergyPlusResult.__init__": "launcher-result-init-30d49efa",
            "EnergyPlusResult.parse_audit": "launcher-result-parse-audit-7315fbc3",
            "EnergyPlusResult.parse_bnd": "launcher-result-parse-bnd-631c7884",
            "EnergyPlusResult.parse_err": "launcher-result-parse-err-f5789307",
            "EnergyPlusResult.parse_eso": "launcher-result-parse-eso-3e849bcd",
            "EnergyPlusResult.parse_table": "launcher-result-parse-table-eaf18f21",
        }
        self.assertEqual(expected_adaptations, generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS)
        self.assertEqual(expected_assertions, generator.EXPECTED_ASSERTION_IDS)
        value = self.fixture()
        self.assertEqual(
            {symbol: "exception" for symbol in generator.TARGET_SYMBOLS},
            value["consumer_contract"]["classifications"],
        )
        for case in value["cases"]:
            self.assertEqual(
                {
                    "adaptation": expected_adaptations[case["symbol"]],
                    "outcome": "returned",
                },
                case["expected_dotnet"],
            )

    def test_fixture_pins_parser_quirks_and_unsupported_eso(self) -> None:
        value = self.fixture()
        defaults = self.case(value, "energyplus-result.init-defaults")["python"]["facts"]
        self.assertFalse(defaults["has_time"])
        self.assertEqual(["audit", "err", "bnd", "tbl", "eso"], defaults["attribute_order"])

        audit = self.case(
            value, "energyplus-result.parse-audit-duplicates-unicode"
        )["python"]["facts"]["entries"]
        self.assertEqual(["A", "B", "한글_١", "Huge"], [item["key"] for item in audit])
        self.assertEqual("3", audit[0]["value"]["decimal"])

        padding = self.case(
            value, "energyplus-result.parse-bnd-duplicates-padding"
        )["python"]["facts"]["tables"][0]["frame"]
        self.assertEqual(["A", "B", "C"], padding["columns"])
        self.assertEqual(["string", "none", "none"], [item["kind"] for item in padding["rows"][0]])

        errors = self.case(
            value, "energyplus-result.parse-err-failure-surface"
        )["python"]["facts"]["observations"]
        self.assertEqual(
            ["AttributeError", "AttributeError", "ValueError", "TypeError", "TypeError"],
            [item["exception_type"] for item in errors],
        )

        eso = self.case(value, "energyplus-result.parse-eso-values")["python"]["facts"]["observations"]
        self.assertTrue(all(item["result"] == {"kind": "none"} for item in eso))

        table_failures = self.case(
            value, "energyplus-result.parse-table-failure-surface"
        )["python"]["facts"]["observations"]
        self.assertEqual(
            ["UnboundLocalError"] * 5 + ["TypeError", "TypeError", "ParserError"],
            [item["exception_type"] for item in table_failures],
        )

    @unittest.skipUnless(PINNED_SOURCE.is_file() and DEPENDENCY_ROOT.is_dir(), "pinned reference environment unavailable")
    def test_generation_is_byte_identical_twice_and_matches_fixture(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        first = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE
        )
        second = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE
        )
        first_bytes = (generator.strict_json_dumps(first, indent=2) + "\n").encode()
        second_bytes = (generator.strict_json_dumps(second, indent=2) + "\n").encode()
        self.assertEqual(first_bytes, second_bytes)
        self.assertEqual(FIXTURE_PATH.read_bytes(), first_bytes)

    def test_root_case_runtime_symbol_and_contract_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []

        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root"))

        case = self.fixture()
        case["cases"][0]["unexpected"] = True
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case"))

        runtime = self.fixture()
        runtime["runtime"]["dependencies"]["pandas"] = "2.3.1"
        changes.append((runtime, "runtime"))

        symbol = self.fixture()
        symbol["symbols"][0]["body_hash"] = "sha256:" + ("0" * 64)
        changes.append((symbol, "Symbol"))

        adaptation = self.fixture()
        adaptation["consumer_contract"]["adaptations"]["EnergyPlusResult"] = "wrong"
        changes.append((adaptation, "consumer contract"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_raw_float_path_address_and_nonfinite_json_are_rejected(self) -> None:
        raw_float = self.fixture()
        raw_float["cases"][0]["python"]["facts"]["raw"] = 1.25
        raw_float["cases_sha256"] = generator.cases_sha256(raw_float["cases"])
        with self.assertRaisesRegex(RuntimeError, "Raw float"):
            generator.validate_oracle(raw_float)

        address = self.fixture()
        address["cases"][0]["python"]["facts"]["module"] = "0xdeadbeef"
        address["cases_sha256"] = generator.cases_sha256(address["cases"])
        with self.assertRaisesRegex(RuntimeError, "runtime address"):
            generator.validate_oracle(address)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(duplicate, generator.EXPECTED_UPSTREAM_COMMIT)

        for index, token in enumerate(("NaN", "Infinity", "-Infinity")):
            path = self.temp_root / f"nonfinite-{index}.json"
            path.write_text('{"schema":' + token + '}\n', encoding="utf-8")
            with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_case_hash_binds_the_exact_ordered_case_array(self) -> None:
        value = self.fixture()
        self.assertEqual(value["cases_sha256"], generator.cases_sha256(value["cases"]))
        changed = copy.deepcopy(value["cases"])
        changed[0]["python"]["facts"]["name"] = "Changed"
        self.assertNotEqual(value["cases_sha256"], generator.cases_sha256(changed))


if __name__ == "__main__":
    unittest.main()

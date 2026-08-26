"""Fail-closed tests for the constants engineering reference oracle."""

from __future__ import annotations

from collections import Counter
import copy
import importlib.util
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
    / "generate_constants_engineering_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "constants-engineering-oracle.json"
)
PINNED_SOURCE = (
    REPOSITORY_ROOT
    / "temp"
    / "reference"
    / "upstream"
    / "eplussimple"
    / "src"
    / "idragon"
    / "constants.py"
)

spec = importlib.util.spec_from_file_location(
    "generate_constants_engineering_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load constants engineering generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_FIXTURE_BYTES = 20_889
EXPECTED_FIXTURE_SHA256 = (
    "sha256:e5261b2898a374722c24247f7d5a4fbc7df83cab1fbe8ad225827ee170d5cf54"
)
EXPECTED_CASES_SHA256 = (
    "sha256:18cc2d2295cad8a96a1a54ebd726c9d258586cd5f44a46c401fcb2f87997050e"
)


class ConstantsEngineeringOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="constants-engineering-oracle-tests-"
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.SUPPORT.load_json_without_duplicates(FIXTURE_PATH)
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

    def test_inventory_binds_exact_source_ast_and_eight_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"])
        self.assertEqual(
            generator.EXPECTED_SOURCE_AST_SHA256, inventory["file"]["ast_hash"]
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

    def test_cases_are_sorted_unique_and_exactly_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(24, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS},
            dict(Counter(item["symbol"] for item in definitions)),
        )

    def test_mixed_equivalent_and_exception_contract_is_exact(self) -> None:
        value = self.fixture()
        self.assertEqual(
            {
                "THERMAL": "native-thermal-default-constant-container",
                "Unit": "native-named-unit-conversion-constants",
            },
            value["consumer_contract"]["adaptations"],
        )
        self.assertEqual(
            generator.EXPECTED_ASSERTION_IDS,
            value["consumer_contract"]["assertion_ids"],
        )
        for case in value["cases"]:
            if case["symbol"] in {"THERMAL", "Unit"}:
                self.assertEqual(
                    generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS[case["symbol"]],
                    case["expected_dotnet"]["adaptation"],
                )
            else:
                self.assertNotIn("expected_dotnet", case)
                self.assertEqual(
                    "equivalent",
                    value["consumer_contract"]["classifications"][case["symbol"]],
                )

    def test_fixture_pins_alias_topology_binary64_and_idf_default(self) -> None:
        value = self.fixture()
        aliases = self.case(
            value, "constants-engineering.unit.class.alias-topology"
        )["python"]["facts"]
        self.assertTrue(aliases["l2m3_is_mm2m"])
        self.assertTrue(aliases["mm2m_is_w2kw"])
        self.assertEqual(
            ["MM2M", "W2KW", "L2M3"], aliases["alias_group"]
        )
        order = self.case(
            value, "constants-engineering.unit.class.member-order"
        )["python"]["facts"]
        self.assertEqual(
            ["MM2M", "NONE2PRC", "PRC2NONE"], order["iterated_member_names"]
        )

        expected_probes = {
            "l2m3": "1.0ff972474538fp-7",
            "mm2m": "1.4000000000000p+0",
            "none2prc": "1.2c00000000000p+5",
            "prc2none": "1.8000000000000p-2",
            "w2kw": "1.0cccccccccccdp+2",
        }
        for token, expected in expected_probes.items():
            case = self.case(
                value,
                f"constants-engineering.unit.{token}.engineering-probe",
            )
            self.assertEqual(expected, case["python"]["facts"]["result"]["binary64"])

        activity = self.case(
            value,
            "constants-engineering.thermal.people-activity-level.idf-default",
        )["python"]["facts"]
        self.assertEqual("1.ac00000000000p+6", activity["activity_value"]["binary64"])
        self.assertEqual("Schedule:Constant", activity["object_type"])
        self.assertEqual("$DEFAULT$PEOPLEACTIVITY", activity["name"])

    @unittest.skipUnless(
        PINNED_SOURCE.is_file() and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
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

    def test_root_case_contract_runtime_and_symbol_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root"))

        case = self.fixture()
        case["cases"][0]["unexpected"] = True
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case"))

        equivalent = self.fixture()
        equivalent["cases"][3]["expected_dotnet"] = {
            "adaptation": "not-allowed",
            "outcome": "returned",
        }
        equivalent["cases_sha256"] = generator.cases_sha256(equivalent["cases"])
        changes.append((equivalent, "case"))

        contract = self.fixture()
        contract["consumer_contract"]["assertion_ids"]["Unit"] = "wrong"
        changes.append((contract, "consumer contract"))

        runtime = self.fixture()
        runtime["runtime"]["dependencies"]["pandas"] = "2.3.1"
        changes.append((runtime, "runtime"))

        symbol = self.fixture()
        symbol["symbols"][0]["body_hash"] = "sha256:" + ("0" * 64)
        changes.append((symbol, "symbol"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_raw_float_path_address_guid_timestamp_and_hash_tampering_fail(self) -> None:
        fixtures = (
            (1.25, "Raw float"),
            (r"C:\\raw\\path", "absolute path"),
            ("/tmp/constants-oracle/file", "absolute path"),
            ("0xdeadbeef", "raw address"),
            ("01234567-89ab-4cde-8fab-0123456789ab", "GUID-like"),
            ("2026-08-26T12:34:56Z", "timestamp"),
        )
        for raw_value, message in fixtures:
            malformed = self.fixture()
            malformed["cases"][0]["python"]["facts"]["raw"] = raw_value
            malformed["cases_sha256"] = generator.cases_sha256(malformed["cases"])
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

        changed = self.fixture()
        changed_cases = copy.deepcopy(changed["cases"])
        changed_cases[0]["python"]["facts"]["member_count"] = 999
        self.assertNotEqual(
            changed["cases_sha256"], generator.cases_sha256(changed_cases)
        )

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(
                duplicate, generator.EXPECTED_UPSTREAM_COMMIT
            )

        for index, token in enumerate(("NaN", "Infinity", "-Infinity")):
            path = self.temp_root / f"nonfinite-{index}.json"
            path.write_text('{"schema":' + token + "}\n", encoding="utf-8")
            with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                generator.load_exact_inventory(
                    path, generator.EXPECTED_UPSTREAM_COMMIT
                )


if __name__ == "__main__":
    unittest.main()

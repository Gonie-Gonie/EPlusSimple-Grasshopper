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
    / "generate_launcher_runtime_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "launcher-runtime-oracle.json"
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
    "generate_launcher_runtime_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load launcher runtime generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_FIXTURE_BYTES = 19_786
EXPECTED_FIXTURE_SHA256 = (
    "sha256:3df3d7fb8c0c9d85ad0e9ffae9ae3055d742671b4554b2c860ff9f1877f9df33"
)
EXPECTED_CASES_SHA256 = (
    "sha256:bf5d658273fcf42e536acc102e1b117497b3f017031c0db0c2d605c87297d4bc"
)


class LauncherRuntimeOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="launcher-runtime-oracle-tests-"
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.BASE.BASE.BASE.BASE.load_json_without_duplicates(
            FIXTURE_PATH
        )
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

    def test_inventory_binds_one_exact_source_and_four_receipts(self) -> None:
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
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(sorted(identifiers), list(identifiers))
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(12, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS}, dict(counts)
        )

    def test_all_four_reviewed_exception_bindings_are_exact(self) -> None:
        expected_adaptations = {
            "ExecutableEnergyPlusNotFoundError": "structured-energyplus-runtime-not-found-failure",
            "find_executable_dir": "hash-verified-energyplus-runtime-resolution",
            "run": "bounded-deterministic-energyplus-batch-execution",
            "run_single": "isolated-cancellable-energyplus-single-run",
        }
        expected_assertions = {
            "ExecutableEnergyPlusNotFoundError": "launcher-runtime-executable-not-found-76d795db",
            "find_executable_dir": "launcher-runtime-find-executable-dir-6de563f4",
            "run": "launcher-runtime-run-84c6ff24",
            "run_single": "launcher-runtime-run-single-eda7f757",
        }
        self.assertEqual(
            expected_adaptations, generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
        )
        self.assertEqual(expected_assertions, generator.EXPECTED_ASSERTION_IDS)
        value = self.fixture()
        self.assertEqual(
            {symbol: "exception" for symbol in generator.TARGET_SYMBOLS},
            value["consumer_contract"]["classifications"],
        )
        self.assertEqual(
            "closed-fakes-no-process-or-active-load",
            value["consumer_contract"]["execution_policy"],
        )

    def test_fixture_pins_discovery_broadcast_and_cleanup_quirks(self) -> None:
        value = self.fixture()
        discovery = self.case(
            value, "launcher-runtime.find-executable-failure"
        )["python"]["facts"]["observations"]
        self.assertEqual(
            [
                "ExecutableEnergyPlusNotFoundError",
                "FileNotFoundError",
                "ValueError",
            ],
            [item["exception_type"] for item in discovery],
        )

        broadcast = self.case(value, "launcher-runtime.run-broadcast")["python"][
            "facts"
        ]
        self.assertEqual(
            ["model.idf", "model.idf"],
            broadcast["caller_lists_after"]["idfs"],
        )
        self.assertTrue(all("output_dir" not in item["kwargs"] for item in broadcast["calls"]))
        self.assertTrue(all(item["kwargs"]["verbose"] is False for item in broadcast["calls"]))

        inferred = self.case(
            value, "launcher-runtime.run-single-inferred-delete"
        )["python"]["facts"]
        self.assertEqual(["23.2.0"], inferred["resolver_calls"])
        self.assertFalse(inferred["copied_audit_exists_after"])

        transaction = self.case(
            value, "launcher-runtime.run-single-transactionality"
        )["python"]["facts"]
        self.assertTrue(transaction["side_effects"]["launch_failure_run_dir_exists"])
        self.assertTrue(transaction["side_effects"]["copy_failure_run_dir_exists"])
        self.assertFalse(transaction["side_effects"]["parse_failure_run_dir_exists"])
        self.assertTrue(transaction["side_effects"]["parse_failure_copied_output_exists"])
        self.assertEqual(0, transaction["process_attempt_count"])

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
        changes.append((symbol, "symbol"))

        contract = self.fixture()
        contract["consumer_contract"]["adaptations"]["run"] = "wrong"
        changes.append((contract, "consumer contract"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_raw_float_path_address_guid_timestamp_and_nonfinite_are_rejected(self) -> None:
        fixtures = (
            (1.25, "Raw float"),
            (r"C:\\raw\\path", "absolute path"),
            ("failure at C://Users//host//AppData//Local//Temp//file", "absolute path"),
            ("failure at /tmp/launcher-runtime/file", "absolute path"),
            ("dragons-launcher-runtime-oracle-random", "temporary token"),
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

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(
                duplicate, generator.EXPECTED_UPSTREAM_COMMIT
            )

        for index, token in enumerate(("NaN", "Infinity", "-Infinity")):
            path = self.temp_root / f"nonfinite-{index}.json"
            path.write_text('{"schema":' + token + '}\n', encoding="utf-8")
            with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                generator.load_exact_inventory(
                    path, generator.EXPECTED_UPSTREAM_COMMIT
                )

    def test_case_hash_binds_the_exact_ordered_case_array(self) -> None:
        value = self.fixture()
        self.assertEqual(value["cases_sha256"], generator.cases_sha256(value["cases"]))
        changed = copy.deepcopy(value["cases"])
        changed[0]["python"]["facts"]["name"] = "Changed"
        self.assertNotEqual(value["cases_sha256"], generator.cases_sha256(changed))


if __name__ == "__main__":
    unittest.main()

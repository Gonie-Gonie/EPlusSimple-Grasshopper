"""Fail-closed tests for the bounded add-supply-system oracle."""

from __future__ import annotations

from collections import Counter
import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
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
    / "generate_dragon_model_add_supply_system_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-model-add-supply-system-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_model_add_supply_system_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load add-supply-system generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 15_119
EXPECTED_FIXTURE_SHA256 = (
    "sha256:42ad2d75ce91edd153bd9e07382a03b5095ea0300df227f87e0d0147b377230f"
)
EXPECTED_CASES_SHA256 = (
    "sha256:ac58c4020edba588dceb8793b42552d261eb6686975bee1b553e9d8697d9cc2d"
)


class DragonModelAddSupplySystemOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-model-add-supply-system-tests-"
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def case(value: dict[str, object], identifier: str) -> dict[str, object]:
        return next(item for item in value["cases"] if item["id"] == identifier)

    def test_fixture_is_exact_utf8_strict_and_self_validating(self) -> None:
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

    def test_inventory_binds_twelve_loaded_sources_and_exact_symbol(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(
            ("EnergyModel.add_supply_system",), generator.TARGET_SYMBOLS
        )

        value = self.fixture()
        loaded = value["upstream"]["loaded_local_modules"]
        self.assertEqual(12, len(loaded))
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)
        self.assertEqual(
            [item["path"] for item in inventory["files"]],
            [item["path"] for item in loaded],
        )

    def test_cases_are_exact_sorted_and_bind_reviewed_adaptation(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(3, len(identifiers))
        self.assertEqual(3, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        for definition in definitions:
            self.assertEqual(
                {
                    "adaptation": "model-context-supply-system-assembly",
                    "outcome": "returned",
                },
                definition["expected_dotnet"],
            )

    def test_consumer_contract_closes_only_public_add_supply_system(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(
            {
                "EnergyModel.add_supply_system": (
                    "model-context-supply-system-assembly"
                )
            },
            contract["adaptations"],
        )
        self.assertEqual(
            {
                "EnergyModel.add_supply_system": (
                    "dragon-model-add-supply-system-174532d0"
                )
            },
            contract["assertion_ids"],
        )
        self.assertEqual(
            {"EnergyModel.add_supply_system": "exception"},
            contract["classifications"],
        )
        self.assertEqual(
            {"EnergyModel.add_supply_system": "EnergyModel.ToIdfDocument"},
            contract["native_targets"],
        )
        self.assertEqual(
            {
                "full_symbol_closure": False,
                "scope": "bounded-reviewed-adaptation-evidence",
                "unresolved_behavior": [
                    "EnergyModel.to_idf",
                    "SupplyGroup",
                    "concrete-supply-systems",
                    "supply-system-postprocessors",
                ],
            },
            contract["closure"],
        )
        self.assertEqual(
            ["EnergyModel.add_supply_system"], contract["target_symbols"]
        )
        self.assertNotIn("EnergyModel.to_idf", contract["classifications"])
        for case in value["cases"]:
            self.assertEqual("returned", case["expected_dotnet"]["outcome"])

    def test_append_then_processor_failure_preserves_mutation_and_prefix(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[0])
        facts = case["python"]["facts"]
        self.assertEqual("raised", case["python"]["outcome"])
        self.assertEqual("RuntimeError", facts["error"]["type"])
        self.assertEqual("processor-failure:", facts["error"]["message_prefix"])
        self.assertTrue(facts["error"]["message_starts_with_prefix"])
        self.assertEqual(1, facts["append_call_count"])
        self.assertEqual(
            "appended-before-processor-error", facts["mutation_state"]
        )
        self.assertEqual(
            [
                "Existing-Zone",
                "Failure-Appended-First",
                "Failure-Appended-Second",
            ],
            facts["zone_names_after"],
        )
        self.assertEqual(
            ["observer-before-failure", "failing-processor"],
            facts["processor_labels_run"],
        )
        self.assertFalse(facts["unreached_processor_ran"])
        self.assertEqual(
            [
                "supply.to_idf_object",
                "idf.append",
                "processor.run",
                "processor.run",
            ],
            [item["event"] for item in facts["events"]],
        )

    def test_generation_failure_precedes_all_mutation(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[1])
        facts = case["python"]["facts"]
        self.assertEqual("raised", case["python"]["outcome"])
        self.assertEqual("generation-failure:", facts["error"]["message_prefix"])
        self.assertTrue(facts["error"]["message_starts_with_prefix"])
        self.assertEqual(0, facts["append_call_count"])
        self.assertEqual(["Existing-Zone"], facts["zone_names_after"])
        self.assertEqual([], facts["processor_labels_run"])
        self.assertEqual(
            ["supply.to_idf_object"],
            [item["event"] for item in facts["events"]],
        )

    def test_success_returns_none_after_ordered_append_and_processors(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[2])
        facts = case["python"]["facts"]
        self.assertEqual("returned", case["python"]["outcome"])
        self.assertEqual({"kind": "none"}, facts["return"])
        self.assertEqual({"kind": "none"}, facts["error"])
        self.assertEqual(1, facts["append_call_count"])
        self.assertEqual(
            ["first-processor", "second-processor"],
            facts["processor_labels_run"],
        )
        self.assertEqual(
            [
                "supply.to_idf_object",
                "idf.append",
                "processor.run",
                "processor.run",
            ],
            [item["event"] for item in facts["events"]],
        )
        visible = facts["zone_names_after"]
        self.assertEqual(visible, facts["events"][2]["zone_names"])
        self.assertEqual(visible, facts["events"][3]["zone_names"])

    @unittest.skipUnless(
        all(
            (PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")).is_file()
            for source in generator.SOURCE_SPECS
        )
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_generation_is_byte_identical_twice_and_matches_fixture(self) -> None:
        bootstrap = (
            REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
        )
        outputs = [self.temp_root / "first.json", self.temp_root / "second.json"]
        environment = os.environ.copy()
        environment.update(
            {
                "PYTHONDONTWRITEBYTECODE": "1",
                "PYTHONHASHSEED": "0",
                "PYTHONUTF8": "1",
            }
        )
        for output in outputs:
            subprocess.run(
                [
                    sys.executable,
                    "-B",
                    "-X",
                    "utf8",
                    str(bootstrap),
                    "--dependency-root",
                    str(DEPENDENCY_ROOT),
                    "--upstream-source",
                    str(PINNED_SOURCE_ROOT),
                    "--generator",
                    str(GENERATOR_PATH),
                    "--",
                    "--inventory",
                    str(INVENTORY_PATH),
                    "--output",
                    str(output),
                    "--upstream-commit",
                    generator.EXPECTED_UPSTREAM_COMMIT,
                ],
                cwd=REPOSITORY_ROOT,
                env=environment,
                check=True,
                capture_output=True,
                text=True,
            )
        self.assertEqual(outputs[0].read_bytes(), outputs[1].read_bytes())
        self.assertEqual(FIXTURE_PATH.read_bytes(), outputs[0].read_bytes())

    @unittest.skipUnless(
        PINNED_SOURCE_ROOT.is_dir() and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_loaded_local_module_without_receipt_fails_closed(self) -> None:
        with self.assertRaisesRegex(SystemExit, "lacks an exact receipt"):
            with generator.SUPPORT._pinned_modules(PINNED_SOURCE_ROOT) as modules:
                imported_root = Path(modules.model.__file__).resolve().parents[2]
                rogue = imported_root / "idragon" / "review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_contract_source_symbol_and_semantic_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root"))
        case = self.fixture()
        case["cases"][0]["expected_dotnet"]["outcome"] = "raised"
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case contract"))
        consumer = self.fixture()
        consumer["consumer_contract"]["closure"]["full_symbol_closure"] = True
        changes.append((consumer, "consumer contract"))
        source = self.fixture()
        source["upstream"]["sources"][0]["source_sha256"] = "sha256:" + "0" * 64
        changes.append((source, "upstream"))
        loaded = self.fixture()
        loaded["upstream"]["loaded_local_modules"][0]["module"] = "idragon.wrong"
        changes.append((loaded, "upstream"))
        symbol = self.fixture()
        symbol["symbols"][0]["body_hash"] = "sha256:" + "0" * 64
        changes.append((symbol, "symbol"))
        semantic = self.fixture()
        semantic["cases"][0]["python"]["facts"]["append_call_count"] = 2
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_unsafe_values_raw_float_stale_hash_and_duplicate_keys_fail(self) -> None:
        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            ("C:\\private\\add-supply-system.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-26T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = unsafe
            changed["cases_sha256"] = generator.cases_sha256(changed["cases"])
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        floating = self.fixture()
        floating["cases"][0]["python"]["facts"]["unsafe"] = 1.5
        floating["cases_sha256"] = generator.cases_sha256(floating["cases"])
        with self.assertRaisesRegex(RuntimeError, "Raw float"):
            generator.validate_oracle(floating)

        stale = self.fixture()
        stale["cases"][0]["python"]["facts"]["append_call_count"] = 2
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.load_json_without_duplicates(duplicate)


if __name__ == "__main__":
    unittest.main()

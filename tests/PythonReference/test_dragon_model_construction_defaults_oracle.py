"""Fail-closed tests for the dragon-model construction/defaults oracle."""

from __future__ import annotations

from collections import Counter
import importlib.util
import os
from pathlib import Path
import subprocess
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
    / "generate_dragon_model_construction_defaults_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-model-construction-defaults-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_model_construction_defaults_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load construction-defaults generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 21_799
EXPECTED_FIXTURE_SHA256 = (
    "sha256:2ecefb4e37eac9a67dfc7545d3bcc4480682598acf7fa04d19f4f452d2dc685b"
)
EXPECTED_CASES_SHA256 = (
    "sha256:7a2c84fc965b884bd93d4e4f12bfab5df03e491b50dd8ed8ecda9eb4d6b21c84"
)


class DragonModelConstructionDefaultsOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-model-construction-defaults-tests-"
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

    def test_fixture_is_exact_utf8_strict_and_self_validating(self) -> None:
        value = self.fixture()
        raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertTrue(raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", raw)
        self.assertIn("용".encode("utf-8"), raw)
        self.assertIn("🐉".encode("utf-8"), raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            raw.decode("utf-8"),
        )

    def test_inventory_binds_five_sources_and_two_exact_symbols(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(
            generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"]
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(
            [
                "src/idragon/common.py",
                "src/idragon/constants.py",
                "src/idragon/dragon/model.py",
                "src/idragon/dragon/profile.py",
                "src/idragon/imugi.py",
            ],
            [item["path"] for item in inventory["files"]],
        )

    def test_cases_are_sorted_unique_and_have_exact_per_symbol_counts(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(9, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )

    def test_consumer_contract_has_one_equivalent_and_one_adaptation(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(
            {
                "EnergyModel.__init__": (
                    "immutable-validated-energy-model-construction"
                )
            },
            contract["adaptations"],
        )
        self.assertEqual(generator.EXPECTED_ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(
            {
                "EnergyModel.__init__": "exception",
                "EnergyModel.create_default_idf": "equivalent",
            },
            contract["classifications"],
        )
        for case in value["cases"]:
            if case["symbol"] == "EnergyModel.__init__":
                self.assertEqual(
                    "immutable-validated-energy-model-construction",
                    case["expected_dotnet"]["adaptation"],
                )
            else:
                self.assertNotIn("expected_dotnet", case)

    def test_fixture_pins_constructor_aliases_and_default_idf_fields(self) -> None:
        value = self.fixture()
        order = self.case(
            value,
            "dragon-model-construction-defaults.create-default-idf."
            "exact-family-order-count",
        )["python"]["facts"]
        self.assertEqual(17, order["object_count"])
        self.assertEqual(0, order["building_object_count"])
        self.assertFalse(order["ensure_validity"])
        self.assertEqual([24, 2, 0], order["version_components"])
        self.assertEqual(
            [
                "Version",
                "SimulationControl",
                "Timestep",
                "SizingPeriod:WeatherFileDays",
                "SizingPeriod:WeatherFileDays",
                "RunPeriod",
                "ScheduleTypeLimits",
                "ScheduleTypeLimits",
                "ScheduleTypeLimits",
                "ScheduleTypeLimits",
                "Schedule:Compact",
                "Schedule:Compact",
                "Schedule:Constant",
                "GlobalGeometryRules",
                "Output:Table:SummaryReports",
                "Output:Table:Monthly",
                "OutputControl:Table:Style",
            ],
            order["flat_object_types"],
        )

        raw_fields = self.case(
            value,
            "dragon-model-construction-defaults.create-default-idf."
            "global-schedule-raw-fields",
        )["python"]["facts"]
        self.assertEqual(
            ["UpperLeftCorner", "Counterclockwise", "World", "Relative", "Relative"],
            [item["value"] for item in raw_fields["global_geometry_rules"]],
        )
        self.assertEqual(
            {"enum_type": "ScheduleType", "kind": "enum", "text": "real", "value": "real"},
            raw_fields["people_activity"]["values"][1],
        )
        self.assertEqual(
            {"kind": "float", "repr": "107.0"},
            raw_fields["people_activity"]["values"][2],
        )
        self.assertEqual(
            {"kind": "none"}, raw_fields["compact_schedules"][0]["values"][1]
        )

        aliases = self.case(
            value,
            "dragon-model-construction-defaults.init.explicit-aliasing",
        )["python"]["facts"]
        self.assertTrue(aliases["explicit_zone_is_input_list"])
        self.assertTrue(aliases["explicit_pv_is_input_list"])
        self.assertTrue(aliases["input_mutation_visible_in_model"])
        self.assertTrue(aliases["model_mutation_visible_in_input"])

        shared = self.case(
            value,
            "dragon-model-construction-defaults.init.shared-defaults-signature",
        )["python"]["facts"]
        self.assertTrue(shared["first_zone_is_second_zone"])
        self.assertTrue(shared["first_pv_is_second_pv"])
        self.assertTrue(shared["shared_zone_default_restored"])
        self.assertTrue(shared["shared_pv_default_restored"])

    @unittest.skipUnless(
        all(
            (
                PINNED_SOURCE_ROOT
                / Path(source["path"]).relative_to("src")
            ).is_file()
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

    def test_root_contract_runtime_source_symbol_and_semantic_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root"))
        case = self.fixture()
        case["cases"][0]["executor"] = "wrong"
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "contract"))
        contract = self.fixture()
        contract["consumer_contract"]["raw_field_encoding"] = "wrong"
        changes.append((contract, "consumer contract"))
        runtime = self.fixture()
        runtime["runtime"]["python_hash_seed"] = 1
        changes.append((runtime, "runtime"))
        source = self.fixture()
        source["upstream"]["sources"][0]["source_sha256"] = "sha256:" + "0" * 64
        changes.append((source, "upstream"))
        symbol = self.fixture()
        symbol["symbols"][0]["body_hash"] = "sha256:" + "0" * 64
        changes.append((symbol, "symbol"))
        semantic = self.fixture()
        semantic["cases"][1]["python"]["facts"]["object_count"] = 18
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_unsafe_values_raw_float_stale_hash_and_duplicate_keys_fail(self) -> None:
        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            ("C:\\private\\construction-defaults.json", "Absolute path"),
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

        stale_hash = self.fixture()
        stale_hash["cases"][0]["python"]["facts"]["signature_text"] = "changed"
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale_hash)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.SUPPORT.load_json_without_duplicates(duplicate)


if __name__ == "__main__":
    unittest.main()

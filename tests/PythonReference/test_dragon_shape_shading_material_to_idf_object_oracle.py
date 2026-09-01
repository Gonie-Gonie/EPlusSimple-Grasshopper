"""Fail-closed tests for the bounded shading-material IDF leaf oracle."""

from __future__ import annotations

from collections import Counter
import importlib.util
import math
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
    / "generate_dragon_shape_shading_material_to_idf_object_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-shape-shading-material-to-idf-object-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_shape_shading_material_to_idf_object_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load shading-material generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 56_701
EXPECTED_FIXTURE_SHA256 = (
    "sha256:364652ab7190f1d55d8f85227ff388d42b838e713ae193f2100ca6dbdb18a5db"
)
EXPECTED_CASES_SHA256 = (
    "sha256:e577eebfb5c6ad65670bc3ae9624d77eec2d2f3e21d0d518c25f78cde2459f92"
)


class DragonShapeShadingMaterialToIdfObjectOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-shape-shading-material-to-idf-object-tests-"
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

    @staticmethod
    def fields(case: dict[str, object], key: str = "emission") -> list[dict[str, object]]:
        return case["python"]["facts"][key]["ordered_fields"]

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

    def test_inventory_binds_twelve_loaded_sources_and_two_exact_leaf_symbols(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(
            ("Blind.to_idf_object", "Shade.to_idf_object"),
            generator.TARGET_SYMBOLS,
        )
        self.assertEqual(
            [1027, 1032],
            [
                next(
                    index
                    for index, symbol in enumerate(
                        generator.load_json_without_duplicates(INVENTORY_PATH)["symbols"]
                    )
                    if symbol["symbol"] == target
                    and symbol["path"] == generator.SHAPE_SOURCE_PATH
                )
                for target in generator.TARGET_SYMBOLS
            ],
        )

        value = self.fixture()
        loaded = value["upstream"]["loaded_local_modules"]
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)
        self.assertEqual(
            [item["path"] for item in inventory["files"]],
            [item["path"] for item in loaded],
        )

    def test_case_set_is_sorted_balanced_and_marks_validation_context(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(6, len(identifiers))
        self.assertEqual(6, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        self.assertEqual(
            [
                "returned",
                "constructor-rejected",
                "returned",
                "returned",
                "constructor-rejected",
                "returned",
            ],
            [item["expected_dotnet"]["outcome"] for item in definitions],
        )
        self.assertTrue(
            all(
                item["expected_dotnet"]["adaptation"] == generator.ADAPTATION
                for item in definitions
            )
        )

    def test_consumer_contract_keeps_context_and_parent_assembly_unresolved(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {
                "Blind.to_idf_object": generator.ADAPTATION,
                "Shade.to_idf_object": generator.ADAPTATION,
            },
            contract["adaptations"],
        )
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(
            {symbol: "exception" for symbol in generator.TARGET_SYMBOLS},
            contract["classifications"],
        )
        self.assertFalse(contract["closure"]["full_symbol_closure"])
        context_only = contract["closure"]["context_only_not_targeted"]
        for symbol in (
            "Blind",
            "Blind.__init__",
            "Shade",
            "Shade.__init__",
            "Shading",
            "IdfObject",
            "IdfObject.__init__",
        ):
            self.assertIn(symbol, context_only)
            self.assertNotIn(symbol, contract["classifications"])
        for boundary in (
            "Surface.to_idf_object",
            "WindowShadingControl-emission",
            "EnergyModel.to_idf",
        ):
            self.assertIn(boundary, contract["closure"]["unresolved_behavior"])

    def test_blind_cases_pin_complete_field_order_freshness_and_alias_context(self) -> None:
        value = self.fixture()
        expected_names = [item["name"] for item in generator._blind_fields(
            "name", 0.05, 0.04, 45.0, 0.6, 0.4
        )]
        self.assertEqual(29, len(expected_names))
        self.assertEqual("Name", expected_names[0])
        self.assertEqual("Maximum Slat Angle", expected_names[-1])

        for identifier in generator.EXPECTED_CASE_IDS[:3]:
            with self.subTest(identifier=identifier):
                case = self.case(value, identifier)
                facts = case["python"]["facts"]
                emission = facts["emission"]
                self.assertEqual("WindowMaterial:Blind", emission["first_object_type"])
                self.assertEqual(expected_names, [item["name"] for item in self.fields(case)])
                self.assertEqual(1, emission["object_count"])
                self.assertTrue(emission["fresh_result_list"])
                self.assertTrue(emission["fresh_idf_object"])
                self.assertTrue(emission["same_idd_definition"])
                self.assertTrue(emission["second_fields_equal"])
                self.assertTrue(facts["constructor_context"]["input_identity_preserved"])
                self.assertTrue(
                    facts["constructor_context"]["state_unchanged_after_two_emissions"]
                )

        permissive = self.case(value, generator.EXPECTED_CASE_IDS[1])
        conditions = permissive["python"]["facts"]["input_conditions"]
        self.assertFalse(conditions["dimensions_positive"])
        self.assertFalse(conditions["reflectances_in_unit_interval"])
        fields = self.fields(permissive)
        self.assertEqual("0.0", fields[2]["value"]["repr"])
        self.assertEqual("-0.5", fields[3]["value"]["repr"])
        self.assertEqual("1.25", fields[8]["value"]["repr"])
        self.assertEqual("-0.25", fields[9]["value"]["repr"])

    def test_shade_cases_pin_computed_emissivity_defaults_and_failure_timing(self) -> None:
        value = self.fixture()
        expected_names = [item["name"] for item in generator._shade_fields(
            "name", 0.2, 0.3, 0.5
        )]
        self.assertEqual(15, len(expected_names))
        self.assertEqual("Name", expected_names[0])
        self.assertEqual("Airflow Permeability", expected_names[-1])

        for index in (3, 5):
            case = self.case(value, generator.EXPECTED_CASE_IDS[index])
            emission = case["python"]["facts"]["emission"]
            self.assertEqual("WindowMaterial:Shade", emission["first_object_type"])
            self.assertEqual(expected_names, [item["name"] for item in self.fields(case)])
            self.assertTrue(emission["fresh_result_list"])
            self.assertTrue(emission["fresh_idf_object"])

        representative = self.fields(
            self.case(value, generator.EXPECTED_CASE_IDS[5])
        )
        self.assertEqual("0.5", representative[5]["value"]["repr"])
        self.assertEqual("0.01", representative[7]["value"]["repr"])
        self.assertEqual("100.0", representative[8]["value"]["repr"])

        invalid = self.case(value, generator.EXPECTED_CASE_IDS[4])["python"]["facts"]
        self.assertFalse(
            invalid["numeric_input_conditions"]["sum_not_greater_than_one"]
        )
        invalid_fields = invalid["numeric_permissive_emission"]["emission"][
            "ordered_fields"
        ]
        self.assertEqual("-0.20000000000000007", invalid_fields[5]["value"]["repr"])
        self.assertEqual(
            {
                "args": ["unsupported operand type(s) for -: 'int' and 'str'"],
                "message": "unsupported operand type(s) for -: 'int' and 'str'",
                "outcome": "raised",
                "type": "TypeError",
            },
            invalid["nonnumeric_to_idf"],
        )

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
                imported_root = Path(modules.shape.__file__).resolve().parents[2]
                rogue = imported_root / "idragon" / "shading_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.shading_review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_schema_contract_case_runtime_source_symbol_and_semantics_tamper_fail(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        schema = self.fixture()
        schema["schema"] = "wrong"
        changes.append((schema, "schema"))
        contract = self.fixture()
        contract["consumer_contract"]["closure"]["full_symbol_closure"] = True
        changes.append((contract, "consumer contract"))
        case = self.fixture()
        case["cases"][0]["executor"] = "wrong"
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case contract"))
        runtime = self.fixture()
        runtime["runtime"]["python_version"] = "3.12.8"
        changes.append((runtime, "runtime"))
        source = self.fixture()
        source["upstream"]["sources"][0]["source_sha256"] = "sha256:" + "0" * 64
        changes.append((source, "upstream"))
        symbol = self.fixture()
        symbol["symbols"][0]["symbol_hash"] = "sha256:" + "0" * 64
        changes.append((symbol, "symbol"))
        semantic = self.fixture()
        semantic["cases"][0]["python"]["facts"]["emission"]["object_count"] = 2
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_values_and_nonfinite_fail(self) -> None:
        stale = self.fixture()
        stale["cases"][0]["python"]["facts"]["emission"]["object_count"] = 2
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.load_json_without_duplicates(duplicate)

        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            ("C:\\private\\shading.json", "Absolute path"),
            ("/home/private/shading.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-26T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = unsafe
            changed["cases_sha256"] = generator.cases_sha256(changed["cases"])
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            self.assertFalse(math.isfinite(nonfinite))
            changed = self.fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaisesRegex(ValueError, "Out of range float"):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

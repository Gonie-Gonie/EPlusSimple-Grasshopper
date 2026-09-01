"""Fail-closed tests for the bounded construction-family IDF oracle."""

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
    / "generate_dragon_construction_to_idf_object_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-construction-to-idf-object-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_construction_to_idf_object_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load construction IDF generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 63_253
EXPECTED_FIXTURE_SHA256 = (
    "sha256:0ca44ff38d80f388b9dea241f3fa81f490e4535f8eacbdd7a485da960bd14bd7"
)
EXPECTED_CASES_SHA256 = (
    "sha256:c99cd6cf0fabfa45e599866d7acb8be22d9c7d7d6d6ab13b8732ad811291cbf5"
)


class DragonConstructionToIdfObjectOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-construction-to-idf-object-tests-"
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
    def record(
        case: dict[str, object], object_index: int = 0
    ) -> dict[str, object]:
        return case["python"]["facts"]["emission"]["first_object_records"][
            object_index
        ]

    @classmethod
    def fields(
        cls, case: dict[str, object], object_index: int = 0
    ) -> list[dict[str, object]]:
        return cls.record(case, object_index)["ordered_fields"]

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

    def test_inventory_binds_twelve_sources_and_five_exact_methods(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(
            (
                "AirBoundary.to_idf_object",
                "Construction.to_idf_object",
                "Glazing.to_idf_object",
                "Layer.to_idf_object",
                "NoMassConstruction.to_idf_object",
            ),
            generator.TARGET_SYMBOLS,
        )
        public_symbols = generator.load_json_without_duplicates(INVENTORY_PATH)[
            "symbols"
        ]
        self.assertEqual(
            [592, 601, 608, 617, 640],
            [
                next(
                    index
                    for index, receipt in enumerate(public_symbols)
                    if receipt["symbol"] == target
                    and receipt["path"] == generator.CONSTRUCTION_SOURCE_PATH
                )
                for target in generator.TARGET_SYMBOLS
            ],
        )

        loaded = self.fixture()["upstream"]["loaded_local_modules"]
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)
        self.assertEqual(
            [item["path"] for item in inventory["files"]],
            [item["path"] for item in loaded],
        )

    def test_case_set_is_sorted_balanced_and_uses_distinct_adaptations(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(10, len(identifiers))
        self.assertEqual(10, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        self.assertEqual(
            set(generator.TARGET_SYMBOLS), set(generator.ADAPTATIONS)
        )
        self.assertEqual(5, len(set(generator.ADAPTATIONS.values())))
        self.assertTrue(
            all(
                item["expected_dotnet"]
                == {
                    "adaptation": generator.ADAPTATIONS[item["symbol"]],
                    "outcome": "returned",
                }
                for item in definitions
            )
        )

    def test_consumer_contract_keeps_nonleaf_and_native_model_rules_open(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(
            {symbol: "exception" for symbol in generator.TARGET_SYMBOLS},
            contract["classifications"],
        )
        self.assertIn(
            "private EnergyModelIdfAssembler model context",
            contract["classification_basis"],
        )
        self.assertIn("compacts default fields", contract["classification_basis"])
        self.assertIn("deduplicates shared definitions", contract["classification_basis"])
        self.assertIn("standalone mutable-list parity", contract["classification_basis"])
        self.assertFalse(contract["closure"]["full_symbol_closure"])
        context_only = contract["closure"]["context_only_not_targeted"]
        for symbol in (
            "AirBoundary",
            "AirBoundary.__init__",
            "Construction",
            "Construction.__init__",
            "Construction.__eq__",
            "Construction.__hash__",
            "Glazing",
            "Glazing.__init__",
            "Glazing.U",
            "Glazing.G",
            "Layer",
            "Layer.__init__",
            "Layer.material",
            "Layer.thickness",
            "Layer.__eq__",
            "Layer.__hash__",
            "NoMassConstruction",
            "NoMassConstruction.__init__",
            "NoMassConstruction.U",
        ):
            self.assertIn(symbol, context_only)
            self.assertNotIn(symbol, contract["classifications"])
        unresolved = contract["closure"]["unresolved_behavior"]
        for boundary in (
            "invalid-domain-and-error-semantics",
            "IdfObject",
            "IdfObject.__init__",
            "Surface",
            "Surface.to_idf_object",
            "Zone",
            "Zone.to_idf_object",
            "EnergyModel.to_idf",
            "native-model-deduplication-and-conflict-semantics",
        ):
            self.assertIn(boundary, unresolved)

    def test_all_cases_pin_complete_fields_freshness_and_unchanged_state(self) -> None:
        value = self.fixture()
        for identifier in generator.EXPECTED_CASE_IDS:
            with self.subTest(identifier=identifier):
                case = self.case(value, identifier)
                facts = case["python"]["facts"]
                context = facts["input_context"]
                emission = facts["emission"]
                self.assertEqual(
                    "properties-read-by-target-method",
                    context["captured_state_scope"],
                )
                self.assertTrue(
                    context["source_state_unchanged_after_two_emissions"]
                )
                self.assertTrue(emission["all_allowed_fields_covered_in_order"])
                self.assertTrue(emission["fresh_return_value"])
                self.assertTrue(emission["first_objects_pairwise_distinct"])
                self.assertTrue(emission["second_objects_pairwise_distinct"])
                self.assertEqual(
                    emission["object_count"], len(emission["first_object_records"])
                )
                self.assertEqual(
                    emission["object_types"],
                    [item["object_type"] for item in emission["first_object_records"]],
                )
                self.assertTrue(all(emission["fresh_idf_object_flags"]))
                self.assertTrue(all(emission["same_idd_definition_flags"]))
                self.assertTrue(all(emission["second_fields_equal_flags"]))
                for record in emission["first_object_records"]:
                    self.assertEqual(
                        record["field_count"], len(record["ordered_fields"])
                    )

        for index in (6, 7):
            layer = self.case(value, generator.EXPECTED_CASE_IDS[index])
            emission = layer["python"]["facts"]["emission"]
            self.assertEqual("IdfObject", emission["result_type"])
            self.assertIsNone(emission["fresh_result_list"])
        for index in (*range(0, 6), 8, 9):
            listed = self.case(value, generator.EXPECTED_CASE_IDS[index])
            emission = listed["python"]["facts"]["emission"]
            self.assertEqual("list", emission["result_type"])
            self.assertTrue(emission["fresh_result_list"])

    def test_air_boundary_cases_pin_all_four_fields_and_blank_schedule(self) -> None:
        value = self.fixture()
        for index, expected_ach in ((0, "1.25"), (1, "0.5")):
            case = self.case(value, generator.EXPECTED_CASE_IDS[index])
            record = self.record(case)
            fields = record["ordered_fields"]
            self.assertEqual("Construction:AirBoundary", record["object_type"])
            self.assertEqual(4, record["field_count"])
            self.assertEqual(
                [
                    "Name",
                    "Air Exchange Method",
                    "Simple Mixing Air Changes per Hour",
                    "Simple Mixing Schedule Name",
                ],
                [item["name"] for item in fields],
            )
            self.assertEqual("SimpleMixing", fields[1]["value"]["value"])
            self.assertEqual(expected_ach, fields[2]["value"]["repr"])
            self.assertEqual({"kind": "none"}, fields[3]["value"])

    def test_construction_cases_pin_surface_scoped_name_layers_and_padding(self) -> None:
        value = self.fixture()
        multi = self.fields(self.case(value, generator.EXPECTED_CASE_IDS[2]))
        single = self.fields(self.case(value, generator.EXPECTED_CASE_IDS[3]))
        expected_names = ["Name", "Outside Layer"] + [
            f"Layer {index}" for index in range(2, 11)
        ]
        self.assertEqual(11, len(multi))
        self.assertEqual(expected_names, [item["name"] for item in multi])
        self.assertEqual(
            "Wall Assembly:for:South Wall", multi[0]["value"]["value"]
        )
        self.assertEqual(
            [
                "Exterior Render 20mm",
                "Structural Core 180mm",
                "Interior Finish 13mm",
            ],
            [item["value"]["value"] for item in multi[1:4]],
        )
        self.assertTrue(all(item["value"] == {"kind": "none"} for item in multi[4:]))
        self.assertEqual("Roof Assembly:for:Roof Plane", single[0]["value"]["value"])
        self.assertEqual("Roof Insulation 200mm", single[1]["value"]["value"])
        self.assertTrue(
            all(item["value"] == {"kind": "none"} for item in single[2:])
        )

    def test_glazing_cases_pin_material_construction_and_optional_visible_field(self) -> None:
        value = self.fixture()
        for index, expected_name, expected_u, expected_g in (
            (4, "Clear Glazing", "2.75", "0.625"),
            (5, "Triple Glazing", "0.8", "0.45"),
        ):
            case = self.case(value, generator.EXPECTED_CASE_IDS[index])
            emission = case["python"]["facts"]["emission"]
            self.assertEqual(
                ["WindowMaterial:SimpleGlazingSystem", "Construction"],
                emission["object_types"],
            )
            material = self.fields(case, 0)
            construction = self.fields(case, 1)
            expected_material_name = f"$GLAZING_FOR${expected_name}"
            self.assertEqual(4, len(material))
            self.assertEqual(expected_material_name, material[0]["value"]["value"])
            self.assertEqual(expected_u, material[1]["value"]["repr"])
            self.assertEqual(expected_g, material[2]["value"]["repr"])
            self.assertEqual({"kind": "none"}, material[3]["value"])
            self.assertEqual(expected_name, construction[0]["value"]["value"])
            self.assertEqual(
                expected_material_name, construction[1]["value"]["value"]
            )
            self.assertEqual(11, len(construction))

    def test_layer_cases_pin_nine_material_fields_and_raw_result_shape(self) -> None:
        value = self.fixture()
        expected_names = [
            "Name",
            "Roughness",
            "Thickness",
            "Conductivity",
            "Density",
            "Specific Heat",
            "Thermal Absorptance",
            "Solar Absorptance",
            "Visible Absorptance",
        ]
        alternate = self.case(value, generator.EXPECTED_CASE_IDS[6])
        representative = self.case(value, generator.EXPECTED_CASE_IDS[7])
        for case in (alternate, representative):
            record = self.record(case)
            self.assertEqual("Material", record["object_type"])
            self.assertEqual(9, record["field_count"])
            self.assertEqual(expected_names, [item["name"] for item in self.fields(case)])
        alternate_fields = self.fields(alternate)
        self.assertEqual("Smooth", alternate_fields[1]["value"]["value"])
        self.assertEqual("0.08", alternate_fields[2]["value"]["repr"])
        self.assertEqual("0.125", alternate_fields[3]["value"]["repr"])
        representative_fields = self.fields(representative)
        self.assertEqual("MediumRough", representative_fields[1]["value"]["value"])
        self.assertEqual("2300.0", representative_fields[4]["value"]["repr"])

    def test_no_mass_cases_pin_resistance_defaults_and_linked_construction(self) -> None:
        value = self.fixture()
        for index, expected_name, expected_resistance in (
            (8, "Light Partition", "0.5"),
            (9, "Insulated Panel", "4.0"),
        ):
            case = self.case(value, generator.EXPECTED_CASE_IDS[index])
            emission = case["python"]["facts"]["emission"]
            self.assertEqual(
                ["Material:NoMass", "Construction"], emission["object_types"]
            )
            material = self.fields(case, 0)
            construction = self.fields(case, 1)
            material_name = f"$MaterialFor$_{expected_name}"
            self.assertEqual(6, len(material))
            self.assertEqual(material_name, material[0]["value"]["value"])
            self.assertEqual("Rough", material[1]["value"]["value"])
            self.assertEqual(expected_resistance, material[2]["value"]["repr"])
            self.assertEqual(
                ["0.9", "0.7", "0.7"],
                [item["value"]["repr"] for item in material[3:]],
            )
            self.assertEqual(expected_name, construction[0]["value"]["value"])
            self.assertEqual(material_name, construction[1]["value"]["value"])
            self.assertTrue(
                all(
                    item["value"] == {"kind": "none"}
                    for item in construction[2:]
                )
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
                rogue = imported_root / "idragon" / "construction_idf_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.construction_idf_review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_schema_contract_case_runtime_source_symbol_and_semantics_tamper_fail(
        self,
    ) -> None:
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
            ("C:\\private\\construction.json", "Absolute path"),
            ("/home/private/construction.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-27T12:34:56", "Timestamp"),
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

"""Fail-closed tests for the bounded HVAC source-system IDF oracle."""

from __future__ import annotations

from collections import Counter
import copy
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
    / "generate_dragon_hvac_source_system_to_idf_object_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-hvac-source-system-to-idf-object-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_hvac_source_system_to_idf_object_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load source-system IDF generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 3_927_647
EXPECTED_FIXTURE_SHA256 = (
    "sha256:c8518ee123b04c9f554190d80ad2943e1f67ed07ca67b472e2345ca14497aebb"
)
EXPECTED_CASES_SHA256 = (
    "sha256:8eb4666decd0c64f39d756fe758fff56d0f48aa7217e8b0d0cace6b9f209b2a8"
)


class DragonHvacSourceSystemToIdfObjectOracleTests(unittest.TestCase):
    fixture_value: dict[str, object]

    @classmethod
    def setUpClass(cls) -> None:
        cls.fixture_value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(cls.fixture_value)

    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-hvac-source-system-idf-tests-"
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @classmethod
    def fixture(cls) -> dict[str, object]:
        return cls.fixture_value

    @classmethod
    def changed_fixture(cls) -> dict[str, object]:
        return copy.deepcopy(cls.fixture_value)

    @classmethod
    def case(cls, identifier: str) -> dict[str, object]:
        return next(
            item for item in cls.fixture_value["cases"] if item["id"] == identifier
        )

    @staticmethod
    def emission(case: dict[str, object]) -> dict[str, object]:
        return case["python"]["facts"]["emission"]

    @classmethod
    def records(cls, case: dict[str, object]) -> list[dict[str, object]]:
        return cls.emission(case)["first_object_records"]

    @staticmethod
    def field(record: dict[str, object], name: str) -> dict[str, object]:
        matches = [
            item["value"] for item in record["ordered_fields"] if item["name"] == name
        ]
        if len(matches) != 1:
            raise AssertionError(
                f"Expected one {name!r} field in {record['object_type']}: {len(matches)}"
            )
        return matches[0]

    @classmethod
    def named_record(
        cls, case: dict[str, object], object_type: str, name: str
    ) -> dict[str, object]:
        matches = [
            record
            for record in cls.records(case)
            if record["object_type"] == object_type
            and cls.field(record, "Name") == generator._encode(name)
        ]
        if len(matches) != 1:
            raise AssertionError(
                f"Expected one {object_type} named {name!r}: {len(matches)}"
            )
        return matches[0]

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

    def test_inventory_binds_twelve_sources_and_thirteen_exact_methods(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(13, len(generator.TARGET_SYMBOLS))
        public_symbols = generator.load_json_without_duplicates(INVENTORY_PATH)[
            "symbols"
        ]
        self.assertEqual(
            [644, 655, 656, 660, 663, 666, 672, 684, 685, 743, 746, 749, 788],
            [
                next(
                    index
                    for index, receipt in enumerate(public_symbols)
                    if receipt["symbol"] == target
                    and receipt["path"] == generator.HVAC_SOURCE_PATH
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

    def test_case_matrix_is_sorted_exact_and_uses_distinct_adaptations(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(20, len(identifiers))
        self.assertEqual(20, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(generator.ADAPTATIONS))
        self.assertEqual(13, len(set(generator.ADAPTATIONS.values())))
        self.assertEqual(13, len(set(generator.ASSERTION_IDS.values())))
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

    def test_consumer_contract_keeps_recommended_boundaries_open(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(
            {symbol: "exception" for symbol in generator.TARGET_SYMBOLS},
            contract["classifications"],
        )
        self.assertFalse(contract["closure"]["full_symbol_closure"])
        unresolved = contract["closure"]["unresolved_behavior"]
        for boundary in (
            "all-related-constructors-properties-and-enums",
            "invalid-domain-nonfinite-and-duck-typed-error-semantics",
            "GeothermalHeatPump",
            "native-DistrictHeating",
            "general-terminal-and-demand-connection-enrichment",
            "IdfObject",
            "isolated-IdfObject-and-IDD-default-policy",
            "parent-EnergyModel-global-order-deduplication-and-conflicts",
            "safe-native-screw-compressor-behavior",
            "active-absorption-runtime-parity",
        ):
            self.assertIn(boundary, unresolved)
        self.assertEqual(
            "external-temporary-copy-with-complete-twelve-module-audit",
            contract["source_import_policy"],
        )

    def test_every_concrete_case_pins_complete_fields_freshness_and_state(self) -> None:
        abstract_ids = {
            generator.EXPECTED_CASE_IDS[12],
            generator.EXPECTED_CASE_IDS[19],
        }
        total_objects = 0
        total_fields = 0
        for identifier in generator.EXPECTED_CASE_IDS:
            case = self.case(identifier)
            facts = case["python"]["facts"]
            context = facts["input_context"]
            emission = facts["emission"]
            with self.subTest(identifier=identifier):
                self.assertTrue(
                    context["source_state_unchanged_after_two_emissions"]
                )
                self.assertEqual(
                    emission["object_count"], len(emission["first_object_records"])
                )
                self.assertEqual(
                    emission["object_types"],
                    [item["object_type"] for item in emission["first_object_records"]],
                )
                self.assertEqual(
                    generator.EXPECTED_FACT_SHA256[identifier],
                    generator.canonical_sha256(facts),
                )
                self.assertEqual(
                    generator.EXPECTED_OBJECT_TYPES[identifier],
                    tuple(emission["object_types"]),
                )
                for record in emission["first_object_records"]:
                    self.assertEqual(
                        record["field_count"], len(record["ordered_fields"])
                    )
                    self.assertGreater(record["field_count"], 0)
                if identifier in abstract_ids:
                    self.assertEqual("NoneType", emission["result_type"])
                    self.assertEqual({"kind": "none"}, emission["first_return"])
                    self.assertEqual(0, emission["object_count"])
                    self.assertFalse(emission["fresh_return_value"])
                else:
                    self.assertEqual("list", emission["result_type"])
                    self.assertTrue(emission["all_allowed_fields_covered_in_order"])
                    self.assertTrue(emission["fresh_result_list"])
                    self.assertTrue(emission["fresh_return_value"])
                    self.assertTrue(emission["first_objects_pairwise_distinct"])
                    self.assertTrue(emission["second_objects_pairwise_distinct"])
                    self.assertTrue(all(emission["fresh_idf_object_flags"]))
                    self.assertTrue(all(emission["same_idd_definition_flags"]))
                    self.assertTrue(all(emission["second_fields_equal_flags"]))
            total_objects += emission["object_count"]
            total_fields += sum(
                record["field_count"] for record in emission["first_object_records"]
            )
        self.assertEqual(519, total_objects)
        self.assertEqual(18_670, total_fields)

    def test_heat_pump_cases_pin_curve_family_capacities_fuel_and_terminal_link(self) -> None:
        for index, capacity, fuel in (
            (17, 58000.0, "NaturalGas"),
            (18, "autosize", "Electricity"),
        ):
            case = self.case(generator.EXPECTED_CASE_IDS[index])
            emission = self.emission(case)
            self.assertEqual(22, emission["object_count"])
            self.assertEqual("ZoneTerminalUnitList", emission["object_types"][-2])
            self.assertEqual(
                "AirConditioner:VariableRefrigerantFlow",
                emission["object_types"][-1],
            )
            outdoor = self.records(case)[-1]
            terminal = self.records(case)[-2]
            self.assertEqual(
                generator._encode(capacity),
                self.field(outdoor, "Gross Rated Total Cooling Capacity"),
            )
            self.assertEqual(generator._encode(fuel), self.field(outdoor, "Fuel Type"))
            self.assertEqual(
                self.field(terminal, "Zone Terminal Unit List Name"),
                self.field(outdoor, "Zone Terminal Unit List Name"),
            )

    def test_compressor_cases_pin_three_curve_orders_and_screw_bicubic(self) -> None:
        expected = (
            (7, "Curve:Quadratic", "0.9441897"),
            (8, "Curve:Bicubic", "0.907133913"),
            (9, "Curve:Quadratic", "0.257183345"),
        )
        for index, tail_type, first_coefficient in expected:
            case = self.case(generator.EXPECTED_CASE_IDS[index])
            records = self.records(case)
            self.assertEqual(
                ["Curve:Biquadratic", "Curve:Biquadratic", tail_type],
                [item["object_type"] for item in records],
            )
            self.assertEqual(
                first_coefficient,
                self.field(records[0], "Coefficient1 Constant")["repr"],
            )
            self.assertEqual(
                "Curve_for_Chiller_named_Curve Context:CoolingCOPPLR",
                self.field(records[2], "Name")["value"],
            )

    def test_tower_main_and_full_cases_pin_capacity_fallback_and_loop_order(self) -> None:
        main_types = (
            (13, "FluidCooler:SingleSpeed", 91000.0),
            (14, "FluidCooler:TwoSpeed", None),
            (15, "CoolingTower:SingleSpeed", 1e6),
            (16, "CoolingTower:TwoSpeed", None),
        )
        for index, object_type, capacity in main_types:
            case = self.case(generator.EXPECTED_CASE_IDS[index])
            record = self.records(case)[0]
            self.assertEqual(object_type, record["object_type"])
            if capacity is not None:
                self.assertEqual(
                    generator._encode(capacity),
                    self.field(record, "Nominal Capacity"),
                )
        for index, first_type in (
            (10, "FluidCooler:TwoSpeed"),
            (11, "CoolingTower:SingleSpeed"),
        ):
            case = self.case(generator.EXPECTED_CASE_IDS[index])
            types = self.emission(case)["object_types"]
            self.assertEqual(29, len(types))
            self.assertEqual(first_type, types[0])
            self.assertEqual("Pump:VariableSpeed", types[1])
            self.assertEqual(["CondenserLoop", "Sizing:Plant"], types[-2:])

    def test_chiller_and_absorption_pin_input_schedule_but_sizing_stays_six(self) -> None:
        cases = (
            (5, "Loop_for_Alternate Chiller", 9.25),
            (6, "Loop_for_Representative Chiller", 6.0),
            (0, "Loop_for_Alternate Absorber", 8.5),
            (1, "Loop_for_Representative Absorber", 6.0),
        )
        for index, loop_name, schedule_value in cases:
            case = self.case(generator.EXPECTED_CASE_IDS[index])
            schedule = self.named_record(
                case, "Schedule:Constant", f"{loop_name} SetpointTemperature"
            )
            self.assertEqual(
                generator._encode(schedule_value), self.field(schedule, "Hourly Value")
            )
            sizing = [
                record
                for record in self.records(case)
                if record["object_type"] == "Sizing:Plant"
                and self.field(record, "Plant or Condenser Loop Name")
                == generator._encode(loop_name)
            ]
            self.assertEqual(1, len(sizing))
            self.assertEqual(
                generator._encode(6.0),
                self.field(sizing[0], "Design Loop Exit Temperature"),
            )

    def test_boiler_generator_and_absorption_pin_python_topology_order(self) -> None:
        generator_case = self.case(generator.EXPECTED_CASE_IDS[2])
        records = self.records(generator_case)
        self.assertEqual("Branch", records[-1]["object_type"])
        generator_branch = "Loop_for_Generator Boiler Demand MainGenerator"
        self.assertEqual(
            generator._encode(generator_branch), self.field(records[-1], "Name")
        )
        branch_list = self.named_record(
            generator_case,
            "BranchList",
            "Loop_for_Generator Boiler Demand BranchList",
        )
        self.assertEqual(
            generator._encode(generator_branch),
            self.field(branch_list, "Branch 3 Name"),
        )
        self.assertEqual(
            generator._encode("Loop_for_Generator Boiler Demand Outlet"),
            self.field(branch_list, "Branch 4 Name"),
        )

        for index, tower_type, boiler_name, absorption_name in (
            (0, "FluidCooler:TwoSpeed", "Alternate Generator", "Alternate Absorber"),
            (
                1,
                "CoolingTower:SingleSpeed",
                "Representative Generator",
                "Representative Absorber",
            ),
        ):
            absorption = self.case(generator.EXPECTED_CASE_IDS[index])
            types = self.emission(absorption)["object_types"]
            self.assertEqual(92, len(types))
            self.assertEqual(["Chiller:Absorption", "Pump:VariableSpeed"], types[:2])
            self.assertLess(types.index("Boiler:HotWater"), types.index(tower_type))
            self.assertEqual(["PlantLoop", "Sizing:Plant"], types[-2:])

            boiler_loop = f"Loop_for_{boiler_name}"
            absorption_target = f"AbsorptionChiller_named_{absorption_name}"
            generator_branch = f"{boiler_loop} Demand MainGenerator"
            branch = self.named_record(absorption, "Branch", generator_branch)
            self.assertEqual(
                generator._encode("Chiller:Absorption"),
                self.field(branch, "Component 1 Object Type"),
            )
            self.assertEqual(
                generator._encode(absorption_target),
                self.field(branch, "Component 1 Name"),
            )
            self.assertEqual(
                generator._encode(f"{absorption_target} Generator InletNode"),
                self.field(branch, "Component 1 Inlet Node Name"),
            )
            self.assertEqual(
                generator._encode(f"{absorption_target} Generator OutletNode"),
                self.field(branch, "Component 1 Outlet Node Name"),
            )

            demand_branches = self.named_record(
                absorption, "BranchList", f"{boiler_loop} Demand BranchList"
            )
            self.assertEqual(
                [
                    generator._encode(f"{boiler_loop} Demand Inlet"),
                    generator._encode(f"{boiler_loop} Demand Bypass"),
                    generator._encode(generator_branch),
                    generator._encode(f"{boiler_loop} Demand Outlet"),
                ],
                [
                    self.field(demand_branches, f"Branch {position} Name")
                    for position in range(1, 5)
                ],
            )
            splitter = self.named_record(
                absorption,
                "Connector:Splitter",
                f"{boiler_loop} Demand Splitter",
            )
            mixer = self.named_record(
                absorption,
                "Connector:Mixer",
                f"{boiler_loop} Demand Mixer",
            )
            self.assertEqual(
                generator._encode(generator_branch),
                self.field(splitter, "Outlet Branch 2 Name"),
            )
            self.assertEqual(
                generator._encode(generator_branch),
                self.field(mixer, "Inlet Branch 2 Name"),
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
                rogue = imported_root / "idragon" / "source_system_idf_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.source_system_idf_review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_schema_contract_case_runtime_source_symbol_and_semantics_tamper_fail(
        self,
    ) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        schema = self.changed_fixture()
        schema["schema"] = "wrong"
        changes.append((schema, "schema"))
        contract = self.changed_fixture()
        contract["consumer_contract"]["closure"]["full_symbol_closure"] = True
        changes.append((contract, "consumer contract"))
        case = self.changed_fixture()
        case["cases"][0]["executor"] = "wrong"
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case contract"))
        runtime = self.changed_fixture()
        runtime["runtime"]["python_version"] = "3.12.8"
        changes.append((runtime, "runtime"))
        source = self.changed_fixture()
        source["upstream"]["sources"][0]["source_sha256"] = "sha256:" + "0" * 64
        changes.append((source, "upstream"))
        symbol = self.changed_fixture()
        symbol["symbols"][0]["symbol_hash"] = "sha256:" + "0" * 64
        changes.append((symbol, "symbol"))
        semantic = self.changed_fixture()
        semantic["cases"][0]["python"]["facts"]["emission"]["object_count"] = 93
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "canonical semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_and_nonfinite_values_fail(self) -> None:
        stale = self.changed_fixture()
        stale["cases"][0]["python"]["facts"]["emission"]["object_count"] = 93
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
            ("C:\\private\\source-system.json", "Absolute path"),
            ("/home/private/source-system.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-27T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.changed_fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = unsafe
            changed["cases_sha256"] = generator.cases_sha256(changed["cases"])
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        encoded_nonfinite = self.changed_fixture()
        first_value = encoded_nonfinite["cases"][0]["python"]["facts"]["emission"][
            "first_object_records"
        ][0]["ordered_fields"][1]["value"]
        first_value.clear()
        first_value.update({"hex": "nan", "kind": "float", "repr": "nan"})
        encoded_nonfinite["cases_sha256"] = generator.cases_sha256(
            encoded_nonfinite["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "nonfinite encoded float"):
            generator.validate_oracle(encoded_nonfinite)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            self.assertFalse(math.isfinite(nonfinite))
            changed = self.changed_fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaisesRegex(ValueError, "Out of range float"):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

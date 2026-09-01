"""Fail-closed tests for the bounded Zone IDF emitter oracle."""

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
    / "generate_dragon_shape_zone_to_idf_object_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-shape-zone-to-idf-object-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_shape_zone_to_idf_object_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load Zone IDF generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 67_640
EXPECTED_GENERATOR_SHA256 = (
    "sha256:41d0de6eee371576d19ed5744b7316ee1dfcf89410c050c1205a2f0c4f9a13fb"
)
EXPECTED_FIXTURE_BYTES = 219_575
EXPECTED_FIXTURE_SHA256 = (
    "sha256:7c0c3f10d8e3a83b52a6ddfde0512e4913e2fc950a0224ba9256f0e94ac19a67"
)
EXPECTED_CASES_SHA256 = (
    "sha256:21f896de5f0685d45bd7c0f29a777488dce65e05a79d90736ed193f1a8db493a"
)


class DragonShapeZoneToIdfObjectOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-shape-zone-to-idf-object-tests-"
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
    def output(case: dict[str, object]) -> list[dict[str, object]]:
        return case["python"]["facts"]["emission"]["first_output"]

    @staticmethod
    def idf_fields(
        case: dict[str, object], index: int
    ) -> list[dict[str, object]]:
        item = DragonShapeZoneToIdfObjectOracleTests.output(case)[index]
        if item["kind"] != "idf-object":
            raise AssertionError(f"Output {index} is not an IDF object")
        return item["ordered_fields"]

    def test_generator_and_fixture_are_exact_strict_utf8_and_self_validating(
        self,
    ) -> None:
        value = self.fixture()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, GENERATOR_PATH.stat().st_size)
        self.assertEqual(
            EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH)
        )
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_inventory_binds_twelve_sources_three_exact_symbols_and_signatures(
        self,
    ) -> None:
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
                "Zone.to_idf_hvac_default_object",
                "Zone.to_idf_load_object",
                "Zone.to_idf_object",
            ),
            generator.TARGET_SYMBOLS,
        )
        public_symbols = generator.load_json_without_duplicates(INVENTORY_PATH)[
            "symbols"
        ]
        self.assertEqual(
            [1092, 1093, 1094],
            [
                next(
                    index
                    for index, symbol in enumerate(public_symbols)
                    if symbol["symbol"] == target
                    and symbol["path"] == generator.SHAPE_SOURCE_PATH
                )
                for target in generator.TARGET_SYMBOLS
            ],
        )
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {
                symbol: "(self) -> 'list[IdfObject]'"
                for symbol in generator.TARGET_SYMBOLS
            },
            contract["runtime_signatures"],
        )
        loaded = self.fixture()["upstream"]["loaded_local_modules"]
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)

    def test_case_set_is_sorted_balanced_and_uses_three_separate_adaptations(
        self,
    ) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(9, len(identifiers))
        self.assertEqual(9, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        for item in definitions:
            self.assertEqual(
                generator.ADAPTATIONS[item["symbol"]],
                item["expected_dotnet"]["adaptation"],
            )
            self.assertEqual("returned", item["expected_dotnet"]["outcome"])
        self.assertEqual(3, len(set(generator.ADAPTATIONS.values())))

    def test_consumer_contract_keeps_children_classes_and_parent_outside_closure(
        self,
    ) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(
            {symbol: "exception" for symbol in generator.TARGET_SYMBOLS},
            contract["classifications"],
        )
        closure = contract["closure"]
        self.assertFalse(closure["full_symbol_closure"])
        for context_only in (
            "Zone",
            "Zone.__init__",
            "Zone.floor_area",
            "Surface",
            "Surface.to_idf_object",
            "Window",
            "Door",
            "Shading",
            "Profile",
            "Schedule",
            "Schedule.normalize_by_max",
            "Schedule.to_idf_object",
            "IdfObject",
        ):
            self.assertIn(context_only, closure["context_only_not_targeted"])
            self.assertNotIn(context_only, contract["classifications"])
        for unresolved in (
            "Zone-class-constructor-and-properties",
            "Surface-class-and-Surface.to_idf_object",
            "Window-door-and-shading-emission",
            "Profile-and-Schedule-child-converter-closure",
            "invalid-duck-types-and-exact-error-behavior",
            "IdfObject-class-constructor-validation-and-mutation",
            "native-global-order-deduplication-and-conflict-policy",
            "EnergyModel-parent-assembly",
        ):
            self.assertIn(unresolved, closure["unresolved_behavior"])

    def test_hvac_cases_pin_conditioning_gate_full_family_order_and_raw_fields(
        self,
    ) -> None:
        value = self.fixture()
        conditioned = self.case(value, generator.EXPECTED_CASE_IDS[0])
        emission = conditioned["python"]["facts"]["emission"]
        self.assertEqual(
            [
                "DesignSpecification:OutdoorAir",
                "DesignSpecification:ZoneAirDistribution",
                "Sizing:Zone",
                "ZoneHVAC:EquipmentList",
                "ZoneHVAC:EquipmentConnections",
                "Schedule:Constant",
                "ThermostatSetpoint:DualSetpoint",
                "ZoneControl:Thermostat",
            ],
            emission["object_family_order"],
        )
        self.assertEqual([8, 6, 37, 110, 8, 3, 3, 12], [
            len(item["ordered_fields"]) for item in self.output(conditioned)
        ])
        sizing = self.idf_fields(conditioned, 2)
        self.assertEqual("Zone or ZoneList Name", sizing[0]["name"])
        self.assertEqual("Conditioned Zone", sizing[0]["value"]["value"])
        self.assertEqual("Type of Space Sum to Use", sizing[-1]["name"])
        equipment = self.idf_fields(conditioned, 3)
        self.assertEqual("Zone Equipment 18 Sequential Heating Fraction Schedule Name", equipment[-1]["name"])
        self.assertEqual({"kind": "none"}, equipment[-1]["value"])
        for index in (1, 2):
            case = self.case(value, generator.EXPECTED_CASE_IDS[index])
            self.assertEqual([], self.output(case))
            self.assertEqual(0, case["python"]["facts"]["emission"]["object_count"])
        self.assertEqual(
            "none",
            self.case(value, generator.EXPECTED_CASE_IDS[1])["python"]["facts"]
            ["input_context"]["profile"]["schedules"]["hvac_availability"]["kind"],
        )
        self.assertEqual(
            "none",
            self.case(value, generator.EXPECTED_CASE_IDS[2])["python"]["facts"]
            ["input_context"]["supply"]["kind"],
        )

    def test_load_cases_pin_normalization_order_defaults_and_ventilation_math(
        self,
    ) -> None:
        value = self.fixture()
        empty = self.case(value, generator.EXPECTED_CASE_IDS[3])
        self.assertEqual([], self.output(empty))

        erv = self.case(value, generator.EXPECTED_CASE_IDS[4])
        self.assertEqual(
            ["Schedule:Compact", "People", "ZoneVentilation:DesignFlowRate"],
            erv["python"]["facts"]["emission"]["object_family_order"],
        )
        self.assertEqual(153, len(self.idf_fields(erv, 0)))
        self.assertEqual(29, len(self.idf_fields(erv, 1)))
        self.assertEqual(26, len(self.idf_fields(erv, 2)))
        erv_ventilation = self.idf_fields(erv, 2)
        self.assertEqual(
            "0.0024900000000000005",
            erv_ventilation[6]["value"]["repr"],
        )
        self.assertEqual("Exhaust", erv_ventilation[8]["value"]["value"])
        self.assertEqual(
            "166.66666666666663",
            erv_ventilation[9]["value"]["repr"],
        )

        full = self.case(value, generator.EXPECTED_CASE_IDS[5])
        self.assertEqual(
            [
                "Lights",
                "Schedule:Compact",
                "ElectricEquipment",
                "Schedule:Compact",
                "People",
                "ZoneInfiltration:DesignFlowRate",
                "ZoneVentilation:DesignFlowRate",
            ],
            full["python"]["facts"]["emission"]["object_family_order"],
        )
        self.assertEqual([17, 153, 11, 153, 29, 12, 26], [
            len(item["ordered_fields"]) for item in self.output(full)
        ])
        for schedule_index in (1, 3):
            fields = self.idf_fields(full, schedule_index)
            self.assertEqual("Field 150", fields[-2]["name"])
            self.assertEqual({"kind": "none"}, fields[-2]["value"])
            self.assertEqual("", fields[-1]["name"])
        ventilation = self.idf_fields(full, 6)
        self.assertEqual("0.0083", ventilation[6]["value"]["repr"])
        self.assertEqual("Natural", ventilation[8]["value"]["value"])

    def test_parent_cases_pin_trace_dependency_call_order_output_order_and_floor_area(
        self,
    ) -> None:
        value = self.fixture()
        empty = self.case(value, generator.EXPECTED_CASE_IDS[6])
        self.assertEqual(["load", "hvac-default"], empty["python"]["facts"]["child_call_trace_first"])
        self.assertEqual(["Zone"], empty["python"]["facts"]["emission"]["object_family_order"])
        self.assertEqual("0.0", self.idf_fields(empty, 0)[9]["value"]["repr"])

        multiple = self.case(value, generator.EXPECTED_CASE_IDS[7])
        self.assertEqual(
            [
                "surface:Floor-A:zone:Multiple Surface Zone",
                "surface:Wall-B:zone:Multiple Surface Zone",
                "load",
                "hvac-default",
            ],
            multiple["python"]["facts"]["child_call_trace_first"],
        )
        self.assertEqual(
            [
                "Zone",
                "trace:surface:Floor-A:object-1",
                "trace:surface:Wall-B:object-1",
                "trace:hvac:object-1",
                "trace:load:object-1",
            ],
            multiple["python"]["facts"]["emission"]["object_family_order"],
        )
        self.assertEqual("25.0", self.idf_fields(multiple, 0)[9]["value"]["repr"])

        ordered = self.case(value, generator.EXPECTED_CASE_IDS[8])
        facts = ordered["python"]["facts"]
        self.assertEqual(facts["child_call_trace_first"], facts["child_call_trace_second"])
        self.assertEqual(
            [
                "surface:Floor-First:zone:Ordered Parent Zone",
                "surface:Floor-Empty:zone:Ordered Parent Zone",
                "surface:Ceiling-Last:zone:Ordered Parent Zone",
                "load",
                "hvac-default",
            ],
            facts["child_call_trace_first"],
        )
        self.assertEqual("20.0", self.idf_fields(ordered, 0)[9]["value"]["repr"])
        self.assertEqual(
            {
                "hvac_default_converter": "instrumented-instance-method-double",
                "load_converter": "instrumented-instance-method-double",
                "surface_converter": "instrumented-surface-trace-double",
            },
            facts["dependency_isolation"],
        )
        for case in (empty, multiple, ordered):
            self.assertTrue(all(case["python"]["facts"]["input_integrity"].values()))
            emission = case["python"]["facts"]["emission"]
            self.assertTrue(emission["fresh_result_list"])
            self.assertTrue(emission["all_output_items_fresh"])
            self.assertTrue(emission["second_output_equal"])

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
                rogue = imported_root / "idragon" / "zone_idf_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.zone_idf_review_probe"] = SimpleNamespace(
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
        semantic["cases"][0]["python"]["facts"]["emission"]["object_count"] = 9
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_values_and_nonfinite_fail(self) -> None:
        stale = self.fixture()
        stale["cases"][0]["python"]["facts"]["emission"]["object_count"] = 9
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
            ("C:\\private\\zone.json", "Absolute path"),
            ("/home/private/zone.json", "Absolute path"),
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

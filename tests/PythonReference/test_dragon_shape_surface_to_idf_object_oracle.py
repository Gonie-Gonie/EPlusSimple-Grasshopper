"""Fail-closed tests for the pinned ``Surface.to_idf_object`` oracle."""

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
    / "generate_dragon_shape_surface_to_idf_object_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-shape-surface-to-idf-object-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_shape_surface_to_idf_object_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load Surface IDF generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 49_143
EXPECTED_GENERATOR_SHA256 = (
    "sha256:f86cd2ce661ae83cfac741c4b0bafaa15db1c9cbe1f7bff4766d4c433ea5bca5"
)
EXPECTED_FIXTURE_BYTES = 535_245
EXPECTED_FIXTURE_SHA256 = (
    "sha256:6c32f737eb12ca869c6e7b5742eed434042c6731fd6aed73178cb1c8765d478d"
)
EXPECTED_CASES_SHA256 = (
    "sha256:d84505731f0e5ebe95144d93faa4bf80752287c5467895ee15f4d083aba5ce11"
)
EXPECTED_TOTAL_OBJECTS = 23
EXPECTED_TOTAL_FIELDS = 2_437


class DragonShapeSurfaceToIdfObjectOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-shape-surface-idf-tests-"
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
    def records(case: dict[str, object]) -> list[dict[str, object]]:
        return case["python"]["facts"]["emission"]["first_object_records"]

    @classmethod
    def record(
        cls, case: dict[str, object], object_index: int = 0
    ) -> dict[str, object]:
        return cls.records(case)[object_index]

    @staticmethod
    def field(
        record: dict[str, object], name: str
    ) -> dict[str, object]:
        matches = [
            item["value"] for item in record["ordered_fields"] if item["name"] == name
        ]
        if len(matches) != 1:
            raise AssertionError(f"Expected one {name!r} field, found {len(matches)}")
        return matches[0]

    @classmethod
    def text(cls, record: dict[str, object], name: str) -> str | None:
        encoded = cls.field(record, name)
        if encoded == {"kind": "none"}:
            return None
        if encoded.get("kind") != "str":
            raise AssertionError(f"Expected encoded string for {name!r}: {encoded!r}")
        return encoded["value"]

    def test_generator_fixture_and_cases_are_exactly_pinned(self) -> None:
        value = self.fixture()
        generator_raw = GENERATOR_PATH.read_bytes()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_raw))
        self.assertEqual(
            EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH)
        )
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(
            EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH)
        )
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_inventory_binds_twelve_sources_and_exact_idx1045_receipt(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(1, len(inventory["symbols"]))

        raw_inventory = generator.load_json_without_duplicates(INVENTORY_PATH)
        receipt = raw_inventory["symbols"][1045]
        self.assertEqual(
            {
                **generator.EXPECTED_SYMBOL_RECEIPT,
                "path": generator.SHAPE_SOURCE_PATH,
                "symbol": generator.TARGET_SYMBOL,
            },
            receipt,
        )
        self.assertEqual(
            "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c",
            next(
                source["source_sha256"]
                for source in generator.SOURCE_SPECS
                if source["path"] == generator.SHAPE_SOURCE_PATH
            ),
        )
        self.assertEqual(
            "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2",
            next(
                source["ast_sha256"]
                for source in generator.SOURCE_SPECS
                if source["path"] == generator.SHAPE_SOURCE_PATH
            ),
        )
        fixture = self.fixture()
        self.assertEqual(
            "(self, zone: 'Zone') -> 'IdfObject'",
            fixture["consumer_contract"]["runtime_signatures"][
                generator.TARGET_SYMBOL
            ],
        )
        self.assertEqual(
            generator._expected_loaded_local_modules(),
            fixture["upstream"]["loaded_local_modules"],
        )

    def test_five_case_matrix_is_sorted_bounded_and_exception_classified(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(5, len(identifiers))
        self.assertEqual(5, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        self.assertTrue(
            all(
                item["expected_dotnet"]
                == {"adaptation": generator.ADAPTATION, "outcome": "returned"}
                for item in definitions
            )
        )

        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {generator.TARGET_SYMBOL: "exception"}, contract["classifications"]
        )
        self.assertEqual(
            {generator.TARGET_SYMBOL: generator.ASSERTION_ID},
            contract["assertion_ids"],
        )
        self.assertEqual(
            {generator.TARGET_SYMBOL: generator.NATIVE_TARGET},
            contract["native_targets"],
        )
        self.assertFalse(contract["closure"]["full_symbol_closure"])
        context_only = contract["closure"]["context_only_not_targeted"]
        for dependency in (
            "Surface.__init__",
            "Vertex",
            "SurfaceBoundaryCondition",
            "SurfaceType",
            "Window.to_idf_object",
            "Door.to_idf_object",
            "Blind.to_idf_object",
            "Shade.to_idf_object",
            "AirBoundary",
            "Construction",
            "Glazing",
            "NoMassConstruction",
            "IdfObject.__init__",
            "Zone.name",
        ):
            self.assertIn(dependency, context_only)
            self.assertNotIn(dependency, contract["classifications"])
        unresolved = contract["closure"]["unresolved_behavior"]
        self.assertIn("native-default-detailed-fenestration-route", unresolved)
        self.assertIn(
            "Window-Door-Blind-Shade-standalone-converter-closure", unresolved
        )

    def test_all_objects_fields_freshness_and_case_fact_hashes_are_exact(self) -> None:
        value = self.fixture()
        object_total = 0
        field_total = 0
        for identifier in generator.EXPECTED_CASE_IDS:
            with self.subTest(identifier=identifier):
                case = self.case(value, identifier)
                facts = case["python"]["facts"]
                emission = facts["emission"]
                records = emission["first_object_records"]
                self.assertEqual(
                    generator.EXPECTED_FACT_SHA256[identifier],
                    generator.canonical_sha256(facts),
                )
                self.assertEqual(
                    list(generator.EXPECTED_OBJECT_TYPES[identifier]),
                    emission["object_types"],
                )
                self.assertEqual(
                    list(generator.EXPECTED_FIELD_COUNTS[identifier]),
                    [record["field_count"] for record in records],
                )
                self.assertEqual(len(records), emission["object_count"])
                self.assertEqual(
                    [record["object_type"] for record in records],
                    emission["object_types"],
                )
                self.assertTrue(emission["all_allowed_fields_covered_in_order"])
                self.assertTrue(emission["fresh_call_result_lists"])
                self.assertTrue(emission["first_objects_pairwise_distinct"])
                self.assertTrue(emission["second_objects_pairwise_distinct"])
                self.assertTrue(all(emission["fresh_idf_object_flags"]))
                self.assertTrue(all(emission["same_idd_definition_flags"]))
                self.assertTrue(all(emission["second_fields_equal_flags"]))
                self.assertTrue(all(facts["input_integrity"].values()))
                self.assertTrue(
                    facts["behavior_facts"]["host_surface_last_in_each_call"]
                )
                for record in records:
                    self.assertEqual(
                        record["field_count"], len(record["ordered_fields"])
                    )
                object_total += len(records)
                field_total += sum(record["field_count"] for record in records)
        self.assertEqual(EXPECTED_TOTAL_OBJECTS, object_total)
        self.assertEqual(EXPECTED_TOTAL_FIELDS, field_total)

    def test_surface_branches_zone_links_and_construction_names_are_exact(self) -> None:
        value = self.fixture()
        expected = (
            (
                0,
                0,
                "Adiabatic Custom-Air Ceiling",
                "Ceiling",
                "DefaultAirBoundary",
                "Adiabatic Parent Zone",
                "Adiabatic",
                None,
                "NoSun",
                "NoWind",
            ),
            (
                1,
                0,
                "Ground Pentagon Floor",
                "Floor",
                "Ground Pentagon Assembly:for:Ground Pentagon Floor",
                "Ground Parent Zone",
                "Ground",
                None,
                "NoSun",
                "NoWind",
            ),
            (
                2,
                4,
                "Interzone Wall A",
                "Wall",
                "Interzone Wall Assembly A:for:Interzone Wall A",
                "Interzone Parent Zone A",
                "Surface",
                "Interzone Wall B",
                "NoSun",
                "NoWind",
            ),
            (
                2,
                9,
                "Interzone Wall B",
                "Wall",
                "Interzone Wall Assembly B:for:Interzone Wall B",
                "Interzone Parent Zone B",
                "Surface",
                "Interzone Wall A",
                "NoSun",
                "NoWind",
            ),
            (
                3,
                0,
                "Outdoor Ceiling Becomes Roof",
                "Roof",
                "Outdoor Roof Assembly:for:Outdoor Ceiling Becomes Roof",
                "Outdoor Roof Parent Zone",
                "Outdoors",
                None,
                "SunExposed",
                "WindExposed",
            ),
            (
                4,
                9,
                "Outdoor Multi-Opening Wall",
                "Wall",
                "Outdoor Wall Assembly:for:Outdoor Multi-Opening Wall",
                "Outdoor Openings Parent Zone",
                "Outdoors",
                None,
                "SunExposed",
                "WindExposed",
            ),
        )
        for (
            case_index,
            record_index,
            name,
            surface_type,
            construction,
            zone,
            boundary,
            counterpart,
            sun,
            wind,
        ) in expected:
            with self.subTest(name=name):
                case = self.case(value, generator.EXPECTED_CASE_IDS[case_index])
                record = self.record(case, record_index)
                self.assertEqual("BuildingSurface:Detailed", record["object_type"])
                self.assertEqual(name, self.text(record, "Name"))
                self.assertEqual(surface_type, self.text(record, "Surface Type"))
                self.assertEqual(construction, self.text(record, "Construction Name"))
                self.assertEqual(zone, self.text(record, "Zone Name"))
                self.assertEqual(
                    {"kind": "none"}, self.field(record, "Space Name")
                )
                self.assertEqual(
                    boundary, self.text(record, "Outside Boundary Condition")
                )
                self.assertEqual(
                    counterpart,
                    self.text(record, "Outside Boundary Condition Object"),
                )
                self.assertEqual(sun, self.text(record, "Sun Exposure"))
                self.assertEqual(wind, self.text(record, "Wind Exposure"))
                self.assertEqual(
                    "autocalculate", self.text(record, "View Factor to Ground")
                )
                self.assertEqual(
                    "autocalculate", self.text(record, "Number of Vertices")
                )

    def test_complete_host_idd_order_and_pentagon_omissions_are_not_compacted(self) -> None:
        value = self.fixture()
        base_names = [
            "Name",
            "Surface Type",
            "Construction Name",
            "Zone Name",
            "Space Name",
            "Outside Boundary Condition",
            "Outside Boundary Condition Object",
            "Sun Exposure",
            "Wind Exposure",
            "View Factor to Ground",
            "Number of Vertices",
        ]
        vertex_names = [
            f"Vertex {index} {axis}-coordinate"
            for index in range(1, 121)
            for axis in ("X", "Y", "Z")
        ]
        expected_names = base_names + vertex_names
        self.assertEqual(371, len(expected_names))
        for case_index, record_index in ((0, 0), (1, 0), (2, 4), (2, 9), (3, 0), (4, 9)):
            record = self.record(
                self.case(value, generator.EXPECTED_CASE_IDS[case_index]), record_index
            )
            self.assertEqual(
                expected_names, [field["name"] for field in record["ordered_fields"]]
            )

        pentagon = self.record(self.case(value, generator.EXPECTED_CASE_IDS[1]))
        expected_vertices = (
            (0.0, 0.0, 0.0),
            (4.0, 0.0, 0.0),
            (5.0, 2.0, 0.0),
            (2.0, 4.0, 0.0),
            (0.0, 2.0, 0.0),
        )
        for index, vertex in enumerate(expected_vertices, start=1):
            for axis, expected_value in zip(("X", "Y", "Z"), vertex, strict=True):
                encoded = self.field(pentagon, f"Vertex {index} {axis}-coordinate")
                self.assertEqual(expected_value.hex(), encoded["hex"])
                self.assertEqual(repr(expected_value), encoded["repr"])
        for index in range(6, 121):
            for axis in ("X", "Y", "Z"):
                self.assertEqual(
                    {"kind": "none"},
                    self.field(pentagon, f"Vertex {index} {axis}-coordinate"),
                )

    def test_custom_air_boundary_dangling_reference_is_preserved_as_a_fact(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[0])
        facts = case["python"]["facts"]
        self.assertEqual(
            {
                "authored_construction_name": "Custom Transfer Air Boundary",
                "custom_construction_object_emitted": False,
                "dangling_default_reference": True,
                "emitted_construction_name": "DefaultAirBoundary",
            },
            facts["behavior_facts"]["air_boundary_reference"],
        )
        self.assertEqual(
            "Custom Transfer Air Boundary",
            facts["input_context"]["calls"][0]["surface"]["construction"]["name"],
        )
        self.assertEqual(
            "DefaultAirBoundary",
            self.text(self.record(case), "Construction Name"),
        )
        self.assertNotIn(
            "Construction:AirBoundary",
            facts["emission"]["object_types"],
        )

    def test_interzone_windows_doors_reciprocal_links_and_order_are_exact(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[2])
        facts = case["python"]["facts"]
        records = self.records(case)
        self.assertEqual([5, 5], facts["behavior_facts"]["call_spans"])
        self.assertEqual([4, 9], facts["behavior_facts"]["host_surface_indices"])
        expected_names = [
            "Interzone A Window 1",
            "Interzone A Window 2",
            "Interzone A Door 1",
            "Interzone A Door 2",
            "Interzone Wall A",
            "Interzone B Window 1",
            "Interzone B Window 2",
            "Interzone B Door 1",
            "Interzone B Door 2",
            "Interzone Wall B",
        ]
        self.assertEqual(expected_names, [self.text(record, "Name") for record in records])
        expected_links = [
            ("Window:Interzone", "Interzone A Window 1", "Interzone B Window 1"),
            ("Window:Interzone", "Interzone A Window 2", "Interzone B Window 2"),
            ("Door:Interzone", "Interzone A Door 1", "Interzone B Door 1"),
            ("Door:Interzone", "Interzone A Door 2", "Interzone B Door 2"),
            ("Window:Interzone", "Interzone B Window 1", "Interzone A Window 1"),
            ("Window:Interzone", "Interzone B Window 2", "Interzone A Window 2"),
            ("Door:Interzone", "Interzone B Door 1", "Interzone A Door 1"),
            ("Door:Interzone", "Interzone B Door 2", "Interzone A Door 2"),
        ]
        self.assertEqual(
            expected_links,
            [
                (item["object_type"], item["name"], item["counterpart_name"])
                for item in facts["behavior_facts"]["opening_counterpart_links"]
            ],
        )
        opening_field_names = [
            "Name",
            "Construction Name",
            "Building Surface Name",
            "Outside Boundary Condition Object",
            "Multiplier",
            "Starting X Coordinate",
            "Starting Z Coordinate",
            "Length",
            "Height",
        ]
        for index in (0, 1, 2, 3, 5, 6, 7, 8):
            self.assertEqual(
                opening_field_names,
                [field["name"] for field in records[index]["ordered_fields"]],
            )

    def test_outdoor_multiple_openings_and_shading_children_keep_authored_order(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[4])
        facts = case["python"]["facts"]
        records = self.records(case)
        self.assertEqual([10], facts["behavior_facts"]["call_spans"])
        self.assertEqual([9], facts["behavior_facts"]["host_surface_indices"])
        self.assertEqual(
            [
                "Outdoor Blind Window",
                "Outdoor Shade Window",
                "Outdoor Clear Window",
                "Outdoor Door 1",
                "Outdoor Door 2",
                "Strong Interior Blind",
                "Outdoor Blind Window:ShadingControl",
                "Simple Interior Shade",
                "Outdoor Shade Window:ShadingControl",
                "Outdoor Multi-Opening Wall",
            ],
            [self.text(record, "Name") for record in records],
        )
        self.assertEqual("InteriorBlind", self.text(records[6], "Shading Type"))
        self.assertEqual(
            "Strong Interior Blind",
            self.text(records[6], "Shading Device Material Name"),
        )
        self.assertEqual(
            "Outdoor Blind Window",
            self.text(records[6], "Fenestration Surface 1 Name"),
        )
        self.assertEqual("InteriorShade", self.text(records[8], "Shading Type"))
        self.assertEqual(
            "Simple Interior Shade",
            self.text(records[8], "Shading Device Material Name"),
        )
        self.assertEqual(
            "Outdoor Shade Window",
            self.text(records[8], "Fenestration Surface 1 Name"),
        )
        self.assertEqual(
            "Outdoor Openings Parent Zone", self.text(records[6], "Zone Name")
        )
        self.assertEqual(
            "Outdoor Openings Parent Zone", self.text(records[8], "Zone Name")
        )
        self.assertEqual([], facts["behavior_facts"]["opening_counterpart_links"])

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
                rogue = imported_root / "idragon" / "surface_idf_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.surface_idf_review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_schema_contract_case_runtime_source_symbol_and_semantic_tamper_fail(
        self,
    ) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        schema = self.fixture()
        schema["schema"] = "wrong"
        changes.append((schema, "schema"))
        contract = self.fixture()
        contract["consumer_contract"]["closure"]["full_symbol_closure"] = True
        changes.append((contract, "consumer contract"))
        case_contract = self.fixture()
        case_contract["cases"][0]["executor"] = "wrong"
        case_contract["cases_sha256"] = generator.cases_sha256(
            case_contract["cases"]
        )
        changes.append((case_contract, "case contract"))
        runtime = self.fixture()
        runtime["runtime"]["python_version"] = "3.12.8"
        changes.append((runtime, "runtime"))
        source = self.fixture()
        source["upstream"]["sources"][0]["source_sha256"] = "sha256:" + "0" * 64
        changes.append((source, "upstream"))
        symbol = self.fixture()
        symbol["symbols"][0]["symbol_hash"] = "sha256:" + "0" * 64
        changes.append((symbol, "symbol"))
        air = self.fixture()
        air["cases"][0]["python"]["facts"]["behavior_facts"][
            "air_boundary_reference"
        ]["dangling_default_reference"] = False
        air["cases_sha256"] = generator.cases_sha256(air["cases"])
        changes.append((air, "canonical semantics"))
        order = self.fixture()
        records = order["cases"][2]["python"]["facts"]["emission"][
            "first_object_records"
        ]
        records[0], records[1] = records[1], records[0]
        order["cases_sha256"] = generator.cases_sha256(order["cases"])
        changes.append((order, "canonical semantics"))
        omission = self.fixture()
        omission_record = omission["cases"][1]["python"]["facts"]["emission"][
            "first_object_records"
        ][0]
        omission_record["ordered_fields"].pop()
        omission["cases_sha256"] = generator.cases_sha256(omission["cases"])
        changes.append((omission, "canonical semantics"))
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
            ("C:\\private\\surface.json", "Absolute path"),
            ("/home/private/surface.json", "Absolute path"),
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
                with self.assertRaises(ValueError):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

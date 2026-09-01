"""Fail-closed tests for the pinned shape opening/adjacency core oracle."""

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
    / "generate_dragon_shape_opening_adjacency_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-shape-opening-adjacency-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_shape_opening_adjacency_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load opening/adjacency generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 91_181
EXPECTED_GENERATOR_SHA256 = (
    "sha256:004eb87cbe18ddf3ac8c6c919c708d78e52182c585ac55b8994afbc7ff1ecec2"
)
EXPECTED_FIXTURE_BYTES = 260_256
EXPECTED_FIXTURE_SHA256 = (
    "sha256:1eb9d258baa9471665d1470498d6855db7e7fde6bc89ac7a259d8908b6a3fe64"
)
EXPECTED_CASES_SHA256 = (
    "sha256:ee98651aeaf270f3d9fb07a862950ffba343dc757de6523541a60daf0b3c392a"
)


class DragonShapeOpeningAdjacencyCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-shape-opening-adjacency-tests-"
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
    def case(value: dict[str, object], scenario: str) -> dict[str, object]:
        return next(
            item
            for item in value["cases"]
            if item["python"]["facts"]["scenario"] == scenario
        )

    @classmethod
    def facts(cls, value: dict[str, object], scenario: str) -> dict[str, object]:
        return cls.case(value, scenario)["python"]["facts"]

    @staticmethod
    def decode(value: dict[str, object]) -> float | int | str | bool | None:
        kind = value["kind"]
        if kind == "none":
            return None
        if kind == "bool":
            return value["value"]
        if kind == "int":
            return int(value["value"])
        if kind == "str":
            return value["value"]
        if kind == "float":
            return float.fromhex(value["hex"])
        if kind == "float-nonfinite":
            return {
                "nan": float("nan"),
                "negative-infinity": float("-inf"),
                "positive-infinity": float("inf"),
            }[value["value"]]
        raise AssertionError(f"Unexpected encoded scalar: {value!r}")

    @staticmethod
    def rehash(value: dict[str, object]) -> None:
        value["fact_sha256"] = {
            case["id"]: generator.canonical_sha256(case["python"]["facts"])
            for case in value["cases"]
        }
        for case in value["cases"]:
            case["python"]["facts_sha256"] = value["fact_sha256"][case["id"]]
        value["case_sha256"] = generator.case_sha256(value["cases"])
        value["cases_sha256"] = generator.cases_sha256(value["cases"])

    def test_generator_fixture_and_all_hash_layers_are_exactly_pinned(self) -> None:
        value = self.fixture()
        generator_raw = GENERATOR_PATH.read_bytes()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_raw))
        self.assertEqual(
            EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH)
        )
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(18, len(value["fact_sha256"]))
        self.assertEqual(18, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_inventory_exposes_exact_nineteen_indexed_shape_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(
            generator._expected_target_receipts(), inventory["target_receipts"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(19, len(inventory["symbols"]))
        self.assertEqual(
            [
                1025,
                1026,
                1028,
                1029,
                1030,
                1031,
                1033,
                1035,
                1039,
                1040,
                1042,
                1048,
                1049,
                1050,
                1051,
                1052,
                1053,
                1081,
                1082,
            ],
            [item["inventory_index"] for item in inventory["target_receipts"]],
        )
        shape = next(
            item
            for item in self.fixture()["upstream"]["sources"]
            if item["path"] == generator.SHAPE_SOURCE_PATH
        )
        self.assertEqual(generator.SHAPE_SOURCE_SHA256, shape["source_sha256"])
        self.assertEqual(generator.SHAPE_AST_SHA256, shape["ast_sha256"])
        self.assertEqual(27_438, self.fixture()["upstream"]["shape_source"]["bytes"])

    def test_target_coverage_is_closed_without_promoting_parent_emission(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        closure = contract["closure"]
        self.assertEqual(list(generator.TARGET_SYMBOLS), contract["target_symbols"])
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(contract["classifications"]))
        self.assertTrue(all(item == "exception" for item in contract["classifications"].values()))
        self.assertFalse(closure["full_symbol_closure"])
        self.assertTrue(closure["target_coverage_complete"])
        self.assertEqual(
            [
                "Surface.get_subsurface-nan-positive-infinity-and-negative-infinity-inputs",
                "Surface.get_subsurface-nonnumeric-inputs-and-arithmetic-error-timing",
            ],
            closure["unresolved_target_behavior"],
        )
        self.assertTrue(closure["parent_emission_is_context_only"])
        self.assertEqual([generator.EXPECTED_CASE_IDS[15]], closure["parent_emission_context_case_ids"])
        counts = Counter(
            symbol
            for case in value["cases"]
            for symbol in case["target_symbols"]
        )
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(counts))
        self.assertNotIn("Surface.to_idf_object", counts)
        self.assertEqual(
            [generator.EXPECTED_CASE_IDS[15]],
            [
                case["id"]
                for case in value["cases"]
                if "Surface.to_idf_object" in case["context_symbols"]
            ],
        )

    def test_runtime_signatures_adaptations_assertions_and_native_routes_are_total(self) -> None:
        contract = self.fixture()["consumer_contract"]
        expected = set(generator.TARGET_SYMBOLS)
        self.assertEqual(expected, set(contract["runtime_signatures"]))
        self.assertEqual(expected, set(contract["adaptations"]))
        self.assertEqual(expected, set(contract["assertion_ids"]))
        self.assertEqual(expected, set(contract["native_targets"]))
        self.assertEqual(19, len(set(contract["assertion_ids"].values())))
        self.assertEqual(
            "legacy-linear-scale-subsurface-projection",
            contract["adaptations"]["Surface.get_subsurface"],
        )
        self.assertEqual(
            "mutable-reciprocal-python-surface-adjacency",
            contract["adaptations"]["Surface.boundary"],
        )

    def test_a01_through_a05_pin_shading_constructor_and_abc_behavior(self) -> None:
        value = self.fixture()
        a01 = self.facts(value, "A01")
        self.assertTrue(a01["observations"]["fresh_instances"])
        self.assertTrue(a01["observations"]["states_equal"])
        self.assertTrue(a01["source_state"]["unchanged"])
        a02 = self.facts(value, "A02")
        self.assertTrue(a02["observations"]["construction_accepted_invalid_bundle"])
        self.assertTrue(a02["observations"]["mutation_accepted_invalid_bundle"])
        self.assertEqual("float-nonfinite", a02["source_state"]["after"]["slat_angle"]["kind"])
        a03 = self.facts(value, "A03")
        self.assertAlmostEqual(0.5, self.decode(a03["observations"]["implied_emissivity"]))
        a04 = self.facts(value, "A04")
        self.assertEqual(1.5, self.decode(a04["observations"]["optical_sum"]))
        self.assertEqual(-0.5, self.decode(a04["observations"]["implied_emissivity_before_mutation"]))
        a05 = self.facts(value, "A05")
        self.assertTrue(a05["observations"]["direct_instantiation_succeeded"])
        self.assertEqual([], a05["observations"]["abstract_method_names"])
        self.assertEqual(["Shading", "ABC", "object"], a05["observations"]["mro_names"])

    def test_a06_through_a09_pin_opening_reference_invalid_and_mutable_state(self) -> None:
        value = self.fixture()
        a06 = self.facts(value, "A06")
        self.assertEqual(
            ["none", "Blind", "Shade"],
            a06["observations"]["shading_kinds_in_order"],
        )
        self.assertTrue(all(a06["observations"]["shading_identity_flags"]))
        a07 = self.facts(value, "A07")
        self.assertEqual(
            ["float", "float-nonfinite", "float-nonfinite"],
            a07["observations"]["area_kinds"],
        )
        self.assertTrue(a07["observations"]["foreign_glazing_reference_preserved"])
        self.assertTrue(a07["observations"]["foreign_blind_reference_preserved"])
        self.assertFalse(a07["source_state"]["unchanged"])
        a08 = self.facts(value, "A08")
        self.assertTrue(a08["observations"]["construction_reference_preserved"])
        self.assertTrue(a08["observations"]["fresh_instances"])
        a09 = self.facts(value, "A09")
        self.assertTrue(a09["observations"]["foreign_construction_reference_preserved"])
        self.assertTrue(a09["observations"]["mutation_accepted"])
        self.assertFalse(a09["source_state"]["unchanged"])

    def test_a10_pins_shared_default_list_alias_mutation_and_restoration(self) -> None:
        facts = self.facts(self.fixture(), "A10")
        observations = facts["observations"]
        self.assertTrue(observations["default_window_list_is_both_instances"])
        self.assertTrue(observations["default_door_list_is_both_instances"])
        self.assertTrue(observations["mutation_visible_through_both_surfaces"])
        self.assertTrue(observations["restored_after_observation"])
        self.assertFalse(facts["source_state"]["unchanged"])
        self.assertEqual(facts["source_state"]["before"], facts["source_state"]["final"])
        self.assertEqual(
            [
                "first-default-construction",
                "second-default-construction",
                "append-default-window",
                "append-first-door",
                "restore-shared-default-lists",
            ],
            [item["phase"] for item in facts["timeline"]],
        )

    def test_a11_pins_explicit_collection_alias_and_raw_list_states(self) -> None:
        facts = self.facts(self.fixture(), "A11")
        observations = facts["observations"]
        self.assertTrue(observations["window_input_alias_preserved"])
        self.assertTrue(observations["door_input_alias_preserved"])
        self.assertTrue(observations["input_mutation_visible_on_surface"])
        self.assertTrue(observations["surface_mutation_visible_on_input"])
        self.assertEqual(
            ["Explicit Window 1", "Explicit Window 2", "Explicit Window 3"],
            observations["window_names_after_mutation"],
        )
        self.assertEqual(
            ["Explicit Door 1", "Explicit Door 2", "Explicit Door 3"],
            observations["door_names_after_mutation"],
        )

    def test_a12_pins_fresh_ordered_blinded_window_projection(self) -> None:
        facts = self.facts(self.fixture(), "A12")
        observations = facts["observations"]
        expected = ["Blind 1", "Shade", "Blind 2"]
        self.assertTrue(observations["fresh_projection_lists"])
        self.assertTrue(all(observations["projected_items_are_source_windows"]))
        self.assertEqual(expected, observations["first_projection_before_local_mutation"])
        self.assertEqual(expected, observations["second_projection_after_first_mutation"])
        self.assertEqual(
            ["Plain 1", "Blind 1", "Shade", "Blind 2", "Plain 2"],
            observations["source_window_order_after_projection_mutation"],
        )
        self.assertTrue(facts["source_state"]["unchanged"])

    def test_a13_pins_enum_values_order_string_and_unlinked_zone_state(self) -> None:
        facts = self.facts(self.fixture(), "A13")
        observations = facts["observations"]
        self.assertEqual(
            ["OUTDOOR", "GROUND", "ADIABATIC", "ZONE"],
            observations["definition_order"],
        )
        self.assertEqual(
            ["outdoors", "ground", "adiabatic", "zone"],
            [item["value"] for item in observations["enum_records"]],
        )
        self.assertTrue(all(item["is_str_instance"] for item in observations["enum_records"]))
        self.assertTrue(all(item["round_trip_is_same_member"] for item in observations["enum_records"]))
        self.assertTrue(observations["unlinked_zone_boundary_allowed"])
        self.assertFalse(observations["unlinked_zone_boundary_is_surface"])
        self.assertEqual(
            {
                "error": {
                    "message": "'surface' is not a valid SurfaceBoundaryCondition",
                    "type": "ValueError",
                },
                "outcome": "raised",
                "phase": "invalid-enum-conversion",
            },
            observations["invalid_enum_conversion"],
        )

    def test_a14_and_a15_pin_reciprocal_stale_and_self_mutation(self) -> None:
        value = self.fixture()
        a14 = self.facts(value, "A14")
        self.assertTrue(a14["observations"]["first_getter_returns_second"])
        self.assertTrue(a14["observations"]["second_getter_returns_first"])
        self.assertTrue(a14["observations"]["first_private_condition_is_zone"])
        self.assertTrue(a14["observations"]["second_private_condition_is_zone"])
        self.assertFalse(a14["source_state"]["unchanged"])
        a15 = self.facts(value, "A15")
        self.assertTrue(a15["observations"]["old_retains_stale_first_link"])
        self.assertTrue(a15["observations"]["first_points_to_replacement"])
        self.assertTrue(a15["observations"]["replacement_points_to_first"])
        self.assertTrue(a15["observations"]["self_adjacency_allowed"])
        self.assertEqual(
            [
                "construct-four-surfaces",
                "assign-first-to-old",
                "reassign-first-to-replacement",
                "assign-self-adjacency",
            ],
            [item["phase"] for item in a15["timeline"]],
        )

    def test_a16_pins_positional_zip_and_derived_name_accounting(self) -> None:
        case = self.case(self.fixture(), "A16")
        facts = case["python"]["facts"]
        observations = facts["observations"]
        self.assertNotIn("Surface.to_idf_object", case["target_symbols"])
        self.assertIn("Surface.to_idf_object", case["context_symbols"])
        self.assertEqual(
            ["Window:Interzone", "Window:Interzone", "Door:Interzone", "BuildingSurface:Detailed"],
            observations["first_call"]["object_types"],
        )
        self.assertEqual(
            [
                ["A Window 1", "B Window 2 First"],
                ["A Window 2", "B Window 1 Second"],
                ["A Door 1", "B Door 1"],
                ["B Window 2 First", "A Window 1"],
                ["B Window 1 Second", "A Window 2"],
                ["B Door 1", "A Door 1"],
            ],
            observations["positional_links"],
        )
        accounting = observations["opening_name_accounting"]
        self.assertEqual(
            [
                {
                    "authored_names": [
                        "A Window 1",
                        "A Window 2",
                        "A Window 3 Truncated",
                        "A Door 1",
                        "A Door 2 Truncated",
                    ],
                    "emitted_names": ["A Window 1", "A Window 2", "A Door 1"],
                    "not_emitted_names": [
                        "A Window 3 Truncated",
                        "A Door 2 Truncated",
                    ],
                    "surface_name": "Zip Surface A",
                },
                {
                    "authored_names": [
                        "B Window 2 First",
                        "B Window 1 Second",
                        "B Door 1",
                    ],
                    "emitted_names": [
                        "B Window 2 First",
                        "B Window 1 Second",
                        "B Door 1",
                    ],
                    "not_emitted_names": [],
                    "surface_name": "Zip Surface B",
                },
            ],
            accounting,
        )
        self.assertTrue(
            all(
                item["not_emitted_names"]
                == [
                    name
                    for name in item["authored_names"]
                    if name not in item["emitted_names"]
                ]
                for item in accounting
            )
        )
        self.assertTrue(observations["repeat_links_equal"])
        self.assertTrue(observations["fresh_batches"])
        self.assertTrue(observations["fresh_objects"])
        self.assertTrue(facts["source_state"]["unchanged"])

    def test_a17_pins_linear_scale_bug_and_equal_zero_negative_domain(self) -> None:
        facts = self.facts(self.fixture(), "A17")
        observations = facts["observations"]["target_observations"]
        self.assertEqual([4.0, 16.0, 0.0, -4.0], [self.decode(item["target_area"]) for item in observations])
        self.assertEqual([0.25, 1.0, 0.0, -0.25], [self.decode(item["linear_scale_factor"]) for item in observations])
        self.assertEqual([1.0, 16.0, 0.0, 1.0], [self.decode(item["result_polygon_area"]) for item in observations])
        self.assertTrue(all(item["fresh_result_lists"] for item in observations))
        self.assertTrue(all(item["fresh_vertices"] for item in observations))
        self.assertTrue(all(item["repeat_coordinates_equal"] for item in observations))
        self.assertTrue(facts["source_state"]["unchanged"])

    def test_a18_pins_oversized_error_type_message_timing_and_source_state(self) -> None:
        facts = self.facts(self.fixture(), "A18")
        observations = facts["observations"]
        expected_message = (
            "Tried to create subsurface whose area (20.000m2) is larger than "
            "that of the mother surface (16.000m2)"
        )
        self.assertTrue(observations["errors_equal"])
        self.assertEqual("ValueError", observations["first_error"]["error"]["type"])
        self.assertEqual(expected_message, observations["first_error"]["error"]["message"])
        self.assertEqual(expected_message, observations["second_error"]["error"]["message"])
        self.assertEqual(
            ["oversized-first-call", "oversized-second-call"],
            [item["phase"] for item in facts["timeline"]],
        )
        self.assertTrue(facts["source_state"]["unchanged"])

    @unittest.skipUnless(
        all(
            (PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")).is_file()
            for source in generator.SOURCE_SPECS
        )
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_generation_is_byte_identical_twice_and_matches_fixture(self) -> None:
        bootstrap = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
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
                rogue = imported_root / "idragon" / "shape_opening_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.shape_opening_review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_semantic_source_timing_coverage_and_receipt_tamper_fail_closed(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []

        semantic = self.fixture()
        self.facts(semantic, "A10")["observations"]["mutation_visible_through_both_surfaces"] = False
        self.rehash(semantic)
        changes.append((semantic, "canonical semantics"))

        source = self.fixture()
        self.facts(source, "A17")["source_state"]["unchanged"] = False
        self.rehash(source)
        changes.append((source, "canonical semantics"))

        timing = self.fixture()
        self.facts(timing, "A18")["timeline"][0]["phase"] = "wrong-phase"
        self.rehash(timing)
        changes.append((timing, "canonical semantics"))

        coverage = self.fixture()
        coverage["cases"][15]["target_symbols"] = ["Surface.to_idf_object"]
        self.rehash(coverage)
        changes.append((coverage, "case contract"))

        receipt = self.fixture()
        receipt["target_receipts"][0]["inventory_index"] = 0
        changes.append((receipt, "indexed target receipts"))

        contract = self.fixture()
        contract["consumer_contract"]["closure"]["parent_emission_is_context_only"] = False
        changes.append((contract, "consumer contract"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_values_and_raw_nonfinite_fail(self) -> None:
        stale = self.fixture()
        self.facts(stale, "A01")["observations"]["fresh_instances"] = False
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
            ("C:\\private\\shape.json", "Absolute path"),
            ("/home/private/shape.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-27T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture()
            self.facts(changed, "A01")["unsafe"] = unsafe
            self.rehash(changed)
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            self.assertFalse(math.isfinite(nonfinite))
            changed = self.fixture()
            self.facts(changed, "A01")["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaises(ValueError):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

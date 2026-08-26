"""Fail-closed tests for the pinned Dragon shape geometry-core oracle."""

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
    / "generate_dragon_shape_geometry_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-shape-geometry-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_shape_geometry_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load geometry generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 82_614
EXPECTED_GENERATOR_SHA256 = (
    "sha256:ac340e5ec1b8eba038a947e0425427d1f8498744c69022fb34f2cfabfbf7f252"
)
EXPECTED_FIXTURE_BYTES = 244_637
EXPECTED_FIXTURE_SHA256 = (
    "sha256:46f026a4ce39931ec1e9d3581f49600e4178f3c744d2c6e022263d0fc695d4d8"
)
EXPECTED_CASES_SHA256 = (
    "sha256:7890ed6463624c17ee70d4f0b0b9d684797b0bb55f1d7dae9a32b16a862fd8c7"
)


class DragonShapeGeometryCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="dragon-shape-geometry-tests-")
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
        raise AssertionError(f"Unexpected encoded value: {value!r}")

    @classmethod
    def vertex(cls, values: list[dict[str, object]]) -> tuple[float | int | bool, ...]:
        return tuple(cls.decode(value) for value in values)

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

    def test_artifacts_and_every_hash_layer_are_exactly_pinned(self) -> None:
        value = self.fixture()
        generator_raw = GENERATOR_PATH.read_bytes()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_raw))
        self.assertEqual(EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH))
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(14, len(value["fact_sha256"]))
        self.assertEqual(14, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_inventory_exposes_all_thirty_one_exact_indexed_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(generator._expected_symbol_descriptors(), inventory["symbols"])
        self.assertEqual(generator._expected_target_receipts(), inventory["target_receipts"])
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(31, len(inventory["symbols"]))
        self.assertEqual(
            [
                1034,
                1038,
                1041,
                1043,
                1044,
                1046,
                1047,
                1054,
                1055,
                1056,
                1057,
                1058,
                1059,
                1060,
                1061,
                1063,
                1064,
                1065,
                1066,
                1068,
                1070,
                1071,
                1072,
                1073,
                1074,
                1075,
                1076,
                1077,
                1078,
                1079,
                1080,
            ],
            [item["inventory_index"] for item in inventory["target_receipts"]],
        )
        source = self.fixture()["upstream"]["shape_source"]
        self.assertEqual(27_438, source["bytes"])
        self.assertEqual(generator.SHAPE_SOURCE_SHA256, source["source_sha256"])
        self.assertEqual(generator.SHAPE_AST_SHA256, source["ast_sha256"])

    def test_three_subfamilies_cover_exact_targets_without_promotions(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        closure = contract["closure"]
        self.assertEqual(
            Counter({"vertex": 7, "surface": 5, "surface-type": 2}),
            Counter(case["subfamily"] for case in value["cases"]),
        )
        counts = Counter(
            symbol for case in value["cases"] for symbol in case["target_symbols"]
        )
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(counts))
        self.assertTrue(closure["target_coverage_complete"])
        self.assertFalse(closure["full_symbol_closure"])
        self.assertEqual(3, len(closure["unresolved_target_behavior"]))
        forbidden = set(closure["opening_adjacency_targets_not_promoted"]) | set(
            closure["out_of_scope_symbols_not_promoted"]
        )
        self.assertFalse(forbidden.intersection(counts))
        self.assertIn("Vertex.__rmul__", self.case(value, "V03")["target_symbols"])
        self.assertNotIn("Vertex.__rmul__", self.case(value, "V04")["target_symbols"])
        self.assertIn("Vertex.__rmul__", self.case(value, "V04")["context_symbols"])
        self.assertEqual(
            {"Vertex", "Vertex.__sub__", "Vertex.cross", "Vertex.unit", "Vertex.dot"},
            set(self.case(value, "V06")["context_symbols"]),
        )
        self.assertEqual(
            {"Vertex", "Vertex.__sub__", "Vertex.cross", "Vertex.unit"},
            set(self.case(value, "V07")["context_symbols"]),
        )
        self.assertIn("Vertex.dot", self.case(value, "S09")["context_symbols"])
        self.assertEqual(
            {
                "Surface.__init__",
                "Vertex",
                "Vertex.cross",
                "Vertex.__radd__",
                "Vertex.__add__",
                "Vertex.dot",
            },
            set(self.case(value, "S10")["context_symbols"]),
        )

    def test_classifications_adaptations_assertions_routes_and_signatures_are_total(self) -> None:
        contract = self.fixture()["consumer_contract"]
        targets = set(generator.TARGET_SYMBOLS)
        self.assertEqual(targets, set(contract["classifications"]))
        self.assertEqual(targets, set(contract["adaptations"]))
        self.assertEqual(targets, set(contract["assertion_ids"]))
        self.assertEqual(targets, set(contract["native_targets"]))
        self.assertEqual(targets, set(contract["runtime_signatures"]))
        self.assertEqual(31, len(set(contract["assertion_ids"].values())))
        self.assertEqual({"equivalent": 3, "exception": 28}, contract["classification_counts"])
        self.assertEqual(
            set(generator.EQUIVALENT_SYMBOLS),
            {symbol for symbol, classification in contract["classifications"].items() if classification == "equivalent"},
        )

    def test_v01_pins_mutable_bool_nonfinite_huge_integer_and_rejection_state(self) -> None:
        facts = self.facts(self.fixture(), "V01")
        observations = facts["observations"]
        self.assertEqual([True, True, True], observations["bool_coordinates_preserve_bool_runtime_values"])
        self.assertEqual([401, 501, 601], observations["huge_integer_digit_counts"])
        self.assertEqual(
            ["nan", "positive-infinity", "negative-infinity"],
            observations["nonfinite_classes"],
        )
        self.assertTrue(observations["mutation_returned"])
        self.assertEqual("TypeError", observations["invalid_property_set"]["error"]["type"])
        self.assertEqual("TypeError", observations["invalid_construction"]["error"]["type"])
        self.assertFalse(facts["source_state"]["unchanged"])

    def test_v02_pins_iteration_deepcopy_zero_and_false_radd_alias_behavior(self) -> None:
        facts = self.facts(self.fixture(), "V02")
        observations = facts["observations"]
        self.assertEqual([1.0, 2.0, 3.0], [self.decode(item) for item in observations["iterated_values"]])
        self.assertEqual([], observations["iterator_exhausted_values"])
        self.assertEqual("generator", observations["iterator_type"])
        self.assertTrue(observations["copy_results_fresh"])
        self.assertTrue(observations["copy_states_retained_after_source_mutation"])
        self.assertTrue(observations["false_is_treated_as_zero_addition"])
        self.assertFalse(facts["source_state"]["unchanged"])

    def test_v03_and_v04_pin_untyped_point_vector_algebra_and_error_timing(self) -> None:
        value = self.fixture()
        v03 = self.facts(value, "V03")
        results = v03["observations"]["first_results"]
        self.assertEqual((5.0, -3.0, 9.0), self.vertex(results["add_point_to_point"]))
        self.assertEqual((-3.0, 7.0, -3.0), self.vertex(results["subtract_point_from_point"]))
        self.assertEqual((0.5, 1.0, 1.5), self.vertex(results["divide_by_two"]))
        self.assertEqual(
            {"add": "Vertex", "divide": "Vertex", "multiply": "Vertex", "subtract": "Vertex"},
            v03["observations"]["result_types"],
        )
        self.assertTrue(v03["observations"]["repeat_results_equal"])
        self.assertTrue(v03["source_state"]["unchanged"])
        v04 = self.facts(value, "V04")
        self.assertEqual(
            [
                "AttributeError",
                "AttributeError",
                "TypeError",
                "TypeError",
                "ZeroDivisionError",
                "ZeroDivisionError",
                "TypeError",
            ],
            v04["observations"]["error_types_in_phase_order"],
        )
        self.assertEqual(
            [
                "add-nonzero-int",
                "radd-nonzero-int",
                "multiply-string",
                "rmultiply-string",
                "divide-zero-int",
                "divide-false",
                "divide-string",
            ],
            [event["phase"] for event in v04["observations"]["error_events"]],
        )
        self.assertEqual((0, 0, 0), self.vertex(v04["observations"]["boolean_and_zero_successes"]["false_scaled"]))

    def test_v05_pins_metrics_cross_dot_distance_and_fresh_zero_unit(self) -> None:
        facts = self.facts(self.fixture(), "V05")
        observations = facts["observations"]
        self.assertEqual(5.0, self.decode(observations["norm"]))
        self.assertEqual(13.0, self.decode(observations["distance"]))
        self.assertEqual(0.0, self.decode(observations["dot"]))
        self.assertEqual((48.0, -36.0, 0.0), self.vertex(observations["cross"]))
        self.assertEqual((0.6, 0.8, 0.0), self.vertex(observations["unit"]))
        self.assertEqual((0, 0, 0), self.vertex(observations["zero_unit"]))
        self.assertTrue(observations["zero_unit_fresh_instances"])
        self.assertTrue(observations["zero_unit_repeat_equal"])
        self.assertTrue(facts["source_state"]["unchanged"])

    def test_v06_and_v07_pin_angular_boundary_and_first_three_collinear_defect(self) -> None:
        value = self.fixture()
        v06 = self.facts(value, "V06")
        probes = v06["observations"]["probe_results"]
        self.assertEqual(["below", "exact", "above"], [item["label"] for item in probes])
        self.assertEqual([True, True, False], [item["coplanar"] for item in probes])
        self.assertLess(self.decode(probes[0]["angular_dot"]), 1e-15)
        self.assertEqual(1e-15, self.decode(probes[1]["angular_dot"]))
        self.assertGreater(self.decode(probes[2]["angular_dot"]), 1e-15)
        self.assertTrue(v06["observations"]["empty_arguments_are_coplanar"])
        self.assertTrue(v06["observations"]["three_arguments_short_circuit_true"])
        self.assertEqual("TypeError", v06["observations"]["invalid_argument_error"]["error"]["type"])
        v07 = self.facts(value, "V07")
        self.assertEqual(
            [
                (0.0, 0.0, 0.0),
                (1.0, 0.0, 0.0),
                (2.0, 0.0, 0.0),
                (0.0, 1.0, 0.0),
                (0.0, 0.0, 1.0),
            ],
            [self.vertex(point) for point in v07["source_state"]["before"]],
        )
        self.assertTrue(v07["observations"]["collinear_first_three_returns_true"])
        self.assertFalse(v07["observations"]["reordered_noncollinear_first_three_is_coplanar"])
        self.assertEqual((0, 0, 0), self.vertex(v07["observations"]["collinear_first_three_normal"]))
        self.assertEqual((0.0, 0.0, 1.0), self.vertex(v07["observations"]["reordered_first_three_normal"]))

    def test_s08_pins_rectangle_scalars_tuple_container_and_vertex_aliases(self) -> None:
        facts = self.facts(self.fixture(), "S08")
        state = facts["source_state"]["before"]["surface"]
        self.assertEqual(12.0, self.decode(state["area"]))
        self.assertEqual((2.0, 0.0, 1.5), self.vertex(state["center"]))
        self.assertEqual(3.0, self.decode(state["height"]))
        self.assertEqual((0.0, -1.0, 0.0), self.vertex(state["normal"]))
        self.assertEqual("wall", state["surface_type"])
        self.assertTrue(facts["observations"]["vertex_container_is_tuple"])
        self.assertTrue(all(facts["observations"]["vertex_alias_flags"]))
        self.assertTrue(facts["source_state"]["unchanged"])

    def test_s09_and_s10_pin_winding_and_concave_negative_area(self) -> None:
        value = self.fixture()
        s09 = self.facts(value, "S09")
        self.assertTrue(s09["observations"]["areas_equal"])
        self.assertTrue(s09["observations"]["centers_equal"])
        self.assertTrue(s09["observations"]["heights_equal"])
        self.assertTrue(s09["observations"]["normals_are_opposite"])
        self.assertEqual(-1.0, self.decode(s09["observations"]["normal_dot"]))
        s10 = self.facts(value, "S10")
        self.assertEqual(-12.0, self.decode(s10["observations"]["python_area"]))
        self.assertTrue(s10["observations"]["python_area_is_negative"])
        self.assertTrue(s10["observations"]["normal_opposes_cross_sum"])
        self.assertEqual((0.0, 0.0, 24.0), self.vertex(s10["observations"]["cross_sum"]))

    def test_s11_pins_invalid_polygon_acceptance_and_setter_error_state(self) -> None:
        facts = self.facts(self.fixture(), "S11")
        observations = facts["observations"]
        accepted = observations["accepted_invalid_polygon_states"]
        self.assertEqual(
            {"collinear_triangle", "duplicate_closing_square", "self_intersecting_bow_tie"},
            set(accepted),
        )
        self.assertEqual(0.0, self.decode(accepted["collinear_triangle"]["area"]))
        self.assertEqual(4.0, self.decode(accepted["duplicate_closing_square"]["area"]))
        self.assertEqual(0.0, self.decode(accepted["self_intersecting_bow_tie"]["area"]))
        self.assertEqual(
            {
                "outcome": "returned",
                "phase": "construct-three-invalid-polygons",
                "return_type": "dict",
            },
            facts["timeline"][0],
        )
        self.assertEqual(
            ["ValueError", "TypeError", "ValueError"],
            [item["error"]["type"] for item in observations["rejected_setter_events"]],
        )
        self.assertTrue(observations["setter_state_unchanged_after_errors"])
        self.assertTrue(facts["source_state"]["unchanged"])

    def test_s12_pins_vertex_alias_mutation_reassignment_and_type_error_timing(self) -> None:
        facts = self.facts(self.fixture(), "S12")
        observations = facts["observations"]
        self.assertTrue(observations["alias_mutation_changed_surface_geometry"])
        self.assertTrue(all(observations["replacement_vertex_alias_flags"]))
        self.assertTrue(observations["replacement_alias_mutation_visible"])
        self.assertTrue(observations["tuple_reassignment_returned"])
        self.assertEqual("ValueError", observations["invalid_type_error"]["error"]["type"])
        self.assertEqual("floor", observations["surface_type_after_failed_case_mismatch"])
        self.assertFalse(facts["source_state"]["unchanged"])
        self.assertIn("intermediate_after_source_alias_mutation", facts["source_state"])

    def test_t13_and_t14_pin_enum_topology_and_three_equivalent_members(self) -> None:
        value = self.fixture()
        t13 = self.facts(value, "T13")
        observations = t13["observations"]
        self.assertEqual(["WALL", "CEILING", "FLOOR"], observations["definition_order"])
        self.assertEqual(["wall", "ceiling", "floor"], [item["value"] for item in observations["member_records"]])
        self.assertTrue(all(item["is_str_instance"] for item in observations["member_records"]))
        self.assertTrue(all(item["round_trip_is_same_member"] for item in observations["member_records"]))
        self.assertEqual(
            {
                "SurfaceType.CEILING": "ceiling",
                "SurfaceType.FLOOR": "floor",
                "SurfaceType.WALL": "wall",
            },
            observations["three_direct_member_mappings"],
        )
        t14 = self.facts(value, "T14")
        self.assertTrue(t14["observations"]["no_enum_aliases"])
        self.assertEqual(
            ["ValueError", "ValueError", "ValueError"],
            [item["error"]["type"] for item in t14["observations"]["error_events"]],
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
                rogue = imported_root / "idragon" / "geometry_core_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.geometry_core_review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_semantic_state_timing_coverage_classification_and_receipt_tamper_fail(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []

        semantic = self.fixture()
        self.facts(semantic, "S10")["observations"]["python_area_is_negative"] = False
        self.rehash(semantic)
        changes.append((semantic, "canonical semantics"))

        state = self.fixture()
        self.facts(state, "V06")["source_state"]["unchanged"] = False
        self.rehash(state)
        changes.append((state, "canonical semantics"))

        timing = self.fixture()
        self.facts(timing, "V04")["timeline"][1]["phase"] = "wrong-phase"
        self.rehash(timing)
        changes.append((timing, "canonical semantics"))

        coverage = self.fixture()
        coverage["cases"][0]["target_symbols"] = ["Vertex.__eq__"]
        self.rehash(coverage)
        changes.append((coverage, "case contract"))

        classification = self.fixture()
        classification["consumer_contract"]["classifications"]["Vertex"] = "equivalent"
        changes.append((classification, "consumer contract"))

        receipt = self.fixture()
        receipt["target_receipts"][0]["inventory_index"] = 0
        changes.append((receipt, "indexed target receipts"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_values_and_raw_nonfinite_fail(self) -> None:
        stale = self.fixture()
        self.facts(stale, "V07")["observations"]["collinear_first_three_returns_true"] = False
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
            ("C:\\private\\geometry.json", "Absolute path"),
            ("/home/private/geometry.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-27T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture()
            self.facts(changed, "V01")["unsafe"] = unsafe
            self.rehash(changed)
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            self.assertFalse(math.isfinite(nonfinite))
            changed = self.fixture()
            self.facts(changed, "V01")["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaises(ValueError):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

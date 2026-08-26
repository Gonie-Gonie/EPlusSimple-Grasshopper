"""Fail-closed tests for the pinned Dragon shape Zone-core oracle."""

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
    / "generate_dragon_shape_zone_core_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-shape-zone-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_shape_zone_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load Zone core generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 67_980
EXPECTED_GENERATOR_SHA256 = (
    "sha256:ce86db526f27158c7e81b40e5e6007c090008bfca5612775a01f8df141936666"
)
EXPECTED_FIXTURE_BYTES = 91_202
EXPECTED_FIXTURE_SHA256 = (
    "sha256:63d62d596d37c2e33adcbaf025f37ccda36d8a4291d96b3201d427d8caed59b3"
)
EXPECTED_CASES_SHA256 = (
    "sha256:b42a68ae6532fa179348796c81982a98579c9789b66a453fd5c1eae22d8b964f"
)


class DragonShapeZoneCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="dragon-shape-zone-core-")
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
        self.assertEqual(10, len(value["fact_sha256"]))
        self.assertEqual(10, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_inventory_exposes_exact_target_context_and_resolved_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_inventory(), inventory)
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(8, len(inventory["target_receipts"]))
        self.assertEqual(7, len(inventory["context_receipts"]))
        self.assertEqual(4, len(inventory["resolved_receipts"]))
        self.assertEqual(
            [1083, 1084, 1085, 1086, 1087, 1088, 1089, 1091],
            [item["inventory_index"] for item in inventory["target_receipts"]],
        )
        self.assertEqual(
            [707, 708, 710, 789, 790, 797, 1056],
            [item["inventory_index"] for item in inventory["context_receipts"]],
        )
        self.assertEqual(
            [1090, 1092, 1093, 1094],
            [item["inventory_index"] for item in inventory["resolved_receipts"]],
        )
        source = self.fixture()["upstream"]["shape_source"]
        self.assertEqual(27_438, source["bytes"])
        self.assertEqual(generator.SHAPE_SOURCE_SHA256, source["source_sha256"])
        self.assertEqual(generator.SHAPE_AST_SHA256, source["ast_sha256"])

    def test_scope_has_strict_target_and_context_closure_without_retargeting(self) -> None:
        value = self.fixture()
        definitions = value["cases"]
        target_counts = Counter(
            symbol for case in definitions for symbol in case["target_symbols"]
        )
        context_symbols = {
            symbol for case in definitions for symbol in case["context_symbols"]
        }
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(target_counts))
        self.assertTrue(set(generator.CONTEXT_SYMBOLS).issubset(context_symbols))
        self.assertFalse(set(generator.RESOLVED_SYMBOLS).intersection(target_counts))
        closure = value["consumer_contract"]["closure"]
        self.assertTrue(closure["target_coverage_complete"])
        self.assertFalse(closure["full_symbol_closure"])
        self.assertEqual(8, len(closure["unresolved_boundaries"]))
        self.assertEqual(
            {
                "edge_failure_inputs": ["str:'bad'-as-first-floor-area"],
                "edge_success_inputs": ["bool:True", "int:3", "float:2.5"],
                "representative_finite_input": "float:12.5",
            },
            closure["observed_floor_sum_domain"],
        )
        self.assertEqual(
            list(generator.RESOLVED_RECEIPTS),
            closure["resolved_receipts_not_retargeted"],
        )
        self.assertEqual(
            Counter({"floor": 6, "supply": 2, "container": 1, "naming": 1}),
            Counter(case["subfamily"] for case in definitions),
        )

    def test_classifications_adaptations_assertions_routes_and_signatures_are_total(self) -> None:
        contract = self.fixture()["consumer_contract"]
        targets = set(generator.TARGET_SYMBOLS)
        self.assertEqual(targets, set(contract["classifications"]))
        self.assertEqual(targets, set(contract["adaptations"]))
        self.assertEqual(targets, set(contract["assertion_ids"]))
        self.assertEqual(targets, set(contract["native_targets"]))
        self.assertEqual(targets, set(contract["runtime_signatures"]))
        self.assertEqual(set(generator.CONTEXT_SYMBOLS), set(contract["context_runtime_signatures"]))
        self.assertEqual(8, len(set(contract["assertion_ids"].values())))
        self.assertEqual({"equivalent": 0, "exception": 8}, contract["classification_counts"])
        self.assertEqual({"exception"}, set(contract["classifications"].values()))
        self.assertEqual(
            "GonieGonie.InvisibleDragon.Shape.Zone typed aggregate",
            contract["native_targets"]["Zone"],
        )

    def test_z01_pins_representative_and_unchecked_permissive_construction(self) -> None:
        facts = self.facts(self.fixture(), "Z01")
        permissive = facts["observations"]["permissive_attributes"]
        self.assertIs(self.decode(permissive["name"]), True)
        self.assertTrue(math.isnan(self.decode(permissive["infiltration"])))
        self.assertEqual(401, len(permissive["light_density"]["value"]))
        self.assertEqual("non-sequence-surface-token", permissive["surface_label"])
        self.assertEqual("None", facts["observations"]["representative_attributes"]["supply_type"])
        self.assertEqual(
            ["returned", "returned"],
            [item["outcome"] for item in facts["timeline"]],
        )

    def test_z02_through_z05_pin_floor_identity_order_freshness_and_sum(self) -> None:
        value = self.fixture()
        z02 = self.facts(value, "Z02")["observations"]
        self.assertEqual([], z02["first_floor_labels"])
        self.assertEqual([], z02["second_floor_labels"])
        self.assertEqual(0, self.decode(z02["floor_area"]))
        self.assertFalse(z02["first_list_is_second_list"])
        self.assertTrue(z02["zone_surface_is_authored_list"])
        self.assertEqual(
            [
                {"authored_labels": [], "phase": "before", "zone_surface_labels": []},
                {"authored_labels": [], "phase": "after", "zone_surface_labels": []},
            ],
            self.facts(value, "Z02")["source_state"]["snapshots"],
        )

        z03 = self.facts(value, "Z03")["observations"]
        self.assertEqual(["floor-1"], z03["first_floor_labels"])
        self.assertEqual(12.5, self.decode(z03["floor_area_value"]))
        self.assertNotIn("string-floor.area", z03["trace_after_floor_area"])
        self.assertFalse(z03["first_list_is_second_list"])

        z04 = self.facts(value, "Z04")["observations"]
        self.assertEqual([], z04["first_floor_labels"])
        self.assertEqual(0, self.decode(z04["floor_area_value"]))
        self.assertFalse(any(item.endswith(".area") for item in z04["trace_after_floor_area"]))

        z05 = self.facts(value, "Z05")["observations"]
        self.assertEqual(
            ["floor-bool", "floor-int", "floor-float"],
            z05["first_floor_labels"],
        )
        self.assertEqual(6.5, self.decode(z05["floor_area_value"]))
        self.assertEqual(
            ["floor-bool.area", "floor-int.area", "floor-float.area"],
            [item for item in z05["trace_after_floor_area"] if item.endswith(".area")],
        )

    def test_z06_pins_list_alias_mutation_order_property_mutation_and_reassignment(self) -> None:
        facts = self.facts(self.fixture(), "Z06")
        observations = facts["observations"]
        snapshots = facts["source_state"]["snapshots"]
        self.assertTrue(observations["zone_surface_is_authored_initially"])
        self.assertTrue(observations["zone_surface_is_replacement_after_assignment"])
        self.assertEqual(
            [10, 13.5, 13.5, 9.5, -4, -4],
            [self.decode(item["floor_area"]) for item in snapshots],
        )
        self.assertEqual(
            ["floor-2", "wall", "floor-1"],
            snapshots[2]["zone_surface_labels"],
        )
        self.assertEqual(["floor-2", "floor-1"], snapshots[2]["floor_labels"])
        self.assertEqual(["wall"], snapshots[-1]["floor_labels"])
        self.assertEqual("tuple", snapshots[-1]["zone_surface_container"])

    def test_z07_pins_exact_name_formatting_for_unicode_empty_and_none(self) -> None:
        snapshots = self.facts(self.fixture(), "Z07")["observations"]["name_output_snapshots"]
        self.assertEqual("EquipmentList_for_North Ω / Zone 01", snapshots[0]["equipment_list_name"])
        self.assertEqual(" Air InletNode List", snapshots[1]["air_inlet_node_list_name"])
        self.assertEqual("None Air ExhaustNode List", snapshots[2]["air_exhaust_node_list_name"])
        self.assertEqual("EquipmentList_for_None", snapshots[2]["equipment_list_name"])

    def test_z08_pins_none_direct_system_existing_group_and_constructor_coercion(self) -> None:
        facts = self.facts(self.fixture(), "Z08")
        snapshots = facts["source_state"]["snapshots"]
        self.assertEqual(
            ["None", "SupplyGroup", "SupplyGroup", "None", "SupplyGroup"],
            [item["supply_type"] for item in snapshots],
        )
        self.assertEqual(["first"], snapshots[1]["system_labels"])
        self.assertEqual(["first", "second"], snapshots[2]["system_labels"])
        self.assertEqual(["second"], snapshots[4]["system_labels"])
        self.assertTrue(facts["observations"]["existing_group_is_retained"])
        self.assertTrue(facts["observations"]["wrapped_group_system_is_direct_input"])
        self.assertEqual(
            [
                "construct-first-system",
                "construct-second-system",
                "construct-existing-group",
                "construct-zone-none",
                "assign-direct-system",
                "assign-existing-group",
                "assign-none",
                "construct-zone-direct-system",
            ],
            [item["phase"] for item in facts["timeline"]],
        )
        self.assertEqual(
            [
                "ElectricRadiator",
                "ElectricRadiator",
                "SupplyGroup",
                "Zone",
                "NoneType",
                "NoneType",
                "NoneType",
                "Zone",
            ],
            [item["return_type"] for item in facts["timeline"]],
        )
        self.assertEqual(
            [False, False, False, False, True, True, True, False],
            [item["returned_none"] for item in facts["timeline"]],
        )

    def test_z09_pins_invalid_supply_error_message_state_and_constructor_timing(self) -> None:
        facts = self.facts(self.fixture(), "Z09")
        events = facts["observations"]["setter_error_events"] + [
            facts["observations"]["constructor_error_event"]
        ]
        self.assertEqual(["TypeError"] * 4, [item["error"]["type"] for item in events])
        self.assertEqual(
            ["supply must be a SupplySystem, SupplyGroup, or None."] * 4,
            [item["error"]["message"] for item in events],
        )
        self.assertTrue(
            all(
                item["supply_is_original_group"]
                for item in facts["source_state"]["snapshots"][:4]
            )
        )
        partial = facts["observations"]["partial_constructor_state"]
        self.assertEqual(
            ["infiltration", "light_density", "name", "profile", "surface"],
            partial["attribute_names"],
        )
        self.assertFalse(partial["ventilation_attribute_present"])
        self.assertEqual("missing", self.decode(partial["private_supply_lookup"]))

    def test_z10_pins_floor_projection_error_types_messages_and_access_timing(self) -> None:
        observations = self.facts(self.fixture(), "Z10")["observations"]
        events = observations["error_events"]
        self.assertEqual(
            ["AttributeError", "AttributeError", "TypeError"],
            [item["error"]["type"] for item in events],
        )
        self.assertEqual(
            "unsupported operand type(s) for +: 'int' and 'str'",
            events[2]["error"]["message"],
        )
        self.assertEqual(
            ["string-area.type", "later-floor.type", "string-area.area"],
            observations["string_area_trace"],
        )
        snapshots = self.facts(self.fixture(), "Z10")["source_state"]["snapshots"]
        self.assertEqual(
            [
                "missing-type-before",
                "missing-type-after-floor-surface-error",
                "missing-type-after-floor-area-error",
                "string-area-after",
            ],
            [item["phase"] for item in snapshots],
        )
        self.assertTrue(
            all(
                item["missing_probe_label"] == "missing-type"
                and self.decode(item["zone_name"]) == "missing-type-zone"
                and item["zone_surface_labels"] == ["missing-type"]
                for item in snapshots[:3]
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
                rogue = imported_root / "idragon" / "zone_core_review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.zone_core_review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_semantics_case_contract_classification_and_receipt_tamper_fail(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []

        semantic = self.fixture()
        self.facts(semantic, "Z05")["observations"]["floor_area_value"] = generator._encode(7.5)
        self.rehash(semantic)
        changes.append((semantic, "canonical semantics"))

        case_contract = self.fixture()
        case_contract["cases"][0]["target_symbols"] = ["Zone.to_idf_object"]
        self.rehash(case_contract)
        changes.append((case_contract, "case contract"))

        classification = self.fixture()
        classification["consumer_contract"]["classifications"]["Zone"] = "equivalent"
        changes.append((classification, "consumer contract"))

        target = self.fixture()
        target["target_receipts"][0]["inventory_index"] = 0
        changes.append((target, "indexed target receipts"))

        context = self.fixture()
        context["context_receipts"][0]["inventory_index"] = 0
        changes.append((context, "context receipts"))

        resolved = self.fixture()
        resolved["resolved_receipts"][0]["inventory_index"] = 0
        changes.append((resolved, "resolved receipts"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_values_and_raw_nonfinite_fail(self) -> None:
        stale = self.fixture()
        self.facts(stale, "Z02")["observations"]["first_list_is_second_list"] = True
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8")
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
            self.facts(changed, "Z01")["unsafe"] = unsafe
            self.rehash(changed)
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            changed = self.fixture()
            self.facts(changed, "Z01")["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaises(ValueError):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

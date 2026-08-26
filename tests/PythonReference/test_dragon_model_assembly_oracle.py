"""Fail-closed tests for the bounded dragon-model assembly oracle."""

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
    / "generate_dragon_model_assembly_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-model-assembly-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_model_assembly_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load dragon-model assembly generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 119_000
EXPECTED_FIXTURE_SHA256 = (
    "sha256:f01960724a367eadb8fbee9a5bef3b8d4304449eb9d3c71fa626f4db03024ef4"
)
EXPECTED_CASES_SHA256 = (
    "sha256:c93a9a25726848777d527518a99419f21175fb9e473c0371654fef9fb5f01eaa"
)


class DragonModelAssemblyOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-model-assembly-tests-"
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

    @staticmethod
    def schedule_names(facts: dict[str, object]) -> list[str]:
        return [item["values"][0]["value"] for item in facts["schedule_compact"]]

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

    def test_inventory_binds_twelve_loaded_sources_and_one_exact_symbol(self) -> None:
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
                "src/idragon/__init__.py",
                "src/idragon/common.py",
                "src/idragon/constants.py",
                "src/idragon/dragon/__init__.py",
                "src/idragon/dragon/construction.py",
                "src/idragon/dragon/hvac.py",
                "src/idragon/dragon/model.py",
                "src/idragon/dragon/profile.py",
                "src/idragon/dragon/shape.py",
                "src/idragon/imugi.py",
                "src/idragon/launcher.py",
                "src/idragon/utils.py",
            ],
            [item["path"] for item in inventory["files"]],
        )
        self.assertEqual(("EnergyModel.to_idf",), generator.TARGET_SYMBOLS)

        value = self.fixture()
        loaded = value["upstream"]["loaded_local_modules"]
        self.assertEqual(12, len(loaded))
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)
        self.assertEqual(
            [item["path"] for item in inventory["files"]],
            [item["path"] for item in loaded],
        )

    def test_cases_are_sorted_unique_and_all_bind_to_idf(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(10, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        self.assertTrue(
            all(item["executor"] == "energy-model-to-idf" for item in definitions)
        )

    def test_consumer_contract_is_bounded_needs_reverification_only(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual({}, contract["adaptations"])
        self.assertEqual({}, contract["assertion_ids"])
        self.assertEqual(
            {"EnergyModel.to_idf": "needs_reverification"},
            contract["classifications"],
        )
        self.assertEqual(
            {
                "full_symbol_closure": False,
                "scope": "bounded-behavioral-evidence-only",
                "uncovered_behavior": (
                    "remaining-EnergyModel.to_idf-branches-require-reverification"
                ),
            },
            contract["closure"],
        )
        self.assertEqual(
            "external-temporary-copy-of-pinned-source",
            contract["source_import_policy"],
        )
        for case in value["cases"]:
            self.assertNotIn("expected_dotnet", case)

    def test_fixture_pins_profile_name_and_case_sensitive_schedule_bugs(self) -> None:
        value = self.fixture()
        case_distinct = self.case(
            value,
            "dragon-model-assembly.to-idf.case-distinct-profile-schedules",
        )["python"]["facts"]
        self.assertEqual(
            ["ALLON", "ALLOFF", "CaseLight", "caselight"],
            self.schedule_names(case_distinct),
        )
        self.assertEqual(
            ["CaseLight", "caselight"],
            case_distinct["casefold_schedule_groups"]["caselight"],
        )
        self.assertEqual(
            ["CaseLight", "caselight"],
            [item["schedule_name"] for item in case_distinct["lights"]],
        )

        duplicate = self.case(
            value,
            "dragon-model-assembly.to-idf.duplicate-profile-last-wins-dangling",
        )["python"]["facts"]
        self.assertEqual(
            ["ALLON", "ALLOFF", "Light-B"], self.schedule_names(duplicate)
        )
        self.assertEqual(["Light-A"], duplicate["missing_schedule_references"])
        self.assertEqual(
            [{"lighting_schedule": "Light-B", "name": "DUPLICATE-PROFILE"}],
            duplicate["used_profiles"],
        )
        self.assertEqual(
            ["Light-A", "Light-B"],
            [item["schedule_name"] for item in duplicate["lights"]],
        )

    def test_fixture_pins_exact_default_object_raw_fields(self) -> None:
        value = self.fixture()
        assigned = self.case(
            value,
            "dragon-model-assembly.to-idf.assigned-without-availability-fallback",
        )["python"]["facts"]
        defaults = assigned["default_objects"]
        self.assertEqual(generator._default_object_facts(), defaults)

        geometry = defaults["global_geometry_rules"][0]
        self.assertEqual(5, geometry["stored_field_count"])
        self.assertEqual(
            [
                "UpperLeftCorner",
                "Counterclockwise",
                "World",
                "Relative",
                "Relative",
            ],
            [field["value"] for field in geometry["values"]],
        )

        activity = defaults["people_activity_schedule_constants"][0]
        self.assertEqual(3, activity["stored_field_count"])
        self.assertEqual(
            {
                "enum_type": "ScheduleType",
                "kind": "enum",
                "text": "real",
                "value": "real",
            },
            activity["values"][1],
        )
        self.assertEqual({"kind": "float", "repr": "107.0"}, activity["values"][2])

        self.assertEqual(
            ["ALLON", "ALLOFF"],
            [item["values"][0]["value"] for item in defaults["schedule_compact"]],
        )
        self.assertEqual(
            [{"kind": "none"}, {"kind": "none"}],
            [item["values"][1] for item in defaults["schedule_compact"]],
        )
        self.assertEqual(
            [
                "ScheduleTypeLimits:Temperature",
                "ScheduleTypeLimits:Onoff",
                "ScheduleTypeLimits:Fraction",
                "ScheduleTypeLimits:Real",
            ],
            [
                item["values"][0]["value"]
                for item in defaults["schedule_type_limits"]
            ],
        )
        self.assertTrue(
            all(item["stored_field_count"] == 5 for item in defaults["schedule_type_limits"])
        )

    def test_fixture_pins_shared_fallback_erv_and_skipped_assignment(self) -> None:
        value = self.fixture()
        two = self.case(
            value,
            "dragon-model-assembly.to-idf.two-unconditioned-shared-fallback",
        )["python"]["facts"]
        self.assertEqual(1, two["allon_object_count"])
        self.assertEqual(1, len(two["fallback_thermostats"]))
        self.assertEqual(2, len(two["fallback_ideal_loads"]))
        self.assertEqual(
            ["Unconditioned-First", "Unconditioned-Second"],
            [item["values"][0]["value"] for item in two["fallback_ideal_loads"]],
        )
        self.assertEqual(
            ["UNCONDITIONED_THERMOSTAT", "UNCONDITIONED_THERMOSTAT"],
            [item["values"][1]["value"] for item in two["fallback_ideal_loads"]],
        )

        erv = self.case(
            value, "dragon-model-assembly.to-idf.legacy-erv-unconditioned"
        )["python"]["facts"]
        self.assertEqual([], erv["heat_recovery_nonempty_families"])
        ventilation = erv["ventilation"][0]
        self.assertEqual(26, ventilation["stored_field_count"])
        self.assertEqual({"kind": "float", "repr": "0.00332"}, ventilation["values"][6])
        self.assertEqual({"kind": "float", "repr": "125.0"}, ventilation["values"][9])
        self.assertEqual({"kind": "float", "repr": "0.85"}, ventilation["values"][10])

        assigned = self.case(
            value,
            "dragon-model-assembly.to-idf.assigned-without-availability-fallback",
        )["python"]["facts"]
        self.assertEqual(["Assigned-Electric"], assigned["assigned_supply_names"])
        self.assertFalse(assigned["zone_is_conditioned"])
        self.assertEqual(
            {
                "DesignSpecification:OutdoorAir": 0,
                "Sizing:Zone": 0,
                "ZoneControl:Thermostat": 0,
                "ZoneHVAC:Baseboard:RadiantConvective:Electric": 0,
                "ZoneHVAC:EquipmentList": 0,
            },
            assigned["absent_object_counts"],
        )

    def test_parent_orchestration_success_binds_order_identity_and_dedup(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.ORCHESTRATION_SUCCESS_CASE_ID)
        self.assertEqual("returned", case["python"]["outcome"])
        facts = case["python"]["facts"]
        self.assertTrue(facts["returned_default_idf_identity"])
        self.assertTrue(facts["model_membership_unchanged"])
        self.assertEqual(
            {
                "conditioned_zones": 2,
                "surfaces": 1,
                "unconditioned_zones": 2,
                "used_layers": 1,
                "used_profiles": 1,
            },
            facts["projection_read_counts"],
        )
        self.assertEqual(51, len(facts["events"]))
        self.assertEqual(
            ["object:layer-1", "object:layer-2"],
            facts["append_batches"][0],
        )
        self.assertEqual(
            ["fallback-thermostat"], facts["append_batches"][-4]
        )
        self.assertEqual(
            ["fallback-ideal:zone-unconditioned"],
            facts["append_batches"][-3],
        )
        self.assertEqual(["object:pv-1"], facts["append_batches"][-2])
        self.assertEqual(["object:pv-2"], facts["append_batches"][-1])

        events = facts["events"]
        self.assertEqual("default.create", events[0]["event"])
        self.assertEqual("building", events[1]["family"])
        self.assertEqual(
            {
                "Name": "trace-model",
                "North Axis": 17,
                "Solar Distribution": "MinimalShadowing",
                "Terrain": "TraceTerrain",
            },
            events[2]["fields"],
        )
        shared_source_calls = [
            event
            for event in events
            if event["event"] == "converter.call"
            and event["label"] == "source-shared"
        ]
        self.assertEqual(1, len(shared_source_calls))
        source_reads = [
            event for event in events if event["event"] == "supply.sources.read"
        ]
        self.assertEqual(
            [
                ["source-shared", "source-2"],
                ["source-shared"],
            ],
            [event["sources"] for event in source_reads],
        )
        self.assertFalse(
            any(
                event["event"] == "converter.call"
                and event["label"] == "air-boundary-duplicate"
                for event in events
            )
        )
        delegates = [
            event for event in events if event["event"] == "supply.delegate"
        ]
        self.assertEqual(["zone-1", "zone-2"], [item["zone"] for item in delegates])
        self.assertTrue(
            all(
                item["idf_identity_aligned"]
                and item["supply_identity_aligned"]
                and item["result"] == "returned"
                for item in delegates
            )
        )

    def test_parent_orchestration_failure_prefixes_are_exact_and_bounded(self) -> None:
        value = self.fixture()

        layer = self.case(value, generator.ORCHESTRATION_LAYER_FAILURE_CASE_ID)
        self.assertEqual("raised", layer["python"]["outcome"])
        layer_facts = layer["python"]["facts"]
        self.assertEqual(generator._trace_error("layer-2"), layer_facts["error"])
        self.assertEqual([], layer_facts["append_batches"])
        self.assertEqual(
            generator._trace_conversion("layer", "layer-2", result="raised"),
            layer_facts["events"][-1],
        )
        self.assertFalse(layer_facts["returned_default_idf_identity"])

        source = self.case(value, generator.ORCHESTRATION_SOURCE_FAILURE_CASE_ID)
        self.assertEqual("raised", source["python"]["outcome"])
        source_facts = source["python"]["facts"]
        self.assertEqual(generator._trace_error("source-2"), source_facts["error"])
        self.assertEqual(
            generator._trace_conversion("source", "source-2", result="raised"),
            source_facts["events"][-1],
        )
        self.assertIn(["object:source-shared"], source_facts["append_batches"])
        self.assertNotIn(["object:source-2"], source_facts["append_batches"])
        self.assertFalse(
            any(
                event["event"] in {"supply.delegate", "idf-object.create"}
                or (
                    event["event"] == "converter.call"
                    and event["kind"] == "photovoltaic"
                )
                for event in source_facts["events"]
            )
        )

        supply = self.case(
            value, generator.ORCHESTRATION_ADD_SUPPLY_FAILURE_CASE_ID
        )
        self.assertEqual("raised", supply["python"]["outcome"])
        supply_facts = supply["python"]["facts"]
        self.assertEqual(
            generator._trace_error("supply-zone-2"), supply_facts["error"]
        )
        delegates = [
            event
            for event in supply_facts["events"]
            if event["event"] == "supply.delegate"
        ]
        self.assertEqual(
            [("zone-1", "returned"), ("zone-2", "raised")],
            [(event["zone"], event["result"]) for event in delegates],
        )
        self.assertFalse(
            any(
                event["event"] == "idf-object.create"
                or (
                    event["event"] == "converter.call"
                    and event["kind"] == "photovoltaic"
                )
                for event in supply_facts["events"]
            )
        )

        photovoltaic = self.case(
            value, generator.ORCHESTRATION_PV_FAILURE_CASE_ID
        )
        self.assertEqual("raised", photovoltaic["python"]["outcome"])
        photovoltaic_facts = photovoltaic["python"]["facts"]
        self.assertEqual(
            generator._trace_error("pv-2"), photovoltaic_facts["error"]
        )
        self.assertEqual(
            generator._trace_conversion("photovoltaic", "pv-2", result="raised"),
            photovoltaic_facts["events"][-1],
        )
        self.assertEqual(["object:pv-1"], photovoltaic_facts["append_batches"][-1])
        self.assertNotIn(["object:pv-2"], photovoltaic_facts["append_batches"])
        self.assertTrue(photovoltaic_facts["model_membership_unchanged"])
        self.assertFalse(photovoltaic_facts["returned_default_idf_identity"])

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
        all(
            (PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")).is_file()
            for source in generator.SOURCE_SPECS
        )
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_loaded_local_module_without_receipt_fails_closed(self) -> None:
        with self.assertRaisesRegex(SystemExit, "lacks an exact receipt"):
            with generator._pinned_modules(PINNED_SOURCE_ROOT) as modules:
                imported_root = Path(modules.model.__file__).resolve().parents[2]
                rogue = imported_root / "idragon" / "review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_root_contract_runtime_source_symbol_and_semantic_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root"))
        case = self.fixture()
        case["cases"][0]["executor"] = "wrong"
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "contract"))
        consumer = self.fixture()
        consumer["consumer_contract"]["closure"]["full_symbol_closure"] = True
        changes.append((consumer, "consumer contract"))
        runtime = self.fixture()
        runtime["runtime"]["python_dont_write_bytecode"] = False
        changes.append((runtime, "runtime"))
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
        semantic["cases"][0]["python"]["facts"]["object_count"] = 24
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_unsafe_values_raw_float_stale_hash_and_duplicate_keys_fail(self) -> None:
        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            ("C:\\private\\dragon-model-assembly.json", "Absolute path"),
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
        stale_hash["cases"][0]["python"]["facts"]["object_count"] = 24
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

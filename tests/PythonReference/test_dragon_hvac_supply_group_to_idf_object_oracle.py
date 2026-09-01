"""Fail-closed tests for the bounded SupplyGroup.to_idf_object oracle."""

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
    / "generate_dragon_hvac_supply_group_to_idf_object_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-hvac-supply-group-to-idf-object-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_hvac_supply_group_to_idf_object_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load SupplyGroup.to_idf_object generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 22_605
EXPECTED_FIXTURE_SHA256 = (
    "sha256:e5e47e5ffa2d725697d8741d05f54655705106e4bb75348c6d9eff46e04715bc"
)
EXPECTED_CASES_SHA256 = (
    "sha256:8937d915b40bde81aff7b1481bf0d747a878dbefe464c28d091b4bb7d4ba8f0e"
)


class DragonHvacSupplyGroupToIdfObjectOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-hvac-supply-group-to-idf-object-tests-"
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

    def test_inventory_binds_twelve_loaded_sources_and_exact_symbol(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(("SupplyGroup.to_idf_object",), generator.TARGET_SYMBOLS)

        value = self.fixture()
        loaded = value["upstream"]["loaded_local_modules"]
        self.assertEqual(12, len(loaded))
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)
        self.assertEqual(
            [item["path"] for item in inventory["files"]],
            [item["path"] for item in loaded],
        )
        self.assertEqual(
            [
                {
                    "body_hash": "sha256:8660a470290bde21a0cc246e107e2362b5698153e7585ea05a1a69367b1342fa",
                    "kind": "function",
                    "path": "src/idragon/dragon/hvac.py",
                    "signature_hash": "sha256:1dd75b2e8cc87cb78c35a6df6c2423c532b8ea9e29f24b53d113cdffdd42d2ec",
                    "symbol": "SupplyGroup.to_idf_object",
                    "symbol_hash": "sha256:3f9c508c5b0d784d27bc327dfe65c84bd7d17ffc144615b852c37b59cbe51a41",
                }
            ],
            value["symbols"],
        )

    def test_cases_are_exact_sorted_and_bind_reviewed_adaptation(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(3, len(identifiers))
        self.assertEqual(3, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        for definition in definitions:
            self.assertEqual("supply-group-to-idf-object", definition["executor"])
            self.assertEqual("SupplyGroup.to_idf_object", definition["symbol"])
            self.assertEqual(
                {
                    "adaptation": "model-context-supply-group-idf-assembly",
                    "outcome": "returned",
                },
                definition["expected_dotnet"],
            )

    def test_consumer_contract_is_bounded_and_names_unresolved_boundaries(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {"SupplyGroup.to_idf_object": "model-context-supply-group-idf-assembly"},
            contract["adaptations"],
        )
        self.assertEqual(
            {
                "SupplyGroup.to_idf_object": (
                    "dragon-hvac-supply-group-to-idf-object-3f9c508c"
                )
            },
            contract["assertion_ids"],
        )
        self.assertEqual(
            {"SupplyGroup.to_idf_object": "exception"},
            contract["classifications"],
        )
        self.assertEqual(
            {"SupplyGroup.to_idf_object": "EnergyModel.ToIdfDocument"},
            contract["native_targets"],
        )
        self.assertEqual(
            {
                "full_symbol_closure": False,
                "scope": "bounded-model-context-supply-group-idf-assembly-adaptation",
                "unresolved_behavior": [
                    "SupplyGroup",
                    "standalone-SupplyGroup-converter-API-shape",
                    "SupplySystem.to_idf_object",
                    "SourceSystem.to_idf_object",
                    "SequentialLoadFractionController",
                    "SequentialLoadFractionController.run",
                    "concrete-supply-system-converters",
                    "supply-system-postprocessor-run-behavior",
                    "arbitrary-probe-systems-and-schedules",
                    "EnergyModel.to_idf",
                ],
            },
            contract["closure"],
        )
        self.assertEqual(["SupplyGroup.to_idf_object"], contract["target_symbols"])
        self.assertNotIn("SupplyGroup", contract["classifications"])
        self.assertNotIn("EnergyModel.to_idf", contract["classifications"])

    def test_availability_failure_occurs_immediately_after_aligned_system(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[0])
        facts = case["python"]["facts"]
        self.assertEqual("raised", case["python"]["outcome"])
        self.assertEqual(
            {
                "args": ["availability-failure:first"],
                "message": "availability-failure:first",
                "outcome": "raised",
                "type": "RuntimeError",
            },
            facts["error"],
        )
        self.assertEqual(["first-object"], facts["created_object_labels_before_failure"])
        self.assertEqual(
            ["first-processor"], facts["created_processor_labels_before_failure"]
        )
        self.assertEqual(
            [
                "capability.read",
                "capability.read",
                "system.to_idf_object",
                "availability.to_idf_object",
            ],
            [event["event"] for event in facts["events"]],
        )
        self.assertEqual(
            ["heatable", "coolable"],
            [event["property"] for event in facts["events"][:2]],
        )
        system_event = facts["events"][2]
        self.assertTrue(system_event["zone_identity_aligned"])
        self.assertTrue(system_event["availability_identity_aligned"])
        self.assertEqual("availability-first", system_event["availability"])
        self.assertEqual(1, facts["failing_availability_call_count"])
        self.assertEqual(1, facts["first_system_call_count"])
        self.assertEqual(0, facts["second_system_call_count"])
        self.assertEqual(0, facts["second_availability_call_count"])
        self.assertFalse(facts["returned_lists_observed"])
        self.assertFalse(facts["sequential_controller_returned"])

    def test_success_flattens_in_zip_order_and_returns_fresh_lists(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[1])
        facts = case["python"]["facts"]
        self.assertEqual("returned", case["python"]["outcome"])
        object_labels = [
            "heat-object-first",
            "heat-object-second",
            "availability-heat-object",
            "both-object",
            "cool-object",
            "availability-cool-object",
        ]
        processor_labels = [
            "heat-processor",
            "both-processor-first",
            "both-processor-second",
            "cool-processor",
            "SequentialLoadFractionController",
        ]
        self.assertEqual(object_labels, facts["first_object_labels"])
        self.assertEqual(object_labels, facts["second_object_labels"])
        self.assertEqual(processor_labels, facts["first_processor_labels"])
        self.assertEqual(processor_labels, facts["second_processor_labels"])
        self.assertEqual("list", facts["object_result_type"])
        self.assertEqual("list", facts["processor_result_type"])
        for key in (
            "all_availability_identities_aligned",
            "all_zone_identities_aligned",
            "availability_objects_immediately_follow_owner",
            "child_objects_fresh",
            "child_processors_fresh",
            "fresh_object_list",
            "fresh_processor_list",
            "fresh_sequential_controller",
            "sequential_controller_group_identity",
            "sequential_controller_last",
            "sequential_controller_zone_identity",
        ):
            self.assertTrue(facts[key], key)
        self.assertEqual(
            ["heatable", "coolable"] * 6,
            facts["capability_read_order"],
        )
        system_events = [
            event for event in facts["events"] if event["event"] == "system.to_idf_object"
        ]
        self.assertEqual(
            [
                ("heat-only", True, False, "availability-heat"),
                ("both", True, True, None),
                ("cool-only", False, True, "availability-cool"),
            ]
            * 2,
            [
                (
                    event["system"],
                    event["for_heating"],
                    event["for_cooling"],
                    event["availability"],
                )
                for event in system_events
            ],
        )
        availability_events = [
            event
            for event in facts["events"]
            if event["event"] == "availability.to_idf_object"
        ]
        self.assertEqual(
            ["availability-heat", "availability-cool"] * 2,
            [event["availability"] for event in availability_events],
        )

    def test_system_failure_preserves_only_completed_prefix_side_effects(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[2])
        facts = case["python"]["facts"]
        self.assertEqual("raised", case["python"]["outcome"])
        self.assertEqual("system-failure:second", facts["error"]["message"])
        self.assertEqual(
            ["first-object-first", "first-object-second", "availability-first-object"],
            facts["created_object_labels_before_failure"],
        )
        self.assertEqual(
            ["first-processor"], facts["created_processor_labels_before_failure"]
        )
        self.assertEqual(
            [
                "capability.read",
                "capability.read",
                "system.to_idf_object",
                "availability.to_idf_object",
                "capability.read",
                "capability.read",
                "system.to_idf_object",
            ],
            [event["event"] for event in facts["events"]],
        )
        self.assertEqual(1, facts["first_system_call_count"])
        self.assertEqual(1, facts["first_availability_call_count"])
        self.assertEqual(1, facts["second_system_call_count"])
        self.assertEqual(0, facts["second_availability_call_count"])
        self.assertEqual(0, facts["third_system_call_count"])
        self.assertEqual(0, facts["third_availability_call_count"])
        self.assertFalse(facts["returned_lists_observed"])
        self.assertFalse(facts["sequential_controller_returned"])

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
                imported_root = Path(modules.hvac.__file__).resolve().parents[2]
                rogue = imported_root / "idragon" / "review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.review_probe"] = SimpleNamespace(__file__=str(rogue))

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
        loaded = self.fixture()
        loaded["upstream"]["loaded_local_modules"][0]["module"] = "idragon.wrong"
        changes.append((loaded, "upstream"))
        symbol = self.fixture()
        symbol["symbols"][0]["symbol_hash"] = "sha256:" + "0" * 64
        changes.append((symbol, "symbol"))
        semantic = self.fixture()
        semantic["cases"][0]["python"]["facts"]["first_system_call_count"] = 2
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_values_and_nonfinite_fail(self) -> None:
        stale = self.fixture()
        stale["cases"][0]["python"]["facts"]["first_system_call_count"] = 2
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
            ("C:\\private\\supply-group.json", "Absolute path"),
            ("/home/private/supply-group.json", "Absolute path"),
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

"""Fail-closed tests for the pinned InvisibleDragon supply-core oracle."""

from __future__ import annotations

from collections import Counter
import copy
import importlib.util
import os
from pathlib import Path
import subprocess
import sys
import tempfile
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
    / "generate_dragon_hvac_supply_core_oracle.py"
)
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-hvac-supply-core-oracle.json"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
BOOTSTRAP_PATH = (
    REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
)

EXPECTED_GENERATOR_BYTES = 65_859
EXPECTED_GENERATOR_SHA256 = (
    "sha256:7ce1af80729c2f2aa333016ba95db3963b25db24e1b23d2c89f49ea2694590e2"
)
EXPECTED_FIXTURE_BYTES = 215_230
EXPECTED_FIXTURE_SHA256 = (
    "sha256:657b53b768c90a2915ca10c781ff63ab5a21323bb09f534d4d5da3178fe99194"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_hvac_supply_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load supply-core generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class DragonHvacSupplyCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-hvac-supply-core-validator-"
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
    def case(value: dict[str, object], code: str) -> dict[str, object]:
        return next(item for item in value["cases"] if item["code"] == code)

    def test_generator_and_fixture_are_exact_strict_utf8_receipts(self) -> None:
        self.assertEqual(EXPECTED_GENERATOR_BYTES, GENERATOR_PATH.stat().st_size)
        self.assertEqual(EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH))
        self.assertEqual(EXPECTED_FIXTURE_BYTES, FIXTURE_PATH.stat().st_size)
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        raw = FIXTURE_PATH.read_bytes()
        self.assertTrue(raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", raw)
        value = self.fixture()
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            raw.decode("utf-8"),
        )

    def test_inventory_is_exact_49_plus_8_nine_family_closure(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        targets = inventory["target_receipts"]
        adjacent = inventory["adjacent_receipts"]
        self.assertEqual(49, len(targets))
        self.assertEqual(8, len(adjacent))
        self.assertEqual(
            generator.TARGET_INDEX_SYMBOLS,
            tuple((item["inventory_index"], item["symbol"]) for item in targets),
        )
        self.assertEqual(
            generator.ADJACENT_INDEX_SYMBOLS,
            tuple((item["inventory_index"], item["symbol"]) for item in adjacent),
        )
        self.assertEqual(
            generator.EXPECTED_TARGET_RECEIPTS_SHA256,
            generator.canonical_sha256(targets),
        )
        self.assertEqual(
            generator.EXPECTED_ADJACENT_RECEIPTS_SHA256,
            generator.canonical_sha256(adjacent),
        )
        self.assertEqual(
            generator.EXPECTED_FAMILY_CLOSURE_SHA256,
            generator.canonical_sha256(targets + adjacent),
        )
        self.assertEqual(57, len(targets + adjacent))
        self.assertTrue(
            all(item["path"] == generator.SOURCE_PATH for item in targets + adjacent)
        )

    def test_cases_partition_targets_once_and_leave_adjacent_unpromoted(self) -> None:
        value = self.fixture()
        definitions = generator.case_definitions()
        self.assertEqual(9, len(definitions))
        target_counts = Counter(
            symbol for item in definitions for symbol in item["target_symbols"]
        )
        context_counts = Counter(
            symbol for item in definitions for symbol in item["context_symbols"]
        )
        self.assertEqual(
            Counter({symbol: 1 for symbol in generator.TARGET_SYMBOLS}), target_counts
        )
        self.assertEqual(
            Counter({symbol: 1 for symbol in generator.ADJACENT_SYMBOLS}),
            context_counts,
        )
        self.assertTrue(set(target_counts).isdisjoint(context_counts))
        self.assertEqual(generator.EXPECTED_CASE_IDS, tuple(item["id"] for item in definitions))
        self.assertEqual(
            generator.EXPECTED_CASES_SHA256,
            generator.canonical_sha256(value["cases"]),
        )
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])

    def test_contract_is_18_equivalent_31_exception_and_public_only(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(
            Counter({"equivalent": 18, "exception": 31}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertFalse(contract["internal_generate_claimed"])
        self.assertEqual(
            {
                "AirHandlingUnit.__deepcopy__": "out_of_scope",
                "SupplyGroup.__init__": "exception",
                "SupplyGroup.coolable": "equivalent",
                "SupplyGroup.cooling_systems": "equivalent",
                "SupplyGroup.heatable": "equivalent",
                "SupplyGroup.heating_systems": "equivalent",
                "SupplyGroup.sources": "exception",
                "SupplyGroup.to_idf_object": "exception",
            },
            contract["closure"]["adjacent_existing_status"],
        )
        self.assertEqual(49, contract["closure"]["target_count"])
        self.assertEqual(57, contract["closure"]["family_declaration_count"])
        self.assertTrue(contract["closure"]["full_family_closure"])
        self.assertTrue(
            all("Generate" not in route for route in contract["native_routes"].values())
        )
        self.assertEqual(
            49, len({item["assertion_id"] for item in value["symbols"]})
        )

    def test_support_native_runtime_source_and_relocation_pins_are_exact(self) -> None:
        value = self.fixture()
        self.assertEqual(
            generator.EXPECTED_SUPPORT_FIXTURES_SHA256,
            generator.canonical_sha256(value["support_fixtures"]),
        )
        self.assertEqual(3, len(value["support_fixtures"]))
        self.assertEqual(
            [item["path"] for item in generator.SUPPORT_FIXTURES],
            [item["path"] for item in value["support_fixtures"]],
        )
        self.assertEqual(
            generator.EXPECTED_NATIVE_REVIEW_SHA256,
            generator.canonical_sha256(value["native_review"]),
        )
        self.assertEqual(5, len(value["native_review"]["native_sources"]))
        self.assertEqual(
            {"equivalent": 18, "exception": 31, "total": 49},
            value["native_review"]["counts"],
        )
        self.assertIn(
            "Internal Generate members are intentionally not evidence routes.",
            value["native_review"]["public_route_boundary"],
        )
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(
                value["consumer_contract"]["runtime_signatures"]
            ),
        )
        self.assertEqual(
            generator.EXPECTED_LOADED_LOCAL_MODULES_SHA256,
            value["upstream"]["loaded_local_modules_sha256"],
        )
        self.assertEqual(12, len(value["upstream"]["loaded_local_modules"]))
        self.assertEqual(
            generator.EXPECTED_RELOCATED_OBSERVATIONS_SHA256,
            value["upstream"]["relocation"]["observations_sha256"],
        )
        self.assertTrue(value["upstream"]["relocation"]["path_independent"])
        self.assertEqual(
            generator.EXPECTED_SOURCE_SHA256,
            value["upstream"]["source"]["source_sha256"],
        )

    def test_concrete_supply_behaviors_execute_real_idf_paths(self) -> None:
        value = self.fixture()
        expected = {
            "A01": (True, True, 8, 3),
            "EF01": (True, False, 4, 1),
            "E01": (True, False, 1, 1),
            "P01": (False, True, 8, 3),
            "RF01": (True, False, 6, 2),
            "R01": (True, False, 3, 2),
        }
        conversion_key = {
            "A01": "explicit_cooling_only_conversion",
            "EF01": "explicit_conversion",
            "E01": "explicit_conversion",
            "P01": "explicit_conversion",
            "RF01": "explicit_conversion",
            "R01": "explicit_conversion",
        }
        for code, (heatable, coolable, objects, processors) in expected.items():
            facts = self.case(value, code)["python"]["facts"]
            self.assertEqual(heatable, facts["capabilities"]["heatable"], code)
            self.assertEqual(coolable, facts["capabilities"]["coolable"], code)
            conversion = facts[conversion_key[code]]
            self.assertEqual(objects, conversion["object_count"], code)
            self.assertEqual(processors, conversion["processor_count"], code)
            self.assertEqual(objects, len(conversion["object_receipts"]), code)
            self.assertEqual(
                conversion["object_receipts_sha256"],
                generator.canonical_sha256(conversion["object_receipts"]),
                code,
            )

        for code in ("EF01", "E01", "RF01", "R01"):
            attempt = self.case(value, code)["python"]["facts"][
                "invalid_cooling_request"
            ]
            self.assertEqual("raised", attempt["outcome"])
            self.assertEqual("ValueError", attempt["type"])

        air = self.case(value, "A01")["python"]["facts"]
        self.assertEqual("Main AHU:COPY", air["deepcopy"]["clone_name"])
        self.assertTrue(air["deepcopy"]["clone_is_fresh"])
        self.assertTrue(air["deepcopy"]["source_identity_preserved"])
        self.assertTrue(air["repeat_default_conversion"]["same_summary"])
        self.assertTrue(air["repeat_default_conversion"]["fresh_object_list"])
        self.assertEqual(
            "ZoneHVAC:TerminalUnit:VariableRefrigerantFlow",
            air["idf_objtypename"],
        )

    def test_fan_coil_group_availability_source_and_naming_quirks_are_exact(self) -> None:
        value = self.fixture()
        fan = self.case(value, "F01")["python"]["facts"]
        self.assertEqual(
            {
                "absorption": (False, True),
                "boiler": (True, False),
                "chiller": (False, True),
                "heat_pump": (False, False),
                "none": (False, False),
            },
            {
                key: (item["heatable"], item["coolable"])
                for key, item in fan["source_combinations"].items()
            },
        )
        self.assertEqual(73, fan["boiler_heating_conversion"]["object_count"])
        self.assertEqual(41, fan["chiller_cooling_conversion"]["object_count"])
        self.assertEqual(3, fan["boiler_heating_conversion"]["processor_count"])
        self.assertEqual(3, fan["chiller_cooling_conversion"]["processor_count"])

        group = self.case(value, "G01")["python"]["facts"]
        self.assertEqual(["Group PAC", "Group Electric"], group["system_order"])
        self.assertEqual(["Group Electric"], group["heating_systems"]["order"])
        self.assertEqual(["Group PAC"], group["cooling_systems"]["order"])
        self.assertEqual(["Group Source"], group["sources"]["order"])
        for key in ("heating_systems", "cooling_systems", "sources"):
            self.assertTrue(group[key]["fresh_tuple"])
            self.assertTrue(group[key]["same_member_identity"])
        self.assertEqual(["Explicit Availability", None], group["availability_order"])
        self.assertEqual(2, group["availability_call_count_after_repeat"])
        self.assertEqual(10, group["conversion"]["first"]["object_count"])
        self.assertEqual(
            "SequentialLoadFractionController",
            group["conversion"]["first"]["processor_type_order"][-1],
        )
        self.assertEqual(
            {
                "availability_count_mismatch": "ValueError",
                "empty": "ValueError",
                "incapable": "ValueError",
                "wrong_type": "TypeError",
            },
            {key: item["type"] for key, item in group["validation"].items()},
        )

        base = self.case(value, "S01")["python"]["facts"]
        self.assertTrue(base["abstract"])
        self.assertTrue(base["idf_objtypename_abstract"])
        self.assertTrue(base["to_idf_object_abstract"])
        self.assertEqual("TypeError", base["direct_instantiation"]["type"])
        self.assertEqual(
            "ProbeSupply_named_Naming Probe_for_Naming Zone",
            base["helpers"]["object"],
        )
        self.assertEqual(
            base["helpers"]["object"] + " Air InletNode",
            base["helpers"]["air_inlet"],
        )
        self.assertEqual(
            base["helpers"]["object"] + " Air OutletNode",
            base["helpers"]["air_outlet"],
        )

    def test_fail_closed_mutations_are_rejected(self) -> None:
        value = self.fixture()
        mutations = []

        promoted = copy.deepcopy(value)
        promoted["cases"][0]["target_symbols"].append(
            generator.ADJACENT_SYMBOLS[0]
        )
        mutations.append(promoted)

        internal_route = copy.deepcopy(value)
        internal_route["consumer_contract"]["native_routes"][
            "AirHandlingUnit.to_idf_object"
        ] = "AirHandlingUnit.Generate"
        mutations.append(internal_route)

        changed_fact = copy.deepcopy(value)
        changed_fact["cases"][0]["python"]["facts"]["capabilities"][
            "coolable"
        ] = False
        mutations.append(changed_fact)

        changed_receipt = copy.deepcopy(value)
        changed_receipt["target_receipts"][0]["inventory_index"] = 646
        mutations.append(changed_receipt)

        for mutation in mutations:
            with self.assertRaises((ValueError, SystemExit)):
                generator.validate_oracle(mutation)

    @unittest.skipUnless(
        DEPENDENCY_ROOT.is_dir()
        and all(
            (
                PINNED_SOURCE_ROOT
                / Path(source["path"]).relative_to("src")
            ).is_file()
            for source in generator.BASE.SOURCE_SPECS
        ),
        "pinned CPython reference environment unavailable",
    )
    def test_bootstrap_regenerates_twice_byte_identically_and_matches_fixture(self) -> None:
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
                    str(BOOTSTRAP_PATH),
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
        expected = FIXTURE_PATH.read_bytes()
        self.assertEqual(expected, outputs[0].read_bytes())
        self.assertEqual(expected, outputs[1].read_bytes())
        self.assertEqual(outputs[0].read_bytes(), outputs[1].read_bytes())


if __name__ == "__main__":
    unittest.main()

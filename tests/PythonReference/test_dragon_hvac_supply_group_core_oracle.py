"""Fail-closed tests for the bounded SupplyGroup core oracle."""

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
DEPENDENCY_ROOT = REPOSITORY_ROOT / ".tools" / "python-reference" / "3.12.7" / "site-packages"
if DEPENDENCY_ROOT.is_dir():
    sys.path.insert(0, str(DEPENDENCY_ROOT))
GENERATOR_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "generate_dragon_hvac_supply_group_core_oracle.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = REPOSITORY_ROOT / "fixtures" / "reference" / "python-0.7.0" / "dragon-hvac-supply-group-core-oracle.json"
PINNED_SOURCE_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"

spec = importlib.util.spec_from_file_location("generate_dragon_hvac_supply_group_core_oracle", GENERATOR_PATH)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load SupplyGroup core generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 29_865
EXPECTED_FIXTURE_SHA256 = "sha256:ac99f78ee10ab3c3c4e39a99059854b49f31ffbf823509764af970564ffba363"
EXPECTED_CASES_SHA256 = "sha256:1204af9174f2853ef303868d072974c1d753bd2657e12f99fc753af83a7dd602"


class DragonHvacSupplyGroupCoreOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="dragon-hvac-supply-group-core-tests-")
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
        self.assertEqual(generator.strict_json_dumps(value, indent=2) + "\n", raw.decode("utf-8"))

    def test_inventory_binds_twelve_loaded_sources_and_six_exact_symbols(self) -> None:
        inventory = generator.load_exact_inventory(INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT)
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(generator._expected_symbol_descriptors(), inventory["symbols"])
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(6, len(inventory["symbols"]))
        self.assertEqual(
            (
                "SupplyGroup.__init__",
                "SupplyGroup.coolable",
                "SupplyGroup.cooling_systems",
                "SupplyGroup.heatable",
                "SupplyGroup.heating_systems",
                "SupplyGroup.sources",
            ),
            generator.TARGET_SYMBOLS,
        )
        self.assertNotIn("SupplyGroup", generator.TARGET_SYMBOLS)
        self.assertNotIn("SupplyGroup.to_idf_object", generator.TARGET_SYMBOLS)

        loaded = self.fixture()["upstream"]["loaded_local_modules"]
        self.assertEqual(12, len(loaded))
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)
        self.assertEqual([item["path"] for item in inventory["files"]], [item["path"] for item in loaded])

    def test_cases_are_sorted_unique_and_exactly_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(18, len(identifiers))
        self.assertEqual(18, len(set(identifiers)))
        self.assertEqual(generator.EXPECTED_CASE_COUNTS, dict(Counter(item["symbol"] for item in definitions)))
        self.assertEqual({3}, set(generator.EXPECTED_CASE_COUNTS.values()))

        for definition in definitions:
            if definition["symbol"] in generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS:
                self.assertEqual(
                    {"adaptation": generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS[definition["symbol"]], "outcome": "returned"},
                    definition["expected_dotnet"],
                )
            else:
                self.assertNotIn("expected_dotnet", definition)

    def test_consumer_contract_pins_classifications_native_targets_and_boundaries(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {
                "SupplyGroup.__init__": "immutable-validated-supply-group-construction",
                "SupplyGroup.sources": "stable-entity-id-supply-source-deduplication",
            },
            contract["adaptations"],
        )
        self.assertEqual(
            {
                "SupplyGroup.__init__": "exception",
                "SupplyGroup.coolable": "equivalent",
                "SupplyGroup.cooling_systems": "equivalent",
                "SupplyGroup.heatable": "equivalent",
                "SupplyGroup.heating_systems": "equivalent",
                "SupplyGroup.sources": "exception",
            },
            contract["classifications"],
        )
        self.assertEqual(
            {
                "SupplyGroup.__init__": "SupplyGroup",
                "SupplyGroup.coolable": "SupplyGroup.CanCool",
                "SupplyGroup.cooling_systems": "SupplyGroup.CoolingSystems",
                "SupplyGroup.heatable": "SupplyGroup.CanHeat",
                "SupplyGroup.heating_systems": "SupplyGroup.HeatingSystems",
                "SupplyGroup.sources": "SupplyGroup.Sources",
            },
            contract["native_targets"],
        )
        self.assertEqual(
            {
                "full_symbol_closure": False,
                "scope": "bounded-supply-group-container-evidence",
                "unresolved_behavior": [
                    "SupplyGroup",
                    "SupplyGroup.to_idf_object",
                    "SupplySystem",
                    "concrete-supply-systems",
                    "supply-system-postprocessors",
                    "EnergyModel.to_idf",
                ],
            },
            contract["closure"],
        )
        self.assertEqual(list(generator.TARGET_SYMBOLS), contract["target_symbols"])

    def test_constructor_capability_projection_and_source_semantics_are_bounded(self) -> None:
        value = self.fixture()
        defaults = self.case(value, "dragon-hvac-supply-group-core.init.defaults-and-snapshot")["python"]["facts"]
        self.assertTrue(defaults["snapshot_isolated"])
        self.assertEqual([None, None, None], defaults["stored_availabilities"])
        self.assertEqual("KEYWORD_ONLY", defaults["availability_parameter_kind"])

        validation = self.case(value, "dragon-hvac-supply-group-core.init.validation-order")["python"]["facts"]
        self.assertEqual(["ValueError", "TypeError", "ValueError", "ValueError"], [item["type"] for item in validation["attempts"]])
        self.assertEqual(
            [
                "SupplyGroup requires at least one system.",
                "All systems must be SupplySystem instances.",
                "The number of availabilities must match the number of systems.",
                "Every supply system must support heating or cooling.",
            ],
            [item["message"] for item in validation["attempts"]],
        )

        cooling = self.case(value, "dragon-hvac-supply-group-core.cooling-systems.distinct-members-and-order")["python"]["facts"]
        self.assertEqual(["both-first", "cool-only", "both-second"], cooling["result_systems"])
        self.assertTrue(cooling["preserved_input_identity"])
        freshness = self.case(value, "dragon-hvac-supply-group-core.heating-systems.fresh-tuple")["python"]["facts"]
        self.assertFalse(freshness["same_result_object"])
        self.assertTrue(freshness["same_system_identity"])

        equal_sources = self.case(value, "dragon-hvac-supply-group-core.sources.distinct-equal-sources")["python"]["facts"]
        self.assertTrue(equal_sources["equal_by_value"])
        self.assertTrue(equal_sources["distinct_source_identity"])
        self.assertEqual(["source-a", "source-b"], equal_sources["result_sources"])

    @unittest.skipUnless(
        all((PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")).is_file() for source in generator.SOURCE_SPECS)
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_generation_is_byte_identical_twice_and_matches_fixture(self) -> None:
        bootstrap = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
        outputs = [self.temp_root / "first.json", self.temp_root / "second.json"]
        environment = os.environ.copy()
        environment.update({"PYTHONDONTWRITEBYTECODE": "1", "PYTHONHASHSEED": "0", "PYTHONUTF8": "1"})
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

    @unittest.skipUnless(PINNED_SOURCE_ROOT.is_dir() and DEPENDENCY_ROOT.is_dir(), "pinned reference environment unavailable")
    def test_loaded_local_module_without_receipt_fails_closed(self) -> None:
        with self.assertRaisesRegex(SystemExit, "lacks an exact receipt"):
            with generator.SUPPORT._pinned_modules(PINNED_SOURCE_ROOT) as modules:
                imported_root = Path(modules.hvac.__file__).resolve().parents[2]
                rogue = imported_root / "idragon" / "review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.review_probe"] = SimpleNamespace(__file__=str(rogue))

    def test_schema_contract_case_runtime_source_symbol_and_semantic_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        schema = self.fixture(); schema["schema"] = "wrong"; changes.append((schema, "schema"))
        contract = self.fixture(); contract["consumer_contract"]["closure"]["full_symbol_closure"] = True; changes.append((contract, "consumer contract"))
        case = self.fixture(); case["cases"][0]["executor"] = "wrong"; case["cases_sha256"] = generator.cases_sha256(case["cases"]); changes.append((case, "case contract"))
        runtime = self.fixture(); runtime["runtime"]["python_version"] = "3.12.8"; changes.append((runtime, "runtime"))
        source = self.fixture(); source["upstream"]["sources"][0]["source_sha256"] = "sha256:" + "0" * 64; changes.append((source, "upstream"))
        loaded = self.fixture(); loaded["upstream"]["loaded_local_modules"][0]["module"] = "idragon.wrong"; changes.append((loaded, "upstream"))
        symbol = self.fixture(); symbol["symbols"][0]["symbol_hash"] = "sha256:" + "0" * 64; changes.append((symbol, "symbol"))
        semantic = self.fixture(); semantic["cases"][0]["python"]["facts"]["result"] = False; semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"]); changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_raw_address_host_paths_and_nonfinite_fail(self) -> None:
        stale = self.fixture(); stale["cases"][0]["python"]["facts"]["result"] = False
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.load_json_without_duplicates(duplicate)

        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            ("C:\\private\\supply-group.json", "Absolute path"),
            ("/home/private/supply-group.json", "Absolute path"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture(); changed["cases"][0]["python"]["facts"]["unsafe"] = unsafe; changed["cases_sha256"] = generator.cases_sha256(changed["cases"])
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            self.assertFalse(math.isfinite(nonfinite))
            changed = self.fixture(); changed["cases"][0]["python"]["facts"]["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaisesRegex(ValueError, "Out of range float"):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

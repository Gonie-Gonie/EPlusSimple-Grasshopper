"""Fail-closed tests for the pinned Dragon AirBoundary core oracle."""

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
    / "generate_dragon_construction_air_boundary_core_oracle.py"
)
BOOTSTRAP_PATH = (
    REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-construction-air-boundary-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_construction_air_boundary_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 47_009
EXPECTED_GENERATOR_SHA256 = (
    "sha256:bb28f9e0a4e68684e4b7752fb127fc3be942d5c35eb3d1a9982a311bc26b4618"
)
EXPECTED_FIXTURE_BYTES = 97_758
EXPECTED_FIXTURE_SHA256 = (
    "sha256:16ad4d6d7a90e39a233d742d336d801e612c214360a5c1ac4c6853aec9f7ec03"
)
EXPECTED_CASES_SHA256 = (
    "sha256:996e6d45dbc2265ef078b6668fbcba423249a100329714031d64e09b3de30abc"
)


class DragonConstructionAirBoundaryCoreOracleTests(unittest.TestCase):
    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def facts(value: dict[str, object], scenario: str) -> dict[str, object]:
        matches = [
            case["python"]["facts"]
            for case in value["cases"]
            if case["python"]["facts"]["scenario"] == scenario
        ]
        if len(matches) != 1:
            raise AssertionError(f"Expected one AirBoundary scenario {scenario}.")
        return matches[0]

    @staticmethod
    def regenerate(output: Path) -> None:
        environment = os.environ.copy()
        environment["PYTHONHASHSEED"] = "0"
        environment["PYTHONDONTWRITEBYTECODE"] = "1"
        subprocess.run(
            [
                sys.executable,
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
        self.assertEqual(4, len(value["cases"]))
        self.assertEqual(4, len(value["fact_sha256"]))
        self.assertEqual(4, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_byte_identical_regeneration_is_repeatable(self) -> None:
        with tempfile.TemporaryDirectory(prefix="air-boundary-oracle-") as temporary:
            first = Path(temporary) / "first.json"
            second = Path(temporary) / "second.json"
            self.regenerate(first)
            self.regenerate(second)
            self.assertEqual(FIXTURE_PATH.read_bytes(), first.read_bytes())
            self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_inventory_binds_two_targets_and_every_adjacent_exclusion(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(
            [588, 589],
            [item["inventory_index"] for item in inventory["target_receipts"]],
        )
        exclusions = inventory["adjacent_exclusions"]
        self.assertEqual(list(range(590, 641)), [item["inventory_index"] for item in exclusions])
        self.assertEqual(
            list(generator.ADJACENT_EXCLUSION_IDENTITIES),
            [(item["inventory_index"], item["symbol"]) for item in exclusions],
        )
        value = self.fixture()
        self.assertEqual(inventory["target_receipts"], value["target_receipts"])
        self.assertEqual(exclusions, value["upstream"]["adjacent_exclusions"])
        self.assertEqual(11_652, value["upstream"]["construction_source"]["bytes"])
        self.assertEqual(
            generator.EXPECTED_SOURCE_SHA256,
            value["upstream"]["construction_source"]["source_sha256"],
        )

    def test_scope_is_exactly_two_exception_targets_without_idf_closure(self) -> None:
        value = self.fixture()
        target_counts = Counter(
            symbol
            for case in value["cases"]
            for symbol in case["target_symbols"]
        )
        context_symbols = {
            symbol
            for case in value["cases"]
            for symbol in case["context_symbols"]
        }
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(target_counts))
        self.assertEqual(set(), context_symbols)
        self.assertFalse(set(generator.EXCLUDED_SYMBOLS).intersection(target_counts))
        self.assertEqual(Counter({"AirBoundary": 4, "AirBoundary.__init__": 4}), target_counts)
        contract = value["consumer_contract"]
        self.assertEqual({"exception"}, set(contract["classifications"].values()))
        self.assertEqual(2, len(set(contract["assertion_ids"].values())))
        self.assertEqual(2, len(set(contract["adaptations"].values())))
        self.assertEqual(
            "proposed-not-yet-cross-language-verified",
            contract["native_binding_status"],
        )
        closure = contract["closure"]
        self.assertTrue(closure["target_coverage_complete"])
        self.assertFalse(closure["full_symbol_closure"])
        self.assertFalse(closure["full_construction_family_closure"])
        self.assertEqual(8, len(closure["unresolved_boundaries"]))
        self.assertFalse(contract["evidence_contract"]["full_idf_closure"])
        self.assertFalse(contract["evidence_contract"]["structural_only"])
        self.assertEqual(2, contract["evidence_contract"]["expected_receipt_count"])

    def test_ab01_pins_default_explicit_zero_and_runtime_types(self) -> None:
        facts = self.facts(self.fixture(), "AB01")
        states = [item["state"] for item in facts["observations"]["constructed_objects"]]
        self.assertEqual(["Default", "Explicit", "Zero"], [item["name"]["value"] for item in states])
        self.assertEqual(["float", "float", "int"], [item["ACH_type"] for item in states])
        self.assertEqual(
            [generator._encode(0.5), generator._encode(1.25), generator._encode(0)],
            [item["ACH"] for item in states],
        )
        self.assertEqual(
            ["construct-default", "construct-explicit", "construct-zero"],
            [item["phase"] for item in facts["timeline"]],
        )
        self.assertTrue(all(item["outcome"] == "returned" for item in facts["timeline"]))

    def test_ab02_pins_only_the_bounded_permissive_domain(self) -> None:
        facts = self.facts(self.fixture(), "AB02")
        probes = facts["observations"]["probes"]
        self.assertEqual(11, len(probes))
        self.assertEqual(
            [
                "null-name",
                "blank-name",
                "padded-name",
                "bool-name",
                "none-ach",
                "negative-ach",
                "nan-ach",
                "positive-infinity-ach",
                "negative-infinity-ach",
                "bool-ach",
                "string-ach",
            ],
            [item["label"] for item in probes],
        )
        self.assertTrue(all(item["outcome"] == "returned" for item in facts["timeline"]))
        self.assertEqual("NoneType", probes[0]["state"]["name_type"])
        self.assertEqual("bool", probes[3]["state"]["name_type"])
        self.assertEqual("NoneType", probes[4]["state"]["ACH_type"])
        self.assertEqual("bool", probes[9]["state"]["ACH_type"])
        self.assertEqual("str", probes[10]["state"]["ACH_type"])
        self.assertEqual(
            ["nan", "positive-infinity", "negative-infinity"],
            [probes[index]["state"]["ACH"]["value"] for index in (6, 7, 8)],
        )

    def test_ab03_pins_alias_visibility_and_direct_reassignment(self) -> None:
        facts = self.facts(self.fixture(), "AB03")
        snapshots = facts["source_state"]["snapshots"]
        self.assertEqual(
            [
                "initial",
                "after-source-mutation",
                "after-reassignment",
                "after-old-source-mutation",
                "after-replacement-mutation",
            ],
            [item["phase"] for item in snapshots],
        )
        self.assertTrue(snapshots[0]["object_name_is_name_source"])
        self.assertTrue(snapshots[0]["object_ach_is_ach_source"])
        self.assertEqual(2, len(snapshots[1]["object"]["name"]["items"]))
        self.assertEqual(generator._encode(2), snapshots[1]["object"]["ACH"]["items"][0]["value"])
        self.assertTrue(snapshots[2]["object_name_is_replacement"])
        self.assertTrue(snapshots[2]["object_ach_is_replacement"])
        final = facts["observations"]["final_object_state"]
        self.assertEqual("dict", final["name"]["kind"])
        self.assertEqual("list", final["ACH"]["kind"])
        self.assertEqual(2, len(final["ACH"]["items"]))

    def test_ab04_pins_python_call_binding_and_error_timing(self) -> None:
        facts = self.facts(self.fixture(), "AB04")
        errors = [item for item in facts["timeline"] if item["outcome"] == "raised"]
        self.assertEqual(
            [
                "missing-name",
                "too-many-positional",
                "unexpected-lowercase-ach-keyword",
            ],
            [item["phase"] for item in errors],
        )
        self.assertTrue(all(item["error"]["type"] == "TypeError" for item in errors))
        self.assertIn("missing 1 required positional argument", errors[0]["error"]["message"])
        self.assertIn("4 were given", errors[1]["error"]["message"])
        self.assertIn("unexpected keyword argument 'ach'", errors[2]["error"]["message"])
        successful = facts["observations"]["successful_call_states"]
        self.assertEqual(["name-keyword", "uppercase-ach-keyword"], [item["phase"] for item in successful])
        self.assertEqual(generator._encode(0.5), successful[0]["state"]["ACH"])
        self.assertEqual(generator._encode(1.5), successful[1]["state"]["ACH"])
        snapshots = facts["source_state"]["snapshots"]
        self.assertEqual(snapshots[0]["object"], snapshots[1]["object"])

    def test_contract_hash_and_exclusion_tampering_fail_closed(self) -> None:
        fixture = self.fixture()
        cases_tampered = copy.deepcopy(fixture)
        cases_tampered["cases"][0]["python"]["facts"]["scenario"] = "tampered"
        with self.assertRaisesRegex(RuntimeError, "hash|semantics"):
            generator.validate_oracle(cases_tampered)

        contract_tampered = copy.deepcopy(fixture)
        contract_tampered["consumer_contract"]["closure"]["full_symbol_closure"] = True
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(contract_tampered)

        exclusion_tampered = copy.deepcopy(fixture)
        exclusion_tampered["upstream"]["adjacent_exclusions"][0]["body_hash"] = (
            "sha256:" + ("0" * 64)
        )
        with self.assertRaisesRegex(RuntimeError, "exclusion receipts"):
            generator.validate_oracle(exclusion_tampered)

    def test_unsafe_or_noncanonical_trees_fail_closed(self) -> None:
        for value in (
            {"raw": float("nan")},
            {"address": "object at 0x123456789abcdef0"},
            {"path": "C:\\host\\secret"},
            {"guid": "12345678-1234-4234-9234-123456789abc"},
            {"timestamp": "2026-08-27T12:34:56"},
        ):
            with self.subTest(value=value):
                with self.assertRaises(RuntimeError):
                    generator._validate_safe_tree(value)

        with tempfile.TemporaryDirectory(prefix="air-boundary-json-") as temporary:
            duplicate = Path(temporary) / "duplicate.json"
            duplicate.write_text(
                '{"schema":"first","schema":"second"}\n',
                encoding="utf-8",
                newline="\n",
            )
            with self.assertRaises(SystemExit):
                generator.load_json_without_duplicates(duplicate)


if __name__ == "__main__":
    unittest.main()

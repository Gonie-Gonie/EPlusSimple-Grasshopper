"""Fail-closed tests for the pinned constants metadata/path oracle."""

from __future__ import annotations

from collections import Counter
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
    / "generate_constants_metadata_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "constants-metadata-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_constants_metadata_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load constants metadata generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 66_735
EXPECTED_GENERATOR_SHA256 = (
    "sha256:00fe4c741b9bc663ff985b609304dd3806ab7d05aa1d8af7555f05c0a76a22fd"
)
EXPECTED_FIXTURE_BYTES = 117_140
EXPECTED_FIXTURE_SHA256 = (
    "sha256:7a154b6147fe4dca6717c59f3005943d6ab44f9c3ce2dfe03255686481413810"
)
EXPECTED_CASES_SHA256 = (
    "sha256:e664bc6349a4965d94f50f5fcf31d544b5472163496a7117146fb3f9ce83a4e0"
)


class ConstantsMetadataOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="constants-metadata-test-")
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

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
            raise AssertionError(f"Expected one scenario {scenario}.")
        return matches[0]

    @classmethod
    def rehash(cls, value: dict[str, object]) -> None:
        value["fact_sha256"] = {
            case["id"]: generator.canonical_sha256(case["python"]["facts"])
            for case in value["cases"]
        }
        for case in value["cases"]:
            case["python"]["facts_sha256"] = value["fact_sha256"][case["id"]]
        value["case_sha256"] = generator.case_sha256(value["cases"])
        value["cases_sha256"] = generator.cases_sha256(value["cases"])

    @staticmethod
    def decode(value: dict[str, object]) -> object:
        kind = value["kind"]
        if kind == "none":
            return None
        if kind in {"bool", "str"}:
            return value["value"]
        if kind == "int":
            return int(value["value"])
        if kind == "tuple":
            return tuple(ConstantsMetadataOracleTests.decode(item) for item in value["items"])
        if kind == "list":
            return [ConstantsMetadataOracleTests.decode(item) for item in value["items"]]
        raise AssertionError(f"Unexpected encoded value: {value!r}")

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

    def test_inventory_exposes_exact_targets_and_resolved_exclusions(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(
            [568, 569, 570, 571, 572, 573, 574, 575],
            [item["inventory_index"] for item in inventory["target_receipts"]],
        )
        self.assertEqual(
            [576, 577, 578, 579],
            [item["inventory_index"] for item in inventory["resolved_receipts"]],
        )
        value = self.fixture()
        self.assertEqual(inventory["target_receipts"], value["target_receipts"])
        self.assertEqual(inventory["resolved_receipts"], value["resolved_receipts"])
        self.assertEqual(2_590, value["upstream"]["source"]["bytes"])
        self.assertEqual(
            generator.EXPECTED_SOURCE_SHA256,
            value["upstream"]["source"]["source_sha256"],
        )
        self.assertEqual(
            generator.ISOLATED_SOURCE_FILES,
            value["upstream"]["isolated_import"]["files_after_execution"],
        )
        self.assertEqual(
            {
                item: generator.EXPECTED_SOURCE_SHA256
                for item in generator.ISOLATED_SOURCE_FILES
            },
            value["upstream"]["isolated_import"]["source_copy_sha256"],
        )

    def test_scope_is_bounded_total_and_does_not_retarget_special_tag(self) -> None:
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
        self.assertTrue(context_symbols.issubset(set(generator.TARGET_SYMBOLS)))
        self.assertFalse(set(generator.RESOLVED_SYMBOLS).intersection(target_counts))
        self.assertEqual(
            Counter({"package": 6, "directory": 4}),
            Counter(case["subfamily"] for case in value["cases"]),
        )
        contract = value["consumer_contract"]
        targets = set(generator.TARGET_SYMBOLS)
        self.assertEqual(targets, set(contract["classifications"]))
        self.assertEqual(targets, set(contract["adaptations"]))
        self.assertEqual(targets, set(contract["assertion_ids"]))
        self.assertEqual(targets, set(contract["native_adaptation_candidates"]))
        self.assertEqual(targets, set(contract["runtime_contracts"]))
        self.assertEqual(8, len(set(contract["assertion_ids"].values())))
        self.assertEqual({"exception"}, set(contract["classifications"].values()))
        self.assertEqual(
            "proposed-not-yet-cross-language-verified",
            contract["native_binding_status"],
        )
        closure = contract["closure"]
        self.assertTrue(closure["target_coverage_complete"])
        self.assertFalse(closure["full_symbol_closure"])
        self.assertEqual(8, len(closure["unresolved_boundaries"]))
        self.assertEqual(
            list(generator.RESOLVED_RECEIPTS),
            closure["resolved_receipts_not_retargeted"],
        )

    def test_c01_pins_class_path_topology_without_absolute_values(self) -> None:
        facts = self.facts(self.fixture(), "C01")
        observations = facts["observations"]
        self.assertEqual("Directory", observations["class_name"])
        self.assertEqual(["object"], observations["base_names"])
        self.assertEqual("()", observations["signature"])
        self.assertEqual(
            ["IDD_DIR", "PROFILE_DIR", "ENERGYPLUS_DIR"],
            observations["public_member_names"],
        )
        paths = observations["target_values"]
        self.assertEqual(["_data", "idd"], paths["IDD_DIR"]["relative_parts"])
        self.assertEqual(["_data", "profile"], paths["PROFILE_DIR"]["relative_parts"])
        self.assertEqual(["runtime"], paths["ENERGYPLUS_DIR"]["relative_parts"])
        self.assertTrue(all(item["is_absolute"] for item in paths.values()))
        self.assertTrue(all(not item["exists"] for item in paths.values()))
        anchors = observations["anchor_state"]
        self.assertEqual(
            ["repository", "src", "idragon"],
            anchors["_MODULE_ROOT"]["relative_to_isolated_location"],
        )
        self.assertEqual(
            ["repository"],
            anchors["_PACKAGE_ROOT"]["relative_to_isolated_location"],
        )
        self.assertEqual(
            ["repository", "src", "idragon", "_data"],
            anchors["_DATA_DIR"]["relative_to_isolated_location"],
        )
        self.assertEqual(
            facts["source_state"]["snapshots"][0]["state"],
            facts["source_state"]["snapshots"][1]["state"],
        )

    def test_c02_pins_two_location_recomputation_and_relative_suffixes(self) -> None:
        observations = self.facts(self.fixture(), "C02")["observations"]
        self.assertFalse(observations["directory_class_identity"])
        self.assertEqual(["Directory", "Directory"], observations["directory_class_names"])
        expected = {
            "IDD_DIR": ["_data", "idd"],
            "PROFILE_DIR": ["_data", "profile"],
            "ENERGYPLUS_DIR": ["runtime"],
        }
        for name, pair in observations["path_pairs"].items():
            self.assertFalse(pair["location_a_equals_location_b"])
            self.assertFalse(pair["location_a_is_location_b"])
            self.assertEqual(expected[name], pair["location_a"]["relative_parts"])
            self.assertEqual(expected[name], pair["location_b"]["relative_parts"])

    def test_c03_and_c04_pin_directory_mutation_deletion_restore_and_instances(self) -> None:
        value = self.fixture()
        c03 = self.facts(value, "C03")
        self.assertEqual(
            [
                "read-missing-IDD_DIR",
                "read-missing-PROFILE_DIR",
                "read-missing-ENERGYPLUS_DIR",
            ],
            [event["phase"] for event in c03["events"] if event["outcome"] == "raised"],
        )
        self.assertTrue(all(c03["observations"]["restored_object_identity"].values()))
        snapshots = c03["source_state"]["snapshots"]
        self.assertEqual(snapshots[0]["state"], snapshots[-1]["state"])
        for name in generator.PUBLIC_DIRECTORY_ATTRIBUTES:
            assigned = next(item for item in snapshots if item["phase"] == f"after-assign-{name}")
            deleted = next(item for item in snapshots if item["phase"] == f"after-delete-{name}")
            self.assertEqual(
                ["relative-probe", name.lower()],
                assigned["state"][name]["value"]["parts"],
            )
            self.assertFalse(deleted["state"][name]["present"])

        c04 = self.facts(value, "C04")
        self.assertFalse(c04["observations"]["first_is_second"])
        self.assertEqual({}, c04["observations"]["first_instance_dictionary_before"])
        self.assertEqual({}, c04["observations"]["first_instance_dictionary_after"])
        for item in c04["observations"]["inherited_attributes"].values():
            self.assertTrue(item["before_is_class_value"])
            self.assertFalse(item["owned_before"])
            self.assertTrue(item["owned_after_assignment"])
            self.assertFalse(item["owned_after_delete"])
            self.assertTrue(item["after_delete_is_class_value"])
        errors = [event["error"] for event in c04["events"] if event["outcome"] == "raised"]
        self.assertEqual(
            [{"message": "Directory() takes no arguments", "type": "TypeError"}] * 2,
            errors,
        )

    def test_c05_through_c07_pin_package_values_mutability_and_instances(self) -> None:
        value = self.fixture()
        c05 = self.facts(value, "C05")
        observations = c05["observations"]
        self.assertEqual("PackageInfo", observations["class_name"])
        self.assertEqual(["object"], observations["base_names"])
        self.assertEqual(["NAME", "VERSION", "REQUIRED_PYTHON"], observations["public_member_names"])
        self.assertEqual("invisible-dragon", self.decode(observations["target_values"]["NAME"]))
        self.assertEqual((0, 7, 0), self.decode(observations["target_values"]["VERSION"]))
        self.assertEqual((3, 12), self.decode(observations["target_values"]["REQUIRED_PYTHON"]))
        self.assertTrue(all(observations["repeated_read_identity"].values()))

        c06 = self.facts(value, "C06")
        snapshots = c06["source_state"]["snapshots"]
        self.assertEqual(snapshots[0]["state"], snapshots[-1]["state"])
        expected_replacements = {"NAME": None, "VERSION": "0.7.0", "REQUIRED_PYTHON": [3, 12]}
        for name, expected in expected_replacements.items():
            assigned = next(item for item in snapshots if item["phase"] == f"after-assign-{name}")
            deleted = next(item for item in snapshots if item["phase"] == f"after-delete-{name}")
            self.assertEqual(expected, self.decode(assigned["state"][name]["value"]))
            self.assertFalse(deleted["state"][name]["present"])
        self.assertTrue(all(c06["observations"]["restored_object_identity"].values()))

        c07 = self.facts(value, "C07")
        self.assertFalse(c07["observations"]["first_is_second"])
        self.assertEqual({}, c07["observations"]["first_instance_dictionary_before"])
        self.assertEqual({}, c07["observations"]["first_instance_dictionary_after"])
        for item in c07["observations"]["inherited_attributes"].values():
            self.assertTrue(item["before_is_class_value"])
            self.assertFalse(item["owned_before"])
            self.assertTrue(item["owned_after_assignment"])
            self.assertFalse(item["owned_after_delete"])
            self.assertTrue(item["after_delete_is_class_value"])
        errors = [event["error"] for event in c07["events"] if event["outcome"] == "raised"]
        self.assertEqual(
            [{"message": "PackageInfo() takes no arguments", "type": "TypeError"}] * 2,
            errors,
        )

    def test_c08_pins_name_string_operations_and_immutable_item_error(self) -> None:
        facts = self.facts(self.fixture(), "C08")
        results = {name: self.decode(value) for name, value in facts["observations"]["operation_results"].items()}
        self.assertEqual(16, results["length"])
        self.assertEqual(["invisible", "dragon"], results["split-hyphen"])
        self.assertEqual("INVISIBLE-DRAGON", results["upper"])
        self.assertEqual("##invisible-dragon0007##", results["placeholder-index-7"])
        self.assertEqual("invisible-dragon-", results["temporary-prefix"])
        self.assertEqual("invisible", results["slice-first-nine"])
        self.assertEqual(
            {"message": "'str' object does not support item assignment", "type": "TypeError"},
            facts["events"][-1]["error"],
        )
        self.assertEqual(
            facts["source_state"]["snapshots"][0]["state"],
            facts["source_state"]["snapshots"][1]["state"],
        )

    def test_c09_pins_version_tuple_order_comparison_join_and_error(self) -> None:
        facts = self.facts(self.fixture(), "C09")
        results = {name: self.decode(value) for name, value in facts["observations"]["operation_results"].items()}
        self.assertEqual(3, results["length"])
        self.assertEqual(0, results["index-zero"])
        self.assertEqual(0, results["index-negative-one"])
        self.assertEqual((0, 7), results["slice-first-two"])
        self.assertEqual("0.7.0", results["display-join"])
        self.assertTrue(results["less-than-0.7.1"])
        self.assertTrue(results["greater-than-0.6.9"])
        self.assertFalse(results["equals-list"])
        self.assertEqual((0, 7, 0, 1), results["concatenate-patch"])
        self.assertEqual(
            {"message": "'tuple' object does not support item assignment", "type": "TypeError"},
            facts["events"][-1]["error"],
        )

    def test_c10_pins_required_python_comparisons_join_bug_and_error_order(self) -> None:
        facts = self.facts(self.fixture(), "C10")
        results = {name: self.decode(value) for name, value in facts["observations"]["operation_results"].items()}
        self.assertEqual((3, 12, 7), self.decode(facts["observations"]["pinned_runtime_version"]))
        self.assertFalse(results["pinned-runtime-less-than-required"])
        self.assertEqual("3,12", results["stringified-comma-join"])
        self.assertEqual((3, 12, 0), results["concatenate-patch"])
        self.assertTrue(results["probe-3.11-less-than-required"])
        self.assertFalse(results["probe-3.12-less-than-required"])
        self.assertFalse(results["probe-3.12.0-less-than-required"])
        self.assertFalse(results["probe-4.0-less-than-required"])
        errors = [event["error"] for event in facts["events"] if event["outcome"] == "raised"]
        self.assertEqual(
            [
                {"message": "sequence item 0: expected str instance, int found", "type": "TypeError"},
                {"message": "'tuple' object does not support item assignment", "type": "TypeError"},
            ],
            errors,
        )

    @unittest.skipUnless(
        (PINNED_SOURCE_ROOT / "idragon" / "constants.py").is_file()
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

    def test_tampered_semantics_contract_receipts_and_classification_fail_closed(self) -> None:
        semantic = self.fixture()
        self.facts(semantic, "C08")["observations"]["operation_results"]["length"] = generator._encode(15)
        self.rehash(semantic)

        case_contract = self.fixture()
        case_contract["cases"][0]["target_symbols"] = ["SpecialTag"]
        self.rehash(case_contract)

        receipt = self.fixture()
        receipt["target_receipts"][0]["inventory_index"] = 0

        classification = self.fixture()
        classification["consumer_contract"]["classifications"]["Directory"] = "equivalent"

        changes = (
            (semantic, "canonical semantics"),
            (case_contract, "case contract"),
            (receipt, "indexed target receipts"),
            (classification, "consumer contract"),
        )
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_paths_and_raw_floats_fail_closed(self) -> None:
        stale = self.fixture()
        self.facts(stale, "C02")["observations"]["directory_class_identity"] = True
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.load_json_without_duplicates(duplicate)

        unsafe_values = (
            ("C:\\private\\constants.json", "Absolute path"),
            ("/home/private/constants.json", "Absolute path"),
            ("0x123456789abcdef0", "address"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-27T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture()
            self.facts(changed, "C01")["unsafe"] = unsafe
            self.rehash(changed)
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        finite_float = self.fixture()
        self.facts(finite_float, "C01")["unsafe"] = 0.0
        self.rehash(finite_float)
        with self.assertRaisesRegex(RuntimeError, "Raw float"):
            generator.validate_oracle(finite_float)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            changed = self.fixture()
            self.facts(changed, "C01")["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaises(ValueError):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

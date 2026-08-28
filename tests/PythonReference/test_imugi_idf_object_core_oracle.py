"""Fail-closed tests for the Imugi IDF and IdfObject core oracle."""

from __future__ import annotations

from collections import Counter
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
DEPENDENCIES = ROOT / ".tools" / "python-reference" / "3.12.7" / "site-packages"
if DEPENDENCIES.is_dir():
    sys.path.insert(0, str(DEPENDENCIES))
GENERATOR = ROOT / "tools" / "python-reference" / "generate_imugi_idf_object_core_oracle.py"
BOOTSTRAP = ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY = ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE = ROOT / "fixtures" / "reference" / "python-0.7.0" / "imugi-idf-object-core-oracle.json"
SOURCE_ROOT = ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
TEMP_ROOT = ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location("generate_imugi_idf_object_core_oracle", GENERATOR)
if spec is None or spec.loader is None:
    raise RuntimeError("Cannot load Imugi IDF/IdfObject oracle generator.")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 30_113
EXPECTED_GENERATOR_SHA256 = "sha256:71b1b35644a6520b3c4ad467629cdc91d5a5003bf31a67f05978f9b78889dcab"
EXPECTED_FIXTURE_BYTES = 119_199
EXPECTED_FIXTURE_SHA256 = "sha256:e20c2330badd57b2e8851b010eb5a1bf5520854f6dcf9baa852e6dfd957eacf8"


class ImugiIdfObjectCoreOracleTests(unittest.TestCase):
    value: dict[str, object]

    @classmethod
    def setUpClass(cls) -> None:
        cls.value = generator.base.load_json_without_duplicates(FIXTURE)
        generator.validate_oracle(cls.value)

    @staticmethod
    def regenerate(path: Path) -> None:
        environment = os.environ.copy()
        environment["PYTHONHASHSEED"] = "0"
        environment["PYTHONUTF8"] = "1"
        environment["PYTHONDONTWRITEBYTECODE"] = "1"
        subprocess.run(
            [
                sys.executable,
                "-B",
                "-X",
                "utf8",
                str(BOOTSTRAP),
                "--dependency-root",
                str(DEPENDENCIES),
                "--upstream-source",
                str(SOURCE_ROOT),
                "--generator",
                str(GENERATOR),
                "--",
                "--inventory",
                str(INVENTORY),
                "--output",
                str(path),
                "--upstream-commit",
                generator.EXPECTED_UPSTREAM_COMMIT,
            ],
            cwd=ROOT,
            env=environment,
            check=True,
            capture_output=True,
            text=True,
        )

    def changed(self) -> dict[str, object]:
        return copy.deepcopy(self.value)

    def test_generator_fixture_and_all_frozen_hash_layers_are_exact(self) -> None:
        generator_bytes = GENERATOR.read_bytes()
        fixture_bytes = FIXTURE.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_bytes))
        self.assertEqual(EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR))
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_bytes))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE))
        self.assertEqual(generator.EXPECTED_FACT_SHA256, self.value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, self.value["case_sha256"])
        self.assertEqual(generator.EXPECTED_CASES_SHA256, self.value["cases_sha256"])
        self.assertTrue(fixture_bytes.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_bytes)
        self.assertEqual(
            generator.strict_json_dumps(self.value, indent=2) + "\n",
            fixture_bytes.decode("utf-8"),
        )

    def test_two_independent_regenerations_are_byte_identical(self) -> None:
        TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="imugi-idf-object-regeneration-", dir=TEMP_ROOT) as temporary:
            first = Path(temporary) / "first.json"
            second = Path(temporary) / "second.json"
            self.regenerate(first)
            self.regenerate(second)
            baseline = FIXTURE.read_bytes()
            self.assertEqual(baseline, first.read_bytes())
            self.assertEqual(first.read_bytes(), second.read_bytes())
            self.assertEqual(EXPECTED_FIXTURE_SHA256, "sha256:" + hashlib.sha256(baseline).hexdigest())

    def test_exact_40_21_25_19_28_partition_closes_all_133_declarations(self) -> None:
        inventory = generator.load_exact_inventory(INVENTORY, generator.EXPECTED_UPSTREAM_COMMIT)
        partitions = inventory["partitions"]
        self.assertEqual(
            {"batch1": 40, "batch2": 21, "target": 25, "batch4": 19, "out_of_scope": 28},
            {name: len(rows) for name, rows in partitions.items()},
        )
        seen: set[int] = set()
        for name, rows in partitions.items():
            indices = {item["inventory_index"] for item in rows}
            self.assertFalse(seen & indices, name)
            seen |= indices
            self.assertEqual(generator.EXPECTED_PARTITION_SHA256[name], generator.canonical_sha256(rows))
        self.assertEqual(set(range(1095, 1228)), seen)
        self.assertEqual(list(generator.TARGET_IDENTITIES), [(item["inventory_index"], item["symbol"]) for item in partitions["target"]])

    def test_cases_routes_and_classifications_cover_each_target_once(self) -> None:
        contract = self.value["consumer_contract"]
        symbols = [symbol for case in self.value["cases"] for symbol in case["target_symbols"]]
        self.assertEqual(Counter({symbol: 1 for symbol in generator.TARGET_SYMBOLS}), Counter(symbols))
        self.assertEqual({"equivalent": 6, "exception": 19}, contract["classification_counts"])
        self.assertEqual(Counter({"equivalent": 6, "exception": 19}), Counter(contract["classifications"].values()))
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(contract["native_routes"]))
        self.assertEqual(25, len(set(contract["assertion_ids"].values())))
        self.assertTrue(all(route.startswith("GonieGonie.") for route in contract["native_routes"].values()))

    def test_relocated_cpython_runtime_and_dependencies_are_fully_pinned(self) -> None:
        isolated = self.value["upstream"]["isolated_import"]
        self.assertEqual(2, isolated["source_location_count"])
        self.assertEqual("two-byte-identical-repository-temp-copies", isolated["relocated_source_copy"])
        self.assertEqual(generator.EXPECTED_LOADED_LOCAL_MODULES_SHA256, isolated["loaded_local_modules_sha256"])
        self.assertEqual(generator.EXPECTED_RELOCATED_OBSERVATIONS_SHA256, isolated["relocated_observations_sha256"])
        self.assertEqual(generator.EXPECTED_RUNTIME_SIGNATURES_SHA256, generator.canonical_sha256(self.value["consumer_contract"]["runtime_signatures"]))
        runtime = self.value["runtime"]
        self.assertEqual("cpython", runtime["implementation"])
        self.assertEqual("3.12.7", runtime["python_version"])
        self.assertEqual(generator.base.EXPECTED_DEPENDENCIES, runtime["dependencies"])

    def test_idf_and_idf_object_observations_are_behavioral_and_path_free(self) -> None:
        by_code = {case["code"]: case["python"]["facts"] for case in self.value["cases"]}
        self.assertEqual(0, by_code["A01"]["required_objects_created"])
        self.assertTrue(by_code["A01"]["default_filename_samples_differ"])
        self.assertFalse(by_code["A02"]["append_duplicate_ignored"])
        self.assertFalse(by_code["A02"]["render_stable"])
        self.assertEqual(2, by_code["A03"]["parsed_count"])
        self.assertFalse(by_code["A04"]["run_executed"])
        self.assertEqual({"kind": "str", "value": "Off"}, by_code["B01"]["normalized_mode"])
        self.assertEqual("raised", by_code["B02"]["static_invalid"]["outcome"])
        self.assertFalse(by_code["B03"]["has_parent"])
        self.assertTrue(by_code["B03"]["rename_without_referenceable_is_noop"])
        encoded = generator.strict_json_dumps(self.value)
        self.assertNotIn(str(ROOT), encoded)
        self.assertNotIn("AppData", encoded)

    def test_support_and_native_receipts_are_immutable_and_no_native_execution_is_claimed(self) -> None:
        review = self.value["native_review"]
        evidence = self.value["consumer_contract"]["evidence_contract"]
        self.assertFalse(review["python_executes_native_runtime"])
        self.assertTrue(review["no_python_api_or_source_compatibility_claim"])
        self.assertTrue(review["public_production_routes_only"])
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertFalse(evidence["native_runtime_executed_by_python_oracle"])
        self.assertFalse(evidence["python_api_or_source_compatibility_claim"])
        self.assertFalse(evidence["structural_only"])
        for receipt in (*review["sources"], *self.value["support"]):
            path = ROOT / receipt["path"]
            self.assertEqual(receipt["bytes"], path.stat().st_size)
            self.assertEqual(receipt["sha256"], generator.sha256_file(path))

    def test_fail_closed_mutations_are_rejected(self) -> None:
        mutations = []
        wrong_classification = self.changed()
        wrong_classification["consumer_contract"]["classification_counts"]["exception"] = 18
        mutations.append(wrong_classification)
        wrong_target = self.changed()
        wrong_target["target_receipts"][0]["symbol"] = "IDF.Drift"
        mutations.append(wrong_target)
        wrong_partition = self.changed()
        wrong_partition["partitions"]["target"][0]["inventory_index"] = 9999
        mutations.append(wrong_partition)
        wrong_facts = self.changed()
        wrong_facts["cases"][0]["python"]["facts"]["version"] = "drift"
        mutations.append(wrong_facts)
        wrong_relocation = self.changed()
        wrong_relocation["upstream"]["isolated_import"]["source_location_count"] = 1
        mutations.append(wrong_relocation)
        for value in mutations:
            with self.assertRaises(RuntimeError):
                generator.validate_oracle(value)

    def test_generator_rejects_wrong_commit_and_inventory_hash(self) -> None:
        with self.assertRaises(SystemExit):
            generator.load_exact_inventory(INVENTORY, "0" * 40)
        TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="imugi-idf-object-inventory-", dir=TEMP_ROOT) as temporary:
            changed = Path(temporary) / "inventory.json"
            changed.write_bytes(INVENTORY.read_bytes() + b"\n")
            with self.assertRaises(SystemExit):
                generator.load_exact_inventory(changed, generator.EXPECTED_UPSTREAM_COMMIT)


if __name__ == "__main__":
    unittest.main()

"""Strict tests for the final Imugi IdfObjectList reference oracle."""

from __future__ import annotations

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
GENERATOR = ROOT / "tools/python-reference/generate_imugi_idf_object_list_core_oracle.py"
FIXTURE = ROOT / "fixtures/reference/python-0.7.0/imugi-idf-object-list-core-oracle.json"
BOOTSTRAP = ROOT / "tools/python-reference/bootstrap_reference.py"
INVENTORY = ROOT / "upstream/public-symbol-inventory.json"
DEPENDENCIES = ROOT / ".tools/python-reference/3.12.7/site-packages"
UPSTREAM = ROOT / "temp/reference/upstream/eplussimple/src"
COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
GENERATOR_BYTES = 22838
GENERATOR_SHA256 = "cc504d32c9b6926093185f0bb7e4c988c4bfe9b27d035330768f5f8b980fa8c4"
FIXTURE_BYTES = 105236
FIXTURE_SHA256 = "6047f16dc92ae8b8e3e93daf43149ec0d8041ac15f748619e143d6efc0f7aaba"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_generator():
    spec = importlib.util.spec_from_file_location("imugi_idf_object_list_core_oracle", GENERATOR)
    if spec is None or spec.loader is None:
        raise RuntimeError("Cannot load generator.")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ImugiIdfObjectListCoreOracleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.generator = load_generator()
        cls.value = cls.generator.base.base.load_json_without_duplicates(FIXTURE)
        cls.generator.validate_oracle(cls.value)

    def test_01_artifacts_are_byte_pinned(self) -> None:
        self.assertEqual((GENERATOR_BYTES, GENERATOR_SHA256), (GENERATOR.stat().st_size, sha256(GENERATOR)))
        self.assertEqual((FIXTURE_BYTES, FIXTURE_SHA256), (FIXTURE.stat().st_size, sha256(FIXTURE)))

    def test_02_exact_authoritative_targets_and_cases(self) -> None:
        self.assertEqual(19, len(self.value["target_receipts"]))
        self.assertEqual(list(self.generator.TARGET_IDENTITIES), [(row["inventory_index"], row["symbol"]) for row in self.value["target_receipts"]])
        targets = [symbol for case in self.value["cases"] for symbol in case["target_symbols"]]
        self.assertEqual(19, len(targets))
        self.assertEqual(set(self.generator.TARGET_SYMBOLS), set(targets))
        self.assertEqual(5, len(self.value["cases"]))

    def test_03_full_133_partition_is_disjoint_and_closed(self) -> None:
        expected = {"batch1": 40, "batch2": 21, "batch3": 25, "out_of_scope": 28, "target": 19}
        self.assertEqual(expected, {name: len(rows) for name, rows in self.value["partitions"].items()})
        indices = [row["inventory_index"] for rows in self.value["partitions"].values() for row in rows]
        self.assertEqual(133, len(indices))
        self.assertEqual(133, len(set(indices)))
        self.assertEqual(list(self.generator.SOURCE_INDICES), sorted(indices))

    def test_04_classifications_and_public_routes_are_exact(self) -> None:
        contract = self.value["consumer_contract"]
        self.assertEqual({"equivalent": 4, "exception": 15}, contract["classification_counts"])
        self.assertEqual(self.generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual({symbol: self.generator._route(symbol) for symbol in self.generator.TARGET_SYMBOLS}, contract["native_routes"])
        self.assertTrue(self.value["native_review"]["public_production_routes_only"])

    def test_05_claims_are_conservative_and_receipts_unique(self) -> None:
        evidence = self.value["consumer_contract"]["evidence_contract"]
        self.assertFalse(evidence["active_energyplus_process_claim"])
        self.assertFalse(evidence["internal_native_route_claim"])
        self.assertFalse(evidence["python_api_or_source_compatibility_claim"])
        self.assertFalse(evidence["structural_only"])
        ids = list(self.value["consumer_contract"]["assertion_ids"].values())
        self.assertEqual(19, len(ids))
        self.assertEqual(19, len(set(ids)))

    def test_06_hash_layers_runtime_relocation_and_support_are_frozen(self) -> None:
        self.generator.validate_oracle(self.value)
        self.assertEqual(self.generator.EXPECTED_FACT_SHA256, self.value["fact_sha256"])
        self.assertEqual(self.generator.EXPECTED_CASE_SHA256, self.value["case_sha256"])
        self.assertEqual(self.generator.EXPECTED_CASES_SHA256, self.value["cases_sha256"])
        isolated = self.value["upstream"]["isolated_import"]
        self.assertEqual(2, isolated["source_location_count"])
        self.assertEqual(self.generator.EXPECTED_RELOCATED_OBSERVATIONS_SHA256, isolated["relocated_observations_sha256"])
        self.assertEqual(self.generator.base.base._runtime_receipt(), self.value["runtime"])
        self.assertEqual(list(self.generator.SUPPORT), [{key: row[key] for key in ("path", "bytes", "sha256")} for row in self.value["support"]])

    def test_07_two_independent_regenerations_are_byte_identical(self) -> None:
        outputs = []
        env = os.environ.copy()
        env.update({"PYTHONHASHSEED": "0", "PYTHONUTF8": "1", "PYTHONDONTWRITEBYTECODE": "1"})
        with tempfile.TemporaryDirectory(dir=ROOT / "temp") as directory:
            for name in ("one.json", "two.json"):
                output = Path(directory) / name
                command = [sys.executable, "-B", "-X", "utf8", str(BOOTSTRAP), "--dependency-root", str(DEPENDENCIES), "--upstream-source", str(UPSTREAM), "--generator", str(GENERATOR), "--", "--inventory", str(INVENTORY), "--output", str(output), "--upstream-commit", COMMIT]
                subprocess.run(command, cwd=ROOT, env=env, check=True, capture_output=True, text=True)
                outputs.append(output.read_bytes())
        self.assertEqual(outputs[0], outputs[1])
        self.assertEqual(FIXTURE.read_bytes(), outputs[0])

    def test_08_validator_fails_closed_on_mutation(self) -> None:
        mutations = []
        changed = copy.deepcopy(self.value)
        changed["consumer_contract"]["evidence_contract"]["structural_only"] = True
        mutations.append(changed)
        changed = copy.deepcopy(self.value)
        changed["target_receipts"][0]["symbol"] = "IdfObjectList.not_authoritative"
        mutations.append(changed)
        changed = copy.deepcopy(self.value)
        changed["cases"][0]["python"]["facts"]["valid"] = "tampered"
        mutations.append(changed)
        for mutation in mutations:
            with self.assertRaises((RuntimeError, ValueError, KeyError)):
                self.generator.validate_oracle(mutation)

    def test_09_wrong_commit_and_inventory_are_rejected(self) -> None:
        inventory = self.generator.load_exact_inventory(INVENTORY, COMMIT)
        with self.assertRaises(SystemExit):
            self.generator.build_oracle(inventory, "0" * 40)
        with tempfile.TemporaryDirectory(dir=ROOT / "temp") as directory:
            altered = json.loads(INVENTORY.read_text(encoding="utf-8"))
            altered["symbols"][0]["symbol"] = "tampered"
            path = Path(directory) / "inventory.json"
            path.write_text(json.dumps(altered), encoding="utf-8")
            with self.assertRaises((SystemExit, RuntimeError)):
                self.generator.load_exact_inventory(path, COMMIT)


if __name__ == "__main__":
    unittest.main()

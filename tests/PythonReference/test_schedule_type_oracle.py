"""Fail-closed tests for the ScheduleType reference generator."""

from __future__ import annotations

import importlib.util
import json
import math
from pathlib import Path
import shutil
import unittest
import uuid


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
GENERATOR_PATH = (
    REPOSITORY_ROOT
    / "tools"
    / "python-reference"
    / "generate_schedule_type_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"

spec = importlib.util.spec_from_file_location("schedule_type_oracle_generator", GENERATOR_PATH)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class ScheduleTypeOracleGeneratorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        root = REPOSITORY_ROOT / "temp" / "python-reference-tests"
        root.mkdir(parents=True, exist_ok=True)
        cls.temp_root = root / str(uuid.uuid4())
        cls.temp_root.mkdir()

    @classmethod
    def tearDownClass(cls) -> None:
        shutil.rmtree(cls.temp_root)

    def write_inventory(self, name: str, value: object) -> Path:
        path = self.temp_root / name
        path.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        return path

    def test_exact_inventory_binds_all_twelve_symbols_and_profile_source(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH,
            generator.EXPECTED_UPSTREAM_COMMIT,
        )

        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"])
        self.assertEqual(
            list(generator.TARGET_SYMBOLS),
            [item["symbol"] for item in inventory["symbols"]],
        )
        self.assertEqual(
            generator.EXPECTED_SYMBOL_HASHES,
            {
                item["symbol"]: item["symbol_hash"]
                for item in inventory["symbols"]
            },
        )

    def test_validation_case_matrix_is_complete_unique_and_strict_json(self) -> None:
        generator.validate_case_specs()

        self.assertEqual(
            {"temperature": 13, "onoff": 11, "fraction": 11, "real": 9},
            {
                name: len(cases)
                for name, cases in generator.VALIDATION_CASE_SPECS.items()
            },
        )
        for cases in generator.VALIDATION_CASE_SPECS.values():
            ids = [case["id"] for case in cases]
            self.assertEqual(len(ids), len(set(ids)))

    def test_boundary_specs_use_adjacent_finite_floats(self) -> None:
        cases = {
            item["id"]: item["input"]
            for item in generator.VALIDATION_CASE_SPECS["temperature"]
        }

        self.assertEqual(math.nextafter(-50.0, -math.inf), cases["just-below-lower"]["value"])
        self.assertEqual(math.nextafter(-50.0, math.inf), cases["just-above-lower"]["value"])
        self.assertEqual(math.nextafter(200.0, -math.inf), cases["just-below-upper"]["value"])
        self.assertEqual(math.nextafter(200.0, math.inf), cases["just-above-upper"]["value"])
        self.assertTrue(all(math.isfinite(item["value"]) for item in cases.values() if item["kind"] == "number"))

    def test_nonfinite_results_are_tagged_without_nonstandard_json_tokens(self) -> None:
        expected = {
            math.inf: "positive-infinity",
            -math.inf: "negative-infinity",
        }
        for value, token in expected.items():
            with self.subTest(value=value):
                tagged = generator.tagged_output_value(value)
                self.assertEqual({"kind": "nonfinite", "value": token}, tagged)
                json.loads(generator.strict_json_dumps(tagged))

        nan_tagged = generator.tagged_output_value(math.nan)
        self.assertEqual({"kind": "nonfinite", "value": "nan"}, nan_tagged)
        json.loads(generator.strict_json_dumps(nan_tagged))

    def test_strict_serializer_rejects_raw_nonfinite_values(self) -> None:
        for value in (math.nan, math.inf, -math.inf):
            with self.subTest(value=value):
                with self.assertRaisesRegex(ValueError, "Out of range float values"):
                    generator.strict_json_dumps({"value": value})

    def test_duplicate_json_object_key_is_rejected(self) -> None:
        path = self.temp_root / "duplicate.json"
        path.write_text(
            '{"schema":"first","schema":"second"}\n',
            encoding="utf-8",
            newline="\n",
        )

        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_nonfinite_inventory_json_constants_are_rejected(self) -> None:
        for index, value in enumerate(("NaN", "Infinity", "-Infinity")):
            with self.subTest(value=value):
                path = self.temp_root / f"nonfinite-{index}.json"
                path.write_text(
                    '{"schema":' + value + "}\n",
                    encoding="utf-8",
                    newline="\n",
                )
                with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                    generator.load_exact_inventory(
                        path,
                        generator.EXPECTED_UPSTREAM_COMMIT,
                    )

    def test_tampered_inventory_content_hash_is_rejected(self) -> None:
        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        value["content_sha256"] = "sha256:" + ("0" * 64)
        path = self.write_inventory("tampered-content-hash.json", value)

        with self.assertRaisesRegex(SystemExit, "content hash is invalid"):
            generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_tampered_profile_source_is_rejected_even_when_resealed(self) -> None:
        value = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        source = next(
            item for item in value["files"] if item["path"] == generator.SOURCE_PATH
        )
        source["content_hash"] = "sha256:" + ("0" * 64)
        value["content_sha256"] = generator.canonical_sha256(
            {
                "files": value["files"],
                "scope_sha256": value["scope_sha256"],
                "symbols": value["symbols"],
                "upstream_commit": value["upstream_commit"],
            }
        )
        path = self.write_inventory("tampered-source-hash.json", value)

        with self.assertRaisesRegex(SystemExit, "not the exact pinned inventory"):
            generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)

    def test_wrong_commit_is_rejected_before_inventory_use(self) -> None:
        with self.assertRaisesRegex(SystemExit, "not the pinned ScheduleType commit"):
            generator.load_exact_inventory(INVENTORY_PATH, "0" * 40)


if __name__ == "__main__":
    unittest.main()

"""Fail-closed tests for the dragon-model projections reference oracle."""

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
DEPENDENCY_ROOT = REPOSITORY_ROOT / ".tools" / "python-reference" / "3.12.7" / "site-packages"
if DEPENDENCY_ROOT.is_dir():
    sys.path.insert(0, str(DEPENDENCY_ROOT))
GENERATOR_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "generate_dragon_model_projections_oracle.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = REPOSITORY_ROOT / "fixtures" / "reference" / "python-0.7.0" / "dragon-model-projections-oracle.json"
PINNED_SOURCE_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"

spec = importlib.util.spec_from_file_location("generate_dragon_model_projections_oracle", GENERATOR_PATH)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load projections generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 22_156
EXPECTED_FIXTURE_SHA256 = "sha256:cee03dd2df0ef704dd77145e94223cc1a74a852ef3d822307749fe368b489117"
EXPECTED_CASES_SHA256 = "sha256:b8ec10dcd0e44e8c46584cd241489b58fb4562d99bb790aff5202a6350b0a784"


class DragonModelProjectionsOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="dragon-model-projections-tests-")
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

    def test_fixture_is_exact_strict_and_self_validating(self) -> None:
        value = self.fixture()
        raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertTrue(raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", raw)
        self.assertEqual(generator.strict_json_dumps(value, indent=2) + "\n", raw.decode("utf-8"))

    def test_inventory_binds_four_exact_sources_and_four_symbols(self) -> None:
        inventory = generator.load_exact_inventory(INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT)
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(generator._expected_symbol_descriptors(), inventory["symbols"])
        self.assertEqual(
            [
                "src/idragon/dragon/construction.py",
                "src/idragon/dragon/model.py",
                "src/idragon/dragon/profile.py",
                "src/idragon/dragon/shape.py",
            ],
            [item["path"] for item in inventory["files"]],
        )

    def test_cases_are_sorted_unique_and_exactly_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(12, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS},
            dict(Counter(item["symbol"] for item in definitions)),
        )

    def test_consumer_contract_has_two_equivalents_and_two_adaptations(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {
                "EnergyModel.used_constructions": "deterministic-used-construction-projection",
                "EnergyModel.used_layers": "deterministic-used-layer-projection",
            },
            contract["adaptations"],
        )
        self.assertEqual(generator.EXPECTED_ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(
            {
                "EnergyModel.surfaces": "equivalent",
                "EnergyModel.used_constructions": "exception",
                "EnergyModel.used_layers": "exception",
                "EnergyModel.used_profiles": "equivalent",
            },
            contract["classifications"],
        )
        for case in self.fixture()["cases"]:
            if case["symbol"] in generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS:
                self.assertEqual(
                    generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS[case["symbol"]],
                    case["expected_dotnet"]["adaptation"],
                )
            else:
                self.assertNotIn("expected_dotnet", case)

    def test_fixture_pins_flatten_set_order_hash_and_profile_replacement(self) -> None:
        value = self.fixture()
        flattened = self.case(value, "dragon-model-projections.surfaces.flatten-order-identity")["python"]["facts"]
        self.assertEqual([0, 1, 1, 2, 0], flattened["output_surface_indices"])
        self.assertTrue(flattened["selected_objects_are_registry_objects"])

        constructions = self.case(value, "dragon-model-projections.used-constructions.hash-order-resize")["python"]["facts"]
        self.assertEqual([5, 4, 1, 6, 3, 0, 7, 8, 2], constructions["output_registry_indices"])
        self.assertEqual(list(generator.ORDER_HASHES), [item["hash_decimal"] for item in constructions["construction_registry"]])

        filtered = self.case(value, "dragon-model-projections.used-constructions.empty-filtered")["python"]["facts"]
        self.assertEqual(
            ["air-boundary", "no-mass", "air-boundary", "air-boundary"],
            filtered["input_kinds"],
        )
        self.assertEqual([], filtered["output_registry_indices"])

        layers = self.case(value, "dragon-model-projections.used-layers.hash-equality-mismatch")["python"]["facts"]
        self.assertEqual([2, 1, 0], layers["output_layer_indices"])
        self.assertTrue(layers["equality"]["base_equals_equal_different_name"])
        self.assertFalse(layers["equality"]["base_equals_same_name_different_thickness"])

        empty_layers = self.case(value, "dragon-model-projections.used-layers.empty-fresh")["python"]["facts"]
        self.assertEqual(
            ["air-boundary", "no-mass", "air-boundary"],
            empty_layers["construction_input_kinds"],
        )
        self.assertEqual([], empty_layers["output_layer_indices"])

        profiles = self.case(value, "dragon-model-projections.used-profiles.duplicate-name-last-wins")["python"]["facts"]
        self.assertEqual(["Team", "Aux", "Core"], profiles["first_seen_name_order"])
        self.assertEqual([2, 4, 3], profiles["output_profile_indices"])
        self.assertEqual(["team-last", "aux-last", "core-only"], profiles["output_labels"])

    @unittest.skipUnless(
        all((PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")).is_file() for source in generator.SOURCE_SPECS)
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

    def test_root_contract_runtime_source_symbol_and_semantic_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        root = self.fixture(); root["unexpected"] = True; changes.append((root, "root"))
        case = self.fixture(); case["cases"][0]["executor"] = "wrong"; case["cases_sha256"] = generator.cases_sha256(case["cases"]); changes.append((case, "contract"))
        contract = self.fixture(); contract["consumer_contract"]["native_order"] = "wrong"; changes.append((contract, "consumer contract"))
        runtime = self.fixture(); runtime["runtime"]["python_hash_seed"] = 1; changes.append((runtime, "runtime"))
        source = self.fixture(); source["upstream"]["sources"][0]["source_sha256"] = "sha256:" + "0" * 64; changes.append((source, "upstream"))
        symbol = self.fixture(); symbol["symbols"][0]["body_hash"] = "sha256:" + "0" * 64; changes.append((symbol, "symbol"))
        semantic = self.fixture(); semantic["cases"][5]["python"]["facts"]["output_registry_indices"] = list(range(9)); semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"]); changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_address_path_guid_timestamp_float_duplicate_and_hash_tampering_fail(self) -> None:
        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            ("C:\\private\\projection.json", "Absolute path"),
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

        floating = self.fixture(); floating["cases"][0]["python"]["facts"]["unsafe"] = 1.5; floating["cases_sha256"] = generator.cases_sha256(floating["cases"])
        with self.assertRaisesRegex(RuntimeError, "Raw float"):
            generator.validate_oracle(floating)

        stale_hash = self.fixture(); stale_hash["cases"][0]["python"]["facts"]["registry_labels"] = ["changed"]
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale_hash)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8")
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.SUPPORT.load_json_without_duplicates(duplicate)


if __name__ == "__main__":
    unittest.main()

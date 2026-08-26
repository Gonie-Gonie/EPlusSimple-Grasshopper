"""Fail-closed tests for the dragon-model conditioning reference oracle."""

from __future__ import annotations

from collections import Counter
import copy
import importlib.util
from pathlib import Path
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
    / "generate_dragon_model_conditioning_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-model-conditioning-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT
    / "temp"
    / "reference"
    / "upstream"
    / "eplussimple"
    / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_model_conditioning_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load conditioning generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_FIXTURE_BYTES = 19_851
EXPECTED_FIXTURE_SHA256 = (
    "sha256:7cbdcad0691b3e56010981217f11e515c6cb7f417b6a22643925876b33e6de81"
)
EXPECTED_CASES_SHA256 = (
    "sha256:96d15556dcde29a91582c66bc7c056c374619d8a50c7c17785ef0eeb241bdfca"
)


class DragonModelConditioningOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-model-conditioning-oracle-tests-"
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

    def test_fixture_is_exact_strict_and_self_validating(self) -> None:
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

    def test_inventory_binds_two_exact_sources_and_three_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(
            [
                {
                    "ast_hash": source["ast_sha256"],
                    "content_hash": source["source_sha256"],
                    "path": source["path"],
                }
                for source in generator.SOURCE_SPECS
            ],
            inventory["files"],
        )
        self.assertEqual(generator._expected_symbol_descriptors(), inventory["symbols"])

    def test_cases_are_sorted_unique_and_exactly_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(9, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS},
            dict(Counter(item["symbol"] for item in definitions)),
        )

    def test_two_equivalents_and_zone_context_adaptation_are_exact(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(
            {"Zone.is_conditioned": "model-context-zone-conditioning-predicate"},
            contract["adaptations"],
        )
        self.assertEqual(generator.EXPECTED_ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(
            {
                "EnergyModel.conditioned_zones": "equivalent",
                "EnergyModel.unconditioned_zones": "equivalent",
                "Zone.is_conditioned": "exception",
            },
            contract["classifications"],
        )
        for case in value["cases"]:
            if case["symbol"] == "Zone.is_conditioned":
                self.assertEqual(
                    "model-context-zone-conditioning-predicate",
                    case["expected_dotnet"]["adaptation"],
                )
            else:
                self.assertNotIn("expected_dotnet", case)

    def test_fixture_pins_falsey_presence_order_identity_and_complement(self) -> None:
        value = self.fixture()
        falsey = self.case(
            value,
            "dragon-model-conditioning.conditioned-zones.falsey-availability-order",
        )["python"]["facts"]
        self.assertEqual([0, 1, 2], falsey["selected_indices"])
        self.assertEqual(
            ["supply-zero", "supply-false", "supply-empty"],
            falsey["selected_labels"],
        )
        self.assertTrue(falsey["fresh_list_each_access"])
        self.assertTrue(falsey["selected_objects_are_input_objects"])
        self.assertTrue(falsey["source_list_unchanged"])

        conditioned = self.case(
            value,
            "dragon-model-conditioning.conditioned-zones.mixed-order-identity",
        )["python"]["facts"]
        unconditioned = self.case(
            value,
            "dragon-model-conditioning.unconditioned-zones.mixed-complement",
        )["python"]["facts"]
        self.assertEqual([1, 3], conditioned["selected_indices"])
        self.assertEqual([0, 2], unconditioned["selected_indices"])
        self.assertEqual(conditioned["input_states"], unconditioned["input_states"])

        required = self.case(
            value,
            "dragon-model-conditioning.zone-is-conditioned.profile-availability-required",
        )["python"]["facts"]["observations"]
        self.assertEqual([False, False, True], [item["zone_is_conditioned"] for item in required])
        self.assertTrue(required[1]["custom_supply_availability_present"])

    @unittest.skipUnless(
        (PINNED_SOURCE_ROOT / "idragon" / "dragon" / "model.py").is_file()
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_generation_is_byte_identical_twice_and_matches_fixture(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        first = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE_ROOT
        )
        second = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE_ROOT
        )
        first_bytes = (generator.strict_json_dumps(first, indent=2) + "\n").encode()
        second_bytes = (generator.strict_json_dumps(second, indent=2) + "\n").encode()
        self.assertEqual(first_bytes, second_bytes)
        self.assertEqual(FIXTURE_PATH.read_bytes(), first_bytes)

    def test_root_case_contract_runtime_source_and_symbol_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root"))

        case = self.fixture()
        case["cases"][0]["executor"] = "wrong"
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "contract"))

        equivalent = self.fixture()
        equivalent["cases"][0]["expected_dotnet"] = {
            "adaptation": "not-allowed",
            "outcome": "returned",
        }
        equivalent["cases_sha256"] = generator.cases_sha256(equivalent["cases"])
        changes.append((equivalent, "key set"))

        contract = self.fixture()
        contract["consumer_contract"]["identity_encoding"] = "wrong"
        changes.append((contract, "consumer contract"))

        runtime = self.fixture()
        runtime["runtime"]["dependencies"]["pandas"] = "2.3.1"
        changes.append((runtime, "runtime"))

        source = self.fixture()
        source["upstream"]["sources"][1]["ast_sha256"] = "sha256:" + ("0" * 64)
        changes.append((source, "upstream"))

        symbol = self.fixture()
        symbol["symbols"][2]["body_hash"] = "sha256:" + ("0" * 64)
        changes.append((symbol, "symbol"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_safe_semantic_tampering_fails_after_cases_hash_is_recomputed(self) -> None:
        changed = self.fixture()
        changed["cases"][1]["python"]["facts"]["selected_indices"] = [0, 2]
        changed["cases_sha256"] = generator.cases_sha256(changed["cases"])
        with self.assertRaisesRegex(RuntimeError, "semantics"):
            generator.validate_oracle(changed)

        extra = self.fixture()
        extra["cases"][8]["python"]["facts"]["observations"][0][
            "safe_extra"
        ] = "unexpected"
        extra["cases_sha256"] = generator.cases_sha256(extra["cases"])
        with self.assertRaisesRegex(RuntimeError, "semantics"):
            generator.validate_oracle(extra)

    def test_path_address_guid_timestamp_nonfinite_duplicate_and_hash_tampering_fail(self) -> None:
        fixtures = (
            (1.25, "Raw float"),
            (r"C:\raw\path", "Absolute path"),
            ("/tmp/conditioning-oracle/file", "Absolute path"),
            ("0xdeadbeef", "Raw address"),
            ("01234567-89ab-4cde-8fab-0123456789ab", "GUID-like"),
            ("2026-08-26T12:34:56Z", "Timestamp"),
        )
        for raw_value, message in fixtures:
            malformed = self.fixture()
            malformed["cases"][0]["python"]["facts"]["raw"] = raw_value
            malformed["cases_sha256"] = generator.cases_sha256(malformed["cases"])
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

        changed = self.fixture()
        changed_cases = copy.deepcopy(changed["cases"])
        changed_cases[0]["python"]["facts"]["selected_indices"] = [0]
        self.assertNotEqual(
            changed["cases_sha256"], generator.cases_sha256(changed_cases)
        )

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key 'schema'"):
            generator.load_exact_inventory(
                duplicate, generator.EXPECTED_UPSTREAM_COMMIT
            )

        for index, token in enumerate(("NaN", "Infinity", "-Infinity")):
            path = self.temp_root / f"nonfinite-{index}.json"
            path.write_text('{"schema":' + token + "}\n", encoding="utf-8")
            with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                generator.load_exact_inventory(path, generator.EXPECTED_UPSTREAM_COMMIT)


if __name__ == "__main__":
    unittest.main()

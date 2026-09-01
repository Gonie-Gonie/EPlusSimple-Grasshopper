"""Fail-closed tests for the dragon-model Terrain reference oracle."""

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
    / "generate_dragon_model_terrain_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-model-terrain-oracle.json"
)
PINNED_SOURCE = (
    REPOSITORY_ROOT
    / "temp"
    / "reference"
    / "upstream"
    / "eplussimple"
    / "src"
    / "idragon"
    / "dragon"
    / "model.py"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_model_terrain_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load Terrain generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


EXPECTED_FIXTURE_BYTES = 17_933
EXPECTED_FIXTURE_SHA256 = (
    "sha256:1e2820763758cf2f997f1f0524a7989c511cfcd05f337117bb47460ec4b6e44e"
)
EXPECTED_CASES_SHA256 = (
    "sha256:aea20222894cc0c5a500dfccb15f9955e56666f4de763fef62c297fe975d0a47"
)


class DragonModelTerrainOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-model-terrain-oracle-tests-"
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

    def test_inventory_binds_exact_source_ast_and_six_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, inventory["file"]["content_hash"])
        self.assertEqual(
            generator.EXPECTED_SOURCE_AST_SHA256, inventory["file"]["ast_hash"]
        )
        self.assertEqual(
            [
                {
                    **generator.EXPECTED_SYMBOL_RECEIPTS[symbol],
                    "path": generator.SOURCE_PATH,
                    "symbol": symbol,
                }
                for symbol in generator.TARGET_SYMBOLS
            ],
            inventory["symbols"],
        )

    def test_cases_are_sorted_unique_and_exactly_three_per_symbol(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertEqual(18, generator.EXPECTED_CASE_COUNT)
        self.assertEqual(
            {symbol: 3 for symbol in generator.TARGET_SYMBOLS},
            dict(Counter(item["symbol"] for item in definitions)),
        )

    def test_mixed_class_exception_and_member_equivalence_is_exact(self) -> None:
        value = self.fixture()
        self.assertEqual(
            {"Terrain": "native-typed-terrain-enum-valid-idf-token"},
            value["consumer_contract"]["adaptations"],
        )
        self.assertEqual(
            generator.EXPECTED_ASSERTION_IDS,
            value["consumer_contract"]["assertion_ids"],
        )
        self.assertEqual("exception", value["consumer_contract"]["classifications"]["Terrain"])
        for case in value["cases"]:
            if case["symbol"] == "Terrain":
                self.assertEqual(
                    generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS["Terrain"],
                    case["expected_dotnet"]["adaptation"],
                )
            else:
                self.assertNotIn("expected_dotnet", case)
                self.assertEqual(
                    "equivalent",
                    value["consumer_contract"]["classifications"][case["symbol"]],
                )

    def test_fixture_pins_member_order_tokens_and_qualified_rendering(self) -> None:
        value = self.fixture()
        topology = self.case(
            value, "dragon-model-terrain.enum.member-topology"
        )["python"]["facts"]
        self.assertEqual(
            ["COUNTRY", "SUBURBS", "CITY", "OCEAN", "URBAN"],
            topology["iterated_member_names"],
        )
        self.assertFalse(topology["has_aliases"])

        text = self.case(
            value, "dragon-model-terrain.enum.text-projection"
        )["python"]["facts"]
        self.assertEqual("City", text["json_tokens"]["CITY"])
        self.assertEqual("Terrain.CITY", text["str_tokens"]["CITY"])
        self.assertEqual(
            "Terrain.CITY", text["rendered_building_tokens"]["CITY"]
        )

        for name, expected in generator.EXPECTED_MEMBER_VALUES.items():
            facts = self.case(
                value,
                f"dragon-model-terrain.member.{name.lower()}.engineering-token",
            )["python"]["facts"]
            self.assertEqual(expected, facts["energyplus_choice_token"])
            self.assertTrue(facts["building_field_is_member"])

    @unittest.skipUnless(
        PINNED_SOURCE.is_file() and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_generation_is_byte_identical_twice_and_matches_fixture(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        first = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE
        )
        second = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE
        )
        first_bytes = (generator.strict_json_dumps(first, indent=2) + "\n").encode()
        second_bytes = (generator.strict_json_dumps(second, indent=2) + "\n").encode()
        self.assertEqual(first_bytes, second_bytes)
        self.assertEqual(FIXTURE_PATH.read_bytes(), first_bytes)

    def test_root_case_contract_runtime_and_symbol_tampering_fails(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        root = self.fixture()
        root["unexpected"] = True
        changes.append((root, "root"))

        case = self.fixture()
        case["cases"][0]["unexpected"] = True
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case"))

        equivalent = self.fixture()
        equivalent["cases"][3]["expected_dotnet"] = {
            "adaptation": "not-allowed",
            "outcome": "returned",
        }
        equivalent["cases_sha256"] = generator.cases_sha256(equivalent["cases"])
        changes.append((equivalent, "case"))

        contract = self.fixture()
        contract["consumer_contract"]["assertion_ids"]["Terrain"] = "wrong"
        changes.append((contract, "consumer contract"))

        runtime = self.fixture()
        runtime["runtime"]["dependencies"]["pandas"] = "2.3.1"
        changes.append((runtime, "runtime"))

        symbol = self.fixture()
        symbol["symbols"][0]["body_hash"] = "sha256:" + ("0" * 64)
        changes.append((symbol, "symbol"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_safe_semantic_tampering_fails_after_cases_hash_is_recomputed(self) -> None:
        construction = self.fixture()
        construction["cases"][0]["python"]["facts"]["invalid_observations"][0][
            "input"
        ]["value"] = "changed-safe-token"
        construction["cases_sha256"] = generator.cases_sha256(
            construction["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "construction semantics"):
            generator.validate_oracle(construction)

        text_projection = self.fixture()
        text_projection["cases"][2]["python"]["facts"][
            "safe_but_unexpected"
        ] = "extra"
        text_projection["cases_sha256"] = generator.cases_sha256(
            text_projection["cases"]
        )
        with self.assertRaisesRegex(RuntimeError, "text projection"):
            generator.validate_oracle(text_projection)

    def test_path_address_guid_timestamp_nonfinite_and_hash_tampering_fail(self) -> None:
        fixtures = (
            (1.25, "Raw float"),
            (r"C:\raw\path", "absolute path"),
            ("/tmp/terrain-oracle/file", "absolute path"),
            ("0xdeadbeef", "raw address"),
            ("01234567-89ab-4cde-8fab-0123456789ab", "GUID-like"),
            ("2026-08-26T12:34:56Z", "timestamp"),
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
        changed_cases[1]["python"]["facts"]["member_count"] = 999
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
                generator.load_exact_inventory(
                    path, generator.EXPECTED_UPSTREAM_COMMIT
                )


if __name__ == "__main__":
    unittest.main()

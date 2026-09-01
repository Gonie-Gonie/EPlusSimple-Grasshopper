from __future__ import annotations

from collections import Counter
import copy
import hashlib
import importlib.util
import json
import math
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
    / "generate_epsimple_construction_core_oracle.py"
)
BOOTSTRAP_PATH = REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-construction-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp" / "reference" / "tests"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_construction_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 107_953
EXPECTED_GENERATOR_SHA256 = (
    "sha256:3a46720e1cdf8ffd301a3af62fabe5c9a710d5fa9ba4c0130916bf9944f8f36f"
)
EXPECTED_FIXTURE_BYTES = 349_184
EXPECTED_FIXTURE_SHA256 = (
    "sha256:8fad664f712facf9eef8627d80e9bafcf468e4b0c63d4cf09d9632db814246b4"
)


def decode(value: object) -> object:
    if not isinstance(value, dict) or "kind" not in value:
        return value
    kind = value["kind"]
    if kind == "none":
        return None
    if kind == "bool":
        return value["value"]
    if kind == "int":
        return int(value["value"])
    if kind == "float":
        return float.fromhex(value["hex"])
    if kind == "float-nonfinite":
        return {
            "nan": math.nan,
            "positive-infinity": math.inf,
            "negative-infinity": -math.inf,
        }[value["value"]]
    if kind == "str":
        return value["value"]
    if kind in {"list", "tuple"}:
        items = [decode(item) for item in value["items"]]
        return items if kind == "list" else tuple(items)
    if kind == "dict":
        return {decode(item["key"]): decode(item["value"]) for item in value["items"]}
    raise AssertionError(f"Unexpected typed value kind: {kind}")


class EPlusSimpleConstructionCoreOracleTests(unittest.TestCase):
    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def facts(value: dict[str, object], code: str) -> dict[str, object]:
        matches = [
            item["python"]["facts"]["observations"]
            for item in value["cases"]
            if item["code"] == code
        ]
        if len(matches) != 1:
            raise AssertionError(f"Expected one construction case {code}.")
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

    @staticmethod
    def reseal(value: dict[str, object]) -> None:
        value["fact_sha256"] = {
            case["id"]: generator.canonical_sha256(case["python"]["facts"])
            for case in value["cases"]
        }
        for case in value["cases"]:
            case["python"]["facts_sha256"] = value["fact_sha256"][case["id"]]
        value["case_sha256"] = generator.case_sha256(value["cases"])
        value["cases_sha256"] = generator.cases_sha256(value["cases"])

    def test_generator_fixture_and_every_hash_layer_are_exact(self) -> None:
        value = self.fixture()
        generator_raw = GENERATOR_PATH.read_bytes()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_raw))
        self.assertEqual(EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH))
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(generator.EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertEqual(19, len(value["cases"]))
        self.assertEqual(19, len(value["fact_sha256"]))
        self.assertEqual(19, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )
        self.assertEqual(
            {
                "case_sha256",
                "cases",
                "cases_sha256",
                "consumer_contract",
                "excluded_receipts",
                "fact_sha256",
                "runtime",
                "schema",
                "symbols",
                "target_receipts",
                "upstream",
            },
            set(value),
        )

    def test_two_independent_bootstrap_regenerations_are_byte_identical(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="epsimple-construction-regeneration-", dir=TEST_TEMP_ROOT
        ) as temporary:
            first = Path(temporary) / "first.json"
            second = Path(temporary) / "second.json"
            self.regenerate(first)
            self.regenerate(second)
            expected = FIXTURE_PATH.read_bytes()
            self.assertEqual(expected, first.read_bytes())
            self.assertEqual(first.read_bytes(), second.read_bytes())
            self.assertEqual(EXPECTED_FIXTURE_SHA256, "sha256:" + hashlib.sha256(expected).hexdigest())

    def test_inventory_closure_is_exactly_48_targets_and_12_exclusions(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_targets = [
            75,
            76,
            79,
            *range(82, 92),
            94,
            *range(97, 115),
            117,
            *range(120, 135),
        ]
        expected_excluded = [77, 78, 80, 81, 92, 93, 95, 96, 115, 116, 118, 119]
        self.assertEqual(expected_targets, [item["inventory_index"] for item in value["target_receipts"]])
        self.assertEqual(expected_excluded, [item["inventory_index"] for item in value["excluded_receipts"]])
        self.assertEqual(48, len(value["target_receipts"]))
        self.assertEqual(12, len(value["excluded_receipts"]))
        self.assertEqual(inventory["target_receipts"], value["target_receipts"])
        self.assertEqual(inventory["excluded_receipts"], value["excluded_receipts"])
        counts = Counter(
            symbol for case in value["cases"] for symbol in case["target_symbols"]
        )
        self.assertEqual(Counter({symbol: 1 for symbol in generator.TARGET_SYMBOLS}), counts)
        all_case_symbols = {
            symbol
            for case in value["cases"]
            for symbol in (*case["target_symbols"], *case["context_symbols"])
        }
        self.assertFalse(all_case_symbols.intersection(generator.EXCLUDED_SYMBOLS))
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(counts))

    def test_consumer_contract_routes_adaptations_and_classifications_are_exact(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        self.assertEqual(generator.CLASSIFICATIONS, contract["classifications"])
        self.assertEqual(generator.ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            Counter({"exception": 41, "equivalent": 7}),
            Counter(contract["classifications"].values()),
        )
        self.assertEqual(set(generator.EQUIVALENT_SYMBOLS), {
            symbol
            for symbol, classification in contract["classifications"].items()
            if classification == "equivalent"
        })
        closure = contract["closure"]
        self.assertTrue(closure["exact_one_case_target_partition"])
        self.assertTrue(closure["full_source_classification_partition"])
        self.assertEqual(48, closure["target_count"])
        self.assertEqual(48, contract["evidence_contract"]["expected_receipt_count"])
        self.assertFalse(contract["evidence_contract"]["full_idf_emission_closure"])

    def test_runtime_source_resources_and_two_location_import_are_pinned(self) -> None:
        value = self.fixture()
        self.assertEqual(generator._expected_runtime(), value["runtime"])
        upstream = value["upstream"]
        self.assertEqual(generator.EXPECTED_SOURCE_SHA256, upstream["source"]["source_sha256"])
        self.assertEqual(generator.EXPECTED_SOURCE_AST_SHA256, upstream["source"]["ast_sha256"])
        self.assertEqual(list(generator.DATABASE_RESOURCES), upstream["database_resources"])
        isolated = upstream["isolated_import"]
        self.assertFalse(isolated["epsimple_package_initializer_executed"])
        self.assertFalse(isolated["epsimple_core_initializer_executed"])
        self.assertEqual(2, isolated["source_location_count"])
        self.assertEqual(16, len(isolated["loaded_local_modules"]))
        self.assertEqual(
            generator.EXPECTED_LOADED_LOCAL_MODULES_SHA256,
            generator.canonical_sha256(isolated["loaded_local_modules"]),
        )
        self.assertEqual(
            generator.EXPECTED_RUNTIME_SIGNATURES_SHA256,
            generator.canonical_sha256(value["consumer_contract"]["runtime_signatures"]),
        )
        relocation = self.facts(value, "R01")
        self.assertTrue(relocation["loaded_module_receipts_equal"])
        self.assertTrue(relocation["relocated_snapshot_equal"])
        self.assertEqual(
            {"material": 4, "surface": 1344, "fenestration": 432},
            relocation["primary_snapshot"]["database_counts"],
        )

    def test_material_state_validation_json_conversion_and_database_are_pinned(self) -> None:
        value = self.fixture()
        m01 = self.facts(value, "M01")
        self.assertTrue(m01["default"]["value"]["ID"]["auto_id"])
        self.assertEqual("MTRL-", m01["default"]["value"]["ID"]["prefix"])
        self.assertNotIn("hex_digits", m01["default"]["value"]["ID"])
        self.assertEqual("MAT-EXPLICIT", m01["explicit"]["value"]["ID"]["value"])
        self.assertIsNone(decode(m01["null_name"]["value"]["name"]))

        m02 = self.facts(value, "M02")
        conductivity = m02["setter_probes"]["conductivity"]
        self.assertEqual(
            ["returned"] * 4 + ["raised"] * 4,
            [item["observation"]["outcome"] for item in conductivity],
        )
        self.assertTrue(math.isnan(decode(conductivity[1]["stored"]["value"])))
        specific = m02["setter_probes"]["specific_heat"]
        self.assertEqual(
            ["raised", "raised", "returned", "returned", "returned", "raised", "raised"],
            [item["observation"]["outcome"] for item in specific],
        )

        m03 = self.facts(value, "M03")
        self.assertEqual(
            {"name": "Json Material", "conductivity": 0.37, "density": 745, "specific_heat": 915},
            decode(m03["to_dict"]["value"]),
        )
        self.assertEqual("Material", m03["to_dragon"]["value"]["type"])
        self.assertEqual("MAT-JSON", m03["to_dragon"]["value"]["name"])
        self.assertEqual("raised", m03["missing_attribute"]["outcome"])

        m04 = self.facts(value, "M04")
        self.assertEqual(4, m04["all"]["value"]["count"])
        self.assertEqual(["concrete", "insulation", "gypsumboard", "glasswool"], m04["all"]["value"]["names"])
        self.assertTrue(m04["object_replaced_on_reload"])
        self.assertTrue(m04["path"]["value"]["exists"])
        self.assertEqual("KeyError", m04["invalid"]["error"]["type"])

    def test_fenestration_state_validation_json_conversion_and_database_are_pinned(self) -> None:
        value = self.fixture()
        f01 = self.facts(value, "F01")
        self.assertTrue(f01["transparent"]["value"]["is_transparent"])
        self.assertFalse(f01["opaque"]["value"]["is_transparent"])
        self.assertIsNone(decode(f01["opaque"]["value"]["g"]))
        self.assertNotIn("hex_digits", f01["default"]["value"]["ID"])

        f02 = self.facts(value, "F02")
        u = f02["setter_probes"]["u"]
        self.assertEqual(["returned"] * 4 + ["raised"] * 4, [item["observation"]["outcome"] for item in u])
        g = f02["setter_probes"]["g"]
        self.assertEqual(
            ["returned"] * 5 + ["raised"] * 5,
            [item["observation"]["outcome"] for item in g],
        )
        self.assertEqual([False, False, True, True, True], [item["is_transparent"] for item in f02["transparency"]])

        f03 = self.facts(value, "F03")
        self.assertEqual("Glazing", f03["transparent"]["to_dragon"]["value"]["type"])
        self.assertEqual("NoMassConstruction", f03["opaque"]["to_dragon"]["value"]["type"])
        self.assertIsNone(decode(f03["opaque"]["from_json"]["value"]["g"]))
        self.assertEqual("AttributeError", f03["missing_transparent_g"]["error"]["type"])

        f04 = self.facts(value, "F04")
        self.assertEqual(432, f04["all"]["value"]["count"])
        self.assertTrue(f04["object_replaced_on_reload"])
        self.assertEqual("construction_regulation_fenestration.csv", f04["path"]["value"]["filename"])

    def test_surface_construction_metrics_filtering_validation_and_simple_factory_are_pinned(self) -> None:
        value = self.fixture()
        s01 = self.facts(value, "S01")
        self.assertEqual(2, len(s01["default"]["value"]["layers"]))
        self.assertEqual(1, len(s01["filtered"]["value"]["layers"]))
        self.assertIs(True, decode(s01["filtered"]["value"]["layers"][0]["thickness"]))
        self.assertEqual("ZeroDivisionError", s01["empty"]["value"]["U_internal"]["error"]["type"])
        self.assertTrue(all(item["observation"]["outcome"] == "raised" for item in s01["malformed"]))

        s02 = self.facts(value, "S02")
        metrics = decode(s02["metrics"]["value"])
        self.assertEqual(0.2, metrics["depth"])
        self.assertEqual(255000.0, metrics["heat_capacity"])
        self.assertEqual(["MAT-FIRST", "MAT-SECOND"], s02["unique_materials"]["value"]["keys"])
        self.assertEqual("ZeroDivisionError", s02["zero_convection"]["error"]["type"])

        s03 = self.facts(value, "S03")
        self.assertEqual(2, len(s03["standard"]["value"]["layers"]))
        self.assertEqual(1, len(s03["no_insulation"]["value"]["layers"]))
        self.assertEqual("ValueError", s03["invalid_equal_maximum"]["error"]["type"])
        self.assertEqual("Unknown format code 'f' for object of type 'str'", s03["invalid_equal_maximum"]["error"]["message"])

    def test_surface_reverse_json_dragon_database_and_regulation_are_pinned(self) -> None:
        value = self.fixture()
        s04 = self.facts(value, "S04")
        reversed_value = s04["reversed"]["value"]
        self.assertEqual("$REVERSED$:SURF-ORIGINAL", reversed_value["ID"]["value"])
        self.assertEqual("Original_reversed", decode(reversed_value["name"]))
        self.assertEqual(["MAT-SECOND", "MAT-FIRST"], [item["material_ID"]["value"] for item in reversed_value["layers"]])
        self.assertEqual([True, True], s04["layer_identity_reversed"])

        s05 = self.facts(value, "S05")
        self.assertEqual("Construction", s05["to_dragon"]["value"]["type"])
        self.assertEqual(["MAT-FIRST_120.0mm", "MAT-SECOND_80.0mm"], [item["name"] for item in s05["to_dragon"]["value"]["layers"]])
        self.assertEqual(["MAT-FIRST"], s05["seed_dictionary_keys_after"])
        self.assertTrue(s05["seed_material_unchanged"])
        self.assertEqual("KeyError", s05["missing_material"]["error"]["type"])

        s06 = self.facts(value, "S06")
        self.assertEqual(1344, s06["all"]["value"]["count"])
        self.assertEqual(1344, s06["count_after_reload"])
        self.assertTrue(s06["object_replaced_on_reload"])
        self.assertEqual("construction_regulation_surface.csv", s06["path"]["value"]["filename"])
        self.assertIn("20180901", s06["regulation_dates"])

        s07 = self.facts(value, "S07")
        self.assertEqual(6, len(s07["selections"]))
        self.assertTrue(all(item["observation"]["outcome"] == "returned" for item in s07["selections"]))
        self.assertEqual("KeyError", s07["missing_climate"]["error"]["type"])
        self.assertEqual("returned", s07["before_range"]["outcome"])

    def test_special_open_and_unknown_singleton_conversion_topology_is_pinned(self) -> None:
        value = self.fixture()
        x01 = self.facts(value, "X01")
        self.assertTrue(x01["base_first_aliases_open"])
        self.assertEqual("SpecialConstruction", x01["base_first_open_runtime_type"])
        self.assertTrue(x01["cross_class_distinct"])
        self.assertTrue(x01["same_class_singleton"])
        self.assertEqual({}, decode(x01["unique_materials"]["value"]))
        self.assertTrue(x01["open_reverse"]["value"]["same_identity"])
        self.assertTrue(x01["unknown_reverse"]["value"]["same_identity"])

        x02 = self.facts(value, "X02")
        self.assertEqual("$SPECIAL$:CTSF-OPEN", x02["ID"])
        self.assertEqual("AirBoundary", x02["dragon"]["value"]["type"])
        self.assertEqual("DefaultAirBoundary", x02["dragon"]["value"]["name"])
        self.assertEqual(0.5, decode(x02["dragon"]["value"]["ACH"]))

        x03 = self.facts(value, "X03")
        self.assertEqual("$SPECIAL$:CTSF-UNKNOWN", x03["ID"])
        self.assertIsNone(decode(x03["dragon"]["value"]))
        self.assertTrue(x03["same_singleton"])

    def test_resealed_fact_contract_receipt_and_order_tampering_all_fail_closed(self) -> None:
        original = self.fixture()

        facts = copy.deepcopy(original)
        self.facts(facts, "M01")["explicit"]["value"]["conductivity"] = generator._encoded_number(9.0)
        self.reseal(facts)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(facts)

        classification = copy.deepcopy(original)
        classification["consumer_contract"]["classifications"]["Material"] = "equivalent"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(classification)

        route = copy.deepcopy(original)
        route["consumer_contract"]["native_routes"]["Material"] = "wrong"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(route)

        loaded = copy.deepcopy(original)
        loaded["upstream"]["isolated_import"]["loaded_local_modules"][0]["module"] = "wrong"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(loaded)

        receipt = copy.deepcopy(original)
        receipt["target_receipts"][0]["inventory_index"] = 0
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(receipt)

        order = copy.deepcopy(original)
        order["cases"][0], order["cases"][1] = order["cases"][1], order["cases"][0]
        self.reseal(order)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(order)

        excluded = copy.deepcopy(original)
        excluded["cases"][0]["context_symbols"].append("Material.__repr__")
        self.reseal(excluded)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(excluded)

    def test_strict_json_safe_tree_duplicate_keys_and_inventory_tampering_fail_closed(self) -> None:
        for unsafe in (
            {"raw": 1.25},
            {"raw": float("nan")},
            {"raw": "object at 0x123456789abcdef0"},
            {"raw": r"C:\unsafe\fixture.json"},
            {"raw": "/tmp/unsafe/fixture.json"},
            {"raw": "2026-08-27T12:34:56"},
            {"kind": "evil", "value": "safe"},
            {7: "non-string-key"},
        ):
            with self.subTest(unsafe=unsafe), self.assertRaises(RuntimeError):
                generator._validate_safe_tree(unsafe)

        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            prefix="epsimple-construction-tamper-", dir=TEST_TEMP_ROOT
        ) as temporary:
            root = Path(temporary)
            duplicate = root / "duplicate.json"
            duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8", newline="\n")
            with self.assertRaises(SystemExit):
                generator.load_json_without_duplicates(duplicate)

            inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
            inventory["symbols"][75]["symbol_hash"] = "sha256:" + "0" * 64
            tampered = root / "inventory.json"
            tampered.write_text(json.dumps(inventory), encoding="utf-8", newline="\n")
            with self.assertRaises(SystemExit):
                generator.load_exact_inventory(tampered, generator.EXPECTED_UPSTREAM_COMMIT)


if __name__ == "__main__":
    unittest.main()

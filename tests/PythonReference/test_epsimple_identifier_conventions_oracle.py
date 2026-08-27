"""Fail-closed tests for the EPlusSimple identifier-conventions oracle."""

from __future__ import annotations

from collections import Counter
import copy
import hashlib
import importlib.util
import json
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
    / "generate_epsimple_identifier_conventions_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-identifier-conventions-oracle.json"
)
PINNED_SOURCE = (
    REPOSITORY_ROOT
    / "temp"
    / "reference"
    / "upstream"
    / "eplussimple"
    / "src"
    / "epsimple"
    / "constants.py"
)
TEST_TEMP_ROOT = REPOSITORY_ROOT / "temp"

spec = importlib.util.spec_from_file_location(
    "generate_epsimple_identifier_conventions_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load identifier-conventions generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 65_523
EXPECTED_GENERATOR_SHA256 = (
    "sha256:599254a8c597c61c369602d4977c7016b97c5334091cf767266fe95705c07a05"
)
EXPECTED_FIXTURE_BYTES = 121_058
EXPECTED_FIXTURE_SHA256 = (
    "sha256:c4375d858409fae187776ab88ebb6c5a21c76ac7d0e98b38ed2912a419f2bf7f"
)
EXPECTED_CASES_SHA256 = (
    "sha256:6244a03437d0d6f50bfeb135c99bfaf284804391998f168a675b30dc60ef3c10"
)


class EpsimpleIdentifierConventionsOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(
            prefix="epsimple-identifier-conventions-tests-",
            dir=TEST_TEMP_ROOT,
        )
        self.temp_root = Path(self.temporary.name)

    def tearDown(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def fixture() -> dict[str, object]:
        value = generator.load_json_without_duplicates(FIXTURE_PATH)
        generator.validate_oracle(value)
        return value

    @staticmethod
    def case(value: dict[str, object], code: str) -> dict[str, object]:
        matches = [item for item in value["cases"] if item["code"] == code]
        if len(matches) != 1:
            raise AssertionError(f"Expected exactly one case with code {code}.")
        return matches[0]

    @classmethod
    def facts(cls, value: dict[str, object], code: str) -> dict[str, object]:
        return cls.case(value, code)["python"]["facts"]

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
        self.assertEqual(
            EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH)
        )
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(22, len(value["fact_sha256"]))
        self.assertEqual(22, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )
        self.assertEqual(
            {
                "artifacts",
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

    def test_inventory_source_runtime_and_artifact_receipts_are_exact(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )

        self.assertEqual(inventory["symbols"], value["symbols"])
        self.assertEqual(inventory["target_receipts"], value["target_receipts"])
        self.assertEqual(inventory["excluded_receipts"], value["excluded_receipts"])
        self.assertEqual(
            [item[0] for item in generator.EXPECTED_TARGETS],
            [item["inventory_index"] for item in value["target_receipts"]],
        )
        self.assertEqual([26, 65], [
            item["inventory_index"] for item in value["excluded_receipts"]
        ])
        self.assertEqual(generator._expected_upstream(), value["upstream"])
        self.assertEqual(generator._expected_runtime(), value["runtime"])
        self.assertEqual(generator._expected_artifacts(), value["artifacts"])
        self.assertEqual(
            generator.EXPECTED_SOURCE_SHA256,
            value["upstream"]["source"]["source_sha256"],
        )
        self.assertEqual(
            generator.EXPECTED_SOURCE_AST_SHA256,
            value["upstream"]["source"]["ast_sha256"],
        )

    def test_scope_is_exactly_34_targets_with_repr_left_out_of_scope(self) -> None:
        value = self.fixture()
        contract = value["consumer_contract"]
        classifications = contract["classifications"]
        all_case_symbols = {
            symbol
            for case in value["cases"]
            for symbol in (*case["target_symbols"], *case["context_symbols"])
        }
        targeted = {
            symbol for case in value["cases"] for symbol in case["target_symbols"]
        }

        self.assertEqual(34, len(value["target_receipts"]))
        self.assertEqual(set(generator.TARGET_SYMBOLS), targeted)
        self.assertTrue(all_case_symbols.issubset(set(generator.TARGET_SYMBOLS)))
        self.assertTrue(set(generator.EXCLUDED_SYMBOLS).isdisjoint(all_case_symbols))
        self.assertEqual(
            Counter({"equivalent": 23, "exception": 11}),
            Counter(classifications.values()),
        )
        self.assertEqual(generator.CLASSIFICATIONS, classifications)
        self.assertEqual(generator.EXCEPTION_ADAPTATIONS, contract["adaptations"])
        self.assertEqual(generator.ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(generator.NATIVE_ROUTES, contract["native_routes"])
        self.assertEqual(
            ["AUTOID_PREFIX.__repr__", "SpecialTag.__repr__"],
            contract["closure"]["excluded_repr_symbols"],
        )

    def test_twenty_two_cases_are_ordered_and_partitioned_as_declared(self) -> None:
        value = self.fixture()
        self.assertEqual(
            list(generator.EXPECTED_CASE_IDS),
            [item["id"] for item in value["cases"]],
        )
        self.assertEqual(
            [item[0] for item in generator.CASE_SPECS],
            [item["code"] for item in value["cases"]],
        )
        self.assertEqual(
            Counter({"autoid": 6, "directory": 4, "package": 6, "special-tag": 6}),
            Counter(item["subfamily"] for item in value["cases"]),
        )
        self.assertTrue(
            all(item["python"]["outcome"] == "observed" for item in value["cases"])
        )

    def test_autoid_values_format_lookup_error_and_mutation_topology(self) -> None:
        value = self.fixture()
        topology = self.facts(value, "A01")
        strings = self.facts(value, "A02")
        construction = self.facts(value, "A03")
        formatting = self.facts(value, "A04")
        direct = self.facts(value, "A05")
        mutation = self.facts(value, "A06")

        self.assertEqual(
            list(generator.AUTO_MEMBERS),
            [(item["name"], item["value"]) for item in topology["declared_members"]],
        )
        self.assertEqual([], topology["alias_groups"])
        self.assertEqual(14, topology["unique_member_count"])
        self.assertTrue(all(
            item["canonical_identity"]
            and item["equals_raw_value"]
            and item["hash_equals_raw_value"]
            and item["is_str_instance"]
            for item in strings["members"]
        ))
        self.assertEqual(
            [("ZONE", "returned")],
            [
                (item["name"], item["observation"]["outcome"])
                for item in construction["from_name_as_value"]
                if item["observation"]["outcome"] == "returned"
            ],
        )
        self.assertEqual(
            ["TypeError", "ValueError", "ValueError", "ValueError", "KeyError"],
            [item["observation"]["error"]["type"] for item in construction["invalid"]],
        )
        material = formatting["members"][0]
        self.assertEqual("MTRL-", material["str"])
        self.assertEqual(
            ["MTRL-", "MTRL:SURFACE-", "MTRL::-", "MTRL:표면-", "MTRL: -"],
            [item["result"] for item in material["formats"]],
        )
        self.assertEqual("MTRL:7-", direct["direct_format_int"]["result"])
        self.assertEqual("MTRL-", direct["direct_format_none"]["result"])
        self.assertEqual(
            "MTRL:STABLE_OBJECT-", direct["direct_format_object"]["result"]
        )
        self.assertEqual("raised", direct["direct_str_extra_argument"]["outcome"])
        self.assertEqual("raised", direct["format_builtin_none"]["outcome"])
        self.assertEqual("returned", mutation["class_add_extra"]["outcome"])
        self.assertEqual("returned", mutation["member_add_extra"]["outcome"])
        self.assertEqual("raised", mutation["class_delete_member"]["outcome"])
        self.assertEqual("raised", mutation["class_reassign_member"]["outcome"])
        self.assertTrue(mutation["shallow_copy_identity"])
        self.assertTrue(mutation["deepcopy_identity"])

    def test_special_tag_values_format_lookup_error_and_mutation_topology(self) -> None:
        value = self.fixture()
        topology = self.facts(value, "S01")
        strings = self.facts(value, "S02")
        construction = self.facts(value, "S03")
        formatting = self.facts(value, "S04")
        direct = self.facts(value, "S05")
        mutation = self.facts(value, "S06")

        self.assertEqual(
            list(generator.SPECIAL_MEMBERS),
            [(item["name"], item["value"]) for item in topology["declared_members"]],
        )
        self.assertEqual([], topology["alias_groups"])
        self.assertEqual(5, topology["unique_member_count"])
        self.assertTrue(all(
            item["canonical_identity"]
            and item["equals_raw_value"]
            and item["hash_equals_raw_value"]
            and item["is_str_instance"]
            for item in strings["members"]
        ))
        self.assertEqual(
            [("SPECIAL", "returned")],
            [
                (item["name"], item["observation"]["outcome"])
                for item in construction["from_name_as_value"]
                if item["observation"]["outcome"] == "returned"
            ],
        )
        special = formatting["members"][0]
        self.assertEqual("$SPECIAL$:", special["str"])
        self.assertEqual(
            [
                "$SPECIAL$:",
                "$SPECIAL:SURFACE$:",
                "$SPECIAL::$:",
                "$SPECIAL:표면$:",
                "$SPECIAL: $:",
            ],
            [item["result"] for item in special["formats"]],
        )
        self.assertEqual("$SPECIAL:7$:", direct["direct_format_int"]["result"])
        self.assertEqual("$SPECIAL$:", direct["direct_format_none"]["result"])
        self.assertEqual(
            "$SPECIAL:STABLE_OBJECT$:", direct["direct_format_object"]["result"]
        )
        self.assertEqual("raised", direct["direct_str_extra_argument"]["outcome"])
        self.assertEqual("raised", direct["format_builtin_none"]["outcome"])
        self.assertEqual("returned", mutation["class_add_extra"]["outcome"])
        self.assertEqual("returned", mutation["member_add_extra"]["outcome"])
        self.assertEqual("raised", mutation["member_set_name"]["outcome"])
        self.assertEqual("raised", mutation["member_set_value"]["outcome"])
        self.assertTrue(mutation["shallow_copy_identity"])
        self.assertTrue(mutation["deepcopy_identity"])

    def test_directory_relocation_path_roles_mutation_and_errors_are_pinned(self) -> None:
        value = self.fixture()
        topology = self.facts(value, "D01")
        relocation = self.facts(value, "D02")
        mutation = self.facts(value, "D03")
        instances = self.facts(value, "D04")

        self.assertEqual(list(generator.DIRECTORY_MEMBERS), topology["public_attribute_order"])
        self.assertEqual(
            "repository/src/epsimple/_data", topology["data_root_relative"]
        )
        self.assertEqual("repository", topology["package_root_relative"])
        for name, state in topology["state"].items():
            self.assertEqual(generator.DIRECTORY_ROLES[name][0], state["anchor"])
            self.assertEqual(generator.DIRECTORY_ROLES[name][1], state["suffix"])
            self.assertTrue(state["is_path"])
            self.assertTrue(state["matches_role"])
        self.assertTrue(relocation["class_identity_distinct"])
        self.assertTrue(relocation["relative_roles_equal"])
        self.assertTrue(all(relocation["public_absolute_values_distinct"].values()))
        self.assertTrue(all(
            item["assigned"]["equals_replacement"]
            and item["deleted_lookup"]["outcome"] == "raised"
            and item["restored_identity"]
            for item in mutation["attributes"]
        ))
        self.assertEqual(
            ["Directory() takes no arguments", "Directory() takes no arguments"],
            [
                instances["keyword_argument"]["error"]["message"],
                instances["positional_argument"]["error"]["message"],
            ],
        )
        self.assertTrue(all(item["class_unchanged"] for item in instances["shadowed"]))

    def test_package_metadata_operations_mutation_and_errors_are_pinned(self) -> None:
        value = self.fixture()
        topology = self.facts(value, "P01")
        mutation = self.facts(value, "P02")
        instances = self.facts(value, "P03")
        name = self.facts(value, "P04")
        version = self.facts(value, "P05")
        required = self.facts(value, "P06")

        self.assertEqual("epsimple", topology["name"])
        self.assertEqual([0, 7, 0], topology["version"])
        self.assertEqual([3, 12], topology["required_python"])
        self.assertEqual("tuple", topology["version_type"])
        self.assertEqual("tuple", topology["required_python_type"])
        self.assertTrue(all(
            item["assigned"]["equals_replacement"]
            and item["deleted_lookup"]["outcome"] == "raised"
            and item["restored_identity"]
            for item in mutation["attributes"]
        ))
        self.assertEqual("returned", instances["construction"]["outcome"])
        self.assertEqual("raised", instances["keyword_argument"]["outcome"])
        self.assertEqual("raised", instances["positional_argument"]["outcome"])
        self.assertTrue(all(item["class_unchanged"] for item in instances["shadowed"]))
        self.assertEqual("EPSIMPLE", name["upper"])
        self.assertEqual("raised", name["item_assignment"]["outcome"])
        self.assertEqual("raised", name["plus_integer"]["outcome"])
        self.assertEqual("0.7.0", version["join"])
        self.assertTrue(version["less_than_next"])
        self.assertEqual("raised", version["mixed_comparison"]["outcome"])
        self.assertTrue(required["runtime_meets_requirement"])
        self.assertFalse(required["supports_3_11"])
        self.assertTrue(required["supports_3_13"])
        self.assertEqual("raised", required["join_without_conversion"]["outcome"])

    def test_regeneration_from_explicit_pinned_source_is_byte_identical(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        regenerated = generator.build_oracle(
            inventory,
            generator.EXPECTED_UPSTREAM_COMMIT,
            PINNED_SOURCE,
        )
        encoded = (generator.strict_json_dumps(regenerated, indent=2) + "\n").encode(
            "utf-8"
        )

        self.assertEqual(FIXTURE_PATH.read_bytes(), encoded)
        self.assertEqual(
            EXPECTED_FIXTURE_SHA256,
            "sha256:" + hashlib.sha256(encoded).hexdigest(),
        )

    def test_validation_fails_closed_on_resealed_contract_fact_and_order_drift(self) -> None:
        original = self.fixture()

        classification = copy.deepcopy(original)
        classification["consumer_contract"]["classifications"]["AUTOID_PREFIX"] = (
            "equivalent"
        )
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(classification)

        adaptation = copy.deepcopy(original)
        adaptation["consumer_contract"]["adaptations"]["Directory"] = "wrong"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(adaptation)

        runtime = copy.deepcopy(original)
        runtime["runtime"]["python_version"] = "3.12.8"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(runtime)

        receipt = copy.deepcopy(original)
        receipt["target_receipts"][0]["symbol_hash"] = "sha256:" + ("0" * 64)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(receipt)

        formatting = copy.deepcopy(original)
        self.facts(formatting, "A04")["members"][0]["str"] = "MTRL"
        self.reseal(formatting)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(formatting)

        order = copy.deepcopy(original)
        order["cases"][0], order["cases"][1] = order["cases"][1], order["cases"][0]
        self.reseal(order)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(order)

        repr_injection = copy.deepcopy(original)
        repr_injection["cases"][0]["context_symbols"].append("AUTOID_PREFIX.__repr__")
        self.reseal(repr_injection)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(repr_injection)

    def test_strict_json_and_safe_tree_reject_tampering(self) -> None:
        for value in (float("nan"), float("inf"), float("-inf")):
            with self.subTest(value=value), self.assertRaises(ValueError):
                generator.strict_json_dumps({"value": value})
            with self.subTest(value=value), self.assertRaises(RuntimeError):
                generator._validate_safe_tree({"value": value})

        for key in (
            "0xdeadbeef",
            r"C:\unsafe\fixture.json",
            "2026-08-27T12:34:56",
        ):
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                generator._validate_safe_tree({key: None})
        with self.assertRaises(RuntimeError):
            generator._validate_safe_tree({7: None})

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text('{"schema":"first","schema":"second"}\n', encoding="utf-8")
        with self.assertRaises(SystemExit):
            generator.load_json_without_duplicates(duplicate)

        nonfinite = self.temp_root / "nonfinite.json"
        nonfinite.write_text('{"value":NaN}\n', encoding="utf-8")
        with self.assertRaises(SystemExit):
            generator.load_json_without_duplicates(nonfinite)

    def test_inventory_and_source_tampering_are_rejected(self) -> None:
        inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        inventory["symbols"][generator.EXPECTED_TARGETS[0][0]]["symbol_hash"] = (
            "sha256:" + ("f" * 64)
        )
        tampered_inventory = self.temp_root / "tampered-inventory.json"
        tampered_inventory.write_text(json.dumps(inventory), encoding="utf-8")
        with self.assertRaises(SystemExit):
            generator.load_exact_inventory(
                tampered_inventory, generator.EXPECTED_UPSTREAM_COMMIT
            )

        source_bytes = bytearray(PINNED_SOURCE.read_bytes())
        source_bytes[0] = ord("#") if source_bytes[0] != ord("#") else ord(" ")
        tampered_source = self.temp_root / "constants.py"
        tampered_source.write_bytes(source_bytes)
        exact_inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        with self.assertRaises(SystemExit):
            generator.build_oracle(
                exact_inventory,
                generator.EXPECTED_UPSTREAM_COMMIT,
                tampered_source,
            )


if __name__ == "__main__":
    unittest.main()

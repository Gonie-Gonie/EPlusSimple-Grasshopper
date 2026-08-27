"""Fail-closed tests for the pinned Dragon construction core oracle."""

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
    / "generate_dragon_construction_core_oracle.py"
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
    / "dragon-construction-core-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_construction_core_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 82_993
EXPECTED_GENERATOR_SHA256 = (
    "sha256:94f9b3822c0e36b0ed12395d87f2febd3c07ebb0159950009d3daddb6766b9b9"
)
EXPECTED_FIXTURE_BYTES = 395_339
EXPECTED_FIXTURE_SHA256 = (
    "sha256:1d7034be43ebf8528db6342eec7c0c2fc151148e9a31f80a2a2c21c5fe04a41e"
)
EXPECTED_CASES_SHA256 = (
    "sha256:fefa2bfd0adc759e513dd2f0a83907595ab4c0519eededa3ed24ffaeb38c3e7c"
)


class DragonConstructionCoreOracleTests(unittest.TestCase):
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
            raise AssertionError(f"Expected one construction scenario {scenario}.")
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
        self.assertEqual(19, len(value["cases"]))
        self.assertEqual(19, len(value["fact_sha256"]))
        self.assertEqual(19, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_byte_identical_regeneration_is_repeatable(self) -> None:
        with tempfile.TemporaryDirectory(prefix="construction-core-oracle-") as temporary:
            first = Path(temporary) / "first.json"
            second = Path(temporary) / "second.json"
            self.regenerate(first)
            self.regenerate(second)
            self.assertEqual(FIXTURE_PATH.read_bytes(), first.read_bytes())
            self.assertEqual(first.read_bytes(), second.read_bytes())

    def test_inventory_binds_thirty_five_targets_and_all_adjacent_exclusions(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        expected_indices = (
            [593, 594]
            + list(range(597, 601))
            + list(range(602, 606))
            + [609, 610]
            + list(range(613, 617))
            + [618]
            + list(range(620, 638))
        )
        self.assertEqual(expected_indices, [item["inventory_index"] for item in inventory["target_receipts"]])
        self.assertEqual(35, len(inventory["symbols"]))
        self.assertEqual(generator.EXPECTED_INVENTORY_SHA256, inventory["content_sha256"])
        exclusions = inventory["adjacent_exclusions"]
        self.assertEqual(18, len(exclusions))
        self.assertEqual(
            list(generator.ADJACENT_EXCLUSION_IDENTITIES),
            [(item["inventory_index"], item["symbol"]) for item in exclusions],
        )
        value = self.fixture()
        self.assertEqual(inventory["target_receipts"], value["target_receipts"])
        self.assertEqual(exclusions, value["upstream"]["adjacent_exclusions"])
        self.assertEqual(
            generator.EXPECTED_ADJACENT_EXCLUSIONS_SHA256,
            generator.canonical_sha256(exclusions),
        )
        self.assertEqual(12, len(value["upstream"]["loaded_local_modules"]))
        self.assertEqual(
            generator.EXPECTED_SOURCE_SHA256,
            value["upstream"]["construction_source"]["source_sha256"],
        )
        self.assertEqual(
            generator.EXPECTED_SOURCE_AST_SHA256,
            value["upstream"]["construction_source"]["ast_sha256"],
        )

    def test_scope_is_exactly_eleven_equivalent_and_twenty_four_exception_targets(self) -> None:
        value = self.fixture()
        target_counts = Counter(
            symbol for case in value["cases"] for symbol in case["target_symbols"]
        )
        self.assertEqual(set(generator.TARGET_SYMBOLS), set(target_counts))
        self.assertTrue(all(count >= 1 for count in target_counts.values()))
        self.assertFalse(set(generator.EXCLUDED_SYMBOLS).intersection(target_counts))
        self.assertEqual(
            Counter({"exception": 24, "equivalent": 11}),
            Counter(generator.CLASSIFICATIONS.values()),
        )
        self.assertEqual(set(generator.EQUIVALENT_SYMBOLS), {
            symbol for symbol, classification in generator.CLASSIFICATIONS.items()
            if classification == "equivalent"
        })
        contract = value["consumer_contract"]
        self.assertEqual({"equivalent": 11, "exception": 24}, contract["classification_counts"])
        self.assertEqual(35, contract["evidence_contract"]["expected_receipt_count"])
        self.assertFalse(contract["evidence_contract"]["full_idf_closure"])
        self.assertFalse(contract["closure"]["full_symbol_closure"])
        self.assertFalse(contract["closure"]["full_construction_family_closure"])
        self.assertTrue(contract["closure"]["target_coverage_complete"])
        self.assertEqual(
            "immutable-validated-native-construction-reverse-f3f8b2b1",
            contract["adaptations"]["Construction.reversed"],
        )

    def test_runtime_signatures_and_six_subfamily_matrix_are_exact(self) -> None:
        value = self.fixture()
        self.assertEqual(generator.RUNTIME_SIGNATURES, value["consumer_contract"]["runtime_signatures"])
        self.assertEqual(35, len(generator.RUNTIME_SIGNATURES))
        subfamilies = Counter(case["subfamily"] for case in value["cases"])
        self.assertEqual(
            Counter({"roughness": 3, "material": 3, "layer": 3, "construction": 4, "glazing": 3, "no-mass": 3}),
            subfamilies,
        )
        self.assertEqual(
            [f"C{index:02d}" for index in range(1, 20)],
            [case["python"]["facts"]["scenario"] for case in value["cases"]],
        )

    def test_c01_to_c03_pin_roughness_order_strings_and_conversion_errors(self) -> None:
        value = self.fixture()
        c01 = self.facts(value, "C01")
        self.assertTrue(c01["observations"]["class_is_str_subclass"])
        self.assertEqual(
            ["VERYROUGH", "ROUGH", "MEDIUMROUGH", "MEDIUMSMOOTH", "SMOOTH"],
            c01["source_state"]["member_names_in_iteration_order"],
        )
        self.assertEqual(
            ["VeryRough", "Rough", "MediumRough", "MediumSmooth", "Smooth"],
            [item["value"] for item in c01["observations"]["members"]],
        )
        c02 = self.facts(value, "C02")
        self.assertEqual(
            "VeryRough|Rough|MediumRough|MediumSmooth|Smooth",
            c02["observations"]["joined"],
        )
        self.assertTrue(all(c02["observations"]["values_equal_strings"]))
        c03 = self.facts(value, "C03")
        self.assertEqual(12, len(c03["observations"]["probes"]))
        self.assertEqual(
            ["returned"] * 6 + ["raised"] * 6,
            [item["outcome"] for item in c03["observations"]["probes"]],
        )
        self.assertTrue(c03["observations"]["probes"][5]["same_identity_as_input"])
        self.assertTrue(all(item["error"]["type"] == "ValueError" for item in c03["observations"]["probes"][6:]))

    def test_c04_to_c06_pin_material_defaults_mutability_and_validator_order(self) -> None:
        value = self.fixture()
        default = self.facts(value, "C04")["observations"]["material"]
        self.assertEqual("Rough", default["roughness"]["value"])
        self.assertEqual(generator._encode(0.9), default["thermal_absorptance"]["value"])
        self.assertEqual(generator._encode(0.7), default["solar_absorptance"]["value"])
        mutation = self.facts(value, "C05")
        self.assertEqual(9, len(mutation["source_state"]["snapshots"]))
        self.assertEqual("list", mutation["observations"]["final"]["name"]["value"]["kind"])
        self.assertEqual("MediumRough", mutation["observations"]["final"]["roughness"]["value"])
        domain = self.facts(value, "C06")
        self.assertEqual("none", domain["observations"]["null_name_state"]["name"]["value"]["kind"])
        conductivity = domain["observations"]["setter_probes"]["conductivity"]
        self.assertEqual(["returned", "returned", "returned", "raised", "raised", "raised"], [item["event"]["outcome"] for item in conductivity])
        self.assertEqual(["bool", "float-nonfinite", "float-nonfinite"], [item["stored"]["value"]["kind"] for item in conductivity[:3]])
        specific = domain["observations"]["setter_probes"]["specific_heat"]
        self.assertEqual("ValueError", specific[1]["event"]["error"]["type"])
        self.assertEqual("returned", specific[2]["event"]["outcome"])
        thermal = domain["observations"]["setter_probes"]["thermal_absorptance"]
        self.assertEqual("returned", thermal[3]["event"]["outcome"])
        self.assertEqual("raised", thermal[6]["event"]["outcome"])

    def test_c07_to_c09_pin_layer_formulas_alias_mutation_and_error_timing(self) -> None:
        value = self.fixture()
        state = self.facts(value, "C07")["observations"]
        self.assertTrue(state["material_identity_retained"])
        self.assertEqual("0x1.e000000000000p+4", state["layer"]["U"]["value"]["hex"])
        self.assertEqual("0x1.9000000000000p+6", state["layer"]["heat_capacity"]["value"]["hex"])
        mutation = self.facts(value, "C08")
        self.assertFalse(mutation["observations"]["material_is_first"])
        self.assertTrue(mutation["observations"]["material_is_second"])
        self.assertEqual(5, len(mutation["source_state"]["snapshots"]))
        domain = self.facts(value, "C09")
        thickness = domain["observations"]["thickness_probes"]
        self.assertEqual(["returned", "returned", "returned", "raised", "raised", "raised"], [item["event"]["outcome"] for item in thickness])
        partial = domain["source_state"]["partial_constructor_states"]
        self.assertEqual(["name"], partial["invalid_material"]["attribute_names"])
        self.assertEqual(["_Layer__material", "name"], partial["invalid_thickness"]["attribute_names"])

    def test_c10_to_c13_pin_binary64_order_overloads_reverse_alias_and_empty_state(self) -> None:
        value = self.fixture()
        c10 = self.facts(value, "C10")
        witness = c10["observations"]["ulp_witness_U"]["value"]
        self.assertEqual("0x1.5d1745d1745d2p+1", witness["hex"])
        self.assertEqual("2.7272727272727275", witness["repr"])
        self.assertEqual([True, True], c10["observations"]["input_identity_order"])
        c11 = self.facts(value, "C11")
        self.assertEqual(["First_1mm", "Second_10mm"], c11["observations"]["generated_layer_names"])
        self.assertEqual("First_1000mm", c11["observations"]["bool_thickness"]["layer_names"][0]["value"]["value"])
        c12 = self.facts(value, "C12")
        self.assertTrue(c12["observations"]["shares_every_layer"])
        self.assertEqual([True, True], c12["observations"]["reversed_identity_order"])
        self.assertEqual(
            "Original_reversed",
            c12["observations"]["default_name"]["value"]["value"],
        )
        self.assertEqual("", c12["observations"]["custom_name"]["value"]["value"])
        c13 = self.facts(value, "C13")
        self.assertEqual("int", c13["observations"]["empty_thickness"]["runtime_type"])
        self.assertEqual("0", c13["observations"]["empty_heat_capacity"]["value"]["value"])
        errors = {item["phase"]: item["error"]["type"] for item in c13["timeline"] if item["outcome"] == "raised"}
        self.assertEqual("ZeroDivisionError", errors["empty-u"])
        self.assertEqual("TypeError", errors["construct-mixed-even"])
        self.assertEqual("ValueError", errors["construct-odd"])
        self.assertEqual("AttributeError", errors["metric-after-invalid-append"])

    def test_c14_to_c19_pin_glazing_and_no_mass_mutability_and_domains(self) -> None:
        value = self.fixture()
        glazing = self.facts(value, "C14")["observations"]["glazing"]
        self.assertEqual(generator._encode(1.6), glazing["U"]["value"])
        self.assertEqual(generator._encode(0.55), glazing["G"]["value"])
        mutated = self.facts(value, "C15")["observations"]["final"]
        self.assertEqual("list", mutated["name"]["value"]["kind"])
        self.assertEqual(generator._encode(1.25), mutated["G"]["value"])
        glazing_domain = self.facts(value, "C16")
        g_probes = glazing_domain["observations"]["setter_probes"]["G"]
        self.assertEqual(["returned"] * 5 + ["raised"] * 3, [item["event"]["outcome"] for item in g_probes])
        self.assertEqual(["_Glazing__U", "name"], glazing_domain["source_state"]["partial_invalid_g"]["attribute_names"])
        no_mass = self.facts(value, "C17")["observations"]["construction"]
        self.assertEqual(generator._encode(2.5), no_mass["U"]["value"])
        no_mass_mutated = self.facts(value, "C18")["observations"]["final"]
        self.assertEqual(generator._encode(3.5), no_mass_mutated["U"]["value"])
        no_mass_domain = self.facts(value, "C19")
        self.assertEqual("none", no_mass_domain["observations"]["null_name"]["name"]["value"]["kind"])
        self.assertEqual(["returned", "returned", "returned", "raised", "raised", "raised"], [item["event"]["outcome"] for item in no_mass_domain["observations"]["setter_probes"]])

    def test_every_case_fact_mutation_and_contract_tampering_fail_closed(self) -> None:
        fixture = self.fixture()
        for index in range(19):
            tampered = copy.deepcopy(fixture)
            tampered["cases"][index]["python"]["facts"]["scenario"] = "tampered"
            with self.subTest(index=index):
                with self.assertRaisesRegex(RuntimeError, "hash|identity"):
                    generator.validate_oracle(tampered)

        contract = copy.deepcopy(fixture)
        contract["consumer_contract"]["classifications"]["Construction.reversed"] = "equivalent"
        with self.assertRaisesRegex(RuntimeError, "consumer contract"):
            generator.validate_oracle(contract)

        exclusion = copy.deepcopy(fixture)
        exclusion["upstream"]["adjacent_exclusions"][0]["body_hash"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(RuntimeError, "exclusion|upstream"):
            generator.validate_oracle(exclusion)

        receipt = copy.deepcopy(fixture)
        receipt["target_receipts"][0]["inventory_index"] = 0
        with self.assertRaisesRegex(RuntimeError, "target receipts"):
            generator.validate_oracle(receipt)

    def test_unsafe_noncanonical_and_duplicate_key_trees_fail_closed(self) -> None:
        for value in (
            {"raw": float("nan")},
            {"raw": 1.25},
            {"address": "object at 0x123456789abcdef0"},
            {"path": "C:\\host\\secret"},
            {"guid": "12345678-1234-4234-9234-123456789abc"},
            {"timestamp": "2026-08-27T12:34:56"},
            {"object at 0x123456789abcdef0": "unsafe-key"},
        ):
            with self.subTest(value=value):
                with self.assertRaises(RuntimeError):
                    generator._validate_safe_tree(value)

        for encoded in (
            {"hex": "nan", "kind": "float", "repr": "nan"},
            {"hex": "0x1.0000000000000p+0", "kind": "float", "repr": "1.00"},
            {"kind": "float-nonfinite", "value": "infinity"},
            {"kind": "int", "value": "01"},
            {"kind": "evil", "value": "safe"},
            {"extra": "still-unsafe", "kind": "evil", "value": "safe"},
            {"items": [{"key": {"kind": "str", "value": "k"}}], "kind": "dict"},
            {
                "items": [
                    {
                        "key": {"kind": "str", "value": "duplicate"},
                        "value": {"kind": "int", "value": "1"},
                    },
                    {
                        "key": {"kind": "str", "value": "duplicate"},
                        "value": {"kind": "int", "value": "2"},
                    },
                ],
                "kind": "dict",
            },
            {
                "items": [
                    {
                        "key": {
                            "kind": "str",
                            "value": "object at 0x123456789abcdef0",
                        },
                        "value": {"kind": "str", "value": "safe"},
                    }
                ],
                "kind": "dict",
            },
        ):
            with self.subTest(encoded=encoded):
                with self.assertRaises(RuntimeError):
                    generator._validate_safe_tree(encoded)

        with tempfile.TemporaryDirectory(prefix="construction-core-json-") as temporary:
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

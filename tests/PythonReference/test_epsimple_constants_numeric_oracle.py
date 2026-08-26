"""Fail-closed tests for the epsimple numeric-constants reference oracle."""

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
    / "generate_epsimple_constants_numeric_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "epsimple-constants-numeric-oracle.json"
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
    "generate_epsimple_constants_numeric_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load epsimple constants generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 89_695
EXPECTED_FIXTURE_SHA256 = (
    "sha256:252708fae632f2d587c8ab6f3659f0c94a34c1d2f2b3c70c70997a824c590ee2"
)
EXPECTED_CASES_SHA256 = (
    "sha256:e80c7d274444f640a4c3a2ddf3b8a7c03e06adfe6e0b3b844c8ed74dce501e3a"
)


class EpsimpleConstantsNumericOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        TEST_TEMP_ROOT.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(
            prefix="epsimple-constants-numeric-oracle-tests-",
            dir=TEST_TEMP_ROOT,
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
        self.assertEqual(generator.SCHEMA, value["schema"])
        self.assertEqual(generator.EXPECTED_CASE_COUNT, len(value["cases"]))
        self.assertEqual(
            list(generator.EXPECTED_CASE_IDS),
            [item["id"] for item in value["cases"]],
        )
        self.assertEqual(
            Counter({symbol: 3 for symbol in generator.TARGET_SYMBOLS}),
            Counter(item["symbol"] for item in value["cases"]),
        )
        self.assertEqual(
            {
                "cases",
                "cases_sha256",
                "consumer_contract",
                "runtime",
                "schema",
                "symbols",
                "upstream",
            },
            set(value),
        )

    def test_inventory_receipts_and_consumer_contract_are_exact(self) -> None:
        value = self.fixture()
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )

        self.assertEqual(inventory["symbols"], value["symbols"])
        self.assertEqual(29, len(value["symbols"]))
        self.assertEqual(list(generator.TARGET_SYMBOLS), [
            item["symbol"] for item in value["symbols"]
        ])
        contract = value["consumer_contract"]
        self.assertEqual(
            generator.EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
            contract["adaptations"],
        )
        self.assertEqual(generator.EXPECTED_ASSERTION_IDS, contract["assertion_ids"])
        self.assertEqual(5, Counter(contract["classifications"].values())["exception"])
        self.assertEqual(24, Counter(contract["classifications"].values())["equivalent"])
        self.assertEqual(
            {
                "ConvectionHeatTransfer",
                "Site2CO2",
                "Site2Cost",
                "Site2Source",
                "Unit",
            },
            {
                symbol
                for symbol, classification in contract["classifications"].items()
                if classification == "exception"
            },
        )

    def test_alias_topology_and_site_to_source_dual_behavior_are_pinned(self) -> None:
        value = self.fixture()
        unit = self.case(
            value,
            generator._class_case_id("Unit", "member-topology"),
        )["python"]["facts"]
        self.assertEqual([["MM_TO_M", "W_TO_KW"]], unit["alias_groups"])
        self.assertEqual("MM_TO_M", unit["canonical_names"]["W_TO_KW"])
        self.assertEqual(7, unit["member_count"])
        self.assertEqual(6, unit["unique_member_count"])

        source = self.case(
            value,
            generator._class_case_id("Site2Source", "member-topology"),
        )["python"]["facts"]
        self.assertEqual(
            [["NATURALGAS", "LPG", "OIL"]], source["alias_groups"]
        )
        self.assertEqual(5, source["member_count"])
        self.assertEqual(3, source["unique_member_count"])
        self.assertEqual(
            ["ELECTRICITY", "NATURALGAS", "DISTRICTHEATING"],
            source["iterated_member_names"],
        )
        scaling = source["result_scaling"]
        self.assertEqual(generator.RESULT_CARRIER_ORDER, scaling["carrier_order"])
        self.assertEqual(
            [
                "ELECTRICITY",
                "NATURALGAS",
                "DISTRICTHEATING",
                "UNMATCHED",
                "UNMATCHED",
            ],
            scaling["factor_sources"],
        )
        self.assertEqual(
            [
                "1.6000000000000p+1",
                "1.199999999999ap+0",
                "1.74bc6a7ef9db2p-1",
                "1.0000000000000p+0",
                "1.0000000000000p+0",
            ],
            [item["binary64"] for item in scaling["factors"]],
        )
        self.assertEqual(
            generator._expected_direct_method_execution(),
            scaling["direct_method_execution"],
        )
        self.assertEqual(
            generator.EXPECTED_MODEL_OBSERVATION_DEPENDENCY,
            value["upstream"]["observation_dependency"],
        )

    def test_every_numeric_member_has_value_semantics_and_probe_cases(self) -> None:
        value = self.fixture()
        for symbol, expected in generator.EXPECTED_VALUES.items():
            value_case = self.case(
                value, generator._member_case_id(symbol, "value")
            )["python"]["facts"]
            semantics = self.case(
                value, generator._member_case_id(symbol, "numeric-semantics")
            )["python"]["facts"]
            probe = self.case(
                value, generator._member_case_id(symbol, "engineering-probe")
            )["python"]["facts"]

            self.assertEqual(expected, value_case["value"]["binary64"])
            self.assertEqual(value_case["canonical_name"], semantics["canonical_name"])
            self.assertEqual(value_case["declared_name"], semantics["declared_name"])
            self.assertTrue(semantics["equals_value"])
            self.assertTrue(semantics["is_float_instance"])
            self.assertTrue(semantics["is_same_as_canonical_member"])
            self.assertEqual("float", semantics["value_type"])
            self.assertEqual(expected, semantics["float_projection"]["binary64"])
            self.assertEqual("multiply", probe["operation"])
            self.assertEqual(
                generator.EXPECTED_PROBE_RESULTS[symbol],
                probe["result"]["binary64"],
            )

    def test_regeneration_is_byte_identical(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        regenerated = generator.build_oracle(
            inventory,
            generator.EXPECTED_UPSTREAM_COMMIT,
            PINNED_SOURCE,
        )
        encoded = (
            generator.strict_json_dumps(regenerated, indent=2) + "\n"
        ).encode("utf-8")

        self.assertEqual(FIXTURE_PATH.read_bytes(), encoded)
        self.assertEqual(
            EXPECTED_FIXTURE_SHA256,
            "sha256:" + hashlib.sha256(encoded).hexdigest(),
        )

    def test_validation_fails_closed_on_contract_and_fact_drift(self) -> None:
        original = self.fixture()

        classification = copy.deepcopy(original)
        classification["consumer_contract"]["classifications"]["Unit"] = "equivalent"
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(classification)

        adaptation = copy.deepcopy(original)
        class_case = next(
            item
            for item in adaptation["cases"]
            if item["symbol"] == "Site2Source" and "expected_dotnet" in item
        )
        class_case["expected_dotnet"]["adaptation"] = "wrong-adaptation"
        adaptation["cases_sha256"] = generator.cases_sha256(adaptation["cases"])
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(adaptation)

        raw_float = copy.deepcopy(original)
        raw_float["cases"][0]["python"]["facts"]["raw"] = 0.1
        raw_float["cases_sha256"] = generator.cases_sha256(raw_float["cases"])
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(raw_float)

        symbol_receipt = copy.deepcopy(original)
        symbol_receipt["symbols"][0]["symbol_hash"] = "sha256:" + ("0" * 64)
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(symbol_receipt)

        construction = copy.deepcopy(original)
        construction_case = self.case(
            construction,
            generator._class_case_id("Unit", "construction"),
        )
        construction_case["python"]["facts"]["observations"][0]["result"][
            "name"
        ] = "W_TO_KW"
        construction["cases_sha256"] = generator.cases_sha256(
            construction["cases"]
        )
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(construction)

        numeric_semantics = copy.deepcopy(original)
        semantics_case = self.case(
            numeric_semantics,
            generator._member_case_id("Site2CO2.ELECTRICITY", "numeric-semantics"),
        )
        semantics_case["python"]["facts"]["is_float_instance"] = False
        numeric_semantics["cases_sha256"] = generator.cases_sha256(
            numeric_semantics["cases"]
        )
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(numeric_semantics)

        engineering_input = copy.deepcopy(original)
        probe_case = self.case(
            engineering_input,
            generator._member_case_id("Unit.MM_TO_M", "engineering-probe"),
        )
        probe_case["python"]["facts"]["input"] = {
            "binary64": "1.0000000000000p+0",
            "kind": "float",
        }
        engineering_input["cases_sha256"] = generator.cases_sha256(
            engineering_input["cases"]
        )
        with self.assertRaises(RuntimeError):
            generator.validate_oracle(engineering_input)

    def test_safe_tree_rejects_unsafe_dictionary_keys(self) -> None:
        for key in (
            "0xdeadbeef",
            r"C:\unsafe\fixture.json",
            "2026-08-27T12:34:56",
        ):
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                generator._validate_safe_tree({key: None})

    def test_inventory_loader_rejects_tampering(self) -> None:
        inventory = json.loads(INVENTORY_PATH.read_text(encoding="utf-8"))
        target = next(
            item
            for item in inventory["symbols"]
            if item["path"] == generator.SOURCE_PATH
            and item["symbol"] == "Unit.W_TO_KW"
        )
        target["symbol_hash"] = "sha256:" + ("f" * 64)
        path = self.temp_root / "tampered-inventory.json"
        path.write_text(json.dumps(inventory), encoding="utf-8")

        with self.assertRaises((RuntimeError, SystemExit)):
            generator.load_exact_inventory(
                path, generator.EXPECTED_UPSTREAM_COMMIT
            )


if __name__ == "__main__":
    unittest.main()

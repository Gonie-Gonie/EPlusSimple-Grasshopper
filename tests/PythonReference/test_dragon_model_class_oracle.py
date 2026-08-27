"""Fail-closed tests for the exact ``EnergyModel`` class oracle."""

from __future__ import annotations

from collections import Counter
import copy
import importlib.util
import math
import os
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
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
    / "generate_dragon_model_class_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-model-class-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_model_class_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load EnergyModel class generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_GENERATOR_BYTES = 42_980
EXPECTED_GENERATOR_SHA256 = (
    "sha256:083e815084afedac2e0fca455f1bae4a108986d3f06aa1e537269091216815eb"
)
EXPECTED_FIXTURE_BYTES = 34_711
EXPECTED_FIXTURE_SHA256 = (
    "sha256:9a5e00a585e983d4a753acb94c46307848d32020e8d3960f9ad8184ccb4cfa7a"
)
EXPECTED_CASES_SHA256 = (
    "sha256:ab27c0de1d256d0942a8db49523fe3ba3d6701ddd469684c2261818518f95a59"
)


class DragonModelClassOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-model-class-oracle-tests-"
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
    def case(value: dict[str, object], identifier: str) -> dict[str, object]:
        return next(item for item in value["cases"] if item["id"] == identifier)

    @staticmethod
    def rehash(value: dict[str, object]) -> None:
        value["fact_sha256"] = {
            case["id"]: generator.canonical_sha256(case["python"]["facts"])
            for case in value["cases"]
        }
        for case in value["cases"]:
            case["python"]["facts_sha256"] = value["fact_sha256"][case["id"]]
        value["case_sha256"] = generator.case_sha256(value["cases"])
        value["cases_sha256"] = generator.cases_sha256(value["cases"])

    def test_artifacts_and_every_hash_layer_are_exactly_pinned(self) -> None:
        value = self.fixture()
        generator_raw = GENERATOR_PATH.read_bytes()
        fixture_raw = FIXTURE_PATH.read_bytes()
        self.assertEqual(EXPECTED_GENERATOR_BYTES, len(generator_raw))
        self.assertEqual(
            EXPECTED_GENERATOR_SHA256, generator.sha256_file(GENERATOR_PATH)
        )
        self.assertEqual(EXPECTED_FIXTURE_BYTES, len(fixture_raw))
        self.assertEqual(EXPECTED_FIXTURE_SHA256, generator.sha256_file(FIXTURE_PATH))
        self.assertEqual(EXPECTED_CASES_SHA256, generator.EXPECTED_CASES_SHA256)
        self.assertEqual(EXPECTED_CASES_SHA256, value["cases_sha256"])
        self.assertEqual(generator.EXPECTED_FACT_SHA256, value["fact_sha256"])
        self.assertEqual(generator.EXPECTED_CASE_SHA256, value["case_sha256"])
        self.assertEqual(3, len(value["fact_sha256"]))
        self.assertEqual(3, len(value["case_sha256"]))
        self.assertTrue(fixture_raw.endswith(b"\n"))
        self.assertNotIn(b"\r\n", fixture_raw)
        self.assertEqual(
            generator.strict_json_dumps(value, indent=2) + "\n",
            fixture_raw.decode("utf-8"),
        )

    def test_inventory_pins_exact_target_context_resolved_and_source_receipts(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_inventory(), inventory)
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual([815], [item["inventory_index"] for item in inventory["target_receipts"]])
        self.assertEqual(
            [556, 558],
            [item["inventory_index"] for item in inventory["context_receipts"]],
        )
        self.assertEqual(
            list(range(816, 826)),
            [item["inventory_index"] for item in inventory["resolved_receipts"]],
        )
        upstream = self.fixture()["upstream"]
        self.assertEqual(8_247, upstream["model_source"]["bytes"])
        self.assertEqual(generator.MODEL_SOURCE_SHA256, upstream["model_source"]["source_sha256"])
        self.assertEqual(generator.MODEL_AST_SHA256, upstream["model_source"]["ast_sha256"])
        self.assertEqual(
            generator._expected_loaded_local_modules(),
            upstream["loaded_local_modules"],
        )

    def test_scope_targets_only_index_815_and_never_retargets_named_members(self) -> None:
        value = self.fixture()
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(3, len(set(identifiers)))
        self.assertEqual(
            Counter({"EnergyModel": 3}),
            Counter(
                symbol
                for definition in definitions
                for symbol in definition["target_symbols"]
            ),
        )
        self.assertEqual(("EnergyModel",), generator.TARGET_SYMBOLS)
        self.assertFalse(
            set(generator.RESOLVED_SYMBOLS).intersection(generator.TARGET_SYMBOLS)
        )
        self.assertEqual(
            list(generator.RESOLVED_RECEIPTS),
            value["consumer_contract"]["closure"][
                "resolved_receipts_not_retargeted"
            ],
        )
        self.assertTrue(
            value["consumer_contract"]["closure"]["target_coverage_complete"]
        )
        self.assertFalse(value["consumer_contract"]["closure"]["full_symbol_closure"])

    def test_contract_has_exact_single_exception_adaptation_and_assertion(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {
                "EnergyModel": (
                    "sealed-read-only-native-energy-model-class-a7582a41"
                )
            },
            contract["adaptations"],
        )
        self.assertEqual(
            {"EnergyModel": "dragon-model-energy-model-class-a7582a41"},
            contract["assertion_ids"],
        )
        self.assertEqual({"EnergyModel": "exception"}, contract["classifications"])
        self.assertEqual({"equivalent": 0, "exception": 1}, contract["classification_counts"])
        self.assertEqual(
            {"EnergyModel": "GonieGonie.InvisibleDragon.Model.EnergyModel"},
            contract["native_targets"],
        )
        self.assertEqual(["EnergyModel"], contract["target_symbols"])
        for case in self.fixture()["cases"]:
            self.assertEqual(["EnergyModel"], case["target_symbols"])
            self.assertEqual(
                [generator.ADAPTATION], case["expected_dotnet"]["adaptations"]
            )

    def test_c01_pins_class_and_supported_versions_topology(self) -> None:
        facts = self.case(self.fixture(), generator.EXPECTED_CASE_IDS[0])["python"][
            "facts"
        ]
        self.assertEqual(
            {
                "direct_base_names": ["object"],
                "metaclass_name": "type",
                "module": "idragon.dragon.model",
                "name": "EnergyModel",
                "qualname": "EnergyModel",
            },
            facts["class_topology"],
        )
        supported = facts["supported_versions"]
        self.assertEqual("list", supported["container_type"])
        self.assertEqual({"kind": "int", "value": "1"}, supported["count"])
        self.assertTrue(supported["class_dictionary_contains_name"])
        self.assertTrue(supported["class_dictionary_value_is_read_value"])
        self.assertEqual(
            [
                {"kind": "int", "value": "24"},
                {"kind": "int", "value": "2"},
                {"kind": "int", "value": "0"},
            ],
            supported["items"][0]["components"],
        )
        self.assertEqual(
            [
                "supported_versions",
                "surfaces",
                "used_constructions",
                "used_layers",
                "used_profiles",
                "conditioned_zones",
                "unconditioned_zones",
                "create_default_idf",
                "add_supply_system",
                "to_idf",
            ],
            [item["name"] for item in facts["declared_public_member_topology"]],
        )

    def test_c02_pins_shared_append_visibility_and_finally_restoration(self) -> None:
        facts = self.case(self.fixture(), generator.EXPECTED_CASE_IDS[1])["python"][
            "facts"
        ]
        mutation = facts["mutation"]
        self.assertEqual({"kind": "int", "value": "1"}, facts["before"]["count"])
        self.assertEqual({"kind": "int", "value": "2"}, mutation["class_count"])
        self.assertEqual({"kind": "int", "value": "2"}, mutation["instance_count"])
        self.assertEqual({"kind": "int", "value": "2"}, mutation["subclass_count"])
        self.assertTrue(mutation["appended_item_identity_preserved"])
        self.assertTrue(mutation["class_read_is_original_container"])
        self.assertTrue(mutation["instance_read_is_original_container"])
        self.assertTrue(mutation["subclass_read_is_original_container"])
        self.assertEqual(
            ["24", "25"],
            [item["components"][0]["value"] for item in mutation["visible_items"]],
        )
        restoration = facts["restoration"]
        self.assertTrue(restoration["class_read_is_original_container"])
        self.assertTrue(restoration["contents_equal_by_identity"])
        self.assertEqual({"kind": "int", "value": "1"}, restoration["count"])

    def test_c03_pins_shadow_arbitrary_attributes_and_subclassability(self) -> None:
        facts = self.case(self.fixture(), generator.EXPECTED_CASE_IDS[2])["python"][
            "facts"
        ]
        instance = facts["instance_topology"]
        self.assertTrue(instance["created_without_constructor"])
        self.assertTrue(instance["instance_is_energy_model"])
        self.assertEqual(["review_marker", "supported_versions"], instance["arbitrary_attribute_names"])
        self.assertEqual("tuple", instance["arbitrary_attribute_type"])
        self.assertFalse(instance["shadow_is_class_container"])
        self.assertTrue(instance["shadow_is_input_container"])
        self.assertTrue(instance["class_supported_versions_unchanged"])
        subclass = facts["subclass_topology"]
        self.assertEqual("returned", subclass["subclass_definition_outcome"])
        self.assertEqual(["EnergyModel"], subclass["direct_base_names"])
        self.assertEqual(
            ["EnergyModelSubclassTopologyProbe", "EnergyModel", "object"],
            subclass["mro_names"],
        )
        self.assertTrue(subclass["base_instance_check"])
        self.assertTrue(subclass["inherited_supported_versions_is_class_container"])

    @unittest.skipUnless(
        all(
            (
                PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")
            ).is_file()
            for source in generator.SOURCE_SPECS
        )
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_direct_build_regenerates_twice_byte_identically(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        first = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE_ROOT
        )
        second = generator.build_oracle(
            inventory, generator.EXPECTED_UPSTREAM_COMMIT, PINNED_SOURCE_ROOT
        )
        first_bytes = (generator.strict_json_dumps(first, indent=2) + "\n").encode(
            "utf-8"
        )
        second_bytes = (generator.strict_json_dumps(second, indent=2) + "\n").encode(
            "utf-8"
        )
        self.assertEqual(first_bytes, second_bytes)
        self.assertEqual(FIXTURE_PATH.read_bytes(), first_bytes)

    @unittest.skipUnless(
        all(
            (
                PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")
            ).is_file()
            for source in generator.SOURCE_SPECS
        )
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_bootstrap_cli_regenerates_twice_byte_identically(self) -> None:
        bootstrap = (
            REPOSITORY_ROOT
            / "tools"
            / "python-reference"
            / "bootstrap_reference.py"
        )
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
                    "-B",
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

    @unittest.skipUnless(
        PINNED_SOURCE_ROOT.is_dir() and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_loaded_local_module_without_receipt_fails_closed(self) -> None:
        with self.assertRaisesRegex(SystemExit, "lacks an exact receipt"):
            with generator.SUPPORT._pinned_modules(PINNED_SOURCE_ROOT) as modules:
                imported_root = Path(modules.model.__file__).resolve().parents[2]
                rogue = imported_root / "idragon" / "energy_model_class_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.energy_model_class_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_schema_contract_runtime_source_receipt_and_semantic_tampering_fail(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        schema = self.fixture()
        schema["schema"] = "wrong"
        changes.append((schema, "schema"))

        case_contract = self.fixture()
        case_contract["cases"][0]["target_symbols"] = ["EnergyModel.__init__"]
        self.rehash(case_contract)
        changes.append((case_contract, "case contract"))

        contract = self.fixture()
        contract["consumer_contract"]["classifications"]["EnergyModel"] = "equivalent"
        changes.append((contract, "consumer contract"))

        runtime = self.fixture()
        runtime["runtime"]["python_version"] = "3.12.8"
        changes.append((runtime, "runtime"))

        source = self.fixture()
        source["upstream"]["model_source"]["bytes"] = 1
        changes.append((source, "upstream"))

        loaded = self.fixture()
        loaded["upstream"]["loaded_local_modules"][0]["module"] = "idragon.wrong"
        changes.append((loaded, "upstream"))

        target = self.fixture()
        target["target_receipts"][0]["inventory_index"] = 816
        changes.append((target, "target receipt"))

        context = self.fixture()
        context["context_receipts"][0]["inventory_index"] = 0
        changes.append((context, "context receipts"))

        resolved = self.fixture()
        resolved["resolved_receipts"][0]["inventory_index"] = 815
        changes.append((resolved, "resolved receipts"))

        semantic = self.fixture()
        semantic["cases"][1]["python"]["facts"]["mutation"]["class_count"] = {
            "kind": "int",
            "value": "3",
        }
        self.rehash(semantic)
        changes.append((semantic, "canonical semantics"))

        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_case_and_duplicate_json_keys_fail_closed(self) -> None:
        stale = self.fixture()
        stale["cases"][0]["python"]["facts"]["class_topology"]["name"] = "Wrong"
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale)

        duplicate_case = self.fixture()
        duplicate_case["cases"][1]["id"] = duplicate_case["cases"][0]["id"]
        duplicate_case["cases"][1]["python"]["facts_sha256"] = duplicate_case[
            "cases"
        ][0]["python"]["facts_sha256"]
        duplicate_case["cases"][1]["python"]["facts"] = copy.deepcopy(
            duplicate_case["cases"][0]["python"]["facts"]
        )
        self.rehash(duplicate_case)
        with self.assertRaisesRegex(RuntimeError, "order/count"):
            generator.validate_oracle(duplicate_case)

        duplicate_json = self.temp_root / "duplicate.json"
        duplicate_json.write_text(
            '{"schema":"first","schema":"second"}\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.load_json_without_duplicates(duplicate_json)

    def test_safe_tree_rejects_nonstring_keys_paths_addresses_guids_and_timestamps(self) -> None:
        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            (r"C:\private\energy-model-class.json", "Absolute path"),
            ("/home/private/energy-model-class.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-27T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = unsafe
            self.rehash(changed)
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        finite = self.fixture()
        finite["cases"][0]["python"]["facts"]["unsafe"] = 1.25
        self.rehash(finite)
        with self.assertRaisesRegex(RuntimeError, "Raw float"):
            generator.validate_oracle(finite)

        path_value = self.fixture()
        path_value["consumer_contract"]["unsafe"] = Path("relative-host-path")
        with self.assertRaisesRegex(RuntimeError, "Raw path"):
            generator.validate_oracle(path_value)

        nonstring = self.fixture()
        nonstring["consumer_contract"][7] = "not-a-string-key"
        with self.assertRaisesRegex(RuntimeError, "Non-string JSON key"):
            generator.validate_oracle(nonstring)

    def test_json_and_in_memory_nonfinite_values_fail_closed(self) -> None:
        for index, token in enumerate(("NaN", "Infinity", "-Infinity")):
            malformed = self.temp_root / f"nonfinite-{index}.json"
            malformed.write_text('{"value":' + token + "}\n", encoding="utf-8")
            with self.subTest(token=token):
                with self.assertRaisesRegex(SystemExit, "forbidden non-finite"):
                    generator.load_json_without_duplicates(malformed)

        for value in (float("nan"), float("inf"), float("-inf")):
            self.assertFalse(math.isfinite(value))
            malformed = self.fixture()
            malformed["cases"][0]["python"]["facts"]["unsafe"] = value
            with self.subTest(value=repr(value)):
                with self.assertRaises(ValueError):
                    generator.validate_oracle(malformed)


if __name__ == "__main__":
    unittest.main()

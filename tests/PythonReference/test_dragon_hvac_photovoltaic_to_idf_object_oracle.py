"""Fail-closed tests for the bounded photovoltaic IDF oracle."""

from __future__ import annotations

from collections import Counter
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
    / "generate_dragon_hvac_photovoltaic_to_idf_object_oracle.py"
)
INVENTORY_PATH = REPOSITORY_ROOT / "upstream" / "public-symbol-inventory.json"
FIXTURE_PATH = (
    REPOSITORY_ROOT
    / "fixtures"
    / "reference"
    / "python-0.7.0"
    / "dragon-hvac-photovoltaic-to-idf-object-oracle.json"
)
PINNED_SOURCE_ROOT = (
    REPOSITORY_ROOT / "temp" / "reference" / "upstream" / "eplussimple" / "src"
)

spec = importlib.util.spec_from_file_location(
    "generate_dragon_hvac_photovoltaic_to_idf_object_oracle", GENERATOR_PATH
)
if spec is None or spec.loader is None:
    raise RuntimeError(f"Cannot load photovoltaic generator: {GENERATOR_PATH}")
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)

EXPECTED_FIXTURE_BYTES = 147_261
EXPECTED_FIXTURE_SHA256 = (
    "sha256:07c383c316989ccb22ac3eadcf9d8388764f76effbbf03c13b7a54f8af20f22b"
)
EXPECTED_CASES_SHA256 = (
    "sha256:767c3314ec20d07aa12fdce48b9969a98b54b835855b4be7ecfdd896816be0dd"
)


class DragonHvacPhotovoltaicToIdfObjectOracleTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="dragon-hvac-photovoltaic-to-idf-object-tests-"
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
    def record(case: dict[str, object], object_type: str) -> dict[str, object]:
        records = case["python"]["facts"]["emission"]["first_object_records"]
        return next(item for item in records if item["object_type"] == object_type)

    @staticmethod
    def encoded_field(record: dict[str, object], field_name: str) -> dict[str, object]:
        fields = record["ordered_fields"]
        return next(item["value"] for item in fields if item["name"] == field_name)

    @staticmethod
    def decode(value: dict[str, object]) -> object:
        kind = value["kind"]
        if kind == "none":
            return None
        if kind == "bool":
            return value["value"]
        if kind == "int":
            return int(value["value"])
        if kind == "float":
            decoded = float.fromhex(value["hex"])
            if repr(decoded) != value["repr"]:
                raise AssertionError("Encoded float repr and hex disagree.")
            return decoded
        if kind == "str":
            return value["value"]
        raise AssertionError(f"Unexpected encoded kind: {kind}")

    def test_fixture_is_exact_utf8_strict_and_self_validating(self) -> None:
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

    def test_inventory_binds_twelve_loaded_sources_and_exact_symbol(self) -> None:
        inventory = generator.load_exact_inventory(
            INVENTORY_PATH, generator.EXPECTED_UPSTREAM_COMMIT
        )
        self.assertEqual(generator._expected_files(), inventory["files"])
        self.assertEqual(
            generator._expected_symbol_descriptors(), inventory["symbols"]
        )
        self.assertEqual(12, len(inventory["files"]))
        self.assertEqual(("PhotoVoltaicPanel.to_idf_object",), generator.TARGET_SYMBOLS)

        value = self.fixture()
        loaded = value["upstream"]["loaded_local_modules"]
        self.assertEqual(12, len(loaded))
        self.assertEqual(generator._expected_loaded_local_modules(), loaded)
        self.assertEqual(
            [item["path"] for item in inventory["files"]],
            [item["path"] for item in loaded],
        )
        self.assertEqual(
            [
                {
                    "body_hash": "sha256:a227ed7b60c5a482a11b9a11f36e243b56cae95e2889effe9abe7e6e70d0346b",
                    "kind": "function",
                    "path": "src/idragon/dragon/hvac.py",
                    "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
                    "symbol": "PhotoVoltaicPanel.to_idf_object",
                    "symbol_hash": "sha256:4723273d4b77d9286d4a47c4d753f71049e87d146ff912b0aa6a8ab8ed911287",
                }
            ],
            value["symbols"],
        )

    def test_cases_are_exact_sorted_and_bind_compact_native_adaptation(self) -> None:
        definitions = generator.case_definitions()
        identifiers = tuple(item["id"] for item in definitions)
        self.assertEqual(generator.EXPECTED_CASE_IDS, identifiers)
        self.assertEqual(tuple(sorted(identifiers)), identifiers)
        self.assertEqual(3, len(identifiers))
        self.assertEqual(3, len(set(identifiers)))
        self.assertEqual(
            generator.EXPECTED_CASE_COUNTS,
            dict(Counter(item["symbol"] for item in definitions)),
        )
        for definition in definitions:
            self.assertEqual("photovoltaic-to-idf-object", definition["executor"])
            self.assertEqual("PhotoVoltaicPanel.to_idf_object", definition["symbol"])
            self.assertEqual(
                {
                    "adaptation": "compact-native-photovoltaic-idf-emission",
                    "outcome": "returned",
                },
                definition["expected_dotnet"],
            )

    def test_consumer_contract_bounds_compact_native_leaf_emission(self) -> None:
        contract = self.fixture()["consumer_contract"]
        self.assertEqual(
            {
                "PhotoVoltaicPanel.to_idf_object": (
                    "compact-native-photovoltaic-idf-emission"
                )
            },
            contract["adaptations"],
        )
        self.assertEqual(
            {
                "PhotoVoltaicPanel.to_idf_object": (
                    "dragon-hvac-photovoltaic-to-idf-object-4723273d"
                )
            },
            contract["assertion_ids"],
        )
        self.assertEqual(
            {"PhotoVoltaicPanel.to_idf_object": "exception"},
            contract["classifications"],
        )
        self.assertEqual(
            {
                "PhotoVoltaicPanel.to_idf_object": (
                    "PhotovoltaicPanel.ToIdfObjects"
                )
            },
            contract["native_targets"],
        )
        closure = contract["closure"]
        self.assertFalse(closure["full_symbol_closure"])
        self.assertEqual(
            "bounded-common-valid-domain-compact-native-photovoltaic-idf-emission-adaptation",
            closure["scope"],
        )
        self.assertEqual(
            {
                "native_compact_field_counts": [8, 4, 4, 4, 5, 8],
                "native_policy": "omit-trailing-blank-and-default-fields",
                "python_complete_allowed_key_field_counts": [8, 5, 7, 151, 5, 21],
            },
            closure["representation_contract"],
        )
        self.assertIn(
            "semantic-populated-and-default-field-parity-requires-csharp-evidence",
            closure["unresolved_behavior"],
        )
        for unresolved in (
            "PhotoVoltaicPanel.__init__",
            "PhotoVoltaicPanel.area",
            "PhotoVoltaicPanel.tilt",
            "photovoltaic-constructor-validation-order-and-errors",
            "photovoltaic-property-setter-validation-order-and-errors",
            "invalid-or-nonfinite-domain-state",
        ):
            self.assertTrue(
                unresolved in closure["context_only_not_targeted"]
                or unresolved in closure["unresolved_behavior"]
            )
        self.assertEqual(["PhotoVoltaicPanel.to_idf_object"], contract["target_symbols"])
        self.assertNotIn("PhotoVoltaicPanel", contract["classifications"])

    def test_every_case_returns_fresh_complete_six_object_family_sequence(self) -> None:
        value = self.fixture()
        expected_counts = [8, 5, 7, 151, 5, 21]
        for identifier in generator.EXPECTED_CASE_IDS:
            with self.subTest(case=identifier):
                case = self.case(value, identifier)
                facts = case["python"]["facts"]
                self.assertEqual("returned", case["python"]["outcome"])
                self.assertTrue(
                    facts["constructor_context"][
                        "state_unchanged_after_two_emissions"
                    ]
                )
                self.assertTrue(
                    facts["constructor_context"]["explicit_input_identity_preserved"]
                )
                emission = facts["emission"]
                self.assertEqual(6, emission["object_count"])
                self.assertEqual(list(generator.OBJECT_TYPES), emission["object_types"])
                self.assertEqual("list", emission["result_type"])
                self.assertTrue(emission["fresh_result_list"])
                self.assertTrue(emission["first_objects_pairwise_distinct"])
                self.assertTrue(emission["second_objects_pairwise_distinct"])
                self.assertEqual([True] * 6, emission["fresh_idf_object_flags"])
                self.assertEqual([True] * 6, emission["same_idd_definition_flags"])
                self.assertEqual([True] * 6, emission["second_fields_equal_flags"])
                self.assertTrue(emission["all_allowed_fields_covered_in_order"])
                records = emission["first_object_records"]
                self.assertEqual(expected_counts, [item["field_count"] for item in records])
                self.assertEqual(
                    expected_counts,
                    [len(item["ordered_fields"]) for item in records],
                )
                for record in records:
                    self.assertEqual(
                        record["field_count"],
                        len({field["name"] for field in record["ordered_fields"]}),
                    )

    def test_default_ratio_and_maximum_tilt_preserve_complete_linkage(self) -> None:
        value = self.fixture()
        case = self.case(value, generator.EXPECTED_CASE_IDS[0])
        constructor = case["python"]["facts"]["constructor_context"]
        self.assertTrue(constructor["used_default_effective_area_ratio"])
        self.assertEqual(
            [
                "name",
                "area",
                "tilt",
                "azimuth",
                "efficiency",
                "effective_area_ratio",
            ],
            constructor["parameter_order"],
        )
        self.assertEqual(["effective_area_ratio"], constructor["keyword_only_parameters"])

        shading = self.record(case, "Shading:Site")
        performance = self.record(case, "PhotovoltaicPerformance:Simple")
        generator_record = self.record(case, "Generator:Photovoltaic")
        distribution = self.record(case, "ElectricLoadCenter:Distribution")
        self.assertEqual(
            90.0,
            self.decode(self.encoded_field(shading, "Tilt Angle")),
        )
        self.assertEqual(2.5, self.decode(self.encoded_field(shading, "Length")))
        self.assertEqual(2.5, self.decode(self.encoded_field(shading, "Height")))
        self.assertEqual(
            0.7,
            self.decode(
                self.encoded_field(
                    performance,
                    "Fraction of Surface Area with Active Solar Cells",
                )
            ),
        )
        self.assertEqual(
            "Shading4PVpanel:Default Ratio PV",
            self.decode(self.encoded_field(generator_record, "Surface Name")),
        )
        self.assertEqual(
            "Inverter4PVpanel:Default Ratio PV",
            self.decode(self.encoded_field(distribution, "Inverter Name")),
        )

    def test_minimum_angles_unit_limits_and_custom_sqrt_are_exact(self) -> None:
        value = self.fixture()
        boundary = self.case(value, generator.EXPECTED_CASE_IDS[1])
        boundary_shading = self.record(boundary, "Shading:Site")
        boundary_performance = self.record(
            boundary, "PhotovoltaicPerformance:Simple"
        )
        self.assertEqual(
            0.0,
            self.decode(self.encoded_field(boundary_shading, "Tilt Angle")),
        )
        self.assertEqual(
            0.0,
            self.decode(self.encoded_field(boundary_shading, "Azimuth Angle")),
        )
        self.assertEqual(
            1.0,
            self.decode(
                self.encoded_field(
                    boundary_performance,
                    "Fraction of Surface Area with Active Solar Cells",
                )
            ),
        )
        self.assertEqual(
            1.0,
            self.decode(
                self.encoded_field(
                    boundary_performance, "Value for Cell Efficiency if Fixed"
                )
            ),
        )

        custom = self.case(value, generator.EXPECTED_CASE_IDS[2])
        custom_shading = self.record(custom, "Shading:Site")
        custom_performance = self.record(custom, "PhotovoltaicPerformance:Simple")
        side = self.decode(self.encoded_field(custom_shading, "Length"))
        self.assertEqual(1.4142135623730951, side)
        self.assertEqual(math.sqrt(2.0), side)
        self.assertEqual(
            side, self.decode(self.encoded_field(custom_shading, "Height"))
        )
        self.assertEqual(
            0.625,
            self.decode(
                self.encoded_field(
                    custom_performance,
                    "Fraction of Surface Area with Active Solar Cells",
                )
            ),
        )
        self.assertFalse(
            custom["python"]["facts"]["constructor_context"][
                "used_default_effective_area_ratio"
            ]
        )

    @unittest.skipUnless(
        all(
            (PINNED_SOURCE_ROOT / Path(source["path"]).relative_to("src")).is_file()
            for source in generator.SOURCE_SPECS
        )
        and DEPENDENCY_ROOT.is_dir(),
        "pinned reference environment unavailable",
    )
    def test_generation_is_byte_identical_twice_and_matches_fixture(self) -> None:
        bootstrap = (
            REPOSITORY_ROOT / "tools" / "python-reference" / "bootstrap_reference.py"
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
                imported_root = Path(modules.hvac.__file__).resolve().parents[2]
                rogue = imported_root / "idragon" / "review_probe.py"
                rogue.write_text("VALUE = 1\n", encoding="utf-8", newline="\n")
                sys.modules["idragon.review_probe"] = SimpleNamespace(
                    __file__=str(rogue)
                )

    def test_schema_contract_case_runtime_source_symbol_and_semantics_tamper_fail(self) -> None:
        changes: list[tuple[dict[str, object], str]] = []
        schema = self.fixture()
        schema["schema"] = "wrong"
        changes.append((schema, "schema"))
        contract = self.fixture()
        contract["consumer_contract"]["closure"]["full_symbol_closure"] = True
        changes.append((contract, "consumer contract"))
        case = self.fixture()
        case["cases"][0]["executor"] = "wrong"
        case["cases_sha256"] = generator.cases_sha256(case["cases"])
        changes.append((case, "case contract"))
        adaptation = self.fixture()
        adaptation["cases"][0]["expected_dotnet"]["adaptation"] = "wrong"
        adaptation["cases_sha256"] = generator.cases_sha256(adaptation["cases"])
        changes.append((adaptation, "case contract"))
        runtime = self.fixture()
        runtime["runtime"]["python_version"] = "3.12.8"
        changes.append((runtime, "runtime"))
        source = self.fixture()
        source["upstream"]["sources"][5]["source_sha256"] = "sha256:" + "0" * 64
        changes.append((source, "upstream"))
        loaded = self.fixture()
        loaded["upstream"]["loaded_local_modules"][5]["module"] = "idragon.wrong"
        changes.append((loaded, "upstream"))
        symbol = self.fixture()
        symbol["symbols"][0]["symbol_hash"] = "sha256:" + "0" * 64
        changes.append((symbol, "symbol"))
        semantic = self.fixture()
        semantic["cases"][0]["python"]["facts"]["emission"]["object_count"] = 5
        semantic["cases_sha256"] = generator.cases_sha256(semantic["cases"])
        changes.append((semantic, "semantics"))
        for malformed, message in changes:
            with self.subTest(message=message):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(malformed)

    def test_stale_hash_duplicate_keys_unsafe_values_and_nonfinite_fail(self) -> None:
        stale = self.fixture()
        stale["cases"][0]["python"]["facts"]["emission"]["object_count"] = 5
        with self.assertRaisesRegex(RuntimeError, "cases hash"):
            generator.validate_oracle(stale)

        duplicate = self.temp_root / "duplicate.json"
        duplicate.write_text(
            '{"schema":"first","schema":"second"}\n', encoding="utf-8"
        )
        with self.assertRaisesRegex(SystemExit, "duplicate key"):
            generator.load_json_without_duplicates(duplicate)

        unsafe_values = (
            ("0x123456789abcdef0", "address"),
            ("C:\\private\\photovoltaic.json", "Absolute path"),
            ("/home/private/photovoltaic.json", "Absolute path"),
            ("12345678-1234-4123-8123-123456789abc", "GUID"),
            ("2026-08-27T12:34:56", "Timestamp"),
        )
        for unsafe, message in unsafe_values:
            changed = self.fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = unsafe
            changed["cases_sha256"] = generator.cases_sha256(changed["cases"])
            with self.subTest(value=unsafe):
                with self.assertRaisesRegex(RuntimeError, message):
                    generator.validate_oracle(changed)

        for nonfinite in (float("nan"), float("inf"), float("-inf")):
            self.assertFalse(math.isfinite(nonfinite))
            changed = self.fixture()
            changed["cases"][0]["python"]["facts"]["unsafe"] = nonfinite
            with self.subTest(value=repr(nonfinite)):
                with self.assertRaisesRegex(ValueError, "Out of range float"):
                    generator.validate_oracle(changed)


if __name__ == "__main__":
    unittest.main()

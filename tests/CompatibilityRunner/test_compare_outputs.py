from __future__ import annotations

import copy
import unittest

from support import TemporaryWorkspace, manifest, metadata, runner


def compare_schedule_compact(
    expected_fields: list[str],
    actual_fields: list[str],
) -> dict[str, object]:
    def render(fields: list[str]) -> str:
        return "Schedule:Compact,\n  " + ",\n  ".join(fields) + ";\n"

    with TemporaryWorkspace() as workspace:
        expected = workspace.write_text("expected.idf", render(expected_fields))
        actual = workspace.write_text("actual.idf", render(actual_fields))
        return runner.compare_idf(expected, actual, 1e-9, 1e-9)


class IdfParsingAndComparisonTests(unittest.TestCase):
    def test_parser_preserves_quoted_comments_delimiters_and_escaped_quotes(self) -> None:
        with TemporaryWorkspace() as workspace:
            source = workspace.write_text(
                "quoted.idf",
                '''! Entire leading comment
Material,
  "Name, with; delimiters! and ""quote""", ! outside comment
  0.1; ! trailing comment

Schedule:Compact,
  Office,
  "Through: 12/31; ! literal";
! comment at end without a record''',
            )

            objects = runner.parse_idf(source)

        self.assertEqual(
            [["Name, with; delimiters! and \"quote\"", "0.1"]],
            objects["material"],
        )
        self.assertEqual(
            [["Office", "Through: 12/31; ! literal"]],
            objects["schedule:compact"],
        )

    def test_object_comparison_is_independent_of_type_and_object_order(self) -> None:
        with TemporaryWorkspace() as workspace:
            expected = workspace.write_text(
                "expected.idf",
                "Material, Alpha, Rough, 0.1000;\nMaterial, Beta, Smooth, 0.2;\nZone, Main;\n",
            )
            actual = workspace.write_text(
                "actual.idf",
                "Zone, main;\nMaterial, beta, smooth, 2e-1;\nMaterial, alpha, rough, 0.1;\n",
            )

            result = runner.compare_idf(expected, actual, 1e-9, 1e-9)

        self.assertTrue(result["passed"], result["mismatches"])
        self.assertEqual(3, result["expected_object_count"])
        self.assertEqual(3, result["matched_object_count"])

    def test_numeric_fields_use_absolute_and_relative_tolerance(self) -> None:
        self.assertTrue(runner.numeric_equal(0.0, 0.0009, 0.001, 0.0))
        self.assertFalse(runner.numeric_equal(0.0, 0.0011, 0.001, 0.0))
        self.assertTrue(runner.numeric_equal(1000.0, 1000.5, 0.1, 0.001))
        self.assertFalse(runner.numeric_equal(1000.0, 1002.0, 0.1, 0.001))
        self.assertTrue(runner.numeric_equal(1000.0, 1001.05, 0.1, 0.001))

        with TemporaryWorkspace() as workspace:
            expected = workspace.write_text("expected.idf", "Value, 0, 1000;\n")
            actual = workspace.write_text("actual.idf", "Value, 0.0009, 1000.5;\n")
            result = runner.compare_idf(expected, actual, 0.001, 0.001)

        self.assertTrue(result["passed"], result["mismatches"])

    def test_object_count_difference_is_reported_by_type(self) -> None:
        with TemporaryWorkspace() as workspace:
            expected = workspace.write_text(
                "expected.idf",
                "Material, Alpha;\nMaterial, Beta;\n",
            )
            actual = workspace.write_text("actual.idf", "Material, Alpha;\n")

            result = runner.compare_idf(expected, actual, 0.0, 0.0)

        self.assertFalse(result["passed"])
        self.assertEqual(2, result["expected_object_count"])
        self.assertEqual(1, result["actual_object_count"])
        self.assertIn(
            {
                "path": "$.material",
                "reason": "object_count",
                "expected": 2,
                "actual": 1,
            },
            result["mismatches"],
        )


class IddDefaultNormalizationTests(unittest.TestCase):
    IDD = r"""Test:Object,
  A1, \field Name
  A2, \field Mode
      \default Auto
  A3; \field Note
"""

    def compare_with_idd(self, expected_text: str, actual_text: str) -> dict[str, object]:
        with TemporaryWorkspace() as workspace:
            idd = workspace.write_text("Energy+.idd", self.IDD)
            expected = workspace.write_text("expected.idf", expected_text)
            actual = workspace.write_text("actual.idf", actual_text)
            schema = runner.parse_idd(idd)
            return runner.compare_idf(expected, actual, 0.0, 0.0, schema)

    def test_omitted_or_blank_default_equals_explicit_default(self) -> None:
        for representation in ("Test:Object, Item;\n", "Test:Object, Item,;\n"):
            with self.subTest(representation=representation):
                result = self.compare_with_idd(
                    representation,
                    "Test:Object, Item, Auto;\n",
                )
                self.assertTrue(result["passed"], result["mismatches"])

    def test_explicit_nondefault_still_fails(self) -> None:
        result = self.compare_with_idd(
            "Test:Object, Item;\n",
            "Test:Object, Item, Manual;\n",
        )

        self.assertFalse(result["passed"])
        self.assertEqual("$.test:object[0].fields[1]", result["mismatches"][0]["path"])

    def test_trailing_blank_without_default_equals_omission(self) -> None:
        result = self.compare_with_idd(
            "Test:Object, Item, Auto;\n",
            "Test:Object, Item, Auto,;\n",
        )

        self.assertTrue(result["passed"], result["mismatches"])

    def test_trailing_blank_does_not_hide_a_later_nonblank_field(self) -> None:
        result = self.compare_with_idd(
            "Test:Object, Item;\n",
            "Test:Object, Item,, Note;\n",
        )

        self.assertFalse(result["passed"])
        self.assertEqual("$.test:object[0].fields[2]", result["mismatches"][0]["path"])
        self.assertEqual("<omitted>", result["mismatches"][0]["expected"])
        self.assertEqual("Note", result["mismatches"][0]["actual"])

    def test_extensible_group_blank_is_not_treated_as_an_omitted_fixed_field(self) -> None:
        extensible_idd = r"""Extensible:Object,
  \extensible:1
  A1, \field Name
  A2; \field Item
      \begin-extensible
"""
        with TemporaryWorkspace() as workspace:
            idd = workspace.write_text("Energy+.idd", extensible_idd)
            expected = workspace.write_text("expected.idf", "Extensible:Object, Item;\n")
            actual = workspace.write_text("actual.idf", "Extensible:Object, Item,;\n")
            schema = runner.parse_idd(idd)
            result = runner.compare_idf(expected, actual, 0.0, 0.0, schema)

        self.assertFalse(result["passed"])
        self.assertEqual(
            "$.extensible:object[0].fields[1]",
            result["mismatches"][0]["path"],
        )

    def test_partial_extensible_group_may_omit_trailing_optional_fields(self) -> None:
        extensible_idd = r"""ElectricLoadCenter:Generators,
  \extensible:5
  A1, \field Name
  A2, \field Generator Name
      \begin-extensible
  A3, \field Generator Object Type
  N1, \field Rated Electric Power Output
  A4, \field Availability Schedule Name
  N2; \field Rated Thermal to Electrical Power Ratio
"""
        with TemporaryWorkspace() as workspace:
            idd = workspace.write_text("Energy+.idd", extensible_idd)
            expected = workspace.write_text(
                "expected.idf",
                "ElectricLoadCenter:Generators, List, PV, Generator:Photovoltaic, 1000000;\n",
            )
            actual = workspace.write_text(
                "actual.idf",
                "ElectricLoadCenter:Generators, List, PV, Generator:Photovoltaic, 1000000,,;\n",
            )
            schema = runner.parse_idd(idd)
            result = runner.compare_idf(expected, actual, 0.0, 0.0, schema)

        self.assertTrue(result["passed"], result["mismatches"])

    def test_malformed_idd_raises_instead_of_guessing(self) -> None:
        malformed = r"""Test:Object,
  A1, \field Name
      \default First
      \default Second
  A2; \field Mode
"""
        with TemporaryWorkspace() as workspace:
            idd = workspace.write_text("malformed.idd", malformed)
            with self.assertRaises(runner.IddParseError):
                runner.parse_idd(idd)

    def test_extensible_declaration_without_begin_marker_raises(self) -> None:
        malformed = r"""Extensible:Object,
  \extensible:1
  A1; \field Name
"""
        with TemporaryWorkspace() as workspace:
            idd = workspace.write_text("malformed-extensible.idd", malformed)
            with self.assertRaises(runner.IddParseError):
                runner.parse_idd(idd)

    def test_unknown_object_type_does_not_borrow_or_invent_defaults(self) -> None:
        result = self.compare_with_idd(
            "Unknown:Object, Item;\n",
            "Unknown:Object, Item, Auto;\n",
        )

        self.assertFalse(result["passed"])
        self.assertEqual("<omitted>", result["mismatches"][0]["expected"])


class ScheduleCompactComparisonTests(unittest.TestCase):
    def test_grouped_days_and_date_separators_compare_semantically(self) -> None:
        expected = [
            "Office",
            "ScheduleTypeLimits:Fraction",
            "Through: 12/31",
            "For: Weekdays",
            "Interpolate: No",
            "Until: 24:00",
            "1",
            "For: Weekends",
            "Until: 24:00",
            "0",
            "For: AllOtherDays",
            "Until: 24:00",
            "0",
        ]
        actual = [
            "Office",
            "ScheduleTypeLimits:Fraction",
            "Through: 12-31",
            "For: Monday",
            "Until: 24:00",
            "1.0",
            "For: Tuesday",
            "Until: 24:00",
            "1.0",
            "For: Wednesday",
            "Until: 24:00",
            "1.0",
            "For: Thursday",
            "Until: 24:00",
            "1.0",
            "For: Friday",
            "Until: 24:00",
            "1.0",
            "For: Saturday",
            "Until: 24:00",
            "0.0",
            "For: Sunday",
            "Until: 24:00",
            "0.0",
            "For: Holiday SummerDesignDay WinterDesignDay CustomDay1 CustomDay2",
            "Until: 24:00",
            "0.0",
        ]

        result = compare_schedule_compact(expected, actual)

        self.assertTrue(result["passed"], result["mismatches"])

    def test_all_other_days_is_the_complement_of_preceding_selectors(self) -> None:
        expected = [
            "Complement",
            "ScheduleTypeLimits:Fraction",
            "Through: 12/31",
            "For: Monday",
            "Until: 24:00",
            "1",
            "For: AllOtherDays",
            "Until: 24:00",
            "0",
        ]
        actual = [
            "Complement",
            "ScheduleTypeLimits:Fraction",
            "Through: 12-31",
            "For: Monday",
            "Until: 24:00",
            "1",
            (
                "For: Tuesday Wednesday Thursday Friday Saturday Sunday "
                "Holiday SummerDesignDay WinterDesignDay CustomDay1 CustomDay2"
            ),
            "Until: 24:00",
            "0",
        ]

        result = compare_schedule_compact(expected, actual)

        self.assertTrue(result["passed"], result["mismatches"])

    def test_changed_until_value_fails_semantic_comparison(self) -> None:
        expected = [
            "Changed value",
            "ScheduleTypeLimits:Fraction",
            "Through: 12/31",
            "For: AllDays",
            "Until: 12:00",
            "0",
            "Until: 24:00",
            "1",
        ]
        actual = expected[:-1] + ["0.9"]

        result = compare_schedule_compact(expected, actual)

        self.assertFalse(result["passed"])
        self.assertEqual(1, result["mismatch_count"])
        self.assertEqual(
            "$.schedule:compact[0].fields[2]",
            result["mismatches"][0]["path"],
        )

    def test_name_interpolation_or_type_limit_change_still_fails(self) -> None:
        baseline = [
            "Metadata",
            "ScheduleTypeLimits:Fraction",
            "Through: 12/31",
            "For: AllDays",
            "Interpolate: No",
            "Until: 24:00",
            "1",
        ]
        changes = {
            "name": (["Renamed"] + baseline[1:], 0),
            "interpolate": (
                baseline[:4] + ["Interpolate: Linear"] + baseline[5:],
                2,
            ),
            "type-limit": (
                baseline[:1] + ["ScheduleTypeLimits:OnOff"] + baseline[2:],
                1,
            ),
        }

        for label, (changed, expected_field) in changes.items():
            with self.subTest(label=label):
                result = compare_schedule_compact(baseline, changed)
                self.assertFalse(result["passed"])
                self.assertEqual(1, result["mismatch_count"])
                self.assertEqual(
                    f"$.schedule:compact[0].fields[{expected_field}]",
                    result["mismatches"][0]["path"],
                )

    def test_malformed_profile_falls_back_to_raw_fail_closed_comparison(self) -> None:
        expected = [
            "Malformed",
            "ScheduleTypeLimits:Fraction",
            "Through: 12/31",
            "For: Nonday",
            "Until: 24:00",
            "1",
        ]
        actual = expected.copy()
        actual[2] = "Through: 12-31"

        result = compare_schedule_compact(expected, actual)

        self.assertFalse(result["passed"])
        self.assertEqual(
            "$.schedule:compact[0].fields[2]",
            result["mismatches"][0]["path"],
        )

    def test_multiple_through_ranges_compare_semantically(self) -> None:
        expected = [
            "Seasonal",
            "ScheduleTypeLimits:Fraction",
            "Through: 6/30",
            "For: AllDays",
            "Until: 24:00",
            "0",
            "Through: 12/31",
            "For: AllDays",
            "Until: 24:00",
            "1",
        ]
        actual = [
            "Seasonal",
            "ScheduleTypeLimits:Fraction",
            "Through: 6-30",
            "For: Weekdays",
            "Until: 24:00",
            "0.0",
            "For: Weekends",
            "Until: 24:00",
            "0.0",
            "For: AllOtherDays",
            "Until: 24:00",
            "0.0",
            "Through: 12-31",
            "For: Weekdays",
            "Until: 24:00",
            "1.0",
            "For: Weekends",
            "Until: 24:00",
            "1.0",
            "For: AllOtherDays",
            "Until: 24:00",
            "1.0",
        ]

        result = compare_schedule_compact(expected, actual)

        self.assertTrue(result["passed"], result["mismatches"])


class EngineeringResultComparisonTests(unittest.TestCase):
    def test_recursive_numeric_comparison_reports_precise_grr_path(self) -> None:
        comparison = runner.JsonComparison(
            absolute=0.01,
            relative=0.001,
            near_zero=0.005,
        )

        comparison.compare(
            {"summary": {"monthly": [0.0, {"cooling": 100.0}]}},
            {"summary": {"monthly": [0.005, {"cooling": 103.0}]}},
        )
        result = comparison.result()

        self.assertFalse(result["passed"])
        self.assertEqual(2, result["compared_numbers"])
        self.assertEqual(3.0, result["max_absolute_error"])
        self.assertEqual("$.summary.monthly[1].cooling", result["max_error_path"])
        self.assertEqual(
            "$.summary.monthly[1].cooling",
            result["mismatches"][0]["path"],
        )
        self.assertEqual("numeric_tolerance", result["mismatches"][0]["reason"])


class WarningAndIdentityTests(unittest.TestCase):
    def test_warning_comparison_is_an_order_independent_normalized_multiset(self) -> None:
        expected_value = {
            "summary": {"warning": 2, "severe": 0, "fatal": 0},
            "items": [
                {"severity": "Warning", "title": "Node 0xABCDEF1   failed"},
                {"severity": "warning", "title": "Node 0xABCDEF1 failed"},
            ],
        }
        actual_value = {
            "summary": {"warning": 2, "severe": 0, "fatal": 0},
            "items": [
                {"severity": "WARNING", "title": "node 0x1234567 failed"},
                {"severity": "warning", "title": "NODE 0x7654321 FAILED"},
            ],
        }
        with TemporaryWorkspace() as workspace:
            expected = workspace.write_json("expected.json", expected_value)
            actual = workspace.write_json("actual.json", actual_value)

            result = runner.compare_warnings(expected, actual, allowed_delta=0)

        self.assertTrue(result["passed"], result["mismatches"])

    def test_warning_comparison_normalizes_lowercase_auto_address_tokens(self) -> None:
        expected_value = {
            "summary": {"warning": 1, "severe": 0, "fatal": 0},
            "items": [
                {
                    "severity": "warning",
                    "title": "HeatPump_named_DedicatedHeatPump0x176cf002540_for_zone warning",
                }
            ],
        }
        actual_value = {
            "summary": {"warning": 1, "severe": 0, "fatal": 0},
            "items": [
                {
                    "severity": "WARNING",
                    "title": "heatpump_named_dedicatedheatpump0xauto0000_for_zone WARNING",
                }
            ],
        }
        with TemporaryWorkspace() as workspace:
            expected = workspace.write_json("expected.json", expected_value)
            actual = workspace.write_json("actual.json", actual_value)

            result = runner.compare_warnings(expected, actual, allowed_delta=0)

        self.assertTrue(result["passed"], result["mismatches"])

    def test_warning_comparison_reports_duplicate_count_difference(self) -> None:
        expected_value = {
            "summary": {"warning": 2, "severe": 0, "fatal": 0},
            "items": [
                {"severity": "warning", "title": "Repeated warning"},
                {"severity": "warning", "title": "Repeated warning"},
            ],
        }
        actual_value = {
            "summary": {"warning": 1, "severe": 0, "fatal": 0},
            "items": [{"severity": "warning", "title": "Repeated warning"}],
        }
        with TemporaryWorkspace() as workspace:
            expected = workspace.write_json("expected.json", expected_value)
            actual = workspace.write_json("actual.json", actual_value)

            result = runner.compare_warnings(expected, actual, allowed_delta=1)

        self.assertFalse(result["passed"])
        self.assertEqual(1, result["mismatch_count"])
        self.assertEqual("missing_warning", result["mismatches"][0]["reason"])
        self.assertEqual(["warning", "repeated warning", 1], result["mismatches"][0]["expected"])

    def test_input_identity_accepts_hash_case_and_reports_changed_input(self) -> None:
        python_metadata = metadata()
        csharp_metadata = copy.deepcopy(python_metadata)
        csharp_metadata["runtime"]["idd_sha256"] = str(  # type: ignore[index]
            csharp_metadata["runtime"]["idd_sha256"]  # type: ignore[index]
        ).upper()

        passing = runner.check_input_identity(python_metadata, csharp_metadata)
        self.assertTrue(passing["passed"])

        csharp_metadata["inputs"]["grm"]["sha256"] = "f" * 64  # type: ignore[index]
        failing = runner.check_input_identity(python_metadata, csharp_metadata)

        self.assertFalse(failing["passed"])
        self.assertEqual("grm_sha256", failing["mismatches"][0]["identity"])

    def test_input_identity_rejects_hash_shared_by_engines_but_not_pinned(self) -> None:
        python_metadata = metadata()
        csharp_metadata = copy.deepcopy(python_metadata)
        pinned = {
            "energyplus_exe_sha256": "1" * 64,
            "idd_sha256": "2" * 64,
            "expandobjects_sha256": "3" * 64,
        }

        result = runner.check_input_identity(python_metadata, csharp_metadata, pinned)

        self.assertFalse(result["passed"])
        self.assertEqual(
            {
                "energyplus_exe_sha256",
                "idd_sha256",
                "expandobjects_sha256",
            },
            {item["identity"] for item in result["mismatches"]},
        )

    def test_case_manifest_pins_grm_and_weather_hashes(self) -> None:
        case = {
            "id": "pinned-inputs",
            "input_grm_sha256": "f" * 64,
            "weather_sha256": "b" * 64,
            "stages": ["authoring_idf", "expanded_idf"],
        }
        with TemporaryWorkspace() as workspace:
            python_root = workspace.path / "python"
            csharp_root = workspace.path / "csharp"
            for root in (python_root, csharp_root):
                case_root = root / "pinned-inputs"
                case_root.mkdir(parents=True)
                workspace.write_json(
                    str((case_root / "metadata.json").relative_to(workspace.path)).replace("\\", "/"),
                    metadata(),
                )
                for name in ("authoring.idf", "expanded.idf"):
                    workspace.write_text(
                        str((case_root / name).relative_to(workspace.path)).replace("\\", "/"),
                        "Version, 24.2;\n",
                    )

            result = runner.compare_case(case, manifest(), python_root, csharp_root)

        identity = result["checks"]["input_identity"]
        self.assertFalse(identity["passed"])
        self.assertEqual("f" * 64, identity["identities"]["grm_sha256"]["pinned"])
        self.assertEqual("b" * 64, identity["identities"]["weather_sha256"]["pinned"])
        self.assertEqual("grm_sha256", identity["mismatches"][0]["identity"])


class CaseStageTests(unittest.TestCase):
    def test_undeclared_optional_stages_do_not_require_grr_or_warning_files(self) -> None:
        case = {
            "id": "authoring-only",
            "stages": ["authoring_idf", "expanded_idf"],
        }
        with TemporaryWorkspace() as workspace:
            python_root = workspace.path / "python"
            csharp_root = workspace.path / "csharp"
            for root in (python_root, csharp_root):
                case_root = root / "authoring-only"
                case_root.mkdir(parents=True)
                workspace.write_json(
                    str((case_root / "metadata.json").relative_to(workspace.path)).replace("\\", "/"),
                    metadata(),
                )
                for name in ("authoring.idf", "expanded.idf"):
                    workspace.write_text(
                        str((case_root / name).relative_to(workspace.path)).replace("\\", "/"),
                        "Version, 24.2;\n",
                    )

            result = runner.compare_case(
                case,
                manifest(),
                python_root,
                csharp_root,
            )

        self.assertTrue(result["passed"])
        self.assertEqual(
            {"input_identity", "authoring_idf", "expanded_idf"},
            set(result["checks"]),
        )
        self.assertNotIn("grr", result["checks"])
        self.assertNotIn("warnings", result["checks"])

    def test_explicit_energyplus_skip_is_structured_and_never_passes(self) -> None:
        case = {
            "id": "skipped-simulation",
            "stages": ["authoring_idf", "expanded_idf", "energyplus", "grr", "warnings"],
        }
        with TemporaryWorkspace() as workspace:
            python_root = workspace.path / "python"
            csharp_root = workspace.path / "csharp"
            for root in (python_root, csharp_root):
                case_root = root / "skipped-simulation"
                case_root.mkdir(parents=True)
                workspace.write_json(
                    str((case_root / "metadata.json").relative_to(workspace.path)).replace("\\", "/"),
                    metadata(),
                )
                for name in ("authoring.idf", "expanded.idf"):
                    workspace.write_text(
                        str((case_root / name).relative_to(workspace.path)).replace("\\", "/"),
                        "Version, 24.2;\n",
                    )

            result = runner.compare_case(
                case,
                manifest(),
                python_root,
                csharp_root,
                skip_energyplus=True,
            )

        self.assertFalse(result["passed"])
        self.assertEqual(3, result["skip_count"])
        self.assertEqual(["energyplus", "grr", "warnings"], sorted(result["skipped_stages"]))
        self.assertNotIn("grr", result["checks"])
        self.assertNotIn("warnings", result["checks"])


if __name__ == "__main__":
    unittest.main()

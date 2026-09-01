"""Generate pinned InvisibleDragon RuleSet operation observations.

Run this only through ``bootstrap_reference.py`` so imports resolve from the
exact pinned upstream source and dependency tree.  The case corpus deliberately
mirrors the hardened DaySchedule operation corpus, then adds RuleSet-specific
topology and conditional-branch observations.
"""

from __future__ import annotations

import argparse
import importlib.util
import math
import os
from pathlib import Path
import re
import sys
from typing import Any, Callable


SCHEMA = "dragons.invisibledragon.rule-set-operations-oracle.v1"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "RuleSet.__add__": (
        "sha256:d658d7f91f8ee7dafbca0504b70bde094910f90830fc85a732e21dcab8ff2405"
    ),
    "RuleSet.__and__": (
        "sha256:68f36cc14f5d257034871f8f96c9ae1e8b225489ee47fd5ce398ade357148315"
    ),
    "RuleSet.__ge__": (
        "sha256:66ecfa68f9710c8f9914577b4617b989292394b534ca98b83e213e7fe735d2b7"
    ),
    "RuleSet.__gt__": (
        "sha256:c73275fa255d1916ee360c0d6a50ea20828cb75191f9733fa193e1ee4a4f0005"
    ),
    "RuleSet.__invert__": (
        "sha256:4c2c592271f4031026fa49d9f7b90e2e9d7edf0ce708cef18108e1509768780e"
    ),
    "RuleSet.__le__": (
        "sha256:c28491e978f051599d30f0582d7d3e6b92ed39207719422007d5f99e44aec32b"
    ),
    "RuleSet.__lt__": (
        "sha256:cb4515a256ae510fed02b2d73955fd02ec0316d68fc480772662ed3faab38a48"
    ),
    "RuleSet.__mul__": (
        "sha256:dfe4535f2bfc5d3e8823015e09f766c4bffb1eaa34a5d34641f5d7b86db22094"
    ),
    "RuleSet.__or__": (
        "sha256:db95291ff1d42fb08f26255bca01aae3ca1bcb0ac48a860f335092d2782a83c5"
    ),
    "RuleSet.__radd__": (
        "sha256:7d78c731949b203b143a486363e54f8572d57c8f12f0a598d7d0470ac776729e"
    ),
    "RuleSet.__rmul__": (
        "sha256:7359aee63c4e4e2dc1fd2c80435b39ac3e7989f60e89e4f9951784b13c003a99"
    ),
    "RuleSet.__rsub__": (
        "sha256:0ee38c580eba67e6c30a824516a9bf4cf97a4965f28ce732a7271ad8b705d0d6"
    ),
    "RuleSet.__rtruediv__": (
        "sha256:b665fd3ac19d91fed1717628316d613dd189aa9199ad0f9a11bf44deabbbd9a0"
    ),
    "RuleSet.__sub__": (
        "sha256:d13292383b4ac45ca61e2e5a3af7f47116cecd9427402a0f82f2facc9a748e8f"
    ),
    "RuleSet.__truediv__": (
        "sha256:5ce5d9fa78fe66d885f07337ea82654b3976d0a954f4dbd9870d2abb08eb272e"
    ),
    "RuleSet.element_eq": (
        "sha256:2d76198253866a17cebeac482806cb4c7172bdd1eac0247412d9e53f96a07f6e"
    ),
    "RuleSet.element_max": (
        "sha256:bfffae347ffeac971d2328d400ae28e5986cc2f3c60b707e0bb55431989edd39"
    ),
    "RuleSet.element_min": (
        "sha256:33739f88089372dfdb936c28027f53c69ca8ec4ec1f47ad24ff6d8b7fc427d64"
    ),
    "RuleSet.element_ne": (
        "sha256:acaa0bfa9274b747da2f9096ecb5598d67e8bb6515846462ae53512c65fe6f60"
    ),
    "RuleSet.is_between": (
        "sha256:1ada7d9d920d4732ef0bc1602db75a12d2cb97853e6cbbb5d0d7d32d15ec63e7"
    ),
    "RuleSet.is_negative": (
        "sha256:344049ce22623af29c4956fe51fd008ae546b6bbaedcaec1946037b00ef9d67e"
    ),
    "RuleSet.is_nonzero": (
        "sha256:12a2434cf468d99a4e259487daa1141861bf48c4b4973115adb76e2c3a24333f"
    ),
    "RuleSet.is_off": (
        "sha256:8f8e714ff0d9a931906eee296428f6565c13fa22152d731f7c0e22e31e0c1f52"
    ),
    "RuleSet.is_on": (
        "sha256:5c914c14bc867f961622cad9d503ee0103ac1f6e5bbffd860be6739fcc093592"
    ),
    "RuleSet.is_positive": (
        "sha256:7a7f9ce61c60171028a80e0e81f072ea7f86e59a5053360cec60843db6714247"
    ),
    "RuleSet.is_zero": (
        "sha256:8f8e714ff0d9a931906eee296428f6565c13fa22152d731f7c0e22e31e0c1f52"
    ),
    "RuleSet.normalize_by_max": (
        "sha256:92c2f28741585003d7e2bab24c4bff10cd1fa42133eb1cf6870409b37ec6ba55"
    ),
    "RuleSet.where": (
        "sha256:b245f2e84cd0e4b15b7f03d663409c07792e05f5143a8aae6a1e567769fa726a"
    ),
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
EXPECTED_CASE_COUNT = 334
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

SLOT_KEYS = (
    "weekdays",
    "weekends",
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
    "holiday",
)
OVERRIDE_KEYS = SLOT_KEYS[2:]
WEEKDAY_KEYS = ("monday", "tuesday", "wednesday", "thursday", "friday")
WEEKEND_KEYS = ("saturday", "sunday")

ARITHMETIC_NONFINITE_ADAPTATIONS = {
    "RuleSet.__add__": "nonfinite-result-ruleset-add",
    "RuleSet.__mul__": "nonfinite-result-ruleset-mul",
    "RuleSet.__radd__": "nonfinite-result-ruleset-radd",
    "RuleSet.__rmul__": "nonfinite-result-ruleset-rmul",
    "RuleSet.__rsub__": "nonfinite-result-ruleset-rsub",
    "RuleSet.__rtruediv__": "nonfinite-result-ruleset-rtruediv",
    "RuleSet.__sub__": "nonfinite-result-ruleset-sub",
    "RuleSet.__truediv__": "nonfinite-result-ruleset-truediv",
}
NORMALIZE_ADAPTATION = "nonfinite-result-ruleset-normalize-by-max"
WHERE_ADAPTATION = "deterministic-ruleset-where-day-names"
SCALAR_EXTREMA_ADAPTATIONS = {
    "RuleSet.element_max": "ruleset-scalar-maximum-upstream-attribute-error",
    "RuleSet.element_min": "ruleset-scalar-minimum-upstream-attribute-error",
}
EXPECTED_ADAPTATION_IDS = frozenset(
    (*ARITHMETIC_NONFINITE_ADAPTATIONS.values(),
     NORMALIZE_ADAPTATION,
     WHERE_ADAPTATION,
     *SCALAR_EXTREMA_ADAPTATIONS.values())
)

AUTO_NAME_PATTERN = re.compile(r"^0x[0-9a-f]+$")
RAW_AUTO_NAME_PATTERN = re.compile(r"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])")


def _load_day_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_day_schedule_operations_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_day_schedule_operations_support",
        path,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load DaySchedule oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
        or module.EXPECTED_CASE_COUNT != 321
    ):
        raise RuntimeError("DaySchedule oracle support is not the pinned corpus.")
    return module


DAY = _load_day_support()
strict_json_dumps = DAY.strict_json_dumps
canonical_sha256 = DAY.canonical_sha256
sha256_file = DAY.sha256_file
load_json_without_duplicates = DAY.load_json_without_duplicates
compact_values = DAY.compact_values
require_finite_scalar = DAY.require_finite_scalar
tagged_observation_value = DAY.tagged_observation_value
INEXACT_BINARY64_INTEGER = DAY.INEXACT_BINARY64_INTEGER
UNBOUNDED_INTEGER = DAY.UNBOUNDED_INTEGER
SIGNED_INT64_MIN = DAY.SIGNED_INT64_MIN
SIGNED_INT64_MAX = DAY.SIGNED_INT64_MAX
FLOAT_MAX = DAY.FLOAT_MAX
FLOAT_MIN_SUBNORMAL = DAY.FLOAT_MIN_SUBNORMAL
SCHEDULE_TEMPLATES = DAY.SCHEDULE_TEMPLATES


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    """Validate the full pinned inventory and bind exactly 28 RuleSet symbols."""

    # The DaySchedule loader fail-closes every inventory key, summary, hash,
    # ordering invariant, source file, commit, and canonical content digest.
    day_inventory = DAY.load_exact_inventory(path, upstream_commit)
    inventory = load_json_without_duplicates(path)
    target_symbols = [
        item
        for item in inventory["symbols"]
        if item["path"] == SOURCE_PATH and item["symbol"] in TARGET_SYMBOLS
    ]
    if [item["symbol"] for item in target_symbols] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover the 28 RuleSet symbols.")
    for item in target_symbols:
        if item["symbol_hash"] != EXPECTED_SYMBOL_HASHES[item["symbol"]]:
            raise SystemExit(f"The inventory hash for {item['symbol']} is not pinned.")
    return {
        "content_sha256": day_inventory["content_sha256"],
        "file": day_inventory["file"],
        "symbols": target_symbols,
    }


def ruleset_ref(name: str) -> tuple[str, str]:
    return ("ruleset", name)


def day_schedule_ref(name: str) -> tuple[str, str]:
    return ("day-schedule", name)


def _copy_specification(specification: tuple[str, Any]) -> tuple[str, Any]:
    kind, value = specification
    return ("ruleset", value) if kind == "schedule" else (kind, value)


def _registered_expectation(
    adaptation: str,
    outcome: str,
    policy: str,
    *,
    error_category: str | None = None,
    reference: str | None = None,
    result_name: str | None = None,
) -> dict[str, str]:
    result = {"adaptation": adaptation, "outcome": outcome, "policy": policy}
    if error_category is not None:
        result["error_category"] = error_category
    if reference is not None:
        result["reference"] = reference
    if result_name is not None:
        result["result_name"] = result_name
    return result


def case_definitions() -> tuple[dict[str, Any], ...]:
    """Transform the complete DaySchedule corpus into RuleSet observations."""

    definitions: list[dict[str, Any]] = []
    for source in DAY.case_definitions():
        symbol = source["symbol"].replace("DaySchedule.", "RuleSet.", 1)
        identifier = source["id"]
        inputs = {
            name: _copy_specification(specification)
            for name, specification in source["inputs"].items()
        }
        expected_exception = source["expected_exception"]
        expected_dotnet: dict[str, str] | None = None
        repair_reference = False

        if symbol == "RuleSet.normalize_by_max":
            inputs.pop("inplace")
            if identifier == "normalize.inplace.real-preserves-metadata":
                identifier = "normalize.copy.asymmetric-topology-real"
                inputs["new_name"] = ("text", "TopologyNormalized")
                expected_exception = None
            elif identifier == "normalize.inplace.temperature-validation-bypass":
                identifier = "normalize.copy.temperature-domain-error-explicit-name"
                inputs["new_name"] = ("text", "InvalidTemperatureNormalization")
                expected_exception = "ValueError"
            elif identifier == "normalize.copy.finite-input-negative-infinity":
                expected_dotnet = _registered_expectation(
                    NORMALIZE_ADAPTATION,
                    "raised",
                    "reject-nonfinite-result",
                    error_category="domain",
                )

        elif symbol == "RuleSet.where":
            identifier = identifier.replace("schedule-schedule", "ruleset-ruleset")
            identifier = identifier.replace("schedule-scalar", "ruleset-scalar")
            identifier = identifier.replace("scalar-schedule", "scalar-ruleset")
            identifier = identifier.replace("mixed-schedule-types", "mixed-ruleset-types")
            identifier = identifier.replace(
                "explicit-schedule-type-mismatch",
                "explicit-ruleset-type-mismatch",
            )
            if identifier in {
                "where.unselected-invalid-scalar",
                "large-int.unbounded.where.unselected-fraction-success",
            }:
                expected_exception = "ValueError"
            if identifier in {
                "where.ruleset-scalar.inferred",
                "where.scalar-ruleset.inferred",
            }:
                # RuleSet.where eagerly wraps an untyped scalar in a REAL
                # RuleSet before DaySchedule.where can infer the other branch.
                expected_exception = "ScheduleOperationError"
            if expected_exception is None:
                if identifier == "nonfinite.where.selected-true.positive-infinity":
                    expected_dotnet = _registered_expectation(
                        WHERE_ADAPTATION,
                        "raised",
                        "reject-nonfinite-result",
                        error_category="domain",
                    )
                else:
                    expected_dotnet = _registered_expectation(
                        WHERE_ADAPTATION,
                        "returned",
                        "deterministic-slot-names",
                    )

        elif symbol in SCALAR_EXTREMA_ADAPTATIONS and inputs["other"][0] in {
            "scalar",
            "nonfinite",
        }:
            expected_exception = "AttributeError"
            repair_reference = True

        else:
            day_expectation = source["expected_dotnet"]
            if day_expectation is not None:
                if symbol not in ARITHMETIC_NONFINITE_ADAPTATIONS:
                    raise RuntimeError(
                        f"Unmapped DaySchedule adaptation for {identifier!r}."
                    )
                expected_dotnet = _registered_expectation(
                    ARITHMETIC_NONFINITE_ADAPTATIONS[symbol],
                    "raised",
                    "reject-nonfinite-result",
                    error_category="domain",
                )

        definitions.append(
            {
                "expected_dotnet": expected_dotnet,
                "expected_exception": expected_exception,
                "id": identifier,
                "inputs": inputs,
                "repair_reference": repair_reference,
                "symbol": symbol,
            }
        )

    where_additions = (
        (
            "where.all-plain.ruleset-ruleset",
            {
                "condition": ruleset_ref("condition@plain"),
                "if_true": ruleset_ref("where-true@plain"),
                "if_false": ruleset_ref("where-false@plain"),
                "name": ("text", "WhereAllPlain"),
                "type": ("none", None),
            },
        ),
        (
            "where.day-day.inferred",
            {
                "condition": ruleset_ref("condition"),
                "if_true": day_schedule_ref("where-true"),
                "if_false": day_schedule_ref("where-false"),
                "name": ("text", "WhereDayDay"),
                "type": ("none", None),
            },
        ),
        (
            "where.day-ruleset.inferred",
            {
                "condition": ruleset_ref("condition"),
                "if_true": day_schedule_ref("where-true"),
                "if_false": ruleset_ref("where-false"),
                "name": ("text", "WhereDayRuleSet"),
                "type": ("none", None),
            },
        ),
        (
            "where.ruleset-day.inferred",
            {
                "condition": ruleset_ref("condition"),
                "if_true": ruleset_ref("where-true"),
                "if_false": day_schedule_ref("where-false"),
                "name": ("text", "WhereRuleSetDay"),
                "type": ("none", None),
            },
        ),
        (
            "where.day-scalar.explicit-fraction",
            {
                "condition": ruleset_ref("condition"),
                "if_true": day_schedule_ref("where-true"),
                "if_false": ("scalar", 0.1),
                "name": ("text", "WhereDayScalar"),
                "type": ("schedule-type", "fraction"),
            },
        ),
        (
            "where.scalar-day.explicit-fraction",
            {
                "condition": ruleset_ref("condition"),
                "if_true": ("scalar", 0.4),
                "if_false": day_schedule_ref("where-false"),
                "name": ("text", "WhereScalarDay"),
                "type": ("schedule-type", "fraction"),
            },
        ),
    )
    for identifier, inputs in where_additions:
        definitions.append(
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "returned",
                    "deterministic-slot-names",
                ),
                "expected_exception": None,
                "id": identifier,
                "inputs": inputs,
                "repair_reference": False,
                "symbol": "RuleSet.where",
            }
        )

    definitions.extend(
        (
            {
                "expected_dotnet": _registered_expectation(
                    NORMALIZE_ADAPTATION,
                    "raised",
                    "reject-invalid-name",
                    error_category="type",
                ),
                "expected_exception": None,
                "id": "normalize.name.empty-native-invalid",
                "inputs": {
                    "new_name": ("text", ""),
                    "receiver": ruleset_ref("real-left"),
                },
                "repair_reference": False,
                "symbol": "RuleSet.normalize_by_max",
            },
            {
                "expected_dotnet": _registered_expectation(
                    NORMALIZE_ADAPTATION,
                    "raised",
                    "reject-invalid-name",
                    error_category="type",
                ),
                "expected_exception": None,
                "id": "normalize.name.whitespace-native-invalid",
                "inputs": {
                    "new_name": ("text", "  \t  "),
                    "receiver": ruleset_ref("real-left"),
                },
                "repair_reference": False,
                "symbol": "RuleSet.normalize_by_max",
            },
            {
                "expected_dotnet": _registered_expectation(
                    NORMALIZE_ADAPTATION,
                    "returned",
                    "trim-result-name",
                    result_name="Normalized",
                ),
                "expected_exception": None,
                "id": "normalize.name.surrounding-whitespace-trimmed",
                "inputs": {
                    "new_name": ("text", "  Normalized  "),
                    "receiver": ruleset_ref("real-left"),
                },
                "repair_reference": False,
                "symbol": "RuleSet.normalize_by_max",
            },
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "returned",
                    "deterministic-slot-names",
                ),
                "expected_exception": None,
                "id": "where.name.empty-falls-back-to-where",
                "inputs": {
                    "condition": ruleset_ref("condition"),
                    "if_false": ruleset_ref("where-false"),
                    "if_true": ruleset_ref("where-true"),
                    "name": ("text", ""),
                    "type": ("none", None),
                },
                "repair_reference": False,
                "symbol": "RuleSet.where",
            },
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "returned",
                    "trim-name-and-deterministic-slot-names",
                    result_name="Selected",
                ),
                "expected_exception": None,
                "id": "where.name.surrounding-whitespace-trimmed",
                "inputs": {
                    "condition": ruleset_ref("condition"),
                    "if_false": ruleset_ref("where-false"),
                    "if_true": ruleset_ref("where-true"),
                    "name": ("text", "  Selected  "),
                    "type": ("none", None),
                },
                "repair_reference": False,
                "symbol": "RuleSet.where",
            },
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "raised",
                    "reject-invalid-name",
                    error_category="type",
                ),
                "expected_exception": None,
                "id": "where.name.whitespace-native-invalid",
                "inputs": {
                    "condition": ruleset_ref("condition"),
                    "if_false": ruleset_ref("where-false"),
                    "if_true": ruleset_ref("where-true"),
                    "name": ("text", "  \t  "),
                    "type": ("none", None),
                },
                "repair_reference": False,
                "symbol": "RuleSet.where",
            },
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "returned",
                    "deterministic-slot-names",
                ),
                "expected_exception": None,
                "id": "where.onoff.selected-negative-zero-normalizes-positive-zero",
                "inputs": {
                    "condition": ruleset_ref("condition-all-true"),
                    "if_false": ("scalar", 1.0),
                    "if_true": ("scalar", -0.0),
                    "name": ("text", "WhereOnOffNegativeZero"),
                    "type": ("schedule-type", "onoff"),
                },
                "repair_reference": False,
                "symbol": "RuleSet.where",
            },
        )
    )

    definitions.sort(key=lambda item: item["id"])
    validate_case_definitions(definitions)
    return tuple(definitions)


def validate_case_definitions(definitions: list[dict[str, Any]]) -> None:
    identifiers = [item["id"] for item in definitions]
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} RuleSet operation cases, got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("RuleSet operation case identifiers are not unique and sorted.")
    if {item["symbol"] for item in definitions} != set(TARGET_SYMBOLS):
        raise RuntimeError("RuleSet operation cases do not cover exactly 28 symbols.")

    single_inputs = {
        "RuleSet.__invert__",
        "RuleSet.is_negative",
        "RuleSet.is_nonzero",
        "RuleSet.is_off",
        "RuleSet.is_on",
        "RuleSet.is_positive",
        "RuleSet.is_zero",
    }
    for definition in definitions:
        if set(definition) != {
            "expected_dotnet",
            "expected_exception",
            "id",
            "inputs",
            "repair_reference",
            "symbol",
        }:
            raise RuntimeError(f"Case {definition.get('id')!r} has unexpected keys.")
        symbol = definition["symbol"]
        expected_inputs = (
            {"receiver"}
            if symbol in single_inputs
            else {"include_max", "include_min", "max_value", "min_value", "receiver"}
            if symbol == "RuleSet.is_between"
            else {"new_name", "receiver"}
            if symbol == "RuleSet.normalize_by_max"
            else {"condition", "if_false", "if_true", "name", "type"}
            if symbol == "RuleSet.where"
            else {"other", "receiver"}
        )
        if set(definition["inputs"]) != expected_inputs:
            raise RuntimeError(
                f"Case {definition['id']!r} inputs are not exact for {symbol}."
            )
        if definition["repair_reference"] != (
            symbol in SCALAR_EXTREMA_ADAPTATIONS
            and definition["inputs"].get("other", (None,))[0]
            in {"scalar", "nonfinite"}
        ):
            raise RuntimeError(f"Case {definition['id']!r} has invalid repair binding.")
        for input_name, specification in definition["inputs"].items():
            if not isinstance(input_name, str) or not isinstance(specification, tuple):
                raise RuntimeError(f"Case {definition['id']!r} has an invalid input.")
            kind, value = specification
            base_name = value.split("@", 1)[0] if isinstance(value, str) else value
            if kind in {"ruleset", "day-schedule"}:
                if base_name not in SCHEDULE_TEMPLATES:
                    raise RuntimeError(
                        f"Case {definition['id']!r} uses an unknown schedule template."
                    )
                if kind == "day-schedule" and symbol != "RuleSet.where":
                    raise RuntimeError("DaySchedule inputs are only valid for RuleSet.where.")
            elif kind == "scalar":
                require_finite_scalar(value, f"Case {definition['id']} input {input_name}")
            elif kind == "nonfinite":
                DAY.nonfinite(value)
            elif kind == "text":
                if not isinstance(value, str):
                    raise RuntimeError(f"Case {definition['id']!r} has invalid text.")
            elif kind == "none":
                if value is not None:
                    raise RuntimeError(f"Case {definition['id']!r} has invalid None input.")
            elif kind == "schedule-type":
                if value not in {"fraction", "onoff", "real", "temperature"}:
                    raise RuntimeError(
                        f"Case {definition['id']!r} has invalid schedule type."
                    )
            else:
                raise RuntimeError(f"Case {definition['id']!r} has unknown input kind.")

        expectation = definition["expected_dotnet"]
        if expectation is not None:
            if expectation["adaptation"] not in EXPECTED_ADAPTATION_IDS:
                raise RuntimeError(f"Case {definition['id']!r} has unknown adaptation.")
            if expectation["outcome"] not in {"raised", "returned"}:
                raise RuntimeError(f"Case {definition['id']!r} has invalid .NET outcome.")
            if expectation["policy"] not in {
                "deterministic-slot-names",
                "reject-invalid-name",
                "reject-nonfinite-result",
                "trim-name-and-deterministic-slot-names",
                "trim-result-name",
            }:
                raise RuntimeError(f"Case {definition['id']!r} has invalid .NET policy.")
            if expectation.get("error_category") is not None and (
                expectation["outcome"] != "raised"
                or expectation["error_category"]
                not in {"divide-by-zero", "domain", "schedule-operation", "type"}
            ):
                raise RuntimeError(f"Case {definition['id']!r} has invalid error category.")
            if expectation["policy"] == "reject-invalid-name" and (
                expectation["outcome"] != "raised"
                or expectation.get("error_category") != "type"
            ):
                raise RuntimeError(
                    f"Case {definition['id']!r} has malformed name rejection."
                )
            trim_policies = {
                "trim-name-and-deterministic-slot-names",
                "trim-result-name",
            }
            if expectation["policy"] in trim_policies:
                result_name = expectation.get("result_name")
                if (
                    expectation["outcome"] != "returned"
                    or not isinstance(result_name, str)
                    or not result_name
                    or result_name != result_name.strip()
                ):
                    raise RuntimeError(
                        f"Case {definition['id']!r} has malformed trimmed name."
                    )
            elif "result_name" in expectation:
                raise RuntimeError(
                    f"Case {definition['id']!r} has an unexpected result name."
                )
        elif (
            not definition["repair_reference"]
            and any(kind == "nonfinite" for kind, _ in definition["inputs"].values())
        ):
            raise RuntimeError(f"Case {definition['id']!r} has unadapted non-finite input.")

    strict_json_dumps(
        [
            {
                **item,
                "inputs": {key: list(value) for key, value in item["inputs"].items()},
            }
            for item in definitions
        ]
    )


def _split_template_name(name: str) -> tuple[str, str | None]:
    base, separator, topology = name.partition("@")
    return base, topology if separator else None


def _topology_for_template(name: str, requested: str | None) -> frozenset[str]:
    if requested == "plain":
        return frozenset()
    if requested is not None:
        raise RuntimeError(f"Unknown RuleSet topology {requested!r}.")
    if name in {"condition", "condition-all-true"}:
        return frozenset({"monday", "thursday", "holiday"})
    if name == "where-true":
        return frozenset({"tuesday", "friday", "sunday"})
    if name == "where-false":
        return frozenset({"wednesday", "saturday", "holiday"})
    if name.endswith("-right"):
        return frozenset({"tuesday", "sunday"})
    return frozenset({"monday", "saturday", "holiday"})


def _expanded_values(pattern: tuple[int | float, ...], offset: int) -> list[int | float]:
    # Slot names carry fallback provenance.  Keep values identical to the
    # DaySchedule corpus so its carefully pinned domain/error expectations are
    # not changed by a one-sided override resolving against a default slot.
    del offset
    return list((pattern * ((144 // len(pattern)) + 1))[:144])


def create_day_schedule(
    template_name: str,
    slot: str,
    DaySchedule: type,
    ScheduleType: type,
) -> Any:
    base_name, _ = _split_template_name(template_name)
    template = SCHEDULE_TEMPLATES[base_name]
    slot_index = SLOT_KEYS.index(slot)
    pattern = tuple(template["pattern"])
    return DaySchedule(
        f"{template['name']}Rules:{slot}",
        _expanded_values(pattern, slot_index),
        type=ScheduleType(template["type"]),
        unit=template["unit"],
    )


def create_ruleset(
    template_name: str,
    DaySchedule: type,
    RuleSet: type,
    ScheduleType: type,
) -> Any:
    base_name, topology_name = _split_template_name(template_name)
    template = SCHEDULE_TEMPLATES[base_name]
    topology = _topology_for_template(base_name, topology_name)
    slots: dict[str, Any] = {
        "weekdays": create_day_schedule(
            template_name, "weekdays", DaySchedule, ScheduleType
        ),
        "weekends": create_day_schedule(
            template_name, "weekends", DaySchedule, ScheduleType
        ),
    }
    slots.update(
        {
            key: create_day_schedule(template_name, key, DaySchedule, ScheduleType)
            if key in topology
            else None
            for key in OVERRIDE_KEYS
        }
    )
    suffix = "PlainRules" if topology_name == "plain" else "Rules"
    return RuleSet(
        f"{template['name']}{suffix}",
        **slots,
        type=ScheduleType(template["type"]),
    )


def _name_descriptor(name: Any, policy: str) -> dict[str, str]:
    if policy == "runtime-identity-hex":
        if not isinstance(name, str) or AUTO_NAME_PATTERN.fullmatch(name) is None:
            raise RuntimeError("A runtime RuleSet child name is not hexadecimal.")
        return {"policy": "runtime-identity-hex"}
    if policy != "literal":
        raise RuntimeError(f"Unknown name policy {policy!r}.")
    if not isinstance(name, str) or AUTO_NAME_PATTERN.fullmatch(name) is not None:
        raise RuntimeError("A raw runtime identity name would enter the RuleSet oracle.")
    return {"policy": "literal", "value": name}


def schedule_descriptor(
    schedule: Any,
    name_policy: str,
    DaySchedule: type,
    *,
    allow_nonfinite: bool,
) -> dict[str, Any]:
    if not isinstance(schedule, DaySchedule):
        raise RuntimeError("A RuleSet slot is not a DaySchedule.")
    return {
        "kind": "day-schedule",
        "name": _name_descriptor(schedule.name, name_policy),
        "schedule_type": schedule.type.value,
        "unit": schedule.unit,
        "values": compact_values(
            list(schedule.data),
            allow_nonfinite=allow_nonfinite,
        ),
    }


def effective_slot_sources(ruleset: Any) -> dict[str, str]:
    return {
        key: key
        if getattr(ruleset, key) is not None
        else "weekdays"
        if key in WEEKDAY_KEYS
        else "weekends"
        for key in OVERRIDE_KEYS
    }


def ruleset_descriptor(
    ruleset: Any,
    RuleSet: type,
    DaySchedule: type,
    *,
    child_name_policy: str = "literal",
    allow_nonfinite: bool = False,
) -> dict[str, Any]:
    if not isinstance(ruleset, RuleSet):
        raise RuntimeError("A successful RuleSet operation returned the wrong type.")
    slots: dict[str, Any] = {}
    for key, schedule in ruleset.to_dict().items():
        if key not in SLOT_KEYS:
            raise RuntimeError(f"A RuleSet exposed unknown slot {key!r}.")
        slots[key] = (
            None
            if schedule is None
            else schedule_descriptor(
                schedule,
                child_name_policy,
                DaySchedule,
                allow_nonfinite=allow_nonfinite,
            )
        )
    if tuple(slots) != SLOT_KEYS:
        raise RuntimeError("A RuleSet descriptor does not contain the exact ten slots.")
    return {
        "effective_slot_sources": effective_slot_sources(ruleset),
        "kind": "ruleset",
        "name": _name_descriptor(ruleset.name, "literal"),
        "schedule_type": ruleset.type.value,
        "slots": slots,
    }


def input_descriptor(
    value: Any,
    DaySchedule: type,
    RuleSet: type,
    ScheduleType: type,
) -> dict[str, Any]:
    if isinstance(value, RuleSet):
        return ruleset_descriptor(value, RuleSet, DaySchedule)
    if isinstance(value, DaySchedule):
        return schedule_descriptor(
            value,
            "literal",
            DaySchedule,
            allow_nonfinite=False,
        )
    if isinstance(value, ScheduleType):
        return {"kind": "schedule-type", "value": value.value}
    if value is None:
        return {"kind": "none"}
    if isinstance(value, str):
        return {"kind": "text", "value": value}
    if isinstance(value, float) and not math.isfinite(value):
        tagged = tagged_observation_value(value, allow_nonfinite=True)
        if not isinstance(tagged, dict):
            raise RuntimeError("A non-finite scalar input was not tagged.")
        return tagged
    require_finite_scalar(value, "Oracle scalar input")
    python_type = "bool" if type(value) is bool else "int" if type(value) is int else "float"
    encoded_value: Any = value
    if type(value) is int and not SIGNED_INT64_MIN <= value <= SIGNED_INT64_MAX:
        encoded_value = {"kind": "decimal-string", "value": str(value)}
    return {"kind": "scalar", "python_type": python_type, "value": encoded_value}


def resolve_input(
    specification: tuple[str, Any],
    DaySchedule: type,
    RuleSet: type,
    ScheduleType: type,
) -> Any:
    kind, value = specification
    if kind == "ruleset":
        return create_ruleset(value, DaySchedule, RuleSet, ScheduleType)
    if kind == "day-schedule":
        return create_day_schedule(value, "weekdays", DaySchedule, ScheduleType)
    if kind == "schedule-type":
        return ScheduleType(value)
    if kind == "nonfinite":
        return {
            "nan": math.nan,
            "negative-infinity": -math.inf,
            "positive-infinity": math.inf,
        }[value]
    return value


def _slot_state(schedule: Any) -> tuple[Any, ...] | None:
    return (
        None
        if schedule is None
        else (id(schedule), schedule.name, schedule.type, schedule.unit, tuple(schedule.data))
    )


def ruleset_state(ruleset: Any) -> tuple[Any, ...]:
    return (
        ruleset.name,
        ruleset.type,
        tuple(_slot_state(ruleset.to_dict()[key]) for key in SLOT_KEYS),
    )


def day_schedule_state(schedule: Any) -> tuple[Any, ...]:
    return (schedule.name, schedule.type, schedule.unit, tuple(schedule.data))


def _ruleset_postcondition(
    ruleset: Any,
    before: tuple[Any, ...],
    RuleSet: type,
    DaySchedule: type,
) -> dict[str, Any]:
    after = ruleset_state(ruleset)
    slot_states_before = before[2]
    slot_states_after = after[2]
    slots: dict[str, Any] = {}
    for index, key in enumerate(SLOT_KEYS):
        old = slot_states_before[index]
        new = slot_states_after[index]
        if old is None or new is None:
            slots[key] = {
                "identity": "none" if new is None else "replaced",
                "status": "unchanged" if old == new else "changed",
            }
        else:
            slots[key] = {
                "identity": "preserved" if old[0] == new[0] else "replaced",
                "status": "unchanged" if old == new else "changed",
            }
    result: dict[str, Any] = {
        "identity": "preserved",
        "slots": slots,
        "status": "unchanged" if before == after else "changed",
    }
    if before != after:
        result["value"] = ruleset_descriptor(
            ruleset,
            RuleSet,
            DaySchedule,
            allow_nonfinite=True,
        )
    return result


def _day_schedule_postcondition(
    schedule: Any,
    before: tuple[Any, ...],
    DaySchedule: type,
) -> dict[str, Any]:
    after = day_schedule_state(schedule)
    result: dict[str, Any] = {
        "identity": "preserved",
        "status": "unchanged" if before == after else "changed",
    }
    if before != after:
        result["value"] = schedule_descriptor(
            schedule,
            "literal",
            DaySchedule,
            allow_nonfinite=True,
        )
    return result


def invoke_case(symbol: str, inputs: dict[str, Any], RuleSet: type) -> Any:
    receiver = inputs.get("receiver")
    other = inputs.get("other")
    method = symbol.removeprefix("RuleSet.")
    if method == "__add__":
        return receiver + other
    if method == "__and__":
        return receiver & other
    if method == "__ge__":
        return receiver >= other
    if method == "__gt__":
        return receiver > other
    if method == "__invert__":
        return ~receiver
    if method == "__le__":
        return receiver <= other
    if method == "__lt__":
        return receiver < other
    if method == "__mul__":
        return receiver * other
    if method == "__or__":
        return receiver | other
    if method == "__radd__":
        return other + receiver
    if method == "__rmul__":
        return other * receiver
    if method == "__rsub__":
        return other - receiver
    if method == "__rtruediv__":
        return other / receiver
    if method == "__sub__":
        return receiver - other
    if method == "__truediv__":
        return receiver / other
    if method == "element_eq":
        return receiver.element_eq(other)
    if method == "element_max":
        return receiver.element_max(other)
    if method == "element_min":
        return receiver.element_min(other)
    if method == "element_ne":
        return receiver.element_ne(other)
    if method == "is_between":
        return receiver.is_between(
            inputs["min_value"],
            inputs["max_value"],
            include_min=inputs["include_min"],
            include_max=inputs["include_max"],
        )
    if method == "is_negative":
        return receiver.is_negative()
    if method == "is_nonzero":
        return receiver.is_nonzero()
    if method == "is_off":
        return receiver.is_off()
    if method == "is_on":
        return receiver.is_on()
    if method == "is_positive":
        return receiver.is_positive()
    if method == "is_zero":
        return receiver.is_zero()
    if method == "normalize_by_max":
        return receiver.normalize_by_max(new_name=inputs["new_name"])
    if method == "where":
        return RuleSet.where(
            inputs["condition"],
            inputs["if_true"],
            inputs["if_false"],
            name=inputs["name"],
            type=inputs["type"],
        )
    raise RuntimeError(f"Unsupported oracle symbol dispatch: {symbol}.")


def invoke_extrema_repair(symbol: str, inputs: dict[str, Any], RuleSet: type) -> Any:
    receiver = inputs["receiver"]
    other = inputs["other"]
    operation = "MAX" if symbol == "RuleSet.element_max" else "MIN"
    day_operator: Callable[[Any, Any], Any] = (
        (lambda left, right: left.element_max(right))
        if operation == "MAX"
        else (lambda left, right: left.element_min(right))
    )
    # Bypass only the pinned bug's eager ``other.name`` read.  Dispatch,
    # fallback materialization, DaySchedule validation, and naming continue
    # through the upstream private helper used by the public method.
    return RuleSet._RuleSet__operate_with_default(
        f"{receiver.name}:{operation}:{str(other)}",
        day_operator,
        receiver,
        other,
    )


def python_error_category(exception: Exception) -> str:
    name = type(exception).__name__
    if name == "ScheduleOperationError":
        return "schedule-operation"
    if name == "ZeroDivisionError":
        return "divide-by-zero"
    if name in {"OverflowError", "ValueError"}:
        return "domain"
    if name in {"AttributeError", "TypeError"}:
        return "type"
    raise RuntimeError(f"Unknown Python operation exception {name!r}.")


def _contains_descriptor_kind(value: Any, expected: str) -> bool:
    if isinstance(value, dict):
        if value.get("kind") == expected:
            return True
        return any(_contains_descriptor_kind(item, expected) for item in value.values())
    if isinstance(value, list):
        return any(_contains_descriptor_kind(item, expected) for item in value)
    return False


def _outcome_observation(
    action: Callable[[], Any],
    *,
    expected_exception: str | None,
    child_name_policy: str,
    actual_inputs: dict[str, Any],
    ruleset_inputs: dict[str, Any],
    ruleset_before: dict[str, tuple[Any, ...]],
    day_inputs: dict[str, Any],
    day_before: dict[str, tuple[Any, ...]],
    RuleSet: type,
    DaySchedule: type,
) -> dict[str, Any]:
    try:
        result = action()
    except Exception as exception:
        if expected_exception is None:
            raise
        if type(exception).__name__ != expected_exception:
            raise RuntimeError(
                f"Raised {type(exception).__name__}, expected {expected_exception}."
            ) from exception
        observation: dict[str, Any] = {
            "exception": {"message": str(exception), "type": type(exception).__name__},
            "outcome": "raised",
        }
    else:
        if expected_exception is not None:
            raise RuntimeError(
                f"Returned but expected {expected_exception}."
            )
        descriptor = ruleset_descriptor(
            result,
            RuleSet,
            DaySchedule,
            child_name_policy=child_name_policy,
            allow_nonfinite=True,
        )
        observation = {
            "outcome": "returned",
            "result": descriptor,
            "result_identity": (
                "receiver" if result is actual_inputs.get("receiver") else "new"
            ),
        }

    observation["ruleset_inputs_after"] = {
        name: _ruleset_postcondition(
            value,
            ruleset_before[name],
            RuleSet,
            DaySchedule,
        )
        for name, value in ruleset_inputs.items()
    }
    observation["day_schedule_inputs_after"] = {
        name: _day_schedule_postcondition(value, day_before[name], DaySchedule)
        for name, value in day_inputs.items()
    }
    return observation


def execute_case(
    definition: dict[str, Any],
    DaySchedule: type,
    RuleSet: type,
    ScheduleType: type,
) -> dict[str, Any]:
    actual_inputs = {
        name: resolve_input(specification, DaySchedule, RuleSet, ScheduleType)
        for name, specification in definition["inputs"].items()
    }
    descriptors = {
        name: input_descriptor(value, DaySchedule, RuleSet, ScheduleType)
        for name, value in actual_inputs.items()
    }
    ruleset_inputs = {
        name: value for name, value in actual_inputs.items() if isinstance(value, RuleSet)
    }
    day_inputs = {
        name: value
        for name, value in actual_inputs.items()
        if isinstance(value, DaySchedule)
    }
    ruleset_before = {
        name: ruleset_state(value) for name, value in ruleset_inputs.items()
    }
    day_before = {
        name: day_schedule_state(value) for name, value in day_inputs.items()
    }
    child_name_policy = (
        "runtime-identity-hex"
        if definition["symbol"] == "RuleSet.where"
        else "literal"
    )
    try:
        observation = _outcome_observation(
            lambda: invoke_case(definition["symbol"], actual_inputs, RuleSet),
            expected_exception=definition["expected_exception"],
            child_name_policy=child_name_policy,
            actual_inputs=actual_inputs,
            ruleset_inputs=ruleset_inputs,
            ruleset_before=ruleset_before,
            day_inputs=day_inputs,
            day_before=day_before,
            RuleSet=RuleSet,
            DaySchedule=DaySchedule,
        )
    except Exception as exception:
        raise RuntimeError(
            f"Case {definition['id']} failed its pinned expectation: {exception}"
        ) from exception

    case: dict[str, Any] = {
        "id": definition["id"],
        "inputs": descriptors,
        "observation": observation,
        "symbol": definition["symbol"],
    }

    if definition["repair_reference"]:
        try:
            repair_observation = _outcome_observation(
                lambda: invoke_extrema_repair(
                    definition["symbol"], actual_inputs, RuleSet
                ),
                expected_exception=None,
                child_name_policy="literal",
                actual_inputs=actual_inputs,
                ruleset_inputs=ruleset_inputs,
                ruleset_before=ruleset_before,
                day_inputs=day_inputs,
                day_before=day_before,
                RuleSet=RuleSet,
                DaySchedule=DaySchedule,
            )
        except Exception as exception:
            # Re-run with the exact observed exception as the expectation so
            # the normalized repair receipt is still emitted.
            repair_observation = _outcome_observation(
                lambda: invoke_extrema_repair(
                    definition["symbol"], actual_inputs, RuleSet
                ),
                expected_exception=type(exception.__cause__ or exception).__name__,
                child_name_policy="literal",
                actual_inputs=actual_inputs,
                ruleset_inputs=ruleset_inputs,
                ruleset_before=ruleset_before,
                day_inputs=day_inputs,
                day_before=day_before,
                RuleSet=RuleSet,
                DaySchedule=DaySchedule,
            )
        case["repair_reference"] = {
            "bypass": "scalar-other-name-read-only",
            "observation": repair_observation,
        }
        adaptation = SCALAR_EXTREMA_ADAPTATIONS[definition["symbol"]]
        if repair_observation["outcome"] == "raised":
            expected_dotnet = _registered_expectation(
                adaptation,
                "raised",
                "match-repair-reference",
                error_category=python_error_category_from_observation(
                    repair_observation
                ),
                reference="repair_reference",
            )
        elif _contains_descriptor_kind(repair_observation["result"], "nonfinite"):
            expected_dotnet = _registered_expectation(
                adaptation,
                "raised",
                "reject-nonfinite-repair-result",
                error_category="domain",
                reference="repair_reference",
            )
        else:
            expected_dotnet = _registered_expectation(
                adaptation,
                "returned",
                "match-repair-reference",
                reference="repair_reference",
            )
        case["expected_dotnet"] = expected_dotnet
    elif definition["expected_dotnet"] is not None:
        case["expected_dotnet"] = definition["expected_dotnet"]

    return case


def python_error_category_from_observation(observation: dict[str, Any]) -> str:
    if observation.get("outcome") != "raised":
        raise RuntimeError("An error category requires a raised observation.")
    name = observation["exception"]["type"]
    if name == "ScheduleOperationError":
        return "schedule-operation"
    if name == "ZeroDivisionError":
        return "divide-by-zero"
    if name in {"OverflowError", "ValueError"}:
        return "domain"
    if name in {"AttributeError", "TypeError"}:
        return "type"
    raise RuntimeError(f"Unknown Python operation exception {name!r}.")


def summarize_cases(cases: list[dict[str, Any]]) -> dict[str, Any]:
    observed_outcomes = {
        outcome: sum(item["observation"]["outcome"] == outcome for item in cases)
        for outcome in ("raised", "returned")
    }
    adapted = [item for item in cases if "expected_dotnet" in item]
    expected_dotnet_outcomes = {
        outcome: sum(item["expected_dotnet"]["outcome"] == outcome for item in adapted)
        for outcome in ("raised", "returned")
    }
    adaptations = sorted(
        {
            item["expected_dotnet"]["adaptation"]
            for item in adapted
        }
    )
    if set(adaptations) != set(EXPECTED_ADAPTATION_IDS):
        raise RuntimeError("The generated oracle does not bind exactly 12 adaptations.")
    return {
        "adaptation_case_count": len(adapted),
        "adaptation_ids": adaptations,
        "case_count": len(cases),
        "expected_dotnet_outcomes": expected_dotnet_outcomes,
        "observed_outcomes": observed_outcomes,
        "repair_reference_count": sum("repair_reference" in item for item in cases),
    }


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import DaySchedule, RuleSet, ScheduleType

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")
    if DaySchedule.DATA_INTERVAL != 6 or DaySchedule("probe").fixed_length != 144:
        raise SystemExit("Pinned DaySchedule grid constants are not exact.")
    if tuple(RuleSet._DAY_KEYS) != OVERRIDE_KEYS:
        raise SystemExit("Pinned RuleSet day keys are not exact.")

    definitions = case_definitions()
    cases = [
        execute_case(item, DaySchedule, RuleSet, ScheduleType)
        for item in definitions
    ]
    if [item["id"] for item in cases] != sorted(item["id"] for item in cases):
        raise RuntimeError("Generated RuleSet operation cases are not sorted.")

    result = {
        "cases": cases,
        "runtime": {
            "implementation": sys.implementation.name,
            "python_hash_algorithm": sys.hash_info.algorithm,
            "python_hash_seed": 0,
            "python_hash_width_bits": sys.hash_info.width,
            "python_version": ".".join(map(str, sys.version_info[:3])),
        },
        "schema": SCHEMA,
        "summary": summarize_cases(cases),
        "symbols": inventory["symbols"],
        "upstream": {
            "commit": commit,
            "inventory_sha256": inventory["content_sha256"],
            "path": SOURCE_PATH,
            "source_sha256": imported_source_sha256,
        },
    }
    serialized = strict_json_dumps(result)
    if RAW_AUTO_NAME_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime identity name entered the RuleSet oracle.")
    return result


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the RuleSet oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for the RuleSet oracle.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("The pinned CPython hash runtime is not exact.")

    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    result = build_oracle(inventory, args.upstream_commit.lower())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

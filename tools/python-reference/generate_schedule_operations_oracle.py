"""Generate pinned InvisibleDragon annual Schedule operation observations.

Run this only through ``bootstrap_reference.py``.  The corpus promotes the
verified RuleSet operation cases to a 365-day annual layer with deliberately
asymmetric compact-period boundaries.  Observations retain the exact nested
RuleSet/DaySchedule values, names, types, fallback sources, period topology,
and source immutability without serializing process identities.
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


SCHEMA = "goniegonie.invisibledragon.schedule-operations-oracle.v1"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "Schedule.__add__": "sha256:b1f53fbc391503bdfb118688bb177fa95bcc24c9dfa53fed27cf00442c23eb72",
    "Schedule.__and__": "sha256:7f01a01b4cac360d47a636894a07fd56068d60de8e9f38f0f2093136b7d5b604",
    "Schedule.__ge__": "sha256:11523775a19222ca1a489b107918fcfc6b8f82c3c0a546e353e56db07531549d",
    "Schedule.__gt__": "sha256:e70545b0c4551837664dfd3c684e8c835b11039a47898efd2c95df938cbcc6dc",
    "Schedule.__invert__": "sha256:474278997d954d91123564a0aa856a2ec834728fb3561ed94084cce5b7893b5e",
    "Schedule.__le__": "sha256:2c2318841748622514438612475423857eae8f569efe30da720d0f20fca8a21d",
    "Schedule.__lt__": "sha256:78d60d6a572ac4b18c51274a13bd5f089f183508f1315e82c4a00767febd87b2",
    "Schedule.__mul__": "sha256:341d9b28a235a5361ed9d16141e7ffdeac6f8933c1e420208fefb4714298029a",
    "Schedule.__or__": "sha256:cad1d342fc3e187970f1e3c996ddcd1d8ab53e184ca980cd948c2f9641ead350",
    "Schedule.__radd__": "sha256:ebaafbe81f9daa483e5f13afbd779c49f7308ef8b9551e10dac213d64a37c045",
    "Schedule.__rmul__": "sha256:279533a07d8189cc0a3f7fa57174faab7ea8500caf007ed8fc08ddb067353be2",
    "Schedule.__rsub__": "sha256:e84f78d3b0d4f00c202d644a04d90915a5302c87e28b1b37394c9504a4400047",
    "Schedule.__rtruediv__": "sha256:32d900f7d3189a35816962c0dae8c984f77bf619f9d75e74464a20803b090209",
    "Schedule.__sub__": "sha256:c963a4baf0da27e3807668ca2a93d212c429e167beebb78cdacce21be1c935dc",
    "Schedule.__truediv__": "sha256:cb9dd7d8cd8f71bb8ddff07c59959540a9a259b1caf6268128a68e17e375652c",
    "Schedule.element_eq": "sha256:e9c68d0b1d5292abffaf63d02594825784cb1f07bdff0151f3ba0fefbcd1dae4",
    "Schedule.element_max": "sha256:6287b64a5cf6b3db41eeae0fdeea354e3803debaac7591381e47939c312a087c",
    "Schedule.element_min": "sha256:56fdf733359e9c5e0fd96ec1d1288795816cc4eb3f34541a5b37917ee9297b36",
    "Schedule.element_ne": "sha256:32a6c5639c7affbca0e62ccf8ec70bb00ced57fbdf6e318cf1196eb7cd3f3e49",
    "Schedule.is_between": "sha256:d359b7f1264f8fedf1c8c448b7efcc6ac8179ec977bb1d3dd6f2e6f2ace4eb5f",
    "Schedule.is_negative": "sha256:49c07d553db98c166cf8cf61ea861b974fe140062ea9f2152a5f830ef6ca94c6",
    "Schedule.is_nonzero": "sha256:c4f3aa30304e19e7b367eb2bb0e49b29c63d7b2b42e7b956334b56ef61aa4b01",
    "Schedule.is_off": "sha256:b57679c27fb4fd20277b0bfb3942f0227ac4a7a69f779b66a4e5d495f19755fa",
    "Schedule.is_on": "sha256:5b1abd1e95bc9b66d360bfec68721a2c989e2b98f37c063935239deffe2a1423",
    "Schedule.is_positive": "sha256:54b471f257f020203e667a9496c22e38b0e021762b2b63dc793341013372a25c",
    "Schedule.is_zero": "sha256:b57679c27fb4fd20277b0bfb3942f0227ac4a7a69f779b66a4e5d495f19755fa",
    "Schedule.normalize_by_max": "sha256:b12e2905f36794820228b307d1ee4dacf368b1c00e19a26b7423acae87bab5d3",
    "Schedule.where": "sha256:d673aaaebf6468cbce8fe25610252702146eda0a155bef637940e26305108315",
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
EXPECTED_CASE_COUNT = 329
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
ANNUAL_LENGTH = 365
CONSUMER_CULTURE = "fr-FR"

ARITHMETIC_NONFINITE_ADAPTATIONS = {
    "Schedule.__add__": "nonfinite-result-schedule-add",
    "Schedule.__mul__": "nonfinite-result-schedule-mul",
    "Schedule.__radd__": "nonfinite-result-schedule-radd",
    "Schedule.__rmul__": "nonfinite-result-schedule-rmul",
    "Schedule.__rsub__": "nonfinite-result-schedule-rsub",
    "Schedule.__rtruediv__": "nonfinite-result-schedule-rtruediv",
    "Schedule.__sub__": "nonfinite-result-schedule-sub",
    "Schedule.__truediv__": "nonfinite-result-schedule-truediv",
}
NORMALIZE_ADAPTATION = "nonfinite-result-schedule-normalize-by-max"
WHERE_ADAPTATION = "deterministic-schedule-where-child-names"
EXPECTED_ADAPTATION_IDS = frozenset(
    (*ARITHMETIC_NONFINITE_ADAPTATIONS.values(), NORMALIZE_ADAPTATION, WHERE_ADAPTATION)
)

AUTO_NAME_PATTERN = re.compile(r"^0x[0-9a-f]+$")
RAW_AUTO_NAME_PATTERN = re.compile(r"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])")


def _load_rule_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_rule_set_operations_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_goniegonie_rule_set_operations_support",
        path,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load RuleSet oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
        or module.EXPECTED_CASE_COUNT != 334
    ):
        raise RuntimeError("RuleSet oracle support is not the pinned corpus.")
    return module


RULE = _load_rule_support()
strict_json_dumps = RULE.strict_json_dumps
canonical_sha256 = RULE.canonical_sha256
sha256_file = RULE.sha256_file
load_json_without_duplicates = RULE.load_json_without_duplicates
compact_values = RULE.compact_values
require_finite_scalar = RULE.require_finite_scalar
tagged_observation_value = RULE.tagged_observation_value
SCHEDULE_TEMPLATES = {
    **RULE.SCHEDULE_TEMPLATES,
    "condition-all-false": {
        "name": "ConditionAllFalse",
        "pattern": (0,),
        "type": "onoff",
        "unit": None,
    },
}
SLOT_KEYS = RULE.SLOT_KEYS
OVERRIDE_KEYS = RULE.OVERRIDE_KEYS
SIGNED_INT64_MIN = RULE.SIGNED_INT64_MIN
SIGNED_INT64_MAX = RULE.SIGNED_INT64_MAX
INEXACT_BINARY64_INTEGER = RULE.INEXACT_BINARY64_INTEGER
UNBOUNDED_INTEGER = RULE.UNBOUNDED_INTEGER

NONFINITE_TOKENS = (
    "negative-infinity",
    "nan",
    "positive-infinity",
)
EXPECTED_WHERE_NONFINITE_MATRIX_IDS = frozenset(
    f"nonfinite.where.{selection}-{branch}.{token}"
    for selection in ("selected", "unselected")
    for branch in ("false", "true")
    for token in NONFINITE_TOKENS
)
EXPECTED_WHERE_BOOL_CASE_IDS = frozenset(
    {
        "where.bool.selected-false-value",
        "where.bool.selected-true-value",
    }
)
EXPECTED_WHERE_MIXED_ANNUAL_CASE_IDS = frozenset(
    {
        "where.branch.ruleset-schedule.inferred",
        "where.branch.schedule-ruleset.inferred",
    }
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    """Fail-close the full inventory, then bind exactly 28 Schedule symbols."""

    base = RULE.DAY.load_exact_inventory(path, upstream_commit)
    inventory = load_json_without_duplicates(path)
    target_symbols = [
        item
        for item in inventory["symbols"]
        if item["path"] == SOURCE_PATH and item["symbol"] in TARGET_SYMBOLS
    ]
    if [item["symbol"] for item in target_symbols] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover the 28 Schedule symbols.")
    for item in target_symbols:
        if item["symbol_hash"] != EXPECTED_SYMBOL_HASHES[item["symbol"]]:
            raise SystemExit(f"The inventory hash for {item['symbol']} is not pinned.")
    return {
        "content_sha256": base["content_sha256"],
        "file": base["file"],
        "symbols": target_symbols,
    }


def schedule_ref(name: str) -> tuple[str, str]:
    return ("schedule", name)


def ruleset_ref(name: str) -> tuple[str, str]:
    return ("ruleset", name)


def day_schedule_ref(name: str) -> tuple[str, str]:
    return ("day-schedule", name)


def _registered_expectation(
    adaptation: str,
    outcome: str,
    policy: str,
    *,
    error_category: str | None = None,
    result_name: str | None = None,
) -> dict[str, str]:
    result = {"adaptation": adaptation, "outcome": outcome, "policy": policy}
    if error_category is not None:
        result["error_category"] = error_category
    if result_name is not None:
        result["result_name"] = result_name
    return result


def _schedule_expectation(
    source: dict[str, str] | None,
    symbol: str,
) -> dict[str, str] | None:
    if source is None:
        return None
    result = dict(source)
    if symbol in ARITHMETIC_NONFINITE_ADAPTATIONS:
        result["adaptation"] = ARITHMETIC_NONFINITE_ADAPTATIONS[symbol]
    elif symbol == "Schedule.normalize_by_max":
        result["adaptation"] = NORMALIZE_ADAPTATION
    elif symbol == "Schedule.where":
        result["adaptation"] = WHERE_ADAPTATION
    else:
        raise RuntimeError(f"Unmapped Schedule adaptation for {symbol}.")
    return result


def _promote_input(
    input_name: str,
    specification: tuple[str, Any],
) -> tuple[str, Any]:
    kind, value = specification
    if kind == "ruleset":
        # The primary annual corpus exercises Schedule-to-Schedule dispatch.
        # Dedicated additions below preserve RuleSet and DaySchedule branches
        # accepted specifically by Schedule.where.
        return ("schedule", value)
    return (kind, value)


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions: list[dict[str, Any]] = []
    for source in RULE.case_definitions():
        # Schedule.element_min/max intentionally accept Schedule only.  The 24
        # RuleSet scalar repair cases therefore do not describe this public API.
        if source["repair_reference"]:
            continue

        symbol = source["symbol"].replace("RuleSet.", "Schedule.", 1)
        identifier = source["id"].replace("ruleset", "schedule")
        definitions.append(
            {
                "expected_dotnet": _schedule_expectation(
                    source["expected_dotnet"], symbol
                ),
                "expected_exception": source["expected_exception"],
                "id": identifier,
                "inputs": {
                    name: _promote_input(name, specification)
                    for name, specification in source["inputs"].items()
                },
                "repair_reference": False,
                "symbol": symbol,
            }
        )

    branch_additions = (
        (
            "where.branch.ruleset-ruleset.inferred",
            ruleset_ref("where-true"),
            ruleset_ref("where-false"),
            None,
        ),
        (
            "where.branch.ruleset-day.inferred",
            ruleset_ref("where-true"),
            day_schedule_ref("where-false"),
            None,
        ),
        (
            "where.branch.day-ruleset.inferred",
            day_schedule_ref("where-true"),
            ruleset_ref("where-false"),
            None,
        ),
        (
            "where.branch.scalar-ruleset.explicit-fraction",
            ("scalar", 0.4),
            ruleset_ref("where-false"),
            ("schedule-type", "fraction"),
        ),
        (
            "where.branch.schedule-ruleset.inferred",
            schedule_ref("where-true"),
            ruleset_ref("where-false"),
            None,
        ),
        (
            "where.branch.ruleset-schedule.inferred",
            ruleset_ref("where-true"),
            schedule_ref("where-false"),
            None,
        ),
    )
    for identifier, if_true, if_false, requested_type in branch_additions:
        definitions.append(
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "returned",
                    "deterministic-period-child-names",
                ),
                "expected_exception": None,
                "id": identifier,
                "inputs": {
                    "condition": schedule_ref("condition"),
                    "if_false": if_false,
                    "if_true": if_true,
                    "name": ("text", "AnnualBranchWhere"),
                    "type": requested_type or ("none", None),
                },
                "repair_reference": False,
                "symbol": "Schedule.where",
            }
        )

    # The promoted RuleSet corpus already contains the selected true-branch
    # positive-infinity case.  Complete the three-token x two-branch-position
    # x selected/unselected matrix here.  A genuinely all-false annual
    # condition is required: a mixed condition would select the allegedly
    # unselected branch somewhere in the 365-day result and could not prove
    # lazy native validation.
    existing_nonfinite = (
        "selected",
        "true",
        "positive-infinity",
    )
    for selection in ("selected", "unselected"):
        for branch in ("false", "true"):
            for token in NONFINITE_TOKENS:
                if (selection, branch, token) == existing_nonfinite:
                    continue
                selected = selection == "selected"
                true_branch = branch == "true"
                condition_template = (
                    "condition-all-true"
                    if selected == true_branch
                    else "condition-all-false"
                )
                if_true = (
                    ("nonfinite", token) if true_branch else ("scalar", 0)
                )
                if_false = (
                    ("scalar", 0) if true_branch else ("nonfinite", token)
                )
                token_name = token.replace("-", " ").title().replace(" ", "")
                definitions.append(
                    {
                        "expected_dotnet": _registered_expectation(
                            WHERE_ADAPTATION,
                            "raised" if selected else "returned",
                            (
                                "reject-nonfinite-result"
                                if selected
                                else "deterministic-slot-names"
                            ),
                            error_category="domain" if selected else None,
                        ),
                        "expected_exception": None,
                        "id": f"nonfinite.where.{selection}-{branch}.{token}",
                        "inputs": {
                            "condition": schedule_ref(condition_template),
                            "if_false": if_false,
                            "if_true": if_true,
                            "name": (
                                "text",
                                f"WhereNonfinite{selection.title()}"
                                f"{branch.title()}{token_name}",
                            ),
                            "type": ("none", None),
                        },
                        "repair_reference": False,
                        "symbol": "Schedule.where",
                    }
                )

    definitions.extend(
        (
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "returned",
                    "deterministic-slot-names",
                ),
                "expected_exception": None,
                "id": "where.bool.selected-true-value",
                "inputs": {
                    "condition": schedule_ref("condition-all-true"),
                    "if_false": ("scalar", 0),
                    "if_true": ("scalar", True),
                    "name": ("text", "WhereBoolTrue"),
                    "type": ("none", None),
                },
                "repair_reference": False,
                "symbol": "Schedule.where",
            },
            {
                "expected_dotnet": _registered_expectation(
                    WHERE_ADAPTATION,
                    "returned",
                    "deterministic-slot-names",
                ),
                "expected_exception": None,
                "id": "where.bool.selected-false-value",
                "inputs": {
                    "condition": schedule_ref("condition-all-true"),
                    "if_false": ("scalar", 1),
                    "if_true": ("scalar", False),
                    "name": ("text", "WhereBoolFalse"),
                    "type": ("none", None),
                },
                "repair_reference": False,
                "symbol": "Schedule.where",
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
            f"Expected {EXPECTED_CASE_COUNT} Schedule operation cases, got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("Schedule operation case identifiers are not unique and sorted.")
    if {item["symbol"] for item in definitions} != set(TARGET_SYMBOLS):
        raise RuntimeError("Schedule operation cases do not cover exactly 28 symbols.")

    actual_nonfinite_matrix_ids = {
        item["id"]
        for item in definitions
        if item["id"].startswith("nonfinite.where.")
    }
    if actual_nonfinite_matrix_ids != EXPECTED_WHERE_NONFINITE_MATRIX_IDS:
        raise RuntimeError("Schedule.where does not contain the exact non-finite matrix.")
    actual_bool_ids = {
        item["id"]
        for item in definitions
        if item["id"].startswith("where.bool.")
    }
    if actual_bool_ids != EXPECTED_WHERE_BOOL_CASE_IDS:
        raise RuntimeError("Schedule.where does not contain the exact bool boundary cases.")
    if not EXPECTED_WHERE_MIXED_ANNUAL_CASE_IDS.issubset(identifiers):
        raise RuntimeError("Schedule.where is missing a mixed annual/RuleSet branch direction.")

    single_inputs = {
        "Schedule.__invert__",
        "Schedule.is_negative",
        "Schedule.is_nonzero",
        "Schedule.is_off",
        "Schedule.is_on",
        "Schedule.is_positive",
        "Schedule.is_zero",
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
        if definition["repair_reference"] is not False:
            raise RuntimeError("Schedule operations have no registered repair reference.")
        symbol = definition["symbol"]
        expected_inputs = (
            {"receiver"}
            if symbol in single_inputs
            else {"include_max", "include_min", "max_value", "min_value", "receiver"}
            if symbol == "Schedule.is_between"
            else {"new_name", "receiver"}
            if symbol == "Schedule.normalize_by_max"
            else {"condition", "if_false", "if_true", "name", "type"}
            if symbol == "Schedule.where"
            else {"other", "receiver"}
        )
        if set(definition["inputs"]) != expected_inputs:
            raise RuntimeError(
                f"Case {definition['id']!r} inputs are not exact for {symbol}."
            )

        for input_name, specification in definition["inputs"].items():
            if not isinstance(input_name, str) or not isinstance(specification, tuple):
                raise RuntimeError(f"Case {definition['id']!r} has an invalid input.")
            kind, value = specification
            base_name = value.split("@", 1)[0] if isinstance(value, str) else value
            if kind in {"schedule", "ruleset", "day-schedule"}:
                if base_name not in SCHEDULE_TEMPLATES:
                    raise RuntimeError(
                        f"Case {definition['id']!r} uses an unknown template."
                    )
                if kind != "schedule" and symbol != "Schedule.where":
                    raise RuntimeError(
                        "RuleSet and DaySchedule inputs are only valid for Schedule.where."
                    )
            elif kind == "scalar":
                require_finite_scalar(value, f"Case {definition['id']} input {input_name}")
            elif kind == "nonfinite":
                RULE.DAY.nonfinite(value)
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
            if not isinstance(expectation, dict) or not {
                "adaptation",
                "outcome",
                "policy",
            }.issubset(expectation) or not set(expectation).issubset(
                {
                    "adaptation",
                    "error_category",
                    "outcome",
                    "policy",
                    "result_name",
                }
            ):
                raise RuntimeError(
                    f"Case {definition['id']!r} has malformed .NET expectation keys."
                )
            if expectation["adaptation"] not in EXPECTED_ADAPTATION_IDS:
                raise RuntimeError(f"Case {definition['id']!r} has unknown adaptation.")
            if expectation["outcome"] not in {"raised", "returned"}:
                raise RuntimeError(f"Case {definition['id']!r} has invalid .NET outcome.")
            if expectation["policy"] not in {
                "deterministic-period-child-names",
                "deterministic-slot-names",
                "reject-invalid-name",
                "reject-nonfinite-result",
                "trim-name-and-deterministic-slot-names",
                "trim-result-name",
            }:
                raise RuntimeError(f"Case {definition['id']!r} has invalid .NET policy.")
            error_category = expectation.get("error_category")
            if expectation["outcome"] == "raised":
                if error_category not in {
                    "divide-by-zero",
                    "domain",
                    "schedule-operation",
                    "type",
                }:
                    raise RuntimeError(
                        f"Case {definition['id']!r} has invalid error category."
                    )
            elif error_category is not None:
                raise RuntimeError(f"Case {definition['id']!r} has invalid error category.")
            policy_outcomes = {
                "deterministic-period-child-names": "returned",
                "deterministic-slot-names": "returned",
                "reject-invalid-name": "raised",
                "reject-nonfinite-result": "raised",
                "trim-name-and-deterministic-slot-names": "returned",
                "trim-result-name": "returned",
            }
            if expectation["outcome"] != policy_outcomes[expectation["policy"]]:
                raise RuntimeError(
                    f"Case {definition['id']!r} has a policy/outcome mismatch."
                )
            if expectation["policy"] == "reject-invalid-name" and (
                error_category != "type"
            ):
                raise RuntimeError(
                    f"Case {definition['id']!r} has malformed name rejection."
                )
            if expectation["policy"] == "reject-nonfinite-result" and (
                error_category != "domain"
            ):
                raise RuntimeError(
                    f"Case {definition['id']!r} has malformed non-finite rejection."
                )
            trim_policies = {
                "trim-name-and-deterministic-slot-names",
                "trim-result-name",
            }
            if expectation["policy"] in trim_policies:
                result_name = expectation.get("result_name")
                if (
                    not isinstance(result_name, str)
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

            if definition["id"] in EXPECTED_WHERE_NONFINITE_MATRIX_IDS:
                match = re.fullmatch(
                    r"nonfinite\.where\.(selected|unselected)-(true|false)\."
                    r"(negative-infinity|nan|positive-infinity)",
                    definition["id"],
                )
                if match is None:
                    raise RuntimeError("A non-finite matrix identifier is malformed.")
                selection, branch, token = match.groups()
                selected = selection == "selected"
                true_branch = branch == "true"
                expected_condition = (
                    "condition-all-true"
                    if selected == true_branch
                    else "condition-all-false"
                )
                inputs = definition["inputs"]
                expected_nonfinite_branch = "if_true" if true_branch else "if_false"
                expected_finite_branch = "if_false" if true_branch else "if_true"
                if (
                    inputs["condition"] != schedule_ref(expected_condition)
                    or inputs[expected_nonfinite_branch] != ("nonfinite", token)
                    or inputs[expected_finite_branch] != ("scalar", 0)
                    or inputs["type"] != ("none", None)
                ):
                    raise RuntimeError(
                        f"Case {definition['id']!r} has a mislabeled selection matrix cell."
                    )
                expected_matrix_outcome = _registered_expectation(
                    WHERE_ADAPTATION,
                    "raised" if selected else "returned",
                    (
                        "reject-nonfinite-result"
                        if selected
                        else "deterministic-slot-names"
                    ),
                    error_category="domain" if selected else None,
                )
                if expectation != expected_matrix_outcome:
                    raise RuntimeError(
                        f"Case {definition['id']!r} has an invalid matrix expectation."
                    )
        elif any(kind == "nonfinite" for kind, _ in definition["inputs"].values()):
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


def _temporal_ranges(template_name: str) -> tuple[tuple[int, int], ...]:
    base_name, _ = RULE._split_template_name(template_name)
    if base_name in {"condition", "condition-all-false", "condition-all-true"}:
        return ((0, 119), (120, 249), (250, 364))
    if base_name == "where-true":
        return ((0, 59), (60, 199), (200, 364))
    if base_name == "where-false":
        return ((0, 149), (150, 299), (300, 364))
    if base_name.endswith("-right"):
        return ((0, 44), (45, 179), (180, 289), (290, 364))
    return ((0, 89), (90, 239), (240, 364))


def create_ruleset_variant(
    template_name: str,
    variant: int,
    DaySchedule: type,
    RuleSet: type,
    ScheduleType: type,
) -> Any:
    base_name, topology_name = RULE._split_template_name(template_name)
    if base_name == "condition-all-false":
        topology = RULE._topology_for_template(
            "condition-all-true",
            topology_name,
        )
        slots = {
            slot_name: DaySchedule(
                f"ConditionAllFalseRules:{slot_name}",
                [0] * 144,
                type=ScheduleType.ONOFF,
                unit=None,
            )
            if slot_name in {"weekdays", "weekends"} or slot_name in topology
            else None
            for slot_name in SLOT_KEYS
        }
        source = RuleSet(
            "ConditionAllFalseRules",
            **slots,
            type=ScheduleType.ONOFF,
        )
    else:
        source = RULE.create_ruleset(
            template_name,
            DaySchedule,
            RuleSet,
            ScheduleType,
        )
    slots: dict[str, Any] = {}
    for slot_name, day_schedule in source.to_dict().items():
        slots[slot_name] = (
            None
            if day_schedule is None
            else DaySchedule(
                f"{day_schedule.name}:AnnualP{variant + 1}",
                # Temporal variants deliberately differ by identity and name,
                # not values.  This retains every pinned domain/error case
                # while still exposing annual compact-period unification.
                list(day_schedule.data),
                type=day_schedule.type,
                unit=day_schedule.unit,
            )
        )
    return RuleSet(
        f"{source.name}:AnnualP{variant + 1}",
        **slots,
        type=source.type,
    )


def create_schedule(
    template_name: str,
    DaySchedule: type,
    RuleSet: type,
    Schedule: type,
    ScheduleType: type,
) -> Any:
    ranges = _temporal_ranges(template_name)
    variants = [
        create_ruleset_variant(
            template_name,
            index,
            DaySchedule,
            RuleSet,
            ScheduleType,
        )
        for index in range(len(ranges))
    ]
    annual: list[Any] = [None] * ANNUAL_LENGTH
    for index, (start, end) in enumerate(ranges):
        annual[start : end + 1] = [variants[index]] * (end - start + 1)
    if any(item is None for item in annual):
        raise RuntimeError("An annual template does not cover exactly 365 days.")
    safe_name = template_name.replace("@", "-").replace("_", "-")
    return Schedule(
        f"{safe_name}:Annual",
        annual,
        type=variants[0].type,
    )


def _identity_periods(schedule: Any) -> list[tuple[int, int, Any]]:
    periods: list[tuple[int, int, Any]] = []
    for index, ruleset in enumerate(schedule.data):
        if index == 0 or ruleset is not schedule.data[index - 1]:
            periods.append((index, index, ruleset))
        else:
            start, _, previous = periods[-1]
            periods[-1] = (start, index, previous)
    return periods


def schedule_descriptor(
    schedule: Any,
    Schedule: type,
    RuleSet: type,
    DaySchedule: type,
    *,
    child_name_policy: str = "literal",
    allow_nonfinite: bool = False,
) -> dict[str, Any]:
    if not isinstance(schedule, Schedule):
        raise RuntimeError("A successful annual operation returned the wrong type.")
    if len(schedule.data) != ANNUAL_LENGTH:
        raise RuntimeError("An annual Schedule descriptor is not length 365.")
    periods = _identity_periods(schedule)
    native_periods = schedule.compactize()
    if len(native_periods) != len(periods):
        raise RuntimeError("The native compact-period count differs from the annual sequence.")
    for (start, end, ruleset), (native_start, native_end, native_ruleset) in zip(
        periods,
        native_periods,
    ):
        if (
            native_start != Schedule.TIME_TUPLE[start]
            or native_end != Schedule.TIME_TUPLE[end]
            or native_ruleset is not ruleset
        ):
            raise RuntimeError("Native compact-period topology differs from the 365-day sequence.")
    period_descriptors = []
    sequence_ranges = []
    for period_index, (start, end, ruleset) in enumerate(periods):
        start_date = Schedule.TIME_TUPLE[start].isoformat()
        end_date = Schedule.TIME_TUPLE[end].isoformat()
        period_descriptors.append(
            {
                "end": end_date,
                "end_index": end,
                "rule_set": RULE.ruleset_descriptor(
                    ruleset,
                    RuleSet,
                    DaySchedule,
                    child_name_policy=child_name_policy,
                    allow_nonfinite=allow_nonfinite,
                ),
                "start": start_date,
                "start_index": start,
            }
        )
        sequence_ranges.append(
            {
                "end_index": end,
                "period_index": period_index,
                "start_index": start,
            }
        )
    if periods[0][0] != 0 or periods[-1][1] != ANNUAL_LENGTH - 1:
        raise RuntimeError("Compact annual periods do not cover the full year.")
    return {
        "annual_rule_set_sequence": {
            "encoding": "period-index-ranges",
            "length": ANNUAL_LENGTH,
            "ranges": sequence_ranges,
        },
        "compact_period_count": len(periods),
        "kind": "schedule",
        "name": RULE._name_descriptor(schedule.name, "literal"),
        "periods": period_descriptors,
        "schedule_type": schedule.type.value,
    }


def input_descriptor(
    value: Any,
    DaySchedule: type,
    RuleSet: type,
    Schedule: type,
    ScheduleType: type,
) -> dict[str, Any]:
    if isinstance(value, Schedule):
        return schedule_descriptor(value, Schedule, RuleSet, DaySchedule)
    if isinstance(value, RuleSet):
        return RULE.ruleset_descriptor(value, RuleSet, DaySchedule)
    if isinstance(value, DaySchedule):
        return RULE.schedule_descriptor(
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
    Schedule: type,
    ScheduleType: type,
) -> Any:
    kind, value = specification
    if kind == "schedule":
        return create_schedule(value, DaySchedule, RuleSet, Schedule, ScheduleType)
    if kind == "ruleset":
        return RULE.create_ruleset(value, DaySchedule, RuleSet, ScheduleType)
    if kind == "day-schedule":
        return RULE.create_day_schedule(value, "weekdays", DaySchedule, ScheduleType)
    if kind == "schedule-type":
        return ScheduleType(value)
    if kind == "nonfinite":
        return {
            "nan": math.nan,
            "negative-infinity": -math.inf,
            "positive-infinity": math.inf,
        }[value]
    return value


def schedule_state(schedule: Any) -> tuple[Any, ...]:
    identities = tuple(id(ruleset) for ruleset in schedule.data)
    seen: set[int] = set()
    unique_states: list[tuple[int, tuple[Any, ...]]] = []
    for ruleset in schedule.data:
        identity = id(ruleset)
        if identity not in seen:
            seen.add(identity)
            unique_states.append((identity, RULE.ruleset_state(ruleset)))
    return (schedule.name, schedule.type, identities, tuple(unique_states))


def _schedule_postcondition(
    schedule: Any,
    before: tuple[Any, ...],
    Schedule: type,
    RuleSet: type,
    DaySchedule: type,
) -> dict[str, Any]:
    after = schedule_state(schedule)
    result: dict[str, Any] = {
        "annual_rule_set_identities": (
            "preserved" if before[2] == after[2] else "changed"
        ),
        "identity": "preserved",
        "status": "unchanged" if before == after else "changed",
    }
    if before != after:
        result["value"] = schedule_descriptor(
            schedule,
            Schedule,
            RuleSet,
            DaySchedule,
            allow_nonfinite=True,
        )
    return result


def invoke_case(symbol: str, inputs: dict[str, Any], Schedule: type) -> Any:
    receiver = inputs.get("receiver")
    other = inputs.get("other")
    method = symbol.removeprefix("Schedule.")
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
        return Schedule.where(
            inputs["condition"],
            inputs["if_true"],
            inputs["if_false"],
            name=inputs["name"],
            type=inputs["type"],
        )
    raise RuntimeError(f"Unsupported oracle symbol dispatch: {symbol}.")


def _outcome_observation(
    action: Callable[[], Any],
    *,
    expected_exception: str | None,
    child_name_policy: str,
    actual_inputs: dict[str, Any],
    schedule_inputs: dict[str, Any],
    schedule_before: dict[str, tuple[Any, ...]],
    ruleset_inputs: dict[str, Any],
    ruleset_before: dict[str, tuple[Any, ...]],
    day_inputs: dict[str, Any],
    day_before: dict[str, tuple[Any, ...]],
    Schedule: type,
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
            raise RuntimeError(f"Returned but expected {expected_exception}.")
        observation = {
            "outcome": "returned",
            "result": schedule_descriptor(
                result,
                Schedule,
                RuleSet,
                DaySchedule,
                child_name_policy=child_name_policy,
                allow_nonfinite=True,
            ),
            "result_identity": (
                "receiver" if result is actual_inputs.get("receiver") else "new"
            ),
        }

    observation["schedule_inputs_after"] = {
        name: _schedule_postcondition(
            value,
            schedule_before[name],
            Schedule,
            RuleSet,
            DaySchedule,
        )
        for name, value in schedule_inputs.items()
    }
    observation["ruleset_inputs_after"] = {
        name: RULE._ruleset_postcondition(
            value,
            ruleset_before[name],
            RuleSet,
            DaySchedule,
        )
        for name, value in ruleset_inputs.items()
    }
    observation["day_schedule_inputs_after"] = {
        name: RULE._day_schedule_postcondition(value, day_before[name], DaySchedule)
        for name, value in day_inputs.items()
    }
    return observation


def execute_case(
    definition: dict[str, Any],
    DaySchedule: type,
    RuleSet: type,
    Schedule: type,
    ScheduleType: type,
) -> dict[str, Any]:
    actual_inputs = {
        name: resolve_input(
            specification,
            DaySchedule,
            RuleSet,
            Schedule,
            ScheduleType,
        )
        for name, specification in definition["inputs"].items()
    }
    descriptors = {
        name: input_descriptor(
            value,
            DaySchedule,
            RuleSet,
            Schedule,
            ScheduleType,
        )
        for name, value in actual_inputs.items()
    }
    schedule_inputs = {
        name: value for name, value in actual_inputs.items() if isinstance(value, Schedule)
    }
    ruleset_inputs = {
        name: value for name, value in actual_inputs.items() if isinstance(value, RuleSet)
    }
    day_inputs = {
        name: value
        for name, value in actual_inputs.items()
        if isinstance(value, DaySchedule)
    }
    schedule_before = {
        name: schedule_state(value) for name, value in schedule_inputs.items()
    }
    ruleset_before = {
        name: RULE.ruleset_state(value) for name, value in ruleset_inputs.items()
    }
    day_before = {
        name: RULE.day_schedule_state(value) for name, value in day_inputs.items()
    }
    child_name_policy = (
        "runtime-identity-hex"
        if definition["symbol"] == "Schedule.where"
        else "literal"
    )
    try:
        observation = _outcome_observation(
            lambda: invoke_case(definition["symbol"], actual_inputs, Schedule),
            expected_exception=definition["expected_exception"],
            child_name_policy=child_name_policy,
            actual_inputs=actual_inputs,
            schedule_inputs=schedule_inputs,
            schedule_before=schedule_before,
            ruleset_inputs=ruleset_inputs,
            ruleset_before=ruleset_before,
            day_inputs=day_inputs,
            day_before=day_before,
            Schedule=Schedule,
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
    if definition["expected_dotnet"] is not None:
        case["expected_dotnet"] = definition["expected_dotnet"]
    return case


def summarize_cases(cases: list[dict[str, Any]]) -> dict[str, Any]:
    adapted = [item for item in cases if "expected_dotnet" in item]
    adaptations = sorted(
        {item["expected_dotnet"]["adaptation"] for item in adapted}
    )
    if set(adaptations) != set(EXPECTED_ADAPTATION_IDS):
        raise RuntimeError("The generated oracle does not bind exactly ten adaptations.")
    return {
        "adaptation_case_count": len(adapted),
        "adaptation_ids": adaptations,
        "case_count": len(cases),
        "expected_dotnet_outcomes": {
            outcome: sum(
                item["expected_dotnet"]["outcome"] == outcome for item in adapted
            )
            for outcome in ("raised", "returned")
        },
        "observed_outcomes": {
            outcome: sum(item["observation"]["outcome"] == outcome for item in cases)
            for outcome in ("raised", "returned")
        },
        "repair_reference_count": 0,
    }


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import DaySchedule, RuleSet, Schedule, ScheduleType

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
    if (
        Schedule.FIXED_LENGTH != ANNUAL_LENGTH
        or len(Schedule.TIME_TUPLE) != ANNUAL_LENGTH
        or Schedule.TIME_TUPLE[0].month != 1
        or Schedule.TIME_TUPLE[0].day != 1
        or Schedule.TIME_TUPLE[-1].month != 12
        or Schedule.TIME_TUPLE[-1].day != 31
    ):
        raise SystemExit("Pinned Schedule annual grid constants are not exact.")

    definitions = case_definitions()
    cases = [
        execute_case(item, DaySchedule, RuleSet, Schedule, ScheduleType)
        for item in definitions
    ]
    if [item["id"] for item in cases] != sorted(item["id"] for item in cases):
        raise RuntimeError("Generated Schedule operation cases are not sorted.")

    result = {
        "cases": cases,
        "consumer_contract": {
            "annual_length": ANNUAL_LENGTH,
            "culture": CONSUMER_CULTURE,
            "period_endpoints": "inclusive-iso-date",
            "scalar_names": "python-str-culture-invariant",
        },
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
        raise RuntimeError("A raw runtime identity name entered the Schedule oracle.")
    return result


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the Schedule oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for the Schedule oracle.")
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

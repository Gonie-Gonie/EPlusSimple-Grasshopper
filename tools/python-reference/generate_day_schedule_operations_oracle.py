"""Generate pinned InvisibleDragon DaySchedule operation observations.

Run this only through ``bootstrap_reference.py`` so imports resolve from the
exact pinned upstream source and dependency tree.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import sys
from typing import Any


SCHEMA = "dragons.invisibledragon.day-schedule-operations-oracle.v1"
INVENTORY_SCHEMA = "dragons.upstream-public-symbol-inventory.v2"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "DaySchedule.__add__": (
        "sha256:f2cc675e8c909fae4fa4461fb915249045e55b0e6d7b754575b00ba2cecf7610"
    ),
    "DaySchedule.__and__": (
        "sha256:28b1aedc4bfa287ba2a8cb24dc3146eed48c955ebfcf167f85ea1c58dddcd238"
    ),
    "DaySchedule.__ge__": (
        "sha256:ea94e3369cd6b4314bae0a24563fb18cf478872702352f5439ee030b2024ada0"
    ),
    "DaySchedule.__gt__": (
        "sha256:5b9a41353d9b00038482ace45403592e4f63c95442789d384b3f06831bebdee1"
    ),
    "DaySchedule.__invert__": (
        "sha256:0920f4745c4f599b013798696350c268640ce02822d4bcc405fdd5fea20916e4"
    ),
    "DaySchedule.__le__": (
        "sha256:5c35fbea76e3e4da3f516363b17530e1972fde58b452ee210c22cb5e8d40f68f"
    ),
    "DaySchedule.__lt__": (
        "sha256:495dc27481315dcb97554de321b8e45a12379a1747bbb8d63bc5cbae2af46aee"
    ),
    "DaySchedule.__mul__": (
        "sha256:c8bbdbc48d7e465d159ab6b829609d582004ead56657b7475b7caeb552454aea"
    ),
    "DaySchedule.__or__": (
        "sha256:1bf84ec95560db45c4e29a34678c9ff7edad4906bbb3a231bc47aff0481f6fce"
    ),
    "DaySchedule.__radd__": (
        "sha256:5a5ededeac5428a72339d7725836d9062c10a107cbc821e2659160c73668831f"
    ),
    "DaySchedule.__rmul__": (
        "sha256:87f6bef2e0be21121fdc990138093d2d07cc225d5edaff5d2129660a902a4e7f"
    ),
    "DaySchedule.__rsub__": (
        "sha256:a1fa02e18d86596b88fdebeceedaa48459b6e1068c301c93ba26170f41c37418"
    ),
    "DaySchedule.__rtruediv__": (
        "sha256:9bc405fae0ca82d5a0ab953af9197871e5c71248a267d07d408071d44abbb374"
    ),
    "DaySchedule.__sub__": (
        "sha256:55fed2bd2b5cbb9b3ed69e4e8c1da4207d382e78ede3fb674750e395f4a1c4e8"
    ),
    "DaySchedule.__truediv__": (
        "sha256:d4bf77a6d67c06dfa3076336eac461f80855fdbfb2f72d46115cd8e67c10ca0b"
    ),
    "DaySchedule.element_eq": (
        "sha256:ef89564449828b40d613fe45cb0f86fe06727df8af9b4f2fa967437a68a1e139"
    ),
    "DaySchedule.element_max": (
        "sha256:6bf704e3d166ef0957b56ff7cd2b0841a32b80e8c1783e12cd698a183cb20f05"
    ),
    "DaySchedule.element_min": (
        "sha256:ac3a8af2147d4a6fb6c85812769b0eea7ddf2c96342d28a0b72c659b0ed1623c"
    ),
    "DaySchedule.element_ne": (
        "sha256:93fa9bc6ed088f976183ab9cf80f0388eb4dedffa2d72422d1ca7fef37987493"
    ),
    "DaySchedule.is_between": (
        "sha256:44e0340fd4f8c80dd25355692d36795370a2957bf582a23243d01a6c38736b29"
    ),
    "DaySchedule.is_negative": (
        "sha256:556646a16befc126236753ebe15e3e626de264cc292fa7aeafeccc87f1d6230a"
    ),
    "DaySchedule.is_nonzero": (
        "sha256:c63f38e66d2c02edbc31f84afe616eda8196ac16dd71ee7a198c5ac12c8105a6"
    ),
    "DaySchedule.is_off": (
        "sha256:c26b058f1987f339e99fb32a831d3e7861f856b5a86ad618f86b4e55335060b5"
    ),
    "DaySchedule.is_on": (
        "sha256:1125889a0369f6326f366dbc743ca024aa4f59ccbf04f21448237818620958a4"
    ),
    "DaySchedule.is_positive": (
        "sha256:95ca3954321930aceddb80707aedfff689163c7fcc4cce416cbe4af558801f8c"
    ),
    "DaySchedule.is_zero": (
        "sha256:c26b058f1987f339e99fb32a831d3e7861f856b5a86ad618f86b4e55335060b5"
    ),
    "DaySchedule.normalize_by_max": (
        "sha256:dd857df94e8e53388add91cb81cb88d6e8de762ee49553fdd52f37979f5259c7"
    ),
    "DaySchedule.where": (
        "sha256:33c2a95572c296a03947b50bb90895168c069b0f054bac875f4039bb8232595c"
    ),
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
EXPECTED_CASE_COUNT = 321
INEXACT_BINARY64_INTEGER = (1 << 53) + 1
UNBOUNDED_INTEGER = 10**400
SIGNED_INT64_MIN = -(1 << 63)
SIGNED_INT64_MAX = (1 << 63) - 1
FLOAT_MAX = float.fromhex("0x1.fffffffffffffp+1023")
FLOAT_MIN_SUBNORMAL = float.fromhex("0x0.0000000000001p-1022")
ARITHMETIC_NONFINITE_ADAPTATIONS = {
    "DaySchedule.__add__": "nonfinite-result-day-schedule-add",
    "DaySchedule.__mul__": "nonfinite-result-day-schedule-mul",
    "DaySchedule.__radd__": "nonfinite-result-day-schedule-radd",
    "DaySchedule.__rmul__": "nonfinite-result-day-schedule-rmul",
    "DaySchedule.__rsub__": "nonfinite-result-day-schedule-rsub",
    "DaySchedule.__rtruediv__": "nonfinite-result-day-schedule-rtruediv",
    "DaySchedule.__sub__": "nonfinite-result-day-schedule-sub",
    "DaySchedule.__truediv__": "nonfinite-result-day-schedule-truediv",
}
DOTNET_ADAPTATION_ERROR_CATEGORIES = {
    **{value: {"domain"} for value in ARITHMETIC_NONFINITE_ADAPTATIONS.values()},
    "nonfinite-result-day-schedule-element-max": {"domain"},
    "nonfinite-result-day-schedule-element-min": {"domain"},
    "deterministic-day-schedule-where-name": {"domain", "schedule-operation"},
    "immutable-day-schedule-normalize-by-max": {"domain"},
}

INVENTORY_KEYS = {
    "content_sha256",
    "files",
    "schema",
    "scope_sha256",
    "summary",
    "symbols",
    "upstream_commit",
}
FILE_KEYS = {"ast_hash", "content_hash", "path"}
SYMBOL_KEYS = {
    "body_hash",
    "kind",
    "path",
    "signature_hash",
    "symbol",
    "symbol_hash",
}
SUMMARY_KEYS = {"kind_counts", "public_symbol_count", "python_file_count"}
SHA256_PATTERN = re.compile(r"^sha256:[0-9a-f]{64}$")
AUTO_NAME_PATTERN = re.compile(r"^0x[0-9a-f]+$")
RAW_AUTO_NAME_PATTERN = re.compile(r"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])")
SINGLE_SCHEDULE_INPUT_SYMBOLS = {
    "DaySchedule.__invert__",
    "DaySchedule.is_negative",
    "DaySchedule.is_nonzero",
    "DaySchedule.is_off",
    "DaySchedule.is_on",
    "DaySchedule.is_positive",
    "DaySchedule.is_zero",
}
BETWEEN_INPUTS = {
    "include_max",
    "include_min",
    "max_value",
    "min_value",
    "receiver",
}
NORMALIZE_INPUTS = {"inplace", "new_name", "receiver"}
WHERE_INPUTS = {"condition", "if_false", "if_true", "name", "type"}


class DuplicateJsonKeyError(ValueError):
    """Raised before json.loads can silently overwrite an object member."""


class NonFiniteJsonConstantError(ValueError):
    """Raised before a non-standard JSON numeric token can enter the oracle."""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def sha256_file(path: Path) -> str:
    return f"sha256:{hashlib.sha256(path.read_bytes()).hexdigest()}"


def strict_json_dumps(value: Any, *, indent: int | None = None) -> str:
    return json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        indent=indent,
        sort_keys=True,
        separators=(",", ":") if indent is None else None,
    )


def canonical_sha256(value: Any) -> str:
    return f"sha256:{hashlib.sha256(strict_json_dumps(value).encode('utf-8')).hexdigest()}"


def reject_duplicate_json_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateJsonKeyError(f"JSON contains duplicate key '{key}'.")
        result[key] = value
    return result


def reject_nonfinite_json_constant(value: str) -> None:
    raise NonFiniteJsonConstantError(
        f"JSON contains forbidden non-finite constant '{value}'."
    )


def load_json_without_duplicates(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(
            path.read_text(encoding="utf-8-sig"),
            object_pairs_hook=reject_duplicate_json_object,
            parse_constant=reject_nonfinite_json_constant,
        )
    except OSError as exception:
        raise SystemExit(
            f"Cannot read public-symbol inventory '{path}': {exception}"
        ) from exception
    except (
        json.JSONDecodeError,
        DuplicateJsonKeyError,
        NonFiniteJsonConstantError,
    ) as exception:
        raise SystemExit(f"Invalid public-symbol inventory JSON: {exception}") from exception
    if not isinstance(value, dict):
        raise SystemExit("The public-symbol inventory root must be an object.")
    return value


def require_exact_keys(value: dict[str, Any], expected: set[str], context: str) -> None:
    actual = set(value)
    if actual != expected:
        raise SystemExit(
            f"{context} keys are not exact; "
            f"missing={sorted(expected - actual)!r}, extra={sorted(actual - expected)!r}."
        )


def require_hash(value: Any, context: str) -> str:
    if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
        raise SystemExit(f"{context} is not a canonical SHA-256 value.")
    return value


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    commit = upstream_commit.lower()
    if commit != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested commit is not the pinned DaySchedule commit.")

    inventory = load_json_without_duplicates(path)
    require_exact_keys(inventory, INVENTORY_KEYS, "Public-symbol inventory")
    if inventory.get("schema") != INVENTORY_SCHEMA:
        raise SystemExit("The public-symbol inventory schema is not v2.")
    if str(inventory.get("upstream_commit", "")).lower() != commit:
        raise SystemExit("The public-symbol inventory commit is stale.")

    files = inventory.get("files")
    symbols = inventory.get("symbols")
    summary = inventory.get("summary")
    if not isinstance(files, list) or not isinstance(symbols, list):
        raise SystemExit("The public-symbol inventory files and symbols must be arrays.")
    if not isinstance(summary, dict):
        raise SystemExit("The public-symbol inventory summary must be an object.")
    require_exact_keys(summary, SUMMARY_KEYS, "Public-symbol inventory summary")

    file_paths: list[str] = []
    for index, item in enumerate(files):
        if not isinstance(item, dict):
            raise SystemExit(f"Public-symbol inventory files[{index}] is not an object.")
        require_exact_keys(item, FILE_KEYS, f"Public-symbol inventory files[{index}]")
        require_hash(item["ast_hash"], f"Public-symbol inventory files[{index}].ast_hash")
        require_hash(
            item["content_hash"], f"Public-symbol inventory files[{index}].content_hash"
        )
        if not isinstance(item["path"], str):
            raise SystemExit(f"Public-symbol inventory files[{index}].path is not text.")
        file_paths.append(item["path"])
    if file_paths != sorted(file_paths) or len(file_paths) != len(set(file_paths)):
        raise SystemExit("Public-symbol inventory files are not unique and sorted.")

    symbol_keys: list[tuple[str, str]] = []
    for index, item in enumerate(symbols):
        if not isinstance(item, dict):
            raise SystemExit(f"Public-symbol inventory symbols[{index}] is not an object.")
        require_exact_keys(item, SYMBOL_KEYS, f"Public-symbol inventory symbols[{index}]")
        for name in ("body_hash", "signature_hash", "symbol_hash"):
            require_hash(item[name], f"Public-symbol inventory symbols[{index}].{name}")
        if not all(isinstance(item[name], str) for name in ("kind", "path", "symbol")):
            raise SystemExit(f"Public-symbol inventory symbols[{index}] identity is not text.")
        symbol_keys.append((item["path"], item["symbol"]))
    if symbol_keys != sorted(symbol_keys) or len(symbol_keys) != len(set(symbol_keys)):
        raise SystemExit("Public-symbol inventory symbols are not unique and sorted.")

    expected_summary = {
        "kind_counts": {
            kind: sum(item["kind"] == kind for item in symbols)
            for kind in ("class", "constant", "function")
        },
        "public_symbol_count": len(symbols),
        "python_file_count": len(files),
    }
    if summary != expected_summary:
        raise SystemExit("The public-symbol inventory summary is inconsistent.")

    computed_inventory_hash = canonical_sha256(
        {
            "files": files,
            "scope_sha256": inventory["scope_sha256"],
            "symbols": symbols,
            "upstream_commit": inventory["upstream_commit"],
        }
    )
    if (
        require_hash(inventory["content_sha256"], "Inventory content_sha256")
        != computed_inventory_hash
    ):
        raise SystemExit("The public-symbol inventory content hash is invalid.")
    if computed_inventory_hash != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory is not the exact pinned inventory.")

    target_symbols = [
        item
        for item in symbols
        if item["path"] == SOURCE_PATH and item["symbol"] in TARGET_SYMBOLS
    ]
    if [item["symbol"] for item in target_symbols] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover the 28 DaySchedule symbols.")
    for item in target_symbols:
        if item["symbol_hash"] != EXPECTED_SYMBOL_HASHES[item["symbol"]]:
            raise SystemExit(f"The inventory hash for {item['symbol']} is not pinned.")

    source_files = [item for item in files if item["path"] == SOURCE_PATH]
    if len(source_files) != 1:
        raise SystemExit("The inventory does not contain one exact profile source file.")
    if source_files[0]["content_hash"] != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The inventoried profile source hash is not pinned.")
    return {
        "content_sha256": computed_inventory_hash,
        "file": source_files[0],
        "symbols": target_symbols,
    }


SCHEDULE_TEMPLATES: dict[str, dict[str, Any]] = {
    "condition": {
        "name": "Condition",
        "pattern": (0, 1),
        "type": "onoff",
        "unit": "flag",
    },
    "condition-all-true": {
        "name": "AlwaysTrue",
        "pattern": (1,),
        "type": "onoff",
        "unit": "flag",
    },
    "fraction-left": {
        "name": "FractionLeft",
        "pattern": (0.2, 0.3),
        "type": "fraction",
        "unit": "ratio",
    },
    "fraction-right": {
        "name": "FractionRight",
        "pattern": (0.2, 0.3),
        "type": "fraction",
        "unit": "ratio",
    },
    "onoff-left": {
        "name": "OnOffLeft",
        "pattern": (0, 1),
        "type": "onoff",
        "unit": "flag",
    },
    "onoff-right": {
        "name": "OnOffRight",
        "pattern": (1, 0),
        "type": "onoff",
        "unit": "flag",
    },
    "real-left": {
        "name": "RealLeft",
        "pattern": (1.0, 2.0),
        "type": "real",
        "unit": "kW",
    },
    "real-float-max": {
        "name": "FloatMax",
        "pattern": (FLOAT_MAX,),
        "type": "real",
        "unit": "count",
    },
    "real-large-above": {
        "name": "LargeAbove",
        "pattern": (INEXACT_BINARY64_INTEGER + 1,),
        "type": "real",
        "unit": "count",
    },
    "real-large-bracket": {
        "name": "LargeBracket",
        "pattern": (
            INEXACT_BINARY64_INTEGER - 1,
            INEXACT_BINARY64_INTEGER + 1,
        ),
        "type": "real",
        "unit": "count",
    },
    "real-negative-divisor": {
        "name": "NegativeDivisor",
        "pattern": (-1.0, -2.0),
        "type": "real",
        "unit": "kW",
    },
    "real-negative-float-max": {
        "name": "NegativeFloatMax",
        "pattern": (-FLOAT_MAX,),
        "type": "real",
        "unit": "count",
    },
    "real-negative-zero": {
        "name": "NegativeZero",
        "pattern": (-0.0,),
        "type": "real",
        "unit": "kW",
    },
    "real-normalize-overflow": {
        "name": "NormalizeOverflow",
        "pattern": (-FLOAT_MAX, FLOAT_MIN_SUBNORMAL),
        "type": "real",
        "unit": "count",
    },
    "real-predicate": {
        "name": "Predicate",
        "pattern": (-2.0, 0.0, 1.0, 2.0),
        "type": "real",
        "unit": "kW",
    },
    "real-positive-zero": {
        "name": "PositiveZero",
        "pattern": (0.0,),
        "type": "real",
        "unit": "kW",
    },
    "real-right": {
        "name": "RealRight",
        "pattern": (3.0, 4.0),
        "type": "real",
        "unit": "kW",
    },
    "real-min-subnormal": {
        "name": "MinSubnormal",
        "pattern": (FLOAT_MIN_SUBNORMAL,),
        "type": "real",
        "unit": "count",
    },
    "real-zero-divisor": {
        "name": "ZeroDivisor",
        "pattern": (1.0, 0.0),
        "type": "real",
        "unit": "kW",
    },
    "temperature-left": {
        "name": "TemperatureLeft",
        "pattern": (2.0, 3.0),
        "type": "temperature",
        "unit": "C",
    },
    "temperature-negative": {
        "name": "TemperatureNegative",
        "pattern": (-50.0, -0.1),
        "type": "temperature",
        "unit": "C",
    },
    "temperature-right": {
        "name": "TemperatureRight",
        "pattern": (4.0, 5.0),
        "type": "temperature",
        "unit": "C",
    },
    "where-false": {
        "name": "WhereFalse",
        "pattern": (0.8, 0.7),
        "type": "fraction",
        "unit": "ratio",
    },
    "where-true": {
        "name": "WhereTrue",
        "pattern": (0.2, 0.3),
        "type": "fraction",
        "unit": "ratio",
    },
    "zero-real": {
        "name": "ZeroReal",
        "pattern": (0.0,),
        "type": "real",
        "unit": "kW",
    },
}


def schedule_ref(name: str) -> tuple[str, str]:
    return ("schedule", name)


def scalar(value: int | float | bool) -> tuple[str, int | float | bool]:
    return ("scalar", value)


def nonfinite(value: str) -> tuple[str, str]:
    if value not in ("nan", "negative-infinity", "positive-infinity"):
        raise RuntimeError(f"Unknown non-finite scalar token {value!r}.")
    return ("nonfinite", value)


def text(value: str) -> tuple[str, str]:
    return ("text", value)


def none() -> tuple[str, None]:
    return ("none", None)


def schedule_type(value: str) -> tuple[str, str]:
    return ("schedule-type", value)


def dotnet_raised_expectation(
    adaptation: str,
    error_category: str = "domain",
) -> dict[str, str]:
    if error_category not in DOTNET_ADAPTATION_ERROR_CATEGORIES.get(adaptation, set()):
        raise RuntimeError(
            f"Unknown .NET adaptation/category pair {adaptation!r}/{error_category!r}."
        )
    return {
        "adaptation": adaptation,
        "error_category": error_category,
        "outcome": "raised",
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions: list[dict[str, Any]] = []

    def add(
        identifier: str,
        symbol: str,
        inputs: dict[str, tuple[str, Any]],
        expected_exception: str | None = None,
        result_name_policy: str = "literal",
        expected_dotnet: dict[str, str] | None = None,
    ) -> None:
        definitions.append(
            {
                "expected_dotnet": expected_dotnet,
                "expected_exception": expected_exception,
                "id": identifier,
                "inputs": inputs,
                "result_name_policy": result_name_policy,
                "symbol": symbol,
            }
        )

    types = ("fraction", "onoff", "real", "temperature")
    left_template = {value: f"{value}-left" for value in types}
    right_template = {value: f"{value}-right" for value in types}

    for operation, symbol in (
        ("add", "DaySchedule.__add__"),
        ("mul", "DaySchedule.__mul__"),
        ("sub", "DaySchedule.__sub__"),
        ("truediv", "DaySchedule.__truediv__"),
    ):
        for left_type in types:
            for right_type in types:
                expected_exception = None
                if operation in ("add", "sub"):
                    allowed = (
                        (left_type == right_type == "fraction")
                        or left_type in ("real", "temperature")
                        and right_type in ("real", "temperature")
                    )
                    if not allowed:
                        expected_exception = "ScheduleOperationError"
                elif operation == "truediv":
                    if left_type == "onoff" or right_type != "real":
                        expected_exception = "ScheduleOperationError"
                add(
                    f"arithmetic.{operation}.schedule.{left_type}-{right_type}",
                    symbol,
                    {
                        "other": schedule_ref(right_template[right_type]),
                        "receiver": schedule_ref(left_template[left_type]),
                    },
                    expected_exception,
                )

    scalar_values = {
        "add": {"fraction": 0.1, "onoff": 2, "real": 2, "temperature": 2},
        "mul": {"fraction": 0.5, "onoff": 2, "real": 2, "temperature": 2},
        "sub": {"fraction": 0.1, "onoff": 2, "real": 2, "temperature": 2},
        "truediv": {"fraction": 0.5, "onoff": 2, "real": 2, "temperature": 2},
    }
    for operation, symbol in (
        ("add", "DaySchedule.__add__"),
        ("mul", "DaySchedule.__mul__"),
        ("sub", "DaySchedule.__sub__"),
        ("truediv", "DaySchedule.__truediv__"),
    ):
        for receiver_type in types:
            expected_exception = None
            if operation in ("add", "sub") and receiver_type == "onoff":
                expected_exception = "ScheduleOperationError"
            add(
                f"arithmetic.{operation}.scalar.{receiver_type}",
                symbol,
                {
                    "other": scalar(scalar_values[operation][receiver_type]),
                    "receiver": schedule_ref(left_template[receiver_type]),
                },
                expected_exception,
            )

    for operation, symbol in (
        ("radd", "DaySchedule.__radd__"),
        ("rmul", "DaySchedule.__rmul__"),
        ("rsub", "DaySchedule.__rsub__"),
        ("rtruediv", "DaySchedule.__rtruediv__"),
    ):
        for receiver_type in types:
            expected_exception = None
            if operation in ("radd", "rsub") and receiver_type == "onoff":
                expected_exception = "ScheduleOperationError"
            if operation == "rtruediv" and receiver_type != "real":
                expected_exception = "ScheduleOperationError"
            reverse_scalar = (
                0.1
                if receiver_type == "fraction" and operation == "radd"
                else 0.5
                if receiver_type == "fraction" and operation == "rmul"
                else 1
                if receiver_type == "fraction"
                else 12
            )
            add(
                f"arithmetic.{operation}.scalar.{receiver_type}",
                symbol,
                {
                    "other": scalar(reverse_scalar),
                    "receiver": schedule_ref(left_template[receiver_type]),
                },
                expected_exception,
            )

    for identifier, symbol, inputs, exception_type in (
        (
            "arithmetic.add.domain-error.fraction-scalar",
            "DaySchedule.__add__",
            {"receiver": schedule_ref("fraction-left"), "other": scalar(0.9)},
            "ValueError",
        ),
        (
            "arithmetic.mul.domain-error.fraction-scalar",
            "DaySchedule.__mul__",
            {"receiver": schedule_ref("fraction-left"), "other": scalar(4)},
            "ValueError",
        ),
        (
            "arithmetic.rsub.domain-error.fraction-scalar",
            "DaySchedule.__rsub__",
            {"receiver": schedule_ref("fraction-left"), "other": scalar(0)},
            "ValueError",
        ),
        (
            "arithmetic.rtruediv.zero-error.real-schedule",
            "DaySchedule.__rtruediv__",
            {"receiver": schedule_ref("real-zero-divisor"), "other": scalar(12)},
            "ZeroDivisionError",
        ),
        (
            "arithmetic.sub.domain-error.fraction-scalar",
            "DaySchedule.__sub__",
            {"receiver": schedule_ref("fraction-left"), "other": scalar(0.25)},
            "ValueError",
        ),
        (
            "arithmetic.truediv.domain-error.fraction-negative-real",
            "DaySchedule.__truediv__",
            {
                "receiver": schedule_ref("fraction-left"),
                "other": schedule_ref("real-negative-divisor"),
            },
            "ValueError",
        ),
        (
            "arithmetic.truediv.zero-error.real-scalar",
            "DaySchedule.__truediv__",
            {"receiver": schedule_ref("real-left"), "other": scalar(0)},
            "ZeroDivisionError",
        ),
        (
            "arithmetic.truediv.zero-error.real-schedule",
            "DaySchedule.__truediv__",
            {
                "receiver": schedule_ref("real-left"),
                "other": schedule_ref("real-zero-divisor"),
            },
            "ZeroDivisionError",
        ),
    ):
        add(identifier, symbol, inputs, exception_type)

    for operation, symbol, receiver, other, result_token in (
        (
            "add",
            "DaySchedule.__add__",
            "real-float-max",
            scalar(FLOAT_MAX),
            "positive-infinity",
        ),
        (
            "mul",
            "DaySchedule.__mul__",
            "real-float-max",
            scalar(2.0),
            "positive-infinity",
        ),
        (
            "radd",
            "DaySchedule.__radd__",
            "real-float-max",
            scalar(FLOAT_MAX),
            "positive-infinity",
        ),
        (
            "rmul",
            "DaySchedule.__rmul__",
            "real-float-max",
            scalar(-2.0),
            "negative-infinity",
        ),
        (
            "rsub",
            "DaySchedule.__rsub__",
            "real-float-max",
            scalar(-FLOAT_MAX),
            "negative-infinity",
        ),
        (
            "rtruediv",
            "DaySchedule.__rtruediv__",
            "real-min-subnormal",
            scalar(-FLOAT_MAX),
            "negative-infinity",
        ),
        (
            "sub",
            "DaySchedule.__sub__",
            "real-negative-float-max",
            scalar(FLOAT_MAX),
            "negative-infinity",
        ),
        (
            "truediv",
            "DaySchedule.__truediv__",
            "real-float-max",
            scalar(FLOAT_MIN_SUBNORMAL),
            "positive-infinity",
        ),
    ):
        add(
            f"overflow.finite-input.{operation}.{result_token}",
            symbol,
            {"receiver": schedule_ref(receiver), "other": other},
            expected_dotnet=dotnet_raised_expectation(
                ARITHMETIC_NONFINITE_ADAPTATIONS[symbol]
            ),
        )

    for operation, symbol, receiver, other, result_token in (
        (
            "add",
            "DaySchedule.__add__",
            "real-left",
            nonfinite("positive-infinity"),
            "positive-infinity",
        ),
        (
            "mul",
            "DaySchedule.__mul__",
            "real-left",
            nonfinite("positive-infinity"),
            "positive-infinity",
        ),
        (
            "radd",
            "DaySchedule.__radd__",
            "real-left",
            nonfinite("positive-infinity"),
            "positive-infinity",
        ),
        (
            "rmul",
            "DaySchedule.__rmul__",
            "real-left",
            nonfinite("negative-infinity"),
            "negative-infinity",
        ),
        (
            "rsub",
            "DaySchedule.__rsub__",
            "real-left",
            nonfinite("negative-infinity"),
            "negative-infinity",
        ),
        (
            "rtruediv",
            "DaySchedule.__rtruediv__",
            "real-left",
            nonfinite("positive-infinity"),
            "positive-infinity",
        ),
        (
            "sub",
            "DaySchedule.__sub__",
            "real-left",
            nonfinite("positive-infinity"),
            "negative-infinity",
        ),
        (
            "truediv",
            "DaySchedule.__truediv__",
            "real-left",
            nonfinite("nan"),
            "nan",
        ),
    ):
        add(
            f"overflow.tagged-scalar.{operation}.{result_token}",
            symbol,
            {"receiver": schedule_ref(receiver), "other": other},
            expected_dotnet=dotnet_raised_expectation(
                ARITHMETIC_NONFINITE_ADAPTATIONS[symbol]
            ),
        )

    for operation, symbol in (
        ("and", "DaySchedule.__and__"),
        ("or", "DaySchedule.__or__"),
    ):
        add(
            f"logical.{operation}.onoff",
            symbol,
            {
                "receiver": schedule_ref("onoff-left"),
                "other": schedule_ref("onoff-right"),
            },
        )
        add(
            f"logical.{operation}.non-onoff-error",
            symbol,
            {
                "receiver": schedule_ref("real-left"),
                "other": schedule_ref("real-right"),
            },
            "ScheduleOperationError",
        )
        add(
            f"logical.{operation}.mixed-error",
            symbol,
            {
                "receiver": schedule_ref("onoff-left"),
                "other": schedule_ref("real-right"),
            },
            "ScheduleOperationError",
        )
    add(
        "logical.invert.onoff",
        "DaySchedule.__invert__",
        {"receiver": schedule_ref("onoff-left")},
    )
    add(
        "logical.invert.non-onoff-error",
        "DaySchedule.__invert__",
        {"receiver": schedule_ref("real-left")},
        "ScheduleOperationError",
    )

    for operation, symbol in (
        ("eq", "DaySchedule.element_eq"),
        ("ge", "DaySchedule.__ge__"),
        ("gt", "DaySchedule.__gt__"),
        ("le", "DaySchedule.__le__"),
        ("lt", "DaySchedule.__lt__"),
        ("ne", "DaySchedule.element_ne"),
    ):
        add(
            f"comparison.{operation}.scalar",
            symbol,
            {"receiver": schedule_ref("real-predicate"), "other": scalar(0)},
        )
        add(
            f"comparison.{operation}.cross-type-schedule",
            symbol,
            {
                "receiver": schedule_ref("real-predicate"),
                "other": schedule_ref("temperature-left"),
            },
        )

    for operation, symbol in (
        ("eq", "DaySchedule.element_eq"),
        ("ge", "DaySchedule.__ge__"),
        ("gt", "DaySchedule.__gt__"),
        ("le", "DaySchedule.__le__"),
        ("lt", "DaySchedule.__lt__"),
        ("ne", "DaySchedule.element_ne"),
    ):
        add(
            f"large-int.comparison.{operation}",
            symbol,
            {
                "receiver": schedule_ref("real-large-bracket"),
                "other": scalar(INEXACT_BINARY64_INTEGER),
            },
        )

    for operation, symbol in (
        ("max", "DaySchedule.element_max"),
        ("min", "DaySchedule.element_min"),
    ):
        add(
            f"extrema.{operation}.schedule",
            symbol,
            {
                "receiver": schedule_ref("real-left"),
                "other": schedule_ref("real-right"),
            },
        )
        add(
            f"extrema.{operation}.scalar",
            symbol,
            {"receiver": schedule_ref("real-left"), "other": scalar(1.5)},
        )
        add(
            f"extrema.{operation}.mismatched-type-error",
            symbol,
            {
                "receiver": schedule_ref("real-left"),
                "other": schedule_ref("temperature-left"),
            },
            "ScheduleOperationError",
        )
        add(
            f"extrema.{operation}.onoff-error",
            symbol,
            {
                "receiver": schedule_ref("onoff-left"),
                "other": schedule_ref("onoff-right"),
            },
            "ScheduleOperationError",
        )
        invalid_scalar = 1.1 if operation == "max" else -0.1
        add(
            f"extrema.{operation}.fraction-domain-error",
            symbol,
            {
                "receiver": schedule_ref("fraction-left"),
                "other": scalar(invalid_scalar),
            },
            "ValueError",
        )

    add(
        "large-int.extrema.max-selected-scalar",
        "DaySchedule.element_max",
        {
            "receiver": schedule_ref("real-left"),
            "other": scalar(INEXACT_BINARY64_INTEGER),
        },
    )
    add(
        "large-int.extrema.min-selected-scalar",
        "DaySchedule.element_min",
        {
            "receiver": schedule_ref("real-large-above"),
            "other": scalar(INEXACT_BINARY64_INTEGER),
        },
    )
    add(
        "nonfinite.extrema.element-max.positive-infinity",
        "DaySchedule.element_max",
        {
            "receiver": schedule_ref("real-left"),
            "other": nonfinite("positive-infinity"),
        },
        expected_dotnet=dotnet_raised_expectation(
            "nonfinite-result-day-schedule-element-max"
        ),
    )
    add(
        "nonfinite.extrema.element-min.negative-infinity",
        "DaySchedule.element_min",
        {
            "receiver": schedule_ref("real-left"),
            "other": nonfinite("negative-infinity"),
        },
        expected_dotnet=dotnet_raised_expectation(
            "nonfinite-result-day-schedule-element-min"
        ),
    )
    add(
        "large-int.unbounded.extrema.element-max-fraction-domain-error",
        "DaySchedule.element_max",
        {
            "receiver": schedule_ref("fraction-left"),
            "other": scalar(UNBOUNDED_INTEGER),
        },
        "ValueError",
    )
    add(
        "large-int.unbounded.extrema.element-min-fraction-domain-error",
        "DaySchedule.element_min",
        {
            "receiver": schedule_ref("fraction-left"),
            "other": scalar(-UNBOUNDED_INTEGER),
        },
        "ValueError",
    )

    scalar_name_boundaries = (
        ("bool-false", False),
        ("bool-true", True),
        ("negative-zero", -0.0),
        ("one-e-minus-six", 1e-6),
        ("one-e-plus-twenty", 1e20),
        ("one-point-five", 1.5),
        ("three-point-zero", 3.0),
    )
    for operation, symbol in (
        ("add", "DaySchedule.__add__"),
        ("mul", "DaySchedule.__mul__"),
        ("radd", "DaySchedule.__radd__"),
        ("rmul", "DaySchedule.__rmul__"),
        ("rsub", "DaySchedule.__rsub__"),
        ("rtruediv", "DaySchedule.__rtruediv__"),
        ("sub", "DaySchedule.__sub__"),
        ("truediv", "DaySchedule.__truediv__"),
        ("eq", "DaySchedule.element_eq"),
        ("ge", "DaySchedule.__ge__"),
        ("gt", "DaySchedule.__gt__"),
        ("le", "DaySchedule.__le__"),
        ("lt", "DaySchedule.__lt__"),
        ("max", "DaySchedule.element_max"),
        ("min", "DaySchedule.element_min"),
        ("ne", "DaySchedule.element_ne"),
    ):
        for scalar_name, scalar_value in scalar_name_boundaries:
            expected_exception = (
                "ZeroDivisionError"
                if operation == "truediv"
                and scalar_name in ("bool-false", "negative-zero")
                else None
            )
            add(
                f"scalar-name.{operation}.{scalar_name}",
                symbol,
                {
                    "receiver": schedule_ref("real-left"),
                    "other": scalar(scalar_value),
                },
                expected_exception,
            )

    for operation, symbol in (
        ("max", "DaySchedule.element_max"),
        ("min", "DaySchedule.element_min"),
    ):
        for left, right in (
            ("positive-zero", "negative-zero"),
            ("negative-zero", "positive-zero"),
        ):
            add(
                f"signed-zero.{operation}.{left}-{right}",
                symbol,
                {
                    "receiver": schedule_ref(f"real-{left}"),
                    "other": schedule_ref(f"real-{right}"),
                },
            )

    for method, symbol in (
        ("negative", "DaySchedule.is_negative"),
        ("nonzero", "DaySchedule.is_nonzero"),
        ("off", "DaySchedule.is_off"),
        ("on", "DaySchedule.is_on"),
        ("positive", "DaySchedule.is_positive"),
        ("zero", "DaySchedule.is_zero"),
    ):
        add(
            f"predicate.{method}",
            symbol,
            {"receiver": schedule_ref("real-predicate")},
        )

    for suffix, include_min, include_max in (
        ("exclusive-exclusive", False, False),
        ("exclusive-inclusive", False, True),
        ("inclusive-exclusive", True, False),
        ("inclusive-inclusive", True, True),
    ):
        add(
            f"between.{suffix}",
            "DaySchedule.is_between",
            {
                "receiver": schedule_ref("real-predicate"),
                "min_value": scalar(0),
                "max_value": scalar(1),
                "include_min": scalar(include_min),
                "include_max": scalar(include_max),
            },
        )
    add(
        "between.reversed-bounds",
        "DaySchedule.is_between",
        {
            "receiver": schedule_ref("real-predicate"),
            "min_value": scalar(2),
            "max_value": scalar(0),
            "include_min": scalar(True),
            "include_max": scalar(True),
        },
    )
    add(
        "large-int.between.exact-lower-inclusive",
        "DaySchedule.is_between",
        {
            "receiver": schedule_ref("real-large-bracket"),
            "min_value": scalar(INEXACT_BINARY64_INTEGER),
            "max_value": scalar(INEXACT_BINARY64_INTEGER + 1),
            "include_min": scalar(True),
            "include_max": scalar(True),
        },
    )
    add(
        "large-int.between.exact-upper-inclusive",
        "DaySchedule.is_between",
        {
            "receiver": schedule_ref("real-large-bracket"),
            "min_value": scalar(INEXACT_BINARY64_INTEGER - 1),
            "max_value": scalar(INEXACT_BINARY64_INTEGER),
            "include_min": scalar(True),
            "include_max": scalar(True),
        },
    )
    add(
        "large-int.between.singleton-inclusive",
        "DaySchedule.is_between",
        {
            "receiver": schedule_ref("real-large-bracket"),
            "min_value": scalar(INEXACT_BINARY64_INTEGER),
            "max_value": scalar(INEXACT_BINARY64_INTEGER),
            "include_min": scalar(True),
            "include_max": scalar(True),
        },
    )

    normalize_common = {
        "inplace": scalar(False),
        "new_name": none(),
    }
    add(
        "normalize.copy.negative-real",
        "DaySchedule.normalize_by_max",
        {"receiver": schedule_ref("real-negative-divisor"), **normalize_common},
    )
    add(
        "normalize.copy.onoff-promotes-real",
        "DaySchedule.normalize_by_max",
        {"receiver": schedule_ref("onoff-left"), **normalize_common},
    )
    add(
        "normalize.copy.zero-divisor-fallback",
        "DaySchedule.normalize_by_max",
        {"receiver": schedule_ref("zero-real"), **normalize_common},
    )
    add(
        "normalize.copy.explicit-name",
        "DaySchedule.normalize_by_max",
        {
            "receiver": schedule_ref("real-left"),
            "inplace": scalar(False),
            "new_name": text("ExplicitNormalized"),
        },
    )
    add(
        "normalize.copy.finite-input-negative-infinity",
        "DaySchedule.normalize_by_max",
        {
            "receiver": schedule_ref("real-normalize-overflow"),
            "inplace": scalar(False),
            "new_name": text("NormalizeOverflowResult"),
        },
        expected_dotnet=dotnet_raised_expectation(
            "immutable-day-schedule-normalize-by-max"
        ),
    )
    add(
        "normalize.inplace.real-preserves-metadata",
        "DaySchedule.normalize_by_max",
        {
            "receiver": schedule_ref("real-negative-divisor"),
            "inplace": scalar(True),
            "new_name": text("IgnoredName"),
        },
    )
    add(
        "normalize.copy.temperature-domain-error",
        "DaySchedule.normalize_by_max",
        {"receiver": schedule_ref("temperature-negative"), **normalize_common},
        "ValueError",
    )
    add(
        "normalize.inplace.temperature-validation-bypass",
        "DaySchedule.normalize_by_max",
        {
            "receiver": schedule_ref("temperature-negative"),
            "inplace": scalar(True),
            "new_name": none(),
        },
    )

    where_symbol = "DaySchedule.where"
    add(
        "where.schedule-schedule.inferred",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": schedule_ref("where-true"),
            "if_false": schedule_ref("where-false"),
            "name": text("WhereScheduleSchedule"),
            "type": none(),
        },
    )
    add(
        "where.schedule-scalar.inferred",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": schedule_ref("where-true"),
            "if_false": scalar(0.1),
            "name": text("WhereScheduleScalar"),
            "type": none(),
        },
    )
    add(
        "where.scalar-schedule.inferred",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": scalar(0.4),
            "if_false": schedule_ref("where-false"),
            "name": text("WhereScalarSchedule"),
            "type": none(),
        },
    )
    add(
        "where.scalar-scalar.inferred-real",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": scalar(3),
            "if_false": scalar(8),
            "name": text("WhereScalarScalar"),
            "type": none(),
        },
    )
    add(
        "where.scalar-scalar.explicit-fraction",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": scalar(0.4),
            "if_false": scalar(0.6),
            "name": text("WhereExplicitFraction"),
            "type": schedule_type("fraction"),
        },
    )
    add(
        "where.error.non-onoff-condition",
        where_symbol,
        {
            "condition": schedule_ref("real-left"),
            "if_true": scalar(1),
            "if_false": scalar(0),
            "name": text("WhereBadCondition"),
            "type": none(),
        },
        "ScheduleOperationError",
    )
    add(
        "where.error.mixed-schedule-types",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": schedule_ref("where-true"),
            "if_false": schedule_ref("real-left"),
            "name": text("WhereMixed"),
            "type": none(),
        },
        "ScheduleOperationError",
    )
    add(
        "where.error.explicit-schedule-type-mismatch",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": schedule_ref("where-true"),
            "if_false": scalar(0.1),
            "name": text("WhereMismatch"),
            "type": schedule_type("real"),
        },
        "ScheduleOperationError",
    )
    add(
        "where.error.selected-invalid-scalar",
        where_symbol,
        {
            "condition": schedule_ref("condition-all-true"),
            "if_true": scalar(1.1),
            "if_false": scalar(0.5),
            "name": text("WhereSelectedInvalid"),
            "type": schedule_type("fraction"),
        },
        "ValueError",
    )
    add(
        "where.unselected-invalid-scalar",
        where_symbol,
        {
            "condition": schedule_ref("condition-all-true"),
            "if_true": scalar(0.5),
            "if_false": scalar(1.1),
            "name": text("WhereUnselectedInvalid"),
            "type": schedule_type("fraction"),
        },
    )
    add(
        "where.default-name-runtime-identity",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": scalar(1),
            "if_false": scalar(0),
            "name": none(),
            "type": none(),
        },
        result_name_policy="runtime-identity-hex",
    )
    add(
        "large-int.where.selected-true-branch",
        where_symbol,
        {
            "condition": schedule_ref("condition-all-true"),
            "if_true": scalar(INEXACT_BINARY64_INTEGER),
            "if_false": scalar(0),
            "name": text("WhereLargeSelected"),
            "type": none(),
        },
    )
    add(
        "large-int.where.unselected-false-branch",
        where_symbol,
        {
            "condition": schedule_ref("condition-all-true"),
            "if_true": scalar(0),
            "if_false": scalar(INEXACT_BINARY64_INTEGER),
            "name": text("WhereLargeUnselected"),
            "type": none(),
        },
    )
    for result_type in ("fraction", "onoff", "temperature"):
        add(
            f"large-int.unbounded.where.selected-{result_type}-domain-error",
            where_symbol,
            {
                "condition": schedule_ref("condition-all-true"),
                "if_true": scalar(UNBOUNDED_INTEGER),
                "if_false": scalar(0),
                "name": text(f"WhereHuge{result_type.title()}"),
                "type": schedule_type(result_type),
            },
            "ValueError",
        )
    add(
        "large-int.unbounded.where.selected-real-overflow-error",
        where_symbol,
        {
            "condition": schedule_ref("condition-all-true"),
            "if_true": scalar(UNBOUNDED_INTEGER),
            "if_false": scalar(0),
            "name": text("WhereHugeReal"),
            "type": schedule_type("real"),
        },
        "OverflowError",
    )
    add(
        "large-int.unbounded.where.unselected-fraction-success",
        where_symbol,
        {
            "condition": schedule_ref("condition-all-true"),
            "if_true": scalar(0),
            "if_false": scalar(UNBOUNDED_INTEGER),
            "name": text("WhereHugeUnselectedFraction"),
            "type": schedule_type("fraction"),
        },
    )
    add(
        "nonfinite.where.selected-true.positive-infinity",
        where_symbol,
        {
            "condition": schedule_ref("condition-all-true"),
            "if_true": nonfinite("positive-infinity"),
            "if_false": scalar(0),
            "name": text("WhereNonfiniteSelected"),
            "type": none(),
        },
        expected_dotnet=dotnet_raised_expectation(
            "deterministic-day-schedule-where-name"
        ),
    )
    add(
        "where.error.unsupported-text-true",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": text("unsupported-true"),
            "if_false": scalar(0),
            "name": text("WhereUnsupportedText"),
            "type": none(),
        },
        "TypeError",
        expected_dotnet=dotnet_raised_expectation(
            "deterministic-day-schedule-where-name",
            "schedule-operation",
        ),
    )
    add(
        "where.error.unsupported-true-before-mismatched-false",
        where_symbol,
        {
            "condition": schedule_ref("condition"),
            "if_true": text("unsupported-true"),
            "if_false": schedule_ref("where-true"),
            "name": text("WhereUnsupportedBeforeMismatch"),
            "type": schedule_type("real"),
        },
        "TypeError",
        expected_dotnet=dotnet_raised_expectation(
            "deterministic-day-schedule-where-name",
            "schedule-operation",
        ),
    )

    definitions.sort(key=lambda item: item["id"])
    validate_case_definitions(definitions)
    return tuple(definitions)


def validate_case_definitions(definitions: list[dict[str, Any]]) -> None:
    identifiers = [item["id"] for item in definitions]
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} DaySchedule operation cases, got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("DaySchedule operation case identifiers are not unique and sorted.")
    symbols = {item["symbol"] for item in definitions}
    if symbols != set(TARGET_SYMBOLS):
        raise RuntimeError("DaySchedule operation cases do not cover exactly 28 symbols.")
    for definition in definitions:
        if set(definition) != {
            "expected_dotnet",
            "expected_exception",
            "id",
            "inputs",
            "result_name_policy",
            "symbol",
        }:
            raise RuntimeError(f"Case {definition.get('id')!r} has unexpected keys.")
        if definition["result_name_policy"] not in (
            "literal",
            "runtime-identity-hex",
        ):
            raise RuntimeError(f"Case {definition['id']!r} has an invalid name policy.")
        if definition["expected_exception"] is not None and not isinstance(
            definition["expected_exception"], str
        ):
            raise RuntimeError(f"Case {definition['id']!r} has an invalid exception.")
        expected_dotnet = definition["expected_dotnet"]
        if expected_dotnet is not None:
            if set(expected_dotnet) != {"adaptation", "error_category", "outcome"}:
                raise RuntimeError(
                    f"Case {definition['id']!r} has malformed .NET expectation keys."
                )
            if (
                expected_dotnet["outcome"] != "raised"
                or expected_dotnet["error_category"]
                not in DOTNET_ADAPTATION_ERROR_CATEGORIES.get(
                    expected_dotnet["adaptation"],
                    set(),
                )
            ):
                raise RuntimeError(
                    f"Case {definition['id']!r} has an unknown .NET expectation."
                )
        symbol = definition["symbol"]
        expected_inputs = (
            {"receiver"}
            if symbol in SINGLE_SCHEDULE_INPUT_SYMBOLS
            else BETWEEN_INPUTS
            if symbol == "DaySchedule.is_between"
            else NORMALIZE_INPUTS
            if symbol == "DaySchedule.normalize_by_max"
            else WHERE_INPUTS
            if symbol == "DaySchedule.where"
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
            if kind == "schedule":
                if value not in SCHEDULE_TEMPLATES:
                    raise RuntimeError(f"Case {definition['id']!r} uses an unknown schedule.")
            elif kind == "scalar":
                require_finite_scalar(value, f"Case {definition['id']} input {input_name}")
            elif kind == "nonfinite":
                if expected_dotnet is None:
                    raise RuntimeError(
                        f"Case {definition['id']!r} has an unadapted non-finite input."
                    )
                nonfinite(value)
            elif kind == "text":
                if not isinstance(value, str):
                    raise RuntimeError(f"Case {definition['id']!r} has invalid text.")
            elif kind == "none":
                if value is not None:
                    raise RuntimeError(f"Case {definition['id']!r} has invalid None input.")
            elif kind == "schedule-type":
                if value not in ("fraction", "onoff", "real", "temperature"):
                    raise RuntimeError(f"Case {definition['id']!r} has invalid schedule type.")
            else:
                raise RuntimeError(f"Case {definition['id']!r} has unknown input kind.")
    strict_json_dumps(
        [
            {
                "expected_dotnet": item["expected_dotnet"],
                "expected_exception": item["expected_exception"],
                "id": item["id"],
                "inputs": {
                    key: list(value) for key, value in item["inputs"].items()
                },
                "result_name_policy": item["result_name_policy"],
                "symbol": item["symbol"],
            }
            for item in definitions
        ]
    )


def require_finite_scalar(value: Any, context: str) -> None:
    if type(value) not in (bool, int, float):
        raise RuntimeError(f"{context} is not a typed Python scalar.")
    if isinstance(value, float) and not math.isfinite(value):
        raise RuntimeError(f"{context} is not finite.")


def tagged_observation_value(
    value: Any,
    *,
    allow_nonfinite: bool,
) -> int | float | bool | dict[str, str]:
    if type(value) not in (bool, int, float):
        raise RuntimeError("A DaySchedule observation contains a non-numeric value.")
    if not isinstance(value, float) or math.isfinite(value):
        return value
    if not allow_nonfinite:
        raise RuntimeError("A finite DaySchedule input contains a non-finite value.")
    token = (
        "nan"
        if math.isnan(value)
        else "positive-infinity"
        if value > 0
        else "negative-infinity"
    )
    return {"kind": "nonfinite", "value": token}


def compact_values(
    values: list[int | float],
    *,
    allow_nonfinite: bool = False,
) -> dict[str, Any]:
    if len(values) != 144:
        raise RuntimeError("A normalized DaySchedule observation is not length 144.")
    normalized = [
        tagged_observation_value(value, allow_nonfinite=allow_nonfinite)
        for value in values
    ]
    for period in range(1, len(values)):
        if len(values) % period == 0 and all(
            normalized[index] == normalized[index % period]
            for index in range(len(normalized))
        ):
            return {
                "encoding": "repeat",
                "length": len(normalized),
                "pattern": normalized[:period],
            }
    return {"encoding": "full", "items": normalized, "length": len(normalized)}


def input_descriptor(value: Any, DaySchedule: type, ScheduleType: type) -> dict[str, Any]:
    if isinstance(value, DaySchedule):
        return {
            "kind": "schedule",
            "name": value.name,
            "schedule_type": value.type.value,
            "unit": value.unit,
            "values": compact_values(list(value.data)),
        }
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


def schedule_result_descriptor(
    schedule: Any,
    name_policy: str,
    DaySchedule: type,
) -> dict[str, Any]:
    if not isinstance(schedule, DaySchedule):
        raise RuntimeError("A successful schedule operation returned the wrong type.")
    if name_policy == "runtime-identity-hex":
        if not isinstance(schedule.name, str) or AUTO_NAME_PATTERN.fullmatch(schedule.name) is None:
            raise RuntimeError("The default DaySchedule name is not a runtime identity hex name.")
        normalized_name = {"policy": "runtime-identity-hex"}
    else:
        if not isinstance(schedule.name, str) or AUTO_NAME_PATTERN.fullmatch(schedule.name):
            raise RuntimeError("A raw runtime identity name would enter the oracle.")
        normalized_name = {"policy": "literal", "value": schedule.name}
    return {
        "kind": "schedule",
        "name": normalized_name,
        "schedule_type": schedule.type.value,
        "unit": schedule.unit,
        "values": compact_values(list(schedule.data), allow_nonfinite=True),
    }


def schedule_state(schedule: Any) -> tuple[Any, ...]:
    return (schedule.name, schedule.type, schedule.unit, tuple(schedule.data))


def resolve_input(specification: tuple[str, Any], DaySchedule: type, ScheduleType: type) -> Any:
    kind, value = specification
    if kind == "schedule":
        template = SCHEDULE_TEMPLATES[value]
        pattern = list(template["pattern"])
        values = (pattern * ((144 // len(pattern)) + 1))[:144]
        return DaySchedule(
            template["name"],
            values,
            type=ScheduleType(template["type"]),
            unit=template["unit"],
        )
    if kind == "schedule-type":
        return ScheduleType(value)
    if kind == "nonfinite":
        return {
            "nan": math.nan,
            "negative-infinity": -math.inf,
            "positive-infinity": math.inf,
        }[value]
    return value


def invoke_case(symbol: str, inputs: dict[str, Any], DaySchedule: type) -> Any:
    receiver = inputs.get("receiver")
    other = inputs.get("other")
    method = symbol.removeprefix("DaySchedule.")
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
        return receiver.normalize_by_max(
            inplace=inputs["inplace"],
            new_name=inputs["new_name"],
        )
    if method == "where":
        return DaySchedule.where(
            inputs["condition"],
            inputs["if_true"],
            inputs["if_false"],
            name=inputs["name"],
            type=inputs["type"],
        )
    raise RuntimeError(f"Unsupported oracle symbol dispatch: {symbol}.")


def execute_case(
    definition: dict[str, Any],
    DaySchedule: type,
    ScheduleType: type,
) -> dict[str, Any]:
    actual_inputs = {
        name: resolve_input(specification, DaySchedule, ScheduleType)
        for name, specification in definition["inputs"].items()
    }
    descriptors = {
        name: input_descriptor(value, DaySchedule, ScheduleType)
        for name, value in actual_inputs.items()
    }
    schedule_inputs = {
        name: value
        for name, value in actual_inputs.items()
        if isinstance(value, DaySchedule)
    }
    states_before = {
        name: schedule_state(value) for name, value in schedule_inputs.items()
    }

    try:
        result = invoke_case(definition["symbol"], actual_inputs, DaySchedule)
    except Exception as exception:
        expected = definition["expected_exception"]
        if expected is None:
            raise RuntimeError(
                f"Case {definition['id']} unexpectedly raised {type(exception).__name__}."
            ) from exception
        if type(exception).__name__ != expected:
            raise RuntimeError(
                f"Case {definition['id']} raised {type(exception).__name__}, expected {expected}."
            ) from exception
        observation: dict[str, Any] = {
            "exception": {
                "message": str(exception),
                "type": type(exception).__name__,
            },
            "outcome": "raised",
        }
    else:
        if definition["expected_exception"] is not None:
            raise RuntimeError(
                f"Case {definition['id']} returned but expected "
                f"{definition['expected_exception']}."
            )
        if result is None:
            normalized_result = {"kind": "none"}
            result_identity = "none"
        else:
            normalized_result = schedule_result_descriptor(
                result,
                definition["result_name_policy"],
                DaySchedule,
            )
            result_identity = (
                "receiver" if result is actual_inputs.get("receiver") else "new"
            )
        observation = {
            "outcome": "returned",
            "result": normalized_result,
            "result_identity": result_identity,
        }

    schedule_inputs_after: dict[str, Any] = {}
    for name, value in schedule_inputs.items():
        if schedule_state(value) == states_before[name]:
            schedule_inputs_after[name] = {"identity": "preserved", "status": "unchanged"}
        else:
            schedule_inputs_after[name] = {
                "identity": "preserved",
                "status": "changed",
                "value": schedule_result_descriptor(value, "literal", DaySchedule),
            }
    observation["schedule_inputs_after"] = schedule_inputs_after
    case = {
        "id": definition["id"],
        "inputs": descriptors,
        "observation": observation,
        "symbol": definition["symbol"],
    }
    if definition["expected_dotnet"] is not None:
        case["expected_dotnet"] = definition["expected_dotnet"]
    return case


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import DaySchedule, ScheduleType

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")
    if DaySchedule.DATA_INTERVAL != 6 or DaySchedule("probe").fixed_length != 144:
        raise SystemExit("Pinned DaySchedule grid constants are not exact.")

    definitions = case_definitions()
    cases = [execute_case(item, DaySchedule, ScheduleType) for item in definitions]
    if [item["id"] for item in cases] != sorted(item["id"] for item in cases):
        raise RuntimeError("Generated DaySchedule operation cases are not sorted.")

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
        raise RuntimeError("A raw runtime identity name entered the operations oracle.")
    return result


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the DaySchedule oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")

    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    serialized = strict_json_dumps(result, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8", newline="\n")
    print(f"Wrote DaySchedule operations oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

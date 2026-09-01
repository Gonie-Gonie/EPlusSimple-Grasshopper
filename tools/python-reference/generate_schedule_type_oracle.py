"""Generate the pinned InvisibleDragon ScheduleType behavior oracle.

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


SCHEMA = "dragons.invisibledragon.schedule-type-oracle.v1"
INVENTORY_SCHEMA = "dragons.upstream-public-symbol-inventory.v2"
SOURCE_PATH = "src/idragon/dragon/profile.py"
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
# This is the LF-normalized Git source selected by reference.ps1. A separate
# planning checkout used CRLF worktree bytes and therefore has a different
# file hash even though Git reports the same pinned blob.
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "ScheduleType": (
        "sha256:f873f5e850d3f042a188507bae21c0e74e115483b80a46f72872438e8eeaa38a"
    ),
    "ScheduleType.FRACTION": (
        "sha256:00d89a2b31e5155ae7bfb099c21c20736c7feb93222e1c11aa002d683c094528"
    ),
    "ScheduleType.ONOFF": (
        "sha256:767a33fed3b7eec45baa4463546cc530953e0e3fce66d9140b9f84dd0a6e90c3"
    ),
    "ScheduleType.REAL": (
        "sha256:daaa37fac4fc602f11bc3fba7684dbd4a2c4613929219c46a94d3f70997fbb0e"
    ),
    "ScheduleType.TEMPERATURE": (
        "sha256:a85b41c57a152b9e1164b77ca6289d10e5d640d50e2f3b7d82f64e2172b2166a"
    ),
    "ScheduleType.idf_objname": (
        "sha256:6922ec3fabc53f7c283f0626837f483147071358be52745fd4542adce1cfff70"
    ),
    "ScheduleType.lower_limit": (
        "sha256:e4bfd0fa9092e8a15c109936aca87b8563ad28e785e0d4d3bf31f9271b8dacf2"
    ),
    "ScheduleType.numeric_type": (
        "sha256:723a16400cd165414a5b9f146557550742fbca24da2d3b341633ff7374c81389"
    ),
    "ScheduleType.to_idf_object": (
        "sha256:7f67c4b1b5f76c37aa6fb6355d194b08fad513d38bae331c65f645677fa3e1a5"
    ),
    "ScheduleType.unit_type": (
        "sha256:66ea929d97c87c709bfffcf03a76c7c8ad86b75c844cbff09074e99f8ce339f0"
    ),
    "ScheduleType.upper_limit": (
        "sha256:e921c8faee5d3b8fa3190333c18831f18e1ff4afc0e2b5ea332156933159c48b"
    ),
    "ScheduleType.validate": (
        "sha256:b09903103bf95c771eb228f80666fb264e176204c332873795c2d96f86056bcb"
    ),
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
TYPE_ORDER = ("temperature", "onoff", "fraction", "real")

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


def _number(case_id: str, value: int | float) -> dict[str, Any]:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise TypeError("A numeric case must contain an int or float, not bool.")
    if isinstance(value, float) and not math.isfinite(value):
        raise ValueError("Non-finite inputs must use the tagged nonfinite encoding.")
    return {
        "id": case_id,
        "input": {
            "kind": "number",
            "numeric_kind": "int" if isinstance(value, int) else "float",
            "value": value,
        },
    }


def _boolean(case_id: str, value: bool) -> dict[str, Any]:
    return {"id": case_id, "input": {"kind": "boolean", "value": value}}


def _string(case_id: str, value: str) -> dict[str, Any]:
    return {"id": case_id, "input": {"kind": "string", "value": value}}


def _nonfinite(case_id: str, value: str) -> dict[str, Any]:
    if value not in {"nan", "positive-infinity", "negative-infinity"}:
        raise ValueError(f"Unknown non-finite input token: {value}")
    return {"id": case_id, "input": {"kind": "nonfinite", "value": value}}


VALIDATION_CASE_SPECS = {
    "temperature": (
        _number("lower-bound", -50),
        _number("upper-bound", 200),
        _number("interior-fractional", 20.5),
        _number("just-below-lower", math.nextafter(-50.0, -math.inf)),
        _number("just-above-lower", math.nextafter(-50.0, math.inf)),
        _number("just-below-upper", math.nextafter(200.0, -math.inf)),
        _number("just-above-upper", math.nextafter(200.0, math.inf)),
        _boolean("boolean-true", True),
        _boolean("boolean-false", False),
        _string("string-input", "20"),
        _nonfinite("nan", "nan"),
        _nonfinite("positive-infinity", "positive-infinity"),
        _nonfinite("negative-infinity", "negative-infinity"),
    ),
    "onoff": (
        _number("zero", 0),
        _number("one", 1),
        _boolean("boolean-true", True),
        _boolean("boolean-false", False),
        _number("below-domain", -1),
        _number("above-domain", 2),
        _number("fractional", 0.5),
        _string("string-input", "1"),
        _nonfinite("nan", "nan"),
        _nonfinite("positive-infinity", "positive-infinity"),
        _nonfinite("negative-infinity", "negative-infinity"),
    ),
    "fraction": (
        _number("lower-bound", 0),
        _number("upper-bound", 1),
        _number("interior-fractional", 0.375),
        _number("just-below-lower", math.nextafter(0.0, -math.inf)),
        _number("just-above-upper", math.nextafter(1.0, math.inf)),
        _boolean("boolean-true", True),
        _boolean("boolean-false", False),
        _string("string-input", "0.375"),
        _nonfinite("nan", "nan"),
        _nonfinite("positive-infinity", "positive-infinity"),
        _nonfinite("negative-infinity", "negative-infinity"),
    ),
    "real": (
        _number("negative", -12.5),
        _number("zero", 0),
        _number("positive", 19.75),
        _boolean("boolean-true", True),
        _boolean("boolean-false", False),
        _string("string-input", "19.75"),
        _nonfinite("nan", "nan"),
        _nonfinite("positive-infinity", "positive-infinity"),
        _nonfinite("negative-infinity", "negative-infinity"),
    ),
}


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
        raise SystemExit(f"Cannot read public-symbol inventory '{path}': {exception}") from exception
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
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        raise SystemExit(
            f"{context} keys are not exact; missing={missing!r}, extra={extra!r}."
        )


def require_hash(value: Any, context: str) -> str:
    if not isinstance(value, str) or SHA256_PATTERN.fullmatch(value) is None:
        raise SystemExit(f"{context} is not a canonical SHA-256 value.")
    return value


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    commit = upstream_commit.lower()
    if commit != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not the pinned ScheduleType commit.")

    inventory = load_json_without_duplicates(path)
    require_exact_keys(inventory, INVENTORY_KEYS, "Public-symbol inventory")
    if inventory.get("schema") != INVENTORY_SCHEMA:
        raise SystemExit("The public-symbol inventory schema is not v2.")
    if str(inventory.get("upstream_commit", "")).lower() != commit:
        raise SystemExit("The public-symbol inventory commit is stale.")

    files = inventory.get("files")
    symbols = inventory.get("symbols")
    if not isinstance(files, list) or not isinstance(symbols, list):
        raise SystemExit("The public-symbol inventory files and symbols must be arrays.")

    summary = inventory.get("summary")
    if not isinstance(summary, dict):
        raise SystemExit("The public-symbol inventory summary must be an object.")
    require_exact_keys(summary, SUMMARY_KEYS, "Public-symbol inventory summary")

    file_paths: list[str] = []
    for index, item in enumerate(files):
        if not isinstance(item, dict):
            raise SystemExit(f"Public-symbol inventory files[{index}] is not an object.")
        require_exact_keys(item, FILE_KEYS, f"Public-symbol inventory files[{index}]")
        require_hash(item["ast_hash"], f"Public-symbol inventory files[{index}].ast_hash")
        require_hash(item["content_hash"], f"Public-symbol inventory files[{index}].content_hash")
        if not isinstance(item["path"], str):
            raise SystemExit(f"Public-symbol inventory files[{index}].path is not text.")
        file_paths.append(item["path"])
    if file_paths != sorted(file_paths) or len(file_paths) != len(set(file_paths)):
        raise SystemExit("Public-symbol inventory files are not unique and canonically sorted.")

    symbol_keys: list[tuple[str, str]] = []
    for index, item in enumerate(symbols):
        if not isinstance(item, dict):
            raise SystemExit(f"Public-symbol inventory symbols[{index}] is not an object.")
        require_exact_keys(item, SYMBOL_KEYS, f"Public-symbol inventory symbols[{index}]")
        for name in ("body_hash", "signature_hash", "symbol_hash"):
            require_hash(item[name], f"Public-symbol inventory symbols[{index}].{name}")
        if not all(isinstance(item[name], str) for name in ("kind", "path", "symbol")):
            raise SystemExit(
                f"Public-symbol inventory symbols[{index}] identity fields are not text."
            )
        symbol_keys.append((item["path"], item["symbol"]))
    if symbol_keys != sorted(symbol_keys) or len(symbol_keys) != len(set(symbol_keys)):
        raise SystemExit("Public-symbol inventory symbols are not unique and canonically sorted.")

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

    declared_inventory_hash = require_hash(
        inventory["content_sha256"], "Public-symbol inventory content_sha256"
    )
    computed_inventory_hash = canonical_sha256(
        {
            "files": files,
            "scope_sha256": inventory["scope_sha256"],
            "symbols": symbols,
            "upstream_commit": inventory["upstream_commit"],
        }
    )
    if declared_inventory_hash != computed_inventory_hash:
        raise SystemExit("The public-symbol inventory content hash is invalid.")
    if computed_inventory_hash != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory is not the exact pinned inventory.")

    target_symbols = [
        item
        for item in symbols
        if item["path"] == SOURCE_PATH and item["symbol"] in TARGET_SYMBOLS
    ]
    if [item["symbol"] for item in target_symbols] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover the 12 ScheduleType symbols.")
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


def decode_input(value: dict[str, Any]) -> Any:
    kind = value["kind"]
    if kind in {"number", "boolean", "string"}:
        return value["value"]
    if kind == "nonfinite":
        return {
            "nan": math.nan,
            "positive-infinity": math.inf,
            "negative-infinity": -math.inf,
        }[value["value"]]
    raise ValueError(f"Unknown input kind: {kind}")


def tagged_output_value(value: int | float) -> dict[str, Any]:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise TypeError("ScheduleType.validate returned a non-numeric value.")
    if isinstance(value, float) and not math.isfinite(value):
        token = "nan" if math.isnan(value) else (
            "positive-infinity" if value > 0 else "negative-infinity"
        )
        return {"kind": "nonfinite", "value": token}
    return {"kind": "finite", "value": value}


def observe_validation(schedule_type: Any, spec: dict[str, Any]) -> dict[str, Any]:
    try:
        result = schedule_type.validate(decode_input(spec["input"]))
    except Exception as exception:  # Exact pinned behavior is oracle data.
        category = "type" if isinstance(exception, TypeError) else "domain"
        return {
            "error_category": category,
            "python_exception": type(exception).__name__,
            "status": "error",
        }
    return {
        "numeric_kind": "int" if isinstance(result, int) else "float",
        "status": "value",
        "value": tagged_output_value(result),
    }


def validate_case_specs() -> None:
    if tuple(VALIDATION_CASE_SPECS) != TYPE_ORDER:
        raise RuntimeError("ScheduleType validation families are not in canonical order.")
    expected_counts = {"temperature": 13, "onoff": 11, "fraction": 11, "real": 9}
    allowed_input_kinds = {"number", "boolean", "string", "nonfinite"}
    for schedule_name, specs in VALIDATION_CASE_SPECS.items():
        ids = [item["id"] for item in specs]
        if len(specs) != expected_counts[schedule_name] or len(ids) != len(set(ids)):
            raise RuntimeError(f"{schedule_name} validation cases are incomplete or duplicated.")
        for spec in specs:
            if set(spec) != {"id", "input"} or not isinstance(spec["id"], str):
                raise RuntimeError(f"{schedule_name} has a malformed validation case.")
            input_value = spec["input"]
            if input_value.get("kind") not in allowed_input_kinds:
                raise RuntimeError(f"{schedule_name}/{spec['id']} has an invalid input kind.")
            # The strict serializer proves no case specification contains a raw
            # Python NaN or infinity before upstream execution.
            strict_json_dumps(spec)


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import ScheduleType

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")

    validate_case_specs()
    members_by_value = {member.value: member for member in ScheduleType}
    if tuple(members_by_value) != TYPE_ORDER:
        raise SystemExit("The pinned ScheduleType variants or declaration order changed.")

    type_results: list[dict[str, Any]] = []
    for type_name in TYPE_ORDER:
        member = members_by_value[type_name]
        idf_object = member.to_idf_object()
        fields = list(idf_object.data.values())
        extended = list(getattr(idf_object, "_IdfObject__extended_input"))
        if extended or len(fields) != 5:
            raise SystemExit(f"{member.name} did not produce exactly five IDF fields.")

        validation_cases = []
        for spec in sorted(VALIDATION_CASE_SPECS[type_name], key=lambda item: item["id"]):
            validation_cases.append(
                {
                    "id": spec["id"],
                    "input": spec["input"],
                    "outcome": observe_validation(member, spec),
                }
            )

        type_results.append(
            {
                "enum_name": member.name,
                "idf_object": {
                    "fields": fields,
                    "object_type": idf_object.idd.name,
                },
                "idf_objname": member.idf_objname,
                "lower_limit": member.lower_limit,
                "numeric_type": member.numeric_type,
                "type": member.value,
                "unit_type": member.unit_type,
                "upper_limit": member.upper_limit,
                "validation_cases": validation_cases,
            }
        )

    return {
        "runtime": {
            "implementation": sys.implementation.name,
            "python_hash_algorithm": sys.hash_info.algorithm,
            "python_hash_seed": 0,
            "python_hash_width_bits": sys.hash_info.width,
            "python_version": ".".join(map(str, sys.version_info[:3])),
        },
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "types": type_results,
        "upstream": {
            "commit": commit,
            "inventory_sha256": inventory["content_sha256"],
            "path": SOURCE_PATH,
            "source_sha256": imported_source_sha256,
        },
    }


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON:
        raise SystemExit("Python 3.12.7 is required for the ScheduleType oracle.")
    if sys.implementation.name != "cpython":
        raise SystemExit("CPython is required for the ScheduleType oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0":
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if sys.flags.hash_randomization != 0:
        raise SystemExit("CPython hash randomization must be disabled by PYTHONHASHSEED=0.")
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
    print(f"Wrote ScheduleType oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

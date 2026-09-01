"""Generate pinned InvisibleDragon DaySchedule fixed-grid metric observations.

Run this only through ``bootstrap_reference.py`` so imports resolve from the
exact pinned upstream source and dependency tree.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
from typing import Any


SCHEMA = "dragons.invisibledragon.day-schedule-metrics-oracle.v1"
INVENTORY_SCHEMA = "dragons.upstream-public-symbol-inventory.v2"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "DaySchedule.DATA_INTERVAL": (
        "sha256:b53131ccec072b1290838381677697006a0c9cec22aff1882fab4a59bdc8c30a"
    ),
    "DaySchedule.average": (
        "sha256:55bc4967765bbee28662c491439fa2c95a4e4128bc1660284502b31b05b24d52"
    ),
    "DaySchedule.fixed_length": (
        "sha256:a353188fed7223a24e31fe0968cb7cdfb191fc779087fd849e018ff42c2d52ea"
    ),
    "DaySchedule.has_nonzero": (
        "sha256:8e7daa8fe6a78bc181c23cc1205b8c0717384320ca337c102e5c89b2bc9d0181"
    ),
    "DaySchedule.has_positive": (
        "sha256:84c867d2b8c3d24aba67c0370e844f3971a81106a53326d844531f8c93b6d603"
    ),
    "DaySchedule.integral": (
        "sha256:cd5749889d0a405f8786818089df75dbae0c53b8c0b994da7cd59c318169576b"
    ),
    "DaySchedule.is_constant": (
        "sha256:48c772e45f4c329dffcfccd76d09f8fb8e58b954461263a96d98904af1378f4e"
    ),
    "DaySchedule.max": (
        "sha256:44f90344e50ce247c439c80440ca0797761507ae8316848df4d7bdf7b4a4b67f"
    ),
    "DaySchedule.min": (
        "sha256:ed9f11bd1e07b0841a20631e20de665591c7ea818a3a70f062825983e7bf4d01"
    ),
    "DaySchedule.nonzero_hours": (
        "sha256:f4c71d3aea51cdd689527156a6824982c7f39c9057525782f980033a2ded25b2"
    ),
    "DaySchedule.positive_average": (
        "sha256:630219d623c8d9761eadafe6bd27ed6bee3aa7e0d96dc4d6b8acbe24d1c7d819"
    ),
    "DaySchedule.positive_hours": (
        "sha256:8408d1e02b37da212885c01f74a5001985c58b4102c44d6391729ddcb148e622"
    ),
    "DaySchedule.step_in_hours": (
        "sha256:8f0c0fc9d2013fb3c88672e86d6bba893a91d01467f9d481ee740379e729f0b3"
    ),
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

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
        require_hash(item["content_hash"], f"Public-symbol inventory files[{index}].content_hash")
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
    if require_hash(inventory["content_sha256"], "Inventory content_sha256") != computed_inventory_hash:
        raise SystemExit("The public-symbol inventory content hash is invalid.")
    if computed_inventory_hash != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory is not the exact pinned inventory.")

    target_symbols = [
        item
        for item in symbols
        if item["path"] == SOURCE_PATH and item["symbol"] in TARGET_SYMBOLS
    ]
    if [item["symbol"] for item in target_symbols] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover the 13 DaySchedule symbols.")
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


def metric_cases(fixed_length: int) -> tuple[tuple[str, list[float]], ...]:
    if fixed_length != 144:
        raise SystemExit("Pinned DaySchedule fixed length is not 144.")
    cases = (
        ("all-zero", [0.0] * fixed_length),
        ("constant-positive", [2.0] * fixed_length),
        ("constant-negative", [-2.0] * fixed_length),
        (
            "compensated-cancellation",
            [1e16, 1.0, 1.0, -1e16] + ([0.0] * (fixed_length - 4)),
        ),
        ("mixed-four", [-2.0, 0.0, 1.0, 2.0] * 36),
        ("sparse-first", [1.0] + ([0.0] * (fixed_length - 1))),
        ("alternating-sign", [-1.0, 1.0] * 72),
        ("endpoint-range", [float(value) for value in range(-72, 72)]),
    )
    identifiers = [identifier for identifier, _ in cases]
    if identifiers != sorted(identifiers):
        cases = tuple(sorted(cases, key=lambda item: item[0]))
        identifiers = [identifier for identifier, _ in cases]
    if len(identifiers) != len(set(identifiers)):
        raise RuntimeError("DaySchedule metric case identifiers are duplicated.")
    if any(len(values) != fixed_length for _, values in cases):
        raise RuntimeError("A DaySchedule metric case is not fixed length.")
    strict_json_dumps(cases)
    return cases


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import DaySchedule, ScheduleType

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")
    if DaySchedule.DATA_INTERVAL != 6:
        raise SystemExit("Pinned DaySchedule DATA_INTERVAL is not six per hour.")

    probe = DaySchedule("fixed-length-probe", type=ScheduleType.REAL)
    fixed_length = probe.fixed_length
    class_observations = {
        "data_interval": DaySchedule.DATA_INTERVAL,
        "fixed_length": fixed_length,
        "step_in_hours": probe.step_in_hours,
    }
    cases: list[dict[str, Any]] = []
    for identifier, values in metric_cases(fixed_length):
        schedule = DaySchedule(identifier, values, type=ScheduleType.REAL)
        cases.append(
            {
                "id": identifier,
                "observations": {
                    "average": schedule.average,
                    "has_nonzero": schedule.has_nonzero,
                    "has_positive": schedule.has_positive,
                    "integral": schedule.integral,
                    "is_constant": schedule.is_constant,
                    "max": schedule.max,
                    "min": schedule.min,
                    "nonzero_hours": schedule.nonzero_hours,
                    "positive_average": schedule.positive_average,
                    "positive_hours": schedule.positive_hours,
                },
                "values": values,
            }
        )

    result = {
        "cases": cases,
        "class_observations": class_observations,
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
    strict_json_dumps(result)
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
    print(f"Wrote DaySchedule metrics oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

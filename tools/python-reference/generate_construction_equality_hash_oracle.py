"""Generate the pinned InvisibleDragon construction equality/hash oracle.

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
from typing import Any, Callable


SCHEMA = "dragons.invisibledragon.construction-equality-hash-oracle.v1"
INVENTORY_SCHEMA = "dragons.upstream-public-symbol-inventory.v2"
SOURCE_PATH = "src/idragon/dragon/construction.py"
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622"
)
EXPECTED_SYMBOL_HASHES = {
    "Construction.__eq__": (
        "sha256:8bf568b5f76ed813063ea04fd2eedf087e8f2525c2be9b9febbdb150a906b019"
    ),
    "Construction.__hash__": (
        "sha256:5994dd14a598a335d7945a1e39b59d93fd6bed9afbaff1308019b57bf22d0889"
    ),
    "Layer.__eq__": (
        "sha256:b3fd4452af62f2d402279427187e70e6165f14be9a0b0543f8702dee39a473e6"
    ),
    "Layer.__hash__": (
        "sha256:5994dd14a598a335d7945a1e39b59d93fd6bed9afbaff1308019b57bf22d0889"
    ),
    "Material.__eq__": (
        "sha256:6ef680a2e300bcb56672f0d036de1b4aea3630cb90a635a3e45002ff8535dbf5"
    ),
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)

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


def canonical_sha256(value: Any) -> str:
    encoded = json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return f"sha256:{hashlib.sha256(encoded).hexdigest()}"


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
        raise SystemExit(
            "The requested upstream commit is not the pinned equality/hash oracle commit."
        )

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
        require_hash(
            item["content_hash"],
            f"Public-symbol inventory files[{index}].content_hash",
        )
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
        inventory["content_sha256"],
        "Public-symbol inventory content_sha256",
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
        raise SystemExit("The inventory does not exactly cover the five target symbols.")
    for item in target_symbols:
        expected_hash = EXPECTED_SYMBOL_HASHES[item["symbol"]]
        if item["symbol_hash"] != expected_hash:
            raise SystemExit(f"The inventory hash for {item['symbol']} is not pinned.")

    source_files = [item for item in files if item["path"] == SOURCE_PATH]
    if len(source_files) != 1:
        raise SystemExit("The inventory does not contain one exact construction source file.")
    if source_files[0]["content_hash"] != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The inventoried construction source hash is not pinned.")

    return {
        "content_sha256": computed_inventory_hash,
        "file": source_files[0],
        "symbols": target_symbols,
    }


def observe(call: Callable[[], Any]) -> dict[str, Any]:
    try:
        return {"outcome": "returned", "value": call()}
    except Exception as exception:  # The exact pinned behavior is oracle data.
        return {
            "exception_message": str(exception),
            "exception_type": type(exception).__name__,
            "outcome": "raised",
        }


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON:
        raise SystemExit("Python 3.12.7 is required for the equality/hash oracle.")
    if sys.implementation.name != "cpython":
        raise SystemExit("CPython is required for the equality/hash oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0":
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic hash observations.")
    if sys.flags.hash_randomization != 0:
        raise SystemExit("CPython hash randomization must be disabled by PYTHONHASHSEED=0.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)

    import idragon.dragon.construction as construction_module
    from idragon.dragon.construction import Construction, Layer, Material, MaterialRoughness

    imported_source = Path(construction_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported construction module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported construction module is not the inventoried source.")

    material = Material(
        "Brick",
        0.72,
        1920,
        840,
        thermal_absorptance=0.1,
        solar_absorptance=0.2,
        visible_absorptance=0.3,
        roughness=MaterialRoughness.VERYROUGH,
    )
    material_same_core = Material(
        "Brick",
        0.72,
        1920,
        840,
        thermal_absorptance=0.9,
        solar_absorptance=0.8,
        visible_absorptance=0.7,
        roughness=MaterialRoughness.SMOOTH,
    )
    material_cases = [
        {"case": "same-core-ignore-optical-and-roughness", "equal": material == material_same_core},
        {"case": "different-name", "equal": material == Material("Other", 0.72, 1920, 840)},
        {"case": "different-conductivity", "equal": material == Material("Brick", 0.73, 1920, 840)},
        {"case": "different-density", "equal": material == Material("Brick", 0.72, 1919, 840)},
        {"case": "different-specific-heat", "equal": material == Material("Brick", 0.72, 1920, 841)},
    ]

    layer = Layer("Exterior concrete", material, 0.2)
    layer_same_value = Layer("Interior concrete", material_same_core, 0.2)
    layer_changed_thickness = Layer("Exterior concrete", material, 0.21)
    layer_changed_material = Layer(
        "Exterior concrete",
        Material("Brick", 0.73, 1920, 840),
        0.2,
    )
    layer_other_name = Layer("Other layer", material, 0.2)
    layer_cases = [
        {"case": "renamed-layer-same-material-and-thickness", "equal": layer == layer_same_value},
        {"case": "different-thickness", "equal": layer == layer_changed_thickness},
        {"case": "different-material", "equal": layer == layer_changed_material},
    ]

    insulation = Material("Insulation", 0.04, 30, 1400)
    outside = Layer("Concrete outside", material, 0.2)
    inside = Layer("Insulation inside", insulation, 0.1)
    construction = Construction("Wall", outside, inside)
    construction_same_value = Construction(
        "Wall",
        Layer("Renamed concrete", material_same_core, 0.2),
        Layer("Renamed insulation", insulation, 0.1),
    )
    construction_renamed = Construction("Other", *construction_same_value.layers)
    construction_reversed = Construction("Wall", *reversed(construction_same_value.layers))
    construction_fewer = Construction("Wall", construction_same_value.layers[0])
    construction_cases = [
        {"case": "same-name-same-ordered-layer-values", "equal": construction == construction_same_value},
        {"case": "different-name", "equal": construction == construction_renamed},
        {"case": "reversed-layer-order", "equal": construction == construction_reversed},
        {"case": "fewer-layers", "equal": construction == construction_fewer},
    ]

    symbols_by_name = {item["symbol"]: item for item in inventory["symbols"]}
    result = {
        "inventory_sha256": inventory["content_sha256"],
        "runtime": {
            "python_hash_algorithm": sys.hash_info.algorithm,
            "python_hash_seed": 0,
            "python_hash_width_bits": sys.hash_info.width,
            "python_version": ".".join(map(str, sys.version_info[:3])),
        },
        "schema": SCHEMA,
        "source": {
            "content_sha256": imported_source_sha256,
            "path": SOURCE_PATH,
        },
        "symbols": [
            {
                "null_operand": observe(lambda: construction == None),
                "same_type_cases": construction_cases,
                "symbol": "Construction.__eq__",
                "symbol_hash": symbols_by_name["Construction.__eq__"]["symbol_hash"],
            },
            {
                "hash_dependency": {
                    "base": {
                        "name": construction.name,
                        "name_hash": hash(construction.name),
                        "object_hash": hash(construction),
                    },
                    "checks": {
                        "object_hash_equals_name_hash": hash(construction) == hash(construction.name),
                        "same_name_reversed_layers_same_hash": hash(construction) == hash(construction_reversed),
                    },
                    "same_name_reversed_layers": {
                        "name": construction_reversed.name,
                        "name_hash": hash(construction_reversed.name),
                        "object_hash": hash(construction_reversed),
                    },
                },
                "symbol": "Construction.__hash__",
                "symbol_hash": symbols_by_name["Construction.__hash__"]["symbol_hash"],
            },
            {
                "null_operand": observe(lambda: layer == None),
                "same_type_cases": layer_cases,
                "symbol": "Layer.__eq__",
                "symbol_hash": symbols_by_name["Layer.__eq__"]["symbol_hash"],
            },
            {
                "hash_dependency": {
                    "base": {
                        "name": layer.name,
                        "name_hash": hash(layer.name),
                        "object_hash": hash(layer),
                    },
                    "checks": {
                        "different_name_observed": layer.name != layer_other_name.name,
                        "equal_different_name": layer == layer_other_name,
                        "equal_different_name_hashes_differ": hash(layer) != hash(layer_other_name),
                        "object_hash_equals_name_hash": hash(layer) == hash(layer.name),
                        "same_name_changed_thickness_same_hash": hash(layer) == hash(layer_changed_thickness),
                    },
                    "different_name": {
                        "name": layer_other_name.name,
                        "name_hash": hash(layer_other_name.name),
                        "object_hash": hash(layer_other_name),
                    },
                    "same_name_changed_thickness": {
                        "name": layer_changed_thickness.name,
                        "name_hash": hash(layer_changed_thickness.name),
                        "object_hash": hash(layer_changed_thickness),
                    },
                },
                "symbol": "Layer.__hash__",
                "symbol_hash": symbols_by_name["Layer.__hash__"]["symbol_hash"],
            },
            {
                "null_operand": observe(lambda: material == None),
                "same_type_cases": material_cases,
                "symbol": "Material.__eq__",
                "symbol_hash": symbols_by_name["Material.__eq__"]["symbol_hash"],
            },
        ],
        "upstream_commit": commit,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(
            result,
            allow_nan=False,
            ensure_ascii=False,
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote construction equality/hash oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

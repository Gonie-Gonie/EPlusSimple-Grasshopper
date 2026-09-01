"""Generate bounded observations for shading-material IDF leaf emitters.

The corpus targets only ``Blind.to_idf_object`` and ``Shade.to_idf_object``.
Constructor permissiveness and failure timing are recorded as context, but the
constructor/class receipts, Surface integration, shading controls, and the
general IdfObject implementation remain separate closure boundaries.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import inspect
import os
from pathlib import Path
import sys
from typing import Any


SCHEMA = "dragons.python-reference.dragon-shape-shading-material-to-idf-object.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
SHAPE_SOURCE_PATH = "src/idragon/dragon/shape.py"
EXPECTED_SYMBOL_RECEIPTS = {
    "Blind.to_idf_object": {
        "body_hash": "sha256:dbdfe63eb69145e34565287fea0891f7bafaeb23b5a147b7f8d6799a8f6b652b",
        "kind": "function",
        "signature_hash": "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
        "symbol_hash": "sha256:16e274127d87265296d229708222d131dbf0885a06196f088f42ade37e18b231",
    },
    "Shade.to_idf_object": {
        "body_hash": "sha256:db351161de65aa88fe02fa9488fdab4ca99c8f8643ff18479fcd91e63de71ef9",
        "kind": "function",
        "signature_hash": "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
        "symbol_hash": "sha256:75e6c8e673fc64d8f7966286fd2094b4d958b170903af6413f91b92ce095d66c",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
ADAPTATION = "model-context-shading-material-idf-assembly"
ASSERTION_IDS = {
    "Blind.to_idf_object": "dragon-shape-blind-to-idf-object-16e27412",
    "Shade.to_idf_object": "dragon-shape-shade-to-idf-object-75e6c8e6",
}
NATIVE_TARGETS = {
    symbol: "EnergyModel.ToIdfDocument" for symbol in TARGET_SYMBOLS
}
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-shape-shading-material-to-idf-object.blind.alternate-values",
        "Blind.to_idf_object",
        "returned",
    ),
    (
        "dragon-shape-shading-material-to-idf-object.blind.permissive-invalid-state",
        "Blind.to_idf_object",
        "constructor-rejected",
    ),
    (
        "dragon-shape-shading-material-to-idf-object.blind.representative-fields-and-freshness",
        "Blind.to_idf_object",
        "returned",
    ),
    (
        "dragon-shape-shading-material-to-idf-object.shade.alternate-values",
        "Shade.to_idf_object",
        "returned",
    ),
    (
        "dragon-shape-shading-material-to-idf-object.shade.permissive-invalid-and-type-failure",
        "Shade.to_idf_object",
        "constructor-rejected",
    ),
    (
        "dragon-shape-shading-material-to-idf-object.shade.representative-fields-and-freshness",
        "Shade.to_idf_object",
        "returned",
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 6
EXPECTED_CASE_COUNTS = {symbol: 3 for symbol in TARGET_SYMBOLS}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_shading_material_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load shading-material support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("Shading-material support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
SOURCE_RECEIPTS = CORE.SOURCE_RECEIPTS
SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == SHAPE_SOURCE_PATH else (),
    }
    for path, ast_hash, source_hash in SOURCE_RECEIPTS
)
EXPECTED_DEPENDENCIES = CORE.EXPECTED_DEPENDENCIES
strict_json_dumps = CORE.strict_json_dumps
canonical_sha256 = CORE.canonical_sha256
sha256_file = CORE.sha256_file
load_json_without_duplicates = CORE.load_json_without_duplicates
RAW_ADDRESS_PATTERN = CORE.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = CORE.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = CORE.GUID_PATTERN
TIMESTAMP_PATTERN = CORE.TIMESTAMP_PATTERN


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(helper, name) for name in names}
    try:
        helper.SOURCE_PATH = source["path"]
        helper.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        helper.EXPECTED_SYMBOL_HASHES = {
            symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"]
            for symbol in source["symbols"]
        }
        helper.TARGET_SYMBOLS = tuple(source["symbols"])
        result = helper.load_exact_inventory(path, commit)
    finally:
        for name, value in original.items():
            setattr(helper, name, value)

    expected_file = {
        "ast_hash": source["ast_sha256"],
        "content_hash": source["source_sha256"],
        "path": source["path"],
    }
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": source["path"],
            "symbol": symbol,
        }
        for symbol in source["symbols"]
    ]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    items = [_load_source_inventory(path, commit, source) for source in SOURCE_SPECS]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in items):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in items],
        "symbols": [symbol for item in items for symbol in item["symbols"]],
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "executor": "shading-material-to-idf-object",
            "expected_dotnet": {"adaptation": ADAPTATION, "outcome": outcome},
            "id": identifier,
            "symbol": symbol,
        }
        for identifier, symbol, outcome in EXPECTED_CASE_BINDINGS
    )


def _encode(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if isinstance(value, bool):
        return {"kind": "bool", "value": value}
    if isinstance(value, int):
        return {"kind": "int", "value": str(value)}
    if isinstance(value, float):
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    if isinstance(value, str):
        return {"kind": "str", "value": value}
    return {"kind": "object", "type": type(value).__name__}


def _ordered_fields(value: Any) -> list[dict[str, Any]]:
    return [
        {"name": name, "value": _encode(field_value)}
        for name, field_value in value.data.items()
    ]


def _state(value: Any, names: tuple[str, ...]) -> list[dict[str, Any]]:
    return [
        {"name": name, "value": _encode(getattr(value, name))}
        for name in names
    ]


def _emission(value: Any) -> dict[str, Any]:
    first = value.to_idf_object()
    second = value.to_idf_object()
    return {
        "first_object_type": first[0].idd.name,
        "fresh_idf_object": first[0] is not second[0],
        "fresh_result_list": first is not second,
        "object_count": len(first),
        "ordered_fields": _ordered_fields(first[0]),
        "result_type": type(first).__name__,
        "same_idd_definition": first[0].idd is second[0].idd,
        "second_fields_equal": list(first[0].data.items())
        == list(second[0].data.items()),
    }


def _attempt(function: Any) -> dict[str, Any]:
    try:
        result = function()
    except Exception as error:
        return {
            "args": [str(value) for value in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned", "result_type": type(result).__name__}


BLIND_STATE_NAMES = (
    "name",
    "slat_width",
    "slat_separation",
    "slat_angle",
    "front_reflectance",
    "back_reflectance",
)
SHADE_STATE_NAMES = ("name", "transmittance", "reflectance")


def _blind_facts(shape: Any, values: tuple[Any, ...]) -> dict[str, Any]:
    blind = shape.Blind(*values)
    before = _state(blind, BLIND_STATE_NAMES)
    emission = _emission(blind)
    return {
        "constructor_context": {
            "input_identity_preserved": all(
                getattr(blind, name) is value
                for name, value in zip(BLIND_STATE_NAMES, values)
            ),
            "parameter_order": list(inspect.signature(shape.Blind).parameters),
            "returned": True,
            "state": before,
            "state_unchanged_after_two_emissions": before
            == _state(blind, BLIND_STATE_NAMES),
        },
        "emission": emission,
    }


def _shade_facts(shape: Any, values: tuple[Any, ...]) -> dict[str, Any]:
    shade = shape.Shade(*values)
    before = _state(shade, SHADE_STATE_NAMES)
    emission = _emission(shade)
    return {
        "constructor_context": {
            "input_identity_preserved": all(
                getattr(shade, name) is value
                for name, value in zip(SHADE_STATE_NAMES, values)
            ),
            "parameter_order": list(inspect.signature(shape.Shade).parameters),
            "returned": True,
            "state": before,
            "state_unchanged_after_two_emissions": before
            == _state(shade, SHADE_STATE_NAMES),
        },
        "emission": emission,
    }


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    shape = modules.shape
    if identifier == EXPECTED_CASE_IDS[0]:
        return _blind_facts(
            shape,
            (
                "alternate blind",
                float("0.03125"),
                float("0.0625"),
                float("135"),
                float("0.25"),
                float("0.75"),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[1]:
        result = _blind_facts(
            shape,
            (
                "permissive blind",
                float("0"),
                float("-0.5"),
                float("-45"),
                float("1.25"),
                float("-0.25"),
            ),
        )
        result["input_conditions"] = {
            "angle_in_native_range": True,
            "dimensions_positive": False,
            "reflectances_in_unit_interval": False,
        }
        return result
    if identifier == EXPECTED_CASE_IDS[2]:
        return _blind_facts(
            shape,
            (
                "representative blind",
                float("0.05"),
                float("0.04"),
                float("45"),
                float("0.6"),
                float("0.4"),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[3]:
        return _shade_facts(
            shape,
            ("alternate shade", float("0.125"), float("0.25")),
        )
    if identifier == EXPECTED_CASE_IDS[4]:
        numeric = _shade_facts(
            shape,
            ("permissive shade", float("0.8"), float("0.4")),
        )
        nonnumeric = shape.Shade("nonnumeric shade", "opaque", float("0.1"))
        return {
            "nonnumeric_state": _state(nonnumeric, SHADE_STATE_NAMES),
            "nonnumeric_to_idf": _attempt(nonnumeric.to_idf_object),
            "numeric_input_conditions": {
                "components_in_unit_interval": True,
                "sum_not_greater_than_one": False,
            },
            "numeric_permissive_emission": numeric,
        }
    if identifier == EXPECTED_CASE_IDS[5]:
        return _shade_facts(
            shape,
            ("representative shade", float("0.2"), float("0.3")),
        )
    raise RuntimeError(f"Unknown shading-material case: {identifier}")


def _field(name: str, value: Any) -> dict[str, Any]:
    return {"name": name, "value": _encode(value)}


def _blind_fields(
    name: str,
    width: float,
    separation: float,
    angle: float,
    front: float,
    back: float,
) -> list[dict[str, Any]]:
    return [
        _field("Name", name),
        _field("Slat Orientation", "Horizontal"),
        _field("Slat Width", width),
        _field("Slat Separation", separation),
        _field("Slat Thickness", 0.00025),
        _field("Slat Angle", angle),
        _field("Slat Conductivity", 221.0),
        _field("Slat Beam Solar Transmittance", 0.0),
        _field("Front Side Slat Beam Solar Reflectance", front),
        _field("Back Side Slat Beam Solar Reflectance", back),
        _field("Slat Diffuse Solar Transmittance", 0.0),
        _field("Front Side Slat Diffuse Solar Reflectance", front),
        _field("Back Side Slat Diffuse Solar Reflectance", back),
        _field("Slat Beam Visible Transmittance", 0.0),
        _field("Front Side Slat Beam Visible Reflectance", None),
        _field("Back Side Slat Beam Visible Reflectance", None),
        _field("Slat Diffuse Visible Transmittance", 0.0),
        _field("Front Side Slat Diffuse Visible Reflectance", None),
        _field("Back Side Slat Diffuse Visible Reflectance", None),
        _field("Slat Infrared Hemispherical Transmittance", 0.0),
        _field("Front Side Slat Infrared Hemispherical Emissivity", 0.9),
        _field("Back Side Slat Infrared Hemispherical Emissivity", 0.9),
        _field("Blind to Glass Distance", 0.05),
        _field("Blind Top Opening Multiplier", 0.5),
        _field("Blind Bottom Opening Multiplier", 0.0),
        _field("Blind Left Side Opening Multiplier", 0.5),
        _field("Blind Right Side Opening Multiplier", 0.5),
        _field("Minimum Slat Angle", 0.0),
        _field("Maximum Slat Angle", 180.0),
    ]


def _shade_fields(
    name: str,
    transmittance: float,
    reflectance: float,
    emissivity: float,
) -> list[dict[str, Any]]:
    return [
        _field("Name", name),
        _field("Solar Transmittance", transmittance),
        _field("Solar Reflectance", reflectance),
        _field("Visible Transmittance", transmittance),
        _field("Visible Reflectance", reflectance),
        _field("Infrared Hemispherical Emissivity", emissivity),
        _field("Infrared Transmittance", transmittance),
        _field("Thickness", 0.01),
        _field("Conductivity", 100.0),
        _field("Shade to Glass Distance", 0.05),
        _field("Top Opening Multiplier", 0.5),
        _field("Bottom Opening Multiplier", 0.5),
        _field("Left-Side Opening Multiplier", 0.5),
        _field("Right-Side Opening Multiplier", 0.5),
        _field("Airflow Permeability", 0.0),
    ]


def _expected_emission(
    object_type: str, fields: list[dict[str, Any]]
) -> dict[str, Any]:
    return {
        "first_object_type": object_type,
        "fresh_idf_object": True,
        "fresh_result_list": True,
        "object_count": 1,
        "ordered_fields": fields,
        "result_type": "list",
        "same_idd_definition": True,
        "second_fields_equal": True,
    }


def _expected_constructor(
    names: tuple[str, ...], values: tuple[Any, ...]
) -> dict[str, Any]:
    return {
        "input_identity_preserved": True,
        "parameter_order": list(names),
        "returned": True,
        "state": [
            {"name": name, "value": _encode(value)}
            for name, value in zip(names, values)
        ],
        "state_unchanged_after_two_emissions": True,
    }


def _expected_blind(values: tuple[Any, ...]) -> dict[str, Any]:
    name, width, separation, angle, front, back = values
    return {
        "constructor_context": _expected_constructor(BLIND_STATE_NAMES, values),
        "emission": _expected_emission(
            "WindowMaterial:Blind",
            _blind_fields(name, width, separation, angle, front, back),
        ),
    }


def _expected_shade(
    values: tuple[Any, ...], emissivity: float
) -> dict[str, Any]:
    name, transmittance, reflectance = values
    return {
        "constructor_context": _expected_constructor(SHADE_STATE_NAMES, values),
        "emission": _expected_emission(
            "WindowMaterial:Shade",
            _shade_fields(name, transmittance, reflectance, emissivity),
        ),
    }


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return _expected_blind(
            ("alternate blind", 0.03125, 0.0625, 135.0, 0.25, 0.75)
        )
    if identifier == EXPECTED_CASE_IDS[1]:
        result = _expected_blind(
            ("permissive blind", 0.0, -0.5, -45.0, 1.25, -0.25)
        )
        result["input_conditions"] = {
            "angle_in_native_range": True,
            "dimensions_positive": False,
            "reflectances_in_unit_interval": False,
        }
        return result
    if identifier == EXPECTED_CASE_IDS[2]:
        return _expected_blind(
            ("representative blind", 0.05, 0.04, 45.0, 0.6, 0.4)
        )
    if identifier == EXPECTED_CASE_IDS[3]:
        return _expected_shade(("alternate shade", 0.125, 0.25), 0.625)
    if identifier == EXPECTED_CASE_IDS[4]:
        numeric = _expected_shade(
            ("permissive shade", 0.8, 0.4), -0.20000000000000007
        )
        return {
            "nonnumeric_state": [
                _field("name", "nonnumeric shade"),
                _field("transmittance", "opaque"),
                _field("reflectance", 0.1),
            ],
            "nonnumeric_to_idf": {
                "args": ["unsupported operand type(s) for -: 'int' and 'str'"],
                "message": "unsupported operand type(s) for -: 'int' and 'str'",
                "outcome": "raised",
                "type": "TypeError",
            },
            "numeric_input_conditions": {
                "components_in_unit_interval": True,
                "sum_not_greater_than_one": False,
            },
            "numeric_permissive_emission": numeric,
        }
    if identifier == EXPECTED_CASE_IDS[5]:
        return _expected_shade(("representative shade", 0.2, 0.3), 0.5)
    raise RuntimeError(f"Unknown shading-material case: {identifier}")


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _module_name(source_path: str) -> str:
    relative = Path(source_path).relative_to("src").with_suffix("")
    parts = list(relative.parts)
    if parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def _expected_loaded_local_modules() -> list[dict[str, str]]:
    return [
        {
            "ast_sha256": source["ast_sha256"],
            "module": _module_name(source["path"]),
            "path": source["path"],
            "source_sha256": source["source_sha256"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_files() -> list[dict[str, str]]:
    return [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_symbol_descriptors() -> list[dict[str, str]]:
    return [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SHAPE_SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "loaded_local_modules": _expected_loaded_local_modules(),
        "sources": [
            {
                "ast_sha256": source["ast_sha256"],
                "path": source["path"],
                "source_sha256": source["source_sha256"],
            }
            for source in SOURCE_SPECS
        ],
    }


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": {symbol: ADAPTATION for symbol in TARGET_SYMBOLS},
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
        "closure": {
            "context_only_not_targeted": [
                "Blind",
                "Blind.__init__",
                "Shade",
                "Shade.__init__",
                "Shading",
                "IdfObject",
                "IdfObject.__init__",
                "isolated-IdfObject-validation-policy",
            ],
            "full_symbol_closure": False,
            "scope": "bounded-valid-state-shading-material-emission-with-validation-context",
            "unresolved_behavior": [
                "standalone-shading-material-converter-API-shape",
                "invalid-or-nonnumeric-state-native-emission",
                "Surface",
                "Surface.blinded_window",
                "Surface.to_idf_object",
                "Window",
                "Window.__init__",
                "WindowShadingControl-emission",
                "EnergyModel.to_idf",
            ],
        },
        "identity_encoding": "booleans-only-no-id-or-address",
        "native_targets": NATIVE_TARGETS,
        "source_import_policy": (
            "external-temporary-copy-with-complete-loaded-local-module-audit"
        ),
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _dependencies() -> dict[str, str]:
    result: dict[str, str] = {}
    for distribution in EXPECTED_DEPENDENCIES:
        try:
            result[distribution] = importlib.metadata.version(distribution)
        except importlib.metadata.PackageNotFoundError as error:
            raise RuntimeError(
                f"Required reference dependency is missing: {distribution}"
            ) from error
    return result


def _expected_runtime() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches = []
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        if all(
            _source_file(root, source).is_file()
            and sha256_file(_source_file(root, source)) == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
    }:
        raise SystemExit("The aggregate shading-material inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        cases = []
        for definition in case_definitions():
            facts = _execute_case(definition["id"], modules)
            if facts != expected_facts(definition["id"]):
                raise SystemExit(
                    "Pinned Python shading-material semantics drifted: "
                    + definition["id"]
                    + "\n"
                    + strict_json_dumps(facts, indent=2)
                )
            case = dict(definition)
            case["python"] = {"facts": facts, "outcome": "returned"}
            cases.append(case)

    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": _expected_consumer_contract(),
        "runtime": {
            "dependencies": _dependencies(),
            "implementation": sys.implementation.name,
            "python_dont_write_bytecode": sys.dont_write_bytecode,
            "python_hash_algorithm": sys.hash_info.algorithm,
            "python_hash_seed": 0,
            "python_hash_width_bits": sys.hash_info.width,
            "python_version": ".".join(map(str, sys.version_info[:3])),
        },
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "upstream": {
            **_expected_upstream(),
            "commit": commit,
            "loaded_local_modules": modules.loaded_local_modules,
            "sources": [
                {
                    "ast_sha256": source["ast_sha256"],
                    "path": source["path"],
                    "source_sha256": sha256_file(_source_file(imported_root, source)),
                }
                for source in SOURCE_SPECS
            ],
        },
    }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if isinstance(value, float):
        raise RuntimeError(f"Raw float is forbidden at {location}.")
    if isinstance(value, str):
        if ABSOLUTE_PATH_PATTERN.search(value):
            raise RuntimeError(f"Absolute path is forbidden at {location}.")
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"Raw address is forbidden at {location}.")
        if GUID_PATTERN.search(value):
            raise RuntimeError(f"GUID-like value is forbidden at {location}.")
        if TIMESTAMP_PATTERN.search(value):
            raise RuntimeError(f"Timestamp is forbidden at {location}.")
        return
    if value is None or isinstance(value, (bool, int)):
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string JSON key is forbidden at {location}.")
            _validate_safe_tree(key, f"{location}.<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Unsupported JSON value at {location}: {type(value).__name__}")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "cases",
            "cases_sha256",
            "consumer_contract",
            "runtime",
            "schema",
            "symbols",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Shading-material schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Shading-material cases hash drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Shading-material case order/count drifted.")
    if (
        list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Pinned shading-material case IDs drifted.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Shading-material per-symbol case counts drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Shading-material case contract drifted: {case['id']}")
        _require_keys(case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if (
            case["python"]["outcome"] != "returned"
            or case["python"]["facts"] != expected_facts(case["id"])
        ):
            raise RuntimeError(f"Shading-material semantics drifted: {case['id']}")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Shading-material consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Shading-material runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Shading-material upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Shading-material symbol receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned checkout.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")

    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote dragon shape shading-material oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

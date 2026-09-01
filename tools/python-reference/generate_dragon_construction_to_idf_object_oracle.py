"""Generate bounded observations for construction-family IDF leaf emitters.

The corpus targets exactly five ``to_idf_object`` methods in the pinned
``idragon.dragon.construction`` module.  It records complete ordered raw IDF
fields for common valid states while keeping constructors, validation,
equality/hash behavior, parent geometry assembly, and native model-level
deduplication as separate closure boundaries.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import os
from pathlib import Path
import sys
from types import SimpleNamespace
from typing import Any


SCHEMA = "dragons.python-reference.dragon-construction-to-idf-object.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
CONSTRUCTION_SOURCE_PATH = "src/idragon/dragon/construction.py"
EXPECTED_CONSTRUCTION_SOURCE_SHA256 = (
    "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622"
)
EXPECTED_CONSTRUCTION_AST_SHA256 = (
    "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "AirBoundary.to_idf_object": {
        "body_hash": "sha256:ada40fa4a3bb88a012f0e91622290700bbae9e525bb8f07ac918f39290e2d325",
        "kind": "function",
        "signature_hash": "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
        "symbol_hash": "sha256:639a205f5c73ed6febc52735b33521b20dbeb644fcc4fd6ac2e148439c4e9545",
    },
    "Construction.to_idf_object": {
        "body_hash": "sha256:a878a51cb6bbfabee7834f446fba29d5b996ba549dd02e93607132b203f47d4c",
        "kind": "function",
        "signature_hash": "sha256:b55fe94795be2a00b3d45a008615fcf1e3efee2bfa89946d24c74488c2b8fb1c",
        "symbol_hash": "sha256:71a76f27ebadf7476c2746f1634258a52b3f16bd19e01624d9ce3809afc37309",
    },
    "Glazing.to_idf_object": {
        "body_hash": "sha256:10b7267535d8de4d92cfa27a6718948e6819ff322d369c51cf9032aae397034b",
        "kind": "function",
        "signature_hash": "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b",
        "symbol_hash": "sha256:3350beafdd06d7e477a86dedc271f9e4e71452dafd3137dcd8e512f94f58d093",
    },
    "Layer.to_idf_object": {
        "body_hash": "sha256:613dae616f8daa7794182d70a909db46722aff6a3c7f9fc68382082231959429",
        "kind": "function",
        "signature_hash": "sha256:aad6529bd53e6b00992b78af16eec99debb2fe8c83c692755dbdc772c8094008",
        "symbol_hash": "sha256:66e6d4589806a69db0d4023bcd6160f1e9a7079ed4aac3f3cb5f0839307fc884",
    },
    "NoMassConstruction.to_idf_object": {
        "body_hash": "sha256:ef1eb1b1d4ae714edb40b1feb6fec0c62e89a7623bc88353eb60c1093e2bfa6a",
        "kind": "function",
        "signature_hash": "sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b",
        "symbol_hash": "sha256:2bc3fe982f11770f5e4e23b97b52c608fc40f61d86d4d1afff00b0077626b096",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
ADAPTATIONS = {
    "AirBoundary.to_idf_object": "model-context-air-boundary-idf-emission",
    "Construction.to_idf_object": "model-context-construction-idf-emission",
    "Glazing.to_idf_object": "model-context-glazing-idf-emission",
    "Layer.to_idf_object": "model-context-layer-idf-emission",
    "NoMassConstruction.to_idf_object": (
        "model-context-no-mass-construction-idf-emission"
    ),
}
ASSERTION_IDS = {
    symbol: "dragon-construction-"
    + symbol.split(".", 1)[0]
    .replace("NoMassConstruction", "no-mass-construction")
    .replace("AirBoundary", "air-boundary")
    .lower()
    + "-to-idf-object-"
    + receipt["symbol_hash"][7:15]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
NATIVE_TARGETS = {
    "AirBoundary.to_idf_object": (
        "EnergyModel.ToIdfDocument via private "
        "EnergyModelIdfAssembler.AppendSurfaceConstruction"
    ),
    "Construction.to_idf_object": (
        "EnergyModel.ToIdfDocument via private "
        "EnergyModelIdfAssembler.AppendSurfaceConstruction"
    ),
    "Glazing.to_idf_object": (
        "EnergyModel.ToIdfDocument via private "
        "EnergyModelIdfAssembler.AppendGlazing"
    ),
    "Layer.to_idf_object": (
        "EnergyModel.ToIdfDocument via private "
        "EnergyModelIdfAssembler.AppendSurfaceConstruction"
    ),
    "NoMassConstruction.to_idf_object": (
        "EnergyModel.ToIdfDocument via private "
        "EnergyModelIdfAssembler.AppendSurfaceConstruction"
    ),
}
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-construction-to-idf-object.air-boundary.alternate-ach",
        "AirBoundary.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.air-boundary.representative-ach",
        "AirBoundary.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.construction.multi-layer-surface-scope",
        "Construction.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.construction.single-layer-surface-scope",
        "Construction.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.glazing.alternate-values",
        "Glazing.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.glazing.representative-values",
        "Glazing.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.layer.alternate-material-values",
        "Layer.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.layer.representative-material-values",
        "Layer.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.no-mass-construction.alternate-u",
        "NoMassConstruction.to_idf_object",
    ),
    (
        "dragon-construction-to-idf-object.no-mass-construction.representative-u",
        "NoMassConstruction.to_idf_object",
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 10
EXPECTED_CASE_COUNTS = {symbol: 2 for symbol in TARGET_SYMBOLS}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_construction_idf_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load construction IDF support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    construction_receipts = [
        item
        for item in module.SOURCE_RECEIPTS
        if item[0] == CONSTRUCTION_SOURCE_PATH
    ]
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
        or construction_receipts
        != [
            (
                CONSTRUCTION_SOURCE_PATH,
                EXPECTED_CONSTRUCTION_AST_SHA256,
                EXPECTED_CONSTRUCTION_SOURCE_SHA256,
            )
        ]
    ):
        raise RuntimeError("Construction IDF support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
SOURCE_RECEIPTS = CORE.SOURCE_RECEIPTS
SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == CONSTRUCTION_SOURCE_PATH else (),
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
            "executor": "construction-to-idf-object",
            "expected_dotnet": {
                "adaptation": ADAPTATIONS[symbol],
                "outcome": "returned",
            },
            "id": identifier,
            "symbol": symbol,
        }
        for identifier, symbol in EXPECTED_CASE_BINDINGS
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


def _field(name: str, value: Any) -> dict[str, Any]:
    return {"name": name, "value": _encode(value)}


def _ordered_fields(value: Any) -> list[dict[str, Any]]:
    return [_field(name, field_value) for name, field_value in value.data.items()]


def _object_record(value: Any) -> dict[str, Any]:
    return {
        "field_count": len(value.data),
        "object_type": value.idd.name,
        "ordered_fields": _ordered_fields(value),
    }


def _result_objects(value: Any) -> tuple[list[Any], bool]:
    if isinstance(value, list):
        return value, True
    return [value], False


def _emission(value: Any, *arguments: Any) -> dict[str, Any]:
    first = value.to_idf_object(*arguments)
    second = value.to_idf_object(*arguments)
    first_objects, first_is_list = _result_objects(first)
    second_objects, second_is_list = _result_objects(second)
    if first_is_list != second_is_list or len(first_objects) != len(second_objects):
        raise RuntimeError("Construction IDF result shape changed between calls.")
    return {
        "all_allowed_fields_covered_in_order": all(
            list(item.data) == list(item.allowed_keys) for item in first_objects
        ),
        "first_object_records": [_object_record(item) for item in first_objects],
        "first_objects_pairwise_distinct": len({id(item) for item in first_objects})
        == len(first_objects),
        "fresh_idf_object_flags": [
            first_item is not second_item
            for first_item, second_item in zip(
                first_objects, second_objects, strict=True
            )
        ],
        "fresh_result_list": first is not second if first_is_list else None,
        "fresh_return_value": first is not second,
        "object_count": len(first_objects),
        "object_types": [item.idd.name for item in first_objects],
        "result_type": type(first).__name__,
        "same_idd_definition_flags": [
            first_item.idd is second_item.idd
            for first_item, second_item in zip(
                first_objects, second_objects, strict=True
            )
        ],
        "second_fields_equal_flags": [
            list(first_item.data.items()) == list(second_item.data.items())
            for first_item, second_item in zip(
                first_objects, second_objects, strict=True
            )
        ],
        "second_objects_pairwise_distinct": len(
            {id(item) for item in second_objects}
        )
        == len(second_objects),
    }


def _air_state(value: Any) -> list[dict[str, Any]]:
    return [_field("name", value.name), _field("ACH", value.ACH)]


def _construction_state(value: Any, surface: Any) -> list[dict[str, Any]]:
    return [
        _field("name", value.name),
        {
            "name": "layer_names",
            "value": [_encode(layer.name) for layer in value.layers],
        },
        _field("surface.name", surface.name),
    ]


def _glazing_state(value: Any) -> list[dict[str, Any]]:
    return [
        _field("name", value.name),
        _field("U", value.U),
        _field("G", value.G),
    ]


def _layer_state(value: Any) -> list[dict[str, Any]]:
    material = value.material
    return [
        _field("name", value.name),
        _field("material.name", material.name),
        _field("material.roughness", str(material.roughness)),
        _field("thickness", value.thickness),
        _field("material.conductivity", material.conductivity),
        _field("material.density", material.density),
        _field("material.specific_heat", material.specific_heat),
        _field("material.thermal_absorptance", material.thermal_absorptance),
        _field("material.solar_absorptance", material.solar_absorptance),
        _field("material.visible_absorptance", material.visible_absorptance),
    ]


def _no_mass_state(value: Any) -> list[dict[str, Any]]:
    return [_field("name", value.name), _field("U", value.U)]


def _facts(
    value: Any,
    state_function: Any,
    *state_args: Any,
    to_idf_args: tuple[Any, ...] = (),
) -> dict[str, Any]:
    before = state_function(value, *state_args)
    emission = _emission(value, *to_idf_args)
    after = state_function(value, *state_args)
    return {
        "input_context": {
            "captured_state_scope": "properties-read-by-target-method",
            "source_state": before,
            "source_state_unchanged_after_two_emissions": before == after,
        },
        "emission": emission,
    }


def _material_layer(
    construction: Any,
    *,
    layer_name: str,
    material_name: str,
    roughness: Any,
    thickness: float,
    conductivity: float,
    density: float,
    specific_heat: float,
    thermal_absorptance: float,
    solar_absorptance: float,
    visible_absorptance: float,
) -> Any:
    material = construction.Material(
        material_name,
        conductivity,
        density,
        specific_heat,
        roughness=roughness,
        thermal_absorptance=thermal_absorptance,
        solar_absorptance=solar_absorptance,
        visible_absorptance=visible_absorptance,
    )
    return construction.Layer(layer_name, material, thickness)


def _execute_case(identifier: str, construction: Any) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return _facts(
            construction.AirBoundary("Transfer Air Alternate", 1.25), _air_state
        )
    if identifier == EXPECTED_CASE_IDS[1]:
        return _facts(
            construction.AirBoundary("Transfer Air Representative", 0.5),
            _air_state,
        )
    if identifier == EXPECTED_CASE_IDS[2]:
        layers = (
            _material_layer(
                construction,
                layer_name="Exterior Render 20mm",
                material_name="Exterior Render",
                roughness=construction.MaterialRoughness.ROUGH,
                thickness=0.02,
                conductivity=0.7,
                density=1600.0,
                specific_heat=850.0,
                thermal_absorptance=0.9,
                solar_absorptance=0.7,
                visible_absorptance=0.7,
            ),
            _material_layer(
                construction,
                layer_name="Structural Core 180mm",
                material_name="Structural Core",
                roughness=construction.MaterialRoughness.MEDIUMROUGH,
                thickness=0.18,
                conductivity=1.5,
                density=2200.0,
                specific_heat=900.0,
                thermal_absorptance=0.9,
                solar_absorptance=0.65,
                visible_absorptance=0.65,
            ),
            _material_layer(
                construction,
                layer_name="Interior Finish 13mm",
                material_name="Interior Finish",
                roughness=construction.MaterialRoughness.SMOOTH,
                thickness=0.013,
                conductivity=0.25,
                density=800.0,
                specific_heat=1090.0,
                thermal_absorptance=0.9,
                solar_absorptance=0.5,
                visible_absorptance=0.5,
            ),
        )
        value = construction.Construction("Wall Assembly", *layers)
        surface = SimpleNamespace(name="South Wall")
        return _facts(
            value, _construction_state, surface, to_idf_args=(surface,)
        )
    if identifier == EXPECTED_CASE_IDS[3]:
        layer = _material_layer(
            construction,
            layer_name="Roof Insulation 200mm",
            material_name="Roof Insulation",
            roughness=construction.MaterialRoughness.MEDIUMROUGH,
            thickness=0.2,
            conductivity=0.04,
            density=35.0,
            specific_heat=1400.0,
            thermal_absorptance=0.9,
            solar_absorptance=0.6,
            visible_absorptance=0.6,
        )
        value = construction.Construction("Roof Assembly", layer)
        surface = SimpleNamespace(name="Roof Plane")
        return _facts(
            value, _construction_state, surface, to_idf_args=(surface,)
        )
    if identifier == EXPECTED_CASE_IDS[4]:
        return _facts(
            construction.Glazing("Clear Glazing", 2.75, 0.625), _glazing_state
        )
    if identifier == EXPECTED_CASE_IDS[5]:
        return _facts(
            construction.Glazing("Triple Glazing", 0.8, 0.45), _glazing_state
        )
    if identifier == EXPECTED_CASE_IDS[6]:
        layer = _material_layer(
            construction,
            layer_name="Wood Fibre 80mm",
            material_name="Wood Fibre",
            roughness=construction.MaterialRoughness.SMOOTH,
            thickness=0.08,
            conductivity=0.125,
            density=160.0,
            specific_heat=2100.0,
            thermal_absorptance=0.85,
            solar_absorptance=0.5,
            visible_absorptance=0.45,
        )
        return _facts(layer, _layer_state)
    if identifier == EXPECTED_CASE_IDS[7]:
        layer = _material_layer(
            construction,
            layer_name="Dense Concrete 180mm",
            material_name="Dense Concrete",
            roughness=construction.MaterialRoughness.MEDIUMROUGH,
            thickness=0.18,
            conductivity=1.75,
            density=2300.0,
            specific_heat=900.0,
            thermal_absorptance=0.9,
            solar_absorptance=0.65,
            visible_absorptance=0.55,
        )
        return _facts(layer, _layer_state)
    if identifier == EXPECTED_CASE_IDS[8]:
        return _facts(
            construction.NoMassConstruction("Light Partition", 2.0),
            _no_mass_state,
        )
    if identifier == EXPECTED_CASE_IDS[9]:
        return _facts(
            construction.NoMassConstruction("Insulated Panel", 0.25),
            _no_mass_state,
        )
    raise RuntimeError(f"Unknown construction IDF case: {identifier}")


def _construction_fields(name: str, layers: tuple[str, ...]) -> list[dict[str, Any]]:
    if not 1 <= len(layers) <= 10:
        raise RuntimeError("Expected construction layer count is outside IDD bounds.")
    fields = [_field("Name", name), _field("Outside Layer", layers[0])]
    for index in range(2, 11):
        value = layers[index - 1] if index <= len(layers) else None
        fields.append(_field(f"Layer {index}", value))
    return fields


def _air_fields(name: str, ach: float) -> list[dict[str, Any]]:
    return [
        _field("Name", name),
        _field("Air Exchange Method", "SimpleMixing"),
        _field("Simple Mixing Air Changes per Hour", ach),
        _field("Simple Mixing Schedule Name", None),
    ]


def _glazing_object_fields(name: str, u_value: float, g_value: float) -> list[dict[str, Any]]:
    return [
        _field("Name", f"$GLAZING_FOR${name}"),
        _field("U-Factor", u_value),
        _field("Solar Heat Gain Coefficient", g_value),
        _field("Visible Transmittance", None),
    ]


def _layer_fields(
    layer_name: str,
    roughness: str,
    thickness: float,
    conductivity: float,
    density: float,
    specific_heat: float,
    thermal_absorptance: float,
    solar_absorptance: float,
    visible_absorptance: float,
) -> list[dict[str, Any]]:
    return [
        _field("Name", layer_name),
        _field("Roughness", roughness),
        _field("Thickness", thickness),
        _field("Conductivity", conductivity),
        _field("Density", density),
        _field("Specific Heat", specific_heat),
        _field("Thermal Absorptance", thermal_absorptance),
        _field("Solar Absorptance", solar_absorptance),
        _field("Visible Absorptance", visible_absorptance),
    ]


def _no_mass_fields(name: str, resistance: float) -> list[dict[str, Any]]:
    return [
        _field("Name", f"$MaterialFor$_{name}"),
        _field("Roughness", "Rough"),
        _field("Thermal Resistance", resistance),
        _field("Thermal Absorptance", 0.9),
        _field("Solar Absorptance", 0.7),
        _field("Visible Absorptance", 0.7),
    ]


def _record(object_type: str, fields: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "field_count": len(fields),
        "object_type": object_type,
        "ordered_fields": fields,
    }


def _expected_emission(
    result_type: str, objects: tuple[tuple[str, list[dict[str, Any]]], ...]
) -> dict[str, Any]:
    count = len(objects)
    return {
        "all_allowed_fields_covered_in_order": True,
        "first_object_records": [_record(name, fields) for name, fields in objects],
        "first_objects_pairwise_distinct": True,
        "fresh_idf_object_flags": [True] * count,
        "fresh_result_list": True if result_type == "list" else None,
        "fresh_return_value": True,
        "object_count": count,
        "object_types": [name for name, _fields in objects],
        "result_type": result_type,
        "same_idd_definition_flags": [True] * count,
        "second_fields_equal_flags": [True] * count,
        "second_objects_pairwise_distinct": True,
    }


def _expected_context(state: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "captured_state_scope": "properties-read-by-target-method",
        "source_state": state,
        "source_state_unchanged_after_two_emissions": True,
    }


def _expected_facts(
    state: list[dict[str, Any]],
    result_type: str,
    objects: tuple[tuple[str, list[dict[str, Any]]], ...],
) -> dict[str, Any]:
    return {
        "input_context": _expected_context(state),
        "emission": _expected_emission(result_type, objects),
    }


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return _expected_facts(
            [_field("name", "Transfer Air Alternate"), _field("ACH", 1.25)],
            "list",
            (("Construction:AirBoundary", _air_fields("Transfer Air Alternate", 1.25)),),
        )
    if identifier == EXPECTED_CASE_IDS[1]:
        return _expected_facts(
            [
                _field("name", "Transfer Air Representative"),
                _field("ACH", 0.5),
            ],
            "list",
            (
                (
                    "Construction:AirBoundary",
                    _air_fields("Transfer Air Representative", 0.5),
                ),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[2]:
        layers = (
            "Exterior Render 20mm",
            "Structural Core 180mm",
            "Interior Finish 13mm",
        )
        return _expected_facts(
            [
                _field("name", "Wall Assembly"),
                {"name": "layer_names", "value": [_encode(item) for item in layers]},
                _field("surface.name", "South Wall"),
            ],
            "list",
            (
                (
                    "Construction",
                    _construction_fields("Wall Assembly:for:South Wall", layers),
                ),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[3]:
        layers = ("Roof Insulation 200mm",)
        return _expected_facts(
            [
                _field("name", "Roof Assembly"),
                {"name": "layer_names", "value": [_encode(item) for item in layers]},
                _field("surface.name", "Roof Plane"),
            ],
            "list",
            (
                (
                    "Construction",
                    _construction_fields("Roof Assembly:for:Roof Plane", layers),
                ),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[4]:
        name, u_value, g_value = "Clear Glazing", 2.75, 0.625
        material_name = f"$GLAZING_FOR${name}"
        return _expected_facts(
            [_field("name", name), _field("U", u_value), _field("G", g_value)],
            "list",
            (
                (
                    "WindowMaterial:SimpleGlazingSystem",
                    _glazing_object_fields(name, u_value, g_value),
                ),
                ("Construction", _construction_fields(name, (material_name,))),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[5]:
        name, u_value, g_value = "Triple Glazing", 0.8, 0.45
        material_name = f"$GLAZING_FOR${name}"
        return _expected_facts(
            [_field("name", name), _field("U", u_value), _field("G", g_value)],
            "list",
            (
                (
                    "WindowMaterial:SimpleGlazingSystem",
                    _glazing_object_fields(name, u_value, g_value),
                ),
                ("Construction", _construction_fields(name, (material_name,))),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[6]:
        state = [
            _field("name", "Wood Fibre 80mm"),
            _field("material.name", "Wood Fibre"),
            _field("material.roughness", "Smooth"),
            _field("thickness", 0.08),
            _field("material.conductivity", 0.125),
            _field("material.density", 160.0),
            _field("material.specific_heat", 2100.0),
            _field("material.thermal_absorptance", 0.85),
            _field("material.solar_absorptance", 0.5),
            _field("material.visible_absorptance", 0.45),
        ]
        return _expected_facts(
            state,
            "IdfObject",
            (
                (
                    "Material",
                    _layer_fields(
                        "Wood Fibre 80mm",
                        "Smooth",
                        0.08,
                        0.125,
                        160.0,
                        2100.0,
                        0.85,
                        0.5,
                        0.45,
                    ),
                ),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[7]:
        state = [
            _field("name", "Dense Concrete 180mm"),
            _field("material.name", "Dense Concrete"),
            _field("material.roughness", "MediumRough"),
            _field("thickness", 0.18),
            _field("material.conductivity", 1.75),
            _field("material.density", 2300.0),
            _field("material.specific_heat", 900.0),
            _field("material.thermal_absorptance", 0.9),
            _field("material.solar_absorptance", 0.65),
            _field("material.visible_absorptance", 0.55),
        ]
        return _expected_facts(
            state,
            "IdfObject",
            (
                (
                    "Material",
                    _layer_fields(
                        "Dense Concrete 180mm",
                        "MediumRough",
                        0.18,
                        1.75,
                        2300.0,
                        900.0,
                        0.9,
                        0.65,
                        0.55,
                    ),
                ),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[8]:
        name, u_value = "Light Partition", 2.0
        material_name = f"$MaterialFor$_{name}"
        return _expected_facts(
            [_field("name", name), _field("U", u_value)],
            "list",
            (
                ("Material:NoMass", _no_mass_fields(name, 0.5)),
                ("Construction", _construction_fields(name, (material_name,))),
            ),
        )
    if identifier == EXPECTED_CASE_IDS[9]:
        name, u_value = "Insulated Panel", 0.25
        material_name = f"$MaterialFor$_{name}"
        return _expected_facts(
            [_field("name", name), _field("U", u_value)],
            "list",
            (
                ("Material:NoMass", _no_mass_fields(name, 4.0)),
                ("Construction", _construction_fields(name, (material_name,))),
            ),
        )
    raise RuntimeError(f"Unknown construction IDF case: {identifier}")


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
            "path": CONSTRUCTION_SOURCE_PATH,
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
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "native conversion is available only through private "
            "EnergyModelIdfAssembler model context, which compacts default fields "
            "and deduplicates shared definitions; standalone mutable-list parity "
            "is not claimed"
        ),
        "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
        "closure": {
            "context_only_not_targeted": [
                "AirBoundary",
                "AirBoundary.__init__",
                "AirBoundary.__repr__",
                "AirBoundary.__str__",
                "Construction",
                "Construction.__eq__",
                "Construction.__hash__",
                "Construction.__init__",
                "Construction.U",
                "Construction.heat_capacity",
                "Construction.reversed",
                "Construction.thickness",
                "Glazing",
                "Glazing.__init__",
                "Glazing.__repr__",
                "Glazing.__str__",
                "Glazing.G",
                "Glazing.U",
                "Layer",
                "Layer.__eq__",
                "Layer.__hash__",
                "Layer.__init__",
                "Layer.U",
                "Layer.heat_capacity",
                "Layer.material",
                "Layer.thickness",
                "Material",
                "MaterialRoughness",
                "NoMassConstruction",
                "NoMassConstruction.__init__",
                "NoMassConstruction.__repr__",
                "NoMassConstruction.__str__",
                "NoMassConstruction.U",
            ],
            "full_symbol_closure": False,
            "scope": (
                "bounded-common-valid-state-construction-family-idf-emission-"
                "in-model-context"
            ),
            "unresolved_behavior": [
                "all-five-class-constructor-property-equality-hash-contracts",
                "invalid-domain-and-error-semantics",
                "IdfObject",
                "IdfObject.__init__",
                "isolated-IdfObject-validation-policy",
                "Surface",
                "Surface.to_idf_object",
                "Zone",
                "Zone.to_idf_object",
                "EnergyModel.to_idf",
                "native-model-deduplication-and-conflict-semantics",
                "native-global-object-order-and-shared-material-compaction",
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
        raise SystemExit("The aggregate construction IDF inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        construction = sys.modules.get("idragon.dragon.construction")
        if construction is None:
            raise SystemExit("Pinned construction module was not loaded.")
        imported_construction = Path(construction.__file__).resolve()
        expected_construction = (
            Path(modules.shape.__file__).resolve().parents[2]
            / Path(CONSTRUCTION_SOURCE_PATH).relative_to("src")
        )
        if (
            imported_construction != expected_construction
            or sha256_file(imported_construction)
            != EXPECTED_CONSTRUCTION_SOURCE_SHA256
            or modules.shape.Construction is not construction.Construction
            or construction.IdfObject is not modules.imugi.IdfObject
        ):
            raise SystemExit("Pinned construction import identities drifted.")

        cases = []
        for definition in case_definitions():
            facts = _execute_case(definition["id"], construction)
            expected = expected_facts(definition["id"])
            if facts != expected:
                raise SystemExit(
                    "Pinned Python construction IDF semantics drifted: "
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
        raise RuntimeError("Construction IDF schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Construction IDF cases hash drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Construction IDF case order/count drifted.")
    if (
        list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Pinned construction IDF case IDs drifted.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Construction IDF per-symbol case counts drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Construction IDF case contract drifted: {case['id']}")
        _require_keys(
            case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet"
        )
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if (
            case["python"]["outcome"] != "returned"
            or case["python"]["facts"] != expected_facts(case["id"])
        ):
            raise RuntimeError(f"Construction IDF semantics drifted: {case['id']}")
        emission = case["python"]["facts"]["emission"]
        if emission["object_count"] != len(emission["first_object_records"]):
            raise RuntimeError(f"Construction IDF object count drifted: {case['id']}")
        for record in emission["first_object_records"]:
            if record["field_count"] != len(record["ordered_fields"]):
                raise RuntimeError(
                    f"Construction IDF field completeness drifted: {case['id']}"
                )

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Construction IDF consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Construction IDF runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Construction IDF upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Construction IDF symbol receipts drifted.")
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
    print(f"Wrote dragon construction IDF oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

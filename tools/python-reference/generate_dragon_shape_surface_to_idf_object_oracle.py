"""Generate pinned observations for ``Surface.to_idf_object``.

The five-case corpus preserves the historical rectangular opening emission,
child shading order, reciprocal interzone links, complete IDD-expanded fields,
and the custom-AirBoundary ``DefaultAirBoundary`` dangling-reference defect.
It targets only ``Surface.to_idf_object``; constructors, child converters, and
parent model assembly remain explicit context.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import sys
from types import SimpleNamespace
from typing import Any, Callable


SCHEMA = "dragons.python-reference.dragon-shape-surface-to-idf-object.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
SHAPE_SOURCE_PATH = "src/idragon/dragon/shape.py"
TARGET_SYMBOL = "Surface.to_idf_object"
EXPECTED_SYMBOL_RECEIPT = {
    "body_hash": "sha256:ab08fb2df61d8afa3cf2ad9b423c1e045de29f50d2c1469842934814f103aa9b",
    "kind": "function",
    "signature_hash": "sha256:ee1bf869a7f2dda7ebcd3108769369b9f5b3c52d60c68d9271d39a0f20315bd9",
    "symbol_hash": "sha256:a03c4d5229587498a9a3451a51c842e1f6df83e08ff5c42488a64959e384fece",
}
TARGET_SYMBOLS = (TARGET_SYMBOL,)
ADAPTATION = "legacy-rectangular-surface-idf-emission"
ASSERTION_ID = "dragon-shape-surface-to-idf-object-a03c4d52"
NATIVE_TARGET = (
    "EnergyModel.ToIdfDocument with UseLegacyRectangularFenestration"
)
PREFIX = "dragon-shape-surface-to-idf-object."
EXPECTED_CASE_BINDINGS = (
    (PREFIX + "adiabatic-ceiling.custom-air-boundary", TARGET_SYMBOL),
    (PREFIX + "ground-floor.pentagon", TARGET_SYMBOL),
    (
        PREFIX + "interzone-wall.reciprocal-two-windows-two-doors",
        TARGET_SYMBOL,
    ),
    (PREFIX + "outdoors-ceiling.roof", TARGET_SYMBOL),
    (PREFIX + "outdoors-wall.multiple-openings-blind-shade", TARGET_SYMBOL),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 5
EXPECTED_CASE_COUNTS = {TARGET_SYMBOL: EXPECTED_CASE_COUNT}
EXPECTED_OBJECT_TYPES = {
    EXPECTED_CASE_IDS[0]: ("BuildingSurface:Detailed",),
    EXPECTED_CASE_IDS[1]: ("BuildingSurface:Detailed",),
    EXPECTED_CASE_IDS[2]: (
        "Window:Interzone",
        "Window:Interzone",
        "Door:Interzone",
        "Door:Interzone",
        "BuildingSurface:Detailed",
        "Window:Interzone",
        "Window:Interzone",
        "Door:Interzone",
        "Door:Interzone",
        "BuildingSurface:Detailed",
    ),
    EXPECTED_CASE_IDS[3]: ("BuildingSurface:Detailed",),
    EXPECTED_CASE_IDS[4]: (
        "Window",
        "Window",
        "Window",
        "Door",
        "Door",
        "WindowMaterial:Blind",
        "WindowShadingControl",
        "WindowMaterial:Shade",
        "WindowShadingControl",
        "BuildingSurface:Detailed",
    ),
}
EXPECTED_FIELD_COUNTS = {
    EXPECTED_CASE_IDS[0]: (371,),
    EXPECTED_CASE_IDS[1]: (371,),
    EXPECTED_CASE_IDS[2]: (9, 9, 9, 9, 371, 9, 9, 9, 9, 371),
    EXPECTED_CASE_IDS[3]: (371,),
    EXPECTED_CASE_IDS[4]: (9, 9, 9, 8, 8, 29, 26, 15, 26, 371),
}
EXPECTED_CALL_SPANS = {
    EXPECTED_CASE_IDS[0]: (1,),
    EXPECTED_CASE_IDS[1]: (1,),
    EXPECTED_CASE_IDS[2]: (5, 5),
    EXPECTED_CASE_IDS[3]: (1,),
    EXPECTED_CASE_IDS[4]: (10,),
}
EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:6d7e9229fb591479a553f453edebaaf34132c997b4bbb31625e179ad094caefa",
    EXPECTED_CASE_IDS[1]: "sha256:0f259981989b7cbcbfe4033832b64ff3304e694656b577a3fedfd05ff2e31efa",
    EXPECTED_CASE_IDS[2]: "sha256:aa54a964ebadce1cdbc0717b7d43b32eb76a7c6f3f2ceffebb800496004fff1f",
    EXPECTED_CASE_IDS[3]: "sha256:dfee7032a57f0a7a2737fd5c934fe8ca9fc4432c93f3f8936c1b7232b61ffa11",
    EXPECTED_CASE_IDS[4]: "sha256:8910fb4c4633de0cea33e4c64ce60677eeffd97acce1058ecaf8a71302d2d6c6",
}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_surface_idf_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load Surface IDF support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("Surface IDF support is not exactly pinned.")
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
            symbol: EXPECTED_SYMBOL_RECEIPT["symbol_hash"]
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
            **EXPECTED_SYMBOL_RECEIPT,
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
            "executor": "surface-to-idf-object",
            "expected_dotnet": {
                "adaptation": ADAPTATION,
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
    raise RuntimeError(f"Unsupported Surface oracle value: {type(value).__name__}")


def _field(name: str, value: Any) -> dict[str, Any]:
    return {"name": name, "value": _encode(value)}


def _record(value: Any) -> dict[str, Any]:
    return {
        "field_count": len(value.data),
        "object_type": value.idd.name,
        "ordered_fields": [
            _field(name, field_value) for name, field_value in value.data.items()
        ],
    }


def _shading_state(value: Any, shape: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if isinstance(value, shape.Blind):
        return {
            "back_reflectance": _encode(value.back_reflectance),
            "front_reflectance": _encode(value.front_reflectance),
            "kind": "Blind",
            "name": value.name,
            "slat_angle": _encode(value.slat_angle),
            "slat_separation": _encode(value.slat_separation),
            "slat_width": _encode(value.slat_width),
        }
    if isinstance(value, shape.Shade):
        return {
            "kind": "Shade",
            "name": value.name,
            "reflectance": _encode(value.reflectance),
            "transmittance": _encode(value.transmittance),
        }
    raise RuntimeError(f"Unexpected shading dependency: {type(value).__name__}")


def _construction_state(value: Any, construction: Any) -> dict[str, Any]:
    result = {"name": value.name, "type": type(value).__name__}
    if isinstance(value, construction.AirBoundary):
        result["air_changes_per_hour"] = _encode(value.ACH)
    elif isinstance(value, construction.Construction):
        result["layers"] = [layer.name for layer in value.layers]
    return result


def _surface_state(value: Any, shape: Any, construction: Any) -> dict[str, Any]:
    boundary = value.boundary
    return {
        "boundary": (
            {"kind": "adjacent-surface", "name": boundary.name}
            if isinstance(boundary, shape.Surface)
            else {"kind": "boundary-condition", "value": boundary.value}
        ),
        "construction": _construction_state(value.construction, construction),
        "doors": [
            {
                "area": _encode(door.area),
                "construction_name": door.construction.name,
                "name": door.name,
            }
            for door in value.door
        ],
        "name": value.name,
        "surface_type": value.type.value,
        "vertices": [
            [_encode(vertex.x), _encode(vertex.y), _encode(vertex.z)]
            for vertex in value.vertex
        ],
        "windows": [
            {
                "area": _encode(window.area),
                "glazing_name": window.glazing.name,
                "name": window.name,
                "shading": _shading_state(window.blind, shape),
            }
            for window in value.window
        ],
    }


def _input_context(
    calls: list[tuple[Any, Any]], shape: Any, construction: Any
) -> dict[str, Any]:
    return {
        "calls": [
            {
                "surface": _surface_state(surface, shape, construction),
                "zone": {"kind": "name-only-parent-context", "name": zone.name},
            }
            for surface, zone in calls
        ],
        "captured_state_scope": (
            "properties-read-by-Surface.to_idf_object-and-explicit-zone-name-context"
        ),
    }


def _identity_snapshot(calls: list[tuple[Any, Any]]) -> dict[str, Any]:
    surfaces = tuple(surface for surface, _ in calls)
    return {
        "boundaries": tuple(surface.boundary for surface in surfaces),
        "constructions": tuple(surface.construction for surface in surfaces),
        "door_collections": tuple(surface.door for surface in surfaces),
        "door_constructions": tuple(
            tuple(door.construction for door in surface.door) for surface in surfaces
        ),
        "doors": tuple(tuple(surface.door) for surface in surfaces),
        "shadings": tuple(
            tuple(window.blind for window in surface.window) for surface in surfaces
        ),
        "surfaces": surfaces,
        "vertex_collections": tuple(surface.vertex for surface in surfaces),
        "vertices": tuple(tuple(surface.vertex) for surface in surfaces),
        "window_collections": tuple(surface.window for surface in surfaces),
        "window_glazings": tuple(
            tuple(window.glazing for window in surface.window) for surface in surfaces
        ),
        "windows": tuple(tuple(surface.window) for surface in surfaces),
        "zones": tuple(zone for _, zone in calls),
    }


def _same_references(current: Any, original: Any) -> bool:
    if isinstance(original, tuple):
        return isinstance(current, tuple) and len(current) == len(original) and all(
            _same_references(left, right)
            for left, right in zip(current, original, strict=True)
        )
    return current is original


def _input_integrity(
    calls: list[tuple[Any, Any]],
    identity: dict[str, Any],
    before: dict[str, Any],
    shape: Any,
    construction: Any,
) -> dict[str, bool]:
    current = _identity_snapshot(calls)
    return {
        "boundary_identities_preserved": _same_references(
            current["boundaries"], identity["boundaries"]
        ),
        "construction_identities_preserved": _same_references(
            current["constructions"], identity["constructions"]
        ),
        "door_collection_identities_preserved": _same_references(
            current["door_collections"], identity["door_collections"]
        ),
        "door_construction_identities_preserved": _same_references(
            current["door_constructions"], identity["door_constructions"]
        ),
        "door_identities_preserved": _same_references(
            current["doors"], identity["doors"]
        ),
        "shading_identities_preserved": _same_references(
            current["shadings"], identity["shadings"]
        ),
        "state_unchanged_after_two_emissions": before
        == _input_context(calls, shape, construction),
        "surface_identities_preserved": _same_references(
            current["surfaces"], identity["surfaces"]
        ),
        "vertex_collection_identities_preserved": _same_references(
            current["vertex_collections"], identity["vertex_collections"]
        ),
        "vertex_identities_preserved": _same_references(
            current["vertices"], identity["vertices"]
        ),
        "window_collection_identities_preserved": _same_references(
            current["window_collections"], identity["window_collections"]
        ),
        "window_glazing_identities_preserved": _same_references(
            current["window_glazings"], identity["window_glazings"]
        ),
        "window_identities_preserved": _same_references(
            current["windows"], identity["windows"]
        ),
        "zone_identities_preserved": _same_references(
            current["zones"], identity["zones"]
        ),
    }


def _record_field(record: dict[str, Any], name: str) -> dict[str, Any]:
    matches = [
        field["value"] for field in record["ordered_fields"] if field["name"] == name
    ]
    if len(matches) != 1:
        raise RuntimeError(
            f"Expected one {name!r} field in {record['object_type']}, got {len(matches)}."
        )
    return matches[0]


def _encoded_text(value: dict[str, Any]) -> str | None:
    if value["kind"] == "none":
        return None
    if value["kind"] != "str":
        raise RuntimeError(f"Expected an encoded string, got {value['kind']}.")
    return value["value"]


def _behavior_facts(
    calls: list[tuple[Any, Any]],
    batches: list[list[Any]],
    records: list[dict[str, Any]],
    construction: Any,
) -> dict[str, Any]:
    offsets: list[int] = []
    total = 0
    for batch in batches:
        offsets.append(total)
        total += len(batch)
    host_indices = [
        offset + next(
            index
            for index, item in enumerate(batch)
            if item.idd.name == "BuildingSurface:Detailed"
        )
        for offset, batch in zip(offsets, batches, strict=True)
    ]
    host_records = [records[index] for index in host_indices]
    custom_air_boundaries = [
        surface.construction
        for surface, _ in calls
        if isinstance(surface.construction, construction.AirBoundary)
    ]
    authored_air_name = (
        custom_air_boundaries[0].name if custom_air_boundaries else None
    )
    emitted_air_name = (
        _encoded_text(_record_field(host_records[0], "Construction Name"))
        if custom_air_boundaries
        else None
    )
    links = []
    for batch in batches:
        for item in batch:
            if item.idd.name in ("Window:Interzone", "Door:Interzone"):
                item_record = _record(item)
                links.append(
                    {
                        "counterpart_name": _encoded_text(
                            _record_field(
                                item_record, "Outside Boundary Condition Object"
                            )
                        ),
                        "name": _encoded_text(_record_field(item_record, "Name")),
                        "object_type": item.idd.name,
                    }
                )
    return {
        "air_boundary_reference": {
            "authored_construction_name": authored_air_name,
            "custom_construction_object_emitted": any(
                record["object_type"] == "Construction:AirBoundary"
                for record in records
            ),
            "dangling_default_reference": bool(custom_air_boundaries)
            and emitted_air_name == "DefaultAirBoundary"
            and emitted_air_name != authored_air_name,
            "emitted_construction_name": emitted_air_name,
        },
        "call_spans": [len(batch) for batch in batches],
        "host_surface_indices": host_indices,
        "host_surface_last_in_each_call": all(
            batch[-1].idd.name == "BuildingSurface:Detailed" for batch in batches
        ),
        "number_of_vertices_fields": [
            _record_field(record, "Number of Vertices") for record in host_records
        ],
        "opening_counterpart_links": links,
        "parent_zone_links": [
            {
                "emitted_zone_name": _encoded_text(
                    _record_field(record, "Zone Name")
                ),
                "surface_name": surface.name,
                "zone_name": zone.name,
            }
            for (surface, zone), record in zip(calls, host_records, strict=True)
        ],
        "surface_type_mappings": [
            {
                "authored_surface_type": surface.type.value,
                "emitted_surface_type": _encoded_text(
                    _record_field(record, "Surface Type")
                ),
                "surface_name": surface.name,
            }
            for (surface, _), record in zip(calls, host_records, strict=True)
        ],
        "vertex_counts": [len(surface.vertex) for surface, _ in calls],
    }


def _observe_twice(
    calls: list[tuple[Any, Any]],
    shape: Any,
    construction: Any,
) -> dict[str, Any]:
    before = _input_context(calls, shape, construction)
    identity = _identity_snapshot(calls)
    first_batches = [surface.to_idf_object(zone) for surface, zone in calls]
    second_batches = [surface.to_idf_object(zone) for surface, zone in calls]
    first = [item for batch in first_batches for item in batch]
    second = [item for batch in second_batches for item in batch]
    first_records = [_record(item) for item in first]
    second_records = [_record(item) for item in second]
    return {
        "behavior_facts": _behavior_facts(
            calls,
            first_batches,
            first_records,
            construction,
        ),
        "emission": {
            "all_allowed_fields_covered_in_order": all(
                list(item.data) == list(item.allowed_keys) for item in first
            ),
            "first_object_records": first_records,
            "first_objects_pairwise_distinct": len({id(item) for item in first})
            == len(first),
            "fresh_call_result_lists": all(
                left is not right
                for left, right in zip(first_batches, second_batches, strict=True)
            ),
            "fresh_idf_object_flags": [
                left is not right
                for left, right in zip(first, second, strict=True)
            ],
            "object_count": len(first),
            "object_types": [item.idd.name for item in first],
            "result_type": type(first_batches[0]).__name__,
            "same_idd_definition_flags": [
                left.idd is right.idd
                for left, right in zip(first, second, strict=True)
            ],
            "second_fields_equal_flags": [
                left_record == right_record
                for left_record, right_record in zip(
                    first_records, second_records, strict=True
                )
            ],
            "second_objects_pairwise_distinct": len({id(item) for item in second})
            == len(second),
        },
        "input_context": before,
        "input_integrity": _input_integrity(
            calls, identity, before, shape, construction
        ),
        "invocation": {
            "calls": [
                {"surface_name": surface.name, "zone_name": zone.name}
                for surface, zone in calls
            ]
        },
    }


def _vertices(shape: Any, values: tuple[tuple[float, float, float], ...]) -> list[Any]:
    return [shape.Vertex(x, y, z) for x, y, z in values]


def _opaque(construction: Any, name: str) -> Any:
    return construction.Construction(name)


def _surface(
    shape: Any,
    name: str,
    surface_type: Any,
    construction_value: Any,
    boundary: Any,
    vertices: tuple[tuple[float, float, float], ...],
    windows: list[Any] | None = None,
    doors: list[Any] | None = None,
) -> Any:
    return shape.Surface(
        name,
        surface_type,
        construction_value,
        boundary,
        _vertices(shape, vertices),
        list(windows or []),
        list(doors or []),
    )


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    shape = modules.shape
    construction = modules.construction
    if identifier == EXPECTED_CASE_IDS[0]:
        surface = _surface(
            shape,
            "Adiabatic Custom-Air Ceiling",
            shape.SurfaceType.CEILING,
            construction.AirBoundary("Custom Transfer Air Boundary", 0.73),
            shape.SurfaceBoundaryCondition.ADIABATIC,
            ((0.0, 0.0, 3.0), (5.0, 0.0, 3.0), (5.0, 4.0, 3.0), (0.0, 4.0, 3.0)),
        )
        return _observe_twice(
            [(surface, SimpleNamespace(name="Adiabatic Parent Zone"))],
            shape,
            construction,
        )
    if identifier == EXPECTED_CASE_IDS[1]:
        surface = _surface(
            shape,
            "Ground Pentagon Floor",
            shape.SurfaceType.FLOOR,
            _opaque(construction, "Ground Pentagon Assembly"),
            shape.SurfaceBoundaryCondition.GROUND,
            ((0.0, 0.0, 0.0), (4.0, 0.0, 0.0), (5.0, 2.0, 0.0), (2.0, 4.0, 0.0), (0.0, 2.0, 0.0)),
        )
        return _observe_twice(
            [(surface, SimpleNamespace(name="Ground Parent Zone"))],
            shape,
            construction,
        )
    if identifier == EXPECTED_CASE_IDS[2]:
        glazing = construction.Glazing("Interzone Shared Glazing", 1.45, 0.41)
        door_construction = construction.NoMassConstruction(
            "Interzone Door Assembly", 1.8
        )
        first_windows = [
            shape.Window("Interzone A Window 1", glazing, 2.2),
            shape.Window("Interzone A Window 2", glazing, 1.4),
        ]
        second_windows = [
            shape.Window("Interzone B Window 1", glazing, 2.2),
            shape.Window("Interzone B Window 2", glazing, 1.4),
        ]
        first_doors = [
            shape.Door("Interzone A Door 1", door_construction, 1.8),
            shape.Door("Interzone A Door 2", door_construction, 2.0),
        ]
        second_doors = [
            shape.Door("Interzone B Door 1", door_construction, 1.8),
            shape.Door("Interzone B Door 2", door_construction, 2.0),
        ]
        first = _surface(
            shape,
            "Interzone Wall A",
            shape.SurfaceType.WALL,
            _opaque(construction, "Interzone Wall Assembly A"),
            shape.SurfaceBoundaryCondition.OUTDOOR,
            ((0.0, 0.0, 0.0), (4.0, 0.0, 0.0), (4.0, 0.0, 3.0), (0.0, 0.0, 3.0)),
            first_windows,
            first_doors,
        )
        second = _surface(
            shape,
            "Interzone Wall B",
            shape.SurfaceType.WALL,
            _opaque(construction, "Interzone Wall Assembly B"),
            shape.SurfaceBoundaryCondition.OUTDOOR,
            ((0.0, 0.0, 0.0), (0.0, 0.0, 3.0), (4.0, 0.0, 3.0), (4.0, 0.0, 0.0)),
            second_windows,
            second_doors,
        )
        first.boundary = second
        return _observe_twice(
            [
                (first, SimpleNamespace(name="Interzone Parent Zone A")),
                (second, SimpleNamespace(name="Interzone Parent Zone B")),
            ],
            shape,
            construction,
        )
    if identifier == EXPECTED_CASE_IDS[3]:
        surface = _surface(
            shape,
            "Outdoor Ceiling Becomes Roof",
            shape.SurfaceType.CEILING,
            _opaque(construction, "Outdoor Roof Assembly"),
            shape.SurfaceBoundaryCondition.OUTDOOR,
            ((0.0, 0.0, 3.2), (0.0, 4.0, 3.2), (6.0, 4.0, 3.2), (6.0, 0.0, 3.2)),
        )
        return _observe_twice(
            [(surface, SimpleNamespace(name="Outdoor Roof Parent Zone"))],
            shape,
            construction,
        )
    if identifier == EXPECTED_CASE_IDS[4]:
        glazing = construction.Glazing("Outdoor Multi Glazing", 1.35, 0.38)
        door_construction = construction.NoMassConstruction(
            "Outdoor Door Assembly", 2.1
        )
        blind = shape.Blind("Strong Interior Blind", 0.025, 0.02, 45.0, 0.62, 0.55)
        shade = shape.Shade("Simple Interior Shade", 0.12, 0.48)
        windows = [
            shape.Window("Outdoor Blind Window", glazing, 1.2, blind),
            shape.Window("Outdoor Shade Window", glazing, 1.6, shade),
            shape.Window("Outdoor Clear Window", glazing, 0.9),
        ]
        doors = [
            shape.Door("Outdoor Door 1", door_construction, 1.9),
            shape.Door("Outdoor Door 2", door_construction, 2.2),
        ]
        surface = _surface(
            shape,
            "Outdoor Multi-Opening Wall",
            shape.SurfaceType.WALL,
            _opaque(construction, "Outdoor Wall Assembly"),
            shape.SurfaceBoundaryCondition.OUTDOOR,
            ((0.0, 0.0, 0.0), (8.0, 0.0, 0.0), (8.0, 0.0, 3.5), (0.0, 0.0, 3.5)),
            windows,
            doors,
        )
        return _observe_twice(
            [(surface, SimpleNamespace(name="Outdoor Openings Parent Zone"))],
            shape,
            construction,
        )
    raise RuntimeError(f"Unknown Surface IDF case: {identifier}")


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    actual_hash = canonical_sha256(facts)
    if actual_hash != EXPECTED_FACT_SHA256[identifier]:
        raise RuntimeError(
            f"Surface IDF canonical semantics drifted: {identifier}: {actual_hash}"
        )
    emission = facts["emission"]
    if tuple(emission["object_types"]) != EXPECTED_OBJECT_TYPES[identifier]:
        raise RuntimeError(f"Surface IDF object order drifted: {identifier}")
    if emission["object_count"] != len(emission["first_object_records"]):
        raise RuntimeError(f"Surface IDF object count drifted: {identifier}")
    if tuple(
        record["field_count"] for record in emission["first_object_records"]
    ) != EXPECTED_FIELD_COUNTS[identifier]:
        raise RuntimeError(f"Surface IDF field counts drifted: {identifier}")
    if tuple(facts["behavior_facts"]["call_spans"]) != EXPECTED_CALL_SPANS[identifier]:
        raise RuntimeError(f"Surface IDF call spans drifted: {identifier}")
    if (
        emission["result_type"] != "list"
        or not emission["all_allowed_fields_covered_in_order"]
        or not emission["first_objects_pairwise_distinct"]
        or not emission["second_objects_pairwise_distinct"]
        or not emission["fresh_call_result_lists"]
        or not all(emission["fresh_idf_object_flags"])
        or not all(emission["same_idd_definition_flags"])
        or not all(emission["second_fields_equal_flags"])
        or not all(facts["input_integrity"].values())
        or not facts["behavior_facts"]["host_surface_last_in_each_call"]
    ):
        raise RuntimeError(f"Surface IDF freshness/order/state drifted: {identifier}")
    for record in emission["first_object_records"]:
        if record["field_count"] != len(record["ordered_fields"]):
            raise RuntimeError(f"Surface IDF field completeness drifted: {identifier}")
    air = facts["behavior_facts"]["air_boundary_reference"]
    if identifier == EXPECTED_CASE_IDS[0]:
        if air != {
            "authored_construction_name": "Custom Transfer Air Boundary",
            "custom_construction_object_emitted": False,
            "dangling_default_reference": True,
            "emitted_construction_name": "DefaultAirBoundary",
        }:
            raise RuntimeError("Surface custom AirBoundary defect drifted.")
    elif any(value is not None and value is not False for value in air.values()):
        raise RuntimeError(f"Unexpected Surface AirBoundary fact: {identifier}")
    if identifier == EXPECTED_CASE_IDS[2]:
        expected_links = [
            ("Window:Interzone", "Interzone A Window 1", "Interzone B Window 1"),
            ("Window:Interzone", "Interzone A Window 2", "Interzone B Window 2"),
            ("Door:Interzone", "Interzone A Door 1", "Interzone B Door 1"),
            ("Door:Interzone", "Interzone A Door 2", "Interzone B Door 2"),
            ("Window:Interzone", "Interzone B Window 1", "Interzone A Window 1"),
            ("Window:Interzone", "Interzone B Window 2", "Interzone A Window 2"),
            ("Door:Interzone", "Interzone B Door 1", "Interzone A Door 1"),
            ("Door:Interzone", "Interzone B Door 2", "Interzone A Door 2"),
        ]
        actual_links = [
            (item["object_type"], item["name"], item["counterpart_name"])
            for item in facts["behavior_facts"]["opening_counterpart_links"]
        ]
        if actual_links != expected_links:
            raise RuntimeError("Surface reciprocal opening links drifted.")
    elif facts["behavior_facts"]["opening_counterpart_links"]:
        raise RuntimeError(f"Unexpected Surface counterpart links: {identifier}")


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
            **EXPECTED_SYMBOL_RECEIPT,
            "path": SHAPE_SOURCE_PATH,
            "symbol": TARGET_SYMBOL,
        }
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
        "adaptations": {TARGET_SYMBOL: ADAPTATION},
        "assertion_ids": {TARGET_SYMBOL: ASSERTION_ID},
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "Python emits legacy area-derived rectangular Window/Door families before "
            "shading children and each host, preserves authored call order, and exposes "
            "the custom-AirBoundary DefaultAirBoundary dangling reference; native binding "
            "requires explicit compatibility context and defect-aware assertions"
        ),
        "classifications": {TARGET_SYMBOL: "exception"},
        "closure": {
            "context_only_not_targeted": [
                "Surface",
                "Surface.__init__",
                "SurfaceBoundaryCondition",
                "SurfaceType",
                "Vertex",
                "Vertex.__init__",
                "Window",
                "Window.__init__",
                "Window.to_idf_object",
                "Door",
                "Door.__init__",
                "Door.to_idf_object",
                "Blind",
                "Blind.__init__",
                "Blind.to_idf_object",
                "Shade",
                "Shade.__init__",
                "Shade.to_idf_object",
                "Construction",
                "Construction.__init__",
                "AirBoundary",
                "AirBoundary.__init__",
                "Glazing",
                "Glazing.__init__",
                "NoMassConstruction",
                "NoMassConstruction.__init__",
                "Zone.name",
                "IdfObject",
                "IdfObject.__init__",
            ],
            "dependency_only_not_closed": {
                TARGET_SYMBOL: [
                    "boundary-recursion-and-reciprocal-surface-identity",
                    "Blind-and-Shade-child-emissions-observed-in-parent-context",
                    "IdfObject-default-field-expansion-and-extensible-vertex-order",
                    "zone-name-only-parent-context",
                ]
            },
            "full_symbol_closure": False,
            "scope": "five-common-valid-state-surface-idf-emission-branches",
            "unresolved_behavior": [
                "Surface-constructor-properties-and-geometry-operations",
                "Window-Door-Blind-Shade-standalone-converter-closure",
                "invalid-domain-nonfinite-and-error-semantics",
                "mutable-default-opening-list-alias-behavior",
                "IdfObject-class-validation-mutation-and-standalone-policy",
                "EnergyModel-parent-order-deduplication-and-conflict-policy",
                "native-default-detailed-fenestration-route",
            ],
        },
        "identity_encoding": "booleans-only-no-id-or-address",
        "native_targets": {TARGET_SYMBOL: NATIVE_TARGET},
        "raw_field_encoding": "complete-ordered-IDD-fields-with-typed-values",
        "runtime_signatures": {
            TARGET_SYMBOL: "(self, zone: 'Zone') -> 'IdfObject'"
        },
        "source_import_policy": (
            "external-temporary-copy-with-complete-loaded-local-module-audit"
        ),
        "target_symbols": [TARGET_SYMBOL],
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


def _runtime_signatures(modules: Any) -> dict[str, str]:
    return {
        TARGET_SYMBOL: str(inspect.signature(modules.shape.Surface.to_idf_object))
    }


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
        raise SystemExit("The aggregate Surface IDF inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        construction = importlib.import_module("idragon.dragon.construction")
        modules.construction = construction
        expected_signatures = _expected_consumer_contract()["runtime_signatures"]
        if _runtime_signatures(modules) != expected_signatures:
            raise SystemExit("Pinned Surface IDF runtime signature drifted.")
        if (
            modules.shape.Construction is not modules.construction.Construction
            or modules.shape.Glazing is not modules.construction.Glazing
            or modules.shape.NoMassConstruction
            is not modules.construction.NoMassConstruction
            or modules.shape.IdfObject is not modules.imugi.IdfObject
        ):
            raise SystemExit("Pinned Surface IDF import identities drifted.")
        observed = {
            definition["id"]: _execute_case(definition["id"], modules)
            for definition in case_definitions()
        }
        observed_hashes = {
            identifier: canonical_sha256(facts)
            for identifier, facts in observed.items()
        }
        if observed_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned Surface IDF per-case facts drifted.\nOBSERVED_HASHES\n"
                + strict_json_dumps(observed_hashes, indent=2)
            )
        cases = []
        for definition in case_definitions():
            facts = observed[definition["id"]]
            _validate_case_facts(definition["id"], facts)
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
                        "source_sha256": sha256_file(
                            _source_file(imported_root, source)
                        ),
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


def _validate_encoded_scalar(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind == "none":
        _require_keys(value, {"kind"}, location)
        return True
    if kind == "bool":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], bool):
            raise RuntimeError(f"Invalid encoded bool at {location}.")
        return True
    if kind == "int":
        _require_keys(value, {"kind", "value"}, location)
        try:
            if str(int(value["value"])) != value["value"]:
                raise ValueError
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid encoded int at {location}.") from error
        return True
    if kind == "str":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], str):
            raise RuntimeError(f"Invalid encoded string at {location}.")
        return True
    if kind == "float":
        _require_keys(value, {"hex", "kind", "repr"}, location)
        try:
            decoded = float.fromhex(value["hex"])
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid encoded float at {location}.") from error
        if (
            not math.isfinite(decoded)
            or decoded.hex() != value["hex"]
            or repr(decoded) != value["repr"]
        ):
            raise RuntimeError(f"Unsafe or nonfinite encoded float at {location}.")
        return True
    return False


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if isinstance(value, float):
        if not math.isfinite(value):
            raise RuntimeError(f"Nonfinite raw float is forbidden at {location}.")
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
        if "kind" in value and _validate_encoded_scalar(value, location):
            for item in value.values():
                if isinstance(item, str):
                    _validate_safe_tree(item, f"{location}.encoded")
            return
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
        raise RuntimeError("Surface IDF schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Surface IDF cases hash drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Surface IDF case order/count drifted.")
    if (
        tuple(sorted(EXPECTED_CASE_IDS)) != EXPECTED_CASE_IDS
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
        or Counter(item["symbol"] for item in definitions)
        != Counter(EXPECTED_CASE_COUNTS)
    ):
        raise RuntimeError("Pinned Surface IDF case matrix drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Surface IDF case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned":
            raise RuntimeError(f"Surface IDF outcome drifted: {case['id']}")
        _validate_case_facts(case["id"], case["python"]["facts"])

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Surface IDF consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Surface IDF runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Surface IDF upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Surface IDF symbol receipt drifted.")
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
    print(f"Wrote dragon Surface IDF oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

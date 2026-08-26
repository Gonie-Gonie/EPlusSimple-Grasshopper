"""Generate bounded observations for the three legacy Zone IDF emitters.

The corpus targets ``Zone.to_idf_hvac_default_object``,
``Zone.to_idf_load_object``, and ``Zone.to_idf_object`` only.  Real pinned
Schedule/Profile/IdfObject dependencies are used for the two child emitters.
Deterministic trace surfaces and child-method doubles isolate the parent
``to_idf_object`` orchestration without claiming Surface or child-converter
closure.
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
from typing import Any, Callable


SCHEMA = "goniegonie.python-reference.dragon-shape-zone-to-idf-object.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
SHAPE_SOURCE_PATH = "src/idragon/dragon/shape.py"
EXPECTED_SYMBOL_RECEIPTS = {
    "Zone.to_idf_hvac_default_object": {
        "body_hash": "sha256:9a121aaad9df4bfa6222f747985a1b07749f518b3501154743ef5c32d307940b",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:ff678ec281fe0726c46fd2145ebfb7fe22b56c5772bf1423d83c4877c0287cd9",
    },
    "Zone.to_idf_load_object": {
        "body_hash": "sha256:17d9c0579f4763783672c981efb7fa0d7c979af8ebfe008b70499f81273e5a78",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:d19165f0aa97a1768174def3da3a46c9c11f29567c558ae844d4cac546452f99",
    },
    "Zone.to_idf_object": {
        "body_hash": "sha256:1964153231690634955bd8ae5c39468cd1ecab4f5c2acbff9ded2cb37978369a",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:479f4d74a625e35e97559f208b41c4bde2f00a519b8e6b840718d78fdfd2e096",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
ADAPTATIONS = {
    "Zone.to_idf_hvac_default_object": "model-context-zone-hvac-default-idf-emission",
    "Zone.to_idf_load_object": "model-context-zone-load-idf-emission",
    "Zone.to_idf_object": "model-context-zone-idf-emission",
}
ASSERTION_IDS = {
    "Zone.to_idf_hvac_default_object": "dragon-shape-zone-to-idf-hvac-default-object-ff678ec2",
    "Zone.to_idf_load_object": "dragon-shape-zone-to-idf-load-object-d19165f0",
    "Zone.to_idf_object": "dragon-shape-zone-to-idf-object-479f4d74",
}
NATIVE_TARGETS = {
    symbol: "EnergyModel.ToIdfDocument" for symbol in TARGET_SYMBOLS
}
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-shape-zone-to-idf-object.hvac-default.conditioned",
        "Zone.to_idf_hvac_default_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.hvac-default.unconditioned-no-availability",
        "Zone.to_idf_hvac_default_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.hvac-default.unconditioned-no-supply",
        "Zone.to_idf_hvac_default_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.load.empty",
        "Zone.to_idf_load_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.load.erv-occupant",
        "Zone.to_idf_load_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.load.full-natural",
        "Zone.to_idf_load_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.parent.empty",
        "Zone.to_idf_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.parent.multiple-surfaces",
        "Zone.to_idf_object",
    ),
    (
        "dragon-shape-zone-to-idf-object.parent.output-and-call-order",
        "Zone.to_idf_object",
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 9
EXPECTED_CASE_COUNTS = {symbol: 3 for symbol in TARGET_SYMBOLS}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_goniegonie_zone_idf_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load Zone IDF support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("Zone IDF support is not exactly pinned.")
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
            "executor": "zone-to-idf-object",
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
    raise TypeError(f"Unsupported oracle scalar: {type(value).__name__}")


def _field(name: str, value: Any) -> dict[str, Any]:
    return {"name": name, "value": _encode(value)}


def _ordered_fields(value: Any) -> list[dict[str, Any]]:
    return [_field(name, field_value) for name, field_value in value.data.items()]


class _TraceToken:
    def __init__(self, label: str) -> None:
        self.label = label


class _TraceSurface:
    def __init__(
        self,
        label: str,
        surface_type: Any,
        area: float,
        emissions: tuple[str, ...],
        trace: list[str],
    ) -> None:
        self.label = label
        self.name = label
        self.type = surface_type
        self.area = area
        self.emissions = emissions
        self._trace = trace

    def to_idf_object(self, zone: Any) -> list[_TraceToken]:
        self._trace.append(f"surface:{self.label}:zone:{zone.name}")
        return [_TraceToken(label) for label in self.emissions]


def _output_item(value: Any) -> dict[str, Any]:
    if isinstance(value, _TraceToken):
        return {"kind": "trace-token", "label": value.label}
    return {
        "kind": "idf-object",
        "object_type": value.idd.name,
        "ordered_fields": _ordered_fields(value),
    }


def _output(value: list[Any]) -> list[dict[str, Any]]:
    return [_output_item(item) for item in value]


PROFILE_SCHEDULE_NAMES = (
    "heating_setpoint",
    "cooling_setpoint",
    "hvac_availability",
    "occupant",
    "lighting",
    "equipment",
    "hotwater",
)


def _schedule_context(value: Any, schedule_class: type[Any]) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if not isinstance(value, schedule_class):
        return {"kind": "dependency-double", "type": type(value).__name__}
    return {
        "day_count": len(value),
        "kind": "pinned-schedule",
        "maximum": _encode(value.max),
        "minimum": _encode(value.min),
        "name": value.name,
        "schedule_type": value.type.value,
    }


def _surface_context(value: Any) -> dict[str, Any]:
    if isinstance(value, _TraceSurface):
        return {
            "area": _encode(value.area),
            "emissions": list(value.emissions),
            "kind": "instrumented-surface-dependency",
            "label": value.label,
            "surface_type": value.type.value,
        }
    return {
        "kind": "pinned-surface",
        "name": value.name,
        "surface_type": value.type.value,
    }


def _zone_context(zone: Any, profile_module: Any) -> dict[str, Any]:
    profile = zone.profile
    supply = zone.supply
    ventilation = zone.ventilation
    return {
        "infiltration": _encode(zone.infiltration),
        "light_density": _encode(zone.light_density),
        "name": zone.name,
        "profile": {
            "name": profile.name,
            "schedules": {
                name: _schedule_context(getattr(profile, name), profile_module.Schedule)
                for name in PROFILE_SCHEDULE_NAMES
            },
        },
        "supply": (
            {"kind": "none"}
            if supply is None
            else {
                "availabilities": [
                    _schedule_context(item, profile_module.Schedule)
                    for item in supply.availabilities
                ],
                "kind": "pinned-supply-group",
                "systems": [
                    {"name": system.name, "type": type(system).__name__}
                    for system in supply.systems
                ],
            }
        ),
        "surfaces": [_surface_context(surface) for surface in zone.surface],
        "ventilation": (
            {"kind": "none"}
            if ventilation is None
            else {
                "cooling_efficiency": _encode(ventilation.cooling_efficiency),
                "heating_efficiency": _encode(ventilation.heating_efficiency),
                "kind": "pinned-energy-recovery-ventilator",
                "name": ventilation.name,
            }
        ),
    }


def _identity_snapshot(zone: Any) -> dict[str, Any]:
    return {
        "profile": zone.profile,
        "schedule_objects": tuple(
            getattr(zone.profile, name) for name in PROFILE_SCHEDULE_NAMES
        ),
        "supply": zone.supply,
        "surface_collection": zone.surface,
        "surface_objects": tuple(zone.surface),
        "ventilation": zone.ventilation,
    }


def _input_integrity(
    zone: Any,
    identity: dict[str, Any],
    before: dict[str, Any],
    profile_module: Any,
) -> dict[str, bool]:
    return {
        "profile_identity_preserved": zone.profile is identity["profile"],
        "schedule_identities_preserved": all(
            getattr(zone.profile, name) is original
            for name, original in zip(
                PROFILE_SCHEDULE_NAMES, identity["schedule_objects"]
            )
        ),
        "state_unchanged_after_two_calls": before
        == _zone_context(zone, profile_module),
        "supply_identity_preserved": zone.supply is identity["supply"],
        "surface_collection_identity_preserved": zone.surface
        is identity["surface_collection"],
        "surface_identities_preserved": all(
            current is original
            for current, original in zip(
                zone.surface, identity["surface_objects"], strict=True
            )
        ),
        "ventilation_identity_preserved": zone.ventilation
        is identity["ventilation"],
    }


def _observe_twice(
    zone: Any,
    method: Callable[[], list[Any]],
    profile_module: Any,
) -> dict[str, Any]:
    before = _zone_context(zone, profile_module)
    identity = _identity_snapshot(zone)
    first = method()
    second = method()
    first_output = _output(first)
    second_output = _output(second)
    return {
        "emission": {
            "all_output_items_fresh": len(first) == len(second)
            and all(left is not right for left, right in zip(first, second)),
            "first_output": first_output,
            "fresh_result_list": first is not second,
            "object_count": len(first),
            "object_family_order": [
                (
                    f"trace:{item.label}"
                    if isinstance(item, _TraceToken)
                    else item.idd.name
                )
                for item in first
            ],
            "result_type": type(first).__name__,
            "same_idd_definition_for_idf_objects": all(
                left.idd is right.idd
                for left, right in zip(first, second)
                if not isinstance(left, _TraceToken)
                and not isinstance(right, _TraceToken)
            ),
            "second_output_equal": first_output == second_output,
        },
        "input_context": before,
        "input_integrity": _input_integrity(
            zone, identity, before, profile_module
        ),
        "invocation": {"args": [], "kwargs": {}},
    }


def _constant_schedule(profile: Any, name: str, value: float, kind: Any) -> Any:
    return profile.Schedule.from_constant(name, value, type=kind)


def _hvac_zone(
    modules: Any,
    name: str,
    *,
    with_supply: bool,
    with_availability: bool,
) -> Any:
    profile_module = modules.profile
    profile = profile_module.Profile(
        "HVAC Profile",
        _constant_schedule(
            profile_module,
            "Heat Schedule",
            20.0,
            profile_module.ScheduleType.TEMPERATURE,
        ),
        _constant_schedule(
            profile_module,
            "Cool Schedule",
            26.0,
            profile_module.ScheduleType.TEMPERATURE,
        ),
        (
            _constant_schedule(
                profile_module,
                "Availability",
                1.0,
                profile_module.ScheduleType.ONOFF,
            )
            if with_availability
            else None
        ),
    )
    supply = (
        modules.hvac.SupplyGroup(
            [modules.hvac.ElectricRadiator("Panel", 2500.0)]
        )
        if with_supply
        else None
    )
    return modules.shape.Zone(
        name,
        [],
        profile,
        0.0,
        7.5,
        supply,
        None,
    )


def _load_zone(modules: Any, variant: str) -> Any:
    profile_module = modules.profile
    if variant == "empty":
        profile = profile_module.Profile("Empty Profile")
        return modules.shape.Zone(
            "Empty Load Zone", [], profile, 0.0, 0.0, None, None
        )
    if variant == "erv-occupant":
        profile = profile_module.Profile(
            "ERV Profile",
            occupant=_constant_schedule(
                profile_module,
                "Dense Occupants",
                0.2,
                profile_module.ScheduleType.REAL,
            ),
        )
        ventilation = modules.hvac.EnergyRecoveryVentilator(
            "Balanced ERV", 0.6, 0.8
        )
        return modules.shape.Zone(
            "ERV Zone", [], profile, 0.0, 0.0, None, ventilation
        )
    if variant == "full-natural":
        profile = profile_module.Profile(
            "Full Load Profile",
            occupant=_constant_schedule(
                profile_module,
                "Occupant Schedule",
                0.125,
                profile_module.ScheduleType.REAL,
            ),
            lighting=_constant_schedule(
                profile_module,
                "Lighting Schedule",
                0.75,
                profile_module.ScheduleType.FRACTION,
            ),
            equipment=_constant_schedule(
                profile_module,
                "Equipment Schedule",
                12.5,
                profile_module.ScheduleType.REAL,
            ),
        )
        return modules.shape.Zone(
            "Full Load Zone", [], profile, 0.35, 8.75, None, None
        )
    raise RuntimeError(f"Unknown load-zone variant: {variant}")


def _parent_zone(
    modules: Any,
    name: str,
    surface_specs: tuple[tuple[str, Any, float, tuple[str, ...]], ...],
    hvac_labels: tuple[str, ...],
    load_labels: tuple[str, ...],
) -> tuple[Any, list[str]]:
    trace: list[str] = []
    surfaces = [
        _TraceSurface(label, surface_type, area, emissions, trace)
        for label, surface_type, area, emissions in surface_specs
    ]
    zone = modules.shape.Zone(
        name,
        surfaces,
        modules.profile.Profile(f"{name} Profile"),
        0.0,
        0.0,
        None,
        None,
    )

    def load_child() -> list[_TraceToken]:
        trace.append("load")
        return [_TraceToken(label) for label in load_labels]

    def hvac_child() -> list[_TraceToken]:
        trace.append("hvac-default")
        return [_TraceToken(label) for label in hvac_labels]

    zone.to_idf_load_object = load_child
    zone.to_idf_hvac_default_object = hvac_child
    return zone, trace


def _observe_parent_twice(
    modules: Any,
    zone: Any,
    trace: list[str],
) -> dict[str, Any]:
    before = _zone_context(zone, modules.profile)
    identity = _identity_snapshot(zone)
    first = modules.shape.Zone.to_idf_object(zone)
    first_trace = list(trace)
    trace.clear()
    second = modules.shape.Zone.to_idf_object(zone)
    second_trace = list(trace)
    first_output = _output(first)
    second_output = _output(second)
    return {
        "child_call_trace_first": first_trace,
        "child_call_trace_second": second_trace,
        "dependency_isolation": {
            "hvac_default_converter": "instrumented-instance-method-double",
            "load_converter": "instrumented-instance-method-double",
            "surface_converter": "instrumented-surface-trace-double",
        },
        "emission": {
            "all_output_items_fresh": len(first) == len(second)
            and all(left is not right for left, right in zip(first, second)),
            "first_output": first_output,
            "fresh_result_list": first is not second,
            "object_count": len(first),
            "object_family_order": [
                (
                    f"trace:{item.label}"
                    if isinstance(item, _TraceToken)
                    else item.idd.name
                )
                for item in first
            ],
            "result_type": type(first).__name__,
            "same_idd_definition_for_idf_objects": all(
                left.idd is right.idd
                for left, right in zip(first, second)
                if not isinstance(left, _TraceToken)
                and not isinstance(right, _TraceToken)
            ),
            "second_output_equal": first_output == second_output,
        },
        "input_context": before,
        "input_integrity": _input_integrity(
            zone, identity, before, modules.profile
        ),
        "invocation": {"args": [], "kwargs": {}},
    }


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        zone = _hvac_zone(
            modules, "Conditioned Zone", with_supply=True, with_availability=True
        )
        facts = _observe_twice(
            zone, zone.to_idf_hvac_default_object, modules.profile
        )
        facts["dependency_mode"] = "real-pinned-profile-schedule-and-idfobject"
        return facts
    if identifier == EXPECTED_CASE_IDS[1]:
        zone = _hvac_zone(
            modules,
            "No Availability Zone",
            with_supply=True,
            with_availability=False,
        )
        facts = _observe_twice(
            zone, zone.to_idf_hvac_default_object, modules.profile
        )
        facts["dependency_mode"] = "real-pinned-profile-schedule-and-idfobject"
        return facts
    if identifier == EXPECTED_CASE_IDS[2]:
        zone = _hvac_zone(
            modules, "No Supply Zone", with_supply=False, with_availability=True
        )
        facts = _observe_twice(
            zone, zone.to_idf_hvac_default_object, modules.profile
        )
        facts["dependency_mode"] = "real-pinned-profile-schedule-and-idfobject"
        return facts
    if identifier == EXPECTED_CASE_IDS[3]:
        zone = _load_zone(modules, "empty")
        facts = _observe_twice(zone, zone.to_idf_load_object, modules.profile)
        facts["dependency_mode"] = "real-pinned-profile-schedule-and-idfobject"
        return facts
    if identifier == EXPECTED_CASE_IDS[4]:
        zone = _load_zone(modules, "erv-occupant")
        facts = _observe_twice(zone, zone.to_idf_load_object, modules.profile)
        facts["dependency_mode"] = "real-pinned-profile-schedule-and-idfobject"
        return facts
    if identifier == EXPECTED_CASE_IDS[5]:
        zone = _load_zone(modules, "full-natural")
        facts = _observe_twice(zone, zone.to_idf_load_object, modules.profile)
        facts["dependency_mode"] = "real-pinned-profile-schedule-and-idfobject"
        return facts
    if identifier == EXPECTED_CASE_IDS[6]:
        zone, trace = _parent_zone(modules, "Empty Parent Zone", (), (), ())
        return _observe_parent_twice(modules, zone, trace)
    if identifier == EXPECTED_CASE_IDS[7]:
        zone, trace = _parent_zone(
            modules,
            "Multiple Surface Zone",
            (
                (
                    "Floor-A",
                    modules.shape.SurfaceType.FLOOR,
                    25.0,
                    ("surface:Floor-A:object-1",),
                ),
                (
                    "Wall-B",
                    modules.shape.SurfaceType.WALL,
                    30.0,
                    ("surface:Wall-B:object-1",),
                ),
            ),
            ("hvac:object-1",),
            ("load:object-1",),
        )
        return _observe_parent_twice(modules, zone, trace)
    if identifier == EXPECTED_CASE_IDS[8]:
        zone, trace = _parent_zone(
            modules,
            "Ordered Parent Zone",
            (
                (
                    "Floor-First",
                    modules.shape.SurfaceType.FLOOR,
                    12.5,
                    (
                        "surface:Floor-First:object-1",
                        "surface:Floor-First:object-2",
                    ),
                ),
                (
                    "Floor-Empty",
                    modules.shape.SurfaceType.FLOOR,
                    7.5,
                    (),
                ),
                (
                    "Ceiling-Last",
                    modules.shape.SurfaceType.CEILING,
                    90.0,
                    ("surface:Ceiling-Last:object-1",),
                ),
            ),
            ("hvac:object-1", "hvac:object-2"),
            ("load:object-1", "load:object-2"),
        )
        return _observe_parent_twice(modules, zone, trace)
    raise RuntimeError(f"Unknown Zone IDF case: {identifier}")


def _idf(object_type: str, fields: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "kind": "idf-object",
        "object_type": object_type,
        "ordered_fields": fields,
    }


def _trace(label: str) -> dict[str, Any]:
    return {"kind": "trace-token", "label": label}


def _expected_emission(output: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "all_output_items_fresh": True,
        "first_output": output,
        "fresh_result_list": True,
        "object_count": len(output),
        "object_family_order": [
            (
                item["object_type"]
                if item["kind"] == "idf-object"
                else f"trace:{item['label']}"
            )
            for item in output
        ],
        "result_type": "list",
        "same_idd_definition_for_idf_objects": True,
        "second_output_equal": True,
    }


def _expected_integrity() -> dict[str, bool]:
    return {
        "profile_identity_preserved": True,
        "schedule_identities_preserved": True,
        "state_unchanged_after_two_calls": True,
        "supply_identity_preserved": True,
        "surface_collection_identity_preserved": True,
        "surface_identities_preserved": True,
        "ventilation_identity_preserved": True,
    }


def _expected_schedule(
    name: str, schedule_type: str, minimum: int | float, maximum: int | float
) -> dict[str, Any]:
    return {
        "day_count": 365,
        "kind": "pinned-schedule",
        "maximum": _encode(maximum),
        "minimum": _encode(minimum),
        "name": name,
        "schedule_type": schedule_type,
    }


def _empty_schedule_map() -> dict[str, dict[str, Any]]:
    return {name: {"kind": "none"} for name in PROFILE_SCHEDULE_NAMES}


def _expected_zone_context(
    *,
    name: str,
    profile_name: str,
    schedules: dict[str, dict[str, Any]],
    infiltration: float,
    light_density: float,
    supply: dict[str, Any] | None = None,
    surfaces: list[dict[str, Any]] | None = None,
    ventilation: dict[str, Any] | None = None,
) -> dict[str, Any]:
    return {
        "infiltration": _encode(infiltration),
        "light_density": _encode(light_density),
        "name": name,
        "profile": {"name": profile_name, "schedules": schedules},
        "supply": {"kind": "none"} if supply is None else supply,
        "surfaces": [] if surfaces is None else surfaces,
        "ventilation": {"kind": "none"} if ventilation is None else ventilation,
    }


def _hvac_context(
    name: str, *, with_supply: bool, with_availability: bool
) -> dict[str, Any]:
    schedules = _empty_schedule_map()
    schedules.update(
        {
            "cooling_setpoint": _expected_schedule(
                "Cool Schedule", "temperature", 26.0, 26.0
            ),
            "heating_setpoint": _expected_schedule(
                "Heat Schedule", "temperature", 20.0, 20.0
            ),
            "hvac_availability": (
                _expected_schedule("Availability", "onoff", 1, 1)
                if with_availability
                else {"kind": "none"}
            ),
        }
    )
    supply = (
        {
            "availabilities": [{"kind": "none"}],
            "kind": "pinned-supply-group",
            "systems": [{"name": "Panel", "type": "ElectricRadiator"}],
        }
        if with_supply
        else None
    )
    return _expected_zone_context(
        name=name,
        profile_name="HVAC Profile",
        schedules=schedules,
        infiltration=0.0,
        light_density=7.5,
        supply=supply,
    )


def _load_context(variant: str) -> dict[str, Any]:
    schedules = _empty_schedule_map()
    if variant == "empty":
        return _expected_zone_context(
            name="Empty Load Zone",
            profile_name="Empty Profile",
            schedules=schedules,
            infiltration=0.0,
            light_density=0.0,
        )
    if variant == "erv-occupant":
        schedules["occupant"] = _expected_schedule(
            "Dense Occupants", "real", 0.2, 0.2
        )
        return _expected_zone_context(
            name="ERV Zone",
            profile_name="ERV Profile",
            schedules=schedules,
            infiltration=0.0,
            light_density=0.0,
            ventilation={
                "cooling_efficiency": _encode(0.8),
                "heating_efficiency": _encode(0.6),
                "kind": "pinned-energy-recovery-ventilator",
                "name": "Balanced ERV",
            },
        )
    if variant == "full-natural":
        schedules.update(
            {
                "equipment": _expected_schedule(
                    "Equipment Schedule", "real", 12.5, 12.5
                ),
                "lighting": _expected_schedule(
                    "Lighting Schedule", "fraction", 0.75, 0.75
                ),
                "occupant": _expected_schedule(
                    "Occupant Schedule", "real", 0.125, 0.125
                ),
            }
        )
        return _expected_zone_context(
            name="Full Load Zone",
            profile_name="Full Load Profile",
            schedules=schedules,
            infiltration=0.35,
            light_density=8.75,
        )
    raise RuntimeError(f"Unknown expected load context: {variant}")


def _trace_surface_context(
    label: str,
    surface_type: str,
    area: float,
    emissions: tuple[str, ...],
) -> dict[str, Any]:
    return {
        "area": _encode(area),
        "emissions": list(emissions),
        "kind": "instrumented-surface-dependency",
        "label": label,
        "surface_type": surface_type,
    }


def _parent_context(
    name: str, surfaces: list[dict[str, Any]]
) -> dict[str, Any]:
    return _expected_zone_context(
        name=name,
        profile_name=f"{name} Profile",
        schedules=_empty_schedule_map(),
        infiltration=0.0,
        light_density=0.0,
        surfaces=surfaces,
    )


def _design_specification_outdoor_air_fields(name: str) -> list[dict[str, Any]]:
    return [
        _field("Name", f"DesignSpecificationOutdoorAir_for_{name}"),
        _field("Outdoor Air Method", "Flow/Person"),
        _field("Outdoor Air Flow per Person", 0.00944),
        _field("Outdoor Air Flow per Zone Floor Area", 0.0),
        _field("Outdoor Air Flow per Zone", 0.0),
        _field("Outdoor Air Flow Air Changes per Hour", 0.0),
        _field("Outdoor Air Schedule Name", "ALLON"),
        _field(
            "Proportional Control Minimum Outdoor Air Flow Rate Schedule Name", None
        ),
    ]


def _design_specification_zone_air_distribution_fields(
    name: str,
) -> list[dict[str, Any]]:
    return [
        _field("Name", f"DesignSpecificationZoneAirDistribution_for_{name}"),
        _field("Zone Air Distribution Effectiveness in Cooling Mode", 1.0),
        _field("Zone Air Distribution Effectiveness in Heating Mode", 1.0),
        _field("Zone Air Distribution Effectiveness Schedule Name", None),
        _field("Zone Secondary Recirculation Fraction", 0.0),
        _field("Minimum Zone Ventilation Efficiency", 0.0),
    ]


def _sizing_zone_fields(name: str) -> list[dict[str, Any]]:
    return [
        _field("Zone or ZoneList Name", name),
        _field(
            "Zone Cooling Design Supply Air Temperature Input Method",
            "SupplyAirTemperature",
        ),
        _field("Zone Cooling Design Supply Air Temperature", 14.0),
        _field("Zone Cooling Design Supply Air Temperature Difference", 10.0),
        _field(
            "Zone Heating Design Supply Air Temperature Input Method",
            "SupplyAirTemperature",
        ),
        _field("Zone Heating Design Supply Air Temperature", 50.0),
        _field("Zone Heating Design Supply Air Temperature Difference", 10.0),
        _field("Zone Cooling Design Supply Air Humidity Ratio", 0.009),
        _field("Zone Heating Design Supply Air Humidity Ratio", 0.004),
        _field(
            "Design Specification Outdoor Air Object Name",
            f"DesignSpecificationOutdoorAir_for_{name}",
        ),
        _field("Zone Heating Sizing Factor", 1.25),
        _field("Zone Cooling Sizing Factor", 1.15),
        _field("Cooling Design Air Flow Method", "DesignDay"),
        _field("Cooling Design Air Flow Rate", 0.0),
        _field("Cooling Minimum Air Flow per Zone Floor Area", 0.000762),
        _field("Cooling Minimum Air Flow", 0.0),
        _field("Cooling Minimum Air Flow Fraction", 0.2),
        _field("Heating Design Air Flow Method", "DesignDay"),
        _field("Heating Design Air Flow Rate", 0.0),
        _field("Heating Maximum Air Flow per Zone Floor Area", 0.002032),
        _field("Heating Maximum Air Flow", 0.1415762),
        _field("Heating Maximum Air Flow Fraction", 0.3),
        _field(
            "Design Specification Zone Air Distribution Object Name",
            f"DesignSpecificationZoneAirDistribution_for_{name}",
        ),
        _field("Account for Dedicated Outdoor Air System", "No"),
        _field(
            "Dedicated Outdoor Air System Control Strategy", "NeutralSupplyAir"
        ),
        _field(
            "Dedicated Outdoor Air Low Setpoint Temperature for Design", "autosize"
        ),
        _field(
            "Dedicated Outdoor Air High Setpoint Temperature for Design", "autosize"
        ),
        _field("Zone Load Sizing Method", "Sensible Load Only No Latent Load"),
        _field(
            "Zone Latent Cooling Design Supply Air Humidity Ratio Input Method",
            "HumidityRatioDifference",
        ),
        _field("Zone Dehumidification Design Supply Air Humidity Ratio", None),
        _field(
            "Zone Cooling Design Supply Air Humidity Ratio Difference", 0.005
        ),
        _field(
            "Zone Latent Heating Design Supply Air Humidity Ratio Input Method",
            "HumidityRatioDifference",
        ),
        _field("Zone Humidification Design Supply Air Humidity Ratio", None),
        _field(
            "Zone Humidification Design Supply Air Humidity Ratio Difference", 0.005
        ),
        _field("Zone Humidistat Dehumidification Set Point Schedule Name", None),
        _field("Zone Humidistat Humidification Set Point Schedule Name", None),
        _field("Type of Space Sum to Use", "Coincident"),
    ]


def _equipment_list_fields(name: str) -> list[dict[str, Any]]:
    fields = [
        _field("Name", f"EquipmentList_for_{name}"),
        _field("Load Distribution Scheme", "SequentialLoad"),
    ]
    for index in range(1, 19):
        fields.extend(
            [
                _field(f"Zone Equipment {index} Object Type", None),
                _field(f"Zone Equipment {index} Name", None),
                _field(f"Zone Equipment {index} Cooling Sequence", None),
                _field(
                    f"Zone Equipment {index} Heating or No-Load Sequence", None
                ),
                _field(
                    f"Zone Equipment {index} Sequential Cooling Fraction Schedule Name",
                    None,
                ),
                _field(
                    f"Zone Equipment {index} Sequential Heating Fraction Schedule Name",
                    None,
                ),
            ]
        )
    return fields


def _equipment_connections_fields(name: str) -> list[dict[str, Any]]:
    return [
        _field("Zone Name", name),
        _field("Zone Conditioning Equipment List Name", f"EquipmentList_for_{name}"),
        _field("Zone Air Inlet Node or NodeList Name", None),
        _field("Zone Air Exhaust Node or NodeList Name", None),
        _field("Zone Air Node Name", f"{name} Zone Air Node"),
        _field("Zone Return Air Node or NodeList Name", None),
        _field("Zone Return Air Node 1 Flow Rate Fraction Schedule Name", None),
        _field("Zone Return Air Node 1 Flow Rate Basis Node or NodeList Name", None),
    ]


def _zone_control_thermostat_fields(name: str) -> list[dict[str, Any]]:
    return [
        _field("Name", f"Thermostat_for_{name}"),
        _field("Zone or ZoneList Name", name),
        _field("Control Type Schedule Name", f"ScheduleTypeForThermostat_for_{name}"),
        _field("Control 1 Object Type", "ThermostatSetpoint:DualSetpoint"),
        _field("Control 1 Name", f"DualSetPoint_for_{name}"),
        _field("Control 2 Object Type", None),
        _field("Control 2 Name", None),
        _field("Control 3 Object Type", None),
        _field("Control 3 Name", None),
        _field("Control 4 Object Type", None),
        _field("Control 4 Name", None),
        _field("Temperature Difference Between Cutout And Setpoint", 0.0),
    ]


def _hvac_output(name: str) -> list[dict[str, Any]]:
    return [
        _idf(
            "DesignSpecification:OutdoorAir",
            _design_specification_outdoor_air_fields(name),
        ),
        _idf(
            "DesignSpecification:ZoneAirDistribution",
            _design_specification_zone_air_distribution_fields(name),
        ),
        _idf("Sizing:Zone", _sizing_zone_fields(name)),
        _idf("ZoneHVAC:EquipmentList", _equipment_list_fields(name)),
        _idf("ZoneHVAC:EquipmentConnections", _equipment_connections_fields(name)),
        _idf(
            "Schedule:Constant",
            [
                _field("Name", f"ScheduleTypeForThermostat_for_{name}"),
                _field("Schedule Type Limits Name", None),
                _field("Hourly Value", 4.0),
            ],
        ),
        _idf(
            "ThermostatSetpoint:DualSetpoint",
            [
                _field("Name", f"DualSetPoint_for_{name}"),
                _field(
                    "Heating Setpoint Temperature Schedule Name", "Heat Schedule"
                ),
                _field(
                    "Cooling Setpoint Temperature Schedule Name", "Cool Schedule"
                ),
            ],
        ),
        _idf("ZoneControl:Thermostat", _zone_control_thermostat_fields(name)),
    ]


def _schedule_compact_fields(name: str) -> list[dict[str, Any]]:
    values: dict[int, Any] = {
        1: "Through: 12/31",
        2: "For: Weekdays",
        3: "Until: 24:00",
        4: "1.0",
        5: "For: Weekends",
        6: "Until: 24:00",
        7: "1.0",
        8: "For: AllOtherDays",
        9: "Until: 24:00",
        10: "1.0",
    }
    return [
        _field("Name", name),
        _field("Schedule Type Limits Name", "ScheduleTypeLimits:Real"),
        *[_field(f"Field {index}", values.get(index)) for index in range(1, 151)],
        _field("", None),
    ]


def _lights_fields(name: str, schedule: str, density: float) -> list[dict[str, Any]]:
    return [
        _field("Name", f"light:{name}"),
        _field("Zone or ZoneList or Space or SpaceList Name", name),
        _field("Schedule Name", schedule),
        _field("Design Level Calculation Method", "Watts/Area"),
        _field("Lighting Level", None),
        _field("Watts per Floor Area", density),
        _field("Watts per Person", None),
        _field("Return Air Fraction", 0.0),
        _field("Fraction Radiant", 0.0),
        _field("Fraction Visible", 0.0),
        _field("Fraction Replaceable", 1.0),
        _field("End-Use Subcategory", "General"),
        _field("Return Air Fraction Calculated from Plenum Temperature", "No"),
        _field(
            "Return Air Fraction Function of Plenum Temperature Coefficient 1", 0.0
        ),
        _field(
            "Return Air Fraction Function of Plenum Temperature Coefficient 2", 0.0
        ),
        _field("Return Air Heat Gain Node Name", None),
        _field("Exhaust Air Heat Gain Node Name", None),
    ]


def _electric_equipment_fields(
    name: str, schedule: str, density: float
) -> list[dict[str, Any]]:
    return [
        _field("Name", f"electric_equipment:{name}"),
        _field("Zone or ZoneList or Space or SpaceList Name", name),
        _field("Schedule Name", schedule),
        _field("Design Level Calculation Method", "Watts/Area"),
        _field("Design Level", None),
        _field("Watts per Floor Area", density),
        _field("Watts per Person", None),
        _field("Fraction Latent", 0.0),
        _field("Fraction Radiant", 0.0),
        _field("Fraction Lost", 0.0),
        _field("End-Use Subcategory", "General"),
    ]


def _people_fields(name: str, schedule: str, density: float) -> list[dict[str, Any]]:
    return [
        _field("Name", f"people:{name}"),
        _field("Zone or ZoneList or Space or SpaceList Name", name),
        _field("Number of People Schedule Name", schedule),
        _field("Number of People Calculation Method", "People/Area"),
        _field("Number of People", None),
        _field("People per Floor Area", density),
        _field("Floor Area per Person", None),
        _field("Fraction Radiant", 0.3),
        _field("Sensible Heat Fraction", "autocalculate"),
        _field("Activity Level Schedule Name", "$DEFAULT$PEOPLEACTIVITY"),
        _field("Carbon Dioxide Generation Rate", "3.82E-8"),
        _field("Enable ASHRAE 55 Comfort Warnings", "No"),
        _field("Mean Radiant Temperature Calculation Type", "EnclosureAveraged"),
        _field("Surface Name/Angle Factor List Name", None),
        _field("Work Efficiency Schedule Name", None),
        _field(
            "Clothing Insulation Calculation Method", "ClothingInsulationSchedule"
        ),
        _field("Clothing Insulation Calculation Method Schedule Name", None),
        _field("Clothing Insulation Schedule Name", None),
        _field("Air Velocity Schedule Name", None),
        *[_field(f"Thermal Comfort Model {index} Type", None) for index in range(1, 8)],
        _field("Ankle Level Air Velocity Schedule Name", None),
        _field("Cold Stress Temperature Threshold", 15.56),
        _field("Heat Stress Temperature Threshold", 30.0),
    ]


def _infiltration_fields(name: str, air_changes: float) -> list[dict[str, Any]]:
    return [
        _field("Name", f"{name}:infiltration"),
        _field("Zone or ZoneList or Space or SpaceList Name", name),
        _field("Schedule Name", "ALLON"),
        _field("Design Flow Rate Calculation Method", "AirChanges/Hour"),
        _field("Design Flow Rate", None),
        _field("Flow Rate per Floor Area", None),
        _field("Flow Rate per Exterior Surface Area", None),
        _field("Air Changes per Hour", air_changes),
        _field("Constant Term Coefficient", 1.0),
        _field("Temperature Term Coefficient", 0.0),
        _field("Velocity Term Coefficient", 0.0),
        _field("Velocity Squared Term Coefficient", 0.0),
    ]


def _ventilation_fields(
    name: str,
    flow_per_person: float,
    ventilation_type: str,
    fan_pressure: float,
    fan_efficiency: float,
) -> list[dict[str, Any]]:
    return [
        _field("Name", f"NaturalVentilation:{name}"),
        _field("Zone or ZoneList or Space or SpaceList Name", name),
        _field("Schedule Name", None),
        _field("Design Flow Rate Calculation Method", "Flow/Person"),
        _field("Design Flow Rate", None),
        _field("Flow Rate per Floor Area", None),
        _field("Flow Rate per Person", flow_per_person),
        _field("Air Changes per Hour", None),
        _field("Ventilation Type", ventilation_type),
        _field("Fan Pressure Rise", fan_pressure),
        _field("Fan Total Efficiency", fan_efficiency),
        _field("Constant Term Coefficient", 1.0),
        _field("Temperature Term Coefficient", 0.0),
        _field("Velocity Term Coefficient", 0.0),
        _field("Velocity Squared Term Coefficient", 0.0),
        _field("Minimum Indoor Temperature", "-100"),
        _field("Minimum Indoor Temperature Schedule Name", None),
        _field("Maximum Indoor Temperature", 100.0),
        _field("Maximum Indoor Temperature Schedule Name", None),
        _field("Delta Temperature", "-100"),
        _field("Delta Temperature Schedule Name", None),
        _field("Minimum Outdoor Temperature", "-100"),
        _field("Minimum Outdoor Temperature Schedule Name", None),
        _field("Maximum Outdoor Temperature", 100.0),
        _field("Maximum Outdoor Temperature Schedule Name", None),
        _field("Maximum Wind Speed", 40.0),
    ]


def _erv_load_output() -> list[dict[str, Any]]:
    normalized = "Dense Occupants_normalized:for:ERV Zone:occupant"
    return [
        _idf("Schedule:Compact", _schedule_compact_fields(normalized)),
        _idf("People", _people_fields("ERV Zone", normalized, 0.2)),
        _idf(
            "ZoneVentilation:DesignFlowRate",
            _ventilation_fields(
                "ERV Zone",
                0.0024900000000000005,
                "Exhaust",
                166.66666666666663,
                0.85,
            ),
        ),
    ]


def _full_natural_load_output() -> list[dict[str, Any]]:
    equipment = "Equipment Schedule_normalized:for:Full Load Zone:equipment"
    occupant = "Occupant Schedule_normalized:for:Full Load Zone:occupant"
    return [
        _idf("Lights", _lights_fields("Full Load Zone", "Lighting Schedule", 8.75)),
        _idf("Schedule:Compact", _schedule_compact_fields(equipment)),
        _idf(
            "ElectricEquipment",
            _electric_equipment_fields("Full Load Zone", equipment, 12.5),
        ),
        _idf("Schedule:Compact", _schedule_compact_fields(occupant)),
        _idf("People", _people_fields("Full Load Zone", occupant, 0.125)),
        _idf(
            "ZoneInfiltration:DesignFlowRate",
            _infiltration_fields("Full Load Zone", 0.35),
        ),
        _idf(
            "ZoneVentilation:DesignFlowRate",
            _ventilation_fields("Full Load Zone", 0.0083, "Natural", 0.0, 1.0),
        ),
    ]


def _zone_fields(name: str, floor_area: float) -> list[dict[str, Any]]:
    return [
        _field("Name", name),
        _field("Direction of Relative North", 0.0),
        _field("X Origin", 0.0),
        _field("Y Origin", 0.0),
        _field("Z Origin", 0.0),
        _field("Type", 1.0),
        _field("Multiplier", 1.0),
        _field("Ceiling Height", "autocalculate"),
        _field("Volume", "autocalculate"),
        _field("Floor Area", floor_area),
        _field("Zone Inside Convection Algorithm", "TARP"),
        _field("Zone Outside Convection Algorithm", "TARP"),
        _field("Part of Total Floor Area", "Yes"),
    ]


def _expected_common(
    context: dict[str, Any], output: list[dict[str, Any]]
) -> dict[str, Any]:
    return {
        "dependency_mode": "real-pinned-profile-schedule-and-idfobject",
        "emission": _expected_emission(output),
        "input_context": context,
        "input_integrity": _expected_integrity(),
        "invocation": {"args": [], "kwargs": {}},
    }


def _expected_parent(
    *,
    context: dict[str, Any],
    output: list[dict[str, Any]],
    call_trace: list[str],
) -> dict[str, Any]:
    return {
        "child_call_trace_first": call_trace,
        "child_call_trace_second": call_trace,
        "dependency_isolation": {
            "hvac_default_converter": "instrumented-instance-method-double",
            "load_converter": "instrumented-instance-method-double",
            "surface_converter": "instrumented-surface-trace-double",
        },
        "emission": _expected_emission(output),
        "input_context": context,
        "input_integrity": _expected_integrity(),
        "invocation": {"args": [], "kwargs": {}},
    }


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return _expected_common(
            _hvac_context(
                "Conditioned Zone", with_supply=True, with_availability=True
            ),
            _hvac_output("Conditioned Zone"),
        )
    if identifier == EXPECTED_CASE_IDS[1]:
        return _expected_common(
            _hvac_context(
                "No Availability Zone",
                with_supply=True,
                with_availability=False,
            ),
            [],
        )
    if identifier == EXPECTED_CASE_IDS[2]:
        return _expected_common(
            _hvac_context(
                "No Supply Zone", with_supply=False, with_availability=True
            ),
            [],
        )
    if identifier == EXPECTED_CASE_IDS[3]:
        return _expected_common(_load_context("empty"), [])
    if identifier == EXPECTED_CASE_IDS[4]:
        return _expected_common(_load_context("erv-occupant"), _erv_load_output())
    if identifier == EXPECTED_CASE_IDS[5]:
        return _expected_common(
            _load_context("full-natural"), _full_natural_load_output()
        )
    if identifier == EXPECTED_CASE_IDS[6]:
        return _expected_parent(
            context=_parent_context("Empty Parent Zone", []),
            output=[_idf("Zone", _zone_fields("Empty Parent Zone", 0.0))],
            call_trace=["load", "hvac-default"],
        )
    if identifier == EXPECTED_CASE_IDS[7]:
        floor = ("surface:Floor-A:object-1",)
        wall = ("surface:Wall-B:object-1",)
        return _expected_parent(
            context=_parent_context(
                "Multiple Surface Zone",
                [
                    _trace_surface_context("Floor-A", "floor", 25.0, floor),
                    _trace_surface_context("Wall-B", "wall", 30.0, wall),
                ],
            ),
            output=[
                _idf("Zone", _zone_fields("Multiple Surface Zone", 25.0)),
                _trace(floor[0]),
                _trace(wall[0]),
                _trace("hvac:object-1"),
                _trace("load:object-1"),
            ],
            call_trace=[
                "surface:Floor-A:zone:Multiple Surface Zone",
                "surface:Wall-B:zone:Multiple Surface Zone",
                "load",
                "hvac-default",
            ],
        )
    if identifier == EXPECTED_CASE_IDS[8]:
        first = (
            "surface:Floor-First:object-1",
            "surface:Floor-First:object-2",
        )
        empty: tuple[str, ...] = ()
        ceiling = ("surface:Ceiling-Last:object-1",)
        return _expected_parent(
            context=_parent_context(
                "Ordered Parent Zone",
                [
                    _trace_surface_context(
                        "Floor-First", "floor", 12.5, first
                    ),
                    _trace_surface_context("Floor-Empty", "floor", 7.5, empty),
                    _trace_surface_context(
                        "Ceiling-Last", "ceiling", 90.0, ceiling
                    ),
                ],
            ),
            output=[
                _idf("Zone", _zone_fields("Ordered Parent Zone", 20.0)),
                _trace(first[0]),
                _trace(first[1]),
                _trace(ceiling[0]),
                _trace("hvac:object-1"),
                _trace("hvac:object-2"),
                _trace("load:object-1"),
                _trace("load:object-2"),
            ],
            call_trace=[
                "surface:Floor-First:zone:Ordered Parent Zone",
                "surface:Floor-Empty:zone:Ordered Parent Zone",
                "surface:Ceiling-Last:zone:Ordered Parent Zone",
                "load",
                "hvac-default",
            ],
        )
    raise RuntimeError(f"Unknown expected Zone IDF case: {identifier}")


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
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
        "closure": {
            "context_only_not_targeted": [
                "Zone",
                "Zone.__init__",
                "Zone.supply",
                "Zone.is_conditioned",
                "Zone.floor_surface",
                "Zone.floor_area",
                "Zone.idf_equipmentlistname",
                "Zone.idf_airinletnodelistname",
                "Zone.idf_airexhaustnodelistname",
                "Profile",
                "Profile.__init__",
                "Schedule",
                "Schedule.normalize_by_max",
                "Schedule.to_idf_object",
                "Surface",
                "Surface.to_idf_object",
                "Window",
                "Door",
                "Shading",
                "Blind",
                "Shade",
                "IdfObject",
                "IdfObject.__init__",
            ],
            "dependency_only_not_closed": {
                "Zone.to_idf_hvac_default_object": [
                    "Profile-setpoint-and-availability-members",
                    "IdfObject-default-field-expansion",
                    "Zone.is_conditioned",
                ],
                "Zone.to_idf_load_object": [
                    "Profile-load-schedule-members",
                    "Schedule.normalize_by_max",
                    "Schedule.to_idf_object",
                    "IdfObject-default-field-expansion",
                ],
                "Zone.to_idf_object": [
                    "Zone.floor_area",
                    "Surface.to_idf_object-trace-double-only",
                    "Zone.to_idf_hvac_default_object-trace-double-only",
                    "Zone.to_idf_load_object-trace-double-only",
                ],
            },
            "full_symbol_closure": False,
            "scope": (
                "bounded-common-valid-state-zone-emission-and-parent-orchestration"
            ),
            "unresolved_behavior": [
                "Zone-class-constructor-and-properties",
                "Surface-class-and-Surface.to_idf_object",
                "Window-door-and-shading-emission",
                "Profile-and-Schedule-child-converter-closure",
                "invalid-duck-types-and-exact-error-behavior",
                "IdfObject-class-constructor-validation-and-mutation",
                "native-global-order-deduplication-and-conflict-policy",
                "EnergyModel-parent-assembly",
            ],
        },
        "identity_encoding": "booleans-only-no-id-or-address",
        "native_targets": NATIVE_TARGETS,
        "runtime_signatures": {
            symbol: "(self) -> 'list[IdfObject]'" for symbol in TARGET_SYMBOLS
        },
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


def _runtime_signatures(modules: Any) -> dict[str, str]:
    return {
        symbol: str(inspect.signature(getattr(modules.shape.Zone, symbol.split(".")[1])))
        for symbol in TARGET_SYMBOLS
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
        raise SystemExit("The aggregate Zone IDF inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        expected_signatures = _expected_consumer_contract()["runtime_signatures"]
        if _runtime_signatures(modules) != expected_signatures:
            raise SystemExit("Pinned Zone IDF runtime signatures drifted.")
        cases = []
        for definition in case_definitions():
            facts = _execute_case(definition["id"], modules)
            expected = expected_facts(definition["id"])
            if facts != expected:
                raise SystemExit(
                    "Pinned Python Zone IDF semantics drifted: "
                    + definition["id"]
                    + "\nOBSERVED\n"
                    + strict_json_dumps(facts, indent=2)
                    + "\nEXPECTED\n"
                    + strict_json_dumps(expected, indent=2)
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
        raise RuntimeError("Zone IDF schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Zone IDF cases hash drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Zone IDF case order/count drifted.")
    if (
        list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Pinned Zone IDF case IDs drifted.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Zone IDF per-symbol case counts drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Zone IDF case contract drifted: {case['id']}")
        _require_keys(
            case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet"
        )
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if (
            case["python"]["outcome"] != "returned"
            or case["python"]["facts"] != expected_facts(case["id"])
        ):
            raise RuntimeError(f"Zone IDF semantics drifted: {case['id']}")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Zone IDF consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Zone IDF runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Zone IDF upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Zone IDF symbol receipts drifted.")
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
    print(f"Wrote dragon shape Zone IDF oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

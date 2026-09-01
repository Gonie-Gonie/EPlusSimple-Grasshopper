"""Generate bounded pinned observations for legacy ``EnergyModel.to_idf``.

Five concrete-model cases preserve the exact profile-name, unconditioned
fallback, legacy ERV, and missing-HVAC-availability branches needed by the
current native assembly review.  Five additional probe cases bind only the
parent method's orchestration order and selected failure prefixes; child
converter semantics remain separate inventory symbols.  The ten cases still
deliberately do not close the complete symbol.  Runtime identity is represented
only by logical labels, and raw IDF values use tagged JSON encodings with
trailing ``None`` fields trimmed.
"""

from __future__ import annotations

import argparse
import ast
from collections import Counter
from contextlib import contextmanager
from enum import Enum
import hashlib
import importlib
import importlib.metadata
import importlib.util
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
import tokenize
from types import SimpleNamespace
from typing import Any, Iterator


SCHEMA = "dragons.python-reference.dragon-model-assembly.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
MODEL_SOURCE_PATH = "src/idragon/dragon/model.py"
SOURCE_SPECS = (
    {
        "ast_sha256": "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618",
        "path": "src/idragon/__init__.py",
        "source_sha256": "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9",
        "path": "src/idragon/common.py",
        "source_sha256": "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084",
        "path": "src/idragon/constants.py",
        "source_sha256": "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52",
        "path": "src/idragon/dragon/__init__.py",
        "source_sha256": "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a",
        "path": "src/idragon/dragon/construction.py",
        "source_sha256": "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31",
        "path": "src/idragon/dragon/hvac.py",
        "source_sha256": "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59",
        "path": MODEL_SOURCE_PATH,
        "source_sha256": "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
        "symbols": ("EnergyModel.to_idf",),
    },
    {
        "ast_sha256": "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef",
        "path": "src/idragon/dragon/profile.py",
        "source_sha256": "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2",
        "path": "src/idragon/dragon/shape.py",
        "source_sha256": "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90",
        "path": "src/idragon/imugi.py",
        "source_sha256": "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e",
        "path": "src/idragon/launcher.py",
        "source_sha256": "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452",
        "path": "src/idragon/utils.py",
        "source_sha256": "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd",
        "symbols": (),
    },
)
EXPECTED_SYMBOL_RECEIPTS = {
    "EnergyModel.to_idf": {
        "body_hash": "sha256:9d1b5a610b485aa782c0c1f39ed57b65d5534e1ba3271f1a325c52a109228189",
        "kind": "function",
        "signature_hash": "sha256:9389bd00d5a2180ea9f3cd1aa5695ba492e1665947515c34c31eff01f072bade",
        "symbol_hash": "sha256:de10251f38f220956e870d8faea1c7a879da9158b369cffc244f7afc6519eb35",
    }
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
ASSIGNED_WITHOUT_AVAILABILITY_CASE_ID = (
    "dragon-model-assembly.to-idf.assigned-without-availability-fallback"
)
CASE_DISTINCT_PROFILE_CASE_ID = (
    "dragon-model-assembly.to-idf.case-distinct-profile-schedules"
)
DUPLICATE_PROFILE_CASE_ID = (
    "dragon-model-assembly.to-idf.duplicate-profile-last-wins-dangling"
)
LEGACY_ERV_CASE_ID = "dragon-model-assembly.to-idf.legacy-erv-unconditioned"
ORCHESTRATION_ADD_SUPPLY_FAILURE_CASE_ID = (
    "dragon-model-assembly.to-idf.orchestration-failure.add-supply-prefix"
)
ORCHESTRATION_LAYER_FAILURE_CASE_ID = (
    "dragon-model-assembly.to-idf.orchestration-failure.layer-batch-prefix"
)
ORCHESTRATION_PV_FAILURE_CASE_ID = (
    "dragon-model-assembly.to-idf.orchestration-failure.pv-prefix"
)
ORCHESTRATION_SOURCE_FAILURE_CASE_ID = (
    "dragon-model-assembly.to-idf.orchestration-failure.source-prefix"
)
ORCHESTRATION_SUCCESS_CASE_ID = (
    "dragon-model-assembly.to-idf.orchestration-success.parent-order"
)
TWO_UNCONDITIONED_CASE_ID = (
    "dragon-model-assembly.to-idf.two-unconditioned-shared-fallback"
)

EXPECTED_CASE_BINDINGS = (
    (
        ASSIGNED_WITHOUT_AVAILABILITY_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        CASE_DISTINCT_PROFILE_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        DUPLICATE_PROFILE_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        LEGACY_ERV_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        ORCHESTRATION_ADD_SUPPLY_FAILURE_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        ORCHESTRATION_LAYER_FAILURE_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        ORCHESTRATION_PV_FAILURE_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        ORCHESTRATION_SOURCE_FAILURE_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        ORCHESTRATION_SUCCESS_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
    (
        TWO_UNCONDITIONED_CASE_ID,
        "energy-model-to-idf",
        "EnergyModel.to_idf",
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 10
EXPECTED_CASE_COUNTS = {"EnergyModel.to_idf": 10}
EXPECTED_DEPENDENCIES = {
    "colorama": "0.4.6",
    "et_xmlfile": "2.0.0",
    "numpy": "2.3.1",
    "openpyxl": "3.1.5",
    "pandas": "2.3.0",
    "python-dateutil": "2.9.0.post0",
    "pytz": "2024.2",
    "six": "1.16.0",
    "tqdm": "4.67.1",
    "tzdata": "2024.2",
}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

RAW_ADDRESS_PATTERN = re.compile(
    r"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])"
)
ABSOLUTE_PATH_PATTERN = re.compile(
    r"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))"
)
GUID_PATTERN = re.compile(
    r"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-"
    r"[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])"
)
TIMESTAMP_PATTERN = re.compile(
    r"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d"
)


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_dragon_model_assembly_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load dragon-model assembly support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Dragon-model assembly support is not pinned.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _load_source_inventory(
    path: Path,
    upstream_commit: str,
    source: dict[str, Any],
) -> dict[str, Any]:
    symbols = tuple(source["symbols"])
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(SUPPORT, name) for name in names}
    try:
        SUPPORT.SOURCE_PATH = source["path"]
        SUPPORT.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        SUPPORT.EXPECTED_SYMBOL_HASHES = {
            symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"]
            for symbol in symbols
        }
        SUPPORT.TARGET_SYMBOLS = symbols
        inventory = SUPPORT.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(SUPPORT, name, value)

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
        for symbol in symbols
    ]
    if inventory["file"] != expected_file or inventory["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return inventory


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    inventories = [
        _load_source_inventory(path, upstream_commit, source)
        for source in SOURCE_SPECS
    ]
    if any(
        item["content_sha256"] != EXPECTED_INVENTORY_SHA256
        for item in inventories
    ):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in inventories],
        "symbols": [symbol for item in inventories for symbol in item["symbols"]],
    }


def _case(identifier: str, executor: str, symbol: str) -> dict[str, str]:
    return {"executor": executor, "id": identifier, "symbol": symbol}


def case_definitions() -> tuple[dict[str, str], ...]:
    return tuple(_case(*binding) for binding in EXPECTED_CASE_BINDINGS)


def _string(value: str) -> dict[str, str]:
    return {"kind": "str", "value": value}


def _integer(value: int) -> dict[str, str]:
    return {"kind": "int", "repr": repr(value)}


def _float(value: float) -> dict[str, str]:
    return {"kind": "float", "repr": repr(value)}


def _none() -> dict[str, str]:
    return {"kind": "none"}


def _raw_object(values: list[dict[str, str]], stored: int) -> dict[str, Any]:
    return {"stored_field_count": stored, "values": values}


def _default_compact(name: str, value: int) -> dict[str, Any]:
    return _raw_object(
        [
            _string(name),
            _none(),
            _string("Through: 12/31"),
            _string("For: AllDays"),
            _string("Until: 24:00"),
            _integer(value),
        ],
        153,
    )


def _constant_compact(
    name: str,
    schedule_type: str,
    value: str,
) -> dict[str, Any]:
    values = [_string(name), _string(schedule_type), _string("Through: 12/31")]
    for day_group in ("Weekdays", "Weekends", "AllOtherDays"):
        values.extend(
            (
                _string(f"For: {day_group}"),
                _string("Until: 24:00"),
                _string(value),
            )
        )
    return _raw_object(values, 153)


def _default_object_facts() -> dict[str, Any]:
    return {
        "global_geometry_rules": [
            _raw_object(
                [
                    _string("UpperLeftCorner"),
                    _string("Counterclockwise"),
                    _string("World"),
                    _string("Relative"),
                    _string("Relative"),
                ],
                5,
            )
        ],
        "people_activity_schedule_constants": [
            _raw_object(
                [
                    _string("$DEFAULT$PEOPLEACTIVITY"),
                    {
                        "enum_type": "ScheduleType",
                        "kind": "enum",
                        "text": "real",
                        "value": "real",
                    },
                    _float(107.0),
                ],
                3,
            )
        ],
        "schedule_compact": [
            _default_compact("ALLON", 1),
            _default_compact("ALLOFF", 0),
        ],
        "schedule_type_limits": [
            _raw_object(
                [
                    _string("ScheduleTypeLimits:Temperature"),
                    _integer(-50),
                    _integer(200),
                    _string("Continuous"),
                    _string("Temperature"),
                ],
                5,
            ),
            _raw_object(
                [
                    _string("ScheduleTypeLimits:Onoff"),
                    _integer(0),
                    _integer(1),
                    _string("Discrete"),
                    _string("Dimensionless"),
                ],
                5,
            ),
            _raw_object(
                [
                    _string("ScheduleTypeLimits:Fraction"),
                    _integer(0),
                    _integer(1),
                    _string("Continuous"),
                    _string("Dimensionless"),
                ],
                5,
            ),
            _raw_object(
                [
                    _string("ScheduleTypeLimits:Real"),
                    _none(),
                    _none(),
                    _string("Continuous"),
                    _string("Dimensionless"),
                ],
                5,
            ),
        ],
    }


def _fallback_thermostat() -> dict[str, Any]:
    return _raw_object(
        [_string("UNCONDITIONED_THERMOSTAT"), _none(), _integer(-30), _none(), _integer(50)],
        5,
    )


def _fallback_ideal(zone_name: str) -> dict[str, Any]:
    return _raw_object(
        [
            _string(zone_name),
            _string("UNCONDITIONED_THERMOSTAT"),
            _string("ALLON"),
            _float(50.0),
            _float(13.0),
            _float(0.0156),
            _float(0.0077),
            _string("NoLimit"),
            _none(),
            _none(),
            _string("NoLimit"),
            _none(),
            _none(),
            _none(),
            _none(),
            _string("ConstantSensibleHeatRatio"),
            _float(0.7),
            _float(60.0),
            _string("None"),
            _float(30.0),
            _string("None"),
            _float(0.00944),
            _float(0.0),
            _float(0.0),
            _none(),
            _string("None"),
            _string("NoEconomizer"),
            _string("None"),
            _float(0.7),
            _float(0.65),
        ],
        30,
    )


def _family(count: int, name: str) -> dict[str, Any]:
    return {"count": count, "object_type": name}


BASE_PREFIX = [
    _family(1, "Version"),
    _family(1, "SimulationControl"),
    _family(1, "Building"),
    _family(1, "Timestep"),
    _family(2, "SizingPeriod:WeatherFileDays"),
    _family(1, "RunPeriod"),
    _family(4, "ScheduleTypeLimits"),
]
BASE_MIDDLE = [_family(1, "Schedule:Constant"), _family(1, "GlobalGeometryRules")]
BASE_SUFFIX = [
    _family(1, "Output:Table:SummaryReports"),
    _family(1, "Output:Table:Monthly"),
    _family(1, "OutputControl:Table:Style"),
]


def _family_order(
    schedule_count: int,
    middle: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    return [
        *BASE_PREFIX,
        _family(schedule_count, "Schedule:Compact"),
        *BASE_MIDDLE,
        *middle,
        *BASE_SUFFIX,
    ]


def _light(name: str, zone_name: str, schedule_name: str) -> dict[str, Any]:
    return {
        "name": name,
        "schedule_name": schedule_name,
        "stored_field_count": 17,
        "zone_name": zone_name,
    }


def _ventilation() -> dict[str, Any]:
    return _raw_object(
        [
            _string("NaturalVentilation:ERV-Zone"),
            _string("ERV-Zone"),
            _none(),
            _string("Flow/Person"),
            _none(),
            _none(),
            _float(0.00332),
            _none(),
            _string("Exhaust"),
            _float(125.0),
            _float(0.85),
            _float(1.0),
            _float(0.0),
            _float(0.0),
            _float(0.0),
            _string("-100"),
            _none(),
            _float(100.0),
            _none(),
            _string("-100"),
            _none(),
            _string("-100"),
            _none(),
            _float(100.0),
            _none(),
            _float(40.0),
        ],
        26,
    )


ORCHESTRATION_FAILURES = {
    ORCHESTRATION_ADD_SUPPLY_FAILURE_CASE_ID: "supply-zone-2",
    ORCHESTRATION_LAYER_FAILURE_CASE_ID: "layer-2",
    ORCHESTRATION_PV_FAILURE_CASE_ID: "pv-2",
    ORCHESTRATION_SOURCE_FAILURE_CASE_ID: "source-2",
}


def _trace_event(event: str, **values: Any) -> dict[str, Any]:
    return {"event": event, **values}


def _trace_conversion(
    kind: str,
    label: str,
    *,
    arguments: tuple[str, ...] = (),
    result: str = "returned",
) -> dict[str, Any]:
    return _trace_event(
        "converter.call",
        arguments=list(arguments),
        kind=kind,
        label=label,
        result=result,
    )


def _trace_append(*labels: str) -> dict[str, Any]:
    return _trace_event("idf.append", labels=list(labels))


def _trace_error(label: str) -> dict[str, Any]:
    message = f"orchestration-failure:{label}"
    return {
        "args": [message],
        "message": message,
        "outcome": "raised",
        "type": "RuntimeError",
    }


def _expected_orchestration_events(fail_at: str | None) -> list[dict[str, Any]]:
    events: list[dict[str, Any]] = [
        _trace_event("default.create"),
        _trace_event("idf.family.get", family="building"),
        _trace_event(
            "idf.family.append",
            family="building",
            fields={
                "Name": "trace-model",
                "North Axis": 17,
                "Solar Distribution": "MinimalShadowing",
                "Terrain": "TraceTerrain",
            },
        ),
        _trace_event("projection.read", call=1, projection="used_layers"),
        _trace_conversion("layer", "layer-1"),
    ]
    if fail_at == "layer-2":
        events.append(_trace_conversion("layer", "layer-2", result="raised"))
        return events
    events.extend(
        (
            _trace_conversion("layer", "layer-2"),
            _trace_append("object:layer-1", "object:layer-2"),
            _trace_event("projection.read", call=1, projection="surfaces"),
            _trace_conversion(
                "construction",
                "construction-opaque",
                arguments=("surface-opaque",),
            ),
            _trace_append("object:construction-opaque"),
            _trace_conversion("glazing", "glazing-1"),
            _trace_append("object:glazing-1:material", "object:glazing-1:construction"),
            _trace_conversion("door-construction", "door-construction-1"),
            _trace_append(
                "object:door-construction-1:material",
                "object:door-construction-1:construction",
            ),
            _trace_event("idf.family.get", family="Construction:AirBoundary"),
            _trace_event(
                "idf.family.names",
                family="Construction:AirBoundary",
                names=[],
            ),
            _trace_conversion("air-boundary", "air-boundary-first"),
            _trace_append("object:air-boundary-first"),
            _trace_event("idf.family.get", family="Construction:AirBoundary"),
            _trace_event(
                "idf.family.names",
                family="Construction:AirBoundary",
                names=["Shared-Air-Boundary"],
            ),
            _trace_event("projection.read", call=1, projection="used_profiles"),
            _trace_conversion("profile", "profile-1"),
            _trace_append("object:profile-1"),
            _trace_conversion("profile", "profile-2"),
            _trace_append("object:profile-2"),
            _trace_conversion("zone", "zone-1"),
            _trace_append("object:zone-1"),
            _trace_conversion("zone", "zone-2"),
            _trace_append("object:zone-2"),
            _trace_conversion("zone", "zone-unconditioned"),
            _trace_append("object:zone-unconditioned"),
            _trace_event(
                "projection.read", call=1, projection="conditioned_zones"
            ),
            _trace_event(
                "supply.sources.read",
                sources=["source-shared", "source-2"],
                zone="zone-1",
            ),
            _trace_conversion("source", "source-shared"),
            _trace_append("object:source-shared"),
        )
    )
    if fail_at == "source-2":
        events.append(_trace_conversion("source", "source-2", result="raised"))
        return events
    events.extend(
        (
            _trace_conversion("source", "source-2"),
            _trace_append("object:source-2"),
            _trace_event(
                "supply.sources.read",
                sources=["source-shared"],
                zone="zone-2",
            ),
            _trace_event(
                "projection.read", call=2, projection="conditioned_zones"
            ),
            _trace_event(
                "supply.delegate",
                idf_identity_aligned=True,
                result="returned",
                supply_identity_aligned=True,
                zone="zone-1",
            ),
        )
    )
    if fail_at == "supply-zone-2":
        events.append(
            _trace_event(
                "supply.delegate",
                idf_identity_aligned=True,
                result="raised",
                supply_identity_aligned=True,
                zone="zone-2",
            )
        )
        return events
    events.extend(
        (
            _trace_event(
                "supply.delegate",
                idf_identity_aligned=True,
                result="returned",
                supply_identity_aligned=True,
                zone="zone-2",
            ),
            _trace_event(
                "projection.read", call=1, projection="unconditioned_zones"
            ),
            _trace_event(
                "idf-object.create",
                fields={
                    "Constant Cooling Setpoint": 50,
                    "Constant Heating Setpoint": -30,
                    "Name": "UNCONDITIONED_THERMOSTAT",
                },
                label="fallback-thermostat",
                object_type="HVACTemplate:Thermostat",
            ),
            _trace_append("fallback-thermostat"),
            _trace_event(
                "projection.read", call=2, projection="unconditioned_zones"
            ),
            _trace_event(
                "idf-object.create",
                fields={
                    "System Availability Schedule Name": "ALLON",
                    "Template Thermostat Name": "UNCONDITIONED_THERMOSTAT",
                    "Zone Name": "zone-unconditioned",
                },
                label="fallback-ideal:zone-unconditioned",
                object_type="HVACTemplate:Zone:IdealLoadsAirSystem",
            ),
            _trace_append("fallback-ideal:zone-unconditioned"),
            _trace_conversion("photovoltaic", "pv-1"),
            _trace_append("object:pv-1"),
        )
    )
    if fail_at == "pv-2":
        events.append(_trace_conversion("photovoltaic", "pv-2", result="raised"))
        return events
    events.extend(
        (
            _trace_conversion("photovoltaic", "pv-2"),
            _trace_append("object:pv-2"),
        )
    )
    return events


def _projection_read_counts(events: list[dict[str, Any]]) -> dict[str, int]:
    names = (
        "conditioned_zones",
        "surfaces",
        "unconditioned_zones",
        "used_layers",
        "used_profiles",
    )
    return {
        name: sum(
            event["event"] == "projection.read" and event["projection"] == name
            for event in events
        )
        for name in names
    }


def _expected_orchestration_facts(fail_at: str | None) -> dict[str, Any]:
    events = _expected_orchestration_events(fail_at)
    facts: dict[str, Any] = {
        "append_batches": [
            event["labels"] for event in events if event["event"] == "idf.append"
        ],
        "events": events,
        "model_membership_unchanged": True,
        "projection_read_counts": _projection_read_counts(events),
        "returned_default_idf_identity": fail_at is None,
    }
    if fail_at is not None:
        facts["error"] = _trace_error(fail_at)
    return facts


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == ASSIGNED_WITHOUT_AVAILABILITY_CASE_ID:
        return {
            "absent_object_counts": {
                "DesignSpecification:OutdoorAir": 0,
                "Sizing:Zone": 0,
                "ZoneControl:Thermostat": 0,
                "ZoneHVAC:Baseboard:RadiantConvective:Electric": 0,
                "ZoneHVAC:EquipmentList": 0,
            },
            "assigned_supply_names": ["Assigned-Electric"],
            "conditioned_zone_names": [],
            "default_objects": _default_object_facts(),
            "ensure_validity": False,
            "fallback_ideal_loads": [_fallback_ideal("Assigned-Zone")],
            "fallback_thermostats": [_fallback_thermostat()],
            "nonempty_families": _family_order(
                4,
                [
                    _family(1, "Zone"),
                    _family(1, "HVACTemplate:Thermostat"),
                    _family(1, "HVACTemplate:Zone:IdealLoadsAirSystem"),
                ],
            ),
            "object_count": 23,
            "schedule_compact": [
                _default_compact("ALLON", 1),
                _default_compact("ALLOFF", 0),
                _constant_compact(
                    "Heat-Assigned", "ScheduleTypeLimits:Temperature", "20.0"
                ),
                _constant_compact(
                    "Cool-Assigned", "ScheduleTypeLimits:Temperature", "26.0"
                ),
            ],
            "unconditioned_zone_names": ["Assigned-Zone"],
            "zone_is_conditioned": False,
            "zone_names": ["Assigned-Zone"],
        }
    if identifier == CASE_DISTINCT_PROFILE_CASE_ID:
        return {
            "casefold_schedule_groups": {
                "alloff": ["ALLOFF"],
                "allon": ["ALLON"],
                "caselight": ["CaseLight", "caselight"],
            },
            "ensure_validity": False,
            "fallback_ideal_loads": [
                _fallback_ideal("Case-Zone-1"),
                _fallback_ideal("Case-Zone-2"),
            ],
            "fallback_thermostats": [_fallback_thermostat()],
            "lights": [
                _light("light:Case-Zone-1", "Case-Zone-1", "CaseLight"),
                _light("light:Case-Zone-2", "Case-Zone-2", "caselight"),
            ],
            "nonempty_families": _family_order(
                4,
                [
                    _family(2, "Zone"),
                    _family(2, "Lights"),
                    _family(1, "HVACTemplate:Thermostat"),
                    _family(2, "HVACTemplate:Zone:IdealLoadsAirSystem"),
                ],
            ),
            "object_count": 27,
            "schedule_compact": [
                _default_compact("ALLON", 1),
                _default_compact("ALLOFF", 0),
                _constant_compact(
                    "CaseLight", "ScheduleTypeLimits:Onoff", "1"
                ),
                _constant_compact(
                    "caselight", "ScheduleTypeLimits:Onoff", "0"
                ),
            ],
            "used_profiles": [
                {"lighting_schedule": "CaseLight", "name": "CaseProfile"},
                {"lighting_schedule": "caselight", "name": "caseprofile"},
            ],
            "zone_names": ["Case-Zone-1", "Case-Zone-2"],
        }
    if identifier == DUPLICATE_PROFILE_CASE_ID:
        return {
            "ensure_validity": False,
            "fallback_ideal_loads": [
                _fallback_ideal("Exact-Zone-1"),
                _fallback_ideal("Exact-Zone-2"),
            ],
            "fallback_thermostats": [_fallback_thermostat()],
            "lights": [
                _light("light:Exact-Zone-1", "Exact-Zone-1", "Light-A"),
                _light("light:Exact-Zone-2", "Exact-Zone-2", "Light-B"),
            ],
            "missing_schedule_references": ["Light-A"],
            "nonempty_families": _family_order(
                3,
                [
                    _family(2, "Zone"),
                    _family(2, "Lights"),
                    _family(1, "HVACTemplate:Thermostat"),
                    _family(2, "HVACTemplate:Zone:IdealLoadsAirSystem"),
                ],
            ),
            "object_count": 26,
            "schedule_compact": [
                _default_compact("ALLON", 1),
                _default_compact("ALLOFF", 0),
                _constant_compact("Light-B", "ScheduleTypeLimits:Onoff", "0"),
            ],
            "used_profiles": [
                {"lighting_schedule": "Light-B", "name": "DUPLICATE-PROFILE"}
            ],
            "zone_names": ["Exact-Zone-1", "Exact-Zone-2"],
        }
    if identifier == LEGACY_ERV_CASE_ID:
        return {
            "conditioned_zone_names": [],
            "ensure_validity": False,
            "fallback_ideal_loads": [_fallback_ideal("ERV-Zone")],
            "fallback_thermostats": [_fallback_thermostat()],
            "heat_recovery_nonempty_families": [],
            "nonempty_families": _family_order(
                4,
                [
                    _family(1, "Zone"),
                    _family(1, "People"),
                    _family(1, "ZoneVentilation:DesignFlowRate"),
                    _family(1, "HVACTemplate:Thermostat"),
                    _family(1, "HVACTemplate:Zone:IdealLoadsAirSystem"),
                ],
            ),
            "object_count": 25,
            "people": [
                {
                    "activity_schedule_name": "$DEFAULT$PEOPLEACTIVITY",
                    "name": "people:ERV-Zone",
                    "occupancy_schedule_name": (
                        "Occ-ERV_normalized:for:ERV-Zone:occupant"
                    ),
                    "stored_field_count": 29,
                    "zone_name": "ERV-Zone",
                }
            ],
            "schedule_compact": [
                _default_compact("ALLON", 1),
                _default_compact("ALLOFF", 0),
                _constant_compact("Occ-ERV", "ScheduleTypeLimits:Real", "1.0"),
                _constant_compact(
                    "Occ-ERV_normalized:for:ERV-Zone:occupant",
                    "ScheduleTypeLimits:Real",
                    "1.0",
                ),
            ],
            "unconditioned_zone_names": ["ERV-Zone"],
            "ventilation": [_ventilation()],
            "zone_is_conditioned": False,
            "zone_names": ["ERV-Zone"],
        }
    if identifier in ORCHESTRATION_FAILURES:
        return _expected_orchestration_facts(ORCHESTRATION_FAILURES[identifier])
    if identifier == ORCHESTRATION_SUCCESS_CASE_ID:
        return _expected_orchestration_facts(None)
    if identifier == TWO_UNCONDITIONED_CASE_ID:
        return {
            "allon_object_count": 1,
            "conditioned_zone_names": [],
            "ensure_validity": False,
            "fallback_ideal_loads": [
                _fallback_ideal("Unconditioned-First"),
                _fallback_ideal("Unconditioned-Second"),
            ],
            "fallback_thermostats": [_fallback_thermostat()],
            "nonempty_families": _family_order(
                2,
                [
                    _family(2, "Zone"),
                    _family(1, "HVACTemplate:Thermostat"),
                    _family(2, "HVACTemplate:Zone:IdealLoadsAirSystem"),
                ],
            ),
            "object_count": 23,
            "schedule_compact": [
                _default_compact("ALLON", 1),
                _default_compact("ALLOFF", 0),
            ],
            "unconditioned_zone_names": [
                "Unconditioned-First",
                "Unconditioned-Second",
            ],
            "zone_names": ["Unconditioned-First", "Unconditioned-Second"],
        }
    raise RuntimeError(f"Unknown dragon-model assembly case: {identifier}")


def expected_outcome(identifier: str) -> str:
    return "raised" if identifier in ORCHESTRATION_FAILURES else "returned"


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


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(str(source["path"])).relative_to("src")


def _module_name(source_path: str) -> str:
    relative = Path(source_path).relative_to("src").with_suffix("")
    parts = list(relative.parts)
    if parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def _source_ast_sha256(path: Path, relative: str) -> str:
    with tokenize.open(path) as stream:
        text = stream.read()
    tree = ast.parse(
        text,
        filename=relative,
        mode="exec",
        type_comments=True,
        feature_version=(3, 12),
    )
    dumped = ast.dump(tree, annotate_fields=True, include_attributes=False)
    return "sha256:" + hashlib.sha256(dumped.encode("utf-8")).hexdigest()


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


def _audit_loaded_local_modules(imported_root: Path) -> list[dict[str, str]]:
    imported_root = imported_root.resolve()
    expected_by_path = {source["path"]: source for source in SOURCE_SPECS}
    observed: list[dict[str, str]] = []
    for name, module in sorted(sys.modules.items()):
        module_file = getattr(module, "__file__", None)
        if module_file is None:
            continue
        resolved = Path(module_file).resolve()
        try:
            relative = resolved.relative_to(imported_root)
        except ValueError:
            continue
        if resolved.suffix.lower() != ".py":
            raise SystemExit(f"Loaded local module is not Python source: {name}")
        source_path = "src/" + relative.as_posix()
        source = expected_by_path.get(source_path)
        if source is None:
            raise SystemExit(
                f"Loaded local module lacks an exact receipt: {name} ({source_path})"
            )
        receipt = {
            "ast_sha256": _source_ast_sha256(resolved, source_path),
            "module": name,
            "path": source_path,
            "source_sha256": sha256_file(resolved),
        }
        if receipt != {
            "ast_sha256": source["ast_sha256"],
            "module": _module_name(source_path),
            "path": source_path,
            "source_sha256": source["source_sha256"],
        }:
            raise SystemExit(f"Loaded local module receipt drifted: {name}")
        observed.append(receipt)
    expected = _expected_loaded_local_modules()
    if observed != expected:
        raise SystemExit("Loaded local idragon module graph drifted.")
    return observed


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        source_root = Path(entry)
        if all(
            _source_file(source_root, source).is_file()
            and sha256_file(_source_file(source_root, source))
            == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(source_root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


@contextmanager
def _pinned_modules(source_root: Path) -> Iterator[SimpleNamespace]:
    source_root = source_root.resolve()
    if any(
        sha256_file(_source_file(source_root, source)) != source["source_sha256"]
        for source in SOURCE_SPECS
    ):
        raise SystemExit("The selected dragon-model assembly sources are not pinned.")

    saved_modules = {
        name: module
        for name, module in sys.modules.items()
        if name == "idragon" or name.startswith("idragon.")
    }
    with tempfile.TemporaryDirectory(prefix="dragons-idragon-assembly-") as temp:
        imported_root = Path(temp) / "src"
        shutil.copytree(source_root, imported_root)
        if any(
            sha256_file(_source_file(imported_root, source))
            != source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            raise SystemExit("The temporary assembly source copy is not pinned.")

        for name in saved_modules:
            sys.modules.pop(name, None)
        sys.path.insert(0, str(imported_root))
        try:
            common = importlib.import_module("idragon.common")
            constants = importlib.import_module("idragon.constants")
            hvac = importlib.import_module("idragon.dragon.hvac")
            model = importlib.import_module("idragon.dragon.model")
            profile = importlib.import_module("idragon.dragon.profile")
            shape = importlib.import_module("idragon.dragon.shape")
            imugi = importlib.import_module("idragon.imugi")
            utils = importlib.import_module("idragon.utils")
            loaded_local_modules = _audit_loaded_local_modules(imported_root)
            if not (
                model.IDF is imugi.IDF
                and model.IdfObject is imugi.IdfObject
                and model.Setting is common.Setting
                and model.THERMAL is constants.THERMAL
                and model.Profile is profile.Profile
                and model.Zone is shape.Zone
                and model.SupplyGroup is hvac.SupplyGroup
                and profile.IdfObject is imugi.IdfObject
                and shape.IdfObject is imugi.IdfObject
                and hvac.IdfObject is imugi.IdfObject
            ):
                raise SystemExit(
                    "Pinned dragon-model assembly dependencies do not share identity."
                )
            modules = SimpleNamespace(
                common=common,
                constants=constants,
                hvac=hvac,
                imugi=imugi,
                loaded_local_modules=loaded_local_modules,
                model=model,
                profile=profile,
                shape=shape,
                utils=utils,
            )
            yield modules
            modules.loaded_local_modules = _audit_loaded_local_modules(imported_root)
        finally:
            for name in list(sys.modules):
                if name == "idragon" or name.startswith("idragon."):
                    sys.modules.pop(name, None)
            sys.modules.update(saved_modules)
            try:
                sys.path.remove(str(imported_root))
            except ValueError:
                pass


def _encode_field(value: Any) -> dict[str, str]:
    if value is None:
        return _none()
    if isinstance(value, Enum):
        raw_value = value.value
        if not isinstance(raw_value, str):
            raise RuntimeError("Only string-enum IDF fields are expected.")
        return {
            "enum_type": type(value).__name__,
            "kind": "enum",
            "text": str(value),
            "value": raw_value,
        }
    if type(value) is bool:
        return {"kind": "bool", "repr": repr(value)}
    if type(value) is int:
        return _integer(value)
    if type(value) is float:
        return _float(value)
    if type(value) is str:
        return _string(value)
    raise RuntimeError("Unexpected IDF field type: " + type(value).__name__)


def _encoded_object(idf_object: Any) -> dict[str, Any]:
    values = list(idf_object.values())
    stored = len(values)
    while values and values[-1] is None:
        values.pop()
    return {
        "stored_field_count": stored,
        "values": [_encode_field(value) for value in values],
    }


def _observed_default_object_facts(idf: Any) -> dict[str, Any]:
    return {
        "global_geometry_rules": [
            _encoded_object(item) for item in idf["GlobalGeometryRules"]
        ],
        "people_activity_schedule_constants": [
            _encoded_object(item)
            for item in idf["Schedule:Constant"]
            if item["Name"] == "$DEFAULT$PEOPLEACTIVITY"
        ],
        "schedule_compact": [
            _encoded_object(item)
            for item in idf["Schedule:Compact"]
            if item["Name"] in ("ALLON", "ALLOFF")
        ],
        "schedule_type_limits": [
            _encoded_object(item) for item in idf["ScheduleTypeLimits"]
        ],
    }


def _nonempty_families(idf: Any) -> list[dict[str, Any]]:
    return [
        {"count": len(objects), "object_type": object_type}
        for object_type, objects in idf.items()
        if objects
    ]


def _zone(
    modules: SimpleNamespace,
    name: str,
    profile: Any,
    *,
    supply: Any = None,
    ventilation: Any = None,
) -> Any:
    return modules.shape.Zone(name, [], profile, 0, 5, supply, ventilation)


def _lights(idf: Any) -> list[dict[str, Any]]:
    return [
        {
            "name": item["Name"],
            "schedule_name": item["Schedule Name"],
            "stored_field_count": len(list(item.values())),
            "zone_name": item["Zone or ZoneList or Space or SpaceList Name"],
        }
        for item in idf["Lights"]
    ]


def _people(idf: Any) -> list[dict[str, Any]]:
    return [
        {
            "activity_schedule_name": item["Activity Level Schedule Name"],
            "name": item["Name"],
            "occupancy_schedule_name": item["Number of People Schedule Name"],
            "stored_field_count": len(list(item.values())),
            "zone_name": item["Zone or ZoneList or Space or SpaceList Name"],
        }
        for item in idf["People"]
    ]


def _common_facts(idf: Any) -> dict[str, Any]:
    return {
        "ensure_validity": idf.ensure_validity,
        "fallback_ideal_loads": [
            _encoded_object(item)
            for item in idf["HVACTemplate:Zone:IdealLoadsAirSystem"]
        ],
        "fallback_thermostats": [
            _encoded_object(item) for item in idf["HVACTemplate:Thermostat"]
        ],
        "nonempty_families": _nonempty_families(idf),
        "object_count": len(idf),
        "schedule_compact": [
            _encoded_object(item) for item in idf["Schedule:Compact"]
        ],
        "zone_names": list(idf["Zone"].names),
    }


class _TraceToken:
    def __init__(
        self,
        label: str,
        *,
        family: str | None = None,
        name: str | None = None,
    ) -> None:
        self.label = label
        self.family = family
        self.name = name


class _TraceFamily:
    def __init__(
        self,
        owner: "_TraceIdf",
        family: str,
    ) -> None:
        self.owner = owner
        self.family = family
        self._names: list[str] = []

    def append(self, fields: dict[str, Any]) -> None:
        if not isinstance(fields, dict):
            raise RuntimeError("Trace family append expected a field mapping.")
        self.owner.events.append(
            _trace_event(
                "idf.family.append",
                family=self.family,
                fields=dict(fields),
            )
        )

    @property
    def names(self) -> list[str]:
        names = list(self._names)
        self.owner.events.append(
            _trace_event("idf.family.names", family=self.family, names=names)
        )
        return names


class _TraceIdf:
    def __init__(self, events: list[dict[str, Any]]) -> None:
        self.events = events
        self.append_batches: list[list[str]] = []
        self._families: dict[str, _TraceFamily] = {}

    def __getitem__(self, family: str) -> _TraceFamily:
        self.events.append(_trace_event("idf.family.get", family=family))
        if family not in self._families:
            self._families[family] = _TraceFamily(self, family)
        return self._families[family]

    def append(self, *objects: _TraceToken) -> None:
        if any(not isinstance(item, _TraceToken) for item in objects):
            raise RuntimeError("Trace IDF append received a non-token object.")
        labels = [item.label for item in objects]
        self.events.append(_trace_append(*labels))
        self.append_batches.append(labels)
        for item in objects:
            if item.family is not None and item.name is not None:
                family = self._families.setdefault(
                    item.family, _TraceFamily(self, item.family)
                )
                family._names.append(item.name)


class _TraceConverter:
    def __init__(
        self,
        kind: str,
        label: str,
        events: list[dict[str, Any]],
        token_labels: tuple[str, ...],
        fail_at: str | None,
        *,
        return_list: bool = True,
    ) -> None:
        self.kind = kind
        self.label = label
        self.events = events
        self.token_labels = token_labels
        self.fail_at = fail_at
        self.return_list = return_list

    def to_idf_object(self, *arguments: Any) -> Any:
        argument_labels = tuple(
            str(getattr(value, "label", getattr(value, "name", type(value).__name__)))
            for value in arguments
        )
        result = "raised" if self.fail_at == self.label else "returned"
        self.events.append(
            _trace_conversion(
                self.kind,
                self.label,
                arguments=argument_labels,
                result=result,
            )
        )
        if result == "raised":
            raise RuntimeError(f"orchestration-failure:{self.label}")
        tokens = [_TraceToken(label) for label in self.token_labels]
        if self.return_list:
            return tokens
        if len(tokens) != 1:
            raise RuntimeError("A scalar trace converter must return one token.")
        return tokens[0]


class _TraceSupply:
    def __init__(
        self,
        zone_label: str,
        sources: tuple[_TraceConverter, ...],
        events: list[dict[str, Any]],
    ) -> None:
        self.zone_label = zone_label
        self._sources = sources
        self.events = events

    @property
    def sources(self) -> tuple[_TraceConverter, ...]:
        self.events.append(
            _trace_event(
                "supply.sources.read",
                sources=[source.label for source in self._sources],
                zone=self.zone_label,
            )
        )
        return tuple(self._sources)


def _same_identity_sequence(first: list[Any], second: list[Any]) -> bool:
    return len(first) == len(second) and all(
        left is right for left, right in zip(first, second, strict=True)
    )


def _execute_orchestration_case(
    modules: SimpleNamespace,
    fail_at: str | None,
) -> tuple[dict[str, Any], str]:
    events: list[dict[str, Any]] = []
    trace_idf = _TraceIdf(events)

    layer_1 = _TraceConverter(
        "layer",
        "layer-1",
        events,
        ("object:layer-1",),
        fail_at,
        return_list=False,
    )
    layer_2 = _TraceConverter(
        "layer",
        "layer-2",
        events,
        ("object:layer-2",),
        fail_at,
        return_list=False,
    )

    class ProbeConstruction(modules.model.Construction):
        def __init__(self) -> None:
            self.probe = _TraceConverter(
                "construction",
                "construction-opaque",
                events,
                ("object:construction-opaque",),
                fail_at,
            )

        def to_idf_object(self, surface: Any) -> list[_TraceToken]:
            return self.probe.to_idf_object(surface)

    class ProbeAirBoundary(modules.model.AirBoundary):
        def __init__(self, label: str) -> None:
            self.name = "Shared-Air-Boundary"
            self.probe = _TraceConverter(
                "air-boundary",
                label,
                events,
                (f"object:{label}",),
                fail_at,
            )

        def to_idf_object(self) -> list[_TraceToken]:
            tokens = self.probe.to_idf_object()
            for token in tokens:
                token.family = "Construction:AirBoundary"
                token.name = self.name
            return tokens

    glazing = _TraceConverter(
        "glazing",
        "glazing-1",
        events,
        ("object:glazing-1:material", "object:glazing-1:construction"),
        fail_at,
    )
    door_construction = _TraceConverter(
        "door-construction",
        "door-construction-1",
        events,
        (
            "object:door-construction-1:material",
            "object:door-construction-1:construction",
        ),
        fail_at,
    )
    opaque_surface = SimpleNamespace(
        construction=ProbeConstruction(),
        door=[SimpleNamespace(construction=door_construction)],
        label="surface-opaque",
        name="surface-opaque",
        window=[SimpleNamespace(glazing=glazing)],
    )
    first_air_surface = SimpleNamespace(
        construction=ProbeAirBoundary("air-boundary-first"),
        door=[],
        label="surface-air-first",
        name="surface-air-first",
        window=[],
    )
    duplicate_air_surface = SimpleNamespace(
        construction=ProbeAirBoundary("air-boundary-duplicate"),
        door=[],
        label="surface-air-duplicate",
        name="surface-air-duplicate",
        window=[],
    )
    profiles = [
        _TraceConverter("profile", "profile-1", events, ("object:profile-1",), fail_at),
        _TraceConverter("profile", "profile-2", events, ("object:profile-2",), fail_at),
    ]
    shared_source = _TraceConverter(
        "source",
        "source-shared",
        events,
        ("object:source-shared",),
        fail_at,
    )
    second_source = _TraceConverter(
        "source", "source-2", events, ("object:source-2",), fail_at
    )
    zone_1 = _TraceConverter(
        "zone", "zone-1", events, ("object:zone-1",), fail_at
    )
    zone_1.name = zone_1.label
    zone_1.supply = _TraceSupply(
        zone_1.label, (shared_source, second_source), events
    )
    zone_2 = _TraceConverter(
        "zone", "zone-2", events, ("object:zone-2",), fail_at
    )
    zone_2.name = zone_2.label
    zone_2.supply = _TraceSupply(zone_2.label, (shared_source,), events)
    unconditioned_zone = _TraceConverter(
        "zone",
        "zone-unconditioned",
        events,
        ("object:zone-unconditioned",),
        fail_at,
    )
    unconditioned_zone.name = unconditioned_zone.label
    unconditioned_zone.supply = None
    photovoltaic = [
        _TraceConverter(
            "photovoltaic", "pv-1", events, ("object:pv-1",), fail_at
        ),
        _TraceConverter(
            "photovoltaic", "pv-2", events, ("object:pv-2",), fail_at
        ),
    ]

    projection_calls = Counter()

    class ProbeEnergyModel(modules.model.EnergyModel):
        def __init__(self) -> None:
            self.name = "trace-model"
            self.north_axis = 17
            self.terrain = "TraceTerrain"
            self.zone = [zone_1, zone_2, unconditioned_zone]
            self.pv = photovoltaic

        def _projection(self, name: str, values: list[Any]) -> list[Any]:
            projection_calls[name] += 1
            events.append(
                _trace_event(
                    "projection.read",
                    call=projection_calls[name],
                    projection=name,
                )
            )
            return list(values)

        @property
        def used_layers(self) -> list[Any]:
            return self._projection("used_layers", [layer_1, layer_2])

        @property
        def surfaces(self) -> list[Any]:
            return self._projection(
                "surfaces",
                [opaque_surface, first_air_surface, duplicate_air_surface],
            )

        @property
        def used_profiles(self) -> list[Any]:
            return self._projection("used_profiles", profiles)

        @property
        def conditioned_zones(self) -> list[Any]:
            return self._projection("conditioned_zones", [zone_1, zone_2])

        @property
        def unconditioned_zones(self) -> list[Any]:
            return self._projection("unconditioned_zones", [unconditioned_zone])

    probe_model = ProbeEnergyModel()
    original_zones = list(probe_model.zone)
    original_pv = list(probe_model.pv)
    energy_model = modules.model.EnergyModel
    original_create_default = energy_model.__dict__["create_default_idf"]
    original_add_supply = energy_model.__dict__["add_supply_system"]
    original_idf_object = modules.model.IdfObject

    def create_default_idf() -> _TraceIdf:
        events.append(_trace_event("default.create"))
        return trace_idf

    def add_supply_system(idf: Any, zone: Any, supply: Any) -> None:
        result = "raised" if fail_at == f"supply-{zone.label}" else "returned"
        events.append(
            _trace_event(
                "supply.delegate",
                idf_identity_aligned=idf is trace_idf,
                result=result,
                supply_identity_aligned=supply is zone.supply,
                zone=zone.label,
            )
        )
        if result == "raised":
            raise RuntimeError(f"orchestration-failure:supply-{zone.label}")

    def idf_object(object_type: str, fields: dict[str, Any]) -> _TraceToken:
        if object_type == "HVACTemplate:Thermostat":
            label = "fallback-thermostat"
        elif object_type == "HVACTemplate:Zone:IdealLoadsAirSystem":
            label = f"fallback-ideal:{fields['Zone Name']}"
        else:
            raise RuntimeError(f"Unexpected trace IdfObject type: {object_type}")
        events.append(
            _trace_event(
                "idf-object.create",
                fields=dict(fields),
                label=label,
                object_type=object_type,
            )
        )
        return _TraceToken(label, family=object_type, name=label)

    result: Any = None
    error_fact: dict[str, Any] | None = None
    outcome = "returned"
    try:
        setattr(energy_model, "create_default_idf", staticmethod(create_default_idf))
        setattr(energy_model, "add_supply_system", staticmethod(add_supply_system))
        modules.model.IdfObject = idf_object
        result = energy_model.to_idf(probe_model)
    except Exception as error:
        outcome = "raised"
        error_fact = {
            "args": [str(value) for value in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    finally:
        setattr(energy_model, "create_default_idf", original_create_default)
        setattr(energy_model, "add_supply_system", original_add_supply)
        modules.model.IdfObject = original_idf_object

    facts: dict[str, Any] = {
        "append_batches": trace_idf.append_batches,
        "events": events,
        "model_membership_unchanged": _same_identity_sequence(
            probe_model.zone, original_zones
        )
        and _same_identity_sequence(probe_model.pv, original_pv),
        "projection_read_counts": _projection_read_counts(events),
        "returned_default_idf_identity": result is trace_idf,
    }
    if error_fact is not None:
        facts["error"] = error_fact
    return facts, outcome


def _execute_case(
    identifier: str,
    modules: SimpleNamespace,
) -> tuple[dict[str, Any], str]:
    if identifier in ORCHESTRATION_FAILURES:
        return _execute_orchestration_case(
            modules, ORCHESTRATION_FAILURES[identifier]
        )
    if identifier == ORCHESTRATION_SUCCESS_CASE_ID:
        return _execute_orchestration_case(modules, None)

    schedule = modules.profile.Schedule
    schedule_type = modules.profile.ScheduleType
    profile = modules.profile.Profile
    energy_model = modules.model.EnergyModel

    if identifier == ASSIGNED_WITHOUT_AVAILABILITY_CASE_ID:
        heating = schedule.from_constant(
            "Heat-Assigned", 20, type=schedule_type.TEMPERATURE
        )
        cooling = schedule.from_constant(
            "Cool-Assigned", 26, type=schedule_type.TEMPERATURE
        )
        assigned_profile = profile(
            "Assigned-Profile",
            heating_setpoint=heating,
            cooling_setpoint=cooling,
            hvac_availability=None,
        )
        radiator = modules.hvac.ElectricRadiator("Assigned-Electric", 1000)
        zone = _zone(
            modules,
            "Assigned-Zone",
            assigned_profile,
            supply=radiator,
        )
        model = energy_model("assigned-without-availability", zone=[zone])
        idf = model.to_idf()
        facts = _common_facts(idf)
        facts.update(
            {
                "absent_object_counts": {
                    family: len(idf[family])
                    for family in (
                        "DesignSpecification:OutdoorAir",
                        "Sizing:Zone",
                        "ZoneControl:Thermostat",
                        "ZoneHVAC:Baseboard:RadiantConvective:Electric",
                        "ZoneHVAC:EquipmentList",
                    )
                },
                "assigned_supply_names": [
                    item.name for item in zone.supply.systems
                ],
                "conditioned_zone_names": [
                    item.name for item in model.conditioned_zones
                ],
                "default_objects": _observed_default_object_facts(idf),
                "unconditioned_zone_names": [
                    item.name for item in model.unconditioned_zones
                ],
                "zone_is_conditioned": zone.is_conditioned,
            }
        )
        return facts, "returned"

    if identifier == CASE_DISTINCT_PROFILE_CASE_ID:
        upper = schedule.from_constant(
            "CaseLight", 1, type=schedule_type.ONOFF
        )
        lower = schedule.from_constant(
            "caselight", 0, type=schedule_type.ONOFF
        )
        first_profile = profile("CaseProfile", lighting=upper)
        second_profile = profile("caseprofile", lighting=lower)
        model = energy_model(
            "case-distinct-profile-schedules",
            zone=[
                _zone(modules, "Case-Zone-1", first_profile),
                _zone(modules, "Case-Zone-2", second_profile),
            ],
        )
        idf = model.to_idf()
        schedule_names = list(idf["Schedule:Compact"].names)
        facts = _common_facts(idf)
        facts.update(
            {
                "casefold_schedule_groups": {
                    key: [name for name in schedule_names if name.casefold() == key]
                    for key in sorted({name.casefold() for name in schedule_names})
                },
                "lights": _lights(idf),
                "used_profiles": [
                    {
                        "lighting_schedule": item.lighting.name,
                        "name": item.name,
                    }
                    for item in model.used_profiles
                ],
            }
        )
        return facts, "returned"

    if identifier == DUPLICATE_PROFILE_CASE_ID:
        first_schedule = schedule.from_constant(
            "Light-A", 1, type=schedule_type.ONOFF
        )
        second_schedule = schedule.from_constant(
            "Light-B", 0, type=schedule_type.ONOFF
        )
        first_profile = profile("DUPLICATE-PROFILE", lighting=first_schedule)
        second_profile = profile("DUPLICATE-PROFILE", lighting=second_schedule)
        model = energy_model(
            "duplicate-profile-last-wins",
            zone=[
                _zone(modules, "Exact-Zone-1", first_profile),
                _zone(modules, "Exact-Zone-2", second_profile),
            ],
        )
        idf = model.to_idf()
        lights = _lights(idf)
        emitted = set(idf["Schedule:Compact"].names) | set(
            idf["Schedule:Constant"].names
        )
        facts = _common_facts(idf)
        facts.update(
            {
                "lights": lights,
                "missing_schedule_references": sorted(
                    {item["schedule_name"] for item in lights} - emitted
                ),
                "used_profiles": [
                    {
                        "lighting_schedule": item.lighting.name,
                        "name": item.name,
                    }
                    for item in model.used_profiles
                ],
            }
        )
        return facts, "returned"

    if identifier == LEGACY_ERV_CASE_ID:
        occupancy = schedule.from_constant(
            "Occ-ERV", 1.0, type=schedule_type.REAL
        )
        erv = modules.hvac.EnergyRecoveryVentilator("Legacy-ERV", 0.7, 0.5)
        zone = _zone(
            modules,
            "ERV-Zone",
            profile("ERV-Profile", occupant=occupancy),
            ventilation=erv,
        )
        model = energy_model("legacy-erv-unconditioned", zone=[zone])
        idf = model.to_idf()
        facts = _common_facts(idf)
        facts.update(
            {
                "conditioned_zone_names": [
                    item.name for item in model.conditioned_zones
                ],
                "heat_recovery_nonempty_families": [
                    {"count": len(objects), "object_type": object_type}
                    for object_type, objects in idf.items()
                    if objects
                    and (
                        "HeatExchanger" in object_type
                        or "EnergyRecovery" in object_type
                    )
                ],
                "people": _people(idf),
                "unconditioned_zone_names": [
                    item.name for item in model.unconditioned_zones
                ],
                "ventilation": [
                    _encoded_object(item)
                    for item in idf["ZoneVentilation:DesignFlowRate"]
                ],
                "zone_is_conditioned": zone.is_conditioned,
            }
        )
        return facts, "returned"

    if identifier == TWO_UNCONDITIONED_CASE_ID:
        first = _zone(modules, "Unconditioned-First", profile("First-Profile"))
        second = _zone(
            modules, "Unconditioned-Second", profile("Second-Profile")
        )
        model = energy_model("two-unconditioned", zone=[first, second])
        idf = model.to_idf()
        facts = _common_facts(idf)
        facts.update(
            {
                "allon_object_count": list(idf["Schedule:Compact"].names).count(
                    "ALLON"
                ),
                "conditioned_zone_names": [
                    item.name for item in model.conditioned_zones
                ],
                "unconditioned_zone_names": [
                    item.name for item in model.unconditioned_zones
                ],
            }
        )
        return facts, "returned"
    raise RuntimeError(f"Unknown dragon-model assembly case: {identifier}")


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _expected_symbol_descriptors() -> list[dict[str, Any]]:
    return [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": MODEL_SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]


def _expected_files() -> list[dict[str, Any]]:
    return [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
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
        "adaptations": {},
        "assertion_ids": {},
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {"EnergyModel.to_idf": "needs_reverification"},
        "closure": {
            "full_symbol_closure": False,
            "scope": "bounded-behavioral-evidence-only",
            "uncovered_behavior": (
                "remaining-EnergyModel.to_idf-branches-require-reverification"
            ),
        },
        "identity_encoding": "logical-labels-only-no-id-or-address",
        "raw_field_encoding": (
            "typed-kind-plus-value-or-repr-with-trailing-none-trimmed"
        ),
        "source_import_policy": "external-temporary-copy-of-pinned-source",
        "target_symbols": list(TARGET_SYMBOLS),
    }


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


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source_root: Path | None = None,
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
    }:
        raise SystemExit("The aggregate dragon-model assembly inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with _pinned_modules(imported_root) as modules:
        cases: list[dict[str, Any]] = []
        for definition in case_definitions():
            facts, outcome = _execute_case(definition["id"], modules)
            if facts != expected_facts(definition["id"]):
                raise SystemExit(
                    "Pinned Python dragon-model assembly semantics drifted: "
                    + definition["id"]
                    + "\n"
                    + strict_json_dumps(facts, indent=2)
                )
            case = dict(definition)
            case["python"] = {"facts": facts, "outcome": outcome}
            cases.append(case)
    loaded_local_modules = modules.loaded_local_modules

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
            "loaded_local_modules": loaded_local_modules,
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
        raise RuntimeError("Dragon-model assembly schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Dragon-model assembly cases hash drifted.")
    _validate_safe_tree(value)

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Dragon-model assembly case order/count drifted.")
    if [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Dragon-model assembly case order/count drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Pinned dragon-model assembly case IDs are not sorted.")
    if len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Pinned dragon-model assembly case IDs are not unique.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Dragon-model assembly per-symbol case counts drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Dragon-model assembly case contract drifted: {case['id']}")
        if "expected_dotnet" in case:
            raise RuntimeError(
                f"Bounded assembly case cannot claim expected_dotnet: {case['id']}"
            )
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != expected_outcome(case["id"]):
            raise RuntimeError(f"Python case outcome drifted: {case['id']}")
        if case["python"]["facts"] != expected_facts(case["id"]):
            raise RuntimeError(f"Dragon-model assembly semantics drifted: {case['id']}")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Dragon-model assembly consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Dragon-model assembly runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Dragon-model assembly upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Dragon-model assembly symbol receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the assembly oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned source checkout.")
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
    serialized = strict_json_dumps(result, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8", newline="\n")
    print(f"Wrote dragon-model assembly oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

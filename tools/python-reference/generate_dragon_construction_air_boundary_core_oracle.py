"""Generate the pinned Dragon ``AirBoundary`` core oracle.

This deliberately small corpus covers only the public ``AirBoundary`` class
and its constructor.  Representation and IDF methods, plus every later
construction-family symbol, are exact inventory-bound exclusions.  Run the
generator through ``bootstrap_reference.py`` so imports resolve from the
pinned CPython 3.12.7 source and dependency roots.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import sys
from typing import Any, Callable


SCHEMA = "dragons.python-reference.dragon-construction-air-boundary-core.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_INVENTORY_FILE_BYTES = 518_067
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8"
)
SOURCE_PATH = "src/idragon/dragon/construction.py"
EXPECTED_SOURCE_BYTES = 11_652
EXPECTED_SOURCE_SHA256 = (
    "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"
)
EXPECTED_ADJACENT_EXCLUSIONS_SHA256 = (
    "sha256:663f44cc25e1c3914cb534eecc32faa896fcab90e507b4b5a92e1e711d029516"
)
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _receipt(
    index: int,
    symbol: str,
    kind: str,
    symbol_hash: str,
    signature_hash: str,
    body_hash: str,
) -> dict[str, Any]:
    return {
        "body_hash": "sha256:" + body_hash,
        "inventory_index": index,
        "kind": kind,
        "path": SOURCE_PATH,
        "signature_hash": "sha256:" + signature_hash,
        "symbol": symbol,
        "symbol_hash": "sha256:" + symbol_hash,
    }


TARGET_RECEIPTS = (
    _receipt(
        588,
        "AirBoundary",
        "class",
        "fd8f9bb9fcc8a5676f77b8abaffdb0d4fc33ac1d8cdc9e1a6803a6b94e85eb0a",
        "39d8dd0e571aa6335663f4a30f26a7d6bb19bada7423f6c07c35ef3164638afc",
        "bd863bc3e852b36fd85133650c3e35281bab274515287b85d902c6731caac0d4",
    ),
    _receipt(
        589,
        "AirBoundary.__init__",
        "function",
        "a69bf7074e3d95dfd347a13b8e35462ad11f92c5d45db4d58ca4dc3f1d7a026f",
        "ca98c4037f22c953f8768718d1e5c516e8f2e54bef701c6018bdb1b8b476d1df",
        "ef4465f1a137910234a3f54a2a658e0260ca8feea32ff97764c831bea0f84095",
    ),
)

ADJACENT_EXCLUSION_IDENTITIES = (
    (590, "AirBoundary.__repr__"),
    (591, "AirBoundary.__str__"),
    (592, "AirBoundary.to_idf_object"),
    (593, "Construction"),
    (594, "Construction.U"),
    (595, "Construction.__eq__"),
    (596, "Construction.__hash__"),
    (597, "Construction.__init__"),
    (598, "Construction.heat_capacity"),
    (599, "Construction.reversed"),
    (600, "Construction.thickness"),
    (601, "Construction.to_idf_object"),
    (602, "Glazing"),
    (603, "Glazing.G"),
    (604, "Glazing.U"),
    (605, "Glazing.__init__"),
    (606, "Glazing.__repr__"),
    (607, "Glazing.__str__"),
    (608, "Glazing.to_idf_object"),
    (609, "Layer"),
    (610, "Layer.U"),
    (611, "Layer.__eq__"),
    (612, "Layer.__hash__"),
    (613, "Layer.__init__"),
    (614, "Layer.heat_capacity"),
    (615, "Layer.material"),
    (616, "Layer.thickness"),
    (617, "Layer.to_idf_object"),
    (618, "Material"),
    (619, "Material.__eq__"),
    (620, "Material.__init__"),
    (621, "Material.conductivity"),
    (622, "Material.density"),
    (623, "Material.roughness"),
    (624, "Material.solar_absorptance"),
    (625, "Material.specific_heat"),
    (626, "Material.thermal_absorptance"),
    (627, "Material.visible_absorptance"),
    (628, "MaterialRoughness"),
    (629, "MaterialRoughness.MEDIUMROUGH"),
    (630, "MaterialRoughness.MEDIUMSMOOTH"),
    (631, "MaterialRoughness.ROUGH"),
    (632, "MaterialRoughness.SMOOTH"),
    (633, "MaterialRoughness.VERYROUGH"),
    (634, "MaterialRoughness.__str__"),
    (635, "NoMassConstruction"),
    (636, "NoMassConstruction.U"),
    (637, "NoMassConstruction.__init__"),
    (638, "NoMassConstruction.__repr__"),
    (639, "NoMassConstruction.__str__"),
    (640, "NoMassConstruction.to_idf_object"),
)

TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
EXCLUDED_SYMBOLS = tuple(item[1] for item in ADJACENT_EXCLUSION_IDENTITIES)
CLASSIFICATIONS = {symbol: "exception" for symbol in TARGET_SYMBOLS}
ADAPTATIONS = {
    "AirBoundary": "permissive-mutable-python-air-boundary-state-fd8f9bb9",
    "AirBoundary.__init__": "unchecked-python-air-boundary-construction-a69bf707",
}
ASSERTION_IDS = {
    "AirBoundary": "dragon-construction-air-boundary-core-588-fd8f9bb9",
    "AirBoundary.__init__": "dragon-construction-air-boundary-core-589-a69bf707",
}
NATIVE_TARGETS = {
    "AirBoundary": "Dragons.InvisibleDragon.Construction.AirBoundary sealed typed record",
    "AirBoundary.__init__": "AirBoundary(string name, double airChangesPerHour = 0.5) validated constructor",
}
RUNTIME_SIGNATURES = {
    "AirBoundary": "(name: 'str', ACH: 'int | float | None' = 0.5) -> 'None'",
    "AirBoundary.__init__": "(self, name: 'str', ACH: 'int | float | None' = 0.5) -> 'None'",
}

PREFIX = "dragon-construction-air-boundary-core."
CASE_SPECS = (
    ("ab01-default-explicit-and-zero", "AB01"),
    ("ab02-permissive-name-and-ach-domain", "AB02"),
    ("ab03-mutable-aliased-state", "AB03"),
    ("ab04-call-shape-and-error-timing", "AB04"),
)
EXPECTED_CASE_IDS = tuple(PREFIX + item[0] for item in CASE_SPECS)
EXPECTED_CASE_COUNT = 4

# Canonical values emitted by the exact pinned interpreter.
EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:6d669081f161a03e6cb3fbe7cb05b460ad0eb01d7b650ed873cc67c125701a40",
    EXPECTED_CASE_IDS[1]: "sha256:119666fe70177310f1e1c7498c8fce06823137262cef4ca1049360489014269f",
    EXPECTED_CASE_IDS[2]: "sha256:708073e6903d388d2593a07a8ed7ec3b85689c33912be2cc6a1a6bfddd6a7eba",
    EXPECTED_CASE_IDS[3]: "sha256:6e0c48b3b860153cd266814cb31b2c1c0064af6f998d318b7c3efd1e903d9c52",
}
EXPECTED_CASE_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:d69af84241275e45f06154097869a0600425d6839f01a8fbc3ef22682dcb79fd",
    EXPECTED_CASE_IDS[1]: "sha256:801065b4f9b00f2a99d23442fe8130cba241ef14523d76c6f4a6cc535c1005f7",
    EXPECTED_CASE_IDS[2]: "sha256:33b0e8338b7043516fe3259b1a6cb13e71e00792814b681420dea5c523ec823c",
    EXPECTED_CASE_IDS[3]: "sha256:d28af605dfb7d12bda5799822a32df51a837c84054783037ff73d095999cbe8d",
}

UNRESOLVED_BOUNDARIES = (
    "arbitrary-descriptors-proxies-and-conversion-hooks-not-observed",
    "subclass-metaclass-monkeypatch-and-manual-dunder-init-calls-not-observed",
    "decimal-fraction-complex-and-huge-integer-ach-values-not-observed",
    "unicode-whitespace-name-domains-beyond-the-bounded-ascii-cases-not-observed",
    "attribute-deletion-and-arbitrary-added-attributes-not-observed",
    "copy-pickle-serialization-and-reflection-bypass-not-observed",
    "concurrent-source-or-instance-mutation-not-observed",
    "representation-idf-emission-and-parent-construction-integration-not-observed",
)


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_air_boundary_core_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load AirBoundary core support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    construction_receipts = [
        receipt for receipt in module.SOURCE_RECEIPTS if receipt[0] == SOURCE_PATH
    ]
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or construction_receipts
        != [(SOURCE_PATH, EXPECTED_SOURCE_AST_SHA256, EXPECTED_SOURCE_SHA256)]
    ):
        raise RuntimeError("AirBoundary core support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
SOURCE_RECEIPTS = CORE.SOURCE_RECEIPTS
EXPECTED_DEPENDENCIES = CORE.EXPECTED_DEPENDENCIES
strict_json_dumps = CORE.strict_json_dumps
canonical_sha256 = CORE.canonical_sha256
sha256_file = CORE.sha256_file
load_json_without_duplicates = CORE.load_json_without_duplicates
RAW_ADDRESS_PATTERN = CORE.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = CORE.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = CORE.GUID_PATTERN
TIMESTAMP_PATTERN = CORE.TIMESTAMP_PATTERN

SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == SOURCE_PATH else (),
    }
    for path, ast_hash, source_hash in SOURCE_RECEIPTS
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    expected = {item["symbol"]: _descriptor(item) for item in TARGET_RECEIPTS}
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
            symbol: expected[symbol]["symbol_hash"] for symbol in source["symbols"]
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
    expected_symbols = [expected[symbol] for symbol in source["symbols"]]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def _exclusion_reason(index: int) -> str:
    if index in (590, 591):
        return "out-of-scope-representation-not-retargeted"
    if index == 592:
        return "resolved-idf-emission-not-retargeted"
    return "separate-construction-family-symbol-not-targeted"


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    if path.stat().st_size != EXPECTED_INVENTORY_FILE_BYTES:
        raise SystemExit("The public-symbol inventory byte length is not pinned.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash is not pinned.")
    raw = load_json_without_duplicates(path)
    inventories = [
        _load_source_inventory(path, commit, source) for source in SOURCE_SPECS
    ]
    if any(
        item["content_sha256"] != EXPECTED_INVENTORY_SHA256
        for item in inventories
    ):
        raise SystemExit("The public-symbol inventory content hash is not exact.")

    for receipt in TARGET_RECEIPTS:
        observed = {
            **raw["symbols"][receipt["inventory_index"]],
            "inventory_index": receipt["inventory_index"],
        }
        if observed != receipt:
            raise SystemExit(
                f"Exact indexed AirBoundary target drifted: {receipt['symbol']}."
            )

    exclusions: list[dict[str, Any]] = []
    for index, symbol in ADJACENT_EXCLUSION_IDENTITIES:
        observed = raw["symbols"][index]
        if observed["symbol"] != symbol or observed["path"] != SOURCE_PATH:
            raise SystemExit(f"Adjacent construction exclusion drifted at index {index}.")
        exclusions.append(
            {
                **observed,
                "inventory_index": index,
                "reason": _exclusion_reason(index),
            }
        )

    return {
        "adjacent_exclusions": exclusions,
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in inventories],
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": [dict(item) for item in TARGET_RECEIPTS],
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "context_symbols": [],
            "executor": "dragon-construction-air-boundary-core",
            "expected_dotnet": {
                "adaptations": sorted(ADAPTATIONS.values()),
                "classifications": dict(CLASSIFICATIONS),
                "outcome": "adapted-as-pinned",
            },
            "id": PREFIX + slug,
            "scenario": scenario,
            "target_symbols": list(TARGET_SYMBOLS),
        }
        for slug, scenario in CASE_SPECS
    )


def _encode(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if isinstance(value, bool):
        return {"kind": "bool", "value": value}
    if isinstance(value, int):
        return {"kind": "int", "value": str(value)}
    if isinstance(value, float):
        if math.isnan(value):
            return {"kind": "float-nonfinite", "value": "nan"}
        if math.isinf(value):
            return {
                "kind": "float-nonfinite",
                "value": "positive-infinity" if value > 0 else "negative-infinity",
            }
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    if isinstance(value, str):
        return {"kind": "str", "value": value}
    if isinstance(value, list):
        return {"items": [_encode(item) for item in value], "kind": "list"}
    if isinstance(value, dict):
        return {
            "items": [
                {"key": _encode(key), "value": _encode(item)}
                for key, item in value.items()
            ],
            "kind": "dict",
        }
    raise RuntimeError(f"Unsupported AirBoundary fact value: {type(value).__name__}")


def _error(error: Exception) -> dict[str, Any]:
    return {
        "args": [_encode(item) for item in error.args],
        "message": str(error),
        "type": type(error).__name__,
    }


def _event(call: Callable[[], Any], phase: str) -> tuple[dict[str, Any], Any]:
    try:
        value = call()
    except Exception as error:
        return (
            {"error": _error(error), "outcome": "raised", "phase": phase},
            None,
        )
    return (
        {
            "outcome": "returned",
            "phase": phase,
            "return_type": type(value).__name__,
            "returned_none": value is None,
        },
        value,
    )


def _state(value: Any) -> dict[str, Any]:
    return {
        "ACH": _encode(value.ACH),
        "ACH_type": type(value.ACH).__name__,
        "attribute_names": sorted(vars(value)),
        "name": _encode(value.name),
        "name_type": type(value.name).__name__,
        "runtime_type": type(value).__name__,
    }


def _snapshot(
    phase: str,
    value: Any,
    name_source: Any | None = None,
    ach_source: Any | None = None,
) -> dict[str, Any]:
    result = {"object": _state(value), "phase": phase}
    if name_source is not None:
        result["name_source"] = _encode(name_source)
        result["object_name_is_name_source"] = value.name is name_source
    if ach_source is not None:
        result["ach_source"] = _encode(ach_source)
        result["object_ach_is_ach_source"] = value.ACH is ach_source
    return result


def _assign(value: Any, attribute: str, replacement: Any) -> None:
    setattr(value, attribute, replacement)


def _set_item(value: dict[str, Any], key: str, item: Any) -> None:
    value[key] = item


def _ab01(construction: Any) -> dict[str, Any]:
    timeline: list[dict[str, Any]] = []
    objects: list[dict[str, Any]] = []
    values: list[Any] = []
    probes = (
        ("construct-default", lambda: construction.AirBoundary("Default")),
        ("construct-explicit", lambda: construction.AirBoundary("Explicit", 1.25)),
        ("construct-zero", lambda: construction.AirBoundary("Zero", 0)),
    )
    for phase, call in probes:
        event, value = _event(call, phase)
        timeline.append(event)
        if value is not None:
            values.append(value)
            objects.append({"phase": phase, "state": _state(value)})
    return {
        "observations": {"constructed_objects": objects},
        "scenario": "AB01",
        "source_state": {
            "snapshots": [
                {"objects": [_state(value) for value in values], "phase": "after-all"}
            ]
        },
        "timeline": timeline,
    }


def _ab02(construction: Any) -> dict[str, Any]:
    probes = (
        ("null-name", None, 0.5),
        ("blank-name", "", 0.5),
        ("padded-name", "  padded  ", 0.5),
        ("bool-name", True, 0.5),
        ("none-ach", "none-ach", None),
        ("negative-ach", "negative-ach", -1),
        ("nan-ach", "nan-ach", float("nan")),
        ("positive-infinity-ach", "positive-infinity-ach", float("inf")),
        ("negative-infinity-ach", "negative-infinity-ach", float("-inf")),
        ("bool-ach", "bool-ach", True),
        ("string-ach", "string-ach", "1.25"),
    )
    timeline: list[dict[str, Any]] = []
    observations: list[dict[str, Any]] = []
    snapshots: list[dict[str, Any]] = []
    for label, name, ach in probes:
        event, value = _event(
            lambda name=name, ach=ach: construction.AirBoundary(name, ach),
            "construct-" + label,
        )
        timeline.append(event)
        item = {"input": {"ACH": _encode(ach), "name": _encode(name)}, "label": label}
        if value is not None:
            item["state"] = _state(value)
            snapshots.append({"label": label, "state": _state(value)})
        observations.append(item)
    return {
        "observations": {"probes": observations},
        "scenario": "AB02",
        "source_state": {"snapshots": snapshots},
        "timeline": timeline,
    }


def _ab03(construction: Any) -> dict[str, Any]:
    name_source: list[Any] = ["alpha"]
    ach_source: dict[str, Any] = {"rate": 1}
    timeline: list[dict[str, Any]] = []
    event, value = _event(
        lambda: construction.AirBoundary(name_source, ach_source),
        "construct-with-mutable-sources",
    )
    timeline.append(event)
    if value is None:
        raise RuntimeError("AB03 construction unexpectedly failed.")
    snapshots = [_snapshot("initial", value, name_source, ach_source)]

    event, _ = _event(lambda: name_source.append("beta"), "append-name-source")
    timeline.append(event)
    event, _ = _event(lambda: _set_item(ach_source, "rate", 2), "mutate-ach-source")
    timeline.append(event)
    snapshots.append(_snapshot("after-source-mutation", value, name_source, ach_source))

    replacement_name: dict[str, Any] = {"replacement": "name"}
    replacement_ach: list[Any] = [2]
    event, _ = _event(
        lambda: _assign(value, "name", replacement_name), "reassign-object-name"
    )
    timeline.append(event)
    event, _ = _event(
        lambda: _assign(value, "ACH", replacement_ach), "reassign-object-ach"
    )
    timeline.append(event)
    snapshots.append(
        {
            **_snapshot("after-reassignment", value),
            "object_name_is_replacement": value.name is replacement_name,
            "object_ach_is_replacement": value.ACH is replacement_ach,
            "replacement_ach": _encode(replacement_ach),
            "replacement_name": _encode(replacement_name),
        }
    )

    event, _ = _event(lambda: name_source.append("old"), "mutate-old-name-source")
    timeline.append(event)
    event, _ = _event(lambda: _set_item(ach_source, "old", 3), "mutate-old-ach-source")
    timeline.append(event)
    snapshots.append(
        {
            **_snapshot("after-old-source-mutation", value),
            "old_ach_source": _encode(ach_source),
            "old_name_source": _encode(name_source),
        }
    )

    event, _ = _event(
        lambda: _set_item(replacement_name, "replacement", "changed"),
        "mutate-replacement-name",
    )
    timeline.append(event)
    event, _ = _event(lambda: replacement_ach.append(4), "mutate-replacement-ach")
    timeline.append(event)
    snapshots.append(
        {
            **_snapshot("after-replacement-mutation", value),
            "replacement_ach": _encode(replacement_ach),
            "replacement_name": _encode(replacement_name),
        }
    )
    return {
        "observations": {"final_object_state": _state(value)},
        "scenario": "AB03",
        "source_state": {"snapshots": snapshots},
        "timeline": timeline,
    }


def _ab04(construction: Any) -> dict[str, Any]:
    timeline: list[dict[str, Any]] = []
    event, baseline = _event(
        lambda: construction.AirBoundary("baseline"), "construct-baseline"
    )
    timeline.append(event)
    if baseline is None:
        raise RuntimeError("AB04 baseline construction unexpectedly failed.")
    snapshots = [_snapshot("before-call-probes", baseline)]
    probes = (
        ("missing-name", lambda: construction.AirBoundary()),
        (
            "too-many-positional",
            lambda: construction.AirBoundary("extra", 0.5, "third"),
        ),
        (
            "unexpected-lowercase-ach-keyword",
            lambda: construction.AirBoundary(name="lower", ach=0.5),
        ),
        ("name-keyword", lambda: construction.AirBoundary(name="keyword-name")),
        (
            "uppercase-ach-keyword",
            lambda: construction.AirBoundary(name="uppercase", ACH=1.5),
        ),
    )
    successful_states: list[dict[str, Any]] = []
    for phase, call in probes:
        event, value = _event(call, phase)
        timeline.append(event)
        if value is not None:
            successful_states.append({"phase": phase, "state": _state(value)})
    snapshots.append(_snapshot("after-call-probes", baseline))
    return {
        "observations": {"successful_call_states": successful_states},
        "scenario": "AB04",
        "source_state": {"snapshots": snapshots},
        "timeline": timeline,
    }


def _execute_case(identifier: str, construction: Any) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return _ab01(construction)
    if identifier == EXPECTED_CASE_IDS[1]:
        return _ab02(construction)
    if identifier == EXPECTED_CASE_IDS[2]:
        return _ab03(construction)
    if identifier == EXPECTED_CASE_IDS[3]:
        return _ab04(construction)
    raise RuntimeError(f"Unknown AirBoundary core case: {identifier}")


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {item["id"]: canonical_sha256(item) for item in cases}


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


def _coverage_by_symbol() -> dict[str, list[str]]:
    result = {symbol: [] for symbol in TARGET_SYMBOLS}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol].append(definition["id"])
    return result


def _expected_exclusion_contract() -> list[dict[str, Any]]:
    return [
        {
            "inventory_index": index,
            "reason": _exclusion_reason(index),
            "symbol": symbol,
        }
        for index, symbol in ADJACENT_EXCLUSION_IDENTITIES
    ]


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "The pinned Python class accepts unchecked, mutable, identity-retained name "
            "and ACH values and exposes writable public attributes. The native sealed "
            "record accepts a validated string and finite non-negative double, trims its "
            "name, and exposes get-only typed properties."
        ),
        "classification_counts": {"equivalent": 0, "exception": 2},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "adjacent_exclusions": _expected_exclusion_contract(),
            "case_coverage_by_symbol": _coverage_by_symbol(),
            "full_construction_family_closure": False,
            "full_symbol_closure": False,
            "scope": "exact-four-case-two-target-air-boundary-core-matrix",
            "target_coverage_complete": True,
            "target_symbols": list(TARGET_SYMBOLS),
            "unresolved_boundaries": list(UNRESOLVED_BOUNDARIES),
        },
        "evidence_contract": {
            "expected_receipt_count": 2,
            "full_idf_closure": False,
            "structural_only": False,
        },
        "identity_encoding": "stable-direct-is-relations-only-no-id-or-address",
        "native_binding_status": "proposed-not-yet-cross-language-verified",
        "native_targets": NATIVE_TARGETS,
        "raw_fact_encoding": (
            "typed-scalars-recursive-mutable-input-snapshots-and-phase-bound-errors"
        ),
        "runtime_signatures": RUNTIME_SIGNATURES,
        "source_import_policy": (
            "external-temporary-copy-with-complete-loaded-local-module-audit"
        ),
        "target_receipts": [dict(item) for item in TARGET_RECEIPTS],
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


def _expected_upstream(adjacent_exclusions: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "adjacent_exclusions": adjacent_exclusions,
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "construction_source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
        "inventory_file": {
            "bytes": EXPECTED_INVENTORY_FILE_BYTES,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
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


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
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


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    expected_hash = EXPECTED_FACT_SHA256.get(identifier)
    actual_hash = canonical_sha256(facts)
    if expected_hash is not None and actual_hash != expected_hash:
        raise RuntimeError(f"AirBoundary canonical semantics drifted: {identifier}")
    _require_keys(
        facts,
        {"observations", "scenario", "source_state", "timeline"},
        f"facts {identifier}",
    )
    scenario = facts["scenario"]
    if scenario not in {"AB01", "AB02", "AB03", "AB04"}:
        raise RuntimeError(f"AirBoundary scenario drifted: {identifier}")
    timeline = facts["timeline"]
    snapshots = facts["source_state"].get("snapshots")
    if not isinstance(timeline, list) or not timeline or not isinstance(snapshots, list):
        raise RuntimeError(f"AirBoundary fact topology drifted: {identifier}")
    if scenario == "AB01":
        states = [item["state"] for item in facts["observations"]["constructed_objects"]]
        valid = (
            [item["name"]["value"] for item in states]
            == ["Default", "Explicit", "Zero"]
            and [item["ACH_type"] for item in states] == ["float", "float", "int"]
            and [item["ACH"] for item in states]
            == [_encode(0.5), _encode(1.25), _encode(0)]
        )
    elif scenario == "AB02":
        probes = facts["observations"]["probes"]
        valid = (
            [item["label"] for item in probes]
            == [
                "null-name",
                "blank-name",
                "padded-name",
                "bool-name",
                "none-ach",
                "negative-ach",
                "nan-ach",
                "positive-infinity-ach",
                "negative-infinity-ach",
                "bool-ach",
                "string-ach",
            ]
            and all(item["outcome"] == "returned" for item in timeline)
            and all("state" in item for item in probes)
        )
    elif scenario == "AB03":
        valid = (
            [item["phase"] for item in snapshots]
            == [
                "initial",
                "after-source-mutation",
                "after-reassignment",
                "after-old-source-mutation",
                "after-replacement-mutation",
            ]
            and snapshots[0]["object_name_is_name_source"]
            and snapshots[0]["object_ach_is_ach_source"]
            and snapshots[2]["object_name_is_replacement"]
            and snapshots[2]["object_ach_is_replacement"]
            and facts["observations"]["final_object_state"]["name"]["kind"] == "dict"
            and facts["observations"]["final_object_state"]["ACH"]["kind"] == "list"
        )
    else:
        error_events = [item for item in timeline if item["outcome"] == "raised"]
        valid = (
            [item["phase"] for item in error_events]
            == [
                "missing-name",
                "too-many-positional",
                "unexpected-lowercase-ach-keyword",
            ]
            and all(item["error"]["type"] == "TypeError" for item in error_events)
            and len(facts["observations"]["successful_call_states"]) == 2
            and snapshots[0]["object"] == snapshots[1]["object"]
        )
    if not valid:
        raise RuntimeError(f"AirBoundary semantic invariant drifted: {identifier}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    source_file = imported_root / Path(SOURCE_PATH).relative_to("src")
    if source_file.stat().st_size != EXPECTED_SOURCE_BYTES:
        raise SystemExit("Pinned construction.py byte length drifted.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        construction = importlib.import_module("idragon.dragon.construction")
        if Path(construction.__file__).resolve().stat().st_size != EXPECTED_SOURCE_BYTES:
            raise SystemExit("Imported construction module byte length drifted.")
        signatures = {
            "AirBoundary": str(inspect.signature(construction.AirBoundary)),
            "AirBoundary.__init__": str(
                inspect.signature(construction.AirBoundary.__init__)
            ),
        }
        if signatures != RUNTIME_SIGNATURES:
            raise SystemExit("Pinned AirBoundary runtime signatures drifted.")
        observed = {
            definition["id"]: _execute_case(definition["id"], construction)
            for definition in case_definitions()
        }
        fact_hashes = {
            identifier: canonical_sha256(facts)
            for identifier, facts in observed.items()
        }
        if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned AirBoundary per-case facts drifted.\nOBSERVED_FACT_HASHES\n"
                + strict_json_dumps(fact_hashes, indent=2)
            )
        cases: list[dict[str, Any]] = []
        for definition in case_definitions():
            identifier = definition["id"]
            facts = observed[identifier]
            _validate_case_facts(identifier, facts)
            case = dict(definition)
            case["python"] = {
                "facts": facts,
                "facts_sha256": fact_hashes[identifier],
                "outcome": "observed",
            }
            cases.append(case)
        case_hashes = case_sha256(cases)
        if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
            raise SystemExit(
                "Pinned AirBoundary per-case records drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(case_hashes, indent=2)
            )
        result = {
            "case_sha256": case_hashes,
            "cases": cases,
            "cases_sha256": cases_sha256(cases),
            "consumer_contract": _expected_consumer_contract(),
            "fact_sha256": fact_hashes,
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
            "target_receipts": inventory["target_receipts"],
            "upstream": {
                **_expected_upstream(inventory["adjacent_exclusions"]),
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


def _validate_encoded(value: dict[str, Any], location: str) -> bool:
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
        if not isinstance(value["value"], str) or str(int(value["value"])) != value["value"]:
            raise RuntimeError(f"Invalid encoded int at {location}.")
        return True
    if kind == "str":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], str):
            raise RuntimeError(f"Invalid encoded string at {location}.")
        return True
    if kind == "float":
        _require_keys(value, {"hex", "kind", "repr"}, location)
        decoded = float.fromhex(value["hex"])
        if (
            not math.isfinite(decoded)
            or decoded.hex() != value["hex"]
            or repr(decoded) != value["repr"]
        ):
            raise RuntimeError(f"Unsafe encoded float at {location}.")
        return True
    if kind == "float-nonfinite":
        _require_keys(value, {"kind", "value"}, location)
        if value["value"] not in {"nan", "negative-infinity", "positive-infinity"}:
            raise RuntimeError(f"Invalid encoded nonfinite value at {location}.")
        return True
    if kind == "list":
        _require_keys(value, {"items", "kind"}, location)
        if not isinstance(value["items"], list):
            raise RuntimeError(f"Invalid encoded list at {location}.")
        for index, item in enumerate(value["items"]):
            if not isinstance(item, dict) or not _validate_encoded(item, f"{location}[{index}]"):
                raise RuntimeError(f"Invalid encoded list item at {location}.")
        return True
    if kind == "dict":
        _require_keys(value, {"items", "kind"}, location)
        if not isinstance(value["items"], list):
            raise RuntimeError(f"Invalid encoded dict at {location}.")
        for index, item in enumerate(value["items"]):
            _require_keys(item, {"key", "value"}, f"{location}[{index}]")
            if not _validate_encoded(item["key"], f"{location}[{index}].key"):
                raise RuntimeError(f"Invalid encoded dict key at {location}.")
            if not _validate_encoded(item["value"], f"{location}[{index}].value"):
                raise RuntimeError(f"Invalid encoded dict value at {location}.")
        return True
    return False


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
        if "kind" in value and _validate_encoded(value, location):
            return
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string JSON key at {location}.")
            _validate_safe_tree(key, f"{location}.<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Unsupported JSON value at {location}: {type(value).__name__}")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "fact_sha256",
            "runtime",
            "schema",
            "symbols",
            "target_receipts",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("AirBoundary core schema drifted.")
    _validate_safe_tree(value)
    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("AirBoundary case order/count drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"AirBoundary case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"AirBoundary Python outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"AirBoundary inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_facts(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("AirBoundary fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("AirBoundary expected fact hashes drifted.")
    if value["case_sha256"] != case_sha256(cases):
        raise RuntimeError("AirBoundary per-case hash map drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("AirBoundary expected case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("AirBoundary aggregate cases hash drifted.")

    target_counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if set(target_counts) != set(TARGET_SYMBOLS):
        raise RuntimeError("AirBoundary target coverage drifted.")
    if set(EXCLUDED_SYMBOLS).intersection(target_counts):
        raise RuntimeError("Adjacent construction symbols were retargeted.")
    if any(definition["context_symbols"] for definition in definitions):
        raise RuntimeError("AirBoundary context symbols drifted.")
    if Counter(CLASSIFICATIONS.values()) != Counter({"exception": 2}):
        raise RuntimeError("AirBoundary classification counts drifted.")
    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("AirBoundary consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("AirBoundary runtime pin drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("AirBoundary symbol descriptors drifted.")
    if value["target_receipts"] != [dict(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("AirBoundary indexed target receipts drifted.")
    upstream = value["upstream"]
    if upstream != _expected_upstream(upstream.get("adjacent_exclusions", [])):
        raise RuntimeError("AirBoundary upstream receipts drifted.")
    observed_exclusions = [
        (item["inventory_index"], item["symbol"])
        for item in upstream["adjacent_exclusions"]
    ]
    if observed_exclusions != list(ADJACENT_EXCLUSION_IDENTITIES):
        raise RuntimeError("AirBoundary adjacent exclusion identities drifted.")
    if any(
        item["reason"] != _exclusion_reason(item["inventory_index"])
        for item in upstream["adjacent_exclusions"]
    ):
        raise RuntimeError("AirBoundary adjacent exclusion reasons drifted.")
    if canonical_sha256(upstream["adjacent_exclusions"]) != EXPECTED_ADJACENT_EXCLUSIONS_SHA256:
        raise RuntimeError("AirBoundary adjacent exclusion receipts drifted.")
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
    print(f"Wrote dragon construction AirBoundary core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Generate bounded observations for photovoltaic IDF emission.

The corpus targets only ``PhotoVoltaicPanel.to_idf_object`` on the common
valid constructor domain.  The class, constructor, property validation, and
invalid-domain behavior remain separate review boundaries.
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


SCHEMA = "dragons.python-reference.dragon-hvac-photovoltaic-to-idf-object.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
HVAC_SOURCE_PATH = "src/idragon/dragon/hvac.py"
TARGET_SYMBOL = "PhotoVoltaicPanel.to_idf_object"
TARGET_SYMBOLS = (TARGET_SYMBOL,)
EXPECTED_SYMBOL_RECEIPTS = {
    TARGET_SYMBOL: {
        "body_hash": "sha256:a227ed7b60c5a482a11b9a11f36e243b56cae95e2889effe9abe7e6e70d0346b",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:4723273d4b77d9286d4a47c4d753f71049e87d146ff912b0aa6a8ab8ed911287",
    }
}
ASSERTION_ID = "dragon-hvac-photovoltaic-to-idf-object-4723273d"
NATIVE_TARGET = "PhotovoltaicPanel.ToIdfObjects"
ADAPTATION = "compact-native-photovoltaic-idf-emission"
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-hvac-photovoltaic-to-idf-object.boundaries.maximum-tilt-default-ratio",
        "photovoltaic-to-idf-object",
        TARGET_SYMBOL,
    ),
    (
        "dragon-hvac-photovoltaic-to-idf-object.boundaries.minimum-angles-unit-efficiencies",
        "photovoltaic-to-idf-object",
        TARGET_SYMBOL,
    ),
    (
        "dragon-hvac-photovoltaic-to-idf-object.custom-ratio-nonsquare-area-sqrt",
        "photovoltaic-to-idf-object",
        TARGET_SYMBOL,
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 3
EXPECTED_CASE_COUNTS = {TARGET_SYMBOL: 3}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_photovoltaic_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load photovoltaic support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("Photovoltaic support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
SOURCE_RECEIPTS = CORE.SOURCE_RECEIPTS
SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == HVAC_SOURCE_PATH else (),
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
            "executor": executor,
            "expected_dotnet": {
                "adaptation": ADAPTATION,
                "outcome": "returned",
            },
            "id": identifier,
            "symbol": symbol,
        }
        for identifier, executor, symbol in EXPECTED_CASE_BINDINGS
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


PV_STATE_NAMES = (
    "name",
    "area",
    "tilt",
    "azimuth",
    "efficiency",
    "effective_area_ratio",
)
OBJECT_TYPES = (
    "Shading:Site",
    "PhotovoltaicPerformance:Simple",
    "Generator:Photovoltaic",
    "ElectricLoadCenter:Generators",
    "ElectricLoadCenter:Inverter:Simple",
    "ElectricLoadCenter:Distribution",
)


def _state(value: Any) -> list[dict[str, Any]]:
    return [
        {"name": name, "value": _encode(getattr(value, name))}
        for name in PV_STATE_NAMES
    ]


def _object_record(value: Any) -> dict[str, Any]:
    return {
        "field_count": len(value.data),
        "object_type": value.idd.name,
        "ordered_fields": _ordered_fields(value),
    }


def _emission(value: Any) -> dict[str, Any]:
    first = value.to_idf_object()
    second = value.to_idf_object()
    return {
        "all_allowed_fields_covered_in_order": all(
            list(item.data) == list(item.allowed_keys) for item in first
        ),
        "first_object_records": [_object_record(item) for item in first],
        "first_objects_pairwise_distinct": len({id(item) for item in first})
        == len(first),
        "fresh_idf_object_flags": [
            first_item is not second_item
            for first_item, second_item in zip(first, second, strict=True)
        ],
        "fresh_result_list": first is not second,
        "object_count": len(first),
        "object_types": [item.idd.name for item in first],
        "result_type": type(first).__name__,
        "same_idd_definition_flags": [
            first_item.idd is second_item.idd
            for first_item, second_item in zip(first, second, strict=True)
        ],
        "second_fields_equal_flags": [
            list(first_item.data.items()) == list(second_item.data.items())
            for first_item, second_item in zip(first, second, strict=True)
        ],
        "second_objects_pairwise_distinct": len({id(item) for item in second})
        == len(second),
    }


def _case_values(identifier: str) -> tuple[str, float, float, float, float, float, bool]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return "Default Ratio PV", 6.25, 90.0, 225.0, 0.2, 0.7, True
    if identifier == EXPECTED_CASE_IDS[1]:
        return "Unit Boundary PV", 1.0, 0.0, 0.0, 1.0, 1.0, False
    if identifier == EXPECTED_CASE_IDS[2]:
        return "Nonsquare Area PV", 2.0, 37.5, 123.25, 0.1875, 0.625, False
    raise RuntimeError(f"Unknown photovoltaic case: {identifier}")


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    name, area, tilt, azimuth, efficiency, ratio, use_default = _case_values(
        identifier
    )
    values = (name, area, tilt, azimuth, efficiency, ratio)
    if use_default:
        panel = modules.hvac.PhotoVoltaicPanel(
            name, area, tilt, azimuth, efficiency
        )
        explicit_values = values[:-1]
        explicit_names = PV_STATE_NAMES[:-1]
    else:
        panel = modules.hvac.PhotoVoltaicPanel(
            name,
            area,
            tilt,
            azimuth,
            efficiency,
            effective_area_ratio=ratio,
        )
        explicit_values = values
        explicit_names = PV_STATE_NAMES

    before = _state(panel)
    emission = _emission(panel)
    signature = inspect.signature(modules.hvac.PhotoVoltaicPanel)
    return {
        "constructor_context": {
            "declared_effective_area_ratio_default": _encode(
                signature.parameters["effective_area_ratio"].default
            ),
            "explicit_input_identity_preserved": all(
                getattr(panel, state_name) is input_value
                for state_name, input_value in zip(
                    explicit_names, explicit_values, strict=True
                )
            ),
            "keyword_only_parameters": [
                name
                for name, parameter in signature.parameters.items()
                if parameter.kind is inspect.Parameter.KEYWORD_ONLY
            ],
            "parameter_order": list(signature.parameters),
            "returned": True,
            "state": before,
            "state_unchanged_after_two_emissions": before == _state(panel),
            "used_default_effective_area_ratio": use_default,
        },
        "emission": emission,
    }


def _generator_list_fields(name: str) -> list[dict[str, Any]]:
    fields = [
        _field("Name", f"Generator4PVpanel:{name}"),
        _field("Generator 1 Name", f"PVpanel:{name}"),
        _field("Generator 1 Object Type", "Generator:Photovoltaic"),
        _field("Generator 1 Rated Electric Power Output", 1_000_000.0),
        _field("Generator 1 Availability Schedule Name", None),
        _field("Generator 1 Rated Thermal to Electrical Power Ratio", None),
    ]
    for index in range(2, 31):
        fields.extend(
            (
                _field(f"Generator {index} Name", None),
                _field(f"Generator {index} Object Type", None),
                _field(f"Generator {index} Rated Electric Power Output", None),
                _field(f"Generator {index} Availability Schedule Name", None),
                _field(
                    f"Generator {index} Rated Thermal to Electrical Power Ratio",
                    None,
                ),
            )
        )
    return fields


def _expected_object_records(
    name: str,
    side: float,
    tilt: float,
    azimuth: float,
    efficiency: float,
    ratio: float,
) -> list[dict[str, Any]]:
    objects = (
        (
            "Shading:Site",
            [
                _field("Name", f"Shading4PVpanel:{name}"),
                _field("Azimuth Angle", azimuth),
                _field("Tilt Angle", tilt),
                _field("Starting X Coordinate", 0),
                _field("Starting Y Coordinate", 0),
                _field("Starting Z Coordinate", 10),
                _field("Length", side),
                _field("Height", side),
            ],
        ),
        (
            "PhotovoltaicPerformance:Simple",
            [
                _field("Name", f"Spec4PVpanel:{name}"),
                _field("Fraction of Surface Area with Active Solar Cells", ratio),
                _field("Conversion Efficiency Input Mode", "Fixed"),
                _field("Value for Cell Efficiency if Fixed", efficiency),
                _field("Efficiency Schedule Name", None),
            ],
        ),
        (
            "Generator:Photovoltaic",
            [
                _field("Name", f"PVpanel:{name}"),
                _field("Surface Name", f"Shading4PVpanel:{name}"),
                _field(
                    "Photovoltaic Performance Object Type",
                    "PhotovoltaicPerformance:Simple",
                ),
                _field("Module Performance Name", f"Spec4PVpanel:{name}"),
                _field("Heat Transfer Integration Mode", "Decoupled"),
                _field("Number of Series Strings in Parallel", 1.0),
                _field("Number of Modules in Series", 1.0),
            ],
        ),
        (
            "ElectricLoadCenter:Generators",
            _generator_list_fields(name),
        ),
        (
            "ElectricLoadCenter:Inverter:Simple",
            [
                _field("Name", f"Inverter4PVpanel:{name}"),
                _field("Availability Schedule Name", "ALLON"),
                _field("Zone Name", None),
                _field("Radiative Fraction", 0),
                _field("Inverter Efficiency", 1),
            ],
        ),
        (
            "ElectricLoadCenter:Distribution",
            [
                _field("Name", f"Distribution4PVpanel:{name}"),
                _field("Generator List Name", f"Generator4PVpanel:{name}"),
                _field("Generator Operation Scheme Type", "Baseload"),
                _field(
                    "Generator Demand Limit Scheme Purchased Electric Demand Limit",
                    1_000_000.0,
                ),
                _field("Generator Track Schedule Name Scheme Schedule Name", None),
                _field("Generator Track Meter Scheme Meter Name", None),
                _field("Electrical Buss Type", "DirectCurrentWithInverter"),
                _field("Inverter Name", f"Inverter4PVpanel:{name}"),
                _field("Electrical Storage Object Name", None),
                _field("Transformer Object Name", None),
                _field(
                    "Storage Operation Scheme",
                    "TrackFacilityElectricDemandStoreExcessOnSite",
                ),
                _field("Storage Control Track Meter Name", None),
                _field("Storage Converter Object Name", None),
                _field("Maximum Storage State of Charge Fraction", 1.0),
                _field("Minimum Storage State of Charge Fraction", 0.0),
                _field("Design Storage Control Charge Power", None),
                _field("Storage Charge Power Fraction Schedule Name", None),
                _field("Design Storage Control Discharge Power", None),
                _field("Storage Discharge Power Fraction Schedule Name", None),
                _field("Storage Control Utility Demand Target", None),
                _field(
                    "Storage Control Utility Demand Target Fraction Schedule Name", None
                ),
            ],
        ),
    )
    return [
        {
            "field_count": len(fields),
            "object_type": object_type,
            "ordered_fields": fields,
        }
        for object_type, fields in objects
    ]


def _expected_constructor(
    values: tuple[str, float, float, float, float, float, bool]
) -> dict[str, Any]:
    name, area, tilt, azimuth, efficiency, ratio, use_default = values
    return {
        "declared_effective_area_ratio_default": _encode(0.7),
        "explicit_input_identity_preserved": True,
        "keyword_only_parameters": ["effective_area_ratio"],
        "parameter_order": list(PV_STATE_NAMES),
        "returned": True,
        "state": [
            {"name": state_name, "value": _encode(value)}
            for state_name, value in zip(
                PV_STATE_NAMES,
                (name, area, tilt, azimuth, efficiency, ratio),
                strict=True,
            )
        ],
        "state_unchanged_after_two_emissions": True,
        "used_default_effective_area_ratio": use_default,
    }


def expected_facts(identifier: str) -> dict[str, Any]:
    values = _case_values(identifier)
    name, _area, tilt, azimuth, efficiency, ratio, _use_default = values
    side = {
        EXPECTED_CASE_IDS[0]: 2.5,
        EXPECTED_CASE_IDS[1]: 1.0,
        EXPECTED_CASE_IDS[2]: 1.4142135623730951,
    }[identifier]
    return {
        "constructor_context": _expected_constructor(values),
        "emission": {
            "all_allowed_fields_covered_in_order": True,
            "first_object_records": _expected_object_records(
                name, side, tilt, azimuth, efficiency, ratio
            ),
            "first_objects_pairwise_distinct": True,
            "fresh_idf_object_flags": [True] * 6,
            "fresh_result_list": True,
            "object_count": 6,
            "object_types": list(OBJECT_TYPES),
            "result_type": "list",
            "same_idd_definition_flags": [True] * 6,
            "second_fields_equal_flags": [True] * 6,
            "second_objects_pairwise_distinct": True,
        },
    }


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
            "path": HVAC_SOURCE_PATH,
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
        "adaptations": {TARGET_SYMBOL: ADAPTATION},
        "assertion_ids": {TARGET_SYMBOL: ASSERTION_ID},
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {TARGET_SYMBOL: "exception"},
        "closure": {
            "context_only_not_targeted": [
                "PhotoVoltaicPanel",
                "PhotoVoltaicPanel.__init__",
                "PhotoVoltaicPanel.area",
                "PhotoVoltaicPanel.azimuth",
                "PhotoVoltaicPanel.effective_area_ratio",
                "PhotoVoltaicPanel.efficiency",
                "PhotoVoltaicPanel.tilt",
                "IdfObject",
                "IdfObject.__init__",
            ],
            "full_symbol_closure": False,
            "representation_contract": {
                "native_compact_field_counts": [8, 4, 4, 4, 5, 8],
                "native_policy": "omit-trailing-blank-and-default-fields",
                "python_complete_allowed_key_field_counts": [8, 5, 7, 151, 5, 21],
            },
            "scope": "bounded-common-valid-domain-compact-native-photovoltaic-idf-emission-adaptation",
            "unresolved_behavior": [
                "photovoltaic-constructor-validation-order-and-errors",
                "photovoltaic-property-setter-validation-order-and-errors",
                "invalid-or-nonfinite-domain-state",
                "semantic-populated-and-default-field-parity-requires-csharp-evidence",
                "isolated-IdfObject-validation-policy",
                "EnergyModel.to_idf",
            ],
        },
        "identity_encoding": "booleans-only-no-id-or-address",
        "native_targets": {TARGET_SYMBOL: NATIVE_TARGET},
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
        raise SystemExit("The aggregate photovoltaic inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        cases = []
        for definition in case_definitions():
            facts = _execute_case(definition["id"], modules)
            if facts != expected_facts(definition["id"]):
                raise SystemExit(
                    "Pinned Python photovoltaic semantics drifted: "
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
        raise RuntimeError("Photovoltaic schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Photovoltaic cases hash drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Photovoltaic case order/count drifted.")
    if (
        list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Pinned photovoltaic case IDs drifted.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Photovoltaic per-symbol case counts drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Photovoltaic case contract drifted: {case['id']}")
        _require_keys(
            case["expected_dotnet"],
            {"adaptation", "outcome"},
            "expected_dotnet",
        )
        if case["expected_dotnet"] != {
            "adaptation": ADAPTATION,
            "outcome": "returned",
        }:
            raise RuntimeError(
                f"Photovoltaic adaptation contract drifted: {case['id']}"
            )
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if (
            case["python"]["outcome"] != "returned"
            or case["python"]["facts"] != expected_facts(case["id"])
        ):
            raise RuntimeError(f"Photovoltaic semantics drifted: {case['id']}")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Photovoltaic consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Photovoltaic runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Photovoltaic upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Photovoltaic symbol receipts drifted.")
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
    print(f"Wrote dragon HVAC photovoltaic oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

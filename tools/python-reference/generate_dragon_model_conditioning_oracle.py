"""Generate the pinned zone-conditioning oracle from Python EPlusSimple 0.7.0.

The corpus binds the two ``EnergyModel`` zone-list properties and the
``Zone.is_conditioned`` predicate without serializing Python object identity.
Logical labels, indices, presence tags, and booleans preserve the observable
semantics needed by the native model-context adaptation.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import importlib
import importlib.metadata
import importlib.util
import os
from pathlib import Path
import re
import sys
from types import SimpleNamespace
from typing import Any, Iterator


SCHEMA = "dragons.python-reference.dragon-model-conditioning.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
MODEL_SOURCE_PATH = "src/idragon/dragon/model.py"
SHAPE_SOURCE_PATH = "src/idragon/dragon/shape.py"
SOURCE_SPECS = (
    {
        "ast_sha256": "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59",
        "path": MODEL_SOURCE_PATH,
        "source_sha256": "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
        "symbols": (
            "EnergyModel.conditioned_zones",
            "EnergyModel.unconditioned_zones",
        ),
    },
    {
        "ast_sha256": "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2",
        "path": SHAPE_SOURCE_PATH,
        "source_sha256": "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c",
        "symbols": ("Zone.is_conditioned",),
    },
)
EXPECTED_SYMBOL_RECEIPTS = {
    "EnergyModel.conditioned_zones": {
        "body_hash": "sha256:ae71f1c62c76cfdf6890e18c83f3dd2709b9fb72627f690db7dc52b7db719348",
        "kind": "function",
        "signature_hash": "sha256:e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e",
        "symbol_hash": "sha256:90ceddf7de437a59950e7081185fefbf1f56354a49662431452f11ac24bc6f24",
    },
    "EnergyModel.unconditioned_zones": {
        "body_hash": "sha256:e65c4689f16398a99be21f56cf6c046ee411718b151d637a75abc7e8076249c8",
        "kind": "function",
        "signature_hash": "sha256:e8822bd4d00ab05c8d049de4b8fedb8917e0b9cd2daa2c2a3f7503b1985b276e",
        "symbol_hash": "sha256:24b8c9a917df6c286d13dfb75c3ca04403b74cf0a70e6056cc933c9ed2822e08",
    },
    "Zone.is_conditioned": {
        "body_hash": "sha256:48a103a5bbb0b2a65f357d705eb38137269140e236bf98c2d56d7dd77474d9f3",
        "kind": "function",
        "signature_hash": "sha256:2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb",
        "symbol_hash": "sha256:6fe80cb193a6716b68c1033c5c52bd29f422ffb9efbdac8475a7f4b4ddc46370",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "Zone.is_conditioned": "model-context-zone-conditioning-predicate",
}
EXPECTED_ASSERTION_IDS = {
    "EnergyModel.conditioned_zones": "dragon-model-conditioning-conditioned-zones-90ceddf7",
    "EnergyModel.unconditioned_zones": "dragon-model-conditioning-unconditioned-zones-24b8c9a9",
    "Zone.is_conditioned": "dragon-model-conditioning-zone-is-conditioned-6fe80cb1",
}
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-model-conditioning.conditioned-zones.empty-selection",
        "energy-model-conditioned-zones",
        "EnergyModel.conditioned_zones",
    ),
    (
        "dragon-model-conditioning.conditioned-zones.falsey-availability-order",
        "energy-model-conditioned-zones",
        "EnergyModel.conditioned_zones",
    ),
    (
        "dragon-model-conditioning.conditioned-zones.mixed-order-identity",
        "energy-model-conditioned-zones",
        "EnergyModel.conditioned_zones",
    ),
    (
        "dragon-model-conditioning.unconditioned-zones.empty-selection",
        "energy-model-unconditioned-zones",
        "EnergyModel.unconditioned_zones",
    ),
    (
        "dragon-model-conditioning.unconditioned-zones.mixed-complement",
        "energy-model-unconditioned-zones",
        "EnergyModel.unconditioned_zones",
    ),
    (
        "dragon-model-conditioning.unconditioned-zones.profile-and-custom-only",
        "energy-model-unconditioned-zones",
        "EnergyModel.unconditioned_zones",
    ),
    (
        "dragon-model-conditioning.zone-is-conditioned.falsey-availability",
        "zone-is-conditioned",
        "Zone.is_conditioned",
    ),
    (
        "dragon-model-conditioning.zone-is-conditioned.no-supply",
        "zone-is-conditioned",
        "Zone.is_conditioned",
    ),
    (
        "dragon-model-conditioning.zone-is-conditioned.profile-availability-required",
        "zone-is-conditioned",
        "Zone.is_conditioned",
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 9
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

NONE_TAG = "none"
ON_TAG = "on"
INT_ZERO_TAG = "int-zero"
BOOL_FALSE_TAG = "bool-false"
EMPTY_STRING_TAG = "empty-string"

EMPTY_SELECTION_STATES = (
    ("profile-only", False, ON_TAG, False),
    ("custom-only", True, NONE_TAG, True),
    ("neither", False, NONE_TAG, False),
)
FALSEY_STATES = (
    ("supply-zero", True, INT_ZERO_TAG, False),
    ("supply-false", True, BOOL_FALSE_TAG, False),
    ("supply-empty", True, EMPTY_STRING_TAG, False),
)
MIXED_STATES = (
    ("no-supply-profile", False, ON_TAG, False),
    ("supply-on", True, ON_TAG, False),
    ("custom-only", True, NONE_TAG, True),
    ("supply-zero", True, INT_ZERO_TAG, False),
)
PROFILE_CUSTOM_STATES = (
    ("profile-only", False, ON_TAG, False),
    ("custom-only", True, NONE_TAG, True),
    ("neither", False, NONE_TAG, False),
    ("supply-on", True, ON_TAG, False),
)
NO_SUPPLY_STATES = (
    ("no-supply-none", False, NONE_TAG, False),
    ("no-supply-on", False, ON_TAG, False),
    ("no-supply-zero", False, INT_ZERO_TAG, False),
)
PROFILE_REQUIRED_STATES = (
    ("supply-none", True, NONE_TAG, False),
    ("supply-custom", True, NONE_TAG, True),
    ("supply-on", True, ON_TAG, False),
)

CASE_STATE_SPECS = {
    EXPECTED_CASE_IDS[0]: EMPTY_SELECTION_STATES,
    EXPECTED_CASE_IDS[1]: FALSEY_STATES,
    EXPECTED_CASE_IDS[2]: MIXED_STATES,
    EXPECTED_CASE_IDS[3]: FALSEY_STATES,
    EXPECTED_CASE_IDS[4]: MIXED_STATES,
    EXPECTED_CASE_IDS[5]: PROFILE_CUSTOM_STATES,
    EXPECTED_CASE_IDS[6]: FALSEY_STATES,
    EXPECTED_CASE_IDS[7]: NO_SUPPLY_STATES,
    EXPECTED_CASE_IDS[8]: PROFILE_REQUIRED_STATES,
}

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
        "_dragons_dragon_model_conditioning_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load conditioning oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Conditioning oracle support is not pinned.")
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


def _symbol_path(symbol: str) -> str:
    for source in SOURCE_SPECS:
        if symbol in source["symbols"]:
            return str(source["path"])
    raise KeyError(symbol)


def _load_source_inventory(
    path: Path,
    upstream_commit: str,
    source: dict[str, Any],
) -> dict[str, Any]:
    symbols = tuple(source["symbols"])
    expected_hashes = {
        symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"] for symbol in symbols
    }
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
        SUPPORT.EXPECTED_SYMBOL_HASHES = expected_hashes
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
    if inventory["file"] != expected_file:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": source["path"],
            "symbol": symbol,
        }
        for symbol in symbols
    ]
    if inventory["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} symbol receipts are not exact.")
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
        "symbols": [
            symbol
            for item in inventories
            for symbol in item["symbols"]
        ],
    }


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    result: dict[str, Any] = {
        "executor": executor,
        "id": identifier,
        "symbol": symbol,
    }
    adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
    if adaptation is not None:
        result["expected_dotnet"] = {
            "adaptation": adaptation,
            "outcome": "returned",
        }
    return result


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(_case(*binding) for binding in EXPECTED_CASE_BINDINGS)


def _availability_tag(tag: str) -> dict[str, Any]:
    if tag == NONE_TAG:
        return {"kind": "none"}
    if tag == ON_TAG:
        return {"kind": "token", "value": "ALLON"}
    if tag == INT_ZERO_TAG:
        return {"decimal": "0", "kind": "int"}
    if tag == BOOL_FALSE_TAG:
        return {"kind": "bool", "value": False}
    if tag == EMPTY_STRING_TAG:
        return {"kind": "string", "value": ""}
    raise RuntimeError(f"Unknown logical availability tag: {tag}")


def _expected_state(state: tuple[str, bool, str, bool]) -> dict[str, Any]:
    label, supply_present, availability_tag, custom_present = state
    return {
        "custom_supply_availability_present": custom_present,
        "label": label,
        "profile_availability": _availability_tag(availability_tag),
        "supply_present": supply_present,
        "zone_is_conditioned": supply_present and availability_tag != NONE_TAG,
    }


def _expected_list_facts(
    states: tuple[tuple[str, bool, str, bool], ...],
    conditioned: bool,
) -> dict[str, Any]:
    input_states = [_expected_state(state) for state in states]
    selected_indices = [
        index
        for index, state in enumerate(input_states)
        if state["zone_is_conditioned"] is conditioned
    ]
    return {
        "fresh_list_each_access": True,
        "input_labels": [state["label"] for state in input_states],
        "input_states": input_states,
        "result_type": "list",
        "selected_indices": selected_indices,
        "selected_labels": [input_states[index]["label"] for index in selected_indices],
        "selected_objects_are_input_objects": True,
        "source_list_unchanged": True,
    }


def expected_facts(identifier: str) -> dict[str, Any]:
    states = CASE_STATE_SPECS[identifier]
    if ".conditioned-zones." in identifier:
        return _expected_list_facts(states, conditioned=True)
    if ".unconditioned-zones." in identifier:
        return _expected_list_facts(states, conditioned=False)
    if ".zone-is-conditioned." in identifier:
        return {"observations": [_expected_state(state) for state in states]}
    raise RuntimeError(f"Unknown conditioning case: {identifier}")


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


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        source_root = Path(entry)
        model_path = source_root / "idragon" / "dragon" / "model.py"
        shape_path = source_root / "idragon" / "dragon" / "shape.py"
        if (
            model_path.is_file()
            and shape_path.is_file()
            and sha256_file(model_path) == SOURCE_SPECS[0]["source_sha256"]
            and sha256_file(shape_path) == SOURCE_SPECS[1]["source_sha256"]
        ):
            matches.append(source_root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


@contextmanager
def _pinned_modules(source_root: Path) -> Iterator[SimpleNamespace]:
    source_root = source_root.resolve()
    model_path = source_root / "idragon" / "dragon" / "model.py"
    shape_path = source_root / "idragon" / "dragon" / "shape.py"
    if (
        sha256_file(model_path) != SOURCE_SPECS[0]["source_sha256"]
        or sha256_file(shape_path) != SOURCE_SPECS[1]["source_sha256"]
    ):
        raise SystemExit("The selected dragon model/shape sources are not pinned.")

    saved_modules = {
        name: module
        for name, module in sys.modules.items()
        if name == "idragon" or name.startswith("idragon.")
    }
    for name in saved_modules:
        sys.modules.pop(name, None)
    sys.path.insert(0, str(source_root))
    try:
        model = importlib.import_module("idragon.dragon.model")
        shape = importlib.import_module("idragon.dragon.shape")
        profile = importlib.import_module("idragon.dragon.profile")
        hvac = importlib.import_module("idragon.dragon.hvac")
        if Path(model.__file__).resolve() != model_path.resolve():
            raise SystemExit("Imported idragon.dragon.model is not the pinned source.")
        if Path(shape.__file__).resolve() != shape_path.resolve():
            raise SystemExit("Imported idragon.dragon.shape is not the pinned source.")
        if (
            model.Zone is not shape.Zone
            or model.Profile is not profile.Profile
            or model.SupplyGroup is not hvac.SupplyGroup
        ):
            raise SystemExit("Pinned dragon model dependencies do not share identity.")
        yield SimpleNamespace(model=model, shape=shape, profile=profile, hvac=hvac)
    finally:
        for name in list(sys.modules):
            if name == "idragon" or name.startswith("idragon."):
                sys.modules.pop(name, None)
        sys.modules.update(saved_modules)
        try:
            sys.path.remove(str(source_root))
        except ValueError:
            pass


def _runtime_resources(modules: SimpleNamespace) -> SimpleNamespace:
    on_schedule = modules.profile.Schedule(
        "ALLON", type=modules.profile.ScheduleType.ONOFF
    )
    custom_schedule = modules.profile.Schedule(
        "CUSTOM", type=modules.profile.ScheduleType.ONOFF
    )
    system = modules.hvac.ElectricRadiator("Conditioning Oracle Radiator", 1)
    return SimpleNamespace(
        custom_schedule=custom_schedule,
        on_schedule=on_schedule,
        system=system,
    )


def _availability_value(tag: str, resources: SimpleNamespace) -> Any:
    if tag == NONE_TAG:
        return None
    if tag == ON_TAG:
        return resources.on_schedule
    if tag == INT_ZERO_TAG:
        return 0
    if tag == BOOL_FALSE_TAG:
        return False
    if tag == EMPTY_STRING_TAG:
        return ""
    raise RuntimeError(f"Unknown runtime availability tag: {tag}")


def _tag_runtime_availability(value: Any, resources: SimpleNamespace) -> dict[str, Any]:
    if value is None:
        return _availability_tag(NONE_TAG)
    if value is resources.on_schedule:
        return _availability_tag(ON_TAG)
    if type(value) is bool:
        if value is not False:
            raise RuntimeError("Only the pinned false boolean is permitted.")
        return _availability_tag(BOOL_FALSE_TAG)
    if type(value) is int:
        if value != 0:
            raise RuntimeError("Only the pinned zero integer is permitted.")
        return _availability_tag(INT_ZERO_TAG)
    if type(value) is str:
        if value != "":
            raise RuntimeError("Only the pinned empty string is permitted.")
        return _availability_tag(EMPTY_STRING_TAG)
    raise RuntimeError(
        "Unexpected runtime availability value: " + type(value).__name__
    )


def _make_zone(
    modules: SimpleNamespace,
    resources: SimpleNamespace,
    state: tuple[str, bool, str, bool],
) -> Any:
    label, supply_present, availability_tag, custom_present = state
    if custom_present and not supply_present:
        raise RuntimeError("Custom supply availability requires a supply group.")
    availability = _availability_value(availability_tag, resources)
    profile = modules.model.Profile(
        "profile:" + label,
        hvac_availability=availability,
    )
    supply = None
    if supply_present:
        custom = resources.custom_schedule if custom_present else None
        supply = modules.model.SupplyGroup(
            [resources.system],
            availabilities=[custom],
        )
    return modules.model.Zone(label, [], profile, 0, 0, supply, None)


def _runtime_state(zone: Any, resources: SimpleNamespace) -> dict[str, Any]:
    supply_present = zone.supply is not None
    custom_present = supply_present and any(
        item is not None for item in zone.supply.availabilities
    )
    return {
        "custom_supply_availability_present": custom_present,
        "label": zone.name,
        "profile_availability": _tag_runtime_availability(
            zone.profile.hvac_availability, resources
        ),
        "supply_present": supply_present,
        "zone_is_conditioned": zone.is_conditioned,
    }


def _identity_indices(source: list[Any], selected: list[Any]) -> list[int]:
    result: list[int] = []
    for item in selected:
        matches = [index for index, candidate in enumerate(source) if item is candidate]
        if len(matches) != 1:
            raise RuntimeError("Selected zone identity is not unique in the input list.")
        result.append(matches[0])
    return result


def _execute_model_property(
    identifier: str,
    modules: SimpleNamespace,
    resources: SimpleNamespace,
    property_name: str,
) -> dict[str, Any]:
    zones = [
        _make_zone(modules, resources, state)
        for state in CASE_STATE_SPECS[identifier]
    ]
    source = list(zones)
    original = tuple(source)
    energy_model = modules.model.EnergyModel(
        "Conditioning Oracle",
        zone=source,
        pv=[],
    )
    first = getattr(energy_model, property_name)
    second = getattr(energy_model, property_name)
    indices = _identity_indices(source, first)
    source_unchanged = (
        energy_model.zone is source
        and len(source) == len(original)
        and all(actual is expected for actual, expected in zip(source, original))
    )
    return {
        "fresh_list_each_access": first is not second,
        "input_labels": [zone.name for zone in source],
        "input_states": [_runtime_state(zone, resources) for zone in source],
        "result_type": type(first).__name__,
        "selected_indices": indices,
        "selected_labels": [zone.name for zone in first],
        "selected_objects_are_input_objects": all(
            selected is source[index] for selected, index in zip(first, indices)
        ),
        "source_list_unchanged": source_unchanged,
    }


def _execute_zone_predicate(
    identifier: str,
    modules: SimpleNamespace,
    resources: SimpleNamespace,
) -> dict[str, Any]:
    zones = [
        _make_zone(modules, resources, state)
        for state in CASE_STATE_SPECS[identifier]
    ]
    return {
        "observations": [_runtime_state(zone, resources) for zone in zones],
    }


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _expected_symbol_descriptors() -> list[dict[str, Any]]:
    return [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": _symbol_path(symbol),
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
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
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "assertion_ids": EXPECTED_ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {
            symbol: (
                "exception"
                if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
                else "equivalent"
            )
            for symbol in TARGET_SYMBOLS
        },
        "identity_encoding": "logical-label-index-and-boolean-only-no-id-or-address",
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
        "state_encoding": "logical-presence-tags-no-raw-objects",
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _expected_runtime() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
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
    expected_files = [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
    ]
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": expected_files,
        "symbols": _expected_symbol_descriptors(),
    }:
        raise SystemExit("The aggregate conditioning inventory is not exact.")
    for source in SOURCE_SPECS:
        source_file = imported_root / Path(str(source["path"])).relative_to("src")
        if sha256_file(source_file) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    definitions = case_definitions()
    with _pinned_modules(imported_root) as modules:
        resources = _runtime_resources(modules)
        cases: list[dict[str, Any]] = []
        for definition in definitions:
            identifier = definition["id"]
            if definition["executor"] == "energy-model-conditioned-zones":
                facts = _execute_model_property(
                    identifier,
                    modules,
                    resources,
                    "conditioned_zones",
                )
            elif definition["executor"] == "energy-model-unconditioned-zones":
                facts = _execute_model_property(
                    identifier,
                    modules,
                    resources,
                    "unconditioned_zones",
                )
            elif definition["executor"] == "zone-is-conditioned":
                facts = _execute_zone_predicate(identifier, modules, resources)
            else:
                raise SystemExit(
                    "Unknown conditioning executor: " + definition["executor"]
                )
            if facts != expected_facts(identifier):
                raise SystemExit(f"Pinned Python conditioning semantics drifted: {identifier}")
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
            "sources": [
                {
                    "ast_sha256": source["ast_sha256"],
                    "path": source["path"],
                    "source_sha256": sha256_file(
                        imported_root
                        / Path(str(source["path"])).relative_to("src")
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
        raise RuntimeError("Conditioning schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Conditioning cases hash drifted.")

    # Reject unsafe values before comparing semantic projections so a safe
    # contract failure cannot mask a host-state leak.
    _validate_safe_tree(value)

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Conditioning case order/count drifted.")
    if [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Conditioning case order/count drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Pinned conditioning case IDs are not sorted.")
    if len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Pinned conditioning case IDs are not unique.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        {symbol: 3 for symbol in TARGET_SYMBOLS}
    ):
        raise RuntimeError("Conditioning cases are not three per symbol.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Conditioning case contract drifted: {case['id']}")
        if "expected_dotnet" in case:
            _require_keys(
                case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet"
            )
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned":
            raise RuntimeError(f"Python case outcome drifted: {case['id']}")
        if case["python"]["facts"] != expected_facts(case["id"]):
            raise RuntimeError(f"Conditioning semantics drifted: {case['id']}")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Conditioning consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Conditioning runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Conditioning upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Conditioning symbol receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the conditioning oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
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
    print(f"Wrote dragon model conditioning oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

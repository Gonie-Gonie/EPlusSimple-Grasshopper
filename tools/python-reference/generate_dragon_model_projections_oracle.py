"""Generate the pinned EnergyModel collection-projection oracle.

The corpus binds four Python EPlusSimple 0.7.0 properties while keeping raw
object addresses out of the fixture.  CPython set order is recorded exactly;
the consumer contract separately declares the two deterministic native-order
adaptations.
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


SCHEMA = "goniegonie.python-reference.dragon-model-projections.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
SOURCE_SPECS = (
    {
        "ast_sha256": "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a",
        "path": "src/idragon/dragon/construction.py",
        "source_sha256": "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59",
        "path": "src/idragon/dragon/model.py",
        "source_sha256": "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
        "symbols": (
            "EnergyModel.surfaces",
            "EnergyModel.used_constructions",
            "EnergyModel.used_layers",
            "EnergyModel.used_profiles",
        ),
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
)
EXPECTED_SYMBOL_RECEIPTS = {
    "EnergyModel.surfaces": {
        "body_hash": "sha256:9ac965df879ac38614b80c38800b8b7e28f3a584d20be71afac9301eea223c06",
        "kind": "function",
        "signature_hash": "sha256:175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea",
        "symbol_hash": "sha256:9bd40b3fbdc974f1f3a7550b2df6ec8f4c41ce9cb55ecbc07b3f2fce264834c0",
    },
    "EnergyModel.used_constructions": {
        "body_hash": "sha256:56cc7c61d049242fa77c1c2457d6d9f5678ca41a41af86ddd2ff93be20ed78b3",
        "kind": "function",
        "signature_hash": "sha256:47d2fe431ebc01347b7bef0a612859f9d45131c67b7ee67971757a0694919023",
        "symbol_hash": "sha256:b34dd26fdb9af00f053278e77ac3cc85394a646405e8e5e0b5c077342fd1bebd",
    },
    "EnergyModel.used_layers": {
        "body_hash": "sha256:bde4ae4c3efe1129e1c3ee19dc273a7e251f770f7173fbc1a3d2b67ec80d0733",
        "kind": "function",
        "signature_hash": "sha256:d5bc4e72ec91b9ecdbdd46cd7a50e3da18408ff227d9549c7ae42bf488381844",
        "symbol_hash": "sha256:e15c8d38a7b918895bf399bc319bbb2caf2810d416cb4c8792fedb5cec3358f0",
    },
    "EnergyModel.used_profiles": {
        "body_hash": "sha256:5e04e97f3e1161b94743a1377272a037c646df7e0aa07b6a3ce51c3d4b61ae9a",
        "kind": "function",
        "signature_hash": "sha256:2417ee894af42b33af27bb335ee1a91c7205d1a2093879c28e6e4178554e4a60",
        "symbol_hash": "sha256:b8a8a5f692a0cbeeec4215cbab71e89291a3f96e68d7702853631dc454a695ab",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "EnergyModel.used_constructions": "deterministic-used-construction-projection",
    "EnergyModel.used_layers": "deterministic-used-layer-projection",
}
EXPECTED_ASSERTION_IDS = {
    "EnergyModel.surfaces": "dragon-model-projections-surfaces-9bd40b3f",
    "EnergyModel.used_constructions": "dragon-model-projections-used-constructions-b34dd26f",
    "EnergyModel.used_layers": "dragon-model-projections-used-layers-e15c8d38",
    "EnergyModel.used_profiles": "dragon-model-projections-used-profiles-b8a8a5f6",
}
EXPECTED_CASE_BINDINGS = (
    ("dragon-model-projections.surfaces.empty-fresh", "energy-model-surfaces", "EnergyModel.surfaces"),
    ("dragon-model-projections.surfaces.flatten-order-identity", "energy-model-surfaces", "EnergyModel.surfaces"),
    ("dragon-model-projections.surfaces.result-mutation-isolated", "energy-model-surfaces", "EnergyModel.surfaces"),
    ("dragon-model-projections.used-constructions.collision-dedup", "energy-model-used-constructions", "EnergyModel.used_constructions"),
    ("dragon-model-projections.used-constructions.empty-filtered", "energy-model-used-constructions", "EnergyModel.used_constructions"),
    ("dragon-model-projections.used-constructions.hash-order-resize", "energy-model-used-constructions", "EnergyModel.used_constructions"),
    ("dragon-model-projections.used-layers.empty-fresh", "energy-model-used-layers", "EnergyModel.used_layers"),
    ("dragon-model-projections.used-layers.hash-equality-mismatch", "energy-model-used-layers", "EnergyModel.used_layers"),
    ("dragon-model-projections.used-layers.hash-order-resize", "energy-model-used-layers", "EnergyModel.used_layers"),
    ("dragon-model-projections.used-profiles.case-sensitive-unicode-replacement", "energy-model-used-profiles", "EnergyModel.used_profiles"),
    ("dragon-model-projections.used-profiles.duplicate-name-last-wins", "energy-model-used-profiles", "EnergyModel.used_profiles"),
    ("dragon-model-projections.used-profiles.empty-fresh", "energy-model-used-profiles", "EnergyModel.used_profiles"),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 12
EXPECTED_DEPENDENCIES = {
    "colorama": "0.4.6", "et_xmlfile": "2.0.0", "numpy": "2.3.1",
    "openpyxl": "3.1.5", "pandas": "2.3.0", "python-dateutil": "2.9.0.post0",
    "pytz": "2024.2", "six": "1.16.0", "tqdm": "4.67.1", "tzdata": "2024.2",
}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
ORDER_NAMES = ("Zulu", "Alpha", "Dragon", "Brick", "Omega", "\ud55c\uae00", "\U0001f409", "Glass", "Roof")
ORDER_HASHES = (
    "-6911130904927632849", "-5489660660273336509", "8186683321401986332",
    "-2926469781815734489", "7551058807157025315", "3011526259676503552",
    "8889289909682346436", "5629188463992988249", "2500655868670704794",
)
RAW_ADDRESS_PATTERN = re.compile(r"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])")
ABSOLUTE_PATH_PATTERN = re.compile(r"(?i)(?:[a-z]:[\\/]|\\\\[^\\]|(?<![A-Za-z0-9_.<>-])/(?:home|mnt|private|root|tmp|Users|var)(?:/|$))")
GUID_PATTERN = re.compile(r"(?i)(?<![0-9a-f])(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|[0-9a-f]{32})(?![0-9a-f])")
TIMESTAMP_PATTERN = re.compile(r"(?<!\d)\d{4}-\d{2}-\d{2}[T ][0-2]\d:[0-5]\d:[0-5]\d")


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
    spec = importlib.util.spec_from_file_location("_goniegonie_projection_support", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256:
        raise RuntimeError("Projection oracle support is not pinned.")
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
    return "src/idragon/dragon/model.py"


def _load_source_inventory(path: Path, upstream_commit: str, source: dict[str, Any]) -> dict[str, Any]:
    symbols = tuple(source["symbols"])
    names = ("SOURCE_PATH", "EXPECTED_SOURCE_SHA256", "EXPECTED_SYMBOL_HASHES", "TARGET_SYMBOLS")
    original = {name: getattr(SUPPORT, name) for name in names}
    try:
        SUPPORT.SOURCE_PATH = source["path"]
        SUPPORT.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        SUPPORT.EXPECTED_SYMBOL_HASHES = {symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"] for symbol in symbols}
        SUPPORT.TARGET_SYMBOLS = symbols
        inventory = SUPPORT.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(SUPPORT, name, value)
    expected_file = {"ast_hash": source["ast_sha256"], "content_hash": source["source_sha256"], "path": source["path"]}
    expected_symbols = [{**EXPECTED_SYMBOL_RECEIPTS[symbol], "path": source["path"], "symbol": symbol} for symbol in symbols]
    if inventory["file"] != expected_file or inventory["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return inventory


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    inventories = [_load_source_inventory(path, upstream_commit, source) for source in SOURCE_SPECS]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in inventories):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in inventories],
        "symbols": [symbol for item in inventories for symbol in item["symbols"]],
    }


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    result: dict[str, Any] = {"executor": executor, "id": identifier, "symbol": symbol}
    adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
    if adaptation is not None:
        result["expected_dotnet"] = {"adaptation": adaptation, "outcome": "returned"}
    return result


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(_case(*binding) for binding in EXPECTED_CASE_BINDINGS)


def _hash_entry(label: str, name: str, decimal: str) -> dict[str, str]:
    return {"hash_decimal": decimal, "label": label, "name": name}


def expected_facts(identifier: str) -> dict[str, Any]:
    common = {
        "fresh_list_each_access": True,
        "result_type": "list",
        "source_lists_unchanged": True,
    }
    if identifier == EXPECTED_CASE_IDS[0]:
        return {
            **common,
            "input_zone_surface_indices": [],
            "output_surface_indices": [],
            "registry_labels": [],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[1]:
        return {
            **common,
            "input_zone_surface_indices": [[0, 1], [], [1, 2, 0]],
            "output_surface_indices": [0, 1, 1, 2, 0],
            "registry_labels": ["A", "B", "C"],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[2]:
        return {
            **common,
            "first_result_indices_after_mutation": [2, 1, 0, 3],
            "first_result_indices_before_mutation": [0, 1, 2],
            "input_zone_surface_indices": [[0, 1], [2]],
            "registry_labels": ["A", "B", "C", "RESULT-ONLY"],
            "returned_list_mutation_supported": True,
            "second_result_indices_after_mutation": [0, 1, 2],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[3]:
        registry = [
            _hash_entry("first-equal", "Shared", "-3612718561660722853"),
            _hash_entry("later-equal", "Shared", "-3612718561660722853"),
            _hash_entry("same-name-unequal", "Shared", "-3612718561660722853"),
            _hash_entry("other", "Other", "-8767484776472450951"),
        ]
        return {
            **common,
            "construction_registry": registry,
            "equality": {
                "first_equals_later": True,
                "first_equals_same_name_unequal": False,
            },
            "input_registry_indices": [0, 1, 2, 3, 0],
            "output_labels": ["other", "same-name-unequal", "first-equal"],
            "output_registry_indices": [3, 2, 0],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[4]:
        return {
            **common,
            "construction_registry": [],
            "input_filtered_labels": ["air-a", "no-mass", "air-b", "air-a"],
            "input_kinds": ["air-boundary", "no-mass", "air-boundary", "air-boundary"],
            "output_labels": [],
            "output_registry_indices": [],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[5]:
        registry = [
            _hash_entry(f"c{index}", name, ORDER_HASHES[index])
            for index, name in enumerate(ORDER_NAMES)
        ]
        return {
            **common,
            "construction_registry": registry,
            "input_registry_indices": list(range(9)),
            "output_labels": ["c5", "c4", "c1", "c6", "c3", "c0", "c7", "c8", "c2"],
            "output_registry_indices": [5, 4, 1, 6, 3, 0, 7, 8, 2],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[6]:
        return {
            **common,
            "construction_input_labels": ["air-a", "no-mass", "air-b"],
            "construction_input_kinds": ["air-boundary", "no-mass", "air-boundary"],
            "layer_registry": [],
            "output_labels": [],
            "output_layer_indices": [],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[7]:
        registry = [
            _hash_entry("base", "Core-A", "-276500280528783050"),
            _hash_entry("equal-different-name", "Core-B", "-8620372976408521596"),
            _hash_entry("same-name-different-thickness", "Core-A", "-276500280528783050"),
            _hash_entry("exact-duplicate", "Core-A", "-276500280528783050"),
        ]
        return {
            **common,
            "equality": {
                "base_equals_equal_different_name": True,
                "base_equals_exact_duplicate": True,
                "base_equals_same_name_different_thickness": False,
            },
            "layer_registry": registry,
            "output_labels": ["same-name-different-thickness", "equal-different-name", "base"],
            "output_layer_indices": [2, 1, 0],
            "python_flattened_layer_indices": [0, 1, 2, 3],
            "python_used_construction_indices": [0],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[8]:
        construction_registry = [
            _hash_entry("construction-zulu", "Zulu", ORDER_HASHES[0]),
            _hash_entry("construction-alpha", "Alpha", ORDER_HASHES[1]),
            _hash_entry("construction-dragon", "Dragon", ORDER_HASHES[2]),
        ]
        layer_registry = [
            _hash_entry(f"l{index}", name, ORDER_HASHES[index])
            for index, name in enumerate(ORDER_NAMES)
        ]
        return {
            **common,
            "construction_input_indices": [0, 1, 2],
            "construction_registry": construction_registry,
            "layer_registry": layer_registry,
            "output_labels": ["l5", "l4", "l6", "l1", "l3", "l0", "l7", "l8", "l2"],
            "output_layer_indices": [5, 4, 6, 1, 3, 0, 7, 8, 2],
            "python_flattened_layer_indices": [3, 4, 5, 6, 7, 8, 0, 1, 2],
            "python_used_construction_indices": [1, 2, 0],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[9]:
        return {
            **common,
            "first_seen_name_order": ["Alpha", "alpha", "\ud55c\uae00", "\U0001f409"],
            "input_names": ["Alpha", "alpha", "\ud55c\uae00", "\U0001f409", "Alpha"],
            "profile_registry_labels": ["alpha-first", "lower-alpha", "korean", "dragon", "alpha-last"],
            "output_labels": ["alpha-last", "lower-alpha", "korean", "dragon"],
            "output_profile_indices": [4, 1, 2, 3],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[10]:
        return {
            **common,
            "first_seen_name_order": ["Team", "Aux", "Core"],
            "input_names": ["Team", "Aux", "Team", "Core", "Aux"],
            "profile_registry_labels": ["team-first", "aux-first", "team-last", "core-only", "aux-last"],
            "output_labels": ["team-last", "aux-last", "core-only"],
            "output_profile_indices": [2, 4, 3],
            "selected_objects_are_registry_objects": True,
        }
    if identifier == EXPECTED_CASE_IDS[11]:
        return {
            **common,
            "first_seen_name_order": [],
            "input_names": [],
            "output_labels": [],
            "output_profile_indices": [],
            "profile_registry_labels": [],
            "selected_objects_are_registry_objects": True,
        }
    raise RuntimeError(f"Unknown projection case: {identifier}")


def _dependencies() -> dict[str, str]:
    result: dict[str, str] = {}
    for distribution in EXPECTED_DEPENDENCIES:
        try:
            result[distribution] = importlib.metadata.version(distribution)
        except importlib.metadata.PackageNotFoundError as error:
            raise RuntimeError(f"Required reference dependency is missing: {distribution}") from error
    return result


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(str(source["path"])).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        candidate = Path(entry)
        if all(_source_file(candidate, source).is_file() and sha256_file(_source_file(candidate, source)) == source["source_sha256"] for source in SOURCE_SPECS):
            matches.append(candidate.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


@contextmanager
def _pinned_modules(source_root: Path) -> Iterator[SimpleNamespace]:
    source_root = source_root.resolve()
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(source_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The selected {source['path']} source is not pinned.")
    saved_modules = {name: module for name, module in sys.modules.items() if name == "idragon" or name.startswith("idragon.")}
    for name in saved_modules:
        sys.modules.pop(name, None)
    sys.path.insert(0, str(source_root))
    try:
        construction = importlib.import_module("idragon.dragon.construction")
        model = importlib.import_module("idragon.dragon.model")
        profile = importlib.import_module("idragon.dragon.profile")
        shape = importlib.import_module("idragon.dragon.shape")
        modules = {"construction": construction, "model": model, "profile": profile, "shape": shape}
        for source in SOURCE_SPECS:
            key = Path(str(source["path"])).stem
            module = modules[key]
            if Path(module.__file__).resolve() != _source_file(source_root, source).resolve():
                raise SystemExit(f"Imported {source['path']} is not the pinned source.")
        if model.Construction is not construction.Construction or model.Profile is not profile.Profile or model.Surface is not shape.Surface or model.Zone is not shape.Zone:
            raise SystemExit("Pinned dragon projection dependencies do not share identity.")
        yield SimpleNamespace(**modules)
    finally:
        for name in list(sys.modules):
            if name == "idragon" or name.startswith("idragon."):
                sys.modules.pop(name, None)
        sys.modules.update(saved_modules)
        try:
            sys.path.remove(str(source_root))
        except ValueError:
            pass


def _material(modules: SimpleNamespace, name: str = "Brick") -> Any:
    return modules.construction.Material(name, 0.72, 1920, 840)


def _layer(modules: SimpleNamespace, name: str, thickness: float, material: Any | None = None) -> Any:
    return modules.construction.Layer(name, material or _material(modules), thickness)


def _construction(modules: SimpleNamespace, name: str, layers: list[Any]) -> Any:
    return modules.construction.Construction(name, *layers)


def _surface(modules: SimpleNamespace, label: str, construction: Any, offset: int) -> Any:
    vertices = [
        modules.shape.Vertex(offset, 0, 0),
        modules.shape.Vertex(offset + 1, 0, 0),
        modules.shape.Vertex(offset + 1, 0, 1),
    ]
    return modules.shape.Surface(
        label,
        modules.shape.SurfaceType.WALL,
        construction,
        modules.shape.SurfaceBoundaryCondition.OUTDOOR,
        vertices,
        window=[],
        door=[],
    )


def _zone(modules: SimpleNamespace, label: str, surfaces: list[Any], profile: Any | None = None) -> Any:
    return modules.shape.Zone(label, surfaces, profile or modules.profile.Profile("profile:" + label), 0, 0, None, None)


def _model(modules: SimpleNamespace, zones: list[Any]) -> Any:
    return modules.model.EnergyModel("Projection Oracle", zone=zones, pv=[])


def _identity_indices(registry: list[Any], selected: list[Any]) -> list[int]:
    result: list[int] = []
    for item in selected:
        matches = [index for index, candidate in enumerate(registry) if item is candidate]
        if len(matches) != 1:
            raise RuntimeError("Projection identity is absent or ambiguous in its registry.")
        result.append(matches[0])
    return result


def _default_construction(modules: SimpleNamespace) -> Any:
    return _construction(modules, "Default", [_layer(modules, "Default layer", 0.1)])


def _surface_source_is_unchanged(zones: list[Any], originals: list[tuple[Any, ...]]) -> bool:
    return len(zones) == len(originals) and all(
        len(zone.surface) == len(original)
        and all(actual is expected for actual, expected in zip(zone.surface, original))
        for zone, original in zip(zones, originals)
    )


def _execute_surfaces(identifier: str, modules: SimpleNamespace) -> dict[str, Any]:
    construction = _default_construction(modules)
    if identifier == EXPECTED_CASE_IDS[0]:
        registry: list[Any] = []
        zones: list[Any] = []
        nested: list[list[int]] = []
    else:
        labels = ("A", "B", "C", "RESULT-ONLY") if identifier == EXPECTED_CASE_IDS[2] else ("A", "B", "C")
        registry = [
            _surface(modules, label, construction, index * 2)
            for index, label in enumerate(labels)
        ]
        if identifier == EXPECTED_CASE_IDS[1]:
            nested = [[0, 1], [], [1, 2, 0]]
        elif identifier == EXPECTED_CASE_IDS[2]:
            nested = [[0, 1], [2]]
        else:
            raise RuntimeError(f"Unknown surfaces case: {identifier}")
        zones = [
            _zone(modules, f"zone-{index}", [registry[item] for item in indices])
            for index, indices in enumerate(nested)
        ]
    zone_source = list(zones)
    originals = [tuple(zone.surface) for zone in zone_source]
    model = _model(modules, zone_source)
    first = model.surfaces
    second = model.surfaces
    if len(first) != len(second) or any(left is not right for left, right in zip(first, second)):
        raise RuntimeError("Repeated surfaces projection changed its object sequence.")
    fresh = first is not second
    source_unchanged = model.zone is zone_source and _surface_source_is_unchanged(zone_source, originals)
    if identifier != EXPECTED_CASE_IDS[2]:
        output = _identity_indices(registry, first)
        return {
            "fresh_list_each_access": fresh,
            "input_zone_surface_indices": nested,
            "output_surface_indices": output,
            "registry_labels": [surface.name for surface in registry],
            "result_type": type(first).__name__,
            "selected_objects_are_registry_objects": all(first[index] is registry[item] for index, item in enumerate(output)),
            "source_lists_unchanged": source_unchanged,
        }
    before = _identity_indices(registry, first)
    first.reverse()
    first.append(registry[3])
    after = _identity_indices(registry, first)
    after_mutation = model.surfaces
    second_after = _identity_indices(registry, after_mutation)
    return {
        "first_result_indices_after_mutation": after,
        "first_result_indices_before_mutation": before,
        "fresh_list_each_access": fresh and first is not after_mutation,
        "input_zone_surface_indices": nested,
        "registry_labels": [surface.name for surface in registry],
        "result_type": type(first).__name__,
        "returned_list_mutation_supported": True,
        "second_result_indices_after_mutation": second_after,
        "selected_objects_are_registry_objects": all(after_mutation[index] is registry[item] for index, item in enumerate(second_after)),
        "source_lists_unchanged": source_unchanged and _surface_source_is_unchanged(zone_source, originals),
    }


def _construction_case_inputs(identifier: str, modules: SimpleNamespace) -> tuple[list[Any], list[str], list[Any]]:
    if identifier == EXPECTED_CASE_IDS[4]:
        air_a = modules.construction.AirBoundary("air-a")
        air_b = modules.construction.AirBoundary("air-b")
        no_mass = modules.construction.NoMassConstruction("no-mass", 0.5)
        return [], [], [air_a, no_mass, air_b, air_a]
    if identifier == EXPECTED_CASE_IDS[3]:
        material = _material(modules)
        registry = [
            _construction(modules, "Shared", [_layer(modules, "base", 0.1, material)]),
            _construction(modules, "Shared", [_layer(modules, "renamed", 0.1, material)]),
            _construction(modules, "Shared", [_layer(modules, "base", 0.2, material)]),
            _construction(modules, "Other", [_layer(modules, "other", 0.1, material)]),
        ]
        return registry, ["first-equal", "later-equal", "same-name-unequal", "other"], [registry[index] for index in (0, 1, 2, 3, 0)]
    if identifier == EXPECTED_CASE_IDS[5]:
        registry = [
            _construction(modules, name, [_layer(modules, f"layer-{index}", 0.01 * (index + 1))])
            for index, name in enumerate(ORDER_NAMES)
        ]
        return registry, [f"c{index}" for index in range(9)], list(registry)
    raise RuntimeError(f"Unknown used-constructions case: {identifier}")


def _execute_used_constructions(identifier: str, modules: SimpleNamespace) -> dict[str, Any]:
    registry, labels, construction_inputs = _construction_case_inputs(identifier, modules)
    surfaces = [
        _surface(modules, f"surface-{index}", construction, index * 2)
        for index, construction in enumerate(construction_inputs)
    ]
    zones = [_zone(modules, "zone", surfaces)]
    originals = [tuple(zone.surface) for zone in zones]
    model = _model(modules, zones)
    first = model.used_constructions
    second = model.used_constructions
    if len(first) != len(second) or any(left is not right for left, right in zip(first, second)):
        raise RuntimeError("Repeated used-constructions projection changed its object sequence.")
    output_indices = _identity_indices(registry, first)
    common = {
        "fresh_list_each_access": first is not second,
        "output_labels": [labels[index] for index in output_indices],
        "output_registry_indices": output_indices,
        "result_type": type(first).__name__,
        "selected_objects_are_registry_objects": all(first[index] is registry[item] for index, item in enumerate(output_indices)),
        "source_lists_unchanged": model.zone is zones and _surface_source_is_unchanged(zones, originals),
    }
    if identifier == EXPECTED_CASE_IDS[4]:
        kinds = {
            modules.construction.AirBoundary: "air-boundary",
            modules.construction.NoMassConstruction: "no-mass",
        }
        return {
            **common,
            "construction_registry": [],
            "input_filtered_labels": [item.name for item in construction_inputs],
            "input_kinds": [kinds[type(item)] for item in construction_inputs],
        }
    construction_registry = [
        _hash_entry(labels[index], item.name, str(hash(item.name)))
        for index, item in enumerate(registry)
    ]
    input_indices = _identity_indices(registry, construction_inputs)
    result = {**common, "construction_registry": construction_registry, "input_registry_indices": input_indices}
    if identifier == EXPECTED_CASE_IDS[3]:
        result["equality"] = {
            "first_equals_later": registry[0] == registry[1],
            "first_equals_same_name_unequal": registry[0] == registry[2],
        }
    return result


def _execute_used_layers(identifier: str, modules: SimpleNamespace) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[6]:
        air_a = modules.construction.AirBoundary("air-a")
        air_b = modules.construction.AirBoundary("air-b")
        no_mass = modules.construction.NoMassConstruction("no-mass", 0.5)
        constructions = [air_a, no_mass, air_b]
        construction_registry: list[Any] = []
        construction_labels = ["air-a", "no-mass", "air-b"]
        layer_registry: list[Any] = []
        layer_labels: list[str] = []
    elif identifier == EXPECTED_CASE_IDS[7]:
        material = _material(modules)
        layer_registry = [
            _layer(modules, "Core-A", 0.1, material),
            _layer(modules, "Core-B", 0.1, material),
            _layer(modules, "Core-A", 0.2, material),
            _layer(modules, "Core-A", 0.1, material),
        ]
        layer_labels = ["base", "equal-different-name", "same-name-different-thickness", "exact-duplicate"]
        construction_registry = [_construction(modules, "Envelope", layer_registry)]
        construction_labels = ["envelope"]
        constructions = list(construction_registry)
    elif identifier == EXPECTED_CASE_IDS[8]:
        material = _material(modules)
        layer_registry = [
            _layer(modules, name, 0.01 * (index + 1), material)
            for index, name in enumerate(ORDER_NAMES)
        ]
        layer_labels = [f"l{index}" for index in range(9)]
        construction_registry = [
            _construction(modules, "Zulu", layer_registry[0:3]),
            _construction(modules, "Alpha", layer_registry[3:6]),
            _construction(modules, "Dragon", layer_registry[6:9]),
        ]
        construction_labels = ["construction-zulu", "construction-alpha", "construction-dragon"]
        constructions = list(construction_registry)
    else:
        raise RuntimeError(f"Unknown used-layers case: {identifier}")
    surfaces = [_surface(modules, f"surface-{index}", item, index * 2) for index, item in enumerate(constructions)]
    zones = [_zone(modules, "zone", surfaces)]
    originals = [tuple(zone.surface) for zone in zones]
    model = _model(modules, zones)
    first = model.used_layers
    second = model.used_layers
    if len(first) != len(second) or any(left is not right for left, right in zip(first, second)):
        raise RuntimeError("Repeated used-layers projection changed its object sequence.")
    output_indices = _identity_indices(layer_registry, first)
    common = {
        "fresh_list_each_access": first is not second,
        "output_labels": [layer_labels[index] for index in output_indices],
        "output_layer_indices": output_indices,
        "result_type": type(first).__name__,
        "selected_objects_are_registry_objects": all(first[index] is layer_registry[item] for index, item in enumerate(output_indices)),
        "source_lists_unchanged": model.zone is zones and _surface_source_is_unchanged(zones, originals),
    }
    if identifier == EXPECTED_CASE_IDS[6]:
        return {
            **common,
            "construction_input_kinds": ["air-boundary", "no-mass", "air-boundary"],
            "construction_input_labels": construction_labels,
            "layer_registry": [],
        }
    used_constructions = model.used_constructions
    used_construction_indices = _identity_indices(construction_registry, used_constructions)
    flattened = [layer for construction in used_constructions for layer in construction.layers]
    flattened_indices = _identity_indices(layer_registry, flattened)
    layer_descriptors = [
        _hash_entry(layer_labels[index], item.name, str(hash(item.name)))
        for index, item in enumerate(layer_registry)
    ]
    result = {
        **common,
        "layer_registry": layer_descriptors,
        "python_flattened_layer_indices": flattened_indices,
        "python_used_construction_indices": used_construction_indices,
    }
    if identifier == EXPECTED_CASE_IDS[7]:
        result["equality"] = {
            "base_equals_equal_different_name": layer_registry[0] == layer_registry[1],
            "base_equals_exact_duplicate": layer_registry[0] == layer_registry[3],
            "base_equals_same_name_different_thickness": layer_registry[0] == layer_registry[2],
        }
    else:
        result["construction_input_indices"] = list(range(3))
        result["construction_registry"] = [
            _hash_entry(construction_labels[index], item.name, str(hash(item.name)))
            for index, item in enumerate(construction_registry)
        ]
    return result


def _execute_used_profiles(identifier: str, modules: SimpleNamespace) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[9]:
        labels = ["alpha-first", "lower-alpha", "korean", "dragon", "alpha-last"]
        names = ["Alpha", "alpha", "\ud55c\uae00", "\U0001f409", "Alpha"]
    elif identifier == EXPECTED_CASE_IDS[10]:
        labels = ["team-first", "aux-first", "team-last", "core-only", "aux-last"]
        names = ["Team", "Aux", "Team", "Core", "Aux"]
    elif identifier == EXPECTED_CASE_IDS[11]:
        labels, names = [], []
    else:
        raise RuntimeError(f"Unknown used-profiles case: {identifier}")
    registry = [modules.profile.Profile(name) for name in names]
    zones = [_zone(modules, f"zone-{index}", [], profile) for index, profile in enumerate(registry)]
    originals = tuple(zones)
    model = _model(modules, zones)
    first = model.used_profiles
    second = model.used_profiles
    if len(first) != len(second) or any(left is not right for left, right in zip(first, second)):
        raise RuntimeError("Repeated used-profiles projection changed its object sequence.")
    output_indices = _identity_indices(registry, first)
    first_seen: list[str] = []
    for name in names:
        if name not in first_seen:
            first_seen.append(name)
    return {
        "first_seen_name_order": first_seen,
        "fresh_list_each_access": first is not second,
        "input_names": names,
        "output_labels": [labels[index] for index in output_indices],
        "output_profile_indices": output_indices,
        "profile_registry_labels": labels,
        "result_type": type(first).__name__,
        "selected_objects_are_registry_objects": all(first[index] is registry[item] for index, item in enumerate(output_indices)),
        "source_lists_unchanged": model.zone is zones and len(zones) == len(originals) and all(actual is expected for actual, expected in zip(zones, originals)),
    }


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _expected_symbol_descriptors() -> list[dict[str, Any]]:
    return [
        {**EXPECTED_SYMBOL_RECEIPTS[symbol], "path": _symbol_path(symbol), "symbol": symbol}
        for symbol in TARGET_SYMBOLS
    ]


def _expected_files() -> list[dict[str, Any]]:
    return [
        {"ast_hash": source["ast_sha256"], "content_hash": source["source_sha256"], "path": source["path"]}
        for source in SOURCE_SPECS
    ]


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "sources": [
            {"ast_sha256": source["ast_sha256"], "path": source["path"], "source_sha256": source["source_sha256"]}
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
            symbol: "exception" if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS else "equivalent"
            for symbol in TARGET_SYMBOLS
        },
        "hash_encoding": "signed-int64-decimal-string",
        "identity_encoding": "logical-label-and-registry-index-only-no-id-or-address",
        "native_order": "stable-first-use-order-for-declared-set-order-adaptations",
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
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


def build_oracle(inventory: dict[str, Any], commit: str, source_root: Path | None = None) -> dict[str, Any]:
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
    }:
        raise SystemExit("The aggregate projection inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    definitions = case_definitions()
    with _pinned_modules(imported_root) as modules:
        cases: list[dict[str, Any]] = []
        for definition in definitions:
            identifier = definition["id"]
            executor = definition["executor"]
            if executor == "energy-model-surfaces":
                facts = _execute_surfaces(identifier, modules)
            elif executor == "energy-model-used-constructions":
                facts = _execute_used_constructions(identifier, modules)
            elif executor == "energy-model-used-layers":
                facts = _execute_used_layers(identifier, modules)
            elif executor == "energy-model-used-profiles":
                facts = _execute_used_profiles(identifier, modules)
            else:
                raise SystemExit("Unknown projection executor: " + executor)
            if facts != expected_facts(identifier):
                raise SystemExit(
                    "Pinned Python projection semantics drifted: " + identifier
                    + "\nexpected=" + strict_json_dumps(expected_facts(identifier), indent=2)
                    + "\nactual=" + strict_json_dumps(facts, indent=2)
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
            "sources": [
                {"ast_sha256": source["ast_sha256"], "path": source["path"], "source_sha256": sha256_file(_source_file(imported_root, source))}
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
    _require_keys(value, {"cases", "cases_sha256", "consumer_contract", "runtime", "schema", "symbols", "upstream"}, "root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("Projection schema drifted.")
    cases = value["cases"]
    if not isinstance(cases, list) or value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Projection cases hash drifted.")
    _validate_safe_tree(value)
    definitions = case_definitions()
    if len(cases) != EXPECTED_CASE_COUNT or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Projection case order/count drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS) or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Pinned projection case IDs are not sorted and unique.")
    if Counter(item["symbol"] for item in definitions) != Counter({symbol: 3 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Projection cases are not three per symbol.")
    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Projection case contract drifted: {case['id']}")
        if "expected_dotnet" in case:
            _require_keys(case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned" or case["python"]["facts"] != expected_facts(case["id"]):
            raise RuntimeError(f"Projection semantics drifted: {case['id']}")
    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Projection consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Projection runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Projection upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Projection symbol receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the projection oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(strict_json_dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote dragon model projections oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

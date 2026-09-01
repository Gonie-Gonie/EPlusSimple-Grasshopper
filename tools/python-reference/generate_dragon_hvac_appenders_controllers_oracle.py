"""Generate pinned dragon HVAC appender/controller behavior observations.

The corpus executes exactly 24 declarations from the legacy dragon HVAC
post-processing families.  SupplyGroup.to_idf_object at inventory index 796
is retained only as an immutable support receipt owned by the supply-core
oracle.  All imports run from two byte-identical copies below the repository
temp tree, and the Python oracle never executes a native or EnergyPlus
process.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import hashlib
import importlib
import importlib.metadata
import importlib.util
import inspect
import json
import math
import os
from pathlib import Path
import shutil
import sys
import tempfile
from types import SimpleNamespace
from typing import Any, Callable, Iterator


SCHEMA = "dragons.python-reference.dragon-hvac-appenders-controllers.v1"
SOURCE_PATH = "src/idragon/dragon/hvac.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_067
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_BYTES = 137_833
EXPECTED_SOURCE_SHA256 = (
    "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"
)

SUPPORT_GENERATOR_PATH = Path(__file__).resolve().with_name(
    "generate_dragon_hvac_supply_core_oracle.py"
)
EXPECTED_SUPPORT_GENERATOR_BYTES = 65_859
EXPECTED_SUPPORT_GENERATOR_SHA256 = (
    "sha256:7ce1af80729c2f2aa333016ba95db3963b25db24e1b23d2c89f49ea2694590e2"
)
SUPPORT_FIXTURE_RELATIVE_PATH = (
    "fixtures/reference/python-0.7.0/dragon-hvac-supply-core-oracle.json"
)
EXPECTED_SUPPORT_FIXTURE_BYTES = 215_230
EXPECTED_SUPPORT_FIXTURE_SHA256 = (
    "sha256:657b53b768c90a2915ca10c781ff63ab5a21323bb09f534d4d5da3178fe99194"
)
EXPECTED_SUPPORT_CASES_SHA256 = (
    "sha256:29eacb2d29f528353302d1afd8e3ef646d7d35886237bb4a3fa494039a4ec36f"
)


def _raw_file_sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def _load_support() -> Any:
    if (
        not SUPPORT_GENERATOR_PATH.is_file()
        or SUPPORT_GENERATOR_PATH.stat().st_size != EXPECTED_SUPPORT_GENERATOR_BYTES
        or _raw_file_sha256(SUPPORT_GENERATOR_PATH)
        != EXPECTED_SUPPORT_GENERATOR_SHA256
    ):
        raise RuntimeError("Pinned dragon HVAC supply-core generator drifted.")
    specification = importlib.util.spec_from_file_location(
        "_dragons_dragon_hvac_appenders_support",
        SUPPORT_GENERATOR_PATH,
    )
    if specification is None or specification.loader is None:
        raise RuntimeError("Cannot load the pinned supply-core support generator.")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.SOURCE_PATH != SOURCE_PATH
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
        or module.EXPECTED_SOURCE_AST_SHA256 != EXPECTED_SOURCE_AST_SHA256
    ):
        raise RuntimeError("Pinned supply-core support identity drifted.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
SOURCE_SPECS = SUPPORT.BASE.SOURCE_SPECS
EXPECTED_DEPENDENCIES = dict(SUPPORT.EXPECTED_DEPENDENCIES)
REQUIRED_PYTHON = SUPPORT.REQUIRED_PYTHON
REQUIRED_HASH_ALGORITHM = SUPPORT.REQUIRED_HASH_ALGORITHM
REQUIRED_HASH_WIDTH_BITS = SUPPORT.REQUIRED_HASH_WIDTH_BITS


TARGET_IDENTITIES = (
    (686, "DemandBranchAppender"),
    (687, "DemandBranchAppender.append_to_branchlist"),
    (688, "DemandBranchAppender.append_to_mixer"),
    (689, "DemandBranchAppender.append_to_spliter"),
    (690, "DemandBranchAppender.count_current_branches_branchlist"),
    (691, "DemandBranchAppender.count_current_branches_connector"),
    (692, "DemandBranchAppender.run"),
    (717, "EquipmentListAppender"),
    (718, "EquipmentListAppender.count_current_equipments"),
    (719, "EquipmentListAppender.run"),
    (774, "SequentialLoadFractionController"),
    (775, "SequentialLoadFractionController.find_target_equipment_number"),
    (776, "SequentialLoadFractionController.run"),
    (804, "SupplySystemToIdfPostProcessor"),
    (805, "SupplySystemToIdfPostProcessor.__init__"),
    (806, "SupplySystemToIdfPostProcessor.run"),
    (807, "SupplySystemToIdfPostProcessor.source"),
    (808, "ZoneAirNodeAppender"),
    (809, "ZoneAirNodeAppender.count_current_nodes"),
    (810, "ZoneAirNodeAppender.ensure_nodelist_existence"),
    (811, "ZoneAirNodeAppender.run"),
    (812, "ZoneTerminalUnitAppender"),
    (813, "ZoneTerminalUnitAppender.count_current_units"),
    (814, "ZoneTerminalUnitAppender.run"),
)
TARGET_INDICES = tuple(index for index, _ in TARGET_IDENTITIES)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_IDENTITIES)
RESOLVED_SUPPORT_IDENTITIES = ((796, "SupplyGroup.to_idf_object"),)
RESOLVED_SUPPORT_INDICES = tuple(index for index, _ in RESOLVED_SUPPORT_IDENTITIES)
SOURCE_INDICES = tuple(range(641, 815))
DEFERRED_INDICES = tuple(
    index
    for index in SOURCE_INDICES
    if index not in TARGET_INDICES and index not in RESOLVED_SUPPORT_INDICES
)
if (
    len(TARGET_INDICES) != 24
    or len(RESOLVED_SUPPORT_INDICES) != 1
    or len(DEFERRED_INDICES) != 149
    or sorted((*TARGET_INDICES, *RESOLVED_SUPPORT_INDICES, *DEFERRED_INDICES))
    != list(SOURCE_INDICES)
):
    raise RuntimeError("Dragon HVAC appender/controller source partition drifted.")

EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:5228c06e02e371e4da5106bb10ba5e2159bd38b452ecdb2be459245c318f2495"
)
EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256 = (
    "sha256:88586a379f20f459fe1500bdc3ec4843aa161e11ac2e4426eeed81754f59c052"
)
EXPECTED_DEFERRED_RECEIPTS_SHA256 = (
    "sha256:2172f41f390f28cc737f78b6e476876c04fda668a64901778bcaf2199393b62e"
)
EXPECTED_FULL_SOURCE_RECEIPTS_SHA256 = (
    "sha256:f5db7f1a79890387192db20619e055691700f48bfbe368efeffbe37b695593e7"
)

CLASSIFICATIONS = {symbol: "exception" for symbol in TARGET_SYMBOLS}
ADAPTATIONS = {
    symbol: f"public-aggregate-hvac-postprocessing-{index}"
    for index, symbol in TARGET_IDENTITIES
}
PUBLIC_NATIVE_ROUTE = (
    "Dragons.InvisibleDragon.Hvac.SupplyGroup -> "
    "Dragons.InvisibleDragon.Hvac.ZoneHvacAssignment -> "
    "Dragons.InvisibleDragon.Model.EnergyModel -> "
    "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument("
    "IddSchema?, EnergyModelIdfOptions?) -> "
    "Dragons.InvisibleDragon.Idf.IdfDocument"
)
NATIVE_ROUTES = {symbol: PUBLIC_NATIVE_ROUTE for symbol in TARGET_SYMBOLS}

PREFIX = "dragon-hvac-appenders-controllers."


def _owned(owner: str) -> tuple[str, ...]:
    return tuple(
        symbol
        for symbol in TARGET_SYMBOLS
        if symbol == owner or symbol.startswith(owner + ".")
    )


CASE_SPECS = (
    ("A01", "demand-branch-appender", "demand-branch", _owned("DemandBranchAppender")),
    ("B01", "equipment-list-appender", "equipment-list", _owned("EquipmentListAppender")),
    (
        "C01",
        "sequential-load-fraction-controller",
        "sequential-controller",
        _owned("SequentialLoadFractionController"),
    ),
    (
        "D01",
        "supply-system-postprocessor",
        "postprocessor-base",
        _owned("SupplySystemToIdfPostProcessor"),
    ),
    ("E01", "zone-air-node-appender", "zone-air-node", _owned("ZoneAirNodeAppender")),
    (
        "F01",
        "zone-terminal-unit-appender",
        "zone-terminal-unit",
        _owned("ZoneTerminalUnitAppender"),
    ),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 6

EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:f44e1bfd639b1c59739524d9d795d6fba96336affacb4d7fa20104b0c8a2c1d5"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:93cfad21e009eac906a4443998ad214eec82e2136ada5b7cea7888ababf30143"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:87cf389f96bf8041e9dca0b22291a465aff5715e209d190a175b03a70cbf7d65"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:855517efc02d77240c56e866a6166ac4c837a9f9ba33227dca048fbd65b90dd7"
)
EXPECTED_FACT_SHA256 = {
    "dragon-hvac-appenders-controllers.demand-branch-appender": "sha256:ad7d55e8192bef04c3f6509932e8760791f0bf8cc5bd67a81de7a1d80638ab53",
    "dragon-hvac-appenders-controllers.equipment-list-appender": "sha256:6b292e7f7e56bb7a01ae1f5c43f16bad2bbd4f0c4592272546e5fa3e907e1f83",
    "dragon-hvac-appenders-controllers.sequential-load-fraction-controller": "sha256:d72589acfc903f580d3d4fb0f32942cc3e7178b5409e79e829b65c9e72b8f16c",
    "dragon-hvac-appenders-controllers.supply-system-postprocessor": "sha256:200eee5adb57d9dbaac68ccff8b4cf34319881d280bbbd5ac50baee628947a90",
    "dragon-hvac-appenders-controllers.zone-air-node-appender": "sha256:54ee0a2d373e153cf6a7ca02a361841b34e273999ba3cf0be2e97560b3496337",
    "dragon-hvac-appenders-controllers.zone-terminal-unit-appender": "sha256:a77be5dbc43bf4b51ba33fd30573a8ec478281857ec5593fb78c68324436dddb",
}
EXPECTED_CASE_SHA256 = {
    "dragon-hvac-appenders-controllers.demand-branch-appender": "sha256:ed597ff707a9e05f6e13272a2046862ddd012e9cc3e4153b88911e74145dfc03",
    "dragon-hvac-appenders-controllers.equipment-list-appender": "sha256:be089ecb06d8b1831af4ccc2f1f47fabc14cbaec81a98418e8cd9253c20999a9",
    "dragon-hvac-appenders-controllers.sequential-load-fraction-controller": "sha256:6db87a69044821b11b27016abd09eb7a0dffdc2764d66e71ce8a98cef2ef2fda",
    "dragon-hvac-appenders-controllers.supply-system-postprocessor": "sha256:a9532aee0ed8dcee59d6c507a16bddde1e8e02a6e83e7d27a11a9e3831cf5ee8",
    "dragon-hvac-appenders-controllers.zone-air-node-appender": "sha256:01734b8966710d9079ca632b73b357feafde8b4ca4a7bc41d6e4171f052dac93",
    "dragon-hvac-appenders-controllers.zone-terminal-unit-appender": "sha256:0ef5b24143176c6dbc201a4477fe79c0008b2ba48277c179ae6f1ed9c6450e58",
}
EXPECTED_CASES_SHA256 = (
    "sha256:2282854918bee238667f1307ecbdf21fa79ff7ceb305810622e6827afec7dd3d"
)

NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 7_561,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs",
        "sha256": "sha256:fcbe9c38cacade8002d121b0834a4441560086052571dd654f3c185a0c897249",
    },
    {
        "bytes": 18_249,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SupplySystems.cs",
        "sha256": "sha256:bf93e1c6889f7d371fff983caad1b3c90d4cbc6113bbb5d9a7a783740af1bb46",
    },
    {
        "bytes": 21_985,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModel.cs",
        "sha256": "sha256:60357af37bea1f6e7dd0640254a30761ed4097d53751183e5902c2efa62a0f28",
    },
    {
        "bytes": 50_723,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs",
        "sha256": "sha256:155981bef61ce31d155926b2c68dca3f5e6ea7f7db969276e5ea013a994ba2d4",
    },
    {
        "bytes": 13_173,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs",
        "sha256": "sha256:0d16e28d37136a3aa0015759ead7ee324cfed08cff1a3269326d4af144518048",
    },
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json_without_duplicates_text(text: str) -> dict[str, Any]:
    value = json.loads(text, object_pairs_hook=_reject_duplicates)
    if not isinstance(value, dict):
        raise ValueError("Expected a JSON object root.")
    return value


def load_json_without_duplicates(path: Path) -> dict[str, Any]:
    return load_json_without_duplicates_text(path.read_text(encoding="utf-8"))


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    commit = upstream_commit.lower()
    if commit != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    if path.stat().st_size != EXPECTED_INVENTORY_BYTES:
        raise SystemExit("The public-symbol inventory byte length drifted.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash drifted.")
    value = load_json_without_duplicates(path)
    if set(value) != {
        "content_sha256",
        "files",
        "schema",
        "scope_sha256",
        "summary",
        "symbols",
        "upstream_commit",
    }:
        raise SystemExit("The public-symbol inventory root contract drifted.")
    aggregate = canonical_sha256(
        {
            "files": value["files"],
            "scope_sha256": value["scope_sha256"],
            "symbols": value["symbols"],
            "upstream_commit": value["upstream_commit"],
        }
    )
    if (
        value["schema"] != "dragons.upstream-public-symbol-inventory.v2"
        or value["upstream_commit"].lower() != commit
        or value["content_sha256"] != aggregate
        or aggregate != EXPECTED_INVENTORY_SHA256
    ):
        raise SystemExit("The public-symbol inventory identity drifted.")
    source_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    if [item for item in value["files"] if item["path"] == SOURCE_PATH] != [source_file]:
        raise SystemExit("The dragon HVAC source receipt drifted.")
    source_receipts = [
        {"inventory_index": index, **item}
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    if [item["inventory_index"] for item in source_receipts] != list(SOURCE_INDICES):
        raise SystemExit("The dragon HVAC source declaration range drifted.")
    by_index = {item["inventory_index"]: item for item in source_receipts}
    targets = [by_index[index] for index in TARGET_INDICES]
    support = [by_index[index] for index in RESOLVED_SUPPORT_INDICES]
    deferred = [by_index[index] for index in DEFERRED_INDICES]
    if [(item["inventory_index"], item["symbol"]) for item in targets] != list(TARGET_IDENTITIES):
        raise SystemExit("Appender/controller target identities drifted.")
    if [(item["inventory_index"], item["symbol"]) for item in support] != list(RESOLVED_SUPPORT_IDENTITIES):
        raise SystemExit("Appender/controller support identity drifted.")
    hashes = {
        "deferred": canonical_sha256(deferred),
        "full": canonical_sha256(source_receipts),
        "support": canonical_sha256(support),
        "targets": canonical_sha256(targets),
    }
    if hashes != {
        "deferred": EXPECTED_DEFERRED_RECEIPTS_SHA256,
        "full": EXPECTED_FULL_SOURCE_RECEIPTS_SHA256,
        "support": EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256,
        "targets": EXPECTED_TARGET_RECEIPTS_SHA256,
    }:
        raise SystemExit("Appender/controller inventory receipt partition drifted.")
    return {
        "content_sha256": aggregate,
        "deferred_receipts_sha256": hashes["deferred"],
        "full_source_receipts_sha256": hashes["full"],
        "raw": value,
        "resolved_support_receipts": support,
        "symbols": [_descriptor(item) for item in targets],
        "target_receipts": targets,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "code": code,
            "id": PREFIX + slug,
            "subfamily": subfamily,
            "target_symbols": list(symbols),
        }
        for code, slug, subfamily, symbols in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Appender/controller case order drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Appender/controller case IDs are not sorted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Appender/controller case target partition drifted.")
    return definitions


def _encode(value: Any) -> Any:
    if value is None:
        return {"kind": "none"}
    if type(value) is bool:
        return {"kind": "bool", "value": value}
    if type(value) is int:
        return {"kind": "int", "value": str(value)}
    if type(value) is float:
        if math.isnan(value):
            return {"kind": "special-float", "token": "nan"}
        if math.isinf(value):
            return {
                "kind": "special-float",
                "token": "positive-infinity" if value > 0 else "negative-infinity",
            }
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    if type(value) is str:
        return {"kind": "str", "value": value}
    raise RuntimeError(f"Unsupported observed scalar: {type(value).__name__}")


def _attempt(function: Callable[[], Any]) -> dict[str, Any]:
    try:
        result = function()
    except Exception as error:
        return {
            "args": [str(argument) for argument in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned", "value": _encode(result)}


def _class_shape(value: type[Any]) -> dict[str, Any]:
    return {
        "abstract": inspect.isabstract(value),
        "abstract_methods": sorted(getattr(value, "__abstractmethods__", ())),
        "bases": [base.__name__ for base in value.__bases__],
        "mro": [item.__name__ for item in value.__mro__],
        "signature": str(inspect.signature(value)),
    }


class _StubObject:
    def __init__(
        self,
        object_type: str,
        fields: dict[str, Any] | None = None,
        *,
        events: list[dict[str, Any]] | None = None,
        label: str | None = None,
    ) -> None:
        self.object_type = object_type
        self.fields = dict(fields or {})
        self.events = events
        self.label = label or self.fields.get("Name") or object_type

    def __getitem__(self, key: str) -> Any:
        return self.fields.get(key)

    def __setitem__(self, key: str, value: Any) -> None:
        self.fields[key] = value
        if self.events is not None:
            self.events.append(
                {
                    "event": "field.set",
                    "field": key,
                    "object": self.label,
                    "value": _encode(value),
                }
            )

    def items(self) -> Any:
        return self.fields.items()

    def update(self, values: dict[str, Any]) -> None:
        for key, value in values.items():
            self[key] = value


class _StubCollection:
    def __init__(
        self,
        object_type: str,
        objects: list[_StubObject] | None,
        events: list[dict[str, Any]],
    ) -> None:
        self.object_type = object_type
        self.objects = list(objects or [])
        self.events = events
        for item in self.objects:
            item.events = events

    @property
    def names(self) -> list[Any]:
        return [item["Name"] for item in self.objects]

    def __getitem__(self, selector: Any) -> Any:
        if isinstance(selector, str):
            for item in self.objects:
                if item["Name"] == selector:
                    return item
            raise KeyError(selector)
        if callable(selector):
            return [item for item in self.objects if selector(item)]
        return self.objects[selector]

    def append(self, value: Any) -> None:
        if isinstance(value, _StubObject):
            item = value
            item.events = self.events
        elif isinstance(value, (list, tuple)):
            item = _StubObject(
                self.object_type,
                {"Name": value[0] if value else None},
                events=self.events,
            )
        else:
            raise TypeError(f"Unsupported stub append value: {type(value).__name__}")
        self.objects.append(item)
        self.events.append(
            {
                "event": "collection.append",
                "name": _encode(item["Name"]),
                "object_type": self.object_type,
            }
        )


class _StubIdf:
    def __init__(self, families: dict[str, list[_StubObject]] | None = None) -> None:
        self.events: list[dict[str, Any]] = []
        self.collections: dict[str, _StubCollection] = {}
        for object_type, objects in (families or {}).items():
            self.collections[object_type] = _StubCollection(
                object_type, objects, self.events
            )
        self.appended: list[_StubObject] = []

    def __getitem__(self, object_type: str) -> _StubCollection:
        if object_type not in self.collections:
            self.collections[object_type] = _StubCollection(
                object_type, [], self.events
            )
        return self.collections[object_type]

    def append(self, value: Any) -> None:
        values = value if isinstance(value, (list, tuple)) else [value]
        names: list[Any] = []
        for item in values:
            if not isinstance(item, _StubObject):
                raise TypeError("Stub IDF accepts only stub IDF objects.")
            self.appended.append(item)
            names.append(item["Name"])
        self.events.append(
            {
                "event": "idf.append",
                "names": [_encode(name) for name in names],
            }
        )


def _object_snapshot(value: _StubObject) -> dict[str, Any]:
    return {
        "fields": [
            {"name": key, "value": _encode(item)}
            for key, item in value.fields.items()
        ],
        "object_type": value.object_type,
    }


def _collection_snapshot(idf: _StubIdf, object_type: str) -> list[dict[str, Any]]:
    return [_object_snapshot(item) for item in idf[object_type].objects]


class _ZoneStub:
    def __init__(self, name: str = "Control Zone") -> None:
        self.name = name
        self.idf_equipmentlistname = f"EquipmentList_for_{name}"
        self.idf_airinletnodelistname = f"AirInletNodeList_for_{name}"
        self.idf_airexhaustnodelistname = f"AirExhaustNodeList_for_{name}"


class _SourceStub:
    def __init__(self, name: str = "Control Source") -> None:
        self.name = name
        self.idf_demandsplittername = f"DemandSplitter_for_{name}"
        self.idf_demandmixername = f"DemandMixer_for_{name}"
        self.idf_demandbranchlistname = f"DemandBranchList_for_{name}"
        self.idf_terminalunitlistname = f"TerminalUnitList_for_{name}"


class _SupplyStub:
    def __init__(
        self,
        label: str,
        source: _SourceStub | None,
        *,
        heatable: bool = True,
        coolable: bool = True,
    ) -> None:
        self.label = label
        self.name = label
        self.source = source
        self.heatable = heatable
        self.coolable = coolable
        self.idf_objtypename = f"Stub:Supply:{label}"

    def idf_get_objname(self, zone: _ZoneStub) -> str:
        return f"{self.label}_for_{zone.name}"

    def idf_get_demandbranchname(self, zone: _ZoneStub) -> str:
        return f"DemandBranch_{self.label}_for_{zone.name}"

    def idf_get_airinletnodename(self, zone: _ZoneStub) -> str:
        return f"{self.idf_get_objname(zone)} Air InletNode"

    def idf_get_airoutletnodename(self, zone: _ZoneStub) -> str:
        return f"{self.idf_get_objname(zone)} Air OutletNode"


class _ScheduleStub:
    trace: list[dict[str, Any]] = []

    def __init__(self, name: str | None, value: float, expression: str) -> None:
        self.name = name
        self.value = float(value)
        self.expression = expression

    @classmethod
    def reset_trace(cls) -> None:
        cls.trace = []

    @classmethod
    def from_constant(cls, name: str | None, value: Any) -> "_ScheduleStub":
        result = cls(name, float(value), f"constant({value!r})")
        cls.trace.append(
            {
                "event": "schedule.from_constant",
                "name": _encode(name),
                "value": _encode(result.value),
            }
        )
        return result

    def changetype(self, schedule_type: Any) -> "_ScheduleStub":
        type_name = getattr(schedule_type, "name", type(schedule_type).__name__)
        result = type(self)(
            self.name,
            self.value,
            f"changetype({self.expression},{type_name})",
        )
        type(self).trace.append(
            {
                "event": "schedule.changetype",
                "name": _encode(self.name),
                "schedule_type": type_name,
                "value": _encode(self.value),
            }
        )
        return result

    def _coerce(self, other: Any) -> tuple[float, str]:
        if isinstance(other, _ScheduleStub):
            return other.value, other.expression
        return float(other), repr(other)

    def __add__(self, other: Any) -> "_ScheduleStub":
        value, expression = self._coerce(other)
        return type(self)(None, self.value + value, f"({self.expression}+{expression})")

    def __radd__(self, other: Any) -> "_ScheduleStub":
        value, expression = self._coerce(other)
        return type(self)(None, value + self.value, f"({expression}+{self.expression})")

    def __sub__(self, other: Any) -> "_ScheduleStub":
        value, expression = self._coerce(other)
        return type(self)(None, self.value - value, f"({self.expression}-{expression})")

    def __mul__(self, other: Any) -> "_ScheduleStub":
        value, expression = self._coerce(other)
        return type(self)(None, self.value * value, f"({self.expression}*{expression})")

    def __rmul__(self, other: Any) -> "_ScheduleStub":
        return self.__mul__(other)

    def __rtruediv__(self, other: Any) -> "_ScheduleStub":
        value, expression = self._coerce(other)
        return type(self)(None, value / self.value, f"({expression}/{self.expression})")

    def to_idf_object(self) -> list[_StubObject]:
        type(self).trace.append(
            {
                "event": "schedule.to_idf_object",
                "expression": self.expression,
                "name": _encode(self.name),
                "value": _encode(self.value),
            }
        )
        return [
            _StubObject(
                "Schedule:Compact",
                {
                    "Name": self.name,
                    "Observed Expression": self.expression,
                    "Observed Value": self.value,
                },
            )
        ]


@contextmanager
def _patched_schedule(hvac: Any) -> Iterator[None]:
    original = hvac.Schedule
    hvac.Schedule = _ScheduleStub
    _ScheduleStub.reset_trace()
    try:
        yield
    finally:
        hvac.Schedule = original


def _equipment_list(
    zone: _ZoneStub,
    names: list[str],
    *,
    events: list[dict[str, Any]] | None = None,
) -> _StubObject:
    fields: dict[str, Any] = {"Name": zone.idf_equipmentlistname}
    for index, name in enumerate(names, start=1):
        fields[f"Zone Equipment {index} Name"] = name
    return _StubObject(
        "ZoneHVAC:EquipmentList",
        fields,
        events=events,
        label=zone.idf_equipmentlistname,
    )


def _observe_postprocessor(hvac: Any) -> dict[str, Any]:
    class Probe(hvac.SupplySystemToIdfPostProcessor):
        def run(self, idf: Any) -> None:
            self.last_idf = idf

    zone = _ZoneStub("Post Zone")
    first_source = _SourceStub("First")
    supply = _SupplyStub("Post Supply", first_source)
    value = Probe(supply, zone)
    before = value.source is first_source
    second_source = _SourceStub("Second")
    supply.source = second_source
    marker = object()
    run_result = value.run(marker)
    return {
        "abstract_run_body": _attempt(
            lambda: hvac.SupplySystemToIdfPostProcessor.__dict__["run"](
                object(), object()
            )
        ),
        "class_shape": _class_shape(hvac.SupplySystemToIdfPostProcessor),
        "constructor_final_marker": bool(
            getattr(
                hvac.SupplySystemToIdfPostProcessor.__dict__["__init__"],
                "__final__",
                False,
            )
        ),
        "constructor_signature": str(
            inspect.signature(hvac.SupplySystemToIdfPostProcessor.__init__)
        ),
        "direct_instantiation": _attempt(
            lambda: hvac.SupplySystemToIdfPostProcessor(supply, zone)
        ),
        "probe": {
            "class_shape": _class_shape(Probe),
            "run_argument_identity_preserved": value.last_idf is marker,
            "run_return": _encode(run_result),
            "source_dynamic_after_supply_mutation": value.source is second_source,
            "source_identity_before_mutation": before,
            "supply_alias_preserved": value.supply is supply,
            "zone_alias_preserved": value.zone is zone,
        },
        "run_signature": str(
            inspect.signature(hvac.SupplySystemToIdfPostProcessor.run)
        ),
        "source_signature": str(
            inspect.signature(
                hvac.SupplySystemToIdfPostProcessor.__dict__["source"].fget
            )
        ),
    }


def _demand_idf(source: _SourceStub) -> _StubIdf:
    return _StubIdf(
        {
            "BranchList": [
                _StubObject(
                    "BranchList",
                    {
                        "Name": source.idf_demandbranchlistname,
                        "Branch 1 Name": "Demand Inlet",
                        "Branch 2 Name": "Demand Bypass",
                        "Branch 3 Name": "Demand Outlet",
                    },
                )
            ],
            "Connector:Mixer": [
                _StubObject(
                    "Connector:Mixer",
                    {
                        "Name": source.idf_demandmixername,
                        "Outlet Branch Name": "Demand Outlet",
                        "Inlet Branch 1 Name": "Demand Bypass",
                    },
                )
            ],
            "Connector:Splitter": [
                _StubObject(
                    "Connector:Splitter",
                    {
                        "Name": source.idf_demandsplittername,
                        "Inlet Branch Name": "Demand Inlet",
                        "Outlet Branch 1 Name": "Demand Bypass",
                    },
                )
            ],
        }
    )


def _observe_demand_branch(hvac: Any) -> dict[str, Any]:
    source = _SourceStub("Demand Source")
    zone = _ZoneStub("Demand Zone")
    supply = _SupplyStub("Demand Supply", source)
    value = hvac.DemandBranchAppender(supply, zone)

    connector_zero = _StubObject(
        "Connector:Splitter", {"Name": "Zero", "Inlet Branch Name": "Inlet"}
    )
    connector_one = _StubObject(
        "Connector:Splitter",
        {
            "Name": "One",
            "Inlet Branch Name": "Inlet",
            "Outlet Branch 1 Name": "Branch",
            "Outlet Branch 2 Name": None,
        },
    )
    connector_extra = _StubObject(
        "Connector:Splitter",
        {
            "Name": "Extra",
            "Inlet Branch Name": "Inlet",
            "Outlet Branch 1 Name": "Branch",
            "Non Branch Metadata": "counted",
        },
    )
    branchlist = _StubObject(
        "BranchList",
        {
            "Name": "List",
            "Branch 1 Name": "One",
            "Branch 2 Name": "Two",
            "Branch 3 Name": None,
        },
    )

    success = _demand_idf(source)
    first_return = value.run(success)
    first_events = list(success.events)
    first_state = {
        family: _collection_snapshot(success, family)
        for family in ("Connector:Splitter", "Connector:Mixer", "BranchList")
    }
    second_return = value.run(success)
    second_events = success.events[len(first_events) :]
    second_state = {
        family: _collection_snapshot(success, family)
        for family in ("Connector:Splitter", "Connector:Mixer", "BranchList")
    }

    failure = _demand_idf(source)
    failure.collections["Connector:Mixer"].objects.clear()
    failure_result = _attempt(lambda: value.run(failure))

    standalone = _demand_idf(source)
    splitter_return = value.append_to_spliter(standalone)
    mixer_return = value.append_to_mixer(standalone)
    branch_return = value.append_to_branchlist(standalone)

    return {
        "class_shape": _class_shape(hvac.DemandBranchAppender),
        "count_probes": {
            "branchlist_two_nonnull": hvac.DemandBranchAppender.count_current_branches_branchlist(branchlist),
            "connector_extra_nonbranch_is_counted": hvac.DemandBranchAppender.count_current_branches_connector(connector_extra),
            "connector_one": hvac.DemandBranchAppender.count_current_branches_connector(connector_one),
            "connector_zero": hvac.DemandBranchAppender.count_current_branches_connector(connector_zero),
        },
        "failure_prefix_missing_mixer": {
            "events": failure.events,
            "outcome": failure_result,
            "splitter": _collection_snapshot(failure, "Connector:Splitter"),
            "branchlist_unchanged": _collection_snapshot(failure, "BranchList"),
        },
        "method_signatures": {
            name: str(inspect.signature(getattr(hvac.DemandBranchAppender, name)))
            for name in (
                "append_to_branchlist",
                "append_to_mixer",
                "append_to_spliter",
                "count_current_branches_branchlist",
                "count_current_branches_connector",
                "run",
            )
        },
        "run_and_rerun": {
            "first_events": first_events,
            "first_return": _encode(first_return),
            "first_state": first_state,
            "second_events": second_events,
            "second_return": _encode(second_return),
            "second_state": second_state,
        },
        "standalone_methods": {
            "events": standalone.events,
            "returns": [
                _encode(splitter_return),
                _encode(mixer_return),
                _encode(branch_return),
            ],
            "state": {
                family: _collection_snapshot(standalone, family)
                for family in ("Connector:Splitter", "Connector:Mixer", "BranchList")
            },
        },
    }


def _observe_equipment_list(hvac: Any) -> dict[str, Any]:
    source = _SourceStub("Equipment Source")
    zone = _ZoneStub("Equipment Zone")
    supply = _SupplyStub("Equipment Supply", source)
    value = hvac.EquipmentListAppender(supply, zone)

    empty = _equipment_list(zone, [])
    one = _equipment_list(zone, ["Existing"])
    hole = _equipment_list(zone, ["First", "Third"])
    hole.fields["Zone Equipment 2 Name"] = None
    hole.fields["Zone Equipment 3 Name"] = "Third"
    full_98 = _equipment_list(zone, [f"Equipment {index}" for index in range(1, 99)])
    full_99 = _equipment_list(zone, [f"Equipment {index}" for index in range(1, 100)])
    count_probes = {
        "empty": _encode(hvac.EquipmentListAppender.count_current_equipments(empty)),
        "first_hole_stops_scan": _encode(
            hvac.EquipmentListAppender.count_current_equipments(hole)
        ),
        "full_98": _encode(
            hvac.EquipmentListAppender.count_current_equipments(full_98)
        ),
        "full_99_falls_through": _encode(
            hvac.EquipmentListAppender.count_current_equipments(full_99)
        ),
        "one": _encode(hvac.EquipmentListAppender.count_current_equipments(one)),
    }

    success_idf = _StubIdf({"ZoneHVAC:EquipmentList": [one]})
    first_return = value.run(success_idf)
    first_events = list(success_idf.events)
    first_state = _object_snapshot(one)
    second_return = value.run(success_idf)
    second_events = success_idf.events[len(first_events) :]

    hole_idf = _StubIdf({"ZoneHVAC:EquipmentList": [hole]})
    hole_return = value.run(hole_idf)

    overflow_idf = _StubIdf({"ZoneHVAC:EquipmentList": [full_99]})
    overflow = _attempt(lambda: value.run(overflow_idf))
    missing_idf = _StubIdf()
    missing = _attempt(lambda: value.run(missing_idf))

    return {
        "class_shape": _class_shape(hvac.EquipmentListAppender),
        "count_probes": count_probes,
        "method_signatures": {
            "count": str(inspect.signature(hvac.EquipmentListAppender.count_current_equipments)),
            "run": str(inspect.signature(hvac.EquipmentListAppender.run)),
        },
        "missing_equipment_list": missing,
        "ninety_nine_limit": {
            "events": overflow_idf.events,
            "outcome": overflow,
            "state_unchanged": len(full_99.fields) == 100,
        },
        "run_first_hole_overwrites_slot": {
            "events": hole_idf.events,
            "return": _encode(hole_return),
            "state": _object_snapshot(hole),
        },
        "run_and_rerun": {
            "first_events": first_events,
            "first_return": _encode(first_return),
            "first_state": first_state,
            "second_events": second_events,
            "second_return": _encode(second_return),
            "second_state": _object_snapshot(one),
        },
    }


def _sequential_fields(value: _StubObject) -> list[dict[str, Any]]:
    return [
        {"name": key, "value": _encode(item)}
        for key, item in value.fields.items()
        if "Sequential" in key or key == "Name" or key.endswith(" Name")
    ]


def _controller_scenario(
    hvac: Any,
    systems: list[_SupplyStub],
    availabilities: list[_ScheduleStub | None],
    equipment_names: list[str],
    *,
    rerun: bool,
) -> dict[str, Any]:
    zone = _ZoneStub("Fraction Zone")
    equipment = _equipment_list(zone, equipment_names)
    idf = _StubIdf({"ZoneHVAC:EquipmentList": [equipment]})
    group = SimpleNamespace(
        systems=tuple(systems),
        availabilities=tuple(availabilities),
    )
    controller = hvac.SequentialLoadFractionController(group, zone)
    with _patched_schedule(hvac):
        first = _attempt(lambda: controller.run(idf))
        first_event_count = len(idf.events)
        first_append_count = len(idf.appended)
        first_fields = _sequential_fields(equipment)
        if rerun and first["outcome"] == "returned":
            second = _attempt(lambda: controller.run(idf))
        else:
            second = {"outcome": "not-run"}
        trace = list(_ScheduleStub.trace)
    return {
        "appended_schedules": [_object_snapshot(item) for item in idf.appended],
        "equipment_fields_after_first": first_fields,
        "equipment_fields_final": _sequential_fields(equipment),
        "first_append_count": first_append_count,
        "first_events": idf.events[:first_event_count],
        "first_outcome": first,
        "rerun_events": idf.events[first_event_count:],
        "rerun_outcome": second,
        "schedule_trace": trace,
    }


def _observe_sequential_controller(hvac: Any) -> dict[str, Any]:
    lookup_zone = _ZoneStub("Lookup Zone")
    lookup = _equipment_list(lookup_zone, ["Alpha", "Beta"])
    lookup.fields["Zone Equipment 3 Name"] = None
    overflow = _equipment_list(
        lookup_zone, [f"Equipment {index}" for index in range(1, 100)]
    )

    zero_system = _SupplyStub(
        "Idle", None, heatable=False, coolable=False
    )
    one_system = _SupplyStub(
        "Heat Only", None, heatable=True, coolable=False
    )
    multi_systems = [
        _SupplyStub("Dual", None, heatable=True, coolable=True),
        _SupplyStub("Heat", None, heatable=True, coolable=False),
        _SupplyStub("Cool", None, heatable=False, coolable=True),
    ]
    failure_systems = [
        _SupplyStub("Present", None, heatable=True, coolable=False),
        _SupplyStub("Missing", None, heatable=True, coolable=False),
    ]
    zone = _ZoneStub("Fraction Zone")

    return {
        "class_shape": _class_shape(hvac.SequentialLoadFractionController),
        "epsilon": _encode(1.0e-10),
        "find_target": {
            "found_second": hvac.SequentialLoadFractionController.find_target_equipment_number(lookup, "Beta"),
            "missing_at_first_empty": _attempt(
                lambda: hvac.SequentialLoadFractionController.find_target_equipment_number(lookup, "Gamma")
            ),
            "overflow_after_99": _attempt(
                lambda: hvac.SequentialLoadFractionController.find_target_equipment_number(overflow, "Not Present")
            ),
        },
        "method_signatures": {
            "find": str(inspect.signature(hvac.SequentialLoadFractionController.find_target_equipment_number)),
            "run": str(inspect.signature(hvac.SequentialLoadFractionController.run)),
        },
        "multi_active": _controller_scenario(
            hvac,
            multi_systems,
            [
                _ScheduleStub("Availability Dual", 0.25, "availability-dual"),
                _ScheduleStub("Availability Heat", 0.75, "availability-heat"),
                None,
            ],
            [system.idf_get_objname(zone) for system in multi_systems],
            rerun=True,
        ),
        "one_active": _controller_scenario(
            hvac,
            [one_system],
            [_ScheduleStub("Ignored Availability", 0.25, "ignored-availability")],
            [one_system.idf_get_objname(zone)],
            rerun=True,
        ),
        "partial_failure_after_schedule_append": _controller_scenario(
            hvac,
            failure_systems,
            [None, None],
            [failure_systems[0].idf_get_objname(zone)],
            rerun=False,
        ),
        "zero_active": _controller_scenario(
            hvac,
            [zero_system],
            [None],
            [zero_system.idf_get_objname(zone)],
            rerun=True,
        ),
    }


def _observe_zone_air_node(hvac: Any) -> dict[str, Any]:
    source = _SourceStub("Air Source")
    zone = _ZoneStub("Air Zone")
    supply = _SupplyStub("Air Supply", source)
    value = hvac.ZoneAirNodeAppender(supply, zone)

    connection = _StubObject(
        "ZoneHVAC:EquipmentConnections",
        {"Name": "Air Zone Connection", "Zone Name": zone.name},
    )
    absent = _StubIdf({"NodeList": [], "ZoneHVAC:EquipmentConnections": [connection]})
    first_return = value.run(absent)
    first_events = list(absent.events)
    first_state = {
        "connection": _object_snapshot(connection),
        "nodelists": _collection_snapshot(absent, "NodeList"),
    }
    second_return = value.run(absent)

    existing_connection = _StubObject(
        "ZoneHVAC:EquipmentConnections",
        {
            "Name": "Existing Connection",
            "Zone Name": zone.name,
            "Zone Air Inlet Node or NodeList Name": "Authored Inlet",
            "Zone Air Exhaust Node or NodeList Name": "Authored Exhaust",
        },
    )
    existing = _StubIdf(
        {
            "NodeList": [
                _StubObject(
                    "NodeList",
                    {"Name": zone.idf_airinletnodelistname, "Node 1 Name": "Existing Inlet"},
                ),
                _StubObject(
                    "NodeList",
                    {"Name": zone.idf_airexhaustnodelistname, "Node 1 Name": "Existing Exhaust"},
                ),
            ],
            "ZoneHVAC:EquipmentConnections": [existing_connection],
        }
    )
    existing_return = value.run(existing)

    missing_connection = _StubIdf({"NodeList": [], "ZoneHVAC:EquipmentConnections": []})
    missing = _attempt(lambda: value.run(missing_connection))

    count_zero = _StubObject("NodeList", {"Name": "Zero", "Node 1 Name": None})
    count_two = _StubObject(
        "NodeList",
        {"Name": "Two", "Node 1 Name": "One", "Node 2 Name": "Two"},
    )
    return {
        "class_shape": _class_shape(hvac.ZoneAirNodeAppender),
        "count_probes": {
            "two": hvac.ZoneAirNodeAppender.count_current_nodes(count_two),
            "zero_none_ignored": hvac.ZoneAirNodeAppender.count_current_nodes(count_zero),
        },
        "existing_lists": {
            "connection_unchanged": _object_snapshot(existing_connection),
            "events": existing.events,
            "nodelists": _collection_snapshot(existing, "NodeList"),
            "return": _encode(existing_return),
        },
        "method_signatures": {
            "count": str(inspect.signature(hvac.ZoneAirNodeAppender.count_current_nodes)),
            "ensure": str(inspect.signature(hvac.ZoneAirNodeAppender.ensure_nodelist_existence)),
            "run": str(inspect.signature(hvac.ZoneAirNodeAppender.run)),
        },
        "missing_connection_failure_prefix": {
            "events": missing_connection.events,
            "nodelists": _collection_snapshot(missing_connection, "NodeList"),
            "outcome": missing,
        },
        "missing_lists_run_and_rerun": {
            "first_events": first_events,
            "first_return": _encode(first_return),
            "first_state": first_state,
            "second_events": absent.events[len(first_events) :],
            "second_return": _encode(second_return),
            "second_state": {
                "connection": _object_snapshot(connection),
                "nodelists": _collection_snapshot(absent, "NodeList"),
            },
        },
    }


def _observe_zone_terminal_unit(hvac: Any) -> dict[str, Any]:
    source = _SourceStub("Terminal Source")
    zone = _ZoneStub("Terminal Zone")
    supply = _SupplyStub("Terminal Supply", source)
    value = hvac.ZoneTerminalUnitAppender(supply, zone)
    terminal_list = _StubObject(
        "ZoneTerminalUnitList",
        {
            "Zone Terminal Unit List Name": source.idf_terminalunitlistname,
            "Zone Terminal Unit Name 1": "Existing Terminal",
        },
        label=source.idf_terminalunitlistname,
    )
    initial_count = hvac.ZoneTerminalUnitAppender.count_current_units(terminal_list)
    idf = _StubIdf({"ZoneTerminalUnitList": [terminal_list]})
    first_return = value.run(idf)
    first_events = list(idf.events)
    first_state = _object_snapshot(terminal_list)
    second_return = value.run(idf)
    missing_idf = _StubIdf({"ZoneTerminalUnitList": []})
    missing = _attempt(lambda: value.run(missing_idf))
    zero = _StubObject(
        "ZoneTerminalUnitList",
        {"Zone Terminal Unit List Name": "Zero", "Zone Terminal Unit Name 1": None},
    )
    return {
        "class_shape": _class_shape(hvac.ZoneTerminalUnitAppender),
        "count_probes": {
            "existing_one": initial_count,
            "three_after_rerun": hvac.ZoneTerminalUnitAppender.count_current_units(terminal_list),
            "zero_none_ignored": hvac.ZoneTerminalUnitAppender.count_current_units(zero),
        },
        "method_signatures": {
            "count": str(inspect.signature(hvac.ZoneTerminalUnitAppender.count_current_units)),
            "run": str(inspect.signature(hvac.ZoneTerminalUnitAppender.run)),
        },
        "missing_list": {
            "events": missing_idf.events,
            "outcome": missing,
        },
        "run_and_rerun": {
            "first_events": first_events,
            "first_return": _encode(first_return),
            "first_state": first_state,
            "second_events": idf.events[len(first_events) :],
            "second_return": _encode(second_return),
            "second_state": _object_snapshot(terminal_list),
        },
    }


def _execute_cases(hvac: Any) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _observe_demand_branch(hvac),
        EXPECTED_CASE_IDS[1]: _observe_equipment_list(hvac),
        EXPECTED_CASE_IDS[2]: _observe_sequential_controller(hvac),
        EXPECTED_CASE_IDS[3]: _observe_postprocessor(hvac),
        EXPECTED_CASE_IDS[4]: _observe_zone_air_node(hvac),
        EXPECTED_CASE_IDS[5]: _observe_zone_terminal_unit(hvac),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("Appender/controller observation order drifted.")
    return observations


def _resolve_descriptor(hvac: Any, symbol: str) -> dict[str, Any]:
    owner_name, separator, member_name = symbol.partition(".")
    owner = getattr(hvac, owner_name)
    if not separator:
        return {
            "abstract": inspect.isabstract(owner),
            "kind": "class",
            "module": owner.__module__,
            "qualname": owner.__qualname__,
            "signature": str(inspect.signature(owner)),
        }
    descriptor = inspect.getattr_static(owner, member_name)
    if isinstance(descriptor, property):
        function = descriptor.fget
        if function is None:
            raise RuntimeError(f"Property getter is absent: {symbol}")
        return {
            "abstract": bool(getattr(function, "__isabstractmethod__", False)),
            "kind": "property",
            "qualname": function.__qualname__,
            "signature": str(inspect.signature(function)),
        }
    if isinstance(descriptor, staticmethod):
        function = descriptor.__func__
        return {
            "abstract": bool(getattr(function, "__isabstractmethod__", False)),
            "kind": "staticmethod",
            "qualname": function.__qualname__,
            "signature": str(inspect.signature(function)),
        }
    if callable(descriptor):
        return {
            "abstract": bool(getattr(descriptor, "__isabstractmethod__", False)),
            "kind": "function",
            "qualname": descriptor.__qualname__,
            "signature": str(inspect.signature(descriptor)),
        }
    raise RuntimeError(f"Unsupported runtime descriptor: {symbol}")


def _runtime_signatures(hvac: Any) -> dict[str, Any]:
    return {symbol: _resolve_descriptor(hvac, symbol) for symbol in TARGET_SYMBOLS}


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


@contextmanager
def _isolated_import(
    source_root: Path,
    work_root: Path,
    prefix: str,
) -> Iterator[SimpleNamespace]:
    source_root = source_root.resolve()
    for source in SOURCE_SPECS:
        path = _source_file(source_root, source)
        if not path.is_file() or sha256_file(path) != source["source_sha256"]:
            raise SystemExit(f"Pinned source input drifted: {source['path']}")
    work_root.mkdir(parents=True, exist_ok=True)
    saved_modules = {
        name: module
        for name, module in sys.modules.items()
        if name == "idragon" or name.startswith("idragon.")
    }
    with tempfile.TemporaryDirectory(prefix=prefix, dir=work_root) as temporary:
        imported_root = Path(temporary) / "src"
        shutil.copytree(source_root, imported_root)
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
            loaded = SUPPORT.BASE._audit_loaded_local_modules(imported_root)
            if not (
                hvac.IdfObject is imugi.IdfObject
                and model.SupplyGroup is hvac.SupplyGroup
                and model.Zone is shape.Zone
                and profile.IdfObject is imugi.IdfObject
                and common.Setting is model.Setting
                and constants.THERMAL is model.THERMAL
                and utils.validate_type is hvac.validate_type
            ):
                raise SystemExit("Pinned dragon module identities drifted.")
            yield SimpleNamespace(hvac=hvac, loaded_local_modules=loaded)
        finally:
            for name in list(sys.modules):
                if name == "idragon" or name.startswith("idragon."):
                    sys.modules.pop(name, None)
            sys.modules.update(saved_modules)
            try:
                sys.path.remove(str(imported_root))
            except ValueError:
                pass


def _find_pinned_source_root() -> Path:
    return SUPPORT.BASE._find_pinned_source_root()


def _support_receipt(resolved_receipts: list[dict[str, Any]]) -> dict[str, Any]:
    repository_root = Path(__file__).resolve().parents[2]
    fixture_path = repository_root / SUPPORT_FIXTURE_RELATIVE_PATH
    if (
        SUPPORT_GENERATOR_PATH.stat().st_size != EXPECTED_SUPPORT_GENERATOR_BYTES
        or sha256_file(SUPPORT_GENERATOR_PATH) != EXPECTED_SUPPORT_GENERATOR_SHA256
        or not fixture_path.is_file()
        or fixture_path.stat().st_size != EXPECTED_SUPPORT_FIXTURE_BYTES
        or sha256_file(fixture_path) != EXPECTED_SUPPORT_FIXTURE_SHA256
    ):
        raise SystemExit("Pinned supply-core supporting resources drifted.")
    fixture = load_json_without_duplicates(fixture_path)
    SUPPORT.validate_oracle(fixture)
    adjacent = fixture["upstream"]["adjacent_receipts"]
    indexed = [item for item in adjacent if item["inventory_index"] == 796]
    if (
        fixture["schema"] != SUPPORT.SCHEMA
        or fixture["cases_sha256"] != EXPECTED_SUPPORT_CASES_SHA256
        or len(fixture["cases"]) != 9
        or indexed != resolved_receipts
        or fixture["consumer_contract"]["closure"]["adjacent_existing_status"].get(
            "SupplyGroup.to_idf_object"
        )
        != "exception"
    ):
        raise SystemExit("Pinned supply-core support contract drifted.")
    return {
        "case_count": 9,
        "cases_sha256": EXPECTED_SUPPORT_CASES_SHA256,
        "fixture": {
            "bytes": EXPECTED_SUPPORT_FIXTURE_BYTES,
            "path": SUPPORT_FIXTURE_RELATIVE_PATH,
            "sha256": EXPECTED_SUPPORT_FIXTURE_SHA256,
        },
        "generator": {
            "bytes": EXPECTED_SUPPORT_GENERATOR_BYTES,
            "path": "tools/python-reference/generate_dragon_hvac_supply_core_oracle.py",
            "sha256": EXPECTED_SUPPORT_GENERATOR_SHA256,
        },
        "resolved_receipts": resolved_receipts,
        "resolved_receipts_sha256": EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256,
        "role": "immutable-index-796-supply-group-conversion-support-only",
        "schema": SUPPORT.SCHEMA,
        "target_promoted": False,
    }


def _native_review() -> dict[str, Any]:
    repository_root = Path(__file__).resolve().parents[2]
    for receipt in NATIVE_SOURCE_RECEIPTS:
        path = repository_root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise SystemExit(f"Reviewed native source drifted: {receipt['path']}")
    result = {
        "classification_sha256": canonical_sha256(CLASSIFICATIONS),
        "internal_generate_route_claimed": False,
        "internal_postprocessor_type_route_claimed": False,
        "public_production_route": PUBLIC_NATIVE_ROUTE,
        "public_production_routes_only": True,
        "python_executes_native_runtime": False,
        "routes_sha256": canonical_sha256(NATIVE_ROUTES),
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        "source_receipts_sha256": canonical_sha256(list(NATIVE_SOURCE_RECEIPTS)),
    }
    digest = canonical_sha256(result)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise SystemExit("Pinned appender/controller native review drifted.")
    return result


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


def _runtime_receipt() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "dependencies_sha256": canonical_sha256(EXPECTED_DEPENDENCIES),
        "implementation": "cpython",
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def _assertion_ids(receipts: list[dict[str, Any]]) -> dict[str, str]:
    return {
        item["symbol"]: (
            f"dragon-hvac-appenders-controllers-{item['inventory_index']}-"
            f"{item['symbol_hash'][7:15]}"
        )
        for item in receipts
    }


def _coverage_by_symbol() -> dict[str, str]:
    result: dict[str, str] = {}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol] = definition["id"]
    if set(result) != set(TARGET_SYMBOLS):
        raise RuntimeError("Appender/controller coverage drifted.")
    return result


def _expected_contract(
    receipts: list[dict[str, Any]],
    signatures: dict[str, Any],
) -> dict[str, Any]:
    assertions = _assertion_ids(receipts)
    expectations = {
        symbol: {
            "adaptation": ADAPTATIONS[symbol],
            "assertion_id": assertions[symbol],
            "classification": "exception",
            "native_route": NATIVE_ROUTES[symbol],
        }
        for symbol in TARGET_SYMBOLS
    }
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": assertions,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {"equivalent": 0, "exception": 24},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "deferred_count": len(DEFERRED_INDICES),
            "deferred_indices": list(DEFERRED_INDICES),
            "deferred_receipts_sha256": EXPECTED_DEFERRED_RECEIPTS_SHA256,
            "exact_disjoint_source_partition": True,
            "exact_one_case_target_partition": True,
            "full_hvac_source_partition": True,
            "full_source_receipts_sha256": EXPECTED_FULL_SOURCE_RECEIPTS_SHA256,
            "resolved_support_count": 1,
            "resolved_support_indices": list(RESOLVED_SUPPORT_INDICES),
            "resolved_support_receipts_sha256": EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256,
            "resolved_support_symbols": [
                symbol for _, symbol in RESOLVED_SUPPORT_IDENTITIES
            ],
            "source_declaration_count": len(SOURCE_INDICES),
            "target_count": len(TARGET_INDICES),
            "target_indices": list(TARGET_INDICES),
            "target_support_overlap": False,
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "coverage_by_symbol": _coverage_by_symbol(),
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "exact_cpython_behavior_oracle": True,
            "idf_objects_are_bounded_instrumented_stubs": True,
            "internal_native_route_claim": False,
            "native_runtime_executed_by_python_oracle": False,
            "path_independent_relocated_import": True,
            "resolved_index_796_reused_from_support": True,
            "target_coverage_complete": True,
        },
        "expectations": expectations,
        "native_routes": NATIVE_ROUTES,
        "runtime_signatures": signatures,
    }


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {case["id"]: canonical_sha256(case) for case in cases}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source_root: Path | None = None,
) -> dict[str, Any]:
    if commit.lower() != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    if inventory["content_sha256"] != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory receipt is not exact.")
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    source_file = imported_root / Path(SOURCE_PATH).relative_to("src")
    if (
        not source_file.is_file()
        or source_file.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(source_file) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported dragon HVAC source drifted.")
    work_root = (
        Path(__file__).resolve().parents[2]
        / "temp"
        / "reference"
        / "dragon-hvac-appenders-controllers-work"
    )
    with _isolated_import(imported_root, work_root, "location-one-") as primary:
        signatures = _runtime_signatures(primary.hvac)
        observations = _execute_cases(primary.hvac)
        loaded_modules = primary.loaded_local_modules
    with _isolated_import(imported_root, work_root, "location-two-") as relocated:
        relocated_signatures = _runtime_signatures(relocated.hvac)
        relocated_observations = _execute_cases(relocated.hvac)
        relocated_modules = relocated.loaded_local_modules
    if signatures != relocated_signatures:
        raise RuntimeError("Appender/controller signatures changed after relocation.")
    if observations != relocated_observations:
        raise RuntimeError("Appender/controller observations changed after relocation.")
    if loaded_modules != relocated_modules:
        raise RuntimeError("Appender/controller loaded modules changed after relocation.")

    signatures_hash = canonical_sha256(signatures)
    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned appender/controller runtime signatures drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and modules_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned appender/controller loaded modules drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Pinned appender/controller relocated observations drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned appender/controller fact hashes drifted.\n"
            + strict_json_dumps(fact_hashes, indent=2)
        )
    cases: list[dict[str, Any]] = []
    for definition in case_definitions():
        identifier = definition["id"]
        case = dict(definition)
        case["python"] = {
            "facts": observations[identifier],
            "facts_sha256": fact_hashes[identifier],
            "outcome": "observed",
        }
        cases.append(case)
    case_hashes = case_sha256(cases)
    aggregate = cases_sha256(cases)
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise SystemExit(
            "Pinned appender/controller case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned appender/controller aggregate case hash drifted.")

    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(
            inventory["target_receipts"], signatures
        ),
        "fact_sha256": fact_hashes,
        "native_review": _native_review(),
        "resolved_support_receipts": inventory["resolved_support_receipts"],
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "support": _support_receipt(inventory["resolved_support_receipts"]),
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "deferred_receipts_sha256": inventory["deferred_receipts_sha256"],
            "full_source_receipts_sha256": inventory["full_source_receipts_sha256"],
            "inventory": {
                "bytes": EXPECTED_INVENTORY_BYTES,
                "content_sha256": EXPECTED_INVENTORY_SHA256,
                "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
            },
            "isolated_import": {
                "loaded_local_modules": loaded_modules,
                "loaded_local_modules_sha256": modules_hash,
                "relocated_observations_sha256": relocation_hash,
                "relocated_source_copy": "two-byte-identical-repository-temp-copies",
                "source_location_count": 2,
            },
            "resolved_support_receipts_sha256": EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256,
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "path": SOURCE_PATH,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
            "target_receipts_sha256": EXPECTED_TARGET_RECEIPTS_SHA256,
        },
    }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "fact_sha256",
            "native_review",
            "resolved_support_receipts",
            "runtime",
            "schema",
            "support",
            "symbols",
            "target_receipts",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Appender/controller schema drifted.")
    targets = value["target_receipts"]
    support_receipts = value["resolved_support_receipts"]
    if (
        not isinstance(targets, list)
        or canonical_sha256(targets) != EXPECTED_TARGET_RECEIPTS_SHA256
        or [(item["inventory_index"], item["symbol"]) for item in targets]
        != list(TARGET_IDENTITIES)
    ):
        raise RuntimeError("Appender/controller target receipts drifted.")
    if (
        not isinstance(support_receipts, list)
        or canonical_sha256(support_receipts)
        != EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256
        or [(item["inventory_index"], item["symbol"]) for item in support_receipts]
        != list(RESOLVED_SUPPORT_IDENTITIES)
    ):
        raise RuntimeError("Appender/controller resolved support receipt drifted.")
    if value["symbols"] != [_descriptor(item) for item in targets]:
        raise RuntimeError("Appender/controller symbol descriptors drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict) or set(signatures) != set(TARGET_SYMBOLS):
        raise RuntimeError("Appender/controller runtime signatures are incomplete.")
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise RuntimeError("Pinned appender/controller runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(targets, signatures):
        raise RuntimeError("Appender/controller consumer contract drifted.")
    if value["runtime"] != _runtime_receipt():
        raise RuntimeError("Appender/controller runtime receipt drifted.")
    if value["support"] != _support_receipt(support_receipts):
        raise RuntimeError("Appender/controller support receipt drifted.")
    if value["native_review"] != _native_review():
        raise RuntimeError("Appender/controller native review drifted.")

    upstream = value["upstream"]
    _require_keys(
        upstream,
        {
            "commit",
            "deferred_receipts_sha256",
            "full_source_receipts_sha256",
            "inventory",
            "isolated_import",
            "resolved_support_receipts_sha256",
            "source",
            "target_receipts_sha256",
        },
        "upstream",
    )
    expected_static = {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "deferred_receipts_sha256": EXPECTED_DEFERRED_RECEIPTS_SHA256,
        "full_source_receipts_sha256": EXPECTED_FULL_SOURCE_RECEIPTS_SHA256,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "resolved_support_receipts_sha256": EXPECTED_RESOLVED_SUPPORT_RECEIPTS_SHA256,
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
        "target_receipts_sha256": EXPECTED_TARGET_RECEIPTS_SHA256,
    }
    for key, expected in expected_static.items():
        if upstream[key] != expected:
            raise RuntimeError(f"Appender/controller upstream field drifted: {key}")
    isolated = upstream["isolated_import"]
    _require_keys(
        isolated,
        {
            "loaded_local_modules",
            "loaded_local_modules_sha256",
            "relocated_observations_sha256",
            "relocated_source_copy",
            "source_location_count",
        },
        "isolated_import",
    )
    if (
        isolated["source_location_count"] != 2
        or isolated["relocated_source_copy"]
        != "two-byte-identical-repository-temp-copies"
        or isolated["loaded_local_modules_sha256"]
        != canonical_sha256(isolated["loaded_local_modules"])
    ):
        raise RuntimeError("Appender/controller relocation contract drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and isolated["loaded_local_modules_sha256"] != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise RuntimeError("Pinned appender/controller loaded modules drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and isolated["relocated_observations_sha256"] != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise RuntimeError("Pinned appender/controller relocation observations drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Appender/controller case order/count drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        _require_keys(case, {*definition, "python"}, f"case {definition['id']}")
        if any(case[key] != expected for key, expected in definition.items()):
            raise RuntimeError(f"Appender/controller case definition drifted: {definition['id']}")
        python = case["python"]
        _require_keys(python, {"facts", "facts_sha256", "outcome"}, "python")
        if python["outcome"] != "observed":
            raise RuntimeError(f"Appender/controller outcome drifted: {definition['id']}")
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(f"Appender/controller fact self hash drifted: {definition['id']}")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Appender/controller fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned appender/controller fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("Appender/controller case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned appender/controller case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Appender/controller aggregate case self hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned appender/controller aggregate case hash drifted.")

    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Appender/controller target coverage drifted.")
    closure = value["consumer_contract"]["closure"]
    if (
        closure["target_indices"] != list(TARGET_INDICES)
        or closure["resolved_support_indices"] != list(RESOLVED_SUPPORT_INDICES)
        or closure["deferred_indices"] != list(DEFERRED_INDICES)
        or sorted(
            (
                *closure["target_indices"],
                *closure["resolved_support_indices"],
                *closure["deferred_indices"],
            )
        )
        != list(SOURCE_INDICES)
    ):
        raise RuntimeError("Appender/controller full source closure drifted.")
    if set(CLASSIFICATIONS.values()) != {"exception"}:
        raise RuntimeError("Appender/controller classifications are not conservative.")
    forbidden_routes = ("SupplyIdfFragment", "EnergyModelIdfAssembler", ".Generate")
    if any(
        token in route
        for route in NATIVE_ROUTES.values()
        for token in forbidden_routes
    ):
        raise RuntimeError("An internal native implementation was claimed as a route.")
    SUPPORT.BASE._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Appender/controller strict JSON round trip drifted.")


def _validate_generation_runtime() -> None:
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


def main() -> int:
    arguments = parse_args()
    _validate_generation_runtime()
    inventory = load_exact_inventory(arguments.inventory, arguments.upstream_commit)
    result = build_oracle(inventory, arguments.upstream_commit)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    counts = Counter(CLASSIFICATIONS.values())
    print(
        f"Wrote {len(result['cases'])} dragon HVAC appender/controller cases "
        f"covering {len(TARGET_INDICES)} targets: "
        f"{counts['equivalent']} equivalent, {counts['exception']} exception, "
        f"aggregate {result['cases_sha256']}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Generate pinned misc-system observations for dragon HVAC.

This bounded corpus executes exactly 15 public declarations across the legacy
DomesticHotWater, EnergyRecoveryVentilator, and PhotoVoltaicPanel families.
The already-owned PhotoVoltaicPanel.to_idf_object declaration at inventory
index 761 is retained as immutable support and is never promoted by this
oracle.  Observations are repeated from two byte-identical source locations;
the Python process does not execute native code or EnergyPlus.
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import shutil
import sys
import tempfile
from typing import Any, Callable


SCHEMA = "goniegonie.python-reference.dragon-hvac-misc-systems-core.v1"
SOURCE_PATH = "src/idragon/dragon/hvac.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
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
EXPECTED_SUPPORT_GENERATOR_BYTES = 65_898
EXPECTED_SUPPORT_GENERATOR_SHA256 = (
    "sha256:3f1bcbf28df62c3426f8d343dab3f123b9c730bcdd234e3c570aaff21b87cd97"
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
        raise RuntimeError("Pinned dragon HVAC supply-core support drifted.")
    specification = importlib.util.spec_from_file_location(
        "_goniegonie_dragon_hvac_misc_support", SUPPORT_GENERATOR_PATH
    )
    if specification is None or specification.loader is None:
        raise RuntimeError("Cannot load pinned dragon HVAC misc support.")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.SOURCE_PATH != SOURCE_PATH
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
        or module.EXPECTED_SOURCE_AST_SHA256 != EXPECTED_SOURCE_AST_SHA256
    ):
        raise RuntimeError("Pinned dragon HVAC misc support identity drifted.")
    return module


SUPPORT = _load_support()
BASE = SUPPORT.BASE
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
load_json_without_duplicates = SUPPORT.load_json_without_duplicates
EXPECTED_DEPENDENCIES = dict(SUPPORT.EXPECTED_DEPENDENCIES)
REQUIRED_PYTHON = SUPPORT.REQUIRED_PYTHON
REQUIRED_HASH_ALGORITHM = SUPPORT.REQUIRED_HASH_ALGORITHM
REQUIRED_HASH_WIDTH_BITS = SUPPORT.REQUIRED_HASH_WIDTH_BITS


TARGET_INDEX_SYMBOLS = (
    (693, "DomesticHotWater"),
    (694, "DomesticHotWater.__init__"),
    (697, "DomesticHotWater.efficiency"),
    (698, "DomesticHotWater.fuel"),
    (699, "DomesticHotWater.to_idf_object"),
    (714, "EnergyRecoveryVentilator"),
    (715, "EnergyRecoveryVentilator.__init__"),
    (716, "EnergyRecoveryVentilator.to_idf_object"),
    (753, "PhotoVoltaicPanel"),
    (754, "PhotoVoltaicPanel.__init__"),
    (756, "PhotoVoltaicPanel.area"),
    (757, "PhotoVoltaicPanel.azimuth"),
    (758, "PhotoVoltaicPanel.effective_area_ratio"),
    (759, "PhotoVoltaicPanel.efficiency"),
    (760, "PhotoVoltaicPanel.tilt"),
)
TARGET_INDICES = tuple(index for index, _ in TARGET_INDEX_SYMBOLS)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_INDEX_SYMBOLS)

SOURCE_TOWER_INDICES = (
    641, 642, 643, 652, 653, 654, 657, 658, 659, 661, 662, 664, 665,
    667, 668, 669, 670, 673, 674, 675, 676, 677, 678, 679, 680, 681,
    682, 683, 726, 727, 728, 729, 730, 731, 732, 733, 734, 735, 736,
    738, 739, 740, 741, 742, 744, 745, 747, 748, 777, 778, 779, 780,
    781, 782, 783, 784, 785, 786, 787,
)
SUPPLY_CORE_INDICES = (
    645, 647, 648, 649, 650, 651, 700, 701, 702, 703, 704, 705, 706,
    707, 708, 709, 710, 711, 712, 713, 720, 721, 722, 723, 724, 725,
    750, 751, 752, 762, 763, 764, 765, 766, 767, 768, 769, 770, 771,
    772, 773, 789, 797, 798, 799, 800, 801, 802, 803,
)
APPENDER_CONTROLLER_INDICES = (
    686, 687, 688, 689, 690, 691, 692, 717, 718, 719, 774, 775,
    776, 804, 805, 806, 807, 808, 809, 810, 811, 812, 813, 814,
)
RESOLVED_INDICES = (
    644, 655, 656, 660, 663, 666, 672, 684, 685, 743, 746, 749,
    761, 788, 790, 791, 792, 793, 794, 795, 796,
)
OUT_OF_SCOPE_INDICES = (646, 671, 695, 696, 737, 755)
SOURCE_INDICES = tuple(range(641, 815))
PARTITION_INDICES = {
    "appenders_controllers": APPENDER_CONTROLLER_INDICES,
    "misc_systems_core": TARGET_INDICES,
    "out_of_scope": OUT_OF_SCOPE_INDICES,
    "resolved": RESOLVED_INDICES,
    "source_tower_core": SOURCE_TOWER_INDICES,
    "supply_core": SUPPLY_CORE_INDICES,
}
_partition_flat = tuple(
    index for indices in PARTITION_INDICES.values() for index in indices
)
if (
    len(TARGET_INDICES) != 15
    or len(SOURCE_TOWER_INDICES) != 59
    or len(SUPPLY_CORE_INDICES) != 49
    or len(APPENDER_CONTROLLER_INDICES) != 24
    or len(RESOLVED_INDICES) != 21
    or len(OUT_OF_SCOPE_INDICES) != 6
    or len(_partition_flat) != 174
    or len(set(_partition_flat)) != 174
    or sorted(_partition_flat) != list(SOURCE_INDICES)
):
    raise RuntimeError("Dragon HVAC 174-declaration source partition drifted.")

EXPECTED_PARTITION_RECEIPTS_SHA256 = {
    "appenders_controllers": "sha256:5228c06e02e371e4da5106bb10ba5e2159bd38b452ecdb2be459245c318f2495",
    "misc_systems_core": "sha256:92bd193686bf7ff9da3219571d197c70f55d16b33996268741e56af7083cff1b",
    "out_of_scope": "sha256:b59d76ef1e149324add85c497b3834fcad265eff512e366c9b6dc5e376ff3c72",
    "resolved": "sha256:134e7de998a36b0b4003a46d9c026544c4f1353ef6bce14849963d36ab304188",
    "source_tower_core": "sha256:894e31bb538cf8be2269a5b35b04e429ceb28b7fd881a7f6deff9d5166f360c1",
    "supply_core": "sha256:3c2629b0da4e0e83c079276de2b744707227784b77f1bf78225eb194d8fb5bf2",
}
EXPECTED_FULL_SOURCE_RECEIPTS_SHA256 = (
    "sha256:f5db7f1a79890387192db20619e055691700f48bfbe368efeffbe37b695593e7"
)

PV_SUPPORT_INDEX_SYMBOLS = ((761, "PhotoVoltaicPanel.to_idf_object"),)
EXPECTED_PV_SUPPORT_RECEIPT_SHA256 = (
    "sha256:cba0ef027d545cebea7499110dc774d4dc7e7de85e1ea8ae1f0b7b42828783cf"
)
PV_SUPPORT_FIXTURE = {
    "bytes": 147_261,
    "cases_sha256": "sha256:767c3314ec20d07aa12fdce48b9969a98b54b835855b4be7ecfdd896816be0dd",
    "path": "fixtures/reference/python-0.7.0/dragon-hvac-photovoltaic-to-idf-object-oracle.json",
    "schema": "goniegonie.python-reference.dragon-hvac-photovoltaic-to-idf-object.v1",
    "sha256": "sha256:07c383c316989ccb22ac3eadcf9d8388764f76effbbf03c13b7a54f8af20f22b",
}

EQUIVALENT_SYMBOLS = {
    "DomesticHotWater.efficiency",
    "DomesticHotWater.to_idf_object",
    "PhotoVoltaicPanel.area",
    "PhotoVoltaicPanel.azimuth",
    "PhotoVoltaicPanel.effective_area_ratio",
    "PhotoVoltaicPanel.efficiency",
    "PhotoVoltaicPanel.tilt",
}
CLASSIFICATIONS = {
    symbol: "equivalent" if symbol in EQUIVALENT_SYMBOLS else "exception"
    for symbol in TARGET_SYMBOLS
}
if Counter(CLASSIFICATIONS.values()) != Counter({"equivalent": 7, "exception": 8}):
    raise RuntimeError("Dragon HVAC misc conservative classification drifted.")

ASSERTION_IDS = {
    symbol: f"dragon-hvac-misc-systems-core-{index}-{symbol.replace('.', '-').lower()}"
    for index, symbol in TARGET_INDEX_SYMBOLS
}


def _native_route(symbol: str) -> str:
    prefix = "GonieGonie.InvisibleDragon.Hvac."
    routes = {
        "DomesticHotWater": prefix + "DomesticHotWater",
        "DomesticHotWater.__init__": prefix + "DomesticHotWater.DomesticHotWater(EntityId, string, Fuel, double)",
        "DomesticHotWater.efficiency": prefix + "DomesticHotWater.Efficiency",
        "DomesticHotWater.fuel": prefix + "DomesticHotWater.Fuel",
        "DomesticHotWater.to_idf_object": prefix + "DomesticHotWater.ToIdfObjects(IdfGenerationContext)",
        "EnergyRecoveryVentilator": prefix + "EnergyRecoveryVentilator",
        "EnergyRecoveryVentilator.__init__": prefix + "EnergyRecoveryVentilator.EnergyRecoveryVentilator(EntityId, string, double, double, double?, double, double)",
        "EnergyRecoveryVentilator.to_idf_object": (
            prefix + "ZoneVentilationAssignment -> "
            "GonieGonie.InvisibleDragon.Model.EnergyModel -> "
            "GonieGonie.InvisibleDragon.Model.EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?)"
        ),
        "PhotoVoltaicPanel": prefix + "PhotovoltaicPanel",
        "PhotoVoltaicPanel.__init__": prefix + "PhotovoltaicPanel.PhotovoltaicPanel(EntityId, string, double, double, double, double, double)",
        "PhotoVoltaicPanel.area": prefix + "PhotovoltaicPanel.AreaSquareMetres",
        "PhotoVoltaicPanel.azimuth": prefix + "PhotovoltaicPanel.AzimuthDegrees",
        "PhotoVoltaicPanel.effective_area_ratio": prefix + "PhotovoltaicPanel.ActiveCellAreaFraction",
        "PhotoVoltaicPanel.efficiency": prefix + "PhotovoltaicPanel.Efficiency",
        "PhotoVoltaicPanel.tilt": prefix + "PhotovoltaicPanel.TiltDegrees",
    }
    return routes[symbol]


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}
ADAPTATIONS = {
    symbol: (
        "direct-public-domestic-hot-water-empty-emission"
        if symbol == "DomesticHotWater.to_idf_object"
        else "aggregate-public-energy-model-ventilation-emission"
        if symbol == "EnergyRecoveryVentilator.to_idf_object"
        else "immutable-native-property"
        if symbol in EQUIVALENT_SYMBOLS
        else "immutable-native-domain-model"
    )
    for symbol in TARGET_SYMBOLS
}

PREFIX = "dragon-hvac-misc-systems-core."
CASE_SPECS = (
    ("A01", "domestic-hot-water-constructor-fuel", "DomesticHotWater", (
        "DomesticHotWater", "DomesticHotWater.__init__", "DomesticHotWater.fuel")),
    ("A02", "domestic-hot-water-efficiency-emission", "DomesticHotWater", (
        "DomesticHotWater.efficiency", "DomesticHotWater.to_idf_object")),
    ("B01", "energy-recovery-ventilator-permissive-empty", "EnergyRecoveryVentilator", (
        "EnergyRecoveryVentilator", "EnergyRecoveryVentilator.__init__", "EnergyRecoveryVentilator.to_idf_object")),
    ("C01", "photovoltaic-constructor-shape", "PhotoVoltaicPanel", (
        "PhotoVoltaicPanel", "PhotoVoltaicPanel.__init__")),
    ("C02", "photovoltaic-geometry-properties", "PhotoVoltaicPanel", (
        "PhotoVoltaicPanel.area", "PhotoVoltaicPanel.azimuth", "PhotoVoltaicPanel.tilt")),
    ("C03", "photovoltaic-efficiency-properties", "PhotoVoltaicPanel", (
        "PhotoVoltaicPanel.effective_area_ratio", "PhotoVoltaicPanel.efficiency")),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:dfc3cb1f6726e8674b9d6c32b3bc4438cc72631936c2a6d66377978d1880e34f"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:93cfad21e009eac906a4443998ad214eec82e2136ada5b7cea7888ababf30143"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:4f52a6e71dd8f2136d7ba9cfe61e904e2038d831291f3cb8c50c0f18aa5e7ca3"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:6acc31ab341ba8abf3ef70b8efc755210ab3febe048bbf4af67728b801de000b"
)
EXPECTED_FACT_SHA256 = {
    "dragon-hvac-misc-systems-core.domestic-hot-water-constructor-fuel": "sha256:8492b46ae506b16cd682e22a4b5b104ea8151eb866c5f308730a29e684d9e13b",
    "dragon-hvac-misc-systems-core.domestic-hot-water-efficiency-emission": "sha256:bdab6a146714be73054d9500686cb1d631b7bc6f76a55c9bdbe30042f6c9fc41",
    "dragon-hvac-misc-systems-core.energy-recovery-ventilator-permissive-empty": "sha256:07678b1e3fccc095815b9ec30e57786a015c071544e2f3816d29b662f8107259",
    "dragon-hvac-misc-systems-core.photovoltaic-constructor-shape": "sha256:9d42863c0cb8de852b4cc45e19518a652b6bd18ad4fee191d2a46872e237149d",
    "dragon-hvac-misc-systems-core.photovoltaic-efficiency-properties": "sha256:a0b39cb7c87d292f5ee53eeffe80c33e0de73f23d1cf6565fc8e38fa7b563453",
    "dragon-hvac-misc-systems-core.photovoltaic-geometry-properties": "sha256:83470f1177e4f17e1fbfeacd6239cc826d5e95845c3acafc196366a3fe0c9625",
}
EXPECTED_CASE_SHA256 = {
    "dragon-hvac-misc-systems-core.domestic-hot-water-constructor-fuel": "sha256:61beb677ffd37c28b5e0342599c9bed0db0d03bc93aaf45dabd350dc96f2554c",
    "dragon-hvac-misc-systems-core.domestic-hot-water-efficiency-emission": "sha256:c04616618b419cc84decdc047747a07aa87d85bde2f9d4a5af3ccf099089937b",
    "dragon-hvac-misc-systems-core.energy-recovery-ventilator-permissive-empty": "sha256:7003b96958be66c6f33756480249a91ff5d879ee912ff34b9edb14e98eeffff8",
    "dragon-hvac-misc-systems-core.photovoltaic-constructor-shape": "sha256:9873174fd27c61b03c65aa9499642195804c89e9af6c559b55adb6ef485c1073",
    "dragon-hvac-misc-systems-core.photovoltaic-efficiency-properties": "sha256:2f4c5a8b04b5582804edbad5e6c4abfbdbdbb3c4331fff49738b677f80bd8ad8",
    "dragon-hvac-misc-systems-core.photovoltaic-geometry-properties": "sha256:bd3cb31de0085b52e6ca0e4665e06cb161884797a2655e6377f025950dd28556",
}
EXPECTED_CASES_SHA256 = (
    "sha256:4f52a6e71dd8f2136d7ba9cfe61e904e2038d831291f3cb8c50c0f18aa5e7ca3"
)

NATIVE_IMPLEMENTATION_COMMIT = "8f289eb8e94883cde53f583ab250fa6c4394ce2a"
NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 7_582,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/HvacAbstractions.cs",
        "sha256": "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a",
    },
    {
        "bytes": 1_941,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/DomesticHotWater.cs",
        "sha256": "sha256:586f020b82c50c70ad20d8a667fa338ce3372d39bb1bd48291ea42c97b8d4e2d",
    },
    {
        "bytes": 7_074,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Hvac/VentilationAndPv.cs",
        "sha256": "sha256:eb7d871d621c8f3970099dff7bdb412dc84f33cd2ef07c0fb99c94a550d5eb82",
    },
    {
        "bytes": 22_015,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModel.cs",
        "sha256": "sha256:f9a4bcda010c2690ea57b2f9f8d9d3b134fc60139bfe24dce5d973dc18eeceb3",
    },
    {
        "bytes": 50_764,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Model/EnergyModelIdfAssembler.cs",
        "sha256": "sha256:af84d55c3450260f6ff59e277724b853a7749def3e18b44ba65e7ccefb725905",
    },
    {
        "bytes": 13_182,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idf/IdfModel.cs",
        "sha256": "sha256:50aa8a362214d34bba37dcf51ef3c0cce89d54895110a0da786c11d8fe233495",
    },
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _receipt(symbols: list[dict[str, Any]], index: int) -> dict[str, Any]:
    return {"inventory_index": index, **symbols[index]}


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    if upstream_commit.lower() != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    if path.stat().st_size != EXPECTED_INVENTORY_BYTES:
        raise SystemExit("The public-symbol inventory byte length drifted.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash drifted.")
    value = load_json_without_duplicates(path)
    if value.get("upstream_commit") != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The public-symbol inventory commit drifted.")
    if value.get("content_sha256") != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory content receipt drifted.")
    source_file = next(
        (item for item in value.get("files", []) if item.get("path") == SOURCE_PATH),
        None,
    )
    if source_file != {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }:
        raise SystemExit("The pinned dragon HVAC source receipt drifted.")
    symbols = value.get("symbols")
    if not isinstance(symbols, list):
        raise SystemExit("The public-symbol inventory symbols are malformed.")
    source_receipts = [
        _receipt(symbols, index)
        for index in SOURCE_INDICES
        if symbols[index].get("path") == SOURCE_PATH
    ]
    if [item["inventory_index"] for item in source_receipts] != list(SOURCE_INDICES):
        raise SystemExit("The dragon HVAC 174-declaration inventory range drifted.")

    partitions = {
        name: [_receipt(symbols, index) for index in indices]
        for name, indices in PARTITION_INDICES.items()
    }
    if tuple(
        (item["inventory_index"], item["symbol"])
        for item in partitions["misc_systems_core"]
    ) != TARGET_INDEX_SYMBOLS:
        raise SystemExit("The misc-system target index/symbol closure drifted.")
    partition_hashes = {
        name: canonical_sha256(receipts) for name, receipts in partitions.items()
    }
    if partition_hashes != EXPECTED_PARTITION_RECEIPTS_SHA256:
        raise SystemExit("The full dragon HVAC source partition receipts drifted.")
    if canonical_sha256(source_receipts) != EXPECTED_FULL_SOURCE_RECEIPTS_SHA256:
        raise SystemExit("The full dragon HVAC source receipt aggregate drifted.")
    pv_support = [_receipt(symbols, index) for index, _ in PV_SUPPORT_INDEX_SYMBOLS]
    if tuple((item["inventory_index"], item["symbol"]) for item in pv_support) != PV_SUPPORT_INDEX_SYMBOLS:
        raise SystemExit("The photovoltaic support identity drifted.")
    if canonical_sha256(pv_support) != EXPECTED_PV_SUPPORT_RECEIPT_SHA256:
        raise SystemExit("The photovoltaic support receipt drifted.")
    return {
        "partition_hashes": partition_hashes,
        "partitions": partitions,
        "pv_support": pv_support,
        "source_file": source_file,
        "target_receipts": partitions["misc_systems_core"],
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "code": code,
            "family": family,
            "id": PREFIX + slug,
            "target_symbols": list(symbols),
        }
        for code, slug, family, symbols in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Dragon HVAC misc case identifiers drifted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Dragon HVAC misc cases do not exactly partition 15 targets.")
    return definitions


def _encode(value: Any) -> dict[str, Any]:
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
    return {"kind": "object", "type": type(value).__name__}


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
        "bases": [base.__name__ for base in value.__bases__],
        "class_signature": str(inspect.signature(value)),
        "init_signature": str(inspect.signature(value.__init__)),
        "module": value.__module__,
    }


def _state(value: Any, names: tuple[str, ...]) -> dict[str, Any]:
    return {name: _encode(getattr(value, name)) for name in names}


def _constructed(function: Callable[[], Any], names: tuple[str, ...]) -> dict[str, Any]:
    try:
        value = function()
    except Exception as error:
        return {
            "args": [str(argument) for argument in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {
        "object_type": type(value).__name__,
        "outcome": "returned",
        "state": _state(value, names),
    }


def _domestic_facts(hvac: Any) -> dict[str, Any]:
    fuel = hvac.Fuel
    state_names = ("name", "fuel", "efficiency")
    constructors: dict[str, Any] = {}
    fuel_values = (
        ("enum-member", fuel.ELECTRICITY),
        ("exact-value-string", "Electricity"),
        ("enum-name-string", "ELECTRICITY"),
        ("integer", 1),
        ("true", True),
        ("none", None),
    )
    for label, candidate in fuel_values:
        record = _constructed(
            lambda candidate=candidate: hvac.DomesticHotWater("DHW", candidate, 0.8),
            state_names,
        )
        if record["outcome"] == "returned":
            value = hvac.DomesticHotWater("DHW", candidate, 0.8)
            record["fuel_storage_type"] = type(
                value.__dict__["_DomesticHotWater__fuel"]
            ).__name__
        constructors[label] = record

    mutations: dict[str, Any] = {}
    for label, candidate in fuel_values:
        value = hvac.DomesticHotWater("DHW", fuel.ELECTRICITY, 0.8)
        outcome = _attempt(lambda value=value, candidate=candidate: setattr(value, "fuel", candidate))
        mutations[label] = {
            "outcome": outcome,
            "state_after": _state(value, state_names),
            "storage_type_after": type(
                value.__dict__["_DomesticHotWater__fuel"]
            ).__name__,
        }
    return {
        "class_shape": _class_shape(hvac.DomesticHotWater),
        "fuel_enum": [
            {"name": item.name, "string": str(item), "value": item.value}
            for item in fuel
        ],
        "fuel_constructor_matrix": constructors,
        "fuel_mutation_matrix": mutations,
        "name_is_mutable": (
            lambda value: (setattr(value, "name", "Renamed"), value.name)[1]
        )(hvac.DomesticHotWater("DHW", fuel.ELECTRICITY, 0.8))
        == "Renamed",
    }


EFFICIENCY_CANDIDATES = (
    ("negative-infinity", float("-inf")),
    ("negative-one", -1.0),
    ("negative-zero", -0.0),
    ("zero", 0.0),
    ("minimum-positive-subnormal", math.nextafter(0.0, math.inf)),
    ("one", 1.0),
    ("nextafter-one-up", math.nextafter(1.0, math.inf)),
    ("positive-infinity", float("inf")),
    ("nan", float("nan")),
    ("false", False),
    ("true", True),
    ("integer-one", 1),
    ("numeric-string", "0.8"),
    ("none", None),
)


def _domestic_efficiency_facts(hvac: Any) -> dict[str, Any]:
    fuel = hvac.Fuel.ELECTRICITY
    state_names = ("name", "fuel", "efficiency")
    constructors: dict[str, Any] = {}
    mutations: dict[str, Any] = {}
    for label, candidate in EFFICIENCY_CANDIDATES:
        constructors[label] = _constructed(
            lambda candidate=candidate: hvac.DomesticHotWater("DHW", fuel, candidate),
            state_names,
        )
        value = hvac.DomesticHotWater("DHW", fuel, 0.8)
        outcome = _attempt(
            lambda value=value, candidate=candidate: setattr(value, "efficiency", candidate)
        )
        mutations[label] = {
            "outcome": outcome,
            "state_after": _state(value, state_names),
            "storage_type_after": type(
                value.__dict__["_DomesticHotWater__efficiency"]
            ).__name__,
        }
    value = hvac.DomesticHotWater("DHW", fuel, 0.8)
    first = value.to_idf_object()
    second = value.to_idf_object()
    return {
        "constructor_matrix": constructors,
        "emission": {
            "first": first,
            "fresh_result_list": first is not second,
            "result_type": type(first).__name__,
            "second": second,
        },
        "mutation_matrix": mutations,
    }


def _erv_facts(hvac: Any) -> dict[str, Any]:
    state_names = ("name", "heating_efficiency", "cooling_efficiency")
    constructor_inputs = (
        ("ordinary", ("ERV", 0.7, 0.6)),
        ("none-bool-nan", (None, True, float("nan"))),
        ("strings", ("ERV", "heat", "cool")),
        ("nonfinite", ("ERV", float("inf"), float("-inf"))),
    )
    constructors = {
        label: _constructed(
            lambda arguments=arguments: hvac.EnergyRecoveryVentilator(*arguments),
            state_names,
        )
        for label, arguments in constructor_inputs
    }
    value = hvac.EnergyRecoveryVentilator("ERV", 0.7, 0.6)
    aliases_before = {
        name: value.__dict__[name] is getattr(value, name) for name in state_names
    }
    mutation_values = {
        "cooling_efficiency": None,
        "heating_efficiency": "changed",
        "name": 17,
    }
    mutation_outcomes = {
        name: _attempt(
            lambda name=name, candidate=candidate: setattr(value, name, candidate)
        )
        for name, candidate in mutation_values.items()
    }
    first = value.to_idf_object()
    second = value.to_idf_object()
    return {
        "aliases_before_mutation": aliases_before,
        "arity_errors": {
            "extra": _attempt(
                lambda: hvac.EnergyRecoveryVentilator("ERV", 0.7, 0.6, 0.5)
            ),
            "missing": _attempt(lambda: hvac.EnergyRecoveryVentilator("ERV", 0.7)),
        },
        "class_shape": _class_shape(hvac.EnergyRecoveryVentilator),
        "constructor_matrix": constructors,
        "emission": {
            "first": first,
            "fresh_result_list": first is not second,
            "result_type": type(first).__name__,
            "second": second,
        },
        "mutation": {
            "outcomes": mutation_outcomes,
            "state_after": _state(value, state_names),
        },
    }


PV_DEFAULTS = {
    "area": 10.0,
    "azimuth": 180.0,
    "effective_area_ratio": 0.7,
    "efficiency": 0.2,
    "tilt": 30.0,
}
PV_STATE_NAMES = (
    "name",
    "area",
    "tilt",
    "azimuth",
    "efficiency",
    "effective_area_ratio",
)
PV_CANDIDATES = {
    "area": (
        ("negative-infinity", float("-inf")),
        ("negative-one", -1.0),
        ("zero", 0.0),
        ("minimum-positive-subnormal", math.nextafter(0.0, math.inf)),
        ("one", 1.0),
        ("positive-infinity", float("inf")),
        ("nan", float("nan")),
        ("false", False),
        ("true", True),
        ("numeric-string", "2"),
    ),
    "tilt": (
        ("negative-one", -1.0),
        ("zero", 0.0),
        ("ninety", 90.0),
        ("nextafter-ninety-up", math.nextafter(90.0, math.inf)),
        ("positive-infinity", float("inf")),
        ("negative-infinity", float("-inf")),
        ("nan", float("nan")),
        ("false", False),
        ("true", True),
        ("numeric-string", "2"),
    ),
    "azimuth": (
        ("negative-one", -1.0),
        ("zero", 0.0),
        ("maximum-below-360", math.nextafter(360.0, -math.inf)),
        ("three-sixty", 360.0),
        ("positive-infinity", float("inf")),
        ("negative-infinity", float("-inf")),
        ("nan", float("nan")),
        ("false", False),
        ("true", True),
        ("numeric-string", "2"),
        ("none", None),
    ),
    "efficiency": EFFICIENCY_CANDIDATES,
    "effective_area_ratio": EFFICIENCY_CANDIDATES,
}


def _new_pv(hvac: Any, property_name: str | None = None, candidate: Any = None) -> Any:
    values = dict(PV_DEFAULTS)
    if property_name is not None:
        values[property_name] = candidate
    return hvac.PhotoVoltaicPanel(
        "PV",
        values["area"],
        values["tilt"],
        values["azimuth"],
        values["efficiency"],
        effective_area_ratio=values["effective_area_ratio"],
    )


def _pv_property_matrix(hvac: Any, property_name: str) -> dict[str, Any]:
    constructors: dict[str, Any] = {}
    mutations: dict[str, Any] = {}
    for label, candidate in PV_CANDIDATES[property_name]:
        constructors[label] = _constructed(
            lambda property_name=property_name, candidate=candidate: _new_pv(
                hvac, property_name, candidate
            ),
            PV_STATE_NAMES,
        )
        value = _new_pv(hvac)
        state_before = _state(value, PV_STATE_NAMES)
        outcome = _attempt(
            lambda value=value, property_name=property_name, candidate=candidate: setattr(
                value, property_name, candidate
            )
        )
        state_after = _state(value, PV_STATE_NAMES)
        mutations[label] = {
            "failed_state_unchanged": (
                state_before == state_after if outcome["outcome"] == "raised" else None
            ),
            "outcome": outcome,
            "state_after": state_after,
        }
    return {"constructor_matrix": constructors, "mutation_matrix": mutations}


def _pv_shape_facts(hvac: Any) -> dict[str, Any]:
    default = hvac.PhotoVoltaicPanel("PV", 10.0, 30.0, 180.0, 0.2)
    keyword = hvac.PhotoVoltaicPanel(
        name="PV-K",
        area=8,
        tilt=0,
        azimuth=None,
        efficiency=True,
        effective_area_ratio=1,
    )
    return {
        "class_shape": _class_shape(hvac.PhotoVoltaicPanel),
        "default_effective_area_ratio": _encode(default.effective_area_ratio),
        "default_state": _state(default, PV_STATE_NAMES),
        "keyword_state": _state(keyword, PV_STATE_NAMES),
        "keyword_only_ratio_rejects_positional": _attempt(
            lambda: hvac.PhotoVoltaicPanel("PV", 10, 30, 180, 0.2, 0.8)
        ),
        "name_is_unvalidated": _constructed(
            lambda: hvac.PhotoVoltaicPanel(None, 10, 30, 180, 0.2),
            PV_STATE_NAMES,
        ),
    }


def _execute_cases(hvac: Any) -> list[dict[str, Any]]:
    facts_by_code = {
        "A01": _domestic_facts(hvac),
        "A02": _domestic_efficiency_facts(hvac),
        "B01": _erv_facts(hvac),
        "C01": _pv_shape_facts(hvac),
        "C02": {
            name: _pv_property_matrix(hvac, name)
            for name in ("area", "azimuth", "tilt")
        },
        "C03": {
            name: _pv_property_matrix(hvac, name)
            for name in ("effective_area_ratio", "efficiency")
        },
    }
    cases: list[dict[str, Any]] = []
    for definition in case_definitions():
        facts = facts_by_code[definition["code"]]
        python = {
            "facts": facts,
            "facts_sha256": canonical_sha256(facts),
            "outcome": "observed",
        }
        case = {**definition, "python": python}
        case["case_sha256"] = canonical_sha256(case)
        cases.append(case)
    return cases


def _resolve_runtime_symbol(hvac: Any, symbol: str) -> Any:
    owner_name, separator, member_name = symbol.partition(".")
    owner = getattr(hvac, owner_name)
    return owner if not separator else inspect.getattr_static(owner, member_name)


def _runtime_signature(value: Any) -> dict[str, Any]:
    if isinstance(value, property):
        return {
            "descriptor_type": "property",
            "getter": str(inspect.signature(value.fget)) if value.fget else None,
            "setter": str(inspect.signature(value.fset)) if value.fset else None,
        }
    return {
        "descriptor_type": type(value).__name__,
        "signature": str(inspect.signature(value)),
    }


def _runtime_signatures(hvac: Any) -> dict[str, Any]:
    return {
        symbol: _runtime_signature(_resolve_runtime_symbol(hvac, symbol))
        for symbol in TARGET_SYMBOLS
    }


def _repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _verify_file_receipt(expected: dict[str, Any]) -> dict[str, Any]:
    path = _repository_root() / expected["path"]
    actual = {
        "bytes": path.stat().st_size,
        "path": expected["path"],
        "sha256": sha256_file(path),
    }
    if actual != expected:
        raise SystemExit(f"Pinned native source drifted: {expected['path']}")
    return actual


def _pv_support_fixture_receipt(
    pv_support_receipts: list[dict[str, Any]],
) -> dict[str, Any]:
    expected = PV_SUPPORT_FIXTURE
    path = _repository_root() / expected["path"]
    value = load_json_without_duplicates(path)
    actual = {
        "bytes": path.stat().st_size,
        "cases_sha256": value.get("cases_sha256"),
        "path": expected["path"],
        "schema": value.get("schema"),
        "sha256": sha256_file(path),
    }
    if actual != expected:
        raise SystemExit("Pinned photovoltaic to-IDF support fixture drifted.")
    symbols = value.get("symbols")
    if not isinstance(symbols, list) or len(symbols) != 1:
        raise SystemExit("Pinned photovoltaic support symbol closure drifted.")
    if symbols[0] != _descriptor(pv_support_receipts[0]):
        raise SystemExit("Pinned photovoltaic support symbol receipt drifted.")
    return {
        **actual,
        "resolved_receipts": pv_support_receipts,
        "role": "immutable-index-761-photovoltaic-idf-emission-support-only",
        "target_promoted": False,
    }


def _native_review() -> dict[str, Any]:
    sources = [_verify_file_receipt(item) for item in NATIVE_SOURCE_RECEIPTS]
    review = {
        "classifications": CLASSIFICATIONS,
        "counts": {"equivalent": 7, "exception": 8, "total": 15},
        "domestic_hot_water_direct_public_api_only": True,
        "energy_recovery_ventilator_public_aggregate_route": True,
        "internal_generate_route_claimed": False,
        "native_implementation_commit": NATIVE_IMPLEMENTATION_COMMIT,
        "native_routes": NATIVE_ROUTES,
        "photovoltaic_public_api_only": True,
        "sources": sources,
    }
    digest = canonical_sha256(review)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise SystemExit("Dragon HVAC misc native review drifted.")
    return review


def _runtime_receipt() -> dict[str, Any]:
    if tuple(sys.version_info[:3]) != REQUIRED_PYTHON:
        raise SystemExit("Dragon HVAC misc generation requires exact CPython 3.12.7.")
    if sys.implementation.name != "cpython":
        raise SystemExit("Dragon HVAC misc generation requires CPython.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("The pinned CPython hash implementation drifted.")
    if os.environ.get("PYTHONHASHSEED") != "0":
        raise SystemExit("Dragon HVAC misc generation requires PYTHONHASHSEED=0.")
    dependencies: dict[str, str] = {}
    for package, expected in EXPECTED_DEPENDENCIES.items():
        try:
            actual = importlib.metadata.version(package)
        except importlib.metadata.PackageNotFoundError as error:
            raise SystemExit(f"Pinned dependency is unavailable: {package}") from error
        if actual != expected:
            raise SystemExit(
                f"Pinned dependency drifted: {package} expected {expected}, got {actual}"
            )
        dependencies[package] = actual
    return {
        "dependencies": dependencies,
        "hash_algorithm": sys.hash_info.algorithm,
        "hash_width_bits": sys.hash_info.width,
        "implementation": sys.implementation.name,
        "python": ".".join(str(item) for item in REQUIRED_PYTHON),
        "pythonhashseed": "0",
        "support": {
            "bytes": EXPECTED_SUPPORT_GENERATOR_BYTES,
            "path": "tools/python-reference/generate_dragon_hvac_supply_core_oracle.py",
            "sha256": EXPECTED_SUPPORT_GENERATOR_SHA256,
        },
        "utf8_mode": bool(sys.flags.utf8_mode),
    }


def _symbol_contract(receipts: list[dict[str, Any]]) -> list[dict[str, Any]]:
    by_symbol = {item["symbol"]: item for item in receipts}
    return [
        {
            "adaptation": ADAPTATIONS[symbol],
            "assertion_id": ASSERTION_IDS[symbol],
            "classification": CLASSIFICATIONS[symbol],
            "inventory_index": by_symbol[symbol]["inventory_index"],
            "native_route": NATIVE_ROUTES[symbol],
            "symbol": symbol,
            "symbol_hash": by_symbol[symbol]["symbol_hash"],
        }
        for symbol in TARGET_SYMBOLS
    ]


def _consumer_contract(
    runtime_signatures: dict[str, Any],
    partition_hashes: dict[str, str],
) -> dict[str, Any]:
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "classification_counts": {"equivalent": 7, "exception": 8},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "exact_disjoint_source_partition": True,
            "full_hvac_source_partition": True,
            "partition_counts": {
                name: len(indices) for name, indices in PARTITION_INDICES.items()
            },
            "partition_indices": {
                name: list(indices) for name, indices in PARTITION_INDICES.items()
            },
            "partition_receipts_sha256": partition_hashes,
            "source_declaration_count": 174,
            "source_indices": list(SOURCE_INDICES),
            "target_count": 15,
        },
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "constructor_property_mutation_and_boundaries_observed": True,
            "domestic_and_erv_fresh_empty_lists_observed": True,
            "native_runtime_executed_by_python_oracle": False,
            "nonfinite_and_bool_quirks_observed": True,
            "photovoltaic_index_761_emission_executed": False,
            "photovoltaic_index_761_support_reused": True,
        },
        "internal_generate_claimed": False,
        "native_routes": NATIVE_ROUTES,
        "runtime_signatures": runtime_signatures,
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _scan_deterministic_payload(value: dict[str, Any]) -> None:
    payload = strict_json_dumps(value)
    patterns = (
        (BASE.RAW_ADDRESS_PATTERN, "raw memory address"),
        (BASE.ABSOLUTE_PATH_PATTERN, "absolute path"),
        (BASE.GUID_PATTERN, "uncontrolled GUID"),
        (BASE.TIMESTAMP_PATTERN, "timestamp"),
    )
    for pattern, label in patterns:
        if pattern.search(payload):
            raise SystemExit(f"Dragon HVAC misc oracle contains a {label}.")


def build_oracle(inventory_path: Path, upstream_commit: str) -> dict[str, Any]:
    inventory = load_exact_inventory(inventory_path, upstream_commit)
    runtime = _runtime_receipt()
    native_review = _native_review()
    pv_support = _pv_support_fixture_receipt(inventory["pv_support"])
    source_root = BASE._find_pinned_source_root()
    source_file = source_root / Path(SOURCE_PATH).relative_to("src")
    if (
        source_file.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(source_file) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported dragon HVAC source bytes drifted.")

    with BASE._pinned_modules(source_root) as modules:
        cases = _execute_cases(modules.hvac)
        signatures = _runtime_signatures(modules.hvac)
        loaded_modules = list(modules.loaded_local_modules)

    with tempfile.TemporaryDirectory(
        prefix="goniegonie-dragon-hvac-misc-relocated-"
    ) as temporary:
        relocated_root = Path(temporary) / "relocated-source"
        shutil.copytree(source_root, relocated_root)
        relocated_source = relocated_root / Path(SOURCE_PATH).relative_to("src")
        if (
            relocated_source.stat().st_size != EXPECTED_SOURCE_BYTES
            or sha256_file(relocated_source) != EXPECTED_SOURCE_SHA256
        ):
            raise SystemExit("The relocated dragon HVAC source copy drifted.")
        with BASE._pinned_modules(relocated_root) as relocated_modules:
            relocated_cases = _execute_cases(relocated_modules.hvac)
            relocated_signatures = _runtime_signatures(relocated_modules.hvac)
            relocated_loaded_modules = list(relocated_modules.loaded_local_modules)

    if cases != relocated_cases:
        raise SystemExit("Dragon HVAC misc observations are source-path dependent.")
    if signatures != relocated_signatures:
        raise SystemExit("Dragon HVAC misc signatures are source-path dependent.")
    if loaded_modules != relocated_loaded_modules:
        raise SystemExit("Dragon HVAC misc loaded modules are source-path dependent.")
    if loaded_modules != BASE._expected_loaded_local_modules():
        raise SystemExit("Dragon HVAC misc imports escaped the pinned source graph.")

    loaded_hash = canonical_sha256(loaded_modules)
    relocated_hash = canonical_sha256(relocated_cases)
    signatures_hash = canonical_sha256(signatures)
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and loaded_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Dragon HVAC misc loaded-module aggregate drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocated_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Dragon HVAC misc relocated-observation aggregate drifted.")
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Dragon HVAC misc runtime-signature aggregate drifted.")

    fact_hashes = {item["id"]: item["python"]["facts_sha256"] for item in cases}
    case_hashes = {item["id"]: item["case_sha256"] for item in cases}
    cases_hash = canonical_sha256(cases)
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit("Dragon HVAC misc fact hash partition drifted.")
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise SystemExit("Dragon HVAC misc case hash partition drifted.")
    if EXPECTED_CASES_SHA256 and cases_hash != EXPECTED_CASES_SHA256:
        raise SystemExit("Dragon HVAC misc case aggregate drifted.")

    value = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": cases_hash,
        "consumer_contract": _consumer_contract(
            signatures, inventory["partition_hashes"]
        ),
        "fact_sha256": fact_hashes,
        "native_review": native_review,
        "runtime": runtime,
        "schema": SCHEMA,
        "support": pv_support,
        "symbols": _symbol_contract(inventory["target_receipts"]),
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "full_source_receipts_sha256": EXPECTED_FULL_SOURCE_RECEIPTS_SHA256,
            "inventory": {
                "bytes": EXPECTED_INVENTORY_BYTES,
                "content_sha256": EXPECTED_INVENTORY_SHA256,
                "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
                "path": "upstream/public-symbol-inventory.json",
            },
            "loaded_local_modules": loaded_modules,
            "loaded_local_modules_sha256": loaded_hash,
            "partitions": inventory["partitions"],
            "relocation": {
                "byte_identical_source_copy": True,
                "observations_sha256": relocated_hash,
                "path_independent": True,
                "runtime_signatures_sha256": canonical_sha256(relocated_signatures),
            },
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "path": SOURCE_PATH,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
            "target_receipts_sha256": EXPECTED_PARTITION_RECEIPTS_SHA256[
                "misc_systems_core"
            ],
        },
    }
    validate_oracle(value)
    _scan_deterministic_payload(value)
    return value


def validate_oracle(value: dict[str, Any]) -> None:
    expected_root_keys = {
        "case_sha256", "cases", "cases_sha256", "consumer_contract",
        "fact_sha256", "native_review", "runtime", "schema", "support",
        "symbols", "target_receipts", "upstream",
    }
    if set(value) != expected_root_keys:
        raise ValueError("Dragon HVAC misc oracle root keys are not exact.")
    if value["schema"] != SCHEMA:
        raise ValueError("Dragon HVAC misc oracle schema drifted.")
    if value["runtime"] != _runtime_receipt():
        raise ValueError("Dragon HVAC misc runtime receipt drifted.")

    targets = value.get("target_receipts")
    if not isinstance(targets, list) or len(targets) != 15:
        raise ValueError("Dragon HVAC misc target receipt count drifted.")
    if tuple((item.get("inventory_index"), item.get("symbol")) for item in targets) != TARGET_INDEX_SYMBOLS:
        raise ValueError("Dragon HVAC misc target identity mapping drifted.")
    if canonical_sha256(targets) != EXPECTED_PARTITION_RECEIPTS_SHA256["misc_systems_core"]:
        raise ValueError("Dragon HVAC misc target receipt aggregate drifted.")

    upstream = value.get("upstream")
    if not isinstance(upstream, dict) or set(upstream) != {
        "commit", "full_source_receipts_sha256", "inventory",
        "loaded_local_modules", "loaded_local_modules_sha256", "partitions",
        "relocation", "source", "target_receipts_sha256",
    }:
        raise ValueError("Dragon HVAC misc upstream contract drifted.")
    if upstream["commit"] != EXPECTED_UPSTREAM_COMMIT:
        raise ValueError("Dragon HVAC misc upstream commit drifted.")
    if upstream["full_source_receipts_sha256"] != EXPECTED_FULL_SOURCE_RECEIPTS_SHA256:
        raise ValueError("Dragon HVAC misc full-source receipt pin drifted.")
    if upstream["inventory"] != {
        "bytes": EXPECTED_INVENTORY_BYTES,
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        "path": "upstream/public-symbol-inventory.json",
    }:
        raise ValueError("Dragon HVAC misc inventory receipt drifted.")
    if upstream["source"] != {
        "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
        "bytes": EXPECTED_SOURCE_BYTES,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise ValueError("Dragon HVAC misc source receipt drifted.")

    partitions = upstream.get("partitions")
    if not isinstance(partitions, dict) or set(partitions) != set(PARTITION_INDICES):
        raise ValueError("Dragon HVAC misc partition names drifted.")
    flat: list[int] = []
    partition_hashes: dict[str, str] = {}
    for name, indices in PARTITION_INDICES.items():
        receipts = partitions[name]
        if [item.get("inventory_index") for item in receipts] != list(indices):
            raise ValueError(f"Dragon HVAC misc partition indices drifted: {name}")
        partition_hashes[name] = canonical_sha256(receipts)
        flat.extend(indices)
    if partition_hashes != EXPECTED_PARTITION_RECEIPTS_SHA256:
        raise ValueError("Dragon HVAC misc partition receipt pins drifted.")
    if len(flat) != 174 or len(set(flat)) != 174 or sorted(flat) != list(SOURCE_INDICES):
        raise ValueError("Dragon HVAC misc partition is not exact and disjoint.")
    if canonical_sha256([item for index in SOURCE_INDICES for item in (
        next(receipt for receipts in partitions.values() for receipt in receipts if receipt["inventory_index"] == index),
    )]) != EXPECTED_FULL_SOURCE_RECEIPTS_SHA256:
        raise ValueError("Dragon HVAC misc partition full-source aggregate drifted.")

    loaded = upstream.get("loaded_local_modules")
    if loaded != BASE._expected_loaded_local_modules():
        raise ValueError("Dragon HVAC misc loaded-module graph drifted.")
    loaded_hash = canonical_sha256(loaded)
    if upstream.get("loaded_local_modules_sha256") != loaded_hash:
        raise ValueError("Dragon HVAC misc loaded-module self receipt drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and loaded_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise ValueError("Dragon HVAC misc loaded-module pin drifted.")

    signatures = value["consumer_contract"].get("runtime_signatures")
    if not isinstance(signatures, dict) or set(signatures) != set(TARGET_SYMBOLS):
        raise ValueError("Dragon HVAC misc runtime-signature closure drifted.")
    signatures_hash = canonical_sha256(signatures)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise ValueError("Dragon HVAC misc runtime-signature pin drifted.")
    relocation = upstream.get("relocation")
    if relocation != {
        "byte_identical_source_copy": True,
        "observations_sha256": canonical_sha256(value["cases"]),
        "path_independent": True,
        "runtime_signatures_sha256": signatures_hash,
    }:
        raise ValueError("Dragon HVAC misc relocation receipt drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation["observations_sha256"] != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise ValueError("Dragon HVAC misc relocated-observation pin drifted.")

    cases = value.get("cases")
    definitions = case_definitions()
    if not isinstance(cases, list) or len(cases) != 6:
        raise ValueError("Dragon HVAC misc case count drifted.")
    if [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise ValueError("Dragon HVAC misc case order drifted.")
    for case, definition in zip(cases, definitions):
        for key in ("code", "family", "id", "target_symbols"):
            if case.get(key) != definition[key]:
                raise ValueError(f"Dragon HVAC misc case definition drifted: {definition['id']}")
        python = case.get("python")
        if not isinstance(python, dict) or python.get("outcome") != "observed":
            raise ValueError(f"Dragon HVAC misc Python outcome drifted: {definition['id']}")
        if python.get("facts_sha256") != canonical_sha256(python.get("facts")):
            raise ValueError(f"Dragon HVAC misc fact self receipt drifted: {definition['id']}")
        case_without_hash = {key: item for key, item in case.items() if key != "case_sha256"}
        if case.get("case_sha256") != canonical_sha256(case_without_hash):
            raise ValueError(f"Dragon HVAC misc case self receipt drifted: {definition['id']}")
    fact_hashes = {item["id"]: item["python"]["facts_sha256"] for item in cases}
    case_hashes = {item["id"]: item["case_sha256"] for item in cases}
    cases_hash = canonical_sha256(cases)
    if value["fact_sha256"] != fact_hashes or value["case_sha256"] != case_hashes:
        raise ValueError("Dragon HVAC misc fact/case hash maps drifted.")
    if value["cases_sha256"] != cases_hash:
        raise ValueError("Dragon HVAC misc case aggregate self receipt drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise ValueError("Dragon HVAC misc fact pin map drifted.")
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise ValueError("Dragon HVAC misc case pin map drifted.")
    if EXPECTED_CASES_SHA256 and cases_hash != EXPECTED_CASES_SHA256:
        raise ValueError("Dragon HVAC misc case aggregate pin drifted.")

    if value["consumer_contract"] != _consumer_contract(signatures, partition_hashes):
        raise ValueError("Dragon HVAC misc consumer contract drifted.")
    if value["native_review"] != _native_review():
        raise ValueError("Dragon HVAC misc native review drifted.")
    support_receipts = partitions["resolved"]
    pv_support = [
        item for item in support_receipts if item["inventory_index"] == 761
    ]
    if value["support"] != _pv_support_fixture_receipt(pv_support):
        raise ValueError("Dragon HVAC misc photovoltaic support drifted.")
    if value["symbols"] != _symbol_contract(targets):
        raise ValueError("Dragon HVAC misc symbol contract drifted.")
    if len({item["assertion_id"] for item in value["symbols"]}) != 15:
        raise ValueError("Dragon HVAC misc assertion IDs are not unique.")
    if any(".Generate" in item["native_route"] for item in value["symbols"]):
        raise ValueError("An internal Generate route was claimed.")
    _scan_deterministic_payload(value)


def main() -> None:
    arguments = parse_args()
    value = build_oracle(arguments.inventory, arguments.upstream_commit)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    arguments.output.write_text(
        strict_json_dumps(value, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )


if __name__ == "__main__":
    main()

"""Generate pinned core observations for legacy dragon HVAC sources and towers.

The bounded corpus executes 59 unresolved declarations from
``src/idragon/dragon/hvac.py``.  It preserves the thirteen already-resolved
IDF emitters through a hash-pinned supporting oracle and keeps the two enum
``__str__`` declarations explicitly out of scope.  Imports run from two
byte-identical copies below the repository ``temp`` tree so that no observation
depends on the checkout location.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
from enum import Enum
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
from typing import Any, Iterator


SCHEMA = "dragons.python-reference.dragon-hvac-source-tower-core.v1"
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
    "generate_dragon_hvac_source_system_to_idf_object_oracle.py"
)
EXPECTED_SUPPORT_GENERATOR_BYTES = 66_475
EXPECTED_SUPPORT_GENERATOR_SHA256 = (
    "sha256:f8c3a031304554ecd43381867188c29bf38c2ce0ebf4bf284c394792f7817159"
)
SUPPORT_FIXTURE_RELATIVE_PATH = (
    "fixtures/reference/python-0.7.0/"
    "dragon-hvac-source-system-to-idf-object-oracle.json"
)
EXPECTED_SUPPORT_FIXTURE_BYTES = 3_927_710
EXPECTED_SUPPORT_FIXTURE_SHA256 = (
    "sha256:2fbc3ad2d810dee6b3e88f8b6e8c119e8ce709abf0c534233343e486f7bf9c7f"
)
EXPECTED_SUPPORT_CASES_SHA256 = (
    "sha256:755e2115db65a100fe1b4249c4b4507719e5083aa2ea22939955a7aae53c5c07"
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
        raise RuntimeError("Pinned source-system IDF support generator drifted.")
    spec = importlib.util.spec_from_file_location(
        "_dragons_dragon_hvac_source_tower_support",
        SUPPORT_GENERATOR_PATH,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load HVAC support: {SUPPORT_GENERATOR_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.HVAC_SOURCE_PATH != SOURCE_PATH
        or module.EXPECTED_HVAC_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
        or module.EXPECTED_HVAC_AST_SHA256 != EXPECTED_SOURCE_AST_SHA256
    ):
        raise RuntimeError("Pinned source-system IDF support identity drifted.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
SOURCE_SPECS = SUPPORT.SOURCE_SPECS
EXPECTED_DEPENDENCIES = SUPPORT.EXPECTED_DEPENDENCIES
REQUIRED_PYTHON = SUPPORT.REQUIRED_PYTHON
REQUIRED_HASH_ALGORITHM = SUPPORT.REQUIRED_HASH_ALGORITHM
REQUIRED_HASH_WIDTH_BITS = SUPPORT.REQUIRED_HASH_WIDTH_BITS


TARGET_IDENTITIES = (
    (641, "AbsorptionChiller"),
    (642, "AbsorptionChiller.__init__"),
    (643, "AbsorptionChiller.idf_objtypename"),
    (652, "Boiler"),
    (653, "Boiler.__init__"),
    (654, "Boiler.idf_objtypename"),
    (657, "Chiller"),
    (658, "Chiller.__init__"),
    (659, "Chiller.idf_objtypename"),
    (661, "ClosedSingleSpeedCoolingTower"),
    (662, "ClosedSingleSpeedCoolingTower.idf_objtypename"),
    (664, "ClosedTwoSpeedCoolingTower"),
    (665, "ClosedTwoSpeedCoolingTower.idf_objtypename"),
    (667, "CompressorType"),
    (668, "CompressorType.RECIPROCATING"),
    (669, "CompressorType.SCREW"),
    (670, "CompressorType.TURBO"),
    (673, "CoolingTower"),
    (674, "CoolingTower.__init__"),
    (675, "CoolingTower.idf_get_demandbranchlistname"),
    (676, "CoolingTower.idf_get_demandmixername"),
    (677, "CoolingTower.idf_get_demandsplittername"),
    (678, "CoolingTower.idf_get_loopname"),
    (679, "CoolingTower.idf_get_objname"),
    (680, "CoolingTower.idf_get_supplybranchlistname"),
    (681, "CoolingTower.idf_get_supplymixername"),
    (682, "CoolingTower.idf_get_supplysplittername"),
    (683, "CoolingTower.idf_objtypename"),
    (726, "Fuel"),
    (727, "Fuel.COAL"),
    (728, "Fuel.DIESEL"),
    (729, "Fuel.ELECTRICITY"),
    (730, "Fuel.FUELOILNO1"),
    (731, "Fuel.FUELOILNO2"),
    (732, "Fuel.GASOLINE"),
    (733, "Fuel.NATURALGAS"),
    (734, "Fuel.OTHERFUEL1"),
    (735, "Fuel.OTHERFUEL2"),
    (736, "Fuel.PROPANE"),
    (738, "GeothermalHeatPump"),
    (739, "GeothermalHeatPump.idf_objtypename"),
    (740, "HeatPump"),
    (741, "HeatPump.__init__"),
    (742, "HeatPump.idf_objtypename"),
    (744, "OpenSingleSpeedCoolingTower"),
    (745, "OpenSingleSpeedCoolingTower.idf_objtypename"),
    (747, "OpenTwoSpeedCoolingTower"),
    (748, "OpenTwoSpeedCoolingTower.idf_objtypename"),
    (777, "SourceSystem"),
    (778, "SourceSystem.idf_demandbranchlistname"),
    (779, "SourceSystem.idf_demandmixername"),
    (780, "SourceSystem.idf_demandsplittername"),
    (781, "SourceSystem.idf_loopname"),
    (782, "SourceSystem.idf_objname"),
    (783, "SourceSystem.idf_objtypename"),
    (784, "SourceSystem.idf_supplybranchlistname"),
    (785, "SourceSystem.idf_supplymixername"),
    (786, "SourceSystem.idf_supplysplittername"),
    (787, "SourceSystem.idf_terminalunitlistname"),
)
TARGET_INDICES = tuple(index for index, _ in TARGET_IDENTITIES)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_IDENTITIES)

ADJACENT_IDENTITIES = (
    (644, "AbsorptionChiller.to_idf_object", "exception"),
    (655, "Boiler.to_idf_object", "exception"),
    (656, "Boiler.to_idf_object_as_generator", "exception"),
    (660, "Chiller.to_idf_object", "exception"),
    (663, "ClosedSingleSpeedCoolingTower.to_idf_main_object", "exception"),
    (666, "ClosedTwoSpeedCoolingTower.to_idf_main_object", "exception"),
    (671, "CompressorType.__str__", "out_of_scope"),
    (672, "CompressorType.to_idf_curve_object", "exception"),
    (684, "CoolingTower.to_idf_main_object", "exception"),
    (685, "CoolingTower.to_idf_object", "exception"),
    (737, "Fuel.__str__", "out_of_scope"),
    (743, "HeatPump.to_idf_object", "exception"),
    (746, "OpenSingleSpeedCoolingTower.to_idf_main_object", "exception"),
    (749, "OpenTwoSpeedCoolingTower.to_idf_main_object", "exception"),
    (788, "SourceSystem.to_idf_object", "exception"),
)
ADJACENT_INDICES = tuple(index for index, _, _ in ADJACENT_IDENTITIES)
ADJACENT_CLASSIFICATIONS = {
    symbol: classification
    for _, symbol, classification in ADJACENT_IDENTITIES
}
FAMILY_INDICES = tuple(sorted((*TARGET_INDICES, *ADJACENT_INDICES)))
SOURCE_INDICES = tuple(range(641, 815))
DEFERRED_INDICES = tuple(index for index in SOURCE_INDICES if index not in FAMILY_INDICES)
if (
    len(TARGET_INDICES) != 59
    or len(ADJACENT_INDICES) != 15
    or len(FAMILY_INDICES) != 74
    or len(DEFERRED_INDICES) != 100
):
    raise RuntimeError("Dragon HVAC source/tower inventory partition drifted.")

EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:894e31bb538cf8be2269a5b35b04e429ceb28b7fd881a7f6deff9d5166f360c1"
)
EXPECTED_ADJACENT_RECEIPTS_SHA256 = (
    "sha256:6e3440ca7a866008249ce603d92cb4da33cd9baf5f1b50be29e9f24e3207d769"
)

EQUIVALENT_INDICES = (
    643,
    654,
    662,
    665,
    667,
    668,
    669,
    670,
    678,
    679,
    683,
    726,
    727,
    728,
    729,
    730,
    731,
    732,
    733,
    734,
    735,
    736,
    742,
    745,
    748,
    781,
    783,
)
CLASSIFICATIONS = {
    symbol: "equivalent" if index in EQUIVALENT_INDICES else "exception"
    for index, symbol in TARGET_IDENTITIES
}
EXCEPTION_SYMBOLS = {
    symbol for symbol, classification in CLASSIFICATIONS.items()
    if classification == "exception"
}


def _adaptation_reason(symbol: str) -> str:
    if symbol == "Chiller.idf_objtypename":
        return "safe-screw-reformulated-eir-type"
    if symbol.startswith("GeothermalHeatPump"):
        return "functional-native-heatpump-route-for-incomplete-abstract-upstream"
    if symbol in {
        "CoolingTower.idf_get_demandbranchlistname",
        "CoolingTower.idf_get_demandmixername",
        "CoolingTower.idf_get_demandsplittername",
        "CoolingTower.idf_get_supplybranchlistname",
        "CoolingTower.idf_get_supplymixername",
        "CoolingTower.idf_get_supplysplittername",
        "SourceSystem.idf_demandbranchlistname",
        "SourceSystem.idf_demandmixername",
        "SourceSystem.idf_demandsplittername",
        "SourceSystem.idf_supplybranchlistname",
        "SourceSystem.idf_supplymixername",
        "SourceSystem.idf_supplysplittername",
        "SourceSystem.idf_terminalunitlistname",
    }:
        return "public-context-emission-derived-name"
    if symbol == "SourceSystem.idf_objname":
        return "concrete-native-idf-object-name-overrides"
    if symbol.endswith(".__init__") or "." not in symbol:
        return "validated-immutable-entity-id-construction"
    return "reviewed-native-source-tower-adaptation"


ADAPTATIONS = {
    symbol: (
        _adaptation_reason(symbol)
        + "-"
        + str(next(
            index for index, candidate in TARGET_IDENTITIES if candidate == symbol
        ))
    )
    for symbol in EXCEPTION_SYMBOLS
}


def _native_route(symbol: str) -> str:
    prefix = "Dragons.InvisibleDragon.Hvac."
    fuel_members = {
        "COAL": "Coal",
        "DIESEL": "Diesel",
        "ELECTRICITY": "Electricity",
        "FUELOILNO1": "FuelOilNo1",
        "FUELOILNO2": "FuelOilNo2",
        "GASOLINE": "Gasoline",
        "NATURALGAS": "NaturalGas",
        "OTHERFUEL1": "OtherFuel1",
        "OTHERFUEL2": "OtherFuel2",
        "PROPANE": "Propane",
    }
    compressor_members = {
        "RECIPROCATING": "Reciprocating",
        "SCREW": "Screw",
        "TURBO": "Turbo",
    }
    if symbol == "Fuel":
        return prefix + "Fuel"
    if symbol.startswith("Fuel."):
        return prefix + "Fuel." + fuel_members[symbol.split(".", 1)[1]]
    if symbol == "CompressorType":
        return prefix + "CompressorType"
    if symbol.startswith("CompressorType."):
        return prefix + "CompressorType." + compressor_members[symbol.split(".", 1)[1]]
    owner, separator, member = symbol.partition(".")
    owner_route = prefix + owner
    if not separator:
        return owner_route
    if member == "__init__":
        if owner == "CoolingTower":
            return (
                prefix
                + "OpenSingleSpeedCoolingTower(...), OpenTwoSpeedCoolingTower(...), "
                + "ClosedSingleSpeedCoolingTower(...), ClosedTwoSpeedCoolingTower(...)"
            )
        return owner_route + "(...)"
    if member == "idf_objtypename":
        return owner_route + ".IdfObjectType"
    if owner == "CoolingTower" and member == "idf_get_objname":
        return owner_route + ".ObjectNameFor(SourceSystem)"
    if owner == "CoolingTower" and member == "idf_get_loopname":
        return owner_route + ".LoopNameFor(SourceSystem)"
    if owner == "CoolingTower":
        return owner_route + ".ToIdfObjects(...) -> public IdfObject fields"
    if owner == "SourceSystem" and member == "idf_loopname":
        return owner_route + ".LoopName"
    if owner == "SourceSystem" and member == "idf_objname":
        return owner_route + ".IdfObjectName"
    if owner == "SourceSystem" and member == "idf_terminalunitlistname":
        return prefix + "HeatPump.TerminalUnitListName"
    if owner == "SourceSystem":
        return owner_route + ".ToIdfObjects(...) -> public IdfObject fields"
    raise RuntimeError(f"No public native route for {symbol}.")


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}

PREFIX = "dragon-hvac-source-tower-core."


def _owned(owner: str) -> tuple[str, ...]:
    return tuple(
        symbol
        for symbol in TARGET_SYMBOLS
        if symbol == owner or symbol.startswith(owner + ".")
    )


CASE_SPECS = (
    ("A01", "absorption-chiller-core", "absorption", _owned("AbsorptionChiller")),
    ("B01", "boiler-core", "boiler", _owned("Boiler")),
    ("C01", "chiller-core", "chiller", _owned("Chiller")),
    ("D01", "compressor-enum", "compressor", _owned("CompressorType")),
    (
        "E01",
        "cooling-tower-concrete-capacity",
        "tower-concrete",
        tuple(
            symbol
            for owner in (
                "ClosedSingleSpeedCoolingTower",
                "ClosedTwoSpeedCoolingTower",
                "OpenSingleSpeedCoolingTower",
                "OpenTwoSpeedCoolingTower",
            )
            for symbol in _owned(owner)
        ),
    ),
    ("F01", "cooling-tower-core-names", "tower-core", _owned("CoolingTower")),
    ("G01", "fuel-enum", "fuel", _owned("Fuel")),
    ("H01", "geothermal-heat-pump-core", "geothermal", _owned("GeothermalHeatPump")),
    ("I01", "heat-pump-core", "heatpump", _owned("HeatPump")),
    ("J01", "source-system-core-names", "source", _owned("SourceSystem")),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 10

EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:e1306d51c36572afadf6dac673623fc251ed27a205819fa2c2444a82d0aa7e8b"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:93cfad21e009eac906a4443998ad214eec82e2136ada5b7cea7888ababf30143"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:2eadd58ac936f71225de5f4181712dd6c8cebafefd12471258f719d02f193a44"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:30c59cb99dfdbf8bcbfe39f823e7573e9e3c259e19ae775ca86dd5a09b6d6012"
)
EXPECTED_FACT_SHA256 = {
    "dragon-hvac-source-tower-core.absorption-chiller-core": "sha256:a18e2e05f3a99a45a2c4a97c4f4ae652e1b542196cdec8a98fcc1d19c14a0505",
    "dragon-hvac-source-tower-core.boiler-core": "sha256:d557036c43e08f47d456b0dfb67967b43b03ac8748f0990e1da19a5ec4e39585",
    "dragon-hvac-source-tower-core.chiller-core": "sha256:85902c31cbc27b28bb4f24ac83d351b2a39b9b19a4b832f6c08c497243c29327",
    "dragon-hvac-source-tower-core.compressor-enum": "sha256:406beba2fdc13b10784996cb350608b17d46d1ba426b303e83e4d5054a0458a3",
    "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity": "sha256:093154fe7b6adc95212d3f9c139e3148d7166d98348fef17c42497d9b072af56",
    "dragon-hvac-source-tower-core.cooling-tower-core-names": "sha256:c647e3382c9a37af793d4c80d537c2b1188ad34a222945fd1a76e3763c7bf67a",
    "dragon-hvac-source-tower-core.fuel-enum": "sha256:8b6044fbd9f678e0a7aa00f19a7d90155dc69bf864a6bb5b52105b94a9be96d3",
    "dragon-hvac-source-tower-core.geothermal-heat-pump-core": "sha256:e203fcb870566e744808738dda87b854705db541f585f2b2f42215122e18f630",
    "dragon-hvac-source-tower-core.heat-pump-core": "sha256:29ec3f4fa65c31d6f8be8b73917d519d9e66e5855e05d2e4efbb6b88b0a7a8f3",
    "dragon-hvac-source-tower-core.source-system-core-names": "sha256:ca494b84caa01187faa7a674e0217b8980cf3e6e1798f87cda981892fc3d0a17",
}
EXPECTED_CASE_SHA256 = {
    "dragon-hvac-source-tower-core.absorption-chiller-core": "sha256:39d2f88b81636ed2e2195c0ec4d725ffc0a2ab198e4efbbf02de1a61c0e8d8c9",
    "dragon-hvac-source-tower-core.boiler-core": "sha256:47d927710da70f1cc91fd75b15aa87204bf4119ab61c4bfe83bbd1af5f1b2c29",
    "dragon-hvac-source-tower-core.chiller-core": "sha256:cc492c1c64cbcf9c73af038611414fbbf785717f0270ad1d18efe587e309db24",
    "dragon-hvac-source-tower-core.compressor-enum": "sha256:b715cb79fab670b3a1f2afff587f66a0bee8962642010181a1af2eb88fc2498b",
    "dragon-hvac-source-tower-core.cooling-tower-concrete-capacity": "sha256:9f084b458a40c99123b9040fcfd8319620a7b9ec2a692824d63f9385bd359c2f",
    "dragon-hvac-source-tower-core.cooling-tower-core-names": "sha256:318e32d8e53dddced2e62bc709960d6e876aad4968da77ea5e7b8cf32b45bd54",
    "dragon-hvac-source-tower-core.fuel-enum": "sha256:22859d5ee16ed6cc66a72a9b54ca6e4ad2b32a379f629e3617013179d913d98d",
    "dragon-hvac-source-tower-core.geothermal-heat-pump-core": "sha256:0398886648e9e9c6941c727979d98ed5c761cb0d63a0ba85bccbda622a71fbaf",
    "dragon-hvac-source-tower-core.heat-pump-core": "sha256:f6ed5624e7964a76caa56a6d3f67a600a85303f7e63069df5db38ff270534602",
    "dragon-hvac-source-tower-core.source-system-core-names": "sha256:41a331927b73f7f359d41b101df0db22c96788b3754a1accd9a7dd273cac9eec",
}
EXPECTED_CASES_SHA256 = (
    "sha256:3e5d0d06f45e91fbbda88b34e9c44944516a7107cf123b9052e373a347944459"
)

NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 7_582,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HvacAbstractions.cs",
        "sha256": "sha256:6c8e16ec5e7ff1fd6c29717112e4dcaa5eb3a0725e20317a3ad35db75131784a",
    },
    {
        "bytes": 18_027,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/SourceSystems.cs",
        "sha256": "sha256:8d302f00514af53816cec9e5ba6b80a8214921b354d86bbbc4d581ec972e026e",
    },
    {
        "bytes": 1_076,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/GeothermalHeatPump.cs",
        "sha256": "sha256:40fcb9c008b953cf54dfa4581c95af4073e0040fc9efcd62598e056c5b2ca80a",
    },
    {
        "bytes": 23_777,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/Chillers.cs",
        "sha256": "sha256:7616675c6750b32ded6edd796576b347703a88103a91dff846ca5a08c65b72be",
    },
    {
        "bytes": 19_554,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/CoolingTowers.cs",
        "sha256": "sha256:007145933076386fcbc44daba8a28c63d3c5467bbd687c9da87f769c969e9d07",
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
    if (
        value["schema"] != "dragons.upstream-public-symbol-inventory.v2"
        or value["upstream_commit"].lower() != commit
    ):
        raise SystemExit("The public-symbol inventory identity drifted.")
    aggregate = canonical_sha256(
        {
            "files": value["files"],
            "scope_sha256": value["scope_sha256"],
            "symbols": value["symbols"],
            "upstream_commit": value["upstream_commit"],
        }
    )
    if aggregate != value["content_sha256"] or aggregate != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory aggregate receipt drifted.")
    source_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    if [item for item in value["files"] if item["path"] == SOURCE_PATH] != [source_file]:
        raise SystemExit("The dragon HVAC source file receipt drifted.")
    source_rows = [
        {**item, "inventory_index": index}
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    if [item["inventory_index"] for item in source_rows] != list(SOURCE_INDICES):
        raise SystemExit("The dragon HVAC declaration range drifted.")
    by_index = {item["inventory_index"]: item for item in source_rows}
    targets = [by_index[index] for index in TARGET_INDICES]
    adjacent = [by_index[index] for index in ADJACENT_INDICES]
    if [(item["inventory_index"], item["symbol"]) for item in targets] != list(TARGET_IDENTITIES):
        raise SystemExit("The dragon HVAC source/tower target identities drifted.")
    if [
        (item["inventory_index"], item["symbol"])
        for item in adjacent
    ] != [(index, symbol) for index, symbol, _ in ADJACENT_IDENTITIES]:
        raise SystemExit("The dragon HVAC source/tower adjacent identities drifted.")
    if canonical_sha256(targets) != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise SystemExit("The source/tower target receipts drifted.")
    if canonical_sha256(adjacent) != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise SystemExit("The source/tower adjacent receipts drifted.")
    if sorted((*TARGET_INDICES, *ADJACENT_INDICES, *DEFERRED_INDICES)) != list(SOURCE_INDICES):
        raise RuntimeError("The dragon HVAC full source partition is incomplete.")
    return {
        "adjacent_receipts": adjacent,
        "content_sha256": aggregate,
        "raw": value,
        "source_file": source_file,
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
        raise RuntimeError("Dragon HVAC source/tower case order drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Dragon HVAC source/tower case IDs are not sorted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Dragon HVAC source/tower cases are not an exact partition.")
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
    if isinstance(value, Enum):
        return {
            "enum_type": type(value).__name__,
            "kind": "enum",
            "name": value.name,
            "value": _encode(value.value),
        }
    raise RuntimeError(f"Unsupported observation value: {type(value).__name__}")


def _attempt(call: Any) -> dict[str, Any]:
    try:
        value = call()
    except Exception as error:
        return {
            "args": [str(argument) for argument in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned", "result_type": type(value).__name__}


def _class_shape(value: type[Any]) -> dict[str, Any]:
    return {
        "abstract": inspect.isabstract(value),
        "abstract_methods": sorted(getattr(value, "__abstractmethods__", ())),
        "bases": [base.__name__ for base in value.__bases__],
        "mro": [item.__name__ for item in value.__mro__],
        "signature": str(inspect.signature(value)),
    }


def _enum_facts(enum_type: type[Enum]) -> dict[str, Any]:
    members = list(enum_type)
    return {
        "class_shape": _class_shape(enum_type),
        "equality_to_declared_value": [member == member.value for member in members],
        "invalid_name": _attempt(lambda: enum_type["NOT_DECLARED"]),
        "invalid_value": _attempt(lambda: enum_type("not-declared")),
        "members": [
            {
                "is_str_instance": isinstance(member, str),
                "lookup_by_name_identity": enum_type[member.name] is member,
                "lookup_by_value_identity": enum_type(member.value) is member,
                "name": member.name,
                "value": member.value,
            }
            for member in members
        ],
        "unique_values": len({member.value for member in members}) == len(members),
    }


def _source_names(value: Any) -> dict[str, str]:
    return {
        "demand_branch_list": value.idf_demandbranchlistname,
        "demand_mixer": value.idf_demandmixername,
        "demand_splitter": value.idf_demandsplittername,
        "loop": value.idf_loopname,
        "object": value.idf_objname,
        "object_type": value.idf_objtypename,
        "supply_branch_list": value.idf_supplybranchlistname,
        "supply_mixer": value.idf_supplymixername,
        "supply_splitter": value.idf_supplysplittername,
        "terminal_unit_list": value.idf_terminalunitlistname,
    }


def _absorption_facts(hvac: Any) -> dict[str, Any]:
    boiler = hvac.Boiler("Generator", hvac.Fuel.NATURALGAS, 0.87)
    tower = hvac.OpenSingleSpeedCoolingTower("Tower", None)
    value = hvac.AbsorptionChiller("Absorber", 0.72, None, boiler, tower)
    permissive = hvac.AbsorptionChiller(
        "",
        0,
        -1,
        object(),
        object(),
        pump_efficiency=float("nan"),
        setpoint_temperature=-50,
    )
    original_cop = value.cop
    value.cop = 1.25
    return {
        "class_shape": _class_shape(hvac.AbsorptionChiller),
        "constructor_signature": str(inspect.signature(hvac.AbsorptionChiller.__init__)),
        "default_state": {
            "capacity": _encode(value.capacity),
            "coolingtower_identity_preserved": value.coolingtower is tower,
            "cop_before_mutation": _encode(original_cop),
            "cop_mutable_after": _encode(value.cop),
            "heatsource_identity_preserved": value.heatsource is boiler,
            "idf_object_type": value.idf_objtypename,
            "name": value.name,
            "pump_efficiency": _encode(value.pump_efficiency),
            "setpoint_temperature": _encode(value.setpoint_temperature),
        },
        "missing_required_arguments": _attempt(
            lambda: hvac.AbsorptionChiller("missing", 1)
        ),
        "permissive_state": {
            "capacity": _encode(permissive.capacity),
            "coolingtower_type": type(permissive.coolingtower).__name__,
            "cop": _encode(permissive.cop),
            "heatsource_type": type(permissive.heatsource).__name__,
            "name": permissive.name,
            "pump_efficiency": _encode(permissive.pump_efficiency),
            "setpoint_temperature": _encode(permissive.setpoint_temperature),
        },
    }


def _boiler_facts(hvac: Any) -> dict[str, Any]:
    value = hvac.Boiler("Boiler", hvac.Fuel.PROPANE, 0.88)
    permissive = hvac.Boiler(
        "",
        "not-a-fuel",
        -1,
        -2,
        pump_efficiency=float("nan"),
        setpoint_temperature=float("inf"),
    )
    original_efficiency = value.efficiency
    value.efficiency = 0.91
    return {
        "class_shape": _class_shape(hvac.Boiler),
        "constructor_signature": str(inspect.signature(hvac.Boiler.__init__)),
        "default_state": {
            "capacity": _encode(value.capacity),
            "efficiency_before_mutation": _encode(original_efficiency),
            "efficiency_mutable_after": _encode(value.efficiency),
            "fuel": _encode(value.fuel),
            "idf_object_type": value.idf_objtypename,
            "name": value.name,
            "pump_efficiency": _encode(value.pump_efficiency),
            "setpoint_temperature": _encode(value.setpoint_temperature),
        },
        "permissive_state": {
            "capacity": _encode(permissive.capacity),
            "efficiency": _encode(permissive.efficiency),
            "fuel": _encode(permissive.fuel),
            "fuel_not_coerced": type(permissive.fuel) is str,
            "name": permissive.name,
            "pump_efficiency": _encode(permissive.pump_efficiency),
            "setpoint_temperature": _encode(permissive.setpoint_temperature),
        },
    }


def _chiller_facts(hvac: Any) -> dict[str, Any]:
    tower = hvac.ClosedSingleSpeedCoolingTower("Chiller Tower", None)
    value = hvac.Chiller("Chiller", 4.2, None, "turbo", tower)
    screw = hvac.Chiller("Screw", 3.8, 50_000, "screw", tower)
    duck = object()
    permissive = hvac.Chiller(
        "",
        -1,
        -2,
        hvac.CompressorType.RECIPROCATING,
        duck,
        pump_efficiency=float("nan"),
        setpoint_temperature=float("inf"),
    )
    value.cop = 9.0
    return {
        "class_shape": _class_shape(hvac.Chiller),
        "constructor_signature": str(inspect.signature(hvac.Chiller.__init__)),
        "default_state": {
            "capacity": _encode(value.capacity),
            "compressor": _encode(value.compressor),
            "coolingtower_identity_preserved": value.coolingtower is tower,
            "cop_mutable_after": _encode(value.cop),
            "idf_object_type": value.idf_objtypename,
            "name": value.name,
            "pump_efficiency": _encode(value.pump_efficiency),
            "setpoint_temperature": _encode(value.setpoint_temperature),
        },
        "invalid_compressor": _attempt(
            lambda: hvac.Chiller("bad", 3, None, "scroll", tower)
        ),
        "missing_capacity": _attempt(
            lambda: hvac.Chiller("missing", 3, compressor="turbo", coolingtower=tower)
        ),
        "permissive_state": {
            "capacity": _encode(permissive.capacity),
            "coolingtower_identity_preserved": permissive.coolingtower is duck,
            "cop": _encode(permissive.cop),
            "name": permissive.name,
            "pump_efficiency": _encode(permissive.pump_efficiency),
            "setpoint_temperature": _encode(permissive.setpoint_temperature),
        },
        "screw_object_type": screw.idf_objtypename,
    }


def _tower_capacity_record(
    tower_type: type[Any],
    branch: str,
    tower_capacity: float | None,
    source_capacity: float | None,
) -> dict[str, Any]:
    tower = tower_type("Unused Tower Label", tower_capacity)
    source = SimpleNamespace(
        capacity=source_capacity,
        idf_objname="Chiller_named_Capacity Source",
    )
    first = tower.to_idf_main_object(source)
    second = tower.to_idf_main_object(source)
    if len(first) != 1 or len(second) != 1:
        raise RuntimeError("A concrete cooling tower must return one main object.")
    item = first[0]
    if tower_type.__name__.endswith("SingleSpeedCoolingTower"):
        capacity_field = "Nominal Capacity"
    else:
        capacity_field = "High Speed Nominal Capacity"
    return {
        "branch": branch,
        "capacity_field": capacity_field,
        "capacity_field_present": capacity_field in item.data,
        "capacity_value": _encode(item.data.get(capacity_field)),
        "fresh_object": first[0] is not second[0],
        "fresh_result": first is not second,
        "name": item.data.get("Name"),
        "object_type": item.idd.name,
        "source_capacity": _encode(source_capacity),
        "state_unchanged": (
            tower.capacity == tower_capacity
            and tower.name == "Unused Tower Label"
            and tower.pump_efficiency == 0.9
        ),
        "tower_capacity": _encode(tower_capacity),
    }


def _concrete_tower_facts(hvac: Any) -> dict[str, Any]:
    tower_types = (
        hvac.ClosedSingleSpeedCoolingTower,
        hvac.ClosedTwoSpeedCoolingTower,
        hvac.OpenSingleSpeedCoolingTower,
        hvac.OpenTwoSpeedCoolingTower,
    )
    families = []
    for tower_type in tower_types:
        value = tower_type("Default", None)
        families.append(
            {
                "branches": [
                    _tower_capacity_record(
                        tower_type, "tower-capacity", 111_000.0, 222_000.0
                    ),
                    _tower_capacity_record(
                        tower_type, "source-capacity", None, 222_000.0
                    ),
                    _tower_capacity_record(
                        tower_type, "fallback-capacity", None, None
                    ),
                ],
                "class_shape": _class_shape(tower_type),
                "constructor_signature": str(inspect.signature(tower_type)),
                "default_pump_efficiency": _encode(value.pump_efficiency),
                "idf_object_type": value.idf_objtypename,
                "missing_capacity": _attempt(lambda tower_type=tower_type: tower_type("missing")),
                "type": tower_type.__name__,
            }
        )
    permissive = hvac.OpenSingleSpeedCoolingTower(
        "", -1, pump_efficiency=float("nan")
    )
    return {
        "families": families,
        "permissive_state": {
            "capacity": _encode(permissive.capacity),
            "name": permissive.name,
            "pump_efficiency": _encode(permissive.pump_efficiency),
        },
    }


def _tower_core_facts(hvac: Any) -> dict[str, Any]:
    class ProbeTower(hvac.CoolingTower):
        @property
        def idf_objtypename(self) -> str:
            return "Probe:Tower"

        def to_idf_main_object(self, chiller: Any) -> list[Any]:
            return []

    source = SimpleNamespace(idf_objname="Chiller_named_Name Context")
    value = ProbeTower("Ignored Tower Name", None)
    permissive = ProbeTower("", -1, pump_efficiency=float("nan"))
    names = {
        "demand_branch_list": value.idf_get_demandbranchlistname(source),
        "demand_mixer": value.idf_get_demandmixername(source),
        "demand_splitter": value.idf_get_demandsplittername(source),
        "loop": value.idf_get_loopname(source),
        "object": value.idf_get_objname(source),
        "supply_branch_list": value.idf_get_supplybranchlistname(source),
        "supply_mixer": value.idf_get_supplymixername(source),
        "supply_splitter": value.idf_get_supplysplittername(source),
    }
    return {
        "abstract_idf_object_type_body": _encode(
            hvac.CoolingTower.__dict__["idf_objtypename"].fget(object())
        ),
        "abstract_main_body": _encode(
            hvac.CoolingTower.__dict__["to_idf_main_object"](object(), source)
        ),
        "class_shape": _class_shape(hvac.CoolingTower),
        "constructor_signature": str(inspect.signature(hvac.CoolingTower.__init__)),
        "direct_instantiation": _attempt(
            lambda: hvac.CoolingTower("abstract", None)
        ),
        "names": names,
        "permissive_state": {
            "capacity": _encode(permissive.capacity),
            "name": permissive.name,
            "pump_efficiency": _encode(permissive.pump_efficiency),
        },
        "probe_object_type": value.idf_objtypename,
        "tower_name_not_used_in_context_names": all(
            "Ignored Tower Name" not in name for name in names.values()
        ),
    }


def _geothermal_facts(hvac: Any) -> dict[str, Any]:
    return {
        "class_shape": _class_shape(hvac.GeothermalHeatPump),
        "direct_idf_object_type_body": _encode(
            hvac.GeothermalHeatPump.__dict__["idf_objtypename"].fget(object())
        ),
        "direct_instantiation": _attempt(lambda: hvac.GeothermalHeatPump()),
        "inherits_source_system": issubclass(
            hvac.GeothermalHeatPump, hvac.SourceSystem
        ),
        "to_idf_inherited_abstract": (
            "to_idf_object" in hvac.GeothermalHeatPump.__abstractmethods__
        ),
    }


def _heat_pump_state(value: Any) -> dict[str, Any]:
    return {
        "cooling_capacity": _encode(value.cooling_capacity),
        "cooling_cop": _encode(value.cooling_cop),
        "fuel": _encode(value.fuel),
        "heating_capacity": _encode(value.heating_capacity),
        "heating_cop": _encode(value.heating_cop),
        "idf_object_type": value.idf_objtypename,
        "name": value.name,
    }


def _heat_pump_facts(hvac: Any) -> dict[str, Any]:
    value = hvac.HeatPump("Heat Pump", "Electricity", 3.4, 2.9)
    permissive = hvac.HeatPump(
        "",
        hvac.Fuel.NATURALGAS,
        float("nan"),
        0,
        -10,
        -20,
    )
    before = _heat_pump_state(value)
    value.heating_cop = 8.0
    return {
        "class_shape": _class_shape(hvac.HeatPump),
        "constructor_signature": str(inspect.signature(hvac.HeatPump.__init__)),
        "invalid_fuel": _attempt(
            lambda: hvac.HeatPump("bad", "electricity", 3, 3)
        ),
        "mutated_heating_cop": _encode(value.heating_cop),
        "permissive_state": _heat_pump_state(permissive),
        "state_before_mutation": before,
        "string_fuel_coerced": value.fuel is hvac.Fuel.ELECTRICITY,
    }


def _source_system_facts(hvac: Any) -> dict[str, Any]:
    class ProbeSource(hvac.SourceSystem):
        def __init__(self, name: str) -> None:
            self.name = name

        @property
        def idf_objtypename(self) -> str:
            return "Probe:Source"

        def to_idf_object(self) -> list[Any]:
            return []

    value = ProbeSource("Source Name")
    before = _source_names(value)
    first_result = value.to_idf_object()
    second_result = value.to_idf_object()
    value.name = "Renamed Source"
    after = _source_names(value)
    return {
        "abstract_idf_object_type_body": _encode(
            hvac.SourceSystem.__dict__["idf_objtypename"].fget(object())
        ),
        "abstract_to_idf_body": _encode(
            hvac.SourceSystem.__dict__["to_idf_object"](object())
        ),
        "class_shape": _class_shape(hvac.SourceSystem),
        "direct_instantiation": _attempt(lambda: hvac.SourceSystem()),
        "fresh_probe_results": first_result is not second_result,
        "names_after_mutation": after,
        "names_before_mutation": before,
        "probe_class_shape": _class_shape(ProbeSource),
    }


def _execute_cases(hvac: Any) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _absorption_facts(hvac),
        EXPECTED_CASE_IDS[1]: _boiler_facts(hvac),
        EXPECTED_CASE_IDS[2]: _chiller_facts(hvac),
        EXPECTED_CASE_IDS[3]: _enum_facts(hvac.CompressorType),
        EXPECTED_CASE_IDS[4]: _concrete_tower_facts(hvac),
        EXPECTED_CASE_IDS[5]: _tower_core_facts(hvac),
        EXPECTED_CASE_IDS[6]: _enum_facts(hvac.Fuel),
        EXPECTED_CASE_IDS[7]: _geothermal_facts(hvac),
        EXPECTED_CASE_IDS[8]: _heat_pump_facts(hvac),
        EXPECTED_CASE_IDS[9]: _source_system_facts(hvac),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("Dragon HVAC source/tower observation order drifted.")
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
    if isinstance(descriptor, Enum):
        return {
            "enum_name": descriptor.name,
            "enum_type": type(descriptor).__name__,
            "kind": "constant",
            "value": descriptor.value,
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


def _module_name(source_path: str) -> str:
    relative = Path(source_path).relative_to("src").with_suffix("")
    parts = list(relative.parts)
    if parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _audit_loaded_modules(imported_root: Path) -> list[dict[str, str]]:
    receipts: list[dict[str, str]] = []
    for source in SOURCE_SPECS:
        module_name = _module_name(source["path"])
        module = sys.modules.get(module_name)
        if module is None or not getattr(module, "__file__", None):
            raise SystemExit(f"Pinned local module was not loaded: {module_name}")
        resolved = Path(module.__file__).resolve()
        expected = _source_file(imported_root, source).resolve()
        receipt = {
            "ast_sha256": source["ast_sha256"],
            "module": module_name,
            "path": source["path"],
            "source_sha256": sha256_file(resolved),
        }
        if resolved != expected or receipt["source_sha256"] != source["source_sha256"]:
            raise SystemExit(f"Loaded local module receipt drifted: {module_name}")
        receipts.append(receipt)
    return receipts


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
            loaded = _audit_loaded_modules(imported_root)
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
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        source_root = Path(entry)
        if all(
            _source_file(source_root, source).is_file()
            and sha256_file(_source_file(source_root, source)) == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(source_root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def _support_receipt() -> dict[str, Any]:
    repository_root = Path(__file__).resolve().parents[2]
    fixture_path = repository_root / SUPPORT_FIXTURE_RELATIVE_PATH
    if (
        SUPPORT_GENERATOR_PATH.stat().st_size != EXPECTED_SUPPORT_GENERATOR_BYTES
        or sha256_file(SUPPORT_GENERATOR_PATH) != EXPECTED_SUPPORT_GENERATOR_SHA256
        or not fixture_path.is_file()
        or fixture_path.stat().st_size != EXPECTED_SUPPORT_FIXTURE_BYTES
        or sha256_file(fixture_path) != EXPECTED_SUPPORT_FIXTURE_SHA256
    ):
        raise SystemExit("Pinned source-system IDF supporting resources drifted.")
    fixture = load_json_without_duplicates(fixture_path)
    SUPPORT.validate_oracle(fixture)
    if (
        fixture["schema"] != SUPPORT.SCHEMA
        or fixture["cases_sha256"] != EXPECTED_SUPPORT_CASES_SHA256
        or len(fixture["cases"]) != 20
    ):
        raise SystemExit("Pinned source-system IDF supporting contract drifted.")
    resolved_symbols = [
        symbol
        for _, symbol, classification in ADJACENT_IDENTITIES
        if classification == "exception"
    ]
    if set(resolved_symbols) != set(SUPPORT.TARGET_SYMBOLS):
        raise RuntimeError("Supporting IDF oracle does not close resolved adjacency.")
    return {
        "case_count": 20,
        "cases_sha256": EXPECTED_SUPPORT_CASES_SHA256,
        "fixture": {
            "bytes": EXPECTED_SUPPORT_FIXTURE_BYTES,
            "path": SUPPORT_FIXTURE_RELATIVE_PATH,
            "sha256": EXPECTED_SUPPORT_FIXTURE_SHA256,
        },
        "generator": {
            "bytes": EXPECTED_SUPPORT_GENERATOR_BYTES,
            "path": "tools/python-reference/"
            "generate_dragon_hvac_source_system_to_idf_object_oracle.py",
            "sha256": EXPECTED_SUPPORT_GENERATOR_SHA256,
        },
        "resolved_adjacent_symbols": resolved_symbols,
        "schema": SUPPORT.SCHEMA,
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
        "public_production_routes_only": True,
        "python_executes_native_runtime": False,
        "routes_sha256": canonical_sha256(NATIVE_ROUTES),
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        "source_receipts_sha256": canonical_sha256(list(NATIVE_SOURCE_RECEIPTS)),
    }
    digest = canonical_sha256(result)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise SystemExit("Pinned source/tower native review drifted.")
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
            f"dragon-hvac-source-tower-core-{item['inventory_index']}-"
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
        raise RuntimeError("Dragon HVAC source/tower coverage drifted.")
    return result


def _expected_contract(
    receipts: list[dict[str, Any]],
    signatures: dict[str, Any],
) -> dict[str, Any]:
    assertions = _assertion_ids(receipts)
    counts = Counter(CLASSIFICATIONS.values())
    expectations = {
        symbol: {
            "adaptation": ADAPTATIONS.get(symbol, "not_applicable"),
            "assertion_id": assertions[symbol],
            "classification": CLASSIFICATIONS[symbol],
            "native_route": NATIVE_ROUTES[symbol],
        }
        for symbol in TARGET_SYMBOLS
    }
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": assertions,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {
            "equivalent": counts["equivalent"],
            "exception": counts["exception"],
        },
        "classifications": CLASSIFICATIONS,
        "closure": {
            "adjacent_classifications": ADJACENT_CLASSIFICATIONS,
            "adjacent_count": len(ADJACENT_INDICES),
            "adjacent_indices": list(ADJACENT_INDICES),
            "deferred_count": len(DEFERRED_INDICES),
            "deferred_indices": list(DEFERRED_INDICES),
            "exact_one_case_target_partition": True,
            "full_hvac_source_partition": True,
            "full_source_tower_family_closure": True,
            "source_declaration_count": len(SOURCE_INDICES),
            "source_tower_family_count": len(FAMILY_INDICES),
            "target_count": len(TARGET_INDICES),
            "target_indices": list(TARGET_INDICES),
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "coverage_by_symbol": _coverage_by_symbol(),
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "exact_cpython_behavior_oracle": True,
            "expected_receipt_count": len(TARGET_INDICES),
            "native_runtime_executed_by_python_oracle": False,
            "path_independent_relocated_import": True,
            "resolved_idf_behavior_reused_from_support": True,
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
    work_root = (
        Path(__file__).resolve().parents[2]
        / "temp"
        / "reference"
        / "dragon-hvac-source-tower-core-work"
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
        raise RuntimeError("Source/tower signatures changed after relocation.")
    if observations != relocated_observations:
        raise RuntimeError("Source/tower observations changed after relocation.")
    if loaded_modules != relocated_modules:
        raise RuntimeError("Source/tower loaded modules changed after relocation.")

    signatures_hash = canonical_sha256(signatures)
    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise SystemExit("Pinned source/tower runtime signatures drifted.")
    if (
        EXPECTED_LOADED_LOCAL_MODULES_SHA256
        and modules_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise SystemExit("Pinned source/tower loaded modules drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise SystemExit("Pinned source/tower relocated observations drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned source/tower fact hashes drifted.\n"
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
            "Pinned source/tower case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned source/tower aggregate case hash drifted.")

    result = {
        "adjacent_receipts": inventory["adjacent_receipts"],
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(
            inventory["target_receipts"], signatures
        ),
        "fact_sha256": fact_hashes,
        "native_review": _native_review(),
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "support": _support_receipt(),
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "commit": EXPECTED_UPSTREAM_COMMIT,
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
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "path": SOURCE_PATH,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
            "target_receipts_sha256": EXPECTED_TARGET_RECEIPTS_SHA256,
            "adjacent_receipts_sha256": EXPECTED_ADJACENT_RECEIPTS_SHA256,
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
            "adjacent_receipts",
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "fact_sha256",
            "native_review",
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
        raise RuntimeError("Dragon HVAC source/tower schema drifted.")
    target_receipts = value["target_receipts"]
    adjacent_receipts = value["adjacent_receipts"]
    if (
        not isinstance(target_receipts, list)
        or canonical_sha256(target_receipts) != EXPECTED_TARGET_RECEIPTS_SHA256
        or [(item["inventory_index"], item["symbol"]) for item in target_receipts]
        != list(TARGET_IDENTITIES)
    ):
        raise RuntimeError("Dragon HVAC source/tower target receipts drifted.")
    if (
        not isinstance(adjacent_receipts, list)
        or canonical_sha256(adjacent_receipts) != EXPECTED_ADJACENT_RECEIPTS_SHA256
        or [(item["inventory_index"], item["symbol"]) for item in adjacent_receipts]
        != [(index, symbol) for index, symbol, _ in ADJACENT_IDENTITIES]
    ):
        raise RuntimeError("Dragon HVAC source/tower adjacent receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in target_receipts]:
        raise RuntimeError("Dragon HVAC source/tower symbol descriptors drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("Dragon HVAC source/tower runtime signatures are absent.")
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise RuntimeError("Pinned source/tower runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(target_receipts, signatures):
        raise RuntimeError("Dragon HVAC source/tower consumer contract drifted.")
    if value["runtime"] != _runtime_receipt():
        raise RuntimeError("Dragon HVAC source/tower runtime receipt drifted.")
    if value["support"] != _support_receipt():
        raise RuntimeError("Dragon HVAC source/tower supporting receipt drifted.")
    if value["native_review"] != _native_review():
        raise RuntimeError("Dragon HVAC source/tower native review drifted.")

    upstream = value["upstream"]
    _require_keys(
        upstream,
        {
            "adjacent_receipts_sha256",
            "commit",
            "inventory",
            "isolated_import",
            "source",
            "target_receipts_sha256",
        },
        "upstream",
    )
    expected_static = {
        "adjacent_receipts_sha256": EXPECTED_ADJACENT_RECEIPTS_SHA256,
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
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
            raise RuntimeError(f"Dragon HVAC source/tower upstream field drifted: {key}")
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
        raise RuntimeError("Dragon HVAC source/tower relocation contract drifted.")
    if (
        EXPECTED_LOADED_LOCAL_MODULES_SHA256
        and isolated["loaded_local_modules_sha256"]
        != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise RuntimeError("Pinned source/tower loaded modules drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and isolated["relocated_observations_sha256"]
        != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise RuntimeError("Pinned source/tower relocation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Dragon HVAC source/tower case order/count drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        _require_keys(case, {*definition, "python"}, f"case {definition['id']}")
        if any(case[key] != expected for key, expected in definition.items()):
            raise RuntimeError(f"Source/tower case definition drifted: {definition['id']}")
        python = case["python"]
        _require_keys(python, {"facts", "facts_sha256", "outcome"}, "python")
        if python["outcome"] != "observed":
            raise RuntimeError(f"Source/tower outcome drifted: {definition['id']}")
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(f"Source/tower inline fact hash drifted: {definition['id']}")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Dragon HVAC source/tower fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned source/tower fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("Dragon HVAC source/tower case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned source/tower case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Dragon HVAC source/tower aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned source/tower aggregate hash drifted.")

    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Dragon HVAC source/tower target closure drifted.")
    closure = value["consumer_contract"]["closure"]
    if (
        closure["target_indices"] != list(TARGET_INDICES)
        or closure["adjacent_indices"] != list(ADJACENT_INDICES)
        or closure["deferred_indices"] != list(DEFERRED_INDICES)
        or sorted(
            (
                *closure["target_indices"],
                *closure["adjacent_indices"],
                *closure["deferred_indices"],
            )
        )
        != list(SOURCE_INDICES)
    ):
        raise RuntimeError("Dragon HVAC source/tower full source closure drifted.")
    SUPPORT._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Dragon HVAC source/tower strict JSON round trip drifted.")


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
    args = parse_args()
    _validate_generation_runtime()
    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    result = build_oracle(inventory, args.upstream_commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    counts = Counter(CLASSIFICATIONS.values())
    print(
        f"Wrote {len(result['cases'])} dragon HVAC source/tower cases covering "
        f"{len(TARGET_INDICES)} targets: {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception, aggregate {result['cases_sha256']}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

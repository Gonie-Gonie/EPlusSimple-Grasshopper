"""Generate the pinned InvisibleDragon supply-system behavior oracle.

This bounded corpus executes exactly 49 public declarations from the legacy
``src/idragon/dragon/hvac.py`` supply families.  Eight already-classified
adjacent declarations complete the nine-family source closure without being
promoted back into the target set.  The pinned upstream package is imported
from an isolated copy and then from a second byte-identical relocated tree.
"""

from __future__ import annotations

import argparse
from collections import Counter
import copy
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
from types import SimpleNamespace
from typing import Any, Callable


SCHEMA = "dragons.python-reference.dragon-hvac-supply-core.v1"
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

BASE_PATH = Path(__file__).resolve().with_name(
    "generate_dragon_model_assembly_oracle.py"
)
EXPECTED_BASE_BYTES = 76_569
EXPECTED_BASE_SHA256 = (
    "sha256:4bcb0c46d810665e5872e45db102468e9bcbdacdab76aa6e00511448417aa8c5"
)


def _raw_file_sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def _load_base() -> Any:
    if (
        BASE_PATH.stat().st_size != EXPECTED_BASE_BYTES
        or _raw_file_sha256(BASE_PATH) != EXPECTED_BASE_SHA256
    ):
        raise RuntimeError("Pinned dragon model-assembly support receipt drifted.")
    spec = importlib.util.spec_from_file_location(
        "_dragons_dragon_hvac_supply_core_base", BASE_PATH
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load supply-core oracle support: {BASE_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Supply-core oracle support is not exactly pinned.")
    return module


BASE = _load_base()
strict_json_dumps = BASE.strict_json_dumps
canonical_sha256 = BASE.canonical_sha256
sha256_file = BASE.sha256_file
load_json_without_duplicates = BASE.SUPPORT.load_json_without_duplicates

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
EXPECTED_DEPENDENCIES = dict(BASE.EXPECTED_DEPENDENCIES)

TARGET_INDEX_SYMBOLS = (
    (645, "AirHandlingUnit"),
    (647, "AirHandlingUnit.__init__"),
    (648, "AirHandlingUnit.coolable"),
    (649, "AirHandlingUnit.heatable"),
    (650, "AirHandlingUnit.idf_objtypename"),
    (651, "AirHandlingUnit.to_idf_object"),
    (700, "ElectricRadiantFloor"),
    (701, "ElectricRadiantFloor.__init__"),
    (702, "ElectricRadiantFloor.coolable"),
    (703, "ElectricRadiantFloor.heatable"),
    (704, "ElectricRadiantFloor.idf_objtypename"),
    (705, "ElectricRadiantFloor.source"),
    (706, "ElectricRadiantFloor.to_idf_object"),
    (707, "ElectricRadiator"),
    (708, "ElectricRadiator.__init__"),
    (709, "ElectricRadiator.coolable"),
    (710, "ElectricRadiator.heatable"),
    (711, "ElectricRadiator.idf_objtypename"),
    (712, "ElectricRadiator.source"),
    (713, "ElectricRadiator.to_idf_object"),
    (720, "FanCoilUnit"),
    (721, "FanCoilUnit.__init__"),
    (722, "FanCoilUnit.coolable"),
    (723, "FanCoilUnit.heatable"),
    (724, "FanCoilUnit.idf_objtypename"),
    (725, "FanCoilUnit.to_idf_object"),
    (750, "PackagedAirConditioner"),
    (751, "PackagedAirConditioner.coolable"),
    (752, "PackagedAirConditioner.heatable"),
    (762, "RadiantFloor"),
    (763, "RadiantFloor.__init__"),
    (764, "RadiantFloor.coolable"),
    (765, "RadiantFloor.heatable"),
    (766, "RadiantFloor.idf_objtypename"),
    (767, "RadiantFloor.to_idf_object"),
    (768, "Radiator"),
    (769, "Radiator.__init__"),
    (770, "Radiator.coolable"),
    (771, "Radiator.heatable"),
    (772, "Radiator.idf_objtypename"),
    (773, "Radiator.to_idf_object"),
    (789, "SupplyGroup"),
    (797, "SupplySystem"),
    (798, "SupplySystem.idf_get_airinletnodename"),
    (799, "SupplySystem.idf_get_airoutletnodename"),
    (800, "SupplySystem.idf_get_demandbranchname"),
    (801, "SupplySystem.idf_get_objname"),
    (802, "SupplySystem.idf_objtypename"),
    (803, "SupplySystem.to_idf_object"),
)
ADJACENT_INDEX_SYMBOLS = (
    (646, "AirHandlingUnit.__deepcopy__"),
    (790, "SupplyGroup.__init__"),
    (791, "SupplyGroup.coolable"),
    (792, "SupplyGroup.cooling_systems"),
    (793, "SupplyGroup.heatable"),
    (794, "SupplyGroup.heating_systems"),
    (795, "SupplyGroup.sources"),
    (796, "SupplyGroup.to_idf_object"),
)
EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:3c2629b0da4e0e83c079276de2b744707227784b77f1bf78225eb194d8fb5bf2"
)
EXPECTED_ADJACENT_RECEIPTS_SHA256 = (
    "sha256:655edb7852d9b2028431fa50eaa72a753195a55c0fe2df58accda3059c82f40e"
)
EXPECTED_FAMILY_CLOSURE_SHA256 = (
    "sha256:be662099ce93ade0ebfc89fbedd9dfbdbc5be8ff66430d37feaf9de3ae111fe3"
)

TARGET_INDICES = tuple(index for index, _ in TARGET_INDEX_SYMBOLS)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_INDEX_SYMBOLS)
ADJACENT_INDICES = tuple(index for index, _ in ADJACENT_INDEX_SYMBOLS)
ADJACENT_SYMBOLS = tuple(symbol for _, symbol in ADJACENT_INDEX_SYMBOLS)
FAMILY_NAMES = (
    "AirHandlingUnit",
    "ElectricRadiantFloor",
    "ElectricRadiator",
    "FanCoilUnit",
    "PackagedAirConditioner",
    "RadiantFloor",
    "Radiator",
    "SupplyGroup",
    "SupplySystem",
)
if len(TARGET_SYMBOLS) != 49 or len(ADJACENT_SYMBOLS) != 8:
    raise RuntimeError("Supply-core target/adjacent count drifted.")

ADJACENT_EXISTING_STATUS = {
    "AirHandlingUnit.__deepcopy__": "out_of_scope",
    "SupplyGroup.__init__": "exception",
    "SupplyGroup.coolable": "equivalent",
    "SupplyGroup.cooling_systems": "equivalent",
    "SupplyGroup.heatable": "equivalent",
    "SupplyGroup.heating_systems": "equivalent",
    "SupplyGroup.sources": "exception",
    "SupplyGroup.to_idf_object": "exception",
}

EQUIVALENT_SYMBOLS = {
    "AirHandlingUnit.coolable",
    "AirHandlingUnit.heatable",
    "ElectricRadiantFloor.coolable",
    "ElectricRadiantFloor.heatable",
    "ElectricRadiantFloor.source",
    "ElectricRadiator.coolable",
    "ElectricRadiator.heatable",
    "ElectricRadiator.source",
    "FanCoilUnit.coolable",
    "FanCoilUnit.heatable",
    "PackagedAirConditioner.coolable",
    "PackagedAirConditioner.heatable",
    "RadiantFloor.coolable",
    "RadiantFloor.heatable",
    "Radiator.coolable",
    "Radiator.heatable",
    "SupplySystem",
    "SupplySystem.idf_get_objname",
}
CLASSIFICATIONS = {
    symbol: "equivalent" if symbol in EQUIVALENT_SYMBOLS else "exception"
    for symbol in TARGET_SYMBOLS
}
if Counter(CLASSIFICATIONS.values()) != Counter({"equivalent": 18, "exception": 31}):
    raise RuntimeError("Supply-core classification count drifted.")

ASSERTION_IDS = {
    symbol: f"dragon-hvac-supply-core-{index}-{symbol.replace('.', '-').lower()}"
    for index, symbol in TARGET_INDEX_SYMBOLS
}


def _native_route(symbol: str) -> str:
    public_prefix = "Dragons.InvisibleDragon.Hvac."
    member_routes = {
        "coolable": "CanCool",
        "heatable": "CanHeat",
        "source": "Source",
    }
    if symbol == "SupplySystem":
        return public_prefix + "SupplySystem"
    if symbol == "SupplySystem.idf_get_objname":
        return public_prefix + "SupplySystem.ObjectNameFor(Zone)"
    if symbol == "SupplyGroup":
        return (
            public_prefix
            + "SupplyGroup constructor/properties; ZoneHvacAssignment; "
            + "EnergyModel.ToIdfDocument(IddSchema?, EnergyModelIdfOptions?)"
        )
    if "." in symbol:
        owner, member = symbol.rsplit(".", 1)
        if member in member_routes:
            return public_prefix + owner + "." + member_routes[member]
        if member == "__init__":
            return public_prefix + owner + " public constructor and immutable properties"
        if member in {
            "idf_objtypename",
            "to_idf_object",
            "idf_get_airinletnodename",
            "idf_get_airoutletnodename",
            "idf_get_demandbranchname",
        }:
            return (
                "Dragons.InvisibleDragon.Model.EnergyModel.ToIdfDocument("
                "IddSchema?, EnergyModelIdfOptions?) public aggregate emission"
            )
    return public_prefix + symbol + " public constructor and immutable properties"


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}
ADAPTATIONS = {
    symbol: (
        "reviewed-public-aggregate-supply-emission-"
        + hashlib.sha256(symbol.encode("utf-8")).hexdigest()[:8]
    )
    for symbol in TARGET_SYMBOLS
    if CLASSIFICATIONS[symbol] == "exception"
}

SUPPORT_FIXTURES = (
    {
        "bytes": 31_160,
        "path": "fixtures/reference/python-0.7.0/dragon-hvac-supply-group-core-oracle.json",
        "schema": "dragons.python-reference.dragon-hvac-supply-group-core.v1",
        "sha256": "sha256:32f05de2a2ead16e0097d3402577e8bce03f40ea151162a6312000bb4f5a5886",
    },
    {
        "bytes": 22_605,
        "path": "fixtures/reference/python-0.7.0/dragon-hvac-supply-group-to-idf-object-oracle.json",
        "schema": "dragons.python-reference.dragon-hvac-supply-group-to-idf-object.v1",
        "sha256": "sha256:e5e47e5ffa2d725697d8741d05f54655705106e4bb75348c6d9eff46e04715bc",
    },
    {
        "bytes": 15_119,
        "path": "fixtures/reference/python-0.7.0/dragon-model-add-supply-system-oracle.json",
        "schema": "dragons.python-reference.dragon-model-add-supply-system.v1",
        "sha256": "sha256:42ad2d75ce91edd153bd9e07382a03b5095ea0300df227f87e0d0147b377230f",
    },
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
        "bytes": 24_504,
        "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Hvac/HydronicSupplySystems.cs",
        "sha256": "sha256:23a9ffa8e776464c77570ab60854a4fb812de22f84a6ba1e4bf242a45f563269",
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
)

PREFIX = "dragon-hvac-supply-core."
CASE_SPECS = (
    ("A01", "air-handling-unit-state-capability-naming-idf", "air-handling-unit", "AirHandlingUnit"),
    ("EF01", "electric-radiant-floor-state-capability-source-idf", "electric-radiant-floor", "ElectricRadiantFloor"),
    ("E01", "electric-radiator-state-capability-source-idf", "electric-radiator", "ElectricRadiator"),
    ("F01", "fan-coil-source-combinations-capability-idf", "fan-coil-unit", "FanCoilUnit"),
    ("P01", "packaged-air-conditioner-capability-inherited-idf", "packaged-air-conditioner", "PackagedAirConditioner"),
    ("RF01", "radiant-floor-state-capability-validation-idf", "radiant-floor", "RadiantFloor"),
    ("R01", "radiator-state-capability-validation-idf", "radiator", "Radiator"),
    ("G01", "supply-group-availability-order-sources-idf", "supply-group", "SupplyGroup"),
    ("S01", "supply-system-abstract-naming-rules", "supply-system", "SupplySystem"),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)

EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:0e78ed585f5c90816f4870f0e6a6e022eb8f87d5f5657c8781ecda93b8862b82"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:93cfad21e009eac906a4443998ad214eec82e2136ada5b7cea7888ababf30143"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:29eacb2d29f528353302d1afd8e3ef646d7d35886237bb4a3fa494039a4ec36f"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:987acb2c178f1d3aeba1b4ce98fbc3137897dc40bca0b17a33a76275811bdbbd"
)
EXPECTED_SUPPORT_FIXTURES_SHA256 = (
    "sha256:c369c071deddcf1b484399cb2b748f1f2b4e62d70268aa51cfbd44a36e68b4d7"
)
EXPECTED_FACT_SHA256 = {
    "dragon-hvac-supply-core.air-handling-unit-state-capability-naming-idf": "sha256:11285c61555f77b299a5382f2f2c0b89563e3709de20b69dc936bd5469ccbf7a",
    "dragon-hvac-supply-core.electric-radiant-floor-state-capability-source-idf": "sha256:1489da36a6b76efe2b68b10cff98020176e7e49a9d5e6da98fc91adf756d9cfb",
    "dragon-hvac-supply-core.electric-radiator-state-capability-source-idf": "sha256:308644678a1a520855e07c1a8eff86876aa6eb21129c838d165a9224d7394c89",
    "dragon-hvac-supply-core.fan-coil-source-combinations-capability-idf": "sha256:0229dc750a4aa81078b0485259ed08e85c2462ef5dcd631b5e5ddfc90647f60a",
    "dragon-hvac-supply-core.packaged-air-conditioner-capability-inherited-idf": "sha256:91ef0cfd82648b8c50ae335ff94204818f604fa6f186d5d42fabb245e1282196",
    "dragon-hvac-supply-core.radiant-floor-state-capability-validation-idf": "sha256:d7aee55947597439f7f74994f96c5280b3f8bb8618c3db7fd62f4ac821846183",
    "dragon-hvac-supply-core.radiator-state-capability-validation-idf": "sha256:dc0ec639fed2d171f7a83920ba8bf02ccfd286175254dbe097c2b49f91bbe0d4",
    "dragon-hvac-supply-core.supply-group-availability-order-sources-idf": "sha256:e26219508dc3894a97d6c83c229b6c36cf256e2c04a9c34298cf72e8cda0e255",
    "dragon-hvac-supply-core.supply-system-abstract-naming-rules": "sha256:aa47fc70622fd0777e10ef96e7d3b1c98f7f9309bea4f3f238bc2b5f5e7d6d4c",
}
EXPECTED_CASE_SHA256 = {
    "dragon-hvac-supply-core.air-handling-unit-state-capability-naming-idf": "sha256:bd9fa9a6386ebe278115d03fe49f23c14826adcb4c0ddcb81d126db4d535b6dd",
    "dragon-hvac-supply-core.electric-radiant-floor-state-capability-source-idf": "sha256:a2974b2e5a5270a529e276406a2ae5e91f246fdc3cdf6307080e86bd24edbc7f",
    "dragon-hvac-supply-core.electric-radiator-state-capability-source-idf": "sha256:41c5b623e527eb5b64894552d2a6ef55867c14f4d627b48503894e64e54bd561",
    "dragon-hvac-supply-core.fan-coil-source-combinations-capability-idf": "sha256:62984652cf966e2a5fac342b40917764327d3e55cf585fdeba369318192bc6d5",
    "dragon-hvac-supply-core.packaged-air-conditioner-capability-inherited-idf": "sha256:aaa97143c3b3280c9538c97da33b169d4d5eadc263385f051e4f50909c22b669",
    "dragon-hvac-supply-core.radiant-floor-state-capability-validation-idf": "sha256:d98574a76002ee9dbee3fa23f4d302cae2a2e67a1c148f87757754e17e240a76",
    "dragon-hvac-supply-core.radiator-state-capability-validation-idf": "sha256:e45ae73c778fc3411a47b0739ab5dad33f57d325cd6ce7280800a1321943a4ed",
    "dragon-hvac-supply-core.supply-group-availability-order-sources-idf": "sha256:7886f188c4f89c422a27e6b067962709fe33ca52e567fbd66d11bbdef8e03ca4",
    "dragon-hvac-supply-core.supply-system-abstract-naming-rules": "sha256:abd1b10feddd575bfa770667c233814cc8f5d4e93b8999a9670a5dff4a7a0b52",
}
EXPECTED_CASES_SHA256 = (
    "sha256:29eacb2d29f528353302d1afd8e3ef646d7d35886237bb4a3fa494039a4ec36f"
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
    symbols = value.get("symbols")
    if not isinstance(symbols, list):
        raise SystemExit("The public-symbol inventory symbols are malformed.")
    source_file = next(
        (item for item in value.get("files", []) if item.get("path") == SOURCE_PATH),
        None,
    )
    expected_source_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    if source_file != expected_source_file:
        raise SystemExit("The pinned HVAC source inventory receipt drifted.")

    targets = [_receipt(symbols, index) for index in TARGET_INDICES]
    adjacent = [_receipt(symbols, index) for index in ADJACENT_INDICES]
    if tuple((item["inventory_index"], item["symbol"]) for item in targets) != TARGET_INDEX_SYMBOLS:
        raise SystemExit("The exact supply-core target index/symbol closure drifted.")
    if tuple((item["inventory_index"], item["symbol"]) for item in adjacent) != ADJACENT_INDEX_SYMBOLS:
        raise SystemExit("The adjacent supply-core index/symbol closure drifted.")
    if any(item["path"] != SOURCE_PATH for item in targets + adjacent):
        raise SystemExit("A supply-core receipt escaped the pinned HVAC source.")

    expected_family_indices = sorted(TARGET_INDICES + ADJACENT_INDICES)
    actual_family_indices = [
        index
        for index, item in enumerate(symbols)
        if item["path"] == SOURCE_PATH
        and any(
            item["symbol"] == family or item["symbol"].startswith(family + ".")
            for family in FAMILY_NAMES
        )
    ]
    if actual_family_indices != expected_family_indices:
        raise SystemExit("The nine-family declaration closure is no longer exact.")
    if canonical_sha256(targets) != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise SystemExit("The target receipt aggregate drifted.")
    if canonical_sha256(adjacent) != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise SystemExit("The adjacent receipt aggregate drifted.")
    if canonical_sha256(targets + adjacent) != EXPECTED_FAMILY_CLOSURE_SHA256:
        raise SystemExit("The full supply-family closure aggregate drifted.")
    return {
        "adjacent_receipts": adjacent,
        "family_closure_sha256": EXPECTED_FAMILY_CLOSURE_SHA256,
        "source_file": source_file,
        "target_receipts": targets,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions: list[dict[str, Any]] = []
    for code, slug, subfamily, family in CASE_SPECS:
        targets = [
            symbol
            for symbol in TARGET_SYMBOLS
            if symbol == family or symbol.startswith(family + ".")
        ]
        context = [
            symbol
            for symbol in ADJACENT_SYMBOLS
            if symbol == family or symbol.startswith(family + ".")
        ]
        definitions.append(
            {
                "code": code,
                "context_symbols": context,
                "id": PREFIX + slug,
                "subfamily": subfamily,
                "target_symbols": targets,
            }
        )
    counts = Counter(
        symbol for item in definitions for symbol in item["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Supply-core cases do not exactly partition 49 targets.")
    context_counts = Counter(
        symbol for item in definitions for symbol in item["context_symbols"]
    )
    if context_counts != Counter({symbol: 1 for symbol in ADJACENT_SYMBOLS}):
        raise RuntimeError("Supply-core cases do not exactly bind eight adjacent symbols.")
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Supply-core case identifiers drifted.")
    return tuple(definitions)


def _runtime_receipt() -> dict[str, Any]:
    implementation = sys.implementation
    hash_info = sys.hash_info
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
    if tuple(sys.version_info[:3]) != REQUIRED_PYTHON:
        raise SystemExit("Supply-core generation requires exact CPython 3.12.7.")
    if implementation.name != "cpython":
        raise SystemExit("Supply-core generation requires CPython.")
    if hash_info.algorithm != REQUIRED_HASH_ALGORITHM or hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("The pinned CPython hash implementation drifted.")
    if os.environ.get("PYTHONHASHSEED") != "0":
        raise SystemExit("Supply-core generation requires PYTHONHASHSEED=0.")
    return {
        "dependencies": dependencies,
        "hash_algorithm": hash_info.algorithm,
        "hash_width_bits": hash_info.width,
        "implementation": implementation.name,
        "python": ".".join(str(value) for value in REQUIRED_PYTHON),
        "pythonhashseed": "0",
        "support": {
            "bytes": EXPECTED_BASE_BYTES,
            "path": "tools/python-reference/generate_dragon_model_assembly_oracle.py",
            "sha256": EXPECTED_BASE_SHA256,
        },
        "utf8_mode": bool(sys.flags.utf8_mode),
    }


def _repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _verify_file_receipts(
    receipts: tuple[dict[str, Any], ...], *, fixture: bool
) -> list[dict[str, Any]]:
    root = _repository_root()
    observed: list[dict[str, Any]] = []
    for expected in receipts:
        path = root / expected["path"]
        if not path.is_file():
            raise SystemExit(f"Pinned support file is unavailable: {expected['path']}")
        actual = {
            "bytes": path.stat().st_size,
            "path": expected["path"],
            "sha256": sha256_file(path),
        }
        if fixture:
            value = load_json_without_duplicates(path)
            actual["cases_sha256"] = value.get("cases_sha256")
            actual["schema"] = value.get("schema")
            expected_core = {
                "bytes": expected["bytes"],
                "path": expected["path"],
                "sha256": expected["sha256"],
            }
            if {key: actual[key] for key in expected_core} != expected_core:
                raise SystemExit(f"Pinned support fixture drifted: {expected['path']}")
            if actual["schema"] != expected["schema"]:
                raise SystemExit(f"Pinned support fixture schema drifted: {expected['path']}")
        elif actual != expected:
            raise SystemExit(f"Pinned native source drifted: {expected['path']}")
        observed.append(actual)
    return observed


def _support_fixture_receipts() -> list[dict[str, Any]]:
    receipts = _verify_file_receipts(SUPPORT_FIXTURES, fixture=True)
    digest = canonical_sha256(receipts)
    if EXPECTED_SUPPORT_FIXTURES_SHA256 and digest != EXPECTED_SUPPORT_FIXTURES_SHA256:
        raise SystemExit("Pinned support-fixture aggregate drifted.")
    return receipts


def _native_review() -> dict[str, Any]:
    sources = _verify_file_receipts(NATIVE_SOURCE_RECEIPTS, fixture=False)
    review = {
        "classifications": CLASSIFICATIONS,
        "counts": {"equivalent": 18, "exception": 31, "total": 49},
        "forbidden_route_claims": [
            "SupplySystem.Generate",
            "AirHandlingUnit.Generate",
            "FanCoilUnit.Generate",
            "Radiator.Generate",
            "RadiantFloor.Generate",
        ],
        "native_routes": NATIVE_ROUTES,
        "native_sources": sources,
        "public_route_boundary": (
            "Only SupplySystem.Source/CanHeat/CanCool/ObjectNameFor, concrete "
            "public supply types, SupplyGroup, ZoneHvacAssignment, and "
            "EnergyModel.ToIdfDocument are claimed. Internal Generate members "
            "are intentionally not evidence routes."
        ),
    }
    digest = canonical_sha256(review)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise SystemExit("Native supply-core review drifted.")
    return review


def _resolve_runtime_symbol(hvac: Any, symbol: str) -> Any:
    owner_name, separator, member_name = symbol.partition(".")
    owner = getattr(hvac, owner_name)
    if not separator:
        return owner
    return inspect.getattr_static(owner, member_name)


def _runtime_signature(value: Any) -> dict[str, Any]:
    if isinstance(value, property):
        callable_value = value.fget
        kind = "property"
    elif inspect.isclass(value):
        callable_value = value
        kind = "class"
    else:
        callable_value = value
        kind = "function"
    if callable_value is None:
        raise RuntimeError("A target property has no getter.")
    return {
        "abstract": bool(getattr(callable_value, "__isabstractmethod__", False))
        or (inspect.isclass(value) and inspect.isabstract(value)),
        "kind": kind,
        "signature": str(inspect.signature(callable_value)),
    }


def _runtime_signatures(hvac: Any) -> dict[str, Any]:
    signatures = {
        symbol: _runtime_signature(_resolve_runtime_symbol(hvac, symbol))
        for symbol in TARGET_SYMBOLS
    }
    digest = canonical_sha256(signatures)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and digest != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Supply-core runtime signatures drifted.")
    return signatures


def _encode_value(value: Any) -> Any:
    if value is None or type(value) in (bool, int, str):
        return value
    if type(value) is float:
        if math.isnan(value):
            return {"kind": "nonfinite", "value": "nan"}
        if math.isinf(value):
            return {
                "kind": "nonfinite",
                "value": "positive-infinity" if value > 0 else "negative-infinity",
            }
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    if isinstance(value, (list, tuple)):
        return [_encode_value(item) for item in value]
    if isinstance(value, dict):
        return {str(key): _encode_value(item) for key, item in value.items()}
    if hasattr(value, "name"):
        return {"kind": type(value).__name__, "name": str(value.name)}
    return {"kind": type(value).__name__}


def _attempt(function: Callable[[], Any]) -> dict[str, Any]:
    try:
        result = function()
    except Exception as error:
        return {
            "args": [_encode_value(value) for value in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned", "value": _encode_value(result)}


class _Schedule:
    def __init__(self, imugi: Any, name: str) -> None:
        self._imugi = imugi
        self.name = name
        self.calls = 0

    def to_idf_object(self) -> Any:
        self.calls += 1
        return self._imugi.IdfObject(
            "Schedule:Constant",
            {"Name": self.name, "Hourly Value": 1},
            ignore_default=False,
        )


def _zone(modules: Any, name: str = "Supply Zone") -> tuple[Any, dict[str, _Schedule]]:
    schedules = {
        "availability": _Schedule(modules.imugi, "HVAC Availability"),
        "explicit": _Schedule(modules.imugi, "Explicit Availability"),
        "heating": _Schedule(modules.imugi, "Heating Setpoint"),
    }
    profile = SimpleNamespace(
        hvac_availability=schedules["availability"],
        heating_setpoint=schedules["heating"],
    )
    construction = SimpleNamespace(
        layers=("finish", "core", "insulation"), name="Floor Assembly"
    )
    surfaces = (
        SimpleNamespace(
            area=60.0, construction=construction, name="Supply Floor A"
        ),
        SimpleNamespace(
            area=40.0, construction=construction, name="Supply Floor B"
        ),
    )
    zone = SimpleNamespace(
        floor_area=100.0,
        floor_surface=surfaces,
        name=name,
        profile=profile,
    )
    return zone, schedules


def _source(name: str, loop: str) -> Any:
    return SimpleNamespace(idf_loopname=loop, name=name)


def _idf_name(value: Any) -> Any:
    try:
        return _encode_value(value["Name"])
    except (KeyError, TypeError):
        return None


def _conversion_summary(result: Any) -> dict[str, Any]:
    objects, processors = result
    object_receipts = []
    for value in objects:
        encoded = BASE._encoded_object(value)
        object_receipts.append(
            {
                "fields_sha256": canonical_sha256(encoded),
                "name": _idf_name(value),
                "object_type": value.idd.name,
                "stored_field_count": encoded["stored_field_count"],
            }
        )
    return {
        "object_count": len(objects),
        "object_receipts": object_receipts,
        "object_receipts_sha256": canonical_sha256(object_receipts),
        "object_type_order": [item["object_type"] for item in object_receipts],
        "processor_count": len(processors),
        "processor_type_order": [type(item).__name__ for item in processors],
    }


def _repeat_conversion(function: Callable[[], Any]) -> dict[str, Any]:
    first = function()
    second = function()
    return {
        "first": _conversion_summary(first),
        "fresh_object_list": first[0] is not second[0],
        "fresh_processor_list": first[1] is not second[1],
        "same_summary": _conversion_summary(first) == _conversion_summary(second),
        "second": _conversion_summary(second),
    }


def _state(value: Any, names: tuple[str, ...]) -> dict[str, Any]:
    return {name: _encode_value(getattr(value, name)) for name in names}


def _observe_air_handling_unit(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    source = _source("VRF Source", "Loop_for_VRF_Source")
    system = hvac.AirHandlingUnit(
        "Main AHU",
        source,
        fan_efficiency=0.71,
        fan_pressure=123.5,
        motor_efficiency=0.91,
    )
    quirky = hvac.AirHandlingUnit(
        "",
        None,
        fan_efficiency=-1,
        fan_pressure=float("nan"),
        motor_efficiency=float("inf"),
    )
    clone = copy.deepcopy(system)
    explicit = system.to_idf_object(
        zone,
        for_heating=False,
        for_cooling=True,
        availability=schedules["explicit"],
    )
    return {
        "capabilities": {"coolable": system.coolable, "heatable": system.heatable},
        "deepcopy": {
            "clone_is_fresh": clone is not system,
            "clone_name": clone.name,
            "source_identity_preserved": clone.source is system.source,
            "state": _state(
                clone,
                (
                    "fan_efficiency",
                    "fan_pressure",
                    "motor_efficiency",
                    "name",
                ),
            ),
        },
        "explicit_cooling_only_conversion": _conversion_summary(explicit),
        "idf_objtypename": system.idf_objtypename,
        "naming": {
            "air_inlet": system.idf_get_airinletnodename(zone),
            "air_outlet": system.idf_get_airoutletnodename(zone),
            "demand_branch": system.idf_get_demandbranchname(zone),
            "object": system.idf_get_objname(zone),
        },
        "quirky_constructor_state": _state(
            quirky,
            (
                "fan_efficiency",
                "fan_pressure",
                "motor_efficiency",
                "name",
                "source",
            ),
        ),
        "repeat_default_conversion": _repeat_conversion(
            lambda: system.to_idf_object(
                zone, for_heating=True, for_cooling=True
            )
        ),
        "state": _state(
            system,
            (
                "fan_efficiency",
                "fan_pressure",
                "motor_efficiency",
                "name",
                "source",
            ),
        ),
    }


def _observe_electric_radiant_floor(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    system = hvac.ElectricRadiantFloor("Electric Floor", throttling_range=2.5)
    quirky = hvac.ElectricRadiantFloor("", throttling_range=float("nan"))
    return {
        "capabilities": {
            "coolable": system.coolable,
            "heatable": system.heatable,
            "source": system.source,
        },
        "explicit_conversion": _conversion_summary(
            system.to_idf_object(
                zone,
                for_heating=True,
                for_cooling=False,
                availability=schedules["explicit"],
            )
        ),
        "idf_objtypename": system.idf_objtypename,
        "invalid_cooling_request": _attempt(
            lambda: system.to_idf_object(
                zone, for_heating=False, for_cooling=True
            )
        ),
        "quirky_constructor_state": _state(quirky, ("name", "throttling_range")),
        "repeat_default_conversion": _repeat_conversion(
            lambda: system.to_idf_object(
                zone, for_heating=True, for_cooling=False
            )
        ),
        "state": _state(system, ("name", "throttling_range")),
    }


def _observe_electric_radiator(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    system = hvac.ElectricRadiator(
        "Electric Radiator", 4500.0, efficiency=0.98, radiant_fraction=0.25
    )
    quirky = hvac.ElectricRadiator(
        "", None, efficiency=-1, radiant_fraction=2
    )
    return {
        "capabilities": {
            "coolable": system.coolable,
            "heatable": system.heatable,
            "source": system.source,
        },
        "explicit_conversion": _conversion_summary(
            system.to_idf_object(
                zone,
                for_heating=True,
                for_cooling=False,
                availability=schedules["explicit"],
            )
        ),
        "idf_objtypename": system.idf_objtypename,
        "invalid_cooling_request": _attempt(
            lambda: system.to_idf_object(
                zone, for_heating=False, for_cooling=True
            )
        ),
        "quirky_constructor_state": _state(
            quirky, ("capacity", "efficiency", "name", "radiant_fraction")
        ),
        "repeat_default_conversion": _repeat_conversion(
            lambda: system.to_idf_object(
                zone, for_heating=True, for_cooling=False
            )
        ),
        "state": _state(
            system, ("capacity", "efficiency", "name", "radiant_fraction")
        ),
    }


def _thermal_sources(hvac: Any) -> dict[str, Any]:
    tower = hvac.OpenSingleSpeedCoolingTower("Tower", 7000.0)
    boiler = hvac.Boiler("Boiler", hvac.Fuel.NATURALGAS, 0.92, 9000.0)
    chiller = hvac.Chiller(
        "Chiller", 3.2, 8000.0, hvac.CompressorType.TURBO, tower
    )
    absorption = hvac.AbsorptionChiller(
        "Absorption", 0.8, 6000.0, boiler, tower
    )
    heat_pump = hvac.HeatPump(
        "Heat Pump", hvac.Fuel.ELECTRICITY, 3.1, 2.8, 5000.0, 4500.0
    )
    return {
        "absorption": absorption,
        "boiler": boiler,
        "chiller": chiller,
        "heat_pump": heat_pump,
        "tower": tower,
    }


def _observe_fan_coil_unit(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    sources = _thermal_sources(hvac)
    systems = {
        "absorption": hvac.FanCoilUnit("FCU Absorption", sources["absorption"]),
        "boiler": hvac.FanCoilUnit("FCU Boiler", sources["boiler"]),
        "chiller": hvac.FanCoilUnit("FCU Chiller", sources["chiller"]),
        "heat_pump": hvac.FanCoilUnit("FCU Heat Pump", sources["heat_pump"]),
        "none": hvac.FanCoilUnit("FCU None", None),
    }
    quirky = hvac.FanCoilUnit(
        "",
        object(),
        fan_efficiency=-2,
        fan_pressure=float("-inf"),
        motor_efficiency=float("nan"),
    )
    return {
        "boiler_heating_conversion": _conversion_summary(
            systems["boiler"].to_idf_object(
                zone,
                for_heating=True,
                for_cooling=False,
                availability=schedules["explicit"],
            )
        ),
        "chiller_cooling_conversion": _conversion_summary(
            systems["chiller"].to_idf_object(
                zone,
                for_heating=False,
                for_cooling=True,
                availability=schedules["explicit"],
            )
        ),
        "idf_objtypename": systems["boiler"].idf_objtypename,
        "quirky_constructor_state": _state(
            quirky,
            (
                "fan_efficiency",
                "fan_pressure",
                "motor_efficiency",
                "name",
                "source",
            ),
        ),
        "source_combinations": {
            label: {
                "coolable": system.coolable,
                "heatable": system.heatable,
                "source_type": type(system.source).__name__,
            }
            for label, system in systems.items()
        },
        "state": _state(
            systems["boiler"],
            (
                "fan_efficiency",
                "fan_pressure",
                "motor_efficiency",
                "name",
                "source",
            ),
        ),
    }


def _observe_packaged_air_conditioner(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    system = hvac.PackagedAirConditioner(
        "Packaged AC",
        _source("PAC Source", "Loop_for_PAC_Source"),
        fan_efficiency=0.68,
        fan_pressure=140,
        motor_efficiency=0.88,
    )
    quirky = hvac.PackagedAirConditioner("", None, fan_pressure=-100)
    return {
        "capabilities": {"coolable": system.coolable, "heatable": system.heatable},
        "explicit_conversion": _conversion_summary(
            system.to_idf_object(
                zone,
                for_heating=False,
                for_cooling=True,
                availability=schedules["explicit"],
            )
        ),
        "inherited_idf_objtypename": system.idf_objtypename,
        "inherited_to_idf_owner": type(system).to_idf_object.__qualname__,
        "quirky_inherited_constructor_state": _state(
            quirky,
            (
                "fan_efficiency",
                "fan_pressure",
                "motor_efficiency",
                "name",
                "source",
            ),
        ),
        "repeat_default_conversion": _repeat_conversion(
            lambda: system.to_idf_object(
                zone, for_heating=False, for_cooling=True
            )
        ),
    }


def _observe_radiant_floor(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    source = _source("Floor Boiler", "Loop_for_Floor_Boiler")
    system = hvac.RadiantFloor("Hydronic Floor", source, throttling_range=1.75)
    quirky = hvac.RadiantFloor("", None, throttling_range=-3)
    return {
        "capabilities": {"coolable": system.coolable, "heatable": system.heatable},
        "explicit_conversion": _conversion_summary(
            system.to_idf_object(
                zone,
                for_heating=True,
                for_cooling=False,
                availability=schedules["explicit"],
            )
        ),
        "idf_objtypename": system.idf_objtypename,
        "invalid_cooling_request": _attempt(
            lambda: system.to_idf_object(
                zone, for_heating=False, for_cooling=True
            )
        ),
        "quirky_constructor_state": _state(
            quirky, ("name", "source", "throttling_range")
        ),
        "repeat_default_conversion": _repeat_conversion(
            lambda: system.to_idf_object(
                zone, for_heating=True, for_cooling=False
            )
        ),
        "state": _state(system, ("name", "source", "throttling_range")),
    }


def _observe_radiator(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    source = _source("Radiator Boiler", "Loop_for_Radiator_Boiler")
    system = hvac.Radiator(
        "Hydronic Radiator", 5500.0, source, radiant_fraction=0.35
    )
    quirky = hvac.Radiator("", -1, None, radiant_fraction=2)
    return {
        "capabilities": {"coolable": system.coolable, "heatable": system.heatable},
        "explicit_conversion": _conversion_summary(
            system.to_idf_object(
                zone,
                for_heating=True,
                for_cooling=False,
                availability=schedules["explicit"],
            )
        ),
        "idf_objtypename": system.idf_objtypename,
        "invalid_cooling_request": _attempt(
            lambda: system.to_idf_object(
                zone, for_heating=False, for_cooling=True
            )
        ),
        "quirky_constructor_state": _state(
            quirky, ("capacity", "name", "radiant_fraction", "source")
        ),
        "repeat_default_conversion": _repeat_conversion(
            lambda: system.to_idf_object(
                zone, for_heating=True, for_cooling=False
            )
        ),
        "state": _state(
            system, ("capacity", "name", "radiant_fraction", "source")
        ),
    }


def _probe_supply_type(hvac: Any) -> type[Any]:
    class ProbeSupply(hvac.SupplySystem):
        def __init__(
            self,
            name: str,
            source: Any,
            heatable: bool,
            coolable: bool,
        ) -> None:
            self.name = name
            self.source = source
            self._heatable = heatable
            self._coolable = coolable

        @property
        def coolable(self) -> bool:
            return self._coolable

        @property
        def heatable(self) -> bool:
            return self._heatable

        @property
        def idf_objtypename(self) -> str:
            return "Probe:Supply"

        def to_idf_object(
            self,
            zone: Any,
            for_heating: bool,
            for_cooling: bool,
            availability: Any = None,
        ) -> tuple[list[Any], list[Any]]:
            return [], []

    return ProbeSupply


def _observe_supply_group(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, schedules = _zone(modules)
    shared_source = _source("Group Source", "Loop_for_Group_Source")
    pac = hvac.PackagedAirConditioner("Group PAC", shared_source)
    electric = hvac.ElectricRadiator("Group Electric", 3000.0)
    group = hvac.SupplyGroup(
        [pac, electric], availabilities=[schedules["explicit"], None]
    )
    first_heating = group.heating_systems
    second_heating = group.heating_systems
    first_cooling = group.cooling_systems
    second_cooling = group.cooling_systems
    first_sources = group.sources
    second_sources = group.sources
    conversion = _repeat_conversion(lambda: group.to_idf_object(zone))
    Probe = _probe_supply_type(hvac)
    incapable = Probe("incapable", None, False, False)
    return {
        "availability_call_count_after_repeat": schedules["explicit"].calls,
        "availability_order": [
            None if item is None else item.name for item in group.availabilities
        ],
        "capabilities": {"coolable": group.coolable, "heatable": group.heatable},
        "conversion": conversion,
        "cooling_systems": {
            "fresh_tuple": first_cooling is not second_cooling,
            "order": [item.name for item in first_cooling],
            "same_member_identity": all(
                left is right for left, right in zip(first_cooling, second_cooling)
            ),
        },
        "heating_systems": {
            "fresh_tuple": first_heating is not second_heating,
            "order": [item.name for item in first_heating],
            "same_member_identity": all(
                left is right for left, right in zip(first_heating, second_heating)
            ),
        },
        "sources": {
            "fresh_tuple": first_sources is not second_sources,
            "order": [item.name for item in first_sources],
            "same_member_identity": all(
                left is right for left, right in zip(first_sources, second_sources)
            ),
        },
        "system_order": [item.name for item in group.systems],
        "validation": {
            "availability_count_mismatch": _attempt(
                lambda: hvac.SupplyGroup([electric], availabilities=[])
            ),
            "empty": _attempt(lambda: hvac.SupplyGroup([])),
            "incapable": _attempt(lambda: hvac.SupplyGroup([incapable])),
            "wrong_type": _attempt(lambda: hvac.SupplyGroup([object()])),
        },
    }


def _observe_supply_system(modules: Any) -> dict[str, Any]:
    hvac = modules.hvac
    zone, _ = _zone(modules, "Naming Zone")
    source = _source("Naming Source", "Loop_for_Naming_Source")
    Probe = _probe_supply_type(hvac)
    probe = Probe("Naming Probe", source, True, False)
    return {
        "abstract": inspect.isabstract(hvac.SupplySystem),
        "direct_instantiation": _attempt(lambda: hvac.SupplySystem()),
        "helpers": {
            "air_inlet": hvac.SupplySystem.idf_get_airinletnodename(probe, zone),
            "air_outlet": hvac.SupplySystem.idf_get_airoutletnodename(probe, zone),
            "demand_branch": hvac.SupplySystem.idf_get_demandbranchname(probe, zone),
            "object": hvac.SupplySystem.idf_get_objname(probe, zone),
        },
        "idf_objtypename_abstract": bool(
            hvac.SupplySystem.idf_objtypename.__isabstractmethod__
        ),
        "probe_capabilities": {
            "coolable": probe.coolable,
            "heatable": probe.heatable,
        },
        "probe_idf_objtypename": probe.idf_objtypename,
        "to_idf_object_abstract": bool(
            hvac.SupplySystem.to_idf_object.__isabstractmethod__
        ),
    }


OBSERVERS: dict[str, Callable[[Any], dict[str, Any]]] = {
    "air-handling-unit": _observe_air_handling_unit,
    "electric-radiant-floor": _observe_electric_radiant_floor,
    "electric-radiator": _observe_electric_radiator,
    "fan-coil-unit": _observe_fan_coil_unit,
    "packaged-air-conditioner": _observe_packaged_air_conditioner,
    "radiant-floor": _observe_radiant_floor,
    "radiator": _observe_radiator,
    "supply-group": _observe_supply_group,
    "supply-system": _observe_supply_system,
}


def _observe_cases(modules: Any) -> list[dict[str, Any]]:
    cases: list[dict[str, Any]] = []
    for definition in case_definitions():
        facts = OBSERVERS[definition["subfamily"]](modules)
        facts_sha256 = canonical_sha256(facts)
        expected_fact = EXPECTED_FACT_SHA256.get(definition["id"])
        if expected_fact and facts_sha256 != expected_fact:
            raise SystemExit(f"Supply-core facts drifted: {definition['id']}")
        case = {
            **definition,
            "python": {
                "facts": facts,
                "facts_sha256": facts_sha256,
                "outcome": "observed",
            },
        }
        case_sha256 = canonical_sha256(case)
        expected_case = EXPECTED_CASE_SHA256.get(definition["id"])
        if expected_case and case_sha256 != expected_case:
            raise SystemExit(f"Supply-core case drifted: {definition['id']}")
        case["case_sha256"] = case_sha256
        cases.append(case)
    return cases


def _symbol_contract(target_receipts: list[dict[str, Any]]) -> list[dict[str, Any]]:
    by_symbol = {item["symbol"]: item for item in target_receipts}
    return [
        {
            "adaptation": ADAPTATIONS.get(symbol),
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
    adjacent_receipts: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "classifications": CLASSIFICATIONS,
        "closure": {
            "adjacent_existing_status": ADJACENT_EXISTING_STATUS,
            "adjacent_indices": list(ADJACENT_INDICES),
            "adjacent_symbols": list(ADJACENT_SYMBOLS),
            "full_family_closure": True,
            "family_count": len(FAMILY_NAMES),
            "family_declaration_count": 57,
            "family_names": list(FAMILY_NAMES),
            "scope": "bounded-dragon-hvac-supply-core-evidence",
            "target_count": 49,
            "target_indices": list(TARGET_INDICES),
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "internal_generate_claimed": False,
        "native_routes": NATIVE_ROUTES,
        "runtime_signatures": runtime_signatures,
        "support_fixture_roles": {
            "dragon-hvac-supply-group-core-oracle.json": (
                "immutable adjacent SupplyGroup constructor/projection/source evidence"
            ),
            "dragon-hvac-supply-group-to-idf-object-oracle.json": (
                "immutable adjacent SupplyGroup conversion/order evidence"
            ),
            "dragon-model-add-supply-system-oracle.json": (
                "immutable EnergyModel supply-assignment aggregate evidence"
            ),
        },
        "target_symbols": list(TARGET_SYMBOLS),
        "unpromoted_adjacent_receipt_sha256": canonical_sha256(adjacent_receipts),
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
            raise SystemExit(f"Supply-core oracle contains a nondeterministic {label}.")


def build_oracle(
    inventory_path: Path,
    upstream_commit: str,
) -> dict[str, Any]:
    inventory = load_exact_inventory(inventory_path, upstream_commit)
    runtime = _runtime_receipt()
    support_fixtures = _support_fixture_receipts()
    native_review = _native_review()
    source_root = BASE._find_pinned_source_root()
    source_file = source_root / Path(SOURCE_PATH).relative_to("src")
    if (
        source_file.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(source_file) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported HVAC source bytes drifted.")

    with BASE._pinned_modules(source_root) as modules:
        primary_cases = _observe_cases(modules)
        signatures = _runtime_signatures(modules.hvac)
        loaded_modules = list(modules.loaded_local_modules)

    with tempfile.TemporaryDirectory(
        prefix="dragons-dragon-hvac-supply-relocated-"
    ) as temporary:
        relocated_root = Path(temporary) / "relocated-source"
        shutil.copytree(source_root, relocated_root)
        relocated_source = relocated_root / Path(SOURCE_PATH).relative_to("src")
        if (
            relocated_source.stat().st_size != EXPECTED_SOURCE_BYTES
            or sha256_file(relocated_source) != EXPECTED_SOURCE_SHA256
        ):
            raise SystemExit("The relocated HVAC source copy drifted.")
        with BASE._pinned_modules(relocated_root) as relocated_modules:
            relocated_cases = _observe_cases(relocated_modules)
            relocated_signatures = _runtime_signatures(relocated_modules.hvac)
            relocated_loaded = list(relocated_modules.loaded_local_modules)

    if primary_cases != relocated_cases:
        raise SystemExit("Supply-core observations are source-path dependent.")
    if signatures != relocated_signatures:
        raise SystemExit("Supply-core runtime signatures are source-path dependent.")
    if loaded_modules != relocated_loaded:
        raise SystemExit("Supply-core loaded module graph is source-path dependent.")
    if loaded_modules != BASE._expected_loaded_local_modules():
        raise SystemExit("Supply-core loaded local modules escaped the pinned graph.")

    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_cases)
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and modules_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Supply-core loaded-local-module aggregate drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Supply-core relocated observation aggregate drifted.")

    facts_hashes = {
        item["id"]: item["python"]["facts_sha256"] for item in primary_cases
    }
    case_hashes = {item["id"]: item["case_sha256"] for item in primary_cases}
    if EXPECTED_FACT_SHA256 and facts_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit("Supply-core fact hash partition drifted.")
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise SystemExit("Supply-core case hash partition drifted.")
    cases_sha256 = canonical_sha256(primary_cases)
    if EXPECTED_CASES_SHA256 and cases_sha256 != EXPECTED_CASES_SHA256:
        raise SystemExit("Supply-core case aggregate drifted.")

    value = {
        "case_sha256": case_hashes,
        "cases": primary_cases,
        "cases_sha256": cases_sha256,
        "consumer_contract": _consumer_contract(
            signatures, inventory["adjacent_receipts"]
        ),
        "fact_sha256": facts_hashes,
        "native_review": native_review,
        "runtime": runtime,
        "schema": SCHEMA,
        "support_fixtures": support_fixtures,
        "symbols": _symbol_contract(inventory["target_receipts"]),
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "adjacent_receipts": inventory["adjacent_receipts"],
            "adjacent_receipts_sha256": canonical_sha256(
                inventory["adjacent_receipts"]
            ),
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "family_closure_sha256": inventory["family_closure_sha256"],
            "inventory": {
                "bytes": EXPECTED_INVENTORY_BYTES,
                "content_sha256": EXPECTED_INVENTORY_SHA256,
                "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
                "path": "upstream/public-symbol-inventory.json",
            },
            "loaded_local_modules": loaded_modules,
            "loaded_local_modules_sha256": modules_hash,
            "relocation": {
                "byte_identical_source_copy": True,
                "observations_sha256": relocation_hash,
                "path_independent": True,
                "runtime_signatures_sha256": canonical_sha256(
                    relocated_signatures
                ),
            },
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "path": SOURCE_PATH,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
            "target_receipts_sha256": canonical_sha256(
                inventory["target_receipts"]
            ),
        },
    }
    validate_oracle(value)
    _scan_deterministic_payload(value)
    return value


def validate_oracle(value: dict[str, Any]) -> None:
    expected_keys = {
        "case_sha256",
        "cases",
        "cases_sha256",
        "consumer_contract",
        "fact_sha256",
        "native_review",
        "runtime",
        "schema",
        "support_fixtures",
        "symbols",
        "target_receipts",
        "upstream",
    }
    if set(value) != expected_keys:
        raise ValueError("Supply-core oracle root keys are not exact.")
    if value["schema"] != SCHEMA:
        raise ValueError("Supply-core oracle schema drifted.")
    if value["runtime"] != _runtime_receipt():
        raise ValueError("Supply-core runtime receipt drifted.")
    if value["target_receipts"] != [
        {
            "inventory_index": index,
            **_descriptor(receipt),
        }
        for (index, symbol), receipt in zip(
            TARGET_INDEX_SYMBOLS, value["target_receipts"]
        )
        if receipt.get("symbol") == symbol
    ]:
        raise ValueError("Supply-core target receipt index/symbol mapping drifted.")
    if len(value["target_receipts"]) != 49:
        raise ValueError("Supply-core target receipt count drifted.")
    if canonical_sha256(value["target_receipts"]) != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise ValueError("Supply-core target receipt aggregate drifted.")

    upstream = value["upstream"]
    if set(upstream) != {
        "adjacent_receipts",
        "adjacent_receipts_sha256",
        "commit",
        "family_closure_sha256",
        "inventory",
        "loaded_local_modules",
        "loaded_local_modules_sha256",
        "relocation",
        "source",
        "target_receipts_sha256",
    }:
        raise ValueError("Supply-core upstream keys are not exact.")
    adjacent = upstream.get("adjacent_receipts")
    if not isinstance(adjacent, list) or len(adjacent) != 8:
        raise ValueError("Supply-core adjacent receipt count drifted.")
    if tuple((item["inventory_index"], item["symbol"]) for item in adjacent) != ADJACENT_INDEX_SYMBOLS:
        raise ValueError("Supply-core adjacent index/symbol mapping drifted.")
    if canonical_sha256(adjacent) != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise ValueError("Supply-core adjacent receipt aggregate drifted.")
    if canonical_sha256(value["target_receipts"] + adjacent) != EXPECTED_FAMILY_CLOSURE_SHA256:
        raise ValueError("Supply-core family closure aggregate drifted.")
    expected_upstream_scalars = {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "family_closure_sha256": EXPECTED_FAMILY_CLOSURE_SHA256,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
            "path": "upstream/public-symbol-inventory.json",
        },
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }
    for key, expected in expected_upstream_scalars.items():
        if upstream.get(key) != expected:
            raise ValueError(f"Supply-core upstream {key} drifted.")
    if upstream.get("target_receipts_sha256") != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise ValueError("Supply-core upstream target receipt pin drifted.")
    if upstream.get("adjacent_receipts_sha256") != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise ValueError("Supply-core upstream adjacent receipt pin drifted.")

    loaded = upstream.get("loaded_local_modules")
    if loaded != BASE._expected_loaded_local_modules():
        raise ValueError("Supply-core loaded-local-module graph drifted.")
    loaded_hash = canonical_sha256(loaded)
    if upstream.get("loaded_local_modules_sha256") != loaded_hash:
        raise ValueError("Supply-core loaded-local-module self receipt drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and loaded_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise ValueError("Supply-core loaded-local-module pin drifted.")

    signatures = value["consumer_contract"].get("runtime_signatures")
    if set(signatures or {}) != set(TARGET_SYMBOLS):
        raise ValueError("Supply-core runtime-signature target closure drifted.")
    signatures_hash = canonical_sha256(signatures)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise ValueError("Supply-core runtime-signature aggregate drifted.")
    relocation = upstream.get("relocation")
    if relocation != {
        "byte_identical_source_copy": True,
        "observations_sha256": canonical_sha256(value["cases"]),
        "path_independent": True,
        "runtime_signatures_sha256": signatures_hash,
    }:
        raise ValueError("Supply-core relocation receipt drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation["observations_sha256"] != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise ValueError("Supply-core relocated observation pin drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if len(cases) != 9 or [item["id"] for item in cases] != list(EXPECTED_CASE_IDS):
        raise ValueError("Supply-core case closure drifted.")
    for case, definition in zip(cases, definitions):
        for key in ("code", "context_symbols", "id", "subfamily", "target_symbols"):
            if case.get(key) != definition[key]:
                raise ValueError(f"Supply-core case definition drifted: {definition['id']}")
        python = case.get("python")
        if not isinstance(python, dict) or python.get("outcome") != "observed":
            raise ValueError(f"Supply-core Python outcome drifted: {definition['id']}")
        facts_hash = canonical_sha256(python.get("facts"))
        if python.get("facts_sha256") != facts_hash:
            raise ValueError(f"Supply-core fact self receipt drifted: {definition['id']}")
        without_case_hash = {key: item for key, item in case.items() if key != "case_sha256"}
        case_hash = canonical_sha256(without_case_hash)
        if case.get("case_sha256") != case_hash:
            raise ValueError(f"Supply-core case self receipt drifted: {definition['id']}")
    fact_hashes = {item["id"]: item["python"]["facts_sha256"] for item in cases}
    case_hashes = {item["id"]: item["case_sha256"] for item in cases}
    if value["fact_sha256"] != fact_hashes or value["case_sha256"] != case_hashes:
        raise ValueError("Supply-core fact/case hash maps drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise ValueError("Supply-core fact pin map drifted.")
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise ValueError("Supply-core case pin map drifted.")
    cases_hash = canonical_sha256(cases)
    if value["cases_sha256"] != cases_hash:
        raise ValueError("Supply-core case aggregate self receipt drifted.")
    if EXPECTED_CASES_SHA256 and cases_hash != EXPECTED_CASES_SHA256:
        raise ValueError("Supply-core case aggregate pin drifted.")

    expected_contract = _consumer_contract(signatures, adjacent)
    if value["consumer_contract"] != expected_contract:
        raise ValueError("Supply-core consumer contract drifted.")
    if value["native_review"] != _native_review():
        raise ValueError("Supply-core native review drifted.")
    support = _support_fixture_receipts()
    if value["support_fixtures"] != support:
        raise ValueError("Supply-core support fixture receipts drifted.")
    if value["symbols"] != _symbol_contract(value["target_receipts"]):
        raise ValueError("Supply-core symbol contract drifted.")
    if len({item["assertion_id"] for item in value["symbols"]}) != 49:
        raise ValueError("Supply-core assertion IDs are not unique.")
    if any("Generate" in item["native_route"] for item in value["symbols"]):
        raise ValueError("An internal Generate member was claimed as a native route.")
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

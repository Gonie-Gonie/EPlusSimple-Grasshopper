"""Generate the pinned EPlusSimple HVAC other-systems behavior oracle.

This bounded corpus executes exactly 17 declarations for PhotoVoltaicSystem
and VentilationSystem in the pinned ``src/epsimple/core/hvac.py`` source.  The
other 185 declarations in that source remain adjacent non-target receipts.
The upstream module is imported without either EPlusSimple package
initializer and is executed again from a byte-identical relocated source tree.
"""

from __future__ import annotations

import argparse
from collections import Counter
from enum import Enum
import hashlib
import importlib.util
import inspect
import math
import os
from pathlib import Path
import re
import struct
import sys
import tempfile
from types import ModuleType, SimpleNamespace
from typing import Any, Callable


SCHEMA = "dragons.python-reference.epsimple-hvac-other-systems.v1"
SOURCE_PATH = "src/epsimple/core/hvac.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_067
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:6f898c6510a42b19841eb0bc60f3344fbed6c76b42d33351821686f3d7eb78e8"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_BYTES = 53_850
EXPECTED_SOURCE_SHA256 = (
    "sha256:9f3ecb27ed612baeed530ccbfd5857f1f528de24f222e6ef5093e4a635665d9c"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:dbbea63f51a001fae4fd73fba96dc099eab8cd5bcec39e3d9bf768e29b463873"
)

BASE_PATH = Path(__file__).resolve().with_name(
    "generate_epsimple_hvac_enums_base_oracle.py"
)
EXPECTED_BASE_BYTES = 61_377
EXPECTED_BASE_SHA256 = (
    "sha256:a397d3169f61a375b12a3934a2270874bfef1f3713a635cfd5e342668d12046b"
)


def _file_sha256(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def _load_base() -> Any:
    if (
        BASE_PATH.stat().st_size != EXPECTED_BASE_BYTES
        or _file_sha256(BASE_PATH) != EXPECTED_BASE_SHA256
    ):
        raise RuntimeError("Pinned HVAC enum/base support receipt drifted.")
    spec = importlib.util.spec_from_file_location(
        "_dragons_epsimple_hvac_other_systems_base", BASE_PATH
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load HVAC oracle support: {BASE_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


BASE = _load_base()
strict_json_dumps = BASE.strict_json_dumps
canonical_sha256 = BASE.canonical_sha256
sha256_file = BASE.sha256_file
load_json_without_duplicates = BASE.load_json_without_duplicates
load_json_without_duplicates_text = BASE.load_json_without_duplicates_text


_TARGET_ROWS = (
    (283, 'PhotoVoltaicSystem', 'class', 'sha256:5a79715b942d118595ac1f1169381d51cc3c070f7859fa6fbd16d20e1f0b8f92', 'sha256:8538fd06ff2115b195bd6fe1aa840bd72bf513fa74c70ab4924cb51140b40fbe', 'sha256:a8cb41ce6b4a3368c18fc1ffc46dc3caa684c9c210d2e0c0b194a276332c88b0'),
    (284, 'PhotoVoltaicSystem.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (287, 'PhotoVoltaicSystem.__init__', 'function', 'sha256:b018746211f6b5223eb6dca39443c3e0e451421ec70cf9daad82a5499a60f8ad', 'sha256:db3945c83aa4bb02dcd141f792b66a2faf8239c5c9eed8c487869207afd4d00f', 'sha256:5bf3ca3105b5c9b755bb7b4667d1f1a81e55c48e78ca991a2cdcb6c2cb35046b'),
    (290, 'PhotoVoltaicSystem.area', 'function', 'sha256:aa93b96bd36a02c789e649c6beb5f1309eefbfa45fbc91d8318df3474ec06d7a', 'sha256:525a1faf6e42740ef61f82b6a24773bfd87d60952f9393b477784bec26b9e4cf', 'sha256:e588872dc6972bed33a8a7331923fb9cea512910ef0d897ca5a7d2c83400dcb7'),
    (291, 'PhotoVoltaicSystem.azimuth', 'function', 'sha256:3b2cfc1a2acc215123de2b982147d0b2a74c978e531166fc7ac4aa7992d3c68f', 'sha256:9f38ee6a5bfbbdb1c739e1d150c51388c5aa36e0eeeabe4750dd405339034ff2', 'sha256:8fc199d010389df1784d072ad12b44ba74d404642c2b4271d4d78409f56c8a59'),
    (292, 'PhotoVoltaicSystem.efficiency', 'function', 'sha256:80144f2f58577c9b96d6d6d012e949a459d23f8cfb4a3f77e5656154e80b6947', 'sha256:7a05da5625820c6c82456f3e46373c9a45937c8d39440d6684a43098197dc63b', 'sha256:b449c78b5f6789e34ec6cf108ed58fc253c9778ca9b74142b39874ffcc6c2efc'),
    (293, 'PhotoVoltaicSystem.from_json', 'function', 'sha256:1571f37e4253739f52c4df3a7d23012ba84be42075a54cd701e56fc59aa12812', 'sha256:eec1058c435a3bfd26ed9bae90cd3afde9f5b6914c5e4635ef143a3196597389', 'sha256:427fb073ac996dc13a1265c560795ab221ab160198e2c2e47c26c0dca98fad94'),
    (294, 'PhotoVoltaicSystem.tilt', 'function', 'sha256:abeb16e68a1dc65d3d43f76029e2824f9f73bd5351e65bb1c5b2f17348eb161d', 'sha256:430cb41df90f491b97c6de4b2624bc51f99e0c3f315bd924f66470822a5b8c86', 'sha256:217707e81716052ded8bcec282489c965a856b2bef201b8988cc704fd5029a0f'),
    (295, 'PhotoVoltaicSystem.to_dragon', 'function', 'sha256:6f67da14e037bd1fb08ad53109aeb2083c1f4b1d1a58ffedce0046604d18360e', 'sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b', 'sha256:8126424ff42b67c4ca7f75cc8eda51bb840b5993d85b9ab4c34d2eff56bb744f'),
    (325, 'VentilationSystem', 'class', 'sha256:b4f227351d8ab5efd177e61ec95a1ec8f5ff115abd3192cfb1d2f5d2956376b4', 'sha256:f95992017959a0f7150618b3a1530e6058ad52be08e591e08619a5ca621ca56b', 'sha256:a8cb41ce6b4a3368c18fc1ffc46dc3caa684c9c210d2e0c0b194a276332c88b0'),
    (326, 'VentilationSystem.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (329, 'VentilationSystem.__init__', 'function', 'sha256:7d9d5173bcaf71fe63dbf9a8b4572b498db76f280f2ab6e0d4bb9d6d5af01a42', 'sha256:7449c5865d5357f17c8680f86663e4b772a2f806181a5840c4df79a063f1ea56', 'sha256:cd21ec881c0aca0378421daac09ce9c137ada1303435db24d7f439b0302caa05'),
    (332, 'VentilationSystem.airflow_rate', 'function', 'sha256:b19eca15d6da2b74f791418ace2d3f9842b3325ee1275aa83d2c9a376f29cecf', 'sha256:ebf17326b32c40dff148e673f2f9fd53f2cdb204714c16837f1bb5799c01025a', 'sha256:a1b5f099b791327d3fd1580b267e0487330ab441d32165d7b373896f017719de'),
    (333, 'VentilationSystem.cooling_efficiency', 'function', 'sha256:839431378f98bb11537ad790591935959db0dabf46c58bf2ef2ce14db593605b', 'sha256:e05e54956fba4af8af5f1534e150dc6463f3e8650df104724484e5fa31fc8dab', 'sha256:bc9be31b88092612769aa683b9382352cf43ae50709f87d9d1e669704f1062cf'),
    (334, 'VentilationSystem.from_json', 'function', 'sha256:acaa4faa2d86c79e192ff8b656f0f994bd68d5521d385c0ab73f87a3bf535e0f', 'sha256:e8490b3609e75eb4bc77408e8c95391c8d0566f25a62a06c94ccb419e46ac444', 'sha256:ef4d24e014eaec930ed906f6f3b49474d247a9cbdbf5821bbdc29f364750a2b7'),
    (335, 'VentilationSystem.heating_efficiency', 'function', 'sha256:76edd9cd644cad9e21d4c774207f921a253c93ffd5d10031c3650a66747a2924', 'sha256:ef7dc696a100503d05975968cf1f5b5464412f894f7d1830d70af18c56e89867', 'sha256:3b9814062256bf3ea2ef2c16974a02293b86402be6048965d3cb6fcc0a4235ec'),
    (336, 'VentilationSystem.to_dragon', 'function', 'sha256:fdc1293c0274742cb447238b18816105cb3feef83864b404fcf8b13a8270e47e', 'sha256:a1cb1a18cbf0115a8c02928192e6e9da52ae4ee12cf2818024c93475d5954103', 'sha256:8f6cfd498bbc3f5c6b9579a78c165bb00e671d7ddde71c2c3c9abb3f2caa468e'),
)


def _receipt(row: tuple[Any, ...]) -> dict[str, Any]:
    index, symbol, kind, symbol_hash, signature_hash, body_hash = row
    return {
        "body_hash": body_hash,
        "inventory_index": index,
        "kind": kind,
        "path": SOURCE_PATH,
        "signature_hash": signature_hash,
        "symbol": symbol,
        "symbol_hash": symbol_hash,
    }


TARGET_RECEIPTS = tuple(_receipt(row) for row in _TARGET_ROWS)
TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
TARGET_INDICES = tuple(item["inventory_index"] for item in TARGET_RECEIPTS)
TARGET_HASHES = {item["symbol"]: item["symbol_hash"] for item in TARGET_RECEIPTS}
SOURCE_INDICES = tuple(range(135, 337))
ADJACENT_INDICES = tuple(index for index in SOURCE_INDICES if index not in TARGET_INDICES)
if len(TARGET_INDICES) != 17 or len(ADJACENT_INDICES) != 185:
    raise RuntimeError("HVAC other-systems source partition count drifted.")

EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:c75dc2dc10c45ca2cc59300b130cc06399ec1ac07d6a138f69bebc43af70fe0f"
)
EXPECTED_ADJACENT_RECEIPTS_SHA256 = (
    "sha256:9496c1be4d58eee9816df92993a953e6c0c946a7254226cf7c52f2c80515b1a2"
)
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:08032fd90460d741d4b7f4b6bf5fab329f8ea195a6a03ef81b2aad976ebad6b2"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:ce5d3cd59eb175aa4fadbe2cb4cb4945a5c653f571f845c32d4ac0e0a6099f23"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:0e2b93750fe52bdc2719d1d1d2dbd9d042ab503572647be88116251a9537b58d"
)
EXPECTED_FACT_SHA256 = {
    "epsimple-hvac-other-systems.photovoltaic-state-validation-json-dragon": "sha256:333b0d584dd37182a2a8a1cfb273680a1bafa4d08f9f0623492f79adf15a2cad",
    "epsimple-hvac-other-systems.ventilation-defaults-state-validation-json-dragon": "sha256:ec05927e73b3fad6290ad3b35c00825f282692e97f8c0ab5b75878185dbec920",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-hvac-other-systems.photovoltaic-state-validation-json-dragon": "sha256:cf332ade7ae06a2da518e2904aaee751e5304bb3ec6971ffca6ee191025b1026",
    "epsimple-hvac-other-systems.ventilation-defaults-state-validation-json-dragon": "sha256:bb870af9eadf5e3e1c462b471b8f00e8cf02b3d8cffc8dec233f1a24f54a92eb",
}
EXPECTED_CASES_SHA256 = (
    "sha256:3d2d33dc4d341965a36f1af6e8b36ef072af9f9d91bb044596826099efdb2c6a"
)

_EXCEPTION_MEMBERS = {"__init__", "from_json", "to_dragon"}
EXCEPTION_SYMBOLS = {
    symbol
    for symbol in TARGET_SYMBOLS
    if "." not in symbol or symbol.rsplit(".", 1)[1] in _EXCEPTION_MEMBERS
}
CLASSIFICATIONS = {
    symbol: "exception" if symbol in EXCEPTION_SYMBOLS else "equivalent"
    for symbol in TARGET_SYMBOLS
}
ADAPTATIONS = {
    symbol: (
        "reviewed-native-immutable-other-system-and-aggregate-route-"
        + TARGET_HASHES[symbol][7:15]
    )
    for symbol in EXCEPTION_SYMBOLS
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"epsimple-hvac-other-systems-{item['inventory_index']}-"
        f"{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}

_PROPERTY_ROUTES = {
    "PhotoVoltaicSystem.ID": "PhotovoltaicSystem.Id",
    "PhotoVoltaicSystem.area": "PhotovoltaicSystem.Area",
    "PhotoVoltaicSystem.azimuth": "PhotovoltaicSystem.Azimuth",
    "PhotoVoltaicSystem.efficiency": "PhotovoltaicSystem.Efficiency",
    "PhotoVoltaicSystem.tilt": "PhotovoltaicSystem.Tilt",
    "VentilationSystem.ID": "VentilationSystem.Id",
    "VentilationSystem.airflow_rate": "VentilationSystem.AirflowRate",
    "VentilationSystem.cooling_efficiency": "VentilationSystem.CoolingEfficiency",
    "VentilationSystem.heating_efficiency": "VentilationSystem.HeatingEfficiency",
}


def _native_route(symbol: str) -> str:
    member = symbol.rsplit(".", 1)[1] if "." in symbol else None
    if symbol in _PROPERTY_ROUTES:
        return "Dragons.SimpleDragon." + _PROPERTY_ROUTES[symbol]
    if member == "from_json":
        collection = (
            "photovoltaic-system" if symbol.startswith("PhotoVoltaicSystem")
            else "ventilation-system"
        )
        return (
            "Dragons.SimpleDragon.GrmReader.Read(string, "
            f"SimpleDragonDatabase?) {collection} dispatch"
        )
    if member == "to_dragon":
        return (
            "Dragons.SimpleDragon.GreenRetrofitConverter.Convert("
            "GreenRetrofitModel, GreenRetrofitConversionOptions?)"
        )
    native_type = (
        "PhotovoltaicSystem" if symbol.startswith("PhotoVoltaicSystem")
        else "VentilationSystem"
    )
    return (
        f"Dragons.SimpleDragon.{native_type} constructor, public immutable "
        "properties, and GrmWriter.Write(GreenRetrofitModel, bool)"
    )


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}
NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 3_846,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/OtherSystems.cs",
        "sha256": "sha256:e1aba0e081e550031cb5dfd9f83f0bc8016c89c36cc2ab1b80c7a6af35aa7714",
    },
    {
        "bytes": 48_641,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs",
        "sha256": "sha256:a212275276ccff153d5df42a44a46ac8877afa485e315ee27d08767a909f29bb",
    },
    {
        "bytes": 16_646,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs",
        "sha256": "sha256:40e6e7aa1eb89cb341c7e7a32471fa029024e49b261dce8a8926514109d727ba",
    },
    {
        "bytes": 87_154,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs",
        "sha256": "sha256:9462f609a4a7a4e062612e4058921b0c91931dc8ff7216dbe54e258cb59ec22c",
    },
)

PREFIX = "epsimple-hvac-other-systems."
CASE_SPECS = (
    (
        "P01",
        "photovoltaic-state-validation-json-dragon",
        "photovoltaic",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("PhotoVoltaicSystem")),
    ),
    (
        "V01",
        "ventilation-defaults-state-validation-json-dragon",
        "ventilation",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("VentilationSystem")),
    ),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 2


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "code": code,
            "context_symbols": [],
            "id": PREFIX + slug,
            "subfamily": subfamily,
            "target_symbols": list(targets),
        }
        for code, slug, subfamily, targets in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("HVAC other-systems case order drifted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC other-systems targets are not an exact case partition.")
    return definitions


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    commit = upstream_commit.lower()
    if commit != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    if path.stat().st_size != EXPECTED_INVENTORY_BYTES:
        raise SystemExit("The public-symbol inventory byte length drifted.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash drifted.")
    value = load_json_without_duplicates(path)
    BASE.SUPPORT.require_exact_keys(
        value,
        {
            "content_sha256",
            "files",
            "schema",
            "scope_sha256",
            "summary",
            "symbols",
            "upstream_commit",
        },
        "Public-symbol inventory",
    )
    if value["schema"] != "dragons.upstream-public-symbol-inventory.v2":
        raise SystemExit("The public-symbol inventory schema drifted.")
    if value["upstream_commit"].lower() != commit:
        raise SystemExit("The public-symbol inventory commit drifted.")
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
        raise SystemExit("The EPlusSimple HVAC source receipt drifted.")
    for receipt in TARGET_RECEIPTS:
        if value["symbols"][receipt["inventory_index"]] != _descriptor(receipt):
            raise SystemExit(
                "HVAC other-systems inventory receipt drifted at index "
                + str(receipt["inventory_index"])
            )
    source_rows = [
        {**item, "inventory_index": index}
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    if [item["inventory_index"] for item in source_rows] != list(SOURCE_INDICES):
        raise SystemExit("The hvac.py source declaration range drifted.")
    adjacent = [
        item for item in source_rows if item["inventory_index"] in ADJACENT_INDICES
    ]
    target_hash = canonical_sha256(list(TARGET_RECEIPTS))
    adjacent_hash = canonical_sha256(adjacent)
    if EXPECTED_TARGET_RECEIPTS_SHA256 and target_hash != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise SystemExit("Pinned HVAC other-systems target receipts drifted.")
    if EXPECTED_ADJACENT_RECEIPTS_SHA256 and adjacent_hash != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise SystemExit("Pinned HVAC other-systems adjacent receipts drifted.")
    if sorted((*TARGET_INDICES, *ADJACENT_INDICES)) != list(SOURCE_INDICES):
        raise RuntimeError("The HVAC other-systems source partition is incomplete.")
    return {
        "adjacent_receipts_sha256": adjacent_hash,
        "content_sha256": aggregate,
        "files": value["files"],
        "raw": value,
        "source_file": source_file,
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": list(TARGET_RECEIPTS),
        "target_receipts_sha256": target_hash,
    }


def _find_pinned_source_root() -> Path:
    relative = Path(SOURCE_PATH).relative_to("src")
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        source = root / relative
        if (
            source.is_file()
            and source.stat().st_size == EXPECTED_SOURCE_BYTES
            and sha256_file(source) == EXPECTED_SOURCE_SHA256
        ):
            matches.append(root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned EPlusSimple source root must be importable.")
    return unique[0]


def _runtime_signatures(module: ModuleType) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for symbol in TARGET_SYMBOLS:
        value = BASE._runtime_member(module, symbol)
        if isinstance(value, property):
            result[symbol] = {
                "getter_signature": str(inspect.signature(value.fget)),
                "setter_signature": (
                    str(inspect.signature(value.fset))
                    if value.fset is not None
                    else None
                ),
                "type": "property",
            }
        elif callable(value):
            try:
                signature = str(inspect.signature(value))
            except (TypeError, ValueError):
                signature = "unavailable"
            result[symbol] = {
                "signature": signature,
                "type": type(value).__name__,
            }
        else:
            result[symbol] = {"type": type(value).__name__}
    return result


def _typed(value: Any) -> Any:
    if value is None or isinstance(value, str):
        return value
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
    if isinstance(value, Enum):
        return {
            "module": type(value).__module__,
            "name": value.name,
            "type": type(value).__name__,
            "value": _typed(value.value),
        }
    if isinstance(value, (list, tuple)):
        return [_typed(item) for item in value]
    if isinstance(value, dict):
        return {
            str(key): _typed(item)
            for key, item in sorted(value.items(), key=lambda pair: str(pair[0]))
        }
    if hasattr(value, "__dict__"):
        return {
            "attributes": {
                key: _typed(item)
                for key, item in sorted(vars(value).items())
            },
            "module": type(value).__module__,
            "type": type(value).__name__,
        }
    raise RuntimeError(f"Unsupported observed value type: {type(value).__name__}")


def _exception(operation: Callable[[], Any]) -> dict[str, Any]:
    try:
        operation()
    except BaseException as error:  # noqa: BLE001 - exact boundary is oracle data.
        return {
            "message_sha256": "sha256:"
            + hashlib.sha256(str(error).encode("utf-8")).hexdigest(),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned"}


def _setattr(instance: Any, name: str, value: Any) -> None:
    setattr(instance, name, value)


def _system_snapshot(instance: Any, fields: tuple[str, ...]) -> dict[str, Any]:
    return {
        "class": type(instance).__name__,
        "class_module": type(instance).__module__,
        "values": {field: _typed(getattr(instance, field)) for field in fields},
    }


def _auto_id_snapshot(first: Any, second: Any, expected_prefix: str) -> dict[str, Any]:
    pattern = re.compile(re.escape(expected_prefix) + r"AUTOID0x[0-9a-f]+\Z")
    return {
        "distinct_live_instances": first.ID != second.ID,
        "first_matches_process_identity_pattern": pattern.fullmatch(first.ID) is not None,
        "prefix": first.ID.split("AUTOID", 1)[0],
        "second_matches_process_identity_pattern": pattern.fullmatch(second.ID) is not None,
    }


def _photovoltaic_facts(module: ModuleType) -> dict[str, Any]:
    fields = ("ID", "name", "area", "efficiency", "azimuth", "tilt")
    explicit = module.PhotoVoltaicSystem(
        "Roof PV", 24.0, 0.2, 180.0, 30.0, ID="PV-EXPLICIT"
    )
    from_json = module.PhotoVoltaicSystem.from_json(
        SimpleNamespace(
            id="PV-JSON",
            name="Facade PV",
            area=12.5,
            efficiency=0.185,
            azimuth=225.0,
            tilt=90.0,
        )
    )
    mutable = module.PhotoVoltaicSystem(
        "Mutable PV", 10.0, 0.15, 90.0, 15.0, ID="PV-MUTABLE"
    )
    mutable.name = "Mutated PV"
    mutable.area = 11
    mutable.efficiency = 1.0
    mutable.azimuth = math.nextafter(360.0, -math.inf)
    mutable.tilt = 90
    auto_first = module.PhotoVoltaicSystem("Auto A", 1, 0.1, 0, 0)
    auto_second = module.PhotoVoltaicSystem("Auto B", 1, 0.1, 0, 0)
    accepted_specials = {
        "area_bool": _system_snapshot(
            module.PhotoVoltaicSystem("bool", True, 0.1, 0, 0, ID="PV-BOOL"),
            fields,
        ),
        "area_infinity": _system_snapshot(
            module.PhotoVoltaicSystem("inf", math.inf, 0.1, 0, 0, ID="PV-INF"),
            fields,
        ),
        "area_nan": _system_snapshot(
            module.PhotoVoltaicSystem("nan-area", math.nan, 0.1, 0, 0, ID="PV-NAN-A"),
            fields,
        ),
        "azimuth_nan": _system_snapshot(
            module.PhotoVoltaicSystem("nan-az", 1, 0.1, math.nan, 0, ID="PV-NAN-Z"),
            fields,
        ),
        "blank_name": _system_snapshot(
            module.PhotoVoltaicSystem("", 1, 0.1, 0, 0, ID="PV-BLANK"),
            fields,
        ),
        "efficiency_nan": _system_snapshot(
            module.PhotoVoltaicSystem("nan-eff", 1, math.nan, 0, 0, ID="PV-NAN-E"),
            fields,
        ),
        "tilt_nan": _system_snapshot(
            module.PhotoVoltaicSystem("nan-tilt", 1, 0.1, 0, math.nan, ID="PV-NAN-T"),
            fields,
        ),
    }
    return {
        "accepted_boundaries": {
            "area_nextafter_zero": _typed(
                module.PhotoVoltaicSystem(
                    "area-min", math.nextafter(0.0, math.inf), 0.1, 0, 0,
                    ID="PV-AREA-MIN",
                ).area
            ),
            "azimuth_nextafter_360": _typed(
                module.PhotoVoltaicSystem(
                    "az-max", 1, 0.1, math.nextafter(360.0, -math.inf), 0,
                    ID="PV-AZ-MAX",
                ).azimuth
            ),
            "efficiency_nextafter_zero": _typed(
                module.PhotoVoltaicSystem(
                    "eff-min", 1, math.nextafter(0.0, math.inf), 0, 0,
                    ID="PV-EFF-MIN",
                ).efficiency
            ),
            "efficiency_one": _typed(
                module.PhotoVoltaicSystem("eff-one", 1, 1, 0, 0, ID="PV-EFF-ONE").efficiency
            ),
            "tilt_ninety": _typed(
                module.PhotoVoltaicSystem("tilt-90", 1, 0.1, 0, 90, ID="PV-TILT-90").tilt
            ),
            "tilt_zero": _typed(
                module.PhotoVoltaicSystem("tilt-0", 1, 0.1, 0, 0, ID="PV-TILT-0").tilt
            ),
        },
        "accepted_specials": accepted_specials,
        "adjacent_behavior_executed": False,
        "auto_id": _auto_id_snapshot(auto_first, auto_second, "PVPN-"),
        "base_classes": [base.__name__ for base in module.PhotoVoltaicSystem.__bases__],
        "dragon": _typed(explicit.to_dragon()),
        "dragon_repeat_fresh": explicit.to_dragon() is not explicit.to_dragon(),
        "errors": {
            "area_negative": _exception(
                lambda: module.PhotoVoltaicSystem("bad", -1, 0.1, 0, 0)
            ),
            "area_string": _exception(
                lambda: module.PhotoVoltaicSystem("bad", "1", 0.1, 0, 0)
            ),
            "area_zero": _exception(
                lambda: module.PhotoVoltaicSystem("bad", 0, 0.1, 0, 0)
            ),
            "azimuth_360": _exception(
                lambda: module.PhotoVoltaicSystem("bad", 1, 0.1, 360, 0)
            ),
            "azimuth_negative": _exception(
                lambda: module.PhotoVoltaicSystem("bad", 1, 0.1, -1, 0)
            ),
            "efficiency_above_one": _exception(
                lambda: module.PhotoVoltaicSystem("bad", 1, 1.01, 0, 0)
            ),
            "efficiency_zero": _exception(
                lambda: module.PhotoVoltaicSystem("bad", 1, 0, 0, 0)
            ),
            "from_json_missing_tilt": _exception(
                lambda: module.PhotoVoltaicSystem.from_json(
                    SimpleNamespace(
                        id="bad", name="bad", area=1, efficiency=0.1, azimuth=0
                    )
                )
            ),
            "setter_area_zero": _exception(lambda: _setattr(explicit, "area", 0)),
            "setter_azimuth_360": _exception(lambda: _setattr(explicit, "azimuth", 360)),
            "setter_efficiency_zero": _exception(
                lambda: _setattr(explicit, "efficiency", 0)
            ),
            "setter_tilt_above_ninety": _exception(
                lambda: _setattr(explicit, "tilt", 90.01)
            ),
            "tilt_above_ninety": _exception(
                lambda: module.PhotoVoltaicSystem("bad", 1, 0.1, 0, 90.01)
            ),
            "tilt_negative": _exception(
                lambda: module.PhotoVoltaicSystem("bad", 1, 0.1, 0, -1)
            ),
        },
        "explicit": _system_snapshot(explicit, fields),
        "from_json": _system_snapshot(from_json, fields),
        "mutated": _system_snapshot(mutable, fields),
    }


def _ventilation_facts(module: ModuleType) -> dict[str, Any]:
    fields = (
        "ID",
        "name",
        "airflow_rate",
        "heating_efficiency",
        "cooling_efficiency",
    )
    default = module.VentilationSystem("Default ERV", 0.5, ID="ERV-DEFAULT")
    explicit = module.VentilationSystem(
        "Explicit ERV", 0.75, 0.82, 0.61, ID="ERV-EXPLICIT"
    )
    from_json_default = module.VentilationSystem.from_json(
        SimpleNamespace(id="ERV-JSON-DEFAULT", name="JSON Default", airflow_rate=0.4)
    )
    from_json_explicit = module.VentilationSystem.from_json(
        SimpleNamespace(
            id="ERV-JSON-EXPLICIT",
            name="JSON Explicit",
            airflow_rate=0.9,
            efficiency_heating=0.78,
            efficiency_cooling=0.56,
        )
    )
    mutable = module.VentilationSystem("Mutable ERV", 0.3, ID="ERV-MUTABLE")
    mutable.name = "Mutated ERV"
    mutable.airflow_rate = 1.25
    mutable.heating_efficiency = math.nextafter(1.0, -math.inf)
    mutable.cooling_efficiency = math.nextafter(0.0, math.inf)
    auto_first = module.VentilationSystem("Auto A", 0.1)
    auto_second = module.VentilationSystem("Auto B", 0.1)
    accepted_specials = {
        "airflow_bool": _system_snapshot(
            module.VentilationSystem("bool", True, ID="ERV-BOOL"), fields
        ),
        "airflow_infinity": _system_snapshot(
            module.VentilationSystem("inf", math.inf, ID="ERV-INF"), fields
        ),
        "airflow_nan": _system_snapshot(
            module.VentilationSystem("nan-flow", math.nan, ID="ERV-NAN-F"), fields
        ),
        "blank_name": _system_snapshot(
            module.VentilationSystem("", 0.1, ID="ERV-BLANK"), fields
        ),
        "cooling_nan": _system_snapshot(
            module.VentilationSystem("nan-c", 0.1, cooling_efficiency=math.nan, ID="ERV-NAN-C"),
            fields,
        ),
        "heating_nan": _system_snapshot(
            module.VentilationSystem("nan-h", 0.1, heating_efficiency=math.nan, ID="ERV-NAN-H"),
            fields,
        ),
    }
    return {
        "accepted_boundaries": {
            "airflow_nextafter_zero": _typed(
                module.VentilationSystem(
                    "flow-min", math.nextafter(0.0, math.inf), ID="ERV-FLOW-MIN"
                ).airflow_rate
            ),
            "cooling_nextafter_one": _typed(
                module.VentilationSystem(
                    "cool-max", 0.1,
                    cooling_efficiency=math.nextafter(1.0, -math.inf),
                    ID="ERV-COOL-MAX",
                ).cooling_efficiency
            ),
            "cooling_nextafter_zero": _typed(
                module.VentilationSystem(
                    "cool-min", 0.1,
                    cooling_efficiency=math.nextafter(0.0, math.inf),
                    ID="ERV-COOL-MIN",
                ).cooling_efficiency
            ),
            "heating_nextafter_one": _typed(
                module.VentilationSystem(
                    "heat-max", 0.1,
                    heating_efficiency=math.nextafter(1.0, -math.inf),
                    ID="ERV-HEAT-MAX",
                ).heating_efficiency
            ),
            "heating_nextafter_zero": _typed(
                module.VentilationSystem(
                    "heat-min", 0.1,
                    heating_efficiency=math.nextafter(0.0, math.inf),
                    ID="ERV-HEAT-MIN",
                ).heating_efficiency
            ),
        },
        "accepted_specials": accepted_specials,
        "adjacent_behavior_executed": False,
        "auto_id": _auto_id_snapshot(auto_first, auto_second, "ERVT-"),
        "base_classes": [base.__name__ for base in module.VentilationSystem.__bases__],
        "default": _system_snapshot(default, fields),
        "dragon": _typed(explicit.to_dragon()),
        "dragon_repeat_fresh": explicit.to_dragon() is not explicit.to_dragon(),
        "errors": {
            "airflow_negative": _exception(
                lambda: module.VentilationSystem("bad", -0.1)
            ),
            "airflow_string": _exception(
                lambda: module.VentilationSystem("bad", "0.1")
            ),
            "airflow_zero": _exception(lambda: module.VentilationSystem("bad", 0)),
            "cooling_efficiency_one": _exception(
                lambda: module.VentilationSystem("bad", 0.1, cooling_efficiency=1)
            ),
            "cooling_efficiency_zero": _exception(
                lambda: module.VentilationSystem("bad", 0.1, cooling_efficiency=0)
            ),
            "from_json_missing_airflow": _exception(
                lambda: module.VentilationSystem.from_json(
                    SimpleNamespace(id="bad", name="bad")
                )
            ),
            "heating_efficiency_one": _exception(
                lambda: module.VentilationSystem("bad", 0.1, heating_efficiency=1)
            ),
            "heating_efficiency_zero": _exception(
                lambda: module.VentilationSystem("bad", 0.1, heating_efficiency=0)
            ),
            "setter_airflow_zero": _exception(
                lambda: _setattr(explicit, "airflow_rate", 0)
            ),
            "setter_cooling_one": _exception(
                lambda: _setattr(explicit, "cooling_efficiency", 1)
            ),
            "setter_heating_zero": _exception(
                lambda: _setattr(explicit, "heating_efficiency", 0)
            ),
        },
        "explicit": _system_snapshot(explicit, fields),
        "from_json_default": _system_snapshot(from_json_default, fields),
        "from_json_explicit": _system_snapshot(from_json_explicit, fields),
        "mutated": _system_snapshot(mutable, fields),
    }


def _execute_cases(module: ModuleType) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _photovoltaic_facts(module),
        EXPECTED_CASE_IDS[1]: _ventilation_facts(module),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("HVAC other-systems observation order drifted.")
    return observations


def _runtime_receipt() -> dict[str, Any]:
    receipt = dict(BASE._runtime_receipt())
    receipt["other_systems_support"] = {
        "bytes": EXPECTED_BASE_BYTES,
        "path": "tools/python-reference/generate_epsimple_hvac_enums_base_oracle.py",
        "sha256": EXPECTED_BASE_SHA256,
    }
    return receipt


def _validate_generation_runtime() -> None:
    BASE._validate_generation_runtime()
    if (
        BASE_PATH.stat().st_size != EXPECTED_BASE_BYTES
        or sha256_file(BASE_PATH) != EXPECTED_BASE_SHA256
    ):
        raise SystemExit("Pinned HVAC other-systems support drifted.")


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
        "reviewed_semantics": {
            "native_auto_ids_are_deterministic": True,
            "native_models_are_immutable": True,
            "native_rejects_blank_names_and_nonfinite_numbers": True,
            "native_ventilation_conversion_preserves_airflow_and_assignment_count": True,
            "python_auto_ids_use_process_identity": True,
            "python_models_are_mutable": True,
            "python_nonfinite_range_behavior_is_observed_not_normalized": True,
            "python_ventilation_to_dragon_omits_airflow": True,
        },
        "public_production_routes_only": True,
        "python_executes_native_runtime": False,
        "routes_sha256": canonical_sha256(NATIVE_ROUTES),
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        "source_receipts_sha256": canonical_sha256(list(NATIVE_SOURCE_RECEIPTS)),
    }
    digest = canonical_sha256(result)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise SystemExit("Pinned HVAC other-systems native review drifted.")
    return result


def _coverage_by_symbol() -> dict[str, str]:
    result: dict[str, str] = {}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol] = definition["id"]
    if set(result) != set(TARGET_SYMBOLS):
        raise RuntimeError("HVAC other-systems symbol coverage drifted.")
    return result


def _expected_contract(signatures: dict[str, Any]) -> dict[str, Any]:
    counts = Counter(CLASSIFICATIONS.values())
    expectations = {
        symbol: {
            "adaptation": ADAPTATIONS.get(symbol, "not_applicable"),
            "assertion_id": ASSERTION_IDS[symbol],
            "classification": CLASSIFICATIONS[symbol],
            "native_route": NATIVE_ROUTES[symbol],
        }
        for symbol in TARGET_SYMBOLS
    }
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {
            "equivalent": counts["equivalent"],
            "exception": counts["exception"],
        },
        "classifications": CLASSIFICATIONS,
        "closure": {
            "adjacent_count": len(ADJACENT_INDICES),
            "adjacent_indices": list(ADJACENT_INDICES),
            "exact_one_case_target_partition": True,
            "full_hvac_source_partition": True,
            "source_declaration_count": len(SOURCE_INDICES),
            "target_count": len(TARGET_INDICES),
            "target_indices": list(TARGET_INDICES),
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "coverage_by_symbol": _coverage_by_symbol(),
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "adjacent_behavior_promoted": False,
            "exact_cpython_behavior_oracle": True,
            "expected_receipt_count": len(TARGET_RECEIPTS),
            "native_runtime_executed_by_python_oracle": False,
            "path_independent_relocated_import": True,
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
    _validate_generation_runtime()
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    work_root = (
        Path(__file__).resolve().parents[2]
        / "temp"
        / "reference"
        / "hvac-other-systems-work"
    )
    work_root.mkdir(parents=True, exist_ok=True)

    with BASE._isolated_import(imported_root, inventory["raw"]) as primary:
        module, loaded_modules = primary
        signatures = _runtime_signatures(module)
        observations = _execute_cases(module)

    with tempfile.TemporaryDirectory(
        prefix="epsimple-hvac-other-systems-relocation-", dir=work_root
    ) as temporary:
        relocated_root = Path(temporary) / "src"
        BASE._copy_source_tree(imported_root, relocated_root)
        with BASE._isolated_import(relocated_root, inventory["raw"]) as relocated:
            relocated_module, relocated_modules = relocated
            relocated_signatures = _runtime_signatures(relocated_module)
            relocated_observations = _execute_cases(relocated_module)

    if signatures != relocated_signatures:
        raise RuntimeError("HVAC other-systems signatures changed after relocation.")
    if observations != relocated_observations:
        raise RuntimeError("HVAC other-systems observations changed after relocation.")
    if loaded_modules != relocated_modules:
        raise RuntimeError("HVAC other-systems loaded modules changed after relocation.")

    signatures_hash = canonical_sha256(signatures)
    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned HVAC other-systems signatures drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and modules_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned HVAC other-systems loaded modules drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Pinned HVAC other-systems relocation observations drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned HVAC other-systems fact hashes drifted.\n"
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
            "Pinned HVAC other-systems case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned HVAC other-systems aggregate case hash drifted.")

    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(signatures),
        "fact_sha256": fact_hashes,
        "native_review": _native_review(),
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "adjacent_receipts_sha256": inventory["adjacent_receipts_sha256"],
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "inventory": {
                "bytes": EXPECTED_INVENTORY_BYTES,
                "content_sha256": EXPECTED_INVENTORY_SHA256,
                "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
            },
            "isolated_import": {
                "epsimple_core_initializer_executed": False,
                "epsimple_package_initializer_executed": False,
                "loaded_local_modules": loaded_modules,
                "loaded_local_modules_sha256": modules_hash,
                "relocated_observations_sha256": relocation_hash,
                "relocated_source_copy": "byte-identical-epsimple-and-idragon-trees",
                "source_location_count": 2,
            },
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "path": SOURCE_PATH,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
            "target_receipts_sha256": inventory["target_receipts_sha256"],
        },
    }
    validate_oracle(result)
    return result


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
        "symbols",
        "target_receipts",
        "upstream",
    }
    if not isinstance(value, dict) or set(value) != expected_keys:
        raise RuntimeError("HVAC other-systems oracle root key set drifted.")
    if value["schema"] != SCHEMA:
        raise RuntimeError("HVAC other-systems schema drifted.")
    if value["target_receipts"] != list(TARGET_RECEIPTS):
        raise RuntimeError("HVAC other-systems target receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("HVAC other-systems symbol descriptors drifted.")
    target_hash = canonical_sha256(value["target_receipts"])
    if EXPECTED_TARGET_RECEIPTS_SHA256 and target_hash != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise RuntimeError("Pinned HVAC other-systems target receipt hash drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("HVAC other-systems runtime signatures are absent.")
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise RuntimeError("Pinned HVAC other-systems runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(signatures):
        raise RuntimeError("HVAC other-systems consumer contract drifted.")
    if value["runtime"] != _runtime_receipt():
        raise RuntimeError("HVAC other-systems runtime receipt drifted.")
    if value["native_review"] != _native_review():
        raise RuntimeError("HVAC other-systems native review drifted.")

    upstream = value["upstream"]
    if not isinstance(upstream, dict) or set(upstream) != {
        "adjacent_receipts_sha256",
        "commit",
        "inventory",
        "isolated_import",
        "source",
        "target_receipts_sha256",
    }:
        raise RuntimeError("HVAC other-systems upstream key set drifted.")
    expected_static = {
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
    }
    for key, expected in expected_static.items():
        if upstream.get(key) != expected:
            raise RuntimeError(f"HVAC other-systems upstream field drifted: {key}")
    if upstream["target_receipts_sha256"] != canonical_sha256(value["target_receipts"]):
        raise RuntimeError("HVAC other-systems upstream target receipt hash drifted.")
    if (
        EXPECTED_ADJACENT_RECEIPTS_SHA256
        and upstream["adjacent_receipts_sha256"] != EXPECTED_ADJACENT_RECEIPTS_SHA256
    ):
        raise RuntimeError("Pinned HVAC other-systems adjacent receipt hash drifted.")
    isolated = upstream["isolated_import"]
    if not isinstance(isolated, dict) or set(isolated) != {
        "epsimple_core_initializer_executed",
        "epsimple_package_initializer_executed",
        "loaded_local_modules",
        "loaded_local_modules_sha256",
        "relocated_observations_sha256",
        "relocated_source_copy",
        "source_location_count",
    }:
        raise RuntimeError("HVAC other-systems isolated-import key set drifted.")
    if (
        isolated["source_location_count"] != 2
        or isolated["epsimple_package_initializer_executed"]
        or isolated["epsimple_core_initializer_executed"]
        or isolated["relocated_source_copy"]
        != "byte-identical-epsimple-and-idragon-trees"
    ):
        raise RuntimeError("HVAC other-systems relocation claim drifted.")
    loaded = isolated["loaded_local_modules"]
    if (
        not isinstance(loaded, list)
        or isolated["loaded_local_modules_sha256"] != canonical_sha256(loaded)
    ):
        raise RuntimeError("HVAC other-systems loaded-module receipt drifted.")
    if (
        EXPECTED_LOADED_LOCAL_MODULES_SHA256
        and canonical_sha256(loaded) != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise RuntimeError("Pinned HVAC other-systems loaded modules drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and isolated["relocated_observations_sha256"]
        != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise RuntimeError("Pinned HVAC other-systems relocation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("HVAC other-systems case count drifted.")
    if [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("HVAC other-systems case order drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        if set(case) != {*definition, "python"}:
            raise RuntimeError(
                f"HVAC other-systems case key set drifted: {definition['id']}"
            )
        for key, expected in definition.items():
            if case[key] != expected:
                raise RuntimeError(
                    f"HVAC other-systems case definition drifted: {definition['id']}"
                )
        python = case["python"]
        if (
            not isinstance(python, dict)
            or set(python) != {"facts", "facts_sha256", "outcome"}
            or python["outcome"] != "observed"
        ):
            raise RuntimeError(
                f"HVAC other-systems Python observation drifted: {definition['id']}"
            )
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(
                f"HVAC other-systems inline fact hash drifted: {definition['id']}"
            )
        if python["facts"].get("adjacent_behavior_executed") is not False:
            raise RuntimeError("Adjacent HVAC behavior was promoted into the target oracle.")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("HVAC other-systems fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned HVAC other-systems fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("HVAC other-systems case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned HVAC other-systems case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("HVAC other-systems aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned HVAC other-systems aggregate hash drifted.")
    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC other-systems exact target closure drifted.")
    closure = value["consumer_contract"]["closure"]
    if (
        closure["target_indices"] != list(TARGET_INDICES)
        or closure["adjacent_indices"] != list(ADJACENT_INDICES)
        or sorted((*closure["target_indices"], *closure["adjacent_indices"]))
        != list(SOURCE_INDICES)
    ):
        raise RuntimeError("HVAC other-systems full source closure drifted.")
    BASE._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("HVAC other-systems strict JSON round trip drifted.")


def main() -> None:
    args = parse_args()
    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    oracle = build_oracle(inventory, args.upstream_commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(oracle, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    counts = Counter(CLASSIFICATIONS.values())
    print(
        f"Wrote {len(oracle['cases'])} HVAC other-systems cases covering "
        f"{len(TARGET_RECEIPTS)} declarations: {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception, aggregate {oracle['cases_sha256']}."
    )


if __name__ == "__main__":
    main()

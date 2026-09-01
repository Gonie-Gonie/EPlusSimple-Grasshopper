"""Generate a pinned oracle for Dragon's remaining Zone core contracts.

The bounded corpus covers the mutable Zone container, floor projections,
legacy IDF naming properties, and the embedded supply coercion property.  The
already-resolved Zone IDF emitters and conditioning property are receipt-bound
but never promoted as targets by this artifact.
"""

from __future__ import annotations

import argparse
from collections import Counter
import functools
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import sys
from typing import Any, Callable


SCHEMA = "dragons.python-reference.dragon-shape-zone-core.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
SHAPE_SOURCE_PATH = "src/idragon/dragon/shape.py"
HVAC_SOURCE_PATH = "src/idragon/dragon/hvac.py"
SHAPE_SOURCE_SHA256 = (
    "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c"
)
SHAPE_AST_SHA256 = (
    "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"
)


def _receipt(
    index: int,
    symbol: str,
    kind: str,
    path: str,
    symbol_hash: str,
    signature_hash: str,
    body_hash: str,
) -> dict[str, Any]:
    return {
        "body_hash": "sha256:" + body_hash,
        "inventory_index": index,
        "kind": kind,
        "path": path,
        "signature_hash": "sha256:" + signature_hash,
        "symbol": symbol,
        "symbol_hash": "sha256:" + symbol_hash,
    }


TARGET_RECEIPTS = (
    _receipt(1083, "Zone", "class", SHAPE_SOURCE_PATH, "4830290e50ed3c4b50717f26a9b0503763c09b5b87f041b2f03d5ab3ba035d30", "16fdf50a01e06bd39fd30bae2eee24f8902679a1db662214f3ba00345680a29e", "82f464feaab2b692325befc8de0fdf44f28698a041430c75dce9acb727a1a318"),
    _receipt(1084, "Zone.__init__", "function", SHAPE_SOURCE_PATH, "fad03092d1390e4a9f0c7f4184a757c7abc55fb85b737f2d0c9be217b7682987", "60d0cabb1fd39adf4a0b915e1aa6dd59bd9861678ae2325ed69e38ee416a2b5e", "990d28f257710186eca517844cd89463291fbe5be37185a4e29df367f56be502"),
    _receipt(1085, "Zone.floor_area", "function", SHAPE_SOURCE_PATH, "21fe276dd163e81d4c0de2f978cb6dca63e807a7fe798d9fdaa5f8316ec8fac2", "f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "c6cbd898d7acd8c43cc2661cbaef1e9e8a8988ce15aeeaef4a41f6b82c2a4213"),
    _receipt(1086, "Zone.floor_surface", "function", SHAPE_SOURCE_PATH, "53382328123e6a81052a598a89a8e41482a1aac0a3d470e7bd66c63d6d8c22b7", "175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea", "6a515e3593890cc2dda844d3daceb975cec58874cdddaa46419fbdde77a86c48"),
    _receipt(1087, "Zone.idf_airexhaustnodelistname", "function", SHAPE_SOURCE_PATH, "48c6fddbf04adf507eabbaa023c0a3a711bb01812e6068becd27f2abdff9c1b7", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "655be145758f5c64d4cf876a2e60ef3729bfa412a6e50755f03dfec1eaa855d8"),
    _receipt(1088, "Zone.idf_airinletnodelistname", "function", SHAPE_SOURCE_PATH, "97745304336763af22c9a31a48c4a590d7faa2a936ec220fa0ee144fae1b701e", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "4c5c03d5d4247e862823a0d61639ea54d7a6457dc644e32a568b3a9b52c173d4"),
    _receipt(1089, "Zone.idf_equipmentlistname", "function", SHAPE_SOURCE_PATH, "ad9ccd78f5ddb00df6add098e600b6526268400988d2d2aaf2d0d3bd324b6a13", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "a20734c2039bd542918bcc16011a6b1c3eb3cb79412d6d1e2fedea909d428c82"),
    _receipt(1091, "Zone.supply", "function", SHAPE_SOURCE_PATH, "1b5900c0e47502e001f7a6055ea868392c85617ee800596666325fa118979b10", "30c559093214655a8e9c1f7a0f57523a48b1fcdba7601332201bca6669ee0a7e", "33112772ec1f8e870b64bcfaf5d178b471689b1dadb017030f23d683503eac1d"),
)

CONTEXT_RECEIPTS = (
    _receipt(707, "ElectricRadiator", "class", HVAC_SOURCE_PATH, "6e4ce6d4489fd995f5cf5ebfd4ca8a96db68c7b5d0bb271fbf37a9ea01dbdf33", "1c9170eb76b09c7feea649df317e2a08e6baf0c314de14d3f28859af519d9b05", "8b8d4de15bc3ac3f97742e4883e96cfbd188a7b4195ba0bf2dd76d29fec1ec92"),
    _receipt(708, "ElectricRadiator.__init__", "function", HVAC_SOURCE_PATH, "07f43ff08d4fb608d661c8399fbf11db4f1d8c7c504e81a6e8b8e9e223772ba5", "d23ce9ebf7e7a2bba349ed7000094734a2eecfb480fb83c721e56a5e6ef0936b", "f34ba716b0ec8995d0c671dd8ee8464d437871508ce667580c2f25f606b059f9"),
    _receipt(710, "ElectricRadiator.heatable", "function", HVAC_SOURCE_PATH, "0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db", "4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "a200989331792d789cc947c1b615c0eb8c31e552b2dbe4f805b7ad72e3f082d4"),
    _receipt(789, "SupplyGroup", "class", HVAC_SOURCE_PATH, "f22147d1bab44415fda473980799cb75dc4ce6c57693b5d9ec0a5faaf131fe69", "705b5c841450a5e51e48e95e3027de5a03632aa70888f7176d77d0cd48087459", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    _receipt(790, "SupplyGroup.__init__", "function", HVAC_SOURCE_PATH, "02b3c43aa048fd31a3ffc31fea96f5086a599d3245847e217dc0c99a9cf5fddd", "f01960cc5a0c00e094cf2eb094922d734343c92c8ec849977ea8b86337805907", "643ca4afc57e9a0b22eee5df0a2cd7b90d9d579cf16bb20fd6d6a9e40b5bc57c"),
    _receipt(797, "SupplySystem", "class", HVAC_SOURCE_PATH, "13ed08986e2e8b8e9b6a3f9b9a1f387ad8075a99a5f79e6df18b2fd0280cfdc1", "e69d386ef2ddabed5236bc05985ae71c826e6a0e7cb4b9b9a35ecc71a6bfb9ef", "ae6bdfe5569d83c09285f8097f3d7783d8e4911c3c43d0cac4dd9eb2ea1ff51e"),
    _receipt(1056, "SurfaceType.FLOOR", "constant", SHAPE_SOURCE_PATH, "c8c4f240e476a6db7cc85ca0bfcaea675233b72f28019edd4308f11cb689e01b", "909756f308b102264b0588f914f69542d69da96738233ca4fbb92a838d087bea", "37194ca6121ae832d5c991164c74dd662b39ba10da745ebc418aef2d1a834e5a"),
)

RESOLVED_RECEIPTS = (
    _receipt(1090, "Zone.is_conditioned", "function", SHAPE_SOURCE_PATH, "6fe80cb193a6716b68c1033c5c52bd29f422ffb9efbdac8475a7f4b4ddc46370", "2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb", "48a103a5bbb0b2a65f357d705eb38137269140e236bf98c2d56d7dd77474d9f3"),
    _receipt(1092, "Zone.to_idf_hvac_default_object", "function", SHAPE_SOURCE_PATH, "ff678ec281fe0726c46fd2145ebfb7fe22b56c5772bf1423d83c4877c0287cd9", "9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519", "9a121aaad9df4bfa6222f747985a1b07749f518b3501154743ef5c32d307940b"),
    _receipt(1093, "Zone.to_idf_load_object", "function", SHAPE_SOURCE_PATH, "d19165f0aa97a1768174def3da3a46c9c11f29567c558ae844d4cac546452f99", "9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519", "17d9c0579f4763783672c981efb7fa0d7c979af8ebfe008b70499f81273e5a78"),
    _receipt(1094, "Zone.to_idf_object", "function", SHAPE_SOURCE_PATH, "479f4d74a625e35e97559f208b41c4bde2f00a519b8e6b840718d78fdfd2e096", "9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519", "1964153231690634955bd8ae5c39468cd1ecab4f5c2acbff9ded2cb37978369a"),
)

TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
CONTEXT_SYMBOLS = tuple(item["symbol"] for item in CONTEXT_RECEIPTS)
RESOLVED_SYMBOLS = tuple(item["symbol"] for item in RESOLVED_RECEIPTS)
ALL_RECEIPTS = TARGET_RECEIPTS + CONTEXT_RECEIPTS + RESOLVED_RECEIPTS

CLASSIFICATIONS = {symbol: "exception" for symbol in TARGET_SYMBOLS}
ADAPTATIONS = {
    "Zone": "permissive-mutable-python-zone-container",
    "Zone.__init__": "unchecked-aliased-python-zone-construction",
    "Zone.floor_area": "python-floor-identity-filter-and-dynamic-sum",
    "Zone.floor_surface": "python-floor-identity-filter-and-fresh-list",
    "Zone.idf_airexhaustnodelistname": "mutable-unvalidated-python-zone-name-formatting",
    "Zone.idf_airinletnodelistname": "mutable-unvalidated-python-zone-name-formatting",
    "Zone.idf_equipmentlistname": "mutable-unvalidated-python-zone-name-formatting",
    "Zone.supply": "embedded-python-zone-supply-coercion-and-mutation",
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"dragon-shape-zone-core-{item['inventory_index']}-"
        f"{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}
NATIVE_TARGETS = {
    "Zone": "Dragons.InvisibleDragon.Shape.Zone typed aggregate",
    "Zone.__init__": "Shape.Zone constructor with validated identifiers, profile, and defensive surface-collection copy retaining immutable Surface references",
    "Zone.floor_area": "Zone.FloorArea over immutable native Surface.GrossArea values",
    "Zone.floor_surface": "Zone.FloorSurfaces filtered from the native read-only surface collection",
    "Zone.idf_airexhaustnodelistname": "EnergyModelIdfAssembler zone exhaust-node naming",
    "Zone.idf_airinletnodelistname": "EnergyModelIdfAssembler zone inlet-node naming",
    "Zone.idf_equipmentlistname": "EnergyModelIdfAssembler zone equipment-list naming",
    "Zone.supply": "ZoneHvacAssignment external HVAC association model",
}
RUNTIME_SIGNATURES = {
    "Zone": "(name, surface, profile, infiltration, light_density, supply_systems: 'None | SupplySystem | SupplyGroup', ventilation)",
    "Zone.__init__": "(self, name, surface, profile, infiltration, light_density, supply_systems: 'None | SupplySystem | SupplyGroup', ventilation)",
    "Zone.floor_area": "property:fget=(self) -> 'int | float'",
    "Zone.floor_surface": "property:fget=(self) -> 'list[Surface]'",
    "Zone.idf_airexhaustnodelistname": "property:fget=(self) -> 'str'",
    "Zone.idf_airinletnodelistname": "property:fget=(self) -> 'str'",
    "Zone.idf_equipmentlistname": "property:fget=(self) -> 'str'",
    "Zone.supply": "property:fget=(self) -> 'SupplyGroup | None';fset=(self, value: 'SupplyGroup | SupplySystem | None') -> 'None'",
}
CONTEXT_RUNTIME_SIGNATURES = {
    "ElectricRadiator": "(name: 'str', capacity: 'int | float', *, efficiency: 'int | float' = 1.0, radiant_fraction: 'int | float' = 0) -> 'None'",
    "ElectricRadiator.__init__": "(self, name: 'str', capacity: 'int | float', *, efficiency: 'int | float' = 1.0, radiant_fraction: 'int | float' = 0) -> 'None'",
    "ElectricRadiator.heatable": "property:fget=(self) -> 'bool'",
    "SupplyGroup": "(systems: 'list[SupplySystem]', *, availabilities: 'list[Schedule | None] | None' = None) -> 'None'",
    "SupplyGroup.__init__": "(self, systems: 'list[SupplySystem]', *, availabilities: 'list[Schedule | None] | None' = None) -> 'None'",
    "SupplySystem": "()",
    "SurfaceType.FLOOR": "enum-member:'floor'",
}

PREFIX = "dragon-shape-zone-core."
CASE_SPECS = (
    ("z01-representative-and-permissive-construction", ("Zone", "Zone.__init__"), ("Zone.supply",)),
    ("z02-empty-floor-projection", ("Zone.floor_surface", "Zone.floor_area"), ("Zone", "Zone.__init__", "Zone.supply")),
    ("z03-mixed-multiple-surface-floor-projection", ("Zone.floor_surface", "Zone.floor_area"), ("Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR")),
    ("z04-no-floor-multiple-surface-projection", ("Zone.floor_surface", "Zone.floor_area"), ("Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR")),
    ("z05-multiple-floor-dynamic-sum", ("Zone.floor_surface", "Zone.floor_area"), ("Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR")),
    ("z06-surface-alias-mutation-and-reassignment", ("Zone.__init__", "Zone.floor_surface", "Zone.floor_area"), ("Zone", "Zone.supply", "SurfaceType.FLOOR")),
    ("z07-name-formatting-and-name-mutation", ("Zone.idf_airexhaustnodelistname", "Zone.idf_airinletnodelistname", "Zone.idf_equipmentlistname"), ("Zone", "Zone.__init__", "Zone.supply")),
    ("z08-supply-none-system-group-coercion", ("Zone.supply",), ("Zone", "Zone.__init__", "ElectricRadiator", "ElectricRadiator.__init__", "ElectricRadiator.heatable", "SupplyGroup", "SupplyGroup.__init__", "SupplySystem")),
    ("z09-invalid-supply-error-and-partial-init", ("Zone.__init__", "Zone.supply"), ("Zone", "ElectricRadiator", "ElectricRadiator.__init__", "ElectricRadiator.heatable", "SupplyGroup", "SupplyGroup.__init__", "SupplySystem")),
    ("z10-floor-projection-error-timing", ("Zone.floor_surface", "Zone.floor_area"), ("Zone", "Zone.__init__", "Zone.supply", "SurfaceType.FLOOR")),
)
EXPECTED_CASE_IDS = tuple(PREFIX + item[0] for item in CASE_SPECS)
EXPECTED_CASE_COUNT = 10

EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:48ca4f2a95644574349289883d7b2053fd89630e987c0c7f413be8ce72a35714",
    EXPECTED_CASE_IDS[1]: "sha256:53fae4c45819d22aa9cbed67bf233a053d90b7cb9671456bd6d8f07eb4c02151",
    EXPECTED_CASE_IDS[2]: "sha256:7db7329767cd20752668cf27a0eb29b54b960387c8e08d628a2165c0f4d00fed",
    EXPECTED_CASE_IDS[3]: "sha256:7c9482907498643f83792188b370eaab0a1b9c09ffa65e9d204abd04a634a7be",
    EXPECTED_CASE_IDS[4]: "sha256:b8ceb11920c5e347c03bf1b3ae7abc3387503b2267802ad168b30877c31c393b",
    EXPECTED_CASE_IDS[5]: "sha256:b68647cc92f046009483d76756e1e8a14fb4db3caf8e28b6c0507694db07a039",
    EXPECTED_CASE_IDS[6]: "sha256:bd383c4ed786db75b4c064874af0f079865e5a53a3114880343fa51bf0f37268",
    EXPECTED_CASE_IDS[7]: "sha256:da56883067aefbccf8d509ed11472acb7ea85c1e04c4d38eaf2cd93aaf01c811",
    EXPECTED_CASE_IDS[8]: "sha256:53ed4cf50a90e1f9da5bb102def1dc020c7fa7fa50e574efd6fb12b2a151af9c",
    EXPECTED_CASE_IDS[9]: "sha256:ee6b6700ce0844bca9878ee1f95a36d68e656355e9882cee89cd7718e366816a",
}
EXPECTED_CASE_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:8525a95f683d23f3c8b9fc1098f54e21545978becf0b1495934994c064e156b6",
    EXPECTED_CASE_IDS[1]: "sha256:1e5bbaa6a5544d3ed2095186464a0a4558e3ac8823cb1aafb5f4f4797091408d",
    EXPECTED_CASE_IDS[2]: "sha256:8753662d5118d6d0624e17fd8595c2658ab7dc34b13e52ff2ec63bf911eeebb6",
    EXPECTED_CASE_IDS[3]: "sha256:c0d59d4de8601928540f97f67ab54e0379a734810adbfd7407028f11ff88a35f",
    EXPECTED_CASE_IDS[4]: "sha256:3e5e7c0fb8dec50819621336aac8eceb679899171a4a6cf7a51ce07c4fcfb937",
    EXPECTED_CASE_IDS[5]: "sha256:8ded2c24ba9f9347474fdf01e01ee4db03de239a1597a93426c4948d2f9028d5",
    EXPECTED_CASE_IDS[6]: "sha256:d946b3b64c6294fb7001400fcde629a62d78235cc616db527c2437ddf52c85d4",
    EXPECTED_CASE_IDS[7]: "sha256:9cf61887a4998f7ed1f278e59cd52b8fe6ad1786e3b94bd4b050f37ed6e792f0",
    EXPECTED_CASE_IDS[8]: "sha256:0cbaeae8b5db19a0505a74099dd634ba7227ecdbf167df35cf074eb3dfbae85b",
    EXPECTED_CASE_IDS[9]: "sha256:73b4f618cd44aa712ea3d1ea472ee2510d07a3614ae3f87bd98cc2598a024df6",
}

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location("_dragons_zone_core_support", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load Zone core support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("Zone core support is not exactly pinned.")
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


def _symbols_for_path(path: str) -> tuple[str, ...]:
    return tuple(
        item["symbol"]
        for item in sorted(
            (receipt for receipt in ALL_RECEIPTS if receipt["path"] == path),
            key=lambda receipt: receipt["inventory_index"],
        )
    )


SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": _symbols_for_path(path),
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


def _indexed(receipts: tuple[dict[str, Any], ...]) -> list[dict[str, Any]]:
    return [dict(item) for item in receipts]


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    expected = {item["symbol"]: _descriptor(item) for item in ALL_RECEIPTS}
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


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    raw = load_json_without_duplicates(path)
    inventories = [
        _load_source_inventory(path, commit, source) for source in SOURCE_SPECS
    ]
    if any(
        item["content_sha256"] != EXPECTED_INVENTORY_SHA256
        for item in inventories
    ):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    for receipt in ALL_RECEIPTS:
        observed = {
            **raw["symbols"][receipt["inventory_index"]],
            "inventory_index": receipt["inventory_index"],
        }
        if observed != receipt:
            raise SystemExit(
                f"Exact indexed Zone receipt drifted: {receipt['symbol']}."
            )
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "context_receipts": _indexed(CONTEXT_RECEIPTS),
        "files": [item["file"] for item in inventories],
        "resolved_receipts": _indexed(RESOLVED_RECEIPTS),
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": _indexed(TARGET_RECEIPTS),
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "context_symbols": list(context),
            "executor": "shape-zone-core",
            "expected_dotnet": {
                "adaptations": sorted({ADAPTATIONS[symbol] for symbol in targets}),
                "classifications": {symbol: CLASSIFICATIONS[symbol] for symbol in targets},
                "outcome": "adapted-as-pinned",
            },
            "id": PREFIX + slug,
            "subfamily": (
                "container"
                if slug.startswith("z01")
                else "floor"
                if slug.startswith(("z02", "z03", "z04", "z05", "z06", "z10"))
                else "naming"
                if slug.startswith("z07")
                else "supply"
            ),
            "target_symbols": list(targets),
        }
        for slug, targets, context in CASE_SPECS
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
    raise RuntimeError(f"Unsupported Zone scalar: {type(value).__name__}")


class _Token:
    def __init__(self, label: str) -> None:
        self.label = label


class _TraceSurface:
    def __init__(self, label: str, surface_type: Any, area: Any, trace: list[str]) -> None:
        self.label = label
        self.surface_type = surface_type
        self.area_value = area
        self.trace = trace

    @property
    def type(self) -> Any:
        self.trace.append(self.label + ".type")
        return self.surface_type

    @type.setter
    def type(self, value: Any) -> None:
        self.surface_type = value

    @property
    def area(self) -> Any:
        self.trace.append(self.label + ".area")
        return self.area_value

    @area.setter
    def area(self, value: Any) -> None:
        self.area_value = value


class _MissingTypeProbe:
    def __init__(self, label: str) -> None:
        self.label = label


def _event(call: Callable[[], Any], phase: str) -> tuple[dict[str, Any], Any]:
    try:
        value = call()
    except Exception as error:
        return (
            {
                "error": {"message": str(error), "type": type(error).__name__},
                "outcome": "raised",
                "phase": phase,
            },
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


def _zone(
    shape: Any,
    *,
    name: Any = "zone",
    surfaces: Any = None,
    supply: Any = None,
) -> Any:
    return shape.Zone(
        name,
        [] if surfaces is None else surfaces,
        _Token("profile"),
        0.25,
        8,
        supply,
        0.5,
    )


def _surface_state(surface: _TraceSurface) -> dict[str, Any]:
    value = surface.surface_type
    return {
        "area": _encode(surface.area_value),
        "label": surface.label,
        "type": (
            {"kind": "surface-type", "value": value.value}
            if hasattr(value, "value")
            else _encode(value)
        ),
    }


def _labels(values: Any) -> list[str]:
    return [getattr(item, "label", type(item).__name__) for item in values]


def _zone_state(zone: Any) -> dict[str, Any]:
    surface = zone.surface
    if isinstance(surface, (list, tuple)):
        surfaces: Any = [getattr(item, "label", type(item).__name__) for item in surface]
    else:
        surfaces = {"logical_type": type(surface).__name__}
    return {
        "infiltration": _encode(zone.infiltration),
        "light_density": _encode(zone.light_density),
        "name": _encode(zone.name),
        "profile": getattr(zone.profile, "label", type(zone.profile).__name__),
        "supply_type": "None" if zone.supply is None else type(zone.supply).__name__,
        "surface_container_type": type(surface).__name__,
        "surfaces": surfaces,
        "ventilation": _encode(zone.ventilation),
    }


def _fact(
    scenario: str,
    observations: dict[str, Any],
    snapshots: list[dict[str, Any]],
    timeline: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "observations": observations,
        "scenario": scenario,
        "source_state": {"snapshots": snapshots},
        "timeline": timeline,
    }


def _z01(shape: Any) -> dict[str, Any]:
    representative_event, representative = _event(
        lambda: _zone(shape, name="Representative", surfaces=()),
        "construct-representative-zone",
    )
    odd_surface = _Token("non-sequence-surface-token")
    odd_profile = _Token("odd-profile")
    odd_ventilation = _Token("odd-ventilation")
    permissive_event, permissive = _event(
        lambda: shape.Zone(
            True,
            odd_surface,
            odd_profile,
            float("nan"),
            10**400,
            None,
            odd_ventilation,
        ),
        "construct-permissive-zone",
    )
    return _fact(
        "Z01",
        {
            "permissive_attributes": {
                "infiltration": _encode(permissive.infiltration),
                "light_density": _encode(permissive.light_density),
                "name": _encode(permissive.name),
                "profile_label": permissive.profile.label,
                "supply": _encode(permissive.supply),
                "surface_label": permissive.surface.label,
                "ventilation_label": permissive.ventilation.label,
            },
            "representative_attributes": _zone_state(representative),
        },
        [
            {"phase": "before", "odd_surface_label": odd_surface.label},
            {"phase": "after", "odd_surface_label": odd_surface.label},
        ],
        [representative_event, permissive_event],
    )


def _z02(shape: Any) -> dict[str, Any]:
    authored: list[Any] = []
    zone = _zone(shape, surfaces=authored)
    before_authored = _labels(authored)
    before_zone_surface = _labels(zone.surface)
    first = zone.floor_surface
    area = zone.floor_area
    second = zone.floor_surface
    after_authored = _labels(authored)
    after_zone_surface = _labels(zone.surface)
    return _fact(
        "Z02",
        {
            "first_floor_labels": _labels(first),
            "first_floor_list_type": type(first).__name__,
            "first_list_is_second_list": first is second,
            "floor_area": _encode(area),
            "second_floor_labels": _labels(second),
            "zone_surface_is_authored_list": zone.surface is authored,
        },
        [
            {
                "authored_labels": before_authored,
                "phase": "before",
                "zone_surface_labels": before_zone_surface,
            },
            {
                "authored_labels": after_authored,
                "phase": "after",
                "zone_surface_labels": after_zone_surface,
            },
        ],
        [
            {"outcome": "returned", "phase": "first-floor-surface", "return_type": type(first).__name__},
            {"outcome": "returned", "phase": "floor-area", "return_type": type(area).__name__},
            {"outcome": "returned", "phase": "second-floor-surface", "return_type": type(second).__name__},
        ],
    )


def _projection_case(
    shape: Any,
    scenario: str,
    surfaces: list[_TraceSurface],
) -> dict[str, Any]:
    trace = surfaces[0].trace if surfaces else []
    zone = _zone(shape, surfaces=surfaces)
    before = [_surface_state(item) for item in surfaces]
    first = zone.floor_surface
    trace_after_first = list(trace)
    area_event, area = _event(lambda: zone.floor_area, "floor-area")
    trace_after_area = list(trace)
    second = zone.floor_surface
    return _fact(
        scenario,
        {
            "first_floor_labels": [item.label for item in first],
            "first_list_is_second_list": first is second,
            "floor_area_event": area_event,
            "floor_area_value": _encode(area) if area_event["outcome"] == "returned" else _encode(None),
            "second_floor_labels": [item.label for item in second],
            "trace_after_first": trace_after_first,
            "trace_after_floor_area": trace_after_area,
            "trace_after_second": list(trace),
        },
        [
            {"phase": "before", "surfaces": before},
            {"phase": "after", "surfaces": [_surface_state(item) for item in surfaces]},
        ],
        [
            {"outcome": "returned", "phase": "floor-surface-first", "return_type": type(first).__name__},
            area_event,
            {"outcome": "returned", "phase": "floor-surface-second", "return_type": type(second).__name__},
        ],
    )


def _z03(shape: Any) -> dict[str, Any]:
    trace: list[str] = []
    return _projection_case(
        shape,
        "Z03",
        [
            _TraceSurface("wall-1", "wall", 100, trace),
            _TraceSurface("floor-1", shape.SurfaceType.FLOOR, 12.5, trace),
            _TraceSurface("string-floor", "floor", 999, trace),
            _TraceSurface("ceiling-1", "ceiling", 100, trace),
        ],
    )


def _z04(shape: Any) -> dict[str, Any]:
    trace: list[str] = []
    return _projection_case(
        shape,
        "Z04",
        [
            _TraceSurface("wall-a", "wall", "unread-a", trace),
            _TraceSurface("ceiling-a", "ceiling", "unread-b", trace),
            _TraceSurface("string-floor", "floor", "unread-c", trace),
        ],
    )


def _z05(shape: Any) -> dict[str, Any]:
    trace: list[str] = []
    return _projection_case(
        shape,
        "Z05",
        [
            _TraceSurface("floor-bool", shape.SurfaceType.FLOOR, True, trace),
            _TraceSurface("wall-huge", "wall", 10**500, trace),
            _TraceSurface("floor-int", shape.SurfaceType.FLOOR, 3, trace),
            _TraceSurface("floor-float", shape.SurfaceType.FLOOR, 2.5, trace),
        ],
    )


def _z06(shape: Any) -> dict[str, Any]:
    trace: list[str] = []
    floor_one = _TraceSurface("floor-1", shape.SurfaceType.FLOOR, 10, trace)
    wall = _TraceSurface("wall", "wall", 20, trace)
    floor_two = _TraceSurface("floor-2", shape.SurfaceType.FLOOR, 3.5, trace)
    authored = [floor_one, wall]
    zone = _zone(shape, surfaces=authored)
    initial_surface_is_authored = zone.surface is authored
    snapshots: list[dict[str, Any]] = []

    def snap(phase: str) -> None:
        snapshots.append(
            {
                "authored_labels": [item.label for item in authored],
                "floor_area": _encode(zone.floor_area),
                "floor_labels": [item.label for item in zone.floor_surface],
                "phase": phase,
                "zone_surface_container": type(zone.surface).__name__,
                "zone_surface_labels": [item.label for item in zone.surface],
            }
        )

    snap("initial")
    authored.append(floor_two)
    snap("after-authored-append")
    authored.reverse()
    snap("after-authored-reverse")
    wall.type = shape.SurfaceType.FLOOR
    wall.area = -4
    snap("after-surface-property-mutation")
    replacement = (wall,)
    zone.surface = replacement
    snap("after-zone-surface-reassignment")
    authored.append(floor_one)
    snap("after-old-authored-list-mutation")
    return _fact(
        "Z06",
        {
            "replacement_container_type": type(replacement).__name__,
            "zone_surface_is_authored_initially": initial_surface_is_authored,
            "zone_surface_is_replacement_after_assignment": zone.surface is replacement,
            "trace": trace,
        },
        snapshots,
        [
            {"outcome": "returned", "phase": item["phase"], "return_type": "snapshot"}
            for item in snapshots
        ],
    )


def _naming_snapshot(zone: Any, phase: str) -> dict[str, Any]:
    return {
        "air_exhaust_node_list_name": zone.idf_airexhaustnodelistname,
        "air_inlet_node_list_name": zone.idf_airinletnodelistname,
        "equipment_list_name": zone.idf_equipmentlistname,
        "name": _encode(zone.name),
        "phase": phase,
    }


def _z07(shape: Any) -> dict[str, Any]:
    zone = _zone(shape, name="North Ω / Zone 01")
    snapshots = [_naming_snapshot(zone, "unicode-name")]
    zone.name = ""
    snapshots.append(_naming_snapshot(zone, "empty-name"))
    zone.name = None
    snapshots.append(_naming_snapshot(zone, "none-name"))
    return _fact(
        "Z07",
        {"name_output_snapshots": snapshots},
        snapshots,
        [
            {"outcome": "returned", "phase": item["phase"], "return_type": "dict"}
            for item in snapshots
        ],
    )


def _supply_snapshot(zone: Any, systems: dict[Any, str], phase: str) -> dict[str, Any]:
    supply = zone.supply
    return {
        "availabilities": (
            [] if supply is None else [_encode(value) for value in supply.availabilities]
        ),
        "phase": phase,
        "supply_type": "None" if supply is None else type(supply).__name__,
        "system_labels": [] if supply is None else [systems[item] for item in supply.systems],
    }


def _z08(shape: Any, hvac: Any) -> dict[str, Any]:
    first_event, first = _event(
        lambda: hvac.ElectricRadiator("first", 1000),
        "construct-first-system",
    )
    second_event, second = _event(
        lambda: hvac.ElectricRadiator("second", 2000),
        "construct-second-system",
    )
    labels = {first: "first", second: "second"}
    existing_event, existing = _event(
        lambda: hvac.SupplyGroup([first, second], availabilities=[None, None]),
        "construct-existing-group",
    )
    zone_event, zone = _event(
        lambda: _zone(shape, supply=None),
        "construct-zone-none",
    )
    snapshots = [_supply_snapshot(zone, labels, "constructed-none")]
    direct_setter_event, _ = _event(
        lambda: setattr(zone, "supply", first),
        "assign-direct-system",
    )
    wrapped = zone.supply
    snapshots.append(_supply_snapshot(zone, labels, "assigned-direct-system"))
    group_setter_event, _ = _event(
        lambda: setattr(zone, "supply", existing),
        "assign-existing-group",
    )
    snapshots.append(_supply_snapshot(zone, labels, "assigned-existing-group"))
    existing_identity = zone.supply is existing
    none_setter_event, _ = _event(
        lambda: setattr(zone, "supply", None),
        "assign-none",
    )
    snapshots.append(_supply_snapshot(zone, labels, "assigned-none"))
    direct_zone_event, direct_zone = _event(
        lambda: _zone(shape, name="direct", supply=second),
        "construct-zone-direct-system",
    )
    snapshots.append(_supply_snapshot(direct_zone, labels, "constructed-direct-system"))
    return _fact(
        "Z08",
        {
            "existing_group_is_retained": existing_identity,
            "wrapped_group_availability_count": len(wrapped.availabilities),
            "wrapped_group_system_is_direct_input": wrapped.systems[0] is first,
        },
        snapshots,
        [
            first_event,
            second_event,
            existing_event,
            zone_event,
            direct_setter_event,
            group_setter_event,
            none_setter_event,
            direct_zone_event,
        ],
    )


def _private_supply(zone: Any) -> Any:
    return zone.__dict__.get("_Zone__supply", "missing")


def _z09(shape: Any, hvac: Any) -> dict[str, Any]:
    radiator = hvac.ElectricRadiator("valid", 500)
    group = hvac.SupplyGroup([radiator])
    zone = _zone(shape, supply=group)
    invalid_events = []
    snapshots = [
        {
            "phase": "before-invalid-setters",
            "supply_is_original_group": zone.supply is group,
            "supply_type": type(zone.supply).__name__,
        }
    ]
    for label, value in (("integer", 0), ("boolean", True), ("token", _Token("bad"))):
        event, _ = _event(lambda value=value: setattr(zone, "supply", value), f"set-{label}")
        invalid_events.append(event)
        snapshots.append(
            {
                "phase": "after-" + label,
                "supply_is_original_group": zone.supply is group,
                "supply_type": type(zone.supply).__name__,
            }
        )

    allocated = shape.Zone.__new__(shape.Zone)
    init_event, _ = _event(
        lambda: shape.Zone.__init__(
            allocated,
            "partial",
            ["surface-token"],
            _Token("profile"),
            0.1,
            7,
            _Token("invalid-supply"),
            _Token("ventilation"),
        ),
        "construct-invalid-supply",
    )
    partial = {
        "attribute_names": sorted(allocated.__dict__),
        "light_density": _encode(allocated.light_density),
        "name": _encode(allocated.name),
        "private_supply_lookup": _encode(_private_supply(allocated)),
        "surface_values": [_encode(item) for item in allocated.surface],
        "ventilation_attribute_present": "ventilation" in allocated.__dict__,
    }
    return _fact(
        "Z09",
        {
            "constructor_error_event": init_event,
            "partial_constructor_state": partial,
            "setter_error_events": invalid_events,
        },
        snapshots + [{"phase": "after-failed-constructor", **partial}],
        invalid_events + [init_event],
    )


def _z10(shape: Any) -> dict[str, Any]:
    missing_probe = _MissingTypeProbe("missing-type")
    missing_zone = _zone(shape, name="missing-type-zone", surfaces=[missing_probe])
    missing_snapshots = [
        {
            "missing_probe_label": missing_probe.label,
            "phase": "missing-type-before",
            "zone_name": _encode(missing_zone.name),
            "zone_surface_labels": _labels(missing_zone.surface),
        }
    ]
    missing_surface_event, _ = _event(
        lambda: missing_zone.floor_surface, "missing-type-floor-surface"
    )
    missing_snapshots.append(
        {
            "missing_probe_label": missing_probe.label,
            "phase": "missing-type-after-floor-surface-error",
            "zone_name": _encode(missing_zone.name),
            "zone_surface_labels": _labels(missing_zone.surface),
        }
    )
    missing_area_event, _ = _event(
        lambda: missing_zone.floor_area, "missing-type-floor-area"
    )
    missing_snapshots.append(
        {
            "missing_probe_label": missing_probe.label,
            "phase": "missing-type-after-floor-area-error",
            "zone_name": _encode(missing_zone.name),
            "zone_surface_labels": _labels(missing_zone.surface),
        }
    )
    trace: list[str] = []
    string_area = _TraceSurface("string-area", shape.SurfaceType.FLOOR, "bad", trace)
    later = _TraceSurface("later-floor", shape.SurfaceType.FLOOR, 4, trace)
    string_zone = _zone(shape, surfaces=[string_area, later])
    string_area_event, _ = _event(
        lambda: string_zone.floor_area, "string-area-floor-area"
    )
    return _fact(
        "Z10",
        {
            "error_events": [missing_surface_event, missing_area_event, string_area_event],
            "string_area_trace": trace,
        },
        missing_snapshots + [
            {
                "phase": "string-area-after",
                "surfaces": [_surface_state(string_area), _surface_state(later)],
            },
        ],
        [missing_surface_event, missing_area_event, string_area_event],
    )


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    slug = identifier.removeprefix(PREFIX)
    functions: dict[str, Callable[[], dict[str, Any]]] = {
        "z01-representative-and-permissive-construction": lambda: _z01(modules.shape),
        "z02-empty-floor-projection": lambda: _z02(modules.shape),
        "z03-mixed-multiple-surface-floor-projection": lambda: _z03(modules.shape),
        "z04-no-floor-multiple-surface-projection": lambda: _z04(modules.shape),
        "z05-multiple-floor-dynamic-sum": lambda: _z05(modules.shape),
        "z06-surface-alias-mutation-and-reassignment": lambda: _z06(modules.shape),
        "z07-name-formatting-and-name-mutation": lambda: _z07(modules.shape),
        "z08-supply-none-system-group-coercion": lambda: _z08(modules.shape, modules.hvac),
        "z09-invalid-supply-error-and-partial-init": lambda: _z09(modules.shape, modules.hvac),
        "z10-floor-projection-error-timing": lambda: _z10(modules.shape),
    }
    try:
        return functions[slug]()
    except KeyError as error:
        raise RuntimeError(f"Unknown Zone core case: {identifier}") from error


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {item["id"]: canonical_sha256(item) for item in cases}


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


def _expected_inventory() -> dict[str, Any]:
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "context_receipts": _indexed(CONTEXT_RECEIPTS),
        "files": _expected_files(),
        "resolved_receipts": _indexed(RESOLVED_RECEIPTS),
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": _indexed(TARGET_RECEIPTS),
    }


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "loaded_local_modules": _expected_loaded_local_modules(),
        "shape_source": {
            "ast_sha256": SHAPE_AST_SHA256,
            "bytes": 27_438,
            "path": SHAPE_SOURCE_PATH,
            "source_sha256": SHAPE_SOURCE_SHA256,
        },
        "sources": [
            {
                "ast_sha256": source["ast_sha256"],
                "path": source["path"],
                "source_sha256": source["source_sha256"],
            }
            for source in SOURCE_SPECS
        ],
    }


def _coverage_by_symbol() -> dict[str, list[str]]:
    result = {symbol: [] for symbol in TARGET_SYMBOLS}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol].append(definition["id"])
    return result


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "All eight contracts require explicit exceptions. Python Zone accepts and "
            "retains unchecked mutable inputs, filters floors by enum-member identity at "
            "each read, dynamically sums the bounded observed area values, formats mutable names "
            "without validation, and owns mutable SupplySystem-to-SupplyGroup coercion. "
            "The native model validates inputs and defensively copies its surface collection "
            "while retaining immutable Surface references, locates IDF naming in the assembler, "
            "and associates HVAC externally."
        ),
        "classification_counts": {"equivalent": 0, "exception": 8},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "case_coverage_by_symbol": _coverage_by_symbol(),
            "context_receipts": _indexed(CONTEXT_RECEIPTS),
            "full_symbol_closure": False,
            "resolved_receipts_not_retargeted": _indexed(RESOLVED_RECEIPTS),
            "scope": "exact-ten-case-eight-target-zone-core-matrix",
            "target_coverage_complete": True,
            "target_symbols": list(TARGET_SYMBOLS),
            "observed_floor_sum_domain": {
                "edge_failure_inputs": ["str:'bad'-as-first-floor-area"],
                "edge_success_inputs": ["bool:True", "int:3", "float:2.5"],
                "representative_finite_input": "float:12.5",
            },
            "unresolved_boundaries": [
                "arbitrary-surface-iterators-that-raise-or-mutate-during-iteration",
                "foreign-area-addition-protocols-beyond-the-bounded-observed-inputs",
                "nonfinite-floor-area-values-not-observed",
                "huge-or-mixed-numeric-floor-area-overflow-and-coercion-not-observed",
                "missing-or-raising-area-attributes-not-observed",
                "zone-name-objects-with-custom-string-conversion-side-effects-or-errors",
                "virtual-or-dynamically-registered-SupplySystem-subclasses-and-descriptor-tampering",
                "concurrent-mutation-during-floor-projection-or-supply-assignment",
            ],
        },
        "context_runtime_signatures": CONTEXT_RUNTIME_SIGNATURES,
        "identity_encoding": "stable-direct-is-relations-only-no-id-or-address",
        "native_targets": NATIVE_TARGETS,
        "raw_fact_encoding": "typed-scalars-snapshots-access-traces-and-phase-bound-errors",
        "runtime_signatures": RUNTIME_SIGNATURES,
        "source_import_policy": "external-temporary-copy-with-complete-loaded-local-module-audit",
        "target_receipts": _indexed(TARGET_RECEIPTS),
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


def _resolve_symbol(modules: Any, symbol: str) -> Any:
    root = modules.hvac if symbol.split(".")[0] in {
        "ElectricRadiator",
        "SupplyGroup",
        "SupplySystem",
    } else modules.shape
    return functools.reduce(getattr, symbol.split("."), root)


def _runtime_signature(value: Any, modules: Any) -> str:
    if isinstance(value, property):
        result = "property:fget=" + str(inspect.signature(value.fget))
        if value.fset is not None:
            result += ";fset=" + str(inspect.signature(value.fset))
        return result
    if isinstance(value, modules.shape.SurfaceType):
        return "enum-member:" + repr(value.value)
    return str(inspect.signature(value))


def _runtime_signatures(modules: Any, symbols: tuple[str, ...]) -> dict[str, str]:
    return {
        symbol: _runtime_signature(_resolve_symbol(modules, symbol), modules)
        for symbol in symbols
    }


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    actual_hash = canonical_sha256(facts)
    expected_hash = EXPECTED_FACT_SHA256.get(identifier)
    if expected_hash is not None and actual_hash != expected_hash:
        raise RuntimeError(f"Zone core canonical semantics drifted: {identifier}")
    _require_keys(
        facts,
        {"observations", "scenario", "source_state", "timeline"},
        f"facts {identifier}",
    )
    if facts["scenario"] != identifier.removeprefix(PREFIX)[:3].upper():
        raise RuntimeError(f"Zone core scenario label drifted: {identifier}")
    _require_keys(facts["source_state"], {"snapshots"}, f"state {identifier}")
    if not isinstance(facts["source_state"]["snapshots"], list):
        raise RuntimeError(f"Zone core snapshots drifted: {identifier}")
    if not isinstance(facts["timeline"], list) or not facts["timeline"]:
        raise RuntimeError(f"Zone core timeline drifted: {identifier}")
    for event in facts["timeline"]:
        if event.get("outcome") not in {"raised", "returned"}:
            raise RuntimeError(f"Zone core event outcome drifted: {identifier}")
        if not isinstance(event.get("phase"), str):
            raise RuntimeError(f"Zone core event phase drifted: {identifier}")

    observations = facts["observations"]
    scenario = facts["scenario"]
    if scenario == "Z01":
        valid = (
            observations["permissive_attributes"]["name"] == _encode(True)
            and observations["permissive_attributes"]["infiltration"] == _encode(float("nan"))
            and len(observations["permissive_attributes"]["light_density"]["value"]) == 401
        )
    elif scenario == "Z02":
        snapshots = facts["source_state"]["snapshots"]
        valid = (
            observations["first_floor_labels"] == []
            and observations["second_floor_labels"] == []
            and observations["floor_area"] == _encode(0)
            and not observations["first_list_is_second_list"]
            and observations["zone_surface_is_authored_list"]
            and snapshots
            == [
                {"authored_labels": [], "phase": "before", "zone_surface_labels": []},
                {"authored_labels": [], "phase": "after", "zone_surface_labels": []},
            ]
        )
    elif scenario == "Z03":
        valid = (
            observations["first_floor_labels"] == ["floor-1"]
            and observations["floor_area_value"] == _encode(12.5)
            and "string-floor.area" not in observations["trace_after_floor_area"]
        )
    elif scenario == "Z04":
        valid = (
            observations["first_floor_labels"] == []
            and observations["floor_area_value"] == _encode(0)
            and not any(item.endswith(".area") for item in observations["trace_after_floor_area"])
        )
    elif scenario == "Z05":
        valid = (
            observations["first_floor_labels"] == ["floor-bool", "floor-int", "floor-float"]
            and observations["floor_area_value"] == _encode(6.5)
            and "wall-huge.area" not in observations["trace_after_floor_area"]
        )
    elif scenario == "Z06":
        valid = (
            [item["phase"] for item in facts["source_state"]["snapshots"]]
            == [
                "initial",
                "after-authored-append",
                "after-authored-reverse",
                "after-surface-property-mutation",
                "after-zone-surface-reassignment",
                "after-old-authored-list-mutation",
            ]
            and observations["zone_surface_is_authored_initially"]
            and observations["zone_surface_is_replacement_after_assignment"]
        )
    elif scenario == "Z07":
        outputs = observations["name_output_snapshots"]
        valid = (
            outputs[0]["equipment_list_name"] == "EquipmentList_for_North Ω / Zone 01"
            and outputs[1]["air_inlet_node_list_name"] == " Air InletNode List"
            and outputs[2]["air_exhaust_node_list_name"] == "None Air ExhaustNode List"
        )
    elif scenario == "Z08":
        snapshots = facts["source_state"]["snapshots"]
        timeline = facts["timeline"]
        valid = (
            [item["supply_type"] for item in snapshots]
            == ["None", "SupplyGroup", "SupplyGroup", "None", "SupplyGroup"]
            and observations["existing_group_is_retained"]
            and observations["wrapped_group_system_is_direct_input"]
            and [item["phase"] for item in timeline]
            == [
                "construct-first-system",
                "construct-second-system",
                "construct-existing-group",
                "construct-zone-none",
                "assign-direct-system",
                "assign-existing-group",
                "assign-none",
                "construct-zone-direct-system",
            ]
            and [item["return_type"] for item in timeline]
            == [
                "ElectricRadiator",
                "ElectricRadiator",
                "SupplyGroup",
                "Zone",
                "NoneType",
                "NoneType",
                "NoneType",
                "Zone",
            ]
            and [item["returned_none"] for item in timeline]
            == [False, False, False, False, True, True, True, False]
        )
    elif scenario == "Z09":
        events = observations["setter_error_events"] + [observations["constructor_error_event"]]
        partial = observations["partial_constructor_state"]
        valid = (
            all(item["error"] == {"message": "supply must be a SupplySystem, SupplyGroup, or None.", "type": "TypeError"} for item in events)
            and partial["attribute_names"] == ["infiltration", "light_density", "name", "profile", "surface"]
            and not partial["ventilation_attribute_present"]
        )
    elif scenario == "Z10":
        events = observations["error_events"]
        snapshots = facts["source_state"]["snapshots"]
        valid = (
            [item["error"]["type"] for item in events]
            == ["AttributeError", "AttributeError", "TypeError"]
            and observations["string_area_trace"]
            == ["string-area.type", "later-floor.type", "string-area.area"]
            and [item["phase"] for item in snapshots]
            == [
                "missing-type-before",
                "missing-type-after-floor-surface-error",
                "missing-type-after-floor-area-error",
                "string-area-after",
            ]
            and all(
                item["missing_probe_label"] == "missing-type"
                and item["zone_name"] == _encode("missing-type-zone")
                and item["zone_surface_labels"] == ["missing-type"]
                for item in snapshots[:3]
            )
        )
    else:
        valid = False
    if not valid:
        raise RuntimeError(f"Zone core semantic invariant drifted: {identifier}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    if inventory != _expected_inventory():
        raise SystemExit("The aggregate Zone core inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    shape_file = imported_root / Path(SHAPE_SOURCE_PATH).relative_to("src")
    if shape_file.stat().st_size != 27_438:
        raise SystemExit("Pinned shape.py byte length drifted.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        if _runtime_signatures(modules, TARGET_SYMBOLS) != RUNTIME_SIGNATURES:
            raise SystemExit("Pinned Zone target runtime signatures drifted.")
        if _runtime_signatures(modules, CONTEXT_SYMBOLS) != CONTEXT_RUNTIME_SIGNATURES:
            raise SystemExit("Pinned Zone context runtime signatures drifted.")
        observed = {
            definition["id"]: _execute_case(definition["id"], modules)
            for definition in case_definitions()
        }
        fact_hashes = {
            identifier: canonical_sha256(facts)
            for identifier, facts in observed.items()
        }
        if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned Zone per-case facts drifted.\nOBSERVED_FACT_HASHES\n"
                + strict_json_dumps(fact_hashes, indent=2)
            )
        cases = []
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
                "Pinned Zone per-case records drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(case_hashes, indent=2)
            )
        result = {
            "case_sha256": case_hashes,
            "cases": cases,
            "cases_sha256": cases_sha256(cases),
            "consumer_contract": _expected_consumer_contract(),
            "context_receipts": inventory["context_receipts"],
            "fact_sha256": fact_hashes,
            "resolved_receipts": inventory["resolved_receipts"],
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


def _validate_encoded_scalar(value: dict[str, Any], location: str) -> bool:
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
        try:
            if str(int(value["value"])) != value["value"]:
                raise ValueError
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid encoded int at {location}.") from error
        return True
    if kind == "str":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], str):
            raise RuntimeError(f"Invalid encoded string at {location}.")
        return True
    if kind == "float":
        _require_keys(value, {"hex", "kind", "repr"}, location)
        try:
            decoded = float.fromhex(value["hex"])
        except (TypeError, ValueError) as error:
            raise RuntimeError(f"Invalid encoded float at {location}.") from error
        if not math.isfinite(decoded) or decoded.hex() != value["hex"] or repr(decoded) != value["repr"]:
            raise RuntimeError(f"Unsafe encoded float at {location}.")
        return True
    if kind == "float-nonfinite":
        _require_keys(value, {"kind", "value"}, location)
        if value["value"] not in {"nan", "negative-infinity", "positive-infinity"}:
            raise RuntimeError(f"Invalid encoded nonfinite at {location}.")
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
        if "kind" in value and _validate_encoded_scalar(value, location):
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
            "context_receipts",
            "fact_sha256",
            "resolved_receipts",
            "runtime",
            "schema",
            "symbols",
            "target_receipts",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Zone core schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Zone core cases hash drifted.")
    if value["case_sha256"] != case_sha256(value["cases"]):
        raise RuntimeError("Zone core per-case hash map drifted.")
    _validate_safe_tree(value)

    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Zone core case order/count drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Zone core case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"Zone core Python outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"Zone core inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_facts(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Zone core fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("Zone core expected fact hashes drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("Zone core expected case hashes drifted.")

    target_counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if set(target_counts) != set(TARGET_SYMBOLS) or any(value < 1 for value in target_counts.values()):
        raise RuntimeError("Zone core target coverage drifted.")
    permitted_context = set(TARGET_SYMBOLS) | set(CONTEXT_SYMBOLS)
    observed_context = {
        symbol for definition in definitions for symbol in definition["context_symbols"]
    }
    if not observed_context.issubset(permitted_context):
        raise RuntimeError("Zone core context coverage drifted.")
    if set(RESOLVED_SYMBOLS).intersection(target_counts):
        raise RuntimeError("Resolved Zone symbols were retargeted.")
    if Counter(CLASSIFICATIONS.values()) != Counter({"exception": 8}):
        raise RuntimeError("Zone core classification counts drifted.")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Zone core consumer contract drifted.")
    if value["context_receipts"] != _indexed(CONTEXT_RECEIPTS):
        raise RuntimeError("Zone core context receipts drifted.")
    if value["resolved_receipts"] != _indexed(RESOLVED_RECEIPTS):
        raise RuntimeError("Zone core resolved receipts drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Zone core runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Zone core upstream receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("Zone core symbol descriptors drifted.")
    if value["target_receipts"] != _indexed(TARGET_RECEIPTS):
        raise RuntimeError("Zone core indexed target receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned checkout.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
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
    print(f"Wrote dragon shape Zone core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Generate the pinned EPlusSimple HVAC enum/base behavior oracle.

This deliberately bounded corpus executes the 28 unresolved declarations for
``CompressorType``, ``CoolingTowerControl``, ``CoolingTowerType``, ``Fuel``,
``NoneSource``, and ``SourceSystem`` in ``src/epsimple/core/hvac.py``.  Every
other unresolved declaration in that source is deferred and every existing
out-of-scope declaration is retained as an exclusion.

The upstream module is loaded without executing either EPlusSimple package
initializer and then loaded again from a byte-identical relocated source tree.
Only deterministic enum, singleton, base-class, and mapper behavior is
observed.  Native declarations are audited solely to bind the consumer
contract to real public SimpleDragon production routes.
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
import re
import shutil
import struct
import sys
import tempfile
from types import ModuleType
from typing import Any, Callable, Iterator


SCHEMA = "dragons.python-reference.epsimple-hvac-enums-base.v1"
SOURCE_PATH = "src/epsimple/core/hvac.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_BYTES = 53_850
EXPECTED_SOURCE_SHA256 = (
    "sha256:9f3ecb27ed612baeed530ccbfd5857f1f528de24f222e6ef5093e4a635665d9c"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:dbbea63f51a001fae4fd73fba96dc099eab8cd5bcec39e3d9bf768e29b463873"
)

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_PLATFORM = "win32"
REQUIRED_POINTER_WIDTH_BITS = 64
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
EXPECTED_DEPENDENCIES = {
    "eppy": "0.5.63",
    "numpy": "2.3.1",
    "pandas": "2.3.0",
    "python-dateutil": "2.9.0.post0",
    "pytz": "2024.2",
    "six": "1.16.0",
    "tzdata": "2024.2",
}

SUPPORT_PATH = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
EXPECTED_SUPPORT_BYTES = 21_114
EXPECTED_SUPPORT_SHA256 = (
    "sha256:4d2dd8d0c487af7a24f93f1e79b9b27ed19676cf7909a8039d90248fd7d6e1bc"
)
BOOTSTRAP_PATH = Path(__file__).resolve().with_name("bootstrap_reference.py")
EXPECTED_BOOTSTRAP_BYTES = 1_232
EXPECTED_BOOTSTRAP_SHA256 = (
    "sha256:0674dcf1fe966de2a4b873a360ef67be48d74f38ba80adba9c74405fd9be7e0f"
)


def _load_support() -> Any:
    if SUPPORT_PATH.stat().st_size != EXPECTED_SUPPORT_BYTES:
        raise RuntimeError("Strict JSON support byte length drifted.")
    spec = importlib.util.spec_from_file_location(
        "_dragons_epsimple_hvac_enums_base_support", SUPPORT_PATH
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load strict JSON support: {SUPPORT_PATH}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if module.sha256_file(SUPPORT_PATH) != EXPECTED_SUPPORT_SHA256:
        raise RuntimeError("Strict JSON support hash drifted.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
load_json_without_duplicates = SUPPORT.load_json_without_duplicates


_TARGET_ROWS = (
    (185, 'CompressorType', 'class', 'sha256:8785ee6da143dbc022e1a9cdb6096fa870f2d9d99804c2ab5ba18641319dfd74', 'sha256:ab5079fb2d6d55f1e976ecf2390324fa23f6f36699a42aaa4a781fc39b9db2c1', 'sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726'),
    (186, 'CompressorType.RECIPROCATING', 'constant', 'sha256:dfd51671c84116479c9ee96bf61343e6c32edc7a675ef8eb6127cb9b579c42a4', 'sha256:7ee3e8a807c0d636c16019f63a60e8b459bcea3273c9bdfe1f6e1101b3f5e0e9', 'sha256:14ef710b785ec24de12978b3183cb333a82b5bc0f568de54939803660521f94b'),
    (187, 'CompressorType.SCREW', 'constant', 'sha256:2947a21386fbbd0393dfc0670795aba5ddb05be02e511da37cd0118a5d70573c', 'sha256:7bdf36eaffe58a5b4664e1f40e728887db746c5d3fcb9afbc55abc405e6d32cb', 'sha256:798047174afcabac9976d05149bfeba3f8cf8c8b06a2f43f88611e5bd07cf24f'),
    (188, 'CompressorType.TURBO', 'constant', 'sha256:5074351dd266b5054fd70ac52d608a348ca3d3bd121be79c7aeb6655f9ad1449', 'sha256:eb1dcdfed7b6604baf0028ad7458ce73be465bfe31a918b8e1718b123ffe4742', 'sha256:27568afe842cedb04fc44e3ae07f54630fce2c3f31a22f7b184b4b98cb0f1d6a'),
    (189, 'CompressorType.__str__', 'function', 'sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e', 'sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab', 'sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8'),
    (190, 'CompressorType.to_dragon', 'function', 'sha256:bff3a12f1d60fb8759d0c55825daa39375c624fb86ce31c03545d045fa2933fd', 'sha256:ae9b2d48f5c0bd9d4abc023ce56cff228bdef92e0a406c572d6d12d1bdc6a1d5', 'sha256:cbab2d540d2face06ab18784eff867aa45244496194f49bedb2d528272d2a658'),
    (191, 'CoolingTowerControl', 'class', 'sha256:31f279b79019dfa39dedafb34b19d67eb9d77fdec455c45d5ac2b04e7cf0ed32', 'sha256:1c9370d192e36e8c8137fa438146a2f6fc539941001bc1c22a92dd72c6aa22a7', 'sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726'),
    (192, 'CoolingTowerControl.SINGLESPEED', 'constant', 'sha256:536f3586055ed157420436c6769db9d49ab058d1a510fc446e2a36334663635c', 'sha256:33b9fb7e3ac286f14605378084c561760ed62da9f2f2568131cf7bd25d7e30cb', 'sha256:c481a5bc5736eadcc831c922d092401622c400201c9555abef5d93373129c952'),
    (193, 'CoolingTowerControl.TWOSPEED', 'constant', 'sha256:bc3d3c6f75384b8448aee861fd14f0fb7c3899495493b91e83f29b46966bf4fd', 'sha256:b2fed9be992df2a7e4798c5ec9d8d48e148f2f3f0676259da18378889aab9645', 'sha256:74cd6022869a3c1b12704ddcc37c18ebc2e1226ee0cefc62e75880d9b3e5d36f'),
    (194, 'CoolingTowerControl.__str__', 'function', 'sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e', 'sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab', 'sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8'),
    (195, 'CoolingTowerType', 'class', 'sha256:9dd879be9b468d09c8ccb2a0c555fac28472d037d6168b5c71147d9e70fda4cc', 'sha256:8d6838d2866b425253e9ba32008780e3c316053ac93e18de26d3163687e4d9ee', 'sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726'),
    (196, 'CoolingTowerType.CLOSED', 'constant', 'sha256:ec6ad133c786c44f7d733ecfcc2c1cfb0148623df362a105ffd7c6504e07ba17', 'sha256:79bb9663ebb7296128246112cce823c8fad654b4d948c2c504952de5c0d8ddcd', 'sha256:4eca9c766d077cf0b4726a846ca96e7fce136ae1c84364db999306f57210961b'),
    (197, 'CoolingTowerType.OPEN', 'constant', 'sha256:0496e7cdd8ccf3a93d3007e6cbebcc3dc266029777e07dc717040659ea0d1fc0', 'sha256:51d46d5499642bc02b961b93ad411ee2aaf05684fbbf32bd5988ef3ce09acab5', 'sha256:4b2264e63aa292ea89f891af4af5662052281f369d7ede7f669297782efd67b8'),
    (198, 'CoolingTowerType.__str__', 'function', 'sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e', 'sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab', 'sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8'),
    (240, 'Fuel', 'class', 'sha256:66a9b58b66331699893ea17fec4d94a5b9cd95e109774f0d31464255e1e445f9', 'sha256:635c8e3b9a25578e49277b0c78b37587a71851c812f655ddb6ce27e20aef6028', 'sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726'),
    (241, 'Fuel.DISTRICTHEATING', 'constant', 'sha256:806c9ca0ad5437ea710337989299e205820b791b56cd74060ad0b745b7b96763', 'sha256:44fcb87674f3bca0666ad5e793e584402f1b77d3b247268f66d0c101a043c345', 'sha256:84aa18e9f957ae38a141ddaf3d4094c9b7eb732ac9d31a08beca92eb01681018'),
    (242, 'Fuel.ELECTRICITY', 'constant', 'sha256:dece9e858b853989bfd0ec919c8a06b11ad855c43ea68b022a3cceab563c33f6', 'sha256:2843207402e54693bbb9439a6b0c6e41fcfdbf56a66599ca188c2d193f480742', 'sha256:b28247e70157f807cfa6cd873ef598aef56b444217859a27b96112696faefe6e'),
    (243, 'Fuel.LPG', 'constant', 'sha256:c70f84e9bfa4fbe3a6d4136ae396aabfadf1727b1bc5fa24322f4651603371aa', 'sha256:2fc54e9ce0fcd25daf93ff36fa981e652a7efb0ecde9f32d577f38f005bdf429', 'sha256:bb9db9c99c8c3cf7c03f727751cdd78254971efcf82d2f6cd8ff288082ea20e8'),
    (244, 'Fuel.NATURALGAS', 'constant', 'sha256:501607887107fbad216e2e24aa4cfbe86101dcbd066bac82718df38a608b0b90', 'sha256:f3f8fdbeeab11eb271ce5f12c06dc91911b5e1c9d37968720ac767c1aa2e2abb', 'sha256:aadd2b15b687e587bb1b95172f5ac82cacc601847218dd5e2dd8a65b26fed89f'),
    (245, 'Fuel.OIL', 'constant', 'sha256:24bb42a15323a13a7af5293bb7623ee5403ecdca81a2abc2aa5020e57b1125d8', 'sha256:0acb7923f6ac235f1334f157a92549dce200704f11a1f9437ce54c64b9f9db49', 'sha256:5b39538afcd29f0a39077426ffa4fbf5386e981d062fa71bc0c31a518488b0d8'),
    (246, 'Fuel.__str__', 'function', 'sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e', 'sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab', 'sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8'),
    (247, 'Fuel.to_dragon', 'function', 'sha256:7ce396261ba0090f0d97c04fd0535641ed453da15ce744c5cc90c1bea00f4c70', 'sha256:d2a4cbd68de1f882056f396dcdb7c01023dacbfeddada1f18cc677fcfec750f1', 'sha256:2f4d557d0c78aae9016358b851a6ce98c0ff01f8357df3b63ee8a049702736b5'),
    (267, 'NoneSource', 'class', 'sha256:8824a756e9240d1b2cf967300a6e1aee791ed895e490f57170f11c08dbdeea63', 'sha256:7cdc1df6d2dd5a079f86b9dfbb2eb32f04766b0b826b1fdabbcd626d4f836efa', 'sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726'),
    (268, 'NoneSource.ID', 'constant', 'sha256:dbf0ef4b5a6dc77c2fafe0a6f5a56b533834750ecceab47d8894e9c7d0b4ba1d', 'sha256:53aff98451b4446011fd6ceb8ad4c82d8b6496f9ed60f7e324e8316249540942', 'sha256:06fd821ee7d9de20392b3e8dff6dc0d2ae83b5e2a32e40d689305a2de3dcb7db'),
    (269, 'NoneSource.__new__', 'function', 'sha256:758d9c0bdcfdb11bb00e2fdade1b60d96bfe5ec0b21e2ccb0dae7939c32e0af4', 'sha256:18f95cf2d0b4d9493b8ec7815066aa0ea4efcf73a25efdf8f6acd9776838bb86', 'sha256:c071aa1cf7e444a483ebbb51406c235041d08f1f875a86e72e8f850781bbf1dd'),
    (270, 'NoneSource.to_dragon', 'function', 'sha256:c8347dc8847f80d58fe6883b055d22c24b5a42fa39582bf034a427e2ca9c5237', 'sha256:a1cb1a18cbf0115a8c02928192e6e9da52ae4ee12cf2818024c93475d5954103', 'sha256:3ffc574f787def97e42c77e072f21c33ba8a6bf1977ce88cd7928e3bdb5f4fcc'),
    (319, 'SourceSystem', 'class', 'sha256:9b6905f8f1fdfe2d10a2933b067e9b01d4213cee0e024433dfcbd4fb862ceaf1', 'sha256:bdedd67007aecfbec90d015dcd141a254e412ccf7c62e92b81704cebb53bdde9', 'sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726'),
    (320, 'SourceSystem.TYPE_MAPPER', 'constant', 'sha256:813567e31e57909cf80e52fba5cba56108f28f3d594203f3b1ab67212ecb4ca2', 'sha256:5d7527784e9e19021b8afabc0ff3bbdfebc4e85bcb909936d0e16b83d7cb4dff', 'sha256:b9eba64579e158cc0b805b796a183e3a7be5ab8d11af2381a93a3bea7a73caad'),
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
TARGET_HASHES = {item["symbol"]: item["symbol_hash"] for item in TARGET_RECEIPTS}
EXPECTED_TARGET_INDICES = tuple(item["inventory_index"] for item in TARGET_RECEIPTS)
EXPECTED_SOURCE_INDICES = tuple(range(135, 337))
EXPECTED_EXCLUDED_INDICES = (
    137, 138, 140, 141, 149, 150, 152, 153, 159, 160, 162, 163,
    172, 173, 175, 176, 201, 202, 204, 205, 211, 212, 214, 215,
    221, 222, 224, 225, 232, 233, 235, 236, 249, 250, 255, 256,
    258, 259, 273, 274, 276, 277, 285, 286, 288, 289, 298, 299,
    301, 302, 310, 311, 313, 314, 327, 328, 330, 331,
)
EXPECTED_DEFERRED_INDICES = tuple(
    index
    for index in EXPECTED_SOURCE_INDICES
    if index not in set(EXPECTED_TARGET_INDICES)
    and index not in set(EXPECTED_EXCLUDED_INDICES)
)
if (
    len(EXPECTED_TARGET_INDICES) != 28
    or len(EXPECTED_DEFERRED_INDICES) != 116
    or len(EXPECTED_EXCLUDED_INDICES) != 58
):
    raise RuntimeError("HVAC enum/base source partition count drifted.")

# Filled after the complete inventory-derived partitions are independently
# generated, then required by validation and regeneration.
EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:501020c049eb5d1de8c21760277d81f4715cbd8e1a561b7038425be0805f9c9e"
)
EXPECTED_DEFERRED_RECEIPTS_SHA256 = (
    "sha256:c33685d13859a420abe1d9420a010d4f246a84f6f0d18ca14f7dd3ff875a19b2"
)
EXPECTED_EXCLUDED_RECEIPTS_SHA256 = (
    "sha256:8b41e85e53a7a7da866de3ea69f3f5b1af668ed98745d5043ca42d390c578367"
)

EXCEPTION_SYMBOLS = {
    "CompressorType.__str__",
    "CoolingTowerControl.__str__",
    "CoolingTowerType.__str__",
    "Fuel.__str__",
    "NoneSource",
    "NoneSource.ID",
    "NoneSource.__new__",
    "NoneSource.to_dragon",
    "SourceSystem",
    "SourceSystem.TYPE_MAPPER",
}
CLASSIFICATIONS = {
    symbol: "exception" if symbol in EXCEPTION_SYMBOLS else "equivalent"
    for symbol in TARGET_SYMBOLS
}
_ADAPTATION_BASES = {
    "CompressorType.__str__": "grm-reader-writer-vocabulary-rather-than-native-enum-tostring",
    "CoolingTowerControl.__str__": "grm-reader-writer-vocabulary-rather-than-native-enum-tostring",
    "CoolingTowerType.__str__": "grm-reader-writer-vocabulary-rather-than-native-enum-tostring",
    "Fuel.__str__": "grm-reader-writer-vocabulary-rather-than-native-enum-tostring",
    "NoneSource": "nullable-resolved-source-reference-rather-than-singleton-sentinel",
    "NoneSource.ID": "null-source-reference-rather-than-special-string-identifier",
    "NoneSource.__new__": "nullable-source-state-rather-than-process-global-singleton",
    "NoneSource.to_dragon": "aggregate-converter-diagnostic-for-unresolved-source-rather-than-null-return",
    "SourceSystem": "sealed-validated-domain-aggregate-rather-than-empty-python-base",
    "SourceSystem.TYPE_MAPPER": "grm-reader-enum-dispatch-rather-than-public-mutable-class-map",
}
ADAPTATIONS = {
    symbol: f"{base}-{TARGET_HASHES[symbol][7:15]}"
    for symbol, base in _ADAPTATION_BASES.items()
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"epsimple-hvac-enums-base-{item['inventory_index']}-{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}


def _native_route(symbol: str) -> str:
    if symbol.startswith("Fuel"):
        if symbol == "Fuel.to_dragon":
            return "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)"
        return "Dragons.SimpleDragon.FuelType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)"
    if symbol.startswith("CompressorType"):
        if symbol == "CompressorType.to_dragon":
            return "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)"
        return "Dragons.SimpleDragon.CompressorType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)"
    if symbol.startswith("CoolingTowerControl"):
        return "Dragons.SimpleDragon.CoolingTowerControl through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)"
    if symbol.startswith("CoolingTowerType"):
        return "Dragons.SimpleDragon.CoolingTowerType through Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) and Dragons.SimpleDragon.GrmWriter.Serialize(GreenRetrofitModel, bool)"
    if symbol.startswith("NoneSource"):
        return "Dragons.SimpleDragon.SupplySystem.SourceSystem nullable reference with Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)"
    if symbol == "SourceSystem":
        return "Dragons.SimpleDragon.SourceSystem constructor and public properties"
    if symbol == "SourceSystem.TYPE_MAPPER":
        return "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?) with Dragons.SimpleDragon.SourceSystemType"
    raise RuntimeError(f"No reviewed native route for {symbol}.")


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}
NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 6_894,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SourceSystem.cs",
        "sha256": "sha256:c96df1bb42da5df66b3c4cbf61b800c9bf8450b4b8e427d97929809bca4e8cad",
    },
    {
        "bytes": 6_465,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SupplySystem.cs",
        "sha256": "sha256:1858281dcb5ea2df12a09c0c19caba77cf785a10458fb8d265e882f5695a11c5",
    },
    {
        "bytes": 48_650,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmReader.cs",
        "sha256": "sha256:d91f90946ec19602751fc7818484ca43f85d1c46f9905fa805d8ee8a7281d968",
    },
    {
        "bytes": 16_652,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Serialization/GrmWriter.cs",
        "sha256": "sha256:4048cc4bdfca312a7baae54c7055bb3aa7177ee6a8143ed9ef1d182353df1842",
    },
    {
        "bytes": 87_343,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Conversion/GreenRetrofitConversion.cs",
        "sha256": "sha256:0a0774b4461442b2a3cccf68d39fbc236104a2aa13611e0d27c38f27aa2fe5fd",
    },
)

PREFIX = "epsimple-hvac-enums-base."
CASE_SPECS = (
    (
        "C01",
        "compressor-values-order-lookup-string-and-conversion",
        "enum",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("CompressorType")),
    ),
    (
        "C02",
        "cooling-tower-control-values-order-string-and-lookup",
        "enum",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("CoolingTowerControl")),
    ),
    (
        "C03",
        "cooling-tower-type-values-order-string-and-lookup",
        "enum",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("CoolingTowerType")),
    ),
    (
        "F01",
        "fuel-values-order-lookup-string-and-conversion",
        "enum",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("Fuel")),
    ),
    (
        "N01",
        "none-source-singleton-id-new-and-conversion",
        "sentinel",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("NoneSource")),
    ),
    (
        "S01",
        "source-system-base-and-type-mapper-topology",
        "base",
        tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("SourceSystem")),
    ),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 6

# Filled from an independent exact-runtime generation after the observation
# surface is finalized.  Empty values are accepted only during bootstrapping.
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:32a219d193c6a79c54df2c58c55afc045ead9f819f1005201070cfb8c27d8104"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8"
)
EXPECTED_RELOCATION_SNAPSHOT_SHA256 = (
    "sha256:ee4d52a9bf09e386f30abb4498166fb4480d770ca2801bdbcad93910100bff7e"
)
EXPECTED_NATIVE_AUDIT_SHA256 = (
    "sha256:87b90c9941fa642be4ef213c3010a4f85119fbde45786454d564dd6ceca503ab"
)
EXPECTED_FACT_SHA256 = {
    "epsimple-hvac-enums-base.compressor-values-order-lookup-string-and-conversion": "sha256:8b67c867364b112d383744e8357daed43f93e75a32352ae0255474d93afc7f2d",
    "epsimple-hvac-enums-base.cooling-tower-control-values-order-string-and-lookup": "sha256:d843b804c6f08d29c482f9eac898fd8e6a169770e6f64889aa2488acb6930bfc",
    "epsimple-hvac-enums-base.cooling-tower-type-values-order-string-and-lookup": "sha256:43c5920065fa1fd0aa558b6f4f96ded8f8791494ff79899935d647d666407ab6",
    "epsimple-hvac-enums-base.fuel-values-order-lookup-string-and-conversion": "sha256:9429dc4285592819464151aae71b8fe663bd60227020e08a3255e764c4c87394",
    "epsimple-hvac-enums-base.none-source-singleton-id-new-and-conversion": "sha256:e92e8a5aa21f23c59ebab2ce092103d26ffa07d12ab3e91ed5e5d683ceacd5cb",
    "epsimple-hvac-enums-base.source-system-base-and-type-mapper-topology": "sha256:db863d8ee217410e14ea263c8936b97a5085ce69d3b9debefb86f120b15da146",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-hvac-enums-base.compressor-values-order-lookup-string-and-conversion": "sha256:4b84056d792dc115be0625844d1ee81b63416861b9781a2ae1ce4be85349bb44",
    "epsimple-hvac-enums-base.cooling-tower-control-values-order-string-and-lookup": "sha256:5b69b936cd8308c6b71198ec5b6c9fa0de0f5cafd783606e93ae1671102e2e6c",
    "epsimple-hvac-enums-base.cooling-tower-type-values-order-string-and-lookup": "sha256:6f94330e40c55537f06af7c8516b12409db018611561834b0d83ec0f54c0d237",
    "epsimple-hvac-enums-base.fuel-values-order-lookup-string-and-conversion": "sha256:21276d5ad5c506764f380b100bae58206233aaea51e0c4267534fd4e9fa950d8",
    "epsimple-hvac-enums-base.none-source-singleton-id-new-and-conversion": "sha256:649b4f64a1aed7a8c45e179349352986650711a63534bb8ddf869277a64eed6f",
    "epsimple-hvac-enums-base.source-system-base-and-type-mapper-topology": "sha256:7b8a645e61bdc99e1b6e1fe1770e59157d363a6af353b76afe13fb92d201034f",
}
EXPECTED_CASES_SHA256 = (
    "sha256:f90df1feee80855dfa215d58ce0ee856d0b9e128b0bf77332eabf4fba0c92d10"
)

RAW_ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,}")
WINDOWS_PATH_PATTERN = re.compile(r"(?i)(?:^|[\s=:'\"])[a-z]:[\\/]")
POSIX_PATH_PATTERN = re.compile(
    r"(?:^|[\s=:'\"])/(?:home|tmp|users|var|private|mnt|workspace)(?:/|\\)",
    re.IGNORECASE,
)
GUID_PATTERN = re.compile(
    r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b"
)
TIMESTAMP_PATTERN = re.compile(r"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "assertion_ids": {symbol: ASSERTION_IDS[symbol] for symbol in symbols},
            "category": category,
            "code": code,
            "context_symbols": [],
            "id": PREFIX + slug,
            "target_symbols": list(symbols),
        }
        for code, slug, category, symbols in CASE_SPECS
    )
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC enum/base cases are not an exact target partition.")
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
    SUPPORT.require_exact_keys(
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
        index = receipt["inventory_index"]
        if value["symbols"][index] != _descriptor(receipt):
            raise SystemExit(f"HVAC enum/base target receipt drifted at index {index}.")
    source_indices = tuple(
        index
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    )
    if source_indices != EXPECTED_SOURCE_INDICES:
        raise SystemExit("The HVAC source declaration bounds drifted.")
    if sorted(
        (*EXPECTED_TARGET_INDICES, *EXPECTED_DEFERRED_INDICES, *EXPECTED_EXCLUDED_INDICES)
    ) != list(EXPECTED_SOURCE_INDICES):
        raise SystemExit("The HVAC source is not an exact target/deferred/OOS partition.")

    def receipts(indices: tuple[int, ...]) -> list[dict[str, Any]]:
        return [
            {"inventory_index": index, **value["symbols"][index]}
            for index in indices
        ]

    targets = list(TARGET_RECEIPTS)
    deferred = receipts(EXPECTED_DEFERRED_INDICES)
    excluded = receipts(EXPECTED_EXCLUDED_INDICES)
    for label, items, expected_hash in (
        ("target", targets, EXPECTED_TARGET_RECEIPTS_SHA256),
        ("deferred", deferred, EXPECTED_DEFERRED_RECEIPTS_SHA256),
        ("excluded", excluded, EXPECTED_EXCLUDED_RECEIPTS_SHA256),
    ):
        if expected_hash and canonical_sha256(items) != expected_hash:
            raise SystemExit(f"Pinned HVAC {label} receipt partition drifted.")
    return {
        "content_sha256": aggregate,
        "deferred_receipts": deferred,
        "excluded_receipts": excluded,
        "file": source_file,
        "files": value["files"],
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": targets,
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


def _validate_source_tree(source_root: Path) -> None:
    source = source_root / Path(SOURCE_PATH).relative_to("src")
    if (
        not source.is_file()
        or source.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(source) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported EPlusSimple HVAC source drifted.")


def _make_package(name: str, directory: Path) -> ModuleType:
    module = ModuleType(name)
    module.__file__ = str(directory / "__init__.py")
    module.__package__ = name
    module.__path__ = [str(directory)]  # type: ignore[attr-defined]
    return module


def _load_module(name: str, path: Path) -> ModuleType:
    spec = importlib.util.spec_from_file_location(name, path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot import pinned module {name} from {path}.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    try:
        spec.loader.exec_module(module)
    except BaseException:
        sys.modules.pop(name, None)
        raise
    if Path(module.__file__).resolve() != path.resolve():
        raise SystemExit(f"Pinned module {name} resolved outside its source file.")
    return module


def _clear_local_modules() -> None:
    for name in list(sys.modules):
        if name in {"epsimple", "idragon"} or name.startswith(("epsimple.", "idragon.")):
            sys.modules.pop(name, None)


def _audit_loaded_local_modules(
    source_root: Path, inventory: dict[str, Any]
) -> list[dict[str, Any]]:
    files = {item["path"]: item for item in inventory["files"]}
    result: list[dict[str, Any]] = []
    for name, module in sorted(sys.modules.items()):
        if not (
            name in {"epsimple", "idragon"}
            or name.startswith(("epsimple.", "idragon."))
        ):
            continue
        filename = getattr(module, "__file__", None)
        if not isinstance(filename, str) or Path(filename).suffix != ".py":
            continue
        path = Path(filename).resolve()
        try:
            relative = path.relative_to(source_root.resolve()).as_posix()
        except ValueError as error:
            raise RuntimeError(f"Local module {name} escaped the source root.") from error
        inventory_path = "src/" + relative
        receipt = files.get(inventory_path)
        if receipt is None or sha256_file(path) != receipt["content_hash"]:
            raise RuntimeError(f"Loaded local module receipt drifted: {name}")
        result.append(
            {
                "ast_sha256": receipt["ast_hash"],
                "bytes": path.stat().st_size,
                "module": name,
                "path": inventory_path,
                "source_sha256": receipt["content_hash"],
            }
        )
    required = {
        "epsimple",
        "epsimple.constants",
        "epsimple.core",
        "epsimple.core.hvac",
        "idragon",
        "idragon.dragon",
        "idragon.dragon.hvac",
        "idragon.utils",
    }
    if not required.issubset({item["module"] for item in result}):
        raise RuntimeError("The isolated HVAC import omitted required local modules.")
    return result


@contextmanager
def _isolated_import(
    source_root: Path, inventory: dict[str, Any]
) -> Iterator[tuple[ModuleType, list[dict[str, Any]]]]:
    _validate_source_tree(source_root)
    saved = {
        name: module
        for name, module in sys.modules.items()
        if name in {"epsimple", "idragon"}
        or name.startswith(("epsimple.", "idragon."))
    }
    previous_path = list(sys.path)
    _clear_local_modules()
    sys.path.insert(0, str(source_root.resolve()))
    try:
        epsimple_dir = source_root / "epsimple"
        core_dir = epsimple_dir / "core"
        sys.modules["epsimple"] = _make_package("epsimple", epsimple_dir)
        sys.modules["epsimple.core"] = _make_package("epsimple.core", core_dir)
        _load_module("epsimple.constants", epsimple_dir / "constants.py")
        importlib.import_module("idragon")
        hvac = _load_module("epsimple.core.hvac", core_dir / "hvac.py")
        loaded = _audit_loaded_local_modules(source_root, inventory)
        yield hvac, loaded
    finally:
        _clear_local_modules()
        sys.modules.update(saved)
        sys.path[:] = previous_path


def _copy_source_tree(source_root: Path, relocated_root: Path) -> None:
    def ignore(_: str, names: list[str]) -> set[str]:
        return {
            name
            for name in names
            if name == "__pycache__" or name.endswith((".pyc", ".pyo"))
        }

    relocated_root.mkdir(parents=True)
    for package in ("epsimple", "idragon"):
        shutil.copytree(
            source_root / package,
            relocated_root / package,
            ignore=ignore,
        )


def _runtime_member(module: ModuleType, symbol: str) -> Any:
    value: Any = module
    for token in symbol.split("."):
        value = inspect.getattr_static(value, token)
    return value


def _runtime_signatures(module: ModuleType) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for symbol in TARGET_SYMBOLS:
        value = _runtime_member(module, symbol)
        if callable(value):
            try:
                signature = str(inspect.signature(value))
            except (TypeError, ValueError):
                signature = "unavailable"
            result[symbol] = {
                "signature": signature,
                "type": type(value).__name__,
            }
        else:
            result[symbol] = {
                "runtime_class": f"{type(value).__module__}.{type(value).__name__}",
                "type": "constant",
            }
    return result


def _exception(operation: Callable[[], Any]) -> dict[str, Any]:
    try:
        operation()
    except BaseException as error:  # noqa: BLE001 - exact boundary is oracle data.
        return {
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned"}


def _dragon_member(member: Any) -> dict[str, Any]:
    return {
        "module": type(member).__module__,
        "name": member.name,
        "string": str(member),
        "type": type(member).__name__,
        "value": member.value,
    }


def _enum_facts(enum_type: Any, *, convert: bool) -> dict[str, Any]:
    members: list[dict[str, Any]] = []
    for member in enum_type:
        fact = {
            "equal_to_raw_string": member == member.value,
            "format_empty": format(member, ""),
            "is_str_instance": isinstance(member, str),
            "name": member.name,
            "name_lookup_is_same": enum_type[member.name] is member,
            "repr": repr(member),
            "string": str(member),
            "value": member.value,
            "value_lookup_is_same": enum_type(member.value) is member,
        }
        if convert:
            converted = member.to_dragon()
            fact["dragon"] = _dragon_member(converted)
            fact["dragon_repeat_same_identity"] = member.to_dragon() is converted
        members.append(fact)
    return {
        "class": enum_type.__name__,
        "class_module": enum_type.__module__,
        "duplicate_alias_count": len(enum_type.__members__) - len(list(enum_type)),
        "invalid_name": _exception(lambda: enum_type["NOT_A_MEMBER"]),
        "invalid_value": _exception(lambda: enum_type("not-a-member")),
        "is_enum_subclass": any(base.__name__ == "Enum" for base in enum_type.__mro__),
        "is_str_subclass": issubclass(enum_type, str),
        "iteration_names": [member.name for member in enum_type],
        "iteration_values": [member.value for member in enum_type],
        "member_count": len(list(enum_type)),
        "member_names": list(enum_type.__members__),
        "members": members,
        "mro_names": [base.__name__ for base in enum_type.__mro__],
        "wrong_case_value": _exception(lambda: enum_type(members[0]["value"].upper())),
    }


def _none_source_facts(module: ModuleType) -> dict[str, Any]:
    cls = module.NoneSource
    preexisting = cls._instance is not None
    first = cls.__new__(cls, "ignored-by-new", marker=True)
    second = cls.__new__(cls, object())
    constructed = cls()
    constructed_with_arguments = cls("ignored-by-constructor", marker=True)
    return {
        "base_classes": [base.__name__ for base in cls.__bases__],
        "class_id": cls.ID,
        "constructor_arguments": _exception(lambda: cls("not-accepted-by-object-init")),
        "constructor_arguments_same_identity": constructed_with_arguments is first,
        "constructor_is_direct_new": constructed is first,
        "direct_new_arguments_ignored": second is first,
        "first_instance_preexisting": preexisting,
        "instance_class": type(first).__name__,
        "instance_dictionary_empty": vars(first) == {},
        "instance_id": first.ID,
        "instance_is_source_system": isinstance(first, module.SourceSystem),
        "mapper_inherited_by_identity": cls.TYPE_MAPPER is module.SourceSystem.TYPE_MAPPER,
        "new_signature": str(inspect.signature(cls.__new__)),
        "repeat_constructor_same_identity": cls() is first,
        "to_dragon_is_none": first.to_dragon() is None,
        "to_dragon_repeat_is_none": first.to_dragon() is None,
    }


def _source_system_facts(module: ModuleType) -> dict[str, Any]:
    cls = module.SourceSystem
    mapper = cls.TYPE_MAPPER
    first = cls()
    second = cls()
    mapped = []
    for key, mapped_type in mapper.items():
        mapped.append(
            {
                "callable": callable(mapped_type),
                "inherited_mapper_by_identity": mapped_type.TYPE_MAPPER is mapper,
                "is_source_subclass": issubclass(mapped_type, cls),
                "key": key,
                "module": mapped_type.__module__,
                "type": mapped_type.__name__,
            }
        )
    copied = dict(mapper)
    copied["probe-only"] = cls
    return {
        "class_module": cls.__module__,
        "constructor_signature": str(inspect.signature(cls)),
        "declared_public_members": sorted(
            name for name in cls.__dict__ if not name.startswith("_")
        ),
        "fresh_instances_are_distinct": first is not second,
        "fresh_instances_empty": vars(first) == {} and vars(second) == {},
        "has_to_dragon": hasattr(first, "to_dragon"),
        "keyword_constructor_error": _exception(lambda: cls(unexpected=True)),
        "mapper_copy_mutation_preserves_original": "probe-only" not in mapper,
        "mapper_identity_across_accesses": cls.TYPE_MAPPER is mapper,
        "mapper_keys": list(mapper),
        "mapper_type": type(mapper).__name__,
        "mapped_types": mapped,
        "missing_key_error": _exception(lambda: mapper["missing"]),
        "none_source_absent_from_values": module.NoneSource not in mapper.values(),
        "positional_constructor_error": _exception(lambda: cls("unexpected")),
        "unhashable_key_error": _exception(lambda: mapper[[]]),
    }


def _execute_cases(module: ModuleType) -> dict[str, dict[str, Any]]:
    return {
        EXPECTED_CASE_IDS[0]: _enum_facts(module.CompressorType, convert=True),
        EXPECTED_CASE_IDS[1]: _enum_facts(module.CoolingTowerControl, convert=False),
        EXPECTED_CASE_IDS[2]: _enum_facts(module.CoolingTowerType, convert=False),
        EXPECTED_CASE_IDS[3]: _enum_facts(module.Fuel, convert=True),
        EXPECTED_CASE_IDS[4]: _none_source_facts(module),
        EXPECTED_CASE_IDS[5]: _source_system_facts(module),
    }


def _relocation_snapshot(module: ModuleType) -> dict[str, Any]:
    return {
        "observations": _execute_cases(module),
        "runtime_signatures": _runtime_signatures(module),
    }


def _dependencies() -> dict[str, str]:
    return {name: importlib.metadata.version(name) for name in EXPECTED_DEPENDENCIES}


def _runtime_receipt() -> dict[str, Any]:
    dependencies = _dependencies()
    return {
        "bootstrap": {
            "bytes": EXPECTED_BOOTSTRAP_BYTES,
            "path": "tools/python-reference/bootstrap_reference.py",
            "sha256": EXPECTED_BOOTSTRAP_SHA256,
        },
        "dependencies": dependencies,
        "dependencies_sha256": canonical_sha256(dependencies),
        "implementation": "cpython",
        "platform": REQUIRED_PLATFORM,
        "pointer_width_bits": REQUIRED_POINTER_WIDTH_BITS,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
        "strict_json_support": {
            "bytes": EXPECTED_SUPPORT_BYTES,
            "path": "tools/python-reference/generate_schedule_type_oracle.py",
            "sha256": EXPECTED_SUPPORT_SHA256,
        },
    }


def _validate_generation_runtime() -> None:
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for HVAC enum/base generation.")
    if sys.platform != REQUIRED_PLATFORM or struct.calcsize("P") * 8 != REQUIRED_POINTER_WIDTH_BITS:
        raise SystemExit("The pinned 64-bit Windows Python runtime is required.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("PYTHONDONTWRITEBYTECODE=1 is required for isolated generation.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    if (
        BOOTSTRAP_PATH.stat().st_size != EXPECTED_BOOTSTRAP_BYTES
        or sha256_file(BOOTSTRAP_PATH) != EXPECTED_BOOTSTRAP_SHA256
    ):
        raise SystemExit("The Python reference bootstrap receipt drifted.")


def _native_audit() -> dict[str, Any]:
    repository_root = Path(__file__).resolve().parents[2]
    for receipt in NATIVE_SOURCE_RECEIPTS:
        path = repository_root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise SystemExit(f"Native production source drifted: {receipt['path']}")
    result = {
        "public_production_routes_only": True,
        "routes_sha256": canonical_sha256(NATIVE_ROUTES),
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        "source_receipts_sha256": canonical_sha256(list(NATIVE_SOURCE_RECEIPTS)),
    }
    digest = canonical_sha256(result)
    if EXPECTED_NATIVE_AUDIT_SHA256 and digest != EXPECTED_NATIVE_AUDIT_SHA256:
        raise SystemExit("Pinned native production route audit drifted.")
    return result


def _expected_contract(signatures: dict[str, Any]) -> dict[str, Any]:
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
        "classification_counts": dict(sorted(Counter(CLASSIFICATIONS.values()).items())),
        "classifications": CLASSIFICATIONS,
        "closure": {
            "deferred_count": len(EXPECTED_DEFERRED_INDICES),
            "exact_one_case_target_partition": True,
            "excluded_count": len(EXPECTED_EXCLUDED_INDICES),
            "full_source_partition": True,
            "source_declaration_count": len(EXPECTED_SOURCE_INDICES),
            "target_count": len(EXPECTED_TARGET_INDICES),
            "target_indices": list(EXPECTED_TARGET_INDICES),
        },
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "expected_receipt_count": len(TARGET_RECEIPTS),
            "full_hvac_declaration_parity_claim": False,
            "native_runtime_executed_by_python_oracle": False,
            "python_behavior_oracle_only": True,
            "relocatable_import_claim": True,
        },
        "expectations": expectations,
        "native_routes": NATIVE_ROUTES,
        "runtime_names": "pinned-python-only-no-native-runtime-name-claims",
        "runtime_signatures": signatures,
        "target_symbols": list(TARGET_SYMBOLS),
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
    _validate_generation_runtime()
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    _validate_source_tree(imported_root)
    repository_root = Path(__file__).resolve().parents[2]
    work_root = repository_root / "temp" / "reference" / "hvac-enums-base-work"
    work_root.mkdir(parents=True, exist_ok=True)

    with _isolated_import(imported_root, inventory) as (module, primary_modules):
        signatures = _runtime_signatures(module)
        observations = _execute_cases(module)
        primary_snapshot = {
            "observations": observations,
            "runtime_signatures": signatures,
        }

    with tempfile.TemporaryDirectory(
        prefix="epsimple-hvac-enums-base-relocation-", dir=work_root
    ) as temporary:
        relocated_root = Path(temporary) / "src"
        _copy_source_tree(imported_root, relocated_root)
        with _isolated_import(relocated_root, inventory) as (
            relocated_module,
            relocated_modules,
        ):
            relocated_snapshot = _relocation_snapshot(relocated_module)
    if primary_snapshot != relocated_snapshot:
        raise RuntimeError("HVAC enum/base observations changed after relocation.")
    if primary_modules != relocated_modules:
        raise RuntimeError("HVAC enum/base loaded module receipts changed after relocation.")

    signature_hash = canonical_sha256(signatures)
    module_hash = canonical_sha256(primary_modules)
    relocation_hash = canonical_sha256(primary_snapshot)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signature_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned HVAC enum/base runtime signatures drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and module_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned HVAC enum/base loaded-module receipt drifted.")
    if EXPECTED_RELOCATION_SNAPSHOT_SHA256 and relocation_hash != EXPECTED_RELOCATION_SNAPSHOT_SHA256:
        raise SystemExit("Pinned HVAC enum/base relocation snapshot drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned HVAC enum/base fact hashes drifted.\n"
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
            "Pinned HVAC enum/base case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned HVAC enum/base aggregate case hash drifted.")

    native_audit = _native_audit()
    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(signatures),
        "deferred_receipts": inventory["deferred_receipts"],
        "excluded_receipts": inventory["excluded_receipts"],
        "fact_sha256": fact_hashes,
        "native_audit": native_audit,
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "inventory_sha256": EXPECTED_INVENTORY_SHA256,
            "isolated_import": {
                "epsimple_core_initializer_executed": False,
                "epsimple_package_initializer_executed": False,
                "loaded_local_modules": primary_modules,
                "loaded_local_modules_sha256": module_hash,
                "relocation_snapshot_sha256": relocation_hash,
                "source_location_count": 2,
            },
            "path": SOURCE_PATH,
            "resource_receipts": [],
            "resource_receipts_sha256": canonical_sha256([]),
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
        },
    }
    validate_oracle(result)
    return result


def _validate_safe_string(value: str, location: str) -> None:
    for pattern, label in (
        (RAW_ADDRESS_PATTERN, "raw object address"),
        (WINDOWS_PATH_PATTERN, "absolute Windows path"),
        (POSIX_PATH_PATTERN, "absolute POSIX path"),
        (GUID_PATTERN, "GUID-like value"),
        (TIMESTAMP_PATTERN, "timestamp"),
    ):
        if pattern.search(value):
            raise RuntimeError(f"Forbidden {label} at {location}.")


def _validate_typed_value(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind not in {"bool", "int", "float", "float-nonfinite"}:
        return False
    if kind == "bool":
        if set(value) != {"kind", "value"} or not isinstance(value["value"], bool):
            raise RuntimeError(f"Noncanonical bool encoding at {location}.")
    elif kind == "int":
        token = value.get("value")
        if (
            set(value) != {"kind", "value"}
            or not isinstance(token, str)
            or str(int(token)) != token
        ):
            raise RuntimeError(f"Noncanonical int encoding at {location}.")
    elif kind == "float":
        if set(value) != {"hex", "kind", "repr"}:
            raise RuntimeError(f"Noncanonical float encoding at {location}.")
        parsed = float.fromhex(value["hex"])
        if (
            not math.isfinite(parsed)
            or parsed.hex() != value["hex"]
            or repr(parsed) != value["repr"]
        ):
            raise RuntimeError(f"Noncanonical finite float at {location}.")
    else:
        if (
            set(value) != {"kind", "value"}
            or value["value"]
            not in {"nan", "positive-infinity", "negative-infinity"}
        ):
            raise RuntimeError(f"Noncanonical nonfinite float at {location}.")
    return True


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if value is None or isinstance(value, bool) or isinstance(value, int):
        return
    if isinstance(value, float):
        raise RuntimeError(f"Raw float is forbidden at {location}.")
    if isinstance(value, str):
        _validate_safe_string(value, location)
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        if _validate_typed_value(value, location):
            return
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string JSON key at {location}.")
            _validate_safe_string(key, location + ".<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Unsafe value type {type(value).__name__} at {location}.")


def load_json_without_duplicates_text(text: str) -> dict[str, Any]:
    def hook(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, item in pairs:
            if key in result:
                raise ValueError(f"Duplicate key: {key}")
            result[key] = item
        return result

    value = json.loads(
        text,
        object_pairs_hook=hook,
        parse_constant=lambda token: (_ for _ in ()).throw(ValueError(token)),
    )
    if not isinstance(value, dict):
        raise RuntimeError("Strict JSON text root is not an object.")
    return value


def validate_oracle(value: dict[str, Any]) -> None:
    expected_keys = {
        "case_sha256",
        "cases",
        "cases_sha256",
        "consumer_contract",
        "deferred_receipts",
        "excluded_receipts",
        "fact_sha256",
        "native_audit",
        "runtime",
        "schema",
        "symbols",
        "target_receipts",
        "upstream",
    }
    if not isinstance(value, dict) or set(value) != expected_keys:
        raise RuntimeError("HVAC enum/base oracle root key set drifted.")
    if value["schema"] != SCHEMA:
        raise RuntimeError("HVAC enum/base schema drifted.")
    if value["target_receipts"] != list(TARGET_RECEIPTS):
        raise RuntimeError("HVAC enum/base target receipts drifted.")
    if [item.get("inventory_index") for item in value["deferred_receipts"]] != list(
        EXPECTED_DEFERRED_INDICES
    ):
        raise RuntimeError("HVAC enum/base deferred receipt indices drifted.")
    if [item.get("inventory_index") for item in value["excluded_receipts"]] != list(
        EXPECTED_EXCLUDED_INDICES
    ):
        raise RuntimeError("HVAC enum/base excluded receipt indices drifted.")
    for label, items, expected_hash in (
        ("target", value["target_receipts"], EXPECTED_TARGET_RECEIPTS_SHA256),
        ("deferred", value["deferred_receipts"], EXPECTED_DEFERRED_RECEIPTS_SHA256),
        ("excluded", value["excluded_receipts"], EXPECTED_EXCLUDED_RECEIPTS_SHA256),
    ):
        if expected_hash and canonical_sha256(items) != expected_hash:
            raise RuntimeError(f"Pinned HVAC {label} receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("HVAC enum/base symbol descriptors drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("HVAC enum/base runtime signatures are absent.")
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise RuntimeError("HVAC enum/base runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(signatures):
        raise RuntimeError("HVAC enum/base consumer contract drifted.")
    if value["runtime"] != _runtime_receipt():
        raise RuntimeError("HVAC enum/base runtime receipt drifted.")
    if value["native_audit"] != _native_audit():
        raise RuntimeError("HVAC enum/base native audit drifted.")

    upstream = value["upstream"]
    if not isinstance(upstream, dict) or set(upstream) != {
        "commit",
        "inventory_sha256",
        "isolated_import",
        "path",
        "resource_receipts",
        "resource_receipts_sha256",
        "source",
    }:
        raise RuntimeError("HVAC enum/base upstream key set drifted.")
    expected_static = {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "resource_receipts": [],
        "resource_receipts_sha256": canonical_sha256([]),
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }
    for key, expected in expected_static.items():
        if upstream.get(key) != expected:
            raise RuntimeError(f"HVAC enum/base upstream field drifted: {key}")
    isolated = upstream["isolated_import"]
    if not isinstance(isolated, dict) or set(isolated) != {
        "epsimple_core_initializer_executed",
        "epsimple_package_initializer_executed",
        "loaded_local_modules",
        "loaded_local_modules_sha256",
        "relocation_snapshot_sha256",
        "source_location_count",
    }:
        raise RuntimeError("HVAC enum/base isolated-import key set drifted.")
    if (
        isolated["source_location_count"] != 2
        or isolated["epsimple_package_initializer_executed"]
        or isolated["epsimple_core_initializer_executed"]
    ):
        raise RuntimeError("HVAC enum/base relocation/initializer claim drifted.")
    loaded = isolated["loaded_local_modules"]
    if (
        not isinstance(loaded, list)
        or isolated["loaded_local_modules_sha256"] != canonical_sha256(loaded)
    ):
        raise RuntimeError("HVAC enum/base loaded-module receipt drifted.")
    if (
        EXPECTED_LOADED_LOCAL_MODULES_SHA256
        and canonical_sha256(loaded) != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise RuntimeError("Pinned HVAC enum/base loaded modules drifted.")
    if (
        EXPECTED_RELOCATION_SNAPSHOT_SHA256
        and isolated["relocation_snapshot_sha256"]
        != EXPECTED_RELOCATION_SNAPSHOT_SHA256
    ):
        raise RuntimeError("Pinned HVAC enum/base relocation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("HVAC enum/base case count drifted.")
    if [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("HVAC enum/base case order drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        if set(case) != {*definition, "python"}:
            raise RuntimeError(f"HVAC enum/base case key set drifted: {definition['id']}")
        for key, expected in definition.items():
            if case[key] != expected:
                raise RuntimeError(f"HVAC enum/base case definition drifted: {definition['id']}")
        python = case["python"]
        if (
            not isinstance(python, dict)
            or set(python) != {"facts", "facts_sha256", "outcome"}
            or python["outcome"] != "observed"
        ):
            raise RuntimeError(f"HVAC enum/base Python observation drifted: {definition['id']}")
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(f"HVAC enum/base inline fact hash drifted: {definition['id']}")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("HVAC enum/base fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned HVAC enum/base fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("HVAC enum/base case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned HVAC enum/base case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("HVAC enum/base aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned HVAC enum/base aggregate hash drifted.")
    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC enum/base exact target closure drifted.")
    _validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("HVAC enum/base strict JSON round trip drifted.")


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
        f"Wrote {len(oracle['cases'])} HVAC enum/base cases covering "
        f"{len(TARGET_RECEIPTS)} declarations: {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception, aggregate {oracle['cases_sha256']}."
    )


if __name__ == "__main__":
    main()

"""Generate the pinned ``epsimple/constants.py`` numeric-constants oracle.

The corpus covers the five engineering constant containers and their twenty-four
public numeric declarations. Exactly three deterministic cases bind each target
symbol. Package metadata, directory paths, ID prefixes, and special tags remain
outside this fixture.
"""

from __future__ import annotations

import argparse
import ast
from collections import Counter, defaultdict
from contextlib import contextmanager
import copy
import importlib
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import re
import sys
from typing import Any, Iterator


SCHEMA = "dragons.python-reference.epsimple-constants-numeric.v1"
SOURCE_PATH = "src/epsimple/constants.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:d5dd5241ec90b14ba3708a525cd74279a8cdc238164a5b8544c4c82b05a29897"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:6740f081f087834aadfef0c11da6cdbe11f907dc170b48ebaa287e000eb6e27b"
)
MODEL_SOURCE_PATH = "src/epsimple/core/model.py"
EXPECTED_MODEL_SOURCE_SHA256 = (
    "sha256:71dc9bb8d97e829c27d9b5d19ef88709af9613f9e53f60807d54ceb2922e4532"
)
EXPECTED_MODEL_SOURCE_AST_SHA256 = (
    "sha256:f79918272c07515ee4ae98fa62f4ca5d5d703e5e2faa334f72d6a6966e1e2447"
)


def _receipt(
    kind: str,
    signature_hash: str,
    body_hash: str,
    symbol_hash: str,
) -> dict[str, str]:
    return {
        "body_hash": body_hash,
        "kind": kind,
        "signature_hash": signature_hash,
        "symbol_hash": symbol_hash,
    }


EXPECTED_SYMBOL_RECEIPTS = {
    "ConvectionHeatTransfer": _receipt(
        "class",
        "sha256:f346b39d59e5bdb4e369113e55c6e167dd2fa73da3021d8f90e896b7a936284c",
        "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "sha256:2d68c0fd189f85734d82a18e0312c03fda9f734cdcf7b3d72bf2d3e356c29577",
    ),
    "ConvectionHeatTransfer.IN": _receipt(
        "constant",
        "sha256:1b01e1353c2601d136ef91cfac7fe225d304239324671935290bad727f78c005",
        "sha256:b6d082785c99259867d0dc0d77d76ac63c22d1a444b8f575a2378b70ee151b01",
        "sha256:f4d1b69119dd3619805511a2f0b25fddbe63554a38c3001f672cd1efdcea1edf",
    ),
    "ConvectionHeatTransfer.OUT": _receipt(
        "constant",
        "sha256:f0e5bfe691366195a447f74f0d6201598e095b6881803351f3030d781cb1892e",
        "sha256:08a45f66df3ebb992ae824045dc367e08a223ac790ab9179e048ca4a0a5dc32a",
        "sha256:c36faf62dd987cd8561bd92a0c78e218baa23444f8d487070612ff8b5b3aa5b9",
    ),
    "Site2CO2": _receipt(
        "class",
        "sha256:58f61ff1835c93a6b0956d0a15d937281796e98f01297ba91c6db6e874fc63d6",
        "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "sha256:9ff40d942ec30fa90a8b95e5d24205d33eea26afc4e143ee8457bf440b0a6270",
    ),
    "Site2CO2.DISTRICTHEATING": _receipt(
        "constant",
        "sha256:7192cff6cf324d01d37009b3eb33734d4266d04f498ecb7c04589663ff47cf36",
        "sha256:7032add1aef46b96675e97d3f35dc6381c8925d1a19ea5ca84df707fa762046a",
        "sha256:1d7b874c5a80a7b28fe56c8dbc5b20c395c260feb0c339657a1ee76922bf447d",
    ),
    "Site2CO2.ELECTRICITY": _receipt(
        "constant",
        "sha256:72aea6596570580ccc52c17466c4628382e08da6627bd43a5b5ed57b9d682c2f",
        "sha256:dc2d4918d3dc700bcca1e3c3a4791792a163ca47cc8c43c8f2bf2260ceeec73c",
        "sha256:427886a21467ffa2e70b09b222f44e9185d1bdc8cf3ff6cc3d858f370b439b5a",
    ),
    "Site2CO2.LPG": _receipt(
        "constant",
        "sha256:679d716960bc375fd53e3e3de5ed9351c5386c9860c457b7f0b4d59d757955b9",
        "sha256:fdf8e9a54a0c32c67b3cb8278615f6e369eccd60728a4e99b7b6720b8a326103",
        "sha256:68cf7791fd2569d21d8dfdc36fffb54c82dcb4493c258708b2da1bda096b62f1",
    ),
    "Site2CO2.NATURALGAS": _receipt(
        "constant",
        "sha256:3219fb8a9ad5ecac99020288d75b2f1fc7ac14f8efc0d03092ce23f6855588f7",
        "sha256:195526433a8bd4b1bc2e83f85212e7d705c50ed510e557f10aa44f7fe83c4d41",
        "sha256:860c2f939cbd8d3c6c89855296706c774d24d3a93a70fb1595e7f04cec6a9e90",
    ),
    "Site2CO2.OIL": _receipt(
        "constant",
        "sha256:8cd43de8532cfc3648f5c42bbef75ffb179a0bbc291bdc65f2e20c3cd562a90d",
        "sha256:ae67781472c12cb4bdb378326502ea5d61ab41c783607453740f066589b29598",
        "sha256:4a1979a27d16ba6b4e0765d2d5f97142a35f5485967e05be6e52916100e07727",
    ),
    "Site2Cost": _receipt(
        "class",
        "sha256:e30974309debfb1ded65597741074db22e42046c3474a3c97a4ec2fc0cec9751",
        "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "sha256:0f8750781047825eb5c4eea60e058821a61701bf39a930e8a78f9a31e2c9566e",
    ),
    "Site2Cost.DISTRICTHEATING": _receipt(
        "constant",
        "sha256:baaeaa529b5cf356ff6f5975d9ac9cddbbfe68b10fda1d105203b306bdb06506",
        "sha256:fc5445f71f3d40f721d85bd50509c6b83f3863cb0704887e5231a7859fa8d2dd",
        "sha256:956e2b0d76110c8aa33eb3b33fec599d6e1ea9f8c98b7cb58d535c5f16884ebb",
    ),
    "Site2Cost.ELECTRICITY": _receipt(
        "constant",
        "sha256:7cd5cc01b3ea53e2080f04dca3fbbecf99ab795221a61df8a1ceff52a24970de",
        "sha256:cef178527e410145519cf69089e01fd78e5600280c283dc1f62279a4fd56498f",
        "sha256:b9b2bc9925459d830c1de8a4e971d5f4597021ec62d980f162e8a7718ac9abff",
    ),
    "Site2Cost.LPG": _receipt(
        "constant",
        "sha256:a2eb07f314178de19dc8709a8c051256a8f86203773b1d619e7aab3513147d4d",
        "sha256:fd6cd001ed389da65ae93f87211c80326ca289e0188269679ac4a5bcdc35dd47",
        "sha256:08fe014b98f9d0492866d4b64446982476f8b07432d981ccd8ff76a96cec5ecf",
    ),
    "Site2Cost.NATURALGAS": _receipt(
        "constant",
        "sha256:45e72751468baaa1d08dc16bde1cb11f8c82797bdfd36abe7c3ae624639d8f16",
        "sha256:cc5db32e4b7f9e9d343246a3f654663763ca62f35d23177fbd39de588bb60f0f",
        "sha256:6c00bbfc4ae58ce5287c7748b5f9dd75141457e56c37b6e9d4a9284a57064055",
    ),
    "Site2Cost.OIL": _receipt(
        "constant",
        "sha256:a00b0901c5edae87145e5d7643f853976f99686f8e1dcc73850de5760d15e7b1",
        "sha256:5570b297853cd48c7931910ccd9e0a5fa97b3ebaef17cea325e4b01e9ed1e57a",
        "sha256:f58bfe501cf9658b50f5541c6fa314ddcb98369d59b760f626fd1318aaf607d1",
    ),
    "Site2Source": _receipt(
        "class",
        "sha256:a0ad366186188fe14567a9661fb67fa7a1b950aa5afad401b8ff10d22f492fb2",
        "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "sha256:763a14c74718b1386a9ddc7a5c0f06ebb769ac08391365d917d1042965292e9d",
    ),
    "Site2Source.DISTRICTHEATING": _receipt(
        "constant",
        "sha256:97f37a60c1b9346e6c48a0affd0acf48bc673a3adcfc06de6f52e93c73d1c193",
        "sha256:6f54899d2973a52f87696315784a866783785a20b9b7222a1cd5d16216ebe662",
        "sha256:5f0ca3b7ed38e426a21befb255af26cd257c025be81734a4e7be8469a777f9f7",
    ),
    "Site2Source.ELECTRICITY": _receipt(
        "constant",
        "sha256:6da7ff4826f9f1c1012fbcd7fcf801cfaa65be65aad36842c0c51626445f2d98",
        "sha256:11748497aa24493f29c98a749db7261b999b4dd5e8da827cb2dae8e0b7fa8f96",
        "sha256:9f6e831e10bc5bee518399cec50e39fc7258fa7186028636d6e7dce89cbd637c",
    ),
    "Site2Source.LPG": _receipt(
        "constant",
        "sha256:e071db60c656b33f740fe3923237e373cb98029cef124a74c2524c909fae40a0",
        "sha256:9d87ff0a62d45b977abbeee07313669faa41a26a1e371fb4de8bd4920ae5ea8c",
        "sha256:f891444c39ad8d08e27afa90b0bd7817d5704ee1b70b187e35c86bf1bf08582e",
    ),
    "Site2Source.NATURALGAS": _receipt(
        "constant",
        "sha256:3575ab52059704e7b3d721927fd400cd5a6435516af21389305b1024b5ce7b95",
        "sha256:9d87ff0a62d45b977abbeee07313669faa41a26a1e371fb4de8bd4920ae5ea8c",
        "sha256:8661aaeabf25d8c5c520b75d20acb9994e59b3153f07d21eafa57971ab6c7394",
    ),
    "Site2Source.OIL": _receipt(
        "constant",
        "sha256:58a3d244b423f6a30f472b721392e57805eac073597c427be88545ff8581398b",
        "sha256:9d87ff0a62d45b977abbeee07313669faa41a26a1e371fb4de8bd4920ae5ea8c",
        "sha256:18468fb1b142964ae9104c7ad816347e9109c40ef4205c7872dc577758efc254",
    ),
    "Unit": _receipt(
        "class",
        "sha256:4207679fe2ede1a951b1882e62a22d8d915b1442dc5d1e1f62925d16cb6422e0",
        "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "sha256:82eeceb9e427512d5ed45c6139c5fb92859289547ded26e7e410b3be3f591b70",
    ),
    "Unit.ACH50_TO_ACH": _receipt(
        "constant",
        "sha256:3afd608864e96c6cbd84dafd3cdfc94ce317a2e2784cea31c8afe3990752c554",
        "sha256:4ee0d3906e46532ef590b16c4f85886bb129c92b697aa36a261bdc1fd09335b5",
        "sha256:fd2a09b09735722d7642be7d9f6f477970306a19540ac5b01d0357ea47c57401",
    ),
    "Unit.FRACTION_TO_PERCENT": _receipt(
        "constant",
        "sha256:1cb497d3cae6e62d4e2c50b1754cf150b4fdce93c973f30509b28cf4fa1e82c4",
        "sha256:d3c3cec052dae85942a722526911012da69bf59aca87bc1229bfbc27211abdd1",
        "sha256:55d3f412e4fc8dc309ceb1e5d298946b289c8adb66736a8de5de2533b5050880",
    ),
    "Unit.M3_PER_S_TO_CMH": _receipt(
        "constant",
        "sha256:574acc67cf8a454280621da85ef059251e2c222aabe5bc1fc7679a3e09d7c3eb",
        "sha256:aa8817177208c34e6d84856ee1bbc0360af016491b5a153690f899cb967626f2",
        "sha256:c67e87d901a7d2de66c51d559d4b4d6552f188503c1f9dc331d5bf698540ea73",
    ),
    "Unit.MM_TO_M": _receipt(
        "constant",
        "sha256:03686fd1f94671e5a411db8c0f4d7a6bc8f62f9033a4f65fecfab0cf2f2a06f8",
        "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf",
        "sha256:78d61c825c4faade4c8268ca8c23a95c00ad44ce68574bc3afaf7791387ba1b5",
    ),
    "Unit.M_TO_MM": _receipt(
        "constant",
        "sha256:ee5969c67823797b883ba77c3c09f0c078a08dde8240b68a2ca79c7a57c70e78",
        "sha256:d1c5df1014d99d4fa0a7e141221a6ba21ecf57cc8755703a7d6229af7a2a376d",
        "sha256:b49a8507bdd65b293983e5930a4b5710befca44ce1583d8ae3ada9d7ddd4c85b",
    ),
    "Unit.PERCENT_TO_FRACTION": _receipt(
        "constant",
        "sha256:13ad23718f631a0cc2b84c7b09e1287564aa549a52c30ec98f10a86b15f6a3fb",
        "sha256:d2dff8ba2e3305a55a5cfcb7f170272f46ce3773420fc2094c6eb318b178a722",
        "sha256:2f91a99f89863099df480e571f6e4f05249479b3adb70f77f1f141838035e240",
    ),
    "Unit.W_TO_KW": _receipt(
        "constant",
        "sha256:3212f8fad3be6cfe8fc6dc7a7391b487924402ac39cacdafd8d9af8686a00085",
        "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf",
        "sha256:9891f5c1310487862261f06e345c18941cad2fac3c22b2210a7a5ee92e22f215",
    ),
}

EXPECTED_MODEL_OBSERVATION_DEPENDENCY = {
    "file": {
        "ast_hash": EXPECTED_MODEL_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_MODEL_SOURCE_SHA256,
        "path": MODEL_SOURCE_PATH,
    },
    "symbols": [
        {
            **_receipt(
                "constant",
                "sha256:aa336779f69a8902021215ad36bc8925e1d599b84b1c2149a383d3313065b1a2",
                "sha256:ddcc9e26678f237b5f7892c086072a5962980b4d4b13bcee47bd9c0d98a52cc6",
                "sha256:ff1cddacd1d221d604e80997d48ef03662bbeb531c45337abde8fcc3f9fc30df",
            ),
            "path": MODEL_SOURCE_PATH,
            "symbol": "GreenRetrofitResult.VALID_DIGITS",
        },
        {
            **_receipt(
                "function",
                "sha256:3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef",
                "sha256:d9c7d1b27a50ae9b04a5278c1d1881309fc297af097af411791f2f1d77e73d5d",
                "sha256:842eb853a7216a84eab7ccc5a04d7454fc7f2572ea9c8e0bc32f73d6ffc84291",
            ),
            "path": MODEL_SOURCE_PATH,
            "symbol": "GreenRetrofitResult.to_source_uses",
        },
    ],
}

TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "ConvectionHeatTransfer": "native-simpledragon-convection-constant-container",
    "Site2CO2": "native-simpledragon-site-to-carbon-dispatch",
    "Site2Cost": "native-simpledragon-site-to-cost-dispatch",
    "Site2Source": "native-simpledragon-site-to-source-dispatch",
    "Unit": "native-simpledragon-unit-conversion-constants",
}

CLASS_SLUGS = {
    "ConvectionHeatTransfer": "convection-heat-transfer",
    "Site2CO2": "site2co2",
    "Site2Cost": "site2cost",
    "Site2Source": "site2source",
    "Unit": "unit",
}
MEMBER_TOKENS = {
    symbol: symbol.split(".", 1)[1].lower().replace("_", "-")
    for symbol in TARGET_SYMBOLS
    if "." in symbol
}
EXPECTED_ASSERTION_IDS = {
    symbol: (
        "epsimple-constants-numeric-"
        + CLASS_SLUGS[symbol.split(".", 1)[0]]
        + ("-" + MEMBER_TOKENS[symbol] if "." in symbol else "")
        + "-"
        + EXPECTED_SYMBOL_HASHES[symbol].removeprefix("sha256:")[:8]
    )
    for symbol in TARGET_SYMBOLS
}

EXPECTED_MEMBER_NAMES = {
    "ConvectionHeatTransfer": ["IN", "OUT"],
    "Site2CO2": ["ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING"],
    "Site2Cost": ["ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING"],
    "Site2Source": ["ELECTRICITY", "NATURALGAS", "LPG", "OIL", "DISTRICTHEATING"],
    "Unit": [
        "MM_TO_M",
        "M_TO_MM",
        "FRACTION_TO_PERCENT",
        "PERCENT_TO_FRACTION",
        "W_TO_KW",
        "ACH50_TO_ACH",
        "M3_PER_S_TO_CMH",
    ],
}
EXPECTED_CANONICAL_NAMES = {
    "ConvectionHeatTransfer": {"IN": "IN", "OUT": "OUT"},
    "Site2CO2": {name: name for name in EXPECTED_MEMBER_NAMES["Site2CO2"]},
    "Site2Cost": {name: name for name in EXPECTED_MEMBER_NAMES["Site2Cost"]},
    "Site2Source": {
        "ELECTRICITY": "ELECTRICITY",
        "NATURALGAS": "NATURALGAS",
        "LPG": "NATURALGAS",
        "OIL": "NATURALGAS",
        "DISTRICTHEATING": "DISTRICTHEATING",
    },
    "Unit": {
        "MM_TO_M": "MM_TO_M",
        "M_TO_MM": "M_TO_MM",
        "FRACTION_TO_PERCENT": "FRACTION_TO_PERCENT",
        "PERCENT_TO_FRACTION": "PERCENT_TO_FRACTION",
        "W_TO_KW": "MM_TO_M",
        "ACH50_TO_ACH": "ACH50_TO_ACH",
        "M3_PER_S_TO_CMH": "M3_PER_S_TO_CMH",
    },
}
EXPECTED_ITERATED_NAMES = {
    class_name: list(dict.fromkeys(canonical.values()))
    for class_name, canonical in EXPECTED_CANONICAL_NAMES.items()
}
EXPECTED_ALIAS_GROUPS = {
    "ConvectionHeatTransfer": [],
    "Site2CO2": [],
    "Site2Cost": [],
    "Site2Source": [["NATURALGAS", "LPG", "OIL"]],
    "Unit": [["MM_TO_M", "W_TO_KW"]],
}
EXPECTED_VALUES = {
    "ConvectionHeatTransfer.IN": "1.22e8ba2e8ba2fp+3",
    "ConvectionHeatTransfer.OUT": "1.7417d05f417d1p+4",
    "Site2CO2.DISTRICTHEATING": "1.161e4f765fd8bp-3",
    "Site2CO2.ELECTRICITY": "1.d0ff972474539p-2",
    "Site2CO2.LPG": "1.dc5d63886594bp-3",
    "Site2CO2.NATURALGAS": "1.9e83e425aee63p-3",
    "Site2CO2.OIL": "1.0a8c154c985f0p-2",
    "Site2Cost.DISTRICTHEATING": "1.7beb851eb851fp+6",
    "Site2Cost.ELECTRICITY": "1.45d70a3d70a3dp+7",
    "Site2Cost.LPG": "1.71c7ae147ae14p+7",
    "Site2Cost.NATURALGAS": "1.387ae147ae148p+6",
    "Site2Cost.OIL": "1.1bd70a3d70a3dp+7",
    "Site2Source.DISTRICTHEATING": "1.74bc6a7ef9db2p-1",
    "Site2Source.ELECTRICITY": "1.6000000000000p+1",
    "Site2Source.LPG": "1.199999999999ap+0",
    "Site2Source.NATURALGAS": "1.199999999999ap+0",
    "Site2Source.OIL": "1.199999999999ap+0",
    "Unit.ACH50_TO_ACH": "1.1eb851eb851ecp-4",
    "Unit.FRACTION_TO_PERCENT": "1.9000000000000p+6",
    "Unit.M3_PER_S_TO_CMH": "1.c200000000000p+11",
    "Unit.MM_TO_M": "1.0624dd2f1a9fcp-10",
    "Unit.M_TO_MM": "1.f400000000000p+9",
    "Unit.PERCENT_TO_FRACTION": "1.47ae147ae147bp-7",
    "Unit.W_TO_KW": "1.0624dd2f1a9fcp-10",
}
PROBE_INPUTS = {
    "ConvectionHeatTransfer.IN": 0.110,
    "ConvectionHeatTransfer.OUT": 0.043,
    "Site2CO2.DISTRICTHEATING": 100.0,
    "Site2CO2.ELECTRICITY": 100.0,
    "Site2CO2.LPG": 100.0,
    "Site2CO2.NATURALGAS": 100.0,
    "Site2CO2.OIL": 100.0,
    "Site2Cost.DISTRICTHEATING": 100.0,
    "Site2Cost.ELECTRICITY": 100.0,
    "Site2Cost.LPG": 100.0,
    "Site2Cost.NATURALGAS": 100.0,
    "Site2Cost.OIL": 100.0,
    "Site2Source.DISTRICTHEATING": 100.0,
    "Site2Source.ELECTRICITY": 100.0,
    "Site2Source.LPG": 100.0,
    "Site2Source.NATURALGAS": 100.0,
    "Site2Source.OIL": 100.0,
    "Unit.ACH50_TO_ACH": 2.0,
    "Unit.FRACTION_TO_PERCENT": 0.375,
    "Unit.M3_PER_S_TO_CMH": 0.5,
    "Unit.MM_TO_M": 1250.0,
    "Unit.M_TO_MM": 0.00125,
    "Unit.PERCENT_TO_FRACTION": 37.5,
    "Unit.W_TO_KW": 4200.0,
}
EXPECTED_PROBE_RESULTS = {
    symbol: (float.fromhex(value) * PROBE_INPUTS[symbol]).hex().removeprefix("0x")
    for symbol, value in EXPECTED_VALUES.items()
}

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
RESULT_CARRIER_ORDER = [
    "ELECTRICITY",
    "NATURALGAS",
    "LPG",
    "OIL",
    "DISTRICTHEATING",
]

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
BINARY64_PATTERN = re.compile(r"^-?[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+$")


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_epsimple_constants_numeric_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load epsimple constants oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Epsimple constants oracle support is not pinned.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file


def _class_case_id(class_name: str, suffix: str) -> str:
    return f"epsimple-constants-numeric.{CLASS_SLUGS[class_name]}.class.{suffix}"


def _member_case_id(symbol: str, suffix: str) -> str:
    class_name = symbol.split(".", 1)[0]
    return (
        f"epsimple-constants-numeric.{CLASS_SLUGS[class_name]}."
        f"{MEMBER_TOKENS[symbol]}.{suffix}"
    )


EXPECTED_CASE_IDS = tuple(
    sorted(
        [
            _class_case_id(class_name, suffix)
            for class_name in CLASS_SLUGS
            for suffix in ("construction", "member-topology", "type-topology")
        ]
        + [
            _member_case_id(symbol, suffix)
            for symbol in TARGET_SYMBOLS
            if "." in symbol
            for suffix in ("engineering-probe", "numeric-semantics", "value")
        ]
    )
)
EXPECTED_CASE_COUNT = 87


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(SUPPORT, name) for name in names}
    try:
        SUPPORT.SOURCE_PATH = SOURCE_PATH
        SUPPORT.EXPECTED_SOURCE_SHA256 = EXPECTED_SOURCE_SHA256
        SUPPORT.EXPECTED_SYMBOL_HASHES = EXPECTED_SYMBOL_HASHES
        SUPPORT.TARGET_SYMBOLS = TARGET_SYMBOLS
        inventory = SUPPORT.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(SUPPORT, name, value)

    if inventory["file"] != {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }:
        raise SystemExit("The epsimple constants.py inventory file receipt is not exact.")
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]
    if inventory["symbols"] != expected_symbols:
        raise SystemExit("The epsimple constants.py symbol receipts are not exact.")

    complete_inventory = SUPPORT.load_json_without_duplicates(path)
    if complete_inventory["content_sha256"] != inventory["content_sha256"]:
        raise SystemExit("The validated inventory content receipt drifted.")
    model_files = [
        item
        for item in complete_inventory["files"]
        if item["path"] == MODEL_SOURCE_PATH
    ]
    model_symbols = [
        item
        for item in complete_inventory["symbols"]
        if item["path"] == MODEL_SOURCE_PATH
        and item["symbol"]
        in {
            "GreenRetrofitResult.VALID_DIGITS",
            "GreenRetrofitResult.to_source_uses",
        }
    ]
    observation_dependency = {
        "file": model_files[0] if len(model_files) == 1 else None,
        "symbols": model_symbols,
    }
    if observation_dependency != EXPECTED_MODEL_OBSERVATION_DEPENDENCY:
        raise SystemExit(
            "The GreenRetrofitResult.to_source_uses observation receipt is not exact."
        )
    return {**inventory, "observation_dependency": observation_dependency}


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
    definitions: list[dict[str, Any]] = []
    for class_name in CLASS_SLUGS:
        executor = (
            "convection-class"
            if class_name == "ConvectionHeatTransfer"
            else "unit-class" if class_name == "Unit" else "site-factor-class"
        )
        for suffix in ("construction", "member-topology", "type-topology"):
            definitions.append(
                _case(_class_case_id(class_name, suffix), executor, class_name)
            )
    for symbol in TARGET_SYMBOLS:
        if "." not in symbol:
            continue
        class_name = symbol.split(".", 1)[0]
        executor = (
            "convection-constant"
            if class_name == "ConvectionHeatTransfer"
            else "unit-constant" if class_name == "Unit" else "site-factor-constant"
        )
        for suffix in ("engineering-probe", "numeric-semantics", "value"):
            definitions.append(_case(_member_case_id(symbol, suffix), executor, symbol))
    definitions.sort(key=lambda item: item["id"])
    counts = Counter(item["symbol"] for item in definitions)
    if (
        tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS
        or len(definitions) != EXPECTED_CASE_COUNT
        or counts != Counter({symbol: 3 for symbol in TARGET_SYMBOLS})
    ):
        raise RuntimeError("Epsimple constants cases are not exactly three per symbol.")
    return tuple(definitions)


def _number(value: int | float) -> dict[str, Any]:
    if type(value) is int:
        return {"decimal": str(value), "kind": "int"}
    if type(value) is float and math.isfinite(value):
        return {"binary64": value.hex().removeprefix("0x"), "kind": "float"}
    raise RuntimeError("Only finite exact int/float values may enter the oracle.")


def _construction_observation(enum_type: Any, value: Any, label: str) -> dict[str, Any]:
    try:
        member = enum_type(value)
    except Exception as error:  # The exact constructor failure is evidence.
        return {
            "error_category": "domain",
            "exception_type": type(error).__name__,
            "input": _number(value),
            "label": label,
            "outcome": "raised",
        }
    return {
        "input": _number(value),
        "label": label,
        "outcome": "returned",
        "result": {"name": member.name, "value": _number(member.value)},
    }


def _enum_type_facts(enum_type: Any) -> dict[str, Any]:
    return {
        "base_names": [base.__name__ for base in enum_type.__bases__],
        "class_name": enum_type.__name__,
        "is_enum_subclass": any(base.__name__ == "Enum" for base in enum_type.__mro__),
        "is_float_subclass": issubclass(enum_type, float),
        "module": enum_type.__module__,
        "signature": str(inspect.signature(enum_type)),
    }


def _alias_groups(enum_type: Any) -> list[list[str]]:
    by_member: dict[int, list[str]] = defaultdict(list)
    for name, member in enum_type.__members__.items():
        by_member[id(member)].append(name)
    return [names for names in by_member.values() if len(names) > 1]


def _model_source_for_constants(constants_source: Path) -> Path:
    model_source = constants_source.resolve().parent / "core" / "model.py"
    if (
        not model_source.is_file()
        or sha256_file(model_source) != EXPECTED_MODEL_SOURCE_SHA256
    ):
        raise SystemExit("The GreenRetrofitResult model.py source is not pinned.")
    return model_source


def _frame_rows(frame: Any) -> list[dict[str, Any]]:
    if list(frame.index) != RESULT_CARRIER_ORDER or list(frame.columns) != ["probe"]:
        raise RuntimeError("The to_source_uses probe dataframe shape drifted.")
    return [
        {
            "carrier": carrier,
            "values": [_number(value) for value in frame.loc[carrier, "probe"]],
        }
        for carrier in RESULT_CARRIER_ORDER
    ]


def _direct_to_source_uses_observation(
    model_source: Path, site_to_source: Any
) -> dict[str, Any]:
    if sha256_file(model_source) != EXPECTED_MODEL_SOURCE_SHA256:
        raise SystemExit("The executed GreenRetrofitResult model.py source is not pinned.")
    tree = ast.parse(model_source.read_text(encoding="utf-8"), filename=MODEL_SOURCE_PATH)
    classes = [
        node
        for node in tree.body
        if isinstance(node, ast.ClassDef) and node.name == "GreenRetrofitResult"
    ]
    if len(classes) != 1:
        raise RuntimeError("The pinned GreenRetrofitResult class AST is not unique.")
    source_class = classes[0]
    valid_digits = [
        node
        for node in source_class.body
        if isinstance(node, ast.Assign)
        and len(node.targets) == 1
        and isinstance(node.targets[0], ast.Name)
        and node.targets[0].id == "VALID_DIGITS"
    ]
    methods = [
        node
        for node in source_class.body
        if isinstance(node, ast.FunctionDef) and node.name == "to_source_uses"
    ]
    if (
        len(valid_digits) != 1
        or not isinstance(valid_digits[0].value, ast.Constant)
        or type(valid_digits[0].value.value) is not int
        or valid_digits[0].value.value != 2
        or len(methods) != 1
    ):
        raise RuntimeError("The pinned to_source_uses execution AST drifted.")

    synthetic_class = ast.ClassDef(
        name="GreenRetrofitResult",
        bases=[],
        keywords=[],
        body=[copy.deepcopy(valid_digits[0]), copy.deepcopy(methods[0])],
        decorator_list=[],
        type_params=[],
    )
    ast.copy_location(synthetic_class, source_class)
    executable = ast.fix_missing_locations(
        ast.Module(body=[synthetic_class], type_ignores=[])
    )
    pandas = importlib.import_module("pandas")
    namespace = {
        "__name__": "_pinned_epsimple_green_retrofit_result_probe",
        "deepcopy": copy.deepcopy,
        "pd": pandas,
        "Site2Source": site_to_source,
    }
    exec(compile(executable, MODEL_SOURCE_PATH, "exec"), namespace)
    result_type = namespace["GreenRetrofitResult"]

    input_frame = pandas.DataFrame(
        {"probe": [[1.0, 2.0] for _ in RESULT_CARRIER_ORDER]},
        index=RESULT_CARRIER_ORDER,
    )
    instance = result_type()
    instance.to_site_uses = lambda: input_frame
    output_frame = result_type.to_source_uses(instance)
    return {
        "input_rows": _frame_rows(input_frame),
        "method": "GreenRetrofitResult.to_source_uses",
        "mode": "pinned-upstream-ast-exact-method",
        "output_rows": _frame_rows(output_frame),
        "valid_digits": result_type.VALID_DIGITS,
    }


def _class_facts(
    identifier: str,
    enum_type: Any,
    class_name: str,
    model_source: Path,
) -> dict[str, Any]:
    if identifier.endswith(".construction"):
        members = enum_type.__members__
        first_name = EXPECTED_MEMBER_NAMES[class_name][0]
        last_name = EXPECTED_MEMBER_NAMES[class_name][-1]
        return {
            "observations": [
                _construction_observation(
                    enum_type, members[first_name].value, "first-declared-value"
                ),
                _construction_observation(
                    enum_type, members[last_name].value, "last-declared-value"
                ),
                _construction_observation(enum_type, -1, "unknown-value"),
            ]
        }
    if identifier.endswith(".member-topology"):
        members = enum_type.__members__
        facts: dict[str, Any] = {
            "alias_groups": _alias_groups(enum_type),
            "canonical_names": {name: member.name for name, member in members.items()},
            "declared_member_names": list(members),
            "declared_values": [_number(member.value) for member in members.values()],
            "iterated_member_names": [member.name for member in enum_type],
            "iterated_values": [_number(member.value) for member in enum_type],
            "member_count": len(members),
            "unique_member_count": len(list(enum_type)),
        }
        if class_name == "Site2Source":
            iterated = list(enum_type)
            unmatched_count = len(RESULT_CARRIER_ORDER) - len(iterated)
            facts["result_scaling"] = {
                "carrier_order": RESULT_CARRIER_ORDER,
                "direct_method_execution": _direct_to_source_uses_observation(
                    model_source, enum_type
                ),
                "factor_sources": [member.name for member in iterated]
                + (["UNMATCHED"] * unmatched_count),
                "factors": [_number(member.value) for member in iterated]
                + ([_number(1.0)] * unmatched_count),
            }
        return facts
    if identifier.endswith(".type-topology"):
        return _enum_type_facts(enum_type)
    raise RuntimeError(f"Unknown numeric constant class case: {identifier}")


def _constant_facts(identifier: str, constants: Any, symbol: str) -> dict[str, Any]:
    class_name, declared_name = symbol.split(".", 1)
    enum_type = getattr(constants, class_name)
    member = enum_type.__members__[declared_name]
    if identifier.endswith(".engineering-probe"):
        probe_input = PROBE_INPUTS[symbol]
        return {
            "input": _number(probe_input),
            "operation": "multiply",
            "result": _number(probe_input * member),
        }
    if identifier.endswith(".numeric-semantics"):
        return {
            "canonical_name": member.name,
            "declared_name": declared_name,
            "equals_value": member == member.value,
            "float_projection": _number(float(member)),
            "is_float_instance": isinstance(member, float),
            "is_same_as_canonical_member": member is enum_type[member.name],
            "value_type": type(member.value).__name__,
        }
    if identifier.endswith(".value"):
        return {
            "canonical_name": member.name,
            "declared_name": declared_name,
            "value": _number(member.value),
        }
    raise RuntimeError(f"Unknown numeric constant member case: {identifier}")


def _execute_class(
    identifier: str, constants: Any, symbol: str, model_source: Path
) -> dict[str, Any]:
    return _class_facts(identifier, getattr(constants, symbol), symbol, model_source)


def _execute_constant(
    identifier: str, constants: Any, symbol: str, model_source: Path
) -> dict[str, Any]:
    del model_source
    return _constant_facts(identifier, constants, symbol)


EXECUTORS = {
    "convection-class": _execute_class,
    "convection-constant": _execute_constant,
    "site-factor-class": _execute_class,
    "site-factor-constant": _execute_constant,
    "unit-class": _execute_class,
    "unit-constant": _execute_constant,
}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


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


def _find_pinned_source() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        candidate = Path(entry) / "epsimple" / "constants.py"
        if candidate.is_file() and sha256_file(candidate) == EXPECTED_SOURCE_SHA256:
            matches.append(candidate.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned epsimple/constants.py must be importable.")
    return unique[0]


@contextmanager
def _pinned_constants(source: Path) -> Iterator[Any]:
    source = source.resolve()
    if sha256_file(source) != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The selected epsimple constants.py source is not pinned.")
    saved = sys.modules.get("epsimple.constants")
    spec = importlib.util.spec_from_file_location("epsimple.constants", source)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot import pinned epsimple constants: {source}")
    module = importlib.util.module_from_spec(spec)
    sys.modules["epsimple.constants"] = module
    try:
        spec.loader.exec_module(module)
        if Path(module.__file__).resolve() != source:
            raise SystemExit("Imported epsimple.constants did not resolve to pinned source.")
        yield module
    finally:
        if saved is None:
            sys.modules.pop("epsimple.constants", None)
        else:
            sys.modules["epsimple.constants"] = saved


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source: Path | None = None,
) -> dict[str, Any]:
    imported_source = source.resolve() if source is not None else _find_pinned_source()
    if sha256_file(imported_source) != inventory["file"]["content_hash"]:
        raise SystemExit("The imported epsimple constants.py is not inventoried source.")
    model_source = _model_source_for_constants(imported_source)
    if (
        inventory.get("observation_dependency")
        != EXPECTED_MODEL_OBSERVATION_DEPENDENCY
    ):
        raise SystemExit("The model-method observation inventory receipt drifted.")
    definitions = case_definitions()
    with _pinned_constants(imported_source) as constants:
        if any(not hasattr(constants, symbol.split(".")[0]) for symbol in TARGET_SYMBOLS):
            raise SystemExit("The pinned epsimple numeric constants surface drifted.")
        cases: list[dict[str, Any]] = []
        for definition in definitions:
            case = dict(definition)
            case["python"] = {
                "facts": EXECUTORS[definition["executor"]](
                    definition["id"], constants, definition["symbol"], model_source
                ),
                "outcome": "returned",
            }
            cases.append(case)

    classifications = {
        symbol: (
            "exception"
            if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
            else "equivalent"
        )
        for symbol in TARGET_SYMBOLS
    }
    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": {
            "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
            "assertion_ids": EXPECTED_ASSERTION_IDS,
            "case_count": EXPECTED_CASE_COUNT,
            "case_ids": list(EXPECTED_CASE_IDS),
            "classifications": classifications,
            "float_encoding": "python-binary64-hex-without-0x-prefix",
            "runtime_names": "pinned-python-only-no-native-type-name-claims",
            "target_symbols": list(TARGET_SYMBOLS),
        },
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
            "observation_dependency": inventory["observation_dependency"],
            "path": SOURCE_PATH,
            "source_sha256": sha256_file(imported_source),
        },
    }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def _case_by_id(value: dict[str, Any], identifier: str) -> dict[str, Any]:
    matches = [item for item in value["cases"] if item["id"] == identifier]
    if len(matches) != 1:
        raise RuntimeError(f"Expected exactly one case {identifier!r}.")
    return matches[0]


def _validate_number(value: Any, location: str) -> None:
    _require_keys(value, {"binary64", "kind"}, location)
    if value["kind"] != "float" or not isinstance(value["binary64"], str):
        raise RuntimeError(f"{location} is not a binary64 float.")
    if BINARY64_PATTERN.fullmatch(value["binary64"]) is None:
        raise RuntimeError(f"{location} binary64 token drifted.")


def _expected_tagged_float(binary64: str) -> dict[str, str]:
    return {"binary64": binary64, "kind": "float"}


def _expected_direct_method_execution() -> dict[str, Any]:
    input_rows = [
        {
            "carrier": carrier,
            "values": [
                _expected_tagged_float("1.0000000000000p+0"),
                _expected_tagged_float("1.0000000000000p+1"),
            ],
        }
        for carrier in RESULT_CARRIER_ORDER
    ]
    output_tokens = {
        "ELECTRICITY": ["1.6000000000000p+1", "1.6000000000000p+2"],
        "NATURALGAS": ["1.199999999999ap+0", "1.199999999999ap+1"],
        "LPG": ["1.75c28f5c28f5cp-1", "1.75c28f5c28f5cp+0"],
        "OIL": ["1.0000000000000p+0", "1.0000000000000p+1"],
        "DISTRICTHEATING": ["1.0000000000000p+0", "1.0000000000000p+1"],
    }
    return {
        "input_rows": input_rows,
        "method": "GreenRetrofitResult.to_source_uses",
        "mode": "pinned-upstream-ast-exact-method",
        "output_rows": [
            {
                "carrier": carrier,
                "values": [
                    _expected_tagged_float(token)
                    for token in output_tokens[carrier]
                ],
            }
            for carrier in RESULT_CARRIER_ORDER
        ],
        "valid_digits": 2,
    }


def _expected_class_facts(class_name: str, suffix: str) -> dict[str, Any]:
    names = EXPECTED_MEMBER_NAMES[class_name]
    canonical = EXPECTED_CANONICAL_NAMES[class_name]
    if suffix == "construction":
        first_name = names[0]
        last_name = names[-1]
        return {
            "observations": [
                {
                    "input": _expected_tagged_float(
                        EXPECTED_VALUES[f"{class_name}.{first_name}"]
                    ),
                    "label": "first-declared-value",
                    "outcome": "returned",
                    "result": {
                        "name": canonical[first_name],
                        "value": _expected_tagged_float(
                            EXPECTED_VALUES[f"{class_name}.{first_name}"]
                        ),
                    },
                },
                {
                    "input": _expected_tagged_float(
                        EXPECTED_VALUES[f"{class_name}.{last_name}"]
                    ),
                    "label": "last-declared-value",
                    "outcome": "returned",
                    "result": {
                        "name": canonical[last_name],
                        "value": _expected_tagged_float(
                            EXPECTED_VALUES[f"{class_name}.{last_name}"]
                        ),
                    },
                },
                {
                    "error_category": "domain",
                    "exception_type": "ValueError",
                    "input": {"decimal": "-1", "kind": "int"},
                    "label": "unknown-value",
                    "outcome": "raised",
                },
            ]
        }
    if suffix == "member-topology":
        facts: dict[str, Any] = {
            "alias_groups": EXPECTED_ALIAS_GROUPS[class_name],
            "canonical_names": canonical,
            "declared_member_names": names,
            "declared_values": [
                _expected_tagged_float(EXPECTED_VALUES[f"{class_name}.{name}"])
                for name in names
            ],
            "iterated_member_names": EXPECTED_ITERATED_NAMES[class_name],
            "iterated_values": [
                _expected_tagged_float(EXPECTED_VALUES[f"{class_name}.{name}"])
                for name in EXPECTED_ITERATED_NAMES[class_name]
            ],
            "member_count": len(names),
            "unique_member_count": len(EXPECTED_ITERATED_NAMES[class_name]),
        }
        if class_name == "Site2Source":
            facts["result_scaling"] = {
                "carrier_order": RESULT_CARRIER_ORDER,
                "direct_method_execution": _expected_direct_method_execution(),
                "factor_sources": [
                    "ELECTRICITY",
                    "NATURALGAS",
                    "DISTRICTHEATING",
                    "UNMATCHED",
                    "UNMATCHED",
                ],
                "factors": [
                    _expected_tagged_float(EXPECTED_VALUES["Site2Source.ELECTRICITY"]),
                    _expected_tagged_float(EXPECTED_VALUES["Site2Source.NATURALGAS"]),
                    _expected_tagged_float(
                        EXPECTED_VALUES["Site2Source.DISTRICTHEATING"]
                    ),
                    _expected_tagged_float("1.0000000000000p+0"),
                    _expected_tagged_float("1.0000000000000p+0"),
                ],
            }
        return facts
    if suffix == "type-topology":
        return {
            "base_names": ["float", "Enum"],
            "class_name": class_name,
            "is_enum_subclass": True,
            "is_float_subclass": True,
            "module": "epsimple.constants",
            "signature": "(*values)",
        }
    raise RuntimeError(f"Unknown expected class case suffix: {suffix}")


def _expected_member_facts(symbol: str, suffix: str) -> dict[str, Any]:
    class_name, declared_name = symbol.split(".", 1)
    canonical_name = EXPECTED_CANONICAL_NAMES[class_name][declared_name]
    tagged_value = _expected_tagged_float(EXPECTED_VALUES[symbol])
    if suffix == "engineering-probe":
        return {
            "input": _number(PROBE_INPUTS[symbol]),
            "operation": "multiply",
            "result": _expected_tagged_float(EXPECTED_PROBE_RESULTS[symbol]),
        }
    if suffix == "numeric-semantics":
        return {
            "canonical_name": canonical_name,
            "declared_name": declared_name,
            "equals_value": True,
            "float_projection": tagged_value,
            "is_float_instance": True,
            "is_same_as_canonical_member": True,
            "value_type": "float",
        }
    if suffix == "value":
        return {
            "canonical_name": canonical_name,
            "declared_name": declared_name,
            "value": tagged_value,
        }
    raise RuntimeError(f"Unknown expected member case suffix: {suffix}")


def _validate_semantics(value: dict[str, Any]) -> None:
    for class_name in CLASS_SLUGS:
        for suffix in ("construction", "member-topology", "type-topology"):
            actual = _case_by_id(value, _class_case_id(class_name, suffix))[
                "python"
            ]["facts"]
            if actual != _expected_class_facts(class_name, suffix):
                raise RuntimeError(f"{class_name} {suffix} facts drifted.")

    for symbol in EXPECTED_VALUES:
        for suffix in ("engineering-probe", "numeric-semantics", "value"):
            actual = _case_by_id(value, _member_case_id(symbol, suffix))["python"][
                "facts"
            ]
            if actual != _expected_member_facts(symbol, suffix):
                raise RuntimeError(f"{symbol} {suffix} facts drifted.")


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if type(value) is float:
        raise RuntimeError(f"Raw float entered {location}.")
    if isinstance(value, Path):
        raise RuntimeError(f"Raw path entered {location}.")
    if isinstance(value, str):
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"A raw address entered {location}.")
        if ABSOLUTE_PATH_PATTERN.search(value):
            raise RuntimeError(f"An absolute path entered {location}.")
        if GUID_PATTERN.search(value):
            raise RuntimeError(f"A GUID-like token entered {location}.")
        if TIMESTAMP_PATTERN.search(value):
            raise RuntimeError(f"A timestamp entered {location}.")
        return
    if value is None or type(value) in (bool, int):
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"A non-string key entered {location}.")
            _validate_safe_tree(key, f"{location}.<key>")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Raw object {type(value).__name__} entered {location}.")


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
        raise RuntimeError("Epsimple constants numeric schema drifted.")
    definitions = case_definitions()
    if len(value["cases"]) != EXPECTED_CASE_COUNT or [
        item["id"] for item in value["cases"]
    ] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Epsimple constants numeric case order/count drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Epsimple constants numeric cases hash drifted.")
    by_id = {item["id"]: item for item in definitions}
    for case in value["cases"]:
        definition = by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case.get('id')!r}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Case contract drifted: {case['id']}")
        if "expected_dotnet" in case:
            _require_keys(case["expected_dotnet"], {"adaptation", "outcome"}, "native")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned" or not case["python"]["facts"]:
            raise RuntimeError(f"Python case outcome drifted: {case['id']}")

    classifications = {
        symbol: (
            "exception"
            if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
            else "equivalent"
        )
        for symbol in TARGET_SYMBOLS
    }
    expected_contract = {
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "assertion_ids": EXPECTED_ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": classifications,
        "float_encoding": "python-binary64-hex-without-0x-prefix",
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
        "target_symbols": list(TARGET_SYMBOLS),
    }
    if value["consumer_contract"] != expected_contract:
        raise RuntimeError("Epsimple constants numeric consumer contract drifted.")
    if value["runtime"] != {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("Epsimple constants numeric runtime pin drifted.")
    if value["upstream"] != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "observation_dependency": EXPECTED_MODEL_OBSERVATION_DEPENDENCY,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("Epsimple constants numeric upstream receipt drifted.")
    if value["symbols"] != [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]:
        raise RuntimeError("Epsimple constants numeric symbol receipts drifted.")
    _validate_semantics(value)
    _validate_safe_tree(value)
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this constants oracle.")
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
    print(f"Wrote epsimple constants numeric oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

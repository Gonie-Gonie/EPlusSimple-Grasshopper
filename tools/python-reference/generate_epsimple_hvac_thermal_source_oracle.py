"""Generate the pinned EPlusSimple HVAC thermal-source behavior oracle.

This bounded corpus executes exactly 47 declarations for AbsorptionChiller,
Boiler, Chiller, DistrictHeating, GeothermalHeatPump, and HeatPump in the
pinned ``src/epsimple/core/hvac.py`` source.  The other 155 declarations in
that source are retained as adjacent non-target receipts.  The upstream module
is imported without either EPlusSimple package initializer and is executed
again from a byte-identical relocated source tree.
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
import struct
import sys
import tempfile
from types import ModuleType, SimpleNamespace
from typing import Any, Callable


SCHEMA = "dragons.python-reference.epsimple-hvac-thermal-source.v1"
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
        "_dragons_epsimple_hvac_thermal_source_base", BASE_PATH
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
    (135, 'AbsorptionChiller', 'class', 'sha256:c44e12f95a00cb5962ca8ad7d437ef2d161ff1771e6a511f01099e697f58c288', 'sha256:197f6bd7f4f53ed2b4f36fc51be027baf327086ce45d10f2d2693d87ca3085cd', 'sha256:a8cb41ce6b4a3368c18fc1ffc46dc3caa684c9c210d2e0c0b194a276332c88b0'),
    (136, 'AbsorptionChiller.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (139, 'AbsorptionChiller.__init__', 'function', 'sha256:4aae19c639fbe20444a95f99af9aaa0bceb7be04710b434fc99f8e80a2f53fdb', 'sha256:1854faa7f9da6126f0d05ed628ff31167b177edcc2939c5989d6ccdf01aa41d5', 'sha256:7a7e3afb0f50f0ac922afbb6e7d2b751102d9766156d4217e9b508a9873d6027'),
    (142, 'AbsorptionChiller.boiler_efficiency', 'function', 'sha256:be052579fc5a280692bed221f5c9dc6eb30dec184a455663dee100148dca7c15', 'sha256:15b5169bd8d540ac00c6d9a9e50155b6945a50a7e029811e5e2eacac2ee4844e', 'sha256:2a12cf8c3f4c699e2cc91ecdc17fa99878e58a87dd4ae3aeb1137f5915460565'),
    (143, 'AbsorptionChiller.capacity', 'function', 'sha256:d699d5f1b04af7405ec421c7c72d1c1425f40bd007065cbec301d1ea9c5bffcb', 'sha256:bbbc01de62864a8be9a07c61355493d534500ac6ebb3732f687667ec2b4dffbd', 'sha256:f4aa61cb14fb78877b25e1727abe6b859eb528573035851ef4823bf96386e5a4'),
    (144, 'AbsorptionChiller.cop', 'function', 'sha256:253d21d2755374e2581dc3d6431e81f5877448480981a16667ffde993d480089', 'sha256:1e2c78159fd7fabe7c33c1d1696213b0e724b3f5ad9ed9ce52dac6ea89e5342b', 'sha256:a7f4e9f7638f69f248c7ba9d965a0c34f33b35868bc64d8be900345af2c33f7a'),
    (145, 'AbsorptionChiller.from_json', 'function', 'sha256:f305d7565898b256d9ad634f8e980ca458c5df316e291ed04e74b1193c74dec1', 'sha256:2dce65a096ae0ba4a62de3fb096ba3aa5390c7d9b4bae150c00d708c20a1d710', 'sha256:c0ff729d75b227ead99231e04d5e6569498f46da386f0570046f9de7f72e2d51'),
    (146, 'AbsorptionChiller.to_dragon', 'function', 'sha256:7a12c01556ae0ebb7acf661b54ff67ba16eaf12ea14800bda8686ce8dd79f08c', 'sha256:bed2fa93a5a6d0c700a44cadecfe1ce52111fb5748d839c10754e93e9c8ef063', 'sha256:4413b1ca617b268c452b04c647b16ea6b595b4868d1b7b46c835834097767d7d'),
    (157, 'Boiler', 'class', 'sha256:8d52ff9e56a001d640fab8decad6c2c1b288c561c5243756d8fcbe09ea018528', 'sha256:6c587d2756d9f3e1fc93de59ec1482ea151ff3a496b9f558f1278b8ad5a2a615', 'sha256:a8cb41ce6b4a3368c18fc1ffc46dc3caa684c9c210d2e0c0b194a276332c88b0'),
    (158, 'Boiler.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (161, 'Boiler.__init__', 'function', 'sha256:f45db90e3bd9e0d7d7bf09b1625885e65948ddbecf23a92e9c5e13a794403469', 'sha256:2c0c0b66128e930a0a67c9ecfd08ad47ff6d145ee4d6f2de96c492159dc92437', 'sha256:24ab511ac7b3bb91e2b785efaf3f6ac1c4a9febfc98c6dd91e2b9bf699ab7f8c'),
    (164, 'Boiler.capacity', 'function', 'sha256:d699d5f1b04af7405ec421c7c72d1c1425f40bd007065cbec301d1ea9c5bffcb', 'sha256:bbbc01de62864a8be9a07c61355493d534500ac6ebb3732f687667ec2b4dffbd', 'sha256:f4aa61cb14fb78877b25e1727abe6b859eb528573035851ef4823bf96386e5a4'),
    (165, 'Boiler.efficiency', 'function', 'sha256:80144f2f58577c9b96d6d6d012e949a459d23f8cfb4a3f77e5656154e80b6947', 'sha256:7a05da5625820c6c82456f3e46373c9a45937c8d39440d6684a43098197dc63b', 'sha256:b449c78b5f6789e34ec6cf108ed58fc253c9778ca9b74142b39874ffcc6c2efc'),
    (166, 'Boiler.from_json', 'function', 'sha256:bd3f1e5a20c905d27a7da940887638fad726b510f707826723015cbaeb1a0a63', 'sha256:7032eecd93acd5362cfc6dad1834d16786483a7420cd560ce7845ac3f6046e85', 'sha256:6f1c9c8c00e8861435e8c6ddc5ab6c1a39f26371affd567f94f1e82a82ec95a8'),
    (167, 'Boiler.fuel', 'function', 'sha256:64d0443e2225b273f9021f371d0dd88a0051e57f9eb3c91673faa8d96ca23ae1', 'sha256:c933e21c7ee9bd9b6a580d730d6d513f2c3a10c57f80fceec7a7b528566d29e8', 'sha256:336057118eaeb957134f4c98af1f688b1dfe2f2512ba3856db9fc064d68e5a8b'),
    (168, 'Boiler.hotwater_supply', 'function', 'sha256:f9effaf3b156faf96d9e3d71ab1ac3222b4933ad4cd29a51e9df1c28fc071124', 'sha256:f4be76fa1996185fb5c5575d4cdfc5267c7f2b15a7370d820be79667920f5751', 'sha256:da1b2b65e62f38fd800a7f741317027fed63081c40cb61e615b137bd1d247246'),
    (169, 'Boiler.to_dragon', 'function', 'sha256:86b77a933c317b02f06d37995f37906562c3a9d9f59fc36b3cda3f7369b333c7', 'sha256:449509b78f4b094a8be5a83cea146f18b10155c63839da70733be347c89df9fa', 'sha256:f84b440673abc9bcc3e44890d2dd74b6695bf07abf399fc84b82cb925c0c01a2'),
    (170, 'Chiller', 'class', 'sha256:8baa00de497b53a61086ec3f181c1b3550b7a14763dcbd79b2d7334b3c837229', 'sha256:64ca2c3d6bdf68fa01f483507de1348169d646b41ede1437e400a0a7df04ff1e', 'sha256:a8cb41ce6b4a3368c18fc1ffc46dc3caa684c9c210d2e0c0b194a276332c88b0'),
    (171, 'Chiller.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (174, 'Chiller.__init__', 'function', 'sha256:9c5215c4ec2ee276bb72b874e53ad5da5867fb1b2c0f0426f0235b67cd13a553', 'sha256:fe0fe20ff70e1974731f85aa4c72e6a1d5c5e2a70f872c9c4e77c4c255248e42', 'sha256:1a7c2c44e244006a8a6b071864be040e666140f1c9b4eb6c43c2bd61ad03010e'),
    (177, 'Chiller.capacity', 'function', 'sha256:d699d5f1b04af7405ec421c7c72d1c1425f40bd007065cbec301d1ea9c5bffcb', 'sha256:bbbc01de62864a8be9a07c61355493d534500ac6ebb3732f687667ec2b4dffbd', 'sha256:f4aa61cb14fb78877b25e1727abe6b859eb528573035851ef4823bf96386e5a4'),
    (178, 'Chiller.compressor_type', 'function', 'sha256:000c99e32fb9a139e083042f27fc916b8e64a78b5ad843808bbc5fadfa0a69e1', 'sha256:af6a3af6e37fb61884754e674514907dd775db68101599f1593f23b4fce1d531', 'sha256:1c1716d452fe42635549e71f3bd2835a33f3c18f8f2c2d92ea68ebe544858511'),
    (179, 'Chiller.coolingtower_capacity', 'function', 'sha256:e56b52fb177c1ab6c0d13f9d228871e659e4af8720807ba77b6ac3e65108412b', 'sha256:eecf4c401568cd53a1360e4edc2d766845f6ae4214701abd67a679cbd9ea2527', 'sha256:4843111dd3b2d964636942490d02cd7a831f85172b3dbe17ec4d1092838ff118'),
    (180, 'Chiller.coolingtower_control', 'function', 'sha256:473c615ac03fcf57bbb3aeb96de55ea45c126624ddf2ba53f5ea71a587e6c369', 'sha256:c28cd63afd90b0d25469e5dda22e11f67573cef6ad7ba6e0213a60cf902dec36', 'sha256:5d2aa15aafcefa55111a4ee8cf4bcef42751f1afd87220a94f98ab8465d9391a'),
    (181, 'Chiller.coolingtower_type', 'function', 'sha256:75acdde9fdf4e8eeaedb7245dad9151cde6f69e3fd3efe9dfa830a5bfb09f1aa', 'sha256:0c4805efa22379b963cfb4e1d7790cca7e1fec914c89518ff8b8c6571feacce1', 'sha256:d142d5add2ce7a4560c7361f184b52b7b70eec2b811bcbbfd96ecb7f7c6e83cf'),
    (182, 'Chiller.cop', 'function', 'sha256:253d21d2755374e2581dc3d6431e81f5877448480981a16667ffde993d480089', 'sha256:1e2c78159fd7fabe7c33c1d1696213b0e724b3f5ad9ed9ce52dac6ea89e5342b', 'sha256:a7f4e9f7638f69f248c7ba9d965a0c34f33b35868bc64d8be900345af2c33f7a'),
    (183, 'Chiller.from_json', 'function', 'sha256:ca5a644592b7e7573475671f42d9a3f10cdd16db3c05f96f159035fba9324643', 'sha256:cb50bc61899e7d12cd1c8464802ac31f20979eb2ebed6e1a247a3971e2769496', 'sha256:0864cb6b1ae1d38253eb911630c624cb95ca5e420825b5445335d2b7ad093bdd'),
    (184, 'Chiller.to_dragon', 'function', 'sha256:b3b58ae8b0cd495b6f402aeab79f8b73009b1bf71a772fb47e015eeec26551b4', 'sha256:9041af4cc269cce2e8cf0737cae7b6c2bae137ef1b28f2af094c92508c117fbf', 'sha256:c4e3bacedfd77aba3c205683f4ddb2bb37e9c63a9f57f7edc98d2237591dd56d'),
    (199, 'DistrictHeating', 'class', 'sha256:a1c6d574e77ca0ae9a864a7e216c3a6c268dc31c5144659793a67d2a5d054d2e', 'sha256:aa8dcf21c53ba2fbdde4dc4b5c5b51cb6a12b2210dcba1bf63eda222052233a8', 'sha256:a8cb41ce6b4a3368c18fc1ffc46dc3caa684c9c210d2e0c0b194a276332c88b0'),
    (200, 'DistrictHeating.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (203, 'DistrictHeating.__init__', 'function', 'sha256:f477c20bcf162bb58f12806e36a82d8c0d399ac3ec43eaa30d63dccadda6a000', 'sha256:e5c5260e76ce25622caba4836eaa4d2381ed86479201d1445aaec22d59215a41', 'sha256:334bfb0cb24cf551efa851b63cd5991a3b44e119d04be80ec1f57aaad581cfcb'),
    (206, 'DistrictHeating.from_json', 'function', 'sha256:c53a5bbb27eb5616cdc9c33b62b17a67476d859db3bd444539ad1a9d8cff589a', 'sha256:0f22469400fca4dd878455bbb0a61a793e3c100bc3e61cb06ecf913a723ef636', 'sha256:fb7fbd38b392655d253b2f3ff34370125300f23b7dd906b42207317fa68c1b04'),
    (207, 'DistrictHeating.hotwater_supply', 'function', 'sha256:f9effaf3b156faf96d9e3d71ab1ac3222b4933ad4cd29a51e9df1c28fc071124', 'sha256:f4be76fa1996185fb5c5575d4cdfc5267c7f2b15a7370d820be79667920f5751', 'sha256:da1b2b65e62f38fd800a7f741317027fed63081c40cb61e615b137bd1d247246'),
    (208, 'DistrictHeating.to_dragon', 'function', 'sha256:bf1c4c8b065467f0750b66841455facb472187eaa7c12c916f70812e9f2c8340', 'sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b', 'sha256:acb2774b37e34ee62fb6bd6ecde9b5d50790ce632923dc3d7eb5ee2507a22f32'),
    (248, 'GeothermalHeatPump', 'class', 'sha256:a87f33ee237c0d521c0f28cddf441d18fe3f656aa2823b5bf6316e73e45cfb9d', 'sha256:b7c9586223e94cb8bbe55c73612dd32541d93ace8e3f638ef0ab444d9d968351', 'sha256:eea5f60464fbf25d0f86d503b9cb73a1e5c0ebef95201afb63878cb533c87f8d'),
    (251, 'GeothermalHeatPump.from_json', 'function', 'sha256:81ac3508503c09174a23f3c4304a30d27941b877a5f442beeff3efb7348af57f', 'sha256:0296f4713b99c0bf9da897637bb5d778ffb7facab6fa7715656cc0af4ed43971', 'sha256:a02e560c380ea9305ad75abb3fec033cd3ed8a82f380238c96da7c7e6da34d6b'),
    (252, 'GeothermalHeatPump.to_dragon', 'function', 'sha256:069a6710418875fdbb56df82bf0d75c5b6aa4466a8cfe74be2ec4e8f977f75d2', 'sha256:a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b', 'sha256:8b29e3c87e3e90e783734bdefb5778458e688e15ee585a02c091cb32c77cdadb'),
    (253, 'HeatPump', 'class', 'sha256:3872db3154378456d1ab86ab5dd8ee1e8473ece56ce3c5e3c5d21c4aab3db56d', 'sha256:ea4b405d80dd6ccb7d596496161420b8a23c1f5f743acbfd0bc1ca614bc37201', 'sha256:a8cb41ce6b4a3368c18fc1ffc46dc3caa684c9c210d2e0c0b194a276332c88b0'),
    (254, 'HeatPump.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (257, 'HeatPump.__init__', 'function', 'sha256:7e88c6cd0a0a1940ba7932b4d2991e2e9be925fce87ef78baebc0fae176a20b9', 'sha256:39d9cb1071124bc34aeb349be1bd750b3e9f80a011814259f9c325bee793a74c', 'sha256:a9b347f0f70f73580556392e3053a39bf6e6eb90b534615c91b2b39b0a133850'),
    (260, 'HeatPump.cooling_capacity', 'function', 'sha256:2c36599247e8d0009bfd6d483f79501927cda80d905dc35c34b7951847beb6ba', 'sha256:3e5ee18c6f80c0b5a8ceb340bcdce0031fc14295a09425e1517658dc7f9926f0', 'sha256:51970bfe0c47e57e70f6c898104e851ee40f1d0a66e8efe5dde7db3589bd38bb'),
    (261, 'HeatPump.cooling_cop', 'function', 'sha256:59bd79833f41971c5935ede33822d3986693f0eb608ee07431110d52dc8a6577', 'sha256:71e9718dab4a568443dd8a275198c8d80091e6d38eaf74eb108259b334eff938', 'sha256:0d6b9f259f06a1709324605664c4de028d218c0c2c6b66a5c6cd4b339df63e94'),
    (262, 'HeatPump.from_json', 'function', 'sha256:20b220f016be64ce4dc06b873fe97718f8a0409e7a0344691d6bce7f2dd5b829', 'sha256:0def9f1d1f9b92a17f366a3c8b7ff0a8dab2b187cd0bd8cd1df8061cef6c0149', 'sha256:a2fc3dfa6b59eb28310266d88b0f9035fe5824b906a0e9fef39eb0fad28102f4'),
    (263, 'HeatPump.fuel', 'function', 'sha256:3742042244a97d39c02c78bc2f4951361dfc5122def303875aaeb7b97e02cc41', 'sha256:c933e21c7ee9bd9b6a580d730d6d513f2c3a10c57f80fceec7a7b528566d29e8', 'sha256:0da73324f74a3e7c4a914c8526ba86925e97a3bc79f67568933b175b4b4dc2c7'),
    (264, 'HeatPump.heating_capacity', 'function', 'sha256:b48949da79c3f6581d75a61d294f60aa6d229657bf0c0119d111ab5730b65e3a', 'sha256:a5915fda8d226f920f4daf2cc114b3f3685aaa47b85311e02ebbc1194b5af930', 'sha256:0dbc87eebfcbdee8403f0f7aafc3b5e752eb42bcd53ad09be636deeb687003a2'),
    (265, 'HeatPump.heating_cop', 'function', 'sha256:55ddf021efaf696de09ff2ecc46d858a0f7906dd751bdc080eea738cf0ce8618', 'sha256:e6c9981e54db16db923dc987e0c91d50ea0b5e267ddccfc3925035eae878efb7', 'sha256:5bed166aee99318c7a61e3447f1a37176c33f57a4e785f0c27ef50c829aab70d'),
    (266, 'HeatPump.to_dragon', 'function', 'sha256:0feeee0bbb444a2d16c5efe0d1e1d41d928ed66197c713cb535ed8eef466b130', 'sha256:97dfeb55a1697d1771209527da11e4b59937f80eb94165a6984c13ac7db6c80c', 'sha256:8b29e3c87e3e90e783734bdefb5778458e688e15ee585a02c091cb32c77cdadb'),
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
if len(TARGET_INDICES) != 47 or len(ADJACENT_INDICES) != 155:
    raise RuntimeError("HVAC thermal-source source partition count drifted.")

EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:0374c74cedba9ecd7ce3e744f1b33cf490531c03cdd93f96bf3510c7f2d2caf1"
)
EXPECTED_ADJACENT_RECEIPTS_SHA256 = (
    "sha256:ef4f76630b955cdfdb33b822b2fa3d59ef89c4d2b02d5435567e3f1684cfb15f"
)
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:9f07d8a23754df14f9fff1e7f2cda0b334e630ad19363173859d5c14bfdc7031"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:bda0a2f6607b8ad2d72183e64c989e76ddb91b65890398aa7191cd2c636c6f03"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:aa0af5125100e524c774ba92d7993d00fadff21347e49303c32850e0868e11a8"
)
EXPECTED_FACT_SHA256 = {
    "epsimple-hvac-thermal-source.absorption-chiller-state-validation-json-dragon": "sha256:b14eee5f3b17ee6554f380af12eef43e8b5af07bcfc0aaa55e506cb2d26142eb",
    "epsimple-hvac-thermal-source.boiler-state-validation-json-dragon": "sha256:f82cb04c34c84047f95c9352634a6b01ba922c70a0b7b674e4a68d30d2985e54",
    "epsimple-hvac-thermal-source.chiller-state-tower-branches-json-dragon": "sha256:108cee171ab3816a0817b6bd78412c74056e953eeb924e1a6909c8828e471de1",
    "epsimple-hvac-thermal-source.district-heating-state-validation-json-dragon": "sha256:2862c963664ca87858dbb43d3f98e432cd963edb196528912163488b6a940c50",
    "epsimple-hvac-thermal-source.geothermal-heatpump-json-dragon": "sha256:abd7c2a03dd1f1c9be3dd4d3009546fd2fddab3b9e0d5e7f9a63222b966d7b81",
    "epsimple-hvac-thermal-source.heatpump-state-validation-json-dragon": "sha256:ab6a248ed48834b7d4e84467aafa68962f306940fd704f66b52e3f59cfe9069e",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-hvac-thermal-source.absorption-chiller-state-validation-json-dragon": "sha256:07e2bba13da938db6c7bbd64d047bda378f2e40ca541615ee898fbb025da6919",
    "epsimple-hvac-thermal-source.boiler-state-validation-json-dragon": "sha256:eaaaacf0bd3ef08a080aca2bc2ab8f61fffe6a07dccaaaa664fc5bf798e7ddc2",
    "epsimple-hvac-thermal-source.chiller-state-tower-branches-json-dragon": "sha256:2cb0247acde415dd8cc99296635057972415979af3754edb55930c2ea89055e3",
    "epsimple-hvac-thermal-source.district-heating-state-validation-json-dragon": "sha256:4d2e2f6b199cdc3bc0eb2a569243fc4bd85b10c8b84be9f36e07d7eef3d94695",
    "epsimple-hvac-thermal-source.geothermal-heatpump-json-dragon": "sha256:86697920fc6275852d414b00544844417927fb1e57f95d5a93718f7d262dffaf",
    "epsimple-hvac-thermal-source.heatpump-state-validation-json-dragon": "sha256:8ac4a3fe84318f9f640b9f2387f5aa8185f97c35a423ba588c27462711050599",
}
EXPECTED_CASES_SHA256 = (
    "sha256:1648981844e29967326b4caeb0b466238e12c07e43fb25469d7325b73ac3feb2"
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
        "reviewed-native-discriminated-source-aggregate-and-conversion-route-"
        + TARGET_HASHES[symbol][7:15]
    )
    for symbol in EXCEPTION_SYMBOLS
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"epsimple-hvac-thermal-source-{item['inventory_index']}-"
        f"{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}


def _class_name(symbol: str) -> str:
    return symbol.split(".", 1)[0]


_PROPERTY_ROUTES = {
    "AbsorptionChiller.ID": "Id",
    "AbsorptionChiller.boiler_efficiency": "BoilerEfficiency",
    "AbsorptionChiller.capacity": "CoolingCapacity",
    "AbsorptionChiller.cop": "CoolingCop",
    "Boiler.ID": "Id",
    "Boiler.capacity": "HeatingCapacity",
    "Boiler.efficiency": "Efficiency",
    "Boiler.fuel": "FuelType",
    "Boiler.hotwater_supply": "HotWaterSupply",
    "Chiller.ID": "Id",
    "Chiller.capacity": "CoolingCapacity",
    "Chiller.compressor_type": "CompressorType",
    "Chiller.coolingtower_capacity": "CoolingTowerCapacity",
    "Chiller.coolingtower_control": "CoolingTowerControl",
    "Chiller.coolingtower_type": "CoolingTowerType",
    "Chiller.cop": "CoolingCop",
    "DistrictHeating.ID": "Id",
    "DistrictHeating.hotwater_supply": "HotWaterSupply",
    "HeatPump.ID": "Id",
    "HeatPump.cooling_capacity": "CoolingCapacity",
    "HeatPump.cooling_cop": "CoolingCop",
    "HeatPump.fuel": "FuelType",
    "HeatPump.heating_capacity": "HeatingCapacity",
    "HeatPump.heating_cop": "HeatingCop",
}


def _native_route(symbol: str) -> str:
    member = symbol.rsplit(".", 1)[1] if "." in symbol else None
    if symbol in _PROPERTY_ROUTES:
        return (
            "Dragons.SimpleDragon.SourceSystem."
            + _PROPERTY_ROUTES[symbol]
        )
    if member == "from_json":
        return (
            "Dragons.SimpleDragon.GrmReader.Read(string, "
            "SimpleDragonDatabase?) source-system dispatch"
        )
    if member == "to_dragon":
        return (
            "Dragons.SimpleDragon.GreenRetrofitConverter.Convert("
            "GreenRetrofitModel, GreenRetrofitConversionOptions?)"
        )
    return (
        "Dragons.SimpleDragon.SourceSystem constructor with "
        f"SourceSystemType.{_class_name(symbol)} and public properties"
    )


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}
NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 6_885,
        "path": "src/SimpleDragon/Dragons.SimpleDragon.Core/Hvac/SourceSystem.cs",
        "sha256": "sha256:db5fafe1034aca7b16ef222ecad981b790952474e5311b798c9eb6a677c82af4",
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

PREFIX = "epsimple-hvac-thermal-source."
CASE_SPECS = (
    ("A01", "absorption-chiller-state-validation-json-dragon", "absorption", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("AbsorptionChiller"))),
    ("B01", "boiler-state-validation-json-dragon", "boiler", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("Boiler"))),
    ("C01", "chiller-state-tower-branches-json-dragon", "chiller", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("Chiller"))),
    ("D01", "district-heating-state-validation-json-dragon", "district", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("DistrictHeating"))),
    ("G01", "geothermal-heatpump-json-dragon", "geothermal", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("GeothermalHeatPump"))),
    ("H01", "heatpump-state-validation-json-dragon", "heatpump", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("HeatPump"))),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 6


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
        raise RuntimeError("HVAC thermal-source case order drifted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC thermal-source targets are not an exact case partition.")
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
                "HVAC thermal-source inventory receipt drifted at index "
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
        raise SystemExit("Pinned HVAC thermal-source target receipts drifted.")
    if EXPECTED_ADJACENT_RECEIPTS_SHA256 and adjacent_hash != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise SystemExit("Pinned HVAC thermal-source adjacent receipts drifted.")
    if sorted((*TARGET_INDICES, *ADJACENT_INDICES)) != list(SOURCE_INDICES):
        raise RuntimeError("The HVAC thermal-source source partition is incomplete.")
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


def _source_snapshot(instance: Any, fields: tuple[str, ...]) -> dict[str, Any]:
    return {
        "class": type(instance).__name__,
        "class_module": type(instance).__module__,
        "values": {field: _typed(getattr(instance, field)) for field in fields},
    }


def _absorption_facts(module: ModuleType) -> dict[str, Any]:
    fields = ("ID", "name", "cop", "capacity", "boiler_efficiency")
    default = module.AbsorptionChiller(
        "Abs Default", boiler_fuel="natural_gas", ID="SRC-ABS-DEFAULT"
    )
    explicit = module.AbsorptionChiller(
        "Abs Explicit",
        1.2,
        12_000.0,
        "lpg",
        0.8,
        ID="SRC-ABS-EXPLICIT",
    )
    from_json = module.AbsorptionChiller.from_json(
        SimpleNamespace(
            id="SRC-ABS-JSON",
            name="Abs JSON",
            fuel_type="oil",
            cop_cooling=1.1,
            capacity_cooling=9_500.0,
            boiler_efficiency=0.75,
        )
    )
    mutable = module.AbsorptionChiller(
        "Abs Mutable", boiler_fuel="natural_gas", ID="SRC-ABS-MUTABLE"
    )
    mutable.cop = 1.05
    mutable.capacity = 8_000
    mutable.boiler_efficiency = 1.0
    return {
        "base_classes": [base.__name__ for base in module.AbsorptionChiller.__bases__],
        "default": _source_snapshot(default, fields),
        "explicit": _source_snapshot(explicit, fields),
        "from_json": _source_snapshot(from_json, fields),
        "mutated": _source_snapshot(mutable, fields),
        "dragon": _typed(explicit.to_dragon()),
        "dragon_repeat_fresh": explicit.to_dragon() is not explicit.to_dragon(),
        "errors": {
            "boiler_efficiency_above_one": _exception(
                lambda: module.AbsorptionChiller(
                    "bad", boiler_fuel="natural_gas", boiler_efficiency=1.01
                )
            ),
            "capacity_zero": _exception(
                lambda: module.AbsorptionChiller(
                    "bad", capacity=0, boiler_fuel="natural_gas"
                )
            ),
            "cop_zero": _exception(
                lambda: module.AbsorptionChiller(
                    "bad", cop=0, boiler_fuel="natural_gas"
                )
            ),
            "fuel_none": _exception(lambda: module.AbsorptionChiller("bad")),
        },
    }


def _boiler_facts(module: ModuleType) -> dict[str, Any]:
    fields = ("ID", "name", "fuel", "hotwater_supply", "efficiency", "capacity")
    default = module.Boiler(
        "Boiler Default", "natural_gas", False, ID="SRC-BOILER-DEFAULT"
    )
    explicit = module.Boiler(
        "Boiler Explicit",
        module.Fuel.LPG,
        True,
        0.92,
        15_000.0,
        ID="SRC-BOILER-EXPLICIT",
    )
    from_json = module.Boiler.from_json(
        SimpleNamespace(
            id="SRC-BOILER-JSON",
            name="Boiler JSON",
            fuel_type="oil",
            hotwater_supply=True,
            efficiency=0.88,
            capacity_heating=17_500.0,
        )
    )
    mutable = module.Boiler(
        "Boiler Mutable", "natural_gas", False, ID="SRC-BOILER-MUTABLE"
    )
    mutable.fuel = "district_heating"
    mutable.hotwater_supply = True
    mutable.efficiency = 1.0
    mutable.capacity = 20_000
    return {
        "base_classes": [base.__name__ for base in module.Boiler.__bases__],
        "default": _source_snapshot(default, fields),
        "explicit": _source_snapshot(explicit, fields),
        "from_json": _source_snapshot(from_json, fields),
        "mutated": _source_snapshot(mutable, fields),
        "dragon": _typed(explicit.to_dragon()),
        "dragon_repeat_fresh": explicit.to_dragon() is not explicit.to_dragon(),
        "errors": {
            "capacity_zero": _exception(
                lambda: module.Boiler("bad", "natural_gas", False, capacity=0)
            ),
            "efficiency_above_one": _exception(
                lambda: module.Boiler("bad", "natural_gas", False, 1.01)
            ),
            "efficiency_zero": _exception(
                lambda: module.Boiler("bad", "natural_gas", False, 0)
            ),
            "fuel_invalid": _exception(
                lambda: module.Boiler("bad", "coal", False)
            ),
            "hotwater_not_bool": _exception(
                lambda: module.Boiler("bad", "natural_gas", 1)
            ),
        },
    }


def _chiller(
    module: ModuleType,
    tower_type: str,
    tower_control: str,
    identifier: str,
) -> Any:
    return module.Chiller(
        "Chiller " + identifier,
        "screw",
        tower_type,
        tower_control,
        4.25,
        24_000.0,
        31_000.0,
        ID=identifier,
    )


def _chiller_facts(module: ModuleType) -> dict[str, Any]:
    fields = (
        "ID",
        "name",
        "cop",
        "capacity",
        "compressor_type",
        "coolingtower_type",
        "coolingtower_capacity",
        "coolingtower_control",
    )
    default = module.Chiller(
        "Chiller Default",
        "turbo",
        "open",
        "single-speed",
        ID="SRC-CHILLER-DEFAULT",
    )
    explicit = _chiller(module, "closed", "two-speed", "SRC-CHILLER-EXPLICIT")
    from_json = module.Chiller.from_json(
        SimpleNamespace(
            id="SRC-CHILLER-JSON",
            name="Chiller JSON",
            compressor_type="reciprocating",
            coolingtower_type="open",
            coolingtower_control="two-speed",
            cop_cooling=3.75,
            capacity_cooling=19_000.0,
            coolingtower_capacity=23_000.0,
        )
    )
    mutable = module.Chiller(
        "Chiller Mutable",
        "turbo",
        "open",
        "single-speed",
        ID="SRC-CHILLER-MUTABLE",
    )
    mutable.cop = 5.0
    mutable.capacity = 20_000
    mutable.compressor_type = "screw"
    mutable.coolingtower_type = "closed"
    mutable.coolingtower_capacity = 26_000
    mutable.coolingtower_control = "two-speed"
    tower_branches = []
    for tower_type in ("open", "closed"):
        for control in ("single-speed", "two-speed"):
            item = _chiller(
                module,
                tower_type,
                control,
                "SRC-CHILLER-" + tower_type.upper() + "-" + control.upper(),
            )
            converted = item.to_dragon()
            tower_branches.append(
                {
                    "control": control,
                    "dragon": _typed(converted),
                    "tower_type": tower_type,
                }
            )
    return {
        "base_classes": [base.__name__ for base in module.Chiller.__bases__],
        "default": _source_snapshot(default, fields),
        "explicit": _source_snapshot(explicit, fields),
        "from_json": _source_snapshot(from_json, fields),
        "mutated": _source_snapshot(mutable, fields),
        "tower_branches": tower_branches,
        "errors": {
            "capacity_zero": _exception(
                lambda: module.Chiller(
                    "bad", "turbo", "open", "single-speed", capacity=0
                )
            ),
            "compressor_invalid": _exception(
                lambda: module.Chiller(
                    "bad", "centrifugal", "open", "single-speed"
                )
            ),
            "coolingtower_capacity_zero": _exception(
                lambda: module.Chiller(
                    "bad",
                    "turbo",
                    "open",
                    "single-speed",
                    coolingtower_capacity=0,
                )
            ),
            "coolingtower_control_invalid": _exception(
                lambda: module.Chiller("bad", "turbo", "open", "variable")
            ),
            "coolingtower_type_invalid": _exception(
                lambda: module.Chiller(
                    "bad", "turbo", "hybrid", "single-speed"
                )
            ),
            "cop_zero": _exception(
                lambda: module.Chiller(
                    "bad", "turbo", "open", "single-speed", cop=0
                )
            ),
        },
    }


def _district_facts(module: ModuleType) -> dict[str, Any]:
    fields = ("ID", "name", "hotwater_supply")
    false_value = module.DistrictHeating(
        "District False", False, ID="SRC-DISTRICT-FALSE"
    )
    true_value = module.DistrictHeating(
        "District True", True, ID="SRC-DISTRICT-TRUE"
    )
    from_json = module.DistrictHeating.from_json(
        SimpleNamespace(
            id="SRC-DISTRICT-JSON",
            name="District JSON",
            hotwater_supply=True,
        )
    )
    false_value.hotwater_supply = True
    return {
        "base_classes": [base.__name__ for base in module.DistrictHeating.__bases__],
        "false_then_mutated": _source_snapshot(false_value, fields),
        "true": _source_snapshot(true_value, fields),
        "from_json": _source_snapshot(from_json, fields),
        "dragon": _typed(true_value.to_dragon()),
        "dragon_repeat_fresh": true_value.to_dragon() is not true_value.to_dragon(),
        "errors": {
            "hotwater_integer": _exception(
                lambda: module.DistrictHeating("bad", 1)
            ),
            "hotwater_none": _exception(
                lambda: module.DistrictHeating("bad", None)
            ),
        },
    }


def _heatpump_snapshot(instance: Any) -> dict[str, Any]:
    return _source_snapshot(
        instance,
        (
            "ID",
            "name",
            "fuel",
            "heating_cop",
            "cooling_cop",
            "heating_capacity",
            "cooling_capacity",
        ),
    )


def _geothermal_facts(module: ModuleType) -> dict[str, Any]:
    explicit = module.GeothermalHeatPump(
        "Geo Explicit",
        "electricity",
        4.5,
        5.0,
        18_000.0,
        16_000.0,
        ID="SRC-GEO-EXPLICIT",
    )
    from_json = module.GeothermalHeatPump.from_json(
        SimpleNamespace(
            id="SRC-GEO-JSON",
            name="Geo JSON",
            fuel_type="electricity",
            cop_heating=4.25,
            cop_cooling=4.75,
            capacity_heating=17_000.0,
            capacity_cooling=15_000.0,
        )
    )
    return {
        "base_classes": [base.__name__ for base in module.GeothermalHeatPump.__bases__],
        "explicit": _heatpump_snapshot(explicit),
        "from_json": _heatpump_snapshot(from_json),
        "is_heatpump": isinstance(explicit, module.HeatPump),
        "dragon": _typed(explicit.to_dragon()),
        "dragon_type": type(explicit.to_dragon()).__name__,
        "dragon_repeat_fresh": explicit.to_dragon() is not explicit.to_dragon(),
    }


def _heatpump_facts(module: ModuleType) -> dict[str, Any]:
    default = module.HeatPump(
        "HeatPump Default", "electricity", ID="SRC-HP-DEFAULT"
    )
    explicit = module.HeatPump(
        "HeatPump Explicit",
        module.Fuel.NATURALGAS,
        3.5,
        4.0,
        14_000.0,
        12_000.0,
        ID="SRC-HP-EXPLICIT",
    )
    from_json = module.HeatPump.from_json(
        SimpleNamespace(
            id="SRC-HP-JSON",
            name="HeatPump JSON",
            fuel_type="lpg",
            cop_heating=3.25,
            cop_cooling=3.75,
            capacity_heating=13_000.0,
            capacity_cooling=11_000.0,
        )
    )
    mutable = module.HeatPump(
        "HeatPump Mutable", "electricity", ID="SRC-HP-MUTABLE"
    )
    mutable.fuel = "oil"
    mutable.heating_cop = 4.2
    mutable.cooling_cop = 4.4
    mutable.heating_capacity = 15_000
    mutable.cooling_capacity = 12_500
    return {
        "base_classes": [base.__name__ for base in module.HeatPump.__bases__],
        "default": _heatpump_snapshot(default),
        "explicit": _heatpump_snapshot(explicit),
        "from_json": _heatpump_snapshot(from_json),
        "mutated": _heatpump_snapshot(mutable),
        "dragon": _typed(explicit.to_dragon()),
        "dragon_repeat_fresh": explicit.to_dragon() is not explicit.to_dragon(),
        "errors": {
            "cooling_capacity_zero": _exception(
                lambda: module.HeatPump(
                    "bad", "electricity", cooling_capacity=0
                )
            ),
            "cooling_cop_zero": _exception(
                lambda: module.HeatPump("bad", "electricity", cooling_cop=0)
            ),
            "fuel_invalid": _exception(
                lambda: module.HeatPump("bad", "coal")
            ),
            "heating_capacity_zero": _exception(
                lambda: module.HeatPump(
                    "bad", "electricity", heating_capacity=0
                )
            ),
            "heating_cop_zero": _exception(
                lambda: module.HeatPump("bad", "electricity", heating_cop=0)
            ),
        },
    }


def _execute_cases(module: ModuleType) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _absorption_facts(module),
        EXPECTED_CASE_IDS[1]: _boiler_facts(module),
        EXPECTED_CASE_IDS[2]: _chiller_facts(module),
        EXPECTED_CASE_IDS[3]: _district_facts(module),
        EXPECTED_CASE_IDS[4]: _geothermal_facts(module),
        EXPECTED_CASE_IDS[5]: _heatpump_facts(module),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("HVAC thermal-source observation order drifted.")
    return observations


def _runtime_receipt() -> dict[str, Any]:
    receipt = dict(BASE._runtime_receipt())
    receipt["thermal_source_support"] = {
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
        raise SystemExit("Pinned HVAC thermal-source support drifted.")


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
        "public_production_routes_only": True,
        "python_executes_native_runtime": False,
        "routes_sha256": canonical_sha256(NATIVE_ROUTES),
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        "source_receipts_sha256": canonical_sha256(list(NATIVE_SOURCE_RECEIPTS)),
    }
    digest = canonical_sha256(result)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise SystemExit("Pinned HVAC thermal-source native review drifted.")
    return result


def _coverage_by_symbol() -> dict[str, str]:
    result: dict[str, str] = {}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol] = definition["id"]
    if set(result) != set(TARGET_SYMBOLS):
        raise RuntimeError("HVAC thermal-source symbol coverage drifted.")
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
        / "hvac-thermal-source-work"
    )
    work_root.mkdir(parents=True, exist_ok=True)

    with BASE._isolated_import(imported_root, inventory["raw"]) as primary:
        module, loaded_modules = primary
        signatures = _runtime_signatures(module)
        observations = _execute_cases(module)

    with tempfile.TemporaryDirectory(
        prefix="epsimple-hvac-thermal-source-relocation-", dir=work_root
    ) as temporary:
        relocated_root = Path(temporary) / "src"
        BASE._copy_source_tree(imported_root, relocated_root)
        with BASE._isolated_import(relocated_root, inventory["raw"]) as relocated:
            relocated_module, relocated_modules = relocated
            relocated_signatures = _runtime_signatures(relocated_module)
            relocated_observations = _execute_cases(relocated_module)

    if signatures != relocated_signatures:
        raise RuntimeError("HVAC thermal-source signatures changed after relocation.")
    if observations != relocated_observations:
        raise RuntimeError("HVAC thermal-source observations changed after relocation.")
    if loaded_modules != relocated_modules:
        raise RuntimeError("HVAC thermal-source loaded modules changed after relocation.")

    signatures_hash = canonical_sha256(signatures)
    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned HVAC thermal-source signatures drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and modules_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned HVAC thermal-source loaded modules drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Pinned HVAC thermal-source relocation observations drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned HVAC thermal-source fact hashes drifted.\n"
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
            "Pinned HVAC thermal-source case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned HVAC thermal-source aggregate case hash drifted.")

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
        raise RuntimeError("HVAC thermal-source oracle root key set drifted.")
    if value["schema"] != SCHEMA:
        raise RuntimeError("HVAC thermal-source schema drifted.")
    if value["target_receipts"] != list(TARGET_RECEIPTS):
        raise RuntimeError("HVAC thermal-source target receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("HVAC thermal-source symbol descriptors drifted.")
    target_hash = canonical_sha256(value["target_receipts"])
    if EXPECTED_TARGET_RECEIPTS_SHA256 and target_hash != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise RuntimeError("Pinned HVAC thermal-source target receipt hash drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("HVAC thermal-source runtime signatures are absent.")
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise RuntimeError("Pinned HVAC thermal-source runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(signatures):
        raise RuntimeError("HVAC thermal-source consumer contract drifted.")
    if value["runtime"] != _runtime_receipt():
        raise RuntimeError("HVAC thermal-source runtime receipt drifted.")
    if value["native_review"] != _native_review():
        raise RuntimeError("HVAC thermal-source native review drifted.")

    upstream = value["upstream"]
    if not isinstance(upstream, dict) or set(upstream) != {
        "adjacent_receipts_sha256",
        "commit",
        "inventory",
        "isolated_import",
        "source",
        "target_receipts_sha256",
    }:
        raise RuntimeError("HVAC thermal-source upstream key set drifted.")
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
            raise RuntimeError(f"HVAC thermal-source upstream field drifted: {key}")
    if upstream["target_receipts_sha256"] != canonical_sha256(value["target_receipts"]):
        raise RuntimeError("HVAC thermal-source upstream target receipt hash drifted.")
    if (
        EXPECTED_ADJACENT_RECEIPTS_SHA256
        and upstream["adjacent_receipts_sha256"]
        != EXPECTED_ADJACENT_RECEIPTS_SHA256
    ):
        raise RuntimeError("Pinned HVAC thermal-source adjacent receipt hash drifted.")
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
        raise RuntimeError("HVAC thermal-source isolated-import key set drifted.")
    if (
        isolated["source_location_count"] != 2
        or isolated["epsimple_package_initializer_executed"]
        or isolated["epsimple_core_initializer_executed"]
        or isolated["relocated_source_copy"]
        != "byte-identical-epsimple-and-idragon-trees"
    ):
        raise RuntimeError("HVAC thermal-source relocation claim drifted.")
    loaded = isolated["loaded_local_modules"]
    if (
        not isinstance(loaded, list)
        or isolated["loaded_local_modules_sha256"] != canonical_sha256(loaded)
    ):
        raise RuntimeError("HVAC thermal-source loaded-module receipt drifted.")
    if (
        EXPECTED_LOADED_LOCAL_MODULES_SHA256
        and canonical_sha256(loaded) != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise RuntimeError("Pinned HVAC thermal-source loaded modules drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and isolated["relocated_observations_sha256"]
        != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise RuntimeError("Pinned HVAC thermal-source relocation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("HVAC thermal-source case count drifted.")
    if [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("HVAC thermal-source case order drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        if set(case) != {*definition, "python"}:
            raise RuntimeError(
                f"HVAC thermal-source case key set drifted: {definition['id']}"
            )
        for key, expected in definition.items():
            if case[key] != expected:
                raise RuntimeError(
                    f"HVAC thermal-source case definition drifted: {definition['id']}"
                )
        python = case["python"]
        if (
            not isinstance(python, dict)
            or set(python) != {"facts", "facts_sha256", "outcome"}
            or python["outcome"] != "observed"
        ):
            raise RuntimeError(
                f"HVAC thermal-source Python observation drifted: {definition['id']}"
            )
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(
                f"HVAC thermal-source inline fact hash drifted: {definition['id']}"
            )
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("HVAC thermal-source fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned HVAC thermal-source fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("HVAC thermal-source case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned HVAC thermal-source case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("HVAC thermal-source aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned HVAC thermal-source aggregate hash drifted.")
    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC thermal-source exact target closure drifted.")
    closure = value["consumer_contract"]["closure"]
    if (
        closure["target_indices"] != list(TARGET_INDICES)
        or closure["adjacent_indices"] != list(ADJACENT_INDICES)
        or sorted((*closure["target_indices"], *closure["adjacent_indices"]))
        != list(SOURCE_INDICES)
    ):
        raise RuntimeError("HVAC thermal-source full source closure drifted.")
    BASE._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("HVAC thermal-source strict JSON round trip drifted.")


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
        f"Wrote {len(oracle['cases'])} HVAC thermal-source cases covering "
        f"{len(TARGET_RECEIPTS)} declarations: {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception, aggregate {oracle['cases_sha256']}."
    )


if __name__ == "__main__":
    main()

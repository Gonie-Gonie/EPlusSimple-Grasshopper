"""Generate the pinned EPlusSimple HVAC supply-system behavior oracle.

This bounded corpus executes exactly 52 declarations for AirHandlingUnit,
ElectricRadiantFloor, ElectricRadiator, FanCoilUnit, PackagedAirConditioner,
RadiantFloor, Radiator, and SupplySystem in the pinned
``src/epsimple/core/hvac.py`` source.  The other 150 declarations are retained
as adjacent non-target receipts.  The upstream module is imported without
either EPlusSimple package initializer and is executed again from a
byte-identical relocated source tree.
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


SCHEMA = "dragons.python-reference.epsimple-hvac-supply-system.v1"
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

BASE_PATH = Path(__file__).resolve().with_name(
    "generate_epsimple_hvac_enums_base_oracle.py"
)
EXPECTED_BASE_BYTES = 61_458
EXPECTED_BASE_SHA256 = (
    "sha256:eaa5691d29c341844097c8690f0e12970824494f1e00e8287811b7876ba3df0d"
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
        "_dragons_epsimple_hvac_supply_system_base", BASE_PATH
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
    (147, 'AirHandlingUnit', 'class', 'sha256:6fd0030bb650b67798e4eb3d2b4b50a44cc9309e47f44858931dcf1b92d2baa5', 'sha256:034590641efa61c135722618e2bdb0d9425ef0cc6a593e8b7a7438b575e4bd9d', 'sha256:644081ff98e0d9dce3b1dd7f739431a71ce8c7524f85a0ec6bc3f40f6bf98de3'),
    (148, 'AirHandlingUnit.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (151, 'AirHandlingUnit.__init__', 'function', 'sha256:ea6e311cb7493ec68f0be403fc02f1eae98dac5ef2d3c7891dd30c4a9ba49df8', 'sha256:ff922c21296cd5a935d82405d1e2aded04638ff6871358bc0b608c7f6bffec3e', 'sha256:0cc8b1eb7455ed6cc1db0d1c38c63038f36496b63c5c02354464a99b86c90088'),
    (154, 'AirHandlingUnit.from_json', 'function', 'sha256:148b0ee3185947c81beff56237a82cde44e855bcaf249925fb067b30b758af02', 'sha256:e72068dd7c37ad55411e8beda4080e4f6af75f99334b2119c195dd9d7a619b13', 'sha256:4757bae32fa6dbc96eb6e60d9de9c51ba7e2c672e657c1427985a9efa18e45de'),
    (155, 'AirHandlingUnit.source', 'function', 'sha256:ef79e1d5dc6f6eaf3bf77fb3dbc3448b90e7460cfac7d82976d7a78024e337a8', 'sha256:c40ec870361be3716a817d13e5109cd53317ffea2213de16b2a6388ceeb8d40f', 'sha256:4442afe01a47794c3f7563dc933f47fd49ae9aaf6f85fb8c752ddd48a163c802'),
    (156, 'AirHandlingUnit.to_dragon', 'function', 'sha256:11a6909a36a0459e5e84d8526678556dea762d1fad19c46318cf8cb9ef2b50c8', 'sha256:e590d06a61927a56c75f002322d6cf177d089441239094959799672a68bbbcb4', 'sha256:167c908c16ef001dfaa09c9f5f4b390339fe0785c0f6be83067800dd2ac9d458'),
    (209, 'ElectricRadiantFloor', 'class', 'sha256:f7f03ff5a5f2cedccb30ad66919833a5b3ed346cf20d31a93123a814c3bda228', 'sha256:9d974909f8251f2f7eeff6cb5712f3e288cfa45df22bff65f45a642891284513', 'sha256:f4da9d03c6960b614b37bcd65261cdf4f77698a0fec10c7db147769c4b6e3311'),
    (210, 'ElectricRadiantFloor.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (213, 'ElectricRadiantFloor.__init__', 'function', 'sha256:f8bde28f68306fd83c30c36a380bf2a2d0ab8f08ff696a8ba543bf2aa6e02abb', 'sha256:105d8455b8c87957bb0de8711d90f3138b23fa0c713656af9e46572a6041cc7d', 'sha256:0000b51bf029340173e6fceaf716b4f4da7081e7b23efbfabaea1f5d0e80e19f'),
    (216, 'ElectricRadiantFloor.from_json', 'function', 'sha256:b13a953698a740ae6f0de057f21881cd97ade59b0fea0cc3c4af2fc28480c90c', 'sha256:ee7e08c298ad4ef7dfa8ed2f6c3aff4ecb16bd2ba67f337e679e632e9b3fbe90', 'sha256:9aefd7f08ac066bc60e48dbb21bfe85ffdc40a14d909aaa24d155b29c8a71bdf'),
    (217, 'ElectricRadiantFloor.source', 'function', 'sha256:b14aeb3a49b0c8bd8dfbe0581a0b1f67b12b366048d074b52786dcd726847fc7', 'sha256:6ae02a07321f7497f335eb6bf52e34eb18892e50361f4307f9c724a602317c0e', 'sha256:dcb489f3c3932b57a7108b78b1823b683ce86e8d71e55948848cae1d7bb3566a'),
    (218, 'ElectricRadiantFloor.to_dragon', 'function', 'sha256:01ae7da4573343c5acf5ba2474c697b860f78f9fee67c8dddbf8f6e522a171ef', 'sha256:7e7cec81cc315dc37211e6000685127d4458a60d9f4817f56a4f44ce1bdb6f12', 'sha256:e68494cd6b57ccbc7e78e89f0c06dc36b62c9f3a1d54298c3f3e08c7d14aaf5b'),
    (219, 'ElectricRadiator', 'class', 'sha256:6354666eadd3e2751913c17acc82528be12be62265bbc12daf0376f7bf3ef44c', 'sha256:1c9170eb76b09c7feea649df317e2a08e6baf0c314de14d3f28859af519d9b05', 'sha256:f4da9d03c6960b614b37bcd65261cdf4f77698a0fec10c7db147769c4b6e3311'),
    (220, 'ElectricRadiator.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (223, 'ElectricRadiator.__init__', 'function', 'sha256:3a47135f63f2cdb439eef5ed5f65dde63949f6a17a2c3d97f6be17106abfdcb4', 'sha256:124207f8097a778aeb3af50ecfe35973b4898b097751c87322f4bc3ea86fccb6', 'sha256:6525afaad9d637d6fd6cd218cc660c3d9abee6fc45eac6efef710fafc94c91c1'),
    (226, 'ElectricRadiator.capacity', 'function', 'sha256:09cfea01a6a157d670e72cbe6db8f4f9d2dd5e6af6c77dd69c4cff1fe8f6d4e1', 'sha256:cc93bb37e2ab0f82f6086da6acbfc044ca713c16ec8afec84a142c6a17312c4d', 'sha256:f4aa61cb14fb78877b25e1727abe6b859eb528573035851ef4823bf96386e5a4'),
    (227, 'ElectricRadiator.from_json', 'function', 'sha256:20bd3338f7cc185c10800f70fd4ddb46812fc5c886d8f3538f6eeae20fba1567', 'sha256:e8357859f04daaa24672ca5422ea5340f07c4f1125ea63a1f3c2858cf0547a81', 'sha256:3555197f1a129ad548d474b19c008382af5582b769d9211e3057d9a314929508'),
    (228, 'ElectricRadiator.source', 'function', 'sha256:b14aeb3a49b0c8bd8dfbe0581a0b1f67b12b366048d074b52786dcd726847fc7', 'sha256:6ae02a07321f7497f335eb6bf52e34eb18892e50361f4307f9c724a602317c0e', 'sha256:dcb489f3c3932b57a7108b78b1823b683ce86e8d71e55948848cae1d7bb3566a'),
    (229, 'ElectricRadiator.to_dragon', 'function', 'sha256:4b95c9d6e62b1dcc13813749df567fdbf471b577ef1f7573861d5ec67ca16ee6', 'sha256:2f211a257843bef8a87ad6d5638f1e0049bc11fbb26e8c931b1679df154420f5', 'sha256:ffb6972794a329be0127ddf4bf55a6e4b84869b248cb32246bef5cd1a0d812af'),
    (230, 'FanCoilUnit', 'class', 'sha256:618e77c4c3ff965908603766f3804fbb7d490aff54e01196c77d7ac0f2d3ae1e', 'sha256:de6cfd7ddb8721c72a3c936f2d1d1027c6f1a91c4b554fd05bab50e1defb6ee0', 'sha256:b5c0e9c1f80ef5884ecae10360d7afe348ea648a90c1efa48455f57a1a2452d6'),
    (231, 'FanCoilUnit.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (234, 'FanCoilUnit.__init__', 'function', 'sha256:ea6e311cb7493ec68f0be403fc02f1eae98dac5ef2d3c7891dd30c4a9ba49df8', 'sha256:ff922c21296cd5a935d82405d1e2aded04638ff6871358bc0b608c7f6bffec3e', 'sha256:0cc8b1eb7455ed6cc1db0d1c38c63038f36496b63c5c02354464a99b86c90088'),
    (237, 'FanCoilUnit.from_json', 'function', 'sha256:4e773b8a6d49b9bc66097044489d7cd39d0c66cfea71b52b45c916c18a9e7b35', 'sha256:a9e19e07c96957f320e3410ace244d3f9ab65320a99cf0912d0e64e696f1b8bf', 'sha256:97aaa85b546f5c026f9f1263b63e44786ec00b261a8195d1661238909fa23885'),
    (238, 'FanCoilUnit.source', 'function', 'sha256:ef79e1d5dc6f6eaf3bf77fb3dbc3448b90e7460cfac7d82976d7a78024e337a8', 'sha256:c40ec870361be3716a817d13e5109cd53317ffea2213de16b2a6388ceeb8d40f', 'sha256:4442afe01a47794c3f7563dc933f47fd49ae9aaf6f85fb8c752ddd48a163c802'),
    (239, 'FanCoilUnit.to_dragon', 'function', 'sha256:09f124747387123277547f78b46e1c80651111dc6984dd26aa7adadd195b5ebf', 'sha256:c6725ba1d0dc9eb74f17aead43049e141e49b0c867e00bd02b33479b2aafd9aa', 'sha256:6aad66b1cd582f39fb4670bc1ab37bddb2b453d3750ec2ed52e9a19efc4d9e13'),
    (271, 'PackagedAirConditioner', 'class', 'sha256:fcef63398815558d11a3c48fe89d0c3e0051f24a79d55672cba849e08b6d2eaa', 'sha256:a1f38707ed1ca8008369419a24a592c1268b446750d113f9b35d6a72df182bd1', 'sha256:6965df85b1111302b1158597b4e86976daddc109107b0819926ee396209489fc'),
    (272, 'PackagedAirConditioner.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (275, 'PackagedAirConditioner.__init__', 'function', 'sha256:b2021d847992c5a86a23d863d37050a80da355055500f1d912686561f4ef5288', 'sha256:51da79096974318c99fe4e790c4515b51fb9ce8448421480f57f3ad7cd54f04b', 'sha256:8783fcb7be82ddb87838124d912a974b12ce6915edca0cbc63b51eb9b57195c0'),
    (278, 'PackagedAirConditioner.capacity', 'function', 'sha256:09cfea01a6a157d670e72cbe6db8f4f9d2dd5e6af6c77dd69c4cff1fe8f6d4e1', 'sha256:cc93bb37e2ab0f82f6086da6acbfc044ca713c16ec8afec84a142c6a17312c4d', 'sha256:f4aa61cb14fb78877b25e1727abe6b859eb528573035851ef4823bf96386e5a4'),
    (279, 'PackagedAirConditioner.cop', 'function', 'sha256:873a49d321180f9f6396b44b23751ea3e025b5c874e7e311491d54e7bcafdcc5', 'sha256:9e252719c39ab6012f0bd288c4083d9d7a7e4ce2c7d86ff33e3dc4c5d31a7578', 'sha256:a7f4e9f7638f69f248c7ba9d965a0c34f33b35868bc64d8be900345af2c33f7a'),
    (280, 'PackagedAirConditioner.from_json', 'function', 'sha256:d49a3e1b427251ccd94d5bac5d99c50d9dcb60da1aa7fd2ebc97ec6c3766c9a1', 'sha256:25e02dccf8701ca9a926ed4e21165a048abc08f7fa10a9ad0c7efc27e8bb19be', 'sha256:da645adada745b2a0c391ff6d4f85041a4cb32fd6f0af1e4447c6aa75238da7b'),
    (281, 'PackagedAirConditioner.source', 'function', 'sha256:b14aeb3a49b0c8bd8dfbe0581a0b1f67b12b366048d074b52786dcd726847fc7', 'sha256:6ae02a07321f7497f335eb6bf52e34eb18892e50361f4307f9c724a602317c0e', 'sha256:dcb489f3c3932b57a7108b78b1823b683ce86e8d71e55948848cae1d7bb3566a'),
    (282, 'PackagedAirConditioner.to_dragon', 'function', 'sha256:0be4894a1c2a75079fc1f5528c30fb1e01a0f3378617f5ce9b453e181154ae4c', 'sha256:3de29776ccd933a3c65e81fd2dd359a42473f6a7a7c1881305c6a6208550afef', 'sha256:0d83ee8547a2553915f3e47991c88289310d59f33c16581a32acac6e9532b83e'),
    (296, 'RadiantFloor', 'class', 'sha256:3a70e982c106c5212623a10732007a9852064f5ad425395b6e45bc62500beed4', 'sha256:6ac8e4688e63eb42b58f942cc69c0d425c2e2b16c0ca931c9a0c2fc9500c1471', 'sha256:333cc42746dff81f974eb6f6810893812fb9d300b5ba3d6e7111309cb7f92ad9'),
    (297, 'RadiantFloor.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (300, 'RadiantFloor.__init__', 'function', 'sha256:ea6e311cb7493ec68f0be403fc02f1eae98dac5ef2d3c7891dd30c4a9ba49df8', 'sha256:ff922c21296cd5a935d82405d1e2aded04638ff6871358bc0b608c7f6bffec3e', 'sha256:0cc8b1eb7455ed6cc1db0d1c38c63038f36496b63c5c02354464a99b86c90088'),
    (303, 'RadiantFloor.coolable', 'function', 'sha256:b81ea250ac6244b33580b16bb18b30bf835ce33f4b947ae01243d866f94d9795', 'sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3', 'sha256:fac934170d680a1a998c791b2d9a0e21abc210518d2f2ec8a56633facc2394a0'),
    (304, 'RadiantFloor.from_json', 'function', 'sha256:a3c19218d4a8a9106ef202bc83d5344709b7e5935967766c107b2595dae20733', 'sha256:dfbfe8c6ecbefd43dec9ec5e3e1811a7c021a7d4c936f74676706c63f143e286', 'sha256:c326dd5cd32e05348b9b6353753974a9f67021c4afae5468552c0e026216643a'),
    (305, 'RadiantFloor.heatable', 'function', 'sha256:0b60e64a309323590a641eb4ac517d15891d836f48176947c1f7a8df43d244db', 'sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3', 'sha256:a200989331792d789cc947c1b615c0eb8c31e552b2dbe4f805b7ad72e3f082d4'),
    (306, 'RadiantFloor.source', 'function', 'sha256:ef79e1d5dc6f6eaf3bf77fb3dbc3448b90e7460cfac7d82976d7a78024e337a8', 'sha256:c40ec870361be3716a817d13e5109cd53317ffea2213de16b2a6388ceeb8d40f', 'sha256:4442afe01a47794c3f7563dc933f47fd49ae9aaf6f85fb8c752ddd48a163c802'),
    (307, 'RadiantFloor.to_dragon', 'function', 'sha256:db1248599e13656a85a83bc3af1716b755819ce437af3a3f245a9b52d9c1c106', 'sha256:0177243494086b0d6d20ad83d9adb434be666785699d3d7079b5223d2d9d829e', 'sha256:7d0905ff487f7dc632518e694f9ed1772ba8a76da2c1a86472489f44f620b2d0'),
    (308, 'Radiator', 'class', 'sha256:8464a277a095d9e36a40b54c366b83405fc63809556dae795c5d1a1513112d73', 'sha256:c6b48693d6dadbb8ae0abebc8271d507d00111e70927ac4caa1f34229e94d1db', 'sha256:333cc42746dff81f974eb6f6810893812fb9d300b5ba3d6e7111309cb7f92ad9'),
    (309, 'Radiator.ID', 'function', 'sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2', 'sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb', 'sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da'),
    (312, 'Radiator.__init__', 'function', 'sha256:35304b6f549ec68ca020d5db6d8a8412a038c5a07c18825b25b5fe9ef775a9f4', 'sha256:8fa22f5eff68f7f34d4294f671aa876da59c4e57c7960bb9c4676e39a269af5c', 'sha256:a87f4596d58e071a70165fa2a955d67f11a8b734ccbf60661f30fc18162fec35'),
    (315, 'Radiator.capacity', 'function', 'sha256:d699d5f1b04af7405ec421c7c72d1c1425f40bd007065cbec301d1ea9c5bffcb', 'sha256:bbbc01de62864a8be9a07c61355493d534500ac6ebb3732f687667ec2b4dffbd', 'sha256:f4aa61cb14fb78877b25e1727abe6b859eb528573035851ef4823bf96386e5a4'),
    (316, 'Radiator.from_json', 'function', 'sha256:349b941b7fb11e3009624a5a75e0707deecd733bacc496f50312779251f5ddf8', 'sha256:53a1edbebf6d656971cf1f19b71b03ea025f49387b7bec20a7ecf6306a166017', 'sha256:331107434c11accd70633c311eb4289240f9eab9962336bb677976a5be5c7591'),
    (317, 'Radiator.source', 'function', 'sha256:ef79e1d5dc6f6eaf3bf77fb3dbc3448b90e7460cfac7d82976d7a78024e337a8', 'sha256:c40ec870361be3716a817d13e5109cd53317ffea2213de16b2a6388ceeb8d40f', 'sha256:4442afe01a47794c3f7563dc933f47fd49ae9aaf6f85fb8c752ddd48a163c802'),
    (318, 'Radiator.to_dragon', 'function', 'sha256:bb8edb65b591e39b622c1569a5969446a150d5e5b0ea7f58d65d34176faabdea', 'sha256:a591803a0c769106be546c678cd7cbb08eaf9c4b433d4b526c03405d55e784d3', 'sha256:48bf1f2a74c0a55a019873f668c2a539ca419e40ff2e9fc799e373a63940b41f'),
    (321, 'SupplySystem', 'class', 'sha256:d236c0a04078304ea345cbd7c8c1869302365acb12658f8497b0826edc52332e', 'sha256:fc556e768556332704e50f080f462c730f7235a8d85b7ba9247517c53588d919', 'sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726'),
    (322, 'SupplySystem.TYPE_MAPPER', 'constant', 'sha256:3639f05812aac7d5fc787679da794f56e05943dc68307da0985c671386518022', 'sha256:7e528899651b38b262aa7c22be2b1a3277169c7406d887fb67f3454a9f44ace8', 'sha256:b9eba64579e158cc0b805b796a183e3a7be5ab8d11af2381a93a3bea7a73caad'),
    (323, 'SupplySystem.coolable', 'function', 'sha256:a658d7c48c13e1d67dd38898c56df5cabff584bd320d5dfe366247981fcde979', 'sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3', 'sha256:53f089ff50733d36140fdcd3bac778d81c5165324206853953ec66075c5940b5'),
    (324, 'SupplySystem.heatable', 'function', 'sha256:9d89b0d88c186efc0a1897ac3ad1658f6dc7d024b162188408a81461e13b7372', 'sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3', 'sha256:cd6c95a179a95ab99b2f0b173e9fb980866ddb81680871d64588621a6ce90e75'),
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
if len(TARGET_INDICES) != 52 or len(ADJACENT_INDICES) != 150:
    raise RuntimeError("HVAC supply-system source partition count drifted.")

EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:5753763192194cfdcef58cb9baf438770dd1bd07bb2a4b846c3e8168f032f839"
)
EXPECTED_ADJACENT_RECEIPTS_SHA256 = (
    "sha256:8516665711bdf76cc747fe3843b097c8ee038dde68a4449278c365a0315542d4"
)
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:12d359d81856556caa506bf380f60baddfd1ab46af8042090a77a831c3a467b4"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:e19bb4e2660bce5866feb71cdaf7d6906f7e8ce8043667f63a290adfeeb115b8"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:9aa09c93f083fd82df4a25c756fbebf5c8138a44db926f316ad42bb298e2fc64"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:4f5dfc68347827185ddbabfe9734c052342583fe11860eafd207622f5a92cebe"
)
EXPECTED_FACT_SHA256 = {
    "epsimple-hvac-supply-system.air-handling-unit-construction-json-source-capabilities-dragon": "sha256:647e2b09be67fe1e7d2af204ed2cad94bf2cf729d6b73976177cf330bd8a7fcf",
    "epsimple-hvac-supply-system.electric-radiant-floor-construction-json-null-source-dragon": "sha256:3c35d57c9de0453c01b1baa5267c49f6da094fd78d8a85ecd88bdc1b02b1b6a7",
    "epsimple-hvac-supply-system.electric-radiator-capacity-validation-json-null-source-dragon": "sha256:5fd43fa8e2f25548aba0db4c4a14480faa98e1a52148962ea2422ac0603a91f3",
    "epsimple-hvac-supply-system.fan-coil-unit-source-branches-json-dragon": "sha256:50a57e6921b7bcce8d810224e31861c4222455777e1e9628799b36294667c830",
    "epsimple-hvac-supply-system.packaged-air-conditioner-defaults-validation-json-dedicated-dragon": "sha256:3ae220873d535c22a827c1a62e515c7659237a0d9794d3a9907418523a3103e9",
    "epsimple-hvac-supply-system.radiant-floor-source-capabilities-json-dragon": "sha256:24078204d3dce794bcc70ccdd50097fbe04e835b5fc23fd66f2abaa76d24199d",
    "epsimple-hvac-supply-system.radiator-capacity-validation-json-dragon": "sha256:ca419685a68ce1fc2810be16b609633882fc0a1d11dd7f7d73eea18e445c9d2e",
    "epsimple-hvac-supply-system.supply-system-base-mapper-capability-topology": "sha256:2f715484e1a9a16a8ae0ce1f4dbdda60d9fefe9e5ddfa2f4d522d072353698a1",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-hvac-supply-system.air-handling-unit-construction-json-source-capabilities-dragon": "sha256:a698272c6fb8e6e337ce12d6f91ee271625452506115f29b4bf385febb5c9462",
    "epsimple-hvac-supply-system.electric-radiant-floor-construction-json-null-source-dragon": "sha256:40591ce52129ae4400fe15825d3ebe78c8399cbff3b33ab1a32b8b3cfa15475c",
    "epsimple-hvac-supply-system.electric-radiator-capacity-validation-json-null-source-dragon": "sha256:9c2e15187f22a4605cb2fd09eb17d56d4940a3f2f10ae796005cb0272ab904a6",
    "epsimple-hvac-supply-system.fan-coil-unit-source-branches-json-dragon": "sha256:347f716087187af48ef8a99e73bf07ea900f9ad6e9ec90e969e4aba72387dfba",
    "epsimple-hvac-supply-system.packaged-air-conditioner-defaults-validation-json-dedicated-dragon": "sha256:827b661ffea091fded609e07e1c9ccec144327e54e15230fdfe4bd8b71f89642",
    "epsimple-hvac-supply-system.radiant-floor-source-capabilities-json-dragon": "sha256:e6bfc2d25e7cbc00aea6f5a2a52c7df23f7076f9a5014b4c92e92f3de660c9f4",
    "epsimple-hvac-supply-system.radiator-capacity-validation-json-dragon": "sha256:57b8d53ca5fc12088454084dcc0a581c14a46c79bef29d7470f3a2968105815a",
    "epsimple-hvac-supply-system.supply-system-base-mapper-capability-topology": "sha256:c7ddd975217eab969087a73702d712dcaaf57c58514a79fd93ca22ea5ceb9dc0",
}
EXPECTED_CASES_SHA256 = (
    "sha256:844e26e1e019dc9fea4d12cc594c6d83ab3c1823e58ab8253ba809a591dd10a2"
)

_EXCEPTION_MEMBERS = {"__init__", "from_json", "to_dragon"}
EXCEPTION_SYMBOLS = {
    symbol
    for symbol in TARGET_SYMBOLS
    if "." not in symbol or symbol.rsplit(".", 1)[1] in _EXCEPTION_MEMBERS
} | {
    "ElectricRadiantFloor.source",
    "ElectricRadiator.source",
    "PackagedAirConditioner.source",
    "SupplySystem.TYPE_MAPPER",
}
CLASSIFICATIONS = {
    symbol: "exception" if symbol in EXCEPTION_SYMBOLS else "equivalent"
    for symbol in TARGET_SYMBOLS
}
ADAPTATIONS = {
    symbol: (
        "reviewed-native-discriminated-supply-aggregate-and-conversion-route-"
        + TARGET_HASHES[symbol][7:15]
    )
    for symbol in EXCEPTION_SYMBOLS
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"epsimple-hvac-supply-system-{item['inventory_index']}-"
        f"{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}


def _class_name(symbol: str) -> str:
    return symbol.split(".", 1)[0]


_PROPERTY_ROUTES = {
    "AirHandlingUnit.ID": "Id",
    "AirHandlingUnit.source": "SourceSystem",
    "ElectricRadiantFloor.ID": "Id",
    "ElectricRadiantFloor.source": "SourceSystem",
    "ElectricRadiator.ID": "Id",
    "ElectricRadiator.capacity": "HeatingCapacity",
    "ElectricRadiator.source": "SourceSystem",
    "FanCoilUnit.ID": "Id",
    "FanCoilUnit.source": "SourceSystem",
    "PackagedAirConditioner.ID": "Id",
    "PackagedAirConditioner.capacity": "CoolingCapacity",
    "PackagedAirConditioner.cop": "CoolingCop",
    "PackagedAirConditioner.source": "SourceSystem",
    "RadiantFloor.ID": "Id",
    "RadiantFloor.coolable": "Coolable",
    "RadiantFloor.heatable": "Heatable",
    "RadiantFloor.source": "SourceSystem",
    "Radiator.ID": "Id",
    "Radiator.capacity": "HeatingCapacity",
    "Radiator.source": "SourceSystem",
    "SupplySystem.coolable": "Coolable",
    "SupplySystem.heatable": "Heatable",
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
            "SimpleDragonDatabase?) supply-system dispatch"
        )
    if member == "to_dragon":
        return (
            "Dragons.SimpleDragon.GreenRetrofitConverter.Convert("
            "GreenRetrofitModel, GreenRetrofitConversionOptions?)"
        )
    if symbol == "SupplySystem.TYPE_MAPPER":
        return (
            "Dragons.SimpleDragon.GrmReader.Read(string, "
            "SimpleDragonDatabase?) with SupplySystemType dispatch"
        )
    if symbol in {"SupplySystem", "SupplySystem.coolable", "SupplySystem.heatable"}:
        return "Dragons.SimpleDragon.SupplySystem constructor and public properties"
    return (
        "Dragons.SimpleDragon.SupplySystem constructor with "
        f"SupplySystemType.{_class_name(symbol)} and public properties"
    )


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}
NATIVE_SOURCE_RECEIPTS = (
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

PREFIX = "epsimple-hvac-supply-system."
CASE_SPECS = (
    ("A01", "air-handling-unit-construction-json-source-capabilities-dragon", "air-handling-unit", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("AirHandlingUnit"))),
    ("E01", "electric-radiant-floor-construction-json-null-source-dragon", "electric-radiant-floor", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("ElectricRadiantFloor"))),
    ("E02", "electric-radiator-capacity-validation-json-null-source-dragon", "electric-radiator", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("ElectricRadiator"))),
    ("F01", "fan-coil-unit-source-branches-json-dragon", "fan-coil-unit", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("FanCoilUnit"))),
    ("P01", "packaged-air-conditioner-defaults-validation-json-dedicated-dragon", "packaged-air-conditioner", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("PackagedAirConditioner"))),
    ("R01", "radiant-floor-source-capabilities-json-dragon", "radiant-floor", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("RadiantFloor"))),
    ("R02", "radiator-capacity-validation-json-dragon", "radiator", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("Radiator"))),
    ("S01", "supply-system-base-mapper-capability-topology", "supply-system", tuple(symbol for symbol in TARGET_SYMBOLS if symbol.startswith("SupplySystem"))),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 8


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
        raise RuntimeError("HVAC supply-system case order drifted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC supply-system targets are not an exact case partition.")
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
                "HVAC supply-system inventory receipt drifted at index "
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
        raise SystemExit("Pinned HVAC supply-system target receipts drifted.")
    if EXPECTED_ADJACENT_RECEIPTS_SHA256 and adjacent_hash != EXPECTED_ADJACENT_RECEIPTS_SHA256:
        raise SystemExit("Pinned HVAC supply-system adjacent receipts drifted.")
    if sorted((*TARGET_INDICES, *ADJACENT_INDICES)) != list(SOURCE_INDICES):
        raise RuntimeError("The HVAC supply-system source partition is incomplete.")
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


def _make_sources(module: ModuleType) -> dict[str, Any]:
    return {
        "absorption": module.AbsorptionChiller(
            "Absorption Source",
            1.1,
            12_000.0,
            "natural_gas",
            0.8,
            ID="SRC-ABSORPTION",
        ),
        "boiler": module.Boiler(
            "Boiler Source",
            "natural_gas",
            False,
            0.9,
            15_000.0,
            ID="SRC-BOILER",
        ),
        "chiller": module.Chiller(
            "Chiller Source",
            "screw",
            "open",
            "single-speed",
            4.25,
            18_000.0,
            22_000.0,
            ID="SRC-CHILLER",
        ),
        "district": module.DistrictHeating(
            "District Source",
            False,
            ID="SRC-DISTRICT",
        ),
        "geothermal": module.GeothermalHeatPump(
            "Geothermal Source",
            "electricity",
            4.5,
            5.0,
            17_000.0,
            16_000.0,
            ID="SRC-GEOTHERMAL",
        ),
        "heatpump": module.HeatPump(
            "HeatPump Source",
            "electricity",
            3.5,
            4.0,
            14_000.0,
            13_000.0,
            ID="SRC-HEATPUMP",
        ),
    }


def _source_link(source: Any) -> dict[str, Any]:
    result = {
        "class": type(source).__name__,
        "class_module": type(source).__module__,
    }
    values: dict[str, Any] = {}
    for field in (
        "ID",
        "fuel",
        "heating_cop",
        "cooling_cop",
        "heating_capacity",
        "cooling_capacity",
        "cop",
        "capacity",
        "efficiency",
        "boiler_efficiency",
        "hotwater_supply",
    ):
        if hasattr(source, field):
            values[field] = _typed(getattr(source, field))
    result["values"] = values
    return result


def _supply_snapshot(instance: Any, fields: tuple[str, ...] = ()) -> dict[str, Any]:
    source = getattr(instance, "source", None)
    values = {
        "ID": instance.ID,
        "coolable": _typed(instance.coolable),
        "heatable": _typed(instance.heatable),
        "name": instance.name,
        **{field: _typed(getattr(instance, field)) for field in fields},
    }
    return {
        "class": type(instance).__name__,
        "class_module": type(instance).__module__,
        "source": _source_link(source),
        "values": values,
    }


def _dragon_supply_snapshot(instance: Any) -> dict[str, Any]:
    values: dict[str, Any] = {}
    for field in (
        "name",
        "capacity",
        "cop",
        "heatable",
        "coolable",
        "efficiency",
        "radiant_fraction",
        "throttling_range",
    ):
        if hasattr(instance, field):
            values[field] = _typed(getattr(instance, field))
    source = getattr(instance, "source", None)
    return {
        "class": type(instance).__name__,
        "class_module": type(instance).__module__,
        "source": _source_link(source),
        "values": values,
    }


def _air_handling_unit_facts(module: ModuleType) -> dict[str, Any]:
    sources = _make_sources(module)
    explicit = module.AirHandlingUnit(
        "AHU Explicit", sources["heatpump"], ID="SUP-AHU-EXPLICIT"
    )
    geothermal = module.AirHandlingUnit(
        "AHU Geothermal", sources["geothermal"], ID="SUP-AHU-GEO"
    )
    from_json = module.AirHandlingUnit.from_json(
        SimpleNamespace(
            id="SUP-AHU-JSON",
            name="AHU JSON",
            source_system_id="SRC-HEATPUMP",
        ),
        {"SRC-HEATPUMP": sources["heatpump"]},
    )
    dragon_sources = {
        sources["heatpump"].ID: sources["heatpump"].to_dragon(),
        sources["geothermal"].ID: sources["geothermal"].to_dragon(),
    }
    dragon_explicit = explicit.to_dragon(dragon_sources)
    dragon_geothermal = geothermal.to_dragon(dragon_sources)
    explicit.source = sources["geothermal"]
    return {
        "base_classes": [base.__name__ for base in module.AirHandlingUnit.__bases__],
        "explicit_after_source_mutation": _supply_snapshot(explicit),
        "geothermal": _supply_snapshot(geothermal),
        "from_json": _supply_snapshot(from_json),
        "dragon_explicit": _dragon_supply_snapshot(dragon_explicit),
        "dragon_geothermal": _dragon_supply_snapshot(dragon_geothermal),
        "dragon_repeat_fresh": from_json.to_dragon(
            {sources["heatpump"].ID: sources["heatpump"].to_dragon()}
        )
        is not from_json.to_dragon(
            {sources["heatpump"].ID: sources["heatpump"].to_dragon()}
        ),
        "errors": {
            "boiler_source": _exception(
                lambda: module.AirHandlingUnit(
                    "Bad AHU", sources["boiler"], ID="SUP-AHU-BAD"
                )
            ),
            "from_json_missing_source": _exception(
                lambda: module.AirHandlingUnit.from_json(
                    SimpleNamespace(
                        id="SUP-AHU-MISSING",
                        name="AHU Missing",
                        source_system_id="SRC-MISSING",
                    ),
                    {},
                )
            ),
            "none_source": _exception(
                lambda: module.AirHandlingUnit(
                    "Bad AHU", None, ID="SUP-AHU-NONE"
                )
            ),
            "to_dragon_missing_source": _exception(
                lambda: from_json.to_dragon({})
            ),
        },
    }


def _electric_radiant_floor_facts(module: ModuleType) -> dict[str, Any]:
    explicit = module.ElectricRadiantFloor(
        "Electric Floor Explicit", ID="SUP-ERF-EXPLICIT"
    )
    from_json = module.ElectricRadiantFloor.from_json(
        SimpleNamespace(id="SUP-ERF-JSON", name="Electric Floor JSON"),
        {},
    )
    dragon = explicit.to_dragon({})
    return {
        "base_classes": [
            base.__name__ for base in module.ElectricRadiantFloor.__bases__
        ],
        "explicit": _supply_snapshot(explicit),
        "from_json": _supply_snapshot(from_json),
        "none_source_singleton_identity": (
            explicit.source is from_json.source is module.NoneSource()
        ),
        "dragon": _dragon_supply_snapshot(dragon),
        "dragon_repeat_fresh": explicit.to_dragon({}) is not explicit.to_dragon({}),
        "errors": {
            "positional_id": _exception(
                lambda: module.ElectricRadiantFloor(
                    "Bad Electric Floor", "SUP-ERF-POSITIONAL"
                )
            ),
            "from_json_missing_id": _exception(
                lambda: module.ElectricRadiantFloor.from_json(
                    SimpleNamespace(name="Missing ID"),
                    {},
                )
            ),
        },
    }


def _electric_radiator_facts(module: ModuleType) -> dict[str, Any]:
    default = module.ElectricRadiator(
        "Electric Radiator Default", ID="SUP-ER-DEFAULT"
    )
    explicit = module.ElectricRadiator(
        "Electric Radiator Explicit", 2_500.0, ID="SUP-ER-EXPLICIT"
    )
    from_json_default = module.ElectricRadiator.from_json(
        SimpleNamespace(id="SUP-ER-JSON-DEFAULT", name="ER JSON Default"),
        {},
    )
    from_json_explicit = module.ElectricRadiator.from_json(
        SimpleNamespace(
            id="SUP-ER-JSON-EXPLICIT",
            name="ER JSON Explicit",
            capacity_heating=3_100.0,
        ),
        {},
    )
    explicit.capacity = 3_000
    return {
        "base_classes": [
            base.__name__ for base in module.ElectricRadiator.__bases__
        ],
        "default": _supply_snapshot(default, ("capacity",)),
        "explicit_mutated": _supply_snapshot(explicit, ("capacity",)),
        "from_json_default": _supply_snapshot(
            from_json_default, ("capacity",)
        ),
        "from_json_explicit": _supply_snapshot(
            from_json_explicit, ("capacity",)
        ),
        "dragon_default": _dragon_supply_snapshot(default.to_dragon({})),
        "dragon_explicit": _dragon_supply_snapshot(explicit.to_dragon({})),
        "none_source_singleton_identity": (
            default.source is explicit.source is module.NoneSource()
        ),
        "errors": {
            "capacity_negative": _exception(
                lambda: module.ElectricRadiator(
                    "Bad ER", -1, ID="SUP-ER-NEGATIVE"
                )
            ),
            "capacity_string": _exception(
                lambda: module.ElectricRadiator(
                    "Bad ER", "2500", ID="SUP-ER-STRING"
                )
            ),
            "capacity_zero": _exception(
                lambda: module.ElectricRadiator(
                    "Bad ER", 0, ID="SUP-ER-ZERO"
                )
            ),
        },
    }


def _fan_coil_unit_facts(module: ModuleType) -> dict[str, Any]:
    sources = _make_sources(module)
    branch_keys = ("boiler", "district", "chiller", "absorption")
    branches = []
    for key in branch_keys:
        source = sources[key]
        supply = module.FanCoilUnit(
            "FCU " + key,
            source,
            ID="SUP-FCU-" + key.upper(),
        )
        converted = supply.to_dragon({source.ID: source.to_dragon()})
        branches.append(
            {
                "branch": key,
                "dragon": _dragon_supply_snapshot(converted),
                "python": _supply_snapshot(supply),
            }
        )
    from_json = module.FanCoilUnit.from_json(
        SimpleNamespace(
            id="SUP-FCU-JSON",
            name="FCU JSON",
            source_system_id="SRC-BOILER",
        ),
        {"SRC-BOILER": sources["boiler"]},
    )
    return {
        "base_classes": [base.__name__ for base in module.FanCoilUnit.__bases__],
        "branches": branches,
        "from_json": _supply_snapshot(from_json),
        "errors": {
            "from_json_missing_source": _exception(
                lambda: module.FanCoilUnit.from_json(
                    SimpleNamespace(
                        id="SUP-FCU-MISSING",
                        name="FCU Missing",
                        source_system_id="SRC-MISSING",
                    ),
                    {},
                )
            ),
            "heatpump_source": _exception(
                lambda: module.FanCoilUnit(
                    "Bad FCU",
                    sources["heatpump"],
                    ID="SUP-FCU-BAD",
                )
            ),
            "to_dragon_missing_source": _exception(
                lambda: from_json.to_dragon({})
            ),
        },
    }


def _packaged_air_conditioner_facts(module: ModuleType) -> dict[str, Any]:
    default = module.PackagedAirConditioner(
        "PAC Default", ID="SUP-PAC-DEFAULT"
    )
    explicit = module.PackagedAirConditioner(
        "PAC Explicit",
        4.2,
        8_500.0,
        ID="SUP-PAC-EXPLICIT",
    )
    from_json_default = module.PackagedAirConditioner.from_json(
        SimpleNamespace(id="SUP-PAC-JSON-DEFAULT", name="PAC JSON Default"),
        {},
    )
    from_json_explicit = module.PackagedAirConditioner.from_json(
        SimpleNamespace(
            id="SUP-PAC-JSON-EXPLICIT",
            name="PAC JSON Explicit",
            cop_cooling=3.8,
            capacity_cooling=9_250.0,
        ),
        {},
    )
    explicit.cop = 4.5
    explicit.capacity = 9_000
    dragon_sources: dict[str, Any] = {}
    dragon_first = explicit.to_dragon(dragon_sources)
    first_count = len(dragon_sources)
    first_dedicated = next(iter(dragon_sources.values()))
    dragon_second = explicit.to_dragon(dragon_sources)
    return {
        "base_classes": [
            base.__name__ for base in module.PackagedAirConditioner.__bases__
        ],
        "default": _supply_snapshot(default, ("cop", "capacity")),
        "explicit_mutated": _supply_snapshot(explicit, ("cop", "capacity")),
        "from_json_default": _supply_snapshot(
            from_json_default, ("cop", "capacity")
        ),
        "from_json_explicit": _supply_snapshot(
            from_json_explicit, ("cop", "capacity")
        ),
        "dragon_first": _dragon_supply_snapshot(dragon_first),
        "dragon_second": _dragon_supply_snapshot(dragon_second),
        "dedicated_source": _source_link(first_dedicated),
        "source_dict_count_after_first": _typed(first_count),
        "source_dict_count_after_second": _typed(len(dragon_sources)),
        "source_dict_values_distinct": len({id(item) for item in dragon_sources.values()})
        == len(dragon_sources),
        "none_source_singleton_identity": (
            default.source is explicit.source is module.NoneSource()
        ),
        "errors": {
            "capacity_negative": _exception(
                lambda: module.PackagedAirConditioner(
                    "Bad PAC", capacity=-1, ID="SUP-PAC-NEGATIVE"
                )
            ),
            "capacity_string": _exception(
                lambda: module.PackagedAirConditioner(
                    "Bad PAC", capacity="8500", ID="SUP-PAC-STRING"
                )
            ),
            "capacity_zero": _exception(
                lambda: module.PackagedAirConditioner(
                    "Bad PAC", capacity=0, ID="SUP-PAC-ZERO"
                )
            ),
            "cop_none_setter": _exception(
                lambda: _setattr(explicit, "cop", None)
            ),
            "cop_zero": _exception(
                lambda: module.PackagedAirConditioner(
                    "Bad PAC", cop=0, ID="SUP-PAC-COP-ZERO"
                )
            ),
        },
    }


def _radiant_floor_facts(module: ModuleType) -> dict[str, Any]:
    sources = _make_sources(module)
    boiler = module.RadiantFloor(
        "Radiant Floor Boiler",
        sources["boiler"],
        ID="SUP-RF-BOILER",
    )
    district = module.RadiantFloor(
        "Radiant Floor District",
        sources["district"],
        ID="SUP-RF-DISTRICT",
    )
    from_json = module.RadiantFloor.from_json(
        SimpleNamespace(
            id="SUP-RF-JSON",
            name="Radiant Floor JSON",
            source_system_id="SRC-BOILER",
        ),
        {"SRC-BOILER": sources["boiler"]},
    )
    return {
        "base_classes": [base.__name__ for base in module.RadiantFloor.__bases__],
        "boiler": _supply_snapshot(boiler),
        "district": _supply_snapshot(district),
        "from_json": _supply_snapshot(from_json),
        "dragon_boiler": _dragon_supply_snapshot(
            boiler.to_dragon(
                {sources["boiler"].ID: sources["boiler"].to_dragon()}
            )
        ),
        "dragon_district": _dragon_supply_snapshot(
            district.to_dragon(
                {sources["district"].ID: sources["district"].to_dragon()}
            )
        ),
        "errors": {
            "chiller_source": _exception(
                lambda: module.RadiantFloor(
                    "Bad RF",
                    sources["chiller"],
                    ID="SUP-RF-BAD",
                )
            ),
            "from_json_missing_source": _exception(
                lambda: module.RadiantFloor.from_json(
                    SimpleNamespace(
                        id="SUP-RF-MISSING",
                        name="RF Missing",
                        source_system_id="SRC-MISSING",
                    ),
                    {},
                )
            ),
            "to_dragon_missing_source": _exception(
                lambda: from_json.to_dragon({})
            ),
        },
    }


def _radiator_facts(module: ModuleType) -> dict[str, Any]:
    sources = _make_sources(module)
    default = module.Radiator(
        "Radiator Default",
        sources["boiler"],
        ID="SUP-RAD-DEFAULT",
    )
    explicit = module.Radiator(
        "Radiator Explicit",
        sources["district"],
        5_500.0,
        ID="SUP-RAD-EXPLICIT",
    )
    from_json_default = module.Radiator.from_json(
        SimpleNamespace(
            id="SUP-RAD-JSON-DEFAULT",
            name="Radiator JSON Default",
            source_system_id="SRC-BOILER",
        ),
        {"SRC-BOILER": sources["boiler"]},
    )
    from_json_explicit = module.Radiator.from_json(
        SimpleNamespace(
            id="SUP-RAD-JSON-EXPLICIT",
            name="Radiator JSON Explicit",
            source_system_id="SRC-DISTRICT",
            capacity_heating=6_250.0,
        ),
        {"SRC-DISTRICT": sources["district"]},
    )
    explicit.capacity = 6_000
    return {
        "base_classes": [base.__name__ for base in module.Radiator.__bases__],
        "default": _supply_snapshot(default, ("capacity",)),
        "explicit_mutated": _supply_snapshot(explicit, ("capacity",)),
        "from_json_default": _supply_snapshot(
            from_json_default, ("capacity",)
        ),
        "from_json_explicit": _supply_snapshot(
            from_json_explicit, ("capacity",)
        ),
        "dragon_default": _dragon_supply_snapshot(
            default.to_dragon(
                {sources["boiler"].ID: sources["boiler"].to_dragon()}
            )
        ),
        "dragon_explicit": _dragon_supply_snapshot(
            explicit.to_dragon(
                {sources["district"].ID: sources["district"].to_dragon()}
            )
        ),
        "errors": {
            "capacity_negative": _exception(
                lambda: module.Radiator(
                    "Bad Radiator",
                    sources["boiler"],
                    -1,
                    ID="SUP-RAD-NEGATIVE",
                )
            ),
            "capacity_string": _exception(
                lambda: module.Radiator(
                    "Bad Radiator",
                    sources["boiler"],
                    "5500",
                    ID="SUP-RAD-STRING",
                )
            ),
            "capacity_zero": _exception(
                lambda: module.Radiator(
                    "Bad Radiator",
                    sources["boiler"],
                    0,
                    ID="SUP-RAD-ZERO",
                )
            ),
            "chiller_source": _exception(
                lambda: module.Radiator(
                    "Bad Radiator",
                    sources["chiller"],
                    ID="SUP-RAD-CHILLER",
                )
            ),
            "to_dragon_missing_source": _exception(
                lambda: from_json_default.to_dragon({})
            ),
        },
    }


def _supply_system_facts(module: ModuleType) -> dict[str, Any]:
    cls = module.SupplySystem
    mapper = cls.TYPE_MAPPER
    sources = _make_sources(module)

    class Probe(cls):
        _heatable_sources = [module.HeatPump]
        _coolable_sources = [module.Chiller]

        def __init__(self, source: Any) -> None:
            self.source = source

    mapper_rows = [
        {
            "base_is_supply_system": issubclass(mapped, cls),
            "key": key,
            "module": mapped.__module__,
            "type": mapped.__name__,
        }
        for key, mapped in mapper.items()
    ]
    copied = dict(mapper)
    copied["probe-only"] = Probe
    base = cls()
    return {
        "base_classes": [base_class.__name__ for base_class in cls.__bases__],
        "base_instance_dictionary_empty": vars(base) == {},
        "base_property_errors": {
            "coolable": _exception(lambda: base.coolable),
            "heatable": _exception(lambda: base.heatable),
        },
        "declared_public_members": sorted(
            name for name in cls.__dict__ if not name.startswith("_")
        ),
        "mapper_copy_mutation_preserves_original": "probe-only" not in mapper,
        "mapper_identity_across_accesses": cls.TYPE_MAPPER is mapper,
        "mapper_keys": list(mapper),
        "mapper_rows": mapper_rows,
        "mapper_type": type(mapper).__name__,
        "probe_exact_type_behavior": {
            "chiller": {
                "coolable": Probe(sources["chiller"]).coolable,
                "heatable": Probe(sources["chiller"]).heatable,
            },
            "geothermal_subclass": {
                "coolable": Probe(sources["geothermal"]).coolable,
                "heatable": Probe(sources["geothermal"]).heatable,
            },
            "heatpump": {
                "coolable": Probe(sources["heatpump"]).coolable,
                "heatable": Probe(sources["heatpump"]).heatable,
            },
        },
        "property_descriptors": {
            "coolable": type(cls.__dict__["coolable"]).__name__,
            "heatable": type(cls.__dict__["heatable"]).__name__,
        },
        "subclasses": [
            subclass.__name__
            for subclass in cls.__subclasses__()
            if subclass.__module__ == module.__name__
        ],
    }


def _execute_cases(module: ModuleType) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _air_handling_unit_facts(module),
        EXPECTED_CASE_IDS[1]: _electric_radiant_floor_facts(module),
        EXPECTED_CASE_IDS[2]: _electric_radiator_facts(module),
        EXPECTED_CASE_IDS[3]: _fan_coil_unit_facts(module),
        EXPECTED_CASE_IDS[4]: _packaged_air_conditioner_facts(module),
        EXPECTED_CASE_IDS[5]: _radiant_floor_facts(module),
        EXPECTED_CASE_IDS[6]: _radiator_facts(module),
        EXPECTED_CASE_IDS[7]: _supply_system_facts(module),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("HVAC supply-system observation order drifted.")
    return observations


def _runtime_receipt() -> dict[str, Any]:
    receipt = dict(BASE._runtime_receipt())
    receipt["supply_system_support"] = {
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
        raise SystemExit("Pinned HVAC supply-system support drifted.")


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
        raise SystemExit("Pinned HVAC supply-system native review drifted.")
    return result


def _coverage_by_symbol() -> dict[str, str]:
    result: dict[str, str] = {}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol] = definition["id"]
    if set(result) != set(TARGET_SYMBOLS):
        raise RuntimeError("HVAC supply-system symbol coverage drifted.")
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
        / "hvac-supply-system-work"
    )
    work_root.mkdir(parents=True, exist_ok=True)

    with BASE._isolated_import(imported_root, inventory["raw"]) as primary:
        module, loaded_modules = primary
        signatures = _runtime_signatures(module)
        observations = _execute_cases(module)

    with tempfile.TemporaryDirectory(
        prefix="epsimple-hvac-supply-system-relocation-", dir=work_root
    ) as temporary:
        relocated_root = Path(temporary) / "src"
        BASE._copy_source_tree(imported_root, relocated_root)
        with BASE._isolated_import(relocated_root, inventory["raw"]) as relocated:
            relocated_module, relocated_modules = relocated
            relocated_signatures = _runtime_signatures(relocated_module)
            relocated_observations = _execute_cases(relocated_module)

    if signatures != relocated_signatures:
        raise RuntimeError("HVAC supply-system signatures changed after relocation.")
    if observations != relocated_observations:
        raise RuntimeError("HVAC supply-system observations changed after relocation.")
    if loaded_modules != relocated_modules:
        raise RuntimeError("HVAC supply-system loaded modules changed after relocation.")

    signatures_hash = canonical_sha256(signatures)
    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned HVAC supply-system signatures drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and modules_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned HVAC supply-system loaded modules drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Pinned HVAC supply-system relocation observations drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned HVAC supply-system fact hashes drifted.\n"
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
            "Pinned HVAC supply-system case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned HVAC supply-system aggregate case hash drifted.")

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
        raise RuntimeError("HVAC supply-system oracle root key set drifted.")
    if value["schema"] != SCHEMA:
        raise RuntimeError("HVAC supply-system schema drifted.")
    if value["target_receipts"] != list(TARGET_RECEIPTS):
        raise RuntimeError("HVAC supply-system target receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("HVAC supply-system symbol descriptors drifted.")
    target_hash = canonical_sha256(value["target_receipts"])
    if EXPECTED_TARGET_RECEIPTS_SHA256 and target_hash != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise RuntimeError("Pinned HVAC supply-system target receipt hash drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("HVAC supply-system runtime signatures are absent.")
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise RuntimeError("Pinned HVAC supply-system runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(signatures):
        raise RuntimeError("HVAC supply-system consumer contract drifted.")
    if value["runtime"] != _runtime_receipt():
        raise RuntimeError("HVAC supply-system runtime receipt drifted.")
    if value["native_review"] != _native_review():
        raise RuntimeError("HVAC supply-system native review drifted.")

    upstream = value["upstream"]
    if not isinstance(upstream, dict) or set(upstream) != {
        "adjacent_receipts_sha256",
        "commit",
        "inventory",
        "isolated_import",
        "source",
        "target_receipts_sha256",
    }:
        raise RuntimeError("HVAC supply-system upstream key set drifted.")
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
            raise RuntimeError(f"HVAC supply-system upstream field drifted: {key}")
    if upstream["target_receipts_sha256"] != canonical_sha256(value["target_receipts"]):
        raise RuntimeError("HVAC supply-system upstream target receipt hash drifted.")
    if (
        EXPECTED_ADJACENT_RECEIPTS_SHA256
        and upstream["adjacent_receipts_sha256"]
        != EXPECTED_ADJACENT_RECEIPTS_SHA256
    ):
        raise RuntimeError("Pinned HVAC supply-system adjacent receipt hash drifted.")
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
        raise RuntimeError("HVAC supply-system isolated-import key set drifted.")
    if (
        isolated["source_location_count"] != 2
        or isolated["epsimple_package_initializer_executed"]
        or isolated["epsimple_core_initializer_executed"]
        or isolated["relocated_source_copy"]
        != "byte-identical-epsimple-and-idragon-trees"
    ):
        raise RuntimeError("HVAC supply-system relocation claim drifted.")
    loaded = isolated["loaded_local_modules"]
    if (
        not isinstance(loaded, list)
        or isolated["loaded_local_modules_sha256"] != canonical_sha256(loaded)
    ):
        raise RuntimeError("HVAC supply-system loaded-module receipt drifted.")
    if (
        EXPECTED_LOADED_LOCAL_MODULES_SHA256
        and canonical_sha256(loaded) != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise RuntimeError("Pinned HVAC supply-system loaded modules drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and isolated["relocated_observations_sha256"]
        != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise RuntimeError("Pinned HVAC supply-system relocation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("HVAC supply-system case count drifted.")
    if [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("HVAC supply-system case order drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        if set(case) != {*definition, "python"}:
            raise RuntimeError(
                f"HVAC supply-system case key set drifted: {definition['id']}"
            )
        for key, expected in definition.items():
            if case[key] != expected:
                raise RuntimeError(
                    f"HVAC supply-system case definition drifted: {definition['id']}"
                )
        python = case["python"]
        if (
            not isinstance(python, dict)
            or set(python) != {"facts", "facts_sha256", "outcome"}
            or python["outcome"] != "observed"
        ):
            raise RuntimeError(
                f"HVAC supply-system Python observation drifted: {definition['id']}"
            )
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(
                f"HVAC supply-system inline fact hash drifted: {definition['id']}"
            )
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("HVAC supply-system fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned HVAC supply-system fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("HVAC supply-system case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned HVAC supply-system case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("HVAC supply-system aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned HVAC supply-system aggregate hash drifted.")
    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("HVAC supply-system exact target closure drifted.")
    closure = value["consumer_contract"]["closure"]
    if (
        closure["target_indices"] != list(TARGET_INDICES)
        or closure["adjacent_indices"] != list(ADJACENT_INDICES)
        or sorted((*closure["target_indices"], *closure["adjacent_indices"]))
        != list(SOURCE_INDICES)
    ):
        raise RuntimeError("HVAC supply-system full source closure drifted.")
    BASE._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("HVAC supply-system strict JSON round trip drifted.")


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
        f"Wrote {len(oracle['cases'])} HVAC supply-system cases covering "
        f"{len(TARGET_RECEIPTS)} declarations: {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception, aggregate {oracle['cases_sha256']}."
    )


if __name__ == "__main__":
    main()


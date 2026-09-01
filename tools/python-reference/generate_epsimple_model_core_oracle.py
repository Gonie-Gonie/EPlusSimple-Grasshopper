"""Generate the pinned EPlusSimple GreenRetrofitModel behavior oracle.

This corpus executes exactly 35 unresolved public declarations from
``src/epsimple/core/model.py``.  The three representation/Excel declarations
retain their existing out-of-scope decisions, while the fourteen
``GreenRetrofitResult`` declarations remain a separate deferred slice.

The upstream module is imported from two byte-identical source locations so
that its package-relative weather resources cannot silently bind observations
to one checkout.  EnergyPlus itself is never started: ``run`` is observed with
an instrumented IDF/result boundary.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
from copy import deepcopy
from datetime import datetime
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
from types import SimpleNamespace
from typing import Any, Callable, Iterator


SCHEMA = "dragons.python-reference.epsimple-model-core.v1"
SOURCE_PATH = "src/epsimple/core/model.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_BYTES = 36_949
EXPECTED_SOURCE_SHA256 = (
    "sha256:71dc9bb8d97e829c27d9b5d19ef88709af9613f9e53f60807d54ceb2922e4532"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:f79918272c07515ee4ae98fa62f4ca5d5d703e5e2faa334f72d6a6966e1e2447"
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
        "_dragons_epsimple_model_support", SUPPORT_PATH
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

WEATHER_RESOURCES = (
    {
        "bytes": 16_318,
        "path": "epsimple/_data/weather/기후지역.csv",
        "sha256": "sha256:a6949a4b3bc967aefc419f64b1da2b7180fd33a333fed0951560951831614c06",
    },
    {
        "bytes": 38_455,
        "path": "epsimple/_data/weather/행정구역별기상데이터.csv",
        "sha256": "sha256:ec667eeb0ade076272d23f89956add7b0f0ec7eeac6106c02a1c9c4888aa788e",
    },
)
MODEL_RESOURCE = {
    "bytes": 8_900,
    "path": "examples/grm/ASHRAE 140 modified.grm",
    "sha256": "sha256:4dd307475207fd57599b43b99be22ab1c1d740c3e5a8a9d39e8ee0e30476257a",
}

_TARGET_ROWS = (
    (337, "ADDR_WEATHER_TABLE", "constant", "1a4029a135d1255a90f77a8b0d319de7e17b4fe70d3114e41485553ecb5a6a80", "9f383055f56d9882c11b76be1d5194f91e2adcb2dbda17bf2b8322d34cd24eac", "87be4b5360ff3816bcda24ff61d639a3676702dae470cde8f721d0159bc9edac"),
    (338, "CLIMATE_TABLE", "constant", "fbfb5af8a7a829546e3cb9f84e035197804aa31b036e26af7862470a9eaf0760", "9d72cb0728cc3438db822e424cfcaf72503250c37ed46d29ff07d7d47f61bb54", "c343b539809241a3a55d0844f1aae7808bfe49eba6db271e28816324925dc14b"),
    (339, "EnergyPlusError", "class", "3ed100420cd0b15df6d11143072a05f60220d7dd87deb1e6ead6689ba4a7624e", "2dfacdb4c9959692a46d69591cb908159e533d0f532cc3ce13e9f07b34c1d85e", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (340, "EnergyPlusError.__init__", "function", "328cf73b77278a305f62b23b960bb7658c9ae9be4c4a5fca9331e3a7fc4cdfd0", "496d71b65d95a84986bec0f9ba7b3e2c715cc5edf0e22c40646df768c35b1d57", "d48c948af596d4efa0f55e52b63173ea141319e3a0dde8d755407d153349d52f"),
    (341, "GreenRetrofitModel", "class", "fb39a800c06d7513705dab266bd7221e147db1e976750b1204e9ebb467819cbe", "5261e2e3084f19f2da8304e608772d3764169f327fb214b843a351145fdb3201", "3505a6963aa2c06f8069c0f0b3b26be49d723cd2918ad6182f3258a5ce39f907"),
    (342, "GreenRetrofitModel.__init__", "function", "e8bd64b762879f6d0c79c0375457ac1644cc80f3d9994ec3a0c9d124625bb6c0", "09565d746abf49d633aabd75a994e05b279342356edcf1c46662ad06080d5bfe", "12ba4fa08d66617798365a14cd091da28bc3a5d25e47482246a8375f4139018b"),
    (345, "GreenRetrofitModel.address", "function", "df3586867cd69bd15d94f3fc4e1ae11c51f449d80096f5f31cb57f47f3296744", "7b3f43294bb435e01776f384415ffa537d4314ed93dbf93b0a73eb3ad7b79acf", "ca04f161957043a71cf2d7aef8482c92dc4bc230ae112cb7208f7f60a9a66241"),
    (346, "GreenRetrofitModel.area", "function", "bf31ed3c8d4218b3847c9509ed651f88c3220ebe435c0d74fe97b4a150d232eb", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "830ef332868981d50d5ba31582760e8e3e5047621055fc826de0cdcdadadf967"),
    (347, "GreenRetrofitModel.averaged_exteriorfloor_Uvalue", "function", "ef752eff55173cdf6098698760d7dec5481712fe27aa80a764b8967674b301e6", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "515e61cdebf396a5322ebede54f36ed0295d88f2db2a87082466d131336d7e4f"),
    (348, "GreenRetrofitModel.averaged_exteriorroof_Uvalue", "function", "871c1b932e6e641da7c28392514c2b6cc464ca9c925f2a0a1b9165177f20e726", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "a7156006e43808607d0fcc9f5a62816a1e2cbd423889081bdc0739e02bdc3326"),
    (349, "GreenRetrofitModel.averaged_exteriorwall_Uvalue", "function", "13f93b869df9add4d157aadf79b05bd22c453ff7fa29c70dbb08ba9ceb6423a2", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "bc39d98071026e251a16b4949bff063852b95496a3dab9b988cd7d6a7d5a9c25"),
    (350, "GreenRetrofitModel.averaged_infiltration", "function", "4046cce9884dda1034f8adc0fa0b9c4ad98173939b77c121f968af70d0893c65", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "acca6a62062fb29953e384edc34af51cf5ed625c1cb4e1353d676ed143a9a321"),
    (351, "GreenRetrofitModel.averaged_lightdensity", "function", "695c215a8b62739fe7b603b73fef0754a8d0622b9dd5efb6ea0e3b10aa065dfe", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "11a031a6dc531092bdd7bfced5a8fb16378d26a76c93be227e6b5a770ffb4891"),
    (352, "GreenRetrofitModel.averaged_window_Uvalue", "function", "235f45ccdb9970dc4448855a6e267a140b34f00b3b7f11d895a1f97ef2797d53", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "8ce5bbae495287e41f2703d7c4f1e177006f290b3787d24ab3e85573316774db"),
    (353, "GreenRetrofitModel.climate", "function", "27c207a5aa410a7847269e6a6b669e12c4fda738f1370cdbf4ae3d651f1901d0", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "9523cdd9eae3fb7552c579ea34634ab40f825d56d7e9a47cebfe3e7ceaaf4dd8"),
    (354, "GreenRetrofitModel.exteriorfloors", "function", "613333060251b890b716889f9b4c182e6a7bbd40fdcb3560d5b52cacf6df0e3e", "175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea", "450eee32ed8baece5e0556f18a5dfe5ba10aac11f9c67a3109d8ba25f3e181ba"),
    (355, "GreenRetrofitModel.exteriorroofs", "function", "9ba0cb6303c1e95763c7d1aa68959647034ac0b546f6e879b19418725a7866bd", "175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea", "cf9fe02e3e0cf4d4f13817e729eda0ccece46957bf6049312aec47bdccde2ab5"),
    (356, "GreenRetrofitModel.exteriorwalls", "function", "428acddc7bc811e7d4021f3f473a53fd2a7b4f65b9fb44125dcd711510df72f7", "175c75a451212fe0099b1206d31f4f11195e5716bdbae2c993097a86e669a0ea", "901879974bf33621331356eaa6a16d46cc4071a2b28741e608b091a73bc95004"),
    (357, "GreenRetrofitModel.exteriorwindows", "function", "d363d7173bf7b1ff6b7128b2276c6422151662aaf73a9a1ddf01e0c8f44a267a", "6ed2bf44ec68a9cda9c9305419f2564c10dcf9ffa3541254a942a64ef21bd2d4", "296bb1a735405edbd0c11bb041657ac0f5270156a47aa53cf17b3c313ccb6fce"),
    (359, "GreenRetrofitModel.from_grjson", "function", "696d04c33af170f7372dae59b38e53b0e68580e89bac8b8a762cd3e683fdafe7", "383773b6e47f5110bbf73dc5762cbf0c8a112394d47deffff47a2e002feb416d", "ae6d620b7d16d12e89a3570a713da137e875294b51136ac971e41518997b0e2d"),
    (360, "GreenRetrofitModel.get_unique_fenestration_constructions", "function", "0963ad7196e27f0227bdb56e8b72c2779a190a07b1063a9b8adf9378530a212c", "fd4e9c1ad3ff2824e64eafe498a14908ba218ecc7baa8ded8a3f316c2591de2d", "32e438d2f79eab5f8118743beb45acf39f8dcb71a2f99fbb1c9e706c631fb2cd"),
    (361, "GreenRetrofitModel.get_unique_materials", "function", "ecb20cb3d82efa5c493a99fa5863d3060531cabca284a696fe8e38a922ac2ee9", "abfea80c9449326669efd9f474c37f074a049c0392c4c2198e2d3f911222f3e8", "a6810b28394f3548417f171684081a938dfa38b802ad1c823418e017a3a660c9"),
    (362, "GreenRetrofitModel.get_unique_profiles", "function", "13af13a19a65f0b8d091ec2f86720c47249e9c0cc5e1d860393be68e569f3a1d", "67df893862c0302efa08b30b163a056d97569f3b93a8dce10cebdf1272f393c4", "f5b326faed39b53be9e1eecdff83ca98c4a30443df5a86a66d1b47b768b95ebb"),
    (363, "GreenRetrofitModel.get_unique_surface_constructions", "function", "a05748b1a1da92805d794a2eb233350ba2e1f0ce61356bb67200ad1c758dc22e", "44f5e0b78dc39ab2968e7e7670a41af6cefc43abeb6b6647ed67a92ab7537d38", "39b4d79a80ff49bad4a4245a7373bfa0b7f410a260b6e41364c808e5ec423b70"),
    (364, "GreenRetrofitModel.north_axis", "function", "fc0d665a25029abe7f854951352d805a206225bd43a338d3f37b0108deb74166", "01a32dc9a269f3e3447e6af35076197a0c8ba205cdfb6e0330079f9b8c4dd8aa", "2e72804ae3e080dc8b42cf8ef7d8adbb2dbb60c6a384b6eed385f7832be69402"),
    (365, "GreenRetrofitModel.run", "function", "bf192ec837aebea5fb0f8ee5990899fd3a75f2fc818048a4ae1a5cf32f18a675", "51ddf9e4cf458b2de9ce6c9725a7c7a2cbb7249015a6dd8373ab1cd5c3e78d29", "a2a869cc1a8f14ef0eafb7fb40becf3539f654c47897bc57ef7d79b454aa116b"),
    (366, "GreenRetrofitModel.source_system", "function", "b2b62b8010ff62705e00918b972890132cf2c864442747f93973f70036a990de", "ecf0515fba0e7757fdd360ee89bbe791a2e1ca561618d704a9be05356bab4f3f", "54eb05e80df84e18a44ee8dae7a3891ec4eb27e0d6af1fb1975ef38eb05eadfd"),
    (367, "GreenRetrofitModel.terrain", "function", "152775fe99a35584281e0312c9216be0aee3bfe76565c049822a296dbb001cfa", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "3acd594cc04467fd73e657fce8e9ddea9fdd1b1827182009c1069daab0c7b800"),
    (368, "GreenRetrofitModel.to_dragon", "function", "5e2e21f3341cc365406d4b00ab7d6ad7fe09125d1866cdf0e3f92aefbd1beaae", "a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b", "156c2a5aebc08703836b870b75187441fea57cce0b51c95efb6b99454738cd9e"),
    (369, "GreenRetrofitModel.to_idf", "function", "e8d26d7207e0d5eb131f29d9ff7c5b37d61b2a494748106c9e60038f55820a20", "a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b", "280e2b48b0efc0dc069bdab75256882099683fb2291b8a1eff9b91859a80187c"),
    (370, "GreenRetrofitModel.vintage", "function", "e739b9d68aa3c20da6ed71d509299b8a6ee7b7d8309e4a9fda254efc65de1f8a", "b301fb283058f2355db7d2ba8b45324adf1418587837bba7cc58bb0dafe68310", "70a7b7756b467f40d28f0cffe8db7dae61b739cf663d6f2f9aec6d5fe957a621"),
    (371, "GreenRetrofitModel.weather", "function", "acd72fe86b0be527be1e08f77bd22b94dd50c7bfcfbb2a2ec56b984f1e030f2a", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "e37fbecf8c76ff119b2e0389f0f53d2716952a6b6594627429dea8927174f1d4"),
    (372, "GreenRetrofitModel.weather_filepath", "function", "fa174585e6b6a0b08679787056e604e2f229a3da568c53aa9adc71e6afd36722", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "b3c6cd4617882c157ff1f2f8c6fd4b45bb102b680445c9b5a6a4107dfea8194c"),
    (387, "InvalidAddressError", "class", "aee12b8f2a2a21c18d4124e0f56b1dd5fce979942d7ece00b5f24d4d659532dc", "eec23128512a858c49eb69a5b02fc2540567dce31954ec653d3fb6889b760256", "921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a"),
    (388, "address_to_weather", "function", "6e86f5469559d6a7149e2ccef4b88deebc9a88c6c952d08955ea835465add983", "e7ae5ada01411afa2e22b34f55b2942d22555d266ad0092f2cc024bf092281a2", "1aef1106ab5dc74bc6e64f77155d0b07775dae8fe3c8b5817dadc3515fd8e774"),
)

_EXCLUDED_ROWS = (
    (343, "GreenRetrofitModel.__repr__", "function", "544b402cf2914882d8ed743d50403b1e01ad47743a9e1530587738fac0f12693", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "c296481c206fe665df151a1948cb07a623d2138929295dbf3e054f811a5b814a"),
    (344, "GreenRetrofitModel.__str__", "function", "39c346fc45abacc4f01d1d0e4e33233c9e458c8a81ee0e8b2ed426cfa9d057d3", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "201b429fd2fab612d920f2fcfada48e931debb1621cbe0a09a91ee0ec29b6e14"),
    (358, "GreenRetrofitModel.from_excel", "function", "46935cc1aaff18b83281df944eb9f099d53c5894f7427ee98fedb8dccefdc206", "383773b6e47f5110bbf73dc5762cbf0c8a112394d47deffff47a2e002feb416d", "b89287289cf4e9e9894cc40227ada1f3a1e91d4420662fbdfd8cc441f4a24221"),
)

_DEFERRED_ROWS = (
    (373, "GreenRetrofitResult", "class", "8b4073860c0a5ec5215658188d0e02cbdd83c2e792e35fc1de93180d2b76e2e0", "ad17da15ebe3f9a8b13f618e3a7d4d8a5d867b8573aab129f9bc0758c0449792", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (374, "GreenRetrofitResult.VALID_DIGITS", "constant", "ff1cddacd1d221d604e80997d48ef03662bbeb531c45337abde8fcc3f9fc30df", "aa336779f69a8902021215ad36bc8925e1d599b84b1c2149a383d3313065b1a2", "ddcc9e26678f237b5f7892c086072a5962980b4d4b13bcee47bd9c0d98a52cc6"),
    (375, "GreenRetrofitResult.__init__", "function", "856dd66b378dc69ca9fdf702af477ca308850afa30e1f79ddaf07c77007d2143", "e3ea637489f15196a395d06b8784e4240a686044f045de1addec871f7ee124b0", "7d8dee39517322f67931eb9ae4eeab47423ca33acb4bd9d48732687b11009213"),
    (376, "GreenRetrofitResult.area", "function", "37a89b1c8b8b29e09038b198162ad3edfe11206794c9b30e104febcdce483f89", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "7335d117f821d4cc789535e20d1f1cb563895a2e27b6fcdbe9c5bf3a1978d037"),
    (377, "GreenRetrofitResult.calc_domestic_hotwater_site_energy", "function", "4e80e0ef21caa93b8a0d7450676b1173677faec1ac8f3d15ad550f290b920c4c", "01ce55e2ae511cb78ed4504c328bc6d4e06786c1bbe7157feb8bd6958d2a5ede", "3d20f42d58aa292c0cda8f36c2c29aba9fcb94cb3a65fe138eed6a7d40fcb26d"),
    (378, "GreenRetrofitResult.get_dhw_servers", "function", "a63f6fa21523147d50860abe9915f96111ca6ace3621e57716040c9f8cc22ff3", "d2b4c877c3074459e858c8ddab98b4b507ad32ac856cab0c0358b2ff4487fce6", "757d1859c51226b31facdfb68107b5a90ce8e7c8d260e6ccb327e31f9203183c"),
    (379, "GreenRetrofitResult.get_domestic_hotwater_energy", "function", "b7774317313c4c32bb28168900a4ccd0af9162b9e9149f7bb58f5605784ed592", "c2d47451050e60f15a22d16146acba292a2a641fff5670ab1cec00ba7f863d58", "d43efb9ead93c11dacb01c2a869c6801e637018483c9524b390640381d1e0eb8"),
    (380, "GreenRetrofitResult.summarize", "function", "93d2bbd846d5cf13baf88fcbacddc16e948ca205b53c7e4f25fd5887dcdc3f87", "808df99bb5631c7829bf7bce92d37533bbddbb2e35281ff3add1b89d35acbab7", "c2c71105186ffc370ee09c436ac894ee6bf797989dd622d36f302633f6009b6e"),
    (381, "GreenRetrofitResult.to_co2", "function", "72b97e85ef6741a8eb2dfcdb37de2a27b37772b2ec054fee14a061d3a3f2d358", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "73699d8b52634390a3efab78dceae92be86d304fb90fbc8acc4c6092b0a2f0e6"),
    (382, "GreenRetrofitResult.to_cost", "function", "7d1d1cd964d4ab0842510bf94bac7aea393ed53469ed7ecdea1d7979057bf266", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "3363e164857a1bc4c9f5f2e9904602b4d9912b9901888e2f5e55197c4c993f30"),
    (383, "GreenRetrofitResult.to_dict", "function", "010fb59959bd7ec395c6e22acccaeb73626df3fa276c4fb7e5ed1c3172a8f8d3", "b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf", "ff7f831331299a45e9c62ac55581b0c4dc6d311580a9abc84e73b53e2763324b"),
    (384, "GreenRetrofitResult.to_site_uses", "function", "48114e1462753ab48eac6ca7d648438ad7e4381d4900cdbfd7618c701562bafa", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "b8a49f1b2b0bcbaf6c27042f1b6926bdd6954194a3db29531bdd8668d4052b7f"),
    (385, "GreenRetrofitResult.to_source_uses", "function", "842eb853a7216a84eab7ccc5a04d7454fc7f2572ea9c8e0bc32f73d6ffc84291", "3a410f05d904cd573f15bd094908c64f55a72f6a804b455f752cf4d0a298d3ef", "d9c7d1b27a50ae9b04a5278c1d1881309fc297af097af411791f2f1d77e73d5d"),
    (386, "GreenRetrofitResult.write", "function", "67ef521c2bdac4646a52e20ba8da306765197f8cc27846cb9d715d605d21db2e", "5294543e03913904c918f3367755b0cffe7f63c47d17de87fcd55fa0a846c288", "be074b70585f464b6e6172733e6fa39c8f8d94e716eddc77260516689568c898"),
)


def _receipts(rows: tuple[tuple[Any, ...], ...]) -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "body_hash": "sha256:" + body_hash,
            "inventory_index": index,
            "kind": kind,
            "path": SOURCE_PATH,
            "signature_hash": "sha256:" + signature_hash,
            "symbol": symbol,
            "symbol_hash": "sha256:" + symbol_hash,
        }
        for index, symbol, kind, symbol_hash, signature_hash, body_hash in rows
    )


TARGET_RECEIPTS = _receipts(_TARGET_ROWS)
EXCLUDED_RECEIPTS = _receipts(_EXCLUDED_ROWS)
DEFERRED_RECEIPTS = _receipts(_DEFERRED_ROWS)
TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
EXCLUDED_SYMBOLS = tuple(item["symbol"] for item in EXCLUDED_RECEIPTS)
DEFERRED_SYMBOLS = tuple(item["symbol"] for item in DEFERRED_RECEIPTS)
TARGET_HASHES = {item["symbol"]: item["symbol_hash"] for item in TARGET_RECEIPTS}

EQUIVALENT_SYMBOLS = {
    "GreenRetrofitModel.area",
    "GreenRetrofitModel.averaged_infiltration",
    "GreenRetrofitModel.climate",
    "GreenRetrofitModel.exteriorfloors",
    "GreenRetrofitModel.exteriorroofs",
    "GreenRetrofitModel.exteriorwalls",
    "GreenRetrofitModel.from_grjson",
    "GreenRetrofitModel.north_axis",
    "GreenRetrofitModel.terrain",
    "GreenRetrofitModel.vintage",
    "GreenRetrofitModel.weather",
}
CLASSIFICATIONS = {
    symbol: "equivalent" if symbol in EQUIVALENT_SYMBOLS else "exception"
    for symbol in TARGET_SYMBOLS
}

_ADAPTATION_BASES = {
    "ADDR_WEATHER_TABLE": "typed-packaged-weather-database-rather-than-mutable-dataframe",
    "CLIMATE_TABLE": "typed-date-indexed-weather-database-rather-than-mutable-dataframe",
    "EnergyPlusError": "structured-diagnostics-rather-than-throwing-table-wrapper",
    "EnergyPlusError.__init__": "energyplus-failure-and-result-builder-diagnostics",
    "GreenRetrofitModel": "immutable-floor-and-catalog-aggregate-rather-than-mutable-zone-list",
    "GreenRetrofitModel.__init__": "immutable-defensive-copy-constructor-with-explicit-weather",
    "GreenRetrofitModel.address": "readonly-address-with-explicit-weather-selection",
    "GreenRetrofitModel.averaged_exteriorfloor_Uvalue": "nullable-construction-filter-rather-than-singleton-identity-regulation",
    "GreenRetrofitModel.averaged_exteriorroof_Uvalue": "nullable-construction-filter-rather-than-singleton-identity-regulation",
    "GreenRetrofitModel.averaged_exteriorwall_Uvalue": "nullable-construction-filter-rather-than-singleton-identity-regulation",
    "GreenRetrofitModel.averaged_lightdensity": "nullable-light-density-excluded-from-weight-denominator",
    "GreenRetrofitModel.averaged_window_Uvalue": "native-window-projection-also-includes-glass-doors",
    "GreenRetrofitModel.exteriorwindows": "native-window-projection-also-includes-glass-doors",
    "GreenRetrofitModel.get_unique_fenestration_constructions": "explicit-validated-model-catalog-rather-than-derived-overwrite-map",
    "GreenRetrofitModel.get_unique_materials": "explicit-validated-model-catalog-rather-than-derived-overwrite-map",
    "GreenRetrofitModel.get_unique_profiles": "database-resolved-zone-profiles-rather-than-derived-overwrite-map",
    "GreenRetrofitModel.get_unique_surface_constructions": "explicit-validated-model-catalog-rather-than-derived-overwrite-map",
    "GreenRetrofitModel.run": "async-runner-and-result-builder-diagnostic-boundary",
    "GreenRetrofitModel.source_system": "immutable-explicit-catalog-rather-than-computed-plus-unvalidated-merge",
    "GreenRetrofitModel.to_dragon": "nonthrowing-aggregate-conversion-result-with-diagnostics",
    "GreenRetrofitModel.to_idf": "native-idf-document-conversion-result-with-diagnostics",
    "GreenRetrofitModel.weather_filepath": "epw-filename-with-caller-owned-directory-resolution",
    "InvalidAddressError": "lookup-diagnostic-rather-than-address-exception",
    "address_to_weather": "typed-nonthrowing-weather-selection-result",
}
ADAPTATIONS = {
    symbol: f"{base}-{TARGET_HASHES[symbol][7:15]}"
    for symbol, base in _ADAPTATION_BASES.items()
}
ASSERTION_IDS = {
    item["symbol"]: f"epsimple-model-core-{item['inventory_index']}-{item['symbol_hash'][7:15]}"
    for item in TARGET_RECEIPTS
}


def _native_route(symbol: str) -> str:
    if symbol in {"ADDR_WEATHER_TABLE", "CLIMATE_TABLE", "InvalidAddressError", "address_to_weather"}:
        return "Dragons.SimpleDragon.WeatherDatabase.FindByAddress(string?, DateTime) and Dragons.SimpleDragon.WeatherSelection"
    if symbol.startswith("EnergyPlusError"):
        return "Dragons.EnergyPlus.Runtime.EnergyPlusFailure and Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)"
    if symbol == "GreenRetrofitModel.from_grjson":
        return "Dragons.SimpleDragon.GrmReader.ReadFile(string, SimpleDragonDatabase?)"
    if symbol == "GreenRetrofitModel.to_dragon":
        return "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)"
    if symbol == "GreenRetrofitModel.to_idf":
        return "Dragons.SimpleDragon.GreenRetrofitConverter.ToIdfDocument(GreenRetrofitModel, GreenRetrofitConversionOptions?, IddSchema?, EnergyModelIdfOptions?)"
    if symbol == "GreenRetrofitModel.run":
        return "Dragons.EnergyPlus.Runtime.EnergyPlusRunner.RunAsync(EnergyPlusRunRequest, CancellationToken) and Dragons.SimpleDragon.GreenRetrofitResultBuilder.Build(GreenRetrofitModel, EnergyPlusSimulationResult, GreenRetrofitResultBuildOptions?)"
    if symbol == "GreenRetrofitModel.source_system":
        return "Dragons.SimpleDragon.GreenRetrofitModel.SourceSystems and Dragons.SimpleDragon.GreenRetrofitModel.SupplySystems"
    if symbol == "GreenRetrofitModel.weather_filepath":
        return "Dragons.SimpleDragon.WeatherSelection.EpwFileName and ResolveEpwPath(string)"
    member = {
        "GreenRetrofitModel.address": "Address",
        "GreenRetrofitModel.area": "Area",
        "GreenRetrofitModel.averaged_exteriorfloor_Uvalue": "AverageExteriorFloorUValue",
        "GreenRetrofitModel.averaged_exteriorroof_Uvalue": "AverageExteriorRoofUValue",
        "GreenRetrofitModel.averaged_exteriorwall_Uvalue": "AverageExteriorWallUValue",
        "GreenRetrofitModel.averaged_infiltration": "AverageInfiltration",
        "GreenRetrofitModel.averaged_lightdensity": "AverageLightDensity",
        "GreenRetrofitModel.averaged_window_Uvalue": "AverageWindowUValue",
        "GreenRetrofitModel.climate": "Weather.ClimateRegion",
        "GreenRetrofitModel.exteriorfloors": "ExteriorFloors",
        "GreenRetrofitModel.exteriorroofs": "ExteriorRoofs",
        "GreenRetrofitModel.exteriorwalls": "ExteriorWalls",
        "GreenRetrofitModel.exteriorwindows": "ExteriorWindows",
        "GreenRetrofitModel.get_unique_fenestration_constructions": "FenestrationConstructions",
        "GreenRetrofitModel.get_unique_materials": "Materials",
        "GreenRetrofitModel.get_unique_profiles": "Zones with SimpleDragonDatabase.Profiles",
        "GreenRetrofitModel.get_unique_surface_constructions": "SurfaceConstructions",
        "GreenRetrofitModel.north_axis": "NorthAxis",
        "GreenRetrofitModel.terrain": "Weather.Terrain",
        "GreenRetrofitModel.vintage": "Vintage",
        "GreenRetrofitModel.weather": "Weather.WeatherLocation",
    }
    if symbol in {"GreenRetrofitModel", "GreenRetrofitModel.__init__"}:
        return "Dragons.SimpleDragon.GreenRetrofitModel constructor"
    if symbol in member:
        return "Dragons.SimpleDragon.GreenRetrofitModel." + member[symbol]
    raise RuntimeError(f"No reviewed native route for {symbol}.")


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}

PREFIX = "epsimple-model-core."
CASE_SPECS = (
    ("T01", "weather-table-topology", "weather", ("ADDR_WEATHER_TABLE", "CLIMATE_TABLE"), ()),
    ("A01", "address-weather-resolution-and-failure", "weather", ("InvalidAddressError", "address_to_weather"), ("ADDR_WEATHER_TABLE", "CLIMATE_TABLE")),
    ("E01", "energyplus-error-formatting", "error", ("EnergyPlusError", "EnergyPlusError.__init__"), ()),
    ("M01", "model-constructor-fundamental-properties", "model", ("GreenRetrofitModel", "GreenRetrofitModel.__init__", "GreenRetrofitModel.address", "GreenRetrofitModel.climate", "GreenRetrofitModel.north_axis", "GreenRetrofitModel.terrain", "GreenRetrofitModel.vintage", "GreenRetrofitModel.weather", "GreenRetrofitModel.weather_filepath"), ("address_to_weather",)),
    ("P01", "area-and-exterior-projections", "projection", ("GreenRetrofitModel.area", "GreenRetrofitModel.exteriorfloors", "GreenRetrofitModel.exteriorroofs", "GreenRetrofitModel.exteriorwalls", "GreenRetrofitModel.exteriorwindows"), ()),
    ("W01", "weighted-averages-and-zero-cases", "projection", ("GreenRetrofitModel.averaged_exteriorfloor_Uvalue", "GreenRetrofitModel.averaged_exteriorroof_Uvalue", "GreenRetrofitModel.averaged_exteriorwall_Uvalue", "GreenRetrofitModel.averaged_infiltration", "GreenRetrofitModel.averaged_lightdensity", "GreenRetrofitModel.averaged_window_Uvalue"), ()),
    ("S01", "source-system-dedup-and-explicit-merge", "model", ("GreenRetrofitModel.source_system",), ()),
    ("U01", "unique-catalog-projections", "projection", ("GreenRetrofitModel.get_unique_fenestration_constructions", "GreenRetrofitModel.get_unique_materials", "GreenRetrofitModel.get_unique_profiles", "GreenRetrofitModel.get_unique_surface_constructions"), ()),
    ("J01", "grjson-full-graph-and-adjacency-allocation", "serialization", ("GreenRetrofitModel.from_grjson",), ()),
    ("C01", "dragon-and-idf-conversion", "conversion", ("GreenRetrofitModel.to_dragon", "GreenRetrofitModel.to_idf"), ()),
    ("R01", "instrumented-run-success-and-failure", "runtime", ("GreenRetrofitModel.run",), ("EnergyPlusError",)),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 11

# Filled after the observation surface is finalized, then required fail-closed.
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:bfa68f55261e8500cfe19f4692189e34f7b5572aeb72e8ef1e0babc541445bff"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:998782cc65bc94d43ffc7538fae747639503f673586bc2815aaddac4dddc1fe1"
)
EXPECTED_RELOCATION_SNAPSHOT_SHA256 = (
    "sha256:311a666c7b67b8cd0fdd272362a33538c4a6dad6c35e7164ccf8b2f5c51204ab"
)
EXPECTED_FACT_SHA256 = {
    "epsimple-model-core.address-weather-resolution-and-failure": "sha256:c4a51fa76ebb12f444c38b9226963bd896e061b20ffa6283f4df92afe18e490a",
    "epsimple-model-core.area-and-exterior-projections": "sha256:ff270cce43b17e56870f654764fa61cfb069867547d0caafcd1013346da10f19",
    "epsimple-model-core.dragon-and-idf-conversion": "sha256:07dded0daf3cf65f6d36031921359823ea75e209e6924df6b0460feea5fe29f4",
    "epsimple-model-core.energyplus-error-formatting": "sha256:fb6b2c39f7c7a69ece80c784ff87504299727a6f9e8ac29befb0651c415f3d4d",
    "epsimple-model-core.grjson-full-graph-and-adjacency-allocation": "sha256:646a852bcceca7aad45be6df9be0b77bbe531b4630c29e37dae4f2f8a90579cd",
    "epsimple-model-core.instrumented-run-success-and-failure": "sha256:404d9f442aa7385107127a5309d372a57c52d6a8d7fb0ee39ee51c1b7f7606e1",
    "epsimple-model-core.model-constructor-fundamental-properties": "sha256:d14a7bbabd527f0acd5f1898a17fb0e3c419cbd0cc185006525bddffcb2844d6",
    "epsimple-model-core.source-system-dedup-and-explicit-merge": "sha256:7f03eb6c6ea5fd9f5e093cea126f9983d5bf929f9e3ffc6d30e6c96b9222afae",
    "epsimple-model-core.unique-catalog-projections": "sha256:61a5d1b8914d16b779f3e56c34666624d370d308e66e3df17c5b15dc63e3ef50",
    "epsimple-model-core.weather-table-topology": "sha256:7c707fa287c1ed8e0acf67f7f6344a34358ec504945dc873c314f74adda922eb",
    "epsimple-model-core.weighted-averages-and-zero-cases": "sha256:1f4d9e053180a350462b0d2e8b46ba46b2beb0ad75fdd206219f117ac9ba5db8",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-model-core.address-weather-resolution-and-failure": "sha256:ccd017b52eb63068b3c42277715c00604ece54b41435586350b754db6cb8ceed",
    "epsimple-model-core.area-and-exterior-projections": "sha256:57ddb3d713362d7ff126361acacd97e33bf1742460bb03ff7a86cfaeb480c81f",
    "epsimple-model-core.dragon-and-idf-conversion": "sha256:8556889dff89e16cab492bbf820498bc13857e9e7e293ed70a5c85cef77d7969",
    "epsimple-model-core.energyplus-error-formatting": "sha256:940b5460270b4b6c5bba6f31f2c6b7ce2371e1efe75268660bd53ed6dd7bc106",
    "epsimple-model-core.grjson-full-graph-and-adjacency-allocation": "sha256:209359d9ed17d9da99e5788b05c91dd29260ad1c8c0ed11d972051f6f4b9eb0a",
    "epsimple-model-core.instrumented-run-success-and-failure": "sha256:178e2c718fe5cbe8951147d8fc16795fed65d4b3a5719ff4975f5e35e9e51c49",
    "epsimple-model-core.model-constructor-fundamental-properties": "sha256:12e64a711ecfa0734eb3d47e890a696344745fc62e8b7a7bbe951af228d1195f",
    "epsimple-model-core.source-system-dedup-and-explicit-merge": "sha256:23a3ab2d2234b2199f89ddb9c1d4d8338d6228fb56f23e28828bced7c80e0a38",
    "epsimple-model-core.unique-catalog-projections": "sha256:1f971ab2878da05a73e6e551d1a02cd2f1d48dc3dfe83d1c61a0c92c9deee066",
    "epsimple-model-core.weather-table-topology": "sha256:8c4c57200340370423cfba8ffa71ae82c0426242f035c7c2c2ef226cef1e0ad2",
    "epsimple-model-core.weighted-averages-and-zero-cases": "sha256:6713975ddac523afde1ddc797fc53a08cfbeede94413b70f77f68641a474c331",
}
EXPECTED_CASES_SHA256 = (
    "sha256:1f7ed658cc9dc6908c0c3bbb31fe4f61927bfbe8881e62af6d04cc66072f8fa1"
)

RAW_ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,}")
WINDOWS_PATH_PATTERN = re.compile(r"(?i)(?:^|[\s=:'\"])[a-z]:[\\/]")
POSIX_PATH_PATTERN = re.compile(r"(?:^|[\s=:'\"])/(?:home|tmp|users|var|private|mnt|workspace)(?:/|\\)", re.IGNORECASE)
GUID_PATTERN = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b")
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
            "code": code,
            "context_symbols": list(context),
            "id": PREFIX + slug,
            "subfamily": subfamily,
            "target_symbols": list(targets),
        }
        for code, slug, subfamily, targets, context in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Model-core case order drifted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Model-core targets are not an exact one-case partition.")
    declared = {
        symbol
        for definition in definitions
        for symbol in (*definition["target_symbols"], *definition["context_symbols"])
    }
    if not declared.issubset(set(TARGET_SYMBOLS)):
        raise RuntimeError("Model-core case context escaped the bounded target set.")
    if declared.intersection(EXCLUDED_SYMBOLS) or declared.intersection(DEFERRED_SYMBOLS):
        raise RuntimeError("An excluded or deferred declaration entered a model-core case.")
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
        {"content_sha256", "files", "schema", "scope_sha256", "summary", "symbols", "upstream_commit"},
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
        raise SystemExit("The EPlusSimple model source receipt drifted.")
    for receipt in (*TARGET_RECEIPTS, *EXCLUDED_RECEIPTS, *DEFERRED_RECEIPTS):
        index = receipt["inventory_index"]
        if value["symbols"][index] != _descriptor(receipt):
            raise SystemExit(f"Model-core inventory receipt drifted at index {index}.")
    source_symbols = [
        (index, item["symbol"])
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    bounded = sorted(
        (item["inventory_index"], item["symbol"])
        for item in (*TARGET_RECEIPTS, *EXCLUDED_RECEIPTS, *DEFERRED_RECEIPTS)
    )
    if source_symbols != bounded:
        raise SystemExit("The model source is not an exact target/OOS/deferred partition.")
    return {
        "content_sha256": aggregate,
        "deferred_receipts": list(DEFERRED_RECEIPTS),
        "excluded_receipts": list(EXCLUDED_RECEIPTS),
        "file": source_file,
        "files": value["files"],
        "symbols": [_descriptor(item) for item in TARGET_RECEIPTS],
        "target_receipts": list(TARGET_RECEIPTS),
    }


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    relative = Path(SOURCE_PATH).relative_to("src")
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
        raise SystemExit("The imported EPlusSimple model source drifted.")
    for receipt in WEATHER_RESOURCES:
        path = source_root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise SystemExit(f"Pinned weather resource drifted: {receipt['path']}")
    example = source_root.parent / MODEL_RESOURCE["path"]
    if (
        not example.is_file()
        or example.stat().st_size != MODEL_RESOURCE["bytes"]
        or sha256_file(example) != MODEL_RESOURCE["sha256"]
    ):
        raise SystemExit("Pinned GRM example resource drifted.")


def _clear_local_modules() -> None:
    for name in list(sys.modules):
        if name in {"epsimple", "idragon"} or name.startswith(("epsimple.", "idragon.")):
            sys.modules.pop(name, None)


@contextmanager
def _isolated_import(source_root: Path) -> Iterator[Any]:
    source_root = source_root.resolve()
    _validate_source_tree(source_root)
    saved = {
        name: module
        for name, module in sys.modules.items()
        if name in {"epsimple", "idragon"} or name.startswith(("epsimple.", "idragon."))
    }
    _clear_local_modules()
    sys.path.insert(0, str(source_root))
    try:
        module = importlib.import_module("epsimple.core.model")
        if Path(module.__file__).resolve() != source_root / Path(SOURCE_PATH).relative_to("src"):
            raise SystemExit("Imported epsimple.core.model did not resolve to pinned source.")
        yield module
    finally:
        _clear_local_modules()
        sys.modules.update(saved)
        try:
            sys.path.remove(str(source_root))
        except ValueError:
            pass


def _copy_source_tree(source_root: Path, relocated_root: Path) -> None:
    relocated_root.mkdir(parents=True)
    for package in ("epsimple", "idragon"):
        shutil.copytree(source_root / package, relocated_root / package)
    source_example = source_root.parent / MODEL_RESOURCE["path"]
    target_example = relocated_root.parent / MODEL_RESOURCE["path"]
    target_example.parent.mkdir(parents=True)
    shutil.copy2(source_example, target_example)


def _loaded_local_modules(source_root: Path, inventory: dict[str, Any]) -> list[dict[str, Any]]:
    files = {item["path"]: item for item in inventory["files"]}
    result: list[dict[str, Any]] = []
    for name, module in sorted(sys.modules.items()):
        if not (name in {"epsimple", "idragon"} or name.startswith(("epsimple.", "idragon."))):
            continue
        filename = getattr(module, "__file__", None)
        if not filename or Path(filename).suffix != ".py":
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
                "sha256": receipt["content_hash"],
            }
        )
    return result


def _runtime_member(module: Any, symbol: str) -> Any:
    value: Any = module
    for token in symbol.split("."):
        value = inspect.getattr_static(value, token)
    return value


def _runtime_signatures(module: Any) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for symbol in TARGET_SYMBOLS:
        value = _runtime_member(module, symbol)
        if isinstance(value, property):
            result[symbol] = {
                "getter": str(inspect.signature(value.fget)),
                "setter": None if value.fset is None else str(inspect.signature(value.fset)),
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
            shape = getattr(value, "shape", None)
            result[symbol] = {
                "shape": None if shape is None else list(shape),
                "type": f"{type(value).__module__}.{type(value).__name__}",
            }
    return result


def _number(value: int | float | bool) -> dict[str, Any]:
    if isinstance(value, bool):
        return {"kind": "bool", "value": value}
    if isinstance(value, int):
        return {"kind": "int", "value": str(value)}
    if math.isfinite(value):
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    return {
        "kind": "float-nonfinite",
        "value": "nan" if math.isnan(value) else ("positive-infinity" if value > 0 else "negative-infinity"),
    }


def _exception(operation: Callable[[], Any]) -> dict[str, Any]:
    try:
        operation()
    except BaseException as error:  # noqa: BLE001 - exact boundary is oracle data.
        message = str(error)
        return {
            "message_sha256": "sha256:" + hashlib.sha256(message.encode("utf-8")).hexdigest(),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned"}


def _sha_text(value: str) -> str:
    return "sha256:" + hashlib.sha256(value.encode("utf-8")).hexdigest()


def _first_address(module: Any) -> str:
    value = module.ADDR_WEATHER_TABLE.index[0]
    if not isinstance(value, str) or not value:
        raise RuntimeError("Weather table did not expose a stable first administrative area.")
    return value


def _new_model(module: Any, **overrides: Any) -> Any:
    values = {
        "name": "Model Core Probe",
        "address": _first_address(module),
        "vintage": [2001, 2, 3],
        "is_multifamily_housing": False,
        "north_axis": 15,
        "zone": [],
        "pv": [],
    }
    values.update(overrides)
    return module.GreenRetrofitModel(**values)


def _table_facts(module: Any) -> dict[str, Any]:
    address = module.ADDR_WEATHER_TABLE
    climate = module.CLIMATE_TABLE
    return {
        "address_table": {
            "column_order": list(address.columns),
            "index_is_unique": bool(address.index.is_unique),
            "index_name": address.index.name,
            "shape": list(address.shape),
        },
        "climate_table": {
            "column_order": list(climate.columns),
            "columns_are_yyyymmdd": all(re.fullmatch(r"\d{8}", item) for item in climate.columns),
            "index_equals_address_table": bool(climate.index.equals(address.index)),
            "index_is_unique": bool(climate.index.is_unique),
            "index_name": climate.index.name,
            "shape": list(climate.shape),
        },
    }


def _address_facts(module: Any) -> dict[str, Any]:
    address = _first_address(module)
    observations = []
    for vintage in (datetime(2000, 1, 1), datetime(2020, 1, 1)):
        terrain, climate, weather, filepath = module.address_to_weather(
            address + " bounded suffix", vintage
        )
        path = Path(filepath)
        observations.append(
            {
                "climate": climate,
                "epw_filename": path.name,
                "path_parent_matches_declared_weather_directory": path.parent == module.Directory.WEATHER_DATA_DIR,
                "terrain": terrain,
                "vintage": vintage.date().isoformat(),
                "weather_location_sha256": _sha_text(weather),
            }
        )
    return {
        "address_sha256": _sha_text(address),
        "invalid_address": _exception(
            lambda: module.address_to_weather("synthetic invalid token", datetime(2020, 1, 1))
        ),
        "invalid_error_bases": [base.__name__ for base in module.InvalidAddressError.__bases__],
        "valid_observations": observations,
    }


def _energyplus_error_facts(module: Any) -> dict[str, Any]:
    frame = module.pd.DataFrame(
        [
            {"type": "Warning", "title": "warning-safe"},
            {"type": "Severe", "title": "severe-safe"},
            {"type": "Fatal", "title": "fatal-safe"},
        ]
    )
    filtered = module.EnergyPlusError(frame)
    missing = module.EnergyPlusError(None)
    return {
        "bases": [base.__name__ for base in module.EnergyPlusError.__bases__],
        "filtered_args": list(filtered.args),
        "filtered_message": str(filtered),
        "missing_args": list(missing.args),
        "missing_message": str(missing),
    }


def _constructor_facts(module: Any) -> dict[str, Any]:
    model = _new_model(module)
    resolved = module.address_to_weather(model.address, model.vintage)
    previous_address_hash = _sha_text(model.address)
    invalid_address = _exception(lambda: setattr(model, "address", "synthetic invalid token"))
    address_retained = _sha_text(model.address) == previous_address_hash

    vintage_from_list = _new_model(module)
    vintage_from_list.vintage = [2024, 5, 6]
    invalid_vintage = _exception(lambda: setattr(vintage_from_list, "vintage", ["bad"]))
    north_probes = []
    for value in (0, 359.999, 360, -1, "north"):
        probe = _new_model(module)
        outcome = _exception(lambda probe=probe, value=value: setattr(probe, "north_axis", value))
        north_probes.append(
            {
                "input": value if isinstance(value, str) else _number(value),
                "outcome": outcome,
                "stored": _number(probe.north_axis),
            }
        )

    defaults = module.GreenRetrofitModel.__init__.__defaults__
    if defaults is None or len(defaults) != 2:
        raise RuntimeError("GreenRetrofitModel mutable defaults drifted.")
    first = module.GreenRetrofitModel(
        "Alias A", _first_address(module), [2001, 1, 1], False, 0
    )
    second = module.GreenRetrofitModel(
        "Alias B", _first_address(module), [2001, 1, 1], False, 0
    )
    zone_marker = object()
    pv_marker = object()
    first.zone.append(zone_marker)
    first.pv.append(pv_marker)
    try:
        mutable_alias = {
            "default_pv_is_instance_list": defaults[1] is first.pv,
            "default_zone_is_instance_list": defaults[0] is first.zone,
            "pv_mutation_visible_in_second": pv_marker in second.pv,
            "same_pv_list": first.pv is second.pv,
            "same_zone_list": first.zone is second.zone,
            "zone_mutation_visible_in_second": zone_marker in second.zone,
        }
    finally:
        first.zone.remove(zone_marker)
        first.pv.remove(pv_marker)

    return {
        "address_invalid": invalid_address,
        "address_retained_after_failure": address_retained,
        "address_sha256": previous_address_hash,
        "class_module": type(model).__module__,
        "fundamentals": {
            "climate_equals_resolution": model.climate == resolved[1],
            "name": model.name,
            "north_axis": _number(model.north_axis),
            "terrain_equals_resolution": model.terrain == resolved[0],
            "vintage": model.vintage.date().isoformat(),
            "weather_equals_resolution": model.weather == resolved[2],
            "weather_file_basename": Path(model.weather_filepath).name,
        },
        "invalid_vintage": invalid_vintage,
        "mutable_default_alias": mutable_alias,
        "north_axis_probes": north_probes,
        "vintage_from_list": vintage_from_list.vintage.date().isoformat(),
    }


def _projection_model(module: Any) -> Any:
    material_a = module.Material("Projection A", 0.5, 1000, 800, ID="MAT-A")
    material_b = module.Material("Projection B", 0.25, 900, 900, ID="MAT-B")
    wall_a = module.SurfaceConstruction("Wall A", material_a, 0.1, ID="CON-WALL-A")
    wall_b = module.SurfaceConstruction("Wall B", material_b, 0.2, ID="CON-WALL-B")
    roof = module.SurfaceConstruction("Roof", material_a, 0.3, ID="CON-ROOF")
    floor = module.SurfaceConstruction("Floor", material_b, 0.4, ID="CON-FLOOR")
    glazing = module.FenestrationConstruction("Glazing", 1.2, 0.5, ID="CON-WIN")
    window = module.Window("Window", 2.0, glazing, ID="WIN-1")
    surfaces_one = [
        module.Surface("Wall one", "wall", "outdoors", 10.0, 0.0, wall_a, [window], ID="SURF-W1"),
        module.Surface("Wall two", "wall", "outdoors", 30.0, 90.0, wall_b, [], ID="SURF-W2"),
        module.Surface("Roof one", "ceiling", "outdoors", 40.0, None, roof, [], ID="SURF-R1"),
        module.Surface("Floor one", "floor", "ground", 40.0, None, floor, [], ID="SURF-F1"),
    ]
    surfaces_two = [
        module.Surface("Roof two", "ceiling", "outdoors", 20.0, None, roof, [], ID="SURF-R2"),
        module.Surface("Floor two", "floor", "outdoors", 20.0, None, floor, [], ID="SURF-F2"),
    ]
    profile_a = SimpleNamespace(ID="PROFILE-A")
    profile_b = SimpleNamespace(ID="PROFILE-B")
    zone_a = module.Zone(
        "Zone A", 3.0, surfaces_one, profile_a, 10.0, ID="ZONE-A"
    )
    zone_b = module.Zone(
        "Zone B", 4.0, surfaces_two, profile_b, None, ID="ZONE-B"
    )
    return _new_model(module, zone=[zone_a, zone_b])


def _projection_facts(module: Any) -> dict[str, Any]:
    model = _projection_model(module)
    return {
        "area": _number(model.area),
        "exterior_floor_ids": [item.ID for item in model.exteriorfloors],
        "exterior_roof_ids": [item.ID for item in model.exteriorroofs],
        "exterior_wall_ids": [item.ID for item in model.exteriorwalls],
        "exterior_window_ids": [item.ID for item in model.exteriorwindows],
        "window_projection_runtime_types": [type(item).__name__ for item in model.exteriorwindows],
    }


def _weighted_facts(module: Any) -> dict[str, Any]:
    model = _projection_model(module)
    zero = _new_model(module)
    names = (
        "averaged_exteriorfloor_Uvalue",
        "averaged_exteriorroof_Uvalue",
        "averaged_exteriorwall_Uvalue",
        "averaged_infiltration",
        "averaged_lightdensity",
        "averaged_window_Uvalue",
    )
    unknown = module.UnknownConstruction()
    return {
        "unknown_identity_comparison": {
            "fresh_constructor_is_same_singleton": unknown is module.UnknownConstruction(),
            "source_predicate_for_unknown": unknown is module.UnknownConstruction(),
        },
        "weighted": {name: _number(getattr(model, name)) for name in names},
        "zero": {name: _number(getattr(zero, name)) for name in names},
        "zone_inputs": [
            {
                "area": _number(zone.area),
                "height": _number(zone.height),
                "infiltration": _number(zone.infiltration),
                "light_density": None if zone.light_density is None else _number(zone.light_density),
            }
            for zone in model.zone
        ],
    }


def _source_system_facts(module: Any) -> dict[str, Any]:
    class Source:
        def __init__(self, identifier: str) -> None:
            self.ID = identifier

    first = Source("SRC-A")
    overwritten = Source("SRC-A")
    second = Source("SRC-B")
    none_source = module.NoneSource()
    supplies = [
        SimpleNamespace(source=first),
        SimpleNamespace(source=overwritten),
        SimpleNamespace(source=second),
        SimpleNamespace(source=none_source),
    ]
    zone = SimpleNamespace(supply_systems=supplies)
    model = _new_model(module, zone=[zone])
    invalid_object = object()
    model.source_system = [invalid_object, first]
    merged = model.source_system
    non_iterable = _exception(lambda: setattr(model, "source_system", 7))
    return {
        "computed_ids_after_last-write-dedup": [item.ID for item in merged if hasattr(item, "ID")],
        "computed_first_source_is_overwritten": merged[-2] is overwritten,
        "explicit_invalid_item_preserved": merged[0] is invalid_object,
        "explicit_source_duplicates_computed_source": merged[1] is first,
        "iterable_validation_short_circuited": True,
        "non_iterable_assignment": non_iterable,
        "none_source_excluded": none_source not in merged,
    }


def _unique_facts(module: Any) -> dict[str, Any]:
    model = _projection_model(module)
    return {
        "fenestration_construction_ids": list(model.get_unique_fenestration_constructions()),
        "material_ids": list(model.get_unique_materials()),
        "profile_ids": list(model.get_unique_profiles()),
        "surface_construction_ids": list(model.get_unique_surface_constructions()),
    }


def _example_path(source_root: Path) -> Path:
    return source_root.parent / MODEL_RESOURCE["path"]


def _modified_graph_payload(module: Any, source_root: Path) -> dict[str, Any]:
    payload = json.loads(_example_path(source_root).read_text(encoding="utf-8"))
    building = payload["building"]
    building["address"] = _first_address(module)
    zones = building["floors"][0]["zones"]
    original = zones[0]
    construction_id = payload["surface_constructions"][0]["id"]
    original["surfaces"].append(
        {
            "adjacent_zone_id": "ZONE-ADJ",
            "area": 8.0,
            "boundary_condition": "zone",
            "construction_id": construction_id,
            "fenestrations": [],
            "id": "SURF-ADJ",
            "name": "Adjacency probe",
            "type": "wall",
        }
    )
    zones.append(
        {
            "height": 2.7,
            "id": "ZONE-ADJ",
            "light_density": 5.0,
            "name": "Adjacent zone",
            "profile": original["profile"],
            "supply_system_ids": [],
            "surfaces": [
                {
                    "area": 12.0,
                    "boundary_condition": "ground",
                    "construction_id": construction_id,
                    "fenestrations": [],
                    "id": "SURF-ADJ-FLOOR",
                    "name": "Adjacent floor",
                    "type": "floor",
                }
            ],
            "ventilation_systems": [],
        }
    )
    boilers = building["source_systems"]["boiler"]
    unused = deepcopy(boilers[0])
    unused["id"] = "SRC-UNUSED"
    unused["name"] = "Unused source"
    boilers.append(unused)
    return payload


def _grjson_facts(module: Any, source_root: Path, work_root: Path) -> dict[str, Any]:
    payload = _modified_graph_payload(module, source_root)
    work_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="epsimple-model-grjson-", dir=work_root) as temporary:
        path = Path(temporary) / "bounded.grm"
        path.write_text(
            json.dumps(payload, ensure_ascii=False, separators=(",", ":")),
            encoding="utf-8",
            newline="\n",
        )
        model = module.GreenRetrofitModel.from_grjson(path)
    surfaces = {surface.ID: surface for zone in model.zone for surface in zone.surface}
    adjacent = surfaces["SURF-ADJ"]
    return {
        "adjacent_object_allocated": adjacent.adjacent_zone is not None,
        "adjacent_zone_id": adjacent.adjacent_zone.ID,
        "area": _number(model.area),
        "fenestration_count": sum(len(surface.fenestrations) for surface in surfaces.values()),
        "material_count": len(model.get_unique_materials()),
        "photovoltaic_count": len(model.pv),
        "source_system_ids": [item.ID for item in model.source_system],
        "surface_count": len(surfaces),
        "unused_source_preserved": any(item.ID == "SRC-UNUSED" for item in model.source_system),
        "zone_ids": [zone.ID for zone in model.zone],
    }


def _conversion_facts(module: Any, source_root: Path) -> dict[str, Any]:
    dragon_model = module.GreenRetrofitModel.from_grjson(_example_path(source_root)).to_dragon()
    idf = module.GreenRetrofitModel.from_grjson(_example_path(source_root)).to_idf()
    return {
        "dragon": {
            "conditioned_zone_count": len(dragon_model.conditioned_zones),
            "north_axis": _number(dragon_model.north_axis),
            "photovoltaic_count": len(dragon_model.pv),
            "runtime_type": f"{type(dragon_model).__module__}.{type(dragon_model).__name__}",
            "terrain": str(dragon_model.terrain),
            "zone_count": len(dragon_model.zone),
        },
        "idf": {
            "first_object_classes": list(idf.keys())[:8],
            "nonempty_object_class_count": sum(bool(value) for value in idf.values()),
            "object_class_count": len(idf),
            "object_count": sum(len(value) for value in idf.values()),
            "runtime_type": f"{type(idf).__module__}.{type(idf).__name__}",
            "version": list(idf.version),
        },
    }


def _run_facts(module: Any) -> dict[str, Any]:
    model = _new_model(module)

    class InstrumentedIdf:
        def __init__(self, result: Any) -> None:
            self.result = result
            self.calls: list[str] = []

        def run(self, weather_path: str) -> Any:
            self.calls.append(Path(weather_path).name)
            return self.result

    success_result = SimpleNamespace(tbl={"bounded": "table"}, err=None)
    success_idf = InstrumentedIdf(success_result)
    model.to_idf = lambda: success_idf
    wrapped = model.run()

    failure_model = _new_model(module)
    failure_frame = module.pd.DataFrame(
        [{"type": "Severe", "title": "instrumented-severe"}]
    )
    failure_idf = InstrumentedIdf(SimpleNamespace(tbl=None, err=failure_frame))
    failure_model.to_idf = lambda: failure_idf
    failure = _exception(failure_model.run)
    return {
        "energyplus_process_started": False,
        "failure": failure,
        "failure_calls": failure_idf.calls,
        "success": {
            "model_identity_retained": wrapped.model is model,
            "result_identity_retained": wrapped.result is success_result,
            "runtime_type": type(wrapped).__name__,
            "weather_calls": success_idf.calls,
        },
    }


def _execute_cases(module: Any, source_root: Path, work_root: Path) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _table_facts(module),
        EXPECTED_CASE_IDS[1]: _address_facts(module),
        EXPECTED_CASE_IDS[2]: _energyplus_error_facts(module),
        EXPECTED_CASE_IDS[3]: _constructor_facts(module),
        EXPECTED_CASE_IDS[4]: _projection_facts(module),
        EXPECTED_CASE_IDS[5]: _weighted_facts(module),
        EXPECTED_CASE_IDS[6]: _source_system_facts(module),
        EXPECTED_CASE_IDS[7]: _unique_facts(module),
        EXPECTED_CASE_IDS[8]: _grjson_facts(module, source_root, work_root),
        EXPECTED_CASE_IDS[9]: _conversion_facts(module, source_root),
        EXPECTED_CASE_IDS[10]: _run_facts(module),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("Model-core observation order drifted.")
    return observations


def _relocation_snapshot(module: Any, source_root: Path, work_root: Path) -> dict[str, Any]:
    address = _address_facts(module)
    graph = _grjson_facts(module, source_root, work_root)
    return {
        "address_table_shape": _table_facts(module)["address_table"]["shape"],
        "address_weather_observations": address["valid_observations"],
        "climate_table_shape": _table_facts(module)["climate_table"]["shape"],
        "graph_adjacency": {
            "adjacent_object_allocated": graph["adjacent_object_allocated"],
            "adjacent_zone_id": graph["adjacent_zone_id"],
            "surface_count": graph["surface_count"],
            "zone_ids": graph["zone_ids"],
        },
    }


def _dependencies() -> dict[str, str]:
    return {name: importlib.metadata.version(name) for name in EXPECTED_DEPENDENCIES}


def _runtime_receipt() -> dict[str, Any]:
    dependencies = _dependencies()
    return {
        "dependencies": dependencies,
        "dependencies_sha256": canonical_sha256(dependencies),
        "implementation": sys.implementation.name,
        "platform": sys.platform,
        "pointer_width_bits": struct.calcsize("P") * 8,
        "python_hash_algorithm": sys.hash_info.algorithm,
        "python_hash_seed": 0,
        "python_hash_width_bits": sys.hash_info.width,
        "python_version": ".".join(map(str, sys.version_info[:3])),
    }


def _validate_generation_runtime() -> None:
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for model-core generation.")
    if sys.platform != REQUIRED_PLATFORM or struct.calcsize("P") * 8 != REQUIRED_POINTER_WIDTH_BITS:
        raise SystemExit("The pinned 64-bit Windows Python runtime is required.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    if BOOTSTRAP_PATH.stat().st_size != EXPECTED_BOOTSTRAP_BYTES or sha256_file(BOOTSTRAP_PATH) != EXPECTED_BOOTSTRAP_SHA256:
        raise SystemExit("The Python reference bootstrap receipt drifted.")


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
        "classifications": CLASSIFICATIONS,
        "closure": {
            "deferred_greenretrofitresult_count": len(DEFERRED_RECEIPTS),
            "exact_one_case_target_partition": True,
            "full_source_partition": True,
            "out_of_scope_exclusion_count": len(EXCLUDED_RECEIPTS),
            "source_declaration_count": len(TARGET_RECEIPTS) + len(EXCLUDED_RECEIPTS) + len(DEFERRED_RECEIPTS),
            "target_count": len(TARGET_RECEIPTS),
        },
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "expected_receipt_count": len(TARGET_RECEIPTS),
            "full_grm_graph_claim": True,
            "full_idf_semantic_parity_claim": False,
            "python_behavior_oracle_only": True,
            "run_boundary_instrumented": True,
        },
        "expectations": expectations,
        "native_routes": NATIVE_ROUTES,
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
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
    work_root = repository_root / "temp" / "reference" / "model-core-work"
    work_root.mkdir(parents=True, exist_ok=True)

    with _isolated_import(imported_root) as module:
        signatures = _runtime_signatures(module)
        observations = _execute_cases(module, imported_root, work_root)
        primary_snapshot = _relocation_snapshot(module, imported_root, work_root)
        primary_modules = _loaded_local_modules(imported_root, inventory)

    with tempfile.TemporaryDirectory(prefix="epsimple-model-relocation-", dir=work_root) as temporary:
        relocated_root = Path(temporary) / "src"
        _copy_source_tree(imported_root, relocated_root)
        with _isolated_import(relocated_root) as relocated_module:
            relocated_signatures = _runtime_signatures(relocated_module)
            relocated_snapshot = _relocation_snapshot(relocated_module, relocated_root, work_root)
            relocated_modules = _loaded_local_modules(relocated_root, inventory)
    if signatures != relocated_signatures:
        raise RuntimeError("Model-core runtime signatures changed after relocation.")
    if primary_snapshot != relocated_snapshot:
        raise RuntimeError("Model-core package-relative observations changed after relocation.")
    if primary_modules != relocated_modules:
        raise RuntimeError("Model-core loaded module receipts changed after relocation.")
    signature_hash = canonical_sha256(signatures)
    module_hash = canonical_sha256(primary_modules)
    relocation_hash = canonical_sha256(primary_snapshot)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signature_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned model-core runtime signatures drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and module_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned model-core loaded-module receipt drifted.")
    if EXPECTED_RELOCATION_SNAPSHOT_SHA256 and relocation_hash != EXPECTED_RELOCATION_SNAPSHOT_SHA256:
        raise SystemExit("Pinned model-core relocation snapshot drifted.")

    fact_hashes = {identifier: canonical_sha256(facts) for identifier, facts in observations.items()}
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit("Pinned model-core fact hashes drifted.\n" + strict_json_dumps(fact_hashes, indent=2))
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
        raise SystemExit("Pinned model-core case hashes drifted.\n" + strict_json_dumps(case_hashes, indent=2))
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned model-core aggregate case hash drifted.")

    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(signatures),
        "deferred_receipts": inventory["deferred_receipts"],
        "excluded_receipts": inventory["excluded_receipts"],
        "fact_sha256": fact_hashes,
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "inventory_sha256": EXPECTED_INVENTORY_SHA256,
            "isolated_import": {
                "loaded_local_modules": primary_modules,
                "loaded_local_modules_sha256": module_hash,
                "relocation_snapshot_sha256": relocation_hash,
                "source_location_count": 2,
            },
            "model_resource": MODEL_RESOURCE,
            "path": SOURCE_PATH,
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
            "weather_resources": list(WEATHER_RESOURCES),
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
        if set(value) != {"kind", "value"} or not isinstance(token, str) or str(int(token)) != token:
            raise RuntimeError(f"Noncanonical int encoding at {location}.")
    elif kind == "float":
        if set(value) != {"hex", "kind", "repr"}:
            raise RuntimeError(f"Noncanonical float encoding at {location}.")
        parsed = float.fromhex(value["hex"])
        if not math.isfinite(parsed) or parsed.hex() != value["hex"] or repr(parsed) != value["repr"]:
            raise RuntimeError(f"Noncanonical finite float at {location}.")
    else:
        if set(value) != {"kind", "value"} or value["value"] not in {"nan", "positive-infinity", "negative-infinity"}:
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
        "case_sha256", "cases", "cases_sha256", "consumer_contract",
        "deferred_receipts", "excluded_receipts", "fact_sha256", "runtime",
        "schema", "symbols", "target_receipts", "upstream",
    }
    if not isinstance(value, dict) or set(value) != expected_keys:
        raise RuntimeError("Model-core oracle root key set drifted.")
    if value["schema"] != SCHEMA:
        raise RuntimeError("Model-core schema drifted.")
    if value["target_receipts"] != list(TARGET_RECEIPTS):
        raise RuntimeError("Model-core target receipts drifted.")
    if value["excluded_receipts"] != list(EXCLUDED_RECEIPTS):
        raise RuntimeError("Model-core OOS receipts drifted.")
    if value["deferred_receipts"] != list(DEFERRED_RECEIPTS):
        raise RuntimeError("Model-core deferred receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("Model-core symbol descriptors drifted.")
    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("Model-core runtime signatures are absent.")
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise RuntimeError("Model-core runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(signatures):
        raise RuntimeError("Model-core consumer contract drifted.")
    expected_runtime = {
        "dependencies": EXPECTED_DEPENDENCIES,
        "dependencies_sha256": canonical_sha256(EXPECTED_DEPENDENCIES),
        "implementation": "cpython",
        "platform": REQUIRED_PLATFORM,
        "pointer_width_bits": REQUIRED_POINTER_WIDTH_BITS,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }
    if value["runtime"] != expected_runtime:
        raise RuntimeError("Model-core runtime receipt drifted.")
    upstream = value["upstream"]
    if not isinstance(upstream, dict) or set(upstream) != {
        "commit", "inventory_sha256", "isolated_import", "model_resource",
        "path", "source", "weather_resources",
    }:
        raise RuntimeError("Model-core upstream key set drifted.")
    if upstream.get("commit") != EXPECTED_UPSTREAM_COMMIT or upstream.get("inventory_sha256") != EXPECTED_INVENTORY_SHA256:
        raise RuntimeError("Model-core upstream identity drifted.")
    isolated = upstream.get("isolated_import", {})
    if not isinstance(isolated, dict) or set(isolated) != {
        "loaded_local_modules", "loaded_local_modules_sha256",
        "relocation_snapshot_sha256", "source_location_count",
    }:
        raise RuntimeError("Model-core isolated-import key set drifted.")
    if isolated.get("source_location_count") != 2:
        raise RuntimeError("Model-core relocation count drifted.")
    loaded = isolated.get("loaded_local_modules")
    if not isinstance(loaded, list) or isolated.get("loaded_local_modules_sha256") != canonical_sha256(loaded):
        raise RuntimeError("Model-core loaded-module receipt drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and canonical_sha256(loaded) != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise RuntimeError("Pinned model-core loaded-module receipt drifted.")
    if EXPECTED_RELOCATION_SNAPSHOT_SHA256 and isolated.get("relocation_snapshot_sha256") != EXPECTED_RELOCATION_SNAPSHOT_SHA256:
        raise RuntimeError("Pinned model-core relocation receipt drifted.")
    expected_upstream_static = {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "model_resource": MODEL_RESOURCE,
        "path": SOURCE_PATH,
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
        "weather_resources": list(WEATHER_RESOURCES),
    }
    for key, expected in expected_upstream_static.items():
        if upstream.get(key) != expected:
            raise RuntimeError(f"Model-core upstream field drifted: {key}")
    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Model-core case count drifted.")
    if [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Model-core case order drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        if set(case) != {*definition, "python"}:
            raise RuntimeError(f"Model-core case key set drifted: {definition['id']}")
        for key, expected in definition.items():
            if case[key] != expected:
                raise RuntimeError(f"Model-core case definition drifted: {definition['id']}")
        python = case["python"]
        if set(python) != {"facts", "facts_sha256", "outcome"} or python["outcome"] != "observed":
            raise RuntimeError(f"Model-core Python observation drifted: {definition['id']}")
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(f"Model-core inline fact hash drifted: {definition['id']}")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Model-core fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned model-core fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("Model-core case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned model-core case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Model-core aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned model-core aggregate hash drifted.")
    counts = Counter(symbol for case in cases for symbol in case["target_symbols"])
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Model-core exact target closure drifted.")
    _validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Model-core strict JSON round trip drifted.")


def main() -> None:
    args = parse_args()
    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    oracle = build_oracle(inventory, args.upstream_commit)
    encoded = strict_json_dumps(oracle, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(encoded, encoding="utf-8", newline="\n")
    persisted = load_json_without_duplicates(args.output)
    validate_oracle(persisted)
    if args.output.read_text(encoding="utf-8") != encoded:
        raise SystemExit("Persisted model-core oracle is not byte-identical.")
    counts = Counter(CLASSIFICATIONS.values())
    print(
        f"Generated {args.output} with {len(TARGET_RECEIPTS)} targets, "
        f"{EXPECTED_CASE_COUNT} cases, {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception, and aggregate {oracle['cases_sha256']}."
    )


if __name__ == "__main__":
    main()

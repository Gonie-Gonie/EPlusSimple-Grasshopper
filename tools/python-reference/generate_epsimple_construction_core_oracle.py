"""Generate the pinned EPlusSimple construction-core behavior oracle.

This bounded corpus executes exactly the 48 unresolved declarations in
``src/epsimple/core/construction.py``.  Equality, hashing, representation,
and string formatting remain excluded by their existing scope decisions.
The module is imported without executing either EPlusSimple package
initializer, then imported again from a byte-identical relocated source tree.

Run through ``bootstrap_reference.py`` with CPython 3.12.7, the pinned
dependency root, and the pinned upstream ``src`` root.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
from datetime import datetime
import importlib
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import re
import shutil
import struct
import sys
import tempfile
from types import ModuleType, SimpleNamespace
from typing import Any, Callable, Iterator


SCHEMA = "dragons.python-reference.epsimple-construction-core.v1"
SOURCE_PATH = "src/epsimple/core/construction.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_BYTES = 25_902
EXPECTED_SOURCE_SHA256 = (
    "sha256:50b784d9c7ebd0df34fb6e524585482f04eb90ef915d5afd125fe779c0620816"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:fe40c8c89f2c3341ce4972976eabf96edd85ccba55a3a7619ca17e0a7603c0ab"
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
    "shapely": "2.0.6",
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
        "_dragons_epsimple_construction_support", SUPPORT_PATH
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

DATABASE_RESOURCES = (
    {
        "bytes": 141,
        "path": "epsimple/_data/construction/material.csv",
        "sha256": "sha256:e7186c4a29ddf1b91195ba86829e4ca49af1f4ee07c59377f6df3b83676614c8",
    },
    {
        "bytes": 105_194,
        "path": "epsimple/_data/construction/construction_regulation_surface.csv",
        "sha256": "sha256:db07a96bd3920ffeb1a2244f2d6bc9e42ea2c8c264143a393c22649c72d12cd7",
    },
    {
        "bytes": 27_190,
        "path": "epsimple/_data/construction/construction_regulation_fenestration.csv",
        "sha256": "sha256:5b452e853be1c2743f187d151fa424af049584a0968a60839d657e10e391b0c7",
    },
)

_TARGET_ROWS = (
    (75, "FenestrationConstruction", "class", "f86ec154c930aea35d523b661942aeacd316c9e32c35a2bdc8e4ed1920c3a268", "cb87381c343f05c6f8346f923f7afb9c9cd443f1e2028cc99da5168d77083f73", "640857b02eaa915d2e876dfa3dee2ab53d0f7f0ed597dd947898ebc33aceaa85"),
    (76, "FenestrationConstruction.ID", "function", "246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da"),
    (79, "FenestrationConstruction.__init__", "function", "929698258c4a4f66db33e0af63ae3a18875ec63ef839ee11f2ab576b68bbb226", "9c957347486df578c1a412a5ac8b80241c954f22af1f399bd07a4188ad989d89", "e037e38458d524e4ec9f4a8cb9cb0bf0d73e171b1ac5ea86c01a886951f0a561"),
    (82, "FenestrationConstruction.from_json", "function", "e3c4284e19789fa458c4f7028006d242d581cfbeb01ad51e32ab90551ce70b49", "4ae1a063d5a939388d8514904f2bb47bfa375f5663e6417ffbcf1adbb7f4eafc", "f084a8422268923699a7cd74201861ef5df649707ccd01047c3ad452d581d22f"),
    (83, "FenestrationConstruction.g", "function", "5025a060539ea2bacfb5ae8ca0ef4abf935192806a13b011d99fd6aa0c3abc64", "5be548e2358267a48bddf7dea8826b24eb8c8de0d48e34c711a4680b8a8e15a6", "e0cfbb22cc204ff9ac3d1af305e947f4f1f58b238db8dcb6788b5f0aa11725cd"),
    (84, "FenestrationConstruction.get_DB", "function", "87537fa63c5e0f3cde9a627c57b32a8a5ae49b260b9c825fb06e3496de97ea03", "7837dc0787c6943168ef7b15dcce28acfb339b8d8c4eb2414b77e2170c4c093f", "ea9e4f01d0dbf83886bf8781a6542e0e3e884ffb9bcb0cb2555623e46d92f127"),
    (85, "FenestrationConstruction.is_transparent", "function", "c288c4c2a8731544d809da7bb7fe3adff9840db7aa04b8643dd1f1fe2babee60", "4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "f2a67ebb532ed3a4d3d44d5c28c8b1a05a561f5aecf967467c4ee259d4277513"),
    (86, "FenestrationConstruction.load_DB", "function", "538b046541254bd26b1b836fb637eab7890c460827c6ff165ef323fa0dec7712", "2aab05b28f2be4842b498e7a9895c14027ba2cf8fcd9c703edd2edfb67beb5a9", "5a9cc8cad7dee5a920c5ce5177560f0797b7bcfa95073612540158a976d38d44"),
    (87, "FenestrationConstruction.to_dict", "function", "8aaf803cd6d01ff2cc451debf66067eb0e60477d77d883827f3020bae99583d9", "b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf", "5d6355f89eac171e3f9ac52635e256391f8ed47635c65bbc987d8dfc01aeeb95"),
    (88, "FenestrationConstruction.to_dragon", "function", "f430c29bc61a187b31291c85b06d137e3ed32f2ad010a35f9533b6a6411c7300", "039ac2a7ed7b396c983008cab8f6a7d0f0cfb2b21f690b70dc084432a67700af", "a0c31ebb91e6d254a93e305fb4633d1f21cae009bca27db874c9986b2bff302b"),
    (89, "FenestrationConstruction.u", "function", "72e986b60a5e934f22e643e067c8c6d3ff0566687f28a4391c30ea672b7dc59e", "6ac2ce8a5dd32fbfca88f5cc4f850b3cd6ec8ca6c4c109f6a54a910452616ad1", "f6e3f38c123beba4f955a651966255356bec6084f7fe9c8e351829ab2d728f84"),
    (90, "Material", "class", "590c4070c1bbecd72a1998debac7ba19ea479a4b6e5d72d46d7661ccbde3dcc3", "f374687b6dfa7d96b4b87d055f10b1c4045aef186851919903647548c74ae2bd", "26f7df3c6ed0243b4d2488006f12746099e9b2e607df21437c36c2740b2c79f8"),
    (91, "Material.ID", "function", "246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da"),
    (94, "Material.__init__", "function", "d909f4938dfd56e2f02cb784a8c1a4575d0d6815c54a7a8391e480b5719a8af4", "27e9b140de8f4387a42e2e9a20fb5ef8cf8afb7999e1a5f9dd6f05b9016d7cc4", "6d637a5b7c4f1d8fbc3e35eba05c7fb6a61d7728898d89575cd1b70faa8ea682"),
    (97, "Material.conductivity", "function", "b733b56b8a0acfcefc97c11b3fef116d8a1a5a29c847ed24e600839289383471", "68da20c9424bcb4ac2882491f00f8c9c26c63e331453583af45c04a260c45453", "f512da9e579d342352c80b0e5ceb0af993e59c64ac17ebd7f46067d0df112c94"),
    (98, "Material.density", "function", "231363247e3bc2f63cd6b88174bb6e3f732f56e00f0abab5bc9eeb69d2ef8893", "8d7e015ab764fc82bd4de0f7447db18903e71574e2ee810518866ea31f0700b7", "7a3173329ae1f0c334b6362b9c1c7cc7f1aaf20be9ddea3d34ebf29c9804cfe9"),
    (99, "Material.from_json", "function", "f2772e15f7943fb719b2db9d0bc97e691961d90139acae872084c91276fe1406", "859557d7ef1d1ee20d04460e3af41269329f1a5995085bcde40ee4e875892a1f", "e07a148de9f1bf1600fd6dd28834642a848c131353d1a8600776510ff0c4fd6a"),
    (100, "Material.get_DB", "function", "c3fc95014f2e9cbe4cb240bf46889f0c131a7fed6e3e80c2c57f5966ba77b71c", "1c0170941b1dc9bdbdb8f78b100e6ad58d3ee457dc588899989f93c365373f52", "c935bf2470ef9c64c197e667a35ae1effa91910f2a597eabe75c18dd911c10e9"),
    (101, "Material.load_DB", "function", "f6b330184181543879fccc96e2cb4dcaaa19f3dff71ff31f517b6a6057dcfd07", "2aab05b28f2be4842b498e7a9895c14027ba2cf8fcd9c703edd2edfb67beb5a9", "a4acc5bdd48943a016cb887345fc26dbe497e6736b0c089c26a203b9950603bd"),
    (102, "Material.specific_heat", "function", "abf4a2ea739fe17a9d04c787331534748bfd530f11baddf215ea17e5363f011b", "3f02e26053465c1d64093f2c803d1f146085da49b32a8561b50291b3df8fea37", "0580b0014f432929c0452ccb674124ad60495272b5976e1380bb90ae7aa21701"),
    (103, "Material.to_dict", "function", "7326bc5b45e32be2a4fdb1f4d39f48e9785d53c30722099eb42ec9452827d550", "b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf", "e5275bb5ad8aa3529930937c3c512160e5b0198167def32ae3cc5f9dbc587c4d"),
    (104, "Material.to_dragon", "function", "352f66b1f2326408f6cab7c60c79aa6532785b79dbcf65fa4690752b7e9eec40", "999a771847b5ee941930dc9cf8892f6f825752e23c111f99f1daca977843cbe8", "28c275cd02afb786ee19fb3d62e01ebf0a1380d65c8c7500a2c965093f56f0f4"),
    (105, "OpenConstruction", "class", "3257fd04fbfc06a4079ac1074912360608ad08cadc63a7cbf58c5ebcad62710c", "eec4fe727082e7e4036cee9a3410c9accc346bbdc3e9f723195de3497ab60259", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (106, "OpenConstruction.ID", "constant", "45236b5bd550caab28fcc2fdb4cda2c86f21e964e743baf6921dc64f400ded4c", "a6a138da8133245bb9a3e32e3a4ae215f462c394b5b212a2ddd2b1da87427145", "c808a2a0434fa42d093cee55008531829678b658c042818ea0d0f91e615e1f7d"),
    (107, "OpenConstruction.to_dragon", "function", "3f5ae9f074a1243720c69d1cfa0d773a82a68d01bbefd33060ad8f00f3117830", "042842a5a5d2cd46744203f1a1894f9c35e143f7737fcdee1f4f7a9a06dc9a48", "4a767e15cb5e9fff6282186dd97d99fec75fd62767f16ae0664413c702d2f244"),
    (108, "SpecialConstruction", "class", "9f4492878f22d68d01a9b58b74a8ed1f95c0b5e4ddd5b524a4b750ca1ba975c1", "dea199c2a61fb6d7cd478d7e4b2395a9ffd3821a37e9873cb88d7084a12cae91", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (109, "SpecialConstruction.__new__", "function", "758d9c0bdcfdb11bb00e2fdade1b60d96bfe5ec0b21e2ccb0dae7939c32e0af4", "18f95cf2d0b4d9493b8ec7815066aa0ea4efcf73a25efdf8f6acd9776838bb86", "c071aa1cf7e444a483ebbb51406c235041d08f1f875a86e72e8f850781bbf1dd"),
    (110, "SpecialConstruction.get_unique_materials", "function", "4f9ce2c08bca6b6b6660b8787c64282ce0f759674d3eb83e665ab6d8ad93a338", "abfea80c9449326669efd9f474c37f074a049c0392c4c2198e2d3f911222f3e8", "fa25fd0c7a4bbfafddc8b59696c51c3e57d323f962691a09b5a4840f5a96a049"),
    (111, "SpecialConstruction.reversed", "function", "119ed204a617dc0b9cfb033f6a01e59b2bc8724b78fc17464815352cb92ec5a5", "ff490046257400f05f0e8c0a1d994135cb76c8bd7dd0d5c28f029e2e24afcaff", "5a5cdb2262256d7fad51f9d68ef4b1c3a811c6717e3ca55ec812a92be33a6b10"),
    (112, "SurfaceConstruction", "class", "f3d6bd23a8b9a8492acac04f404b2b745daea21cb9447149fd1271a9c6e7f9fa", "6cd0b97c9f50d84ae9c3dc545b9ff697fb9cec4b9423d75428a58df8cb5ac9e3", "1670f93a577e0eddf6bb6f82277ad0e2a633cbf4b6388fe70efcfd2f87b8e06f"),
    (113, "SurfaceConstruction.ID", "function", "246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2", "b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb", "8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da"),
    (114, "SurfaceConstruction.U_internal", "function", "c6b969b4ccefeda55959e92de33912e43cc94f0f49f293bc9d403606820aa325", "f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "09a1b39958180e0a1d1f0fb76806c2e48f3796f632805617a9e4feaf77a5bb30"),
    (117, "SurfaceConstruction.__init__", "function", "6e437543a7dc3a677c95b83683ae87bb5d89343de9ddfeea554388b09158af89", "3f8f0e7459563c71eadb885ba44f5996eaff000b1d3cce74633f56ccac15b627", "cf1513062b752b56a8f090975d53432e1dc3a502d8b7405d8da4cbfc7bf1dd80"),
    (120, "SurfaceConstruction.create_simply", "function", "23907b76ef097189c79b03ef1a3e4ae87f2d06eb9d2a24c67084e536ba00c06c", "4e9f3126c2e6f280f307cd52438859b0ff05da8edde57adb66a5d87d57397f48", "be8d38be2479786dbee8927b35f308f3dd22981b9595b86c4493996b8e2c2971"),
    (121, "SurfaceConstruction.depth", "function", "60a500a853fd24e3185f4cb823dbe1082963bb0773877fba6d9cd9f294b2c13f", "f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "e46d4bbd4eeb9f98c157d161c0f7fdf898c92e6577a9a85399f64c1de803936e"),
    (122, "SurfaceConstruction.from_json", "function", "b1bb16e6a85ab929bd0657bc5ba8fa1895047228b4a221da4cefb068c156eb90", "54da007ecaf993a88fddf16c27b8f8a1aad8c74ca505a789945c3e05d863d6f8", "02b13446b59c8ca2f9e4e5373b2fb3ba93afeed59ba8ae64f558c0cbf45f73ae"),
    (123, "SurfaceConstruction.get_DB", "function", "d21ed4dbccdc7e6f5b1029ec520e8b2f83cb628abbc5e8351825b025297af4ed", "9e29d3e9130d39f650370195309931b1e075ed6ad454c911763567b0ac5cf6d9", "c9cda93c968b151716b14e98c453ae172c6aba93356d8e5e9e5f31aadfd77477"),
    (124, "SurfaceConstruction.get_U", "function", "8a480443a0211eae9da81c95877c150096d41b6a225c136f758bc6eb88816e8d", "1565610762c1d9b08ad867e7eb03788ad0e50171180fc7c4bddd5fbc1c875105", "aa34bcb15911f4ab6cb1b3e9a47fd140802f8eb03a5a6baf15c58d0c0bc51882"),
    (125, "SurfaceConstruction.get_regulated_construction", "function", "a806c4c35a386b53f752853fcf8088962fb9772e71d89cfb40378fba39060262", "161c1b7607d2a010bfe26b8f47d6e6398678da139b194a2a67f62928a792498e", "19e01530d6e800e12dc8c3510f8971871ef2fcfbba131ca0b178f21a30a88a80"),
    (126, "SurfaceConstruction.get_unique_materials", "function", "71552576f7f4f8b2e9e38fd63a57d94adb0e8e7a454b1654a516449faa90357c", "abfea80c9449326669efd9f474c37f074a049c0392c4c2198e2d3f911222f3e8", "37de2c0b6482cbb66bf9e477f288afebf6018a196ac0e820b585b2a580c53f44"),
    (127, "SurfaceConstruction.heat_capacity", "function", "dc8c7ebca81b60c6eb1f565ea6ca5073d0df7ea7526cc83dfe860219883856f9", "f1b77727408acaea93a770200d99c9972e070fabcf19c0e2f6dbc4c1c1f3feea", "000fa7f767d8cde41947852da4c82a9b6808d2646fb0f708e1ce81fd92db1c90"),
    (128, "SurfaceConstruction.load_DB", "function", "fec259a469c8d9f2d54a8f78bcc38f43a987578c139c91e8100928a46b39fcdf", "2aab05b28f2be4842b498e7a9895c14027ba2cf8fcd9c703edd2edfb67beb5a9", "2345fa523aa379a82fda19ff7b1d92bc947cadabbd145eea419729cc6cae9b8c"),
    (129, "SurfaceConstruction.reversed", "function", "d72c214309ce560e2d2ebce89b415f29d0c754feab6e99b04d4cbc5834f6bc88", "ff490046257400f05f0e8c0a1d994135cb76c8bd7dd0d5c28f029e2e24afcaff", "dadbd566dbd1545e9538721b68cfc2ff76e374eda5665e2579f8a65df0d42050"),
    (130, "SurfaceConstruction.to_dict", "function", "59426aa28f30e4d0c260e6d507ce80bfbba21ddb613c59e913ac7cc63f6a6019", "b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf", "a57703c505042c1f075c16b58646a02fca956f82258d716e96c962702b07eecd"),
    (131, "SurfaceConstruction.to_dragon", "function", "a204e68013cc32a0d596f99acd750ebd475421080351bc798b599c0dfd3f4c50", "0357f58b19684a433f6b7283c8f7b4b664f8a1dbfa1e49a14e6eceaef535f609", "c7647dc28fad76f68709e55ebcfff6758c729dd218f4d58f602ad8622741f19c"),
    (132, "UnknownConstruction", "class", "d803cd9dd044e9d991bec64fb646d717304de1badb2e53b5f5aabe632da8dcde", "bbaea5250268d2bcff09c67fdda3c04730b3bd6fcf3276e700068e1c64b2e212", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (133, "UnknownConstruction.ID", "constant", "d6777d2d28fa5736706903a4a7c235c6e39a40d31f7ccf7b719e78aeccbf69d3", "f99ca84113bc856877bc2ed0281f5e53891aec7f328d7241bb9c7ab2b4e08aa9", "f5875169d6106ac43bc97f75085e4db2ed6116036bd5140e9caf6b40307680ef"),
    (134, "UnknownConstruction.to_dragon", "function", "558da4a73ac95566184dff797ab97cb6d72cd53eaef97e48dae0c3aabba72ebe", "141c767c0ec1011b163a80079f4eb52561f2a3c6a62ed0dc5f3ac4594d8e5ad6", "3ffc574f787def97e42c77e072f21c33ba8a6bf1977ce88cd7928e3bdb5f4fcc"),
)

_EXCLUDED_ROWS = (
    (77, "FenestrationConstruction.__eq__", "function", "e668db31494d912d923704133c583f0dfee62f01e57929089c94fa5d48d90137", "7b1c495ed153f0efdcb6ce072aeef2426e334626cc93fa5ba73922a90afd5cb2", "9c54be7c5569f3a55cbf101f8fdf78dd9f793fa387cb72b82a96402fd0a5ec60"),
    (78, "FenestrationConstruction.__hash__", "function", "60007ac6f6ff93642af22c6affa7e2d91ac81564499568cf87b6de5f9e73a0c9", "1e9bc05f5a8588970559462f4b1ff6118775cc82d0229ba62f3a868e3aac5cbb", "077c262390b3e180d496642015ca2d5a0eb48d1a12f9724452917ef01d1fc1b5"),
    (80, "FenestrationConstruction.__repr__", "function", "103f1788822b2b530254219eca1b561f847c9910b31f977b6862f1d3b62363df", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "47306571f6f636eaf0b1edc75e261e648a09582e71f8153e3d2d25508d448499"),
    (81, "FenestrationConstruction.__str__", "function", "a44a4626266b5a2fb702b6ea6cb54ad9fd0ca32c89a8bdd6dd46d07cfc971cbb", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "9d59998e5c12174a489eca4d70621970230ff5a7d94061d4dac67956d307c9f7"),
    (92, "Material.__eq__", "function", "e8bba35e8e378a95857398b372b6158dce72ada37639bcccbdb8cfd64783cfac", "95bc5831aa2735df7b4f5f9c7807064ec930e27a73ab07463f4da2b32bffb572", "3fa8a9e0d5c1c6fd8ddd35e3bf420df94389633b870b38535f95c8170491c2d2"),
    (93, "Material.__hash__", "function", "60007ac6f6ff93642af22c6affa7e2d91ac81564499568cf87b6de5f9e73a0c9", "1e9bc05f5a8588970559462f4b1ff6118775cc82d0229ba62f3a868e3aac5cbb", "077c262390b3e180d496642015ca2d5a0eb48d1a12f9724452917ef01d1fc1b5"),
    (95, "Material.__repr__", "function", "1525e7894b412ea0355f4513f250046da995a247d794e4d4b7720e896dd1016d", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "acb7ae1827d20d48987fa854b9ec6679c5d295597df672ab7b5955d4f8d2ad6e"),
    (96, "Material.__str__", "function", "4df0e2868a1f89c96d734e2ba7baee7973c83fba02825bde4e0656e9a32ffa7a", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "3ad779c28eb4dfce01b2d6cc1f8d4262406c01457280ef8562c655b51da4d11b"),
    (115, "SurfaceConstruction.__eq__", "function", "7b7e80e480f2d225d172abb9f1e78f6799a00f9770132acbc73f7b1fb97a8ddb", "a82a299b8a708498b1841d13f4a4c9f288eb8a5a5310b885978887491c55db78", "375cb76a707d539857f6fc81e528f4e0e063cfbb0a60d96c40e218e82d375fd9"),
    (116, "SurfaceConstruction.__hash__", "function", "60007ac6f6ff93642af22c6affa7e2d91ac81564499568cf87b6de5f9e73a0c9", "1e9bc05f5a8588970559462f4b1ff6118775cc82d0229ba62f3a868e3aac5cbb", "077c262390b3e180d496642015ca2d5a0eb48d1a12f9724452917ef01d1fc1b5"),
    (118, "SurfaceConstruction.__repr__", "function", "490fa3376240d00d0946c7d47747256eeda8f5aa9472292499d509c2d507765c", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "0e9d9877856000367fb5cb271f76709a8447a4fbe3a611ebada822b6757e1f6f"),
    (119, "SurfaceConstruction.__str__", "function", "b9270d7aaaca3ba735eb5fffdbd597e12f1635268b3d0fc0abedd77de8bc1852", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "ff3d65ad2e1bf41b4fe2b7b28aa88e08ba846c383dee185a33555190cf05f68a"),
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
TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
EXCLUDED_SYMBOLS = tuple(item["symbol"] for item in EXCLUDED_RECEIPTS)
EQUIVALENT_SYMBOLS = (
    "FenestrationConstruction.is_transparent",
    "OpenConstruction.to_dragon",
    "SurfaceConstruction.U_internal",
    "SurfaceConstruction.depth",
    "SurfaceConstruction.get_U",
    "SurfaceConstruction.get_unique_materials",
    "SurfaceConstruction.heat_capacity",
)
CLASSIFICATIONS = {
    symbol: "equivalent" if symbol in EQUIVALENT_SYMBOLS else "exception"
    for symbol in TARGET_SYMBOLS
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"epsimple-construction-core-{item['inventory_index']}-"
        f"{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}

NATIVE_ROUTES = {
    "FenestrationConstruction": "Dragons.SimpleDragon.FenestrationConstruction",
    "FenestrationConstruction.ID": "FenestrationConstruction.Id",
    "FenestrationConstruction.__init__": "FenestrationConstruction(string, double, double?, EntityId?)",
    "FenestrationConstruction.from_json": "Dragons.SimpleDragon.GrmReader fenestration construction path",
    "FenestrationConstruction.g": "FenestrationConstruction.SolarHeatGainCoefficient",
    "FenestrationConstruction.get_DB": "FenestrationConstructionDatabase.Find and Entries",
    "FenestrationConstruction.is_transparent": "FenestrationConstruction.IsTransparent",
    "FenestrationConstruction.load_DB": "SimpleDragonEmbeddedData.FenestrationConstructions",
    "FenestrationConstruction.to_dict": "Dragons.SimpleDragon.GrmWriter fenestration construction path",
    "FenestrationConstruction.to_dragon": "GreenRetrofitConversion fenestration construction conversion",
    "FenestrationConstruction.u": "FenestrationConstruction.UValue",
    "Material": "Dragons.SimpleDragon.Material",
    "Material.ID": "Material.Id",
    "Material.__init__": "Material(string, double, double, double, EntityId?)",
    "Material.conductivity": "Material.Conductivity",
    "Material.density": "Material.Density",
    "Material.from_json": "Dragons.SimpleDragon.GrmReader material path",
    "Material.get_DB": "MaterialDatabase.Find and Items",
    "Material.load_DB": "SimpleDragonEmbeddedData.Materials",
    "Material.specific_heat": "Material.SpecificHeat",
    "Material.to_dict": "Dragons.SimpleDragon.GrmWriter material path",
    "Material.to_dragon": "GreenRetrofitConversion material conversion",
    "OpenConstruction": "SurfaceConstructionReferenceKind.Open",
    "OpenConstruction.ID": "Surface.ConstructionId value open and SurfaceConstructionReferenceKind.Open",
    "OpenConstruction.to_dragon": "GreenRetrofitConversion returns DragonAirBoundary",
    "SpecialConstruction": "SurfaceConstructionReferenceKind special cases",
    "SpecialConstruction.__new__": "SimpleSurface construction reference kind value semantics",
    "SpecialConstruction.get_unique_materials": "GreenRetrofitConversion special construction material bypass",
    "SpecialConstruction.reversed": "GreenRetrofitConversion special construction orientation bypass",
    "SurfaceConstruction": "Dragons.SimpleDragon.SurfaceConstruction",
    "SurfaceConstruction.ID": "SurfaceConstruction.Id",
    "SurfaceConstruction.U_internal": "SurfaceConstruction.InternalUValue",
    "SurfaceConstruction.__init__": "SurfaceConstruction(string, IEnumerable<SurfaceConstructionLayer>, EntityId?)",
    "SurfaceConstruction.create_simply": "SurfaceConstruction.CreateSimple",
    "SurfaceConstruction.depth": "SurfaceConstruction.Depth",
    "SurfaceConstruction.from_json": "Dragons.SimpleDragon.GrmReader surface construction path",
    "SurfaceConstruction.get_DB": "SurfaceConstructionDatabase.Find and Entries",
    "SurfaceConstruction.get_U": "SurfaceConstruction.GetUValue",
    "SurfaceConstruction.get_regulated_construction": "SurfaceConstructionDatabase.FindRegulated",
    "SurfaceConstruction.get_unique_materials": "SurfaceConstruction.Layers material projection",
    "SurfaceConstruction.heat_capacity": "SurfaceConstruction.HeatCapacity",
    "SurfaceConstruction.load_DB": "SimpleDragonEmbeddedData.SurfaceConstructions",
    "SurfaceConstruction.reversed": "SurfaceConstruction.Reverse",
    "SurfaceConstruction.to_dict": "Dragons.SimpleDragon.GrmWriter surface construction path",
    "SurfaceConstruction.to_dragon": "GreenRetrofitConversion surface construction conversion",
    "UnknownConstruction": "SurfaceConstructionReferenceKind.Unknown",
    "UnknownConstruction.ID": "Surface.ConstructionId null or empty and SurfaceConstructionReferenceKind.Unknown",
    "UnknownConstruction.to_dragon": "GreenRetrofitConversion.ResolveUnknownConstruction",
}
ADAPTATIONS = {
    symbol: (
        ("direct-native-" if CLASSIFICATIONS[symbol] == "equivalent" else "reviewed-native-adaptation-")
        + re.sub(r"[^a-z0-9]+", "-", symbol.lower()).strip("-")
        + "-"
        + next(
            item["symbol_hash"][7:15]
            for item in TARGET_RECEIPTS
            if item["symbol"] == symbol
        )
    )
    for symbol in TARGET_SYMBOLS
}

PREFIX = "epsimple-construction-core."
CASE_SPECS = (
    ("M01", "material-construction-id-state", "material", ("Material", "Material.ID", "Material.__init__"), ("Material.conductivity", "Material.density", "Material.specific_heat")),
    ("M02", "material-property-validation-mutation", "material", ("Material.conductivity", "Material.density", "Material.specific_heat"), ("Material", "Material.__init__")),
    ("M03", "material-json-dict-dragon", "material", ("Material.from_json", "Material.to_dict", "Material.to_dragon"), ("Material", "Material.ID")),
    ("M04", "material-database-load-get", "material", ("Material.get_DB", "Material.load_DB"), ("Material", "Material.to_dict")),
    ("F01", "fenestration-construction-id-state", "fenestration", ("FenestrationConstruction", "FenestrationConstruction.ID", "FenestrationConstruction.__init__"), ("FenestrationConstruction.u", "FenestrationConstruction.g", "FenestrationConstruction.is_transparent")),
    ("F02", "fenestration-property-validation-transparency", "fenestration", ("FenestrationConstruction.u", "FenestrationConstruction.g", "FenestrationConstruction.is_transparent"), ("FenestrationConstruction", "FenestrationConstruction.__init__")),
    ("F03", "fenestration-json-dict-dragon", "fenestration", ("FenestrationConstruction.from_json", "FenestrationConstruction.to_dict", "FenestrationConstruction.to_dragon"), ("FenestrationConstruction", "FenestrationConstruction.ID")),
    ("F04", "fenestration-database-load-get", "fenestration", ("FenestrationConstruction.get_DB", "FenestrationConstruction.load_DB"), ("FenestrationConstruction", "FenestrationConstruction.to_dict")),
    ("S01", "surface-construction-id-layer-filtering", "surface", ("SurfaceConstruction", "SurfaceConstruction.ID", "SurfaceConstruction.__init__"), ("Material",)),
    ("S02", "surface-derived-state-and-validation", "surface", ("SurfaceConstruction.U_internal", "SurfaceConstruction.depth", "SurfaceConstruction.get_U", "SurfaceConstruction.get_unique_materials", "SurfaceConstruction.heat_capacity"), ("SurfaceConstruction", "SurfaceConstruction.__init__", "Material")),
    ("S03", "surface-create-simple-branches", "surface", ("SurfaceConstruction.create_simply",), ("SurfaceConstruction", "SurfaceConstruction.get_U", "Material.get_DB")),
    ("S04", "surface-reverse-and-dict", "surface", ("SurfaceConstruction.reversed", "SurfaceConstruction.to_dict"), ("SurfaceConstruction", "SurfaceConstruction.ID", "SurfaceConstruction.get_unique_materials")),
    ("S05", "surface-json-and-dragon", "surface", ("SurfaceConstruction.from_json", "SurfaceConstruction.to_dragon"), ("SurfaceConstruction", "Material.to_dragon")),
    ("S06", "surface-database-load-get", "surface", ("SurfaceConstruction.get_DB", "SurfaceConstruction.load_DB"), ("SurfaceConstruction", "SurfaceConstruction.to_dict")),
    ("S07", "surface-regulation-selection", "surface", ("SurfaceConstruction.get_regulated_construction",), ("SurfaceConstruction.get_DB",)),
    ("X01", "special-singleton-empty-reverse", "special", ("SpecialConstruction", "SpecialConstruction.__new__", "SpecialConstruction.get_unique_materials", "SpecialConstruction.reversed"), ("OpenConstruction", "UnknownConstruction")),
    ("X02", "open-singleton-id-dragon", "special", ("OpenConstruction", "OpenConstruction.ID", "OpenConstruction.to_dragon"), ("SpecialConstruction", "SpecialConstruction.__new__")),
    ("X03", "unknown-singleton-id-dragon", "special", ("UnknownConstruction", "UnknownConstruction.ID", "UnknownConstruction.to_dragon"), ("SpecialConstruction", "SpecialConstruction.__new__")),
    ("R01", "byte-identical-relocated-import", "relocation", (), ("Material.load_DB", "SurfaceConstruction.load_DB", "FenestrationConstruction.load_DB")),
)
EXPECTED_CASE_IDS = tuple(PREFIX + suffix for _, suffix, _, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 19

# Sealed from a direct pinned CPython 3.12.7 bootstrap run.
EXPECTED_RUNTIME_SIGNATURES: dict[str, str] = {}
EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:e345635e8c3f121f23b95a501c9b6cc1fbcd9b83a8140716a4e7ef85638c234d"
)
EXPECTED_LOADED_LOCAL_MODULES: list[dict[str, str]] = []
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:f27243a35ebfa64a6dfa0f54516e02e41f3648e5fe45d9723933a838a2e8bba7"
)
EXPECTED_FACT_SHA256 = {
    "epsimple-construction-core.byte-identical-relocated-import": "sha256:17d201c32fdce2398bd45ab41de992740c7f60b11fc1d4a36eea21bbf3f34229",
    "epsimple-construction-core.fenestration-construction-id-state": "sha256:cdc71569325f286ea212ba5e4924b935fbd9c18d6700bb7de2880354aa3e01e1",
    "epsimple-construction-core.fenestration-database-load-get": "sha256:a762d9c0960840ecd04b1101e0a641e84a4e9e08dd4d557fea1af14812775485",
    "epsimple-construction-core.fenestration-json-dict-dragon": "sha256:75277a02c3018735535b23b5e8570427eca4bd4b25764058c34e6ca3f4094789",
    "epsimple-construction-core.fenestration-property-validation-transparency": "sha256:f31f333465a424b9db81f08fd89cd40d6f9c68813b0fbdb773ffed01a6647e3d",
    "epsimple-construction-core.material-construction-id-state": "sha256:a92d5d96221defd842f23f6583a068644b3021fda80ceaf7dad07fee75d3c030",
    "epsimple-construction-core.material-database-load-get": "sha256:e3ac196fd183cad8fad4d321879f99a220e0722cba0900d20aaf58afbb6412e8",
    "epsimple-construction-core.material-json-dict-dragon": "sha256:98e70de2024fff1826dd7079ca1e5832a78840d89e7100b4989b9e2a5f74cc04",
    "epsimple-construction-core.material-property-validation-mutation": "sha256:b925283d864b137f1d8df9d404c7d7eeb31e9decb1e2cd77cf157a2382e2b544",
    "epsimple-construction-core.open-singleton-id-dragon": "sha256:a6c1792d8aa673eb6768ab947dda77db08e78c3f7d821bcd7e5a24e7e56429c6",
    "epsimple-construction-core.special-singleton-empty-reverse": "sha256:6bd4a806154977176eec59049331235c94f086fb337038ceb7e01bfdb11cc906",
    "epsimple-construction-core.surface-construction-id-layer-filtering": "sha256:30a911d59265890c603e627e82644f41baeb6e675717430678518678435110d4",
    "epsimple-construction-core.surface-create-simple-branches": "sha256:6db25a9f88e0f782a019af35800d4c6f83e21e5d18de42bb208157dd318ebef3",
    "epsimple-construction-core.surface-database-load-get": "sha256:2ed68fd6da2bd79b8c832990ff1d1ae7b7be03c720d1226d1b74b23681ed7635",
    "epsimple-construction-core.surface-derived-state-and-validation": "sha256:a64179c7d1f1e6dbed4194e2f64ecbbf06473376ddeeeb856b32b523ea3ed2f6",
    "epsimple-construction-core.surface-json-and-dragon": "sha256:4214c3837d5fcad3dbba9357369710ab221c4ebaf0bec4510a63a2934c5eb549",
    "epsimple-construction-core.surface-regulation-selection": "sha256:6e9a7e0f77d9db9f8a506a9c4dcc878b09955ead2f6a710eaa9d2ef57c2225d0",
    "epsimple-construction-core.surface-reverse-and-dict": "sha256:e0742c6fa8c672ab34f828292c6602eb5187a9b9fb7cf520d58a009ff405def2",
    "epsimple-construction-core.unknown-singleton-id-dragon": "sha256:59e23d864b5bbbd3b900d5a1a62853182080f9b935620f82fb147ac95a08d3fd",
}
EXPECTED_FACT_MAP_SHA256 = (
    "sha256:eaf7a5ed86e0113a1f12b5f57867f5e6a34fd66f54b97c14ec5b17cb4c2451bd"
)
EXPECTED_CASE_SHA256 = {
    "epsimple-construction-core.byte-identical-relocated-import": "sha256:2307fc5a22839f2c103bcbc5511b49d2fd4d05c933e605530070bfbaf32a582c",
    "epsimple-construction-core.fenestration-construction-id-state": "sha256:0d2d5fb3e613b6a437afb4621da0c81a8db18ca817ade0eb477c356adfd57102",
    "epsimple-construction-core.fenestration-database-load-get": "sha256:34347c009760b31d79be70cd9cc82f25eb6c6692b32afcdbe68bc7a8d9dc11a1",
    "epsimple-construction-core.fenestration-json-dict-dragon": "sha256:1ae7f8ba0bfd36052d9c71788d0b1745a329941d02f650ba848bf6b7aac5f0ad",
    "epsimple-construction-core.fenestration-property-validation-transparency": "sha256:551df1ba806799b0fb9d557be0b8cca1d305ef0a1f574cd45ef9054d9ee40208",
    "epsimple-construction-core.material-construction-id-state": "sha256:4751e51ddb0f3b9e49ca82041138c209b07f87bfe1938c9fb7055881ece7eaef",
    "epsimple-construction-core.material-database-load-get": "sha256:d81e8b09fceca3c4cee939812df383500e9fafa05e275081c4d1c2eae6800929",
    "epsimple-construction-core.material-json-dict-dragon": "sha256:7a2ef5f2f7a4cf3495ebb1cb1e04495c8bb3d9980f6591588b8555445ba9d356",
    "epsimple-construction-core.material-property-validation-mutation": "sha256:edb9b59868285928aa302702bb75e5c8b17a1ea8ef9468ef99b61438ffa33632",
    "epsimple-construction-core.open-singleton-id-dragon": "sha256:45fd2438e983f4d5b79cea2a850b86a95923cb10b044ef1ca63d36e194abc961",
    "epsimple-construction-core.special-singleton-empty-reverse": "sha256:f810dcc9194081639c84d1fe09108102acb77e16c308ed5fc094b115412d7487",
    "epsimple-construction-core.surface-construction-id-layer-filtering": "sha256:72ba71d45668765872ac8a83e09e9256780fa25d99c5b31f8ce8213bc2e9a444",
    "epsimple-construction-core.surface-create-simple-branches": "sha256:c4be9d0ebf60c8b3a413b954429c0d9bbc7d0e34f6e02ff883b281cc467c5640",
    "epsimple-construction-core.surface-database-load-get": "sha256:3532d39d80fd6547a90017341ae1226fd4e6cd6360d04e7aa7227801b82b4463",
    "epsimple-construction-core.surface-derived-state-and-validation": "sha256:cd046046d0502f2f4f2a7e46ac29168829368ebe926efcb7278aef2abd5ebc17",
    "epsimple-construction-core.surface-json-and-dragon": "sha256:2002c34f535cf15f01c1b5495f614c75658c7b8acdc02b19e0e50a93ef26ae78",
    "epsimple-construction-core.surface-regulation-selection": "sha256:7c7d8fc7d6df51ca699873555837a21431e1c21174fc2b7913f3cb4262612d19",
    "epsimple-construction-core.surface-reverse-and-dict": "sha256:ad4ac7bce44d0c78d242acf0e9e522736ca97efa8ea7a37d472ca78607eafc7a",
    "epsimple-construction-core.unknown-singleton-id-dragon": "sha256:75e592cd05e8ab2786f261abdc4a346a75c97b91bd846e63950aa442f8cbe1fb",
}
EXPECTED_CASE_MAP_SHA256 = (
    "sha256:ea1a9888aea198d9f32be778ca6ce96f1f0390da72b6a04a6bc0b354f913904e"
)
EXPECTED_CASES_SHA256 = (
    "sha256:9046cfba389607b07ceb9308c6962cba74c8550fd1e2557fe453f8144d1b0f92"
)

ADJACENT_POLICY = (
    "existing equality hash representation and string scope decisions remain unchanged",
    "no excluded symbol appears in target or context coverage",
    "object identity is observed only as boolean alias topology and never promoted",
    "raw memory addresses are normalized and rejected from persisted facts",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "code": code,
            "context_symbols": list(context),
            "id": PREFIX + suffix,
            "subfamily": subfamily,
            "target_symbols": list(targets),
        }
        for code, suffix, subfamily, targets, context in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Construction case order drifted.")
    target_counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if target_counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Construction targets are not an exact one-case partition.")
    all_symbols = {
        symbol
        for definition in definitions
        for symbol in (*definition["target_symbols"], *definition["context_symbols"])
    }
    if not all_symbols.issubset(set(TARGET_SYMBOLS)):
        raise RuntimeError("Construction context escaped the bounded target set.")
    if all_symbols.intersection(EXCLUDED_SYMBOLS):
        raise RuntimeError("An adjacent out-of-scope symbol was promoted.")
    return definitions


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
    SUPPORT.require_exact_keys(
        value,
        {"content_sha256", "files", "schema", "scope_sha256", "summary", "symbols", "upstream_commit"},
        "Public-symbol inventory",
    )
    if value["schema"] != "dragons.upstream-public-symbol-inventory.v2":
        raise SystemExit("The public-symbol inventory schema drifted.")
    if value["upstream_commit"].lower() != commit:
        raise SystemExit("The public-symbol inventory commit drifted.")
    computed = canonical_sha256(
        {
            "files": value["files"],
            "scope_sha256": value["scope_sha256"],
            "symbols": value["symbols"],
            "upstream_commit": value["upstream_commit"],
        }
    )
    if computed != value["content_sha256"] or computed != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The public-symbol inventory aggregate receipt drifted.")
    expected_summary = {
        "kind_counts": {
            kind: sum(item["kind"] == kind for item in value["symbols"])
            for kind in ("class", "constant", "function")
        },
        "public_symbol_count": len(value["symbols"]),
        "python_file_count": len(value["files"]),
    }
    if value["summary"] != expected_summary:
        raise SystemExit("The public-symbol inventory summary drifted.")
    source_files = [item for item in value["files"] if item["path"] == SOURCE_PATH]
    expected_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    if source_files != [expected_file]:
        raise SystemExit("The EPlusSimple construction source receipt drifted.")
    target_receipts = []
    for expected in TARGET_RECEIPTS:
        index = expected["inventory_index"]
        if value["symbols"][index] != _descriptor(expected):
            raise SystemExit(f"Target inventory receipt drifted at index {index}.")
        target_receipts.append(expected)
    excluded_receipts = []
    for expected in EXCLUDED_RECEIPTS:
        index = expected["inventory_index"]
        if value["symbols"][index] != _descriptor(expected):
            raise SystemExit(f"Excluded inventory receipt drifted at index {index}.")
        excluded_receipts.append(expected)
    source_symbols = [
        (index, item["symbol"])
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    bounded = sorted(
        [(item["inventory_index"], item["symbol"]) for item in TARGET_RECEIPTS]
        + [(item["inventory_index"], item["symbol"]) for item in EXCLUDED_RECEIPTS]
    )
    if source_symbols != bounded:
        raise SystemExit("Construction source contains an unclassified adjacent symbol.")
    return {
        "content_sha256": computed,
        "excluded_receipts": list(excluded_receipts),
        "file": expected_file,
        "files": value["files"],
        "symbols": [_descriptor(item) for item in target_receipts],
        "target_receipts": list(target_receipts),
    }


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        source = root / Path(SOURCE_PATH).relative_to("src")
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


def _validate_source_tree(source_root: Path, inventory: dict[str, Any]) -> None:
    source = source_root / Path(SOURCE_PATH).relative_to("src")
    if (
        not source.is_file()
        or source.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(source) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported EPlusSimple construction source drifted.")
    for receipt in DATABASE_RESOURCES:
        path = source_root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise SystemExit(f"Pinned database resource drifted: {receipt['path']}")
    file_by_path = {item["path"]: item for item in inventory["files"]}
    if file_by_path[SOURCE_PATH] != inventory["file"]:
        raise SystemExit("Construction source inventory binding drifted.")


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


def _audit_loaded_local_modules(
    source_root: Path, inventory: dict[str, Any]
) -> list[dict[str, str]]:
    file_by_path = {item["path"]: item for item in inventory["files"]}
    loaded: list[dict[str, str]] = []
    for name, module in sorted(sys.modules.items()):
        if not (name == "epsimple" or name.startswith("epsimple.") or name == "idragon" or name.startswith("idragon.")):
            continue
        filename = getattr(module, "__file__", None)
        if not isinstance(filename, str):
            raise SystemExit(f"Pinned local module {name} has no source file.")
        path = Path(filename).resolve()
        try:
            relative = path.relative_to(source_root.resolve())
        except ValueError as error:
            raise SystemExit(f"Pinned local module {name} escaped the isolated root.") from error
        source_path = "src/" + relative.as_posix()
        receipt = file_by_path.get(source_path)
        if receipt is None:
            raise SystemExit(f"Loaded local module {name} is absent from inventory.")
        if sha256_file(path) != receipt["content_hash"]:
            raise SystemExit(f"Loaded local module {name} source hash drifted.")
        loaded.append(
            {
                "ast_sha256": receipt["ast_hash"],
                "module": name,
                "path": source_path,
                "source_sha256": receipt["content_hash"],
            }
        )
    required = {"epsimple", "epsimple.constants", "epsimple.core", "epsimple.core.construction", "idragon", "idragon.dragon.construction"}
    if not required.issubset({item["module"] for item in loaded}):
        raise SystemExit("The isolated construction import omitted required local modules.")
    return loaded


@contextmanager
def _isolated_import(
    source_root: Path, inventory: dict[str, Any]
) -> Iterator[tuple[ModuleType, list[dict[str, str]]]]:
    names = [
        name
        for name in sys.modules
        if name == "epsimple" or name.startswith("epsimple.") or name == "idragon" or name.startswith("idragon.")
    ]
    previous = {name: sys.modules[name] for name in names}
    previous_path = list(sys.path)
    for name in names:
        sys.modules.pop(name, None)
    sys.path.insert(0, str(source_root))
    try:
        epsimple_dir = source_root / "epsimple"
        core_dir = epsimple_dir / "core"
        sys.modules["epsimple"] = _make_package("epsimple", epsimple_dir)
        sys.modules["epsimple.core"] = _make_package("epsimple.core", core_dir)
        _load_module("epsimple.constants", epsimple_dir / "constants.py")
        importlib.import_module("idragon")
        construction = _load_module(
            "epsimple.core.construction", core_dir / "construction.py"
        )
        loaded = _audit_loaded_local_modules(source_root, inventory)
        yield construction, loaded
    finally:
        for name in list(sys.modules):
            if name == "epsimple" or name.startswith("epsimple.") or name == "idragon" or name.startswith("idragon."):
                sys.modules.pop(name, None)
        sys.modules.update(previous)
        sys.path[:] = previous_path


def _copy_source_tree(source_root: Path, destination: Path) -> None:
    def ignore(_: str, names: list[str]) -> set[str]:
        return {name for name in names if name == "__pycache__" or name.endswith((".pyc", ".pyo"))}

    for package in ("epsimple", "idragon"):
        shutil.copytree(source_root / package, destination / package, ignore=ignore)


def _resolve_symbol(module: ModuleType, symbol: str) -> Any:
    value: Any = module
    for part in symbol.split("."):
        value = getattr(value, part)
    return value


def _runtime_signature(value: Any) -> str:
    if isinstance(value, property):
        result = "property:fget=" + str(inspect.signature(value.fget))
        if value.fset is not None:
            result += ";fset=" + str(inspect.signature(value.fset))
        return result
    if not callable(value):
        return "constant:" + type(value).__name__
    return str(inspect.signature(value))


def _runtime_signatures(module: ModuleType) -> dict[str, str]:
    return {
        symbol: _runtime_signature(_resolve_symbol(module, symbol))
        for symbol in TARGET_SYMBOLS
    }


def _encoded_number(value: Any) -> dict[str, Any]:
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
    raise TypeError(f"Expected a Python numeric value, got {type(value).__name__}.")


def _encode(value: Any) -> Any:
    if value is None:
        return {"kind": "none"}
    if isinstance(value, (bool, int, float)):
        return _encoded_number(value)
    if isinstance(value, str):
        return {"kind": "str", "value": value}
    if isinstance(value, list):
        return {"items": [_encode(item) for item in value], "kind": "list"}
    if isinstance(value, tuple):
        return {"items": [_encode(item) for item in value], "kind": "tuple"}
    if isinstance(value, dict):
        return {
            "items": [
                {"key": _encode(key), "value": _encode(item)}
                for key, item in value.items()
            ],
            "kind": "dict",
        }
    raise TypeError(f"Cannot encode unstable value of type {type(value).__name__}.")


RAW_ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,}")
WINDOWS_PATH_PATTERN = re.compile(r"(?i)(?:^|[\s=:'\"])[a-z]:[\\/]")
POSIX_PATH_PATTERN = re.compile(r"(?:^|[\s=:'\"])/(?:home|tmp|users|var|private|mnt|workspace)(?:/|\\)", re.IGNORECASE)
GUID_PATTERN = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}\b")
TIMESTAMP_PATTERN = re.compile(r"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}")


def _safe_identifier(value: str, prefix: str) -> dict[str, Any]:
    pattern = re.compile(re.escape(prefix) + r"AUTOID0x[0-9a-fA-F]+\Z")
    if pattern.fullmatch(value):
        return {
            "auto_id": True,
            "prefix": prefix,
        }
    if RAW_ADDRESS_PATTERN.search(value):
        raise RuntimeError("A non-normalized identity address was observed.")
    return {"auto_id": False, "value": value}


def _observe(operation: Callable[[], Any], projector: Callable[[Any], Any] | None = None) -> dict[str, Any]:
    try:
        value = operation()
    except BaseException as exception:  # noqa: BLE001 - exception timing is oracle data.
        return {
            "error": {"message": str(exception), "type": type(exception).__name__},
            "outcome": "raised",
        }
    result: dict[str, Any] = {
        "outcome": "returned",
        "runtime_type": type(value).__name__,
    }
    if projector is not None:
        result["value"] = projector(value)
    elif value is None or isinstance(value, (bool, int, float, str, list, tuple, dict)):
        result["value"] = _encode(value)
    return result


def _material_state(value: Any) -> dict[str, Any]:
    return {
        "ID": _safe_identifier(value.ID, "MTRL-"),
        "conductivity": _encoded_number(value.conductivity),
        "density": _encoded_number(value.density),
        "name": _encode(value.name),
        "specific_heat": _encoded_number(value.specific_heat),
    }


def _fenestration_state(value: Any) -> dict[str, Any]:
    return {
        "ID": _safe_identifier(value.ID, "CTFN-"),
        "g": _encode(value.g),
        "is_transparent": value.is_transparent,
        "name": _encode(value.name),
        "u": _encoded_number(value.u),
    }


def _surface_state(value: Any) -> dict[str, Any]:
    result = {
        "ID": _safe_identifier(value.ID, "CTSF-"),
        "depth": _encoded_number(value.depth),
        "heat_capacity": _encoded_number(value.heat_capacity),
        "layers": [
            {
                "material_ID": _safe_identifier(material.ID, "MTRL-"),
                "material_name": _encode(material.name),
                "thickness": _encoded_number(thickness),
            }
            for material, thickness in value.layers
        ],
        "name": _encode(value.name),
    }
    result["U_internal"] = (
        _observe(lambda: value.U_internal, _encoded_number)
        if not value.layers
        else _encoded_number(value.U_internal)
    )
    return result


def _dragon_material_state(value: Any) -> dict[str, Any]:
    return {
        "conductivity": _encoded_number(value.conductivity),
        "density": _encoded_number(value.density),
        "name": value.name,
        "roughness": value.roughness.value,
        "solar_absorptance": _encoded_number(value.solar_absorptance),
        "specific_heat": _encoded_number(value.specific_heat),
        "thermal_absorptance": _encoded_number(value.thermal_absorptance),
        "type": type(value).__name__,
        "visible_absorptance": _encoded_number(value.visible_absorptance),
    }


def _dragon_construction_state(value: Any) -> dict[str, Any]:
    name = type(value).__name__
    if name == "Construction":
        return {
            "layers": [
                {
                    "material": _dragon_material_state(layer.material),
                    "name": layer.name,
                    "thickness": _encoded_number(layer.thickness),
                }
                for layer in value.layers
            ],
            "name": value.name,
            "type": name,
        }
    if name == "Glazing":
        return {"G": _encoded_number(value.G), "U": _encoded_number(value.U), "name": value.name, "type": name}
    if name == "NoMassConstruction":
        return {"U": _encoded_number(value.U), "name": value.name, "type": name}
    if name == "AirBoundary":
        return {"ACH": _encoded_number(value.ACH), "name": value.name, "type": name}
    raise RuntimeError(f"Unexpected Dragon construction type: {name}")


def _setter_probes(
    factory: Callable[[], Any], attribute: str, values: tuple[Any, ...]
) -> list[dict[str, Any]]:
    results = []
    for value in values:
        instance = factory()
        observation = _observe(lambda instance=instance, value=value: setattr(instance, attribute, value))
        stored = _observe(lambda instance=instance: getattr(instance, attribute), _encode)
        results.append({"input": _encode(value), "observation": observation, "stored": stored})
    return results


def _db_path_receipt(value: str, filename: str) -> dict[str, Any]:
    path = Path(value)
    return {
        "exists": path.is_file(),
        "filename": path.name,
        "suffix": "/".join(path.parts[-4:]),
        "expected_filename": filename,
    }


def _fact(code: str, subfamily: str, observations: dict[str, Any], timeline: list[dict[str, Any]]) -> dict[str, Any]:
    if not timeline:
        raise RuntimeError(f"Case {code} has no executable observation timeline.")
    return {
        "observations": observations,
        "scenario": code,
        "subfamily": subfamily,
        "timeline": timeline,
    }


def _phase(name: str, observation: dict[str, Any]) -> dict[str, Any]:
    return {"phase": name, **observation}


def _m01(module: ModuleType) -> dict[str, Any]:
    default_event = _observe(
        lambda: module.Material("Default", 0.4, 800, 900), _material_state
    )
    explicit_event = _observe(
        lambda: module.Material("Explicit", 1, 2.5, 100, ID="MAT-EXPLICIT"),
        _material_state,
    )
    null_name_event = _observe(
        lambda: module.Material(None, 0.2, 600, 850, ID="MAT-NULL"),
        _material_state,
    )
    return _fact(
        "M01",
        "material",
        {
            "class_module": module.Material.__module__,
            "class_name": module.Material.__name__,
            "default": default_event,
            "explicit": explicit_event,
            "null_name": null_name_event,
        },
        [
            _phase("construct-default", default_event),
            _phase("construct-explicit", explicit_event),
            _phase("construct-null-name", null_name_event),
        ],
    )


def _m02(module: ModuleType) -> dict[str, Any]:
    factory = lambda: module.Material("Probe", 0.4, 800, 900, ID="MAT-PROBE")
    probes = {
        "conductivity": _setter_probes(
            factory,
            "conductivity",
            (True, math.nan, math.inf, math.nextafter(0.0, math.inf), 0, -1, "bad", None),
        ),
        "density": _setter_probes(
            factory,
            "density",
            (True, math.nan, math.inf, math.nextafter(0.0, math.inf), 0, -1, "bad", None),
        ),
        "specific_heat": _setter_probes(
            factory,
            "specific_heat",
            (True, 99, 100, math.nan, math.inf, "bad", None),
        ),
    }
    constructor_probes = [
        {
            "label": label,
            "observation": _observe(operation, _material_state),
        }
        for label, operation in (
            ("bools", lambda: module.Material("Bool", True, True, True, ID="MAT-BOOL")),
            ("nan", lambda: module.Material("Nan", math.nan, math.nan, math.nan, ID="MAT-NAN")),
            ("zero-conductivity", lambda: module.Material("Zero", 0, 800, 900, ID="MAT-ZERO")),
            ("low-specific-heat", lambda: module.Material("Low", 0.4, 800, 99, ID="MAT-LOW")),
            ("text-density", lambda: module.Material("Text", 0.4, "800", 900, ID="MAT-TEXT")),
        )
    ]
    timeline = [
        _phase(f"set-{name}-{index}", item["observation"])
        for name, items in probes.items()
        for index, item in enumerate(items)
    ] + [
        _phase("construct-" + item["label"], item["observation"])
        for item in constructor_probes
    ]
    return _fact(
        "M02",
        "material",
        {"constructor_probes": constructor_probes, "setter_probes": probes},
        timeline,
    )


def _m03(module: ModuleType) -> dict[str, Any]:
    payload = SimpleNamespace(
        name="Json Material",
        conductivity=0.37,
        density=745,
        specific_heat=915,
        id="MAT-JSON",
    )
    from_event = _observe(lambda: module.Material.from_json(payload), _material_state)
    material = module.Material.from_json(payload)
    dict_event = _observe(material.to_dict, _encode)
    dragon_event = _observe(material.to_dragon, _dragon_material_state)
    missing_event = _observe(
        lambda: module.Material.from_json(SimpleNamespace(name="Missing")),
        _material_state,
    )
    return _fact(
        "M03",
        "material",
        {
            "from_json": from_event,
            "missing_attribute": missing_event,
            "to_dict": dict_event,
            "to_dragon": dragon_event,
        },
        [
            _phase("from-json", from_event),
            _phase("to-dict", dict_event),
            _phase("to-dragon", dragon_event),
            _phase("from-json-missing", missing_event),
        ],
    )


def _m04(module: ModuleType) -> dict[str, Any]:
    first_before = module.Material._DB["concrete"]
    none_event = _observe(lambda: module.Material.get_DB(None), _encode)
    path_event = _observe(
        lambda: module.Material.get_DB("__path__"),
        lambda value: _db_path_receipt(value, "material.csv"),
    )
    all_event = _observe(
        lambda: module.Material.get_DB("__all__"),
        lambda values: {
            "count": len(values),
            "names": [value.name for value in values],
            "states_sha256": canonical_sha256([_material_state(value) for value in values]),
        },
    )
    concrete_event = _observe(
        lambda: module.Material.get_DB("concrete"), _material_state
    )
    dict_event = _observe(
        lambda: module.Material.get_DB("concrete", as_dict=True), _encode
    )
    invalid_event = _observe(lambda: module.Material.get_DB("missing-material"))
    load_event = _observe(module.Material.load_DB)
    after = module.Material._DB["concrete"]
    return _fact(
        "M04",
        "material",
        {
            "all": all_event,
            "as_dict": dict_event,
            "concrete": concrete_event,
            "count_after_reload": len(module.Material._DB),
            "count_before_reload": 4,
            "invalid": invalid_event,
            "none": none_event,
            "object_replaced_on_reload": first_before is not after,
            "path": path_event,
            "reload": load_event,
        },
        [
            _phase("get-none", none_event),
            _phase("get-path", path_event),
            _phase("get-all", all_event),
            _phase("get-concrete", concrete_event),
            _phase("get-concrete-dict", dict_event),
            _phase("get-invalid", invalid_event),
            _phase("reload", load_event),
        ],
    )


def _f01(module: ModuleType) -> dict[str, Any]:
    default_event = _observe(
        lambda: module.FenestrationConstruction("Default", 1.6, 0.55),
        _fenestration_state,
    )
    transparent_event = _observe(
        lambda: module.FenestrationConstruction("Transparent", 1.25, 0.42, ID="FEN-GLASS"),
        _fenestration_state,
    )
    opaque_event = _observe(
        lambda: module.FenestrationConstruction("Opaque", 2.4, ID="FEN-OPAQUE"),
        _fenestration_state,
    )
    null_name_event = _observe(
        lambda: module.FenestrationConstruction(None, 2.0, None, ID="FEN-NULL"),
        _fenestration_state,
    )
    return _fact(
        "F01",
        "fenestration",
        {
            "class_module": module.FenestrationConstruction.__module__,
            "default": default_event,
            "null_name": null_name_event,
            "opaque": opaque_event,
            "transparent": transparent_event,
        },
        [
            _phase("construct-default", default_event),
            _phase("construct-transparent", transparent_event),
            _phase("construct-opaque", opaque_event),
            _phase("construct-null-name", null_name_event),
        ],
    )


def _f02(module: ModuleType) -> dict[str, Any]:
    factory = lambda: module.FenestrationConstruction("Probe", 1.6, 0.55, ID="FEN-PROBE")
    probes = {
        "u": _setter_probes(factory, "u", (True, math.nan, math.inf, math.nextafter(0.0, math.inf), 0, -1, "bad", None)),
        "g": _setter_probes(factory, "g", (None, math.nan, 0.5, math.nextafter(0.0, math.inf), math.nextafter(1.0, -math.inf), 0, 1, math.inf, True, "bad")),
    }
    transparency_values = (None, math.nan, math.nextafter(0.0, math.inf), 0.5, math.nextafter(1.0, -math.inf))
    transparency = [
        {
            "g": _encode(value),
            "is_transparent": module.FenestrationConstruction("T", 1.6, value, ID="FEN-T").is_transparent,
        }
        for value in transparency_values
    ]
    timeline = [
        _phase(f"set-{name}-{index}", item["observation"])
        for name, items in probes.items()
        for index, item in enumerate(items)
    ]
    timeline.append(
        _phase(
            "observe-transparency",
            {"outcome": "returned", "runtime_type": "list", "value": _encode([item["is_transparent"] for item in transparency])},
        )
    )
    return _fact(
        "F02",
        "fenestration",
        {"setter_probes": probes, "transparency": transparency},
        timeline,
    )


def _f03(module: ModuleType) -> dict[str, Any]:
    transparent_payload = SimpleNamespace(
        name="Json Glass", u=1.4, g=0.48, is_transparent=True, id="FEN-JSON-T"
    )
    opaque_payload = SimpleNamespace(
        name="Json Opaque", u=2.1, g=0.48, is_transparent=False, id="FEN-JSON-O"
    )
    transparent = module.FenestrationConstruction.from_json(transparent_payload)
    opaque = module.FenestrationConstruction.from_json(opaque_payload)
    from_transparent = _observe(
        lambda: module.FenestrationConstruction.from_json(transparent_payload),
        _fenestration_state,
    )
    from_opaque = _observe(
        lambda: module.FenestrationConstruction.from_json(opaque_payload),
        _fenestration_state,
    )
    transparent_dict = _observe(transparent.to_dict, _encode)
    opaque_dict = _observe(opaque.to_dict, _encode)
    transparent_dragon = _observe(transparent.to_dragon, _dragon_construction_state)
    opaque_dragon = _observe(opaque.to_dragon, _dragon_construction_state)
    missing_g = _observe(
        lambda: module.FenestrationConstruction.from_json(
            SimpleNamespace(name="Missing", u=1.5, is_transparent=True, id="FEN-MISSING")
        )
    )
    return _fact(
        "F03",
        "fenestration",
        {
            "missing_transparent_g": missing_g,
            "opaque": {"from_json": from_opaque, "to_dict": opaque_dict, "to_dragon": opaque_dragon},
            "transparent": {"from_json": from_transparent, "to_dict": transparent_dict, "to_dragon": transparent_dragon},
        },
        [
            _phase("from-json-transparent", from_transparent),
            _phase("from-json-opaque", from_opaque),
            _phase("to-dict-transparent", transparent_dict),
            _phase("to-dict-opaque", opaque_dict),
            _phase("to-dragon-transparent", transparent_dragon),
            _phase("to-dragon-opaque", opaque_dragon),
            _phase("from-json-missing-g", missing_g),
        ],
    )


def _f04(module: ModuleType) -> dict[str, Any]:
    key = next(iter(module.FenestrationConstruction._DB))
    first_before = module.FenestrationConstruction._DB[key]
    none_event = _observe(lambda: module.FenestrationConstruction.get_DB(None), _encode)
    path_event = _observe(
        lambda: module.FenestrationConstruction.get_DB("__path__"),
        lambda value: _db_path_receipt(value, "construction_regulation_fenestration.csv"),
    )
    all_event = _observe(
        lambda: module.FenestrationConstruction.get_DB("__all__"),
        lambda values: {
            "count": len(values),
            "first": _fenestration_state(values[0]),
            "last": _fenestration_state(values[-1]),
            "states_sha256": canonical_sha256([_fenestration_state(value) for value in values]),
        },
    )
    item_event = _observe(lambda: module.FenestrationConstruction.get_DB(key), _fenestration_state)
    dict_event = _observe(lambda: module.FenestrationConstruction.get_DB(key, as_dict=True), _encode)
    invalid_event = _observe(lambda: module.FenestrationConstruction.get_DB(("missing",) * 6))
    load_event = _observe(module.FenestrationConstruction.load_DB)
    return _fact(
        "F04",
        "fenestration",
        {
            "all": all_event,
            "as_dict": dict_event,
            "count_after_reload": len(module.FenestrationConstruction._DB),
            "first_key": _encode(key),
            "invalid": invalid_event,
            "item": item_event,
            "none": none_event,
            "object_replaced_on_reload": first_before is not module.FenestrationConstruction._DB[key],
            "path": path_event,
            "reload": load_event,
        },
        [
            _phase("get-none", none_event),
            _phase("get-path", path_event),
            _phase("get-all", all_event),
            _phase("get-first", item_event),
            _phase("get-first-dict", dict_event),
            _phase("get-invalid", invalid_event),
            _phase("reload", load_event),
        ],
    )


def _surface_materials(module: ModuleType) -> tuple[Any, Any]:
    first = module.Material("First", 0.5, 800, 900, ID="MAT-FIRST")
    second = module.Material("Second", 1.5, 2200, 1000, ID="MAT-SECOND")
    return first, second


def _s01(module: ModuleType) -> dict[str, Any]:
    first, second = _surface_materials(module)
    default_event = _observe(
        lambda: module.SurfaceConstruction("Default", first, 0.1, second, 0.2),
        _surface_state,
    )
    filtered_event = _observe(
        lambda: module.SurfaceConstruction("Filtered", first, -0.1, second, 0, first, True, ID="SURF-FILTER"),
        _surface_state,
    )
    empty_event = _observe(
        lambda: module.SurfaceConstruction("Empty", ID="SURF-EMPTY"),
        _surface_state,
    )
    malformed = [
        {
            "label": label,
            "observation": _observe(operation, _surface_state),
        }
        for label, operation in (
            ("odd", lambda: module.SurfaceConstruction("Odd", first, ID="SURF-ODD")),
            ("wrong-material", lambda: module.SurfaceConstruction("Wrong", "material", 0.1, ID="SURF-WRONG")),
            ("wrong-thickness", lambda: module.SurfaceConstruction("Wrong", first, "0.1", ID="SURF-WRONG")),
        )
    ]
    return _fact(
        "S01",
        "surface",
        {"default": default_event, "empty": empty_event, "filtered": filtered_event, "malformed": malformed},
        [
            _phase("construct-default", default_event),
            _phase("construct-filtered", filtered_event),
            _phase("construct-empty", empty_event),
            *[_phase("construct-" + item["label"], item["observation"]) for item in malformed],
        ],
    )


def _s02(module: ModuleType) -> dict[str, Any]:
    first, second = _surface_materials(module)
    surface = module.SurfaceConstruction("Metrics", first, 0.125, second, 0.075, ID="SURF-METRICS")
    empty = module.SurfaceConstruction("Empty", ID="SURF-EMPTY")
    metrics_event = _observe(
        lambda: {
            "U_internal": surface.U_internal,
            "depth": surface.depth,
            "get_U_default": surface.get_U(),
            "get_U_custom": surface.get_U(8.0, 20.0),
            "heat_capacity": surface.heat_capacity,
        },
        _encode,
    )
    unique_event = _observe(
        surface.get_unique_materials,
        lambda values: {"keys": list(values), "names": [value.name for value in values.values()]},
    )
    empty_u = _observe(lambda: empty.U_internal, _encoded_number)
    empty_get_u = _observe(empty.get_U, _encoded_number)
    zero_h = _observe(lambda: surface.get_U(0, 20), _encoded_number)
    return _fact(
        "S02",
        "surface",
        {
            "empty_depth": _encoded_number(empty.depth),
            "empty_get_u": empty_get_u,
            "empty_heat_capacity": _encoded_number(empty.heat_capacity),
            "empty_u_internal": empty_u,
            "metrics": metrics_event,
            "unique_materials": unique_event,
            "zero_convection": zero_h,
        },
        [
            _phase("observe-metrics", metrics_event),
            _phase("get-unique-materials", unique_event),
            _phase("empty-u-internal", empty_u),
            _phase("empty-get-u", empty_get_u),
            _phase("zero-convection", zero_h),
        ],
    )


def _s03(module: ModuleType) -> dict[str, Any]:
    standard = _observe(
        lambda: module.SurfaceConstruction.create_simply("Standard", 0.25, ID="SURF-SIMPLE-A"),
        _surface_state,
    )
    no_insulation = _observe(
        lambda: module.SurfaceConstruction.create_simply("High U", 5.0, ID="SURF-SIMPLE-B"),
        _surface_state,
    )
    custom = _observe(
        lambda: module.SurfaceConstruction.create_simply(
            "Custom", 0.5, h_in=8, h_out=20, concrete_thickness=0.15, ID="SURF-SIMPLE-C"
        ),
        _surface_state,
    )
    maximum = 1 / (1 / module.ConvectionHeatTransfer.IN + 1 / module.ConvectionHeatTransfer.OUT)
    invalid_equal = _observe(lambda: module.SurfaceConstruction.create_simply("Invalid", maximum))
    invalid_above = _observe(lambda: module.SurfaceConstruction.create_simply("Invalid", maximum + 1))
    return _fact(
        "S03",
        "surface",
        {
            "custom": custom,
            "invalid_above_maximum": invalid_above,
            "invalid_equal_maximum": invalid_equal,
            "maximum_u": _encoded_number(maximum),
            "no_insulation": no_insulation,
            "standard": standard,
        },
        [
            _phase("create-standard", standard),
            _phase("create-no-insulation", no_insulation),
            _phase("create-custom", custom),
            _phase("create-equal-maximum", invalid_equal),
            _phase("create-above-maximum", invalid_above),
        ],
    )


def _s04(module: ModuleType) -> dict[str, Any]:
    first, second = _surface_materials(module)
    original = module.SurfaceConstruction("Original", first, 0.1, second, 0.2, ID="SURF-ORIGINAL")
    reverse_event = _observe(original.reversed, _surface_state)
    reversed_value = original.reversed()
    dict_event = _observe(original.to_dict, _encode)
    reverse_dict_event = _observe(reversed_value.to_dict, _encode)
    return _fact(
        "S04",
        "surface",
        {
            "layer_identity_reversed": [
                reversed_value.layers[index][0] is original.layers[-index - 1][0]
                for index in range(len(original.layers))
            ],
            "original_unchanged": _surface_state(original),
            "reversed": reverse_event,
            "reversed_to_dict": reverse_dict_event,
            "to_dict": dict_event,
        },
        [
            _phase("reverse", reverse_event),
            _phase("to-dict", dict_event),
            _phase("reversed-to-dict", reverse_dict_event),
        ],
    )


def _s05(module: ModuleType) -> dict[str, Any]:
    first, second = _surface_materials(module)
    payload = SimpleNamespace(
        name="Json Surface",
        id="SURF-JSON",
        layers=[
            SimpleNamespace(material_id="MAT-FIRST", thickness=0.12),
            SimpleNamespace(material_id="MAT-SECOND", thickness=0.08),
        ],
    )
    materials = {"MAT-FIRST": first, "MAT-SECOND": second}
    from_event = _observe(lambda: module.SurfaceConstruction.from_json(payload, materials), _surface_state)
    surface = module.SurfaceConstruction.from_json(payload, materials)
    seeded = {"MAT-FIRST": first.to_dragon()}
    seed_before = _dragon_material_state(seeded["MAT-FIRST"])
    dragon_event = _observe(
        lambda: surface.to_dragon(material_dict=seeded), _dragon_construction_state
    )
    missing_event = _observe(
        lambda: module.SurfaceConstruction.from_json(payload, {"MAT-FIRST": first}),
        _surface_state,
    )
    return _fact(
        "S05",
        "surface",
        {
            "from_json": from_event,
            "missing_material": missing_event,
            "seed_dictionary_keys_after": list(seeded),
            "seed_material_unchanged": seed_before == _dragon_material_state(seeded["MAT-FIRST"]),
            "to_dragon": dragon_event,
        },
        [
            _phase("from-json", from_event),
            _phase("to-dragon", dragon_event),
            _phase("from-json-missing-material", missing_event),
        ],
    )


def _s06(module: ModuleType) -> dict[str, Any]:
    key = next(iter(module.SurfaceConstruction._DB))
    first_before = module.SurfaceConstruction._DB[key]
    none_event = _observe(lambda: module.SurfaceConstruction.get_DB(None), _encode)
    path_event = _observe(
        lambda: module.SurfaceConstruction.get_DB("__path__"),
        lambda value: _db_path_receipt(value, "construction_regulation_surface.csv"),
    )
    all_event = _observe(
        lambda: module.SurfaceConstruction.get_DB("__all__"),
        lambda values: {
            "count": len(values),
            "first": _surface_state(values[0]),
            "last": _surface_state(values[-1]),
            "states_sha256": canonical_sha256([_surface_state(value) for value in values]),
        },
    )
    item_event = _observe(lambda: module.SurfaceConstruction.get_DB(key), _surface_state)
    dict_event = _observe(lambda: module.SurfaceConstruction.get_DB(key, as_dict=True), _encode)
    invalid_event = _observe(lambda: module.SurfaceConstruction.get_DB(("19000101", "missing", "missing", "missing", "missing")))
    load_event = _observe(module.SurfaceConstruction.load_DB)
    return _fact(
        "S06",
        "surface",
        {
            "all": all_event,
            "as_dict": dict_event,
            "count_after_reload": len(module.SurfaceConstruction._DB),
            "first_key": _encode(key),
            "invalid": invalid_event,
            "item": item_event,
            "none": none_event,
            "object_replaced_on_reload": first_before is not module.SurfaceConstruction._DB[key],
            "path": path_event,
            "regulation_dates": [value.strftime("%Y%m%d") for value in module.SurfaceConstruction.REGULATION_DATES],
            "reload": load_event,
        },
        [
            _phase("get-none", none_event),
            _phase("get-path", path_event),
            _phase("get-all", all_event),
            _phase("get-first", item_event),
            _phase("get-first-dict", dict_event),
            _phase("get-invalid", invalid_event),
            _phase("reload", load_event),
        ],
    )


def _s07(module: ModuleType) -> dict[str, Any]:
    vintage = datetime(2020, 1, 1)
    combinations = (
        ("wall-outdoor", module.SurfaceType.WALL, module.SurfaceBoundaryCondition.OUTDOOR, False, False),
        ("ceiling-outdoor", module.SurfaceType.CEILING, module.SurfaceBoundaryCondition.OUTDOOR, False, False),
        ("ceiling-adjacent-radiant", module.SurfaceType.CEILING, module.SurfaceBoundaryCondition.ZONE, True, False),
        ("floor-ground", module.SurfaceType.FLOOR, module.SurfaceBoundaryCondition.GROUND, False, False),
        ("floor-outdoor-radiant", module.SurfaceType.FLOOR, module.SurfaceBoundaryCondition.OUTDOOR, True, True),
        ("floor-adjacent", module.SurfaceType.FLOOR, module.SurfaceBoundaryCondition.ZONE, False, True),
    )
    selections = []
    timeline = []
    for label, surface_type, boundary, radiant, multifamily in combinations:
        event = _observe(
            lambda surface_type=surface_type, boundary=boundary, radiant=radiant, multifamily=multifamily: module.SurfaceConstruction.get_regulated_construction(
                vintage,
                surface_type,
                boundary,
                "중부1",
                is_radiant_floor=radiant,
                is_multifamily_housing=multifamily,
            ),
            _surface_state,
        )
        selections.append({"label": label, "observation": event})
        timeline.append(_phase("select-" + label, event))
    before_range = _observe(
        lambda: module.SurfaceConstruction.get_regulated_construction(
            datetime(1900, 1, 1),
            module.SurfaceType.WALL,
            module.SurfaceBoundaryCondition.OUTDOOR,
            "중부1",
        )
    )
    missing_climate = _observe(
        lambda: module.SurfaceConstruction.get_regulated_construction(
            vintage,
            module.SurfaceType.WALL,
            module.SurfaceBoundaryCondition.OUTDOOR,
            "missing-climate",
        )
    )
    timeline.extend(
        [
            _phase("select-before-range", before_range),
            _phase("select-missing-climate", missing_climate),
        ]
    )
    return _fact(
        "S07",
        "surface",
        {
            "before_range": before_range,
            "missing_climate": missing_climate,
            "selections": selections,
            "vintage": vintage.strftime("%Y%m%d"),
        },
        timeline,
    )


def _reset_special_singletons(module: ModuleType) -> None:
    module.SpecialConstruction._instance = None
    for subtype in (module.OpenConstruction, module.UnknownConstruction):
        if "_instance" in subtype.__dict__:
            delattr(subtype, "_instance")


def _x01(module: ModuleType) -> dict[str, Any]:
    _reset_special_singletons(module)
    base_first = module.SpecialConstruction()
    inherited_open = module.OpenConstruction()
    base_first_aliases_open = base_first is inherited_open
    _reset_special_singletons(module)
    open_value = module.OpenConstruction()
    unknown_value = module.UnknownConstruction()
    special_a = module.SpecialConstruction()
    special_b = module.SpecialConstruction()
    reverse_special = _observe(special_a.reversed, lambda value: {"same_identity": value is special_a, "type": type(value).__name__})
    reverse_open = _observe(open_value.reversed, lambda value: {"same_identity": value is open_value, "type": type(value).__name__})
    reverse_unknown = _observe(unknown_value.reversed, lambda value: {"same_identity": value is unknown_value, "type": type(value).__name__})
    unique_event = _observe(special_a.get_unique_materials, _encode)
    return _fact(
        "X01",
        "special",
        {
            "base_first_aliases_open": base_first_aliases_open,
            "base_first_open_runtime_type": type(inherited_open).__name__,
            "cross_class_distinct": special_a is not open_value and open_value is not unknown_value,
            "open_reverse": reverse_open,
            "same_class_singleton": special_a is special_b,
            "special_reverse": reverse_special,
            "unique_materials": unique_event,
            "unknown_reverse": reverse_unknown,
        },
        [
            _phase("construct-base-before-open", {"outcome": "returned", "runtime_type": type(inherited_open).__name__, "value": _encode(base_first_aliases_open)}),
            _phase("construct-special-twice", {"outcome": "returned", "runtime_type": "bool", "value": _encode(special_a is special_b)}),
            _phase("get-unique-materials", unique_event),
            _phase("reverse-special", reverse_special),
            _phase("reverse-open", reverse_open),
            _phase("reverse-unknown", reverse_unknown),
        ],
    )


def _x02(module: ModuleType) -> dict[str, Any]:
    _reset_special_singletons(module)
    first = module.OpenConstruction()
    second = module.OpenConstruction()
    dragon_event = _observe(first.to_dragon, _dragon_construction_state)
    return _fact(
        "X02",
        "special",
        {
            "ID": first.ID,
            "dragon": dragon_event,
            "same_singleton": first is second,
            "type": type(first).__name__,
        },
        [
            _phase("construct-open", {"outcome": "returned", "runtime_type": type(first).__name__}),
            _phase("open-to-dragon", dragon_event),
        ],
    )


def _x03(module: ModuleType) -> dict[str, Any]:
    _reset_special_singletons(module)
    first = module.UnknownConstruction()
    second = module.UnknownConstruction()
    dragon_event = _observe(first.to_dragon, _encode)
    return _fact(
        "X03",
        "special",
        {
            "ID": first.ID,
            "dragon": dragon_event,
            "same_singleton": first is second,
            "type": type(first).__name__,
        },
        [
            _phase("construct-unknown", {"outcome": "returned", "runtime_type": type(first).__name__}),
            _phase("unknown-to-dragon", dragon_event),
        ],
    )


CASE_EXECUTORS = (
    _m01,
    _m02,
    _m03,
    _m04,
    _f01,
    _f02,
    _f03,
    _f04,
    _s01,
    _s02,
    _s03,
    _s04,
    _s05,
    _s06,
    _s07,
    _x01,
    _x02,
    _x03,
)


def _relocation_snapshot(module: ModuleType) -> dict[str, Any]:
    material = module.Material("Relocated", 0.45, 900, 950, ID="MAT-RELOCATED")
    surface = module.SurfaceConstruction("Relocated", material, 0.2, ID="SURF-RELOCATED")
    fenestration = module.FenestrationConstruction("Relocated", 1.7, 0.51, ID="FEN-RELOCATED")
    material_keys = list(module.Material._DB)
    surface_keys = list(module.SurfaceConstruction._DB)
    fenestration_keys = list(module.FenestrationConstruction._DB)
    return {
        "database_counts": {
            "fenestration": len(fenestration_keys),
            "material": len(material_keys),
            "surface": len(surface_keys),
        },
        "database_key_sha256": {
            "fenestration": canonical_sha256(_encode(fenestration_keys)),
            "material": canonical_sha256(_encode(material_keys)),
            "surface": canonical_sha256(_encode(surface_keys)),
        },
        "fenestration": _fenestration_state(fenestration),
        "material": _material_state(material),
        "open_ID": module.OpenConstruction.ID,
        "surface": _surface_state(surface),
        "unknown_ID": module.UnknownConstruction.ID,
    }


def _r01(primary: dict[str, Any], relocated: dict[str, Any], modules_equal: bool) -> dict[str, Any]:
    equality = primary == relocated
    event = {
        "outcome": "returned",
        "runtime_type": "bool",
        "value": _encode(equality and modules_equal),
    }
    return _fact(
        "R01",
        "relocation",
        {
            "loaded_module_receipts_equal": modules_equal,
            "primary_snapshot": primary,
            "relocated_snapshot_equal": equality,
            "source_copy_roles": ["primary-pinned-root", "byte-identical-relocated-root"],
        },
        [_phase("compare-relocated-import", event)],
    )


def _execute_cases(module: ModuleType) -> dict[str, dict[str, Any]]:
    identifiers = EXPECTED_CASE_IDS[:-1]
    if len(identifiers) != len(CASE_EXECUTORS):
        raise RuntimeError("Construction executor count drifted.")
    return {
        identifier: executor(module)
        for identifier, executor in zip(identifiers, CASE_EXECUTORS, strict=True)
    }


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {item["id"]: canonical_sha256(item) for item in cases}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _dependencies() -> dict[str, str]:
    observed = {}
    for distribution, expected in EXPECTED_DEPENDENCIES.items():
        try:
            observed[distribution] = importlib.metadata.version(distribution)
        except importlib.metadata.PackageNotFoundError as error:
            raise SystemExit(f"Pinned dependency is missing: {distribution}") from error
        if observed[distribution] != expected:
            raise SystemExit(
                f"Pinned dependency {distribution} drifted: {observed[distribution]}"
            )
    return observed


def _expected_runtime() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "platform": REQUIRED_PLATFORM,
        "pointer_width_bits": REQUIRED_POINTER_WIDTH_BITS,
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def _runtime_receipt() -> dict[str, Any]:
    return {
        "dependencies": _dependencies(),
        "implementation": sys.implementation.name,
        "platform": sys.platform,
        "pointer_width_bits": struct.calcsize("P") * 8,
        "python_dont_write_bytecode": sys.dont_write_bytecode,
        "python_hash_algorithm": sys.hash_info.algorithm,
        "python_hash_seed": int(os.environ.get("PYTHONHASHSEED", "-1")),
        "python_hash_width_bits": sys.hash_info.width,
        "python_version": ".".join(map(str, sys.version_info[:3])),
    }


def _validate_generation_runtime() -> None:
    if _runtime_receipt() != _expected_runtime():
        raise SystemExit(
            "The reference runtime is not exact.\nOBSERVED_RUNTIME\n"
            + strict_json_dumps(_runtime_receipt(), indent=2)
        )


def _expected_artifacts() -> dict[str, Any]:
    return {
        "bootstrap": {
            "bytes": EXPECTED_BOOTSTRAP_BYTES,
            "path": "tools/python-reference/bootstrap_reference.py",
            "sha256": EXPECTED_BOOTSTRAP_SHA256,
        },
        "strict_json_support": {
            "bytes": EXPECTED_SUPPORT_BYTES,
            "path": "tools/python-reference/generate_schedule_type_oracle.py",
            "sha256": EXPECTED_SUPPORT_SHA256,
        },
    }


def _validate_artifacts() -> None:
    for path, size, digest in (
        (BOOTSTRAP_PATH, EXPECTED_BOOTSTRAP_BYTES, EXPECTED_BOOTSTRAP_SHA256),
        (SUPPORT_PATH, EXPECTED_SUPPORT_BYTES, EXPECTED_SUPPORT_SHA256),
    ):
        if path.stat().st_size != size or sha256_file(path) != digest:
            raise SystemExit(f"Pinned reference artifact drifted: {path.name}")


def _coverage_by_symbol() -> dict[str, list[str]]:
    coverage = {symbol: [] for symbol in TARGET_SYMBOLS}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            coverage[symbol].append(definition["id"])
    return coverage


def _coverage_by_subfamily() -> dict[str, list[str]]:
    return {
        subfamily: [
            definition["id"]
            for definition in case_definitions()
            if definition["subfamily"] == subfamily
        ]
        for subfamily in ("material", "fenestration", "surface", "special", "relocation")
    }


def _expected_contract(runtime_signatures: dict[str, str] | None = None) -> dict[str, Any]:
    signatures = EXPECTED_RUNTIME_SIGNATURES or runtime_signatures
    if signatures is None:
        raise RuntimeError("Runtime signatures are unavailable for contract validation.")
    counts = Counter(CLASSIFICATIONS.values())
    return {
        "adaptations": ADAPTATIONS,
        "adjacent_policy": list(ADJACENT_POLICY),
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {
            "equivalent": counts["equivalent"],
            "exception": counts["exception"],
        },
        "classifications": CLASSIFICATIONS,
        "closure": {
            "exact_one_case_target_partition": True,
            "excluded_indices": [item["inventory_index"] for item in EXCLUDED_RECEIPTS],
            "excluded_symbols": list(EXCLUDED_SYMBOLS),
            "full_source_classification_partition": True,
            "target_count": len(TARGET_RECEIPTS),
            "target_indices": [item["inventory_index"] for item in TARGET_RECEIPTS],
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "coverage_by_subfamily": _coverage_by_subfamily(),
        "coverage_by_symbol": _coverage_by_symbol(),
        "evidence_contract": {
            "expected_receipt_count": 48,
            "full_idf_emission_closure": False,
            "target_coverage_complete": True,
        },
        "native_routes": NATIVE_ROUTES,
        "runtime_signatures": signatures,
    }


def _expected_upstream(loaded_modules: list[dict[str, str]] | None = None) -> dict[str, Any]:
    modules = EXPECTED_LOADED_LOCAL_MODULES or loaded_modules
    if modules is None:
        raise RuntimeError("Loaded-module receipts are unavailable.")
    return {
        "adjacent_exclusions": list(EXCLUDED_RECEIPTS),
        "artifacts": _expected_artifacts(),
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "database_resources": list(DATABASE_RESOURCES),
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "isolated_import": {
            "epsimple_package_initializer_executed": False,
            "epsimple_core_initializer_executed": False,
            "loaded_local_modules": modules,
            "relocated_source_copy": "byte-identical-epsimple-and-idragon-trees",
            "source_location_count": 2,
        },
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    if not isinstance(facts, dict) or set(facts) != {
        "observations",
        "scenario",
        "subfamily",
        "timeline",
    }:
        raise RuntimeError(f"Construction facts key set drifted: {identifier}")
    index = EXPECTED_CASE_IDS.index(identifier)
    spec = CASE_SPECS[index]
    if facts["scenario"] != spec[0] or facts["subfamily"] != spec[2]:
        raise RuntimeError(f"Construction facts identity drifted: {identifier}")
    if not isinstance(facts["observations"], dict) or not isinstance(facts["timeline"], list) or not facts["timeline"]:
        raise RuntimeError(f"Construction facts topology drifted: {identifier}")
    for event in facts["timeline"]:
        if not isinstance(event, dict) or event.get("outcome") not in {"raised", "returned"} or not isinstance(event.get("phase"), str):
            raise RuntimeError(f"Construction event topology drifted: {identifier}")


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
    _validate_artifacts()
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    _validate_source_tree(imported_root, inventory)

    with _isolated_import(imported_root, inventory) as (module, primary_modules):
        signatures = _runtime_signatures(module)
        if canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
            raise SystemExit("Pinned construction runtime-signature receipt drifted.")
        if EXPECTED_RUNTIME_SIGNATURES and signatures != EXPECTED_RUNTIME_SIGNATURES:
            raise SystemExit(
                "Pinned construction runtime signatures drifted.\nOBSERVED_SIGNATURES\n"
                + strict_json_dumps(signatures, indent=2)
            )
        observed = _execute_cases(module)
        primary_snapshot = _relocation_snapshot(module)

    repository_root = Path(__file__).resolve().parents[2]
    work_root = repository_root / "temp" / "reference" / "work"
    work_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="epsimple-construction-relocation-", dir=work_root
    ) as temporary:
        relocated_root = Path(temporary) / "src"
        _copy_source_tree(imported_root, relocated_root)
        _validate_source_tree(relocated_root, inventory)
        with _isolated_import(relocated_root, inventory) as (relocated_module, relocated_modules):
            relocated_signatures = _runtime_signatures(relocated_module)
            relocated_snapshot = _relocation_snapshot(relocated_module)
    if relocated_signatures != signatures:
        raise RuntimeError("Runtime signatures changed after source relocation.")
    modules_equal = primary_modules == relocated_modules
    if not modules_equal:
        raise RuntimeError("Loaded local module receipts changed after relocation.")
    if EXPECTED_LOADED_LOCAL_MODULES and primary_modules != EXPECTED_LOADED_LOCAL_MODULES:
        raise SystemExit(
            "Pinned loaded-module receipts drifted.\nOBSERVED_MODULES\n"
            + strict_json_dumps(primary_modules, indent=2)
        )
    if canonical_sha256(primary_modules) != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned loaded-module aggregate receipt drifted.")
    observed[EXPECTED_CASE_IDS[-1]] = _r01(
        primary_snapshot, relocated_snapshot, modules_equal
    )
    if list(observed) != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Construction observed case order drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts) for identifier, facts in observed.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned construction facts drifted.\nOBSERVED_FACT_HASHES\n"
            + strict_json_dumps(fact_hashes, indent=2)
        )
    if canonical_sha256(fact_hashes) != EXPECTED_FACT_MAP_SHA256:
        raise SystemExit("Pinned construction fact-map receipt drifted.")
    cases: list[dict[str, Any]] = []
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
            "Pinned construction case records drifted.\nOBSERVED_CASE_HASHES\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if canonical_sha256(case_hashes) != EXPECTED_CASE_MAP_SHA256:
        raise SystemExit("Pinned construction case-map receipt drifted.")
    aggregate = cases_sha256(cases)
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned construction aggregate case hash drifted.")

    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(signatures),
        "excluded_receipts": inventory["excluded_receipts"],
        "fact_sha256": fact_hashes,
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": _expected_upstream(primary_modules),
    }
    validate_oracle(result)
    return result


def _validate_safe_string(value: str, location: str) -> None:
    for pattern, label in (
        (RAW_ADDRESS_PATTERN, "raw address"),
        (WINDOWS_PATH_PATTERN, "absolute Windows path"),
        (POSIX_PATH_PATTERN, "absolute POSIX path"),
        (GUID_PATTERN, "GUID-like value"),
        (TIMESTAMP_PATTERN, "timestamp"),
    ):
        if pattern.search(value):
            raise RuntimeError(f"Forbidden {label} at {location}.")


def _validate_typed_value(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind is None:
        return False
    known = {"none", "bool", "int", "float", "float-nonfinite", "str", "list", "tuple", "dict"}
    if kind not in known:
        if set(value).issubset({"hex", "items", "kind", "repr", "value"}):
            raise RuntimeError(f"Unknown typed encoding {kind!r} at {location}.")
        return False
    if kind == "none":
        if set(value) != {"kind"}:
            raise RuntimeError(f"Noncanonical none encoding at {location}.")
    elif kind == "bool":
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
    elif kind == "float-nonfinite":
        if set(value) != {"kind", "value"} or value["value"] not in {
            "nan",
            "positive-infinity",
            "negative-infinity",
        }:
            raise RuntimeError(f"Noncanonical nonfinite float at {location}.")
    elif kind == "str":
        if set(value) != {"kind", "value"} or not isinstance(value["value"], str):
            raise RuntimeError(f"Noncanonical string encoding at {location}.")
        _validate_safe_string(value["value"], location + ".value")
    elif kind in {"list", "tuple"}:
        if set(value) != {"items", "kind"} or not isinstance(value["items"], list):
            raise RuntimeError(f"Noncanonical sequence encoding at {location}.")
        for index, item in enumerate(value["items"]):
            _validate_safe_tree(item, f"{location}.items[{index}]")
    elif kind == "dict":
        if set(value) != {"items", "kind"} or not isinstance(value["items"], list):
            raise RuntimeError(f"Noncanonical dict encoding at {location}.")
        keys: set[str] = set()
        for index, item in enumerate(value["items"]):
            if not isinstance(item, dict) or set(item) != {"key", "value"}:
                raise RuntimeError(f"Noncanonical dict item at {location}.items[{index}].")
            _validate_safe_tree(item["key"], f"{location}.items[{index}].key")
            _validate_safe_tree(item["value"], f"{location}.items[{index}].value")
            fingerprint = canonical_sha256(item["key"])
            if fingerprint in keys:
                raise RuntimeError(f"Duplicate encoded dict key at {location}.")
            keys.add(fingerprint)
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


def validate_oracle(value: dict[str, Any]) -> None:
    expected_keys = {
        "case_sha256",
        "cases",
        "cases_sha256",
        "consumer_contract",
        "excluded_receipts",
        "fact_sha256",
        "runtime",
        "schema",
        "symbols",
        "target_receipts",
        "upstream",
    }
    if not isinstance(value, dict) or set(value) != expected_keys:
        raise RuntimeError("Construction oracle root key set drifted.")
    if value["schema"] != SCHEMA:
        raise RuntimeError("Construction oracle schema drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Construction oracle runtime receipt drifted.")
    if value["target_receipts"] != list(TARGET_RECEIPTS):
        raise RuntimeError("Construction target receipts drifted.")
    if value["symbols"] != [_descriptor(item) for item in TARGET_RECEIPTS]:
        raise RuntimeError("Construction target symbol descriptors drifted.")
    if value["excluded_receipts"] != list(EXCLUDED_RECEIPTS):
        raise RuntimeError("Construction excluded receipts drifted.")
    loaded = value.get("upstream", {}).get("isolated_import", {}).get("loaded_local_modules")
    if not isinstance(loaded, list) or canonical_sha256(loaded) != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise RuntimeError("Construction loaded-module aggregate receipt drifted.")
    if value["upstream"] != _expected_upstream(loaded):
        raise RuntimeError("Construction upstream receipt drifted.")
    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict) or canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise RuntimeError("Construction runtime-signature aggregate receipt drifted.")
    if value["consumer_contract"] != _expected_contract(signatures):
        raise RuntimeError("Construction consumer contract drifted.")
    if not isinstance(value["cases"], list) or len(value["cases"]) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Construction case count drifted.")
    definitions = case_definitions()
    if [item["id"] for item in value["cases"]] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Construction case order drifted.")
    for case, definition in zip(value["cases"], definitions, strict=True):
        if set(case) != {*definition, "python"}:
            raise RuntimeError(f"Construction case key set drifted: {definition['id']}")
        for key, expected in definition.items():
            if case[key] != expected:
                raise RuntimeError(f"Construction case definition drifted: {definition['id']}")
        python = case["python"]
        if not isinstance(python, dict) or set(python) != {"facts", "facts_sha256", "outcome"} or python["outcome"] != "observed":
            raise RuntimeError(f"Construction Python observation drifted: {definition['id']}")
        _validate_case_facts(definition["id"], python["facts"])
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest or value["fact_sha256"].get(definition["id"]) != digest:
            raise RuntimeError(f"Construction fact hash drifted: {definition['id']}")
    actual_case_hashes = case_sha256(value["cases"])
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("Construction per-case hash map drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Construction aggregate case hash drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("Construction pinned fact hashes drifted.")
    if canonical_sha256(value["fact_sha256"]) != EXPECTED_FACT_MAP_SHA256:
        raise RuntimeError("Construction pinned fact-map receipt drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("Construction pinned case hashes drifted.")
    if canonical_sha256(value["case_sha256"]) != EXPECTED_CASE_MAP_SHA256:
        raise RuntimeError("Construction pinned case-map receipt drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Construction pinned aggregate hash drifted.")
    target_counts = Counter(
        symbol for case in value["cases"] for symbol in case["target_symbols"]
    )
    if target_counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Construction exact target closure drifted.")
    all_case_symbols = {
        symbol
        for case in value["cases"]
        for symbol in (*case["target_symbols"], *case["context_symbols"])
    }
    if all_case_symbols.intersection(EXCLUDED_SYMBOLS):
        raise RuntimeError("An excluded symbol was promoted by a case.")
    _validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Construction oracle strict JSON round trip drifted.")


def load_json_without_duplicates_text(text: str) -> dict[str, Any]:
    import json

    class DuplicateKeyError(ValueError):
        pass

    def hook(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result = {}
        for key, item in pairs:
            if key in result:
                raise DuplicateKeyError(key)
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
        raise SystemExit("Persisted construction oracle is not byte-identical.")
    print(
        f"Generated {args.output} with {len(TARGET_RECEIPTS)} exact targets, "
        f"{len(oracle['cases'])} cases, and aggregate {oracle['cases_sha256']}."
    )


if __name__ == "__main__":
    main()

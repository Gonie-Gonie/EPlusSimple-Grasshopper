"""Generate pinned facts for Dragon's historical geometry-core contracts.

The fourteen cases form three explicit subfamilies: seven Vertex/algebra and
coplanarity cases, five Surface scalar-geometry cases, and two SurfaceType
cases.  Opening, adjacency, IDF-emission, and out-of-scope representation or
equality symbols remain context-only or excluded.
"""

from __future__ import annotations

import argparse
from collections import Counter
from copy import deepcopy
import functools
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import sys
from typing import Any, Callable


SCHEMA = "dragons.python-reference.dragon-shape-geometry-core.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
SHAPE_SOURCE_PATH = "src/idragon/dragon/shape.py"
SHAPE_SOURCE_SHA256 = (
    "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c"
)
SHAPE_AST_SHA256 = (
    "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"
)

# index, symbol, kind, symbol hash, signature hash, body hash
_RECEIPT_ROWS = (
    (1034, "Surface", "class", "cb620c55ad36aaa035597b8c9975721d7fd397a000213beae556880050f75dba", "570bcca6296bf984b2732617159dc1d1b13c10126d72c363fbd6058b9aa3e6bf", "926a3131b4eecef848f3f1fca552718277fa4340c9b34aca7db597364c57df1f"),
    (1038, "Surface.area", "function", "f254ab666c61170d9ea16598a4182e7f49526eb4e0eaff0af293499695cbd9fa", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "a562982884a4f5e17de2537275772db7d9600b609ed527d0fb20966f4f1c0d58"),
    (1041, "Surface.center", "function", "f0c05c2bc1bd07b18d9140cafa7f970129215c9aa311f80a0073445b92526273", "758f0228871f1c7811c457dd084ec9436eefb5e60aac482d304c0646a5f803f0", "8773235ee6e9cd4ea33c6d93b8289dd7a7bc3ce44e7ccaec3ce469719395716f"),
    (1043, "Surface.height", "function", "d479fe2f2ded1a09be3f2686e3ee6306b96beccb8cc20f04def11be7c0712f55", "a42366eda169c0da7d82ff37d52c5efd8af8f131bc96546d7c925077ed2036e4", "c6fcbfd9ae4872946ffeecb5b90f49babe04c57e3a3999fe4353a66666869230"),
    (1044, "Surface.normal", "function", "3f089c8c429d26cd3ee65ff085dea58b961a0ec0c4b9b757172f65bf42a8b7e7", "758f0228871f1c7811c457dd084ec9436eefb5e60aac482d304c0646a5f803f0", "de6322fc22827c75a81be55ee33b7d86367f4e7619e5c61ae6bbd6dd09969fe8"),
    (1046, "Surface.type", "function", "ae4bdcc76210c35b23978d30c1d57491785d9fa9a2a66e80cc123e7c633a2db5", "8044a015cf023f600bfb62367bd05f9fb767cf01534d3432f781bbf466084b16", "5c4cda2372327676ed37a856be4b27f0f64d0c5846b3ca2523ea9665d5651313"),
    (1047, "Surface.vertex", "function", "7ed5c6b3be62b893275d7dedccacd8cc2a85e7d0862801001a67650330ac2be8", "7d427b018243593f11def8ee612f23ac830a5a61aea07c16449702d14d2ce9b4", "6d481bd915484732dfe6bedcd08e09c4b0c1f3ee6ba47001210ad0238f8ab7e3"),
    (1054, "SurfaceType", "class", "61a37f9dc7fea0761d67c6e8efbd3ef6ef7e6e75788e8bcec26784d2a9bbf1a3", "db178ad05149cc0a5f8e817db69cd413099b8604c60b139f9a8603d7522744a5", "643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726"),
    (1055, "SurfaceType.CEILING", "constant", "9ece83237cbba05bedb4f1f349b4505dba0a06a6d8e661bbb2e51485c0a28c4c", "2a36b1b600c86dd06a1f51523c5562edd967304930f43e09d0d0dcd555ed23d7", "f90f71b95564dd2dd802153760314b74dde7e11adc8daa18c55b696c6f10e914"),
    (1056, "SurfaceType.FLOOR", "constant", "c8c4f240e476a6db7cc85ca0bfcaea675233b72f28019edd4308f11cb689e01b", "909756f308b102264b0588f914f69542d69da96738233ca4fbb92a838d087bea", "37194ca6121ae832d5c991164c74dd662b39ba10da745ebc418aef2d1a834e5a"),
    (1057, "SurfaceType.WALL", "constant", "ca6d5593884470ef294f9e38f3e03f945136bb49d08ef1e6fa9d08d5cac35cf4", "df01e4736e1699406341a3ee335a4f9131b888ade81d8a1a5781ba152ae3bf65", "d0a6a6d9c9b4333e9f62641d948701347778a44a64f2db44b9d1f6dd8bde1aff"),
    (1058, "SurfaceType.__str__", "function", "f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e", "f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab", "5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8"),
    (1059, "Vertex", "class", "786502893a6774ddb9c263e2ce3de1037c9f88f8dde30c32f04ad6e1418f0b64", "b1fc2f021d39b52f7fdf7c9fef986a60b73e5b86dc26bbe4b00dedc0cf5c4f17", "5ebda0e1f32f1fd86c57fc26145879526562d3c03795f2dceb5fbfcf00544a72"),
    (1060, "Vertex.__add__", "function", "a5c7ecea4df4c83044d8b673c72a7352e3121627c2aafe1f6e99a3ffba35977e", "0b34e90dfcf0e856807608a50fd75c29f13fe6b59fb8c1770d465590c56f6ef8", "8c3de0950e49fa12688e9d4d9c9762768c1e5590dc4ec407d7ea446a11cf4f0f"),
    (1061, "Vertex.__deepcopy__", "function", "2c79da1a720680314133fb5aebf7c420f8586bd91d402a578b6797f0833b7f85", "6fdfdefd8e1f58c6a42b3d6022896a8dabcd8576ae421e727a5662fd45da8c58", "81f41f4035c79daddde1320ce8f8285f29d7b1a6b54ab19e60f48a89c11cca22"),
    (1063, "Vertex.__init__", "function", "be3c69c5422b57d538899edd108fb477fcb0766fdea42e53f6e6ca25ae838ac3", "39724fa8eb687875f0df66ecc43c3a3681896413da32edd88babedfcafb38aa2", "acc791c29de051e80a5e0e5abe4d3b37dc788ff78231ee1f84faa7121755a4e6"),
    (1064, "Vertex.__iter__", "function", "e95d7ce5aa55d56bc0012c191bc98fc7cd74941f724816583108f53e9bda37e7", "a100b1521302f5a4be62ff692f110f299cc3b33f4d633fae0968c7054d76051b", "235dfb710a8b427a949ffdaba55989daa05a5467bbcd2bb625e6441ae6506649"),
    (1065, "Vertex.__mul__", "function", "323878e160b4a3f298740187d6136d8d8e9c112ae6f7097dccc7fd9d4be57747", "eead1105b53d5053c1389ede1e8718c2eebe7c78cd9f6a3e9989b8e665b1bf41", "83a388a00380d4a43bab7d21857dbd57886c74559980ec71fcbb7fd86eca662e"),
    (1066, "Vertex.__radd__", "function", "a473d0f327d8b3055e2e614d0e6da54681e058469f4d3266ac69d0849849dd35", "0b34e90dfcf0e856807608a50fd75c29f13fe6b59fb8c1770d465590c56f6ef8", "27ca274fd8c8adccd65b61c31f4fb234fa0597a63e30eb23d237ba6f4857915c"),
    (1068, "Vertex.__rmul__", "function", "1dbe33d37c8ebeda67422c7b71c99b4314290005faaab35c11a6d62446da88ef", "eead1105b53d5053c1389ede1e8718c2eebe7c78cd9f6a3e9989b8e665b1bf41", "deaef8f9df40bb8ed6eb2dbd9bbd9be5bfcfbb2a5f989592308bde8e8f3cfc4f"),
    (1070, "Vertex.__sub__", "function", "4ee38e65b625fbec9e82d2cf2497d08bc3569dd9f19bb9d68500823113b2a9fc", "adec158f0b08785de53c534711342a08a6615b6a64fa56ad349652df955f9117", "4e0eaec417cf72093e0a90ceae1895df9ec9faa3b7b4ff3c4dcd7c69049c8161"),
    (1071, "Vertex.__truediv__", "function", "94f397b889c7022f9e61270308cb32f2994fa6336aa0034bf6af9f73fa05ee53", "eead1105b53d5053c1389ede1e8718c2eebe7c78cd9f6a3e9989b8e665b1bf41", "fd9873b2ccd6e62b270feaf054790d38d7c7b908c5eb4f33f96c13f3924aae75"),
    (1072, "Vertex.are_coplanar", "function", "905ebbf25f731adcf96fd59e0ee78f8afda0e325ed624baa9e0124cc3a5da493", "7be14f957bc48e96bae40454c83374af2b60d403fe48462c29c7b230debf7e19", "56358e3c2ceccce0c3bea4251071e545f085ba3ac12c7e67889af47901603ef3"),
    (1073, "Vertex.cross", "function", "6bc5db49d054daacb8f76e26342f1a6f45ccbdffdc1119addebe8e18ccbad02a", "adec158f0b08785de53c534711342a08a6615b6a64fa56ad349652df955f9117", "2230ee104aae5a223bb3bc01226737df630ece57a4b82431d159f9b1713d6fc2"),
    (1074, "Vertex.distance", "function", "88c4cb9fbd03fc69d540cf3b644516743673077ab0ec7540c84a767eaca902cc", "569df6a5f374ddb3ab8f3639f6b20f67c2cdeac646b5e466fa0a30abc63bf4f0", "bb92daef11c92597c835b9e59a5dafddba4087552e828196595bb123a588a24a"),
    (1075, "Vertex.dot", "function", "1aaf5930f9dbfec62d7999fc240ee947eaa0397482c417da92d23ea51d79cc87", "8b6676a26cd4d89db3c842512e2bbb89318f331b84cc563c81a6163c4de9a41c", "886a9cd2804a3444fd71aaf9a4813692f51559ee541da3df44733244d2f19b03"),
    (1076, "Vertex.norm", "function", "e41eae31e96f574bb148c14e0e8f19d03302136144b6e43baf73a18bfa678b49", "2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb", "096687da4f4b02a9c7ec12d7156245accf2ef86da9cbf4be0b05e28a5f2ddf4e"),
    (1077, "Vertex.unit", "function", "4267bc06a7a7d67fece4bdcb4963be1e87dd65436d8cecacce017bd19cf8c756", "2ee623b35ab3aacb49e23aff07dd62f5cbcb8efcfa87d52572a74a57b32ebcfb", "78c3a37b25dd7def0de8575181b283a5f240201d40991df19edc538a271cacab"),
    (1078, "Vertex.x", "function", "d859bad0320353e43a2fc277a54559f90cbcf19e91d3d5b49e0ec77a98da5125", "46ed90dbe20788ec581fc97c8027d66792fdc63ad8cf0702b3e84a8a69db3b35", "ecb4351565fd2434784b488f3f7faa82b7ebdc52c0c46698a71fb80b5a0496aa"),
    (1079, "Vertex.y", "function", "ff0bcc126b70820f4cd15e2d743102715885b2f19b3df6662eaba221a54f6e4c", "83e3d9391df015016420796f049ffde5c068bd6aa96d53568378dc723c8378fe", "ef8438299afc4f99a72230048fe2ae093565a58ebf9c57237f52c91abdd0531e"),
    (1080, "Vertex.z", "function", "64899affcdb0d27b23069a9323ba7e71ae572ecddadb121c28051a8d279fcfc5", "6763f7596780d07ccc7b400fd60c35cf716e7acc81fccb83ed5f5ad9cc2e7538", "9afbb156e7e4dbb655341601471eca492d18f0484e157bfb73d6e7e1db309158"),
)
TARGET_RECEIPTS = tuple(
    {
        "body_hash": "sha256:" + body_hash,
        "inventory_index": index,
        "kind": kind,
        "signature_hash": "sha256:" + signature_hash,
        "symbol": symbol,
        "symbol_hash": "sha256:" + symbol_hash,
    }
    for index, symbol, kind, symbol_hash, signature_hash, body_hash in _RECEIPT_ROWS
)
TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)
EQUIVALENT_SYMBOLS = (
    "SurfaceType.CEILING",
    "SurfaceType.FLOOR",
    "SurfaceType.WALL",
)
CLASSIFICATIONS = {
    symbol: "equivalent" if symbol in EQUIVALENT_SYMBOLS else "exception"
    for symbol in TARGET_SYMBOLS
}

ADAPTATIONS = {
    "Surface": "permissive-python-surface-polygon-model",
    "Surface.area": "first-triple-oriented-python-surface-area",
    "Surface.center": "vertex-mean-python-surface-center",
    "Surface.height": "z-span-python-surface-height",
    "Surface.normal": "first-triple-python-surface-normal",
    "Surface.type": "mutable-string-coerced-python-surface-type",
    "Surface.vertex": "aliased-mutable-python-surface-vertices",
    "SurfaceType": "lowercase-python-surface-type-enum",
    "SurfaceType.CEILING": "direct-surface-type-member-mapping",
    "SurfaceType.FLOOR": "direct-surface-type-member-mapping",
    "SurfaceType.WALL": "direct-surface-type-member-mapping",
    "SurfaceType.__str__": "lowercase-python-surface-type-enum",
    "Vertex": "permissive-mutable-python-vertex-state",
    "Vertex.__add__": "untyped-python-vertex-algebra",
    "Vertex.__deepcopy__": "python-vertex-copy-iteration-zero-addition",
    "Vertex.__init__": "permissive-mutable-python-vertex-state",
    "Vertex.__iter__": "python-vertex-copy-iteration-zero-addition",
    "Vertex.__mul__": "untyped-python-vertex-algebra",
    "Vertex.__radd__": "python-vertex-copy-iteration-zero-addition",
    "Vertex.__rmul__": "untyped-python-vertex-algebra",
    "Vertex.__sub__": "untyped-python-vertex-algebra",
    "Vertex.__truediv__": "untyped-python-vertex-algebra",
    "Vertex.are_coplanar": "legacy-first-triple-angular-coplanarity",
    "Vertex.cross": "untyped-python-vertex-metrics",
    "Vertex.distance": "untyped-python-vertex-metrics",
    "Vertex.dot": "untyped-python-vertex-metrics",
    "Vertex.norm": "untyped-python-vertex-metrics",
    "Vertex.unit": "zero-preserving-python-vertex-unit",
    "Vertex.x": "permissive-mutable-python-vertex-state",
    "Vertex.y": "permissive-mutable-python-vertex-state",
    "Vertex.z": "permissive-mutable-python-vertex-state",
}
ASSERTION_IDS = {
    item["symbol"]: (
        f"dragon-shape-geometry-core-{item['inventory_index']}-"
        f"{item['symbol_hash'][7:15]}"
    )
    for item in TARGET_RECEIPTS
}
NATIVE_TARGETS = {
    "Surface": "Dragons.InvisibleDragon.Shape.Surface plus PlanarPolygon",
    "Surface.area": "Surface.GrossArea via PlanarPolygon.Area",
    "Surface.center": "Surface.Center via PlanarPolygon.Centroid",
    "Surface.height": "Surface.Height via PlanarPolygon.Height",
    "Surface.normal": "Surface.Normal via PlanarPolygon.Normal",
    "Surface.type": "Surface.Type immutable enum property",
    "Surface.vertex": "Surface.Polygon.Vertices immutable defensive copy",
    "SurfaceType": "Dragons.InvisibleDragon.Shape.SurfaceType",
    "SurfaceType.CEILING": "SurfaceType.Ceiling",
    "SurfaceType.FLOOR": "SurfaceType.Floor",
    "SurfaceType.WALL": "SurfaceType.Wall",
    "SurfaceType.__str__": "explicit native enum-to-IDF mapping where required",
    "Vertex": "Dragons.InvisibleDragon.Shape.Vertex readonly struct",
    "Vertex.__add__": "Vertex plus Vector3 operator",
    "Vertex.__deepcopy__": "Vertex value-copy semantics",
    "Vertex.__init__": "Vertex constructor with finite double guards",
    "Vertex.__iter__": "explicit X/Y/Z projection",
    "Vertex.__mul__": "Vector3 scalar multiplication after point-to-vector adaptation",
    "Vertex.__radd__": "explicit identity/copy adaptation",
    "Vertex.__rmul__": "Vector3 scalar multiplication after point-to-vector adaptation",
    "Vertex.__sub__": "Vertex minus Vertex returns Vector3",
    "Vertex.__truediv__": "Vector3 scalar division after point-to-vector adaptation",
    "Vertex.are_coplanar": "Vertex.AreCoplanar with geometric distance tolerance",
    "Vertex.cross": "Vector3.Cross after point-to-vector adaptation",
    "Vertex.distance": "Vertex.DistanceTo",
    "Vertex.dot": "Vector3.Dot after point-to-vector adaptation",
    "Vertex.norm": "Vector3.Length after point-to-vector adaptation",
    "Vertex.unit": "Vector3.Normalize with zero-vector rejection",
    "Vertex.x": "Vertex.X immutable finite double",
    "Vertex.y": "Vertex.Y immutable finite double",
    "Vertex.z": "Vertex.Z immutable finite double",
}

RUNTIME_SIGNATURES = {
    "Surface": "(name: 'str', type: 'SurfaceType | str', construction: 'Construction', boundary: 'str', vertex: 'list[Vertex]', window: 'list[Window]' = [], door: 'list[Door]' = []) -> 'None'",
    "Surface.area": "property:fget=(self) -> 'float'",
    "Surface.center": "property:fget=(self) -> 'Vertex'",
    "Surface.height": "property:fget=(self) -> 'float'",
    "Surface.normal": "property:fget=(self) -> 'Vertex'",
    "Surface.type": "property:fget=(self);fset=(self, value: 'str')",
    "Surface.vertex": "property:fget=(self);fset=(self, value: 'list[Vertex]')",
    "SurfaceType": "(*values)",
    "SurfaceType.CEILING": "enum-member:'ceiling'",
    "SurfaceType.FLOOR": "enum-member:'floor'",
    "SurfaceType.WALL": "enum-member:'wall'",
    "SurfaceType.__str__": "(self) -> 'str'",
    "Vertex": "(x: 'int | float' = 0, y: 'int | float' = 0, z: 'int | float' = 0) -> 'None'",
    "Vertex.__add__": "(self, other: 'Vertex | int') -> 'Vertex'",
    "Vertex.__deepcopy__": "(self, memo) -> 'Vertex'",
    "Vertex.__init__": "(self, x: 'int | float' = 0, y: 'int | float' = 0, z: 'int | float' = 0) -> 'None'",
    "Vertex.__iter__": "(self)",
    "Vertex.__mul__": "(self, value: 'int | float') -> 'Vertex'",
    "Vertex.__radd__": "(self, other: 'Vertex | int') -> 'Vertex'",
    "Vertex.__rmul__": "(self, value: 'int | float') -> 'Vertex'",
    "Vertex.__sub__": "(self, other: 'Vertex') -> 'Vertex'",
    "Vertex.__truediv__": "(self, value: 'int | float') -> 'Vertex'",
    "Vertex.are_coplanar": "(*args: 'Vertex') -> 'bool'",
    "Vertex.cross": "(self, other: 'Vertex') -> 'Vertex'",
    "Vertex.distance": "(self, other: 'Vertex') -> 'float'",
    "Vertex.dot": "(self, other: 'Vertex') -> 'int | float'",
    "Vertex.norm": "property:fget=(self)",
    "Vertex.unit": "property:fget=(self)",
    "Vertex.x": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Vertex.y": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
    "Vertex.z": "property:fget=(self) -> 'int | float';fset=(self, value: 'int | float') -> 'None'",
}

PREFIX = "dragon-shape-geometry-core."
CASE_SPECS = (
    ("v01-vertex-domain-mutable-state", "vertex", ("Vertex", "Vertex.__init__", "Vertex.x", "Vertex.y", "Vertex.z"), ()),
    ("v02-vertex-copy-iteration-zero-radd", "vertex", ("Vertex.__deepcopy__", "Vertex.__iter__", "Vertex.__radd__"), ("Vertex",)),
    ("v03-vertex-point-vector-arithmetic", "vertex", ("Vertex.__add__", "Vertex.__mul__", "Vertex.__rmul__", "Vertex.__sub__", "Vertex.__truediv__"), ("Vertex",)),
    ("v04-vertex-operator-error-timing", "vertex", ("Vertex.__add__", "Vertex.__mul__", "Vertex.__radd__", "Vertex.__truediv__"), ("Vertex", "Vertex.__rmul__")),
    ("v05-vertex-vector-metrics-zero-unit", "vertex", ("Vertex.cross", "Vertex.distance", "Vertex.dot", "Vertex.norm", "Vertex.unit"), ("Vertex",)),
    ("v06-vertex-coplanarity-angular-threshold", "vertex", ("Vertex.are_coplanar",), ("Vertex", "Vertex.__sub__", "Vertex.cross", "Vertex.unit", "Vertex.dot")),
    ("v07-vertex-coplanarity-first-three-collinear-defect", "vertex", ("Vertex.are_coplanar",), ("Vertex", "Vertex.__sub__", "Vertex.cross", "Vertex.unit")),
    ("s08-surface-rectangle-scalar-geometry", "surface", ("Surface", "Surface.area", "Surface.center", "Surface.height", "Surface.normal", "Surface.type", "Surface.vertex"), ("Surface.__init__", "Vertex")),
    ("s09-surface-reversed-winding", "surface", ("Surface.area", "Surface.center", "Surface.height", "Surface.normal"), ("Surface.__init__", "Vertex", "Vertex.dot")),
    ("s10-surface-concave-reflex-first-turn-negative-area", "surface", ("Surface.area", "Surface.normal"), ("Surface.__init__", "Vertex", "Vertex.cross", "Vertex.__radd__", "Vertex.__add__", "Vertex.dot")),
    ("s11-surface-invalid-polygon-acceptance", "surface", ("Surface", "Surface.area", "Surface.normal", "Surface.vertex"), ("Surface.__init__", "Vertex", "Vertex.are_coplanar")),
    ("s12-surface-vertex-alias-mutation-and-setter-errors", "surface", ("Surface.area", "Surface.center", "Surface.height", "Surface.normal", "Surface.type", "Surface.vertex"), ("Surface.__init__", "Vertex")),
    ("t13-surface-type-enum-string-topology", "surface-type", ("SurfaceType", "SurfaceType.CEILING", "SurfaceType.FLOOR", "SurfaceType.WALL", "SurfaceType.__str__"), ()),
    ("t14-surface-type-conversion-error-topology", "surface-type", ("SurfaceType", "SurfaceType.__str__"), ("SurfaceType.CEILING", "SurfaceType.FLOOR", "SurfaceType.WALL")),
)
EXPECTED_CASE_IDS = tuple(PREFIX + item[0] for item in CASE_SPECS)
EXPECTED_CASE_COUNT = 14
EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:904509ce52c0b486f82f1f130c63cd9f13b8d29987704c3c0fecf70250fb414f",
    EXPECTED_CASE_IDS[1]: "sha256:7680009ebbee5bfe61ad9bd2f497a4c0ae9b42dcb9fe422790796b0f0a98c02e",
    EXPECTED_CASE_IDS[2]: "sha256:dc6c1311ec1fe99e3c2e717157233427591ed85e3368abfeabe228914caca7a2",
    EXPECTED_CASE_IDS[3]: "sha256:3b87113455f92d8aa78515880ae94b03e4cdcd71c9cca302797d2fee77067166",
    EXPECTED_CASE_IDS[4]: "sha256:3a0f873d1d743750db80dbee692ab6c1aed2d2ac09206243250e62453f77964f",
    EXPECTED_CASE_IDS[5]: "sha256:486dc8e1c2705160ee637f0969f2fdff6ef09f221752d0a41759c337485bd5d4",
    EXPECTED_CASE_IDS[6]: "sha256:56bde4fb6e5fa9d5fedd1bc17781abf8837dc9f29c545ff6fa1606772a40cdce",
    EXPECTED_CASE_IDS[7]: "sha256:539b0710520bf4c4a14b8e6b1dff08dc2cbabd22934f9b66083b232c7b7fcf0f",
    EXPECTED_CASE_IDS[8]: "sha256:68dc77a160f95b446be3c9ef4167adb4e75c96dc66a619e43a8dad27bb1841b9",
    EXPECTED_CASE_IDS[9]: "sha256:807e123999c4e67b21b1d4f7f6fdd6bf709a3a98507ac7554a42302019b21b7c",
    EXPECTED_CASE_IDS[10]: "sha256:aa93eef166452655a444e6b1868322ff07084345c31c99f5422bfedadbc6d7a7",
    EXPECTED_CASE_IDS[11]: "sha256:7f2da067edcf9230cc17b6bf7e448975dfb8aea9fbc781871907ec5eca3ba66f",
    EXPECTED_CASE_IDS[12]: "sha256:f5251fb2e0a46f95621b7ca0f458ef61efad0aee3a4fe05dbed26f14f70c0a80",
    EXPECTED_CASE_IDS[13]: "sha256:cb4c8b03cb4a2207eb499e0d38e5c25eee2f7159dfc1be11478ad8e19f6a3a7a",
}
EXPECTED_CASE_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:fb9fd9b854743b7eedf56c21dd178bcdfab0943cab1b4237ab9288510070acd1",
    EXPECTED_CASE_IDS[1]: "sha256:a3562158798dfcb9b2482c11bb187086dac404bb56b791e4dd73ca72a962464d",
    EXPECTED_CASE_IDS[2]: "sha256:d8f34a82a16391847fa791d76675cbf7aba9ebcc0446e2d682e03f88ecc5bcc6",
    EXPECTED_CASE_IDS[3]: "sha256:910a2f90944c03a01a33df17abed33bc2819cb721f50bb0fe5af055acd4dec47",
    EXPECTED_CASE_IDS[4]: "sha256:6ecdf3b360223b0d2972fbe212d96d601ee38a3039081cf819be9791016e139a",
    EXPECTED_CASE_IDS[5]: "sha256:88da80328380e29b8d2813e736c4bfd44aa6451f438ea0dfd6f3c0da36090872",
    EXPECTED_CASE_IDS[6]: "sha256:eed56b138d4c5e6dc4ab8ccace69c356137ef91f440793d055a3e335291f4e50",
    EXPECTED_CASE_IDS[7]: "sha256:11350d69b5fa99127ab43938b8edc9376cf4a7fdc1f8efccee6fcecc83bc4cc1",
    EXPECTED_CASE_IDS[8]: "sha256:5dc3ef16751f34401aaa608718bce6e01ca209914749168412b98a66b76d380a",
    EXPECTED_CASE_IDS[9]: "sha256:54a1e4b8b9c02583ad03e1c0421cbcc94fc4d1f0a6535f7c806c2b55b926d72e",
    EXPECTED_CASE_IDS[10]: "sha256:161291e9b0287d395f1d639c2e5bc03e49ea6977df36b7468e6449a336c3c5c8",
    EXPECTED_CASE_IDS[11]: "sha256:912e648311f5808094fb0c0ea689be6aa03628691422e79b5d0ccce8422b745c",
    EXPECTED_CASE_IDS[12]: "sha256:03d049d473c467138691d7c6aa3bcd70da99b1fa5178ad5598ef975b61a7c055",
    EXPECTED_CASE_IDS[13]: "sha256:ec5dabd2f1f0e30bba726e23b11b4ed33b681b91b6bcfd6f2d8a7bfa8c8cc3bd",
}

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_shape_geometry_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load geometry oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("Geometry oracle support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
SOURCE_RECEIPTS = CORE.SOURCE_RECEIPTS
SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == SHAPE_SOURCE_PATH else (),
    }
    for path, ast_hash, source_hash in SOURCE_RECEIPTS
)
EXPECTED_DEPENDENCIES = CORE.EXPECTED_DEPENDENCIES
strict_json_dumps = CORE.strict_json_dumps
canonical_sha256 = CORE.canonical_sha256
sha256_file = CORE.sha256_file
load_json_without_duplicates = CORE.load_json_without_duplicates
RAW_ADDRESS_PATTERN = CORE.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = CORE.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = CORE.GUID_PATTERN
TIMESTAMP_PATTERN = CORE.TIMESTAMP_PATTERN


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {
        key: value for key, value in receipt.items() if key != "inventory_index"
    } | {"path": SHAPE_SOURCE_PATH}


def _expected_symbol_descriptors() -> list[dict[str, Any]]:
    return [_descriptor(item) for item in TARGET_RECEIPTS]


def _expected_target_receipts() -> list[dict[str, Any]]:
    return [
        {**_descriptor(item), "inventory_index": item["inventory_index"]}
        for item in TARGET_RECEIPTS
    ]


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    expected = {item["symbol"]: item for item in _expected_symbol_descriptors()}
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
    items = [_load_source_inventory(path, commit, source) for source in SOURCE_SPECS]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in items):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    expected_receipts = _expected_target_receipts()
    observed = [
        {**raw["symbols"][item["inventory_index"]], "inventory_index": item["inventory_index"]}
        for item in expected_receipts
    ]
    if observed != expected_receipts:
        raise SystemExit("Exact indexed geometry target receipts drifted.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in items],
        "symbols": [symbol for item in items for symbol in item["symbols"]],
        "target_receipts": observed,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    result = []
    for slug, subfamily, targets, context in CASE_SPECS:
        result.append(
            {
                "context_symbols": list(context),
                "executor": "shape-geometry-core",
                "expected_dotnet": {
                    "adaptations": sorted({ADAPTATIONS[symbol] for symbol in targets}),
                    "classifications": {
                        symbol: CLASSIFICATIONS[symbol] for symbol in targets
                    },
                    "outcome": "adapted-or-equivalent-as-pinned",
                },
                "id": PREFIX + slug,
                "subfamily": subfamily,
                "target_symbols": list(targets),
            }
        )
    return tuple(result)


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
    raise RuntimeError(f"Unsupported geometry scalar: {type(value).__name__}")


def _vertex(value: Any) -> list[dict[str, Any]]:
    return [_encode(value.x), _encode(value.y), _encode(value.z)]


def _vertices(values: Any) -> list[list[dict[str, Any]]]:
    return [_vertex(value) for value in values]


def _error(call: Callable[[], Any], phase: str) -> dict[str, Any]:
    try:
        value = call()
    except Exception as error:
        return {
            "error": {"message": str(error), "type": type(error).__name__},
            "outcome": "raised",
            "phase": phase,
        }
    return {
        "outcome": "returned",
        "phase": phase,
        "return_type": type(value).__name__,
        "returned_none": value is None,
    }


def _fact(
    scenario: str,
    subfamily: str,
    observations: dict[str, Any],
    source_state: dict[str, Any],
    timeline: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "observations": observations,
        "scenario": scenario,
        "source_state": source_state,
        "subfamily": subfamily,
        "timeline": timeline,
    }


def _state(before: Any, after: Any, unchanged: bool | None = None) -> dict[str, Any]:
    return {
        "after": after,
        "before": before,
        "unchanged": before == after if unchanged is None else unchanged,
    }


def _v01(shape: Any) -> dict[str, Any]:
    default = shape.Vertex()
    booleans = shape.Vertex(True, False, True)
    nonfinite = shape.Vertex(float("nan"), float("inf"), float("-inf"))
    huge = shape.Vertex(10**400, -(10**500), 10**600)
    mutable = shape.Vertex(1.0, 2.0, 3.0)
    before = {
        "booleans": _vertex(booleans),
        "default": _vertex(default),
        "huge": _vertex(huge),
        "mutable": _vertex(mutable),
        "nonfinite": _vertex(nonfinite),
    }

    def mutate() -> None:
        mutable.x = float("nan")
        mutable.y = True
        mutable.z = 10**700

    mutation = _error(mutate, "mutate-to-nan-bool-huge-int")
    invalid_set = _error(
        lambda: setattr(mutable, "x", "not-numeric"),
        "reject-nonnumeric-property-set",
    )
    invalid_construct = _error(
        lambda: shape.Vertex([], 0, 0),
        "reject-nonnumeric-construction",
    )
    after = {
        "booleans": _vertex(booleans),
        "default": _vertex(default),
        "huge": _vertex(huge),
        "mutable": _vertex(mutable),
        "nonfinite": _vertex(nonfinite),
    }
    return _fact(
        "V01",
        "vertex",
        {
            "bool_coordinates_preserve_bool_runtime_values": [
                isinstance(booleans.x, bool),
                isinstance(booleans.y, bool),
                isinstance(booleans.z, bool),
            ],
            "huge_integer_digit_counts": [
                len(str(abs(huge.x))),
                len(str(abs(huge.y))),
                len(str(abs(huge.z))),
            ],
            "invalid_construction": invalid_construct,
            "invalid_property_set": invalid_set,
            "mutation_returned": mutation["outcome"] == "returned",
            "nonfinite_classes": [
                before["nonfinite"][0]["value"],
                before["nonfinite"][1]["value"],
                before["nonfinite"][2]["value"],
            ],
        },
        _state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "construct-default-bool-nonfinite-huge-and-mutable", "return_type": "tuple"},
            mutation,
            invalid_set,
            invalid_construct,
        ],
    )


def _v02(shape: Any) -> dict[str, Any]:
    original = shape.Vertex(1.0, 2.0, 3.0)
    iterator = iter(original)
    iterated = list(iterator)
    exhausted = list(iterator)
    copied = deepcopy(original)
    zero_added = 0 + original
    false_added = False + original
    before = {
        "copied": _vertex(copied),
        "false_added": _vertex(false_added),
        "original": _vertex(original),
        "zero_added": _vertex(zero_added),
    }
    original.x = 9.0
    original.y = 8.0
    original.z = 7.0
    after = {
        "copied": _vertex(copied),
        "false_added": _vertex(false_added),
        "original": _vertex(original),
        "zero_added": _vertex(zero_added),
    }
    return _fact(
        "V02",
        "vertex",
        {
            "copy_results_fresh": copied is not original and zero_added is not original and false_added is not original,
            "copy_states_retained_after_source_mutation": (
                _vertex(copied) == before["copied"]
                and _vertex(zero_added) == before["zero_added"]
                and _vertex(false_added) == before["false_added"]
            ),
            "false_is_treated_as_zero_addition": _vertex(false_added) == before["original"],
            "iterated_values": [_encode(value) for value in iterated],
            "iterator_exhausted_values": [_encode(value) for value in exhausted],
            "iterator_type": type(iter(original)).__name__,
            "zero_addition_state": _vertex(zero_added),
        },
        _state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "iterate-once-and-exhaust", "return_type": "list"},
            {"outcome": "returned", "phase": "deepcopy", "return_type": "Vertex"},
            {"outcome": "returned", "phase": "integer-zero-radd", "return_type": "Vertex"},
            {"outcome": "returned", "phase": "boolean-false-radd", "return_type": "Vertex"},
            {"outcome": "returned", "phase": "mutate-original-after-copies", "return_type": "NoneType"},
        ],
    )


def _v03(shape: Any) -> dict[str, Any]:
    left = shape.Vertex(1.0, 2.0, 3.0)
    right = shape.Vertex(4.0, -5.0, 6.0)
    before = {"left": _vertex(left), "right": _vertex(right)}
    first_results = {
        "add_point_to_point": _vertex(left + right),
        "divide_by_two": _vertex(left / 2),
        "left_multiply": _vertex(2 * left),
        "right_multiply": _vertex(left * 2),
        "subtract_point_from_point": _vertex(left - right),
    }
    second_results = {
        "add_point_to_point": _vertex(left + right),
        "divide_by_two": _vertex(left / 2),
        "left_multiply": _vertex(2 * left),
        "right_multiply": _vertex(left * 2),
        "subtract_point_from_point": _vertex(left - right),
    }
    after = {"left": _vertex(left), "right": _vertex(right)}
    return _fact(
        "V03",
        "vertex",
        {
            "first_results": first_results,
            "repeat_results_equal": first_results == second_results,
            "result_types": {
                "add": type(left + right).__name__,
                "divide": type(left / 2).__name__,
                "multiply": type(left * 2).__name__,
                "subtract": type(left - right).__name__,
            },
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "first-operator-batch", "return_type": "dict"},
            {"outcome": "returned", "phase": "repeat-operator-batch", "return_type": "dict"},
        ],
    )


def _v04(shape: Any) -> dict[str, Any]:
    value = shape.Vertex(1, 2, 3)
    before = _vertex(value)
    zero_added = value + 0
    false_added = value + False
    false_scaled = value * False
    true_scaled = True * value
    events = [
        _error(lambda: value + 1, "add-nonzero-int"),
        _error(lambda: 1 + value, "radd-nonzero-int"),
        _error(lambda: value * "2", "multiply-string"),
        _error(lambda: "2" * value, "rmultiply-string"),
        _error(lambda: value / 0, "divide-zero-int"),
        _error(lambda: value / False, "divide-false"),
        _error(lambda: value / "2", "divide-string"),
    ]
    after = _vertex(value)
    return _fact(
        "V04",
        "vertex",
        {
            "boolean_and_zero_successes": {
                "false_added": _vertex(false_added),
                "false_scaled": _vertex(false_scaled),
                "true_scaled": _vertex(true_scaled),
                "zero_added": _vertex(zero_added),
            },
            "error_events": events,
            "error_types_in_phase_order": [event["error"]["type"] for event in events],
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "zero-and-boolean-operator-batch", "return_type": "dict"},
            *events,
        ],
    )


def _v05(shape: Any) -> dict[str, Any]:
    value = shape.Vertex(3.0, 4.0, 0.0)
    other = shape.Vertex(0.0, 0.0, 12.0)
    zero = shape.Vertex(0.0, 0.0, 0.0)
    before = {"other": _vertex(other), "value": _vertex(value), "zero": _vertex(zero)}
    first_zero_unit = zero.unit
    second_zero_unit = zero.unit
    observations = {
        "cross": _vertex(value.cross(other)),
        "distance": _encode(value.distance(other)),
        "dot": _encode(value.dot(other)),
        "norm": _encode(value.norm),
        "unit": _vertex(value.unit),
        "zero_norm": _encode(zero.norm),
        "zero_unit": _vertex(first_zero_unit),
        "zero_unit_fresh_instances": first_zero_unit is not second_zero_unit,
        "zero_unit_repeat_equal": _vertex(first_zero_unit) == _vertex(second_zero_unit),
    }
    after = {"other": _vertex(other), "value": _vertex(value), "zero": _vertex(zero)}
    return _fact(
        "V05",
        "vertex",
        observations,
        _state(before, after),
        [
            {"outcome": "returned", "phase": "metric-and-vector-batch", "return_type": "dict"},
            {"outcome": "returned", "phase": "zero-unit-first-read", "return_type": "Vertex"},
            {"outcome": "returned", "phase": "zero-unit-second-read", "return_type": "Vertex"},
        ],
    )


def _v06(shape: Any) -> dict[str, Any]:
    p0 = shape.Vertex(0.0, 0.0, 0.0)
    p1 = shape.Vertex(1.0, 0.0, 0.0)
    p2 = shape.Vertex(0.0, 1.0, 0.0)
    below_value = math.nextafter(1e-15, 0.0)
    exact_value = 1e-15
    above_value = math.nextafter(1e-15, math.inf)
    probes = [
        ("below", shape.Vertex(1.0, 0.0, below_value)),
        ("exact", shape.Vertex(1.0, 0.0, exact_value)),
        ("above", shape.Vertex(1.0, 0.0, above_value)),
    ]
    before = _vertices([p0, p1, p2, *[item[1] for item in probes]])
    normal = (p1 - p0).cross(p2 - p0).unit
    results = [
        {
            "angular_dot": _encode(abs(normal.dot((point - p0).unit))),
            "coplanar": shape.Vertex.are_coplanar(p0, p1, p2, point),
            "label": label,
            "z": _encode(point.z),
        }
        for label, point in probes
    ]
    invalid = _error(
        lambda: shape.Vertex.are_coplanar(p0, p1, "not-a-vertex"),
        "reject-nonvertex-before-cardinality-shortcut",
    )
    after = _vertices([p0, p1, p2, *[item[1] for item in probes]])
    return _fact(
        "V06",
        "vertex",
        {
            "empty_arguments_are_coplanar": shape.Vertex.are_coplanar(),
            "invalid_argument_error": invalid,
            "probe_results": results,
            "three_arguments_short_circuit_true": shape.Vertex.are_coplanar(
                p0, p1, shape.Vertex(0.0, 0.0, 9.0)
            ),
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "below-exact-above-threshold-probes", "return_type": "list"},
            {"outcome": "returned", "phase": "empty-and-three-argument-shortcuts", "return_type": "bool"},
            invalid,
        ],
    )


def _v07(shape: Any) -> dict[str, Any]:
    p0 = shape.Vertex(0.0, 0.0, 0.0)
    p1 = shape.Vertex(1.0, 0.0, 0.0)
    p2 = shape.Vertex(2.0, 0.0, 0.0)
    p3 = shape.Vertex(0.0, 1.0, 0.0)
    p4 = shape.Vertex(0.0, 0.0, 1.0)
    points = [p0, p1, p2, p3, p4]
    before = _vertices(points)
    collinear_first = shape.Vertex.are_coplanar(*points)
    noncollinear_first = shape.Vertex.are_coplanar(p0, p1, p3, p2, p4)
    first_normal = (p1 - p0).cross(p2 - p0).unit
    reordered_normal = (p1 - p0).cross(p3 - p0).unit
    after = _vertices(points)
    return _fact(
        "V07",
        "vertex",
        {
            "collinear_first_three_normal": _vertex(first_normal),
            "collinear_first_three_returns_true": collinear_first,
            "reordered_first_three_normal": _vertex(reordered_normal),
            "reordered_noncollinear_first_three_is_coplanar": noncollinear_first,
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "collinear-first-three-call", "return_type": "bool"},
            {"outcome": "returned", "phase": "reordered-noncollinear-first-three-call", "return_type": "bool"},
        ],
    )


def _make_surface(
    shape: Any,
    construction: Any,
    name: str,
    vertices: list[Any] | tuple[Any, ...],
    surface_type: Any = "wall",
) -> Any:
    return shape.Surface(
        name,
        surface_type,
        construction.Construction(name + " Construction"),
        "outdoors",
        vertices,
        [],
        [],
    )


def _surface_state(value: Any) -> dict[str, Any]:
    return {
        "area": _encode(value.area),
        "center": _vertex(value.center),
        "height": _encode(value.height),
        "normal": _vertex(value.normal),
        "surface_type": value.type.value,
        "vertex_container_type": type(value.vertex).__name__,
        "vertices": _vertices(value.vertex),
    }


def _s08(shape: Any, construction: Any) -> dict[str, Any]:
    vertices = [
        shape.Vertex(0.0, 0.0, 0.0),
        shape.Vertex(4.0, 0.0, 0.0),
        shape.Vertex(4.0, 0.0, 3.0),
        shape.Vertex(0.0, 0.0, 3.0),
    ]
    surface = _make_surface(
        shape, construction, "Vertical Rectangle", vertices, "wall"
    )
    before = {
        "input_vertices": _vertices(vertices),
        "surface": _surface_state(surface),
    }
    repeat = _surface_state(surface)
    after = {
        "input_vertices": _vertices(vertices),
        "surface": _surface_state(surface),
    }
    return _fact(
        "S08",
        "surface",
        {
            "input_container_not_retained": surface.vertex is not vertices,
            "repeat_scalar_state_equal": repeat == before["surface"],
            "surface_type_is_enum_wall": surface.type is shape.SurfaceType.WALL,
            "vertex_alias_flags": [
                left is right
                for left, right in zip(surface.vertex, vertices, strict=True)
            ],
            "vertex_container_is_tuple": isinstance(surface.vertex, tuple),
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "construct-vertical-rectangle", "return_type": "Surface"},
            {"outcome": "returned", "phase": "first-scalar-geometry-read", "return_type": "dict"},
            {"outcome": "returned", "phase": "repeat-scalar-geometry-read", "return_type": "dict"},
        ],
    )


def _s09(shape: Any, construction: Any) -> dict[str, Any]:
    forward_vertices = [
        shape.Vertex(0.0, 0.0, 0.0),
        shape.Vertex(4.0, 0.0, 0.0),
        shape.Vertex(4.0, 0.0, 3.0),
        shape.Vertex(0.0, 0.0, 3.0),
    ]
    reverse_vertices = list(reversed(forward_vertices))
    forward = _make_surface(
        shape, construction, "Forward Rectangle", forward_vertices
    )
    reverse = _make_surface(
        shape, construction, "Reverse Rectangle", reverse_vertices
    )
    before = {"forward": _surface_state(forward), "reverse": _surface_state(reverse)}
    forward_normal = forward.normal
    reverse_normal = reverse.normal
    after = {"forward": _surface_state(forward), "reverse": _surface_state(reverse)}
    return _fact(
        "S09",
        "surface",
        {
            "areas_equal": forward.area == reverse.area,
            "centers_equal": _vertex(forward.center) == _vertex(reverse.center),
            "heights_equal": forward.height == reverse.height,
            "normal_dot": _encode(forward_normal.dot(reverse_normal)),
            "normals_are_opposite": math.isclose(
                forward_normal.dot(reverse_normal), -1.0
            ),
            "reversal_uses_same_vertex_objects": all(
                left is right
                for left, right in zip(
                    forward.vertex,
                    reversed(reverse.vertex),
                    strict=True,
                )
            ),
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "construct-forward-and-reverse", "return_type": "tuple"},
            {"outcome": "returned", "phase": "compare-scalar-geometry", "return_type": "dict"},
        ],
    )


def _s10(shape: Any, construction: Any) -> dict[str, Any]:
    vertices = [
        shape.Vertex(4.0, 4.0, 0.0),
        shape.Vertex(2.0, 2.0, 0.0),
        shape.Vertex(0.0, 4.0, 0.0),
        shape.Vertex(0.0, 0.0, 0.0),
        shape.Vertex(4.0, 0.0, 0.0),
    ]
    surface = _make_surface(shape, construction, "Concave Reflex First Turn", vertices)
    before = _surface_state(surface)
    cross_sum = sum(
        [
            left.cross(right)
            for left, right in zip(vertices, vertices[1:] + vertices[:1], strict=True)
        ]
    )
    after = _surface_state(surface)
    return _fact(
        "S10",
        "surface",
        {
            "cross_sum": _vertex(cross_sum),
            "first_turn_normal": _vertex(surface.normal),
            "normal_opposes_cross_sum": surface.normal.dot(cross_sum) < 0,
            "python_area": _encode(surface.area),
            "python_area_is_negative": surface.area < 0,
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "construct-concave-reflex-first-turn", "return_type": "Surface"},
            {"outcome": "returned", "phase": "read-negative-area-and-opposite-normal", "return_type": "dict"},
        ],
    )


def _s11(shape: Any, construction: Any) -> dict[str, Any]:
    invalid_sets = {
        "collinear_triangle": [
            shape.Vertex(0.0, 0.0, 0.0),
            shape.Vertex(1.0, 0.0, 0.0),
            shape.Vertex(2.0, 0.0, 0.0),
        ],
        "duplicate_closing_square": [
            shape.Vertex(0.0, 0.0, 0.0),
            shape.Vertex(2.0, 0.0, 0.0),
            shape.Vertex(2.0, 2.0, 0.0),
            shape.Vertex(0.0, 2.0, 0.0),
            shape.Vertex(0.0, 0.0, 0.0),
        ],
        "self_intersecting_bow_tie": [
            shape.Vertex(0.0, 0.0, 0.0),
            shape.Vertex(2.0, 2.0, 0.0),
            shape.Vertex(0.0, 2.0, 0.0),
            shape.Vertex(2.0, 0.0, 0.0),
        ],
    }
    surfaces = {
        name: _make_surface(shape, construction, name, vertices)
        for name, vertices in invalid_sets.items()
    }
    before = {name: _surface_state(surface) for name, surface in surfaces.items()}
    valid = _make_surface(
        shape,
        construction,
        "Setter Error Host",
        [
            shape.Vertex(0.0, 0.0, 0.0),
            shape.Vertex(2.0, 0.0, 0.0),
            shape.Vertex(2.0, 2.0, 0.0),
            shape.Vertex(0.0, 2.0, 0.0),
        ],
    )
    valid_before = _surface_state(valid)
    too_few = _error(
        lambda: setattr(valid, "vertex", [shape.Vertex(), shape.Vertex(1, 0, 0)]),
        "reject-too-few-vertices",
    )
    foreign = _error(
        lambda: setattr(valid, "vertex", [shape.Vertex(), "foreign", shape.Vertex(1, 1, 0)]),
        "reject-foreign-vertex",
    )
    nonplanar = _error(
        lambda: setattr(
            valid,
            "vertex",
            [
                shape.Vertex(0, 0, 0),
                shape.Vertex(2, 0, 0),
                shape.Vertex(2, 2, 0),
                shape.Vertex(0, 2, 1),
            ],
        ),
        "reject-nonplanar-vertices",
    )
    after = {name: _surface_state(surface) for name, surface in surfaces.items()}
    valid_after = _surface_state(valid)
    return _fact(
        "S11",
        "surface",
        {
            "accepted_invalid_polygon_states": before,
            "rejected_setter_events": [too_few, foreign, nonplanar],
            "setter_state_unchanged_after_errors": valid_before == valid_after,
        },
        _state(
            {"accepted": before, "setter_host": valid_before},
            {"accepted": after, "setter_host": valid_after},
        ),
        [
            {"outcome": "returned", "phase": "construct-three-invalid-polygons", "return_type": "dict"},
            too_few,
            foreign,
            nonplanar,
        ],
    )


def _s12(shape: Any, construction: Any) -> dict[str, Any]:
    source_vertices = [
        shape.Vertex(0.0, 0.0, 0.0),
        shape.Vertex(4.0, 0.0, 0.0),
        shape.Vertex(4.0, 4.0, 0.0),
        shape.Vertex(0.0, 4.0, 0.0),
    ]
    surface = _make_surface(
        shape, construction, "Vertex Alias Mutation", source_vertices, "wall"
    )
    before = {
        "source_vertices": _vertices(source_vertices),
        "surface": _surface_state(surface),
    }
    source_vertices[0].x = 1.0
    after_alias_mutation = {
        "source_vertices": _vertices(source_vertices),
        "surface": _surface_state(surface),
    }
    replacement_vertices = (
        shape.Vertex(0.0, 0.0, 0.0),
        shape.Vertex(3.0, 0.0, 0.0),
        shape.Vertex(3.0, 2.0, 0.0),
        shape.Vertex(0.0, 2.0, 0.0),
    )
    valid_reassignment = _error(
        lambda: setattr(surface, "vertex", replacement_vertices),
        "accept-tuple-vertex-reassignment",
    )
    surface.type = "floor"
    invalid_type = _error(
        lambda: setattr(surface, "type", "Wall"),
        "reject-case-mismatched-surface-type",
    )
    replacement_vertices[0].x = 0.5
    final = {
        "replacement_vertices": _vertices(replacement_vertices),
        "source_vertices": _vertices(source_vertices),
        "surface": _surface_state(surface),
    }
    return _fact(
        "S12",
        "surface",
        {
            "alias_mutation_changed_surface_geometry": before["surface"] != after_alias_mutation["surface"],
            "replacement_vertex_alias_flags": [
                left is right
                for left, right in zip(surface.vertex, replacement_vertices, strict=True)
            ],
            "invalid_type_error": invalid_type,
            "replacement_alias_mutation_visible": surface.vertex[0] is replacement_vertices[0],
            "surface_type_after_failed_case_mismatch": surface.type.value,
            "tuple_reassignment_returned": valid_reassignment["outcome"] == "returned",
        },
        {
            "after": final,
            "before": before,
            "intermediate_after_source_alias_mutation": after_alias_mutation,
            "unchanged": False,
        },
        [
            {"outcome": "returned", "phase": "construct-with-source-vertex-aliases", "return_type": "Surface"},
            {"outcome": "returned", "phase": "mutate-original-vertex-x", "return_type": "NoneType"},
            valid_reassignment,
            {"outcome": "returned", "phase": "set-surface-type-floor", "return_type": "NoneType"},
            invalid_type,
            {"outcome": "returned", "phase": "mutate-replacement-vertex-x", "return_type": "NoneType"},
        ],
    )


def _t13(shape: Any) -> dict[str, Any]:
    members = list(shape.SurfaceType)
    before = [
        {"name": item.name, "string": str(item), "value": item.value}
        for item in members
    ]
    after = [
        {"name": item.name, "string": str(item), "value": item.value}
        for item in members
    ]
    return _fact(
        "T13",
        "surface-type",
        {
            "definition_order": [item.name for item in members],
            "member_records": [
                {
                    "equal_to_raw_string": item == item.value,
                    "is_str_instance": isinstance(item, str),
                    "name": item.name,
                    "round_trip_is_same_member": shape.SurfaceType(item.value) is item,
                    "string": str(item),
                    "value": item.value,
                }
                for item in members
            ],
            "three_direct_member_mappings": {
                "SurfaceType.CEILING": shape.SurfaceType.CEILING.value,
                "SurfaceType.FLOOR": shape.SurfaceType.FLOOR.value,
                "SurfaceType.WALL": shape.SurfaceType.WALL.value,
            },
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "enumerate-members-in-definition-order", "return_type": "list"},
            {"outcome": "returned", "phase": "string-and-round-trip-topology", "return_type": "dict"},
        ],
    )


def _t14(shape: Any) -> dict[str, Any]:
    members = list(shape.SurfaceType)
    before = [{"name": item.name, "value": item.value} for item in members]
    uppercase = _error(
        lambda: shape.SurfaceType("Wall"),
        "reject-title-case-value",
    )
    integer = _error(lambda: shape.SurfaceType(1), "reject-integer-value")
    unknown = _error(
        lambda: shape.SurfaceType("roof"),
        "reject-unknown-lowercase-value",
    )
    after = [{"name": item.name, "value": item.value} for item in members]
    return _fact(
        "T14",
        "surface-type",
        {
            "error_events": [uppercase, integer, unknown],
            "exact_lowercase_conversions": [
                {
                    "input": item.value,
                    "member_name": shape.SurfaceType(item.value).name,
                    "string": str(shape.SurfaceType(item.value)),
                }
                for item in members
            ],
            "no_enum_aliases": len(shape.SurfaceType.__members__) == len(members),
        },
        _state(before, after),
        [
            {"outcome": "returned", "phase": "exact-lowercase-conversions", "return_type": "list"},
            uppercase,
            integer,
            unknown,
        ],
    )


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    shape = modules.shape
    construction = modules.construction
    slug = identifier.removeprefix(PREFIX)
    functions: dict[str, Callable[[], dict[str, Any]]] = {
        "v01-vertex-domain-mutable-state": lambda: _v01(shape),
        "v02-vertex-copy-iteration-zero-radd": lambda: _v02(shape),
        "v03-vertex-point-vector-arithmetic": lambda: _v03(shape),
        "v04-vertex-operator-error-timing": lambda: _v04(shape),
        "v05-vertex-vector-metrics-zero-unit": lambda: _v05(shape),
        "v06-vertex-coplanarity-angular-threshold": lambda: _v06(shape),
        "v07-vertex-coplanarity-first-three-collinear-defect": lambda: _v07(shape),
        "s08-surface-rectangle-scalar-geometry": lambda: _s08(shape, construction),
        "s09-surface-reversed-winding": lambda: _s09(shape, construction),
        "s10-surface-concave-reflex-first-turn-negative-area": lambda: _s10(shape, construction),
        "s11-surface-invalid-polygon-acceptance": lambda: _s11(shape, construction),
        "s12-surface-vertex-alias-mutation-and-setter-errors": lambda: _s12(shape, construction),
        "t13-surface-type-enum-string-topology": lambda: _t13(shape),
        "t14-surface-type-conversion-error-topology": lambda: _t14(shape),
    }
    try:
        return functions[slug]()
    except KeyError as error:
        raise RuntimeError(f"Unknown geometry case: {identifier}") from error


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


def _coverage_by_subfamily() -> dict[str, list[str]]:
    return {
        subfamily: [
            definition["id"]
            for definition in case_definitions()
            if definition["subfamily"] == subfamily
        ]
        for subfamily in ("vertex", "surface", "surface-type")
    }


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "The three SurfaceType constants map directly to native enum members. "
            "Every other target requires an explicit exception because Python exposes "
            "mutable and nonfinite Vertex state, untyped point/vector algebra, zero-unit "
            "success, first-triple angular coplanarity, mutable aliased Surface vertices, "
            "permissive invalid polygons, first-turn-oriented signed area, or lowercase "
            "string-enum topology absent from the validated immutable native geometry model."
        ),
        "classification_counts": {"equivalent": 3, "exception": 28},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "case_coverage_by_subfamily": _coverage_by_subfamily(),
            "case_coverage_by_symbol": _coverage_by_symbol(),
            "context_only_not_targeted": [
                "Construction",
                "Surface.__init__",
                "Surface.to_idf_object",
                "SurfaceBoundaryCondition",
                "Vertex.__eq__",
                "Window",
                "Door",
                "Blind",
                "Shade",
            ],
            "full_symbol_closure": False,
            "opening_adjacency_targets_not_promoted": [
                "Surface.__init__",
                "Surface.blinded_window",
                "Surface.boundary",
                "Surface.get_subsurface",
                "SurfaceBoundaryCondition",
                "Window",
                "Door",
                "Blind",
                "Shade",
                "Shading",
            ],
            "out_of_scope_symbols_not_promoted": [
                "Surface.__repr__",
                "Surface.__str__",
                "Vertex.__eq__",
                "Vertex.__repr__",
                "Vertex.__str__",
            ],
            "scope": "exact-fourteen-case-three-subfamily-geometry-core-matrix",
            "target_coverage_complete": True,
            "target_symbols": list(TARGET_SYMBOLS),
            "unresolved_target_behavior": [
                "arbitrary-foreign-object-operator-protocols-beyond-the-pinned-error-phases",
                "overflow-and-nonfinite-derived-Vertex-metric-results",
                "all-mutable-Surface-post-construction-invalidity-combinations",
            ],
        },
        "equivalent_candidate_symbols": list(EQUIVALENT_SYMBOLS),
        "identity_encoding": "stable-boolean-relations-only-no-id-or-address",
        "native_targets": NATIVE_TARGETS,
        "raw_fact_encoding": (
            "typed-scalars-with-explicit-nonfinite-classes-huge-integer-decimals-and-phase-bound-errors"
        ),
        "runtime_signatures": RUNTIME_SIGNATURES,
        "source_import_policy": (
            "external-temporary-copy-with-complete-loaded-local-module-audit"
        ),
        "target_receipts": _expected_target_receipts(),
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


def _resolve_symbol(shape: Any, symbol: str) -> Any:
    return functools.reduce(getattr, symbol.split("."), shape)


def _runtime_signature(value: Any, shape: Any) -> str:
    if isinstance(value, property):
        result = "property:fget=" + str(inspect.signature(value.fget))
        if value.fset is not None:
            result += ";fset=" + str(inspect.signature(value.fset))
        return result
    if isinstance(value, shape.SurfaceType):
        return "enum-member:" + repr(value.value)
    return str(inspect.signature(value))


def _runtime_signatures(shape: Any) -> dict[str, str]:
    return {
        symbol: _runtime_signature(_resolve_symbol(shape, symbol), shape)
        for symbol in TARGET_SYMBOLS
    }


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    actual_hash = canonical_sha256(facts)
    expected_hash = EXPECTED_FACT_SHA256.get(identifier)
    if expected_hash is not None and actual_hash != expected_hash:
        raise RuntimeError(
            f"Geometry canonical semantics drifted: {identifier}: {actual_hash}"
        )
    if set(facts) != {
        "observations",
        "scenario",
        "source_state",
        "subfamily",
        "timeline",
    }:
        raise RuntimeError(f"Geometry fact key set drifted: {identifier}")
    definition = next(item for item in case_definitions() if item["id"] == identifier)
    if facts["subfamily"] != definition["subfamily"]:
        raise RuntimeError(f"Geometry subfamily drifted: {identifier}")
    if facts["scenario"] != identifier.removeprefix(PREFIX)[:3].upper():
        raise RuntimeError(f"Geometry scenario label drifted: {identifier}")
    source = facts["source_state"]
    if not isinstance(source, dict) or not {"before", "after", "unchanged"}.issubset(source):
        raise RuntimeError(f"Geometry source-state shape drifted: {identifier}")
    if not isinstance(source["unchanged"], bool):
        raise RuntimeError(f"Geometry source-state flag drifted: {identifier}")
    timeline = facts["timeline"]
    if not isinstance(timeline, list) or not timeline:
        raise RuntimeError(f"Geometry timeline drifted: {identifier}")
    for event in timeline:
        if event.get("outcome") not in ("raised", "returned") or not isinstance(event.get("phase"), str):
            raise RuntimeError(f"Geometry event drifted: {identifier}")
        if event["outcome"] == "raised" and set(event) != {"error", "outcome", "phase"}:
            raise RuntimeError(f"Geometry error timing drifted: {identifier}")

    observations = facts["observations"]
    scenario = facts["scenario"]
    if scenario == "V01":
        valid = observations["bool_coordinates_preserve_bool_runtime_values"] == [True, True, True] and observations["huge_integer_digit_counts"] == [401, 501, 601] and observations["nonfinite_classes"] == ["nan", "positive-infinity", "negative-infinity"] and observations["mutation_returned"] and observations["invalid_property_set"]["outcome"] == "raised" and observations["invalid_construction"]["outcome"] == "raised" and not source["unchanged"]
    elif scenario == "V02":
        valid = observations["copy_results_fresh"] and observations["copy_states_retained_after_source_mutation"] and observations["false_is_treated_as_zero_addition"] and observations["iterator_exhausted_values"] == [] and not source["unchanged"]
    elif scenario == "V03":
        valid = observations["repeat_results_equal"] and set(observations["result_types"].values()) == {"Vertex"} and source["unchanged"]
    elif scenario == "V04":
        valid = len(observations["error_events"]) == 7 and all(item["outcome"] == "raised" for item in observations["error_events"]) and source["unchanged"]
    elif scenario == "V05":
        valid = observations["norm"] == _encode(5.0) and observations["distance"] == _encode(13.0) and observations["dot"] == _encode(0.0) and observations["zero_unit"] == [_encode(0), _encode(0), _encode(0)] and observations["zero_unit_fresh_instances"] and source["unchanged"]
    elif scenario == "V06":
        valid = [item["coplanar"] for item in observations["probe_results"]] == [True, True, False] and observations["empty_arguments_are_coplanar"] and observations["three_arguments_short_circuit_true"] and observations["invalid_argument_error"]["outcome"] == "raised" and source["unchanged"]
    elif scenario == "V07":
        expected_points = [
            [_encode(0.0), _encode(0.0), _encode(0.0)],
            [_encode(1.0), _encode(0.0), _encode(0.0)],
            [_encode(2.0), _encode(0.0), _encode(0.0)],
            [_encode(0.0), _encode(1.0), _encode(0.0)],
            [_encode(0.0), _encode(0.0), _encode(1.0)],
        ]
        valid = source["before"] == expected_points and observations["collinear_first_three_normal"] == [_encode(0), _encode(0), _encode(0)] and observations["reordered_first_three_normal"] == [_encode(0.0), _encode(0.0), _encode(1.0)] and observations["collinear_first_three_returns_true"] and not observations["reordered_noncollinear_first_three_is_coplanar"] and source["unchanged"]
    elif scenario == "S08":
        state = source["before"]["surface"]
        valid = state["area"] == _encode(12.0) and state["center"] == [_encode(2.0), _encode(0.0), _encode(1.5)] and state["height"] == _encode(3.0) and state["normal"] == [_encode(0.0), _encode(-1.0), _encode(0.0)] and observations["vertex_container_is_tuple"] and all(observations["vertex_alias_flags"]) and source["unchanged"]
    elif scenario == "S09":
        valid = observations["areas_equal"] and observations["centers_equal"] and observations["heights_equal"] and observations["normals_are_opposite"] and observations["normal_dot"] == _encode(-1.0) and source["unchanged"]
    elif scenario == "S10":
        valid = observations["python_area"] == _encode(-12.0) and observations["python_area_is_negative"] and observations["normal_opposes_cross_sum"] and source["unchanged"]
    elif scenario == "S11":
        accepted = observations["accepted_invalid_polygon_states"]
        valid = timeline[0] == {"outcome": "returned", "phase": "construct-three-invalid-polygons", "return_type": "dict"} and set(accepted) == {"collinear_triangle", "duplicate_closing_square", "self_intersecting_bow_tie"} and all(item["outcome"] == "raised" for item in observations["rejected_setter_events"]) and observations["setter_state_unchanged_after_errors"] and source["unchanged"]
    elif scenario == "S12":
        valid = observations["alias_mutation_changed_surface_geometry"] and all(observations["replacement_vertex_alias_flags"]) and observations["replacement_alias_mutation_visible"] and observations["tuple_reassignment_returned"] and observations["invalid_type_error"]["outcome"] == "raised" and observations["surface_type_after_failed_case_mismatch"] == "floor" and not source["unchanged"]
    elif scenario == "T13":
        valid = observations["definition_order"] == ["WALL", "CEILING", "FLOOR"] and set(observations["three_direct_member_mappings"]) == set(EQUIVALENT_SYMBOLS) and all(item["is_str_instance"] and item["round_trip_is_same_member"] for item in observations["member_records"]) and source["unchanged"]
    elif scenario == "T14":
        valid = observations["no_enum_aliases"] and [item["input"] for item in observations["exact_lowercase_conversions"]] == ["wall", "ceiling", "floor"] and all(item["outcome"] == "raised" for item in observations["error_events"]) and source["unchanged"]
    else:
        valid = False
    if not valid:
        raise RuntimeError(f"Geometry semantic invariant drifted: {identifier}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
        "target_receipts": _expected_target_receipts(),
    }:
        raise SystemExit("The aggregate geometry inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    if (imported_root / Path(SHAPE_SOURCE_PATH).relative_to("src")).stat().st_size != 27_438:
        raise SystemExit("Pinned shape.py byte length drifted.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        construction = importlib.import_module("idragon.dragon.construction")
        modules.construction = construction
        if _runtime_signatures(modules.shape) != RUNTIME_SIGNATURES:
            raise SystemExit("Pinned geometry runtime signatures drifted.")
        if modules.shape.Construction is not construction.Construction:
            raise SystemExit("Pinned geometry import identities drifted.")
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
                "Pinned geometry per-case facts drifted.\nOBSERVED_FACT_HASHES\n"
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
                "Pinned geometry per-case records drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(case_hashes, indent=2)
            )
        result = {
            "case_sha256": case_hashes,
            "cases": cases,
            "cases_sha256": cases_sha256(cases),
            "consumer_contract": _expected_consumer_contract(),
            "fact_sha256": fact_hashes,
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
            raise RuntimeError(f"Invalid encoded nonfinite float at {location}.")
        return True
    return False


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if isinstance(value, float):
        if not math.isfinite(value):
            raise RuntimeError(f"Raw nonfinite float is forbidden at {location}.")
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
            for item in value.values():
                if isinstance(item, str):
                    _validate_safe_tree(item, f"{location}.encoded")
            return
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string JSON key is forbidden at {location}.")
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
            "fact_sha256",
            "runtime",
            "schema",
            "symbols",
            "target_receipts",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Geometry schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Geometry cases hash drifted.")
    if value["case_sha256"] != case_sha256(value["cases"]):
        raise RuntimeError("Geometry per-case hash map drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Geometry case order/count drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Geometry case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"Geometry Python outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"Geometry inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_facts(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Geometry fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("Geometry expected fact hashes drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("Geometry expected case hashes drifted.")

    target_counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if set(target_counts) != set(TARGET_SYMBOLS) or any(count < 1 for count in target_counts.values()):
        raise RuntimeError("Geometry target coverage drifted.")
    subfamilies = Counter(definition["subfamily"] for definition in definitions)
    if subfamilies != Counter({"vertex": 7, "surface": 5, "surface-type": 2}):
        raise RuntimeError("Geometry subfamily matrix drifted.")
    forbidden = {
        "Surface.__init__",
        "Surface.to_idf_object",
        "Surface.blinded_window",
        "Surface.boundary",
        "Surface.get_subsurface",
        "SurfaceBoundaryCondition",
        "Vertex.__eq__",
        "Vertex.__repr__",
        "Vertex.__str__",
    }
    if forbidden.intersection(target_counts):
        raise RuntimeError("Geometry batch promoted a forbidden target.")
    if Counter(CLASSIFICATIONS.values()) != Counter({"exception": 28, "equivalent": 3}):
        raise RuntimeError("Geometry classification counts drifted.")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Geometry consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Geometry runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Geometry upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Geometry symbol receipts drifted.")
    if value["target_receipts"] != _expected_target_receipts():
        raise RuntimeError("Geometry indexed target receipts drifted.")
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
    print(f"Wrote dragon shape geometry core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

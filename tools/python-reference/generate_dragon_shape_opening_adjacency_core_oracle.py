"""Generate pinned Python facts for Dragon shape opening/adjacency contracts.

The A01--A18 corpus targets exactly nineteen constructor, property, enum, and
helper symbols in ``idragon.dragon.shape``.  Historical
``Surface.to_idf_object`` calls appear only in A16 as context witnesses for the
opening positional-zip behavior; that already separate emitter is never
promoted to a target by this oracle.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import sys
from types import SimpleNamespace
from typing import Any, Callable


SCHEMA = "dragons.python-reference.dragon-shape-opening-adjacency-core.v1"
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

TARGET_RECEIPTS = (
    {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "inventory_index": 1025,
        "kind": "class",
        "signature_hash": "sha256:fc7a9c184e4c3d27ade9f49aa28e0fac174fc62924cb16b31214be1c5040a0ce",
        "symbol": "Blind",
        "symbol_hash": "sha256:75f7c91c526ca8c2a86f7a984fa2007d17e94a8a3e38a6a80ffa6a7af37cd36b",
    },
    {
        "body_hash": "sha256:c7af4f5037c03da48ea55ce1b17434d0adee92079ed159f3662f8f3529807067",
        "inventory_index": 1026,
        "kind": "function",
        "signature_hash": "sha256:d42cf37a1ce3ef68b7b965525da19d840fad8f959e20cedce16272a4c2062f32",
        "symbol": "Blind.__init__",
        "symbol_hash": "sha256:574e9b5ab31178c6d64eaeb70e19e3a434448c712cf2d8459bfdc36704047eee",
    },
    {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "inventory_index": 1028,
        "kind": "class",
        "signature_hash": "sha256:0e2346da9e26019c14e49847521a42359715dcbe64fa76f594be06344837ac38",
        "symbol": "Door",
        "symbol_hash": "sha256:717d717ab0c24c7d2900081f9853e5b1670c8f37731d3076410b3401718e59b9",
    },
    {
        "body_hash": "sha256:64e9af88814e32ae336e082a145dc9bd7fcb7a35aabec066fb6441f9b6697d86",
        "inventory_index": 1029,
        "kind": "function",
        "signature_hash": "sha256:1b879b85e5521d34e5f6d6b4b8b5de28d161b537609dcec99a6eb4443f9220c7",
        "symbol": "Door.__init__",
        "symbol_hash": "sha256:efd71c8161c4540503d2a0539dd30c3bf05109fa353292091035dec6b848bbde",
    },
    {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "inventory_index": 1030,
        "kind": "class",
        "signature_hash": "sha256:0f80436ffb22f4436b5ba8ddc953c234450eaa30fb8d9a28a00302dc1dd524ba",
        "symbol": "Shade",
        "symbol_hash": "sha256:9404da043505f2d5bcd314f7a1ce2a994eaec9ba237a8d039f9c107bb97987a0",
    },
    {
        "body_hash": "sha256:3dc5d4920337b46160c5da4cc1f2c4ac137e8c5fa1d58dd1ba3639fe9abe1ef0",
        "inventory_index": 1031,
        "kind": "function",
        "signature_hash": "sha256:0993a1b330563d2c24636c42d55e2049625588c9be7766db90230da994886dd5",
        "symbol": "Shade.__init__",
        "symbol_hash": "sha256:f76ed298cc435ea32d2c8b3631590e12fbb4b844e60af60e13aa517867b225b7",
    },
    {
        "body_hash": "sha256:841d4cb6106fd1288f259549c1674303f32505b0270beb50c4048e496e48d5db",
        "inventory_index": 1033,
        "kind": "class",
        "signature_hash": "sha256:134552eef91182656eaed430922ad3ea45c073c187ddbc3c54d8f65ccb782416",
        "symbol": "Shading",
        "symbol_hash": "sha256:4dba9833a4c24512afe7f0cc7566f8e89fa27a5c4b4d2be523a568dfa83d221c",
    },
    {
        "body_hash": "sha256:4a5a7556a35cd8ddd65641f5ba6e98ba112631c7581f158c349fc7737e50c389",
        "inventory_index": 1035,
        "kind": "function",
        "signature_hash": "sha256:91e81dfb11f60b18fb209a8ce5ab7b1c31ccf24e1fab04c3a4f79cd370173980",
        "symbol": "Surface.__init__",
        "symbol_hash": "sha256:ef349ef4b0a7bfcd1f47a297b0107d24018f5c4350b1765051948f2cfde5daa3",
    },
    {
        "body_hash": "sha256:1d3cc4d0181730c8ef36c846de99dcf384cafdca1995d6e321529b42f2d5760c",
        "inventory_index": 1039,
        "kind": "function",
        "signature_hash": "sha256:6ed2bf44ec68a9cda9c9305419f2564c10dcf9ffa3541254a942a64ef21bd2d4",
        "symbol": "Surface.blinded_window",
        "symbol_hash": "sha256:f520fbfe3104ddbfa8f056b4c28908706faac3b0b333f46b19ff4a7366d73234",
    },
    {
        "body_hash": "sha256:f751320cef2e3413ed702ef8e23a43d9148130cd02678df8145fb094890b2276",
        "inventory_index": 1040,
        "kind": "function",
        "signature_hash": "sha256:11060e585257ead0cc3dbce8f24b8dba7b63f4df1140d5665311b5fcf798980f",
        "symbol": "Surface.boundary",
        "symbol_hash": "sha256:7753d96736d6410917d1eb131f747db5f1e5538aa51e5f00bcf68ee34c084316",
    },
    {
        "body_hash": "sha256:c0dff706444e067d08d2c480969520b1927bd7085f67452388642139565f6547",
        "inventory_index": 1042,
        "kind": "function",
        "signature_hash": "sha256:b4b38a26eb25cd420fa750f6e3df05aff1f17a04019274a0866a9919b192a8b6",
        "symbol": "Surface.get_subsurface",
        "symbol_hash": "sha256:7e43708dfc08dc4b915a0fbb6ea3ebb1ee7b943031a60d12336e4fe3ed33e91f",
    },
    {
        "body_hash": "sha256:fa63e0c63f78931ad2499d6cbdce49736062ce69e49ba7e475be611ff93799c4",
        "inventory_index": 1048,
        "kind": "class",
        "signature_hash": "sha256:a19cb257b67cfe826191f490a77f5e4d2ec67dd04d22e67840b4e0db65a8976d",
        "symbol": "SurfaceBoundaryCondition",
        "symbol_hash": "sha256:73a8b86f663a2874b87c5c6f8ba801e5515095918422a1854e1acf157bb72fa7",
    },
    {
        "body_hash": "sha256:6e122ac194244051572f5d6fad4d0d208a8ef86998cf763329afe6b5882d935a",
        "inventory_index": 1049,
        "kind": "constant",
        "signature_hash": "sha256:a77afcfa981dffc115a2d5b307c32e5a87017d0bb905ea40499433a79dc8988e",
        "symbol": "SurfaceBoundaryCondition.ADIABATIC",
        "symbol_hash": "sha256:1d0e3d46c8e9ae9dec15e60e913ee94e01a3261bbae746ebfe9f71913eb08051",
    },
    {
        "body_hash": "sha256:c7f32d1a16829421283e84020abdc7359b68f59cdbc7982fbc3bd54131019c0f",
        "inventory_index": 1050,
        "kind": "constant",
        "signature_hash": "sha256:1a16c10fc43be40d81c04f68b02c92dfeadd1fce921e156f9998399f9874df74",
        "symbol": "SurfaceBoundaryCondition.GROUND",
        "symbol_hash": "sha256:0992cbf625fbf401fbc1229e59696a8fa65bc36efc11177322a8b181c329e410",
    },
    {
        "body_hash": "sha256:e77842f79eabf8bd08cd21c0af1d558de32c12c118304601a01f5e4d5c2b3dd9",
        "inventory_index": 1051,
        "kind": "constant",
        "signature_hash": "sha256:f1fb2d320126039c88d7c8b391550959a7479d2212123217df022690c957fb3a",
        "symbol": "SurfaceBoundaryCondition.OUTDOOR",
        "symbol_hash": "sha256:8560160a8415533fb8b2572a963112b6fef686482ffaacd99c461ea99fa30306",
    },
    {
        "body_hash": "sha256:5ff79e3fee75f5cebcfa0af7c998358641f1579818f3cf36df0663a984c3f44f",
        "inventory_index": 1052,
        "kind": "constant",
        "signature_hash": "sha256:6e2c14954d19501e9e789403c235fb2d61160415b444b44c346814335989a15d",
        "symbol": "SurfaceBoundaryCondition.ZONE",
        "symbol_hash": "sha256:3ec06789fa4f783e94be2d46f5c31e90fdad2fac6641ea8097a304beba8e613e",
    },
    {
        "body_hash": "sha256:5c924f1658508d952a1e1f3a8f21de59dc5b45bd154d6721874df4eaed6930d8",
        "inventory_index": 1053,
        "kind": "function",
        "signature_hash": "sha256:f422dd08dc32ca6866adf6b2fc835616ecd56dfe2fdd6803d424398609700eab",
        "symbol": "SurfaceBoundaryCondition.__str__",
        "symbol_hash": "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e",
    },
    {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "inventory_index": 1081,
        "kind": "class",
        "signature_hash": "sha256:51e36b1ede4e2ba8870f6b2ab855c3d628e8e9fbb02fef5efabd828d925c9e70",
        "symbol": "Window",
        "symbol_hash": "sha256:af640a9abfcfaae14201dbe8195aba06780027412da5ac3ffaf480d7bfe45b3b",
    },
    {
        "body_hash": "sha256:1ec931f0f7720883c9c44f4a2c10e240602039e80d5f2179a50cf0cb07212641",
        "inventory_index": 1082,
        "kind": "function",
        "signature_hash": "sha256:f69f7e176b5b3338f40002a66cdc91c8eaec356648e739aa66f40a2ad3c02c7b",
        "symbol": "Window.__init__",
        "symbol_hash": "sha256:3ce851bd512903617cce711c5883a4968e1e0ab7e275c2bb10d0b046532e7380",
    },
)
TARGET_SYMBOLS = tuple(item["symbol"] for item in TARGET_RECEIPTS)

ADAPTATIONS = {
    "Blind": "permissive-python-blind-state",
    "Blind.__init__": "permissive-python-blind-state",
    "Door": "permissive-python-door-state",
    "Door.__init__": "permissive-python-door-state",
    "Shade": "permissive-python-shade-state",
    "Shade.__init__": "permissive-python-shade-state",
    "Shading": "directly-instantiable-empty-python-shading",
    "Surface.__init__": "aliased-python-surface-opening-inputs",
    "Surface.blinded_window": "fresh-python-blinded-window-projection",
    "Surface.boundary": "mutable-reciprocal-python-surface-adjacency",
    "Surface.get_subsurface": "legacy-linear-scale-subsurface-projection",
    "SurfaceBoundaryCondition": "lowercase-python-surface-boundary-enum",
    "SurfaceBoundaryCondition.ADIABATIC": "lowercase-python-surface-boundary-enum",
    "SurfaceBoundaryCondition.GROUND": "lowercase-python-surface-boundary-enum",
    "SurfaceBoundaryCondition.OUTDOOR": "lowercase-python-surface-boundary-enum",
    "SurfaceBoundaryCondition.ZONE": "lowercase-python-surface-boundary-enum",
    "SurfaceBoundaryCondition.__str__": "lowercase-python-surface-boundary-enum",
    "Window": "permissive-python-window-state",
    "Window.__init__": "permissive-python-window-state",
}
ASSERTION_IDS = {
    item["symbol"]: (
        "dragon-shape-opening-adjacency-core-"
        + str(item["inventory_index"])
        + "-"
        + item["symbol_hash"][7:15]
    )
    for item in TARGET_RECEIPTS
}
NATIVE_TARGETS = {
    "Blind": "Dragons.InvisibleDragon.Shape.Blind constructor",
    "Blind.__init__": "Dragons.InvisibleDragon.Shape.Blind constructor",
    "Door": "Dragons.InvisibleDragon.Shape.Door constructor",
    "Door.__init__": "Dragons.InvisibleDragon.Shape.Door constructor",
    "Shade": "Dragons.InvisibleDragon.Shape.Shade constructor",
    "Shade.__init__": "Dragons.InvisibleDragon.Shape.Shade constructor",
    "Shading": "Dragons.InvisibleDragon.Shape.IShadingDevice contract",
    "Surface.__init__": "Dragons.InvisibleDragon.Shape.Surface constructor",
    "Surface.blinded_window": "Surface.Windows filtered by Window.Shading",
    "Surface.boundary": "SurfaceBoundary plus SurfaceAdjacency.Match",
    "Surface.get_subsurface": "Surface.CreateCenteredSubsurface",
    "SurfaceBoundaryCondition": "Dragons.InvisibleDragon.Shape.SurfaceBoundaryCondition",
    "SurfaceBoundaryCondition.ADIABATIC": "SurfaceBoundaryCondition.Adiabatic",
    "SurfaceBoundaryCondition.GROUND": "SurfaceBoundaryCondition.Ground",
    "SurfaceBoundaryCondition.OUTDOOR": "SurfaceBoundaryCondition.Outdoors",
    "SurfaceBoundaryCondition.ZONE": "SurfaceBoundaryCondition.Zone",
    "SurfaceBoundaryCondition.__str__": "EnergyModelIdfAssembler boundary mapping",
    "Window": "Dragons.InvisibleDragon.Shape.Window constructor",
    "Window.__init__": "Dragons.InvisibleDragon.Shape.Window constructor",
}
RUNTIME_SIGNATURES = {
    "Blind": "(name, slat_width: 'int | float', slat_separation: 'int | float', slat_angle: 'int | float', front_reflectance: 'int | float', back_reflectance: 'int | float') -> 'None'",
    "Blind.__init__": "(self, name, slat_width: 'int | float', slat_separation: 'int | float', slat_angle: 'int | float', front_reflectance: 'int | float', back_reflectance: 'int | float') -> 'None'",
    "Door": "(name: 'str', construction: 'NoMassConstruction', area: 'int | float') -> 'None'",
    "Door.__init__": "(self, name: 'str', construction: 'NoMassConstruction', area: 'int | float') -> 'None'",
    "Shade": "(name: 'str', transmittance: 'int | float', reflectance: 'int | float') -> 'None'",
    "Shade.__init__": "(self, name: 'str', transmittance: 'int | float', reflectance: 'int | float') -> 'None'",
    "Shading": "()",
    "Surface.__init__": "(self, name: 'str', type: 'SurfaceType | str', construction: 'Construction', boundary: 'str', vertex: 'list[Vertex]', window: 'list[Window]' = [], door: 'list[Door]' = []) -> 'None'",
    "Surface.blinded_window": "property:fget=(self) -> 'list[Window]'",
    "Surface.boundary": "property:fget=(self);fset=(self, value: 'str')",
    "Surface.get_subsurface": "(self, area: 'int | float') -> 'list[Vertex]'",
    "SurfaceBoundaryCondition": "(*values)",
    "SurfaceBoundaryCondition.ADIABATIC": "enum-member:'adiabatic'",
    "SurfaceBoundaryCondition.GROUND": "enum-member:'ground'",
    "SurfaceBoundaryCondition.OUTDOOR": "enum-member:'outdoors'",
    "SurfaceBoundaryCondition.ZONE": "enum-member:'zone'",
    "SurfaceBoundaryCondition.__str__": "(self) -> 'str'",
    "Window": "(name: 'str', glazing: 'Glazing', area: 'int | float', blind: 'Shading | None' = None) -> 'None'",
    "Window.__init__": "(self, name: 'str', glazing: 'Glazing', area: 'int | float', blind: 'Shading | None' = None) -> 'None'",
}

PREFIX = "dragon-shape-opening-adjacency-core."
CASE_SPECS = (
    ("a01-blind-representative", ("Blind", "Blind.__init__"), ()),
    ("a02-blind-unchecked-invalid-state", ("Blind.__init__",), ()),
    ("a03-shade-representative", ("Shade", "Shade.__init__"), ()),
    ("a04-shade-excessive-optical-sum", ("Shade.__init__",), ()),
    ("a05-shading-direct-instantiation", ("Shading",), ()),
    ("a06-window-shading-variants", ("Window", "Window.__init__"), ("Blind", "Shade")),
    ("a07-window-unchecked-invalid-mutable", ("Window.__init__",), ()),
    ("a08-door-representative", ("Door", "Door.__init__"), ()),
    ("a09-door-unchecked-invalid-mutable", ("Door.__init__",), ()),
    ("a10-surface-shared-default-opening-lists", ("Surface.__init__",), ()),
    ("a11-surface-explicit-mixed-opening-alias-order", ("Surface.__init__",), ("Window", "Door")),
    ("a12-surface-blinded-window-fresh-order", ("Surface.blinded_window",), ("Window", "Blind", "Shade")),
    (
        "a13-boundary-enum-and-unlinked-zone",
        (
            "Surface.boundary",
            "SurfaceBoundaryCondition",
            "SurfaceBoundaryCondition.ADIABATIC",
            "SurfaceBoundaryCondition.GROUND",
            "SurfaceBoundaryCondition.OUTDOOR",
            "SurfaceBoundaryCondition.ZONE",
            "SurfaceBoundaryCondition.__str__",
        ),
        ("Surface.__init__",),
    ),
    ("a14-boundary-reciprocal-adjacency", ("Surface.boundary",), ("Surface.__init__",)),
    ("a15-boundary-stale-reassignment-and-self", ("Surface.boundary",), ("Surface.__init__",)),
    (
        "a16-adjacency-positional-zip-truncation",
        ("Surface.boundary",),
        ("Surface.to_idf_object", "Window", "Door"),
    ),
    ("a17-get-subsurface-linear-scale-edge-domain", ("Surface.get_subsurface",), ("Surface.__init__",)),
    ("a18-get-subsurface-oversized-error", ("Surface.get_subsurface",), ("Surface.__init__",)),
)
EXPECTED_CASE_IDS = tuple(PREFIX + item[0] for item in CASE_SPECS)
EXPECTED_CASE_COUNT = 18
EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:f9080ec5ff6a7bbb6d9788e458b572a2957ff2a677148a3ba513fc73a8158b3f",
    EXPECTED_CASE_IDS[1]: "sha256:876b0cd631164560a6e6e5badcd9fc848f3325b049d0291f90101a82198ed7ab",
    EXPECTED_CASE_IDS[2]: "sha256:4535f170e18df765a101f38253a642afbff4dca14449308bb15d6558135f96f7",
    EXPECTED_CASE_IDS[3]: "sha256:6443dc89c3fdda5446a32f31250b8cf96efe117643f0fa37be9642f7d0b13792",
    EXPECTED_CASE_IDS[4]: "sha256:ffcd94abf936bbbf60c884dcc27140b892b2a0f8cfb08e79036a7f3c96632c6b",
    EXPECTED_CASE_IDS[5]: "sha256:4dce9b5e6485f3b3be1d13335d1d423c48d32bec3410419d14781d4e76ed87ed",
    EXPECTED_CASE_IDS[6]: "sha256:02750bd234014e81870497cf6cd8511c524797ac85797d787f4ef31b650108c9",
    EXPECTED_CASE_IDS[7]: "sha256:6c121f349750c9547ddb069adb18235000120a63842c5f58b56cbda7b27477a0",
    EXPECTED_CASE_IDS[8]: "sha256:93b39be1ee324732ddc2601a2c3d6d8c8aff2d91864781c03e614bb3ea7d1eb7",
    EXPECTED_CASE_IDS[9]: "sha256:ccaeeade20e5f5701b4237b4fec87d00ee9d220c56c0692de8bef43f47323ec5",
    EXPECTED_CASE_IDS[10]: "sha256:e4c3da1a51457308b722dadf09a1247aef504583481a2c2fc2bd98f43d5c25d3",
    EXPECTED_CASE_IDS[11]: "sha256:44f0693db9b99c8cc39e1853ae9c6ae8689b88f301d34e3e4e1103065d7c0c1d",
    EXPECTED_CASE_IDS[12]: "sha256:182810f5fe7be8cb5171347a101b5ff934772daa8251d81b55eb9081271043d1",
    EXPECTED_CASE_IDS[13]: "sha256:fab7534265a39f9ed910248300a8d084cf9504dc25ed3b3de3347f56a181b5dd",
    EXPECTED_CASE_IDS[14]: "sha256:516318d8c182edd72b42d63bef9749818cbe220fa2dad2206affa2e2a3f35c53",
    EXPECTED_CASE_IDS[15]: "sha256:d4ef2f888df43dd3baec2c96168b14e2233558f2b445bdf33fa4a6817eb50ca5",
    EXPECTED_CASE_IDS[16]: "sha256:7dd9dcdf4a0ec5f996c73dfe9e4bd0fec5612a53030ac31b435c63d227aa0ed9",
    EXPECTED_CASE_IDS[17]: "sha256:92986ed3d126f8bb177e6694849f68feb824b9e9ea014e55a4464208cf55e025",
}
EXPECTED_CASE_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:b2bf732059c388f149c4a3375e55c7d0b56fc31a9d4c714101511ab7408217d0",
    EXPECTED_CASE_IDS[1]: "sha256:45a43e2bacd6564067fb1bc9c5c2d27459d8af3dbc776235035ea25a68ae9345",
    EXPECTED_CASE_IDS[2]: "sha256:5fdbd59ce5a497d699a5b9778ba0f85176e2913b55d23d435ab759695d67ff6b",
    EXPECTED_CASE_IDS[3]: "sha256:d4f1fa7308fe19242606b1a868e55d35bd1c39c738c14aeee4a7d8f18d5a7932",
    EXPECTED_CASE_IDS[4]: "sha256:ef5c444a8bea4f22fb93b436c893905d040bfc9e74fb305e3e100627c70bced2",
    EXPECTED_CASE_IDS[5]: "sha256:7b9d16ee52fc8aa1582b6ba8e7297aedc1bf307729e53e5d17912ea795ec29d7",
    EXPECTED_CASE_IDS[6]: "sha256:d4c3f02f82d44a7b461d50e70f84eb284b67dfcf61ff1cead93fb89595d59c51",
    EXPECTED_CASE_IDS[7]: "sha256:bc94adb5e13a599494c8bee1a853c8e969ee2f8a6f70e250b02ff693df1d383f",
    EXPECTED_CASE_IDS[8]: "sha256:24e0063bea60899ce7162878a5e69f3270b9eaa60b3a1aea076726402e8344bf",
    EXPECTED_CASE_IDS[9]: "sha256:422c402fd0515082f8e7fe591aef027e56cc758a6eaee37434825388e6c49b2f",
    EXPECTED_CASE_IDS[10]: "sha256:f91da16dad8709b62464757ee82d0ab0f81f2a9399904a26a5a84428125da36b",
    EXPECTED_CASE_IDS[11]: "sha256:ec504f49dc42679c5f7ce4132ede8fd12bef3b3fd05cc0852df0cc2b729e4ce2",
    EXPECTED_CASE_IDS[12]: "sha256:1086dc82bc075cd6af3da1899388bbf1b188d42c28f824ce938960a18352c1a4",
    EXPECTED_CASE_IDS[13]: "sha256:21f4e7f4d7f06134fd9ea09db671ef1359e49f3468003d1f64ab3d30c7760f81",
    EXPECTED_CASE_IDS[14]: "sha256:55cbbe6652a044e435bf39628a6c877d3542084cf1d56fa554737214dc742528",
    EXPECTED_CASE_IDS[15]: "sha256:646013d5fd36efdd5e77c041b32e8fc0a5505d8fed5f98ea6269e43bb71c469f",
    EXPECTED_CASE_IDS[16]: "sha256:c2e3a6894e6511afd6d277292054e7a9c07907ee33440f6134cb8030d24a1b07",
    EXPECTED_CASE_IDS[17]: "sha256:30afd4da99d6fea8f340cd33d342e3350786057ce7cfbe74d9f6a0cf4f36927a",
}

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_shape_opening_adjacency_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load shape oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("Shape oracle support is not exactly pinned.")
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


def _symbol_descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {
        key: value
        for key, value in receipt.items()
        if key != "inventory_index"
    } | {"path": SHAPE_SOURCE_PATH}


def _expected_symbol_descriptors() -> list[dict[str, Any]]:
    return [_symbol_descriptor(receipt) for receipt in TARGET_RECEIPTS]


def _expected_target_receipts() -> list[dict[str, Any]]:
    return [
        {**_symbol_descriptor(receipt), "inventory_index": receipt["inventory_index"]}
        for receipt in TARGET_RECEIPTS
    ]


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    expected_by_symbol = {
        item["symbol"]: item for item in _expected_symbol_descriptors()
    }
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
            symbol: expected_by_symbol[symbol]["symbol_hash"]
            for symbol in source["symbols"]
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
    expected_symbols = [expected_by_symbol[symbol] for symbol in source["symbols"]]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    raw = load_json_without_duplicates(path)
    items = [_load_source_inventory(path, commit, source) for source in SOURCE_SPECS]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in items):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    expected_receipts = _expected_target_receipts()
    observed_receipts = []
    for expected in expected_receipts:
        index = expected["inventory_index"]
        if index >= len(raw["symbols"]):
            raise SystemExit(f"Missing inventory index {index}.")
        observed_receipts.append(
            {**raw["symbols"][index], "inventory_index": index}
        )
    if observed_receipts != expected_receipts:
        raise SystemExit("Exact indexed shape target receipts drifted.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in items],
        "symbols": [symbol for item in items for symbol in item["symbols"]],
        "target_receipts": observed_receipts,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = []
    for slug, targets, context in CASE_SPECS:
        adaptation_ids = sorted({ADAPTATIONS[symbol] for symbol in targets})
        definitions.append(
            {
                "context_symbols": list(context),
                "executor": "shape-opening-adjacency-core",
                "expected_dotnet": {
                    "adaptations": adaptation_ids,
                    "classification": "exception",
                    "outcome": "adapted-or-rejected-as-pinned",
                },
                "id": PREFIX + slug,
                "target_symbols": list(targets),
            }
        )
    return tuple(definitions)


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
    raise RuntimeError(f"Unsupported shape oracle scalar: {type(value).__name__}")


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


def _shading_state(value: Any, shape: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if isinstance(value, shape.Blind):
        return {
            "back_reflectance": _encode(value.back_reflectance),
            "front_reflectance": _encode(value.front_reflectance),
            "kind": "Blind",
            "name": _encode(value.name),
            "slat_angle": _encode(value.slat_angle),
            "slat_separation": _encode(value.slat_separation),
            "slat_width": _encode(value.slat_width),
        }
    if isinstance(value, shape.Shade):
        return {
            "kind": "Shade",
            "name": _encode(value.name),
            "reflectance": _encode(value.reflectance),
            "transmittance": _encode(value.transmittance),
        }
    return {
        "kind": "foreign-shading-reference",
        "name": _encode(getattr(value, "name", None)),
        "type": type(value).__name__,
    }


def _opening_state(value: Any, shape: Any) -> dict[str, Any]:
    if isinstance(value, shape.Window):
        return {
            "area": _encode(value.area),
            "blind": _shading_state(value.blind, shape),
            "glazing": {
                "name": _encode(getattr(value.glazing, "name", None)),
                "type": type(value.glazing).__name__,
            },
            "kind": "Window",
            "name": _encode(value.name),
        }
    if isinstance(value, shape.Door):
        return {
            "area": _encode(value.area),
            "construction": {
                "name": _encode(getattr(value.construction, "name", None)),
                "type": type(value.construction).__name__,
            },
            "kind": "Door",
            "name": _encode(value.name),
        }
    raise RuntimeError(f"Unexpected opening state: {type(value).__name__}")


def _vertices_state(vertices: Any) -> list[list[dict[str, Any]]]:
    return [[_encode(vertex.x), _encode(vertex.y), _encode(vertex.z)] for vertex in vertices]


def _boundary_state(surface: Any, shape: Any) -> dict[str, Any]:
    boundary = surface.boundary
    if isinstance(boundary, shape.Surface):
        return {"kind": "adjacent-surface", "name": boundary.name}
    return {
        "kind": "boundary-condition",
        "name": boundary.name,
        "string": str(boundary),
        "value": boundary.value,
    }


def _surface_state(surface: Any, shape: Any) -> dict[str, Any]:
    return {
        "boundary": _boundary_state(surface, shape),
        "construction": {
            "name": _encode(getattr(surface.construction, "name", None)),
            "type": type(surface.construction).__name__,
        },
        "doors": [_opening_state(item, shape) for item in surface.door],
        "name": _encode(surface.name),
        "surface_type": surface.type.value,
        "vertices": _vertices_state(surface.vertex),
        "windows": [_opening_state(item, shape) for item in surface.window],
    }


def _vertices(shape: Any, *, z: float = 0.0) -> list[Any]:
    return [
        shape.Vertex(0.0, 0.0, z),
        shape.Vertex(4.0, 0.0, z),
        shape.Vertex(4.0, 4.0, z),
        shape.Vertex(0.0, 4.0, z),
    ]


def _vertical_vertices(shape: Any, x: float) -> list[Any]:
    return [
        shape.Vertex(x, 0.0, 0.0),
        shape.Vertex(x, 4.0, 0.0),
        shape.Vertex(x, 4.0, 4.0),
        shape.Vertex(x, 0.0, 4.0),
    ]


def _surface(
    shape: Any,
    construction: Any,
    name: str,
    boundary: Any = "outdoors",
    *,
    vertices: list[Any] | None = None,
    windows: list[Any] | None = None,
    doors: list[Any] | None = None,
) -> Any:
    arguments: list[Any] = [
        name,
        shape.SurfaceType.WALL,
        construction.Construction(name + " Construction"),
        boundary,
        vertices if vertices is not None else _vertices(shape),
    ]
    if windows is not None or doors is not None:
        arguments.extend([windows or [], doors or []])
    return shape.Surface(*arguments)


def _source_state(
    before: Any,
    after: Any,
    *,
    final: Any | None = None,
    unchanged: bool | None = None,
) -> dict[str, Any]:
    result = {"after": after, "before": before, "unchanged": before == after if unchanged is None else unchanged}
    if final is not None:
        result["final"] = final
    return result


def _fact(
    scenario: str,
    observations: dict[str, Any],
    source_state: dict[str, Any],
    timeline: list[dict[str, Any]],
) -> dict[str, Any]:
    return {
        "observations": observations,
        "scenario": scenario,
        "source_state": source_state,
        "timeline": timeline,
    }


def _case_a01(shape: Any) -> dict[str, Any]:
    arguments = {
        "back_reflectance": _encode(0.4),
        "front_reflectance": _encode(0.6),
        "name": _encode("Representative Blind"),
        "slat_angle": _encode(45.0),
        "slat_separation": _encode(0.02),
        "slat_width": _encode(0.025),
    }
    first = shape.Blind("Representative Blind", 0.025, 0.02, 45.0, 0.6, 0.4)
    before = _shading_state(first, shape)
    second = shape.Blind("Representative Blind", 0.025, 0.02, 45.0, 0.6, 0.4)
    after = _shading_state(first, shape)
    return _fact(
        "A01",
        {
            "arguments": arguments,
            "first_is_shading": isinstance(first, shape.Shading),
            "fresh_instances": first is not second,
            "states_equal": before == _shading_state(second, shape),
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "first-construction", "return_type": "Blind"},
            {"outcome": "returned", "phase": "second-construction", "return_type": "Blind"},
        ],
    )


def _case_a02(shape: Any) -> dict[str, Any]:
    blind = shape.Blind("Unchecked Blind", -1.0, 0.0, 999.0, -0.2, 1.5)
    before = _shading_state(blind, shape)

    def mutate() -> None:
        blind.name = None
        blind.slat_width = "mutated-width"
        blind.slat_separation = -7
        blind.slat_angle = float("nan")
        blind.front_reflectance = 8.0
        blind.back_reflectance = -9.0

    mutation = _error(mutate, "post-construction-attribute-mutation")
    after = _shading_state(blind, shape)
    return _fact(
        "A02",
        {
            "construction_accepted_invalid_bundle": True,
            "mutation_accepted_invalid_bundle": mutation["outcome"] == "returned",
            "pre_mutation_invalid_state": before,
        },
        _source_state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "construction", "return_type": "Blind"},
            mutation,
        ],
    )


def _case_a03(shape: Any) -> dict[str, Any]:
    first = shape.Shade("Representative Shade", 0.3, 0.2)
    before = _shading_state(first, shape)
    second = shape.Shade("Representative Shade", 0.3, 0.2)
    after = _shading_state(first, shape)
    return _fact(
        "A03",
        {
            "first_is_shading": isinstance(first, shape.Shading),
            "fresh_instances": first is not second,
            "implied_emissivity": _encode(1 - first.transmittance - first.reflectance),
            "states_equal": before == _shading_state(second, shape),
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "first-construction", "return_type": "Shade"},
            {"outcome": "returned", "phase": "second-construction", "return_type": "Shade"},
        ],
    )


def _case_a04(shape: Any) -> dict[str, Any]:
    shade = shape.Shade("Unchecked Shade", 0.8, 0.7)
    before = _shading_state(shade, shape)

    def mutate() -> None:
        shade.name = 17
        shade.transmittance = float("inf")
        shade.reflectance = -4.0

    mutation = _error(mutate, "post-construction-attribute-mutation")
    after = _shading_state(shade, shape)
    return _fact(
        "A04",
        {
            "construction_accepted_sum_above_one": True,
            "implied_emissivity_before_mutation": _encode(-0.5),
            "mutation_accepted": mutation["outcome"] == "returned",
            "optical_sum": _encode(1.5),
        },
        _source_state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "construction", "return_type": "Shade"},
            mutation,
        ],
    )


def _case_a05(shape: Any) -> dict[str, Any]:
    first = shape.Shading()
    before = {
        "attributes": sorted(first.__dict__),
        "class": type(first).__name__,
    }
    second = shape.Shading()
    after = {
        "attributes": sorted(first.__dict__),
        "class": type(first).__name__,
    }
    return _fact(
        "A05",
        {
            "abstract_method_names": sorted(shape.Shading.__abstractmethods__),
            "direct_instantiation_succeeded": True,
            "fresh_instances": first is not second,
            "mro_names": [item.__name__ for item in type(first).__mro__],
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "first-direct-instantiation", "return_type": "Shading"},
            {"outcome": "returned", "phase": "second-direct-instantiation", "return_type": "Shading"},
        ],
    )


def _case_a06(shape: Any, construction: Any) -> dict[str, Any]:
    glazing = construction.Glazing("Window Variants Glazing", 1.45, 0.41)
    blind = shape.Blind("Window Variant Blind", 0.02, 0.018, 35.0, 0.55, 0.45)
    shade = shape.Shade("Window Variant Shade", 0.25, 0.35)
    windows = [
        shape.Window("Unshaded Window", glazing, 2.0),
        shape.Window("Blind Window", glazing, 1.5, blind),
        shape.Window("Shade Window", glazing, 1.0, shade),
    ]
    before = [_opening_state(item, shape) for item in windows]
    after = [_opening_state(item, shape) for item in windows]
    return _fact(
        "A06",
        {
            "all_glazing_references_preserved": all(item.glazing is glazing for item in windows),
            "shading_identity_flags": [
                windows[0].blind is None,
                windows[1].blind is blind,
                windows[2].blind is shade,
            ],
            "shading_kinds_in_order": [item["blind"]["kind"] for item in before],
            "window_names_in_order": [item.name for item in windows],
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "none-shading-construction", "return_type": "Window"},
            {"outcome": "returned", "phase": "blind-shading-construction", "return_type": "Window"},
            {"outcome": "returned", "phase": "shade-shading-construction", "return_type": "Window"},
        ],
    )


def _case_a07(shape: Any) -> dict[str, Any]:
    foreign_glazing = SimpleNamespace(name="Foreign Glazing")
    foreign_blind = SimpleNamespace(name="Foreign Blind")
    windows = [
        shape.Window("Negative Area Window", foreign_glazing, -3.0, foreign_blind),
        shape.Window("NaN Area Window", foreign_glazing, float("nan"), foreign_blind),
        shape.Window("Infinite Area Window", foreign_glazing, float("inf"), foreign_blind),
    ]
    before = [_opening_state(item, shape) for item in windows]

    def mutate() -> None:
        windows[0].name = None
        windows[0].glazing = SimpleNamespace(name="Mutated Foreign Glazing")
        windows[0].area = float("-inf")
        windows[0].blind = 42

    mutation = _error(mutate, "post-construction-attribute-mutation")
    after = [_opening_state(item, shape) for item in windows]
    return _fact(
        "A07",
        {
            "all_invalid_constructions_returned": True,
            "area_kinds": [item["area"]["kind"] for item in before],
            "foreign_blind_reference_preserved": all(item.blind is foreign_blind for item in windows[1:]),
            "foreign_glazing_reference_preserved": all(item.glazing is foreign_glazing for item in windows[1:]),
            "mutation_accepted": mutation["outcome"] == "returned",
        },
        _source_state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "negative-area-construction", "return_type": "Window"},
            {"outcome": "returned", "phase": "nan-area-construction", "return_type": "Window"},
            {"outcome": "returned", "phase": "infinite-area-construction", "return_type": "Window"},
            mutation,
        ],
    )


def _case_a08(shape: Any, construction: Any) -> dict[str, Any]:
    assembly = construction.NoMassConstruction("Representative Door Assembly", 1.8)
    first = shape.Door("Representative Door", assembly, 2.1)
    before = _opening_state(first, shape)
    second = shape.Door("Representative Door", assembly, 2.1)
    after = _opening_state(first, shape)
    return _fact(
        "A08",
        {
            "construction_reference_preserved": first.construction is assembly,
            "fresh_instances": first is not second,
            "states_equal": before == _opening_state(second, shape),
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "first-construction", "return_type": "Door"},
            {"outcome": "returned", "phase": "second-construction", "return_type": "Door"},
        ],
    )


def _case_a09(shape: Any) -> dict[str, Any]:
    foreign_construction = SimpleNamespace(name="Foreign Door Construction")
    doors = [
        shape.Door("Negative Area Door", foreign_construction, -2.0),
        shape.Door("NaN Area Door", foreign_construction, float("nan")),
        shape.Door("Infinite Area Door", foreign_construction, float("inf")),
    ]
    before = [_opening_state(item, shape) for item in doors]

    def mutate() -> None:
        doors[0].name = 99
        doors[0].construction = SimpleNamespace(name="Mutated Door Construction")
        doors[0].area = float("-inf")

    mutation = _error(mutate, "post-construction-attribute-mutation")
    after = [_opening_state(item, shape) for item in doors]
    return _fact(
        "A09",
        {
            "all_invalid_constructions_returned": True,
            "area_kinds": [item["area"]["kind"] for item in before],
            "foreign_construction_reference_preserved": all(
                item.construction is foreign_construction for item in doors[1:]
            ),
            "mutation_accepted": mutation["outcome"] == "returned",
        },
        _source_state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "negative-area-construction", "return_type": "Door"},
            {"outcome": "returned", "phase": "nan-area-construction", "return_type": "Door"},
            {"outcome": "returned", "phase": "infinite-area-construction", "return_type": "Door"},
            mutation,
        ],
    )


def _case_a10(shape: Any, construction: Any) -> dict[str, Any]:
    first = _surface(shape, construction, "First Default Surface")
    second = _surface(shape, construction, "Second Default Surface")
    defaults = shape.Surface.__init__.__defaults__
    if defaults is None or len(defaults) != 2:
        raise RuntimeError("Surface default opening lists drifted structurally.")
    default_windows, default_doors = defaults
    glazing = construction.Glazing("Default Alias Glazing", 1.5, 0.4)
    door_construction = construction.NoMassConstruction(
        "Default Alias Door Construction", 1.7
    )
    sentinel_window = shape.Window("Default Alias Window", glazing, 1.0)
    sentinel_door = shape.Door("Default Alias Door", door_construction, 1.2)
    before = {
        "default_door_names": [item.name for item in default_doors],
        "default_window_names": [item.name for item in default_windows],
        "first": _surface_state(first, shape),
        "second": _surface_state(second, shape),
    }
    append_timeline: list[dict[str, Any]] = []
    try:
        default_windows.append(sentinel_window)
        append_timeline.append(
            {"outcome": "returned", "phase": "append-default-window", "return_type": "NoneType"}
        )
        first.door.append(sentinel_door)
        append_timeline.append(
            {"outcome": "returned", "phase": "append-first-door", "return_type": "NoneType"}
        )
        after = {
            "default_door_names": [item.name for item in default_doors],
            "default_window_names": [item.name for item in default_windows],
            "first": _surface_state(first, shape),
            "second": _surface_state(second, shape),
        }
        mutation_visible = (
            first.window[-1] is sentinel_window
            and second.window[-1] is sentinel_window
            and first.door[-1] is sentinel_door
            and second.door[-1] is sentinel_door
        )
    finally:
        if default_windows and default_windows[-1] is sentinel_window:
            default_windows.pop()
        if default_doors and default_doors[-1] is sentinel_door:
            default_doors.pop()
    final = {
        "default_door_names": [item.name for item in default_doors],
        "default_window_names": [item.name for item in default_windows],
        "first": _surface_state(first, shape),
        "second": _surface_state(second, shape),
    }
    return _fact(
        "A10",
        {
            "default_door_list_is_both_instances": default_doors is first.door is second.door,
            "default_window_list_is_both_instances": default_windows is first.window is second.window,
            "mutation_visible_through_both_surfaces": mutation_visible,
            "restored_after_observation": final == before,
        },
        _source_state(before, after, final=final, unchanged=False),
        [
            {"outcome": "returned", "phase": "first-default-construction", "return_type": "Surface"},
            {"outcome": "returned", "phase": "second-default-construction", "return_type": "Surface"},
            *append_timeline,
            {"outcome": "returned", "phase": "restore-shared-default-lists", "return_type": "NoneType"},
        ],
    )


def _case_a11(shape: Any, construction: Any) -> dict[str, Any]:
    glazing = construction.Glazing("Explicit Alias Glazing", 1.4, 0.45)
    door_construction = construction.NoMassConstruction(
        "Explicit Alias Door Construction", 1.9
    )
    windows = [
        shape.Window("Explicit Window 1", glazing, 1.0),
        shape.Window("Explicit Window 2", glazing, 1.1),
    ]
    doors = [
        shape.Door("Explicit Door 1", door_construction, 1.8),
        shape.Door("Explicit Door 2", door_construction, 1.9),
    ]
    surface = _surface(
        shape,
        construction,
        "Explicit Alias Surface",
        windows=windows,
        doors=doors,
    )
    before = {
        "input_doors": [_opening_state(item, shape) for item in doors],
        "input_windows": [_opening_state(item, shape) for item in windows],
        "surface": _surface_state(surface, shape),
    }
    appended_window = shape.Window("Explicit Window 3", glazing, 1.2)
    appended_door = shape.Door("Explicit Door 3", door_construction, 2.0)
    windows.append(appended_window)
    surface.door.append(appended_door)
    after = {
        "input_doors": [_opening_state(item, shape) for item in doors],
        "input_windows": [_opening_state(item, shape) for item in windows],
        "surface": _surface_state(surface, shape),
    }
    return _fact(
        "A11",
        {
            "door_input_alias_preserved": surface.door is doors,
            "door_names_after_mutation": [item.name for item in surface.door],
            "input_mutation_visible_on_surface": surface.window[-1] is appended_window,
            "separate_window_then_door_collections": surface.window is not surface.door,
            "surface_mutation_visible_on_input": doors[-1] is appended_door,
            "window_input_alias_preserved": surface.window is windows,
            "window_names_after_mutation": [item.name for item in surface.window],
        },
        _source_state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "surface-construction", "return_type": "Surface"},
            {"outcome": "returned", "phase": "append-through-window-input", "return_type": "NoneType"},
            {"outcome": "returned", "phase": "append-through-surface-door-list", "return_type": "NoneType"},
        ],
    )


def _case_a12(shape: Any, construction: Any) -> dict[str, Any]:
    glazing = construction.Glazing("Blinded Projection Glazing", 1.35, 0.5)
    blind_one = shape.Blind("Projection Blind 1", 0.02, 0.018, 25.0, 0.5, 0.45)
    shade = shape.Shade("Projection Shade", 0.2, 0.4)
    blind_two = shape.Blind("Projection Blind 2", 0.03, 0.025, 55.0, 0.6, 0.35)
    windows = [
        shape.Window("Plain 1", glazing, 0.8),
        shape.Window("Blind 1", glazing, 0.9, blind_one),
        shape.Window("Shade", glazing, 1.0, shade),
        shape.Window("Blind 2", glazing, 1.1, blind_two),
        shape.Window("Plain 2", glazing, 1.2),
    ]
    surface = _surface(
        shape,
        construction,
        "Blinded Projection Surface",
        windows=windows,
        doors=[],
    )
    before = _surface_state(surface, shape)
    first = surface.blinded_window
    second = surface.blinded_window
    first_before_mutation = [item.name for item in first]
    first.clear()
    first.append(windows[0])
    after = _surface_state(surface, shape)
    return _fact(
        "A12",
        {
            "first_projection_after_local_mutation": [item.name for item in first],
            "first_projection_before_local_mutation": first_before_mutation,
            "fresh_projection_lists": first is not second,
            "projected_items_are_source_windows": [
                second[0] is windows[1],
                second[1] is windows[2],
                second[2] is windows[3],
            ],
            "second_projection_after_first_mutation": [item.name for item in second],
            "source_window_order_after_projection_mutation": [item.name for item in surface.window],
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "first-property-read", "return_type": "list"},
            {"outcome": "returned", "phase": "second-property-read", "return_type": "list"},
            {"outcome": "returned", "phase": "mutate-first-projection-list", "return_type": "NoneType"},
        ],
    )


def _case_a13(shape: Any, construction: Any) -> dict[str, Any]:
    enum_records = [
        {
            "equal_to_raw_string": item == item.value,
            "is_str_instance": isinstance(item, str),
            "name": item.name,
            "round_trip_is_same_member": shape.SurfaceBoundaryCondition(item.value) is item,
            "string": str(item),
            "value": item.value,
        }
        for item in shape.SurfaceBoundaryCondition
    ]
    surface = _surface(
        shape,
        construction,
        "Unlinked Zone Boundary Surface",
        boundary="zone",
    )
    before = _surface_state(surface, shape)
    invalid = _error(
        lambda: shape.SurfaceBoundaryCondition("surface"),
        "invalid-enum-conversion",
    )
    after = _surface_state(surface, shape)
    return _fact(
        "A13",
        {
            "definition_order": [item["name"] for item in enum_records],
            "enum_records": enum_records,
            "invalid_enum_conversion": invalid,
            "unlinked_zone_boundary_allowed": surface.boundary is shape.SurfaceBoundaryCondition.ZONE,
            "unlinked_zone_boundary_is_surface": isinstance(surface.boundary, shape.Surface),
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "enum-member-observation", "return_type": "list"},
            {"outcome": "returned", "phase": "unlinked-zone-surface-construction", "return_type": "Surface"},
            invalid,
        ],
    )


def _case_a14(shape: Any, construction: Any) -> dict[str, Any]:
    first = _surface(shape, construction, "Reciprocal Surface A")
    second = _surface(shape, construction, "Reciprocal Surface B")
    before = {
        "first": _surface_state(first, shape),
        "second": _surface_state(second, shape),
    }
    mutation = _error(
        lambda: setattr(first, "boundary", second),
        "assign-first-boundary-to-second",
    )
    after = {
        "first": _surface_state(first, shape),
        "second": _surface_state(second, shape),
    }
    return _fact(
        "A14",
        {
            "first_getter_returns_second": first.boundary is second,
            "first_private_condition_is_zone": first._Surface__boundary is shape.SurfaceBoundaryCondition.ZONE,
            "mutation_returned": mutation["outcome"] == "returned",
            "second_getter_returns_first": second.boundary is first,
            "second_private_condition_is_zone": second._Surface__boundary is shape.SurfaceBoundaryCondition.ZONE,
        },
        _source_state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "construct-distinct-surfaces", "return_type": "tuple"},
            mutation,
        ],
    )


def _case_a15(shape: Any, construction: Any) -> dict[str, Any]:
    first = _surface(shape, construction, "Stale Surface A")
    old = _surface(shape, construction, "Stale Surface B")
    replacement = _surface(shape, construction, "Stale Surface C")
    self_surface = _surface(shape, construction, "Self Adjacent Surface")
    before = {
        "first": _surface_state(first, shape),
        "old": _surface_state(old, shape),
        "replacement": _surface_state(replacement, shape),
        "self": _surface_state(self_surface, shape),
    }
    first_link = _error(
        lambda: setattr(first, "boundary", old),
        "assign-first-to-old",
    )
    replacement_link = _error(
        lambda: setattr(first, "boundary", replacement),
        "reassign-first-to-replacement",
    )
    self_link = _error(
        lambda: setattr(self_surface, "boundary", self_surface),
        "assign-self-adjacency",
    )
    after = {
        "first": _surface_state(first, shape),
        "old": _surface_state(old, shape),
        "replacement": _surface_state(replacement, shape),
        "self": _surface_state(self_surface, shape),
    }
    return _fact(
        "A15",
        {
            "first_points_to_replacement": first.boundary is replacement,
            "old_retains_stale_first_link": old.boundary is first,
            "replacement_points_to_first": replacement.boundary is first,
            "self_adjacency_allowed": self_surface.boundary is self_surface,
            "all_mutations_returned": all(
                item["outcome"] == "returned"
                for item in (first_link, replacement_link, self_link)
            ),
        },
        _source_state(before, after, unchanged=False),
        [
            {"outcome": "returned", "phase": "construct-four-surfaces", "return_type": "tuple"},
            first_link,
            replacement_link,
            self_link,
        ],
    )


def _idf_opening_links(batch: list[Any]) -> list[dict[str, Any]]:
    links = []
    for item in batch:
        if item.idd.name not in ("Window:Interzone", "Door:Interzone"):
            continue
        links.append(
            {
                "building_surface_name": _encode(item.data["Building Surface Name"]),
                "counterpart_name": _encode(item.data["Outside Boundary Condition Object"]),
                "name": _encode(item.data["Name"]),
                "object_type": item.idd.name,
            }
        )
    return links


def _case_a16(shape: Any, construction: Any) -> dict[str, Any]:
    glazing = construction.Glazing("Zip Witness Glazing", 1.45, 0.42)
    door_construction = construction.NoMassConstruction(
        "Zip Witness Door Construction", 1.8
    )
    a_windows = [
        shape.Window("A Window 1", glazing, 1.0),
        shape.Window("A Window 2", glazing, 1.1),
        shape.Window("A Window 3 Truncated", glazing, 1.2),
    ]
    b_windows = [
        shape.Window("B Window 2 First", glazing, 1.1),
        shape.Window("B Window 1 Second", glazing, 1.0),
    ]
    a_doors = [
        shape.Door("A Door 1", door_construction, 1.8),
        shape.Door("A Door 2 Truncated", door_construction, 1.9),
    ]
    b_doors = [shape.Door("B Door 1", door_construction, 1.8)]
    first = _surface(
        shape,
        construction,
        "Zip Surface A",
        vertices=_vertical_vertices(shape, 0.0),
        windows=a_windows,
        doors=a_doors,
    )
    second = _surface(
        shape,
        construction,
        "Zip Surface B",
        vertices=_vertical_vertices(shape, 0.0),
        windows=b_windows,
        doors=b_doors,
    )
    first.boundary = second
    before = {
        "first": _surface_state(first, shape),
        "second": _surface_state(second, shape),
    }
    first_batch = first.to_idf_object(SimpleNamespace(name="Zip Zone A"))
    second_batch = second.to_idf_object(SimpleNamespace(name="Zip Zone B"))
    repeat_first = first.to_idf_object(SimpleNamespace(name="Zip Zone A"))
    repeat_second = second.to_idf_object(SimpleNamespace(name="Zip Zone B"))
    after = {
        "first": _surface_state(first, shape),
        "second": _surface_state(second, shape),
    }
    first_links = _idf_opening_links(first_batch)
    second_links = _idf_opening_links(second_batch)
    opening_name_accounting = []
    for surface, links in ((first, first_links), (second, second_links)):
        authored_names = [
            *[item.name for item in surface.window],
            *[item.name for item in surface.door],
        ]
        emitted_names = [item["name"]["value"] for item in links]
        opening_name_accounting.append(
            {
                "authored_names": authored_names,
                "emitted_names": emitted_names,
                "not_emitted_names": [
                    name for name in authored_names if name not in emitted_names
                ],
                "surface_name": surface.name,
            }
        )
    return _fact(
        "A16",
        {
            "first_call": {
                "links": first_links,
                "object_types": [item.idd.name for item in first_batch],
            },
            "fresh_batches": first_batch is not repeat_first and second_batch is not repeat_second,
            "fresh_objects": all(
                left is not right
                for left, right in zip(
                    first_batch + second_batch,
                    repeat_first + repeat_second,
                    strict=True,
                )
            ),
            "positional_links": [
                [item["name"]["value"], item["counterpart_name"]["value"]]
                for item in first_links + second_links
            ],
            "opening_name_accounting": opening_name_accounting,
            "repeat_links_equal": (
                first_links == _idf_opening_links(repeat_first)
                and second_links == _idf_opening_links(repeat_second)
            ),
            "second_call": {
                "links": second_links,
                "object_types": [item.idd.name for item in second_batch],
            },
        },
        _source_state(before, after),
        [
            {"outcome": "returned", "phase": "reciprocal-boundary-assignment", "return_type": "NoneType"},
            {"outcome": "returned", "phase": "first-parent-context-emission", "return_type": "list"},
            {"outcome": "returned", "phase": "second-parent-context-emission", "return_type": "list"},
            {"outcome": "returned", "phase": "repeat-parent-context-emissions", "return_type": "tuple"},
        ],
    )


def _polygon_area_xy(vertices: list[Any]) -> float:
    return abs(
        sum(
            left.x * right.y - right.x * left.y
            for left, right in zip(vertices, vertices[1:] + vertices[:1], strict=True)
        )
    ) / 2


def _case_a17(shape: Any, construction: Any) -> dict[str, Any]:
    surface = _surface(shape, construction, "Linear Scale Host")
    before = _surface_state(surface, shape)
    observations = []
    timeline = []
    for target in (4.0, 16.0, 0.0, -4.0):
        first = surface.get_subsurface(target)
        second = surface.get_subsurface(target)
        observations.append(
            {
                "coordinate_results": _vertices_state(first),
                "fresh_result_lists": first is not second,
                "fresh_vertices": all(
                    left is not right
                    for left, right in zip(first, second, strict=True)
                ),
                "host_area": _encode(surface.area),
                "linear_scale_factor": _encode(target / surface.area),
                "repeat_coordinates_equal": _vertices_state(first) == _vertices_state(second),
                "result_polygon_area": _encode(_polygon_area_xy(first)),
                "target_area": _encode(target),
            }
        )
        timeline.extend(
            [
                {"outcome": "returned", "phase": f"target-{repr(target)}-first-call", "return_type": "list"},
                {"outcome": "returned", "phase": f"target-{repr(target)}-second-call", "return_type": "list"},
            ]
        )
    after = _surface_state(surface, shape)
    return _fact(
        "A17",
        {
            "equal_zero_and_negative_targets_all_returned": True,
            "target_observations": observations,
        },
        _source_state(before, after),
        timeline,
    )


def _case_a18(shape: Any, construction: Any) -> dict[str, Any]:
    surface = _surface(shape, construction, "Oversized Host")
    before = _surface_state(surface, shape)
    first = _error(lambda: surface.get_subsurface(20.0), "oversized-first-call")
    second = _error(lambda: surface.get_subsurface(20.0), "oversized-second-call")
    after = _surface_state(surface, shape)
    return _fact(
        "A18",
        {
            "errors_equal": first == {**second, "phase": first["phase"]},
            "first_error": first,
            "host_area": _encode(surface.area),
            "second_error": second,
            "target_area": _encode(20.0),
        },
        _source_state(before, after),
        [first, second],
    )


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    slug = identifier.removeprefix(PREFIX)
    shape = modules.shape
    construction = modules.construction
    functions: dict[str, Callable[[], dict[str, Any]]] = {
        "a01-blind-representative": lambda: _case_a01(shape),
        "a02-blind-unchecked-invalid-state": lambda: _case_a02(shape),
        "a03-shade-representative": lambda: _case_a03(shape),
        "a04-shade-excessive-optical-sum": lambda: _case_a04(shape),
        "a05-shading-direct-instantiation": lambda: _case_a05(shape),
        "a06-window-shading-variants": lambda: _case_a06(shape, construction),
        "a07-window-unchecked-invalid-mutable": lambda: _case_a07(shape),
        "a08-door-representative": lambda: _case_a08(shape, construction),
        "a09-door-unchecked-invalid-mutable": lambda: _case_a09(shape),
        "a10-surface-shared-default-opening-lists": lambda: _case_a10(shape, construction),
        "a11-surface-explicit-mixed-opening-alias-order": lambda: _case_a11(shape, construction),
        "a12-surface-blinded-window-fresh-order": lambda: _case_a12(shape, construction),
        "a13-boundary-enum-and-unlinked-zone": lambda: _case_a13(shape, construction),
        "a14-boundary-reciprocal-adjacency": lambda: _case_a14(shape, construction),
        "a15-boundary-stale-reassignment-and-self": lambda: _case_a15(shape, construction),
        "a16-adjacency-positional-zip-truncation": lambda: _case_a16(shape, construction),
        "a17-get-subsurface-linear-scale-edge-domain": lambda: _case_a17(shape, construction),
        "a18-get-subsurface-oversized-error": lambda: _case_a18(shape, construction),
    }
    try:
        return functions[slug]()
    except KeyError as error:
        raise RuntimeError(f"Unknown opening/adjacency case: {identifier}") from error


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


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "Python constructors preserve unchecked and mutable scalar/reference state; "
            "Surface preserves caller/default list aliases, its blinded-window projection "
            "returns a fresh filtered list, adjacency mutates reciprocal objects without "
            "stale/self protection, interzone parent context uses positional zip truncation, "
            "and get_subsurface scales linearly while accepting equal, zero, and negative areas. "
            "Native records, exact polygons, immutable copies, strict adjacency validation, and "
            "square-root/domain-correct subsurface creation require explicit exception bindings."
        ),
        "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
        "closure": {
            "already_covered_emitters_not_retargeted": [
                "Blind.to_idf_object",
                "Door.to_idf_object",
                "Shade.to_idf_object",
                "Surface.to_idf_object",
                "Window.to_idf_object",
            ],
            "case_coverage_by_symbol": _coverage_by_symbol(),
            "context_only_not_targeted": [
                "Construction",
                "Glazing",
                "IdfObject",
                "NoMassConstruction",
                "Surface",
                "Surface.to_idf_object",
                "SurfaceType",
                "Vertex",
                "Zone.name",
            ],
            "deferred_generic_geometry_contracts": [
                "Surface.area",
                "Surface.center",
                "Surface.height",
                "Surface.normal",
                "Surface.type",
                "Surface.vertex",
                "SurfaceType",
                "SurfaceType.__str__",
                "Vertex",
            ],
            "full_symbol_closure": False,
            "parent_emission_context_case_ids": [EXPECTED_CASE_IDS[15]],
            "parent_emission_is_context_only": True,
            "scope": "exact-nineteen-symbol-opening-adjacency-core-A01-through-A18",
            "target_coverage_complete": True,
            "target_symbols": list(TARGET_SYMBOLS),
            "unresolved_target_behavior": [
                "Surface.get_subsurface-nan-positive-infinity-and-negative-infinity-inputs",
                "Surface.get_subsurface-nonnumeric-inputs-and-arithmetic-error-timing",
            ],
        },
        "identity_encoding": "stable-boolean-relations-only-no-id-or-address",
        "native_targets": NATIVE_TARGETS,
        "raw_fact_encoding": (
            "typed-scalars-with-explicit-nonfinite-classes-and-phase-bound-errors"
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
    value = shape
    for segment in symbol.split("."):
        value = getattr(value, segment)
    return value


def _runtime_signature(value: Any, shape: Any) -> str:
    if isinstance(value, property):
        result = "property:fget=" + str(inspect.signature(value.fget))
        if value.fset is not None:
            result += ";fset=" + str(inspect.signature(value.fset))
        return result
    if isinstance(value, shape.SurfaceBoundaryCondition):
        return "enum-member:" + repr(value.value)
    return str(inspect.signature(value))


def _runtime_signatures(shape: Any) -> dict[str, str]:
    return {
        symbol: _runtime_signature(_resolve_symbol(shape, symbol), shape)
        for symbol in TARGET_SYMBOLS
    }


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    expected_hash = EXPECTED_FACT_SHA256.get(identifier)
    actual_hash = canonical_sha256(facts)
    if expected_hash is not None and actual_hash != expected_hash:
        raise RuntimeError(
            f"Opening/adjacency canonical semantics drifted: {identifier}: {actual_hash}"
        )
    if set(facts) != {"observations", "scenario", "source_state", "timeline"}:
        raise RuntimeError(f"Opening/adjacency fact key set drifted: {identifier}")
    case_number = int(identifier[len(PREFIX) + 1 : len(PREFIX) + 3])
    if facts["scenario"] != f"A{case_number:02d}":
        raise RuntimeError(f"Opening/adjacency scenario label drifted: {identifier}")
    source = facts["source_state"]
    if set(source) not in (
        {"after", "before", "unchanged"},
        {"after", "before", "final", "unchanged"},
    ):
        raise RuntimeError(f"Opening/adjacency source-state shape drifted: {identifier}")
    if not isinstance(source["unchanged"], bool):
        raise RuntimeError(f"Opening/adjacency source-state flag drifted: {identifier}")
    timeline = facts["timeline"]
    if not isinstance(timeline, list) or not timeline:
        raise RuntimeError(f"Opening/adjacency timeline drifted: {identifier}")
    for event in timeline:
        if event.get("outcome") not in ("raised", "returned") or not isinstance(
            event.get("phase"), str
        ):
            raise RuntimeError(f"Opening/adjacency event drifted: {identifier}")
        if event["outcome"] == "raised" and set(event) != {
            "error",
            "outcome",
            "phase",
        }:
            raise RuntimeError(f"Opening/adjacency error timing drifted: {identifier}")

    observations = facts["observations"]
    scenario = facts["scenario"]
    if scenario == "A01":
        valid = observations["fresh_instances"] and observations["states_equal"] and source["unchanged"]
    elif scenario == "A02":
        valid = observations["construction_accepted_invalid_bundle"] and observations["mutation_accepted_invalid_bundle"] and not source["unchanged"]
    elif scenario == "A03":
        valid = observations["fresh_instances"] and observations["implied_emissivity"] == _encode(1 - 0.3 - 0.2) and source["unchanged"]
    elif scenario == "A04":
        valid = observations["construction_accepted_sum_above_one"] and observations["optical_sum"] == _encode(1.5) and observations["implied_emissivity_before_mutation"] == _encode(-0.5) and not source["unchanged"]
    elif scenario == "A05":
        valid = observations["direct_instantiation_succeeded"] and observations["abstract_method_names"] == [] and observations["fresh_instances"]
    elif scenario == "A06":
        valid = observations["shading_kinds_in_order"] == ["none", "Blind", "Shade"] and all(observations["shading_identity_flags"]) and source["unchanged"]
    elif scenario == "A07":
        valid = observations["area_kinds"] == ["float", "float-nonfinite", "float-nonfinite"] and observations["mutation_accepted"] and not source["unchanged"]
    elif scenario == "A08":
        valid = observations["construction_reference_preserved"] and observations["fresh_instances"] and observations["states_equal"] and source["unchanged"]
    elif scenario == "A09":
        valid = observations["area_kinds"] == ["float", "float-nonfinite", "float-nonfinite"] and observations["mutation_accepted"] and not source["unchanged"]
    elif scenario == "A10":
        valid = observations["default_window_list_is_both_instances"] and observations["default_door_list_is_both_instances"] and observations["mutation_visible_through_both_surfaces"] and observations["restored_after_observation"] and source["final"] == source["before"] and not source["unchanged"]
    elif scenario == "A11":
        valid = observations["window_input_alias_preserved"] and observations["door_input_alias_preserved"] and observations["input_mutation_visible_on_surface"] and observations["surface_mutation_visible_on_input"] and not source["unchanged"]
    elif scenario == "A12":
        valid = observations["fresh_projection_lists"] and observations["first_projection_before_local_mutation"] == ["Blind 1", "Shade", "Blind 2"] and observations["second_projection_after_first_mutation"] == ["Blind 1", "Shade", "Blind 2"] and source["unchanged"]
    elif scenario == "A13":
        valid = observations["definition_order"] == ["OUTDOOR", "GROUND", "ADIABATIC", "ZONE"] and observations["unlinked_zone_boundary_allowed"] and not observations["unlinked_zone_boundary_is_surface"] and observations["invalid_enum_conversion"]["outcome"] == "raised" and observations["invalid_enum_conversion"]["error"]["type"] == "ValueError" and source["unchanged"]
    elif scenario == "A14":
        valid = observations["first_getter_returns_second"] and observations["second_getter_returns_first"] and observations["first_private_condition_is_zone"] and observations["second_private_condition_is_zone"] and not source["unchanged"]
    elif scenario == "A15":
        valid = observations["old_retains_stale_first_link"] and observations["first_points_to_replacement"] and observations["replacement_points_to_first"] and observations["self_adjacency_allowed"] and not source["unchanged"]
    elif scenario == "A16":
        expected_links = [
            ["A Window 1", "B Window 2 First"],
            ["A Window 2", "B Window 1 Second"],
            ["A Door 1", "B Door 1"],
            ["B Window 2 First", "A Window 1"],
            ["B Window 1 Second", "A Window 2"],
            ["B Door 1", "A Door 1"],
        ]
        accounting = observations["opening_name_accounting"]
        accounting_is_derived = all(
            item["not_emitted_names"]
            == [
                name
                for name in item["authored_names"]
                if name not in item["emitted_names"]
            ]
            for item in accounting
        )
        valid = observations["positional_links"] == expected_links and len(accounting) == 2 and accounting_is_derived and observations["repeat_links_equal"] and observations["fresh_batches"] and observations["fresh_objects"] and source["unchanged"]
    elif scenario == "A17":
        expected_areas = [_encode(1.0), _encode(16.0), _encode(0.0), _encode(1.0)]
        observed_areas = [item["result_polygon_area"] for item in observations["target_observations"]]
        valid = observations["equal_zero_and_negative_targets_all_returned"] and observed_areas == expected_areas and all(item["fresh_result_lists"] and item["fresh_vertices"] and item["repeat_coordinates_equal"] for item in observations["target_observations"]) and source["unchanged"]
    elif scenario == "A18":
        valid = observations["errors_equal"] and observations["first_error"]["outcome"] == "raised" and observations["first_error"]["error"]["type"] == "ValueError" and observations["second_error"]["outcome"] == "raised" and source["unchanged"]
    else:
        valid = False
    if not valid:
        raise RuntimeError(f"Opening/adjacency semantic invariant drifted: {identifier}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
        "target_receipts": _expected_target_receipts(),
    }:
        raise SystemExit("The aggregate opening/adjacency inventory is not exact.")
    for source in SOURCE_SPECS:
        source_file = _source_file(imported_root, source)
        if sha256_file(source_file) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    shape_file = imported_root / Path(SHAPE_SOURCE_PATH).relative_to("src")
    if shape_file.stat().st_size != 27_438:
        raise SystemExit("Pinned shape.py byte length drifted.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        construction = importlib.import_module("idragon.dragon.construction")
        modules.construction = construction
        if _runtime_signatures(modules.shape) != RUNTIME_SIGNATURES:
            raise SystemExit("Pinned opening/adjacency runtime signatures drifted.")
        if (
            modules.shape.Construction is not construction.Construction
            or modules.shape.Glazing is not construction.Glazing
            or modules.shape.NoMassConstruction is not construction.NoMassConstruction
            or modules.shape.IdfObject is not modules.imugi.IdfObject
        ):
            raise SystemExit("Pinned opening/adjacency import identities drifted.")

        observed = {
            definition["id"]: _execute_case(definition["id"], modules)
            for definition in case_definitions()
        }
        observed_fact_hashes = {
            identifier: canonical_sha256(facts)
            for identifier, facts in observed.items()
        }
        if EXPECTED_FACT_SHA256 and observed_fact_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned opening/adjacency per-case facts drifted.\nOBSERVED_FACT_HASHES\n"
                + strict_json_dumps(observed_fact_hashes, indent=2)
            )

        cases = []
        for definition in case_definitions():
            identifier = definition["id"]
            facts = observed[identifier]
            _validate_case_facts(identifier, facts)
            case = dict(definition)
            case["python"] = {
                "facts": facts,
                "facts_sha256": observed_fact_hashes[identifier],
                "outcome": "observed",
            }
            cases.append(case)
        observed_case_hashes = case_sha256(cases)
        if EXPECTED_CASE_SHA256 and observed_case_hashes != EXPECTED_CASE_SHA256:
            raise SystemExit(
                "Pinned opening/adjacency per-case records drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(observed_case_hashes, indent=2)
            )

        result = {
            "case_sha256": observed_case_hashes,
            "cases": cases,
            "cases_sha256": cases_sha256(cases),
            "consumer_contract": _expected_consumer_contract(),
            "fact_sha256": observed_fact_hashes,
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
        if (
            not math.isfinite(decoded)
            or decoded.hex() != value["hex"]
            or repr(decoded) != value["repr"]
        ):
            raise RuntimeError(f"Unsafe encoded float at {location}.")
        return True
    if kind == "float-nonfinite":
        _require_keys(value, {"kind", "value"}, location)
        if value["value"] not in {
            "nan",
            "negative-infinity",
            "positive-infinity",
        }:
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
        raise RuntimeError("Opening/adjacency schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Opening/adjacency cases hash drifted.")
    if value["case_sha256"] != case_sha256(value["cases"]):
        raise RuntimeError("Opening/adjacency per-case hash map drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
        or tuple(sorted(EXPECTED_CASE_IDS)) != EXPECTED_CASE_IDS
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Opening/adjacency case order/count drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Opening/adjacency case contract drifted: {case['id']}")
        _require_keys(
            case["python"], {"facts", "facts_sha256", "outcome"}, "python"
        )
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"Opening/adjacency outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"Opening/adjacency inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_facts(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Opening/adjacency fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("Opening/adjacency expected fact hashes drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("Opening/adjacency expected case hashes drifted.")

    target_counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if set(target_counts) != set(TARGET_SYMBOLS) or any(
        count < 1 for count in target_counts.values()
    ):
        raise RuntimeError("Opening/adjacency target coverage drifted.")
    if any(
        "Surface.to_idf_object" in definition["target_symbols"]
        for definition in definitions
    ):
        raise RuntimeError("Surface.to_idf_object was incorrectly promoted to a target.")
    parent_context_cases = [
        definition["id"]
        for definition in definitions
        if "Surface.to_idf_object" in definition["context_symbols"]
    ]
    if parent_context_cases != [EXPECTED_CASE_IDS[15]]:
        raise RuntimeError("Parent-emission context closure drifted.")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Opening/adjacency consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Opening/adjacency runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Opening/adjacency upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Opening/adjacency symbol receipts drifted.")
    if value["target_receipts"] != _expected_target_receipts():
        raise RuntimeError("Opening/adjacency indexed target receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned checkout.")
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
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote dragon shape opening/adjacency core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

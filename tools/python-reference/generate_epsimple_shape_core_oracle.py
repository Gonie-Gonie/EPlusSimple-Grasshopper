"""Generate the pinned EPlusSimple area-based shape-core oracle.

The corpus executes exactly the 53 unresolved public declarations in
``src/epsimple/core/shape.py`` selected by the compatibility audit.  Hash,
repr, and string-representation declarations outside that slice remain
explicitly excluded.  Run this file through ``bootstrap_reference.py`` with
CPython 3.12.7, ``PYTHONHASHSEED=0``, and bytecode generation disabled.
"""

from __future__ import annotations

import argparse
from collections import Counter
from copy import deepcopy
import importlib
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import re
import struct
import sys
from types import SimpleNamespace
from typing import Any, Callable


SCHEMA = "dragons.python-reference.epsimple-shape-core.v1"
SOURCE_PATH = "src/epsimple/core/shape.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_BYTES = 22_922
EXPECTED_SOURCE_SHA256 = (
    "sha256:9caa67d424693afc58ee6a456c86d42d504fce4e30e56d73e8ee658dc8e515c1"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:63cfdec0aec079cfc2d2896091974a5c253656e198cbcb1ea328dbace92c1b7e"
)
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_PLATFORM = "win32"
REQUIRED_POINTER_WIDTH_BITS = 64
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

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
        "_dragons_epsimple_shape_support", SUPPORT_PATH
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

EXPECTED_TARGETS = (
    (405, "BlindType", "sha256:6008dd91f9eff70327d99f32f7479d55adb49e8da0024588ae03c736c4baea91"),
    (406, "BlindType.SHADE", "sha256:bb03051d2c3d0af309e74730cf6b9f0487ef3bc734e3bb5d765e89c85f72792b"),
    (407, "BlindType.VENETIAN", "sha256:09c92f4a396529bc46f123f00266199afe7010a8552471ed2d8df7084dc36f8a"),
    (408, "BlindType.__str__", "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e"),
    (409, "Door", "sha256:8c468e24f71eee70841b7374cd605a58ff596c9534f687cf96d848a11456e840"),
    (410, "Door.construction", "sha256:2ca0072cbca39b5232786daac7992b2aed9295ce141409da2a865f020b906755"),
    (411, "Door.from_json", "sha256:26b0f9bbc8e02311b2a024c1c1d4c3fed65546b19a58fe9e74af58c3785e69be"),
    (412, "Door.to_dragon", "sha256:eb81bd06ff88de53694c0aebcc92a0d4fc50041396fb61450b88e7bddafa67b9"),
    (413, "Fenestration", "sha256:43d44ea17615d54a46086dccdae3b68d756ba66dc51043315394c398ce317506"),
    (414, "Fenestration.ID", "sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2"),
    (415, "Fenestration.__deepcopy__", "sha256:a0dbc41130a29593da15c131fbb08fbc1213a8057821d7007fded50d504805f7"),
    (417, "Fenestration.__init__", "sha256:1b22b2f18a540de4aa832db02439453eedced6e7d4b9215568fd8df2e40e520a"),
    (418, "Fenestration.construction", "sha256:0b0cbf2f9f3cdd34c003b74bf9eb18deb9cadf62ebd427dc7c3ccf11030a37eb"),
    (419, "Fenestration.from_json", "sha256:2e553f683898606ece889675669b292f3e8273ada5d5cd7a882d6fe159a1d0ef"),
    (420, "Fenestration.to_dragon", "sha256:ede823e2a01313e006bfae12e1fcd912f22dd7eb65cb7acb424aa0bec9f4b9c3"),
    (421, "GlassDoor", "sha256:1981a40487517cecff870298033d0956d6414d76e20c7ae55703e940379da1dd"),
    (422, "Surface", "sha256:996a596cf10c231190f1a33a734cdbd8dd0f6d642fa67c2d619b663486019069"),
    (423, "Surface.ID", "sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2"),
    (424, "Surface.__deepcopy__", "sha256:0d951ae6777609e1a883c40873574a7a270224fe014783291d3e43db7ebb604f"),
    (426, "Surface.__init__", "sha256:bd742aa0e5cee63e774cb93eb9bb81267178cda8632b06462f3edc3a7621fe68"),
    (429, "Surface.adjacent_zone", "sha256:cf314ac63c9ce1c0c82a8c4e00bc733b383acdb7ce44199ea572e48d8b262912"),
    (430, "Surface.area", "sha256:aa93b96bd36a02c789e649c6beb5f1309eefbfa45fbc91d8318df3474ec06d7a"),
    (431, "Surface.azimuth", "sha256:98e03520fd17b6906c2e09b44672fb393581b59bd4df3dd361a811b5a05d3b7c"),
    (432, "Surface.boundary", "sha256:3680772f6d45c5b6b37b159f985365ec783c5ec20a8a517cb025c3f69fb5e821"),
    (433, "Surface.construction", "sha256:9aed8e7125912f3271f750a7cb03c1114bbc92a2a739e1eb187225684264d2d7"),
    (434, "Surface.flip", "sha256:8e01b8fa930670a91c613f41808e368b165900c73b4caa8a8b4efd91342a1f21"),
    (435, "Surface.from_json", "sha256:3da5f69584c40dd71d27b9f01d6123727c72d7891c962200d15ad8ad6c02c704"),
    (436, "Surface.get_unique_fenestration_constructions", "sha256:72d9807c6c2ca3eb6323e1a184d3f943206f21f2f9ed98745a747a666cf0664f"),
    (437, "Surface.num_doors", "sha256:42d0195ca4d164b19524043b0f8246de8c36046acb91d5e9796d3105dc92985d"),
    (438, "Surface.num_windows", "sha256:4ec64b535f7a5b9bce13317bb5e5ae6367f9bdfb3af3a0414343a9ac54146b23"),
    (439, "Surface.reflectance", "sha256:3a69bea0c6b61870b1cadddc63cd700362e7298c7c2815b85278f523e5b43770"),
    (440, "Surface.to_dragon", "sha256:26abf64e462921ad823eade3b2fbe92d027d63d992122d2d90f27819be7efd2a"),
    (441, "Surface.type", "sha256:5afcce2a27f1772b4a2213957cc8bc19878b1c9de7efc1ad7be96b0352d52b24"),
    (442, "Window", "sha256:00f305afc68a9a36ee4cf733d6cdde693e412b951c9145e327eea2f2198d8689"),
    (443, "Window.__init__", "sha256:e8fad25a400b3c7b8edd138cfccde815de154546fc1f5224aa47f217bc32a441"),
    (444, "Window.blind", "sha256:92ce583d039b9c9f3cf7206db4af5dabfc85e215e9c5ddd9851a8bb7a72b80a4"),
    (445, "Window.construction", "sha256:4f40b518a9390d43ad0a88edfde01ef7a565309a4c5c620d11c66fae11ed6940"),
    (446, "Window.from_json", "sha256:93259bede25daa11c0a6815d246063f1a2740150025245febcc47b2a0e73beb9"),
    (447, "Window.to_dragon", "sha256:f032bad25197edd2e5af14b9ac9b7400ec92a1ed454a669e15ecc6b990237bb3"),
    (448, "Zone", "sha256:dda48f664e889804627559444713cfcd61ced3eaa43dbed0840cba179ea1e313"),
    (449, "Zone.ID", "sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2"),
    (451, "Zone.__init__", "sha256:a5f3cee1e5928625be1d26e36c31e38514256bb1abc369275559c891afb0e361"),
    (452, "Zone.area", "sha256:51ef4a1ee39d91ba10397558ba9b194d9e2a8788b30dcc131f778dae5c1eda8f"),
    (453, "Zone.cooling_supply_systems", "sha256:e0f58a2eca488e9a88b46807bd3ac0b4f946022f8f065b303f37872e469be4fe"),
    (454, "Zone.from_json", "sha256:1254d46e8911bd87019d102dbd222a0a7d7f899540c91584d2ed8165f84a2c0c"),
    (455, "Zone.get_unique_fenestration_constructions", "sha256:d807711017c58e9e6237547dbd4e8549650a4fc629e5b20ddcec2c24bff8db13"),
    (456, "Zone.get_unique_materials", "sha256:ecb20cb3d82efa5c493a99fa5863d3060531cabca284a696fe8e38a922ac2ee9"),
    (457, "Zone.get_unique_surface_constructions", "sha256:486d73d3932d12d15fae4387d79551b240ae97b05108de34422ae5906ee168c9"),
    (458, "Zone.heating_supply_systems", "sha256:c68b3d6503165c08f86fbac458781a28e833f838c4483bd46e838ccf1901f565"),
    (459, "Zone.height", "sha256:349a48c8c6ecbeefebe0b475d63298d72258a5b03f3b8798e2e19c800066b58b"),
    (460, "Zone.infiltration", "sha256:3fffc5a89798f969732ff5e45b8731caa0583e5e5b2cb2b30f37e26673738144"),
    (461, "Zone.supply_systems", "sha256:3eaf6c2588f29f53e73ce5a9773029a0cec51388db59ccccff48c94354e9d547"),
    (462, "Zone.to_dragon", "sha256:da336048a153f041d9d56ff87b6578fa464ce4dd9332aaa97e054d570faf12bd"),
)

EXPECTED_EXCLUDED = (
    (416, "Fenestration.__hash__", "sha256:60007ac6f6ff93642af22c6affa7e2d91ac81564499568cf87b6de5f9e73a0c9"),
    (425, "Surface.__hash__", "sha256:60007ac6f6ff93642af22c6affa7e2d91ac81564499568cf87b6de5f9e73a0c9"),
    (427, "Surface.__repr__", "sha256:ac4399ab740bd268e10398e830e1f0a0ff8fe90879a6d7c1cfe83f2603adf1a0"),
    (428, "Surface.__str__", "sha256:f4142b9674e036ca2a501635b4b0ed750839c7509b0aeb9faa339a2ba57bbaac"),
    (450, "Zone.__hash__", "sha256:60007ac6f6ff93642af22c6affa7e2d91ac81564499568cf87b6de5f9e73a0c9"),
)

TARGET_SYMBOLS = tuple(item[1] for item in EXPECTED_TARGETS)
EXCLUDED_SYMBOLS = tuple(item[1] for item in EXPECTED_EXCLUDED)
TARGET_HASHES = {symbol: value for _, symbol, value in EXPECTED_TARGETS}

EXCEPTION_ADAPTATION_BASES = {
    "BlindType.__str__": "grm-vocabulary-rather-than-native-enum-tostring",
    "Door": "unified-immutable-fenestration-with-door-discriminator",
    "Fenestration": "sealed-discriminated-native-fenestration-rather-than-abc",
    "Fenestration.__deepcopy__": "immutable-native-fenestration-explicit-reconstruction",
    "Fenestration.__init__": "deterministic-native-id-and-discriminated-constructor",
    "Fenestration.construction": "immutable-resolved-native-construction-reference",
    "Fenestration.to_dragon": "aggregate-native-converter-rather-than-abstract-instance-method",
    "GlassDoor": "unified-immutable-fenestration-with-glassdoor-discriminator",
    "Surface.__deepcopy__": "immutable-native-surface-explicit-reconstruction",
    "Surface.__init__": "deterministic-native-id-and-immutable-constructor",
    "Surface.adjacent_zone": "native-adjacent-zone-id-rather-than-object-reference",
    "Surface.flip": "pure-deterministic-native-flip-without-inplace-mutation",
    "Surface.get_unique_fenestration_constructions": "model-catalog-native-aggregation",
    "Window": "unified-immutable-fenestration-with-window-discriminator",
    "Window.__init__": "unified-native-fenestration-constructor",
    "Zone.__init__": "deterministic-native-id-and-immutable-zone-constructor",
    "Zone.get_unique_fenestration_constructions": "model-level-native-fenestration-catalog",
    "Zone.get_unique_materials": "model-level-native-material-catalog",
    "Zone.get_unique_surface_constructions": "model-level-native-surface-catalog",
    "Zone.to_dragon": "native-greenretrofit-converter-implements-upstream-missing-operation",
}
EXCEPTION_ADAPTATIONS = {
    symbol: f"{base}-{TARGET_HASHES[symbol][7:15]}"
    for symbol, base in EXCEPTION_ADAPTATION_BASES.items()
}
CLASSIFICATIONS = {
    symbol: "exception" if symbol in EXCEPTION_ADAPTATIONS else "equivalent"
    for symbol in TARGET_SYMBOLS
}
ASSERTION_IDS = {
    symbol: f"epsimple-shape-core-{index}-{symbol_hash[7:15]}"
    for index, symbol, symbol_hash in EXPECTED_TARGETS
}


def _native_route(symbol: str) -> str:
    if symbol.startswith("BlindType"):
        return "Dragons.SimpleDragon.BlindType with GrmReader.Read(string, SimpleDragonDatabase?) and GrmWriter.Serialize(GreenRetrofitModel, bool)"
    if symbol.startswith(("Door", "Fenestration", "GlassDoor", "Window")):
        if symbol.endswith("from_json"):
            return "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)"
        if symbol.endswith("to_dragon"):
            return "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)"
        if symbol == "Fenestration.ID":
            return "Dragons.SimpleDragon.Fenestration.Id"
        if symbol in {"Door.construction", "Fenestration.construction", "Window.construction"}:
            return "Dragons.SimpleDragon.Fenestration.Construction"
        if symbol == "Window.blind":
            return "Dragons.SimpleDragon.Fenestration.Blind"
        if symbol in {"Fenestration.__deepcopy__", "Fenestration.__init__", "Window.__init__"}:
            return "Dragons.SimpleDragon.Fenestration constructor"
        return "Dragons.SimpleDragon.Fenestration with Dragons.SimpleDragon.FenestrationType"
    if symbol == "Surface.from_json":
        return "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)"
    if symbol == "Surface.to_dragon":
        return "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)"
    if symbol == "Surface.flip":
        return "Dragons.SimpleDragon.Surface.Flip()"
    if symbol == "Surface.get_unique_fenestration_constructions":
        return "Dragons.SimpleDragon.GreenRetrofitModel.FenestrationConstructions"
    if symbol == "Surface.adjacent_zone":
        return "Dragons.SimpleDragon.Surface.AdjacentZoneId"
    if symbol.startswith("Surface"):
        if symbol == "Surface.__init__":
            return "Dragons.SimpleDragon.Surface constructor"
        if symbol == "Surface.__deepcopy__":
            return "Dragons.SimpleDragon.Surface constructor"
        members = {
            "Surface.ID": "Id",
            "Surface.area": "Area",
            "Surface.azimuth": "Azimuth",
            "Surface.boundary": "BoundaryCondition",
            "Surface.construction": "Construction",
            "Surface.num_doors": "DoorCount",
            "Surface.num_windows": "WindowCount",
            "Surface.reflectance": "CoolRoofReflectance",
            "Surface.type": "Type",
        }
        return (
            f"Dragons.SimpleDragon.Surface.{members[symbol]}"
            if symbol in members
            else "Dragons.SimpleDragon.Surface"
        )
    if symbol == "Zone.from_json":
        return "Dragons.SimpleDragon.GrmReader.Read(string, SimpleDragonDatabase?)"
    if symbol == "Zone.to_dragon":
        return "Dragons.SimpleDragon.GreenRetrofitConverter.Convert(GreenRetrofitModel, GreenRetrofitConversionOptions?)"
    if symbol == "Zone.get_unique_fenestration_constructions":
        return "Dragons.SimpleDragon.GreenRetrofitModel.FenestrationConstructions"
    if symbol == "Zone.get_unique_surface_constructions":
        return "Dragons.SimpleDragon.GreenRetrofitModel.SurfaceConstructions"
    if symbol == "Zone.get_unique_materials":
        return "Dragons.SimpleDragon.GreenRetrofitModel.Materials"
    if symbol.startswith("Zone"):
        if symbol == "Zone.__init__":
            return "Dragons.SimpleDragon.Zone constructor"
        members = {
            "Zone.ID": "Id",
            "Zone.area": "Area",
            "Zone.cooling_supply_systems": "CoolingSupplySystems",
            "Zone.heating_supply_systems": "HeatingSupplySystems",
            "Zone.height": "Height",
            "Zone.infiltration": "Infiltration",
            "Zone.supply_systems": "SupplySystems",
        }
        return (
            f"Dragons.SimpleDragon.Zone.{members[symbol]}"
            if symbol in members
            else "Dragons.SimpleDragon.Zone"
        )
    raise RuntimeError(f"No reviewed native route for {symbol}.")


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}

PREFIX = "epsimple-shape-core."
CASE_SPECS = (
    ("B01", "blind-type-values-and-string-semantics", "blind", ("BlindType", "BlindType.SHADE", "BlindType.VENETIAN", "BlindType.__str__"), ()),
    ("D01", "door-validation-json-and-dragon-conversion", "fenestration", ("Door", "Door.construction", "Door.from_json", "Door.to_dragon"), ("Fenestration",)),
    ("F01", "fenestration-abstract-contract", "fenestration", ("Fenestration", "Fenestration.__init__", "Fenestration.construction", "Fenestration.to_dragon"), ()),
    ("F02", "fenestration-id-deepcopy-and-factory-dispatch", "fenestration", ("Fenestration.ID", "Fenestration.__deepcopy__", "Fenestration.from_json"), ("Door", "Window", "GlassDoor")),
    ("G01", "glass-door-window-subtype-and-conversion", "fenestration", ("GlassDoor",), ("Window", "Fenestration")),
    ("S01", "surface-constructor-properties-and-boundary-coupling", "surface", ("Surface", "Surface.ID", "Surface.__init__", "Surface.adjacent_zone", "Surface.area", "Surface.azimuth", "Surface.boundary", "Surface.construction", "Surface.reflectance", "Surface.type"), ("Zone",)),
    ("S02", "surface-deepcopy-and-flip-semantics", "surface", ("Surface.__deepcopy__", "Surface.flip"), ("Surface.type", "Surface.azimuth")),
    ("S03", "surface-json-defined-open-unknown-constructions", "surface", ("Surface.from_json",), ("Surface.construction", "Fenestration.from_json")),
    ("S04", "surface-opening-counts-and-unique-constructions", "surface", ("Surface.get_unique_fenestration_constructions", "Surface.num_doors", "Surface.num_windows"), ("Door", "Window", "GlassDoor")),
    ("S05", "surface-dragon-geometry-and-opening-partition", "surface", ("Surface.to_dragon",), ("Window.to_dragon", "Door.to_dragon")),
    ("W01", "window-constructor-blind-and-construction-validation", "fenestration", ("Window", "Window.__init__", "Window.blind", "Window.construction"), ("BlindType",)),
    ("W02", "window-json-and-dragon-blind-mapping", "fenestration", ("Window.from_json", "Window.to_dragon"), ("BlindType.SHADE", "BlindType.VENETIAN")),
    ("Z01", "zone-constructor-id-height-and-supply-validation", "zone", ("Zone", "Zone.ID", "Zone.__init__", "Zone.height", "Zone.supply_systems"), ()),
    ("Z02", "zone-area-infiltration-and-supply-filtering", "zone", ("Zone.area", "Zone.cooling_supply_systems", "Zone.heating_supply_systems", "Zone.infiltration"), ("Surface.num_windows",)),
    ("Z03", "zone-json-surface-profile-system-and-ventilation-counts", "zone", ("Zone.from_json",), ("Surface.from_json",)),
    ("Z04", "zone-unique-construction-and-material-aggregation", "zone", ("Zone.get_unique_fenestration_constructions", "Zone.get_unique_materials", "Zone.get_unique_surface_constructions"), ("Surface.get_unique_fenestration_constructions",)),
    ("Z05", "zone-to-dragon-upstream-failure", "zone", ("Zone.to_dragon",), ("Zone",)),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 17

# Filled from an independent CPython 3.12.7, hash-seed-zero generation after
# the observation surface is finalized.  Empty values are accepted only while
# bootstrapping this source file and are pinned before the fixture is retained.
EXPECTED_FACT_SHA256 = {
    "epsimple-shape-core.blind-type-values-and-string-semantics": "sha256:15b8de85cd4d9332d48c04334d3636b4c865f2b5cb2d64a6ffc2257a00bb398c",
    "epsimple-shape-core.door-validation-json-and-dragon-conversion": "sha256:31718b07756d58c19a76a2ddd2142050bc5929d2071a31aa52270c5293a2037a",
    "epsimple-shape-core.fenestration-abstract-contract": "sha256:71fdfc17d5d49293c0e9e9ca8f67e93a17bdd1ece347a7843df6070f47aff15c",
    "epsimple-shape-core.fenestration-id-deepcopy-and-factory-dispatch": "sha256:a369c9b79535f2f0e1b244ba2ae07b1531d043634bd02f03a16477de64f315bc",
    "epsimple-shape-core.glass-door-window-subtype-and-conversion": "sha256:32873aa8ca4eadbae3fb8270ea83b8ab5962bd84b2adb8d17e4ffca8033da1f8",
    "epsimple-shape-core.surface-constructor-properties-and-boundary-coupling": "sha256:5d8187491d8aeb46ed3bc6aaa94f0fa0d7e4f150b2675970dc94efc174feec6f",
    "epsimple-shape-core.surface-deepcopy-and-flip-semantics": "sha256:144124e7d1b568ae7feb1a2b0440f7be41276ba2b1d588195646744684f1c0b3",
    "epsimple-shape-core.surface-dragon-geometry-and-opening-partition": "sha256:2ec9a3d6fa5dab7e066fd91144afecd64d319543382b84d3c99b658202333415",
    "epsimple-shape-core.surface-json-defined-open-unknown-constructions": "sha256:00984c4cbe9bf848f7ef23176d6b33fd566caf3894e96a1ac11aa2e2caf2ecba",
    "epsimple-shape-core.surface-opening-counts-and-unique-constructions": "sha256:54585f1a9f1dce957c3029eaeb64c1a63f070a7a2262a3611b2235272369af78",
    "epsimple-shape-core.window-constructor-blind-and-construction-validation": "sha256:e8d9eae97ecb3028407c3e44f341db74fb1f6aafef4d3ae7e6ee4fb87436c620",
    "epsimple-shape-core.window-json-and-dragon-blind-mapping": "sha256:35de73b349ee6459875511cc87b8bda256ec43faaec8e75565be856efa27704a",
    "epsimple-shape-core.zone-area-infiltration-and-supply-filtering": "sha256:b6792937ff3ff7fe7267f5fa5718f486870213b03363346246a5059a6d36d8b4",
    "epsimple-shape-core.zone-constructor-id-height-and-supply-validation": "sha256:56416046eb966ea8ef36472c1afd7a8e91ca13dbfe3802937b067e6542f1e8d5",
    "epsimple-shape-core.zone-json-surface-profile-system-and-ventilation-counts": "sha256:6e157cb3d0f65744730c4b504762681877f2b3e415fee1efb40e0f9aa3102f23",
    "epsimple-shape-core.zone-to-dragon-upstream-failure": "sha256:493d789c6bc273ada4b17f0a776084c450311cf7a9ef5fd0bc54eb3d5cb1a778",
    "epsimple-shape-core.zone-unique-construction-and-material-aggregation": "sha256:5d888077c59733555f0eca8413bd45a7732f608f233b543791b762c0b957b731",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-shape-core.blind-type-values-and-string-semantics": "sha256:6a8ac8400beb6c4122c6fa6a1fc9908674c499f5adb571201a19d5417cc806e6",
    "epsimple-shape-core.door-validation-json-and-dragon-conversion": "sha256:6b0994673eeaac9a47bdbfde027edebb65c7b4909385bb71b4e9520f621eaa94",
    "epsimple-shape-core.fenestration-abstract-contract": "sha256:f467490337b86f2d9a5c655a16969520e69f39c02a2b851336c05ed447140126",
    "epsimple-shape-core.fenestration-id-deepcopy-and-factory-dispatch": "sha256:20f684ccb9cd4ab8ea7479779199f89be239308e8176f3809bd82e80e1cf3140",
    "epsimple-shape-core.glass-door-window-subtype-and-conversion": "sha256:861a3fd0e8cd792dc7e833a96af580b29bf8cb0fe7d23320f34f67fe4a856269",
    "epsimple-shape-core.surface-constructor-properties-and-boundary-coupling": "sha256:ceb53ccba0964b9b75a9128fe407c44cf4be7b18cf582d42293a656f5e1c99fe",
    "epsimple-shape-core.surface-deepcopy-and-flip-semantics": "sha256:4f9dadc6bf62c5c2bfc210a20e5aa9ec5cd18dcc7319baf7dad98bd5ccdd7ea4",
    "epsimple-shape-core.surface-dragon-geometry-and-opening-partition": "sha256:4cc1736d5fdfebdbaef75032688585c656c8745feddc42bc15c9c232fe2fb337",
    "epsimple-shape-core.surface-json-defined-open-unknown-constructions": "sha256:4035c1295cc3847d12ed743a40907129032105320affbf26121d3bda8150707b",
    "epsimple-shape-core.surface-opening-counts-and-unique-constructions": "sha256:4bde8e1a5cf1ee711bf2de689fb8ff41b394ea44f556a788d4fc1950c55fbec2",
    "epsimple-shape-core.window-constructor-blind-and-construction-validation": "sha256:1eb22483d6cf26f7e591a0a04261894d4276d98c499df475d5ea23322b22f8ca",
    "epsimple-shape-core.window-json-and-dragon-blind-mapping": "sha256:6c9d395fd92f8c1308f7c3790e4eebae0b5a662c49f9ab4c318ebb4d56867d1e",
    "epsimple-shape-core.zone-area-infiltration-and-supply-filtering": "sha256:5328b9ee32e0a71252006f2c2285800f8c1d2677dd80c899b3baa92f7b7d66ee",
    "epsimple-shape-core.zone-constructor-id-height-and-supply-validation": "sha256:1b779e39a56312ee23d131296b4e73cc0f62512ba062d7287ad179fc8f2a4df9",
    "epsimple-shape-core.zone-json-surface-profile-system-and-ventilation-counts": "sha256:1c6cfbb7efaf0115b9e781d13bbbbc5d9525256045616474972df37ae1763665",
    "epsimple-shape-core.zone-to-dragon-upstream-failure": "sha256:817afda425049cf2992edaef2c9a9fff00352e80a619c8ee9aed2a96b76e28a1",
    "epsimple-shape-core.zone-unique-construction-and-material-aggregation": "sha256:e1fe208bc5a494d8d91418e6943a80aaae62c6b19e4c106d0beb3b6e42a1f219",
}
EXPECTED_CASES_SHA256 = "sha256:1b6be41823b3a165d1e5c923f46278a44ae8ff68ccef1a0edd08d72ab637398e"
EXPECTED_TARGET_RECEIPTS_SHA256 = "sha256:c9f3c076692688e309cfa4a890a17409b7f9b245590b21e844e41188493cbee3"
EXPECTED_EXCLUDED_RECEIPTS_SHA256 = "sha256:09eab277cdb483b9b266dee756aa8908cb1a83bf9bcecd29488c4803d88f4367"

EXPECTED_LOADED_SOURCES = (
    ("epsimple", "src/epsimple/__init__.py", 2262, "sha256:adff45de2c37d23586de00015e05502b1ee4ff7c167f5017b946827f1f383996", "sha256:f26684ea4b6e1bfe7b9576f06d45971e420d2e9e49da99b6c52ff9d0d424fae3"),
    ("epsimple.api", "src/epsimple/api.py", 11149, "sha256:fb34501b221a34279dc4e408af04cd103448b71b563780e6eda9ca5ffdae8e10", "sha256:7910d7d9e0383b319b9374bdcdf5f8c069930c609fa8e9e6bd8c50a59f4858e5"),
    ("epsimple.constants", "src/epsimple/constants.py", 4873, "sha256:d5dd5241ec90b14ba3708a525cd74279a8cdc238164a5b8544c4c82b05a29897", "sha256:6740f081f087834aadfef0c11da6cdbe11f907dc170b48ebaa287e000eb6e27b"),
    ("epsimple.core", "src/epsimple/core/__init__.py", 1264, "sha256:e6d571f7bf775cd13cba7abf0f625802e92680fdc87838f4039b037174a1a4c9", "sha256:7409c8f18e040e02d52a7e2a70ab56b4539212eb76035f5b218a5bacd84f4762"),
    ("epsimple.core.construction", "src/epsimple/core/construction.py", 25902, "sha256:50b784d9c7ebd0df34fb6e524585482f04eb90ef915d5afd125fe779c0620816", "sha256:fe40c8c89f2c3341ce4972976eabf96edd85ccba55a3a7619ca17e0a7603c0ab"),
    ("epsimple.core.hvac", "src/epsimple/core/hvac.py", 53850, "sha256:9f3ecb27ed612baeed530ccbfd5857f1f528de24f222e6ef5093e4a635665d9c", "sha256:dbbea63f51a001fae4fd73fba96dc099eab8cd5bcec39e3d9bf768e29b463873"),
    ("epsimple.core.model", "src/epsimple/core/model.py", 36949, "sha256:71dc9bb8d97e829c27d9b5d19ef88709af9613f9e53f60807d54ceb2922e4532", "sha256:f79918272c07515ee4ae98fa62f4ca5d5d703e5e2faa334f72d6a6966e1e2447"),
    ("epsimple.core.profile", "src/epsimple/core/profile.py", 18964, "sha256:e43f07d41e1e90cb9dcb7207fce67d8a6cb93acf54242b7a87c0aa30dda1309c", "sha256:bf39751f2e76642e1dbad2c4196f23bcdcdeefaad28e750e5c28c616ea9434ed"),
    ("epsimple.core.shape", SOURCE_PATH, EXPECTED_SOURCE_BYTES, EXPECTED_SOURCE_SHA256, EXPECTED_SOURCE_AST_SHA256),
    ("epsimple.debug", "src/epsimple/debug.py", 29858, "sha256:7fa33eea0c10970c770572e4206dc9d195dc90579a3ded33a53ce4c2b011db6e", "sha256:0cc9cc2ed116f8923f44277a62cc13371e5f687198276c54455c4198be0d84ad"),
    ("epsimple.utils", "src/epsimple/utils.py", 28288, "sha256:4b19874951feb696f0a5f1b42d85a11c405e5f83958828997af9a977a6aa9cf8", "sha256:d7757fa8a1fc22604c82479200d8af4338d9283d090bc764b2ed526ee9142135"),
    ("idragon", "src/idragon/__init__.py", 2011, "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618"),
    ("idragon.common", "src/idragon/common.py", 6247, "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9"),
    ("idragon.constants", "src/idragon/constants.py", 2590, "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"),
    ("idragon.dragon", "src/idragon/dragon/__init__.py", 1505, "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52"),
    ("idragon.dragon.construction", "src/idragon/dragon/construction.py", 11652, "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a"),
    ("idragon.dragon.hvac", "src/idragon/dragon/hvac.py", 137833, "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0", "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"),
    ("idragon.dragon.model", "src/idragon/dragon/model.py", 8247, "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"),
    ("idragon.dragon.profile", "src/idragon/dragon/profile.py", 117731, "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef"),
    ("idragon.dragon.shape", "src/idragon/dragon/shape.py", 27438, "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2"),
    ("idragon.imugi", "src/idragon/imugi.py", 91815, "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"),
    ("idragon.launcher", "src/idragon/launcher.py", 12367, "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e"),
    ("idragon.utils", "src/idragon/utils.py", 2616, "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452"),
)

EXPECTED_DEPENDENCIES = {
    "eppy": "0.5.63",
    "numpy": "2.3.1",
    "pandas": "2.3.0",
    "python-dateutil": "2.9.0.post0",
    "pytz": "2024.2",
    "six": "1.16.0",
    "tzdata": "2024.2",
}

RAW_ADDRESS_PATTERN = re.compile(r"0x[0-9a-fA-F]{6,}")
WINDOWS_ABSOLUTE_PATH_PATTERN = re.compile(r"(?i)(?:[A-Z]:\\|[A-Z]:/)")
GUID_PATTERN = re.compile(r"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b")
TIMESTAMP_PATTERN = re.compile(r"\b\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    if path.stat().st_size != EXPECTED_INVENTORY_BYTES:
        raise SystemExit("The public-symbol inventory byte length is not pinned.")
    if sha256_file(path) != EXPECTED_INVENTORY_FILE_SHA256:
        raise SystemExit("The public-symbol inventory file hash is not pinned.")
    value = load_json_without_duplicates(path)
    if value.get("upstream_commit") != upstream_commit:
        raise SystemExit("The inventory upstream commit does not match the request.")
    if value.get("content_sha256") != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The inventory content hash drifted.")
    source_file = next(
        (item for item in value.get("files", []) if item.get("path") == SOURCE_PATH),
        None,
    )
    if source_file != {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }:
        raise SystemExit("The EPlusSimple shape source receipt drifted.")
    symbols = value.get("symbols")
    if not isinstance(symbols, list):
        raise SystemExit("The inventory symbol list is missing.")

    def collect(specs: tuple[tuple[int, str, str], ...], label: str) -> list[dict[str, Any]]:
        result = []
        for index, symbol, symbol_hash in specs:
            observed = symbols[index]
            if (
                observed.get("path") != SOURCE_PATH
                or observed.get("symbol") != symbol
                or observed.get("symbol_hash") != symbol_hash
            ):
                raise SystemExit(f"{label} inventory receipt drifted at index {index}.")
            result.append({"inventory_index": index, **observed})
        return result

    target_receipts = collect(EXPECTED_TARGETS, "Target")
    excluded_receipts = collect(EXPECTED_EXCLUDED, "Excluded")
    if EXPECTED_TARGET_RECEIPTS_SHA256 and canonical_sha256(target_receipts) != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise SystemExit("Pinned target receipt collection drifted.")
    if EXPECTED_EXCLUDED_RECEIPTS_SHA256 and canonical_sha256(excluded_receipts) != EXPECTED_EXCLUDED_RECEIPTS_SHA256:
        raise SystemExit("Pinned excluded receipt collection drifted.")
    return {
        "content_sha256": value["content_sha256"],
        "excluded_receipts": excluded_receipts,
        "file": source_file,
        "symbols": [_descriptor(item) for item in target_receipts],
        "target_receipts": target_receipts,
    }


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
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Shape oracle cases must partition every target exactly once.")
    permitted = set(TARGET_SYMBOLS)
    if any(not set(item["context_symbols"]).issubset(permitted) for item in definitions):
        raise RuntimeError("Shape oracle context escaped the bounded target set.")
    if set(EXCLUDED_SYMBOLS).intersection(
        symbol
        for definition in definitions
        for symbol in (*definition["target_symbols"], *definition["context_symbols"])
    ):
        raise RuntimeError("An explicitly excluded symbol entered a shape case.")
    return definitions


def _find_pinned_source() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        candidate = Path(entry) / "epsimple" / "core" / "shape.py"
        if (
            candidate.is_file()
            and candidate.stat().st_size == EXPECTED_SOURCE_BYTES
            and sha256_file(candidate) == EXPECTED_SOURCE_SHA256
        ):
            matches.append(candidate.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned epsimple/core/shape.py must be importable.")
    return unique[0]


def _import_modules(source: Path) -> SimpleNamespace:
    shape = importlib.import_module("epsimple.core.shape")
    if Path(shape.__file__).resolve() != source.resolve():
        raise SystemExit("Imported epsimple.core.shape did not come from the pinned source.")
    construction = importlib.import_module("epsimple.core.construction")
    hvac = importlib.import_module("epsimple.core.hvac")
    profile = importlib.import_module("epsimple.core.profile")
    dragon = importlib.import_module("idragon.dragon")
    return SimpleNamespace(
        construction=construction,
        dragon=dragon,
        hvac=hvac,
        profile=profile,
        shape=shape,
    )


def _loaded_source_receipts(source: Path) -> list[dict[str, Any]]:
    source_root = source.parents[2]
    observed: list[dict[str, Any]] = []
    for module_name, relative_path, byte_count, file_hash, ast_hash in EXPECTED_LOADED_SOURCES:
        module = sys.modules.get(module_name)
        if module is None or not getattr(module, "__file__", None):
            raise SystemExit(f"Pinned local module was not loaded: {module_name}")
        module_path = Path(module.__file__).resolve()
        expected_path = source_root / relative_path.removeprefix("src/")
        if module_path != expected_path.resolve():
            raise SystemExit(f"Loaded local module path drifted: {module_name}")
        if module_path.stat().st_size != byte_count or sha256_file(module_path) != file_hash:
            raise SystemExit(f"Loaded local module bytes drifted: {module_name}")
        observed.append(
            {
                "ast_sha256": ast_hash,
                "bytes": byte_count,
                "module": module_name,
                "path": relative_path,
                "source_sha256": file_hash,
            }
        )
    local_names: set[str] = set()
    for module_name, module in sys.modules.items():
        module_path_text = getattr(module, "__file__", None)
        if not module_path_text:
            continue
        try:
            Path(module_path_text).resolve().relative_to(source_root)
        except ValueError:
            continue
        local_names.add(module_name)
    if local_names != {item[0] for item in EXPECTED_LOADED_SOURCES}:
        raise SystemExit("The exact loaded upstream module closure drifted.")
    return observed


def _number(value: int | float | None) -> Any:
    if value is None or isinstance(value, int):
        return value
    if not isinstance(value, float) or not math.isfinite(value):
        raise RuntimeError("Only finite oracle numbers are supported.")
    return {"decimal": format(value, ".17g"), "hex": value.hex(), "kind": "float"}


def _error(call: Callable[[], Any]) -> dict[str, Any]:
    try:
        call()
    except Exception as exception:  # observation is intentionally broad
        return {
            "error": {
                "message": str(exception),
                "type": type(exception).__name__,
            },
            "outcome": "raised",
        }
    return {"outcome": "returned"}


def _normalized_auto_id(value: str, prefix: str) -> str:
    if not re.fullmatch(re.escape(prefix) + r"AUTOID0x[0-9a-f]+", value):
        raise RuntimeError(f"Automatic ID family drifted for prefix {prefix}.")
    return prefix + "AUTOID<address>"


def _make_graph(modules: SimpleNamespace) -> dict[str, Any]:
    c = modules.construction
    h = modules.hvac
    s = modules.shape

    material_a = c.Material("brick", 0.5, 1000, 800, ID="MAT-A")
    material_b = c.Material("insulation", 0.04, 30, 1000, ID="MAT-B")
    surface_a = c.SurfaceConstruction(
        "wall", material_a, 0.1, material_b, 0.05, ID="SC-A"
    )
    surface_b = c.SurfaceConstruction("floor", material_b, 0.2, ID="SC-B")
    glass = c.FenestrationConstruction("glass", 1.5, 0.5, ID="FC-G")
    opaque = c.FenestrationConstruction("door", 2.0, None, ID="FC-D")
    window = s.Window("window", 2.0, glass, blind="shade", ID="WIN-A")
    door = s.Door("door", 1.0, opaque, ID="DOOR-A")
    glass_door = s.GlassDoor(
        "glassdoor", 1.5, glass, blind="venetian", ID="GLASSDOOR-A"
    )
    wall = s.Surface(
        "wall", "wall", "outdoors", 20, 90, surface_a,
        [window, door, glass_door], ID="SURF-WALL"
    )
    floor = s.Surface(
        "floor", "floor", "ground", 40, None, surface_b, [], ID="SURF-FLOOR"
    )
    roof = s.Surface(
        "roof", "ceiling", "outdoors", 40, None, surface_b, [],
        reflectance=0.7, ID="SURF-ROOF"
    )

    class OracleSupplySystem(h.SupplySystem):
        def __init__(self, identifier: str, heatable: bool, coolable: bool) -> None:
            self.ID = identifier
            self._oracle_heatable = heatable
            self._oracle_coolable = coolable

        @property
        def heatable(self) -> bool:
            return self._oracle_heatable

        @property
        def coolable(self) -> bool:
            return self._oracle_coolable

    heating = OracleSupplySystem("SYS-HEAT", True, False)
    cooling = OracleSupplySystem("SYS-COOL", False, True)
    both = OracleSupplySystem("SYS-BOTH", True, True)
    zone = s.Zone(
        "zone", 3, [floor, wall, roof], None, 8,
        [heating, cooling, both], [], floor=2, ID="ZONE-A"
    )
    return {
        "both": both,
        "cooling": cooling,
        "door": door,
        "floor": floor,
        "glass": glass,
        "glass_door": glass_door,
        "heating": heating,
        "material_a": material_a,
        "material_b": material_b,
        "opaque": opaque,
        "roof": roof,
        "surface_a": surface_a,
        "surface_b": surface_b,
        "wall": wall,
        "window": window,
        "zone": zone,
    }


def _opening_state(value: Any) -> dict[str, Any]:
    return {
        "area": _number(value.area),
        "blind": getattr(value, "blind", None),
        "construction_id": value.construction.ID,
        "id": value.ID,
        "name": value.name,
        "type": type(value).__name__,
    }


def _execute_case(code: str, modules: SimpleNamespace) -> dict[str, Any]:
    c = modules.construction
    d = modules.dragon
    h = modules.hvac
    p = modules.profile
    s = modules.shape
    graph = _make_graph(modules)

    if code == "B01":
        return {
            "construct_by_value": [
                {"input": value, "member": s.BlindType(value).name}
                for value in ("shade", "venetian")
            ],
            "invalid": _error(lambda: s.BlindType("roller")),
            "members": [
                {
                    "is_str": isinstance(member, str),
                    "name": member.name,
                    "string": str(member),
                    "value": member.value,
                }
                for member in s.BlindType
            ],
        }

    if code == "D01":
        parsed = s.Door.from_json(
            SimpleNamespace(name="parsed-door", area=1.25, construction_id="FC-D", id="DOOR-J"),
            {"FC-D": graph["opaque"]},
        )
        dragonized = graph["door"].to_dragon({"FC-D": graph["opaque"].to_dragon()})
        return {
            "class_topology": {
                "is_fenestration_subclass": issubclass(s.Door, s.Fenestration),
                "is_abstract": inspect.isabstract(s.Door),
            },
            "construction_validation": {
                "opaque": _opening_state(graph["door"]),
                "transparent_error": _error(
                    lambda: s.Door("bad", 1, graph["glass"], ID="DOOR-BAD")
                ),
            },
            "from_json": _opening_state(parsed),
            "to_dragon": {
                "area": _number(dragonized.area),
                "construction_name": dragonized.construction.name,
                "construction_type": type(dragonized.construction).__name__,
                "name": dragonized.name,
                "type": type(dragonized).__name__,
            },
        }

    if code == "F01":
        construction_descriptor = s.Fenestration.__dict__["construction"]
        return {
            "abstract_methods": sorted(s.Fenestration.__abstractmethods__),
            "construction_descriptor": {
                "getter_abstract": bool(getattr(construction_descriptor.fget, "__isabstractmethod__", False)),
                "setter_abstract": bool(getattr(construction_descriptor.fset, "__isabstractmethod__", False)),
            },
            "direct_instantiation": _error(
                lambda: s.Fenestration("abstract", 1, graph["glass"], ID="FNST-ABSTRACT")
            ),
            "is_abstract": inspect.isabstract(s.Fenestration),
            "to_dragon_abstract": bool(
                getattr(s.Fenestration.__dict__["to_dragon"], "__isabstractmethod__", False)
            ),
        }

    if code == "F02":
        auto = s.Window("automatic", 1, graph["glass"])
        inputs = (
            SimpleNamespace(type="window", name="w", area=1, construction_id="FC-G", blind="shade", id="W-J"),
            SimpleNamespace(type="door", name="d", area=1, construction_id="FC-D", id="D-J"),
            SimpleNamespace(type="glassdoor", name="g", area=1, construction_id="FC-G", blind="venetian", id="G-J"),
        )
        parsed = [
            s.Fenestration.from_json(item, {"FC-G": graph["glass"], "FC-D": graph["opaque"]})
            for item in inputs
        ]
        copies = [deepcopy(item) for item in (graph["window"], graph["door"], graph["glass_door"])]
        return {
            "automatic_id": _normalized_auto_id(auto.ID, "FNST-"),
            "explicit_id": graph["window"].ID,
            "factory_dispatch": [_opening_state(item) for item in parsed],
            "factory_unknown_type": _error(
                lambda: s.Fenestration.from_json(SimpleNamespace(type="skylight"), {})
            ),
            "deepcopies": [
                {
                    "construction_shared": copied.construction is original.construction,
                    "copy": _opening_state(copied),
                    "original": _opening_state(original),
                    "same_object": copied is original,
                }
                for original, copied in zip(
                    (graph["window"], graph["door"], graph["glass_door"]), copies
                )
            ],
        }

    if code == "G01":
        value = graph["glass_door"]
        converted = value.to_dragon({"FC-G": graph["glass"].to_dragon()})
        return {
            "state": _opening_state(value),
            "subclass_of_fenestration": isinstance(value, s.Fenestration),
            "subclass_of_window": isinstance(value, s.Window),
            "to_dragon": {
                "blind_type": type(converted.blind).__name__,
                "glazing_name": converted.glazing.name,
                "output_type": type(converted).__name__,
            },
        }

    if code == "S01":
        auto = s.Surface("auto", "floor", "ground", 1, None, graph["surface_b"], [])
        adjacent = s.Zone("adjacent", 3, [], None, 0, ID="ZONE-ADJ")
        coupled = s.Surface("coupled", "floor", "ground", 2, None, graph["surface_b"], [], ID="SURF-COUPLED")
        coupled.adjacent_zone = adjacent
        after_assignment = {
            "adjacent_id": coupled.adjacent_zone.ID,
            "boundary": coupled.boundary,
        }
        coupled.boundary = "adiabatic"
        after_boundary_reset = {
            "adjacent_is_none": coupled.adjacent_zone is None,
            "boundary": coupled.boundary,
        }
        mutated = s.Surface("mutable", "wall", "outdoors", 10, 15, graph["surface_a"], [], ID="SURF-MUT")
        mutated.area = 12
        mutated.reflectance = 0.6
        mutated.construction = c.UnknownConstruction()
        mutated.type = "ceiling"
        return {
            "automatic_id": _normalized_auto_id(auto.ID, "SURF-"),
            "boundary_coupling": {
                "after_adjacent_assignment": after_assignment,
                "after_boundary_reset": after_boundary_reset,
                "invalid_adjacent": _error(lambda: setattr(coupled, "adjacent_zone", "ZONE-TEXT")),
            },
            "mutated_state": {
                "area": _number(mutated.area),
                "azimuth": _number(mutated.azimuth),
                "construction_id": mutated.construction.ID,
                "reflectance": _number(mutated.reflectance),
                "type": mutated.type,
            },
            "states": [
                {
                    "area": _number(item.area),
                    "azimuth": _number(item.azimuth),
                    "boundary": item.boundary,
                    "construction_id": item.construction.ID,
                    "id": item.ID,
                    "reflectance": _number(item.reflectance),
                    "type": item.type,
                }
                for item in (graph["wall"], graph["floor"], graph["roof"])
            ],
            "validation": {
                "area_zero": _error(lambda: setattr(graph["floor"], "area", 0)),
                "floor_azimuth": _error(lambda: s.Surface("bad", "floor", "ground", 1, 10, graph["surface_b"], [], ID="S-BAD-AZ")),
                "ground_opening": _error(lambda: s.Surface("bad", "floor", "ground", 1, None, graph["surface_b"], [graph["window"]], ID="S-BAD-OPEN")),
                "invalid_construction": _error(lambda: setattr(graph["floor"], "construction", object())),
                "reflectance_zero": _error(lambda: setattr(graph["roof"], "reflectance", 0)),
                "wall_missing_azimuth": _error(lambda: s.Surface("bad", "wall", "outdoors", 1, None, graph["surface_a"], [], ID="S-BAD-WALL")),
            },
        }

    if code == "S02":
        sources = (graph["wall"], graph["floor"], graph["roof"])
        copies = [deepcopy(item) for item in sources]
        flips = [item.flip() for item in sources]
        inplace = s.Surface("inplace", "wall", "outdoors", 10, 45, graph["surface_a"], [], ID="SURF-INPLACE")
        returned = inplace.flip(inplace=True)
        return {
            "deepcopies": [
                {
                    "copy_azimuth": _number(copied.azimuth),
                    "copy_id": copied.ID,
                    "copy_name": copied.name,
                    "copy_type": copied.type,
                    "same_object": copied is original,
                }
                for original, copied in zip(sources, copies)
            ],
            "flips": [
                {
                    "azimuth": _number(value.azimuth),
                    "id": value.ID,
                    "name": value.name,
                    "same_object": value is original,
                    "type": value.type,
                }
                for original, value in zip(sources, flips)
            ],
            "inplace": {
                "azimuth": _number(inplace.azimuth),
                "returned_none": returned is None,
                "type": inplace.type,
            },
        }

    if code == "S03":
        base = {
            "area": 10,
            "azimuth": None,
            "boundary_condition": "ground",
            "fenestrations": [],
            "type": "floor",
        }
        results = []
        for label, construction_id in (("open", "open"), ("unknown", None), ("defined", "SC-B")):
            item = s.Surface.from_json(
                SimpleNamespace(**base, construction_id=construction_id, id="S-" + label, name=label),
                {"SC-B": graph["surface_b"]},
                {"FC-G": graph["glass"], "FC-D": graph["opaque"]},
            )
            results.append(
                {
                    "construction_id": item.construction.ID,
                    "construction_type": type(item.construction).__name__,
                    "id": item.ID,
                    "label": label,
                }
            )
        wall_input = SimpleNamespace(
            area=20,
            azimuth=180,
            boundary_condition="outdoors",
            construction_id="SC-A",
            coolroof_reflectance=None,
            fenestrations=[
                SimpleNamespace(type="window", name="parsed-window", area=2, construction_id="FC-G", blind="shade", id="PW"),
                SimpleNamespace(type="door", name="parsed-door", area=1, construction_id="FC-D", id="PD"),
            ],
            id="S-WALL-J",
            name="json-wall",
            type="wall",
        )
        parsed_wall = s.Surface.from_json(
            wall_input,
            {"SC-A": graph["surface_a"]},
            {"FC-G": graph["glass"], "FC-D": graph["opaque"]},
        )
        return {
            "construction_branches": results,
            "defined_wall": {
                "azimuth": _number(parsed_wall.azimuth),
                "fenestrations": [_opening_state(item) for item in parsed_wall.fenestrations],
                "id": parsed_wall.ID,
            },
        }

    if code == "S04":
        duplicate = c.FenestrationConstruction("replacement", 1.2, 0.4, ID="FC-G")
        surface = s.Surface(
            "aggregate", "wall", "outdoors", 20, 0, graph["surface_a"],
            [graph["window"], graph["door"], s.Window("replacement", 1, duplicate, ID="W-REPLACE"), graph["glass_door"]],
            ID="SURF-AGG",
        )
        unique = surface.get_unique_fenestration_constructions()
        return {
            "door_count": surface.num_doors,
            "fenestration_count": len(surface.fenestrations),
            "glassdoor_counts_as_window": surface.num_windows == 3,
            "unique_construction_keys": list(unique),
            "unique_fc_g_selected_name": unique["FC-G"].name,
            "window_count": surface.num_windows,
        }

    if code == "S05":
        surface_constructions = {
            "SC-A": graph["surface_a"].to_dragon(),
            "SC-B": graph["surface_b"].to_dragon(),
        }
        fenestration_constructions = {
            "FC-G": graph["glass"].to_dragon(),
            "FC-D": graph["opaque"].to_dragon(),
        }
        converted = graph["wall"].to_dragon(4, surface_constructions, fenestration_constructions)
        return {
            "area_partition": {
                "door_areas": [_number(item.area) for item in converted.door],
                "window_areas": [_number(item.area) for item in converted.window],
            },
            "construction_name": converted.construction.name,
            "door_names": [item.name for item in converted.door],
            "name": converted.name,
            "output_type": type(converted).__name__,
            "surface_boundary": str(converted.boundary),
            "surface_type": str(converted.type),
            "vertices": [
                {"x": _number(vertex.x), "y": _number(vertex.y), "z": _number(vertex.z)}
                for vertex in converted.vertex
            ],
            "window_blind_types": [
                None if item.blind is None else type(item.blind).__name__
                for item in converted.window
            ],
            "window_names": [item.name for item in converted.window],
        }

    if code == "W01":
        mutable = s.Window("mutable", 1, graph["glass"], ID="W-MUT")
        transitions = []
        for value in ("shade", "venetian", None):
            mutable.blind = value
            transitions.append(mutable.blind)
        mutable.construction = graph["glass"]
        auto = s.Window("automatic", 1, graph["glass"])
        return {
            "automatic_id": _normalized_auto_id(auto.ID, "FNST-"),
            "blind_transitions": transitions,
            "class_topology": {
                "is_abstract": inspect.isabstract(s.Window),
                "is_fenestration_subclass": issubclass(s.Window, s.Fenestration),
            },
            "explicit": _opening_state(graph["window"]),
            "validation": {
                "invalid_blind": _error(lambda: setattr(mutable, "blind", "roller")),
                "opaque_construction": _error(lambda: setattr(mutable, "construction", graph["opaque"])),
            },
        }

    if code == "W02":
        parsed = s.Window.from_json(
            SimpleNamespace(name="parsed", area=1.75, construction_id="FC-G", blind="venetian", id="W-J"),
            {"FC-G": graph["glass"]},
        )
        mappings = []
        for index, blind in enumerate((None, "shade", "venetian")):
            value = s.Window("mapped", 1, graph["glass"], blind=blind, ID=f"W-MAP-{index}")
            converted = value.to_dragon({"FC-G": graph["glass"].to_dragon()})
            mappings.append(
                {
                    "input": blind,
                    "output_blind_type": None if converted.blind is None else type(converted.blind).__name__,
                    "output_glazing_name": converted.glazing.name,
                    "output_name": converted.name,
                }
            )
        return {"from_json": _opening_state(parsed), "to_dragon_mappings": mappings}

    if code == "Z01":
        auto = s.Zone("automatic", 3, [], None, 0)
        supplied = [graph["heating"]]
        copied = s.Zone("copied-list", 3, [], None, 0, supplied, ID="ZONE-COPY")
        supplied.append(graph["cooling"])
        return {
            "automatic_id": _normalized_auto_id(auto.ID, "ZONE-"),
            "defensive_supply_copy": [item.ID for item in copied.supply_systems],
            "explicit": {
                "height": _number(graph["zone"].height),
                "id": graph["zone"].ID,
                "supply_ids": [item.ID for item in graph["zone"].supply_systems],
            },
            "validation": {
                "duplicate_id": _error(lambda: s.Zone("bad", 3, [], None, 0, [graph["heating"], graph["heating"]], ID="Z-DUP")),
                "height_zero": _error(lambda: s.Zone("bad", 0, [], None, 0, ID="Z-HEIGHT")),
                "non_list": _error(lambda: s.Zone("bad", 3, [], None, 0, (), ID="Z-TUPLE")),
                "non_supply_item": _error(lambda: s.Zone("bad", 3, [], None, 0, [object()], ID="Z-ITEM")),
                "two_radiant_floors": _error(lambda: s.Zone("bad", 3, [], None, 0, [h.ElectricRadiantFloor("r1", ID="R-1"), h.ElectricRadiantFloor("r2", ID="R-2")], ID="Z-RAD")),
            },
        }

    if code == "Z02":
        door_surface = s.Surface("door-wall", "wall", "outdoors", 10, 0, graph["surface_a"], [graph["door"]], ID="S-DOOR")
        glassdoor_surface = s.Surface("glass-wall", "wall", "outdoors", 10, 0, graph["surface_a"], [graph["glass_door"]], ID="S-GLASS")
        cases = [
            s.Zone("none", 3, [graph["floor"]], None, 0, ID="Z-NONE"),
            s.Zone("door", 3, [graph["floor"], door_surface], None, 0, ID="Z-DOOR"),
            s.Zone("glassdoor", 3, [graph["floor"], glassdoor_surface], None, 0, ID="Z-GLASS"),
            graph["zone"],
        ]
        return {
            "area": _number(graph["zone"].area),
            "cooling_ids": [item.ID for item in graph["zone"].cooling_supply_systems],
            "heating_ids": [item.ID for item in graph["zone"].heating_supply_systems],
            "infiltration_cases": [
                {"id": item.ID, "value": _number(item.infiltration)} for item in cases
            ],
        }

    if code == "Z03":
        profile_key = "__EPSIMPLE_SHAPE_ORACLE_PROFILE__"
        had_profile = profile_key in p.Profile._DB
        old_profile = p.Profile._DB.get(profile_key)
        profile = SimpleNamespace(name="oracle-profile")
        p.Profile._DB[profile_key] = profile
        try:
            ventilation = h.VentilationSystem("vent", 1, ID="VENT-A")
            input_value = SimpleNamespace(
                height=3,
                id="ZONE-JSON",
                light_density=7,
                name="json-zone",
                profile=profile_key,
                supply_system_ids=["SYS-HEAT"],
                surfaces=[
                    SimpleNamespace(
                        area=12,
                        boundary_condition="ground",
                        construction_id="SC-B",
                        fenestrations=[],
                        id="SURF-JSON",
                        name="json-floor",
                        type="floor",
                    )
                ],
                ventilation_systems=[SimpleNamespace(count=3, id="VENT-A")],
            )
            value = s.Zone.from_json(
                input_value,
                {"SC-B": graph["surface_b"]},
                {"FC-G": graph["glass"]},
                {"SYS-HEAT": graph["heating"]},
                {"VENT-A": ventilation},
            )
            facts = {
                "id": value.ID,
                "profile_identity_preserved": value.profile is profile,
                "surface_ids": [item.ID for item in value.surface],
                "supply_ids": [item.ID for item in value.supply_systems],
                "ventilation_alias_identity": all(item is ventilation for item in value.ventilation_systems),
                "ventilation_ids": [item.ID for item in value.ventilation_systems],
            }
        finally:
            if had_profile:
                p.Profile._DB[profile_key] = old_profile
            else:
                del p.Profile._DB[profile_key]
        return facts

    if code == "Z04":
        fenestrations = graph["zone"].get_unique_fenestration_constructions()
        materials = graph["zone"].get_unique_materials()
        surfaces = graph["zone"].get_unique_surface_constructions()
        return {
            "fenestration_constructions": [
                {"id": key, "name": value.name} for key, value in fenestrations.items()
            ],
            "materials": [
                {"id": key, "name": value.name} for key, value in materials.items()
            ],
            "surface_constructions": [
                {"id": key, "name": value.name} for key, value in surfaces.items()
            ],
        }

    if code == "Z05":
        return {"to_dragon": _error(graph["zone"].to_dragon)}

    raise RuntimeError(f"Unknown shape oracle case code: {code}")


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {item["id"]: canonical_sha256(item) for item in cases}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _expected_runtime() -> dict[str, Any]:
    return {
        "byteorder": "little",
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "platform": REQUIRED_PLATFORM,
        "pointer_width_bits": REQUIRED_POINTER_WIDTH_BITS,
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": "3.12.7",
    }


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


def _expected_upstream(loaded_sources: list[dict[str, Any]]) -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "loaded_sources": loaded_sources,
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }


def _expected_loaded_sources() -> list[dict[str, Any]]:
    return [
        {
            "ast_sha256": ast_hash,
            "bytes": byte_count,
            "module": module,
            "path": path,
            "source_sha256": source_hash,
        }
        for module, path, byte_count, source_hash, ast_hash in EXPECTED_LOADED_SOURCES
    ]


def _expected_contract() -> dict[str, Any]:
    counts = Counter(CLASSIFICATIONS.values())
    return {
        "adaptations": EXCEPTION_ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": dict(sorted(counts.items())),
        "classifications": CLASSIFICATIONS,
        "closure": {
            "excluded_indices": [item[0] for item in EXPECTED_EXCLUDED],
            "excluded_symbols": list(EXCLUDED_SYMBOLS),
            "target_count": len(EXPECTED_TARGETS),
            "target_indices": [item[0] for item in EXPECTED_TARGETS],
        },
        "native_routes": NATIVE_ROUTES,
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _validate_runtime() -> None:
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("CPython 3.12.7 is required for this oracle.")
    if sys.platform != REQUIRED_PLATFORM or struct.calcsize("P") * 8 != REQUIRED_POINTER_WIDTH_BITS:
        raise SystemExit("The pinned 64-bit Windows runtime is required.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("The pinned CPython hash implementation is required.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required.")
    if not sys.dont_write_bytecode:
        raise SystemExit("PYTHONDONTWRITEBYTECODE=1 is required.")
    dependencies = {name: importlib.metadata.version(name) for name in EXPECTED_DEPENDENCIES}
    if dependencies != EXPECTED_DEPENDENCIES:
        raise SystemExit("Pinned Python dependency versions drifted.")


def _validate_artifacts() -> None:
    for path, byte_count, expected_hash in (
        (SUPPORT_PATH, EXPECTED_SUPPORT_BYTES, EXPECTED_SUPPORT_SHA256),
        (BOOTSTRAP_PATH, EXPECTED_BOOTSTRAP_BYTES, EXPECTED_BOOTSTRAP_SHA256),
    ):
        if path.stat().st_size != byte_count or sha256_file(path) != expected_hash:
            raise SystemExit(f"Pinned oracle artifact drifted: {path.name}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source: Path | None = None
) -> dict[str, Any]:
    source = source.resolve() if source is not None else _find_pinned_source()
    if source.stat().st_size != EXPECTED_SOURCE_BYTES or sha256_file(source) != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported EPlusSimple shape source is not pinned.")
    if commit != EXPECTED_UPSTREAM_COMMIT or inventory["content_sha256"] != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The requested upstream identity is not pinned.")
    _validate_artifacts()
    modules = _import_modules(source)
    loaded_sources = _loaded_source_receipts(source)
    observed: dict[str, dict[str, Any]] = {}
    for definition in case_definitions():
        observed[definition["id"]] = _execute_case(definition["code"], modules)
    fact_hashes = {identifier: canonical_sha256(facts) for identifier, facts in observed.items()}
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned EPlusSimple shape facts drifted.\nOBSERVED_FACT_HASHES\n"
            + strict_json_dumps(fact_hashes, indent=2)
        )
    cases: list[dict[str, Any]] = []
    for definition in case_definitions():
        identifier = definition["id"]
        case = dict(definition)
        case["python"] = {
            "facts": observed[identifier],
            "facts_sha256": fact_hashes[identifier],
            "outcome": "observed",
        }
        cases.append(case)
    case_hashes = case_sha256(cases)
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise SystemExit(
            "Pinned EPlusSimple shape cases drifted.\nOBSERVED_CASE_HASHES\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    result = {
        "artifacts": _expected_artifacts(),
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": _expected_contract(),
        "excluded_receipts": inventory["excluded_receipts"],
        "fact_sha256": fact_hashes,
        "runtime": _expected_runtime(),
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": _expected_upstream(loaded_sources),
    }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{context} keys drifted: expected={sorted(expected)!r}, actual={actual!r}")


def _validate_safe_tree(value: Any, context: str = "root") -> None:
    if value is None or isinstance(value, (bool, int)):
        return
    if isinstance(value, float):
        raise RuntimeError(f"Raw float escaped into {context}.")
    if isinstance(value, str):
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"Raw address escaped into {context}.")
        if WINDOWS_ABSOLUTE_PATH_PATTERN.search(value):
            raise RuntimeError(f"Absolute path escaped into {context}.")
        if GUID_PATTERN.search(value) or TIMESTAMP_PATTERN.search(value):
            raise RuntimeError(f"Nondeterministic token escaped into {context}.")
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{context}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string key escaped into {context}.")
            _validate_safe_tree(key, f"{context}.key")
            _validate_safe_tree(item, f"{context}.{key}")
        return
    raise RuntimeError(f"Unsupported value {type(value).__name__} in {context}.")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "artifacts", "case_sha256", "cases", "cases_sha256",
            "consumer_contract", "excluded_receipts", "fact_sha256", "runtime",
            "schema", "symbols", "target_receipts", "upstream",
        },
        "root",
    )
    _validate_safe_tree(value)
    if value["schema"] != SCHEMA:
        raise RuntimeError("Shape oracle schema drifted.")
    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Shape oracle case count drifted.")
    if [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Shape oracle case order drifted.")
    by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Shape case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"Shape Python outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"Inline shape fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Shape fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned shape fact hashes drifted.")
    case_hashes = case_sha256(cases)
    if value["case_sha256"] != case_hashes:
        raise RuntimeError("Shape per-case hash map drifted.")
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned shape case hashes drifted.")
    aggregate = cases_sha256(cases)
    if value["cases_sha256"] != aggregate:
        raise RuntimeError("Shape aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned shape aggregate case hash drifted.")
    if value["consumer_contract"] != _expected_contract():
        raise RuntimeError("Shape consumer contract drifted.")
    if value["runtime"] != _expected_runtime() or value["artifacts"] != _expected_artifacts():
        raise RuntimeError("Shape runtime or artifact pins drifted.")
    if value["upstream"] != _expected_upstream(_expected_loaded_sources()):
        raise RuntimeError("Shape upstream source pins drifted.")
    if value["symbols"] != [_descriptor(item) for item in value["target_receipts"]]:
        raise RuntimeError("Shape symbol descriptors drifted.")
    if [item["inventory_index"] for item in value["target_receipts"]] != [item[0] for item in EXPECTED_TARGETS]:
        raise RuntimeError("Shape target index closure drifted.")
    if [item["symbol"] for item in value["target_receipts"]] != list(TARGET_SYMBOLS):
        raise RuntimeError("Shape target symbol closure drifted.")
    if [item["inventory_index"] for item in value["excluded_receipts"]] != [item[0] for item in EXPECTED_EXCLUDED]:
        raise RuntimeError("Shape excluded index closure drifted.")
    if [item["symbol"] for item in value["excluded_receipts"]] != list(EXCLUDED_SYMBOLS):
        raise RuntimeError("Shape excluded symbol closure drifted.")
    if EXPECTED_TARGET_RECEIPTS_SHA256 and canonical_sha256(value["target_receipts"]) != EXPECTED_TARGET_RECEIPTS_SHA256:
        raise RuntimeError("Shape target receipts drifted.")
    if EXPECTED_EXCLUDED_RECEIPTS_SHA256 and canonical_sha256(value["excluded_receipts"]) != EXPECTED_EXCLUDED_RECEIPTS_SHA256:
        raise RuntimeError("Shape excluded receipts drifted.")


def main() -> int:
    args = parse_args()
    _validate_runtime()
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote EPlusSimple shape-core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

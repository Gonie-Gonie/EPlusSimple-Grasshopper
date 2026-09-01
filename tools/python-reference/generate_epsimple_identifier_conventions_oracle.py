"""Generate the pinned EPlusSimple identifier and metadata conventions oracle.

The bounded corpus executes exactly 34 unresolved declarations from
``src/epsimple/constants.py``.  It deliberately excludes both ``__repr__``
declarations, which remain out of scope, and does not execute the numeric
constant families covered by the separate numeric oracle.

Run this generator through ``bootstrap_reference.py`` so the exact pinned
upstream source is available on ``sys.path``.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import copy
import importlib.util
import inspect
import os
from pathlib import Path
import re
import shutil
import struct
import sys
import tempfile
from types import ModuleType
from typing import Any, Callable, Iterator


SCHEMA = "dragons.python-reference.epsimple-identifier-conventions.v1"
SOURCE_PATH = "src/epsimple/constants.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_BYTES = 4_873
EXPECTED_SOURCE_SHA256 = (
    "sha256:d5dd5241ec90b14ba3708a525cd74279a8cdc238164a5b8544c4c82b05a29897"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:6740f081f087834aadfef0c11da6cdbe11f907dc170b48ebaa287e000eb6e27b"
)
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
REQUIRED_PLATFORM = "win32"
REQUIRED_POINTER_WIDTH_BITS = 64

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
        "_dragons_epsimple_identifier_support", SUPPORT_PATH
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

AUTO_MEMBERS = (
    ("MATERIAL", "MTRL"),
    ("SURFACE_CONSTRUCTION", "CTSF"),
    ("FENESTRATION_CONSTRUCTION", "CTFN"),
    ("SOURCE_SYSTEM", "SRCE"),
    ("SUPPLY_SYSTEM", "SUPL"),
    ("HEAT_EXCHANGER", "ERVT"),
    ("PV_PANEL", "PVPN"),
    ("SURFACE", "SURF"),
    ("FENESTRATION", "FNST"),
    ("ZONE", "ZONE"),
    ("DAY_SCHEDULE", "DYSC"),
    ("RULESET", "RLST"),
    ("SCHEDULE", "SCHE"),
    ("PROFILE", "PRFL"),
)
SPECIAL_MEMBERS = (
    ("SPECIAL", "SPECIAL"),
    ("DB", "FROM_DB"),
    ("CLONE", "CLONE_OF"),
    ("FLIP", "REVERSED"),
    ("COOLROOF", "FOR_COOLROOF"),
)
AUTO_MEMBER_SYMBOLS = tuple(f"AUTOID_PREFIX.{name}" for name, _ in AUTO_MEMBERS)
SPECIAL_MEMBER_SYMBOLS = tuple(f"SpecialTag.{name}" for name, _ in SPECIAL_MEMBERS)

# Inventory order is authoritative and intentionally differs from declaration
# order within each Enum class.
EXPECTED_TARGETS = (
    (10, "AUTOID_PREFIX", "sha256:9a7c270abf554af2ac0d3455101382eca02debe8c0b23e6f8c3f8a465bb32355"),
    (11, "AUTOID_PREFIX.DAY_SCHEDULE", "sha256:7d4821ca360166e6a06218c647b7ea935dd62080d896fd2f45cdff14da52eea0"),
    (12, "AUTOID_PREFIX.FENESTRATION", "sha256:d327acd7e82d257668484c17fe1ad79cca5a086b7977682c6e0a07af27987603"),
    (13, "AUTOID_PREFIX.FENESTRATION_CONSTRUCTION", "sha256:a00d7b14c20b1fbeaedf4e6b456bff8555bcb9ee539f74799d9e7e42a40fcc80"),
    (14, "AUTOID_PREFIX.HEAT_EXCHANGER", "sha256:d76b9ddc6df8f01d27ebf334bb8797bf798947ed40835eea8f0cf5fc84d94ccd"),
    (15, "AUTOID_PREFIX.MATERIAL", "sha256:9b7489e4c9b530dab76d9d2dd9cc834d6f751d1d35188c70976af5ecea048275"),
    (16, "AUTOID_PREFIX.PROFILE", "sha256:f04014577c229312753d0289ef8342419fb7fed9452799dc2bce8e6d5438c32e"),
    (17, "AUTOID_PREFIX.PV_PANEL", "sha256:46500b8a4aa511167e9fcfb13e33c74b901d1ba1a274ec71390708960fee493a"),
    (18, "AUTOID_PREFIX.RULESET", "sha256:e5ac2688f0382545b277dae27cc3a02744e6d5f7f3de1c1111ce2b487751bc15"),
    (19, "AUTOID_PREFIX.SCHEDULE", "sha256:c61dbb424961f11afedba70eabadd6c54ccdb52a7f7be1d56299ede42c0468c6"),
    (20, "AUTOID_PREFIX.SOURCE_SYSTEM", "sha256:60d016219cabe29d48669ee37e1d223932bfc6556bcc8f0a4a5ea0af147655c3"),
    (21, "AUTOID_PREFIX.SUPPLY_SYSTEM", "sha256:c2e6d435b1a6650d0998650fdb23d4310cd07ce02806553a645755990ca3bcd4"),
    (22, "AUTOID_PREFIX.SURFACE", "sha256:7fca2d17fabcb91b32dd28349a50f44fe1ed0c0be63cf6dba21d37ffbf229472"),
    (23, "AUTOID_PREFIX.SURFACE_CONSTRUCTION", "sha256:147095a3d8aeedcd5fb82264a34c353733325268d6ad25b8e85222340fff3ca5"),
    (24, "AUTOID_PREFIX.ZONE", "sha256:5f36f9019cc2b5ad1e96b3338a84b04d4da0360941a4499db6846ddafa926ccf"),
    (25, "AUTOID_PREFIX.__format__", "sha256:d0c85092c98182b0366673cd287507b75d62850d9e272b32896597e787a58170"),
    (27, "AUTOID_PREFIX.__str__", "sha256:13ed292afebbf1a59717e776df9d6ba3e220d2cc248ac2cc450deab9c2261c98"),
    (31, "Directory", "sha256:5b876ad7fd9b11f66cc01ecb6c43d4e143b6f0258ba070c02551d968dd68aaf6"),
    (32, "Directory.CONSTRUCTION_DIR", "sha256:91c573a02d0e3b2d93a1271fbe1c3ddb5d4d10c04083707c799fa1503f5b3dea"),
    (33, "Directory.PROFILE_DIR", "sha256:f65d5eaefa2bc1cbb6f0c9b5904624194a1551f48e7966c7973d35526bad4fa6"),
    (34, "Directory.WEATHER_DATA_DIR", "sha256:8a5bf6543c4f0db98ee0169deb7dfddd4c126e34d52aa67b512136dc3e8bcd01"),
    (35, "Directory.WEATHER_META_DIR", "sha256:15e81d1d4205ffe651af323c3cc7352255847972301ad1753fd3d8d5098dc260"),
    (36, "PackageInfo", "sha256:aaf5b98d4a7dc29f83b698f1fb2881b7bb258885bd2aaf17a53b6da902d1eda1"),
    (37, "PackageInfo.NAME", "sha256:537c8c3bc3c2d48105e8e6c453208e725f985ac9d84f87e5f66c094ea5696cad"),
    (38, "PackageInfo.REQUIRED_PYTHON", "sha256:cf74d0eb707a3668aa515bdd31d767109337841bcf28f03b96c6e9264d9407a4"),
    (39, "PackageInfo.VERSION", "sha256:a8260e5f38f8422e1ac38ce24fd0136b4bb3a4de24f268e9a262aa6034031ea4"),
    (58, "SpecialTag", "sha256:a66e2175ee03b1d6d73c70998500b45ae7eac6989b60ddd2adb09882a17f2c9b"),
    (59, "SpecialTag.CLONE", "sha256:00989ee6011feaa240308f2a1e1bb8c47def1f4be493b51b91e75c75ee7bf39f"),
    (60, "SpecialTag.COOLROOF", "sha256:622c00d22fff7838ef72f37deeac6461b137ff084511ea1085717955cc893f4b"),
    (61, "SpecialTag.DB", "sha256:a43168ea5003995edfe35fec6f3f6b25ad26eb9111e97337db7dece5e0ede870"),
    (62, "SpecialTag.FLIP", "sha256:4a5884386e242adce385eb0991559fb93426172ac5e152276000036947d4683f"),
    (63, "SpecialTag.SPECIAL", "sha256:0faf9b24524c68d912d1b0d1438b85bf856778fbbdc7a11ef7c20137c8d08be6"),
    (64, "SpecialTag.__format__", "sha256:4ef932bb8135c4cfaf7e17e805cfb299e50d9400f4a106605bdb2fb75477d3a0"),
    (66, "SpecialTag.__str__", "sha256:13ed292afebbf1a59717e776df9d6ba3e220d2cc248ac2cc450deab9c2261c98"),
)
EXPECTED_EXCLUDED = (
    (26, "AUTOID_PREFIX.__repr__", "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e"),
    (65, "SpecialTag.__repr__", "sha256:f40e4929e52296ef884601b57579680f005907a223f96e12fc07cce3d637265e"),
)
TARGET_SYMBOLS = tuple(item[1] for item in EXPECTED_TARGETS)
EXCLUDED_SYMBOLS = tuple(item[1] for item in EXPECTED_EXCLUDED)

EXCEPTION_ADAPTATIONS = {
    "AUTOID_PREFIX": "immutable-native-auto-id-prefix-catalog-9a7c270a",
    "Directory": "embedded-explicit-native-resource-layout-5b876ad7",
    "Directory.CONSTRUCTION_DIR": "embedded-native-construction-resources-91c573a0",
    "Directory.PROFILE_DIR": "embedded-native-profile-resources-f65d5eae",
    "Directory.WEATHER_DATA_DIR": "caller-supplied-native-weather-data-root-8a5bf654",
    "Directory.WEATHER_META_DIR": "embedded-native-weather-metadata-resources-15e81d1d",
    "PackageInfo": "static-native-simpledragon-package-information-aaf5b98d",
    "PackageInfo.NAME": "native-simpledragon-package-name-537c8c3b",
    "PackageInfo.REQUIRED_PYTHON": "compiled-simpledragon-target-framework-contract-cf74d0eb",
    "PackageInfo.VERSION": "native-simpledragon-and-upstream-version-identity-a8260e5f",
    "SpecialTag": "immutable-native-special-tag-catalog-a66e2175",
}
CLASSIFICATIONS = {
    symbol: "exception" if symbol in EXCEPTION_ADAPTATIONS else "equivalent"
    for symbol in TARGET_SYMBOLS
}
ASSERTION_IDS = {
    symbol: f"epsimple-identifier-conventions-{index}-{symbol_hash[7:15]}"
    for index, symbol, symbol_hash in EXPECTED_TARGETS
}

NATIVE_ROUTES = {
    **{
        symbol: "Dragons.SimpleDragon.AutoIdPrefix"
        for symbol in ("AUTOID_PREFIX", *AUTO_MEMBER_SYMBOLS)
    },
    "AUTOID_PREFIX.__format__": "Dragons.SimpleDragon.AutoIdPrefix.ToString(string?, IFormatProvider?)",
    "AUTOID_PREFIX.__str__": "Dragons.SimpleDragon.AutoIdPrefix.ToString()",
    "Directory": "Dragons.SimpleDragon.SimpleDragonEmbeddedData and WeatherSelection.ResolveEpwPath",
    "Directory.CONSTRUCTION_DIR": "Dragons.SimpleDragon.SimpleDragonEmbeddedData construction resources",
    "Directory.PROFILE_DIR": "Dragons.SimpleDragon.SimpleDragonEmbeddedData profile resources",
    "Directory.WEATHER_DATA_DIR": "Dragons.SimpleDragon.WeatherSelection.ResolveEpwPath",
    "Directory.WEATHER_META_DIR": "Dragons.SimpleDragon.SimpleDragonEmbeddedData weather resources",
    "PackageInfo": "Dragons.SimpleDragon.PackageInfo",
    "PackageInfo.NAME": "Dragons.SimpleDragon.PackageInfo.Name",
    "PackageInfo.REQUIRED_PYTHON": "net48, net7.0-windows, and net8.0-windows target frameworks",
    "PackageInfo.VERSION": "Dragons.SimpleDragon.PackageInfo.Version and Compatibility.UpstreamVersion",
    **{
        symbol: "Dragons.SimpleDragon.SpecialTag"
        for symbol in ("SpecialTag", *SPECIAL_MEMBER_SYMBOLS)
    },
    "SpecialTag.__format__": "Dragons.SimpleDragon.SpecialTag.ToString(string?, IFormatProvider?)",
    "SpecialTag.__str__": "Dragons.SimpleDragon.SpecialTag.ToString()",
}

PREFIX = "epsimple-identifier-conventions."
CASE_SPECS = (
    ("A01", "autoid-topology-order-values", "autoid", ("AUTOID_PREFIX", *AUTO_MEMBER_SYMBOLS), ()),
    ("A02", "autoid-string-value-semantics", "autoid", AUTO_MEMBER_SYMBOLS, ("AUTOID_PREFIX",)),
    ("A03", "autoid-construction-lookup-errors", "autoid", ("AUTOID_PREFIX",), AUTO_MEMBER_SYMBOLS),
    ("A04", "autoid-format-empty-custom", "autoid", ("AUTOID_PREFIX.__format__", "AUTOID_PREFIX.__str__"), ("AUTOID_PREFIX", *AUTO_MEMBER_SYMBOLS)),
    ("A05", "autoid-direct-format-type-context", "autoid", ("AUTOID_PREFIX",), ("AUTOID_PREFIX.__format__", "AUTOID_PREFIX.__str__")),
    ("A06", "autoid-mutation-copy-alias-context", "autoid", ("AUTOID_PREFIX",), AUTO_MEMBER_SYMBOLS),
    ("D01", "directory-import-topology-path-roles", "directory", ("Directory", "Directory.CONSTRUCTION_DIR", "Directory.PROFILE_DIR", "Directory.WEATHER_DATA_DIR", "Directory.WEATHER_META_DIR"), ()),
    ("D02", "directory-two-location-relocation", "directory", ("Directory.CONSTRUCTION_DIR", "Directory.PROFILE_DIR", "Directory.WEATHER_DATA_DIR", "Directory.WEATHER_META_DIR"), ("Directory",)),
    ("D03", "directory-class-attribute-mutation", "directory", ("Directory", "Directory.CONSTRUCTION_DIR", "Directory.PROFILE_DIR", "Directory.WEATHER_DATA_DIR", "Directory.WEATHER_META_DIR"), ()),
    ("D04", "directory-instance-shadow-construction-errors", "directory", ("Directory",), ("Directory.CONSTRUCTION_DIR", "Directory.PROFILE_DIR", "Directory.WEATHER_DATA_DIR", "Directory.WEATHER_META_DIR")),
    ("P01", "package-info-topology-values", "package", ("PackageInfo", "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION"), ()),
    ("P02", "package-info-class-attribute-mutation", "package", ("PackageInfo", "PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION"), ()),
    ("P03", "package-info-instance-shadow-construction-errors", "package", ("PackageInfo",), ("PackageInfo.NAME", "PackageInfo.REQUIRED_PYTHON", "PackageInfo.VERSION")),
    ("P04", "package-name-string-operations-errors", "package", ("PackageInfo.NAME",), ("PackageInfo",)),
    ("P05", "package-version-tuple-operations-errors", "package", ("PackageInfo.VERSION",), ("PackageInfo",)),
    ("P06", "required-python-comparison-errors", "package", ("PackageInfo.REQUIRED_PYTHON",), ("PackageInfo",)),
    ("S01", "special-tag-topology-order-values", "special-tag", ("SpecialTag", *SPECIAL_MEMBER_SYMBOLS), ()),
    ("S02", "special-tag-string-value-semantics", "special-tag", SPECIAL_MEMBER_SYMBOLS, ("SpecialTag",)),
    ("S03", "special-tag-construction-lookup-errors", "special-tag", ("SpecialTag",), SPECIAL_MEMBER_SYMBOLS),
    ("S04", "special-tag-format-empty-custom", "special-tag", ("SpecialTag.__format__", "SpecialTag.__str__"), ("SpecialTag", *SPECIAL_MEMBER_SYMBOLS)),
    ("S05", "special-tag-direct-format-type-context", "special-tag", ("SpecialTag",), ("SpecialTag.__format__", "SpecialTag.__str__")),
    ("S06", "special-tag-mutation-copy-alias-context", "special-tag", ("SpecialTag",), SPECIAL_MEMBER_SYMBOLS),
)
EXPECTED_CASE_IDS = tuple(PREFIX + suffix for _, suffix, _, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 22

# Filled from a direct CPython 3.12.7 bootstrap run.  These pins deliberately
# cover both the fact tree and the complete case record around it.
EXPECTED_FACT_SHA256 = {
    "epsimple-identifier-conventions.autoid-construction-lookup-errors": "sha256:91cdc2fb8293eca7c9d7b2d13e73cfa3b6b924a0c06049a1d2a0af77ac816c01",
    "epsimple-identifier-conventions.autoid-direct-format-type-context": "sha256:195155edcd826e64a318ba975ed6161d64cfe02af65f8bb5ace2df07d61af007",
    "epsimple-identifier-conventions.autoid-format-empty-custom": "sha256:5d2f30c684ebbef6bd7d8f504214a03d81701d09af5a9c667a378bc9c4d345a9",
    "epsimple-identifier-conventions.autoid-mutation-copy-alias-context": "sha256:e4a73198d0fd5b15ceb948a25e2991dc3113e0c6f21641e0f670204122f1f62b",
    "epsimple-identifier-conventions.autoid-string-value-semantics": "sha256:abd961181c7f6abc5bd5d8e5e2b65fddd7f69a65e089c6732f2403ba134296da",
    "epsimple-identifier-conventions.autoid-topology-order-values": "sha256:b86d8a037a51a8bbcc78576f1f3a47224eec0c1de54f0695835e397ba1b77082",
    "epsimple-identifier-conventions.directory-class-attribute-mutation": "sha256:5b0aa58b88482801cf9868da1b383bdab6b8b21f5b3898f73d5ee07e365dc368",
    "epsimple-identifier-conventions.directory-import-topology-path-roles": "sha256:7cac34d818f671dfacfea500ac2cca72f508083cac28ffeafb3be75a9c635c75",
    "epsimple-identifier-conventions.directory-instance-shadow-construction-errors": "sha256:6cb6ab832b426a84859b69a8a9e144279decaa3d7247755ca093bc98c1753261",
    "epsimple-identifier-conventions.directory-two-location-relocation": "sha256:720d3c2f30f7b27ca8bc518cbb6464368beed73a571b49342f685f8ccdaf0815",
    "epsimple-identifier-conventions.package-info-class-attribute-mutation": "sha256:316bdcc43df9ccb8ddddb6a4163c7bdda00926a6220a6a4356910bc799aec100",
    "epsimple-identifier-conventions.package-info-instance-shadow-construction-errors": "sha256:34e7d59eca31c2c1a8dc35fccf2a07970ec46c6889ef5eb14f158c10d1dbe380",
    "epsimple-identifier-conventions.package-info-topology-values": "sha256:f59eb4d5b80189cc791d946e1d662b7209a5274e9aff5be78444ac8f736b3fb4",
    "epsimple-identifier-conventions.package-name-string-operations-errors": "sha256:a6d812adb05f7d8c1d9c7618f3a893f401d8599cb4dcc7205050f409045bfc84",
    "epsimple-identifier-conventions.package-version-tuple-operations-errors": "sha256:e5fcb3126fe49753262efdb8ccba508cfa1888bddeb7a06d7560fcbb97a7962c",
    "epsimple-identifier-conventions.required-python-comparison-errors": "sha256:c202a35f61b40c279248de8c640957455b1f6850c4b697bbf3957a2dbdef56b6",
    "epsimple-identifier-conventions.special-tag-construction-lookup-errors": "sha256:1bd97477876e5cfc9098df97e5073c2a99ad967538012458c2805e49fdf2fdc2",
    "epsimple-identifier-conventions.special-tag-direct-format-type-context": "sha256:0e8726490fd3628580c631915ed55145f54338f914780728be0e265fab9866f1",
    "epsimple-identifier-conventions.special-tag-format-empty-custom": "sha256:e9474278e2a07499f9d77430c4d9e7b03349a50e1d060a9183f3ca7a1522acd1",
    "epsimple-identifier-conventions.special-tag-mutation-copy-alias-context": "sha256:9be4d17fde6cf7e53ab6e05c643b87539dd7c5ef9323acfba2651879fe416fb1",
    "epsimple-identifier-conventions.special-tag-string-value-semantics": "sha256:4685cdfdcd892090ecab457b08f5c5f90476b738df722ccea650df3dd51de594",
    "epsimple-identifier-conventions.special-tag-topology-order-values": "sha256:26b4ec7fcb9add9a377ec5e9a71b09e145ba63ec7b3e1fb17238f4b4e47ec69a",
}
EXPECTED_CASE_SHA256 = {
    "epsimple-identifier-conventions.autoid-construction-lookup-errors": "sha256:d172e2d8520295227932ab161d479145e4c603d1a521a04a34d433701822db95",
    "epsimple-identifier-conventions.autoid-direct-format-type-context": "sha256:a52a1ad8f120251a61d24a56eff1b933f3fc0b16c2565805d75a3e0a2ab931e5",
    "epsimple-identifier-conventions.autoid-format-empty-custom": "sha256:bfee63da4c39846ccf17d7fdeb0fb262f7ee3b9d4dc838bd6d94f926de357ca7",
    "epsimple-identifier-conventions.autoid-mutation-copy-alias-context": "sha256:5f7f0b11edadb9b86ecaf0a15eeaebca0d65915e91a03ec9bd5e7819b29b96d8",
    "epsimple-identifier-conventions.autoid-string-value-semantics": "sha256:86ee3d64986853ea3388c3c5e11e7b9598a1e3c0fd309e4dee7d31410e8d25f1",
    "epsimple-identifier-conventions.autoid-topology-order-values": "sha256:f9f582260b35f90e65d14df7f968ed44d3969dea64071c5314f40bca80a286b9",
    "epsimple-identifier-conventions.directory-class-attribute-mutation": "sha256:f87fa5a492ce71c964b1c2f499f34ae28ffee5c7e66fa305f0e9baf479f21694",
    "epsimple-identifier-conventions.directory-import-topology-path-roles": "sha256:70c4bac4355394e0528f3eb2e7cc2d167f76ebc1a19106825ed8a4258ae7fd86",
    "epsimple-identifier-conventions.directory-instance-shadow-construction-errors": "sha256:1df5c68fc31d36fd8f5019f9a0435a44f73636dc69ad94fc0716228d026acb05",
    "epsimple-identifier-conventions.directory-two-location-relocation": "sha256:99a57fb24c50cbc2bbc2ad47bbe4ada4434eb782247386d4001438f2cdfd045a",
    "epsimple-identifier-conventions.package-info-class-attribute-mutation": "sha256:9f47ac49cc441103847fc505f11787af0866e16dce0dab7ebde29a4ac4f1f4d3",
    "epsimple-identifier-conventions.package-info-instance-shadow-construction-errors": "sha256:6c14827958990d69e12b316473e28340ef7d42580d321cfdec475cf5f2419d55",
    "epsimple-identifier-conventions.package-info-topology-values": "sha256:295492546456eefd4bd71aecddf241b66f2078ceb3f2e30a0282c3c53db118d6",
    "epsimple-identifier-conventions.package-name-string-operations-errors": "sha256:43a3ca062e10f6e2e070044b8f7e269138bc01432d1037d6e0336f460d2275ff",
    "epsimple-identifier-conventions.package-version-tuple-operations-errors": "sha256:b957013dc67207cfc1e7f711fa9721facd61e1db89580c9b2d09d710536065a5",
    "epsimple-identifier-conventions.required-python-comparison-errors": "sha256:1e946a62576ed7ef371090823414ce1398d83c505fe956aa707b61cb03934a52",
    "epsimple-identifier-conventions.special-tag-construction-lookup-errors": "sha256:433cf040ce5c958055c731dbab8751e8f8046638ef62512c61041f91ed0964f1",
    "epsimple-identifier-conventions.special-tag-direct-format-type-context": "sha256:baf99a9e112a1756b6d23933facd36551db0f0fcd8addb604faef8a1cfc6ebf7",
    "epsimple-identifier-conventions.special-tag-format-empty-custom": "sha256:af690eade3cf4510005b0ebc3374dcaac2b372bf7eba0318ae61731a4beb86bc",
    "epsimple-identifier-conventions.special-tag-mutation-copy-alias-context": "sha256:802e342589ebee34b8bdaf739cfcf7abd0bf2cbbc7cf377546d66c0e8976f22d",
    "epsimple-identifier-conventions.special-tag-string-value-semantics": "sha256:90b472cb993e8237e5854101d697c50b85187c7deb8af22af561f652b8e7c42c",
    "epsimple-identifier-conventions.special-tag-topology-order-values": "sha256:046e5d02046697287f196af925a8ee8c330d19fd894e0481a7c84924fb7fcaae",
}
EXPECTED_CASES_SHA256 = (
    "sha256:6244a03437d0d6f50bfeb135c99bfaf284804391998f168a675b30dc60ef3c10"
)

DIRECTORY_MEMBERS = (
    "WEATHER_META_DIR",
    "WEATHER_DATA_DIR",
    "PROFILE_DIR",
    "CONSTRUCTION_DIR",
)
DIRECTORY_ROLES = {
    "CONSTRUCTION_DIR": ("module-root", "_data/construction"),
    "PROFILE_DIR": ("module-root", "_data/profile"),
    "WEATHER_DATA_DIR": ("package-root", "runtime/Weather/TMY"),
    "WEATHER_META_DIR": ("module-root", "_data/weather"),
}
PACKAGE_MEMBERS = ("NAME", "VERSION", "REQUIRED_PYTHON")
ISOLATED_FILES = (
    "location-a/repository/src/epsimple/constants.py",
    "location-b/repository/src/epsimple/constants.py",
)
ISOLATED_MODULE_NAMES = (
    "_dragons_epsimple_identifier_location_a",
    "_dragons_epsimple_identifier_location_b",
)

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
    r"(?<!\d)\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}"
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _require_keys(value: dict[str, Any], expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        raise RuntimeError(
            f"{context} keys drifted: expected={sorted(expected)!r}, "
            f"actual={sorted(value) if isinstance(value, dict) else type(value).__name__!r}"
        )


def _validate_safe_string(value: str, context: str) -> None:
    if RAW_ADDRESS_PATTERN.search(value):
        raise RuntimeError(f"Raw memory address escaped into {context}.")
    if ABSOLUTE_PATH_PATTERN.search(value):
        raise RuntimeError(f"Host-absolute path escaped into {context}.")
    if GUID_PATTERN.search(value):
        raise RuntimeError(f"Nondeterministic GUID escaped into {context}.")
    if TIMESTAMP_PATTERN.search(value):
        raise RuntimeError(f"Nondeterministic timestamp escaped into {context}.")


def _validate_safe_tree(value: Any, context: str = "root") -> None:
    if value is None or isinstance(value, (bool, int)):
        return
    if isinstance(value, float):
        raise RuntimeError(f"Raw floating-point value is forbidden in {context}.")
    if isinstance(value, str):
        _validate_safe_string(value, context)
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{context}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-string dictionary key in {context}.")
            _validate_safe_string(key, f"{context} key")
            _validate_safe_tree(item, f"{context}.{key}")
        return
    raise RuntimeError(f"Unsupported fixture value {type(value).__name__} in {context}.")


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
    expected_source_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    if source_file != expected_source_file:
        raise SystemExit("The EPlusSimple constants source receipt drifted.")

    symbols = value.get("symbols")
    if not isinstance(symbols, list):
        raise SystemExit("The inventory symbol list is missing.")
    target_receipts: list[dict[str, Any]] = []
    for index, expected_symbol, expected_hash in EXPECTED_TARGETS:
        item = symbols[index]
        if (
            item.get("path") != SOURCE_PATH
            or item.get("symbol") != expected_symbol
            or item.get("symbol_hash") != expected_hash
        ):
            raise SystemExit(f"Target inventory receipt drifted at index {index}.")
        target_receipts.append({"inventory_index": index, **item})
    excluded_receipts: list[dict[str, Any]] = []
    for index, expected_symbol, expected_hash in EXPECTED_EXCLUDED:
        item = symbols[index]
        if (
            item.get("path") != SOURCE_PATH
            or item.get("symbol") != expected_symbol
            or item.get("symbol_hash") != expected_hash
        ):
            raise SystemExit(f"Excluded repr receipt drifted at index {index}.")
        excluded_receipts.append({"inventory_index": index, **item})
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
            "id": PREFIX + suffix,
            "subfamily": subfamily,
            "target_symbols": list(targets),
        }
        for code, suffix, subfamily, targets, context in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Identifier convention case order drifted.")
    target_counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if set(target_counts) != set(TARGET_SYMBOLS):
        raise RuntimeError("Identifier convention target coverage is not exact.")
    permitted = set(TARGET_SYMBOLS)
    if any(
        not set(definition["context_symbols"]).issubset(permitted)
        for definition in definitions
    ):
        raise RuntimeError("Identifier convention context escaped the bounded target.")
    if set(EXCLUDED_SYMBOLS).intersection(
        symbol
        for definition in definitions
        for symbol in (*definition["target_symbols"], *definition["context_symbols"])
    ):
        raise RuntimeError("An out-of-scope repr symbol was retargeted.")
    return definitions


def _find_pinned_source() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        candidate = Path(entry) / "epsimple" / "constants.py"
        if (
            candidate.is_file()
            and candidate.stat().st_size == EXPECTED_SOURCE_BYTES
            and sha256_file(candidate) == EXPECTED_SOURCE_SHA256
        ):
            matches.append(candidate.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned epsimple/constants.py must be importable.")
    return unique[0]


def _load_module(source: Path, destination: Path, module_name: str) -> ModuleType:
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, destination)
    if (
        destination.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(destination) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The isolated constants.py copy is not byte-identical.")
    spec = importlib.util.spec_from_file_location(module_name, destination)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot import isolated constants module {module_name}.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    try:
        spec.loader.exec_module(module)
    except BaseException:
        sys.modules.pop(module_name, None)
        raise
    if Path(module.__file__).resolve() != destination.resolve():
        raise SystemExit("The isolated constants module resolved to another file.")
    return module


@contextmanager
def _isolated_modules(source: Path) -> Iterator[tuple[Path, ModuleType, ModuleType]]:
    with tempfile.TemporaryDirectory(prefix="epsimple-identifier-conventions-") as temporary:
        root = Path(temporary).resolve()
        modules: list[ModuleType] = []
        try:
            for relative, name in zip(ISOLATED_FILES, ISOLATED_MODULE_NAMES, strict=True):
                modules.append(_load_module(source, root / relative, name))
            yield root, modules[0], modules[1]
        finally:
            for name in ISOLATED_MODULE_NAMES:
                sys.modules.pop(name, None)


def _outcome(operation: Callable[[], Any]) -> dict[str, Any]:
    try:
        return {"outcome": "returned", "result": operation()}
    except BaseException as exception:  # noqa: BLE001 - exception topology is the observation.
        return {
            "error": {
                "message": str(exception),
                "type": type(exception).__name__,
            },
            "outcome": "raised",
        }


def _alias_groups(enum_type: type[Any]) -> list[list[str]]:
    groups: list[list[str]] = []
    seen: set[int] = set()
    for _, member in enum_type.__members__.items():
        identity = id(member)
        if identity in seen:
            continue
        seen.add(identity)
        names = [name for name, candidate in enum_type.__members__.items() if candidate is member]
        if len(names) > 1:
            groups.append(names)
    return groups


def _enum_topology(enum_type: type[Any]) -> dict[str, Any]:
    return {
        "alias_groups": _alias_groups(enum_type),
        "bases": [base.__name__ for base in enum_type.__bases__],
        "class_name": enum_type.__name__,
        "class_signature": str(inspect.signature(enum_type)),
        "declared_members": [
            {
                "canonical_name": member.name,
                "name": name,
                "value": member.value,
            }
            for name, member in enum_type.__members__.items()
        ],
        "iterated_names": [member.name for member in enum_type],
        "member_count": len(enum_type.__members__),
        "unique_member_count": len(list(enum_type)),
    }


def _enum_string_semantics(enum_type: type[Any]) -> dict[str, Any]:
    return {
        "members": [
            {
                "canonical_identity": enum_type(member.value) is member,
                "concat_left": "prefix/" + member,
                "concat_right": member + "/suffix",
                "equals_raw_value": member == member.value,
                "hash_equals_raw_value": hash(member) == hash(member.value),
                "is_str_instance": isinstance(member, str),
                "name": member.name,
                "raw_contains_token": member.value in member,
                "raw_value": member.value,
                "split_round_trip": (member + "/suffix").split("/")[0],
            }
            for member in enum_type
        ]
    }


def _enum_construction(enum_type: type[Any]) -> dict[str, Any]:
    declared = list(enum_type.__members__.items())
    return {
        "from_name": [
            {"name": name, "same_member": enum_type[name] is member}
            for name, member in declared
        ],
        "from_name_as_value": [
            {"name": name, "observation": _outcome(lambda name=name: enum_type(name).name)}
            for name, _ in declared
        ],
        "from_value": [
            {
                "name": name,
                "same_member": enum_type(member.value) is member,
                "value": member.value,
            }
            for name, member in declared
        ],
        "invalid": [
            {"label": "no-argument", "observation": _outcome(lambda: enum_type())},
            {"label": "none", "observation": _outcome(lambda: enum_type(None))},
            {"label": "integer", "observation": _outcome(lambda: enum_type(7))},
            {"label": "missing-value", "observation": _outcome(lambda: enum_type("__MISSING__"))},
            {"label": "missing-name", "observation": _outcome(lambda: enum_type["__MISSING__"])},
        ],
    }


def _enum_format(enum_type: type[Any]) -> dict[str, Any]:
    specs = ("", "SURFACE", ":", "표면", " ")
    return {
        "members": [
            {
                "formats": [
                    {"result": format(member, spec), "spec": spec} for spec in specs
                ],
                "name": member.name,
                "str": str(member),
                "value": member.value,
            }
            for member in enum_type
        ]
    }


class _StableFormatSpec:
    def __str__(self) -> str:
        return "STABLE_OBJECT"


def _enum_direct_format(enum_type: type[Any]) -> dict[str, Any]:
    member = next(iter(enum_type))
    return {
        "direct_format_int": _outcome(lambda: enum_type.__format__(member, 7)),
        "direct_format_none": _outcome(lambda: enum_type.__format__(member, None)),
        "direct_format_object": _outcome(
            lambda: enum_type.__format__(member, _StableFormatSpec())
        ),
        "direct_str_extra_argument": _outcome(lambda: enum_type.__str__(member, "x")),
        "format_builtin_none": _outcome(lambda: format(member, None)),
        "member_name": member.name,
    }


def _set_attribute(value: Any, name: str, replacement: Any) -> Any:
    setattr(value, name, replacement)
    return getattr(value, name)


def _delete_attribute(value: Any, name: str) -> Any:
    delattr(value, name)
    return getattr(value, name)


def _enum_mutation(enum_type: type[Any]) -> dict[str, Any]:
    member = next(iter(enum_type))
    extra_name = "AUDIT_EXTRA_ATTRIBUTE"
    member_extra = _outcome(lambda: _set_attribute(member, extra_name, "member-extra"))
    if hasattr(member, extra_name):
        delattr(member, extra_name)
    class_extra = _outcome(lambda: _set_attribute(enum_type, extra_name, "class-extra"))
    if hasattr(enum_type, extra_name):
        delattr(enum_type, extra_name)
    return {
        "alias_groups": _alias_groups(enum_type),
        "class_add_extra": class_extra,
        "class_delete_member": _outcome(lambda: _delete_attribute(enum_type, member.name)),
        "class_reassign_member": _outcome(
            lambda: _set_attribute(enum_type, member.name, "replacement")
        ),
        "deepcopy_identity": copy.deepcopy(member) is member,
        "member_add_extra": member_extra,
        "member_name": member.name,
        "member_set_name": _outcome(lambda: _set_attribute(member, "name", "replacement")),
        "member_set_value": _outcome(lambda: _set_attribute(member, "value", "replacement")),
        "shallow_copy_identity": copy.copy(member) is member,
    }


def _relative(path: Path, root: Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def _directory_state(directory: type[Any], location_root: Path) -> dict[str, Any]:
    module_root = directory._MODULE_ROOT
    package_root = directory._PACKAGE_ROOT
    result: dict[str, Any] = {}
    for name in DIRECTORY_MEMBERS:
        value = getattr(directory, name)
        anchor_name, suffix = DIRECTORY_ROLES[name]
        anchor = module_root if anchor_name == "module-root" else package_root
        result[name] = {
            "anchor": anchor_name,
            "is_path": isinstance(value, Path),
            "matches_role": value == anchor / Path(suffix),
            "relative_to_location": _relative(value, location_root),
            "suffix": suffix,
        }
    return result


def _directory_topology(module: ModuleType, root_a: Path) -> dict[str, Any]:
    directory = module.Directory
    return {
        "class_name": directory.__name__,
        "class_signature": str(inspect.signature(directory)),
        "data_root_relative": _relative(directory._DATA_DIR, root_a),
        "module_root_relative": _relative(directory._MODULE_ROOT, root_a),
        "package_root_relative": _relative(directory._PACKAGE_ROOT, root_a),
        "public_attribute_order": [
            name for name in directory.__dict__ if name in DIRECTORY_MEMBERS
        ],
        "state": _directory_state(directory, root_a),
    }


def _directory_relocation(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, Any]:
    return {
        "class_identity_distinct": module_a.Directory is not module_b.Directory,
        "location_a": _directory_state(module_a.Directory, root_a),
        "location_b": _directory_state(module_b.Directory, root_b),
        "public_absolute_values_distinct": {
            name: getattr(module_a.Directory, name) != getattr(module_b.Directory, name)
            for name in DIRECTORY_MEMBERS
        },
        "relative_roles_equal": _directory_state(module_a.Directory, root_a)
        == _directory_state(module_b.Directory, root_b),
    }


def _directory_mutation(module: ModuleType) -> dict[str, Any]:
    directory = module.Directory
    observations: list[dict[str, Any]] = []
    for name in DIRECTORY_MEMBERS:
        original = getattr(directory, name)
        replacement = "replacement/" + name.lower()
        try:
            setattr(directory, name, replacement)
            assigned = {
                "equals_replacement": getattr(directory, name) == replacement,
                "type": type(getattr(directory, name)).__name__,
            }
            delattr(directory, name)
            deleted = _outcome(lambda name=name: getattr(directory, name))
        finally:
            setattr(directory, name, original)
        observations.append(
            {
                "assigned": assigned,
                "deleted_lookup": deleted,
                "name": name,
                "restored_identity": getattr(directory, name) is original,
            }
        )
    return {"attributes": observations}


def _directory_instance(module: ModuleType) -> dict[str, Any]:
    directory = module.Directory
    instance = directory()
    shadowed: list[dict[str, Any]] = []
    for name in DIRECTORY_MEMBERS:
        class_value = getattr(directory, name)
        replacement = "instance/" + name.lower()
        setattr(instance, name, replacement)
        shadowed.append(
            {
                "class_unchanged": getattr(directory, name) is class_value,
                "instance_value": getattr(instance, name),
                "name": name,
            }
        )
    return {
        "construction": _outcome(lambda: directory().__class__.__name__),
        "keyword_argument": _outcome(lambda: directory(value=1)),
        "positional_argument": _outcome(lambda: directory(1)),
        "shadowed": shadowed,
    }


def _package_topology(module: ModuleType) -> dict[str, Any]:
    package = module.PackageInfo
    return {
        "attribute_order": [name for name in package.__dict__ if name in PACKAGE_MEMBERS],
        "class_name": package.__name__,
        "class_signature": str(inspect.signature(package)),
        "name": package.NAME,
        "name_type": type(package.NAME).__name__,
        "required_python": list(package.REQUIRED_PYTHON),
        "required_python_item_types": [type(item).__name__ for item in package.REQUIRED_PYTHON],
        "required_python_type": type(package.REQUIRED_PYTHON).__name__,
        "version": list(package.VERSION),
        "version_item_types": [type(item).__name__ for item in package.VERSION],
        "version_type": type(package.VERSION).__name__,
    }


def _package_mutation(module: ModuleType) -> dict[str, Any]:
    package = module.PackageInfo
    replacements = {
        "NAME": 17,
        "VERSION": "0.7.0",
        "REQUIRED_PYTHON": ["3", "12"],
    }
    observations: list[dict[str, Any]] = []
    for name in PACKAGE_MEMBERS:
        original = getattr(package, name)
        replacement = replacements[name]
        try:
            setattr(package, name, replacement)
            assigned = {
                "equals_replacement": getattr(package, name) == replacement,
                "type": type(getattr(package, name)).__name__,
            }
            delattr(package, name)
            deleted = _outcome(lambda name=name: getattr(package, name))
        finally:
            setattr(package, name, original)
        observations.append(
            {
                "assigned": assigned,
                "deleted_lookup": deleted,
                "name": name,
                "restored_identity": getattr(package, name) is original,
            }
        )
    return {"attributes": observations}


def _package_instance(module: ModuleType) -> dict[str, Any]:
    package = module.PackageInfo
    instance = package()
    replacements = {
        "NAME": "instance-name",
        "VERSION": [9, 9, 9],
        "REQUIRED_PYTHON": [9, 9],
    }
    shadowed: list[dict[str, Any]] = []
    for name in PACKAGE_MEMBERS:
        class_value = getattr(package, name)
        setattr(instance, name, replacements[name])
        shadowed.append(
            {
                "class_unchanged": getattr(package, name) is class_value,
                "instance_type": type(getattr(instance, name)).__name__,
                "name": name,
            }
        )
    return {
        "construction": _outcome(lambda: package().__class__.__name__),
        "keyword_argument": _outcome(lambda: package(value=1)),
        "positional_argument": _outcome(lambda: package(1)),
        "shadowed": shadowed,
    }


def _assign_item(value: Any, index: Any, replacement: Any) -> Any:
    value[index] = replacement
    return value[index]


def _package_name(module: ModuleType) -> dict[str, Any]:
    value = module.PackageInfo.NAME
    return {
        "concat": value + "-suffix",
        "equals_exact": value == "epsimple",
        "index": value[0],
        "item_assignment": _outcome(lambda: _assign_item(value, 0, "E")),
        "plus_integer": _outcome(lambda: value + 7),
        "replace": value.replace("simple", "SIMPLE"),
        "split": value.split("s"),
        "starts_with": value.startswith("ep"),
        "upper": value.upper(),
    }


def _package_version(module: ModuleType) -> dict[str, Any]:
    value = module.PackageInfo.VERSION
    return {
        "concat": list(value + (1,)),
        "equals_exact": value == (0, 7, 0),
        "index": value[1],
        "item_assignment": _outcome(lambda: _assign_item(value, 0, 1)),
        "join": ".".join(str(item) for item in value),
        "less_than_next": value < (0, 8, 0),
        "mixed_comparison": _outcome(lambda: value < "0.8.0"),
        "slice": list(value[1:]),
        "string_index": _outcome(lambda: value["0"]),
    }


def _required_python(module: ModuleType) -> dict[str, Any]:
    value = module.PackageInfo.REQUIRED_PYTHON
    return {
        "concat": list(value + (99,)),
        "equals_exact": value == (3, 12),
        "index": value[0],
        "item_assignment": _outcome(lambda: _assign_item(value, 0, 9)),
        "join_without_conversion": _outcome(lambda: ",".join(value)),
        "mixed_comparison": _outcome(lambda: value < "3.13"),
        "runtime_meets_requirement": sys.version_info[:2] >= value,
        "slice": list(value[:1]),
        "supports_3_11": (3, 11) >= value,
        "supports_3_13": (3, 13) >= value,
    }


def _execute_cases(
    module_a: ModuleType, module_b: ModuleType, root_a: Path, root_b: Path
) -> dict[str, dict[str, Any]]:
    return {
        EXPECTED_CASE_IDS[0]: _enum_topology(module_a.AUTOID_PREFIX),
        EXPECTED_CASE_IDS[1]: _enum_string_semantics(module_a.AUTOID_PREFIX),
        EXPECTED_CASE_IDS[2]: _enum_construction(module_a.AUTOID_PREFIX),
        EXPECTED_CASE_IDS[3]: _enum_format(module_a.AUTOID_PREFIX),
        EXPECTED_CASE_IDS[4]: _enum_direct_format(module_a.AUTOID_PREFIX),
        EXPECTED_CASE_IDS[5]: _enum_mutation(module_a.AUTOID_PREFIX),
        EXPECTED_CASE_IDS[6]: _directory_topology(module_a, root_a),
        EXPECTED_CASE_IDS[7]: _directory_relocation(module_a, module_b, root_a, root_b),
        EXPECTED_CASE_IDS[8]: _directory_mutation(module_a),
        EXPECTED_CASE_IDS[9]: _directory_instance(module_a),
        EXPECTED_CASE_IDS[10]: _package_topology(module_a),
        EXPECTED_CASE_IDS[11]: _package_mutation(module_a),
        EXPECTED_CASE_IDS[12]: _package_instance(module_a),
        EXPECTED_CASE_IDS[13]: _package_name(module_a),
        EXPECTED_CASE_IDS[14]: _package_version(module_a),
        EXPECTED_CASE_IDS[15]: _required_python(module_a),
        EXPECTED_CASE_IDS[16]: _enum_topology(module_a.SpecialTag),
        EXPECTED_CASE_IDS[17]: _enum_string_semantics(module_a.SpecialTag),
        EXPECTED_CASE_IDS[18]: _enum_construction(module_a.SpecialTag),
        EXPECTED_CASE_IDS[19]: _enum_format(module_a.SpecialTag),
        EXPECTED_CASE_IDS[20]: _enum_direct_format(module_a.SpecialTag),
        EXPECTED_CASE_IDS[21]: _enum_mutation(module_a.SpecialTag),
    }


def _expected_formats(members: tuple[tuple[str, str], ...], special: bool) -> list[dict[str, Any]]:
    specs = ("", "SURFACE", ":", "표면", " ")
    result: list[dict[str, Any]] = []
    for name, value in members:
        formats: list[dict[str, str]] = []
        for spec in specs:
            suffix = f":{spec}" if spec else ""
            formatted = f"${value}{suffix}$:" if special else f"{value}{suffix}-"
            formats.append({"result": formatted, "spec": spec})
        plain = f"${value}$:" if special else f"{value}-"
        result.append({"formats": formats, "name": name, "str": plain, "value": value})
    return result


def _all_outcomes(value: Any) -> Iterator[dict[str, Any]]:
    if isinstance(value, dict):
        if value.get("outcome") in {"returned", "raised"}:
            yield value
        for item in value.values():
            yield from _all_outcomes(item)
    elif isinstance(value, list):
        for item in value:
            yield from _all_outcomes(item)


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    _validate_safe_tree(facts, identifier)
    code = next(definition["code"] for definition in case_definitions() if definition["id"] == identifier)
    valid = True
    if code in {"A01", "S01"}:
        expected = AUTO_MEMBERS if code == "A01" else SPECIAL_MEMBERS
        valid = (
            facts["bases"] == ["str", "Enum"]
            and facts["class_signature"] == "(*values)"
            and [(item["name"], item["value"]) for item in facts["declared_members"]] == list(expected)
            and facts["iterated_names"] == [item[0] for item in expected]
            and facts["member_count"] == len(expected)
            and facts["unique_member_count"] == len(expected)
            and facts["alias_groups"] == []
        )
    elif code in {"A02", "S02"}:
        expected = AUTO_MEMBERS if code == "A02" else SPECIAL_MEMBERS
        valid = (
            [(item["name"], item["raw_value"]) for item in facts["members"]] == list(expected)
            and all(
                item["canonical_identity"]
                and item["equals_raw_value"]
                and item["hash_equals_raw_value"]
                and item["is_str_instance"]
                and item["raw_contains_token"]
                for item in facts["members"]
            )
        )
    elif code in {"A03", "S03"}:
        expected = AUTO_MEMBERS if code == "A03" else SPECIAL_MEMBERS
        valid = (
            [(item["name"], item["value"]) for item in facts["from_value"]] == list(expected)
            and all(item["same_member"] for item in facts["from_value"])
            and all(item["same_member"] for item in facts["from_name"])
            and all(item["observation"]["outcome"] == "raised" for item in facts["invalid"])
        )
    elif code in {"A04", "S04"}:
        valid = facts["members"] == _expected_formats(
            AUTO_MEMBERS if code == "A04" else SPECIAL_MEMBERS,
            code == "S04",
        )
    elif code in {"A05", "S05"}:
        valid = (
            facts["direct_format_int"]["outcome"] == "returned"
            and facts["direct_format_none"]["outcome"] == "returned"
            and facts["direct_format_object"]["outcome"] == "returned"
            and facts["direct_str_extra_argument"]["outcome"] == "raised"
            and facts["format_builtin_none"]["outcome"] == "raised"
        )
    elif code in {"A06", "S06"}:
        valid = (
            facts["alias_groups"] == []
            and facts["class_add_extra"]["outcome"] == "returned"
            and facts["member_add_extra"]["outcome"] == "returned"
            and facts["class_delete_member"]["outcome"] == "raised"
            and facts["class_reassign_member"]["outcome"] == "raised"
            and facts["member_set_name"]["outcome"] == "raised"
            and facts["member_set_value"]["outcome"] == "raised"
            and facts["shallow_copy_identity"]
            and facts["deepcopy_identity"]
        )
    elif code == "D01":
        valid = (
            facts["class_signature"] == "()"
            and facts["public_attribute_order"] == list(DIRECTORY_MEMBERS)
            and all(
                item["is_path"]
                and item["matches_role"]
                and (item["anchor"], item["suffix"]) == DIRECTORY_ROLES[name]
                for name, item in facts["state"].items()
            )
        )
    elif code == "D02":
        valid = (
            facts["class_identity_distinct"]
            and facts["relative_roles_equal"]
            and all(facts["public_absolute_values_distinct"].values())
        )
    elif code in {"D03", "P02"}:
        valid = all(
            item["assigned"]["equals_replacement"]
            and item["deleted_lookup"]["outcome"] == "raised"
            and item["restored_identity"]
            for item in facts["attributes"]
        )
    elif code in {"D04", "P03"}:
        valid = (
            facts["construction"]["outcome"] == "returned"
            and facts["keyword_argument"]["outcome"] == "raised"
            and facts["positional_argument"]["outcome"] == "raised"
            and all(item["class_unchanged"] for item in facts["shadowed"])
        )
    elif code == "P01":
        valid = facts == {
            "attribute_order": ["NAME", "VERSION", "REQUIRED_PYTHON"],
            "class_name": "PackageInfo",
            "class_signature": "()",
            "name": "epsimple",
            "name_type": "str",
            "required_python": [3, 12],
            "required_python_item_types": ["int", "int"],
            "required_python_type": "tuple",
            "version": [0, 7, 0],
            "version_item_types": ["int", "int", "int"],
            "version_type": "tuple",
        }
    elif code == "P04":
        valid = (
            facts["equals_exact"]
            and facts["upper"] == "EPSIMPLE"
            and facts["item_assignment"]["outcome"] == "raised"
            and facts["plus_integer"]["outcome"] == "raised"
        )
    elif code == "P05":
        valid = (
            facts["equals_exact"]
            and facts["join"] == "0.7.0"
            and facts["less_than_next"]
            and facts["item_assignment"]["outcome"] == "raised"
            and facts["mixed_comparison"]["outcome"] == "raised"
            and facts["string_index"]["outcome"] == "raised"
        )
    elif code == "P06":
        valid = (
            facts["equals_exact"]
            and facts["runtime_meets_requirement"]
            and not facts["supports_3_11"]
            and facts["supports_3_13"]
            and facts["item_assignment"]["outcome"] == "raised"
            and facts["join_without_conversion"]["outcome"] == "raised"
            and facts["mixed_comparison"]["outcome"] == "raised"
        )
    else:
        valid = False
    if not valid:
        raise RuntimeError(f"Identifier convention semantic invariant drifted: {identifier}")
    for outcome in _all_outcomes(facts):
        _require_keys(
            outcome,
            {"outcome", "result"} if outcome["outcome"] == "returned" else {"error", "outcome"},
            f"{identifier} outcome",
        )
        if outcome["outcome"] == "raised":
            _require_keys(outcome["error"], {"message", "type"}, f"{identifier} error")


def case_sha256(cases: list[dict[str, Any]]) -> dict[str, str]:
    return {item["id"]: canonical_sha256(item) for item in cases}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _expected_runtime() -> dict[str, Any]:
    return {
        "byteorder": "little",
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


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "isolated_import": {
            "files_after_execution": list(ISOLATED_FILES),
            "module_names": list(ISOLATED_MODULE_NAMES),
            "source_copy_sha256": {
                path: EXPECTED_SOURCE_SHA256 for path in ISOLATED_FILES
            },
        },
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }


def _expected_contract() -> dict[str, Any]:
    return {
        "adaptations": EXCEPTION_ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {"equivalent": 23, "exception": 11},
        "classifications": CLASSIFICATIONS,
        "closure": {
            "excluded_repr_indices": [26, 65],
            "excluded_repr_symbols": list(EXCLUDED_SYMBOLS),
            "target_count": 34,
            "target_indices": [item[0] for item in EXPECTED_TARGETS],
        },
        "native_routes": NATIVE_ROUTES,
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _validate_artifact_files() -> None:
    for path, expected_bytes, expected_hash in (
        (SUPPORT_PATH, EXPECTED_SUPPORT_BYTES, EXPECTED_SUPPORT_SHA256),
        (BOOTSTRAP_PATH, EXPECTED_BOOTSTRAP_BYTES, EXPECTED_BOOTSTRAP_SHA256),
    ):
        if path.stat().st_size != expected_bytes or sha256_file(path) != expected_hash:
            raise SystemExit(f"Pinned generator artifact drifted: {path.name}")


def build_oracle(
    inventory: dict[str, Any], commit: str, source: Path | None = None
) -> dict[str, Any]:
    imported_source = source.resolve() if source is not None else _find_pinned_source()
    if (
        imported_source.stat().st_size != EXPECTED_SOURCE_BYTES
        or sha256_file(imported_source) != EXPECTED_SOURCE_SHA256
    ):
        raise SystemExit("The imported epsimple/constants.py is not exactly pinned.")
    if commit != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    if inventory["content_sha256"] != EXPECTED_INVENTORY_SHA256:
        raise SystemExit("The aggregate inventory receipt is not exact.")
    _validate_artifact_files()

    with _isolated_modules(imported_source) as (root, module_a, module_b):
        root_a = root / "location-a"
        root_b = root / "location-b"
        observed = _execute_cases(module_a, module_b, root_a, root_b)
        if list(observed) != list(EXPECTED_CASE_IDS):
            raise RuntimeError("Observed case order drifted.")
        fact_hashes = {
            identifier: canonical_sha256(facts) for identifier, facts in observed.items()
        }
        if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
            raise SystemExit(
                "Pinned identifier convention facts drifted.\nOBSERVED_FACT_HASHES\n"
                + strict_json_dumps(fact_hashes, indent=2)
            )
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
                "Pinned identifier convention cases drifted.\nOBSERVED_CASE_HASHES\n"
                + strict_json_dumps(case_hashes, indent=2)
            )
        files_after = sorted(
            path.relative_to(root).as_posix()
            for path in root.rglob("*")
            if path.is_file()
        )
        if files_after != list(ISOLATED_FILES):
            raise SystemExit("Isolated import created an unexpected file.")

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
            "upstream": _expected_upstream(),
        }
    validate_oracle(result)
    return result


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "artifacts",
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
        },
        "root",
    )
    _validate_safe_tree(value)
    if value["schema"] != SCHEMA:
        raise RuntimeError("Identifier convention schema drifted.")
    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Identifier convention case order/count drifted.")
    by_id = {item["id"]: item for item in definitions}
    fact_hashes: dict[str, str] = {}
    for case in cases:
        definition = by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Case contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "facts_sha256", "outcome"}, "python")
        if case["python"]["outcome"] != "observed":
            raise RuntimeError(f"Python outcome drifted: {case['id']}")
        fact_hash = canonical_sha256(case["python"]["facts"])
        if case["python"]["facts_sha256"] != fact_hash:
            raise RuntimeError(f"Inline fact hash drifted: {case['id']}")
        fact_hashes[case["id"]] = fact_hash
        _validate_case_facts(case["id"], case["python"]["facts"])
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Expected fact hash pins drifted.")
    case_hashes = case_sha256(cases)
    if value["case_sha256"] != case_hashes:
        raise RuntimeError("Per-case hash map drifted.")
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Expected case hash pins drifted.")
    aggregate = cases_sha256(cases)
    if value["cases_sha256"] != aggregate:
        raise RuntimeError("Aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise RuntimeError("Expected aggregate case hash pin drifted.")

    if Counter(CLASSIFICATIONS.values()) != Counter({"equivalent": 23, "exception": 11}):
        raise RuntimeError("Identifier convention classification count drifted.")
    if value["consumer_contract"] != _expected_contract():
        raise RuntimeError("Identifier convention consumer contract drifted.")
    if value["artifacts"] != _expected_artifacts():
        raise RuntimeError("Identifier convention artifact pins drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Identifier convention runtime pins drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Identifier convention upstream pins drifted.")
    if value["symbols"] != [_descriptor(item) for item in value["target_receipts"]]:
        raise RuntimeError("Target symbol descriptors drifted.")
    if [item["inventory_index"] for item in value["target_receipts"]] != [
        item[0] for item in EXPECTED_TARGETS
    ]:
        raise RuntimeError("Target inventory indices drifted.")
    if [item["symbol"] for item in value["target_receipts"]] != list(TARGET_SYMBOLS):
        raise RuntimeError("Target receipt symbols drifted.")
    if [item["inventory_index"] for item in value["excluded_receipts"]] != [26, 65]:
        raise RuntimeError("Excluded repr receipts drifted.")
    if [item["symbol"] for item in value["excluded_receipts"]] != list(EXCLUDED_SYMBOLS):
        raise RuntimeError("Excluded repr symbols drifted.")
    strict_json_dumps(value)


def _validate_runtime() -> None:
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for isolated imports.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if sys.platform != REQUIRED_PLATFORM or struct.calcsize("P") * 8 != REQUIRED_POINTER_WIDTH_BITS:
        raise SystemExit("The pinned 64-bit Windows CPython runtime is required.")
    if sys.byteorder != "little":
        raise SystemExit("The pinned little-endian runtime is required.")


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
    print(f"Wrote EPlusSimple identifier conventions oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

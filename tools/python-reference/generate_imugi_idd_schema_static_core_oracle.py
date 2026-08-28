"""Generate the closed Imugi IDD/schema/static-container reference oracle.

This oracle executes exactly 21 zero-based public-symbol inventory rows from
``src/idragon/imugi.py``.  It records legacy CPython behavior for ``IDD``, the
three exception classes in this slice, and ``StaticIndexedDict``.  The complete
133-declaration Imugi source is partitioned into the 21 targets, the 40 batch-1
IDD-definition rows, 44 still-deferred rows, and the established 28 out-of-
scope rows.  Two byte-identical relocated imports make checkout location
irrelevant.

The Python process never loads .NET.  Public native routes and conservative
classifications are review metadata pinned to current production sources and
the immutable full EnergyPlus 24.2 IDD oracle.  A native parity test can later
consume this fixture without treating Python API or source shape as native
compatibility requirements.
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import inspect
from pathlib import Path
import sys
import tempfile
from typing import Any


BASE_GENERATOR_RECEIPT = {
    "bytes": 70_965,
    "path": "tools/python-reference/generate_imugi_idd_definitions_core_oracle.py",
    "sha256": "sha256:fa70dfc565a30542f58697cee512701356cf2200b3f07332de4e345f0b7b1398",
}


def _load_pinned_base() -> Any:
    repository_root = Path(__file__).resolve().parents[2]
    path = repository_root / BASE_GENERATOR_RECEIPT["path"]
    if (
        not path.is_file()
        or path.stat().st_size != BASE_GENERATOR_RECEIPT["bytes"]
        or "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()
        != BASE_GENERATOR_RECEIPT["sha256"]
    ):
        raise RuntimeError("Pinned Imugi batch-1 generator support drifted.")
    spec = importlib.util.spec_from_file_location(
        "_pinned_imugi_idd_definitions_core_oracle", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("Cannot load the pinned Imugi batch-1 generator.")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


_base = _load_pinned_base()

SCHEMA = "goniegonie.python-reference.imugi-idd-schema-static-core.v1"
PREFIX = "imugi-idd-schema-static-core."
SOURCE_PATH = _base.SOURCE_PATH
EXPECTED_UPSTREAM_COMMIT = _base.EXPECTED_UPSTREAM_COMMIT
EXPECTED_INVENTORY_BYTES = _base.EXPECTED_INVENTORY_BYTES
EXPECTED_INVENTORY_FILE_SHA256 = _base.EXPECTED_INVENTORY_FILE_SHA256
EXPECTED_INVENTORY_SHA256 = _base.EXPECTED_INVENTORY_SHA256
EXPECTED_SOURCE_BYTES = _base.EXPECTED_SOURCE_BYTES
EXPECTED_SOURCE_SHA256 = _base.EXPECTED_SOURCE_SHA256
EXPECTED_SOURCE_AST_SHA256 = _base.EXPECTED_SOURCE_AST_SHA256
SOURCE_SPECS = _base.SOURCE_SPECS
EXPECTED_DEPENDENCIES = _base.EXPECTED_DEPENDENCIES

sha256_file = _base.sha256_file
strict_json_dumps = _base.strict_json_dumps
canonical_sha256 = _base.canonical_sha256
load_json_without_duplicates_text = _base.load_json_without_duplicates_text
load_json_without_duplicates = _base.load_json_without_duplicates
DuplicateJsonKeyError = _base.DuplicateJsonKeyError
NonFiniteJsonConstantError = _base.NonFiniteJsonConstantError

TARGET_IDENTITIES = (
    (1095, "IDD"),
    (1097, "IDD.__init__"),
    (1100, "IDD.load"),
    (1101, "IDD.read_idd"),
    (1102, "IDD.reference_map_cls"),
    (1103, "IDD.reference_map_obj"),
    (1104, "IDD.referenced_map_obj"),
    (1105, "IDD.required_objects"),
    (1106, "IDD.to_pickle"),
    (1107, "IDD.version"),
    (1217, "InvalidFieldValue"),
    (1218, "InvalidParentManagement"),
    (1219, "StaticIndexedDict"),
    (1220, "StaticIndexedDict.__getitem__"),
    (1221, "StaticIndexedDict.__init__"),
    (1222, "StaticIndexedDict.__setitem__"),
    (1223, "StaticIndexedDict.allowed_keys"),
    (1224, "StaticIndexedDict.items"),
    (1225, "StaticIndexedDict.keys"),
    (1226, "StaticIndexedDict.values"),
    (1227, "VersionIdentificationError"),
)
TARGET_INDICES = tuple(index for index, _ in TARGET_IDENTITIES)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_IDENTITIES)

BATCH1_RESOLVED_IDENTITIES = tuple(_base.TARGET_IDENTITIES)
BATCH1_RESOLVED_INDICES = tuple(index for index, _ in BATCH1_RESOLVED_IDENTITIES)
OUT_OF_SCOPE_IDENTITIES = tuple(_base.OUT_OF_SCOPE_IDENTITIES)
OUT_OF_SCOPE_INDICES = tuple(index for index, _ in OUT_OF_SCOPE_IDENTITIES)
SOURCE_INDICES = tuple(range(1095, 1228))
DEFERRED_INDICES = tuple(
    index
    for index in SOURCE_INDICES
    if index
    not in set(TARGET_INDICES)
    | set(BATCH1_RESOLVED_INDICES)
    | set(OUT_OF_SCOPE_INDICES)
)
if (
    len(TARGET_INDICES) != 21
    or len(BATCH1_RESOLVED_INDICES) != 40
    or len(DEFERRED_INDICES) != 44
    or len(OUT_OF_SCOPE_INDICES) != 28
    or sorted(
        (
            *TARGET_INDICES,
            *BATCH1_RESOLVED_INDICES,
            *DEFERRED_INDICES,
            *OUT_OF_SCOPE_INDICES,
        )
    )
    != list(SOURCE_INDICES)
):
    raise RuntimeError("Imugi batch-2 full-source partition drifted.")

EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:8ba1afe1d26824fe0def879330816229feb65f9bf158e2fbc24072ae61ad6727"
)
EXPECTED_BATCH1_RESOLVED_RECEIPTS_SHA256 = (
    "sha256:cea1bdce699efee3b7f152d932f8dd1b52affe0ad139b642e3be2371446e5223"
)
EXPECTED_DEFERRED_RECEIPTS_SHA256 = (
    "sha256:e0f9739effa5d9ffafa3d1bec19fa57c338d8c76a2d730ba5833edb6401c7e1c"
)
EXPECTED_OUT_OF_SCOPE_RECEIPTS_SHA256 = (
    "sha256:3ad4f99816b0591241fe459bd60a0af70f9a40e497be34bab7b132ced2fe42da"
)

EQUIVALENT_INDICES = (
    1101,
    1102,
    1103,
    1105,
    1107,
    1223,
    1224,
    1225,
    1226,
)
CLASSIFICATIONS = {
    symbol: "equivalent" if index in EQUIVALENT_INDICES else "exception"
    for index, symbol in TARGET_IDENTITIES
}
EXCEPTION_SYMBOLS = {
    symbol
    for symbol, classification in CLASSIFICATIONS.items()
    if classification == "exception"
}

ADAPTATIONS = {
    "IDD": "typed-immutable-idd-schema-instead-of-mutable-user-dictionary",
    "IDD.__init__": "validated-immutable-schema-construction-with-explicit-source-identity",
    "IDD.load": "source-hash-bound-json-gzip-cache-instead-of-global-pickle-cache",
    "IDD.referenced_map_obj": "explicit-public-schema-projection-instead-of-absent-legacy-private-state",
    "IDD.to_pickle": "portable-json-gzip-cache-instead-of-arbitrary-python-pickle",
    "InvalidFieldValue": "standard-public-argument-and-format-exceptions-instead-of-legacy-marker-type",
    "InvalidParentManagement": "immutable-definition-ownership-instead-of-parent-mutation-exception",
    "StaticIndexedDict": "typed-immutable-schema-collections-instead-of-generic-mutable-user-dictionary",
    "StaticIndexedDict.__getitem__": "typed-case-insensitive-indexers-with-conventional-boundary-semantics",
    "StaticIndexedDict.__init__": "typed-schema-constructors-instead-of-allowed-key-user-dictionary",
    "StaticIndexedDict.__setitem__": "immutable-read-only-production-collections",
    "VersionIdentificationError": "empty-version-parser-result-instead-of-legacy-dedicated-exception",
}
if set(ADAPTATIONS) != EXCEPTION_SYMBOLS:
    raise RuntimeError("Imugi batch-2 adaptation coverage drifted.")

NATIVE_ROUTES = {
    "IDD": "GonieGonie.InvisibleDragon.Idd.IddSchema",
    "IDD.__init__": "GonieGonie.InvisibleDragon.Idd.IddSchema(...) constructor",
    "IDD.load": "GonieGonie.InvisibleDragon.Idd.IddSchemaCache.Read/TryRead",
    "IDD.read_idd": "GonieGonie.InvisibleDragon.Idd.IddParser.ParseFile",
    "IDD.reference_map_cls": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects/Fields/ReferenceClassNames projection",
    "IDD.reference_map_obj": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects/Fields/References projection",
    "IDD.referenced_map_obj": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects/Fields/ObjectLists projection",
    "IDD.required_objects": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects projection over IsRequired",
    "IDD.to_pickle": "GonieGonie.InvisibleDragon.Idd.IddSchemaCache.Write",
    "IDD.version": "GonieGonie.InvisibleDragon.Idd.IddSchema.Version",
    "InvalidFieldValue": "GonieGonie.InvisibleDragon.Idd.IddFieldDefinition/IddObjectDefinition public validation",
    "InvalidParentManagement": "GonieGonie.InvisibleDragon.Idd.IddObjectDefinition immutable public ownership",
    "StaticIndexedDict": "GonieGonie.InvisibleDragon.Idd.IddSchema and IddObjectDefinition typed collections",
    "StaticIndexedDict.__getitem__": "GonieGonie.InvisibleDragon.Idd.IddSchema.this[int|string]",
    "StaticIndexedDict.__init__": "GonieGonie.InvisibleDragon.Idd.IddSchema(...) constructor",
    "StaticIndexedDict.__setitem__": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects read-only collection",
    "StaticIndexedDict.allowed_keys": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects projection over Name",
    "StaticIndexedDict.items": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects key/value projection",
    "StaticIndexedDict.keys": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects projection over Name",
    "StaticIndexedDict.values": "GonieGonie.InvisibleDragon.Idd.IddSchema.Objects",
    "VersionIdentificationError": "GonieGonie.InvisibleDragon.Idd.IddParser.Parse/ParseFile version contract",
}
if set(NATIVE_ROUTES) != set(TARGET_SYMBOLS):
    raise RuntimeError("Imugi batch-2 public native-route coverage drifted.")

CASE_SPECS = (
    (
        "A01",
        "a-exception-types",
        "exception-types",
        ("InvalidFieldValue", "InvalidParentManagement", "VersionIdentificationError"),
    ),
    (
        "B01",
        "b-static-construction",
        "static-construction",
        ("StaticIndexedDict", "StaticIndexedDict.__init__", "StaticIndexedDict.allowed_keys"),
    ),
    (
        "C01",
        "c-static-index-read",
        "static-index-read",
        ("StaticIndexedDict.__getitem__",),
    ),
    (
        "D01",
        "d-static-index-write",
        "static-index-write",
        ("StaticIndexedDict.__setitem__",),
    ),
    (
        "E01",
        "e-static-views",
        "static-views",
        ("StaticIndexedDict.items", "StaticIndexedDict.keys", "StaticIndexedDict.values"),
    ),
    (
        "F01",
        "f-idd-construction-and-maps",
        "idd-construction-and-maps",
        (
            "IDD",
            "IDD.__init__",
            "IDD.reference_map_cls",
            "IDD.reference_map_obj",
            "IDD.referenced_map_obj",
            "IDD.required_objects",
            "IDD.version",
        ),
    ),
    ("G01", "g-idd-read", "idd-read", ("IDD.read_idd",)),
    ("H01", "h-idd-cache-roundtrip", "idd-cache", ("IDD.load", "IDD.to_pickle")),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 8

EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:6e6524357de9edd851713567c1d62da167fa0b666187e73ba731ead98342e091"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:b38033bf44c4359f5ee8cf44f8a12b2b267a2f4ddf83a25f0a13b5628b20f692"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:89b8c44c53fb90ecf4ae781d3cae69a37a3301277f933c0a65d3525130540166"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:3aa3f7403e3469566fc8f93a0ecbbd2e2e5fcffd8bfbd91e59aeca43a83aeb79"
)
EXPECTED_FACT_SHA256 = {
    "imugi-idd-schema-static-core.a-exception-types": "sha256:c63c81fd17ded5a68ac3854944aa1350dbbfc72da3f1d2dc15e8da87c4e2ae0b",
    "imugi-idd-schema-static-core.b-static-construction": "sha256:2f6eacc4845b2167b483323ac3b79fbb700470e1076bce6412d17f34f5dc6c91",
    "imugi-idd-schema-static-core.c-static-index-read": "sha256:0f36f0ddda42f4f16f42c3e66dfee804c43dace739350252f7b8d908661bff03",
    "imugi-idd-schema-static-core.d-static-index-write": "sha256:56a01263bb07d2cdf36f448f1d7f06c57cf3309614248cc09e7ca888751c1280",
    "imugi-idd-schema-static-core.e-static-views": "sha256:608cade9ddc207cc3dd6e3beb201e548b36fc1b6c82b33bdf84a49072ce4db0d",
    "imugi-idd-schema-static-core.f-idd-construction-and-maps": "sha256:d0470082dd01ad14251cbb80d511398ecb2893df1863077d75c96252e54b7e7c",
    "imugi-idd-schema-static-core.g-idd-read": "sha256:e6bd97600be399f9a5730f2a47ce1e12a683153e7f580cece54df79d087ea63a",
    "imugi-idd-schema-static-core.h-idd-cache-roundtrip": "sha256:80e7e21ee555890e28600b9ce811ddca07d3e3dddd5763549d6643b1a8871a22",
}
EXPECTED_CASE_SHA256 = {
    "imugi-idd-schema-static-core.a-exception-types": "sha256:fc066cfba1bdee780c09f706abcf65aaa144091d37f9d06cf4a3f52fd5dd2829",
    "imugi-idd-schema-static-core.b-static-construction": "sha256:76b6813bb3043fd424f339d53a8cc282beb0822ca19d2b263c5c76cb14f5330b",
    "imugi-idd-schema-static-core.c-static-index-read": "sha256:a38a3f22e3e6eaef4ee29b6e36c465fa1e56f5f75a82c762d128c998104ec2c7",
    "imugi-idd-schema-static-core.d-static-index-write": "sha256:6d6bf9f6a30d1b49305d4f1803db809c35ee3977d9492df658388b17f95450ea",
    "imugi-idd-schema-static-core.e-static-views": "sha256:a18061c3296ee6b2b1766cb01e9e6fa9f9be3ff8ea2c6be8cd9849bdbe1496f4",
    "imugi-idd-schema-static-core.f-idd-construction-and-maps": "sha256:b01247d21c80cf48840e0d3e5056f1de445d3ed237e7579de1fca90d1f34498c",
    "imugi-idd-schema-static-core.g-idd-read": "sha256:cfc1c957ba542371308e820d7e678410c8ddc91c7c5919086abbcd69f4cb3752",
    "imugi-idd-schema-static-core.h-idd-cache-roundtrip": "sha256:7a1bd5bd7109155fa45b37e3e8cdd23cc21f0ede4c162245b159aa625707c391",
}
EXPECTED_CASES_SHA256 = (
    "sha256:bb7a6f135116803da606049843a114d3ba3647ce4d0c6a63f144ab559bd821af"
)

NATIVE_SOURCE_RECEIPTS = (
    {
        "bytes": 13_005,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddDefinitions.cs",
        "sha256": "sha256:5e716db28821b68ae147ab0700380fdc6d406bb2666367903f3c12c2b54427ed",
    },
    {
        "bytes": 19_960,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddParser.cs",
        "sha256": "sha256:0f932fe250ca0e63b8734032abc34adf98c31ade16405caa547f5ac67c76823f",
    },
    {
        "bytes": 11_254,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Idd/IddSchemaCache.cs",
        "sha256": "sha256:80f2e2a803128b52aec6df95b0ff2567a5b53bd51e72b1154e7c9a8a3ebf9e4b",
    },
    {
        "bytes": 4_954,
        "path": "src/InvisibleDragon/GonieGonie.InvisibleDragon.Core/Common/EnergyPlusVersion.cs",
        "sha256": "sha256:ea908729f5517e3c9d301210f882019bc8b026da8e3055caeb187d80db86a685",
    },
    {
        "bytes": 8_339,
        "path": "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idd/IddParserTests.cs",
        "sha256": "sha256:783ff125aa66cd72afe67ef5c45b69bc208a7c7f9a9d04fe99a930d9ec7a1eaa",
    },
    {
        "bytes": 16_860,
        "path": "tests/InvisibleDragon/GonieGonie.InvisibleDragon.Core.Tests/Idd/IddSchemaOracleTests.cs",
        "sha256": "sha256:04d3a61e8c5d2a6bf7addc6900f5a8e0c2736005f90955f97641457cb27ea31f",
    },
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    # The pinned batch-1 loader verifies every byte/hash layer of the common
    # inventory and the exact Imugi declaration range before this slice reads it.
    _base.load_exact_inventory(path, upstream_commit)
    value = load_json_without_duplicates(path)
    source_rows = [
        {**item, "inventory_index": index}
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    if [item["inventory_index"] for item in source_rows] != list(SOURCE_INDICES):
        raise SystemExit("The Imugi declaration range drifted.")
    by_index = {item["inventory_index"]: item for item in source_rows}
    targets = [by_index[index] for index in TARGET_INDICES]
    batch1 = [by_index[index] for index in BATCH1_RESOLVED_INDICES]
    deferred = [by_index[index] for index in DEFERRED_INDICES]
    out_of_scope = [by_index[index] for index in OUT_OF_SCOPE_INDICES]
    identity_specs = (
        (targets, TARGET_IDENTITIES, "target"),
        (batch1, BATCH1_RESOLVED_IDENTITIES, "batch-1"),
        (out_of_scope, OUT_OF_SCOPE_IDENTITIES, "out-of-scope"),
    )
    for rows, expected, label in identity_specs:
        actual = [(item["inventory_index"], item["symbol"]) for item in rows]
        if actual != list(expected):
            raise SystemExit(f"The Imugi {label} identities drifted.")
    receipt_specs = (
        (targets, EXPECTED_TARGET_RECEIPTS_SHA256, "target"),
        (batch1, EXPECTED_BATCH1_RESOLVED_RECEIPTS_SHA256, "batch-1"),
        (deferred, EXPECTED_DEFERRED_RECEIPTS_SHA256, "deferred"),
        (out_of_scope, EXPECTED_OUT_OF_SCOPE_RECEIPTS_SHA256, "out-of-scope"),
    )
    for rows, expected_hash, label in receipt_specs:
        if expected_hash and canonical_sha256(rows) != expected_hash:
            raise SystemExit(f"The Imugi {label} receipt hash drifted.")
    all_indices = sorted(
        item["inventory_index"] for rows, _, _ in receipt_specs for item in rows
    )
    if all_indices != list(SOURCE_INDICES):
        raise RuntimeError("The Imugi batch-2 receipt partition is incomplete.")
    return {
        "batch1_resolved_receipts": batch1,
        "content_sha256": value["content_sha256"],
        "deferred_receipts": deferred,
        "out_of_scope_receipts": out_of_scope,
        "symbols": [_descriptor(item) for item in targets],
        "target_receipts": targets,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = tuple(
        {
            "code": code,
            "id": PREFIX + slug,
            "subfamily": subfamily,
            "target_symbols": list(symbols),
        }
        for code, slug, subfamily, symbols in CASE_SPECS
    )
    if tuple(item["id"] for item in definitions) != EXPECTED_CASE_IDS:
        raise RuntimeError("Imugi batch-2 case order drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Imugi batch-2 case IDs are not sorted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Imugi batch-2 cases do not exactly partition the targets.")
    return definitions


def _raise(error: Exception) -> None:
    raise error


def _safe_signature(value: Any) -> dict[str, Any]:
    try:
        return {"outcome": "available", "value": str(inspect.signature(value))}
    except (TypeError, ValueError) as error:
        return {
            "message": str(error),
            "outcome": "unavailable",
            "type": type(error).__name__,
        }


def _exception_class_shape(value: type[Exception]) -> dict[str, Any]:
    return {
        "abstract": inspect.isabstract(value),
        "abstract_methods": sorted(getattr(value, "__abstractmethods__", ())),
        "bases": [base.__name__ for base in value.__bases__],
        "mro": [item.__name__ for item in value.__mro__],
        "signature": _safe_signature(value),
    }


def _exception_facts(imugi: Any) -> dict[str, Any]:
    names = (
        "InvalidFieldValue",
        "InvalidParentManagement",
        "VersionIdentificationError",
    )
    return {
        "classes": {
            name: {
                "class_shape": _exception_class_shape(getattr(imugi, name)),
                "module": getattr(imugi, name).__module__,
                "subclass_exception": issubclass(getattr(imugi, name), Exception),
            }
            for name in names
        },
        "raised": {
            name: _base._attempt(
                lambda cls=getattr(imugi, name): _raise(cls("probe", 7))
            )
            for name in names
        },
        "types_are_distinct": len({getattr(imugi, name) for name in names}) == 3,
    }


def _new_static(imugi: Any) -> Any:
    return imugi.StaticIndexedDict(
        {"Alpha": 1, "Beta": 2}, allowed_keys=["Alpha", "Beta"]
    )


def _static_construction_facts(imugi: Any) -> dict[str, Any]:
    allowed = ["Alpha", "Beta"]
    value = imugi.StaticIndexedDict(
        {"Alpha": 1, "Beta": 2}, allowed_keys=allowed
    )
    allowed.append("Gamma")
    return {
        "allowed_keys_after_source_mutation": _base._encode(value.allowed_keys),
        "allowed_keys_identity_preserved": value.allowed_keys is allowed,
        "allowed_keys_property_assignment": _base._attempt(
            lambda: setattr(value, "allowed_keys", ("Other",))
        ),
        "class_shape": _base._class_shape(imugi.StaticIndexedDict),
        "constructor_signature": str(inspect.signature(imugi.StaticIndexedDict.__init__)),
        "initial_items": _base._encode(list(value.items())),
        "initial_unallowed_key": _base._attempt(
            lambda: imugi.StaticIndexedDict(
                {"Gamma": 3}, allowed_keys=["Alpha", "Beta"]
            )
        ),
        "non_string_allowed_key": _base._attempt(
            lambda: imugi.StaticIndexedDict(allowed_keys=[1])
        ),
    }


def _static_read_facts(imugi: Any) -> dict[str, Any]:
    value = _new_static(imugi)
    return {
        "bool_index": _base._encode(value[True]),
        "case_insensitive": _base._encode(value["aLpHa"]),
        "first_integer": _base._encode(value[0]),
        "index_at_count": _base._attempt(lambda: value[2]),
        "missing_string": _base._attempt(lambda: value["Missing"]),
        "negative_integer": _base._encode(value[-1]),
        "too_negative_integer": _base._attempt(lambda: value[-3]),
        "unsupported_key_type": _base._attempt(lambda: value[object()]),
    }


def _static_write_facts(imugi: Any) -> dict[str, Any]:
    value = _new_static(imugi)
    value["aLpHa"] = 10
    after_case = list(value.items())
    value[-1] = 20
    after_negative = list(value.items())
    return {
        "after_case_insensitive_write": _base._encode(after_case),
        "after_negative_integer_write": _base._encode(after_negative),
        "index_at_count": _base._attempt(lambda: value.__setitem__(2, 30)),
        "new_key_rejected": _base._attempt(
            lambda: value.__setitem__("Gamma", 30)
        ),
        "unsupported_key_type": _base._attempt(
            lambda: value.__setitem__([], 30)
        ),
    }


def _static_view_facts(imugi: Any) -> dict[str, Any]:
    value = _new_static(imugi)
    keys = value.keys()
    values = value.values()
    items = value.items()
    before = {
        "items": _base._encode(list(items)),
        "keys": _base._encode(list(keys)),
        "values": _base._encode(list(values)),
    }
    value["Alpha"] = 99
    return {
        "after_value_update": {
            "items": _base._encode(list(items)),
            "keys": _base._encode(list(keys)),
            "values": _base._encode(list(values)),
        },
        "before_value_update": before,
        "view_types": {
            "items": type(items).__name__,
            "keys": type(keys).__name__,
            "values": type(values).__name__,
        },
    }


def _sample_idd(imugi: Any) -> tuple[Any, Any, Any]:
    source_field = imugi.IddField(
        name="Name",
        is_required=True,
        default="Unnamed",
        reference=["NameReferences"],
        reference_cls=["SourceClasses"],
    )
    source_object = imugi.IddObject(
        source_field,
        name="Source:Object",
        index=["A1"],
        is_required=True,
    )
    target_field = imugi.IddField(
        name="Source Name", object_list=["NameReferences"]
    )
    target_object = imugi.IddObject(
        target_field,
        name="Target:Object",
        index=["A1"],
    )
    return (
        imugi.IDD(imugi.Version("24.2.0"), source_object, target_object),
        source_object,
        target_object,
    )


def _idd_construction_facts(imugi: Any) -> dict[str, Any]:
    value, source_object, target_object = _sample_idd(imugi)
    duplicate = imugi.IddObject(name="Source:Object", index=[])
    duplicate_idd = imugi.IDD(
        imugi.Version("24.2.0"), source_object, duplicate
    )
    return {
        "case_insensitive_object_lookup": value["source:object"] is source_object,
        "class_shape": _base._class_shape(imugi.IDD),
        "constructor_signature": str(inspect.signature(imugi.IDD.__init__)),
        "duplicate_object_resolution": {
            "allowed_keys": _base._encode(duplicate_idd.allowed_keys),
            "count": len(duplicate_idd),
            "stored_second_identity": duplicate_idd["Source:Object"] is duplicate,
        },
        "integer_object_lookup": value[1] is target_object,
        "reference_map_cls": _base._encode(dict(value.reference_map_cls)),
        "reference_map_obj": _base._encode(dict(value.reference_map_obj)),
        "referenceable_side_effect": _base._encode(
            source_object["Name"].referenceable
        ),
        "referenced_map_obj": _base._attempt(
            lambda: value.referenced_map_obj
        ),
        "required_objects": _base._encode(value.required_objects),
        "version": {
            "components": _base._encode(tuple(value.version)),
            "formatted_dot": f"{value.version:.}",
            "type": type(value.version).__name__,
        },
        "wrong_object_type": _base._attempt(
            lambda: imugi.IDD(imugi.Version("24.2.0"), "not-an-idd-object")
        ),
    }


def _idd_read_facts(imugi: Any) -> dict[str, Any]:
    representative = r"""!IDD_Version 24.2.0
\group Test Group
Version,
  \required-object
  A1;
    \field Version Identifier
    \required-field
    \default 24.2
"""
    with tempfile.TemporaryDirectory(prefix="imugi-idd-read-") as temporary:
        root = Path(temporary)
        valid_path = root / "Energy+.idd"
        valid_path.write_text(representative, encoding="utf-8", newline="\n")
        parsed = imugi.IDD.read_idd(str(valid_path), verbose=False)
        invalid_path = root / "missing-version.idd"
        invalid_path.write_text("Object,\n A1;\n", encoding="utf-8", newline="\n")
        invalid = _base._attempt(lambda: imugi.IDD.read_idd(str(invalid_path)))
    parsed_object = parsed[0]
    return {
        "invalid_version_marker": invalid,
        "parsed": {
            "field_count": len(parsed_object),
            "field_default": _base._encode(parsed_object[0].default),
            "field_name": parsed_object[0].name,
            "field_required": parsed_object[0].is_required,
            "object_count": len(parsed),
            "object_name": parsed_object.name,
            "required_objects": _base._encode(parsed.required_objects),
            "version": _base._encode(tuple(parsed.version)),
        },
        "signature": str(inspect.signature(imugi.IDD.read_idd)),
    }


def _canonical_to_pickle_signature(imugi: Any) -> dict[str, Any]:
    signature = inspect.signature(imugi.IDD.to_pickle)
    default = signature.parameters["save_dir"].default
    if default != imugi.Directory.IDD_DIR:
        raise RuntimeError("IDD.to_pickle default is not bound to Directory.IDD_DIR.")
    return {
        "default_binding": "idragon.constants.Directory.IDD_DIR",
        "default_type": type(default).__name__,
        "signature": "(self, save_dir: 'str' = <Directory.IDD_DIR>) -> 'None'",
    }


def _idd_cache_facts(imugi: Any) -> dict[str, Any]:
    value, _, _ = _sample_idd(imugi)
    to_pickle_signature = _canonical_to_pickle_signature(imugi)
    previous_directory = imugi.Directory.IDD_DIR
    previous_loaded = imugi.IDD.loaded
    try:
        with tempfile.TemporaryDirectory(prefix="imugi-idd-cache-") as temporary:
            value.to_pickle(save_dir=temporary)
            file_path = Path(temporary) / value.version._pyiddname
            imugi.Directory.IDD_DIR = temporary
            imugi.IDD.loaded = {}
            first = imugi.IDD.load("24.2.0")
            second = imugi.IDD.load((24, 2, 0))
            facts = {
                "cache_keys": _base._encode(list(imugi.IDD.loaded.keys())),
                "cached_identity": first is second,
                "file_bytes": file_path.stat().st_size,
                "file_name": file_path.name,
                "file_sha256": sha256_file(file_path),
                "loaded_object_names": _base._encode(list(first.keys())),
                "loaded_type": type(first).__name__,
                "loaded_version": _base._encode(tuple(first.version)),
                "roundtrip_is_distinct_instance": first is not value,
                "to_pickle_signature": to_pickle_signature,
                "load_signature": str(inspect.signature(imugi.IDD.load)),
            }
    finally:
        imugi.Directory.IDD_DIR = previous_directory
        imugi.IDD.loaded = previous_loaded
    return facts


def _execute_cases(imugi: Any) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _exception_facts(imugi),
        EXPECTED_CASE_IDS[1]: _static_construction_facts(imugi),
        EXPECTED_CASE_IDS[2]: _static_read_facts(imugi),
        EXPECTED_CASE_IDS[3]: _static_write_facts(imugi),
        EXPECTED_CASE_IDS[4]: _static_view_facts(imugi),
        EXPECTED_CASE_IDS[5]: _idd_construction_facts(imugi),
        EXPECTED_CASE_IDS[6]: _idd_read_facts(imugi),
        EXPECTED_CASE_IDS[7]: _idd_cache_facts(imugi),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("Imugi batch-2 observation order drifted.")
    return observations


def _runtime_signatures(imugi: Any) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for symbol in TARGET_SYMBOLS:
        try:
            result[symbol] = _base._resolve_descriptor(imugi, symbol)
        except (TypeError, ValueError) as error:
            owner = getattr(imugi, symbol)
            if not inspect.isclass(owner):
                raise
            result[symbol] = {
                "abstract": inspect.isabstract(owner),
                "kind": "class",
                "module": owner.__module__,
                "qualname": owner.__qualname__,
                "signature": {
                    "message": str(error),
                    "outcome": "unavailable",
                    "type": type(error).__name__,
                },
            }
        if symbol == "IDD.to_pickle":
            result[symbol]["signature"] = _canonical_to_pickle_signature(imugi)
    return result


def _native_review() -> dict[str, Any]:
    repository_root = Path(__file__).resolve().parents[2]
    if (
        not (repository_root / BASE_GENERATOR_RECEIPT["path"]).is_file()
        or sha256_file(repository_root / BASE_GENERATOR_RECEIPT["path"])
        != BASE_GENERATOR_RECEIPT["sha256"]
    ):
        raise RuntimeError("Pinned batch-1 support receipt drifted.")
    for receipt in NATIVE_SOURCE_RECEIPTS:
        path = repository_root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise RuntimeError(f"Reviewed native source drifted: {receipt['path']}")
    result = {
        "classification_sha256": canonical_sha256(CLASSIFICATIONS),
        "public_production_routes_only": True,
        "python_api_compatibility_claimed": False,
        "python_executes_native_runtime": False,
        "python_source_compatibility_claimed": False,
        "routes_sha256": canonical_sha256(NATIVE_ROUTES),
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        "source_receipts_sha256": canonical_sha256(list(NATIVE_SOURCE_RECEIPTS)),
    }
    digest = canonical_sha256(result)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise RuntimeError("Pinned Imugi batch-2 native review drifted.")
    return result


def _assertion_ids(receipts: list[dict[str, Any]]) -> dict[str, str]:
    return {
        item["symbol"]: (
            f"imugi-idd-schema-static-core-{item['inventory_index']}-"
            f"{item['symbol_hash'][7:15]}"
        )
        for item in receipts
    }


def _coverage_by_symbol() -> dict[str, str]:
    result: dict[str, str] = {}
    for definition in case_definitions():
        for symbol in definition["target_symbols"]:
            result[symbol] = definition["id"]
    if set(result) != set(TARGET_SYMBOLS):
        raise RuntimeError("Imugi batch-2 target coverage drifted.")
    return result


def _expected_contract(
    receipts: list[dict[str, Any]], signatures: dict[str, Any]
) -> dict[str, Any]:
    assertion_ids = _assertion_ids(receipts)
    classification_counts = Counter(CLASSIFICATIONS.values())
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": assertion_ids,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {
            "equivalent": classification_counts["equivalent"],
            "exception": classification_counts["exception"],
        },
        "classifications": CLASSIFICATIONS,
        "closure": {
            "batch1_resolved_count": len(BATCH1_RESOLVED_INDICES),
            "batch1_resolved_indices": list(BATCH1_RESOLVED_INDICES),
            "deferred_count": len(DEFERRED_INDICES),
            "deferred_indices": list(DEFERRED_INDICES),
            "exact_one_case_target_partition": True,
            "full_imugi_source_partition": True,
            "matrix_batch1_promotion_deferred": True,
            "out_of_scope_count": len(OUT_OF_SCOPE_INDICES),
            "out_of_scope_indices": list(OUT_OF_SCOPE_INDICES),
            "source_declaration_count": len(SOURCE_INDICES),
            "target_count": len(TARGET_INDICES),
            "target_indices": list(TARGET_INDICES),
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "coverage_by_symbol": _coverage_by_symbol(),
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "exact_cpython_behavior_oracle": True,
            "expected_receipt_count": len(TARGET_INDICES),
            "full_energyplus_idd_support_hash_pinned": True,
            "native_runtime_executed_by_python_oracle": False,
            "path_independent_relocated_import": True,
            "structural_only": False,
            "target_coverage_complete": True,
        },
        "expectations": {
            symbol: {
                "adaptation": ADAPTATIONS.get(symbol, "not_applicable"),
                "assertion_id": assertion_ids[symbol],
                "classification": CLASSIFICATIONS[symbol],
                "native_route": NATIVE_ROUTES[symbol],
            }
            for symbol in TARGET_SYMBOLS
        },
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
    imported_root = (
        source_root.resolve() if source_root is not None else _base._find_pinned_source_root()
    )
    work_root = (
        Path(__file__).resolve().parents[2]
        / "temp"
        / "reference"
        / "imugi-idd-schema-static-core-work"
    )
    with _base._isolated_import(imported_root, work_root, "location-one-") as primary:
        signatures = _runtime_signatures(primary.imugi)
        observations = _execute_cases(primary.imugi)
        loaded_modules = primary.loaded_local_modules
    with _base._isolated_import(imported_root, work_root, "location-two-") as relocated:
        relocated_signatures = _runtime_signatures(relocated.imugi)
        relocated_observations = _execute_cases(relocated.imugi)
        relocated_modules = relocated.loaded_local_modules
    if signatures != relocated_signatures:
        raise RuntimeError(
            "Imugi batch-2 signatures changed after relocation.\nprimary="
            + strict_json_dumps(signatures, indent=2)
            + "\nrelocated="
            + strict_json_dumps(relocated_signatures, indent=2)
        )
    if observations != relocated_observations:
        raise RuntimeError("Imugi batch-2 observations changed after relocation.")
    if loaded_modules != relocated_modules:
        raise RuntimeError("Imugi batch-2 loaded modules changed after relocation.")

    signatures_hash = canonical_sha256(signatures)
    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    pinned_hashes = (
        (signatures_hash, EXPECTED_RUNTIME_SIGNATURES_SHA256, "runtime signatures"),
        (modules_hash, EXPECTED_LOADED_LOCAL_MODULES_SHA256, "loaded modules"),
        (relocation_hash, EXPECTED_RELOCATED_OBSERVATIONS_SHA256, "relocation"),
    )
    for actual, expected, label in pinned_hashes:
        if expected and actual != expected:
            raise SystemExit(f"Pinned Imugi batch-2 {label} drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned Imugi batch-2 fact hashes drifted.\n"
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
            "Pinned Imugi batch-2 case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned Imugi batch-2 case aggregate drifted.")

    receipt_hashes = {
        "batch1_resolved": canonical_sha256(inventory["batch1_resolved_receipts"]),
        "deferred": canonical_sha256(inventory["deferred_receipts"]),
        "out_of_scope": canonical_sha256(inventory["out_of_scope_receipts"]),
        "target": canonical_sha256(inventory["target_receipts"]),
    }
    result = {
        "batch1_resolved_receipts": inventory["batch1_resolved_receipts"],
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(inventory["target_receipts"], signatures),
        "deferred_receipts": inventory["deferred_receipts"],
        "fact_sha256": fact_hashes,
        "native_review": _native_review(),
        "out_of_scope_receipts": inventory["out_of_scope_receipts"],
        "runtime": _base._runtime_receipt(),
        "schema": SCHEMA,
        "support": {
            "base_generator": BASE_GENERATOR_RECEIPT,
            "energyplus_idd": _base._support_receipt(),
        },
        "symbols": inventory["symbols"],
        "target_receipts": inventory["target_receipts"],
        "upstream": {
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "inventory": {
                "bytes": EXPECTED_INVENTORY_BYTES,
                "content_sha256": EXPECTED_INVENTORY_SHA256,
                "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
            },
            "isolated_import": {
                "loaded_local_modules": loaded_modules,
                "loaded_local_modules_sha256": modules_hash,
                "relocated_observations_sha256": relocation_hash,
                "relocated_source_copy": "two-byte-identical-repository-temp-copies",
                "source_location_count": 2,
            },
            "partition_receipts_sha256": receipt_hashes,
            "source": {
                "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
                "bytes": EXPECTED_SOURCE_BYTES,
                "path": SOURCE_PATH,
                "source_sha256": EXPECTED_SOURCE_SHA256,
            },
        },
    }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def validate_oracle(value: dict[str, Any]) -> None:
    _require_keys(
        value,
        {
            "batch1_resolved_receipts",
            "case_sha256",
            "cases",
            "cases_sha256",
            "consumer_contract",
            "deferred_receipts",
            "fact_sha256",
            "native_review",
            "out_of_scope_receipts",
            "runtime",
            "schema",
            "support",
            "symbols",
            "target_receipts",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Imugi batch-2 schema drifted.")
    receipt_specs = (
        (
            value["target_receipts"],
            EXPECTED_TARGET_RECEIPTS_SHA256,
            TARGET_IDENTITIES,
            "target",
        ),
        (
            value["batch1_resolved_receipts"],
            EXPECTED_BATCH1_RESOLVED_RECEIPTS_SHA256,
            BATCH1_RESOLVED_IDENTITIES,
            "batch-1",
        ),
        (
            value["deferred_receipts"],
            EXPECTED_DEFERRED_RECEIPTS_SHA256,
            None,
            "deferred",
        ),
        (
            value["out_of_scope_receipts"],
            EXPECTED_OUT_OF_SCOPE_RECEIPTS_SHA256,
            OUT_OF_SCOPE_IDENTITIES,
            "out-of-scope",
        ),
    )
    for receipts, expected_hash, expected_identities, label in receipt_specs:
        if not isinstance(receipts, list):
            raise RuntimeError(f"Imugi {label} receipts are not an array.")
        if expected_hash and canonical_sha256(receipts) != expected_hash:
            raise RuntimeError(f"Imugi {label} receipts drifted.")
        if expected_identities is not None and [
            (item["inventory_index"], item["symbol"]) for item in receipts
        ] != list(expected_identities):
            raise RuntimeError(f"Imugi {label} receipt identities drifted.")
    if value["symbols"] != [
        _descriptor(item) for item in value["target_receipts"]
    ]:
        raise RuntimeError("Imugi batch-2 symbol descriptors drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("Imugi batch-2 runtime signatures are absent.")
    if (
        EXPECTED_RUNTIME_SIGNATURES_SHA256
        and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256
    ):
        raise RuntimeError("Pinned Imugi batch-2 runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(
        value["target_receipts"], signatures
    ):
        raise RuntimeError("Imugi batch-2 consumer contract drifted.")
    if value["runtime"] != _base._runtime_receipt():
        raise RuntimeError("Imugi batch-2 runtime receipt drifted.")
    if value["support"] != {
        "base_generator": BASE_GENERATOR_RECEIPT,
        "energyplus_idd": _base._support_receipt(),
    }:
        raise RuntimeError("Imugi batch-2 support receipt drifted.")
    if value["native_review"] != _native_review():
        raise RuntimeError("Imugi batch-2 native review drifted.")

    upstream = value["upstream"]
    _require_keys(
        upstream,
        {
            "commit",
            "inventory",
            "isolated_import",
            "partition_receipts_sha256",
            "source",
        },
        "upstream",
    )
    expected_partition_hashes = {
        "batch1_resolved": canonical_sha256(value["batch1_resolved_receipts"]),
        "deferred": canonical_sha256(value["deferred_receipts"]),
        "out_of_scope": canonical_sha256(value["out_of_scope_receipts"]),
        "target": canonical_sha256(value["target_receipts"]),
    }
    expected_static = {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "partition_receipts_sha256": expected_partition_hashes,
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }
    for key, expected in expected_static.items():
        if upstream[key] != expected:
            raise RuntimeError(f"Imugi batch-2 upstream field drifted: {key}")
    isolated = upstream["isolated_import"]
    _require_keys(
        isolated,
        {
            "loaded_local_modules",
            "loaded_local_modules_sha256",
            "relocated_observations_sha256",
            "relocated_source_copy",
            "source_location_count",
        },
        "isolated_import",
    )
    if (
        isolated["source_location_count"] != 2
        or isolated["relocated_source_copy"]
        != "two-byte-identical-repository-temp-copies"
        or isolated["loaded_local_modules_sha256"]
        != canonical_sha256(isolated["loaded_local_modules"])
    ):
        raise RuntimeError("Imugi batch-2 relocation contract drifted.")
    if (
        EXPECTED_LOADED_LOCAL_MODULES_SHA256
        and isolated["loaded_local_modules_sha256"]
        != EXPECTED_LOADED_LOCAL_MODULES_SHA256
    ):
        raise RuntimeError("Pinned Imugi batch-2 loaded modules drifted.")
    if (
        EXPECTED_RELOCATED_OBSERVATIONS_SHA256
        and isolated["relocated_observations_sha256"]
        != EXPECTED_RELOCATED_OBSERVATIONS_SHA256
    ):
        raise RuntimeError("Pinned Imugi batch-2 relocation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Imugi batch-2 case order/count drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        _require_keys(case, {*definition, "python"}, f"case {definition['id']}")
        if any(case[key] != expected for key, expected in definition.items()):
            raise RuntimeError(f"Imugi batch-2 case drifted: {definition['id']}")
        python = case["python"]
        _require_keys(python, {"facts", "facts_sha256", "outcome"}, "python")
        if python["outcome"] != "observed":
            raise RuntimeError(f"Imugi batch-2 outcome drifted: {definition['id']}")
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(f"Imugi batch-2 inline facts drifted: {definition['id']}")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Imugi batch-2 fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned Imugi batch-2 fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("Imugi batch-2 case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned Imugi batch-2 case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Imugi batch-2 case aggregate drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned Imugi batch-2 case aggregate hash drifted.")

    all_receipt_indices = sorted(
        item["inventory_index"]
        for key in (
            "target_receipts",
            "batch1_resolved_receipts",
            "deferred_receipts",
            "out_of_scope_receipts",
        )
        for item in value[key]
    )
    closure = value["consumer_contract"]["closure"]
    if (
        closure["target_indices"] != list(TARGET_INDICES)
        or closure["batch1_resolved_indices"] != list(BATCH1_RESOLVED_INDICES)
        or closure["deferred_indices"] != list(DEFERRED_INDICES)
        or closure["out_of_scope_indices"] != list(OUT_OF_SCOPE_INDICES)
        or all_receipt_indices != list(SOURCE_INDICES)
    ):
        raise RuntimeError("Imugi batch-2 full-source partition drifted.")
    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Imugi batch-2 target closure drifted.")
    _base._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Imugi batch-2 strict JSON round trip drifted.")


def _validate_generation_runtime() -> None:
    _base._validate_generation_runtime()


def main() -> int:
    args = parse_args()
    _validate_generation_runtime()
    source_root = _base._find_pinned_source_root()
    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    oracle = build_oracle(inventory, args.upstream_commit, source_root)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(oracle, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())

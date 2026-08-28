"""Generate pinned observations for legacy Imugi IDD definition semantics.

This bounded oracle executes the 40 unresolved ``IddField`` and ``IddObject``
declarations in ``src/idragon/imugi.py``.  Every one of the module's 133 public
declarations is retained in one of three disjoint, hash-pinned receipt sets:
the 40 targets, 65 declarations deferred to later Imugi slices, and 28 symbols
that are explicitly outside the compatibility surface.  The upstream module
is imported from two byte-identical relocated copies below ``temp`` so that no
observation can depend on the checkout path.

The Python observations intentionally do not execute .NET code.  Native routes
and classifications are backed by hash-pinned production sources and the full
EnergyPlus 24.2 IDD regression oracle; a later native parity test consumes this
fixture and closes that half of the evidence chain.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import gzip
import hashlib
import importlib
import importlib.metadata
import inspect
import json
import math
import os
from pathlib import Path
import re
import shutil
import sys
import tempfile
from types import ModuleType, SimpleNamespace
from typing import Any, Iterator


SCHEMA = "goniegonie.python-reference.imugi-idd-definitions-core.v1"
SOURCE_PATH = "src/idragon/imugi.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_BYTES = 518_070
EXPECTED_INVENTORY_FILE_SHA256 = (
    "sha256:182ee3c169f7d5fd5ae6c12746a21ed1615a16575920bb45eb1bd8059832f2e3"
)
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_BYTES = 91_815
EXPECTED_SOURCE_SHA256 = (
    "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90"
)

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
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

SOURCE_SPECS = (
    {
        "ast_sha256": "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9",
        "bytes": 6_247,
        "module": "idragon.common",
        "path": "src/idragon/common.py",
        "source_sha256": "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d",
    },
    {
        "ast_sha256": "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084",
        "bytes": 2_590,
        "module": "idragon.constants",
        "path": "src/idragon/constants.py",
        "source_sha256": "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520",
    },
    {
        "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
        "bytes": EXPECTED_SOURCE_BYTES,
        "module": "idragon.imugi",
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    },
    {
        "ast_sha256": "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e",
        "bytes": 12_367,
        "module": "idragon.launcher",
        "path": "src/idragon/launcher.py",
        "source_sha256": "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f",
    },
)

TARGET_IDENTITIES = (
    (1123, "IddField"),
    (1124, "IddField.__eq__"),
    (1125, "IddField.__init__"),
    (1128, "IddField.default"),
    (1129, "IddField.external_list"),
    (1130, "IddField.from_text"),
    (1131, "IddField.is_autocalculatable"),
    (1132, "IddField.is_autosizable"),
    (1133, "IddField.is_deprecated"),
    (1134, "IddField.is_extensible"),
    (1135, "IddField.is_required"),
    (1136, "IddField.is_retaincase"),
    (1137, "IddField.key"),
    (1138, "IddField.maximum"),
    (1139, "IddField.memo"),
    (1140, "IddField.minimum"),
    (1141, "IddField.name"),
    (1142, "IddField.object_list"),
    (1143, "IddField.reference"),
    (1144, "IddField.reference_cls"),
    (1145, "IddField.referenceable"),
    (1146, "IddField.type"),
    (1147, "IddField.unit"),
    (1148, "IddObject"),
    (1149, "IddObject.__eq__"),
    (1150, "IddObject.__init__"),
    (1153, "IddObject.begin_extensible"),
    (1154, "IddObject.default"),
    (1155, "IddObject.extensible"),
    (1156, "IddObject.format"),
    (1157, "IddObject.from_text"),
    (1158, "IddObject.idd_index"),
    (1159, "IddObject.is_obsolete"),
    (1160, "IddObject.is_required"),
    (1161, "IddObject.is_unique"),
    (1162, "IddObject.memo"),
    (1163, "IddObject.min_fields"),
    (1164, "IddObject.name"),
    (1165, "IddObject.reference"),
    (1166, "IddObject.required_fields"),
)
TARGET_INDICES = tuple(index for index, _ in TARGET_IDENTITIES)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_IDENTITIES)

OUT_OF_SCOPE_IDENTITIES = (
    (1096, "IDD.__eq__"),
    (1098, "IDD.__repr__"),
    (1099, "IDD.__str__"),
    (1110, "IDF.__len__"),
    (1111, "IDF.__repr__"),
    (1117, "IDF.quick_map"),
    (1120, "IDF.shrink"),
    (1126, "IddField.__repr__"),
    (1127, "IddField.__str__"),
    (1151, "IddObject.__repr__"),
    (1152, "IddObject.__str__"),
    (1168, "IdfObject.__deepcopy__"),
    (1169, "IdfObject.__eq__"),
    (1172, "IdfObject.__repr__"),
    (1184, "IdfObjectLinkedDataFrame"),
    (1185, "IdfObjectLinkedDataFrame.__enter__"),
    (1186, "IdfObjectLinkedDataFrame.__exit__"),
    (1187, "IdfObjectLinkedDataFrame.__init__"),
    (1188, "IdfObjectLinkedDataFrame.columns"),
    (1189, "IdfObjectLinkedDataFrame.linked"),
    (1191, "IdfObjectList.__add__"),
    (1192, "IdfObjectList.__deepcopy__"),
    (1193, "IdfObjectList.__eq__"),
    (1196, "IdfObjectList.__repr__"),
    (1200, "IdfObjectList.as_dataframe"),
    (1202, "IdfObjectList.clear"),
    (1213, "IdfObjectList.pop"),
    (1216, "IdfObjectList.to_dataframe"),
)
OUT_OF_SCOPE_INDICES = tuple(index for index, _ in OUT_OF_SCOPE_IDENTITIES)
SOURCE_INDICES = tuple(range(1095, 1228))
DEFERRED_INDICES = tuple(
    index
    for index in SOURCE_INDICES
    if index not in set(TARGET_INDICES) | set(OUT_OF_SCOPE_INDICES)
)
if (
    len(TARGET_INDICES) != 40
    or len(DEFERRED_INDICES) != 65
    or len(OUT_OF_SCOPE_INDICES) != 28
    or sorted((*TARGET_INDICES, *DEFERRED_INDICES, *OUT_OF_SCOPE_INDICES))
    != list(SOURCE_INDICES)
):
    raise RuntimeError("Imugi IDD definition source partition drifted.")

EXPECTED_TARGET_RECEIPTS_SHA256 = (
    "sha256:cea1bdce699efee3b7f152d932f8dd1b52affe0ad139b642e3be2371446e5223"
)
EXPECTED_DEFERRED_RECEIPTS_SHA256 = (
    "sha256:61f4342d8b5391b714de9ae1a37d505ed58d169d13fb1c739bac607c54056c96"
)
EXPECTED_OUT_OF_SCOPE_RECEIPTS_SHA256 = (
    "sha256:3ad4f99816b0591241fe459bd60a0af70f9a40e497be34bab7b132ced2fe42da"
)

EQUIVALENT_INDICES = (
    1129,
    1131,
    1132,
    1133,
    1134,
    1135,
    1136,
    1137,
    1141,
    1142,
    1143,
    1144,
    1155,
    1156,
    1160,
    1161,
    1163,
    1164,
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


def _adaptation_reason(symbol: str) -> str:
    reasons = {
        "IddField": "typed-immutable-field-definition",
        "IddField.__eq__": "field-by-field-structural-parity-without-value-equality-override",
        "IddField.__init__": "token-position-kind-explicit-validated-construction",
        "IddField.default": "lossless-string-default-instead-of-legacy-numeric-coercion",
        "IddField.from_text": "full-schema-parser-route-instead-of-field-fragment-parser",
        "IddField.maximum": "explicit-inclusive-bound-instead-of-nextafter-sentinel",
        "IddField.memo": "ordered-note-list-instead-of-formatted-sentence-string",
        "IddField.minimum": "explicit-inclusive-bound-instead-of-nextafter-sentinel",
        "IddField.referenceable": "schema-projection-instead-of-mutable-backreference-list",
        "IddField.type": "closed-idd-data-type-enum-with-kind-derived-default",
        "IddField.unit": "separate-units-ip-units-and-units-based-on-field-metadata",
        "IddObject": "typed-immutable-object-definition",
        "IddObject.__eq__": "field-by-field-structural-parity-without-value-equality-override",
        "IddObject.__init__": "ordered-consecutive-field-definition-construction",
        "IddObject.begin_extensible": "resolved-zero-based-extensible-start-index",
        "IddObject.default": "field-default-projection-instead-of-cached-list",
        "IddObject.from_text": "full-schema-parser-route-instead-of-object-fragment-parser",
        "IddObject.idd_index": "ordered-field-token-projection",
        "IddObject.is_obsolete": "obsolete-message-preservation-instead-of-boolean-only",
        "IddObject.memo": "ordered-memo-list-instead-of-formatted-sentence-string",
        "IddObject.reference": "additional-directive-preservation-for-reference-class-name",
        "IddObject.required_fields": "required-field-definition-projection-instead-of-cached-name-list",
    }
    return reasons[symbol]


ADAPTATIONS = {
    symbol: (
        f"{_adaptation_reason(symbol)}-"
        f"{next(index for index, candidate in TARGET_IDENTITIES if candidate == symbol)}"
    )
    for symbol in EXCEPTION_SYMBOLS
}


def _native_route(symbol: str) -> str:
    prefix = "GonieGonie.InvisibleDragon.Idd."
    field_routes = {
        "IddField": "IddFieldDefinition",
        "IddField.__eq__": "IddFieldDefinition public properties (structural comparison)",
        "IddField.__init__": "IddFieldDefinition(...) constructor",
        "IddField.default": "IddFieldDefinition.DefaultValue",
        "IddField.external_list": "IddFieldDefinition.ExternalList",
        "IddField.from_text": "IddParser.Parse(...).Objects[0].Fields[0]",
        "IddField.is_autocalculatable": "IddFieldDefinition.IsAutocalculatable",
        "IddField.is_autosizable": "IddFieldDefinition.IsAutosizable",
        "IddField.is_deprecated": "IddFieldDefinition.IsDeprecated",
        "IddField.is_extensible": "IddFieldDefinition.BeginsExtensible",
        "IddField.is_required": "IddFieldDefinition.IsRequired",
        "IddField.is_retaincase": "IddFieldDefinition.RetainsCase",
        "IddField.key": "IddFieldDefinition.Choices",
        "IddField.maximum": "IddFieldDefinition.Maximum",
        "IddField.memo": "IddFieldDefinition.Notes",
        "IddField.minimum": "IddFieldDefinition.Minimum",
        "IddField.name": "IddFieldDefinition.Name",
        "IddField.object_list": "IddFieldDefinition.ObjectLists",
        "IddField.reference": "IddFieldDefinition.References",
        "IddField.reference_cls": "IddFieldDefinition.ReferenceClassNames",
        "IddField.referenceable": "IddSchema.Objects projection over IddFieldDefinition.References",
        "IddField.type": "IddFieldDefinition.DataType",
        "IddField.unit": "IddFieldDefinition.Units and UnitsBasedOnField",
    }
    object_routes = {
        "IddObject": "IddObjectDefinition",
        "IddObject.__eq__": "IddObjectDefinition public properties (structural comparison)",
        "IddObject.__init__": "IddObjectDefinition(...) constructor",
        "IddObject.begin_extensible": "IddObjectDefinition.ExtensibleStartIndex",
        "IddObject.default": "IddObjectDefinition.Fields projection over DefaultValue",
        "IddObject.extensible": "IddObjectDefinition.ExtensibleGroupSize",
        "IddObject.format": "IddObjectDefinition.Format",
        "IddObject.from_text": "IddParser.Parse(...).Objects[0]",
        "IddObject.idd_index": "IddObjectDefinition.Fields projection over Token",
        "IddObject.is_obsolete": "IddObjectDefinition.ObsoleteMessage",
        "IddObject.is_required": "IddObjectDefinition.IsRequired",
        "IddObject.is_unique": "IddObjectDefinition.IsUnique",
        "IddObject.memo": "IddObjectDefinition.Memo",
        "IddObject.min_fields": "IddObjectDefinition.MinimumFields",
        "IddObject.name": "IddObjectDefinition.Name",
        "IddObject.reference": "IddObjectDefinition.AdditionalDirectives",
        "IddObject.required_fields": "IddObjectDefinition.Fields projection over IsRequired",
    }
    route = {**field_routes, **object_routes}.get(symbol)
    if route is None:
        raise RuntimeError(f"No public native route for {symbol}.")
    return prefix + route


NATIVE_ROUTES = {symbol: _native_route(symbol) for symbol in TARGET_SYMBOLS}

PREFIX = "imugi-idd-definitions-core."


def _owned(owner: str) -> tuple[str, ...]:
    return tuple(
        symbol
        for symbol in TARGET_SYMBOLS
        if symbol == owner or symbol.startswith(owner + ".")
    )


FIELD_PROPERTIES = tuple(
    symbol
    for symbol in _owned("IddField")
    if symbol not in {
        "IddField",
        "IddField.__eq__",
        "IddField.__init__",
        "IddField.from_text",
    }
)
OBJECT_PROPERTIES = tuple(
    symbol
    for symbol in _owned("IddObject")
    if symbol not in {
        "IddObject",
        "IddObject.__eq__",
        "IddObject.__init__",
        "IddObject.from_text",
    }
)
CASE_SPECS = (
    ("A01", "field-class-and-construction", "field-construction", ("IddField", "IddField.__init__")),
    ("B01", "field-equality", "field-equality", ("IddField.__eq__",)),
    ("C01", "field-fragment-parsing", "field-parser", ("IddField.from_text",)),
    ("D01", "field-properties", "field-properties", FIELD_PROPERTIES),
    ("E01", "object-class-and-construction", "object-construction", ("IddObject", "IddObject.__init__")),
    ("F01", "object-equality", "object-equality", ("IddObject.__eq__",)),
    ("G01", "object-fragment-parsing", "object-parser", ("IddObject.from_text",)),
    ("H01", "object-properties", "object-properties", OBJECT_PROPERTIES),
)
EXPECTED_CASE_IDS = tuple(PREFIX + slug for _, slug, _, _ in CASE_SPECS)
EXPECTED_CASE_COUNT = 8

EXPECTED_RUNTIME_SIGNATURES_SHA256 = (
    "sha256:2e63f560d0e9a805d6357f763eb75512ccd0cb1f288c1ccea294928b52e6302a"
)
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = (
    "sha256:b38033bf44c4359f5ee8cf44f8a12b2b267a2f4ddf83a25f0a13b5628b20f692"
)
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = (
    "sha256:757fa1f6f1a78f595eb2894b11427cb2ee7ec9ceb61fe98df86d9d1eb3e939d4"
)
EXPECTED_NATIVE_REVIEW_SHA256 = (
    "sha256:8e9287c015eb273b3847e92fb7106a0e186354ccba5cb74f17436dfe4270269f"
)
EXPECTED_FACT_SHA256 = {
    "imugi-idd-definitions-core.field-class-and-construction": "sha256:d9af6e406b1a21cedc127a4168d1c65fe9bf67d919c76e774187b9749ab84748",
    "imugi-idd-definitions-core.field-equality": "sha256:5686c751946a0d9980d837d7f13b9bfd3b46347d0bf55ffd5344c784f47fba71",
    "imugi-idd-definitions-core.field-fragment-parsing": "sha256:138e8e32a2a070e3ef2e9f1452b748d0851e52b0370ff9f5ebeb6279f5b92961",
    "imugi-idd-definitions-core.field-properties": "sha256:8350e46049c9d06038368828ea991d6d87e5cce74895f916151128dd0d1c7d20",
    "imugi-idd-definitions-core.object-class-and-construction": "sha256:67e38ac935576b850b0675f82947a500e59de1e91dab203d9b598e4c49016872",
    "imugi-idd-definitions-core.object-equality": "sha256:6738956989a52db24baebc8806c95a9f50e4e6fc718435ca99ddd423f3dfcfa2",
    "imugi-idd-definitions-core.object-fragment-parsing": "sha256:2fd8b5d6fb596da60a3ec3c79ca175f79fdc5ef7f93b5a1f85f48bb1be684d2a",
    "imugi-idd-definitions-core.object-properties": "sha256:e92e05c38c65d60b046bfb9944b5c9dc65ea3f79897ea35beb9e29ea9627a522",
}
EXPECTED_CASE_SHA256 = {
    "imugi-idd-definitions-core.field-class-and-construction": "sha256:7db966a34658ab56826ff09313db42fdc890b3395e90ff91d2fa9ae2af8c951a",
    "imugi-idd-definitions-core.field-equality": "sha256:202813e7ba7c1b561a2bc375bdcae66e698ecb2cee300dc130e7be479bc4884c",
    "imugi-idd-definitions-core.field-fragment-parsing": "sha256:8916ca69542e0f3b232febad12877b394d9070594dfe7ea4b2d6c3bca9bc7815",
    "imugi-idd-definitions-core.field-properties": "sha256:fc1a4435bcd89569e412aee78d85a14fcd8f48cde9ef0c800e2174f1e13223c2",
    "imugi-idd-definitions-core.object-class-and-construction": "sha256:485dae80cd1fce309c039161fa42f291bc775f2e7a103983da5acf61ba4ed21e",
    "imugi-idd-definitions-core.object-equality": "sha256:6074b9ff57024b8f660643a3088ea9c791f174c8627de316563d3760648a1c40",
    "imugi-idd-definitions-core.object-fragment-parsing": "sha256:a9ad860cacce1bd0ef4b92c3662d7f58ab5f1d801c6c2c5e7fb03f2b717fd86b",
    "imugi-idd-definitions-core.object-properties": "sha256:6d103260cb4ef1a89d451028abda8844cacfe5ce93628ad69721330a64bf87b3",
}
EXPECTED_CASES_SHA256 = (
    "sha256:002239e3f457bc553c44b4144c0e45e1b470ba7ababe0e2a4aa33c0038abc6ce"
)

SUPPORT_GENERATOR_RECEIPT = {
    "bytes": 38_634,
    "path": "tools/python-reference/generate_idd_schema_oracle.py",
    "sha256": "sha256:64986549c0e3a3aadfef16606396006257d1be4e3b301058098ce364db8391f0",
}
SUPPORT_FIXTURE_RECEIPT = {
    "bytes": 585_482,
    "path": "fixtures/reference/python-0.7.0/idd-24.2.0.schema.json.gz",
    "sha256": "sha256:f2dfc27d39f788f945ef5cc3b79ffce2a516a568075717bd67088d900a75c705",
}
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


class DuplicateJsonKeyError(ValueError):
    """Raised before ``json.loads`` can silently overwrite a member."""


class NonFiniteJsonConstantError(ValueError):
    """Raised when non-standard NaN or infinity tokens occur in JSON."""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def sha256_file(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def strict_json_dumps(value: Any, *, indent: int | None = None) -> str:
    return json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        indent=indent,
        sort_keys=True,
        separators=(",", ":") if indent is None else None,
    )


def canonical_sha256(value: Any) -> str:
    return "sha256:" + hashlib.sha256(
        strict_json_dumps(value).encode("utf-8")
    ).hexdigest()


def _reject_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise DuplicateJsonKeyError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def _reject_nonfinite(value: str) -> None:
    raise NonFiniteJsonConstantError(f"Forbidden non-finite JSON constant: {value}")


def load_json_without_duplicates_text(text: str) -> dict[str, Any]:
    value = json.loads(
        text,
        object_pairs_hook=_reject_duplicates,
        parse_constant=_reject_nonfinite,
    )
    if not isinstance(value, dict):
        raise ValueError("Expected a JSON object root.")
    return value


def load_json_without_duplicates(path: Path) -> dict[str, Any]:
    return load_json_without_duplicates_text(path.read_text(encoding="utf-8"))


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
    if set(value) != {
        "content_sha256",
        "files",
        "schema",
        "scope_sha256",
        "summary",
        "symbols",
        "upstream_commit",
    }:
        raise SystemExit("The public-symbol inventory root contract drifted.")
    if (
        value["schema"] != "goniegonie.upstream-public-symbol-inventory.v2"
        or value["upstream_commit"].lower() != commit
    ):
        raise SystemExit("The public-symbol inventory identity drifted.")
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
    expected_file = {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }
    if [item for item in value["files"] if item["path"] == SOURCE_PATH] != [expected_file]:
        raise SystemExit("The Imugi source file receipt drifted.")
    source_rows = [
        {**item, "inventory_index": index}
        for index, item in enumerate(value["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    if [item["inventory_index"] for item in source_rows] != list(SOURCE_INDICES):
        raise SystemExit("The Imugi declaration range drifted.")
    by_index = {item["inventory_index"]: item for item in source_rows}
    targets = [by_index[index] for index in TARGET_INDICES]
    deferred = [by_index[index] for index in DEFERRED_INDICES]
    out_of_scope = [by_index[index] for index in OUT_OF_SCOPE_INDICES]
    if [(item["inventory_index"], item["symbol"]) for item in targets] != list(TARGET_IDENTITIES):
        raise SystemExit("The Imugi IDD definition target identities drifted.")
    if [
        (item["inventory_index"], item["symbol"]) for item in out_of_scope
    ] != list(OUT_OF_SCOPE_IDENTITIES):
        raise SystemExit("The Imugi out-of-scope identities drifted.")
    receipts = (
        (targets, EXPECTED_TARGET_RECEIPTS_SHA256, "target"),
        (deferred, EXPECTED_DEFERRED_RECEIPTS_SHA256, "deferred"),
        (out_of_scope, EXPECTED_OUT_OF_SCOPE_RECEIPTS_SHA256, "out-of-scope"),
    )
    for rows, expected_hash, label in receipts:
        if expected_hash and canonical_sha256(rows) != expected_hash:
            raise SystemExit(f"The Imugi {label} receipts drifted.")
    if sorted(
        item["inventory_index"] for rows, _, _ in receipts for item in rows
    ) != list(SOURCE_INDICES):
        raise RuntimeError("The Imugi full-source receipt partition is incomplete.")
    return {
        "content_sha256": aggregate,
        "deferred_receipts": deferred,
        "out_of_scope_receipts": out_of_scope,
        "source_file": expected_file,
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
        raise RuntimeError("Imugi IDD definition case order drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Imugi IDD definition case IDs are not sorted.")
    counts = Counter(
        symbol for definition in definitions for symbol in definition["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Imugi IDD definition cases are not an exact partition.")
    return definitions


def _encode(value: Any) -> Any:
    if value is None:
        return {"kind": "none"}
    if type(value) is bool:
        return {"kind": "bool", "value": value}
    if type(value) is int:
        return {"kind": "int", "value": str(value)}
    if type(value) is float:
        if math.isnan(value):
            return {"kind": "special-float", "token": "nan"}
        if math.isinf(value):
            return {
                "kind": "special-float",
                "token": "positive-infinity" if value > 0 else "negative-infinity",
            }
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    if type(value) is str:
        return {"kind": "str", "value": value}
    if isinstance(value, (list, tuple)):
        return {
            "items": [_encode(item) for item in value],
            "kind": "list" if isinstance(value, list) else "tuple",
        }
    if isinstance(value, dict):
        return {
            "items": [
                {"key": _encode(key), "value": _encode(item)}
                for key, item in value.items()
            ],
            "kind": "dict",
        }
    raise RuntimeError(f"Unsupported observation value: {type(value).__name__}")


def _attempt(call: Any) -> dict[str, Any]:
    try:
        value = call()
    except Exception as error:
        return {
            "args": [str(argument) for argument in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }
    return {"outcome": "returned", "result_type": type(value).__name__}


def _class_shape(value: type[Any]) -> dict[str, Any]:
    return {
        "abstract": inspect.isabstract(value),
        "abstract_methods": sorted(getattr(value, "__abstractmethods__", ())),
        "bases": [base.__name__ for base in value.__bases__],
        "mro": [item.__name__ for item in value.__mro__],
        "signature": str(inspect.signature(value)),
    }


def _field_state(value: Any) -> dict[str, Any]:
    return {
        name: _encode(getattr(value, name))
        for name in (
            "name",
            "memo",
            "unit",
            "is_required",
            "is_extensible",
            "minimum",
            "maximum",
            "default",
            "is_deprecated",
            "is_autosizable",
            "is_autocalculatable",
            "type",
            "is_retaincase",
            "key",
            "object_list",
            "external_list",
            "reference",
            "reference_cls",
            "referenceable",
        )
    }


def _field_construction_facts(imugi: Any) -> dict[str, Any]:
    explicit_key = ["On", "Off"]
    explicit_object_list = ["ObjectNames"]
    explicit_reference = ["ReferenceNames"]
    explicit_reference_cls = ["ReferenceClasses"]
    value = imugi.IddField(
        name="Mode",
        memo="First sentence. Second sentence.",
        unit="m",
        is_required=True,
        is_extensible=True,
        is_deprecated=True,
        is_autosizable=True,
        is_autocalculatable=True,
        default=1.25,
        type="choice",
        key=explicit_key,
        object_list=explicit_object_list,
        external_list="ExternalNames",
        minimum=0.0,
        maximum=10.0,
        is_retaincase=True,
        reference=explicit_reference,
        reference_cls=explicit_reference_cls,
    )
    first_default = imugi.IddField()
    second_default = imugi.IddField()
    default_aliases = {
        "key": first_default.key is second_default.key,
        "object_list": first_default.object_list is second_default.object_list,
        "reference": first_default.reference is second_default.reference,
        "reference_cls": first_default.reference_cls is second_default.reference_cls,
        "referenceable": first_default.referenceable is second_default.referenceable,
    }
    first_default.key.append("temporary-probe")
    try:
        shared_key_mutation_visible = second_default.key == ["temporary-probe"]
    finally:
        first_default.key.pop()
    return {
        "class_shape": _class_shape(imugi.IddField),
        "constructor_signature": str(inspect.signature(imugi.IddField.__init__)),
        "defaults": _field_state(imugi.IddField()),
        "explicit_list_identities_preserved": {
            "key": value.key is explicit_key,
            "object_list": value.object_list is explicit_object_list,
            "reference": value.reference is explicit_reference,
            "reference_cls": value.reference_cls is explicit_reference_cls,
        },
        "explicit_state": _field_state(value),
        "keyword_only_rejection": _attempt(lambda: imugi.IddField("Name")),
        "mutable_default_aliases": default_aliases,
        "shared_default_key_mutation_visible": shared_key_mutation_visible,
    }


def _field_equality_facts(imugi: Any) -> dict[str, Any]:
    left = imugi.IddField(name="Mode", memo="Memo.", key=["On", "Off"])
    equal = imugi.IddField(name="Mode", memo="Memo.", key=["On", "Off"])
    different = imugi.IddField(name="Mode", memo="Memo.", key=["On"])
    before = left == equal
    equal.key.append("Auto")
    return {
        "different_key": left == different,
        "equal_before_mutation": before,
        "equal_referenceable_ignored": (
            left == imugi.IddField(name="Mode", memo="Memo.", key=["On", "Off"])
            and not left.referenceable
        ),
        "equality_after_mutating_other_key": left == equal,
        "identity": left == left,
        "wrong_type": _attempt(lambda: left == {"name": "Mode"}),
    }


def _field_parsing_facts(imugi: Any) -> dict[str, Any]:
    representative = """\
\\field Mode
\\note First sentence. Second sentence.
\\memo Third sentence.
\\required-field
\\begin-extensible
\\units m
\\ip-units ft
\\default 1.25
\\deprecated
\\autosizable
\\autocalculatable
\\type choice
\\retaincase
\\key On
\\key Off
\\object-list ObjectNames
\\external-list ExternalNames
\\minimum 0
\\maximum 10
\\reference ReferenceNames
\\reference-class-name ReferenceClasses
"""
    value = imugi.IddField.from_text(representative)
    based_on = imugi.IddField.from_text(
        "\\field Derived Unit\n\\unitsBasedOnField A2\n"
    )
    numeric_defaults = {
        text: _encode(imugi.IddField.from_text(f"\\default {text}\n").default)
        for text in ("0", "12.5", "-1", "1e3", "Autosize")
    }
    return {
        "based_on_field_unit": _encode(based_on.unit),
        "class_method_signature": str(inspect.signature(imugi.IddField.from_text)),
        "exclusive_maximum_failure": _attempt(
            lambda: imugi.IddField.from_text("\\maximum< 10\n")
        ),
        "exclusive_minimum_failure": _attempt(
            lambda: imugi.IddField.from_text("\\minimum> 0\n")
        ),
        "numeric_default_coercion": numeric_defaults,
        "representative_state": _field_state(value),
        "unknown_directive": _attempt(
            lambda: imugi.IddField.from_text("\\not-a-field-flag value\n")
        ),
    }


def _field_property_facts(imugi: Any) -> dict[str, Any]:
    keys = ["A", "B"]
    objects = ["Objects"]
    references = ["Refs"]
    classes = ["Classes"]
    value = imugi.IddField(
        name="Field Name",
        memo="One. Two.",
        unit="kg/s",
        is_required=True,
        is_extensible=True,
        is_deprecated=True,
        is_autosizable=True,
        is_autocalculatable=True,
        default="Autosize",
        type="real",
        key=keys,
        object_list=objects,
        external_list="External",
        minimum=-3.5,
        maximum=9,
        is_retaincase=True,
        reference=references,
        reference_cls=classes,
    )
    before = _field_state(value)
    keys.append("C")
    objects.append("MoreObjects")
    references.append("MoreRefs")
    classes.append("MoreClasses")
    value.referenceable.append({"object": "Thing", "field": "Name"})
    return {
        "after_external_list_mutation": _field_state(value),
        "before_external_list_mutation": before,
        "property_names": list(
            name
            for name, descriptor in imugi.IddField.__dict__.items()
            if isinstance(descriptor, property)
        ),
    }


def _object_state(value: Any) -> dict[str, Any]:
    return {
        "begin_extensible": _encode(value.begin_extensible),
        "default": _encode(value.default),
        "extensible": _encode(value.extensible),
        "field_names": _encode(list(value.keys())),
        "format": _encode(value.format),
        "idd_index": _encode(value.idd_index),
        "is_obsolete": _encode(value.is_obsolete),
        "is_required": _encode(value.is_required),
        "is_unique": _encode(value.is_unique),
        "memo": _encode(value.memo),
        "min_fields": _encode(value.min_fields),
        "name": _encode(value.name),
        "reference": _encode(value.reference),
        "required_fields": _encode(value.required_fields),
    }


def _object_construction_facts(imugi: Any) -> dict[str, Any]:
    name = imugi.IddField(name="Name", is_required=True, default="Unnamed")
    size = imugi.IddField(name="Size", default=1.5)
    value = imugi.IddObject(
        name,
        size,
        name="Test:Object",
        index=["A1", "N1"],
        memo="First sentence. Second sentence.",
        is_unique=True,
        is_required=True,
        is_obsolete=True,
        min_fields=2,
        extensible=1,
        begin_extensible="N1",
        format="vertices",
        reference="TestClasses",
    )
    duplicate_first = imugi.IddField(name="Duplicate", default="first")
    duplicate_second = imugi.IddField(name="Duplicate", default="second")
    duplicate = imugi.IddObject(duplicate_first, duplicate_second, name="Duplicate:Object")
    return {
        "class_shape": _class_shape(imugi.IddObject),
        "constructor_signature": str(inspect.signature(imugi.IddObject.__init__)),
        "duplicate_field_resolution": {
            "field_count": len(duplicate),
            "stored_second_identity": duplicate["Duplicate"] is duplicate_second,
        },
        "empty_state": _object_state(imugi.IddObject()),
        "explicit_field_identities_preserved": [
            value["Name"] is name,
            value["Size"] is size,
        ],
        "explicit_state": _object_state(value),
        "invalid_positional_field": _attempt(
            lambda: imugi.IddObject(name, object(), name="Broken")
        ),
    }


def _object_equality_facts(imugi: Any) -> dict[str, Any]:
    def make(default: Any = "A") -> Any:
        return imugi.IddObject(
            imugi.IddField(name="Name", is_required=True, default=default),
            name="Thing",
            index=["A1"],
            memo="Memo.",
            is_unique=True,
            min_fields=1,
        )

    left = make()
    equal = make()
    different_field = make("B")
    different_attribute = imugi.IddObject(
        imugi.IddField(name="Name", is_required=True, default="A"),
        name="Other",
        index=["A1"],
        memo="Memo.",
        is_unique=True,
        min_fields=1,
    )
    return {
        "different_attribute": left == different_attribute,
        "different_field": left == different_field,
        "equal": left == equal,
        "idd_index_ignored": left == imugi.IddObject(
            imugi.IddField(name="Name", is_required=True, default="A"),
            name="Thing",
            index=["DIFFERENT"],
            memo="Memo.",
            is_unique=True,
            min_fields=1,
        ),
        "identity": left == left,
        "wrong_type": _attempt(lambda: left == {"Name": "A"}),
    }


def _object_parsing_facts(imugi: Any) -> dict[str, Any]:
    representative = """Test:Object,
  \\memo First sentence. Second sentence.
  \\unique-object
  \\required-object
  \\min-fields 2
  \\obsolete Superseded by New:Object
  \\extensible:2
  \\begin-extensible A2
  \\format vertices
  \\reference-class-name TestClasses
  A1, \\field Name
      \\required-field
      \\default Unnamed
  N1, \\field Size
      \\units m
      \\minimum 0
  A2, \\field Vertex Name
      \\begin-extensible
  N2; \\field Vertex Value
      \\default 1.5
"""
    value = imugi.IddObject.from_text(representative)
    unknown = """Broken:Object,
  \\not-an-object-flag value
  A1; \\field Name
"""
    no_fields = """Empty:Object,
  \\memo Nothing here.
"""
    return {
        "class_method_signature": str(inspect.signature(imugi.IddObject.from_text)),
        "field_states": [_field_state(field) for field in value.values()],
        "no_fields": _attempt(lambda: imugi.IddObject.from_text(no_fields)),
        "representative_state": _object_state(value),
        "unknown_directive": _attempt(lambda: imugi.IddObject.from_text(unknown)),
    }


def _object_property_facts(imugi: Any) -> dict[str, Any]:
    required = imugi.IddField(name="Required", is_required=True, default=1)
    optional = imugi.IddField(name="Optional", default="X")
    value = imugi.IddObject(
        required,
        optional,
        name="Property:Object",
        index=("N1", "A1"),
        memo="One. Two.",
        is_unique=True,
        is_required=True,
        is_obsolete=True,
        min_fields=1,
        extensible=2,
        begin_extensible="A1",
        format="singleLine",
        reference="PropertyClasses",
    )
    before = _object_state(value)
    required._IddField__default = 9  # Observe cached projections versus live fields.
    required._IddField__is_required = False
    return {
        "after_private_field_mutation": _object_state(value),
        "before_private_field_mutation": before,
        "property_names": list(
            name
            for name, descriptor in imugi.IddObject.__dict__.items()
            if isinstance(descriptor, property)
        ),
    }


def _execute_cases(imugi: Any) -> dict[str, dict[str, Any]]:
    observations = {
        EXPECTED_CASE_IDS[0]: _field_construction_facts(imugi),
        EXPECTED_CASE_IDS[1]: _field_equality_facts(imugi),
        EXPECTED_CASE_IDS[2]: _field_parsing_facts(imugi),
        EXPECTED_CASE_IDS[3]: _field_property_facts(imugi),
        EXPECTED_CASE_IDS[4]: _object_construction_facts(imugi),
        EXPECTED_CASE_IDS[5]: _object_equality_facts(imugi),
        EXPECTED_CASE_IDS[6]: _object_parsing_facts(imugi),
        EXPECTED_CASE_IDS[7]: _object_property_facts(imugi),
    }
    if tuple(observations) != EXPECTED_CASE_IDS:
        raise RuntimeError("Imugi IDD definition observation order drifted.")
    return observations


def _resolve_descriptor(imugi: Any, symbol: str) -> dict[str, Any]:
    owner_name, separator, member_name = symbol.partition(".")
    owner = getattr(imugi, owner_name)
    if not separator:
        return {
            "abstract": inspect.isabstract(owner),
            "kind": "class",
            "module": owner.__module__,
            "qualname": owner.__qualname__,
            "signature": str(inspect.signature(owner)),
        }
    descriptor = inspect.getattr_static(owner, member_name)
    if isinstance(descriptor, property):
        function = descriptor.fget
        if function is None:
            raise RuntimeError(f"Property getter is absent: {symbol}")
        return {
            "abstract": bool(getattr(function, "__isabstractmethod__", False)),
            "kind": "property",
            "qualname": function.__qualname__,
            "signature": str(inspect.signature(function)),
        }
    if isinstance(descriptor, (classmethod, staticmethod)):
        function = descriptor.__func__
        return {
            "abstract": bool(getattr(function, "__isabstractmethod__", False)),
            "binding": type(descriptor).__name__,
            "kind": "function",
            "qualname": function.__qualname__,
            "signature": str(inspect.signature(function)),
        }
    if callable(descriptor):
        return {
            "abstract": bool(getattr(descriptor, "__isabstractmethod__", False)),
            "kind": "function",
            "qualname": descriptor.__qualname__,
            "signature": str(inspect.signature(descriptor)),
        }
    raise RuntimeError(f"Unsupported runtime descriptor: {symbol}")


def _runtime_signatures(imugi: Any) -> dict[str, Any]:
    return {symbol: _resolve_descriptor(imugi, symbol) for symbol in TARGET_SYMBOLS}


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _audit_loaded_modules(imported_root: Path) -> list[dict[str, Any]]:
    receipts: list[dict[str, Any]] = []
    for source in SOURCE_SPECS:
        module = sys.modules.get(source["module"])
        if module is None or not getattr(module, "__file__", None):
            raise SystemExit(f"Pinned local module was not loaded: {source['module']}")
        resolved = Path(module.__file__).resolve()
        expected = _source_file(imported_root, source).resolve()
        receipt = {
            "ast_sha256": source["ast_sha256"],
            "bytes": resolved.stat().st_size,
            "module": source["module"],
            "path": source["path"],
            "source_sha256": sha256_file(resolved),
        }
        if (
            resolved != expected
            or receipt["bytes"] != source["bytes"]
            or receipt["source_sha256"] != source["source_sha256"]
        ):
            raise SystemExit(f"Loaded local module receipt drifted: {source['module']}")
        receipts.append(receipt)
    loaded_names = sorted(
        name for name in sys.modules if name == "idragon" or name.startswith("idragon.")
    )
    expected_names = sorted(("idragon", *(item["module"] for item in SOURCE_SPECS)))
    if loaded_names != expected_names:
        raise SystemExit(f"Unexpected local modules were loaded: {loaded_names!r}")
    return receipts


@contextmanager
def _isolated_import(
    source_root: Path,
    work_root: Path,
    prefix: str,
) -> Iterator[SimpleNamespace]:
    source_root = source_root.resolve()
    for source in SOURCE_SPECS:
        path = _source_file(source_root, source)
        if (
            not path.is_file()
            or path.stat().st_size != source["bytes"]
            or sha256_file(path) != source["source_sha256"]
        ):
            raise SystemExit(f"Pinned source input drifted: {source['path']}")
    work_root.mkdir(parents=True, exist_ok=True)
    saved_modules = {
        name: module
        for name, module in sys.modules.items()
        if name == "idragon" or name.startswith("idragon.")
    }
    with tempfile.TemporaryDirectory(prefix=prefix, dir=work_root) as temporary:
        imported_root = Path(temporary) / "src"
        shutil.copytree(source_root, imported_root)
        for name in saved_modules:
            sys.modules.pop(name, None)
        package = ModuleType("idragon")
        package.__package__ = "idragon"
        package.__path__ = [str(imported_root / "idragon")]
        sys.modules["idragon"] = package
        try:
            imugi = importlib.import_module("idragon.imugi")
            loaded = _audit_loaded_modules(imported_root)
            if not (
                imugi.Setting is sys.modules["idragon.common"].Setting
                and imugi.Directory is sys.modules["idragon.constants"].Directory
                and imugi.run is sys.modules["idragon.launcher"].run
            ):
                raise SystemExit("Pinned Imugi local dependency identities drifted.")
            yield SimpleNamespace(imugi=imugi, loaded_local_modules=loaded)
        finally:
            for name in list(sys.modules):
                if name == "idragon" or name.startswith("idragon."):
                    sys.modules.pop(name, None)
            sys.modules.update(saved_modules)


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        source_root = Path(entry)
        if all(
            _source_file(source_root, source).is_file()
            and _source_file(source_root, source).stat().st_size == source["bytes"]
            and sha256_file(_source_file(source_root, source)) == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(source_root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def _support_receipt() -> dict[str, Any]:
    repository_root = Path(__file__).resolve().parents[2]
    for receipt in (SUPPORT_GENERATOR_RECEIPT, SUPPORT_FIXTURE_RECEIPT):
        path = repository_root / receipt["path"]
        if (
            not path.is_file()
            or path.stat().st_size != receipt["bytes"]
            or sha256_file(path) != receipt["sha256"]
        ):
            raise SystemExit(f"Pinned IDD support drifted: {receipt['path']}")
    fixture_path = repository_root / SUPPORT_FIXTURE_RECEIPT["path"]
    with gzip.open(fixture_path, "rt", encoding="utf-8") as stream:
        support = load_json_without_duplicates_text(stream.read())
    if set(support) != {
        "energyplus_build",
        "energyplus_version",
        "field_count",
        "groups",
        "object_count",
        "objects",
        "official_epjson_schema",
        "oracle_schema",
        "source_bytes",
        "source_sha256",
        "upstream_commit",
    }:
        raise SystemExit("Pinned full IDD support root contract drifted.")
    official = support["official_epjson_schema"]
    if not isinstance(official, dict):
        raise SystemExit("Pinned official epJSON support is absent.")
    official_identity = {
        "energyplus_version": official["energyplus_version"],
        "field_definition_count": official["field_definition_count"],
        "object_count": official["object_count"],
        "paired_energyplus_build": official["paired_energyplus_build"],
        "source_bytes": official["source_bytes"],
        "source_sha256": official["source_sha256"],
        "validated_dimensions": official["validated_dimensions"],
        "validated_field_occurrence_count": official[
            "validated_field_occurrence_count"
        ],
    }
    identity = {
        "energyplus_build": support["energyplus_build"],
        "energyplus_version": support["energyplus_version"],
        "field_count": support["field_count"],
        "object_count": support["object_count"],
        "oracle_schema": support["oracle_schema"],
        "official_epjson_validation": official_identity,
        "source_bytes": support["source_bytes"],
        "source_sha256": support["source_sha256"],
        "upstream_commit": support["upstream_commit"],
    }
    if (
        identity["oracle_schema"] != "goniegonie.energyplus-idd-schema.v1"
        or identity["energyplus_version"] != "24.2.0"
        or identity["energyplus_build"] != "94a887817b"
        or identity["object_count"] != 848
        or identity["field_count"] != 13_702
        or identity["upstream_commit"] != EXPECTED_UPSTREAM_COMMIT
        or identity["source_sha256"]
        != "3b56fd8afb02a557f1c2cfb963cbc6f53963738bc6aa169f996d7a5175b324a2"
    ):
        raise SystemExit("Pinned full IDD support identity drifted.")
    return {
        "fixture": SUPPORT_FIXTURE_RECEIPT,
        "full_schema_identity": identity,
        "full_schema_identity_sha256": canonical_sha256(identity),
        "generator": SUPPORT_GENERATOR_RECEIPT,
        "native_full_schema_test_route": (
            "GonieGonie.InvisibleDragon.Tests.Idd."
            "IddSchemaOracleTests.EnergyPlus242FullSchemaMatchesRawIddRegressionOracleWhenRuntimeReady"
        ),
    }


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
        "classification_sha256": canonical_sha256(CLASSIFICATIONS),
        "public_production_routes_only": True,
        "python_executes_native_runtime": False,
        "routes_sha256": canonical_sha256(NATIVE_ROUTES),
        "source_receipts": list(NATIVE_SOURCE_RECEIPTS),
        "source_receipts_sha256": canonical_sha256(list(NATIVE_SOURCE_RECEIPTS)),
    }
    digest = canonical_sha256(result)
    if EXPECTED_NATIVE_REVIEW_SHA256 and digest != EXPECTED_NATIVE_REVIEW_SHA256:
        raise SystemExit("Pinned Imugi IDD definition native review drifted.")
    return result


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


def _runtime_receipt() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "dependencies_sha256": canonical_sha256(EXPECTED_DEPENDENCIES),
        "implementation": "cpython",
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def _assertion_ids(receipts: list[dict[str, Any]]) -> dict[str, str]:
    return {
        item["symbol"]: (
            f"imugi-idd-definitions-core-{item['inventory_index']}-"
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
        raise RuntimeError("Imugi IDD definition coverage drifted.")
    return result


def _expected_contract(
    receipts: list[dict[str, Any]],
    signatures: dict[str, Any],
) -> dict[str, Any]:
    assertions = _assertion_ids(receipts)
    counts = Counter(CLASSIFICATIONS.values())
    expectations = {
        symbol: {
            "adaptation": ADAPTATIONS.get(symbol, "not_applicable"),
            "assertion_id": assertions[symbol],
            "classification": CLASSIFICATIONS[symbol],
            "native_route": NATIVE_ROUTES[symbol],
        }
        for symbol in TARGET_SYMBOLS
    }
    return {
        "adaptations": ADAPTATIONS,
        "assertion_ids": assertions,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_counts": {
            "equivalent": counts["equivalent"],
            "exception": counts["exception"],
        },
        "classifications": CLASSIFICATIONS,
        "closure": {
            "deferred_count": len(DEFERRED_INDICES),
            "deferred_indices": list(DEFERRED_INDICES),
            "exact_one_case_target_partition": True,
            "full_imugi_source_partition": True,
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
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    work_root = (
        Path(__file__).resolve().parents[2]
        / "temp"
        / "reference"
        / "imugi-idd-definitions-core-work"
    )
    with _isolated_import(imported_root, work_root, "location-one-") as primary:
        signatures = _runtime_signatures(primary.imugi)
        observations = _execute_cases(primary.imugi)
        loaded_modules = primary.loaded_local_modules
    with _isolated_import(imported_root, work_root, "location-two-") as relocated:
        relocated_signatures = _runtime_signatures(relocated.imugi)
        relocated_observations = _execute_cases(relocated.imugi)
        relocated_modules = relocated.loaded_local_modules
    if signatures != relocated_signatures:
        raise RuntimeError("Imugi IDD definition signatures changed after relocation.")
    if observations != relocated_observations:
        raise RuntimeError("Imugi IDD definition observations changed after relocation.")
    if loaded_modules != relocated_modules:
        raise RuntimeError("Imugi IDD definition loaded modules changed after relocation.")

    signatures_hash = canonical_sha256(signatures)
    modules_hash = canonical_sha256(loaded_modules)
    relocation_hash = canonical_sha256(relocated_observations)
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and signatures_hash != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned Imugi IDD definition runtime signatures drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and modules_hash != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned Imugi IDD definition loaded modules drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and relocation_hash != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Pinned Imugi IDD definition relocated observations drifted.")

    fact_hashes = {
        identifier: canonical_sha256(facts)
        for identifier, facts in observations.items()
    }
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit(
            "Pinned Imugi IDD definition fact hashes drifted.\n"
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
            "Pinned Imugi IDD definition case hashes drifted.\n"
            + strict_json_dumps(case_hashes, indent=2)
        )
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned Imugi IDD definition aggregate case hash drifted.")

    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": aggregate,
        "consumer_contract": _expected_contract(inventory["target_receipts"], signatures),
        "deferred_receipts": inventory["deferred_receipts"],
        "fact_sha256": fact_hashes,
        "native_review": _native_review(),
        "out_of_scope_receipts": inventory["out_of_scope_receipts"],
        "runtime": _runtime_receipt(),
        "schema": SCHEMA,
        "support": _support_receipt(),
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
            "partition_receipts_sha256": {
                "deferred": canonical_sha256(inventory["deferred_receipts"]),
                "out_of_scope": canonical_sha256(inventory["out_of_scope_receipts"]),
                "target": canonical_sha256(inventory["target_receipts"]),
            },
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


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if isinstance(value, float):
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
        raise RuntimeError("Imugi IDD definition schema drifted.")
    receipt_specs = (
        (
            value["target_receipts"],
            EXPECTED_TARGET_RECEIPTS_SHA256,
            list(TARGET_IDENTITIES),
            "target",
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
            list(OUT_OF_SCOPE_IDENTITIES),
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
        ] != expected_identities:
            raise RuntimeError(f"Imugi {label} receipt identities drifted.")
    target_receipts = value["target_receipts"]
    if value["symbols"] != [_descriptor(item) for item in target_receipts]:
        raise RuntimeError("Imugi IDD definition symbol descriptors drifted.")

    signatures = value.get("consumer_contract", {}).get("runtime_signatures")
    if not isinstance(signatures, dict):
        raise RuntimeError("Imugi IDD definition runtime signatures are absent.")
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise RuntimeError("Pinned Imugi IDD definition runtime signatures drifted.")
    if value["consumer_contract"] != _expected_contract(target_receipts, signatures):
        raise RuntimeError("Imugi IDD definition consumer contract drifted.")
    if value["runtime"] != _runtime_receipt():
        raise RuntimeError("Imugi IDD definition runtime receipt drifted.")
    if value["support"] != _support_receipt():
        raise RuntimeError("Imugi IDD definition support receipt drifted.")
    if value["native_review"] != _native_review():
        raise RuntimeError("Imugi IDD definition native review drifted.")

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
    expected_static = {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory": {
            "bytes": EXPECTED_INVENTORY_BYTES,
            "content_sha256": EXPECTED_INVENTORY_SHA256,
            "file_sha256": EXPECTED_INVENTORY_FILE_SHA256,
        },
        "partition_receipts_sha256": {
            "deferred": canonical_sha256(value["deferred_receipts"]),
            "out_of_scope": canonical_sha256(value["out_of_scope_receipts"]),
            "target": canonical_sha256(value["target_receipts"]),
        },
        "source": {
            "ast_sha256": EXPECTED_SOURCE_AST_SHA256,
            "bytes": EXPECTED_SOURCE_BYTES,
            "path": SOURCE_PATH,
            "source_sha256": EXPECTED_SOURCE_SHA256,
        },
    }
    for key, expected in expected_static.items():
        if upstream[key] != expected:
            raise RuntimeError(f"Imugi IDD definition upstream field drifted: {key}")
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
        raise RuntimeError("Imugi IDD definition relocation contract drifted.")
    if EXPECTED_LOADED_LOCAL_MODULES_SHA256 and isolated["loaded_local_modules_sha256"] != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise RuntimeError("Pinned Imugi IDD definition loaded modules drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and isolated["relocated_observations_sha256"] != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise RuntimeError("Pinned Imugi IDD definition relocation receipt drifted.")

    definitions = case_definitions()
    cases = value["cases"]
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [case.get("id") for case in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Imugi IDD definition case order/count drifted.")
    fact_hashes: dict[str, str] = {}
    for case, definition in zip(cases, definitions, strict=True):
        _require_keys(case, {*definition, "python"}, f"case {definition['id']}")
        if any(case[key] != expected for key, expected in definition.items()):
            raise RuntimeError(f"Imugi IDD definition case drifted: {definition['id']}")
        python = case["python"]
        _require_keys(python, {"facts", "facts_sha256", "outcome"}, "python")
        if python["outcome"] != "observed":
            raise RuntimeError(f"Imugi IDD definition outcome drifted: {definition['id']}")
        digest = canonical_sha256(python["facts"])
        if python["facts_sha256"] != digest:
            raise RuntimeError(f"Imugi IDD definition inline facts drifted: {definition['id']}")
        fact_hashes[definition["id"]] = digest
    if value["fact_sha256"] != fact_hashes:
        raise RuntimeError("Imugi IDD definition fact hash map drifted.")
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned Imugi IDD definition fact hashes drifted.")
    actual_case_hashes = case_sha256(cases)
    if value["case_sha256"] != actual_case_hashes:
        raise RuntimeError("Imugi IDD definition case hash map drifted.")
    if EXPECTED_CASE_SHA256 and actual_case_hashes != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned Imugi IDD definition case hashes drifted.")
    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("Imugi IDD definition aggregate case hash drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned Imugi IDD definition aggregate hash drifted.")

    counts = Counter(
        symbol for case in cases for symbol in case["target_symbols"]
    )
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Imugi IDD definition target closure drifted.")
    closure = value["consumer_contract"]["closure"]
    all_receipt_indices = sorted(
        item["inventory_index"]
        for key in ("target_receipts", "deferred_receipts", "out_of_scope_receipts")
        for item in value[key]
    )
    if (
        closure["target_indices"] != list(TARGET_INDICES)
        or closure["deferred_indices"] != list(DEFERRED_INDICES)
        or closure["out_of_scope_indices"] != list(OUT_OF_SCOPE_INDICES)
        or all_receipt_indices != list(SOURCE_INDICES)
    ):
        raise RuntimeError("Imugi full-source partition drifted.")
    _validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Imugi IDD definition strict JSON round trip drifted.")


def _validate_generation_runtime() -> None:
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


def main() -> int:
    args = parse_args()
    _validate_generation_runtime()
    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    result = build_oracle(inventory, args.upstream_commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    counts = Counter(CLASSIFICATIONS.values())
    print(
        f"Wrote {len(result['cases'])} Imugi IDD definition cases covering "
        f"{len(TARGET_INDICES)} targets: {counts['equivalent']} equivalent, "
        f"{counts['exception']} exception; full partition "
        f"{len(TARGET_INDICES)}/{len(DEFERRED_INDICES)}/{len(OUT_OF_SCOPE_INDICES)}; "
        f"aggregate {result['cases_sha256']}."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

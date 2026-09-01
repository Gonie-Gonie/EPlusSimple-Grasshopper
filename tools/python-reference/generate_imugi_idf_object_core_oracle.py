"""Generate the closed Python oracle for Imugi IDF and IdfObject semantics.

The oracle executes exactly 25 unresolved public declarations from the pinned
0.7.0 module in two relocated source copies.  It does not execute .NET.  Native
routes describe current public C# production APIs and intentionally make no
claim of Python source/API compatibility.
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import inspect
import json
import os
from pathlib import Path
import sys
import tempfile
from typing import Any


BASE_PATH = Path(__file__).with_name("generate_imugi_idd_definitions_core_oracle.py")
BASE_BYTES = 70_965
BASE_SHA256 = "sha256:fa70dfc565a30542f58697cee512701356cf2200b3f07332de4e345f0b7b1398"
_base_spec = importlib.util.spec_from_file_location("_imugi_idd_oracle_support", BASE_PATH)
if _base_spec is None or _base_spec.loader is None:
    raise RuntimeError("Cannot load pinned Imugi oracle support.")
base = importlib.util.module_from_spec(_base_spec)
_base_spec.loader.exec_module(base)

SCHEMA = "dragons.python-reference.imugi-idf-object-core.v1"
PREFIX = "imugi-idf-object-core."
SOURCE_PATH = base.SOURCE_PATH
EXPECTED_UPSTREAM_COMMIT = base.EXPECTED_UPSTREAM_COMMIT
EXPECTED_INVENTORY_BYTES = base.EXPECTED_INVENTORY_BYTES
EXPECTED_INVENTORY_FILE_SHA256 = base.EXPECTED_INVENTORY_FILE_SHA256
EXPECTED_INVENTORY_SHA256 = base.EXPECTED_INVENTORY_SHA256
EXPECTED_SOURCE_BYTES = base.EXPECTED_SOURCE_BYTES
EXPECTED_SOURCE_SHA256 = base.EXPECTED_SOURCE_SHA256
EXPECTED_SOURCE_AST_SHA256 = base.EXPECTED_SOURCE_AST_SHA256

BATCH1_INDICES = tuple(range(1123, 1126)) + tuple(range(1128, 1151)) + tuple(range(1153, 1167))
BATCH2_INDICES = (1095, 1097, *range(1100, 1108), 1217, 1218, *range(1219, 1228))
TARGET_IDENTITIES = (
    (1108, "IDF"),
    (1109, "IDF.__init__"),
    (1112, "IDF.__str__"),
    (1113, "IDF.append"),
    (1114, "IDF.check_validity"),
    (1115, "IDF.default_filename"),
    (1116, "IDF.idd"),
    (1118, "IDF.read_idf"),
    (1119, "IDF.run"),
    (1121, "IDF.version"),
    (1122, "IDF.write"),
    (1167, "IdfObject"),
    (1170, "IdfObject.__getitem__"),
    (1171, "IdfObject.__init__"),
    (1173, "IdfObject.__setitem__"),
    (1174, "IdfObject.__str__"),
    (1175, "IdfObject.check_field_validity"),
    (1176, "IdfObject.check_validity"),
    (1177, "IdfObject.choices"),
    (1178, "IdfObject.ensure_validity"),
    (1179, "IdfObject.grandparent"),
    (1180, "IdfObject.has_parent"),
    (1181, "IdfObject.idd"),
    (1182, "IdfObject.parent"),
    (1183, "IdfObject.rename"),
)
TARGET_INDICES = tuple(index for index, _ in TARGET_IDENTITIES)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_IDENTITIES)
BATCH4_INDICES = (1190, 1194, 1195, 1197, 1198, 1199, 1201, *range(1203, 1213), 1214, 1215)
OUT_OF_SCOPE_INDICES = base.OUT_OF_SCOPE_INDICES
SOURCE_INDICES = tuple(range(1095, 1228))
if (
    len(BATCH1_INDICES) != 40
    or len(BATCH2_INDICES) != 21
    or len(TARGET_INDICES) != 25
    or len(BATCH4_INDICES) != 19
    or len(OUT_OF_SCOPE_INDICES) != 28
    or sorted((*BATCH1_INDICES, *BATCH2_INDICES, *TARGET_INDICES, *BATCH4_INDICES, *OUT_OF_SCOPE_INDICES))
    != list(SOURCE_INDICES)
):
    raise RuntimeError("The exact 40/21/25/19/28 Imugi partition drifted.")

EXPECTED_PARTITION_SHA256 = {
    "batch1": "sha256:cea1bdce699efee3b7f152d932f8dd1b52affe0ad139b642e3be2371446e5223",
    "batch2": "sha256:8ba1afe1d26824fe0def879330816229feb65f9bf158e2fbc24072ae61ad6727",
    "batch4": "sha256:9a292cd543bb675b93c77e7456ab43def3dc0ea004159d511cab1bef17d7feb3",
    "out_of_scope": "sha256:3ad4f99816b0591241fe459bd60a0af70f9a40e497be34bab7b132ced2fe42da",
    "target": "sha256:b7cf5615507de3309fc1d8429390216b1920764ef910200f2559c8e187ea3b94",
}

EXPECTED_FACT_SHA256 = {
    "imugi-idf-object-core.idf-construction-and-properties": "sha256:1e6778325aa3e425f7c8a4d9bc6e21b9a9d9db3c8eb21eaad1ffeef35066c4d8",
    "imugi-idf-object-core.idf-object-construction-indexing-and-text": "sha256:a7286a586fc9405231883776e22fa7f32e6c2e18f78ef4409cd769e438403b85",
    "imugi-idf-object-core.idf-object-relationships-and-rename": "sha256:f7fedb8dab3e50ccaf7fac294548f0643b66c53f813ac9b84812d39a2a1290c8",
    "imugi-idf-object-core.idf-object-validation-and-choices": "sha256:a6162af6ebf81b3b9d2d015090810f37098340fccf836e44823541c42a0566a1",
    "imugi-idf-object-core.idf-read-and-write": "sha256:8ad8bde738a9b855393561c66150f855cb015c9f0ea42a144a13e0bf00cfaac1",
    "imugi-idf-object-core.idf-run-signature": "sha256:c8e92c934fce4ae101ec6ebef6a74fc03e0cd28884192bdf4a8a9cd32e9e773d",
    "imugi-idf-object-core.idf-text-append-and-validity": "sha256:223305616ffe25b10756ef7a5de922e771282114b3757f558ac73276683f0e7f",
}
EXPECTED_CASE_SHA256 = {
    "imugi-idf-object-core.idf-construction-and-properties": "sha256:f002a89574b46c2a90f44a59eddea0b930a2bcb8613111d31ec09068f5a4a414",
    "imugi-idf-object-core.idf-object-construction-indexing-and-text": "sha256:f5f3aa52012a91758af292099ad8c8d137752b3484f80cba1663f3dc6720a802",
    "imugi-idf-object-core.idf-object-relationships-and-rename": "sha256:fe98fe37c75c289d5c6e6cb28c35206027474a4d9bef965d1c2ff0428e9218d6",
    "imugi-idf-object-core.idf-object-validation-and-choices": "sha256:319a7e8ad0efc26303e657747157a3512f5a84cb8ca4a0af06b5ca8966ce5ec7",
    "imugi-idf-object-core.idf-read-and-write": "sha256:b6cb09fcf092b7e107b93a7f4710cade1b542636ec282a2466b36e133f0c5872",
    "imugi-idf-object-core.idf-run-signature": "sha256:0dbac25c23c3a1bb3c397707d461b29db5bde22e363f9b7b60fd78ad5de33130",
    "imugi-idf-object-core.idf-text-append-and-validity": "sha256:fbf30d45ff890ac30b4d11c2891d46fa38b9b2826816255bd084e57b067fac90",
}
EXPECTED_CASES_SHA256 = "sha256:b756d2c05de8a6c61319b0e7dcaa44e13a4a4dcc01919480418b6555e7d12cc5"
EXPECTED_RUNTIME_SIGNATURES_SHA256 = "sha256:77dec7ad133bea6d345c54a314004428272b47dfbeb7b8477a55c0fad6d2b51a"
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = "sha256:b38033bf44c4359f5ee8cf44f8a12b2b267a2f4ddf83a25f0a13b5628b20f692"
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = "sha256:7a61f827f76c6fe4c259373295603c25ce69da2bf33fb544511918c5caec1003"

EQUIVALENT_INDICES = (1112, 1113, 1118, 1170, 1174, 1181)
CLASSIFICATIONS = {
    symbol: "equivalent" if index in EQUIVALENT_INDICES else "exception"
    for index, symbol in TARGET_IDENTITIES
}

CASE_SPECS = (
    ("A01", "idf-construction-and-properties", ("IDF", "IDF.__init__", "IDF.default_filename", "IDF.idd", "IDF.version")),
    ("A02", "idf-text-append-and-validity", ("IDF.__str__", "IDF.append", "IDF.check_validity")),
    ("A03", "idf-read-and-write", ("IDF.read_idf", "IDF.write")),
    ("A04", "idf-run-signature", ("IDF.run",)),
    ("B01", "idf-object-construction-indexing-and-text", ("IdfObject", "IdfObject.__getitem__", "IdfObject.__init__", "IdfObject.__setitem__", "IdfObject.__str__")),
    ("B02", "idf-object-validation-and-choices", ("IdfObject.check_field_validity", "IdfObject.check_validity", "IdfObject.choices", "IdfObject.ensure_validity")),
    ("B03", "idf-object-relationships-and-rename", ("IdfObject.grandparent", "IdfObject.has_parent", "IdfObject.idd", "IdfObject.parent", "IdfObject.rename")),
)
CASE_IDS = tuple(PREFIX + slug for _, slug, _ in CASE_SPECS)

NATIVE_SOURCES = (
    {"bytes": 13_182, "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs", "sha256": "sha256:50aa8a362214d34bba37dcf51ef3c0cce89d54895110a0da786c11d8fe233495"},
    {"bytes": 6_040, "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfParser.cs", "sha256": "sha256:98a33eaed892707acb1d05c9e9ef74a9ebb9ec3d258e370e89ff706e267806be"},
    {"bytes": 4_289, "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfWriter.cs", "sha256": "sha256:cc7cc49afcd98a4d4067371686feb49d120a4dd5f7bf30611599a6512c062892"},
    {"bytes": 12_094, "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfValidator.cs", "sha256": "sha256:3f1c8c191cf7054ebdbf674895a2efcabe0b4d265c0de093d900efbb369ed3dd"},
)
SUPPORT = (
    {"bytes": BASE_BYTES, "path": "tools/python-reference/generate_imugi_idd_definitions_core_oracle.py", "sha256": BASE_SHA256},
    {"bytes": 165_323, "path": "fixtures/reference/python-0.7.0/imugi-idd-definitions-core-oracle.json", "sha256": "sha256:3e56e7fe6026fef3146a62aadf3248940c65aa9a2b5c624b519fbc0e3d99dd69"},
    {"bytes": 2_459, "path": "fixtures/reference/python-0.7.0/ashrae-140-modified.idf-summary.json", "sha256": "sha256:b37f44bb097f84cebd7bc9afbba0086b86f00b8e74440e93e753d991ec99420e"},
)


def sha256_file(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def canonical_sha256(value: Any) -> str:
    return base.canonical_sha256(value)


def strict_json_dumps(value: Any, *, indent: int | None = None) -> str:
    return base.strict_json_dumps(value, indent=indent)


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def _repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _pin_artifacts() -> None:
    root = _repository_root()
    for receipt in (*NATIVE_SOURCES, *SUPPORT):
        path = root / receipt["path"]
        if not path.is_file() or path.stat().st_size != receipt["bytes"]:
            raise SystemExit(f"Pinned support artifact drifted: {receipt['path']}")
        actual = sha256_file(path)
        if receipt["sha256"] and actual != receipt["sha256"]:
            raise SystemExit(f"Pinned support artifact hash drifted: {receipt['path']}")


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    base.load_exact_inventory(path, commit)
    raw = base.load_json_without_duplicates(path)
    rows = [
        {**item, "inventory_index": index}
        for index, item in enumerate(raw["symbols"])
        if item["path"] == SOURCE_PATH
    ]
    by_index = {item["inventory_index"]: item for item in rows}
    partitions = {
        "batch1": [by_index[index] for index in BATCH1_INDICES],
        "batch2": [by_index[index] for index in BATCH2_INDICES],
        "target": [by_index[index] for index in TARGET_INDICES],
        "batch4": [by_index[index] for index in BATCH4_INDICES],
        "out_of_scope": [by_index[index] for index in OUT_OF_SCOPE_INDICES],
    }
    if [(item["inventory_index"], item["symbol"]) for item in partitions["target"]] != list(TARGET_IDENTITIES):
        raise SystemExit("Imugi IDF/IdfObject target identities drifted.")
    for name, receipts in partitions.items():
        digest = canonical_sha256(receipts)
        expected = EXPECTED_PARTITION_SHA256[name]
        if expected and digest != expected:
            raise SystemExit(f"Pinned {name} partition receipt drifted.")
    if sorted(item["inventory_index"] for receipts in partitions.values() for item in receipts) != list(SOURCE_INDICES):
        raise RuntimeError("The full 133-declaration partition is incomplete.")
    return {
        "content_sha256": raw["content_sha256"],
        "partitions": partitions,
        "symbols": [_descriptor(item) for item in partitions["target"]],
        "target_receipts": partitions["target"],
    }


def _attempt(call: Any) -> dict[str, Any]:
    try:
        value = call()
    except Exception as error:
        return {"args": [str(item) for item in error.args], "message": str(error), "outcome": "raised", "type": type(error).__name__}
    return {"outcome": "returned", "result_type": type(value).__name__}


def _encode(value: Any) -> Any:
    return base._encode(value)


def _custom_idd(imugi: Any) -> Any:
    name = imugi.IddField(name="Name", type="alpha", is_required=True, reference=["OracleNames"])
    mode = imugi.IddField(name="Mode", type="choice", key=["On", "Off"], default="Off")
    size = imugi.IddField(name="Size", type="real", minimum=0.0, maximum=10.0, default=1.5)
    return imugi.IddObject(name, mode, size, name="Oracle:Object", index=["A1", "A2", "N1"])


def _object(imugi: Any, *, parent: Any = None) -> Any:
    return imugi.IdfObject(_custom_idd(imugi), parent, ["Oracle Name", "on", "3.0"], ensure_validity=False)


def _idf(imugi: Any) -> Any:
    return imugi.IDF("24.2.0", ensure_validity=False, create_required=False)


def _idf_construction(imugi: Any) -> dict[str, Any]:
    value = _idf(imugi)
    return {
        "class_shape": base._class_shape(imugi.IDF),
        "constructor_signature": str(inspect.signature(imugi.IDF.__init__)),
        "default_filename_samples_differ": value.default_filename != value.default_filename,
        "idd_type": type(value.idd).__name__,
        "key_count": len(value.keys()),
        "required_objects_created": len(value),
        "version": str(value.version),
    }


def _idf_text(imugi: Any) -> dict[str, Any]:
    value = _idf(imugi)
    version = imugi.IdfObject("Version", ["24.2"], version="24.2.0", ensure_validity=False)
    value.append(version)
    first = str(value)
    value.append(version)
    return {
        "append_duplicate_ignored": len(value["Version"]) == 1,
        "check_validity_without_required": _attempt(value.check_validity),
        "object_count": len(value),
        "render_sha256": "sha256:" + hashlib.sha256(first.encode("utf-8")).hexdigest(),
        "render_stable": first == str(value),
        "version_fields": [_encode(item) for item in value["Version"][0].values()],
    }


def _idf_io(imugi: Any, work: Path) -> dict[str, Any]:
    text = "Version,24.2;\nGlobalGeometryRules,UpperLeftCorner,CounterClockWise,Relative,Relative,Relative;\n"
    source = work / "input.idf"
    target = work / "output.idf"
    source.write_text(text, encoding="utf-8", newline="\n")
    value = imugi.IDF.read_idf(str(source), ensure_validity=False, encoding="utf-8")
    value.write(str(target))
    written = target.read_text(encoding="utf-8")
    body = "\n".join(line for line in written.splitlines() if not line.startswith("!") and line.strip())
    return {
        "parsed_count": len(value),
        "parsed_types": [key for key in value.keys() if len(value[key])],
        "read_signature": str(inspect.signature(imugi.IDF.read_idf)),
        "write_body_sha256": "sha256:" + hashlib.sha256(body.encode("utf-8")).hexdigest(),
        "write_header_contains_runtime_metadata": "! at " in written,
        "write_signature": str(inspect.signature(imugi.IDF.write)),
    }


def _idf_run(imugi: Any) -> dict[str, Any]:
    return {
        "run_signature": str(inspect.signature(imugi.IDF.run)),
        "run_executed": False,
        "active_energyplus_process_claim": False,
        "native_route": "Dragons.EnergyPlus.Runtime.EnergyPlusRunner.RunAsync",
    }


def _object_core(imugi: Any) -> dict[str, Any]:
    value = _object(imugi)
    before = str(value)
    value["Mode"] = "OFF"
    value["Size"] = "4.5"
    return {
        "class_shape": base._class_shape(imugi.IdfObject),
        "constructor_signature": str(inspect.signature(imugi.IdfObject.__init__)),
        "getitem_name": _encode(value["Name"]),
        "normalized_mode": _encode(value["Mode"]),
        "normalized_size": _encode(value["Size"]),
        "render_before_sha256": "sha256:" + hashlib.sha256(before.encode("utf-8")).hexdigest(),
        "render_after_sha256": "sha256:" + hashlib.sha256(str(value).encode("utf-8")).hexdigest(),
    }


def _object_validation(imugi: Any) -> dict[str, Any]:
    value = _object(imugi)
    return {
        "choices_mode": [_encode(item) for item in value.choices("Mode")],
        "ensure_validity_getter": _encode(value.ensure_validity),
        "instance_check": _attempt(value.check_validity),
        "static_valid": _attempt(lambda: imugi.IdfObject.check_field_validity("On", value.idd["Mode"], ["On", "Off"])),
        "static_invalid": _attempt(lambda: imugi.IdfObject.check_field_validity("Maybe", value.idd["Mode"], ["On", "Off"])),
    }


def _object_relationships(imugi: Any) -> dict[str, Any]:
    isolated = _object(imugi)
    before = isolated["Name"]
    isolated.rename("Renamed")
    return {
        "grandparent": _encode(isolated.grandparent),
        "has_parent": isolated.has_parent,
        "idd_identity_preserved": isolated.idd.name == "Oracle:Object",
        "parent": _encode(isolated.parent),
        "rename_without_referenceable_is_noop": isolated["Name"] == before,
        "relationship_property_names": [name for name in ("grandparent", "has_parent", "idd", "parent") if isinstance(inspect.getattr_static(imugi.IdfObject, name), property)],
    }


def _execute_cases(imugi: Any, work: Path) -> dict[str, dict[str, Any]]:
    return {
        CASE_IDS[0]: _idf_construction(imugi),
        CASE_IDS[1]: _idf_text(imugi),
        CASE_IDS[2]: _idf_io(imugi, work),
        CASE_IDS[3]: _idf_run(imugi),
        CASE_IDS[4]: _object_core(imugi),
        CASE_IDS[5]: _object_validation(imugi),
        CASE_IDS[6]: _object_relationships(imugi),
    }


def _runtime_signatures(imugi: Any) -> dict[str, Any]:
    return {symbol: base._resolve_descriptor(imugi, symbol) for symbol in TARGET_SYMBOLS}


def _route(symbol: str) -> str:
    routes = {
        "IDF.__str__": "Dragons.InvisibleDragon.Idf.IdfWriter.Write(IdfDocument, IdfWriterOptions?)",
        "IDF.append": "Dragons.InvisibleDragon.Idf.IdfDocument.Append(IdfObject)",
        "IDF.read_idf": "Dragons.InvisibleDragon.Idf.IdfParser.ParseFile(string, IddSchema?, Encoding?)",
        "IdfObject.__getitem__": "Dragons.InvisibleDragon.Idf.IdfObject.this[int|string]",
        "IdfObject.__str__": "Dragons.InvisibleDragon.Idf.IdfWriter.Write(IdfDocument, IdfWriterOptions?)",
        "IdfObject.idd": "Dragons.InvisibleDragon.Idf.IdfObject.Definition",
    }
    if symbol in routes:
        return routes[symbol]
    owner = "IdfDocument" if symbol.startswith("IDF") else "IdfObject"
    return f"Dragons.InvisibleDragon.Idf.{owner} public production API (intentional adaptation; no Python source/API compatibility claim)"


def _adaptation(symbol: str) -> str:
    if CLASSIFICATIONS[symbol] == "equivalent":
        return "semantic-public-idf-route"
    return "typed-immutable-native-idf-adaptation"


def case_definitions() -> list[dict[str, Any]]:
    result = [
        {"code": code, "id": PREFIX + slug, "target_symbols": list(symbols)}
        for code, slug, symbols in CASE_SPECS
    ]
    counts = Counter(symbol for case in result for symbol in case["target_symbols"])
    if counts != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Cases are not an exact target partition.")
    return result


def _native_review(root: Path) -> dict[str, Any]:
    sources = []
    for pin in NATIVE_SOURCES:
        path = root / pin["path"]
        sources.append({**pin, "actual_sha256": sha256_file(path)})
    if any(item["actual_sha256"] != item["sha256"] for item in sources):
        raise SystemExit("Pinned native IDF source drifted.")
    return {
        "classifications": CLASSIFICATIONS,
        "counts": dict(sorted(Counter(CLASSIFICATIONS.values()).items())),
        "native_routes": {symbol: _route(symbol) for symbol in TARGET_SYMBOLS},
        "no_python_api_or_source_compatibility_claim": True,
        "public_production_routes_only": True,
        "python_executes_native_runtime": False,
        "sources": [{key: item[key] for key in ("bytes", "path", "sha256")} for item in sources],
    }


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    if commit.lower() != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("The requested upstream commit is not exactly pinned.")
    _pin_artifacts()
    source_root = base._find_pinned_source_root()
    work_root = _repository_root() / "temp" / "reference" / "imugi-idf-object-core-work"
    with base._isolated_import(source_root, work_root, "location-one-") as first:
        with tempfile.TemporaryDirectory(prefix="observations-one-", dir=work_root) as temporary:
            facts = _execute_cases(first.imugi, Path(temporary))
        signatures = _runtime_signatures(first.imugi)
        modules = first.loaded_local_modules
    with base._isolated_import(source_root, work_root, "location-two-") as second:
        with tempfile.TemporaryDirectory(prefix="observations-two-", dir=work_root) as temporary:
            relocated_facts = _execute_cases(second.imugi, Path(temporary))
        relocated_signatures = _runtime_signatures(second.imugi)
        relocated_modules = second.loaded_local_modules
    if facts != relocated_facts or signatures != relocated_signatures or modules != relocated_modules:
        raise RuntimeError("Relocated Imugi observations are not byte-semantic-identical.")
    if canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Pinned runtime signatures drifted.")
    if canonical_sha256(modules) != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Pinned loaded-module receipts drifted.")
    if canonical_sha256(relocated_facts) != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Pinned relocated observations drifted.")

    cases = []
    fact_hashes = {}
    for definition in case_definitions():
        identifier = definition["id"]
        digest = canonical_sha256(facts[identifier])
        fact_hashes[identifier] = digest
        cases.append({**definition, "python": {"facts": facts[identifier], "facts_sha256": digest, "outcome": "observed"}})
    case_hashes = {case["id"]: canonical_sha256(case) for case in cases}
    if fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit("Pinned fact hashes drifted.")
    if case_hashes != EXPECTED_CASE_SHA256 or canonical_sha256(cases) != EXPECTED_CASES_SHA256:
        raise SystemExit("Pinned case hashes drifted.")
    target_receipts = inventory["target_receipts"]
    partition_hashes = {name: canonical_sha256(rows) for name, rows in inventory["partitions"].items()}
    contract = {
        "adaptations": {symbol: _adaptation(symbol) for symbol in TARGET_SYMBOLS},
        "assertion_ids": {symbol: f"imugi-idf-object-core-{next(index for index, candidate in TARGET_IDENTITIES if candidate == symbol)}-{hashlib.sha256(symbol.encode()).hexdigest()[:8]}" for symbol in TARGET_SYMBOLS},
        "classification_counts": dict(sorted(Counter(CLASSIFICATIONS.values()).items())),
        "classifications": CLASSIFICATIONS,
        "closure": {
            "batch1_count": 40,
            "batch1_indices": list(BATCH1_INDICES),
            "batch2_count": 21,
            "batch2_indices": list(BATCH2_INDICES),
            "batch4_count": 19,
            "batch4_indices": list(BATCH4_INDICES),
            "exact_disjoint_source_partition": True,
            "out_of_scope_count": 28,
            "out_of_scope_indices": list(OUT_OF_SCOPE_INDICES),
            "partition_receipts_sha256": partition_hashes,
            "source_declaration_count": 133,
            "source_indices": list(SOURCE_INDICES),
            "target_count": 25,
            "target_indices": list(TARGET_INDICES),
        },
        "evidence_contract": {
            "active_energyplus_process_claim": False,
            "expected_receipt_count": 25,
            "native_runtime_executed_by_python_oracle": False,
            "path_independent_relocated_import": True,
            "python_api_or_source_compatibility_claim": False,
            "structural_only": False,
        },
        "native_routes": {symbol: _route(symbol) for symbol in TARGET_SYMBOLS},
        "runtime_signatures": signatures,
    }
    result = {
        "case_sha256": case_hashes,
        "cases": cases,
        "cases_sha256": canonical_sha256(cases),
        "consumer_contract": contract,
        "fact_sha256": fact_hashes,
        "native_review": _native_review(_repository_root()),
        "partitions": inventory["partitions"],
        "runtime": base._runtime_receipt(),
        "schema": SCHEMA,
        "support": [{**item, "role": "immutable-existing-idf-support"} for item in SUPPORT],
        "symbols": inventory["symbols"],
        "target_receipts": target_receipts,
        "upstream": {
            "commit": EXPECTED_UPSTREAM_COMMIT,
            "inventory": {"bytes": EXPECTED_INVENTORY_BYTES, "content_sha256": EXPECTED_INVENTORY_SHA256, "file_sha256": EXPECTED_INVENTORY_FILE_SHA256},
            "isolated_import": {
                "loaded_local_modules": modules,
                "loaded_local_modules_sha256": canonical_sha256(modules),
                "relocated_observations_sha256": canonical_sha256(relocated_facts),
                "relocated_source_copy": "two-byte-identical-repository-temp-copies",
                "source_location_count": 2,
            },
            "source": {"ast_sha256": EXPECTED_SOURCE_AST_SHA256, "bytes": EXPECTED_SOURCE_BYTES, "path": SOURCE_PATH, "source_sha256": EXPECTED_SOURCE_SHA256},
        },
    }
    validate_oracle(result)
    return result


def validate_oracle(value: dict[str, Any]) -> None:
    expected_root = {"case_sha256", "cases", "cases_sha256", "consumer_contract", "fact_sha256", "native_review", "partitions", "runtime", "schema", "support", "symbols", "target_receipts", "upstream"}
    if set(value) != expected_root or value["schema"] != SCHEMA:
        raise RuntimeError("Oracle root contract drifted.")
    if len(value["target_receipts"]) != 25 or len(value["cases"]) != len(CASE_SPECS):
        raise RuntimeError("Oracle target or case count drifted.")
    if [(item["inventory_index"], item["symbol"]) for item in value["target_receipts"]] != list(TARGET_IDENTITIES):
        raise RuntimeError("Target receipt identities drifted.")
    if value["symbols"] != [_descriptor(item) for item in value["target_receipts"]]:
        raise RuntimeError("Target descriptors drifted.")
    for name, receipts in value["partitions"].items():
        if canonical_sha256(receipts) != value["consumer_contract"]["closure"]["partition_receipts_sha256"][name]:
            raise RuntimeError(f"Partition hash drifted: {name}")
        expected = EXPECTED_PARTITION_SHA256[name]
        if expected and canonical_sha256(receipts) != expected:
            raise RuntimeError(f"Pinned partition drifted: {name}")
    all_indices = sorted(item["inventory_index"] for rows in value["partitions"].values() for item in rows)
    if all_indices != list(SOURCE_INDICES):
        raise RuntimeError("Full source closure drifted.")
    definitions = case_definitions()
    for case, definition in zip(value["cases"], definitions, strict=True):
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Case definition drifted: {definition['id']}")
        digest = canonical_sha256(case["python"]["facts"])
        if digest != case["python"]["facts_sha256"] or digest != value["fact_sha256"][case["id"]]:
            raise RuntimeError(f"Case facts drifted: {definition['id']}")
        if canonical_sha256(case) != value["case_sha256"][case["id"]]:
            raise RuntimeError(f"Case hash drifted: {definition['id']}")
    if canonical_sha256(value["cases"]) != value["cases_sha256"]:
        raise RuntimeError("Aggregate case hash drifted.")
    if value["fact_sha256"] != EXPECTED_FACT_SHA256 or value["case_sha256"] != EXPECTED_CASE_SHA256 or value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned case/fact layers drifted.")
    contract = value["consumer_contract"]
    if contract["classification_counts"] != {"equivalent": 6, "exception": 19}:
        raise RuntimeError("Classification counts drifted.")
    if contract["evidence_contract"] != {
        "active_energyplus_process_claim": False,
        "expected_receipt_count": 25,
        "native_runtime_executed_by_python_oracle": False,
        "path_independent_relocated_import": True,
        "python_api_or_source_compatibility_claim": False,
        "structural_only": False,
    }:
        raise RuntimeError("Evidence contract drifted.")
    if value["upstream"]["isolated_import"]["source_location_count"] != 2:
        raise RuntimeError("Relocation count drifted.")
    if canonical_sha256(value["consumer_contract"]["runtime_signatures"]) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise RuntimeError("Runtime signature hash drifted.")
    if value["upstream"]["isolated_import"]["loaded_local_modules_sha256"] != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise RuntimeError("Loaded-module hash drifted.")
    if value["upstream"]["isolated_import"]["relocated_observations_sha256"] != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise RuntimeError("Relocated observation hash drifted.")
    if value["native_review"]["python_executes_native_runtime"]:
        raise RuntimeError("Python must not claim native runtime execution.")
    base._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(base.load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Strict JSON round trip drifted.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    base._validate_generation_runtime()
    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    result = build_oracle(inventory, args.upstream_commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(strict_json_dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    counts = Counter(CLASSIFICATIONS.values())
    print(f"Wrote {len(result['cases'])} cases for 25 targets: {counts['equivalent']} equivalent, {counts['exception']} exception; partition hashes {strict_json_dumps(result['consumer_contract']['closure']['partition_receipts_sha256'])}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

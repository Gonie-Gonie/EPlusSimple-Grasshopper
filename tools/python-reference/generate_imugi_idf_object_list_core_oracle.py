"""Generate the final closed Imugi IdfObjectList compatibility oracle."""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import inspect
from pathlib import Path
import sys
from typing import Any


BASE_PATH = Path(__file__).with_name("generate_imugi_idf_object_core_oracle.py")
BASE_BYTES = 30_077
BASE_SHA256 = "sha256:3e87aaf0501d1176ab1ffb2be07710d1c8e6c58ef061101b4a70b14eb6f8b7f7"
spec = importlib.util.spec_from_file_location("_imugi_idf_object_oracle_support", BASE_PATH)
if spec is None or spec.loader is None:
    raise RuntimeError("Cannot load the pinned Imugi IDF support oracle.")
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)

SCHEMA = "dragons.python-reference.imugi-idf-object-list-core.v1"
PREFIX = "imugi-idf-object-list-core."
EXPECTED_UPSTREAM_COMMIT = base.EXPECTED_UPSTREAM_COMMIT
SOURCE_PATH = base.SOURCE_PATH
TARGET_IDENTITIES = (
    (1190, "IdfObjectList"),
    (1194, "IdfObjectList.__getitem__"),
    (1195, "IdfObjectList.__init__"),
    (1197, "IdfObjectList.__setitem__"),
    (1198, "IdfObjectList.__str__"),
    (1199, "IdfObjectList.append"),
    (1201, "IdfObjectList.check_validity"),
    (1203, "IdfObjectList.ensure_validity"),
    (1204, "IdfObjectList.fieldnames"),
    (1205, "IdfObjectList.get_fields"),
    (1206, "IdfObjectList.has_name"),
    (1207, "IdfObjectList.has_parent"),
    (1208, "IdfObjectList.idd"),
    (1209, "IdfObjectList.insert"),
    (1210, "IdfObjectList.is_containor"),
    (1211, "IdfObjectList.names"),
    (1212, "IdfObjectList.parent"),
    (1214, "IdfObjectList.set_fields"),
    (1215, "IdfObjectList.set_wwr"),
)
TARGET_INDICES = tuple(index for index, _ in TARGET_IDENTITIES)
TARGET_SYMBOLS = tuple(symbol for _, symbol in TARGET_IDENTITIES)
BATCH1_INDICES = base.BATCH1_INDICES
BATCH2_INDICES = base.BATCH2_INDICES
BATCH3_INDICES = base.TARGET_INDICES
OUT_OF_SCOPE_INDICES = base.OUT_OF_SCOPE_INDICES
SOURCE_INDICES = tuple(range(1095, 1228))
if (
    len(BATCH1_INDICES) != 40 or len(BATCH2_INDICES) != 21 or len(BATCH3_INDICES) != 25
    or len(TARGET_INDICES) != 19 or len(OUT_OF_SCOPE_INDICES) != 28
    or sorted((*BATCH1_INDICES, *BATCH2_INDICES, *BATCH3_INDICES, *TARGET_INDICES, *OUT_OF_SCOPE_INDICES)) != list(SOURCE_INDICES)
):
    raise RuntimeError("The exact final 40/21/25/19/28 partition drifted.")

EXPECTED_PARTITION_SHA256 = {
    "batch1": "sha256:cea1bdce699efee3b7f152d932f8dd1b52affe0ad139b642e3be2371446e5223",
    "batch2": "sha256:8ba1afe1d26824fe0def879330816229feb65f9bf158e2fbc24072ae61ad6727",
    "batch3": "sha256:b7cf5615507de3309fc1d8429390216b1920764ef910200f2559c8e187ea3b94",
    "out_of_scope": "sha256:3ad4f99816b0591241fe459bd60a0af70f9a40e497be34bab7b132ced2fe42da",
    "target": "sha256:9a292cd543bb675b93c77e7456ab43def3dc0ea004159d511cab1bef17d7feb3",
}
EQUIVALENT_INDICES = (1194, 1199, 1209, 1211)
CLASSIFICATIONS = {symbol: "equivalent" if index in EQUIVALENT_INDICES else "exception" for index, symbol in TARGET_IDENTITIES}

CASE_SPECS = (
    ("A01", "construction-and-properties", ("IdfObjectList", "IdfObjectList.__init__", "IdfObjectList.ensure_validity", "IdfObjectList.has_name", "IdfObjectList.has_parent", "IdfObjectList.idd", "IdfObjectList.is_containor", "IdfObjectList.parent")),
    ("A02", "append-insert-index-and-set", ("IdfObjectList.__getitem__", "IdfObjectList.__setitem__", "IdfObjectList.append", "IdfObjectList.insert")),
    ("A03", "fields-and-names", ("IdfObjectList.fieldnames", "IdfObjectList.get_fields", "IdfObjectList.names", "IdfObjectList.set_fields")),
    ("A04", "text-and-validity", ("IdfObjectList.__str__", "IdfObjectList.check_validity")),
    ("A05", "set-window-wall-ratio-placeholder", ("IdfObjectList.set_wwr",)),
)
CASE_IDS = tuple(PREFIX + slug for _, slug, _ in CASE_SPECS)

NATIVE_SOURCES = (
    {"bytes": 13_173, "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfModel.cs", "sha256": "sha256:0d16e28d37136a3aa0015759ead7ee324cfed08cff1a3269326d4af144518048"},
    {"bytes": 12_082, "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfValidator.cs", "sha256": "sha256:12488433e2e9f349553e0716531e88db275f563b7f5b806c10a316ae3719cf7e"},
    {"bytes": 4_280, "path": "src/InvisibleDragon/Dragons.InvisibleDragon.Core/Idf/IdfWriter.cs", "sha256": "sha256:c7b98b6eed298687fca229ae7262ffdf2494953b3cc6576835cacbcc47cf998a"},
)
SUPPORT = (
    {"bytes": BASE_BYTES, "path": "tools/python-reference/generate_imugi_idf_object_core_oracle.py", "sha256": BASE_SHA256},
    {"bytes": 119_037, "path": "fixtures/reference/python-0.7.0/imugi-idf-object-core-oracle.json", "sha256": "sha256:61c137044af671cd9a1a935fea516b3d72eaa74f3d3c5122b3a61acef981cc93"},
    {"bytes": 165_062, "path": "fixtures/reference/python-0.7.0/imugi-idd-definitions-core-oracle.json", "sha256": "sha256:5b586ac030309bed3ab840525b4c9cff207b97919cff76bb48e8003b9135bcf9"},
)

EXPECTED_FACT_SHA256: dict[str, str] = {
    "imugi-idf-object-list-core.append-insert-index-and-set": "sha256:5687334d30870976ca0bdb56b0308fa21aec506f195797de40155bacb4ee4008",
    "imugi-idf-object-list-core.construction-and-properties": "sha256:88728631589871091bfaed6f37a6307d999073a4b3f2f93651dc51392b18e9f7",
    "imugi-idf-object-list-core.fields-and-names": "sha256:36009e6e83fc1286c5ba085af6d64c9b178c961ddf38c609629c536a24f9a30f",
    "imugi-idf-object-list-core.set-window-wall-ratio-placeholder": "sha256:a544572b20ae8a283c23d1fe38ff917f6a1f219df8581ee2e27c833e891aab49",
    "imugi-idf-object-list-core.text-and-validity": "sha256:477cacdffa9b37d8d0af2f12d47e3ab4f13cd9a6a1dfa88160efee132fd4a6fd",
}
EXPECTED_CASE_SHA256: dict[str, str] = {
    "imugi-idf-object-list-core.append-insert-index-and-set": "sha256:c4889e3169f848995ccf954220d407f602273ac5507ca231984b57195268da8d",
    "imugi-idf-object-list-core.construction-and-properties": "sha256:b8a2f2c9e0b1daacb576b29686834f8735cef0ae5ad241cc0295b640b75b31fe",
    "imugi-idf-object-list-core.fields-and-names": "sha256:11770de066bf66ce09a761e8662c0f668714426e0a8d197be6d2dc55114b0cd3",
    "imugi-idf-object-list-core.set-window-wall-ratio-placeholder": "sha256:b689f20d9880c35704a0a7bd2e3b2e3e57ec8c78eb508dd3721ec392f4d12879",
    "imugi-idf-object-list-core.text-and-validity": "sha256:38f23a51a79eef9d2338de5b04c0e555288795fc72454a71a70c453953347bfd",
}
EXPECTED_CASES_SHA256 = "sha256:60ddb2ba91b3c3b19867063bdca5be7e0f31d628f193569ba487f79cb6816c2f"
EXPECTED_RUNTIME_SIGNATURES_SHA256 = "sha256:834b1a22acbd2742c9984e70504c25db8b69a537ca77f9b9e63e98b38a5a4327"
EXPECTED_LOADED_LOCAL_MODULES_SHA256 = base.EXPECTED_LOADED_LOCAL_MODULES_SHA256
EXPECTED_RELOCATED_OBSERVATIONS_SHA256 = "sha256:8823eb0813bc73566b05515658dea657fc4852a76f9c1d628b104a15a019a45e"


def sha256_file(path: Path) -> str:
    return "sha256:" + hashlib.sha256(path.read_bytes()).hexdigest()


def canonical_sha256(value: Any) -> str:
    return base.canonical_sha256(value)


def strict_json_dumps(value: Any, *, indent: int | None = None) -> str:
    return base.strict_json_dumps(value, indent=indent)


def _root() -> Path:
    return Path(__file__).resolve().parents[2]


def _descriptor(receipt: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in receipt.items() if key != "inventory_index"}


def _pin_artifacts() -> None:
    for receipt in (*NATIVE_SOURCES, *SUPPORT):
        path = _root() / receipt["path"]
        if not path.is_file() or path.stat().st_size != receipt["bytes"] or sha256_file(path) != receipt["sha256"]:
            raise SystemExit(f"Pinned support drifted: {receipt['path']}")


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    base.load_exact_inventory(path, commit)
    raw = base.base.load_json_without_duplicates(path)
    rows = [{**item, "inventory_index": index} for index, item in enumerate(raw["symbols"]) if item["path"] == SOURCE_PATH]
    by_index = {item["inventory_index"]: item for item in rows}
    partitions = {
        "batch1": [by_index[index] for index in BATCH1_INDICES],
        "batch2": [by_index[index] for index in BATCH2_INDICES],
        "batch3": [by_index[index] for index in BATCH3_INDICES],
        "target": [by_index[index] for index in TARGET_INDICES],
        "out_of_scope": [by_index[index] for index in OUT_OF_SCOPE_INDICES],
    }
    if [(item["inventory_index"], item["symbol"]) for item in partitions["target"]] != list(TARGET_IDENTITIES):
        raise SystemExit("Final Imugi target identities drifted.")
    for name, receipts in partitions.items():
        if canonical_sha256(receipts) != EXPECTED_PARTITION_SHA256[name]:
            raise SystemExit(f"Pinned final partition drifted: {name}")
    if sorted(item["inventory_index"] for values in partitions.values() for item in values) != list(SOURCE_INDICES):
        raise RuntimeError("The full 133 declaration closure drifted.")
    return {"content_sha256": raw["content_sha256"], "partitions": partitions, "symbols": [_descriptor(item) for item in partitions["target"]], "target_receipts": partitions["target"]}


def _list(imugi: Any) -> Any:
    return imugi.IdfObjectList(base._custom_idd(imugi), None, ensure_validity=False)


def _construction(imugi: Any) -> dict[str, Any]:
    value = _list(imugi)
    return {
        "class_shape": base.base._class_shape(imugi.IdfObjectList),
        "constructor_signature": str(inspect.signature(imugi.IdfObjectList.__init__)),
        "ensure_validity": value.ensure_validity,
        "fieldnames": value.fieldnames(),
        "has_name": value.has_name,
        "has_parent": value.has_parent,
        "idd_identity": value.idd.name,
        "is_containor": value.is_containor,
        "parent_is_none": value.parent is None,
    }


def _editing(imugi: Any) -> dict[str, Any]:
    value = _list(imugi)
    value.append(["First", "On", 1.0])
    value.insert(0, {"Name": "Zero", "Mode": "Off", "Size": 0.0})
    selected = value["First"]
    value[1] = ["Second", "On", 2.0]
    sliced = value[:1]
    return {
        "count": len(value),
        "integer_name": value[0]["Name"],
        "name_index_identity": selected.idd is value.idd,
        "names": value.names,
        "setitem_name": value[1]["Name"],
        "slice_count": len(sliced),
        "slice_is_containor": sliced.is_containor,
    }


def _fields(imugi: Any) -> dict[str, Any]:
    value = _list(imugi)
    value.append(["One", "On", 1])
    value.append(["Two", "Off", 2])
    before = value.get_fields("Mode")
    value.set_fields("Mode", "On")
    return {"fieldnames": value.fieldnames(), "modes_before": before, "modes_after": value.get_fields("Mode"), "names": value.names}


def _text_validity(imugi: Any) -> dict[str, Any]:
    value = _list(imugi)
    value.append(["One", "On", 1])
    text = str(value)
    return {"check_validity_result": base._attempt(value.check_validity), "render_sha256": "sha256:" + hashlib.sha256(text.encode()).hexdigest(), "render_header": text.splitlines()[0]}


def _wwr(imugi: Any) -> dict[str, Any]:
    value = _list(imugi)
    before = list(value.data)
    result = value.set_wwr(0.4)
    return {"input": base.base._encode(0.4), "result": base.base._encode(result), "state_unchanged": before == value.data, "placeholder_behavior": True}


def _execute(imugi: Any) -> dict[str, dict[str, Any]]:
    return {CASE_IDS[0]: _construction(imugi), CASE_IDS[1]: _editing(imugi), CASE_IDS[2]: _fields(imugi), CASE_IDS[3]: _text_validity(imugi), CASE_IDS[4]: _wwr(imugi)}


def _signatures(imugi: Any) -> dict[str, Any]:
    return {symbol: base.base._resolve_descriptor(imugi, symbol) for symbol in TARGET_SYMBOLS}


def _route(symbol: str) -> str:
    routes = {
        "IdfObjectList.__getitem__": "Dragons.InvisibleDragon.Idf.IdfObjectCollection.this[int|string]",
        "IdfObjectList.append": "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Append(IdfObject)",
        "IdfObjectList.insert": "Dragons.InvisibleDragon.Idf.IdfObjectCollection.Insert(int, IdfObject)",
        "IdfObjectList.names": "Dragons.InvisibleDragon.Idf.IdfObjectCollection -> IdfObject.Name",
    }
    return routes.get(symbol, "Dragons.InvisibleDragon.Idf.IdfObjectCollection/IdfDocument public typed adaptation (no Python API/source compatibility claim)")


def definitions() -> list[dict[str, Any]]:
    result = [{"code": code, "id": PREFIX + slug, "target_symbols": list(symbols)} for code, slug, symbols in CASE_SPECS]
    if Counter(symbol for case in result for symbol in case["target_symbols"]) != Counter({symbol: 1 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Final Imugi cases do not close targets exactly once.")
    return result


def _native_review() -> dict[str, Any]:
    for pin in NATIVE_SOURCES:
        if sha256_file(_root() / pin["path"]) != pin["sha256"]:
            raise SystemExit(f"Native source drifted: {pin['path']}")
    return {
        "classifications": CLASSIFICATIONS,
        "counts": dict(sorted(Counter(CLASSIFICATIONS.values()).items())),
        "native_routes": {symbol: _route(symbol) for symbol in TARGET_SYMBOLS},
        "no_python_api_or_source_compatibility_claim": True,
        "public_production_routes_only": True,
        "python_executes_native_runtime": False,
        "sources": list(NATIVE_SOURCES),
    }


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    if commit.lower() != EXPECTED_UPSTREAM_COMMIT:
        raise SystemExit("Upstream commit is not pinned.")
    _pin_artifacts()
    source_root = base.base._find_pinned_source_root()
    work = _root() / "temp" / "reference" / "imugi-idf-object-list-core-work"
    with base.base._isolated_import(source_root, work, "location-one-") as first:
        facts, signatures, modules = _execute(first.imugi), _signatures(first.imugi), first.loaded_local_modules
    with base.base._isolated_import(source_root, work, "location-two-") as second:
        relocated, relocated_signatures, relocated_modules = _execute(second.imugi), _signatures(second.imugi), second.loaded_local_modules
    if facts != relocated or signatures != relocated_signatures or modules != relocated_modules:
        raise RuntimeError("Relocated final Imugi observations drifted.")
    if canonical_sha256(modules) != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise SystemExit("Loaded module receipt drifted.")
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and canonical_sha256(signatures) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise SystemExit("Runtime signatures drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and canonical_sha256(relocated) != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise SystemExit("Relocated observations drifted.")
    cases = []
    fact_hashes = {}
    for definition in definitions():
        identifier = definition["id"]
        digest = canonical_sha256(facts[identifier])
        fact_hashes[identifier] = digest
        cases.append({**definition, "python": {"facts": facts[identifier], "facts_sha256": digest, "outcome": "observed"}})
    case_hashes = {case["id"]: canonical_sha256(case) for case in cases}
    aggregate = canonical_sha256(cases)
    if EXPECTED_FACT_SHA256 and fact_hashes != EXPECTED_FACT_SHA256:
        raise SystemExit("Fact hashes drifted.")
    if EXPECTED_CASE_SHA256 and case_hashes != EXPECTED_CASE_SHA256:
        raise SystemExit("Case hashes drifted.")
    if EXPECTED_CASES_SHA256 and aggregate != EXPECTED_CASES_SHA256:
        raise SystemExit("Aggregate case hash drifted.")
    partition_hashes = {name: canonical_sha256(rows) for name, rows in inventory["partitions"].items()}
    contract = {
        "adaptations": {symbol: "semantic-public-collection-route" if CLASSIFICATIONS[symbol] == "equivalent" else "typed-native-collection-adaptation" for symbol in TARGET_SYMBOLS},
        "assertion_ids": {symbol: f"imugi-idf-object-list-core-{next(index for index, candidate in TARGET_IDENTITIES if candidate == symbol)}-{hashlib.sha256(symbol.encode()).hexdigest()[:8]}" for symbol in TARGET_SYMBOLS},
        "classification_counts": dict(sorted(Counter(CLASSIFICATIONS.values()).items())),
        "classifications": CLASSIFICATIONS,
        "closure": {"batch1_count": 40, "batch2_count": 21, "batch3_count": 25, "exact_disjoint_source_partition": True, "out_of_scope_count": 28, "partition_receipts_sha256": partition_hashes, "source_declaration_count": 133, "source_indices": list(SOURCE_INDICES), "target_count": 19, "target_indices": list(TARGET_INDICES)},
        "evidence_contract": {"active_energyplus_process_claim": False, "expected_receipt_count": 19, "internal_native_route_claim": False, "native_runtime_executed_by_python_oracle": False, "path_independent_relocated_import": True, "python_api_or_source_compatibility_claim": False, "structural_only": False},
        "native_routes": {symbol: _route(symbol) for symbol in TARGET_SYMBOLS},
        "runtime_signatures": signatures,
    }
    result = {
        "case_sha256": case_hashes, "cases": cases, "cases_sha256": aggregate, "consumer_contract": contract,
        "fact_sha256": fact_hashes, "native_review": _native_review(), "partitions": inventory["partitions"],
        "runtime": base.base._runtime_receipt(), "schema": SCHEMA,
        "support": [{**pin, "role": "immutable-existing-imugi-support"} for pin in SUPPORT],
        "symbols": inventory["symbols"], "target_receipts": inventory["target_receipts"],
        "upstream": {"commit": EXPECTED_UPSTREAM_COMMIT, "inventory": {"bytes": base.EXPECTED_INVENTORY_BYTES, "content_sha256": base.EXPECTED_INVENTORY_SHA256, "file_sha256": base.EXPECTED_INVENTORY_FILE_SHA256}, "isolated_import": {"loaded_local_modules": modules, "loaded_local_modules_sha256": canonical_sha256(modules), "relocated_observations_sha256": canonical_sha256(relocated), "relocated_source_copy": "two-byte-identical-repository-temp-copies", "source_location_count": 2}, "source": {"ast_sha256": base.EXPECTED_SOURCE_AST_SHA256, "bytes": base.EXPECTED_SOURCE_BYTES, "path": SOURCE_PATH, "source_sha256": base.EXPECTED_SOURCE_SHA256}},
    }
    validate_oracle(result)
    return result


def validate_oracle(value: dict[str, Any]) -> None:
    expected = {"case_sha256", "cases", "cases_sha256", "consumer_contract", "fact_sha256", "native_review", "partitions", "runtime", "schema", "support", "symbols", "target_receipts", "upstream"}
    if set(value) != expected or value["schema"] != SCHEMA or len(value["target_receipts"]) != 19:
        raise RuntimeError("Final oracle root/count drifted.")
    if [(item["inventory_index"], item["symbol"]) for item in value["target_receipts"]] != list(TARGET_IDENTITIES):
        raise RuntimeError("Final target identities drifted.")
    if value["runtime"] != base.base._runtime_receipt():
        raise RuntimeError("Pinned generation runtime drifted.")
    for name, rows in value["partitions"].items():
        if canonical_sha256(rows) != EXPECTED_PARTITION_SHA256[name]:
            raise RuntimeError(f"Final partition drifted: {name}")
    if sorted(item["inventory_index"] for rows in value["partitions"].values() for item in rows) != list(SOURCE_INDICES):
        raise RuntimeError("Final full-source closure drifted.")
    for case, definition in zip(value["cases"], definitions(), strict=True):
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError("Case definition drifted.")
        digest = canonical_sha256(case["python"]["facts"])
        if digest != case["python"]["facts_sha256"] or digest != value["fact_sha256"][case["id"]] or canonical_sha256(case) != value["case_sha256"][case["id"]]:
            raise RuntimeError("Case hash layer drifted.")
    if canonical_sha256(value["cases"]) != value["cases_sha256"]:
        raise RuntimeError("Aggregate case hash drifted.")
    if EXPECTED_FACT_SHA256 and value["fact_sha256"] != EXPECTED_FACT_SHA256:
        raise RuntimeError("Pinned fact layer drifted.")
    if EXPECTED_CASE_SHA256 and value["case_sha256"] != EXPECTED_CASE_SHA256:
        raise RuntimeError("Pinned case layer drifted.")
    if EXPECTED_CASES_SHA256 and value["cases_sha256"] != EXPECTED_CASES_SHA256:
        raise RuntimeError("Pinned aggregate drifted.")
    if value["consumer_contract"]["classification_counts"] != {"equivalent": 4, "exception": 15}:
        raise RuntimeError("Classification counts drifted.")
    review = value["native_review"]
    if review["classifications"] != CLASSIFICATIONS or review["native_routes"] != {symbol: _route(symbol) for symbol in TARGET_SYMBOLS}:
        raise RuntimeError("Native public route review drifted.")
    evidence = value["consumer_contract"]["evidence_contract"]
    if evidence["active_energyplus_process_claim"] or evidence["internal_native_route_claim"] or evidence["python_api_or_source_compatibility_claim"] or evidence["structural_only"]:
        raise RuntimeError("Evidence claims drifted.")
    if value["upstream"]["isolated_import"]["source_location_count"] != 2 or value["upstream"]["isolated_import"]["loaded_local_modules_sha256"] != EXPECTED_LOADED_LOCAL_MODULES_SHA256:
        raise RuntimeError("Relocation receipt drifted.")
    if EXPECTED_RUNTIME_SIGNATURES_SHA256 and canonical_sha256(value["consumer_contract"]["runtime_signatures"]) != EXPECTED_RUNTIME_SIGNATURES_SHA256:
        raise RuntimeError("Runtime signature pin drifted.")
    if EXPECTED_RELOCATED_OBSERVATIONS_SHA256 and value["upstream"]["isolated_import"]["relocated_observations_sha256"] != EXPECTED_RELOCATED_OBSERVATIONS_SHA256:
        raise RuntimeError("Relocated observation pin drifted.")
    base.base._validate_safe_tree(value)
    encoded = strict_json_dumps(value, indent=2)
    if strict_json_dumps(base.base.load_json_without_duplicates_text(encoded), indent=2) != encoded:
        raise RuntimeError("Strict JSON round trip drifted.")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    base.base._validate_generation_runtime()
    result = build_oracle(load_exact_inventory(args.inventory, args.upstream_commit), args.upstream_commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(strict_json_dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    counts = Counter(CLASSIFICATIONS.values())
    print(f"Wrote {len(result['cases'])} final Imugi cases for 19 targets: {counts['equivalent']} equivalent, {counts['exception']} exception.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

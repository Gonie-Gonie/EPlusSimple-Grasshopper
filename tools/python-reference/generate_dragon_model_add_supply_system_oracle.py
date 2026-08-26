"""Generate bounded observations for legacy ``EnergyModel.add_supply_system``.

The three cases isolate the upstream method's generation, append, and
postprocessor choreography with logical recording doubles around a real pinned
``IDF``.  They support one reviewed native model-context adaptation without
claiming closure for ``EnergyModel.to_idf``, ``SupplyGroup``, concrete supply
systems, or their postprocessors.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import os
from pathlib import Path
import sys
from types import SimpleNamespace
from typing import Any


SCHEMA = "goniegonie.python-reference.dragon-model-add-supply-system.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
MODEL_SOURCE_PATH = "src/idragon/dragon/model.py"
SOURCE_RECEIPTS = (
    (
        "src/idragon/__init__.py",
        "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618",
        "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50",
    ),
    (
        "src/idragon/common.py",
        "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9",
        "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d",
    ),
    (
        "src/idragon/constants.py",
        "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084",
        "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520",
    ),
    (
        "src/idragon/dragon/__init__.py",
        "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52",
        "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a",
    ),
    (
        "src/idragon/dragon/construction.py",
        "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a",
        "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622",
    ),
    (
        "src/idragon/dragon/hvac.py",
        "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31",
        "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0",
    ),
    (
        MODEL_SOURCE_PATH,
        "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59",
        "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
    ),
    (
        "src/idragon/dragon/profile.py",
        "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef",
        "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445",
    ),
    (
        "src/idragon/dragon/shape.py",
        "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2",
        "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c",
    ),
    (
        "src/idragon/imugi.py",
        "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90",
        "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613",
    ),
    (
        "src/idragon/launcher.py",
        "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e",
        "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f",
    ),
    (
        "src/idragon/utils.py",
        "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452",
        "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd",
    ),
)
EXPECTED_SYMBOL_RECEIPTS = {
    "EnergyModel.add_supply_system": {
        "body_hash": "sha256:6bf509a4d5050f54bd748c516ed98b6ae249edf3aaa84a75c4c7bd11b7fbef4b",
        "kind": "function",
        "signature_hash": "sha256:576bb4584970582d94ae80ad061612e84dad263321a9e6288b39a92af7cd959f",
        "symbol_hash": "sha256:174532d0aa6b76826dd78f3d7020ba49eeba26494019da3fb361396e31c15a94",
    }
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
ADAPTATION = "model-context-supply-system-assembly"
ASSERTION_ID = "dragon-model-add-supply-system-174532d0"
NATIVE_TARGET = "EnergyModel.ToIdfDocument"
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-model-add-supply-system.add-supply-system.append-then-processor-failure",
        "energy-model-add-supply-system",
        "EnergyModel.add_supply_system",
    ),
    (
        "dragon-model-add-supply-system.add-supply-system.generation-failure-before-mutation",
        "energy-model-add-supply-system",
        "EnergyModel.add_supply_system",
    ),
    (
        "dragon-model-add-supply-system.add-supply-system.success-return-and-order",
        "energy-model-add-supply-system",
        "EnergyModel.add_supply_system",
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 3
EXPECTED_CASE_COUNTS = {"EnergyModel.add_supply_system": 3}
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
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_model_assembly_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_goniegonie_add_supply_system_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load add-supply-system support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    observed_receipts = tuple(
        (
            source["path"],
            source["ast_sha256"],
            source["source_sha256"],
        )
        for source in module.SOURCE_SPECS
    )
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or observed_receipts != SOURCE_RECEIPTS
    ):
        raise RuntimeError("Add-supply-system support is not exactly pinned.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
load_json_without_duplicates = SUPPORT.SUPPORT.load_json_without_duplicates
RAW_ADDRESS_PATTERN = SUPPORT.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = SUPPORT.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = SUPPORT.GUID_PATTERN
TIMESTAMP_PATTERN = SUPPORT.TIMESTAMP_PATTERN

SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_sha256,
        "path": path,
        "source_sha256": source_sha256,
        "symbols": (
            ("EnergyModel.add_supply_system",)
            if path == MODEL_SOURCE_PATH
            else ()
        ),
    }
    for path, ast_sha256, source_sha256 in SOURCE_RECEIPTS
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _load_source_inventory(
    path: Path,
    upstream_commit: str,
    source: dict[str, Any],
) -> dict[str, Any]:
    symbols = tuple(source["symbols"])
    inventory_support = SUPPORT.SUPPORT
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(inventory_support, name) for name in names}
    try:
        inventory_support.SOURCE_PATH = source["path"]
        inventory_support.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        inventory_support.EXPECTED_SYMBOL_HASHES = {
            symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"]
            for symbol in symbols
        }
        inventory_support.TARGET_SYMBOLS = symbols
        inventory = inventory_support.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(inventory_support, name, value)

    expected_file = {
        "ast_hash": source["ast_sha256"],
        "content_hash": source["source_sha256"],
        "path": source["path"],
    }
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": source["path"],
            "symbol": symbol,
        }
        for symbol in symbols
    ]
    if inventory["file"] != expected_file or inventory["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return inventory


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    inventories = [
        _load_source_inventory(path, upstream_commit, source)
        for source in SOURCE_SPECS
    ]
    if any(
        item["content_sha256"] != EXPECTED_INVENTORY_SHA256
        for item in inventories
    ):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in inventories],
        "symbols": [symbol for item in inventories for symbol in item["symbols"]],
    }


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    return {
        "executor": executor,
        "expected_dotnet": {
            "adaptation": ADAPTATION,
            "outcome": "returned",
        },
        "id": identifier,
        "symbol": symbol,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(_case(*binding) for binding in EXPECTED_CASE_BINDINGS)


def _object_descriptor(value: Any) -> dict[str, str]:
    return {
        "name": value["Name"],
        "object_type": value.idd.name,
    }


class _RecordingIdf:
    def __init__(self, idf: Any, events: list[dict[str, Any]]) -> None:
        self.idf = idf
        self.events = events
        self.append_call_count = 0

    def append(self, *objects: Any) -> None:
        self.append_call_count += 1
        self.events.append(
            {
                "event": "idf.append",
                "objects": [_object_descriptor(value) for value in objects],
            }
        )
        self.idf.append(*objects)

    def zone_names(self) -> list[str]:
        return list(self.idf["Zone"].names)


class _ProbeProcessor:
    def __init__(
        self,
        label: str,
        events: list[dict[str, Any]],
        *,
        failure_message: str | None = None,
    ) -> None:
        self.label = label
        self.events = events
        self.failure_message = failure_message

    def run(self, idf: _RecordingIdf) -> None:
        self.events.append(
            {
                "event": "processor.run",
                "processor": self.label,
                "zone_names": idf.zone_names(),
            }
        )
        if self.failure_message is not None:
            raise RuntimeError(self.failure_message)


class _ProbeSupply:
    def __init__(
        self,
        events: list[dict[str, Any]],
        objects: list[Any],
        processors: list[_ProbeProcessor],
        *,
        failure_message: str | None = None,
    ) -> None:
        self.events = events
        self.objects = objects
        self.processors = processors
        self.failure_message = failure_message
        self.generation_count = 0

    def to_idf_object(self, zone: SimpleNamespace) -> tuple[list[Any], list[Any]]:
        self.generation_count += 1
        self.events.append(
            {
                "event": "supply.to_idf_object",
                "zone_name": zone.name,
            }
        )
        if self.failure_message is not None:
            raise RuntimeError(self.failure_message)
        return self.objects, self.processors


def _error_facts(error: RuntimeError, prefix: str) -> dict[str, Any]:
    return {
        "args": [str(value) for value in error.args],
        "message": str(error),
        "message_prefix": prefix,
        "message_starts_with_prefix": str(error).startswith(prefix),
        "type": type(error).__name__,
    }


def _append_event(first: str, second: str) -> dict[str, Any]:
    return {
        "event": "idf.append",
        "objects": [
            {"name": first, "object_type": "Zone"},
            {"name": second, "object_type": "Zone"},
        ],
    }


def _processor_event(label: str, names: list[str]) -> dict[str, Any]:
    return {
        "event": "processor.run",
        "processor": label,
        "zone_names": names,
    }


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        names = [
            "Existing-Zone",
            "Failure-Appended-First",
            "Failure-Appended-Second",
        ]
        message = "processor-failure: intentional failure after append"
        return {
            "append_call_count": 1,
            "error": {
                "args": [message],
                "message": message,
                "message_prefix": "processor-failure:",
                "message_starts_with_prefix": True,
                "type": "RuntimeError",
            },
            "events": [
                {
                    "event": "supply.to_idf_object",
                    "zone_name": "Processor-Failure-Zone",
                },
                _append_event(
                    "Failure-Appended-First",
                    "Failure-Appended-Second",
                ),
                _processor_event("observer-before-failure", names),
                _processor_event("failing-processor", names),
            ],
            "mutation_state": "appended-before-processor-error",
            "processor_labels_run": [
                "observer-before-failure",
                "failing-processor",
            ],
            "return": {"kind": "not-returned"},
            "supply_generation_count": 1,
            "unreached_processor_ran": False,
            "zone_names_after": names,
        }
    if identifier == EXPECTED_CASE_IDS[1]:
        message = "generation-failure: intentional failure before append"
        return {
            "append_call_count": 0,
            "error": {
                "args": [message],
                "message": message,
                "message_prefix": "generation-failure:",
                "message_starts_with_prefix": True,
                "type": "RuntimeError",
            },
            "events": [
                {
                    "event": "supply.to_idf_object",
                    "zone_name": "Generation-Failure-Zone",
                }
            ],
            "mutation_state": "unchanged-before-generation-error",
            "processor_labels_run": [],
            "return": {"kind": "not-returned"},
            "supply_generation_count": 1,
            "unreached_processor_ran": False,
            "zone_names_after": ["Existing-Zone"],
        }
    if identifier == EXPECTED_CASE_IDS[2]:
        names = [
            "Existing-Zone",
            "Success-Appended-First",
            "Success-Appended-Second",
        ]
        return {
            "append_call_count": 1,
            "error": {"kind": "none"},
            "events": [
                {
                    "event": "supply.to_idf_object",
                    "zone_name": "Success-Zone",
                },
                _append_event(
                    "Success-Appended-First",
                    "Success-Appended-Second",
                ),
                _processor_event("first-processor", names),
                _processor_event("second-processor", names),
            ],
            "mutation_state": "appended-before-ordered-processors",
            "processor_labels_run": ["first-processor", "second-processor"],
            "return": {"kind": "none"},
            "supply_generation_count": 1,
            "unreached_processor_ran": False,
            "zone_names_after": names,
        }
    raise RuntimeError(f"Unknown add-supply-system case: {identifier}")


def _new_recording_idf(
    modules: SimpleNamespace,
    events: list[dict[str, Any]],
) -> _RecordingIdf:
    idf = modules.imugi.IDF((24, 2, 0))
    idf.append(modules.imugi.IdfObject("Zone", ["Existing-Zone"]))
    return _RecordingIdf(idf, events)


def _zones(modules: SimpleNamespace, first: str, second: str) -> list[Any]:
    return [
        modules.imugi.IdfObject("Zone", [first]),
        modules.imugi.IdfObject("Zone", [second]),
    ]


def _execute_case(
    identifier: str,
    modules: SimpleNamespace,
) -> tuple[str, dict[str, Any]]:
    events: list[dict[str, Any]] = []
    idf = _new_recording_idf(modules, events)

    if identifier == EXPECTED_CASE_IDS[0]:
        failure_message = "processor-failure: intentional failure after append"
        processors = [
            _ProbeProcessor("observer-before-failure", events),
            _ProbeProcessor(
                "failing-processor",
                events,
                failure_message=failure_message,
            ),
            _ProbeProcessor("unreached-processor", events),
        ]
        supply = _ProbeSupply(
            events,
            _zones(
                modules,
                "Failure-Appended-First",
                "Failure-Appended-Second",
            ),
            processors,
        )
        try:
            modules.model.EnergyModel.add_supply_system(
                idf,
                SimpleNamespace(name="Processor-Failure-Zone"),
                supply,
            )
        except RuntimeError as error:
            facts = {
                "append_call_count": idf.append_call_count,
                "error": _error_facts(error, "processor-failure:"),
                "events": events,
                "mutation_state": "appended-before-processor-error",
                "processor_labels_run": [
                    item["processor"]
                    for item in events
                    if item["event"] == "processor.run"
                ],
                "return": {"kind": "not-returned"},
                "supply_generation_count": supply.generation_count,
                "unreached_processor_ran": any(
                    item.get("processor") == "unreached-processor"
                    for item in events
                ),
                "zone_names_after": idf.zone_names(),
            }
            return "raised", facts
        raise RuntimeError("Expected processor failure was not raised.")

    if identifier == EXPECTED_CASE_IDS[1]:
        failure_message = "generation-failure: intentional failure before append"
        supply = _ProbeSupply(
            events,
            [],
            [],
            failure_message=failure_message,
        )
        try:
            modules.model.EnergyModel.add_supply_system(
                idf,
                SimpleNamespace(name="Generation-Failure-Zone"),
                supply,
            )
        except RuntimeError as error:
            facts = {
                "append_call_count": idf.append_call_count,
                "error": _error_facts(error, "generation-failure:"),
                "events": events,
                "mutation_state": "unchanged-before-generation-error",
                "processor_labels_run": [],
                "return": {"kind": "not-returned"},
                "supply_generation_count": supply.generation_count,
                "unreached_processor_ran": False,
                "zone_names_after": idf.zone_names(),
            }
            return "raised", facts
        raise RuntimeError("Expected generation failure was not raised.")

    if identifier == EXPECTED_CASE_IDS[2]:
        processors = [
            _ProbeProcessor("first-processor", events),
            _ProbeProcessor("second-processor", events),
        ]
        supply = _ProbeSupply(
            events,
            _zones(
                modules,
                "Success-Appended-First",
                "Success-Appended-Second",
            ),
            processors,
        )
        returned = modules.model.EnergyModel.add_supply_system(
            idf,
            SimpleNamespace(name="Success-Zone"),
            supply,
        )
        facts = {
            "append_call_count": idf.append_call_count,
            "error": {"kind": "none"},
            "events": events,
            "mutation_state": "appended-before-ordered-processors",
            "processor_labels_run": [
                item["processor"]
                for item in events
                if item["event"] == "processor.run"
            ],
            "return": {"kind": "none"} if returned is None else {"kind": "other"},
            "supply_generation_count": supply.generation_count,
            "unreached_processor_ran": False,
            "zone_names_after": idf.zone_names(),
        }
        return "returned", facts

    raise RuntimeError(f"Unknown add-supply-system case: {identifier}")


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


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


def _expected_files() -> list[dict[str, Any]]:
    return [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_symbol_descriptors() -> list[dict[str, Any]]:
    return [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": MODEL_SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "loaded_local_modules": _expected_loaded_local_modules(),
        "sources": [
            {
                "ast_sha256": source["ast_sha256"],
                "path": source["path"],
                "source_sha256": source["source_sha256"],
            }
            for source in SOURCE_SPECS
        ],
    }


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": {"EnergyModel.add_supply_system": ADAPTATION},
        "assertion_ids": {"EnergyModel.add_supply_system": ASSERTION_ID},
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {"EnergyModel.add_supply_system": "exception"},
        "closure": {
            "full_symbol_closure": False,
            "scope": "bounded-reviewed-adaptation-evidence",
            "unresolved_behavior": [
                "EnergyModel.to_idf",
                "SupplyGroup",
                "concrete-supply-systems",
                "supply-system-postprocessors",
            ],
        },
        "identity_encoding": "logical-labels-only-no-id-or-address",
        "native_targets": {"EnergyModel.add_supply_system": NATIVE_TARGET},
        "source_import_policy": (
            "external-temporary-copy-with-complete-loaded-local-module-audit"
        ),
        "state_encoding": "ordered-logical-events-and-object-names",
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
    return source_root / Path(str(source["path"])).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        source_root = Path(entry)
        if all(
            _source_file(source_root, source).is_file()
            and sha256_file(_source_file(source_root, source))
            == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(source_root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source_root: Path | None = None,
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
    }:
        raise SystemExit("The aggregate add-supply-system inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        cases: list[dict[str, Any]] = []
        for definition in case_definitions():
            outcome, facts = _execute_case(definition["id"], modules)
            if facts != expected_facts(definition["id"]):
                raise SystemExit(
                    "Pinned Python add-supply-system semantics drifted: "
                    + definition["id"]
                    + "\n"
                    + strict_json_dumps(facts, indent=2)
                )
            case = dict(definition)
            case["python"] = {"facts": facts, "outcome": outcome}
            cases.append(case)
    loaded_local_modules = modules.loaded_local_modules

    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": _expected_consumer_contract(),
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
        "upstream": {
            **_expected_upstream(),
            "commit": commit,
            "loaded_local_modules": loaded_local_modules,
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
            "cases",
            "cases_sha256",
            "consumer_contract",
            "runtime",
            "schema",
            "symbols",
            "upstream",
        },
        "root",
    )
    if value["schema"] != SCHEMA:
        raise RuntimeError("Add-supply-system schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Add-supply-system cases hash drifted.")
    _validate_safe_tree(value)

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Add-supply-system case order/count drifted.")
    if [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Add-supply-system case order/count drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Pinned add-supply-system case IDs are not sorted.")
    if len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Pinned add-supply-system case IDs are not unique.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Add-supply-system per-symbol case counts drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    expected_outcomes = {
        EXPECTED_CASE_IDS[0]: "raised",
        EXPECTED_CASE_IDS[1]: "raised",
        EXPECTED_CASE_IDS[2]: "returned",
    }
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Add-supply-system case contract drifted: {case['id']}")
        _require_keys(
            case["expected_dotnet"],
            {"adaptation", "outcome"},
            "expected_dotnet",
        )
        if case["expected_dotnet"] != {
            "adaptation": ADAPTATION,
            "outcome": "returned",
        }:
            raise RuntimeError(f"Expected .NET contract drifted: {case['id']}")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != expected_outcomes[case["id"]]:
            raise RuntimeError(f"Python case outcome drifted: {case['id']}")
        if case["python"]["facts"] != expected_facts(case["id"]):
            raise RuntimeError(f"Add-supply-system semantics drifted: {case['id']}")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Add-supply-system consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Add-supply-system runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Add-supply-system upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Add-supply-system symbol receipts drifted.")
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
    serialized = strict_json_dumps(result, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8", newline="\n")
    print(f"Wrote dragon-model add-supply-system oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

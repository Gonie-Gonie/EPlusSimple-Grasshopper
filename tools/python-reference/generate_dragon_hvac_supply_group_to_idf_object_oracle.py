"""Generate bounded observations for ``SupplyGroup.to_idf_object``.

The corpus fixes the legacy container orchestration contract only.  Concrete
supply-system conversion, postprocessor execution, the SupplyGroup class
receipt, and full EnergyModel conversion remain separate review boundaries.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import os
from pathlib import Path
import sys
from typing import Any


SCHEMA = "dragons.python-reference.dragon-hvac-supply-group-to-idf-object.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
HVAC_SOURCE_PATH = "src/idragon/dragon/hvac.py"
TARGET_SYMBOL = "SupplyGroup.to_idf_object"
TARGET_SYMBOLS = (TARGET_SYMBOL,)
EXPECTED_SYMBOL_RECEIPTS = {
    TARGET_SYMBOL: {
        "body_hash": "sha256:8660a470290bde21a0cc246e107e2362b5698153e7585ea05a1a69367b1342fa",
        "kind": "function",
        "signature_hash": "sha256:1dd75b2e8cc87cb78c35a6df6c2423c532b8ea9e29f24b53d113cdffdd42d2ec",
        "symbol_hash": "sha256:3f9c508c5b0d784d27bc327dfe65c84bd7d17ffc144615b852c37b59cbe51a41",
    }
}
ADAPTATION = "model-context-supply-group-idf-assembly"
ASSERTION_ID = "dragon-hvac-supply-group-to-idf-object-3f9c508c"
NATIVE_TARGET = "EnergyModel.ToIdfDocument"
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-hvac-supply-group-to-idf-object.availability-failure.immediate-after-system",
        "supply-group-to-idf-object",
        TARGET_SYMBOL,
    ),
    (
        "dragon-hvac-supply-group-to-idf-object.success.flatten-order-controller-last-and-fresh-lists",
        "supply-group-to-idf-object",
        TARGET_SYMBOL,
    ),
    (
        "dragon-hvac-supply-group-to-idf-object.system-failure.prefix-before-failure",
        "supply-group-to-idf-object",
        TARGET_SYMBOL,
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 3
EXPECTED_CASE_COUNTS = {TARGET_SYMBOL: 3}


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_dragons_supply_group_to_idf_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load SupplyGroup support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
    ):
        raise RuntimeError("SupplyGroup core support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
SOURCE_RECEIPTS = CORE.SOURCE_RECEIPTS
SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == HVAC_SOURCE_PATH else (),
    }
    for path, ast_hash, source_hash in SOURCE_RECEIPTS
)
EXPECTED_DEPENDENCIES = CORE.EXPECTED_DEPENDENCIES
REQUIRED_PYTHON = CORE.REQUIRED_PYTHON
REQUIRED_HASH_ALGORITHM = CORE.REQUIRED_HASH_ALGORITHM
REQUIRED_HASH_WIDTH_BITS = CORE.REQUIRED_HASH_WIDTH_BITS
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


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
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
            symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"]
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
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": source["path"],
            "symbol": symbol,
        }
        for symbol in source["symbols"]
    ]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    items = [_load_source_inventory(path, commit, source) for source in SOURCE_SPECS]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in items):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in items],
        "symbols": [symbol for item in items for symbol in item["symbols"]],
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "executor": executor,
            "expected_dotnet": {"adaptation": ADAPTATION, "outcome": "returned"},
            "id": identifier,
            "symbol": symbol,
        }
        for identifier, executor, symbol in EXPECTED_CASE_BINDINGS
    )


class _LogicalValue:
    def __init__(self, label: str) -> None:
        self.label = label


class _LogicalZone:
    def __init__(self, label: str) -> None:
        self.label = label
        self.name = label


class _LogicalAvailability:
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
        self.call_count = 0
        self.created: list[_LogicalValue] = []

    def to_idf_object(self) -> _LogicalValue:
        self.call_count += 1
        self.events.append(
            {
                "availability": self.label,
                "event": "availability.to_idf_object",
                "group_call": self.call_count,
            }
        )
        if self.failure_message is not None:
            raise RuntimeError(self.failure_message)
        value = _LogicalValue(f"{self.label}-object")
        self.created.append(value)
        return value


def _probe_type(hvac: Any) -> type[Any]:
    class ProbeSupply(hvac.SupplySystem):
        def __init__(
            self,
            label: str,
            heatable: bool,
            coolable: bool,
            events: list[dict[str, Any]],
            object_labels: tuple[str, ...],
            processor_labels: tuple[str, ...],
            *,
            failure_message: str | None = None,
        ) -> None:
            self.label = label
            self.name = label
            self._heatable = heatable
            self._coolable = coolable
            self.events = events
            self.object_labels = object_labels
            self.processor_labels = processor_labels
            self.failure_message = failure_message
            self.call_count = 0
            self.expected_zone: Any = None
            self.expected_availability: Any = None
            self.created_objects: list[list[_LogicalValue]] = []
            self.created_processors: list[list[_LogicalValue]] = []

        @property
        def heatable(self) -> bool:
            self.events.append(
                {
                    "event": "capability.read",
                    "group_call": self.call_count + 1,
                    "property": "heatable",
                    "system": self.label,
                    "value": self._heatable,
                }
            )
            return self._heatable

        @property
        def coolable(self) -> bool:
            self.events.append(
                {
                    "event": "capability.read",
                    "group_call": self.call_count + 1,
                    "property": "coolable",
                    "system": self.label,
                    "value": self._coolable,
                }
            )
            return self._coolable

        @property
        def idf_objtypename(self) -> str:
            return "ProbeSupply"

        def to_idf_object(
            self,
            zone: Any,
            for_heating: bool,
            for_cooling: bool,
            availability: Any = None,
        ) -> tuple[list[Any], list[Any]]:
            self.call_count += 1
            self.events.append(
                {
                    "availability": (
                        None if availability is None else availability.label
                    ),
                    "availability_identity_aligned": (
                        availability is self.expected_availability
                    ),
                    "event": "system.to_idf_object",
                    "for_cooling": for_cooling,
                    "for_heating": for_heating,
                    "group_call": self.call_count,
                    "system": self.label,
                    "zone": zone.label,
                    "zone_identity_aligned": zone is self.expected_zone,
                }
            )
            if self.failure_message is not None:
                raise RuntimeError(self.failure_message)
            objects = [_LogicalValue(label) for label in self.object_labels]
            processors = [_LogicalValue(label) for label in self.processor_labels]
            self.created_objects.append(objects)
            self.created_processors.append(processors)
            return objects, processors

    return ProbeSupply


def _event_capability(
    group_call: int, system: str, property_name: str, value: bool
) -> dict[str, Any]:
    return {
        "event": "capability.read",
        "group_call": group_call,
        "property": property_name,
        "system": system,
        "value": value,
    }


def _event_system(
    group_call: int,
    system: str,
    heatable: bool,
    coolable: bool,
    availability: str | None,
) -> dict[str, Any]:
    return {
        "availability": availability,
        "availability_identity_aligned": True,
        "event": "system.to_idf_object",
        "for_cooling": coolable,
        "for_heating": heatable,
        "group_call": group_call,
        "system": system,
        "zone": "zone-main",
        "zone_identity_aligned": True,
    }


def _event_availability(group_call: int, availability: str) -> dict[str, Any]:
    return {
        "availability": availability,
        "event": "availability.to_idf_object",
        "group_call": group_call,
    }


def _success_events(group_call: int) -> list[dict[str, Any]]:
    return [
        _event_capability(group_call, "heat-only", "heatable", True),
        _event_capability(group_call, "heat-only", "coolable", False),
        _event_system(group_call, "heat-only", True, False, "availability-heat"),
        _event_availability(group_call, "availability-heat"),
        _event_capability(group_call, "both", "heatable", True),
        _event_capability(group_call, "both", "coolable", True),
        _event_system(group_call, "both", True, True, None),
        _event_capability(group_call, "cool-only", "heatable", False),
        _event_capability(group_call, "cool-only", "coolable", True),
        _event_system(group_call, "cool-only", False, True, "availability-cool"),
        _event_availability(group_call, "availability-cool"),
    ]


def _error(message: str) -> dict[str, Any]:
    return {
        "args": [message],
        "message": message,
        "outcome": "raised",
        "type": "RuntimeError",
    }


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return {
            "created_object_labels_before_failure": ["first-object"],
            "created_processor_labels_before_failure": ["first-processor"],
            "error": _error("availability-failure:first"),
            "events": [
                _event_capability(1, "first", "heatable", True),
                _event_capability(1, "first", "coolable", True),
                _event_system(1, "first", True, True, "availability-first"),
                _event_availability(1, "availability-first"),
            ],
            "failing_availability_call_count": 1,
            "first_system_call_count": 1,
            "returned_lists_observed": False,
            "second_availability_call_count": 0,
            "second_system_call_count": 0,
            "sequential_controller_returned": False,
        }
    if identifier == EXPECTED_CASE_IDS[1]:
        objects = [
            "heat-object-first",
            "heat-object-second",
            "availability-heat-object",
            "both-object",
            "cool-object",
            "availability-cool-object",
        ]
        processors = [
            "heat-processor",
            "both-processor-first",
            "both-processor-second",
            "cool-processor",
            "SequentialLoadFractionController",
        ]
        return {
            "all_availability_identities_aligned": True,
            "all_zone_identities_aligned": True,
            "availability_objects_immediately_follow_owner": True,
            "capability_read_order": ["heatable", "coolable"] * 6,
            "child_objects_fresh": True,
            "child_processors_fresh": True,
            "events": _success_events(1) + _success_events(2),
            "first_object_labels": objects,
            "first_processor_labels": processors,
            "fresh_object_list": True,
            "fresh_processor_list": True,
            "fresh_sequential_controller": True,
            "object_result_type": "list",
            "processor_result_type": "list",
            "second_object_labels": objects,
            "second_processor_labels": processors,
            "sequential_controller_group_identity": True,
            "sequential_controller_last": True,
            "sequential_controller_zone_identity": True,
        }
    if identifier == EXPECTED_CASE_IDS[2]:
        return {
            "created_object_labels_before_failure": [
                "first-object-first",
                "first-object-second",
                "availability-first-object",
            ],
            "created_processor_labels_before_failure": ["first-processor"],
            "error": _error("system-failure:second"),
            "events": [
                _event_capability(1, "first", "heatable", True),
                _event_capability(1, "first", "coolable", False),
                _event_system(1, "first", True, False, "availability-first"),
                _event_availability(1, "availability-first"),
                _event_capability(1, "second", "heatable", True),
                _event_capability(1, "second", "coolable", True),
                _event_system(1, "second", True, True, "availability-second"),
            ],
            "first_availability_call_count": 1,
            "first_system_call_count": 1,
            "returned_lists_observed": False,
            "second_availability_call_count": 0,
            "second_system_call_count": 1,
            "sequential_controller_returned": False,
            "third_availability_call_count": 0,
            "third_system_call_count": 0,
        }
    raise RuntimeError(f"Unknown SupplyGroup.to_idf_object case: {identifier}")


def _attempt(function: Any) -> tuple[Any, dict[str, Any]]:
    try:
        return function(), {"kind": "none"}
    except Exception as error:
        return None, {
            "args": [str(value) for value in error.args],
            "message": str(error),
            "outcome": "raised",
            "type": type(error).__name__,
        }


def _labels(values: list[Any], hvac: Any) -> list[str]:
    return [
        "SequentialLoadFractionController"
        if isinstance(value, hvac.SequentialLoadFractionController)
        else value.label
        for value in values
    ]


def _execute_case(identifier: str, modules: Any) -> tuple[dict[str, Any], str]:
    hvac = modules.hvac
    Probe = _probe_type(hvac)
    events: list[dict[str, Any]] = []
    zone = _LogicalZone("zone-main")

    if identifier == EXPECTED_CASE_IDS[0]:
        failing = _LogicalAvailability(
            "availability-first", events, failure_message="availability-failure:first"
        )
        second_availability = _LogicalAvailability("availability-second", events)
        first = Probe(
            "first", True, True, events, ("first-object",), ("first-processor",)
        )
        second = Probe("second", False, True, events, ("second-object",), ())
        first.expected_zone = second.expected_zone = zone
        first.expected_availability = failing
        second.expected_availability = second_availability
        group = hvac.SupplyGroup(
            [first, second], availabilities=[failing, second_availability]
        )
        events.clear()
        result, error = _attempt(lambda: group.to_idf_object(zone))
        facts = {
            "created_object_labels_before_failure": [
                value.label for batch in first.created_objects for value in batch
            ],
            "created_processor_labels_before_failure": [
                value.label for batch in first.created_processors for value in batch
            ],
            "error": error,
            "events": events,
            "failing_availability_call_count": failing.call_count,
            "first_system_call_count": first.call_count,
            "returned_lists_observed": result is not None,
            "second_availability_call_count": second_availability.call_count,
            "second_system_call_count": second.call_count,
            "sequential_controller_returned": False,
        }
        return facts, "raised"

    if identifier == EXPECTED_CASE_IDS[1]:
        availability_heat = _LogicalAvailability("availability-heat", events)
        availability_cool = _LogicalAvailability("availability-cool", events)
        heat = Probe(
            "heat-only",
            True,
            False,
            events,
            ("heat-object-first", "heat-object-second"),
            ("heat-processor",),
        )
        both = Probe(
            "both",
            True,
            True,
            events,
            ("both-object",),
            ("both-processor-first", "both-processor-second"),
        )
        cool = Probe(
            "cool-only",
            False,
            True,
            events,
            ("cool-object",),
            ("cool-processor",),
        )
        for system, availability in zip(
            (heat, both, cool), (availability_heat, None, availability_cool), strict=True
        ):
            system.expected_zone = zone
            system.expected_availability = availability
        group = hvac.SupplyGroup(
            [heat, both, cool],
            availabilities=[availability_heat, None, availability_cool],
        )
        events.clear()
        first_objects, first_processors = group.to_idf_object(zone)
        second_objects, second_processors = group.to_idf_object(zone)
        first_controller = first_processors[-1]
        second_controller = second_processors[-1]
        capability_order = [
            item["property"] for item in events if item["event"] == "capability.read"
        ]
        system_events = [
            item for item in events if item["event"] == "system.to_idf_object"
        ]
        facts = {
            "all_availability_identities_aligned": all(
                item["availability_identity_aligned"] for item in system_events
            ),
            "all_zone_identities_aligned": all(
                item["zone_identity_aligned"] for item in system_events
            ),
            "availability_objects_immediately_follow_owner": _labels(
                first_objects, hvac
            )
            == [
                "heat-object-first",
                "heat-object-second",
                "availability-heat-object",
                "both-object",
                "cool-object",
                "availability-cool-object",
            ],
            "capability_read_order": capability_order,
            "child_objects_fresh": all(
                left is not right
                for left, right in zip(first_objects, second_objects, strict=True)
            ),
            "child_processors_fresh": all(
                left is not right
                for left, right in zip(first_processors, second_processors, strict=True)
            ),
            "events": events,
            "first_object_labels": _labels(first_objects, hvac),
            "first_processor_labels": _labels(first_processors, hvac),
            "fresh_object_list": first_objects is not second_objects,
            "fresh_processor_list": first_processors is not second_processors,
            "fresh_sequential_controller": first_controller is not second_controller,
            "object_result_type": type(first_objects).__name__,
            "processor_result_type": type(first_processors).__name__,
            "second_object_labels": _labels(second_objects, hvac),
            "second_processor_labels": _labels(second_processors, hvac),
            "sequential_controller_group_identity": first_controller.supply is group,
            "sequential_controller_last": isinstance(
                first_controller, hvac.SequentialLoadFractionController
            ),
            "sequential_controller_zone_identity": first_controller.zone is zone,
        }
        return facts, "returned"

    if identifier == EXPECTED_CASE_IDS[2]:
        first_availability = _LogicalAvailability("availability-first", events)
        second_availability = _LogicalAvailability("availability-second", events)
        third_availability = _LogicalAvailability("availability-third", events)
        first = Probe(
            "first",
            True,
            False,
            events,
            ("first-object-first", "first-object-second"),
            ("first-processor",),
        )
        second = Probe(
            "second",
            True,
            True,
            events,
            ("second-object",),
            (),
            failure_message="system-failure:second",
        )
        third = Probe("third", False, True, events, ("third-object",), ())
        availabilities = (first_availability, second_availability, third_availability)
        for system, availability in zip(
            (first, second, third), availabilities, strict=True
        ):
            system.expected_zone = zone
            system.expected_availability = availability
        group = hvac.SupplyGroup(
            [first, second, third], availabilities=list(availabilities)
        )
        events.clear()
        result, error = _attempt(lambda: group.to_idf_object(zone))
        prefix_objects = [
            value.label for batch in first.created_objects for value in batch
        ] + [value.label for value in first_availability.created]
        facts = {
            "created_object_labels_before_failure": prefix_objects,
            "created_processor_labels_before_failure": [
                value.label for batch in first.created_processors for value in batch
            ],
            "error": error,
            "events": events,
            "first_availability_call_count": first_availability.call_count,
            "first_system_call_count": first.call_count,
            "returned_lists_observed": result is not None,
            "second_availability_call_count": second_availability.call_count,
            "second_system_call_count": second.call_count,
            "sequential_controller_returned": False,
            "third_availability_call_count": third_availability.call_count,
            "third_system_call_count": third.call_count,
        }
        return facts, "raised"

    raise RuntimeError(f"Unknown SupplyGroup.to_idf_object case: {identifier}")


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


def _expected_files() -> list[dict[str, str]]:
    return [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_symbol_descriptors() -> list[dict[str, str]]:
    return [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": HVAC_SOURCE_PATH,
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
        "adaptations": {TARGET_SYMBOL: ADAPTATION},
        "assertion_ids": {TARGET_SYMBOL: ASSERTION_ID},
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {TARGET_SYMBOL: "exception"},
        "closure": {
            "full_symbol_closure": False,
            "scope": "bounded-model-context-supply-group-idf-assembly-adaptation",
            "unresolved_behavior": [
                "SupplyGroup",
                "standalone-SupplyGroup-converter-API-shape",
                "SupplySystem.to_idf_object",
                "SourceSystem.to_idf_object",
                "SequentialLoadFractionController",
                "SequentialLoadFractionController.run",
                "concrete-supply-system-converters",
                "supply-system-postprocessor-run-behavior",
                "arbitrary-probe-systems-and-schedules",
                "EnergyModel.to_idf",
            ],
        },
        "identity_encoding": "logical-labels-only-no-id-or-address",
        "native_targets": {TARGET_SYMBOL: NATIVE_TARGET},
        "source_import_policy": (
            "external-temporary-copy-with-complete-loaded-local-module-audit"
        ),
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


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    expected_inventory = {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
    }
    if inventory != expected_inventory:
        raise SystemExit("The aggregate SupplyGroup.to_idf_object inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    with SUPPORT._pinned_modules(imported_root) as modules:
        cases = []
        for definition in case_definitions():
            facts, outcome = _execute_case(definition["id"], modules)
            if facts != expected_facts(definition["id"]):
                raise SystemExit(
                    "Pinned Python SupplyGroup.to_idf_object semantics drifted: "
                    + definition["id"]
                    + "\n"
                    + strict_json_dumps(facts, indent=2)
                )
            case = dict(definition)
            case["python"] = {"facts": facts, "outcome": outcome}
            cases.append(case)
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
    raise RuntimeError(
        f"Unsupported JSON value at {location}: {type(value).__name__}"
    )


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
        raise RuntimeError("SupplyGroup.to_idf_object schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("SupplyGroup.to_idf_object cases hash drifted.")
    _validate_safe_tree(value)
    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("SupplyGroup.to_idf_object case order/count drifted.")
    if (
        list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Pinned SupplyGroup.to_idf_object case IDs drifted.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("SupplyGroup.to_idf_object per-symbol counts drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(
                f"SupplyGroup.to_idf_object case contract drifted: {case['id']}"
            )
        _require_keys(case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        expected_outcome = "returned" if case["id"] == EXPECTED_CASE_IDS[1] else "raised"
        if (
            case["python"]["outcome"] != expected_outcome
            or case["python"]["facts"] != expected_facts(case["id"])
        ):
            raise RuntimeError(
                f"SupplyGroup.to_idf_object semantics drifted: {case['id']}"
            )
    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("SupplyGroup.to_idf_object consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("SupplyGroup.to_idf_object runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("SupplyGroup.to_idf_object upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("SupplyGroup.to_idf_object symbol receipt drifted.")
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
    print(f"Wrote dragon HVAC SupplyGroup.to_idf_object oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

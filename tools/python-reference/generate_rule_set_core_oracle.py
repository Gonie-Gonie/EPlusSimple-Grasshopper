"""Generate pinned observations for the remaining ``RuleSet`` core API.

The corpus is deliberately bounded to three high-value cases for each of the
24 residual public symbols.  Run it only through ``bootstrap_reference.py`` so
imports resolve from the pinned CPython 3.12.7 environment and upstream tree.
"""

from __future__ import annotations

import argparse
import copy
from collections import Counter
import importlib.util
import math
import os
from pathlib import Path
import sys
from typing import Any


SCHEMA = "dragons.invisibledragon.rule-set-core-oracle.v1"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "RuleSet": "sha256:3e0aaca76114e9e5a84d2b6ceb9a650913ad03b5bb6d35c99d3f0a5f97b36994",
    "RuleSet.__deepcopy__": "sha256:058f6012eabebca75ffb65c55f0fc1fccc51995d38521394faaceb32dfbb9748",
    "RuleSet.__init__": "sha256:f1c4b446cbbc826152dae8f4c4677d323271ad408d12fda0b0b527ae9ecaec51",
    "RuleSet.astype": "sha256:0c0d27de9ef57d948f60d77e2ad8ff58f6898f25965135e6584bb0ad65dff226",
    "RuleSet.clip": "sha256:c3bd923567b392c6753dd317132395cea97872100ce08853d218144d579f1ede",
    "RuleSet.friday": "sha256:72220457054927f7999dd905ba93aabf314844b6571266eea6aefbc92823880b",
    "RuleSet.from_constant": "sha256:1093e8f49640c59a592997f0bf053e4a153733c7fab6d2ac36dc913c742e635c",
    "RuleSet.from_days": "sha256:d1d5dd6fce56c158588b0e2ce11671d0063e1e16aee2161b15af5a1c7f5213e9",
    "RuleSet.get_dayschedule": "sha256:51486c906fb24fd537abf5d0f07c77d5ec77c150a9f3874e3f1991d59b1de645",
    "RuleSet.holiday": "sha256:9bbd78bae0f36cfa3af556f39f48a53eb852e8a65e338b0a7ea8235a4861087a",
    "RuleSet.max": "sha256:c62c3676c65897d28e02ae555dd0343582ed8be67411b36fedbef32cca4d3d38",
    "RuleSet.min": "sha256:bf1962353ed21c07ad290a4ff9a5ccd94e7db31b8f7a6313ef3104e280a30807",
    "RuleSet.monday": "sha256:4cca788f61eeb17cb784485a8b94b01ff7679deadfdf1d4d9fe8160abfe54c95",
    "RuleSet.saturday": "sha256:693a3041f2dcd664bea7b574ecea60f7f4b66dce26af8cefbdf49ae356fb71a2",
    "RuleSet.summary": "sha256:f669cea057b58f712dc37b4439991f0fc91aa923ecd1d7b0ab80f8e7cd8cc9fc",
    "RuleSet.sunday": "sha256:cfcbc078846cee7f94d9c09b0f32190b495489b8b8f1f21b860a7cbaa16324fc",
    "RuleSet.thursday": "sha256:2d3bbbc02f1cd354f02d0a564e8c8979970ed867e9f5e61fbd76d5465293f602",
    "RuleSet.to_dict": "sha256:e2a85d522fcc2dbacec768944cf872ba9b7ffd5dd42e03eb4f7cac035da2efff",
    "RuleSet.to_idf_compactexpr": "sha256:015a80b07ad77b088b27d89e7c2f2224553870ad20bffa55a4d43dd1573fb6de",
    "RuleSet.tuesday": "sha256:30f9dc0b522275442a6bff0cc22640539789e05274ad72c7a77b487caebe3e68",
    "RuleSet.type": "sha256:63a5d7d94275c2184f4c9eae268b1c33ee3351525711e7c232fae68f80c84d6a",
    "RuleSet.wednesday": "sha256:a896496ee156854d2a7693128f6a66bac9628457468ef2a7b9a00732abe86a22",
    "RuleSet.weekdays": "sha256:f89fcc578196070c1535586d7c2b7142654dcf2d4b0179c21ee92643e0098294",
    "RuleSet.weekends": "sha256:c3fdd0ae9d51b8f43fa821ee2dd774593c90db66b6c19c4a51e0db0ccfd927b5",
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
EXPECTED_EQUIVALENT_SYMBOLS = frozenset(
    {
        "RuleSet.get_dayschedule",
        "RuleSet.max",
        "RuleSet.min",
        "RuleSet.summary",
        "RuleSet.to_dict",
        "RuleSet.to_idf_compactexpr",
        "RuleSet.type",
    }
)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "RuleSet": "immutable-ruleset-value-object",
    "RuleSet.__deepcopy__": "native-ruleset-deepcopy-memo",
    "RuleSet.__init__": "immutable-deterministic-ruleset-construction",
    "RuleSet.astype": "immutable-ruleset-astype",
    "RuleSet.clip": "immutable-ruleset-clip",
    "RuleSet.friday": "immutable-ruleset-friday-update",
    "RuleSet.from_constant": "deterministic-finite-ruleset-from-constant",
    "RuleSet.from_days": "validated-deterministic-ruleset-from-days",
    "RuleSet.holiday": "immutable-ruleset-holiday-update",
    "RuleSet.monday": "immutable-ruleset-monday-update",
    "RuleSet.saturday": "immutable-ruleset-saturday-update",
    "RuleSet.sunday": "immutable-ruleset-sunday-update",
    "RuleSet.thursday": "immutable-ruleset-thursday-update",
    "RuleSet.tuesday": "immutable-ruleset-tuesday-update",
    "RuleSet.wednesday": "immutable-ruleset-wednesday-update",
    "RuleSet.weekdays": "immutable-ruleset-weekdays-update",
    "RuleSet.weekends": "immutable-ruleset-weekends-update",
}
EXPECTED_CASE_COUNT = 72
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

SLOT_KEYS = (
    "weekdays",
    "weekends",
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
    "holiday",
)
OPTIONAL_SLOT_KEYS = frozenset(SLOT_KEYS[2:])
REQUIRED_SLOT_KEYS = frozenset(SLOT_KEYS[:2])

ORACLE_KEYS = {
    "cases",
    "cases_sha256",
    "consumer_contract",
    "runtime",
    "schema",
    "symbols",
    "upstream",
}
CASE_KEYS = {"executor", "id", "python", "symbol"}
CASE_DEFINITION_KEYS = {"executor", "expected_dotnet", "id", "symbol"}
EXPECTED_DOTNET_KEYS = {"adaptation", "outcome"}
EXPECTED_DOTNET_ERROR_KEYS = {"adaptation", "error_category", "outcome"}
PYTHON_RETURN_KEYS = {"facts", "outcome"}
PYTHON_RAISE_KEYS = {
    "error_category",
    "exception_type",
    "facts",
    "message",
    "outcome",
}
CONSUMER_CONTRACT_KEYS = {
    "adaptations",
    "case_count",
    "case_ids",
    "classifications",
    "float_encoding",
    "runtime_names",
    "target_symbols",
}
RUNTIME_KEYS = {
    "implementation",
    "python_hash_algorithm",
    "python_hash_seed",
    "python_hash_width_bits",
    "python_version",
}
UPSTREAM_KEYS = {"commit", "inventory_sha256", "path", "source_sha256"}
SYMBOL_KEYS = {
    "body_hash",
    "kind",
    "path",
    "signature_hash",
    "symbol",
    "symbol_hash",
}


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_day_schedule_core_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_rule_set_core_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load RuleSet oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
    ):
        raise RuntimeError("RuleSet oracle support is not pinned.")
    return module


BASE = _load_support()
strict_json_dumps = BASE.strict_json_dumps
canonical_sha256 = BASE.canonical_sha256
sha256_file = BASE.sha256_file
AUTO_NAME_PATTERN = BASE.AUTO_NAME_PATTERN
RAW_AUTO_NAME_PATTERN = BASE.RAW_AUTO_NAME_PATTERN


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    """Reuse the hardened full-inventory validator with this exact symbol set."""

    original_hashes = BASE.EXPECTED_SYMBOL_HASHES
    original_symbols = BASE.TARGET_SYMBOLS
    try:
        BASE.EXPECTED_SYMBOL_HASHES = EXPECTED_SYMBOL_HASHES
        BASE.TARGET_SYMBOLS = TARGET_SYMBOLS
        inventory = BASE.load_exact_inventory(path, upstream_commit)
    finally:
        BASE.EXPECTED_SYMBOL_HASHES = original_hashes
        BASE.TARGET_SYMBOLS = original_symbols
    if [item["symbol"] for item in inventory["symbols"]] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover 24 RuleSet symbols.")
    return inventory


def _dotnet(
    adaptation: str,
    outcome: str,
    error_category: str | None = None,
) -> dict[str, str]:
    if adaptation not in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.values():
        raise RuntimeError(f"Unknown RuleSet core adaptation {adaptation!r}.")
    if outcome not in {"raised", "returned"}:
        raise RuntimeError(f"Unknown native outcome {outcome!r}.")
    value = {"adaptation": adaptation, "outcome": outcome}
    if error_category is not None:
        if outcome != "raised" or error_category not in {"domain", "range", "type"}:
            raise RuntimeError("Native error category is not closed and well formed.")
        value["error_category"] = error_category
    return value


def _case(
    identifier: str,
    executor: str,
    symbol: str,
    native_outcome: str = "returned",
    native_error_category: str | None = None,
) -> dict[str, Any]:
    adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
    return {
        "executor": executor,
        "expected_dotnet": None
        if adaptation is None
        else _dotnet(adaptation, native_outcome, native_error_category),
        "id": identifier,
        "symbol": symbol,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = [
        _case("astype.inplace-stale-type", "astype", "RuleSet.astype"),
        _case("astype.outplace-string", "astype", "RuleSet.astype"),
        _case(
            "astype.partial-failure",
            "astype",
            "RuleSet.astype",
            "raised",
            "domain",
        ),
        _case("class.alias-topology", "class", "RuleSet"),
        _case("class.mutable-slot", "class", "RuleSet"),
        _case("class.slot-inventory", "class", "RuleSet"),
        _case("clip.bounds-empty-name", "clip", "RuleSet.clip"),
        _case("clip.inplace", "clip", "RuleSet.clip"),
        _case("clip.reversed", "clip", "RuleSet.clip", "raised", "domain"),
        _case("deepcopy.alias-topology", "deepcopy", "RuleSet.__deepcopy__"),
        _case("deepcopy.memo-hit", "deepcopy", "RuleSet.__deepcopy__"),
        _case("deepcopy.repeated", "deepcopy", "RuleSet.__deepcopy__"),
        _case("from-constant.day-alias", "from-constant", "RuleSet.from_constant"),
        _case(
            "from-constant.nonfinite",
            "from-constant",
            "RuleSet.from_constant",
            "raised",
            "domain",
        ),
        _case(
            "from-constant.scalar-distinct",
            "from-constant",
            "RuleSet.from_constant",
        ),
        _case("from-days.day-ignores-type", "from-days", "RuleSet.from_days"),
        _case(
            "from-days.mixed-types",
            "from-days",
            "RuleSet.from_days",
            "raised",
            "domain",
        ),
        _case("from-days.scalar-overrides", "from-days", "RuleSet.from_days"),
        _case(
            "get-dayschedule.invalid-index",
            "get-dayschedule",
            "RuleSet.get_dayschedule",
        ),
        _case(
            "get-dayschedule.integer-indices",
            "get-dayschedule",
            "RuleSet.get_dayschedule",
        ),
        _case(
            "get-dayschedule.string-fallback",
            "get-dayschedule",
            "RuleSet.get_dayschedule",
        ),
        _case("init.default-anonymous", "init", "RuleSet.__init__"),
        _case("init.explicit-padded", "init", "RuleSet.__init__"),
        _case(
            "init.mixed-types",
            "init",
            "RuleSet.__init__",
            "raised",
            "domain",
        ),
        _case("max.defaults", "max", "RuleSet.max"),
        _case("max.override", "max", "RuleSet.max"),
        _case("max.signed-zero", "max", "RuleSet.max"),
        _case("min.defaults", "min", "RuleSet.min"),
        _case("min.override", "min", "RuleSet.min"),
        _case("min.signed-zero", "min", "RuleSet.min"),
        _case("summary.default-normalized", "summary", "RuleSet.summary"),
        _case("summary.exclude-days", "summary", "RuleSet.summary"),
        _case("summary.override-rich", "summary", "RuleSet.summary"),
        _case("to-dict.aliases", "to-dict", "RuleSet.to_dict"),
        _case("to-dict.nulls", "to-dict", "RuleSet.to_dict"),
        _case("to-dict.order", "to-dict", "RuleSet.to_dict"),
        _case("to-idf.defaults", "to-idf", "RuleSet.to_idf_compactexpr"),
        _case(
            "to-idf.weekday-expansion",
            "to-idf",
            "RuleSet.to_idf_compactexpr",
        ),
        _case("to-idf.weekend-holiday", "to-idf", "RuleSet.to_idf_compactexpr"),
        _case("type.default-real", "type", "RuleSet.type"),
        _case("type.explicit-token", "type", "RuleSet.type"),
        _case("type.inferred-day", "type", "RuleSet.type"),
    ]

    for slot in sorted(OPTIONAL_SLOT_KEYS):
        symbol = f"RuleSet.{slot}"
        definitions.extend(
            (
                _case(f"{slot}.clear", "slot", symbol),
                _case(f"{slot}.explicit", "slot", symbol),
                _case(
                    f"{slot}.mixed-type",
                    "slot",
                    symbol,
                    "raised",
                    "domain",
                ),
            )
        )
    for slot in sorted(REQUIRED_SLOT_KEYS):
        symbol = f"RuleSet.{slot}"
        definitions.extend(
            (
                _case(f"{slot}.explicit", "slot", symbol),
                _case(
                    f"{slot}.mixed-type",
                    "slot",
                    symbol,
                    "raised",
                    "domain",
                ),
                _case(f"{slot}.replace", "slot", symbol),
            )
        )

    ordered = tuple(sorted(definitions, key=lambda item: item["id"]))
    identifiers = [item["id"] for item in ordered]
    if len(ordered) != EXPECTED_CASE_COUNT or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("RuleSet core case identifiers are not exactly 72 unique values.")
    if Counter(item["symbol"] for item in ordered) != Counter(
        {symbol: 3 for symbol in TARGET_SYMBOLS}
    ):
        raise RuntimeError("RuleSet core does not contain three cases per symbol.")
    validate_case_definitions(ordered)
    return ordered


def validate_case_definitions(definitions: tuple[dict[str, Any], ...]) -> None:
    identifiers = [item.get("id") for item in definitions]
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} RuleSet core cases, got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("RuleSet core case identifiers are not unique and sorted.")

    counts: Counter[str] = Counter()
    for definition in definitions:
        if set(definition) != CASE_DEFINITION_KEYS:
            raise RuntimeError(
                f"Case definition {definition.get('id')!r} has an invalid key set."
            )
        identifier = definition["id"]
        executor = definition["executor"]
        symbol = definition["symbol"]
        if not isinstance(identifier, str) or not identifier:
            raise RuntimeError("A RuleSet core case has an invalid identifier.")
        if not isinstance(executor, str) or not executor:
            raise RuntimeError(f"Case {identifier!r} has an invalid executor.")
        if symbol not in TARGET_SYMBOLS:
            raise RuntimeError(f"Case {identifier!r} targets an unknown symbol.")
        counts[symbol] += 1

        expectation = definition["expected_dotnet"]
        adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
        if adaptation is None:
            if expectation is not None:
                raise RuntimeError(
                    f"Equivalent case {identifier!r} unexpectedly has an adaptation."
                )
        else:
            _validate_dotnet_expectation(identifier, expectation, adaptation)

    if counts != Counter({symbol: 3 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("RuleSet core does not contain three cases per symbol.")


def _validate_dotnet_expectation(
    identifier: str,
    expectation: Any,
    adaptation: str,
) -> None:
    if not isinstance(expectation, dict):
        raise RuntimeError(f"Adapted case {identifier!r} has no native expectation.")
    keys = set(expectation)
    if keys not in {
        frozenset(EXPECTED_DOTNET_KEYS),
        frozenset(EXPECTED_DOTNET_ERROR_KEYS),
    }:
        raise RuntimeError(f"Case {identifier!r} has an invalid native key set.")
    if expectation.get("adaptation") != adaptation:
        raise RuntimeError(f"Case {identifier!r} has an unknown adaptation.")
    outcome = expectation.get("outcome")
    if outcome not in {"raised", "returned"}:
        raise RuntimeError(f"Case {identifier!r} has an invalid native outcome.")
    if "error_category" in expectation and (
        outcome != "raised"
        or expectation["error_category"] not in {"domain", "range", "type"}
    ):
        raise RuntimeError(f"Case {identifier!r} has an invalid error category.")


def normalize(value: Any) -> Any:
    return BASE.normalize(value)


encode = normalize


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


class IdentityNormalizer:
    @staticmethod
    def name(value: str) -> dict[str, str]:
        return BASE.IdentityNormalizer.name(value)


def _name(value: str) -> dict[str, str]:
    return BASE._name(value)


def _schedule(value: Any, DaySchedule: type) -> dict[str, Any]:
    return BASE._schedule(value, DaySchedule)


def _returned(facts: dict[str, Any]) -> dict[str, Any]:
    return BASE._returned(facts)


def _raised(exception: Exception, facts: dict[str, Any] | None = None) -> dict[str, Any]:
    return BASE._raised(exception, facts)


def _ruleset(value: Any, RuleSet: type, DaySchedule: type) -> dict[str, Any]:
    if not isinstance(value, RuleSet):
        raise RuntimeError("A RuleSet snapshot received the wrong type.")
    references: dict[int, str] = {}
    slots: dict[str, str | None] = {}
    days: list[dict[str, Any]] = []
    for key in SLOT_KEYS:
        day = getattr(value, key)
        if day is None:
            slots[key] = None
            continue
        if not isinstance(day, DaySchedule):
            raise RuntimeError(f"RuleSet slot {key!r} is not a DaySchedule.")
        identity = id(day)
        reference = references.get(identity)
        if reference is None:
            reference = f"day-{len(references) + 1:02d}"
            references[identity] = reference
            days.append(
                {
                    "reference": reference,
                    "schedule": _schedule(day, DaySchedule),
                }
            )
        slots[key] = reference
    ruleset_type = value.type.value if hasattr(value.type, "value") else value.type
    return {
        "days": days,
        "kind": "ruleset",
        "name": _name(value.name),
        "ruleset_type": ruleset_type,
        "slots": slots,
    }


def _mapping_snapshot(mapping: dict[str, Any], DaySchedule: type) -> dict[str, Any]:
    references: dict[int, str] = {}
    slots: dict[str, str | None] = {}
    days: list[dict[str, Any]] = []
    for key, day in mapping.items():
        if day is None:
            slots[key] = None
            continue
        if not isinstance(day, DaySchedule):
            raise RuntimeError(f"Day mapping slot {key!r} is not a DaySchedule.")
        identity = id(day)
        reference = references.get(identity)
        if reference is None:
            reference = f"day-{len(references) + 1:02d}"
            references[identity] = reference
            days.append(
                {
                    "reference": reference,
                    "schedule": _schedule(day, DaySchedule),
                }
            )
        slots[key] = reference
    return {"days": days, "keys": list(mapping), "slots": slots}


def _day(
    DaySchedule: type,
    ScheduleType: type,
    name: str | None,
    value: int | float,
    schedule_type: Any = None,
    unit: str | None = None,
) -> Any:
    selected_type = ScheduleType.REAL if schedule_type is None else schedule_type
    return DaySchedule(name, [value] * 144, type=selected_type, unit=unit)


def _base_ruleset(RuleSet: type, DaySchedule: type, ScheduleType: type) -> Any:
    weekdays = _day(DaySchedule, ScheduleType, "weekday", 1.0)
    weekends = _day(DaySchedule, ScheduleType, "weekend", 2.0)
    return RuleSet("rules", weekdays, weekends)


def _slot_case(
    identifier: str,
    RuleSet: type,
    DaySchedule: type,
    ScheduleType: type,
) -> dict[str, Any]:
    slot, mode = identifier.split(".", 1)
    if slot not in SLOT_KEYS:
        raise RuntimeError(f"Unknown RuleSet slot case {identifier!r}.")
    explicit = _day(DaySchedule, ScheduleType, f"{slot}-explicit", 3.0)
    arguments = {slot: explicit}
    if slot in REQUIRED_SLOT_KEYS:
        weekdays = arguments.get(
            "weekdays", _day(DaySchedule, ScheduleType, "weekday", 1.0)
        )
        weekends = arguments.get(
            "weekends", _day(DaySchedule, ScheduleType, "weekend", 2.0)
        )
        ruleset = RuleSet("slots", weekdays, weekends)
    else:
        ruleset = RuleSet(
            "slots",
            _day(DaySchedule, ScheduleType, "weekday", 1.0),
            _day(DaySchedule, ScheduleType, "weekend", 2.0),
            **arguments,
        )

    if mode == "explicit":
        return _returned(
            {
                "getter_is_input": getattr(ruleset, slot) is explicit,
                "result": _ruleset(ruleset, RuleSet, DaySchedule),
            }
        )
    if mode == "clear" and slot in OPTIONAL_SLOT_KEYS:
        result = setattr(ruleset, slot, None)
        return _returned(
            {
                "return_is_none": result is None,
                "result": _ruleset(ruleset, RuleSet, DaySchedule),
            }
        )
    if mode == "replace" and slot in REQUIRED_SLOT_KEYS:
        replacement = _day(DaySchedule, ScheduleType, f"{slot}-replacement", 4.0)
        result = setattr(ruleset, slot, replacement)
        return _returned(
            {
                "getter_is_replacement": getattr(ruleset, slot) is replacement,
                "return_is_none": result is None,
                "result": _ruleset(ruleset, RuleSet, DaySchedule),
            }
        )
    if mode == "mixed-type":
        mixed = _day(
            DaySchedule,
            ScheduleType,
            f"{slot}-temperature",
            20.0,
            ScheduleType.TEMPERATURE,
        )
        result = setattr(ruleset, slot, mixed)
        return _returned(
            {
                "getter_is_mixed": getattr(ruleset, slot) is mixed,
                "return_is_none": result is None,
                "result": _ruleset(ruleset, RuleSet, DaySchedule),
            }
        )
    raise RuntimeError(f"Unknown RuleSet slot mode {identifier!r}.")


def _execute(
    identifier: str,
    RuleSet: type,
    DaySchedule: type,
    ScheduleType: type,
) -> dict[str, Any]:
    R, D, T = RuleSet, DaySchedule, ScheduleType

    if identifier == "class.alias-topology":
        shared = _day(D, T, "shared", 1.0)
        result = R("alias", shared, shared, monday=shared)
        return _returned({"result": _ruleset(result, R, D)})
    if identifier == "class.mutable-slot":
        result = _base_ruleset(R, D, T)
        replacement = _day(D, T, "monday", 4.0)
        return_value = setattr(result, "monday", replacement)
        return _returned(
            {
                "getter_is_replacement": result.monday is replacement,
                "return_is_none": return_value is None,
                "result": _ruleset(result, R, D),
            }
        )
    if identifier == "class.slot-inventory":
        return _returned(
            {
                "day_keys": list(R._DAY_KEYS),
                "mro_names": [item.__name__ for item in R.__mro__],
                "weekday_keys": list(R._WEEKDAY_KEYS),
                "weekend_keys": list(R._WEEKEND_KEYS),
            }
        )

    if identifier == "deepcopy.memo-hit":
        source = _base_ruleset(R, D, T)
        sentinel = object()
        result = source.__deepcopy__({id(source): sentinel})
        return _returned({"returned_sentinel": result is sentinel})
    if identifier == "deepcopy.alias-topology":
        shared = _day(D, T, "shared", 1.0)
        source = R("source", shared, shared, monday=shared)
        result = copy.deepcopy(source)
        return _returned(
            {
                "fresh": result is not source,
                "result": _ruleset(result, R, D),
                "source": _ruleset(source, R, D),
            }
        )
    if identifier == "deepcopy.repeated":
        source = _base_ruleset(R, D, T)
        left, right = copy.deepcopy(source), copy.deepcopy(source)
        return _returned(
            {
                "distinct": left is not right and left is not source and right is not source,
                "left": _ruleset(left, R, D),
                "right": _ruleset(right, R, D),
            }
        )

    if identifier == "init.default-anonymous":
        return _returned({"result": _ruleset(R(None), R, D)})
    if identifier == "init.explicit-padded":
        weekdays = _day(D, T, "  weekday  ", 0.25, T.FRACTION, "  ratio  ")
        weekends = _day(D, T, "  weekend  ", 0.75, T.FRACTION, "  ratio  ")
        result = R("  rules  ", weekdays, weekends, type="fraction")
        return _returned({"result": _ruleset(result, R, D)})
    if identifier == "init.mixed-types":
        weekdays = _day(D, T, "weekday", 0.5, T.FRACTION)
        weekends = _day(D, T, "weekend", 20.0, T.TEMPERATURE)
        try:
            R("mixed", weekdays, weekends)
        except Exception as exception:
            return _raised(exception)
        raise RuntimeError("Mixed RuleSet construction unexpectedly returned.")

    if identifier == "astype.outplace-string":
        shared = _day(D, T, "shared", 0.5, T.FRACTION, "ratio")
        source = R("typed", shared, shared, monday=shared)
        result = source.astype("real")
        return _returned(
            {
                "fresh": result is not source,
                "result": _ruleset(result, R, D),
                "source": _ruleset(source, R, D),
            }
        )
    if identifier == "astype.inplace-stale-type":
        source = R.from_constant("typed", 0.5, type=T.FRACTION)
        result = source.astype(T.REAL, inplace=True)
        return _returned(
            {
                "return_is_none": result is None,
                "source_after": _ruleset(source, R, D),
            }
        )
    if identifier == "astype.partial-failure":
        source = R(
            "partial",
            _day(D, T, "weekday", 0.5),
            _day(D, T, "weekend", 2.0),
        )
        before = _ruleset(source, R, D)
        try:
            source.astype(T.FRACTION, inplace=True)
        except Exception as exception:
            return _raised(
                exception,
                {
                    "source_after": _ruleset(source, R, D),
                    "source_before": before,
                },
            )
        raise RuntimeError("Partially failing RuleSet astype unexpectedly returned.")

    if identifier == "clip.bounds-empty-name":
        source = R(
            "source",
            D("weekday", list((-2.0, 2.0) * 72), unit="kW"),
            D("weekend", list((3.0, -3.0) * 72), unit="kW"),
        )
        result = source.clip(-1.0, 1.0, name="")
        return _returned(
            {
                "result": _ruleset(result, R, D),
                "source": _ruleset(source, R, D),
            }
        )
    if identifier == "clip.inplace":
        source = R(
            "source",
            D("weekday", list((-2.0, 2.0) * 72)),
            D("weekend", list((3.0, -3.0) * 72)),
        )
        result = source.clip(-1.0, 1.0, inplace=True)
        return _returned(
            {
                "return_is_none": result is None,
                "source_after": _ruleset(source, R, D),
            }
        )
    if identifier == "clip.reversed":
        source = _base_ruleset(R, D, T)
        result = source.clip(3.0, 1.0)
        return _returned(
            {
                "result": _ruleset(result, R, D),
                "source": _ruleset(source, R, D),
            }
        )

    if identifier.startswith(tuple(f"{slot}." for slot in SLOT_KEYS)):
        return _slot_case(identifier, R, D, T)

    if identifier == "from-constant.scalar-distinct":
        result = R.from_constant(None, 2.5, type=T.REAL)
        return _returned({"result": _ruleset(result, R, D)})
    if identifier == "from-constant.day-alias":
        day = _day(D, T, "shared", 0.75, T.FRACTION, "ratio")
        result = R.from_constant("day-alias", day, type=T.TEMPERATURE)
        return _returned(
            {
                "input_is_weekdays": result.weekdays is day,
                "input_is_weekends": result.weekends is day,
                "result": _ruleset(result, R, D),
            }
        )
    if identifier == "from-constant.nonfinite":
        result = R.from_constant("nonfinite", math.nan, type=T.REAL)
        return _returned({"result": _ruleset(result, R, D)})

    if identifier == "from-days.scalar-overrides":
        result = R.from_days(
            "days",
            0.25,
            monday=0.75,
            saturday=1.0,
            holiday=0.5,
            type=T.FRACTION,
        )
        return _returned({"result": _ruleset(result, R, D)})
    if identifier == "from-days.day-ignores-type":
        default = _day(D, T, "default", 0.25, T.FRACTION, "ratio")
        friday = _day(D, T, "friday", 0.75, T.FRACTION, "ratio")
        result = R.from_days(
            "typed-day",
            default,
            friday=friday,
            type=T.TEMPERATURE,
        )
        return _returned(
            {
                "default_is_weekdays": result.weekdays is default,
                "default_is_weekends": result.weekends is default,
                "friday_is_input": result.friday is friday,
                "result": _ruleset(result, R, D),
            }
        )
    if identifier == "from-days.mixed-types":
        default = _day(D, T, "default", 0.25, T.FRACTION)
        monday = _day(D, T, "monday", 20.0, T.TEMPERATURE)
        try:
            R.from_days("mixed", default, monday=monday)
        except Exception as exception:
            return _raised(exception)
        raise RuntimeError("Mixed RuleSet.from_days unexpectedly returned.")

    if identifier == "get-dayschedule.string-fallback":
        ruleset = _base_ruleset(R, D, T)
        tuesday = _day(D, T, "tuesday", 3.0)
        ruleset.tuesday = tuesday
        return _returned(
            {
                "holiday_fallback_is_weekends": ruleset.get_dayschedule("holiday")
                is ruleset.weekends,
                "monday_fallback_is_weekdays": ruleset.get_dayschedule("monday")
                is ruleset.weekdays,
                "monday_raw_is_none": ruleset.get_dayschedule(
                    "monday", fallback=False
                )
                is None,
                "tuesday_explicit_is_input": ruleset.get_dayschedule("tuesday")
                is tuesday,
                "weekdays_string_is_default": ruleset.get_dayschedule("weekdays")
                is ruleset.weekdays,
            }
        )
    if identifier == "get-dayschedule.integer-indices":
        ruleset = _base_ruleset(R, D, T)
        monday = _day(D, T, "monday", 3.0)
        holiday = _day(D, T, "holiday", 4.0)
        ruleset.monday = monday
        ruleset.holiday = holiday
        return _returned(
            {
                "index_0_is_monday": ruleset.get_dayschedule(0) is monday,
                "index_7_is_holiday": ruleset.get_dayschedule(7) is holiday,
                "negative_1_is_holiday": ruleset.get_dayschedule(-1) is holiday,
                "negative_8_is_monday": ruleset.get_dayschedule(-8) is monday,
            }
        )
    if identifier == "get-dayschedule.invalid-index":
        ruleset = _base_ruleset(R, D, T)
        try:
            ruleset.get_dayschedule(8)
        except Exception as exception:
            return _raised(exception, {"source": _ruleset(ruleset, R, D)})
        raise RuntimeError("Out-of-range RuleSet day index unexpectedly returned.")

    if identifier in {"min.defaults", "max.defaults"}:
        ruleset = R(
            "range",
            _day(D, T, "weekday", 1.0),
            _day(D, T, "weekend", 2.0),
        )
        value = ruleset.min if identifier.startswith("min") else ruleset.max
        return _returned({"value": value})
    if identifier in {"min.override", "max.override"}:
        ruleset = R(
            "range",
            _day(D, T, "weekday", 1.0),
            _day(D, T, "weekend", 2.0),
            monday=_day(D, T, "monday", -5.0),
            holiday=_day(D, T, "holiday", 9.0),
        )
        value = ruleset.min if identifier.startswith("min") else ruleset.max
        return _returned({"value": value})
    if identifier == "min.signed-zero":
        ruleset = R(
            "zero",
            _day(D, T, "weekday", 0.0),
            _day(D, T, "weekend", -0.0),
        )
        return _returned({"value": ruleset.min})
    if identifier == "max.signed-zero":
        ruleset = R(
            "zero",
            _day(D, T, "weekday", -0.0),
            _day(D, T, "weekend", 0.0),
        )
        return _returned({"value": ruleset.max})

    if identifier == "summary.default-normalized":
        summary = R(None).summary()
        return _returned({"summary": BASE._normalize_text(summary)})
    if identifier == "summary.exclude-days":
        summary = _base_ruleset(R, D, T).summary(include_days=False)
        return _returned({"summary": summary})
    if identifier == "summary.override-rich":
        ruleset = R(
            "a'b",
            _day(D, T, "weekday", 1.23456, unit="kW"),
            _day(D, T, "weekend", -0.000012345, unit="kW"),
            monday=_day(D, T, "monday", 10000.0, unit="kW"),
            holiday=_day(D, T, "holiday", -2.0, unit="kW"),
        )
        return _returned({"summary": ruleset.summary(include_days=True)})

    if identifier == "to-dict.order":
        mapping = _base_ruleset(R, D, T).to_dict()
        return _returned({"keys": list(mapping)})
    if identifier == "to-dict.nulls":
        mapping = _base_ruleset(R, D, T).to_dict()
        return _returned({"mapping": _mapping_snapshot(mapping, D)})
    if identifier == "to-dict.aliases":
        shared = _day(D, T, "shared", 1.0)
        mapping = R("alias", shared, shared, monday=shared, holiday=shared).to_dict()
        return _returned({"mapping": _mapping_snapshot(mapping, D)})

    if identifier == "to-idf.defaults":
        ruleset = _base_ruleset(R, D, T)
        return _returned({"fields": ruleset.to_idf_compactexpr()})
    if identifier == "to-idf.weekday-expansion":
        ruleset = _base_ruleset(R, D, T)
        ruleset.wednesday = _day(D, T, "wednesday", 3.0)
        return _returned({"fields": ruleset.to_idf_compactexpr()})
    if identifier == "to-idf.weekend-holiday":
        ruleset = R(
            "idf",
            _day(D, T, "weekday", 1.0),
            _day(D, T, "weekend", -0.0),
            saturday=_day(D, T, "saturday", 2.0),
            holiday=_day(D, T, "holiday", 3.0),
        )
        return _returned({"fields": ruleset.to_idf_compactexpr()})

    if identifier == "type.default-real":
        return _returned({"type": R("default").type.value})
    if identifier == "type.explicit-token":
        return _returned({"type": R("typed", type="temperature").type.value})
    if identifier == "type.inferred-day":
        weekend = _day(D, T, "weekend", 20.0, T.TEMPERATURE)
        ruleset = R("inferred", weekends=weekend)
        return _returned(
            {
                "result": _ruleset(ruleset, R, D),
                "type": ruleset.type.value,
            }
        )

    raise RuntimeError(f"Unknown RuleSet core case {identifier!r}.")


def _require_exact_keys(value: Any, expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(
            f"{context} has an invalid key set: expected {sorted(expected)}, got {actual}."
        )


def validate_oracle(value: dict[str, Any]) -> None:
    """Validate the complete generated artifact before any bytes are written."""

    _require_exact_keys(value, ORACLE_KEYS, "RuleSet core oracle top-level root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("The RuleSet core oracle schema drifted.")

    upstream = value["upstream"]
    _require_exact_keys(upstream, UPSTREAM_KEYS, "RuleSet core upstream receipt")
    if upstream != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("The RuleSet core upstream receipt is not exact.")

    runtime = value["runtime"]
    _require_exact_keys(runtime, RUNTIME_KEYS, "RuleSet core runtime receipt")
    if runtime != {
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("The RuleSet core runtime receipt is not exact.")

    symbols = value["symbols"]
    if not isinstance(symbols, list) or len(symbols) != len(TARGET_SYMBOLS):
        raise RuntimeError("The RuleSet core symbol receipt count is not exact.")
    for expected_symbol, receipt in zip(TARGET_SYMBOLS, symbols, strict=True):
        _require_exact_keys(receipt, SYMBOL_KEYS, f"Symbol receipt {expected_symbol!r}")
        if receipt["symbol"] != expected_symbol:
            raise RuntimeError("The RuleSet core symbol receipt order drifted.")
        if receipt["path"] != SOURCE_PATH:
            raise RuntimeError(f"Symbol {expected_symbol!r} points to the wrong source.")
        if receipt["symbol_hash"] != EXPECTED_SYMBOL_HASHES[expected_symbol]:
            raise RuntimeError(f"Symbol {expected_symbol!r} has the wrong hash.")

    definitions = case_definitions()
    definition_by_id = {item["id"]: item for item in definitions}
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("The RuleSet core oracle case count is not exact.")
    identifiers = [item.get("id") for item in cases if isinstance(item, dict)]
    if identifiers != [item["id"] for item in definitions]:
        raise RuntimeError("The RuleSet core oracle case order drifted.")

    for case in cases:
        if not isinstance(case, dict):
            raise RuntimeError("A RuleSet core oracle case is not an object.")
        identifier = case.get("id")
        if identifier not in definition_by_id:
            raise RuntimeError(f"Unknown RuleSet core oracle case {identifier!r}.")
        definition = definition_by_id[identifier]
        expected_keys = CASE_KEYS
        if definition["expected_dotnet"] is not None:
            expected_keys = expected_keys | {"expected_dotnet"}
        _require_exact_keys(case, expected_keys, f"Oracle case {identifier!r}")
        if case["executor"] != definition["executor"]:
            raise RuntimeError(f"Case {identifier!r} executor drifted.")
        if case["symbol"] != definition["symbol"]:
            raise RuntimeError(f"Case {identifier!r} symbol drifted.")
        if definition["expected_dotnet"] is not None and (
            case["expected_dotnet"] != definition["expected_dotnet"]
        ):
            raise RuntimeError(f"Case {identifier!r} native expectation drifted.")

        observation = case["python"]
        if not isinstance(observation, dict):
            raise RuntimeError(f"Case {identifier!r} Python receipt is not an object.")
        outcome = observation.get("outcome")
        if outcome == "returned":
            _require_exact_keys(
                observation,
                PYTHON_RETURN_KEYS,
                f"Case {identifier!r} Python return receipt",
            )
        elif outcome == "raised":
            _require_exact_keys(
                observation,
                PYTHON_RAISE_KEYS,
                f"Case {identifier!r} Python error receipt",
            )
            if observation["error_category"] not in {"domain", "range", "type"}:
                raise RuntimeError(
                    f"Case {identifier!r} has an invalid Python error category."
                )
            if not isinstance(observation["exception_type"], str) or not observation[
                "exception_type"
            ]:
                raise RuntimeError(f"Case {identifier!r} has an invalid exception type.")
            if not isinstance(observation["message"], str):
                raise RuntimeError(f"Case {identifier!r} has an invalid exception message.")
        else:
            raise RuntimeError(f"Case {identifier!r} has an invalid Python outcome.")
        if not isinstance(observation["facts"], dict):
            raise RuntimeError(f"Case {identifier!r} facts are not an object.")

    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("The RuleSet core cases hash is invalid.")

    consumer = value["consumer_contract"]
    _require_exact_keys(consumer, CONSUMER_CONTRACT_KEYS, "Consumer contract")
    expected_classifications = {
        symbol: "equivalent" if symbol in EXPECTED_EQUIVALENT_SYMBOLS else "exception"
        for symbol in TARGET_SYMBOLS
    }
    if consumer != {
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": [item["id"] for item in definitions],
        "classifications": expected_classifications,
        "float_encoding": "python-binary64-hex-without-0x-prefix",
        "runtime_names": "policy-token-no-raw-address",
        "target_symbols": list(TARGET_SYMBOLS),
    }:
        raise RuntimeError("The RuleSet core consumer contract drifted.")

    serialized = strict_json_dumps(value)
    if RAW_AUTO_NAME_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime identity name entered the RuleSet core oracle.")


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import DaySchedule, RuleSet, ScheduleType

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")
    if tuple(RuleSet._DAY_KEYS) != SLOT_KEYS[2:] or tuple(RuleSet._WEEKDAY_KEYS) != SLOT_KEYS[2:7] or tuple(RuleSet._WEEKEND_KEYS) != SLOT_KEYS[7:9]:
        raise SystemExit("Pinned RuleSet slot constants are not exact.")
    if DaySchedule.DATA_INTERVAL != 6 or DaySchedule("probe").fixed_length != 144:
        raise SystemExit("Pinned DaySchedule grid constants are not exact.")

    definitions = case_definitions()
    cases: list[dict[str, Any]] = []
    for definition in definitions:
        observation = _execute(definition["id"], RuleSet, DaySchedule, ScheduleType)
        case = {
            "executor": definition["executor"],
            "id": definition["id"],
            "python": observation,
            "symbol": definition["symbol"],
        }
        if definition["expected_dotnet"] is not None:
            case["expected_dotnet"] = definition["expected_dotnet"]
        cases.append(case)

    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": {
            "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
            "case_count": EXPECTED_CASE_COUNT,
            "case_ids": [item["id"] for item in definitions],
            "classifications": {
                symbol: "equivalent"
                if symbol in EXPECTED_EQUIVALENT_SYMBOLS
                else "exception"
                for symbol in TARGET_SYMBOLS
            },
            "float_encoding": "python-binary64-hex-without-0x-prefix",
            "runtime_names": "policy-token-no-raw-address",
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "runtime": {
            "implementation": sys.implementation.name,
            "python_hash_algorithm": sys.hash_info.algorithm,
            "python_hash_seed": 0,
            "python_hash_width_bits": sys.hash_info.width,
            "python_version": ".".join(map(str, sys.version_info[:3])),
        },
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "upstream": {
            "commit": commit,
            "inventory_sha256": inventory["content_sha256"],
            "path": SOURCE_PATH,
            "source_sha256": imported_source_sha256,
        },
    }
    validate_oracle(result)
    return result


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the RuleSet oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")

    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    serialized = strict_json_dumps(result, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8", newline="\n")
    print(f"Wrote RuleSet core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Generate pinned observations for the residual ``profile.py`` symbols.

The corpus is deliberately bounded to three high-value cases for each of the
five public symbols left after the DaySchedule, RuleSet, and Schedule slices.
Run it only through ``bootstrap_reference.py`` so imports resolve from the
pinned CPython 3.12.7 environment and upstream source tree.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.util
import os
from pathlib import Path
import sys
from typing import Any


SCHEMA = "dragons.invisibledragon.profile-residual-oracle.v1"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "Profile": {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "kind": "class",
        "signature_hash": "sha256:bf35db5abe6e8851938c2d634421f972436bb46ab9abab1dca41465ffcd7e9d4",
        "symbol_hash": "sha256:3cf55ef99529b6051e2e5bea5c32bbecc5850819101e522fed1008be0599d6ad",
    },
    "Profile.__init__": {
        "body_hash": "sha256:73dd1c37c7a808baa32cbd8e9c811b443e20a07b79a88e38f01ad7387631251f",
        "kind": "function",
        "signature_hash": "sha256:64eb4f95ace84bc62c18887bae8642d24c5a613faea7f0a6403a4d7a4cf9ba52",
        "symbol_hash": "sha256:19f87b176fd6f00e83c6b55bda01ac7e9bb5d8a0829e8f869f13c20a0388aa25",
    },
    "Profile.to_idf_object": {
        "body_hash": "sha256:77716652f9c58182268dd2afe13cec984edbf15c4d64fffac3a85905bc740713",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:0b06ee5f7b81782b986777c9f524320ff3f272722a9d0ec4942f5f53ac074893",
    },
    "Schedule": {
        "body_hash": "sha256:00679b8c55fe41d3ab7f7d84e2d3a1e3f0b6ed9c003c318e9ff8ed595932fd34",
        "kind": "class",
        "signature_hash": "sha256:24241d2bdfbc529f097a3f866f790e3a45ad8d0ad336d65ced3c9841d8844453",
        "symbol_hash": "sha256:1a40948f1e3ccbc15dbee4033662c4e80a2a6b4ee559271dd0ca2f59f890095c",
    },
    "ScheduleOperationError": {
        "body_hash": "sha256:921a63a3a05234e5b1c61efbee031114924c6587cc8d60b93d4932290c0b549a",
        "kind": "class",
        "signature_hash": "sha256:302b0beaf8566368e9c978cee1c9dcbdf5e3ad95728e33169278853fa1dc0cab",
        "symbol_hash": "sha256:d808ccddebceb72eed1685cd6f236255cc7cc32a21a0b4459237b35af6c7f129",
    },
}
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_EQUIVALENT_SYMBOLS = frozenset({"Profile.to_idf_object"})
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "Profile": "immutable-profile-value-object",
    "Profile.__init__": "validated-immutable-profile-construction",
    "Schedule": "immutable-schedule-value-object",
    "ScheduleOperationError": "native-schedule-operation-exception-family",
}
EXPECTED_CASE_COUNT = 15
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

PROFILE_SLOT_KEYS = (
    "heating_setpoint",
    "cooling_setpoint",
    "hvac_availability",
    "occupant",
    "lighting",
    "equipment",
    "hotwater",
)

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
IDF_OBJECT_KEYS = {"fields", "kind", "object_type"}
PROFILE_IDF_FACT_KEYS = {
    "profile-idf.empty": {
        "count",
        "null_slots_omitted",
        "objects",
        "repeated_call_count",
        "results_are_fresh",
    },
    "profile-idf.ordered-seven": {
        "count",
        "objects",
        "schedule_names",
        "type_limit_names",
    },
    "profile-idf.repeated-reference": {
        "converted_objects_are_fresh",
        "converted_values_match",
        "count",
        "duplicate_positions_preserved",
        "objects",
    },
}
FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS = (
    "append",
    "list",
    "mutability",
    "mutable",
)


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_day_schedule_core_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_profile_residual_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load Profile residual oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
    ):
        raise RuntimeError("Profile residual oracle support is not pinned.")
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
        raise SystemExit("The inventory does not exactly cover five residual symbols.")
    for item in inventory["symbols"]:
        expected = {
            **EXPECTED_SYMBOL_RECEIPTS[item["symbol"]],
            "path": SOURCE_PATH,
            "symbol": item["symbol"],
        }
        if item != expected:
            raise SystemExit(
                f"The inventory receipt for {item['symbol']!r} is not exact."
            )
    return inventory


def _dotnet(
    adaptation: str,
    outcome: str,
    error_category: str | None = None,
) -> dict[str, str]:
    if adaptation not in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.values():
        raise RuntimeError(f"Unknown Profile residual adaptation {adaptation!r}.")
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
    definitions = (
        _case("profile.alias-topology", "profile", "Profile"),
        _case("profile.identity-equality", "profile", "Profile"),
        _case("profile.mutable-surface", "profile", "Profile"),
        _case("profile-init.defaults", "profile-init", "Profile.__init__"),
        _case(
            "profile-init.unvalidated-inputs",
            "profile-init",
            "Profile.__init__",
            "raised",
            "type",
        ),
        _case(
            "profile-init.valid-seven-slots",
            "profile-init",
            "Profile.__init__",
        ),
        _case(
            "profile-idf.empty",
            "profile-idf",
            "Profile.to_idf_object",
        ),
        _case(
            "profile-idf.ordered-seven",
            "profile-idf",
            "Profile.to_idf_object",
        ),
        _case(
            "profile-idf.repeated-reference",
            "profile-idf",
            "Profile.to_idf_object",
        ),
        _case("schedule.alias-container", "schedule", "Schedule"),
        _case("schedule.default-topology", "schedule", "Schedule"),
        _case("schedule.mutable-userlist", "schedule", "Schedule"),
        _case(
            "schedule-operation-error.args",
            "schedule-operation-error",
            "ScheduleOperationError",
        ),
        _case(
            "schedule-operation-error.catch-family",
            "schedule-operation-error",
            "ScheduleOperationError",
        ),
        _case(
            "schedule-operation-error.inheritance",
            "schedule-operation-error",
            "ScheduleOperationError",
        ),
    )
    ordered = tuple(sorted(definitions, key=lambda item: item["id"]))
    identifiers = [item["id"] for item in ordered]
    if len(ordered) != EXPECTED_CASE_COUNT or len(identifiers) != len(
        set(identifiers)
    ):
        raise RuntimeError(
            "Profile residual case identifiers are not exactly 15 unique values."
        )
    if Counter(item["symbol"] for item in ordered) != Counter(
        {symbol: 3 for symbol in TARGET_SYMBOLS}
    ):
        raise RuntimeError("Profile residual does not contain three cases per symbol.")
    validate_case_definitions(ordered)
    return ordered


def validate_case_definitions(definitions: tuple[dict[str, Any], ...]) -> None:
    identifiers = [item.get("id") for item in definitions]
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} Profile residual cases, "
            f"got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(
        set(identifiers)
    ):
        raise RuntimeError("Profile residual case identifiers are not unique and sorted.")

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
            raise RuntimeError("A Profile residual case has an invalid identifier.")
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
        raise RuntimeError("Profile residual does not contain three cases per symbol.")


def _validate_dotnet_expectation(
    identifier: str,
    expectation: Any,
    adaptation: str,
) -> None:
    if not isinstance(expectation, dict):
        raise RuntimeError(f"Adapted case {identifier!r} has no native expectation.")
    if set(expectation) not in {
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


class ReferenceNormalizer:
    """Assign deterministic, kind-scoped tokens without serializing identities."""

    def __init__(self) -> None:
        self._references: dict[int, str] = {}
        self._counts: Counter[str] = Counter()

    def reference(self, value: Any, kind: str) -> str:
        identity = id(value)
        existing = self._references.get(identity)
        if existing is not None:
            return existing
        self._counts[kind] += 1
        reference = f"{kind}-{self._counts[kind]:02d}"
        self._references[identity] = reference
        return reference


def _name(value: str) -> dict[str, str]:
    return BASE._name(value)


def _returned(facts: dict[str, Any]) -> dict[str, Any]:
    return BASE._returned(facts)


def _raised(exception: Exception, facts: dict[str, Any] | None = None) -> dict[str, Any]:
    return BASE._raised(exception, facts)


def _schedule_brief(value: Any, Schedule: type) -> dict[str, Any]:
    if not isinstance(value, Schedule):
        raise RuntimeError("A Profile schedule descriptor received the wrong type.")
    return {
        "kind": "schedule",
        "length": len(value),
        "name": _name(value.name),
        "schedule_type": value.type.value,
    }


def _profile(value: Any, Profile: type, Schedule: type) -> dict[str, Any]:
    if not isinstance(value, Profile):
        raise RuntimeError("A Profile snapshot received the wrong type.")
    references = ReferenceNormalizer()
    slots: dict[str, str | None] = {}
    objects: list[dict[str, Any]] = []
    observed: set[str] = set()
    for key in PROFILE_SLOT_KEYS:
        item = getattr(value, key)
        if item is None:
            slots[key] = None
            continue
        kind = "schedule" if isinstance(item, Schedule) else "foreign"
        reference = references.reference(item, kind)
        slots[key] = reference
        if reference in observed:
            continue
        observed.add(reference)
        descriptor = (
            _schedule_brief(item, Schedule)
            if isinstance(item, Schedule)
            else {"kind": "foreign", "type": type(item).__name__}
        )
        objects.append({"reference": reference, "value": descriptor})
    if value.name is None:
        name: Any = None
    elif isinstance(value.name, str):
        name = _name(value.name)
    else:
        name = {"kind": "foreign", "type": type(value.name).__name__}
    return {
        "kind": "profile",
        "name": name,
        "objects": objects,
        "slots": slots,
    }


def _idf_object(value: Any) -> dict[str, Any]:
    dictionary = vars(value)
    extended = next(
        (item for key, item in dictionary.items() if key.endswith("__extended_input")),
        None,
    )
    if not isinstance(extended, list):
        raise RuntimeError("An IdfObject does not expose its ordered extended input.")
    data = getattr(value, "data", None)
    if not isinstance(data, dict):
        raise RuntimeError("An IdfObject does not expose its ordered primary data.")
    fields = list(data.values()) + list(extended)
    while fields and fields[-1] is None:
        fields.pop()
    object_type = getattr(getattr(value, "idd", None), "name", None)
    descriptor = {
        "fields": fields,
        "kind": "idf-object",
        "object_type": object_type,
    }
    _validate_idf_object(descriptor)
    return descriptor


def _validate_idf_object(value: Any) -> None:
    if not isinstance(value, dict) or set(value) != IDF_OBJECT_KEYS:
        raise RuntimeError("A Profile IDF descriptor has an invalid key set.")
    if value["kind"] != "idf-object" or value["object_type"] != "Schedule:Compact":
        raise RuntimeError("A Profile IDF descriptor has an invalid object identity.")
    if not isinstance(value["fields"], list) or not value["fields"]:
        raise RuntimeError("A Profile IDF descriptor has no ordered fields.")
    if value["fields"][-1] is None:
        raise RuntimeError("A Profile IDF descriptor retains trailing null fields.")


def _constant_schedule(
    Schedule: type,
    ScheduleType: type,
    name: str,
    value: int | float,
    schedule_type: Any,
) -> Any:
    return Schedule.from_constant(name, value, type=schedule_type)


def _valid_profile_inputs(
    Schedule: type,
    ScheduleType: type,
) -> dict[str, Any]:
    return {
        "heating_setpoint": _constant_schedule(
            Schedule, ScheduleType, "heating", 20.0, ScheduleType.TEMPERATURE
        ),
        "cooling_setpoint": _constant_schedule(
            Schedule, ScheduleType, "cooling", 25.0, ScheduleType.TEMPERATURE
        ),
        "hvac_availability": _constant_schedule(
            Schedule, ScheduleType, "hvac", 1, ScheduleType.ONOFF
        ),
        "occupant": _constant_schedule(
            Schedule, ScheduleType, "occupant", 0.1, ScheduleType.REAL
        ),
        "lighting": _constant_schedule(
            Schedule, ScheduleType, "lighting", 0.2, ScheduleType.FRACTION
        ),
        "equipment": _constant_schedule(
            Schedule, ScheduleType, "equipment", 3.0, ScheduleType.REAL
        ),
        "hotwater": _constant_schedule(
            Schedule, ScheduleType, "hotwater", 4.0, ScheduleType.REAL
        ),
    }


def _execute(
    identifier: str,
    Profile: type,
    Schedule: type,
    RuleSet: type,
    DaySchedule: type,
    ScheduleType: type,
    ScheduleOperationError: type,
) -> dict[str, Any]:
    P, S, R, T = Profile, Schedule, RuleSet, ScheduleType

    if identifier == "profile.alias-topology":
        temperature = _constant_schedule(S, T, "temperature-shared", 20.0, T.TEMPERATURE)
        real = _constant_schedule(S, T, "real-shared", 1.0, T.REAL)
        hvac = _constant_schedule(S, T, "hvac", 1, T.ONOFF)
        lighting = _constant_schedule(S, T, "lighting", 0.5, T.FRACTION)
        profile = P(
            "alias",
            temperature,
            temperature,
            hvac,
            real,
            lighting,
            real,
            real,
        )
        return _returned(
            {
                "heating_is_cooling": profile.heating_setpoint
                is profile.cooling_setpoint,
                "occupant_is_equipment_is_hotwater": profile.occupant
                is profile.equipment
                is profile.hotwater,
                "result": _profile(profile, P, S),
            }
        )
    if identifier == "profile.identity-equality":
        shared = _constant_schedule(S, T, "shared", 1.0, T.REAL)
        left = P("same", occupant=shared)
        right = P("same", occupant=shared)
        return _returned(
            {
                "left_equals_right": left == right,
                "left_equals_self": left == left,
                "left_is_right": left is right,
                "object_equality_is_not_implemented": left.__eq__(right)
                is NotImplemented,
                "profiles_are_hashable": hash(left) != hash(right) or left is not right,
            }
        )
    if identifier == "profile.mutable-surface":
        profile = P("before")
        marker = object()
        profile.name = "after"
        profile.heating_setpoint = marker
        profile.dynamic_note = "added"
        del profile.cooling_setpoint
        return _returned(
            {
                "cooling_attribute_deleted": not hasattr(
                    profile, "cooling_setpoint"
                ),
                "dictionary_keys": list(vars(profile)),
                "dynamic_attribute": profile.dynamic_note,
                "heating_is_foreign_marker": profile.heating_setpoint is marker,
                "name": profile.name,
            }
        )

    if identifier == "profile-init.defaults":
        profile = P("defaults")
        return _returned(
            {
                "dictionary_keys": list(vars(profile)),
                "result": _profile(profile, P, S),
            }
        )
    if identifier == "profile-init.unvalidated-inputs":
        marker = object()
        profile = P(None, marker, marker, marker, marker, marker, marker, marker)
        return _returned(
            {
                "idf_result_is_empty": profile.to_idf_object() == [],
                "result": _profile(profile, P, S),
                "slots_retain_one_foreign_identity": len(
                    {id(getattr(profile, key)) for key in PROFILE_SLOT_KEYS}
                )
                == 1,
            }
        )
    if identifier == "profile-init.valid-seven-slots":
        inputs = _valid_profile_inputs(S, T)
        profile = P("  valid profile  ", **inputs)
        return _returned(
            {
                "all_input_references_preserved": all(
                    getattr(profile, key) is value for key, value in inputs.items()
                ),
                "result": _profile(profile, P, S),
            }
        )

    if identifier == "profile-idf.empty":
        profile = P("ignored-name")
        left = profile.to_idf_object()
        right = profile.to_idf_object()
        return _returned(
            {
                "count": len(left),
                "null_slots_omitted": len(left) == 0,
                "objects": [_idf_object(value) for value in left],
                "repeated_call_count": len(right),
                "results_are_fresh": left is not right,
            }
        )
    if identifier == "profile-idf.ordered-seven":
        inputs = _valid_profile_inputs(S, T)
        profile = P("ordered", **inputs)
        values = profile.to_idf_object()
        descriptors = [_idf_object(value) for value in values]
        return _returned(
            {
                "count": len(values),
                "objects": descriptors,
                "schedule_names": [item["fields"][0] for item in descriptors],
                "type_limit_names": [item["fields"][1] for item in descriptors],
            }
        )
    if identifier == "profile-idf.repeated-reference":
        shared = _constant_schedule(S, T, "shared-temperature", 21.0, T.TEMPERATURE)
        profile = P("repeated", shared, shared)
        values = profile.to_idf_object()
        descriptors = [_idf_object(value) for value in values]
        return _returned(
            {
                "converted_objects_are_fresh": values[0] is not values[1],
                "converted_values_match": descriptors[0] == descriptors[1],
                "count": len(values),
                "duplicate_positions_preserved": len(values) == 2,
                "objects": descriptors,
            }
        )

    if identifier == "schedule.alias-container":
        shared = R.from_constant("shared", 1.0, type=T.REAL)
        replacement = R.from_constant("replacement", 2.0, type=T.REAL)
        source = [shared] * S.FIXED_LENGTH
        schedule = S("annual", source)
        data_is_source = schedule.data is source
        all_shared_before = all(item is shared for item in schedule)
        source[0] = replacement
        source.append(shared)
        return _returned(
            {
                "all_items_shared_before": all_shared_before,
                "data_is_source_container": data_is_source,
                "first_tracks_source_replacement": schedule[0] is replacement,
                "fixed_length_constant": S.FIXED_LENGTH,
                "last_is_shared": schedule[-1] is shared,
                "length_after_source_append": len(schedule),
            }
        )
    if identifier == "schedule.default-topology":
        schedule = S(None)
        rules = list(schedule.data)
        days = [
            day
            for ruleset in rules
            for day in (ruleset.weekdays, ruleset.weekends)
        ]
        return _returned(
            {
                "all_day_values_zero": all(
                    value == 0 for day in days for value in day.data
                ),
                "all_ruleset_types_real": all(
                    ruleset.type is T.REAL for ruleset in rules
                ),
                "distinct_day_schedule_count": len({id(day) for day in days}),
                "distinct_ruleset_count": len({id(ruleset) for ruleset in rules}),
                "fixed_length_constant": S.FIXED_LENGTH,
                "length": len(schedule),
                "runtime_day_name_count": sum(
                    AUTO_NAME_PATTERN.fullmatch(day.name) is not None for day in days
                ),
                "runtime_ruleset_name_count": sum(
                    AUTO_NAME_PATTERN.fullmatch(ruleset.name) is not None
                    for ruleset in rules
                ),
                "schedule_name": _name(schedule.name),
                "weekday_weekend_are_distinct": all(
                    ruleset.weekdays is not ruleset.weekends for ruleset in rules
                ),
            }
        )
    if identifier == "schedule.mutable-userlist":
        shared = R.from_constant("shared", 1.0, type=T.REAL)
        replacement = R.from_constant("replacement", 2.0, type=T.REAL)
        schedule = S("mutable", [shared] * S.FIXED_LENGTH)
        negative_result = schedule.__setitem__(-1, replacement)
        slice_result = schedule.__setitem__(slice(0, 2), [replacement])
        length_after_slice = len(schedule)
        try:
            schedule.append("blocked")
        except Exception as exception:
            blocked_append = {
                "message": BASE._normalize_text(str(exception)),
                "type": type(exception).__name__,
            }
        else:
            raise RuntimeError("Schedule.append unexpectedly changed the sequence.")
        schedule.data.append("foreign")
        last_is_foreign = schedule[-1] == "foreign"
        removed = schedule.data.pop(0)
        return _returned(
            {
                "blocked_append": blocked_append,
                "last_was_foreign_value": last_is_foreign,
                "length_after_raw_pop": len(schedule),
                "length_after_slice": length_after_slice,
                "negative_assignment_is_replacement": schedule[-2] is replacement,
                "negative_assignment_returns_none": negative_result is None,
                "public_data_is_list": type(schedule.data) is list,
                "raw_pop_removed_replacement": removed is replacement,
                "slice_assignment_returns_none": slice_result is None,
            }
        )

    if identifier == "schedule-operation-error.args":
        empty = ScheduleOperationError()
        single = ScheduleOperationError("operation failed")
        multiple = ScheduleOperationError("left", 2)
        return _returned(
            {
                "empty_args": list(empty.args),
                "empty_message": str(empty),
                "multiple_args": list(multiple.args),
                "multiple_message": str(multiple),
                "single_args": list(single.args),
                "single_message": str(single),
            }
        )
    if identifier == "schedule-operation-error.catch-family":
        exception = ScheduleOperationError("boom")
        caught_as_type_error = False
        try:
            raise exception
        except TypeError as caught:
            caught_as_type_error = caught is exception
        return _returned(
            {
                "caught_as_type_error": caught_as_type_error,
                "is_exception": isinstance(exception, Exception),
                "is_not_value_error": not isinstance(exception, ValueError),
                "is_type_error": isinstance(exception, TypeError),
                "normalized_category": "schedule-operation",
            }
        )
    if identifier == "schedule-operation-error.inheritance":
        return _returned(
            {
                "is_exception_subclass": issubclass(
                    ScheduleOperationError, Exception
                ),
                "is_not_value_error_subclass": not issubclass(
                    ScheduleOperationError, ValueError
                ),
                "is_type_error_subclass": issubclass(
                    ScheduleOperationError, TypeError
                ),
                "mro_names": [item.__name__ for item in ScheduleOperationError.__mro__],
            }
        )

    raise RuntimeError(f"Unknown Profile residual case {identifier!r}.")


def _require_exact_keys(value: Any, expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(
            f"{context} has an invalid key set: expected {sorted(expected)}, "
            f"got {actual}."
        )


def validate_oracle(value: dict[str, Any]) -> None:
    """Validate the complete generated artifact before any bytes are written."""

    _require_exact_keys(value, ORACLE_KEYS, "Profile residual oracle top-level root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("The Profile residual oracle schema drifted.")

    upstream = value["upstream"]
    _require_exact_keys(upstream, UPSTREAM_KEYS, "Profile residual upstream receipt")
    if upstream != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("The Profile residual upstream receipt is not exact.")

    runtime = value["runtime"]
    _require_exact_keys(runtime, RUNTIME_KEYS, "Profile residual runtime receipt")
    if runtime != {
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("The Profile residual runtime receipt is not exact.")

    symbols = value["symbols"]
    if not isinstance(symbols, list) or len(symbols) != len(TARGET_SYMBOLS):
        raise RuntimeError("The Profile residual symbol receipt count is not exact.")
    for expected_symbol, receipt in zip(TARGET_SYMBOLS, symbols, strict=True):
        _require_exact_keys(receipt, SYMBOL_KEYS, f"Symbol receipt {expected_symbol!r}")
        expected_receipt = {
            **EXPECTED_SYMBOL_RECEIPTS[expected_symbol],
            "path": SOURCE_PATH,
            "symbol": expected_symbol,
        }
        if receipt != expected_receipt:
            raise RuntimeError(f"Symbol receipt {expected_symbol!r} is not exact.")

    definitions = case_definitions()
    definition_by_id = {item["id"]: item for item in definitions}
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("The Profile residual oracle case count is not exact.")
    identifiers = [item.get("id") for item in cases if isinstance(item, dict)]
    if identifiers != [item["id"] for item in definitions]:
        raise RuntimeError("The Profile residual oracle case order drifted.")

    for case in cases:
        if not isinstance(case, dict):
            raise RuntimeError("A Profile residual oracle case is not an object.")
        identifier = case.get("id")
        if identifier not in definition_by_id:
            raise RuntimeError(f"Unknown Profile residual oracle case {identifier!r}.")
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

        if identifier.startswith("profile-idf.") and outcome == "returned":
            _validate_profile_idf_facts(identifier, observation["facts"])
            for descriptor in _idf_descriptors_from_facts(observation["facts"]):
                _validate_idf_object(descriptor)

    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("The Profile residual cases hash is invalid.")

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
        raise RuntimeError("The Profile residual consumer contract drifted.")

    serialized = strict_json_dumps(value)
    if RAW_AUTO_NAME_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime identity entered the Profile residual oracle.")


def _idf_descriptors_from_facts(facts: dict[str, Any]) -> list[dict[str, Any]]:
    descriptors: list[dict[str, Any]] = []
    objects = facts.get("objects")
    if objects is not None:
        if not isinstance(objects, list):
            raise RuntimeError("Profile IDF objects facts are not an array.")
        descriptors.extend(objects)
    return descriptors


def _validate_profile_idf_facts(identifier: str, facts: dict[str, Any]) -> None:
    expected_keys = PROFILE_IDF_FACT_KEYS.get(identifier)
    if expected_keys is None or set(facts) != expected_keys:
        raise RuntimeError(
            f"Equivalent case {identifier!r} has an invalid fact key set."
        )
    pending: list[Any] = [facts]
    while pending:
        value = pending.pop()
        if isinstance(value, dict):
            for key, item in value.items():
                lowered = key.lower()
                if any(
                    fragment in lowered
                    for fragment in FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS
                ):
                    raise RuntimeError(
                        f"Equivalent case {identifier!r} exposes a Python-container-only fact."
                    )
                pending.append(item)
        elif isinstance(value, list):
            pending.extend(value)


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import (
        DaySchedule,
        Profile,
        RuleSet,
        Schedule,
        ScheduleOperationError,
        ScheduleType,
    )

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")
    if Schedule.FIXED_LENGTH != 365 or len(Schedule.TIME_TUPLE) != 365:
        raise SystemExit("Pinned Schedule annual constants are not exact.")
    if DaySchedule.DATA_INTERVAL != 6 or DaySchedule("probe").fixed_length != 144:
        raise SystemExit("Pinned DaySchedule grid constants are not exact.")
    if not issubclass(ScheduleOperationError, TypeError):
        raise SystemExit("Pinned ScheduleOperationError inheritance is not exact.")

    definitions = case_definitions()
    cases: list[dict[str, Any]] = []
    for definition in definitions:
        observation = _execute(
            definition["id"],
            Profile,
            Schedule,
            RuleSet,
            DaySchedule,
            ScheduleType,
            ScheduleOperationError,
        )
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
        raise SystemExit("Exact CPython 3.12.7 is required for the Profile residual oracle.")
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
    print(f"Wrote Profile residual oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

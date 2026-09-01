"""Generate pinned observations for the remaining ``DaySchedule`` API.

This corpus is intentionally bounded to three high-value cases for each of the
14 still-unverified public symbols.  Run it only through
``bootstrap_reference.py`` so imports resolve from the pinned CPython 3.12.7
environment and upstream source tree.
"""

from __future__ import annotations

import argparse
import copy
from collections import Counter
import importlib.util
import math
import os
from pathlib import Path
import re
import sys
from typing import Any, Callable


SCHEMA = "dragons.invisibledragon.day-schedule-core-oracle.v1"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "DaySchedule": "sha256:3d09af6328fa8beb98a435f86468dbe5db1f906ae8eaef5db6f60b2e75d3ebad",
    "DaySchedule.__deepcopy__": "sha256:94716a2a9f9896956ef0fe11d0a43630ef22e3490260cdffd5b5eb34aed20061",
    "DaySchedule.__init__": "sha256:64dc644b6b17c50070088875126038fbd0f7fa37c6b102efc1a9fdce7c238b29",
    "DaySchedule.__setitem__": "sha256:f7d024f8afb2246d678ae93f48ec2dd247cee4a69f050dab9824e41d1043a703",
    "DaySchedule.astype": "sha256:b9602775c81765b2c8833aa2e420e788fc0e15e8ecee1cc26cea6959c1896791",
    "DaySchedule.clip": "sha256:d8d8325402e25fc7490c3ab97a5e5406a6aa81fd4529c7e17e181a5fc79eb5e7",
    "DaySchedule.compactize": "sha256:b8cb0746fc938250dd097a746f74f769d1e34cf83dbcdfdf3f83eef958581542",
    "DaySchedule.from_compact": "sha256:7584e03e29fb0ebfc974fd95edd605fc2fc5ce7d1266b6c936553fd9131d2fe9",
    "DaySchedule.from_constant": "sha256:71ce65d43f4c5ccf2fe5be57f6f7bd011138f11243e348f9b356bed85dfd1848",
    "DaySchedule.from_windows": "sha256:5a0b430f3f9b0ba4df876567989aff0675970b3993848114e381b0c69cd6b28f",
    "DaySchedule.summary": "sha256:0dc726d3cf145593aa0305902687e751b9ac6571450ca1d28acff2bf97aa5d85",
    "DaySchedule.time_tuple": "sha256:a7a04f776f37d8676cd20b07bc190cc28185f207663c2a183671f9bc016d6bbd",
    "DaySchedule.to_idf_compactexpr": "sha256:e33e015cdd6a0057839061ecfdc1103b6c88abda0e0bc896c5c813c98113dbed",
    "DaySchedule.type": "sha256:6c3809ae6a4918dfe994dbec71bfe025272a6ffdd18300b3146888cade19ced3",
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
EXPECTED_EQUIVALENT_SYMBOLS = frozenset(
    {
        "DaySchedule.compactize",
        "DaySchedule.summary",
        "DaySchedule.time_tuple",
        "DaySchedule.to_idf_compactexpr",
    }
)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "DaySchedule": "immutable-day-schedule-value-object",
    "DaySchedule.__deepcopy__": "native-day-schedule-deepcopy-memo",
    "DaySchedule.__init__": "immutable-deterministic-day-schedule-construction",
    "DaySchedule.__setitem__": "immutable-day-schedule-item-update",
    "DaySchedule.astype": "immutable-day-schedule-astype",
    "DaySchedule.clip": "immutable-day-schedule-clip",
    "DaySchedule.from_compact": "validated-deterministic-day-schedule-from-compact",
    "DaySchedule.from_constant": "deterministic-finite-day-schedule-from-constant",
    "DaySchedule.from_windows": "validated-deterministic-day-schedule-from-windows",
    "DaySchedule.type": "immutable-validated-day-schedule-type",
}
EXPECTED_CASE_COUNT = 42
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
AUTO_NAME_PATTERN = re.compile(r"^0x[0-9a-f]+$")
RAW_AUTO_NAME_PATTERN = re.compile(r"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])")
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
    path = Path(__file__).resolve().with_name("generate_day_schedule_metrics_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_day_schedule_core_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load DaySchedule oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
    ):
        raise RuntimeError("DaySchedule oracle support is not pinned.")
    return module


BASE = _load_support()
strict_json_dumps = BASE.strict_json_dumps
canonical_sha256 = BASE.canonical_sha256
sha256_file = BASE.sha256_file


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
        raise SystemExit("The inventory does not exactly cover 14 DaySchedule symbols.")
    return inventory


def _dotnet(
    adaptation: str,
    outcome: str,
    error_category: str | None = None,
) -> dict[str, str]:
    if adaptation not in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.values():
        raise RuntimeError(f"Unknown DaySchedule core adaptation {adaptation!r}.")
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
        _case("astype.inplace", "astype", "DaySchedule.astype"),
        _case("astype.invalid-atomic", "astype", "DaySchedule.astype", "raised", "domain"),
        _case("astype.outplace-string", "astype", "DaySchedule.astype"),
        _case("class.mutable-data", "class", "DaySchedule"),
        _case("class.sequence", "class", "DaySchedule"),
        _case("class.source-isolation", "class", "DaySchedule"),
        _case("clip.bounds-empty-name", "clip", "DaySchedule.clip"),
        _case("clip.reversed", "clip", "DaySchedule.clip", "raised", "domain"),
        _case("clip.signed-zero", "clip", "DaySchedule.clip"),
        _case("compactize.alternating", "compactize", "DaySchedule.compactize"),
        _case("compactize.constant", "compactize", "DaySchedule.compactize"),
        _case("compactize.signed-zero", "compactize", "DaySchedule.compactize"),
        _case("deepcopy.memo-hit", "deepcopy", "DaySchedule.__deepcopy__"),
        _case("deepcopy.normal", "deepcopy", "DaySchedule.__deepcopy__"),
        _case("deepcopy.repeated", "deepcopy", "DaySchedule.__deepcopy__"),
        _case("from-compact.invalid-end", "from-compact", "DaySchedule.from_compact", "raised", "domain"),
        _case("from-compact.off-grid", "from-compact", "DaySchedule.from_compact", "raised", "domain"),
        _case("from-compact.valid", "from-compact", "DaySchedule.from_compact"),
        _case("from-constant.anonymous-real", "from-constant", "DaySchedule.from_constant"),
        _case("from-constant.bool-onoff", "from-constant", "DaySchedule.from_constant"),
        _case("from-constant.nonfinite", "from-constant", "DaySchedule.from_constant", "raised", "domain"),
        _case("from-windows.first-overlap", "from-windows", "DaySchedule.from_windows"),
        _case("from-windows.out-of-day", "from-windows", "DaySchedule.from_windows", "raised", "domain"),
        _case("from-windows.reversed", "from-windows", "DaySchedule.from_windows", "raised", "domain"),
        _case("init.default", "init", "DaySchedule.__init__"),
        _case("init.nonfinite-real", "init", "DaySchedule.__init__", "raised", "domain"),
        _case("init.text-preservation", "init", "DaySchedule.__init__"),
        _case("setitem.invalid-atomic", "setitem", "DaySchedule.__setitem__", "raised", "domain"),
        _case("setitem.negative", "setitem", "DaySchedule.__setitem__", "raised", "range"),
        _case("setitem.positive", "setitem", "DaySchedule.__setitem__"),
        _case("summary.negative-limit", "summary", "DaySchedule.summary"),
        _case("summary.repr-name", "summary", "DaySchedule.summary"),
        _case("summary.rich", "summary", "DaySchedule.summary"),
        _case("time-tuple.fresh", "time-tuple", "DaySchedule.time_tuple"),
        _case("time-tuple.grid", "time-tuple", "DaySchedule.time_tuple"),
        _case("time-tuple.rollover", "time-tuple", "DaySchedule.time_tuple"),
        _case("to-idf.onoff", "to-idf", "DaySchedule.to_idf_compactexpr"),
        _case("to-idf.real", "to-idf", "DaySchedule.to_idf_compactexpr"),
        _case("to-idf.signed-zero", "to-idf", "DaySchedule.to_idf_compactexpr"),
        _case("type.getters", "type", "DaySchedule.type"),
        _case("type.invalid-token", "type", "DaySchedule.type", "raised", "type"),
        _case("type.stale-string-setter", "type", "DaySchedule.type", "raised", "domain"),
    )
    ordered = tuple(sorted(definitions, key=lambda item: item["id"]))
    identifiers = [item["id"] for item in ordered]
    if len(ordered) != EXPECTED_CASE_COUNT or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("DaySchedule core case identifiers are not exactly 42 unique values.")
    counts = Counter(item["symbol"] for item in ordered)
    if counts != Counter({symbol: 3 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("DaySchedule core does not contain three cases per symbol.")
    validate_case_definitions(ordered)
    return ordered


def validate_case_definitions(definitions: tuple[dict[str, Any], ...]) -> None:
    """Reject drift in the static dispatch and native-adaptation contract."""

    identifiers = [item.get("id") for item in definitions]
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} DaySchedule core cases, "
            f"got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("DaySchedule core case identifiers are not unique and sorted.")

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
            raise RuntimeError("A DaySchedule core case has an invalid identifier.")
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
            continue
        _validate_dotnet_expectation(identifier, expectation, adaptation)

    if counts != Counter({symbol: 3 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("DaySchedule core does not contain three cases per symbol.")


def _validate_dotnet_expectation(
    identifier: str,
    expectation: Any,
    adaptation: str,
) -> None:
    if not isinstance(expectation, dict):
        raise RuntimeError(f"Adapted case {identifier!r} has no native expectation.")
    keys = set(expectation)
    if keys not in {frozenset(EXPECTED_DOTNET_KEYS), frozenset(EXPECTED_DOTNET_ERROR_KEYS)}:
        raise RuntimeError(f"Case {identifier!r} has an invalid native key set.")
    if expectation.get("adaptation") != adaptation:
        raise RuntimeError(f"Case {identifier!r} has an unknown adaptation.")
    outcome = expectation.get("outcome")
    if outcome not in {"raised", "returned"}:
        raise RuntimeError(f"Case {identifier!r} has an invalid native outcome.")
    if "error_category" in expectation:
        if outcome != "raised" or expectation["error_category"] not in {
            "domain",
            "range",
            "type",
        }:
            raise RuntimeError(f"Case {identifier!r} has an invalid error category.")


def _normalize_text(value: str) -> str:
    return RAW_AUTO_NAME_PATTERN.sub("<runtime-identity>", value)


def _encode_float(value: float) -> dict[str, str]:
    # Omitting Python's ``0x`` marker prevents exact floating-point evidence
    # from being mistaken for a serialized runtime identity/address.
    return {
        "hex_without_prefix": value.hex().replace("0x", ""),
        "kind": "binary64",
    }


def normalize(value: Any) -> Any:
    if value is None or type(value) in {bool, int, str}:
        return value
    if type(value) is float:
        return _encode_float(value)
    if isinstance(value, (tuple, list)):
        return [normalize(item) for item in value]
    if isinstance(value, dict):
        if any(not isinstance(key, str) for key in value):
            raise RuntimeError("Oracle observation dictionaries require text keys.")
        return {key: normalize(item) for key, item in value.items()}
    raise RuntimeError(f"Unsupported DaySchedule observation type {type(value).__name__}.")


encode = normalize


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


class IdentityNormalizer:
    """Normalize the upstream object's default ``hex(id(...))`` name."""

    @staticmethod
    def name(value: str) -> dict[str, str]:
        return _name(value)


def _values(values: list[int | float]) -> dict[str, Any]:
    if len(values) != 144:
        raise RuntimeError("A DaySchedule observation is not exactly 144 values.")
    encoded = [encode(value) for value in values]
    for period in range(1, len(encoded) + 1):
        if len(encoded) % period == 0 and all(
            encoded[index] == encoded[index % period] for index in range(len(encoded))
        ):
            return {"encoding": "repeat", "length": 144, "pattern": encoded[:period]}
    return {"encoding": "full", "items": encoded, "length": 144}


def _name(value: str) -> dict[str, str]:
    if AUTO_NAME_PATTERN.fullmatch(value):
        return {"policy": "runtime-identity-hex"}
    if RAW_AUTO_NAME_PATTERN.search(value):
        raise RuntimeError("A literal schedule name contains a runtime identity token.")
    return {"policy": "literal", "value": value}


def _schedule(value: Any, DaySchedule: type) -> dict[str, Any]:
    if not isinstance(value, DaySchedule):
        raise RuntimeError("A DaySchedule snapshot received the wrong type.")
    return {
        "kind": "schedule",
        "name": _name(value.name),
        "schedule_type": value.type.value,
        "unit": value.unit,
        "values": _values(list(value.data)),
    }


def _exception_category(exception: Exception) -> str:
    if isinstance(exception, (IndexError, KeyError)):
        return "range"
    if isinstance(exception, (TypeError, AttributeError)):
        return "type"
    if isinstance(exception, ValueError):
        return "domain"
    raise RuntimeError(f"Unclassified Python exception {type(exception).__name__}.")


def _returned(facts: dict[str, Any]) -> dict[str, Any]:
    return {"facts": encode(facts), "outcome": "returned"}


def _raised(exception: Exception, facts: dict[str, Any] | None = None) -> dict[str, Any]:
    result: dict[str, Any] = {
        "error_category": _exception_category(exception),
        "exception_type": type(exception).__name__,
        "message": _normalize_text(str(exception)),
        "outcome": "raised",
    }
    result["facts"] = encode({} if facts is None else facts)
    return result


def _pattern(pattern: tuple[int | float, ...]) -> list[int | float]:
    return list((pattern * ((144 // len(pattern)) + 1))[:144])


def _rich_day(DaySchedule: type, ScheduleType: type) -> Any:
    values = [0.0] * 36 + [1.23456] * 12 + [10000.0] * 54 + [-0.000012345] * 42
    return DaySchedule("workday", values, type=ScheduleType.REAL, unit="kW")


def _execute(identifier: str, DaySchedule: type, ScheduleType: type) -> dict[str, Any]:
    D = DaySchedule
    T = ScheduleType
    if identifier == "class.mutable-data":
        schedule = D("mutable", [0.0] * 144)
        schedule.data[0] = 9.0
        return _returned({"first": schedule[0], "snapshot": _schedule(schedule, D)})
    if identifier == "class.sequence":
        schedule = D("sequence", list(range(144)))
        return _returned({"base_names": [item.__name__ for item in type(schedule).__mro__], "count": len(schedule), "first": schedule[0], "last": schedule[-1], "sum": sum(schedule)})
    if identifier == "class.source-isolation":
        source = [0.25] * 144
        schedule = D("isolated", source, type=T.FRACTION)
        source[0] = 1.0
        return _returned({"first": schedule[0], "source_first": source[0], "snapshot": _schedule(schedule, D)})
    if identifier == "deepcopy.memo-hit":
        schedule = D("memo", [1.0] * 144)
        sentinel = object()
        result = schedule.__deepcopy__({id(schedule): sentinel})
        return _returned({"returned_sentinel": result is sentinel})
    if identifier == "deepcopy.normal":
        schedule = D("source", _pattern((0.2, 0.8)), type=T.FRACTION, unit="ratio")
        result = copy.deepcopy(schedule)
        return _returned({"fresh": result is not schedule, "result": _schedule(result, D), "source": _schedule(schedule, D)})
    if identifier == "deepcopy.repeated":
        schedule = D("source", [2.0] * 144)
        left, right = copy.deepcopy(schedule), copy.deepcopy(schedule)
        return _returned({"distinct": left is not right and left is not schedule and right is not schedule, "left": _schedule(left, D), "right": _schedule(right, D)})
    if identifier == "init.default":
        return _returned({"result": _schedule(D(), D)})
    if identifier == "init.nonfinite-real":
        values = [math.nan, math.inf, -math.inf] + [0.0] * 141
        return _returned({"result": _schedule(D("nonfinite", values, type=T.REAL), D)})
    if identifier == "init.text-preservation":
        result = D("  padded  ", [1.0] * 144, type=T.REAL, unit="  W  ")
        return _returned({"result": _schedule(result, D)})
    if identifier in {"setitem.positive", "setitem.negative", "setitem.invalid-atomic"}:
        schedule = D("items", [0.25] * 144, type=T.FRACTION, unit="ratio")
        try:
            if identifier == "setitem.positive":
                result = schedule.__setitem__(5, 0.75)
            elif identifier == "setitem.negative":
                result = schedule.__setitem__(-1, 1.0)
            else:
                result = schedule.__setitem__(3, 2.0)
        except Exception as exception:
            return _raised(exception, {"source_after": _schedule(schedule, D)})
        return _returned({"return_is_none": result is None, "source_after": _schedule(schedule, D)})
    if identifier == "astype.outplace-string":
        source = D("typed", _pattern((0, 1)), type=T.ONOFF, unit="flag")
        result = source.astype("real")
        return _returned({"fresh": result is not source, "result": _schedule(result, D), "source": _schedule(source, D)})
    if identifier == "astype.inplace":
        source = D("typed", _pattern((0.25, 0.75)), type=T.FRACTION, unit="ratio")
        result = source.astype(T.REAL, inplace=True)
        return _returned({"return_is_none": result is None, "source_after": _schedule(source, D)})
    if identifier == "astype.invalid-atomic":
        source = D("typed", [2.0] * 144, type=T.REAL)
        before = _schedule(source, D)
        try:
            source.astype(T.FRACTION, inplace=True)
        except Exception as exception:
            return _raised(exception, {"source_after": _schedule(source, D), "source_before": before})
        raise RuntimeError("Invalid astype unexpectedly returned.")
    if identifier == "clip.bounds-empty-name":
        source = D("source", _pattern((-2.0, 2.0)), unit="kW")
        return _returned({"result": _schedule(source.clip(-1, 1, name=""), D), "source": _schedule(source, D)})
    if identifier == "clip.reversed":
        source = D("source", _pattern((-2.0, 2.0)))
        return _returned({"result": _schedule(source.clip(3, 1), D), "source": _schedule(source, D)})
    if identifier == "clip.signed-zero":
        lower = D("lower", [0.0] * 144).clip(min_value=-0.0)
        upper = D("upper", [-0.0] * 144).clip(max_value=0.0)
        return _returned({"lower": _schedule(lower, D), "upper": _schedule(upper, D)})
    if identifier == "compactize.constant":
        schedule = D("constant", [2.0] * 144)
        return _returned({"compact": schedule.compactize()})
    if identifier == "compactize.alternating":
        compact = D("alternating", _pattern((0.0, 1.0))).compactize()
        return _returned({"count": len(compact), "first": compact[0], "last": compact[-1]})
    if identifier == "compactize.signed-zero":
        compact = D("zero", ([0.0] * 143) + [-0.0]).compactize()
        return _returned({"compact": compact})
    if identifier == "from-compact.valid":
        result = D.from_compact("office", [(9, 0, 0), (18, 0, 1), (24, 0, 0)], type=T.ONOFF)
        return _returned({"result": _schedule(result, D)})
    if identifier == "from-compact.off-grid":
        result = D.from_compact("offgrid", [(0, 5, 1), (24, 0, 0)], type=T.ONOFF)
        return _returned({"result": _schedule(result, D)})
    if identifier == "from-compact.invalid-end":
        try:
            D.from_compact("bad", [(23, 50, 1)], type=T.ONOFF)
        except Exception as exception:
            return _raised(exception)
        raise RuntimeError("Invalid compact end unexpectedly returned.")
    if identifier == "from-constant.bool-onoff":
        return _returned({"result": _schedule(D.from_constant("on", True, type=T.ONOFF), D)})
    if identifier == "from-constant.anonymous-real":
        return _returned({"result": _schedule(D.from_constant(None, 4.7, type=T.REAL), D)})
    if identifier == "from-constant.nonfinite":
        return _returned({"result": _schedule(D.from_constant("nan", math.nan, type=T.REAL), D)})
    if identifier == "from-windows.first-overlap":
        result = D.from_windows("overlap", 0, [((8, 0), (12, 0), 1), ((9, 0), (11, 0), 2)], type=T.REAL)
        return _returned({"result": _schedule(result, D)})
    if identifier == "from-windows.reversed":
        result = D.from_windows("reversed", 0, [((18, 0), (9, 0), 1)], type=T.REAL)
        return _returned({"result": _schedule(result, D)})
    if identifier == "from-windows.out-of-day":
        result = D.from_windows("outday", 0, [((-1, 0), (1, 0), 1)], type=T.REAL)
        return _returned({"result": _schedule(result, D)})
    if identifier == "summary.rich":
        schedule = _rich_day(D, T)
        return _returned({"summary": schedule.summary()})
    if identifier == "summary.negative-limit":
        schedule = _rich_day(D, T)
        return _returned({"summary": schedule.summary(max_segments=-1)})
    if identifier == "summary.repr-name":
        schedule = D("a'b", [1.0] * 144, type=T.REAL, unit="W")
        return _returned({"summary": schedule.summary(max_segments=0)})
    if identifier == "time-tuple.grid":
        values = D.time_tuple()
        return _returned({"count": len(values), "items": values})
    if identifier == "time-tuple.fresh":
        left, right = D.time_tuple(), D.time_tuple()
        return _returned(
            {
                "distinct": left is not right,
                "left_count": len(left),
                "right_count": len(right),
                "same_values": left == right,
            }
        )
    if identifier == "time-tuple.rollover":
        values = D.time_tuple()
        return _returned({"hour_end": values[5], "last": values[-1], "midnight_first": values[0]})
    if identifier == "to-idf.real":
        return _returned({"fields": _rich_day(D, T).to_idf_compactexpr()})
    if identifier == "to-idf.onoff":
        return _returned({"fields": D.from_constant("on", 1, type=T.ONOFF).to_idf_compactexpr()})
    if identifier == "to-idf.signed-zero":
        return _returned({"fields": D.from_constant("negative-zero", -0.0, type=T.REAL).to_idf_compactexpr()})
    if identifier == "type.getters":
        values = [D(value, [0.0] * 144, type=T(value)).type.value for value in ("onoff", "fraction", "real", "temperature")]
        return _returned({"types": values})
    if identifier == "type.stale-string-setter":
        schedule = D("stale", [2.0] * 144, type=T.REAL)
        schedule.type = "fraction"
        return _returned({"result": _schedule(schedule, D)})
    if identifier == "type.invalid-token":
        schedule = D("typed", [0.0] * 144)
        try:
            schedule.type = "invalid"
        except Exception as exception:
            return _raised(exception, {"source_after": _schedule(schedule, D)})
        raise RuntimeError("Invalid type token unexpectedly returned.")
    raise RuntimeError(f"Unknown DaySchedule core case {identifier!r}.")


def _require_exact_keys(value: Any, expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(
            f"{context} has an invalid key set: expected {sorted(expected)}, got {actual}."
        )


def validate_oracle(value: dict[str, Any]) -> None:
    """Validate the complete generated artifact before any bytes are written."""

    _require_exact_keys(value, ORACLE_KEYS, "DaySchedule core oracle top-level root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("The DaySchedule core oracle schema drifted.")

    upstream = value["upstream"]
    _require_exact_keys(upstream, UPSTREAM_KEYS, "DaySchedule core upstream receipt")
    if upstream != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("The DaySchedule core upstream receipt is not exact.")

    runtime = value["runtime"]
    _require_exact_keys(runtime, RUNTIME_KEYS, "DaySchedule core runtime receipt")
    if runtime != {
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("The DaySchedule core runtime receipt is not exact.")

    symbols = value["symbols"]
    if not isinstance(symbols, list) or len(symbols) != len(TARGET_SYMBOLS):
        raise RuntimeError("The DaySchedule core symbol receipt count is not exact.")
    for expected_symbol, receipt in zip(TARGET_SYMBOLS, symbols, strict=True):
        _require_exact_keys(receipt, SYMBOL_KEYS, f"Symbol receipt {expected_symbol!r}")
        if receipt["symbol"] != expected_symbol:
            raise RuntimeError("The DaySchedule core symbol receipt order drifted.")
        if receipt["path"] != SOURCE_PATH:
            raise RuntimeError(f"Symbol {expected_symbol!r} points to the wrong source.")
        if receipt["symbol_hash"] != EXPECTED_SYMBOL_HASHES[expected_symbol]:
            raise RuntimeError(f"Symbol {expected_symbol!r} has the wrong hash.")

    definitions = case_definitions()
    definition_by_id = {item["id"]: item for item in definitions}
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("The DaySchedule core oracle case count is not exact.")
    identifiers = [item.get("id") for item in cases if isinstance(item, dict)]
    if identifiers != [item["id"] for item in definitions]:
        raise RuntimeError("The DaySchedule core oracle case order drifted.")

    for case in cases:
        if not isinstance(case, dict):
            raise RuntimeError("A DaySchedule core oracle case is not an object.")
        identifier = case.get("id")
        if identifier not in definition_by_id:
            raise RuntimeError(f"Unknown DaySchedule core oracle case {identifier!r}.")
        definition = definition_by_id[identifier]
        expected_keys = CASE_KEYS
        if definition["expected_dotnet"] is not None:
            expected_keys = expected_keys | {"expected_dotnet"}
        _require_exact_keys(case, expected_keys, f"Oracle case {identifier!r}")
        if case["executor"] != definition["executor"]:
            raise RuntimeError(f"Case {identifier!r} executor drifted.")
        if case["symbol"] != definition["symbol"]:
            raise RuntimeError(f"Case {identifier!r} symbol drifted.")
        if definition["expected_dotnet"] is not None:
            if case["expected_dotnet"] != definition["expected_dotnet"]:
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
                raise RuntimeError(f"Case {identifier!r} has an invalid Python error category.")
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
        raise RuntimeError("The DaySchedule core cases hash is invalid.")

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
        raise RuntimeError("The DaySchedule core consumer contract drifted.")

    serialized = strict_json_dumps(value)
    if RAW_AUTO_NAME_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime identity name entered the DaySchedule core oracle.")


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import DaySchedule, ScheduleType

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")
    if DaySchedule.DATA_INTERVAL != 6 or DaySchedule("probe").fixed_length != 144:
        raise SystemExit("Pinned DaySchedule grid constants are not exact.")

    definitions = case_definitions()
    cases: list[dict[str, Any]] = []
    for definition in definitions:
        observation = _execute(definition["id"], DaySchedule, ScheduleType)
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
                symbol: "equivalent" if symbol in EXPECTED_EQUIVALENT_SYMBOLS else "exception"
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
        raise SystemExit("Exact CPython 3.12.7 is required for the DaySchedule oracle.")
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
    print(f"Wrote DaySchedule core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

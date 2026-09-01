"""Generate pinned observations for the four in-scope ``utils.py`` symbols.

The corpus contains exactly three cases for each symbol.  Run this generator
only through ``bootstrap_reference.py`` so imports resolve from the pinned
CPython 3.12.7 dependency tree and upstream checkout.
"""

from __future__ import annotations

import argparse
from collections import Counter
from copy import deepcopy
from enum import Enum
import importlib.util
import os
from pathlib import Path
import re
import sys
from typing import Any, Callable


SCHEMA = "dragons.simpledragon.utils-core-oracle.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCES = (
    {
        "path": "src/epsimple/utils.py",
        "source_sha256": (
            "sha256:4b19874951feb696f0a5f1b42d85a11c405e5f83958828997af9a977a6aa9cf8"
        ),
    },
    {
        "path": "src/idragon/utils.py",
        "source_sha256": (
            "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd"
        ),
    },
)
EXPECTED_SYMBOL_RECEIPTS = {
    "GRJSON_FORMAT": {
        "body_hash": "sha256:bfd54ded3c829caf3ffe7c5b15a3692067451f5f758d2bc89df825fb39c4409e",
        "kind": "constant",
        "path": "src/epsimple/utils.py",
        "signature_hash": "sha256:d85c1609b8dd75fa0730679f37f9ee903e8f5cb3f7aadb6d2f81b72cc03bfe8e",
        "symbol_hash": "sha256:6c3ef8ba838797c6783d1ed35b52dcd6b4eb364baa529820c6df9ed8dfb2e75e",
    },
    "validate_enum": {
        "body_hash": "sha256:38228c97c0219e1c852349edbbfec7cdc92cf88421439e0bf3f0e99c0c8f3558",
        "kind": "function",
        "path": "src/idragon/utils.py",
        "signature_hash": "sha256:a1cad1caa130af3a903461789f644227240d4623fe1f248ed8865270b8b9e1cc",
        "symbol_hash": "sha256:8b3b34b63f7091d045c421b0309c3549f935ee47aa704faf4931be786991402c",
    },
    "validate_range": {
        "body_hash": "sha256:c92ba78111abd3b3bbd34d23a8f932ef366f93d1d673c455497e4d663189bf7e",
        "kind": "function",
        "path": "src/idragon/utils.py",
        "signature_hash": "sha256:5326abdf0b673e41a11c76f5f481e600209f97011936c814d4e4a518b38c8f17",
        "symbol_hash": "sha256:a5710a725c7060dead58c254874c24d8c82b0e25d08cc88abff1e68275fcb0b1",
    },
    "validate_type": {
        "body_hash": "sha256:4e168989f6958d9a6aa63f5af53727b21bcbe2a154d9e44132f9944cfd99a7bf",
        "kind": "function",
        "path": "src/idragon/utils.py",
        "signature_hash": "sha256:aad965d407adc54c3b5324be5dd2c3d2d6ea1786fe01f5c0b32b698b353019fb",
        "symbol_hash": "sha256:d2d6da05e97ccf6815cd924a3c8e4502fcb9055aa771281f95f609cd11c6eb26",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "GRJSON_FORMAT": "immutable-validated-grm-template",
    "validate_enum": "strongly-typed-native-enum-validation",
    "validate_range": "finite-native-range-validation",
    "validate_type": "strongly-typed-native-type-validation",
}
EXPECTED_CASE_COUNT = 12
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

EXPECTED_GRJSON_TEMPLATE = {
    "building": {
        "name": "",
        "north_axis": 0,
        "address": "",
        "vintage": [1900, 1, 1],
        "num_aboveground_floors": 0,
        "num_underground_floors": 0,
        "floors": [],
        "supply_systems": [],
        "source_systems": [],
        "ventilation_systems": [],
        "photovoltaic_systems": [],
    },
    "materials": [],
    "surface_constructions": [],
    "fenestration_constructions": [],
}
EXPECTED_ROOT_KEY_ORDER = tuple(EXPECTED_GRJSON_TEMPLATE)
EXPECTED_BUILDING_KEY_ORDER = tuple(EXPECTED_GRJSON_TEMPLATE["building"])

ORACLE_KEYS = {
    "cases",
    "cases_sha256",
    "consumer_contract",
    "runtime",
    "schema",
    "symbols",
    "upstream",
}
CASE_KEYS = {"executor", "expected_dotnet", "id", "python", "symbol"}
CASE_DEFINITION_KEYS = {"executor", "expected_dotnet", "id", "symbol"}
EXPECTED_DOTNET_KEYS = {"adaptation", "outcome"}
PYTHON_RETURN_KEYS = {"facts", "outcome"}
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
UPSTREAM_KEYS = {"commit", "inventory_sha256", "sources"}
SOURCE_KEYS = {"path", "source_sha256"}
SYMBOL_KEYS = {
    "body_hash",
    "kind",
    "path",
    "signature_hash",
    "symbol",
    "symbol_hash",
}
RETURNED_OBSERVATION_KEYS = {"outcome", "result"}
RAISED_OBSERVATION_KEYS = {
    "error_category",
    "exception_type",
    "message",
    "outcome",
}
CASE_FACT_KEYS = {
    "grjson-format.copy-isolation": {
        "building_is_distinct",
        "copy_mutation_isolated",
        "floors_is_distinct",
        "root_is_distinct",
        "vintage_is_distinct",
    },
    "grjson-format.exact-defaults": {
        "building_key_order",
        "root_key_order",
        "snapshot",
    },
    "grjson-format.shared-global-mutation": {
        "alias_is_same",
        "mutation_visible",
        "nested_alias_is_same",
        "restored_exactly",
    },
    "validate-enum.accepted-members-and-raw-values": {"observations"},
    "validate-enum.none-and-wraps": {"metadata", "observations"},
    "validate-enum.rejection-surface": {"observations"},
    "validate-range.inclusive-boundaries": {"metadata", "observations"},
    "validate-range.none-and-nonfinite": {"observations"},
    "validate-range.rejection-surface": {"observations"},
    "validate-type.allow-none-and-wraps": {"metadata", "observations"},
    "validate-type.rejection-surface": {"observations"},
    "validate-type.union-subclass-and-bool": {"observations"},
}
RAW_ADDRESS_PATTERN = re.compile(
    r"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])"
)
BINARY64_PATTERN = re.compile(
    r"^-?(?:[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+|inf|nan)$"
)


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_day_schedule_core_oracle.py")
    spec = importlib.util.spec_from_file_location("_dragons_utils_core_support", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load utils core oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Utils core oracle support is not pinned.")
    return module


BASE = _load_support()
strict_json_dumps = BASE.strict_json_dumps
canonical_sha256 = BASE.canonical_sha256
sha256_file = BASE.sha256_file
normalize = BASE.normalize


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _load_source_inventory(
    path: Path,
    upstream_commit: str,
    source: dict[str, str],
    symbols: tuple[str, ...],
) -> dict[str, Any]:
    """Run the hardened full-inventory validator for one exact source slice."""

    support = BASE.BASE
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(support, name) for name in names}
    try:
        support.SOURCE_PATH = source["path"]
        support.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        support.EXPECTED_SYMBOL_HASHES = {
            symbol: EXPECTED_SYMBOL_HASHES[symbol] for symbol in symbols
        }
        support.TARGET_SYMBOLS = symbols
        return support.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(support, name, value)


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    source_symbols = (
        (EXPECTED_SOURCES[0], ("GRJSON_FORMAT",)),
        (
            EXPECTED_SOURCES[1],
            ("validate_enum", "validate_range", "validate_type"),
        ),
    )
    slices = [
        _load_source_inventory(path, upstream_commit, source, symbols)
        for source, symbols in source_symbols
    ]
    content_hashes = {item["content_sha256"] for item in slices}
    if content_hashes != {EXPECTED_INVENTORY_SHA256}:
        raise SystemExit("The utils slices do not share the exact pinned inventory.")

    files = [item["file"] for item in slices]
    symbols = [symbol for item in slices for symbol in item["symbols"]]
    if [item["path"] for item in files] != [item["path"] for item in EXPECTED_SOURCES]:
        raise SystemExit("The utils source receipts are not in canonical order.")
    if [item["symbol"] for item in symbols] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover four utils symbols.")
    for item in symbols:
        expected = {
            **EXPECTED_SYMBOL_RECEIPTS[item["symbol"]],
            "symbol": item["symbol"],
        }
        if item != expected:
            raise SystemExit(f"The inventory receipt for {item['symbol']!r} is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": files,
        "symbols": symbols,
    }


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    return {
        "executor": executor,
        "expected_dotnet": {
            "adaptation": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS[symbol],
            "outcome": "returned",
        },
        "id": identifier,
        "symbol": symbol,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = (
        _case("grjson-format.copy-isolation", "grjson-format", "GRJSON_FORMAT"),
        _case("grjson-format.exact-defaults", "grjson-format", "GRJSON_FORMAT"),
        _case(
            "grjson-format.shared-global-mutation",
            "grjson-format",
            "GRJSON_FORMAT",
        ),
        _case(
            "validate-enum.accepted-members-and-raw-values",
            "validate-enum",
            "validate_enum",
        ),
        _case(
            "validate-enum.none-and-wraps", "validate-enum", "validate_enum"
        ),
        _case(
            "validate-enum.rejection-surface", "validate-enum", "validate_enum"
        ),
        _case(
            "validate-range.inclusive-boundaries",
            "validate-range",
            "validate_range",
        ),
        _case(
            "validate-range.none-and-nonfinite",
            "validate-range",
            "validate_range",
        ),
        _case(
            "validate-range.rejection-surface",
            "validate-range",
            "validate_range",
        ),
        _case(
            "validate-type.allow-none-and-wraps", "validate-type", "validate_type"
        ),
        _case(
            "validate-type.rejection-surface", "validate-type", "validate_type"
        ),
        _case(
            "validate-type.union-subclass-and-bool",
            "validate-type",
            "validate_type",
        ),
    )
    ordered = tuple(sorted(definitions, key=lambda item: item["id"]))
    validate_case_definitions(ordered)
    return ordered


def validate_case_definitions(definitions: tuple[dict[str, Any], ...]) -> None:
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Utils core must contain exactly twelve cases.")
    identifiers = [item.get("id") for item in definitions]
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("Utils core case identifiers are not unique and sorted.")
    counts: Counter[str] = Counter()
    for definition in definitions:
        if set(definition) != CASE_DEFINITION_KEYS:
            raise RuntimeError(f"Invalid case definition {definition.get('id')!r}.")
        symbol = definition["symbol"]
        if symbol not in TARGET_SYMBOLS:
            raise RuntimeError(f"Unknown utils symbol {symbol!r}.")
        if not isinstance(definition["executor"], str) or not definition["executor"]:
            raise RuntimeError(f"Invalid executor for {definition['id']!r}.")
        expectation = definition["expected_dotnet"]
        if not isinstance(expectation, dict) or set(expectation) != EXPECTED_DOTNET_KEYS:
            raise RuntimeError(f"Invalid native expectation for {definition['id']!r}.")
        if expectation != {
            "adaptation": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS[symbol],
            "outcome": "returned",
        }:
            raise RuntimeError(f"Stale native expectation for {definition['id']!r}.")
        counts[symbol] += 1
    if counts != Counter({symbol: 3 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Utils core does not contain three cases per symbol.")


def _value(value: Any) -> dict[str, Any]:
    if isinstance(value, Enum):
        return {"python_type": type(value).__name__, "value": value.value}
    return {
        "python_type": type(value).__name__,
        "value": int(value) if type(value).__name__ == "IntChild" else value,
    }


def _exception_category(exception: Exception) -> str:
    if isinstance(exception, TypeError):
        return "type"
    if isinstance(exception, ValueError):
        return "domain"
    raise RuntimeError(f"Unclassified utils exception {type(exception).__name__}.")


def _observe(call: Callable[[], Any]) -> dict[str, Any]:
    try:
        return {"outcome": "returned", "result": call()}
    except Exception as exception:  # Exact pinned behavior is oracle data.
        return {
            "error_category": _exception_category(exception),
            "exception_type": type(exception).__name__,
            "message": str(exception),
            "outcome": "raised",
        }


def _returned(facts: dict[str, Any]) -> dict[str, Any]:
    return {"facts": normalize(facts), "outcome": "returned"}


def _execute_grjson(identifier: str, template: dict[str, Any]) -> dict[str, Any]:
    if identifier == "grjson-format.exact-defaults":
        return _returned(
            {
                "building_key_order": list(template["building"]),
                "root_key_order": list(template),
                "snapshot": template,
            }
        )
    if identifier == "grjson-format.copy-isolation":
        before = deepcopy(template)
        copied = deepcopy(template)
        copied["building"]["name"] = "copy-only"
        copied["building"]["vintage"][0] = 2099
        copied["building"]["floors"].append({"floor_number": 1})
        return _returned(
            {
                "building_is_distinct": copied["building"] is not template["building"],
                "copy_mutation_isolated": template == before,
                "floors_is_distinct": (
                    copied["building"]["floors"] is not template["building"]["floors"]
                ),
                "root_is_distinct": copied is not template,
                "vintage_is_distinct": (
                    copied["building"]["vintage"] is not template["building"]["vintage"]
                ),
            }
        )
    if identifier == "grjson-format.shared-global-mutation":
        before = deepcopy(template)
        alias = template
        building = template["building"]
        floors = building["floors"]
        try:
            building["name"] = "shared-mutation"
            floors.append({"floor_number": 99, "zones": []})
            facts = {
                "alias_is_same": alias is template,
                "mutation_visible": (
                    alias["building"]["name"] == "shared-mutation"
                    and len(alias["building"]["floors"]) == 1
                ),
                "nested_alias_is_same": alias["building"] is building,
            }
        finally:
            building["name"] = before["building"]["name"]
            floors.clear()
            floors.extend(before["building"]["floors"])
        facts["restored_exactly"] = template == before
        return _returned(facts)
    raise RuntimeError(f"Unknown GRJSON_FORMAT case {identifier!r}.")


def _execute_enum(identifier: str, validate_enum: Callable[..., Any]) -> dict[str, Any]:
    class StringMode(str, Enum):
        Alpha = "alpha"
        Beta = "beta"

    class NumberMode(Enum):
        One = 1
        Two = 2

    class Probe:
        @validate_enum(StringMode)
        def mode(self, value: Any) -> dict[str, Any]:
            """String-backed enum probe."""
            return _value(value)

        @validate_enum(StringMode, None)
        def optional(self, value: Any) -> dict[str, Any]:
            """Optional enum probe."""
            return _value(value)

        @validate_enum(NumberMode)
        def number(self, value: Any) -> dict[str, Any]:
            return _value(value)

    probe = Probe()
    if identifier == "validate-enum.accepted-members-and-raw-values":
        inputs = (StringMode.Alpha, "alpha", StringMode.Beta, "beta")
        return _returned(
            {"observations": [_observe(lambda item=item: probe.mode(item)) for item in inputs]}
        )
    if identifier == "validate-enum.none-and-wraps":
        return _returned(
            {
                "metadata": {
                    "doc": probe.optional.__doc__,
                    "has_wrapped": hasattr(probe.optional, "__wrapped__"),
                    "name": probe.optional.__name__,
                },
                "observations": [
                    _observe(lambda: probe.optional(None)),
                    _observe(lambda: probe.optional(StringMode.Alpha)),
                ],
            }
        )
    if identifier == "validate-enum.rejection-surface":
        return _returned(
            {
                "observations": [
                    _observe(lambda: probe.mode("unknown")),
                    _observe(lambda: probe.optional("unknown")),
                    _observe(lambda: probe.number(3)),
                ]
            }
        )
    raise RuntimeError(f"Unknown validate_enum case {identifier!r}.")


def _execute_range(identifier: str, validate_range: Callable[..., Any]) -> dict[str, Any]:
    class Probe:
        @validate_range(min=0, max=1)
        def fraction(self, value: Any) -> dict[str, Any]:
            """Inclusive range probe."""
            return _value(value)

    probe = Probe()
    if identifier == "validate-range.inclusive-boundaries":
        return _returned(
            {
                "metadata": {
                    "doc": probe.fraction.__doc__,
                    "has_wrapped": hasattr(probe.fraction, "__wrapped__"),
                    "name": probe.fraction.__name__,
                },
                "observations": [
                    _observe(lambda: probe.fraction(0)),
                    _observe(lambda: probe.fraction(1)),
                    _observe(lambda: probe.fraction(0.5)),
                ],
            }
        )
    if identifier == "validate-range.none-and-nonfinite":
        return _returned(
            {
                "observations": [
                    _observe(lambda: probe.fraction(None)),
                    _observe(lambda: probe.fraction(float("nan"))),
                    _observe(lambda: probe.fraction(float("inf"))),
                    _observe(lambda: probe.fraction(float("-inf"))),
                ]
            }
        )
    if identifier == "validate-range.rejection-surface":
        return _returned(
            {
                "observations": [
                    _observe(lambda: probe.fraction(-1)),
                    _observe(lambda: probe.fraction(2)),
                    _observe(lambda: probe.fraction("0.5")),
                ]
            }
        )
    raise RuntimeError(f"Unknown validate_range case {identifier!r}.")


def _execute_type(identifier: str, validate_type: Callable[..., Any]) -> dict[str, Any]:
    class IntChild(int):
        pass

    class Probe:
        @validate_type(int, str)
        def union(self, value: Any) -> dict[str, Any]:
            """Union type probe."""
            return _value(value)

        @validate_type(int, allow_none=True)
        def optional(self, value: Any) -> dict[str, Any]:
            """Optional type probe."""
            return _value(value)

    probe = Probe()
    if identifier == "validate-type.union-subclass-and-bool":
        inputs = (7, "seven", True, IntChild(8))
        return _returned(
            {"observations": [_observe(lambda item=item: probe.union(item)) for item in inputs]}
        )
    if identifier == "validate-type.allow-none-and-wraps":
        return _returned(
            {
                "metadata": {
                    "doc": probe.optional.__doc__,
                    "has_wrapped": hasattr(probe.optional, "__wrapped__"),
                    "name": probe.optional.__name__,
                },
                "observations": [
                    _observe(lambda: probe.optional(None)),
                    _observe(lambda: probe.optional(4)),
                ],
            }
        )
    if identifier == "validate-type.rejection-surface":
        return _returned(
            {
                "observations": [
                    _observe(lambda: probe.union(3.5)),
                    _observe(lambda: probe.union(None)),
                    _observe(lambda: probe.union([1, 2])),
                ]
            }
        )
    raise RuntimeError(f"Unknown validate_type case {identifier!r}.")


def _execute(
    definition: dict[str, Any],
    template: dict[str, Any],
    validate_enum: Callable[..., Any],
    validate_range: Callable[..., Any],
    validate_type: Callable[..., Any],
) -> dict[str, Any]:
    executor = definition["executor"]
    if executor == "grjson-format":
        return _execute_grjson(definition["id"], template)
    if executor == "validate-enum":
        return _execute_enum(definition["id"], validate_enum)
    if executor == "validate-range":
        return _execute_range(definition["id"], validate_range)
    if executor == "validate-type":
        return _execute_type(definition["id"], validate_type)
    raise RuntimeError(f"Unknown utils executor {executor!r}.")


def _require_exact_keys(value: Any, expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{context} key set is not exact: {actual!r}.")


def _validate_normalized_tree(value: Any, context: str) -> None:
    if isinstance(value, float):
        raise RuntimeError(f"Raw float is forbidden at {context}.")
    if isinstance(value, str):
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"Raw runtime address is forbidden at {context}.")
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_normalized_tree(item, f"{context}[{index}]")
        return
    if isinstance(value, dict):
        if value.get("kind") == "binary64":
            _require_exact_keys(
                value, {"hex_without_prefix", "kind"}, f"Binary64 value at {context}"
            )
            encoded = value["hex_without_prefix"]
            if not isinstance(encoded, str) or BINARY64_PATTERN.fullmatch(encoded) is None:
                raise RuntimeError(f"Invalid binary64 value at {context}.")
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"Non-text observation key at {context}.")
            _validate_normalized_tree(item, f"{context}.{key}")
        return
    if value is not None and type(value) not in {bool, int}:
        raise RuntimeError(f"Unsupported observation value at {context}: {type(value).__name__}.")


def _validate_observations(value: Any, context: str) -> None:
    if not isinstance(value, list) or not value:
        raise RuntimeError(f"{context} must contain observations.")
    for index, item in enumerate(value):
        if not isinstance(item, dict):
            raise RuntimeError(f"{context}[{index}] is not an object.")
        if item.get("outcome") == "returned":
            _require_exact_keys(item, RETURNED_OBSERVATION_KEYS, f"{context}[{index}]")
        elif item.get("outcome") == "raised":
            _require_exact_keys(item, RAISED_OBSERVATION_KEYS, f"{context}[{index}]")
            if item["error_category"] not in {"domain", "type"}:
                raise RuntimeError(f"{context}[{index}] has an invalid error category.")
            if not isinstance(item["exception_type"], str) or not item["exception_type"]:
                raise RuntimeError(f"{context}[{index}] has no exception type.")
            if not isinstance(item["message"], str):
                raise RuntimeError(f"{context}[{index}] has no exception message.")
        else:
            raise RuntimeError(f"{context}[{index}] has an invalid outcome.")


def _case_by_id(value: dict[str, Any], identifier: str) -> dict[str, Any]:
    return next(item for item in value["cases"] if item["id"] == identifier)


def _validate_semantics(value: dict[str, Any]) -> None:
    exact = _case_by_id(value, "grjson-format.exact-defaults")["python"]["facts"]
    if exact != {
        "building_key_order": list(EXPECTED_BUILDING_KEY_ORDER),
        "root_key_order": list(EXPECTED_ROOT_KEY_ORDER),
        "snapshot": EXPECTED_GRJSON_TEMPLATE,
    }:
        raise RuntimeError("The exact GRJSON_FORMAT default contract drifted.")
    copied = _case_by_id(value, "grjson-format.copy-isolation")["python"]["facts"]
    if set(copied.values()) != {True}:
        raise RuntimeError("The GRJSON_FORMAT deepcopy isolation contract drifted.")
    shared = _case_by_id(value, "grjson-format.shared-global-mutation")["python"]["facts"]
    if set(shared.values()) != {True}:
        raise RuntimeError("The GRJSON_FORMAT shared mutation contract drifted.")

    enum_rejections = _case_by_id(
        value, "validate-enum.rejection-surface"
    )["python"]["facts"]["observations"]
    if [item["exception_type"] for item in enum_rejections] != [
        "ValueError",
        "TypeError",
        "TypeError",
    ]:
        raise RuntimeError("The validate_enum rejection surface drifted.")
    if enum_rejections[0]["message"] != (
        "Invalid value 'unknown' for mode. Allowed values: alpha,beta"
    ):
        raise RuntimeError("The validate_enum ValueError message drifted.")

    nonfinite = _case_by_id(
        value, "validate-range.none-and-nonfinite"
    )["python"]["facts"]["observations"]
    if nonfinite[0]["outcome"] != "returned" or nonfinite[0]["result"] != {
        "python_type": "NoneType",
        "value": None,
    }:
        raise RuntimeError("The validate_range None bypass drifted.")
    if nonfinite[1]["outcome"] != "returned" or nonfinite[1]["result"]["value"] != {
        "hex_without_prefix": "nan",
        "kind": "binary64",
    }:
        raise RuntimeError("The validate_range NaN bypass drifted.")
    range_rejections = _case_by_id(
        value, "validate-range.rejection-surface"
    )["python"]["facts"]["observations"]
    if range_rejections[1]["message"] != (
        "Value '2' for fraction is below the maxmimum 1."
    ):
        raise RuntimeError("The pinned validate_range maximum message drifted.")

    union = _case_by_id(
        value, "validate-type.union-subclass-and-bool"
    )["python"]["facts"]["observations"]
    if [item["result"]["python_type"] for item in union] != [
        "int",
        "str",
        "bool",
        "IntChild",
    ]:
        raise RuntimeError("The validate_type isinstance contract drifted.")
    if union[2]["result"] != {"python_type": "bool", "value": True}:
        raise RuntimeError("The validate_type bool-as-int behavior drifted.")


def validate_oracle(value: dict[str, Any]) -> None:
    """Fail closed on the complete artifact before writing any bytes."""

    _require_exact_keys(value, ORACLE_KEYS, "Utils core oracle root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("The utils core oracle schema drifted.")

    upstream = value["upstream"]
    _require_exact_keys(upstream, UPSTREAM_KEYS, "Utils upstream receipt")
    if upstream["commit"] != EXPECTED_UPSTREAM_COMMIT:
        raise RuntimeError("The utils upstream commit drifted.")
    if upstream["inventory_sha256"] != EXPECTED_INVENTORY_SHA256:
        raise RuntimeError("The utils inventory receipt drifted.")
    sources = upstream["sources"]
    if not isinstance(sources, list) or sources != list(EXPECTED_SOURCES):
        raise RuntimeError("The utils source receipts drifted.")
    for index, source in enumerate(sources):
        _require_exact_keys(source, SOURCE_KEYS, f"Utils source receipt {index}")

    runtime = value["runtime"]
    _require_exact_keys(runtime, RUNTIME_KEYS, "Utils runtime receipt")
    if runtime != {
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("The utils runtime receipt drifted.")

    symbols = value["symbols"]
    if not isinstance(symbols, list) or len(symbols) != len(TARGET_SYMBOLS):
        raise RuntimeError("The utils symbol receipt count is not exact.")
    for expected_symbol, receipt in zip(TARGET_SYMBOLS, symbols, strict=True):
        _require_exact_keys(receipt, SYMBOL_KEYS, f"Symbol receipt {expected_symbol!r}")
        if receipt != {
            **EXPECTED_SYMBOL_RECEIPTS[expected_symbol],
            "symbol": expected_symbol,
        }:
            raise RuntimeError(f"Symbol receipt {expected_symbol!r} is not exact.")

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("The utils case count is not exact.")
    if [case.get("id") for case in cases if isinstance(case, dict)] != [
        item["id"] for item in definitions
    ]:
        raise RuntimeError("The utils case order drifted.")
    for case, definition in zip(cases, definitions, strict=True):
        _require_exact_keys(case, CASE_KEYS, f"Utils case {definition['id']!r}")
        for key in ("executor", "expected_dotnet", "id", "symbol"):
            if case[key] != definition[key]:
                raise RuntimeError(f"Utils case {definition['id']!r} binding drifted.")
        python = case["python"]
        _require_exact_keys(python, PYTHON_RETURN_KEYS, f"Python case {definition['id']!r}")
        if python["outcome"] != "returned":
            raise RuntimeError(f"Python case {definition['id']!r} did not return.")
        facts = python["facts"]
        _require_exact_keys(facts, CASE_FACT_KEYS[definition["id"]], f"Facts {definition['id']!r}")
        if "observations" in facts:
            _validate_observations(facts["observations"], definition["id"])
        _validate_normalized_tree(python, f"case.{definition['id']}.python")

    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("The utils cases hash is invalid.")
    consumer = value["consumer_contract"]
    _require_exact_keys(consumer, CONSUMER_CONTRACT_KEYS, "Utils consumer contract")
    expected_classifications = {symbol: "exception" for symbol in TARGET_SYMBOLS}
    if consumer != {
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": [item["id"] for item in definitions],
        "classifications": expected_classifications,
        "float_encoding": "python-binary64-hex-without-0x-prefix",
        "runtime_names": "policy-token-no-raw-address",
        "target_symbols": list(TARGET_SYMBOLS),
    }:
        raise RuntimeError("The utils consumer contract drifted.")
    _validate_semantics(value)
    serialized = strict_json_dumps(value)
    if RAW_ADDRESS_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime address entered the utils oracle.")


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import epsimple.utils as epsimple_utils
    import idragon.utils as idragon_utils

    imported_modules = (epsimple_utils, idragon_utils)
    for module, expected, inventoried in zip(
        imported_modules, EXPECTED_SOURCES, inventory["files"], strict=True
    ):
        imported_path = Path(module.__file__).resolve()
        imported_sha256 = sha256_file(imported_path)
        if imported_sha256 != expected["source_sha256"]:
            raise SystemExit(f"Imported utils source is not pinned: {expected['path']}.")
        if imported_sha256 != inventoried["content_hash"]:
            raise SystemExit(f"Imported utils source is not inventoried: {expected['path']}.")

    if epsimple_utils.GRJSON_FORMAT != EXPECTED_GRJSON_TEMPLATE:
        raise SystemExit("The imported GRJSON_FORMAT default tree is not exact.")
    definitions = case_definitions()
    cases = []
    for definition in definitions:
        cases.append(
            {
                "executor": definition["executor"],
                "expected_dotnet": definition["expected_dotnet"],
                "id": definition["id"],
                "python": _execute(
                    definition,
                    epsimple_utils.GRJSON_FORMAT,
                    idragon_utils.validate_enum,
                    idragon_utils.validate_range,
                    idragon_utils.validate_type,
                ),
                "symbol": definition["symbol"],
            }
        )
    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": {
            "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
            "case_count": EXPECTED_CASE_COUNT,
            "case_ids": [item["id"] for item in definitions],
            "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
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
            "sources": list(EXPECTED_SOURCES),
        },
    }
    validate_oracle(result)
    return result


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the utils core oracle.")
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
    print(f"Wrote utils core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

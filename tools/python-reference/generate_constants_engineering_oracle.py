"""Generate the pinned ``idragon/constants.py`` engineering oracle.

The corpus covers only the two engineering constant containers and their six
public members.  Package paths and metadata intentionally remain outside this
fixture.  Exactly three deterministic cases bind each target symbol.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import importlib
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import re
import sys
from typing import Any, Iterator


SCHEMA = "dragons.python-reference.constants-engineering.v1"
SOURCE_PATH = "src/idragon/constants.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "THERMAL": {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "kind": "class",
        "signature_hash": "sha256:1a8e65ce71d37c495d404d7e8379dc1e3007bea81f99cca0d6c39c13f281d902",
        "symbol_hash": "sha256:c55d90e3a5f7120226dc556d856b18c8070aac02531b2632e56ee15f8d8dcdcd",
    },
    "THERMAL.PEOPLE_ACTIVITY_LEVEL": {
        "body_hash": "sha256:b33ef9739f6bd8533418c2d2c199e209601c5aa7111e178afb610494d4ea2696",
        "kind": "constant",
        "signature_hash": "sha256:6987d99c6d345cbd8d6ff4397ca43194b04fe907b89d9422c7972c5a0a501d74",
        "symbol_hash": "sha256:5a39d884ca1bdfa92fe0568bc4b11f8164ed3b50ed783378becec0c18147d946",
    },
    "Unit": {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "kind": "class",
        "signature_hash": "sha256:4207679fe2ede1a951b1882e62a22d8d915b1442dc5d1e1f62925d16cb6422e0",
        "symbol_hash": "sha256:82eeceb9e427512d5ed45c6139c5fb92859289547ded26e7e410b3be3f591b70",
    },
    "Unit.L2M3": {
        "body_hash": "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf",
        "kind": "constant",
        "signature_hash": "sha256:d4f677f2c249499bd341314182b551f8f784d9d00f8df315ddb9f1d3fec321e6",
        "symbol_hash": "sha256:91d7c58294dae00c815dbf158fb57990500db567405a7b2c31350eef60ea7102",
    },
    "Unit.MM2M": {
        "body_hash": "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf",
        "kind": "constant",
        "signature_hash": "sha256:6c5322fba5eeccac01411c863db5421b2ed98765a307fea5b69e2f6878f511ff",
        "symbol_hash": "sha256:4f90e5dec4746b485bf2d2b35f73b00ca8b742d8ca1babed858dc04fddc01e69",
    },
    "Unit.NONE2PRC": {
        "body_hash": "sha256:d3c3cec052dae85942a722526911012da69bf59aca87bc1229bfbc27211abdd1",
        "kind": "constant",
        "signature_hash": "sha256:c28ce3b1d369b3c8be93fbedc29a75951029b8485d3b7885f43e46eb817efdb1",
        "symbol_hash": "sha256:743aa08ade92de4311700e7e29b0bcfd084735a36520906bec9e74acd373c31a",
    },
    "Unit.PRC2NONE": {
        "body_hash": "sha256:d2dff8ba2e3305a55a5cfcb7f170272f46ce3773420fc2094c6eb318b178a722",
        "kind": "constant",
        "signature_hash": "sha256:de430edab58a6cacc63b7c0d76b68d49302e7dc3217bd6b45da3db4369a05219",
        "symbol_hash": "sha256:48e9d7619e573e8c55d44bbd640558260c077144a4b24fe384a91b7c433e6306",
    },
    "Unit.W2KW": {
        "body_hash": "sha256:2c90d8b6a6e407cf5919aa4be628204f8dbdeba19539303f86d2ba56ab41a6bf",
        "kind": "constant",
        "signature_hash": "sha256:f9130ac841cd7644647450db8a07fc69eaa4ace7594cd4f0ebb0ed6af610dbf8",
        "symbol_hash": "sha256:f00a14847f11df61238d82b56c9a31ecc8453877c7bda1eb12fbe13573f0f3eb",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "THERMAL": "native-thermal-default-constant-container",
    "Unit": "native-named-unit-conversion-constants",
}
EXPECTED_ASSERTION_IDS = {
    "THERMAL": "constants-engineering-thermal-c55d90e3",
    "THERMAL.PEOPLE_ACTIVITY_LEVEL": "constants-engineering-thermal-people-activity-5a39d884",
    "Unit": "constants-engineering-unit-82eeceb9",
    "Unit.L2M3": "constants-engineering-unit-l2m3-91d7c582",
    "Unit.MM2M": "constants-engineering-unit-mm2m-4f90e5de",
    "Unit.NONE2PRC": "constants-engineering-unit-none2prc-743aa08a",
    "Unit.PRC2NONE": "constants-engineering-unit-prc2none-48e9d761",
    "Unit.W2KW": "constants-engineering-unit-w2kw-f00a1484",
}
EXPECTED_CASE_IDS = (
    "constants-engineering.thermal.class.construction",
    "constants-engineering.thermal.class.member-topology",
    "constants-engineering.thermal.class.type-topology",
    "constants-engineering.thermal.people-activity-level.idf-default",
    "constants-engineering.thermal.people-activity-level.numeric-semantics",
    "constants-engineering.thermal.people-activity-level.value",
    "constants-engineering.unit.class.alias-topology",
    "constants-engineering.unit.class.member-order",
    "constants-engineering.unit.class.type-topology",
    "constants-engineering.unit.l2m3.engineering-probe",
    "constants-engineering.unit.l2m3.numeric-semantics",
    "constants-engineering.unit.l2m3.value",
    "constants-engineering.unit.mm2m.engineering-probe",
    "constants-engineering.unit.mm2m.numeric-semantics",
    "constants-engineering.unit.mm2m.value",
    "constants-engineering.unit.none2prc.engineering-probe",
    "constants-engineering.unit.none2prc.numeric-semantics",
    "constants-engineering.unit.none2prc.value",
    "constants-engineering.unit.prc2none.engineering-probe",
    "constants-engineering.unit.prc2none.numeric-semantics",
    "constants-engineering.unit.prc2none.value",
    "constants-engineering.unit.w2kw.engineering-probe",
    "constants-engineering.unit.w2kw.numeric-semantics",
    "constants-engineering.unit.w2kw.value",
)
EXPECTED_CASE_COUNT = 24
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

UNIT_CASES = {
    "Unit.L2M3": ("L2M3", 8.3),
    "Unit.MM2M": ("MM2M", 1250.0),
    "Unit.NONE2PRC": ("NONE2PRC", 0.375),
    "Unit.PRC2NONE": ("PRC2NONE", 37.5),
    "Unit.W2KW": ("W2KW", 4200.0),
}
EXPECTED_PROBE_RESULTS = {
    "Unit.L2M3": "1.0ff972474538fp-7",
    "Unit.MM2M": "1.4000000000000p+0",
    "Unit.NONE2PRC": "1.2c00000000000p+5",
    "Unit.PRC2NONE": "1.8000000000000p-2",
    "Unit.W2KW": "1.0cccccccccccdp+2",
}

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
BINARY64_PATTERN = re.compile(r"^-?[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+$")


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_constants_engineering_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load constants oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Constants oracle support is not pinned.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(SUPPORT, name) for name in names}
    try:
        SUPPORT.SOURCE_PATH = SOURCE_PATH
        SUPPORT.EXPECTED_SOURCE_SHA256 = EXPECTED_SOURCE_SHA256
        SUPPORT.EXPECTED_SYMBOL_HASHES = EXPECTED_SYMBOL_HASHES
        SUPPORT.TARGET_SYMBOLS = TARGET_SYMBOLS
        inventory = SUPPORT.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(SUPPORT, name, value)

    if inventory["file"] != {
        "ast_hash": EXPECTED_SOURCE_AST_SHA256,
        "content_hash": EXPECTED_SOURCE_SHA256,
        "path": SOURCE_PATH,
    }:
        raise SystemExit("The constants.py inventory file receipt is not exact.")
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]
    if inventory["symbols"] != expected_symbols:
        raise SystemExit("The constants.py symbol receipts are not exact.")
    return inventory


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    result: dict[str, Any] = {
        "executor": executor,
        "id": identifier,
        "symbol": symbol,
    }
    adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
    if adaptation is not None:
        result["expected_dotnet"] = {
            "adaptation": adaptation,
            "outcome": "returned",
        }
    return result


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions: list[dict[str, Any]] = []
    for identifier in EXPECTED_CASE_IDS:
        if ".thermal.class." in identifier:
            symbol, executor = "THERMAL", "thermal-class"
        elif ".thermal.people-activity-level." in identifier:
            symbol, executor = "THERMAL.PEOPLE_ACTIVITY_LEVEL", "thermal-constant"
        elif ".unit.class." in identifier:
            symbol, executor = "Unit", "unit-class"
        else:
            token = identifier.split(".")[2].upper()
            symbol, executor = f"Unit.{token}", "unit-constant"
        definitions.append(_case(identifier, executor, symbol))

    if tuple(item["id"] for item in definitions) != tuple(sorted(EXPECTED_CASE_IDS)):
        raise RuntimeError("Constants engineering case IDs are not sorted.")
    counts = Counter(item["symbol"] for item in definitions)
    if len(definitions) != EXPECTED_CASE_COUNT or counts != Counter(
        {symbol: 3 for symbol in TARGET_SYMBOLS}
    ):
        raise RuntimeError("Constants engineering cases are not exactly three per symbol.")
    return tuple(definitions)


def _number(value: int | float) -> dict[str, Any]:
    if type(value) is int:
        return {"decimal": str(value), "kind": "int"}
    if type(value) is float and math.isfinite(value):
        return {
            "binary64": value.hex().removeprefix("0x"),
            "kind": "float",
        }
    raise RuntimeError("Only finite exact int/float values may enter the oracle.")


def _construction_observation(enum_type: Any, value: Any, label: str) -> dict[str, Any]:
    try:
        member = enum_type(value)
    except Exception as error:  # The exact constructor failure is evidence.
        return {
            "error_category": "domain",
            "exception_type": type(error).__name__,
            "input": _number(value),
            "label": label,
            "outcome": "raised",
        }
    return {
        "input": _number(value),
        "label": label,
        "outcome": "returned",
        "result": {
            "name": member.name,
            "value": _number(member.value),
        },
    }


def _enum_type_facts(enum_type: Any) -> dict[str, Any]:
    return {
        "base_names": [base.__name__ for base in enum_type.__bases__],
        "class_name": enum_type.__name__,
        "is_enum_subclass": any(base.__name__ == "Enum" for base in enum_type.__mro__),
        "is_float_subclass": issubclass(enum_type, float),
        "module": enum_type.__module__,
        "signature": str(inspect.signature(enum_type)),
    }


def _execute_thermal_class(identifier: str, constants: Any, model: Any) -> dict[str, Any]:
    del model
    enum_type = constants.THERMAL
    if identifier.endswith(".construction"):
        return {
            "observations": [
                _construction_observation(enum_type, 107, "integer-member"),
                _construction_observation(enum_type, 107.0, "float-member"),
                _construction_observation(enum_type, 106, "unknown-value"),
            ]
        }
    if identifier.endswith(".member-topology"):
        return {
            "declared_member_names": list(enum_type.__members__),
            "iterated_member_names": [member.name for member in enum_type],
            "member_count": len(enum_type.__members__),
            "unique_member_count": len(list(enum_type)),
        }
    if identifier.endswith(".type-topology"):
        return _enum_type_facts(enum_type)
    raise RuntimeError(f"Unknown THERMAL class case: {identifier}")


def _execute_thermal_constant(
    identifier: str, constants: Any, model: Any
) -> dict[str, Any]:
    member = constants.THERMAL.PEOPLE_ACTIVITY_LEVEL
    if identifier.endswith(".idf-default"):
        idf = model.EnergyModel.create_default_idf()
        activity = idf["Schedule:Constant"][0]
        fields = list(activity.data.values())
        extended = list(getattr(activity, "_IdfObject__extended_input"))
        if len(fields) != 3 or extended:
            raise RuntimeError("The default activity schedule shape drifted.")
        schedule_type = fields[1]
        return {
            "activity_value": _number(fields[2]),
            "field_count": len(fields),
            "name": fields[0],
            "object_type": activity.idd.name,
            "schedule_type": {
                "name": schedule_type.name,
                "value": schedule_type.value,
            },
        }
    if identifier.endswith(".numeric-semantics"):
        return {
            "equals_107": member == 107,
            "float_projection": _number(float(member)),
            "is_float_instance": isinstance(member, float),
            "value_type": type(member.value).__name__,
        }
    if identifier.endswith(".value"):
        return {
            "canonical_name": member.name,
            "declared_name": "PEOPLE_ACTIVITY_LEVEL",
            "value": _number(member.value),
        }
    raise RuntimeError(f"Unknown THERMAL member case: {identifier}")


def _execute_unit_class(identifier: str, constants: Any, model: Any) -> dict[str, Any]:
    del model
    enum_type = constants.Unit
    if identifier.endswith(".alias-topology"):
        return {
            "alias_group": ["MM2M", "W2KW", "L2M3"],
            "canonical_names": {
                name: member.name for name, member in enum_type.__members__.items()
            },
            "l2m3_is_mm2m": enum_type.L2M3 is enum_type.MM2M,
            "mm2m_is_w2kw": enum_type.MM2M is enum_type.W2KW,
        }
    if identifier.endswith(".member-order"):
        return {
            "declared_member_names": list(enum_type.__members__),
            "iterated_member_names": [member.name for member in enum_type],
            "iterated_values": [_number(member.value) for member in enum_type],
            "member_count": len(enum_type.__members__),
            "unique_member_count": len(list(enum_type)),
        }
    if identifier.endswith(".type-topology"):
        return _enum_type_facts(enum_type)
    raise RuntimeError(f"Unknown Unit class case: {identifier}")


def _execute_unit_constant(
    identifier: str, constants: Any, model: Any
) -> dict[str, Any]:
    del model
    symbol = next(
        symbol
        for symbol in UNIT_CASES
        if f".{symbol.split('.')[1].lower()}." in identifier
    )
    declared_name, probe_input = UNIT_CASES[symbol]
    member = constants.Unit.__members__[declared_name]
    if identifier.endswith(".engineering-probe"):
        return {
            "input": _number(probe_input),
            "operation": "multiply",
            "result": _number(probe_input * member),
        }
    if identifier.endswith(".numeric-semantics"):
        return {
            "canonical_name": member.name,
            "declared_name": declared_name,
            "equals_value": member == member.value,
            "float_projection": _number(float(member)),
            "is_float_instance": isinstance(member, float),
            "is_same_as_canonical_member": member is constants.Unit[member.name],
        }
    if identifier.endswith(".value"):
        return {
            "canonical_name": member.name,
            "declared_name": declared_name,
            "value": _number(member.value),
        }
    raise RuntimeError(f"Unknown Unit member case: {identifier}")


EXECUTORS = {
    "thermal-class": _execute_thermal_class,
    "thermal-constant": _execute_thermal_constant,
    "unit-class": _execute_unit_class,
    "unit-constant": _execute_unit_constant,
}


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


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


def _find_pinned_source() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        candidate = Path(entry) / "idragon" / "constants.py"
        if candidate.is_file() and sha256_file(candidate) == EXPECTED_SOURCE_SHA256:
            matches.append(candidate.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon/constants.py must be importable.")
    return unique[0]


@contextmanager
def _pinned_modules(source: Path) -> Iterator[tuple[Any, Any]]:
    source = source.resolve()
    if sha256_file(source) != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The selected constants.py source is not pinned.")
    source_root = source.parents[1]
    saved_modules = {
        name: module
        for name, module in sys.modules.items()
        if name == "idragon" or name.startswith("idragon.")
    }
    for name in saved_modules:
        sys.modules.pop(name, None)
    sys.path.insert(0, str(source_root))
    try:
        constants = importlib.import_module("idragon.constants")
        model = importlib.import_module("idragon.dragon.model")
        if Path(constants.__file__).resolve() != source:
            raise SystemExit("Imported idragon.constants did not resolve to pinned source.")
        yield constants, model
    finally:
        for name in list(sys.modules):
            if name == "idragon" or name.startswith("idragon."):
                sys.modules.pop(name, None)
        sys.modules.update(saved_modules)
        try:
            sys.path.remove(str(source_root))
        except ValueError:
            pass


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source: Path | None = None,
) -> dict[str, Any]:
    imported_source = source.resolve() if source is not None else _find_pinned_source()
    if sha256_file(imported_source) != inventory["file"]["content_hash"]:
        raise SystemExit("The imported constants.py is not the inventoried source.")
    definitions = case_definitions()
    with _pinned_modules(imported_source) as (constants, model):
        if any(
            not hasattr(constants, symbol.split(".")[0])
            for symbol in TARGET_SYMBOLS
        ):
            raise SystemExit("The pinned constants engineering surface drifted.")
        cases = []
        for definition in definitions:
            case = dict(definition)
            case["python"] = {
                "facts": EXECUTORS[definition["executor"]](
                    definition["id"], constants, model
                ),
                "outcome": "returned",
            }
            cases.append(case)

    classifications = {
        symbol: (
            "exception"
            if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
            else "equivalent"
        )
        for symbol in TARGET_SYMBOLS
    }
    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": {
            "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
            "assertion_ids": EXPECTED_ASSERTION_IDS,
            "case_count": EXPECTED_CASE_COUNT,
            "case_ids": list(EXPECTED_CASE_IDS),
            "classifications": classifications,
            "float_encoding": "python-binary64-hex-without-0x-prefix",
            "runtime_names": "pinned-python-only-no-native-type-name-claims",
            "target_symbols": list(TARGET_SYMBOLS),
        },
        "runtime": {
            "dependencies": _dependencies(),
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
            "source_sha256": sha256_file(imported_source),
        },
    }
    validate_oracle(result)
    return result


def _require_keys(value: Any, expected: set[str], location: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(f"{location} key set drifted: {actual!r}")


def _case_by_id(value: dict[str, Any], identifier: str) -> dict[str, Any]:
    matches = [item for item in value["cases"] if item["id"] == identifier]
    if len(matches) != 1:
        raise RuntimeError(f"Expected exactly one case {identifier!r}.")
    return matches[0]


def _validate_number(value: Any, location: str) -> None:
    _require_keys(value, {"binary64", "kind"}, location)
    if value["kind"] != "float" or not isinstance(value["binary64"], str):
        raise RuntimeError(f"{location} is not a binary64 float.")
    if BINARY64_PATTERN.fullmatch(value["binary64"]) is None:
        raise RuntimeError(f"{location} binary64 token drifted.")


def _validate_semantics(value: dict[str, Any]) -> None:
    topology = _case_by_id(
        value, "constants-engineering.unit.class.alias-topology"
    )["python"]["facts"]
    if (
        topology["alias_group"] != ["MM2M", "W2KW", "L2M3"]
        or not topology["l2m3_is_mm2m"]
        or not topology["mm2m_is_w2kw"]
        or topology["canonical_names"]
        != {
            "L2M3": "MM2M",
            "MM2M": "MM2M",
            "NONE2PRC": "NONE2PRC",
            "PRC2NONE": "PRC2NONE",
            "W2KW": "MM2M",
        }
    ):
        raise RuntimeError("Unit alias topology drifted.")

    order = _case_by_id(
        value, "constants-engineering.unit.class.member-order"
    )["python"]["facts"]
    if order["declared_member_names"] != [
        "MM2M",
        "NONE2PRC",
        "PRC2NONE",
        "W2KW",
        "L2M3",
    ] or order["iterated_member_names"] != ["MM2M", "NONE2PRC", "PRC2NONE"]:
        raise RuntimeError("Unit member order drifted.")

    expected_values = {
        "Unit.L2M3": "1.0624dd2f1a9fcp-10",
        "Unit.MM2M": "1.0624dd2f1a9fcp-10",
        "Unit.NONE2PRC": "1.9000000000000p+6",
        "Unit.PRC2NONE": "1.47ae147ae147bp-7",
        "Unit.W2KW": "1.0624dd2f1a9fcp-10",
    }
    for symbol, expected in expected_values.items():
        token = symbol.split(".")[1].lower()
        facts = _case_by_id(
            value, f"constants-engineering.unit.{token}.value"
        )["python"]["facts"]
        _validate_number(facts["value"], f"{symbol}.value")
        if facts["value"]["binary64"] != expected:
            raise RuntimeError(f"{symbol} binary64 value drifted.")
        probe = _case_by_id(
            value, f"constants-engineering.unit.{token}.engineering-probe"
        )["python"]["facts"]
        _validate_number(probe["result"], f"{symbol}.probe")
        if probe["result"]["binary64"] != EXPECTED_PROBE_RESULTS[symbol]:
            raise RuntimeError(f"{symbol} engineering probe drifted.")

    activity = _case_by_id(
        value,
        "constants-engineering.thermal.people-activity-level.idf-default",
    )["python"]["facts"]
    _validate_number(activity["activity_value"], "activity default")
    if (
        activity["activity_value"]["binary64"] != "1.ac00000000000p+6"
        or activity["name"] != "$DEFAULT$PEOPLEACTIVITY"
        or activity["object_type"] != "Schedule:Constant"
        or activity["schedule_type"] != {"name": "REAL", "value": "real"}
    ):
        raise RuntimeError("People activity IDF default drifted.")

    for identifier in (
        "constants-engineering.thermal.class.type-topology",
        "constants-engineering.unit.class.type-topology",
    ):
        facts = _case_by_id(value, identifier)["python"]["facts"]
        if facts["base_names"] != ["float", "Enum"] or facts["signature"] != "(*values)":
            raise RuntimeError(f"Enum type topology drifted: {identifier}")


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if type(value) is float:
        raise RuntimeError(f"Raw float entered {location}.")
    if isinstance(value, Path):
        raise RuntimeError(f"Raw path entered {location}.")
    if isinstance(value, str):
        if RAW_ADDRESS_PATTERN.search(value):
            raise RuntimeError(f"A raw address entered {location}.")
        if ABSOLUTE_PATH_PATTERN.search(value):
            raise RuntimeError(f"An absolute path entered {location}.")
        if GUID_PATTERN.search(value):
            raise RuntimeError(f"A GUID-like token entered {location}.")
        if TIMESTAMP_PATTERN.search(value):
            raise RuntimeError(f"A timestamp entered {location}.")
        return
    if value is None or type(value) in (bool, int):
        return
    if isinstance(value, list):
        for index, item in enumerate(value):
            _validate_safe_tree(item, f"{location}[{index}]")
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise RuntimeError(f"A non-string key entered {location}.")
            _validate_safe_tree(item, f"{location}.{key}")
        return
    raise RuntimeError(f"Raw object {type(value).__name__} entered {location}.")


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
        raise RuntimeError("Constants engineering schema drifted.")
    definitions = case_definitions()
    if len(value["cases"]) != EXPECTED_CASE_COUNT or [
        item["id"] for item in value["cases"]
    ] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Constants engineering case order/count drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Constants engineering cases hash drifted.")
    by_id = {item["id"]: item for item in definitions}
    for case in value["cases"]:
        definition = by_id[case["id"]]
        expected_keys = set(definition) | {"python"}
        _require_keys(case, expected_keys, f"case {case.get('id')!r}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Case contract drifted: {case['id']}")
        if "expected_dotnet" in case:
            _require_keys(case["expected_dotnet"], {"adaptation", "outcome"}, "native")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned" or not case["python"]["facts"]:
            raise RuntimeError(f"Python case outcome drifted: {case['id']}")

    classifications = {
        symbol: (
            "exception"
            if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
            else "equivalent"
        )
        for symbol in TARGET_SYMBOLS
    }
    expected_contract = {
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "assertion_ids": EXPECTED_ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": classifications,
        "float_encoding": "python-binary64-hex-without-0x-prefix",
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
        "target_symbols": list(TARGET_SYMBOLS),
    }
    if value["consumer_contract"] != expected_contract:
        raise RuntimeError("Constants engineering consumer contract drifted.")
    if value["runtime"] != {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("Constants engineering runtime pin drifted.")
    if value["upstream"] != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("Constants engineering upstream receipt drifted.")
    if value["symbols"] != [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]:
        raise RuntimeError("Constants engineering symbol receipts drifted.")
    _validate_semantics(value)
    _validate_safe_tree(value)
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the constants oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
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
    print(f"Wrote constants engineering oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

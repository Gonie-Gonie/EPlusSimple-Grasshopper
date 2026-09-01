"""Generate the pinned ``idragon/dragon/model.py`` Terrain oracle.

Exactly three deterministic cases bind the Terrain enum class and each of its
five public members.  The corpus records the pinned Python string-enum surface,
including its qualified ``str``/IDF rendering, without leaking host state.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
import importlib
import importlib.metadata
import importlib.util
import inspect
import json
import os
from pathlib import Path
import re
import sys
from typing import Any, Iterator


SCHEMA = "dragons.python-reference.dragon-model-terrain.v1"
SOURCE_PATH = "src/idragon/dragon/model.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090"
)
EXPECTED_SOURCE_AST_SHA256 = (
    "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "Terrain": {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "kind": "class",
        "signature_hash": "sha256:1d1e2b681f443f98c601d67c7ad6574c3ab400169fba214018821be810b35a05",
        "symbol_hash": "sha256:c6163ac59051a6638838c9f9b2953585bf6825942dfa79b46af3be27279e5799",
    },
    "Terrain.CITY": {
        "body_hash": "sha256:1dd88966c75717b665c6649618e6003073b9f4c6c767171d6adc097e23263394",
        "kind": "constant",
        "signature_hash": "sha256:8111cd1050752ea024674b02b1502d1fdab240d04147d65f4c8ad71f148f0791",
        "symbol_hash": "sha256:86bbbeccfdcac8147f1ea09090065c8567a1a910715d4679b1059b02a27839bc",
    },
    "Terrain.COUNTRY": {
        "body_hash": "sha256:20ae46499cfabff7e35ca4cda49b33ccfd5258adad3ceed6ae7feb05eaae3772",
        "kind": "constant",
        "signature_hash": "sha256:cd58cf34472c886ee073d9c92cccd9a21ef585675a3aebbfac665ec8701fd93c",
        "symbol_hash": "sha256:b5cce6c9c3dbcbe551d86663ed5d7b4615451b5b9841f0fd6c8ddc6c6a5b5eae",
    },
    "Terrain.OCEAN": {
        "body_hash": "sha256:49dab2386f677c04c24d008110220ae1ef2e02d84ce9a54de25a4c05e6e683d8",
        "kind": "constant",
        "signature_hash": "sha256:43f22f5af8b01a0e2ac6f0d4c47016cc200961a8b80b0228c0f7768076df9086",
        "symbol_hash": "sha256:4fb458afdad96d03018c848e08a853065cf2ff1f71d110175a13e18481c6b20a",
    },
    "Terrain.SUBURBS": {
        "body_hash": "sha256:6bece3a025b22ae5b104d63e066146295e565cfae57cf5fcc92e827ec2644291",
        "kind": "constant",
        "signature_hash": "sha256:201c53eabe683bbe1abea3efd17c21f4b74c585b63fd2d76ca2bb44878f99587",
        "symbol_hash": "sha256:3de90284fe1a6b5e8b582cd04c07cd01da2a3fc6d097bce30b2c3d23144167e6",
    },
    "Terrain.URBAN": {
        "body_hash": "sha256:84445019fc9c0fbb69f98f9b193728c3227aeabb9f1b19ca165d80f1e0250b30",
        "kind": "constant",
        "signature_hash": "sha256:69ca03abbb5e119dbba6122c1e9a4c0eb82beaeeae2abca2fd9c8ea80949c011",
        "symbol_hash": "sha256:a4c4bc7a7a67f1165956614348dde48e687d79001443487b879f5abf1cbf5a62",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "Terrain": "native-typed-terrain-enum-valid-idf-token",
}
EXPECTED_ASSERTION_IDS = {
    "Terrain": "dragon-model-terrain-c6163ac5",
    "Terrain.CITY": "dragon-model-terrain-city-86bbbecc",
    "Terrain.COUNTRY": "dragon-model-terrain-country-b5cce6c9",
    "Terrain.OCEAN": "dragon-model-terrain-ocean-4fb458af",
    "Terrain.SUBURBS": "dragon-model-terrain-suburbs-3de90284",
    "Terrain.URBAN": "dragon-model-terrain-urban-a4c4bc7",
}
EXPECTED_CASE_IDS = (
    "dragon-model-terrain.enum.construction",
    "dragon-model-terrain.enum.member-topology",
    "dragon-model-terrain.enum.text-projection",
    "dragon-model-terrain.member.city.engineering-token",
    "dragon-model-terrain.member.city.roundtrip",
    "dragon-model-terrain.member.city.value",
    "dragon-model-terrain.member.country.engineering-token",
    "dragon-model-terrain.member.country.roundtrip",
    "dragon-model-terrain.member.country.value",
    "dragon-model-terrain.member.ocean.engineering-token",
    "dragon-model-terrain.member.ocean.roundtrip",
    "dragon-model-terrain.member.ocean.value",
    "dragon-model-terrain.member.suburbs.engineering-token",
    "dragon-model-terrain.member.suburbs.roundtrip",
    "dragon-model-terrain.member.suburbs.value",
    "dragon-model-terrain.member.urban.engineering-token",
    "dragon-model-terrain.member.urban.roundtrip",
    "dragon-model-terrain.member.urban.value",
)
EXPECTED_CASE_COUNT = 18
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
EXPECTED_MEMBER_ORDER = ("COUNTRY", "SUBURBS", "CITY", "OCEAN", "URBAN")
EXPECTED_MEMBER_VALUES = {
    "CITY": "City",
    "COUNTRY": "Country",
    "OCEAN": "Ocean",
    "SUBURBS": "Suburbs",
    "URBAN": "Urban",
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


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_schedule_type_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_dragon_model_terrain_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load Terrain oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Terrain oracle support is not pinned.")
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
        raise SystemExit("The dragon/model.py inventory file receipt is not exact.")
    expected_symbols = [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]
    if inventory["symbols"] != expected_symbols:
        raise SystemExit("The Terrain symbol receipts are not exact.")
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
        if ".enum." in identifier:
            symbol, executor = "Terrain", "terrain-class"
        else:
            member_name = identifier.split(".")[2].upper()
            symbol, executor = f"Terrain.{member_name}", "terrain-member"
        definitions.append(_case(identifier, executor, symbol))

    if tuple(item["id"] for item in definitions) != tuple(sorted(EXPECTED_CASE_IDS)):
        raise RuntimeError("Terrain case IDs are not sorted.")
    counts = Counter(item["symbol"] for item in definitions)
    if len(definitions) != EXPECTED_CASE_COUNT or counts != Counter(
        {symbol: 3 for symbol in TARGET_SYMBOLS}
    ):
        raise RuntimeError("Terrain cases are not exactly three per symbol.")
    return tuple(definitions)


def _tag_input(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if type(value) is int:
        return {"decimal": str(value), "kind": "int"}
    if type(value) is str:
        return {"kind": "string", "value": value}
    raise RuntimeError(f"Unsupported Terrain constructor probe: {type(value).__name__}")


def _construction_observation(enum_type: Any, value: Any) -> dict[str, Any]:
    try:
        member = enum_type(value)
    except Exception as error:  # The exact constructor failure is evidence.
        return {
            "error_category": "domain",
            "exception_type": type(error).__name__,
            "input": _tag_input(value),
            "outcome": "raised",
        }
    return {
        "input": _tag_input(value),
        "outcome": "returned",
        "result": {
            "name": member.name,
            "value": member.value,
        },
        "same_member": member is enum_type.__members__[member.name],
    }


def _building_object(model: Any, member: Any) -> Any:
    idf = model.EnergyModel(
        "Terrain oracle", terrain=member, zone=[], pv=[]
    ).to_idf()
    return idf["Building"][0]


def _rendered_terrain_token(building: Any) -> str:
    matches = [
        line for line in str(building).splitlines() if line.rstrip().endswith("!- Terrain")
    ]
    if len(matches) != 1:
        raise RuntimeError("The rendered Building Terrain field shape drifted.")
    return matches[0].split(",", 1)[0].strip()


def _execute_terrain_class(
    identifier: str, model: Any, symbol: str
) -> dict[str, Any]:
    if symbol != "Terrain":
        raise RuntimeError("The Terrain class executor received a member symbol.")
    enum_type = model.Terrain
    if identifier.endswith(".construction"):
        valid = [
            _construction_observation(enum_type, enum_type.__members__[name].value)
            for name in EXPECTED_MEMBER_ORDER
        ]
        passthrough = {
            name: enum_type(enum_type.__members__[name])
            is enum_type.__members__[name]
            for name in EXPECTED_MEMBER_ORDER
        }
        invalid = [
            _construction_observation(enum_type, value)
            for value in ("country", "Rural", "", 0, None)
        ]
        return {
            "invalid_observations": invalid,
            "member_passthrough_identity": passthrough,
            "valid_observations": valid,
        }
    if identifier.endswith(".member-topology"):
        declared = list(enum_type.__members__.values())
        iterated = list(enum_type)
        return {
            "declared_member_names": list(enum_type.__members__),
            "declared_member_values": [member.value for member in declared],
            "has_aliases": len(declared) != len(iterated),
            "iterated_member_names": [member.name for member in iterated],
            "iterated_member_values": [member.value for member in iterated],
            "member_count": len(declared),
            "unique_member_count": len(iterated),
        }
    if identifier.endswith(".text-projection"):
        members = list(enum_type)
        return {
            "base_names": [base.__name__ for base in enum_type.__bases__],
            "class_name": enum_type.__name__,
            "is_enum_subclass": any(base.__name__ == "Enum" for base in enum_type.__mro__),
            "is_str_subclass": issubclass(enum_type, str),
            "json_tokens": {
                member.name: json.loads(json.dumps(member)) for member in members
            },
            "module": enum_type.__module__,
            "rendered_building_tokens": {
                member.name: _rendered_terrain_token(_building_object(model, member))
                for member in members
            },
            "signature": str(inspect.signature(enum_type)),
            "str_tokens": {member.name: str(member) for member in members},
        }
    raise RuntimeError(f"Unknown Terrain class case: {identifier}")


def _execute_terrain_member(
    identifier: str, model: Any, symbol: str
) -> dict[str, Any]:
    declared_name = symbol.split(".", 1)[1]
    member = model.Terrain.__members__[declared_name]
    if identifier.endswith(".engineering-token"):
        building = _building_object(model, member)
        field = building["Terrain"]
        constructed = model.EnergyModel(
            "Terrain retention", terrain=member, zone=[], pv=[]
        )
        return {
            "building_field_equals_value": field == member.value,
            "building_field_is_member": field is member,
            "building_field_value": field.value,
            "energyplus_choice_token": member.value,
            "model_retains_member": constructed.terrain is member,
        }
    if identifier.endswith(".roundtrip"):
        return {
            "construct_from_member_is_member": model.Terrain(member) is member,
            "construct_from_value_is_member": model.Terrain(member.value) is member,
            "hash_equals_value_hash": hash(member) == hash(member.value),
            "json_value": json.loads(json.dumps(member)),
            "lookup_by_name_is_member": model.Terrain[declared_name] is member,
        }
    if identifier.endswith(".value"):
        return {
            "canonical_name": member.name,
            "declared_name": declared_name,
            "equals_value": member == member.value,
            "is_str_instance": isinstance(member, str),
            "value": member.value,
            "value_type": type(member.value).__name__,
        }
    raise RuntimeError(f"Unknown Terrain member case: {identifier}")


EXECUTORS = {
    "terrain-class": _execute_terrain_class,
    "terrain-member": _execute_terrain_member,
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
        candidate = Path(entry) / "idragon" / "dragon" / "model.py"
        if candidate.is_file() and sha256_file(candidate) == EXPECTED_SOURCE_SHA256:
            matches.append(candidate.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon/dragon/model.py must be importable.")
    return unique[0]


@contextmanager
def _pinned_model(source: Path) -> Iterator[Any]:
    source = source.resolve()
    if sha256_file(source) != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The selected dragon/model.py source is not pinned.")
    source_root = source.parents[2]
    saved_modules = {
        name: module
        for name, module in sys.modules.items()
        if name == "idragon" or name.startswith("idragon.")
    }
    for name in saved_modules:
        sys.modules.pop(name, None)
    sys.path.insert(0, str(source_root))
    try:
        model = importlib.import_module("idragon.dragon.model")
        if Path(model.__file__).resolve() != source:
            raise SystemExit("Imported idragon.dragon.model did not resolve to pinned source.")
        yield model
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
        raise SystemExit("The imported dragon/model.py is not the inventoried source.")
    definitions = case_definitions()
    with _pinned_model(imported_source) as model:
        if not hasattr(model, "Terrain") or tuple(model.Terrain.__members__) != EXPECTED_MEMBER_ORDER:
            raise SystemExit("The pinned Terrain surface drifted.")
        cases: list[dict[str, Any]] = []
        for definition in definitions:
            case = dict(definition)
            case["python"] = {
                "facts": EXECUTORS[definition["executor"]](
                    definition["id"], model, definition["symbol"]
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


def _validate_semantics(value: dict[str, Any]) -> None:
    topology = _case_by_id(
        value, "dragon-model-terrain.enum.member-topology"
    )["python"]["facts"]
    expected_order = list(EXPECTED_MEMBER_ORDER)
    expected_order_values = [EXPECTED_MEMBER_VALUES[name] for name in expected_order]
    if topology != {
        "declared_member_names": expected_order,
        "declared_member_values": expected_order_values,
        "has_aliases": False,
        "iterated_member_names": expected_order,
        "iterated_member_values": expected_order_values,
        "member_count": 5,
        "unique_member_count": 5,
    }:
        raise RuntimeError("Terrain member topology drifted.")

    construction = _case_by_id(
        value, "dragon-model-terrain.enum.construction"
    )["python"]["facts"]
    expected_valid = [
        {
            "input": {"kind": "string", "value": member_value},
            "outcome": "returned",
            "result": {"name": member_name, "value": member_value},
            "same_member": True,
        }
        for member_name, member_value in zip(
            expected_order, expected_order_values, strict=True
        )
    ]
    expected_invalid = [
        {
            "error_category": "domain",
            "exception_type": "ValueError",
            "input": {"kind": "string", "value": input_value},
            "outcome": "raised",
        }
        for input_value in ("country", "Rural", "")
    ]
    expected_invalid.extend(
        [
            {
                "error_category": "domain",
                "exception_type": "ValueError",
                "input": {"decimal": "0", "kind": "int"},
                "outcome": "raised",
            },
            {
                "error_category": "domain",
                "exception_type": "ValueError",
                "input": {"kind": "none"},
                "outcome": "raised",
            },
        ]
    )
    if construction != {
        "invalid_observations": expected_invalid,
        "member_passthrough_identity": {name: True for name in expected_order},
        "valid_observations": expected_valid,
    }:
        raise RuntimeError("Terrain construction semantics drifted.")

    text = _case_by_id(value, "dragon-model-terrain.enum.text-projection")[
        "python"
    ]["facts"]
    expected_qualified = {name: f"Terrain.{name}" for name in expected_order}
    if text != {
        "base_names": ["str", "Enum"],
        "class_name": "Terrain",
        "is_enum_subclass": True,
        "is_str_subclass": True,
        "json_tokens": {
            name: EXPECTED_MEMBER_VALUES[name] for name in expected_order
        },
        "module": "idragon.dragon.model",
        "rendered_building_tokens": expected_qualified,
        "signature": "(*values)",
        "str_tokens": expected_qualified,
    }:
        raise RuntimeError("Terrain text projection drifted.")

    for symbol in TARGET_SYMBOLS[1:]:
        name = symbol.split(".", 1)[1]
        slug = name.lower()
        expected_value = EXPECTED_MEMBER_VALUES[name]
        engineering = _case_by_id(
            value, f"dragon-model-terrain.member.{slug}.engineering-token"
        )["python"]["facts"]
        if engineering != {
            "building_field_equals_value": True,
            "building_field_is_member": True,
            "building_field_value": expected_value,
            "energyplus_choice_token": expected_value,
            "model_retains_member": True,
        }:
            raise RuntimeError(f"{symbol} engineering token drifted.")
        roundtrip = _case_by_id(
            value, f"dragon-model-terrain.member.{slug}.roundtrip"
        )["python"]["facts"]
        if roundtrip != {
            "construct_from_member_is_member": True,
            "construct_from_value_is_member": True,
            "hash_equals_value_hash": True,
            "json_value": expected_value,
            "lookup_by_name_is_member": True,
        }:
            raise RuntimeError(f"{symbol} roundtrip drifted.")
        member_value = _case_by_id(
            value, f"dragon-model-terrain.member.{slug}.value"
        )["python"]["facts"]
        if member_value != {
            "canonical_name": name,
            "declared_name": name,
            "equals_value": True,
            "is_str_instance": True,
            "value": expected_value,
            "value_type": "str",
        }:
            raise RuntimeError(f"{symbol} value semantics drifted.")


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
        raise RuntimeError("Terrain schema drifted.")
    definitions = case_definitions()
    if len(value["cases"]) != EXPECTED_CASE_COUNT or [
        item["id"] for item in value["cases"]
    ] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Terrain case order/count drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Terrain cases hash drifted.")
    by_id = {item["id"]: item for item in definitions}
    for case in value["cases"]:
        definition = by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case.get('id')!r}")
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
        "runtime_names": "pinned-python-only-no-native-type-name-claims",
        "target_symbols": list(TARGET_SYMBOLS),
    }
    if value["consumer_contract"] != expected_contract:
        raise RuntimeError("Terrain consumer contract drifted.")
    if value["runtime"] != {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("Terrain runtime pin drifted.")
    if value["upstream"] != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("Terrain upstream receipt drifted.")
    if value["symbols"] != [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]:
        raise RuntimeError("Terrain symbol receipts drifted.")
    _validate_safe_tree(value)
    _validate_semantics(value)
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the Terrain oracle.")
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
    print(f"Wrote dragon model Terrain oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

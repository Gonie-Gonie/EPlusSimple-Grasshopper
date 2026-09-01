"""Generate pinned observations for ``idragon/common.py``.

The corpus contains exactly three cases for each of the thirteen unresolved
public symbols in the pinned source.  ``Version.__repr__`` and
``Version.__str__`` are intentionally absent because the compatibility scope
classifies those human/debug representations as out of scope.

Run this generator only through ``bootstrap_reference.py``.  It directly loads
the byte-pinned ``common.py`` rather than importing the ``idragon`` package and
therefore cannot silently execute a different installed package surface.
"""

from __future__ import annotations

import argparse
import calendar
from collections import Counter
import importlib.util
import os
from pathlib import Path
import re
import sys
from typing import Any, Callable


SCHEMA = "dragons.invisibledragon.common-core-oracle.v1"
SOURCE_PATH = "src/idragon/common.py"
IMPORT_RELATIVE_PATH = Path("idragon") / "common.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "Setting": {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "kind": "class",
        "signature_hash": "sha256:ee5384599d7bf86f25c4c9be3c78b9ca50772d5770a0cdd4e4ba6df05ca13228",
        "symbol_hash": "sha256:6e21a2020f51e224497609cab212d06906e185320247c6604a763f2498b8a965",
    },
    "Setting.DEFAULT_EP_VERSION": {
        "body_hash": "sha256:7ae62845ed4693ec74ed0a0816732e1e6b73208c56e2a77a7618fc521026fde2",
        "kind": "constant",
        "signature_hash": "sha256:23bdff34828c36f054d2cbb1d25fba1a6c760b3caafa55eaf7e79275d9bdc112",
        "symbol_hash": "sha256:f61d5ffdf018890e5d6e521ee25af49c93de60ce1d8a10c00e41b8d484d64ba6",
    },
    "Setting.DEFAULT_YEAR": {
        "body_hash": "sha256:0d1272fa2e01e32086f61a1b3ba1e1f0fa830eac73d098d45c29c22d4c5e6b36",
        "kind": "constant",
        "signature_hash": "sha256:1b8a61e6ffe40d8e48d9355f31cc15c065d4667bab652fc07b81b08e40d7e92c",
        "symbol_hash": "sha256:06415c37d66501858c44650d009662b50212cac62baf713ca9e75276e737eb14",
    },
    "Version": {
        "body_hash": "sha256:fb7b04e087cf5ee44ca605240380ca8847066ea9c7c879315419dc0b52446c3c",
        "kind": "class",
        "signature_hash": "sha256:127a8b300808358bf3f1a153c025fb3d53ef73e7fd1ba8cc098576acb458a6ed",
        "symbol_hash": "sha256:1c497416f9054aec72cc23eb32f3740e6001e70183471e0453128ec74d7770c8",
    },
    "Version.__format__": {
        "body_hash": "sha256:c839272a4d8790a62fefc1020c9eb590c9f978c6fe48f967062d5b936c3771b9",
        "kind": "function",
        "signature_hash": "sha256:898cc4fc44ddc0f34fa112615fb7b40d48b275e241775ced67b86d2912549d7e",
        "symbol_hash": "sha256:da210c4fe8b52304df65a5ebcd0ac74511eed62730dd724b3c6f8ce3fbabc528",
    },
    "Version.__init__": {
        "body_hash": "sha256:fca44c5193da96a1ce893264f7969f6edb34bc2f579bc0447f87386e417adbce",
        "kind": "function",
        "signature_hash": "sha256:03d7516c1730f6f95147d7ebd855ace566e32c4f896eab3ff830b5ba6e716413",
        "symbol_hash": "sha256:a3def1029c1ebaf97d2c94d1efdc88f0c302c44e0c93d2045c38be0b12a0e983",
    },
    "Version.__iter__": {
        "body_hash": "sha256:08cbcae78468818d6528e4acca4edbebc02f602d9f5c88ce4a48a0708b48dc9c",
        "kind": "function",
        "signature_hash": "sha256:d9c32b0d50573f40cb5a4661cd4e1a7d0fed48c9126e12d1232aa81d4986ab85",
        "symbol_hash": "sha256:6d3a4baddd16fa313692dee29016da7b507b724a99f3e96f90b3def0b20c84e0",
    },
    "Version.ep_dirname": {
        "body_hash": "sha256:29ae518a6fbdb45dc66b0b7da90a3440e5b467f8a9d548e50c16841dbc0d2d1b",
        "kind": "function",
        "signature_hash": "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb",
        "symbol_hash": "sha256:4b01fd15706bc10675d11074bffb225f0ff0cf52d42c9367e9f815e420c43f38",
    },
    "Version.iddname": {
        "body_hash": "sha256:ca48532a9eba41657918bcc72e8f326620e023a4b16df154772eee584ef1c280",
        "kind": "function",
        "signature_hash": "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb",
        "symbol_hash": "sha256:35a0ff29689c5bec73734a0541aed807b56f3f7d452f9803d3dc48cdfa2987cf",
    },
    "Version.major": {
        "body_hash": "sha256:25aebf43a7db451d8989bb906db40d99b7f30f80903bd26fad7fcd9ca367012c",
        "kind": "function",
        "signature_hash": "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876",
        "symbol_hash": "sha256:eb78e2b16110644dbb1186f0957d03be39f3b277422c81019a9da6b15d4e8723",
    },
    "Version.minor": {
        "body_hash": "sha256:dcc7fe6ca11597a4e305f98448113b96de26e37cc25544a5092372ed8932ef3c",
        "kind": "function",
        "signature_hash": "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876",
        "symbol_hash": "sha256:2574c06325619eff67f689a237849ff548f990cc4f121556aa1b8a563d9828c0",
    },
    "Version.patch": {
        "body_hash": "sha256:52d92682a931ea9189cec4de0714158104ba614d5ae6fed24a1e0a47779fb9be",
        "kind": "function",
        "signature_hash": "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876",
        "symbol_hash": "sha256:e799dbd50398b1bce90539df69a7c61165ec72ea1933bba3cf17bbdea580b8de",
    },
    "Version.to_version_anyway": {
        "body_hash": "sha256:126857416d367ce852290756b157a9a33e5a191247d1ec56e9752711b7bbaec5",
        "kind": "function",
        "signature_hash": "sha256:692fddbade2d31fda71ec2d931a2797265fd87743d9f23211f29d3d7851c9dc1",
        "symbol_hash": "sha256:d59930546366ae649f0b4c1b7f0c3e38b46194099dc987873b519e47883fcc61",
    },
}
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_EQUIVALENT_SYMBOLS = frozenset(
    {
        "Setting",
        "Setting.DEFAULT_EP_VERSION",
        "Setting.DEFAULT_YEAR",
        "Version.__format__",
        "Version.__iter__",
        "Version.ep_dirname",
        "Version.iddname",
        "Version.major",
        "Version.minor",
        "Version.patch",
    }
)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "Version": "native-energyplus-version-descriptor",
    "Version.__init__": "validated-energyplus-version-construction",
    "Version.to_version_anyway": "strongly-typed-energyplus-version-coercion",
}
EXPECTED_CASE_COUNT = 39
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64

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
UPSTREAM_KEYS = {"commit", "inventory_sha256", "path", "source_sha256"}
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
VERSION_SNAPSHOT_KEYS = {
    "component_types",
    "components",
    "ep_dirname",
    "format_dash",
    "format_dot",
    "iddname",
    "major",
    "minor",
    "patch",
}
CASE_FACT_KEYS = {
    "setting-default-ep-version.components": {
        "component_count",
        "components",
    },
    "setting-default-ep-version.formatted-identities": {
        "dotted",
        "ep_dirname",
        "hyphenated",
        "iddname",
    },
    "setting-default-ep-version.semantic-shape": {
        "all_nonnegative",
        "component_count",
        "patch_is_zero",
    },
    "setting-default-year.calendar": {
        "day_count",
        "is_leap",
        "year",
    },
    "setting-default-year.run-period": {
        "end",
        "start",
    },
    "setting-default-year.scalar": {
        "next_year",
        "previous_year",
        "text",
        "value",
    },
    "setting.baseline-values": {"default_ep_version", "default_year"},
    "setting.default-version-roundtrip": {"version"},
    "setting.engineering-shape": {
        "component_count",
        "patch_default_is_zero",
        "year_day_count",
        "year_is_non_leap",
    },
    "version-class.descriptor": {
        "defines_equality",
        "has_instance_dictionary",
        "public_descriptors",
        "type_name",
    },
    "version-class.identity-equality": {
        "components_equal",
        "separate_instances_equal",
        "self_equal",
    },
    "version-class.readonly-properties": {"observations"},
    "version-coerce.existing-identity": {"same_identity", "version"},
    "version-coerce.failure-surface": {"observations"},
    "version-coerce.strings-and-sequences": {"observations"},
    "version-ep-dirname.default": {"components", "value"},
    "version-ep-dirname.legacy": {"components", "value"},
    "version-ep-dirname.zero-and-large": {"observations"},
    "version-format.default-direct": {"direct_default", "explicit_dash"},
    "version-format.delimiters": {"dash", "dot", "double_colon", "slash"},
    "version-format.empty-spec": {
        "builtin_format",
        "direct_empty",
        "fstring_empty",
    },
    "version-iddname.default": {"components", "value"},
    "version-iddname.legacy": {"components", "value"},
    "version-iddname.zero-and-large": {"observations"},
    "version-init.failure-surface": {"observations"},
    "version-init.integer-overloads": {"observations"},
    "version-init.string-tokenization": {"observations"},
    "version-iter.conversions": {"list", "tuple"},
    "version-iter.fresh-generators": {
        "first_is_second",
        "first_type",
        "first_values",
        "second_values",
    },
    "version-iter.ordered-exhaustion": {"exhausted", "values"},
    "version-major.default-baseline": {"components", "value"},
    "version-major.explicit-three": {"components", "value"},
    "version-major.two-component-default": {"components", "value"},
    "version-minor.default-baseline": {"components", "value"},
    "version-minor.explicit-three": {"components", "value"},
    "version-minor.two-component-default": {"components", "value"},
    "version-patch.default-baseline": {"components", "value"},
    "version-patch.explicit-three": {"components", "value"},
    "version-patch.two-component-default": {"components", "value"},
}
RAW_ADDRESS_PATTERN = re.compile(
    r"(?<![0-9A-Za-z])0[xX][0-9A-Fa-f]{7,16}(?![0-9A-Za-z])"
)
BINARY64_PATTERN = re.compile(
    r"^-?(?:[0-9a-f]+\.[0-9a-f]+p[+-][0-9]+|inf|nan)$"
)


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_day_schedule_core_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_common_core_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load common core oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Common core oracle support is not pinned.")
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


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    """Run the hardened full-inventory validator for this exact source slice."""

    support = BASE.BASE
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(support, name) for name in names}
    try:
        support.SOURCE_PATH = SOURCE_PATH
        support.EXPECTED_SOURCE_SHA256 = EXPECTED_SOURCE_SHA256
        support.EXPECTED_SYMBOL_HASHES = EXPECTED_SYMBOL_HASHES
        support.TARGET_SYMBOLS = TARGET_SYMBOLS
        inventory = support.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(support, name, value)

    if [item["symbol"] for item in inventory["symbols"]] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover thirteen common symbols.")
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


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
    return {
        "executor": executor,
        "expected_dotnet": None
        if adaptation is None
        else {"adaptation": adaptation, "outcome": "returned"},
        "id": identifier,
        "symbol": symbol,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    definitions = (
        _case("setting.baseline-values", "setting", "Setting"),
        _case("setting.default-version-roundtrip", "setting", "Setting"),
        _case("setting.engineering-shape", "setting", "Setting"),
        _case(
            "setting-default-ep-version.components",
            "setting-default-ep-version",
            "Setting.DEFAULT_EP_VERSION",
        ),
        _case(
            "setting-default-ep-version.formatted-identities",
            "setting-default-ep-version",
            "Setting.DEFAULT_EP_VERSION",
        ),
        _case(
            "setting-default-ep-version.semantic-shape",
            "setting-default-ep-version",
            "Setting.DEFAULT_EP_VERSION",
        ),
        _case(
            "setting-default-year.calendar",
            "setting-default-year",
            "Setting.DEFAULT_YEAR",
        ),
        _case(
            "setting-default-year.run-period",
            "setting-default-year",
            "Setting.DEFAULT_YEAR",
        ),
        _case(
            "setting-default-year.scalar",
            "setting-default-year",
            "Setting.DEFAULT_YEAR",
        ),
        _case("version-class.descriptor", "version-class", "Version"),
        _case("version-class.identity-equality", "version-class", "Version"),
        _case("version-class.readonly-properties", "version-class", "Version"),
        _case(
            "version-format.default-direct",
            "version-format",
            "Version.__format__",
        ),
        _case(
            "version-format.delimiters", "version-format", "Version.__format__"
        ),
        _case(
            "version-format.empty-spec", "version-format", "Version.__format__"
        ),
        _case(
            "version-init.failure-surface", "version-init", "Version.__init__"
        ),
        _case(
            "version-init.integer-overloads", "version-init", "Version.__init__"
        ),
        _case(
            "version-init.string-tokenization", "version-init", "Version.__init__"
        ),
        _case("version-iter.conversions", "version-iter", "Version.__iter__"),
        _case("version-iter.fresh-generators", "version-iter", "Version.__iter__"),
        _case(
            "version-iter.ordered-exhaustion", "version-iter", "Version.__iter__"
        ),
        _case(
            "version-ep-dirname.default", "version-ep-dirname", "Version.ep_dirname"
        ),
        _case(
            "version-ep-dirname.legacy", "version-ep-dirname", "Version.ep_dirname"
        ),
        _case(
            "version-ep-dirname.zero-and-large",
            "version-ep-dirname",
            "Version.ep_dirname",
        ),
        _case("version-iddname.default", "version-iddname", "Version.iddname"),
        _case("version-iddname.legacy", "version-iddname", "Version.iddname"),
        _case(
            "version-iddname.zero-and-large", "version-iddname", "Version.iddname"
        ),
        _case("version-major.default-baseline", "version-major", "Version.major"),
        _case("version-major.explicit-three", "version-major", "Version.major"),
        _case(
            "version-major.two-component-default", "version-major", "Version.major"
        ),
        _case("version-minor.default-baseline", "version-minor", "Version.minor"),
        _case("version-minor.explicit-three", "version-minor", "Version.minor"),
        _case(
            "version-minor.two-component-default", "version-minor", "Version.minor"
        ),
        _case("version-patch.default-baseline", "version-patch", "Version.patch"),
        _case("version-patch.explicit-three", "version-patch", "Version.patch"),
        _case(
            "version-patch.two-component-default", "version-patch", "Version.patch"
        ),
        _case(
            "version-coerce.existing-identity",
            "version-coerce",
            "Version.to_version_anyway",
        ),
        _case(
            "version-coerce.failure-surface",
            "version-coerce",
            "Version.to_version_anyway",
        ),
        _case(
            "version-coerce.strings-and-sequences",
            "version-coerce",
            "Version.to_version_anyway",
        ),
    )
    ordered = tuple(sorted(definitions, key=lambda item: item["id"]))
    validate_case_definitions(ordered)
    return ordered


def validate_case_definitions(definitions: tuple[dict[str, Any], ...]) -> None:
    identifiers = [item.get("id") for item in definitions]
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} common cases, got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(
        set(identifiers)
    ):
        raise RuntimeError("Common case identifiers are not unique and sorted.")
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
            raise RuntimeError("A common case has an invalid identifier.")
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
        elif expectation != {"adaptation": adaptation, "outcome": "returned"}:
            raise RuntimeError(f"Case {identifier!r} has a stale native expectation.")
    if counts != Counter({symbol: 3 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("Common core does not contain three cases per symbol.")


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _exception_category(exception: Exception) -> str:
    if isinstance(exception, ValueError):
        return "domain"
    if isinstance(exception, OverflowError):
        return "range"
    if isinstance(exception, (AttributeError, TypeError)):
        return "type"
    raise RuntimeError(f"Unclassified common exception {type(exception).__name__}.")


def _observe(
    call: Callable[[], Any],
    projector: Callable[[Any], Any] | None = None,
) -> dict[str, Any]:
    try:
        value = call()
        return {
            "outcome": "returned",
            "result": normalize(value if projector is None else projector(value)),
        }
    except Exception as exception:  # Exact pinned behavior is oracle data.
        return {
            "error_category": _exception_category(exception),
            "exception_type": type(exception).__name__,
            "message": str(exception),
            "outcome": "raised",
        }


def _returned(facts: dict[str, Any]) -> dict[str, Any]:
    return {"facts": normalize(facts), "outcome": "returned"}


def _version_snapshot(value: Any, Version: type) -> dict[str, Any]:
    if not isinstance(value, Version):
        raise RuntimeError("A common version snapshot received the wrong type.")
    components = list(value)
    result = {
        "component_types": [type(item).__name__ for item in components],
        "components": components,
        "ep_dirname": value.ep_dirname,
        "format_dash": format(value, "-"),
        "format_dot": format(value, "."),
        "iddname": value.iddname,
        "major": value.major,
        "minor": value.minor,
        "patch": value.patch,
    }
    if set(result) != VERSION_SNAPSHOT_KEYS:
        raise RuntimeError("A common version snapshot has an invalid key set.")
    return result


def _property_facts(value: Any, property_name: str) -> dict[str, Any]:
    return {"components": list(value), "value": getattr(value, property_name)}


def _execute_setting(identifier: str, Setting: type, Version: type) -> dict[str, Any]:
    components = list(Setting.DEFAULT_EP_VERSION)
    year = Setting.DEFAULT_YEAR
    if identifier == "setting.baseline-values":
        return _returned(
            {"default_ep_version": components, "default_year": year}
        )
    if identifier == "setting.default-version-roundtrip":
        return _returned(
            {
                "version": _version_snapshot(
                    Version.to_version_anyway(Setting.DEFAULT_EP_VERSION), Version
                )
            }
        )
    if identifier == "setting.engineering-shape":
        return _returned(
            {
                "component_count": len(components),
                "patch_default_is_zero": components[2] == 0,
                "year_day_count": 366 if calendar.isleap(year) else 365,
                "year_is_non_leap": not calendar.isleap(year),
            }
        )
    raise RuntimeError(f"Unknown Setting case {identifier!r}.")


def _execute_default_ep_version(
    identifier: str, Setting: type, Version: type
) -> dict[str, Any]:
    components = list(Setting.DEFAULT_EP_VERSION)
    version = Version(*Setting.DEFAULT_EP_VERSION)
    if identifier == "setting-default-ep-version.components":
        return _returned(
            {"component_count": len(components), "components": components}
        )
    if identifier == "setting-default-ep-version.formatted-identities":
        return _returned(
            {
                "dotted": format(version, "."),
                "ep_dirname": version.ep_dirname,
                "hyphenated": format(version, "-"),
                "iddname": version.iddname,
            }
        )
    if identifier == "setting-default-ep-version.semantic-shape":
        return _returned(
            {
                "all_nonnegative": all(item >= 0 for item in components),
                "component_count": len(components),
                "patch_is_zero": components[2] == 0,
            }
        )
    raise RuntimeError(f"Unknown DEFAULT_EP_VERSION case {identifier!r}.")


def _execute_default_year(identifier: str, Setting: type) -> dict[str, Any]:
    year = Setting.DEFAULT_YEAR
    if identifier == "setting-default-year.calendar":
        return _returned(
            {
                "day_count": 366 if calendar.isleap(year) else 365,
                "is_leap": calendar.isleap(year),
                "year": year,
            }
        )
    if identifier == "setting-default-year.run-period":
        return _returned({"end": [year, 12, 31], "start": [year, 1, 1]})
    if identifier == "setting-default-year.scalar":
        return _returned(
            {
                "next_year": year + 1,
                "previous_year": year - 1,
                "text": str(year),
                "value": year,
            }
        )
    raise RuntimeError(f"Unknown DEFAULT_YEAR case {identifier!r}.")


def _execute_version_class(identifier: str, Version: type) -> dict[str, Any]:
    if identifier == "version-class.descriptor":
        public_descriptors = sorted(
            name
            for name in Version.__dict__
            if not name.startswith("_")
        )
        return _returned(
            {
                "defines_equality": "__eq__" in Version.__dict__,
                "has_instance_dictionary": hasattr(Version(24, 2, 0), "__dict__"),
                "public_descriptors": public_descriptors,
                "type_name": Version.__name__,
            }
        )
    if identifier == "version-class.identity-equality":
        left = Version(24, 2, 0)
        right = Version(24, 2, 0)
        return _returned(
            {
                "components_equal": list(left) == list(right),
                "separate_instances_equal": left == right,
                "self_equal": left == left,
            }
        )
    if identifier == "version-class.readonly-properties":
        value = Version(24, 2, 0)
        return _returned(
            {
                "observations": [
                    _observe(lambda: setattr(value, name, replacement))
                    for name, replacement in (
                        ("major", 25),
                        ("minor", 3),
                        ("patch", 1),
                    )
                ]
            }
        )
    raise RuntimeError(f"Unknown Version descriptor case {identifier!r}.")


def _execute_version_init(identifier: str, Version: type) -> dict[str, Any]:
    project = lambda value: _version_snapshot(value, Version)
    if identifier == "version-init.integer-overloads":
        calls = (
            lambda: Version(9, 6),
            lambda: Version(9, 6, 0),
            lambda: Version(-1, 2, 3),
            lambda: Version(True, 2),
        )
        return _returned(
            {"observations": [_observe(call, project) for call in calls]}
        )
    if identifier == "version-init.string-tokenization":
        values = (
            "V9-6-0",
            "9.6",
            "prefix24__2++0suffix",
            "-1.-2",
            "24..2",
            "V\u0661\u0662-\u0662-\u0660",
        )
        return _returned(
            {
                "observations": [
                    _observe(lambda value=value: Version(value), project)
                    for value in values
                ]
            }
        )
    if identifier == "version-init.failure-surface":
        calls = (
            lambda: Version(""),
            lambda: Version("9"),
            lambda: Version("1.2.3.4"),
            lambda: Version(9),
            lambda: Version(9, 6, "0"),
            lambda: Version(),
        )
        return _returned(
            {"observations": [_observe(call, project) for call in calls]}
        )
    raise RuntimeError(f"Unknown Version constructor case {identifier!r}.")


def _execute_version_format(identifier: str, Version: type) -> dict[str, Any]:
    value = Version(24, 2, 0)
    if identifier == "version-format.default-direct":
        return _returned(
            {
                "direct_default": value.__format__(),
                "explicit_dash": value.__format__("-"),
            }
        )
    if identifier == "version-format.delimiters":
        return _returned(
            {
                "dash": format(value, "-"),
                "dot": format(value, "."),
                "double_colon": format(value, "::"),
                "slash": format(value, "/"),
            }
        )
    if identifier == "version-format.empty-spec":
        return _returned(
            {
                "builtin_format": format(value),
                "direct_empty": value.__format__(""),
                "fstring_empty": f"{value}",
            }
        )
    raise RuntimeError(f"Unknown Version format case {identifier!r}.")


def _execute_version_iter(identifier: str, Version: type) -> dict[str, Any]:
    value = Version(24, 2, 0)
    if identifier == "version-iter.conversions":
        return _returned({"list": list(value), "tuple": list(tuple(value))})
    if identifier == "version-iter.fresh-generators":
        first = iter(value)
        second = iter(value)
        return _returned(
            {
                "first_is_second": first is second,
                "first_type": type(first).__name__,
                "first_values": list(first),
                "second_values": list(second),
            }
        )
    if identifier == "version-iter.ordered-exhaustion":
        iterator = iter(value)
        values = [next(iterator), next(iterator), next(iterator)]
        try:
            next(iterator)
            exhausted = False
        except StopIteration:
            exhausted = True
        return _returned({"exhausted": exhausted, "values": values})
    raise RuntimeError(f"Unknown Version iteration case {identifier!r}.")


def _execute_ep_dirname(identifier: str, Version: type) -> dict[str, Any]:
    if identifier == "version-ep-dirname.default":
        value = Version(24, 2, 0)
        return _returned({"components": list(value), "value": value.ep_dirname})
    if identifier == "version-ep-dirname.legacy":
        value = Version("V9.6")
        return _returned({"components": list(value), "value": value.ep_dirname})
    if identifier == "version-ep-dirname.zero-and-large":
        return _returned(
            {
                "observations": [
                    _observe(lambda: Version(0, 0, 0).ep_dirname),
                    _observe(lambda: Version(123, 45, 6).ep_dirname),
                ]
            }
        )
    raise RuntimeError(f"Unknown Version ep_dirname case {identifier!r}.")


def _execute_iddname(identifier: str, Version: type) -> dict[str, Any]:
    if identifier == "version-iddname.default":
        value = Version(24, 2, 0)
        return _returned({"components": list(value), "value": value.iddname})
    if identifier == "version-iddname.legacy":
        value = Version("V9.6")
        return _returned({"components": list(value), "value": value.iddname})
    if identifier == "version-iddname.zero-and-large":
        return _returned(
            {
                "observations": [
                    _observe(lambda: Version(0, 0, 0).iddname),
                    _observe(lambda: Version(123, 45, 6).iddname),
                ]
            }
        )
    raise RuntimeError(f"Unknown Version iddname case {identifier!r}.")


def _execute_property(
    identifier: str, Version: type, property_name: str
) -> dict[str, Any]:
    if identifier.endswith(".default-baseline"):
        return _returned(_property_facts(Version(24, 2, 0), property_name))
    if identifier.endswith(".explicit-three"):
        return _returned(_property_facts(Version(9, 6, 7), property_name))
    if identifier.endswith(".two-component-default"):
        return _returned(_property_facts(Version(9, 6), property_name))
    raise RuntimeError(f"Unknown Version {property_name} case {identifier!r}.")


def _execute_version_coerce(identifier: str, Version: type) -> dict[str, Any]:
    project = lambda value: _version_snapshot(value, Version)
    if identifier == "version-coerce.existing-identity":
        value = Version(24, 2, 0)
        coerced = Version.to_version_anyway(value)
        return _returned(
            {"same_identity": coerced is value, "version": project(coerced)}
        )
    if identifier == "version-coerce.strings-and-sequences":
        calls = (
            lambda: Version.to_version_anyway("V24-2-0"),
            lambda: Version.to_version_anyway((24, 2, 0)),
            lambda: Version.to_version_anyway([24, 2]),
        )
        return _returned(
            {"observations": [_observe(call, project) for call in calls]}
        )
    if identifier == "version-coerce.failure-surface":
        calls = (
            lambda: Version.to_version_anyway((24,)),
            lambda: Version.to_version_anyway(None),
            lambda: Version.to_version_anyway([24, 2, "0"]),
            lambda: Version.to_version_anyway(24),
        )
        return _returned(
            {"observations": [_observe(call, project) for call in calls]}
        )
    raise RuntimeError(f"Unknown Version coercion case {identifier!r}.")


def _execute(
    definition: dict[str, Any], Setting: type, Version: type
) -> dict[str, Any]:
    identifier = definition["id"]
    executor = definition["executor"]
    if executor == "setting":
        return _execute_setting(identifier, Setting, Version)
    if executor == "setting-default-ep-version":
        return _execute_default_ep_version(identifier, Setting, Version)
    if executor == "setting-default-year":
        return _execute_default_year(identifier, Setting)
    if executor == "version-class":
        return _execute_version_class(identifier, Version)
    if executor == "version-init":
        return _execute_version_init(identifier, Version)
    if executor == "version-format":
        return _execute_version_format(identifier, Version)
    if executor == "version-iter":
        return _execute_version_iter(identifier, Version)
    if executor == "version-ep-dirname":
        return _execute_ep_dirname(identifier, Version)
    if executor == "version-iddname":
        return _execute_iddname(identifier, Version)
    if executor in {"version-major", "version-minor", "version-patch"}:
        return _execute_property(identifier, Version, executor.removeprefix("version-"))
    if executor == "version-coerce":
        return _execute_version_coerce(identifier, Version)
    raise RuntimeError(f"Unknown common executor {executor!r}.")


def _require_exact_keys(value: Any, expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(
            f"{context} has an invalid key set: expected {sorted(expected)}, "
            f"got {actual}."
        )


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
                value, {"hex_without_prefix", "kind"}, f"Binary64 at {context}"
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
        raise RuntimeError(
            f"Unsupported observation value at {context}: {type(value).__name__}."
        )


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
            if item["error_category"] not in {"domain", "range", "type"}:
                raise RuntimeError(f"{context}[{index}] has an invalid error category.")
            if not isinstance(item["exception_type"], str) or not item["exception_type"]:
                raise RuntimeError(f"{context}[{index}] has no exception type.")
            if not isinstance(item["message"], str):
                raise RuntimeError(f"{context}[{index}] has no exception message.")
        else:
            raise RuntimeError(f"{context}[{index}] has an invalid outcome.")
        _validate_normalized_tree(item, f"{context}[{index}]")


def _case_by_id(value: dict[str, Any], identifier: str) -> dict[str, Any]:
    return next(item for item in value["cases"] if item["id"] == identifier)


def _validate_semantics(value: dict[str, Any]) -> None:
    baseline = _case_by_id(value, "setting.baseline-values")["python"]["facts"]
    if baseline != {"default_ep_version": [24, 2, 0], "default_year": 2026}:
        raise RuntimeError("The common Setting baseline drifted.")

    calendar_facts = _case_by_id(
        value, "setting-default-year.calendar"
    )["python"]["facts"]
    if calendar_facts != {"day_count": 365, "is_leap": False, "year": 2026}:
        raise RuntimeError("The common default-year calendar contract drifted.")

    identity = _case_by_id(
        value, "version-class.identity-equality"
    )["python"]["facts"]
    if identity != {
        "components_equal": True,
        "separate_instances_equal": False,
        "self_equal": True,
    }:
        raise RuntimeError("The upstream Version identity equality contract drifted.")

    readonly = _case_by_id(
        value, "version-class.readonly-properties"
    )["python"]["facts"]["observations"]
    if [item["exception_type"] for item in readonly] != [
        "AttributeError",
        "AttributeError",
        "AttributeError",
    ]:
        raise RuntimeError("The upstream Version public-property surface drifted.")

    integer_overloads = _case_by_id(
        value, "version-init.integer-overloads"
    )["python"]["facts"]["observations"]
    if [item["result"]["components"] for item in integer_overloads] != [
        [9, 6, 0],
        [9, 6, 0],
        [-1, 2, 3],
        [True, 2, 0],
    ]:
        raise RuntimeError("The upstream Version integer overload contract drifted.")
    if integer_overloads[3]["result"]["component_types"] != [
        "bool",
        "int",
        "int",
    ]:
        raise RuntimeError("The upstream Version bool-as-int behavior drifted.")

    tokenized = _case_by_id(
        value, "version-init.string-tokenization"
    )["python"]["facts"]["observations"]
    if [item["result"]["components"] for item in tokenized] != [
        [9, 6, 0],
        [9, 6, 0],
        [24, 2, 0],
        [1, 2, 0],
        [24, 2, 0],
        [12, 2, 0],
    ]:
        raise RuntimeError("The upstream Version string tokenization drifted.")

    failures = _case_by_id(
        value, "version-init.failure-surface"
    )["python"]["facts"]["observations"]
    if [item["exception_type"] for item in failures] != [
        "ValueError",
        "ValueError",
        "ValueError",
        "TypeError",
        "TypeError",
        "ValueError",
    ]:
        raise RuntimeError("The upstream Version constructor failure surface drifted.")
    if failures[3]["message"] != "sequence item 0: expected str instance, type found":
        raise RuntimeError("The pinned Version invalid-type defect drifted.")

    default_format = _case_by_id(
        value, "version-format.default-direct"
    )["python"]["facts"]
    empty_format = _case_by_id(
        value, "version-format.empty-spec"
    )["python"]["facts"]
    if default_format != {"direct_default": "24-2-0", "explicit_dash": "24-2-0"}:
        raise RuntimeError("The Version direct-format default drifted.")
    if set(empty_format.values()) != {"2420"}:
        raise RuntimeError("The Version empty-format quirk drifted.")

    ep_name = _case_by_id(value, "version-ep-dirname.default")["python"]["facts"]
    idd_name = _case_by_id(value, "version-iddname.default")["python"]["facts"]
    if ep_name["value"] != "EnergyPlusV24-2-0":
        raise RuntimeError("The Version EnergyPlus directory name drifted.")
    if idd_name["value"] != "V24-2-0-Energy+.idd":
        raise RuntimeError("The Version IDD filename drifted.")

    existing = _case_by_id(
        value, "version-coerce.existing-identity"
    )["python"]["facts"]
    if existing["same_identity"] is not True:
        raise RuntimeError("Version.to_version_anyway stopped preserving identity.")
    coercion_failures = _case_by_id(
        value, "version-coerce.failure-surface"
    )["python"]["facts"]["observations"]
    if {item["exception_type"] for item in coercion_failures} != {"TypeError"}:
        raise RuntimeError("The Version coercion failure surface drifted.")


def validate_oracle(value: dict[str, Any]) -> None:
    """Fail closed on the complete artifact before writing any bytes."""

    _require_exact_keys(value, ORACLE_KEYS, "Common core oracle root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("The common core oracle schema drifted.")

    upstream = value["upstream"]
    _require_exact_keys(upstream, UPSTREAM_KEYS, "Common upstream receipt")
    if upstream != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("The common upstream receipt is not exact.")

    runtime = value["runtime"]
    _require_exact_keys(runtime, RUNTIME_KEYS, "Common runtime receipt")
    if runtime != {
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("The common runtime receipt is not exact.")

    symbols = value["symbols"]
    if not isinstance(symbols, list) or len(symbols) != len(TARGET_SYMBOLS):
        raise RuntimeError("The common symbol receipt count is not exact.")
    for expected_symbol, receipt in zip(TARGET_SYMBOLS, symbols, strict=True):
        _require_exact_keys(receipt, SYMBOL_KEYS, f"Symbol receipt {expected_symbol!r}")
        if receipt != {
            **EXPECTED_SYMBOL_RECEIPTS[expected_symbol],
            "path": SOURCE_PATH,
            "symbol": expected_symbol,
        }:
            raise RuntimeError(f"Symbol receipt {expected_symbol!r} is not exact.")

    definitions = case_definitions()
    definition_by_id = {item["id"]: item for item in definitions}
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("The common case count is not exact.")
    identifiers = [item.get("id") for item in cases if isinstance(item, dict)]
    if identifiers != [item["id"] for item in definitions]:
        raise RuntimeError("The common case order drifted.")

    for case in cases:
        if not isinstance(case, dict):
            raise RuntimeError("A common oracle case is not an object.")
        identifier = case.get("id")
        if identifier not in definition_by_id:
            raise RuntimeError(f"Unknown common case {identifier!r}.")
        definition = definition_by_id[identifier]
        expected_keys = CASE_KEYS
        if definition["expected_dotnet"] is not None:
            expected_keys = expected_keys | {"expected_dotnet"}
        _require_exact_keys(case, expected_keys, f"Common case {identifier!r}")
        if case["executor"] != definition["executor"]:
            raise RuntimeError(f"Case {identifier!r} executor drifted.")
        if case["symbol"] != definition["symbol"]:
            raise RuntimeError(f"Case {identifier!r} symbol drifted.")
        if definition["expected_dotnet"] is not None and case[
            "expected_dotnet"
        ] != definition["expected_dotnet"]:
            raise RuntimeError(f"Case {identifier!r} native expectation drifted.")

        python = case["python"]
        _require_exact_keys(python, PYTHON_RETURN_KEYS, f"Python case {identifier!r}")
        if python["outcome"] != "returned":
            raise RuntimeError(f"Python case {identifier!r} did not return.")
        facts = python["facts"]
        _require_exact_keys(facts, CASE_FACT_KEYS[identifier], f"Facts {identifier!r}")
        if "observations" in facts:
            _validate_observations(facts["observations"], identifier)
        _validate_normalized_tree(python, f"case.{identifier}.python")

    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("The common cases hash is invalid.")

    consumer = value["consumer_contract"]
    _require_exact_keys(consumer, CONSUMER_CONTRACT_KEYS, "Common consumer contract")
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
        raise RuntimeError("The common consumer contract drifted.")

    _validate_semantics(value)
    serialized = strict_json_dumps(value)
    if RAW_ADDRESS_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime address entered the common oracle.")


def _find_pinned_common_source() -> Path:
    """Resolve exactly one byte-pinned ``common.py`` from bootstrap sys.path."""

    candidates: dict[str, Path] = {}
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        candidate = root / IMPORT_RELATIVE_PATH
        try:
            if not candidate.is_file():
                continue
            resolved = candidate.resolve(strict=True)
        except OSError:
            continue
        if sha256_file(resolved) == EXPECTED_SOURCE_SHA256:
            candidates[str(resolved).casefold()] = resolved
    if len(candidates) != 1:
        raise SystemExit(
            "Expected exactly one byte-pinned idragon/common.py on the bootstrap "
            f"path, found {len(candidates)}."
        )
    return next(iter(candidates.values()))


def _load_pinned_common_module(path: Path) -> Any:
    if sha256_file(path) != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The selected common.py is not the exact pinned source.")
    spec = importlib.util.spec_from_file_location(
        "_dragons_pinned_idragon_common", path
    )
    if spec is None or spec.loader is None:
        raise SystemExit(f"Cannot directly import pinned common.py: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    imported_path = Path(module.__file__).resolve()
    if imported_path != path.resolve() or sha256_file(imported_path) != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The directly imported common.py source identity drifted.")
    if not isinstance(module.Setting.DEFAULT_EP_VERSION, tuple):
        raise SystemExit("The pinned default EnergyPlus version is not a tuple.")
    if module.Setting.DEFAULT_EP_VERSION != (24, 2, 0):
        raise SystemExit("The pinned default EnergyPlus version drifted.")
    if module.Setting.DEFAULT_YEAR != 2026:
        raise SystemExit("The pinned default simulation year drifted.")
    return module


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    imported_source = _find_pinned_common_source()
    imported_sha256 = sha256_file(imported_source)
    if imported_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported common.py is not the inventoried source.")
    module = _load_pinned_common_module(imported_source)
    Setting = module.Setting
    Version = module.Version

    definitions = case_definitions()
    cases: list[dict[str, Any]] = []
    for definition in definitions:
        case = {
            "executor": definition["executor"],
            "id": definition["id"],
            "python": _execute(definition, Setting, Version),
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
            "source_sha256": imported_sha256,
        },
    }
    validate_oracle(result)
    return result


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the common core oracle.")
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
    print(f"Wrote common core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

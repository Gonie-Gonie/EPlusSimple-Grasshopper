"""Generate pinned observations for the residual ``epsimple.core.profile`` API.

The corpus contains exactly three cases for each of the thirteen public symbols
left after the existing UsageProfile schedule coverage.  Run this generator
only through ``bootstrap_reference.py`` so imports resolve from the pinned
CPython 3.12.7 dependency tree and upstream checkout.
"""

from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import importlib.util
import os
from pathlib import Path
import re
import sys
import tempfile
from typing import Any


SCHEMA = "dragons.simpledragon.usage-profile-core-oracle.v1"
SOURCE_PATH = "src/epsimple/core/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e43f07d41e1e90cb9dcb7207fce67d8a6cb93acf54242b7a87c0aa30dda1309c"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "KoreanUsageProfile": {
        "body_hash": "sha256:7594749ad9c4f32ae9f1ea29805b588ae2e6493decca92fda443e9363102903a",
        "kind": "class",
        "signature_hash": "sha256:52fc20db82dbf6bb9654482bbd6d5d08dd2bcfdd42db88091dc00e0dfd5d87e6",
        "symbol_hash": "sha256:52a3656b8d8c7abbbbc0403206bede355eb776a007aee2f301c0819bf9a3044f",
    },
    "KoreanUsageProfile.DHW_HEAT_PER_LITER": {
        "body_hash": "sha256:43b0a1e070650d194d682674baf38e90a76f731ec3761d157708953c6ed428bf",
        "kind": "constant",
        "signature_hash": "sha256:7845fdd56019103d844c7b7a865059fbfe15574031fff7e4e921fdd375c285af",
        "symbol_hash": "sha256:f43f031dc4dd8dd0426bbe82871259fbfad10d3011a78f865d753c02b5203f98",
    },
    "KoreanUsageProfile.ID": {
        "body_hash": "sha256:8b81f98bc84a8caff11fe0920e2d11663748bc18953a73f761ef279d5db698da",
        "kind": "function",
        "signature_hash": "sha256:b14de42215f3df7a3eb60763850f7ddb187c7effeafa2a8816b4168ca283fcdb",
        "symbol_hash": "sha256:246156d9c5e30456c2c58c64d1bc48da290df6081b24e05e52b95993f9e1b0e2",
    },
    "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL": {
        "body_hash": "sha256:7bf2eac9a816cecbf9a285d8e32c30a318024376221831c47fc41ef1200d8665",
        "kind": "constant",
        "signature_hash": "sha256:56324a51320f6e58c78e4f87d74a7154168d47d805bd66317c622c49911294f8",
        "symbol_hash": "sha256:e2da67c4aaf7e9220236aa66fead7ae05e32914c255302ea32fcee37c1679b72",
    },
    "KoreanUsageProfile.__init__": {
        "body_hash": "sha256:f206c2112434015da09a81e129633eb8f6825e79569cc81d294f97410b474fcc",
        "kind": "function",
        "signature_hash": "sha256:89c656bd22ded0c0657f5a722839fafdb42cccd7f610bb43b7040cba6695805d",
        "symbol_hash": "sha256:f242c8e5794ae9b49de1e768956963696c8351c947440f6fa3ef1f70230d50f0",
    },
    "KoreanUsageProfile.occupied_hours": {
        "body_hash": "sha256:16138d28c27becbb4fa4a8cd449bd3cf98db4060d4b29455953384886f37ac75",
        "kind": "function",
        "signature_hash": "sha256:eb9fa11a201dd61305f0314fe0261cbc371edeb6909c805081c19c6b05e73876",
        "symbol_hash": "sha256:511dd2e08d266e099afda2d88e98ff3f7976a7fb6738bb8ff2304f19033cfc90",
    },
    "KoreanUsageProfile.operating_days": {
        "body_hash": "sha256:d86bd36f9d41592b8774143ffc945075fd0faa0cf82fc7b1ac571423f4a1f382",
        "kind": "function",
        "signature_hash": "sha256:3600cccc11bc6800f262c4e5f0aacb4e7f2bf7ca486cbc455c0376a25e228afd",
        "symbol_hash": "sha256:1ab019f1c745c00702036a96b87c9d604cd18608b033510e3de6621e8f6a930d",
    },
    "KoreanUsageProfile.to_dict": {
        "body_hash": "sha256:368f4628ce0a2ef5bef5d48ec4f456c8f031be49094e21df37458ffd2a8ffec4",
        "kind": "function",
        "signature_hash": "sha256:b38b1b6e4f6aab6bc88bb0fcbf1620621166ceff9373e717951d554649663abf",
        "symbol_hash": "sha256:40c556a7cf3a93741c48f26c3eb30ba4d70f7dade0abfc3ef50ecfbf3cfded5e",
    },
    "KoreanUsageProfile.to_dragon": {
        "body_hash": "sha256:ae4c8cbc2e4327627bf44d9a2c9d9373c86b3e5a1526bd089fadf9ca0e6e6291",
        "kind": "function",
        "signature_hash": "sha256:6f7976906c2ab650b07c77535c90a8ebdf8d495a52aebe95d2201ff513d29f07",
        "symbol_hash": "sha256:f3b70764f326865596e72fcfc799555b190ef18db77a54dcbfa6df012f236d3e",
    },
    "KoreanUsageProfileExtended": {
        "body_hash": "sha256:65200e57b6567a313c3d3ac518535f8188a781c942d8f67c386e20bc55dce686",
        "kind": "class",
        "signature_hash": "sha256:4e620273c8656d32f9be6d99fabfa0a3cfcafdfcd2098dc30dee339c4e58bb16",
        "symbol_hash": "sha256:5a6703884a6c29f977d9e025af134b26199f10dab6f1edb680ee161c0ece47e1",
    },
    "Profile": {
        "body_hash": "sha256:643d5437104296e21d906ecb15b2c96ad278f20cfc4af53b12bb6069bd853726",
        "kind": "class",
        "signature_hash": "sha256:bf35db5abe6e8851938c2d634421f972436bb46ab9abab1dca41465ffcd7e9d4",
        "symbol_hash": "sha256:3cf55ef99529b6051e2e5bea5c32bbecc5850819101e522fed1008be0599d6ad",
    },
    "Profile.get_DB": {
        "body_hash": "sha256:cb03eb616b3998116052d637f18e3d9ad13e571cf74878b01281b8b11d4406f6",
        "kind": "function",
        "signature_hash": "sha256:0d34914867d00b5b2ea706bb6109049695c2f386f02f0b59a77a3d51dcfc0011",
        "symbol_hash": "sha256:a8448202da1e84bb21aa6672fee1c03fb401390f51cf1ea4b2d6810af74aeecc",
    },
    "read_csv_without_units": {
        "body_hash": "sha256:da342c3cae4fbd3456d7c2f712ae3670576b88c5fe264fab2f541db0bf84383c",
        "kind": "function",
        "signature_hash": "sha256:33729a0e3540283ad3b0b84235e4b6997278a0b31619ee3f51c3f1906460101e",
        "symbol_hash": "sha256:77befcdc77b99adb5b3b7311f90774dd82ff72ba4eea1c6b7058419c1aff412a",
    },
}
EXPECTED_SYMBOL_HASHES = {
    symbol: receipt["symbol_hash"]
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_EQUIVALENT_SYMBOLS = frozenset(
    {
        "KoreanUsageProfile.DHW_HEAT_PER_LITER",
        "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL",
        "KoreanUsageProfile.occupied_hours",
        "KoreanUsageProfile.operating_days",
        "KoreanUsageProfile.to_dragon",
    }
)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "KoreanUsageProfile": "immutable-validated-usage-profile-value-object",
    "KoreanUsageProfile.ID": "deterministic-native-usage-profile-identity",
    "KoreanUsageProfile.__init__": "validated-immutable-usage-profile-construction",
    "KoreanUsageProfile.to_dict": "typed-usage-profile-serialization",
    "KoreanUsageProfileExtended": "usage-profile-source-discriminator",
    "Profile": "immutable-usage-profile-database",
    "Profile.get_DB": "diagnostic-usage-profile-lookup",
    "read_csv_without_units": "strict-invariant-profile-csv-reader",
}
EXPECTED_CASE_COUNT = 39
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
PROFILE_DAY_KEYS = (
    "monday",
    "tuesday",
    "wednesday",
    "thursday",
    "friday",
    "saturday",
    "sunday",
    "holiday",
)
PROFILE_FIELD_KEYS = (
    "name",
    "occupant_start",
    "occupant_end",
    "hvac_start",
    "hvac_end",
    "ventilation",
    "domestic_hotwater",
    "lighting_hours",
    "occupancy",
    "equipment",
    "heating_setpoint",
    "cooling_setpoint",
    *tuple(f"operate_in_{day}" for day in PROFILE_DAY_KEYS),
    "vacations",
    "ID",
)
TO_DICT_KEYS = (
    "name",
    "occupant_start",
    "occupant_end",
    "hvac_start",
    "hvac_end",
    "ventilation",
    "domestic_hotwater",
    "lighting_hours",
    "occupancy",
    "equipment",
    "heating_setpoint",
    "cooling_setpoint",
    "operate_weekdays",
    "vacations",
)
SCHEDULE_SLOTS = (
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
SCHEDULE_DESCRIPTOR_KEYS = {
    "idf_fields",
    "maximum",
    "minimum",
    "name",
    "schedule_type",
    "value_count",
    "values_encoding",
    "values_sha256",
}
CONVERTED_PROFILE_KEYS = {
    "domestic_hotwater",
    "name",
    "occupied_hours",
    "operating_days",
    "native_output_identity",
    "output_name",
    "schedules",
    "source",
    "source_identity",
    "upstream_output_name_equals_source_identity",
    "vacations",
    "ventilation",
}
DATABASE_OUTPUT_IDENTITY_KEYS = {
    "adaptation",
    "comparison",
    "python_counterpart",
}
CUSTOM_OUTPUT_IDENTITY_KEYS = {
    "comparison",
    "python_counterpart",
}
RAW_RUNTIME_IDENTITY_PATTERN = re.compile(r"0x[0-9a-fA-F]{7,16}")
FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS = (
    "append",
    "container",
    "list",
    "mutability",
    "mutable",
    "python_type",
)
EQUIVALENT_FACT_KEYS = {
    "dhw-heat-per-liter.database-factors": {"factors"},
    "dhw-heat-per-liter.numeric-kind": {
        "arithmetic_probe",
        "is_boolean",
        "is_integral",
    },
    "dhw-heat-per-liter.value": {"value"},
    "occupied-hours.daytime": {"occupant_end", "occupant_start", "value"},
    "occupied-hours.equal-full-day": {"occupant_end", "occupant_start", "value"},
    "occupied-hours.overnight": {"occupant_end", "occupant_start", "value"},
    "operating-days.all": {"flags", "value"},
    "operating-days.none": {"flags", "value"},
    "operating-days.sparse-order": {"flags", "value"},
    "people-activity-level.database-factors": {"factors"},
    "people-activity-level.numeric-kind": {
        "arithmetic_probe",
        "is_boolean",
        "is_integral",
    },
    "people-activity-level.value": {"value"},
    "usage-profile-dragon.all-database-profiles": {
        "profile_count",
        "profiles",
        "schedule_slots",
    },
    "usage-profile-dragon.lighting-tie": {
        "fractional_lighting_value_count",
        "fractional_lighting_values",
        "profile",
        "schedule_slots",
    },
    "usage-profile-dragon.overnight-vacation": {
        "leap_day_failure",
        "overnight",
        "profile",
        "schedule_slots",
        "vacation_count",
        "wrapped_vacation_noop",
    },
}
WRAPPED_VACATION_NOOP_KEYS = {
    "end",
    "schedule_slots_equal_without_vacation",
    "start",
    "vacation_mask_positive_days",
}
LEAP_DAY_FAILURE_FACT_KEYS = {"end", "start"}


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_day_schedule_core_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_usage_profile_core_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load UsageProfile core oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("UsageProfile core oracle support is not pinned.")
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
    """Apply the existing hardened whole-inventory validator to this slice."""

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
        raise SystemExit("The inventory does not exactly cover 13 UsageProfile symbols.")
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


def normalize(value: Any) -> Any:
    return BASE.normalize(value)


encode = normalize


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


class IdentityNormalizer:
    """Replace every embedded CPython address with encounter-ordered tokens."""

    def __init__(self) -> None:
        self._tokens: dict[str, str] = {}

    def text(self, value: str) -> str:
        if not isinstance(value, str):
            raise RuntimeError("A runtime identity normalizer received non-text.")

        def replace(match: re.Match[str]) -> str:
            source = match.group(0)
            if source not in self._tokens:
                self._tokens[source] = f"runtime-identity-{len(self._tokens) + 1:04d}"
            return self._tokens[source]

        return RAW_RUNTIME_IDENTITY_PATTERN.sub(replace, value)

    def name(self, value: str) -> dict[str, str]:
        normalized = self.text(value)
        if normalized == value:
            return {"policy": "literal", "value": value}
        return {"policy": "tokenized-runtime-identities", "value": normalized}


def _dotnet(
    adaptation: str,
    outcome: str,
    error_category: str | None = None,
) -> dict[str, str]:
    if adaptation not in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.values():
        raise RuntimeError(f"Unknown UsageProfile core adaptation {adaptation!r}.")
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
        _case("usage-profile.alias-topology", "usage-profile", "KoreanUsageProfile"),
        _case("usage-profile.identity-equality", "usage-profile", "KoreanUsageProfile"),
        _case("usage-profile.mutable-surface", "usage-profile", "KoreanUsageProfile"),
        _case("dhw-heat-per-liter.database-factors", "constant", "KoreanUsageProfile.DHW_HEAT_PER_LITER"),
        _case("dhw-heat-per-liter.numeric-kind", "constant", "KoreanUsageProfile.DHW_HEAT_PER_LITER"),
        _case("dhw-heat-per-liter.value", "constant", "KoreanUsageProfile.DHW_HEAT_PER_LITER"),
        _case("usage-profile-id.explicit", "usage-profile-id", "KoreanUsageProfile.ID"),
        _case("usage-profile-id.private-mutation", "usage-profile-id", "KoreanUsageProfile.ID"),
        _case("usage-profile-id.runtime-default", "usage-profile-id", "KoreanUsageProfile.ID"),
        _case("people-activity-level.database-factors", "constant", "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL"),
        _case("people-activity-level.numeric-kind", "constant", "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL"),
        _case("people-activity-level.value", "constant", "KoreanUsageProfile.PEOPLE_ACTIVITY_LEVEL"),
        _case("usage-profile-init.complete", "usage-profile-init", "KoreanUsageProfile.__init__"),
        _case("usage-profile-init.mutable-inputs", "usage-profile-init", "KoreanUsageProfile.__init__"),
        _case("usage-profile-init.unvalidated", "usage-profile-init", "KoreanUsageProfile.__init__", "raised", "type"),
        _case("occupied-hours.daytime", "occupied-hours", "KoreanUsageProfile.occupied_hours"),
        _case("occupied-hours.equal-full-day", "occupied-hours", "KoreanUsageProfile.occupied_hours"),
        _case("occupied-hours.overnight", "occupied-hours", "KoreanUsageProfile.occupied_hours"),
        _case("operating-days.all", "operating-days", "KoreanUsageProfile.operating_days"),
        _case("operating-days.none", "operating-days", "KoreanUsageProfile.operating_days"),
        _case("operating-days.sparse-order", "operating-days", "KoreanUsageProfile.operating_days"),
        _case("usage-profile-dict.exact-order", "usage-profile-dict", "KoreanUsageProfile.to_dict"),
        _case("usage-profile-dict.sparse-days", "usage-profile-dict", "KoreanUsageProfile.to_dict"),
        _case("usage-profile-dict.vacations", "usage-profile-dict", "KoreanUsageProfile.to_dict"),
        _case("usage-profile-dragon.all-database-profiles", "usage-profile-dragon", "KoreanUsageProfile.to_dragon"),
        _case("usage-profile-dragon.lighting-tie", "usage-profile-dragon", "KoreanUsageProfile.to_dragon"),
        _case("usage-profile-dragon.overnight-vacation", "usage-profile-dragon", "KoreanUsageProfile.to_dragon"),
        _case("usage-profile-extended.database-membership", "usage-profile-extended", "KoreanUsageProfileExtended"),
        _case("usage-profile-extended.datapath", "usage-profile-extended", "KoreanUsageProfileExtended"),
        _case("usage-profile-extended.subclass-topology", "usage-profile-extended", "KoreanUsageProfileExtended"),
        _case("usage-profile-database.alias-topology", "usage-profile-database", "Profile"),
        _case("usage-profile-database.mutable-registry", "usage-profile-database", "Profile"),
        _case("usage-profile-database.type-topology", "usage-profile-database", "Profile"),
        _case("usage-profile-lookup.all", "usage-profile-lookup", "Profile.get_DB"),
        _case("usage-profile-lookup.found-and-path", "usage-profile-lookup", "Profile.get_DB"),
        _case("usage-profile-lookup.missing", "usage-profile-lookup", "Profile.get_DB"),
        _case("profile-csv.greedy-header-and-quotes", "profile-csv", "read_csv_without_units"),
        _case("profile-csv.packaged-sources", "profile-csv", "read_csv_without_units"),
        _case("profile-csv.strip-unit-headers", "profile-csv", "read_csv_without_units"),
    )
    ordered = tuple(sorted(definitions, key=lambda item: item["id"]))
    validate_case_definitions(ordered)
    return ordered


def validate_case_definitions(definitions: tuple[dict[str, Any], ...]) -> None:
    identifiers = [item.get("id") for item in definitions]
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} UsageProfile core cases, got {len(definitions)}."
        )
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("UsageProfile core case identifiers are not unique and sorted.")
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
            raise RuntimeError("A UsageProfile core case has an invalid identifier.")
        if not isinstance(executor, str) or not executor:
            raise RuntimeError(f"Case {identifier!r} has an invalid executor.")
        if symbol not in TARGET_SYMBOLS:
            raise RuntimeError(f"Case {identifier!r} targets an unknown symbol.")
        counts[symbol] += 1
        adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
        expectation = definition["expected_dotnet"]
        if adaptation is None:
            if expectation is not None:
                raise RuntimeError(
                    f"Equivalent case {identifier!r} unexpectedly has an adaptation."
                )
        else:
            _validate_dotnet_expectation(identifier, expectation, adaptation)
    if counts != Counter({symbol: 3 for symbol in TARGET_SYMBOLS}):
        raise RuntimeError("UsageProfile core does not contain three cases per symbol.")


def _validate_dotnet_expectation(
    identifier: str, expectation: Any, adaptation: str
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


def _returned(facts: dict[str, Any]) -> dict[str, Any]:
    return BASE._returned(facts)


def _raised(exception: Exception, facts: dict[str, Any] | None = None) -> dict[str, Any]:
    return BASE._raised(exception, facts)


def _profile_inputs(**overrides: Any) -> dict[str, Any]:
    value: dict[str, Any] = {
        "name": "Oracle Probe",
        "occupant_start": 9,
        "occupant_end": 18,
        "hvac_start": 7,
        "hvac_end": 19,
        "ventilation": 1.25,
        "domestic_hotwater": 40.0,
        "lighting_hours": 5,
        "occupancy": 30.0,
        "equipment": 42.0,
        "heating_setpoint": 20.0,
        "cooling_setpoint": 26.0,
        "operate_in_monday": True,
        "operate_in_tuesday": True,
        "operate_in_wednesday": True,
        "operate_in_thursday": True,
        "operate_in_friday": True,
        "operate_in_saturday": False,
        "operate_in_sunday": False,
        "operate_in_holiday": False,
        "vacations": [((7, 1), (7, 7))],
        "ID": "PROFILE-ORACLE-PROBE",
    }
    value.update(overrides)
    return value


def _make_profile(profile_type: type, **overrides: Any) -> Any:
    return profile_type(**_profile_inputs(**overrides))


def _profile_snapshot(profile: Any, identity: IdentityNormalizer | None = None) -> dict[str, Any]:
    identity = identity or IdentityNormalizer()
    return {
        "id": identity.name(profile.ID),
        "name": profile.name,
        "occupant_start": profile.occupant_start,
        "occupant_end": profile.occupant_end,
        "hvac_start": profile.hvac_start,
        "hvac_end": profile.hvac_end,
        "ventilation": profile.ventilation,
        "domestic_hotwater": profile.domestic_hotwater,
        "lighting_hours": profile.lighting_hours,
        "occupancy": profile.occupancy,
        "equipment": profile.equipment,
        "heating_setpoint": profile.heating_setpoint,
        "cooling_setpoint": profile.cooling_setpoint,
        "operate_flags": {
            day: getattr(profile, f"operate_in_{day}") for day in PROFILE_DAY_KEYS
        },
        "vacations": profile.vacations,
    }


def _values_sha256(schedule: Any) -> tuple[int, str]:
    digest = hashlib.sha256()
    count = 0
    for day_schedule in schedule.dayschedules:
        for value in day_schedule.data:
            if type(value) not in {int, float}:
                raise RuntimeError("A converted schedule contains a non-numeric value.")
            digest.update(float(value).hex().replace("0x", "").encode("ascii"))
            digest.update(b"\n")
            count += 1
    return count, f"sha256:{digest.hexdigest()}"


def _idf_fields(schedule: Any, identity: IdentityNormalizer) -> list[str]:
    value = schedule.to_idf_object()
    data = getattr(value, "data", None)
    if not isinstance(data, dict):
        raise RuntimeError("A converted schedule IDF object has no ordered data.")
    extended = next(
        (item for key, item in vars(value).items() if key.endswith("__extended_input")),
        None,
    )
    if not isinstance(extended, list):
        raise RuntimeError("A converted schedule IDF object has no extended data.")
    fields = list(data.values()) + list(extended)
    while fields and fields[-1] is None:
        fields.pop()
    return [identity.text("" if item is None else str(item)) for item in fields]


def _schedule_descriptor(schedule: Any, identity: IdentityNormalizer) -> dict[str, Any]:
    count, values_hash = _values_sha256(schedule)
    result = {
        "idf_fields": _idf_fields(schedule, identity),
        "maximum": schedule.max,
        "minimum": schedule.min,
        "name": identity.name(schedule.name),
        "schedule_type": schedule.type.value,
        "value_count": count,
        "values_encoding": "binary64-hex-without-prefix-lines",
        "values_sha256": values_hash,
    }
    if set(result) != SCHEDULE_DESCRIPTOR_KEYS or count != 365 * 144:
        raise RuntimeError("A converted schedule descriptor is not exact.")
    return result


def _converted_profile(
    profile: Any,
    extended_type: type,
    *,
    database_identity: bool,
) -> dict[str, Any]:
    identity = IdentityNormalizer()
    dragon_profile = profile.to_dragon()
    if database_identity:
        native_output_identity = {
            "adaptation": "deterministic-native-usage-profile-identity",
            "comparison": "native-only-output-id-equals-native-source-usage-profile-id",
            "python_counterpart": "absent",
        }
        source = "extended" if isinstance(profile, extended_type) else "standard"
    else:
        native_output_identity = {
            "comparison": "native-only-output-id-equals-exact-source-usage-profile-id",
            "python_counterpart": "absent",
        }
        source = "custom"
    schedules = {
        slot: _schedule_descriptor(getattr(dragon_profile, slot), identity)
        for slot in SCHEDULE_SLOTS
    }
    return {
        "name": profile.name,
        "native_output_identity": native_output_identity,
        "output_name": identity.name(dragon_profile.name),
        "source": source,
        "source_identity": identity.name(profile.ID),
        "upstream_output_name_equals_source_identity": dragon_profile.name
        == profile.ID,
        "ventilation": profile.ventilation,
        "domestic_hotwater": profile.domestic_hotwater,
        "occupied_hours": profile.occupied_hours,
        "operating_days": profile.operating_days,
        "vacations": [
            {
                "end": f"{end_month:02d}/{end_day:02d}",
                "start": f"{start_month:02d}/{start_day:02d}",
            }
            for (start_month, start_day), (end_month, end_day) in profile.vacations
        ],
        "schedules": schedules,
    }


def _flags(profile: Any) -> dict[str, bool]:
    return {day: bool(getattr(profile, f"operate_in_{day}")) for day in PROFILE_DAY_KEYS}


def _csv_descriptor(path: Path, read_csv_without_units: Any) -> dict[str, Any]:
    frame = read_csv_without_units(path)
    return {
        "columns": [str(item) for item in frame.columns],
        "filename": path.name,
        "row_count": int(len(frame.index)),
        "sha256": sha256_file(path),
    }


def _execute(
    identifier: str,
    KoreanUsageProfile: type,
    KoreanUsageProfileExtended: type,
    Profile: type,
    read_csv_without_units: Any,
) -> dict[str, Any]:
    K = KoreanUsageProfile
    E = KoreanUsageProfileExtended
    P = Profile

    if identifier == "usage-profile.alias-topology":
        vacations = [((1, 2), (1, 3))]
        profile = _make_profile(K, vacations=vacations)
        return _returned(
            {
                "input_is_stored_reference": profile.vacations is vacations,
                "snapshot": _profile_snapshot(profile),
                "vacation_entry_is_shared": profile.vacations[0] is vacations[0],
            }
        )
    if identifier == "usage-profile.identity-equality":
        left = _make_profile(K, ID="SAME-ID")
        right = _make_profile(K, ID="SAME-ID")
        return _returned(
            {
                "equal_hashes": hash(left) == hash(right),
                "left_equals_right": left == right,
                "left_equals_self": left == left,
                "same_id": left.ID == right.ID,
            }
        )
    if identifier == "usage-profile.mutable-surface":
        profile = _make_profile(K)
        profile.name = "Changed"
        profile.ventilation = -9.5
        profile.dynamic_note = "added"
        del profile.cooling_setpoint
        return _returned(
            {
                "cooling_attribute_deleted": not hasattr(profile, "cooling_setpoint"),
                "dynamic_note": profile.dynamic_note,
                "name": profile.name,
                "ventilation": profile.ventilation,
            }
        )

    if identifier == "dhw-heat-per-liter.value":
        return _returned({"value": K.DHW_HEAT_PER_LITER})
    if identifier == "dhw-heat-per-liter.numeric-kind":
        value = K.DHW_HEAT_PER_LITER
        return _returned(
            {
                "arithmetic_probe": value + 1,
                "is_boolean": isinstance(value, bool),
                "is_integral": isinstance(value, int),
            }
        )
    if identifier == "dhw-heat-per-liter.database-factors":
        return _returned(
            {
                "factors": [
                    {
                        "factor": profile.domestic_hotwater
                        / profile.occupied_hours
                        / K.DHW_HEAT_PER_LITER,
                        "name": profile.name,
                    }
                    for profile in P.get_DB("__all__")
                ]
            }
        )

    if identifier == "people-activity-level.value":
        return _returned({"value": K.PEOPLE_ACTIVITY_LEVEL})
    if identifier == "people-activity-level.numeric-kind":
        value = K.PEOPLE_ACTIVITY_LEVEL
        return _returned(
            {
                "arithmetic_probe": value + 1,
                "is_boolean": isinstance(value, bool),
                "is_integral": isinstance(value, int),
            }
        )
    if identifier == "people-activity-level.database-factors":
        return _returned(
            {
                "factors": [
                    {
                        "factor": profile.occupancy
                        / profile.occupied_hours
                        / K.PEOPLE_ACTIVITY_LEVEL,
                        "name": profile.name,
                    }
                    for profile in P.get_DB("__all__")
                ]
            }
        )

    if identifier == "usage-profile-id.explicit":
        profile = _make_profile(K, ID="  explicit ID  ")
        return _returned(
            {
                "id": profile.ID,
                "property_is_read_only": type(profile).ID.fset is None,
            }
        )
    if identifier == "usage-profile-id.runtime-default":
        identity = IdentityNormalizer()
        left = _make_profile(K, ID=None)
        right = _make_profile(K, ID=None)
        return _returned(
            {
                "identities_are_distinct": left.ID != right.ID,
                "left": identity.name(left.ID),
                "right": identity.name(right.ID),
            }
        )
    if identifier == "usage-profile-id.private-mutation":
        profile = _make_profile(K, ID="before")
        profile._KoreanUsageProfile__ID = "after"
        return _returned(
            {
                "after": profile.ID,
                "dictionary_has_mangled_key": "_KoreanUsageProfile__ID" in vars(profile),
                "hash_tracks_after": hash(profile) == hash("after"),
            }
        )

    if identifier == "usage-profile-init.complete":
        return _returned({"snapshot": _profile_snapshot(_make_profile(K))})
    if identifier == "usage-profile-init.mutable-inputs":
        vacations = [((2, 1), (2, 2))]
        profile = _make_profile(K, vacations=vacations)
        vacations.append(((3, 1), (3, 2)))
        return _returned(
            {
                "input_is_stored_reference": profile.vacations is vacations,
                "stored_count_after_input_change": len(profile.vacations),
                "stored_values": profile.vacations,
            }
        )
    if identifier == "usage-profile-init.unvalidated":
        profile = _make_profile(
            K,
            name=None,
            occupant_start="late",
            occupant_end="early",
            ventilation=float("nan"),
            domestic_hotwater=-1,
            operate_in_monday=1,
            vacations="not-vacations",
            ID=7,
        )
        return _returned(
            {
                "id": profile.ID,
                "name": profile.name,
                "occupant_end": profile.occupant_end,
                "occupant_start": profile.occupant_start,
                "operate_in_monday": profile.operate_in_monday,
                "vacations": profile.vacations,
                "ventilation": profile.ventilation,
            }
        )

    if identifier.startswith("occupied-hours."):
        starts = {
            "occupied-hours.daytime": (9, 18),
            "occupied-hours.equal-full-day": (8, 8),
            "occupied-hours.overnight": (22, 6),
        }
        start, end = starts[identifier]
        profile = _make_profile(K, occupant_start=start, occupant_end=end)
        return _returned(
            {"occupant_end": end, "occupant_start": start, "value": profile.occupied_hours}
        )

    if identifier.startswith("operating-days."):
        selected = {
            "operating-days.all": set(PROFILE_DAY_KEYS),
            "operating-days.none": set(),
            "operating-days.sparse-order": {"tuesday", "saturday", "holiday"},
        }[identifier]
        profile = _make_profile(
            K,
            **{f"operate_in_{day}": day in selected for day in PROFILE_DAY_KEYS},
        )
        return _returned({"flags": _flags(profile), "value": profile.operating_days})

    if identifier == "usage-profile-dict.exact-order":
        profile = _make_profile(K)
        value = profile.to_dict()
        return _returned(
            {
                "key_order": list(value),
                "result": value,
            }
        )
    if identifier == "usage-profile-dict.sparse-days":
        selected = {"monday", "thursday", "sunday"}
        profile = _make_profile(
            K,
            **{f"operate_in_{day}": day in selected for day in PROFILE_DAY_KEYS},
        )
        value = profile.to_dict()
        return _returned(
            {
                "key_order": list(value),
                "operate_weekdays": value["operate_weekdays"],
            }
        )
    if identifier == "usage-profile-dict.vacations":
        profile = _make_profile(
            K,
            vacations=[((1, 2), (3, 4)), ((11, 9), (12, 31))],
        )
        value = profile.to_dict()
        return _returned(
            {"key_order": list(value), "vacations": value["vacations"]}
        )

    if identifier == "usage-profile-dragon.all-database-profiles":
        profiles = P.get_DB("__all__")
        return _returned(
            {
                "profile_count": len(profiles),
                "profiles": [
                    _converted_profile(profile, E, database_identity=True)
                    for profile in profiles
                ],
                "schedule_slots": list(SCHEDULE_SLOTS),
            }
        )
    if identifier == "usage-profile-dragon.lighting-tie":
        profile = _make_profile(
            K,
            name="Lighting Tie",
            occupant_start=8,
            occupant_end=16,
            hvac_start=8,
            hvac_end=16,
            lighting_hours=0.25,
            vacations=[],
            ID="PROFILE-LIGHTING-TIE",
        )
        converted = _converted_profile(profile, E, database_identity=False)
        dragon_profile = profile.to_dragon()
        values = [
            float(value)
            for day in dragon_profile.lighting.dayschedules
            for value in day.data
            if float(value) not in {0.0, 1.0}
        ]
        distinct = sorted(set(values))
        return _returned(
            {
                "fractional_lighting_value_count": len(values),
                "fractional_lighting_values": distinct,
                "profile": converted,
                "schedule_slots": list(SCHEDULE_SLOTS),
            }
        )
    if identifier == "usage-profile-dragon.overnight-vacation":
        profile = _make_profile(
            K,
            name="Overnight Vacation",
            occupant_start=22,
            occupant_end=6,
            hvac_start=21,
            hvac_end=7,
            lighting_hours=2,
            vacations=[((8, 1), (8, 15))],
            ID="PROFILE-OVERNIGHT-VACATION",
        )
        wrapped_profile = _make_profile(
            K,
            name="Wrapped Vacation",
            occupant_start=22,
            occupant_end=6,
            hvac_start=21,
            hvac_end=7,
            lighting_hours=2,
            vacations=[((12, 29), (1, 3))],
            ID="PROFILE-WRAPPED-VACATION",
        )
        no_vacation_profile = _make_profile(
            K,
            name="Wrapped Vacation",
            occupant_start=22,
            occupant_end=6,
            hvac_start=21,
            hvac_end=7,
            lighting_hours=2,
            vacations=[],
            ID="PROFILE-WRAPPED-VACATION",
        )
        wrapped_converted = _converted_profile(
            wrapped_profile, E, database_identity=False
        )
        no_vacation_converted = _converted_profile(
            no_vacation_profile, E, database_identity=False
        )
        wrapped_mask = wrapped_profile._get_vacation_mask()
        wrapped_noop = {
            "end": "01/03",
            "schedule_slots_equal_without_vacation": [
                slot
                for slot in SCHEDULE_SLOTS
                if wrapped_converted["schedules"][slot]
                == no_vacation_converted["schedules"][slot]
            ],
            "start": "12/29",
            "vacation_mask_positive_days": sum(
                any(float(value) > 0.0 for value in day.data)
                for day in wrapped_mask.dayschedules
            ),
        }
        leap_profile = _make_profile(
            K,
            name="Leap Day Vacation",
            vacations=[((2, 29), (3, 1))],
            ID="PROFILE-LEAP-DAY-VACATION",
        )
        try:
            leap_profile.to_dragon()
        except Exception as exception:
            if not isinstance(exception, ValueError):
                raise RuntimeError(
                    "A leap-day UsageProfile conversion raised the wrong exception."
                ) from exception
            leap_failure = _raised(
                exception,
                {"end": "03/01", "start": "02/29"},
            )
        else:
            raise RuntimeError("A leap-day UsageProfile conversion unexpectedly returned.")
        return _returned(
            {
                "leap_day_failure": leap_failure,
                "overnight": profile.occupant_end < profile.occupant_start,
                "profile": _converted_profile(
                    profile, E, database_identity=False
                ),
                "schedule_slots": list(SCHEDULE_SLOTS),
                "vacation_count": len(profile.vacations),
                "wrapped_vacation_noop": wrapped_noop,
            }
        )

    if identifier == "usage-profile-extended.database-membership":
        profiles = P.get_DB("__all__")
        members = [profile for profile in profiles if isinstance(profile, E)]
        return _returned(
            {
                "extended_count": len(members),
                "extended_names": [profile.name for profile in members],
                "total_count": len(profiles),
            }
        )
    if identifier == "usage-profile-extended.datapath":
        path = Path(E.datapath)
        return _returned(
            {
                "filename": path.name,
                "is_distinct_from_standard": path != Path(K.datapath),
                "sha256": sha256_file(path),
            }
        )
    if identifier == "usage-profile-extended.subclass-topology":
        return _returned(
            {
                "is_profile_subclass": issubclass(E, P),
                "is_usage_profile_subclass": issubclass(E, K),
                "mro_names": [item.__name__ for item in E.__mro__],
            }
        )

    if identifier == "usage-profile-database.alias-topology":
        first_key = next(iter(P._DB))
        return _returned(
            {
                "all_values_are_registry_values": all(
                    left is right
                    for left, right in zip(P.get_DB("__all__"), P._DB.values())
                ),
                "found_is_registry_value": P.get_DB(first_key) is P._DB[first_key],
                "registry_count": len(P._DB),
            }
        )
    if identifier == "usage-profile-database.mutable-registry":
        marker = object()
        key = "__ORACLE_TEMPORARY_PROFILE__"
        if key in P._DB:
            raise RuntimeError("The temporary UsageProfile registry key already exists.")
        try:
            P._DB[key] = marker
            observed = P.get_DB(key) is marker
            count_during = len(P._DB)
        finally:
            P._DB.pop(key, None)
        return _returned(
            {
                "count_after_restore": len(P._DB),
                "count_during_change": count_during,
                "temporary_value_was_observable": observed,
            }
        )
    if identifier == "usage-profile-database.type-topology":
        return _returned(
            {
                "database_attribute_is_shared": P._DB is K._DB is E._DB,
                "mro_names": [item.__name__ for item in P.__mro__],
                "profile_instances_in_registry": sum(
                    isinstance(item, P) for item in P._DB.values()
                ),
                "registry_count": len(P._DB),
            }
        )

    if identifier == "usage-profile-lookup.all":
        values = P.get_DB("__all__")
        dictionaries = P.get_DB("__all__", as_dict=True)
        return _returned(
            {
                "dictionary_key_orders": [list(item) for item in dictionaries],
                "identities_match_registry_order": all(
                    left is right for left, right in zip(values, P._DB.values())
                ),
                "names": [item.name for item in values],
                "value_count": len(values),
            }
        )
    if identifier == "usage-profile-lookup.found-and-path":
        key = next(iter(P._DB))
        found = P.get_DB(key)
        as_dict = P.get_DB(key, as_dict=True)
        paths = P.get_DB("__path__")
        return _returned(
            {
                "dictionary_key_order": list(as_dict),
                "found_is_registry_value": found is P._DB[key],
                "key": key,
                "path_filenames": [Path(item).name for item in paths],
                "path_count": len(paths),
            }
        )
    if identifier == "usage-profile-lookup.missing":
        key = "__MISSING_USAGE_PROFILE__"
        before = len(P._DB)
        try:
            P.get_DB(key)
        except Exception as exception:
            return _raised(
                exception,
                {"database_count_unchanged": len(P._DB) == before, "key": key},
            )
        raise RuntimeError("A missing UsageProfile lookup unexpectedly returned.")

    if identifier == "profile-csv.packaged-sources":
        return _returned(
            {
                "extended": _csv_descriptor(Path(E.datapath), read_csv_without_units),
                "standard": _csv_descriptor(Path(K.datapath), read_csv_without_units),
            }
        )
    if identifier == "profile-csv.strip-unit-headers":
        with tempfile.TemporaryDirectory(prefix="usage-profile-oracle-") as root:
            path = Path(root) / "headers.csv"
            path.write_text(
                "Alpha [kW],Beta[unit],Gamma\n1,2,3\n",
                encoding="utf-8",
                newline="\n",
            )
            frame = read_csv_without_units(path)
            return _returned(
                {
                    "columns": [str(item) for item in frame.columns],
                    "row_count": int(len(frame.index)),
                    "row_values": [int(item) for item in frame.iloc[0].tolist()],
                }
            )
    if identifier == "profile-csv.greedy-header-and-quotes":
        with tempfile.TemporaryDirectory(prefix="usage-profile-oracle-") as root:
            path = Path(root) / "greedy.csv"
            path.write_text(
                '"A [one] middle [two] suffix","Comma, Header [u]",Plain\n7,"x,y",9\n',
                encoding="utf-8",
                newline="\n",
            )
            frame = read_csv_without_units(path)
            return _returned(
                {
                    "columns": [str(item) for item in frame.columns],
                    "row_count": int(len(frame.index)),
                    "row_values": [str(item) for item in frame.iloc[0].tolist()],
                }
            )

    raise RuntimeError(f"Unknown UsageProfile core case {identifier!r}.")


def _require_exact_keys(value: Any, expected: set[str], context: str) -> None:
    if not isinstance(value, dict) or set(value) != expected:
        actual = sorted(value) if isinstance(value, dict) else type(value).__name__
        raise RuntimeError(
            f"{context} has an invalid key set: expected {sorted(expected)}, got {actual}."
        )


def _walk(value: Any) -> Any:
    yield value
    if isinstance(value, dict):
        for item in value.values():
            yield from _walk(item)
    elif isinstance(value, list):
        for item in value:
            yield from _walk(item)


def _validate_equivalent_facts(identifier: str, facts: dict[str, Any]) -> None:
    expected = EQUIVALENT_FACT_KEYS.get(identifier)
    if expected is None or set(facts) != expected:
        raise RuntimeError(f"Equivalent case {identifier!r} has an invalid fact key set.")
    pending: list[Any] = [facts]
    while pending:
        value = pending.pop()
        if isinstance(value, dict):
            for key, item in value.items():
                lowered = key.lower()
                if any(fragment in lowered for fragment in FORBIDDEN_EQUIVALENT_FACT_KEY_FRAGMENTS):
                    raise RuntimeError(
                        f"Equivalent case {identifier!r} exposes a Python-container-only fact."
                    )
                pending.append(item)
        elif isinstance(value, list):
            pending.extend(value)


def _validate_schedule_descriptor(item: Any) -> None:
    _require_exact_keys(item, SCHEDULE_DESCRIPTOR_KEYS, "Schedule descriptor")
    if item["value_count"] != 365 * 144:
        raise RuntimeError("A schedule descriptor has the wrong value count.")
    if item["values_encoding"] != "binary64-hex-without-prefix-lines":
        raise RuntimeError("A schedule descriptor has the wrong values encoding.")
    if not isinstance(item["idf_fields"], list) or len(item["idf_fields"]) < 2:
        raise RuntimeError("A schedule descriptor has no exact IDF fields.")
    _require_exact_keys(item["name"], {"policy", "value"}, "Schedule name descriptor")
    if item["name"]["policy"] not in {"literal", "tokenized-runtime-identities"}:
        raise RuntimeError("A schedule name descriptor has an invalid policy.")
    expected_type_limits = {
        "fraction": "ScheduleTypeLimits:Fraction",
        "onoff": "ScheduleTypeLimits:Onoff",
        "real": "ScheduleTypeLimits:Real",
        "temperature": "ScheduleTypeLimits:Temperature",
    }
    schedule_type = item["schedule_type"]
    if schedule_type not in expected_type_limits:
        raise RuntimeError("A schedule descriptor has an invalid schedule type.")
    if item["idf_fields"][:2] != [
        item["name"]["value"],
        expected_type_limits[schedule_type],
    ]:
        raise RuntimeError("A schedule descriptor name/type does not match its IDF fields.")
    if not isinstance(item["values_sha256"], str) or not re.fullmatch(
        r"sha256:[0-9a-f]{64}", item["values_sha256"]
    ):
        raise RuntimeError("A schedule descriptor has an invalid values hash.")


def _validate_schedule_descriptors(value: Any) -> None:
    for item in _walk(value):
        if isinstance(item, dict) and "values_sha256" in item:
            _validate_schedule_descriptor(item)


def _validate_converted_profile(value: Any, *, database_identity: bool) -> None:
    _require_exact_keys(value, CONVERTED_PROFILE_KEYS, "Converted profile descriptor")
    if not isinstance(value["schedules"], dict) or set(value["schedules"]) != set(SCHEDULE_SLOTS):
        raise RuntimeError("A converted profile does not contain seven ordered schedules.")
    for descriptor in value["schedules"].values():
        _validate_schedule_descriptor(descriptor)
    if not value["upstream_output_name_equals_source_identity"]:
        raise RuntimeError("The upstream converted name no longer equals its source ID.")
    _require_exact_keys(
        value["source_identity"],
        {"policy", "value"},
        "Converted profile source identity",
    )
    _require_exact_keys(value["output_name"], {"policy", "value"}, "Converted output name")
    if value["output_name"] != value["source_identity"]:
        raise RuntimeError("The Python converted output name and source identity differ.")
    output = value["native_output_identity"]
    if database_identity:
        _require_exact_keys(output, DATABASE_OUTPUT_IDENTITY_KEYS, "Database output identity")
        if value["source"] not in {"standard", "extended"}:
            raise RuntimeError("A database converted profile has an invalid source.")
        if output["adaptation"] != "deterministic-native-usage-profile-identity" or output["comparison"] != "native-only-output-id-equals-native-source-usage-profile-id" or output["python_counterpart"] != "absent":
            raise RuntimeError("A database output identity policy drifted.")
    else:
        _require_exact_keys(output, CUSTOM_OUTPUT_IDENTITY_KEYS, "Custom output identity")
        if value["source"] != "custom" or output["comparison"] != "native-only-output-id-equals-exact-source-usage-profile-id" or output["python_counterpart"] != "absent":
            raise RuntimeError("A custom output identity policy drifted.")


def _validate_to_dragon_facts(identifier: str, facts: dict[str, Any]) -> None:
    if facts["schedule_slots"] != list(SCHEDULE_SLOTS):
        raise RuntimeError(f"Equivalent case {identifier!r} schedule slot order drifted.")
    if identifier == "usage-profile-dragon.all-database-profiles":
        profiles = facts["profiles"]
        if not isinstance(profiles, list) or facts["profile_count"] != 24 or len(profiles) != 24:
            raise RuntimeError("The all-database conversion corpus is not exactly 24 profiles.")
        for profile in profiles:
            _validate_converted_profile(profile, database_identity=True)
        return
    profile = facts["profile"]
    _validate_converted_profile(profile, database_identity=False)
    if identifier == "usage-profile-dragon.lighting-tie":
        if facts["fractional_lighting_value_count"] != 522 or facts["fractional_lighting_values"] != [
            {"hex_without_prefix": "1.8000000000000p-1", "kind": "binary64"}
        ]:
            raise RuntimeError("The lighting tie no longer produces exact 0.75 values.")
    elif identifier == "usage-profile-dragon.overnight-vacation":
        if facts["overnight"] is not True or facts["vacation_count"] != 1:
            raise RuntimeError("The overnight-vacation conversion facts drifted.")
        wrapped = facts["wrapped_vacation_noop"]
        _require_exact_keys(
            wrapped,
            WRAPPED_VACATION_NOOP_KEYS,
            "Wrapped vacation no-op receipt",
        )
        if wrapped != {
            "end": "01/03",
            "schedule_slots_equal_without_vacation": list(SCHEDULE_SLOTS),
            "start": "12/29",
            "vacation_mask_positive_days": 0,
        }:
            raise RuntimeError("The wrapped vacation no-op contract drifted.")
        leap = facts["leap_day_failure"]
        _require_exact_keys(leap, PYTHON_RAISE_KEYS, "Leap-day failure receipt")
        _require_exact_keys(
            leap["facts"],
            LEAP_DAY_FAILURE_FACT_KEYS,
            "Leap-day failure facts",
        )
        if leap != {
            "error_category": "domain",
            "exception_type": "ValueError",
            "facts": {"end": "03/01", "start": "02/29"},
            "message": "day is out of range for month",
            "outcome": "raised",
        }:
            raise RuntimeError("The leap-day conversion failure contract drifted.")


def validate_oracle(value: dict[str, Any]) -> None:
    """Fail closed on the complete artifact before writing any bytes."""

    _require_exact_keys(value, ORACLE_KEYS, "UsageProfile core oracle top-level root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("The UsageProfile core oracle schema drifted.")
    upstream = value["upstream"]
    _require_exact_keys(upstream, UPSTREAM_KEYS, "UsageProfile upstream receipt")
    if upstream != {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "path": SOURCE_PATH,
        "source_sha256": EXPECTED_SOURCE_SHA256,
    }:
        raise RuntimeError("The UsageProfile upstream receipt is not exact.")
    runtime = value["runtime"]
    _require_exact_keys(runtime, RUNTIME_KEYS, "UsageProfile runtime receipt")
    if runtime != {
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }:
        raise RuntimeError("The UsageProfile runtime receipt is not exact.")

    symbols = value["symbols"]
    if not isinstance(symbols, list) or len(symbols) != len(TARGET_SYMBOLS):
        raise RuntimeError("The UsageProfile symbol receipt count is not exact.")
    for expected_symbol, receipt in zip(TARGET_SYMBOLS, symbols, strict=True):
        _require_exact_keys(receipt, SYMBOL_KEYS, f"Symbol receipt {expected_symbol!r}")
        if receipt != {
            **EXPECTED_SYMBOL_RECEIPTS[expected_symbol],
            "path": SOURCE_PATH,
            "symbol": expected_symbol,
        }:
            raise RuntimeError(f"Symbol receipt {expected_symbol!r} is not exact.")

    definitions = case_definitions()
    definitions_by_id = {item["id"]: item for item in definitions}
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("The UsageProfile oracle case count is not exact.")
    if [case.get("id") for case in cases if isinstance(case, dict)] != [
        item["id"] for item in definitions
    ]:
        raise RuntimeError("The UsageProfile oracle case order drifted.")

    for case in cases:
        if not isinstance(case, dict):
            raise RuntimeError("A UsageProfile oracle case is not an object.")
        identifier = case.get("id")
        if identifier not in definitions_by_id:
            raise RuntimeError(f"Unknown UsageProfile oracle case {identifier!r}.")
        definition = definitions_by_id[identifier]
        expected_keys = CASE_KEYS
        if definition["expected_dotnet"] is not None:
            expected_keys = expected_keys | {"expected_dotnet"}
        _require_exact_keys(case, expected_keys, f"Oracle case {identifier!r}")
        if case["executor"] != definition["executor"] or case["symbol"] != definition["symbol"]:
            raise RuntimeError(f"Case {identifier!r} binding drifted.")
        if definition["expected_dotnet"] is not None and case["expected_dotnet"] != definition["expected_dotnet"]:
            raise RuntimeError(f"Case {identifier!r} native expectation drifted.")
        observation = case["python"]
        if not isinstance(observation, dict):
            raise RuntimeError(f"Case {identifier!r} Python receipt is not an object.")
        expected_python_outcome = (
            "raised" if identifier == "usage-profile-lookup.missing" else "returned"
        )
        if observation.get("outcome") != expected_python_outcome:
            raise RuntimeError(f"Case {identifier!r} Python outcome drifted.")
        if observation.get("outcome") == "returned":
            _require_exact_keys(observation, PYTHON_RETURN_KEYS, f"Case {identifier!r} Python return receipt")
        elif observation.get("outcome") == "raised":
            _require_exact_keys(observation, PYTHON_RAISE_KEYS, f"Case {identifier!r} Python error receipt")
            if observation["error_category"] not in {"domain", "range", "type"}:
                raise RuntimeError(f"Case {identifier!r} has an invalid Python error category.")
            if not isinstance(observation["exception_type"], str) or not observation["exception_type"]:
                raise RuntimeError(f"Case {identifier!r} has an invalid exception type.")
            if not isinstance(observation["message"], str):
                raise RuntimeError(f"Case {identifier!r} has an invalid exception message.")
        else:
            raise RuntimeError(f"Case {identifier!r} has an invalid Python outcome.")
        if not isinstance(observation["facts"], dict):
            raise RuntimeError(f"Case {identifier!r} facts are not an object.")
        if case["symbol"] in EXPECTED_EQUIVALENT_SYMBOLS:
            _validate_equivalent_facts(identifier, observation["facts"])
        if identifier.startswith("usage-profile-dragon."):
            _validate_to_dragon_facts(identifier, observation["facts"])
        _validate_schedule_descriptors(observation["facts"])

    if value["cases_sha256"] != cases_sha256(cases):
        raise RuntimeError("The UsageProfile cases hash is invalid.")
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
        raise RuntimeError("The UsageProfile consumer contract drifted.")
    serialized = strict_json_dumps(value)
    if RAW_RUNTIME_IDENTITY_PATTERN.search(serialized):
        raise RuntimeError("A raw runtime identity entered the UsageProfile oracle.")


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import epsimple.core.profile as profile_module
    from epsimple.core.profile import (
        KoreanUsageProfile,
        KoreanUsageProfileExtended,
        Profile,
        read_csv_without_units,
    )

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported UsageProfile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported UsageProfile module is not the inventoried source.")
    profiles = Profile.get_DB("__all__")
    if len(profiles) != 24 or len(Profile._DB) != 24:
        raise SystemExit("The pinned UsageProfile database does not contain exactly 24 profiles.")
    if any(len(getattr(profile.to_dragon(), slot)) != 365 for profile in profiles[:1] for slot in SCHEDULE_SLOTS):
        raise SystemExit("The pinned converted schedule annual grid is not exact.")

    definitions = case_definitions()
    cases: list[dict[str, Any]] = []
    for definition in definitions:
        observation = _execute(
            definition["id"],
            KoreanUsageProfile,
            KoreanUsageProfileExtended,
            Profile,
            read_csv_without_units,
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
        raise SystemExit("Exact CPython 3.12.7 is required for the UsageProfile core oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    serialized = strict_json_dumps(result, indent=2) + "\n"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(serialized, encoding="utf-8", newline="\n")
    print(f"Wrote UsageProfile core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

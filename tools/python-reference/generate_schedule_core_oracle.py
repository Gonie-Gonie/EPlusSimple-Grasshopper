"""Generate pinned observations for the remaining annual ``Schedule`` API.

This corpus deliberately excludes the algebraic operations covered by
``generate_schedule_operations_oracle.py``.  It records constructors, mutation
semantics, identity topology, metrics, compact-period utilities, summary text,
and EnergyPlus ``Schedule:Compact`` serialization for EPlusSimple 0.7.0.

Run the generator through ``bootstrap_reference.py`` with CPython 3.12.7,
``PYTHONHASHSEED=0``, the pinned dependency root, and the pinned upstream source.
Runtime ``hex(id(...))`` names are represented by stable identity-group tokens;
raw process identities are never allowed into JSON.
"""

from __future__ import annotations

import argparse
from collections import Counter
from collections.abc import Callable, Iterable
import copy
import datetime
import importlib.util
import math
import os
from pathlib import Path
import re
import sys
from typing import Any


SCHEMA = "dragons.invisibledragon.schedule-core-oracle.v1"
SOURCE_PATH = "src/idragon/dragon/profile.py"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
EXPECTED_SOURCE_SHA256 = (
    "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"
)
EXPECTED_SYMBOL_HASHES = {
    "Schedule.FIXED_LENGTH": "sha256:60d994b214a3939f0fb0a15f398a1198ef3ef96416327199e5b8b8be5ba9f598",
    "Schedule.TIME_TUPLE": "sha256:e175d235cac1a4c1ad2f2f06b27f6df1ee8dfcafa93bcba1482d4c9fc3a823a3",
    "Schedule.__deepcopy__": "sha256:be9a64938799225409f7b10083e2fcae187eb2bae01151a68f21305ffd240a7d",
    "Schedule.__init__": "sha256:72d34a65bd7c9b82f9962da98b6ec5e1496459918de625fb8e2126c9832ddf06",
    "Schedule.apply": "sha256:cac23120005e2cac2c4729c70471a7796840160338fff91e1c49e9670f763ba9",
    "Schedule.astype": "sha256:3c3e1ad91d7a933d4d60c38cab8d9b0ef5ed28f1b036eb23f3c9961115df2c07",
    "Schedule.average": "sha256:e5a1cd49cc7fd4ceff37a6ec7f39d72c7269e769e351d6c3b4e46a2fbd3fa9e0",
    "Schedule.clip": "sha256:a5c9474c7c676e8512720ed02a0f4f112e0e7380c5e2d3cc19e22d3aee38dec2",
    "Schedule.compactize": "sha256:47d2d3d2edf795f4d2d532bd242e1f6497df6085dec0bc7ec0a6deeff74ae470",
    "Schedule.dayschedules": "sha256:61806264198f15d60f0113d4c0aa9bc2dd6ffa4d7d9d719399297c90d4efe1e5",
    "Schedule.from_compact": "sha256:ce943fca5d32b9a2c538b68eca8edd3e9fd16f63f9fc7dc7847ff7695719622b",
    "Schedule.from_constant": "sha256:921474e6c535cf86b9c5452f828a1ee00c0a9aafbafc8b64ea9d3a924f3242fd",
    "Schedule.from_windows": "sha256:95346844e1f1e0554287cd904a0b24ef17522ccb1099007fbb3b72cfd2703a2d",
    "Schedule.integral": "sha256:ef9cd611a4831abe92322e26ea5be6be6c185f9afc07efc8560ca4818e20c254",
    "Schedule.max": "sha256:5b932882346fc3af953b9bf3695807e819437b1938e15ea9dd62d89548c5a66b",
    "Schedule.min": "sha256:788223628b6747bd445c617bbc83e40062f57ac8168a06908c67dff054d25771",
    "Schedule.positive_average": "sha256:8c464f8c2937679875bcb851700d359c029c7a9d6480c138a15cbb82ea3cfc2a",
    "Schedule.summary": "sha256:6ccaf08dce837f4e10048cdd029bde79236ef48b0731d455abd213844e9b7118",
    "Schedule.to_idf_object": "sha256:afa76bbb026a6b79b79602918b37f93ad3f936239cbd7b57e3a037d59e8b30fc",
    "Schedule.type": "sha256:2819b394ba739818561d80cd1f770484a9e012fb106d62d66ed4cb53c35d1c7b",
    "Schedule.unify_compactized_schedules": "sha256:6f7741b799e71d8d5f0f180a0d9ade68fe4d3a86bf0fde346f34a57b287c8231",
    "Schedule.unify_compactized_schedules_many": "sha256:51d9dbc95d51184a2b61120e8501128d2c9dd5cac33bee6020b0695b0c657788",
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_HASHES)
EXPECTED_CASE_COUNT = 104
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64
ANNUAL_LENGTH = 365
DEFAULT_YEAR = 2026
CONSUMER_CULTURE = "fr-FR"

CONSUMER_CONTRACT = {
    "annual_length": ANNUAL_LENGTH,
    "culture": CONSUMER_CULTURE,
    "date_grid": f"{DEFAULT_YEAR}-non-leap-inclusive",
    "float_encoding": "json-number-plus-python-binary64-hex-without-0x-prefix",
    "identity_encoding": "case-local-type-scoped-first-observation-groups",
    "idf_observation_scope": {
        "excluded": (
            "rendered-IdfObject-text-escaping-and-sanitization; "
            "covered-by-the-separate-IdfObject-serializer-contract"
        ),
        "included": (
            "raw-logical-Schedule:Compact-object-type-field-order-field-values-"
            "and-extended-input"
        ),
    },
    "native_container_mappings": {
        "Schedule.dayschedules": {
            "dotnet": "fresh-read-only-collection-on-every-property-access",
            "preserved": "length-order-and-DaySchedule-reference-identity",
            "python": "fresh-mutable-list-on-every-property-access",
        },
        "Schedule.to_idf_object": {
            "dotnet": "one-contiguous-IdfObject.Fields-logical-value-sequence",
            "fixture_validation_metadata": (
                "python-field-names-and-primary-extension-boundary-only; "
                "native-IdfObject-has-no-separate-extended-collection"
            ),
            "normalization": "only-trailing-null-primary-slots-may-be-omitted",
            "preserved": (
                "object-type-exact-non-null-primary-prefix-in-field-position-order-"
                "and-exact-extension-continuation"
            ),
            "python": (
                "ordered-fixed-153-primary-data-entries-plus-ordered-extended_input"
            ),
        },
    },
    "period_endpoints": "inclusive-iso-date",
    "runtime_names": "normalized-identity-linked-segments",
}

IDF_PRIMARY_FIELD_NAMES = (
    "Name",
    "Schedule Type Limits Name",
) + tuple(f"Field {index}" for index in range(1, 152))
IDF_UPSTREAM_DATA_KEYS = (
    "Name",
    "Schedule Type Limits Name",
) + tuple(f"Field {index}" for index in range(1, 151)) + ("",)
IDF_CASE_SHAPES = {
    "idf.constant-real": {"extended_count": 0, "primary_non_null_count": 12},
    "idf.default-expanded-fields": {
        "extended_count": 3499,
        "primary_non_null_count": 153,
    },
    "idf.multiple-periods": {"extended_count": 0, "primary_non_null_count": 22},
    "idf.rich-overrides": {"extended_count": 0, "primary_non_null_count": 32},
}

FLOAT_MAX = float.fromhex("0x1.fffffffffffffp+1023")
FLOAT_MIN_SUBNORMAL = float.fromhex("0x0.0000000000001p-1022")
AUTO_NAME_PATTERN = re.compile(r"^0x[0-9a-f]+$")
RAW_AUTO_NAME_PATTERN = re.compile(r"(?<![0-9A-Za-z])0x[0-9a-f]+(?![0-9A-Za-z])")

ADAPTATION_IDS = frozenset(
    {
        "deterministic-schedule-from-constant-child-names",
        "immutable-deterministic-schedule-construction",
        "immutable-schedule-apply",
        "immutable-schedule-astype",
        "immutable-schedule-clip",
        "immutable-schedule-time-tuple",
        "native-schedule-deepcopy-memo",
        "validated-deterministic-schedule-from-compact",
        "validated-deterministic-schedule-from-windows",
        "validated-schedule-unify-coverage",
    }
)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "Schedule.TIME_TUPLE": "immutable-schedule-time-tuple",
    "Schedule.__deepcopy__": "native-schedule-deepcopy-memo",
    "Schedule.__init__": "immutable-deterministic-schedule-construction",
    "Schedule.apply": "immutable-schedule-apply",
    "Schedule.astype": "immutable-schedule-astype",
    "Schedule.clip": "immutable-schedule-clip",
    "Schedule.from_compact": "validated-deterministic-schedule-from-compact",
    "Schedule.from_constant": "deterministic-schedule-from-constant-child-names",
    "Schedule.from_windows": "validated-deterministic-schedule-from-windows",
    "Schedule.unify_compactized_schedules": "validated-schedule-unify-coverage",
}
EXPECTED_EQUIVALENT_SYMBOLS = frozenset(TARGET_SYMBOLS) - frozenset(
    EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
)


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_schedule_operations_oracle.py")
    spec = importlib.util.spec_from_file_location(
        "_dragons_schedule_operations_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load Schedule operation support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or module.EXPECTED_SOURCE_SHA256 != EXPECTED_SOURCE_SHA256
        or module.EXPECTED_CASE_COUNT != 329
    ):
        raise RuntimeError("Schedule operation support is not the pinned corpus.")
    return module


OPS = _load_support()
strict_json_dumps = OPS.strict_json_dumps
canonical_sha256 = OPS.canonical_sha256
sha256_file = OPS.sha256_file
load_json_without_duplicates = OPS.load_json_without_duplicates
SLOT_KEYS = OPS.SLOT_KEYS


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    """Validate the complete inventory, then bind exactly these 22 symbols."""

    base = OPS.RULE.DAY.load_exact_inventory(path, upstream_commit)
    inventory = load_json_without_duplicates(path)
    target_symbols = [
        item
        for item in inventory["symbols"]
        if item["path"] == SOURCE_PATH and item["symbol"] in TARGET_SYMBOLS
    ]
    if [item["symbol"] for item in target_symbols] != list(TARGET_SYMBOLS):
        raise SystemExit("The inventory does not exactly cover the 22 Schedule symbols.")
    for item in target_symbols:
        if item["symbol_hash"] != EXPECTED_SYMBOL_HASHES[item["symbol"]]:
            raise SystemExit(f"The inventory hash for {item['symbol']} is not pinned.")
    return {
        "content_sha256": base["content_sha256"],
        "file": base["file"],
        "symbols": target_symbols,
    }


def _dotnet(
    adaptation: str,
    outcome: str,
    policy: str,
    *,
    error_category: str | None = None,
) -> dict[str, str]:
    if adaptation not in ADAPTATION_IDS:
        raise RuntimeError(f"Unknown Schedule core adaptation {adaptation!r}.")
    if outcome not in {"raised", "returned"}:
        raise RuntimeError(f"Unknown .NET outcome {outcome!r}.")
    value = {"adaptation": adaptation, "outcome": outcome, "policy": policy}
    if error_category is not None:
        value["error_category"] = error_category
    return value


def _definition(
    identifier: str,
    symbol: str,
    *,
    expected_exception: str | None = None,
    expected_dotnet: dict[str, str] | None = None,
) -> dict[str, Any]:
    return {
        "expected_dotnet": expected_dotnet,
        "expected_exception": expected_exception,
        "id": identifier,
        "symbol": symbol,
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    construction = _dotnet(
        "immutable-deterministic-schedule-construction",
        "returned",
        "immutable-input-list-and-stable-anonymous-object-graph-names",
    )
    apply_returned = _dotnet(
        "immutable-schedule-apply",
        "returned",
        "return-new-and-preserve-source-graph",
    )
    astype_raised = _dotnet(
        "immutable-schedule-astype",
        "raised",
        "atomic-failure-preserves-source-graph",
        error_category="domain",
    )
    apply_foreign_year = _dotnet(
        "immutable-schedule-apply",
        "returned",
        "normalize-foreign-year-by-month-day",
    )
    apply_raised = _dotnet(
        "immutable-schedule-apply",
        "raised",
        "reject-reversed-or-type-mismatched-update",
        error_category="domain",
    )

    definitions = [
        _definition("constant.fixed-length", "Schedule.FIXED_LENGTH"),
        _definition(
            "constant.time-tuple",
            "Schedule.TIME_TUPLE",
            expected_dotnet=_dotnet(
                "immutable-schedule-time-tuple",
                "returned",
                "read-only-annual-date-grid",
            ),
        ),
        _definition("init.anonymous", "Schedule.__init__", expected_dotnet=construction),
        _definition("init.default-fraction", "Schedule.__init__", expected_dotnet=construction),
        _definition("init.default-real", "Schedule.__init__", expected_dotnet=construction),
        _definition(
            "init.empty-name",
            "Schedule.__init__",
            expected_dotnet=_dotnet(
                "immutable-deterministic-schedule-construction",
                "raised",
                "reject-empty-product-name",
                error_category="domain",
            ),
        ),
        _definition(
            "init.explicit-type-mismatch",
            "Schedule.__init__",
            expected_exception="ValueError",
        ),
        _definition("init.invalid-item", "Schedule.__init__", expected_exception="TypeError"),
        _definition("init.invalid-length", "Schedule.__init__", expected_exception="ValueError"),
        _definition("init.mixed-types", "Schedule.__init__", expected_exception="ValueError"),
        _definition(
            "init.surrounding-space-name",
            "Schedule.__init__",
            expected_dotnet=_dotnet(
                "immutable-deterministic-schedule-construction",
                "returned",
                "trim-surrounding-product-name-whitespace",
            ),
        ),
        _definition("init.supplied-list-alias", "Schedule.__init__", expected_dotnet=construction),
        _definition(
            "init.whitespace-name",
            "Schedule.__init__",
            expected_dotnet=_dotnet(
                "immutable-deterministic-schedule-construction",
                "raised",
                "reject-whitespace-only-product-name",
                error_category="domain",
            ),
        ),
        _definition(
            "apply.foreign-year-noop",
            "Schedule.apply",
            expected_dotnet=apply_foreign_year,
        ),
        _definition("apply.inplace-inclusive-mmdd", "Schedule.apply", expected_dotnet=apply_returned),
        _definition(
            "apply.invalid-date",
            "Schedule.apply",
            expected_exception="TypeError",
            expected_dotnet=_dotnet(
                "immutable-schedule-apply",
                "raised",
                "reject-invalid-date-format",
                error_category="domain",
            ),
        ),
        _definition(
            "apply.noninplace-deepcopy",
            "Schedule.apply",
            expected_dotnet=_dotnet(
                "immutable-schedule-apply",
                "returned",
                "preserve-native-name-and-unchanged-source-child-references",
            ),
        ),
        _definition("apply.outside-year-noop", "Schedule.apply", expected_dotnet=apply_foreign_year),
        _definition("apply.parse-digit-pair", "Schedule.apply", expected_dotnet=apply_returned),
        _definition("apply.parse-yyyymmdd", "Schedule.apply", expected_dotnet=apply_returned),
        _definition("apply.reversed-noop", "Schedule.apply", expected_dotnet=apply_raised),
        _definition("apply.type-unchecked", "Schedule.apply", expected_dotnet=apply_raised),
        _definition(
            "deepcopy.memo-hit",
            "Schedule.__deepcopy__",
            expected_dotnet=_dotnet(
                "native-schedule-deepcopy-memo",
                "returned",
                "native-copy-does-not-expose-python-memo",
            ),
        ),
        _definition("deepcopy.noncontiguous-alias-split", "Schedule.__deepcopy__"),
        _definition("deepcopy.shared-period", "Schedule.__deepcopy__"),
        _definition("astype.inplace-partial", "Schedule.astype", expected_exception="ValueError", expected_dotnet=astype_raised),
        _definition(
            "astype.inplace-stale",
            "Schedule.astype",
            expected_dotnet=_dotnet(
                "immutable-schedule-astype",
                "returned",
                "return-new-with-consistent-container-and-child-types",
            ),
        ),
        _definition("astype.outplace", "Schedule.astype"),
        _definition("astype.outplace-failure-atomic", "Schedule.astype", expected_exception="ValueError"),
        _definition("average.catastrophic", "Schedule.average"),
        _definition("average.minimum-subnormal", "Schedule.average"),
        _definition("average.negative-zero", "Schedule.average"),
        _definition("clip.empty-name-default", "Schedule.clip"),
        _definition(
            "clip.inplace-distinct",
            "Schedule.clip",
            expected_dotnet=_dotnet(
                "immutable-schedule-clip",
                "returned",
                "return-new-and-preserve-source-graph",
            ),
        ),
        _definition(
            "clip.inplace-partial",
            "Schedule.clip",
            expected_exception="ValueError",
            expected_dotnet=_dotnet(
                "immutable-schedule-clip",
                "raised",
                "atomic-failure-preserves-source-graph",
                error_category="domain",
            ),
        ),
        _definition(
            "clip.min-greater-than-max",
            "Schedule.clip",
            expected_dotnet=_dotnet(
                "immutable-schedule-clip",
                "raised",
                "reject-inverted-clip-bounds",
                error_category="domain",
            ),
        ),
        _definition("clip.outplace-bounds", "Schedule.clip"),
        _definition("clip.outplace-lower-only", "Schedule.clip"),
        _definition("clip.outplace-no-bounds-copy", "Schedule.clip"),
        _definition("clip.outplace-upper-only", "Schedule.clip"),
        _definition("compactize.default-distinct", "Schedule.compactize"),
        _definition("compactize.equal-distinct", "Schedule.compactize"),
        _definition("compactize.full-run", "Schedule.compactize"),
        _definition("compactize.identity-runs", "Schedule.compactize"),
        _definition("dayschedules.fresh-list", "Schedule.dayschedules"),
        _definition("dayschedules.weekday-overrides", "Schedule.dayschedules"),
        _definition(
            "from-compact.distinct-equal-adjacent",
            "Schedule.from_compact",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-compact",
                "returned",
                "stable-gap-names-and-preserved-reference-periods",
            ),
        ),
        _definition("from-compact.empty", "Schedule.from_compact", expected_exception="ValueError"),
        _definition(
            "from-compact.leap-day",
            "Schedule.from_compact",
            expected_exception="ValueError",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-compact",
                "raised",
                "reject-invalid-fixed-calendar-leap-day",
                error_category="domain",
            ),
        ),
        _definition("from-compact.mixed-type", "Schedule.from_compact", expected_exception="ValueError"),
        _definition(
            "from-compact.outside-noop",
            "Schedule.from_compact",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-compact",
                "returned",
                "normalize-foreign-year-by-month-day",
            ),
        ),
        _definition(
            "from-compact.overlap-later-wins",
            "Schedule.from_compact",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-compact",
                "returned",
                "stable-gap-names-and-later-window-wins",
            ),
        ),
        _definition(
            "from-compact.reversed-noop",
            "Schedule.from_compact",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-compact",
                "raised",
                "reject-reversed-period",
                error_category="domain",
            ),
        ),
        _definition(
            "from-compact.same-ref-adjacent",
            "Schedule.from_compact",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-compact",
                "returned",
                "stable-gap-names-and-preserved-reference-periods",
            ),
        ),
        _definition(
            "from-compact.single-gap",
            "Schedule.from_compact",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-compact",
                "returned",
                "stable-distinct-gap-object-names",
            ),
        ),
        _definition(
            "from-constant.anonymous",
            "Schedule.from_constant",
            expected_dotnet=_dotnet(
                "deterministic-schedule-from-constant-child-names",
                "returned",
                "stable-schedule-ruleset-and-day-names",
            ),
        ),
        _definition(
            "from-constant.bool",
            "Schedule.from_constant",
            expected_dotnet=_dotnet(
                "deterministic-schedule-from-constant-child-names",
                "returned",
                "stable-ruleset-and-day-names",
            ),
        ),
        _definition(
            "from-constant.day-explicit-type-ignored",
            "Schedule.from_constant",
            expected_dotnet=_dotnet(
                "deterministic-schedule-from-constant-child-names",
                "returned",
                "stable-wrapper-ruleset-name",
            ),
        ),
        _definition(
            "from-constant.ruleset-explicit-type-ignored",
            "Schedule.from_constant",
        ),
        _definition(
            "from-constant.real-nan",
            "Schedule.from_constant",
            expected_dotnet=_dotnet(
                "deterministic-schedule-from-constant-child-names",
                "raised",
                "reject-nonfinite-real-scalar-at-native-boundary",
                error_category="domain",
            ),
        ),
        _definition(
            "from-constant.scalar",
            "Schedule.from_constant",
            expected_dotnet=_dotnet(
                "deterministic-schedule-from-constant-child-names",
                "returned",
                "stable-ruleset-and-day-names",
            ),
        ),
        _definition(
            "from-constant.surrounding-space-name",
            "Schedule.from_constant",
            expected_dotnet=_dotnet(
                "deterministic-schedule-from-constant-child-names",
                "returned",
                "trim-name-and-use-stable-derived-child-names",
            ),
        ),
        _definition(
            "from-constant.unsupported-object",
            "Schedule.from_constant",
            expected_exception="TypeError",
            expected_dotnet=_dotnet(
                "deterministic-schedule-from-constant-child-names",
                "raised",
                "explicit-unsupported-constant-operand-error",
                error_category="type",
            ),
        ),
        _definition(
            "from-windows.day-alias",
            "Schedule.from_windows",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "returned",
                "stable-wrapper-names-and-preserved-day-aliases",
            ),
        ),
        _definition(
            "from-windows.empty",
            "Schedule.from_windows",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "returned",
                "stable-default-child-names-and-empty-window-topology",
            ),
        ),
        _definition(
            "from-windows.leap-day",
            "Schedule.from_windows",
            expected_exception="ValueError",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "raised",
                "reject-invalid-fixed-calendar-leap-day",
                error_category="domain",
            ),
        ),
        _definition(
            "from-windows.repeated-day-wrappers",
            "Schedule.from_windows",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "returned",
                "distinct-stable-wrappers-preserve-repeated-day-aliases",
            ),
        ),
        _definition(
            "from-windows.reversed-noop",
            "Schedule.from_windows",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "raised",
                "reject-reversed-window",
                error_category="domain",
            ),
        ),
        _definition(
            "from-windows.ruleset-alias",
            "Schedule.from_windows",
        ),
        _definition(
            "from-windows.scalar-overlap",
            "Schedule.from_windows",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "returned",
                "stable-window-child-names-and-later-window-wins",
            ),
        ),
        _definition(
            "from-windows.repeated-scalar-wrappers",
            "Schedule.from_windows",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "returned",
                "distinct-stable-wrapper-graphs-for-repeated-scalars",
            ),
        ),
        _definition(
            "from-windows.scalar-positive-infinity",
            "Schedule.from_windows",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "raised",
                "reject-nonfinite-real-window-scalar-at-native-boundary",
                error_category="domain",
            ),
        ),
        _definition("from-windows.type-mismatch", "Schedule.from_windows", expected_exception="ValueError"),
        _definition(
            "from-windows.unsupported-object",
            "Schedule.from_windows",
            expected_exception="TypeError",
            expected_dotnet=_dotnet(
                "validated-deterministic-schedule-from-windows",
                "raised",
                "explicit-unsupported-window-operand-error",
                error_category="type",
            ),
        ),
        _definition("idf.constant-real", "Schedule.to_idf_object"),
        _definition("idf.default-expanded-fields", "Schedule.to_idf_object"),
        _definition("idf.multiple-periods", "Schedule.to_idf_object"),
        _definition("idf.rich-overrides", "Schedule.to_idf_object"),
        _definition("integral.catastrophic", "Schedule.integral"),
        _definition("integral.minimum-subnormal", "Schedule.integral"),
        _definition("integral.overflow", "Schedule.integral"),
        _definition("max.negative-zero", "Schedule.max"),
        _definition("max.unused-holiday", "Schedule.max"),
        _definition("min.negative-zero", "Schedule.min"),
        _definition("min.unused-holiday", "Schedule.min"),
        _definition("positive-average.catastrophic", "Schedule.positive_average"),
        _definition("positive-average.minimum-subnormal", "Schedule.positive_average"),
        _definition("positive-average.none", "Schedule.positive_average"),
        _definition("summary.exact-rich", "Schedule.summary"),
        _definition("summary.invalid-period-limit", "Schedule.summary", expected_exception="TypeError"),
        _definition("summary.negative-period-limit", "Schedule.summary"),
        _definition("summary.zero-period-limit", "Schedule.summary"),
        _definition("type.normal", "Schedule.type"),
        _definition("type.explicit-fraction", "Schedule.type"),
        _definition("unify-many.asymmetric-three", "Schedule.unify_compactized_schedules_many"),
        _definition("unify-many.first-overlap-wins", "Schedule.unify_compactized_schedules_many"),
        _definition("unify-many.missing-coverage", "Schedule.unify_compactized_schedules_many", expected_exception="ValueError"),
        _definition("unify-many.one-empty", "Schedule.unify_compactized_schedules_many"),
        _definition("unify-many.zero", "Schedule.unify_compactized_schedules_many"),
        _definition("unify-pair.asymmetric", "Schedule.unify_compactized_schedules"),
        _definition("unify-pair.empty", "Schedule.unify_compactized_schedules"),
        _definition("unify-pair.first-overlap-wins", "Schedule.unify_compactized_schedules"),
        _definition(
            "unify-pair.interior-gap",
            "Schedule.unify_compactized_schedules",
            expected_dotnet=_dotnet(
                "validated-schedule-unify-coverage",
                "raised",
                "reject-interior-uncovered-unified-period",
                error_category="domain",
            ),
        ),
        _definition(
            "unify-pair.missing-coverage",
            "Schedule.unify_compactized_schedules",
            expected_dotnet=_dotnet(
                "validated-schedule-unify-coverage",
                "raised",
                "reject-uncovered-unified-period",
                error_category="domain",
            ),
        ),
    ]
    definitions.sort(key=lambda item: item["id"])
    validate_case_definitions(definitions)
    return tuple(definitions)


def validate_case_definitions(definitions: list[dict[str, Any]]) -> None:
    if len(definitions) != EXPECTED_CASE_COUNT:
        raise RuntimeError(
            f"Expected {EXPECTED_CASE_COUNT} Schedule core cases, got {len(definitions)}."
        )
    identifiers = [item["id"] for item in definitions]
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("Schedule core case identifiers are not unique and sorted.")
    if {item["symbol"] for item in definitions} != set(TARGET_SYMBOLS):
        raise RuntimeError("Schedule core cases do not cover exactly 22 symbols.")
    for item in definitions:
        if set(item) != {"expected_dotnet", "expected_exception", "id", "symbol"}:
            raise RuntimeError(f"Case {item.get('id')!r} has unexpected keys.")
        if item["symbol"] not in TARGET_SYMBOLS:
            raise RuntimeError(f"Case {item['id']!r} targets an unknown symbol.")
        expected_exception = item["expected_exception"]
        if expected_exception is not None and expected_exception not in {
            "TypeError",
            "ValueError",
        }:
            raise RuntimeError(f"Case {item['id']!r} has an invalid exception type.")
        expectation = item["expected_dotnet"]
        if expectation is None:
            continue
        required = {"adaptation", "outcome", "policy"}
        if set(expectation) not in (required, required | {"error_category"}):
            raise RuntimeError(f"Case {item['id']!r} has malformed .NET metadata.")
        if expectation["adaptation"] not in ADAPTATION_IDS:
            raise RuntimeError(f"Case {item['id']!r} has an unknown adaptation.")
        if (
            EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(item["symbol"])
            != expectation["adaptation"]
        ):
            raise RuntimeError(
                f"Case {item['id']!r} adaptation is not symbol-bound."
            )
        if expectation["outcome"] not in {"raised", "returned"}:
            raise RuntimeError(f"Case {item['id']!r} has an invalid .NET outcome.")
        if expectation["outcome"] == "raised":
            if expectation.get("error_category") not in {"domain", "type"}:
                raise RuntimeError(f"Case {item['id']!r} has an invalid error category.")
        elif "error_category" in expectation:
            raise RuntimeError(f"Case {item['id']!r} has an invalid error category.")
    actual_adaptations = {
        item["expected_dotnet"]["adaptation"]
        for item in definitions
        if item["expected_dotnet"] is not None
    }
    if actual_adaptations != ADAPTATION_IDS:
        raise RuntimeError("Schedule core cases do not bind exactly ten adaptations.")
    adapted_symbols = {
        item["symbol"]
        for item in definitions
        if item["expected_dotnet"] is not None
    }
    if adapted_symbols != set(EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS):
        raise RuntimeError("Schedule core cases do not bind exactly ten exception symbols.")


class Scenario:
    def __init__(self, inputs: dict[str, Any], action: Callable[[], Any]) -> None:
        self.inputs = inputs
        self.action = action


class IdentityNormalizer:
    """Assign deterministic, type-scoped identity groups within one case."""

    def __init__(self) -> None:
        self._groups: dict[int, str] = {}
        self._hex_to_group: dict[str, str] = {}
        self._counts: Counter[str] = Counter()

    def identity(self, value: Any, kind: str) -> str:
        key = id(value)
        existing = self._groups.get(key)
        if existing is not None:
            return existing
        group = f"{kind}:{self._counts[kind]}"
        self._counts[kind] += 1
        self._groups[key] = group
        self._hex_to_group[hex(key)] = group
        return group

    def name(self, value: Any, owner: Any, kind: str) -> dict[str, Any]:
        if not isinstance(value, str):
            raise RuntimeError(f"A {kind} name is not text.")
        owner_group = self.identity(owner, kind)
        if value == hex(id(owner)):
            return {
                "identity_group": owner_group,
                "policy": "runtime-identity-hex",
            }
        return {
            "policy": "literal-with-normalized-runtime-identities",
            "segments": self.text_segments(value),
        }

    def text_segments(self, value: str) -> list[dict[str, str]]:
        segments: list[dict[str, str]] = []
        cursor = 0
        for match in RAW_AUTO_NAME_PATTERN.finditer(value):
            if match.start() > cursor:
                segments.append({"kind": "literal", "value": value[cursor : match.start()]})
            token = match.group(0)
            segments.append(
                {
                    "kind": "runtime-identity",
                    "value": self._hex_to_group.get(token, "unbound-runtime-identity"),
                }
            )
            cursor = match.end()
        if cursor < len(value):
            segments.append({"kind": "literal", "value": value[cursor:]})
        if not segments:
            segments.append({"kind": "literal", "value": ""})
        return segments

    def normalized_text(self, value: str) -> str:
        parts: list[str] = []
        for segment in self.text_segments(value):
            if segment["kind"] == "literal":
                parts.append(segment["value"])
            else:
                parts.append("{" + segment["value"] + "}")
        return "".join(parts)


def scalar_descriptor(value: Any) -> dict[str, Any]:
    if type(value) is bool:
        return {"kind": "bool", "value": value}
    if type(value) is int:
        return {"kind": "int", "value": value}
    if type(value) is float:
        if math.isnan(value):
            return {"kind": "nonfinite", "value": "nan"}
        if math.isinf(value):
            return {
                "kind": "nonfinite",
                "value": "positive-infinity" if value > 0 else "negative-infinity",
            }
        # Omit Python's ``0x`` marker so binary64 evidence can never be
        # mistaken for an unnormalized runtime ``hex(id(...))`` name.
        return {
            "hex_without_prefix": value.hex().replace("0x", ""),
            "kind": "binary64",
            "value": value,
        }
    raise RuntimeError(f"Unsupported scalar type {type(value).__name__}.")


def compact_scalar_values(values: list[Any]) -> dict[str, Any]:
    normalized = [scalar_descriptor(value) for value in values]
    if not normalized:
        return {"encoding": "empty", "length": 0}
    for period in range(1, len(normalized) + 1):
        if len(normalized) % period == 0 and all(
            normalized[index] == normalized[index % period]
            for index in range(len(normalized))
        ):
            return {
                "encoding": "repeat",
                "length": len(normalized),
                "pattern": normalized[:period],
            }
    raise RuntimeError("Unreachable scalar compaction failure.")


def _run_length_encode(values: list[Any]) -> list[dict[str, Any]]:
    runs: list[dict[str, Any]] = []
    for value in values:
        if runs and runs[-1]["value"] == value:
            runs[-1]["count"] += 1
        else:
            runs.append({"count": 1, "value": value})
    return runs


def _register_rule_graph(
    rules: Iterable[Any],
    normalizer: IdentityNormalizer,
    RuleSet: type,
    DaySchedule: type,
) -> tuple[list[Any], list[Any]]:
    unique_rules: list[Any] = []
    seen_rules: set[int] = set()
    for rule in rules:
        if rule is None:
            continue
        if not isinstance(rule, RuleSet):
            raise RuntimeError("A Schedule graph contains a non-RuleSet value.")
        normalizer.identity(rule, "ruleset")
        if id(rule) not in seen_rules:
            seen_rules.add(id(rule))
            unique_rules.append(rule)

    unique_days: list[Any] = []
    seen_days: set[int] = set()
    for rule in unique_rules:
        for key in SLOT_KEYS:
            day = getattr(rule, key)
            if day is None:
                continue
            if not isinstance(day, DaySchedule):
                raise RuntimeError("A RuleSet graph contains a non-DaySchedule value.")
            normalizer.identity(day, "day-schedule")
            if id(day) not in seen_days:
                seen_days.add(id(day))
                unique_days.append(day)
    return unique_rules, unique_days


def _rule_graph_descriptor(
    rules: list[Any],
    normalizer: IdentityNormalizer,
    RuleSet: type,
    DaySchedule: type,
) -> dict[str, Any]:
    unique_rules, unique_days = _register_rule_graph(
        rules, normalizer, RuleSet, DaySchedule
    )
    return {
        "day_schedules": [
            {
                "identity_group": normalizer.identity(day, "day-schedule"),
                "name": normalizer.name(day.name, day, "day-schedule"),
                "schedule_type": day.type.value,
                "unit": day.unit,
                "values": compact_scalar_values(list(day.data)),
            }
            for day in unique_days
        ],
        "rulesets": [
            {
                "identity_group": normalizer.identity(rule, "ruleset"),
                "name": normalizer.name(rule.name, rule, "ruleset"),
                "schedule_type": rule.type.value,
                "slots": {
                    key: (
                        None
                        if getattr(rule, key) is None
                        else normalizer.identity(getattr(rule, key), "day-schedule")
                    )
                    for key in SLOT_KEYS
                },
            }
            for rule in unique_rules
        ],
    }


def schedule_descriptor(
    schedule: Any,
    normalizer: IdentityNormalizer,
    Schedule: type,
    RuleSet: type,
    DaySchedule: type,
) -> dict[str, Any]:
    if not isinstance(schedule, Schedule):
        raise RuntimeError("A Schedule result has the wrong type.")
    schedule_group = normalizer.identity(schedule, "schedule")
    rules = list(schedule.data)
    graph = _rule_graph_descriptor(rules, normalizer, RuleSet, DaySchedule)
    refs = [normalizer.identity(rule, "ruleset") for rule in rules]
    return {
        "identity_group": schedule_group,
        "kind": "schedule",
        "length": len(rules),
        "name": normalizer.name(schedule.name, schedule, "schedule"),
        "object_graph": graph,
        "rule_references": _run_length_encode(refs),
        "schedule_type": schedule.type.value,
    }


def _compact_descriptor(
    periods: list[Any],
    normalizer: IdentityNormalizer,
    RuleSet: type,
    DaySchedule: type,
) -> dict[str, Any]:
    rules = [period[2] for period in periods if period[2] is not None]
    graph = _rule_graph_descriptor(rules, normalizer, RuleSet, DaySchedule)
    return {
        "kind": "compact-periods",
        "object_graph": graph,
        "periods": [
            {
                "end": describe_value(period[1], normalizer, None, RuleSet, DaySchedule, None),
                "ruleset_identity_group": (
                    None
                    if period[2] is None
                    else normalizer.identity(period[2], "ruleset")
                ),
                "start": describe_value(period[0], normalizer, None, RuleSet, DaySchedule, None),
            }
            for period in periods
        ],
    }


def _idf_descriptor(value: Any, normalizer: IdentityNormalizer) -> dict[str, Any]:
    dictionary = vars(value)
    extended = next(
        (
            item
            for key, item in dictionary.items()
            if key.endswith("__extended_input")
        ),
        None,
    )
    if extended is None:
        raise RuntimeError("An IdfObject does not expose its extended input list.")
    if not isinstance(extended, list):
        raise RuntimeError("An IdfObject extended input is not an ordered list.")
    raw_entries = list(value.data.items())
    raw_keys = tuple(str(key) for key, _ in raw_entries)
    if raw_keys != IDF_UPSTREAM_DATA_KEYS:
        raise RuntimeError("An IdfObject does not expose the exact pinned primary keys.")
    data_entries = [
        {"field": field, "value": item}
        for field, (_, item) in zip(IDF_PRIMARY_FIELD_NAMES, raw_entries)
    ]
    descriptor = {
        "data_entries": data_entries,
        "extended_input": list(extended),
        "kind": "idf-object",
        "object_type": value.idd.name,
    }
    _validate_idf_descriptor(None, descriptor)
    return descriptor


def _validate_idf_descriptor(
    identifier: str | None,
    descriptor: dict[str, Any],
) -> None:
    if set(descriptor) != {
        "data_entries",
        "extended_input",
        "kind",
        "object_type",
    }:
        raise RuntimeError("An IDF descriptor has unexpected keys.")
    if descriptor["kind"] != "idf-object":
        raise RuntimeError("An IDF descriptor has the wrong result kind.")
    if descriptor["object_type"] != "Schedule:Compact":
        raise RuntimeError("An IDF descriptor has the wrong object type.")
    entries = descriptor["data_entries"]
    if not isinstance(entries, list) or len(entries) != len(IDF_PRIMARY_FIELD_NAMES):
        raise RuntimeError("An IDF descriptor does not have 153 primary entries.")
    if any(
        not isinstance(entry, dict) or set(entry) != {"field", "value"}
        for entry in entries
    ):
        raise RuntimeError("An IDF descriptor primary entry is malformed.")
    if tuple(entry["field"] for entry in entries) != IDF_PRIMARY_FIELD_NAMES:
        raise RuntimeError("An IDF descriptor primary field order drifted.")

    values = [entry["value"] for entry in entries]
    primary_non_null_count = next(
        (index for index, item in enumerate(values) if item is None),
        len(values),
    )
    if primary_non_null_count < 2:
        raise RuntimeError("An IDF descriptor omits a required primary header field.")
    if any(not isinstance(item, str) for item in values[:primary_non_null_count]):
        raise RuntimeError("An IDF descriptor primary prefix contains a non-text value.")
    if any(item is not None for item in values[primary_non_null_count:]):
        raise RuntimeError("An IDF descriptor has a non-null value after its null tail.")

    extended = descriptor["extended_input"]
    if not isinstance(extended, list) or any(
        not isinstance(item, str) for item in extended
    ):
        raise RuntimeError("An IDF descriptor extension is not an ordered text list.")
    if extended and primary_non_null_count != len(entries):
        raise RuntimeError("An IDF descriptor extension does not continue a full primary prefix.")

    if identifier is None:
        return
    expected_shape = IDF_CASE_SHAPES.get(identifier)
    if expected_shape is None:
        raise RuntimeError(f"Unknown Schedule IDF case {identifier!r}.")
    actual_shape = {
        "extended_count": len(extended),
        "primary_non_null_count": primary_non_null_count,
    }
    if actual_shape != expected_shape:
        raise RuntimeError(f"Schedule IDF case {identifier!r} shape drifted.")


def describe_value(
    value: Any,
    normalizer: IdentityNormalizer,
    Schedule: type | None,
    RuleSet: type,
    DaySchedule: type,
    ScheduleType: type | None,
) -> Any:
    if Schedule is not None and isinstance(value, Schedule):
        return schedule_descriptor(value, normalizer, Schedule, RuleSet, DaySchedule)
    if isinstance(value, RuleSet):
        graph = _rule_graph_descriptor([value], normalizer, RuleSet, DaySchedule)
        return {
            "identity_group": normalizer.identity(value, "ruleset"),
            "kind": "ruleset",
            "object_graph": graph,
        }
    if isinstance(value, DaySchedule):
        normalizer.identity(value, "day-schedule")
        return {
            "identity_group": normalizer.identity(value, "day-schedule"),
            "kind": "day-schedule",
            "name": normalizer.name(value.name, value, "day-schedule"),
            "schedule_type": value.type.value,
            "unit": value.unit,
            "values": compact_scalar_values(list(value.data)),
        }
    if ScheduleType is not None and isinstance(value, ScheduleType):
        return {
            "idf_object_name": value.idf_objname,
            "kind": "schedule-type",
            "value": value.value,
        }
    if value is None:
        return {"kind": "none"}
    if type(value) in (bool, int, float):
        return scalar_descriptor(value)
    if isinstance(value, datetime.date):
        return {"kind": "date", "value": value.isoformat()}
    if isinstance(value, str):
        return {"kind": "text", "value": normalizer.normalized_text(value)}
    if isinstance(value, dict):
        return {
            str(key): describe_value(
                item, normalizer, Schedule, RuleSet, DaySchedule, ScheduleType
            )
            for key, item in value.items()
        }
    if isinstance(value, (list, tuple)):
        sequence = list(value)
        if sequence and all(isinstance(item, datetime.date) for item in sequence):
            return {
                "container": "list" if isinstance(value, list) else "tuple",
                "dates": [item.isoformat() for item in sequence],
                "kind": "date-sequence",
                "length": len(sequence),
            }
        if sequence and all(isinstance(item, RuleSet) for item in sequence):
            graph = _rule_graph_descriptor(sequence, normalizer, RuleSet, DaySchedule)
            return {
                "container": "list" if isinstance(value, list) else "tuple",
                "kind": "ruleset-sequence",
                "length": len(sequence),
                "object_graph": graph,
                "references": _run_length_encode(
                    [normalizer.identity(item, "ruleset") for item in sequence]
                ),
            }
        if sequence and all(isinstance(item, DaySchedule) for item in sequence):
            for item in sequence:
                normalizer.identity(item, "day-schedule")
            unique_days: list[Any] = []
            seen_days: set[int] = set()
            for item in sequence:
                if id(item) not in seen_days:
                    seen_days.add(id(item))
                    unique_days.append(item)
            return {
                "container": "list" if isinstance(value, list) else "tuple",
                "day_schedules": [
                    describe_value(
                        item,
                        normalizer,
                        Schedule,
                        RuleSet,
                        DaySchedule,
                        ScheduleType,
                    )
                    for item in unique_days
                ],
                "kind": "day-schedule-sequence",
                "length": len(sequence),
                "references": _run_length_encode(
                    [normalizer.identity(item, "day-schedule") for item in sequence]
                ),
            }
        if sequence and all(
            isinstance(item, tuple)
            and len(item) == 3
            and isinstance(item[0], (datetime.date, str))
            and isinstance(item[1], (datetime.date, str))
            and (item[2] is None or isinstance(item[2], RuleSet))
            for item in sequence
        ):
            return _compact_descriptor(sequence, normalizer, RuleSet, DaySchedule)
        return {
            "container": "list" if isinstance(value, list) else "tuple",
            "items": [
                describe_value(
                    item, normalizer, Schedule, RuleSet, DaySchedule, ScheduleType
                )
                for item in sequence
            ],
            "kind": "sequence",
        }
    if hasattr(value, "idd") and hasattr(value, "data"):
        return _idf_descriptor(value, normalizer)
    return {"kind": "object", "python_type": type(value).__name__}


def python_error_category(exception: Exception) -> str:
    name = type(exception).__name__
    if name in {"OverflowError", "ValueError"}:
        return "domain"
    if name in {"AttributeError", "TypeError"}:
        return "type"
    raise RuntimeError(f"Unknown Python core exception {name!r}.")


def _date(month: int, day: int) -> datetime.date:
    return datetime.date(DEFAULT_YEAR, month, day)


def _make_day(DaySchedule: type, ScheduleType: type, name: str, value: Any, type_name: str = "real") -> Any:
    return DaySchedule(name, [value] * 144, type=ScheduleType(type_name))


def _make_pattern_day(
    DaySchedule: type,
    ScheduleType: type,
    name: str,
    values: list[Any],
    type_name: str = "real",
) -> Any:
    if len(values) > 144:
        raise RuntimeError("A test pattern is longer than one day.")
    return DaySchedule(
        name,
        values + ([0] * (144 - len(values))),
        type=ScheduleType(type_name),
    )


def _make_rule(
    DaySchedule: type,
    RuleSet: type,
    ScheduleType: type,
    name: str,
    value: Any,
    type_name: str = "real",
) -> Any:
    return RuleSet.from_constant(name, value, type=ScheduleType(type_name))


def _metric_schedule(
    DaySchedule: type,
    RuleSet: type,
    Schedule: type,
    ScheduleType: type,
    values: list[Any],
    *,
    name: str,
) -> tuple[Any, Any, Any]:
    day = DaySchedule(name + "Day", values, type=ScheduleType.REAL)
    rule = RuleSet(name + "Rule", day, day)
    schedule = Schedule.from_constant(name, rule)
    return schedule, rule, day


def _three_period_schedule(
    DaySchedule: type,
    RuleSet: type,
    Schedule: type,
    ScheduleType: type,
    *,
    convertible: bool = True,
) -> tuple[Any, Any, Any]:
    left = _make_rule(DaySchedule, RuleSet, ScheduleType, "A", 0 if convertible else 0.25)
    middle = _make_rule(DaySchedule, RuleSet, ScheduleType, "B", 1 if convertible else 0.75)
    schedule = Schedule("S", [left] * 2 + [middle] * 2 + [left] * 361)
    return schedule, left, middle


def build_scenario(
    identifier: str,
    DaySchedule: type,
    RuleSet: type,
    Schedule: type,
    ScheduleType: type,
) -> Scenario:
    real = ScheduleType.REAL
    fraction = ScheduleType.FRACTION
    onoff = ScheduleType.ONOFF

    if identifier == "constant.fixed-length":
        return Scenario({}, lambda: Schedule.FIXED_LENGTH)
    if identifier == "constant.time-tuple":
        grid = Schedule.TIME_TUPLE

        def mutate_and_restore() -> dict[str, Any]:
            original = grid[0]
            replacement = original + datetime.timedelta(days=1)
            try:
                grid[0] = replacement
                return {
                    "container_type": type(grid).__name__,
                    "first_after_assignment": grid[0],
                    "is_class_value": grid is Schedule.TIME_TUPLE,
                    "mutation_succeeded": grid[0] == replacement,
                }
            finally:
                grid[0] = original

        return Scenario({"time_tuple": grid}, mutate_and_restore)

    if identifier == "init.default-real":
        return Scenario({"name": "DefaultReal"}, lambda: Schedule("DefaultReal"))
    if identifier == "init.default-fraction":
        return Scenario(
            {"name": "DefaultFraction", "type": fraction},
            lambda: Schedule("DefaultFraction", type=fraction),
        )
    if identifier == "init.anonymous":
        return Scenario({"name": None}, lambda: Schedule(None))
    if identifier == "init.empty-name":
        return Scenario({"name": ""}, lambda: Schedule(""))
    if identifier == "init.whitespace-name":
        return Scenario({"name": "  "}, lambda: Schedule("  "))
    if identifier == "init.surrounding-space-name":
        return Scenario({"name": "  Named  "}, lambda: Schedule("  Named  "))
    if identifier == "init.invalid-length":
        rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "R", 0)
        items = [rule] * 364
        return Scenario({"rulesets": items}, lambda: Schedule("BadLength", items))
    if identifier == "init.invalid-item":
        rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "R", 0)
        items = [rule] * 364 + [object()]
        return Scenario({"rulesets": items}, lambda: Schedule("BadItem", items))
    if identifier == "init.mixed-types":
        real_rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "Real", 0)
        fraction_rule = _make_rule(
            DaySchedule, RuleSet, ScheduleType, "Fraction", 0.5, "fraction"
        )
        items = [real_rule] * 364 + [fraction_rule]
        return Scenario({"rulesets": items}, lambda: Schedule("Mixed", items))
    if identifier == "init.explicit-type-mismatch":
        rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "Real", 0)
        items = [rule] * 365
        return Scenario(
            {"rulesets": items, "type": fraction},
            lambda: Schedule("Mismatch", items, type=fraction),
        )
    if identifier == "init.supplied-list-alias":
        original = _make_rule(DaySchedule, RuleSet, ScheduleType, "Original", 0)
        replacement = _make_rule(DaySchedule, RuleSet, ScheduleType, "Replacement", 1)
        items = [original] * 365
        schedule = Schedule("Aliased", items)

        def replace_external_item() -> dict[str, Any]:
            items[0] = replacement
            return {
                "data_is_supplied_list": schedule.data is items,
                "schedule_first_is_replacement": schedule[0] is replacement,
            }

        return Scenario(
            {
                "original": original,
                "replacement": replacement,
                "rulesets": items,
                "schedule": schedule,
            },
            replace_external_item,
        )

    if identifier.startswith("apply."):
        source = _make_rule(DaySchedule, RuleSet, ScheduleType, "Source", 0)
        override = _make_rule(DaySchedule, RuleSet, ScheduleType, "Override", 1)
        schedule = Schedule.from_constant("Apply", source)
        inputs: dict[str, Any] = {
            "override": override,
            "schedule": schedule,
            "source": source,
        }
        if identifier == "apply.inplace-inclusive-mmdd":
            return Scenario(inputs, lambda: schedule.apply(override, start="0102", end="0103"))
        if identifier == "apply.noninplace-deepcopy":
            return Scenario(
                inputs,
                lambda: schedule.apply(override, start="0102", end="0103", inplace=False),
            )
        if identifier == "apply.parse-yyyymmdd":
            return Scenario(inputs, lambda: schedule.apply(override, start="20260102", end="20260103"))
        if identifier == "apply.parse-digit-pair":
            return Scenario(inputs, lambda: schedule.apply(override, start="1/2", end="1-3"))
        if identifier == "apply.reversed-noop":
            return Scenario(inputs, lambda: schedule.apply(override, start="0103", end="0102"))
        if identifier == "apply.outside-year-noop":
            return Scenario(inputs, lambda: schedule.apply(override, start="20270101", end="20271231"))
        if identifier == "apply.foreign-year-noop":
            return Scenario(inputs, lambda: schedule.apply(override, start="20250102", end="20250103"))
        if identifier == "apply.type-unchecked":
            onoff_rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "OnOff", 1, "onoff")
            inputs["override"] = onoff_rule
            return Scenario(inputs, lambda: schedule.apply(onoff_rule, start="0102", end="0102"))
        if identifier == "apply.invalid-date":
            return Scenario(inputs, lambda: schedule.apply(override, start="not-a-date", end="0102"))

    if identifier.startswith("deepcopy."):
        schedule, left, middle = _three_period_schedule(
            DaySchedule, RuleSet, Schedule, ScheduleType
        )
        inputs = {"left": left, "middle": middle, "schedule": schedule}
        if identifier == "deepcopy.shared-period":
            shared_day = _make_day(DaySchedule, ScheduleType, "SharedDay", 0)
            shared_rule = RuleSet("SharedRule", shared_day, shared_day, monday=shared_day, holiday=shared_day)
            shared_schedule = Schedule.from_constant("Shared", shared_rule)
            return Scenario(
                {"day": shared_day, "rule": shared_rule, "schedule": shared_schedule},
                lambda: copy.deepcopy(shared_schedule),
            )
        if identifier == "deepcopy.noncontiguous-alias-split":
            return Scenario(inputs, lambda: copy.deepcopy(schedule))
        if identifier == "deepcopy.memo-hit":
            return Scenario(inputs, lambda: schedule.__deepcopy__({id(schedule): "memo-sentinel"}))

    if identifier.startswith("astype."):
        schedule, left, middle = _three_period_schedule(
            DaySchedule, RuleSet, Schedule, ScheduleType
        )
        inputs = {"left": left, "middle": middle, "schedule": schedule, "type": onoff}
        if identifier == "astype.outplace":
            return Scenario(inputs, lambda: schedule.astype(onoff))
        if identifier == "astype.inplace-stale":
            return Scenario(inputs, lambda: schedule.astype(onoff, inplace=True))
        good = _make_rule(DaySchedule, RuleSet, ScheduleType, "GOOD", 0.0)
        bad = _make_rule(DaySchedule, RuleSet, ScheduleType, "BAD", 2.0)
        partial = Schedule("Partial", [good] + [bad] * 364)
        partial_inputs = {"bad": bad, "good": good, "schedule": partial, "type": onoff}
        if identifier == "astype.inplace-partial":
            return Scenario(partial_inputs, lambda: partial.astype(onoff, inplace=True))
        if identifier == "astype.outplace-failure-atomic":
            return Scenario(partial_inputs, lambda: partial.astype(onoff))

    if identifier.startswith(("average.", "integral.", "positive-average.")):
        if identifier.endswith("catastrophic"):
            values = [1e16, 1.0, -1e16] + ([0.0] * 141)
        elif identifier.endswith("minimum-subnormal"):
            values = [FLOAT_MIN_SUBNORMAL] * 144
        elif identifier == "average.negative-zero":
            values = [-0.0] * 144
        elif identifier == "integral.overflow":
            values = [FLOAT_MAX] * 144
        elif identifier == "positive-average.none":
            values = [-1.0, 0.0] * 72
        else:
            raise RuntimeError(f"Unknown metric scenario {identifier!r}.")
        schedule, rule, day = _metric_schedule(
            DaySchedule, RuleSet, Schedule, ScheduleType, values, name="Metric"
        )
        inputs = {"day": day, "rule": rule, "schedule": schedule}
        if identifier.startswith("average."):
            return Scenario(inputs, lambda: schedule.average)
        if identifier.startswith("integral."):
            return Scenario(inputs, lambda: schedule.integral)
        return Scenario(inputs, lambda: schedule.positive_average)

    if identifier.startswith(("min.", "max.")):
        if identifier.endswith("negative-zero"):
            rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "NegativeZero", -0.0)
            schedule = Schedule.from_constant("Extrema", rule)
            inputs = {"rule": rule, "schedule": schedule}
        else:
            base = _make_day(DaySchedule, ScheduleType, "Base", 1.0)
            holiday_value = -999.0 if identifier.startswith("min.") else 999.0
            holiday = _make_day(DaySchedule, ScheduleType, "UnusedHoliday", holiday_value)
            rule = RuleSet("HolidayExtrema", base, base, holiday=holiday)
            schedule = Schedule.from_constant("Extrema", rule)
            inputs = {"base": base, "holiday": holiday, "rule": rule, "schedule": schedule}
        return Scenario(
            inputs,
            (lambda: schedule.min) if identifier.startswith("min.") else (lambda: schedule.max),
        )

    if identifier.startswith("clip."):
        source = _make_rule(DaySchedule, RuleSet, ScheduleType, "ClipRule", 0.5)
        schedule = Schedule.from_constant("ClipSchedule", source)
        inputs = {"schedule": schedule, "source": source}
        if identifier == "clip.outplace-bounds":
            return Scenario(inputs, lambda: schedule.clip(0.6, 0.8, name="Clipped"))
        if identifier == "clip.outplace-lower-only":
            return Scenario(inputs, lambda: schedule.clip(min_value=0.6))
        if identifier == "clip.outplace-no-bounds-copy":
            return Scenario(inputs, lambda: schedule.clip())
        if identifier == "clip.outplace-upper-only":
            return Scenario(inputs, lambda: schedule.clip(max_value=0.4))
        if identifier == "clip.min-greater-than-max":
            return Scenario(inputs, lambda: schedule.clip(0.8, 0.2))
        if identifier == "clip.empty-name-default":
            return Scenario(inputs, lambda: schedule.clip(0.0, 1.0, name=""))
        if identifier == "clip.inplace-distinct":
            return Scenario(inputs, lambda: schedule.clip(0.2, 0.8, inplace=True))
        if identifier == "clip.inplace-partial":
            bad = _make_rule(DaySchedule, RuleSet, ScheduleType, "OnOff", 1, "onoff")
            schedule.data[1] = bad
            inputs["bad"] = bad
            return Scenario(inputs, lambda: schedule.clip(0.2, 0.8, inplace=True))

    if identifier.startswith("compactize."):
        left = _make_rule(DaySchedule, RuleSet, ScheduleType, "A", 0)
        right = _make_rule(DaySchedule, RuleSet, ScheduleType, "B", 0)
        if identifier == "compactize.identity-runs":
            items = [left] * 2 + [right] * 2 + [left] * 361
        elif identifier == "compactize.equal-distinct":
            items = [left, right] + [right] * 363
        elif identifier == "compactize.full-run":
            items = [left] * 365
        elif identifier == "compactize.default-distinct":
            schedule = Schedule("CompactDefault")
            return Scenario(
                {"schedule": schedule},
                lambda: schedule.compactize(),
            )
        else:
            raise RuntimeError(f"Unknown compactize scenario {identifier!r}.")
        schedule = Schedule("Compact", items)
        return Scenario(
            {"left": left, "right": right, "schedule": schedule},
            lambda: schedule.compactize(),
        )

    if identifier.startswith("dayschedules."):
        fallback_week = _make_day(DaySchedule, ScheduleType, "Weekdays", 1)
        fallback_end = _make_day(DaySchedule, ScheduleType, "Weekends", 2)
        monday = _make_day(DaySchedule, ScheduleType, "Monday", 3)
        sunday = _make_day(DaySchedule, ScheduleType, "Sunday", 4)
        holiday = _make_day(DaySchedule, ScheduleType, "Holiday", 999)
        rule = RuleSet(
            "CalendarRule",
            fallback_week,
            fallback_end,
            monday=monday,
            sunday=sunday,
            holiday=holiday,
        )
        schedule = Schedule.from_constant("Calendar", rule)
        inputs = {
            "holiday": holiday,
            "monday": monday,
            "rule": rule,
            "schedule": schedule,
            "sunday": sunday,
            "weekdays": fallback_week,
            "weekends": fallback_end,
        }
        if identifier == "dayschedules.weekday-overrides":
            return Scenario(inputs, lambda: schedule.dayschedules)

        def read_twice() -> dict[str, Any]:
            first = schedule.dayschedules
            second = schedule.dayschedules
            return {
                "first": first,
                "lists_are_distinct": first is not second,
                "same_day_references": all(a is b for a, b in zip(first, second)),
                "second": second,
            }

        return Scenario(inputs, read_twice)

    if identifier.startswith("from-compact."):
        left = _make_rule(DaySchedule, RuleSet, ScheduleType, "A", 0)
        right = _make_rule(DaySchedule, RuleSet, ScheduleType, "B", 1)
        inputs: dict[str, Any] = {"left": left, "right": right}
        if identifier == "from-compact.empty":
            compact: list[Any] = []
        elif identifier == "from-compact.mixed-type":
            fraction_rule = _make_rule(
                DaySchedule, RuleSet, ScheduleType, "Fraction", 0.5, "fraction"
            )
            inputs["fraction"] = fraction_rule
            compact = [("0101", "0101", left), ("0102", "0102", fraction_rule)]
        elif identifier == "from-compact.single-gap":
            compact = [("0102", "0103", left)]
        elif identifier == "from-compact.leap-day":
            compact = [("0229", "0301", left)]
        elif identifier == "from-compact.same-ref-adjacent":
            compact = [("0101", "0102", left), ("0103", "0104", left)]
        elif identifier == "from-compact.distinct-equal-adjacent":
            equal = _make_rule(DaySchedule, RuleSet, ScheduleType, "A", 0)
            inputs["equal"] = equal
            compact = [("0101", "0102", left), ("0103", "0104", equal)]
        elif identifier == "from-compact.overlap-later-wins":
            compact = [("0101", "0104", left), ("0103", "0105", right)]
        elif identifier == "from-compact.reversed-noop":
            compact = [("0103", "0102", left)]
        elif identifier == "from-compact.outside-noop":
            compact = [("20270101", "20271231", left)]
        else:
            raise RuntimeError(f"Unknown compact scenario {identifier!r}.")
        inputs["compact"] = compact
        return Scenario(inputs, lambda: Schedule.from_compact("FromCompact", compact))

    if identifier.startswith("from-constant."):
        if identifier == "from-constant.scalar":
            return Scenario(
                {"type": fraction, "value": 0.25},
                lambda: Schedule.from_constant("Scalar", 0.25, type=fraction),
            )
        if identifier == "from-constant.bool":
            return Scenario({"value": True}, lambda: Schedule.from_constant("Bool", True))
        if identifier == "from-constant.anonymous":
            return Scenario({"name": None, "value": 0.25}, lambda: Schedule.from_constant(None, 0.25))
        if identifier == "from-constant.real-nan":
            return Scenario(
                {"type": real, "value": math.nan},
                lambda: Schedule.from_constant("NaN", math.nan, type=real),
            )
        if identifier == "from-constant.surrounding-space-name":
            return Scenario(
                {"name": "  Scalar  ", "type": fraction, "value": 0.25},
                lambda: Schedule.from_constant("  Scalar  ", 0.25, type=fraction),
            )
        if identifier == "from-constant.unsupported-object":
            unsupported = object()
            return Scenario(
                {"type": real, "value": unsupported},
                lambda: Schedule.from_constant("Unsupported", unsupported, type=real),
            )
        day = _make_day(DaySchedule, ScheduleType, "FractionDay", 0.25, "fraction")
        if identifier == "from-constant.day-explicit-type-ignored":
            return Scenario(
                {"day": day, "explicit_type": real},
                lambda: Schedule.from_constant("Day", day, type=real),
            )
        rule = RuleSet("FractionRule", day, day)
        return Scenario(
            {"explicit_type": real, "rule": rule},
            lambda: Schedule.from_constant("Rule", rule, type=real),
        )

    if identifier.startswith("from-windows."):
        if identifier == "from-windows.empty":
            windows: list[Any] = []
            return Scenario(
                {"default": 0.1, "type": fraction, "windows": windows},
                lambda: Schedule.from_windows("EmptyWindows", 0.1, windows, type=fraction),
            )
        if identifier == "from-windows.leap-day":
            windows = [("0229", "0301", 0.5)]
            return Scenario(
                {"default": 0.1, "type": fraction, "windows": windows},
                lambda: Schedule.from_windows("LeapDay", 0.1, windows, type=fraction),
            )
        if identifier == "from-windows.repeated-day-wrappers":
            repeated_day = _make_day(
                DaySchedule, ScheduleType, "RepeatedDay", 0.5, "fraction"
            )
            windows = [
                ("0102", "0102", repeated_day),
                ("0104", "0104", repeated_day),
            ]
            return Scenario(
                {
                    "default": 0.1,
                    "override": repeated_day,
                    "type": fraction,
                    "windows": windows,
                },
                lambda: Schedule.from_windows(
                    "RepeatedDayWindows", 0.1, windows, type=fraction
                ),
            )
        if identifier == "from-windows.repeated-scalar-wrappers":
            windows = [("0102", "0102", 0.5), ("0104", "0104", 0.5)]
            return Scenario(
                {"default": 0.1, "type": fraction, "windows": windows},
                lambda: Schedule.from_windows(
                    "RepeatedScalarWindows", 0.1, windows, type=fraction
                ),
            )
        if identifier == "from-windows.scalar-positive-infinity":
            windows = [("0102", "0103", math.inf)]
            return Scenario(
                {"default": 0.0, "type": real, "windows": windows},
                lambda: Schedule.from_windows(
                    "InfiniteWindow", 0.0, windows, type=real
                ),
            )
        if identifier == "from-windows.scalar-overlap":
            windows = [("0102", "0103", 0.2), ("0103", "0104", 0.3)]
            return Scenario(
                {"default": 0.1, "type": fraction, "windows": windows},
                lambda: Schedule.from_windows("ScalarWindows", 0.1, windows, type=fraction),
            )
        if identifier == "from-windows.day-alias":
            default_day = _make_day(DaySchedule, ScheduleType, "DefaultDay", 0.1, "fraction")
            override_day = _make_day(DaySchedule, ScheduleType, "OverrideDay", 0.5, "fraction")
            windows = [("0102", "0103", override_day)]
            return Scenario(
                {"default": default_day, "override": override_day, "windows": windows},
                lambda: Schedule.from_windows("DayWindows", default_day, windows, type=real),
            )
        if identifier == "from-windows.ruleset-alias":
            default_rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "DefaultRule", 0)
            override_rule = _make_rule(DaySchedule, RuleSet, ScheduleType, "OverrideRule", 1)
            windows = [("0102", "0103", override_rule)]
            return Scenario(
                {"default": default_rule, "override": override_rule, "windows": windows},
                lambda: Schedule.from_windows("RuleWindows", default_rule, windows, type=fraction),
            )
        if identifier == "from-windows.type-mismatch":
            mismatch = _make_rule(DaySchedule, RuleSet, ScheduleType, "Mismatch", 1, "onoff")
            windows = [("0102", "0103", mismatch)]
            return Scenario(
                {"default": 0.1, "override": mismatch, "type": fraction, "windows": windows},
                lambda: Schedule.from_windows("Mismatch", 0.1, windows, type=fraction),
            )
        if identifier == "from-windows.unsupported-object":
            unsupported = object()
            windows = [("0102", "0103", unsupported)]
            return Scenario(
                {"default": 0.1, "override": unsupported, "windows": windows},
                lambda: Schedule.from_windows("Unsupported", 0.1, windows),
            )
        windows = [("0103", "0102", 0.5)]
        return Scenario(
            {"default": 0.1, "type": fraction, "windows": windows},
            lambda: Schedule.from_windows("Reversed", 0.1, windows, type=fraction),
        )

    if identifier.startswith("idf."):
        if identifier == "idf.constant-real":
            schedule = Schedule.from_constant("Annual", 1.0)
            return Scenario({"schedule": schedule}, lambda: schedule.to_idf_object())
        if identifier == "idf.default-expanded-fields":
            schedule = Schedule("Default")
            return Scenario({"schedule": schedule}, lambda: schedule.to_idf_object())
        if identifier == "idf.multiple-periods":
            first = _make_rule(DaySchedule, RuleSet, ScheduleType, "First", 1.0)
            second = _make_rule(DaySchedule, RuleSet, ScheduleType, "Second", 2.0)
            schedule = Schedule.from_compact(
                "Multiple", [("0101", "0630", first), ("0701", "1231", second)]
            )
            return Scenario(
                {"first": first, "schedule": schedule, "second": second},
                lambda: schedule.to_idf_object(),
            )
        weekdays = _make_day(DaySchedule, ScheduleType, "Weekdays", 0.5)
        weekends = _make_day(DaySchedule, ScheduleType, "Weekends", -0.0)
        monday = DaySchedule(
            "Monday",
            ([10000.0] * 36) + ([FLOAT_MIN_SUBNORMAL] * 108),
            type=real,
        )
        saturday = _make_day(DaySchedule, ScheduleType, "Saturday", 1.23456789)
        holiday = _make_day(DaySchedule, ScheduleType, "Holiday", 2.0)
        rule = RuleSet(
            "RichRule",
            weekdays,
            weekends,
            monday=monday,
            saturday=saturday,
            holiday=holiday,
        )
        schedule = Schedule.from_constant("A,B;!", rule)
        return Scenario(
            {
                "holiday": holiday,
                "monday": monday,
                "rule": rule,
                "saturday": saturday,
                "schedule": schedule,
                "weekdays": weekdays,
                "weekends": weekends,
            },
            lambda: schedule.to_idf_object(),
        )

    if identifier.startswith("summary."):
        left_day = _make_day(DaySchedule, ScheduleType, "LeftDay", -0.0)
        left = RuleSet("dup", left_day, left_day)
        right = _make_rule(DaySchedule, RuleSet, ScheduleType, "peak", 12345.0)
        final_day = _make_day(DaySchedule, ScheduleType, "FinalDay", -0.0)
        final = RuleSet("dup", final_day, final_day)
        schedule = Schedule("S'Q", [left] * 2 + [right] * 2 + [final] * 361)
        inputs = {"final": final, "left": left, "right": right, "schedule": schedule}
        if identifier == "summary.exact-rich":
            return Scenario(inputs, lambda: schedule.summary())
        if identifier == "summary.zero-period-limit":
            return Scenario(inputs, lambda: schedule.summary(max_periods=0))
        if identifier == "summary.negative-period-limit":
            return Scenario(inputs, lambda: schedule.summary(max_periods=-1))
        return Scenario(inputs, lambda: schedule.summary(max_periods=1.5))

    if identifier.startswith("type."):
        schedule = Schedule.from_constant(
            "Type",
            0.0,
            type=(fraction if identifier == "type.explicit-fraction" else real),
        )
        inputs = {"schedule": schedule}
        return Scenario(inputs, lambda: schedule.type)

    if identifier.startswith(("unify-pair.", "unify-many.")):
        a = _make_rule(DaySchedule, RuleSet, ScheduleType, "A", 0)
        b = _make_rule(DaySchedule, RuleSet, ScheduleType, "B", 1)
        c = _make_rule(DaySchedule, RuleSet, ScheduleType, "C", 2)
        d = _make_rule(DaySchedule, RuleSet, ScheduleType, "D", 3)
        e = _make_rule(DaySchedule, RuleSet, ScheduleType, "E", 4)
        full_a = [(_date(1, 1), _date(4, 10), a), (_date(4, 11), _date(12, 31), b)]
        full_b = [(_date(1, 1), _date(2, 19), c), (_date(2, 20), _date(12, 31), d)]
        full_c = [(_date(1, 1), _date(6, 30), e), (_date(7, 1), _date(12, 31), a)]
        if identifier.endswith("asymmetric") or identifier.endswith("asymmetric-three"):
            compacts = [full_a, full_b] if identifier.startswith("unify-pair") else [full_a, full_b, full_c]
        elif identifier == "unify-pair.interior-gap":
            compacts = [
                [
                    (_date(1, 1), _date(1, 1), a),
                    (_date(1, 3), _date(1, 3), a),
                ],
                [(_date(1, 1), _date(1, 3), b)],
            ]
        elif identifier.endswith("missing-coverage"):
            compacts = [
                [(_date(1, 1), _date(1, 1), a)],
                [(_date(1, 1), _date(1, 2), b)],
            ]
        elif identifier.endswith("first-overlap-wins"):
            overlapping = [
                (_date(1, 1), _date(12, 31), a),
                (_date(1, 10), _date(1, 20), b),
            ]
            compacts = [overlapping, full_b]
        elif identifier == "unify-pair.empty":
            compacts = [[], []]
        elif identifier == "unify-many.one-empty":
            compacts = [[]]
        elif identifier == "unify-many.zero":
            compacts = []
        else:
            raise RuntimeError(f"Unknown unification scenario {identifier!r}.")
        inputs = {"compactized_schedules": compacts, "rules": [a, b, c, d, e]}
        if identifier.startswith("unify-pair"):
            return Scenario(
                inputs,
                lambda: Schedule.unify_compactized_schedules(compacts[0], compacts[1]),
            )
        return Scenario(
            inputs,
            lambda: Schedule.unify_compactized_schedules_many(*compacts),
        )

    raise RuntimeError(f"No Schedule core scenario is registered for {identifier!r}.")


def execute_case(
    definition: dict[str, Any],
    DaySchedule: type,
    RuleSet: type,
    Schedule: type,
    ScheduleType: type,
) -> dict[str, Any]:
    scenario = build_scenario(
        definition["id"], DaySchedule, RuleSet, Schedule, ScheduleType
    )
    normalizer = IdentityNormalizer()
    before = {
        key: describe_value(
            value, normalizer, Schedule, RuleSet, DaySchedule, ScheduleType
        )
        for key, value in sorted(scenario.inputs.items())
    }
    try:
        result = scenario.action()
    except Exception as exception:
        if type(exception).__name__ != definition["expected_exception"]:
            raise RuntimeError(
                f"Raised {type(exception).__name__}, expected "
                f"{definition['expected_exception']}."
            ) from exception
        observation: dict[str, Any] = {
            "error_category": python_error_category(exception),
            "exception": {
                "message": normalizer.normalized_text(str(exception)),
                "type": type(exception).__name__,
            },
            "outcome": "raised",
        }
    else:
        if definition["expected_exception"] is not None:
            raise RuntimeError(
                f"Returned but expected {definition['expected_exception']}."
            )
        observation = {
            "outcome": "returned",
            "result": describe_value(
                result, normalizer, Schedule, RuleSet, DaySchedule, ScheduleType
            ),
        }
    after = {
        key: describe_value(
            value, normalizer, Schedule, RuleSet, DaySchedule, ScheduleType
        )
        for key, value in sorted(scenario.inputs.items())
    }
    observation["input_postconditions"] = {}
    for key in before:
        preserved = before[key] == after[key]
        observation["input_postconditions"][key] = {
            "after": (
                {"kind": "same-as-before"} if preserved else after[key]
            ),
            "before": before[key],
            "preserved": preserved,
        }
    case: dict[str, Any] = {
        "id": definition["id"],
        "observation": observation,
        "symbol": definition["symbol"],
    }
    if definition["expected_dotnet"] is not None:
        case["expected_dotnet"] = definition["expected_dotnet"]
    return case


def summarize_cases(cases: list[dict[str, Any]]) -> dict[str, Any]:
    adapted = [case for case in cases if "expected_dotnet" in case]
    exception_symbols = sorted({case["symbol"] for case in adapted})
    equivalent_symbols = sorted(set(TARGET_SYMBOLS) - set(exception_symbols))
    return {
        "adaptation_case_count": len(adapted),
        "adaptation_ids": sorted(
            {case["expected_dotnet"]["adaptation"] for case in adapted}
        ),
        "case_count": len(cases),
        "classification_counts": {
            "equivalent": len(equivalent_symbols),
            "exception": len(exception_symbols),
        },
        "equivalent_symbols": equivalent_symbols,
        "exception_symbols": exception_symbols,
        "expected_dotnet_outcomes": {
            outcome: sum(
                case["expected_dotnet"]["outcome"] == outcome for case in adapted
            )
            for outcome in ("raised", "returned")
        },
        "observed_outcomes": {
            outcome: sum(case["observation"]["outcome"] == outcome for case in cases)
            for outcome in ("raised", "returned")
        },
        "symbol_case_counts": dict(
            sorted(Counter(case["symbol"] for case in cases).items())
        ),
    }


def validate_oracle(value: dict[str, Any]) -> None:
    required = {
        "cases",
        "consumer_contract",
        "runtime",
        "schema",
        "summary",
        "symbols",
        "upstream",
    }
    if set(value) != required or value.get("schema") != SCHEMA:
        raise RuntimeError("Schedule core oracle top-level schema is malformed.")
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Schedule core oracle case count is not exact.")
    identifiers = [case.get("id") for case in cases]
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise RuntimeError("Schedule core oracle case identifiers are malformed.")
    definitions = {item["id"]: item for item in case_definitions()}
    observed_idf_cases: set[str] = set()
    for case in cases:
        if set(case) not in (
            {"id", "observation", "symbol"},
            {"expected_dotnet", "id", "observation", "symbol"},
        ):
            raise RuntimeError(f"Oracle case {case.get('id')!r} has unexpected keys.")
        definition = definitions.get(case["id"])
        if definition is None or case["symbol"] != definition["symbol"]:
            raise RuntimeError(f"Oracle case {case['id']!r} is not definition-bound.")
        expected_dotnet = definition["expected_dotnet"]
        if expected_dotnet is None and "expected_dotnet" in case:
            raise RuntimeError(f"Oracle case {case['id']!r} has unexpected adaptation metadata.")
        if expected_dotnet is not None and case.get("expected_dotnet") != expected_dotnet:
            raise RuntimeError(f"Oracle case {case['id']!r} adaptation metadata drifted.")
        observation = case["observation"]
        if observation.get("outcome") not in {"raised", "returned"}:
            raise RuntimeError(f"Oracle case {case['id']!r} has an invalid outcome.")
        if not isinstance(observation.get("input_postconditions"), dict):
            raise RuntimeError(f"Oracle case {case['id']!r} lacks input postconditions.")
        if observation["outcome"] == "raised":
            if set(observation) != {
                "error_category",
                "exception",
                "input_postconditions",
                "outcome",
            }:
                raise RuntimeError(f"Oracle case {case['id']!r} raised shape is malformed.")
            if observation["exception"]["type"] != definition["expected_exception"]:
                raise RuntimeError(f"Oracle case {case['id']!r} exception drifted.")
            if observation["error_category"] not in {"domain", "type"}:
                raise RuntimeError(f"Oracle case {case['id']!r} error category drifted.")
        else:
            if set(observation) != {"input_postconditions", "outcome", "result"}:
                raise RuntimeError(f"Oracle case {case['id']!r} return shape is malformed.")
            if definition["expected_exception"] is not None:
                raise RuntimeError(f"Oracle case {case['id']!r} should have raised.")
            if case["symbol"] == "Schedule.to_idf_object":
                _validate_idf_descriptor(case["id"], observation["result"])
                observed_idf_cases.add(case["id"])
    if observed_idf_cases != set(IDF_CASE_SHAPES):
        raise RuntimeError("Schedule IDF case coverage drifted.")
    expected_summary = summarize_cases(cases)
    if value["summary"] != expected_summary:
        raise RuntimeError("Schedule core oracle summary is inconsistent.")
    if expected_summary["adaptation_ids"] != sorted(ADAPTATION_IDS):
        raise RuntimeError("Schedule core oracle adaptation inventory drifted.")
    serialized = strict_json_dumps(value)
    if RAW_AUTO_NAME_PATTERN.search(serialized):
        leaking_cases = [
            case["id"]
            for case in cases
            if RAW_AUTO_NAME_PATTERN.search(strict_json_dumps(case))
        ]
        raise RuntimeError(
            "A raw runtime identity entered the Schedule core oracle in "
            f"{leaking_cases!r}."
        )


def build_oracle(inventory: dict[str, Any], commit: str) -> dict[str, Any]:
    import idragon.dragon.profile as profile_module
    from idragon.dragon.profile import DaySchedule, RuleSet, Schedule, ScheduleType

    imported_source = Path(profile_module.__file__).resolve()
    imported_source_sha256 = sha256_file(imported_source)
    if imported_source_sha256 != EXPECTED_SOURCE_SHA256:
        raise SystemExit("The imported profile module is not the exact pinned source.")
    if imported_source_sha256 != inventory["file"]["content_hash"]:
        raise SystemExit("The imported profile module is not the inventoried source.")
    if (
        Schedule.FIXED_LENGTH != ANNUAL_LENGTH
        or not isinstance(Schedule.TIME_TUPLE, list)
        or len(Schedule.TIME_TUPLE) != ANNUAL_LENGTH
        or Schedule.TIME_TUPLE[0] != datetime.date(DEFAULT_YEAR, 1, 1)
        or Schedule.TIME_TUPLE[-1] != datetime.date(DEFAULT_YEAR, 12, 31)
    ):
        raise SystemExit("Pinned Schedule annual constants are not exact.")
    if tuple(RuleSet._DAY_KEYS) != tuple(OPS.OVERRIDE_KEYS):
        raise SystemExit("Pinned RuleSet day-key order is not exact.")
    if DaySchedule.DATA_INTERVAL != 6 or DaySchedule("probe").fixed_length != 144:
        raise SystemExit("Pinned DaySchedule grid constants are not exact.")

    cases = [
        execute_case(item, DaySchedule, RuleSet, Schedule, ScheduleType)
        for item in case_definitions()
    ]
    result = {
        "cases": cases,
        "consumer_contract": copy.deepcopy(CONSUMER_CONTRACT),
        "runtime": {
            "implementation": sys.implementation.name,
            "python_hash_algorithm": sys.hash_info.algorithm,
            "python_hash_seed": 0,
            "python_hash_width_bits": sys.hash_info.width,
            "python_version": ".".join(map(str, sys.version_info[:3])),
        },
        "schema": SCHEMA,
        "summary": summarize_cases(cases),
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
        raise SystemExit("Exact CPython 3.12.7 is required for the Schedule core oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for the Schedule core oracle.")
    if (
        sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM
        or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS
    ):
        raise SystemExit("The pinned CPython hash runtime is not exact.")

    inventory = load_exact_inventory(args.inventory, args.upstream_commit)
    result = build_oracle(inventory, args.upstream_commit.lower())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Generate the pinned EnergyModel initialization/default-IDF oracle.

The corpus binds only ``EnergyModel.__init__`` and
``EnergyModel.create_default_idf`` from EPlusSimple 0.7.0.  Mutable Python
identity is represented by booleans and logical labels; IDF fields use an
explicit tagged encoding so binary64 spelling, enum text, blanks, and Python
scalar kinds remain observable without admitting raw JSON floats.
"""

from __future__ import annotations

import argparse
from collections import Counter
from contextlib import contextmanager
from enum import Enum
import importlib
import importlib.metadata
import importlib.util
import inspect
import os
from pathlib import Path
import re
import sys
from types import SimpleNamespace
from typing import Any, Iterator


SCHEMA = "dragons.python-reference.dragon-model-construction-defaults.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
MODEL_SOURCE_PATH = "src/idragon/dragon/model.py"
SOURCE_SPECS = (
    {
        "ast_sha256": "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9",
        "path": "src/idragon/common.py",
        "source_sha256": "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084",
        "path": "src/idragon/constants.py",
        "source_sha256": "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59",
        "path": MODEL_SOURCE_PATH,
        "source_sha256": "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090",
        "symbols": (
            "EnergyModel.__init__",
            "EnergyModel.create_default_idf",
        ),
    },
    {
        "ast_sha256": "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef",
        "path": "src/idragon/dragon/profile.py",
        "source_sha256": "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445",
        "symbols": (),
    },
    {
        "ast_sha256": "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90",
        "path": "src/idragon/imugi.py",
        "source_sha256": "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613",
        "symbols": (),
    },
)
EXPECTED_SYMBOL_RECEIPTS = {
    "EnergyModel.__init__": {
        "body_hash": "sha256:e4e5ef56fd12719fe976231c03d867e932eff64870f9c0fd7a5107b7e11538f1",
        "kind": "function",
        "signature_hash": "sha256:9706dcab3a90048744a47f3596613b34247cb6cd1eb2903582e2fb2cb6342a2d",
        "symbol_hash": "sha256:1d1dbee8fef8b70b2919c4e46a0ea60efbd748b360d31ff353ea121c72ad97d2",
    },
    "EnergyModel.create_default_idf": {
        "body_hash": "sha256:e505591e57b64f4f7ff0b6fb18e775ad88048d4eaddb9d8a4f9e5a0afd2c8ab7",
        "kind": "function",
        "signature_hash": "sha256:6750822d2a0b36e44dced756c45817742cfc0940e8646be6212eedfe3698d8cf",
        "symbol_hash": "sha256:585b53682bd5dbd4d2081e79eddc2789fa60925baafb5eae26de0541346ac9f4",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "EnergyModel.__init__": "immutable-validated-energy-model-construction",
}
EXPECTED_ASSERTION_IDS = {
    "EnergyModel.__init__": "dragon-model-construction-defaults-init-1d1dbee8",
    "EnergyModel.create_default_idf": "dragon-model-construction-defaults-create-default-idf-585b5368",
}
EXPECTED_CASE_BINDINGS = (
    (
        "dragon-model-construction-defaults.create-default-idf.argument-rejection",
        "energy-model-create-default-idf",
        "EnergyModel.create_default_idf",
    ),
    (
        "dragon-model-construction-defaults.create-default-idf.exact-family-order-count",
        "energy-model-create-default-idf",
        "EnergyModel.create_default_idf",
    ),
    (
        "dragon-model-construction-defaults.create-default-idf.fresh-mutation-isolation",
        "energy-model-create-default-idf",
        "EnergyModel.create_default_idf",
    ),
    (
        "dragon-model-construction-defaults.create-default-idf.global-schedule-raw-fields",
        "energy-model-create-default-idf",
        "EnergyModel.create_default_idf",
    ),
    (
        "dragon-model-construction-defaults.create-default-idf.output-objects",
        "energy-model-create-default-idf",
        "EnergyModel.create_default_idf",
    ),
    (
        "dragon-model-construction-defaults.init.call-shape-errors",
        "energy-model-init",
        "EnergyModel.__init__",
    ),
    (
        "dragon-model-construction-defaults.init.explicit-aliasing",
        "energy-model-init",
        "EnergyModel.__init__",
    ),
    (
        "dragon-model-construction-defaults.init.permissive-invalid-values",
        "energy-model-init",
        "EnergyModel.__init__",
    ),
    (
        "dragon-model-construction-defaults.init.shared-defaults-signature",
        "energy-model-init",
        "EnergyModel.__init__",
    ),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 9
EXPECTED_CASE_COUNTS = {
    "EnergyModel.__init__": 4,
    "EnergyModel.create_default_idf": 5,
}
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
        "_dragons_dragon_model_construction_defaults_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load construction-defaults oracle support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
    ):
        raise RuntimeError("Construction-defaults oracle support is not pinned.")
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


def _load_source_inventory(
    path: Path,
    upstream_commit: str,
    source: dict[str, Any],
) -> dict[str, Any]:
    symbols = tuple(source["symbols"])
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(SUPPORT, name) for name in names}
    try:
        SUPPORT.SOURCE_PATH = source["path"]
        SUPPORT.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        SUPPORT.EXPECTED_SYMBOL_HASHES = {
            symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"]
            for symbol in symbols
        }
        SUPPORT.TARGET_SYMBOLS = symbols
        inventory = SUPPORT.load_exact_inventory(path, upstream_commit)
    finally:
        for name, value in original.items():
            setattr(SUPPORT, name, value)
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
        for symbol in symbols
    ]
    if inventory["file"] != expected_file or inventory["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return inventory


def load_exact_inventory(path: Path, upstream_commit: str) -> dict[str, Any]:
    inventories = [
        _load_source_inventory(path, upstream_commit, source)
        for source in SOURCE_SPECS
    ]
    if any(
        item["content_sha256"] != EXPECTED_INVENTORY_SHA256
        for item in inventories
    ):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in inventories],
        "symbols": [symbol for item in inventories for symbol in item["symbols"]],
    }


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
    return tuple(_case(*binding) for binding in EXPECTED_CASE_BINDINGS)


def _string(value: str) -> dict[str, str]:
    return {"kind": "str", "value": value}


def _integer(value: int) -> dict[str, str]:
    return {"kind": "int", "repr": str(value)}


def _none() -> dict[str, str]:
    return {"kind": "none"}


def _encoded_values(*values: dict[str, str]) -> list[dict[str, str]]:
    return list(values)


EXPECTED_FLAT_OBJECT_TYPES = [
    "Version",
    "SimulationControl",
    "Timestep",
    "SizingPeriod:WeatherFileDays",
    "SizingPeriod:WeatherFileDays",
    "RunPeriod",
    "ScheduleTypeLimits",
    "ScheduleTypeLimits",
    "ScheduleTypeLimits",
    "ScheduleTypeLimits",
    "Schedule:Compact",
    "Schedule:Compact",
    "Schedule:Constant",
    "GlobalGeometryRules",
    "Output:Table:SummaryReports",
    "Output:Table:Monthly",
    "OutputControl:Table:Style",
]
EXPECTED_NONEMPTY_FAMILIES = [
    {"count": 1, "object_type": "Version"},
    {"count": 1, "object_type": "SimulationControl"},
    {"count": 1, "object_type": "Timestep"},
    {"count": 2, "object_type": "SizingPeriod:WeatherFileDays"},
    {"count": 1, "object_type": "RunPeriod"},
    {"count": 4, "object_type": "ScheduleTypeLimits"},
    {"count": 2, "object_type": "Schedule:Compact"},
    {"count": 1, "object_type": "Schedule:Constant"},
    {"count": 1, "object_type": "GlobalGeometryRules"},
    {"count": 1, "object_type": "Output:Table:SummaryReports"},
    {"count": 1, "object_type": "Output:Table:Monthly"},
    {"count": 1, "object_type": "OutputControl:Table:Style"},
]
SUMMARY_REPORTS = [
    "EndUseEnergyConsumptionElectricityMonthly",
    "EndUseEnergyConsumptionNaturalGasMonthly",
    "EndUseEnergyConsumptionDieselMonthly",
    "EndUseEnergyConsumptionFuelOilMonthly",
    "EndUseEnergyConsumptionCoalMonthly",
    "EndUseEnergyConsumptionPropaneMonthly",
    "EndUseEnergyConsumptionGasolineMonthly",
    "EndUseEnergyConsumptionOtherFuelsMonthly",
]


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return {
            "positional_argument_error_type": "TypeError",
            "signature_text": "() -> 'IDF'",
            "staticmethod_descriptor": True,
            "unexpected_keyword_error_type": "TypeError",
        }
    if identifier == EXPECTED_CASE_IDS[1]:
        return {
            "building_object_count": 0,
            "ensure_validity": False,
            "flat_object_types": EXPECTED_FLAT_OBJECT_TYPES,
            "nonempty_families": EXPECTED_NONEMPTY_FAMILIES,
            "object_count": 17,
            "version_components": [24, 2, 0],
            "version_field": {"kind": "float", "repr": "24.2"},
        }
    if identifier == EXPECTED_CASE_IDS[2]:
        return {
            "all_corresponding_objects_are_distinct": True,
            "first_allon_name_after_mutation": "MUTATED-🐉",
            "first_building_count_after_mutation": 1,
            "first_count_after_mutation": 18,
            "fresh_idf_instances": True,
            "shared_immutable_idd_schema": True,
            "second_allon_name_after_first_mutation": "ALLON",
            "second_building_count_after_first_mutation": 0,
            "second_count_after_first_mutation": 17,
        }
    if identifier == EXPECTED_CASE_IDS[3]:
        return {
            "compact_schedules": [
                {
                    "stored_field_count": 153,
                    "values": _encoded_values(
                        _string("ALLON"),
                        _none(),
                        _string("Through: 12/31"),
                        _string("For: AllDays"),
                        _string("Until: 24:00"),
                        _integer(1),
                    ),
                },
                {
                    "stored_field_count": 153,
                    "values": _encoded_values(
                        _string("ALLOFF"),
                        _none(),
                        _string("Through: 12/31"),
                        _string("For: AllDays"),
                        _string("Until: 24:00"),
                        _integer(0),
                    ),
                },
            ],
            "global_geometry_rules": _encoded_values(
                _string("UpperLeftCorner"),
                _string("Counterclockwise"),
                _string("World"),
                _string("Relative"),
                _string("Relative"),
            ),
            "people_activity": {
                "stored_field_count": 3,
                "values": [
                    _string("$DEFAULT$PEOPLEACTIVITY"),
                    {"enum_type": "ScheduleType", "kind": "enum", "text": "real", "value": "real"},
                    {"kind": "float", "repr": "107.0"},
                ],
            },
            "run_period": _encoded_values(
                _string("Year-Round"),
                _integer(1),
                _integer(1),
                _integer(2026),
                _integer(12),
                _integer(31),
                _integer(2026),
            ),
            "schedule_type_limits": [
                _encoded_values(
                    _string("ScheduleTypeLimits:Temperature"),
                    _integer(-50),
                    _integer(200),
                    _string("Continuous"),
                    _string("Temperature"),
                ),
                _encoded_values(
                    _string("ScheduleTypeLimits:Onoff"),
                    _integer(0),
                    _integer(1),
                    _string("Discrete"),
                    _string("Dimensionless"),
                ),
                _encoded_values(
                    _string("ScheduleTypeLimits:Fraction"),
                    _integer(0),
                    _integer(1),
                    _string("Continuous"),
                    _string("Dimensionless"),
                ),
                _encoded_values(
                    _string("ScheduleTypeLimits:Real"),
                    _none(),
                    _none(),
                    _string("Continuous"),
                    _string("Dimensionless"),
                ),
            ],
            "simulation_control": _encoded_values(
                _string("Yes"),
                _string("Yes"),
                _string("Yes"),
                _string("No"),
                _string("Yes"),
                _string("No"),
            ),
            "sizing_periods": [
                _encoded_values(
                    _string("DesignWinter"),
                    _integer(1),
                    _integer(1),
                    _integer(1),
                    _integer(31),
                ),
                _encoded_values(
                    _string("DesignSummer"),
                    _integer(8),
                    _integer(1),
                    _integer(8),
                    _integer(31),
                ),
            ],
            "timestep": _encoded_values(_integer(6)),
        }
    if identifier == EXPECTED_CASE_IDS[4]:
        return {
            "monthly": {
                "stored_field_count": 52,
                "values": _encoded_values(
                    _string("ElectricityBalanceMonthly"),
                    _integer(3),
                    _string("ElectricityProduced:Facility"),
                    _string("SumOrAverage"),
                    _string("ElectricitySurplusSold:Facility"),
                    _string("SumOrAverage"),
                    _string("ElectricityPurchased:Facility"),
                    _string("SumOrAverage"),
                ),
            },
            "style": {
                "stored_field_count": 2,
                "values": _encoded_values(_string("Comma"), _string("JtoKWH")),
            },
            "summary": {
                "stored_field_count": 25,
                "values": [_string(report) for report in SUMMARY_REPORTS],
            },
        }
    if identifier == EXPECTED_CASE_IDS[5]:
        return {
            "missing_name_error_type": "TypeError",
            "positional_pv_error_type": "TypeError",
            "unexpected_keyword_error_type": "TypeError",
        }
    if identifier == EXPECTED_CASE_IDS[6]:
        return {
            "explicit_pv_is_input_list": True,
            "explicit_zone_is_input_list": True,
            "input_mutation_visible_in_model": True,
            "model_mutation_visible_in_input": True,
            "pv_labels_after_bidirectional_mutation": [
                "pv:initial-🐉",
                "pv:input-appended",
                "pv:model-appended",
            ],
            "zone_labels_after_bidirectional_mutation": [
                "zone:initial-용",
                "zone:input-appended",
                "zone:model-appended",
            ],
        }
    if identifier == EXPECTED_CASE_IDS[7]:
        return {
            "constructed_without_error": True,
            "name_is_none": True,
            "north_axis_identity_preserved": True,
            "north_axis_type": "list",
            "pv_is_none": True,
            "terrain_identity_preserved": True,
            "terrain_type": "dict",
            "zone_identity_preserved": True,
            "zone_type": "str",
        }
    if identifier == EXPECTED_CASE_IDS[8]:
        return {
            "first_pv_is_second_pv": True,
            "first_zone_is_second_zone": True,
            "keyword_only_parameters": ["pv"],
            "positional_parameters": [
                "self",
                "name",
                "north_axis",
                "terrain",
                "zone",
            ],
            "pv_default_is_distinct_from_zone_default": True,
            "pv_mutation_visible_cross_instance": True,
            "shared_pv_default_restored": True,
            "shared_zone_default_restored": True,
            "signature_text": (
                "(self, name: 'str', north_axis: 'int | float' = 0, "
                "terrain: 'str' = <Terrain.SUBURBS: 'Suburbs'>, "
                "zone: 'list[Zone]' = [], *, "
                "pv: 'list[PhotoVoltaicPanel]' = [])"
            ),
            "zone_mutation_visible_cross_instance": True,
        }
    raise RuntimeError(f"Unknown construction-defaults case: {identifier}")


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


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(str(source["path"])).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches: list[Path] = []
    for entry in sys.path:
        if not entry:
            continue
        source_root = Path(entry)
        if all(
            _source_file(source_root, source).is_file()
            and sha256_file(_source_file(source_root, source))
            == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(source_root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


@contextmanager
def _pinned_modules(source_root: Path) -> Iterator[SimpleNamespace]:
    source_root = source_root.resolve()
    if any(
        sha256_file(_source_file(source_root, source)) != source["source_sha256"]
        for source in SOURCE_SPECS
    ):
        raise SystemExit("The selected construction-defaults sources are not pinned.")

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
        common = importlib.import_module("idragon.common")
        constants = importlib.import_module("idragon.constants")
        imugi = importlib.import_module("idragon.imugi")
        profile = importlib.import_module("idragon.dragon.profile")
        if Path(model.__file__).resolve() != _source_file(
            source_root, SOURCE_SPECS[2]
        ).resolve():
            raise SystemExit("Imported idragon.dragon.model is not pinned.")
        if not (
            model.IDF is imugi.IDF
            and model.IdfObject is imugi.IdfObject
            and model.Setting is common.Setting
            and model.THERMAL is constants.THERMAL
            and model.ScheduleType is profile.ScheduleType
        ):
            raise SystemExit("Pinned construction-defaults dependencies do not share identity.")
        yield SimpleNamespace(
            common=common,
            constants=constants,
            imugi=imugi,
            model=model,
            profile=profile,
        )
    finally:
        for name in list(sys.modules):
            if name == "idragon" or name.startswith("idragon."):
                sys.modules.pop(name, None)
        sys.modules.update(saved_modules)
        try:
            sys.path.remove(str(source_root))
        except ValueError:
            pass


def _error_type(callback: Any) -> str:
    try:
        callback()
    except Exception as error:  # noqa: BLE001 - exception type is the fixture fact.
        return type(error).__name__
    raise RuntimeError("The pinned call unexpectedly returned.")


def _encode_field(value: Any) -> dict[str, str]:
    if value is None:
        return _none()
    if isinstance(value, Enum):
        raw_value = value.value
        if not isinstance(raw_value, str):
            raise RuntimeError("Only string-enum IDF fields are expected.")
        return {
            "enum_type": type(value).__name__,
            "kind": "enum",
            "text": str(value),
            "value": raw_value,
        }
    if type(value) is bool:
        return {"kind": "bool", "repr": repr(value)}
    if type(value) is int:
        return _integer(value)
    if type(value) is float:
        return {"kind": "float", "repr": repr(value)}
    if type(value) is str:
        return _string(value)
    raise RuntimeError("Unexpected IDF field type: " + type(value).__name__)


def _trimmed_values(idf_object: Any) -> list[Any]:
    values = list(idf_object.values())
    while values and values[-1] is None:
        values.pop()
    return values


def _encoded_object_values(idf_object: Any) -> list[dict[str, str]]:
    return [_encode_field(value) for value in _trimmed_values(idf_object)]


def _execute_create_default(identifier: str, modules: SimpleNamespace) -> dict[str, Any]:
    energy_model = modules.model.EnergyModel
    if identifier == EXPECTED_CASE_IDS[0]:
        return {
            "positional_argument_error_type": _error_type(
                lambda: energy_model.create_default_idf(1)
            ),
            "signature_text": str(inspect.signature(energy_model.create_default_idf)),
            "staticmethod_descriptor": isinstance(
                energy_model.__dict__["create_default_idf"], staticmethod
            ),
            "unexpected_keyword_error_type": _error_type(
                lambda: energy_model.create_default_idf(unexpected=True)
            ),
        }

    if identifier == EXPECTED_CASE_IDS[1]:
        idf = energy_model.create_default_idf()
        flat = [
            item.idd.name
            for _, objects in idf.items()
            for item in objects
        ]
        families = [
            {"count": len(objects), "object_type": object_type}
            for object_type, objects in idf.items()
            if len(objects) > 0
        ]
        version = idf["Version"][0]["Version Identifier"]
        return {
            "building_object_count": len(idf["Building"]),
            "ensure_validity": idf.ensure_validity,
            "flat_object_types": flat,
            "nonempty_families": families,
            "object_count": len(idf),
            "version_components": list(idf.version),
            "version_field": _encode_field(version),
        }

    if identifier == EXPECTED_CASE_IDS[2]:
        first = energy_model.create_default_idf()
        second = energy_model.create_default_idf()
        corresponding_distinct = all(
            left is not right
            for object_type in first.keys()
            for left, right in zip(first[object_type], second[object_type])
        )
        first["Schedule:Compact"][0]["Name"] = "MUTATED-🐉"
        first["Building"].append(["MUTATION-BUILDING-🐉"])
        return {
            "all_corresponding_objects_are_distinct": corresponding_distinct,
            "first_allon_name_after_mutation": first["Schedule:Compact"][0]["Name"],
            "first_building_count_after_mutation": len(first["Building"]),
            "first_count_after_mutation": len(first),
            "fresh_idf_instances": first is not second,
            "shared_immutable_idd_schema": first.idd is second.idd,
            "second_allon_name_after_first_mutation": second["Schedule:Compact"][0]["Name"],
            "second_building_count_after_first_mutation": len(second["Building"]),
            "second_count_after_first_mutation": len(second),
        }

    idf = energy_model.create_default_idf()
    if identifier == EXPECTED_CASE_IDS[3]:
        return {
            "compact_schedules": [
                {
                    "stored_field_count": len(list(item.values())),
                    "values": _encoded_object_values(item),
                }
                for item in idf["Schedule:Compact"]
            ],
            "global_geometry_rules": _encoded_object_values(
                idf["GlobalGeometryRules"][0]
            ),
            "people_activity": {
                "stored_field_count": len(
                    list(idf["Schedule:Constant"][0].values())
                ),
                "values": _encoded_object_values(idf["Schedule:Constant"][0]),
            },
            "run_period": _encoded_object_values(idf["RunPeriod"][0]),
            "schedule_type_limits": [
                _encoded_object_values(item) for item in idf["ScheduleTypeLimits"]
            ],
            "simulation_control": _encoded_object_values(
                idf["SimulationControl"][0]
            ),
            "sizing_periods": [
                _encoded_object_values(item)
                for item in idf["SizingPeriod:WeatherFileDays"]
            ],
            "timestep": _encoded_object_values(idf["Timestep"][0]),
        }

    if identifier == EXPECTED_CASE_IDS[4]:
        summary = idf["Output:Table:SummaryReports"][0]
        monthly = idf["Output:Table:Monthly"][0]
        style = idf["OutputControl:Table:Style"][0]
        return {
            "monthly": {
                "stored_field_count": len(list(monthly.values())),
                "values": _encoded_object_values(monthly),
            },
            "style": {
                "stored_field_count": len(list(style.values())),
                "values": _encoded_object_values(style),
            },
            "summary": {
                "stored_field_count": len(list(summary.values())),
                "values": _encoded_object_values(summary),
            },
        }
    raise RuntimeError(f"Unknown create-default case: {identifier}")


def _label(item: Any) -> str:
    return item.label


def _execute_init(identifier: str, modules: SimpleNamespace) -> dict[str, Any]:
    energy_model = modules.model.EnergyModel
    if identifier == EXPECTED_CASE_IDS[5]:
        return {
            "missing_name_error_type": _error_type(lambda: energy_model()),
            "positional_pv_error_type": _error_type(
                lambda: energy_model(
                    "positional-pv",
                    0,
                    modules.model.Terrain.SUBURBS,
                    [],
                    [],
                )
            ),
            "unexpected_keyword_error_type": _error_type(
                lambda: energy_model("unexpected", unknown=True)
            ),
        }

    if identifier == EXPECTED_CASE_IDS[6]:
        zones = [SimpleNamespace(label="zone:initial-용")]
        panels = [SimpleNamespace(label="pv:initial-🐉")]
        model = energy_model("alias-용🐉", zone=zones, pv=panels)
        zones.append(SimpleNamespace(label="zone:input-appended"))
        panels.append(SimpleNamespace(label="pv:input-appended"))
        input_visible = len(model.zone) == 2 and len(model.pv) == 2
        model.zone.append(SimpleNamespace(label="zone:model-appended"))
        model.pv.append(SimpleNamespace(label="pv:model-appended"))
        model_visible = len(zones) == 3 and len(panels) == 3
        return {
            "explicit_pv_is_input_list": model.pv is panels,
            "explicit_zone_is_input_list": model.zone is zones,
            "input_mutation_visible_in_model": input_visible,
            "model_mutation_visible_in_input": model_visible,
            "pv_labels_after_bidirectional_mutation": [_label(item) for item in panels],
            "zone_labels_after_bidirectional_mutation": [_label(item) for item in zones],
        }

    if identifier == EXPECTED_CASE_IDS[7]:
        north = ["north-🐉"]
        terrain = {"terrain-용": True}
        zones = "zones-not-a-list-🐉"
        model = energy_model(
            None,
            north_axis=north,
            terrain=terrain,
            zone=zones,
            pv=None,
        )
        return {
            "constructed_without_error": True,
            "name_is_none": model.name is None,
            "north_axis_identity_preserved": model.north_axis is north,
            "north_axis_type": type(model.north_axis).__name__,
            "pv_is_none": model.pv is None,
            "terrain_identity_preserved": model.terrain is terrain,
            "terrain_type": type(model.terrain).__name__,
            "zone_identity_preserved": model.zone is zones,
            "zone_type": type(model.zone).__name__,
        }

    if identifier == EXPECTED_CASE_IDS[8]:
        signature = inspect.signature(energy_model.__init__)
        zone_default = energy_model.__init__.__defaults__[2]
        pv_default = energy_model.__init__.__kwdefaults__["pv"]
        if zone_default or pv_default:
            raise RuntimeError("Pinned constructor defaults were not pristine.")
        first = energy_model("shared:first")
        second = energy_model("shared:second")
        zone_marker = SimpleNamespace(label="shared-zone-용")
        pv_marker = SimpleNamespace(label="shared-pv-🐉")
        try:
            first.zone.append(zone_marker)
            first.pv.append(pv_marker)
            zone_visible = len(second.zone) == 1 and second.zone[0] is zone_marker
            pv_visible = len(second.pv) == 1 and second.pv[0] is pv_marker
        finally:
            zone_default.clear()
            pv_default.clear()
        positional = [
            name
            for name, parameter in signature.parameters.items()
            if parameter.kind
            in (
                inspect.Parameter.POSITIONAL_ONLY,
                inspect.Parameter.POSITIONAL_OR_KEYWORD,
            )
        ]
        keyword_only = [
            name
            for name, parameter in signature.parameters.items()
            if parameter.kind is inspect.Parameter.KEYWORD_ONLY
        ]
        return {
            "first_pv_is_second_pv": first.pv is second.pv,
            "first_zone_is_second_zone": first.zone is second.zone,
            "keyword_only_parameters": keyword_only,
            "positional_parameters": positional,
            "pv_default_is_distinct_from_zone_default": pv_default is not zone_default,
            "pv_mutation_visible_cross_instance": pv_visible,
            "shared_pv_default_restored": pv_default == [],
            "shared_zone_default_restored": zone_default == [],
            "signature_text": str(signature),
            "zone_mutation_visible_cross_instance": zone_visible,
        }
    raise RuntimeError(f"Unknown init case: {identifier}")


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _expected_symbol_descriptors() -> list[dict[str, Any]]:
    return [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": MODEL_SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]


def _expected_files() -> list[dict[str, Any]]:
    return [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
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
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "assertion_ids": EXPECTED_ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {
            symbol: (
                "exception"
                if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS
                else "equivalent"
            )
            for symbol in TARGET_SYMBOLS
        },
        "identity_encoding": "logical-label-and-boolean-only-no-id-or-address",
        "raw_field_encoding": "typed-kind-plus-value-or-repr-with-trailing-none-trimmed",
        "runtime_names": "pinned-python-builtins-and-enums-only-no-native-type-name-claims",
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _expected_runtime() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def build_oracle(
    inventory: dict[str, Any],
    commit: str,
    source_root: Path | None = None,
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
    }:
        raise SystemExit("The aggregate construction-defaults inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with _pinned_modules(imported_root) as modules:
        cases: list[dict[str, Any]] = []
        for definition in case_definitions():
            identifier = definition["id"]
            if definition["executor"] == "energy-model-create-default-idf":
                facts = _execute_create_default(identifier, modules)
            elif definition["executor"] == "energy-model-init":
                facts = _execute_init(identifier, modules)
            else:
                raise SystemExit(
                    "Unknown construction-defaults executor: " + definition["executor"]
                )
            if facts != expected_facts(identifier):
                raise SystemExit(
                    f"Pinned Python construction-defaults semantics drifted: {identifier}\n"
                    + strict_json_dumps(facts, indent=2)
                )
            case = dict(definition)
            case["python"] = {"facts": facts, "outcome": "returned"}
            cases.append(case)

    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": _expected_consumer_contract(),
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
            **_expected_upstream(),
            "commit": commit,
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
    raise RuntimeError(f"Unsupported JSON value at {location}: {type(value).__name__}")


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
        raise RuntimeError("Construction-defaults schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Construction-defaults cases hash drifted.")
    _validate_safe_tree(value)

    definitions = case_definitions()
    cases = value["cases"]
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Construction-defaults case order/count drifted.")
    if [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("Construction-defaults case order/count drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS):
        raise RuntimeError("Pinned construction-defaults case IDs are not sorted.")
    if len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Pinned construction-defaults case IDs are not unique.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Construction-defaults per-symbol case counts drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Construction-defaults case contract drifted: {case['id']}")
        if "expected_dotnet" in case:
            _require_keys(
                case["expected_dotnet"],
                {"adaptation", "outcome"},
                "expected_dotnet",
            )
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned":
            raise RuntimeError(f"Python case outcome drifted: {case['id']}")
        if case["python"]["facts"] != expected_facts(case["id"]):
            raise RuntimeError(f"Construction-defaults semantics drifted: {case['id']}")

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Construction-defaults consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Construction-defaults runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Construction-defaults upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Construction-defaults symbol receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for the construction-defaults oracle.")
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
    print(f"Wrote dragon model construction-defaults oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

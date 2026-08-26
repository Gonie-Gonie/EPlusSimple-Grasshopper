"""Generate pinned IDF observations for the legacy HVAC source-system family.

The corpus executes exactly thirteen methods from ``idragon.dragon.hvac`` at
EPlusSimple 0.7.0.  It records complete ordered ``IdfObject`` fields for a
bounded twenty-case valid-state matrix.  Constructors, validation/error
semantics, unrelated source systems, parent model assembly, and native IDD
compaction remain explicit closure boundaries.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import inspect
import math
import os
from pathlib import Path
import sys
from types import SimpleNamespace
from typing import Any, Callable


SCHEMA = "goniegonie.python-reference.dragon-hvac-source-system-to-idf-object.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:fdafc8752a9f1bee90b1d2099274899d74ab7e6fb47738211918d683d7cf82b0"
)
HVAC_SOURCE_PATH = "src/idragon/dragon/hvac.py"
EXPECTED_HVAC_SOURCE_SHA256 = (
    "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0"
)
EXPECTED_HVAC_AST_SHA256 = (
    "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31"
)
EXPECTED_SYMBOL_RECEIPTS = {
    "AbsorptionChiller.to_idf_object": {
        "body_hash": "sha256:235dd3954501399871a0317fb9665091c192830665e9f95baae7eb9a3d80823b",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:17d5fb8afe2207a9772bc47b4f5424d740b3df76301f04c9155c0fbd725af969",
    },
    "Boiler.to_idf_object": {
        "body_hash": "sha256:416b84930ac077833ccee544e07d01ea1e542df536d32b8a1d7c18b4a94725ed",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:b63a454be07eaaee80563cbac25cd78a3fb632e462e2ea37aed7906c2967a7ae",
    },
    "Boiler.to_idf_object_as_generator": {
        "body_hash": "sha256:32eebbda47034344f145d801a729648469cd7b24e0af847d97c1f9b6b7294cf2",
        "kind": "function",
        "signature_hash": "sha256:b39bcfffa903f90ee98ddd5d79d4b6827d2e526aaa6acabe5667e446c80794c3",
        "symbol_hash": "sha256:d239b10e14f899ec4f7d9d914e7322fd684d3cfe5096609119f32eef9dc79aa0",
    },
    "Chiller.to_idf_object": {
        "body_hash": "sha256:72ef316133dec589de7deec328090b0d097b8370726a5c360d73953dd6dc9f25",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:fc75129f85debd982652240620407bcb408a73fcf5fef197871599da771e34d3",
    },
    "ClosedSingleSpeedCoolingTower.to_idf_main_object": {
        "body_hash": "sha256:330c3494967559c366002476ef010eb8be6c27fde0e918832932f4c2daeb6162",
        "kind": "function",
        "signature_hash": "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
        "symbol_hash": "sha256:0e14065ae1ca788b3219a54f5d1ae41d7783e0dd6497667cf583e7387e0396d8",
    },
    "ClosedTwoSpeedCoolingTower.to_idf_main_object": {
        "body_hash": "sha256:13c013239561f33e2d0ae10cde93531feea0f97a1557337402e2afe8407e2a0d",
        "kind": "function",
        "signature_hash": "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
        "symbol_hash": "sha256:30402683c6a9db760ad1727995d72c8357b93cf5704625779e5ce43b907739ae",
    },
    "CompressorType.to_idf_curve_object": {
        "body_hash": "sha256:eba2dfb849d0251170f777cb131385bbaf7316d8231cba3d096d241ce9ddce00",
        "kind": "function",
        "signature_hash": "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
        "symbol_hash": "sha256:8ca6c2d070a534718d90fe79dff5d8a1e015593a0551a5a53ec3bf1c3e932d81",
    },
    "CoolingTower.to_idf_main_object": {
        "body_hash": "sha256:d534464a3d86dfeb1f92e18bf2296fb90a71ce3810ab72675ff520fac00f4ce1",
        "kind": "function",
        "signature_hash": "sha256:679b45a374ed222434707e448b38c110efb7b0d13bc0089cadaaf661a48c7708",
        "symbol_hash": "sha256:4615e08c6ec284f9bac80d2a5f25beca2b9706f4c706e0b47cf27ab35c2c5915",
    },
    "CoolingTower.to_idf_object": {
        "body_hash": "sha256:77fa14ec4670bce06dd78b19a08c5be26bc01e0d947ed0ade6155954879d6b3f",
        "kind": "function",
        "signature_hash": "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
        "symbol_hash": "sha256:74287ab5af4712528e239034183e43122280dcf9760ebece16161e93c629c762",
    },
    "HeatPump.to_idf_object": {
        "body_hash": "sha256:601ab95c68822d4e94a03062618d0a29ab4b7c0c8f529742d9e1bd99ed850311",
        "kind": "function",
        "signature_hash": "sha256:9ce384ca48519051591ce6adac791b33a19b891ac5626bde847d37298c470519",
        "symbol_hash": "sha256:b8cb28ab0ec6d2775a69548b0b5d7983afa38e0f980ec1e1835d40ccd1edacb1",
    },
    "OpenSingleSpeedCoolingTower.to_idf_main_object": {
        "body_hash": "sha256:6f24857ae8cda107880f2bf123e2401b2c1ab4ffffdf3b224195350b3465bfb5",
        "kind": "function",
        "signature_hash": "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
        "symbol_hash": "sha256:102bccd9091484e0f915dc24010d22c22a91c69b95a17e10f44ab7d6b189e61f",
    },
    "OpenTwoSpeedCoolingTower.to_idf_main_object": {
        "body_hash": "sha256:a4e24f89a146eae7181177115cce1a89842869e9bd70e9f54e2b45e8bc6ead73",
        "kind": "function",
        "signature_hash": "sha256:61ff646496aae3a4e3f5a07c18df33ddc0a2dd1cbeaca102be9de4f1da30f573",
        "symbol_hash": "sha256:7fd75338aa5a98323eb0d3cfeac729d921c00f95e91f7e03cfddf4b2b885e736",
    },
    "SourceSystem.to_idf_object": {
        "body_hash": "sha256:d534464a3d86dfeb1f92e18bf2296fb90a71ce3810ab72675ff520fac00f4ce1",
        "kind": "function",
        "signature_hash": "sha256:d62b0f5a2745a3f0d6f1ace245fbc66899d0e8953e93173c8f4d815eec741a50",
        "symbol_hash": "sha256:63aa5eab420418dc4467359ae79d5b1b0b59f1a0501e6e5953039b3a3adfb57b",
    },
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)

ADAPTATIONS = {
    "AbsorptionChiller.to_idf_object": "legacy-context-absorption-chiller-idf-emission",
    "Boiler.to_idf_object": "compact-native-boiler-idf-emission",
    "Boiler.to_idf_object_as_generator": "immutable-native-boiler-generator-idf-emission",
    "Chiller.to_idf_object": "legacy-context-chiller-idf-emission",
    "ClosedSingleSpeedCoolingTower.to_idf_main_object": "cooling-tower-context-closed-single-speed-main-idf-emission",
    "ClosedTwoSpeedCoolingTower.to_idf_main_object": "cooling-tower-context-closed-two-speed-main-idf-emission",
    "CompressorType.to_idf_curve_object": "chiller-context-compressor-curve-idf-emission",
    "CoolingTower.to_idf_main_object": "contextual-native-cooling-tower-main-idf-contract",
    "CoolingTower.to_idf_object": "legacy-context-cooling-tower-idf-emission",
    "HeatPump.to_idf_object": "compact-native-heat-pump-idf-emission",
    "OpenSingleSpeedCoolingTower.to_idf_main_object": "cooling-tower-context-open-single-speed-main-idf-emission",
    "OpenTwoSpeedCoolingTower.to_idf_main_object": "cooling-tower-context-open-two-speed-main-idf-emission",
    "SourceSystem.to_idf_object": "contextual-native-source-system-idf-contract",
}
SYMBOL_SLUGS = {
    "AbsorptionChiller.to_idf_object": "absorption-chiller-to-idf-object",
    "Boiler.to_idf_object": "boiler-to-idf-object",
    "Boiler.to_idf_object_as_generator": "boiler-to-idf-object-as-generator",
    "Chiller.to_idf_object": "chiller-to-idf-object",
    "ClosedSingleSpeedCoolingTower.to_idf_main_object": "closed-single-speed-cooling-tower-to-idf-main-object",
    "ClosedTwoSpeedCoolingTower.to_idf_main_object": "closed-two-speed-cooling-tower-to-idf-main-object",
    "CompressorType.to_idf_curve_object": "compressor-type-to-idf-curve-object",
    "CoolingTower.to_idf_main_object": "cooling-tower-to-idf-main-object",
    "CoolingTower.to_idf_object": "cooling-tower-to-idf-object",
    "HeatPump.to_idf_object": "heat-pump-to-idf-object",
    "OpenSingleSpeedCoolingTower.to_idf_main_object": "open-single-speed-cooling-tower-to-idf-main-object",
    "OpenTwoSpeedCoolingTower.to_idf_main_object": "open-two-speed-cooling-tower-to-idf-main-object",
    "SourceSystem.to_idf_object": "source-system-to-idf-object",
}
ASSERTION_IDS = {
    symbol: f"dragon-hvac-{SYMBOL_SLUGS[symbol]}-{receipt['symbol_hash'][7:15]}"
    for symbol, receipt in EXPECTED_SYMBOL_RECEIPTS.items()
}
NATIVE_TARGETS = {
    "AbsorptionChiller.to_idf_object": "AbsorptionChiller.ToIdfObjects legacy context",
    "Boiler.to_idf_object": "Boiler.ToIdfObjects",
    "Boiler.to_idf_object_as_generator": "Boiler.ToIdfObjects with generator demand connection",
    "Chiller.to_idf_object": "Chiller.ToIdfObjects legacy context",
    "ClosedSingleSpeedCoolingTower.to_idf_main_object": "ClosedSingleSpeedCoolingTower.ToIdfObjects",
    "ClosedTwoSpeedCoolingTower.to_idf_main_object": "ClosedTwoSpeedCoolingTower.ToIdfObjects",
    "CompressorType.to_idf_curve_object": "Chiller.ToIdfObjects compressor curve slice",
    "CoolingTower.to_idf_main_object": "CoolingTower main-object contract in chiller context",
    "CoolingTower.to_idf_object": "CoolingTower.ToIdfObjects legacy context",
    "HeatPump.to_idf_object": "HeatPump.ToIdfObjects",
    "OpenSingleSpeedCoolingTower.to_idf_main_object": "OpenSingleSpeedCoolingTower.ToIdfObjects",
    "OpenTwoSpeedCoolingTower.to_idf_main_object": "OpenTwoSpeedCoolingTower.ToIdfObjects",
    "SourceSystem.to_idf_object": "SourceSystem.ToIdfObjects abstract contract",
}

PREFIX = "dragon-hvac-source-system-to-idf-object."
EXPECTED_CASE_BINDINGS = (
    (PREFIX + "absorption-chiller.alternate-setpoint", "AbsorptionChiller.to_idf_object"),
    (PREFIX + "absorption-chiller.representative", "AbsorptionChiller.to_idf_object"),
    (PREFIX + "boiler-generator.topology", "Boiler.to_idf_object_as_generator"),
    (PREFIX + "boiler.autosized-natural-gas", "Boiler.to_idf_object"),
    (PREFIX + "boiler.explicit-propane", "Boiler.to_idf_object"),
    (PREFIX + "chiller.alternate-setpoint", "Chiller.to_idf_object"),
    (PREFIX + "chiller.representative", "Chiller.to_idf_object"),
    (PREFIX + "compressor.reciprocating", "CompressorType.to_idf_curve_object"),
    (PREFIX + "compressor.screw", "CompressorType.to_idf_curve_object"),
    (PREFIX + "compressor.turbo", "CompressorType.to_idf_curve_object"),
    (PREFIX + "cooling-tower-full.closed-two-speed", "CoolingTower.to_idf_object"),
    (PREFIX + "cooling-tower-full.open-single-speed", "CoolingTower.to_idf_object"),
    (PREFIX + "cooling-tower-main.abstract-contract", "CoolingTower.to_idf_main_object"),
    (PREFIX + "cooling-tower-main.closed-single-speed", "ClosedSingleSpeedCoolingTower.to_idf_main_object"),
    (PREFIX + "cooling-tower-main.closed-two-speed", "ClosedTwoSpeedCoolingTower.to_idf_main_object"),
    (PREFIX + "cooling-tower-main.open-single-speed", "OpenSingleSpeedCoolingTower.to_idf_main_object"),
    (PREFIX + "cooling-tower-main.open-two-speed", "OpenTwoSpeedCoolingTower.to_idf_main_object"),
    (PREFIX + "heat-pump.explicit-capacities", "HeatPump.to_idf_object"),
    (PREFIX + "heat-pump.representative-autosize", "HeatPump.to_idf_object"),
    (PREFIX + "source-system.abstract-contract", "SourceSystem.to_idf_object"),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 20
EXPECTED_CASE_COUNTS = {
    "AbsorptionChiller.to_idf_object": 2,
    "Boiler.to_idf_object": 2,
    "Boiler.to_idf_object_as_generator": 1,
    "Chiller.to_idf_object": 2,
    "ClosedSingleSpeedCoolingTower.to_idf_main_object": 1,
    "ClosedTwoSpeedCoolingTower.to_idf_main_object": 1,
    "CompressorType.to_idf_curve_object": 3,
    "CoolingTower.to_idf_main_object": 1,
    "CoolingTower.to_idf_object": 2,
    "HeatPump.to_idf_object": 2,
    "OpenSingleSpeedCoolingTower.to_idf_main_object": 1,
    "OpenTwoSpeedCoolingTower.to_idf_main_object": 1,
    "SourceSystem.to_idf_object": 1,
}

# Independently frozen from the first pinned execution.  These bind live facts,
# rather than trusting the fixture that this generator writes.
EXPECTED_FACT_SHA256 = {
    EXPECTED_CASE_IDS[0]: "sha256:5c71ceb16217b251c7282d4b4a0ca6a620e16cd2b2143013df889721c2cea768",
    EXPECTED_CASE_IDS[1]: "sha256:24c044caaefef0b4eb7ab83a5ef24414e80b223bb4bfc63f74404be394d38fd4",
    EXPECTED_CASE_IDS[2]: "sha256:2e69baa1fe4fe84418a37edc480c413659f08a90edf1250f2c4a0fa198edb9e6",
    EXPECTED_CASE_IDS[3]: "sha256:d84a073bf7a1e735bf3af15988a26ee2933b77e29b706af796a4a94060ca47b7",
    EXPECTED_CASE_IDS[4]: "sha256:774ec151da44e025ebb6d607b2569111fb93243c5fe2a1c55350c66da30654c1",
    EXPECTED_CASE_IDS[5]: "sha256:e35658f7a4083b465179540af9cf760ddc8b608cfaff83b70eca9a6f66318011",
    EXPECTED_CASE_IDS[6]: "sha256:047d102db517aad7aa28a9d905ed2c1fc66241a7367c64db4349f3594fec9319",
    EXPECTED_CASE_IDS[7]: "sha256:48349b86be34f8e4c49219912912a6fb2f4551d22fb8695e4d37950f7845e6c1",
    EXPECTED_CASE_IDS[8]: "sha256:7bd3100ca6041d3ecb230da70caff7dce7752c551630c9200124700aae3a3a71",
    EXPECTED_CASE_IDS[9]: "sha256:9a2e805f0761f2859c1f2b2baca5a8023586b8e4d443116d892384d2c48da2ff",
    EXPECTED_CASE_IDS[10]: "sha256:106fdf88789a60b3f4d1d8066bcc8ea56db122213809ccf574f0d57fdd04ea06",
    EXPECTED_CASE_IDS[11]: "sha256:67a93158f8debcaa611c61ee000b9ec74d78328becab32c3d4393d6b71c9dc25",
    EXPECTED_CASE_IDS[12]: "sha256:096dfdc0db0a7f55451a2a0b70abf49fad5776fb2c4d215bffcb459a3fcc18fd",
    EXPECTED_CASE_IDS[13]: "sha256:653bf79e5b03fa8bfa38f56ce4b29ccb57cdd602407bb77c37a4f23db6874744",
    EXPECTED_CASE_IDS[14]: "sha256:9e1aea610dc00251cd8febe12f8e1abed12abb06df8dea3fd4784b451d89a808",
    EXPECTED_CASE_IDS[15]: "sha256:5a0d12780bfa8680bcc7392fbc14db40feb0f7f6a77bcee28e85c815585118bf",
    EXPECTED_CASE_IDS[16]: "sha256:fa85f9673cdfc4d1dccb2374f7bac51ab52a443098c089658bafa6b191e544dd",
    EXPECTED_CASE_IDS[17]: "sha256:932ffe33dcc60f78fd4b5d9790d3cb46445b195daa1a9a72e610168719584014",
    EXPECTED_CASE_IDS[18]: "sha256:cc5420bb6e32d3031f7f50c7e3f51d8ab5b0c3a73bc07c12b8d9acdb059c5c1d",
    EXPECTED_CASE_IDS[19]: "sha256:d3a57cd9f36787a09e87def0892c959494f4637bd3513dcf610dbf37e74caac6",
}

_COMMON_PIPES_BRANCHES = (
    "Pump:VariableSpeed",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Branch",
    "Branch",
    "Branch",
    "Branch",
    "BranchList",
    "Branch",
    "Branch",
    "Branch",
    "BranchList",
    "Connector:Splitter",
    "Connector:Mixer",
    "ConnectorList",
    "Connector:Splitter",
    "Connector:Mixer",
    "ConnectorList",
)
_HEATING_CONTROL_TYPES = (
    "PlantEquipmentList",
    "PlantEquipmentOperation:HeatingLoad",
    "PlantEquipmentOperationSchemes",
    "Schedule:Constant",
    "SetpointManager:Scheduled",
    "AvailabilityManager:Scheduled",
    "AvailabilityManagerAssignmentList",
)
_COOLING_CONTROL_TYPES = (
    "PlantEquipmentList",
    "PlantEquipmentOperation:CoolingLoad",
    "PlantEquipmentOperationSchemes",
    "Schedule:Constant",
    "SetpointManager:Scheduled",
    "AvailabilityManager:Scheduled",
    "AvailabilityManagerAssignmentList",
)
_TOWER_AFTER_MAIN_TYPES = (
    "Pump:VariableSpeed",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Pipe:Adiabatic",
    "Branch",
    "Branch",
    "Branch",
    "Branch",
    "BranchList",
    "Branch",
    "Branch",
    "Branch",
    "Branch",
    "BranchList",
    "Connector:Splitter",
    "Connector:Mixer",
    "ConnectorList",
    "Connector:Splitter",
    "Connector:Mixer",
    "ConnectorList",
    "CondenserEquipmentList",
    "PlantEquipmentOperation:CoolingLoad",
    "CondenserEquipmentOperationSchemes",
    "SetpointManager:FollowOutdoorAirTemperature",
    "CondenserLoop",
    "Sizing:Plant",
)
_BOILER_TYPES = (
    "Boiler:HotWater",
) + _COMMON_PIPES_BRANCHES + _HEATING_CONTROL_TYPES + ("PlantLoop", "Sizing:Plant")
_COOLING_PREFIX_TYPES = (
    "Chiller:Electric:EIR",
) + _COMMON_PIPES_BRANCHES + _COOLING_CONTROL_TYPES
_ABSORPTION_PREFIX_TYPES = (
    "Chiller:Absorption",
) + _COMMON_PIPES_BRANCHES + _COOLING_CONTROL_TYPES
_HEAT_PUMP_TYPES = (
    "Curve:Biquadratic",
    "Curve:Cubic",
    "Curve:Biquadratic",
    "Curve:Biquadratic",
    "Curve:Cubic",
    "Curve:Biquadratic",
    "Curve:Cubic",
    "Curve:Linear",
    "Curve:Linear",
    "Curve:Linear",
    "Curve:Biquadratic",
    "Curve:Cubic",
    "Curve:Biquadratic",
    "Curve:Biquadratic",
    "Curve:Cubic",
    "Curve:Biquadratic",
    "Curve:Cubic",
    "Curve:Quadratic",
    "Curve:Linear",
    "Curve:Linear",
    "ZoneTerminalUnitList",
    "AirConditioner:VariableRefrigerantFlow",
)


def _tower_types(main_type: str) -> tuple[str, ...]:
    return (main_type,) + _TOWER_AFTER_MAIN_TYPES


def _chiller_types(curve_tail: str, tower_type: str) -> tuple[str, ...]:
    return (
        "Curve:Biquadratic",
        "Curve:Biquadratic",
        curve_tail,
    ) + _tower_types(tower_type) + _COOLING_PREFIX_TYPES + ("PlantLoop", "Sizing:Plant")


def _absorption_types(tower_type: str) -> tuple[str, ...]:
    return (
        _ABSORPTION_PREFIX_TYPES
        + _BOILER_TYPES
        + ("Branch",)
        + _tower_types(tower_type)
        + ("PlantLoop", "Sizing:Plant")
    )


EXPECTED_OBJECT_TYPES = {
    EXPECTED_CASE_IDS[0]: _absorption_types("FluidCooler:TwoSpeed"),
    EXPECTED_CASE_IDS[1]: _absorption_types("CoolingTower:SingleSpeed"),
    EXPECTED_CASE_IDS[2]: _BOILER_TYPES + ("Branch",),
    EXPECTED_CASE_IDS[3]: _BOILER_TYPES,
    EXPECTED_CASE_IDS[4]: _BOILER_TYPES,
    EXPECTED_CASE_IDS[5]: _chiller_types("Curve:Quadratic", "FluidCooler:SingleSpeed"),
    EXPECTED_CASE_IDS[6]: _chiller_types("Curve:Quadratic", "CoolingTower:TwoSpeed"),
    EXPECTED_CASE_IDS[7]: (
        "Curve:Biquadratic",
        "Curve:Biquadratic",
        "Curve:Quadratic",
    ),
    EXPECTED_CASE_IDS[8]: (
        "Curve:Biquadratic",
        "Curve:Biquadratic",
        "Curve:Bicubic",
    ),
    EXPECTED_CASE_IDS[9]: (
        "Curve:Biquadratic",
        "Curve:Biquadratic",
        "Curve:Quadratic",
    ),
    EXPECTED_CASE_IDS[10]: _tower_types("FluidCooler:TwoSpeed"),
    EXPECTED_CASE_IDS[11]: _tower_types("CoolingTower:SingleSpeed"),
    EXPECTED_CASE_IDS[12]: (),
    EXPECTED_CASE_IDS[13]: ("FluidCooler:SingleSpeed",),
    EXPECTED_CASE_IDS[14]: ("FluidCooler:TwoSpeed",),
    EXPECTED_CASE_IDS[15]: ("CoolingTower:SingleSpeed",),
    EXPECTED_CASE_IDS[16]: ("CoolingTower:TwoSpeed",),
    EXPECTED_CASE_IDS[17]: _HEAT_PUMP_TYPES,
    EXPECTED_CASE_IDS[18]: _HEAT_PUMP_TYPES,
    EXPECTED_CASE_IDS[19]: (),
}

REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_core_support() -> Any:
    path = Path(__file__).resolve().with_name(
        "generate_dragon_hvac_supply_group_core_oracle.py"
    )
    spec = importlib.util.spec_from_file_location(
        "_goniegonie_source_system_idf_support", path
    )
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load source-system IDF support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    hvac_receipts = [item for item in module.SOURCE_RECEIPTS if item[0] == HVAC_SOURCE_PATH]
    if (
        module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT
        or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256
        or len(module.SOURCE_RECEIPTS) != 12
        or hvac_receipts
        != [(HVAC_SOURCE_PATH, EXPECTED_HVAC_AST_SHA256, EXPECTED_HVAC_SOURCE_SHA256)]
    ):
        raise RuntimeError("Source-system IDF support is not exactly pinned.")
    return module


CORE = _load_core_support()
SUPPORT = CORE.SUPPORT
SOURCE_RECEIPTS = CORE.SOURCE_RECEIPTS
SOURCE_SPECS = tuple(
    {
        "ast_sha256": ast_hash,
        "path": path,
        "source_sha256": source_hash,
        "symbols": TARGET_SYMBOLS if path == HVAC_SOURCE_PATH else (),
    }
    for path, ast_hash, source_hash in SOURCE_RECEIPTS
)
EXPECTED_DEPENDENCIES = CORE.EXPECTED_DEPENDENCIES
strict_json_dumps = CORE.strict_json_dumps
canonical_sha256 = CORE.canonical_sha256
sha256_file = CORE.sha256_file
load_json_without_duplicates = CORE.load_json_without_duplicates
RAW_ADDRESS_PATTERN = CORE.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = CORE.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = CORE.GUID_PATTERN
TIMESTAMP_PATTERN = CORE.TIMESTAMP_PATTERN


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _load_source_inventory(
    path: Path, commit: str, source: dict[str, Any]
) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    names = (
        "SOURCE_PATH",
        "EXPECTED_SOURCE_SHA256",
        "EXPECTED_SYMBOL_HASHES",
        "TARGET_SYMBOLS",
    )
    original = {name: getattr(helper, name) for name in names}
    try:
        helper.SOURCE_PATH = source["path"]
        helper.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        helper.EXPECTED_SYMBOL_HASHES = {
            symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"]
            for symbol in source["symbols"]
        }
        helper.TARGET_SYMBOLS = tuple(source["symbols"])
        result = helper.load_exact_inventory(path, commit)
    finally:
        for name, value in original.items():
            setattr(helper, name, value)
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
        for symbol in source["symbols"]
    ]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    items = [_load_source_inventory(path, commit, source) for source in SOURCE_SPECS]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in items):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": [item["file"] for item in items],
        "symbols": [symbol for item in items for symbol in item["symbols"]],
    }


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(
        {
            "executor": "hvac-source-system-to-idf-object",
            "expected_dotnet": {
                "adaptation": ADAPTATIONS[symbol],
                "outcome": "returned",
            },
            "id": identifier,
            "symbol": symbol,
        }
        for identifier, symbol in EXPECTED_CASE_BINDINGS
    )


def _encode(value: Any) -> dict[str, Any]:
    if value is None:
        return {"kind": "none"}
    if isinstance(value, bool):
        return {"kind": "bool", "value": value}
    if isinstance(value, int):
        return {"kind": "int", "value": str(value)}
    if isinstance(value, float):
        return {"hex": value.hex(), "kind": "float", "repr": repr(value)}
    if isinstance(value, str):
        return {"kind": "str", "value": value}
    raise RuntimeError(f"Unsupported observed value: {type(value).__name__}")


def _field(name: str, value: Any) -> dict[str, Any]:
    return {"name": name, "value": _encode(value)}


def _ordered_fields(value: Any) -> list[dict[str, Any]]:
    return [_field(name, field_value) for name, field_value in value.data.items()]


def _object_record(value: Any) -> dict[str, Any]:
    return {
        "field_count": len(value.data),
        "object_type": value.idd.name,
        "ordered_fields": _ordered_fields(value),
    }


def _emission(call: Callable[[], Any]) -> dict[str, Any]:
    first = call()
    second = call()
    if not isinstance(first, list) or not isinstance(second, list):
        raise RuntimeError("A concrete source-system emitter must return a list.")
    if len(first) != len(second):
        raise RuntimeError("Source-system IDF result shape changed between calls.")
    return {
        "all_allowed_fields_covered_in_order": all(
            list(item.data) == list(item.allowed_keys) for item in first
        ),
        "first_object_records": [_object_record(item) for item in first],
        "first_objects_pairwise_distinct": len({id(item) for item in first})
        == len(first),
        "fresh_idf_object_flags": [
            left is not right for left, right in zip(first, second, strict=True)
        ],
        "fresh_result_list": first is not second,
        "fresh_return_value": first is not second,
        "object_count": len(first),
        "object_types": [item.idd.name for item in first],
        "result_type": "list",
        "same_idd_definition_flags": [
            left.idd is right.idd for left, right in zip(first, second, strict=True)
        ],
        "second_fields_equal_flags": [
            list(left.data.items()) == list(right.data.items())
            for left, right in zip(first, second, strict=True)
        ],
        "second_objects_pairwise_distinct": len({id(item) for item in second})
        == len(second),
    }


def _abstract_facts(owner: type[Any], method_name: str) -> dict[str, Any]:
    method = owner.__dict__[method_name]
    first = method(object()) if owner.__name__ == "SourceSystem" else method(object(), object())
    second = method(object()) if owner.__name__ == "SourceSystem" else method(object(), object())
    state = [
        _field("declaring_type", owner.__name__),
        _field("method_name", method_name),
        _field("is_abstract_method", bool(getattr(method, "__isabstractmethod__", False))),
        _field("signature", str(inspect.signature(method))),
    ]
    return {
        "input_context": {
            "captured_state_scope": "abstract-method-descriptor-and-direct-body-return",
            "source_state": state,
            "source_state_unchanged_after_two_emissions": state
            == [
                _field("declaring_type", owner.__name__),
                _field("method_name", method_name),
                _field(
                    "is_abstract_method",
                    bool(getattr(owner.__dict__[method_name], "__isabstractmethod__", False)),
                ),
                _field("signature", str(inspect.signature(owner.__dict__[method_name]))),
            ],
        },
        "emission": {
            "all_allowed_fields_covered_in_order": True,
            "first_object_records": [],
            "first_objects_pairwise_distinct": True,
            "first_return": _encode(first),
            "fresh_idf_object_flags": [],
            "fresh_result_list": None,
            "fresh_return_value": first is not second,
            "object_count": 0,
            "object_types": [],
            "result_type": type(first).__name__,
            "same_idd_definition_flags": [],
            "second_fields_equal_flags": [],
            "second_objects_pairwise_distinct": True,
        },
    }


def _facts(
    call: Callable[[], Any], state_function: Callable[[], list[dict[str, Any]]]
) -> dict[str, Any]:
    before = state_function()
    emission = _emission(call)
    after = state_function()
    return {
        "input_context": {
            "captured_state_scope": "properties-read-by-target-method-and-explicit-call-context",
            "source_state": before,
            "source_state_unchanged_after_two_emissions": before == after,
        },
        "emission": emission,
    }


def _heat_pump_state(value: Any) -> list[dict[str, Any]]:
    return [
        _field("name", value.name),
        _field("fuel", value.fuel.value),
        _field("heating_cop", value.heating_cop),
        _field("cooling_cop", value.cooling_cop),
        _field("heating_capacity", value.heating_capacity),
        _field("cooling_capacity", value.cooling_capacity),
    ]


def _compressor_state(value: Any, chiller: Any) -> list[dict[str, Any]]:
    return [
        _field("compressor", value.value),
        _field("chiller.idf_objname", chiller.idf_objname),
    ]


def _tower_state(value: Any, chiller: Any) -> list[dict[str, Any]]:
    return [
        _field("tower.type", type(value).__name__),
        _field("tower.name", value.name),
        _field("tower.capacity", value.capacity),
        _field("tower.pump_efficiency", value.pump_efficiency),
        _field("chiller.idf_objname", chiller.idf_objname),
        _field("chiller.idf_objtypename", chiller.idf_objtypename),
        _field("chiller.capacity", chiller.capacity),
    ]


def _chiller_state(value: Any) -> list[dict[str, Any]]:
    return [
        _field("name", value.name),
        _field("cop", value.cop),
        _field("capacity", value.capacity),
        _field("compressor", value.compressor.value),
        _field("coolingtower.type", type(value.coolingtower).__name__),
        _field("coolingtower.name", value.coolingtower.name),
        _field("coolingtower.capacity", value.coolingtower.capacity),
        _field("coolingtower.pump_efficiency", value.coolingtower.pump_efficiency),
        _field("pump_efficiency", value.pump_efficiency),
        _field("setpoint_temperature", value.setpoint_temperature),
    ]


def _boiler_state(value: Any) -> list[dict[str, Any]]:
    return [
        _field("name", value.name),
        _field("fuel", value.fuel.value),
        _field("efficiency", value.efficiency),
        _field("capacity", value.capacity),
        _field("pump_efficiency", value.pump_efficiency),
        _field("setpoint_temperature", value.setpoint_temperature),
    ]


def _boiler_generator_state(value: Any, target: Any) -> list[dict[str, Any]]:
    return _boiler_state(value) + [
        _field("target.idf_objname", target.idf_objname),
    ]


def _absorption_state(value: Any) -> list[dict[str, Any]]:
    return [
        _field("name", value.name),
        _field("cop", value.cop),
        _field("capacity", value.capacity),
        _field("heatsource.name", value.heatsource.name),
        _field("heatsource.fuel", value.heatsource.fuel.value),
        _field("heatsource.efficiency", value.heatsource.efficiency),
        _field("heatsource.capacity", value.heatsource.capacity),
        _field("heatsource.pump_efficiency", value.heatsource.pump_efficiency),
        _field("heatsource.setpoint_temperature", value.heatsource.setpoint_temperature),
        _field("coolingtower.type", type(value.coolingtower).__name__),
        _field("coolingtower.name", value.coolingtower.name),
        _field("coolingtower.capacity", value.coolingtower.capacity),
        _field("coolingtower.pump_efficiency", value.coolingtower.pump_efficiency),
        _field("pump_efficiency", value.pump_efficiency),
        _field("setpoint_temperature", value.setpoint_temperature),
    ]


def _tower_context(name: str, capacity: float | None) -> Any:
    return SimpleNamespace(
        capacity=capacity,
        idf_objname=f"Chiller_named_{name}",
        idf_objtypename="Chiller:Electric:EIR",
    )


def _execute_case(identifier: str, hvac: Any) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        boiler = hvac.Boiler(
            "Alternate Generator",
            hvac.Fuel.PROPANE,
            0.88,
            91000.0,
            pump_efficiency=0.86,
            setpoint_temperature=72.0,
        )
        tower = hvac.ClosedTwoSpeedCoolingTower(
            "Alternate Closed Tower", 125000.0, pump_efficiency=0.83
        )
        value = hvac.AbsorptionChiller(
            "Alternate Absorber",
            0.74,
            110000.0,
            boiler,
            tower,
            pump_efficiency=0.84,
            setpoint_temperature=8.5,
        )
        return _facts(value.to_idf_object, lambda: _absorption_state(value))
    if identifier == EXPECTED_CASE_IDS[1]:
        boiler = hvac.Boiler(
            "Representative Generator",
            hvac.Fuel.NATURALGAS,
            0.92,
            None,
            pump_efficiency=0.9,
            setpoint_temperature=60,
        )
        tower = hvac.OpenSingleSpeedCoolingTower(
            "Representative Open Tower", None, pump_efficiency=0.9
        )
        value = hvac.AbsorptionChiller(
            "Representative Absorber",
            0.7,
            150000.0,
            boiler,
            tower,
        )
        return _facts(value.to_idf_object, lambda: _absorption_state(value))
    if identifier == EXPECTED_CASE_IDS[2]:
        value = hvac.Boiler(
            "Generator Boiler",
            hvac.Fuel.NATURALGAS,
            0.91,
            85000.0,
            pump_efficiency=0.88,
            setpoint_temperature=68.0,
        )
        target = SimpleNamespace(idf_objname="AbsorptionChiller_named_Generator Target")
        return _facts(
            lambda: value.to_idf_object_as_generator(target),
            lambda: _boiler_generator_state(value, target),
        )
    if identifier == EXPECTED_CASE_IDS[3]:
        value = hvac.Boiler(
            "Autosized Boiler", hvac.Fuel.NATURALGAS, 0.9, None
        )
        return _facts(value.to_idf_object, lambda: _boiler_state(value))
    if identifier == EXPECTED_CASE_IDS[4]:
        value = hvac.Boiler(
            "Propane Boiler",
            hvac.Fuel.PROPANE,
            0.86,
            72000.0,
            pump_efficiency=0.82,
            setpoint_temperature=67.5,
        )
        return _facts(value.to_idf_object, lambda: _boiler_state(value))
    if identifier == EXPECTED_CASE_IDS[5]:
        tower = hvac.ClosedSingleSpeedCoolingTower(
            "Alternate Chiller Tower", 98000.0, pump_efficiency=0.81
        )
        value = hvac.Chiller(
            "Alternate Chiller",
            4.75,
            88000.0,
            hvac.CompressorType.RECIPROCATING,
            tower,
            pump_efficiency=0.83,
            setpoint_temperature=9.25,
        )
        return _facts(value.to_idf_object, lambda: _chiller_state(value))
    if identifier == EXPECTED_CASE_IDS[6]:
        tower = hvac.OpenTwoSpeedCoolingTower(
            "Representative Chiller Tower", None, pump_efficiency=0.9
        )
        value = hvac.Chiller(
            "Representative Chiller",
            5.5,
            None,
            hvac.CompressorType.TURBO,
            tower,
        )
        return _facts(value.to_idf_object, lambda: _chiller_state(value))
    if identifier in EXPECTED_CASE_IDS[7:10]:
        compressor = {
            EXPECTED_CASE_IDS[7]: hvac.CompressorType.RECIPROCATING,
            EXPECTED_CASE_IDS[8]: hvac.CompressorType.SCREW,
            EXPECTED_CASE_IDS[9]: hvac.CompressorType.TURBO,
        }[identifier]
        chiller = SimpleNamespace(idf_objname="Chiller_named_Curve Context")
        return _facts(
            lambda: compressor.to_idf_curve_object(chiller),
            lambda: _compressor_state(compressor, chiller),
        )
    if identifier == EXPECTED_CASE_IDS[10]:
        value = hvac.ClosedTwoSpeedCoolingTower(
            "Full Closed Tower", 103000.0, pump_efficiency=0.79
        )
        chiller = _tower_context("Full Closed Context", 97000.0)
        return _facts(
            lambda: value.to_idf_object(chiller),
            lambda: _tower_state(value, chiller),
        )
    if identifier == EXPECTED_CASE_IDS[11]:
        value = hvac.OpenSingleSpeedCoolingTower(
            "Full Open Tower", None, pump_efficiency=0.91
        )
        chiller = _tower_context("Full Open Context", 93000.0)
        return _facts(
            lambda: value.to_idf_object(chiller),
            lambda: _tower_state(value, chiller),
        )
    if identifier == EXPECTED_CASE_IDS[12]:
        return _abstract_facts(hvac.CoolingTower, "to_idf_main_object")
    if identifier in EXPECTED_CASE_IDS[13:17]:
        tower_type, tower_capacity, chiller_capacity = {
            EXPECTED_CASE_IDS[13]: (hvac.ClosedSingleSpeedCoolingTower, None, 91000.0),
            EXPECTED_CASE_IDS[14]: (hvac.ClosedTwoSpeedCoolingTower, 92000.0, None),
            EXPECTED_CASE_IDS[15]: (hvac.OpenSingleSpeedCoolingTower, None, None),
            EXPECTED_CASE_IDS[16]: (hvac.OpenTwoSpeedCoolingTower, 94000.0, 90000.0),
        }[identifier]
        value = tower_type("Main Object Tower", tower_capacity, pump_efficiency=0.87)
        chiller = _tower_context("Main Object Context", chiller_capacity)
        return _facts(
            lambda: value.to_idf_main_object(chiller),
            lambda: _tower_state(value, chiller),
        )
    if identifier == EXPECTED_CASE_IDS[17]:
        value = hvac.HeatPump(
            "Explicit Heat Pump",
            hvac.Fuel.NATURALGAS,
            4.2,
            3.6,
            65000.0,
            58000.0,
        )
        return _facts(value.to_idf_object, lambda: _heat_pump_state(value))
    if identifier == EXPECTED_CASE_IDS[18]:
        value = hvac.HeatPump(
            "Representative Heat Pump", hvac.Fuel.ELECTRICITY, 3.8, 3.2
        )
        return _facts(value.to_idf_object, lambda: _heat_pump_state(value))
    if identifier == EXPECTED_CASE_IDS[19]:
        return _abstract_facts(hvac.SourceSystem, "to_idf_object")
    raise RuntimeError(f"Unknown source-system IDF case: {identifier}")


def _record_field(record: dict[str, Any], name: str) -> dict[str, Any]:
    matches = [item["value"] for item in record["ordered_fields"] if item["name"] == name]
    if len(matches) != 1:
        raise RuntimeError(
            f"Expected one {name!r} field in {record['object_type']}, got {len(matches)}."
        )
    return matches[0]


def _records_of_type(facts: dict[str, Any], object_type: str) -> list[dict[str, Any]]:
    return [
        item
        for item in facts["emission"]["first_object_records"]
        if item["object_type"] == object_type
    ]


def _named_record(
    facts: dict[str, Any], object_type: str, name: str
) -> dict[str, Any]:
    matches = [
        item
        for item in _records_of_type(facts, object_type)
        if _record_field(item, "Name") == _encode(name)
    ]
    if len(matches) != 1:
        raise RuntimeError(
            f"Expected one {object_type} named {name!r}, got {len(matches)}."
        )
    return matches[0]


def _assert_field(
    record: dict[str, Any], field_name: str, expected: Any, identifier: str
) -> None:
    if _record_field(record, field_name) != _encode(expected):
        raise RuntimeError(
            f"Source-system IDF linked value drifted: {identifier}: "
            f"{record['object_type']}.{field_name}"
        )


def _validate_abstract_contract(identifier: str, facts: dict[str, Any]) -> None:
    context = facts["input_context"]
    emission = facts["emission"]
    if (
        context["captured_state_scope"]
        != "abstract-method-descriptor-and-direct-body-return"
        or not context["source_state_unchanged_after_two_emissions"]
        or context["source_state"][2] != _field("is_abstract_method", True)
        or emission["object_count"] != 0
        or emission["object_types"] != []
        or emission["first_object_records"] != []
        or emission["first_return"] != _encode(None)
        or emission["result_type"] != "NoneType"
        or emission["fresh_return_value"]
        or emission["fresh_result_list"] is not None
    ):
        raise RuntimeError(f"Abstract source-system IDF contract drifted: {identifier}")


def _validate_boiler_generator_topology(
    identifier: str, facts: dict[str, Any]
) -> None:
    loop_name = "Loop_for_Generator Boiler"
    generator_branch = f"{loop_name} Demand MainGenerator"
    records = facts["emission"]["first_object_records"]
    last = records[-1]
    if last["object_type"] != "Branch":
        raise RuntimeError(f"Boiler generator append order drifted: {identifier}")
    _assert_field(last, "Name", generator_branch, identifier)
    _assert_field(last, "Component 1 Object Type", "Chiller:Absorption", identifier)
    _assert_field(
        last,
        "Component 1 Name",
        "AbsorptionChiller_named_Generator Target",
        identifier,
    )
    branch_list = _named_record(
        facts, "BranchList", f"{loop_name} Demand BranchList"
    )
    expected = [
        f"{loop_name} Demand Inlet",
        f"{loop_name} Demand Bypass",
        generator_branch,
        f"{loop_name} Demand Outlet",
    ]
    for index, branch in enumerate(expected, 1):
        _assert_field(branch_list, f"Branch {index} Name", branch, identifier)
    splitter = _named_record(
        facts, "Connector:Splitter", f"{loop_name} Demand Splitter"
    )
    mixer = _named_record(facts, "Connector:Mixer", f"{loop_name} Demand Mixer")
    _assert_field(splitter, "Outlet Branch 2 Name", generator_branch, identifier)
    _assert_field(mixer, "Inlet Branch 2 Name", generator_branch, identifier)


def _validate_main_loop_setpoint(
    identifier: str,
    facts: dict[str, Any],
    loop_name: str,
    schedule_value: float | int,
    sizing_value: float | int,
) -> None:
    schedule = _named_record(
        facts, "Schedule:Constant", f"{loop_name} SetpointTemperature"
    )
    _assert_field(schedule, "Hourly Value", schedule_value, identifier)
    sizing_matches = [
        record
        for record in _records_of_type(facts, "Sizing:Plant")
        if _record_field(record, "Plant or Condenser Loop Name") == _encode(loop_name)
    ]
    if len(sizing_matches) != 1:
        raise RuntimeError(f"Main-loop Sizing:Plant identity drifted: {identifier}")
    _assert_field(
        sizing_matches[0], "Design Loop Exit Temperature", sizing_value, identifier
    )


def _validate_absorption_order(identifier: str, facts: dict[str, Any]) -> None:
    records = facts["emission"]["first_object_records"]
    types = [item["object_type"] for item in records]
    if types[:2] != ["Chiller:Absorption", "Pump:VariableSpeed"]:
        raise RuntimeError(f"Absorption leading object order drifted: {identifier}")
    boiler_index = types.index("Boiler:HotWater")
    tower_types = {
        "CoolingTower:SingleSpeed",
        "CoolingTower:TwoSpeed",
        "FluidCooler:SingleSpeed",
        "FluidCooler:TwoSpeed",
    }
    tower_indices = [index for index, item in enumerate(types) if item in tower_types]
    if len(tower_indices) != 1 or not boiler_index < tower_indices[0] < len(types) - 2:
        raise RuntimeError(f"Absorption nested source order drifted: {identifier}")
    if types[-2:] != ["PlantLoop", "Sizing:Plant"]:
        raise RuntimeError(f"Absorption main-loop tail order drifted: {identifier}")
    generator_names = [
        _record_field(item, "Name")
        for item in records
        if item["object_type"] == "Branch"
        and _record_field(item, "Name")["kind"] == "str"
        and _record_field(item, "Name")["value"].endswith("Demand MainGenerator")
    ]
    if len(generator_names) != 1:
        raise RuntimeError(f"Absorption generator topology drifted: {identifier}")


def _validate_case_facts(identifier: str, facts: dict[str, Any]) -> None:
    if set(EXPECTED_FACT_SHA256) != set(EXPECTED_CASE_IDS):
        raise RuntimeError("Pinned source-system per-case fact hashes are incomplete.")
    if set(EXPECTED_OBJECT_TYPES) != set(EXPECTED_CASE_IDS):
        raise RuntimeError("Pinned source-system per-case object types are incomplete.")
    actual_hash = canonical_sha256(facts)
    if actual_hash != EXPECTED_FACT_SHA256[identifier]:
        raise RuntimeError(
            f"Source-system IDF canonical semantics drifted: {identifier}: {actual_hash}"
        )
    emission = facts["emission"]
    if tuple(emission["object_types"]) != EXPECTED_OBJECT_TYPES[identifier]:
        raise RuntimeError(f"Source-system IDF object order drifted: {identifier}")
    if emission["object_count"] != len(emission["first_object_records"]):
        raise RuntimeError(f"Source-system IDF object count drifted: {identifier}")
    for record in emission["first_object_records"]:
        if record["field_count"] != len(record["ordered_fields"]):
            raise RuntimeError(f"Source-system IDF field completeness drifted: {identifier}")

    if identifier in (EXPECTED_CASE_IDS[12], EXPECTED_CASE_IDS[19]):
        _validate_abstract_contract(identifier, facts)
        return

    context = facts["input_context"]
    if (
        context["captured_state_scope"]
        != "properties-read-by-target-method-and-explicit-call-context"
        or not context["source_state_unchanged_after_two_emissions"]
        or emission["result_type"] != "list"
        or not emission["all_allowed_fields_covered_in_order"]
        or not emission["fresh_result_list"]
        or not emission["fresh_return_value"]
        or not emission["first_objects_pairwise_distinct"]
        or not emission["second_objects_pairwise_distinct"]
        or not all(emission["fresh_idf_object_flags"])
        or not all(emission["same_idd_definition_flags"])
        or not all(emission["second_fields_equal_flags"])
    ):
        raise RuntimeError(f"Source-system IDF freshness/state drifted: {identifier}")

    if identifier == EXPECTED_CASE_IDS[2]:
        _validate_boiler_generator_topology(identifier, facts)
    elif identifier == EXPECTED_CASE_IDS[3]:
        boiler = _records_of_type(facts, "Boiler:HotWater")[0]
        _assert_field(boiler, "Nominal Capacity", "autosize", identifier)
        _validate_main_loop_setpoint(
            identifier, facts, "Loop_for_Autosized Boiler", 60.0, 80.0
        )
    elif identifier == EXPECTED_CASE_IDS[4]:
        boiler = _records_of_type(facts, "Boiler:HotWater")[0]
        _assert_field(boiler, "Fuel Type", "Propane", identifier)
        _assert_field(boiler, "Nominal Capacity", 72000.0, identifier)
        _validate_main_loop_setpoint(
            identifier, facts, "Loop_for_Propane Boiler", 67.5, 80.0
        )
    elif identifier in EXPECTED_CASE_IDS[7:10]:
        expected_last = {
            EXPECTED_CASE_IDS[7]: "Curve:Quadratic",
            EXPECTED_CASE_IDS[8]: "Curve:Bicubic",
            EXPECTED_CASE_IDS[9]: "Curve:Quadratic",
        }[identifier]
        if emission["object_count"] != 3 or emission["object_types"][-1] != expected_last:
            raise RuntimeError(f"Compressor curve family drifted: {identifier}")
        expected_names = [
            "Curve_for_Chiller_named_Curve Context:CoolingCapaTemp",
            "Curve_for_Chiller_named_Curve Context:CoolingCOPTemp",
            "Curve_for_Chiller_named_Curve Context:CoolingCOPPLR",
        ]
        for record, name in zip(emission["first_object_records"], expected_names, strict=True):
            _assert_field(record, "Name", name, identifier)
    elif identifier in EXPECTED_CASE_IDS[13:17]:
        expected_type = {
            EXPECTED_CASE_IDS[13]: "FluidCooler:SingleSpeed",
            EXPECTED_CASE_IDS[14]: "FluidCooler:TwoSpeed",
            EXPECTED_CASE_IDS[15]: "CoolingTower:SingleSpeed",
            EXPECTED_CASE_IDS[16]: "CoolingTower:TwoSpeed",
        }[identifier]
        if emission["object_types"] != [expected_type]:
            raise RuntimeError(f"Cooling-tower main object type drifted: {identifier}")
        expected_capacity = {
            EXPECTED_CASE_IDS[13]: 91000.0,
            EXPECTED_CASE_IDS[15]: 1e6,
        }.get(identifier)
        if expected_capacity is not None:
            _assert_field(
                emission["first_object_records"][0],
                "Nominal Capacity",
                expected_capacity,
                identifier,
            )
    elif identifier in EXPECTED_CASE_IDS[10:12]:
        if emission["object_types"][-2:] != ["CondenserLoop", "Sizing:Plant"]:
            raise RuntimeError(f"Cooling-tower full-loop tail drifted: {identifier}")
        if emission["object_types"][1] != "Pump:VariableSpeed":
            raise RuntimeError(f"Cooling-tower pump order drifted: {identifier}")
    elif identifier == EXPECTED_CASE_IDS[5]:
        _validate_main_loop_setpoint(
            identifier, facts, "Loop_for_Alternate Chiller", 9.25, 6.0
        )
    elif identifier == EXPECTED_CASE_IDS[6]:
        _validate_main_loop_setpoint(
            identifier, facts, "Loop_for_Representative Chiller", 6.0, 6.0
        )
    elif identifier == EXPECTED_CASE_IDS[0]:
        _validate_absorption_order(identifier, facts)
        _validate_main_loop_setpoint(
            identifier, facts, "Loop_for_Alternate Absorber", 8.5, 6.0
        )
    elif identifier == EXPECTED_CASE_IDS[1]:
        _validate_absorption_order(identifier, facts)
        _validate_main_loop_setpoint(
            identifier, facts, "Loop_for_Representative Absorber", 6.0, 6.0
        )
    elif identifier in EXPECTED_CASE_IDS[17:19]:
        vrf = _records_of_type(facts, "AirConditioner:VariableRefrigerantFlow")
        if len(vrf) != 1 or emission["object_count"] != 22:
            raise RuntimeError(f"Heat-pump IDF object family drifted: {identifier}")
        expected_capacity = 58000.0 if identifier == EXPECTED_CASE_IDS[17] else "autosize"
        _assert_field(vrf[0], "Gross Rated Total Cooling Capacity", expected_capacity, identifier)
        expected_fuel = "NaturalGas" if identifier == EXPECTED_CASE_IDS[17] else "Electricity"
        _assert_field(vrf[0], "Fuel Type", expected_fuel, identifier)


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _module_name(source_path: str) -> str:
    relative = Path(source_path).relative_to("src").with_suffix("")
    parts = list(relative.parts)
    if parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def _expected_loaded_local_modules() -> list[dict[str, str]]:
    return [
        {
            "ast_sha256": source["ast_sha256"],
            "module": _module_name(source["path"]),
            "path": source["path"],
            "source_sha256": source["source_sha256"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_files() -> list[dict[str, str]]:
    return [
        {
            "ast_hash": source["ast_sha256"],
            "content_hash": source["source_sha256"],
            "path": source["path"],
        }
        for source in SOURCE_SPECS
    ]


def _expected_symbol_descriptors() -> list[dict[str, str]]:
    return [
        {
            **EXPECTED_SYMBOL_RECEIPTS[symbol],
            "path": HVAC_SOURCE_PATH,
            "symbol": symbol,
        }
        for symbol in TARGET_SYMBOLS
    ]


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "loaded_local_modules": _expected_loaded_local_modules(),
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
        "adaptations": ADAPTATIONS,
        "assertion_ids": ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classification_basis": (
            "native source emitters use immutable collections, compact defaults, "
            "and explicit generation context; legacy mutable standalone lists are "
            "bounded here as exception evidence"
        ),
        "classifications": {symbol: "exception" for symbol in TARGET_SYMBOLS},
        "closure": {
            "context_only_not_targeted": [
                "AbsorptionChiller",
                "AbsorptionChiller.__init__",
                "Boiler",
                "Boiler.__init__",
                "Chiller",
                "Chiller.__init__",
                "ClosedSingleSpeedCoolingTower",
                "ClosedTwoSpeedCoolingTower",
                "CompressorType",
                "CoolingTower",
                "CoolingTower.__init__",
                "Fuel",
                "HeatPump",
                "HeatPump.__init__",
                "OpenSingleSpeedCoolingTower",
                "OpenTwoSpeedCoolingTower",
                "SourceSystem",
                "all-related-naming-properties",
                "all-related-enum-string-and-value-contracts",
            ],
            "full_symbol_closure": False,
            "scope": "bounded-common-valid-state-hvac-source-system-idf-emission",
            "unresolved_behavior": [
                "all-related-constructors-properties-and-enums",
                "invalid-domain-nonfinite-and-duck-typed-error-semantics",
                "GeothermalHeatPump",
                "native-DistrictHeating",
                "general-terminal-and-demand-connection-enrichment",
                "IdfObject",
                "IdfObject.__init__",
                "isolated-IdfObject-and-IDD-default-policy",
                "EnergyModel.to_idf",
                "parent-EnergyModel-global-order-deduplication-and-conflicts",
                "safe-native-screw-compressor-behavior",
                "active-absorption-runtime-parity",
            ],
        },
        "identity_encoding": "booleans-only-no-id-or-address",
        "native_targets": NATIVE_TARGETS,
        "raw_field_encoding": "complete-ordered-IDD-fields-with-typed-values",
        "source_import_policy": (
            "external-temporary-copy-with-complete-twelve-module-audit"
        ),
        "target_symbols": list(TARGET_SYMBOLS),
    }


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


def _expected_runtime() -> dict[str, Any]:
    return {
        "dependencies": EXPECTED_DEPENDENCIES,
        "implementation": "cpython",
        "python_dont_write_bytecode": True,
        "python_hash_algorithm": REQUIRED_HASH_ALGORITHM,
        "python_hash_seed": 0,
        "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS,
        "python_version": ".".join(map(str, REQUIRED_PYTHON)),
    }


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches = []
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        if all(
            _source_file(root, source).is_file()
            and sha256_file(_source_file(root, source)) == source["source_sha256"]
            for source in SOURCE_SPECS
        ):
            matches.append(root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def build_oracle(
    inventory: dict[str, Any], commit: str, source_root: Path | None = None
) -> dict[str, Any]:
    imported_root = (
        source_root.resolve() if source_root is not None else _find_pinned_source_root()
    )
    if inventory != {
        "content_sha256": EXPECTED_INVENTORY_SHA256,
        "files": _expected_files(),
        "symbols": _expected_symbol_descriptors(),
    }:
        raise SystemExit("The aggregate source-system IDF inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")

    with SUPPORT._pinned_modules(imported_root) as modules:
        hvac = modules.hvac
        imported_hvac = Path(hvac.__file__).resolve()
        expected_hvac = (
            Path(modules.shape.__file__).resolve().parents[2]
            / Path(HVAC_SOURCE_PATH).relative_to("src")
        )
        if (
            imported_hvac != expected_hvac
            or sha256_file(imported_hvac) != EXPECTED_HVAC_SOURCE_SHA256
            or hvac.IdfObject is not modules.imugi.IdfObject
        ):
            raise SystemExit("Pinned HVAC import identities drifted.")

        cases = []
        for definition in case_definitions():
            facts = _execute_case(definition["id"], hvac)
            _validate_case_facts(definition["id"], facts)
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
                "python_dont_write_bytecode": sys.dont_write_bytecode,
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
                "loaded_local_modules": modules.loaded_local_modules,
                "sources": [
                    {
                        "ast_sha256": source["ast_sha256"],
                        "path": source["path"],
                        "source_sha256": sha256_file(
                            _source_file(imported_root, source)
                        ),
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


def _validate_encoded_scalar(value: dict[str, Any], location: str) -> bool:
    kind = value.get("kind")
    if kind == "none":
        _require_keys(value, {"kind"}, location)
        return True
    if kind == "bool":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], bool):
            raise RuntimeError(f"Invalid encoded bool at {location}.")
        return True
    if kind == "int":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], str):
            raise RuntimeError(f"Invalid encoded int at {location}.")
        try:
            if str(int(value["value"])) != value["value"]:
                raise ValueError
        except ValueError as error:
            raise RuntimeError(f"Invalid encoded int at {location}.") from error
        return True
    if kind == "str":
        _require_keys(value, {"kind", "value"}, location)
        if not isinstance(value["value"], str):
            raise RuntimeError(f"Invalid encoded string at {location}.")
        return True
    if kind == "float":
        _require_keys(value, {"hex", "kind", "repr"}, location)
        if not isinstance(value["hex"], str) or not isinstance(value["repr"], str):
            raise RuntimeError(f"Invalid encoded float at {location}.")
        try:
            decoded = float.fromhex(value["hex"])
        except ValueError as error:
            raise RuntimeError(f"Invalid encoded float at {location}.") from error
        if (
            not math.isfinite(decoded)
            or decoded.hex() != value["hex"]
            or repr(decoded) != value["repr"]
        ):
            raise RuntimeError(f"Unsafe or nonfinite encoded float at {location}.")
        return True
    return False


def _validate_safe_tree(value: Any, location: str = "root") -> None:
    if isinstance(value, float):
        if not math.isfinite(value):
            raise RuntimeError(f"Nonfinite raw float is forbidden at {location}.")
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
        if "kind" in value and _validate_encoded_scalar(value, location):
            for item in value.values():
                if isinstance(item, str):
                    _validate_safe_tree(item, f"{location}.encoded")
            return
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
        raise RuntimeError("Source-system IDF schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("Source-system IDF cases hash drifted.")
    _validate_safe_tree(value)

    cases = value["cases"]
    definitions = case_definitions()
    if (
        not isinstance(cases, list)
        or len(cases) != EXPECTED_CASE_COUNT
        or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS)
    ):
        raise RuntimeError("Source-system IDF case order/count drifted.")
    if (
        list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS)
        or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT
    ):
        raise RuntimeError("Pinned source-system IDF case IDs drifted.")
    if Counter(item["symbol"] for item in definitions) != Counter(
        EXPECTED_CASE_COUNTS
    ):
        raise RuntimeError("Source-system IDF per-symbol case counts drifted.")
    if (
        set(ADAPTATIONS) != set(TARGET_SYMBOLS)
        or len(set(ADAPTATIONS.values())) != len(TARGET_SYMBOLS)
        or len(set(ASSERTION_IDS.values())) != len(TARGET_SYMBOLS)
    ):
        raise RuntimeError("Source-system IDF adaptation/assertion identities drifted.")

    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"Source-system IDF case contract drifted: {case['id']}")
        _require_keys(
            case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet"
        )
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned":
            raise RuntimeError(f"Source-system IDF outcome drifted: {case['id']}")
        _validate_case_facts(case["id"], case["python"]["facts"])

    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("Source-system IDF consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("Source-system IDF runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("Source-system IDF upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("Source-system IDF symbol receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned checkout.")
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
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        strict_json_dumps(result, indent=2) + "\n",
        encoding="utf-8",
        newline="\n",
    )
    print(f"Wrote dragon HVAC source-system IDF oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

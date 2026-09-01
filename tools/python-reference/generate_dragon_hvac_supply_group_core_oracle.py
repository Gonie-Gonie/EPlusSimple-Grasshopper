"""Generate bounded observations for the legacy SupplyGroup core surface.

This corpus covers only SupplyGroup.__init__, heatable/coolable, the two
capability projections, and sources.  The class receipt and IDF conversion are
intentionally outside this slice.
"""

from __future__ import annotations

import argparse
from collections import Counter
import importlib.metadata
import importlib.util
import inspect
import os
from pathlib import Path
import sys
from typing import Any


SCHEMA = "dragons.python-reference.dragon-hvac-supply-group-core.v1"
EXPECTED_UPSTREAM_COMMIT = "847b01f68f438f560a986072bcaa7768fbf67897"
EXPECTED_INVENTORY_SHA256 = (
    "sha256:4e52456b1e922630603a66344aa25d59be2fc687a3ea7bc3052129e924842e02"
)
HVAC_SOURCE_PATH = "src/idragon/dragon/hvac.py"
SOURCE_RECEIPTS = (
    ("src/idragon/__init__.py", "sha256:a486e6471fc9afa8f431ee1b63eea9054d8ba757863c617365a515751f881618", "sha256:1d80e812842f6ef6803fedfb9c996a8e50841c4a4399b89230f5178554597e50"),
    ("src/idragon/common.py", "sha256:a361e8780970d1070591443cef73e2242ab6a45908af8901e6925c881a5982e9", "sha256:0445472b3e0551365bbaf9d3576e408fed8d2736d72521ff5d6d2f6cdbbd6c9d"),
    ("src/idragon/constants.py", "sha256:b8487539fc6085f2d4e3db229a88f9fdab37c0f9f42233b91b4259478e37a084", "sha256:90f6d9750bc33f68ca5003ed7a643e920119133520d2369d0d0c3bfc2b08e520"),
    ("src/idragon/dragon/__init__.py", "sha256:1a1a599171964e2dfda806d66a5c46bb8b8c8514bdf997419a859187d9564d52", "sha256:88df519f22bc3b086d76e318a3a58bb07677da33d2947e1095d0236b270f048a"),
    ("src/idragon/dragon/construction.py", "sha256:04bd33fb46d0e41adb681267ec8792eaa8985fd7a694b9e36971a63ca8d2757a", "sha256:2cbae026eaad36833111d7d8c96eb12ee615ec952294db62454197d11ac75622"),
    (HVAC_SOURCE_PATH, "sha256:ce151dba25ac7bf4f7dc0ba47be840440f13663950043ff8d1f5bffc302c7a31", "sha256:a57ec9d15df749efe0c42b3b68016293cf39ee1ffde1d3960d2451b3853e8ed0"),
    ("src/idragon/dragon/model.py", "sha256:89c4fa95b97d069fa62d2baf09055be9819893645e41c773a77723e26f62dd59", "sha256:8899ac8e262f21561ab877698a8405a44ede093df1ba06350d20d9e07474b090"),
    ("src/idragon/dragon/profile.py", "sha256:7a58e27e28b9de5a32d3de5cb4b103cfc99c25699da88e7117fda707cbddeeef", "sha256:e286a612360a781cf40e0afbb09b60befdfd7526c36267f608620b9a1b89d445"),
    ("src/idragon/dragon/shape.py", "sha256:905a14a9f05a12c26c75ee5401fd9cb7d5a732cdab231d590b1246cdbd8714c2", "sha256:20a0b0d1e642c5cf8fb878cbf3ea6adabaace0d9d6360bb6cbab851246ceae7c"),
    ("src/idragon/imugi.py", "sha256:e3d5d9756c4c75c1adf4d7ee8ec90112cba34e4c9258b1e800bd4c5604d4fa90", "sha256:cde6cf0415ac97086a58b9fc2c213528311746c9782d2af2fcea336622ce6613"),
    ("src/idragon/launcher.py", "sha256:80fdaa33ba9ac3b524719c8fd312a3abcc928996a95b90e20c2f3ed98b3dc26e", "sha256:741f3319c18aae63d6c9a73f828b36e138e51ddaa263505926088ce565aed68f"),
    ("src/idragon/utils.py", "sha256:abda2bfa93ff7461fb412cd1dd8fe526d30983ff22017e714b17dea1aa9f7452", "sha256:aa4b4e66c4ea48a4a7a03e4fcc8041eb1cb06671196ad36d5b9d00e4bf6689cd"),
)
EXPECTED_SYMBOL_RECEIPTS = {
    "SupplyGroup.__init__": {"body_hash": "sha256:643ca4afc57e9a0b22eee5df0a2cd7b90d9d579cf16bb20fd6d6a9e40b5bc57c", "kind": "function", "signature_hash": "sha256:f01960cc5a0c00e094cf2eb094922d734343c92c8ec849977ea8b86337805907", "symbol_hash": "sha256:02b3c43aa048fd31a3ffc31fea96f5086a599d3245847e217dc0c99a9cf5fddd"},
    "SupplyGroup.coolable": {"body_hash": "sha256:73f3cb2b0806dccc2593dcdb1412835c8258e8bca42388c75c1ba2e3038afa56", "kind": "function", "signature_hash": "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "symbol_hash": "sha256:0f6f3f1afaac0b5144d7a4f3af1857e2d5d6ca2e02baf98d0427cd1a317abd36"},
    "SupplyGroup.cooling_systems": {"body_hash": "sha256:ba298377ff3ee58bec8d56a856e9fab8941fec0cfd35321d6e48c7c9b3df9c89", "kind": "function", "signature_hash": "sha256:97cc1e2d625ebc73e65314802efcf1b1278d42ee34f0bba31a167bb7a7525344", "symbol_hash": "sha256:e2ee9492964b6c3eeaa5d54700d66a010198413a3c006edd46427c126150221c"},
    "SupplyGroup.heatable": {"body_hash": "sha256:ac6066fdc4f9bed2e2f6b2f7c5910634c34c5b26f84a6bb5c4e2049deb3d8096", "kind": "function", "signature_hash": "sha256:4d8304d5438dea6290c4bc8f7da2ecae177f6dacdbaa0bbb164b5181953b43f3", "symbol_hash": "sha256:ab11abdd7afeb3b7fde0805ce2697af2df49ad6e874691b164bb5674ae9ac655"},
    "SupplyGroup.heating_systems": {"body_hash": "sha256:f1ea945dbd140a8fed2b6adf855e9751375c5ca15aae41b6d9a591c25d3291f1", "kind": "function", "signature_hash": "sha256:97cc1e2d625ebc73e65314802efcf1b1278d42ee34f0bba31a167bb7a7525344", "symbol_hash": "sha256:1fdfba66763618fe1880c3d0354b764e551c8a7747eb4a1dedac24d375f87dc2"},
    "SupplyGroup.sources": {"body_hash": "sha256:8380d67f068d32acc9710838b7314fd04f3acf74b53e5f9484cdde3b07e3d09d", "kind": "function", "signature_hash": "sha256:74055a2ba47ab60bd034a8ca75be001a2cd1b1c1e78e201eed646b37d5b2065d", "symbol_hash": "sha256:482d0fa2c4cc9f732bc33911ae01ea857e3042ff4cf60e680583f2abefdab423"},
}
TARGET_SYMBOLS = tuple(EXPECTED_SYMBOL_RECEIPTS)
EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS = {
    "SupplyGroup.__init__": "immutable-validated-supply-group-construction",
    "SupplyGroup.sources": "stable-entity-id-supply-source-deduplication",
}
EXPECTED_ASSERTION_IDS = {
    symbol: "dragon-hvac-supply-group-core-" + symbol.rsplit(".", 1)[-1].replace("__", "").replace("_", "-") + "-" + EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"][7:15]
    for symbol in TARGET_SYMBOLS
}
NATIVE_TARGETS = {
    "SupplyGroup.__init__": "SupplyGroup",
    "SupplyGroup.coolable": "SupplyGroup.CanCool",
    "SupplyGroup.cooling_systems": "SupplyGroup.CoolingSystems",
    "SupplyGroup.heatable": "SupplyGroup.CanHeat",
    "SupplyGroup.heating_systems": "SupplyGroup.HeatingSystems",
    "SupplyGroup.sources": "SupplyGroup.Sources",
}
EXPECTED_CASE_BINDINGS = (
    ("dragon-hvac-supply-group-core.coolable.cooling-only-true", "supply-group-coolable", "SupplyGroup.coolable"),
    ("dragon-hvac-supply-group-core.coolable.heating-only-false", "supply-group-coolable", "SupplyGroup.coolable"),
    ("dragon-hvac-supply-group-core.coolable.mixed-capability-true", "supply-group-coolable", "SupplyGroup.coolable"),
    ("dragon-hvac-supply-group-core.cooling-systems.distinct-members-and-order", "supply-group-cooling-systems", "SupplyGroup.cooling_systems"),
    ("dragon-hvac-supply-group-core.cooling-systems.fresh-tuple", "supply-group-cooling-systems", "SupplyGroup.cooling_systems"),
    ("dragon-hvac-supply-group-core.cooling-systems.heating-only-empty", "supply-group-cooling-systems", "SupplyGroup.cooling_systems"),
    ("dragon-hvac-supply-group-core.heatable.cooling-only-false", "supply-group-heatable", "SupplyGroup.heatable"),
    ("dragon-hvac-supply-group-core.heatable.heating-only-true", "supply-group-heatable", "SupplyGroup.heatable"),
    ("dragon-hvac-supply-group-core.heatable.mixed-capability-true", "supply-group-heatable", "SupplyGroup.heatable"),
    ("dragon-hvac-supply-group-core.heating-systems.cooling-only-empty", "supply-group-heating-systems", "SupplyGroup.heating_systems"),
    ("dragon-hvac-supply-group-core.heating-systems.distinct-members-and-order", "supply-group-heating-systems", "SupplyGroup.heating_systems"),
    ("dragon-hvac-supply-group-core.heating-systems.fresh-tuple", "supply-group-heating-systems", "SupplyGroup.heating_systems"),
    ("dragon-hvac-supply-group-core.init.defaults-and-snapshot", "supply-group-init", "SupplyGroup.__init__"),
    ("dragon-hvac-supply-group-core.init.duplicates-and-explicit-availabilities", "supply-group-init", "SupplyGroup.__init__"),
    ("dragon-hvac-supply-group-core.init.validation-order", "supply-group-init", "SupplyGroup.__init__"),
    ("dragon-hvac-supply-group-core.sources.distinct-equal-sources", "supply-group-sources", "SupplyGroup.sources"),
    ("dragon-hvac-supply-group-core.sources.distinct-identifiers-first-seen", "supply-group-sources", "SupplyGroup.sources"),
    ("dragon-hvac-supply-group-core.sources.identity-dedup-and-none", "supply-group-sources", "SupplyGroup.sources"),
)
EXPECTED_CASE_IDS = tuple(item[0] for item in EXPECTED_CASE_BINDINGS)
EXPECTED_CASE_COUNT = 18
EXPECTED_CASE_COUNTS = {symbol: 3 for symbol in TARGET_SYMBOLS}
EXPECTED_DEPENDENCIES = {
    "colorama": "0.4.6", "et_xmlfile": "2.0.0", "numpy": "2.3.1",
    "openpyxl": "3.1.5", "pandas": "2.3.0", "python-dateutil": "2.9.0.post0",
    "pytz": "2024.2", "six": "1.16.0", "tqdm": "4.67.1", "tzdata": "2024.2",
}
REQUIRED_PYTHON = (3, 12, 7)
REQUIRED_HASH_ALGORITHM = "siphash13"
REQUIRED_HASH_WIDTH_BITS = 64


def _load_support() -> Any:
    path = Path(__file__).resolve().with_name("generate_dragon_model_assembly_oracle.py")
    spec = importlib.util.spec_from_file_location("_dragons_supply_group_support", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load SupplyGroup support: {path}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    receipts = tuple((item["path"], item["ast_sha256"], item["source_sha256"]) for item in module.SOURCE_SPECS)
    if module.EXPECTED_UPSTREAM_COMMIT != EXPECTED_UPSTREAM_COMMIT or module.EXPECTED_INVENTORY_SHA256 != EXPECTED_INVENTORY_SHA256 or receipts != SOURCE_RECEIPTS:
        raise RuntimeError("SupplyGroup support is not exactly pinned.")
    return module


SUPPORT = _load_support()
strict_json_dumps = SUPPORT.strict_json_dumps
canonical_sha256 = SUPPORT.canonical_sha256
sha256_file = SUPPORT.sha256_file
load_json_without_duplicates = SUPPORT.SUPPORT.load_json_without_duplicates
RAW_ADDRESS_PATTERN = SUPPORT.RAW_ADDRESS_PATTERN
ABSOLUTE_PATH_PATTERN = SUPPORT.ABSOLUTE_PATH_PATTERN
GUID_PATTERN = SUPPORT.GUID_PATTERN
TIMESTAMP_PATTERN = SUPPORT.TIMESTAMP_PATTERN
SOURCE_SPECS = tuple({"ast_sha256": ast_hash, "path": path, "source_sha256": source_hash, "symbols": TARGET_SYMBOLS if path == HVAC_SOURCE_PATH else ()} for path, ast_hash, source_hash in SOURCE_RECEIPTS)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inventory", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--upstream-commit", required=True)
    return parser.parse_args()


def _load_source_inventory(path: Path, commit: str, source: dict[str, Any]) -> dict[str, Any]:
    helper = SUPPORT.SUPPORT
    names = ("SOURCE_PATH", "EXPECTED_SOURCE_SHA256", "EXPECTED_SYMBOL_HASHES", "TARGET_SYMBOLS")
    original = {name: getattr(helper, name) for name in names}
    try:
        helper.SOURCE_PATH = source["path"]
        helper.EXPECTED_SOURCE_SHA256 = source["source_sha256"]
        helper.EXPECTED_SYMBOL_HASHES = {symbol: EXPECTED_SYMBOL_RECEIPTS[symbol]["symbol_hash"] for symbol in source["symbols"]}
        helper.TARGET_SYMBOLS = tuple(source["symbols"])
        result = helper.load_exact_inventory(path, commit)
    finally:
        for name, value in original.items():
            setattr(helper, name, value)
    expected_file = {"ast_hash": source["ast_sha256"], "content_hash": source["source_sha256"], "path": source["path"]}
    expected_symbols = [{**EXPECTED_SYMBOL_RECEIPTS[symbol], "path": source["path"], "symbol": symbol} for symbol in source["symbols"]]
    if result["file"] != expected_file or result["symbols"] != expected_symbols:
        raise SystemExit(f"The {source['path']} inventory receipt is not exact.")
    return result


def load_exact_inventory(path: Path, commit: str) -> dict[str, Any]:
    items = [_load_source_inventory(path, commit, source) for source in SOURCE_SPECS]
    if any(item["content_sha256"] != EXPECTED_INVENTORY_SHA256 for item in items):
        raise SystemExit("The public-symbol inventory hash is not exact.")
    return {"content_sha256": EXPECTED_INVENTORY_SHA256, "files": [item["file"] for item in items], "symbols": [symbol for item in items for symbol in item["symbols"]]}


def _case(identifier: str, executor: str, symbol: str) -> dict[str, Any]:
    result = {"executor": executor, "id": identifier, "symbol": symbol}
    adaptation = EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS.get(symbol)
    if adaptation is not None:
        result["expected_dotnet"] = {"adaptation": adaptation, "outcome": "returned"}
    return result


def case_definitions() -> tuple[dict[str, Any], ...]:
    return tuple(_case(*binding) for binding in EXPECTED_CASE_BINDINGS)


class _LogicalSource:
    def __init__(self, label: str, entity_key: str | None = None) -> None:
        self.label = label
        self.entity_key = entity_key if entity_key is not None else label

    def __eq__(self, other: object) -> bool:
        return isinstance(other, _LogicalSource) and self.entity_key == other.entity_key


class _LogicalAvailability:
    def __init__(self, label: str) -> None:
        self.label = label


def _probe_type(hvac: Any) -> type[Any]:
    class ProbeSupply(hvac.SupplySystem):
        def __init__(self, label: str, heatable: bool, coolable: bool, source: Any, events: list[dict[str, Any]]) -> None:
            self.label = label
            self.name = label
            self._heatable = heatable
            self._coolable = coolable
            self._source = source
            self.events = events

        @property
        def heatable(self) -> bool:
            value = self._heatable
            self.events.append({"capability": "heatable", "system": self.label, "value": value})
            return value

        @property
        def coolable(self) -> bool:
            value = self._coolable
            self.events.append({"capability": "coolable", "system": self.label, "value": value})
            return value

        @property
        def source(self) -> Any:
            value = self._source
            self.events.append({"source": None if value is None else value.label, "system": self.label})
            return value

        @property
        def idf_objtypename(self) -> str:
            return "ProbeSupply"

        def to_idf_object(self, zone: Any, for_heating: bool, for_cooling: bool, availability: Any = None) -> tuple[list[Any], list[Any]]:
            raise AssertionError("IDF conversion is outside the SupplyGroup core oracle.")

    return ProbeSupply


def _system_labels(values: Any) -> list[str]:
    return [value.label for value in values]


def _source_labels(values: Any) -> list[str]:
    return [value.label for value in values]


def _availability_labels(values: Any) -> list[str | None]:
    return [None if value is None else value.label for value in values]


def _attempt(function: Any) -> dict[str, Any]:
    try:
        function()
    except Exception as error:
        return {"args": [str(value) for value in error.args], "message": str(error), "outcome": "raised", "type": type(error).__name__}
    return {"outcome": "returned"}


def _input_conditions(
    system_count: int,
    all_systems_are_supply_system: bool,
    availability_count_matches: bool | None,
    all_systems_capable: bool | None,
) -> dict[str, Any]:
    return {
        "all_systems_are_supply_system": all_systems_are_supply_system,
        "all_systems_capable": all_systems_capable,
        "availability_count_matches": availability_count_matches,
        "system_count": system_count,
    }


def _new_system(Probe: type[Any], label: str, heatable: bool, coolable: bool, events: list[dict[str, Any]], source: Any = None) -> Any:
    return Probe(label, heatable, coolable, source, events)


def _execute_case(identifier: str, modules: Any) -> dict[str, Any]:
    SupplyGroup = modules.hvac.SupplyGroup
    Probe = _probe_type(modules.hvac)
    events: list[dict[str, Any]] = []

    if identifier == EXPECTED_CASE_IDS[0]:
        system = _new_system(Probe, "cool-only", False, True, events)
        group = SupplyGroup([system]); events.clear()
        result = group.coolable
        return {"capability_reads": events, "result": result, "result_type": type(result).__name__}
    if identifier == EXPECTED_CASE_IDS[1]:
        system = _new_system(Probe, "heat-only", True, False, events)
        group = SupplyGroup([system]); events.clear()
        result = group.coolable
        return {"capability_reads": events, "result": result, "result_type": type(result).__name__}
    if identifier == EXPECTED_CASE_IDS[2]:
        heat = _new_system(Probe, "heat-only", True, False, events)
        cool = _new_system(Probe, "cool-only", False, True, events)
        group = SupplyGroup([heat, cool]); events.clear(); result = group.coolable
        return {"capability_reads": events, "result": result, "result_type": type(result).__name__, "systems": _system_labels(group.systems)}

    if identifier == EXPECTED_CASE_IDS[3]:
        heat = _new_system(Probe, "heat-only", True, False, events)
        both_first = _new_system(Probe, "both-first", True, True, events)
        cool = _new_system(Probe, "cool-only", False, True, events)
        both_second = _new_system(Probe, "both-second", True, True, events)
        inputs = [heat, both_first, cool, both_second]
        group = SupplyGroup(inputs); events.clear(); result = group.cooling_systems
        return {"capability_reads": events, "input_systems": _system_labels(inputs), "preserved_input_identity": result[0] is both_first and result[1] is cool and result[2] is both_second, "result_systems": _system_labels(result), "result_type": type(result).__name__}
    if identifier == EXPECTED_CASE_IDS[4]:
        both = _new_system(Probe, "both", True, True, events)
        group = SupplyGroup([both]); events.clear(); first = group.cooling_systems; second = group.cooling_systems
        return {"capability_reads": events, "first_result": _system_labels(first), "same_result_object": first is second, "same_system_identity": first[0] is second[0] is both, "second_result": _system_labels(second), "result_type": type(first).__name__}
    if identifier == EXPECTED_CASE_IDS[5]:
        first = _new_system(Probe, "heat-first", True, False, events)
        second = _new_system(Probe, "heat-second", True, False, events)
        group = SupplyGroup([first, second]); events.clear(); result = group.cooling_systems
        return {"capability_reads": events, "result_systems": _system_labels(result), "result_type": type(result).__name__, "systems": _system_labels(group.systems)}

    if identifier == EXPECTED_CASE_IDS[6]:
        system = _new_system(Probe, "cool-only", False, True, events)
        group = SupplyGroup([system]); events.clear(); result = group.heatable
        return {"capability_reads": events, "result": result, "result_type": type(result).__name__}
    if identifier == EXPECTED_CASE_IDS[7]:
        system = _new_system(Probe, "heat-only", True, False, events)
        group = SupplyGroup([system]); events.clear(); result = group.heatable
        return {"capability_reads": events, "result": result, "result_type": type(result).__name__}
    if identifier == EXPECTED_CASE_IDS[8]:
        cool = _new_system(Probe, "cool-only", False, True, events)
        heat = _new_system(Probe, "heat-only", True, False, events)
        group = SupplyGroup([cool, heat]); events.clear(); result = group.heatable
        return {"capability_reads": events, "result": result, "result_type": type(result).__name__, "systems": _system_labels(group.systems)}

    if identifier == EXPECTED_CASE_IDS[9]:
        first = _new_system(Probe, "cool-first", False, True, events)
        second = _new_system(Probe, "cool-second", False, True, events)
        group = SupplyGroup([first, second]); events.clear(); result = group.heating_systems
        return {"capability_reads": events, "result_systems": _system_labels(result), "result_type": type(result).__name__, "systems": _system_labels(group.systems)}
    if identifier == EXPECTED_CASE_IDS[10]:
        cool = _new_system(Probe, "cool-only", False, True, events)
        both_first = _new_system(Probe, "both-first", True, True, events)
        heat = _new_system(Probe, "heat-only", True, False, events)
        both_second = _new_system(Probe, "both-second", True, True, events)
        inputs = [cool, both_first, heat, both_second]
        group = SupplyGroup(inputs); events.clear(); result = group.heating_systems
        return {"capability_reads": events, "input_systems": _system_labels(inputs), "preserved_input_identity": result[0] is both_first and result[1] is heat and result[2] is both_second, "result_systems": _system_labels(result), "result_type": type(result).__name__}
    if identifier == EXPECTED_CASE_IDS[11]:
        both = _new_system(Probe, "both", True, True, events)
        group = SupplyGroup([both]); events.clear(); first = group.heating_systems; second = group.heating_systems
        return {"capability_reads": events, "first_result": _system_labels(first), "same_result_object": first is second, "same_system_identity": first[0] is second[0] is both, "second_result": _system_labels(second), "result_type": type(first).__name__}

    if identifier == EXPECTED_CASE_IDS[12]:
        heat = _new_system(Probe, "heat-only", True, False, events); both = _new_system(Probe, "both", True, True, events); cool = _new_system(Probe, "cool-only", False, True, events)
        systems = [heat, both, cool]; originals = tuple(systems); group = SupplyGroup(systems); systems.reverse()
        signature = inspect.signature(SupplyGroup)
        return {"availability_default_is_none": signature.parameters["availabilities"].default is None, "availability_parameter_kind": signature.parameters["availabilities"].kind.name, "input_systems_after_mutation": _system_labels(systems), "parameter_order": list(signature.parameters), "snapshot_isolated": group.systems == originals, "stored_availabilities": _availability_labels(group.availabilities), "stored_availabilities_type": type(group.availabilities).__name__, "stored_objects_are_inputs": all(observed is expected for observed, expected in zip(group.systems, originals)), "stored_systems": _system_labels(group.systems), "stored_systems_type": type(group.systems).__name__}
    if identifier == EXPECTED_CASE_IDS[13]:
        both = _new_system(Probe, "both", True, True, events); heat = _new_system(Probe, "heat-only", True, False, events)
        systems = [both, both, heat]; available_a = _LogicalAvailability("availability-a"); available_b = _LogicalAvailability("availability-b"); availabilities = [available_a, None, available_b]
        group = SupplyGroup(systems, availabilities=availabilities); systems.clear(); availabilities.reverse()
        return {"duplicate_same_object_accepted": group.systems[0] is group.systems[1] is both, "explicit_availabilities_snapshot_isolated": list(group.availabilities) != availabilities, "non_schedule_availabilities_accepted": group.availabilities[0] is available_a and group.availabilities[2] is available_b, "stored_availabilities": _availability_labels(group.availabilities), "stored_availabilities_type": type(group.availabilities).__name__, "stored_systems": _system_labels(group.systems), "stored_systems_type": type(group.systems).__name__}
    if identifier == EXPECTED_CASE_IDS[14]:
        incapable = _new_system(Probe, "incapable", False, False, events)
        attempts = [
            {
                "label": "empty",
                **_input_conditions(0, True, None, None),
                **_attempt(lambda: SupplyGroup([])),
            },
            {
                "label": "type-before-count",
                **_input_conditions(1, False, False, None),
                **_attempt(lambda: SupplyGroup([object()], availabilities=[])),
            },
            {
                "label": "count-before-capability",
                **_input_conditions(1, True, False, False),
                **_attempt(lambda: SupplyGroup([incapable], availabilities=[])),
            },
            {
                "label": "incapable",
                **_input_conditions(1, True, True, False),
                **_attempt(lambda: SupplyGroup([incapable], availabilities=[None])),
            },
        ]
        return {"attempts": attempts, "validation_order": [item["label"] for item in attempts]}

    if identifier == EXPECTED_CASE_IDS[15]:
        source_a = _LogicalSource("source-a", "shared"); source_b = _LogicalSource("source-b", "shared")
        first = _new_system(Probe, "first", True, False, events, source_a); second = _new_system(Probe, "second", True, False, events, source_b)
        group = SupplyGroup([first, second]); events.clear(); result = group.sources
        return {"distinct_source_identity": source_a is not source_b, "equal_by_value": source_a == source_b, "result_sources": _source_labels(result), "result_type": type(result).__name__, "source_reads": events}
    if identifier == EXPECTED_CASE_IDS[16]:
        source_z = _LogicalSource("source-z", "entity-z"); source_a = _LogicalSource("source-a", "entity-a")
        first = _new_system(Probe, "first", True, False, events, source_z); second = _new_system(Probe, "second", True, False, events, source_a)
        group = SupplyGroup([first, second]); events.clear(); first_result = group.sources; second_result = group.sources
        return {
            "distinct_entity_keys": source_z.entity_key != source_a.entity_key,
            "distinct_source_identity": source_z is not source_a,
            "first_result_sources": _source_labels(first_result),
            "first_seen_order_preserved": first_result[0] is source_z and first_result[1] is source_a,
            "fresh_result_tuple": first_result is not second_result,
            "input_sources": [
                {"entity_key": source_z.entity_key, "label": source_z.label, "system": first.label},
                {"entity_key": source_a.entity_key, "label": source_a.label, "system": second.label},
            ],
            "result_type": type(first_result).__name__,
            "reverse_logical_label_order": source_z.label > source_a.label,
            "second_result_sources": _source_labels(second_result),
            "source_reads": events,
        }
    if identifier == EXPECTED_CASE_IDS[17]:
        source_a = _LogicalSource("source-a"); source_b = _LogicalSource("source-b")
        systems = [_new_system(Probe, "first", True, False, events, source_a), _new_system(Probe, "second", True, False, events, source_a), _new_system(Probe, "third", True, False, events, None), _new_system(Probe, "fourth", True, False, events, source_b), _new_system(Probe, "fifth", True, False, events, source_a)]
        group = SupplyGroup(systems); events.clear(); first_result = group.sources; second_result = group.sources
        return {"first_seen_identity_deduplication": _source_labels(first_result) == ["source-a", "source-b"], "fresh_result_tuple": first_result is not second_result, "none_skipped": None not in first_result, "result_sources": _source_labels(first_result), "result_type": type(first_result).__name__, "source_reads": events}

    raise RuntimeError(f"Unknown SupplyGroup core case: {identifier}")


def _capability(system: str, capability: str, value: bool) -> dict[str, Any]:
    return {"capability": capability, "system": system, "value": value}


def _source_read(system: str, source: str | None) -> dict[str, Any]:
    return {"source": source, "system": system}


def _raised(label: str, error_type: str, message: str) -> dict[str, Any]:
    return {"args": [message], "label": label, "message": message, "outcome": "raised", "type": error_type}


def expected_facts(identifier: str) -> dict[str, Any]:
    if identifier == EXPECTED_CASE_IDS[0]:
        return {"capability_reads": [_capability("cool-only", "coolable", True)], "result": True, "result_type": "bool"}
    if identifier == EXPECTED_CASE_IDS[1]:
        return {"capability_reads": [_capability("heat-only", "coolable", False)], "result": False, "result_type": "bool"}
    if identifier == EXPECTED_CASE_IDS[2]:
        return {"capability_reads": [_capability("heat-only", "coolable", False), _capability("cool-only", "coolable", True)], "result": True, "result_type": "bool", "systems": ["heat-only", "cool-only"]}
    if identifier == EXPECTED_CASE_IDS[3]:
        return {"capability_reads": [_capability("heat-only", "coolable", False), _capability("both-first", "coolable", True), _capability("cool-only", "coolable", True), _capability("both-second", "coolable", True)], "input_systems": ["heat-only", "both-first", "cool-only", "both-second"], "preserved_input_identity": True, "result_systems": ["both-first", "cool-only", "both-second"], "result_type": "tuple"}
    if identifier == EXPECTED_CASE_IDS[4]:
        return {"capability_reads": [_capability("both", "coolable", True), _capability("both", "coolable", True)], "first_result": ["both"], "same_result_object": False, "same_system_identity": True, "second_result": ["both"], "result_type": "tuple"}
    if identifier == EXPECTED_CASE_IDS[5]:
        return {"capability_reads": [_capability("heat-first", "coolable", False), _capability("heat-second", "coolable", False)], "result_systems": [], "result_type": "tuple", "systems": ["heat-first", "heat-second"]}
    if identifier == EXPECTED_CASE_IDS[6]:
        return {"capability_reads": [_capability("cool-only", "heatable", False)], "result": False, "result_type": "bool"}
    if identifier == EXPECTED_CASE_IDS[7]:
        return {"capability_reads": [_capability("heat-only", "heatable", True)], "result": True, "result_type": "bool"}
    if identifier == EXPECTED_CASE_IDS[8]:
        return {"capability_reads": [_capability("cool-only", "heatable", False), _capability("heat-only", "heatable", True)], "result": True, "result_type": "bool", "systems": ["cool-only", "heat-only"]}
    if identifier == EXPECTED_CASE_IDS[9]:
        return {"capability_reads": [_capability("cool-first", "heatable", False), _capability("cool-second", "heatable", False)], "result_systems": [], "result_type": "tuple", "systems": ["cool-first", "cool-second"]}
    if identifier == EXPECTED_CASE_IDS[10]:
        return {"capability_reads": [_capability("cool-only", "heatable", False), _capability("both-first", "heatable", True), _capability("heat-only", "heatable", True), _capability("both-second", "heatable", True)], "input_systems": ["cool-only", "both-first", "heat-only", "both-second"], "preserved_input_identity": True, "result_systems": ["both-first", "heat-only", "both-second"], "result_type": "tuple"}
    if identifier == EXPECTED_CASE_IDS[11]:
        return {"capability_reads": [_capability("both", "heatable", True), _capability("both", "heatable", True)], "first_result": ["both"], "same_result_object": False, "same_system_identity": True, "second_result": ["both"], "result_type": "tuple"}
    if identifier == EXPECTED_CASE_IDS[12]:
        return {"availability_default_is_none": True, "availability_parameter_kind": "KEYWORD_ONLY", "input_systems_after_mutation": ["cool-only", "both", "heat-only"], "parameter_order": ["systems", "availabilities"], "snapshot_isolated": True, "stored_availabilities": [None, None, None], "stored_availabilities_type": "tuple", "stored_objects_are_inputs": True, "stored_systems": ["heat-only", "both", "cool-only"], "stored_systems_type": "tuple"}
    if identifier == EXPECTED_CASE_IDS[13]:
        return {"duplicate_same_object_accepted": True, "explicit_availabilities_snapshot_isolated": True, "non_schedule_availabilities_accepted": True, "stored_availabilities": ["availability-a", None, "availability-b"], "stored_availabilities_type": "tuple", "stored_systems": ["both", "both", "heat-only"], "stored_systems_type": "tuple"}
    if identifier == EXPECTED_CASE_IDS[14]:
        attempts = [
            {**_raised("empty", "ValueError", "SupplyGroup requires at least one system."), **_input_conditions(0, True, None, None)},
            {**_raised("type-before-count", "TypeError", "All systems must be SupplySystem instances."), **_input_conditions(1, False, False, None)},
            {**_raised("count-before-capability", "ValueError", "The number of availabilities must match the number of systems."), **_input_conditions(1, True, False, False)},
            {**_raised("incapable", "ValueError", "Every supply system must support heating or cooling."), **_input_conditions(1, True, True, False)},
        ]
        return {"attempts": attempts, "validation_order": [item["label"] for item in attempts]}
    if identifier == EXPECTED_CASE_IDS[15]:
        return {"distinct_source_identity": True, "equal_by_value": True, "result_sources": ["source-a", "source-b"], "result_type": "tuple", "source_reads": [_source_read("first", "source-a"), _source_read("second", "source-b")]}
    if identifier == EXPECTED_CASE_IDS[16]:
        return {
            "distinct_entity_keys": True,
            "distinct_source_identity": True,
            "first_result_sources": ["source-z", "source-a"],
            "first_seen_order_preserved": True,
            "fresh_result_tuple": True,
            "input_sources": [
                {"entity_key": "entity-z", "label": "source-z", "system": "first"},
                {"entity_key": "entity-a", "label": "source-a", "system": "second"},
            ],
            "result_type": "tuple",
            "reverse_logical_label_order": True,
            "second_result_sources": ["source-z", "source-a"],
            "source_reads": [
                _source_read("first", "source-z"),
                _source_read("second", "source-a"),
                _source_read("first", "source-z"),
                _source_read("second", "source-a"),
            ],
        }
    if identifier == EXPECTED_CASE_IDS[17]:
        reads = [_source_read("first", "source-a"), _source_read("second", "source-a"), _source_read("third", None), _source_read("fourth", "source-b"), _source_read("fifth", "source-a")]
        return {"first_seen_identity_deduplication": True, "fresh_result_tuple": True, "none_skipped": True, "result_sources": ["source-a", "source-b"], "result_type": "tuple", "source_reads": reads + reads}
    raise RuntimeError(f"Unknown SupplyGroup core case: {identifier}")


def cases_sha256(cases: list[dict[str, Any]]) -> str:
    return canonical_sha256(cases)


def _module_name(source_path: str) -> str:
    relative = Path(source_path).relative_to("src").with_suffix("")
    parts = list(relative.parts)
    if parts[-1] == "__init__":
        parts.pop()
    return ".".join(parts)


def _expected_loaded_local_modules() -> list[dict[str, str]]:
    return [{"ast_sha256": source["ast_sha256"], "module": _module_name(source["path"]), "path": source["path"], "source_sha256": source["source_sha256"]} for source in SOURCE_SPECS]


def _expected_files() -> list[dict[str, str]]:
    return [{"ast_hash": source["ast_sha256"], "content_hash": source["source_sha256"], "path": source["path"]} for source in SOURCE_SPECS]


def _expected_symbol_descriptors() -> list[dict[str, str]]:
    return [{**EXPECTED_SYMBOL_RECEIPTS[symbol], "path": HVAC_SOURCE_PATH, "symbol": symbol} for symbol in TARGET_SYMBOLS]


def _expected_upstream() -> dict[str, Any]:
    return {
        "commit": EXPECTED_UPSTREAM_COMMIT,
        "inventory_sha256": EXPECTED_INVENTORY_SHA256,
        "loaded_local_modules": _expected_loaded_local_modules(),
        "sources": [{"ast_sha256": source["ast_sha256"], "path": source["path"], "source_sha256": source["source_sha256"]} for source in SOURCE_SPECS],
    }


def _expected_consumer_contract() -> dict[str, Any]:
    return {
        "adaptations": EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS,
        "assertion_ids": EXPECTED_ASSERTION_IDS,
        "case_count": EXPECTED_CASE_COUNT,
        "case_ids": list(EXPECTED_CASE_IDS),
        "classifications": {symbol: "exception" if symbol in EXPECTED_EXCEPTION_SYMBOL_ADAPTATIONS else "equivalent" for symbol in TARGET_SYMBOLS},
        "closure": {"full_symbol_closure": False, "scope": "bounded-supply-group-container-evidence", "unresolved_behavior": ["SupplyGroup", "SupplyGroup.to_idf_object", "SupplySystem", "concrete-supply-systems", "supply-system-postprocessors", "EnergyModel.to_idf"]},
        "identity_encoding": "logical-labels-only-no-id-or-address",
        "native_targets": NATIVE_TARGETS,
        "source_import_policy": "external-temporary-copy-with-complete-loaded-local-module-audit",
        "target_symbols": list(TARGET_SYMBOLS),
    }


def _dependencies() -> dict[str, str]:
    result: dict[str, str] = {}
    for distribution in EXPECTED_DEPENDENCIES:
        try:
            result[distribution] = importlib.metadata.version(distribution)
        except importlib.metadata.PackageNotFoundError as error:
            raise RuntimeError(f"Required reference dependency is missing: {distribution}") from error
    return result


def _expected_runtime() -> dict[str, Any]:
    return {"dependencies": EXPECTED_DEPENDENCIES, "implementation": "cpython", "python_dont_write_bytecode": True, "python_hash_algorithm": REQUIRED_HASH_ALGORITHM, "python_hash_seed": 0, "python_hash_width_bits": REQUIRED_HASH_WIDTH_BITS, "python_version": ".".join(map(str, REQUIRED_PYTHON))}


def _source_file(source_root: Path, source: dict[str, Any]) -> Path:
    return source_root / Path(source["path"]).relative_to("src")


def _find_pinned_source_root() -> Path:
    matches = []
    for entry in sys.path:
        if not entry:
            continue
        root = Path(entry)
        if all(_source_file(root, source).is_file() and sha256_file(_source_file(root, source)) == source["source_sha256"] for source in SOURCE_SPECS):
            matches.append(root.resolve())
    unique = list(dict.fromkeys(matches))
    if len(unique) != 1:
        raise SystemExit("Exactly one pinned idragon source root must be importable.")
    return unique[0]


def build_oracle(inventory: dict[str, Any], commit: str, source_root: Path | None = None) -> dict[str, Any]:
    imported_root = source_root.resolve() if source_root is not None else _find_pinned_source_root()
    if inventory != {"content_sha256": EXPECTED_INVENTORY_SHA256, "files": _expected_files(), "symbols": _expected_symbol_descriptors()}:
        raise SystemExit("The aggregate SupplyGroup core inventory is not exact.")
    for source in SOURCE_SPECS:
        if sha256_file(_source_file(imported_root, source)) != source["source_sha256"]:
            raise SystemExit(f"The imported {source['path']} source is not inventoried.")
    with SUPPORT._pinned_modules(imported_root) as modules:
        cases = []
        for definition in case_definitions():
            facts = _execute_case(definition["id"], modules)
            if facts != expected_facts(definition["id"]):
                raise SystemExit("Pinned Python SupplyGroup core semantics drifted: " + definition["id"] + "\n" + strict_json_dumps(facts, indent=2))
            case = dict(definition)
            case["python"] = {"facts": facts, "outcome": "returned"}
            cases.append(case)
    result = {
        "cases": cases,
        "cases_sha256": cases_sha256(cases),
        "consumer_contract": _expected_consumer_contract(),
        "runtime": {"dependencies": _dependencies(), "implementation": sys.implementation.name, "python_dont_write_bytecode": sys.dont_write_bytecode, "python_hash_algorithm": sys.hash_info.algorithm, "python_hash_seed": 0, "python_hash_width_bits": sys.hash_info.width, "python_version": ".".join(map(str, sys.version_info[:3]))},
        "schema": SCHEMA,
        "symbols": inventory["symbols"],
        "upstream": {**_expected_upstream(), "commit": commit, "loaded_local_modules": modules.loaded_local_modules, "sources": [{"ast_sha256": source["ast_sha256"], "path": source["path"], "source_sha256": sha256_file(_source_file(imported_root, source))} for source in SOURCE_SPECS]},
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
    _require_keys(value, {"cases", "cases_sha256", "consumer_contract", "runtime", "schema", "symbols", "upstream"}, "root")
    if value["schema"] != SCHEMA:
        raise RuntimeError("SupplyGroup core schema drifted.")
    if value["cases_sha256"] != cases_sha256(value["cases"]):
        raise RuntimeError("SupplyGroup core cases hash drifted.")
    _validate_safe_tree(value)
    cases = value["cases"]
    definitions = case_definitions()
    if not isinstance(cases, list) or len(cases) != EXPECTED_CASE_COUNT or [item.get("id") for item in cases] != list(EXPECTED_CASE_IDS):
        raise RuntimeError("SupplyGroup core case order/count drifted.")
    if list(EXPECTED_CASE_IDS) != sorted(EXPECTED_CASE_IDS) or len(set(EXPECTED_CASE_IDS)) != EXPECTED_CASE_COUNT:
        raise RuntimeError("Pinned SupplyGroup core case IDs drifted.")
    if Counter(item["symbol"] for item in definitions) != Counter(EXPECTED_CASE_COUNTS):
        raise RuntimeError("SupplyGroup core per-symbol case counts drifted.")
    definitions_by_id = {item["id"]: item for item in definitions}
    for case in cases:
        definition = definitions_by_id[case["id"]]
        _require_keys(case, set(definition) | {"python"}, f"case {case['id']}")
        if any(case[key] != definition[key] for key in definition):
            raise RuntimeError(f"SupplyGroup core case contract drifted: {case['id']}")
        if "expected_dotnet" in case:
            _require_keys(case["expected_dotnet"], {"adaptation", "outcome"}, "expected_dotnet")
        _require_keys(case["python"], {"facts", "outcome"}, "python")
        if case["python"]["outcome"] != "returned" or case["python"]["facts"] != expected_facts(case["id"]):
            raise RuntimeError(f"SupplyGroup core semantics drifted: {case['id']}")
    if value["consumer_contract"] != _expected_consumer_contract():
        raise RuntimeError("SupplyGroup core consumer contract drifted.")
    if value["runtime"] != _expected_runtime():
        raise RuntimeError("SupplyGroup core runtime pin drifted.")
    if value["upstream"] != _expected_upstream():
        raise RuntimeError("SupplyGroup core upstream receipts drifted.")
    if value["symbols"] != _expected_symbol_descriptors():
        raise RuntimeError("SupplyGroup core symbol receipts drifted.")
    strict_json_dumps(value)


def main() -> int:
    args = parse_args()
    if sys.version_info[:3] != REQUIRED_PYTHON or sys.implementation.name != "cpython":
        raise SystemExit("Exact CPython 3.12.7 is required for this oracle.")
    if os.environ.get("PYTHONHASHSEED") != "0" or sys.flags.hash_randomization != 0:
        raise SystemExit("PYTHONHASHSEED=0 is required for deterministic observations.")
    if not sys.dont_write_bytecode:
        raise SystemExit("Bytecode writes must be disabled for the pinned checkout.")
    if sys.hash_info.algorithm != REQUIRED_HASH_ALGORITHM or sys.hash_info.width != REQUIRED_HASH_WIDTH_BITS:
        raise SystemExit("CPython siphash13 with a 64-bit hash width is required.")
    if _dependencies() != EXPECTED_DEPENDENCIES:
        raise SystemExit("The exact pinned Python dependency set is required.")
    commit = args.upstream_commit.lower()
    inventory = load_exact_inventory(args.inventory, commit)
    result = build_oracle(inventory, commit)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(strict_json_dumps(result, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote dragon HVAC SupplyGroup core oracle: {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

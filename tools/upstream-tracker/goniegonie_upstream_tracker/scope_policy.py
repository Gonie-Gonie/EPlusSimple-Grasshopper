"""Deterministic reviewed product-scope integration for the pinned 0.7.0 API."""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
import re
from typing import Iterable, Mapping

from .compatibility import CompatibilityMatrix, MatrixEntry, PublicSymbolInventory
from .errors import ConfigurationError
from .evidence import ScopeDecision, ScopeDecisionRegistry


POLICY_REFERENCE = "docs/compatibility.md#declared-product-compatibility-scope"
PRODUCT_CONTRACT = "compiled_rhino_grasshopper_product"
EXPECTED_SAFE_SCOPE_COUNT = 250
EXPECTED_BASELINE_DECISION_COUNT = 16
EXPECTED_SELECTION_SHA256 = (
    "sha256:36fe1d9c6a9473c20e181e09aeaaadcef859971e5847c5a71b490ee0de3c70ce"
)
EXPECTED_SYMBOL_CONTRACT_SHA256 = (
    "sha256:905f776e28c75fca528ea79c1e6166d1702e2dc9713bc5b1e6bba5b152c635ed"
)

BASELINE_RATIONALE = (
    "The historical Python import/call or Excel adapter contract is outside the "
    "compiled Grasshopper product scope; its underlying engineering domains are "
    "reviewed under their own symbols."
)
DEBUG_ADAPTER_RATIONALE = (
    "This exact declaration is in the legacy pandas-based Excel/JSON debugger and "
    "report-format surface. The adapter/report API is excluded, but the underlying "
    "model validity constraints must still be demonstrated by domain and workflow tests."
)
HUMAN_REPRESENTATION_RATIONALE = (
    "Human/debug representation only. The body is not consumed by GRM/IDF writing or "
    "conversion; many variants include process memory addresses. Python representation "
    "syntax is outside the native Grasshopper interface."
)
DISPLAY_STRING_RATIONALE = (
    "The reviewed call sites use this result only for human summaries/errors (or do not "
    "call it at all); engineering tokens for this type are obtained through .value, "
    "explicit mapping, or a separately retained writer symbol. Console/display "
    "formatting is outside scope."
)
STANDARD_EQUALITY_RATIONALE = (
    "No epsimple conversion call site uses this object equality/hash protocol. Model "
    "collection and reference topology are keyed explicitly by ID strings in "
    "epsimple/core/model.py and shape.py, so the Python comparison surface can be "
    "excluded while ID/reference outcomes remain gated."
)
UNUSED_IDRAGON_TAG_RATIONALE = (
    "The IDragon SpecialTag class has no production use outside its own docstring "
    "examples. The used SimpleDragon tag family is reviewed separately, so this Python "
    "representation entry point is safely outside scope."
)
AHU_DEEPCOPY_RATIONALE = (
    "No upstream production call site deep-copies an IDragon AirHandlingUnit. Python "
    "copy syntax can be excluded; shared source identity/reference topology remains "
    "covered by the native model, persistence, and authored-IDF gates."
)
SCHEDULE_MUTATION_RATIONALE = (
    "This method only rejects a Python list-style size mutation with AttributeError. No "
    "production caller invokes it, and mutable-container behavior is explicitly outside "
    "scope; fixed-length schedule values remain covered through constructors and writers."
)
VERTEX_EQUALITY_RATIONALE = (
    "The approximate Python equality has no upstream geometry call site; coplanarity, "
    "centroid, and IDF coordinate generation use vector operations instead. The C# "
    "native API separates exact Equals from tolerance-aware AlmostEquals, and actual "
    "geometry/topology remains covered by Rhino/IDF evidence."
)
IDD_EQUALITY_RATIONALE = (
    "No production authoring path compares complete IDD instances. Object insertion "
    "compares IddObject definitions instead; complete official schema/hash validation is "
    "the engineering replacement evidence."
)
PYTHON_REPRESENTATION_RATIONALE = (
    "This exact declaration is a Python representation/copy protocol entry point. Only "
    "the Python protocol surface is excluded; when it produces IDF text, the same "
    "authoring meaning remains covered by the semantic IDF gate."
)
IDF_LENGTH_RATIONALE = (
    "The full-IDF length protocol is used only for representation/object-count "
    "convenience, not model generation or write. Authored object sets are compared "
    "independently."
)
SHRINK_QUICK_MAP_RATIONALE = (
    "This exact declaration is one of the shrink/quick_map Python convenience APIs named "
    "explicitly outside scope. It does not exclude ordinary IDF authoring or the "
    "engineering results of a model."
)
IDF_EQUALITY_RATIONALE = (
    "This equality is reached only by excluded mutable-list editing/pop behavior or "
    "explicit Python comparisons, not by the legacy model-to-IDF append path. Semantic "
    "IDF equivalence remains independently gated."
)
PANDAS_LINK_RATIONALE = (
    "This exact declaration implements the pandas-linked IDF editing adapter that is "
    "explicitly outside scope. Authoring-IDF meaning and reference topology remain "
    "subject to the semantic differential gate."
)
MUTABLE_IDF_RATIONALE = (
    "This exact declaration is a Python mutable-container/editing entry point. The entry "
    "point is excluded, but no resulting IDF object, reference, warning, or simulation "
    "difference is waived by this scope decision."
)
PYTHON_INDEXING_RATIONALE = (
    "This exact declaration exposes Python key/list or regex/callable selection syntax. "
    "That protocol is excluded, while the selected IDF objects, field values, and "
    "downstream authored model remain in scope through native C# interfaces."
)


RISKY_AUTHORING_KEYS = frozenset(
    {
        ("src/idragon/imugi.py", "IDF.append"),
        ("src/idragon/imugi.py", "IdfObject.__getitem__"),
        ("src/idragon/imugi.py", "IdfObject.__setitem__"),
        ("src/idragon/imugi.py", "IdfObject.__str__"),
        ("src/idragon/imugi.py", "IdfObjectList.__getitem__"),
        ("src/idragon/imugi.py", "IdfObjectList.__setitem__"),
        ("src/idragon/imugi.py", "IdfObjectList.append"),
        ("src/idragon/imugi.py", "IdfObjectList.insert"),
        ("src/idragon/imugi.py", "IdfObjectList.__str__"),
        ("src/idragon/imugi.py", "StaticIndexedDict.__getitem__"),
        ("src/idragon/imugi.py", "StaticIndexedDict.__setitem__"),
    }
)


@dataclass(frozen=True)
class SafeScopePlan:
    """The exact registry and matrix produced by the reviewed scope policy."""

    decisions: ScopeDecisionRegistry
    matrix: CompatibilityMatrix
    previous_decision_count: int
    added_decision_count: int
    selection_sha256: str
    symbol_contract_sha256: str

    @property
    def new_decision_count(self) -> int:
        return self.added_decision_count

    @property
    def classification_counts(self) -> dict[str, int]:
        return {
            status: sum(item.classification == status for item in self.matrix.entries)
            for status in (
                "equivalent",
                "exception",
                "needs_reverification",
                "out_of_scope",
            )
        }


def build_safe_scope_plan(
    inventory: PublicSymbolInventory,
    matrix: CompatibilityMatrix,
    current_decisions: ScopeDecisionRegistry,
) -> SafeScopePlan:
    """Apply the fixed reviewed policy without trusting an untracked review artifact."""

    if matrix.upstream_commit != inventory.upstream_commit:
        raise ConfigurationError("scope policy matrix commit does not match inventory")
    if matrix.inventory_sha256 != inventory.content_sha256:
        raise ConfigurationError("scope policy matrix inventory hash is stale")
    if current_decisions.upstream_commit != inventory.upstream_commit:
        raise ConfigurationError("scope policy decision commit does not match inventory")
    if current_decisions.inventory_sha256 != inventory.content_sha256:
        raise ConfigurationError("scope policy decision inventory hash is stale")
    if tuple(item.key for item in matrix.entries) != tuple(
        item.key for item in inventory.symbols
    ):
        raise ConfigurationError("scope policy matrix order does not match inventory")

    rationales = _reviewed_rationales(inventory)
    selected_keys = set(rationales)
    if selected_keys & RISKY_AUTHORING_KEYS:
        path, symbol = sorted(selected_keys & RISKY_AUTHORING_KEYS)[0]
        raise ConfigurationError(
            f"scope policy selected risky authoring symbol '{path}::{symbol}'"
        )
    missing_risky = RISKY_AUTHORING_KEYS - set(inventory.symbols_by_key)
    if missing_risky:
        path, symbol = sorted(missing_risky)[0]
        raise ConfigurationError(
            f"scope policy risky-symbol guard is stale for '{path}::{symbol}'"
        )
    if len(selected_keys) != EXPECTED_SAFE_SCOPE_COUNT:
        raise ConfigurationError(
            "scope policy must select exactly "
            f"{EXPECTED_SAFE_SCOPE_COUNT} symbols; found {len(selected_keys)}"
        )

    selection_hash = _selection_sha256(selected_keys)
    if selection_hash != EXPECTED_SELECTION_SHA256:
        raise ConfigurationError("scope policy exact key selection changed")
    symbol_contract_hash = _symbol_contract_sha256(selected_keys, inventory)
    if symbol_contract_hash != EXPECTED_SYMBOL_CONTRACT_SHA256:
        raise ConfigurationError("scope policy exact upstream symbol contract changed")

    expected_decisions = tuple(
        _decision(inventory.symbols_by_key[key], rationales[key])
        for key in sorted(selected_keys)
    )
    expected_by_key = {item.key: item for item in expected_decisions}
    current_by_key = current_decisions.decisions_by_key
    if not BASELINE_SCOPE_KEYS <= set(current_by_key):
        raise ConfigurationError("scope policy no longer contains all baseline 16 decisions")
    unexpected = set(current_by_key) - selected_keys
    if unexpected:
        path, symbol = sorted(unexpected)[0]
        raise ConfigurationError(
            f"scope policy refuses existing out-of-policy decision '{path}::{symbol}'"
        )
    for key, current in current_by_key.items():
        if current != expected_by_key[key]:
            raise ConfigurationError(
                f"scope policy existing decision changed for '{key[0]}::{key[1]}'"
            )

    current_oos = {
        item.key for item in matrix.entries if item.classification == "out_of_scope"
    }
    if current_oos != set(current_by_key):
        raise ConfigurationError(
            "scope policy requires exact alignment between current decisions and matrix"
        )

    planned_entries = tuple(
        _out_of_scope_entry(item, expected_by_key[item.key])
        if item.key in expected_by_key
        else item
        for item in matrix.entries
    )
    planned_matrix = CompatibilityMatrix(
        inventory.upstream_commit,
        inventory.content_sha256,
        planned_entries,
    )
    planned_decisions = ScopeDecisionRegistry(
        inventory.upstream_commit,
        inventory.content_sha256,
        expected_decisions,
    )
    counts = {
        status: sum(item.classification == status for item in planned_entries)
        for status in (
            "equivalent",
            "exception",
            "needs_reverification",
            "out_of_scope",
        )
    }
    if counts["out_of_scope"] != EXPECTED_SAFE_SCOPE_COUNT:
        raise ConfigurationError("scope policy did not produce exactly 250 out-of-scope rows")
    return SafeScopePlan(
        planned_decisions,
        planned_matrix,
        len(current_by_key),
        len(selected_keys - set(current_by_key)),
        selection_hash,
        symbol_contract_hash,
    )


def _reviewed_rationales(
    inventory: PublicSymbolInventory,
) -> dict[tuple[str, str], str]:
    keys = set(inventory.symbols_by_key)
    selected: dict[tuple[str, str], str] = {}

    def add(candidates: Iterable[tuple[str, str]], rationale: str) -> None:
        for key in candidates:
            if key not in keys:
                raise ConfigurationError(
                    f"scope policy identifies unknown symbol '{key[0]}::{key[1]}'"
                )
            if key in selected:
                raise ConfigurationError(
                    f"scope policy selects symbol twice '{key[0]}::{key[1]}'"
                )
            selected[key] = rationale

    add(BASELINE_SCOPE_KEYS, BASELINE_RATIONALE)
    add(_whole_file(keys, "src/epsimple/debug.py"), DEBUG_ADAPTER_RATIONALE)

    standard_equality: set[tuple[str, str]] = set()
    standard_equality |= _members(
        "src/epsimple/core/construction.py",
        ("FenestrationConstruction", "Material", "SurfaceConstruction"),
        ("__eq__", "__hash__"),
    )
    standard_equality |= _members(
        "src/epsimple/core/hvac.py",
        _EPSIMPLE_HVAC_EQUALITY_TYPES,
        ("__eq__", "__hash__"),
    )
    standard_equality |= {
        ("src/epsimple/core/profile.py", "KoreanUsageProfile.__hash__"),
        ("src/epsimple/core/shape.py", "Fenestration.__hash__"),
        ("src/epsimple/core/shape.py", "Surface.__hash__"),
        ("src/epsimple/core/shape.py", "Zone.__hash__"),
    }
    add(standard_equality, STANDARD_EQUALITY_RATIONALE)

    human_representation: set[tuple[str, str]] = set()
    for path, types in _HUMAN_REPRESENTATION_TYPES.items():
        human_representation |= _members(path, types, ("__repr__",))
    add(human_representation, HUMAN_REPRESENTATION_RATIONALE)

    display_strings: set[tuple[str, str]] = set()
    for path, types in _DISPLAY_STRING_TYPES.items():
        display_strings |= _members(path, types, ("__str__",))
    add(display_strings, DISPLAY_STRING_RATIONALE)

    add(
        {
            ("src/idragon/constants.py", "SpecialTag.__format__"),
            ("src/idragon/constants.py", "SpecialTag.__str__"),
        },
        UNUSED_IDRAGON_TAG_RATIONALE,
    )
    add(
        {("src/idragon/dragon/hvac.py", "AirHandlingUnit.__deepcopy__")},
        AHU_DEEPCOPY_RATIONALE,
    )
    add(
        _members(
            "src/idragon/dragon/profile.py",
            ("DaySchedule", "Schedule"),
            ("__delitem__", "append", "clear", "extend", "insert", "pop"),
        ),
        SCHEDULE_MUTATION_RATIONALE,
    )
    add(
        {("src/idragon/dragon/shape.py", "Vertex.__eq__")},
        VERTEX_EQUALITY_RATIONALE,
    )
    add({("src/idragon/imugi.py", "IDD.__eq__")}, IDD_EQUALITY_RATIONALE)
    add(
        _exact(
            "src/idragon/imugi.py",
            (
                "IDD.__repr__",
                "IDF.__repr__",
                "IddField.__repr__",
                "IddObject.__repr__",
                "IdfObject.__deepcopy__",
                "IdfObject.__repr__",
                "IdfObjectList.__deepcopy__",
                "IdfObjectList.__repr__",
            ),
        ),
        PYTHON_REPRESENTATION_RATIONALE,
    )
    add({("src/idragon/imugi.py", "IDF.__len__")}, IDF_LENGTH_RATIONALE)
    add(
        _exact("src/idragon/imugi.py", ("IDF.quick_map", "IDF.shrink")),
        SHRINK_QUICK_MAP_RATIONALE,
    )
    add(
        _exact(
            "src/idragon/imugi.py",
            ("IdfObject.__eq__", "IdfObjectList.__eq__"),
        ),
        IDF_EQUALITY_RATIONALE,
    )
    add(
        _exact(
            "src/idragon/imugi.py",
            (
                "IdfObjectLinkedDataFrame",
                "IdfObjectLinkedDataFrame.__enter__",
                "IdfObjectLinkedDataFrame.__exit__",
                "IdfObjectLinkedDataFrame.__init__",
                "IdfObjectLinkedDataFrame.columns",
                "IdfObjectLinkedDataFrame.linked",
                "IdfObjectList.as_dataframe",
                "IdfObjectList.to_dataframe",
            ),
        ),
        PANDAS_LINK_RATIONALE,
    )
    add(
        _exact(
            "src/idragon/imugi.py",
            ("IdfObjectList.__add__", "IdfObjectList.clear"),
        ),
        MUTABLE_IDF_RATIONALE,
    )
    add(
        {("src/idragon/imugi.py", "IdfObjectList.pop")},
        PYTHON_INDEXING_RATIONALE,
    )
    return selected


def _decision(symbol, rationale: str) -> ScopeDecision:
    return ScopeDecision(
        _decision_id(symbol.path, symbol.symbol, symbol.symbol_hash),
        symbol.path,
        symbol.symbol,
        symbol.symbol_hash,
        "out_of_scope",
        PRODUCT_CONTRACT,
        rationale,
        POLICY_REFERENCE,
        "approved",
    )


def _out_of_scope_entry(entry: MatrixEntry, decision: ScopeDecision) -> MatrixEntry:
    return MatrixEntry(
        entry.path,
        entry.symbol,
        "out_of_scope",
        decision.rationale,
        (f"upstream/scope-decisions.json#{decision.identifier}",),
        None,
    )


def _whole_file(
    keys: set[tuple[str, str]], path: str
) -> set[tuple[str, str]]:
    return {key for key in keys if key[0] == path}


def _members(
    path: str, types: Iterable[str], members: Iterable[str]
) -> set[tuple[str, str]]:
    return {
        (path, f"{type_name}.{member}")
        for type_name in types
        for member in members
    }


def _exact(path: str, symbols: Iterable[str]) -> set[tuple[str, str]]:
    return {(path, symbol) for symbol in symbols}


def _decision_id(path: str, symbol: str, symbol_hash: str) -> str:
    slug = re.sub(r"[^a-z0-9]+", "-", f"{path}-{symbol}".lower()).strip("-")
    return f"scope-{slug}-{symbol_hash.removeprefix('sha256:')[:8]}"


def _selection_sha256(keys: Iterable[tuple[str, str]]) -> str:
    payload = json.dumps(
        [list(key) for key in sorted(keys)],
        ensure_ascii=False,
        separators=(",", ":"),
    ).encode("utf-8")
    return "sha256:" + hashlib.sha256(payload).hexdigest()


def _symbol_contract_sha256(
    keys: Iterable[tuple[str, str]], inventory: PublicSymbolInventory
) -> str:
    payload = json.dumps(
        [
            [path, symbol, inventory.symbols_by_key[(path, symbol)].symbol_hash]
            for path, symbol in sorted(keys)
        ],
        ensure_ascii=False,
        separators=(",", ":"),
    ).encode("utf-8")
    return "sha256:" + hashlib.sha256(payload).hexdigest()


BASELINE_SCOPE_KEYS = frozenset(
    _exact(
        "src/epsimple/api.py",
        (
            "GreenRetrofitDataFormat",
            "GreenRetrofitDataFormat.EXCEL",
            "GreenRetrofitDataFormat.IDF",
            "GreenRetrofitDataFormat.JSON",
            "GreenRetrofitDataFormat.__new__",
            "convert_inputformat",
            "debug",
            "get_database",
            "run_grexcel",
            "run_grjson",
        ),
    )
    | _exact(
        "src/epsimple/utils.py",
        (
            "COLUMN_RENAME_DICT",
            "ID_PREFIX",
            "PROPERTY_RENAME_DICT",
            "VALID_COLUMNS",
            "check_modules",
            "excel2grjson",
        ),
    )
)

_EPSIMPLE_HVAC_EQUALITY_TYPES = (
    "AbsorptionChiller",
    "AirHandlingUnit",
    "Boiler",
    "Chiller",
    "DistrictHeating",
    "ElectricRadiantFloor",
    "ElectricRadiator",
    "FanCoilUnit",
    "HeatPump",
    "PackagedAirConditioner",
    "PhotoVoltaicSystem",
    "RadiantFloor",
    "Radiator",
    "VentilationSystem",
)

_HUMAN_REPRESENTATION_TYPES: Mapping[str, tuple[str, ...]] = {
    "src/epsimple/constants.py": ("AUTOID_PREFIX", "SpecialTag"),
    "src/epsimple/core/construction.py": (
        "FenestrationConstruction",
        "Material",
        "SurfaceConstruction",
    ),
    "src/epsimple/core/hvac.py": (
        *_EPSIMPLE_HVAC_EQUALITY_TYPES,
        "GeothermalHeatPump",
    ),
    "src/epsimple/core/model.py": ("GreenRetrofitModel",),
    "src/epsimple/core/profile.py": ("KoreanUsageProfile",),
    "src/epsimple/core/shape.py": ("Surface",),
    "src/idragon/common.py": ("Version",),
    "src/idragon/constants.py": ("SpecialTag",),
    "src/idragon/dragon/construction.py": (
        "AirBoundary",
        "Glazing",
        "NoMassConstruction",
    ),
    "src/idragon/dragon/hvac.py": ("DomesticHotWater",),
    "src/idragon/dragon/profile.py": ("DaySchedule", "RuleSet", "Schedule"),
    "src/idragon/dragon/shape.py": ("Surface", "Vertex"),
}

_DISPLAY_STRING_TYPES: Mapping[str, tuple[str, ...]] = {
    "src/epsimple/core/construction.py": (
        "FenestrationConstruction",
        "Material",
        "SurfaceConstruction",
    ),
    "src/epsimple/core/hvac.py": (
        *_EPSIMPLE_HVAC_EQUALITY_TYPES,
        "GeothermalHeatPump",
    ),
    "src/epsimple/core/model.py": ("GreenRetrofitModel",),
    "src/epsimple/core/profile.py": ("KoreanUsageProfile",),
    "src/epsimple/core/shape.py": ("Surface",),
    "src/idragon/common.py": ("Version",),
    "src/idragon/dragon/construction.py": (
        "AirBoundary",
        "Glazing",
        "NoMassConstruction",
    ),
    "src/idragon/dragon/hvac.py": (
        "CompressorType",
        "DomesticHotWater",
        "Fuel",
        "PhotoVoltaicPanel",
    ),
    "src/idragon/dragon/profile.py": (
        "DaySchedule",
        "RuleSet",
        "Schedule",
        "ScheduleType",
    ),
    "src/idragon/dragon/shape.py": ("Surface", "Vertex"),
    "src/idragon/imugi.py": ("IDD", "IddField", "IddObject"),
}


if len(BASELINE_SCOPE_KEYS) != EXPECTED_BASELINE_DECISION_COUNT:
    raise RuntimeError("reviewed scope policy baseline must contain exactly 16 keys")

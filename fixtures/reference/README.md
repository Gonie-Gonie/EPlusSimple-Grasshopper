# Reference baselines

This directory contains deterministic outputs generated from the exact
historical Python source pinned in `upstream/upstream.lock.json`. They are
development compatibility fixtures, not runtime dependencies of either Dragon
plugin. Regenerate into `temp` with `.\dev.cmd reference`; update tracked files only
after reviewing intentional compatibility changes.

`python-0.7.0/day-schedule-core-oracle.json` pins 42 ordered CPython 3.12.7
observations for the remaining 14 DaySchedule symbols. Its file hash and byte
count are recorded in the generated `python-0.7.0/manifest.json` alongside the
other reviewed reference artifacts.

`python-0.7.0/rule-set-core-oracle.json` pins 72 ordered CPython 3.12.7
observations for the remaining 24 RuleSet core symbols, including exact alias,
lookup, fallback, summary, ordered-dictionary, and IDF-expression behavior.
Its file hash and byte count are recorded in the same generated manifest.

`python-0.7.0/profile-residual-oracle.json` pins 15 ordered CPython 3.12.7
observations for the final five public symbols in `profile.py`. It records
Profile slot and IDF-export order, Schedule container topology, and the
ScheduleOperationError family while separating exact behavior from reviewed
immutable native adaptations.

`python-0.7.0/usage-profile-core-oracle.json` pins 39 ordered CPython 3.12.7
observations for the 13 public symbols in `epsimple/core/profile.py` that are
inside the native port scope. It covers all 24 embedded usage profiles, their
ordered dictionaries and seven generated schedules, deterministic identity,
database lookup, and invariant CSV parsing while recording reviewed native
adaptations explicitly.

`python-0.7.0/utils-core-oracle.json` pins 12 ordered CPython 3.12.7
observations for `GRJSON_FORMAT`, `validate_type`, `validate_range`, and
`validate_enum` across the exact `epsimple` and `idragon` `utils.py` sources.
It preserves legacy template order and mutability facts plus decorator
acceptance and rejection surfaces, and records the four reviewed immutable,
finite, or strongly typed native adaptations explicitly.

`python-0.7.0/common-core-oracle.json` pins 39 ordered CPython 3.12.7
observations for the 13 unresolved public symbols in `idragon/common.py` while
leaving the already excluded debug representations out of the corpus. It
records the EnergyPlus 24.2.0 and 2026 defaults, ASCII and Unicode-decimal
version parsing, formatting, component order, filenames, identity behavior,
and coercion failures with the exact ten-equivalent versus three-reviewed-
adaptation split.

`python-0.7.0/constants-engineering-oracle.json` pins 24 ordered CPython
3.12.7 observations for the eight engineering symbols in
`idragon/constants.py`. It preserves exact binary64 conversion coefficients,
five declared `Unit` names with the `MM2M`/`W2KW`/`L2M3` identity alias and
three-member iteration, representative engineering products, and the 107 W
default people-activity field. Six members are exact equivalents and the two
Python enum containers are reviewed native API adaptations.

`python-0.7.0/epsimple-constants-numeric-oracle.json` pins 87 ordered CPython
3.12.7 observations for 29 numeric symbols in `epsimple/constants.py`. It
preserves exact binary64 values and products, float-enum construction and
member order, the `Unit.MM_TO_M`/`W_TO_KW` alias, and both sides of the
`Site2Source` contract: all five declared factors and the three-member enum
iteration whose zip leaves the final two result rows unscaled. The latter is a
direct execution of the exact pinned `GreenRetrofitResult.to_source_uses` AST,
with its model-file, AST, method, and `VALID_DIGITS` receipts included. The 24
numeric members are exact equivalents and the five enum containers are
reviewed native container or dispatch adaptations.

`python-0.7.0/epsimple-construction-core-oracle.json` pins 19 ordered CPython
3.12.7 cases for exactly 48 unresolved targets in
`epsimple/core/construction.py`. It executes Material, FenestrationConstruction,
SurfaceConstruction, and Special/Open/Unknown construction state, validation,
ID generation, JSON/dictionary/dragon conversion, embedded-database load/get,
derived U-value/depth/heat-capacity and unique-material behavior, regulation
selection, simple construction creation, reversal, and singleton semantics.
Isolated imports plus a byte-identical relocated `epsimple`/`idragon` source
copy prove path independence. Seven targets are exact equivalents and 41 are
explicit native-route exceptions with stable adaptation IDs. The 12 adjacent
equality, hash, representation, and string declarations remain explicitly
excluded from this bounded fixture.

`python-0.7.0/epsimple-hvac-enums-base-oracle.json` pins six ordered CPython
3.12.7 cases for exactly 28 `CompressorType`, `CoolingTowerControl`,
`CoolingTowerType`, `Fuel`, `NoneSource`, and `SourceSystem` declarations in
`epsimple/core/hvac.py`. It executes enum topology, values, strings, lookups and
dragon conversion; the NoneSource singleton, ID and null conversion; and the
empty base-class and mutable type-mapper behavior with error boundaries.
Isolated imports plus a byte-identical relocated source copy prove path
independence. Eighteen targets are exact equivalents and ten use reviewed CLR
enum, nullable source, sealed aggregate, or GRM dispatch adaptations. The other
116 unresolved declarations are deferred and 58 representation/equality
declarations remain explicitly excluded.

`python-0.7.0/epsimple-hvac-other-systems-oracle.json` pins two ordered
CPython 3.12.7 cases for exactly 17 declarations at inventory indices
`283,284,287,290-295;325,326,329,332-336` in `epsimple/core/hvac.py`. It
executes `PhotoVoltaicSystem` and `VentilationSystem` constructors, process-
identity default IDs, names, defaults, explicit and mutated state, property
validation boundaries, `from_json`, and fresh `to_dragon` conversion. The
observations preserve upstream acceptance of selected bool, NaN, infinity, and
blank-name inputs and the legacy ventilation conversion that omits airflow.
Isolated imports plus a byte-identical relocated source copy prove path
independence. Nine targets are direct ID/property equivalents and eight use
reviewed immutable-aggregate, GRM reader/writer, or GreenRetrofit-conversion
adaptations. All 185 adjacent HVAC declarations are pinned as a fail-closed
non-target closure and cannot be promoted by this fixture.

`python-0.7.0/epsimple-hvac-thermal-source-oracle.json` pins six ordered
CPython 3.12.7 cases for exactly 47 declarations at inventory indices
`135,136,139,142-146;157,158,161,164-169;170,171,174,177-184;
199,200,203,206-208;248,251,252;253,254,257,260-266` in
`epsimple/core/hvac.py`. It executes `AbsorptionChiller`, `Boiler`, `Chiller`,
`DistrictHeating`, `GeothermalHeatPump`, and `HeatPump` constructors, defaults,
explicit and mutated state, property validation, `from_json`, and recursive
`to_dragon` behavior, including all four chiller cooling-tower type/control
branches and fresh conversion outputs. Isolated imports plus a byte-identical
relocated source copy prove path independence. Twenty-four targets are direct
ID/property equivalents and 23 use reviewed sealed `SourceSystem`, GRM-
dispatch, or GreenRetrofit-conversion adaptations. All 155 adjacent HVAC
declarations are pinned as a fail-closed non-target closure and cannot be
promoted by this fixture.

`python-0.7.0/epsimple-hvac-supply-system-oracle.json` pins eight ordered
CPython 3.12.7 cases for exactly 52 declarations at inventory indices
`147,148,151,154-156;209,210,213,216-220,223,226-231,234,237-239;
271,272,275,278-282;296,297,300,303-309,312,315-318;321-324` in
`epsimple/core/hvac.py`. It executes `AirHandlingUnit`,
`ElectricRadiantFloor`, `ElectricRadiator`, `FanCoilUnit`,
`PackagedAirConditioner`, `RadiantFloor`, `Radiator`, and `SupplySystem`
constructors, defaults, explicit and mutated state, source-system validation,
`from_json`, and recursive `to_dragon` behavior, including the packaged-air-
conditioner dedicated source map and the mutable supply-system type mapper.
Isolated imports plus a byte-identical relocated source copy prove path
independence. Nineteen targets are direct equivalents and 33 use reviewed
sealed aggregate, nullable source, GRM-dispatch, or GreenRetrofit-conversion
adaptations. All 150 adjacent HVAC declarations are pinned as a fail-closed
non-target closure and cannot be promoted by this fixture.

`python-0.7.0/epsimple-identifier-conventions-oracle.json` pins 22 ordered
CPython 3.12.7 cases for the 34 unresolved `AUTOID_PREFIX`, `Directory`,
`PackageInfo`, and `SpecialTag` symbols in `epsimple/constants.py`. It records
enum declaration and iteration topology, string and formatting behavior,
construction and lookup failures, mutation and copy aliasing, relocatable
resource-path roles, package metadata operations, and class-versus-instance
attribute semantics. Twenty-three targets are exact equivalents and eleven
use reviewed immutable, embedded-resource, caller-supplied-resource, package-
metadata, or compiled-target adaptations. `AUTOID_PREFIX.__repr__` and
`SpecialTag.__repr__` remain explicitly excluded from this bounded corpus.

`python-0.7.0/epsimple-model-core-oracle.json` pins 11 ordered CPython 3.12.7
cases for exactly 35 unresolved targets in `epsimple/core/model.py`. It executes
the weather tables and lookup, EnergyPlusError state, GreenRetrofitModel
construction and validation, area/exterior projections, six weighted averages
and zero boundaries, source-system and unique-catalog behavior, full temporary
GRM graph loading, adjacency, `to_dragon`, `to_idf`, and an instrumented `run`
success and failure path without starting EnergyPlus. Isolated imports plus a
byte-identical relocated source copy prove path independence. Eleven targets
are exact equivalents and 24 are explicit native-route exceptions. The three
representation/Excel declarations remain out of scope; the 14
GreenRetrofitResult declarations remain explicitly deferred to their own
bounded fixture.

`python-0.7.0/epsimple-model-result-oracle.json` pins 11 ordered CPython
3.12.7 cases for the exact 14 `GreenRetrofitResult` declarations in
`epsimple/core/model.py`. It executes result construction, area and rounding,
domestic-hot-water demand and server selection, site/source energy, carbon and
cost factors, gross and per-area summaries, dictionary topology, and
deterministic JSON file writing with overwrite and error boundaries. Isolated
imports plus a byte-identical relocated source copy prove path independence.
Nine targets are exact equivalents and five use reviewed immutable result,
validated builder, typed DHW, or deterministic writer adaptations. The other
38 declarations in `model.py` are bound as adjacent non-target receipts.

`python-0.7.0/epsimple-shape-core-oracle.json` pins 17 ordered CPython
3.12.7 cases for exactly 53 unresolved targets in
`epsimple/core/shape.py`. It executes BlindType, Fenestration, Door, GlassDoor,
Window, Surface, and Zone state and validation; ID generation; factory and JSON
dispatch; copy and flip behavior; construction/material aggregation; opening
counts; supply-system filtering; and dragon conversion or pinned upstream
failure behavior. Isolated imports plus a byte-identical relocated
`epsimple`/`idragon` source copy prove path independence. Thirty-three targets
are exact equivalents and 20 are explicit native-route exceptions with stable
adaptation IDs. The five adjacent representation/equality declarations remain
explicitly excluded from this bounded fixture.

`python-0.7.0/dragon-construction-core-oracle.json` pins 19 ordered direct-state
CPython 3.12.7 cases for exactly 35 class, constructor, property, enum, and
reversal targets across `Construction`, `Glazing`, `Layer`, `Material`,
`MaterialRoughness`, and `NoMassConstruction`. It preserves default, explicit,
and mutated state; bool, range, and nonfinite boundaries plus error timing;
exact binary64 derived-property operation order; and child/container alias
topology. Eleven targets are exact equivalents and 24 are reviewed native
adaptations. `Construction.reversed` remains an adaptation because the native
model validates names and copies only the container around shared immutable
`Layer` references, while the upstream result shares mutable `Layer` objects.
AirBoundary, representation strings,
equality/hash behavior, and all IDF emission are explicitly outside this
direct-state fixture.

`python-0.7.0/dragon-construction-to-idf-object-oracle.json` pins two common-
valid-state observations each for `AirBoundary.to_idf_object`,
`Construction.to_idf_object`, `Glazing.to_idf_object`, `Layer.to_idf_object`,
and `NoMassConstruction.to_idf_object`. It records complete ordered fields and
fresh returned IDF objects plus fresh lists where the upstream API returns
lists. The Construction cases also
pin the surface argument and its `<construction>:for:<surface>` name linkage.
The distinct reviewed `model-context-air-boundary-idf-emission`,
`model-context-construction-idf-emission`,
`model-context-glazing-idf-emission`, `model-context-layer-idf-emission`, and
`model-context-no-mass-construction-idf-emission` exceptions restrict the
evidence to the corresponding private `EnergyModelIdfAssembler` paths reached
through `EnergyModel.ToIdfDocument`. Class constructors and properties,
equality/hash behavior, invalid-domain and error semantics, isolated
`IdfObject` policy, parent Surface/Zone/model assembly, and native
deduplication, conflict, compaction, and global ordering remain explicit
unresolved boundaries.

`python-0.7.0/dragon-hvac-appenders-controllers-oracle.json` pins six bounded,
relocatable CPython 3.12.7 cases for 24 exact appender/controller receipts.
The cases execute DemandBranchAppender, EquipmentListAppender,
SequentialLoadFractionController, SupplySystemToIdfPostProcessor,
ZoneAirNodeAppender, and ZoneTerminalUnitAppender against deterministic IDF
stubs. They preserve exact append counts and order, the 99-entry boundary,
absent and existing node lists, terminal-list failures, target lookup and
overflow errors, zero/one/multiple active schedules, ALLOFF and epsilon
arithmetic, failure prefixes, and repeated-run mutation. All 24 receipts remain
conservative exceptions because the reviewed public InvisibleDragon route is
the model-level SupplyGroup-to-IdfDocument chain rather than standalone native
appender/controller APIs. Existing SupplyGroup conversion receipt 796 is
immutable hash-pinned support, and the remaining 149 declarations stay
deferred, closing the exact 174-receipt source partition without executing
native or EnergyPlus processes.

`python-0.7.0/dragon-hvac-photovoltaic-to-idf-object-oracle.json` pins three
ordered CPython 3.12.7 observations for `PhotoVoltaicPanel.to_idf_object` on
the common valid constructor domain. It records the exact six-object family
order, complete allowed-field order and values, fresh list/object topology,
square-panel side length derived from `sqrt(area)`, default and custom
effective-area ratios, and cross-object names at tilt/azimuth boundaries and
unit efficiency/ratio values. The
reviewed `compact-native-photovoltaic-idf-emission` mapping targets
`PhotovoltaicPanel.ToIdfObjects`, preserving populated and default semantics
while omitting trailing blank/default native fields. The class and constructor,
property-validation order and errors, invalid or nonfinite domain state,
isolated `IdfObject` policy, and parent `EnergyModel.to_idf` remain explicit
unresolved boundaries.

`python-0.7.0/dragon-hvac-source-tower-core-oracle.json` pins ten bounded,
relocatable CPython 3.12.7 cases for the source-system and cooling-tower core.
Its exact 59 inventory receipts cover the `Fuel` and `CompressorType` enums,
source abstractions and constructors, HeatPump/GeothermalHeatPump, boiler and
chiller variants, four concrete cooling-tower types, and public naming and
capacity branches. The corpus preserves legacy validation, coercion, abstract
class, screw-compressor type, mutable-name, and closed-two-speed capacity
omission quirks. Twenty-seven receipts are equivalent on reviewed public
InvisibleDragon routes and 32 are conservative exceptions. Thirteen adjacent
conversion receipts are hash-pinned to the existing source-system-to-IDF
oracle, while `CompressorType.__str__` and `Fuel.__str__` remain out of scope,
closing the exact 74-receipt family partition without executing native or
EnergyPlus processes.

`python-0.7.0/dragon-hvac-supply-core-oracle.json` pins nine bounded,
relocatable CPython 3.12.7 cases for 49 exact supply-system target receipts.
The cases cover AirHandlingUnit, electric radiant floor/radiator, FanCoilUnit,
PackagedAirConditioner, hydronic radiant floor/radiator, SupplyGroup, and the
SupplySystem abstraction. They execute constructor state, source combinations,
heating/cooling capability, naming, availability, concrete IDF-object order,
validation quirks, deepcopy, and fresh result/projection behavior. Eight
existing adjacent declarations remain unpromoted and preserve their prior
out-of-scope/equivalent/exception status, closing the exact 57-receipt family
partition. Reviewed public InvisibleDragon routes yield 18 equivalents and 31
conservative exceptions; internal `Generate` methods are not claimed. Three
existing SupplyGroup/model fixtures are immutable hash-pinned support.

`python-0.7.0/dragon-hvac-supply-group-core-oracle.json` pins 18 ordered
CPython 3.12.7 observations, three each for the six bounded public
`SupplyGroup` constructor and container-projection symbols. It records exact
validation order and messages, tuple snapshots, valid-domain heat/cool
capability selection, fresh ordered projection tuples, and static first-seen
source ordering across distinct entity identifiers.
Construction uses the reviewed `immutable-validated-supply-group-construction`
adaptation, and source identity de-duplication uses
`stable-entity-id-supply-source-deduplication`; the four capability properties
are exact equivalents on the native valid domain. All twelve loaded local
modules carry exact source and Python 3.12 AST receipts. The `SupplyGroup` class
receipt, `SupplyGroup.to_idf_object`, concrete systems, postprocessors, and full
model IDF assembly remain outside this core corpus.

`python-0.7.0/dragon-hvac-supply-group-to-idf-object-oracle.json` pins three
ordered CPython 3.12.7 observations for the bounded orchestration performed by
`SupplyGroup.to_idf_object`: system/availability zip order, heatability before
coolability reads, aligned availability identity, flattened object and
processor order, immediate custom-availability conversion, and a fresh
`SequentialLoadFractionController` in the final processor position. Separate
system- and availability-failure cases preserve exact prefix side effects and
failure order. The reviewed `model-context-supply-group-idf-assembly` exception
maps this evidence only to `EnergyModel.ToIdfDocument` under assertion
`dragon-hvac-supply-group-to-idf-object-3f9c508c`. It does not claim a
standalone native SupplyGroup converter, `SupplySystem.to_idf_object`,
`SourceSystem.to_idf_object`, the controller class or its `run` behavior,
concrete converters, arbitrary probe acceptance, or full `EnergyModel.to_idf`
compatibility.

`python-0.7.0/dragon-model-add-supply-system-oracle.json` pins three ordered
CPython 3.12.7 observations for `EnergyModel.add_supply_system`: generation
failure before mutation, processor failure after appended objects persist, and
successful append/processor/`None` return order. It binds the reviewed
`model-context-supply-system-assembly` exception to public target
`EnergyModel.ToIdfDocument` and assertion
`dragon-model-add-supply-system-174532d0`. All twelve local modules loaded by
the cases carry exact source and Python 3.12 AST receipts. The corpus is bounded
and does not close `EnergyModel.to_idf`, `SupplyGroup`, concrete systems, or
postprocessors.

`python-0.7.0/dragon-model-assembly-oracle.json` pins ten ordered CPython
3.12.7 observations for bounded `EnergyModel.to_idf` behavior. Five model cases
record exact-name profile replacement and its dangling schedule reference,
case-sensitive profile and schedule emission, the shared unconditioned
thermostat/ALLON fallback, legacy ERV ventilation fields, and the silent
fallback when assigned HVAC lacks availability. One case additionally pins the
exact five-field geometry rules, default 107 W people-activity schedule,
blank-typed ALLON/ALLOFF schedules, and four schedule type-limit objects. Five
additional orchestration cases pin the parent call order, batch-before-append
layer behavior, first-seen source identity de-duplication, repeated conditioned
and unconditioned projections, shared fallback creation, photovoltaic order,
same-document return identity, and exact failure prefixes. The generator audits
all twelve loaded local `idragon` modules against exact source and Python 3.12
AST receipts. This is behavioral evidence only: the symbol remains
`needs_reverification`, with no adaptation, trusted assertion, child-converter
closure, or full-symbol closure claim.

`python-0.7.0/dragon-model-class-oracle.json` targets only public-inventory
index 815, `EnergyModel`, through three CPython 3.12.7 observations: class and
`supported_versions` topology, shared-list append visibility with unconditional
`finally` restoration, and instance shadow/arbitrary-attribute/subclass
topology. The classification exception is
`sealed-read-only-native-energy-model-class-a7582a41`, its assertion is
`dragon-model-energy-model-class-a7582a41`, and its native binding is
`GonieGonie.InvisibleDragon.Model.EnergyModel`. Indices 816-825 are resolved
receipts only; loaded `Version` symbols are context receipts only. Exact source
and Python 3.12 AST receipts cover all twelve loaded local modules. Constructors
and named members are not separate targets or compatibility claims.

`python-0.7.0/dragon-model-conditioning-oracle.json` pins nine ordered
CPython 3.12.7 observations for `EnergyModel.conditioned_zones`,
`EnergyModel.unconditioned_zones`, and `Zone.is_conditioned` across
`idragon/dragon/model.py` and `idragon/dragon/shape.py`. It preserves zone
order, fresh-list and input-object identity semantics, falsey-but-present
profile availability, and the exact supply-plus-profile predicate without
serializing raw Python identities. The two model list properties are exact
equivalents; the zone-local predicate is a reviewed model-context adaptation.

`python-0.7.0/dragon-model-construction-defaults-oracle.json` pins nine
ordered CPython 3.12.7 observations for `EnergyModel.__init__` and
`EnergyModel.create_default_idf`. It preserves the constructor's exact call
shape, shared omitted-list defaults, explicit list aliasing, and permissive raw
assignments, plus the fresh default IDF's exact 17-object family order and raw
fields. Default-IDF creation is an exact equivalent; native construction uses
the reviewed `immutable-validated-energy-model-construction` adaptation.

`python-0.7.0/dragon-model-projections-oracle.json` pins 12 ordered CPython
3.12.7 observations for `EnergyModel.surfaces`, `used_constructions`,
`used_layers`, and `used_profiles`. It preserves nested surface flattening and
source identity, seed-zero SipHash13 construction/layer set order and their
equality/hash edge cases, explicit AirBoundary and NoMass exclusion, and
duplicate-profile last-value replacement without moving the first name slot.
Surface and profile projections are exact
equivalents; the two runtime-specific set orders are reviewed deterministic
first-use native-order adaptations.

`python-0.7.0/dragon-model-terrain-oracle.json` pins 18 ordered CPython
3.12.7 observations for the `Terrain` class and five members in
`idragon/dragon/model.py`. It preserves exact member order, title-case semantic
tokens, value/name construction, and JSON behavior while separately recording
the historical qualified `Terrain.NAME` IDF rendering. The five member values
are exact equivalents; the class is a reviewed typed-native-enum adaptation
that emits valid EnergyPlus terrain tokens.

`python-0.7.0/dragon-shape-shading-material-to-idf-object-oracle.json` pins
six ordered CPython 3.12.7 observations for `Blind.to_idf_object` and
`Shade.to_idf_object`. It records the complete 29- and 15-field EnergyPlus
material order, fresh list/object behavior, Shade emissivity arithmetic,
constructor alias context, permissive invalid numeric emission, and the exact
nonnumeric failure boundary. The reviewed
`model-context-shading-material-idf-assembly` mapping is restricted to valid
material emission through `EnergyModel.ToIdfDocument`; constructors, invalid
native emission parity, standalone converters, shading controls, surfaces, and
parent `EnergyModel.to_idf` remain explicit unresolved boundaries.

`python-0.7.0/launcher-result-parser-oracle.json` pins 21 ordered CPython
3.12.7 observations for the seven result-parser symbols in
`idragon/launcher.py`. It records constructor dispatch and partial failures,
audit duplicates, boundary padding, error-log filtering, the legacy tabular
CSV grammar, pandas NaN and binary64 values, and the explicit absence of ESO
parsing. All seven symbols are bound to reviewed native API adaptations.

`python-0.7.0/launcher-runtime-oracle.json` pins 12 ordered CPython 3.12.7
observations for the remaining four public runtime symbols in
`idragon/launcher.py`. It records the inherited not-found exception surface,
package-versus-system discovery precedence, list broadcasting and cardinality
quirks, version inference, output retention/deletion, and failure cleanup
ordering. All execution uses closed fakes inside a unique controlled temporary
descendant; the fixture contains logical tokens only and makes no active-load
claim. All four symbols are reviewed native API adaptations.

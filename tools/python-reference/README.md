# Python reference oracle

The released Grasshopper plugins do not depend on Python. This tool runs the
historical `epsimple` and `idragon` implementation only as a behavioral oracle
for the C# port.

`.\dev.cmd reference` verifies the exact Python 3.12.7 interpreter selected by
`.\dev.cmd setup`, materializes the commit in `upstream/upstream.lock.json`, installs
the exact requirements into `.tools/python-reference`, and writes all generated
work to `temp/reference/python-output`.

```text
.\dev.cmd reference
.\dev.cmd reference -Mode Verify
.\dev.cmd reference -RefreshDependencies
```

The generators fix `PYTHONHASHSEED`, remove CPython memory addresses from the
generated IDF with a stable first-occurrence mapping, and record hashes for
every output. The construction equality/hash oracle additionally binds its
Material, Layer, and Construction observations to the exact public-symbol
inventory, source bytes, and five upstream symbol hashes. The ScheduleType
oracle binds all 12 upstream ScheduleType symbols and records the four exact
five-field `ScheduleTypeLimits` objects plus 44 boundary, coercion, type-error,
and tagged non-finite validation cases. Its `real` NaN and infinity results use
strict JSON tags instead of non-standard numeric tokens. The DaySchedule
core oracle binds the remaining 14 lifecycle, immutable-update, factory,
compactization, summary, time-grid, IDF-expression, and type symbols through
42 exactly ordered cases. It preserves recursive binary64 hex facts and
runtime-name policy tokens while pinning the exact four equivalent versus ten
reviewed native-adaptation split. The DaySchedule
metrics oracle binds 13 fixed-grid properties and includes a catastrophic
cancellation case that locks the CPython 3.12 compensated-float summation
behavior used by `average`, `integral`, and `positive_average`. The
DaySchedule, RuleSet, and Schedule operation oracles each bind exactly 28
upstream symbols and preserve Python scalar kinds, reverse-operation names,
typed failures, tagged non-finite results, nested fallback topology, and source
immutability. A separate RuleSet core oracle binds the remaining 24 lifecycle,
immutable-slot, factory, lookup, summary, dictionary, IDF-expression, extrema,
and type symbols through 72 exactly ordered cases. It preserves default and
override alias topology, string and positive/negative integer day lookup,
fallback suppression, exact dictionary order, binary64 values, runtime-name
policy tokens, and the exact seven equivalent versus seventeen reviewed
native-adaptation split. The annual Schedule corpus additionally records all
365 days and the exact inclusive compact-period union across asymmetric
operands. A separate Schedule core oracle binds the remaining 22 annual
lifecycle, factory, metric,
summary, IDF, and compact-unification symbols through 104 cases. It records
case-local alias graphs, input postconditions, CPython 3.12 binary64 results,
runtime-name normalization, partial-mutation defects, read-only day-list and
contiguous IDF-field native mappings, and the exact 12 equivalent versus 10
reviewed native-adaptation split. A final profile residual oracle binds the
five remaining public `profile.py` symbols through 15 ordered cases. It pins
Profile slot and IDF-export order, Schedule container topology, and the native
schedule-operation exception family as one equivalent behavior and four
reviewed adaptations. The usage-profile core oracle binds the 13 in-scope
public symbols in `epsimple/core/profile.py` through 39 ordered cases. It pins
the exact 14-key dictionary contract, standard and extended database lookup,
invariant CSV parsing, deterministic IDs, and the complete seven-schedule
conversion for all 24 embedded profiles, including vacation and holiday day
topology. The utils core oracle binds the exact two historical `utils.py`
sources and their four public-symbol receipts through 12 ordered cases. It
records the legacy GRJSON template's exact order, defaults, deep-copy
isolation, and shared-global mutation behavior together with the Python
type, range, and enum decorator surfaces, while declaring the four reviewed
immutable or strongly typed native adaptations explicitly. `-Mode Verify`
compares all generated files byte-for-byte with the reviewed baseline under
`fixtures/reference/python-0.7.0`. Every reference run also executes the
fail-closed generator tests under `tests/PythonReference` before producing an
oracle.

The common core oracle directly imports the pinned `idragon/common.py` bytes
without initializing the surrounding package and binds its 13 unresolved
symbols through 39 ordered cases. It preserves the 24.2.0 and 2026 defaults,
two- and three-component construction, arbitrary nonnumeric delimiters,
Unicode decimal digits, the empty-format separator quirk, ordered iteration,
legacy IDD and EnergyPlus directory names, and the upstream invalid-input
failure surface. Its consumer contract declares ten exact equivalents and the
three reviewed native descriptor, validated-construction, and strongly typed
coercion adaptations.

The constants engineering oracle binds the eight `THERMAL` and `Unit` symbols
in the exact pinned `idragon/constants.py` source through 24 ordered cases. It
records canonical binary64 conversion values and engineering probes, the
five-name versus three-iterated-member `Unit` alias topology, and the 107 W
people-activity value emitted by the default IDF. The two mutable Python enum
containers are reviewed native adaptations; all six engineering members are
exact equivalents. Package directories and Python package metadata are not
part of this engineering corpus.

The dragon-HVAC photovoltaic-to-IDF-object oracle binds three common-valid-
domain cases for `PhotoVoltaicPanel.to_idf_object`. It records the exact
six-object EnergyPlus family sequence and complete allowed fields, fresh result
and object topology, `sqrt(area)` panel dimensions, default and custom
effective-area ratios, cross-object names, tilt/azimuth boundaries, and unit
efficiency/ratio values. The reviewed
`compact-native-photovoltaic-idf-emission` mapping targets
`PhotovoltaicPanel.ToIdfObjects`; its compact native representation preserves
populated and default semantics while omitting trailing blank/default fields.
The class and constructor, property-validation order and errors, invalid or
nonfinite domain state, isolated `IdfObject` policy, and parent
`EnergyModel.to_idf` remain unresolved. It is generated by
`generate_dragon_hvac_photovoltaic_to_idf_object_oracle.py`.

The dragon-HVAC SupplyGroup core oracle binds 18 cases, three each for
`SupplyGroup.__init__`, `coolable`, `cooling_systems`, `heatable`,
`heating_systems`, and `sources`. Logical probes capture exact constructor
validation, tuple snapshots, capability reads, valid-domain projection
order and identity, tuple freshness, static first-seen ordering across distinct
entity identifiers, and source identity de-duplication without serializing
runtime addresses. Constructor validation and stable native
entity-ID source de-duplication are explicit reviewed adaptations; the four
capability projections map to `CanCool`, `CoolingSystems`, `CanHeat`, and
`HeatingSystems`. The generator audits the complete twelve-module local import
graph. It deliberately excludes the `SupplyGroup` class receipt and
`to_idf_object`, which remain outside this core slice. It is generated by
`generate_dragon_hvac_supply_group_core_oracle.py`.

The dragon-HVAC SupplyGroup-to-IDF-object oracle binds three bounded
`SupplyGroup.to_idf_object` orchestration cases: ordered system/availability
zipping and capability reads, flattened child outputs with immediate custom
availability conversion and the sequential controller last, system failure,
and availability failure. Repeated success conversion records fresh result
lists, child lists, and controller identity without serializing runtime
addresses. It uses reviewed adaptation
`model-context-supply-group-idf-assembly`, public target
`EnergyModel.ToIdfDocument`, and assertion
`dragon-hvac-supply-group-to-idf-object-3f9c508c`. The standalone converter API,
`SupplySystem.to_idf_object`, `SourceSystem.to_idf_object`,
`SequentialLoadFractionController` and its `run` method, concrete converters,
arbitrary probe systems/schedules, and full `EnergyModel.to_idf` remain explicit
unresolved boundaries. It is generated by
`generate_dragon_hvac_supply_group_to_idf_object_oracle.py`.

The dragon-model add-supply-system oracle binds three bounded
`EnergyModel.add_supply_system` cases. It records generation failure before
mutation, processor failure after append mutation persists, exception prefixes,
and successful append/processor/`None` return order. The public native target is
`EnergyModel.ToIdfDocument`, using reviewed adaptation
`model-context-supply-system-assembly` and assertion
`dragon-model-add-supply-system-174532d0`. Its external temporary import audits
all twelve loaded local modules against exact source and Python 3.12 AST
receipts. `EnergyModel.to_idf`, `SupplyGroup`, concrete systems, and
postprocessors remain unresolved. It is generated by
`generate_dragon_model_add_supply_system_oracle.py`.

The dragon-model assembly oracle binds ten bounded `EnergyModel.to_idf` cases
against external temporary copies of the exact pinned source graph. Five model
cases record duplicate and case-only profile/schedule naming, ordered shared
unconditioned fallback objects, legacy ERV ventilation fields, an assigned
supply skipped because HVAC availability is absent, and exact raw default
geometry, people-activity, ALLON/ALLOFF, and schedule type-limit fields. Five
probe cases additionally record the complete parent orchestration order,
identity de-duplication, repeated projections, same-document return, and exact
layer/source/supply/PV failure prefixes. The generator fails unless all twelve
loaded sources match exact source and Python 3.12 AST receipts. The fixture
explicitly keeps `EnergyModel.to_idf` as `needs_reverification`: it has no
native adaptation, trusted assertion, child-converter closure, or full-symbol
closure claim. It is generated by `generate_dragon_model_assembly_oracle.py`.

The dragon-shape shading-material-to-IDF-object oracle binds three cases each
for `Blind.to_idf_object` and `Shade.to_idf_object`. It records their exact
EnergyPlus object types and complete ordered field sets, fresh result topology,
Shade emissivity arithmetic, permissive invalid numeric emission, and the
nonnumeric failure point. Constructor aliasing is context only. The reviewed
`model-context-shading-material-idf-assembly` mapping targets valid emission
through `EnergyModel.ToIdfDocument`; constructors, invalid native emission,
standalone conversion, shading controls, surfaces, and parent model assembly
remain unresolved. It is generated by
`generate_dragon_shape_shading_material_to_idf_object_oracle.py`.

The dragon-model conditioning oracle binds
`EnergyModel.conditioned_zones`, `EnergyModel.unconditioned_zones`, and
`Zone.is_conditioned` across the exact pinned `idragon/dragon/model.py` and
`idragon/dragon/shape.py` sources through nine ordered cases. It records zone
order, fresh-list and input-object identity semantics, falsey-but-present
profile availability, and the exact supply-plus-profile predicate using only
logical labels, indices, booleans, and tagged state. The two model list
properties are exact equivalents; the zone-local predicate is one reviewed
model-context adaptation. It is generated by
`generate_dragon_model_conditioning_oracle.py`.

The dragon-model construction-defaults oracle binds `EnergyModel.__init__`
and `EnergyModel.create_default_idf` in the exact pinned
`idragon/dragon/model.py` source through nine ordered cases. It records the
legacy constructor call shape, shared omitted-list defaults, explicit aliasing,
permissive assignments, and the fresh default IDF's exact 17-object family
order and raw fields. Default-IDF creation is an exact equivalent; the native
constructor is the reviewed
`immutable-validated-energy-model-construction` adaptation. It is generated by
`generate_dragon_model_construction_defaults_oracle.py`.

The dragon-model projections oracle binds `EnergyModel.surfaces`,
`EnergyModel.used_constructions`, `EnergyModel.used_layers`, and
`EnergyModel.used_profiles` through 12 ordered cases. It hashes the exact
`construction.py`, `model.py`, `profile.py`, and `shape.py` sources, records
CPython 3.12.7 seed-zero set iteration as reference data, and uses only logical
labels and registry indices for identity. AirBoundary and NoMass inputs also
pin their exclusion from opaque construction and layer projections. The surface and duplicate-name
profile projections are exact equivalents; native construction/layer results
retain the same membership and source objects in deterministic first-use order.
It is generated by `generate_dragon_model_projections_oracle.py`.

The dragon-model Terrain oracle binds the `Terrain` string-enum class and its
five public members in the exact pinned `idragon/dragon/model.py` source through
18 ordered cases. It records declaration and iteration order, exact title-case
EnergyPlus choice tokens, value/name construction, JSON projection, and the
pinned CPython qualified `Terrain.NAME` string that the historical IDF writer
emits. The class is one reviewed typed-native-enum adaptation that corrects the
rendered token for EnergyPlus; the five semantic member values are exact
equivalents.

The launcher result-parser oracle directly loads the pinned
`idragon/launcher.py` together with only its pinned sibling modules, without
executing `idragon.__init__`. Its 21 ordered cases bind the seven
`EnergyPlusResult` class, constructor, audit, boundary, error, ESO, and tabular
parser symbols. DataFrames are normalized as ordered columns, index, dtypes,
and tagged cells; NaN and finite binary64 values never enter JSON as
non-standard numeric tokens. The consumer contract records all seven reviewed
typed native adaptations, including `explicitly-unsupported-energyplus-eso`;
it does not claim that the native port parses ESO output.

The launcher runtime oracle directly loads the same pinned launcher source
under closed fakes. Its 12 ordered cases bind the runtime-not-found exception,
runtime discovery, scalar/broadcast dispatch, and single-run lifecycle through
exactly three cases per symbol. No process or active EnergyPlus load is
executed. Temporary work is confined to one unique controlled descendant, and
only logical path tokens enter the fixture. The consumer contract records four
reviewed structured runtime, verified discovery, bounded batch, and isolated
cancellable single-run adaptations.

Updating the tracked baseline is an explicit review action:

```text
.\dev.cmd reference -UpdateBaseline
```

For auditing an already available exact checkout without allowing the script
to change it, pass `-UpstreamPath <path>`. An explicit checkout must already be
clean, have the pinned origin, and be at the pinned commit.

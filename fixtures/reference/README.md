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

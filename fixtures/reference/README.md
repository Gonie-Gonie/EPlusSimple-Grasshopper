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

`python-0.7.0/dragon-model-conditioning-oracle.json` pins nine ordered
CPython 3.12.7 observations for `EnergyModel.conditioned_zones`,
`EnergyModel.unconditioned_zones`, and `Zone.is_conditioned` across
`idragon/dragon/model.py` and `idragon/dragon/shape.py`. It preserves zone
order, fresh-list and input-object identity semantics, falsey-but-present
profile availability, and the exact supply-plus-profile predicate without
serializing raw Python identities. The two model list properties are exact
equivalents; the zone-local predicate is a reviewed model-context adaptation.

`python-0.7.0/dragon-model-terrain-oracle.json` pins 18 ordered CPython
3.12.7 observations for the `Terrain` class and five members in
`idragon/dragon/model.py`. It preserves exact member order, title-case semantic
tokens, value/name construction, and JSON behavior while separately recording
the historical qualified `Terrain.NAME` IDF rendering. The five member values
are exact equivalents; the class is a reviewed typed-native-enum adaptation
that emits valid EnergyPlus terrain tokens.

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

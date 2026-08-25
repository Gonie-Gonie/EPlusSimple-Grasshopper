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
reviewed adaptations. `-Mode Verify`
compares all generated files byte-for-byte with the reviewed baseline under
`fixtures/reference/python-0.7.0`. Every reference run also executes the
fail-closed generator tests under `tests/PythonReference` before producing an
oracle.

Updating the tracked baseline is an explicit review action:

```text
.\dev.cmd reference -UpdateBaseline
```

For auditing an already available exact checkout without allowing the script
to change it, pass `-UpstreamPath <path>`. An explicit checkout must already be
clean, have the pinned origin, and be at the pinned commit.

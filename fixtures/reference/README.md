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

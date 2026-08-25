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

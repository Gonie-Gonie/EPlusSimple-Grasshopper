# Compatibility fixtures

Fixtures are immutable compatibility inputs or expected outputs copied from the
tracked upstream commit. Generated test runs, semantic diffs, logs, and candidate
outputs must go under `temp`, never beside these files.

The initial pair is the historical upstream `ASHRAE 140 modified` GRM/GRR
baseline used for SimpleDragon compatibility checks. Fixture hashes are
recorded in `upstream/data-hashes.json`.

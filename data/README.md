# Ported reference data

The CSV files under `data/simple-dragon` are exact copies from the historical
upstream commit in `upstream/upstream.lock.json`. Their SHA-256 values are
pinned in `upstream/data-hashes.json` and are verified before reference or
release tests.

They contain the source project's material, construction-regulation, profile,
holiday, climate-region, and address-to-weather metadata used by SimpleDragon.
They are product inputs, not temporary test output. Runtime EPW weather files
are intentionally excluded and must be supplied by the user.

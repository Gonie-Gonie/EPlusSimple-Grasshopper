# GonieGonie upstream tracker

This Python 3.12 development tool compares the pinned historical source with a
candidate source root. It classifies AST-level changes, maps affected
GonieGonie InvisibleDragon and SimpleDragon code and tests, and produces stable
JSON, Markdown, and sync-branch review files.

The tracker uses only the Python standard library. Generated files are allowed
only beneath the repository `temp/` directory and can be deleted at any time.

Validate the lock, coarse port map, compatibility exceptions, public-symbol
scope/inventory, and the exact one-to-one classification matrix:

```text
.\dev.cmd upstream validate
```

`validate` succeeds when every public inventory symbol has exactly one allowed
classification. Its JSON output reports `compatibility.complete: false` while
any symbol remains `needs_reverification`; it never upgrades a coarse
`implemented` or `enhanced` port-map row to symbol-level equivalence.

Regenerate the pinned AST-declared public inventory into disposable `temp/`
output, then compare it byte-for-byte with the tracked manifest:

```text
.\dev.cmd upstream inventory ^
  --source-root temp/upstream-eplussimple ^
  --output temp/upstream-tracker/public-symbol-inventory.json
```

The inventory command requires a clean Git clone at the locked commit and
origin. The policy includes public top-level declarations, public members of
public classes, class dunder methods such as `__init__`, and uppercase assignment
targets as constants (including computed constants); private names,
imports/re-exports, lowercase runtime assignments, and function-local
declarations are excluded explicitly in `upstream/compatibility-scope.json`.

Create a review template for a newly generated inventory without inferring
equivalence:

```text
.\dev.cmd upstream ^
  --public-symbol-inventory temp/upstream-tracker/public-symbol-inventory.json ^
  matrix-template ^
  --output temp/upstream-tracker/compatibility-matrix-template.json
```

Exact symbol-scoped approved exceptions are carried into the template. Every
other symbol starts as `needs_reverification`; `equivalent` requires explicit
evidence, and `exception` requires an exact registered exception identifier.

Write the machine-readable compatibility report using the exact pinned clone:

```text
.\dev.cmd upstream compatibility-report ^
  --source-root temp/upstream-eplussimple ^
  --require-verified-pin
```

Run the fail-closed completion gate:

```text
.\dev.cmd upstream compatibility-gate ^
  --source-root temp/upstream-eplussimple
```

For a valid verified clone, the gate returns exit code `5` until its generated
inventory matches the tracked inventory, every symbol is classified, and no
`needs_reverification` entries remain. Invalid configuration or source identity
returns exit code `2`. Reports and generated templates stay beneath
`temp/upstream-tracker/`.

Generate Python 3.12 AST hashes for a pinned or candidate source tree:

```text
.\dev.cmd upstream hash --source-root <source-root>
```

Compare two trees and write a review package under `temp/upstream-tracker`:

```text
.\dev.cmd upstream compare ^
  --baseline-source <pinned-source-root> ^
  --current-source <candidate-source-root> ^
  --require-verified-pin
```

Use `--fail-on-drift` for a strict scheduled check or
`--fail-on-unmapped` to reject changes that do not resolve through the current
port map. A source export without Git metadata is accepted for local analysis,
but its report records `pin_verified: false`.

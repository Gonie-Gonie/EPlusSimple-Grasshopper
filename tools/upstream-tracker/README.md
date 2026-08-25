# GonieGonie upstream tracker

This Python 3.12 development tool compares the pinned historical source with a
candidate source root. It classifies AST-level changes, maps affected
GonieGonie InvisibleDragon and SimpleDragon code and tests, and produces stable
JSON, Markdown, and sync-branch review files.

The tracker uses only the Python standard library. Generated files are allowed
only beneath the repository `temp/` directory and can be deleted at any time.

Validate the lock, coarse port map, compatibility exceptions, public-symbol
scope/inventory, the exact one-to-one classification matrix, and the two
hash-bound registries:

```text
.\dev.cmd upstream validate
```

`validate` succeeds when every public inventory symbol has exactly one allowed
classification. Its JSON output reports `compatibility.complete: false` while
any symbol remains `needs_reverification`; it never upgrades a coarse
`implemented` or `enhanced` port-map row—or a prepared evidence-registry
entry—to symbol-level equivalence.

Regenerate the pinned AST-declared public inventory into disposable `temp/`
output, then compare it byte-for-byte with the tracked manifest:

```text
.\dev.cmd upstream inventory ^
  --source-root temp/reference/upstream/eplussimple ^
  --output temp/upstream-tracker/public-symbol-inventory.json
```

The inventory command requires a byte-exact Git clone at the locked commit and
origin. Pin verification ignores index shortcuts and compares every working
file directly with its `HEAD` blob; hidden `assume-unchanged`/`skip-worktree`
changes, replacement objects, missing files, and all untracked files are
rejected. Managed reference clones disable `core.autocrlf` before checkout so
this byte-level result is reproducible on Windows. Inventory schema v2 preserves
`symbol_hash`, `signature_hash`, and
`body_hash` for every exact path/symbol pair as well as the pinned commit. The
policy includes public top-level declarations, public members of public
classes, class dunder methods such as `__init__`, and uppercase assignment
targets as constants (including computed constants); private names,
imports/re-exports, lowercase runtime assignments, and function-local
declarations are excluded explicitly in `upstream/compatibility-scope.json`.

If a previously tracked inventory came from a line-ending-converted checkout,
rebind its dependent registries only after proving the complete AST and symbol
contract is unchanged:

```text
.\dev.cmd upstream rebase-inventory ^
  --replacement-inventory temp/upstream-tracker/public-symbol-inventory.json ^
  --output-dir temp/upstream-tracker/rebased
```

The command fails if any path, AST hash, symbol, signature, body, or symbol hash
changed; it writes generated candidates beneath `temp/` and never overwrites
the reviewed manifests directly.

`upstream/symbol-evidence.json` binds an implementation symbol and one or more
exact assertion ids to that upstream symbol hash. A receipt definition must
name one test file and one test symbol—globs and directory-level claims are
rejected. Implementation and test files must be regular files committed in the
repository `HEAD`, their declared Python or C# symbols must be present, and
their source SHA-256 bindings must still match. PowerShell source bindings stay
disabled until an AST-backed inspector is available. Definitions declaring skipped, failed, or
structural-only evidence are invalid, and an active-load claim must declare a
nonzero exercised load. The registry can contain prepared evidence for a
`needs_reverification` symbol, but that does not change its matrix
classification.

`upstream/scope-decisions.json` records every `out_of_scope` decision separately
by path, symbol, and upstream symbol hash. The matrix must reference the exact
decision id. Missing, extra, duplicate, stale, or broad scope decisions fail
configuration validation.

Regenerate the reviewed native-product scope integration deterministically:

```text
.\dev.cmd upstream apply-safe-scope
```

The command writes candidate `scope-decisions.json` and
`compatibility-matrix.json` files beneath `temp/upstream-tracker/safe-scope`.
Its checked-in policy selects exactly 250 hash-bound symbols: the original 16
approved decisions plus 234 reviewed Python adapter/protocol decisions. The
selection-key and exact-symbol-contract digests are fixed, so an upstream API
change fails closed instead of silently expanding the excluded surface. Eleven
mixed or production authoring methods, including `IdfObjectList.insert` (`IDF.append`,
the ordinary IDF field and container accessors/mutators/formatters, and
`StaticIndexedDict` accessors)
remain `needs_reverification` explicitly.

After reviewing the generated candidates, apply the same byte-deterministic
result to the canonical registries with:

```text
.\dev.cmd upstream apply-safe-scope --write-canonical
```

Canonical writes require the existing compatibility manifests to be clean,
exact `HEAD` files. Re-running the generator after integration reports zero new
decisions and produces files byte-identical to the canonical pair.

Create a review template for a newly generated inventory without inferring
equivalence:

```text
.\dev.cmd upstream ^
  --public-symbol-inventory temp/upstream-tracker/public-symbol-inventory.json ^
  matrix-template ^
  --output temp/upstream-tracker/compatibility-matrix-template.json
```

Every symbol starts as `needs_reverification`. An approved exception is bound
to the exact upstream symbol hash, but its declaration alone is still only a
candidate: both `equivalent` and `exception` rows require exact executed
assertion receipts before the matrix can complete them.

Write the machine-readable compatibility report using the exact pinned clone:

```text
.\dev.cmd upstream compatibility-report ^
  --source-root temp/reference/upstream/eplussimple ^
  --require-verified-pin
```

For every matrix entry classified `equivalent` or `exception`, the report also
requires an executed assertion-result artifact. Test collectors emit
`goniegonie.upstream-evidence-results.v1` JSON containing the exact
`assertion_id`, pass/skip/structural flags, exercised-load category, and a
deterministic `output_sha256`. Each result is also bound to the exact test path,
test symbol, and test-source SHA-256; the artifact itself records the exact
collector path, symbol, and source SHA-256. It also carries the complete
`symbol-evidence.json` content hash, so a result collected before any
implementation, test, receipt, or expected-output binding changed is rejected.
Supply one or more collector artifacts to the diagnostic report with:

```text
.\dev.cmd upstream compatibility-report ^
  --source-root temp/reference/upstream/eplussimple ^
  --evidence-results temp/test-results/core-symbol-receipts.json ^
  --evidence-results temp/test-results/energyplus-symbol-receipts.json
```

The report rejects missing or duplicate assertions, failed/skipped/structural-only
results, stale inventory or evidence-registry bindings, output-hash mismatches,
and zero-load results used to claim active-load behavior. A static registry
declaration alone never satisfies `gate.required_symbol_evidence_satisfied`
when any `equivalent` or `exception` row requires assertions.

Externally supplied JSON is deliberately non-authoritative: it can diagnose
exact binding problems, but it cannot make the release compatibility gate pass.
Only result artifacts produced by a test subprocess launched by the gate itself
may set `evidence_execution.authoritative=true`. Until such a checked-in
collector is configured, every `equivalent` or `exception` row remains
fail-closed even when a well-formed external result JSON is supplied.

Run the fail-closed completion gate:

```text
.\dev.cmd upstream compatibility-gate ^
  --source-root temp/reference/upstream/eplussimple
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

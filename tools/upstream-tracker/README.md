# GonieGonie upstream tracker

This Python 3.12 development tool compares the pinned historical source with a
candidate source root. It classifies AST-level changes, maps affected
GonieGonie InvisibleDragon and SimpleDragon code and tests, and produces stable
JSON, Markdown, and sync-branch review files.

The tracker uses only the Python standard library. Generated files are allowed
only beneath the repository `temp/` directory and can be deleted at any time.

Validate the lock, port map, and compatibility exceptions:

```text
.\dev.cmd upstream validate
```

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

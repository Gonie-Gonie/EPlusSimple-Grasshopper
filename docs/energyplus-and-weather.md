# EnergyPlus and weather

## No setup paths in the normal graph

The canonical Grasshopper workflow does not ask for an EnergyPlus executable, Energy+.idd, runtime root, EPW path, temp root, or output working directory.

`SD Model` selects weather from Address and Vintage. `SD to IDF` verifies and prepares that packaged EPW and emits an opaque typed `Weather` value. `Run InvisibleDragon` consumes the typed IDF and Weather, resolves the supported runtime, and runs EnergyPlus asynchronously.

The module-owned locations are:

```text
Runtime: %LOCALAPPDATA%\GonieGonie\BuildingEnergyRuntime\EnergyPlus\24.2.0-94a887817b
Weather: %LOCALAPPDATA%\GonieGonie\BuildingEnergyWeather\SimpleDragon\korean-tmy-v1
Runs:    %TEMP%\GonieGonie\Dragons\energyplus-runs
```

These locations are implementation details rather than Grasshopper inputs. They are per-user and writable without administrator rights. Rhino's installation folders are never used as write targets.

## Pinned runtime

The supported runtime is EnergyPlus 24.2.0 build `94a887817b`. The executable, IDD, ExpandObjects executable, and official Windows archive are pinned by size and SHA-256. A directory that merely has the expected name is not trusted.

InvisibleDragon packages carry the unchanged pinned archive. On the first explicit run, the module reuses a verified per-user cache or transactionally prepares it from that archive. A saved `Run=True` is only a baseline; a new False-to-True edge is required, so opening a document never extracts a runtime or launches EnergyPlus.

## Packaged weather

SimpleDragon packages carry the pinned `KoreanTMY-v1.zip`. It contains 80 root EPWs covering all 78 unique filenames referenced by the address database. Only the address-selected EPW is atomically extracted and content-hash verified.

InvisibleDragon does not choose or download weather. It accepts the verified `Weather` handle produced by SimpleDragon and rejects an artifact that is missing or whose SHA-256 has changed. Local paths are deliberately omitted from Grasshopper display text and ports.

Developer setup verifies the same archives used by candidate packaging. Released plugins do not need Python, the .NET SDK, or a machine-wide EnergyPlus installation.

## Security and recovery

Runtime bootstrap rejects archive traversal, links, device paths, excessive entries/expanded size, and hash mismatches. Staging is promoted atomically. Failed or cancelled operations clean their owned partial directories.

If a runtime or weather cache becomes invalid, the next explicit solve reports a structured diagnostic and can prepare the module-owned cache again. Historical components with explicit path inputs retain their GUIDs only for old-file compatibility and are hidden from the normal palette.

Successful simulations remove their temporary working directories after the
result is parsed. Failed or cancelled simulations are retained below
`%TEMP%\GonieGonie\Dragons\energyplus-runs` so their EnergyPlus output and logs
can be inspected; that whole location is disposable after diagnosis.

The local candidate mechanism does not establish redistribution rights. Public publication remains unauthorized as recorded in `NOTICE.md`.

# EnergyPlus and weather

## No setup paths in the SimpleDragon graph

The canonical SimpleDragon workflow does not ask for an EnergyPlus executable,
Energy+.idd, runtime root, EPW path, temp root, or output working directory.

`SD Model` records Address and Vintage. `Run SimpleDragon` uses them to select
and verify the packaged EPW, converts the GRM to the execution IDF, resolves the
supported runtime, runs EnergyPlus asynchronously, and returns a GRR. IDF and
Weather are internal values rather than Grasshopper ports in this workflow.

The module-owned locations are:

```text
Runtime: %LOCALAPPDATA%\GonieGonie\BuildingEnergyRuntime\EnergyPlus\24.2.0-94a887817b
Weather: %LOCALAPPDATA%\GonieGonie\BuildingEnergyWeather\SimpleDragon\korean-tmy-v1
Runs:    %TEMP%\GonieGonie\Dragons\energyplus-runs
```

These locations are implementation details rather than Grasshopper inputs. They are per-user and writable without administrator rights. Rhino's installation folders are never used as write targets.

## Pinned runtime

The supported runtime is EnergyPlus 24.2.0 build `94a887817b`. The executable, IDD, ExpandObjects executable, and official Windows archive are pinned by size and SHA-256. A directory that merely has the expected name is not trusted.

InvisibleDragon packages carry the unchanged pinned archive. On the first
explicit run, the internal execution layer reuses a verified per-user cache or
transactionally prepares it from that archive. A matching InvisibleDragon
package provides the offline archive to a SimpleDragon run without placing an
InvisibleDragon component on the canvas; the verified official download is the
fallback when the archive is unavailable. Connect a Grasshopper Button to Run
and press it for a momentary action pulse; its resting False value ensures that
opening a document never extracts a runtime or launches EnergyPlus.

## Packaged weather

SimpleDragon packages carry the pinned `KoreanTMY-v1.zip`. It contains 80 root EPWs covering all 78 unique filenames referenced by the address database. Only the address-selected EPW is atomically extracted and content-hash verified.

SimpleDragon selects weather for its own direct runner and rejects an artifact
that is missing or whose SHA-256 has changed. The verified Weather handle and
local path remain inside the component.

## Standalone InvisibleDragon weather boundary

InvisibleDragon does not select, acquire, or infer weather. A deliberate
standalone execution graph supplies one user-owned local file:

```text
ID Model -> Compile InvisibleDragon -> IDF --------+-> Run InvisibleDragon
                                                   ^
EPW File -> Verify InvisibleDragon Weather --------+
```

`Verify InvisibleDragon Weather` (`ID Weather`) accepts an EPW file, verifies
the local artifact, records its content hash, and emits the typed Weather value
required by `Run InvisibleDragon`. The runner checks that the same artifact is
still present and unchanged before starting EnergyPlus. EnergyPlus, IDD,
runtime-cache, and temporary-work paths remain internal; only this intentionally
selected EPW path appears on a standalone InvisibleDragon canvas.

An unconnected or data-empty EPW File input is a safe no-op: it does not read a
path, emit a Weather value, or report a path error. The tracked standalone
example connects a momentary Grasshopper Button to Run; the unpressed Button
rests at False, so the example opens and recomputes without touching a weather
file or starting EnergyPlus.

Developer setup verifies the same archives used by candidate packaging. Released plugins do not need Python, the .NET SDK, or a machine-wide EnergyPlus installation.

## Security and recovery

Runtime bootstrap rejects archive traversal, links, device paths, excessive entries/expanded size, and hash mismatches. Staging is promoted atomically. Failed or cancelled operations clean their owned partial directories.

If a runtime or weather cache becomes invalid, the next explicit run reports a
structured diagnostic and can prepare the module-owned cache again. The
SimpleDragon Grasshopper product exposes one managed path-free run boundary;
conversion, preparation, compilation, and execution stay behind it.

Successful simulations remove their temporary working directories after the
result is parsed. Failed or cancelled simulations are retained below
`%TEMP%\GonieGonie\Dragons\energyplus-runs` so their EnergyPlus output and logs
can be inspected; that whole location is disposable after diagnosis.

The local candidate mechanism does not establish redistribution rights. Public publication remains unauthorized as recorded in `NOTICE.md`.

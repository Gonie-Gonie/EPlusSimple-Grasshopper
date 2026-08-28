# Verified Grasshopper examples

This tool generates eight tracked Grasshopper definitions and two Rhino building
models with Rhino 7, then validates them inside real Rhino 7 and Rhino 8 hosts.
It is intentionally separate from the package host smoke gate.

Run validation from the repository root after `.\dev.cmd setup`:

```powershell
.\dev.cmd examples
```

Regenerate the canonical examples with Rhino 7, then validate them in both
supported host generations:

```powershell
.\dev.cmd examples -Generate
```

Rhino 7 is the canonical writer so the committed `.gh` and `.3dm` files remain
readable by the oldest supported host. Rhino 8 only writes round-trip copies
below `temp/`. Every build log, host log, summary, and round-trip artifact is
written below `temp/example-definitions/run-*`. Generation stages candidates
there and only replaces tracked files after they reopen and pass their checks.

Definition checks cover exact object/component identities, source order for
every wire and its exact total, runtime errors, typed outputs, selected
Boolean/numeric results, outward envelope winding, solve, save, reopen, and
round trip. Hosts run from a disposable system-temp directory outside the
repository, and the result example proves that saved-document-relative file
paths do not depend on the host process working directory.
The InvisibleDragon single-zone example persists a guarded Prepare-to-Run-to-
Result path, and the SimpleDragon two-zone run example adds the typed Result-to-
GRR-to-CSV path plus a separate batch path. Every persisted preparation,
execution, cancellation, repair, overwrite, and export trigger must remain
false. When EnergyPlus and an EPW are ready, the host enables the two-zone
operations only in memory; disabled or unavailable states remain explicitly
`Not Run` in the summary.
Building-model checks cover metre units, layers, object names and user strings,
closed solid zone Breps, exact bounds, expected adjacency pairs, and closed
planar window curves. The two-zone GRM-to-IDF graph also proves that its
internalized Breps and curves match `30-two-zone-office.3dm`.

Use `-Target Rhino7` or `-Target Rhino8` for a single validation host. Generation
requires the default `-Target All`: Rhino 7 writes every canonical binary, then
Rhino 7 and Rhino 8 both validate it before the command succeeds. Custom Rhino
executable locations can be supplied with `-Rhino7Exe` and `-Rhino8Exe`.
Use `-EnergyPlusRoot` and `-WeatherPath` for explicit runtime inputs, or
`-SkipEnergyPlusWorkflow` to verify the disabled state without executing a
simulation.

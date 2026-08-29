# Troubleshooting

## The Grasshopper tab does not appear

Close all Rhino processes, verify that the package targets the running Rhino generation, and unblock the downloaded archive before reinstalling. Do not copy only the GHA; its adjacent `GonieGonie.*` dependencies are required.

For a source checkout, rerun `./dev.cmd setup` after installing Rhino, then run `./dev.cmd build`. Generated host logs are below `temp/grasshopper-smoke`; stable reports are below `artifacts/reports`.

## One Dragon loads and the other fails

This usually indicates mixed shared assemblies. Close Rhino, remove both installed product directories, and reinstall both products from the same release commit. `package-index.json` and `checksums.sha256` identify a matching candidate set.

## EnergyPlus is not found

Use `Run InvisibleDragon`; no separate Prepare Runtime component is needed. Toggle Run False, allow one Grasshopper solution, then toggle True. InvisibleDragon verifies the per-user cache and prepares the pinned bundled runtime when needed.

If it still fails, inspect the structured diagnostics. For a source checkout, run `./dev.cmd setup` and `./dev.cmd install` again with Rhino closed. Do not point the graph at Rhino's `Program Files` directory or manually copy an unverified EnergyPlus folder.

## An action stays idle

This is expected when a saved Boolean is already True. Set it False, allow one solution, then set it True. The same edge rule prevents Run, Cancel, Write, Export, and Batch actions from repeating on document load or ordinary recompute.

## The simulation has no weather

Connect `SD Model` to `SD to IDF`, then connect its typed `Weather` output directly to `Run InvisibleDragon`. The GRM Address and Vintage must resolve to a supported Korean weather record.

If Weather remains empty, inspect `SD.GH.WEATHER_*` and `SD.WEATHER.*` diagnostics, then rebuild/reinstall both products from the same candidate. There is no EPW path panel in the canonical workflow.

## Rhino reports access denied when Run is clicked

The canonical runner uses `%LOCALAPPDATA%` for verified runtime/weather caches and `%TEMP%/GonieGonie/Dragons` for simulation work. It does not write below Rhino's installation directory and requires no administrator rights.

If access is still denied, verify that the current Windows profile can write to
LocalAppData and the operating-system temp directory, reinstall the current
packages, and include the diagnostic code in the report.

## An opening has no host face

Connect each `SD Opening` only to its owning `SD Zone`. Its curve must be a closed planar polygon lying on exactly one planar Brep face and contained by that face. No Zone Index or Face Index is required. Coincident duplicate faces produce an ambiguity diagnostic instead of an arbitrary assignment.

## A model compiles but EnergyPlus reports severe errors

Read the structured Diagnostics and Result outputs. Check source/supply compatibility, direct Zone HVAC ownership, duplicate explicit IDs, positive capacities and flows, schedule ranges, construction references, and the model address. A successful run removes its temporary working directory after parsing the result. A failed or cancelled run is retained below `%TEMP%\GonieGonie\Dragons\energyplus-runs` so its EnergyPlus output and logs can be inspected.

## Cleaning local work

`.\dev.cmd clean -TempOnly` removes only the disposable repository `temp`
tree; `.\dev.cmd clean` also removes generated artifact content after validating
the target paths. Both preserve `.tools`, tracked artifact documentation,
GH/3DM/GRM/GRR files, per-user runtime/weather caches, and retained system-temp
simulation failures. Remove a retained failure directory only after collecting
the diagnostics you need.

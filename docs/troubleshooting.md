# Troubleshooting

## The Grasshopper tab does not appear

Close all Rhino processes, verify that the package targets the running Rhino generation, and unblock the downloaded archive before reinstalling. Do not copy only the GHA; its adjacent `GonieGonie.*` dependencies are required.

For a source checkout, rerun `./dev.cmd setup` after installing Rhino, then run
`./dev.cmd build`. Stable reports are below `artifacts/reports`; the newest
failed host run remains below `temp/grasshopper-smoke` until the next workflow.

## One Dragon loads and the other fails

This usually indicates mixed shared assemblies. Close Rhino, remove both installed product directories, and reinstall both products from the same release commit. `package-index.json` and `checksums.sha256` identify a matching candidate set.

## EnergyPlus is not found

Use `Run SimpleDragon`; no To-IDF, Weather, InvisibleDragon Run, or separate
Prepare Runtime component is needed. Toggle Run False, allow one Grasshopper
solution, then toggle True. The component verifies the per-user cache and asks
the internal execution layer to prepare the pinned runtime when needed.

For deliberate standalone InvisibleDragon execution, connect
`ID Model -> Compile InvisibleDragon -> Run InvisibleDragon` and
`EPW File -> ID Weather -> Run InvisibleDragon`. EnergyPlus and IDD paths remain
internal; the EPW is the only user-selected execution path.

If it still fails, inspect the structured diagnostics. For a source checkout, run `./dev.cmd setup` and `./dev.cmd install` again with Rhino closed. Do not point the graph at Rhino's `Program Files` directory or manually copy an unverified EnergyPlus folder.

## An action stays idle

For Run, Cancel, and Managed Batch, this is expected when a saved Boolean is
already True. Set it False, allow one solution, then set it True. Write GRM,
Write GRR, and Export CSV are currently level-triggered instead: they attempt
the action on each solution while True, so return them to False immediately
after writing.

## The simulation has no weather

Connect `SD Model` directly to `Run SimpleDragon`. The GRM Address and Vintage
must resolve to a supported Korean weather record; weather selection,
verification, and handoff are internal.

If the run reports a weather failure, inspect `SD.GH.WEATHER_*` and
`SD.WEATHER.*` diagnostics, then rebuild/reinstall both products from the same
candidate. There is no EPW path panel in the canonical SimpleDragon workflow.

For standalone InvisibleDragon, select a local `.epw` file in the `EPW File`
parameter and connect it to `ID Weather`. Success must be True before its Weather
output can feed `Run InvisibleDragon`. If the parameter is empty, the verifier
is intentionally a no-op; it does not access a path or emit an error.

## Rhino reports access denied when Run is clicked

The canonical runner uses `%LOCALAPPDATA%` for verified runtime/weather caches and `%TEMP%/GonieGonie/Dragons` for simulation work. It does not write below Rhino's installation directory and requires no administrator rights.

If access is still denied, verify that the current Windows profile can write to
LocalAppData and the operating-system temp directory, reinstall the current
packages, and include the diagnostic code in the report.

## An opening has no host Surface

Connect a Fenestration Construction to each `SD Opening`, then connect that
completed Opening only to its owning `SD Wall`, `SD Ceiling`, or `SD Floor`--in
the usual case, its `SD Wall`. Its curve must be a closed planar polygon
coplanar with and contained by that component's single-face Brep. A trimmed
inner loop also needs a geometrically matching explicit Opening; the module does
not invent fallback opening metadata. No Zone Index or Face Index is required.

## A Zone does not form a valid enclosure

Connect the complete outputs from `SD Floor`, `SD Ceiling`, and `SD Wall` to the
Zone's Surfaces input. The chosen component fixes each type, so there is no Type
input to populate. Select Boundary Condition by name on the input instead of
supplying an integer code; Floor defaults to Ground, while Ceiling and Wall
default to Outdoors. Every Surface must have one valid planar face and
compatible openings. Coincident opposite Surfaces with the Outdoors Boundary
Condition in two different Zones are paired automatically; do not add
adjacent-zone or face IDs.
Also supply the Zone Height explicitly in metres.
Coincident duplicate faces produce an ambiguity diagnostic instead of an
arbitrary assignment.

## A face list creates unexpected Zones or Opening hosts

Floor, Ceiling, and Wall geometry inputs use item access, so a connected list
or Data Tree is vectorized and its branch paths are preserved. A Zone consumes
the complete Surfaces list on each branch. If one input produces several Zones,
inspect the paths before flattening or merging and make one enclosure branch
per intended Zone.

The Openings port is a whole branch-local ownership list. Group opening-free
walls in one face list, but keep an opening-bearing wall separate unless its
tree paths deliberately match only that wall. Otherwise Grasshopper data
matching can apply the same Opening list to unrelated faces.

## A model compiles but EnergyPlus reports severe errors

Read the structured Diagnostics and Result outputs. Check source/supply compatibility, direct Zone HVAC ownership, indistinguishable duplicate authored objects, positive capacities and flows, schedule ranges, construction references, and the model address. Entity IDs are generated internally, so resolve a duplicate-identity diagnostic by making the relevant authored names, geometry, or connected definitions distinct. A successful run removes its temporary working directory after parsing the result. A failed or cancelled run is retained below `%TEMP%\GonieGonie\Dragons\energyplus-runs` so its EnergyPlus output and logs can be inspected.

## Cleaning local work

Each non-clean top-level `dev.cmd` workflow empties exact-name heavy run
collections before execution. On success it removes the new Grasshopper smoke,
example, trusted-evidence, and release-test runs too; their durable evidence is
already tracked or copied under `artifacts`. On failure it retains only the
newest run until the next explicit workflow. Small install receipts always keep
their newest entry. The policy never selects an unknown name and leaves reusable
build/reference workspaces intact. A repository-local lease prevents supported
concurrent workflows from deleting the current run; `-WhatIf` creates no lease
and performs no automatic deletion.

`.\dev.cmd clean -TempOnly` removes the complete disposable repository `temp`
tree; `.\dev.cmd clean` also removes generated artifact content and ignored
source-tree caches after validating the target paths. Use
`.\dev.cmd clean -CachesOnly` to remove only ignored `bin`, `obj`,
`TestResults`, `__pycache__`, and `.pytest_cache` directories beneath source,
test, script, and tool roots. All modes preserve `.tools`, tracked artifact documentation,
GH/3DM/GRM/GRR files, per-user runtime/weather caches, and retained system-temp
simulation failures. Remove or copy a retained failure directory only after
collecting the diagnostics you need.

# Troubleshooting

Start here when an installed Dragon package, model, or simulation does not behave as expected.

## The Grasshopper tab does not appear

Close all Rhino processes, verify that the package targets the running Rhino
generation, and unblock the downloaded archive before reinstalling. Do not copy
only the GHA; its adjacent `Dragons.*` dependencies are required.

## One Dragon loads and the other fails

This usually indicates mixed shared assemblies. Close Rhino, remove both installed product directories, and reinstall both products from the same release commit. `package-index.json` and `checksums.sha256` identify a matching candidate set.

## EnergyPlus is not found

Use `Run SimpleDragon`; no To-IDF, Weather, InvisibleDragon Run, or separate
Prepare Runtime component is needed. Connect a Grasshopper Button to Run, let
the definition solve once with the Button unpressed, and press it once. The
component verifies the per-user cache and asks the internal execution layer to
prepare the pinned runtime when needed.

For deliberate standalone InvisibleDragon execution, connect
`ID Model -> Compile InvisibleDragon -> Run InvisibleDragon` and
`EPW File -> ID Weather -> Run InvisibleDragon`. EnergyPlus and IDD paths remain
internal; the EPW is the only user-selected execution path.

If it still fails, inspect the structured diagnostics and reinstall the complete
matching package with Rhino closed. Do not point the graph at Rhino's `Program
Files` directory or manually copy an unverified EnergyPlus folder.

## An action stays idle

Every action input should be driven by a momentary Grasshopper Button, not a
Toggle: Run, Cancel, Managed Batch Run/Cancel, Write GRM, Write GRR, and Export
CSV. Let the definition solve once with a newly connected Button at rest, then
press it. Force Rerun and Overwrite are persistent option Toggles; changing
either one alone does not launch an action.

## The simulation has no weather

Connect `SD Model` directly to `Run SimpleDragon`. The GRM Address and Vintage
must resolve to a supported Korean weather record; weather selection,
verification, and handoff are internal.

If the run reports a weather failure, inspect `SD.GH.WEATHER_*` and
`SD.WEATHER.*` diagnostics, then reinstall both products from the same release.
There is no EPW path panel in the canonical SimpleDragon workflow.

For standalone InvisibleDragon, select a local `.epw` file in the `EPW File`
parameter and connect it to `ID Weather`. Success must be True before its Weather
output can feed `Run InvisibleDragon`. If the parameter is empty, the verifier
is intentionally a no-op; it does not access a path or emit an error.

## Rhino reports access denied when Run is clicked

The canonical runner uses `%LOCALAPPDATA%` for verified runtime/weather caches and `%TEMP%/Dragons` for simulation work. It does not write below Rhino's installation directory and requires no administrator rights.

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

Read the structured Diagnostics and Result outputs. Check source/supply compatibility, direct Zone HVAC ownership, indistinguishable duplicate authored objects, positive capacities and flows, schedule ranges, construction references, and the model address. Entity IDs are generated internally, so resolve a duplicate-identity diagnostic by making the relevant authored names, geometry, or connected definitions distinct. A successful run removes its temporary working directory after parsing the result. A failed or cancelled run is retained below `%TEMP%\Dragons\energyplus-runs` so its EnergyPlus output and logs can be inspected.

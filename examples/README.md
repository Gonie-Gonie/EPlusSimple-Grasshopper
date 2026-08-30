# Grasshopper and Rhino examples

These examples are executable project files, not screenshots or pseudocode.
The `.gh` files contain public Dragon components, persisted wires, inputs, and
preview panels. The `.3dm` files use metres and contain named closed Breps and
window curves that can be referenced directly from Grasshopper.

| File | Coverage |
| --- | --- |
| `00-invisibledragon-material-construction.gh` | Minimal InvisibleDragon Material -> Construction Layer -> Construction -> U-value graph |
| `01-invisibledragon-envelope-profile.gh` | Three-layer envelope, no-mass construction, constant annual profile, and typed output previews |
| `02-invisibledragon-single-zone-hvac-idf.gh` | Window→Surface→Zone and HVAC/ERV→Zone direct ownership, PV, energy model, and path-free EnergyPlus 24.2 IDF compile/validation |
| `10-simpledragon-material-construction.gh` | Minimal SimpleDragon Material -> Construction Layer -> Surface Construction graph |
| `11-simpledragon-envelope-hvac.gh` | Three-layer envelope, fenestration, packaged usage profile, three compatible source/supply families, ERV, and PV |
| `12-simpledragon-two-zone-to-idf.gh` | Complex two-Zone composition/IDF authoring: Fenestration Construction -> Opening -> local Zone, west heat-pump/AHU, east boiler/radiator, dedicated ERVs, PV, then path-free IDF and packaged Weather preparation |
| `13-simpledragon-results-and-plots.gh` | Real GRR read, annual summary, monthly DataTree, line plot, bar plot, and non-writing CSV preview |
| `14-simpledragon-two-zone-run-results-csv.gh` | Stable end-to-end flow: dedicated electric radiators and ERVs connect to their own Zones, typed IDF and Weather feed managed InvisibleDragon Run, and a typed Batch Case feeds managed batch without parallel model/ID lists |
| `30-two-zone-office.3dm` | Two adjacent named office-zone solids and two named south-window curves |
| `31-three-zone-stepped-office.3dm` | Two adjacent ground-floor zones plus an adjacent upper zone and three named windows |

## Run locally

Install the current Dragon build, close and reopen Rhino, then open any `.gh`
file in Grasshopper:

```powershell
.\dev.cmd install
```

Opening an example manually is safe: Run, Cancel, Force, Export, and Overwrite
are persisted as `False`. SimpleDragon prepares the Address/Vintage-selected packaged
weather handle without exposing its cache path; InvisibleDragon resolves its
managed EnergyPlus runtime and system-temp work directory only after Run
receives an explicit False-to-True edge.
Example 02 is the complete standalone InvisibleDragon authoring-to-IDF path;
example 14 is the complete simulation path shared by the two products. Example
14 derives Seoul weather from the Model Address/Vintage: wait until
`Preparation success` is `True` and `Verified packaged weather` contains a typed
weather value, then create a fresh False-to-True edge on Run. Its intentionally
simple electric-radiator HVAC keeps this full-process execution example stable;
use example 12 for the broader HVAC/ERV/PV composition demonstration. No EnergyPlus,
IDD, EPW, runtime-root, or temporary-directory path belongs on this canonical
canvas.

`13-simpledragon-results-and-plots.gh` keeps both paths relative to the saved
Grasshopper document:

```text
..\fixtures\simple-dragon\grr\ASHRAE 140 modified.grr
..\temp\example-preview\simpledragon-csv
```

SimpleDragon resolves relative GRM, GRR, and CSV paths from the folder that
contains the saved `.gh` file, regardless of Rhino's working directory. In an
unsaved definition, read-only GRM/GRR inputs use the current working directory,
while GRM/GRR/CSV output paths use the system temp directory. `Export CSV` is
held at `False`; its directory and file-content outputs are previews only, so
this example does not create the preview directory or write CSV files.

The canonical SimpleDragon Prepare and InvisibleDragon Run components expose no
EnergyPlus, IDD, EPW, or temp path;
implementation-owned artifacts remain in verified per-user caches or the system
temporary directory. User-selected CSV export destinations remain visible.

## Relink the two-zone definition to live Rhino objects

`12-simpledragon-two-zone-to-idf.gh` contains internalized copies of the exact
geometry in `30-two-zone-office.3dm`, so it runs immediately and remains
portable. To use live document references instead:

1. Open `30-two-zone-office.3dm` in Rhino.
2. Right-click `ZONE_01_WEST` and `ZONE_02_EAST` in Grasshopper and set each
   parameter to its same-named Rhino Brep.
3. Right-click `WINDOW_ZONE_01_SOUTH` and `WINDOW_ZONE_02_SOUTH` and set each
   parameter to its same-named Rhino curve.
4. Keep each curve wired to the SimpleDragon Opening in the same local Zone
   cluster. The owning Zone and host face are inferred from that connection and
   geometry; there are no zone-index or face-index panels.

The Rhino objects are on `DRAGON_ZONES` and `DRAGON_OPENINGS`. Their attributes
also carry `DragonRole` and, for windows, `ZoneName` user strings. The stepped
three-zone model follows the same naming and layer convention and can replace
the inputs for geometry studies.

## Automated generation and verification

Validate every tracked definition and model in both supported Rhino hosts:

```powershell
.\dev.cmd examples
```

Regenerate all canonical binaries with Rhino 7, then validate them in Rhino 7
and Rhino 8:

```powershell
.\dev.cmd examples -Generate
```

The gate runs its Rhino hosts from a disposable system-temp directory outside
the repository. It checks component identities, the exact persisted wire set
and total, typed outputs, selected Boolean/numeric results, outward envelope
winding after save/reopen, runtime errors, Grasshopper round trips, and the
document-relative GRR fixture path. For `.3dm` files it also checks metre units,
layer and object names, solid Breps, exact bounds, required zone adjacencies,
closed planar windows, and equality between the model geometry and the
internalized two-zone Grasshopper inputs. Candidates, logs, summaries, and
round-trip copies remain below `temp/example-definitions/`.

When the verified distribution payloads and EnergyPlus runtime are available,
the gate temporarily enables example 14 in memory and verifies typed packaged
weather preparation, its direct-Zone electric-radiator model, managed Run,
Result, GRR, CSV, cache, and cancellation in both hosts. The saved trigger
values remain `False`. Use
`-SkipEnergyPlusWorkflow` to test the explicit disabled state or
`-EnergyPlusRoot` to select a runtime. `-WeatherPath` remains an explicit test
override for workflows that require one. An unavailable prerequisite is
reported as `Not Run`, not as a successful simulation.

## Further workflow recipes

- Example 02 uses `Compile InvisibleDragon`, whose managed IDD and embedded
  EnergyPlus 24.2 execution mapping require no path input. Use example 14 to see
  SimpleDragon select and verify weather from Model Address/Vintage, then pass
  typed IDF and Weather directly to the managed InvisibleDragon runner.
- Use Read GRM with `fixtures\simple-dragon\grm\ASHRAE 140 modified.grm`, then
  connect the typed model directly to Prepare to inspect or simulate an existing model.
- Wrap each model and its optional stable ID in a SimpleDragon Batch Case, feed
  the Cases list into Managed Run SimpleDragon Batch, set a parallel limit, and
  use only the Run/Cancel triggers. Runtime, weather, case temp, and result
  storage paths are managed internally.

See [the workflow guide](../docs/grasshopper-workflow.md) for units, optional
inputs, triggers, and persistence rules.

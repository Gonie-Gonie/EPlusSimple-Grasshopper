# Grasshopper and Rhino examples

These examples are executable project files, not screenshots or pseudocode.
The `.gh` files contain public Dragon components, persisted wires, inputs, and
preview panels. The `.3dm` files use metres and contain named planar single-face
Surface Breps and window curves that can be referenced directly from Grasshopper.

| File | Coverage |
| --- | --- |
| `00-invisibledragon-material-construction.gh` | Minimal InvisibleDragon Material -> Construction Layer -> Construction -> U-value graph |
| `01-invisibledragon-envelope-profile.gh` | Three-layer envelope, no-mass construction, constant annual profile, and typed output previews |
| `02-invisibledragon-single-zone-hvac-idf.gh` | Window→Surface→Zone and HVAC/ERV→Zone direct ownership, PV, energy model, and path-free EnergyPlus 24.2 IDF compile/validation |
| `10-simpledragon-material-construction.gh` | Minimal SimpleDragon Material -> Construction Layer -> Surface Construction graph |
| `11-simpledragon-envelope-hvac.gh` | Three-layer envelope, fenestration, packaged usage profile, three compatible source/supply families, ERV, and PV |
| `12-simpledragon-two-zone-to-idf.gh` | Complex two-Zone composition/IDF authoring: Fenestration Construction -> Opening -> local Surface, six Surfaces -> each Zone, west heat-pump/AHU, east boiler/radiator, dedicated ERVs, PV, then path-free IDF and packaged Weather preparation |
| `13-simpledragon-results-and-plots.gh` | Real GRR read, annual summary, monthly DataTree, line plot, bar plot, and non-writing CSV preview |
| `14-simpledragon-two-zone-run-results-csv.gh` | Stable end-to-end flow: explicit Surface ownership, dedicated electric radiators and ERVs connect to their own Zones, typed IDF and Weather feed managed InvisibleDragon Run, and a typed Batch Case feeds managed batch without parallel model/ID lists |
| `30-two-zone-office.3dm` | Twelve named planar Surface Breps forming two adjacent office Zones, plus two named south-window curves |
| `31-three-zone-stepped-office.3dm` | Eighteen named planar Surface Breps forming two adjacent ground-floor Zones and an adjacent upper Zone, plus three named windows |

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
Surface and opening geometry in `30-two-zone-office.3dm`, so it runs immediately
and remains portable. To use live document references instead:

1. Open `30-two-zone-office.3dm` in Rhino.
2. For each Grasshopper Brep parameter ending in `_FLOOR`, `_CEILING`,
   `_SOUTH`, `_NORTH`, `_WEST`, or `_EAST`, set it to the same-named Rhino
   single-face Brep. Each parameter feeds exactly one `SD Surface`; no face list
   or positional selection is involved.
3. Set `WINDOW_ZONE_01_SOUTH` and `WINDOW_ZONE_02_SOUTH` to their same-named
   Rhino curves.
4. Keep each curve wired through `SD Opening` to the matching `_SOUTH`
   `SD Surface`, and keep the six completed Surfaces wired to their local Zone.
   Construction and Boundary Intent belong to Surface; Height, Profile, HVAC,
   and ERV belong to Zone. There are no zone-index or face-index panels.

The Rhino objects are on `DRAGON_SURFACES` and `DRAGON_OPENINGS`. Surface
attributes carry `DragonRole=ThermalSurface`, `ZoneName`, `SurfaceType`, and
`BoundaryIntent`; window attributes also carry their owning `SurfaceName`.
The stepped three-Zone model follows the same naming and layer convention and
can replace or extend the inputs for geometry studies.

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
and total, typed outputs, selected Boolean/numeric results, outward Surface
winding after save/reopen, runtime errors, Grasshopper round trips, and the
document-relative GRR fixture path. For `.3dm` files it also checks metre units,
layer, object names and ownership attributes, single-face planar Breps, exact
bounds and normals, required Zone adjacencies, closed planar windows, and
equality between model geometry and the internalized two-Zone Grasshopper
inputs. Candidates, logs, summaries, and round-trip copies remain below
`temp/example-definitions/`.

When the verified distribution payloads and EnergyPlus runtime are available,
the gate temporarily enables example 14 in memory and verifies typed packaged
weather preparation, its Surface-to-Zone electric-radiator model, managed Run,
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

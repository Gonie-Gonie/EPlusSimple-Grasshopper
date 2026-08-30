# Grasshopper and Rhino examples

These examples are executable project files, not screenshots or pseudocode.
The `.gh` files contain public Dragon components, persisted wires, inputs, and
preview panels. The `.3dm` files use metres and contain named planar single-face
Surface Breps and window curves that can be referenced directly from Grasshopper.

| File | Coverage |
| --- | --- |
| `00-invisibledragon-material-construction.gh` | Minimal InvisibleDragon Material -> Construction Layer -> Construction -> U-value graph |
| `01-invisibledragon-envelope-profile.gh` | Three-layer envelope, no-mass construction, constant annual profile, and typed output previews |
| `02-invisibledragon-single-zone-hvac-idf.gh` | Window -> opening-bearing Wall, list-authored plain Walls, explicit Floor/Ceiling/Wall -> Zone and HVAC/ERV -> Zone ownership, PV, `ID Model -> Compile -> Run`, and the deliberate `EPW File -> ID Weather -> Run` standalone boundary |
| `10-simpledragon-material-construction.gh` | Minimal SimpleDragon Material -> Construction Layer -> Surface Construction graph |
| `11-simpledragon-envelope-hvac.gh` | Three-layer envelope, fenestration, packaged usage profile, three compatible source/supply families, ERV, and PV |
| `12-simpledragon-two-zone-model.gh` | Complex two-Zone model authoring: explicit Floor/Ceiling/Wall components, opening-free Walls authored as a list, each opening-bearing Wall kept separate, one owned Surface branch per Zone, west heat-pump/AHU, east boiler/radiator, dedicated ERVs, PV, then one complete GRM with JSON, provenance, and area previews |
| `13-simpledragon-results-and-plots.gh` | Real GRR read, annual summary, monthly DataTree, line plot, bar plot, and non-writing CSV preview |
| `14-simpledragon-two-zone-run-results-csv.gh` | Stable end-to-end flow: explicit Floor/Ceiling/Wall ownership with plain-wall lists and separate opening hosts, dedicated electric radiators and ERVs connect to their own Zones, the complete model feeds `Run SimpleDragon` directly, and GRR feeds a zero-configuration monthly graph, summaries, and CSV; a typed Batch Case feeds managed batch with its identity derived internally |
| `30-two-zone-office.3dm` | Twelve named planar Surface Breps forming two adjacent office Zones, plus two named south-window curves |
| `31-three-zone-stepped-office.3dm` | Eighteen named planar Surface Breps forming two adjacent ground-floor Zones and an adjacent upper Zone, plus three named windows |

## Run locally

Install the current Dragon build, close and reopen Rhino, then open any `.gh`
file in Grasshopper:

```powershell
.\dev.cmd install
```

Opening an example manually is safe: Run, Cancel, Force, Export, and Overwrite
are persisted as `False`. `Run SimpleDragon` selects and verifies the
Address/Vintage-based packaged weather, converts the GRM, resolves the managed
EnergyPlus runtime, and uses a system-temp work directory only after Run
receives an explicit False-to-True edge.
Example 02 is the complete standalone InvisibleDragon authoring-to-run graph.
Its EPW File parameter intentionally contains no data, so `ID Weather` performs
no path access and the definition remains a safe preview until a user chooses
an EPW and creates a fresh Run edge. The Run, Cancel, and Force controls are
saved False.
Example 14 is the complete SimpleDragon simulation path. It derives Seoul
weather from the Model Address/Vintage internally: create a fresh False-to-True
edge on Run and follow the State, Success, Diagnostics, and monthly line-graph
outputs. The Run GRR is the graph's only connected input; its defaults draw
monthly SiteUses per area, grouped by fuel, on a 12 x 6 World XY frame. Its intentionally
simple electric-radiator HVAC keeps this full-process execution example stable;
use example 12 for the broader HVAC/ERV/PV composition demonstration. No EnergyPlus,
IDD, EPW, runtime-root, or temporary-directory path belongs on this canonical
SimpleDragon canvas.

Neither Dragon authoring graph asks for entity or relationship IDs. The modules
derive them deterministically from the authored content, while typed wires carry
Opening, Floor/Ceiling/Wall Surface, Zone, HVAC, ERV, PV, and model relationships. Example 14 also
derives its CSV and batch case identities from the connected GRM.
Component labels such as `ID Model` use `ID` as the InvisibleDragon prefix, not
as an identifier port.

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

The canonical `Run SimpleDragon` component exposes no InvisibleDragon model,
IDF, EnergyPlus-result, Weather, EnergyPlus, IDD, EPW, or temp-path port;
implementation-owned artifacts remain in verified per-user caches or the system
temporary directory. User-selected CSV export destinations remain visible.

## Relink the two-zone definition to live Rhino objects

`12-simpledragon-two-zone-model.gh` contains internalized copies of the exact
Surface and opening geometry in `30-two-zone-office.3dm`, so it runs immediately
and remains portable. To use live document references instead:

1. Open `30-two-zone-office.3dm` in Rhino.
2. Relink each `_FLOOR`, `_CEILING`, and opening-bearing `_SOUTH` Brep
   parameter to the same-named Rhino single-face Brep.
3. For each `plain walls (list)` Brep parameter, use **Set Multiple Breps** and
   select that Zone's `_NORTH`, `_WEST`, and `_EAST` objects. The item-access
   Face input on `SD Wall` vectorizes this list and preserves its branch path.
4. Set `WINDOW_ZONE_01_SOUTH` and `WINDOW_ZONE_02_SOUTH` to their same-named
   Rhino curves.
5. Keep each curve wired through `SD Opening` to the separate matching `_SOUTH`
   `SD Wall`. Combine the completed `SD Floor`, `SD Ceiling`, opening-free `SD
   Wall` list, and opening-bearing `SD Wall` into that Zone's Surfaces branch.
   `SD Zone` consumes each branch as one owned Surface list. Construction and
   the named Boundary Condition choice belong to Floor/Ceiling/Wall; their Type
   is fixed by the component, not an integer input. Height, Profile, HVAC, and
   ERV belong to Zone. There are no zone-index or face-index panels.

Keep an opening-bearing Wall outside the plain-wall list unless you deliberately
build matching tree paths. The Openings input is branch-local; mixing the host
with unrelated faces can broadcast the same Opening list to those faces.

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
inputs. Candidates, logs, summaries, and round-trip copies remain below the
short-path run directory `temp/e/<token>`.

For example 02, that topology check includes `ID Weather`, `Run
InvisibleDragon`, and every compile/weather/run wire. Its deliberately data-empty
EPW File parameter and False triggers remain no-op through solve, save, reopen,
and round trip; the automated gate does not invent a standalone weather path.

When the verified distribution payloads and EnergyPlus runtime are available,
the gate temporarily enables example 14 in memory and verifies internal
packaged-weather selection, its Floor/Ceiling/Wall-to-Zone electric-radiator model, direct
SimpleDragon Run-to-GRR execution, the default monthly graph, CSV, cache, and cancellation in both hosts. The saved trigger
values remain `False`. Use
`-SkipEnergyPlusWorkflow` to test the explicit disabled state or
`-EnergyPlusRoot` to select a runtime. The gate supplies that root, its IDD, and
an optional `-WeatherPath` EPW to `Run SimpleDragon` only through internal
automation environment values; none are canvas inputs. Without gate overrides,
the component uses its managed LocalAppData runtime bootstrap and address-selected
packaged weather. An unavailable prerequisite is reported as `Not Run`, not as a
successful simulation.

## Further workflow recipes

- Example 02 uses `Compile InvisibleDragon`, whose managed IDD and embedded
  EnergyPlus 24.2 execution mapping require no path input. To run it deliberately,
  select an EPW in the empty EPW File parameter, pass it through `ID Weather`,
  and create a fresh False-to-True edge on `Run InvisibleDragon`. Use example 14 to see
  SimpleDragon select and verify weather from Model Address/Vintage and perform
  the complete managed simulation without exposing its internal IDF or Weather.
- Use Read GRM with `fixtures\simple-dragon\grm\ASHRAE 140 modified.grm`, then
  connect the typed model directly to `Run SimpleDragon` to simulate an existing model.
- Wrap each model in a SimpleDragon Batch Case; its deterministic case identity
  is derived from the GRM. Feed the Cases list or tree into Managed Run
  SimpleDragon Batch, set a parallel limit, and use only the Run/Cancel
  triggers. The complete tree is one batch, and Case IDs/Statuses preserve its
  paths. Runtime, weather, case temp, and result storage paths are managed
  internally.

See [the workflow guide](../docs/grasshopper-workflow.md) for units, optional
inputs, triggers, and persistence rules.

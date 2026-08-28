# Grasshopper and Rhino examples

These examples are executable project files, not screenshots or pseudocode.
The `.gh` files contain public Dragon components, persisted wires, inputs, and
preview panels. The `.3dm` files use metres and contain named closed Breps and
window curves that can be referenced directly from Grasshopper.

| File | Coverage |
| --- | --- |
| `00-invisibledragon-material-construction.gh` | Minimal InvisibleDragon material, thickness, construction, and U-value graph |
| `01-invisibledragon-envelope-profile.gh` | Three-layer envelope, no-mass construction, constant annual profile, and typed output previews |
| `02-invisibledragon-single-zone-hvac-idf.gh` | Six planar surfaces, closed zone, HVAC/ERV/PV, energy model, IDF compile/validation, runtime preparation, EnergyPlus run, and result summary |
| `10-simpledragon-material-construction.gh` | Minimal SimpleDragon material and surface-construction graph |
| `11-simpledragon-envelope-hvac.gh` | Three-layer envelope, fenestration, packaged usage profile, three compatible source/supply families, ERV, and PV |
| `12-simpledragon-two-zone-to-idf.gh` | Two Brep zones and windows through extraction, immutable HVAC/ERV assignment, GRM assembly, and InvisibleDragon IDF conversion |
| `13-simpledragon-results-and-plots.gh` | Real GRR read, annual summary, monthly DataTree, line plot, bar plot, and non-writing CSV preview |
| `14-simpledragon-two-zone-run-results-csv.gh` | Two Brep zones through GRM/IDF conversion, a gated EnergyPlus run, result summary, GRR build/summary, CSV export, cache/cancellation controls, and a separate batch branch |
| `30-two-zone-office.3dm` | Two adjacent named office-zone solids and two named south-window curves |
| `31-three-zone-stepped-office.3dm` | Two adjacent ground-floor zones plus an adjacent upper zone and three named windows |

## Run locally

Install the current Dragon build, close and reopen Rhino, then open any `.gh`
file in Grasshopper:

```powershell
.\dev.cmd install
```

Opening an example manually is safe: all action triggers, including Run,
Cancel, Repair, Force, Export, Overwrite, Batch Run, and Batch Cancel, are
persisted as `False`. Example 14 keeps `Prepare packaged runtime` at `True` as a
run policy; it performs no work until Run receives an explicit False-to-True
edge. The IDF examples still compile deterministic input text; an unresolved
EnergyPlus IDD is reported as a warning and can be resolved by running
`.\dev.cmd setup -InstallEnergyPlus`.
Examples 02 and 14 are the complete InvisibleDragon and SimpleDragon execution
paths. Example 02 requires an absolute EPW supplied by the user: first toggle
`Prepare` and wait for `Ready`, then toggle `Run`; its verified Runtime Root is
already connected to Run EnergyPlus. Example 14 derives the Seoul EPW from the
model address and SimpleDragon's packaged KoreanTMY archive. Wait until the
`Address-selected packaged EPW` panel contains the absolute cached path, then
create a fresh False-to-True edge on only Run or Batch Run.

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

`Run EnergyPlus`, `Prepare EnergyPlus Runtime`, `Run SimpleDragon Batch`, and the
IDD inputs likewise resolve relative paths from the saved `.gh` file. For an
unsaved definition, relative run, runtime-preparation, batch, GRM/GRR, and CSV
output paths use the per-user system temp directory as their base rather than
Rhino's installation directory. Read-only EPW and IDD inputs should be absolute
until the definition has been saved.

## Relink the two-zone definition to live Rhino objects

`12-simpledragon-two-zone-to-idf.gh` contains internalized copies of the exact
geometry in `30-two-zone-office.3dm`, so it runs immediately and remains
portable. To use live document references instead:

1. Open `30-two-zone-office.3dm` in Rhino.
2. In Grasshopper, right-click `Two closed office zones`, choose **Set Multiple
   Breps**, and select `ZONE_01_WEST` followed by `ZONE_02_EAST`.
3. Right-click `South facade windows`, choose **Set Multiple Curves**, and
   select `WINDOW_ZONE_01_SOUTH` followed by `WINDOW_ZONE_02_SOUTH`.
4. Keep the persisted zone-index and face-index inputs in their original order.

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
the gate temporarily enables example 14 in memory and verifies the address to
packaged-EPW path, Run, Result, GRR, CSV, cache, cancellation, and batch behavior
in both hosts. The saved trigger values remain `False`. Use
`-SkipEnergyPlusWorkflow` to test the explicit disabled state or
`-EnergyPlusRoot` to select a runtime. `-WeatherPath` remains an explicit test
override for workflows that require one. An unavailable prerequisite is
reported as `Not Run`, not as a successful simulation.

## Further workflow recipes

- Use the persisted Prepare → Run → Result chain in example 02 for a direct
  InvisibleDragon simulation with an explicit EPW, or use example 14 to see
  SimpleDragon resolve the EPW automatically from its model address.
- Use Read GRM with `fixtures\simple-dragon\grm\ASHRAE 140 modified.grm`, then
  Convert GRM to inspect compatibility diagnostics for an existing model.
- Feed ordered GRM cases and stable IDs into Batch Research for parallel studies;
  keep its case temp root and result root under `temp/` while developing.

See [the workflow guide](../docs/grasshopper-workflow.md) for units, optional
inputs, triggers, and persistence rules.

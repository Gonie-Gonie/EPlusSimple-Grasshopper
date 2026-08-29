# Grasshopper workflow

## Shared conventions

- Numeric geometry and HVAC inputs use the SI units shown in each parameter description.
- Leave an optional numeric input disconnected to preserve `null` or autosize semantics. Zero is a real value.
- IDs are deterministic when the ID input is empty. Supply one only when another system must refer to that object.
- Red runtime messages stop that component's output. Warnings describe a usable result that needs review.
- Run, Write, Export, and Batch actions require a new False-to-True Boolean edge. A saved True value never starts work when a document opens.

## Canonical SimpleDragon graph

Build each object where its ownership is visible in the wires:

```text
Opening Curve -> SD Opening -------------------+
                                                |
Zone Brep + Profile + HVAC + ERV ------------> SD Zone --+
Opening Curve -> SD Opening -------------------+          |
                                                           +-> SD Model (Address)
Zone Brep + Profile + HVAC + ERV ------------> SD Zone --+          |
                                                                      v
                                                                  SD to IDF
                                                               IDF + Weather
                                                                      |
                                                                      v
                                                             Run InvisibleDragon
                                                                      |
                                                                      v
                                                        Result / GRR / Plot / CSV
```

`SD Opening` has no Zone Index or Face Index. Connect it only to the `SD Zone` that owns it; the host face is inferred from coplanarity and containment. Connect supply systems and ERVs directly to that same Zone. A raw ERV means one unit, while `SD ERV Count` is available only when a count greater than one is needed.

`SD Model` resolves every Zone together, so shared Brep faces still become inter-zone adjacency. It also derives the material, construction, supply, source, and ventilation catalogs from the connected objects. Those catalogs do not need parallel wires into a later assembly component.

The model Address and Vintage select the weather record. `SD to IDF` preserves the SimpleDragon conversion semantics while emitting the valid EnergyPlus 24.2 HVAC field layout, then asynchronously prepares the matching packaged EPW. Its `Weather` output is a verified typed handle, not a path panel.

## InvisibleDragon execution boundary

For low-level authoring, connect `Energy Model -> Compile InvisibleDragon`.
The compiler has only a typed Model input and resolves the managed EnergyPlus
24.2 IDD or its embedded execution mapping internally. Its typed IDF can then
join the verified SimpleDragon Weather handle at the execution boundary.

`Run InvisibleDragon` accepts only:

- a typed IDF;
- the typed `Weather` produced by SimpleDragon;
- Run, Cancel, Force Rerun, and Timeout controls.

EnergyPlus, Energy+.idd, the runtime cache, EPW extraction, working directories, and cleanup are module-owned. The runtime is verified and prepared below the current user's LocalAppData; simulations use the operating-system temp directory. Nothing writes below the Rhino installation directory, and no administrator permission is required.

The first observed Run or Cancel value is a baseline. Toggle False, allow one solution, then toggle True to request an action. Identical IDF/weather/timeout inputs reuse the last result unless Force Rerun is enabled.

InvisibleDragon still contains the low-level model and HVAC types used by the port. Earlier path-oriented Compile, Validate, Prepare Runtime, and Run components retain their GUIDs and ports so historical `.gh` files load, but they are hidden from the normal palette.

## Results, CSV, and batch studies

Build GRR applies the SimpleDragon result aggregation after an InvisibleDragon result is available. Summary, DataTree, line-plot, and bar-plot components expose the same ordered month, fuel, and end-use data without a file round trip.

GRM/GRR readers, writers, and CSV export intentionally expose artifact destinations because those are user-owned results, not simulation setup. Relative output paths use the saved Grasshopper document folder; unsaved definitions fall back to the per-user temp directory.

`Managed Run SimpleDragon Batch` accepts models, optional case IDs, a parallel limit, and explicit Run/Cancel controls. It selects packaged weather from each model's Address/Vintage and manages EnergyPlus/runtime/temp paths internally. Combined CSV and reproducibility-manifest paths are outputs only.

## Runnable examples

The tracked definitions under `examples/` progress from materials and profiles to linked Rhino geometry and a complete two-zone simulation. The principal authoring examples are:

- `12-simpledragon-two-zone-to-idf.gh`: the complex authoring demonstration,
  with two independently owned Zones and Openings, heat-pump/AHU and
  boiler/radiator systems, ERV, and PV resolved into one GRM/IDF;
- `14-simpledragon-two-zone-run-results-csv.gh`: the stable end-to-end gate,
  with an electric radiator connected directly to each Zone, Address/Vintage-selected
  Weather, InvisibleDragon execution, GRR, summaries, and CSV.

`02-invisibledragon-single-zone-hvac-idf.gh` shows the standalone low-level
authoring path through the path-free `Compile InvisibleDragon` component.

The geometry is also available as `30-two-zone-office.3dm` and `31-three-zone-stepped-office.3dm`. All action triggers are saved False.

Run `./dev.cmd examples` to solve and round-trip every `.gh` and `.3dm` example in Rhino 7 and Rhino 8. Use `-SkipEnergyPlusWorkflow` to verify the explicit Not Run path. Run `./dev.cmd examples -Generate` only when deliberately refreshing the Rhino 7-authored canonical binaries.

## Saving and reopening

Dragon Goo stores deterministic, schema-versioned snapshots in the Grasshopper document. Opening and Zone definitions include their Rhino geometry, and model/HVAC/weather/result values survive save and reopen. Existing v1 snapshots and legacy component GUIDs remain readable.

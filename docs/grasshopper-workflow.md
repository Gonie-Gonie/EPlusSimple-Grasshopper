# Grasshopper workflow

## Shared conventions

- Numeric geometry and HVAC inputs use the SI units shown in each parameter description.
- Leave an optional numeric input disconnected to preserve `null` or autosize semantics. Zero is a real value.
- Entity and relationship IDs are generated deterministically inside both Dragon modules; they are not authoring inputs. Express ownership and references by connecting the typed objects instead of passing text IDs.
- Red runtime messages stop that component's output. Warnings describe a usable result that needs review.
- Run, Write, Export, and Batch actions require a new False-to-True Boolean edge. A saved True value never starts work when a document opens.

## Canonical SimpleDragon graph

Build each object where its ownership is visible in the wires:

```text
Opening Curve + Fenestration Construction -> SD Opening -> West SD Surface <- Face / Type / Construction / Boundary
                                                              |
                                                              +-> West SD Zone <- Height / Profile / HVAC / ERV --+

Opening Curve + Fenestration Construction -> SD Opening -> East SD Surface <- Face / Type / Construction / Boundary
                                                              |
                                                              +-> East SD Zone <- Height / Profile / HVAC / ERV --+-> SD Model (Address)
                                                                                                                       |
                                                                                                                       v
                                                                                                              Run SimpleDragon
                                                                                                                       |
                                                                                                                       v
                                                                                                            GRR / Plot / CSV
```

`SD Opening` has no Zone Index or Face Index. Its Construction input is required
and belongs to the Opening. Connect that completed Opening only to its owning
`SD Surface`. Each Surface owns one planar single-face Brep, its Wall/Ceiling/Floor
type, opaque Construction, Boundary Intent, Openings, and optional cool-roof
reflectance. The opening curve must be coplanar with and contained by that face;
a trimmed inner loop needs a matching explicit Opening rather than guessed
metadata.

Connect the completed Surfaces of one closed thermal enclosure to `SD Zone`.
The Zone owns only those Surfaces plus its Name, Floor Number, positive Height,
Profile, lighting density, HVAC, and ERVs; it has no Brep, Construction,
Boundary, or Opening input. Connect each Zone-owned supply system and `SD ERV`
only to that Zone. Set the ERV's Count input when a Zone has multiple identical
units; no intermediate assignment component is involved.

`SD Model` resolves every Zone together. Coincident opposite Surfaces with
`Outdoors` intent in different Zones are promoted to reciprocal Zone boundaries
automatically. The Model also derives the material, construction,
supply, source, and ventilation catalogs from the connected objects, so those
catalogs need no parallel wires into a later assembly component.

Build each opaque construction from `Construction Layer` values. Each layer owns
its Material and Thickness, and the construction receives one ordered Layers
list; Materials and Thicknesses are never matched by list position.

The model Address and Vintage select the weather record. Connect the completed
GRM directly to `Run SimpleDragon`, then use its Run, Cancel, Force Rerun, and
Timeout controls. The component internally converts the GRM, generates the
EnergyPlus 24.2 IDF, verifies the matching packaged EPW, resolves the supported
runtime, executes EnergyPlus, and constructs the GRR. Its public outputs are
only GRR, State, Success, and Diagnostics.

The first observed Run or Cancel value is a baseline. Toggle False, allow one
solution, then toggle True to request an action. An identical
model/weather/timeout combination reuses the last GRR unless Force Rerun is
enabled. Opening or recomputing a saved document never prepares weather or
starts EnergyPlus by itself.

## Standalone InvisibleDragon boundary

Low-level authoring uses the same local-ownership rule:

```text
Curve + Glazing -> ID Window --------------------+
                                                    |
Curve + Construction + owned Openings -> ID Surface -> ID Zone <- Profile / HVAC / ERV
                                                        |
                                                        +-> ID Model <- PV
                                                                 |
                                                                 v
                                                        Compile InvisibleDragon -> IDF --+-> Run InvisibleDragon
                                                                                          ^
                                                           EPW File -> ID Weather --------+
```

Here `ID` is the InvisibleDragon component prefix, not an identifier input.

Connect each Window or Door to its owning Surface, each Surface and system to
its owning Zone, and only completed Zone definitions to the Model. Coincident,
opposite-facing Surfaces with `Outdoors` intent in different Zones are paired
automatically into reciprocal inter-zone boundaries. The Model derives HVAC
assignments and nested sources from the Zone wires, so there are no Zone
indices, adjacent-surface IDs, source catalogs, or assignment components on the
canvas. Material, geometry, profile, Zone, HVAC, ERV, and PV components also
generate their entity IDs internally; none exposes a relationship-ID input.

Connect `ID Model -> Compile InvisibleDragon -> Run InvisibleDragon`.
The compiler has only a typed Model input and resolves the managed EnergyPlus
24.2 IDD or its embedded execution mapping internally. This visible low-level
compile path is useful when the author deliberately works in InvisibleDragon;
SimpleDragon does not expose it as an intermediate stage.

For the second runner input, connect
`EPW File -> Verify InvisibleDragon Weather -> Run InvisibleDragon`.
`ID Weather` verifies the deliberately selected local EPW and emits a typed,
content-addressed Weather handle. An empty EPW File parameter is a safe no-op,
so a saved example can show the complete topology without reading a path.
InvisibleDragon does not select, download, or infer weather.

`Run InvisibleDragon` remains the low-level executor for this explicit workflow.
It is not inserted between `SD Model` and `Run SimpleDragon`, and none of its
ports is required on a SimpleDragon canvas.

EnergyPlus, Energy+.idd, runtime caches, working directories, and cleanup are
module-owned. SimpleDragon also owns its packaged weather cache. A standalone
InvisibleDragon EPW stays at the user-selected location and is verified before
execution; simulations use the operating-system temp directory. Nothing writes
below the Rhino installation directory, and no administrator permission is
required.

## Results, CSV, and batch studies

`Run SimpleDragon` emits the completed GRR directly. Summary, DataTree,
line-plot, and bar-plot components expose the same ordered month, fuel, and
end-use data without an InvisibleDragon-result handoff or file round trip.

For an immediate Rhino preview, connect GRR to `Monthly Lines` or `Monthly
Bars` and leave every other input alone. The defaults draw SiteUses per area,
grouped by fuel, on a 12 x 6 World XY frame; the bar variant defaults to grouped
bars. Before Run has produced a GRR, these result components simply wait without
raising a red error. Metric, grouping, plane, size, and stacking remain optional
controls for a customized graph.

GRM/GRR readers, writers, and CSV export intentionally expose artifact destinations because those are user-owned results, not simulation setup. Relative output paths use the saved Grasshopper document folder; unsaved definitions fall back to the per-user temp directory. When a GRM is connected, CSV export derives its case identity from that model rather than asking for a text ID.

Wrap each model in a `SimpleDragon Batch Case`, which derives a deterministic
case identity from the GRM, then
connect the Cases list to `Managed Run SimpleDragon Batch` with a parallel limit
and explicit Run/Cancel controls. It selects packaged weather from each model's
Address/Vintage and manages EnergyPlus/runtime/temp paths internally. Combined
CSV and reproducibility-manifest paths are outputs only.

## Runnable examples

The tracked definitions under `examples/` progress from materials and profiles to linked Rhino geometry and a complete two-zone simulation. The principal authoring examples are:

- `12-simpledragon-two-zone-model.gh`: the complex authoring demonstration,
  with named face Breps composed as Surface-owned Openings, constructions, and
  boundary intents, then two independently owned Zones, terminal systems, and ERVs;
  a heat-pump/AHU serves the west Zone, a boiler/radiator serves the east Zone,
  and PV is resolved with both into one complete GRM;
- `14-simpledragon-two-zone-run-results-csv.gh`: the stable end-to-end gate,
  with an electric radiator and ERV connected directly to each Zone,
  `SD Model -> Run SimpleDragon -> GRR`, a directly connected default monthly
  line graph, summaries, CSV, and a typed Batch Case feeding the managed batch
  runner.

`02-invisibledragon-single-zone-hvac-idf.gh` shows the complete standalone
low-level topology: authoring, path-free compile, explicit EPW verification,
and `Run InvisibleDragon`. Its EPW File parameter contains no data and all
action triggers are saved False, so choose an EPW and create a fresh Run edge
only when execution is intended.

The geometry is also available as `30-two-zone-office.3dm` and `31-three-zone-stepped-office.3dm`. All action triggers are saved False.

Run `./dev.cmd examples` to solve and round-trip every `.gh` and `.3dm` example in Rhino 7 and Rhino 8. Use `-SkipEnergyPlusWorkflow` to verify the explicit Not Run path. Run `./dev.cmd examples -Generate` only when deliberately refreshing the Rhino 7-authored canonical binaries.

## Saving and reopening

Dragon Goo stores deterministic, schema-versioned snapshots in the Grasshopper
document. Opening and Surface definitions include their geometry; Zone
snapshots include their owned Surface definitions and Zone-level values.
Model, HVAC, and result values also survive save and reopen.

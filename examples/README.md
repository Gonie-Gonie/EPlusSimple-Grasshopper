# Grasshopper example recipes

The initial example set is organized as reproducible recipes while the binary
`.gh` files are generated and real-host verified for the release candidate.
Use the component search names below; do not substitute similarly named legacy
modules.

## 01 — InvisibleDragon single zone

1. Create Opaque Material and Layered Construction values.
2. Draw six closed planar polylines and convert each with Surface From
   Polyline, assigning Wall, Floor, or Ceiling and the construction.
3. Assemble the surfaces with Zone.
4. Connect the zone to Energy Model and Compile IDF.
5. Prepare EnergyPlus, supply an EPW, and create a Run edge.

This recipe demonstrates vertex preservation and is the smallest direct Dragon
model.

## 02 — InvisibleDragon heat pump and AHU

1. Start from recipe 01.
2. Create Heat Pump, then connect it to Air Handling Unit.
3. Use Supply Group/assignment to connect the AHU to the zone.
4. Pass the explicit source and supply lists to Energy Model.
5. Compile, run, and inspect Result Summary and diagnostics.

## 03 — InvisibleDragon boiler and radiant floor

Connect Boiler → Radiant Floor → Supply Group → Zone. Leave an optional
capacity disconnected to retain autosize semantics; do not enter zero as a
stand-in for an omitted value.

## 11 — Read and convert an existing SimpleDragon GRM

Use Read GRM with
`fixtures\simple-dragon\grm\ASHRAE 140 modified.grm`, then Convert GRM. Inspect
the converted InvisibleDragon model, IDF text, EPW filename metadata, and every
diagnostic before running.

## 12 — SimpleDragon zone from Brep

Create a closed Rhino Brep, use Extract SimpleDragon Zones, assign a packaged
usage profile and constructions, then Assemble GRM. Compare the original Brep
with the converted-surface preview to see the area-and-azimuth abstraction.

## 13 — SimpleDragon multiple HVAC

Create one or more source systems, wire each compatible supply, assign supplies
and ERVs to immutable zone copies, add PV at model level, and pass the explicit
system collections to Assemble GRM. Convert to InvisibleDragon and review the
mapping diagnostics.

## 14 — Results, CSV, and plots

Run the converted IDF, build a GRR, then connect the same result to Summary,
Monthly DataTree, Line Plot, Bar Plot, and Export CSV. Use a new False-to-True
Export edge for each requested write.

## 15 — Batch geometry study

Provide an ordered GRM list and stable case IDs to Batch Research. Set a
conservative process limit, an isolated temp root, an output root, and a
user-supplied EPW. The combined CSV and reproducibility manifest retain input
order even when cases finish in parallel or partially fail.

See [the workflow guide](../docs/grasshopper-workflow.md) for units, optional
inputs, triggers, and persistence rules. Generated release examples must pass
Rhino 7 and Rhino 8 save/reopen before they are added as binary assets.

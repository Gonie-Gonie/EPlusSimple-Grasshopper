# Grasshopper workflow

## Shared conventions

- Numeric geometry and HVAC inputs use SI units shown in each parameter
  description. Capacity is W, flow is m³/s, area is m², and efficiencies/COPs
  are dimensionless.
- Leave an optional numeric input disconnected to preserve `null`/autosize
  semantics. Supplying zero is not the same as leaving it disconnected.
- IDs are deterministic when the ID input is empty. Supply an explicit stable
  ID when another file or system refers to that object.
- Red runtime messages stop that component's output. Warnings identify a valid
  result with a compatibility, geometry-loss, or authoring concern.
- Run, Prepare, Write, Export, and Batch actions require an explicit Boolean
  edge. Hold the input at False between attempts.

## InvisibleDragon

The normal left-to-right graph is:

```text
Material → Construction → Surface → Zone
                                  ↘
Source / Tower → Supply → Supply Group → Energy Model → Compile IDF
ERV / PV ───────────────────────────────↗                  ↓
Prepare EnergyPlus Runtime → Run EnergyPlus → Result → Summary
```

Create source and supply systems with the dedicated HVAC components. Hydronic
or air-side supplies take their compatible source on the input wire; this wire
is the system relationship rather than a separate node/loop editor. Use Supply
Group/assignment to associate one or more supplies with a zone and optional
availability schedules. The Energy Model component accepts explicit sources,
supplies, ERVs, and PV panels in addition to the zone graph so that unassigned
plant objects are never inferred silently.

Compile IDF produces a typed IDF value and deterministic text. Validation can
use the pinned EnergyPlus IDD. Run EnergyPlus accepts the typed IDF, a
user-supplied EPW, an optional runtime root, an isolated temp root, timeout and
cleanup choices. A repeated input is cached unless Force Rerun is enabled.

## SimpleDragon

Two entry paths are supported:

```text
Read GRM ───────────────────────────────────────────────┐
                                                       ↓
Rhino Brep → Extract Zones → Assign Supply / ERV → Assemble GRM
Source → Supply ────────────────────────────────────────↗
PV ─────────────────────────────────────────────────────↗
                                                       ↓
             Convert GRM → InvisibleDragon model + IDF + diagnostics
                                                       ↓
             Run EnergyPlus → Build GRR → Summary / DataTree / Plot / CSV
```

SimpleDragon sources comprise Heat Pump, Geothermal Heat Pump, Chiller,
Absorption Chiller, Boiler, and District Heating. Supplies comprise Packaged
Air Conditioner, Air Handling Unit, Fan Coil Unit, Radiator, Electric
Radiator, Radiant Floor, and Electric Radiant Floor. The source-to-supply wire
is validated; incompatible combinations return actionable diagnostics instead
of inventing a different system.

Assignments copy the immutable zone and either append or replace its current
systems. Assemble GRM accepts the explicit model-level source, supply, ERV, and
PV collections. Existing definitions that used only the earlier inputs retain
their input order and optional behavior.

Convert GRM is the authoritative SimpleDragon-to-InvisibleDragon path. Inspect
its diagnostic list and converted Breps before simulation. Build GRR applies
the SimpleDragon result aggregation after the EnergyPlus result is available.

## CSV and batch studies

CSV export writes deterministic invariant-culture tables and uses UTF-8 BOM
where spreadsheet interoperability requires it. Monthly DataTree, line-plot,
and bar-plot components expose the same ordered month/fuel/end-use data without
requiring a file round trip.

Batch Research accepts an ordered model list and stable case IDs. It limits
parallel EnergyPlus processes, supports cancellation and partial failure,
caches matching cases, and writes a combined CSV plus reproducibility manifest.
All temporary case directories remain below the configured temp root.

## Saving and reopening

Dragon Goo stores a deterministic schema-versioned snapshot in the Grasshopper
document. Model, HVAC, ERV, PV, IDF, result, and diagnostic values survive
save/reopen. Existing v1 zone/model snapshots remain readable. Component and
parameter GUIDs are release contracts and must not be changed after publication.

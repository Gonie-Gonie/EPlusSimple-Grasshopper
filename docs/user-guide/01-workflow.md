# Workflow

InvisibleDragon and SimpleDragon are two independently installable Grasshopper
products for building-energy authoring and EnergyPlus simulation. Both use
typed Grasshopper values and direct ownership wires: an opening belongs to a
surface, a surface belongs to a zone, and zone equipment belongs to that zone.
Users do not enter internal entity IDs, zone indices, face indices, or catalog
indices.

This chapter starts with installation and shared canvas rules, then gives a
complete workflow for each product.

## Install and open the components

### Installed-product requirements

An installed Dragon product requires:

- Windows x64.
- Rhino 7 or Rhino 8 with Grasshopper.
- A valid Rhino installation and license.

It does not require Python, a Python virtual environment, the .NET SDK, Visual
Studio, or a machine-wide EnergyPlus installation. Python and OODocs in the
repository are documentation-development tools; installed Grasshopper plugins
do not load them.

InvisibleDragon and SimpleDragon have separate package identities. Install
either one or both from the same version. For Yak packages, select the package
for the host generation: `rh7-win` for Rhino 7 and `rh8-win` for Rhino 8. Close
all Rhino processes before installing, replacing, or removing plugin files.

For a local source checkout, one command replaces any existing local Dragon
packages with a newly built candidate in every detected Rhino 7 and Rhino 8
host:

```text
.\dev.cmd install
```

After installation:

1. Start Rhino and open Grasshopper.
2. Confirm that the `InvisibleDragon` or `SimpleDragon` tab appears.
3. Place that product's `Version` component on a blank canvas.
4. If both products are installed, confirm that they report the same release
   version before combining them in one Rhino session.

SimpleDragon may be installed alone. Its package contains the shared compiled
libraries needed by its internal conversion and run pipeline, but it does not
add the InvisibleDragon Grasshopper tab. Installing the matching
InvisibleDragon package alongside it also makes the pinned EnergyPlus archive
available for an offline first SimpleDragon run.

## Choose a Dragon

| Goal | Use SimpleDragon | Use InvisibleDragon |
| --- | --- | --- |
| Early-stage or retrofit modelling with the GRM/GRR format | Yes | No |
| Select Korean weather from a building address and vintage | Yes | No |
| Run without placing IDF, EPW, IDD, or runtime paths on the canvas | Yes | Partly: only EPW remains explicit |
| Author the lower-level typed EnergyPlus model | No | Yes |
| Inspect deterministic IDF text before a run | No public conversion-preview component | Yes |
| Select a specific local EPW | Not in the canonical direct-run graph | Yes |
| Primary result | GRR | Structured EnergyPlus result |

Choose SimpleDragon when the intended graph is building description to GRR and
the address-driven Korean weather database is appropriate. Choose
InvisibleDragon when explicit EnergyPlus-facing construction, geometry, HVAC,
IDF, and local weather control matter.

The two products can coexist, but a normal SimpleDragon graph does not need an
InvisibleDragon component or wire.

## The shared ownership model

The shortest reliable graphs follow the ownership hierarchy from left to
right.

```text
opaque material -> layer -> opaque construction ----+
                                                     |
fenestration construction -> opening -> Floor/Ceiling/Wall
                                                     |
source -> terminal ----------------------------------+-> Zone -> Model
ERV -------------------------------------------------+
PV -------------------------------------------------------> Model
```

The important boundaries are:

- An opening owns its fenestration construction and geometry.
- A `Floor`, `Ceiling`, or `Wall` owns its face or boundary geometry, opaque
  construction, boundary condition, and openings.
- A `Zone` owns its surfaces, usage profile, HVAC terminals, and ERVs.
- A `Model` owns its zones, site settings, and photovoltaic panels.
- A plant or heat-pump source connects to the terminal that uses it. The
  completed terminal connects to the owning zone.

There is no generic public SimpleDragon Surface component. Choose `SD Floor`,
`SD Ceiling`, or `SD Wall`; each component fixes the physical surface type.
InvisibleDragon likewise provides explicit `Floor`, `Ceiling`, and `Wall`
components. Boundary conditions are named choices such as `Outdoors`, `Ground`,
and `Adiabatic`, not integer codes.

Internal IDs are generated deterministically from authored content and are
carried inside typed values. Do not duplicate a model wire merely to recreate
relationships already expressed by the local ownership chain.

## Geometry, units, lists, and trees

### Geometry and SI values

Dragon geometry adapters read the active Rhino document's unit system and
convert geometry to metres internally. A one-metre wall may therefore be drawn
as `1` in a metre document, `1000` in a millimetre document, or the equivalent
value in another supported Rhino unit system.

Numeric component inputs use the unit printed in their description. Thermal
and model quantities are generally SI: metres, square metres, watts,
watts per square metre, cubic metres per second, degrees Celsius, and
dimensionless efficiency fractions. Angles are degrees. Do not scale a numeric
input labelled metres merely because the Rhino document is in millimetres.
Plot `Width` and `Height`, unlike simulation quantities, are drawn in Rhino
model units.

### Valid surface and opening geometry

SimpleDragon surfaces accept one planar, single-face polygonal Brep per item.
InvisibleDragon surfaces accept a closed planar polygonal curve per item.
Openings use closed planar polygonal curves on their intended host surface.
Avoid curved edges, non-planar faces, multi-face polysurfaces, self-intersection,
and zero-area geometry.

Use outward-facing surface normals. In SimpleDragon, face orientation also
supports the floor/ceiling/wall interpretation: floor normals point down,
ceiling normals point up, and wall normals are horizontal. Opening curves must
lie inside their host and must not overlap another opening on that surface.
SimpleDragon Ground and Adiabatic surfaces cannot own openings.

For a shared boundary between two zones, author one coincident surface for each
zone. Leave both authored boundary conditions as `Outdoors` and use opposite
outward normals. The Model component pairs valid coincident faces and replaces
them with reciprocal zone boundaries. Supported pairings include wall-to-wall
and floor-to-ceiling. Do not manually assign the other zone or its index.

### Lists and data trees

The explicit surface components have item access for geometry, so a list or
tree of faces/curves vectorizes into a corresponding list or tree of typed
surfaces. The `Surfaces` input on a Zone consumes a list. A practical pattern is
one branch per zone:

```text
{0}: every completed surface owned by Zone 0 -> Zone 0
{1}: every completed surface owned by Zone 1 -> Zone 1
```

The same principle applies to openings and HVAC. Keep an opening-bearing wall
separate from a plain-wall list unless the opening tree exactly matches the
wall tree. An `Openings` list is branch-local; careless flattening or
broadcasting can place the same opening on unrelated surfaces.

Keep each zone's surfaces and equipment together until after the Zone
component. Combine the completed Zone values only at the Model component. This
reduces long crossing wires and preserves Grasshopper paths naturally.

### Disconnected inputs and zero

There is no project-wide rule that `0` means “unset.” Treat zero as a real value
unless the individual input description explicitly assigns another meaning.
For example, InvisibleDragon Zone defaults of `0` ACH infiltration, `0` W/m2
lighting, and `0` m3/s outdoor air are real zero loads, while a SimpleDragon
fenestration SHGC of `0` intentionally creates an opaque door construction.

SimpleDragon capacity inputs described as optional should be left disconnected
for autosize/unset. Supplying `0` to those positive optional capacities is not
equivalent. Some InvisibleDragon capacity inputs explicitly say that `0` means
autosize; use zero only on those documented ports. The complete In/Out
reference in this guide records the rule for every input.

## Complete SimpleDragon workflow

The canonical SimpleDragon execution boundary is deliberately short:

```text
SD Floor / SD Ceiling / SD Wall -> SD Zone -> SD Model -> Run SimpleDragon -> GRR
SD Opening --------------------------^                         |
SD HVAC / SD ERV --------------------+                         +-> GRR Summary
SD PV -----------------------------------------> SD Model      +-> Monthly Lines
                                                               +-> Monthly Bars
                                                               +-> Export CSV
```

No `toIdf`, InvisibleDragon model, IDF, Weather, EnergyPlus result, executable,
IDD, EPW, runtime-root, or temporary-directory input belongs between `SD Model`
and `Run SimpleDragon`.

### 1. Choose a usage profile

Place `SD Profile` (`Lookup SimpleDragon Usage Profile`). With an empty `Name`,
the component lists all packaged profile names. Feed one exact name back to the
component and connect the resulting Profile to the Zone. A partial or
case-adjusted name is not a substitute for the packaged name.

### 2. Create opaque constructions when needed

For a custom opaque assembly:

1. Create each `SD Material` with conductivity, density, and specific heat.
2. Connect each material to `SD Layer` and set its thickness in metres.
3. Connect the ordered layer list to `SD Construction`.
4. Connect that construction only to the `SD Floor`, `SD Ceiling`, or `SD Wall`
   that owns it.

An opaque Construction input on a SimpleDragon surface is optional. Leaving it
disconnected records an unknown construction. During the direct-run conversion,
SimpleDragon resolves that construction from model Vintage, address-derived
climate, surface type, boundary condition, radiant-floor status, and the
multifamily flag. Connect an explicit construction when a known assembly should
override that regulated default.

### 3. Create openings and their constructions

Create `SD Fenestration`, then connect it to `SD Opening` together with the
opening boundary curve. The opening type is `Window`, `GlassDoor`, or `Door`.
Windows and glass doors require a transparent construction; an opaque Door uses
SHGC `0`. `None`, `Shade`, and `Venetian` are the available blind choices, and
an opaque Door cannot have a blind.

Connect the completed opening to the `Openings` input of its one owning surface.
Do not also connect its fenestration construction to the Zone or Model. A
construction carried by the opening is already complete ownership information.

### 4. Create explicit surfaces

Use one of:

- `SD Floor`: default boundary `Ground`.
- `SD Ceiling`: default boundary `Outdoors`; an optional cool-roof reflectance
  in `(0, 1]` is valid only for an Outdoors ceiling.
- `SD Wall`: default boundary `Outdoors`.

Connect one single-face planar Brep, the optional opaque construction, a named
boundary-condition choice, and only the openings owned by that surface. Lists
of plain surfaces can pass through one component. Keep each opening host on a
path that receives only its own openings.

### 5. Add HVAC, ERV, and PV values by ownership

Build source-and-terminal pairs according to the HVAC patterns later in this
chapter. Connect completed terminals directly to `SD Zone` `HVAC`; connect
`SD ERV` directly to that Zone's `ERVs`. `SD ERV` `Count` defaults to one and,
when supplied, must be positive. Connect `SD PV` to `SD Model`, not to a Zone.

### 6. Create zones

Connect the surface list, one profile, and any HVAC/ERV lists to `SD Zone`.
Useful defaults are Floor Number `0`, Height `3` m, and Lighting Power Density
`10` W/m2. Change them deliberately for the building being modelled. One Zone
component should receive only the surfaces and equipment it owns.

### 7. Create the GRM model

Connect all completed zones to `SD Model`. Its principal inputs and defaults
are:

- Name: `SimpleDragon Model`.
- North Axis: `0` degrees, clockwise.
- Address: 서울특별시 종로구.
- Vintage: `2020-01-01` in strict `yyyy-MM-dd` form.
- Multifamily Housing: `False`.
- Photovoltaic Panels: optional model-level list.

Address is not merely a label. It selects climate metadata and a packaged EPW.
Whitespace is normalized, but the address must begin with an exact supported
Korean administrative-area prefix. A longer full street address is acceptable
when its leading prefix is supported. The Vintage must use the exact date
format and fall within the effective date coverage of the climate data. Check
the `SD Model` Diagnostics output before running; an unsupported prefix or
uncovered vintage is a model error.

`SD Model` resolves zone adjacency, nested constructions, openings, HVAC, ERV,
PV, and generated relationships into one GRM. Its geometry-map outputs can be
passed to `Export CSV` when geometry provenance is useful downstream.

### 8. Run without setup paths

Connect the GRM output directly to `Run SimpleDragon`. The runner internally:

1. Resolves Address and Vintage to the packaged Korean TMY record.
2. Verifies and prepares only the selected EPW.
3. Resolves unknown opaque constructions.
4. Converts the GRM to the execution model and IDF.
5. Resolves or prepares the pinned EnergyPlus runtime.
6. Runs EnergyPlus away from Rhino's UI thread.
7. Parses the result and returns a GRR.

Connect a momentary Grasshopper Button to `Run` and another to `Cancel`; do not
use Toggles for these action inputs. Let the definition solve once while the
Buttons are unpressed, then press Run to launch exactly one run. Press Cancel
during an active run when needed. Each Button returns to its resting False value
after the pulse, so opening or recomputing a saved document does not launch work.

`Force Rerun` is an option sampled when the Run Button is pressed; it is not
itself a run command. When False, an identical GRM and timeout may reuse the
previous result. When True, the next Run Button press ignores that last
identical result. Timeout defaults to 30 positive minutes.

Watch `State`, `Success`, and `Diagnostics`. `Run SimpleDragon` accepts one
data-matched model per component. Use the managed batch workflow for a list or
tree of models.

The first run may take longer while the verified per-user runtime and weather
cache are prepared. A matching InvisibleDragon package supplies the pinned
EnergyPlus archive offline. If it is not installed, SimpleDragon can reuse an
existing verified cache or use the verified network fallback for that exact
archive.

### 9. Read, plot, and export the GRR

Connect the GRR directly to one or more result components:

- `GRR Summary` extracts SiteUses, SourceUses, Carbon, or Cost. `Gross=False`
  returns per-area values; `Gross=True` returns whole-building values.
- `GRR Tree` creates monthly series in a Grasshopper DataTree.
- `Monthly Lines` and `Monthly Bars` draw result geometry. Connecting only the
  GRR is sufficient for a default plot: SiteUses per area, grouped by fuel, on
  a World XY frame with Width `12` and Height `6`. Bars default to unstacked.

Site and source energy are reported as kWh/m2 when Gross is False and kWh when
it is True. Carbon uses kgCO2e/m2 or kgCO2e, and Cost uses KRW/m2 or KRW.
Monthly series can be grouped by Fuel or End Use; Fuel is the default. Result
tree components preserve the incoming GRR path and append a series index, so
separate result branches stay separate downstream.

Use `Write GRR` to save the canonical result JSON, or `Export CSV` for a table
package. Connect a momentary Grasshopper Button to every Write or Export action
input and press it only for the intended write. These inputs are internally
level-sensitive, so a Toggle left True could write again on later solutions;
the Button avoids that accidental repetition. `Export CSV` requires the
`Overwrite` option Toggle to be True when replacing existing package files.
`Write GRM` and `Write GRR` have no separate Overwrite option and replace their
selected destination when their Write Button is pressed.

## Complete InvisibleDragon workflow

InvisibleDragon exposes the EnergyPlus-facing authoring model while keeping the
EnergyPlus executable, IDD, runtime root, and work directory internal.

```text
Material -> Layer -> Construction -> Floor/Ceiling/Wall -> Thermal Zone -> Energy Model
Glazing -> Window ---------------------------^                              |
Construction -> Door ------------------------+                              v
source -> terminal -> Thermal Zone ------------------------------> Compile InvisibleDragon
ERV -----------------> Thermal Zone                                      |
PV ------------------------------------------> Energy Model               +-> IDF -> Run InvisibleDragon
EPW file -> ID Weather ------------------------------------------------------------^
                                                                                   |
                                                                  EnergyPlus Result Summary
```

### 1. Create constructions and geometry

Use `Opaque Material` -> `Construction Layer` -> `Layered Construction` for a
mass assembly. InvisibleDragon layered construction inputs are ordered from
outside to inside. Use `No-Mass Construction` when a U-value-only assembly is
appropriate. Unlike a SimpleDragon surface, every InvisibleDragon opaque
surface requires a construction.

Use `Glazing` -> `Window From Polyline` for a transparent opening. Use an opaque
construction -> `Door From Polyline` for a door. Connect each completed Window
or Door directly to the `Openings` list of its owning `Floor`, `Ceiling`, or
`Wall`. The explicit surface components return gross area, net opaque area,
validity, and diagnostics.

### 2. Create profiles, HVAC, and zones

`Constant Profile` provides constant annual heating and cooling setpoints and
occupancy; its defaults are 20 °C heating, 26 °C cooling, and zero occupancy.
Connect one profile, the closed surface list, and any completed HVAC and ERV
values to `Thermal Zone`.

Zone defaults of infiltration `0` ACH, lighting power density `0` W/m2, and
outdoor air flow `0` m3/s are real zeros. Set the intended loads explicitly.
Connect `Energy Recovery Ventilator` directly to `ERVs`. Connect completed
terminals, not their plant sources, directly to `HVAC`.

### 3. Create and compile the model

Connect all zones and any `Photovoltaic Panel` values to `Energy Model`. North
Axis defaults to `0` degrees and Terrain defaults to `Suburbs`. The component
resolves coincident interzone surfaces and emits model diagnostics.

Connect the valid model to `Compile InvisibleDragon`. The component returns a
typed IDF, deterministic IDF text, a Valid flag, and diagnostics for EnergyPlus
24.2. It has no IDD-path input; the managed schema and execution mapping are
resolved internally.

### 4. Select and verify the EPW

InvisibleDragon deliberately does not infer weather. Enter an absolute `.epw`
path in `ID Weather`, or a relative path after saving the Grasshopper document.
A relative path is resolved from the folder containing the `.gh` file. The
component verifies that the file exists, has an EPW extension and a valid
`LOCATION` header, then emits an opaque content-addressed Weather value.

An empty EPW input is a quiet no-op, which makes a tracked example safe to
open. The runner verifies that the selected artifact is still present and
unchanged before execution. If the EPW is moved or edited, recompute
`ID Weather` and reconnect the new Weather value.

### 5. Run and inspect results

Connect the compiled IDF and verified Weather to `Run InvisibleDragon`. Connect
momentary Grasshopper Buttons to Run and Cancel, then press Run to start or
Cancel during an active simulation. `Force Rerun=True` is a persistent option
that bypasses reuse of the last identical IDF, weather, and timeout when the
next Run Button is pressed; changing Force alone does not start work. Timeout
defaults to 30 positive minutes.

The runner accepts one data-matched input set. Use one runner per simultaneous
InvisibleDragon simulation. Connect its structured Result to
`EnergyPlus Result Summary` to inspect success, warning/severe/fatal counts,
elapsed time, available monthly tables, work directory when retained, and
diagnostics. `Read EnergyPlus Results` is available for an existing EnergyPlus
output directory.

## HVAC wiring patterns

Always connect a source to the terminal that consumes it and the completed
terminal to the Zone. This keeps plant wiring local and prevents the same model
object from being routed across the canvas repeatedly.

| Terminal | Compatible source pattern |
| --- | --- |
| SimpleDragon Packaged AC | Source-free, cooling-only |
| InvisibleDragon Packaged AC | Heat Pump or Geothermal Heat Pump |
| Air Handling Unit | Heat Pump or Geothermal Heat Pump |
| Fan Coil Unit | Boiler, District Heating, Chiller, or Absorption Chiller |
| Radiator | Boiler or District Heating |
| SimpleDragon Radiant Floor | Boiler or District Heating |
| Electric Radiator | Source-free |
| Electric Radiant Floor | Source-free |

InvisibleDragon `Radiant Floor` asks for a non-heat-pump hydronic plant source.
Check the component's diagnostics and In/Out reference when using a less common
plant combination. Chillers may also require a cooling-tower value, and an
absorption chiller requires its generator heat definition; these relationships
are completed inside the source chain before a terminal is connected to a
Zone.

A `Fan Coil Unit` carries one compatible heating or cooling plant source per
authored value. Use separate completed terminals when a zone needs additional
systems. An ERV bypasses this source/terminal chain and connects directly to the
Zone. PV connects directly to the Model.

For SimpleDragon capacity ports labelled optional, leave the port disconnected
to retain autosize/unset. Do not attach a zero panel. InvisibleDragon inputs
that explicitly document `0 means autosize` may use zero; other values must
follow their individual validation ranges.

## Files, CSV packages, and batch studies

### GRM and GRR files

`Read GRM` and `Read GRR` load existing SimpleDragon files and return typed
values plus validation diagnostics. `Write GRM` and `Write GRR` always provide
their deterministic JSON and resolved path outputs, but they touch the file
system only during a Write Button pulse.

For a saved Grasshopper document, relative Dragon file paths resolve from its
folder. In an unsaved definition, read-only GRM/GRR paths resolve from the
current working directory, while GRM, GRR, and CSV output paths resolve from
the system temporary directory. Save the definition before using relative
paths that must be portable with the project.

### CSV package

With the Export Button unpressed, `Export CSV` previews the resolved directory,
stable file names, file paths, and contents without creating a directory. When
the Export Button is pressed, the package contains:

- `manifest.json`
- `summary.csv`
- `monthly_by_fuel.csv`
- `monthly_by_enduse.csv`
- `annual_by_fuel.csv`
- `annual_by_enduse.csv`
- `diagnostics.csv`
- `geometry_map.csv`

Every CSV is written as UTF-8 with a BOM so Korean text opens correctly in
Windows Excel. Numbers always use `.` as the decimal separator, enum values use
stable `snake_case`, dates use ISO 8601 text, and units are carried in column
names or explicit unit columns. Every result table includes `case_id`; the
combined batch CSV also preserves it for each input case.

`manifest.json` is UTF-8 JSON without a BOM. It records the schema and product
versions, tracked upstream commit, EnergyPlus version/build, canonical numeric
and enum formats, explicit-trigger and overwrite policies, and SHA-256 values
for the model, result, and every emitted CSV.

The separate batch `reproducibility-manifest.json` records the run fingerprint,
parallelism, executor and canonical options, both core versions, upstream and
EnergyPlus identities, plus each case's cache key, model/weather hashes,
snake-case status, metrics, and diagnostics. The release gate binds its verified
reports, packages, documentation, runtime payloads, and checksums into the local
candidate evidence.

GRR and Directory are required. GRM, diagnostics, and Model `Geometry Map Data`
are optional additions to the package. If files already exist, either choose a
new directory or deliberately set the `Overwrite` option Toggle to True. Press
the Export Button once for the intended write; do not leave a Toggle connected
to this level-sensitive action input.

### Managed SimpleDragon batch

Wrap each GRM in `SD Batch Case`; the case identity is derived internally.
Connect a list or tree of cases to `Managed Run SimpleDragon Batch`. The entire
tree is one batch and the Case IDs and Statuses outputs preserve input paths.

Parallel Limit defaults to the smaller of the machine's logical processor
count and `4`, with a minimum of `1`. Connect momentary Grasshopper Buttons to
Batch Run and Cancel and press one only for the intended action. The runner returns state, path-preserving identities and statuses,
combined CSV and manifest paths, a Complete flag, and diagnostics. Completed
cases remain available when an active batch is cancelled. Runtime, weather,
case-work, and result-storage paths stay internal.

## Worked examples

The repository examples are executable `.gh` and `.3dm` files, not screenshots.
Every simulation and file-write action is wired to a Grasshopper Button saved
at its resting False value. Force Rerun and Overwrite remain option Toggles.

| Start here | What it demonstrates |
| --- | --- |
| `examples/00-invisibledragon-material-construction.gh` | Minimal InvisibleDragon material, layer, construction, and U-value graph |
| `examples/01-invisibledragon-envelope-profile.gh` | Envelope construction and a constant profile |
| `examples/02-invisibledragon-single-zone-hvac-idf.gh` | Full InvisibleDragon surface ownership, Zone HVAC/ERV, Model, compile, explicit EPW verification, run, and result flow |
| `examples/10-simpledragon-material-construction.gh` | Minimal SimpleDragon construction graph |
| `examples/11-simpledragon-envelope-hvac.gh` | Fenestration, packaged profile, multiple compatible HVAC families, ERV, and PV |
| `examples/12-simpledragon-two-zone-model.gh` | Detailed two-zone list/tree authoring, automatic adjacency, ownership, HVAC, ERV, PV, GRM, and geometry mapping |
| `examples/13-simpledragon-results-and-plots.gh` | GRR reading, summaries, DataTree output, line/bar plots, and non-writing CSV preview |
| `examples/14-simpledragon-two-zone-run-results-csv.gh` | Complete zero-path SimpleDragon run, default monthly graph, CSV, and batch connection |
| `examples/30-two-zone-office.3dm` | Named planar two-zone Breps and window curves for live Rhino references |
| `examples/31-three-zone-stepped-office.3dm` | A larger stepped three-zone Rhino model |

For the quickest visible full process, open example 14. Press its Run Button
and watch State, Success, Diagnostics, and the monthly line plot. The plot
already has useful defaults, so the run's GRR is its only required connection.

For InvisibleDragon, open example 02, select a local EPW in its intentionally
empty EPW File parameter, let `ID Weather` verify it, then press the Run Button.
The example remains a safe preview until both actions are performed.

The two-zone Grasshopper example contains internalized geometry and opens by
itself. To work with live Rhino geometry, open
`examples/30-two-zone-office.3dm` and relink the named Breps and window curves.
Use Set Multiple Breps for a zone's plain walls, but keep each opening-bearing
wall on a matching branch.

## Troubleshooting

### Pressing Run does not start

Confirm that Run is connected to a Grasshopper Button rather than a Toggle. Let
the definition solve once with a newly connected Button unpressed, then press
it again. Changing the Force Rerun option alone does not launch a run. Use the
same Button pattern for Cancel and managed Batch Run/Cancel.

### Rhino reports access denied or a runtime cache cannot be prepared

Dragon runners do not write into Rhino's installation directory. Their owned
locations are per-user:

```text
EnergyPlus: %LOCALAPPDATA%\GonieGonie\BuildingEnergyRuntime\EnergyPlus\24.2.0-94a887817b
Weather:    %LOCALAPPDATA%\GonieGonie\BuildingEnergyWeather\SimpleDragon\korean-tmy-v1
Runs:       %TEMP%\GonieGonie\Dragons\energyplus-runs
```

Normal use should not require “Run as administrator.” Confirm that the current
Windows account can write to LocalAppData and the system temporary directory,
close all Rhino processes, and reinstall the matching product package. If a
cache is reported as corrupt, remove only the exact module-owned cache named in
the diagnostic; the next Run Button press verifies and prepares it again.
Do not redirect the cache into `Program Files` or the Rhino plugin folder.

### SimpleDragon rejects Address or Vintage

Use a Korean address beginning with a supported administrative-area prefix.
Whitespace may vary, but the leading administrative names must match the
packaged database. Use strict `yyyy-MM-dd` Vintage text and a date covered by
the climate record. The default 서울특별시 종로구 and `2020-01-01` provide a
known starting point. The current product has no public address-catalog listing
component, so use `SD Model` Diagnostics to identify an unsupported value.

### InvisibleDragon weather stays empty

An empty EPW input is intentionally quiet. Select an absolute EPW path, or save
the `.gh` file before using a relative path. Confirm the `.epw` extension,
read permission, and a valid `LOCATION` header. If the file changed after
verification, recompute `ID Weather`.

### A surface, opening, or adjacency is invalid

Check for one planar polygonal face/boundary, a closed opening curve, containment
inside the host, no opening overlap, the correct explicit Floor/Ceiling/Wall
component, and outward normals. For adjacency, provide one coincident Outdoors
surface per zone with opposite normals. Inspect the Surface, Zone, and Model
diagnostics in that order; fix the first ownership level that reports an error.

### One opening appears on several walls

The opening list was broadcast across a geometry list or flattened too early.
Separate the opening host from plain surfaces, or construct matching tree paths
so each surface branch receives only its own opening list.

### A plot is blank

The plot components wait quietly until they receive a complete valid GRR. Check
Run Success and Diagnostics or `Read GRR` Success first. Then connect the GRR
directly and retain the default Metric/Grouping until a basic plot appears.

### CSV export repeats or errors on a later solve

Connect a momentary Button, not a Toggle, to Export. The Button automatically
returns to False after the write pulse, preventing later solutions from
repeating it. When intentionally replacing existing files, enable the
Overwrite option Toggle before pressing Export, then disable Overwrite after
the write.

### One Dragon tab loads and the other fails

Do not mix assemblies from different candidates. Close Rhino and reinstall
both products from the same version. A complete Yak or portable package should
be replaced as a unit, not DLL by DLL.

### A failed or cancelled run left a large temporary directory

Successful runs remove their working directories after parsing. Failed and
cancelled runs are retained under the module-owned Runs location so EnergyPlus
logs remain inspectable. After diagnosis and with Rhino closed, those retained
run directories are disposable.

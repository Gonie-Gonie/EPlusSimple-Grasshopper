# Component In/Out Reference

_Generated from the public runtime catalog; do not edit this chapter directly._

This reference combines runtime-reflected Grasshopper ports with curated workflow guidance. It covers every public component in InvisibleDragon and SimpleDragon; port order, access mode, defaults, choices, and wire types come from the built plugins rather than a manually maintained list.

**Coverage:** 78 components and 38 standalone typed parameters for `net48 + net7.0-windows + net8.0-windows`.

A port marked optional accepts an omitted wire. A non-optional port can still show a persistent default; consult the Default / choices column before wiring a replacement. Choice inputs are selected directly on the component and are flagged so integer or identifier plumbing is unnecessary.

## InvisibleDragon

### Category: InvisibleDragon

#### Subcategory: Construction

##### Construction Layer (`Layer`)

**Role:** Authoring

**Purpose:** Combines one opaque material with a physical thickness into a reusable construction layer.

**How to use it:** Connect Opaque Material to Material, set Thickness in metres, and send the Layer output into the ordered Layers list of Layered Construction. Leave Name blank when the generated material-and-thickness label is sufficient.

**Canvas location:** InvisibleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- Thickness must be positive; the default is 0.1 m.
- Build each material/thickness pair as its own Layer instead of matching separate lists later.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Material (`M`) | InvisibleDragon Material | Item | No | — | Opaque material owned by this layer. |
| 1 | Thickness (`T`) | Number | Item | No | Default: `0.1` | Layer thickness in metres. |
| 2 | Name (`N`) | Text | Item | No | Default: `""` | Optional layer name. Blank generates a stable descriptive name. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Layer (`L`) | InvisibleDragon Construction Layer | Item | Typed construction layer. |

##### Glazing (`Glass`)

**Role:** Authoring

**Purpose:** Defines the aggregate U-value and solar heat-gain coefficient used by an InvisibleDragon window or glass door.

**How to use it:** Set the fenestration performance once, connect Glazing to ID Window or ID GlassDoor, and then connect the completed Opening only to its host Surface.

**Canvas location:** InvisibleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- U-value must be positive and SHGC must be between 0 and 1.
- This is a simple glazing system; panes, gas layers, frames, and shades are not authored separately.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Glazing` | Glazing name. |
| 1 | U-Value (`U`) | Number | Item | No | Default: `1.5` | Glazing U-value in W/(m² K). |
| 2 | SHGC (`g`) | Number | Item | No | Default: `0.5` | Solar heat-gain coefficient from 0 to 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Glazing (`G`) | InvisibleDragon Glazing | Item | InvisibleDragon glazing. |

##### Layered Construction (`Con`)

**Role:** Authoring

**Purpose:** Collects one or more construction layers into an opaque assembly and reports its calculated U-value.

**How to use it:** Connect a branch-local list of Layer outputs in outside-to-inside order, then reuse the resulting Construction on Floors, Ceilings, Walls, and opaque Doors that share the assembly.

**Canvas location:** InvisibleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- At least one valid Layer is required and layer order is significant.
- The reported U-value is based on layer resistances and excludes inside and outside surface-film resistance.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Layered Construction` | Construction name. |
| 1 | Layers (`L`) | InvisibleDragon Construction Layer | List | No | — | Construction layers ordered from outside to inside. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Construction (`C`) | InvisibleDragon Construction | Item | InvisibleDragon layered construction. |
| 1 | U-Value (`U`) | Number | Item | Calculated U-value in W/(m² K). |

##### No-Mass Construction (`NoMass`)

**Role:** Authoring

**Purpose:** Creates a lightweight opaque construction directly from a target U-value.

**How to use it:** Use it for schematic envelope studies where thermal storage is intentionally omitted, and connect it anywhere an opaque Construction is accepted.

**Canvas location:** InvisibleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- U-value must be positive; the default is 0.35 W/(m² K).
- It cannot reproduce material heat capacity and is not a good host construction for radiant-floor modeling.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `No-Mass Construction` | Construction name. |
| 1 | U-Value (`U`) | Number | Item | No | Default: `0.35` | U-value in W/(m² K). |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Construction (`C`) | InvisibleDragon Construction | Item | InvisibleDragon no-mass construction. |

##### Opaque Material (`Mat`)

**Role:** Authoring

**Purpose:** Defines the conductivity, density, and specific heat of a reusable opaque material.

**How to use it:** Enter SI material properties, connect Material to one or more Construction Layer components, and reuse the same output wherever the material specification is identical.

**Canvas location:** InvisibleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- Conductivity and density must be positive; specific heat must be at least 100 J/(kg K).
- Roughness and absorptance values are fixed internally in this release.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Opaque Material` | Material name. |
| 1 | Conductivity (`k`) | Number | Item | No | Default: `0.5` | Conductivity in W/(m K). |
| 2 | Density (ρ) | Number | Item | No | Default: `800` | Density in kg/m³. |
| 3 | Specific Heat (`Cp`) | Number | Item | No | Default: `1000` | Specific heat in J/(kg K). |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Material (`M`) | InvisibleDragon Material | Item | InvisibleDragon material. |

#### Subcategory: Core

##### InvisibleDragon Version (`InvisibleDragonVersion`)

**Role:** Utility

**Flags:** `UTILITY`

**Purpose:** Reports the installed InvisibleDragon version and the pinned upstream revision.

**How to use it:** Connect both outputs to a Panel when recording a study, preparing a screenshot, or reporting a compatibility problem. It does not participate in model authoring.

**Canvas location:** InvisibleDragon → Core. Exposure: `primary`.

**Important caveats:**

- Version information describes the loaded assembly, so include it when mixed-package loading is suspected.

**Inputs**

_This component has no inputs._

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Version (`V`) | Text | Item | InvisibleDragon.GH version. |
| 1 | Upstream (`U`) | Text | Item | Tracked upstream compatibility commit. |

##### Run InvisibleDragon (`Run`)

**Role:** Trigger

**Flags:** `RUN TRIGGER`

**Purpose:** Runs one compiled InvisibleDragon IDF against one verified EPW and returns a typed EnergyPlus result.

**How to use it:** Connect Compile InvisibleDragon to IDF and Verify InvisibleDragon Weather to Weather. Connect momentary Grasshopper Buttons to Run and Cancel, let them solve once at rest, then press Run; use the Result with EnergyPlus Result Summary.

**Canvas location:** InvisibleDragon → Core. Exposure: `primary`.

**Important caveats:**

- One component accepts one data-matched simulation; use separate Run components for separate low-level cases.
- An identical IDF, weather hash, and timeout can reuse the last result unless the Force Rerun option is enabled for the next Run Button press.
- Successful work directories are cleaned after parsing; failed and cancelled runs remain under the Windows temp directory for diagnosis.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | IDF (`IDF`) | InvisibleDragon IDF | Item | No | — | Compiled EnergyPlus IDF document. |
| 1 | Weather (`EPW`) | InvisibleDragon Prepared Weather | Item | No | — | Verified EPW handle from ID Weather. |
| 2 | Run (`Run`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it to start one run; do not use a Toggle for this action. |
| 3 | Cancel (`Cancel`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it to cancel the active run. |
| 4 | Force Rerun (`Force`) | Boolean | Item | No | Default: `False` | Ignore the last result for identical IDF, weather, and timeout inputs. |
| 5 | Timeout (`Min`) | Number | Item | No | Default: `30` | Positive timeout in minutes. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Result (`R`) | InvisibleDragon EnergyPlus Result | Item | Last structured EnergyPlus result. |
| 1 | State (`S`) | Text | Item | Idle, active EnergyPlus state, Cached, or terminal state. |
| 2 | Success (`OK`) | Boolean | Item | True when the last run succeeded. |
| 3 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Runtime and EnergyPlus diagnostics. |

##### Verify InvisibleDragon Weather (`ID Weather`)

**Role:** Utility

**Flags:** `UTILITY`

**Purpose:** Validates the explicit EPW chosen for a standalone InvisibleDragon simulation and creates an opaque Weather handle.

**How to use it:** Choose a local EPW, connect its path to EPW File, check Success, and wire Weather to Run InvisibleDragon. Relative paths resolve from the saved Grasshopper document.

**Canvas location:** InvisibleDragon → Core. Exposure: `primary`.

**Important caveats:**

- Blank input is an intentional no-op, which keeps example definitions safe to open.
- InvisibleDragon does not infer, select, or download weather from an address.
- An unsaved definition cannot resolve a relative EPW path.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | EPW File (`EPW`) | Text | Item | Yes | — | Absolute EPW file location, or a relative location resolved from the saved Grasshopper document. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Weather (`Weather`) | InvisibleDragon Prepared Weather | Item | Opaque, content-addressed weather handle for Run InvisibleDragon. |
| 1 | Success (`OK`) | Boolean | Item | True when the selected EPW was verified. |
| 2 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Path-free EPW verification diagnostics. |

#### Subcategory: Geometry

##### Ceiling (`Ceiling`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a vertex-preserving Ceiling Surface with its opaque construction, boundary intent, and owned openings.

**How to use it:** Connect a closed planar polygon, Construction, and named Boundary Condition, then combine the Surface with the other enclosure surfaces in one Thermal Zone branch.

**Canvas location:** InvisibleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Outdoors is the default; a coincident upper-zone Floor must also be Outdoors for automatic Floor/Ceiling adjacency.
- Openings must be coplanar, contained, non-overlapping, and uniquely named.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Curve (`C`) | Curve | Item | No | — | Closed planar polygonal surface boundary. |
| 1 | Name (`N`) | Text | Item | No | Default: `Ceiling` | Ceiling name. |
| 2 | Construction (`C`) | InvisibleDragon Construction | Item | No | — | Opaque Ceiling construction. |
| 3 | Boundary Condition (`BC`) | Text | Item | No | Default: `Outdoors`; Choices: `Outdoors`; `Ground`; `Adiabatic` | Outdoors, Ground, or Adiabatic. Coincident surfaces in distinct Zones become reciprocal Zone boundaries automatically. Choices: Outdoors, Ground, Adiabatic. |
| 4 | Openings (`O`) | InvisibleDragon Opening | List | Yes | — | Openings owned by this Ceiling. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Surface (`S`) | InvisibleDragon Surface | Item | InvisibleDragon surface. |
| 1 | Gross Area (`Gross`) | Number | Item | Surface gross area in m². |
| 2 | Net Area (`Net`) | Number | Item | Opaque net area after openings in m². |
| 3 | Valid (`V`) | Boolean | Item | True when opening containment and overlap validation pass. |
| 4 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Surface and opening diagnostics. |

##### Floor (`Floor`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a vertex-preserving Floor Surface and reports gross area, opaque net area, validity, and diagnostics.

**How to use it:** Connect a closed planar floor polygon and Construction, keep the Ground default for slab-on-ground work, and combine the output with Ceiling and Wall outputs in the Zone's Surfaces branch.

**Canvas location:** InvisibleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- For an inter-story Floor/Ceiling pair, change the Floor boundary to Outdoors so the Model may infer adjacency.
- Valid checks the Surface and its openings but does not prove that all Zone surfaces form a watertight enclosure.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Curve (`C`) | Curve | Item | No | — | Closed planar polygonal surface boundary. |
| 1 | Name (`N`) | Text | Item | No | Default: `Floor` | Floor name. |
| 2 | Construction (`C`) | InvisibleDragon Construction | Item | No | — | Opaque Floor construction. |
| 3 | Boundary Condition (`BC`) | Text | Item | No | Default: `Ground`; Choices: `Outdoors`; `Ground`; `Adiabatic` | Outdoors, Ground, or Adiabatic. Coincident surfaces in distinct Zones become reciprocal Zone boundaries automatically. Choices: Outdoors, Ground, Adiabatic. |
| 4 | Openings (`O`) | InvisibleDragon Opening | List | Yes | — | Openings owned by this Floor. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Surface (`S`) | InvisibleDragon Surface | Item | InvisibleDragon surface. |
| 1 | Gross Area (`Gross`) | Number | Item | Surface gross area in m². |
| 2 | Net Area (`Net`) | Number | Item | Opaque net area after openings in m². |
| 3 | Valid (`V`) | Boolean | Item | True when opening containment and overlap validation pass. |
| 4 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Surface and opening diagnostics. |

##### InvisibleDragon Door (`ID Door`)

**Role:** Authoring

**Purpose:** Creates an opaque ID Door Opening that already owns its construction.

**How to use it:** Connect a closed polygon and opaque Construction, then wire the completed Opening only to the Openings input of its host ID Floor, ID Ceiling, or usually ID Wall.

**Canvas location:** InvisibleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- The curve must be a closed polygon; curved/NURBS boundaries must be explicitly polygonized first.
- Coplanarity, host containment, and overlap are validated by the host Surface.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Curve (`C`) | Curve | Item | No | — | Closed planar polygonal door boundary. |
| 1 | Name (`N`) | Text | Item | No | Default: `Door` | Door name. |
| 2 | Construction (`C`) | InvisibleDragon Construction | Item | No | — | Opaque door construction. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Opening (`O`) | InvisibleDragon Opening | Item | InvisibleDragon door opening. |

##### InvisibleDragon Glass Door (`ID GlassDoor`)

**Role:** Authoring

**Purpose:** Creates a transparent ID GlassDoor Opening from a closed polygon and a completed Glazing definition.

**How to use it:** Connect the glass-door boundary and Glazing, then connect Opening only to its owning ID Floor, ID Ceiling, or usually ID Wall; the glazing does not need another Zone-level wire.

**Canvas location:** InvisibleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- InvisibleDragon's low-level domain distinguishes only Window and Door, so ID GlassDoor follows the same transparent Window domain route as ID Window.
- The boundary must be closed, planar, polygonal, coplanar with the host, fully contained by it, and non-overlapping with other openings.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Curve (`C`) | Curve | Item | No | — | Closed planar polygonal glass-door boundary. |
| 1 | Name (`N`) | Text | Item | No | Default: `Glass Door` | Glass-door name. |
| 2 | Glazing (`G`) | InvisibleDragon Glazing | Item | No | — | Glass-door glazing. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Opening (`O`) | InvisibleDragon Opening | Item | InvisibleDragon glass-door opening represented by transparent Window semantics. |

##### InvisibleDragon Window (`ID Window`)

**Role:** Authoring

**Purpose:** Creates an ID Window Opening from a closed polygon and a completed Glazing definition.

**How to use it:** Connect the Rhino window boundary and Glazing, then connect Opening only to its owning ID Floor, ID Ceiling, or usually ID Wall; the glazing does not need another Zone-level wire.

**Canvas location:** InvisibleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- The boundary must be closed, planar, polygonal, coplanar with the host, and fully contained by it.
- Overlapping host openings are rejected during Surface validation.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Curve (`C`) | Curve | Item | No | — | Closed planar polygonal window boundary. |
| 1 | Name (`N`) | Text | Item | No | Default: `Window` | Window name. |
| 2 | Glazing (`G`) | InvisibleDragon Glazing | Item | No | — | Window glazing. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Opening (`O`) | InvisibleDragon Opening | Item | InvisibleDragon window opening. |

##### Wall (`Wall`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a vertex-preserving Wall Surface and is the normal host for Window, Glass Door, and Door openings.

**How to use it:** Connect the wall polygon, Construction, boundary choice, and only that wall's completed Openings. Send the resulting Surface into the owning Thermal Zone branch.

**Canvas location:** InvisibleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Two coincident Outdoors walls in different Zones are paired automatically; do not add adjacent-surface IDs or indices.
- Interzone openings must be mirrored on both InvisibleDragon walls with matching geometry and type.
- Three or more coincident candidate surfaces are ambiguous and invalidate model composition.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Curve (`C`) | Curve | Item | No | — | Closed planar polygonal surface boundary. |
| 1 | Name (`N`) | Text | Item | No | Default: `Wall` | Wall name. |
| 2 | Construction (`C`) | InvisibleDragon Construction | Item | No | — | Opaque Wall construction. |
| 3 | Boundary Condition (`BC`) | Text | Item | No | Default: `Outdoors`; Choices: `Outdoors`; `Ground`; `Adiabatic` | Outdoors, Ground, or Adiabatic. Coincident surfaces in distinct Zones become reciprocal Zone boundaries automatically. Choices: Outdoors, Ground, Adiabatic. |
| 4 | Openings (`O`) | InvisibleDragon Opening | List | Yes | — | Openings owned by this Wall. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Surface (`S`) | InvisibleDragon Surface | Item | InvisibleDragon surface. |
| 1 | Gross Area (`Gross`) | Number | Item | Surface gross area in m². |
| 2 | Net Area (`Net`) | Number | Item | Opaque net area after openings in m². |
| 3 | Valid (`V`) | Boolean | Item | True when opening containment and overlap validation pass. |
| 4 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Surface and opening diagnostics. |

#### Subcategory: HVAC

##### Absorption Chiller (`AbsChiller`)

**Role:** Authoring

**Purpose:** Creates a thermally driven chilled-water source with an explicit generator boiler and cooling tower.

**How to use it:** Connect Boiler to Generator Boiler and Cooling Tower to Cooling Tower, then pass Source through a Fan Coil Unit before connecting the Supply to a Zone.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Generator Boiler must be a Boiler rather than District Heating.
- Nominal Capacity 0 means autosize; Thermal COP and pump efficiency must be positive and valid.
- This is a cooling source and should not be connected directly to a Zone.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Absorption Chiller` | Source-system name. |
| 1 | Thermal COP (`COP`) | Number | Item | No | Default: `1` | Rated thermal coefficient of performance. |
| 2 | Generator Boiler (`B`) | InvisibleDragon Source System | Item | No | — | Boiler source supplying generator heat. |
| 3 | Cooling Tower (`T`) | Generic Data | Item | No | — | CoolingTower value created by the Cooling Tower component. |
| 4 | Nominal Capacity (`Cap`) | Number | Item | No | Default: `0` | Rated cooling capacity in W; 0 means autosize. |
| 5 | Pump Motor Efficiency (`Eff`) | Number | Item | No | Default: `0.9` | Chilled-water pump motor efficiency from 0 to 1. |
| 6 | Chilled Water Setpoint (`Tset`) | Number | Item | No | Default: `6` | Chilled-water supply setpoint in degrees C. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | InvisibleDragon Source System | Item | InvisibleDragon absorption-chiller source. |

##### Air Handling Unit (`AHU`)

**Role:** Authoring

**Purpose:** Turns an air-source or geothermal Heat Pump into a heating-and-cooling zone supply terminal.

**How to use it:** Use the common path Heat Pump or Geothermal Heat Pump → Air Handling Unit → Thermal Zone HVAC. Adjust fan efficiency, pressure rise, and motor efficiency only when project data is available.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Boilers, chillers, and district sources belong to other terminal types.
- The connected Heat Pump owns the heating/cooling COP and capacity behavior.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Air Handling Unit` | Supply-system name. |
| 1 | Heat Pump (`HP`) | InvisibleDragon Source System | Item | No | — | HeatPump or GeothermalHeatPump source. |
| 2 | Fan Total Efficiency (`FanEff`) | Number | Item | No | Default: `0.7` | Supply-fan total efficiency from 0 to 1. |
| 3 | Fan Pressure Rise (`dP`) | Number | Item | No | Default: `100` | Supply-fan pressure rise in Pa. |
| 4 | Motor Efficiency (`Motor`) | Number | Item | No | Default: `0.9` | Fan motor efficiency from 0 to 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | InvisibleDragon Supply System | Item | InvisibleDragon air handling unit. |

##### Boiler (`Boiler`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a fuel-fired hot-water plant source for hydronic heating or absorption cooling.

**How to use it:** Connect Source to Radiator, Radiant Floor, a heating-mode Fan Coil Unit, or the Generator Boiler input of Absorption Chiller; connect only the resulting Supply to a Zone.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Nominal Capacity 0 means autosize.
- Thermal and pump efficiencies must be greater than 0 and no greater than 1.
- Choose Fuel by name from the input menu rather than using an integer enum.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Boiler` | Source-system name. |
| 1 | Fuel (`F`) | Text | Item | No | Default: `NaturalGas`; Choices: `Electricity`; Natural Gas (`NaturalGas`); `Propane`; Fuel Oil No 1 (`FuelOilNo1`); Fuel Oil No 2 (`FuelOilNo2`); `Coal`; `Diesel`; `Gasoline`; Other Fuel 1 (`OtherFuel1`); Other Fuel 2 (`OtherFuel2`) | Boiler fuel selection. Choices: Electricity, Natural Gas, Propane, Fuel Oil No 1, Fuel Oil No 2, Coal, Diesel, Gasoline, Other Fuel 1, Other Fuel 2. |
| 2 | Thermal Efficiency (`Eff`) | Number | Item | No | Default: `0.9` | Nominal thermal efficiency from 0 to 1. |
| 3 | Nominal Capacity (`Cap`) | Number | Item | No | Default: `0` | Rated heating capacity in W; 0 means autosize. |
| 4 | Pump Motor Efficiency (`Pump`) | Number | Item | No | Default: `0.9` | Hot-water pump motor efficiency from 0 to 1. |
| 5 | Hot Water Setpoint (`Tset`) | Number | Item | No | Default: `60` | Hot-water supply setpoint in degrees C. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | InvisibleDragon Source System | Item | InvisibleDragon boiler source. |

##### Chiller (`Chiller`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a water-cooled electric chilled-water source with an owned condenser connection.

**How to use it:** Connect a Cooling Tower, choose compressor type, and feed Source into Fan Coil Unit before sending the Supply to the Zone.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- A Cooling Tower is required and Nominal Capacity 0 means autosize.
- This source is cooling-only; the single-source Fan Coil path does not independently author a second heating plant.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Chiller` | Source-system name. |
| 1 | Reference COP (`COP`) | Number | Item | No | Default: `5` | Reference electric coefficient of performance. |
| 2 | Compressor (`Comp`) | Text | Item | No | Default: `Turbo`; Choices: `Turbo`; `Screw`; `Reciprocating` | Compressor selection: Turbo, Screw, or Reciprocating. Choices: Turbo, Screw, Reciprocating. |
| 3 | Cooling Tower (`T`) | Generic Data | Item | No | — | CoolingTower value created by the Cooling Tower component. |
| 4 | Nominal Capacity (`Cap`) | Number | Item | No | Default: `0` | Rated cooling capacity in W; 0 means autosize. |
| 5 | Pump Motor Efficiency (`Eff`) | Number | Item | No | Default: `0.9` | Chilled-water pump motor efficiency from 0 to 1. |
| 6 | Chilled Water Setpoint (`Tset`) | Number | Item | No | Default: `6` | Chilled-water supply setpoint in degrees C. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | InvisibleDragon Source System | Item | InvisibleDragon chiller source. |

##### Cooling Tower (`Tower`)

**Role:** Helper

**Flags:** `CHOICE INPUTS`

**Purpose:** Defines the condenser-side tower consumed by a Chiller or Absorption Chiller.

**How to use it:** Choose Open/Closed circuit and Single/Two fan speeds, then wire Cooling Tower to the matching plant component. It is not a Zone terminal.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Nominal Capacity 0 means autosize and pump efficiency defaults to 0.9.
- The Generic Data output is deliberately a plant helper, not an object to connect to Zone or Model.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Cooling Tower` | Cooling-tower name. |
| 1 | Circuit (`C`) | Text | Item | No | Default: `Open`; Choices: `Open`; `Closed` | Circuit selection: Open cooling tower or Closed fluid cooler. Choices: Open, Closed. |
| 2 | Fan Speeds (`S`) | Text | Item | No | Default: `Single`; Choices: `Single`; `Two` | Fan-speed selection: Single or Two speed. Choices: Single, Two. |
| 3 | Nominal Capacity (`Cap`) | Number | Item | No | Default: `0` | Heat-rejection capacity in W; 0 means autosize. |
| 4 | Pump Motor Efficiency (`Eff`) | Number | Item | No | Default: `0.9` | Condenser-loop pump motor efficiency from 0 to 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Cooling Tower (`T`) | Generic Data | Item | CoolingTower domain value for Chiller components. |

##### District Heating (`DistrictHeat`)

**Role:** Authoring

**Purpose:** Represents purchased hot water whose fuel conversion occurs outside the modeled building.

**How to use it:** Connect Source to Radiator, Radiant Floor, or a heating-mode Fan Coil Unit, then connect the completed Supply to Thermal Zone HVAC.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Nominal Capacity 0 means autosize.
- Use Boiler instead when on-site fuel and conversion efficiency must be represented.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `District Heating` | Source-system name. |
| 1 | Nominal Capacity (`Cap`) | Number | Item | No | Default: `0` | Available heating capacity in W; 0 means autosize. |
| 2 | Pump Motor Efficiency (`Pump`) | Number | Item | No | Default: `0.9` | Distribution-pump motor efficiency from 0 to 1. |
| 3 | Hot Water Setpoint (`Tset`) | Number | Item | No | Default: `60` | Hot-water supply setpoint in degrees C. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | InvisibleDragon Source System | Item | InvisibleDragon district-heating source. |

##### Domestic Hot Water (`DHW`)

**Role:** Compatibility

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a typed domestic-hot-water definition retained for domain compatibility and future integration.

**How to use it:** Use it only when inspecting or extending the domain model; it is not part of the executable 0.1.2 Grasshopper simulation graph.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- No public Zone or Model input currently accepts this output.
- The current Core export path produces no EnergyPlus objects for this value, so it must not be presented as an active load.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Domestic Hot Water` | Domestic-hot-water system name. |
| 1 | Fuel (`F`) | Text | Item | No | Default: `NaturalGas`; Choices: `Electricity`; Natural Gas (`NaturalGas`); `Propane`; Fuel Oil No 1 (`FuelOilNo1`); Fuel Oil No 2 (`FuelOilNo2`); `Coal`; `Diesel`; `Gasoline`; Other Fuel 1 (`OtherFuel1`); Other Fuel 2 (`OtherFuel2`) | Fuel selection. Choices: Electricity, Natural Gas, Propane, Fuel Oil No 1, Fuel Oil No 2, Coal, Diesel, Gasoline, Other Fuel 1, Other Fuel 2. |
| 2 | Efficiency (`Eff`) | Number | Item | No | Default: `0.85` | Fuel-to-water conversion efficiency greater than 0 and no greater than 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Domestic Hot Water (`DHW`) | InvisibleDragon Domestic Hot Water | Item | InvisibleDragon domestic-hot-water system. |

##### Electric Radiant Floor (`ElecRadiantFloor`)

**Role:** Authoring

**Purpose:** Creates a source-free electric radiant-floor terminal.

**How to use it:** Connect Supply directly to Thermal Zone HVAC. Use layered Floor constructions and a valid heating setpoint so the exporter can place the internal floor heat source.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- The Zone must contain at least one Floor and the throttling range must be positive.
- All Zone Floor surfaces participate; there is intentionally no face-index input.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Electric Radiant Floor` | Supply-system name. |
| 1 | Throttling Range (`dT`) | Number | Item | No | Default: `2` | Heating control throttling range in degrees C. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | InvisibleDragon Supply System | Item | InvisibleDragon electric radiant floor. |

##### Electric Radiator (`ElecRadiator`)

**Role:** Authoring

**Purpose:** Creates a simple source-free electric radiator suitable for a stable heating-only Zone.

**How to use it:** Connect Supply directly to Thermal Zone HVAC; no plant Source component is required. This is the shortest dependable HVAC path for a runnable example.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Heating Capacity 0 means autosize.
- Efficiency must be greater than 0 and no greater than 1; Radiant Fraction must be between 0 and 1.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Electric Radiator` | Supply-system name. |
| 1 | Heating Capacity (`Cap`) | Number | Item | No | Default: `0` | Rated heating capacity in W; 0 means autosize. |
| 2 | Efficiency (`Eff`) | Number | Item | No | Default: `1` | Electric conversion efficiency from 0 to 1. |
| 3 | Radiant Fraction (`Rad`) | Number | Item | No | Default: `0` | Fraction of heat emitted radiantly, from 0 to 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | InvisibleDragon Supply System | Item | InvisibleDragon electric radiator. |

##### Energy Recovery Ventilator (`ERV`)

**Role:** Authoring

**Purpose:** Creates a standalone sensible/latent energy-recovery ventilator owned by one Zone.

**How to use it:** Connect Ventilator to Thermal Zone ERVs rather than HVAC. Use a list of completed ERVs when a Zone has more than one distinct unit.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Supply Air Flow 0 means autosize.
- Effectiveness and fan efficiency must remain in their documented physical ranges.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Energy Recovery Ventilator` | Ventilator name. |
| 1 | Sensible Effectiveness (`Sens`) | Number | Item | No | Default: `0.75` | Sensible heat-recovery effectiveness from 0 to 1. |
| 2 | Latent Effectiveness (`Lat`) | Number | Item | No | Default: `0.65` | Latent heat-recovery effectiveness from 0 to 1. |
| 3 | Supply Air Flow (`Flow`) | Number | Item | No | Default: `0` | Supply air flow in m³/s; 0 means autosize. |
| 4 | Fan Total Efficiency (`FanEff`) | Number | Item | No | Default: `0.7` | Supply and exhaust fan total efficiency from 0 to 1. |
| 5 | Fan Pressure Rise (`dP`) | Number | Item | No | Default: `100` | Supply and exhaust fan pressure rise in Pa. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Ventilator (`V`) | InvisibleDragon Energy Recovery Ventilator | Item | InvisibleDragon energy recovery ventilator. |

##### Fan Coil Unit (`FCU`)

**Role:** Authoring

**Purpose:** Turns one compatible hydronic plant Source into a zone-side fan-coil Supply.

**How to use it:** Connect Boiler or District Heating for a heating path, or Chiller or Absorption Chiller for a cooling path, then connect Supply to Zone HVAC.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- The single connected Source determines the active mode; the opposite loop is only a structural auxiliary in this abstraction.
- Use separate terminals if the study requires independently authored heating and cooling systems.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Fan Coil Unit` | Supply-system name. |
| 1 | Plant Source (`Plant`) | InvisibleDragon Source System | Item | No | — | Boiler, DistrictHeating, Chiller, or AbsorptionChiller source. |
| 2 | Fan Total Efficiency (`FanEff`) | Number | Item | No | Default: `0.7` | Fan total efficiency from 0 to 1. |
| 3 | Fan Pressure Rise (`dP`) | Number | Item | No | Default: `100` | Fan pressure rise in Pa. |
| 4 | Motor Efficiency (`Motor`) | Number | Item | No | Default: `0.9` | Fan motor efficiency from 0 to 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | InvisibleDragon Supply System | Item | InvisibleDragon fan-coil unit. |

##### Geothermal Heat Pump (`GeoHeatPump`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a geothermal-labeled reversible heat-pump Source for an AHU or Packaged Air Conditioner.

**How to use it:** Set heating/cooling COP and optional autosize capacities, then wire Source through a compatible Supply terminal before the Zone.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Heating and Cooling Capacity 0 mean autosize.
- This release preserves geothermal identity but does not separately model boreholes, ground loops, or ground heat exchangers.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Geothermal Heat Pump` | Source-system name. |
| 1 | Fuel (`F`) | Text | Item | No | Default: `Electricity`; Choices: `Electricity`; Natural Gas (`NaturalGas`); `Propane`; Fuel Oil No 1 (`FuelOilNo1`); Fuel Oil No 2 (`FuelOilNo2`); `Coal`; `Diesel`; `Gasoline`; Other Fuel 1 (`OtherFuel1`); Other Fuel 2 (`OtherFuel2`) | Fuel selection. Geothermal heat pumps normally use Electricity. Choices: Electricity, Natural Gas, Propane, Fuel Oil No 1, Fuel Oil No 2, Coal, Diesel, Gasoline, Other Fuel 1, Other Fuel 2. |
| 2 | Heating COP (`HCOP`) | Number | Item | No | Default: `4` | Rated heating coefficient of performance. |
| 3 | Cooling COP (`CCOP`) | Number | Item | No | Default: `5` | Rated cooling coefficient of performance. |
| 4 | Heating Capacity (`HCap`) | Number | Item | No | Default: `0` | Rated heating capacity in W; 0 means autosize. |
| 5 | Cooling Capacity (`CCap`) | Number | Item | No | Default: `0` | Rated cooling capacity in W; 0 means autosize. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | InvisibleDragon Source System | Item | InvisibleDragon geothermal heat-pump source. |

##### Heat Pump (`HeatPump`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a reversible air-source heat-pump Source for packaged or air-handling terminals.

**How to use it:** Use Heat Pump → Air Handling Unit → Zone for heating and cooling, or Heat Pump → Packaged Air Conditioner → Zone for the exposed cooling-only terminal path.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Heating and Cooling Capacity 0 mean autosize; negative capacities are invalid.
- Source does not connect directly to Thermal Zone.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Heat Pump` | Source-system name. |
| 1 | Fuel (`F`) | Text | Item | No | Default: `Electricity`; Choices: `Electricity`; Natural Gas (`NaturalGas`); `Propane`; Fuel Oil No 1 (`FuelOilNo1`); Fuel Oil No 2 (`FuelOilNo2`); `Coal`; `Diesel`; `Gasoline`; Other Fuel 1 (`OtherFuel1`); Other Fuel 2 (`OtherFuel2`) | Fuel selection. Heat pumps normally use Electricity. Choices: Electricity, Natural Gas, Propane, Fuel Oil No 1, Fuel Oil No 2, Coal, Diesel, Gasoline, Other Fuel 1, Other Fuel 2. |
| 2 | Heating COP (`HCOP`) | Number | Item | No | Default: `3.5` | Rated heating coefficient of performance. |
| 3 | Cooling COP (`CCOP`) | Number | Item | No | Default: `4` | Rated cooling coefficient of performance. |
| 4 | Heating Capacity (`HCap`) | Number | Item | No | Default: `0` | Rated heating capacity in W; 0 means autosize. |
| 5 | Cooling Capacity (`CCap`) | Number | Item | No | Default: `0` | Rated cooling capacity in W; 0 means autosize. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | InvisibleDragon Source System | Item | InvisibleDragon heat-pump source. |

##### Packaged Air Conditioner (`PackagedAC`)

**Role:** Authoring

**Purpose:** Creates a cooling-only packaged zone terminal around an InvisibleDragon Heat Pump source.

**How to use it:** Connect Heat Pump or Geothermal Heat Pump to Heat Pump, then connect Supply directly to Thermal Zone HVAC.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- The terminal exposes no separate heating mode in this abstraction.
- Internal fan values use domain defaults; use Air Handling Unit when those fan inputs must be controlled.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Packaged AC` | Supply-system name. |
| 1 | Heat Pump (`HP`) | InvisibleDragon Source System | Item | No | — | HeatPump or GeothermalHeatPump source. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | InvisibleDragon Supply System | Item | InvisibleDragon packaged air conditioner. |

##### Radiant Floor (`RadiantFloor`)

**Role:** Authoring

**Purpose:** Creates a hydronic low-temperature radiant-floor Supply.

**How to use it:** Use Boiler or District Heating → Radiant Floor → Thermal Zone HVAC. Prefer layered Floor constructions and provide a usable heating setpoint.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- All Floor surfaces in the Zone participate; no face index is exposed.
- The component strictly rejects Heat Pump, but cooling sources should also not be used as heating plants even if a broad domain type reaches the input.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Radiant Floor` | Supply-system name. |
| 1 | Hydronic Source (`Plant`) | InvisibleDragon Source System | Item | No | — | Non-heat-pump hydronic plant source. |
| 2 | Throttling Range (`dT`) | Number | Item | No | Default: `2` | Heating control throttling range in degrees C. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | InvisibleDragon Supply System | Item | InvisibleDragon hydronic radiant floor. |

##### Radiator (`Radiator`)

**Role:** Authoring

**Purpose:** Creates a hydronic radiant-convective radiator Supply from Boiler or District Heating.

**How to use it:** Connect the compatible heating Source, then wire Supply directly to Thermal Zone HVAC.

**Canvas location:** InvisibleDragon → HVAC. Exposure: `primary`.

**Important caveats:**

- Heating Capacity 0 means autosize and Radiant Fraction must be between 0 and 1.
- Nonzero radiant output is distributed across Zone surfaces by gross area.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Radiator` | Supply-system name. |
| 1 | Heating Source (`Plant`) | InvisibleDragon Source System | Item | No | — | Boiler or DistrictHeating source. |
| 2 | Heating Capacity (`Cap`) | Number | Item | No | Default: `0` | Rated heating capacity in W; 0 means autosize. |
| 3 | Radiant Fraction (`Rad`) | Number | Item | No | Default: `0` | Fraction of heat emitted radiantly, from 0 to 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | InvisibleDragon Supply System | Item | InvisibleDragon hydronic radiator. |

#### Subcategory: Model

##### Compile InvisibleDragon (`ID to IDF`)

**Role:** Utility

**Flags:** `UTILITY`

**Purpose:** Compiles a typed InvisibleDragon Energy Model into the EnergyPlus 24.2 IDF used by the low-level runner.

**How to use it:** Connect Energy Model to Model, review Valid and Diagnostics, inspect Text in a Panel if needed, and pass IDF to Run InvisibleDragon. No EnergyPlus or IDD path is required.

**Canvas location:** InvisibleDragon → Model. Exposure: `primary`.

**Important caveats:**

- A typed IDF may coexist with Valid=False, so gate execution on validity and diagnostics.
- Managed IDD validation is used when available; otherwise the embedded 24.2 execution mapping reports deferred validation information.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Model (`M`) | InvisibleDragon Energy Model | Item | No | — | InvisibleDragon energy model. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | IDF (`IDF`) | InvisibleDragon IDF | Item | EnergyPlus 24.2 execution document. |
| 1 | Text (`T`) | Text | Item | Deterministic IDF text. |
| 2 | Valid (`V`) | Boolean | Item | True when model validation and any available managed-IDD validation pass. |
| 3 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Compilation diagnostics. |

##### Energy Model (`Model`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Resolves all authored Zones together, derives nested systems, infers compatible surface adjacency, and creates the complete low-level Energy Model.

**How to use it:** Connect completed Zone branches and optional model-level PV panels, choose Terrain and North Axis, then send Model to Compile InvisibleDragon.

**Canvas location:** InvisibleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Exactly two coincident Outdoors faces may pair as Wall/Wall or Floor/Ceiling; ambiguous multi-face coincidence is rejected.
- Zone, Surface, Opening, and PV names must be distinct where they identify different authored objects.
- HVAC sources and assignments are derived from Zone wires and need no parallel catalog connection.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `InvisibleDragon Model` | Model name. |
| 1 | Zones (`Z`) | InvisibleDragon Zone Definition | List | No | — | Zone definitions. Coincident surfaces across distinct Zones are paired automatically. |
| 2 | North Axis (`North`) | Number | Item | No | Default: `0` | North-axis rotation in degrees. |
| 3 | Terrain (`T`) | Text | Item | No | Default: `Suburbs`; Choices: `Country`; `Suburbs`; `City`; `Ocean`; `Urban` | Site terrain used by the EnergyPlus model. Choices: Country, Suburbs, City, Ocean, Urban. |
| 4 | PV Panels (`PV`) | InvisibleDragon Photovoltaic Panel | List | Yes | — | Optional model-level photovoltaic panels. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Model (`M`) | InvisibleDragon Energy Model | Item | InvisibleDragon energy model. |
| 1 | Valid (`V`) | Boolean | Item | True when adjacency and model validation pass. |
| 2 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Adjacency and model diagnostics. |

##### Thermal Zone (`Zone`)

**Role:** Authoring

**Purpose:** Collects one branch-local surface enclosure, one Profile, optional terminal HVAC systems, and optional ERVs into a Thermal Zone definition.

**How to use it:** Connect completed Floor/Ceiling/Wall outputs as one Surfaces branch, a Profile, only completed Supply objects to HVAC, and Ventilator objects to ERVs; send Zone to Energy Model.

**Canvas location:** InvisibleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Source systems never connect directly to the Zone.
- Lighting Power Density is emitted only when the connected Profile contains a Lighting schedule; Constant Profile does not.
- Outdoor Air Flow is stored but is not consumed by the current native IDF assembler; positive Constant Profile occupancy instead activates the current per-person ventilation path.
- Valid aggregates local diagnostics but does not prove watertight enclosure topology.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Zone` | Zone name. |
| 1 | Surfaces (`S`) | InvisibleDragon Surface | List | No | — | Closed boundary surfaces owned by this Zone. |
| 2 | Profile (`P`) | InvisibleDragon Profile | Item | No | — | Zone usage profile. |
| 3 | Infiltration (`ACH`) | Number | Item | No | Default: `0` | Infiltration in air changes per hour. |
| 4 | Lighting Power Density (`LPD`) | Number | Item | No | Default: `0` | Lighting power density in W/m². |
| 5 | Outdoor Air Flow (`OA`) | Number | Item | No | Default: `0` | Outdoor air flow in m³/s. |
| 6 | HVAC (`HVAC`) | InvisibleDragon Supply System | List | Yes | — | Supply systems owned by this Zone. |
| 7 | ERVs (`ERV`) | InvisibleDragon Energy Recovery Ventilator | List | Yes | — | Energy-recovery ventilators owned by this Zone. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Zone (`Z`) | InvisibleDragon Zone Definition | Item | InvisibleDragon Zone definition with directly owned HVAC and ERVs. |
| 1 | Valid (`V`) | Boolean | Item | True when the Zone definition has no error diagnostics. |
| 2 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Zone and owned-system diagnostics. |

#### Subcategory: Profile

##### Constant Profile (`Prof`)

**Role:** Authoring

**Purpose:** Creates a quick annual profile with constant heating/cooling setpoints, HVAC availability, and occupancy.

**How to use it:** Connect Profile directly to Thermal Zone for simple constant-condition studies. Positive Occupancy is interpreted as people per floor area by the current exporter.

**Canvas location:** InvisibleDragon → Profile. Exposure: `primary`.

**Important caveats:**

- It does not author Lighting, Equipment, or Hot Water schedules, so it is not a detailed weekday/seasonal schedule editor.
- A Zone with no Supply system is compiled as unconditioned rather than controlled to the profile's normal setpoints.
- Heating setpoint above cooling setpoint produces a warning.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Basic Profile` | Profile name. |
| 1 | Heating Setpoint (`Heat`) | Number | Item | No | Default: `20` | Constant heating setpoint in °C. |
| 2 | Cooling Setpoint (`Cool`) | Number | Item | No | Default: `26` | Constant cooling setpoint in °C. |
| 3 | Occupancy (`Occ`) | Number | Item | No | Default: `0` | Constant non-negative occupant schedule value. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Profile (`P`) | InvisibleDragon Profile | Item | InvisibleDragon zone profile. |
| 1 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Profile validation diagnostics. |

#### Subcategory: Results

##### EnergyPlus Result Summary (`Sum`)

**Role:** Result

**Flags:** `RESULT / ANALYSIS`

**Purpose:** Extracts the principal run state, diagnostic counts, timing, monthly table names, and work-directory information from an EnergyPlus Result.

**How to use it:** Connect Result from Run InvisibleDragon or Read EnergyPlus Results and use the outputs as the first post-run health check or issue-report panel.

**Canvas location:** InvisibleDragon → Results. Exposure: `primary`.

**Important caveats:**

- Monthly Tables contains table titles rather than their full cell series; InvisibleDragon has no dedicated plot component in this release.
- A successful run's reported work directory may already have been cleaned.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Result (`R`) | InvisibleDragon EnergyPlus Result | Item | No | — | Structured EnergyPlus result. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Run ID (`ID`) | Text | Item | Runtime run identifier, if known. |
| 1 | State (`S`) | Text | Item | Runtime state, if known. |
| 2 | Success (`OK`) | Boolean | Item | Runtime or EnergyPlus completion success. |
| 3 | Warnings (`W`) | Integer | Item | EnergyPlus warning count. |
| 4 | Severe (`E`) | Integer | Item | EnergyPlus severe error count. |
| 5 | Fatal (`F`) | Integer | Item | EnergyPlus fatal error count. |
| 6 | Elapsed (`Sec`) | Number | Item | Elapsed seconds, if known. |
| 7 | Monthly Tables (`M`) | Text | List | Available monthly table titles. |
| 8 | Work Directory (`Dir`) | Text | Item | EnergyPlus work directory, if known. |
| 9 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | EnergyPlus diagnostics. |

##### Read EnergyPlus Results (`ReadR`)

**Role:** Result

**Flags:** `RESULT / ANALYSIS`

**Purpose:** Imports an existing EnergyPlus output folder into the same typed Result used by the managed runner.

**How to use it:** Point Directory at the folder that contains eplusout.err, eplusout.audit, eplusout.bnd, and table CSV output, then connect Result to EnergyPlus Result Summary.

**Canvas location:** InvisibleDragon → Results. Exposure: `primary`.

**Important caveats:**

- Use the actual EnergyPlus output directory, not merely its parent work folder.
- Missing individual files are tolerated, so a wrong but existing directory can produce a mostly empty result rather than a path error.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Directory (`Dir`) | Text | Item | No | — | EnergyPlus output directory. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Result (`R`) | InvisibleDragon EnergyPlus Result | Item | Structured EnergyPlus result. |
| 1 | Diagnostics (`D`) | InvisibleDragon Diagnostic | List | Parsed EnergyPlus diagnostics. |

#### Subcategory: Systems

##### Photovoltaic Panel (`PV`)

**Role:** Authoring

**Purpose:** Creates a simplified fixed-performance photovoltaic generator from area and orientation values.

**How to use it:** Connect PV Panel to Energy Model PV Panels, not to a Zone or Surface. Use a list for multiple distinct arrays.

**Canvas location:** InvisibleDragon → Systems. Exposure: `primary`.

**Important caveats:**

- Azimuth is clockwise from north; area, efficiency, and active-cell fraction must satisfy their documented physical ranges.
- There is no Rhino panel geometry or host-surface placement input; export uses a simplified site-shading representation.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Photovoltaic Panel` | Photovoltaic panel name. |
| 1 | Area (`A`) | Number | Item | No | Default: `10` | Gross panel area in m². |
| 2 | Tilt (`Tilt`) | Number | Item | No | Default: `30` | Panel tilt in degrees from horizontal, 0 to 90. |
| 3 | Azimuth (`Az`) | Number | Item | No | Default: `180` | Panel azimuth in degrees clockwise from north, 0 to less than 360. |
| 4 | Efficiency (`Eff`) | Number | Item | No | Default: `0.2` | Module conversion efficiency from 0 to 1. |
| 5 | Active Cell Area Fraction (`Cell`) | Number | Item | No | Default: `0.7` | Fraction of gross area occupied by active cells, from 0 to 1. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | PV Panel (`PV`) | InvisibleDragon Photovoltaic Panel | Item | InvisibleDragon photovoltaic panel. |

## SimpleDragon

### Category: SimpleDragon

#### Subcategory: Analysis

##### SimpleDragon GRR Data Tree (`GRR Tree`)

**Role:** Analysis

**Flags:** `CHOICE INPUTS`

**Purpose:** Transforms one GRR metric into stable monthly Fuel or End Use series for native Grasshopper tree calculations.

**How to use it:** Find GRR Data Tree in the Analysis group, connect GRR, and normally leave the defaults for Site Uses per area by Fuel. Use Series Names with the matching X/Y branches, or consume the selected monthly CSV text without writing a file.

**Canvas location:** SimpleDragon → Analysis. Exposure: `primary`.

**Important caveats:**

- Each series appends its index to the incoming GRR path, preserving separate result branches instead of flattening them.
- X branches contain month numbers 1–12 and Y branches contain the corresponding twelve values; use Unit rather than assuming every metric is kWh.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | No | — | SimpleDragon result. |
| 1 | Metric (`M`) | Text | Item | No | Default: `SiteUses`; Choices: Site Uses (`SiteUses`); Source Uses (`SourceUses`); `Carbon`; `Cost` | Monthly GRR metric. Choices: Site Uses, Source Uses, Carbon, Cost. |
| 2 | Gross (`G`) | Boolean | Item | No | Default: `False` | False for per-area values; true for gross values. |
| 3 | Grouping (`By`) | Text | Item | No | Default: `Fuel`; Choices: `Fuel`; End Use (`EndUse`) | Monthly series grouping. Choices: Fuel, End Use. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Series Names (`N`) | Text | List | Stable snake_case series names. |
| 1 | Month Names (`Months`) | Text | List | January through December. |
| 2 | X Values (`X`) | Number | Tree | Month numbers, one branch per series. |
| 3 | Y Values (`Y`) | Number | Tree | Monthly values, one branch per series. |
| 4 | Unit (`U`) | Text | Item | Selected value unit. |
| 5 | CSV (`CSV`) | Text | Item | Selected deterministic monthly CSV. |

##### SimpleDragon GRR Summary (`GRR Summary`)

**Role:** Analysis

**Flags:** `CHOICE INPUTS`

**Purpose:** Extracts annual, January-to-December, carrier, and end-use totals for one selected GRR metric and basis.

**How to use it:** Find GRR Summary in the Analysis group, connect GRR, choose Site Uses, Source Uses, Carbon, or Cost, and keep Gross False for per-area comparison or enable it for whole-building totals. Pair each names list with its corresponding totals list.

**Canvas location:** SimpleDragon → Analysis. Exposure: `primary`.

**Important caveats:**

- Energy units are kWh/m² or kWh, carbon units kgCO2e/m² or kgCO2e, and cost units KRW/m² or KRW according to Gross.
- Monthly Totals are always January through December; carrier and end-use lists include stable zero-valued categories as well as active ones.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | No | — | SimpleDragon result. |
| 1 | Metric (`M`) | Text | Item | No | Default: `SiteUses`; Choices: Site Uses (`SiteUses`); Source Uses (`SourceUses`); `Carbon`; `Cost` | GRR summary metric. Choices: Site Uses, Source Uses, Carbon, Cost. |
| 2 | Gross (`G`) | Boolean | Item | No | Default: `False` | False for per-area values; true for gross building values. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Total Area (`A`) | Number | Item | Building floor area in m². |
| 1 | Annual Total (`Annual`) | Number | Item | Net annual total for the selected metric. |
| 2 | Monthly Totals (`Monthly`) | Number | List | January through December net totals. |
| 3 | Carriers (`C`) | Text | List | Energy carrier names. |
| 4 | Carrier Totals (`CV`) | Number | List | Totals corresponding to Carriers. |
| 5 | End Uses (`E`) | Text | List | Energy end-use names. |
| 6 | End-Use Totals (`EV`) | Number | List | Totals corresponding to End Uses. |
| 7 | Basis (`B`) | Text | Item | Selected metric and gross/per-area basis. |

##### SimpleDragon Model Summary (`SD Model Summary`)

**Role:** Analysis

**Purpose:** Exposes the Python-oracle-compatible derived envelope, load, infiltration, and weather properties of a typed SimpleDragon model without expanding the SD Model authoring component.

**How to use it:** Find SD Model Summary in the Analysis group and connect GRM from SimpleDragon Model or Read GRM. Use Floor Area and the typed exterior Surface/Fenestration lists for model checks, and use the weighted U-value, ACH50, LPD, climate, terrain, and weather-location outputs for downstream analysis.

**Canvas location:** SimpleDragon → Analysis. Exposure: `primary`.

**Important caveats:**

- Exterior Windows includes windows and glass doors hosted by exterior walls; exterior floors include outdoor and ground-contact floors.
- A zero weighted value can also mean there was no contributing resolved construction or load. Average Infiltration is the zone-volume-weighted source value at 50 Pa, not natural ACH.
- Weather outputs are unavailable when the GRM has no resolved weather selection; EPW filenames and paths remain internal.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRM (`GRM`) | SimpleDragon GRM | Item | No | — | SimpleDragon model to summarize. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Floor Area (`Area`) | Number | Item | Total conditioned zone floor area in m². |
| 1 | Exterior Floors (`Floors`) | SimpleDragon Surface | List | Exterior or ground-contact floor surfaces. |
| 2 | Exterior Roofs (`Roofs`) | SimpleDragon Surface | List | Outdoor ceiling surfaces used as exterior roofs. |
| 3 | Exterior Walls (`Walls`) | SimpleDragon Surface | List | Outdoor wall surfaces. |
| 4 | Exterior Windows (`Windows`) | SimpleDragon Fenestration | List | Windows and glass doors hosted by exterior walls. |
| 5 | Average Exterior Floor U-Value (`Floor U`) | Number | Item | Area-weighted U-value of exterior and ground-contact floors in W/(m²·K). |
| 6 | Average Exterior Roof U-Value (`Roof U`) | Number | Item | Area-weighted U-value of exterior roofs in W/(m²·K). |
| 7 | Average Exterior Wall U-Value (`Wall U`) | Number | Item | Area-weighted U-value of exterior walls in W/(m²·K). |
| 8 | Average Window U-Value (`Window U`) | Number | Item | Area-weighted U-value of exterior windows and glass doors in W/(m²·K). |
| 9 | Average Infiltration at 50 Pa (`ACH50`) | Number | Item | Zone-volume-weighted average infiltration rate at 50 Pa in air changes per hour. |
| 10 | Average Lighting Power Density (`LPD`) | Number | Item | Zone-area-weighted average lighting power density in W/m² for zones with a defined value. |
| 11 | Climate Region (`Climate`) | Text | Item | Resolved climate region embedded in the model. |
| 12 | Terrain (`Terrain`) | Text | Item | Resolved terrain category embedded in the model. |
| 13 | Weather Location (`Weather`) | Text | Item | Resolved weather-station location embedded in the model. |

##### SimpleDragon Monthly Bar Plot (`Monthly Bars`)

**Role:** Analysis

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates grouped or stacked monthly bar-outline geometry from the same typed series used by the numeric result components.

**How to use it:** Find Monthly Bars in the Analysis group, connect GRR for the default grouped Fuel chart, enable Stacked for monthly accumulation, and use the parallel Series Names, X/Y trees, and Unit to build labels or downstream visualization.

**Canvas location:** SimpleDragon → Analysis. Exposure: `primary`.

**Important caveats:**

- Bars are outline curves in one branch per series rather than filled chart objects; the Y Values tree remains the authoritative numeric result.
- Positive and negative values stack separately around Zero Axis, and Plane/Width/Height must be valid and positive.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | No | — | SimpleDragon result. |
| 1 | Metric (`M`) | Text | Item | No | Default: `SiteUses`; Choices: Site Uses (`SiteUses`); Source Uses (`SourceUses`); `Carbon`; `Cost` | Monthly GRR metric. Choices: Site Uses, Source Uses, Carbon, Cost. |
| 2 | Gross (`G`) | Boolean | Item | No | Default: `False` | False for per-area values; true for gross values. |
| 3 | Grouping (`By`) | Text | Item | No | Default: `Fuel`; Choices: `Fuel`; End Use (`EndUse`) | Monthly series grouping. Choices: Fuel, End Use. |
| 4 | Plane (`P`) | Plane | Item | No | Default: `World XY` | Plot plane. |
| 5 | Width (`W`) | Number | Item | No | Default: `12` | Plot width in model units. |
| 6 | Height (`H`) | Number | Item | No | Default: `6` | Plot height in model units. |
| 7 | Stacked (`S`) | Boolean | Item | No | Default: `False` | True stacks series by month; false groups them side by side. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Bars (`B`) | Curve | Tree | Bar-outline tree with one branch per series. |
| 1 | Frame (`F`) | Curve | Item | Plot frame. |
| 2 | Zero Axis (`Z`) | Curve | Item | Zero-value axis. |
| 3 | Series Names (`N`) | Text | List | Stable snake_case series names. |
| 4 | Month Names (`Months`) | Text | List | January through December. |
| 5 | X Values (`X`) | Number | Tree | Month numbers, one branch per series. |
| 6 | Y Values (`Y`) | Number | Tree | Monthly values, one branch per series. |
| 7 | Unit (`U`) | Text | Item | Selected value unit. |

##### SimpleDragon Monthly Line Plot (`Monthly Lines`)

**Role:** Analysis

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates immediately previewable monthly result curves while exposing the exact matching data trees for annotation or custom graphics.

**How to use it:** Find Monthly Lines in the Analysis group and connect only GRR for a default Site Uses-per-area Fuel plot on a 12 by 6 World XY frame, or set Metric, Gross, Grouping, Plane, Width, and Height for a custom layout.

**Canvas location:** SimpleDragon → Analysis. Exposure: `primary`.

**Important caveats:**

- The component draws one 12-point polyline per series plus Frame and Zero Axis, but it does not create colors, labels, or a legend; use Series Names and Month Names downstream.
- Plane must be valid and Width/Height finite and positive; before a GRR exists the component intentionally waits without a red error.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | No | — | SimpleDragon result. |
| 1 | Metric (`M`) | Text | Item | No | Default: `SiteUses`; Choices: Site Uses (`SiteUses`); Source Uses (`SourceUses`); `Carbon`; `Cost` | Monthly GRR metric. Choices: Site Uses, Source Uses, Carbon, Cost. |
| 2 | Gross (`G`) | Boolean | Item | No | Default: `False` | False for per-area values; true for gross values. |
| 3 | Grouping (`By`) | Text | Item | No | Default: `Fuel`; Choices: `Fuel`; End Use (`EndUse`) | Monthly series grouping. Choices: Fuel, End Use. |
| 4 | Plane (`P`) | Plane | Item | No | Default: `World XY` | Plot plane. |
| 5 | Width (`W`) | Number | Item | No | Default: `12` | Plot width in model units. |
| 6 | Height (`H`) | Number | Item | No | Default: `6` | Plot height in model units. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Lines (`L`) | Curve | List | One preview curve per series. |
| 1 | Frame (`F`) | Curve | Item | Plot frame. |
| 2 | Zero Axis (`Z`) | Curve | Item | Zero-value axis. |
| 3 | Series Names (`N`) | Text | List | Stable snake_case series names. |
| 4 | Month Names (`Months`) | Text | List | January through December. |
| 5 | X Values (`X`) | Number | Tree | Month numbers, one branch per series. |
| 6 | Y Values (`Y`) | Number | Tree | Monthly values, one branch per series. |
| 7 | Unit (`U`) | Text | Item | Selected value unit. |

#### Subcategory: Construction

##### SimpleDragon Construction Layer (`SD Layer`)

**Role:** Authoring

**Purpose:** Binds one opaque Material to one physical thickness so layer ownership is explicit before construction assembly.

**How to use it:** Connect Material and set Thickness in metres, then send each completed Layer to the ordered Layers list of SimpleDragon Surface Construction.

**Canvas location:** SimpleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- Thickness must be positive; leaving it at the 0.1 m default has real thermal meaning.
- Create each material/thickness pair as its own Layer instead of trying to match two independent lists at the Construction component.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Material (`M`) | SimpleDragon Material | Item | No | — | Opaque material owned by this layer. |
| 1 | Thickness (`T`) | Number | Item | No | Default: `0.1` | Layer thickness in metres. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Layer (`L`) | SimpleDragon Construction Layer | Item | Typed SimpleDragon construction layer. |

##### SimpleDragon Fenestration Construction (`SD Fenestration`)

**Role:** Authoring

**Purpose:** Defines the aggregate thermal and solar performance owned by a SimpleDragon Window, Glass Door, or opaque Door opening.

**How to use it:** Set U-Value and SHGC once, connect Construction to SD Window, SD Door, or SD GlassDoor, and let that completed opening carry the construction into its host Surface.

**Canvas location:** SimpleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- SHGC 0 creates an opaque door construction; a positive SHGC strictly below 1 creates a transparent window or glass-door construction.
- Window and Glass Door require a transparent construction, while Door requires SHGC 0 and cannot use a blind.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Simple Window` | Fenestration construction name. |
| 1 | U-Value (`U`) | Number | Item | No | Default: `1.5` | U-value in W/(m² K). |
| 2 | SHGC (`g`) | Number | Item | No | Default: `0.5` | Solar heat gain coefficient. Set zero for an opaque door. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Construction (`C`) | SimpleDragon Fenestration Construction | Item | SimpleDragon fenestration construction. |
| 1 | Transparent (`T`) | Boolean | Item | True when this construction is for windows or glass doors. |

##### SimpleDragon Material (`SD Material`)

**Role:** Authoring

**Purpose:** Defines a reusable opaque material from SI thermophysical properties for explicit envelope assemblies.

**How to use it:** Enter project material data, connect Material to one or more SimpleDragon Construction Layer components, and reuse the same output wherever the specification is identical.

**Canvas location:** SimpleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- Conductivity and density must be positive, and specific heat must be at least 100 J/(kg K).
- The defaults resemble a lightweight insulation example and should not be treated as a verified project specification.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Simple Material` | Material name. |
| 1 | Conductivity (`k`) | Number | Item | No | Default: `0.04` | Conductivity in W/(m K). |
| 2 | Density (ρ) | Number | Item | No | Default: `30` | Density in kg/m³. |
| 3 | Specific Heat (`Cp`) | Number | Item | No | Default: `1400` | Specific heat in J/(kg K). |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Material (`M`) | SimpleDragon Material | Item | SimpleDragon material. |

##### SimpleDragon Surface Construction (`SD Construction`)

**Role:** Authoring

**Purpose:** Collects one or more completed layers into an explicit opaque envelope assembly and calculates its film-inclusive U-value.

**How to use it:** Connect Layer outputs in the intended physical order, then reuse Construction on every Floor, Ceiling, or Wall that shares the assembly. Inspect U-Value to catch thickness or conductivity mistakes early.

**Canvas location:** SimpleDragon → Construction. Exposure: `primary`.

**Important caveats:**

- At least one valid Layer is required and the connected order is preserved.
- Connecting this explicit assembly overrides the automatic regulated-construction selection used when a Surface Construction input is left empty.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Simple Construction` | Construction name. |
| 1 | Layers (`L`) | SimpleDragon Construction Layer | List | No | — | Construction layers in SimpleDragon database order. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Construction (`C`) | SimpleDragon Surface Construction | Item | SimpleDragon surface construction. |
| 1 | U-Value (`U`) | Number | Item | U-value including default films in W/(m² K). |

#### Subcategory: Geometry

##### SimpleDragon Ceiling (`SD Ceiling`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Turns one planar single-face Brep into a Ceiling or roof Surface with its own envelope and boundary choices.

**How to use it:** Connect an upward-facing face, keep Outdoors for a roof or pair it with an upper-zone Outdoors Floor, and send the completed Surface into the owning Zone branch.

**Canvas location:** SimpleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Cool Roof Reflectance is valid only for an Outdoors Ceiling and must be greater than 0 and no greater than 1.
- An inter-zone Floor/Ceiling pair cannot retain cool-roof reflectance, and Ground or Adiabatic surfaces cannot contain openings.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Face (`F`) | Brep | Item | No | — | One valid single-face planar polygon Brep. |
| 1 | Name (`N`) | Text | Item | No | Default: `Ceiling` | Ceiling name. |
| 2 | Construction (`SC`) | SimpleDragon Surface Construction | Item | Yes | — | Optional opaque construction owned by this Ceiling; leave empty for an unknown construction. |
| 3 | Boundary Condition (`BC`) | Text | Item | No | Default: `Outdoors`; Choices: `Outdoors`; `Ground`; `Adiabatic` | Outdoors, Ground, or Adiabatic. Coincident Outdoors surfaces in different Zones become reciprocal Zone boundaries. Choices: Outdoors, Ground, Adiabatic. |
| 4 | Openings (`O`) | SimpleDragon Opening Definition | List | Yes | — | Completed openings owned by this Ceiling. Each opening owns its fenestration Construction. |
| 5 | Cool Roof Reflectance (`CR`) | Number | Item | Yes | — | Optional value in (0, 1], valid only when this Ceiling is Outdoors. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Surface (`S`) | SimpleDragon Surface Definition | Item | Geometry-backed Ceiling definition for one Zone. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Surface authoring diagnostics. |

##### SimpleDragon Door (`SD Door`)

**Role:** Authoring

**Purpose:** Creates a geometry-backed opaque Door that already owns its fenestration construction.

**How to use it:** Connect a closed planar door boundary and an opaque Fenestration Construction, then wire Opening only to the Floor, Ceiling, or normally Wall that owns it; Door has no Type or Blind input.

**Canvas location:** SimpleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Door requires an opaque construction with SHGC 0; transparent constructions are rejected.
- Host coplanarity, containment, overlap, and trim-loop agreement are checked when SimpleDragon Model resolves the complete geometry.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Boundary (`C`) | Curve | Item | No | — | Closed planar polygonal Door curve on its intended Surface. |
| 1 | Name (`N`) | Text | Item | No | Default: `Door` | Door name. |
| 2 | Construction (`FC`) | SimpleDragon Fenestration Construction | Item | No | — | Opaque fenestration construction owned by this Door. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Opening (`O`) | SimpleDragon Opening Definition | Item | Typed Door definition for one Surface. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Door authoring diagnostics. |

##### SimpleDragon Floor (`SD Floor`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Turns one planar single-face Brep into a Floor Surface with locally owned boundary intent, construction, and openings.

**How to use it:** Connect a downward-facing floor Brep, use Ground for slab-on-ground work or Outdoors for an inter-story pair, and combine the completed output with the other Surfaces owned by one Zone.

**Canvas location:** SimpleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Leaving Construction empty requests SimpleDragon's regulated construction selection from Address, Vintage, climate, housing type, and radiant-floor context.
- Ground and Adiabatic floors cannot own openings; a coincident upper-zone Ceiling must use Outdoors with the opposite normal for automatic adjacency.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Face (`F`) | Brep | Item | No | — | One valid single-face planar polygon Brep. |
| 1 | Name (`N`) | Text | Item | No | Default: `Floor` | Floor name. |
| 2 | Construction (`SC`) | SimpleDragon Surface Construction | Item | Yes | — | Optional opaque construction owned by this Floor; leave empty for an unknown construction. |
| 3 | Boundary Condition (`BC`) | Text | Item | No | Default: `Ground`; Choices: `Outdoors`; `Ground`; `Adiabatic` | Outdoors, Ground, or Adiabatic. Coincident Outdoors surfaces in different Zones become reciprocal Zone boundaries. Choices: Outdoors, Ground, Adiabatic. |
| 4 | Openings (`O`) | SimpleDragon Opening Definition | List | Yes | — | Completed openings owned by this Floor. Each opening owns its fenestration Construction. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Surface (`S`) | SimpleDragon Surface Definition | Item | Geometry-backed Floor definition for one Zone. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Surface authoring diagnostics. |

##### SimpleDragon Glass Door (`SD GlassDoor`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a geometry-backed Glass Door that already owns its transparent fenestration construction and optional blind.

**How to use it:** Connect a closed planar glass-door boundary and transparent Fenestration Construction, optionally choose a Blind, then wire Opening only to the Floor, Ceiling, or normally Wall that owns it; the component fixes the type as GlassDoor.

**Canvas location:** SimpleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- GlassDoor requires a transparent construction with SHGC greater than 0 and less than 1.
- Host coplanarity, containment, overlap, and trim-loop agreement are checked when SimpleDragon Model resolves the complete geometry.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Boundary (`C`) | Curve | Item | No | — | Closed planar polygonal Glass Door curve on its intended Surface. |
| 1 | Name (`N`) | Text | Item | No | Default: `Glass Door` | Glass Door name. |
| 2 | Construction (`FC`) | SimpleDragon Fenestration Construction | Item | No | — | Transparent fenestration construction owned by this Glass Door. |
| 3 | Blind (`Blind`) | Text | Item | No | Default: `None`; Choices: `None`; `Shade`; `Venetian` | Optional Shade or Venetian; leave empty or use None for no blind. Choices: None, Shade, Venetian. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Opening (`O`) | SimpleDragon Opening Definition | Item | Typed Glass Door definition for one Surface. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Glass Door authoring diagnostics. |

##### SimpleDragon Wall (`SD Wall`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a Wall Surface and is the normal ownership boundary for completed Window and Door openings.

**How to use it:** Connect one outward-facing planar Brep, optional explicit Construction, boundary choice, and only that face's Openings. Use one component for a list of plain walls and a separate component for each opening-bearing wall unless tree paths are deliberately matched.

**Canvas location:** SimpleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Two coincident Outdoors walls in different Zones require opposite normals and are promoted to a reciprocal Zone boundary automatically.
- A branch-local Openings list can be repeated across multiple Face items by Grasshopper data matching, so separate hosts to avoid accidental broadcast.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Face (`F`) | Brep | Item | No | — | One valid single-face planar polygon Brep. |
| 1 | Name (`N`) | Text | Item | No | Default: `Wall` | Wall name. |
| 2 | Construction (`SC`) | SimpleDragon Surface Construction | Item | Yes | — | Optional opaque construction owned by this Wall; leave empty for an unknown construction. |
| 3 | Boundary Condition (`BC`) | Text | Item | No | Default: `Outdoors`; Choices: `Outdoors`; `Ground`; `Adiabatic` | Outdoors, Ground, or Adiabatic. Coincident Outdoors surfaces in different Zones become reciprocal Zone boundaries. Choices: Outdoors, Ground, Adiabatic. |
| 4 | Openings (`O`) | SimpleDragon Opening Definition | List | Yes | — | Completed openings owned by this Wall. Each opening owns its fenestration Construction. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Surface (`S`) | SimpleDragon Surface Definition | Item | Geometry-backed Wall definition for one Zone. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Surface authoring diagnostics. |

##### SimpleDragon Window (`SD Window`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a geometry-backed Window that already owns its transparent fenestration construction and optional blind.

**How to use it:** Connect a closed planar window boundary and transparent Fenestration Construction, optionally choose a Blind, then wire Opening only to the Floor, Ceiling, or normally Wall that owns it; the component fixes the type as Window.

**Canvas location:** SimpleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Host coplanarity, containment, overlap, and trim-loop agreement are checked when SimpleDragon Model resolves the complete geometry.
- A Rhino inner trim still needs matching Opening metadata, and the same opening list should not be broadcast to unrelated face items.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Boundary (`C`) | Curve | Item | No | — | Closed planar polygonal Window curve on its intended Surface. |
| 1 | Name (`N`) | Text | Item | No | Default: `Window` | Window name. |
| 2 | Construction (`FC`) | SimpleDragon Fenestration Construction | Item | No | — | Transparent fenestration construction owned by this Window. |
| 3 | Blind (`Blind`) | Text | Item | No | Default: `None`; Choices: `None`; `Shade`; `Venetian` | Optional Shade or Venetian; leave empty or use None for no blind. Choices: None, Shade, Venetian. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Opening (`O`) | SimpleDragon Opening Definition | Item | Typed Window definition for one Surface. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Window authoring diagnostics. |

##### SimpleDragon Zone (`SD Zone`)

**Role:** Authoring

**Purpose:** Collects one branch-local surface enclosure, usage profile, terminal HVAC list, and ERV list into a Zone definition.

**How to use it:** Connect completed Floor, Ceiling, and Wall outputs to Surfaces; connect one Profile; connect only Supply outputs to HVAC and Zone ERV outputs to ERVs; then send Zone to SimpleDragon Model.

**Canvas location:** SimpleDragon → Geometry. Exposure: `primary`.

**Important caveats:**

- Source systems never connect directly to Zone, and the Zone has no Brep, construction, opening, or identifier input.
- Height must be positive, lighting power density non-negative, and a Zone may contain at most one hydronic or electric radiant-floor system.
- Use the ERV Count input for identical units instead of duplicating one deterministic ERV value.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Surfaces (`S`) | SimpleDragon Surface Definition | List | No | — | Surface definitions owned by this Zone. |
| 1 | Name (`N`) | Text | Item | No | Default: `Zone` | Zone name. |
| 2 | Floor Number (`F`) | Integer | Item | No | Default: `0` | Zone floor number. |
| 3 | Height (`H`) | Number | Item | No | Default: `3` | Positive SimpleDragon Zone height in metres. |
| 4 | Profile (`P`) | SimpleDragon Usage Profile | Item | No | — | Zone usage profile. |
| 5 | HVAC (`HVAC`) | SimpleDragon Supply System | List | Yes | — | Supply systems owned by this Zone. |
| 6 | ERVs (`ERV`) | SimpleDragon Zone ERV | List | Yes | — | ERV values owned by this Zone. |
| 7 | Lighting Power Density (`LPD`) | Number | Item | No | Default: `10` | Lighting power density in W/m². |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Zone (`Z`) | SimpleDragon Zone Definition | Item | Surface-backed Zone definition for SimpleDragon Model. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Zone authoring diagnostics. |

#### Subcategory: Model

##### Lookup SimpleDragon Usage Profile (`SD Profile`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Resolves a complete packaged Korean operating profile for occupancy, HVAC, ventilation, loads, setpoints, holidays, and vacations.

**How to use it:** Find SD Profile in the Model group, choose Name directly from its native selector, and connect the resulting Profile to every Zone that follows that use pattern. No Panel or copied text is required.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- A real packaged usage profile is selected by default, and every packaged name is available from the input selector.
- Text supplied by a wire must normalize to one packaged selector choice; use the selector whenever possible so the canonical packaged name remains visible.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: 주거공간; Choices: 주거공간; 소규모사무실; 대규모사무실; 회의실 및 세미나실; 강당; 구내식당; 화장실; 그 외 체류공간; 부속공간; 창고/설비/문서실; 전산실; 주방 및 조리실; 병실; 객실; 교실(초중고); 강의실(대학); 매장(상점/백화점); 전시실(전시관/박물관); 열람실(도서관); 체육시설; 구내식당(초중고); 주방 및 조리실(초중고); 체육시설(초중고); 교실(어린이집) | Packaged usage profile. Choices: 주거공간, 소규모사무실, 대규모사무실, 회의실 및 세미나실, 강당, 구내식당, 화장실, 그 외 체류공간, 부속공간, 창고/설비/문서실, 전산실, 주방 및 조리실, 병실, 객실, 교실(초중고), 강의실(대학), 매장(상점/백화점), 전시실(전시관/박물관), 열람실(도서관), 체육시설, 구내식당(초중고), 주방 및 조리실(초중고), 체육시설(초중고), 교실(어린이집). |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Profile (`P`) | SimpleDragon Usage Profile | Item | Resolved usage profile. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Lookup diagnostics. |

##### SimpleDragon Absorption Chiller (`SD Absorption`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates an absorption cooling Source whose generator boiler, fuel, and boiler efficiency are contained in one simplified definition.

**How to use it:** Choose generator fuel, set thermal COP and boiler efficiency, optionally size cooling capacity, then use Absorption Chiller → Fan Coil Unit → Zone HVAC.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Thermal COP and connected capacity must be positive, and boiler efficiency must be greater than 0 and no greater than 1.
- There is no separate generator-boiler wire in SimpleDragon; its properties are owned by this Source.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Absorption Chiller` | Absorption-chiller name. |
| 1 | Fuel (`Fuel`) | Text | Item | No | Default: `NaturalGas`; Choices: `Electricity`; Natural Gas (`NaturalGas`); Liquefied Petroleum Gas (`LiquefiedPetroleumGas`); `Oil`; District Heating (`DistrictHeating`) | Generator-boiler fuel. Choices: Electricity, Natural Gas, Liquefied Petroleum Gas, Oil, District Heating. |
| 2 | Thermal COP (`COP`) | Number | Item | No | Default: `0.9` | Dimensionless thermal cooling COP (> 0). |
| 3 | Cooling Capacity (`Cap`) | Number | Item | Yes | — | Optional nominal cooling capacity in W; leave disconnected for autosize/unset. |
| 4 | Boiler Efficiency (`Eff`) | Number | Item | No | Default: `0.85` | Generator-boiler thermal efficiency fraction in (0, 1]. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | SimpleDragon Source System | Item | Authored absorption-chiller source. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Air Handling Unit (`SD AHU`)

**Role:** Authoring

**Purpose:** Turns a reversible Heat Pump or Geothermal Heat Pump Source into the zone-side AHU Supply accepted by a Zone.

**How to use it:** Connect Heat Pump or Geothermal Heat Pump to Source, then wire the resulting Supply to the owning Zone's HVAC list.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Boiler, District Heating, Chiller, and Absorption Chiller are rejected as incompatible AHU sources.
- COP and capacity live on the connected Source; this compact AHU exposes no second sizing layer.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Air Handling Unit` | Supply-system name. |
| 1 | Source (`Src`) | SimpleDragon Source System | Item | No | — | Required compatible SimpleDragon source system. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | SimpleDragon Supply System | Item | Authored SimpleDragon supply system. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Compatibility and authoring diagnostics. |

##### SimpleDragon Boiler (`SD Boiler`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a fuel-fired hot-water Source for simplified hydronic terminals and optional domestic-hot-water metadata.

**How to use it:** Choose Fuel, efficiency, optional heating capacity, and Hot Water Supply, then connect Source to Fan Coil Unit, Radiator, or Radiant Floor before the Zone.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Efficiency must be in (0, 1], while disconnected capacity remains unset/autosized and connected capacity must be positive.
- Hot Water Supply records source responsibility; it is not a separate DHW load or schedule component.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Boiler` | Boiler name. |
| 1 | Fuel (`Fuel`) | Text | Item | No | Default: `NaturalGas`; Choices: `Electricity`; Natural Gas (`NaturalGas`); Liquefied Petroleum Gas (`LiquefiedPetroleumGas`); `Oil`; District Heating (`DistrictHeating`) | Boiler fuel. Choices: Electricity, Natural Gas, Liquefied Petroleum Gas, Oil, District Heating. |
| 2 | Efficiency (`Eff`) | Number | Item | No | Default: `0.85` | Nominal thermal efficiency fraction in (0, 1]. |
| 3 | Heating Capacity (`Cap`) | Number | Item | Yes | — | Optional nominal heating capacity in W; leave disconnected for autosize/unset. |
| 4 | Hot Water Supply (`DHW`) | Boolean | Item | No | Default: `False` | Whether the boiler also serves domestic hot water metadata. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | SimpleDragon Source System | Item | Authored boiler source. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Chiller (`SD Chiller`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Defines an electric chiller and its cooling-tower circuit as one self-contained cooling Source.

**How to use it:** Choose compressor, tower circuit, and fan control, set COP and optional chiller/tower capacities, then use Chiller → Fan Coil Unit → Zone HVAC.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Compressor choices are Turbo, Screw, and Reciprocating; tower choices are Open/Closed and Single/Two Speed.
- This is a cooling Source and does not connect directly to Zone; optional capacities are unset when disconnected.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Chiller` | Chiller name. |
| 1 | Cooling COP (`COP`) | Number | Item | No | Default: `3` | Dimensionless reference cooling COP (> 0). |
| 2 | Cooling Capacity (`Cap`) | Number | Item | Yes | — | Optional nominal cooling capacity in W; leave disconnected for autosize/unset. |
| 3 | Compressor (`Comp`) | Text | Item | No | Default: `Turbo`; Choices: `Turbo`; `Screw`; `Reciprocating` | Compressor family. Choices: Turbo, Screw, Reciprocating. |
| 4 | Tower Circuit (`Tower`) | Text | Item | No | Default: `Open`; Choices: `Closed`; `Open` | Cooling-tower circuit. Choices: Closed, Open. |
| 5 | Tower Control (`Control`) | Text | Item | No | Default: `SingleSpeed`; Choices: Single Speed (`SingleSpeed`); Two Speed (`TwoSpeed`) | Cooling-tower fan control. Choices: Single Speed, Two Speed. |
| 6 | Tower Capacity (`TCap`) | Number | Item | Yes | — | Optional nominal cooling-tower capacity in W; leave disconnected for autosize/unset. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | SimpleDragon Source System | Item | Authored chiller source. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon District Heating (`SD District Heat`)

**Role:** Authoring

**Purpose:** Represents purchased district heat without disguising it as a local fuel-fired boiler.

**How to use it:** Set optional service capacity and Hot Water Supply, then connect Source to Fan Coil Unit, Radiator, or Radiant Floor before the Zone.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- There is intentionally no Fuel input because District Heating is preserved as its own energy carrier.
- The optional capacity is unset when disconnected and must be positive when supplied.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `District Heating` | District-heating service name. |
| 1 | Heating Capacity (`Cap`) | Number | Item | Yes | — | Optional nominal heating capacity in W; leave disconnected for autosize/unset. |
| 2 | Hot Water Supply (`DHW`) | Boolean | Item | No | Default: `False` | Whether the service also supplies domestic hot water metadata. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | SimpleDragon Source System | Item | Authored district-heating source. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Electric Radiant Floor (`SD Electric Floor`)

**Role:** Authoring

**Purpose:** Creates a source-free electric radiant-floor Supply with the shortest radiant-heating wire path.

**How to use it:** Name the system and connect Supply directly to Zone HVAC; SimpleDragon applies radiant-floor behavior to the Zone rather than asking for floor indices.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- There is no capacity input in this release.
- It counts toward the same one-radiant-floor-system-per-Zone limit as the hydronic variant.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Electric Radiant Floor` | Electric-radiant-floor name. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | SimpleDragon Supply System | Item | Authored electric radiant floor. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Electric Radiator (`SD Electric Radiator`)

**Role:** Authoring

**Purpose:** Creates a simple source-free electric radiator suitable for a stable heating-only Zone path.

**How to use it:** Optionally set heating capacity and connect Supply directly to Zone HVAC; no Boiler, heat pump, or other Source is required.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- A disconnected capacity preserves unset/autosize behavior; a connected value must be positive.
- Use a separate cooling Supply if the Zone also requires active cooling.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Electric Radiator` | Electric-radiator name. |
| 1 | Heating Capacity (`Cap`) | Number | Item | Yes | — | Optional heating capacity in W; leave disconnected for autosize/unset. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | SimpleDragon Supply System | Item | Authored electric radiator. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Energy Recovery Ventilator (`SD ERV`)

**Role:** Authoring

**Purpose:** Creates both an ERV definition and its owned Zone assignment in one step.

**How to use it:** Set airflow and sensible heating/cooling recovery efficiencies, use Count for identical units, and connect Zone ERV directly to the one Zone that owns it.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Airflow and Count must be positive, while both efficiencies must be strictly between 0 and 1.
- Connect this output to Zone ERVs, not HVAC, and use Count instead of duplicating an identical deterministic ERV value.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Energy Recovery Ventilator` | Ventilator name. |
| 1 | Airflow (`Flow`) | Number | Item | No | Default: `0.2` | Design supply airflow rate in m³/s (> 0). |
| 2 | Heating Efficiency (`HEff`) | Number | Item | No | Default: `0.7` | Sensible heating-recovery efficiency fraction in (0, 1). |
| 3 | Cooling Efficiency (`CEff`) | Number | Item | No | Default: `0.45` | Cooling-recovery efficiency fraction in (0, 1). |
| 4 | Count (`Count`) | Integer | Item | Yes | Default: `1` | Optional positive number of identical ERV units owned by the connected Zone. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Zone ERV (`ERV`) | SimpleDragon Zone ERV | Item | Owned ERV value to connect directly to one SimpleDragon Zone. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Fan Coil Unit (`SD Fan Coil`)

**Role:** Authoring

**Purpose:** Turns one compatible hydronic heating or cooling Source into a zone-side fan-coil Supply.

**How to use it:** Connect Boiler or District Heating for a heating path, or Chiller or Absorption Chiller for a cooling path, then wire Supply to Zone HVAC.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Heat Pump and Geothermal Heat Pump are not accepted by this SimpleDragon terminal.
- One Fan Coil Unit owns one Source; use additional terminal objects if the study needs separately authored heating and cooling paths.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Fan Coil Unit` | Supply-system name. |
| 1 | Source (`Src`) | SimpleDragon Source System | Item | No | — | Required compatible SimpleDragon source system. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | SimpleDragon Supply System | Item | Authored SimpleDragon supply system. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Compatibility and authoring diagnostics. |

##### SimpleDragon Geothermal Heat Pump (`SD Geothermal`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a reversible geothermal-labeled Source while retaining the same compact COP and capacity authoring surface as the air-source heat pump.

**How to use it:** Set heating/cooling COP and optional capacities, then use Geothermal Heat Pump → Air Handling Unit → Zone HVAC.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- This release preserves geothermal identity but does not separately author boreholes, ground loops, or heat-exchanger geometry.
- Disconnected capacities remain unset/autosized; connected values must be positive.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Geothermal Heat Pump` | Source-system name. |
| 1 | Fuel (`Fuel`) | Text | Item | No | Default: `Electricity`; Choices: `Electricity`; Natural Gas (`NaturalGas`); Liquefied Petroleum Gas (`LiquefiedPetroleumGas`); `Oil`; District Heating (`DistrictHeating`) | Energy carrier used by the heat pump. Choices: Electricity, Natural Gas, Liquefied Petroleum Gas, Oil, District Heating. |
| 2 | Heating COP (`HCOP`) | Number | Item | No | Default: `3` | Dimensionless heating coefficient of performance (> 0). |
| 3 | Cooling COP (`CCOP`) | Number | Item | No | Default: `3` | Dimensionless cooling coefficient of performance (> 0). |
| 4 | Heating Capacity (`HCap`) | Number | Item | Yes | — | Optional nominal heating capacity in W; leave disconnected for autosize/unset. |
| 5 | Cooling Capacity (`CCap`) | Number | Item | Yes | — | Optional nominal cooling capacity in W; leave disconnected for autosize/unset. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | SimpleDragon Source System | Item | Authored SimpleDragon source system. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Heat Pump (`SD Heat Pump`)

**Role:** Authoring

**Flags:** `CHOICE INPUTS`

**Purpose:** Creates a reversible air-source heat-pump plant Source for the simplified AHU supply path.

**How to use it:** Set heating and cooling COP, leave capacities disconnected for unset/autosize behavior when appropriate, then use Heat Pump → Air Handling Unit → Zone HVAC.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- COP and any connected capacities must be positive; a connected zero is not the same as leaving a capacity empty.
- Source does not connect directly to a Zone, and the current public AHU compatibility accepts Heat Pump or Geothermal Heat Pump only.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Heat Pump` | Source-system name. |
| 1 | Fuel (`Fuel`) | Text | Item | No | Default: `Electricity`; Choices: `Electricity`; Natural Gas (`NaturalGas`); Liquefied Petroleum Gas (`LiquefiedPetroleumGas`); `Oil`; District Heating (`DistrictHeating`) | Energy carrier used by the heat pump. Choices: Electricity, Natural Gas, Liquefied Petroleum Gas, Oil, District Heating. |
| 2 | Heating COP (`HCOP`) | Number | Item | No | Default: `3` | Dimensionless heating coefficient of performance (> 0). |
| 3 | Cooling COP (`CCOP`) | Number | Item | No | Default: `3` | Dimensionless cooling coefficient of performance (> 0). |
| 4 | Heating Capacity (`HCap`) | Number | Item | Yes | — | Optional nominal heating capacity in W; leave disconnected for autosize/unset. |
| 5 | Cooling Capacity (`CCap`) | Number | Item | Yes | — | Optional nominal cooling capacity in W; leave disconnected for autosize/unset. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Source (`S`) | SimpleDragon Source System | Item | Authored SimpleDragon source system. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Model (`SD Model`)

**Role:** Authoring

**Purpose:** Resolves all Zone definitions together, infers geometry adjacency, derives every nested catalog, selects climate metadata, and creates the complete GRM 0.7 model.

**How to use it:** Connect all completed Zone branches and optional model-level PV panels, set Address and Vintage, inspect Diagnostics, then wire GRM directly to Run SimpleDragon and Model Summary. Geometry provenance stays inside the typed GRM and follows it automatically into Run and CSV Export.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Address must equal or begin with a packaged Korean administrative-area name; Vintage must use yyyy-MM-dd and be covered by the climate database.
- An active Rhino document supplies units and tolerances, and Floor/Ceiling/Wall normals must agree with their explicit component types.
- North Axis remains a model value while wall azimuths use Rhino world north; do not pre-rotate geometry merely to apply North Axis.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `SimpleDragon Model` | Building/model name. |
| 1 | Zones (`Z`) | SimpleDragon Zone Definition | List | No | — | Zone definitions. Their coincident owned Surfaces are resolved collectively for shared-boundary adjacency. |
| 2 | North Axis (`North`) | Number | Item | No | Default: `0` | Clockwise building north-axis rotation in degrees. |
| 3 | Address (`A`) | Text | Item | No | Default: 서울특별시 종로구 | Korean address used internally to select climate metadata and packaged EPW. |
| 4 | Vintage (`V`) | Text | Item | No | Default: `2020-01-01` | Building vintage as yyyy-MM-dd. |
| 5 | Multifamily Housing (`MF`) | Boolean | Item | No | Default: `False` | True for multifamily housing. |
| 6 | Photovoltaic Panels (`PV`) | SimpleDragon Photovoltaic Panel | List | Yes | — | Optional model-level photovoltaic panels. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | GRM (`GRM`) | SimpleDragon GRM | Item | Complete GRM 0.7 model. |
| 1 | Zones (`Z`) | SimpleDragon Zone | List | Resolved immutable thermal zones. |
| 2 | Surfaces (`S`) | SimpleDragon Surface | Tree | Resolved area-based surfaces, one branch per Zone. |
| 3 | JSON (`J`) | Text | Item | Deterministic GRM 0.7 JSON. |
| 4 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Geometry, weather, and model diagnostics. |

##### SimpleDragon Packaged Air Conditioner (`SD Packaged AC`)

**Role:** Authoring

**Purpose:** Creates a source-free cooling-only packaged terminal for a short direct Zone HVAC path.

**How to use it:** Set cooling COP and optional capacity, then connect Supply directly to Zone HVAC without creating a plant Source.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- The terminal exposes cooling only; pair the Zone with a separate heating Supply when heating is required.
- Cooling capacity remains unset/autosized when disconnected and must be positive when connected.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Packaged Air Conditioner` | Packaged-air-conditioner name. |
| 1 | Cooling COP (`COP`) | Number | Item | No | Default: `3` | Dimensionless cooling COP (> 0). |
| 2 | Cooling Capacity (`Cap`) | Number | Item | Yes | — | Optional cooling capacity in W; leave disconnected for autosize/unset. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | SimpleDragon Supply System | Item | Authored packaged air conditioner. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Photovoltaic Panel (`SD PV`)

**Role:** Authoring

**Purpose:** Defines a simplified fixed photovoltaic array from active area, efficiency, azimuth, and tilt.

**How to use it:** Connect PV to SimpleDragon Model's Photovoltaic Panels list, not to a Zone, Ceiling, or host Surface. Use a list for multiple distinct arrays.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Area must be positive, efficiency in (0, 1], azimuth in [0, 360), and tilt in [0, 90].
- There is no panel Brep or host geometry; distinguish repeated arrays by meaningful names or values to avoid deterministic duplicate identities.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Photovoltaic Panel` | Photovoltaic-panel name. |
| 1 | Area (`A`) | Number | Item | No | Default: `10` | Active panel area in m² (> 0). |
| 2 | Efficiency (`Eff`) | Number | Item | No | Default: `0.2` | Conversion efficiency fraction in (0, 1]. |
| 3 | Azimuth (`Az`) | Number | Item | No | Default: `180` | Clockwise azimuth from north in degrees [0, 360). |
| 4 | Tilt (`Tilt`) | Number | Item | No | Default: `30` | Tilt above horizontal in degrees [0, 90]. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | PV (`PV`) | SimpleDragon Photovoltaic Panel | Item | Authored photovoltaic panel. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Authoring diagnostics. |

##### SimpleDragon Radiant Floor (`SD Radiant Floor`)

**Role:** Authoring

**Purpose:** Creates a hydronic radiant-floor Supply whose presence also informs regulated Floor construction selection.

**How to use it:** Use Boiler or District Heating → Radiant Floor → Zone HVAC, and author the Zone's actual Floor Surfaces rather than selecting faces by index.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- A Zone may own at most one radiant-floor system across hydronic and electric variants.
- This compact terminal has no separate capacity input and rejects non-heating Sources.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Radiant Floor` | Supply-system name. |
| 1 | Source (`Src`) | SimpleDragon Source System | Item | No | — | Required compatible SimpleDragon source system. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | SimpleDragon Supply System | Item | Authored SimpleDragon supply system. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Compatibility and authoring diagnostics. |

##### SimpleDragon Radiator (`SD Radiator`)

**Role:** Authoring

**Purpose:** Creates a hydronic radiator Supply from a Boiler or District Heating Source.

**How to use it:** Connect the compatible heating Source, optionally provide terminal heating capacity, and connect Supply directly to the owning Zone's HVAC list.

**Canvas location:** SimpleDragon → Model. Exposure: `primary`.

**Important caveats:**

- Chiller, absorption, and heat-pump Sources are rejected; compatibility errors are returned as Diagnostics rather than an invalid Supply.
- Heating capacity remains unset/autosized when disconnected and must be positive when connected.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Name (`N`) | Text | Item | No | Default: `Radiator` | Radiator name. |
| 1 | Source (`Src`) | SimpleDragon Source System | Item | No | — | Required Boiler or District Heating source. |
| 2 | Heating Capacity (`Cap`) | Number | Item | Yes | — | Optional heating capacity in W; leave disconnected for autosize/unset. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Supply (`S`) | SimpleDragon Supply System | Item | Authored hydronic radiator. |
| 1 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Compatibility and authoring diagnostics. |

#### Subcategory: Simulation

##### Export SimpleDragon CSV (`Export CSV`)

**Role:** Simulation

**Purpose:** Builds a deterministic eight-file analysis package from a GRR and optionally enriches its manifest, diagnostics, and geometry provenance.

**How to use it:** Find Export CSV in the Simulation group, connect GRR, Directory, and optionally GRM and Run Diagnostics, then inspect File Names, Paths, and Content while the Export Button is unpressed. Press Export once for a deliberate user-owned write; live Rhino provenance follows the typed GRM and GRR automatically.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- The package contains manifest.json, summary.csv, monthly/annual files by fuel and end use, diagnostics.csv, and geometry_map.csv. A GRM or GRR read from a standalone file has no Rhino-session provenance, so geometry_map.csv is header-only unless live Grasshopper context is present.
- An unpressed Export Button provides the no-write preview, while the Overwrite option Toggle blocks existing package files when False. Export is internally level-sensitive, so use a momentary Button rather than a Toggle.
- File Paths describe what would be written; Written is the authoritative indication that all package files were created in that solution.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | No | — | SimpleDragon result to export. |
| 1 | GRM (`GRM`) | SimpleDragon GRM | Item | Yes | — | Optional source model metadata for manifest.json. |
| 2 | Directory (`D`) | Text | Item | No | — | Requested export directory. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory. |
| 3 | Diagnostics (`Diag`) | SimpleDragon Diagnostic | List | Yes | — | Optional diagnostics to include. |
| 4 | Export (`E`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it once to write; unpressed previews content without creating files. |
| 5 | Overwrite (`O`) | Boolean | Item | No | Default: `False` | Explicitly allow replacement of existing package files. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Full Directory (`D`) | Text | Item | Resolved export directory. |
| 1 | File Names (`N`) | Text | List | Stable manifest/CSV file order. |
| 2 | File Paths (`P`) | Text | List | Resolved paths that were or would be written. |
| 3 | Content (`C`) | Text | List | Deterministic manifest/CSV content in File Names order. |
| 4 | Written (`OK`) | Boolean | Item | True only when this solution wrote every package file. |

##### Managed Run SimpleDragon Batch (`Managed Batch`)

**Role:** Trigger

**Flags:** `RUN TRIGGER`

**Purpose:** Executes a branch-preserving research matrix of SimpleDragon models with managed runtime, packaged weather, caching, and result artifacts.

**How to use it:** In the Simulation group, feed a Case tree from SimpleDragon Batch Case, choose a practical Parallel Limit, and connect momentary Grasshopper Buttons to Run and Cancel. Let them solve once at rest, then press Run. Track State, Case IDs, Statuses, Combined CSV, Manifest, and Complete.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- Parallel Limit must be 1–1024; the default caps ordinary execution at four simultaneous cases or the processor count.
- Case IDs and Statuses preserve the original paths, but the component outputs aggregate CSV/manifest artifacts rather than one GRR wire per case.
- Pressing the Cancel Button preserves already completed cases; Complete is True only when every case succeeds.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Cases (`Cases`) | SimpleDragon Batch Case | Tree | No | — | Typed batch-case tree. Branches are preserved in Case IDs and Statuses; execution identity and weather are resolved within SimpleDragon. |
| 1 | Parallel Limit (`N`) | Integer | Item | No | Default: `4` | Maximum simultaneous EnergyPlus cases. |
| 2 | Run (`R`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it to explicitly start one batch. |
| 3 | Cancel (`C`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it to cancel the active batch while preserving completed cases. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | State (`S`) | Text | Item | Current managed batch state and progress. |
| 1 | Case IDs (`IDs`) | Text | Tree | Case identities in the original input paths. |
| 2 | Statuses (`Status`) | Text | Tree | Case statuses in the original input paths. |
| 3 | Combined CSV (`CSV`) | Text | Item | Deterministic combined CSV result path. |
| 4 | Manifest (`Manifest`) | Text | Item | Deterministic reproducibility manifest result path. |
| 5 | Complete (`OK`) | Boolean | Item | True when every case succeeded. |
| 6 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | Path-free preparation and per-case diagnostics. |

##### Read SimpleDragon GRM (`Read GRM`)

**Role:** Simulation

**Purpose:** Loads an existing strict GRM 0.7 document into the same typed model used by the visual authoring workflow.

**How to use it:** Find Read GRM in the Simulation group, connect a .grm or JSON path, check Success and Diagnostics, and send GRM directly to Run SimpleDragon, Batch Case, or Write GRM. Use Canonical JSON when reviewing deterministic differences.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- Relative paths use the saved Grasshopper document folder; an unsaved definition falls back to the process working directory for reads.
- This is an artifact interchange path, not a required stage between SimpleDragon Model and Run.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Path (`P`) | Text | Item | No | — | Path to a GRM JSON file. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | GRM (`GRM`) | SimpleDragon GRM | Item | Parsed GRM model. |
| 1 | Zones (`Z`) | SimpleDragon Zone | List | Zones contained in the model. |
| 2 | Canonical JSON (`J`) | Text | Item | Deterministic canonical GRM JSON. |
| 3 | Success (`OK`) | Boolean | Item | True when parsing and reference resolution succeeded. |
| 4 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | GRM read diagnostics. |

##### Read SimpleDragon GRR (`Read GRR`)

**Role:** Simulation

**Purpose:** Loads a strict GRR 0.7 artifact into the same typed result consumed by every SimpleDragon analysis component.

**How to use it:** Find Read GRR in the Simulation group, connect a GRR path, check Success and Diagnostics, then fan GRR out to Analysis-group Summary, Data Tree, and Plots or to Write GRR and CSV Export without rerunning EnergyPlus.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- Relative paths use the saved Grasshopper document folder; unsaved read paths fall back to the process working directory.
- Invalid or incomplete GRR content does not become a usable result even when the file itself exists.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Path (`P`) | Text | Item | No | — | Path to a GRR JSON file. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | Parsed GRR result. |
| 1 | Canonical JSON (`J`) | Text | Item | Deterministic canonical GRR JSON. |
| 2 | Success (`OK`) | Boolean | Item | True when the GRR is complete and valid. |
| 3 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | GRR read diagnostics. |

##### Run SimpleDragon (`SD Run`)

**Role:** Trigger

**Flags:** `RUN TRIGGER`

**Purpose:** Runs one complete GRM through address-selected packaged weather and module-managed EnergyPlus, returning the final typed GRR and optionally saving the canonical GRR file without exposing an intermediate IDF workflow.

**How to use it:** Find SD Run in the Simulation group and connect GRM directly from SimpleDragon Model or Read GRM. Optionally connect a user-owned .grr or JSON destination to GRR Path; leave it blank to keep the result in Grasshopper only. Connect momentary Grasshopper Buttons to Run and Cancel, let them solve once at rest, then press Run. Send GRR to the Analysis-group summaries and plots or to Simulation-group Write GRR and CSV Export.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- One Run component accepts one data-matched input set; use Batch Case and Managed Batch for model lists or trees.
- The last successful identical model/weather/timeout result is reused unless the Force Rerun option is True for the next Run Button press.
- A Run pulse saves a newly completed or cached GRR when GRR Path is set, creates missing parent directories, and replaces an existing destination; changing the path alone never launches work.
- Previous GRR output is hidden while a run or cached-GRR save is active, or after simulation inputs change; a save failure keeps the completed in-memory GRR usable and is reported through State and Diagnostics.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRM (`GRM`) | SimpleDragon GRM | Item | No | — | Complete SimpleDragon model. Its Address and Vintage select the packaged weather internally. |
| 1 | Run (`Run`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it to start one run; do not use a Toggle for this action. |
| 2 | Cancel (`Cancel`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it to cancel the active run. |
| 3 | Force Rerun (`Force`) | Boolean | Item | No | Default: `False` | Ignore the last result for an identical GRM and timeout. |
| 4 | Timeout (`Min`) | Number | Item | No | Default: `30` | Positive EnergyPlus timeout in minutes. |
| 5 | GRR Path (`P`) | Text | Item | Yes | — | Optional destination .grr or JSON path. Leave blank to keep the GRR in memory only. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | Last complete SimpleDragon result. |
| 1 | State (`State`) | Text | Item | Idle, preparation/execution progress, Cached, or a terminal state. |
| 2 | Success (`OK`) | Boolean | Item | True when the last run produced a complete GRR. A requested file-write failure is reported by State and Diagnostics while the in-memory GRR remains usable. |
| 3 | Diagnostics (`D`) | SimpleDragon Diagnostic | List | SimpleDragon conversion, weather, runtime, simulation, and result diagnostics. |

##### SimpleDragon Batch Case (`SD Batch Case`)

**Role:** Simulation

**Purpose:** Wraps one complete GRM alternative in the typed value consumed by the managed batch runner.

**How to use it:** Connect GRM items, lists, or trees to create matching Case values, then preserve those branches when wiring Cases into Managed Run SimpleDragon Batch.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- There is intentionally no public Case ID input; stable identity is derived internally from the model and execution order.
- Runtime, weather, work, and output paths are not stored as user-authored case inputs.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRM (`GRM`) | SimpleDragon GRM | Item | No | — | One complete SimpleDragon model alternative. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | Case (`Case`) | SimpleDragon Batch Case | Item | Typed SimpleDragon batch case. |

##### Write SimpleDragon GRM (`Write GRM`)

**Role:** Simulation

**Purpose:** Serializes a typed model as deterministic UTF-8 GRM 0.7 JSON for exchange, review, or versioned study records.

**How to use it:** Connect GRM and a destination, inspect JSON and Full Path while the Write Button is unpressed, then press the Button once when the user-owned artifact should be created.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- Relative output paths use the saved .gh folder; unsaved definitions use the Windows temp directory.
- Write is internally level-sensitive, so connect a momentary Grasshopper Button rather than a Toggle; one Button pulse bounds the overwrite to the intended solution.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRM (`GRM`) | SimpleDragon GRM | Item | No | — | GRM model to serialize. |
| 1 | Path (`P`) | Text | Item | No | — | Destination .grm or JSON path. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory. |
| 2 | Write (`W`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it once to write. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | JSON (`J`) | Text | Item | Deterministic GRM JSON. |
| 1 | Full Path (`P`) | Text | Item | Resolved destination path. |
| 2 | Written (`OK`) | Boolean | Item | True when the file was written during this solution. |

##### Write SimpleDragon GRR (`Write GRR`)

**Role:** Simulation

**Purpose:** Serializes a typed GRR as deterministic UTF-8 GRR 0.7 JSON for reproducible exchange or archival.

**How to use it:** Connect GRR and a destination, inspect JSON and Full Path while the Write Button is unpressed, then press the Button once when the result artifact should be persisted.

**Canvas location:** SimpleDragon → Simulation. Exposure: `primary`.

**Important caveats:**

- Relative output paths use the saved .gh folder; unsaved definitions use the Windows temp directory.
- Write is internally level-sensitive, so connect a momentary Grasshopper Button rather than a Toggle; one Button pulse bounds the overwrite to the intended solution.

**Inputs**

| # | Input (nickname) | Wire type | Access | Optional | Default / choices | Description |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | GRR (`GRR`) | SimpleDragon GRR | Item | No | — | GRR result to serialize. |
| 1 | Path (`P`) | Text | Item | No | — | Destination .grr or JSON path. Relative paths use the saved Grasshopper document; unsaved definitions use the system temp directory. |
| 2 | Write (`W`) | Boolean | Item | No | Default: `False` | Connect a momentary Grasshopper Button and press it once to write. |

**Outputs**

| # | Output (nickname) | Wire type | Access | Description |
| --- | --- | --- | --- | --- |
| 0 | JSON (`J`) | Text | Item | Deterministic GRR JSON. |
| 1 | Full Path (`P`) | Text | Item | Resolved destination path. |
| 2 | Written (`OK`) | Boolean | Item | True when the file was written during this solution. |

## Typed parameter appendix

Typed parameters are the native Grasshopper containers carried by component wires. They are listed here for canvas inspection, relays, and data management; they are not additional modeling steps and do not require users to handle internal identifiers.

### InvisibleDragon

#### Category: InvisibleDragon

##### Subcategory: Parameters

| Parameter | Nickname | Wire type | Exposure | Description |
| --- | --- | --- | --- | --- |
| EnergyPlus Result | `Result` | InvisibleDragon EnergyPlus Result | secondary | A structured EnergyPlus simulation result. |
| InvisibleDragon Construction | `Construction` | InvisibleDragon Construction | secondary | An InvisibleDragon surface construction. |
| InvisibleDragon Construction Layer | `Layer` | InvisibleDragon Construction Layer | secondary | One material and thickness in an InvisibleDragon opaque construction. |
| InvisibleDragon Diagnostic | `Diagnostic` | InvisibleDragon Diagnostic | secondary | A validation or execution diagnostic. |
| InvisibleDragon Domestic Hot Water | `DHW` | InvisibleDragon Domestic Hot Water | secondary | An InvisibleDragon domestic-hot-water system. |
| InvisibleDragon Energy Model | `Model` | InvisibleDragon Energy Model | secondary | An InvisibleDragon EnergyPlus model. |
| InvisibleDragon Energy Recovery Ventilator | `ERV` | InvisibleDragon Energy Recovery Ventilator | secondary | An InvisibleDragon energy-recovery ventilator. |
| InvisibleDragon Glazing | `Glazing` | InvisibleDragon Glazing | secondary | An InvisibleDragon transparent opening construction. |
| InvisibleDragon IDF | `IDF` | InvisibleDragon IDF | secondary | An assembled EnergyPlus IDF document. |
| InvisibleDragon Material | `Material` | InvisibleDragon Material | secondary | An InvisibleDragon opaque material. |
| InvisibleDragon Opening | `Opening` | InvisibleDragon Opening | secondary | An InvisibleDragon polygonal window, glass door, or opaque door. |
| InvisibleDragon Photovoltaic Panel | `PV` | InvisibleDragon Photovoltaic Panel | secondary | An InvisibleDragon photovoltaic panel. |
| InvisibleDragon Prepared Weather | `Weather` | InvisibleDragon Prepared Weather | secondary | A content-addressed EPW artifact prepared for InvisibleDragon execution. |
| InvisibleDragon Profile | `Profile` | InvisibleDragon Profile | secondary | An InvisibleDragon zone usage profile. |
| InvisibleDragon Schedule | `Schedule` | InvisibleDragon Schedule | secondary | An InvisibleDragon annual schedule. |
| InvisibleDragon Source System | `Source` | InvisibleDragon Source System | secondary | An InvisibleDragon HVAC source system. |
| InvisibleDragon Supply System | `Supply` | InvisibleDragon Supply System | secondary | An InvisibleDragon zone HVAC supply system. |
| InvisibleDragon Surface | `Surface` | InvisibleDragon Surface | secondary | An InvisibleDragon polygon surface. |
| InvisibleDragon Zone Definition | `Zone` | InvisibleDragon Zone Definition | secondary | An InvisibleDragon thermal zone with its owned HVAC and ventilation systems. |

### SimpleDragon

#### Category: SimpleDragon

##### Subcategory: Parameters

| Parameter | Nickname | Wire type | Exposure | Description |
| --- | --- | --- | --- | --- |
| SimpleDragon Batch Case | `Batch Case` | SimpleDragon Batch Case | secondary | One SimpleDragon GRM alternative with its optional stable batch case ID. |
| SimpleDragon Construction Layer | `Layer` | SimpleDragon Construction Layer | secondary | One material and thickness in a SimpleDragon opaque construction. |
| SimpleDragon Diagnostic | `Diagnostic` | SimpleDragon Diagnostic | secondary | A stable SimpleDragon validation or execution diagnostic. |
| SimpleDragon Fenestration | `Fenestration` | SimpleDragon Fenestration | secondary | A SimpleDragon window, glass door, or opaque door. |
| SimpleDragon Fenestration Construction | `Fenestration` | SimpleDragon Fenestration Construction | secondary | A SimpleDragon window or door construction. |
| SimpleDragon GRM | `GRM` | SimpleDragon GRM | secondary | A complete GRM 0.7 SimpleDragon model. |
| SimpleDragon GRR | `GRR` | SimpleDragon GRR | secondary | A complete GRR 0.7 SimpleDragon result. |
| SimpleDragon Material | `Material` | SimpleDragon Material | secondary | A SimpleDragon opaque material. |
| SimpleDragon Opening Definition | `Opening` | SimpleDragon Opening Definition | secondary | A geometry-backed opening connected directly to its owning SimpleDragon surface. |
| SimpleDragon Photovoltaic Panel | `PV` | SimpleDragon Photovoltaic Panel | secondary | A SimpleDragon photovoltaic panel. |
| SimpleDragon Source System | `Source` | SimpleDragon Source System | secondary | A SimpleDragon HVAC source system. |
| SimpleDragon Supply System | `Supply` | SimpleDragon Supply System | secondary | A SimpleDragon zone HVAC supply system. |
| SimpleDragon Surface | `Surface` | SimpleDragon Surface | secondary | An area-and-azimuth SimpleDragon surface. |
| SimpleDragon Surface Construction | `Construction` | SimpleDragon Surface Construction | secondary | A layered SimpleDragon opaque construction. |
| SimpleDragon Surface Definition | `Surface Definition` | SimpleDragon Surface Definition | secondary | A geometry-backed surface with its construction, boundary intent, and openings. |
| SimpleDragon Usage Profile | `Profile` | SimpleDragon Usage Profile | secondary | A SimpleDragon Korean usage profile. |
| SimpleDragon Zone | `Zone` | SimpleDragon Zone | secondary | An area-based SimpleDragon thermal zone. |
| SimpleDragon Zone Definition | `Zone Definition` | SimpleDragon Zone Definition | secondary | A SimpleDragon zone composed from its owned surfaces, usage, and HVAC inputs. |
| SimpleDragon Zone ERV | `Zone ERV` | SimpleDragon Zone ERV | secondary | An ERV owned by a SimpleDragon Zone, including its positive unit count. |

---

Reference completeness: 78 of 78 public components documented; 38 standalone typed parameters listed.

# Release notes

## 0.1.0 — Unreleased local candidate

Version 0.1.0 is an unreleased, locally buildable candidate. There is no public
0.1.0 binary, Git tag, GitHub release, or Rhino Yak publication. Local packages
and the PDF guide are development artifacts and must not be represented as a
published release.

Public distribution remains withheld while the historical upstream
standalone-license omission described in `NOTICE.md` is reviewed.

### Product highlights

- Introduces two independently installable Gonie-Gonie Grasshopper products:
  InvisibleDragon for lower-level EnergyPlus-facing authoring and SimpleDragon
  for the GRM/GRR building workflow.
- Supports Rhino 7 on .NET Framework 4.8, Rhino 8.0–8.19 on .NET 7, and Rhino
  8.20+ on .NET 8, all on Windows x64.
- Provides host-specific Yak candidates and portable packages for each product.
- Keeps installed plugins independent of Python, OODocs, Visual Studio, the
  .NET SDK, and a machine-wide EnergyPlus installation.
- Tracks the EPlusSimple/IDragon 0.7.0 engineering baseline at upstream commit
  `847b01f68f438f560a986072bcaa7768fbf67897`.

### Grasshopper-native authoring

- Adds explicit `Floor`, `Ceiling`, and `Wall` components in both products.
  There is no generic public SimpleDragon Surface component.
- Uses direct `Opening -> Surface -> Zone -> Model` ownership rather than zone
  indices, face indices, assignment passes, or user-authored relationship IDs.
- Makes each opening own its fenestration construction and each surface own its
  opaque construction, boundary condition, geometry, and opening list.
- Makes Zones own their HVAC terminals and ERVs, while Models own zones and PV.
  Plant and heat-pump sources are nested through the terminal that consumes
  them.
- Supports Grasshopper lists and DataTrees through the authoring hierarchy,
  including branch-local opening ownership and path-preserving result/batch
  outputs.
- Resolves valid coincident Outdoors surfaces across zones into reciprocal
  interzone boundaries.

### SimpleDragon workflow

- Adds packaged Korean usage-profile, construction, climate, and weather data.
- Adds strict Korean Address and `yyyy-MM-dd` Vintage inputs on `SD Model`.
- Resolves an unconnected SimpleDragon opaque Construction from Vintage,
  address-derived climate, surface type and boundary, radiant-floor state, and
  multifamily status.
- Adds direct `SD Model -> Run SimpleDragon -> GRR` execution. Weather
  selection, conversion, IDF, EnergyPlus, IDD, runtime-cache, and work paths
  remain behind the component boundary.
- Adds GRM/GRR read and deterministic write components, annual/monthly summary,
  DataTree output, default line and bar plots, deterministic CSV packages, and
  a managed path-preserving batch workflow.

### InvisibleDragon workflow

- Adds typed opaque materials, layers, constructions, glazing, windows, doors,
  explicit surfaces, profiles, zones, HVAC sources and terminals, ERVs, PV, and
  Energy Models.
- Adds `Compile InvisibleDragon`, which emits a typed EnergyPlus 24.2 IDF and
  deterministic text without an IDD-path input.
- Adds `ID Weather` as the deliberate standalone EPW boundary. It verifies a
  selected local artifact and passes an opaque content-addressed Weather value
  to `Run InvisibleDragon`.
- Adds asynchronous managed EnergyPlus execution and structured result
  summaries without executable, IDD, runtime-root, or work-directory inputs.

### Runtime and weather behavior

- Pins EnergyPlus 24.2.0 build `94a887817b` and verifies the official Windows
  archive and prepared runtime files.
- InvisibleDragon candidates carry the pinned EnergyPlus archive.
- SimpleDragon candidates carry the pinned `KoreanTMY-v1` archive and prepare
  only the address-selected EPW.
- Uses per-user LocalAppData caches and system temporary run directories rather
  than writing into Rhino installation folders.
- Cleans successful run directories after parsing and retains failed or
  cancelled directories for diagnosis.

### Execution and file-control behavior

- `Run`, `Cancel`, and managed Batch Run/Cancel require a new False-to-True
  edge. A saved True is only a baseline and does not launch work when a
  Grasshopper document opens.
- `Force Rerun` is an option for the next Run edge, not a trigger by itself.
- `Write GRM`, `Write GRR`, and `Export CSV` are level-gated: while True, each
  new solution may write. Users should reset them to False after the intended
  operation.
- SimpleDragon optional capacity inputs use a disconnected port for
  autosize/unset. Zero remains a real value unless a specific input explicitly
  documents `0 means autosize`.

### Examples and guide

- Includes eight executable Grasshopper definitions covering construction,
  envelope, profiles, ownership, HVAC/ERV/PV, full simulation, results, plots,
  CSV, and batch workflows.
- Includes two Rhino building models with named planar Breps and opening curves
  for two-zone and stepped three-zone studies.
- Includes one full process example for each product. The SimpleDragon example
  runs directly from GRM to GRR and draws a monthly graph from the GRR with
  useful defaults. The InvisibleDragon example keeps its EPW input empty until
  the user deliberately selects and verifies a file.
- Adds this PDF-only user guide with a task-oriented Workflow, exhaustive public
  component In/Out reference, Compatibility chapter, and Release Notes.

### Known limitations in this candidate

- Windows x64 only; Rhino 9 beta and macOS are not targeted.
- One pinned EnergyPlus version only.
- SimpleDragon's automatic weather workflow covers the packaged Korean
  address/climate database and does not expose a public address-prefix listing
  component or explicit EPW input.
- SimpleDragon's area-and-azimuth abstraction does not preserve arbitrary source
  vertices as model data.
- Neither product is a general editor for every EnergyPlus object or HVAC node
  graph.
- The public InvisibleDragon DHW value is not yet attachable through the current
  Zone or Model inputs and therefore is not part of the canonical executable
  workflow.
- There is no public SimpleDragon-to-InvisibleDragon conversion-preview
  component; the direct SimpleDragon runner intentionally keeps that layer
  internal.
- Public packages remain unavailable until the distribution notice is resolved.

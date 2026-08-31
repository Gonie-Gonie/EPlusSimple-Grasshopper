# Release notes

These notes describe externally relevant behavior in the current candidate.

## 0.1.0 — Unreleased local candidate

Version 0.1.0 is an unreleased, locally buildable candidate. There is no public
0.1.0 binary, Git tag, GitHub release, or Rhino Yak publication. Local packages
and the PDF guide are development artifacts and must not be represented as a
published release.

On 2026-08-31, the individual owner authorized public distribution to proceed
while accepting that written permission for the complete rights-and-notice
chain of the bundled Climate.OneBuilding TMYx weather, including Oikolab/ERA5,
Copernicus, and ASHRAE obligations, has not been verified. The recorded status
is `owner-risk-accepted-unverified`; it is not a claim of upstream permission.
The MIT code license and public support address `hyeonggon.jo@snu.ac.kr` are
confirmed.

### Product highlights

- Introduces two independently installable Gonie-Gonie Grasshopper products:
  InvisibleDragon for lower-level EnergyPlus-facing authoring and SimpleDragon
  for the GRM/GRR building workflow.
- Supports Rhino 7 on .NET Framework 4.8, Rhino 8.0–8.19 on .NET 7, and Rhino
  8.20+ on .NET 8, all on Windows x64.
- Produces host-specific Yak candidates and portable packages internally, then
  assembles one relative-path Windows Installer ZIP for the future GitHub
  release.
- Keeps installed plugins independent of Python, OODocs, Visual Studio, the
  .NET SDK, and a machine-wide EnergyPlus installation.
- Tracks the EPlusSimple/IDragon 0.7.0 engineering baseline at upstream commit
  `847b01f68f438f560a986072bcaa7768fbf67897`.

### Candidate verification matrix

| Gate | Required 0.1.0 evidence | Candidate status |
| --- | --- | --- |
| Managed implementation | All discovered unit/integration projects pass with no required skip | Passed by the verified candidate gate |
| Engineering parity | 11 paired Python/C# cases × 6 stages; zero failure or skip | Enforced; report included in candidate |
| Rhino/Grasshopper | Rhino 7 and Rhino 8 geometry, load, solve, save, and reopen | Enforced on the release host |
| Examples | Eight `.gh` definitions and two `.3dm` models, including both full workflows | Enforced on Rhino 7 and Rhino 8 |
| Packages | Invisible-only, Simple-only, and co-loaded portable candidates; no Python | Enforced for both Rhino generations |
| Documentation | Four chapters, all 75 components and 37 typed parameters, plus Food4Rhino metadata PDF postflight | Enforced as two version-bound OODocs PDFs |
| GitHub assets | Installer ZIP, user guide PDF, Food4Rhino metadata PDF, and `SHA256SUMS.txt` only | Deterministically assembled and verified locally |
| Public publication | Owner authorization separated from unverified Climate.OneBuilding/Oikolab/Copernicus/ASHRAE rights | Owner-authorized with risk accepted; no tag, GitHub Release, Yak, or Food4Rhino publication has occurred |

A candidate is distributable only when its engineering, host, example,
package, checksum, and documentation reports all belong to the same source
commit. Source code or a green headless build by itself is not a release
attestation.

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

- `Run`, `Cancel`, managed Batch Run/Cancel, `Write GRM`, `Write GRR`, and
  `Export CSV` are action inputs intended for momentary Grasshopper Buttons.
  The tracked examples wire every such input to a Button at its resting False
  value, so opening a document cannot launch work or write files.
- `Force Rerun` and `Overwrite` are persistent option Toggles. They do not
  launch an action by themselves.
- Write and Export inputs are internally level-sensitive; the required Button
  pulse prevents an accidentally enabled Toggle from writing again on later
  solutions.
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

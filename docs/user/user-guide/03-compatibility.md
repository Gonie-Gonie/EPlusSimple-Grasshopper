# Compatibility

This chapter describes compatibility guarantees and limitations relevant to plugin users.

## Supported host matrix

Version 0.1.2 targets Grasshopper on Windows x64.

| Rhino host | Plugin target selected for that host | Status |
| --- | --- | --- |
| Rhino 7 / Grasshopper | .NET Framework 4.8 (`net48`) | Supported |
| Rhino 8.0 through 8.19 / Grasshopper | .NET 7 (`net7.0-windows`) | Supported |
| Rhino 8.20 and later / Grasshopper | .NET 8 (`net8.0-windows`) | Supported |
| Rhino 9 beta | None | Not a 0.1.2 target |
| Rhino for macOS | None | Not supported |

The Rhino 8 package contains both Rhino 8 payloads so the host can select the
appropriate target. Rhino 7 and Rhino 8 may be installed on the same machine.
Each Dragon product has a host-specific Yak package and a portable package that
contains all supported Windows host variants.

InvisibleDragon and SimpleDragon are independent products built from the same
source version. They may be installed separately or together. When installed
together, use matching package versions; the shared compiled assemblies are
expected to be identical.

## Installed-plugin independence

An installed Dragon package does not require:

- Python or the repository's Python virtual environment.
- OODocs or any other Python module.
- The .NET SDK or Visual Studio.
- A machine-wide EnergyPlus installation.
- A user-supplied EnergyPlus executable or IDD path.

Rhino supplies the host runtime for the selected payload. The plugin uses
compiled .NET assemblies only. Python 3.12.7 and OODocs 1.3.0 are pinned for
repository documentation and compatibility work, not for Grasshopper use.

## EnergyPlus and weather baseline

The supported simulation runtime is EnergyPlus 24.2.0, build
`94a887817b`. InvisibleDragon packages carry the unchanged hash-pinned official
Windows runtime archive. The archive is verified before a per-user runtime is
prepared. A directory name alone is never accepted as proof that a runtime is
correct.

SimpleDragon packages carry the hash-pinned `KoreanTMY-v1` archive. Address and
Vintage select one of its Korean EPW records inside the canonical direct-run
workflow. Only the selected file is prepared and verified. A matching
InvisibleDragon installation supplies the EnergyPlus archive for an offline
first SimpleDragon run; without it, SimpleDragon may reuse a verified per-user
runtime cache or obtain the exact pinned official archive through its verified
network fallback.

Standalone InvisibleDragon intentionally requires one user-selected local EPW
through `ID Weather`. It does not infer weather from geometry or address.
EnergyPlus, IDD, runtime-cache, and temporary-work paths remain internal in both
products.

Only the pinned EnergyPlus version is supported by the 0.1.2 product. A
different machine-wide EnergyPlus installation is neither required nor used as
an interchangeable runtime.

## Historical engineering baseline

The port's historical behavior baseline is EPlusSimple/IDragon 0.7.0 at exact
upstream commit:

```text
847b01f68f438f560a986072bcaa7768fbf67897
```

Compatibility means that the compiled Grasshopper products preserve the
verified engineering meaning of that baseline for the declared scope. It does
not mean that the C# API copies Python syntax or mutable Python object behavior.

The 0.1.2 engineering scope includes:

- GRM 0.7 read/write meaning, defaults, nulls, and relationships.
- Packaged construction, profile, climate, and weather data used by supported
  workflows.
- SimpleDragon model conversion and deterministic relationship generation.
- Typed InvisibleDragon model authoring and deterministic EnergyPlus 24.2 IDF
  compilation.
- Supported geometry, openings, interzone topology, HVAC, ERV, and PV paths.
- EnergyPlus 24.2 results and GRR values for the verified workflow set.
- Rhino 7 and Rhino 8 Grasshopper persistence and package loading.

The native Grasshopper interfaces intentionally use typed, immutable values and
ownership wires. Generated IDs may differ as text from historical in-memory
addresses; their definitions and reference topology remain significant.

The following are not compatibility promises for the Grasshopper products:

- Importing or calling the historical Python packages.
- Historical Python CLI output or commands.
- Excel/GREXCEL conversion and execution.
- pandas/DataFrame-linked mutation APIs.
- Python regex, callable, list-indexing, mutable-container, `shrink`, or
  `quick_map` behavior.
- A general editor for every EnergyPlus object, field, node, branch, or plant
  loop.

## Verified compatibility coverage

The 0.1.2 release was checked with 11 paired Python/C# engineering cases and
66 declared stages covering GRM cross-read, authored and expanded IDF,
EnergyPlus 24.2 execution, GRR values, and diagnostics. The host gates also
load, solve, save, and reopen the public components and examples in Rhino 7 and
Rhino 8. Package gates exercise InvisibleDragon alone, SimpleDragon alone, and
both products together.

These checks support the engineering behavior described here; they do not
promise historical Python import, call, container, CLI, Excel, pandas, or regex
compatibility or perform a publication. The release-notes and NOTICE chapters
record the current publication state and runtime-data provenance.

## Geometry abstraction and provenance

InvisibleDragon keeps authored planar vertices. SimpleDragon deliberately
reduces surfaces to its area, azimuth, height, boundary, construction, and
opening abstraction before generating deterministic InvisibleDragon geometry.
Consequently a converted face is valid simulation geometry, not a promise to
reproduce every original Rhino vertex.

The typed GRM and GRR Grasshopper values privately preserve generated entity
identity and source Rhino object/face provenance where available.
`geometry_map.csv` receives this context automatically and carries the entity,
topology indices, Rhino object, geometry fingerprint, and Grasshopper tree
path/index; `diagnostics.csv` additionally carries a Brep face index when the
diagnostic has one. Standalone GRM/GRR files intentionally do not store Rhino
session provenance. Users never supply these identifiers as relationship or
export inputs.

The InvisibleDragon Version component exposes its loaded product version and
pinned upstream commit. SimpleDragon product/core versions and run identity are
recorded in its CSV and batch manifests. Release manifests additionally bind
EnergyPlus executable, IDD, ExpandObjects, weather archive/file, shared
assembly, and package SHA-256 values.

## Product boundary compatibility

SimpleDragon owns the GRM, GRR, and SimpleDragon diagnostic values visible on
its canvas. Its direct runner performs conversion, IDF generation, weather
selection, and EnergyPlus execution internally. It does not require or expose
an InvisibleDragon Grasshopper type between `SD Model` and `Run SimpleDragon`.

The standalone InvisibleDragon boundary is intentionally different: the user
authors an `Energy Model`, compiles it to an IDF, verifies one local EPW with
`ID Weather`, and sends the IDF and opaque Weather value to
`Run InvisibleDragon`.

GRM and GRR files target the supported 0.7 schema semantics. Deterministic
writes make equivalent authored content stable, but a C# Grasshopper definition
is not a Python source-compatibility artifact.

## Known limitations

- Windows x64 is the only supported platform.
- Rhino 9 beta and Rhino for macOS are not 0.1.2 targets.
- Only EnergyPlus 24.2.0 build `94a887817b` is supported.
- SimpleDragon's packaged weather workflow is Korean-address based. An address
  must begin with a supported administrative-area prefix and Vintage must fall
  within that climate record's effective-date coverage. There is currently no
  public component that lists every supported address prefix.
- The canonical SimpleDragon direct runner does not accept an explicit EPW. Use
  standalone InvisibleDragon when user-selected weather is required.
- SimpleDragon converts Rhino surface geometry to its area-and-azimuth
  abstraction. Arbitrary original source vertices are therefore not preserved
  as a SimpleDragon model guarantee.
- Supported planar geometry is polygonal. Curved, non-planar, zero-area, and
  multi-face surface inputs are outside the authored workflow.
- Neither product exposes every EnergyPlus object or a full HVAC node/branch
  graph editor.
- `Domestic Hot Water` currently creates a typed InvisibleDragon value, but the
  public `Thermal Zone` and `Energy Model` inputs do not currently attach that
  value to the canonical executable model graph. Do not treat a standalone DHW
  component as simulated equipment in 0.1.2.
- `Run SimpleDragon` and `Run InvisibleDragon` each accept one data-matched
  simulation per component. SimpleDragon provides a separate managed batch
  workflow; use one runner per simulation for standalone InvisibleDragon.
- Successful managed runs clean their work directories. Failed and cancelled
  run directories are retained for diagnosis and can consume temporary storage
  until removed.
- SimpleDragon's automatic weather workflow uses the bundled, hash-verified
  Korean TMYx archive sourced from Climate.OneBuilding.

Review the release-notes chapter for the current distribution status before
sharing a package.

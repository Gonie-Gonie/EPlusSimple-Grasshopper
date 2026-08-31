# Compatibility

## Supported host matrix

Version 0.1.0 targets Grasshopper on Windows x64.

| Rhino host | Plugin target selected for that host | Status |
| --- | --- | --- |
| Rhino 7 / Grasshopper | .NET Framework 4.8 (`net48`) | Supported |
| Rhino 8.0 through 8.19 / Grasshopper | .NET 7 (`net7.0-windows`) | Supported |
| Rhino 8.20 and later / Grasshopper | .NET 8 (`net8.0-windows`) | Supported |
| Rhino 9 beta | None | Not a 0.1.0 target |
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

Only the pinned EnergyPlus version is supported by the 0.1.0 product. A
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

The 0.1.0 engineering scope includes:

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

## Engineering compatibility gate

Run `dev.cmd compatibility` to execute the paired Python/C# engineering gate.
The tracked manifest declares exactly 11 cases. Every case must execute these
six stages without a skip:

1. GRM cross-read.
2. Authoring IDF comparison.
3. Expanded IDF comparison.
4. EnergyPlus 24.2.0 execution.
5. GRR comparison.
6. Warning, Severe, and Fatal comparison.

Eight cases use the pinned Chicago weather record. The packaged ERV/PV/opening
case is additionally repeated with Tampa, Golden, and San Francisco EPWs. The
matrix covers adjacency, shared heat pumps, screw and absorption chillers,
cooling towers, geothermal AHU, packaged equipment, boilers, district heat,
FCU, radiators, radiant systems, DHW, ERV, PV, multiple fuels, and openings.
The resulting 11-case/66-stage report is written to
`artifacts/reports/engineering-compatibility.json`.

Numeric comparison uses:

```text
|C# - Python| <= absolute_tolerance
                + relative_tolerance * max(|C#|, |Python|)
```

The tracked case manifest sets authoring/expanded IDF numeric tolerances to
absolute and relative `1e-9`. GRR values use absolute `0.01` and relative
`0.001` under the same formula. Its `0.005` near-zero value floors the
denominator used to report relative error and is the threshold that explicitly
designated non-zero result totals must exceed; it does not replace the pass/fail
formula. Warning-count delta is zero. A matching non-zero Severe or
Fatal result is still a failure unless the exact normalized diagnostic and
count belong to a reviewed exception. The report records the maximum absolute
error, the relative error at that same JSON path, and the path itself rather
than hiding the comparison behind a pass flag.

`-AllowDifferences` is a diagnostic development mode and is never release
evidence. A verified release requires all 11 cases, all 66 stages, zero failed
cases, zero skipped stages, and no unreviewed difference.

## Compatibility evidence and exceptions

The upstream public-symbol inventory contains 1,242 ordered symbols. Each row
must be classified as `equivalent`, `exception`, or `out_of_scope`; the release
gate rejects `needs_reverification`. Symbol evidence binds the exact upstream
commit and symbol hash, production C# file/symbol/hash, verifying test
file/symbol/hash, assertion ID, and deterministic expected output. Fixture-backed
receipts additionally record their fixture and generator hashes. Broad
file-level mappings are not equivalence claims.

Intentional native adaptations live in
`upstream/compatibility-exceptions.yml`. Each exception identifies the exact
upstream symbol, native behavior, engineering/IDF/result effect, evidence, and
approval. Generated-name differences may be canonicalized only as a one-to-one
mapping that preserves every definition and reference; missing, swapped,
merged, or dangling relationships fail the gate.

The data parity suite separately covers all 24 packaged usage profiles through
their final schedules, every surface-regulation branch, every fenestration key,
all 252 weather rows, and climate effective-date boundaries. Runtime, EPW, GRM,
and source inputs are content-addressed before either engine runs.

## Geometry abstraction and provenance

InvisibleDragon keeps authored planar vertices. SimpleDragon deliberately
reduces surfaces to its area, azimuth, height, boundary, construction, and
opening abstraction before generating deterministic InvisibleDragon geometry.
Consequently a converted face is valid simulation geometry, not a promise to
reproduce every original Rhino vertex.

Structured Geometry Map outputs preserve the generated entity identity and
source Rhino object/face provenance where available. `geometry_map.csv` carries
the entity, topology indices, Rhino object, geometry fingerprint, and
Grasshopper tree path/index; `diagnostics.csv` additionally carries a Brep face
index when the diagnostic has one. Use these values to trace a result back to
source geometry; users never supply them as relationship inputs.

The two Version components expose the product version and pinned upstream
commit. CSV/batch manifests record the product/core versions and run identity;
release manifests additionally bind EnergyPlus executable, IDD, ExpandObjects,
weather archive/file, shared assembly, and package SHA-256 values.

## Maintainer upstream synchronization

The scheduled/manual upstream workflow detects source and data drift but never
auto-merges Python code. To accept a new baseline:

1. Create `sync/simpledragon-upstream-<short-sha>` from current `main`.
2. Collect the upstream diff and symbol-hash report without changing the lock.
3. Add or update Python behavioral fixtures for every affected branch.
4. Port the behavior in C# and update its symbol/test mapping.
5. Run unit, semantic authoring/expanded IDF, and numerical EnergyPlus gates.
6. Review every intentional difference in the exception registry.
7. Require zero unmapped symbols and zero `needs_reverification` rows.
8. Update the pinned upstream lock only after all evidence passes, then review
   and merge the sync branch.

Never update the compatibility lock merely to silence a drift report.

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
- Rhino 9 beta and Rhino for macOS are not 0.1.0 targets.
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
  component as simulated equipment in 0.1.0.
- `Run SimpleDragon` and `Run InvisibleDragon` each accept one data-matched
  simulation per component. SimpleDragon provides a separate managed batch
  workflow; use one runner per simulation for standalone InvisibleDragon.
- Successful managed runs clean their work directories. Failed and cancelled
  run directories are retained for diagnosis and can consume temporary storage
  until removed.
- Public binary distribution remains withheld while the historical upstream
  standalone-license omission recorded in `NOTICE.md` is reviewed. Repository
  source licensing does not by itself authorize a public Dragon binary release.

Review the release-notes chapter for the current distribution status before
sharing a package.

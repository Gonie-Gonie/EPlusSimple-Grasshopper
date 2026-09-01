# Compatibility policy

This document is a maintainer contract for port scope, evidence, and release
verification. User-facing host support and limitations are documented in the
[public compatibility chapter](../user/user-guide/03-compatibility.md).

## Host matrix

| Host | Target | Status |
|---|---|---|
| Rhino 7 / Grasshopper | .NET Framework 4.8 (`net48`) | Supported |
| Rhino 8.0–8.19 / Grasshopper | .NET 7 (`net7.0-windows`) | Supported |
| Rhino 8.20+ / Grasshopper | .NET 8 (`net8.0-windows`) | Supported |
| Rhino 9 beta, macOS | — | Not a 0.1.2 target |

Both Dragon GHAs are built from the same commit and may be loaded together.
SimpleDragon owns its Grasshopper model, result, and diagnostic types. Its
direct runner keeps InvisibleDragon conversion, IDF, weather, and EnergyPlus
execution values behind the `SD Model -> Run SimpleDragon -> GRR` boundary, so
the SimpleDragon GHA does not depend on InvisibleDragon Grasshopper types.

## Historical baseline

The port tracks EPlusSimple/IDragon 0.7.0 at the exact repository commit in
`upstream/upstream.lock.json`. Historical names and paths are retained only in
provenance, symbol maps, fixtures, and compatibility reports. The public
products, namespaces, packages, schemas, and owned directory structure use
InvisibleDragon and SimpleDragon identities.

Compatibility is assessed at several levels:

1. Deterministic database, GRM, and serialization fixtures.
2. Algorithm and default-value comparisons against the Python 3.12 oracle.
3. Semantic IDF object/field comparison rather than byte layout alone.
4. Real EnergyPlus 24.2.0 validity and numerical result tolerances.
5. Rhino geometry conversion and real Grasshopper host load/save/reopen gates.
6. Package-only, dual-package, shared-assembly, and no-Python checks.

The paired engineering gate is `dev.cmd compatibility`. Every case records the GRM SHA-256 and
the EPW runtime-relative path, SHA-256, and `LOCATION` header receipt in
`fixtures/compatibility/cases.json`; both engines reject changed inputs before model
generation. The gate also fixes the upstream commit and EnergyPlus executable/IDD/ExpandObjects
hashes. For every case, the C# writer emits a deterministic GRM that the pinned Python 0.7.0
reader must accept and convert to the same semantic IDF. The gate then compares authoring and
expanded IDF separately and records path-level GRR numeric errors and warning differences in
`artifacts/reports/engineering-compatibility.json`.
`-AllowDifferences` is a development-only reporting mode and is never evidence of compatibility.

Every push and pull request runs the compatibility reporter regression suite and the same strict
eleven-case, sixty-six-stage paired EnergyPlus gate on Windows. It retains all eight Chicago cases
and reruns `packaged-erv-pv-openings` with Tampa, Golden, and San Francisco weather. CI prepares
the hash-pinned EnergyPlus 24.2 runtime,
IDD, and ExpandObjects through `dev.cmd setup`; the gate then requires and hash-checks its pinned
EPW before either engine runs. An immutable Actions cache avoids repeated runtime downloads, while
setup still revalidates restored runtime files. The oracle job reuses its single reference
preparation and restores only the compatibility runner project graph. Any available engineering
report and the setup, oracle, and compatibility diagnostic logs are retained even when a gate
fails. Release-candidate runs repeat the reporter regression before their expensive bootstrap and
retain the same diagnostics independently of the complete package artifact.

EPW payloads remain outside source control and reports. Setup verifies the pinned KoreanTMY ZIP,
and SimpleDragon package candidates embed that archive unchanged; directly expanded EPW files
remain forbidden. InvisibleDragon candidates likewise embed the unchanged pinned official
EnergyPlus archive rather than directly expanded runtime files.

Numeric comparisons use `|a-b| <= absolute + relative * max(|a|, |b|)`.
The tracked case manifest is authoritative: IDF fields use absolute and relative
`1e-9`; GRR values use absolute `0.01` and relative `0.001`; values whose
magnitude is at most `0.005` use the near-zero rule. Warning-count delta is
exactly zero. Matching non-zero `Severe` or `Fatal` diagnostics fail the gate
unless the exact normalized title and count are pinned to a reviewed exception;
equal failure alone is not compatibility evidence. The report records the
maximum observed error and its JSON path, so a tolerance pass cannot hide where
the largest difference occurred. Cases with deliberately limited evidence also
publish limitation and diagnostic-exception counts and IDs.

The data parity suite exhaustively checks all 24 pinned usage profiles through their final
legacy `Schedule:Compact` fields, every surface-regulation branch, every fenestration key, all
252 weather rows, and climate effective-date boundaries. `dev.cmd examples` separately solves,
saves, and reopens eight tracked Grasshopper definitions and validates two Rhino building models
inside both Rhino 7 and Rhino 8. Its full-workflow definition also executes the gated EnergyPlus,
Result, GRR, CSV, cache/cancellation, and batch paths when a verified runtime and EPW are available.
Known authoring quirks retained for exact upstream parity are
listed in `upstream/compatibility-exceptions.yml`; they remain visible as warnings and are not
silently removed from the emitted IDF.

A broad class or object name in the port map does not by itself assert complete
behavioral parity. Review the tracked compatibility exceptions, generated
engineering report, and release notes for the exact verified matrix.

## Declared product compatibility scope

"Compatible" in this repository means engineering compatibility for the two
compiled Grasshopper products. It does not mean Python source, import, or call
syntax compatibility. The release gate may claim compatibility only for rows
that have symbol-level evidence or a reviewed exception.

| In the 0.1.2 engineering scope | Outside the Grasshopper product scope |
|---|---|
| GRM 0.7.0 read/write semantics, defaults, nulls, and references | Python package import and function-call syntax |
| Pinned construction, profile, climate, and weather data/query results | Excel/GREXCEL input conversion and execution |
| EPlusSimple-to-IDragon model conversion and deterministic identities | Original Python CLI commands and console formatting |
| Typed model authoring and deterministic EnergyPlus 24.2 IDF compilation | pandas/DataFrame-linked IDF mutation APIs |
| Expanded IDF meaning, reference topology, and geometry | regex/callable/list Python indexing behavior |
| EnergyPlus 24.2.0 results, GRR values, and warnings within declared tolerances | Python mutable-container behavior, `shrink`, and `quick_map` syntax |
| Rhino 7/8 geometry adapters, Grasshopper persistence, and packaged workflows | General editing support for every EnergyPlus object |

Generated address tokens are nondeterministic implementation details, but their graph is not ignored.
IDF comparison permits a template-scoped one-to-one token rename only when the same mapping is used
by each defining name and reference. Reference swaps, alias merges, and dangling identities therefore
remain release failures even when object order and the raw address text differ.

The C# APIs and Grasshopper components are native product interfaces. They may
be more immutable, deterministic, or strongly typed than the historical Python
objects while preserving the verified model meaning. Any such difference that
affects authoring text, expanded IDF, warnings, or results must be registered in
`upstream/compatibility-exceptions.yml`; otherwise it is a release failure.

Active absorption parity is exercised by
`absorption-default-explicit-electric-radiant`: a 10 W/m² internal lighting load
dispatches two 250 W absorption chillers with natural-gas and oil hot-water
generators. The pinned Python and C# paths both complete the annual Chicago run
with 27 warnings, no Severe or Fatal diagnostics, and matching non-zero GRR
values for cooling electricity, natural-gas heating, and oil heating. The
bounded capacities make this a deterministic compatibility fixture, not an
equipment-sizing recommendation. Its declared `grr_expectations` fail closed if
either engine returns a zero active-load result.

## Current limitations

- Windows is the only supported operating system.
- One pinned EnergyPlus version is supported.
- SimpleDragon candidates include the hash-pinned KoreanTMY archive and select
  weather internally from Address and Vintage. SimpleDragon exposes no EPW
  override; explicit EPW selection belongs only to standalone InvisibleDragon.
- Neither module exposes every EnergyPlus object or a full HVAC node/branch
  graph editor.
- SimpleDragon intentionally loses arbitrary source vertices during its
  area-and-azimuth abstraction.
- SimpleDragon includes the exact hash-pinned Korean TMYx archive sourced from
  Climate.OneBuilding and selects weather internally from Address and Vintage.

The repository's own code is offered under MIT and the public support address
is confirmed. Runtime and weather provenance remain recorded in `NOTICE.md`.

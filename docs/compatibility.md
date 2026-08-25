# Compatibility

## Host matrix

| Host | Target | Status |
|---|---|---|
| Rhino 7 / Grasshopper | .NET Framework 4.8 (`net48`) | Supported |
| Rhino 8.0–8.19 / Grasshopper | .NET 7 (`net7.0-windows`) | Supported |
| Rhino 8.20+ / Grasshopper | .NET 8 (`net8.0-windows`) | Supported |
| Rhino 9 beta, macOS | — | Not a 0.1.0 target |

Both Dragon GHAs are built from the same commit and may be loaded together.
SimpleDragon's converted model, IDF, EnergyPlus result, and diagnostic values
use the same shared type identities as InvisibleDragon.

## Historical baseline

The port tracks EPlusSimple/IDragon 0.7.0 at the exact repository commit in
`upstream/upstream.lock.json`. Historical names and paths are retained only in
provenance, symbol maps, fixtures, and compatibility reports. The Gonie-Gonie
products, namespaces, packages, schemas, and owned directory structure are
InvisibleDragon and SimpleDragon.

Compatibility is assessed at several levels:

1. Deterministic database, GRM, and serialization fixtures.
2. Algorithm and default-value comparisons against the Python 3.12 oracle.
3. Semantic IDF object/field comparison rather than byte layout alone.
4. Real EnergyPlus 24.2.0 validity and numerical result tolerances.
5. Rhino geometry conversion and real Grasshopper host load/save/reopen gates.
6. Package-only, dual-package, shared-assembly, and no-Python checks.

The paired engineering gate is `dev.cmd compatibility`. It fixes the GRM, EPW bytes, upstream
commit, and EnergyPlus executable/IDD/ExpandObjects hashes for both engines; compares authoring
and expanded IDF separately; and records path-level GRR numeric errors and warning differences in
`artifacts/reports/engineering-compatibility.json`. `-AllowDifferences` is a development-only
reporting mode and is never evidence of compatibility.

A broad class or object name in the port map does not by itself assert complete
behavioral parity. Review `upstream/reports`, compatibility exceptions, and the
release notes for the exact verified matrix.

## Current limitations

- Windows is the only supported operating system.
- One pinned EnergyPlus version is supported.
- Weather files are user-supplied and never redistributed.
- Neither module exposes every EnergyPlus object or a full HVAC node/branch
  graph editor.
- SimpleDragon intentionally loses arbitrary source vertices during its
  area-and-azimuth abstraction.
- Public binary publication remains blocked until the historical upstream
  license omission recorded in `NOTICE.md` is resolved or release counsel
  confirms the required attribution basis.

The repository's own code is offered under its `LICENSE`; that statement does
not erase third-party provenance or the release checks in `NOTICE.md`.

# Release checklist

This checklist is for maintainers preparing the first local InvisibleDragon and
SimpleDragon 0.1.0 candidate for Rhino 7+. Building a candidate does not
authorize publication, Yak upload, tag creation, or a GitHub release.

## Source and provenance

- Work from a clean `main` commit already pushed to `origin/main`.
- Confirm the package version, assembly version, and both product manifests.
- Validate the upstream lock, symbol map, compatibility exceptions, and Python
  reference fixtures.
- Recheck `LICENSE` and `NOTICE.md`. Resolve the recorded historical upstream
  standalone-license omission before public binary release.
- Confirm that SimpleDragon contains only the exact hash-pinned KoreanTMY
  archive, no expanded or arbitrary EPW files, and that redistribution rights
  are resolved before any public binary release.
- Require both Rhino 7 and Rhino 8 for the complete release gate. A normal
  developer build may skip tests for a missing Rhino generation, but a release
  candidate may not.

## Reproducibility and behavior

The supported one-command local candidate gate is:

```text
.\dev.cmd release
```

It verifies the clean branch and live `origin/main`, prepares the pinned local
environment, runs the full sequence below, and publishes the verified report
set atomically to `artifacts\release`. A failed rerun leaves no partially
published candidate there.

For diagnosis, its constituent commands are:

```text
.\dev.cmd setup -InstallEnergyPlus -RequireEnergyPlus -RequireRhino7 -RequireRhino8
.\dev.cmd reference -Mode Verify
.\dev.cmd upstream compatibility-gate --source-root temp/reference/upstream/eplussimple --collect-evidence
.\dev.cmd build -NoRestore -RequireEnergyPlus
.\dev.cmd compatibility -SkipReferencePreparation -NoRestore
.\dev.cmd examples -SkipPluginBuild -RequireEnergyPlusWorkflow
.\dev.cmd package -SkipBuild -RunPortableHostGate
```

- Require zero compiler warnings and errors for `net48`, `net7.0-windows`, and
  `net8.0-windows`.
- Require all 1,242 pinned upstream public symbols to have exact registry
  coverage, no `needs_reverification` rows, and fresh authoritative assertion
  evidence for every `equivalent` or `exception` row. External evidence JSON
  cannot satisfy this release gate.
- Require the upstream report to contain no duplicate or case-ambiguous JSON
  object keys, real JSON booleans (never truthy strings), equal nonzero
  required/collected assertion counts, empty evidence failure arrays, and
  classification counts that total exactly 1,242. The release copies the
  already-validated report bytes atomically and attests that exact byte hash.
  It also reconciles each authoritative session's positive project, assertion,
  and indexed-artifact counts and validates the request, child result, receipt,
  and index against the repository HEAD, upstream manifests,
  `net8.0-windows`, and the pinned SDK toolchain. Every indexed build prop,
  stdout/stderr stream, TRX, test/implementation DLL, and evidence record is
  read once with path/reparse/hardlink checks and copied byte-exactly beneath
  `artifacts\release\trusted-evidence\<session>\artifacts`; the receipt and
  index are copied beside it and all files enter the final checksum inventory.
  Receipt/index q/z hashes must match the actual held bytes. The tracked and
  isolated symbol-evidence manifests, report/request/child assertion ids, exact
  receipt output/load/test fields, zero project exits, request commands/graphs,
  parent-bound `g2` descriptor, and recomputed EvidenceResults hash must all
  agree before packaging.
- Require all managed tests, real EnergyPlus integration, Rhino geometry, and
  Grasshopper save/reopen gates applicable to the machine.
- Require every declared Python/C# engineering compatibility case to pass with
  zero skipped stages, including authoring/expanded IDF, EnergyPlus, GRR, and
  warning comparisons declared by that case.
- Require the engineering report to bind the exact eleven cases and their exact six
  stages to the clean release HEAD, the complete declared production C# source
  set, and the five `net8.0-windows` Release assemblies actually executed by
  the compatibility runner. The release gate recomputes every file and
  aggregate SHA-256 and rejects dirty, stale, omitted, or substituted inputs.
  This runner executes Core and Runtime assemblies directly; it must not claim
  that a Grasshopper GHA was exercised by this evidence.
- Require 66 executed stage receipts with no skips: all eight Chicago cases plus
  `packaged-erv-pv-openings` under the pinned Tampa, Golden, and San Francisco
  EPWs. Track only runtime-relative paths, SHA-256 values, and `LOCATION` header
  receipts; weather payloads must not enter release artifacts.
- Require zero unregistered `Severe` and `Fatal` EnergyPlus diagnostics in both
  engines. An allowed diagnostic must match its normalized title and count
  exactly, and every diagnostic or `not_verified` limitation must reference an
  approved compatibility exception.
- Run package-host scenarios from safely extracted portable archives in Rhino
  7 and Rhino 8: InvisibleDragon-only, SimpleDragon-only, and both.
- Require both genuine starter definitions to solve, save, reopen, preserve
  their real wires and typed construction values, and round-trip without
  structural drift in Rhino 7 and Rhino 8.
- Confirm the package verifier reports correct Yak tags, framework layout,
  shared DLL hashes, component interoperability, the exact product-exclusive
  EnergyPlus/KoreanTMY archive pins, and no Python/Rhino SDK or directly
  expanded runtime/weather payload leakage.
- Confirm all generated output and logs are under `artifacts` or `temp`.

## Candidate review

- Inspect `artifacts\release\release-gate.json`, its checksum inventory, the
  copied `upstream-compatibility-gate.json` and
  complete `trusted-evidence\<session-id>` bundle (receipt, index, and indexed
  `artifacts` tree),
  `engineering-compatibility.json`, package index and compatibility report,
  build/test reports, and all six copied real-host summaries.
- Open the tracked InvisibleDragon and SimpleDragon starter definitions in each
  installed Rhino generation. Separately exercise a direct InvisibleDragon
  HVAC graph and a SimpleDragon HVAC-to-InvisibleDragon conversion using the
  recipes in `examples\README.md`.
- Verify explicit Run/Prepare trigger behavior, cancellation, cache reuse,
  address-selected packaged EPW handling plus explicit overrides, CSV schema,
  and saved Goo persistence.
- Review release notes for exact supported and intentionally unsupported scope.

## Publication

`.\dev.cmd release` creates local evidence only. It never creates a tag, GitHub
release, plugin installation, or Yak publication.

The manually dispatched `Build verified local release candidate` workflow runs
that same authoritative `dev.cmd release` gate and uploads its local candidate
and evidence as workflow artifacts. It does not react to tags and does not
create a GitHub release or publish to Yak. Its self-hosted Windows x64 runner
must carry the `rhino7`, `rhino8`, `energyplus-24-2`, and `dragons-release`
labels and have licensed Rhino 7 and Rhino 8 installations available to the
interactive host gates. The gate still verifies the pinned EnergyPlus runtime
and every required tool rather than trusting runner labels alone.

Do not push an `invisible-dragon-vX.Y.Z` or `simple-dragon-vX.Y.Z` tag, upload a
binary, create or publish a GitHub release, or publish to Yak while the
historical upstream standalone-license omission recorded in `NOTICE.md` remains
under review. After that review is explicitly resolved, any tag, GitHub
release, binary upload, or Yak publication remains a distinct, explicitly
authorized manual operation outside this candidate workflow.

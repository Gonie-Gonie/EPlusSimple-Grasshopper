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
- Confirm that no weather payload or unlicensed EPW is present.
- Require both Rhino 7 and Rhino 8 for the complete release gate. A normal
  developer build may skip tests for a missing Rhino generation, but a release
  candidate may not.

## Reproducibility and behavior

The supported one-command local candidate gate is:

```text
dev.cmd release
```

It verifies the clean branch and live `origin/main`, prepares the pinned local
environment, runs the full sequence below, and publishes the verified report
set atomically to `artifacts\release`. A failed rerun leaves no partially
published candidate there.

For diagnosis, its constituent commands are:

```text
dev.cmd setup -InstallEnergyPlus -RequireEnergyPlus -RequireRhino7 -RequireRhino8
dev.cmd reference -Mode Verify
dev.cmd build -NoRestore -RequireEnergyPlus
dev.cmd examples -SkipPluginBuild
dev.cmd package -SkipBuild -RunPortableHostGate
```

- Require zero compiler warnings and errors for `net48`, `net7.0-windows`, and
  `net8.0-windows`.
- Require all managed tests, real EnergyPlus integration, Rhino geometry, and
  Grasshopper save/reopen gates applicable to the machine.
- Run package-host scenarios from safely extracted portable archives in Rhino
  7 and Rhino 8: InvisibleDragon-only, SimpleDragon-only, and both.
- Require both genuine starter definitions to solve, save, reopen, preserve
  their real wires and typed construction values, and round-trip without
  structural drift in Rhino 7 and Rhino 8.
- Confirm the package verifier reports correct Yak tags, framework layout,
  shared DLL hashes, component interoperability, and no Python/Rhino SDK/runtime
  payload leakage.
- Confirm all generated output and logs are under `artifacts` or `temp`.

## Candidate review

- Inspect `artifacts\release\release-gate.json`, its checksum inventory, the
  package index and compatibility report, build/test reports, and all six
  copied real-host summaries.
- Open the tracked InvisibleDragon and SimpleDragon starter definitions in each
  installed Rhino generation. Separately exercise a direct InvisibleDragon
  HVAC graph and a SimpleDragon HVAC-to-InvisibleDragon conversion using the
  recipes in `examples\README.md`.
- Verify explicit Run/Prepare trigger behavior, cancellation, cache reuse,
  user-supplied EPW handling, CSV schema, and saved Goo persistence.
- Review release notes for exact supported and intentionally unsupported scope.

## Publication

`dev.cmd release` creates local evidence only. It never creates a tag, GitHub
release, plugin installation, or Yak publication.

Do not push an `invisible-dragon-vX.Y.Z` or `simple-dragon-vX.Y.Z` tag, upload a
binary, create or publish a GitHub release, or publish to Yak while the
historical upstream standalone-license omission recorded in `NOTICE.md` remains
under review. After that review is explicitly resolved, a matching tag may
start the attested workflow and create a GitHub **draft** release for separate
inspection. Yak publication remains a distinct, explicitly authorized manual
operation.

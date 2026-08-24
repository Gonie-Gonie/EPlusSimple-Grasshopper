# Release checklist

This checklist is for maintainers. Building a candidate does not authorize
publication, Yak upload, tag creation, or a public GitHub release.

## Source and provenance

- Work from a clean `main` commit already pushed to `origin/main`.
- Confirm the package version, assembly version, and both product manifests.
- Validate the upstream lock, symbol map, compatibility exceptions, and Python
  reference fixtures.
- Recheck `LICENSE` and `NOTICE.md`. Resolve the recorded historical upstream
  standalone-license omission before public binary release.
- Confirm that no weather payload or unlicensed EPW is present.

## Reproducibility and behavior

```text
setup.cmd -InstallEnergyPlus -RequireEnergyPlus
reference.cmd -Mode Verify
build.cmd -NoRestore -RequireEnergyPlus
package.cmd -SkipBuild -NoRestore
tools\example-definitions\run.cmd -SkipPluginBuild
```

- Require zero compiler warnings and errors for `net48`, `net7.0-windows`, and
  `net8.0-windows`.
- Require all managed tests, real EnergyPlus integration, Rhino geometry, and
  Grasshopper save/reopen gates applicable to the machine.
- Run package-host scenarios from safely extracted portable archives in Rhino
  7 and Rhino 8: InvisibleDragon-only, SimpleDragon-only, and both.
- Confirm the package verifier reports correct Yak tags, framework layout,
  shared DLL hashes, component interoperability, and no Python/Rhino SDK/runtime
  payload leakage.
- Confirm all generated output and logs are under `artifacts` or `temp`.

## Candidate review

- Inspect `package-index.json`, compatibility report, SHA-256 inventory, build
  manifest, test report, and real-host summaries.
- Open at least one authored HVAC definition and one existing GRM workflow in
  each installed Rhino generation.
- Verify explicit Run/Prepare trigger behavior, cancellation, cache reuse,
  user-supplied EPW handling, CSV schema, and saved Goo persistence.
- Review release notes for exact supported and intentionally unsupported scope.

## Publication

Pushing `invisible-dragon-vX.Y.Z` or `simple-dragon-vX.Y.Z` starts the attested
release-candidate workflow and creates a GitHub **draft** release only. Inspect
that draft and its provenance before making it public. Yak publication is a
separate, explicitly authorized operation and is never performed by repository
build or package scripts.

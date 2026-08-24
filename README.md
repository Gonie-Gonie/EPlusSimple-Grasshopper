# Dragons-Grasshopper

This Gonie-Gonie repository ports the upstream EPlusSimple 0.7.0 project and
its IDragon layer to two independently installable Rhino 7+ / Grasshopper
plugins written in C#. The public product names in this port are:

- `GonieGonie.InvisibleDragon.GH` preserves planar polygon vertices and builds
  EnergyPlus models.
- `GonieGonie.SimpleDragon.GH` preserves the upstream area-and-azimuth
  abstraction and uses `GreenRetrofitModel.ToInvisibleDragon()` as its
  simulation conversion path.

The tracked upstream source is pinned in
[`upstream/upstream.lock.json`](upstream/upstream.lock.json). Python is used only
as a development oracle; released plugins do not require Python.

## Supported baseline

| Dependency | Pinned baseline |
|---|---|
| Windows | Windows 11 x64 |
| Rhino 7 / Grasshopper | Rhino 7.0+, `net48` plugin target |
| Rhino 8 / Grasshopper | Rhino 8.0+, `net7.0-windows` and `net8.0-windows` plugin targets |
| .NET SDK | 8.0.424 |
| C# | 12 |
| EnergyPlus | 24.2.0, build `94a887817b` |
| Python oracle | 3.12.7 |

Rhino itself is licensed software and is never installed by repository scripts.
The SDK packages are sufficient for a headless compile. Rhino 7 and Rhino 8 are
detected independently; each installed version enables only its own viewport,
Grasshopper load, geometry, and document tests.

## Quick start

From a Windows command prompt or PowerShell:

```text
setup.cmd
build.cmd
```

`setup.cmd` finds the exact SDK from `global.json` or installs it under
`.tools\dotnet`, selects exact Python 3.12.7 (installing the official embeddable
package locally when needed), validates Rhino 7 and Rhino 8 independently, and
writes the generated, non-secret `.config\local.settings.json`. It is
idempotent, so rerun it after installing Rhino to enable that version's tests.

For port-equivalence work, `reference.cmd` runs the pinned historical Python
implementation in an isolated dependency directory and writes deterministic
database, GRM, and semantic IDF references under `temp\reference`. Use
`reference.cmd -Mode Verify` to compare them byte-for-byte with the reviewed
baseline in `fixtures\reference\python-0.7.0`. Python remains development-only.

EnergyPlus uses a deliberate detect-only default. Setup validates the corrected
24.2.0 build `94a887817b` at `C:\EnergyPlusV24-2-0` or under `.tools` by the
executable, IDD, and ExpandObjects SHA-256 values. To download and verify the
official portable ZIP when no compatible runtime exists, run:

```text
setup.cmd -InstallEnergyPlus
```

Use `setup.cmd -WhatIf -SkipRestore` for a no-write preview. `build.cmd`
performs restore, compile, and tests with the setup-selected SDK, then stages
all runtime variants under `artifacts\<module>\rhino7\net48`,
`artifacts\<module>\rhino8\net7.0`, and
`artifacts\<module>\rhino8\net8.0`. Rhino-dependent tests are skipped only for the
missing Rhino version; headless projects continue to build.

All disposable downloads, build intermediates, test runs, logs, and simulations
are routed under `temp`; NuGet packages and reusable local toolchains are under
`.tools`. Run `scripts\clean.ps1` to safely remove only `temp` and generated
artifact contents while preserving `.tools` and `artifacts\README.md`.

## Current status

The port is under active development toward the first independently installable
`InvisibleDragon 0.1.0` and `SimpleDragon 0.1.0` distributions. A generated
binary is not considered compatible until the algorithm, semantic IDF,
EnergyPlus result, Rhino geometry, and dual-package load gates applicable to it
pass.

## Repository rules

- Core projects do not reference RhinoCommon or Grasshopper.
- Rhino adapters own geometry conversion; GH projects own UI and serialization.
- Simulation runs are explicit and asynchronous, never slider-triggered.
- IDs, collection order, numeric culture, manifests, and generated artifacts are
  deterministic.
- Meaningful milestones are tested, committed, and pushed to `main`.

See [NOTICE.md](NOTICE.md) for third-party and redistribution notes.

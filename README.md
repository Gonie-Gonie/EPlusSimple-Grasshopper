# Dragons-Grasshopper

This Gonie-Gonie repository ports the upstream EPlusSimple 0.7.0 project and
its IDragon layer to two independently installable Rhino 7+ / Grasshopper
plugins written in C#. The public product names in this port are:

- `GonieGonie.InvisibleDragon.GH` is the renamed IDragon port. It preserves
  planar polygon vertices and builds EnergyPlus models.
- `GonieGonie.SimpleDragon.GH` is the renamed EPlusSimple port. It preserves the
  area-and-azimuth abstraction and internally converts a `GreenRetrofitModel`
  through the InvisibleDragon engine when `Run SimpleDragon` is requested.

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

## Installing a packaged candidate

Installed plugins require Rhino 7 or Rhino 8 on Windows, but not the .NET SDK,
Python, or Visual Studio. Local candidate builds produce a matching Yak archive
and portable plugin ZIP for each product. These outputs are currently for
controlled verification and inspection only; they are not publicly published.
InvisibleDragon candidates carry the exact pinned EnergyPlus ZIP and
SimpleDragon candidates carry the exact pinned KoreanTMY ZIP; neither payload
is expanded in source control or directly inside a package.

Read [Installation](docs/installation.md), [Choosing a Dragon](docs/choosing-a-dragon.md),
and [EnergyPlus and weather](docs/energyplus-and-weather.md) before using a
candidate. InvisibleDragon and SimpleDragon may be installed independently or
together when they come from the same release commit.

The canonical Grasshopper flow keeps ownership local and simulation setup internal:

```text
Curve + Fenestration Construction -> SD Opening ----------------+
                                                                v
Face Brep(s) + Construction + Boundary choice -> SD Wall / Ceiling / Floor -> SD Zone <- Height / Profile / HVAC / ERV
                                                                                     |
                                                                                     +-> SD Model (Address/Vintage) -> Run SimpleDragon -> GRR

Curve -> ID Window / Door -> ID Wall / Ceiling / Floor -> ID Zone <- Profile / HVAC / ERV
                                                                |
                                                                +-> ID Model <- PV
                                                                      |
                                                                      +-> Compile InvisibleDragon -> IDF --+-> Run InvisibleDragon -> Result
                                                                                                            ^
                                                                           EPW File -> ID Weather -----------+
```

In these component labels, `ID` abbreviates InvisibleDragon; it is not an
identifier port.

No entity-ID or Zone/Face-index input, parent-level construction fallback, assignment pass,
EnergyPlus/IDD path, runtime root, or temporary-work input is required. The
generic Surface authoring component has been replaced by explicit Floor,
Ceiling, and Wall components; each fixes its surface type. Boundary Condition
and other finite categories are named choices available from the input menu,
not integer codes.

Openings and opaque constructions belong to their completed Floor, Ceiling, or
Wall. A Zone receives only those owned Surfaces and Zone-level values.
Grasshopper item inputs naturally vectorize over lists and trees and preserve
their paths, while a Zone consumes each branch of its Surfaces input as one
owned list. Coincident, opposite-facing Surfaces with `Outdoors` selected are
paired as inter-zone boundaries when either model is composed. Both Dragon
modules create their deterministic relationship IDs internally; users express
ownership by wiring typed objects rather than typing identifiers.
SimpleDragon's direct runner performs weather selection, conversion, IDF
generation, EnergyPlus execution, and GRR construction internally; no
EPW path or InvisibleDragon execution type appears on the SimpleDragon canvas.
Standalone InvisibleDragon execution deliberately exposes one user-owned
weather boundary: `EPW File -> ID Weather -> Run InvisibleDragon`. `ID Weather`
verifies the selected EPW and creates the typed handle consumed by the runner;
InvisibleDragon does not select or download weather.

## Developer quick start

From a Windows command prompt or PowerShell:

```text
.\dev.cmd setup
.\dev.cmd build
```

Run `.\dev.cmd help` to list every supported workflow from this single entry point.

`.\dev.cmd setup` finds the exact SDK from `global.json` or installs it under
`.tools\dotnet`, selects exact Python 3.12.7 (installing the official embeddable
package locally when needed), validates Rhino 7 and Rhino 8 independently, and
writes the generated, non-secret `.config\local.settings.json`. It is
idempotent, so rerun it after installing Rhino to enable that version's tests.
Setup and build serialize their sanctioned NuGet restore workflows with a
repository-local lease under `.tools` and always run the all-file normalizer
after the restore attempt. A successful verified batch normalizes every tracked
`packages.lock.json` to LF. A caught commit failure is rolled back when rollback
verification succeeds; otherwise ambiguous state is retained with recovery
evidence instead of overwritten. A process crash leaves its snapshots under
`.tools\package-lock-normalization`, and the next restore fails closed before
doing work so they are not silently reused.
`dev.cmd clean` acquires the same lease before removing the fully disposable
`temp` tree and preserves `.tools` recovery state. This lease coordinates the
supplied repository commands; it is not an operating-system sandbox against a
malicious same-user process swapping filesystem paths mid-operation.

For port-equivalence work, `.\dev.cmd reference` runs the pinned historical Python
implementation plus the hash-locked EnergyPlus 24.2 IDD and official epJSON
schema in isolated processes, then writes deterministic database, full-schema,
GRM, and semantic IDF references under `temp\reference`. If EnergyPlus is not
already present, first run
`.\dev.cmd setup -InstallEnergyPlus`. Use
`.\dev.cmd reference -Mode Verify` to compare them byte-for-byte with the reviewed
baseline in `fixtures\reference\python-0.7.0`. Python remains development-only.

EnergyPlus extraction uses a deliberate detect-only default. Setup always
prepares the exact EnergyPlus and KoreanTMY distribution ZIPs under
`.tools\distributions` unless `-SkipEmbeddedPayloads` is passed, and validates the corrected
24.2.0 build `94a887817b` at `C:\EnergyPlusV24-2-0` or under `.tools` by the
executable, IDD, and ExpandObjects SHA-256 values. To download and verify the
official portable ZIP and extract it when no compatible runtime exists, run:

```text
.\dev.cmd setup -InstallEnergyPlus
```

Use `.\dev.cmd setup -WhatIf -SkipRestore` for a no-write preview. `.\dev.cmd build`
performs restore, compile, and tests with the setup-selected SDK, then stages
all runtime variants under `artifacts\<module>\rhino7\net48`,
`artifacts\<module>\rhino8\net7.0`, and
`artifacts\<module>\rhino8\net8.0`. Rhino-dependent tests are skipped only for the
missing Rhino version; headless projects continue to build.

All disposable downloads, build intermediates, test runs, logs, and simulations
are routed under `temp`; NuGet packages and reusable local toolchains are under
`.tools`. Run `.\dev.cmd clean` to safely remove only `temp` and generated
artifact contents while preserving `.tools` and `artifacts\README.md`.

To package an already successful build into deterministic Yak archives and
portable plugin ZIPs, run:

```text
.\dev.cmd package -SkipBuild
```

The eight tracked definitions and two named-building Rhino models under
[`examples`](examples/README.md) cover materials, profiles, geometry, HVAC,
standalone InvisibleDragon compile/weather/run wiring, two-zone GRM authoring,
Address/Vintage-selected packaged weather, direct simulation, result plots,
CSV previews, and the gated Run-to-GRR/CSV workflow. Example 02 keeps its EPW
File input empty and every action trigger False, so opening it never reads a
weather path or starts EnergyPlus. Example 12 is the complex two-zone
Floor/Ceiling/Wall-to-Zone model-authoring demonstration; its opening-free walls
are authored as lists while opening-bearing walls remain separate. Example 14
uses the same explicit Surface ownership with electric radiators for a stable
end-to-end `SD Model -> Run SimpleDragon -> GRR` workflow.
Rhino 7 writes the canonical files;
`.\dev.cmd examples` solves and round-trip validates them in both Rhino 7 and
Rhino 8.

Maintainers can execute the complete first-candidate gate with:

```text
.\dev.cmd release
```

This command requires a clean `main` commit already pushed to `origin/main`,
both Rhino 7 and Rhino 8, and the pinned EnergyPlus runtime (which setup can
prepare). It repeats setup, oracle verification, build/tests, example
round-trips, packaging, and six exact-portable-ZIP host scenarios, then writes
the attested local candidate below `artifacts\release`. It does not create a
tag, GitHub release, plugin installation, or Yak publication. See the
[release checklist](docs/release-checklist.md) for the evidence reviewed by the
gate.

To rebuild the current source, remove any installed Dragon packages, and
install both products into every detected Rhino 7/8 generation, close Rhino and
run:

```text
.\dev.cmd install
```

Use `.\dev.cmd install -UseExistingPackages` for an immediate reinstall from the
already generated, hash-checked Yak files under `artifacts\packages`.

## Current status

The port is preparing the first independently installable local
`InvisibleDragon 0.1.0` and `SimpleDragon 0.1.0` release candidate. A generated
binary is not considered compatible until the algorithm, semantic IDF,
EnergyPlus result, Rhino geometry, example round-trip, and isolated/co-loaded
package gates applicable to it pass.

The [documentation index](docs/README.md) covers the end-user workflow,
compatibility boundary, troubleshooting, examples, and maintainer release
gates. No public binary, release tag, GitHub release, or Yak publication is
authorized until the historical upstream standalone-license omission recorded
in [NOTICE.md](NOTICE.md) has been reviewed and resolved.

## Repository rules

- Core projects do not reference RhinoCommon or Grasshopper.
- Rhino adapters own geometry conversion; GH projects own UI and serialization.
- Simulation runs are explicit and asynchronous, never slider-triggered.
- IDs, collection order, numeric culture, manifests, and generated artifacts are
  deterministic.
- Meaningful milestones are tested, committed, and pushed to `main`.

See [NOTICE.md](NOTICE.md) for third-party and redistribution notes.

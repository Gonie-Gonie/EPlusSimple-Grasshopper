# Dragons-Grasshopper

This repository ports the upstream EPlusSimple 0.7.0 project and its IDragon
layer to two independently installable Rhino 7+ / Grasshopper plugins written
in C#. The public product names in this port are:

- `Dragons.InvisibleDragon.GH` is the renamed IDragon port. It preserves
  planar polygon vertices and builds EnergyPlus models.
- `Dragons.SimpleDragon.GH` is the renamed EPlusSimple port. It preserves the
  area-and-azimuth abstraction and internally converts a `GreenRetrofitModel`
  through the InvisibleDragon engine when `Run SimpleDragon` is requested.

The tracked upstream source is pinned in
[`upstream/upstream.lock.json`](upstream/upstream.lock.json). Python supports the
development oracle and PDF documentation toolchain; released plugins do not
require Python.

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

## Repository layout

The root contains only stable project boundaries:

| Path | Responsibility |
|---|---|
| `src/` | InvisibleDragon, SimpleDragon, and shared production code |
| `tests/` | Product, compatibility, packaging, installer, and lifecycle tests |
| `examples/` | Tracked Grasshopper definitions and Rhino building models |
| `docs/` | Audience-routed user documentation and separate development/maintainer procedures |
| `scripts/` / `tools/` | `dev.cmd` workflows and their implementation utilities |
| `resources/` | Canonical icon artwork and pinned external-runtime declarations |
| `packaging/` | Product manifests, package rules, and runtime packaging policy |
| `data/` | Byte-pinned upstream SimpleDragon CSV inputs |
| `fixtures/` / `upstream/` | Immutable compatibility baselines and provenance controls |
| `artifacts/` / `temp/` / `.tools/` | Results, disposable work, and reusable local toolchains |

Generated local settings live with the reusable toolchain state under
`.tools\state`; no generated configuration directory is kept at the root.

## Installing a release package

Installed plugins require Rhino 7 or Rhino 8 on Windows, but not the .NET SDK,
Python, or Visual Studio. Local candidate builds produce a matching Yak archive
and portable plugin ZIP for each product. Local outputs are verification
artifacts; use only files attached to the matching GitHub release for public
installation.
InvisibleDragon packages carry the exact pinned EnergyPlus ZIP and
SimpleDragon packages carry the exact pinned KoreanTMY ZIP; neither payload
is expanded in source control or directly inside a package.

Read [Installation](docs/user/installation.md), [Choosing a Dragon](docs/user/choosing-a-dragon.md),
and [EnergyPlus and weather](docs/user/energyplus-and-weather.md) before
installation. InvisibleDragon and SimpleDragon may be installed independently or
together when they come from the same release commit.

The canonical Grasshopper flow keeps ownership local and simulation setup internal:

![Illustrated SimpleDragon workflow from owned surfaces and systems through SD Model and Run SimpleDragon to GRR outputs.](docs/user/assets/illustrations/simpledragon-workflow.png)

*SimpleDragon keeps weather selection and EnergyPlus execution inside the direct run boundary.*

![Illustrated InvisibleDragon workflow from explicit model authoring and compilation through verified EPW input to EnergyPlus results.](docs/user/assets/illustrations/invisibledragon-workflow.png)

*InvisibleDragon exposes the Energy Model, IDF, and verified EPW boundary while keeping the runtime internal.*

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

## Development

Repository contributors use the single root wrapper:

```text
.\dev.cmd setup
.\dev.cmd build
```

The [development documentation](docs/development/README.md) owns setup,
build/test, OODocs, compatibility, packaging, installation, cleanup, release,
and publishing procedures. The public [user documentation](docs/user/README.md)
contains no repository setup steps. Run `.\dev.cmd help` for the complete
command list.

## Current status

This repository defines the corrected lockstep
`InvisibleDragon 0.1.2` and `SimpleDragon 0.1.2` release. A generated
binary is not considered compatible until the algorithm, semantic IDF,
EnergyPlus result, Rhino geometry, example round-trip, and isolated/co-loaded
package gates applicable to it pass.

The [documentation index](docs/README.md) covers the end-user workflow,
compatibility boundary, troubleshooting, examples, and maintainer release
gates. Published artifacts are accepted only when their version and source
commit match the verified release record. SimpleDragon includes hash-verified
Korean TMYx weather data sourced from
[Climate.OneBuilding](https://climate.onebuilding.org/); its pinned archive and
dataset citation are recorded in `resources/runtime/distributions.json`.

## Repository rules

- Core projects do not reference RhinoCommon or Grasshopper.
- Rhino adapters own geometry conversion; GH projects own UI and serialization.
- Simulation runs are explicit and asynchronous. Run, Cancel, Write, and Export
  actions use momentary Grasshopper Buttons rather than Sliders or Toggles.
- IDs, collection order, numeric culture, manifests, and generated artifacts are
  deterministic.
- Meaningful milestones are tested, committed, and pushed to `main`.

See [NOTICE.md](NOTICE.md) for third-party notices and runtime-data provenance.

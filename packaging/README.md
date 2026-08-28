# Packaging sources

`.\dev.cmd package` is the local, one-command packaging entry point. It builds the
Release plugins unless `-SkipBuild` is supplied, verifies/downloads the exact
Yak executable pinned in `yak.lock.json`, creates fresh package stages, builds
Yak archives, creates portable plugin ZIPs, and runs the package layout and
shared-assembly compatibility gates. It never publishes or installs a package.
Rhino is not required for this default packaging flow.

To run the release-level host gate against the portable ZIP artifacts after all
normal package verification has passed, use:

```powershell
.\dev.cmd package -RunPortableHostGate
```

This explicit option requires installed Rhino 7 and Rhino 8. It starts six fresh
host processes: `InvisibleOnly`, `SimpleOnly`, and `Both` once on each Rhino
major version. The gate uses `Source PortablePackage`, extracts the just-created
archives from `artifacts/packages`, and does not rebuild or substitute plugin
assemblies. Each host summary carries the package-index-verified archive path and
SHA-256 plus the SHA-256 of every loaded GHA. Omitting the switch keeps the
existing no-Rhino packaging behavior.

Generated output is written below `artifacts/packages`; disposable work and
logs stay below `temp/packaging`. Each product is independent:

```text
artifacts/packages/<product>/
|-- stage/
|   |-- rhino7/                  # flat net48 Yak payload
|   `-- rhino8/                  # net7.0 + net8.0 Yak multi-target payload
|-- yak/
|   |-- <product>-0.1.0-rh7-win.yak
|   `-- <product>-0.1.0-rh8-win.yak
`-- portable/
    `-- <product>-0.1.0-portable-plugin-win.zip
```

The stage and ZIP roots include their manifest, icon, Gonie-Gonie notices,
payload manifest, and SHA-256 list. Each package root additionally contains
exactly one product-specific verified archive: EnergyPlus for InvisibleDragon,
or KoreanTMY weather for SimpleDragon. RhinoCommon, Grasshopper, PDB, XML
documentation, Python, and directly expanded EnergyPlus/EPW files are excluded.
The package manifest and index record the archive path, length, SHA-256, and
product exclusivity. InvisibleDragon also exposes the EnergyPlus archive's
exact `LICENSE.txt` at `runtime/energyplus/LICENSE.txt`.

Yak 0.13.0 is executed from a SHA-256-verified temp copy. A source-built startup
hook gives Yak's inspection-only process access to the staged dependencies and
the locked RhinoCommon/Grasshopper NuGet reference assemblies. Those resolver
files remain below `temp/packaging`; the verifier rejects them from every stage,
Yak archive, portable ZIP, and artifact tree. Packaging accepts only the real
`rh7_*`/`rh8_*` tags inferred by Yak from the entry GHA; an `any` tag is a hard
failure.

Rhino 7 receives a flat `net48` Yak distribution. Rhino 8 receives an official
multi-target layout with `net7.0` for Rhino 8.0-8.19 and `net8.0` for Rhino 8.20
and later. The verifier rejects incorrect framework directories, distribution
tags, duplicate assembly identities, version drift, and shared-DLL SHA drift.

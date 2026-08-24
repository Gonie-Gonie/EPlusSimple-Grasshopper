# Packaging sources

`package.cmd` is the local, one-command packaging entry point. It builds the
Release plugins unless `-SkipBuild` is supplied, verifies/downloads the exact
Yak executable pinned in `yak.lock.json`, creates fresh package stages, builds
Yak archives, creates plugin-only offline ZIPs, and runs the package layout and
shared-assembly compatibility gates. It never publishes or installs a package.

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
`-- offline/
    `-- <product>-0.1.0-offline-plugin-win.zip
```

The stage and ZIP roots include their manifest, icon, Gonie-Gonie notices,
payload manifest, and SHA-256 list. Plugin payloads contain runtime assemblies
only: RhinoCommon, Grasshopper, PDB, XML documentation, Python, EnergyPlus
binaries, and weather files are excluded. The Gonie-Gonie runtime bootstrap
locates or prepares the separately pinned EnergyPlus runtime after install.

Yak 0.13.0 is executed from a SHA-256-verified temp copy. A source-built startup
hook gives Yak's inspection-only process access to the staged dependencies and
the locked RhinoCommon/Grasshopper NuGet reference assemblies. Those resolver
files remain below `temp/packaging`; the verifier rejects them from every stage,
Yak archive, offline ZIP, and artifact tree. Packaging accepts only the real
`rh7_*`/`rh8_*` tags inferred by Yak from the entry GHA—an `any` tag is a hard
failure.

Rhino 7 receives a flat `net48` Yak distribution. Rhino 8 receives an official
multi-target layout with `net7.0` for Rhino 8.0-8.19 and `net8.0` for Rhino 8.20
and later. The verifier rejects incorrect framework directories, distribution
tags, duplicate assembly identities, version drift, and shared-DLL SHA drift.

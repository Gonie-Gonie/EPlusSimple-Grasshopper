# Packaging sources

`.\dev.cmd package` is the local, one-command packaging entry point. It builds the
Release plugins unless `-SkipBuild` is supplied, verifies/downloads the exact
Yak executable pinned in `yak.lock.json`, creates fresh package stages, builds
Yak archives, creates portable plugin ZIPs, and runs the package layout and
shared-assembly compatibility gates. It never publishes or installs a package.
Rhino is not required for this default packaging flow.

Each tracked packaging input has one canonical location:

```text
packaging/
|-- manifests/{invisible-dragon,simple-dragon}.yml
|-- package-spec.json
`-- yak.lock.json

resources/
|-- icons/generated/<product>/<product>-256.png
`-- runtime/{distributions.json,manifest.template.json}
```

The generated 256-pixel product icon is consumed directly; packaging does not
keep a duplicate `icon.png` beside each source manifest.

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

Final output is written below `artifacts/packages`. Verification stages and
logs stay below `temp/packaging` only while packaging is running and are
removed after a successful run. Failed-run scratch remains available until the
next package run resets it or `dev.cmd clean -TempOnly` removes it. Each product
is independent:

```text
artifacts/packages/<product>/
|-- yak/
|   |-- <product>-0.1.0-rh7-win.yak
|   `-- <product>-0.1.0-rh8-win.yak
`-- portable/
    `-- <product>-0.1.0-portable-plugin-win.zip
```

These per-product files are candidate inputs, not the GitHub release attachment
set. The local release gate assembles a separate end-user bundle below
`artifacts/release/github-assets` with exactly four future public assets:

```text
Dragons-Grasshopper-0.1.0-Windows-Installer.zip
Dragons-Grasshopper-User-Guide-0.1.0.pdf
Dragons-Grasshopper-Food4Rhino-Metadata-0.1.0.pdf
SHA256SUMS.txt
```

The Installer ZIP contains `Install-Dragons.cmd`, `release-manifest.json`, an
internal `checksums.sha256`, `LICENSE.txt`, `NOTICE.md`, `README.txt`, and only
the required Yak payloads below `packages/rhino7` and `packages/rhino8`. The
installer must work from a complete extracted copy by resolving every payload
relative to its own directory. It must not require the repository, `.tools`,
or development setup. `SHA256SUMS.txt` verifies the other three GitHub assets;
the internal `release-assets-manifest.json` remains beside `github-assets` as
candidate evidence and is not a fifth public asset.

`packaging/package-spec.json` deliberately fixes the current first-release
version at `0.1.0`. The final version decision must be made in source, and the
future repository tag must equal it exactly (`v0.1.0` for `0.1.0`). Assembling
these files locally does not create a tag or authorize publication.
The same specification is the machine-readable source for the individual
Gonie-Gonie ownership, MIT license, confirmed public support email, and current
Climate.OneBuilding weather-redistribution status. Packaging verifies the
Oikolab/ERA5 and Copernicus provenance links in the distribution manifest,
copies the reviewed publication values into `package-index.json`, and lets the
release gate cross-check them before preparing any GitHub assets.

The transient stage and final ZIP roots include their manifest, icon, Gonie-Gonie notices,
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
files remain below `temp/packaging` while verification runs; the verifier rejects
them from every stage, Yak archive, portable ZIP, and artifact tree. Packaging
then removes that scratch tree. Packaging accepts only the real
`rh7_*`/`rh8_*` tags inferred by Yak from the entry GHA; an `any` tag is a hard
failure.

Rhino 7 receives a flat `net48` Yak distribution. Rhino 8 receives an official
multi-target layout with `net7.0` for Rhino 8.0-8.19 and `net8.0` for Rhino 8.20
and later. The verifier rejects incorrect framework directories, distribution
tags, duplicate assembly identities, version drift, and shared-DLL SHA drift.

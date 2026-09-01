# Installation

This page is for people installing and loading released Dragon packages.

There is no public Dragon binary or `v0.1.0` tag yet. The release names and
steps below describe the intended first public bundle only. On 2026-08-31, the
individual owner authorized publication to proceed while accepting that written
permission for the Climate.OneBuilding/Oikolab/ERA5, Copernicus, and ASHRAE
rights chain of SimpleDragon's embedded weather has not been verified. This
owner decision does not establish upstream permission or license that payload
under MIT. The MIT code license and public support address are confirmed.

## Requirements

- Windows x64.
- Rhino 7 or Rhino 8 with Grasshopper. Both versions may be installed.
- Network access is needed only when an installed product cannot find its
  embedded or cached pinned EnergyPlus runtime and uses the verified fallback.
  A normal InvisibleDragon package prepares its embedded runtime offline.

Installed plugins do not require the .NET SDK, Python, Visual Studio, or a
machine-wide EnergyPlus installer. Rhino is licensed separately and is never
installed by this project.

## Package choices

Each release candidate contains independent `invisible-dragon` and
`simple-dragon` products:

- Two Yak files per product: `rh7-win` and `rh8-win`.
- One portable plugin ZIP per product containing all supported Windows host
  variants.
- One hash-pinned product archive at the package root: EnergyPlus for
  InvisibleDragon or KoreanTMY weather for SimpleDragon.
- SHA-256 inventories and a package compatibility report.

Those are product-candidate inputs. The future GitHub release does not attach
the `.yak` or per-product portable ZIP files separately. Its Installer ZIP
contains the four verified Rhino 7/8 Yak payloads behind one installation
entry point; a portable ZIP may instead be offered separately through an
authorized Food4Rhino record.

Yak is the preferred installation format because Rhino selects the correct
payload. Rhino 7 uses `net48`. Rhino 8.0–8.19 uses the `net7.0` payload, and
Rhino 8.20 or later uses `net8.0`. The portable ZIP is intended for inspection,
controlled deployment, and recovery. Embedded archives remain compressed and
are verified before use; Python and directly expanded runtime/weather files are
deliberately excluded.

Do not combine files from different commits or versions. When both products
are installed, install the complete packages from the same release so their
shared `Dragons.*` assemblies remain identical.

## Installing the future release bundle

The future `v0.1.0` GitHub release is intentionally limited to exactly four
assets:

```text
Dragons-Grasshopper-0.1.0-Windows-Installer.zip
Dragons-Grasshopper-User-Guide-0.1.0.pdf
Dragons-Grasshopper-Food4Rhino-Metadata-0.1.0.pdf
SHA256SUMS.txt
```

The version is fixed by the release source, not chosen by the installer. The
tag must equal the source version exactly: source `0.1.0` uses tag `v0.1.0`.
Use `SHA256SUMS.txt` from that same release to verify the Installer ZIP before
opening it.

1. Download the Installer ZIP and `SHA256SUMS.txt` from the same release.
2. If Windows marked the ZIP as blocked, open its Properties and unblock it.
3. Extract the complete ZIP into a new writable directory. Do not run the
   command from the compressed-folder view, and do not move files out of the
   extracted tree.
4. Close every Rhino process. The check and install modes both require Rhino
   to be closed.
5. From that extracted root, optionally run `Install-Dragons.cmd --check` to
   inspect the detected Rhino installations and selected payloads without
   installing.
6. Run `Install-Dragons.cmd`. By default it
   installs both Dragon products for every detected Rhino 7 and Rhino 8 host.
   Use `Install-Dragons.cmd rhino7` or `Install-Dragons.cmd rhino8` to limit the
   host generation; `--check` can be combined with either target.

Keep the extracted directory intact until installation finishes. The command
resolves `release-manifest.json`, its internal `checksums.sha256`, legal files,
and `packages\rhino7|rhino8` relative to its own directory. It does not use the
current working directory and does not require a repository checkout,
developer environment setup, .NET SDK, Python, or Visual Studio.

## First load

1. Start Rhino and open Grasshopper after installation completes.
2. Confirm that the `InvisibleDragon` or `SimpleDragon` tab appears.
3. Add the module's Version component before opening a production definition.

InvisibleDragon can be installed by itself. SimpleDragon can also be installed
by itself; its package carries the shared InvisibleDragon Core and Rhino libraries
needed by its internal conversion and execution pipeline, but it does not
install the InvisibleDragon GHA or tab. `Run SimpleDragon` still accepts a GRM
and returns a GRR directly; it never asks for an InvisibleDragon model, IDF,
Weather, or EnergyPlus result on the canvas.

Installing the matching InvisibleDragon product alongside SimpleDragon makes
the pinned EnergyPlus archive available for an offline first run. Without that
sibling package, SimpleDragon can reuse an existing verified per-user runtime
cache or acquire the exact pinned official archive through the verified network
fallback. No explicit EnergyPlus, IDD, EPW, runtime, or temp path is required.

## Updating or removing

Close Rhino before updating. Replace the complete product package rather than
individual DLLs. A mixed directory can load one shared assembly from an older
version and make the second GHA fail even though both files exist.

The module-owned EnergyPlus and packaged-weather per-user caches are separate
from the plugins. Removing a Dragon package does not remove either cache. See
[EnergyPlus and weather](energyplus-and-weather.md) before deleting runtime or
weather data.

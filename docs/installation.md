# Installation

## Requirements

- Windows x64.
- Rhino 7 or Rhino 8 with Grasshopper. Both versions may be installed.
- Network access while setup downloads the pinned archives, or only as a
  verified fallback if an installed package's embedded EnergyPlus archive is
  absent. A normal InvisibleDragon package prepares its embedded runtime
  offline.

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

Yak is the preferred installation format because Rhino selects the correct
payload. Rhino 7 uses `net48`. Rhino 8.0–8.19 uses the `net7.0` payload, and
Rhino 8.20 or later uses `net8.0`. The portable ZIP is intended for inspection,
controlled deployment, and recovery. Embedded archives remain compressed and
are verified before use; Python and directly expanded runtime/weather files are
deliberately excluded.

Do not combine files from different commits or versions. When both products
are installed, their shared `GonieGonie.*` assemblies must be byte-identical.
The package verifier enforces this for every generated candidate.

## Local release candidates

Contributors can reproduce packages from a clean checkout:

```text
.\dev.cmd setup
.\dev.cmd build
.\dev.cmd package -SkipBuild
```

Outputs are written below `artifacts\packages`. Packaging never installs a
plugin, publishes to Rhino Package Manager, creates a Git tag, or creates a
public release.

## One-command local reinstall

Close every Rhino process, then run `.\dev.cmd install`. It prepares the reproducible
environment, builds the current source without tests, creates fresh packages,
uninstalls only the `invisible-dragon` and `simple-dragon` package IDs from each
detected Rhino 7/8 installation, and installs the matching local Yak files.

```text
.\dev.cmd install
```

To skip setup/build/package and reinstall the existing hash-checked Yak files:

```text
.\dev.cmd install -UseExistingPackages
```

Use `-Target Rhino7` or `-Target Rhino8` to limit the operation. Logs and the
installation receipt are written below `temp\install`; the script never
publishes a package and never removes unrelated Rhino packages.

## First load

1. Close every Rhino process before changing installed plugin files.
2. Install the product package for the intended Rhino generation.
3. If Windows marked a downloaded archive as blocked, use the file Properties
   dialog to unblock it before extraction or installation.
4. Start Rhino and open Grasshopper.
5. Confirm that the `InvisibleDragon` or `SimpleDragon` tab appears.
6. Add the module's Version component before opening a production definition.

InvisibleDragon can be installed by itself. SimpleDragon can also be installed
by itself; its package carries the shared InvisibleDragon model/type libraries
needed for conversion, but it does not install the InvisibleDragon GHA or tab.
Installing both products enables typed conversion and result connections.
The canonical full simulation uses both tabs: SimpleDragon selects and verifies
the Address/Vintage-based packaged Weather, and InvisibleDragon consumes it with the
typed IDF. No explicit EnergyPlus, IDD, EPW, runtime, or temp path is required.

## Updating or removing

Close Rhino before updating. Replace the complete product package rather than
individual DLLs. A mixed directory can load one shared assembly from an older
version and make the second GHA fail even though both files exist.

The module-owned EnergyPlus and packaged-weather per-user caches are separate
from the plugins. Removing a Dragon package does not remove either cache. See
[EnergyPlus and weather](energyplus-and-weather.md) before deleting runtime or
weather data.

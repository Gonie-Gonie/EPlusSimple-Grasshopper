# Installation

## Requirements

- Windows x64.
- Rhino 7 or Rhino 8 with Grasshopper. Both versions may be installed.
- An EPW weather file for any weather-based annual simulation.
- Network access the first time the optional EnergyPlus runtime preparation is
  used, unless a hash-matching EnergyPlus 24.2.0 runtime is already available.

Installed plugins do not require the .NET SDK, Python, Visual Studio, or a
machine-wide EnergyPlus installer. Rhino is licensed separately and is never
installed by this project.

## Package choices

Each release candidate contains independent `invisible-dragon` and
`simple-dragon` products:

- Two Yak files per product: `rh7-win` and `rh8-win`.
- One portable plugin ZIP per product containing all supported Windows host
  variants.
- SHA-256 inventories and a package compatibility report.

Yak is the preferred installation format because Rhino selects the correct
payload. Rhino 7 uses `net48`. Rhino 8.0–8.19 uses the `net7.0` payload, and
Rhino 8.20 or later uses `net8.0`. The portable ZIP is intended for inspection,
controlled deployment, and recovery; it is not a complete offline simulation
bundle because EnergyPlus and weather data are deliberately excluded.

Do not combine files from different commits or versions. When both products
are installed, their shared `GonieGonie.*` assemblies must be byte-identical.
The package verifier enforces this for every generated candidate.

## Local release candidates

Contributors can reproduce packages from a clean checkout:

```text
setup.cmd
build.cmd
package.cmd -SkipBuild
```

Outputs are written below `artifacts\packages`. Packaging never installs a
plugin, publishes to Rhino Package Manager, creates a Git tag, or creates a
public release.

## One-command local reinstall

Close every Rhino process, then run `install.cmd`. It prepares the reproducible
environment, builds the current source without tests, creates fresh packages,
uninstalls only the `invisible-dragon` and `simple-dragon` package IDs from each
detected Rhino 7/8 installation, and installs the matching local Yak files.

```text
install.cmd
```

To skip setup/build/package and reinstall the existing hash-checked Yak files:

```text
install.cmd -UseExistingPackages
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

## Updating or removing

Close Rhino before updating. Replace the complete product package rather than
individual DLLs. A mixed directory can load one shared assembly from an older
version and make the second GHA fail even though both files exist.

The EnergyPlus per-user cache is separate from the plugin. Removing a Dragon
package does not remove that cache or any user-supplied EPW files. See
[EnergyPlus and weather](energyplus-and-weather.md) before deleting runtime
data.

# Dragon user documentation

These pages are written for people installing or using InvisibleDragon and
SimpleDragon. They may be linked from public project pages and are the source
for externally distributed guidance.

## Start here

- [Installation](installation.md): requirements, package choices, first load,
  updates, and removal.
- [Choosing a Dragon](choosing-a-dragon.md): when to use the explicit
  vertex-preserving InvisibleDragon model or the reduced SimpleDragon model.
- [Grasshopper workflow](grasshopper-workflow.md): ownership-based authoring,
  direct runs, lists/trees, results, plots, CSV, and batch studies.
- [EnergyPlus and weather](energyplus-and-weather.md): internal runtime
  preparation, SimpleDragon address-based weather, and InvisibleDragon's one
  explicit EPW boundary.
- [Troubleshooting](troubleshooting.md): loading, permissions, weather,
  geometry, results, and package conflicts.
- [Runnable examples](../../examples/README.md): eight Grasshopper definitions
  and two Rhino building models.

## Complete user guide

The PDF is assembled in this order from the following public chapters:

1. [Workflow](user-guide/01-workflow.md)
2. [Component In/Out Reference](user-guide/02-in-out-reference.md)
3. [Compatibility](user-guide/03-compatibility.md)
4. [Release Notes](user-guide/04-release-notes.md)

The In/Out Reference is generated from the actual Rhino 7 and Rhino 8 plugin
contracts and detailed component guidance. It covers every public component
and typed parameter. Python, OODocs, the .NET SDK, and EnergyPlus setup paths
are development concerns and are never prerequisites for an installed plugin.

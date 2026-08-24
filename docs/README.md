# Dragon documentation

InvisibleDragon and SimpleDragon are separate Grasshopper products built from
one source tree. Start with the module comparison, then follow the installation
and workflow guides.

- [Installation](installation.md): Rhino versions, packages, local builds, and
  simultaneous installation.
- [Choosing a Dragon](choosing-a-dragon.md): the geometry and modeling boundary
  between InvisibleDragon and SimpleDragon.
- [Grasshopper workflow](grasshopper-workflow.md): authoring, HVAC connections,
  conversion, simulation, results, CSV, and batch studies.
- [EnergyPlus and weather](energyplus-and-weather.md): the pinned runtime,
  explicit preparation, cache location, and user-supplied EPW policy.
- [Compatibility](compatibility.md): supported hosts, upstream baseline,
  interoperability, and current limitations.
- [Troubleshooting](troubleshooting.md): load, runtime, weather, and duplicate
  assembly problems.
- [Release checklist](release-checklist.md): maintainer-only candidate and
  publication gates.

Worked recipe outlines live in [the examples directory](../examples/README.md).
Python is never required by an installed plugin; it is only a development
oracle used to compare this port with its pinned historical baseline.

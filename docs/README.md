# Dragon documentation

InvisibleDragon and SimpleDragon are separate Grasshopper products built from
one source tree. Start with the module comparison, then follow the installation
and workflow guides.

- [Installation](installation.md): Rhino versions, packages, local builds, and
  simultaneous installation.
- [Choosing a Dragon](choosing-a-dragon.md): the geometry and modeling boundary
  between InvisibleDragon and SimpleDragon.
- [Grasshopper workflow](grasshopper-workflow.md): authoring, HVAC connections,
  direct simulation, results, CSV, and batch studies.
- [EnergyPlus and weather](energyplus-and-weather.md): the pinned runtime,
  bundled preparation, cache locations, SimpleDragon's Address/Vintage-selected
  EPW behavior, and the explicit EPW verification boundary for standalone
  InvisibleDragon execution.
- [Compatibility](compatibility.md): supported hosts, upstream baseline,
  interoperability, and current limitations.
- [Troubleshooting](troubleshooting.md): load, runtime, weather, and duplicate
  assembly problems.
- [Release checklist](release-checklist.md): maintainer-only candidate and
  publication gates.

Worked recipe outlines live in [the examples directory](../examples/README.md).
The four canonical PDF sources live in [user-guide](user-guide/README.md); run
`dev.cmd docs` to regenerate the exhaustive component reference from the
current runtime metadata and build the user-distribution PDF with OODocs.

Python is never required by an installed plugin. The repository setup creates
one isolated venv for the development oracle and PDF documentation build, so
contributors do not install those modules globally.

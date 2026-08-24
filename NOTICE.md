# Notices

Dragons-Grasshopper, including the InvisibleDragon and SimpleDragon products,
is owned and maintained by Gonie-Gonie. It is a C# port that tracks the
historical upstream project
[`snu-bslab/EPlusSimple`](https://github.com/snu-bslab/EPlusSimple) at the
commit recorded in `upstream/upstream.lock.json`. The upstream repository name,
module names, and source paths are retained only where exact provenance or
compatibility mapping requires them.

The tracked upstream `citation.cff` declares its EPlusSimple and IDragon source
as MIT licensed. Those names identify the upstream Python packages; the names
of the products published from this repository are SimpleDragon and
InvisibleDragon. The tracked upstream commit does not contain a standalone
`LICENSE` file, so that omission is recorded here and must be rechecked before
a public binary release.

EnergyPlus is a separate product of the U.S. Department of Energy and its
contributors. EnergyPlus binaries and data are not stored in this repository or
redistributed inside plugin packages. Development setup and the installed-plugin
bootstrap obtain or locate the pinned EnergyPlus runtime and retain its own
license and notices.

Rhino, RhinoCommon, Grasshopper, and Yak are products or technologies of Robert
McNeel & Associates. Their SDK assemblies are restored from McNeel's NuGet
packages and are not redistributed as part of the plugin payload.

Korean TMY weather files are not stored in this repository, downloaded by
setup, or redistributed in packages. Users must supply an EPW file. Weather
redistribution rights must be established separately before any weather payload
is added to a future distribution.

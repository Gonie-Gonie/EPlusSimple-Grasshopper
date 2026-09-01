# Notices

Dragons-Grasshopper, including the InvisibleDragon and SimpleDragon products,
is a C# port that tracks the historical upstream project
[`snu-bslab/EPlusSimple`](https://github.com/snu-bslab/EPlusSimple) at the
commit recorded in `upstream/upstream.lock.json`. The upstream repository name,
module names, and source paths are retained only where exact provenance or
compatibility mapping requires them.

The tracked upstream `citation.cff` declares its EPlusSimple and IDragon source
as MIT licensed. Those names identify the upstream Python packages; the public
products in this repository are SimpleDragon and InvisibleDragon. The tracked
upstream commit does not contain a standalone `LICENSE` file, so this notice
preserves the exact MIT declaration and pinned source attribution. This port is
offered under the repository MIT license.

EnergyPlus is a separate product of the U.S. Department of Energy and its
contributors. Source control does not store its binaries. Development setup
downloads the exact official EnergyPlus 24.2.0 Windows archive pinned by URL,
byte length, and SHA-256 in `resources/runtime/distributions.json`;
InvisibleDragon packages embed that archive unchanged under
`runtime/energyplus/`. EnergyPlus retains its own license and notices;
`runtime/energyplus/LICENSE.txt` is copied byte-for-byte from the pinned archive
and separately hash-checked.

Rhino, RhinoCommon, Grasshopper, and Yak are products or technologies of Robert
McNeel & Associates. Their SDK assemblies are restored from McNeel's NuGet
packages and are not redistributed as part of the plugin payload.

Korean TMY EPW files are not expanded or stored in source control. Development
setup downloads the exact `KoreanTMY-v1.zip` pinned by URL, byte length, and
SHA-256 in `resources/runtime/distributions.json`; SimpleDragon packages embed
that archive unchanged under `runtime/weather/`. The archive contains 80 root
EPWs and covers all 78 unique EPW names referenced by the tracked address
metadata. Their headers identify the files as TMYx data sourced from
[Climate.OneBuilding](https://climate.onebuilding.org/). The archive source,
TMYx dataset citation, address coverage, byte length, and SHA-256 identity are
recorded in `resources/runtime/distributions.json`. SimpleDragon resolves its
tracked address metadata against this embedded archive and validates each EPW
before simulation.

The public support address for both Dragon products is
`hyeonggon.jo@snu.ac.kr`.

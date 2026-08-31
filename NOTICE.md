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
`LICENSE` file, so this notice preserves the exact MIT declaration and pinned
source attribution. On 2026-08-31, the Gonie-Gonie owner confirmed that this
port is individually owned and released under the repository MIT license. The
source-code license review is therefore complete; this does not grant rights
to separately sourced runtime data.

EnergyPlus is a separate product of the U.S. Department of Energy and its
contributors. Source control does not store its binaries. Development setup
downloads the exact official EnergyPlus 24.2.0 Windows archive pinned by URL,
byte length, and SHA-256 in `resources/runtime/distributions.json`; InvisibleDragon package
candidates embed that archive unchanged under `runtime/energyplus/`. EnergyPlus
retains its own license and notices; `runtime/energyplus/LICENSE.txt` is copied
byte-for-byte from the pinned archive and separately hash-checked. This
packaging implementation is not, by itself, authorization to publish the
resulting candidate publicly.

Rhino, RhinoCommon, Grasshopper, and Yak are products or technologies of Robert
McNeel & Associates. Their SDK assemblies are restored from McNeel's NuGet
packages and are not redistributed as part of the plugin payload.

Korean TMY EPW files are not expanded or stored in source control. Development
setup downloads the exact `KoreanTMY-v1.zip` pinned by URL, byte length, and
SHA-256 in `resources/runtime/distributions.json`; SimpleDragon package candidates embed
that archive unchanged under `runtime/weather/`. The archive contains 80 root
EPWs and covers all 78 unique EPW names referenced by the tracked address
metadata. Their headers identify the files as TMYx data downloaded from
[Climate.OneBuilding](https://climate.onebuilding.org/), whose source page says
that its authors create TMYx from public ISD observations and ERA5 solar data
supplied through Oikolab, and supplies the requested citation. The current
general Copernicus Products licence allows distribution and adaptation subject
to visible attribution and a European Commission/ECMWF no-responsibility
statement. However, the exact archive carries neither those notices nor a
documented pass-through grant, Oikolab's public API terms do not grant public
redistribution, and Climate.OneBuilding states `All Rights Reserved`. Public
download availability therefore does not establish a complete right to bundle
the files in a new installer. In addition, 59 of the 80 EPWs embed 2021 ASHRAE
climatic design conditions, for which ASHRAE separately directs software
developers to obtain an embedding licence. The evidence is recorded in the
[weather rights review](https://github.com/Gonie-Gonie/EPlusSimple-Grasshopper/blob/main/docs/development/publishing/weather-rights-review.md).
Public package publication remains unauthorized until written redistribution
permission and every applicable upstream notice are retained, or this payload
is replaced with one carrying adequate redistribution terms.

The confirmed public support address for both Dragon products is
`hyeonggon.jo@snu.ac.kr`.

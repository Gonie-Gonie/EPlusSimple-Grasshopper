# Climate.OneBuilding weather rights review

This is the maintainer evidence record for the `KoreanTMY-v1.zip` payload
pinned in `resources/runtime/distributions.json`. It records provenance and the
result of the public-rights review; it is not a substitute for legal advice or
written permission from the data publisher.

## Payload identity

- Reviewed: `2026-08-31`
- Archive: `KoreanTMY-v1.zip`
- Bytes: `128349513`
- SHA-256: `fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0`
- Contents: 80 root-level EPW files, with no license, copying, notice, or README
  entry in the archive
- Filename variants: 76 names end in `2009-2023` and four end in `2007-2021`
- EPW header source-data periods: 65 x `2009-2023`, 10 x `2009-2015`, and one
  each of `2007-2015`, `2007-2021`, `2008-2020`, `2009-2014`, and `2009-2019`

The reviewed EPW headers identify `SRC-TMYx`, describe ISD/ERA5 source data,
and state that the files were downloaded from Climate.OneBuilding. The local
archive is an exact, hash-pinned aggregate hosted by the historical
`EPlusSimple-resources` release; that intermediate download location does not
create a new license for the underlying weather files.

## Official-source findings

Climate.OneBuilding describes itself as a repository of climate data for
building simulation and makes South Korean TMYx files available for individual
download. Its [weather data sources](https://climate.onebuilding.org/sources/default.html)
page says that TMYx files are created by the site's authors from public ISD
data, with ERA5 solar data, and requests this citation:

```text
Lawrie, Linda K, Drury B Crawley. 2022. Development of Global Typical Meteorological Years (TMYx). https://climate.onebuilding.org
```

The [South Korea index](https://climate.onebuilding.org/WMO_Region_2_Asia/KOR_South_Korea/index.html)
publishes the relevant station archives. However, the official
[About](https://climate.onebuilding.org/about/default.html) and
[Contact](https://climate.onebuilding.org/contact/default.html) pages state
`Copyright (c) 2014-2026 Climate.OneBuilding. All Rights Reserved.` No reviewed
official page or file contains an open-data license or an express grant to
republish those files inside another software installer.

The source page also says that the TMYx solar fields use ERA5 data supplied
through Oikolab. The current general
[Copernicus Products licence](https://cds.climate.copernicus.eu/licences/licence-to-use-copernicus-products)
permits public distribution and adaptation, but requires clear Copernicus
attribution and a statement that neither the European Commission nor ECMWF is
responsible for downstream use. The exact archive includes neither notice, and
the reviewed public pages do not establish that this general licence rather
than separate Oikolab or Climate.OneBuilding terms governs every transformed
solar value. Oikolab's public
[API terms](https://docs.oikolab.com/terms/) grant ordinary API users only a
non-transferable, non-sublicensable right for internal business use. Those
public terms do not prove the terms of Oikolab's separate arrangement with
Climate.OneBuilding or grant downstream redistribution rights to this project.

Of the 80 reviewed EPWs, 59 embed `2021 ASHRAE Handbook -- Fundamentals -
Chapter 14 Climatic Design Information` in their `DESIGN CONDITIONS` records;
the other 21 declare zero design conditions.
[ASHRAE's permissions page](https://www.ashrae.org/permissions) directs software
developers to purchase a license to embed ASHRAE climatic design data in
derivative works. The eventual rights record must therefore either cover those
embedded records with a documented downstream grant, or the release must
separately resolve or remove that material.

Publicly accessible download and a requested citation establish source and
attribution, not redistribution permission. The result of this review is
therefore `BLOCKED_PENDING_CLIMATE_ONEBUILDING_REDISTRIBUTION_PERMISSION`.

## Release decision

Do not upload a SimpleDragon Yak, portable ZIP, combined Windows Installer, or
other binary that contains this archive until one of the following is recorded:

1. Written permission from Climate.OneBuilding or another party able to
   demonstrate the complete rights chain, expressly permitting bundling and
   redistribution of the exact reviewed TMYx EPWs in the MIT-licensed, free
   SimpleDragon package. The record must resolve the Oikolab/ERA5 pass-through
   terms, identify and include every applicable Copernicus attribution and
   no-responsibility statement, and cover or remove the embedded ASHRAE design
   conditions; or
2. Replacement weather files whose license expressly allows the intended
   public binary redistribution, followed by new file, count, metadata,
   compatibility, and package hashes.

The repository license and public support-email reviews are complete. This
complete weather rights chain is the only documented rights blocker, but
resolving it still does not itself create a tag, GitHub Release, Yak
publication, or Food4Rhino submission; each publication action remains
deliberate.

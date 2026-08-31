# Tracked product resources

This directory owns project-authored resources that are consumed by more than
one build or packaging workflow:

- `icons/` is the single source of truth for product artwork, generated
  Grasshopper icons, contact sheets, assembly resources, and package icons.
- `runtime/` declares the exact external EnergyPlus and KoreanTMY archives that
  setup validates and packages without committing or expanding those payloads.
  The KoreanTMY declaration also pins its Climate.OneBuilding origin,
  Oikolab/ERA5 and Copernicus provenance links, and current
  `blocked-permission-not-found` redistribution status; see the maintainer
  [weather rights review](../docs/development/publishing/weather-rights-review.md).

Generated packages belong under `artifacts`, downloaded payloads under
`.tools`, and disposable processing under `temp`.

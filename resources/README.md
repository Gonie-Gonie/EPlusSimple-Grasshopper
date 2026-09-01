# Tracked product resources

This directory owns project-authored resources that are consumed by more than
one build or packaging workflow:

- `icons/` is the single source of truth for product artwork, generated
  Grasshopper icons, contact sheets, assembly resources, and package icons.
- `runtime/` declares the exact external EnergyPlus and KoreanTMY archives that
  setup validates and packages without committing or expanding those payloads.
  The KoreanTMY declaration also pins its Climate.OneBuilding origin, TMYx
  dataset citation, archive identity, and address coverage. See the maintainer
  [weather provenance record](../docs/development/publishing/weather-provenance.md).

Generated packages belong under `artifacts`, downloaded payloads under
`.tools`, and disposable processing under `temp`.

# Climate.OneBuilding weather provenance

This maintainer record identifies the Korean TMYx weather archive embedded by
SimpleDragon. The canonical machine-readable values live in
`resources/runtime/distributions.json`; setup, packaging, and package
verification fail closed if the archive identity or provenance changes.

## Canonical payload

| Field | Value |
|---|---|
| Distribution ID | `korean-tmy-v1` |
| Archive | `KoreanTMY-v1.zip` |
| Byte length | `128349513` |
| SHA-256 | `fa88b8d69364b6a6b663afdc6dc2eb30c0ddee17cd37e5802ce5a5dec63d92d0` |
| Package path | `runtime/weather/KoreanTMY-v1.zip` |
| Product | `simple-dragon` |

## Dataset provenance

- Source: [Climate.OneBuilding](https://climate.onebuilding.org/)
- Dataset: TMYx
- South Korea index:
  <https://climate.onebuilding.org/WMO_Region_2_Asia/KOR_South_Korea/index.html>
- Citation: Lawrie, Linda K, Drury B Crawley. 2022. *Development of Global
  Typical Meteorological Years (TMYx).* <https://climate.onebuilding.org>
- Solar input recorded by the source metadata: ERA5 supplied through Oikolab

## Coverage and runtime behavior

The archive contains 80 root EPW files and covers all 78 unique EPW names
referenced by the tracked SimpleDragon address metadata. Setup stores the
validated archive below the repository-local tool state; packages embed the
archive unchanged and never include expanded EPW files. At runtime,
SimpleDragon resolves Address and Vintage internally, extracts only the selected
EPW into its per-user cache, validates it, and passes it to the managed
EnergyPlus workflow without exposing an EPW path on the Grasshopper canvas.

When the archive, address metadata, or source citation changes, update the
distribution declaration and all corresponding setup, package, and verifier
pins in the same commit.

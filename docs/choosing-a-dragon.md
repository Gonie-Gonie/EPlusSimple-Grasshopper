# Choosing a Dragon

The two products share an EnergyPlus execution path but intentionally model
geometry at different levels.

| Question | InvisibleDragon | SimpleDragon |
|---|---|---|
| Geometry | Preserves ordered planar vertices | Preserves area, azimuth, height, opening area, and boundary meaning |
| Primary use | Explicit EnergyPlus model authoring | Fast retrofit and parametric studies compatible with the SimpleDragon GRM abstraction |
| Rhino input | Planar polylines and Brep faces converted to vertex polygons | Breps reduced to the values used by the area-and-azimuth model |
| HVAC | Explicit source, tower, supply, ERV, PV, and zone assignments | Simplified source-to-supply-to-zone relationships converted to InvisibleDragon |
| Main model | `DragonEnergyModel` | `GreenRetrofitModel` (GRM) |
| Simulation path | Compile IDF, then run EnergyPlus | Convert GRM to InvisibleDragon/IDF, then run EnergyPlus |
| Existing files | IDF-oriented values and Dragon persistence | GRM/GRR read and write |

Choose InvisibleDragon when non-rectangular face vertices or direct control of
the generated EnergyPlus geometry matters. Choose SimpleDragon when the study
must retain the historical GRM concepts, regulation/profile data, rapid option
generation, batch evaluation, or its result aggregation.

SimpleDragon deliberately does not retain arbitrary Rhino vertices. Its
conversion preview exists so the exact InvisibleDragon surfaces produced by
the abstraction can be inspected. A geometry-loss diagnostic is information
about that boundary, not a failed conversion.

Neither product is a general-purpose editor for every EnergyPlus object. The
initial release exposes the construction, profile, geometry, HVAC, result, and
research workflows required by the pinned compatibility baseline.

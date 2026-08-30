# Choosing a Dragon

The two products share an EnergyPlus execution path but intentionally model
geometry at different levels.

| Question | InvisibleDragon | SimpleDragon |
|---|---|---|
| Geometry | Preserves ordered planar vertices | Preserves area, azimuth, height, opening area, and boundary meaning |
| Primary use | Explicit EnergyPlus model authoring | Fast retrofit and parametric studies compatible with the SimpleDragon GRM abstraction |
| Rhino input | Planar polylines and Brep faces converted to vertex polygons | Named planar face Breps become `SD Surface` values, then reduce to the area-and-azimuth model |
| HVAC | Explicit source, tower, supply, ERV, and PV object graph | Supply systems and ERVs connect directly to their owning `SD Zone` |
| Main model | `DragonEnergyModel` | `GreenRetrofitModel` (GRM) |
| Simulation path | Compile a typed IDF without paths, then consume typed IDF + verified Weather and manage EnergyPlus internally | Select Weather from Address/Vintage, prepare IDF, then hand both to InvisibleDragon |
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

For the shortest complete graph, author with SimpleDragon and execute with
InvisibleDragon: `SD Model -> SD to IDF -> Run InvisibleDragon`. Runtime, IDD,
EPW, and temporary-work paths are intentionally absent from that graph.

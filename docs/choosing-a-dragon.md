# Choosing a Dragon

The two products share an EnergyPlus execution path but intentionally model
geometry at different levels.

| Question | InvisibleDragon | SimpleDragon |
|---|---|---|
| Geometry | Preserves ordered planar vertices | Preserves area, azimuth, height, opening area, and boundary meaning |
| Primary use | Explicit EnergyPlus model authoring | Fast retrofit and parametric studies compatible with the SimpleDragon GRM abstraction |
| Rhino input | Planar polylines enter explicit `ID Floor`, `ID Ceiling`, or `ID Wall` components and become vertex polygons | Named planar face Breps enter explicit `SD Floor`, `SD Ceiling`, or `SD Wall` components, then reduce to the area-and-azimuth model |
| HVAC | Explicit source, tower, supply, ERV, and PV object graph | Supply systems and ERVs connect directly to their owning `SD Zone` |
| Main model | `DragonEnergyModel` | `GreenRetrofitModel` (GRM) |
| Simulation path | `ID Model -> Compile InvisibleDragon -> Run InvisibleDragon`, with `EPW File -> ID Weather -> Run InvisibleDragon` as the deliberate weather boundary | Connect the GRM directly to `Run SimpleDragon`; Weather, IDF, runtime execution, and GRR construction stay internal |
| Existing files | IDF-oriented values and Dragon persistence | GRM/GRR read and write |

Choose InvisibleDragon when non-rectangular face vertices or direct control of
the generated EnergyPlus geometry matters. Choose SimpleDragon when the study
must retain the historical GRM concepts, regulation/profile data, rapid option
generation, batch evaluation, or its result aggregation.

Both products expose Floor, Ceiling, and Wall authoring components instead of a
generic Surface component with a Type code. Boundary Condition and other finite
categories are selected by name. Their item-access geometry inputs naturally
vectorize Grasshopper lists and trees while preserving paths, and each Zone
consumes its owned Surface list branch by branch.

SimpleDragon deliberately does not retain arbitrary Rhino vertices. Its
conversion preview exists so the exact InvisibleDragon surfaces produced by
the abstraction can be inspected. A geometry-loss diagnostic is information
about that boundary, not a failed conversion.

Neither product is a general-purpose editor for every EnergyPlus object. The
initial release exposes the construction, profile, geometry, HVAC, result, and
research workflows required by the pinned compatibility baseline.

For the shortest complete graph, use `SD Model -> Run SimpleDragon -> GRR`.
Runtime, IDD, IDF, EPW, InvisibleDragon execution types, and temporary-work
paths are intentionally absent from that graph.

Standalone InvisibleDragon is intentionally more explicit. Its compiler still
manages EnergyPlus and IDD internally, but the author selects a local EPW and
passes it through `Verify InvisibleDragon Weather` (`ID Weather`) before
connecting the resulting typed Weather value to `Run InvisibleDragon`.
InvisibleDragon never guesses weather from an address and never acquires it.

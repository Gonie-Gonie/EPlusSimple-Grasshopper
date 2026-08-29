# Verified Grasshopper examples

This tool generates eight tracked Grasshopper definitions and two Rhino building
models with Rhino 7, then validates them inside real Rhino 7 and Rhino 8 hosts.
It is intentionally separate from the package host smoke gate.

Run validation from the repository root after `.\dev.cmd setup`:

```powershell
.\dev.cmd examples
```

Regenerate the canonical examples with Rhino 7, then validate them in both
supported host generations:

```powershell
.\dev.cmd examples -Generate
```

Rhino 7 is the canonical writer so the committed `.gh` and `.3dm` files remain
readable by the oldest supported host. Rhino 8 only writes round-trip copies
below `temp/`. Every build log, host log, summary, and round-trip artifact is
written below `temp/example-definitions/run-*`. Generation stages candidates
there and only replaces tracked files after they reopen and pass their checks.

Definition checks cover exact object/component identities, source order for
every wire and its exact total, source/target parameter names, runtime types and
access contracts, runtime errors, typed outputs, selected
Boolean/numeric results, outward envelope winding, solve, save, reopen, and
round trip. Hosts run from a disposable system-temp directory outside the
repository, and the result example proves that saved-document-relative file
paths do not depend on the host process working directory.
The SimpleDragon two-zone examples use one Brep and opening parameter per local
Zone cluster. Typed Opening definitions, HVAC, and ventilation feed Zone;
Zone definitions feed Model; and Model feeds the path-free Prepare component.
The standalone InvisibleDragon example follows the same rule: Window feeds its
owning Surface, Surface/HVAC/ERV feed Zone, and only Zone plus model-level PV
feed Model. The gate rejects any relationship-index or assignment-stage graph.
Example 12 is the complex composition/IDF authoring case with heat-pump/AHU,
boiler/radiator, ERV, and PV. Example 14 uses direct-Zone electric radiators as
the stable execution case and connects Prepare's typed IDF and verified Weather
outputs directly to the managed InvisibleDragon runner before
Result-to-GRR-to-CSV. Every persisted
execution, cancellation, overwrite, and export trigger remains false. Runtime,
IDD, weather-cache, and run-temp paths are implementation-owned and never appear
on the canonical canvas or in its manifest description.
Building-model checks cover metre units, layers, object names and user strings,
closed solid zone Breps, exact bounds, expected adjacency pairs, and closed
planar window curves. The two-zone graphs aggregate and validate all of their
separate internalized Brep and Curve parameters against
`30-two-zone-office.3dm`.

Use `-Target Rhino7` or `-Target Rhino8` for a single validation host. Generation
requires the default `-Target All`: Rhino 7 writes every canonical binary, then
Rhino 7 and Rhino 8 both validate it before the command succeeds. Custom Rhino
executable locations can be supplied with `-Rhino7Exe` and `-Rhino8Exe`.
Use `-EnergyPlusRoot` to select the runtime gate or `-SkipEnergyPlusWorkflow` to
verify the disabled state without executing a simulation. `-WeatherPath` remains
an optional legacy/test override; the canonical SimpleDragon examples select
and verify packaged weather internally from the Model Address/Vintage.

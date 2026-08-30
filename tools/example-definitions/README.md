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
The SimpleDragon two-Zone examples use one named single-face Brep parameter per
Surface. Fenestration Construction feeds Opening only; each completed Opening
feeds exactly one Surface, each completed Surface feeds exactly one Zone, and
each Zone-exclusive HVAC/ERV value feeds exactly one Zone. Zone definitions feed
Model. The complex authoring example ends at the complete GRM, while the
execution example feeds that GRM directly to `Run SimpleDragon`.
The standalone InvisibleDragon example follows the same rule: Window feeds its
owning Surface, Surface/HVAC/ERV feed Zone, and only Zone plus model-level PV
feed Model. It then shows `Model -> Compile -> Run` beside
`EPW File -> ID Weather -> Run`. The EPW File parameter has no persisted data
and Run, Cancel, and Force are False, so solve/save/reopen performs no weather
path access and starts no simulation. The gate rejects any relationship-index
or assignment-stage graph.
Example 12 is the complex model-authoring case with explicit Surface
type, construction, boundary, and opening ownership, a west-Zone heat-pump/AHU,
east-Zone boiler/radiator, dedicated ERVs, and PV. Example 14 uses dedicated
electric radiators and ERVs on the same Surface-to-Zone structure as
the stable execution case and connects Model directly to `Run SimpleDragon`,
whose GRR output feeds summary and CSV components. Conversion, IDF, weather,
EnergyPlus execution, and GRR construction remain internal. Every persisted
execution, cancellation, overwrite, and export trigger remains false. Runtime,
IDD, weather-cache, and run-temp paths are implementation-owned and never appear
on the canonical canvas or in its manifest description.
Building-model checks cover metre units, layers, object names and ownership user
strings, planar single-face Surface Breps, exact bounds and outward normals,
expected adjacency pairs, and closed planar window curves. The two-Zone graphs
aggregate and validate all separate internalized Surface Brep and Curve
parameters against `30-two-zone-office.3dm` without face-index selectors.

Use `-Target Rhino7` or `-Target Rhino8` for a single validation host. Generation
requires the default `-Target All`: Rhino 7 writes every canonical binary, then
Rhino 7 and Rhino 8 both validate it before the command succeeds. Custom Rhino
executable locations can be supplied with `-Rhino7Exe` and `-Rhino8Exe`.
Use `-EnergyPlusRoot` to select the runtime gate or `-SkipEnergyPlusWorkflow` to
verify the disabled state without executing a simulation. The gate passes its
verified runtime root and IDD to `Run SimpleDragon` through internal automation
environment values; they never become Grasshopper inputs. `-WeatherPath` is an
optional test-host-only EPW override carried through the same internal boundary.
Without these gate overrides, `Run SimpleDragon` keeps its managed LocalAppData
runtime bootstrap and packaged weather selection from the Model Address/Vintage.

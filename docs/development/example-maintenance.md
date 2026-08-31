# Maintaining the Grasshopper and Rhino examples

This page is for contributors who generate or verify the tracked binary
examples. Plugin users should use the [public example inventory and
recipes](../../examples/README.md).

## Automated generation and verification

Validate every tracked definition and model in both supported Rhino hosts:

```powershell
.\dev.cmd examples
```

Regenerate all canonical binaries with Rhino 7, then validate them in Rhino 7
and Rhino 8:

```powershell
.\dev.cmd examples -Generate
```

The gate runs its Rhino hosts from a disposable system-temp directory outside
the repository. It checks component identities, the exact persisted wire set
and total, typed outputs, selected Boolean/numeric results, outward Surface
winding after save/reopen, runtime errors, Grasshopper round trips, and the
document-relative GRR fixture path. For `.3dm` files it also checks metre units,
layer, object names and ownership attributes, single-face planar Breps, exact
bounds and normals, required Zone adjacencies, closed planar windows, and
equality between model geometry and the internalized two-Zone Grasshopper
inputs. Candidates, logs, summaries, and round-trip copies remain below the
short-path run directory `temp/e/<token>`.

For example 02, that topology check includes `ID Weather`, `Run
InvisibleDragon`, and every compile/weather/run wire. Its deliberately
data-empty EPW File parameter and unpressed action Buttons remain no-op through
solve, save, reopen, and round trip. When the required runtime gate runs,
automation extracts one verified EPW below `temp/`, injects it only into the
reopened in-memory document, and verifies first-run, cache, and cancellation
behavior in Rhino 7 and Rhino 8. The tracked definition remains blank and safe
to open.

When the verified distribution payloads and EnergyPlus runtime are available,
the gate also temporarily enables example 14 in memory and verifies internal
packaged-weather selection, its Floor/Ceiling/Wall-to-Zone electric-radiator
model, direct SimpleDragon Run-to-GRR execution, the default monthly graph,
CSV, cache, and cancellation in both hosts. All saved action Buttons remain at
their resting `False` value. Use `-SkipEnergyPlusWorkflow` to test the explicit
disabled state or `-EnergyPlusRoot` to select a runtime. The gate supplies that
root, its IDD, and an optional `-WeatherPath` test override only through
internal automation environment values; none are added as product path inputs.
Without gate overrides, the component uses its managed LocalAppData runtime
bootstrap and address-selected packaged weather. An unavailable prerequisite
is reported as `Not Run`, not as a successful simulation.

# Dragons runtime policy

EnergyPlus and weather payloads are not committed here. During repository
development, `.\dev.cmd setup` downloads and validates the two archives pinned
in `distributions.json` under `.tools\distributions`. With
`-InstallEnergyPlus`, it also extracts and validates the pinned portable runtime
under `.tools`.
Installed plugins use the same validated identity and can securely prepare it
under `%LOCALAPPDATA%\GonieGonie\BuildingEnergyRuntime`. Runtime identity is
based on hashes, not on a machine-specific path.

Canonical simulation work directories, copied input files, stdout/stderr, and
raw output live below `%TEMP%\GonieGonie\Dragons\energyplus-runs`. Successful
runs are removed after their result is parsed; failed or cancelled runs are
retained for diagnosis and can later be removed as disposable directories.
Repository build, test, and example work remains below `temp` and can be cleared
with `.\dev.cmd clean -TempOnly`.

Each candidate package copies one verified archive unchanged at its root:
InvisibleDragon uses `runtime/energyplus/` and SimpleDragon uses
`runtime/weather/`. Python and directly expanded EnergyPlus/EPW files remain
excluded. Public publication stays unauthorized until the licensing items in
`NOTICE.md` are resolved.

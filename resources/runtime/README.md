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
excluded. Written permission for the weather rights chain has not been
verified. The individual owner accepted that risk and authorized public
publication to proceed under the recorded
`owner-risk-accepted-unverified` status; this is not a claim that the payload
is openly licensed.

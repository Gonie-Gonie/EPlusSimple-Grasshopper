# Dragons runtime policy

EnergyPlus and weather payloads are not committed here. `setup.cmd` either
validates a compatible existing EnergyPlus 24.2.0 installation or prepares the
pinned portable runtime under `.tools`. Runtime identity is based on hashes, not
on a machine-specific path.

Simulation work directories, copied input files, stdout/stderr, and raw output
belong under `temp/runs/<run-id>` and can be removed as a single tree.

The release build copies only the shared Gonie-Gonie runtime bootstrap and
manifest into the lean InvisibleDragon and SimpleDragon Yak packages. Offline
ZIP creation remains disabled until all relevant source, EnergyPlus, and
weather redistribution notices have been verified.

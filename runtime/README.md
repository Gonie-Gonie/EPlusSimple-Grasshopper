# Dragons runtime policy

EnergyPlus and weather payloads are not committed here. `setup.cmd` either
validates a compatible existing EnergyPlus 24.2.0 installation or prepares the
pinned portable runtime under `.tools`. Runtime identity is based on hashes, not
on a machine-specific path.

Simulation work directories, copied input files, stdout/stderr, and raw output
belong under `temp/runs/<run-id>` and can be removed as a single tree.

The release packages copy only the shared Gonie-Gonie runtime bootstrap into
the lean InvisibleDragon and SimpleDragon plugin payloads. Plugin-only offline
ZIP creation is enabled, but those ZIPs deliberately contain no EnergyPlus
binary, EnergyPlus data file, or weather file. On the destination machine the
bootstrap validates/reuses a compatible EnergyPlus installation or prepares
the separately pinned runtime through the setup/runtime flow. Weather remains
source-specific and is never redistributed in these packages.

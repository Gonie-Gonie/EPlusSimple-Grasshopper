# Troubleshooting

## The Grasshopper tab does not appear

Close all Rhino processes, verify that the package targets the running Rhino
generation, and unblock the downloaded archive before reinstalling. Do not copy
only the GHA: its adjacent `GonieGonie.*` dependencies are required. Check
Grasshopper's loading/protection settings for the exact rejected path.

For a source checkout, rerun `.\dev.cmd setup` after installing Rhino, then run
`.\dev.cmd build`. Generated host logs are below `temp\grasshopper-smoke`; stable
build reports are below `artifacts\reports`.

## One Dragon loads and the other fails

This usually indicates mixed shared assemblies. Remove both product directories
with Rhino closed and reinstall both packages from the same release commit.
Do not merge DLLs from separate release candidates. `package-index.json` and
`checksums.sha256` identify the matching set.

## EnergyPlus is not found

Use Prepare EnergyPlus Runtime and create a new False-to-True Prepare edge. If
Ready remains False, inspect the structured diagnostic code and suggested
action. A custom target containing invalid files is not overwritten unless the
Repair input is explicitly enabled. The default managed cache can repair its
own invalid transaction safely.

## Preparation stays idle

This is expected when a saved Boolean is already True. Set it to False, allow
one Grasshopper solution, then set it to True. The same rule prevents Run,
Cancel, Write, Export, and Batch actions from repeating on document load or
ordinary recompute.

## The simulation has no weather or fails on EPW

EnergyPlus preparation installs only the runtime. Supply a real local EPW path
separately. SimpleDragon weather metadata may name an expected station file but
does not contain or download that file. Confirm that the EPW is readable and
appropriate for EnergyPlus 24.2.0.

## A model compiles but EnergyPlus reports severe errors

Read the structured Diagnostics output and retained `.err` file. Check source
and supply compatibility, zone assignments, duplicate explicit IDs, positive
capacities/flows, schedule ranges, construction references, and whether the
selected IDD is the pinned version. Keep the work directory for diagnosis and
remove it only after the case is understood.

## Cleaning local work

`.\dev.cmd clean` removes disposable `temp` output and generated artifacts
only after validating their repository paths. It preserves `.tools` and the
tracked artifact documentation. Never delete or edit a user's EPW, project GH
definition, GRM, GRR, or per-user runtime cache as part of repository cleanup.

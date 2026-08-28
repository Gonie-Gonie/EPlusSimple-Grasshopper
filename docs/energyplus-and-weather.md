# EnergyPlus and weather

## Pinned runtime

The supported runtime is EnergyPlus 24.2.0 build `94a887817b`. The executable,
IDD, ExpandObjects executable, and official Windows archive are pinned by size
and SHA-256. A directory that merely has the expected name is not trusted.

The installed-plugin default cache is:

```text
%LOCALAPPDATA%\GonieGonie\BuildingEnergyRuntime\EnergyPlus\24.2.0-94a887817b
```

Preparation is per-user and transactional. It does not require administrator
rights and does not modify a machine-wide EnergyPlus installation. Valid
existing runtimes are reused. InvisibleDragon candidates carry the unchanged
pinned archive, so preparation can use it without an administrator-level or
machine-wide install.

## Preparing from Grasshopper

Use `InvisibleDragon > Core > Prepare EnergyPlus Runtime`:

1. Leave Target Root empty for the managed cache.
2. Set Prepare to False once, then toggle it to True.
3. Observe State, Progress, Message, Ready, and Diagnostics.
4. Keep the verified Runtime Root output with the project record if a custom
   location was used.

The first observation of a True toggle is treated as a baseline. Consequently,
opening a saved Grasshopper document with Prepare=True cannot start a download.
An operation begins only after a new False-to-True edge. A held True value and
ordinary recomputes are coalesced. Cancel uses the same edge rule.

`Run EnergyPlus` first resolves a verified cache, explicit root, environment
hint, or conventional installation. Its optional `Prepare Missing Runtime`
input permits bootstrap only as part of a new explicit Run edge. Leave it False
when network acquisition must be administratively separated from simulation.

## Weather policy

Developer setup downloads and verifies the pinned `KoreanTMY-v1.zip`, and each
SimpleDragon candidate embeds that ZIP unchanged. It contains 80 root EPWs and
covers all 78 unique filenames referenced by the address database. The runtime
resolver may use an address-selected entry from this archive; workflows that
accept an explicit weather path may still use an appropriate user-supplied EPW.
Directly expanded EPW files are not stored in source control or packages.

This local candidate mechanism does not establish redistribution rights. Public
publication remains unauthorized as recorded in `NOTICE.md`.

## Security and recovery

Bootstrap accepts only the pinned HTTPS archive identity, rejects archive path
traversal, links, device paths, excessive entries/expanded size, and hash
mismatches, and promotes a verified staging directory atomically. Invalid
custom targets are preserved unless `Replace Invalid Custom Target` is
explicitly enabled. A failed or cancelled operation cleans its owned partial
and staging paths; the inert lock file may remain to prevent unlink/recreate
races.

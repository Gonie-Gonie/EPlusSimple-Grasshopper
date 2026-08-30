# Grasshopper host smoke gate

This tool loads the built InvisibleDragon and SimpleDragon GHAs inside real, non-UI Rhino/Grasshopper hosts. It is intentionally outside the production solution and does not alter the solution, central package versions, root build script, or production lock files.

Run both installed hosts from the repository root:

```powershell
.\dev.cmd smoke
```

Run one host, change the executable location, or tighten/extend the hard timeout:

```powershell
.\dev.cmd smoke -Host Rhino8 -TimeoutSeconds 90
.\dev.cmd smoke -Host Rhino7 -Rhino7Exe "D:\Rhino 7\System\Rhino.exe"
```

The historical command above remains the build-output `Both` scenario. Individual
module scenarios and the full three-scenario matrix are also available:

```powershell
.\dev.cmd smoke -Host Rhino8 -Scenario InvisibleOnly
.\dev.cmd smoke -Host Rhino7 -Scenario SimpleOnly
.\dev.cmd smoke -Scenario All
```

To gate the files users actually receive, first create packages and then run the
explicit portable-package mode. It safely extracts the one unambiguous portable
ZIP for each requested product below the run's `temp` directory and runs six fresh
hosts (three scenarios on each Rhino version):

```powershell
.\dev.cmd package
.\dev.cmd smoke -Source PortablePackage -Scenario All -Target All -SkipPluginBuild
```

`.\dev.cmd package -RunPortableHostGate` performs those two steps as the opt-in local
release gate. Normal packaging and CI runs do not require an installed Rhino.
The archive guards have a Rhino-free negative-test entry point:

```powershell
.\dev.cmd smoke -ArchiveSafetySelfTest
```

It proves traversal, trailing-dot/space, reserved `NUL`/`COM9` basenames with
extensions, case-ambiguous entries, and package-index SHA mismatch are rejected.

The default paths are the standard Rhino 7 and Rhino 8 installations. Run `.\dev.cmd setup` first so the pinned .NET SDK and restored production assets are available. Production GHAs are built with `--no-restore`, so the gate cannot rewrite their lock files. Pass `-SkipPluginBuild` only when the target-framework GHA outputs are already current.

## What is gated

Rhino 8 uses a .NET 8 STA process, `Rhino.Inside`, an in-process RhinoCore, and Grasshopper's `RunHeadless` initialization. Rhino 7 uses a .NET Framework 4.8 STA process, `Rhino.Inside 7`, and an in-process RhinoCore. No editor window is shown by either runner.

For each selected host the gate:

- loads the installed Rhino runtime and verifies its major version;
- registers exactly the scenario's GHA paths as Grasshopper external libraries;
- discovers every public component in each requested GHA and every public module
  parameter in its Types assembly, then verifies unique GUIDs, proxy origins, and
  emitted runtime types;
- rejects proxies or loaded GHA assemblies from the absent module;
- instantiates every discovered component and parameter;
- saves the complete proxy document and reopens it;
- verifies a representative persistent Goo/domain value for every present module.

The expectations are derived from the assemblies instead of hard-coded GUIDs or
counts, so adding a public component or module parameter automatically extends
both host gates. Each JSON summary records the scenario, artifact source, exact
GHA count and paths, discovered counts, and reopened Goo values. In portable mode,
all Dragon dependency origins must remain inside the extracted package payloads;
the host runners have no product project reference that could mask a missing DLL.
Portable archives must exactly match the artifact path and SHA-256 declared by
`artifacts/packages/package-index.json`. Summary schema v3 records a
`portableArchives` product/path/SHA entry for every source ZIP and a
`pluginArtifacts` product/path/SHA entry for every GHA the host actually loads.

Portable extraction rejects rooted/traversal paths, duplicate or case-ambiguous
entries, link/reparse entries, oversized content, multiple matching archives, and
Windows trailing-dot/space or reserved DOS-device aliases, plus package layouts
containing the other product's GHA. Shared InvisibleDragon
Core/Rhino DLLs are valid SimpleDragon implementation dependencies, but the
InvisibleDragon Grasshopper Types DLL and GHA are forbidden from the
SimpleDragon-only package.

Grasshopper's targeted `ParseGHA` entry point is not public, so both runners invoke that one host API through reflection after RhinoCore is active. The public all-external-files scan is intentionally not used: it can load unrelated user plug-ins and is not a bounded repository gate.

A managed-only Rhino 7 substitute is not safe. Without RhinoCore, component-server/document initialization either waits indefinitely or fails while loading `rhcommon_c`. The Rhino.Inside 7 host is therefore a required part of the Rhino 7 gate.

Every build log, host log, summary, and saved `.gh` document is written below `temp/grasshopper-smoke/run-*`. Each host process has a hard timeout and is terminated on expiry, so a failed plug-in load cannot leave this command waiting indefinitely.

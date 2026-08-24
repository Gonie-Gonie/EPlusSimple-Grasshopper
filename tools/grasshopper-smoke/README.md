# Grasshopper host smoke gate

This tool loads the built InvisibleDragon and SimpleDragon GHAs inside real, non-UI Rhino/Grasshopper hosts. It is intentionally outside the production solution and does not alter the solution, central package versions, root build script, or production lock files.

Run both installed hosts from the repository root:

```powershell
tools\grasshopper-smoke\run.cmd
```

Run one host, change the executable location, or tighten/extend the hard timeout:

```powershell
tools\grasshopper-smoke\run.cmd -Host Rhino8 -TimeoutSeconds 90
tools\grasshopper-smoke\run.cmd -Host Rhino7 -Rhino7Exe "D:\Rhino 7\System\Rhino.exe"
```

The default paths are the standard Rhino 7 and Rhino 8 installations. Run `setup.cmd` first so the pinned .NET SDK and restored production assets are available. Production GHAs are built with `--no-restore`, so the gate cannot rewrite their lock files. Pass `-SkipPluginBuild` only when the target-framework GHA outputs are already current.

## What is gated

Rhino 8 uses a .NET 8 STA process, `Rhino.Inside`, an in-process RhinoCore, and Grasshopper's `RunHeadless` initialization. Rhino 7 uses a .NET Framework 4.8 STA process, `Rhino.Inside 7`, and an in-process RhinoCore. No editor window is shown by either runner.

For each selected host the gate:

- loads the installed Rhino runtime and verifies its major version;
- loads both target-framework GHAs through the Grasshopper component server;
- verifies all 13 InvisibleDragon component proxies and all 10 custom parameter proxies;
- discovers every public SimpleDragon component and parameter type from the built assemblies, then verifies its unique GUID, registered proxy, and emitted runtime type;
- instantiates every discovered SimpleDragon component and parameter alongside representative InvisibleDragon workflow components;
- saves the complete proxy document and reopens it;
- verifies both an InvisibleDragon Goo and a representative SimpleDragon Material Goo/domain value survived persistence.

The SimpleDragon expectations are derived from the assemblies instead of a hard-coded count, so adding a new public component or parameter automatically extends both host gates. Each summary records the discovered counts and the reopened SimpleDragon Goo type and value.

Grasshopper's targeted `ParseGHA` entry point is not public, so both runners invoke that one host API through reflection after RhinoCore is active. The public all-external-files scan is intentionally not used: it can load unrelated user plug-ins and is not a bounded repository gate.

A managed-only Rhino 7 substitute is not safe. Without RhinoCore, component-server/document initialization either waits indefinitely or fails while loading `rhcommon_c`. The Rhino.Inside 7 host is therefore a required part of the Rhino 7 gate.

Every build log, host log, summary, and saved `.gh` document is written below `temp/grasshopper-smoke/run-*`. Each host process has a hard timeout and is terminated on expiry, so a failed plug-in load cannot leave this command waiting indefinitely.

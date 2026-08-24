# Verified Grasshopper examples

This tool generates the two tracked starter definitions with Rhino 7 and then
validates their components, wires, solution output, save, and reopen behavior
inside real Rhino 7 and Rhino 8 Grasshopper hosts. It is intentionally separate
from the package host smoke gate.

Run validation from the repository root after `setup.cmd`:

```powershell
tools\example-definitions\run.cmd
```

Regenerate the canonical examples with Rhino 7, then validate them in both
supported host generations:

```powershell
tools\example-definitions\run.cmd -Generate
```

Rhino 7 is the canonical writer so the committed files remain readable by the
oldest supported host. Rhino 8 only writes a round-trip copy below `temp/`.
Every build log, host log, summary, and round-trip definition is written below
`temp/example-definitions/run-*`. Generation stages a candidate below that
directory and only replaces the tracked file after the candidate reopens and
passes its graph checks.

Use `-Target Rhino7` or `-Target Rhino8` for a single validation host. The
`-Generate` option always invokes Rhino 7 in addition to the selected validation
target. Custom Rhino executable locations can be supplied with `-Rhino7Exe` and
`-Rhino8Exe`.

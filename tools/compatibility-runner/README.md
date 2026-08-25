# Engineering compatibility runner

`dev.cmd compatibility` runs the pinned Python 0.7.0 implementation and the C# port with the
same tracked GRM, exact EPW bytes, and hash-verified EnergyPlus 24.2.0 runtime. Each engine emits
authoring IDF, expanded IDF, warnings, and full GRR output. The reporter performs order-independent
IDD-aware semantic IDF matching and tolerance-aware numeric GRR comparison, then writes
`artifacts/reports/engineering-compatibility.json`.

The reporter requires the repository runtime manifest and rejects a reporter IDD or engine runtime
whose executable, IDD, or ExpandObjects hash is not the pinned EnergyPlus 24.2.0 identity.

The normal command is a gate and exits nonzero on any difference:

```text
dev.cmd compatibility
dev.cmd compatibility -Case ashrae-140-modified
```

During port development, retain a failing structured report without weakening the release gate:

```text
dev.cmd compatibility -AllowDifferences
```

All simulation scratch data remains under `temp/compatibility/`. Python is used only as the
historical oracle and is never a runtime dependency of either Grasshopper package.

Reporter regression tests are disposable and run entirely under `temp/`:

```text
tests\CompatibilityRunner\run-tests.cmd
```

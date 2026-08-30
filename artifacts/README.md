# Generated deliverables

`dev.cmd build` recreates this directory as the stable hand-off location for current
build results. Generated contents are intentionally ignored by Git.

Expected layout once the plugin projects are available:

```text
artifacts/
|-- invisible-dragon/
|   |-- rhino7/net48/
|   `-- rhino8/
|       |-- net7.0/
|       `-- net8.0/
|-- simple-dragon/
|   |-- rhino7/net48/
|   `-- rhino8/
|       |-- net7.0/
|       `-- net8.0/
|-- packages/
|   |-- invisible-dragon/{yak,portable}/
|   |-- simple-dragon/{yak,portable}/
|   |-- compatibility-report.json
|   `-- checksums.sha256
`-- reports/
    |-- build-manifest.json
    `-- test-summary.json
```

Delete the generated files at any time and run `dev.cmd build` to reproduce them.
Run `dev.cmd package` to recreate transient verification stages plus the retained
Yak distributions and portable plugin ZIPs after normal build artifacts are
available. Successful package runs remove their scratch stages automatically.

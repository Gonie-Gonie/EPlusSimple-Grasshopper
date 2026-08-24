# Generated deliverables

`build.cmd` recreates this directory as the stable hand-off location for current
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
|   |-- invisible-dragon/{stage,yak,offline}/
|   |-- simple-dragon/{stage,yak,offline}/
|   |-- compatibility-report.json
|   `-- checksums.sha256
`-- reports/
    |-- build-manifest.json
    `-- test-summary.json
```

Delete the generated files at any time and run `build.cmd` to reproduce them.
Run `package.cmd` to reproduce the lean package stages, Yak distributions, and
plugin-only offline ZIPs after the normal build artifacts are available.

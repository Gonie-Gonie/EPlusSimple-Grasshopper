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
`-- reports/
    |-- build-manifest.json
    `-- test-summary.json
```

Delete the generated files at any time and run `build.cmd` to reproduce them.

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
|-- documentation/
|   |-- Dragons-Grasshopper-User-Guide-0.1.2.pdf
|   `-- Dragons-Grasshopper-Food4Rhino-Metadata-0.1.2.pdf
|-- release/
|   |-- github-assets/
|   |   |-- Dragons-Grasshopper-0.1.2-Windows-Installer.zip
|   |   |-- Dragons-Grasshopper-User-Guide-0.1.2.pdf
|   |   |-- Dragons-Grasshopper-Food4Rhino-Metadata-0.1.2.pdf
|   |   `-- SHA256SUMS.txt
|   |-- release-assets-manifest.json
|   `-- release-gate.json
`-- reports/
    |-- build-manifest.json
    `-- test-summary.json
```

Generated contents can be deleted at any time. Each command owns and recreates
its corresponding subtree: `dev.cmd build` resets and stages plugin binaries and
reports, `dev.cmd package` recreates the package subtree after a build, and
`dev.cmd docs` recreates both OODocs PDFs in the documentation subtree from
current runtime metadata and the tracked authored chapters and publishing
worksheet. `dev.cmd release` assembles the exact four-file `github-assets`
candidate set and keeps its internal manifest outside that directory. Because
a new build resets generated artifacts, run package and docs after build when
preparing a complete hand-off. Successful package runs remove their scratch
stages automatically.

The generated version is deliberately fixed at `0.1.2`; its matching release
tag is `v0.1.2`. These are local, ignored artifacts. Their presence alone does
not mean that a tag, GitHub release, Yak package, or Food4Rhino listing has been
published; public installation files are the copies attached to the matching
GitHub release.

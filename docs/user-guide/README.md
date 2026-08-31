# Dragon user-guide sources

The distributable PDF is assembled from these Markdown chapters in this order:

1. `01-workflow.md`
2. `02-in-out-reference.md`
3. `03-compatibility.md`
4. `04-release-notes.md`

This README is not included in the PDF. The In/Out reference is generated from
the current public Grasshopper catalog; the other chapters are maintained as
task-oriented user guidance.

From the repository root, prepare the repository-local documentation
environment once and then build the PDF:

```text
.\dev.cmd setup
.\dev.cmd docs
```

Setup creates the isolated environment at `.tools\venv` and pins Python
3.12.7 plus OODocs 1.3.0 from the repository lock. Do not install these
documentation dependencies into Rhino's Python environment.

The only distributable documentation output is:

```text
artifacts\documentation\Dragons-Grasshopper-User-Guide-0.1.0.pdf
```

`dev.cmd docs` verifies the isolated Python environment, rebuilds and extracts
the current Rhino 7 and Rhino 8 component catalogs, and requires their public
Grasshopper contracts to match. The Python builder reads those catalogs into
immutable native dataclasses, joins them 1:1 with the authored practical guide
records, generates the complete In/Out reference, and renders the combined
guide with OODocs. Temporary catalog and log files remain under
`temp\documentation` and are not part of the user deliverable.

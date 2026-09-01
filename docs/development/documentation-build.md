# Building the Dragon documentation PDFs

`dev.cmd docs` creates two PDF-only OODocs outputs for different audiences. The
user guide is assembled from the externally visible Markdown chapters under
`docs/user/user-guide` in this order:

1. `01-workflow.md`
2. `02-in-out-reference.md`
3. `03-compatibility.md`
4. `04-release-notes.md`

This development page is not included in the user guide. The In/Out reference
is generated from the current public Grasshopper catalog and carries a
generated warning; the other chapters are maintained as task-oriented user
guidance. The second PDF renders the maintainer-owned
`docs/development/publishing/food4rhino.md` worksheet. It preserves the
copy/select/upload fields and the canonical runtime and weather provenance; it
is release metadata, not plugin-user guidance.

From the repository root, prepare the repository-local documentation
environment once and then build both PDFs:

```text
.\dev.cmd setup
.\dev.cmd docs
```

Setup creates the isolated environment at `.tools\venv` and pins Python
3.12.7 plus OODocs 1.3.0 from the repository lock. Do not install these
documentation dependencies into Rhino's Python environment.

The outputs are:

```text
artifacts\documentation\Dragons-Grasshopper-User-Guide-0.1.2.pdf
artifacts\documentation\Dragons-Grasshopper-Food4Rhino-Metadata-0.1.2.pdf
```

`dev.cmd docs` verifies the isolated Python environment, rebuilds and extracts
the current Rhino 7 and Rhino 8 component catalogs, and requires their public
Grasshopper contracts to match. The Python builder reads those catalogs into
immutable native dataclasses, joins them 1:1 with the authored practical guide
records, generates the complete In/Out reference, and renders the combined
guide with OODocs. A separate Python-native builder imports the canonical
Food4Rhino Markdown, renders it with the same locked OODocs environment, and
postflights its PDF metadata, headings, field values, provenance, and copy
blocks. Temporary catalog and log files remain under `temp\documentation` and
are not part of either deliverable.

Both filenames are bound to `packaging/package-spec.json`. The first-release
source and builders deliberately require `0.1.2`; this is not a command-line
version override. Before publication, maintainers must make a final deliberate
version decision in source. For version `0.1.2`, the only matching repository
tag is `v0.1.2`.

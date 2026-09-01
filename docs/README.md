# Documentation

The documentation tree has two explicit audiences. Do not place a maintainer
procedure in the user tree or link a development worksheet as user guidance.

| Audience | Start here | Contents |
|---|---|---|
| Plugin users and public project pages | [User documentation](user/README.md) | Installation, product choice, Grasshopper workflows, runtime/weather behavior, troubleshooting, examples, and the four externally distributed user-guide chapters |
| Contributors and release maintainers | [Development documentation](development/README.md) | Reproducible setup, build/test workflows, compatibility policy, OODocs generation, packaging, release gates, and publishing worksheets |

The PDF-only OODocs build creates two version-bound artifacts:

- `artifacts/documentation/Dragons-Grasshopper-User-Guide-0.1.2.pdf` is the
  externally visible user guide. Its authored and generated Markdown chapters
  live under [`user/user-guide`](user/user-guide/01-workflow.md).
- `artifacts/documentation/Dragons-Grasshopper-Food4Rhino-Metadata-0.1.2.pdf`
  is the maintainer publishing worksheet rendered from
  [`development/publishing/food4rhino.md`](development/publishing/food4rhino.md).
  It is not normal plugin-user guidance.

The build procedure lives only in the [development tree](development/documentation-build.md).
Locally built copies are candidates; the copies attached to the matching
`v0.1.2` GitHub release are the public release assets.

Repository-wide legal and release records remain at the conventional root
locations: [LICENSE](../LICENSE), [NOTICE](../NOTICE.md), and
[CHANGELOG](../CHANGELOG.md).

# Documentation

The documentation tree has two explicit audiences. Do not place a maintainer
procedure in the user tree or link a development worksheet as user guidance.

| Audience | Start here | Contents |
|---|---|---|
| Plugin users and public project pages | [User documentation](user/README.md) | Installation, product choice, Grasshopper workflows, runtime/weather behavior, troubleshooting, examples, and the four externally distributed user-guide chapters |
| Contributors and release maintainers | [Development documentation](development/README.md) | Reproducible setup, build/test workflows, compatibility policy, OODocs generation, packaging, release gates, and publishing worksheets |

The generated PDF remains the only bundled documentation artifact:
`artifacts/documentation/Dragons-Grasshopper-User-Guide-0.1.0.pdf`.
Its authored and generated Markdown chapters live under
[`user/user-guide`](user/user-guide/01-workflow.md); the build procedure lives
only in the development tree.

Repository-wide legal and release records remain at the conventional root
locations: [LICENSE](../LICENSE), [NOTICE](../NOTICE.md), and
[CHANGELOG](../CHANGELOG.md).

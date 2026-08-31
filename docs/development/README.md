# Dragon development documentation

This tree is for contributors, maintainers, verification work, and publication
operations. Nothing here should be presented as a normal plugin-user step.
Plugin users should start with the [user documentation](../user/README.md).

## Reproducible local workflow

Run every supported workflow through the single root wrapper:

```text
.\dev.cmd setup
.\dev.cmd build
```

`setup` selects or installs the pinned .NET SDK and CPython under `.tools`,
creates the isolated documentation/reference venv, verifies its hash-locked
dependencies, prepares pinned distribution archives, and detects Rhino 7 and
Rhino 8 independently. It is safe to rerun after installing another Rhino
generation.

| Command | Maintainer purpose |
|---|---|
| `dev.cmd setup` | Prepare the reproducible SDK, Python/OODocs, Rhino, EnergyPlus, and embedded-archive environment |
| `dev.cmd build` | Restore, compile all host targets, test, and stage plugin outputs |
| `dev.cmd reference` | Generate or verify the pinned historical Python oracle |
| `dev.cmd compatibility` | Compare the Python/C# engineering cases and EnergyPlus outputs |
| `dev.cmd examples` | Generate and round-trip the tracked `.gh` and `.3dm` examples |
| `dev.cmd docs` | Reflect every public component contract and build both OODocs PDFs |
| `dev.cmd package` | Produce verified Yak and portable ZIP candidates without publishing |
| `dev.cmd install` | Replace only local Dragon packages in detected Rhino installations |
| `dev.cmd release` | Build a clean, pushed, fully attested local release candidate |
| `dev.cmd clean` | Remove disposable work while preserving reusable `.tools` state |

Use `dev.cmd help` for command options. Build intermediates, diagnostics, and
simulations stay under `temp`; generated deliverables stay under `artifacts`;
reusable toolchains and local settings stay under `.tools`. Normal workflows
retain only useful recent diagnostics, and `dev.cmd clean -TempOnly` removes
the complete disposable tree.

## Maintainer contracts

- [Compatibility policy](compatibility-policy.md): declared port scope,
  historical baseline, evidence boundaries, and current limitations.
- [Building the documentation PDFs](documentation-build.md): repository-local
  Python/OODocs environment, runtime metadata extraction, generated reference,
  Food4Rhino worksheet, and the two version-bound PDF outputs.
- [Release checklist](release-checklist.md): clean-source, host, package,
  evidence, legal, and publication gates.
- [Example maintenance](example-maintenance.md): canonical binary generation,
  Rhino-host round trips, runtime gates, and disposable evidence.
- [Food4Rhino publishing sheet](publishing/food4rhino.md): the single
  copy/paste surface for both future Food4Rhino App records.

## Co-located technical references

Implementation-specific documentation remains beside the system it describes:

- [Public example inventory and recipes](../../examples/README.md)
- [Packaging](../../packaging/README.md)
- [Icon generation](../../resources/icons/README.md)
- [Component catalog and PDF tooling](../../tools/documentation/build_user_guide.py)
- [Grasshopper definition generator](../../tools/example-definitions/README.md)
- [Grasshopper host smoke tests](../../tools/grasshopper-smoke/README.md)
- [Engineering compatibility runner](../../tools/compatibility-runner/README.md)
- [Historical Python reference](../../tools/python-reference/README.md)
- [Upstream tracker](../../tools/upstream-tracker/README.md)

The standard root [LICENSE](../../LICENSE), [NOTICE](../../NOTICE.md), and
[CHANGELOG](../../CHANGELOG.md) remain authoritative for legal and release
history. The future GitHub release contract is exactly one Installer ZIP, the
user guide PDF, the Food4Rhino metadata PDF, and `SHA256SUMS.txt`, all bound to
the deliberately selected package version and matching repository tag. A local
candidate does not authorize public publication.

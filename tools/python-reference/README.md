# Python reference oracle

The released Grasshopper plugins do not depend on Python. This tool runs the
historical `epsimple` and `idragon` implementation only as a behavioral oracle
for the C# port.

`reference.cmd` verifies the exact Python 3.12.7 interpreter selected by
`setup.cmd`, materializes the commit in `upstream/upstream.lock.json`, installs
the exact requirements into `.tools/python-reference`, and writes all generated
work to `temp/reference/python-output`.

```text
reference.cmd
reference.cmd -Mode Verify
reference.cmd -RefreshDependencies
```

The generator fixes `PYTHONHASHSEED`, removes CPython memory addresses from the
generated IDF with a stable first-occurrence mapping, and records hashes for
every output. `-Mode Verify` compares those files byte-for-byte with the
reviewed baseline under `fixtures/reference/python-0.7.0`.

Updating the tracked baseline is an explicit review action:

```text
reference.cmd -UpdateBaseline
```

For auditing an already available exact checkout without allowing the script
to change it, pass `-UpstreamPath <path>`. An explicit checkout must already be
clean, have the pinned origin, and be at the pinned commit.

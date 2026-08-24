# Packaging sources

The `invisible-dragon` and `simple-dragon` manifests in this directory are
Gonie-Gonie-owned, source-controlled inputs. `build.cmd` creates fresh stage
directories under `temp/packaging`, copies only the required assemblies and
notices, invokes the pinned Yak CLI, and publishes final files to `artifacts`.

Rhino 7 payloads use the `net48` build. Rhino 8 payloads include `net7.0` for
Rhino 8.0-8.19 and `net8.0` for Rhino 8.20 and later. Distribution tags and
framework directories are verified explicitly so Rhino selects the matching
runtime assembly.

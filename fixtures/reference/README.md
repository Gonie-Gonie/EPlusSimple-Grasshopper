# Reference baselines

This directory contains deterministic outputs generated from the exact
historical Python source pinned in `upstream/upstream.lock.json`. They are
development compatibility fixtures, not runtime dependencies of either Dragon
plugin. Regenerate into `temp` with `.\dev.cmd reference`; update tracked files only
after reviewing intentional compatibility changes.

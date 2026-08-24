"""Stdlib-only launcher for both full and embeddable CPython distributions."""

from __future__ import annotations

import argparse
import runpy
import sys
from pathlib import Path


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dependency-root", type=Path, required=True)
    parser.add_argument("--upstream-source", type=Path, required=True)
    parser.add_argument("--generator", type=Path, required=True)
    parser.add_argument("arguments", nargs=argparse.REMAINDER)
    args = parser.parse_args()

    for required in (args.dependency_root, args.upstream_source, args.generator):
        if not required.exists():
            raise SystemExit(f"Required reference path does not exist: {required}")

    # The official embeddable Windows distribution ignores PYTHONPATH. Insert
    # both isolated roots explicitly before any third-party or upstream import.
    sys.path[:0] = [str(args.dependency_root), str(args.upstream_source)]
    forwarded = list(args.arguments)
    if forwarded and forwarded[0] == "--":
        forwarded.pop(0)
    sys.argv = [str(args.generator), *forwarded]
    runpy.run_path(str(args.generator), run_name="__main__")


if __name__ == "__main__":
    main()

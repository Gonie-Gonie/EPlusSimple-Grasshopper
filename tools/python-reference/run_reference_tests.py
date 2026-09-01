"""Run Python reference tests behind the repo-local oracle process boundary."""

from __future__ import annotations

import argparse
from collections.abc import Mapping, Sequence
import os
from pathlib import Path
import subprocess
import sys
import unittest
from typing import Any


_BOOTSTRAP_NAME = "bootstrap_reference.py"
_EXPECTED_BOOTSTRAP = Path(__file__).resolve().with_name(_BOOTSTRAP_NAME)
_EXACT_INTERPRETER_PREFIX = ("-S", "-P", "-B", "-X", "utf8")
_ALLOWED_SOURCE_PREFIXES = frozenset(
    {
        (),
        ("-B", "-X", "utf8"),
        ("-X", "utf8"),
        _EXACT_INTERPRETER_PREFIX,
    }
)
_ORIGINAL_RUN = getattr(
    subprocess.run,
    "_dragons_original_run",
    subprocess.run,
)


def _path_identity(value: object) -> str:
    return os.path.normcase(os.path.abspath(os.fsdecode(os.fspath(value))))


def _bootstrap_index(command: Sequence[object]) -> int | None:
    matches: list[int] = []
    for index, item in enumerate(command):
        try:
            name = Path(os.fsdecode(os.fspath(item))).name
        except TypeError:
            continue
        if name.casefold() == _BOOTSTRAP_NAME.casefold():
            matches.append(index)
    if not matches:
        return None
    if len(matches) != 1:
        raise RuntimeError("Oracle subprocess command contains multiple bootstrap paths.")
    return matches[0]


def normalize_bootstrap_command(arguments: object) -> list[object] | None:
    """Return an exact isolated command, or ``None`` for a non-oracle command."""

    if isinstance(arguments, (str, bytes, os.PathLike)):
        if _BOOTSTRAP_NAME.casefold() in os.fsdecode(os.fspath(arguments)).casefold():
            raise RuntimeError("Oracle bootstrap commands must use an argument sequence.")
        return None
    if not isinstance(arguments, Sequence):
        return None
    command = list(arguments)
    bootstrap_index = _bootstrap_index(command)
    if bootstrap_index is None:
        return None
    if bootstrap_index < 1:
        raise RuntimeError("Oracle bootstrap command has no Python executable.")
    if _path_identity(command[bootstrap_index]) != _path_identity(_EXPECTED_BOOTSTRAP):
        raise RuntimeError("Oracle subprocess command uses an unreviewed bootstrap path.")
    if _path_identity(command[0]) != _path_identity(sys.executable):
        raise RuntimeError(
            "Oracle bootstrap command must use the setup-selected Python executable."
        )
    source_prefix = tuple(str(item) for item in command[1:bootstrap_index])
    if source_prefix not in _ALLOWED_SOURCE_PREFIXES:
        raise RuntimeError(
            "Oracle bootstrap command has an unreviewed interpreter prefix: "
            + repr(source_prefix)
        )
    return [
        command[0],
        *_EXACT_INTERPRETER_PREFIX,
        *command[bootstrap_index:],
    ]


def isolated_environment(
    supplied: Mapping[str, str] | None,
) -> dict[str, str]:
    """Copy a child environment and remove host Python search-path controls."""

    source = os.environ if supplied is None else supplied
    environment = {
        str(key): str(value)
        for key, value in source.items()
        if str(key).upper() not in {"PYTHONHOME", "PYTHONPATH"}
    }
    environment.update(
        {
            "PYTHONDONTWRITEBYTECODE": "1",
            "PYTHONHASHSEED": "0",
            "PYTHONUTF8": "1",
        }
    )
    return environment


def run_isolated_bootstrap(
    arguments: object,
    *popen_arguments: Any,
    **kwargs: Any,
) -> subprocess.CompletedProcess[Any]:
    """Delegate to ``subprocess.run`` while isolating bootstrap invocations."""

    normalized = normalize_bootstrap_command(arguments)
    if normalized is None:
        return _ORIGINAL_RUN(arguments, *popen_arguments, **kwargs)
    if kwargs.get("shell"):
        raise RuntimeError("Oracle bootstrap commands cannot use a shell.")
    kwargs["env"] = isolated_environment(kwargs.get("env"))
    return _ORIGINAL_RUN(normalized, *popen_arguments, **kwargs)


setattr(run_isolated_bootstrap, "_dragons_original_run", _ORIGINAL_RUN)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--start-directory", type=Path, required=True)
    parser.add_argument("--pattern", default="test_*.py")
    parser.add_argument("--verbosity", type=int, default=2)
    args = parser.parse_args()

    if not args.start_directory.is_dir():
        raise SystemExit(
            f"Python reference test directory does not exist: {args.start_directory}"
        )

    subprocess.run = run_isolated_bootstrap
    try:
        suite = unittest.defaultTestLoader.discover(
            str(args.start_directory),
            pattern=args.pattern,
        )
        result = unittest.TextTestRunner(verbosity=args.verbosity).run(suite)
    finally:
        subprocess.run = _ORIGINAL_RUN
    return 0 if result.wasSuccessful() else 1


if __name__ == "__main__":
    raise SystemExit(main())

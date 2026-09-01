"""Exclusive temporary-file writes for tracker-generated text artifacts."""

from __future__ import annotations

import os
from pathlib import Path
import tempfile


def write_text_atomically(path: Path, content: str) -> None:
    """Write UTF-8 text without following attacker-planted temporary links."""

    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor = -1
    temporary: Path | None = None
    try:
        descriptor, name = tempfile.mkstemp(
            dir=path.parent,
            prefix=f".{path.name}.",
            suffix=".tmp",
        )
        temporary = Path(name)
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
            descriptor = -1
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        metadata = os.stat(temporary, follow_symlinks=False)
        if metadata.st_nlink != 1 or temporary.is_symlink():
            raise OSError("exclusive temporary output acquired an unexpected link")
        os.replace(temporary, path)
        temporary = None
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        if temporary is not None:
            try:
                temporary.unlink()
            except FileNotFoundError:
                pass

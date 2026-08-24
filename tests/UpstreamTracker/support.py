from __future__ import annotations

from pathlib import Path
import shutil
import sys
import uuid


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
TOOL_ROOT = REPOSITORY_ROOT / "tools" / "upstream-tracker"
if str(TOOL_ROOT) not in sys.path:
    sys.path.insert(0, str(TOOL_ROOT))


class TemporaryWorkspace:
    def __init__(self) -> None:
        base = REPOSITORY_ROOT / "temp" / "tests" / "upstream-tracker"
        base.mkdir(parents=True, exist_ok=True)
        self.path = base / uuid.uuid4().hex
        self.path.mkdir()

    def __enter__(self) -> "TemporaryWorkspace":
        return self

    def __exit__(self, exception_type, exception, traceback) -> None:
        resolved = self.path.resolve()
        expected = (REPOSITORY_ROOT / "temp" / "tests" / "upstream-tracker").resolve()
        resolved.relative_to(expected)
        shutil.rmtree(resolved)

    def write(self, relative: str, text: str) -> Path:
        target = self.path.joinpath(*relative.split("/"))
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8", newline="\n")
        return target


def write_configuration(
    workspace: TemporaryWorkspace,
    *,
    mapping_symbol: str = "Service.run",
    project: str = "GonieGonie.InvisibleDragon.Core",
    mapping_path: str = "src/source/service.py",
    exception_symbol: str | None = None,
) -> tuple[Path, Path, Path]:
    lock = workspace.write(
        "config/upstream.lock.json",
        """{
  "schema": "goniegonie.upstream-lock.v1",
  "repository": "https://example.invalid/historical-source.git",
  "branch": "main",
  "commit": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "checked_at": "2026-08-24",
  "modules": {
    "source": {
      "version": "1.0.0",
      "paths": ["src/source"]
    }
  },
  "fixtures": {}
}
""",
    )
    port_map = workspace.write(
        "config/port-map.yml",
        f"""- upstream:
    path: {mapping_path}
    symbol: {mapping_symbol}
  dotnet:
    project: {project}
    file: Model/Service.cs
    symbol: GonieGonie.InvisibleDragon.Model.Service
  tests:
    - ServiceParityTests
  status: equivalent
""",
    )
    if exception_symbol is None:
        exception_source = """- id: source-export-policy
  upstream:
    path: null
    symbol: null
  difference:
    upstream: source behavior
    dotnet: reviewed GonieGonie behavior
  effect:
    engineering_result: none
  approval: accepted
"""
    else:
        exception_source = f"""- id: reviewed-service-difference
  upstream:
    path: src/source/service.py
    symbol: {exception_symbol}
  difference:
    upstream: source behavior
    dotnet: reviewed GonieGonie behavior
  effect:
    engineering_result: none
  approval: accepted
"""
    exceptions = workspace.write("config/compatibility-exceptions.yml", exception_source)
    return lock, port_map, exceptions

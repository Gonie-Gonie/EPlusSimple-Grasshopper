"""A strict, data-only YAML subset used by the repository tracking manifests.

The accepted grammar intentionally covers only nested mappings, lists, and
scalar values. Anchors, aliases, tags, directives, and block strings are not
accepted. This keeps the tracker dependency-free and prevents configuration
files from acquiring executable or environment-dependent semantics.
"""

from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path
import re
from typing import Any

from .errors import ConfigurationError


_KEY_VALUE = re.compile(r"^(?P<key>[A-Za-z0-9_.-]+):(?P<value>.*)$")
_INTEGER = re.compile(r"^[+-]?(?:0|[1-9][0-9]*)$")
_FLOAT = re.compile(
    r"^[+-]?(?:(?:[0-9]+\.[0-9]*)|(?:[0-9]*\.[0-9]+)|(?:[0-9]+[eE][+-]?[0-9]+))(?:[eE][+-]?[0-9]+)?$"
)


@dataclass(frozen=True)
class _Line:
    number: int
    indent: int
    content: str


def load_yaml_subset(path: Path) -> Any:
    """Load a strict YAML data document from *path*."""

    try:
        text = path.read_text(encoding="utf-8-sig")
    except OSError as exception:
        raise ConfigurationError(f"Cannot read configuration '{path}': {exception}") from exception
    return parse_yaml_subset(text, source_name=str(path))


def parse_yaml_subset(text: str, *, source_name: str = "<yaml>") -> Any:
    """Parse the supported YAML subset and return plain Python data."""

    lines: list[_Line] = []
    for number, raw_line in enumerate(text.splitlines(), start=1):
        if "\t" in raw_line:
            raise ConfigurationError(f"{source_name}:{number}: tabs are not allowed")
        content = raw_line.lstrip(" ")
        if not content or content.startswith("#"):
            continue
        indent = len(raw_line) - len(content)
        if indent % 2:
            raise ConfigurationError(
                f"{source_name}:{number}: indentation must use multiples of two spaces"
            )
        lines.append(_Line(number, indent, content.rstrip()))

    if not lines:
        raise ConfigurationError(f"{source_name}: configuration is empty")
    if lines[0].indent != 0:
        raise ConfigurationError(f"{source_name}:{lines[0].number}: root must start at column zero")
    if len(lines) == 1 and lines[0].content.startswith(("[", "{")):
        try:
            return json.loads(lines[0].content)
        except json.JSONDecodeError as exception:
            raise ConfigurationError(
                f"{source_name}:{lines[0].number}: invalid inline collection: {exception.msg}"
            ) from exception

    parser = _Parser(lines, source_name)
    result = parser.parse_block(0)
    if not parser.finished:
        line = parser.current
        raise ConfigurationError(f"{source_name}:{line.number}: unexpected indentation")
    return result


class _Parser:
    def __init__(self, lines: list[_Line], source_name: str) -> None:
        self._lines = lines
        self._source_name = source_name
        self._index = 0

    @property
    def finished(self) -> bool:
        return self._index >= len(self._lines)

    @property
    def current(self) -> _Line:
        return self._lines[self._index]

    def parse_block(self, indent: int) -> Any:
        if self.finished or self.current.indent != indent:
            self._error("expected a nested value")
        return self._parse_list(indent) if self.current.content.startswith("-") else self._parse_mapping(indent)

    def _parse_list(self, indent: int) -> list[Any]:
        result: list[Any] = []
        while not self.finished and self.current.indent == indent:
            line = self.current
            if not line.content.startswith("-"):
                break
            if len(line.content) > 1 and line.content[1] != " ":
                self._error("list marker must be followed by a space", line)
            remainder = line.content[1:].strip()
            self._index += 1
            if not remainder:
                result.append(self._parse_required_child(indent, line))
                continue

            match = _KEY_VALUE.match(remainder)
            if match is None:
                result.append(self._parse_scalar(remainder, line))
                continue

            item: dict[str, Any] = {}
            key = match.group("key")
            value = match.group("value").strip()
            item[key] = (
                self._parse_scalar(value, line)
                if value
                else self._parse_required_child(indent, line)
            )
            continuation_indent = indent + 2
            if (
                not self.finished
                and self.current.indent == continuation_indent
                and not self.current.content.startswith("-")
            ):
                continuation = self._parse_mapping(continuation_indent)
                duplicates = set(item).intersection(continuation)
                if duplicates:
                    self._error(f"duplicate key '{sorted(duplicates)[0]}'", line)
                item.update(continuation)
            result.append(item)
        return result

    def _parse_mapping(self, indent: int) -> dict[str, Any]:
        result: dict[str, Any] = {}
        while not self.finished and self.current.indent == indent:
            line = self.current
            if line.content.startswith("-"):
                break
            match = _KEY_VALUE.match(line.content)
            if match is None:
                self._error("expected a mapping entry", line)
            key = match.group("key")
            if key in result:
                self._error(f"duplicate key '{key}'", line)
            value = match.group("value").strip()
            self._index += 1
            result[key] = (
                self._parse_scalar(value, line)
                if value
                else self._parse_required_child(indent, line)
            )
        return result

    def _parse_required_child(self, parent_indent: int, parent: _Line) -> Any:
        if self.finished or self.current.indent <= parent_indent:
            self._error("mapping or list value is missing", parent)
        return self.parse_block(self.current.indent)

    def _parse_scalar(self, value: str, line: _Line) -> Any:
        if value[0] in "&*!|>":
            self._error("YAML anchors, aliases, tags, and block strings are not supported", line)
        lowered = value.lower()
        if lowered in {"null", "~"}:
            return None
        if lowered == "true":
            return True
        if lowered == "false":
            return False
        if value.startswith('"'):
            try:
                parsed = json.loads(value)
            except json.JSONDecodeError as exception:
                self._error(f"invalid quoted scalar: {exception.msg}", line)
            if not isinstance(parsed, str):
                self._error("double-quoted scalar must contain a string", line)
            return parsed
        if value.startswith("'"):
            if len(value) < 2 or not value.endswith("'"):
                self._error("unterminated single-quoted scalar", line)
            return value[1:-1].replace("''", "'")
        if value.startswith("[") or value.startswith("{"):
            try:
                return json.loads(value)
            except json.JSONDecodeError as exception:
                self._error(f"inline collection must use JSON syntax: {exception.msg}", line)
        if _INTEGER.fullmatch(value):
            return int(value)
        if _FLOAT.fullmatch(value):
            return float(value)
        return value

    def _error(self, message: str, line: _Line | None = None) -> None:
        target = line or (None if self.finished else self.current)
        location = self._source_name if target is None else f"{self._source_name}:{target.number}"
        raise ConfigurationError(f"{location}: {message}")

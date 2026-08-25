"""Strict loading and cross-validation of upstream tracking manifests."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date
import json
from pathlib import Path, PurePosixPath
import re
from typing import Any, Mapping, Sequence
from urllib.parse import urlparse

from .errors import ConfigurationError
from .yaml_subset import load_yaml_subset


_COMMIT = re.compile(r"^[0-9a-f]{40}$")
_SHA256 = re.compile(r"^sha256:[0-9a-f]{64}$")
_IDENTIFIER = re.compile(r"^[A-Za-z][A-Za-z0-9_.-]*$")
_EXCEPTION_ID = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
_DOTNET_PROJECT = re.compile(
    r"^GonieGonie\.(?:InvisibleDragon|SimpleDragon)(?:\.[A-Za-z][A-Za-z0-9]*)*$"
)
_DOTNET_SYMBOL = re.compile(r"^GonieGonie\.(?:InvisibleDragon|SimpleDragon)(?:\.|$)")
_TEST_NAME = re.compile(r"^[A-Za-z_][A-Za-z0-9_.]*$")
_PORT_STATUSES = frozenset({"planned", "implemented", "equivalent", "enhanced", "excluded"})


@dataclass(frozen=True)
class ModulePin:
    key: str
    version: str
    paths: tuple[str, ...]


@dataclass(frozen=True)
class UpstreamLock:
    schema: str
    repository: str
    branch: str
    commit: str
    checked_at: str
    modules: tuple[ModulePin, ...]
    fixtures: tuple[tuple[str, str], ...]

    @property
    def module_paths(self) -> tuple[str, ...]:
        return tuple(path for module in self.modules for path in module.paths)


@dataclass(frozen=True)
class PortMapping:
    upstream_path: str
    upstream_symbol: str
    dotnet_project: str
    dotnet_file: str
    dotnet_symbol: str
    tests: tuple[str, ...]
    status: str


@dataclass(frozen=True)
class CompatibilityException:
    identifier: str
    upstream_path: str | None
    upstream_symbol: str | None
    upstream_symbol_hash: str | None
    upstream_difference: str
    dotnet_difference: str
    effects: tuple[tuple[str, str], ...]
    approval: str


@dataclass(frozen=True)
class TrackerConfiguration:
    lock: UpstreamLock
    mappings: tuple[PortMapping, ...]
    exceptions: tuple[CompatibilityException, ...]
    manifest_paths: tuple[Path, ...] = ()

    @property
    def tracked_paths(self) -> tuple[str, ...]:
        values = set(self.lock.module_paths)
        values.update(path for _, path in self.lock.fixtures)
        values.update(mapping.upstream_path for mapping in self.mappings)
        values.update(
            item.upstream_path
            for item in self.exceptions
            if item.upstream_path is not None
        )
        return tuple(sorted(values))


def load_configuration(
    lock_path: Path,
    port_map_path: Path,
    exceptions_path: Path,
) -> TrackerConfiguration:
    """Load and validate all Phase U tracking inputs."""

    lock = _load_lock(lock_path)
    mappings = _load_port_map(port_map_path)
    exceptions = _load_exceptions(exceptions_path)
    _validate_cross_references(lock, mappings, exceptions)
    return TrackerConfiguration(
        lock,
        mappings,
        exceptions,
        (lock_path.resolve(), port_map_path.resolve(), exceptions_path.resolve()),
    )


def _load_lock(path: Path) -> UpstreamLock:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except OSError as exception:
        raise ConfigurationError(f"Cannot read upstream lock '{path}': {exception}") from exception
    except json.JSONDecodeError as exception:
        raise ConfigurationError(
            f"{path}:{exception.lineno}:{exception.colno}: invalid JSON: {exception.msg}"
        ) from exception

    root = _mapping(value, "upstream lock")
    _keys(
        root,
        required={"schema", "repository", "branch", "commit", "checked_at", "modules", "fixtures"},
        optional={"source_license_declared_in", "source_license"},
        context="upstream lock",
    )
    schema = _text(root["schema"], "upstream lock.schema")
    if schema != "goniegonie.upstream-lock.v1":
        raise ConfigurationError(
            f"upstream lock.schema must be 'goniegonie.upstream-lock.v1', found '{schema}'"
        )
    repository = _text(root["repository"], "upstream lock.repository")
    parsed_repository = urlparse(repository)
    if parsed_repository.scheme != "https" or not parsed_repository.netloc:
        raise ConfigurationError("upstream lock.repository must be an absolute HTTPS URL")
    branch = _text(root["branch"], "upstream lock.branch")
    if not _valid_git_branch(branch):
        raise ConfigurationError("upstream lock.branch is not a safe Git branch name")
    commit = _text(root["commit"], "upstream lock.commit").lower()
    if _COMMIT.fullmatch(commit) is None:
        raise ConfigurationError("upstream lock.commit must be a full 40-character SHA-1")
    checked_at = _text(root["checked_at"], "upstream lock.checked_at")
    try:
        parsed_date = date.fromisoformat(checked_at)
    except ValueError as exception:
        raise ConfigurationError("upstream lock.checked_at must use YYYY-MM-DD") from exception
    if parsed_date.isoformat() != checked_at:
        raise ConfigurationError("upstream lock.checked_at must use canonical YYYY-MM-DD")

    module_values = _mapping(root["modules"], "upstream lock.modules")
    if not module_values:
        raise ConfigurationError("upstream lock.modules must contain at least one module")
    modules: list[ModulePin] = []
    for key in sorted(module_values):
        if _IDENTIFIER.fullmatch(key) is None:
            raise ConfigurationError(f"upstream lock module key '{key}' is invalid")
        module = _mapping(module_values[key], f"upstream lock.modules.{key}")
        _keys(
            module,
            required={"version", "paths"},
            optional=set(),
            context=f"upstream lock.modules.{key}",
        )
        paths = tuple(
            _relative_path(item, f"upstream lock.modules.{key}.paths")
            for item in _sequence(module["paths"], f"upstream lock.modules.{key}.paths")
        )
        if not paths or len(set(paths)) != len(paths):
            raise ConfigurationError(
                f"upstream lock.modules.{key}.paths must be non-empty and unique"
            )
        modules.append(ModulePin(key, _text(module["version"], f"module {key}.version"), paths))

    fixture_values = _mapping(root["fixtures"], "upstream lock.fixtures")
    fixtures: list[tuple[str, str]] = []
    for key in sorted(fixture_values):
        if _IDENTIFIER.fullmatch(key) is None:
            raise ConfigurationError(f"upstream lock fixture key '{key}' is invalid")
        fixtures.append((key, _relative_path(fixture_values[key], f"fixture {key}")))

    return UpstreamLock(
        schema,
        repository,
        branch,
        commit,
        checked_at,
        tuple(modules),
        tuple(fixtures),
    )


def _load_port_map(path: Path) -> tuple[PortMapping, ...]:
    values = _sequence(load_yaml_subset(path), "port map")
    if not values:
        raise ConfigurationError("port map must contain at least one mapping")
    mappings: list[PortMapping] = []
    for index, raw in enumerate(values):
        context = f"port map[{index}]"
        item = _mapping(raw, context)
        _keys(
            item,
            required={"upstream", "dotnet", "tests", "status"},
            optional=set(),
            context=context,
        )
        upstream = _mapping(item["upstream"], f"{context}.upstream")
        dotnet = _mapping(item["dotnet"], f"{context}.dotnet")
        _keys(
            upstream,
            required={"path", "symbol"},
            optional=set(),
            context=f"{context}.upstream",
        )
        _keys(
            dotnet,
            required={"project", "file", "symbol"},
            optional=set(),
            context=f"{context}.dotnet",
        )
        project = _text(dotnet["project"], f"{context}.dotnet.project")
        if _DOTNET_PROJECT.fullmatch(project) is None:
            raise ConfigurationError(
                f"{context}.dotnet.project must use a GonieGonie InvisibleDragon or SimpleDragon name"
            )
        dotnet_symbol = _text(dotnet["symbol"], f"{context}.dotnet.symbol")
        if _DOTNET_SYMBOL.match(dotnet_symbol) is None:
            raise ConfigurationError(
                f"{context}.dotnet.symbol must use a GonieGonie InvisibleDragon or SimpleDragon namespace"
            )
        tests = tuple(
            _text(test, f"{context}.tests")
            for test in _sequence(item["tests"], f"{context}.tests")
        )
        if not tests or len(set(tests)) != len(tests):
            raise ConfigurationError(f"{context}.tests must be non-empty and unique")
        invalid_test = next((test for test in tests if _TEST_NAME.fullmatch(test) is None), None)
        if invalid_test is not None:
            raise ConfigurationError(f"{context}.tests contains invalid name '{invalid_test}'")
        status = _text(item["status"], f"{context}.status")
        if status not in _PORT_STATUSES:
            raise ConfigurationError(
                f"{context}.status must be one of {', '.join(sorted(_PORT_STATUSES))}"
            )
        mappings.append(
            PortMapping(
                _relative_path(upstream["path"], f"{context}.upstream.path"),
                _text(upstream["symbol"], f"{context}.upstream.symbol"),
                project,
                _relative_path(dotnet["file"], f"{context}.dotnet.file"),
                dotnet_symbol,
                tests,
                status,
            )
        )
    return tuple(mappings)


def _load_exceptions(path: Path) -> tuple[CompatibilityException, ...]:
    values = _sequence(load_yaml_subset(path), "compatibility exceptions")
    exceptions: list[CompatibilityException] = []
    identifiers: set[str] = set()
    for index, raw in enumerate(values):
        context = f"compatibility exceptions[{index}]"
        item = _mapping(raw, context)
        _keys(
            item,
            required={"id", "upstream", "difference", "effect", "approval"},
            optional=set(),
            context=context,
        )
        identifier = _text(item["id"], f"{context}.id")
        if _EXCEPTION_ID.fullmatch(identifier) is None or identifier in identifiers:
            raise ConfigurationError(f"{context}.id must be a unique kebab-case identifier")
        identifiers.add(identifier)
        upstream = _mapping(item["upstream"], f"{context}.upstream")
        difference = _mapping(item["difference"], f"{context}.difference")
        effect = _mapping(item["effect"], f"{context}.effect")
        _keys(
            upstream,
            required={"path", "symbol", "symbol_hash"},
            optional=set(),
            context=f"{context}.upstream",
        )
        _keys(
            difference,
            required={"upstream", "dotnet"},
            optional=set(),
            context=f"{context}.difference",
        )
        upstream_path = _optional_relative_path(upstream["path"], f"{context}.upstream.path")
        upstream_symbol = _optional_text(upstream["symbol"], f"{context}.upstream.symbol")
        upstream_symbol_hash = _optional_hash(
            upstream["symbol_hash"],
            f"{context}.upstream.symbol_hash",
        )
        if len(
            {
                upstream_path is None,
                upstream_symbol is None,
                upstream_symbol_hash is None,
            }
        ) != 1:
            raise ConfigurationError(
                f"{context}.upstream.path, symbol, and symbol_hash must either all be null or all be populated"
            )
        if not effect:
            raise ConfigurationError(f"{context}.effect must not be empty")
        effects = tuple(
            (key, _text(effect[key], f"{context}.effect.{key}"))
            for key in sorted(effect)
        )
        exceptions.append(
            CompatibilityException(
                identifier,
                upstream_path,
                upstream_symbol,
                upstream_symbol_hash,
                _text(difference["upstream"], f"{context}.difference.upstream"),
                _text(difference["dotnet"], f"{context}.difference.dotnet"),
                effects,
                _text(item["approval"], f"{context}.approval"),
            )
        )
    return tuple(exceptions)


def _validate_cross_references(
    lock: UpstreamLock,
    mappings: tuple[PortMapping, ...],
    exceptions: tuple[CompatibilityException, ...],
) -> None:
    roots = lock.module_paths
    for mapping in mappings:
        if not any(_is_within(mapping.upstream_path, root) for root in roots):
            raise ConfigurationError(
                f"port map path '{mapping.upstream_path}' is outside every locked module path"
            )
    for item in exceptions:
        if item.upstream_path is not None and not any(
            _is_within(item.upstream_path, root) for root in roots
        ):
            raise ConfigurationError(
                f"compatibility exception '{item.identifier}' is outside every locked module path"
            )


def _is_within(path: str, root: str) -> bool:
    return path == root or path.startswith(f"{root}/")


def _valid_git_branch(value: str) -> bool:
    forbidden = {" ", "~", "^", ":", "?", "*", "[", "\\"}
    return not (
        value.startswith(("-", ".", "/"))
        or value.endswith((".", "/", ".lock"))
        or ".." in value
        or "//" in value
        or "@{" in value
        or any(character in forbidden or ord(character) < 32 for character in value)
    )


def _relative_path(value: Any, context: str) -> str:
    text = _text(value, context)
    if "\\" in text or text.startswith("/") or re.match(r"^[A-Za-z]:", text):
        raise ConfigurationError(f"{context} must be a relative POSIX path")
    path = PurePosixPath(text)
    if not path.parts or any(part in {"", ".", ".."} for part in path.parts):
        raise ConfigurationError(f"{context} contains an invalid path segment")
    return path.as_posix()


def _optional_relative_path(value: Any, context: str) -> str | None:
    return None if value is None else _relative_path(value, context)


def _text(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip() or value != value.strip():
        raise ConfigurationError(f"{context} must be a non-empty trimmed string")
    return value


def _optional_text(value: Any, context: str) -> str | None:
    return None if value is None else _text(value, context)


def _optional_hash(value: Any, context: str) -> str | None:
    if value is None:
        return None
    text = _text(value, context)
    if _SHA256.fullmatch(text) is None:
        raise ConfigurationError(f"{context} must be a lowercase sha256 hash or null")
    return text


def _mapping(value: Any, context: str) -> Mapping[str, Any]:
    if not isinstance(value, dict) or not all(isinstance(key, str) for key in value):
        raise ConfigurationError(f"{context} must be a mapping")
    return value


def _sequence(value: Any, context: str) -> Sequence[Any]:
    if not isinstance(value, list):
        raise ConfigurationError(f"{context} must be a list")
    return value


def _keys(
    value: Mapping[str, Any],
    *,
    required: set[str],
    optional: set[str],
    context: str,
) -> None:
    missing = required.difference(value)
    unknown = set(value).difference(required, optional)
    if missing:
        raise ConfigurationError(f"{context} is missing key '{sorted(missing)[0]}'")
    if unknown:
        raise ConfigurationError(f"{context} contains unknown key '{sorted(unknown)[0]}'")

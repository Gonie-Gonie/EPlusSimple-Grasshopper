"""Python 3.12 AST symbol fingerprints and source-tree snapshots."""

from __future__ import annotations

import ast
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import tokenize
from typing import Any, Iterable

from .errors import SourceError


_IGNORED_DIRECTORIES = frozenset({".git", "__pycache__", ".mypy_cache", ".pytest_cache"})
_IGNORED_SUFFIXES = frozenset({".pyc", ".pyo"})


@dataclass(frozen=True)
class SymbolFingerprint:
    name: str
    kind: str
    hash: str
    signature_hash: str
    body_hash: str

    def to_data(self) -> dict[str, str]:
        return {
            "body_hash": self.body_hash,
            "hash": self.hash,
            "kind": self.kind,
            "name": self.name,
            "signature_hash": self.signature_hash,
        }


@dataclass(frozen=True)
class FileFingerprint:
    path: str
    kind: str
    content_hash: str
    ast_hash: str | None
    symbols: tuple[SymbolFingerprint, ...]

    @property
    def symbols_by_name(self) -> dict[str, SymbolFingerprint]:
        return {symbol.name: symbol for symbol in self.symbols}

    def to_data(self) -> dict[str, Any]:
        return {
            "ast_hash": self.ast_hash,
            "content_hash": self.content_hash,
            "kind": self.kind,
            "path": self.path,
            "symbols": [symbol.to_data() for symbol in self.symbols],
        }


@dataclass(frozen=True)
class SourceSnapshot:
    files: tuple[FileFingerprint, ...]

    @property
    def files_by_path(self) -> dict[str, FileFingerprint]:
        return {item.path: item for item in self.files}

    def to_data(self) -> dict[str, Any]:
        return {
            "schema": "goniegonie.upstream-symbol-hashes.v1",
            "files": [item.to_data() for item in self.files],
        }


def build_snapshot(
    source_root: Path,
    tracked_paths: Iterable[str],
    *,
    require_tracked_paths: bool = False,
) -> SourceSnapshot:
    """Fingerprint files beneath the configured paths in *source_root*."""

    root = source_root.resolve()
    if not root.is_dir():
        raise SourceError(f"Source root does not exist or is not a directory: {root}")

    files: dict[str, Path] = {}
    missing: list[str] = []
    for tracked_path in sorted(set(tracked_paths)):
        relative = _validated_relative_path(tracked_path)
        candidate = _resolve_under(root, relative)
        if not candidate.exists():
            missing.append(relative.as_posix())
            continue
        if candidate.is_symlink():
            raise SourceError(f"Tracked source path must not be a symbolic link: {relative.as_posix()}")
        if candidate.is_file():
            _add_file(root, candidate, files)
            continue
        if not candidate.is_dir():
            raise SourceError(f"Tracked source path is not a regular file or directory: {relative.as_posix()}")
        for directory, directory_names, file_names in os.walk(candidate, followlinks=False):
            directory_names[:] = sorted(
                name
                for name in directory_names
                if name not in _IGNORED_DIRECTORIES and not name.startswith(".")
            )
            for file_name in sorted(file_names):
                path = Path(directory, file_name)
                if path.suffix.lower() in _IGNORED_SUFFIXES:
                    continue
                _add_file(root, path, files)

    if require_tracked_paths and missing:
        raise SourceError(
            "Pinned source is missing tracked path(s): " + ", ".join(sorted(missing))
        )

    fingerprints = tuple(_fingerprint(path, relative) for relative, path in sorted(files.items()))
    return SourceSnapshot(fingerprints)


def _add_file(root: Path, path: Path, files: dict[str, Path]) -> None:
    if path.is_symlink():
        raise SourceError(f"Tracked source file must not be a symbolic link: {path}")
    if not path.is_file():
        return
    resolved = path.resolve()
    try:
        relative = resolved.relative_to(root).as_posix()
    except ValueError as exception:
        raise SourceError(f"Tracked source escapes the source root: {path}") from exception
    files[relative] = resolved


def _fingerprint(path: Path, relative: str) -> FileFingerprint:
    try:
        raw = path.read_bytes()
    except OSError as exception:
        raise SourceError(f"Cannot read tracked source '{relative}': {exception}") from exception
    content_hash = _hash_bytes(raw)
    if path.suffix.lower() != ".py":
        return FileFingerprint(relative, "data", content_hash, None, ())

    try:
        with tokenize.open(path) as stream:
            text = stream.read()
        tree = ast.parse(
            text,
            filename=relative,
            mode="exec",
            type_comments=True,
            feature_version=(3, 12),
        )
    except (OSError, SyntaxError, UnicodeError) as exception:
        raise SourceError(f"Cannot parse Python 3.12 source '{relative}': {exception}") from exception

    ast_hash = _hash_text(ast.dump(tree, annotate_fields=True, include_attributes=False))
    symbols = _SymbolCollector().collect(tree)
    return FileFingerprint(relative, "python", content_hash, ast_hash, symbols)


@dataclass(frozen=True)
class _SymbolPart:
    kind: str
    signature: Any
    body: Any


class _SymbolCollector:
    def __init__(self) -> None:
        self._parts: dict[str, list[_SymbolPart]] = {}

    def collect(self, tree: ast.Module) -> tuple[SymbolFingerprint, ...]:
        self._visit_container("", tree.body, module=True)
        fingerprints: list[SymbolFingerprint] = []
        for name in sorted(self._parts):
            parts = self._parts[name]
            kinds = sorted({part.kind for part in parts})
            kind = kinds[0] if len(kinds) == 1 else "mixed"
            signature = [part.signature for part in parts]
            body = [part.body for part in parts]
            signature_hash = _hash_json(signature)
            body_hash = _hash_json(body)
            fingerprints.append(
                SymbolFingerprint(
                    name,
                    kind,
                    _hash_json({"body": body, "kind": kinds, "signature": signature}),
                    signature_hash,
                    body_hash,
                )
            )
        return tuple(fingerprints)

    def _visit_container(
        self,
        prefix: str,
        statements: list[ast.stmt],
        *,
        module: bool = False,
    ) -> None:
        residual: list[str] = []
        for statement in statements:
            if isinstance(statement, (ast.FunctionDef, ast.AsyncFunctionDef)):
                self._add_function(prefix, statement)
            elif isinstance(statement, ast.ClassDef):
                self._add_class(prefix, statement)
            elif _is_tracked_assignment(statement):
                for target_name in _assignment_names(statement):
                    qualified = _qualify(prefix, target_name)
                    self._add(
                        qualified,
                        _SymbolPart(
                            "constant",
                            {
                                "annotation": _dump(getattr(statement, "annotation", None)),
                                "target": qualified,
                            },
                            _dump(getattr(statement, "value", None)),
                        ),
                    )
            else:
                residual.append(_dump(statement))

        container_name = "<module>" if module else prefix
        self._add(
            container_name,
            _SymbolPart(
                "module" if module else "class",
                {"name": container_name},
                residual,
            ),
        )

    def _add_function(
        self,
        prefix: str,
        node: ast.FunctionDef | ast.AsyncFunctionDef,
    ) -> None:
        name = _qualify(prefix, node.name)
        signature = {
            "arguments": _dump(node.args),
            "decorators": [_dump(item) for item in node.decorator_list],
            "node_type": type(node).__name__,
            "returns": _dump(node.returns),
            "type_comment": node.type_comment,
            "type_params": [_dump(item) for item in getattr(node, "type_params", [])],
        }
        self._add(
            name,
            _SymbolPart("function", signature, [_dump(statement) for statement in node.body]),
        )

    def _add_class(self, prefix: str, node: ast.ClassDef) -> None:
        name = _qualify(prefix, node.name)
        signature = {
            "bases": [_dump(item) for item in node.bases],
            "decorators": [_dump(item) for item in node.decorator_list],
            "keywords": [_dump(item) for item in node.keywords],
            "name": name,
            "type_params": [_dump(item) for item in getattr(node, "type_params", [])],
        }
        self._add(name, _SymbolPart("class", signature, []))
        self._visit_container(name, node.body)

    def _add(self, name: str, part: _SymbolPart) -> None:
        self._parts.setdefault(name, []).append(part)


def _is_tracked_assignment(statement: ast.stmt) -> bool:
    if not isinstance(statement, (ast.Assign, ast.AnnAssign)):
        return False
    names = _assignment_names(statement)
    if not names:
        return False
    if any(name.isupper() for name in names):
        return True
    try:
        ast.literal_eval(statement.value)
    except (ValueError, TypeError):
        return False
    return True


def _assignment_names(statement: ast.stmt) -> tuple[str, ...]:
    if isinstance(statement, ast.Assign):
        return tuple(target.id for target in statement.targets if isinstance(target, ast.Name))
    if isinstance(statement, ast.AnnAssign) and isinstance(statement.target, ast.Name):
        return (statement.target.id,)
    return ()


def _qualify(prefix: str, name: str) -> str:
    return f"{prefix}.{name}" if prefix else name


def _dump(node: ast.AST | None) -> str | None:
    return None if node is None else ast.dump(node, annotate_fields=True, include_attributes=False)


def _validated_relative_path(value: str) -> PurePosixPath:
    path = PurePosixPath(value)
    if value.startswith("/") or "\\" in value or any(part in {"", ".", ".."} for part in path.parts):
        raise SourceError(f"Tracked path must be a safe relative POSIX path: {value}")
    return path


def _resolve_under(root: Path, relative: PurePosixPath) -> Path:
    candidate = root.joinpath(*relative.parts).resolve()
    try:
        candidate.relative_to(root)
    except ValueError as exception:
        raise SourceError(f"Tracked path escapes the source root: {relative.as_posix()}") from exception
    return candidate


def _hash_json(value: Any) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return _hash_bytes(encoded)


def _hash_text(value: str) -> str:
    return _hash_bytes(value.encode("utf-8"))


def _hash_bytes(value: bytes) -> str:
    return f"sha256:{hashlib.sha256(value).hexdigest()}"

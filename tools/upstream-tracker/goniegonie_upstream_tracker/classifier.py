"""Deterministic source diff classification and impacted-test mapping."""

from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum
from pathlib import Path
import subprocess
from typing import Any, Iterable

from .config import (
    CompatibilityException,
    PortMapping,
    TrackerConfiguration,
)
from .errors import SourceError
from .symbols import FileFingerprint, SourceSnapshot, SymbolFingerprint, build_snapshot


class ChangeClassification(StrEnum):
    ADDED = "added"
    DELETED = "deleted"
    SIGNATURE_CHANGED = "signature_changed"
    BODY_CHANGED = "body_changed"
    CONSTANT_CHANGED = "constant_changed"
    DATA_CHANGED = "data_changed"
    COMMENTS_ONLY = "comments_only"


@dataclass(frozen=True)
class SourceIdentity:
    kind: str
    commit: str | None
    repository: str | None
    branch: str | None
    clean: bool | None
    pin_verified: bool

    def to_data(self) -> dict[str, Any]:
        return {
            "branch": self.branch,
            "clean": self.clean,
            "commit": self.commit,
            "kind": self.kind,
            "pin_verified": self.pin_verified,
            "repository": self.repository,
        }


@dataclass(frozen=True)
class MappingImpact:
    project: str
    file: str
    symbol: str
    status: str
    tests: tuple[str, ...]
    match: str

    def to_data(self) -> dict[str, Any]:
        return {
            "file": self.file,
            "match": self.match,
            "project": self.project,
            "status": self.status,
            "symbol": self.symbol,
            "tests": list(self.tests),
        }


@dataclass(frozen=True)
class SourceChange:
    path: str
    symbol: str
    symbol_kind: str
    classification: ChangeClassification
    baseline_hash: str | None
    current_hash: str | None
    mappings: tuple[MappingImpact, ...]
    compatibility_exceptions: tuple[str, ...]

    def to_data(self) -> dict[str, Any]:
        return {
            "baseline_hash": self.baseline_hash,
            "classification": self.classification.value,
            "compatibility_exceptions": list(self.compatibility_exceptions),
            "current_hash": self.current_hash,
            "mappings": [mapping.to_data() for mapping in self.mappings],
            "path": self.path,
            "symbol": self.symbol,
            "symbol_kind": self.symbol_kind,
        }


@dataclass(frozen=True)
class ImpactedTargets:
    projects: tuple[str, ...]
    files: tuple[str, ...]
    symbols: tuple[str, ...]
    tests: tuple[str, ...]

    def to_data(self) -> dict[str, Any]:
        return {
            "files": list(self.files),
            "projects": list(self.projects),
            "symbols": list(self.symbols),
            "tests": list(self.tests),
        }


@dataclass(frozen=True)
class ComparisonReport:
    baseline: SourceIdentity
    current: SourceIdentity
    configuration: TrackerConfiguration
    changes: tuple[SourceChange, ...]
    impacted: ImpactedTargets

    @property
    def has_drift(self) -> bool:
        return bool(self.changes)

    @property
    def review_required(self) -> bool:
        return any(
            change.classification != ChangeClassification.COMMENTS_ONLY
            for change in self.changes
        )

    @property
    def unmapped_change_count(self) -> int:
        return sum(not change.mappings for change in self.changes)

    def to_data(self) -> dict[str, Any]:
        counts = {
            classification.value: sum(
                change.classification == classification for change in self.changes
            )
            for classification in ChangeClassification
        }
        return {
            "baseline": self.baseline.to_data(),
            "changes": [change.to_data() for change in self.changes],
            "current": self.current.to_data(),
            "has_drift": self.has_drift,
            "impacted": self.impacted.to_data(),
            "pinned": {
                "branch": self.configuration.lock.branch,
                "checked_at": self.configuration.lock.checked_at,
                "commit": self.configuration.lock.commit,
                "repository": self.configuration.lock.repository,
            },
            "review_required": self.review_required,
            "schema": "goniegonie.upstream-diff-report.v1",
            "summary": {
                "change_count": len(self.changes),
                "classification_counts": counts,
                "compatibility_exception_count": len(
                    {
                        identifier
                        for change in self.changes
                        for identifier in change.compatibility_exceptions
                    }
                ),
                "mapped_change_count": len(self.changes) - self.unmapped_change_count,
                "unmapped_change_count": self.unmapped_change_count,
            },
        }


def compare_sources(
    configuration: TrackerConfiguration,
    baseline_root: Path,
    current_root: Path,
) -> ComparisonReport:
    """Compare a locked baseline clone/export with a current source root."""

    baseline_identity = inspect_source_identity(
        baseline_root,
        expected_commit=configuration.lock.commit,
        expected_repository=configuration.lock.repository,
    )
    current_identity = inspect_source_identity(current_root)
    baseline = build_snapshot(
        baseline_root,
        configuration.tracked_paths,
        require_tracked_paths=True,
    )
    current = build_snapshot(
        current_root,
        configuration.tracked_paths,
        require_tracked_paths=False,
    )
    changes = _classify(
        baseline,
        current,
        configuration.mappings,
        configuration.exceptions,
    )
    impacts = tuple(mapping for change in changes for mapping in change.mappings)
    impacted = ImpactedTargets(
        tuple(sorted({item.project for item in impacts})),
        tuple(sorted({item.file for item in impacts})),
        tuple(sorted({item.symbol for item in impacts})),
        tuple(sorted({test for item in impacts for test in item.tests})),
    )
    return ComparisonReport(
        baseline_identity,
        current_identity,
        configuration,
        changes,
        impacted,
    )


def inspect_source_identity(
    source_root: Path,
    *,
    expected_commit: str | None = None,
    expected_repository: str | None = None,
) -> SourceIdentity:
    """Inspect Git metadata when available and strictly verify a pinned clone."""

    root = source_root.resolve()
    if not root.is_dir():
        raise SourceError(f"Source root does not exist or is not a directory: {root}")
    if not (root / ".git").exists():
        return SourceIdentity("source_root", None, None, None, None, False)

    commit = _git(root, "rev-parse", "HEAD").lower()
    repository = _git_optional(root, "remote", "get-url", "origin")
    branch = _git_optional(root, "branch", "--show-current") or None
    clean = not bool(_git(root, "status", "--porcelain"))
    if expected_commit is not None and commit != expected_commit.lower():
        raise SourceError(
            f"Pinned source HEAD is '{commit}', expected '{expected_commit.lower()}'"
        )
    if expected_repository is not None:
        if repository is None or _normalize_repository(repository) != _normalize_repository(expected_repository):
            raise SourceError("Pinned source origin does not match upstream lock.repository")
    if expected_commit is not None and not clean:
        raise SourceError("Pinned source clone must be clean before comparison")
    return SourceIdentity(
        "git_clone",
        commit,
        repository,
        branch,
        clean,
        expected_commit is not None,
    )


def _classify(
    baseline: SourceSnapshot,
    current: SourceSnapshot,
    mappings: tuple[PortMapping, ...],
    exceptions: tuple[CompatibilityException, ...],
) -> tuple[SourceChange, ...]:
    baseline_files = baseline.files_by_path
    current_files = current.files_by_path
    result: list[SourceChange] = []
    for path in sorted(set(baseline_files).union(current_files)):
        before = baseline_files.get(path)
        after = current_files.get(path)
        raw_changes = _classify_file(path, before, after)
        known_symbols = set()
        if before is not None:
            known_symbols.update(before.symbols_by_name)
        if after is not None:
            known_symbols.update(after.symbols_by_name)
        for raw in raw_changes:
            selected_mappings = _matching_mappings(path, raw.symbol, known_symbols, mappings)
            impacts = tuple(
                MappingImpact(
                    mapping.dotnet_project,
                    mapping.dotnet_file,
                    mapping.dotnet_symbol,
                    mapping.status,
                    mapping.tests,
                    match,
                )
                for mapping, match in selected_mappings
            )
            exception_ids = tuple(
                sorted(
                    item.identifier
                    for item in exceptions
                    if item.upstream_path == path and item.upstream_symbol == raw.symbol
                )
            )
            result.append(
                SourceChange(
                    raw.path,
                    raw.symbol,
                    raw.symbol_kind,
                    raw.classification,
                    raw.baseline_hash,
                    raw.current_hash,
                    impacts,
                    exception_ids,
                )
            )
    return tuple(
        sorted(
            result,
            key=lambda item: (item.path, item.symbol, item.classification.value),
        )
    )


@dataclass(frozen=True)
class _RawChange:
    path: str
    symbol: str
    symbol_kind: str
    classification: ChangeClassification
    baseline_hash: str | None
    current_hash: str | None


def _classify_file(
    path: str,
    before: FileFingerprint | None,
    after: FileFingerprint | None,
) -> tuple[_RawChange, ...]:
    if before is None and after is not None:
        return _file_side_changes(path, after, ChangeClassification.ADDED, baseline=False)
    if before is not None and after is None:
        return _file_side_changes(path, before, ChangeClassification.DELETED, baseline=True)
    if before is None or after is None:
        return ()
    if before.content_hash == after.content_hash:
        return ()
    if before.kind != "python" or after.kind != "python":
        return (
            _RawChange(
                path,
                "<file>",
                "data",
                ChangeClassification.DATA_CHANGED,
                before.content_hash,
                after.content_hash,
            ),
        )

    before_symbols = before.symbols_by_name
    after_symbols = after.symbols_by_name
    changes: list[_RawChange] = []
    for symbol_name in sorted(set(before_symbols).union(after_symbols)):
        old = before_symbols.get(symbol_name)
        new = after_symbols.get(symbol_name)
        if old is None and new is not None:
            changes.append(
                _RawChange(path, symbol_name, new.kind, ChangeClassification.ADDED, None, new.hash)
            )
        elif old is not None and new is None:
            changes.append(
                _RawChange(path, symbol_name, old.kind, ChangeClassification.DELETED, old.hash, None)
            )
        elif old is not None and new is not None and old.hash != new.hash:
            if old.kind != new.kind or old.signature_hash != new.signature_hash:
                classification = ChangeClassification.SIGNATURE_CHANGED
            elif old.kind == "constant":
                classification = ChangeClassification.CONSTANT_CHANGED
            else:
                classification = ChangeClassification.BODY_CHANGED
            changes.append(
                _RawChange(path, symbol_name, new.kind, classification, old.hash, new.hash)
            )

    if not changes and before.ast_hash == after.ast_hash:
        changes.append(
            _RawChange(
                path,
                "<file>",
                "python",
                ChangeClassification.COMMENTS_ONLY,
                before.content_hash,
                after.content_hash,
            )
        )
    elif not changes:
        changes.append(
            _RawChange(
                path,
                "<module>",
                "module",
                ChangeClassification.BODY_CHANGED,
                before.ast_hash,
                after.ast_hash,
            )
        )
    return tuple(changes)


def _file_side_changes(
    path: str,
    fingerprint: FileFingerprint,
    classification: ChangeClassification,
    *,
    baseline: bool,
) -> tuple[_RawChange, ...]:
    if fingerprint.kind == "python" and fingerprint.symbols:
        return tuple(
            _RawChange(
                path,
                symbol.name,
                symbol.kind,
                classification,
                symbol.hash if baseline else None,
                None if baseline else symbol.hash,
            )
            for symbol in fingerprint.symbols
        )
    return (
        _RawChange(
            path,
            "<file>",
            fingerprint.kind,
            classification,
            fingerprint.content_hash if baseline else None,
            None if baseline else fingerprint.content_hash,
        ),
    )


def _matching_mappings(
    path: str,
    symbol: str,
    known_symbols: set[str],
    mappings: tuple[PortMapping, ...],
) -> tuple[tuple[PortMapping, str], ...]:
    path_mappings = [mapping for mapping in mappings if mapping.upstream_path == path]
    if symbol == "<file>":
        selected = [(mapping, "path") for mapping in path_mappings]
    else:
        selected = []
        for mapping in path_mappings:
            if mapping.upstream_symbol == symbol:
                selected.append((mapping, "symbol"))
            elif mapping.upstream_symbol not in known_symbols:
                selected.append((mapping, "path"))
    unique: dict[tuple[str, str, str, str], tuple[PortMapping, str]] = {}
    for mapping, match in selected:
        key = (
            mapping.dotnet_project,
            mapping.dotnet_file,
            mapping.dotnet_symbol,
            mapping.upstream_symbol,
        )
        unique[key] = (mapping, match)
    return tuple(
        unique[key]
        for key in sorted(unique)
    )


def _git(root: Path, *arguments: str) -> str:
    try:
        completed = subprocess.run(
            ["git", "-C", str(root), *arguments],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
    except (OSError, subprocess.CalledProcessError) as exception:
        detail = getattr(exception, "stderr", None) or str(exception)
        raise SourceError(f"Git metadata inspection failed: {detail.strip()}") from exception
    return completed.stdout.strip()


def _git_optional(root: Path, *arguments: str) -> str | None:
    try:
        return _git(root, *arguments)
    except SourceError:
        return None


def _normalize_repository(value: str) -> str:
    normalized = value.strip().replace("\\", "/").rstrip("/")
    return normalized[:-4] if normalized.lower().endswith(".git") else normalized

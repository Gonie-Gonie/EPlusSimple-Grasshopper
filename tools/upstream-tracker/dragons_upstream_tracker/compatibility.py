"""Fail-closed public-symbol compatibility inventory, matrix, and gate support."""

from __future__ import annotations

from dataclasses import asdict, dataclass
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from typing import Any, Mapping, Sequence

from .atomic_io import write_text_atomically
from .classifier import inspect_source_identity
from .config import CompatibilityException, TrackerConfiguration, load_configuration
from .evidence import (
    EvidenceExecution,
    EvidenceResults,
    ScopeDecisionRegistry,
    SymbolEvidenceRegistry,
    evaluate_evidence_execution,
    load_scope_decisions,
    load_symbol_evidence,
    validate_repository_manifest_paths,
)
from .errors import ConfigurationError, SourceError
from .symbols import SourceSnapshot, build_snapshot


SCOPE_SCHEMA = "dragons.upstream-compatibility-scope.v1"
INVENTORY_SCHEMA = "dragons.upstream-public-symbol-inventory.v2"
MATRIX_SCHEMA = "dragons.upstream-compatibility-matrix.v1"
REPORT_SCHEMA = "dragons.upstream-compatibility-report.v2"

CANONICAL_MANIFEST_NAMES = (
    "upstream.lock.json",
    "port-map.yml",
    "compatibility-exceptions.yml",
    "compatibility-scope.json",
    "public-symbol-inventory.json",
    "compatibility-matrix.json",
    "symbol-evidence.json",
    "scope-decisions.json",
)

ALLOWED_CLASSIFICATIONS = (
    "equivalent",
    "exception",
    "out_of_scope",
    "needs_reverification",
)
COMPLETE_CLASSIFICATIONS = (
    "equivalent",
    "exception",
    "out_of_scope",
)
PUBLIC_INVENTORY_POLICY: dict[str, Any] = {
    "language": "python",
    "python_feature_version": "3.12",
    "symbol_universe": "ast_declared_public",
    "include_kinds": ["class", "constant", "function"],
    "constant_rule": "uppercase_assignment_target",
    "include_public_top_level": True,
    "include_public_class_members": True,
    "include_dunder_class_members": True,
    "exclude_private_top_level": True,
    "exclude_private_class_members": True,
    "exclude_import_aliases": True,
    "exclude_nested_function_locals": True,
}

_SHA256 = re.compile(r"^sha256:[0-9a-f]{64}$")


@dataclass(frozen=True)
class CompatibilityScope:
    upstream_commit: str
    module_paths: tuple[str, ...]

    @property
    def policy_sha256(self) -> str:
        return _sha256_data(PUBLIC_INVENTORY_POLICY)

    @property
    def content_sha256(self) -> str:
        return _sha256_data(self.to_data())

    def to_data(self) -> dict[str, Any]:
        return {
            "classifications": {
                "allowed": list(ALLOWED_CLASSIFICATIONS),
                "complete": list(COMPLETE_CLASSIFICATIONS),
            },
            "completion_gate": {
                "forbid": ["needs_reverification"],
                "require_exact_inventory_coverage": True,
                "require_inventory_matches_pinned_source": True,
            },
            "inventory_policy": dict(PUBLIC_INVENTORY_POLICY),
            "module_paths": list(self.module_paths),
            "schema": SCOPE_SCHEMA,
            "upstream_commit": self.upstream_commit,
        }


@dataclass(frozen=True)
class PublicFile:
    path: str
    content_hash: str
    ast_hash: str

    def to_data(self) -> dict[str, str]:
        return {
            "ast_hash": self.ast_hash,
            "content_hash": self.content_hash,
            "path": self.path,
        }


@dataclass(frozen=True)
class PublicSymbol:
    path: str
    symbol: str
    kind: str
    symbol_hash: str
    signature_hash: str
    body_hash: str

    @property
    def key(self) -> tuple[str, str]:
        return self.path, self.symbol

    def to_data(self) -> dict[str, str]:
        return {
            "body_hash": self.body_hash,
            "kind": self.kind,
            "path": self.path,
            "signature_hash": self.signature_hash,
            "symbol": self.symbol,
            "symbol_hash": self.symbol_hash,
        }


@dataclass(frozen=True)
class PublicSymbolInventory:
    upstream_commit: str
    scope_sha256: str
    files: tuple[PublicFile, ...]
    symbols: tuple[PublicSymbol, ...]

    @property
    def symbols_by_key(self) -> dict[tuple[str, str], PublicSymbol]:
        return {item.key: item for item in self.symbols}

    @property
    def content_sha256(self) -> str:
        return _sha256_data(self._content_data())

    def _content_data(self) -> dict[str, Any]:
        return {
            "files": [item.to_data() for item in self.files],
            "scope_sha256": self.scope_sha256,
            "symbols": [item.to_data() for item in self.symbols],
            "upstream_commit": self.upstream_commit,
        }

    def to_data(self) -> dict[str, Any]:
        kind_counts = {
            kind: sum(item.kind == kind for item in self.symbols)
            for kind in PUBLIC_INVENTORY_POLICY["include_kinds"]
        }
        return {
            "content_sha256": self.content_sha256,
            "files": [item.to_data() for item in self.files],
            "schema": INVENTORY_SCHEMA,
            "scope_sha256": self.scope_sha256,
            "summary": {
                "kind_counts": kind_counts,
                "python_file_count": len(self.files),
                "public_symbol_count": len(self.symbols),
            },
            "symbols": [item.to_data() for item in self.symbols],
            "upstream_commit": self.upstream_commit,
        }


@dataclass(frozen=True)
class MatrixEntry:
    path: str
    symbol: str
    classification: str
    rationale: str
    evidence: tuple[str, ...]
    exception_id: str | None

    @property
    def key(self) -> tuple[str, str]:
        return self.path, self.symbol

    def to_data(self) -> dict[str, Any]:
        data: dict[str, Any] = {
            "classification": self.classification,
            "evidence": list(self.evidence),
            "path": self.path,
            "rationale": self.rationale,
            "symbol": self.symbol,
        }
        if self.exception_id is not None:
            data["exception_id"] = self.exception_id
        return data


@dataclass(frozen=True)
class CompatibilityMatrix:
    upstream_commit: str
    inventory_sha256: str
    entries: tuple[MatrixEntry, ...]

    @property
    def entries_by_key(self) -> dict[tuple[str, str], MatrixEntry]:
        return {item.key: item for item in self.entries}

    @property
    def content_sha256(self) -> str:
        return _sha256_data(self._content_data())

    def _content_data(self) -> dict[str, Any]:
        details = [
            _matrix_detail(index, item)
            for index, item in enumerate(self.entries)
            if item.classification != "needs_reverification"
        ]
        return {
            "classifications": [item.classification for item in self.entries],
            "details": details,
            "entry_order": "public-symbol-inventory.symbols",
            "inventory_sha256": self.inventory_sha256,
            "needs_reverification_rationale": (
                "No symbol-level equivalence, verified exception, or out-of-scope evidence is registered."
            ),
            "upstream_commit": self.upstream_commit,
        }

    def to_data(self) -> dict[str, Any]:
        counts = {
            status: sum(item.classification == status for item in self.entries)
            for status in ALLOWED_CLASSIFICATIONS
        }
        return {
            "content_sha256": self.content_sha256,
            "classifications": [item.classification for item in self.entries],
            "details": [
                _matrix_detail(index, item)
                for index, item in enumerate(self.entries)
                if item.classification != "needs_reverification"
            ],
            "entry_order": "public-symbol-inventory.symbols",
            "inventory_sha256": self.inventory_sha256,
            "needs_reverification_rationale": (
                "No symbol-level equivalence, verified exception, or out-of-scope evidence is registered."
            ),
            "schema": MATRIX_SCHEMA,
            "summary": {
                "classification_counts": counts,
                "entry_count": len(self.entries),
            },
            "upstream_commit": self.upstream_commit,
        }


@dataclass(frozen=True)
class _RepositoryManifestReceipt:
    repository_root: Path
    relative_paths: tuple[str, ...]
    tracker_sha256: str
    scope_sha256: str
    inventory_sha256: str
    matrix_sha256: str
    symbol_evidence_sha256: str
    scope_decisions_sha256: str


@dataclass(frozen=True)
class CompatibilityConfiguration:
    tracker: TrackerConfiguration
    scope: CompatibilityScope
    inventory: PublicSymbolInventory
    matrix: CompatibilityMatrix
    symbol_evidence: SymbolEvidenceRegistry | None = None
    scope_decisions: ScopeDecisionRegistry | None = None
    _repository_manifest_receipt: _RepositoryManifestReceipt | None = None

    def __post_init__(self) -> None:
        if (self.symbol_evidence is None) != (self.scope_decisions is None):
            raise ConfigurationError(
                "symbol evidence and scope decisions must be supplied together"
            )
        if self.symbol_evidence is not None and self.scope_decisions is not None:
            _validate_exact_registry_alignment(
                self.matrix,
                self.symbol_evidence,
                self.scope_decisions,
            )

    @property
    def needs_reverification(self) -> tuple[MatrixEntry, ...]:
        return tuple(
            item
            for item in self.matrix.entries
            if item.classification == "needs_reverification"
        )

    @property
    def exact_registry_coverage(self) -> bool:
        if (
            self.symbol_evidence is None
            or self.scope_decisions is None
            or self._repository_manifest_receipt is None
        ):
            return False
        receipt = self._repository_manifest_receipt
        expected_paths = tuple(
            f"upstream/{name}" for name in CANONICAL_MANIFEST_NAMES
        )
        if (
            receipt.relative_paths != expected_paths
            or receipt.tracker_sha256 != _tracker_content_sha256(self.tracker)
            or receipt.scope_sha256 != self.scope.content_sha256
            or receipt.inventory_sha256 != self.inventory.content_sha256
            or receipt.matrix_sha256 != self.matrix.content_sha256
            or receipt.symbol_evidence_sha256 != self.symbol_evidence.content_sha256
            or receipt.scope_decisions_sha256 != self.scope_decisions.content_sha256
        ):
            return False
        paths = tuple(receipt.repository_root / item for item in receipt.relative_paths)
        try:
            manifests_are_clean = (
                validate_repository_manifest_paths(receipt.repository_root, paths)
                == receipt.relative_paths
            )
        except ConfigurationError:
            return False
        return manifests_are_clean and _repository_manifests_match_configuration(
            self,
            receipt,
        )

    @property
    def required_assertion_ids(self) -> tuple[str, ...]:
        if self.symbol_evidence is None:
            return ()
        required: set[str] = set()
        evidence_by_key = self.symbol_evidence.entries_by_key
        for entry in self.matrix.entries:
            if entry.classification not in {"equivalent", "exception"}:
                continue
            registered = evidence_by_key.get(entry.key)
            if registered is not None:
                required.update(item.identifier for item in registered.receipts)
        return tuple(sorted(required))


@dataclass(frozen=True)
class CompatibilityReport:
    configuration: CompatibilityConfiguration
    source_identity: Mapping[str, Any] | None
    source_inventory_sha256: str | None
    source_matches_inventory: bool | None
    exception_bindings_match_source: bool | None
    evidence_execution: EvidenceExecution

    @property
    def classification_complete(self) -> bool:
        return not self.configuration.needs_reverification

    @property
    def pin_verified(self) -> bool:
        identity = self.source_identity
        return bool(
            identity
            and identity.get("kind") == "git_clone"
            and identity.get("pin_verified") is True
            and identity.get("commit") == self.configuration.tracker.lock.commit
            and identity.get("clean") is True
        )

    @property
    def passed(self) -> bool:
        return (
            self.classification_complete
            and self.configuration.exact_registry_coverage
            and self.evidence_execution.passed
            and self.exception_bindings_match_source is True
            and self.pin_verified
            and self.source_matches_inventory is True
        )

    def to_data(self) -> dict[str, Any]:
        configuration = self.configuration
        counts = {
            status: sum(item.classification == status for item in configuration.matrix.entries)
            for status in ALLOWED_CLASSIFICATIONS
        }
        unresolved = [
            {"path": item.path, "symbol": item.symbol}
            for item in configuration.needs_reverification
        ]
        evidence = configuration.symbol_evidence
        decisions = configuration.scope_decisions
        return {
            "classification_counts": counts,
            "gate": {
                "classification_complete": self.classification_complete,
                "exact_registry_coverage": configuration.exact_registry_coverage,
                "exception_bindings_match_source": self.exception_bindings_match_source,
                "exact_inventory_coverage": True,
                "required_symbol_evidence_satisfied": self.evidence_execution.passed,
                "no_skipped_evidence": not self.evidence_execution.skipped_assertion_ids,
                "no_structural_only_evidence": not self.evidence_execution.structural_only_assertion_ids,
                "exact_test_source_bindings": not self.evidence_execution.test_binding_mismatch_ids,
                "no_zero_load_active_overclaim": not self.evidence_execution.load_mismatch_ids,
                "passed": self.passed,
                "pinned_source_verified": self.pin_verified,
                "source_matches_inventory": self.source_matches_inventory,
            },
            "inventory": {
                "content_sha256": configuration.inventory.content_sha256,
                "python_file_count": len(configuration.inventory.files),
                "public_symbol_count": len(configuration.inventory.symbols),
            },
            "evidence_execution": self.evidence_execution.to_data(),
            "symbol_evidence": (
                None
                if evidence is None
                else {
                    "content_sha256": evidence.content_sha256,
                    "entry_count": len(evidence.entries),
                    "receipt_count": len(evidence.receipts),
                }
            ),
            "scope_decisions": (
                None
                if decisions is None
                else {
                    "content_sha256": decisions.content_sha256,
                    "decision_count": len(decisions.decisions),
                }
            ),
            "matrix": {
                "content_sha256": configuration.matrix.content_sha256,
                "entry_count": len(configuration.matrix.entries),
            },
            "pinned": {
                "commit": configuration.tracker.lock.commit,
                "repository": configuration.tracker.lock.repository,
            },
            "schema": REPORT_SCHEMA,
            "source": None if self.source_identity is None else dict(self.source_identity),
            "source_inventory_sha256": self.source_inventory_sha256,
            "unresolved": unresolved,
        }


def load_compatibility_scope(
    path: Path,
    tracker: TrackerConfiguration,
) -> CompatibilityScope:
    root = _json_mapping(path, "compatibility scope")
    _keys(
        root,
        required={
            "schema",
            "upstream_commit",
            "module_paths",
            "inventory_policy",
            "classifications",
            "completion_gate",
        },
        optional=set(),
        context="compatibility scope",
    )
    if _text(root["schema"], "compatibility scope.schema") != SCOPE_SCHEMA:
        raise ConfigurationError(f"compatibility scope.schema must be '{SCOPE_SCHEMA}'")
    commit = _text(root["upstream_commit"], "compatibility scope.upstream_commit").lower()
    if commit != tracker.lock.commit:
        raise ConfigurationError("compatibility scope.upstream_commit does not match upstream lock")
    module_paths = tuple(
        _relative_path(item, "compatibility scope.module_paths")
        for item in _sequence(root["module_paths"], "compatibility scope.module_paths")
    )
    if tuple(sorted(module_paths)) != tuple(sorted(tracker.lock.module_paths)):
        raise ConfigurationError("compatibility scope.module_paths must exactly match locked module paths")
    if len(module_paths) != len(set(module_paths)):
        raise ConfigurationError("compatibility scope.module_paths must be unique")

    policy = _mapping(root["inventory_policy"], "compatibility scope.inventory_policy")
    if policy != PUBLIC_INVENTORY_POLICY:
        raise ConfigurationError("compatibility scope.inventory_policy does not match tracker policy")
    classifications = _mapping(root["classifications"], "compatibility scope.classifications")
    _keys(
        classifications,
        required={"allowed", "complete"},
        optional=set(),
        context="compatibility scope.classifications",
    )
    if tuple(_text_sequence(classifications["allowed"], "classifications.allowed")) != ALLOWED_CLASSIFICATIONS:
        raise ConfigurationError("compatibility scope allowed classifications are not canonical")
    if tuple(_text_sequence(classifications["complete"], "classifications.complete")) != COMPLETE_CLASSIFICATIONS:
        raise ConfigurationError("compatibility scope complete classifications are not canonical")
    gate = _mapping(root["completion_gate"], "compatibility scope.completion_gate")
    expected_gate = {
        "forbid": ["needs_reverification"],
        "require_exact_inventory_coverage": True,
        "require_inventory_matches_pinned_source": True,
    }
    if gate != expected_gate:
        raise ConfigurationError("compatibility scope.completion_gate does not match fail-closed policy")
    return CompatibilityScope(commit, tuple(sorted(module_paths)))


def build_public_inventory(
    source_root: Path,
    scope: CompatibilityScope,
) -> PublicSymbolInventory:
    """Build the exhaustive AST-declared public-symbol inventory for *scope*."""

    snapshot = build_snapshot(
        source_root,
        scope.module_paths,
        require_tracked_paths=True,
    )
    return _inventory_from_snapshot(snapshot, scope)


def load_public_inventory(
    path: Path,
    scope: CompatibilityScope,
) -> PublicSymbolInventory:
    root = _json_mapping(path, "public symbol inventory")
    _keys(
        root,
        required={
            "schema",
            "upstream_commit",
            "scope_sha256",
            "content_sha256",
            "files",
            "symbols",
            "summary",
        },
        optional=set(),
        context="public symbol inventory",
    )
    if _text(root["schema"], "public symbol inventory.schema") != INVENTORY_SCHEMA:
        raise ConfigurationError(f"public symbol inventory.schema must be '{INVENTORY_SCHEMA}'")
    commit = _text(root["upstream_commit"], "public symbol inventory.upstream_commit").lower()
    if commit != scope.upstream_commit:
        raise ConfigurationError("public symbol inventory commit does not match compatibility scope")
    scope_sha256 = _hash(root["scope_sha256"], "public symbol inventory.scope_sha256")
    if scope_sha256 != scope.content_sha256:
        raise ConfigurationError("public symbol inventory scope hash does not match compatibility scope")

    files = tuple(_load_public_file(item, index) for index, item in enumerate(
        _sequence(root["files"], "public symbol inventory.files")
    ))
    symbols = tuple(_load_public_symbol(item, index) for index, item in enumerate(
        _sequence(root["symbols"], "public symbol inventory.symbols")
    ))
    inventory = PublicSymbolInventory(commit, scope_sha256, files, symbols)
    _validate_inventory(inventory, scope, root["summary"])
    supplied_hash = _hash(root["content_sha256"], "public symbol inventory.content_sha256")
    if supplied_hash != inventory.content_sha256:
        raise ConfigurationError("public symbol inventory content hash is invalid")
    return inventory


def load_compatibility_matrix(
    path: Path,
    scope: CompatibilityScope,
    inventory: PublicSymbolInventory,
    exceptions: tuple[CompatibilityException, ...],
) -> CompatibilityMatrix:
    root = _json_mapping(path, "compatibility matrix")
    _keys(
        root,
        required={
            "schema",
            "upstream_commit",
            "inventory_sha256",
            "content_sha256",
            "classifications",
            "details",
            "entry_order",
            "needs_reverification_rationale",
            "summary",
        },
        optional=set(),
        context="compatibility matrix",
    )
    if _text(root["schema"], "compatibility matrix.schema") != MATRIX_SCHEMA:
        raise ConfigurationError(f"compatibility matrix.schema must be '{MATRIX_SCHEMA}'")
    commit = _text(root["upstream_commit"], "compatibility matrix.upstream_commit").lower()
    if commit != scope.upstream_commit:
        raise ConfigurationError("compatibility matrix commit does not match compatibility scope")
    inventory_sha256 = _hash(root["inventory_sha256"], "compatibility matrix.inventory_sha256")
    if inventory_sha256 != inventory.content_sha256:
        raise ConfigurationError("compatibility matrix inventory hash does not match public inventory")
    entry_order = _text(root["entry_order"], "compatibility matrix.entry_order")
    if entry_order != "public-symbol-inventory.symbols":
        raise ConfigurationError(
            "compatibility matrix.entry_order must be 'public-symbol-inventory.symbols'"
        )
    needs_rationale = _text(
        root["needs_reverification_rationale"],
        "compatibility matrix.needs_reverification_rationale",
    )
    classifications = tuple(
        _text_sequence(root["classifications"], "compatibility matrix.classifications")
    )
    if len(classifications) != len(inventory.symbols):
        raise ConfigurationError(
            "compatibility matrix must classify every public inventory symbol exactly once"
        )
    invalid = next(
        (value for value in classifications if value not in ALLOWED_CLASSIFICATIONS),
        None,
    )
    if invalid is not None:
        raise ConfigurationError(
            "compatibility matrix classification must be one of "
            + ", ".join(ALLOWED_CLASSIFICATIONS)
        )
    details = _load_matrix_details(root["details"], len(classifications))
    entries_list: list[MatrixEntry] = []
    for index, (symbol, classification) in enumerate(zip(inventory.symbols, classifications)):
        detail = details.get(index)
        if classification == "needs_reverification":
            if detail is not None:
                raise ConfigurationError(
                    f"compatibility matrix detail[{index}] is not allowed for needs_reverification"
                )
            entries_list.append(
                MatrixEntry(
                    symbol.path,
                    symbol.symbol,
                    classification,
                    needs_rationale,
                    (),
                    None,
                )
            )
            continue
        if detail is None:
            raise ConfigurationError(
                f"compatibility matrix classification[{index}] requires evidence detail"
            )
        rationale, evidence, exception_id = detail
        if classification == "exception":
            if exception_id is None or not evidence:
                raise ConfigurationError(
                    f"compatibility matrix detail[{index}] exception requires exception_id and evidence"
                )
        elif exception_id is not None:
            raise ConfigurationError(
                f"compatibility matrix detail[{index}].exception_id is valid only for exception"
            )
        if classification == "equivalent" and not evidence:
            raise ConfigurationError(
                f"compatibility matrix detail[{index}] equivalent requires evidence"
            )
        entries_list.append(
            MatrixEntry(
                symbol.path,
                symbol.symbol,
                classification,
                rationale,
                evidence,
                exception_id,
            )
        )
    if set(details) != {
        index
        for index, classification in enumerate(classifications)
        if classification != "needs_reverification"
    }:
        raise ConfigurationError("compatibility matrix details do not match classified entries")
    entries = tuple(entries_list)
    matrix = CompatibilityMatrix(commit, inventory_sha256, entries)
    _validate_matrix(matrix, inventory, exceptions, root["summary"])
    supplied_hash = _hash(root["content_sha256"], "compatibility matrix.content_sha256")
    if supplied_hash != matrix.content_sha256:
        raise ConfigurationError("compatibility matrix content hash is invalid")
    return matrix


def load_compatibility_configuration(
    tracker: TrackerConfiguration,
    scope_path: Path,
    inventory_path: Path,
    matrix_path: Path,
    symbol_evidence_path: Path | None = None,
    scope_decisions_path: Path | None = None,
    repository_root: Path | None = None,
) -> CompatibilityConfiguration:
    validation_root = (repository_root or scope_path.resolve().parent.parent).resolve()
    scope = load_compatibility_scope(scope_path, tracker)
    inventory = load_public_inventory(inventory_path, scope)
    matrix = load_compatibility_matrix(
        matrix_path,
        scope,
        inventory,
        tracker.exceptions,
    )
    evidence = load_symbol_evidence(
        symbol_evidence_path or inventory_path.with_name("symbol-evidence.json"),
        inventory,
        repository_root=validation_root,
    )
    decisions = load_scope_decisions(
        scope_decisions_path or inventory_path.with_name("scope-decisions.json"),
        inventory,
        repository_root=validation_root,
    )
    _validate_exact_registry_alignment(matrix, evidence, decisions)
    evidence_path = symbol_evidence_path or inventory_path.with_name("symbol-evidence.json")
    decisions_path = scope_decisions_path or inventory_path.with_name("scope-decisions.json")
    manifest_paths = (
        *tracker.manifest_paths,
        scope_path.resolve(),
        inventory_path.resolve(),
        matrix_path.resolve(),
        evidence_path.resolve(),
        decisions_path.resolve(),
    )
    canonical_paths = tuple(
        validation_root / "upstream" / name for name in CANONICAL_MANIFEST_NAMES
    )
    receipt: _RepositoryManifestReceipt | None = None
    if tuple(path.resolve() for path in manifest_paths) == tuple(
        path.resolve() for path in canonical_paths
    ):
        try:
            relative_paths = validate_repository_manifest_paths(
                validation_root,
                manifest_paths,
            )
        except ConfigurationError:
            pass
        else:
            receipt = _RepositoryManifestReceipt(
                validation_root,
                relative_paths,
                _tracker_content_sha256(tracker),
                scope.content_sha256,
                inventory.content_sha256,
                matrix.content_sha256,
                evidence.content_sha256,
                decisions.content_sha256,
            )
    return CompatibilityConfiguration(
        tracker,
        scope,
        inventory,
        matrix,
        evidence,
        decisions,
        receipt,
    )


def build_compatibility_report(
    configuration: CompatibilityConfiguration,
    *,
    source_root: Path | None = None,
    source_identity: Mapping[str, Any] | None = None,
    evidence_results: Sequence[EvidenceResults] = (),
) -> CompatibilityReport:
    # Caller-provided identity data is diagnostic-only legacy input. Authority is
    # derived here from the actual source root so a direct API caller cannot mint
    # a verified pin with a mapping containing ``pin_verified: true``.
    del source_identity
    inspected_identity = _inspect_report_source_identity(configuration, source_root)
    source_hash: str | None = None
    matches: bool | None = None
    exception_matches: bool | None = None
    if source_root is not None:
        generated = build_public_inventory(source_root, configuration.scope)
        source_hash = generated.content_sha256
        matches = source_hash == configuration.inventory.content_sha256
        snapshot = build_snapshot(
            source_root,
            configuration.tracker.lock.module_paths,
            require_tracked_paths=True,
        )
        source_files = snapshot.files_by_path
        exception_matches = all(
            item.upstream_path is None
            or (
                item.upstream_symbol is not None
                and item.upstream_symbol_hash is not None
                and item.upstream_path in source_files
                and item.upstream_symbol
                in source_files[item.upstream_path].symbols_by_name
                and source_files[item.upstream_path]
                .symbols_by_name[item.upstream_symbol]
                .hash
                == item.upstream_symbol_hash
            )
            for item in configuration.tracker.exceptions
        )
    evidence = configuration.symbol_evidence
    if evidence is None:
        execution = EvidenceExecution(
            False,
            configuration.required_assertion_ids,
            (),
            (),
            (),
            (),
            configuration.required_assertion_ids,
            (),
            (),
            (),
            (),
            (),
            (),
        )
    else:
        execution = evaluate_evidence_execution(
            evidence,
            configuration.required_assertion_ids,
            evidence_results,
        )
    return CompatibilityReport(
        configuration,
        inspected_identity,
        source_hash,
        matches,
        exception_matches,
        execution,
    )


def build_reverification_matrix(
    inventory: PublicSymbolInventory,
    exceptions: tuple[CompatibilityException, ...],
) -> CompatibilityMatrix:
    """Create an honest review template without inferring equivalence."""

    entries: list[MatrixEntry] = []
    for symbol in inventory.symbols:
        entries.append(
            MatrixEntry(
                symbol.path,
                symbol.symbol,
                "needs_reverification",
                "No symbol-level equivalence, verified exception, or out-of-scope evidence is registered.",
                (),
                None,
            )
        )
    return CompatibilityMatrix(
        inventory.upstream_commit,
        inventory.content_sha256,
        tuple(entries),
    )


def rebase_compatibility_inventory(
    configuration: CompatibilityConfiguration,
    inventory: PublicSymbolInventory,
) -> CompatibilityConfiguration:
    """Rebind registries after file-byte drift with an identical AST contract."""

    if configuration.symbol_evidence is None or configuration.scope_decisions is None:
        raise ConfigurationError("exact registries are required for inventory rebasing")
    if (
        inventory.upstream_commit != configuration.inventory.upstream_commit
        or inventory.scope_sha256 != configuration.inventory.scope_sha256
    ):
        raise ConfigurationError("replacement inventory pin or scope does not match")
    old_file_contract = tuple(
        (item.path, item.ast_hash) for item in configuration.inventory.files
    )
    new_file_contract = tuple((item.path, item.ast_hash) for item in inventory.files)
    if new_file_contract != old_file_contract:
        raise ConfigurationError("replacement inventory changes a file path or AST hash")
    if inventory.symbols != configuration.inventory.symbols:
        raise ConfigurationError(
            "replacement inventory changes the exact public-symbol contract"
        )
    matrix = CompatibilityMatrix(
        inventory.upstream_commit,
        inventory.content_sha256,
        configuration.matrix.entries,
    )
    evidence = SymbolEvidenceRegistry(
        inventory.upstream_commit,
        inventory.content_sha256,
        configuration.symbol_evidence.entries,
    )
    decisions = ScopeDecisionRegistry(
        inventory.upstream_commit,
        inventory.content_sha256,
        configuration.scope_decisions.decisions,
    )
    return CompatibilityConfiguration(
        configuration.tracker,
        configuration.scope,
        inventory,
        matrix,
        evidence,
        decisions,
    )


def render_compatibility_report(report: CompatibilityReport) -> str:
    return json.dumps(report.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def write_compatibility_report(report: CompatibilityReport, path: Path) -> Path:
    try:
        write_text_atomically(path, render_compatibility_report(report))
    except OSError as exception:
        raise ConfigurationError(f"Cannot write compatibility report '{path}': {exception}") from exception
    return path


def render_public_inventory(inventory: PublicSymbolInventory) -> str:
    return json.dumps(inventory.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def render_compatibility_matrix(matrix: CompatibilityMatrix) -> str:
    return json.dumps(matrix.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def _matrix_detail(index: int, entry: MatrixEntry) -> dict[str, Any]:
    data: dict[str, Any] = {
        "evidence": list(entry.evidence),
        "index": index,
        "rationale": entry.rationale,
    }
    if entry.exception_id is not None:
        data["exception_id"] = entry.exception_id
    return data


def _inventory_from_snapshot(
    snapshot: SourceSnapshot,
    scope: CompatibilityScope,
) -> PublicSymbolInventory:
    files: list[PublicFile] = []
    symbols: list[PublicSymbol] = []
    for file in snapshot.files:
        if file.kind != "python" or file.ast_hash is None:
            continue
        files.append(PublicFile(file.path, file.content_hash, file.ast_hash))
        for symbol in file.symbols:
            if _is_public_symbol(symbol.name, symbol.kind):
                symbols.append(
                    PublicSymbol(
                        file.path,
                        symbol.name,
                        symbol.kind,
                        symbol.hash,
                        symbol.signature_hash,
                        symbol.body_hash,
                    )
                )
    return PublicSymbolInventory(
        scope.upstream_commit,
        scope.content_sha256,
        tuple(sorted(files, key=lambda item: item.path)),
        tuple(sorted(symbols, key=lambda item: item.key)),
    )


def _is_public_symbol(name: str, kind: str) -> bool:
    if kind not in PUBLIC_INVENTORY_POLICY["include_kinds"] or name == "<module>":
        return False
    parts = name.split(".")
    if not parts or parts[0].startswith("_"):
        return False
    if not all(not part.startswith("_") or _is_dunder(part) for part in parts[1:]):
        return False
    return kind != "constant" or parts[-1].isupper()


def _is_dunder(value: str) -> bool:
    return len(value) > 4 and value.startswith("__") and value.endswith("__")


def _load_public_file(value: Any, index: int) -> PublicFile:
    context = f"public symbol inventory.files[{index}]"
    item = _mapping(value, context)
    _keys(item, required={"path", "content_hash", "ast_hash"}, optional=set(), context=context)
    path = _relative_path(item["path"], f"{context}.path")
    if not path.endswith(".py"):
        raise ConfigurationError(f"{context}.path must be a Python source file")
    return PublicFile(
        path,
        _hash(item["content_hash"], f"{context}.content_hash"),
        _hash(item["ast_hash"], f"{context}.ast_hash"),
    )


def _load_public_symbol(value: Any, index: int) -> PublicSymbol:
    context = f"public symbol inventory.symbols[{index}]"
    item = _mapping(value, context)
    _keys(
        item,
        required={
            "path",
            "symbol",
            "kind",
            "symbol_hash",
            "signature_hash",
            "body_hash",
        },
        optional=set(),
        context=context,
    )
    symbol = _text(item["symbol"], f"{context}.symbol")
    kind = _text(item["kind"], f"{context}.kind")
    if not _is_public_symbol(symbol, kind):
        raise ConfigurationError(f"{context} is outside the canonical public-symbol policy")
    return PublicSymbol(
        _relative_path(item["path"], f"{context}.path"),
        symbol,
        kind,
        _hash(item["symbol_hash"], f"{context}.symbol_hash"),
        _hash(item["signature_hash"], f"{context}.signature_hash"),
        _hash(item["body_hash"], f"{context}.body_hash"),
    )


def _load_matrix_details(
    value: Any,
    classification_count: int,
) -> dict[int, tuple[str, tuple[str, ...], str | None]]:
    result: dict[int, tuple[str, tuple[str, ...], str | None]] = {}
    for position, raw in enumerate(_sequence(value, "compatibility matrix.details")):
        context = f"compatibility matrix.details[{position}]"
        item = _mapping(raw, context)
        _keys(
            item,
            required={"index", "rationale", "evidence"},
            optional={"exception_id"},
            context=context,
        )
        index = item["index"]
        if not isinstance(index, int) or isinstance(index, bool) or not 0 <= index < classification_count:
            raise ConfigurationError(f"{context}.index is outside the inventory")
        if index in result:
            raise ConfigurationError(f"{context}.index must be unique")
        evidence = tuple(_text_sequence(item["evidence"], f"{context}.evidence"))
        if len(evidence) != len(set(evidence)):
            raise ConfigurationError(f"{context}.evidence must be unique")
        exception_id = (
            None
            if "exception_id" not in item
            else _text(item["exception_id"], f"{context}.exception_id")
        )
        result[index] = (
            _text(item["rationale"], f"{context}.rationale"),
            evidence,
            exception_id,
        )
    return result


def _validate_exact_registry_alignment(
    matrix: CompatibilityMatrix,
    evidence: SymbolEvidenceRegistry,
    decisions: ScopeDecisionRegistry,
) -> None:
    """Bind complete matrix claims to one exact, hash-validated registry entry."""

    evidence_by_key = evidence.entries_by_key
    decisions_by_key = decisions.decisions_by_key
    matrix_by_key = matrix.entries_by_key

    for key, registered in evidence_by_key.items():
        matrix_entry = matrix_by_key.get(key)
        if matrix_entry is None:
            raise ConfigurationError(
                f"symbol evidence classifies unknown matrix symbol '{key[0]}::{key[1]}'"
            )
        if matrix_entry.classification not in {
            "equivalent",
            "exception",
            "needs_reverification",
        }:
            raise ConfigurationError(
                f"symbol evidence for '{key[0]}::{key[1]}' conflicts with "
                f"matrix classification '{matrix_entry.classification}'"
            )
        if matrix_entry.classification == "needs_reverification":
            # Prepared definitions are candidates only.  Their presence never
            # mutates or completes the honest matrix classification.
            continue
        expected_references = [
            f"upstream/symbol-evidence.json#{receipt.identifier}"
            for receipt in registered.receipts
        ]
        if matrix_entry.classification == "exception":
            expected_references.append(
                "upstream/compatibility-exceptions.yml#"
                f"{matrix_entry.exception_id}"
            )
        expected_references_tuple = tuple(sorted(expected_references))
        if tuple(matrix_entry.evidence) != expected_references_tuple:
            raise ConfigurationError(
                f"{matrix_entry.classification} matrix entry '{key[0]}::{key[1]}' "
                "must reference every exact assertion receipt and no broad test-file evidence"
            )

    missing_evidence = next(
        (
            entry
            for entry in matrix.entries
            if entry.classification in {"equivalent", "exception"}
            and entry.key not in evidence_by_key
        ),
        None,
    )
    if missing_evidence is not None:
        raise ConfigurationError(
            "symbol evidence registry is missing completed symbol "
            f"'{missing_evidence.path}::{missing_evidence.symbol}'"
        )

    out_of_scope_keys = {
        entry.key for entry in matrix.entries if entry.classification == "out_of_scope"
    }
    decision_keys = set(decisions_by_key)
    missing_decisions = sorted(out_of_scope_keys - decision_keys)
    if missing_decisions:
        path, symbol = missing_decisions[0]
        raise ConfigurationError(
            f"scope decision registry is missing out-of-scope symbol '{path}::{symbol}'"
        )
    extra_decisions = sorted(decision_keys - out_of_scope_keys)
    if extra_decisions:
        path, symbol = extra_decisions[0]
        raise ConfigurationError(
            f"scope decision registry overclaims non-out-of-scope symbol '{path}::{symbol}'"
        )
    for key in sorted(out_of_scope_keys):
        matrix_entry = matrix_by_key[key]
        decision = decisions_by_key[key]
        expected_reference = f"upstream/scope-decisions.json#{decision.identifier}"
        if matrix_entry.evidence != (expected_reference,):
            raise ConfigurationError(
                f"out-of-scope matrix entry '{key[0]}::{key[1]}' must reference its "
                "exact scope decision and no broad policy claim"
            )


def _validate_inventory(
    inventory: PublicSymbolInventory,
    scope: CompatibilityScope,
    summary_value: Any,
) -> None:
    file_paths = [item.path for item in inventory.files]
    if file_paths != sorted(file_paths) or len(file_paths) != len(set(file_paths)):
        raise ConfigurationError("public symbol inventory files must be unique and sorted")
    outside_scope = next(
        (
            path
            for path in file_paths
            if not any(
                PurePosixPath(module_path) in PurePosixPath(path).parents
                for module_path in scope.module_paths
            )
        ),
        None,
    )
    if outside_scope is not None:
        raise ConfigurationError(
            f"public symbol inventory file '{outside_scope}' is outside compatibility scope"
        )
    symbol_keys = [item.key for item in inventory.symbols]
    if symbol_keys != sorted(symbol_keys) or len(symbol_keys) != len(set(symbol_keys)):
        raise ConfigurationError("public symbol inventory symbols must be unique and sorted")
    known_files = set(file_paths)
    orphan = next((item for item in inventory.symbols if item.path not in known_files), None)
    if orphan is not None:
        raise ConfigurationError(
            f"public symbol inventory symbol '{orphan.path}::{orphan.symbol}' has no file record"
        )
    summary = _mapping(summary_value, "public symbol inventory.summary")
    _keys(
        summary,
        required={"python_file_count", "public_symbol_count", "kind_counts"},
        optional=set(),
        context="public symbol inventory.summary",
    )
    expected_counts = {
        kind: sum(item.kind == kind for item in inventory.symbols)
        for kind in PUBLIC_INVENTORY_POLICY["include_kinds"]
    }
    if summary != {
        "python_file_count": len(inventory.files),
        "public_symbol_count": len(inventory.symbols),
        "kind_counts": expected_counts,
    }:
        raise ConfigurationError("public symbol inventory.summary is inconsistent with entries")


def _validate_matrix(
    matrix: CompatibilityMatrix,
    inventory: PublicSymbolInventory,
    exceptions: tuple[CompatibilityException, ...],
    summary_value: Any,
) -> None:
    entry_keys = [item.key for item in matrix.entries]
    if entry_keys != sorted(entry_keys) or len(entry_keys) != len(set(entry_keys)):
        raise ConfigurationError("compatibility matrix entries must be unique and sorted")
    inventory_keys = set(inventory.symbols_by_key)
    matrix_keys = set(matrix.entries_by_key)
    missing = sorted(inventory_keys - matrix_keys)
    if missing:
        path, symbol = missing[0]
        raise ConfigurationError(
            f"compatibility matrix is missing classification for '{path}::{symbol}'"
        )
    extra = sorted(matrix_keys - inventory_keys)
    if extra:
        path, symbol = extra[0]
        raise ConfigurationError(
            f"compatibility matrix classifies unknown symbol '{path}::{symbol}'"
        )

    exceptions_by_id = {item.identifier: item for item in exceptions}
    for entry in matrix.entries:
        if entry.classification != "exception":
            continue
        registered = exceptions_by_id.get(entry.exception_id or "")
        if registered is None:
            raise ConfigurationError(
                f"compatibility matrix references unknown exception '{entry.exception_id}'"
            )
        if registered.upstream_path != entry.path or registered.upstream_symbol != entry.symbol:
            raise ConfigurationError(
                f"compatibility exception '{entry.exception_id}' does not identify "
                f"'{entry.path}::{entry.symbol}'"
            )
        inventory_symbol = inventory.symbols_by_key[entry.key]
        if registered.upstream_symbol_hash != inventory_symbol.symbol_hash:
            raise ConfigurationError(
                f"compatibility exception '{entry.exception_id}' has a stale upstream symbol hash"
            )

    summary = _mapping(summary_value, "compatibility matrix.summary")
    _keys(
        summary,
        required={"entry_count", "classification_counts"},
        optional=set(),
        context="compatibility matrix.summary",
    )
    expected_counts = {
        status: sum(item.classification == status for item in matrix.entries)
        for status in ALLOWED_CLASSIFICATIONS
    }
    if summary != {
        "entry_count": len(matrix.entries),
        "classification_counts": expected_counts,
    }:
        raise ConfigurationError("compatibility matrix.summary is inconsistent with entries")


def _json_mapping(path: Path, context: str) -> Mapping[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8-sig"))
    except OSError as exception:
        raise ConfigurationError(f"Cannot read {context} '{path}': {exception}") from exception
    except json.JSONDecodeError as exception:
        raise ConfigurationError(
            f"{path}:{exception.lineno}:{exception.colno}: invalid JSON: {exception.msg}"
        ) from exception
    return _mapping(value, context)


def _sha256_data(value: Any) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return f"sha256:{hashlib.sha256(encoded).hexdigest()}"


def _tracker_content_sha256(tracker: TrackerConfiguration) -> str:
    data = asdict(tracker)
    data.pop("manifest_paths", None)
    return _sha256_data(data)


def _repository_manifests_match_configuration(
    configuration: CompatibilityConfiguration,
    receipt: _RepositoryManifestReceipt,
) -> bool:
    """Reparse clean canonical HEAD manifests and match them to the live objects."""

    paths = tuple(receipt.repository_root / item for item in receipt.relative_paths)
    try:
        tracker = load_configuration(paths[0], paths[1], paths[2])
        canonical = load_compatibility_configuration(
            tracker,
            paths[3],
            paths[4],
            paths[5],
            paths[6],
            paths[7],
            repository_root=receipt.repository_root,
        )
    except (ConfigurationError, OSError):
        return False
    return (
        canonical._repository_manifest_receipt is not None
        and _tracker_content_sha256(canonical.tracker)
        == _tracker_content_sha256(configuration.tracker)
        and canonical.scope.content_sha256 == configuration.scope.content_sha256
        and canonical.inventory.content_sha256 == configuration.inventory.content_sha256
        and canonical.matrix.content_sha256 == configuration.matrix.content_sha256
        and canonical.symbol_evidence is not None
        and configuration.symbol_evidence is not None
        and canonical.symbol_evidence.content_sha256
        == configuration.symbol_evidence.content_sha256
        and canonical.scope_decisions is not None
        and configuration.scope_decisions is not None
        and canonical.scope_decisions.content_sha256
        == configuration.scope_decisions.content_sha256
    )


def _inspect_report_source_identity(
    configuration: CompatibilityConfiguration,
    source_root: Path | None,
) -> Mapping[str, Any] | None:
    if source_root is None:
        return None
    try:
        identity = inspect_source_identity(
            source_root,
            expected_commit=configuration.tracker.lock.commit,
            expected_repository=configuration.tracker.lock.repository,
        )
    except SourceError:
        try:
            identity = inspect_source_identity(source_root)
        except SourceError:
            return {
                "branch": None,
                "clean": None,
                "commit": None,
                "kind": "invalid_source_root",
                "pin_verified": False,
                "repository": None,
            }
    return identity.to_data()


def _hash(value: Any, context: str) -> str:
    text = _text(value, context)
    if _SHA256.fullmatch(text) is None:
        raise ConfigurationError(f"{context} must be a lowercase sha256 hash")
    return text


def _relative_path(value: Any, context: str) -> str:
    text = _text(value, context)
    if "\\" in text or text.startswith("/") or re.match(r"^[A-Za-z]:", text):
        raise ConfigurationError(f"{context} must be a relative POSIX path")
    path = PurePosixPath(text)
    if not path.parts or any(part in {"", ".", ".."} for part in path.parts):
        raise ConfigurationError(f"{context} contains an invalid path segment")
    return path.as_posix()


def _text(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip() or value != value.strip():
        raise ConfigurationError(f"{context} must be a non-empty trimmed string")
    return value


def _text_sequence(value: Any, context: str) -> tuple[str, ...]:
    return tuple(_text(item, context) for item in _sequence(value, context))


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

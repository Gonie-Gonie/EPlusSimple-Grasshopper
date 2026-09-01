"""Exact, hash-bound compatibility evidence and product-scope decisions."""

from __future__ import annotations

from dataclasses import dataclass
import ast
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
from typing import TYPE_CHECKING, Any, Mapping, Sequence

from .errors import ConfigurationError

if TYPE_CHECKING:
    from .compatibility import PublicSymbolInventory


EVIDENCE_SCHEMA = "dragons.upstream-symbol-evidence.v1"
EVIDENCE_RESULTS_SCHEMA = "dragons.upstream-evidence-results.v1"
SCOPE_DECISIONS_SCHEMA = "dragons.upstream-scope-decisions.v1"

_SHA256 = re.compile(r"^sha256:[0-9a-f]{64}$")
_IDENTIFIER = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
_BROAD_REFERENCE_CHARACTERS = frozenset("*?[]{}")
_WINDOWS_INVALID_PATH_CHARACTERS = frozenset('<>:"|')
_WINDOWS_RESERVED_PATH_PARTS = frozenset(
    {"aux", "con", "nul", "prn"}
    | {f"com{index}" for index in range(1, 10)}
    | {f"lpt{index}" for index in range(1, 10)}
)
_VERIFICATION_KINDS = frozenset(
    {
        "unit_behavior",
        "cross_language",
        "energyplus_integration",
        "rhino_workflow",
    }
)
_LOAD_CASES = frozenset({"not_applicable", "zero", "nonzero"})


@dataclass(frozen=True)
class EvidenceReceipt:
    identifier: str
    test_path: str
    test_symbol: str
    test_source_sha256: str
    assertion: str
    verification_kind: str
    outcome: str
    skipped: bool
    structural_only: bool
    exercised_load: str
    claims_active_load: bool
    expected_output_sha256: str

    def to_data(self) -> dict[str, Any]:
        return {
            "assertion": self.assertion,
            "claims_active_load": self.claims_active_load,
            "exercised_load": self.exercised_load,
            "expected_output_sha256": self.expected_output_sha256,
            "id": self.identifier,
            "outcome": self.outcome,
            "skipped": self.skipped,
            "structural_only": self.structural_only,
            "test_path": self.test_path,
            "test_source_sha256": self.test_source_sha256,
            "test_symbol": self.test_symbol,
            "verification_kind": self.verification_kind,
        }


@dataclass(frozen=True)
class SymbolEvidence:
    path: str
    symbol: str
    upstream_symbol_hash: str
    implementation_path: str
    implementation_symbol: str
    implementation_source_sha256: str
    receipts: tuple[EvidenceReceipt, ...]

    @property
    def key(self) -> tuple[str, str]:
        return self.path, self.symbol

    @property
    def exact_key(self) -> tuple[str, str, str]:
        return self.path, self.symbol, self.upstream_symbol_hash

    def to_data(self) -> dict[str, Any]:
        return {
            "implementation": {
                "path": self.implementation_path,
                "source_sha256": self.implementation_source_sha256,
                "symbol": self.implementation_symbol,
            },
            "path": self.path,
            "receipts": [item.to_data() for item in self.receipts],
            "symbol": self.symbol,
            "upstream_symbol_hash": self.upstream_symbol_hash,
        }


@dataclass(frozen=True)
class SymbolEvidenceRegistry:
    upstream_commit: str
    inventory_sha256: str
    entries: tuple[SymbolEvidence, ...]

    @property
    def entries_by_key(self) -> dict[tuple[str, str], SymbolEvidence]:
        return {item.key: item for item in self.entries}

    @property
    def receipts(self) -> tuple[EvidenceReceipt, ...]:
        return tuple(receipt for entry in self.entries for receipt in entry.receipts)

    @property
    def receipts_by_id(self) -> dict[str, EvidenceReceipt]:
        return {item.identifier: item for item in self.receipts}

    @property
    def content_sha256(self) -> str:
        return _sha256_data(self._content_data())

    def _content_data(self) -> dict[str, Any]:
        return {
            "entries": [item.to_data() for item in self.entries],
            "inventory_sha256": self.inventory_sha256,
            "upstream_commit": self.upstream_commit,
        }

    def to_data(self) -> dict[str, Any]:
        receipts = self.receipts
        return {
            "content_sha256": self.content_sha256,
            "entries": [item.to_data() for item in self.entries],
            "inventory_sha256": self.inventory_sha256,
            "schema": EVIDENCE_SCHEMA,
            "summary": {
                "entry_count": len(self.entries),
                "passed_receipt_count": sum(item.outcome == "passed" for item in receipts),
                "receipt_count": len(receipts),
                "skipped_receipt_count": sum(item.skipped for item in receipts),
                "structural_only_receipt_count": sum(item.structural_only for item in receipts),
                "zero_load_active_claim_count": sum(
                    item.claims_active_load and item.exercised_load != "nonzero"
                    for item in receipts
                ),
            },
            "upstream_commit": self.upstream_commit,
        }


@dataclass(frozen=True)
class ExecutedAssertion:
    assertion_id: str
    test_path: str
    test_symbol: str
    test_source_sha256: str
    outcome: str
    skipped: bool
    structural_only: bool
    exercised_load: str
    output_sha256: str

    def to_data(self) -> dict[str, Any]:
        return {
            "assertion_id": self.assertion_id,
            "exercised_load": self.exercised_load,
            "outcome": self.outcome,
            "output_sha256": self.output_sha256,
            "skipped": self.skipped,
            "structural_only": self.structural_only,
            "test_path": self.test_path,
            "test_source_sha256": self.test_source_sha256,
            "test_symbol": self.test_symbol,
        }


@dataclass(frozen=True)
class EvidenceResults:
    upstream_commit: str
    inventory_sha256: str
    symbol_evidence_sha256: str
    collector_path: str
    collector_symbol: str
    collector_source_sha256: str
    assertions: tuple[ExecutedAssertion, ...]
    target_framework: str

    @property
    def assertions_by_id(self) -> dict[str, ExecutedAssertion]:
        return {item.assertion_id: item for item in self.assertions}

    @property
    def content_sha256(self) -> str:
        return _sha256_data(self._content_data())

    def _content_data(self) -> dict[str, Any]:
        return {
            "assertions": [item.to_data() for item in self.assertions],
            "collector": {
                "path": self.collector_path,
                "source_sha256": self.collector_source_sha256,
                "symbol": self.collector_symbol,
            },
            "inventory_sha256": self.inventory_sha256,
            "symbol_evidence_sha256": self.symbol_evidence_sha256,
            "target_framework": self.target_framework,
            "upstream_commit": self.upstream_commit,
        }

    def to_data(self) -> dict[str, Any]:
        return {
            "assertions": [item.to_data() for item in self.assertions],
            "collector": {
                "path": self.collector_path,
                "source_sha256": self.collector_source_sha256,
                "symbol": self.collector_symbol,
            },
            "content_sha256": self.content_sha256,
            "inventory_sha256": self.inventory_sha256,
            "schema": EVIDENCE_RESULTS_SCHEMA,
            "symbol_evidence_sha256": self.symbol_evidence_sha256,
            "target_framework": self.target_framework,
            "summary": {
                "assertion_count": len(self.assertions),
                "failed_count": sum(item.outcome == "failed" for item in self.assertions),
                "passed_count": sum(item.outcome == "passed" for item in self.assertions),
                "skipped_count": sum(item.skipped for item in self.assertions),
                "structural_only_count": sum(item.structural_only for item in self.assertions),
            },
            "upstream_commit": self.upstream_commit,
        }


@dataclass(frozen=True)
class EvidenceExecution:
    authoritative: bool
    required_assertion_ids: tuple[str, ...]
    collected_assertion_ids: tuple[str, ...]
    result_artifact_sha256s: tuple[str, ...]
    result_artifacts: tuple[Mapping[str, Any], ...]
    target_frameworks: tuple[str, ...]
    missing_assertion_ids: tuple[str, ...]
    failed_assertion_ids: tuple[str, ...]
    skipped_assertion_ids: tuple[str, ...]
    structural_only_assertion_ids: tuple[str, ...]
    output_hash_mismatch_ids: tuple[str, ...]
    load_mismatch_ids: tuple[str, ...]
    test_binding_mismatch_ids: tuple[str, ...]

    @property
    def assertions_satisfied(self) -> bool:
        return not any(
            (
                self.missing_assertion_ids,
                self.failed_assertion_ids,
                self.skipped_assertion_ids,
                self.structural_only_assertion_ids,
                self.output_hash_mismatch_ids,
                self.load_mismatch_ids,
                self.test_binding_mismatch_ids,
            )
        )

    @property
    def passed(self) -> bool:
        return self.assertions_satisfied and (
            not self.required_assertion_ids or self.authoritative
        )

    def to_data(self) -> dict[str, Any]:
        return {
            "assertions_satisfied": self.assertions_satisfied,
            "authoritative": self.authoritative,
            "collected_assertion_count": len(self.collected_assertion_ids),
            "collected_assertion_ids": list(self.collected_assertion_ids),
            "failed_assertion_ids": list(self.failed_assertion_ids),
            "load_mismatch_ids": list(self.load_mismatch_ids),
            "missing_assertion_ids": list(self.missing_assertion_ids),
            "output_hash_mismatch_ids": list(self.output_hash_mismatch_ids),
            "passed": self.passed,
            "required_assertion_count": len(self.required_assertion_ids),
            "required_assertion_ids": list(self.required_assertion_ids),
            "result_artifact_sha256s": list(self.result_artifact_sha256s),
            "result_artifacts": [dict(item) for item in self.result_artifacts],
            "skipped_assertion_ids": list(self.skipped_assertion_ids),
            "structural_only_assertion_ids": list(self.structural_only_assertion_ids),
            "test_binding_mismatch_ids": list(self.test_binding_mismatch_ids),
            "target_frameworks": list(self.target_frameworks),
        }


@dataclass(frozen=True)
class ScopeDecision:
    identifier: str
    path: str
    symbol: str
    upstream_symbol_hash: str
    decision: str
    product_contract: str
    rationale: str
    policy_reference: str
    approval: str

    @property
    def key(self) -> tuple[str, str]:
        return self.path, self.symbol

    @property
    def exact_key(self) -> tuple[str, str, str]:
        return self.path, self.symbol, self.upstream_symbol_hash

    def to_data(self) -> dict[str, str]:
        return {
            "approval": self.approval,
            "decision": self.decision,
            "id": self.identifier,
            "path": self.path,
            "policy_reference": self.policy_reference,
            "product_contract": self.product_contract,
            "rationale": self.rationale,
            "symbol": self.symbol,
            "upstream_symbol_hash": self.upstream_symbol_hash,
        }


@dataclass(frozen=True)
class ScopeDecisionRegistry:
    upstream_commit: str
    inventory_sha256: str
    decisions: tuple[ScopeDecision, ...]

    @property
    def decisions_by_key(self) -> dict[tuple[str, str], ScopeDecision]:
        return {item.key: item for item in self.decisions}

    @property
    def content_sha256(self) -> str:
        return _sha256_data(self._content_data())

    def _content_data(self) -> dict[str, Any]:
        return {
            "decisions": [item.to_data() for item in self.decisions],
            "inventory_sha256": self.inventory_sha256,
            "upstream_commit": self.upstream_commit,
        }

    def to_data(self) -> dict[str, Any]:
        return {
            "content_sha256": self.content_sha256,
            "decisions": [item.to_data() for item in self.decisions],
            "inventory_sha256": self.inventory_sha256,
            "schema": SCOPE_DECISIONS_SCHEMA,
            "summary": {
                "approved_count": sum(item.approval == "approved" for item in self.decisions),
                "decision_count": len(self.decisions),
                "out_of_scope_count": sum(
                    item.decision == "out_of_scope" for item in self.decisions
                ),
            },
            "upstream_commit": self.upstream_commit,
        }


def empty_symbol_evidence(inventory: PublicSymbolInventory) -> SymbolEvidenceRegistry:
    return SymbolEvidenceRegistry(inventory.upstream_commit, inventory.content_sha256, ())


def empty_scope_decisions(inventory: PublicSymbolInventory) -> ScopeDecisionRegistry:
    return ScopeDecisionRegistry(inventory.upstream_commit, inventory.content_sha256, ())


def load_symbol_evidence(
    path: Path,
    inventory: PublicSymbolInventory,
    *,
    repository_root: Path,
) -> SymbolEvidenceRegistry:
    root = _json_mapping(path, "symbol evidence registry")
    _keys(
        root,
        required={
            "schema",
            "upstream_commit",
            "inventory_sha256",
            "content_sha256",
            "entries",
            "summary",
        },
        optional=set(),
        context="symbol evidence registry",
    )
    if _text(root["schema"], "symbol evidence registry.schema") != EVIDENCE_SCHEMA:
        raise ConfigurationError(
            f"symbol evidence registry.schema must be '{EVIDENCE_SCHEMA}'"
        )
    commit = _text(root["upstream_commit"], "symbol evidence registry.upstream_commit").lower()
    if commit != inventory.upstream_commit:
        raise ConfigurationError("symbol evidence registry commit does not match public inventory")
    inventory_hash = _hash(
        root["inventory_sha256"], "symbol evidence registry.inventory_sha256"
    )
    if inventory_hash != inventory.content_sha256:
        raise ConfigurationError("symbol evidence registry inventory hash is stale")
    entries = tuple(
        _load_symbol_evidence(item, index)
        for index, item in enumerate(_sequence(root["entries"], "symbol evidence registry.entries"))
    )
    registry = SymbolEvidenceRegistry(commit, inventory_hash, entries)
    _validate_symbol_evidence(
        registry,
        inventory,
        root["summary"],
        repository_root=repository_root,
    )
    if _hash(root["content_sha256"], "symbol evidence registry.content_sha256") != registry.content_sha256:
        raise ConfigurationError("symbol evidence registry content hash is invalid")
    return registry


def load_scope_decisions(
    path: Path,
    inventory: PublicSymbolInventory,
    *,
    repository_root: Path,
) -> ScopeDecisionRegistry:
    root = _json_mapping(path, "scope decision registry")
    _keys(
        root,
        required={
            "schema",
            "upstream_commit",
            "inventory_sha256",
            "content_sha256",
            "decisions",
            "summary",
        },
        optional=set(),
        context="scope decision registry",
    )
    if _text(root["schema"], "scope decision registry.schema") != SCOPE_DECISIONS_SCHEMA:
        raise ConfigurationError(
            f"scope decision registry.schema must be '{SCOPE_DECISIONS_SCHEMA}'"
        )
    commit = _text(root["upstream_commit"], "scope decision registry.upstream_commit").lower()
    if commit != inventory.upstream_commit:
        raise ConfigurationError("scope decision registry commit does not match public inventory")
    inventory_hash = _hash(
        root["inventory_sha256"], "scope decision registry.inventory_sha256"
    )
    if inventory_hash != inventory.content_sha256:
        raise ConfigurationError("scope decision registry inventory hash is stale")
    decisions = tuple(
        _load_scope_decision(item, index)
        for index, item in enumerate(_sequence(root["decisions"], "scope decision registry.decisions"))
    )
    registry = ScopeDecisionRegistry(commit, inventory_hash, decisions)
    _validate_scope_decisions(
        registry,
        inventory,
        root["summary"],
        repository_root=repository_root,
    )
    if _hash(root["content_sha256"], "scope decision registry.content_sha256") != registry.content_sha256:
        raise ConfigurationError("scope decision registry content hash is invalid")
    return registry


def load_evidence_results(
    path: Path,
    inventory: PublicSymbolInventory,
    symbol_evidence: SymbolEvidenceRegistry,
    *,
    repository_root: Path,
) -> EvidenceResults:
    """Load one deterministic assertion-result artifact emitted by a test collector."""

    root = _json_mapping(path, "evidence results")
    _keys(
        root,
        required={
            "schema",
            "upstream_commit",
            "inventory_sha256",
            "symbol_evidence_sha256",
            "content_sha256",
            "collector",
            "assertions",
            "summary",
            "target_framework",
        },
        optional=set(),
        context="evidence results",
    )
    if _text(root["schema"], "evidence results.schema") != EVIDENCE_RESULTS_SCHEMA:
        raise ConfigurationError(
            f"evidence results.schema must be '{EVIDENCE_RESULTS_SCHEMA}'"
        )
    commit = _text(root["upstream_commit"], "evidence results.upstream_commit").lower()
    if commit != inventory.upstream_commit:
        raise ConfigurationError("evidence results commit does not match public inventory")
    inventory_hash = _hash(root["inventory_sha256"], "evidence results.inventory_sha256")
    if inventory_hash != inventory.content_sha256:
        raise ConfigurationError("evidence results inventory hash is stale")
    symbol_evidence_hash = _hash(
        root["symbol_evidence_sha256"],
        "evidence results.symbol_evidence_sha256",
    )
    if symbol_evidence_hash != symbol_evidence.content_sha256:
        raise ConfigurationError(
            "evidence results symbol-evidence registry hash is stale"
        )
    collector = _mapping(root["collector"], "evidence results.collector")
    _keys(
        collector,
        required={"path", "symbol", "source_sha256"},
        optional=set(),
        context="evidence results.collector",
    )
    collector_path = _exact_path(collector["path"], "evidence results.collector.path")
    collector_symbol = _exact_symbol(
        collector["symbol"], "evidence results.collector.symbol"
    )
    collector_source_sha256 = _hash(
        collector["source_sha256"], "evidence results.collector.source_sha256"
    )
    assertions = tuple(
        _load_executed_assertion(item, index)
        for index, item in enumerate(_sequence(root["assertions"], "evidence results.assertions"))
    )
    identifiers = [item.assertion_id for item in assertions]
    if identifiers != sorted(identifiers) or len(identifiers) != len(set(identifiers)):
        raise ConfigurationError("evidence result assertions must be unique and sorted by assertion_id")
    results = EvidenceResults(
        commit,
        inventory_hash,
        symbol_evidence_hash,
        collector_path,
        collector_symbol,
        collector_source_sha256,
        assertions,
        _framework(root["target_framework"], "evidence results.target_framework"),
    )
    if repository_root is not None:
        repository_state = _git_head_repository_state(
            repository_root.resolve(strict=True).as_posix()
        )
        _validate_source_binding(
            repository_root,
            collector_path,
            collector_symbol,
            collector_source_sha256,
            "evidence results.collector",
            repository_state,
        )
        for index, assertion in enumerate(assertions):
            _validate_source_binding(
                repository_root,
                assertion.test_path,
                assertion.test_symbol,
                assertion.test_source_sha256,
                f"evidence results.assertions[{index}]",
                repository_state,
            )
    if _mapping(root["summary"], "evidence results.summary") != results.to_data()["summary"]:
        raise ConfigurationError("evidence results.summary is inconsistent with assertions")
    if _hash(root["content_sha256"], "evidence results.content_sha256") != results.content_sha256:
        raise ConfigurationError("evidence results content hash is invalid")
    return results


def evaluate_evidence_execution(
    registry: SymbolEvidenceRegistry,
    required_assertion_ids: Sequence[str],
    result_sets: Sequence[EvidenceResults],
) -> EvidenceExecution:
    """Match required exact assertions to actually collected pass artifacts.

    Registry declarations never promote a matrix entry.  A required assertion is
    verified only when a collector artifact carries the same assertion id and
    deterministic output hash, and the execution is neither skipped nor
    structural-only.
    """

    required = tuple(sorted(set(required_assertion_ids)))
    if len(required) != len(tuple(required_assertion_ids)):
        raise ConfigurationError("required evidence assertion ids must be unique")
    definitions = registry.receipts_by_id
    unknown_required = next((item for item in required if item not in definitions), None)
    if unknown_required is not None:
        raise ConfigurationError(
            f"required evidence assertion '{unknown_required}' is not declared in symbol evidence"
        )
    collected: dict[str, ExecutedAssertion] = {}
    authoritative_by_assertion: dict[str, bool] = {}
    result_artifact_hashes: list[str] = []
    result_artifacts: list[Mapping[str, Any]] = []
    target_frameworks: set[str] = set()
    for result_set in result_sets:
        if result_set.upstream_commit != registry.upstream_commit:
            raise ConfigurationError(
                "evidence results commit does not match symbol evidence"
            )
        if result_set.inventory_sha256 != registry.inventory_sha256:
            raise ConfigurationError(
                "evidence results inventory hash does not match symbol evidence"
            )
        if result_set.symbol_evidence_sha256 != registry.content_sha256:
            raise ConfigurationError(
                "evidence results symbol-evidence registry hash is stale"
            )
        # A JSON-loaded EvidenceResults instance is declaration-only.  The
        # trusted collector returns an in-memory subclass carrying a
        # process-local HMAC seal after independently validating its fresh
        # build/TRX/session receipt.  Import lazily to keep evidence parsing
        # independent from the collector implementation.
        from .trusted_collector import (
            authority_artifact_trace,
            authority_receipt_sha256,
            is_authoritative_evidence_results,
        )

        trusted = is_authoritative_evidence_results(result_set)
        authority_hash = authority_receipt_sha256(result_set)
        trace = authority_artifact_trace(result_set)
        if trace is not None:
            result_artifacts.append(trace)
        target_frameworks.add(result_set.target_framework)
        result_artifact_hashes.append(
            authority_hash if authority_hash is not None else result_set.content_sha256
        )
        for assertion in result_set.assertions:
            if assertion.assertion_id in collected:
                raise ConfigurationError(
                    f"evidence assertion '{assertion.assertion_id}' was collected more than once"
                )
            if assertion.assertion_id not in definitions:
                raise ConfigurationError(
                    f"evidence results contain undeclared assertion '{assertion.assertion_id}'"
                )
            collected[assertion.assertion_id] = assertion
            authoritative_by_assertion[assertion.assertion_id] = trusted

    missing: list[str] = []
    failed: list[str] = []
    skipped: list[str] = []
    structural: list[str] = []
    hash_mismatch: list[str] = []
    load_mismatch: list[str] = []
    test_binding_mismatch: list[str] = []
    for identifier in required:
        expected = definitions[identifier]
        actual = collected.get(identifier)
        if actual is None:
            missing.append(identifier)
            continue
        if actual.outcome != "passed":
            failed.append(identifier)
        if actual.skipped:
            skipped.append(identifier)
        if actual.structural_only:
            structural.append(identifier)
        if actual.output_sha256 != expected.expected_output_sha256:
            hash_mismatch.append(identifier)
        if (
            actual.test_path != expected.test_path
            or actual.test_symbol != expected.test_symbol
            or actual.test_source_sha256 != expected.test_source_sha256
        ):
            test_binding_mismatch.append(identifier)
        if actual.exercised_load != expected.exercised_load or (
            expected.claims_active_load and actual.exercised_load != "nonzero"
        ):
            load_mismatch.append(identifier)
    authoritative = bool(required) and all(
        authoritative_by_assertion.get(identifier, False)
        for identifier in required
    )
    return EvidenceExecution(
        authoritative,
        required,
        tuple(sorted(collected)),
        tuple(sorted(result_artifact_hashes)),
        tuple(sorted(result_artifacts, key=lambda item: item["session_id"])),
        tuple(sorted(target_frameworks)),
        tuple(missing),
        tuple(failed),
        tuple(skipped),
        tuple(structural),
        tuple(hash_mismatch),
        tuple(load_mismatch),
        tuple(test_binding_mismatch),
    )


def render_symbol_evidence(registry: SymbolEvidenceRegistry) -> str:
    return json.dumps(registry.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def render_scope_decisions(registry: ScopeDecisionRegistry) -> str:
    return json.dumps(registry.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def render_evidence_results(results: EvidenceResults) -> str:
    return json.dumps(results.to_data(), ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def validate_repository_manifest_paths(
    repository_root: Path,
    paths: Sequence[Path],
) -> tuple[str, ...]:
    """Require every authoritative manifest to be an exact clean HEAD blob."""

    root = repository_root.resolve(strict=True)
    repository_state = _git_head_repository_state(root.as_posix())
    relative_paths: list[str] = []
    for index, path in enumerate(paths):
        try:
            relative = path.resolve(strict=True).relative_to(root).as_posix()
        except (OSError, ValueError) as exception:
            raise ConfigurationError(
                f"authoritative manifest[{index}] must be inside the repository root"
            ) from exception
        exact = _exact_path(relative, f"authoritative manifest[{index}]")
        _resolve_repository_file(
            root,
            exact,
            f"authoritative manifest[{index}]",
            repository_state,
        )
        relative_paths.append(exact)
    return tuple(relative_paths)


def _load_symbol_evidence(value: Any, index: int) -> SymbolEvidence:
    context = f"symbol evidence registry.entries[{index}]"
    item = _mapping(value, context)
    _keys(
        item,
        required={
            "path",
            "symbol",
            "upstream_symbol_hash",
            "implementation",
            "receipts",
        },
        optional=set(),
        context=context,
    )
    implementation = _mapping(item["implementation"], f"{context}.implementation")
    _keys(
        implementation,
        required={"path", "symbol", "source_sha256"},
        optional=set(),
        context=f"{context}.implementation",
    )
    receipts = tuple(
        _load_receipt(raw, receipt_index, context)
        for receipt_index, raw in enumerate(_sequence(item["receipts"], f"{context}.receipts"))
    )
    if not receipts:
        raise ConfigurationError(f"{context}.receipts must contain exact passing evidence")
    return SymbolEvidence(
        _exact_path(item["path"], f"{context}.path"),
        _exact_symbol(item["symbol"], f"{context}.symbol"),
        _hash(item["upstream_symbol_hash"], f"{context}.upstream_symbol_hash"),
        _exact_path(implementation["path"], f"{context}.implementation.path"),
        _exact_symbol(implementation["symbol"], f"{context}.implementation.symbol"),
        _hash(
            implementation["source_sha256"],
            f"{context}.implementation.source_sha256",
        ),
        receipts,
    )


def _load_receipt(value: Any, index: int, parent_context: str) -> EvidenceReceipt:
    context = f"{parent_context}.receipts[{index}]"
    item = _mapping(value, context)
    _keys(
        item,
        required={
            "id",
            "test_path",
            "test_symbol",
            "test_source_sha256",
            "assertion",
            "verification_kind",
            "outcome",
            "skipped",
            "structural_only",
            "exercised_load",
            "claims_active_load",
            "expected_output_sha256",
        },
        optional=set(),
        context=context,
    )
    identifier = _identifier(item["id"], f"{context}.id")
    verification_kind = _text(item["verification_kind"], f"{context}.verification_kind")
    if verification_kind not in _VERIFICATION_KINDS:
        raise ConfigurationError(
            f"{context}.verification_kind must be one of {', '.join(sorted(_VERIFICATION_KINDS))}"
        )
    outcome = _text(item["outcome"], f"{context}.outcome")
    skipped = _boolean(item["skipped"], f"{context}.skipped")
    structural_only = _boolean(item["structural_only"], f"{context}.structural_only")
    exercised_load = _text(item["exercised_load"], f"{context}.exercised_load")
    if exercised_load not in _LOAD_CASES:
        raise ConfigurationError(
            f"{context}.exercised_load must be one of {', '.join(sorted(_LOAD_CASES))}"
        )
    claims_active_load = _boolean(
        item["claims_active_load"], f"{context}.claims_active_load"
    )
    expected_output_sha256 = _hash(
        item["expected_output_sha256"], f"{context}.expected_output_sha256"
    )
    if outcome != "passed":
        raise ConfigurationError(f"{context} cannot claim equivalence with outcome '{outcome}'")
    if skipped:
        raise ConfigurationError(f"{context} cannot claim equivalence from a skipped test")
    if structural_only:
        raise ConfigurationError(f"{context} cannot claim equivalence from structural-only evidence")
    if claims_active_load and exercised_load != "nonzero":
        raise ConfigurationError(
            f"{context} cannot claim active-load behavior from a zero or non-applicable load case"
        )
    return EvidenceReceipt(
        identifier,
        _exact_path(item["test_path"], f"{context}.test_path"),
        _exact_symbol(item["test_symbol"], f"{context}.test_symbol"),
        _hash(item["test_source_sha256"], f"{context}.test_source_sha256"),
        _text(item["assertion"], f"{context}.assertion"),
        verification_kind,
        outcome,
        skipped,
        structural_only,
        exercised_load,
        claims_active_load,
        expected_output_sha256,
    )


def _load_executed_assertion(value: Any, index: int) -> ExecutedAssertion:
    context = f"evidence results.assertions[{index}]"
    item = _mapping(value, context)
    _keys(
        item,
        required={
            "assertion_id",
            "test_path",
            "test_symbol",
            "test_source_sha256",
            "outcome",
            "skipped",
            "structural_only",
            "exercised_load",
            "output_sha256",
        },
        optional=set(),
        context=context,
    )
    outcome = _text(item["outcome"], f"{context}.outcome")
    if outcome not in {"passed", "failed", "skipped"}:
        raise ConfigurationError(f"{context}.outcome must be passed, failed, or skipped")
    skipped = _boolean(item["skipped"], f"{context}.skipped")
    if (outcome == "skipped") != skipped:
        raise ConfigurationError(f"{context}.outcome and skipped flag are inconsistent")
    exercised_load = _text(item["exercised_load"], f"{context}.exercised_load")
    if exercised_load not in _LOAD_CASES:
        raise ConfigurationError(
            f"{context}.exercised_load must be one of {', '.join(sorted(_LOAD_CASES))}"
        )
    return ExecutedAssertion(
        _identifier(item["assertion_id"], f"{context}.assertion_id"),
        _exact_path(item["test_path"], f"{context}.test_path"),
        _exact_symbol(item["test_symbol"], f"{context}.test_symbol"),
        _hash(item["test_source_sha256"], f"{context}.test_source_sha256"),
        outcome,
        skipped,
        _boolean(item["structural_only"], f"{context}.structural_only"),
        exercised_load,
        _hash(item["output_sha256"], f"{context}.output_sha256"),
    )


def _load_scope_decision(value: Any, index: int) -> ScopeDecision:
    context = f"scope decision registry.decisions[{index}]"
    item = _mapping(value, context)
    _keys(
        item,
        required={
            "id",
            "path",
            "symbol",
            "upstream_symbol_hash",
            "decision",
            "product_contract",
            "rationale",
            "policy_reference",
            "approval",
        },
        optional=set(),
        context=context,
    )
    decision = _text(item["decision"], f"{context}.decision")
    if decision != "out_of_scope":
        raise ConfigurationError(f"{context}.decision must be 'out_of_scope'")
    product_contract = _text(item["product_contract"], f"{context}.product_contract")
    if product_contract != "compiled_rhino_grasshopper_product":
        raise ConfigurationError(
            f"{context}.product_contract must be 'compiled_rhino_grasshopper_product'"
        )
    approval = _text(item["approval"], f"{context}.approval")
    if approval != "approved":
        raise ConfigurationError(f"{context}.approval must be 'approved'")
    policy_reference = _text(item["policy_reference"], f"{context}.policy_reference")
    _reject_broad_reference(policy_reference, f"{context}.policy_reference")
    if "#" not in policy_reference:
        raise ConfigurationError(f"{context}.policy_reference must identify an exact document anchor")
    return ScopeDecision(
        _identifier(item["id"], f"{context}.id"),
        _exact_path(item["path"], f"{context}.path"),
        _exact_symbol(item["symbol"], f"{context}.symbol"),
        _hash(item["upstream_symbol_hash"], f"{context}.upstream_symbol_hash"),
        decision,
        product_contract,
        _text(item["rationale"], f"{context}.rationale"),
        policy_reference,
        approval,
    )


def _validate_symbol_evidence(
    registry: SymbolEvidenceRegistry,
    inventory: PublicSymbolInventory,
    summary_value: Any,
    *,
    repository_root: Path | None,
) -> None:
    exact_keys = [item.exact_key for item in registry.entries]
    if exact_keys != sorted(exact_keys) or len(exact_keys) != len(set(exact_keys)):
        raise ConfigurationError("symbol evidence entries must be unique and sorted by exact symbol key")
    receipt_ids = [receipt.identifier for receipt in registry.receipts]
    if len(receipt_ids) != len(set(receipt_ids)):
        raise ConfigurationError("symbol evidence receipt ids must be globally unique")
    repository_state = (
        None
        if repository_root is None
        else _git_head_repository_state(
            repository_root.resolve(strict=True).as_posix()
        )
    )
    for entry in registry.entries:
        receipt_order = [item.identifier for item in entry.receipts]
        if receipt_order != sorted(receipt_order):
            raise ConfigurationError(
                f"symbol evidence receipts for '{entry.path}::{entry.symbol}' must be sorted by id"
            )
        upstream = inventory.symbols_by_key.get(entry.key)
        if upstream is None:
            raise ConfigurationError(
                f"symbol evidence identifies unknown symbol '{entry.path}::{entry.symbol}'"
            )
        if upstream.symbol_hash != entry.upstream_symbol_hash:
            raise ConfigurationError(
                f"symbol evidence for '{entry.path}::{entry.symbol}' has a stale upstream symbol hash"
            )
        if repository_root is not None:
            _validate_source_binding(
                repository_root,
                entry.implementation_path,
                entry.implementation_symbol,
                entry.implementation_source_sha256,
                f"symbol evidence for '{entry.path}::{entry.symbol}'.implementation",
                repository_state,
            )
            for receipt in entry.receipts:
                _validate_source_binding(
                    repository_root,
                    receipt.test_path,
                    receipt.test_symbol,
                    receipt.test_source_sha256,
                    f"symbol evidence receipt '{receipt.identifier}'",
                    repository_state,
                )
    summary = _mapping(summary_value, "symbol evidence registry.summary")
    expected = registry.to_data()["summary"]
    if summary != expected:
        raise ConfigurationError("symbol evidence registry.summary is inconsistent with entries")


def _validate_scope_decisions(
    registry: ScopeDecisionRegistry,
    inventory: PublicSymbolInventory,
    summary_value: Any,
    *,
    repository_root: Path | None,
) -> None:
    exact_keys = [item.exact_key for item in registry.decisions]
    if exact_keys != sorted(exact_keys) or len(exact_keys) != len(set(exact_keys)):
        raise ConfigurationError("scope decisions must be unique and sorted by exact symbol key")
    identifiers = [item.identifier for item in registry.decisions]
    if len(identifiers) != len(set(identifiers)):
        raise ConfigurationError("scope decision ids must be unique")
    repository_state = (
        None
        if repository_root is None
        else _git_head_repository_state(
            repository_root.resolve(strict=True).as_posix()
        )
    )
    for decision in registry.decisions:
        upstream = inventory.symbols_by_key.get(decision.key)
        if upstream is None:
            raise ConfigurationError(
                f"scope decision identifies unknown symbol '{decision.path}::{decision.symbol}'"
            )
        if upstream.symbol_hash != decision.upstream_symbol_hash:
            raise ConfigurationError(
                f"scope decision for '{decision.path}::{decision.symbol}' has a stale upstream symbol hash"
            )
        if repository_root is not None:
            _validate_policy_reference(
                repository_root,
                decision.policy_reference,
                f"scope decision '{decision.identifier}'.policy_reference",
                repository_state,
            )
    summary = _mapping(summary_value, "scope decision registry.summary")
    expected = registry.to_data()["summary"]
    if summary != expected:
        raise ConfigurationError("scope decision registry.summary is inconsistent with decisions")


def _validate_source_binding(
    repository_root: Path,
    relative_path: str,
    symbol: str,
    expected_sha256: str,
    context: str,
    repository_state: Mapping[str, str] | None = None,
) -> None:
    source_path = _resolve_repository_file(
        repository_root,
        relative_path,
        context,
        repository_state,
    )
    actual_sha256 = _sha256_bytes(source_path.read_bytes())
    if actual_sha256 != expected_sha256:
        raise ConfigurationError(
            f"{context} source hash is stale for '{relative_path}'"
        )
    if not _source_declares_symbol(source_path, symbol):
        raise ConfigurationError(
            f"{context} file '{relative_path}' does not declare exact symbol '{symbol}'"
        )


def _validate_policy_reference(
    repository_root: Path,
    reference: str,
    context: str,
    repository_state: Mapping[str, str] | None = None,
) -> None:
    relative_path, anchor = reference.split("#", 1)
    path = _resolve_repository_file(
        repository_root,
        _exact_path(relative_path, context),
        context,
        repository_state,
    )
    try:
        text = path.read_text(encoding="utf-8-sig")
    except (OSError, UnicodeError) as exception:
        raise ConfigurationError(f"Cannot read {context} '{relative_path}': {exception}") from exception
    anchors = {
        _markdown_anchor(match.group(1))
        for line in text.splitlines()
        if (match := re.match(r"^#{1,6}\s+(.+?)\s*#*\s*$", line)) is not None
    }
    if anchor not in anchors:
        raise ConfigurationError(
            f"{context} references missing document anchor '{reference}'"
        )


def _resolve_repository_file(
    repository_root: Path,
    relative_path: str,
    context: str,
    repository_state: Mapping[str, str] | None = None,
) -> Path:
    try:
        root = repository_root.resolve(strict=True)
    except OSError as exception:
        raise ConfigurationError(
            f"Repository root for {context} does not exist: {repository_root}"
        ) from exception
    if not root.is_dir():
        raise ConfigurationError(f"Repository root for {context} is not a directory: {root}")
    relative = PurePosixPath(relative_path)
    candidate = root
    for part in relative.parts:
        candidate = candidate / part
        try:
            metadata = os.lstat(candidate)
        except OSError as exception:
            raise ConfigurationError(
                f"{context} file '{relative_path}' does not exist"
            ) from exception
        reparse_flag = getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)
        if candidate.is_symlink() or (
            reparse_flag and getattr(metadata, "st_file_attributes", 0) & reparse_flag
        ):
            raise ConfigurationError(
                f"{context} file '{relative_path}' must not traverse a symbolic link or reparse point"
            )
    try:
        resolved = candidate.resolve(strict=True)
        resolved.relative_to(root)
    except (OSError, ValueError) as exception:
        raise ConfigurationError(
            f"{context} file '{relative_path}' escapes the repository root"
        ) from exception
    if not resolved.is_file():
        raise ConfigurationError(
            f"{context} path '{relative_path}' must identify one regular file"
        )
    if relative_path.split("/", 1)[0].casefold() in {
        ".git",
        "artifacts",
        "bin",
        "obj",
        "temp",
    }:
        raise ConfigurationError(
            f"{context} file '{relative_path}' must not use an ephemeral output directory"
        )
    head_files = repository_state or _git_head_repository_state(root.as_posix())
    head_blob = head_files.get(relative_path)
    if head_blob is None:
        raise ConfigurationError(
            f"{context} file '{relative_path}' must exist in the repository HEAD tree"
        )
    try:
        working_bytes = resolved.read_bytes()
    except OSError as exception:
        raise ConfigurationError(
            f"Cannot read {context} file '{relative_path}': {exception}"
        ) from exception
    if _git_blob_oid(working_bytes, len(head_blob)) != head_blob:
        raise ConfigurationError(
            f"{context} file '{relative_path}' differs from the repository HEAD tree"
        )
    return resolved


def _git_head_repository_state(
    repository_root: str,
) -> Mapping[str, str]:
    git_environment = {
        key: value
        for key, value in os.environ.items()
        if not key.upper().startswith("GIT_")
    }
    git_environment["GIT_NO_REPLACE_OBJECTS"] = "1"
    try:
        completed = subprocess.run(
            [
                "git",
                "--no-replace-objects",
                "-C",
                repository_root,
                "ls-tree",
                "-r",
                "-z",
                "HEAD",
            ],
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            env=git_environment,
        )
    except OSError as exception:
        raise ConfigurationError(
            f"Cannot inspect Git-tracked evidence files beneath '{repository_root}': {exception}"
        ) from exception
    if completed.returncode != 0:
        detail = completed.stderr.decode("utf-8", errors="replace").strip()
        raise ConfigurationError(
            f"Evidence repository root has no readable HEAD tree: {detail or repository_root}"
        )
    result: dict[str, str] = {}
    for item in completed.stdout.split(b"\0"):
        if not item:
            continue
        try:
            metadata, raw_path = item.split(b"\t", 1)
            _, object_type, raw_identifier = metadata.split(b" ", 2)
        except ValueError as exception:
            raise ConfigurationError(
                "Cannot parse the repository HEAD tree for evidence validation"
            ) from exception
        if object_type != b"blob":
            continue
        result[raw_path.decode("utf-8", errors="surrogateescape")] = raw_identifier.decode(
            "ascii"
        )
    return result


def _git_blob_oid(value: bytes, identifier_length: int) -> str:
    payload = f"blob {len(value)}\0".encode("ascii") + value
    if identifier_length == 40:
        return hashlib.sha1(payload).hexdigest()
    if identifier_length == 64:
        return hashlib.sha256(payload).hexdigest()
    raise ConfigurationError(
        f"Unsupported Git object identifier length: {identifier_length}"
    )


def _source_declares_symbol(path: Path, symbol: str) -> bool:
    try:
        text = path.read_text(encoding="utf-8-sig")
    except (OSError, UnicodeError):
        return False
    suffix = path.suffix.lower()
    if suffix == ".py":
        try:
            tree = ast.parse(
                text,
                filename=path.as_posix(),
                mode="exec",
                type_comments=True,
                feature_version=(3, 12),
            )
        except SyntaxError:
            return False
        return symbol in _python_declared_symbols(tree)
    if suffix == ".cs":
        return _csharp_declares_symbol(text, symbol)
    if suffix == ".ps1":
        # A regex cannot distinguish declarations from block comments or
        # here-strings. Keep PowerShell evidence fail-closed until the gate
        # gains a PowerShell-AST-backed inspector.
        return False
    return False


def _python_declared_symbols(tree: ast.Module) -> set[str]:
    result: set[str] = set()

    def visit(statements: Sequence[ast.stmt], prefix: str = "") -> None:
        for statement in statements:
            if isinstance(statement, (ast.FunctionDef, ast.AsyncFunctionDef)):
                result.add(f"{prefix}.{statement.name}" if prefix else statement.name)
            elif isinstance(statement, ast.ClassDef):
                name = f"{prefix}.{statement.name}" if prefix else statement.name
                result.add(name)
                visit(statement.body, name)
            elif isinstance(statement, (ast.Assign, ast.AnnAssign)):
                targets = (
                    statement.targets
                    if isinstance(statement, ast.Assign)
                    else [statement.target]
                )
                for target in targets:
                    if isinstance(target, ast.Name):
                        result.add(f"{prefix}.{target.id}" if prefix else target.id)

    visit(tree.body)
    return result


def _csharp_declares_symbol(text: str, symbol: str) -> bool:
    masked = _mask_csharp_conditional_regions(_mask_csharp_non_code(text))
    parts = re.split(r"\.|::", symbol)
    declarations = _csharp_type_declarations(masked)
    requested = ".".join(parts)
    operator_metadata_request = parts[-1].startswith("op_")
    type_matches = tuple(
        item
        for item in declarations
        if item.full_name == requested
    )
    if (
        not operator_metadata_request
        and type_matches
        and len({item.full_name for item in type_matches}) == 1
    ):
        return True
    if len(parts) < 2:
        return False
    owner_query = ".".join(parts[:-1])
    owner_matches = tuple(
        item
        for item in declarations
        if item.full_name == owner_query
    )
    if len({item.full_name for item in owner_matches}) != 1:
        return False
    return any(
        item.body is not None
        and (
            not operator_metadata_request
            and _csharp_enum_body_declares_member(item.body, parts[-1])
            if item.kind == "enum"
            else _csharp_type_body_declares_member(item.body, item.name, parts[-1])
        )
        for item in owner_matches
    )


@dataclass(frozen=True)
class _CSharpTypeDeclaration:
    kind: str
    name: str
    full_name: str
    start: int
    opening: int | None
    closing: int | None
    body: str | None


def _csharp_type_declarations(masked: str) -> tuple[_CSharpTypeDeclaration, ...]:
    namespace_pattern = re.compile(
        r"\bnamespace\s+([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*([;{])"
    )
    file_namespace = ""
    namespace_scopes: list[tuple[str, int, int]] = []
    for match in namespace_pattern.finditer(masked):
        if match.group(2) == ";":
            file_namespace = match.group(1)
            continue
        opening = masked.find("{", match.start(2), match.end(2) + 1)
        closing = _matching_csharp_brace(masked, opening)
        if closing is not None:
            namespace_scopes.append((match.group(1), opening, closing))

    type_pattern = re.compile(
        r"\b(class|record(?:\s+(?:class|struct))?|struct|enum|interface|delegate)\s+"
        r"([A-Za-z_][A-Za-z0-9_]*)\b"
    )
    raw: list[tuple[str, str, int, int | None, int | None]] = []
    for match in type_pattern.finditer(masked):
        opening = masked.find("{", match.end())
        terminator = masked.find(";", match.end())
        if opening < 0 or (terminator >= 0 and terminator < opening):
            raw.append((match.group(1), match.group(2), match.start(), None, None))
            continue
        closing = _matching_csharp_brace(masked, opening)
        if closing is not None:
            raw.append((match.group(1), match.group(2), match.start(), opening, closing))

    result: list[_CSharpTypeDeclaration] = []
    for kind, name, start, opening, closing in raw:
        namespace_parts: list[str] = []
        if file_namespace:
            namespace_parts.extend(file_namespace.split("."))
        containing_namespaces = sorted(
            (
                (scope_opening, scope_name)
                for scope_name, scope_opening, scope_closing in namespace_scopes
                if scope_opening < start < scope_closing
            ),
            key=lambda item: item[0],
        )
        for _, scope_name in containing_namespaces:
            namespace_parts.extend(scope_name.split("."))
        containing_types = sorted(
            (
                (other_opening, other_name)
                for _, other_name, other_start, other_opening, other_closing in raw
                if other_opening is not None
                and other_closing is not None
                and other_start != start
                and other_opening < start < other_closing
            ),
            key=lambda item: item[0],
        )
        full_name = ".".join(
            [*namespace_parts, *(item[1] for item in containing_types), name]
        )
        result.append(
            _CSharpTypeDeclaration(
                kind,
                name,
                full_name,
                start,
                opening,
                closing,
                None
                if opening is None or closing is None
                else masked[opening + 1 : closing],
            )
        )
    return tuple(result)


def _csharp_enum_body_declares_member(body: str, leaf: str) -> bool:
    segment_start = 0
    round_depth = 0
    square_depth = 0
    curly_depth = 0
    segments: list[str] = []
    for index, character in enumerate(body):
        if character == "(":
            round_depth += 1
        elif character == ")" and round_depth:
            round_depth -= 1
        elif character == "[":
            square_depth += 1
        elif character == "]" and square_depth:
            square_depth -= 1
        elif character == "{":
            curly_depth += 1
        elif character == "}" and curly_depth:
            curly_depth -= 1
        elif (
            character == ","
            and round_depth == 0
            and square_depth == 0
            and curly_depth == 0
        ):
            segments.append(body[segment_start:index])
            segment_start = index + 1
    segments.append(body[segment_start:])

    for segment in segments:
        without_attributes = re.sub(
            r"^\s*(?:\[[^\]]*\]\s*)*",
            "",
            segment,
        )
        match = re.match(r"([A-Za-z_][A-Za-z0-9_]*)\b", without_attributes)
        if match is not None and match.group(1) == leaf:
            return True
    return False


def _matching_csharp_brace(value: str, opening: int) -> int | None:
    depth = 0
    for index in range(opening, len(value)):
        character = value[index]
        if character == "{":
            depth += 1
        elif character == "}":
            depth -= 1
            if depth == 0:
                return index
    return None


_CSHARP_MEMBER_MODIFIERS = frozenset(
    {
        "abstract",
        "async",
        "const",
        "event",
        "extern",
        "file",
        "in",
        "internal",
        "new",
        "out",
        "override",
        "partial",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "required",
        "scoped",
        "sealed",
        "static",
        "unsafe",
        "virtual",
        "volatile",
    }
)


_CSHARP_PARAMETER_MODIFIERS = frozenset(
    {"in", "out", "params", "readonly", "ref", "scoped", "this"}
)


_CSHARP_RESERVED_KEYWORDS = frozenset(
    {
        "abstract",
        "as",
        "base",
        "bool",
        "break",
        "byte",
        "case",
        "catch",
        "char",
        "checked",
        "class",
        "const",
        "continue",
        "decimal",
        "default",
        "delegate",
        "do",
        "double",
        "else",
        "enum",
        "event",
        "explicit",
        "extern",
        "false",
        "finally",
        "fixed",
        "float",
        "for",
        "foreach",
        "goto",
        "if",
        "implicit",
        "in",
        "int",
        "interface",
        "internal",
        "is",
        "lock",
        "long",
        "namespace",
        "new",
        "null",
        "object",
        "operator",
        "out",
        "override",
        "params",
        "private",
        "protected",
        "public",
        "readonly",
        "ref",
        "return",
        "sbyte",
        "sealed",
        "short",
        "sizeof",
        "stackalloc",
        "static",
        "string",
        "struct",
        "switch",
        "this",
        "throw",
        "true",
        "try",
        "typeof",
        "uint",
        "ulong",
        "unchecked",
        "unsafe",
        "ushort",
        "using",
        "virtual",
        "void",
        "volatile",
        "while",
    }
)


_CSHARP_PREDEFINED_TYPE_KEYWORDS = frozenset(
    {
        "bool",
        "byte",
        "char",
        "decimal",
        "double",
        "dynamic",
        "float",
        "int",
        "long",
        "nint",
        "nuint",
        "object",
        "sbyte",
        "short",
        "string",
        "uint",
        "ulong",
        "ushort",
    }
)


_CSHARP_FORBIDDEN_TYPE_IDENTIFIERS = frozenset(
    {
        *_CSHARP_MEMBER_MODIFIERS,
        *_CSHARP_PARAMETER_MODIFIERS,
        *(_CSHARP_RESERVED_KEYWORDS - _CSHARP_PREDEFINED_TYPE_KEYWORDS),
        "var",
        "where",
    }
)


_CSHARP_DECLARATION_TOKEN = re.compile(
    r"::|@?[A-Za-z_][A-Za-z0-9_]*|[<>,.?*\[\]]"
)


_CSHARP_OPERATOR_METADATA_TOKENS = {
    "op_Addition": ("+", 2),
    "op_Subtraction": ("-", 2),
    "op_Multiply": ("*", 2),
    "op_Division": ("/", 2),
    "op_BitwiseAnd": ("&", 2),
    "op_BitwiseOr": ("|", 2),
    "op_LogicalNot": ("!", 1),
}


def _csharp_type_body_declares_member(body: str, owner: str, leaf: str) -> bool:
    top_level = _mask_nested_csharp_blocks(body)
    operator_descriptor = _CSHARP_OPERATOR_METADATA_TOKENS.get(leaf)
    if operator_descriptor is not None:
        return _csharp_type_body_declares_operator(top_level, *operator_descriptor)
    if leaf.startswith("op_"):
        return False

    occurrence = re.compile(rf"\b{re.escape(leaf)}\b")
    for match in occurrence.finditer(top_level):
        suffix = top_level[match.end() :]
        source_suffix = body[match.end() :]
        next_token = re.match(r"\s*(?:<[^;{}()]*>\s*)?(\(|=>|=|;|,)", suffix)
        if next_token is None:
            continue
        segment_start = top_level.rfind(";", 0, match.start()) + 1
        raw_prefix = top_level[segment_start : match.start()]
        if (
            next_token.group(1) == "("
            and leaf != owner
            and _csharp_tuple_return_method_prefix_is_declaration(raw_prefix)
            and _csharp_method_suffix_is_declaration(
                suffix,
                source_suffix,
                next_token.start(1),
            )
        ):
            return True
        prefix = re.sub(r"\[[^\]]*\]", " ", raw_prefix)
        if any(token in prefix for token in ("=", "=>", "(", ")")):
            continue
        if prefix.rstrip().endswith((".", "::")):
            continue
        identifiers = re.findall(r"\b[A-Za-z_][A-Za-z0-9_]*\b", prefix)
        non_modifiers = [
            token for token in identifiers if token not in _CSHARP_MEMBER_MODIFIERS
        ]
        if next_token.group(1) == "(":
            if leaf == owner:
                if not non_modifiers and not prefix.rstrip().endswith("~"):
                    return True
            elif non_modifiers:
                return True
        elif non_modifiers:
            return True
    return False


def _csharp_tuple_return_method_prefix_is_declaration(prefix: str) -> bool:
    """Recognize an explicitly accessible member's tuple return type.

    The general member recognizer deliberately rejects parentheses in the
    declaration prefix because they commonly indicate an invocation or local
    expression. Tuple return types are the one declaration form that needs
    those parentheses. Keep that exception narrow: require only method
    modifiers before the tuple, an explicit member-only accessibility
    modifier, and a balanced tuple with at least two non-empty type elements.
    """

    candidate = prefix.strip()
    opening = candidate.find("(")
    if opening < 0:
        return False
    modifier_text = candidate[:opening].strip()
    if not modifier_text or re.search(r"[^A-Za-z0-9_\s]", modifier_text):
        return False
    modifiers = modifier_text.split()
    # Keep the exception at the two declaration shapes required by current
    # evidence. Other valid tuple-return forms remain false negatives until a
    # full C# syntax inspector replaces this fail-closed recognizer.
    if tuple(modifiers) != ("public", "static"):
        return False

    closing = _matching_csharp_delimiter(candidate, opening, "(", ")")
    if closing is None or candidate[closing + 1 :].strip():
        return False
    elements = _csharp_tuple_return_elements(candidate[opening + 1 : closing])
    return len(elements) >= 2


def _csharp_tuple_return_elements(value: str) -> tuple[str, ...]:
    elements = _split_csharp_declaration_list(value)
    if len(elements) < 2 or any(
        not _csharp_type_declaration_fragment(element, require_name=False)
        for element in elements
    ):
        return ()
    return elements


def _split_csharp_declaration_list(value: str) -> tuple[str, ...]:
    segments: list[str] = []
    start = 0
    angle_depth = 0
    square_depth = 0
    round_depth = 0
    for index, character in enumerate(value):
        if character == "<":
            angle_depth += 1
        elif character == ">":
            if angle_depth == 0:
                return ()
            angle_depth -= 1
        elif character == "[":
            square_depth += 1
        elif character == "]":
            if square_depth == 0:
                return ()
            square_depth -= 1
        elif character == "(":
            round_depth += 1
        elif character == ")":
            if round_depth == 0:
                return ()
            round_depth -= 1
        elif character in "{};=\"'":
            return ()
        elif (
            character == ","
            and angle_depth == 0
            and square_depth == 0
            and round_depth == 0
        ):
            segments.append(value[start:index].strip())
            start = index + 1
    if angle_depth or square_depth or round_depth:
        return ()
    segments.append(value[start:].strip())
    if any(not segment for segment in segments):
        return ()
    return tuple(segments)


def _csharp_type_declaration_fragment(value: str, *, require_name: bool) -> bool:
    tokens = _csharp_declaration_tokens(value)
    if not tokens:
        return False
    end = _parse_csharp_named_type(tokens, 0)
    if end is None:
        return False
    has_name = end < len(tokens) and _csharp_is_declaration_identifier(tokens[end])
    if has_name:
        end += 1
    return end == len(tokens) and (has_name or not require_name)


def _csharp_declaration_tokens(value: str) -> tuple[str, ...]:
    tokens: list[str] = []
    end = 0
    for match in _CSHARP_DECLARATION_TOKEN.finditer(value):
        if value[end : match.start()].strip():
            return ()
        tokens.append(match.group(0))
        end = match.end()
    if value[end:].strip():
        return ()
    return tuple(tokens)


def _parse_csharp_named_type(tokens: tuple[str, ...], start: int) -> int | None:
    index = start
    has_global_alias = False
    if index + 1 < len(tokens) and tokens[index : index + 2] == ("global", "::"):
        has_global_alias = True
        index += 2
    if index < len(tokens) and tokens[index] in _CSHARP_PREDEFINED_TYPE_KEYWORDS:
        if has_global_alias:
            return None
        index += 1
    else:
        index = _parse_csharp_type_segment(tokens, index)
        if index is None:
            return None
        while index < len(tokens) and tokens[index] == ".":
            if (
                index + 1 >= len(tokens)
                or tokens[index + 1] in _CSHARP_PREDEFINED_TYPE_KEYWORDS
            ):
                return None
            index = _parse_csharp_type_segment(tokens, index + 1)
            if index is None:
                return None
    nullable_seen = False
    while index < len(tokens):
        if tokens[index] == "?":
            if nullable_seen:
                return None
            nullable_seen = True
            index += 1
            continue
        if tokens[index] != "[":
            break
        index += 1
        while index < len(tokens) and tokens[index] == ",":
            index += 1
        if index >= len(tokens) or tokens[index] != "]":
            return None
        index += 1
        nullable_seen = False
    return index


def _parse_csharp_type_segment(tokens: tuple[str, ...], start: int) -> int | None:
    if start >= len(tokens) or not _csharp_is_type_identifier(tokens[start]):
        return None
    index = start + 1
    if index >= len(tokens) or tokens[index] != "<":
        return index
    index += 1
    while True:
        index = _parse_csharp_named_type(tokens, index)
        if index is None or index >= len(tokens):
            return None
        if tokens[index] == ">":
            return index + 1
        if tokens[index] != ",":
            return None
        index += 1


def _csharp_is_type_identifier(value: str) -> bool:
    return bool(
        re.fullmatch(r"@?[A-Za-z_][A-Za-z0-9_]*", value)
        and (
            value.startswith("@")
            or value in _CSHARP_PREDEFINED_TYPE_KEYWORDS
            or value not in _CSHARP_FORBIDDEN_TYPE_IDENTIFIERS
        )
    )


def _csharp_is_declaration_identifier(value: str) -> bool:
    return bool(
        re.fullmatch(r"@?[A-Za-z_][A-Za-z0-9_]*", value)
        and (
            value.startswith("@")
            or (
                value not in _CSHARP_RESERVED_KEYWORDS
                and value != "global"
            )
        )
    )


def _csharp_method_suffix_is_declaration(
    suffix: str,
    source_suffix: str,
    opening: int,
) -> bool:
    if suffix[:opening].strip():
        # Generic tuple-return methods remain unsupported rather than letting a
        # permissive regex mistake malformed type-argument syntax for a method.
        return False
    closing = _matching_csharp_delimiter(suffix, opening, "(", ")")
    source_closing = _matching_csharp_delimiter(source_suffix, opening, "(", ")")
    if closing is None or source_closing != closing:
        return False
    parameters = suffix[opening + 1 : closing]
    if parameters.strip():
        declarations = _split_csharp_declaration_list(parameters)
        if not declarations or any(
            not _csharp_parameter_is_declaration(item) for item in declarations
        ):
            return False
    remainder = suffix[closing + 1 :].lstrip()
    source_remainder = source_suffix[closing + 1 :].lstrip()
    return source_remainder.startswith("{") and remainder.startswith(";")


def _csharp_parameter_is_declaration(value: str) -> bool:
    tokens = _csharp_declaration_tokens(value)
    if not tokens:
        return False
    end = _parse_csharp_named_type(tokens, 0)
    return (
        end is not None
        and end + 1 == len(tokens)
        and _csharp_is_declaration_identifier(tokens[end])
    )


def _csharp_type_body_declares_operator(
    top_level: str,
    token: str,
    arity: int,
) -> bool:
    pattern = re.compile(
        rf"(?<![@.A-Za-z0-9_])operator\s*{re.escape(token)}\s*(\()"
    )
    for match in pattern.finditer(top_level):
        opening = match.start(1)
        closing = _matching_csharp_delimiter(top_level, opening, "(", ")")
        if closing is None:
            continue
        if _csharp_parameter_count(top_level[opening + 1 : closing]) == arity:
            return True
    return False


def _csharp_parameter_count(value: str) -> int:
    if not value.strip():
        return 0
    comma_count = 0
    round_depth = 0
    square_depth = 0
    curly_depth = 0
    angle_depth = 0
    for character in value:
        if character == "(":
            round_depth += 1
        elif character == ")" and round_depth:
            round_depth -= 1
        elif character == "[":
            square_depth += 1
        elif character == "]" and square_depth:
            square_depth -= 1
        elif character == "{":
            curly_depth += 1
        elif character == "}" and curly_depth:
            curly_depth -= 1
        elif character == "<":
            angle_depth += 1
        elif character == ">" and angle_depth:
            angle_depth -= 1
        elif (
            character == ","
            and round_depth == 0
            and square_depth == 0
            and curly_depth == 0
            and angle_depth == 0
        ):
            comma_count += 1
    return comma_count + 1


def _matching_csharp_delimiter(
    value: str,
    opening: int,
    opening_character: str,
    closing_character: str,
) -> int | None:
    depth = 0
    for index in range(opening, len(value)):
        character = value[index]
        if character == opening_character:
            depth += 1
        elif character == closing_character:
            depth -= 1
            if depth == 0:
                return index
    return None


def _mask_nested_csharp_blocks(value: str) -> str:
    result: list[str] = []
    depth = 0
    for character in value:
        if character == "{":
            if depth == 0:
                result.append(";")
            else:
                result.append("\n" if character == "\n" else " ")
            depth += 1
        elif character == "}":
            if depth > 0:
                depth -= 1
            result.append(";" if depth == 0 else " ")
        elif depth == 0:
            result.append(character)
        else:
            result.append("\n" if character == "\n" else " ")
    return "".join(result)


def _mask_csharp_non_code(value: str) -> str:
    result = list(value)
    index = 0
    while index < len(value):
        end = _csharp_non_code_span(value, index)
        if end is None:
            index += 1
            continue
        for masked_index in range(index, end):
            if result[masked_index] not in {"\r", "\n"}:
                result[masked_index] = " "
        index = end
    return "".join(result)


def _csharp_non_code_span(value: str, index: int) -> int | None:
    if value.startswith("//", index):
        newline = value.find("\n", index + 2)
        return len(value) if newline < 0 else newline
    if value.startswith("/*", index):
        closing = value.find("*/", index + 2)
        return len(value) if closing < 0 else closing + 2
    if value[index] == "'":
        return _csharp_regular_string_end(value, index, verbatim=False, quote="'")

    prefix_end = index
    while prefix_end < len(value) and value[prefix_end] == "$":
        prefix_end += 1
    if prefix_end < len(value) and value[prefix_end] == '"':
        quote_count = _same_character_run(value, prefix_end, '"')
        if quote_count >= 3:
            cursor = prefix_end + quote_count
            while cursor < len(value):
                if value[cursor] == '"':
                    closing_count = _same_character_run(value, cursor, '"')
                    if closing_count >= quote_count:
                        return cursor + quote_count
                    cursor += closing_count
                else:
                    cursor += 1
            return len(value)

    regular_prefixes = (
        ("$@\"", True),
        ("@$\"", True),
        ("@\"", True),
        ("$\"", False),
        ("\"", False),
    )
    for prefix, verbatim in regular_prefixes:
        if value.startswith(prefix, index):
            quote_index = index + len(prefix) - 1
            return _csharp_regular_string_end(
                value,
                quote_index,
                verbatim=verbatim,
                quote='"',
            )
    return None


def _csharp_regular_string_end(
    value: str,
    opening: int,
    *,
    verbatim: bool,
    quote: str,
) -> int:
    cursor = opening + 1
    while cursor < len(value):
        character = value[cursor]
        if verbatim and quote == '"' and value.startswith('""', cursor):
            cursor += 2
            continue
        if not verbatim and character == "\\":
            cursor += 2
            continue
        if character == quote:
            return cursor + 1
        cursor += 1
    return len(value)


def _same_character_run(value: str, start: int, character: str) -> int:
    cursor = start
    while cursor < len(value) and value[cursor] == character:
        cursor += 1
    return cursor - start


def _mask_csharp_conditional_regions(value: str) -> str:
    """Conservatively reject declarations controlled by preprocessor symbols.

    Source text alone does not identify the target framework or complete define
    set used for a compiled assembly. Mask every conditional branch so a
    receipt must bind to an unconditional declaration (or, later, a compiled
    artifact inspector) instead of guessing which branch exists at runtime.
    """

    result: list[str] = []
    depth = 0
    for line in value.splitlines(keepends=True):
        directive = re.match(r"^\s*#\s*([A-Za-z_][A-Za-z0-9_]*)\b", line)
        if directive is not None:
            keyword = directive.group(1)
            if keyword == "if":
                depth += 1
            result.append("".join("\n" if character == "\n" else " " for character in line))
            if keyword == "endif" and depth > 0:
                depth -= 1
            continue
        if depth:
            result.append("".join("\n" if character == "\n" else " " for character in line))
        else:
            result.append(line)
    return "".join(result)


def _markdown_anchor(value: str) -> str:
    lowered = value.strip().lower()
    without_markup = re.sub(r"[`*_~]", "", lowered)
    without_punctuation = re.sub(r"[^\w\s-]", "", without_markup, flags=re.UNICODE)
    return re.sub(r"\s+", "-", without_punctuation)


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


def _exact_path(value: Any, context: str) -> str:
    text = _text(value, context)
    _reject_broad_reference(text, context)
    if (
        "\\" in text
        or text.startswith("/")
        or any(character in text for character in _WINDOWS_INVALID_PATH_CHARACTERS)
        or any(ord(character) < 32 for character in text)
    ):
        raise ConfigurationError(f"{context} must be an exact relative POSIX file path")
    path = PurePosixPath(text)
    if not path.parts or any(part in {"", ".", ".."} for part in path.parts):
        raise ConfigurationError(f"{context} contains an invalid path segment")
    for part in path.parts:
        if part.endswith((" ", ".")):
            raise ConfigurationError(
                f"{context} contains a Windows-ambiguous trailing dot or space"
            )
        device_name = part.split(".", 1)[0].casefold()
        if device_name in _WINDOWS_RESERVED_PATH_PARTS:
            raise ConfigurationError(
                f"{context} contains a Windows-reserved path segment"
            )
    if PurePosixPath(text).suffix == "":
        raise ConfigurationError(f"{context} must identify one exact file, not a directory")
    return path.as_posix()


def _exact_symbol(value: Any, context: str) -> str:
    text = _text(value, context)
    _reject_broad_reference(text, context)
    if re.fullmatch(
        r"[A-Za-z_][A-Za-z0-9_]*(?:(?:\.|::)[A-Za-z_][A-Za-z0-9_]*)*",
        text,
    ) is None:
        raise ConfigurationError(
            f"{context} must identify one exact dot- or double-colon-qualified symbol"
        )
    return text


def _reject_broad_reference(value: str, context: str) -> None:
    if any(character in value for character in _BROAD_REFERENCE_CHARACTERS):
        raise ConfigurationError(f"{context} must not contain a glob or broad reference")


def _identifier(value: Any, context: str) -> str:
    text = _text(value, context)
    if _IDENTIFIER.fullmatch(text) is None:
        raise ConfigurationError(f"{context} must be a lowercase hyphenated identifier")
    return text


def _hash(value: Any, context: str) -> str:
    text = _text(value, context)
    if _SHA256.fullmatch(text) is None:
        raise ConfigurationError(f"{context} must be a lowercase sha256 hash")
    return text


def _boolean(value: Any, context: str) -> bool:
    if not isinstance(value, bool):
        raise ConfigurationError(f"{context} must be a boolean")
    return value


def _framework(value: Any, context: str) -> str:
    text = _text(value, context)
    if re.fullmatch(r"net[0-9]+\.[0-9]+-windows", text) is None:
        raise ConfigurationError(
            f"{context} must be an exact Windows target framework"
        )
    return text


def _text(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value.strip() or value != value.strip():
        raise ConfigurationError(f"{context} must be a non-empty trimmed string")
    return value


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


def _sha256_data(value: Any) -> str:
    encoded = json.dumps(
        value,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return _sha256_bytes(encoded)


def _sha256_bytes(value: bytes) -> str:
    return f"sha256:{hashlib.sha256(value).hexdigest()}"
